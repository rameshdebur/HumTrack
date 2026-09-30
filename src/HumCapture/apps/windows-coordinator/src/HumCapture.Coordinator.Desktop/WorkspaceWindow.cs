using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace HumCapture.Coordinator.Desktop;

internal sealed class WorkspaceWindow : Window
{
    private readonly string root;
    private readonly StackPanel body = new() { Spacing = 10 };
    private readonly TextBlock context = Text("No session selected");
    private readonly TextBlock status = Text("Loading saved simulation workspace…");
    private readonly TextBlock elapsed = Text("");
    private readonly StackPanel navigation = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private Snapshot snapshot = new([], [], [], 0);
    private Session? selected;
    private Attempt? attempt;
    private bool busy;
    private bool available;
    private bool allowClose;
    private int screen;
    private int page;
    private string repositoryRoot = "";
    private Guid? repositoryCursor;
    private Subject draft = new("SYN-001", "Synthetic Example", "2000-01-01", "Not recorded", 170);

    internal WorkspaceWindow(string workspaceRoot)
    {
        root = workspaceRoot;
        Title = "HumCapture — Simulation workspace"; Width = 1120; Height = 830; MinWidth = 760; MinHeight = 540;
        var panel = new DockPanel { Margin = new Thickness(24) };
        var header = new StackPanel { Spacing = 10, Margin = new Thickness(0, 0, 0, 18) };
        header.Children.Add(new TextBlock { Text = "HumCapture", FontSize = 26, FontWeight = FontWeight.SemiBold });
        header.Children.Add(new Border { Background = Brushes.DarkSlateBlue, Padding = new Thickness(12), Child = new TextBlock { Text = "SIMULATION — no physical camera capture • synthetic subjects only", Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap } });
        header.Children.Add(context); header.Children.Add(navigation); header.Children.Add(status);
        DockPanel.SetDock(header, Dock.Top); panel.Children.Add(header);
        panel.Children.Add(new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Content = panel;
        Opened += async (_, _) => await Run(new Request("reconcile"));
        Closing += async (_, e) =>
        {
            if (allowClose)
            {
                return;
            }

            if (busy) { e.Cancel = true; status.Text = "Wait for the current operation before closing."; return; }
            if (!Workspace.Active(attempt))
            {
                return;
            }

            e.Cancel = true;
            var dialog = new Window { Title = "Capture is active", Width = 480, Height = 210, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var choices = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
            choices.Children.Add(Text("Stop and finalize this simulated attempt before closing? No files will be deleted."));
            choices.Children.Add(Button("Keep workspace open", () => { dialog.Close(false); return Task.CompletedTask; }));
            choices.Children.Add(Button("Stop, finalize and close", () => { dialog.Close(true); return Task.CompletedTask; })); dialog.Content = choices;
            if (await dialog.ShowDialog<bool>(this)) { await Stop(); if (!Workspace.Active(attempt) && available) { allowClose = true; Close(); } }
        };
        Closed += (_, _) => timer.Stop();
        timer.Tick += (_, _) =>
        {
            if (attempt?.Started is { } start)
            {
                var duration = (attempt.Ended ?? DateTimeOffset.UtcNow) - start;
                elapsed.Text = $"Simulated elapsed time: {Math.Max(0, duration.TotalSeconds):F1} s • Activity is generated, not measured camera evidence";
            }
        };
        timer.Start(); Render();
    }
    private async Task Run(Request request)
    {
        if (busy)
        {
            return;
        }

        busy = true; status.Text = "Working… status pending"; body.IsEnabled = false; navigation.IsEnabled = false;
        try
        {
            var response = await Program.Call(root, request);
            available = response.Success;
            status.Text = response.Message;
            if (response.Snapshot is { } value)
            {
                snapshot = value;
                if (request.Command == "reconcile" && value.Sessions.Length > 0) { screen = 4; }
                selected = Array.Find(value.Sessions, s => s.Id == request.SessionId) ?? selected;
                attempt = Array.Find(value.Attempts, a => a.Id == request.AttemptId) ?? value.Attempts.FirstOrDefault();
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Text.Json.JsonException or OperationCanceledException)
        { available = false; status.Text = "Status unavailable. Refresh to reconcile before acting. " + ex.Message; }
        finally { busy = false; body.IsEnabled = true; navigation.IsEnabled = true; Render(); }
    }
    private Request Action(string command) => new(command, selected?.Id ?? Guid.Empty, attempt?.Id ?? Guid.Empty, Page: page);
    private async Task Stop()
    {
        await Run(Action("stop"));
        if (available && attempt?.State == "Finalizing")
        {
            await Run(Action("finalize"));
        }

        screen = 3; Render();
    }
    private void Render()
    {
        context.Text = selected is null ? "Signed-in operator: " + Environment.UserName : $"{selected.Subject.Name} • {selected.Subject.Code} • Subject {selected.SubjectId}\nSession {selected.Id} • Protocol {selected.Protocol} • Attempt {attempt?.Id.ToString() ?? "not prepared"}";
        navigation.Children.Clear();
        string[] labels = ["1  Session", "2  Cameras", "3  Capture", "4  Results", "History / repository"];
        for (int i = 0; i < labels.Length; i++)
        {
            int target = i;
            var button = Button(labels[i], () => { screen = target; Render(); return Task.CompletedTask; });
            button.IsEnabled = !Workspace.Active(attempt) && (i is 0 or 4 || selected is not null) || target == screen;
            navigation.Children.Add(button);
        }
        body.Children.Clear();
        if (!available)
        {
            body.Children.Add(Text("Status unavailable — saved state must be refreshed before capture actions."));
            body.Children.Add(Button("Refresh saved state", () => Run(Action("list"))));
            return;
        }
        switch (screen) { case 0: SessionScreen(); break; case 1: CamerasScreen(); break; case 2: CaptureScreen(); break; case 3: ResultsScreen(); break; default: HistoryScreen(); break; }
    }
    private void SessionScreen()
    {
        Heading("Session", "Use fictional details only. This workspace is separate from capture repositories.");
        var location = Text("Simulation data location: " + Path.GetFileName(root) + " (hover for full path)");
        ToolTip.SetTip(location, root); body.Children.Add(location);
        if (selected is not null)
        {
            body.Children.Add(Text("Saved session selected. Subject and protocol are fixed for this session."));
            body.Children.Add(Button("Continue to Cameras", () => { screen = 1; Render(); return Task.CompletedTask; }));
            body.Children.Add(Button("Create a different session", () => { selected = null; attempt = null; Render(); return Task.CompletedTask; }));
            return;
        }
        var code = Field("Synthetic subject code", draft.Code); var name = Field("Synthetic full name", draft.Name);
        var birth = Field("Synthetic date of birth (YYYY-MM-DD)", draft.BirthDate);
        var sex = Choice("Sex", ["Female", "Male", "Other", "Not recorded"], draft.Sex);
        var height = Field("Height (cm)", draft.HeightCm.ToString(CultureInfo.InvariantCulture));
        var protocol = Choice("Existing simulation protocol", ["single-v1", "dual-v1"], "single-v1");
        body.Children.Add(Text("single-v1: one Front role • dual-v1: Front and Side required. Both fixtures request 1280×720, fixed 30 fps. Not approved physical-capture protocols."));
        var validation = Text(""); body.Children.Add(validation);
        var save = Button("Save session and continue to Cameras", async () =>
        {
            if (!decimal.TryParse(height.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var cm)) { validation.Text = "Height: enter a number in cm."; return; }
            draft = new Subject(code.Text?.Trim() ?? "", name.Text?.Trim() ?? "", birth.Text ?? "", sex.SelectedItem?.ToString() ?? "", cm);
            try { Workspace.ValidateSubject(draft); } catch (InvalidDataException ex) { validation.Text = ex.Message; return; }
            var id = Guid.NewGuid(); await Run(new Request("create-session", id, Subject: draft, Protocol: protocol.SelectedItem?.ToString() ?? ""));
            if (available) { screen = 1; Render(); }
        }); body.Children.Add(save);
        void ValidateFields()
        {
            try
            {
                if (!decimal.TryParse(height.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var cm)) { throw new InvalidDataException("Height: enter a number in cm."); }
                Workspace.ValidateSubject(new Subject(code.Text?.Trim() ?? "", name.Text?.Trim() ?? "", birth.Text ?? "", sex.SelectedItem?.ToString() ?? "", cm));
                save.IsEnabled = true; validation.Text = "";
            }
            catch (InvalidDataException ex) { save.IsEnabled = false; validation.Text = ex.Message; }
        }
        code.TextChanged += (_, _) => ValidateFields(); name.TextChanged += (_, _) => ValidateFields();
        birth.TextChanged += (_, _) => ValidateFields(); height.TextChanged += (_, _) => ValidateFields(); sex.SelectionChanged += (_, _) => ValidateFields(); ValidateFields();
        foreach (var existing in snapshot.Sessions.GroupBy(s => s.SubjectId).Select(g => g.First().Subject).Take(5))
        {
            body.Children.Add(Button("Use existing synthetic subject: " + existing.Code + " / " + existing.Name, () => { draft = existing; Render(); return Task.CompletedTask; }));
        }
    }
    private void CamerasScreen()
    {
        Heading("Cameras", "Configuration readiness only — not measured cadence or camera qualification.");
        body.Children.Add(Text("Requested: 1280×720 • fixed 30 fps. SIM-A and SIM-B support this fixture profile. IMU/lens metadata: unavailable, optional for these protocols."));
        var front = Choice("Front — required", ["Not assigned", "SIM-A", "SIM-B"], "SIM-A");
        ComboBox? side = selected?.Protocol == "dual-v1" ? Choice("Side — required", ["Not assigned", "SIM-A", "SIM-B"], "SIM-B") : null;
        var scenario = Choice("Simulation test scenario (not a camera setting)", Workspace.Scenarios, "normal");
        var readiness = Text("Ready for configuration check. Physical discovery is unavailable."); body.Children.Add(readiness);
        var prepare = Button("Prepare new attempt and continue to Capture", async () =>
        {
            string[] sources = side is null ? [front.SelectedItem?.ToString() ?? ""] : [front.SelectedItem?.ToString() ?? "", side.SelectedItem?.ToString() ?? ""];
            await Run(new Request("prepare", selected!.Id, Guid.NewGuid(), Sources: sources, Scenario: scenario.SelectedItem?.ToString() ?? ""));
            if (available) { screen = 2; Render(); }
        });
        void Check()
        {
            bool assigned = front.SelectedItem?.ToString() is "SIM-A" or "SIM-B" && (side is null || side.SelectedItem?.ToString() is "SIM-A" or "SIM-B");
            bool unique = side is null || !Equals(front.SelectedItem, side.SelectedItem);
            bool compatible = scenario.SelectedItem?.ToString() is not ("source-unavailable" or "unsupported-profile");
            prepare.IsEnabled = assigned && unique && compatible;
            if (!assigned) { readiness.Text = "Not assigned — choose each required source."; }
            else if (!unique) { readiness.Text = "Needs attention — each role needs a distinct source."; }
            else if (!compatible) { readiness.Text = "Needs attention — source unavailable or profile unsupported. Change scenario; no silent profile substitution."; }
            else { readiness.Text = "Ready — synthetic configuration checks pass; measured cadence not assessed."; }
        }
        front.SelectionChanged += (_, _) => Check(); if (side is not null)
        {
            side.SelectionChanged += (_, _) => Check();
        }

        scenario.SelectionChanged += (_, _) => Check(); Check();
        body.Children.Add(prepare);
        body.Children.Add(Text("Real-camera discovery and capture: unavailable in this batch."));
    }
    private void CaptureScreen()
    {
        Heading("Capture", "The simulation creates activity records, not video files.");
        if (attempt is null) { body.Children.Add(Text("Prepare an attempt on Cameras first.")); return; }
        body.Children.Add(Text("State: " + attempt.State + "\n" + attempt.Reason));
        if (Workspace.Active(attempt)) { body.Children.Add(Text("Stop this simulated capture before changing screens. Closing will ask whether to stop and finalize.")); }
        foreach (var source in attempt.Sources)
        {
            string sourceStatus = source == attempt.FailedSource ? "Source lost — incomplete" : attempt.State;
            body.Children.Add(new Border { BorderBrush = Brushes.SlateGray, BorderThickness = new Thickness(1), Padding = new Thickness(20), Child = Text(source + " • " + sourceStatus + " • SIMULATED PREVIEW\n" + attempt.Preview + "\nNo live image or sensor timestamps") });
        }

        body.Children.Add(elapsed);
        var start = Button("Start simulated capture", () => Run(Action("start"))); start.IsEnabled = attempt.State == "Ready"; body.Children.Add(start);
        var stop = Button("Stop capture", Stop); stop.IsEnabled = attempt.State is "Recording" or "Finalizing"; body.Children.Add(stop);
        if (attempt.State == "Recording" && attempt.Scenario == "preview-loss")
        {
            body.Children.Add(Button("Inject simulated preview loss", () => Run(Action("preview-loss"))));
        }

        if (attempt.State == "Recording" && attempt.Scenario == "source-loss")
        {
            body.Children.Add(Button("Inject simulated source loss", () => Run(Action("source-loss"))));
        }

        if (!Workspace.Active(attempt))
        {
            body.Children.Add(Button("View Results", () => { screen = 3; Render(); return Task.CompletedTask; }));
        }
    }
    private void ResultsScreen()
    {
        Heading("Results and recovery", "Stored state and scientific acceptance are separate.");
        if (attempt is null) { body.Children.Add(Text("No attempt selected. Open History or prepare one on Cameras.")); return; }
        body.Children.Add(Text("Attempt: " + attempt.Id + "\nSynthetic recording/finalization: " + attempt.State + "\n" + attempt.Reason));
        body.Children.Add(Text("Collection: unavailable — no media package generated\nVerification: NOT ASSESSED\nRepository commit: NOT COMMITTED\nProtocol outcome: NOT ASSESSED\nWorkflow: NOT COMPLETE"));
        foreach (var source in attempt.Sources)
        {
            body.Children.Add(Text(source + " — synthetic activity only; recording and metadata evidence unavailable"));
        }

        if (attempt.State == "Finalizing")
        {
            body.Children.Add(Button("Finish pending synthetic finalization", () => Run(Action("finalize"))));
        }

        body.Children.Add(Text("Simulation-only processing exercise: " + attempt.Processing + " • processing attempts: " + attempt.ProcessingAttempts));
        if (attempt.State == "Finalized" && attempt.Processing != "SimulatedOnly")
        {
            string label = attempt.ProcessingAttempts == 0 ? "Exercise simulated verification / storage" : "Retry simulated processing — same recording attempt";
            body.Children.Add(Button(label, () => Run(Action("simulate-processing"))));
        }
        body.Children.Add(Text("Actual verification/storage is unavailable for simulated activity. The processing exercise writes history only, never a verified package. Real capture-to-package integration remains separate work. No source deletion is performed."));
        body.Children.Add(Button("Prepare a new attempt", () => { screen = 1; Render(); return Task.CompletedTask; }));
        body.Children.Add(Button("Refresh attempt evidence", () => Run(Action("list"))));
        body.Children.Add(Button("Open simulation data folder", () =>
        {
            var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false }; start.ArgumentList.Add(root);
            using var launched = Process.Start(start); return Task.CompletedTask;
        }));
        foreach (var item in snapshot.Events)
        {
            body.Children.Add(Text($"#{item.Sequence} • {item.State} • {item.Reason}"));
        }
    }
    private void HistoryScreen()
    {
        Heading("History / repository", "Simulation history is separate from existing repository evidence.");
        body.Children.Add(Text("20 sessions per page; latest 20 attempts for the selected session. Results show latest 50 events; older evidence remains retained."));
        body.Children.Add(Button("Refresh simulation history", () => Run(Action("list"))));
        foreach (var item in snapshot.Sessions)
        {
            body.Children.Add(Button($"Resume {item.Subject.Code} • {item.Protocol} • {item.Id}", async () => { selected = item; attempt = null; await Run(new Request("list", item.Id, Page: page)); screen = 3; Render(); }));
        }

        body.Children.Add(Button("Previous session page", async () => { page = Math.Max(0, page - 1); await Run(Action("list")); }));
        body.Children.Add(Button("Next session page", async () => { page++; await Run(Action("list")); }));
        foreach (var item in snapshot.Attempts)
        {
            body.Children.Add(Button($"Attempt {item.Id} • {item.State}", async () => { attempt = item; await Run(Action("list")); screen = 3; Render(); }));
        }

        body.Children.Add(Text("Existing repository — READ-ONLY historical inspection. Does not verify bytes, perform recovery or mark this simulation complete."));
        var path = Field("Existing repository absolute path", repositoryRoot);
        var report = Text("No repository inspected.");
        async Task Inspect(bool next)
        {
            if (busy)
            {
                return;
            }

            if (repositoryRoot != path.Text)
            {
                repositoryCursor = null;
            }

            repositoryRoot = path.Text ?? ""; busy = true; report.Text = "Inspecting…";
            try
            {
                var result = await Program.Call(repositoryRoot, new Request("inspect-repository", AttemptId: next ? repositoryCursor ?? Guid.Empty : Guid.Empty));
                report.Text = result.Message; repositoryCursor = result.Success ? result.Cursor : null;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Text.Json.JsonException or OperationCanceledException) { report.Text = "Status unavailable. " + ex.Message; repositoryCursor = null; }
            finally { busy = false; }
        }
        body.Children.Add(Button("Inspect existing repository", () => Inspect(false)));
        body.Children.Add(Button("Next repository page", () => Inspect(true))); body.Children.Add(report);
    }
    private void Heading(string title, string description) { body.Children.Add(new TextBlock { Text = title, FontSize = 23, FontWeight = FontWeight.SemiBold }); body.Children.Add(Text(description)); }
    private TextBox Field(string label, string value)
    {
        var control = new TextBox { Text = value, Width = 560, HorizontalAlignment = HorizontalAlignment.Left };
        var group = new StackPanel { Spacing = 3 };
        group.Children.Add(new Label { Content = label, Target = control }); group.Children.Add(control); body.Children.Add(group); return control;
    }
    private ComboBox Choice(string label, string[] items, string value)
    {
        var control = new ComboBox { ItemsSource = items, SelectedItem = value, MinWidth = 300 };
        var group = new StackPanel { Spacing = 3 };
        group.Children.Add(new Label { Content = label, Target = control }); group.Children.Add(control); body.Children.Add(group); return control;
    }
    private static TextBlock Text(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap };
    private static Button Button(string text, Func<Task> action)
    {
        var control = new Button { Content = text, Padding = new Thickness(14, 9), MinHeight = 40 };
        control.Click += async (_, _) => await action(); return control;
    }
}
