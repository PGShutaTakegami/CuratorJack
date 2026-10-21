/// <summary>効果カード・スペシャルカードが持つ効果の種類（グッズカードは None）</summary>
public enum CardEffect
{
    None,

    /// <summary>口コミ：効果カードを 1 枚追加でドロー</summary>
    WordOfMouth,

    /// <summary>真贋証明：出品カードに付いている効果カードを全員に公開</summary>
    CertificateOfAuthenticity,

    /// <summary>免罪符：次に受ける効果をすべて 1 回無効にする（強制）</summary>
    Indulgence,

    /// <summary>逆転：出品ターンの回る向きを逆にする</summary>
    Reversal,

    /// <summary>投げ売り：次に出品するときの最低出品額が 1〜5 下がる</summary>
    FireSale,

    /// <summary>圧力：出品カード（グッズ）のドローが 1 回できなくなる</summary>
    Pressure,

    /// <summary>伝手：出品カード（グッズ）を 1 枚ドロー</summary>
    Connections,

    /// <summary>値踏み：付いていた商品を購入した人が、出品者へ 1〜3 コインを追加で払う</summary>
    Markdown,

    /// <summary>2度目：今のターンの後に、もう一度出品ターンになる</summary>
    SecondTurn,

    /// <summary>横やり：次の出品で効果カードを付けられない</summary>
    Interference,

    /// <summary>鑑定：自分の手札の効果カードを 1 枚ランダムで公開</summary>
    Appraisal,

    /// <summary>黄金律：コインが 20 枚増える</summary>
    GoldenBoon,

    /// <summary>猶予の証：見送り権が 1 枚増える</summary>
    GraceToken,

    /// <summary>ランダム：全鑑定・全横やり・全圧力のどれかを発動</summary>
    Random,

    /// <summary>全鑑定：自分以外の全員（出品者も含む）の手札の効果カードを公開</summary>
    AppraisalAll,

    /// <summary>全横やり：自分以外の全員（出品者も含む）が、次の出品で効果カードを付けられない</summary>
    InterferenceAll,

    /// <summary>全圧力：自分以外の全員（出品者も含む）の、出品カード（グッズ）のドローを 1 回できなくする</summary>
    PressureAll
}
