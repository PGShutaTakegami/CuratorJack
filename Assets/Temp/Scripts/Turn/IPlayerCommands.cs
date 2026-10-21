/// <summary>
/// 操作役が今の手続き（出品・オークション）へ操作を送る窓口。
/// 送った操作がいつ反映されるか（すぐ / 全員の画面で同じ順に）は窓口の側が決める。
/// </summary>
public interface IPlayerCommands
{
    void SelectGoods(int player, HandCard card);

    void AttachBuff(int player, HandCard card);

    void SetRaise(int player, bool raise);

    void Confirm(int player);

    void Bid(int player, int amount);

    void Fold(int player);

    /// <summary>リタイアして脱落する（手続きの途中でなくても送れる）</summary>
    void Retire(int player);
}
