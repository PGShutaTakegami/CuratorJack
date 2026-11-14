/// <summary>
/// 作成フォームで決めた部屋の設定。ルーム名が空なら作る側で自動の名前を付ける。パスワードが空ならパスワードなし。
/// </summary>
public sealed class RoomCreationRequest
{
    public string RoomName { get; }

    public int Capacity { get; }

    public string Password { get; }

    public string Message { get; }

    public bool HasPassword => !string.IsNullOrEmpty(Password);

    public RoomCreationRequest(string roomName, int capacity, string password, string message)
    {
        RoomName = (roomName ?? string.Empty).Trim();
        Capacity = capacity;
        Password = password ?? string.Empty;
        Message = (message ?? string.Empty).Trim();
    }
}
