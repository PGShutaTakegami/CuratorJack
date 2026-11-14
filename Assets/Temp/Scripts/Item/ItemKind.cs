/// <summary>アイテムの種類（使ったときの効果を決める）</summary>
public enum ItemKind
{
    /// <summary>虫眼鏡：今の出品に付いている効果カードを、使った人だけが見られる</summary>
    Magnifier,

    /// <summary>電気プラグ：見送り権の代わりに使えるシールドを 1 枚貼る</summary>
    PowerPlug,

    /// <summary>リモコン：自分以外の 1 人を選び、その人の次の出品ターンを飛ばす</summary>
    RemoteControl
}
