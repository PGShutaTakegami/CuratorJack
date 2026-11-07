using UnityEngine.InputSystem;

/// <summary>
/// コントローラーのボタン入力をまとめて読む。
/// 左スティックは疑似カーソル（VirtualCursor）に使うため、画面ごとの選択は十字キーで行う。
/// 疑似カーソルが出ている間の A はクリックとして使うため、決定としては扱わない。
/// </summary>
public static class ControllerInput
{
    static Gamepad Pad => Gamepad.current;

    /// <summary>決定（A）。疑似カーソルが出ている間はクリックに使うので false</summary>
    public static bool SubmitPressed => Pad != null && Pad.buttonSouth.wasPressedThisFrame && !VirtualCursor.IsVisible;

    /// <summary>戻る・降りる（B）</summary>
    public static bool BackPressed => Pad != null && Pad.buttonEast.wasPressedThisFrame;

    /// <summary>ポーズ（Start）</summary>
    public static bool PausePressed => Pad != null && Pad.startButton.wasPressedThisFrame;

    /// <summary>補助（Y）</summary>
    public static bool AltPressed => Pad != null && Pad.buttonNorth.wasPressedThisFrame;

    public static bool UpPressed => Pad != null && Pad.dpad.up.wasPressedThisFrame;
    public static bool DownPressed => Pad != null && Pad.dpad.down.wasPressedThisFrame;
    public static bool LeftPressed => Pad != null && Pad.dpad.left.wasPressedThisFrame;
    public static bool RightPressed => Pad != null && Pad.dpad.right.wasPressedThisFrame;

    public static bool UpHeld => Pad != null && Pad.dpad.up.isPressed;
    public static bool DownHeld => Pad != null && Pad.dpad.down.isPressed;
    public static bool LeftHeld => Pad != null && Pad.dpad.left.isPressed;
    public static bool RightHeld => Pad != null && Pad.dpad.right.isPressed;
}
