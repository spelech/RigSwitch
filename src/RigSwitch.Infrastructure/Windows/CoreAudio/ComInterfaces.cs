namespace RigSwitch.Infrastructure.Windows.CoreAudio;

using System.Runtime.InteropServices;
using RigSwitch.Core.Enums;

#pragma warning disable CA1051 // Do not declare visible instance fields
#pragma warning disable CA1069 // Enums values should not be duplicated
#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
#pragma warning disable CA1712 // Do not prefix enum values with type name
#pragma warning disable CA1815 // Override equals and operator equals on value types
#pragma warning disable CS0649 // Field is never assigned to

/// <summary>
/// CoreAudio endpoint role used by PolicyConfig and MMDevice APIs.
/// </summary>
public enum ERole
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2,
}

/// <summary>
/// CoreAudio data flow direction.
/// </summary>
public enum EDataFlow
{
    eRender = 0,
    eCapture = 1,
    eAll = 2,
}

/// <summary>
/// Storage access mode flags for opening an MMDevice property store.
/// </summary>
public enum StorageAccessMode
{
    Read = 0,
    Write = 1,
    ReadWrite = 2,
}

/// <summary>
/// Specifies a unique property key for CoreAudio property stores.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PROPERTYKEY
{
    public Guid fmtid;
    public uint pid;

    public PROPERTYKEY(Guid fmtid, uint pid)
    {
        this.fmtid = fmtid;
        this.pid = pid;
    }
}

/// <summary>
/// Well-known CoreAudio property keys.
/// </summary>
public static class PropertyKeys
{
    public static readonly PROPERTYKEY PKEY_Device_FriendlyName =
        new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);

    public static readonly PROPERTYKEY PKEY_DeviceInterface_FriendlyName =
        new(new Guid("026e516e-b814-414b-83cd-856d6fef4822"), 2);

    public static readonly PROPERTYKEY PKEY_Device_DeviceDesc =
        new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 2);
}

/// <summary>
/// Minimal PROPVARIANT structure representation for property value extraction.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct PROPVARIANT
{
    [FieldOffset(0)]
    public ushort vt;

    [FieldOffset(2)]
    public ushort wReserved1;

    [FieldOffset(4)]
    public ushort wReserved2;

    [FieldOffset(6)]
    public ushort wReserved3;

    [FieldOffset(8)]
    public IntPtr pwszVal;

    [FieldOffset(8)]
    public int intVal;

    [FieldOffset(8)]
    public uint uintVal;
}

internal static class PropVariantNative
{
    public const ushort VT_LPWSTR = 31;

    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern int PropVariantClear(ref PROPVARIANT pvar);
}

/// <summary>
/// CoreAudio property store COM interface.
/// </summary>
[ComImport]
[Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPropertyStore
{
    [PreserveSig]
    int GetCount(out uint cProps);

    [PreserveSig]
    int GetAt(uint iProp, out PROPERTYKEY pkey);

    [PreserveSig]
    int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);

    [PreserveSig]
    int SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);

    [PreserveSig]
    int Commit();
}

/// <summary>
/// CoreAudio device COM interface.
/// </summary>
[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

    [PreserveSig]
    int OpenPropertyStore(StorageAccessMode stgmAccess, out IPropertyStore ppProperties);

    [PreserveSig]
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);

    [PreserveSig]
    int GetState(out DevicePresenceState pdwState);
}

/// <summary>
/// CoreAudio device collection COM interface.
/// </summary>
[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDeviceCollection
{
    [PreserveSig]
    int GetCount(out uint pcDevices);

    [PreserveSig]
    int Item(uint nDevice, out IMMDevice ppDevice);
}

/// <summary>
/// CoreAudio device enumerator COM interface.
/// </summary>
[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(EDataFlow dataFlow, DevicePresenceState dwStateMask, out IMMDeviceCollection ppDevices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);

    [PreserveSig]
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);

    [PreserveSig]
    int RegisterEndpointNotificationCallback(IntPtr pClient);

    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IntPtr pClient);
}

/// <summary>
/// Undocumented Windows audio policy configuration COM interface.
/// </summary>
[ComImport]
[Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPolicyConfig
{
    [PreserveSig]
    int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr ppFormat);

    [PreserveSig]
    int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bDefault, IntPtr ppFormat);

    [PreserveSig]
    int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);

    [PreserveSig]
    int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);

    [PreserveSig]
    int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);

    [PreserveSig]
    int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);

    [PreserveSig]
    int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);

    [PreserveSig]
    int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);

    [PreserveSig]
    int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bFxStore, IntPtr key, IntPtr pv);

    [PreserveSig]
    int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bFxStore, IntPtr key, IntPtr pv);

    [PreserveSig]
    int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, ERole eRole);

    [PreserveSig]
    int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, [MarshalAs(UnmanagedType.Bool)] bool bVisible);
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject
{
}

[ComImport]
[Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
internal class PolicyConfigClientComObject
{
}

/// <summary>
/// GUID constants for CoreAudio COM classes and interfaces.
/// </summary>
public static class ComGuids
{
    public static readonly Guid ClsidMMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    public static readonly Guid IidMMDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    public static readonly Guid ClsidPolicyConfigClient = new("870af99c-171d-4f9e-af0d-e63df40c2bc9");
    public static readonly Guid IidPolicyConfig = new("f8679f50-850a-41cf-9c72-430f290290c8");
}
