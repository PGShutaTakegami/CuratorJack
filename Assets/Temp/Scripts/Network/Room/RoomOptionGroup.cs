using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 検索・作成フォームの選択肢ボタン（人数・パスワードの有無など）から 1 つを選ぶ。選択中のボタンは押された見た目のまま固定する。
/// </summary>
public class RoomOptionGroup : MonoBehaviour
{
    [Serializable]
    struct Option
    {
        public int value;
        public Button button;
        public ButtonManager view;
    }

    [SerializeField] private Option[] options;

    UnityAction[] listeners;

    public int Value { get; private set; }

    public event Action<int> Changed;

    void OnEnable()
    {
        listeners = new UnityAction[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            int value = options[i].value;
            listeners[i] = () => Select(value);
            options[i].button.onClick.AddListener(listeners[i]);
        }
    }

    void OnDisable()
    {
        for (int i = 0; i < options.Length; i++)
            options[i].button.onClick.RemoveListener(listeners[i]);
    }

    /// <summary>ButtonManager は表示されてから元の画像を覚えるため、表示中に呼ぶ。</summary>
    public void Select(int value)
    {
        Value = value;
        foreach (Option option in options)
        {
            if (option.view != null)
                option.view.SetPinned(option.value == value);
        }

        Changed?.Invoke(value);
    }
}
