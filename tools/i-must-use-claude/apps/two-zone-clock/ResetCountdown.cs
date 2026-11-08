using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace TwoZoneClock {
    // Only reset timestamps and provenance are saved, never the pasted usage text.
    public sealed class ResetSnapshot {
        public string SessionResetUtc;
        public string WeeklyResetUtc;
        public string ObservedUtc;
        public string Source;
        public bool Estimated;
        // Memory-only fingerprints. XmlSerializer does not serialize internal fields.
        internal string SessionEvidence;
        internal string WeeklyEvidence;
    }

    public static class ResetCountdown {
        private const int MaximumInputLength = 16000;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly TimeSpan MaximumResetDistance = TimeSpan.FromDays(8);
        private static readonly DateTimeOffset UnixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly Regex IsoTimestamp = Pattern(@"^\d{4}-\d{2}-\d{2}[Tt]\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:[Zz]|[+-]\d{2}:\d{2})$");
        private static readonly Regex SessionHeading = Pattern(@"^(?:Current\s+session|当前会话|當前會話|本次会话|本次會話|当前时段|當前時段|本轮额度|本輪額度|本轮|本輪)(?=$|\s|[:：])\s*[:：]?\s*(.*)$");
        private static readonly Regex WeeklyHeading = Pattern(@"^(?:Weekly\s+limits?|每周限额|每週限額|每周限制|每週限制|每?周用量限制|每?週用量限制)(?=$|\s|[:：])\s*[:：]?\s*(.*)$");
        // The compact Usage page labels its aggregate weekly card simply 本周.
        // Other weekly layouts still need their explicit All models subheading.
        private static readonly Regex CompactWeeklyHeading = Pattern(@"^(?:本周|本週|This\s+week)(?=$|\s|[:：])\s*[:：]?\s*(.*)$");
        private static readonly Regex AllModelsHeading = Pattern(@"^(?:All\s+models|所有模型|全部模型)(?=$|\s|[:：])\s*[:：]?\s*(.*)$");
        private static readonly Regex ModelHeading = Pattern(@"^(?:(?:(?:Only|仅限?|僅限?|只有)\s*)?(?:Claude\s+)?(?:Sonnet|Opus|Haiku)(?![a-z])|模型专属|模型專屬|Model[- ]specific\b)");
        private static readonly Regex OtherHeading = Pattern(@"^(?:Extra\s+usage|Additional\s+usage|Usage\s+credits|额外用量|額外用量|额外使用量|額外使用量|额外使用|額外使用|限额重置|限額重置|用量额度|用量額度)(?=$|\s|[:：])");
        private static readonly Regex ResetWord = Pattern(@"\bResets?\b|重置");
        private static readonly Regex UsageIndicator = Pattern(@"^(?:\d{1,3}(?:\.\d{1,2})?\s*%\s*(?:(?:of\s+(?:the\s+)?limit\s+)?(?:used|remaining)|已使用|已用|已用量|使用量|剩余|剩餘)?|(?:used|remaining|已使用|已用|已用量|使用量|剩余|剩餘)(?:\s*:?\s*\d{1,3}(?:\.\d{1,2})?\s*%)?|%)$");
        private static readonly Regex EnglishDuration = Pattern(@"(?<number>\d+)\s*(?<unit>days?|d|hours?|hrs?|hr|h|minutes?|mins?|min|m|seconds?|secs?|sec|s)(?![a-z])");
        private static readonly Regex ChineseDuration = Pattern(@"(?<number>\d+)\s*(?<unit>天|小时|小時|分钟|分鐘|秒钟|秒鐘|秒)");
        private static readonly Regex LocalResetTime = Pattern(@"^(?:(?<weekday>Sun(?:day)?|Mon(?:day)?|Tue(?:sday)?|Wed(?:nesday)?|Thu(?:rsday)?|Fri(?:day)?|Sat(?:urday)?|(?:周|週|星期)[一二三四五六日天])\s*[,]?\s*)?(?:(?<period>上午|下午|晚上|中午|凌晨)\s*)?(?<hour>\d{1,2}):(?<minute>\d{2})(?::(?<second>\d{2}))?\s*(?<ampm>AM|PM)?(?:\s*(?<zone>UTC|GMT|[+-]\d{2}:\d{2}))?$");

        private static Regex Pattern(string pattern) {
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        }

        private static string UtcString(DateTimeOffset value) {
            return value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", Invariant);
        }

        public static DateTimeOffset? ParseUtc(string value) {
            if (String.IsNullOrWhiteSpace(value) || value.Length > 64) return null;
            string trimmed = value.Trim();
            DateTimeOffset parsed;
            if (!IsoTimestamp.IsMatch(trimmed) || !DateTimeOffset.TryParse(trimmed, Invariant, DateTimeStyles.None, out parsed)) return null;
            return parsed.ToUniversalTime();
        }

        public static string Remaining(string utc, DateTimeOffset now, bool english) {
            DateTimeOffset? reset = ParseUtc(utc);
            if (!reset.HasValue) return english ? "Not set" : "未设置";
            TimeSpan remaining = reset.Value - now;
            if (remaining <= TimeSpan.Zero) return english ? "Reset time reached" : "已到重置时间";
            // Round up so a positive subsecond remainder never looks expired.
            long seconds = (long)Math.Ceiling(remaining.TotalSeconds);
            long days = seconds / 86400;
            string clock = String.Format(Invariant, "{0:00}:{1:00}:{2:00}", (seconds / 3600) % 24, (seconds / 60) % 60, seconds % 60);
            return days == 0 ? clock : days.ToString(Invariant) + (english ? "d " : "天 ") + clock;
        }

        public static ResetSnapshot Validate(ResetSnapshot snapshot) {
            if (snapshot == null) throw new FormatException("No reset snapshot was provided.");
            if (String.IsNullOrWhiteSpace(snapshot.SessionResetUtc) && String.IsNullOrWhiteSpace(snapshot.WeeklyResetUtc) && String.IsNullOrWhiteSpace(snapshot.ObservedUtc))
                return new ResetSnapshot { Estimated = snapshot.Estimated, SessionEvidence = snapshot.SessionEvidence, WeeklyEvidence = snapshot.WeeklyEvidence };
            DateTimeOffset? observed = ParseUtc(snapshot.ObservedUtc);
            if (!observed.HasValue) throw new FormatException("The observation time must be an ISO 8601 timestamp with a UTC offset.");
            string session = ValidateReset(snapshot.SessionResetUtc, observed.Value);
            string weekly = ValidateReset(snapshot.WeeklyResetUtc, observed.Value);
            string source = String.IsNullOrWhiteSpace(snapshot.Source) ? "Manual" : snapshot.Source.Trim();
            if (source.Length > 120) throw new FormatException("The reset source label is too long.");
            foreach (char character in source) {
                if (Char.IsControl(character)) throw new FormatException("The reset source label is invalid.");
            }
            return new ResetSnapshot {
                SessionResetUtc = session,
                WeeklyResetUtc = weekly,
                ObservedUtc = UtcString(observed.Value),
                Source = source,
                Estimated = snapshot.Estimated,
                SessionEvidence = snapshot.SessionEvidence,
                WeeklyEvidence = snapshot.WeeklyEvidence
            };
        }

        public static ResetSnapshot MergeObserved(ResetSnapshot previous, ResetSnapshot updated) {
            ResetSnapshot next = Validate(updated);
            if (previous == null) return next;
            ResetSnapshot prior = Validate(previous);
            if (prior.SessionResetUtc != null && next.SessionResetUtc == null || prior.WeeklyResetUtc != null && next.WeeklyResetUtc == null)
                throw new FormatException("The latest usage read is missing a previously known reset. Read both applicable limits before updating.");
            // Each label is independent: changing weekly text must not move a rounded session reset.
            if (prior.SessionResetUtc != null && !String.IsNullOrEmpty(prior.SessionEvidence) && String.Equals(prior.SessionEvidence, next.SessionEvidence, StringComparison.Ordinal))
                next.SessionResetUtc = prior.SessionResetUtc;
            if (prior.WeeklyResetUtc != null && !String.IsNullOrEmpty(prior.WeeklyEvidence) && String.Equals(prior.WeeklyEvidence, next.WeeklyEvidence, StringComparison.Ordinal))
                next.WeeklyResetUtc = prior.WeeklyResetUtc;
            return Validate(next);
        }

        public static ResetSnapshot Validate(ResetSnapshot snapshot, DateTimeOffset now) {
            ResetSnapshot result = Validate(snapshot);
            DateTimeOffset? observed = ParseUtc(result.ObservedUtc);
            if (observed.HasValue && observed.Value - now > TimeSpan.FromMinutes(5))
                throw new FormatException("The observation time is in the future. Check the selected time and time zone.");
            return result;
        }

        private static string ValidateReset(string text, DateTimeOffset observed) {
            if (String.IsNullOrWhiteSpace(text)) return null;
            DateTimeOffset? reset = ParseUtc(text);
            if (!reset.HasValue) throw new FormatException("A reset time must be an ISO 8601 timestamp with a UTC offset.");
            TimeSpan distance = reset.Value - observed;
            if (distance > MaximumResetDistance || distance < -MaximumResetDistance)
                throw new FormatException("A reset time is more than eight days from its observation time.");
            return UtcString(reset.Value);
        }

        public static ResetSnapshot ParseUsageText(string text, DateTimeOffset observed, TimeZoneInfo displayZone) {
            CheckInput(text, "Usage text");
            if (displayZone == null) throw new FormatException("Select the time zone used by the usage page.");
            try {
                return ParseUsageTextCore(text, observed, displayZone);
            } catch (RegexMatchTimeoutException) {
                throw new FormatException("The usage text could not be safely parsed.");
            } catch (ArgumentOutOfRangeException) {
                throw new FormatException("A reset time is outside the supported date range.");
            }
        }

        private static ResetSnapshot ParseUsageTextCore(string text, DateTimeOffset observed, TimeZoneInfo displayZone) {
            string normalized = text.Replace('\u00a0', ' ').Replace('\u202f', ' ').Replace('\u200e', ' ').Replace('\u200f', ' ').Replace('：', ':');
            string[] lines = normalized.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            int section = 0; // 1: current session; 2: weekly limits.
            int weeklyScope = 0; // Explicit All models, or the compact 本周 card.
            bool sawHeading = false;
            DateTimeOffset? session = null;
            DateTimeOffset? weekly = null;
            string sessionEvidence = null;
            string weeklyEvidence = null;
            for (int index = 0; index < lines.Length; index++) {
                string line = lines[index].Trim();
                if (line.Length == 0) continue;
                Match heading = SessionHeading.Match(line);
                if (heading.Success) {
                    section = 1;
                    weeklyScope = 0;
                    sawHeading = true;
                    line = heading.Groups[1].Value.Trim();
                } else {
                    heading = CompactWeeklyHeading.Match(line);
                    bool compactWeekly = heading.Success;
                    if (!compactWeekly) heading = WeeklyHeading.Match(line);
                    if (heading.Success) {
                        section = 2;
                        weeklyScope = compactWeekly ? 1 : 0;
                        sawHeading = true;
                        line = heading.Groups[1].Value.Trim();
                    }
                }
                if (OtherHeading.IsMatch(line)) { section = 0; weeklyScope = 0; continue; }
                // Some accessibility providers split the suffix caption from its
                // visible time. Join only a recognized clock value and 重置.
                if ((section == 1 || section == 2 && weeklyScope == 1) && LocalResetTime.IsMatch(line)) {
                    int next = index + 1;
                    while (next < lines.Length && String.IsNullOrWhiteSpace(lines[next])) next++;
                    if (next < lines.Length && lines[next].Trim() == "重置") {
                        line += " 重置";
                        index = next;
                    }
                }
                if (section == 2) {
                    Match allModels = AllModelsHeading.Match(line);
                    if (allModels.Success) {
                        weeklyScope = 1;
                        line = allModels.Groups[1].Value.Trim();
                    } else if (ModelHeading.IsMatch(line)) {
                        weeklyScope = 2;
                        continue;
                    }
                    // A pasted page can contain new model names or unrelated limit headings.
                    // Only known usage indicators may intervene before an all-model reset.
                    if (weeklyScope == 1 && line.Length > 0 && !ResetWord.IsMatch(line) && !UsageIndicator.IsMatch(line)) {
                        weeklyScope = 2;
                        continue;
                    }
                }
                if (section == 0 || (section == 2 && weeklyScope != 1) || !ResetWord.IsMatch(line)) continue;
                // Accessibility text may expose the caption and value as separate nodes.
                if (Pattern(@"^(?:Resets?(?:\s+(?:in|at|on))?|重置(?:时间|時間)?\s*:?)$").IsMatch(line)) {
                    int next = index + 1;
                    while (next < lines.Length && String.IsNullOrWhiteSpace(lines[next])) next++;
                    if (next >= lines.Length) throw new FormatException("A reset caption has no readable time.");
                    line = line + " " + lines[next].Trim();
                    index = next;
                }
                DateTimeOffset candidate = ParseResetLine(line, observed, displayZone, section == 2);
                string evidence = EvidenceSignature(line);
                if (section == 1) AddCandidate(ref session, ref sessionEvidence, candidate, evidence);
                else AddCandidate(ref weekly, ref weeklyEvidence, candidate, evidence);
            }
            if (!sawHeading) throw new FormatException("Usage text must contain a Current session or Weekly limits heading.");
            if (!session.HasValue && !weekly.HasValue)
                throw new FormatException("No readable session or all-model weekly reset was found. Copy the usage headings and reset times.");
            return Validate(new ResetSnapshot {
                SessionResetUtc = session.HasValue ? UtcString(session.Value) : null,
                WeeklyResetUtc = weekly.HasValue ? UtcString(weekly.Value) : null,
                ObservedUtc = UtcString(observed),
                Source = "Usage text",
                Estimated = true,
                SessionEvidence = sessionEvidence,
                WeeklyEvidence = weeklyEvidence
            });
        }

        private static string EvidenceSignature(string resetLine) {
            string canonical = String.Join(" ", resetLine.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
            using (SHA256 sha = SHA256.Create()) {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var signature = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) signature.Append(value.ToString("x2", Invariant));
                return signature.ToString();
            }
        }

        private static void AddCandidate(ref DateTimeOffset? existing, ref string existingEvidence, DateTimeOffset candidate, string evidence) {
            if (existing.HasValue && existing.Value != candidate)
                throw new FormatException("Conflicting reset times were found for the same limit. Copy only one current usage page.");
            if (!existing.HasValue) existingEvidence = evidence;
            existing = candidate;
        }

        private static DateTimeOffset ParseResetLine(string line, DateTimeOffset observed, TimeZoneInfo displayZone, bool weekly) {
            Match relative = Pattern(@"^Resets?\s+in\s+(.+)$").Match(line);
            if (relative.Success) return observed.Add(ParseDuration(relative.Groups[1].Value, false));
            relative = Pattern(@"^(?:将在|將在)?\s*(.+?)\s*(?:后|後)\s*重置$").Match(line);
            if (relative.Success) return observed.Add(ParseDuration(relative.Groups[1].Value, true));
            string absolute = line;
            Match prefix = Pattern(@"^Resets?\s+(?:(?:at|on)\s+)?(.+)$").Match(line);
            if (prefix.Success) absolute = prefix.Groups[1].Value.Trim();
            else {
                prefix = Pattern(@"^(?:重置(?:时间|時間)?\s*:?\s*(?:于|於)?\s*|(?:将于|將於|于|於)\s*)(.+?)(?:\s*重置)?$").Match(line);
                if (!prefix.Success) prefix = Pattern(@"^(.+?)\s*重置$").Match(line);
                if (!prefix.Success) throw new FormatException("A reset time has an unsupported or unreadable format.");
                absolute = prefix.Groups[1].Value.Trim();
            }
            DateTimeOffset? explicitUtc = ParseUtc(absolute);
            if (explicitUtc.HasValue) return explicitUtc.Value;
            Match local = LocalResetTime.Match(absolute);
            if (!local.Success) throw new FormatException("A reset time has an unsupported format or time zone. Use a displayed time or an ISO 8601 timestamp with an offset.");
            int hour = Int32.Parse(local.Groups["hour"].Value, Invariant);
            int minute = Int32.Parse(local.Groups["minute"].Value, Invariant);
            int second = local.Groups["second"].Success ? Int32.Parse(local.Groups["second"].Value, Invariant) : 0;
            string period = local.Groups["period"].Value;
            string ampm = local.Groups["ampm"].Value;
            if (period.Length > 0 && ampm.Length > 0) throw new FormatException("A reset time has conflicting AM/PM indicators.");
            bool hasPeriod = period.Length > 0 || ampm.Length > 0;
            if (minute > 59 || second > 59 || hour > 23 || (hasPeriod && (hour < 1 || hour > 12)))
                throw new FormatException("A reset time contains an invalid clock time.");
            if (hasPeriod) {
                bool pm = ampm.Equals("PM", StringComparison.OrdinalIgnoreCase) || period == "下午" || period == "晚上" || period == "中午";
                hour = hour % 12 + (pm ? 12 : 0);
            }
            TimeZoneInfo zone = displayZone;
            string zoneText = local.Groups["zone"].Value;
            if (zoneText.Length > 0) {
                TimeSpan offset = TimeSpan.Zero;
                if (zoneText != "UTC" && zoneText != "GMT" && !zoneText.Equals("UTC", StringComparison.OrdinalIgnoreCase) && !zoneText.Equals("GMT", StringComparison.OrdinalIgnoreCase)) {
                    int offsetHours = Int32.Parse(zoneText.Substring(1, 2), Invariant);
                    int offsetMinutes = Int32.Parse(zoneText.Substring(4, 2), Invariant);
                    if (offsetMinutes > 59 || offsetHours > 14 || (offsetHours == 14 && offsetMinutes > 0))
                        throw new FormatException("A reset time contains an invalid UTC offset.");
                    offset = new TimeSpan(offsetHours, offsetMinutes, 0);
                    if (zoneText[0] == '-') offset = -offset;
                }
                zone = TimeZoneInfo.CreateCustomTimeZone("Reset text offset", offset, "Reset text offset", "Reset text offset");
            }
            DateTime localNow = TimeZoneInfo.ConvertTime(observed, zone).DateTime;
            DateTime candidateLocal = DateTime.SpecifyKind(localNow.Date.AddHours(hour).AddMinutes(minute).AddSeconds(second), DateTimeKind.Unspecified);
            bool hasWeekday = local.Groups["weekday"].Success;
            if (weekly && !hasWeekday)
                throw new FormatException("A weekly reset needs a weekday, a relative duration, or an ISO 8601 timestamp. A clock time alone does not identify the reset date.");
            if (hasWeekday) {
                if (!weekly) throw new FormatException("A session reset must not use a weekly weekday schedule.");
                int weekday = ParseWeekday(local.Groups["weekday"].Value);
                int days = (weekday - (int)localNow.DayOfWeek + 7) % 7;
                candidateLocal = candidateLocal.AddDays(days);
                if (candidateLocal < localNow) candidateLocal = candidateLocal.AddDays(7);
            } else if (candidateLocal < localNow) candidateLocal = candidateLocal.AddDays(1);
            if (zone.IsInvalidTime(candidateLocal)) throw new FormatException("The reset time falls in a daylight-saving clock gap. Use an ISO 8601 timestamp with an explicit offset.");
            if (zone.IsAmbiguousTime(candidateLocal)) throw new FormatException("The reset time is ambiguous during a daylight-saving clock change. Use an ISO 8601 timestamp with an explicit offset.");
            DateTimeOffset candidate = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(candidateLocal, zone), TimeSpan.Zero);
            TimeSpan ahead = candidate - observed;
            TimeSpan limit = hasWeekday ? TimeSpan.FromDays(7) : TimeSpan.FromHours(24);
            if (ahead < TimeSpan.Zero || ahead > limit)
                throw new FormatException("The next displayed reset time is outside the supported daily or weekly window.");
            return candidate;
        }

        private static int ParseWeekday(string value) {
            string shortened = value.ToLowerInvariant();
            string[] days = { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };
            for (int index = 0; index < days.Length; index++) if (shortened.StartsWith(days[index], StringComparison.Ordinal)) return index;
            char last = value[value.Length - 1];
            if (last == '日' || last == '天') return 0;
            return "一二三四五六".IndexOf(last) + 1;
        }

        private static TimeSpan ParseDuration(string value, bool chinese) {
            Regex token = chinese ? ChineseDuration : EnglishDuration;
            MatchCollection matches = token.Matches(value);
            if (matches.Count == 0) throw new FormatException("A relative reset duration is unreadable.");
            decimal totalSeconds = 0;
            int position = 0;
            var usedUnits = new HashSet<int>();
            foreach (Match match in matches) {
                string separator = value.Substring(position, match.Index - position);
                if (position == 0 ? separator.Trim().Length != 0 : !DurationSeparator(separator, chinese))
                    throw new FormatException("A relative reset duration contains unsupported text.");
                decimal number;
                if (!Decimal.TryParse(match.Groups["number"].Value, NumberStyles.None, Invariant, out number) || number > 691200)
                    throw new FormatException("A relative reset duration is too large.");
                string unit = match.Groups["unit"].Value.ToLowerInvariant();
                int multiplier = unit == "天" || unit.StartsWith("d", StringComparison.Ordinal) ? 86400 :
                    unit == "小时" || unit == "小時" || unit.StartsWith("h", StringComparison.Ordinal) ? 3600 :
                    unit == "分钟" || unit == "分鐘" || unit.StartsWith("m", StringComparison.Ordinal) ? 60 : 1;
                if (!usedUnits.Add(multiplier)) throw new FormatException("A relative reset duration repeats a time unit.");
                totalSeconds += number * multiplier;
                position = match.Index + match.Length;
            }
            if (value.Substring(position).Trim().Length != 0 || totalSeconds > 691200)
                throw new FormatException("A relative reset duration is invalid or exceeds eight days.");
            return TimeSpan.FromSeconds((double)totalSeconds);
        }

        private static bool DurationSeparator(string value, bool chinese) {
            string separator = value.Trim();
            return separator.Length == 0 || separator == "," || (!chinese && separator.Equals("and", StringComparison.OrdinalIgnoreCase)) || (chinese && separator == "和");
        }

        public static ResetSnapshot ParseStatuslineJson(string json, DateTimeOffset observed) {
            CheckInput(json, "Statusline JSON");
            object decoded;
            try {
                // Reject duplicate keys instead of allowing a serializer to silently select one.
                RejectDuplicateJsonKeys(json);
                var serializer = new JavaScriptSerializer { MaxJsonLength = MaximumInputLength, RecursionLimit = 32 };
                decoded = serializer.DeserializeObject(json);
            } catch (Exception error) {
                if (error is ArgumentException || error is InvalidOperationException || error is FormatException)
                    throw new FormatException("Statusline JSON is malformed, too deeply nested, or contains duplicate fields.");
                throw;
            }
            var root = decoded as Dictionary<string, object>;
            if (root == null) throw new FormatException("Statusline JSON must be an object.");
            object value;
            Dictionary<string, object> rates = null;
            if (root.TryGetValue("rate_limits", out value) && value != null) {
                rates = value as Dictionary<string, object>;
                if (rates == null) throw new FormatException("The rate_limits field must be an object.");
            }
            return Validate(new ResetSnapshot {
                SessionResetUtc = ReadStatuslineReset(rates, "five_hour"),
                WeeklyResetUtc = ReadStatuslineReset(rates, "seven_day"),
                ObservedUtc = UtcString(observed),
                Source = "Claude Code statusline",
                Estimated = false
            });
        }

        private static string ReadStatuslineReset(Dictionary<string, object> rates, string field) {
            object value;
            if (rates == null || !rates.TryGetValue(field, out value) || value == null) return null;
            var limit = value as Dictionary<string, object>;
            if (limit == null) throw new FormatException("A statusline rate limit must be an object.");
            if (!limit.TryGetValue("resets_at", out value) || value == null) return null;
            if (!(value is int) && !(value is long) && !(value is decimal) && !(value is double))
                throw new FormatException("A statusline reset must be a Unix timestamp in seconds.");
            try {
                decimal seconds = Convert.ToDecimal(value, Invariant);
                if (Decimal.Truncate(seconds) != seconds || seconds < -62135596800m || seconds > 253402300799m)
                    throw new FormatException("A statusline reset must be a whole Unix timestamp in seconds.");
                return UtcString(UnixEpoch.AddSeconds((double)seconds));
            } catch (OverflowException) {
                throw new FormatException("A statusline reset timestamp is outside the supported range.");
            } catch (ArgumentOutOfRangeException) {
                throw new FormatException("A statusline reset timestamp is outside the supported range.");
            }
        }

        private static void CheckInput(string input, string label) {
            if (String.IsNullOrWhiteSpace(input)) throw new FormatException(label + " is empty or inaccessible.");
            if (input.Length > MaximumInputLength) throw new FormatException(label + " exceeds the 16,000-character limit.");
        }

        private static void RejectDuplicateJsonKeys(string json) {
            var keySerializer = new JavaScriptSerializer { MaxJsonLength = MaximumInputLength, RecursionLimit = 4 };
            int position = 0;
            ReadJsonValue(json, ref position, 0, keySerializer);
            SkipJsonWhitespace(json, ref position);
            if (position != json.Length) throw new FormatException("JSON has trailing data.");
        }

        // JavaScriptSerializer accepts some JavaScript extensions. Validate strict JSON first.
        private static void ReadJsonValue(string json, ref int position, int depth, JavaScriptSerializer keySerializer) {
            if (depth > 32) throw new FormatException("JSON nesting is too deep.");
            SkipJsonWhitespace(json, ref position);
            if (position >= json.Length) throw new FormatException("JSON is incomplete.");
            char current = json[position];
            if (current == '{' || current == '[') {
                bool isObject = current == '{';
                char close = isObject ? '}' : ']';
                var keys = isObject ? new HashSet<string>(StringComparer.Ordinal) : null;
                position++;
                SkipJsonWhitespace(json, ref position);
                if (position < json.Length && json[position] == close) { position++; return; }
                while (true) {
                    if (isObject) {
                        SkipJsonWhitespace(json, ref position);
                        int start = position;
                        ReadJsonString(json, ref position);
                        string key = keySerializer.Deserialize<string>(json.Substring(start, position - start));
                        if (!keys.Add(key)) throw new FormatException("JSON has duplicate fields.");
                        SkipJsonWhitespace(json, ref position);
                        if (position >= json.Length || json[position++] != ':') throw new FormatException("A JSON field is incomplete.");
                    }
                    ReadJsonValue(json, ref position, depth + 1, keySerializer);
                    SkipJsonWhitespace(json, ref position);
                    if (position >= json.Length) throw new FormatException("JSON is incomplete.");
                    char next = json[position++];
                    if (next == close) return;
                    if (next != ',') throw new FormatException("JSON has an invalid separator.");
                }
            }
            if (current == '"') { ReadJsonString(json, ref position); return; }
            foreach (string literal in new string[] { "null", "true", "false" }) {
                if (position + literal.Length <= json.Length && String.CompareOrdinal(json, position, literal, 0, literal.Length) == 0) {
                    position += literal.Length;
                    return;
                }
            }
            if (current == '-') position++;
            if (position >= json.Length || json[position] < '0' || json[position] > '9') throw new FormatException("JSON has an invalid value.");
            if (json[position] == '0') position++;
            else while (position < json.Length && json[position] >= '0' && json[position] <= '9') position++;
            if (position < json.Length && json[position] == '.') {
                position++;
                ReadJsonDigits(json, ref position);
            }
            if (position < json.Length && (json[position] == 'e' || json[position] == 'E')) {
                position++;
                if (position < json.Length && (json[position] == '+' || json[position] == '-')) position++;
                ReadJsonDigits(json, ref position);
            }
        }

        private static void ReadJsonDigits(string json, ref int position) {
            int start = position;
            while (position < json.Length && json[position] >= '0' && json[position] <= '9') position++;
            if (position == start) throw new FormatException("JSON has an invalid number.");
        }

        private static void ReadJsonString(string json, ref int position) {
            if (position >= json.Length || json[position++] != '"') throw new FormatException("JSON field names must be quoted strings.");
            while (position < json.Length) {
                char character = json[position++];
                if (character == '"') return;
                if (character < 32) throw new FormatException("JSON has an invalid string.");
                if (character == '\\') {
                    if (position >= json.Length) throw new FormatException("JSON has an incomplete escape.");
                    char escape = json[position++];
                    if (escape == 'u') {
                        for (int index = 0; index < 4; index++) {
                            if (position >= json.Length || !Uri.IsHexDigit(json[position++])) throw new FormatException("JSON has an invalid Unicode escape.");
                        }
                    } else if ("\"\\/bfnrt".IndexOf(escape) < 0) throw new FormatException("JSON has an invalid escape.");
                }
            }
            throw new FormatException("JSON has an unfinished string.");
        }

        private static void SkipJsonWhitespace(string json, ref int position) {
            while (position < json.Length && (json[position] == ' ' || json[position] == '\r' || json[position] == '\n' || json[position] == '\t')) position++;
        }
    }
}
