using UnityEngine;

/// <summary>
/// 画面の表示方法（フルスクリーン／ウィンドウ）と大きさを決めて反映する。Alt+Enter とオプションの両方がここを通す。
/// フルスクリーンは常にモニターの解像度にする（選んだ解像度で描いて引き伸ばすと、切り替えたあと元の大きさに戻らないため）。
/// ウィンドウは選んだ解像度を使い、モニターに収まらない大きさは 16:9 のまま縮める（タイトルバーやタスクバーで画面の端が切れないように）。
/// </summary>
public static class ScreenModeApplier
{
    /// <summary>ウィンドウの縦横を、モニターのこの割合までに収める</summary>
    const float MaxWindowRatio = 0.9f;

    public static void Apply(GameSettings settings)
    {
        Vector2Int display = DisplaySize();
        if (settings.WindowMode == WindowMode.FullScreen)
        {
            Screen.SetResolution(display.x, display.y, FullScreenMode.FullScreenWindow);
            Debug.Log($"[Display] フルスクリーンにしました（{display.x}x{display.y}）");
            return;
        }

        Vector2Int size = FitWindow(settings.Resolution.Size(), display);
        Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
        Debug.Log($"[Display] ウィンドウにしました（{size.x}x{size.y}）");
    }

    /// <summary>前回のウィンドウがモニターに収まらない大きさのまま覚えられていたら、収まる大きさへ縮める</summary>
    public static void FitCurrentWindow()
    {
        if (Screen.fullScreenMode != FullScreenMode.Windowed)
            return;

        var current = new Vector2Int(Screen.width, Screen.height);
        Vector2Int fitted = FitWindow(current, DisplaySize());
        if (fitted == current)
            return;

        Screen.SetResolution(fitted.x, fitted.y, FullScreenMode.Windowed);
        Debug.Log($"[Display] ウィンドウが画面に収まらないため {fitted.x}x{fitted.y} に縮めました（前回 {current.x}x{current.y}）");
    }

    public static Vector2Int FitWindow(Vector2Int requested, Vector2Int display)
    {
        if (requested.x <= 0 || requested.y <= 0 || display.x <= 0 || display.y <= 0)
            return requested;

        float scale = Mathf.Min(1f, display.x * MaxWindowRatio / requested.x, display.y * MaxWindowRatio / requested.y);
        if (scale >= 1f)
            return requested;
        return new Vector2Int(Mathf.FloorToInt(requested.x * scale), Mathf.FloorToInt(requested.y * scale));
    }

    /// <summary>ゲームのウィンドウがあるモニターの解像度（Screen.currentResolution は、解像度を下げたフルスクリーン中にその解像度を返すことがあるため使わない）</summary>
    static Vector2Int DisplaySize()
    {
        DisplayInfo info = Screen.mainWindowDisplayInfo;
        if (info.width > 0 && info.height > 0)
            return new Vector2Int(info.width, info.height);

        Resolution fallback = Screen.currentResolution;
        return new Vector2Int(fallback.width, fallback.height);
    }
}
