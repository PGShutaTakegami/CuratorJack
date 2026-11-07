using System.Linq;

/// <summary>
/// 他のプレイヤーが決めた文字（名前・部屋名・部屋のメッセージ）を TextMeshPro に出す前に、タグとして解釈される文字を取り除く。
/// 取り除かないと、&lt;size=500%&gt; などで全員の画面の表示を崩せてしまう。
/// </summary>
public static class RichTextGuard
{
    public static bool IsTagCharacter(char character) => character == '<' || character == '>';

    public static string Strip(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text.Any(IsTagCharacter) ? new string(text.Where(character => !IsTagCharacter(character)).ToArray()) : text;
    }
}
