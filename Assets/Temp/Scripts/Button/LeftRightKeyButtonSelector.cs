using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 左右入力で左右に配置されたボタンを選択し、決定入力で選択中のボタンを実行する。
/// 選択の方法はシーンごとに変わるため、別の方法が必要なシーンでは別のスクリプトから ButtonSelectionGroup を操作する。
/// </summary>
public class LeftRightKeyButtonSelector : MonoBehaviour
{
    [SerializeField] private ButtonSelectionGroup selectionGroup;

    [SerializeField] private ButtonManager leftButton;

    [SerializeField] private ButtonManager rightButton;

    void Update()
    {
        if (selectionGroup == null)
            return;

        if (IsSubmitPressed())
            selectionGroup.SubmitSelected();
        else if (IsLeftPressed() && leftButton != null)
            selectionGroup.Select(leftButton);
        else if (IsRightPressed() && rightButton != null)
            selectionGroup.Select(rightButton);
    }

    /// <summary>
    /// 決定入力（Space / Enter / Xbox A）。マウス・疑似カーソルの左クリックはボタン側のクリック処理で扱い、Alt+Enter は画面の切り替えに使うため除く
    /// </summary>
    static bool IsSubmitPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || (keyboard.enterKey.wasPressedThisFrame && !DisplayModeToggle.IsAltHeld(keyboard))))
            return true;

        return ControllerInput.SubmitPressed;
    }

    /// <summary>
    /// 左入力（A / ← / 十字キー左）。左スティックは疑似カーソルに使う
    /// </summary>
    static bool IsLeftPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame))
            return true;

        return ControllerInput.LeftPressed;
    }

    /// <summary>
    /// 右入力（D / → / 十字キー右）。左スティックは疑似カーソルに使う
    /// </summary>
    static bool IsRightPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame))
            return true;

        return ControllerInput.RightPressed;
    }
}
