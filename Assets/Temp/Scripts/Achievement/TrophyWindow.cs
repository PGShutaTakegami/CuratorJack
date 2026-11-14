using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// トロフィー画面。実績の一覧と、解除した数・割合を出す。
/// ↑↓（W/S・十字キー）で行を選び、選んだ行が見える位置まで一覧を送る（マウスのホイールでも送れる）。
/// 戻るボタンでタイトルへ戻る（Esc・B は MonitorManager が受ける）。表示中に解除・言語の切り替えがあれば出し直す。
/// </summary>
public class TrophyWindow : MonoBehaviour
{
    const string ProgressKey = "trophy.progress";

    [SerializeField] private TrophyRow rowPrefab;

    [SerializeField] private ScrollRect rowsScroll;

    [SerializeField] private TMP_Text progressText;

    [Tooltip("解除した割合だけ横に伸ばす（左端を基準にする）")]
    [SerializeField] private RectTransform progressFill;

    [SerializeField] private Button backButton;

    [SerializeField] private MonitorManager monitorManager;

    readonly List<TrophyRow> rows = new List<TrophyRow>();
    int selectedRow;

    void Awake()
    {
        backButton.onClick.AddListener(Back);
    }

    void OnEnable()
    {
        BuildRows();
        Refresh();
        Select(0);
        rowsScroll.verticalNormalizedPosition = 1f;
        GameLanguage.Changed += Refresh;
        AchievementService.Unlocked += HandleUnlocked;
    }

    void OnDisable()
    {
        GameLanguage.Changed -= Refresh;
        AchievementService.Unlocked -= HandleUnlocked;
    }

    void HandleUnlocked(AchievementDefinition definition) => Refresh();

    void BuildRows()
    {
        if (rows.Count > 0)
            return;

        foreach (AchievementDefinition unused in AchievementService.Book.Definitions)
        {
            TrophyRow row = Instantiate(rowPrefab, rowsScroll.content);
            row.FocusRequested += Select;
            rows.Add(row);
        }
    }

    void Refresh()
    {
        AchievementCatalog catalog = AchievementService.Catalog;
        AchievementBook book = AchievementService.Book;
        IReadOnlyList<AchievementDefinition> definitions = book.Definitions;
        for (int i = 0; i < rows.Count && i < definitions.Count; i++)
        {
            AchievementDefinition definition = definitions[i];
            bool unlocked = book.TryGetUnlockedAt(definition.Id, out DateTime unlockedAt);
            rows[i].Show(catalog, definition, unlocked, unlockedAt);
        }

        int total = definitions.Count;
        int unlockedCount = book.UnlockedCount;
        float ratio = total > 0 ? (float)unlockedCount / total : 0f;
        progressText.text = catalog.Format(ProgressKey, unlockedCount, total, Mathf.FloorToInt(ratio * 100f));
        progressFill.anchorMax = new Vector2(ratio, progressFill.anchorMax.y);
    }

    void Select(TrophyRow row)
    {
        int index = rows.IndexOf(row);
        if (index >= 0)
            Select(index);
    }

    void Select(int index)
    {
        if (rows.Count == 0)
            return;

        selectedRow = Mathf.Clamp(index, 0, rows.Count - 1);
        for (int i = 0; i < rows.Count; i++)
            rows[i].SetHighlighted(i == selectedRow);
        Reveal((RectTransform)rows[selectedRow].transform);
    }

    /// <summary>行が一覧の見えている範囲からはみ出していたら、見える位置まで送る</summary>
    void Reveal(RectTransform target)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform viewport = rowsScroll.viewport;
        RectTransform content = rowsScroll.content;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
        Rect view = viewport.rect;
        float shift = 0f;
        if (bounds.max.y > view.yMax)
            shift = bounds.max.y - view.yMax;
        else if (bounds.min.y < view.yMin)
            shift = bounds.min.y - view.yMin;
        if (shift == 0f)
            return;

        float max = Mathf.Max(0f, content.rect.height - view.height);
        rowsScroll.StopMovement();
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(content.anchoredPosition.y - shift, 0f, max));
    }

    void Back()
    {
        if (monitorManager != null)
            monitorManager.ReturnToTitle();
    }

    void Update()
    {
        int vertical = 0;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) vertical = -1;
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) vertical = 1;
        }

        // 左スティックは疑似カーソルに使うため、選択は十字キーだけで行う
        if (ControllerInput.UpPressed) vertical = -1;
        else if (ControllerInput.DownPressed) vertical = 1;

        if (vertical != 0 && rows.Count > 0)
            Select((selectedRow + vertical + rows.Count) % rows.Count);
    }
}
