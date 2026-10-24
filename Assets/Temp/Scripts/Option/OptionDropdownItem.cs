using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ドロップダウンの一覧の 1 項目。クリックとマウスが乗ったことを番号付きで知らせる。
/// </summary>
public class OptionDropdownItem : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private Button button;

    [SerializeField] private TMP_Text label;

    [Tooltip("キーボード・マウスで選んでいる間だけ表示する背景")]
    [SerializeField] private GameObject focus;

    int index;

    public event Action<int> Clicked;

    public event Action<int> Hovered;

    void Awake()
    {
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => Clicked?.Invoke(index));
    }

    public void Setup(string text, int itemIndex)
    {
        label.text = text;
        index = itemIndex;
    }

    public void SetFocused(bool focused) => focus.SetActive(focused);

    /// <summary>キー操作のスクロールで止まったマウスの下に来ただけのときは、選んでいる項目を奪わない</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.delta != Vector2.zero)
            Hovered?.Invoke(index);
    }
}
