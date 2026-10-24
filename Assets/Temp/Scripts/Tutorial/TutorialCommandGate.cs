/// <summary>
/// チュートリアルで、案内している操作だけを進行へ送る窓口。それ以外の操作は捨てる。
/// リタイアはポーズ画面から抜けられるよう、いつでも送る。
/// </summary>
public sealed class TutorialCommandGate : IPlayerCommands
{
    enum Allowed
    {
        None,
        SelectGoods,
        AttachBuff,
        Raise,
        Confirm,
        Bid
    }

    readonly IPlayerCommands inner;
    Allowed allowed;
    HandCard allowedCard;

    public TutorialCommandGate(IPlayerCommands inner)
    {
        this.inner = inner;
    }

    public void Lock() => Allow(Allowed.None, null);

    public void AllowSelectGoods(HandCard card) => Allow(Allowed.SelectGoods, card);

    public void AllowAttachBuff(HandCard card) => Allow(Allowed.AttachBuff, card);

    /// <summary>増額する（+5）だけを許す</summary>
    public void AllowRaise() => Allow(Allowed.Raise, null);

    public void AllowConfirm() => Allow(Allowed.Confirm, null);

    public void AllowBid() => Allow(Allowed.Bid, null);

    void Allow(Allowed action, HandCard card)
    {
        allowed = action;
        allowedCard = card;
    }

    public void SelectGoods(int player, HandCard card)
    {
        if (allowed == Allowed.SelectGoods && card == allowedCard)
            inner.SelectGoods(player, card);
    }

    public void AttachBuff(int player, HandCard card)
    {
        if (allowed == Allowed.AttachBuff && card == allowedCard)
            inner.AttachBuff(player, card);
    }

    public void SetRaise(int player, bool raise)
    {
        if (allowed == Allowed.Raise && raise)
            inner.SetRaise(player, true);
    }

    public void Confirm(int player)
    {
        if (allowed == Allowed.Confirm)
            inner.Confirm(player);
    }

    public void Bid(int player, int amount)
    {
        if (allowed == Allowed.Bid)
            inner.Bid(player, amount);
    }

    public void Fold(int player)
    {
    }

    public void Retire(int player) => inner.Retire(player);
}
