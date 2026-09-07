using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Music/SFX volume, control mode, and the only sanctioned progress reset
/// (two-tap confirm). Backed entirely by SaveSystem.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Toggle swapToggle;
    [SerializeField] private Toggle coopToggle;
    [SerializeField] private Toggle hapticsToggle;
    [SerializeField] private GameObject hapticsLabel;
    [SerializeField] private GameObject resetConfirm;

    void OnEnable()
    {
        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(SaveSystem.MusicVolume);
        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(SaveSystem.SfxVolume);

        ControlMode mode = ControlModeResolver.Resolve();
        if (swapToggle != null)
            swapToggle.SetIsOnWithoutNotify(mode == ControlMode.Swap);
        if (coopToggle != null)
            coopToggle.SetIsOnWithoutNotify(mode == ControlMode.Coop);
        // The haptics row is hidden wherever the native bridge cannot fire —
        // every iPad (no Taptic Engine), Android and the editor. One binary
        // ships to iPhone and iPad, so this cannot be baked by the builder:
        // it has to be decided here, at runtime, on the device. A visible
        // switch that does nothing is exactly the Guideline 4 complaint that
        // got v1.1.0 rejected on an iPad Air 11-inch (M3).
        bool hapticsAvailable = Haptics.IsSupported;
        // Fallback for a GameplayUI.prefab baked before hapticsLabel existed:
        // without it the toggle would hide and leave an orphan "HAPTICS"
        // caption. HUDBuilder wires the field properly on the next rebake.
        if (hapticsLabel == null)
        {
            Transform found = transform.Find("HapticsLabel");
            if (found != null)
                hapticsLabel = found.gameObject;
        }
        if (hapticsToggle != null)
        {
            hapticsToggle.SetIsOnWithoutNotify(SaveSystem.HapticsEnabled);
            hapticsToggle.gameObject.SetActive(hapticsAvailable);
        }
        if (hapticsLabel != null)
            hapticsLabel.SetActive(hapticsAvailable);
        if (resetConfirm != null)
            resetConfirm.SetActive(false);
    }

    public void OnMusicChanged(float value)
    {
        SaveSystem.MusicVolume = value;
        if (AudioManager.Instance != null)
            AudioManager.Instance.RefreshMusicVolume();
    }

    public void OnSfxChanged(float value)
    {
        SaveSystem.SfxVolume = value;
        AudioManager.PlaySfx("ui_tap");
    }

    public void OnSwapModeSelected(bool on)
    {
        if (on)
            SaveSystem.ControlMode = SaveSystem.ControlModeSetting.Swap;
    }

    public void OnCoopModeSelected(bool on)
    {
        if (on)
            SaveSystem.ControlMode = SaveSystem.ControlModeSetting.Coop;
    }

    public void OnHapticsToggled(bool on)
    {
        SaveSystem.HapticsEnabled = on;
        // Instant confirmation tick (no-op when just turned off).
        Haptics.SelectionTick();
    }

    public void OnResetProgress()
    {
        if (resetConfirm != null)
            resetConfirm.SetActive(true);
    }

    public void OnResetConfirmed()
    {
        SaveSystem.ResetProgress();
        if (resetConfirm != null)
            resetConfirm.SetActive(false);
    }

    public void OnResetCancelled()
    {
        if (resetConfirm != null)
            resetConfirm.SetActive(false);
    }

    public void OnClose()
    {
        gameObject.SetActive(false);
    }
}
