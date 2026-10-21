using System;
using UnityEngine;

/// <summary>
/// ゲーム開始の権限（自分がスタートできるか）と、開始済みかどうかを管理する。
/// 開始の実処理は、プレイ形式（SinglePlay / ネットワーク）ごとに登録された処理へ任せる。
/// </summary>
public class GameStartGate : MonoBehaviour
{
    Action startHandler;

    public bool CanLocalStart { get; private set; }

    public bool HasStarted { get; private set; }

    public event Action Changed;

    public event Action Started;

    /// <summary>自分にスタートを許可する。押されたときの処理を渡す</summary>
    public void AllowLocalStart(Action onStartRequested)
    {
        startHandler = onStartRequested;
        CanLocalStart = true;
        Changed?.Invoke();
    }

    public void RequestStart()
    {
        if (!CanLocalStart || HasStarted)
            return;

        startHandler?.Invoke();
    }

    /// <summary>ゲームが開始されたことを通知する（全員の画面で呼ばれる）</summary>
    public void NotifyStarted()
    {
        if (HasStarted)
            return;

        HasStarted = true;
        Changed?.Invoke();
        Started?.Invoke();
    }
}
