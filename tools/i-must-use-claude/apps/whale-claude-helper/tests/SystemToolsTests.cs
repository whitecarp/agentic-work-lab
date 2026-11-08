using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Security.AccessControl;
using WhaleClaudeHelper;

internal static class SystemToolsTests {
    private static int checks;
    private static void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks++; }
    private static SystemChangeResult Bilingual(SystemChangeResult result) {
        Check(!String.IsNullOrWhiteSpace(result.Message), "Chinese result is present");
        Check(!String.IsNullOrWhiteSpace(result.MessageEnglish) && result.MessageEnglish.All(c => c < 128), "English result is present and uses English text");
        return result;
    }
    private sealed class FakePlatform : ITimeZonePlatform {
        internal string Current = "UTC";
        internal int Writes;
        internal bool IgnoreWrite, DenyWrite;
        internal Exception ReadError;
        public List<TimeZoneInfo> GetTimeZones() { return new List<TimeZoneInfo> { TimeZoneInfo.Utc, TimeZoneInfo.CreateCustomTimeZone("Fixture East", TimeSpan.FromHours(2), "Fixture East", "Fixture East"), TimeZoneInfo.CreateCustomTimeZone("Fixture West", TimeSpan.FromHours(-5), "Fixture West", "Fixture West") }; }
        public string ReadSetting() { if (ReadError != null) throw ReadError; return Current; }
        public void WriteSetting(string setting) { if (DenyWrite) throw new InvalidOperationException("timezone_permission_or_policy"); Writes++; if (!IgnoreWrite) Current = setting; }
    }
    private static void Main() {
        var realZones = SystemTools.ListTimeZones();
        Check(realZones.Count > 0, "read-only Windows timezone enumeration");
        Check(realZones.Any(x => x.Id == SystemTools.CurrentTimeZoneId()), "read-only actual timezone resolves to valid ID");
        string root = Path.Combine(Path.GetTempPath(), "WhaleTimezoneTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var fake = new FakePlatform();
        var service = new TimeZoneChangeService(fake, root);
        Check(!Bilingual(service.Apply("UTC\" & invalid")).Ok && fake.Writes == 0, "invalid ID never reaches external utility");
        Check(Bilingual(service.Apply("UTC")).Ok && fake.Writes == 0 && Directory.GetDirectories(root).Length == 0, "unchanged ID has no mutation or backup");
        fake.Current = "UTC_dstoff";
        var applied = Bilingual(service.Apply("Fixture East"));
        Check(applied.Ok && fake.Current == "Fixture East" && fake.Writes == 1, "apply exact selected timezone");
        Check(File.ReadAllText(Path.Combine(applied.BackupDirectory, "timezone.json")).Contains("UTC_dstoff"), "preserve disabled daylight saving setting");
        Check(service.ListBackups().SequenceEqual(new[] { applied.BackupDirectory }), "only own recognized backups listed");
        var acl = Directory.GetAccessControl(applied.BackupDirectory);
        Check(acl.AreAccessRulesProtected, "backup ACL protected");
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier))) Check(rule.IdentityReference.Equals(WindowsIdentity.GetCurrent().User), "backup grants current user only");
        Check(Bilingual(service.Restore(applied.BackupDirectory)).Ok && fake.Current == "UTC_dstoff", "restore exact original and DST flag");
        int writes = fake.Writes;
        Check(Bilingual(service.Restore(applied.BackupDirectory)).Ok && fake.Writes == writes, "restore idempotent");
        applied = Bilingual(service.Apply("Fixture East"));
        fake.Current = "Fixture West";
        writes = fake.Writes;
        Check(!Bilingual(service.Restore(applied.BackupDirectory)).Ok && fake.Writes == writes, "restore refuses later user change");
        Check(!Bilingual(service.Restore(Path.GetTempPath())).Ok && fake.Writes == writes, "restore refuses outside backup root");
        fake.DenyWrite = true;
        var denied = Bilingual(service.Apply("Fixture East"));
        Check(!denied.Ok && fake.Current == "Fixture West" && File.Exists(Path.Combine(denied.BackupDirectory, "timezone.json")), "denied OS write preserves backup and current state");
        fake.DenyWrite = false; fake.IgnoreWrite = true;
        var failed = Bilingual(service.Apply("Fixture East"));
        Check(!failed.Ok && fake.Current == "Fixture West", "read-back catches no-op utility");
        fake.IgnoreWrite = false;
        File.WriteAllText(Path.Combine(failed.BackupDirectory, "timezone.json"), "{\"Format\":1,\"Kind\":\"wrong-app\",\"Before\":\"UTC\",\"After\":\"Fixture West\",\"State\":\"applied\"}");
        writes = fake.Writes;
        Check(!Bilingual(service.Restore(failed.BackupDirectory)).Ok && fake.Writes == writes, "foreign receipt never changes system");
        Check(!service.ListBackups().Contains(failed.BackupDirectory), "foreign receipts omitted from backup list");
        foreach (Exception error in new Exception[] { new TimeoutException("timezone_timeout"), new IOException("unrecognized_current_timezone"), new InvalidOperationException("timezone_changed_rescan"), new InvalidDataException("linked_path_not_allowed"), new IOException("backup_verification_failed"), new UnauthorizedAccessException("PRIVATE_ERROR_MUST_NOT_LEAK") }) {
            fake.ReadError = error;
            var result = Bilingual(service.Apply("UTC"));
            Check(!result.Ok && fake.Writes == writes && !result.MessageEnglish.Contains("PRIVATE_ERROR"), "early failures are localized without leaking exception text or changing timezone");
        }
        Console.WriteLine("PASS " + checks + " checks; all writes used fake adapter; fixture: " + root);
    }
}
