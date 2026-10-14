using System;
using UnityEngine;

/// <summary>
/// ゲームが開始されたら出品者を決めて通知し、ドローが終わったら出品者のターンを始める。
/// 出品者を先に決めておくのは、最初のドローの内容が「自分が最初の出品者か」で変わるため。
/// 決める処理は、決める権限を持つ画面（SinglePlay は自分、ネットワークはホスト）だけが登録する。
/// </summary>
public class SellerSelector : MonoBehaviour
{
    [SerializeField] private GameStartGate startGate;

    [SerializeField] private DrawPhaseController drawPhase;

    Action selectHandler;

    public int CurrentSeller { get; private set; } = PlayerRegistry.NoPlayer;

    /// <summary>出品者が決まった（全員の画面で通知される）。出品者のプレイヤー番号を渡す</summary>
    public event Action<int> SellerDecided;

    /// <summary>ドローが終わり、出品者のターンが始まった。出品者のプレイヤー番号を渡す</summary>
    public event Action<int> SellerTurnStarted;

    void Awake()
    {
        startGate.Started += RequestSelect;
        drawPhase.DrawFinished += StartSellerTurn;
    }

    void OnDestroy()
    {
        startGate.Started -= RequestSelect;
        drawPhase.DrawFinished -= StartSellerTurn;
    }

    /// <summary>この画面に出品者を決める権限を与える。決めるときの処理を渡す</summary>
    public void AllowLocalSelect(Action onSelectRequested)
    {
        selectHandler = onSelectRequested;
    }

    void RequestSelect()
    {
        selectHandler?.Invoke();
    }

    public void NotifySellerDecided(int playerNumber)
    {
        if (playerNumber == PlayerRegistry.NoPlayer)
            return;

        CurrentSeller = playerNumber;
        SellerDecided?.Invoke(playerNumber);
    }

    void StartSellerTurn()
    {
        if (CurrentSeller != PlayerRegistry.NoPlayer)
            SellerTurnStarted?.Invoke(CurrentSeller);
    }
}
