using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WhaleClaudeHelper {
    internal sealed class ProcessCloseTarget {
        internal int Pid { get; private set; }
        internal long StartTimeUtcTicks { get; private set; }
        internal string Name { get; private set; }
        internal string OwnerSid { get; private set; }
        internal int SessionId { get; private set; }
        internal ProcessCloseTarget(int pid, long started, string name, string ownerSid, int session) {
            Pid = pid; StartTimeUtcTicks = started; Name = name; OwnerSid = ownerSid; SessionId = session;
        }
        internal string Key { get { return Pid.ToString(CultureInfo.InvariantCulture) + ":" + StartTimeUtcTicks.ToString(CultureInfo.InvariantCulture); } }
        public override string ToString() { return Name + " (PID " + Pid.ToString(CultureInfo.InvariantCulture) + ")"; }
    }

    internal sealed class ProcessCloseEnumeration {
        internal List<ProcessCloseTarget> Processes = new List<ProcessCloseTarget>();
        internal List<string> Errors = new List<string>();
    }

    internal sealed class ProcessCloseSnapshot {
        internal List<ProcessCloseTarget> Targets = new List<ProcessCloseTarget>();
        internal List<string> Errors = new List<string>();
        internal int ExcludedCount;
        internal bool AllTargetsClosed { get { return Targets.Count == 0 && ExcludedCount == 0 && Errors.Count == 0; } }
    }

    internal sealed class ProcessCloseIssue {
        internal ProcessCloseTarget Target;
        internal string Code;
    }

    internal sealed class ProcessCloseBatchResult {
        internal int RequestedCount;
        internal int ExitedCount;
        internal bool Cancelled;
        internal List<ProcessCloseTarget> Remaining = new List<ProcessCloseTarget>();
        internal List<ProcessCloseIssue> Issues = new List<ProcessCloseIssue>();
    }

    internal interface IProcessClosePlatform {
        int CurrentProcessId { get; }
        int CurrentSessionId { get; }
        string CurrentUserSid { get; }
        ProcessCloseEnumeration Enumerate(CancellationToken token);
        ProcessCloseTarget Read(int pid, CancellationToken token);
        string RequestClose(ProcessCloseTarget expected, CancellationToken token);
        string ForceClose(ProcessCloseTarget expected, CancellationToken token);
        Task DelayAsync(int milliseconds, CancellationToken token);
    }

    internal sealed class ProcessCloseService {
        internal const int MaximumTargets = 512;
        private const int PollCount = 12;
        private const int PollMilliseconds = 250;
        private readonly IProcessClosePlatform platform;
        private readonly object gate = new object();
        private readonly Dictionary<string, ProcessCloseTarget> forceEligible = new Dictionary<string, ProcessCloseTarget>(StringComparer.Ordinal);
        private bool running;

        internal ProcessCloseService() : this(new WindowsProcessClosePlatform()) { }
        internal ProcessCloseService(IProcessClosePlatform adapter) {
            if (adapter == null) throw new ArgumentNullException("adapter");
            platform = adapter;
        }

        internal static bool IsAllowedName(string name) {
            return String.Equals(name, "Claude.exe", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(name, "chrome.exe", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(name, "msedge.exe", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsOwnedAllowed(ProcessCloseTarget target) {
            return target != null && target.Pid > 0 && target.Pid != platform.CurrentProcessId && target.StartTimeUtcTicks > 0 &&
                IsAllowedName(target.Name) && target.SessionId == platform.CurrentSessionId &&
                !String.IsNullOrEmpty(platform.CurrentUserSid) && String.Equals(target.OwnerSid, platform.CurrentUserSid, StringComparison.Ordinal);
        }

        internal static bool SameIdentity(ProcessCloseTarget left, ProcessCloseTarget right) {
            return left != null && right != null && left.Pid == right.Pid && left.StartTimeUtcTicks == right.StartTimeUtcTicks &&
                left.SessionId == right.SessionId && String.Equals(left.OwnerSid, right.OwnerSid, StringComparison.Ordinal) &&
                String.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }

        internal ProcessCloseSnapshot Snapshot() { return Snapshot(CancellationToken.None); }
        internal ProcessCloseSnapshot Snapshot(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            var snapshot = new ProcessCloseSnapshot();
            ProcessCloseEnumeration enumeration;
            try { enumeration = platform.Enumerate(token); }
            catch (OperationCanceledException) { throw; }
            catch { snapshot.Errors.Add("enumeration_failed"); return snapshot; }
            if (enumeration == null) { snapshot.Errors.Add("enumeration_failed"); return snapshot; }
            // Platform errors are deliberately replaced by fixed codes; paths/titles never reach UI.
            if (enumeration.Errors.Count != 0) snapshot.Errors.Add("enumeration_incomplete");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            foreach (ProcessCloseTarget target in enumeration.Processes) {
                token.ThrowIfCancellationRequested();
                if (++count > MaximumTargets) { snapshot.Errors.Add("too_many_processes"); break; }
                if (target == null || !IsAllowedName(target.Name)) continue;
                if (!IsOwnedAllowed(target)) { snapshot.ExcludedCount++; continue; }
                if (seen.Add(target.Key)) snapshot.Targets.Add(target);
            }
            snapshot.Targets = snapshot.Targets.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Pid).ToList();
            return snapshot;
        }

        internal bool CanForce(ProcessCloseTarget target) {
            if (!IsOwnedAllowed(target)) return false;
            lock (gate) {
                ProcessCloseTarget approved;
                return forceEligible.TryGetValue(target.Key, out approved) && SameIdentity(approved, target);
            }
        }

        internal Task<ProcessCloseBatchResult> CloseGracefullyAsync(IEnumerable<ProcessCloseTarget> selected, CancellationToken token) {
            return CloseAsync(selected, false, true, token);
        }

        internal Task<ProcessCloseBatchResult> ForceCloseAsync(IEnumerable<ProcessCloseTarget> selectedRemaining, bool explicitConfirmation, CancellationToken token) {
            return CloseAsync(selectedRemaining, true, explicitConfirmation, token);
        }

        private async Task<ProcessCloseBatchResult> CloseAsync(IEnumerable<ProcessCloseTarget> selected, bool force, bool confirmed, CancellationToken token) {
            var result = new ProcessCloseBatchResult();
            if (force && !confirmed) { result.Cancelled = true; return result; }
            lock (gate) {
                if (running) { AddIssue(result, null, "operation_busy"); return result; }
                running = true;
            }
            var pending = new List<ProcessCloseTarget>();
            try {
                token.ThrowIfCancellationRequested();
                var targets = (selected ?? new ProcessCloseTarget[0]).Where(x => x != null).Take(MaximumTargets + 1).GroupBy(x => x.Key, StringComparer.Ordinal).Select(x => x.First()).ToList();
                if (targets.Count > MaximumTargets) { AddIssue(result, null, "too_many_processes"); return result; }
                if (!force) lock (gate) foreach (ProcessCloseTarget target in targets) forceEligible.Remove(target.Key);
                foreach (ProcessCloseTarget target in targets) {
                    token.ThrowIfCancellationRequested();
                    if (!IsOwnedAllowed(target)) { AddIssue(result, target, "not_allowed_or_owned"); continue; }
                    if (force && !CanForce(target)) { AddIssue(result, target, "normal_close_required"); continue; }
                    ProcessCloseTarget current;
                    if (!TryRead(target, token, result, out current)) continue;
                    if (current == null) { result.ExitedCount++; Forget(target); continue; }
                    string action;
                    try { action = force ? platform.ForceClose(target, token) : platform.RequestClose(target, token); }
                    catch (OperationCanceledException) { throw; }
                    catch { action = "action_failed"; }
                    if (action == "exited") { result.ExitedCount++; Forget(target); continue; }
                    if (action == "requested" || (!force && action == "no_window")) {
                        if (action == "requested") result.RequestedCount++;
                        else AddIssue(result, target, "no_window");
                        pending.Add(target);
                    } else {
                        AddIssue(result, target, SafeActionCode(action));
                        // A failed force request can be retried only against the same verified instance.
                        if (force && action == "action_failed") pending.Add(target);
                        else Forget(target);
                    }
                }
                // A single bounded wait for the selected instances, never one wait per process.
                for (int poll = 0; poll <= PollCount && pending.Count > 0; poll++) {
                    token.ThrowIfCancellationRequested();
                    var survivors = new List<ProcessCloseTarget>();
                    foreach (ProcessCloseTarget target in pending) {
                        token.ThrowIfCancellationRequested();
                        ProcessCloseTarget current;
                        if (!TryRead(target, token, result, out current)) { Forget(target); continue; }
                        if (current == null) { result.ExitedCount++; Forget(target); }
                        else survivors.Add(target);
                    }
                    pending = survivors;
                    if (pending.Count == 0 || poll == PollCount) break;
                    await platform.DelayAsync(PollMilliseconds, token).ConfigureAwait(false);
                }
                token.ThrowIfCancellationRequested();
                result.Remaining.AddRange(pending);
                // Force is unlocked only after a complete normal-close attempt and bounded wait.
                if (!force) lock (gate) foreach (ProcessCloseTarget target in pending) forceEligible[target.Key] = target;
            } catch (OperationCanceledException) {
                result.Cancelled = true;
                result.Remaining.AddRange(pending);
            } finally { lock (gate) running = false; }
            return result;
        }

        private bool TryRead(ProcessCloseTarget expected, CancellationToken token, ProcessCloseBatchResult result, out ProcessCloseTarget current) {
            current = null;
            try { current = platform.Read(expected.Pid, token); }
            catch (OperationCanceledException) { throw; }
            catch { AddIssue(result, expected, "validation_failed"); Forget(expected); return false; }
            if (current == null) return true;
            if (!IsOwnedAllowed(current) || !SameIdentity(expected, current)) {
                AddIssue(result, expected, "identity_changed");
                Forget(expected);
                return false;
            }
            return true;
        }

        private void Forget(ProcessCloseTarget target) { lock (gate) forceEligible.Remove(target.Key); }
        private static void AddIssue(ProcessCloseBatchResult result, ProcessCloseTarget target, string code) { result.Issues.Add(new ProcessCloseIssue { Target = target, Code = code }); }
        private static string SafeActionCode(string code) {
            switch (code) {
                case "identity_changed": case "not_allowed_or_owned": case "access_denied": return code;
                default: return "action_failed";
            }
        }
    }

    // Uses process handles to revalidate identity and ownership immediately before each action.
    // TerminateProcess receives that same handle, so PID reuse cannot redirect a force request.
    internal sealed class WindowsProcessClosePlatform : IProcessClosePlatform {
        private const uint QueryLimitedInformation = 0x1000, Synchronize = 0x100000, Terminate = 1, TokenQuery = 8;
        private const uint WaitTimeout = 258;
        public int CurrentProcessId { get; private set; }
        public int CurrentSessionId { get; private set; }
        public string CurrentUserSid { get; private set; }

        internal WindowsProcessClosePlatform() {
            using (Process process = Process.GetCurrentProcess()) { CurrentProcessId = process.Id; CurrentSessionId = process.SessionId; }
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent()) { CurrentUserSid = identity.User == null ? null : identity.User.Value; }
        }

        public ProcessCloseEnumeration Enumerate(CancellationToken token) {
            var result = new ProcessCloseEnumeration();
            int inspected = 0;
            foreach (string name in new[] { "Claude", "chrome", "msedge" }) {
                token.ThrowIfCancellationRequested();
                Process[] processes;
                try { processes = Process.GetProcessesByName(name); }
                catch { result.Errors.Add("enumeration_failed"); continue; }
                try {
                    foreach (Process process in processes) {
                        token.ThrowIfCancellationRequested();
                        if (++inspected > ProcessCloseService.MaximumTargets) { result.Errors.Add("too_many_processes"); return result; }
                        try {
                            ProcessCloseTarget record = Read(process.Id, token);
                            if (record != null) result.Processes.Add(record);
                        } catch (OperationCanceledException) { throw; }
                          catch { result.Errors.Add("process_unreadable"); }
                    }
                } finally { foreach (Process process in processes) process.Dispose(); }
            }
            return result;
        }

        public ProcessCloseTarget Read(int pid, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            IntPtr handle = OpenProcess(QueryLimitedInformation | Synchronize, false, pid);
            if (handle == IntPtr.Zero) {
                int error = Marshal.GetLastWin32Error();
                if (error == 87) return null; // Process exited before OpenProcess.
                throw new UnauthorizedAccessException("process_unreadable");
            }
            try { return ReadHandle(handle, pid, token); }
            finally { CloseHandle(handle); }
        }

        public string RequestClose(ProcessCloseTarget expected, CancellationToken token) { return Act(expected, false, token); }
        public string ForceClose(ProcessCloseTarget expected, CancellationToken token) { return Act(expected, true, token); }
        public Task DelayAsync(int milliseconds, CancellationToken token) { return Task.Delay(milliseconds, token); }

        private bool OwnedAllowed(ProcessCloseTarget target) {
            return target != null && target.Pid > 0 && target.Pid != CurrentProcessId && ProcessCloseService.IsAllowedName(target.Name) &&
                target.SessionId == CurrentSessionId && !String.IsNullOrEmpty(CurrentUserSid) && String.Equals(target.OwnerSid, CurrentUserSid, StringComparison.Ordinal);
        }

        private string Act(ProcessCloseTarget expected, bool force, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            if (!OwnedAllowed(expected)) return "not_allowed_or_owned";
            IntPtr handle = OpenProcess(QueryLimitedInformation | Synchronize | (force ? Terminate : 0), false, expected.Pid);
            if (handle == IntPtr.Zero) return Marshal.GetLastWin32Error() == 87 ? "exited" : "access_denied";
            try {
                ProcessCloseTarget current = ReadHandle(handle, expected.Pid, token);
                if (current == null) return "exited";
                if (!OwnedAllowed(current) || !ProcessCloseService.SameIdentity(expected, current)) return "identity_changed";
                token.ThrowIfCancellationRequested();
                if (force) return TerminateProcess(handle, 1) ? "requested" : "action_failed";
                int posted = 0;
                bool failed = false;
                // Only visible top-level windows belonging to this selected process are asked to close.
                // No title is read, and the close message does not answer any save-work dialog.
                EnumWindowsProc callback = delegate(IntPtr window, IntPtr parameter) {
                    if (token.IsCancellationRequested || posted >= 64) return false;
                    uint pid;
                    GetWindowThreadProcessId(window, out pid);
                    if (pid != expected.Pid || !IsWindowVisible(window)) return true;
                    if (WaitForSingleObject(handle, 0) != WaitTimeout) return false;
                    if (PostMessage(window, 0x0010, UIntPtr.Zero, IntPtr.Zero)) posted++;
                    else failed = true;
                    return true;
                };
                bool enumerated = EnumWindows(callback, IntPtr.Zero);
                GC.KeepAlive(callback);
                // Preserve the fact that a request was already sent if cancellation arrived
                // partway through this process's windows. The service stops before the next PID.
                if (posted > 0) return "requested";
                token.ThrowIfCancellationRequested();
                if (WaitForSingleObject(handle, 0) != WaitTimeout) return "exited";
                return failed || !enumerated ? "action_failed" : "no_window";
            } catch (OperationCanceledException) { throw; }
              catch (UnauthorizedAccessException) { return "access_denied"; }
              catch { return "action_failed"; }
            finally { CloseHandle(handle); }
        }

        private static ProcessCloseTarget ReadHandle(IntPtr handle, int pid, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            uint wait = WaitForSingleObject(handle, 0);
            if (wait == 0) return null;
            if (wait != WaitTimeout) throw new IOException("process_state_unavailable");
            NativeFileTime created, exited, kernel, user;
            if (!GetProcessTimes(handle, out created, out exited, out kernel, out user)) throw new IOException("process_start_unavailable");
            var path = new StringBuilder(32768);
            int length = path.Capacity;
            if (!QueryFullProcessImageName(handle, 0, path, ref length)) throw new IOException("process_name_unavailable");
            string name = Path.GetFileName(path.ToString());
            uint session;
            if (!ProcessIdToSessionId((uint)pid, out session) || session > Int32.MaxValue) throw new IOException("process_session_unavailable");
            string owner = ReadOwnerSid(handle);
            token.ThrowIfCancellationRequested();
            if (WaitForSingleObject(handle, 0) == 0) return null;
            long fileTime = ((long)created.High << 32) | created.Low;
            return new ProcessCloseTarget(pid, DateTime.FromFileTimeUtc(fileTime).Ticks, name, owner, (int)session);
        }

        private static string ReadOwnerSid(IntPtr process) {
            IntPtr token;
            if (!OpenProcessToken(process, TokenQuery, out token)) throw new UnauthorizedAccessException("process_owner_unavailable");
            IntPtr buffer = IntPtr.Zero;
            try {
                int size;
                GetTokenInformation(token, 1, IntPtr.Zero, 0, out size);
                if (size < IntPtr.Size || size > 65536) throw new IOException("invalid_owner_size");
                buffer = Marshal.AllocHGlobal(size);
                if (!GetTokenInformation(token, 1, buffer, size, out size)) throw new UnauthorizedAccessException("process_owner_unavailable");
                return new SecurityIdentifier(Marshal.ReadIntPtr(buffer)).Value;
            } finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); CloseHandle(token); }
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
        [StructLayout(LayoutKind.Sequential)] private struct NativeFileTime { internal uint Low, High; }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int pid);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetProcessTimes(IntPtr process, out NativeFileTime created, out NativeFileTime exited, out NativeFileTime kernel, out NativeFileTime user);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref int size);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ProcessIdToSessionId(uint pid, out uint session);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateProcess(IntPtr process, uint exitCode);
        [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetTokenInformation(IntPtr token, int informationClass, IntPtr information, int length, out int returnedLength);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);
    }
}
