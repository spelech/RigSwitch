namespace RigSwitch.Infrastructure.Windows.CoreAudio;

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using RigSwitch.Core.Enums;
using RigSwitch.Core.Models;

/// <summary>
/// Implements <see cref="INativeAudioProvider"/> using Windows CoreAudio COM interfaces and registry fallback.
/// </summary>
public sealed partial class WindowsNativeAudioProvider : INativeAudioProvider
{
    private readonly IMMDeviceEnumerator? _deviceEnumerator;
    private readonly IPolicyConfig? _policyConfig;
    private readonly Action<string, bool>? _registryFallbackAction;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsNativeAudioProvider"/> class using live Windows COM activations.
    /// </summary>
    public WindowsNativeAudioProvider()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsNativeAudioProvider"/> class with explicit dependencies for testing.
    /// </summary>
    /// <param name="deviceEnumerator">Optional <see cref="IMMDeviceEnumerator"/> mock or instance.</param>
    /// <param name="policyConfig">Optional <see cref="IPolicyConfig"/> mock or instance.</param>
    /// <param name="registryFallbackAction">Optional delegate to intercept registry fallback in tests.</param>
    internal WindowsNativeAudioProvider(
        IMMDeviceEnumerator? deviceEnumerator,
        IPolicyConfig? policyConfig,
        Action<string, bool>? registryFallbackAction = null)
    {
        _deviceEnumerator = deviceEnumerator;
        _policyConfig = policyConfig;
        _registryFallbackAction = registryFallbackAction;
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    public IReadOnlyList<AudioEndpointInfo> EnumerateRenderEndpoints()
        => EnumerateEndpointsByFlow(EDataFlow.eRender, AudioDeviceFlow.Playback);

    /// <inheritdoc/>
    public IReadOnlyList<AudioEndpointInfo> EnumerateCaptureEndpoints()
        => EnumerateEndpointsByFlow(EDataFlow.eCapture, AudioDeviceFlow.Capture);

    private System.Collections.ObjectModel.ReadOnlyCollection<AudioEndpointInfo> EnumerateEndpointsByFlow(EDataFlow dataFlow, AudioDeviceFlow deviceFlow)
    {
        bool ownsEnumerator = _deviceEnumerator == null;
        var enumerator = _deviceEnumerator ?? CreateDeviceEnumerator();

        try
        {
            string? defaultPlaybackId = deviceFlow == AudioDeviceFlow.Playback
                ? GetDefaultEndpointId(enumerator, dataFlow, ERole.eMultimedia)
                : null;
            string? defaultCommunicationsId = GetDefaultEndpointId(enumerator, dataFlow, ERole.eCommunications);
            string? defaultCaptureId = deviceFlow == AudioDeviceFlow.Capture
                ? GetDefaultEndpointId(enumerator, dataFlow, ERole.eConsole)
                : null;

            const DevicePresenceState allStates =
                DevicePresenceState.Active |
                DevicePresenceState.Disabled |
                DevicePresenceState.NotPresent |
                DevicePresenceState.Unplugged;

            int hr = enumerator.EnumAudioEndpoints(dataFlow, allStates, out var collection);
            if (hr < 0 || collection == null)
            {
                Marshal.ThrowExceptionForHR(hr < 0 ? hr : unchecked((int)0x80004005));
                return [];
            }

            var endpoints = new List<AudioEndpointInfo>();
            try
            {
                hr = collection.GetCount(out uint count);
                if (hr < 0)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                for (uint i = 0; i < count; i++)
                {
                    hr = collection.Item(i, out var device);
                    if (hr != 0 || device == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (device.GetId(out var id) != 0 || string.IsNullOrWhiteSpace(id))
                        {
                            continue;
                        }

                        device.GetState(out var state);

                        string friendlyName = string.Empty;
                        string adapterDescription = string.Empty;

                        if (device.OpenPropertyStore(StorageAccessMode.Read, out var store) == 0 && store != null)
                        {
                            try
                            {
                                friendlyName = ReadStringProperty(store, PropertyKeys.PKEY_Device_FriendlyName);
                                if (string.IsNullOrWhiteSpace(friendlyName))
                                {
                                    friendlyName = ReadStringProperty(store, PropertyKeys.PKEY_DeviceInterface_FriendlyName);
                                }

                                adapterDescription = ReadStringProperty(store, PropertyKeys.PKEY_Device_DeviceDesc);
                            }
                            finally
                            {
                                SafeReleaseComObject(store);
                            }
                        }

                        if (string.IsNullOrWhiteSpace(friendlyName))
                        {
                            friendlyName = id;
                        }

                        bool isDefaultPlayback = !string.IsNullOrEmpty(defaultPlaybackId) &&
                            string.Equals(id, defaultPlaybackId, StringComparison.OrdinalIgnoreCase);

                        bool isDefaultCommunications = !string.IsNullOrEmpty(defaultCommunicationsId) &&
                            string.Equals(id, defaultCommunicationsId, StringComparison.OrdinalIgnoreCase);

                        bool isDefaultCapture = !string.IsNullOrEmpty(defaultCaptureId) &&
                            string.Equals(id, defaultCaptureId, StringComparison.OrdinalIgnoreCase);

                        endpoints.Add(new AudioEndpointInfo(
                            id: id,
                            name: friendlyName,
                            adapterDescription: adapterDescription,
                            state: state,
                            isDefaultPlayback: isDefaultPlayback,
                            isDefaultCommunications: isDefaultCommunications,
                            flow: deviceFlow,
                            isDefaultCapture: isDefaultCapture));
                    }
                    finally
                    {
                        SafeReleaseComObject(device);
                    }
                }
            }
            finally
            {
                SafeReleaseComObject(collection);
            }

            return endpoints.AsReadOnly();
        }
        finally
        {
            if (ownsEnumerator)
            {
                SafeReleaseComObject(enumerator);
            }
        }
    }

    /// <inheritdoc/>
    public void SetDefaultEndpoint(string endpointId, ERole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);

        bool ownsPolicyConfig = _policyConfig == null;
        var policyConfig = _policyConfig ?? CreatePolicyConfig();

        try
        {
            int hr = policyConfig.SetDefaultEndpoint(endpointId, role);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr < 0 ? hr : unchecked((int)0x80004005));
            }
        }
        finally
        {
            if (ownsPolicyConfig)
            {
                SafeReleaseComObject(policyConfig);
            }
        }
    }

    /// <inheritdoc/>
    public void SetEndpointVisibility(string endpointId, bool isVisible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);

        bool ownsPolicyConfig = _policyConfig == null;
        IPolicyConfig? policyConfig = null;
        bool comSucceeded = false;

        try
        {
            policyConfig = _policyConfig ?? CreatePolicyConfig();
            int hr = policyConfig.SetEndpointVisibility(endpointId, isVisible);
            if (hr == 0)
            {
                comSucceeded = true;
            }
        }
        catch
        {
            // COM call failed; attempt registry fallback
        }
        finally
        {
            if (ownsPolicyConfig)
            {
                SafeReleaseComObject(policyConfig);
            }
        }

        if (!comSucceeded)
        {
            if (_registryFallbackAction != null)
            {
                _registryFallbackAction(endpointId, isVisible);
            }
            else
            {
                ApplyRegistryVisibilityFallback(endpointId, isVisible);
            }
        }
    }

    /// <inheritdoc/>
    public void SetEndpointVolume(string endpointId, float scalarLevel, bool isMuted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);

        bool ownsEnumerator = _deviceEnumerator == null;
        var enumerator = _deviceEnumerator ?? CreateDeviceEnumerator();

        try
        {
            int hr = enumerator.GetDevice(endpointId, out var device);
            if (hr != 0 || device == null)
            {
                Marshal.ThrowExceptionForHR(hr < 0 ? hr : unchecked((int)0x80004005));
                return;
            }

            try
            {
                var iid = ComGuids.IidIAudioEndpointVolume;
                hr = device.Activate(ref iid, ComGuids.CLSCTX_INPROC_SERVER, IntPtr.Zero, out var obj);
                if (hr != 0 || obj is not IAudioEndpointVolume vol)
                {
                    Marshal.ThrowExceptionForHR(hr < 0 ? hr : unchecked((int)0x80004005));
                    return;
                }

                try
                {
                    var ctx = Guid.Empty;
                    vol.SetMasterVolumeLevelScalar(Math.Clamp(scalarLevel, 0.0f, 1.0f), ref ctx);
                    vol.SetMute(isMuted, ref ctx);
                }
                finally
                {
                    SafeReleaseComObject(vol);
                }
            }
            finally
            {
                SafeReleaseComObject(device);
            }
        }
        finally
        {
            if (ownsEnumerator)
            {
                SafeReleaseComObject(enumerator);
            }
        }
    }

    /// <inheritdoc/>
    public (float ScalarLevel, bool IsMuted) GetEndpointVolume(string endpointId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);

        bool ownsEnumerator = _deviceEnumerator == null;
        var enumerator = _deviceEnumerator ?? CreateDeviceEnumerator();

        try
        {
            int hr = enumerator.GetDevice(endpointId, out var device);
            if (hr != 0 || device == null)
            {
                return (1.0f, false);
            }

            try
            {
                var iid = ComGuids.IidIAudioEndpointVolume;
                hr = device.Activate(ref iid, ComGuids.CLSCTX_INPROC_SERVER, IntPtr.Zero, out var obj);
                if (hr != 0 || obj is not IAudioEndpointVolume vol)
                {
                    return (1.0f, false);
                }

                try
                {
                    vol.GetMasterVolumeLevelScalar(out float level);
                    vol.GetMute(out bool muted);
                    return (level, muted);
                }
                finally
                {
                    SafeReleaseComObject(vol);
                }
            }
            finally
            {
                SafeReleaseComObject(device);
            }
        }
        finally
        {
            if (ownsEnumerator)
            {
                SafeReleaseComObject(enumerator);
            }
        }
    }

    internal static string ExtractEndpointGuid(string endpointId)
    {
        var match = EndpointGuidRegex().Match(endpointId);
        if (!match.Success)
        {
            return endpointId.StartsWith('{') && endpointId.EndsWith('}') ? endpointId : $"{{{endpointId}}}";
        }

        string val = match.Value;
        return val.StartsWith('{') && val.EndsWith('}') ? val : $"{{{val}}}";
    }

    internal static void ApplyRegistryVisibilityFallback(string endpointId, bool isVisible)
    {
        string guidPart = ExtractEndpointGuid(endpointId);
        string renderKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{guidPart}";
        string captureKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture\{guidPart}";

        using var key = Registry.LocalMachine.OpenSubKey(renderKey, writable: true)
            ?? Registry.LocalMachine.OpenSubKey(captureKey, writable: true)
            ?? throw new InvalidOperationException($"Registry key for endpoint '{guidPart}' could not be opened for writing in Render or Capture.");

        // DeviceState: 1 = Active, 2 = Disabled
        int newState = isVisible ? 1 : 2;
        key.SetValue("DeviceState", newState, RegistryValueKind.DWord);
    }

    private static void SafeReleaseComObject(object? obj)
    {
        if (obj != null && Marshal.IsComObject(obj))
        {
            Marshal.ReleaseComObject(obj);
        }
    }

    private static IMMDeviceEnumerator CreateDeviceEnumerator()
    {
        var type = Type.GetTypeFromCLSID(ComGuids.ClsidMMDeviceEnumerator)
            ?? throw new InvalidOperationException("Failed to resolve MMDeviceEnumerator CLSID.");

        return (IMMDeviceEnumerator)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Failed to instantiate MMDeviceEnumerator."));
    }

    private static IPolicyConfig CreatePolicyConfig()
    {
        var type = Type.GetTypeFromCLSID(ComGuids.ClsidPolicyConfigClient)
            ?? throw new InvalidOperationException("Failed to resolve PolicyConfigClient CLSID.");

        return (IPolicyConfig)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Failed to instantiate PolicyConfigClient."));
    }

    private static string? GetDefaultEndpointId(IMMDeviceEnumerator enumerator, EDataFlow flow, ERole role)
    {
        try
        {
            int hr = enumerator.GetDefaultAudioEndpoint(flow, role, out var endpoint);
            if (hr == 0 && endpoint != null)
            {
                try
                {
                    if (endpoint.GetId(out var id) == 0)
                    {
                        return id;
                    }
                }
                finally
                {
                    SafeReleaseComObject(endpoint);
                }
            }
        }
        catch
        {
            // Default endpoint query may fail if no active render endpoints exist
        }

        return null;
    }

    private static string ReadStringProperty(IPropertyStore store, PROPERTYKEY key)
    {
        var pv = new PROPVARIANT();
        try
        {
            if (store.GetValue(ref key, out pv) == 0)
            {
                if (pv.vt == PropVariantNative.VT_LPWSTR && pv.pwszVal != IntPtr.Zero)
                {
                    return Marshal.PtrToStringUni(pv.pwszVal) ?? string.Empty;
                }
            }
        }
        finally
        {
            _ = PropVariantNative.PropVariantClear(ref pv);
        }

        return string.Empty;
    }

    [GeneratedRegex(@"(?:\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})", RegexOptions.RightToLeft)]
    private static partial Regex EndpointGuidRegex();
}
