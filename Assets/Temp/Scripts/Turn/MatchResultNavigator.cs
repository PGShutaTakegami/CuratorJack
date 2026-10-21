using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// リザルトのリトライ / タイトルを受けて、今動いている接続方式に片付けを任せてからシーンを切り替える。
/// リトライは GameScene を読み直し、SinglePlay ならスタート前、マルチプレイなら部屋の準備中に戻る。
/// </summary>
public class MatchResultNavigator : MonoBehaviour
{
    [SerializeField] private TurnUI ui;

    [SerializeField] private TurnFlowController turnFlow;

    [Tooltip("接続方式ごとの片付け役（IMatchSession）。動いているものを 1 つ選んで使う")]
    [SerializeField] private MonoBehaviour[] sessions;

    [SerializeField] private string titleSceneName = "Temp_Title";

    void Awake()
    {
        ui.RetryClicked += HandleRetry;
        ui.TitleClicked += HandleTitle;
        turnFlow.GameFinished += HandleGameFinished;
    }

    void OnDestroy()
    {
        ui.RetryClicked -= HandleRetry;
        ui.TitleClicked -= HandleTitle;
        turnFlow.GameFinished -= HandleGameFinished;
    }

    void HandleGameFinished() => ActiveSession()?.FinishMatch();

    void HandleRetry()
    {
        IMatchSession session = ActiveSession();
        Leave(SceneManager.GetActiveScene().name, session != null ? session.PrepareRetryAsync : null);
    }

    void HandleTitle()
    {
        IMatchSession session = ActiveSession();
        Leave(titleSceneName, session != null ? session.PrepareTitleAsync : null);
    }

    void Leave(string sceneName, Func<Task> prepare)
    {
        if (SceneFader.IsFading)
            return;

        ui.SetResultButtonsInteractable(false);
        SceneFader.LoadScene(sceneName, prepare);
    }

    IMatchSession ActiveSession()
    {
        foreach (MonoBehaviour behaviour in sessions)
        {
            if (behaviour is IMatchSession session && session.IsActive)
                return session;
        }

        return null;
    }
}
