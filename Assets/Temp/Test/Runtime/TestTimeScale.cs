#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// テストプレイ用のゲーム速度（0.25〜32 倍の段階）。
/// ポーズ中（timeScale が 0）は止めたままにし、ポーズが解除されたら Keep で選んでいる速度をかけ直す。
/// </summary>
public static class TestTimeScale
{
    static readonly float[] Steps = { 0.25f, 0.5f, 1f, 2f, 4f, 8f, 16f, 32f };
    const int NormalIndex = 2;

    static int index = NormalIndex;
    static bool isControlling;

    public static float Current => Steps[index];

    public static bool IsNormal => index == NormalIndex;

    public static void Faster() => SetIndex(index + 1);

    public static void Slower() => SetIndex(index - 1);

    public static void ResetToNormal() => SetIndex(NormalIndex);

    /// <summary>scale に一番近い段階にする</summary>
    public static void Set(float scale)
    {
        int nearest = 0;
        for (int i = 1; i < Steps.Length; i++)
        {
            if (Mathf.Abs(Steps[i] - scale) < Mathf.Abs(Steps[nearest] - scale))
                nearest = i;
        }
        SetIndex(nearest);
    }

    /// <summary>ポーズの解除で別の速さに戻されたら、選んでいる速度をかけ直す（毎フレーム呼ぶ）</summary>
    public static void Keep()
    {
        if (isControlling && Time.timeScale > 0f && !Mathf.Approximately(Time.timeScale, Current))
            Time.timeScale = Current;
    }

    static void SetIndex(int value)
    {
        index = Mathf.Clamp(value, 0, Steps.Length - 1);
        isControlling = true;
        if (Time.timeScale > 0f)
            Time.timeScale = Current;
        Debug.Log($"[Test] ゲーム速度 x{Current}");
    }
}
#endif
