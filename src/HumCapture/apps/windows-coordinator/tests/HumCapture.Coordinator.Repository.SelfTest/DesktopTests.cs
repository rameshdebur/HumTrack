using System.Diagnostics;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class DesktopTests
{
    internal static void Service() => Run("--self-test");
    internal static void Screens() => Run("--ui-test");
    private static void Run(string mode)
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory);
        DirectoryInfo? root = output;
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "project", "PROJECT_STATE.md"))) { root = root.Parent; }
        if (root is null) { throw new InvalidOperationException("HumCapture root unavailable."); }
        var assembly = Path.Combine(root.FullName, "apps", "windows-coordinator", "src", "HumCapture.Coordinator.Desktop", "bin", output.Parent!.Name, output.Name, "HumCapture.Coordinator.Desktop.dll");
        var evidence = Path.Combine(root.FullName, "evidence-vault", "c16-ci-" + Guid.NewGuid().ToString("N"));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(assembly); start.ArgumentList.Add(mode); start.ArgumentList.Add(evidence);
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Desktop test unavailable.");
        try
        {
            var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
            if (!child.WaitForExit(55000)) { throw new TimeoutException("Desktop test timed out."); }
            Console.Write(stdout.GetAwaiter().GetResult());
            if (child.ExitCode != 0) { throw new InvalidOperationException(stderr.GetAwaiter().GetResult()); }
        }
        finally { if (!child.HasExited) { child.Kill(true); child.WaitForExit(); } }
    }
}
