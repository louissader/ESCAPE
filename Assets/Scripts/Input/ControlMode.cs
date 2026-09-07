using UnityEngine;

public enum ControlMode { Swap, Coop }

/// <summary>
/// Resolves the effective control mode: explicit user setting wins,
/// otherwise desktop defaults to Co-op and every touch device to Swap.
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
        // co-op, always. This branch must stay ahead of the touch default
        // below, which would otherwise catch a small desktop window / editor
        // game view; Swap mode is NOT inert on desktop — SwapModeController
        // parks a hero (dimmed + invulnerable) and ExitDoor ferries the
        // parked hero to its door, hijacking the second keyboard player's
        // character (Playtest 6 P0). Explicit Swap above stays honored so
        // swap mode remains testable in the editor.
        if (!Application.isMobilePlatform)
            return ControlMode.Coop;

        // Auto detection: every touch device defaults to Swap. Co-op is a
        // deliberate two-player choice, not something a device string should
        // infer. Defaulting iPads to Co-op handed solo App Review testers two
        // mirrored control clusters filling 94% of the screen width, which is
        // what drew the Guideline 4 "crowded ... difficult to use" rejection
        // on 1.1.0 (reviewed on an iPad Air 11-inch M3).
        return ControlMode.Swap;
    }
}
