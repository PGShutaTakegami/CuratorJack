using System;

/// <summary>
/// 受け付ける回数を制限する（まとめて capacity 回まで、その後は毎秒 refillPerSecond 回ずつ回復）。
/// 改造したクライアントが RPC を大量に送ってきても、ホストが全員へ配り直さないようにする。
/// </summary>
public sealed class RpcRateLimiter
{
    readonly float capacity;
    readonly float refillPerSecond;
    float tokens;
    float lastTime = float.NaN;

    public RpcRateLimiter(float capacity, float refillPerSecond)
    {
        this.capacity = capacity;
        this.refillPerSecond = refillPerSecond;
        tokens = capacity;
    }

    /// <summary>now（秒）の時点で 1 回分受け付けられるなら消費して true</summary>
    public bool TryConsume(float now)
    {
        if (!float.IsNaN(lastTime) && now > lastTime)
            tokens = Math.Min(capacity, tokens + (now - lastTime) * refillPerSecond);
        lastTime = float.IsNaN(lastTime) ? now : Math.Max(lastTime, now);

        if (tokens < 1f)
            return false;

        tokens -= 1f;
        return true;
    }
}
