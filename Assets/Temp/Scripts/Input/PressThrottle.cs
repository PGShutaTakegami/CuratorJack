using UnityEngine;

/// <summary>
/// 連打対策。一度受け付けた操作は、決めた秒数のあいだ受け付けない（ポーズ中も進む実時間で数える）。
/// マウス・キーボード・コントローラーのどれから来ても同じ受け口で確かめるため、操作を実行する側に 1 つずつ持たせる。
/// </summary>
public sealed class PressThrottle
{
    public const float DefaultSeconds = 0.4f;

    readonly float seconds;
    float nextTime = float.NegativeInfinity;

    public PressThrottle(float seconds = DefaultSeconds)
    {
        this.seconds = seconds;
    }

    /// <summary>受け付けられるなら true を返し、そこから決めた秒数のあいだは false を返す</summary>
    public bool TryAccept()
    {
        float now = Time.unscaledTime;
        if (now < nextTime)
            return false;

        nextTime = now + seconds;
        return true;
    }
}
