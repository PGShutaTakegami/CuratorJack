using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// プレイヤー 1 人分のアイコン（タイトルで描いた PNG）を全員へ配る。
/// 自分のプレイヤーが生成されたら PNG を分けてサーバーへ送り、サーバーは受け取ったものを全員へ配る。
/// 後から入った人は、生成されたときにサーバーへ今のアイコンを頼む。CPU やアイコンを描いていない人は何も持たない。
/// ホストが代わって生成し直されたときも、自分のプレイヤーが送り直す。
/// </summary>
public class NetworkPlayerIcon : NetworkBehaviour
{
    /// <summary>1 回の RPC で送る大きさ（UnityTransport の 1 回の上限より小さくする）</summary>
    const int ChunkSize = 4000;

    const int MaxChunks = (PlayerIconStore.MaxBytes + ChunkSize - 1) / ChunkSize;

    /// <summary>アイコン 1 枚分の送信を受け付ける間隔（秒）。送り直すたびに全員へ配り直すため、ホストが受け付ける回数を絞る</summary>
    const float UploadIntervalSeconds = 10f;

    /// <summary>1 人がアイコンを頼める間隔（秒）</summary>
    const float RequestIntervalSeconds = 5f;

    readonly IconChunkAssembly uploadAssembly = new(PlayerIconStore.MaxBytes, MaxChunks);
    readonly IconChunkAssembly deliveryAssembly = new(PlayerIconStore.MaxBytes, MaxChunks);
    readonly RpcRateLimiter uploadLimiter = new(MaxChunks, MaxChunks / UploadIntervalSeconds);
    readonly Dictionary<ulong, RpcRateLimiter> requestLimiters = new();

    byte[] serverIcon;
    int serverIconId;

    /// <summary>このプレイヤーのアイコン。届いていない・ないときは null</summary>
    public Texture2D Icon { get; private set; }

    /// <summary>アイコンが届いた（変わった）</summary>
    public event Action Changed;

    public override void OnNetworkSpawn()
    {
        if (IsLocalPlayer)
            Upload(PlayerIconStore.Current);
        else if (!IsServer)
            RequestIconRpc();
    }

    public override void OnNetworkDespawn()
    {
        serverIcon = null;
        SetIcon(null);
    }

    public override void OnDestroy()
    {
        if (Icon != null)
            Destroy(Icon);
        base.OnDestroy();
    }

    void Upload(byte[] png)
    {
        if (png == null || png.Length == 0 || png.Length > PlayerIconStore.MaxBytes)
            return;

        int id = UnityEngine.Random.Range(1, int.MaxValue);
        ForEachChunk(png, (index, count, chunk) => SubmitChunkRpc(id, index, count, chunk));
    }

    [Rpc(SendTo.Server)]
    void SubmitChunkRpc(int id, int index, int count, byte[] chunk, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || !uploadLimiter.TryConsume(Time.unscaledTime))
            return;

        byte[] png = uploadAssembly.Add(id, index, count, chunk);
        if (png == null)
            return;

        if (!SetIcon(png))
            return;

        serverIcon = png;
        serverIconId = id;
        Deliver(RpcTarget.NotServer);
    }

    [Rpc(SendTo.Server)]
    void RequestIconRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!requestLimiters.TryGetValue(sender, out RpcRateLimiter limiter))
            requestLimiters[sender] = limiter = new RpcRateLimiter(1f, 1f / RequestIntervalSeconds);

        if (serverIcon != null && limiter.TryConsume(Time.unscaledTime))
            Deliver(RpcTarget.Single(sender, RpcTargetUse.Temp));
    }

    void Deliver(BaseRpcTarget target)
    {
        ForEachChunk(serverIcon, (index, count, chunk) => DeliverChunkRpc(serverIconId, index, count, chunk, target));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void DeliverChunkRpc(int id, int index, int count, byte[] chunk, RpcParams rpcParams)
    {
        if (IsServer)
            return;

        byte[] png = deliveryAssembly.Add(id, index, count, chunk);
        if (png != null)
            SetIcon(png);
    }

    /// <summary>PNG からアイコンを作り直す（null なら消す）。読めない PNG なら false</summary>
    bool SetIcon(byte[] png)
    {
        Texture2D texture = png != null ? PlayerIconStore.CreateTexture(png) : null;
        if (png != null && texture == null)
            return false;

        if (Icon != null)
            Destroy(Icon);
        Icon = texture;
        Changed?.Invoke();
        return true;
    }

    static void ForEachChunk(byte[] png, Action<int, int, byte[]> send)
    {
        int count = (png.Length + ChunkSize - 1) / ChunkSize;
        for (int i = 0; i < count; i++)
        {
            int length = Mathf.Min(ChunkSize, png.Length - i * ChunkSize);
            var chunk = new byte[length];
            Buffer.BlockCopy(png, i * ChunkSize, chunk, 0, length);
            send(i, count, chunk);
        }
    }
}
