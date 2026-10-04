using System;

/// <summary>
/// 起動引数（-key value 形式）を読む。ローカルテスト用ウィンドウの起動設定の受け渡しに使う。
/// </summary>
public static class LaunchArguments
{
    /// <summary>起動時に表示する画面（TitleSelectManager.TitleSelect の名前）</summary>
    public const string StartSelectKey = "-cjStartSelect";

    /// <summary>追加ウィンドウの番号（0 始まり）。ウィンドウの配置に使う</summary>
    public const string WindowIndexKey = "-cjWindowIndex";

    public static bool TryGetValue(string key, out string value)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                value = args[i + 1];
                return true;
            }
        }

        value = null;
        return false;
    }
}
