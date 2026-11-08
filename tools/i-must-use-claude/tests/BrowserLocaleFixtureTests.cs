using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using WhaleClaudeHelper;

// No browser Apply/Restore, process closing, profile access or Windows changes.
internal static class BrowserLocaleFixtureTests {
    private static int checks;
    private static void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
    private static void Rejected(string backup, Dictionary<string, string> roots, string label) {
        bool rejected = false;
        try { new BrowserLocaleService(backup, roots); } catch (ArgumentException) { rejected = true; }
        Check(rejected, label);
    }
    private static void Main() {
        string root = Path.Combine(Path.GetTempPath(), "whale-browser-constructor-" + Guid.NewGuid().ToString("N"));
        string backup = Path.Combine(root, "backups"), chrome = Path.Combine(root, "chrome"), edge = Path.Combine(root, "edge");
        var roots = new Dictionary<string, string> { { "Chrome", chrome }, { "Edge", edge } };
        var service = new BrowserLocaleService(backup, roots);
        roots["Chrome"] = Path.Combine(root, "changed-by-caller");
        var stored = (Dictionary<string, string>)typeof(BrowserLocaleService).GetField("roots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
        Check(stored["Chrome"] == chrome && stored["Edge"] == edge, "injected roots are defensively copied");
        Rejected(backup, new Dictionary<string, string> { { "Chrome", chrome } }, "missing browser rejected");
        Rejected(backup, new Dictionary<string, string> { { "Chrome", chrome }, { "Firefox", edge } }, "unsupported browser rejected");
        Rejected(backup, new Dictionary<string, string> { { "chrome", chrome }, { "Edge", edge } }, "noncanonical browser key rejected");
        Rejected(backup, new Dictionary<string, string> { { "Chrome", "relative/profile" }, { "Edge", edge } }, "relative root rejected");
        Rejected(backup, new Dictionary<string, string> { { "Chrome", "" }, { "Edge", edge } }, "empty root rejected");
        Check(!Directory.Exists(root), "constructors create no profile or backup directories");
        foreach (bool state in new[] { true, false }) {
            string original = state
                ? "{\"intl\":{\"app_locale\":\"zh-CN\"},\"keep\":1.2500}\n"
                : "{\"intl\":{\"accept_languages\":\"zh-CN\",\"selected_languages\":\"zh-CN\"},\"keep\":1.2500}\n";
            string edited = BrowserLocaleService.Edit(original, state);
            Check(edited != original && edited.Contains("en-US") && edited.Contains("\"keep\":1.2500"), "language edit retains unrelated raw JSON");
            List<string> conflicts;
            string restored = BrowserLocaleService.RestoreText(original, edited, state, out conflicts);
            Check(conflicts.Count == 0 && restored == original, "unchanged generated preference restores exactly");
            string later = edited.Replace("1.2500", "2.5000");
            restored = BrowserLocaleService.RestoreText(original, later, state, out conflicts);
            Check(conflicts.Count == 0 && restored == original.Replace("1.2500", "2.5000"), "undo retains unrelated later JSON change");
        }
        Console.WriteLine("PASS " + checks + " browser locale fixture checks; no real profiles opened.");
    }
}
