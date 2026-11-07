using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 自分のアイコン（タイトルで描いた正方形の絵）を PNG としてゲームのデータフォルダに保存・読み込みする。
/// 保存していなければアイコンはない（Current が null）。
/// </summary>
public static class PlayerIconStore
{
    /// <summary>描くアイコンの一辺のピクセル数</summary>
    public const int Size = 128;

    /// <summary>受け取る PNG の最大のバイト数（ネットワークから受け取ったものもこれを超えたら捨てる）</summary>
    public const int MaxBytes = 128 * 1024;

    /// <summary>受け取る画像の一辺の最大のピクセル数</summary>
    public const int MaxDimension = 256;

    const string FileName = "player_icon.png";

    static byte[] current;
    static bool isLoaded;

    /// <summary>保存し直した（アイコンが変わった）</summary>
    public static event Action Changed;

    static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>保存している PNG。なければ null</summary>
    public static byte[] Current
    {
        get
        {
            if (!isLoaded)
            {
                isLoaded = true;
                current = File.Exists(FilePath) ? File.ReadAllBytes(FilePath) : null;
                if (current != null && current.Length > MaxBytes)
                    current = null;
            }
            return current;
        }
    }

    public static void Save(byte[] png)
    {
        if (png == null || png.Length == 0 || png.Length > MaxBytes)
            return;

        File.WriteAllBytes(FilePath, png);
        current = png;
        isLoaded = true;
        Changed?.Invoke();
    }

    /// <summary>PNG からテクスチャを作る。読めない・大きすぎるときは null</summary>
    public static Texture2D CreateTexture(byte[] png, FilterMode filterMode = FilterMode.Bilinear)
    {
        if (png == null || png.Length == 0 || png.Length > MaxBytes)
            return null;

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(png) || texture.width > MaxDimension || texture.height > MaxDimension)
        {
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        texture.filterMode = filterMode;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }
}
