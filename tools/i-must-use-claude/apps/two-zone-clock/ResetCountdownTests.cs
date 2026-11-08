using System;
using System.Globalization;
using System.Web.Script.Serialization;

namespace TwoZoneClock {
    // Frozen fixtures only: no real Claude data, configuration, account, or local clock.
    internal static class ResetCountdownTests {
        private static int checks;
        private static readonly DateTimeOffset Observed = At("2026-11-01T12:00:00Z");
        private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
        private static readonly TimeZoneInfo Singapore = TimeZoneInfo.CreateCustomTimeZone("Test +08:00", TimeSpan.FromHours(8), "Test +08:00", "Test +08:00");

        private static DateTimeOffset At(string value) {
            return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
        }

        private static void Assert(bool condition, string description) {
            if (!condition) throw new Exception("Reset countdown: " + description);
            checks++;
        }

        private static void Equal(string actual, string expected, string description) {
            Assert(actual == expected, description + "; expected " + expected + ", got " + actual);
        }

        private static void SameInstant(string actual, string expected, string description) {
            DateTimeOffset? parsed = ResetCountdown.ParseUtc(actual);
            Assert(parsed.HasValue && parsed.Value == At(expected), description);
        }

        private static void Reject(Action action, string description) {
            try { action(); }
            catch (FormatException error) {
                Assert(!String.IsNullOrWhiteSpace(error.Message), description + " must have a useful error");
                Assert(!error.Message.Contains("SENSITIVE_FIXTURE"), description + " must not echo the source");
                return;
            }
            throw new Exception("Reset countdown: expected FormatException: " + description);
        }

        private static ResetSnapshot Usage(string text) {
            return ResetCountdown.ParseUsageText(text, Observed, Utc);
        }

        private static void CheckUtcAndCountdown() {
            Assert(!ResetCountdown.ParseUtc(null).HasValue, "null timestamp is unknown");
            Assert(!ResetCountdown.ParseUtc(" ").HasValue, "empty timestamp is unknown");
            Assert(!ResetCountdown.ParseUtc("2026-11-01T12:00:00").HasValue, "offsetless ISO timestamp is rejected");
            Assert(!ResetCountdown.ParseUtc("11/01/2026 12:00 PM").HasValue, "culture-dependent timestamp is rejected");
            Assert(!ResetCountdown.ParseUtc("2026-11-01T12:00:00+99:00").HasValue, "invalid offset is rejected");
            Assert(!ResetCountdown.ParseUtc(new string('x', 100)).HasValue, "oversized timestamp is unknown");
            SameInstant("2026-11-01T20:00:00+08:00", "2026-11-01T12:00:00Z", "explicit offset is normalized");
            SameInstant("2026-11-01T12:00:00.125Z", "2026-11-01T12:00:00.125Z", "fractional seconds survive");
            Equal(ResetCountdown.Remaining(null, Observed, true), "Not set", "unset English label");
            Equal(ResetCountdown.Remaining("invalid", Observed, false), "未设置", "invalid Chinese label");
            Equal(ResetCountdown.Remaining("2026-11-01T11:00:00Z", Observed, true), "Reset time reached", "expired deadline is not availability");
            Equal(ResetCountdown.Remaining("2026-11-01T12:00:00Z", Observed, false), "已到重置时间", "equal deadline is reached");
            Equal(ResetCountdown.Remaining("2026-11-01T14:30:01Z", Observed, true), "02:30:01", "countdown includes seconds");
            Equal(ResetCountdown.Remaining("2026-11-03T15:04:05Z", Observed, true), "2d 03:04:05", "countdown includes days");
            Equal(ResetCountdown.Remaining("2026-11-03T15:04:05Z", Observed, false), "2天 03:04:05", "Chinese countdown includes days");
            Equal(ResetCountdown.Remaining("2026-11-01T12:00:00.1Z", Observed, true), "00:00:01", "positive subsecond time rounds up");
        }

        private static void CheckValidation() {
            Reject(delegate { ResetCountdown.Validate(null); }, "null snapshot");
            var empty = ResetCountdown.Validate(new ResetSnapshot());
            Assert(empty.SessionResetUtc == null && empty.WeeklyResetUtc == null && empty.ObservedUtc == null, "clear snapshot remains empty");
            Assert(ResetCountdown.Validate(new ResetSnapshot(), Observed).ObservedUtc == null, "clear snapshot works with current-time validation");
            var source = new ResetSnapshot { SessionResetUtc = "2026-11-01T22:00:00+08:00", WeeklyResetUtc = "2026-10-26T12:00:00Z", ObservedUtc = "2026-11-01T20:00:00+08:00", Source = " Manual ", Estimated = true };
            var normalized = ResetCountdown.Validate(source, Observed);
            Assert(!Object.ReferenceEquals(source, normalized), "validation makes a copy");
            Assert(source.SessionResetUtc.EndsWith("+08:00", StringComparison.Ordinal), "validation does not mutate input");
            Assert(normalized.SessionResetUtc.EndsWith("Z", StringComparison.Ordinal), "stored reset uses UTC");
            Assert(normalized.ObservedUtc.EndsWith("Z", StringComparison.Ordinal), "stored observation uses UTC");
            Equal(normalized.Source, "Manual", "source label is normalized");
            Assert(normalized.Estimated, "estimated flag survives validation");
            Equal(ResetCountdown.Remaining(normalized.WeeklyResetUtc, Observed, true), "Reset time reached", "valid old reset stays expired");
            var restored = new JavaScriptSerializer().Deserialize<ResetSnapshot>(new JavaScriptSerializer().Serialize(normalized));
            Assert(restored.SessionResetUtc == normalized.SessionResetUtc && restored.Estimated, "timestamp and estimate flag roundtrip");
            Reject(delegate { ResetCountdown.Validate(new ResetSnapshot { SessionResetUtc = "2026-11-01T13:00:00Z" }); }, "deadline without observation");
            Reject(delegate { ResetCountdown.Validate(new ResetSnapshot { ObservedUtc = "bad SENSITIVE_FIXTURE" }); }, "malformed observation");
            Reject(delegate { ResetCountdown.Validate(new ResetSnapshot { ObservedUtc = "2026-11-01T12:00:00Z", SessionResetUtc = "2026-11-10T12:00:00Z" }); }, "far future reset");
            Reject(delegate { ResetCountdown.Validate(new ResetSnapshot { ObservedUtc = "2026-11-01T12:00:00Z", SessionResetUtc = "2026-10-23T12:00:00Z" }); }, "far past reset");
            Reject(delegate { ResetCountdown.Validate(new ResetSnapshot { ObservedUtc = "2026-11-01T12:00:00Z", Source = "bad\nSENSITIVE_FIXTURE" }); }, "source control character");
            Reject(delegate { ResetCountdown.Validate(new ResetSnapshot { ObservedUtc = "2026-11-01T13:00:00Z" }, Observed); }, "future observation");
        }

        private static void CheckUsageSections() {
            var both = Usage("Usage\nCurrent session\nResets in 2 hr 30 min\n15% used\nWeekly limits\nAll models\nResets Fri 7:00 PM\n28% used\nSonnet only\nResets Mon 8:00 AM\nExtra usage\nResets in 99 days");
            SameInstant(both.SessionResetUtc, "2026-11-01T14:30:00Z", "English session duration");
            SameInstant(both.WeeklyResetUtc, "2026-11-06T19:00:00Z", "English all-model weekly reset");
            SameInstant(both.ObservedUtc, "2026-11-01T12:00:00Z", "observation is preserved");
            Assert(both.Estimated, "usage-page reset is an estimate");
            Equal(both.Source, "Usage text", "usage provenance contains no raw input");
            var chinese = Usage("当前会话\n2小时30分钟后重置\n每周限额\n所有模型\n将于周五 晚上7:00重置\n仅限Sonnet\n将于周一 上午8:00重置");
            SameInstant(chinese.SessionResetUtc, "2026-11-01T14:30:00Z", "Chinese session duration");
            SameInstant(chinese.WeeklyResetUtc, "2026-11-06T19:00:00Z", "Chinese all-model weekly reset");
            var compact = Usage("Current session: Resets in 1 hr\nWeekly limits: All models: Resets Fri 7:00 PM");
            SameInstant(compact.SessionResetUtc, "2026-11-01T13:00:00Z", "inline session heading");
            SameInstant(compact.WeeklyResetUtc, "2026-11-06T19:00:00Z", "inline weekly heading");
            var split = Usage("Current session\nResets in\n\n2 hr 30 min");
            SameInstant(split.SessionResetUtc, "2026-11-01T14:30:00Z", "split accessibility caption");
            Assert(split.WeeklyResetUtc == null, "missing weekly limit remains unknown");
            var weeklyOnly = Usage("Weekly limits\nAll models\nResets Fri 7:00 PM");
            Assert(weeklyOnly.SessionResetUtc == null, "missing session limit remains unknown");
            foreach (string alias in new string[] { "本次会话", "本轮额度", "当前时段" }) {
                SameInstant(Usage(alias + "\n2小时后重置").SessionResetUtc, "2026-11-01T14:00:00Z", "recognized Chinese session alias");
            }
            SameInstant(Usage("周用量限制\n所有模型\nResets Fri 7:00 PM").WeeklyResetUtc, "2026-11-06T19:00:00Z", "recognized Chinese weekly alias");
            var sonnetOnly = Usage("Current session\nResets in 1 hr\nWeekly limits\nSonnet only\nResets Fri 7:00 PM");
            Assert(sonnetOnly.WeeklyResetUtc == null, "Sonnet-only limit never supplies all-model weekly reset");
            var opus = Usage("Weekly limits\nAll models\nResets Fri 7:00 PM\nOpus 4.5\nResets Thu 9:00 PM");
            SameInstant(opus.WeeklyResetUtc, "2026-11-06T19:00:00Z", "Opus row does not change all-model weekly reset");
            foreach (string modelHeading in new string[] { "Sonnet-only", "Only Sonnet", "Sonnet（专用）", "Opus-only", "Haiku only" }) {
                var modelOnly = Usage("Current session\nResets in 1 hr\nWeekly limits\nAll models\n" + modelHeading + "\nResets Fri 7:00 PM");
                Assert(modelOnly.WeeklyResetUtc == null, "model-only variant never supplies a missing all-model reset");
            }
            foreach (string unknownHeading in new string[] { "Unknown model only", "Future model", "Another limit", "其他模型", "未知额度" }) {
                var ambiguous = Usage("Usage\nCurrent session\nResets in 1 hr\nWeekly limits\nAll models\n" + unknownHeading + "\nResets Fri 7:00 PM");
                Assert(ambiguous.WeeklyResetUtc == null && ambiguous.WeeklyEvidence == null, "unknown headings cannot inherit the all-model scope");
            }
            var afterKnown = Usage("Weekly limits\nAll models\nResets Fri 7:00 PM\nUnknown model only\nResets Thu 8:00 PM");
            SameInstant(afterKnown.WeeklyResetUtc, "2026-11-06T19:00:00Z", "unknown model after a known all-model reset cannot override it");
            var recoveredScope = Usage("Weekly limits\nAll models\nUnknown model only\nResets Thu 8:00 PM\nAll models\nResets Fri 7:00 PM");
            SameInstant(recoveredScope.WeeklyResetUtc, "2026-11-06T19:00:00Z", "an explicit repeated all-model heading restores scope");
            foreach (string indicator in new string[] { "10% used", "10%", "used", "used: 10%", "10% of limit used", "已使用 10%", "10% 已用", "剩余 90%", "10%\nused" }) {
                var usageLabel = Usage("Weekly limits\nAll models\n" + indicator + "\nResets Fri 7:00 PM");
                SameInstant(usageLabel.WeeklyResetUtc, "2026-11-06T19:00:00Z", "common usage indicators preserve the all-model scope");
            }
            var duplicate = Usage("Current session\nResets in 1 hr\nCurrent session\nResets in 1 hr");
            SameInstant(duplicate.SessionResetUtc, "2026-11-01T13:00:00Z", "identical duplicate captions are safe");
            var traditional = Usage("本輪\n將在 2 小時 30 分鐘後重置\n每週限制\n全部模型\n重置時間: 2026-11-06T19:00:00Z");
            SameInstant(traditional.SessionResetUtc, "2026-11-01T14:30:00Z", "traditional Chinese duration");
            SameInstant(traditional.WeeklyResetUtc, "2026-11-06T19:00:00Z", "explicit ISO reset timestamp");
            var zero = Usage("Current session\nResets in 0 min");
            Equal(ResetCountdown.Remaining(zero.SessionResetUtc, Observed, true), "Reset time reached", "zero relative duration remains reached");
            var expired = Usage("Current session\nResets at 2026-11-01T11:00:00Z");
            Equal(ResetCountdown.Remaining(expired.SessionResetUtc, Observed, true), "Reset time reached", "explicit expired reset is not rolled forward");
        }

        private static void CheckBadUsage() {
            string[] bad = {
                null, "", "SENSITIVE_FIXTURE Resets in 2 hr", "Usage\nResets in 2 hr",
                "Current session", "Current session\nNot accessible SENSITIVE_FIXTURE",
                "Weekly limits\nSonnet only\nResets Fri 7:00 PM",
                "Weekly limits\nResets Fri 7:00 PM",
                "Weekly limits\nAll models\nUnknown model only\nResets Fri 7:00 PM",
                "Weekly limits\nAll models\nUnknown model only: Resets Fri 7:00 PM",
                "Weekly limits\nAll models\nResets at 8:00 PM",
                "Weekly limits\nAll models\nResets at 8:00 PM UTC",
                "Current session\nResets", "Current session\nResets in",
                "Current session\nResets in a few hours SENSITIVE_FIXTURE",
                "Current session\nResets in -2 hr", "Current session\nResets in 1.5 hr",
                "Current session\nResets in 1 hr 2 hr", "Current session\nResets in 9 days",
                "Current session\nResets in and 1 hr", "Current session\nResets in 1 hr and",
                "Current session\nResets in 2 hr SENSITIVE_FIXTURE", "Current session\nResets in 2 hr\nResets in 3 hr",
                "Weekly limits\nAll models\nResets Fri 7:00 PM\nAll models\nResets Thu 7:00 PM",
                "Current session\nResets at 25:00", "Current session\nResets at 7:99 PM",
                "Current session\nResets at 0:00 PM", "Current session\nResets at 7:00 PM PST",
                "Current session\nResets at 7:00 PM (America/Chicago)",
                "Current session\nResets at 7:00 PM +15:00", "Current session\nResets Fri 7:00 PM",
                "Current session\nResets at 2026-11-01T13:00:00",
                "Current session\nResets at 2026-11-10T13:00:00Z",
                "Current session\n2小时SENSITIVE_FIXTURE后重置"
            };
            foreach (string value in bad) {
                string fixture = value;
                Reject(delegate { Usage(fixture); }, "unreadable or unsafe usage fixture");
            }
            Reject(delegate { Usage(new string('x', 16001)); }, "oversized usage text");
            Reject(delegate { ResetCountdown.ParseUsageText("Current session\nResets at 8:00 PM", Observed, null); }, "missing display zone");
        }

        private static void CheckCompactUsagePage() {
            const string screenshot = "您的用量 Max\n进度正常。在星期三重置前，您的额度应该还很充裕。\n当前会话\n18:20 重置\n已使用 10%\n本周\n星期三17:00 重置\n已使用 42%\n限额重置\n暂无重置\n您获得后，它会连同到期日期一起显示在此处。\n用量额度";
            var central = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
            var parsed = ResetCountdown.ParseUsageText(screenshot, Observed, central);
            SameInstant(parsed.SessionResetUtc, "2026-11-02T00:20:00Z", "compact Chinese session suffix uses selected Central page zone");
            SameInstant(parsed.WeeklyResetUtc, "2026-11-04T23:00:00Z", "compact Chinese weekly card without All models uses next Wednesday");
            Assert(parsed.Estimated, "compact page remains an estimated page reading");
            var compact = Usage("当前会话\n18:20重置\n本周\n星期三17:00重置");
            SameInstant(compact.SessionResetUtc, "2026-11-01T18:20:00Z", "suffix reset needs no separating space");
            SameInstant(compact.WeeklyResetUtc, "2026-11-04T17:00:00Z", "weekday and time need no separating space");
            var split = Usage("当前会话\n18:20\n重置\n本周\n星期三17:00\n重置");
            SameInstant(split.SessionResetUtc, "2026-11-01T18:20:00Z", "detached Chinese suffix follows a session clock value");
            SameInstant(split.WeeklyResetUtc, "2026-11-04T17:00:00Z", "detached Chinese suffix follows a weekday clock value");
            foreach (string boundary in new[] { "限额重置", "用量额度", "Unknown model", "Sonnet only" }) {
                var scoped = Usage("当前会话\n18:20 重置\n本周\n" + boundary + "\n星期三17:00 重置");
                Assert(scoped.WeeklyResetUtc == null, "compact weekly scope does not cross unrelated/model boundary");
            }
            Reject(delegate { Usage("本周\n17:00 重置"); }, "compact weekly still requires a date or weekday");
            Reject(delegate { Usage("本周\n星期三25:00 重置"); }, "compact weekly rejects invalid clock value");
            Reject(delegate { Usage("每周限额\n星期三17:00 重置"); }, "old weekly section still requires explicit all-model scope");
        }

        private static TimeZoneInfo EasternTestZone() {
            var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday);
            var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday);
            var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2020, 1, 1), new DateTime(2030, 12, 31), TimeSpan.FromHours(1), start, end);
            return TimeZoneInfo.CreateCustomTimeZone("Test Eastern", TimeSpan.FromHours(-5), "Test Eastern", "Test standard", "Test daylight", new TimeZoneInfo.AdjustmentRule[] { rule });
        }

        private static void CheckAbsoluteTimesAndDst() {
            SameInstant(Usage("Current session\nResets at 8:00 PM").SessionResetUtc, "2026-11-01T20:00:00Z", "same-day time-only reset");
            SameInstant(Usage("Current session\nResets at 8:00 AM").SessionResetUtc, "2026-11-02T08:00:00Z", "next-day time-only reset");
            SameInstant(Usage("Current session\nResets at 12:00 AM").SessionResetUtc, "2026-11-02T00:00:00Z", "12 AM means midnight");
            SameInstant(Usage("Current session\nResets at 12:00 PM").SessionResetUtc, "2026-11-01T12:00:00Z", "12 PM means noon and equality does not roll");
            SameInstant(Usage("Weekly limits\nAll models\nResets Sunday 11:00 AM").WeeklyResetUtc, "2026-11-08T11:00:00Z", "weekly earlier weekday time chooses next week once");
            var selected = ResetCountdown.ParseUsageText("Current session\nResets at 9:00 PM", Observed, Singapore);
            SameInstant(selected.SessionResetUtc, "2026-11-01T13:00:00Z", "unzoned text uses explicitly selected display zone");
            var explicitZone = ResetCountdown.ParseUsageText("Current session\nResets at 9:00 PM UTC", Observed, Singapore);
            SameInstant(explicitZone.SessionResetUtc, "2026-11-01T21:00:00Z", "explicit UTC overrides display zone");
            var eastern = EasternTestZone();
            Reject(delegate { ResetCountdown.ParseUsageText("Current session\nResets at 2:30 AM", At("2026-03-08T06:00:00Z"), eastern); }, "DST gap must reject");
            Reject(delegate { ResetCountdown.ParseUsageText("Current session\nResets at 1:30 AM", At("2026-11-01T04:30:00Z"), eastern); }, "DST overlap must reject");
            var explicitOffset = ResetCountdown.ParseUsageText("Current session\nResets at 1:30 AM -04:00", At("2026-11-01T04:30:00Z"), eastern);
            SameInstant(explicitOffset.SessionResetUtc, "2026-11-01T05:30:00Z", "explicit offset resolves overlap without guessing");
            var explicitIso = ResetCountdown.ParseUsageText("Current session\nResets at 2026-11-01T01:30:00-05:00", At("2026-11-01T04:30:00Z"), eastern);
            SameInstant(explicitIso.SessionResetUtc, "2026-11-01T06:30:00Z", "ISO offset resolves overlap without guessing");
            Reject(delegate { ResetCountdown.ParseUsageText("Current session\nResets at 2:00 AM", At("2026-10-31T06:00:01Z"), eastern); }, "time-only next occurrence must be within 24 hours");
            Reject(delegate { ResetCountdown.ParseUsageText("Weekly limits\nAll models\nResets Sun 2:00 AM", At("2026-10-25T06:00:01Z"), eastern); }, "weekday next occurrence must be within seven days");
        }

        private static string Unix(string value) {
            return ((long)(At(value) - At("1970-01-01T00:00:00Z")).TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        private static void CheckStatusline() {
            string session = Unix("2026-11-01T14:30:00Z");
            string weekly = Unix("2026-11-06T19:00:00Z");
            var parsed = ResetCountdown.ParseStatuslineJson("{\"rate_limits\":{\"five_hour\":{\"utilization\":12,\"resets_at\":" + session + "},\"seven_day\":{\"resets_at\":" + weekly + "},\"seven_day_sonnet\":{\"resets_at\":0}},\"irrelevant\":\"SENSITIVE_FIXTURE\"}", Observed);
            SameInstant(parsed.SessionResetUtc, "2026-11-01T14:30:00Z", "official session Unix timestamp");
            SameInstant(parsed.WeeklyResetUtc, "2026-11-06T19:00:00Z", "official all-model weekly Unix timestamp");
            Assert(!parsed.Estimated, "statusline timestamp is not a rounded page estimate");
            Equal(parsed.Source, "Claude Code statusline", "statusline source does not retain unrelated fields");
            Assert(!new JavaScriptSerializer().Serialize(parsed).Contains("SENSITIVE_FIXTURE"), "unrelated JSON fields are discarded");
            foreach (string empty in new string[] { "{}", "{\"rate_limits\":null}", "{\"rate_limits\":{}}", "{\"rate_limits\":{\"five_hour\":null,\"seven_day\":{\"resets_at\":null}}}" }) {
                var unknown = ResetCountdown.ParseStatuslineJson(empty, Observed);
                Assert(unknown.SessionResetUtc == null && unknown.WeeklyResetUtc == null, "absent statusline rate limits remain unknown");
            }
            var one = ResetCountdown.ParseStatuslineJson("{\"rate_limits\":{\"five_hour\":{\"resets_at\":" + session + "}}}", Observed);
            Assert(one.SessionResetUtc != null && one.WeeklyResetUtc == null, "statusline deadlines are independent");
            string[] bad = {
                null, "", "null", "[]", "{", "{\"rate_limits\":3}",
                "{\"rate_limits\":{\"five_hour\":true}}",
                "{\"rate_limits\":{\"five_hour\":{\"resets_at\":\"SENSITIVE_FIXTURE\"}}}",
                "{\"rate_limits\":{\"five_hour\":{\"resets_at\":1.5}}}",
                "{\"rate_limits\":{\"five_hour\":{\"resets_at\":9999999999999999999999999999}}}",
                "{\"rate_limits\":{\"five_hour\":{\"resets_at\":0}}}",
                "{\"rate_limits\":{},\"rate_limits\":{}}",
                "{\"rate_limits\":{},\"\\u0072ate_limits\":{}}",
                "{'rate_limits':{}}", "{rate_limits:{}}", "{\"rate_limits\":{},}",
                "{\"unused\":01}", "{\"unused\":1.}", "{\"unused\":1e}", "{\"unused\":+1}",
                "{\"unused\":\"bad\\xescape\"}", "{\"unused\":[1,]}", "{\"unused\":true}false",
                "{\"rate_limits\":{\"five_hour\":{\"resets_at\":" + session + ",\"resets_at\":" + weekly + "}}}"
            };
            foreach (string value in bad) {
                string fixture = value;
                Reject(delegate { ResetCountdown.ParseStatuslineJson(fixture, Observed); }, "malformed statusline fixture");
            }
            Reject(delegate { ResetCountdown.ParseStatuslineJson(new string(' ', 16000) + "{}", Observed); }, "oversized statusline JSON");
            var expired = ResetCountdown.ParseStatuslineJson("{\"rate_limits\":{\"five_hour\":{\"resets_at\":" + Unix("2026-11-01T11:00:00Z") + "}}}", Observed);
            Equal(ResetCountdown.Remaining(expired.SessionResetUtc, Observed, true), "Reset time reached", "statusline expired reset is not rescheduled");
        }

        private static void CheckPollingMerge() {
            string initialText = "Current session\nResets in 2 hr\n10% used\nWeekly limits\nAll models\nResets in 5 days\n20% used\nSonnet only\nResets Fri 7:00 PM";
            var first = Usage(initialText);
            Assert(first.SessionEvidence != null && first.SessionEvidence.Length == 64 && first.WeeklyEvidence != null && first.WeeklyEvidence.Length == 64, "usage evidence contains per-limit SHA-256 hashes");
            Assert(!first.SessionEvidence.Contains("Resets") && !first.WeeklyEvidence.Contains("Resets"), "evidence contains no raw reset text");
            var copied = ResetCountdown.Validate(first);
            Assert(copied.SessionEvidence == first.SessionEvidence && copied.WeeklyEvidence == first.WeeklyEvidence, "validation preserves memory-only hashes");
            var firstMerge = ResetCountdown.MergeObserved(null, first);
            Assert(firstMerge.SessionResetUtc == first.SessionResetUtc && !Object.ReferenceEquals(firstMerge, first), "first observed merge copies validated timestamps");
            Assert(ResetCountdown.MergeObserved(new ResetSnapshot(), first).WeeklyResetUtc == first.WeeklyResetUtc, "empty prior snapshot accepts first reading");
            string laterText = "Current session\nResets in 2 hr\n11% used\nWeekly limits\nAll models\nResets in 4 days 23 hr 59 min\n21% used\nSonnet only\nResets Thu 6:00 PM";
            var later = ResetCountdown.ParseUsageText(laterText, Observed.AddMinutes(1), Utc);
            var merged = ResetCountdown.MergeObserved(first, later);
            SameInstant(merged.SessionResetUtc, "2026-11-01T14:00:00Z", "changed weekly evidence does not move unchanged rounded session reset");
            SameInstant(merged.WeeklyResetUtc, "2026-11-06T12:00:00Z", "changed weekly duration adopts its newly calculated deadline");
            SameInstant(merged.ObservedUtc, "2026-11-01T12:01:00Z", "merge records the latest observation");
            Assert(merged.SessionEvidence == first.SessionEvidence && merged.WeeklyEvidence == later.WeeklyEvidence, "merged hashes stay independent");
            Assert(later.SessionResetUtc != merged.SessionResetUtc, "merging does not mutate the new reading");
            var cosmetic = ResetCountdown.ParseUsageText("CURRENT SESSION\n  RESETS   IN\n  2   HR\n99% used\nWeekly limits\nAll models\n RESETS  IN  5  DAYS \n88% used\nOnly Sonnet\nResets Thu 1:00 PM", Observed.AddMinutes(1), Utc);
            Assert(cosmetic.SessionEvidence == first.SessionEvidence && cosmetic.WeeklyEvidence == first.WeeklyEvidence, "evidence normalizes case whitespace and detached labels only");
            var cosmeticMerge = ResetCountdown.MergeObserved(first, cosmetic);
            Assert(cosmeticMerge.SessionResetUtc == first.SessionResetUtc && cosmeticMerge.WeeklyResetUtc == first.WeeklyResetUtc, "percent and model-specific changes never move reset times");
            var expiredRead = ResetCountdown.ParseUsageText(initialText, Observed.AddHours(2).AddMinutes(1), Utc);
            var expiredMerge = ResetCountdown.MergeObserved(first, expiredRead);
            Equal(ResetCountdown.Remaining(expiredMerge.SessionResetUtc, Observed.AddHours(2).AddMinutes(1), true), "Reset time reached", "unchanged label at expiry never rolls the session reset forward");
            Assert(expiredMerge.SessionResetUtc == first.SessionResetUtc && expiredMerge.WeeklyResetUtc == first.WeeklyResetUtc, "unchanged labels preserve both timestamps even after session expiry");
            var changed = ResetCountdown.ParseUsageText("Current session\nResets in 3 hr\nWeekly limits\nAll models\nResets in 4 days", Observed.AddMinutes(1), Utc);
            var changedMerge = ResetCountdown.MergeObserved(first, changed);
            Assert(changedMerge.SessionResetUtc == changed.SessionResetUtc && changedMerge.WeeklyResetUtc == changed.WeeklyResetUtc, "changed applicable evidence adopts each new deadline");
            var onlySession = ResetCountdown.ParseUsageText("Current session\nResets in 2 hr", Observed.AddMinutes(1), Utc);
            Reject(delegate { ResetCountdown.MergeObserved(first, onlySession); }, "partial read cannot erase a known weekly deadline");
            var onlyWeekly = ResetCountdown.ParseUsageText("Weekly limits\nAll models\nResets in 5 days", Observed.AddMinutes(1), Utc);
            Reject(delegate { ResetCountdown.MergeObserved(first, onlyWeekly); }, "partial read cannot erase a known session deadline");
            var addsWeekly = ResetCountdown.MergeObserved(onlySession, changed);
            Assert(addsWeekly.WeeklyResetUtc == changed.WeeklyResetUtc, "a newly readable limit can be added");
            var noEvidence = new ResetSnapshot { SessionResetUtc = "2026-11-01T15:00:00+00:00", WeeklyResetUtc = "2026-11-06T12:00:00Z", ObservedUtc = "2026-11-01T12:01:00Z", Source = "Manual", Estimated = false };
            var explicitMerge = ResetCountdown.MergeObserved(first, noEvidence);
            SameInstant(explicitMerge.SessionResetUtc, "2026-11-01T15:00:00Z", "explicit updated timestamps without evidence are adopted");
            Assert(explicitMerge.Source == "Manual" && !explicitMerge.Estimated, "latest source and estimate flag are preserved");
            string serialized = new JavaScriptSerializer().Serialize(first);
            Assert(!serialized.Contains("Evidence") && !serialized.Contains(first.SessionEvidence) && !serialized.Contains(first.WeeklyEvidence), "internal hashes are not public serialized data");
        }

        public static int Run() {
            checks = 0;
            CheckUtcAndCountdown();
            CheckValidation();
            CheckUsageSections();
            CheckBadUsage();
            CheckCompactUsagePage();
            CheckAbsoluteTimesAndDst();
            CheckStatusline();
            CheckPollingMerge();
            return checks;
        }

        public static int Main() {
            try {
                Console.WriteLine("Reset countdown tests passed: " + Run().ToString(CultureInfo.InvariantCulture));
                return 0;
            } catch (Exception error) {
                Console.Error.WriteLine(error.Message);
                return 1;
            }
        }
    }
}
