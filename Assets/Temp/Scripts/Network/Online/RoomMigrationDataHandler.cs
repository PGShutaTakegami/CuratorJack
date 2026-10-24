using Unity.Services.Multiplayer;

/// <summary>
/// オンラインの部屋でホストの引き継ぎ（Host Migration）を有効にするためのデータ受け渡し役。
/// 部屋の設定は全員が RoomConnectionContext に控えていて、部屋主待ちも各自が覚えているため、引き継ぐデータは無い（意味の無い 1 バイトを渡す）。
/// </summary>
public sealed class RoomMigrationDataHandler : IMigrationDataHandler
{
    public byte[] Generate() => new byte[1];

    public void Apply(byte[] migrationData)
    {
    }
}
