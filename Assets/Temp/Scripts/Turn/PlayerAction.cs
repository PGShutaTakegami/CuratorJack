/// <summary>プレイヤーが手続き（出品・オークション）に対して行う操作の種類</summary>
public enum PlayerActionKind
{
    SelectGoods,
    AttachBuff,
    SetRaise,
    Confirm,
    Bid,
    Fold,
    Retire,
    Timeout
}

/// <summary>
/// プレイヤーの操作 1 回分。どの手続き宛てか（Phase）と、誰が・何を・どの値で行ったかを持つ。
/// カードは出品を始めた時点の手札の並びでの位置、増額は 1 / 0、入札は金額を Value に入れる。
/// 時間切れは部屋主が送り、出品ではグッズ未選択のときに選ぶグッズの位置（選択済みなら -1）を Value に入れる。
/// </summary>
public readonly struct PlayerAction
{
    public PlayerAction(int phase, int player, PlayerActionKind kind, int value)
    {
        Phase = phase;
        Player = player;
        Kind = kind;
        Value = value;
    }

    public int Phase { get; }

    public int Player { get; }

    public PlayerActionKind Kind { get; }

    public int Value { get; }
}
