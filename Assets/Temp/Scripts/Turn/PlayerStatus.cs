/// <summary>効果でプレイヤーに残る持続効果。値は残りの回数（投げ売りだけは次の出品で下げる額）</summary>
public enum PlayerStatus
{
    /// <summary>圧力：出品カード（グッズ）のドローを飛ばす回数</summary>
    Pressure,

    /// <summary>横やり：効果カードを付けられない出品の回数</summary>
    Interference,

    /// <summary>免罪符：受ける効果をすべて無効にできる回数</summary>
    Indulgence,

    /// <summary>2度目：追加で出品ターンになる回数</summary>
    ExtraTurn,

    /// <summary>投げ売り：次の出品で最低出品額から下げる額</summary>
    FireSaleDiscount
}
