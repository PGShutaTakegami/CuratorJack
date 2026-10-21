using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 再生開始時、テスト用ビルドより新しい変更があればビルドを作り直してから再生する。
/// 追加ウィンドウ（ビルド）がエディターと同じ内容で動くようにするため。
/// ビルドは再生中に行えないため、いったん再生を取り消し、ビルド後に再生し直す。
/// </summary>
[InitializeOnLoad]
public static class LocalTestAutoBuild
{
    static readonly string[] WatchedFolders = { "Assets", "ProjectSettings" };

    static bool isRebuilding;

    static LocalTestAutoBuild()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    static void HandlePlayModeStateChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode || isRebuilding)
            return;

        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        if (!IsBuildOutdated())
            return;

        EditorApplication.isPlaying = false;
        isRebuilding = true;
        EditorApplication.delayCall += RebuildThenPlay;
    }

    static void RebuildThenPlay()
    {
        try
        {
            Debug.Log("[LocalTest] 変更があるため、追加ウィンドウ用のテストビルドを作り直してから再生します");
            LocalTestBuildMenu.Build();
        }
        finally
        {
            EditorApplication.isPlaying = true;
            EditorApplication.delayCall += () => isRebuilding = false;
        }
    }

    static bool IsBuildOutdated()
    {
        string executable = LocalTestBuildPaths.ExecutablePath;
        if (!File.Exists(executable))
            return true;

        DateTime builtAt = File.GetLastWriteTimeUtc(executable);
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        foreach (string folder in WatchedFolders)
        {
            foreach (string file in Directory.EnumerateFiles(Path.Combine(projectRoot, folder), "*", SearchOption.AllDirectories))
            {
                if (File.GetLastWriteTimeUtc(file) > builtAt)
                    return true;
            }
        }

        return false;
    }
}
