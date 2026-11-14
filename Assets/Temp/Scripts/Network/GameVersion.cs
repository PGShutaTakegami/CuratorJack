using UnityEngine;

/// <summary>
/// ゲームのバージョン。Project Settings > Player の Version（ビルドに埋め込まれる）をそのまま使う。
/// マルチプレイは同じバージョン同士でしか遊べない（部屋一覧にも出さず、接続も断る）。配るビルドを作るたびに Version を上げること。
/// </summary>
public static class GameVersion
{
    /// <summary>バージョンが違うためホストに断られたときに出す文言</summary>
    public static string MismatchMessage => GameText.Get("net.version_mismatch");

    public static string Current => Application.version;

    public static bool IsCompatible(string version) => !string.IsNullOrEmpty(version) && version == Current;
}
