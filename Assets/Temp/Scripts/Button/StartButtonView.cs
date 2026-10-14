using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// スタートボタン。スタートできるプレイヤーにだけ表示し、全員が準備完了するまでは灰色で押せない。
/// ゲームが開始されたらボタン UI をまとめて消す。
/// </summary>
public class StartButtonView : MonoBehaviour
{
    [SerializeField] private Button button;

    [SerializeField] private Image image;

    [SerializeField] private Sprite waitingSprite;

    [SerializeField] private Sprite allReadySprite;

    [Tooltip("ゲーム開始時に消すボタン UI")]
    [SerializeField] private GameObject buttonRoot;

    [SerializeField] private GameStartGate startGate;

    [SerializeField] private ReadyRoster roster;

    void OnEnable()
    {
        button.onClick.AddListener(startGate.RequestStart);
        startGate.Changed += Refresh;
        startGate.Started += HideButtons;
        roster.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        button.onClick.RemoveListener(startGate.RequestStart);
        startGate.Changed -= Refresh;
        startGate.Started -= HideButtons;
        roster.Changed -= Refresh;
    }

    void Refresh()
    {
        bool visible = startGate.CanLocalStart && !startGate.HasStarted;
        button.gameObject.SetActive(visible);
        if (!visible)
            return;

        bool allReady = roster.IsAllReady;
        button.interactable = allReady;
        image.sprite = allReady ? allReadySprite : waitingSprite;
    }

    void HideButtons()
    {
        buttonRoot.SetActive(false);
    }
}
