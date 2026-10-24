using System;
using UnityEngine.InputSystem;

/// <summary>
/// 保存したプッシュトゥトークのキーの名前を Key に戻す。空や読めない名前なら Key.None（操作の割り当てアセットの初期値を使う）。
/// </summary>
public static class PushToTalkKeyParser
{
    public static Key Parse(string name)
    {
        return !string.IsNullOrEmpty(name) && Enum.TryParse(name, out Key key) ? key : Key.None;
    }
}
