using System.IO;
using UnityEngine;

/// <summary>
/// ローカルテスト用ビルドの置き場所。Git の管理外（.gitignore の /Builds/）に置く。
/// </summary>
public static class LocalTestBuildPaths
{
    public static string ExecutablePath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "CJLocalTest", "CJLocalTest.exe"));

    /// <summary>追加ウィンドウが最初に開くシーン。ほかのシーンはビルド設定の有効なシーンをすべて含める</summary>
    public const string StartScene = "Assets/Temp/Scenes/HumanMonitorScene.unity";
}
