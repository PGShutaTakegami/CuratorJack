using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アイテムのマス 1 つ分の表示。アイコンが無いアイテムは名前を文字で出す。
/// 使えない間は暗くし、空きのマスは押せない。
/// </summary>
public class ItemSlotView : MonoBehaviour
{
    [SerializeField] private Button button;

    [SerializeField] private Image icon;

    [SerializeField] private TMP_Text nameLabel;

    [SerializeField] private CanvasGroup group;

    [Tooltip("使えない間の濃さ")]
    [SerializeField] private float disabledAlpha = 0.55f;

    bool hasItem;

    public event Action Clicked;

    void Awake()
    {
        button.onClick.AddListener(() => Clicked?.Invoke());
    }

    public void Bind(ItemDefinition item)
    {
        hasItem = item != null;
        Sprite sprite = hasItem ? item.Icon : null;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        nameLabel.text = hasItem && sprite == null ? item.DisplayName : string.Empty;
    }

    public void SetUsable(bool usable)
    {
        button.interactable = usable && hasItem;
        group.alpha = usable ? 1f : disabledAlpha;
    }
}
