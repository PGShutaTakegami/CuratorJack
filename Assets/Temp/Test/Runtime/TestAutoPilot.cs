#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// 自分の操作役を CPU と入れ替える（全員 CPU にして観戦したり、高速で流したりするため）。
/// 戻すと自分の操作（LocalPlayerAgent）が今の出品・オークションの続きを引き継ぐ。
/// </summary>
public static class TestAutoPilot
{
    static CpuPlayerAgent pilot;

    public static bool IsOn => pilot != null;

    public static void Toggle(TestSceneRefs refs)
    {
        if (IsOn)
            TurnOff(refs);
        else
            TurnOn(refs);
    }

    public static bool TurnOn(TestSceneRefs refs)
    {
        if (IsOn || !refs.IsGameScene || !refs.Registry.TryGet(refs.LocalPlayer, out Transform body))
            return IsOn;

        pilot = body.gameObject.AddComponent<CpuPlayerAgent>();
        pilot.Initialize(refs.LocalPlayer, refs.Game, refs.Flow.Commands);
        refs.Flow.ReplaceAgent(pilot);
        Debug.Log($"[Test] オートプレイ ON（Player{refs.LocalPlayer} を CPU が操作）");
        return true;
    }

    public static void TurnOff(TestSceneRefs refs)
    {
        if (!IsOn)
            return;

        if (refs.IsGameScene && refs.LocalAgent != null)
            refs.Flow.ReplaceAgent(refs.LocalAgent);
        Object.Destroy(pilot);
        pilot = null;
        Debug.Log("[Test] オートプレイ OFF");
    }
}
#endif
