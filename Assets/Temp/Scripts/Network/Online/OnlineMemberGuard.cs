using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// ホストだけが働く。接続してきた画面が部屋（Lobby）のメンバーのものかを確かめ、キックした人を部屋から外して入り直させない。
/// 各画面は自分の接続番号を Lobby の自分のプロパティに書く（OnlineConnectionClaim）。Lobby のプロパティは本人しか書けないため、
/// 接続番号を名乗るメンバーがいない接続（部屋のメンバーでない画面が Relay から直接つないできたものなど）は、猶予を過ぎたら切る。
/// 名乗られた時点のメンバーを記録しておき、キックしたらそのメンバーを部屋から外してキック済みの一覧に載せる。
/// </summary>
public sealed class OnlineMemberGuard : IConnectionGuard
{
    /// <summary>Lobby のプレイヤーのプロパティで、接続番号を書くキー</summary>
    public const string ClaimKey = "netClient";

    public const string UnverifiedReason = "Unverified";

    readonly NetworkManager networkManager;
    readonly Func<ISession> currentSession;
    readonly KickedPlayerList kicked;
    readonly float graceSeconds;
    readonly Dictionary<ulong, float> unverified = new Dictionary<ulong, float>();
    readonly Dictionary<ulong, string[]> claimants = new Dictionary<ulong, string[]>();

    public OnlineMemberGuard(NetworkManager networkManager, Func<ISession> currentSession, KickedPlayerList kicked, float graceSeconds)
    {
        this.networkManager = networkManager;
        this.currentSession = currentSession;
        this.kicked = kicked;
        this.graceSeconds = graceSeconds;
    }

    /// <summary>
    /// Lobby に書く接続番号の値。ホストの引き継ぎで接続番号は振り直されるため、ホストの PlayerId を付けて、
    /// 前のホストのときの古い値が新しいホストの別の接続と一致しないようにする。
    /// </summary>
    public static string ClaimValue(string hostId, ulong clientId) => $"{hostId}:{clientId}";

    public bool Accepts(ConnectionPayload payload) => !kicked.Contains(payload.PlayerId);

    public void HandleConnected(ulong clientId)
    {
        if (clientId != NetworkManager.ServerClientId)
            unverified[clientId] = Time.realtimeSinceStartup;
    }

    public void HandleDisconnected(ulong clientId)
    {
        unverified.Remove(clientId);
        claimants.Remove(clientId);
    }

    public void HandleKicked(ulong clientId)
    {
        IEnumerable<string> ids = ClaimantsOf(clientId);
        if (claimants.TryGetValue(clientId, out string[] recorded))
            ids = ids.Concat(recorded);

        foreach (string playerId in ids.Distinct().ToArray())
        {
            kicked.Add(playerId);
            RemoveFromSession(playerId);
        }
    }

    /// <summary>Lobby の部屋に誰かが入ってきた。キック済みの人なら外す</summary>
    public void HandlePlayerJoined(string playerId)
    {
        if (kicked.Contains(playerId))
            RemoveFromSession(playerId);
    }

    /// <summary>毎フレーム呼ぶ。名乗られた接続を確認済みにし、猶予を過ぎても名乗られない接続を切る</summary>
    public void Tick()
    {
        ISession session = currentSession();
        if (unverified.Count == 0 || session == null || !session.IsHost || !networkManager.IsServer)
            return;

        float now = Time.realtimeSinceStartup;
        foreach (ulong clientId in unverified.Keys.ToArray())
        {
            string[] members = ClaimantsOf(clientId).Where(playerId => !kicked.Contains(playerId)).ToArray();
            if (members.Length > 0)
            {
                unverified.Remove(clientId);
                claimants[clientId] = members;
            }
            else if (now - unverified[clientId] > graceSeconds)
            {
                unverified.Remove(clientId);
                Debug.LogWarning(GameText.Format("log.session.unknown_client", clientId));
                networkManager.DisconnectClient(clientId, UnverifiedReason);
            }
        }
    }

    IEnumerable<string> ClaimantsOf(ulong clientId)
    {
        ISession session = currentSession();
        if (session?.Players == null)
            return Enumerable.Empty<string>();

        string value = ClaimValue(session.Host, clientId);
        return session.Players
            .Where(player => player.Properties != null
                && player.Properties.TryGetValue(ClaimKey, out PlayerProperty property)
                && property.Value == value)
            .Select(player => player.Id)
            .ToArray();
    }

    async void RemoveFromSession(string playerId)
    {
        ISession session = currentSession();
        if (session == null || !session.IsHost || session.Players == null || session.Players.All(player => player.Id != playerId))
            return;

        try
        {
            await session.AsHost().RemovePlayerAsync(playerId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(GameText.Format("log.session.kick_remove_failed", exception.Message));
        }
    }
}
