/// <summary>
/// 繋がっているのに自分のプレイヤーが出てこない時間を数える。
/// 自分のプレイヤーが持つカメラが無い間は LoadingScreen が出続けるため、待ちすぎたら部屋を諦める目安にする。
/// </summary>
public sealed class LocalPlayerSpawnWatch
{
    readonly float waitSeconds;
    float missingSince = -1f;

    public LocalPlayerSpawnWatch(float waitSeconds)
    {
        this.waitSeconds = waitSeconds;
    }

    /// <summary>自分のプレイヤーが waitSeconds より長く出てこなければ true。</summary>
    public bool IsOverdue(bool hasLocalPlayer, float now)
    {
        if (hasLocalPlayer)
        {
            missingSince = -1f;
            return false;
        }

        if (missingSince < 0f)
            missingSince = now;

        return now - missingSince > waitSeconds;
    }

    /// <summary>接続が切れたら数え直す。</summary>
    public void Reset()
    {
        missingSince = -1f;
    }
}
