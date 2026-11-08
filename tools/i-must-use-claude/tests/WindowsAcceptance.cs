using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using WhaleClaudeHelper;

// Only this entry point invokes real Windows font/recycle operations. It is not
// part of an application binary, and rejects ordinary developer machines.
internal static class WindowsAcceptance {
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    private static string root, report;
    private static readonly List<Case> Cases = new List<Case>();
    private sealed class Case {
        public string Name, State = "running", Error, Scope;
        public List<string> Checks = new List<string>();
        public Dictionary<string, string> Evidence = new Dictionary<string, string>();
    }
    private static void Save() {
        File.WriteAllText(report, Json.Serialize(new {
            Schema = 1, Kind = "windows-acceptance", Utc = DateTime.UtcNow.ToString("o"),
            Host = "github-hosted-windows", Guard = "GITHUB_ACTIONS=true AND WHALE_E2E_ALLOW_SYSTEM_CHANGES=1",
            Scope = "Temporary browser, cache, CuteCloud and Codex profiles; one temporary HKCU font registration. No network probes.",
            Cases = Cases, Passed = Cases.Count == 5 && Cases.All(x => x.State == "passed"),
            Checks = Cases.Sum(x => x.Checks.Count)
        }), Utf8);
    }
    private static void Check(Case item, bool condition, string label) {
        if (!condition) throw new InvalidOperationException(label);
        item.Checks.Add(label); Save();
    }
    private static void Run(string name, string scope, Action<Case> action) {
        var item = new Case { Name = name, Scope = scope }; Cases.Add(item); Save();
        try { action(item); item.State = "passed"; }
        catch (Exception error) { item.State = "failed"; item.Error = error.GetType().Name + ": " + error.Message; }
        Save(); Console.WriteLine(item.State.ToUpperInvariant() + " " + name + " (" + item.Checks.Count + " checks)");
    }
    private static string Hash(string path) {
        using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
    private static string Within(string directory, string relative) {
        string path = Path.GetFullPath(Path.Combine(directory, relative));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("outside_fresh_fixture_root");
        RepairEngine.AssertPlainPath(path); return path;
    }
    private static void Write(string path, string text) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, text, Utf8); }
    private static Dictionary<string, object> Object(object value) { return (Dictionary<string, object>)value; }
    private static bool Equal(object left, object right) {
        var a = left as Dictionary<string, object>; var b = right as Dictionary<string, object>;
        if (a != null || b != null) return a != null && b != null && a.Count == b.Count && a.All(x => b.ContainsKey(x.Key) && Equal(x.Value, b[x.Key]));
        if (!(left is string) && !(right is string) && left is System.Collections.IEnumerable && right is System.Collections.IEnumerable) {
            object[] aa = ((System.Collections.IEnumerable)left).Cast<object>().ToArray(), bb = ((System.Collections.IEnumerable)right).Cast<object>().ToArray();
            return aa.Length == bb.Length && aa.Select((x, i) => Equal(x, bb[i])).All(x => x);
        }
        return System.Object.Equals(left, right);
    }
    private static Dictionary<string, object> Preferences(string path) {
        var outer = Object(Json.DeserializeObject(File.ReadAllText(path)));
        outer["flutter.config"] = Json.DeserializeObject((string)outer["flutter.config"]);
        return outer;
    }
    private static string Intel(string path, string key) { return (string)Object(Object(Json.DeserializeObject(File.ReadAllText(path)))["intl"])[key]; }

    private static void Browsers(Case item) {
        var roots = new Dictionary<string, string>();
        var before = new Dictionary<string, string>();
        foreach (string browser in new[] { "Chrome", "Edge" }) {
            string data = Within(root, "browsers/" + browser); roots.Add(browser, data);
            string state = Within(data, "Local State"), prefs = Within(data, "Default/Preferences");
            Write(state, "{\"intl\":{\"app_locale\":\"zh-CN\"},\"fixture_sentinel\":1.2500}\n");
            Write(prefs, "{\"intl\":{\"accept_languages\":\"zh-CN,zh\",\"selected_languages\":\"zh-CN\"},\"fixture_sentinel\":true}\n");
            before[state] = Hash(state); before[prefs] = Hash(prefs);
        }
        var service = new BrowserLocaleService(Within(root, "browser-backups"), roots);
        foreach (string browser in new[] { "Chrome", "Edge" }) {
            BrowserLocaleResult applied = null;
            try {
                applied = service.ApplyForBrowser(browser);
                Check(item, applied.Ok && !String.IsNullOrEmpty(applied.BackupDirectory), browser + " applies through BrowserLocaleService");
                string state = Within(roots[browser], "Local State"), prefs = Within(roots[browser], "Default/Preferences");
                Check(item, Intel(state, "app_locale") == "en-US" && Intel(prefs, "accept_languages") == "en-US,en" && Intel(prefs, "selected_languages") == "en-US,en", browser + " stored language fields verified");
                Check(item, File.ReadAllText(state).Contains("\"fixture_sentinel\":1.2500") && File.ReadAllText(prefs).Contains("\"fixture_sentinel\":true"), browser + " unrelated JSON retained");
                string other = browser == "Chrome" ? "Edge" : "Chrome";
                Check(item, before[Within(roots[other], "Local State")] == Hash(Within(roots[other], "Local State")) && before[Within(roots[other], "Default/Preferences")] == Hash(Within(roots[other], "Default/Preferences")), browser + " leaves other browser untouched");
            } finally {
                if (applied != null && !String.IsNullOrEmpty(applied.BackupDirectory)) {
                    var undo = service.Restore(applied.BackupDirectory);
                    Check(item, undo.Ok, browser + " restore returns success");
                }
                foreach (var entry in before) Check(item, File.Exists(entry.Key) && Hash(entry.Key) == entry.Value, browser + " final original bytes: " + Path.GetFileName(Path.GetDirectoryName(entry.Key)) + "/" + Path.GetFileName(entry.Key));
            }
        }
        item.Evidence["profiles"] = "Two generated Local State / Default Preferences pairs; no installed browser profiles opened or launched.";
    }

    private static void Fonts(Case item) {
        string data = Within(root, "font"), user = Within(data, "user-fonts");
        Directory.CreateDirectory(user);
        string source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
        Check(item, File.Exists(source), "Windows test-font source exists (read-only Arial copy)");
        string originalSystemHash = Hash(source), path = Within(user, "WhaleCi.ttf");
        File.Copy(source, path, false);
        string name = "Whale CI " + Guid.NewGuid().ToString("N") + " (TrueType)";
        var platform = new WindowsFontPlatform();
        var service = new FontService(new FontContext { UserFonts = user, SystemFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts), BackupRoot = Within(data, "backups") }, platform);
        FontResult removed = null; bool registered = false, loaded = false;
        string fontHash = Hash(path);
        try {
            Check(item, !platform.Read(true).Any(x => x.Name == name), "unique temporary HKCU font registration is absent");
            platform.Register(name, path); registered = true;
            loaded = platform.Load(path); Check(item, loaded, "real Windows font resource loaded from test copy");
            FontEntry entry = service.ListFonts().Single(x => x.Name == name && x.Path == path);
            Check(item, entry.IsUser && entry.CanRemove, "only the temporary per-user font is removable");
            removed = service.Remove(entry);
            Check(item, removed.Ok, "FontService.Remove succeeds: " + removed.Message);
            registered = false; loaded = false;
            Check(item, !File.Exists(path) && !platform.Read(true).Any(x => x.Name == name), "test font file and HKCU registration removed");
            Check(item, Hash(Path.Combine(removed.BackupDirectory, "font.ttf")) == fontHash, "font backup matches exact original bytes");
        } finally {
            try {
                if (removed != null && !String.IsNullOrEmpty(removed.BackupDirectory)) {
                    var undo = service.Restore(removed.BackupDirectory);
                    registered = platform.Read(true).Any(x => x.Name == name && x.Data == path);
                    loaded = registered;
                    Check(item, undo.Ok, "FontService.Restore succeeds: " + undo.Message);
                    Check(item, File.Exists(path) && Hash(path) == fontHash && registered, "test font file and registry restored exactly");
                }
            } finally {
                // Remove only our exact random registration/resource; never an existing font.
                if (loaded) platform.Unload(path);
                var owned = platform.Read(true).Where(x => x.Name == name).ToList();
                if (owned.Count > 0) {
                    if (owned.Count != 1 || owned[0].Data != path) throw new IOException("temporary_font_registration_conflict");
                    platform.Unregister(name);
                }
                if (File.Exists(path)) platform.Recycle(path);
                platform.Notify();
                Check(item, !File.Exists(path) && !platform.Read(true).Any(x => x.Name == name), "temporary font setup removed in finally");
                Check(item, Hash(source) == originalSystemHash, "installed Windows font source remains byte-identical");
            }
        }
        item.Evidence["fontSource"] = "Windows Arial copied into a fresh temporary user-font root. No pre-existing font registration removed.";
    }

    private static void Cache(Case item) {
        string data = Within(root, "cache");
        var context = new CacheContext { Home = Within(data, "home"), Local = Within(data, "local"), Roaming = Within(data, "roaming"), BackupRoot = Within(data, "backups") };
        string code = Within(context.Home, ".claude/cache/fixture.bin"), desktop = Within(context.Roaming, "Claude/GPUCache/fixture.bin"), protectedFile = Within(context.Home, ".claude/projects/keep.txt");
        Write(code, "generated code cache\n"); Write(desktop, "generated desktop cache\n"); Write(protectedFile, "persistent fixture, must survive\n");
        var before = new Dictionary<string, string> { { code, Hash(code) }, { desktop, Hash(desktop) }, { protectedFile, Hash(protectedFile) } };
        var service = new CacheService(context); CacheOperationResult applied = null;
        try {
            var targets = service.Audit(CancellationToken.None);
            Check(item, targets.Count == 2 && targets.All(x => x.Eligible), "audit selects exactly two generated cache roots");
            applied = service.Clean(targets, CancellationToken.None);
            Check(item, applied.Ok && applied.ChangedCount == 2, "real Windows recycler cleaned both allowlisted fixture caches");
            Check(item, !File.Exists(code) && !File.Exists(desktop), "cache payloads removed");
            Check(item, Hash(protectedFile) == before[protectedFile], "persistent project fixture untouched");
        } finally {
            if (applied != null && !String.IsNullOrEmpty(applied.BackupDirectory)) Check(item, service.Restore(applied.BackupDirectory, CancellationToken.None).Ok, "cache restore succeeds");
            foreach (var pair in before) Check(item, File.Exists(pair.Key) && Hash(pair.Key) == pair.Value, "cache final original bytes: " + pair.Key.Substring(data.Length + 1));
        }
    }

    private static void Dns(Case item) {
        string data = Within(root, "dns/data"), prefs = Within(data, "shared_preferences.json"), yaml = Within(data, "config.yaml");
        string config = "{\"overrideDns\":false,\"fixture_sentinel\":true,\"patchClashConfig\":{\"dns\":{\"nameserver\":[\"223.5.5.5\"],\"fallback\":[],\"nameserver-policy\":{\"geosite:cn\":\"223.5.5.5\",\"internal.invalid\":\"10.0.0.1\"}}}}";
        Write(prefs, Json.Serialize(new Dictionary<string, object> { { "flutter.config", config }, { "fixture_sentinel", "keep" } }));
        Write(yaml, "dns:\n  nameserver: [223.5.5.5]\n  fallback: []\nproxy-groups:\n  - name: FixtureProxy\n    type: select\n    proxies: [DIRECT]\n");
        string before = Hash(prefs), yamlBefore = Hash(yaml);
        var original = Preferences(prefs);
        var service = new NetworkDnsService(data, Within(root, "dns/backups"), delegate { return false; });
        bool attempted = false;
        try {
            var plan = service.Preview("FixtureProxy");
            Check(item, plan.Valid && Hash(prefs) == before, "DNS preview validates the fixture without mutation");
            attempted = true; var applied = service.Apply(plan);
            Check(item, applied.Ok && applied.Code == "applied", "DNS apply succeeds: " + applied.Code);
            var parsed = Object(Json.DeserializeObject((string)Object(Json.DeserializeObject(File.ReadAllText(prefs)))["flutter.config"]));
            var dns = Object(Object(parsed["patchClashConfig"])["dns"]);
            string[] servers = ((System.Collections.IEnumerable)dns["nameserver"]).Cast<object>().Select(Convert.ToString).ToArray();
            Check(item, (bool)parsed["overrideDns"] && servers.SequenceEqual(new[] { "https://1.1.1.1/dns-query#FixtureProxy", "https://8.8.8.8/dns-query#FixtureProxy" }), "DNS resolver values verified in saved preferences");
            var policy = Object(dns["nameserver-policy"]);
            Check(item, !policy.ContainsKey("geosite:cn") && (string)policy["internal.invalid"] == "10.0.0.1", "DNS matched policy removed; internal policy retained");
            Check(item, (bool)parsed["fixture_sentinel"] && Hash(yaml) == yamlBefore, "unrelated preferences and generated YAML untouched");
        } finally {
            if (attempted && service.HasUndo) Check(item, service.Undo().Ok, "DNS undo succeeds");
            // Field-level undo can reinsert a removed JSON member in a new order.
            // Verify every original value, not an unjustified byte-order promise.
            Check(item, Equal(original, Preferences(prefs)) && Hash(yaml) == yamlBefore, "all DNS preference values restored and YAML byte-identical");
            item.Evidence["beforePreferencesSha256"] = before;
            item.Evidence["restoredPreferencesSha256"] = Hash(prefs);
        }
        item.Evidence["network"] = "No CuteCloud process or network connection. Production DNS edit/undo on generated config files only.";
    }

    private static void Codex(Case item) {
        string data = Within(root, "codex"), config = Within(data, ".codex"), path = Within(config, ".env");
        Write(path, "# generated CI fixture\r\nUNRELATED_SETTING=keep-this\r\nHTTP_PROXY=\"http://127.0.0.1:18080\"\r\n");
        string before = Hash(path);
        var service = new CodexProxySettings(config, Within(data, "backups")); CodexProxyChange applied = null;
        try {
            applied = service.Apply("http://127.0.0.1:18081", "socks5h://127.0.0.1:18082");
            Check(item, applied.Success && applied.Changed, "Codex proxy apply succeeds: " + applied.Error);
            string text = File.ReadAllText(path);
            Func<string, string> value = delegate(string key) { return text.Split('\n').Single(line => line.StartsWith(key + "=", StringComparison.Ordinal)).Substring(key.Length + 1).Trim().Trim('"'); };
            Check(item, value("HTTP_PROXY") == "http://127.0.0.1:18081" && value("HTTPS_PROXY") == "http://127.0.0.1:18081" && value("ALL_PROXY") == "socks5h://127.0.0.1:18082" && value("NO_PROXY") == "localhost,127.0.0.1,::1", "all four stored proxy values verified");
            Check(item, text.Contains("UNRELATED_SETTING=keep-this"), "unrelated env setting retained");
        } finally {
            if (applied != null && !String.IsNullOrEmpty(applied.BackupDirectory)) Check(item, service.Restore(applied.BackupDirectory).Success, "Codex proxy undo succeeds");
            Check(item, Hash(path) == before, "Codex env restored byte-for-byte");
        }
        item.Evidence["network"] = "Loopback URIs are fixture values only. No listener, request, process environment or user profile modified.";
    }

    [STAThread] private static int Main(string[] args) {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Environment.GetEnvironmentVariable("WHALE_E2E_ALLOW_SYSTEM_CHANGES") != "1" || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted" || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows") {
            Console.Error.WriteLine("Refusing real acceptance outside an explicitly enabled GitHub-hosted Windows runner."); return 2;
        }
        try {
            string runner = Path.GetFullPath(Environment.GetEnvironmentVariable("RUNNER_TEMP"));
            if (args.Length != 1) throw new ArgumentException("Expected a fresh output directory under RUNNER_TEMP.");
            root = Path.GetFullPath(args[0]).TrimEnd(Path.DirectorySeparatorChar);
            if (!root.StartsWith(runner.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || Directory.Exists(root) || File.Exists(root)) throw new IOException("output_must_be_new_under_runner_temp");
            RepairEngine.AssertPlainPath(root); Directory.CreateDirectory(root);
            report = Within(root, "acceptance-results.json");
            Run("browser-language", "Temporary Chrome/Edge profiles, real file apply/undo", Browsers);
            Run("personal-font", "One test-owned HKCU registration, GDI resource and temporary font copy", Fonts);
            Run("cache-cleanup", "Generated whitelist caches, real Windows Recycle Bin and backup restore", Cache);
            Run("cutecloud-dns", "Generated CuteCloud preferences and YAML only", Dns);
            Run("codex-env", "Generated .codex/.env only", Codex);
            return Cases.All(x => x.State == "passed") ? 0 : 1;
        } catch (Exception error) { Console.Error.WriteLine(error.GetType().Name + ": " + error.Message); return 1; }
    }
}
