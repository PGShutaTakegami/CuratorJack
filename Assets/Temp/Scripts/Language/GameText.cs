using UnityEngine;

/// <summary>
/// スクリプトから出す文言（進行・効果・接続・操作の案内・ログなど）の入口。
/// シーンに置かないクラスからも使えるよう、Resources/Language にある言語テーブルをすべて読み、キーを持つテーブルの文言を返す。
/// キーはテーブルごとの接頭辞（game.・title.・net. など）で分けるため、テーブルをまたいで重ねない。
/// キーが無ければキーをそのまま返す（抜けに気づけるように）。
/// </summary>
public static class GameText
{
    public const string ResourceFolder = "Language";

    static LanguageTable[] tables;

    static LanguageTable[] Tables => tables ??= Resources.LoadAll<LanguageTable>(ResourceFolder);

    public static string Get(string key) => Get(key, GameLanguage.Current);

    public static string Get(string key, LanguageCode language)
    {
        foreach (LanguageTable table in Tables)
        {
            if (table.Contains(key))
                return table.Get(key, language);
        }
        return key;
    }

    public static string Format(string key, params object[] args) => string.Format(Get(key), args);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => tables = null;
}
