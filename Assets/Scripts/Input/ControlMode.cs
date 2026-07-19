using UnityEngine;

public enum ControlMode { Swap, Coop }

/// <summary>
/// Resolves the effective control mode: explicit user setting wins,
/// otherwise device heuristics (tablets default to Co-op, phones to Swap).
/// </summary>
public static class ControlModeResolver
{
    public static ControlMode Resolve()
    {
        switch (SaveSystem.ControlMode)
        {
            case SaveSystem.ControlModeSetting.Swap: return ControlMode.Swap;
            case SaveSystem.ControlModeSetting.Coop: return ControlMode.Coop;
        }

        // Keyboard machines (desktop builds and the editor) are two-pilot
        // co-op, always. The size heuristic below exists to tell phones from
        // tablets and misreads a small desktop window / editor game view as
        // a phone; Swap mode is NOT inert on desktop — SwapModeController
        // parks a hero (dimmed + invulnerable) and ExitDoor ferries the
        // parked hero to its door, hijacking the second keyboard player's
        // character (Playtest 6 P0). Explicit Swap above stays honored so
        // swap mode remains testable in the editor.
        if (!Application.isMobilePlatform)
            return ControlMode.Coop;

        // Auto detection (phones default to Swap, tablets to Co-op).
#if UNITY_IOS && !UNITY_EDITOR
        if (SystemInfo.deviceModel.StartsWith("iPad"))
            return ControlMode.Coop;
        return ControlMode.Swap;
#else
        // Screen.dpi is 0 or garbage on a meaningful share of Android
        // devices; only trust the diagonal when dpi is plausible.
        float dpi = Screen.dpi;
        if (dpi > 60f)
        {
            float w = Screen.width / dpi;
            float h = Screen.height / dpi;
            float diagonal = Mathf.Sqrt(w * w + h * h);
            if (diagonal >= 7f)
                return ControlMode.Coop;
        }
        return ControlMode.Swap;
#endif
    }
}
