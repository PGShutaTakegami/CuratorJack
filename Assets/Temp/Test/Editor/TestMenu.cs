using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// テストプレイの道具をメニュー（CJ/Test）から使う。どれも Play 中だけ使える。
/// </summary>
static class TestMenu
{
    const string Root = "CJ/Test/";

    [MenuItem(Root + "シングル 4 人で GameScene を読み直す")]
    static void ReloadSingle4() => ReloadSingle(4);

    [MenuItem(Root + "シングル 2 人で GameScene を読み直す")]
    static void ReloadSingle2() => ReloadSingle(2);

    [MenuItem(Root + "スタート")]
    static void StartGame() => TestSceneRefs.Find().StartGate?.RequestStart();

    [MenuItem(Root + "オートプレイ切り替え（自分を CPU に）")]
    static void ToggleAutoPilot() => TestAutoPilot.Toggle(TestSceneRefs.Find());

    [MenuItem(Root + "待ち時間切れにする")]
    static void TimeOutNow() => TestCheats.TimeOutNow(TestSceneRefs.Find());

    [MenuItem(Root + "速度/x1")]
    static void Speed1() => TestTimeScale.Set(1f);

    [MenuItem(Root + "速度/x4")]
    static void Speed4() => TestTimeScale.Set(4f);

    [MenuItem(Root + "速度/x8")]
    static void Speed8() => TestTimeScale.Set(8f);

    [MenuItem(Root + "速度/x16")]
    static void Speed16() => TestTimeScale.Set(16f);

    [MenuItem(Root + "速度/x32")]
    static void Speed32() => TestTimeScale.Set(32f);

    [MenuItem(Root + "シミュレーション/4 人 × 10 試合")]
    static void Simulate4x10() => MatchSimulationRunner.Begin(10, 4);

    [MenuItem(Root + "シミュレーション/3 人 × 10 試合")]
    static void Simulate3x10() => MatchSimulationRunner.Begin(10, 3);

    [MenuItem(Root + "シミュレーション/2 人 × 10 試合")]
    static void Simulate2x10() => MatchSimulationRunner.Begin(10, 2);

    [MenuItem(Root + "シミュレーション/中止")]
    static void CancelSimulation() => MatchSimulationRunner.Cancel();

    [MenuItem(Root + "シングル 4 人で GameScene を読み直す", true)]
    [MenuItem(Root + "シングル 2 人で GameScene を読み直す", true)]
    [MenuItem(Root + "スタート", true)]
    [MenuItem(Root + "オートプレイ切り替え（自分を CPU に）", true)]
    [MenuItem(Root + "待ち時間切れにする", true)]
    [MenuItem(Root + "速度/x1", true)]
    [MenuItem(Root + "速度/x4", true)]
    [MenuItem(Root + "速度/x8", true)]
    [MenuItem(Root + "速度/x16", true)]
    [MenuItem(Root + "速度/x32", true)]
    [MenuItem(Root + "シミュレーション/4 人 × 10 試合", true)]
    [MenuItem(Root + "シミュレーション/3 人 × 10 試合", true)]
    [MenuItem(Root + "シミュレーション/2 人 × 10 試合", true)]
    [MenuItem(Root + "シミュレーション/中止", true)]
    static bool IsPlaying() => EditorApplication.isPlaying;

    static void ReloadSingle(int players)
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Object.Destroy(NetworkManager.Singleton.gameObject);
        }
        SinglePlayContext.Set(players);
        SceneManager.LoadScene("GameScene");
    }
}
