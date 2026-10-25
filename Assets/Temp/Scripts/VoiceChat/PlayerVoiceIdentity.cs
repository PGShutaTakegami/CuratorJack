using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// プレイヤー 1 人分の VC 上の ID（Unity Authentication の PlayerId。Vivox の参加者の PlayerId と同じ）と、
/// 今声を送っているか（プッシュトゥトークのキーを押している／常時）を全員へ同期する。
/// ID は自分のプレイヤーから送ってサーバーだけが書き込み、送っているかは本人だけが書き込む。CPU や VC に入っていない人は空のまま。
/// </summary>
public class PlayerVoiceIdentity : NetworkBehaviour
{
    readonly NetworkVariable<FixedString64Bytes> voiceId = new NetworkVariable<FixedString64Bytes>();

    readonly NetworkVariable<bool> transmitting = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public string VoiceId => voiceId.Value.ToString();

    public bool IsTransmitting => transmitting.Value;

    /// <summary>自分のプレイヤーの VC 上の ID をサーバーへ送る。</summary>
    public void Report(string id)
    {
        if (!IsSpawned || !IsOwner || string.IsNullOrEmpty(id) || id == VoiceId)
            return;

        ReportRpc(id);
    }

    /// <summary>自分のプレイヤーが今声を送っているかを全員へ知らせる。</summary>
    public void SetTransmitting(bool value)
    {
        if (!IsSpawned || !IsOwner || transmitting.Value == value)
            return;

        transmitting.Value = value;
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
