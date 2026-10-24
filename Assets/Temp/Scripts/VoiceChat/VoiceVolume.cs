using UnityEngine;

/// <summary>
/// オプションの音量（0〜1）を、Vivox の出力の音量（dB。0 がそのまま、-50 が最小）に変える。
/// </summary>
public static class VoiceVolume
{
    public const int Min = -50;

    /// <summary>0 は無音（出力をミュートする）として扱う</summary>
    public static bool IsSilent(float volume) => volume <= 0f;

    public static int ToVivox(float volume)
    {
        if (IsSilent(volume))
            return Min;

        return Mathf.Clamp(Mathf.RoundToInt(20f * Mathf.Log10(volume)), Min, 0);
    }
}
