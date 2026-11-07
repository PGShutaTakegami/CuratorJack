/// <summary>
/// ゲームで鳴らす効果音の種類。音の素材との対応は SoundEffectPlayer（Resources/SoundEffects）に設定する。
/// </summary>
public enum SoundId
{
    /// <summary>ボタンを押した</summary>
    UIClick = 0,
    /// <summary>入札・出品の確定</summary>
    Confirm = 1,
    /// <summary>手札のカードをクリックした</summary>
    CardTap = 2,
    /// <summary>カードを引いて手札に入った</summary>
    CardDraw = 3,
    /// <summary>カードがディゾルブで消える</summary>
    CardDissolve = 4,
    /// <summary>金額のリールが上がる</summary>
    ReelUp = 5,
    /// <summary>金額のリールが下がる</summary>
    ReelDown = 6,
    /// <summary>見送り権のチケットを切る</summary>
    TicketTear = 7,
    /// <summary>YOUR TURN・FIRST TURN・NEXT TURN の告知</summary>
    TurnChime = 8,
    /// <summary>FINAL BATTLE の告知</summary>
    FinalBattle = 9,
    /// <summary>勝者の告知</summary>
    Victory = 10,
    /// <summary>脱落の告知</summary>
    DropOut = 11,
    /// <summary>GAME START の告知</summary>
    GameStart = 12,
    /// <summary>落札などで所持金が増減した</summary>
    Coins = 13,
    /// <summary>使えない操作をした</summary>
    Denied = 14
}
