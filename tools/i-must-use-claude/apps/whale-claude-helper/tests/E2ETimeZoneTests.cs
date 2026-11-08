using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
using WhaleClaudeHelper;

// Real end-to-end check of the time zone change and undo, through tzutil on an actual Windows install.
// It changes the machine's time zone, so it refuses to run anywhere except a disposable CI runner.
internal static class E2ETimeZoneTests {
    static int checks;
    static readonly List<string> passed = new List<string>();
    static void Check(bool ok, string label) {
        if (!ok) throw new Exception("FAILED: " + label);
        checks++;
        passed.Add(label);
        Console.WriteLine("ok " + checks + " - " + label);
    }
    static int Main(string[] args) {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Environment.GetEnvironmentVariable("WHALE_E2E_ALLOW_SYSTEM_CHANGES") != "1" || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted" || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows") {
            Console.Error.WriteLine("Refusing to run: this test changes the real Windows time zone and is meant only for disposable CI runners.");
            return 2;
        }
        if (args.Length != 1) { Console.Error.WriteLine("A CI receipt path is required."); return 2; }
        string runner = Path.GetFullPath(Environment.GetEnvironmentVariable("RUNNER_TEMP"));
        string report = Path.GetFullPath(args[0]);
        if (!report.StartsWith(runner.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("Receipt must be under RUNNER_TEMP."); return 2; }
        var platform = new WindowsTimeZonePlatform();
        string original = platform.ReadSetting();
        string backupRoot = Path.Combine(Path.GetTempPath(), "WhaleTimeZoneE2E-" + Guid.NewGuid().ToString("N"));
        var service = new TimeZoneChangeService(platform, backupRoot);
        const string home = "China Standard Time";
        const string elsewhere = "Tokyo Standard Time";
        int result = 0;
        string failure = null, restoreFailure = null;
        bool restoredOriginal = false;
        try {
            platform.WriteSetting(home);
            Check(platform.ReadSetting() == home, "runner starts from " + home);

            SystemChangeResult applied = service.Apply("UTC");
            Check(applied.Ok, "apply UTC succeeds: " + applied.MessageEnglish);
            Check(platform.ReadSetting() == "UTC", "Windows now reports UTC");
            Check(Directory.Exists(applied.BackupDirectory), "a backup receipt was written before the change");

            SystemChangeResult restored = service.Restore(applied.BackupDirectory);
            Check(restored.Ok, "undo succeeds: " + restored.MessageEnglish);
            Check(platform.ReadSetting() == home, "Windows is back on " + home);

            SystemChangeResult again = service.Restore(applied.BackupDirectory);
            Check(again.Ok && platform.ReadSetting() == home, "repeating undo is a safe no-op");

            SystemChangeResult second = service.Apply("UTC");
            Check(second.Ok && platform.ReadSetting() == "UTC", "apply UTC a second time");
            platform.WriteSetting(elsewhere);
            SystemChangeResult conflict = service.Restore(second.BackupDirectory);
            Check(!conflict.Ok, "undo refuses when the time zone was changed again later");
            Check(platform.ReadSetting() == elsewhere, "the later change (" + elsewhere + ") is kept, not overwritten");

            Console.WriteLine("PASS " + checks + " real time zone checks on a disposable Windows runner.");
        } catch (Exception error) {
            Console.Error.WriteLine(error);
            failure = error.GetType().Name + ": " + error.Message;
            result = 1;
        } finally {
            try {
                platform.WriteSetting(original);
                restoredOriginal = platform.ReadSetting() == original;
                Check(restoredOriginal, "finally restores the runner's exact original time zone and DST setting");
            } catch (Exception error) {
                restoreFailure = error.GetType().Name + ": " + error.Message;
                Console.Error.WriteLine("Could not restore the runner time zone: " + restoreFailure);
                result = 1;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllText(report, new JavaScriptSerializer().Serialize(new {
                Schema = 1, Kind = "real-timezone-acceptance", Utc = DateTime.UtcNow.ToString("o"),
                Original = original, BackupRoot = backupRoot, Checks = passed,
                Passed = result == 0 && restoredOriginal, RestoredOriginal = restoredOriginal,
                Error = failure, RestoreError = restoreFailure
            }), new UTF8Encoding(false));
        }
        return result;
    }
}
