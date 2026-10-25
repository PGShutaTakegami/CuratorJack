using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// オプション画面の 1 行。選ばれている行に印を出し、左右の入力で値を 1 段ずつ変える。
/// マウスが乗ったことを知らせ、どの行を選ぶかは OptionWindow が決める。
/// クリックで EventSystem に選ばれると決定や矢印の入力が二重に効くため、行の中の部品は選ばれたままにしない。
/// </summary>
public abstract class OptionRow : MonoBehaviour, IPointerEnterHandler
{
    [Tooltip("選ばれている間だけ表示する印")]
    [SerializeField] private GameObject highlight;

    /// <summary>この行を選んでほしいとき（マウスが乗った・クリックされた）</summary>
    public event Action<OptionRow> FocusRequested;

    /// <summary>一覧を開いているなど、上下と決定の入力をこの行が受け取る状態か</summary>
    public virtual bool IsExpanded => false;

    protected virtual void Awake()
    {
        foreach (Selectable selectable in GetComponentsInChildren<Selectable>(true))
            selectable.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    void LateUpdate()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.currentSelectedGameObject != null && eventSystem.currentSelectedGameObject.transform.IsChildOf(transform))
            eventSystem.SetSelectedGameObject(null);
    }

    public void SetHighlighted(bool highlighted) => highlight.SetActive(highlighted);

    /// <summary>値を direction（-1 / +1）の向きに 1 段変える</summary>
    public abstract void Step(int direction);

    /// <summary>決定の入力</summary>
    public virtual void Submit() { }

    /// <summary>IsExpanded の間の上下の入力（-1 が上）</summary>
    public virtual void Navigate(int direction) { }

    protected void RequestFocus() => FocusRequested?.Invoke(this);

    /// <summary>一覧が閉じて止まったマウスの下に出ただけのときは、キー操作で選んだ行を奪わない</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.delta != Vector2.zero)
            RequestFocus();
    }
}
