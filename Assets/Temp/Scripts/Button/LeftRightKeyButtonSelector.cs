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

    const float StickThreshold = 0.5f;
    float previousStickX;

    void Update()
    {
        var gamepad = Gamepad.current;
        float stickX = gamepad != null ? gamepad.leftStick.ReadValue().x : 0f;

        if (selectionGroup != null)
        {
            if (IsSubmitPressed())
                selectionGroup.SubmitSelected();
            else if (IsLeftPressed(stickX) && leftButton != null)
                selectionGroup.Select(leftButton);
            else if (IsRightPressed(stickX) && rightButton != null)
                selectionGroup.Select(rightButton);
        }

        previousStickX = stickX;
    }

    /// <summary>
    /// 決定入力（Space / Enter / Xbox A）。マウスの左クリックはボタン側のクリック処理で扱う
    /// </summary>
    static bool IsSubmitPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
    }

    /// <summary>
    /// 左入力（A / ← / 十字キー左 / 左スティックが閾値をまたいだ瞬間）
    /// </summary>
    bool IsLeftPressed(float stickX)
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame))
            return true;

        var gamepad = Gamepad.current;
        if (gamepad == null)
            return false;

        return gamepad.dpad.left.wasPressedThisFrame
            || (previousStickX > -StickThreshold && stickX <= -StickThreshold);
    }

    /// <summary>
    /// 右入力（D / → / 十字キー右 / 左スティックが閾値をまたいだ瞬間）
    /// </summary>
    bool IsRightPressed(float stickX)
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame))
            return true;

        var gamepad = Gamepad.current;
        if (gamepad == null)
            return false;

        return gamepad.dpad.right.wasPressedThisFrame
            || (previousStickX < StickThreshold && stickX >= StickThreshold);
    }
}
