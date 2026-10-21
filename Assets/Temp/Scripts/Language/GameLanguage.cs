/// <summary>
/// 現在の表示言語。デフォルトは日本語。タイトル／ゲーム内の LanguageTable がここを参照する。
/// </summary>
public static class GameLanguage
{
    public static LanguageCode Current { get; set; } = LanguageCode.Japanese;
}
