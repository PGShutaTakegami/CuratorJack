using System;
using UnityEngine;

/// <summary>
/// オプションで選んだオーディオ出力デバイスへ、ゲームの音（SE・BGM）の出力先を切り替える。
/// Windows にこのアプリの出力先を指定してから、Unity の音声出力を開き直して新しい出力先を使わせる（開き直すと鳴っている音は止まる）。
/// 名前が空、または選んだデバイスが見つからないときは OS の既定のデバイスにする。切り替えられない環境では何もしない。
/// </summary>
public static class AudioOutputSwitcher
{
    static string appliedDevice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        appliedDevice = null;
    }

    public static void Apply(string deviceName)
    {
        deviceName ??= string.Empty;
        if (appliedDevice == deviceName)
            return;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        try
        {
            string endpointId = string.IsNullOrEmpty(deviceName) ? string.Empty : WindowsAudioEndpoints.FindOutputDeviceId(deviceName);
            if (endpointId == null)
            {
                Debug.LogWarning($"[Option] オーディオ出力デバイス「{deviceName}」が見つからないため、既定のデバイスを使います");
                endpointId = string.Empty;
            }
            WindowsAppAudioRouting.SetOutputDevice(endpointId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Option] オーディオ出力デバイスを切り替えられませんでした: {exception.Message}");
            return;
        }

        appliedDevice = deviceName;
        AudioSettings.Reset(AudioSettings.GetConfiguration());
#endif
    }
}
