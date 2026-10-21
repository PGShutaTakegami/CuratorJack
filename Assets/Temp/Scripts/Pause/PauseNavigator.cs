using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ポーズ画面のリタイア・Quit と、リタイア後の選択を受けて進める。
/// リタイア：ポーズを閉じ、自分が生きていればリタイアを送って脱落し、観戦 / 最初から / タイトルへの選択を出す（試合から抜けられるのは SinglePlay だけ）。
/// 選択はポーズを開くと閉じる（脱落後にもう一度リタイアを押すと出し直す）。
/// Quit（準備中のみ）：部屋にいれば部屋を抜け、いなければ接続方式の片付けをしてからタイトルへ戻る。
/// 最初から：SinglePlay を同じ人数でスタート前からやり直す。
/// </summary>
public class PauseNavigator : MonoBehaviour
{
    [SerializeField] private PauseMenu menu;

    [SerializeField] private RetireChoiceMenu retireChoice;

    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private SinglePlaySessionBootstrap singlePlay;

    [Tooltip("接続方式ごとの片付け役（IMatchSession / IRoomSession）。動いているものを選んで使う")]
    [SerializeField] private MonoBehaviour[] sessions;

    [SerializeField] private string titleSceneName = "Temp_Title";

    void Awake()
    {
        menu.Opened += retireChoice.Hide;
        menu.RetireClicked += HandleRetire;
        menu.QuitClicked += HandleQuit;
        retireChoice.RestartClicked += HandleRestart;
        retireChoice.TitleClicked += HandleTitle;
        turnFlow.GameFinished += retireChoice.Hide;
    }

    void OnDestroy()
    {
        menu.Opened -= retireChoice.Hide;
        menu.RetireClicked -= HandleRetire;
        menu.QuitClicked -= HandleQuit;
        retireChoice.RestartClicked -= HandleRestart;
        retireChoice.TitleClicked -= HandleTitle;
        turnFlow.GameFinished -= retireChoice.Hide;
    }

    void HandleRetire()
    {
        menu.Close();
        int me = registry.LocalPlayerNumber;
        if (game.IsAlive(me))
            turnFlow.Commands.Retire(me);
        retireChoice.Show(singlePlay.IsActive);
    }

    void HandleQuit()
    {
        menu.Close();
        IRoomSession room = ActiveRoom();
        IMatchSession match = ActiveMatch();
        Func<Task> prepare = room != null ? room.PrepareLeaveRoomAsync : match != null ? match.PrepareTitleAsync : null;
        Leave(titleSceneName, prepare);
    }

    void HandleRestart()
    {
        IMatchSession match = ActiveMatch();
        Leave(SceneManager.GetActiveScene().name, match != null ? match.PrepareRetryAsync : null);
    }

    void HandleTitle()
    {
        IMatchSession match = ActiveMatch();
        Leave(titleSceneName, match != null ? match.PrepareTitleAsync : null);
    }

    void Leave(string sceneName, Func<Task> prepare)
    {
        if (SceneFader.IsFading)
            return;

        retireChoice.Hide();
        SceneFader.LoadScene(sceneName, prepare);
    }

    IMatchSession ActiveMatch()
    {
        foreach (MonoBehaviour behaviour in sessions)
        {
            if (behaviour is IMatchSession session && session.IsActive)
                return session;
        }

        return null;
    }

    IRoomSession ActiveRoom()
    {
        foreach (MonoBehaviour behaviour in sessions)
        {
            if (behaviour is IRoomSession session && session.IsActive)
                return session;
        }

        return null;
    }
}
