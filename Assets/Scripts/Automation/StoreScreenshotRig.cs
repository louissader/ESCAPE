// Capture harness only — never compiled into a player build.
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.IO;
using Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace Escape.Automation
{
    /// <summary>
    /// One accepted App Store screenshot size (landscape), plus the safe-area
    /// insets the matching device reports. Sizes verified 2026-09-06 against
    /// developer.apple.com/help/app-store-connect/reference/screenshot-specifications/
    /// </summary>
    public struct ScreenshotSpec
    {
        public readonly string Slug;
        public readonly int Width;
        public readonly int Height;

        /// <summary>Safe-area inset per side, as a fraction of the frame.
        /// APPROXIMATE — see StoreScreenshotRig's class comment.</summary>
        public readonly float SideInset;
        public readonly float BottomInset;
        public readonly float TopInset;

        public ScreenshotSpec(string slug, int width, int height,
                              float sideInset, float bottomInset, float topInset)
        {
            Slug = slug;
            Width = width;
            Height = height;
            SideInset = sideInset;
            BottomInset = bottomInset;
            TopInset = topInset;
        }

        public bool HasInsets
        {
            get { return SideInset > 0f || BottomInset > 0f || TopInset > 0f; }
        }
    }

    /// <summary>
    /// Shared rig behind BOTH screenshot paths (the play-mode capture test and
    /// the edit-mode Escape menu item). It exists because a bare camera render
    /// is NOT the app: Apple rejected 1.1.0 under Guideline 2.3.3 because every
    /// uploaded shot was a HUD-less world render.
    ///
    /// What it does, all of it undone by Dispose():
    ///  1. Points the camera at a RenderTexture of the requested size FIRST, so
    ///     everything that follows lays out against the capture frame.
    ///  2. Flips every screen-space-OVERLAY canvas to ScreenSpaceCamera on that
    ///     camera (overlay canvases are composited by the display, never by a
    ///     camera, which is why they were missing from the store shots). The
    ///     ScreenFader canvas is deliberately skipped: it is a full-screen black
    ///     Image that would composite over the whole capture.
    ///  3. Pins the touch controls to the SWAP layout — the shipped default on
    ///     every touch device (ControlModeResolver). The editor otherwise
    ///     resolves to Co-op, whose two mirrored clusters are exactly what drew
    ///     the Guideline 4 "crowded" rejection on iPad.
    ///  4. Hides any open modal (pause / settings / win / fail / how-to-play)
    ///     and anything named like a debug overlay.
    ///  5. Forces the CanvasScaler scale factor for the capture size instead of
    ///     trusting the Game view's size.
    ///
    /// KNOWN FIDELITY LIMIT — safe area. A canvas laid out in camera space sizes
    /// itself from the camera's pixel rect (the RenderTexture), but SafeAreaFitter
    /// reads Screen.safeArea, which in the editor is always the full Game view
    /// with zero insets — Screen.SetResolution is a no-op for the editor Game
    /// view, and there is no scripted way to make it report a device's insets
    /// from a batch/test run. So the rig SUBSTITUTES the insets below. They are
    /// close to what the devices report, not exact, and they do not vary by
    /// device within a class. Treat these captures as store-ready content at
    /// exactly-correct pixel sizes; if you need pixel-exact device chrome and
    /// insets, capture on a real device or in the iOS Simulator at the same
    /// sizes instead.
    /// </summary>
    public static class StoreScreenshotRig
    {
        public const string OutputDirectory = "StoreAssets";

        // iPhone 6.9" — accepted: 2868x1320, 2796x1290, 2736x1260 (landscape).
        // 6.7" is no longer a display class; a 6.9" upload covers 6.5" and down.
        // Insets: ~62pt per side and ~21pt bottom of a 956x440pt landscape frame.
        public static readonly ScreenshotSpec IPhone69 =
            new ScreenshotSpec("iphone69", 2868, 1320, 62f / 956f, 21f / 440f, 0f);

        // iPad 13" — accepted: 2752x2064, 2732x2048 (landscape).
        // Insets: home indicator only, ~20pt of a 1376x1032pt landscape frame.
        public static readonly ScreenshotSpec IPad13 =
            new ScreenshotSpec("ipad13", 2752, 2064, 0f, 20f / 1032f, 0f);

        /// <summary>Internal QA reference shot. Matches NO App Store size class
        /// on purpose — never upload it.</summary>
        public static readonly ScreenshotSpec Qa1080 =
            new ScreenshotSpec("qa", 1920, 1080, 0f, 0f, 0f);

        /// <summary>The two sets App Store Connect actually requires.</summary>
        public static readonly ScreenshotSpec[] StoreSpecs = { IPhone69, IPad13 };

        static readonly HashSet<string> ModalNames = new HashSet<string>
        {
            "PauseOverlay", "SettingsPanel", "ResetConfirm", "WinPanel", "FailPanel",
            "HowToPlay", "HowToPlayPanel", "HowToPlayCard",
        };

        public static string PathFor(ScreenshotSpec spec, string prefix, int displayNumber)
        {
            return Path.Combine(OutputDirectory,
                                string.Format("{0}_{1}_level{2}.png", prefix, spec.Slug, displayNumber));
        }

        /// <summary>Opens a capture session. ALWAYS dispose it (try/finally) —
        /// it mutates live canvases and the camera's target texture.</summary>
        public static CaptureSession Begin(Camera cam, ScreenshotSpec spec)
        {
            return new CaptureSession(cam, spec);
        }

        /// <summary>Frames the camera on the two heroes, clamped to the level's
        /// CameraBounds so a capture can never show the void outside the level.
        /// Call AFTER Begin(): the aspect comes from the camera's target
        /// texture. Dispose the result to put the camera back.</summary>
        public static CameraFraming FrameOnHeroes(Camera cam,
                                                  float minOrthoSize = 4.5f,
                                                  float maxOrthoSize = 7.5f,
                                                  float padding = 2f)
        {
            var framing = new CameraFraming(cam);

            Transform p1 = FindHero("Player1");
            Transform p2 = FindHero("Player2");
            if (p1 == null && p2 == null)
            {
                Debug.LogWarning("[Screenshot] no Player1/Player2 — camera left as authored.");
                return framing;
            }

            Vector3 a = p1 != null ? p1.position : p2.position;
            Vector3 b = p2 != null ? p2.position : a;
            Vector3 center = (a + b) * 0.5f + new Vector3(0f, 0.5f, 0f);

            float aspect = cam.pixelHeight > 0
                ? (float)cam.pixelWidth / cam.pixelHeight
                : cam.aspect;

            // Fit both heroes with breathing room, then clamp. The authored
            // vcam runs 7..11.25 ortho; a store shot wants the characters
            // readable, not a wide level diagram.
            float halfHeight = Mathf.Abs(a.y - b.y) * 0.5f + padding;
            float halfWidth = Mathf.Abs(a.x - b.x) * 0.5f + padding;
            float size = Mathf.Clamp(Mathf.Max(halfHeight, halfWidth / aspect),
                                     minOrthoSize, maxOrthoSize);

            center = ClampToCameraBounds(center, size, aspect);

            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);
            return framing;
        }

        static Transform FindHero(string name)
        {
            GameObject go = GameObject.Find(name);
            return go != null ? go.transform : null;
        }

        /// <summary>Keeps the view inside the level's CinemachineConfiner2D
        /// shape (LevelBuilder names it "CameraBounds"), the same rectangle the
        /// running game confines to.</summary>
        static Vector3 ClampToCameraBounds(Vector3 center, float orthoSize, float aspect)
        {
            GameObject boundsObj = GameObject.Find("CameraBounds");
            Collider2D col = boundsObj != null ? boundsObj.GetComponent<Collider2D>() : null;
            if (col == null)
                return center;

            Bounds b = col.bounds;
            if (b.size.x < 1f || b.size.y < 1f)
                return center;   // collider not evaluated (can happen in edit mode)

            float halfHeight = orthoSize;
            float halfWidth = orthoSize * aspect;

            center.x = b.size.x <= halfWidth * 2f
                ? b.center.x
                : Mathf.Clamp(center.x, b.min.x + halfWidth, b.max.x - halfWidth);
            center.y = b.size.y <= halfHeight * 2f
                ? b.center.y
                : Mathf.Clamp(center.y, b.min.y + halfHeight, b.max.y - halfHeight);
            return center;
        }

        // ------------------------------------------------------------------

        public sealed class CameraFraming : System.IDisposable
        {
            readonly Camera cam;
            readonly Vector3 position;
            readonly float orthoSize;
            readonly bool wasOrthographic;
            readonly CinemachineBrain brain;
            readonly bool brainWasEnabled;
            bool disposed;

            internal CameraFraming(Camera cam)
            {
                this.cam = cam;
                position = cam.transform.position;
                orthoSize = cam.orthographicSize;
                wasOrthographic = cam.orthographic;

                // The brain rewrites the transform every LateUpdate; park it so
                // the framing survives the frames we wait for canvas layout.
                brain = cam.GetComponent<CinemachineBrain>();
                brainWasEnabled = brain != null && brain.enabled;
                if (brain != null)
                    brain.enabled = false;
            }

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                if (cam != null)
                {
                    cam.transform.position = position;
                    cam.orthographicSize = orthoSize;
                    cam.orthographic = wasOrthographic;
                }
                if (brain != null)
                    brain.enabled = brainWasEnabled;
            }
        }

        // ------------------------------------------------------------------

        public sealed class CaptureSession : System.IDisposable
        {
            struct CanvasState
            {
                public Canvas Canvas;
                public RenderMode Mode;
                public Camera WorldCamera;
                public float PlaneDistance;
                public float ScaleFactor;
            }

            struct ScalerState
            {
                public CanvasScaler Scaler;
                public bool Enabled;
            }

            struct RectState
            {
                public RectTransform Rect;
                public Vector2 AnchorMin;
                public Vector2 AnchorMax;
                public Vector2 OffsetMin;
                public Vector2 OffsetMax;
                public SafeAreaFitter Fitter;
                public bool FitterEnabled;
            }

            struct AlphaState
            {
                public CanvasGroup Group;
                public float Alpha;
            }

            readonly Camera cam;
            readonly ScreenshotSpec spec;
            readonly RenderTexture rt;
            readonly RenderTexture previousTarget;
            readonly RenderTexture previousActive;

            readonly List<CanvasState> canvases = new List<CanvasState>();
            readonly List<ScalerState> scalers = new List<ScalerState>();
            readonly List<RectState> rects = new List<RectState>();
            readonly List<AlphaState> alphas = new List<AlphaState>();
            readonly List<GameObject> deactivated = new List<GameObject>();
            readonly List<GameObject> activated = new List<GameObject>();
            bool disposed;

            public ScreenshotSpec Spec { get { return spec; } }

            internal CaptureSession(Camera cam, ScreenshotSpec spec)
            {
                this.cam = cam;
                this.spec = spec;

                rt = new RenderTexture(spec.Width, spec.Height, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                previousTarget = cam.targetTexture;
                previousActive = RenderTexture.active;

                // FIRST: a Screen Space - Camera canvas sizes itself from the
                // camera's pixel rect, which is the render texture once this is
                // assigned. Flipping the canvases before this would lay them out
                // against the Game view instead.
                cam.targetTexture = rt;

                try
                {
                    HideModalsAndDebug();
                    PinTouchControlsToSwapLayout();
                    FlipOverlayCanvases();
                    ApplySafeArea();
                    ForceLayout();
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            /// <summary>Renders and writes a PNG. RGB24 — App Store Connect
            /// rejects images carrying an alpha channel.</summary>
            public void Write(string path)
            {
                Canvas.ForceUpdateCanvases();
                cam.Render();

                RenderTexture active = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(spec.Width, spec.Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, spec.Width, spec.Height), 0, 0);
                tex.Apply();
                RenderTexture.active = active;

                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);

                Debug.Log(string.Format("[Screenshot] {0} ({1}x{2})", path, spec.Width, spec.Height));
            }

            // ---------------- setup ----------------

            void FlipOverlayCanvases()
            {
                foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>())
                {
                    if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        continue;

                    // ScreenFader is a DontDestroyOnLoad full-screen black
                    // Image at sortingOrder 999. Rendered into the capture it
                    // would black out the frame mid-fade; leave it on the
                    // display compositor where it cannot reach the RT.
                    if (canvas.GetComponentInParent<ScreenFader>() != null)
                        continue;

                    canvases.Add(new CanvasState
                    {
                        Canvas = canvas,
                        Mode = canvas.renderMode,
                        WorldCamera = canvas.worldCamera,
                        PlaneDistance = canvas.planeDistance,
                        ScaleFactor = canvas.scaleFactor,
                    });

                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = 1f;
                }
            }

            void HideModalsAndDebug()
            {
                foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>())
                    HideMatching(canvas.transform);
            }

            void HideMatching(Transform t)
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    Transform child = t.GetChild(i);
                    if (!child.gameObject.activeSelf)
                        continue;

                    string name = child.name;
                    bool isModal = ModalNames.Contains(name) ||
                                   child.GetComponent<HowToPlayPanel>() != null;
                    bool isDebug = name.IndexOf("debug", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   name.IndexOf("diagnost", System.StringComparison.OrdinalIgnoreCase) >= 0;

                    if (isModal || isDebug)
                    {
                        child.gameObject.SetActive(false);
                        deactivated.Add(child.gameObject);
                        continue;   // its children go with it
                    }

                    HideMatching(child);
                }
            }

            void PinTouchControlsToSwapLayout()
            {
                foreach (TouchControlsUI touch in Object.FindObjectsOfType<TouchControlsUI>(true))
                {
                    SetActive(touch.gameObject, true);
                    if (touch.swapLayout != null)
                        SetActive(touch.swapLayout, true);
                    if (touch.coopLayout != null)
                        SetActive(touch.coopLayout, false);

                    // ModalGuard can have left the controls faded out.
                    CanvasGroup group = touch.GetComponent<CanvasGroup>();
                    if (group != null && group.alpha < 1f)
                    {
                        alphas.Add(new AlphaState { Group = group, Alpha = group.alpha });
                        group.alpha = 1f;
                    }
                }
            }

            void SetActive(GameObject go, bool active)
            {
                if (go == null || go.activeSelf == active)
                    return;
                go.SetActive(active);
                if (active)
                    activated.Add(go);
                else
                    deactivated.Add(go);
            }

            /// <summary>Substitutes device safe-area insets. The editor reports
            /// none (see the class comment), so without this the touch controls
            /// sit hard against the frame edge, where no device would draw them.</summary>
            void ApplySafeArea()
            {
                if (!spec.HasInsets)
                    return;

                foreach (SafeAreaFitter fitter in Object.FindObjectsOfType<SafeAreaFitter>())
                {
                    var rect = fitter.GetComponent<RectTransform>();
                    if (rect == null)
                        continue;

                    rects.Add(new RectState
                    {
                        Rect = rect,
                        AnchorMin = rect.anchorMin,
                        AnchorMax = rect.anchorMax,
                        OffsetMin = rect.offsetMin,
                        OffsetMax = rect.offsetMax,
                        Fitter = fitter,
                        FitterEnabled = fitter.enabled,
                    });

                    fitter.enabled = false;   // else Update() reverts us next frame
                    rect.anchorMin = new Vector2(spec.SideInset, spec.BottomInset);
                    rect.anchorMax = new Vector2(1f - spec.SideInset, 1f - spec.TopInset);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
            }

            /// <summary>Sets each scaler's factor for the CAPTURE size. Camera
            /// -space canvases scale from Canvas.renderingDisplaySize (the RT),
            /// but computing it here removes the dependency on that behaviour
            /// entirely — the harness can't be run headlessly to verify it.</summary>
            void ForceLayout()
            {
                foreach (CanvasScaler scaler in Object.FindObjectsOfType<CanvasScaler>())
                {
                    Canvas canvas = scaler.GetComponent<Canvas>();
                    if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceCamera)
                        continue;

                    if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                        scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight ||
                        scaler.referenceResolution.x <= 0f || scaler.referenceResolution.y <= 0f)
                    {
                        // Anything else: let the scaler do its own thing.
                        scaler.enabled = false;
                        scaler.enabled = true;   // OnEnable() -> Handle()
                        continue;
                    }

                    float logWidth = Mathf.Log(spec.Width / scaler.referenceResolution.x, 2f);
                    float logHeight = Mathf.Log(spec.Height / scaler.referenceResolution.y, 2f);
                    float factor = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));

                    scalers.Add(new ScalerState { Scaler = scaler, Enabled = scaler.enabled });
                    scaler.enabled = false;      // stop it recomputing from the Game view
                    canvas.scaleFactor = factor;
                }

                Canvas.ForceUpdateCanvases();
            }

            // ---------------- teardown ----------------

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;

                for (int i = rects.Count - 1; i >= 0; i--)
                {
                    RectState s = rects[i];
                    if (s.Rect != null)
                    {
                        s.Rect.anchorMin = s.AnchorMin;
                        s.Rect.anchorMax = s.AnchorMax;
                        s.Rect.offsetMin = s.OffsetMin;
                        s.Rect.offsetMax = s.OffsetMax;
                    }
                    if (s.Fitter != null)
                        s.Fitter.enabled = s.FitterEnabled;
                }

                for (int i = scalers.Count - 1; i >= 0; i--)
                    if (scalers[i].Scaler != null)
                        scalers[i].Scaler.enabled = scalers[i].Enabled;

                for (int i = canvases.Count - 1; i >= 0; i--)
                {
                    CanvasState s = canvases[i];
                    if (s.Canvas == null)
                        continue;
                    s.Canvas.renderMode = s.Mode;
                    s.Canvas.worldCamera = s.WorldCamera;
                    s.Canvas.planeDistance = s.PlaneDistance;
                    s.Canvas.scaleFactor = s.ScaleFactor;
                }

                for (int i = alphas.Count - 1; i >= 0; i--)
                    if (alphas[i].Group != null)
                        alphas[i].Group.alpha = alphas[i].Alpha;

                for (int i = deactivated.Count - 1; i >= 0; i--)
                    if (deactivated[i] != null)
                        deactivated[i].SetActive(true);

                for (int i = activated.Count - 1; i >= 0; i--)
                    if (activated[i] != null)
                        activated[i].SetActive(false);

                if (cam != null)
                    cam.targetTexture = previousTarget;
                RenderTexture.active = previousActive;

                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }

                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif
