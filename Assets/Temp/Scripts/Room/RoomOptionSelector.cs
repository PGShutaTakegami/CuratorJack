using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 横に並んだ選択肢のボタン（人数 2 / 3 / 4 など）。
/// 選ばれている値のボタンは明るい見た目のままにし、選ばれていないボタンは影を重ねて暗くする。選べない値のボタンは灰色にする。
/// 押された値を知らせるだけで、反映は受け取った側が行う。
/// </summary>
public class RoomOptionSelector : MonoBehaviour
{
    [SerializeField] private MenuButton[] buttons;

    [SerializeField] private ButtonManager[] sprites;

    [Tooltip("buttons と同じ順に並べた、選ばれていないボタンにだけ重ねて暗くする影")]
    [FormerlySerializedAs("selectedMarks")]
    [SerializeField] private GameObject[] unselectedShades;

    [Tooltip("buttons と同じ順に並べた、各ボタンが表す値")]
    [SerializeField] private int[] values;

    public event Action<int> Selected;

    void Awake()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            int value = values[i];
            buttons[i].Button.onClick.AddListener(() => Selected?.Invoke(value));
        }
    }

    public void Show(int selectedValue, Func<int, bool> isAvailable)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            bool isSelected = values[i] == selectedValue;
            buttons[i].SetAvailable(isAvailable(values[i]));
            sprites[i].SetPinned(isSelected);
            unselectedShades[i].SetActive(!isSelected);
        }
    }
}
