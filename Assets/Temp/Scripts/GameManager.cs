using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ゲーム中の全プレイヤーの状態（コイン・見送り権・生死・手札など）を持ち、書き換えと変更の通知を受け持つ。
/// ゲームが開始されたら、PlayerRegistry に登録されているプレイヤーの状態を作る。
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField] private GameStartGate startGate;

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("最初に持っているコイン")]
    [SerializeField] private int initialCoins = 100;

    [Tooltip("最初に持っている見送り権")]
    [SerializeField] private int initialPassRights = 3;

    readonly Dictionary<int, PlayerState> players = new Dictionary<int, PlayerState>();

    public IEnumerable<PlayerState> Players => players.Values.OrderBy(player => player.PlayerNumber);

    public IEnumerable<PlayerState> AlivePlayers => Players.Where(player => player.IsAlive);

    public event Action<PlayerState, HandCard> HandCardAdded;

    public event Action<PlayerState, HandCard> HandCardRemoved;

    /// <summary>コイン・見送り権・落札した商品・かかっているバフデバフのどれかが変わった</summary>
    public event Action<PlayerState> StatusChanged;

    public event Action<PlayerState> PlayerDied;

    void Awake()
    {
        startGate.Started += CreatePlayers;
    }

    void OnDestroy()
    {
        startGate.Started -= CreatePlayers;
    }

    void CreatePlayers()
    {
        foreach (int playerNumber in registry.PlayerNumbers)
        {
            if (!players.ContainsKey(playerNumber))
                players[playerNumber] = new PlayerState(playerNumber, initialCoins, initialPassRights);
        }
    }

    public bool TryGet(int playerNumber, out PlayerState player)
    {
        return players.TryGetValue(playerNumber, out player);
    }

    public bool IsAlive(int playerNumber)
    {
        return players.TryGetValue(playerNumber, out PlayerState player) && player.IsAlive;
    }

    public void AddToHand(int playerNumber, HandCard card)
    {
        if (!players.TryGetValue(playerNumber, out PlayerState player))
            return;

        player.AddToHand(card);
        HandCardAdded?.Invoke(player, card);
    }

    /// <summary>手札からカードを消費する。手札になければ false を返す。</summary>
    public bool RemoveFromHand(int playerNumber, HandCard card)
    {
        if (!players.TryGetValue(playerNumber, out PlayerState player) || !player.RemoveFromHand(card))
            return false;

        HandCardRemoved?.Invoke(player, card);
        return true;
    }

    /// <summary>見送り権を消費する。見送り権が 0 枚で消費しようとしたプレイヤーは死亡する。</summary>
    public void ConsumePassRight(int playerNumber)
    {
        if (!players.TryGetValue(playerNumber, out PlayerState player) || !player.IsAlive)
            return;

        bool died = player.ConsumePassRight();
        StatusChanged?.Invoke(player);
        if (died)
        {
            Debug.Log($"[Game] Player{playerNumber} は見送り権を使えず死亡しました");
            PlayerDied?.Invoke(player);
        }
    }

    /// <summary>購入者から出品者へ代金を払い、商品を購入者へ渡す。</summary>
    public void SettlePurchase(int buyerNumber, int sellerNumber, HandCard goods, int price)
    {
        if (!players.TryGetValue(buyerNumber, out PlayerState buyer) || !players.TryGetValue(sellerNumber, out PlayerState seller))
            return;

        buyer.AddCoins(-price);
        seller.AddCoins(price);
        buyer.AddToCollection(goods);
        StatusChanged?.Invoke(buyer);
        StatusChanged?.Invoke(seller);
        Debug.Log($"[Game] Player{buyerNumber} が {goods.DisplayName} を {price} コインで購入（出品者 Player{sellerNumber}）");
    }

    /// <summary>バフデバフカードの効果をプレイヤーにかける（効果の中身はまだないため、かかっていることだけを記録する）。</summary>
    public void ApplyBuffs(int playerNumber, IReadOnlyList<HandCard> buffs)
    {
        if (buffs.Count == 0 || !players.TryGetValue(playerNumber, out PlayerState player))
            return;

        player.AddBuffs(buffs);
        StatusChanged?.Invoke(player);
        Debug.Log($"[Game] Player{playerNumber} にバフデバフ {buffs.Count} 枚がかかりました");
    }
}
