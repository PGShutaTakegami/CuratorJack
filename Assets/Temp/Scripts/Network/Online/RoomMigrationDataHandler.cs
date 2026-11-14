using Unity.Services.Multiplayer;

/// <summary>
/// オンラインの部屋でホストの引き継ぎ（Host Migration）を有効にするためのデータ受け渡し役。
/// 部屋の設定は全員が RoomConnectionContext に控えていて、部屋主待ちも各自が覚えているため、引き継ぐのはキック済みの一覧だけ。
/// </summary>
public sealed class RoomMigrationDataHandler : IMigrationDataHandler
{
    readonly KickedPlayerList kicked;

    public RoomMigrationDataHandler(KickedPlayerList kicked)
    {
        this.kicked = kicked;
    }

    /// <summary>空のデータは渡せないため、キックした人がいなければ意味の無い 1 バイトを渡す</summary>
    public byte[] Generate()
    {
        byte[] data = kicked.Serialize();
        return data.Length > 0 ? data : new byte[1];
    }

    public void Apply(byte[] migrationData)
    {
        kicked.Load(migrationData);
    }
}
