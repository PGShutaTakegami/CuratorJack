using System;

/// <summary>
/// 現在の表示言語。デフォルトは日本語。タイトル／ゲーム内の LanguageTable がここを参照する。
/// 言語が変わったら Changed で知らせ、表示中の文言を書き換えてもらう。
/// </summary>
public static class GameLanguage
{
    static LanguageCode current = LanguageCode.Japanese;

    public static event Action Changed;

    public static LanguageCode Current
    {
        get => current;
        set
        {
            if (current == value)
                return;

            current = value;
            Changed?.Invoke();
        }
    }
}
