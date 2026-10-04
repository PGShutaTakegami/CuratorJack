using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 部屋の人数（2〜4 人）を選ぶ。選択中のボタンは押された見た目のまま固定する。
/// </summary>
public class RoomCapacitySelector : MonoBehaviour
{
    [Serializable]
    struct Option
    {
        public int capacity;
        public Button button;
        public ButtonManager view;
    }

    [SerializeField] private Option[] options;

    UnityAction[] listeners;

    /// <summary>選択中の人数（未選択なら 0）</summary>
    public int SelectedCapacity { get; private set; }

    public event Action<int> CapacityChanged;

    void OnEnable()
    {
        listeners = new UnityAction[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            int capacity = options[i].capacity;
            listeners[i] = () => Select(capacity);
            options[i].button.onClick.AddListener(listeners[i]);
        }
    }

    void OnDisable()
    {
        for (int i = 0; i < options.Length; i++)
            options[i].button.onClick.RemoveListener(listeners[i]);
    }

    public void Select(int capacity)
    {
        SelectedCapacity = capacity;
        foreach (Option option in options)
        {
            if (option.view != null)
                option.view.SetPinned(option.capacity == capacity);
        }

        CapacityChanged?.Invoke(capacity);
    }
}
