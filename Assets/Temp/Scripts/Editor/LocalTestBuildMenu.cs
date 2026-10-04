using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// ローカルテスト用の追加ウィンドウとして起動するビルドを作成する。
/// スクリプトやシーンを変更したら作り直す。
/// </summary>
public static class LocalTestBuildMenu
{
    [MenuItem("Tools/CJ/Local テストビルドを作成")]
    static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[LocalTest] 再生中はビルドできません。再生を止めてから実行してください。");
            return;
        }

        var options = new BuildPlayerOptions
        {
            scenes = LocalTestBuildPaths.Scenes,
            locationPathName = LocalTestBuildPaths.ExecutablePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log($"[LocalTest] テストビルドを作成しました: {LocalTestBuildPaths.ExecutablePath}（{report.summary.totalTime:mm\\:ss}）");
        else
            Debug.LogError($"[LocalTest] テストビルドに失敗しました: {report.summary.result}（エラー {report.summary.totalErrors} 件）");
    }
}
