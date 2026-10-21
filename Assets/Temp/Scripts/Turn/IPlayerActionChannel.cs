/// <summary>
/// 操作を全員の画面へ配る経路（ネットワーク）。送った操作は、全員の画面で同じ順番に PlayerActionDispatcher.Receive へ届く。
/// </summary>
public interface IPlayerActionChannel
{
    void Send(PlayerAction action);
}
