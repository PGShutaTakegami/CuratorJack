using UnityEngine;

/// <summary>対応する表示言語。設定には番号で保存されるため、番号は変えない。</summary>
public enum LanguageCode
{
    Japanese = 0,
    English = 1,
    ChineseSimplified = 2,
    ChineseTraditional = 3
}

/// <summary>表示言語の名前・PC の言語からの選び方・訳が無いときの代わりの選び方</summary>
public static class LanguageCodes
{
    /// <summary>オプションの選択肢の並び（LanguageCode の番号順）。自分の言語を見つけられるよう、それぞれの言語の名前で書く</summary>
    public static readonly string[] NativeNames = { "日本語", "English", "简体中文", "繁體中文" };

    /// <summary>PC の言語に合う表示言語。日本語・中国語以外は英語</summary>
    public static LanguageCode FromSystem(SystemLanguage system)
    {
        switch (system)
        {
            case SystemLanguage.Japanese:
                return LanguageCode.Japanese;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
                return LanguageCode.ChineseSimplified;
            case SystemLanguage.ChineseTraditional:
                return LanguageCode.ChineseTraditional;
            default:
                return LanguageCode.English;
        }
    }

    /// <summary>
    /// 指定言語の訳を選ぶ。空なら、中国語はもう一方の字体 → 英語 → 日本語の順に、入っているものを使う。
    /// </summary>
    public static string Pick(LanguageCode language, string japanese, string english, string chineseSimplified, string chineseTraditional)
    {
        switch (language)
        {
            case LanguageCode.ChineseSimplified:
                return FirstFilled(chineseSimplified, chineseTraditional, english, japanese);
            case LanguageCode.ChineseTraditional:
                return FirstFilled(chineseTraditional, chineseSimplified, english, japanese);
            case LanguageCode.English:
                return FirstFilled(english, japanese);
            default:
                return japanese ?? string.Empty;
        }
    }

    static string FirstFilled(params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrEmpty(candidate))
                return candidate;
        }
        return string.Empty;
    }
}
