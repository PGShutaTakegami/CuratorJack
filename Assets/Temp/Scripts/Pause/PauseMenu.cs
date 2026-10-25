using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ポーズ画面。Esc か画面のポーズボタンで開閉する。
/// ボタンは上から再開・リタイア・設定・ルーム設定・Quit。リタイアはゲーム中（開始後、決着まで）だけ押せる。
/// Quit はゲーム開始前（準備中）と、自分が脱落して抜けてよいとき（MatchLeaveRule）に押せる。押せないときは灰色にする。
/// ルーム設定は部屋主（SinglePlay は自分、マルチプレイは今のホスト）にだけ表示する。開いている間はボタン群を隠し、Esc でボタン群へ戻る。
/// 設定は設定画面（OptionWindow）をボタン群の代わりに開き、戻るボタンか Esc でボタン群へ戻る（一覧やキーの待ち受けを開いていれば、Esc はそれを先に閉じる）。
/// リタイアは押すとボタン群の代わりに確認（はい / いいえ）を出し、はいでリタイアを知らせる。いいえ・Esc・決着でボタン群へ戻る。
/// 開閉とボタンを押したことを知らせるだけで、時間を止める・リタイアする・シーンを移るのは受け取った側が行う。
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Tooltip("ポーズ中だけ表示するもの（暗幕と背景）")]
    [SerializeField] private GameObject[] views;

    [Tooltip("画面に置いたポーズボタン")]
    [SerializeField] private Button openButton;

    [SerializeField] private Button resumeButton;

    [SerializeField] private MenuButton retireButton;

    [SerializeField] private MenuButton quitButton;

    [SerializeField] private Button settingsButton;

    [Tooltip("設定のボタンで、ポーズのボタン群の代わりに開く設定画面")]
    [SerializeField] private OptionWindow settings;

    [Tooltip("部屋主にだけ表示するルーム設定のボタン")]
    [SerializeField] private Button roomSettingsButton;

    [Tooltip("ルーム設定のボタンで、ポーズのボタン群の代わりに開く画面")]
    [SerializeField] private RoomSettingsPanel roomSettings;

    [Tooltip("リタイアのボタンで、ポーズのボタン群の代わりに開く確認")]
    [SerializeField] private RetireConfirmMenu retireConfirm;

    [Tooltip("設定・ルーム設定・リタイアの確認を開いている間だけ隠す、ポーズのボタン群の背景")]
    [SerializeField] private GameObject menuPanel;

    [SerializeField] private LocalRoomOwner roomOwner;

    [SerializeField] private GameStartGate startGate;

    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private MatchLeaveRule leaveRule;

    bool isFinished;
    bool isQuitAvailable;

    public bool IsOpen { get; private set; }

    public event Action Opened;

    public event Action Closed;

    public event Action RetireClicked;

    public event Action QuitClicked;

    void Awake()
    {
        openButton.onClick.AddListener(Toggle);
        resumeButton.onClick.AddListener(Close);
        retireButton.Button.onClick.AddListener(OpenRetireConfirm);
        quitButton.Button.onClick.AddListener(() => QuitClicked?.Invoke());
        settingsButton.onClick.AddListener(OpenSettings);
        settings.BackRequested += CloseSettings;
        settings.gameObject.SetActive(false);
        roomSettingsButton.onClick.AddListener(OpenRoomSettings);
        roomSettings.Closed += HandleSubPanelClosed;
        retireConfirm.Confirmed += HandleRetireConfirmed;
        retireConfirm.Closed += HandleSubPanelClosed;
        turnFlow.GameFinished += HandleGameFinished;
        SetViewsVisible(false);
    }

    void OnDestroy()
    {
        settings.BackRequested -= CloseSettings;
        roomSettings.Closed -= HandleSubPanelClosed;
        retireConfirm.Confirmed -= HandleRetireConfirmed;
        retireConfirm.Closed -= HandleSubPanelClosed;
        turnFlow.GameFinished -= HandleGameFinished;
    }

    void HandleGameFinished()
    {
        isFinished = true;
        retireButton.SetAvailable(false);
        retireConfirm.Close();
    }

    void HandleRetireConfirmed() => RetireClicked?.Invoke();

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !SceneFader.IsFading)
        {
            if (IsSettingsOpen)
            {
                if (!settings.HandleBack())
                    CloseSettings();
            }
            else if (roomSettings.IsOpen)
                roomSettings.Close();
            else if (retireConfirm.IsOpen)
                retireConfirm.Close();
            else
                Toggle();
        }

        if (IsOpen)
        {
            RefreshRoomSettings();
            RefreshQuit();
        }
    }

    /// <summary>開いている間に脱落しても押せるよう、変わったときだけ合わせる</summary>
    void RefreshQuit()
    {
        bool available = leaveRule.CanQuit;
        if (available == isQuitAvailable)
            return;

        isQuitAvailable = available;
        quitButton.SetAvailable(available);
    }

    /// <summary>開いている間に部屋主が移っても、今の部屋主にだけ表示されるよう毎フレーム合わせる</summary>
    void RefreshRoomSettings()
    {
        bool visible = roomOwner.IsOwner;
        GameObject button = roomSettingsButton.gameObject;
        if (button.activeSelf != visible)
            button.SetActive(visible);
        if (!visible && roomSettings.IsOpen)
            roomSettings.Close();
    }

    bool IsSettingsOpen => settings.gameObject.activeSelf;

    void OpenSettings()
    {
        menuPanel.SetActive(false);
        settings.gameObject.SetActive(true);
    }

    void CloseSettings()
    {
        if (!IsSettingsOpen)
            return;

        settings.gameObject.SetActive(false);
        HandleSubPanelClosed();
    }

    void OpenRoomSettings()
    {
        menuPanel.SetActive(false);
        roomSettings.Open();
    }

    void OpenRetireConfirm()
    {
        menuPanel.SetActive(false);
        retireConfirm.Open();
    }

    void HandleSubPanelClosed()
    {
        if (IsOpen)
            menuPanel.SetActive(true);
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        if (IsOpen)
            return;

        IsOpen = true;
        retireButton.SetAvailable(startGate.HasStarted && !isFinished);
        isQuitAvailable = leaveRule.CanQuit;
        quitButton.SetAvailable(isQuitAvailable);
        RefreshRoomSettings();
        SetViewsVisible(true);
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        CloseSettings();
        roomSettings.Close();
        retireConfirm.Close();
        SetViewsVisible(false);
        Closed?.Invoke();
    }

    void SetViewsVisible(bool visible)
    {
        foreach (GameObject view in views)
            view.SetActive(visible);
    }
}
