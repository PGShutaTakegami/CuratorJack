using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// アイコンのお絵かきの絵（正方形のピクセル。座標は左下が 0）。
/// 線を引く・全部消す・1 つ戻す・1 つ進めるを持つ。画面には依存しない。
/// </summary>
public class IconCanvas
{
    readonly Color32[] pixels;
    readonly LinkedList<Color32[]> undoHistory = new();
    readonly Stack<Color32[]> redoHistory = new();
    readonly int maxHistory;

    public int Size { get; }
    public Color32 Background { get; }

    /// <summary>今の絵（左下から行ごと。テクスチャの SetPixels32 にそのまま渡せる並び）</summary>
    public Color32[] Pixels => pixels;

    public bool CanUndo => undoHistory.Count > 0;
    public bool CanRedo => redoHistory.Count > 0;

    public IconCanvas(int size, Color32 background, int maxHistory)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));

        Size = size;
        Background = background;
        this.maxHistory = Mathf.Max(1, maxHistory);
        pixels = new Color32[size * size];
        Fill(background);
    }

    /// <summary>絵を置き換えて、戻す・進めるの履歴を消す（大きさが違うときは背景だけにする）</summary>
    public void Load(Color32[] source)
    {
        if (source != null && source.Length == pixels.Length)
            Array.Copy(source, pixels, pixels.Length);
        else
            Fill(Background);

        undoHistory.Clear();
        redoHistory.Clear();
    }

    /// <summary>1 本の線を描き始める（ここまでの絵を「1 つ戻す」の先として残す）</summary>
    public void BeginStroke()
    {
        undoHistory.AddLast((Color32[])pixels.Clone());
        if (undoHistory.Count > maxHistory)
            undoHistory.RemoveFirst();
        redoHistory.Clear();
    }

    /// <summary>from から to まで、半径 radius の丸を並べて線を引く</summary>
    public void DrawLine(Vector2 from, Vector2 to, float radius, Color32 color)
    {
        radius = Mathf.Max(0.5f, radius);
        float step = Mathf.Max(0.5f, radius * 0.5f);
        int count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / step));

        for (int i = 0; i <= count; i++)
            Stamp(Vector2.Lerp(from, to, (float)i / count), radius, color);
    }

    public void Clear()
    {
        BeginStroke();
        Fill(Background);
    }

    public bool Undo()
    {
        if (!CanUndo) return false;

        redoHistory.Push((Color32[])pixels.Clone());
        Array.Copy(undoHistory.Last.Value, pixels, pixels.Length);
        undoHistory.RemoveLast();
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;

        undoHistory.AddLast((Color32[])pixels.Clone());
        Array.Copy(redoHistory.Pop(), pixels, pixels.Length);
        return true;
    }

    void Stamp(Vector2 center, float radius, Color32 color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
        int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(center.x + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
        int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(center.y + radius));
        float radiusSqr = radius * radius;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - center.x;
                float dy = y + 0.5f - center.y;
                if (dx * dx + dy * dy <= radiusSqr)
                    pixels[y * Size + x] = color;
            }
        }
    }

    void Fill(Color32 color)
    {
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
    }
}
