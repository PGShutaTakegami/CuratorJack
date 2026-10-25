using System;
using UnityEngine;

public enum ScreenResolutionOption
{
    FullHD = 0,
    HD = 1,
    QuarterHD = 2
}

public enum GraphicsQuality
{
    High = 0,
    Medium = 1,
    Low = 2
}

public enum WindowMode
{
    FullScreen = 0,
    Windowed = 1
}

/// <summary>VC で自分の声を送るタイミング</summary>
public enum VoiceTalkMode
{
    /// <summary>キーを押している間だけ送る</summary>
    PushToTalk = 0,
    /// <summary>常に送る</summary>
    Open = 1,
    /// <summary>送らない（聞くだけ）</summary>
    Off = 2
}

/// <summary>
/// オプション画面で変えられる設定の値。保存は GameSettingsStore、ゲームへの反映は GameSettingsApplier が行う。
/// 音量は 0〜1。
/// </summary>
[Serializable]
public class GameSettings
{
    [SerializeField] private float masterVolume = 1f;
    [SerializeField] private float voiceChatVolume = 1f;
    [SerializeField] private float seVolume = 1f;
    [SerializeField] private float bgmVolume = 1f;
    [SerializeField] private ScreenResolutionOption resolution = ScreenResolutionOption.FullHD;
    [SerializeField] private GraphicsQuality graphicsQuality = GraphicsQuality.High;
    [SerializeField] private WindowMode windowMode = WindowMode.FullScreen;
    [SerializeField] private bool vibration = true;
    [SerializeField] private string outputDevice = string.Empty;
    [SerializeField] private string microphoneDevice = string.Empty;
    [SerializeField] private bool noiseSuppression = true;
    [SerializeField] private VoiceTalkMode talkMode = VoiceTalkMode.PushToTalk;
    [SerializeField] private string pushToTalkKey = string.Empty;
    [SerializeField] private LanguageCode language = LanguageCode.Japanese;

    public float MasterVolume { get => masterVolume; set => masterVolume = Mathf.Clamp01(value); }

    public float VoiceChatVolume { get => voiceChatVolume; set => voiceChatVolume = Mathf.Clamp01(value); }

    public float SeVolume { get => seVolume; set => seVolume = Mathf.Clamp01(value); }

    public float BgmVolume { get => bgmVolume; set => bgmVolume = Mathf.Clamp01(value); }

    public ScreenResolutionOption Resolution { get => resolution; set => resolution = value; }

    public GraphicsQuality GraphicsQuality { get => graphicsQuality; set => graphicsQuality = value; }

    public WindowMode WindowMode { get => windowMode; set => windowMode = value; }

    public bool Vibration { get => vibration; set => vibration = value; }

    /// <summary>オーディオ出力デバイスの名前。空なら OS の既定のデバイス</summary>
    public string OutputDevice { get => outputDevice ?? string.Empty; set => outputDevice = value ?? string.Empty; }

    /// <summary>マイクの名前（Microphone.devices の名前）。空なら OS の既定のデバイス</summary>
    public string MicrophoneDevice { get => microphoneDevice ?? string.Empty; set => microphoneDevice = value ?? string.Empty; }

    /// <summary>VC で送る声のノイズを抑えるか</summary>
    public bool NoiseSuppression { get => noiseSuppression; set => noiseSuppression = value; }

    public VoiceTalkMode TalkMode { get => talkMode; set => talkMode = value; }

    /// <summary>プレイヤーが変えたプッシュトゥトークのキーボードのキー（UnityEngine.InputSystem.Key の名前）。空なら操作の割り当てアセットの初期値を使う</summary>
    public string PushToTalkKey { get => pushToTalkKey ?? string.Empty; set => pushToTalkKey = value ?? string.Empty; }

    public LanguageCode Language { get => language; set => language = value; }

    public GameSettings Clone() => (GameSettings)MemberwiseClone();
}

public static class ScreenResolutionOptionExtensions
{
    public static Vector2Int Size(this ScreenResolutionOption option)
    {
        switch (option)
        {
            case ScreenResolutionOption.FullHD:
                return new Vector2Int(1920, 1080);
            case ScreenResolutionOption.HD:
                return new Vector2Int(1280, 720);
            default:
                return new Vector2Int(960, 540);
        }
    }
}
