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

    public static void ImpactLight()   { if (SaveSystem.HapticsEnabled) _EscapeHapticImpact(0); }
    public static void ImpactMedium()  { if (SaveSystem.HapticsEnabled) _EscapeHapticImpact(1); }
    public static void ImpactHeavy()   { if (SaveSystem.HapticsEnabled) _EscapeHapticImpact(2); }
    public static void Success()       { if (SaveSystem.HapticsEnabled) _EscapeHapticNotify(1); }
    public static void SelectionTick() { if (SaveSystem.HapticsEnabled) _EscapeHapticSelect(); }
}
