using System.IO;
using UnityEngine;

/// <summary>
/// ローカルテスト用ビルドの置き場所。Git の管理外（.gitignore の /Builds/）に置く。
/// </summary>
public static class LocalTestBuildPaths
{
    public static string ExecutablePath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "CJLocalTest", "CJLocalTest.exe"));

    /// <summary>ビルドに含めるシーン。先頭のシーンから起動する</summary>
    public static readonly string[] Scenes =
    {
        "Assets/Temp/Scenes/HumanMonitorScene.unity",
        "Assets/Temp/Scenes/Temp_Title.unity",
        "Assets/Temp/Scenes/GameScene.unity"
    };
}
