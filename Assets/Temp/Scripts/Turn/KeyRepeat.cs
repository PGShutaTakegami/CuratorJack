using UnityEngine;

/// <summary>
/// キーの長押しを、一定間隔の連続入力に変える。押した瞬間に 1 回、delay 秒押し続けると連続で入力し、
/// 押し続けるほど間隔を縮める（最短 fastestInterval）。
/// </summary>
public sealed class KeyRepeat
{
    readonly float delay;
    readonly float interval;
    readonly float fastestInterval;
    readonly float acceleration;

    float untilNext;
    float currentInterval;

    public KeyRepeat(float delay = 0.4f, float interval = 0.1f, float fastestInterval = 0.03f, float acceleration = 0.85f)
    {
        this.delay = delay;
        this.interval = interval;
        this.fastestInterval = fastestInterval;
        this.acceleration = acceleration;
    }

    /// <summary>このフレームの入力回数を返す（押していなければ 0）</summary>
    public int Tick(bool pressedThisFrame, bool isPressed, float deltaTime)
    {
        if (pressedThisFrame)
        {
            untilNext = delay;
            currentInterval = interval;
            return 1;
        }

        if (!isPressed)
            return 0;

        untilNext -= deltaTime;
        int count = 0;
        while (untilNext <= 0f)
        {
            count++;
            untilNext += currentInterval;
            currentInterval = Mathf.Max(fastestInterval, currentInterval * acceleration);
        }
        return count;
    }
}
