using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime-generated level-select grid (replaces the hand-wired buttons).
/// One tile per level: number, lock state from SaveSystem, crystal pips.
/// </summary>
public class LevelSelectGrid : MonoBehaviour
{
    public Sprite lockSprite;   // Assets/Lock.png, wired by editor automation

    void Start()
    {
        int highest = SaveSystem.HighestUnlockedLevel;

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
            BuildTile(level, level <= highest);
    }

    void BuildTile(int level, bool unlocked)
    {
        GameObject tile = new GameObject($"LevelTile_{level}", typeof(RectTransform));
        tile.transform.SetParent(transform, false);

        Image bg = tile.AddComponent<Image>();
        bg.color = unlocked
            ? new Color(0.13f, 0.35f, 0.46f, 0.95f)
            : new Color(0.08f, 0.14f, 0.18f, 0.9f);

        if (unlocked)
        {
            Button button = tile.AddComponent<Button>();
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
            int stars = SaveSystem.StarsForLevel(level);
            GameObject pipsObj = new GameObject("Pips", typeof(RectTransform));
            pipsObj.transform.SetParent(tile.transform, false);
            var pips = pipsObj.AddComponent<TextMeshProUGUI>();
            pips.text = new string('◆', stars) + new string('◇', Mathf.Max(0, 3 - stars));
            pips.fontSize = 36;
            pips.alignment = TextAlignmentOptions.Center;
            pips.color = new Color(0.47f, 0.96f, 0.75f);
            var rect = (RectTransform)pipsObj.transform;
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 16);
            rect.sizeDelta = new Vector2(0, 44);
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
