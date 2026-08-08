using System;

/// <summary>
/// Static ref-count of blocking modal overlays (pause menu, win/fail
/// panels; the settings panel rides on the pause state). While any modal
/// is open the touch-controls canvas must stand down: it renders at
/// sortingOrder 100, above GameplayUI's 90, so without this it swallows
/// every tap aimed at a modal button — worst in Coop/iPad, where the
/// layout covers the panels and tapping SETTINGS fired the shoot button.
///
/// Ref-counted rather than a plain bool so overlapping modals (e.g. a
/// fail panel appearing while OnApplicationPause already paused) can
/// never flip the guard off while one of them is still up. State is
/// static and survives scene loads, so every opener must Pop on close
/// AND from OnDestroy if it dies while open.
/// </summary>
public static class ModalGuard
{
    static int openCount;

    /// <summary>True while at least one blocking modal is open.</summary>
    public static bool IsModalOpen => openCount > 0;

    /// <summary>Raised with the new IsModalOpen value whenever it changes
    /// (not on every Push/Pop — only on the 0 to 1 and 1 to 0 edges).</summary>
    public static event Action<bool> OnModalChanged;

    public static void Push()
    {
        openCount++;
        if (openCount == 1)
            OnModalChanged?.Invoke(true);
    }

    public static void Pop()
    {
        // Tolerate a stray pop (scene-teardown paths can overlap) rather
        // than going negative and needing two pushes to guard again.
        if (openCount <= 0)
            return;
        openCount--;
        if (openCount == 0)
            OnModalChanged?.Invoke(false);
    }
}
