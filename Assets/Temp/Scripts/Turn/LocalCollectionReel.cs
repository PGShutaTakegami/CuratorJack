/// <summary>
/// 自分が落札した商品の数を上部のバーのリールに表示する。
/// </summary>
public class LocalCollectionReel : LocalPlayerReel
{
    protected override int ValueOf(PlayerState player) => player.Collection.Count;
}
