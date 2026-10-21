using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// SinglePlay 画面。人数（1v1 / 1v2 / 1v3）を選んで選択状態にし、開始ボタンで GameScene へ移動する。
/// </summary>
public class SinglePlayLobbyController : MonoBehaviour
{
    [Serializable]
    struct Option
    {
        [Tooltip("自分を含めた合計人数")]
        public int totalPlayers;
        public Button button;
        public ButtonManager view;
    }

    [SerializeField] private Option[] options;

    [SerializeField] private Button startButton;

    [SerializeField] private string gameSceneName = "GameScene";

    UnityAction[] listeners;
    int selectedTotalPlayers;
    bool isStarting;

    void OnEnable()
    {
        listeners = new UnityAction[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            int total = options[i].totalPlayers;
            listeners[i] = () => Select(total);
            options[i].button.onClick.AddListener(listeners[i]);
        }

        startButton.onClick.AddListener(StartGame);
    }

    void OnDisable()
    {
        for (int i = 0; i < options.Length; i++)
            options[i].button.onClick.RemoveListener(listeners[i]);

        startButton.onClick.RemoveListener(StartGame);
    }

    void Select(int totalPlayers)
    {
        selectedTotalPlayers = totalPlayers;
        foreach (Option option in options)
        {
            if (option.view != null)
                option.view.SetPinned(option.totalPlayers == totalPlayers);
        }
    }

    void StartGame()
    {
        if (isStarting)
            return;

        if (selectedTotalPlayers <= 0)
        {
            Debug.LogWarning("[SinglePlay] 先に人数を選択してください");
            return;
        }

        isStarting = true;
        SinglePlayContext.Set(selectedTotalPlayers);
        Debug.Log($"[SinglePlay] ゲームを開始します（合計 {selectedTotalPlayers} 人）");
        SceneFader.LoadScene(gameSceneName);
    }
}
