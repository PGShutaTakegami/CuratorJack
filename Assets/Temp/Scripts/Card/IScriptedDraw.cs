/// <summary>
/// ドローで配るカードを、山札の代わりに決めておく仕組み（チュートリアルなど）。
/// 決めていないときは false を返し、山札から引かせる。
/// </summary>
public interface IScriptedDraw
{
    bool TryCreate(int player, CardManager.CardKinds kind, out HandCard card);
}
