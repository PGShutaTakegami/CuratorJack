using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// オプション画面。音量（マスター・VC・SE・BGM）、解像度、画質、表示方法、コントローラーの振動、オーディオ出力デバイス、マイク、
/// VC のノイズキャンセリング・話し方・プッシュトゥトークのキー（キーボードだけ変えられ、コントローラーは割り当てアセットの表示だけ）、言語を変える。
/// デバイスの一覧は画面を開くたびに取り直し、先頭に「既定のデバイス」（保存値は空文字）を置く。
/// 変えた値はすぐに保存し、機能があるもの（マスター音量・画質・解像度と表示方法・言語）はすぐに反映する。VC の設定は VoiceChatSession が保存のたびに読んで反映する。
/// ↑↓（W/S・十字キー・左スティック）で行を選び、←→（A/D）で値を変え、決定（Enter/Space/A）で一覧を開く。マウスを乗せた行も選ぶ。
/// 一覧を開いている間は、上下と決定の入力をその行へ渡す。行が入りきらない分はスクロールし、選んだ行は見える位置まで送る。
/// 戻るボタンで BackRequested を知らせ、タイトルの画面（monitorManager を設定したとき）ではタイトルへ戻る。
/// 選択肢などの文言は言語テーブルから表示言語に合わせて作り、言語が変わったら作り直す。
/// 解像度と言語の選択肢は、どの言語でも同じ表記にする（言語は自分の言語を見つけられるよう、それぞれの言語の名前のまま）。
/// </summary>
public class OptionWindow : MonoBehaviour
{
    static readonly string[] ResolutionLabels = { "1920×1080 (FHD)", "1280×720 (HD)", "960×540" };
    static readonly string[] LanguageLabels = { "日本語", "English" };
    static readonly string[] QualityKeys = { "option.quality.high", "option.quality.medium", "option.quality.low" };
    static readonly string[] WindowModeKeys = { "option.window_mode.fullscreen", "option.window_mode.windowed" };
    static readonly string[] OnOffKeys = { "option.on", "option.off" };
    static readonly string[] TalkModeKeys = { "option.talk_mode.push_to_talk", "option.talk_mode.open", "option.talk_mode.off" };
    const string DefaultDeviceKey = "option.default_device";
    const string WaitingKey = "option.waiting";

    const float StickThreshold = 0.5f;

    [Tooltip("オプション画面の文言の言語テーブル")]
    [SerializeField] private LanguageTable table;

    [SerializeField] private OptionVolumeBar masterVolume;

    [SerializeField] private OptionVolumeBar voiceChatVolume;

    [SerializeField] private OptionVolumeBar seVolume;

    [SerializeField] private OptionVolumeBar bgmVolume;

    [SerializeField] private OptionChoiceRow resolution;

    [SerializeField] private OptionChoiceRow graphicsQuality;

    [SerializeField] private OptionChoiceRow windowMode;

    [SerializeField] private OptionChoiceRow vibration;

    [SerializeField] private OptionDropdownRow outputDevice;

    [SerializeField] private OptionDropdownRow microphoneDevice;

    [SerializeField] private OptionChoiceRow noiseSuppression;

    [SerializeField] private OptionChoiceRow talkMode;

    [SerializeField] private OptionKeyBindRow pushToTalkKey;

    [Tooltip("操作の割り当てアセットのプッシュトゥトーク（初期値の表示に使う）")]
    [SerializeField] private InputActionReference pushToTalkAction;

    [SerializeField] private OptionDropdownRow language;

    [Tooltip("行の一覧をスクロールさせる")]
    [SerializeField] private ScrollRect rowsScroll;

    [SerializeField] private Button backButton;

    [Tooltip("タイトルの画面で使うときだけ設定する（戻るボタンでタイトルへ戻る）")]
    [SerializeField] private MonitorManager monitorManager;

    /// <summary>戻るボタンが押された</summary>
    public event System.Action BackRequested;

    OptionRow[] rows;
    GameSettings settings;
    IReadOnlyList<string> outputDevices = new string[0];
    IReadOnlyList<string> microphoneDevices = new string[0];
    int selectedRow;
    Vector2 previousStick;
    bool isSaving;

    void Awake()
    {
        rows = new OptionRow[]
        {
            masterVolume, voiceChatVolume, seVolume, bgmVolume, resolution, graphicsQuality, windowMode, vibration,
            outputDevice, microphoneDevice, noiseSuppression, talkMode, pushToTalkKey, language
        };
        foreach (OptionRow row in rows)
            row.FocusRequested += Select;

        resolution.Setup(ResolutionLabels);
        language.Setup(LanguageLabels);

        masterVolume.Changed += value => Change(s => s.MasterVolume = value, GameSettingsApplier.ApplyAudio);
        voiceChatVolume.Changed += value => Change(s => s.VoiceChatVolume = value);
        seVolume.Changed += value => Change(s => s.SeVolume = value);
        bgmVolume.Changed += value => Change(s => s.BgmVolume = value);
        resolution.Changed += index => Change(s => s.Resolution = (ScreenResolutionOption)index, GameSettingsApplier.ApplyDisplay);
        graphicsQuality.Changed += index => Change(s => s.GraphicsQuality = (GraphicsQuality)index, GameSettingsApplier.ApplyGraphics);
        windowMode.Changed += index => Change(s => s.WindowMode = (WindowMode)index, GameSettingsApplier.ApplyDisplay);
        vibration.Changed += index => Change(s => s.Vibration = index == 0);
        outputDevice.Changed += index => Change(s => s.OutputDevice = DeviceAt(outputDevices, index));
        microphoneDevice.Changed += index => Change(s => s.MicrophoneDevice = DeviceAt(microphoneDevices, index));
        noiseSuppression.Changed += index => Change(s => s.NoiseSuppression = index == 0);
        talkMode.Changed += index => Change(s => s.TalkMode = (VoiceTalkMode)index);
        pushToTalkKey.Changed += key => Change(s => s.PushToTalkKey = key.ToString());
        language.Changed += index => Change(s => s.Language = (LanguageCode)index, GameSettingsApplier.ApplyLanguage);

        backButton.onClick.AddListener(Back);
    }

    void OnEnable()
    {
        settings = GameSettingsStore.Current;
        outputDevices = AudioDeviceCatalog.GetOutputDevices();
        microphoneDevices = AudioDeviceCatalog.GetMicrophoneDevices();
        SetupTexts();
        ShowValues();
        GameLanguage.Changed += HandleLanguageChanged;
        GameSettingsStore.Saved += HandleSettingsSaved;
        rowsScroll.verticalNormalizedPosition = 1f;
        Select(rows[0]);
    }

    void HandleLanguageChanged()
    {
        SetupTexts();
        ShowValues();
    }

    /// <summary>表示言語に合わせて、選択肢と待ち受けの文言を作る</summary>
    void SetupTexts()
    {
        graphicsQuality.Setup(Texts(QualityKeys));
        windowMode.Setup(Texts(WindowModeKeys));
        vibration.Setup(Texts(OnOffKeys));
        noiseSuppression.Setup(Texts(OnOffKeys));
        talkMode.Setup(Texts(TalkModeKeys));
        pushToTalkKey.SetWaitingText(Text(WaitingKey));
    }

    string Text(string key) => table != null ? table.Get(key) : key;

    string[] Texts(string[] keys) => System.Array.ConvertAll(keys, Text);

    void ShowValues()
    {
        masterVolume.Show(settings.MasterVolume);
        voiceChatVolume.Show(settings.VoiceChatVolume);
        seVolume.Show(settings.SeVolume);
        bgmVolume.Show(settings.BgmVolume);
        resolution.Show((int)settings.Resolution);
        graphicsQuality.Show((int)settings.GraphicsQuality);
        windowMode.Show((int)settings.WindowMode);
        vibration.Show(settings.Vibration ? 0 : 1);
        ShowDevices(outputDevice, outputDevices, settings.OutputDevice);
        ShowDevices(microphoneDevice, microphoneDevices, settings.MicrophoneDevice);
        noiseSuppression.Show(settings.NoiseSuppression ? 0 : 1);
        talkMode.Show((int)settings.TalkMode);
        InputAction pushToTalk = pushToTalkAction != null ? pushToTalkAction.action : null;
        pushToTalkKey.ShowKeyboard(PushToTalkKeyParser.Parse(settings.PushToTalkKey), InputBindings.DefaultDisplayName(pushToTalk, InputBindings.KeyboardGroup));
        pushToTalkKey.ShowGamepad(InputBindings.DefaultDisplayName(pushToTalk, InputBindings.GamepadGroup));
        language.Show((int)settings.Language);
    }

    /// <summary>保存したデバイスが今は見つからない場合も、保存値は変えずに「既定のデバイス」と表示する</summary>
    void ShowDevices(OptionDropdownRow row, IReadOnlyList<string> devices, string selected)
    {
        var labels = new List<string>(devices.Count + 1) { Text(DefaultDeviceKey) };
        labels.AddRange(devices);
        row.Setup(labels);

        int index = 0;
        for (int i = 0; i < devices.Count && !string.IsNullOrEmpty(selected); i++)
        {
            if (devices[i] == selected)
            {
                index = i + 1;
                break;
            }
        }
        row.Show(index);
    }

    static string DeviceAt(IReadOnlyList<string> devices, int index) => index <= 0 || index > devices.Count ? string.Empty : devices[index - 1];

    void OnDisable()
    {
        GameLanguage.Changed -= HandleLanguageChanged;
        GameSettingsStore.Saved -= HandleSettingsSaved;
        GameSettingsStore.Flush();
    }

    /// <summary>
    /// 画面の外（Alt+Enter）で設定が変わったら、手元の値を取り直す（古い値のまま保存して元に戻さないように）。
    /// 外から変わるのは表示方法だけのため、表示を直すのもその行だけにする（開いている一覧や待ち受けを閉じないように）。
    /// </summary>
    void HandleSettingsSaved(GameSettings saved)
    {
        if (isSaving)
            return;

        settings = saved;
        windowMode.Show((int)settings.WindowMode);
    }

    void Change(System.Action<GameSettings> edit, System.Action<GameSettings> apply = null)
    {
        edit(settings);
        isSaving = true;
        GameSettingsStore.Save(settings);
        isSaving = false;
        apply?.Invoke(settings);
    }

    void Select(OptionRow row)
    {
        if (rows[selectedRow].IsExpanded)
            return;

        selectedRow = System.Array.IndexOf(rows, row);
        for (int i = 0; i < rows.Length; i++)
            rows[i].SetHighlighted(i == selectedRow);
        Reveal((RectTransform)row.transform);
    }

    /// <summary>行が一覧の見えている範囲からはみ出していたら、見える位置まで送る</summary>
    void Reveal(RectTransform target)
    {
        RectTransform viewport = rowsScroll.viewport;
        RectTransform content = rowsScroll.content;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
        Rect view = viewport.rect;
        float shift = 0f;
        if (bounds.max.y > view.yMax)
            shift = bounds.max.y - view.yMax;
        else if (bounds.min.y < view.yMin)
            shift = bounds.min.y - view.yMin;
        if (shift == 0f)
            return;

        float max = Mathf.Max(0f, content.rect.height - view.height);
        rowsScroll.StopMovement();
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(content.anchoredPosition.y - shift, 0f, max));
    }

    void Back()
    {
        BackRequested?.Invoke();
        if (monitorManager != null)
            monitorManager.ReturnToTitle();
    }

    /// <summary>開いている一覧・キーの待ち受けがあれば閉じて true を返す。なければ false（画面ごと閉じてよい）</summary>
    public bool HandleBack()
    {
        if (rows == null)
            return false;

        foreach (OptionRow row in rows)
        {
            if (row is IBackInputHandler handler && handler.HandleBack())
                return true;
        }

        return false;
    }

    void Update()
    {
        Gamepad gamepad = Gamepad.current;
        Vector2 stick = gamepad != null ? gamepad.leftStick.ReadValue() : Vector2.zero;
        Keyboard keyboard = Keyboard.current;

        int vertical = 0;
        int horizontal = 0;
        bool submit = false;
        if (keyboard != null)
        {
            submit = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) vertical = -1;
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) vertical = 1;
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) horizontal = -1;
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) horizontal = 1;
        }

        if (gamepad != null)
        {
            submit |= gamepad.buttonSouth.wasPressedThisFrame;
            if (gamepad.dpad.up.wasPressedThisFrame || Crossed(previousStick.y, stick.y)) vertical = -1;
            else if (gamepad.dpad.down.wasPressedThisFrame || Crossed(-previousStick.y, -stick.y)) vertical = 1;
            if (gamepad.dpad.left.wasPressedThisFrame || Crossed(-previousStick.x, -stick.x)) horizontal = -1;
            else if (gamepad.dpad.right.wasPressedThisFrame || Crossed(previousStick.x, stick.x)) horizontal = 1;
        }

        previousStick = stick;
        OptionRow current = rows[selectedRow];
        if (current.IsExpanded)
        {
            if (vertical != 0)
                current.Navigate(vertical);
            if (submit)
                current.Submit();
            return;
        }

        if (vertical != 0)
            Select(rows[(selectedRow + vertical + rows.Length) % rows.Length]);
        if (horizontal != 0)
            rows[selectedRow].Step(horizontal);
        if (submit)
            rows[selectedRow].Submit();
    }

    /// <summary>スティックの値が閾値を下から上へまたいだ瞬間（逆向きは符号を反転して渡す）</summary>
    static bool Crossed(float previous, float current) => previous < StickThreshold && current >= StickThreshold;
}
