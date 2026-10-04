/// <summary>
/// 戻る入力を画面内で処理するもの（入力欄を閉じるなど）。MonitorManager が表示中の画面から探して呼ぶ。
/// </summary>
public interface IBackInputHandler
{
    /// <summary>処理した場合は true を返す。false ならタイトルへ戻る</summary>
    bool HandleBack();
}
