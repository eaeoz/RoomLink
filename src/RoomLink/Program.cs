using System;
using System.Linq;
using System.Windows.Forms;

namespace RoomLink;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Console test harness still available for development:
        //   RoomLink.exe --cli
        if (args.Any(a => a.Equals("--cli", StringComparison.OrdinalIgnoreCase)))
        {
            return ConsoleHarness.Run();
        }

        // Machine-readable device dump for diagnostics:
        //   RoomLink.exe --devices
        if (args.Any(a => a.Equals("--devices", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var lines = new List<string>();
                foreach (var d in DeviceService.GetCaptureDevices())
                    lines.Add($"in|{d.Name}|{d.Id}");
                foreach (var d in DeviceService.GetRenderDevices())
                    lines.Add($"out|{d.Name}|{d.Id}");
                System.IO.File.WriteAllLines("RoomLinkDevices.txt", lines);
                Console.WriteLine($"Wrote {lines.Count} devices to RoomLinkDevices.txt");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex}");
                return 1;
            }
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
        return 0;
    }
}
