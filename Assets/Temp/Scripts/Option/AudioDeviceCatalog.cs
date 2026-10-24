using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 選べるオーディオ出力デバイスとマイクの名前を取得する。取得できない環境では空の一覧を返す。
/// マイクは Microphone.Start に渡せる名前（Microphone.devices）、出力は Windows の表示名。
/// </summary>
public static class AudioDeviceCatalog
{
    public static IReadOnlyList<string> GetOutputDevices()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        try
        {
            return WindowsAudioEndpoints.GetOutputDeviceNames();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Option] オーディオ出力デバイスを取得できませんでした: {exception.Message}");
        }
#endif
        return Array.Empty<string>();
    }

    public static IReadOnlyList<string> GetMicrophoneDevices() => Microphone.devices;
}
