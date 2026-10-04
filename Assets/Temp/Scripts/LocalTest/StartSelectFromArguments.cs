using System;
using UnityEngine;

/// <summary>
/// 起動引数で画面が指定されていれば、タイトルを経由せずその画面から始める。
/// </summary>
public static class StartSelectFromArguments
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        if (!LaunchArguments.TryGetValue(LaunchArguments.StartSelectKey, out string value))
            return;

        if (Enum.TryParse(value, true, out TitleSelectManager.TitleSelect select))
            TitleSelectionContext.Set(select);
        else
            Debug.LogWarning($"[LaunchArguments] 不明な画面指定です: {value}");
    }
}
