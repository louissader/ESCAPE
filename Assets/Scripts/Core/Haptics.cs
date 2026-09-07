#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Transient haptics, gated on SaveSystem.HapticsEnabled. iOS-only native
/// bridge (Plugins/iOS/EscapeHaptics.mm); no-ops everywhere else (editor,
/// Android, iPads without a Taptic Engine).
/// </summary>
public static class Haptics
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void _EscapeHapticImpact(int strength);
    [DllImport("__Internal")] static extern void _EscapeHapticNotify(int success);
    [DllImport("__Internal")] static extern void _EscapeHapticSelect();
#else
    static void _EscapeHapticImpact(int strength) { }
    static void _EscapeHapticNotify(int success) { }
    static void _EscapeHapticSelect() { }
#endif

    /// <summary>
    /// True only where the native bridge can actually produce a tap. The
    /// bridge is iOS-only, and among iOS devices only iPhones carry a Taptic
    /// Engine — no iPad has one, and the iPod touch 7 (the only non-iPhone
    /// left on iOS 13+) has none either. Every iPhone able to run iOS 13 is
    /// an iPhone 6s (iPhone8,1) or later, so the model family alone is a
    /// complete test; no version parsing and no CoreHaptics link needed.
    /// UI that offers a haptics switch MUST gate on this: the App Review
    /// device for the v1.1.0 Guideline 4 rejection was an iPad Air 11" (M3),
    /// where the VIBRATION toggle was a visible control that did nothing.
    /// </summary>
    public static bool IsSupported
    {
        get
        {
#if UNITY_IOS && !UNITY_EDITOR
            return UnityEngine.SystemInfo.deviceModel.StartsWith("iPhone");
#else
            // Editor, Android and desktop all fall through to the no-op
            // stubs above, so the switch would be inert there too.
            return false;
#endif
        }
    }

    public static void ImpactLight()   { if (SaveSystem.HapticsEnabled) _EscapeHapticImpact(0); }
    public static void ImpactMedium()  { if (SaveSystem.HapticsEnabled) _EscapeHapticImpact(1); }
    public static void ImpactHeavy()   { if (SaveSystem.HapticsEnabled) _EscapeHapticImpact(2); }
    public static void Success()       { if (SaveSystem.HapticsEnabled) _EscapeHapticNotify(1); }
    public static void SelectionTick() { if (SaveSystem.HapticsEnabled) _EscapeHapticSelect(); }
}
