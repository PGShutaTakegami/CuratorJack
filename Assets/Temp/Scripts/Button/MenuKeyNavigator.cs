using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 縦に並んだメニューのボタンを ↑↓（W/S）で選び、Enter / Space で決定する（Alt+Enter は画面の切り替えに使うため除く）。押せない・表示していないボタンは飛ばす。
/// 選択の表示は ButtonSelectionGroup に任せる（マウスを乗せたボタンも選択される）。メニューを閉じたら選択を外す。
/// </summary>
public class MenuKeyNavigator : MonoBehaviour
{
    [SerializeField] private ButtonSelectionGroup selection;

    [Tooltip("上から順に並べたボタン")]
    [SerializeField] private ButtonManager[] buttons;

    void OnDisable()
    {
        selection.ClearSelection();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            Move(-1);
        else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            Move(1);
        else if (((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) && !DisplayModeToggle.IsAltHeld(keyboard))
            || keyboard.spaceKey.wasPressedThisFrame)
            selection.SubmitSelected();
    }

    /// <summary>選択を direction 方向の次の押せるボタンへ移す（未選択なら端から探す）</summary>
    void Move(int direction)
    {
        int count = buttons.Length;
        int current = System.Array.IndexOf(buttons, selection.SelectedButton);
        int index = current < 0 ? (direction > 0 ? -1 : count) : current;
        for (int i = 0; i < count; i++)
        {
            index = (index + direction + count) % count;
            if (IsAvailable(buttons[index]))
            {
                selection.Select(buttons[index]);
                return;
            }
        }
    }

    static bool IsAvailable(ButtonManager button)
    {
        return button != null && button.gameObject.activeInHierarchy && button.TryGetComponent(out Button target) && target.IsInteractable();
    }
}
