using TMPro;

/// <summary>
/// 部屋のパスワードに使える文字と長さ（半角英数字 1〜16 文字）。入力欄は伏せ字にする。
/// </summary>
public static class RoomPasswordRule
{
    public const int MaxLength = 16;

    public static bool IsValid(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length > MaxLength)
            return false;

        foreach (char character in password)
        {
            if (!IsAllowed(character))
                return false;
        }

        return true;
    }

    public static void Configure(TMP_InputField inputField)
    {
        inputField.contentType = TMP_InputField.ContentType.Password;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterLimit = MaxLength;
        inputField.onValidateInput = (text, index, added) => IsAllowed(added) ? added : '\0';
    }

    static bool IsAllowed(char character) =>
        (character >= '0' && character <= '9') || (character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z');
}
