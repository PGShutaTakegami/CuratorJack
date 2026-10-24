using UnityEngine;

/// <summary>
/// 設定のうち、今のゲームに機能があるもの（マスター音量・画質・解像度と表示方法・言語）をゲームに反映する。
/// SE・BGM の音量、コントローラーの振動は、まだ機能がないため値を保存するだけ（使う側が GameSettingsStore から読む）。
/// VC の音量・マイク・出力デバイスなど VC の設定は、VoiceChatSession が VC にだけ反映する（Unity の音声の出力デバイスは切り替えられない）。
/// 起動時は解像度と表示方法以外を反映する。解像度と表示方法は Unity が前回の画面を覚えているため触らない（テスト用の追加ウィンドウの大きさ指定も上書きしない）。
/// </summary>
public static class GameSettingsApplier
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyOnStartup()
    {
        GameSettings settings = GameSettingsStore.Current;
        ApplyAudio(settings);
        ApplyGraphics(settings);
        ApplyLanguage(settings);
    }

    public static void ApplyLanguage(GameSettings settings)
    {
        GameLanguage.Current = settings.Language;
    }

    public static void ApplyAudio(GameSettings settings)
    {
        AudioListener.volume = settings.MasterVolume;
    }

    /// <summary>テクスチャの解像度（ミップマップの段）と異方性フィルタリングを切り替える。ミップマップのない UI の画像は変わらない</summary>
    public static void ApplyGraphics(GameSettings settings)
    {
        switch (settings.GraphicsQuality)
        {
            case GraphicsQuality.High:
                QualitySettings.globalTextureMipmapLimit = 0;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                break;
            case GraphicsQuality.Medium:
                QualitySettings.globalTextureMipmapLimit = 1;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                break;
            default:
                QualitySettings.globalTextureMipmapLimit = 2;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                break;
        }
    }

    /// <summary>エディターの Game ビューは大きさを変えられないため、ビルドでだけ反映する</summary>
    public static void ApplyDisplay(GameSettings settings)
    {
        if (Application.isEditor)
            return;

        Vector2Int size = settings.Resolution.Size();
        FullScreenMode mode = settings.WindowMode == WindowMode.Windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        Screen.SetResolution(size.x, size.y, mode);
        Debug.Log($"[Option] 画面を {size.x}x{size.y}（{mode}）にしました");
    }
}
