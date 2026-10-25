using UnityEngine.InputSystem;

/// <summary>
/// 操作の割り当て（Assets/Temp/Input/CJControls.inputactions）を、キーボード・コントローラーの組ごとに読み書きする。
/// 割り当ての初期値はアセットで管理し、プレイヤーが変えたキーは上書きとして重ねる（アセットは書き換えない）。
/// </summary>
public static class InputBindings
{
    /// <summary>アセットのキーボード・マウスの組の名前</summary>
    public const string KeyboardGroup = "Keyboard&Mouse";

    /// <summary>アセットのコントローラーの組の名前</summary>
    public const string GamepadGroup = "Gamepad";

    /// <summary>組の最初の割り当て（複合の割り当ては除く）。なければ -1</summary>
    public static int IndexOf(InputAction action, string group)
    {
        if (action == null)
            return -1;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (!binding.isComposite && !binding.isPartOfComposite && InGroup(binding, group))
                return i;
        }
        return -1;
    }

    /// <summary>アセットの初期値の表示名（上書きは含めない）。割り当てがなければ空</summary>
    public static string DefaultDisplayName(InputAction action, string group)
    {
        int index = IndexOf(action, group);
        if (index < 0)
            return string.Empty;

        return InputControlPath.ToHumanReadableString(
            action.bindings[index].path,
            InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
    }

    /// <summary>キーボードの割り当てを key で上書きする。Key.None ならアセットの初期値に戻す</summary>
    public static void OverrideKeyboardKey(InputAction action, Key key)
    {
        int index = IndexOf(action, KeyboardGroup);
        if (index < 0)
            return;

        Keyboard keyboard = Keyboard.current;
        if (key == Key.None || keyboard == null)
            action.RemoveBindingOverride(index);
        else
            action.ApplyBindingOverride(index, $"<Keyboard>/{keyboard[key].name}");
    }

    static bool InGroup(InputBinding binding, string group)
    {
        if (string.IsNullOrEmpty(binding.groups))
            return false;

        foreach (string name in binding.groups.Split(InputBinding.Separator))
        {
            if (name == group)
                return true;
        }
        return false;
    }
}
