/// <summary>
/// 効果カード・スペシャルカードが持つ効果の種類（グッズカードは None）。
/// カードのデータには番号で保存されるため、値を消しても残りの番号は変えない。
/// </summary>
public enum CardEffect
{
    None = 0,

    /// <summary>口コミ：効果カードを 1 枚追加でドロー</summary>
    WordOfMouth = 1,

    /// <summary>免罪符：次に受ける効果をすべて 1 回無効にする（強制）</summary>
    Indulgence = 3,

    /// <summary>逆転：出品ターンの回る向きを逆にする</summary>
    Reversal = 4,

    /// <summary>投げ売り：次に出品するときの最低出品額が 1〜5 下がる</summary>
    FireSale = 5,

    /// <summary>圧力：出品カード（グッズ）のドローが 1 回できなくなる</summary>
    Pressure = 6,

    /// <summary>伝手：出品カード（グッズ）を 1 枚ドロー</summary>
    Connections = 7,

    /// <summary>値踏み：付いていた商品を購入した人が、出品者へ 1〜3 コインを追加で払う</summary>
    Markdown = 8,

    /// <summary>2度目：今のターンの後に、もう一度出品ターンになる</summary>
    SecondTurn = 9,

    /// <summary>横やり：次の出品で効果カードを付けられない</summary>
    Interference = 10,

    /// <summary>黄金律：コインが 20 枚増える</summary>
    GoldenBoon = 12,

    /// <summary>猶予の証：見送り権が 1 枚増える</summary>
    GraceToken = 13,

    /// <summary>ランダム：全横やり・全圧力のどちらかを発動</summary>
    Random = 14,

    /// <summary>全横やり：自分以外の全員（出品者も含む）が、次の出品で効果カードを付けられない</summary>
    InterferenceAll = 16,

    /// <summary>全圧力：自分以外の全員（出品者も含む）の、出品カード（グッズ）のドローを 1 回できなくする</summary>
    PressureAll = 17
}
