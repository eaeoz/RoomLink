using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;

namespace RoomLink;

/// <summary>
/// Represents one audio endpoint (input or output) on the system.
/// </summary>
public sealed class AudioDeviceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required DataFlow Flow { get; init; }   // Capture = mic/in, Render = speaker/out
    public required bool IsBluetooth { get; init; }
    public required bool IsDefault { get; init; }

    public override string ToString() =>
        (IsBluetooth ? "[BT] " : "") + Name + (IsDefault ? "  (default)" : "");
}

/// <summary>
/// Enumerates WASAPI endpoints. Bluetooth headsets expose two endpoint pairs:
/// a stereo pair (A2DP) and a hands-free pair (HFP/HSP). For mic access we must
/// use the hands-free capture endpoint; NAudio/WASAPI handles the profile switch
/// automatically when the device is opened for recording.
/// </summary>
public static class DeviceService
{
    /// <summary>List all active capture (microphone) endpoints.</summary>
    public static List<AudioDeviceInfo> GetCaptureDevices()
    {
        return Enumerate(DataFlow.Capture);
    }

    /// <summary>List all active render (speaker/headphone) endpoints.</summary>
    public static List<AudioDeviceInfo> GetRenderDevices()
    {
        return Enumerate(DataFlow.Render);
    }

    private static List<AudioDeviceInfo> Enumerate(DataFlow flow)
    {
        var result = new List<AudioDeviceInfo>();
        using var enumerator = new MMDeviceEnumerator();

        string? defaultId = null;
        try
        {
            using var def = enumerator.GetDefaultAudioEndpoint(flow, Role.Console);
            defaultId = def.ID;
        }
        catch
        {
            // No default endpoint of this type exists.
        }

        foreach (var dev in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            result.Add(new AudioDeviceInfo
            {
                Id = dev.ID,
                Name = dev.FriendlyName,
                Flow = flow,
                IsBluetooth = IsBluetooth(dev),
                IsDefault = dev.ID == defaultId,
            });
        }

        return result.OrderBy(d => d.IsBluetooth ? 0 : 1).ThenBy(d => d.Name).ToList();
    }

    /// <summary>
    /// Heuristic Bluetooth detection: the Windows BT stack names endpoints
    /// "…Hands-Free…" for HFP/HSP pairs. A device whose capture and render
    /// endpoints share the same base name is a Bluetooth headset.
    /// </summary>
    private static bool IsBluetooth(MMDevice dev)
    {
        var name = dev.FriendlyName;
        return name.Contains("Hands-Free", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Handsfree", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase)
            || name.Contains("AirPods", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Open a capture device by id.</summary>
    public static MMDevice OpenCapture(string id)
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.GetDevice(id);
    }
}
