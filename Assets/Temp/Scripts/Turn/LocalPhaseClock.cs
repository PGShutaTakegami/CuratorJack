using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SinglePlay の待ち時間の時計。この画面の演出が終わったら数え始める。
/// ゲームの時間で計るため、ポーズ中は止まる。
/// </summary>
public sealed class LocalPhaseClock : IPhaseClock
{
    int step = -1;
    int waitSeconds;
    bool isStarted;
    float deadline;

    public void BeginStep(int step, IReadOnlyCollection<int> targets, int waitSeconds)
    {
        this.step = step;
        this.waitSeconds = waitSeconds;
        isStarted = false;
    }

    public void ReportReady(int step)
    {
        if (step != this.step || isStarted)
            return;

        isStarted = true;
        deadline = Time.time + waitSeconds;
    }

    public bool TryGetRemaining(int step, out float seconds)
    {
        bool counting = isStarted && step == this.step;
        seconds = counting ? Mathf.Max(0f, deadline - Time.time) : 0f;
        return counting;
    }

    public void EndStep()
    {
        step = -1;
        isStarted = false;
    }
}
