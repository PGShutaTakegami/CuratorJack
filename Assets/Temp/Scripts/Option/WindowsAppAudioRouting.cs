#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;

/// <summary>
/// このプロセスの音声の出力先を、Windows の「アプリの音量とデバイスの設定」と同じ仕組みで指定する。
/// 公開されていない Windows.Media.Internal.AudioPolicyConfig を使う（Windows 10 1803 以降）。Windows のバージョンでインターフェイスの ID が違うため両方を試す。
/// 指定は Windows がアプリごとに覚えるため、既定のデバイスに戻すときは空の ID で消す。
/// </summary>
static class WindowsAppAudioRouting
{
    const string ClassName = "Windows.Media.Internal.AudioPolicyConfig";
    const string DevicePrefix = @"\\?\SWD#MMDEVAPI#";
    const string RenderInterface = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
    const int RenderFlow = 0;
    const int ConsoleRole = 0;
    const int MultimediaRole = 1;

    /// <summary>IUnknown(3) + IInspectable(3) + 使わないメソッド(19) の次</summary>
    const int SetPersistedDefaultEndpointSlot = 25;

    static readonly Guid[] FactoryIids =
    {
        new Guid("ab3d4648-e242-459f-b02f-541c70306324"),
        new Guid("2a59116d-6c4f-45e0-a74f-707e3fef9258"),
    };

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int SetPersistedDefaultAudioEndpoint(IntPtr self, uint processId, int flow, int role, IntPtr deviceId);

    /// <summary>出力先を endpointId（WindowsAudioEndpoints の ID）にする。空なら指定を消して OS の既定のデバイスに戻す。失敗したら例外</summary>
    public static void SetOutputDevice(string endpointId)
    {
        IntPtr factory = ActivateFactory();
        IntPtr deviceId = IntPtr.Zero;
        try
        {
            if (!string.IsNullOrEmpty(endpointId))
            {
                string path = DevicePrefix + endpointId + RenderInterface;
                Marshal.ThrowExceptionForHR(WindowsCreateString(path, path.Length, out deviceId));
            }

            IntPtr function = Marshal.ReadIntPtr(Marshal.ReadIntPtr(factory), SetPersistedDefaultEndpointSlot * IntPtr.Size);
            var setEndpoint = Marshal.GetDelegateForFunctionPointer<SetPersistedDefaultAudioEndpoint>(function);
            uint processId = GetCurrentProcessId();
            Marshal.ThrowExceptionForHR(setEndpoint(factory, processId, RenderFlow, ConsoleRole, deviceId));
            Marshal.ThrowExceptionForHR(setEndpoint(factory, processId, RenderFlow, MultimediaRole, deviceId));
        }
        finally
        {
            if (deviceId != IntPtr.Zero)
                WindowsDeleteString(deviceId);
            Marshal.Release(factory);
        }
    }

    static IntPtr ActivateFactory()
    {
        Marshal.ThrowExceptionForHR(WindowsCreateString(ClassName, ClassName.Length, out IntPtr className));
        try
        {
            int result = 0;
            foreach (Guid iid in FactoryIids)
            {
                Guid id = iid;
                result = RoGetActivationFactory(className, ref id, out IntPtr factory);
                if (result >= 0 && factory != IntPtr.Zero)
                    return factory;
            }
            Marshal.ThrowExceptionForHR(result);
            throw new NotSupportedException(ClassName);
        }
        finally
        {
            WindowsDeleteString(className);
        }
    }

    [DllImport("combase.dll")]
    static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    static extern int WindowsCreateString(string source, int length, out IntPtr hstring);

    [DllImport("combase.dll")]
    static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("kernel32.dll")]
    static extern uint GetCurrentProcessId();
}
#endif
