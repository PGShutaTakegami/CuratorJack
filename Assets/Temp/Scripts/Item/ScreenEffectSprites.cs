using UnityEngine;

/// <summary>
/// 画面の演出に使う白い画像を実行時に作る（色は Image の color で付ける）。
/// ・EdgeGlow：中央が透明で、画面の端ほど濃くなる枠。
/// ・Bolt：横向きのギザギザの稲妻（芯は濃く、周りはぼかす）。
/// </summary>
public static class ScreenEffectSprites
{
    const int EdgeSize = 256;
    const int BoltWidth = 256;
    const int BoltHeight = 64;

    static Sprite edgeGlow;

    public static Sprite EdgeGlow => edgeGlow != null ? edgeGlow : edgeGlow = CreateEdgeGlow(0.22f);

    /// <summary>edgeWidth（画像の幅に対する割合）の範囲で、端に向かって濃くなる枠</summary>
    static Sprite CreateEdgeGlow(float edgeWidth)
    {
        var texture = NewTexture(EdgeSize, EdgeSize, "EdgeGlow");
        var pixels = new Color32[EdgeSize * EdgeSize];
        for (int y = 0; y < EdgeSize; y++)
        {
            for (int x = 0; x < EdgeSize; x++)
            {
                float u = (x + 0.5f) / EdgeSize;
                float v = (y + 0.5f) / EdgeSize;
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float strength = 1f - Mathf.Clamp01(edge / edgeWidth);
                pixels[y * EdgeSize + x] = new Color32(255, 255, 255, (byte)(strength * strength * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, EdgeSize, EdgeSize), new Vector2(0.5f, 0.5f));
    }

    /// <summary>seed ごとに形の違う稲妻を作る</summary>
    public static Sprite CreateBolt(int seed)
    {
        var random = new System.Random(seed);
        const int points = 9;
        var path = new Vector2[points];
        for (int i = 0; i < points; i++)
        {
            float x = Mathf.Lerp(4f, BoltWidth - 4f, i / (float)(points - 1));
            float y = i == 0 || i == points - 1 ? BoltHeight * 0.5f : BoltHeight * (0.2f + 0.6f * (float)random.NextDouble());
            path[i] = new Vector2(x, y);
        }

        var texture = NewTexture(BoltWidth, BoltHeight, $"Bolt{seed}");
        var pixels = new Color32[BoltWidth * BoltHeight];
        for (int y = 0; y < BoltHeight; y++)
        {
            for (int x = 0; x < BoltWidth; x++)
            {
                float distance = DistanceToPath(new Vector2(x + 0.5f, y + 0.5f), path);
                float core = 1f - Mathf.Clamp01((distance - 1f) / 1.5f);
                float glow = Mathf.Pow(1f - Mathf.Clamp01(distance / 9f), 2f) * 0.6f;
                pixels[y * BoltWidth + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(Mathf.Max(core, glow)) * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, BoltWidth, BoltHeight), new Vector2(0.5f, 0.5f));
    }

    static Texture2D NewTexture(int width, int height, string name)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
    }

    static float DistanceToPath(Vector2 point, Vector2[] path)
    {
        float best = float.MaxValue;
        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector2 a = path[i];
            Vector2 ab = path[i + 1] - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(point, a + ab * t));
        }
        return best;
    }
}
