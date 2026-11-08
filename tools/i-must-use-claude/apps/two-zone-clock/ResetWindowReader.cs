using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Automation;

namespace TwoZoneClock {
    internal sealed class ResetSourceWindow {
        public long Handle;
        public int ProcessId;
        public long StartTicks;
        public string Title;
        public override string ToString() { return Title ?? "Claude"; }
    }

    internal sealed class ResetWindowResult {
        public bool Ok;
        public string Status;
        public string Text;
        public bool DiagnosticsAvailable;
        public int VisitedNodes, ReadableNodes, RepeatedInlineNodes;
        public bool HasUsageHeading, HasSessionHeading, HasWeeklyHeading, HasResetLabel;
    }

    // Run Read only in the disposable child process. A UIA provider can block inside
    // any call, so the clock's parent process must enforce its own wall-clock timeout.
    // This class never reads credentials, documents, edit values, or the clipboard.
    internal static class ResetWindowReader {
        internal const int MaximumWindows = 128;
        internal const int MaximumNodes = 2400;
        internal const int MaximumTextCharacters = 16000;
        private const int MaximumScanMilliseconds = 2500;
        private const int MaximumTitleCharacters = 512;
        private const uint RootAncestor = 2;
        private delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder title, int maximumCount);

        internal static bool IsCandidateApp(string processName, string title) {
            if (String.Equals(processName, "Claude", StringComparison.OrdinalIgnoreCase)) return true;
            bool browser = String.Equals(processName, "chrome", StringComparison.OrdinalIgnoreCase)
                || String.Equals(processName, "msedge", StringComparison.OrdinalIgnoreCase)
                || String.Equals(processName, "firefox", StringComparison.OrdinalIgnoreCase);
            return browser && !String.IsNullOrWhiteSpace(title)
                && title.IndexOf("Claude", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Kept pure so identity races and allowlist boundaries can be tested without
        // enumerating or touching any real application window.
        internal static string ClassifyWindow(bool exists, bool visible, bool minimized, bool topLevel,
            int actualPid, long actualStartTicks, int expectedPid, long expectedStartTicks,
            string processName, string title) {
            if (!exists) return "window_closed";
            if (!visible || minimized) return "not_visible";
            if (!topLevel || expectedPid <= 0 || expectedStartTicks <= 0
                || actualPid != expectedPid || actualStartTicks != expectedStartTicks
                || !IsCandidateApp(processName, title)) return "wrong_window";
            return "ok";
        }

        private static string WindowTitle(IntPtr window) {
            var title = new StringBuilder(MaximumTitleCharacters + 1);
            GetWindowText(window, title, title.Capacity);
            return title.ToString();
        }

        internal static List<ResetSourceWindow> ListWindows() {
            var result = new List<ResetSourceWindow>();
            EnumWindowCallback callback = delegate(IntPtr window, IntPtr parameter) {
                if (result.Count >= MaximumWindows) return false;
                try {
                    if (!IsWindowVisible(window) || IsIconic(window)) return true;
                    uint pid;
                    GetWindowThreadProcessId(window, out pid);
                    if (pid == 0 || pid > Int32.MaxValue) return true;
                    string title = WindowTitle(window);
                    using (var process = Process.GetProcessById((int)pid)) {
                        if (!IsCandidateApp(process.ProcessName, title)) return true;
                        long started = process.StartTime.ToUniversalTime().Ticks;
                        // Recheck after reading process metadata; windows can close or
                        // be reassigned while EnumWindows is walking the desktop.
                        uint currentPid;
                        GetWindowThreadProcessId(window, out currentPid);
                        if (!IsWindow(window) || !IsWindowVisible(window) || IsIconic(window)
                            || currentPid != pid) return true;
                        result.Add(new ResetSourceWindow {
                            Handle = window.ToInt64(), ProcessId = (int)pid,
                            StartTicks = started, Title = title
                        });
                    }
                } catch { /* Unreadable or disappearing windows are not candidates. */ }
                return result.Count < MaximumWindows;
            };
            EnumWindows(callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            return result;
        }

        private static string ValidateWindow(IntPtr window, int expectedPid, long expectedStartTicks) {
            if (window == IntPtr.Zero || !IsWindow(window)) return "window_closed";
            if (!IsWindowVisible(window) || IsIconic(window)) return "not_visible";
            if (GetAncestor(window, RootAncestor) != window) return "wrong_window";
            uint pid;
            GetWindowThreadProcessId(window, out pid);
            if (pid == 0 || pid > Int32.MaxValue || (int)pid != expectedPid) return "wrong_window";
            try {
                using (var process = Process.GetProcessById((int)pid)) {
                    long started = process.StartTime.ToUniversalTime().Ticks;
                    string processName = process.ProcessName;
                    string title = WindowTitle(window);
                    uint latestPid;
                    GetWindowThreadProcessId(window, out latestPid);
                    return ClassifyWindow(IsWindow(window), IsWindowVisible(window), IsIconic(window),
                        GetAncestor(window, RootAncestor) == window,
                        latestPid > Int32.MaxValue ? 0 : (int)latestPid, started,
                        expectedPid, expectedStartTicks, processName, title);
                }
            } catch {
                return IsWindow(window) ? "read_failed" : "window_closed";
            }
        }

        private static ResetWindowResult Result(string status, string text) {
            return new ResetWindowResult { Ok = status == "ok", Status = status, Text = text ?? "" };
        }

        private sealed class ScanEntry {
            internal readonly AutomationElement Element;
            internal readonly string CoveredResetText;
            internal ScanEntry(AutomationElement element, string coveredResetText) {
                Element = element; CoveredResetText = coveredResetText;
            }
        }

        internal static ResetWindowResult Read(long handle, int expectedPid, long expectedStartTicks) {
            var watch = Stopwatch.StartNew();
            try {
                var window = new IntPtr(handle);
                string identity = ValidateWindow(window, expectedPid, expectedStartTicks);
                if (identity != "ok") return Result(identity, "");
                var root = AutomationElement.FromHandle(window);
                if (root == null) return Result("read_failed", "");
                // Chromium can expose visible static text only in RawView, or
                // attach inline text children to Text controls. ControlView plus
                // a leaf-Text-only rule silently missed those labels.
                var walker = TreeWalker.RawViewWalker;
                System.Windows.Rect viewport = root.Current.BoundingRectangle;
                var pending = new Stack<ScanEntry>();
                var collector = new UsageTextCollector();
                var cache = new CacheRequest();
                cache.TreeScope = TreeScope.Element;
                cache.Add(AutomationElement.ControlTypeProperty);
                cache.Add(AutomationElement.IsOffscreenProperty);
                cache.Add(AutomationElement.IsPasswordProperty);
                cache.Add(AutomationElement.BoundingRectangleProperty);
                // Name is deliberately NOT cached: names are requested only after
                // the control has passed the type/viewport/password checks.
                AutomationElement first = walker.GetFirstChild(root, cache);
                if (first != null) pending.Push(new ScanEntry(first, null));
                int nodes = 0, readableNodes = 0;
                while (pending.Count > 0) {
                    if (watch.ElapsedMilliseconds >= MaximumScanMilliseconds || ++nodes > MaximumNodes)
                        return Result("scan_limit", "");
                    ScanEntry entry = pending.Pop();
                    AutomationElement current = entry.Element;
                    AutomationElement next = walker.GetNextSibling(current, cache);
                    if (next != null) pending.Push(new ScanEntry(next, entry.CoveredResetText));
                    var info = current.Cached;
                    if (info.IsPassword || info.ControlType == ControlType.Edit) continue;
                    AutomationElement child = walker.GetFirstChild(current, cache);
                    string childCoverage = entry.CoveredResetText;
                    // Structural containers may be marked offscreen even when
                    // their children are visible; visibility applies to each text
                    // node independently. Never read an edit/password subtree.
                    bool readStaticText = MayReadName(info.ControlType, child != null, info.IsOffscreen,
                        info.IsPassword, info.BoundingRectangle, viewport);
                    bool readHeadingControl = !readStaticText && MayReadHeadingName(info.ControlType,
                        info.IsOffscreen, info.IsPassword, info.BoundingRectangle, viewport);
                    if (readStaticText || readHeadingControl) {
                        if (watch.ElapsedMilliseconds >= MaximumScanMilliseconds) return Result("scan_limit", "");
                        readableNodes++;
                        string name = current.Current.Name;
                        // The compact page's 本周 heading is a dropdown button.
                        // Its exact usage heading is eligible, never arbitrary
                        // button text (which must not end or change limit scope).
                        bool added = readStaticText ? collector.AddInlineBlock(name, entry.CoveredResetText) : collector.AddHeadingBlock(name);
                        if (!added) return Result("scan_limit", "");
                        string completeReset = collector.AcceptedInlineCoverage(info.ControlType, name);
                        if (completeReset != null) childCoverage = completeReset;
                    }
                    if (child != null) pending.Push(new ScanEntry(child, childCoverage));
                    if (collector.Ended) break;
                }
                if (watch.ElapsedMilliseconds >= MaximumScanMilliseconds) return Result("scan_limit", "");
                identity = ValidateWindow(window, expectedPid, expectedStartTicks);
                if (identity != "ok") return Result(identity, "");
                if (watch.ElapsedMilliseconds >= MaximumScanMilliseconds) return Result("scan_limit", "");
                string text = collector.Finish();
                var result = String.IsNullOrEmpty(text) ? Result("usage_not_found", "") : Result("ok", text);
                result.DiagnosticsAvailable = true; result.VisitedNodes = nodes; result.ReadableNodes = readableNodes;
                collector.AddDiagnostics(result);
                return result;
            } catch {
                // Exception text may include application content or internal paths.
                return Result("read_failed", "");
            }
        }

        // Pure visibility/type gate used by synthetic accessibility fixtures.
        // Container names can contain whole documents, so only visible text or
        // leaf static containers are eligible. No Value/TextPattern is requested.
        internal static bool MayReadName(ControlType type, bool hasChildren, bool offscreen,
            bool password, System.Windows.Rect bounds, System.Windows.Rect viewport) {
            if (password || offscreen || type == ControlType.Edit || bounds.IsEmpty || viewport.IsEmpty
                || bounds.Width <= 0 || bounds.Height <= 0 || !bounds.IntersectsWith(viewport)) return false;
            return type == ControlType.Text || !hasChildren &&
                (type == ControlType.Custom || type == ControlType.Group || type == ControlType.Pane);
        }

        internal static bool MayReadHeadingName(ControlType type, bool offscreen, bool password,
            System.Windows.Rect bounds, System.Windows.Rect viewport) {
            return (type == ControlType.Button || type == ControlType.ComboBox)
                && MayReadName(ControlType.Text, false, offscreen, password, bounds, viewport);
        }

        // Coverage belongs only to descendants of one visible Text node containing
        // a complete reset value. It never crosses siblings/limit cards or comes
        // from a document/container name, so independent or conflicting times stay.
        internal static string InlineResetCoverage(ControlType type, string text) {
            return type == ControlType.Text ? UsageTextCollector.CompleteResetText(text) : null;
        }

        // Only the plan-limit section is returned, never general page or chat text.
        // Unknown/localized layouts fail closed and can use the paste fallback.
        internal sealed class UsageTextCollector {
            private int seenCharacters, repeatedInlineNodes;
            private bool started, usageHeading, session, weekly, reset;
            private bool ended, weeklyAllScope;
            private readonly List<string> lines = new List<string>();
            private static readonly Regex Percentage = new Regex(@"^\d{1,3}(?:\.\d+)?\s*%\s*(?:used|已使用|已用|使用量)?$|^(?:已使用|已用)\s*\d{1,3}(?:\.\d+)?\s*%$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            private static readonly Regex SessionHeading = Pattern(@"^(?:Current\s+session|当前会话|當前會話|本次会话|本次會話|当前时段|當前時段|本轮额度|本輪額度|本轮|本輪)(?=$|\s|[:：])\s*[:：]?\s*(.*)$");
            private static readonly Regex WeeklyHeading = Pattern(@"^(?:Weekly\s+limits?|每周限额|每週限額|每周限制|每週限制|每?周用量限制|每?週用量限制)(?=$|\s|[:：])\s*[:：]?\s*(.*)$");
            private static readonly Regex CompactWeeklyHeading = Pattern(@"^(?:本周|本週|This\s+week)(?=$|\s|[:：])\s*[:：]?\s*(.*)$");
            private static readonly Regex UsageTitle = Pattern(@"^(?:您的用量|你的用量|Your\s+usage)(?:\s+(?:Max|Pro|Free|Team|Enterprise))?$");
            private static readonly Regex AllModelsHeading = Pattern(@"^(?:All\s+models|所有模型|全部模型)(?=$|\s|[:：])\s*[:：]?\s*(.*)$");
            // Preserve the parser's scope markers even when they carry a model
            // version or qualifier. Dropping one can assign its time to All models.
            private static readonly Regex ModelHeading = Pattern(@"^(?:(?:(?:Only|仅限?|僅限?|只有)\s*)?(?:Claude\s+)?(?:Sonnet|Opus|Haiku)(?![a-z])|模型专属|模型專屬|Model[- ]specific\b)");
            private static readonly Regex OtherHeading = Pattern(@"^(?:Extra\s+usage|Additional\s+usage|Usage\s+credits|额外用量|額外用量|额外使用量|額外使用量|额外使用|額外使用|用量积分|用量積分|Usage\s+history|使用记录|使用記錄|限额重置|限額重置|用量额度|用量額度)(?=$|\s|[:：])");
            private static readonly Regex ResetWord = Pattern(@"\bResets?\b|重置");
            private static readonly Regex TimeValue = Pattern(@"^(?:(?:Sun(?:day)?|Mon(?:day)?|Tue(?:sday)?|Wed(?:nesday)?|Thu(?:rsday)?|Fri(?:day)?|Sat(?:urday)?|(?:周|週|星期)[一二三四五六日天])\s*[,]?\s*)?(?:(?:上午|下午|晚上|中午|凌晨)\s*)?\d{1,2}:\d{2}(?::\d{2})?\s*(?:AM|PM)?(?:\s*(?:UTC|GMT|[+-]\d{2}:\d{2}))?$");
            private static readonly Regex DurationValue = Pattern(@"^\d+\s*(?:days?|d|hours?|hrs?|hr|h|minutes?|mins?|min|m|seconds?|secs?|sec|s|天|小时|小時|分钟|分鐘|秒钟|秒鐘|秒)(?:\s*(?:,|and|和)?\s*\d+\s*(?:days?|d|hours?|hrs?|hr|h|minutes?|mins?|min|m|seconds?|secs?|sec|s|天|小时|小時|分钟|分鐘|秒钟|秒鐘|秒)){0,3}$");
            private static readonly Regex IsoValue = Pattern(@"^\d{4}-\d{2}-\d{2}[Tt]\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:[Zz]|[+-]\d{2}:\d{2})$");
            private static readonly Regex RelativeEnglishReset = Pattern(@"^Resets?\s+in\s+(.+)$");
            private static readonly Regex RelativeChineseReset = Pattern(@"^(?:将在|將在)?\s*(.+?)\s*(?:后|後)\s*重置$");
            private static readonly Regex AbsoluteEnglishReset = Pattern(@"^Resets?\s+(?:(?:at|on)\s+)?(.+)$");
            private static readonly Regex AbsoluteChineseReset = Pattern(@"^(?:重置(?:时间|時間)?\s*:?\s*(?:于|於)?\s*|(?:将于|將於|于|於)\s*)(.+?)(?:\s*重置)?$");
            private static readonly Regex SuffixChineseReset = Pattern(@"^(.+?)\s*重置$");
            private static Regex Pattern(string pattern) {
                return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            }

            internal bool Ended { get { return ended; } }

            internal void AddDiagnostics(ResetWindowResult result) {
                result.RepeatedInlineNodes = repeatedInlineNodes;
                result.HasUsageHeading = usageHeading; result.HasSessionHeading = session;
                result.HasWeeklyHeading = weekly; result.HasResetLabel = reset;
            }

            internal string AcceptedInlineCoverage(ControlType type, string text) {
                // A complete-looking value before the usage context was ignored;
                // it cannot suppress children that establish that context later.
                return started && !ended ? InlineResetCoverage(type, text) : null;
            }

            internal bool AddHeadingBlock(string raw) {
                if (raw == null) return true;
                if (raw.Length > MaximumTextCharacters - seenCharacters) return false;
                if (IsUsageHeadingName(raw)) return AddBlock(raw);
                seenCharacters += raw.Length;
                return true;
            }

            internal static bool IsUsageHeadingName(string raw) {
                if (raw == null || raw.Length > 300 || raw.IndexOf('\n') >= 0 || raw.IndexOf('\r') >= 0) return false;
                string value = NormalizeLine(raw);
                if (UsageTitle.IsMatch(value) || IsOneOf(value.ToLowerInvariant(), "usage", "用量", "使用情况", "使用情況", "使用量", "plan usage limits", "套餐使用限制", "计划用量限制", "計劃用量限制", "方案使用限制", "方案使用量限制")) return true;
                foreach (Regex heading in new[] { SessionHeading, WeeklyHeading, CompactWeeklyHeading, AllModelsHeading }) {
                    Match match = heading.Match(value);
                    if (match.Success && match.Groups[1].Value.Trim().Length == 0) return true;
                }
                return false;
            }

            internal bool AddInlineBlock(string raw, string coveredResetText) {
                if (raw != null && raw.Length > MaximumTextCharacters - seenCharacters) return false;
                if (raw != null && coveredResetText != null) {
                    string fragment = CompactInlineText(raw);
                    if (fragment.Length > 0 && CompactInlineText(coveredResetText).IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) {
                        // Repeated inline text still consumes the same scan budget.
                        seenCharacters += raw.Length;
                        repeatedInlineNodes++;
                        return true;
                    }
                }
                return AddBlock(raw);
            }

            private static string CompactInlineText(string value) {
                return String.Concat(NormalizeLine(value).Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            }

            internal static string CompleteResetText(string raw) {
                if (raw == null || raw.Length > 300 || raw.IndexOf('\n') >= 0 || raw.IndexOf('\r') >= 0) return null;
                string value = NormalizeLine(raw);
                if (!ResetWord.IsMatch(value)) return null;
                Match match = RelativeEnglishReset.Match(value);
                if (!match.Success) match = RelativeChineseReset.Match(value);
                if (match.Success) return DurationValue.IsMatch(match.Groups[1].Value.Trim()) ? value : null;
                match = AbsoluteEnglishReset.Match(value);
                if (!match.Success) match = AbsoluteChineseReset.Match(value);
                if (!match.Success) match = SuffixChineseReset.Match(value);
                if (!match.Success) return null;
                string time = match.Groups[1].Value.Trim();
                return TimeValue.IsMatch(time) || IsoValue.IsMatch(time) ? value : null;
            }

            private static string NormalizeLine(string raw) {
                return raw.Replace('\u00a0', ' ').Replace('\u202f', ' ').Replace('\u200e', ' ').Replace('\u200f', ' ').Replace('\t', ' ').Replace('：', ':').Trim();
            }

            internal bool AddBlock(string raw) {
                if (raw == null) return true;
                // A bounded static accessibility name may expose several lines.
                // Each line still passes the same usage-only allowlist.
                if (raw.Length > MaximumTextCharacters - seenCharacters) return false;
                foreach (string line in raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                    if (!Add(line)) return false;
                return true;
            }

            internal bool Add(string raw) {
                if (raw == null) return true;
                if (raw.Length > MaximumTextCharacters - seenCharacters) return false;
                seenCharacters += raw.Length;
                if (ended) return true;
                string value = NormalizeLine(raw);
                if (value.Length == 0 || value.Length > 300 || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0) return true;
                string key = value.ToLowerInvariant();
                if (OtherHeading.IsMatch(value)) {
                    if (started) ended = true;
                    return true;
                }
                Match sessionMatch = SessionHeading.Match(value);
                Match weeklyMatch = CompactWeeklyHeading.Match(value);
                bool compactWeekly = weeklyMatch.Success;
                if (!compactWeekly) weeklyMatch = WeeklyHeading.Match(value);
                bool sessionLine = sessionMatch.Success;
                bool weeklyLine = weeklyMatch.Success;
                bool heading = UsageTitle.IsMatch(value) || IsOneOf(key, "usage", "用量", "使用情况", "使用情況", "使用量", "plan usage limits", "套餐使用限制", "计划用量限制", "計劃用量限制", "方案使用限制", "方案使用量限制");
                if (heading || sessionLine || weeklyLine) {
                    started = true;
                    usageHeading |= heading;
                    session |= sessionLine;
                    weekly |= weeklyLine;
                    if (sessionLine || weeklyLine) weeklyAllScope = weeklyLine && (compactWeekly || AllModelsHeading.IsMatch(weeklyMatch.Groups[1].Value.Trim()));
                    reset |= ResetWord.IsMatch(value);
                    Append(value);
                    return true;
                }
                if (!started) return true;
                if (ModelHeading.IsMatch(value)) {
                    weeklyAllScope = false;
                    Append(value);
                    return true;
                }
                if (AllModelsHeading.IsMatch(value)) {
                    weeklyAllScope = true;
                    reset |= ResetWord.IsMatch(value);
                    Append(value);
                    return true;
                }
                bool resetLine = key == "resets" || key == "reset"
                    || key.StartsWith("resets ", StringComparison.Ordinal)
                    || key.StartsWith("reset ", StringComparison.Ordinal)
                    || key.StartsWith("重置", StringComparison.Ordinal)
                    || key.EndsWith("后重置", StringComparison.Ordinal)
                    || key.EndsWith("後重置", StringComparison.Ordinal)
                    || (key.EndsWith("重置", StringComparison.Ordinal)
                        && TimeValue.IsMatch(value.Substring(0, value.Length - 2).Trim()))
                    || ((key.StartsWith("将在", StringComparison.Ordinal) || key.StartsWith("將在", StringComparison.Ordinal)
                        || key.StartsWith("将于", StringComparison.Ordinal) || key.StartsWith("將於", StringComparison.Ordinal)
                        || key.StartsWith("于", StringComparison.Ordinal) || key.StartsWith("於", StringComparison.Ordinal))
                        && key.IndexOf("重置", StringComparison.Ordinal) >= 0);
                if (resetLine) { reset = true; Append(value); return true; }
                if (Percentage.IsMatch(value) || TimeValue.IsMatch(value) || DurationValue.IsMatch(value) || IsoValue.IsMatch(value)) {
                    Append(value);
                    return true;
                }
                // An unknown section after All models must not inherit that scope.
                // Keep only already-observed values and stop at the unknown marker.
                if (weeklyAllScope) ended = true;
                return true;
            }

            private static bool IsOneOf(string value, params string[] choices) {
                foreach (string choice in choices) if (value == choice) return true;
                return false;
            }

            private void Append(string value) {
                // Identical values in two different limit sections are meaningful.
                if (lines.Count == 0 || !String.Equals(lines[lines.Count - 1], value, StringComparison.Ordinal)) lines.Add(value);
            }

            internal string Finish() {
                // A bare reset warning or a chat quoting a time is not a usage page.
                // A section outside the viewport must not invalidate the visible one.
                bool confirmedUsage = (usageHeading && (session || weekly)) || (session && weekly);
                return confirmedUsage && reset ? String.Join("\n", lines.ToArray()) : "";
            }
        }

        internal static bool Run(string[] args) {
            if (args == null || args.Length == 0 || args[0] != "--read-reset-window") return false;
            ResetWindowResult result;
            long handle, ticks;
            int pid;
            if (args.Length != 4
                || !Int64.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out handle)
                || !Int32.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out pid)
                || !Int64.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)
                || handle == 0 || pid <= 0 || ticks <= 0) result = Result("wrong_window", "");
            else result = Read(handle, pid, ticks);
            // Output travels only through the parent's redirected pipe. No files or
            // logs are created, and no exception details or window titles are emitted.
            using (var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))) {
                output.WriteLine(new JavaScriptSerializer().Serialize(result));
            }
            return true;
        }
    }
}
