/// <summary>
/// 自分の所持コインを上部のバーのリールに表示する。
/// </summary>
public class LocalCoinReel : LocalPlayerReel
{
    protected override int ValueOf(PlayerState player) => player.Coins;
}
