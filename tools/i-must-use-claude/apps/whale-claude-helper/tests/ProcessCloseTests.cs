using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WhaleClaudeHelper;

#if PROCESS_CLOSE_FIXTURE
namespace WhaleClaudeHelper {
    // This fixture-only UI adapter never loads/saves user preferences or starts the app.
    internal static class Ui {
        internal static bool English;
        internal static string T(string zh, string en) { return English ? en : zh; }
    }
}
#endif

internal static class ProcessCloseTests {
    private static int checks;
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
    private static ProcessCloseTarget P(int pid, string name = "chrome.exe", long started = 100, string owner = "FIXTURE-USER", int session = 7) { return new ProcessCloseTarget(pid, started, name, owner, session); }

    private sealed class FakePlatform : IProcessClosePlatform {
        internal readonly Dictionary<int, ProcessCloseTarget> Records = new Dictionary<int, ProcessCloseTarget>();
        internal readonly List<int> NormalCalls = new List<int>(), ForceCalls = new List<int>();
        internal readonly HashSet<int> ExitNormally = new HashSet<int>(), NoWindow = new HashSet<int>(), ReadFailures = new HashSet<int>(), NormalFailures = new HashSet<int>(), ForceFailures = new HashSet<int>();
        internal Action<ProcessCloseTarget> AfterNormal, AfterForce, BeforeAction;
        internal Action AfterDelay;
        internal bool EnumerationFails, EnumerationIncomplete, EnumerationCancels;
        internal int Enumerations, Reads, Delays;
        public int CurrentProcessId { get { return 42; } }
        public int CurrentSessionId { get { return 7; } }
        public string CurrentUserSid { get { return "FIXTURE-USER"; } }
        internal FakePlatform(params ProcessCloseTarget[] records) { foreach (var record in records) Records[record.Pid] = record; }
        public ProcessCloseEnumeration Enumerate(CancellationToken token) {
            token.ThrowIfCancellationRequested(); Enumerations++;
            if (EnumerationCancels) throw new OperationCanceledException();
            if (EnumerationFails) throw new InvalidOperationException("PRIVATE_DATA_DO_NOT_LOG");
            var result = new ProcessCloseEnumeration(); result.Processes.AddRange(Records.Values);
            if (EnumerationIncomplete) result.Errors.Add("PRIVATE_PATH_DO_NOT_LOG");
            return result;
        }
        public ProcessCloseTarget Read(int pid, CancellationToken token) {
            token.ThrowIfCancellationRequested(); Reads++;
            if (ReadFailures.Contains(pid)) throw new InvalidOperationException("PRIVATE_DATA_DO_NOT_LOG");
            ProcessCloseTarget value; return Records.TryGetValue(pid, out value) ? value : null;
        }
        public string RequestClose(ProcessCloseTarget expected, CancellationToken token) { return Act(expected, false, token); }
        public string ForceClose(ProcessCloseTarget expected, CancellationToken token) { return Act(expected, true, token); }
        private string Act(ProcessCloseTarget expected, bool force, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            if (BeforeAction != null) BeforeAction(expected);
            ProcessCloseTarget current;
            if (!Records.TryGetValue(expected.Pid, out current)) return "exited";
            if (!ProcessCloseService.SameIdentity(current, expected)) return "identity_changed";
            if (current.OwnerSid != CurrentUserSid || current.SessionId != CurrentSessionId || !ProcessCloseService.IsAllowedName(current.Name) || current.Pid == CurrentProcessId) return "not_allowed_or_owned";
            if (force) {
                ForceCalls.Add(expected.Pid);
                if (ForceFailures.Contains(expected.Pid)) return "action_failed";
                Records.Remove(expected.Pid);
                if (AfterForce != null) AfterForce(expected);
            } else {
                NormalCalls.Add(expected.Pid);
                if (NormalFailures.Contains(expected.Pid)) return "access_denied";
                if (ExitNormally.Contains(expected.Pid)) Records.Remove(expected.Pid);
                if (AfterNormal != null) AfterNormal(expected);
                if (NoWindow.Contains(expected.Pid)) return "no_window";
            }
            return "requested";
        }
        public Task DelayAsync(int milliseconds, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            Check(milliseconds == 250, "bounded wait uses fixed 250ms poll step");
            Delays++;
            if (AfterDelay != null) AfterDelay();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }
    }

    private static void SnapshotTests() {
        var fake = new FakePlatform(P(1, "Claude.exe"), P(2), P(3, "msedge.exe"), P(4, "node.exe"), P(5, "Codex.exe"), P(6, "ChatGPT.exe"), P(7, "FlClashCore.exe"), P(8, "powershell.exe"), P(9, "cmd.exe"), P(10, "chrome-helper.exe"), P(11, "chrome.exe", 100, "OTHER-USER"), P(12, "Claude.exe", 100, "FIXTURE-USER", 8), P(42), P(13, "chrome.exe", 0));
        var service = new ProcessCloseService(fake);
        var snapshot = service.Snapshot();
        Check(snapshot.Targets.Select(x => x.Pid).OrderBy(x => x).SequenceEqual(new[] { 1, 2, 3 }), "only exact allowed executable names owned by current user/session are offered");
        Check(snapshot.ExcludedCount == 4 && !snapshot.AllTargetsClosed, "other user/session, helper and unknown start time fail closed");
        Check(fake.NormalCalls.Count == 0 && fake.ForceCalls.Count == 0, "snapshot never closes a process");
        Check(snapshot.Targets.All(x => !service.CanForce(x)), "opening or refreshing never unlocks force close");
        Check(snapshot.Targets.All(x => x.ToString().Contains("PID") && !x.ToString().Contains("FIXTURE-USER")), "display identity contains only exact name and PID");
        Check(!ProcessCloseService.IsAllowedName("chrome") && !ProcessCloseService.IsAllowedName("chrome.exe /arg") && ProcessCloseService.IsAllowedName("CLAUDE.EXE"), "allowlist requires exact executable basename case-insensitively");
        fake.Records.Clear();
        Check(service.Snapshot().AllTargetsClosed, "complete empty snapshot permits return");
        fake.EnumerationIncomplete = true;
        snapshot = service.Snapshot();
        Check(!snapshot.AllTargetsClosed && snapshot.Errors.SequenceEqual(new[] { "enumeration_incomplete" }), "incomplete enumeration does not claim all closed or expose platform text");
        fake.EnumerationFails = true;
        Check(service.Snapshot().Errors.SequenceEqual(new[] { "enumeration_failed" }), "enumeration exceptions become fixed error code");
        var many = new FakePlatform(Enumerable.Range(1000, 514).Select(i => P(i)).ToArray());
        var limited = new ProcessCloseService(many).Snapshot();
        Check(limited.Targets.Count == 512 && limited.Errors.Contains("too_many_processes"), "snapshot has bounded target count");
    }

    private static async Task GracefulAndForceTests() {
        var fake = new FakePlatform(P(1, "Claude.exe"), P(2), P(3, "msedge.exe"));
        fake.ExitNormally.UnionWith(new[] { 1, 2 });
        var service = new ProcessCloseService(fake);
        var selected = service.Snapshot().Targets.Where(x => x.Pid != 3).ToArray();
        ProcessCloseBatchResult result = await service.CloseGracefullyAsync(selected, CancellationToken.None);
        Check(result.RequestedCount == 2 && result.ExitedCount == 2 && result.Remaining.Count == 0, "normal requests verify selected process exit");
        Check(fake.NormalCalls.OrderBy(x => x).SequenceEqual(new[] { 1, 2 }) && fake.ForceCalls.Count == 0, "normal close never calls force");
        Check(fake.Records.ContainsKey(3) && service.Snapshot().Targets.Single().Pid == 3, "unselected process remains untouched and visible");
        Check(fake.Delays == 0, "already exited selections do not wait unnecessarily");

        fake = new FakePlatform(P(11), P(12, "msedge.exe"), P(13, "Claude.exe"));
        fake.NoWindow.Add(12);
        service = new ProcessCloseService(fake);
        selected = service.Snapshot().Targets.Where(x => x.Pid != 13).ToArray();
        result = await service.ForceCloseAsync(selected, true, CancellationToken.None);
        Check(fake.ForceCalls.Count == 0 && result.Issues.All(x => x.Code == "normal_close_required"), "force cannot bypass completed normal-close attempt");
        result = await service.CloseGracefullyAsync(selected, CancellationToken.None);
        Check(result.RequestedCount == 1 && result.Remaining.Count == 2 && result.Issues.Any(x => x.Code == "no_window"), "background process is identified without inventing a close window");
        Check(fake.Delays == 12 && selected.All(service.CanForce), "one three-second nominal wait unlocks only surviving attempted identities");
        int reads = fake.Reads;
        result = await service.ForceCloseAsync(selected, false, CancellationToken.None);
        Check(result.Cancelled && fake.ForceCalls.Count == 0 && fake.Reads == reads, "declined explicit force confirmation performs no read or action");
        result = await service.ForceCloseAsync(selected.Where(x => x.Pid == 11), true, CancellationToken.None);
        Check(result.ExitedCount == 1 && fake.ForceCalls.SequenceEqual(new[] { 11 }), "confirmed force affects only the confirmed subset");
        Check(fake.Records.ContainsKey(12) && fake.Records.ContainsKey(13), "force does not expand to other process IDs or app instances");
        Check(!service.CanForce(selected.Single(x => x.Pid == 11)), "exited process loses force eligibility");
    }

    private static async Task IdentityAndScopeTests() {
        var fake = new FakePlatform(P(1));
        var service = new ProcessCloseService(fake);
        var original = service.Snapshot().Targets.Single();
        fake.Records[1] = P(1, "chrome.exe", 200);
        ProcessCloseBatchResult result = await service.CloseGracefullyAsync(new[] { original }, CancellationToken.None);
        Check(fake.NormalCalls.Count == 0 && result.Issues.Single().Code == "identity_changed", "PID reuse before normal close is rejected");
        fake.Records[1] = original;
        await service.CloseGracefullyAsync(new[] { original }, CancellationToken.None);
        fake.Records[1] = P(1, "chrome.exe", 300);
        result = await service.ForceCloseAsync(new[] { original }, true, CancellationToken.None);
        Check(fake.ForceCalls.Count == 0 && result.Issues.Single().Code == "identity_changed", "PID reuse before force cannot kill replacement");
        Check(!service.CanForce(original), "stale authorization is discarded");

        fake = new FakePlatform(P(2)); service = new ProcessCloseService(fake); original = service.Snapshot().Targets.Single();
        fake.BeforeAction = x => fake.Records[x.Pid] = P(x.Pid, "chrome.exe", 500);
        result = await service.CloseGracefullyAsync(new[] { original }, CancellationToken.None);
        Check(fake.NormalCalls.Count == 0 && result.Issues.Single().Code == "identity_changed", "platform revalidation catches replacement between service read and action");

        fake = new FakePlatform(P(3)); service = new ProcessCloseService(fake); original = service.Snapshot().Targets.Single();
        fake.ExitNormally.Add(3);
        fake.AfterNormal = x => fake.Records[99] = P(99, "chrome.exe", 900);
        result = await service.CloseGracefullyAsync(new[] { original }, CancellationToken.None);
        var newlyStarted = service.Snapshot().Targets.Single();
        Check(result.ExitedCount == 1 && newlyStarted.Pid == 99 && fake.NormalCalls.SequenceEqual(new[] { 3 }), "new process during close is listed but never silently selected");
        await service.ForceCloseAsync(new[] { newlyStarted }, true, CancellationToken.None);
        Check(fake.ForceCalls.Count == 0 && fake.Records.ContainsKey(99), "new process lacks normal-close authorization");

        foreach (ProcessCloseTarget changed in new[] { P(4, "chrome.exe", 100, "OTHER-USER"), P(4, "chrome.exe", 100, "FIXTURE-USER", 9), P(4, "node.exe"), P(4, "Codex.exe") }) {
            fake = new FakePlatform(P(4)); service = new ProcessCloseService(fake); original = service.Snapshot().Targets.Single();
            fake.Records[4] = changed;
            result = await service.CloseGracefullyAsync(new[] { original }, CancellationToken.None);
            Check(fake.NormalCalls.Count == 0 && result.Issues.Single().Code == "identity_changed", "changed ownership/session/name blocks action");
        }
        fake = new FakePlatform(P(42), P(5, "node.exe")); service = new ProcessCloseService(fake);
        result = await service.CloseGracefullyAsync(fake.Records.Values.ToArray(), CancellationToken.None);
        Check(fake.NormalCalls.Count == 0 && result.Issues.Count == 2, "forged selection cannot close helper or arbitrary node");
        fake = new FakePlatform(P(6)); service = new ProcessCloseService(fake); original = service.Snapshot().Targets.Single();
        await service.CloseGracefullyAsync(new[] { original }, CancellationToken.None);
        Check(!service.CanForce(P(6, "msedge.exe")), "force authorization pins name and owner as well as PID/start time");
    }

    private static async Task CancellationAndFailureTests() {
        var fake = new FakePlatform(P(1), P(2));
        var service = new ProcessCloseService(fake);
        var selected = service.Snapshot().Targets.ToArray();
        using (var cancellation = new CancellationTokenSource()) {
            cancellation.Cancel();
            var cancelled = await service.CloseGracefullyAsync(selected, cancellation.Token);
            Check(cancelled.Cancelled && fake.NormalCalls.Count == 0, "pre-cancelled request closes nothing");
        }
        using (var cancellation = new CancellationTokenSource()) {
            fake.AfterNormal = x => cancellation.Cancel();
            var cancelled = await service.CloseGracefullyAsync(selected, cancellation.Token);
            Check(cancelled.Cancelled && fake.NormalCalls.Count == 1 && fake.ForceCalls.Count == 0, "cancellation stops subsequent close requests");
            Check(selected.All(x => !service.CanForce(x)), "cancelled normal-close phase does not unlock force");
        }
        fake = new FakePlatform(P(3)); service = new ProcessCloseService(fake); selected = service.Snapshot().Targets.ToArray();
        using (var cancellation = new CancellationTokenSource()) {
            fake.AfterDelay = () => cancellation.Cancel();
            var cancelled = await service.CloseGracefullyAsync(selected, cancellation.Token);
            Check(cancelled.Cancelled && fake.Delays == 1 && !service.CanForce(selected[0]), "cancelled wait remains bounded without force authorization");
        }
        fake = new FakePlatform(P(20), P(21)); service = new ProcessCloseService(fake); selected = service.Snapshot().Targets.ToArray();
        await service.CloseGracefullyAsync(selected, CancellationToken.None);
        using (var cancellation = new CancellationTokenSource()) {
            fake.AfterForce = x => cancellation.Cancel();
            var cancelled = await service.ForceCloseAsync(selected, true, cancellation.Token);
            Check(cancelled.Cancelled && cancelled.RequestedCount == 1 && fake.ForceCalls.Count == 1, "force cancellation records the issued request and stops before next process");
            Check(fake.Records.Count == 1, "force cancellation preserves the not-yet-targeted process");
        }
        fake = new FakePlatform(P(4)); service = new ProcessCloseService(fake); selected = service.Snapshot().Targets.ToArray();
        fake.ReadFailures.Add(4);
        var failed = await service.CloseGracefullyAsync(selected, CancellationToken.None);
        Check(fake.NormalCalls.Count == 0 && failed.Issues.Single().Code == "validation_failed", "unreadable current identity fails closed without private error text");
        fake.ReadFailures.Clear(); fake.NormalFailures.Add(4);
        failed = await service.CloseGracefullyAsync(selected, CancellationToken.None);
        Check(failed.Issues.Single().Code == "access_denied" && !service.CanForce(selected[0]), "failed normal request does not falsely claim success or unlock force");
        fake.NormalFailures.Clear();
        await service.CloseGracefullyAsync(selected, CancellationToken.None);
        fake.ForceFailures.Add(4);
        failed = await service.ForceCloseAsync(selected, true, CancellationToken.None);
        Check(failed.RequestedCount == 0 && failed.ExitedCount == 0 && failed.Remaining.Count == 1 && failed.Issues.Single().Code == "action_failed", "failed force is reported with verified remaining instance");
        Check(service.CanForce(selected[0]), "failed force can be explicitly retried against same identity");
        fake.Records.Remove(4);
        var gone = await service.ForceCloseAsync(selected, true, CancellationToken.None);
        Check(gone.ExitedCount == 1 && !service.CanForce(selected[0]), "exit between display and click is treated as verified no-op");
    }

    private static IEnumerable<Control> All(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (Control nested in All(child)) yield return nested; } }
    private static object Field(object obj, string name) { return obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj); }
    private static void DialogTests() {
        foreach (bool english in new[] { false, true }) {
            Ui.English = english;
            using (var dialog = new ProcessCloseDialog(true)) {
                Check(!dialog.Visible && Field(dialog, "service") == null, "hidden demo creates no live platform");
                Check(!dialog.ShowInTaskbar && dialog.Icon != null, "process dialog uses bundled executable icon and no separate taskbar button");
                Check(dialog.MinimumSize.Width >= 700 && dialog.MinimumSize.Height >= 500, "dialog minimum size is 700x500");
                var list = All(dialog).OfType<ListView>().Single();
                Check(list.Items.Count == 3 && list.Items.Cast<ListViewItem>().All(x => x.SubItems.Count == 3), "demo lists names, PIDs and status only");
                Check(!((Button)Field(dialog, "normal")).Enabled && !((Button)Field(dialog, "force")).Enabled, "demo cannot close real processes");
                Check(!dialog.AllTargetsClosed && dialog.DialogResult == DialogResult.None, "construction does not claim apps exited or continue optimization");
                string text = String.Join("\n", All(dialog).Select(x => x.Text));
                Check(text.Contains(english ? "save prompts" : "保存提示") && text.Contains(english ? "separate confirmation" : "另行确认"), "normal close and separate force confirmation explained in both languages");
            }
            var fake = new FakePlatform(P(9));
            using (var dialog = new ProcessCloseDialog(false, new ProcessCloseService(fake))) {
                Check(fake.Enumerations == 0 && fake.NormalCalls.Count == 0 && fake.ForceCalls.Count == 0 && !dialog.Visible, "injected dialog construction does not inspect or close processes");
                Check(!((Button)Field(dialog, "force")).Enabled, "force is unavailable before a completed normal-close attempt");
                typeof(ProcessCloseDialog).GetMethod("Populate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(dialog, new object[] { new ProcessCloseSnapshot() });
                Check(dialog.AllTargetsClosed, "complete empty fixture snapshot enables continue");
                fake.EnumerationCancels = true;
                SynchronizationContext priorContext = SynchronizationContext.Current;
                try {
                    // No message pump or visible form is used; the injected listing cancels immediately.
                    SynchronizationContext.SetSynchronizationContext(null);
                    Task refresh = (Task)typeof(ProcessCloseDialog).GetMethod("RefreshAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(dialog, null);
                    Check(!dialog.AllTargetsClosed, "refresh immediately invalidates old empty snapshot");
                    refresh.GetAwaiter().GetResult();
                    Check(!dialog.AllTargetsClosed && ((Button)Field(dialog, "done")).Text == (english ? "Back" : "返回"), "cancelled refresh cannot restore stale continue authorization");
                } finally { SynchronizationContext.SetSynchronizationContext(priorContext); }
                Check(fake.NormalCalls.Count == 0 && fake.ForceCalls.Count == 0, "cancelled fixture refresh has no process actions");
            }
            var many = Enumerable.Range(1000, 512).Select(i => P(i)).ToList();
            using (Form confirmation = ProcessCloseDialog.CreateForceConfirmation(many)) {
                Check(!confirmation.Visible, "force-confirmation fixture is never shown");
                Check(!confirmation.ShowInTaskbar && confirmation.Icon != null, "force confirmation inherits executable icon and stays off taskbar");
                var list = All(confirmation).OfType<TextBox>().Single();
                Check(list.ReadOnly && list.ScrollBars == ScrollBars.Both && list.Lines.Length == 512, "large force confirmation retains all exact targets in a scrollable read-only list");
                Check(((Button)confirmation.AcceptButton).DialogResult == DialogResult.No && ((Button)confirmation.CancelButton).DialogResult == DialogResult.No, "Enter and Escape both default to declining force");
                string text = String.Join("\n", All(confirmation).Select(x => x.Text));
                Check(text.Contains("chrome.exe: 512") && text.Contains(english ? "Unsaved work may be lost" : "未保存的工作可能丢失"), "force confirmation gives exact name/count and explicit unsaved-work warning");
            }
        }
    }

    [STAThread] private static int Main() {
        try {
            SnapshotTests();
            GracefulAndForceTests().GetAwaiter().GetResult();
            IdentityAndScopeTests().GetAwaiter().GetResult();
            CancellationAndFailureTests().GetAwaiter().GetResult();
            DialogTests();
            Console.WriteLine("PASS " + checks + " checks. Injected fake process platform and hidden demo controls only; no real process enumeration, close requests, force termination or live dialog display.");
            return 0;
        } catch (Exception error) { Console.Error.WriteLine("FAIL after " + checks + " checks: " + error.GetType().Name + ": " + error.Message); return 1; }
    }
}
