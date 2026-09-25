using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace PocketMicReceiver;

/// <summary>
/// Windows audio endpoint enumeration and per-role default capture access. Setting defaults uses
/// the undocumented IPolicyConfig interface; it is only called after an explicit user action.
/// </summary>
public static class DefaultDeviceSwitcher
{
    private enum ERole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2,
    }

    [Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        // Only SetDefaultEndpoint is used, but preceding slots must match the native vtable.
        [PreserveSig] int GetMixFormat(string deviceName, IntPtr format);
        [PreserveSig] int GetDeviceFormat(string deviceName, bool isDefault, IntPtr format);
        [PreserveSig] int ResetDeviceFormat(string deviceName);
        [PreserveSig] int SetDeviceFormat(string deviceName, IntPtr endpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod(string deviceName, bool isDefault, IntPtr defaultPeriod, IntPtr minimumPeriod);
        [PreserveSig] int SetProcessingPeriod(string deviceName, IntPtr period);
        [PreserveSig] int GetShareMode(string deviceName, IntPtr mode);
        [PreserveSig] int SetShareMode(string deviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue(string deviceName, bool fxStore, IntPtr key, IntPtr value);
        [PreserveSig] int SetPropertyValue(string deviceName, bool fxStore, IntPtr key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
        [PreserveSig] int SetEndpointVisibility(string deviceName, bool visible);
    }

    [ComImport]
    [Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class PolicyConfigClient
    {
    }

    public static CaptureEndpointResolution ResolveCaptureForRenderName(string renderFriendlyName)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            try
            {
                var endpoints = devices
                    .Select(device => new CaptureEndpointInfo(device.ID, device.FriendlyName))
                    .ToArray();
                return CaptureEndpointMatcher.Resolve(renderFriendlyName, endpoints);
            }
            finally
            {
                foreach (var device in devices) device.Dispose();
            }
        }
        catch
        {
            return new CaptureEndpointResolution(string.Empty, Array.Empty<CaptureEndpointInfo>());
        }
    }

    public static string? DefaultRenderFriendlyName()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            if (!enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)) return null;
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return device.FriendlyName;
        }
        catch
        {
            return null;
        }
    }

    public static CaptureDefaultRouting CreateRoutingController() =>
        new(new PolicyConfigCaptureEndpointAccess());

    private sealed class PolicyConfigCaptureEndpointAccess : IDefaultCaptureEndpointAccess
    {
        public string? GetDefaultEndpointId(CaptureDeviceRole role)
        {
            using var enumerator = new MMDeviceEnumerator();
            var naudioRole = ToNAudioRole(role);
            if (!enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, naudioRole)) return null;
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, naudioRole);
            return device.ID;
        }

        public bool TrySetDefaultEndpointId(string deviceId, CaptureDeviceRole role, out string error)
        {
            error = string.Empty;
            object? client = null;
            try
            {
                client = new PolicyConfigClient();
                if (client is not IPolicyConfig policy)
                {
                    error = "IPolicyConfig is unavailable on this version of Windows.";
                    return false;
                }

                var hr = policy.SetDefaultEndpoint(deviceId, ToPolicyRole(role));
                if (hr == 0) return true;
                error = $"Windows rejected the default-device change (HRESULT 0x{hr:X8}).";
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                if (client is not null && Marshal.IsComObject(client))
                    Marshal.ReleaseComObject(client);
            }
        }
    }

    private static Role ToNAudioRole(CaptureDeviceRole role) => role switch
    {
        CaptureDeviceRole.Console => Role.Console,
        CaptureDeviceRole.Multimedia => Role.Multimedia,
        CaptureDeviceRole.Communications => Role.Communications,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    private static ERole ToPolicyRole(CaptureDeviceRole role) => role switch
    {
        CaptureDeviceRole.Console => ERole.Console,
        CaptureDeviceRole.Multimedia => ERole.Multimedia,
        CaptureDeviceRole.Communications => ERole.Communications,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}
