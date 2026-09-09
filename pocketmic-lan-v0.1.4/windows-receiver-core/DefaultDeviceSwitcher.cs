using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace PocketMicReceiver;

/// <summary>
/// Sets the Windows default recording device, so the virtual cable's capture endpoint becomes
/// the microphone applications pick up without the user opening Sound Settings.
///
/// This uses <c>IPolicyConfig</c>, which Microsoft has never documented. It is not a security
/// boundary and needs no entitlement — it is simply the interface every third-party default
/// device switcher has relied on since Windows Vista, unchanged for well over a decade. There
/// is no documented alternative: NAudio's <c>MMDeviceEnumerator</c> can only enumerate.
///
/// Because this changes a system-wide setting the user may not expect, it is never invoked
/// automatically — it is bound to an explicit button, and the previous default is remembered
/// so it can be put back.
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
        // Only SetDefaultEndpoint is used, but every preceding slot must be declared so the
        // vtable offsets line up. Signatures are deliberately loose (IntPtr) since we never
        // call them; getting the arity right is what matters.
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

    /// <summary>Substrings identifying a virtual cable's CAPTURE endpoint.</summary>
    private static readonly string[] CaptureHints =
    {
        "CABLE Output",
        "VB-Audio Virtual Cable",
        "VoiceMeeter Output",
        "PocketMic",
    };

    /// <summary>Finds the virtual cable's recording endpoint, or null when none is installed.</summary>
    public static MMDevice? FindVirtualCaptureDevice()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            foreach (var hint in CaptureHints)
            {
                foreach (var device in devices)
                {
                    if (device.FriendlyName.Contains(hint, StringComparison.OrdinalIgnoreCase))
                    {
                        return device;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Endpoint enumeration can fail on machines with a broken audio stack; a missing
            // convenience feature must never take the receiver down with it.
        }

        return null;
    }

    public static string? CurrentDefaultCaptureId()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            if (!enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)) return null;
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications).ID;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Sets a device as default for all three roles. Communications is the one that matters for
    /// call applications; Console and Multimedia are set too so behaviour is consistent whichever
    /// role an application asks for.
    /// </summary>
    public static bool TrySetDefaultCapture(string deviceId, out string error)
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

            foreach (var role in new[] { ERole.Console, ERole.Multimedia, ERole.Communications })
            {
                var hr = policy.SetDefaultEndpoint(deviceId, role);
                if (hr != 0)
                {
                    error = $"Windows rejected the default-device change (HRESULT 0x{hr:X8}).";
                    return false;
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            if (client is not null && Marshal.IsComObject(client))
            {
                Marshal.ReleaseComObject(client);
            }
        }
    }
}
