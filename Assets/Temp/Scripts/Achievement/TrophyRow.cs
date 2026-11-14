using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// トロフィー画面の 1 行。解除済みは金色で名前・説明・解除日を出す。
/// 未解除は行全体（背景・文字・アイコン）を灰色にし、隠し実績はさらに名前と説明を伏せる。マウスを乗せると選ばれる。
/// </summary>
public class TrophyRow : MonoBehaviour, IPointerEnterHandler
{
    const string HiddenNameKey = "trophy.hidden_name";
    const string HiddenDescriptionKey = "trophy.hidden_description";
    const string UnlockedAtKey = "trophy.unlocked_at";
    const string LockedKey = "trophy.locked";

    [SerializeField] private Image background;

    [SerializeField] private GameObject highlight;

    [SerializeField] private Image icon;

    [SerializeField] private TMP_Text nameText;

    [SerializeField] private TMP_Text descriptionText;

    [SerializeField] private TMP_Text statusText;

    [SerializeField] private Color unlockedBackground = new Color(0.2f, 0.15f, 0.09f, 0.95f);

    [SerializeField] private Color lockedBackground = new Color(0.16f, 0.16f, 0.16f, 0.95f);

    [SerializeField] private Color unlockedName = new Color(0.95f, 0.82f, 0.55f, 1f);

    [SerializeField] private Color unlockedDescription = new Color(0.92f, 0.88f, 0.8f, 1f);

    [SerializeField] private Color lockedText = new Color(0.55f, 0.55f, 0.55f, 1f);

    public event Action<TrophyRow> FocusRequested;

    public void Show(AchievementCatalog catalog, AchievementDefinition definition, bool unlocked, DateTime unlockedAt)
    {
        bool masked = !unlocked && definition.IsHidden;
        icon.sprite = catalog.IconOf(definition, unlocked);
        nameText.text = masked ? catalog.Text(HiddenNameKey) : catalog.NameOf(definition);
        descriptionText.text = masked ? catalog.Text(HiddenDescriptionKey) : catalog.DescriptionOf(definition);
        statusText.text = unlocked ? catalog.Format(UnlockedAtKey, unlockedAt) : catalog.Text(LockedKey);

        background.color = unlocked ? unlockedBackground : lockedBackground;
        nameText.color = unlocked ? unlockedName : lockedText;
        descriptionText.color = unlocked ? unlockedDescription : lockedText;
        statusText.color = unlocked ? unlockedName : lockedText;
    }

    public void SetHighlighted(bool highlighted) => highlight.SetActive(highlighted);

    public void OnPointerEnter(PointerEventData eventData) => FocusRequested?.Invoke(this);
}
