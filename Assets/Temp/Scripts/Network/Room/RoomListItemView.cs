using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 部屋一覧の 1 行。ダブルクリックで入室要求を通知する。
/// </summary>
public class RoomListItemView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text nameText;

    [SerializeField] private TMP_Text personText;

    [SerializeField] private TMP_Text messageText;

    [Tooltip("クリック判定とカーソル時の強調表示に使う背景")]
    [SerializeField] private Image hitArea;

    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0f);

    [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 0.15f);

    const string EmptyMessage = "None";

    public RoomInfo Room { get; private set; }

    public event Action<RoomListItemView> DoubleClicked;

    void Awake()
    {
        if (hitArea != null)
            hitArea.color = normalColor;
    }

    public void Bind(RoomInfo room)
    {
        Room = room;
        nameText.text = room.roomName;
        personText.text = $"{room.playerCount}/{room.capacity}";
        messageText.text = string.IsNullOrEmpty(room.message) ? EmptyMessage : room.message;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
            DoubleClicked?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hitArea != null)
            hitArea.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hitArea != null)
            hitArea.color = normalColor;
    }
}
