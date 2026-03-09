using System;
using System.IO;
using Avalonia;

namespace HumTrack.App;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Capture all unhandled exceptions (including XAML parse failures)
        // and write them to a crash file so we can diagnose silent failures
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var msg = e.ExceptionObject?.ToString() ?? "Unknown error";
            var path = Path.Combine(AppContext.BaseDirectory, "humtrack-crash.log");
            File.AppendAllText(path, $"[{DateTime.Now:u}] UNHANDLED:\n{msg}\n\n");
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "humtrack-crash.log");
            File.AppendAllText(path, $"[{DateTime.Now:u}] STARTUP CRASH:\n{ex}\n\n");
            // Also write to stderr so terminal runs can see it
            Console.Error.WriteLine($"HumTrack startup crash: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
