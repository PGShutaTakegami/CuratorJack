/// <summary>
/// ネットワーク越しの相手の操作役。相手の操作は PlayerActionDispatcher へ届いて反映されるため、ここでは何もしない。
/// </summary>
public sealed class RemotePlayerAgent : IPlayerAgent
{
    public RemotePlayerAgent(int playerNumber)
    {
        PlayerNumber = playerNumber;
    }

    public int PlayerNumber { get; }

    public void BeginListing(ListingSession session)
    {
    }

    public void BeginAuction(AuctionModel auction)
    {
    }

    public void EndPhase()
    {
    }
}
