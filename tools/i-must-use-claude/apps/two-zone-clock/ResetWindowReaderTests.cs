using System;
using System.Globalization;
using System.Windows.Automation;
using TwoZoneClock;

internal static class ResetWindowReaderTests {
    private static void Assert(bool value, string message) {
        if (!value) throw new Exception("Reset reader: " + message);
    }

    internal static void Run() {
        Assert(ResetWindowReader.IsCandidateApp("Claude", "Settings"), "Claude process must be selectable");
        Assert(ResetWindowReader.IsCandidateApp("CHROME", "Claude - Settings"), "named browser tab must be selectable");
        Assert(!ResetWindowReader.IsCandidateApp("chrome", "Another account"), "unrelated browser tab was accepted");
        Assert(!ResetWindowReader.IsCandidateApp("notepad", "Claude"), "unrelated process was accepted");
        Assert(!ResetWindowReader.IsCandidateApp("ClaudeHelper", "Claude"), "process prefix was accepted");
        Assert(!ResetWindowReader.IsCandidateApp("firefox", null), "untitled browser was accepted");
        Assert(ResetWindowReader.ClassifyWindow(true, true, false, true, 42, 100, 42, 100, "Claude", "Settings") == "ok", "valid identity rejected");
        Assert(ResetWindowReader.ClassifyWindow(true, true, false, true, 42, 101, 42, 100, "Claude", "Settings") == "wrong_window", "reused PID accepted");
        Assert(ResetWindowReader.ClassifyWindow(true, true, false, true, 43, 100, 42, 100, "Claude", "Settings") == "wrong_window", "reused handle accepted");
        Assert(ResetWindowReader.ClassifyWindow(true, true, false, true, 42, 100, 42, 100, "chrome", "Other tab") == "wrong_window", "switched browser tab accepted");
        Assert(ResetWindowReader.ClassifyWindow(true, true, true, true, 42, 100, 42, 100, "Claude", "Settings") == "not_visible", "minimized window accepted");
        Assert(ResetWindowReader.ClassifyWindow(true, false, false, true, 42, 100, 42, 100, "Claude", "Settings") == "not_visible", "hidden window accepted");
        Assert(ResetWindowReader.ClassifyWindow(false, true, false, true, 42, 100, 42, 100, "Claude", "Settings") == "window_closed", "closed window accepted");
        Assert(ResetWindowReader.ClassifyWindow(true, true, false, false, 42, 100, 42, 100, "Claude", "Settings") == "wrong_window", "child handle accepted");

        var usage = new ResetWindowReader.UsageTextCollector();
        foreach (string line in new[] { "Private conversation text", "Resets in 1h", "Plan usage limits", "Current session", "25% used", "Resets in 2h 30m", "Weekly limits", "All models", "40% used", "Resets Fri 3:00 PM", "Another private paragraph", "Usage credits", "Resets in 7h" })
            Assert(usage.Add(line), "normal usage scan hit a limit");
        string text = usage.Finish();
        Assert(text.Contains("Resets in 2h 30m") && text.Contains("Resets Fri 3:00 PM"), "usage resets missing");
        Assert(!text.Contains("Private") && !text.Contains("private") && !text.Contains("Resets in 1h") && !text.Contains("Resets in 7h"), "unrelated page text escaped filtering");
        var incomplete = new ResetWindowReader.UsageTextCollector();
        incomplete.Add("Current session"); incomplete.Add("Resets in 2h");
        Assert(incomplete.Finish() == "", "partial context accepted as usage page");
        var sessionOnly = new ResetWindowReader.UsageTextCollector();
        sessionOnly.Add("Usage"); sessionOnly.Add("Current session"); sessionOnly.Add("Resets in 2h");
        Assert(sessionOnly.Finish().Contains("Resets in 2h"), "visible session requires an offscreen weekly section");
        var weeklyOnly = new ResetWindowReader.UsageTextCollector();
        weeklyOnly.Add("用量"); weeklyOnly.Add("每周限制"); weeklyOnly.Add("2天后重置");
        Assert(weeklyOnly.Finish().Contains("2天后重置"), "visible weekly section requires an offscreen session");
        var repeatedTime = new ResetWindowReader.UsageTextCollector();
        repeatedTime.Add("Current session"); repeatedTime.Add("Resets in 2h"); repeatedTime.Add("Weekly limits"); repeatedTime.Add("Resets in 2h");
        Assert(repeatedTime.Finish().Split(new[] { "Resets in 2h" }, StringSplitOptions.None).Length == 3, "identical reset values in separate sections were dropped");
        var oversized = new ResetWindowReader.UsageTextCollector();
        Assert(!oversized.Add(new String('x', ResetWindowReader.MaximumTextCharacters + 1)), "text budget not enforced");
        Assert(!ResetWindowReader.Run(new string[0]), "normal launch was handled by worker mode");
        CheckParserScopeParity();
        CheckCompactAccessibilityFixture();
        CheckInlineResetChildren();
        CheckDropdownUsageHeading();
        Console.WriteLine("ResetWindowReader synthetic identity/filter tests passed.");
    }

    private static void CheckDropdownUsageHeading() {
        var viewport = new System.Windows.Rect(0, 0, 1200, 900);
        var visible = new System.Windows.Rect(100, 100, 200, 30);
        Assert(ResetWindowReader.MayReadHeadingName(ControlType.Button, false, false, visible, viewport), "visible dropdown button cannot supply known usage heading");
        Assert(ResetWindowReader.MayReadHeadingName(ControlType.ComboBox, false, false, visible, viewport), "visible combobox cannot supply known usage heading");
        Assert(!ResetWindowReader.MayReadHeadingName(ControlType.Button, true, false, visible, viewport), "offscreen dropdown heading was accepted");
        Assert(!ResetWindowReader.MayReadHeadingName(ControlType.Button, false, true, visible, viewport), "password dropdown heading was accepted");
        Assert(!ResetWindowReader.MayReadHeadingName(ControlType.Button, false, false, new System.Windows.Rect(5000, 5000, 200, 30), viewport), "dropdown outside selected window was accepted");
        Assert(!ResetWindowReader.MayReadHeadingName(ControlType.Edit, false, false, visible, viewport), "edit control entered heading path");
        Assert(!ResetWindowReader.MayReadHeadingName(ControlType.Document, false, false, visible, viewport), "whole document entered heading path");
        foreach (string heading in new[] { "本周", "本週", "This week", "Current session", "当前会话", "Weekly limits", "All models", "您的用量 Max" })
            Assert(ResetWindowReader.UsageTextCollector.IsUsageHeadingName(heading), "exact dropdown usage heading was rejected: " + heading);
        foreach (string other in new[] { "展开", "收起", "Unknown model", "Open usage settings", "本周 Resets in 2h", "Current session private details", "PRIVATE_BUTTON", "Usage\nCurrent session" })
            Assert(!ResetWindowReader.UsageTextCollector.IsUsageHeadingName(other), "arbitrary button content accepted as usage heading");

        // Captured usage-only shape from the reported failure: the 本周 button
        // was omitted, so a weekday reset incorrectly remained in session scope.
        var missing = new ResetWindowReader.UsageTextCollector();
        foreach (string line in new[] { "您的用量", "当前会话", "18:20 重置", "已使用 21%", "星期三17:00 重置", "已使用 44%" }) missing.Add(line);
        bool rejected = false; try { ParseCollected(missing); } catch (FormatException) { rejected = true; }
        Assert(rejected, "missing weekly heading no longer reproduces invalid session weekday");
        var restored = new ResetWindowReader.UsageTextCollector();
        foreach (string line in new[] { "您的用量", "当前会话", "18:20 重置", "已使用 21%" }) restored.Add(line);
        Assert(restored.AddHeadingBlock("本周"), "dropdown weekly heading exceeded budget");
        Assert(restored.AddHeadingBlock("展开") && restored.AddHeadingBlock("PRIVATE_BUTTON"), "ignored buttons exceeded budget");
        restored.Add("星期三17:00 重置"); restored.Add("已使用 44%");
        var parsed = ParseCollected(restored);
        Assert(ResetCountdown.ParseUtc(parsed.SessionResetUtc).Value == DateTimeOffset.Parse("2026-11-02T00:20:00Z", CultureInfo.InvariantCulture), "dropdown heading changed the session reset");
        Assert(ResetCountdown.ParseUtc(parsed.WeeklyResetUtc).Value == DateTimeOffset.Parse("2026-11-04T23:00:00Z", CultureInfo.InvariantCulture), "dropdown heading did not restore weekly reset");
        Assert(!restored.Finish().Contains("PRIVATE_BUTTON") && !restored.Finish().Contains("展开") && !restored.Ended, "unknown button text escaped filtering or changed scope");
        var noScope = new ResetWindowReader.UsageTextCollector(); noScope.Add("Usage"); noScope.Add("Current session"); noScope.Add("Resets in 1 hr");
        noScope.AddHeadingBlock("本周 Resets in 2h");
        Assert(ParseCollected(noScope).WeeklyResetUtc == null, "unknown compound button created weekly scope");
    }

    private static void AddResetWithInlineChildren(ResetWindowReader.UsageTextCollector collector, string parent, params string[] children) {
        Assert(collector.AddInlineBlock(parent, null), "complete reset parent exceeded scan budget");
        string coverage = collector.AcceptedInlineCoverage(ControlType.Text, parent);
        Assert(coverage != null, "complete reset parent did not cover its inline children");
        foreach (string child in children) Assert(collector.AddInlineBlock(child, coverage), "inline reset child exceeded scan budget");
    }

    private static ResetSnapshot ParseCollected(ResetWindowReader.UsageTextCollector collector) {
        return ResetCountdown.ParseUsageText(collector.Finish(),
            DateTimeOffset.Parse("2026-11-01T12:00:00Z", CultureInfo.InvariantCulture),
            TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"));
    }

    private static void CheckInlineResetChildren() {
        // These are synthetic parent/inline-child shapes, not a capture of any
        // real account. A complete parent followed by a repeated bare caption
        // previously consumed the next section heading as a second reset value.
        var chinese = new ResetWindowReader.UsageTextCollector();
        chinese.Add("您的用量 Max"); chinese.Add("当前会话");
        AddResetWithInlineChildren(chinese, "18:20 重置", "重置");
        chinese.Add("已使用18%"); chinese.Add("本周");
        AddResetWithInlineChildren(chinese, "星期三17:00 重置", "重置");
        chinese.Add("已使用43%");
        var parsed = ParseCollected(chinese);
        Assert(ResetCountdown.ParseUtc(parsed.SessionResetUtc).Value == DateTimeOffset.Parse("2026-11-02T00:20:00Z", CultureInfo.InvariantCulture), "repeated inline Chinese caption broke session time");
        Assert(ResetCountdown.ParseUtc(parsed.WeeklyResetUtc).Value == DateTimeOffset.Parse("2026-11-04T23:00:00Z", CultureInfo.InvariantCulture), "repeated inline Chinese caption broke weekly time");
        var metadata = new ResetWindowResult(); chinese.AddDiagnostics(metadata);
        Assert(metadata.RepeatedInlineNodes == 2 && metadata.HasUsageHeading && metadata.HasSessionHeading && metadata.HasWeeklyHeading && metadata.HasResetLabel, "non-content diagnostic flags disagree with collector");

        // A second partial duration must not contradict its already-complete
        // parent (2 hr instead of 2 hr 30 min), while independent values remain.
        var english = new ResetWindowReader.UsageTextCollector();
        english.Add("Usage"); english.Add("Current session");
        AddResetWithInlineChildren(english, "Resets in 2 hr 30 min", "Resets in", "2 hr", "30 min");
        english.Add("Weekly limits"); english.Add("All models");
        AddResetWithInlineChildren(english, "Resets Wednesday 5:00 PM", "Resets", "Wednesday 5:00 PM");
        parsed = ParseCollected(english);
        Assert(ResetCountdown.ParseUtc(parsed.SessionResetUtc).Value == DateTimeOffset.Parse("2026-11-01T14:30:00Z", CultureInfo.InvariantCulture), "split inline duration contradicted complete parent");
        Assert(ResetCountdown.ParseUtc(parsed.WeeklyResetUtc).Value == DateTimeOffset.Parse("2026-11-04T23:00:00Z", CultureInfo.InvariantCulture), "English weekly inline children changed reset");

        var repeated = new ResetWindowReader.UsageTextCollector();
        repeated.Add("Usage"); repeated.Add("Current session");
        AddResetWithInlineChildren(repeated, "Resets in 2 hr", "Resets in", "2 hr");
        repeated.Add("Weekly limits"); repeated.Add("All models");
        AddResetWithInlineChildren(repeated, "Resets in 2 hr", "Resets in", "2 hr");
        parsed = ParseCollected(repeated);
        Assert(parsed.SessionResetUtc == parsed.WeeklyResetUtc && parsed.SessionResetUtc != null, "same reset in separate limit cards was removed");

        var conflict = new ResetWindowReader.UsageTextCollector();
        conflict.Add("Usage"); conflict.Add("Current session");
        AddResetWithInlineChildren(conflict, "18:20 重置", "19:20 重置");
        bool rejected = false; try { ParseCollected(conflict); } catch (FormatException) { rejected = true; }
        Assert(rejected, "different descendant reset was incorrectly treated as duplicate");

        foreach (ControlType type in new[] { ControlType.Document, ControlType.Group, ControlType.Pane, ControlType.Custom, ControlType.Edit })
            Assert(ResetWindowReader.InlineResetCoverage(type, "18:20 重置") == null, "non-Text ancestor supplied duplicate coverage");
        foreach (string name in new[] { "重置", "Resets in", "18:20", "Current session", "Private sentence with 18:20 重置", "Usage\nCurrent session\n18:20 重置" })
            Assert(ResetWindowReader.InlineResetCoverage(ControlType.Text, name) == null, "partial or whole-page parent supplied duplicate coverage");

        var unrecognizedParent = new ResetWindowReader.UsageTextCollector(); unrecognizedParent.Add("Usage");
        unrecognizedParent.AddInlineBlock("当前会话18:20重置", null);
        string noCoverage = unrecognizedParent.AcceptedInlineCoverage(ControlType.Text, "当前会话18:20重置");
        Assert(noCoverage == null, "unaccepted compound parent supplied inline coverage");
        unrecognizedParent.AddInlineBlock("当前会话", noCoverage); unrecognizedParent.AddInlineBlock("18:20 重置", noCoverage);
        Assert(ParseCollected(unrecognizedParent).SessionResetUtc != null, "unrecognized parent swallowed readable child labels");
        var beforeContext = new ResetWindowReader.UsageTextCollector(); beforeContext.AddInlineBlock("18:20 重置", null);
        Assert(beforeContext.AcceptedInlineCoverage(ControlType.Text, "18:20 重置") == null, "ignored value before usage context supplied coverage");

        // Do not globally swallow an orphan caption: only the structural inline
        // duplicate above is eligible, and every duplicate still costs budget.
        var orphan = new ResetWindowReader.UsageTextCollector(); orphan.Add("Usage"); orphan.Add("Current session"); orphan.Add("18:20 重置"); orphan.Add("重置"); orphan.Add("本周");
        rejected = false; try { ParseCollected(orphan); } catch (FormatException) { rejected = true; }
        Assert(rejected, "unrelated orphan reset caption was silently discarded");
        var budget = new ResetWindowReader.UsageTextCollector();
        string cover = ResetWindowReader.InlineResetCoverage(ControlType.Text, "18:20 重置");
        Assert(budget.Add(new String('x', ResetWindowReader.MaximumTextCharacters - 2)), "budget fixture rejected allowed prefix");
        Assert(budget.AddInlineBlock("重置", cover) && !budget.AddInlineBlock("重置", cover), "repeated inline nodes bypass text budget");
    }

    private static void CheckCompactAccessibilityFixture() {
        var viewport = new System.Windows.Rect(100, 100, 1000, 800);
        var visible = new System.Windows.Rect(200, 180, 180, 24);
        Assert(ResetWindowReader.MayReadName(ControlType.Text, true, false, false, visible, viewport), "Text with inline children must remain readable");
        Assert(ResetWindowReader.MayReadName(ControlType.Custom, false, false, false, visible, viewport), "raw-view static custom leaf must be readable");
        Assert(ResetWindowReader.MayReadName(ControlType.Group, false, false, false, visible, viewport), "static group leaf must be readable");
        Assert(!ResetWindowReader.MayReadName(ControlType.Group, true, false, false, visible, viewport), "whole container name must not be read");
        Assert(!ResetWindowReader.MayReadName(ControlType.Document, false, false, false, visible, viewport), "whole document name must not be read");
        Assert(!ResetWindowReader.MayReadName(ControlType.Edit, false, false, false, visible, viewport), "edit name must not be read");
        Assert(!ResetWindowReader.MayReadName(ControlType.Text, false, false, true, visible, viewport), "password name must not be read");
        Assert(!ResetWindowReader.MayReadName(ControlType.Text, false, true, false, visible, viewport), "offscreen text must not be read");
        Assert(!ResetWindowReader.MayReadName(ControlType.Text, false, false, false, new System.Windows.Rect(5000, 5000, 10, 10), viewport), "text outside selected window must not be read");
        Assert(!ResetWindowReader.MayReadName(ControlType.Text, false, false, false, System.Windows.Rect.Empty, viewport), "unlocated text must not be read");
        // Synthetic accessibility names transcribed from the supplied screenshot;
        // this fixture does not enumerate or inspect any application window.
        var collector = new ResetWindowReader.UsageTextCollector();
        foreach (string name in new[] {
            "PRIVATE_FIXTURE", "您的用量 Max", "进度正常。在星期三重置前，您的额度应该还很充裕。",
            "当前会话\n18:20 重置", "已使用 10%", "本周", "星期三17:00 重置", "已使用 42%",
            "限额重置", "暂无重置", "您获得后，它会连同到期日期一起显示在此处。", "用量额度"
        }) Assert(collector.AddBlock(name), "compact accessibility fixture exceeded budget");
        string filtered = collector.Finish();
        Assert(filtered.Contains("18:20 重置") && filtered.Contains("星期三17:00 重置"), "compact screenshot reset labels were dropped");
        Assert(!filtered.Contains("PRIVATE_FIXTURE") && !filtered.Contains("进度正常") && !filtered.Contains("暂无重置"), "non-limit page content escaped filter");
        Assert(collector.Ended, "other usage section must stop accessibility scan");
        var parsed = ResetCountdown.ParseUsageText(filtered, DateTimeOffset.Parse("2026-11-01T12:00:00Z", CultureInfo.InvariantCulture), TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"));
        Assert(ResetCountdown.ParseUtc(parsed.SessionResetUtc).Value == DateTimeOffset.Parse("2026-11-02T00:20:00Z", CultureInfo.InvariantCulture), "accessibility-to-parser session time disagrees with selected zone");
        Assert(ResetCountdown.ParseUtc(parsed.WeeklyResetUtc).Value == DateTimeOffset.Parse("2026-11-04T23:00:00Z", CultureInfo.InvariantCulture), "accessibility-to-parser weekly time disagrees with selected zone");
        var single = new ResetWindowReader.UsageTextCollector();
        single.Add("您的用量 Max"); single.Add("当前会话"); single.Add("18:20 重置");
        Assert(single.Finish().Contains("18:20"), "compact visible session does not require offscreen weekly card");
        var split = CollectAndParse("您的用量 Max\n当前会话\n18:20\n重置\n本周\n星期三17:00\n重置");
        Assert(split.SessionResetUtc != null && split.WeeklyResetUtc != null, "split suffix accessibility names were lost");
    }

    private static ResetSnapshot CollectAndParse(string text) {
        var collector = new ResetWindowReader.UsageTextCollector();
        foreach (string line in text.Split('\n')) Assert(collector.Add(line), "integration fixture reached text limit");
        return ResetCountdown.ParseUsageText(collector.Finish(),
            DateTimeOffset.Parse("2026-11-01T12:00:00Z", CultureInfo.InvariantCulture), TimeZoneInfo.Utc);
    }

    private static void CheckParserScopeParity() {
        foreach (string model in new[] { "Sonnet", "Only Sonnet", "Sonnet-only", "Claude Sonnet 4.5", "Opus 4.5", "Haiku only", "仅限Sonnet", "僅限 Opus", "Model-specific", "模型專屬" }) {
            var parsed = CollectAndParse("Usage\nCurrent session\nResets in 1 hr\nWeekly limits\nAll models\n" + model + "\nResets Fri 7:00 PM");
            Assert(parsed.SessionResetUtc != null && parsed.WeeklyResetUtc == null, "model reset inherited All models scope: " + model);
            var withGlobal = CollectAndParse("Usage\nCurrent session\nResets in 1 hr\nWeekly limits\nAll models\nResets Thu 6:00 PM\n" + model + "\nResets Fri 7:00 PM");
            Assert(ResetCountdown.ParseUtc(withGlobal.WeeklyResetUtc).Value.Day == 5, "model reset replaced a global weekly reset: " + model);
        }
        var unknown = CollectAndParse("Usage\nCurrent session\nResets in 1 hr\nWeekly limits\nAll models\nUnknown model only\nResets Fri 7:00 PM");
        Assert(unknown.WeeklyResetUtc == null, "unknown model scope inherited All models");
        foreach (string alias in new[] { "本轮额度", "当前时段", "本輪", "當前會話" }) {
            var parsed = CollectAndParse("Usage\n" + alias + "\n將在 2 小時 30 分鐘後重置");
            Assert(ResetCountdown.ParseUtc(parsed.SessionResetUtc).Value.Hour == 14, "Chinese heading was removed: " + alias);
        }
        var split = CollectAndParse("Usage\nCurrent session\nResets\n14:30\nWeekly limits\nAll models\nResets on\nFriday 7:00 PM");
        Assert(ResetCountdown.ParseUtc(split.SessionResetUtc).Value.Hour == 14 && ResetCountdown.ParseUtc(split.WeeklyResetUtc).Value.Day == 6, "split caption/value was removed");
        var splitDuration = CollectAndParse("Usage\nCurrent session\nResets in\n2 hr 30 min");
        Assert(ResetCountdown.ParseUtc(splitDuration.SessionResetUtc).Value.Minute == 30, "split duration was removed");
        var spaced = CollectAndParse("Usage\nCurrent\u00a0session\nResets\u202fin\u00a02 hr 30 min");
        Assert(ResetCountdown.ParseUtc(spaced.SessionResetUtc).Value.Minute == 30, "browser nonbreaking spaces broke reset detection");
        var chinese = CollectAndParse("当前会话\n2小时30分钟后重置\n每周限额\n所有模型\n将于周五 晚上7:00重置\n仅限Sonnet\n将于周一 上午8:00重置");
        Assert(ResetCountdown.ParseUtc(chinese.WeeklyResetUtc).Value.Day == 6, "Chinese scope/time was removed");
    }
}
