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

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
        return 0;
    }
}
