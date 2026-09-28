using UnityEngine;

/// <summary>
/// 登録されたボタンのうち 1 つだけを選択状態にする。
/// マウスカーソルが乗ったボタンを選択し、離れたら選択を解除する。
/// キー入力などによる選択は、シーンごとのスクリプトから Select / ClearSelection を呼んで行う。
/// </summary>
public class ButtonSelectionGroup : MonoBehaviour
{
    [SerializeField] private ButtonManager[] buttons;

    /// <summary>
    /// 選択中のボタン（未選択なら null）
    /// </summary>
    public ButtonManager SelectedButton { get; private set; }

    void OnEnable()
    {
        foreach (ButtonManager button in buttons)
        {
            if (button == null)
                continue;

            button.PointerEntered += Select;
            button.PointerExited += OnPointerExited;
        }
    }

    void OnDisable()
    {
        foreach (ButtonManager button in buttons)
        {
            if (button == null)
                continue;

            button.PointerEntered -= Select;
            button.PointerExited -= OnPointerExited;
        }
    }

    void Start()
    {
        ClearSelection();
    }

    /// <summary>
    /// 指定したボタンを選択し、他のボタンの選択を解除する
    /// </summary>
    public void Select(ButtonManager target)
    {
        SelectedButton = target;
        foreach (ButtonManager button in buttons)
        {
            if (button != null)
                button.SetSelected(button == target);
        }
    }

    /// <summary>
    /// 選択中のボタンの処理を実行する。未選択なら何もしない
    /// </summary>
    public void SubmitSelected()
    {
        if (SelectedButton != null)
            SelectedButton.Submit();
    }

    /// <summary>
    /// すべてのボタンを未選択にする
    /// </summary>
    public void ClearSelection()
    {
        Select(null);
    }

    void OnPointerExited(ButtonManager button)
    {
        if (SelectedButton == button)
            ClearSelection();
    }
}
