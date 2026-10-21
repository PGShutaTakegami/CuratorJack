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

    /// <summary>生き残りが 2 人の最後の 1v1 か</summary>
    public bool IsFinalDuel => AlivePlayers.Count() == 2;

    public event Action<PlayerState, HandCard> HandCardAdded;

    public event Action<PlayerState, HandCard> HandCardRemoved;

    /// <summary>コイン・見送り権・落札した商品・かかっているバフデバフのどれかが変わった</summary>
    public event Action<PlayerState> StatusChanged;

    public event Action<PlayerState> PlayerDied;

    /// <summary>見送り権を使った（プレイヤーと使った枚数。足りずに死亡したときは PlayerDied だけを出す）</summary>
    public event Action<PlayerState, int> PassRightConsumed;

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

    /// <summary>見送り権を count 枚消費する。count 枚に足りないプレイヤーは死亡する。</summary>
    public void ConsumePassRight(int playerNumber, int count = 1)
    {
        if (count <= 0 || !players.TryGetValue(playerNumber, out PlayerState player) || !player.IsAlive)
            return;

        bool died = player.ConsumePassRights(count);
        StatusChanged?.Invoke(player);
        if (died)
        {
            Debug.Log($"[Game] Player{playerNumber} は見送り権 {count} 枚を使えず死亡しました");
            PlayerDied?.Invoke(player);
        }
        else
        {
            PassRightConsumed?.Invoke(player, count);
        }
    }

    /// <summary>リタイアしたプレイヤーを死亡させる（生きていなければ何もしない）</summary>
    public void Retire(int playerNumber)
    {
        if (!players.TryGetValue(playerNumber, out PlayerState player) || !player.IsAlive)
            return;

        player.Retire();
        StatusChanged?.Invoke(player);
        Debug.Log($"[Game] Player{playerNumber} がリタイアしました");
        PlayerDied?.Invoke(player);
    }

    /// <summary>
    /// 出品者が売れたときの代金（落札代金・値踏みの追加払い）を受け取れるか。
    /// 生き残りが 2 人（最後の 1v1）のときは受け取れず、購入者が払った分は消える。
    /// </summary>
    public bool SellerEarnsProceeds => AlivePlayers.Count() > 2;

    /// <summary>購入者から代金を払い、商品を購入者へ渡す。代金は SellerEarnsProceeds のときだけ出品者に入る。</summary>
    public void SettlePurchase(int buyerNumber, int sellerNumber, HandCard goods, int price)
    {
        if (!players.TryGetValue(buyerNumber, out PlayerState buyer) || !players.TryGetValue(sellerNumber, out PlayerState seller))
            return;

        bool sellerEarns = SellerEarnsProceeds;
        buyer.AddCoins(-price);
        if (sellerEarns)
            seller.AddCoins(price);
        buyer.AddToCollection(goods);
        StatusChanged?.Invoke(buyer);
        StatusChanged?.Invoke(seller);
        string proceeds = sellerEarns ? string.Empty : "。最後の 1v1 のため代金は出品者に入りません";
        Debug.Log($"[Game] Player{buyerNumber} が {goods.DisplayName} を {price} コインで購入（出品者 Player{sellerNumber}）{proceeds}");
    }

    /// <summary>購入者から出品者へ追加の代金を払う（持っている分まで）。出品者に入るのは SellerEarnsProceeds のときだけ。払った額を返す。</summary>
    public int PayProceeds(int buyerNumber, int sellerNumber, int amount)
    {
        if (SellerEarnsProceeds)
            return TransferCoins(buyerNumber, sellerNumber, amount);

        if (!players.TryGetValue(buyerNumber, out PlayerState buyer))
            return 0;

        int paid = Mathf.Clamp(amount, 0, Mathf.Max(0, buyer.Coins));
        if (paid == 0)
            return 0;

        buyer.AddCoins(-paid);
        StatusChanged?.Invoke(buyer);
        return paid;
    }

    public void AddCoins(int playerNumber, int amount)
    {
        if (amount == 0 || !players.TryGetValue(playerNumber, out PlayerState player))
            return;

        player.AddCoins(amount);
        StatusChanged?.Invoke(player);
    }

    /// <summary>from から to へコインを払う（持っている分まで）。払った額を返す。</summary>
    int TransferCoins(int fromNumber, int toNumber, int amount)
    {
        if (!players.TryGetValue(fromNumber, out PlayerState from) || !players.TryGetValue(toNumber, out PlayerState to))
            return 0;

        int paid = Mathf.Clamp(amount, 0, Mathf.Max(0, from.Coins));
        if (paid == 0)
            return 0;

        from.AddCoins(-paid);
        to.AddCoins(paid);
        StatusChanged?.Invoke(from);
        StatusChanged?.Invoke(to);
        return paid;
    }

    public void AddPassRights(int playerNumber, int amount)
    {
        if (amount == 0 || !players.TryGetValue(playerNumber, out PlayerState player))
            return;

        player.AddPassRights(amount);
        StatusChanged?.Invoke(player);
    }

    public void AddStatus(int playerNumber, PlayerStatus status, int amount)
    {
        if (amount <= 0 || !players.TryGetValue(playerNumber, out PlayerState player))
            return;

        player.AddStatus(status, amount);
        StatusChanged?.Invoke(player);
    }

    /// <summary>持続効果を 1 回使う。残っていなければ false を返す。</summary>
    public bool TryUseStatus(int playerNumber, PlayerStatus status) => UseStatus(playerNumber, status, 1) > 0;

    /// <summary>持続効果を残りすべて使い、使った量を返す（投げ売りの値引き額など）。</summary>
    public int UseAllStatus(int playerNumber, PlayerStatus status) => UseStatus(playerNumber, status, int.MaxValue);

    int UseStatus(int playerNumber, PlayerStatus status, int amount)
    {
        if (!players.TryGetValue(playerNumber, out PlayerState player))
            return 0;

        int used = player.UseStatus(status, amount);
        if (used > 0)
            StatusChanged?.Invoke(player);
        return used;
    }

    /// <summary>受けたバフデバフカードを記録する（効果の中身は CardEffectResolver が処理する）。</summary>
    public void ApplyBuffs(int playerNumber, IReadOnlyList<HandCard> buffs)
    {
        if (buffs.Count == 0 || !players.TryGetValue(playerNumber, out PlayerState player))
            return;

        player.AddBuffs(buffs);
        StatusChanged?.Invoke(player);
        Debug.Log($"[Game] Player{playerNumber} にバフデバフ {buffs.Count} 枚がかかりました");
    }
}
