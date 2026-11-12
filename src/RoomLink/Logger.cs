using System;
using System.IO;

namespace RoomLink;

/// <summary>
/// Tiny file logger: %APPDATA%\RoomLink\RoomLink.log
/// Never throws — logging must not crash the app.
/// </summary>
internal static class Logger
{
    private static readonly object Gate = new();

    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RoomLink", "RoomLink.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                var dir = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                // Rotate by truncating when it grows past ~1 MB.
                if (File.Exists(Path) && new FileInfo(Path).Length > 1_000_000)
                    File.WriteAllText(Path, $"--- log truncated ---\n");

                File.AppendAllText(Path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
            }
        }
        catch
        {
            // ignore
        }
    }
}
