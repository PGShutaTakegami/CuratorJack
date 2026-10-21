/// <summary>
/// 今の手続き（出品・オークション）を待ち時間切れにする窓口。部屋主だけが使う。
/// </summary>
public interface IPhaseTimeouts
{
    /// <summary>
    /// 出品はグッズ未選択ならランダムなグッズで確定し、オークションは最高額の人以外を降ろす。
    /// </summary>
    /// <param name="sender">時間切れを送る部屋主のプレイヤー番号</param>
    void TimeOut(int sender);
}
