using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace HumCapture.Coordinator.Desktop;

internal static class WorkspaceUiTests
{
    internal static int Run(string output)
    {
        if (!Path.IsPathFullyQualified(output) || Directory.Exists(output)) { return 2; }
        Directory.CreateDirectory(output);
        try
        {
            AppBuilder.Configure<DesktopApplication>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
            var window = new WorkspaceWindow(Path.Combine(output, "workspace")); window.Show();
            static bool Has(Window w, string value) => w.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains(value, StringComparison.Ordinal) == true);
            static void Pump(Func<bool> done)
            {
                var timeout = DateTime.UtcNow.AddSeconds(15);
                while (!done()) { Dispatcher.UIThread.RunJobs(); if (DateTime.UtcNow > timeout) { throw new TimeoutException("UI state timeout."); } Thread.Sleep(10); }
                Dispatcher.UIThread.RunJobs();
            }
            static Button Find(Window w, string text) => w.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, text));
            static void Click(Window w, string text)
            {
                var button = Find(w, text); if (!button.IsEffectivelyEnabled) { throw new InvalidOperationException("Disabled: " + text); }
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
            }
            void Save(string name) { using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered frame."); frame.Save(Path.Combine(output, name + ".png")); }
            Pump(() => Has(window, "Synthetic subject code")); Save("01-session");
            var input = window.GetVisualDescendants().OfType<TextBox>().First();
            if (!input.Focus()) { throw new InvalidOperationException("Keyboard focus unavailable."); }
            window.KeyTextInput("-UI");
            string originalCode = input.Text!; input.Text = "";
            Dispatcher.UIThread.RunJobs();
            if (Find(window, "Save session and continue to Cameras").IsEnabled) { throw new InvalidOperationException("Invalid form was enabled."); }
            input.Text = originalCode;
            Dispatcher.UIThread.RunJobs();
            Click(window, "Save session and continue to Cameras"); Pump(() => Has(window, "Front — required")); Save("02-cameras");
            Click(window, "Prepare new attempt and continue to Capture"); Pump(() => Has(window, "State: Ready")); Save("03-ready");
            Click(window, "Start simulated capture"); Pump(() => Has(window, "State: Recording"));
            if (Find(window, "1  Session").IsEnabled) { throw new InvalidOperationException("Active navigation was not gated."); }
            window.Close(); Pump(() => window.OwnedWindows.Count > 0);
            Click(window.OwnedWindows[0], "Keep workspace open");
            if (!window.IsVisible || !Has(window, "State: Recording")) { throw new InvalidOperationException("Close cancellation changed recording."); }
            Save("04-recording"); Click(window, "Stop capture"); Pump(() => Has(window, "Workflow: NOT COMPLETE")); Save("05-results");
            if (!Has(window, "NOT COMMITTED") || !Has(window, "SIMULATION")) { throw new InvalidOperationException("Missing evidence boundary labels."); }
            window.Width = 760; window.Height = 540; Dispatcher.UIThread.RunJobs(); Save("06-small-window");
            window.Close(); Console.WriteLine("PASS C16-12 headless actual UI: four-screen service flow, keyboard input/focus, invalid-form gate, active navigation and close-confirmation guards, simulation/result labels, 1120x830 and 760x540 renders. Native accessibility remains unverified."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
