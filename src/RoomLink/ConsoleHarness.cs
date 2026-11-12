using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RoomLink;

namespace RoomLink;

/// <summary>
/// The Phase-1 interactive console harness, kept for development.
/// Run with: RoomLink.exe --cli
/// </summary>
internal static class ConsoleHarness
{
    public static int Run()
    {
        var settings = Settings.Load();

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== RoomLink v0.2 (console harness) ===");
            Console.WriteLine(" [1] List audio devices");
            Console.WriteLine(" [2] Auto-pick devices and START bridge");
            Console.WriteLine(" [3] Manually pick devices and START bridge");
            Console.WriteLine(" [4] Show saved settings");
            Console.WriteLine(" [Q] Quit");
            Console.Write("> ");
            var key = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (key is "q" or null) break;

            switch (key)
            {
                case "1": ListDevices(); break;
                case "2": Run(AutoPick(), settings); break;
                case "3": Run(PickManually(), settings); break;
                case "4":
                    Console.WriteLine($"  headsetMic  : {settings.HeadsetMicId ?? "(unset)"}");
                    Console.WriteLine($"  headsetSpk  : {settings.HeadsetSpeakerId ?? "(unset)"}");
                    Console.WriteLine($"  localMic    : {settings.LocalMicId ?? "(unset)"}");
                    Console.WriteLine($"  localSpk    : {settings.LocalSpeakerId ?? "(unset)"}");
                    break;
            }
        }
        return 0;
    }

    // ---------------------------------------------------------------------------

    static void ListDevices()
    {
        Console.WriteLine("\n-- Capture (microphone) endpoints --");
        foreach (var d in DeviceService.GetCaptureDevices())
            Console.WriteLine($"   in : {d}");

        Console.WriteLine("\n-- Render (speaker/headphone) endpoints --");
        foreach (var d in DeviceService.GetRenderDevices())
            Console.WriteLine($"   out: {d}");
    }

    static (string? headsetMic, string? headsetSpk, string? localMic, string? localSpk) AutoPick()
    {
        var ins = DeviceService.GetCaptureDevices();
        var outs = DeviceService.GetRenderDevices();

        // Bluetooth device = one that appears on BOTH flows (BT headset = mic + speaker).
        var btIns = ins.Where(i => i.IsBluetooth).ToList();
        var btOuts = outs.Where(o => o.IsBluetooth).ToList();

        string? headsetMic = btIns.FirstOrDefault()?.Id;
        string? headsetSpk = btOuts.FirstOrDefault()?.Id;

        // Local devices = default endpoints that are NOT the headset.
        string? localMic = ins.FirstOrDefault(d => d.IsDefault && d.Id != headsetMic)?.Id
                           ?? ins.FirstOrDefault(d => d.Id != headsetMic)?.Id;
        string? localSpk = outs.FirstOrDefault(d => d.IsDefault && d.Id != headsetSpk)?.Id
                           ?? outs.FirstOrDefault(d => d.Id != headsetSpk)?.Id;

        Console.WriteLine($"\n  headsetMic : {ins.FirstOrDefault(i => i.Id == headsetMic) ?? null}");
        Console.WriteLine($"  headsetSpk : {outs.FirstOrDefault(o => o.Id == headsetSpk) ?? null}");
        Console.WriteLine($"  localMic   : {ins.FirstOrDefault(i => i.Id == localMic) ?? null}");
        Console.WriteLine($"  localSpk   : {outs.FirstOrDefault(o => o.Id == localSpk) ?? null}");

        return (headsetMic, headsetSpk, localMic, localSpk);
    }

    static (string?, string?, string?, string?) PickManually()
    {
        var headsetMic = Pick("Headset MICROPHONE (capture)", DeviceService.GetCaptureDevices());
        var headsetSpk = Pick("Headset SPEAKER (render)", DeviceService.GetRenderDevices());
        var localMic = Pick("Local MICROPHONE (capture)", DeviceService.GetCaptureDevices());
        var localSpk = Pick("Local SPEAKER (render)", DeviceService.GetRenderDevices());
        return (headsetMic, headsetSpk, localMic, localSpk);
    }

    static string? Pick(string title, List<AudioDeviceInfo> devices)
    {
        Console.WriteLine($"\n-- {title} --");
        for (int i = 0; i < devices.Count; i++)
            Console.WriteLine($"  [{i}] {devices[i]}");
        Console.Write("  number: ");
        if (int.TryParse(Console.ReadLine(), out int n) && n >= 0 && n < devices.Count)
            return devices[n].Id;
        Console.WriteLine("  invalid, skipped");
        return null;
    }

    static void Run((string?, string?, string?, string?) ids, Settings settings)
    {
        var (headsetMic, headsetSpk, localMic, localSpk) = ids;

        if (headsetMic == null || headsetSpk == null || localMic == null || localSpk == null)
        {
            Console.WriteLine("\n! Missing endpoints — connect the Bluetooth headset first, or pick manually.");
            return;
        }

        // Persist for next time.
        settings.HeadsetMicId = headsetMic;
        settings.HeadsetSpeakerId = headsetSpk;
        settings.LocalMicId = localMic;
        settings.LocalSpeakerId = localSpk;
        settings.Save();

        using var headsetToLocal = new Bridge("BT-mic → Speakers", headsetMic, localSpk);
        using var localToHeadset = new Bridge("Mic → BT-speaker", localMic, headsetSpk);

        Console.WriteLine("\n>>> TALK. You should hear yourself in the headset, and the headset");
        Console.WriteLine(">>> mic out of your speakers. Press [S] + Enter to stop.\n");

        try
        {
            headsetToLocal.Start();
            localToHeadset.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"! Failed to start: {ex.Message}");
            return;
        }

        while (true)
        {
            var line = Console.ReadLine();
            if (line == null || line.Trim().Equals("s", StringComparison.OrdinalIgnoreCase))
                break;
            if (line.Trim().Equals("v", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine(
                    $"  buffer A: {headsetToLocal.BufferedMilliseconds} ms, " +
                    $"buffer B: {localToHeadset.BufferedMilliseconds} ms");
        }

        headsetToLocal.Stop();
        localToHeadset.Stop();
        Console.WriteLine("Bridge stopped.");
    }
}
