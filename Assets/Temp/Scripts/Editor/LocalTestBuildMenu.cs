using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// ローカルテスト用の追加ウィンドウとして起動するビルドを作成する。
/// 再生開始時は LocalTestAutoBuild が変更を検知して自動で作り直す。
/// </summary>
public static class LocalTestBuildMenu
{
    [MenuItem("Tools/CJ/Local テストビルドを作成")]
    static void BuildFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[LocalTest] 再生中はビルドできません。再生を止めてから実行してください。");
            return;
        }

        Build();
    }

    public static bool Build()
    {
        var options = new BuildPlayerOptions
        {
            scenes = LocalTestBuildPaths.Scenes,
            locationPathName = LocalTestBuildPaths.ExecutablePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            // ビルド中に書き換わったプロジェクト内のファイルを「ビルドより新しい変更」と誤判定しないよう、完了時刻を記録する
            File.SetLastWriteTimeUtc(LocalTestBuildPaths.ExecutablePath, DateTime.UtcNow);
            Debug.Log($"[LocalTest] テストビルドを作成しました: {LocalTestBuildPaths.ExecutablePath}（{report.summary.totalTime:mm\\:ss}）");
            return true;
        }

        Debug.LogError($"[LocalTest] テストビルドに失敗しました: {report.summary.result}（エラー {report.summary.totalErrors} 件）");
        return false;
    }
}
