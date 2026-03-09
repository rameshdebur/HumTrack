using System;
using System.IO;
using Avalonia;

namespace HumTrack.App;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Write crash to a log file next to the exe so we can diagnose launch failures
            var logPath = Path.Combine(
                AppContext.BaseDirectory, "humtrack-crash.log");
            File.WriteAllText(logPath,
                $"[{DateTime.Now:u}] Fatal startup crash:\n{ex}\n");
            throw; // Re-throw so the OS shows the standard crash dialog
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
