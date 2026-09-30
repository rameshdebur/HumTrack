using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Desktop;

internal static class WorkspaceTests
{
    internal static int Run(string testRoot)
    {
        if (!Path.IsPathFullyQualified(testRoot) || Directory.Exists(testRoot))
        {
            return 2;
        }

        Directory.CreateDirectory(testRoot);
        int passed = 0;
        void Check(bool value, string name) { if (!value) { throw new InvalidOperationException(name); } Console.WriteLine("PASS " + name); passed++; }
        static void Refused(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new InvalidOperationException("Expected refusal."); }
        try
        {
            var root = Path.Combine(testRoot, "simulation"); var session = Guid.NewGuid(); var attempt = Guid.NewGuid();
            Response Send(string command) => Workspace.Execute(root, new Request(command, session, attempt));
            Workspace.Execute(root, new Request("create-session", session, Subject: new Subject("SYN-001", "Synthetic Person", "2000-01-01", "Not recorded", 170)));
            Workspace.Execute(root, new Request("prepare", session, attempt, Sources: ["SIM-A"]));
            Check(Send("list").Snapshot!.Attempts.Single().State == "Ready", "C16-01 single fixed-rate source ready");
            Send("start"); Send("start");
            Check(Send("list").Snapshot!.Events.Count(e => e.State == "Recording") == 1, "C16-04 duplicate start idempotent");
            Send("preview-loss"); Check(Send("list").Snapshot!.Attempts.Single().State == "Recording", "C16-05 preview independent");
            Send("stop"); Send("stop");
            Check(Send("list").Snapshot!.Events.Count(e => e.State == "Finalizing") == 1, "C16-04 duplicate stop idempotent");
            Send("finalize"); Check(Send("list").Snapshot!.Attempts.Single().Reason.Contains("NOT COMMITTED", StringComparison.Ordinal), "C16-09 no synthetic commit");
            Refused(() => Send("start")); Check(true, "C16-08 finalized attempt cannot restart");
            Refused(() => Workspace.Execute(root, new Request("prepare", session, Guid.NewGuid(), Sources: ["SIM-A"], Scenario: "unsupported-profile")));
            Check(true, "C16-03 unsupported profile refused");
            attempt = Guid.NewGuid(); Workspace.Execute(root, new Request("prepare", session, attempt, Sources: ["SIM-A"], Scenario: "finalization-failure"));
            Send("start"); Send("stop"); Send("finalize"); Check(Send("list").Snapshot!.Attempts[0].State == "FinalizationFailed", "C16-06 finalization failure retained");
            attempt = Guid.NewGuid(); Workspace.Execute(root, new Request("prepare", session, attempt, Sources: ["SIM-A"])); Send("start");
            var child = Program.Call(root, new Request("reconcile", session, attempt)).GetAwaiter().GetResult();
            Check(child.Success && child.Snapshot!.Attempts[0].State == "Interrupted", "C16-07 separate process restart reconciliation");
            Check(child.Snapshot!.Attempts.Length == 3, "C16-08 prior attempts preserved");
            var dual = Guid.NewGuid(); Workspace.Execute(root, new Request("create-session", dual, Subject: new Subject("SYN-001", "Synthetic Person", "2000-01-01", "Not recorded", 170), Protocol: "dual-v1"));
            Refused(() => Workspace.Execute(root, new Request("prepare", dual, Guid.NewGuid(), Sources: ["SIM-A", "SIM-A"])));
            Check(true, "C16-02 duplicate role source refused");
            var pair = Guid.NewGuid();
            Workspace.Execute(root, new Request("prepare", dual, pair, Sources: ["SIM-A", "SIM-B"], Scenario: "source-loss"));
            Workspace.Execute(root, new Request("start", dual, pair));
            var loss = Workspace.Execute(root, new Request("source-loss", dual, pair));
            Check(loss.Snapshot!.Attempts[0].State == "Recording" && loss.Snapshot.Attempts[0].FailedSource == "SIM-A", "C16-06 survivor continues after first source loss");
            Workspace.Execute(root, new Request("stop", dual, pair));
            var incomplete = Workspace.Execute(root, new Request("finalize", dual, pair));
            Check(incomplete.Snapshot!.Attempts[0].State == "Interrupted", "C16-06 source loss cannot finalize complete");
            foreach (string fault in new[] { "verification-failure", "storage-failure" })
            {
                attempt = Guid.NewGuid(); Workspace.Execute(root, new Request("prepare", session, attempt, Sources: ["SIM-A"], Scenario: fault));
                Send("start"); Send("stop"); Send("finalize");
                var failure = Send("simulate-processing");
                Check(failure.Snapshot!.Attempts[0].Processing.EndsWith("Failed", StringComparison.Ordinal), "C16-06 simulated " + fault + " retained");
                var retry = Send("simulate-processing");
                Check(retry.Snapshot!.Attempts[0].Id == attempt && retry.Snapshot.Attempts[0].Processing == "SimulatedOnly"
                    && Array.Exists(retry.Snapshot.Events, e => e.Reason.Contains("Failed", StringComparison.Ordinal)), "C16-08 same-attempt processing retry retains failure " + fault);
            }
            Refused(() => Workspace.Execute(root, new Request("list", SchemaVersion: "2.0.0")));
            Check(true, "C16-10 version refused");
            Refused(() => Workspace.Execute(root, new Request("create-session", session, Subject: new Subject("OTHER", "Synthetic Other", "2000-01-01", "Not recorded", 170))));
            Check(true, "C16-01 identity immutable");
            var repository = Path.Combine(testRoot, "repository"); new RepositoryService().Initialize(repository, Environment.UserName);
            Refused(() => Workspace.Execute(repository, new Request("list"))); Check(true, "C16-10 repository isolation");
            var descriptor = File.ReadAllBytes(Path.Combine(repository, "repository.json"));
            var inspection = Program.Call(repository, new Request("inspect-repository")).GetAwaiter().GetResult();
            Check(inspection.Success && inspection.Message.Contains("HISTORICAL", StringComparison.Ordinal)
                && descriptor.SequenceEqual(File.ReadAllBytes(Path.Combine(repository, "repository.json"))), "C16-11 actual repository historical inspection");
            Console.WriteLine($"C16 software assertions: {passed} passed. No HIL/native accessibility acceptance."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
