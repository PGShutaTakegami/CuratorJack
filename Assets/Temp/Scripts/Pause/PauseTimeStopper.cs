using UnityEngine;

/// <summary>
/// SinglePlay ではポーズ画面を開いている間ゲームの時間を止め、閉じたら元の速さに戻す（マルチプレイでは止めない）。
/// </summary>
public class PauseTimeStopper : MonoBehaviour
{
    [SerializeField] private PauseMenu menu;

    [SerializeField] private SinglePlaySessionBootstrap singlePlay;

    float resumeScale = 1f;
    bool isStopped;

    void Awake()
    {
        menu.Opened += Stop;
        menu.Closed += Resume;
    }

    void OnDestroy()
    {
        menu.Opened -= Stop;
        menu.Closed -= Resume;
        Resume();
    }

    void Stop()
    {
        if (!singlePlay.IsActive || isStopped)
            return;

        resumeScale = Time.timeScale;
        Time.timeScale = 0f;
        isStopped = true;
    }

    void Resume()
    {
        if (!isStopped)
            return;

        Time.timeScale = resumeScale;
        isStopped = false;
    }
}
