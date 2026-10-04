using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ボタンの選択状態を表示に反映する。選択中は switchSprite、非選択時は元の画像を表示する。
/// 選択の判断は行わず、マウスの出入りをイベントで通知する。
/// 選択と決定は ButtonSelectionGroup 側で扱うため、EventSystem のナビゲーション・選択は使わない。
/// </summary>
[RequireComponent(typeof(Image))]
public class ButtonManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Sprite switchSprite;

    Image image;
    Button button;
    Sprite defaultSprite;
    bool isPinned;

    /// <summary>
    /// 選択中かどうか
    /// </summary>
    public bool IsSelected { get; private set; }

    /// <summary>
    /// マウスカーソルがボタンに乗った
    /// </summary>
    public event Action<ButtonManager> PointerEntered;

    /// <summary>
    /// マウスカーソルがボタンから離れた
    /// </summary>
    public event Action<ButtonManager> PointerExited;

    void Awake()
    {
        image = GetComponent<Image>();
        button = GetComponent<Button>();
        defaultSprite = image.sprite;

        // EventSystem の Move 入力（スティック・十字キー・矢印キー）で選択されないようにする
        if (button != null)
            button.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    void LateUpdate()
    {
        // クリックで EventSystem に選択されると Submit 入力で二重に実行されるため、選択を外す
        var eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.currentSelectedGameObject == gameObject)
            eventSystem.SetSelectedGameObject(null);
    }

    /// <summary>
    /// ボタンを押したときと同じ処理を実行する。押せない状態なら何もしない
    /// </summary>
    public void Submit()
    {
        if (button != null && button.IsInteractable())
            button.onClick.Invoke();
    }

    /// <summary>
    /// 選択状態を設定し、画像を切り替える
    /// </summary>
    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        RefreshSprite();
    }

    /// <summary>
    /// カーソルの有無に関係なく switchSprite を表示し続ける（人数選択などの決定済み表示）
    /// </summary>
    public void SetPinned(bool pinned)
    {
        isPinned = pinned;
        RefreshSprite();
    }

    void RefreshSprite()
    {
        if (image == null)
            return;

        image.sprite = (IsSelected || isPinned) && switchSprite != null ? switchSprite : defaultSprite;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        PointerEntered?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PointerExited?.Invoke(this);
    }
}
