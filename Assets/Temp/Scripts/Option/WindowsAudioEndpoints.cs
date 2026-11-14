#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>
/// Windows の Core Audio から、有効なオーディオ出力デバイスの表示名と ID を取得する（Unity には出力デバイスを列挙する API がないため）。
/// </summary>
static class WindowsAudioEndpoints
{
    const int RenderFlow = 0;
    const int ActiveState = 1;
    const int ReadAccess = 0;
    const ushort WideStringType = 31;

    static readonly PropertyKey FriendlyNameKey = new PropertyKey(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    public readonly struct Endpoint
    {
        public readonly string Id;
        public readonly string Name;

        public Endpoint(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    public static List<string> GetOutputDeviceNames() => GetOutputDevices().ConvertAll(endpoint => endpoint.Name);

    /// <summary>表示名が name の出力デバイスの ID。見つからなければ null</summary>
    public static string FindOutputDeviceId(string name)
    {
        foreach (Endpoint endpoint in GetOutputDevices())
        {
            if (endpoint.Name == name)
                return endpoint.Id;
        }
        return null;
    }

    public static List<Endpoint> GetOutputDevices()
    {
        var endpoints = new List<Endpoint>();
        IMMDeviceEnumerator enumerator = null;
        IMMDeviceCollection collection = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(RenderFlow, ActiveState, out collection));
            Marshal.ThrowExceptionForHR(collection.GetCount(out int count));
            for (int i = 0; i < count; i++)
            {
                Endpoint endpoint = ReadEndpoint(collection, i);
                if (!string.IsNullOrEmpty(endpoint.Name) && !string.IsNullOrEmpty(endpoint.Id))
                    endpoints.Add(endpoint);
            }
        }
        finally
        {
            Release(collection);
            Release(enumerator);
        }

        return endpoints;
    }

    static Endpoint ReadEndpoint(IMMDeviceCollection collection, int index)
    {
        IMMDevice device = null;
        IPropertyStore store = null;
        var value = new PropVariant();
        try
        {
            Marshal.ThrowExceptionForHR(collection.Item(index, out device));
            Marshal.ThrowExceptionForHR(device.GetId(out string id));
            Marshal.ThrowExceptionForHR(device.OpenPropertyStore(ReadAccess, out store));
            PropertyKey key = FriendlyNameKey;
            Marshal.ThrowExceptionForHR(store.GetValue(ref key, out value));
            string name = value.Type == WideStringType ? Marshal.PtrToStringUni(value.Pointer) : null;
            return new Endpoint(id, name);
        }
        finally
        {
            PropVariantClear(ref value);
            Release(store);
            Release(device);
        }
    }

    static void Release(object comObject)
    {
        if (comObject != null)
            Marshal.ReleaseComObject(comObject);
    }

    [DllImport("ole32.dll")]
    static extern int PropVariantClear(ref PropVariant value);

    [StructLayout(LayoutKind.Sequential)]
    struct PropertyKey
    {
        public Guid FormatId;
        public int PropertyId;

        public PropertyKey(Guid formatId, int propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr Pointer;
        [FieldOffset(16)] public IntPtr Reserved;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int Item(int index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid interfaceId, int context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetAt(int index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    }
}
#endif
