using System;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// 自分の接続番号を Lobby の自分のプロパティに書き、ホストが「この接続は部屋のメンバーのもの」と確かめられるようにする（OnlineMemberGuard）。
/// ホストの引き継ぎで繋ぎ直すとホストと接続番号が変わるため、変わるたびに書き直す。書けなかったら少し待ってやり直す。
/// </summary>
public sealed class OnlineConnectionClaim
{
    readonly NetworkManager networkManager;
    readonly Func<ISession> currentSession;
    readonly float retrySeconds;
    ISession claimedSession;
    string claimedValue;
    float nextTryAt;
    bool isSaving;

    public OnlineConnectionClaim(NetworkManager networkManager, Func<ISession> currentSession, float retrySeconds)
    {
        this.networkManager = networkManager;
        this.currentSession = currentSession;
        this.retrySeconds = retrySeconds;
    }

    /// <summary>毎フレーム呼ぶ</summary>
    public void Tick()
    {
        ISession session = currentSession();
        if (session?.CurrentPlayer == null || isSaving || networkManager == null || !networkManager.IsConnectedClient || networkManager.IsServer)
            return;

        if (session != claimedSession)
        {
            claimedSession = session;
            claimedValue = null;
        }

        string value = OnlineMemberGuard.ClaimValue(session.Host, networkManager.LocalClientId);
        if (value == claimedValue || Time.realtimeSinceStartup < nextTryAt)
            return;

        Save(session, value);
    }

    async void Save(ISession session, string value)
    {
        isSaving = true;
        try
        {
            session.CurrentPlayer.SetProperty(OnlineMemberGuard.ClaimKey, new PlayerProperty(value, VisibilityPropertyOptions.Member));
            await session.SaveCurrentPlayerDataAsync();
            if (session == claimedSession)
                claimedValue = value;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(GameText.Format("log.session.claim_failed", exception.Message));
            nextTryAt = Time.realtimeSinceStartup + retrySeconds;
        }
        finally
        {
            isSaving = false;
        }
    }
}
