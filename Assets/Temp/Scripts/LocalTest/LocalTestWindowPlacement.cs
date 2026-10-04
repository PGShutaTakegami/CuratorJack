using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 追加ウィンドウとして起動されたとき、番号に応じてウィンドウを重ならない位置へ並べる（Windows ビルドのみ）。
/// </summary>
public static class LocalTestWindowPlacement
{
    const int Columns = 2;
    const int OffsetX = 40;
    const int OffsetY = 40;
    const int GapPixels = 8;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    const uint NoSize = 0x0001;
    const uint NoZOrder = 0x0004;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Apply()
    {
        if (!LaunchArguments.TryGetValue(LaunchArguments.WindowIndexKey, out string value) || !int.TryParse(value, out int index))
            return;

        int x = OffsetX + (index % Columns) * (Screen.width + GapPixels);
        int y = OffsetY + (index / Columns) * (Screen.height + GapPixels + 32);
        SetWindowPos(GetActiveWindow(), IntPtr.Zero, x, y, 0, 0, NoSize | NoZOrder);
    }
#endif
}
