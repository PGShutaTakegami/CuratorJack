using System;
using System.Collections.Generic;

/// <summary>
/// オプションで選んだデバイスの名前（OS の表示名）から、VC が使うデバイスを探す。
/// 名前が空なら OS の既定のデバイスを使う。見つからないときも既定のデバイスにする（抜いたマイクの名前が残っていても VC が使えるように）。
/// </summary>
public static class VoiceDeviceMatcher
{
    /// <summary>Vivox が OS の既定のデバイスに付ける ID</summary>
    public const string DefaultDeviceId = "Default System Device";

    public static T Find<T>(IEnumerable<T> devices, string wantedName, Func<T, string> nameOf, Func<T, string> idOf) where T : class
    {
        if (devices == null)
            return null;

        T fallback = null;
        T partial = null;
        foreach (T device in devices)
        {
            string name = nameOf(device) ?? string.Empty;
            if (idOf(device) == DefaultDeviceId)
                fallback = device;
            if (string.IsNullOrEmpty(wantedName))
                continue;
            if (name == wantedName)
                return device;
            if (partial == null && name.Length > 0 && (name.Contains(wantedName) || wantedName.Contains(name)))
                partial = device;
        }

        return partial ?? fallback;
    }
}
