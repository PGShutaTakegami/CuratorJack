using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ポーズ画面。Esc か画面のポーズボタンで開閉する。
/// ボタンは上から再開・リタイア・設定・ルーム設定・Quit。リタイアはゲーム中（開始後、決着まで）だけ、Quit はゲーム開始前（準備中）だけ押せ、押せないときは灰色にする。
/// ルーム設定は部屋主（SinglePlay は自分、マルチプレイは今のホスト）にだけ表示する。開いている間はボタン群を隠し、Esc でボタン群へ戻る。
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

    [Tooltip("部屋主にだけ表示するルーム設定のボタン")]
    [SerializeField] private Button roomSettingsButton;

    [Tooltip("ルーム設定のボタンで、ポーズのボタン群の代わりに開く画面")]
    [SerializeField] private RoomSettingsPanel roomSettings;

    [Tooltip("ルーム設定を開いている間だけ隠す、ポーズのボタン群の背景")]
    [SerializeField] private GameObject menuPanel;

    [SerializeField] private LocalRoomOwner roomOwner;

    [SerializeField] private GameStartGate startGate;

    [SerializeField] private TurnFlowController turnFlow;

    bool isFinished;

    public bool IsOpen { get; private set; }

    public event Action Opened;

    public event Action Closed;

    public event Action RetireClicked;

    public event Action QuitClicked;

    void Awake()
    {
        openButton.onClick.AddListener(Toggle);
        resumeButton.onClick.AddListener(Close);
        retireButton.Button.onClick.AddListener(() => RetireClicked?.Invoke());
        quitButton.Button.onClick.AddListener(() => QuitClicked?.Invoke());
        roomSettingsButton.onClick.AddListener(OpenRoomSettings);
        roomSettings.Closed += HandleRoomSettingsClosed;
        turnFlow.GameFinished += HandleGameFinished;
        SetViewsVisible(false);
    }

    void OnDestroy()
    {
        roomSettings.Closed -= HandleRoomSettingsClosed;
        turnFlow.GameFinished -= HandleGameFinished;
    }

    void HandleGameFinished() => isFinished = true;

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !SceneFader.IsFading)
        {
            if (roomSettings.IsOpen)
                roomSettings.Close();
            else
                Toggle();
        }

        if (IsOpen)
            RefreshRoomSettings();
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

    void OpenRoomSettings()
    {
        menuPanel.SetActive(false);
        roomSettings.Open();
    }

    void HandleRoomSettingsClosed()
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
        quitButton.SetAvailable(!startGate.HasStarted);
        RefreshRoomSettings();
        SetViewsVisible(true);
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        roomSettings.Close();
        SetViewsVisible(false);
        Closed?.Invoke();
    }

    void SetViewsVisible(bool visible)
    {
        foreach (GameObject view in views)
            view.SetActive(visible);
    }
}
