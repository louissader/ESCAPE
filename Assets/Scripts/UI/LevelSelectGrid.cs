using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime-generated level-select grid (replaces the hand-wired buttons).
/// One tile per level: number, lock state from SaveSystem, crystal pips.
/// </summary>
public class LevelSelectGrid : MonoBehaviour
{
    public Sprite lockSprite;       // Assets/Lock.png, wired by editor automation
    public Sprite starFilled;       // gold star (collected)
    public Sprite starEmpty;        // hollow outline star (uncollected)

    void Start()
    {
        int highest = SaveSystem.HighestUnlockedLevel;
        int justCompleted = SaveSystem.ConsumeLastCompleted();

        var grid = gameObject.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(200, 220);
            grid.spacing = new Vector2(28, 28);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.MiddleCenter;
        }

        for (int level = 1; level <= SceneRouter.LevelCount; level++)
            BuildTile(level, level <= highest, level == justCompleted);
    }

    void BuildTile(int level, bool unlocked, bool justCompleted)
    {
        bool completed = SaveSystem.IsLevelCompleted(level);
        GameObject tile = new GameObject($"LevelTile_{level}", typeof(RectTransform));
        tile.transform.SetParent(transform, false);

        Image bg = tile.AddComponent<Image>();
        bg.color = completed
            ? new Color(0.10f, 0.42f, 0.24f, 0.95f)          // green: done!
            : unlocked
                ? new Color(0.13f, 0.35f, 0.46f, 0.95f)
                : new Color(0.08f, 0.14f, 0.18f, 0.9f);

        if (completed)
        {
            // Soft green glow behind the tile + pulse (one-shot pop if the
            // player JUST beat this level — the dopamine moment).
            GameObject glowObj = new GameObject("Glow", typeof(RectTransform));
            glowObj.transform.SetParent(tile.transform, false);
            glowObj.transform.SetAsFirstSibling();
            var glowRect = (RectTransform)glowObj.transform;
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-10, -10);
            glowRect.offsetMax = new Vector2(10, 10);
            Image glowImage = glowObj.AddComponent<Image>();
            glowImage.raycastTarget = false;
            var celebration = tile.AddComponent<LevelTileCelebration>();
            celebration.glow = glowImage;
            celebration.celebrate = justCompleted;
        }

        if (unlocked)
        {
            Button button = tile.AddComponent<Button>();
            // Touch UI: no focus concept, don't latch the "selected" tint.
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            int captured = level;
            button.onClick.AddListener(() =>
            {
                AudioManager.PlaySfx("ui_tap");
                SceneRouter.LoadLevel(captured);
            });
        }

        // Level number.
        GameObject labelObj = new GameObject("Num", typeof(RectTransform));
        labelObj.transform.SetParent(tile.transform, false);
        var label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = level.ToString();
        label.fontSize = 84;
        label.alignment = TextAlignmentOptions.Center;
        label.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.25f);
        Fill(labelObj, new Vector2(0, 20));

        if (!unlocked && lockSprite != null)
        {
            GameObject lockObj = new GameObject("Lock", typeof(RectTransform));
            lockObj.transform.SetParent(tile.transform, false);
            Image lockImage = lockObj.AddComponent<Image>();
            lockImage.sprite = lockSprite;
            lockImage.preserveAspect = true;
            lockImage.raycastTarget = false;
            var rect = (RectTransform)lockObj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(90, 90);
            label.color = new Color(1f, 1f, 1f, 0.1f);
        }

        if (unlocked)
        {
            // Mario-style: ONE star icon per level, always present — hollow
            // until the level's star is collected, then the same icon fills.
            bool hasStar = SaveSystem.StarsForLevel(level) >= 1;
            GameObject starObj = new GameObject("Star", typeof(RectTransform));
            starObj.transform.SetParent(tile.transform, false);
            var rect = (RectTransform)starObj.transform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(56, 56);
            rect.anchoredPosition = new Vector2(0, 12);
            Image star = starObj.AddComponent<Image>();
            star.sprite = hasStar ? starFilled : starEmpty;
            star.preserveAspect = true;
            star.raycastTarget = false;
            if (star.sprite == null)
                star.color = hasStar
                    ? new Color(0.96f, 0.75f, 0.35f)
                    : new Color(1f, 1f, 1f, 0.25f);
        }
    }

    static void Fill(GameObject go, Vector2 offset)
    {
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offset;
        rect.offsetMax = offset;
    }
}
