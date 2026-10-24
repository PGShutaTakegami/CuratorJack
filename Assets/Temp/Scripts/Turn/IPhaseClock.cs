using System.Collections.Generic;

/// <summary>
/// 出品・オークションの待ち時間を、いつから数え、残り何秒かを決める時計。
/// 待ち時間を計り直すたびに番号（ステップ）を振り、この画面の演出が終わった知らせを受けてから数え始める。
/// </summary>
public interface IPhaseClock
{
    /// <summary>新しいステップを始める（全員の画面で同じ番号・同じ対象者で呼ぶ）。数え始めるのは ReportReady の後</summary>
    void BeginStep(int step, IReadOnlyCollection<int> targets, int waitSeconds);

    /// <summary>この画面の演出が終わり、数え始めてよいことを知らせる</summary>
    void ReportReady(int step);

    /// <summary>そのステップを数え始めていれば、時間切れまでの残り秒数を返す</summary>
    bool TryGetRemaining(int step, out float seconds);

    /// <summary>出品・オークションが終わり、計るのをやめる</summary>
    void EndStep();
}
