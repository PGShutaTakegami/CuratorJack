using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// プレイヤー 1 人分の VC 上の ID（Unity Authentication の PlayerId。Vivox の参加者の PlayerId と同じ）を全員へ同期する。
/// 自分のプレイヤーから送り、サーバーだけが書き込む。CPU や VC に入っていない人は空のまま。
/// </summary>
public class PlayerVoiceIdentity : NetworkBehaviour
{
    readonly NetworkVariable<FixedString64Bytes> voiceId = new NetworkVariable<FixedString64Bytes>();

    public string VoiceId => voiceId.Value.ToString();

    /// <summary>自分のプレイヤーの VC 上の ID をサーバーへ送る。</summary>
    public void Report(string id)
    {
        if (!IsSpawned || !IsOwner || string.IsNullOrEmpty(id) || id == VoiceId)
            return;

        ReportRpc(id);
    }

    [Rpc(SendTo.Server)]
    void ReportRpc(string id, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        var value = new FixedString64Bytes();
        value.CopyFromTruncated(id);
        voiceId.Value = value;
    }
}
