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

        // Auto detection.
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
