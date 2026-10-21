using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// マルチプレイの部屋の準備中（ゲーム開始前）に Esc が押されたら、部屋を抜けて MultiPlay 画面へ戻る。
/// </summary>
public class RoomLeaveInput : MonoBehaviour
{
    [SerializeField] private GameStartGate startGate;

    [Tooltip("部屋の片付け役（IRoomSession）。動いているものを 1 つ選んで使う")]
    [SerializeField] private MonoBehaviour[] sessions;

    [Tooltip("戻る先のシーン（MultiPlay 画面）")]
    [SerializeField] private string lobbySceneName = "HumanMonitorScene";

    void Update()
    {
        if (startGate.HasStarted || SceneFader.IsFading)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return;

        IRoomSession session = ActiveSession();
        if (session != null)
            SceneFader.LoadScene(lobbySceneName, session.PrepareLeaveRoomAsync);
    }

    IRoomSession ActiveSession()
    {
        foreach (MonoBehaviour behaviour in sessions)
        {
            if (behaviour is IRoomSession session && session.IsActive)
                return session;
        }

        return null;
    }
}
