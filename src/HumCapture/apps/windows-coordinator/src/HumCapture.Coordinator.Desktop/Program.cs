using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Desktop;

internal static class Program
{
    internal static string Root { get; private set; } = "";
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            DirectoryInfo? sourceRoot = new(AppContext.BaseDirectory);
            while (sourceRoot is not null && !File.Exists(Path.Combine(sourceRoot.FullName, "docs", "project", "PROJECT_STATE.md"))) { sourceRoot = sourceRoot.Parent; }
            if (sourceRoot is null) { return 2; }
            args = ["--workspace", Path.Combine(sourceRoot.FullName, "evidence-vault", "coordinator-ui-simulation")];
        }
        if (args.Length == 2 && args[0] == "--ui-test") { return WorkspaceUiTests.Run(args[1]); }
        if (args.Length == 2 && args[0] == "--self-test")
        {
            return WorkspaceTests.Run(args[1]);
        }

        if (args.Length == 2 && args[0] == "--service")
        {
            return Serve(args[1]);
        }

        if (args.Length != 2 || args[0] != "--workspace")
        {
            return 2;
        }

        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1]));
        Workspace.ValidateRoot(Root);
        using var windowGuard = new Mutex(false, "HumCapture.Simulation.UI." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Root.ToUpperInvariant()))));
        bool held;
        try { held = windowGuard.WaitOne(0); } catch (AbandonedMutexException) { held = true; }
        if (!held)
        {
            return 7;
        }

        try { return AppBuilder.Configure<DesktopApplication>().UseWin32().UseSkia().StartWithClassicDesktopLifetime(args); }
        finally { windowGuard.ReleaseMutex(); }
    }
    private static int Serve(string root)
    {
        try
        {
            var text = Console.In.ReadToEnd();
            if (text.Length > 65536)
            {
                throw new InvalidDataException("Request exceeds limit.");
            }

            var request = JsonSerializer.Deserialize<Request>(text, Workspace.Json) ?? throw new InvalidDataException("Missing request.");
            Response response;
            if (request.Command == "inspect-repository")
            {
                if (request.SchemaVersion != "1.0.0")
                {
                    throw new InvalidDataException("Unsupported request.");
                }

                var service = new RepositoryService();
                var opened = service.Open(root);
                Guid? cursor = request.AttemptId == Guid.Empty ? null : request.AttemptId;
                var candidates = opened.CanMutate ? service.ListStartupTransactions(root, cursor, 20) : [];
                response = new Response(true, "HISTORICAL REPOSITORY INSPECTION — not fresh verification or session completion.\nRepository: "
                    + opened.Descriptor.RepositoryId + "\n" + opened.Explanation + "\n"
                    + string.Join("\n", candidates.Select(c => $"{c.TransactionId} | stored {c.State} | revision {c.Revision}"))
                    + "\nNo files were reverified and no recovery/commit was performed.", Cursor: candidates.LastOrDefault()?.TransactionId);
            }
            else
            {
                response = Workspace.Execute(root, request);
            }

            Console.WriteLine(JsonSerializer.Serialize(response, Workspace.Json)); return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException
            or JsonException or Microsoft.Data.Sqlite.SqliteException or RepositoryException)
        {
            Console.WriteLine(JsonSerializer.Serialize(new Response(false, ex.Message), Workspace.Json)); return 3;
        }
    }
    internal static async Task<Response> Call(string root, Request request)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate service executable.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        }

        start.ArgumentList.Add("--service"); start.ArgumentList.Add(root);
        using var child = Process.Start(start) ?? throw new IOException("Unable to start workspace service.");
        try
        {
            await child.StandardInput.WriteAsync(JsonSerializer.Serialize(request, Workspace.Json)).ConfigureAwait(false); child.StandardInput.Close();
            var output = child.StandardOutput.ReadToEndAsync(); var error = child.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await child.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var raw = await output.ConfigureAwait(false); _ = await error.ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<Response>(raw, Workspace.Json) ?? throw new IOException("Service status unavailable.");
            if (result.SchemaVersion != "1.0.0" || (child.ExitCode != 0 && result.Success))
            {
                throw new IOException("Service response refused.");
            }

            return result;
        }
        finally { if (!child.HasExited) { child.Kill(true); await child.WaitForExitAsync().ConfigureAwait(false); } }
    }
}

internal sealed class DesktopApplication : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new WorkspaceWindow(Program.Root);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
