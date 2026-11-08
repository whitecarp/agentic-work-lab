using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using WhaleClaudeHelper;

// This entry point uses only generated files below a new temporary directory and
// an injected process probe. It must never instantiate the production constructor.
internal static class NetworkDnsTests {
    private const string FakeSecret = "fixture-secret-DO-NOT-DISPLAY-39a2";
    private const string FakeNode = "fixture-private-node.invalid";
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private static string root;
    private static int checks, failures;

    private sealed class TestAssertionFailure : Exception {
        internal TestAssertionFailure(string label) : base(label) { }
    }

    private sealed class Fixture {
        internal readonly string DirectoryPath, Data, Backups, Preferences, Yaml;
        internal NetworkDnsService Service;
        internal bool Running;
        internal Func<bool> Probe;
        internal Fixture(string name) {
            DirectoryPath = Path.Combine(root, name);
            Data = Path.Combine(DirectoryPath, "data");
            Backups = Path.Combine(DirectoryPath, "backups");
            Directory.CreateDirectory(Data);
            Preferences = Path.Combine(Data, "shared_preferences.json");
            Yaml = Path.Combine(Data, "config.yaml");
            Write(Preferences, PreferenceText());
            Write(Yaml, YamlText());
            Probe = delegate { return Running; };
            Service = NewService();
        }
        internal NetworkDnsService NewService() {
            return new NetworkDnsService(Data, Backups, delegate { return Probe(); });
        }
        internal Dictionary<string, object> Outer() { return Object(Json.DeserializeObject(File.ReadAllText(Preferences, Utf8))); }
        internal Dictionary<string, object> Config() { return Object(Json.DeserializeObject((string)Outer()["flutter.config"])); }
        internal Dictionary<string, object> Dns() { return Object(Object(Config()["patchClashConfig"])["dns"]); }
        internal void ChangeConfig(Action<Dictionary<string, object>> change) {
            var outer = Outer();
            var config = Object(Json.DeserializeObject((string)outer["flutter.config"]));
            change(config);
            outer["flutter.config"] = Json.Serialize(config);
            Write(Preferences, Json.Serialize(outer));
        }
        internal string Backup(string name) {
            var matches = Directory.GetFiles(Backups, name, SearchOption.AllDirectories);
            Check(matches.Length == 1, "one " + name + " in this fixture");
            return matches[0];
        }
    }

    private static void Check(bool condition, string name) { if (!condition) throw new TestAssertionFailure(name); checks++; }
    private static void Run(string name, Action body) {
        try { body(); Console.WriteLine("PASS " + name); }
        catch (Exception error) {
            failures++;
            // Only test-owned labels are emitted. Unexpected backend exception
            // text could contain user-shaped fixture data, so never print it.
            Console.WriteLine("FAIL " + name + ": " + (error is TestAssertionFailure ? error.Message : error.GetType().Name));
        }
    }
    private static void Write(string path, string text) { File.WriteAllText(path, text, Utf8); }
    private static Dictionary<string, object> Object(object value) { return (Dictionary<string, object>)value; }
    private static object[] ArrayOf(object value) { return ((IEnumerable)value).Cast<object>().ToArray(); }
    private static bool Equal(object a, object b) {
        var left = a as Dictionary<string, object>; var right = b as Dictionary<string, object>;
        if (left != null || right != null) return left != null && right != null && left.Count == right.Count && left.All(p => right.ContainsKey(p.Key) && Equal(p.Value, right[p.Key]));
        if (!(a is string) && !(b is string) && a is IEnumerable && b is IEnumerable) {
            object[] aa = ArrayOf(a), bb = ArrayOf(b); return aa.Length == bb.Length && aa.Select((x, i) => Equal(x, bb[i])).All(x => x);
        }
        return System.Object.Equals(a, b);
    }
    private static void Safe(string text, string label) {
        Check(text != null && !text.Contains(FakeSecret) && !text.Contains(FakeNode) && !text.Contains("fixture-subscription.invalid"), label + " is redacted");
    }
    private static string PreferenceText() {
        var dns = new Dictionary<string, object> {
            { "enable", true }, { "listen", "0.0.0.0:1053" }, { "ipv6", false }, { "respect-rules", false },
            { "nameserver", new[] { "https://dns.alidns.com/dns-query", "223.5.5.5" } },
            { "fallback", new[] { "https://8.8.8.8/dns-query" } },
            { "default-nameserver", new[] { "223.5.5.5", "119.29.29.29" } },
            { "proxy-server-nameserver", new[] { "223.5.5.5" } },
            { "direct-nameserver", null }, { "fake-ip-range", "198.18.0.1/16" },
            { "nameserver-policy", new Dictionary<string, object> {
                { "www.baidu.com", "114.114.114.114" }, { "geosite:cn", "https://doh.pub/dns-query" },
                { "+.internal.crop.com", "10.0.0.1" }, { "geosite:private", "192.168.2.1" },
                { "geosite:cn,private", new[] { "223.5.5.5" } }, { "unknown.fixture.example", "9.9.9.9" }
            } },
            { "fallback-filter", new Dictionary<string, object> { { "geoip", true }, { "geoip-code", "CN" }, { "domain", new[] { "+.google.com" } } } },
            { "custom-dns-option", "keep-me" }
        };
        var config = new Dictionary<string, object> {
            { "overrideDns", false }, { "theme", "fixture-theme" },
            { "networkProps", new Dictionary<string, object> { { "autoSetSystemDns", true }, { "appendSystemDns", false } } },
            { "vpnProps", new Dictionary<string, object> { { "ipv6", false }, { "dnsHijacking", false } } },
            { "patchClashConfig", new Dictionary<string, object> {
                { "mode", "rule" }, { "ipv6", false }, { "mixed-port", 7890 }, { "external-controller", "" },
                { "tun", new Dictionary<string, object> { { "enable", true }, { "dns-hijack", new[] { "any:53" } } } },
                { "dns", dns }
            } },
            { "account", new Dictionary<string, object> { { "token", FakeSecret }, { "subscription", "https://fixture-subscription.invalid/" + FakeSecret } } }
        };
        return "{\n  \"flutter.config\": " + Json.Serialize(Json.Serialize(config)) + ",\n  \"flutter.token\": " + Json.Serialize(FakeSecret) + ",\n  \"unrelated\": {\"untouched\":true,\"numeric\":1.2500},\n  \"flutter.account\": {\"password\":" + Json.Serialize(FakeSecret) + "}\n}\n";
    }
    private static string YamlText() {
        return "mixed-port: 7890\nmode: rule\ndns:\n  enable: true\n  respect-rules: false\n  nameserver:\n    - \"https://dns.alidns.com/dns-query\"\n  fallback: [\"https://8.8.8.8/dns-query\"]\n  nameserver-policy:\n    geosite:cn: \"223.5.5.5\"\n  default-nameserver: [223.5.5.5]\n  proxy-server-nameserver: [119.29.29.29]\ntun:\n  enable: true\n  dns-hijack: [\"any:53\"]\nproxy-groups:\n  - name: \"FixtureGroup\"\n    type: select\n    proxies: [fixture-node]\n  - {name: \"Work Proxy\", type: select, proxies: [fixture-node]}\nproxies:\n  - name: fixture-node\n    server: " + FakeNode + "\n    password: " + FakeSecret + "\nrules:\n  - MATCH,FixtureGroup\n";
    }

    private static void InspectAndPreview() {
        var f = new Fixture("inspect"); string before = File.ReadAllText(f.Preferences), yamlBefore = File.ReadAllText(f.Yaml);
        var snapshot = f.Service.Inspect();
        Check(snapshot.Available && !snapshot.ClientRunning, "fixture is available and stopped");
        Check(snapshot.ProxyGroups.Contains("FixtureGroup") && snapshot.ProxyGroups.Contains("Work Proxy") && snapshot.ProxyGroups.Length == 2, "only existing block and flow proxy-group names extracted");
        Safe(snapshot.Format(false), "Chinese snapshot"); Safe(snapshot.Format(true), "English snapshot");
        var plan = f.Service.Preview("Work Proxy"); Check(plan.Valid, "explicit existing non-default group accepted");
        Safe(plan.Format(false), "Chinese plan"); Safe(plan.Format(true), "English plan");
        foreach (bool english in new[] { false, true }) {
            string review = plan.Format(english);
            Check(review.Contains("shared_preferences.json"), "preview identifies the exact target filename");
            Check(review.Contains("https://1.1.1.1/dns-query#Work%20Proxy") && review.Contains("https://8.8.8.8/dns-query#Work%20Proxy"), "preview shows both complete safely escaped DNS endpoints");
            Check(review.Contains("www.baidu.com") && review.Contains("geosite:cn"), "preview names each explicit policy exception to be removed");
        }
        Check(File.ReadAllText(f.Preferences) == before && File.ReadAllText(f.Yaml) == yamlBefore, "inspection and preview leave both input files byte-identical");
        Check(!Directory.Exists(f.Backups) || Directory.GetFiles(f.Backups, "*", SearchOption.AllDirectories).Length == 0, "preview produces no backup writes");
        Check(!f.Service.HasUndo, "no undo before apply");
        f.Running = true; Check(f.Service.Inspect().ClientRunning, "inspection reports injected running state");
    }

    private static void InvalidSelectionsAndInputs() {
        var f = new Fixture("invalid");
        foreach (string group in new[] { "", "IpEqual", "missing", "FixtureGroup\nother", "FixtureGroup#DIRECT", "FixtureGroup&x=1", "FixtureGroup\r", "FixtureGroup\t" }) {
            var plan = f.Service.Preview(group); Check(!plan.Valid, "unavailable or injected group rejected"); Safe(plan.Format(false), "invalid plan");
            Check(!f.Service.Apply(plan).Ok, "invalid plan cannot apply");
        }
        Check(!f.Service.Apply(null).Ok, "null plan cannot apply");
        var nested = new Fixture("nested-flow-group");
        Write(nested.Yaml, "dns:\n  nameserver: [223.5.5.5]\nproxy-groups:\n  - {type: select, proxies: [{name: NestedFake}]}\n");
        Check(!nested.Service.Preview("NestedFake").Valid, "nested flow node name cannot impersonate an outbound group");
        Write(nested.Yaml, "dns:\n  nameserver: [223.5.5.5]\nproxy-groups:\n  - {name: FixtureGroup, type: select\n");
        Check(!nested.Service.Preview("FixtureGroup").Valid, "unclosed flow mapping cannot authorize a group");
        Write(f.Preferences, "{\"flutter.config\":\"bad " + FakeSecret + "\",\"token\":\"" + FakeSecret + "\"}");
        var broken = f.Service.Inspect(); Check(!broken.Available, "invalid config is unavailable"); Safe(broken.Format(false), "invalid config error");
        Safe(f.Service.Preview("FixtureGroup").Format(true), "invalid config plan error");
        File.Delete(f.Preferences); Directory.CreateDirectory(f.Preferences);
        Check(!f.Service.Inspect().Available && !f.Service.Preview("FixtureGroup").Valid, "directory cannot substitute for preferences file");
    }

    private static void NonstandardStructuresRefused() {
        foreach (string shape in new[] { "override-string", "nameserver-number", "fallback-object", "missing-patch", "missing-dns" }) {
            var f = new Fixture("nonstandard-" + shape);
            f.ChangeConfig(delegate(Dictionary<string, object> config) {
                if (shape == "override-string") config["overrideDns"] = "false";
                else if (shape == "missing-patch") config.Remove("patchClashConfig");
                else {
                    var patch = Object(config["patchClashConfig"]);
                    if (shape == "missing-dns") patch.Remove("dns");
                    else {
                        var dns = Object(patch["dns"]);
                        if (shape == "nameserver-number") dns["nameserver"] = 42;
                        else dns["fallback"] = new Dictionary<string, object> { { "unexpected", "shape" } };
                    }
                }
            });
            byte[] before = File.ReadAllBytes(f.Preferences), yamlBefore = File.ReadAllBytes(f.Yaml);
            var plan = f.Service.Preview("FixtureGroup");
            Check(!plan.Valid, "nonstandard or absent existing configuration cannot be initialized by preview: " + shape);
            Safe(plan.Format(false), "nonstandard structure plan");
            var result = f.Service.Apply(plan);
            Check(!result.Ok, "nonstandard structure cannot apply: " + shape);
            Safe(result.Format(true), "nonstandard structure result");
            Check(File.ReadAllBytes(f.Preferences).SequenceEqual(before) && File.ReadAllBytes(f.Yaml).SequenceEqual(yamlBefore), "nonstandard structure refusal preserves exact input bytes: " + shape);
        }
    }

    private static void SnapshotComparisonScope() {
        var f = new Fixture("comparison-scope");
        f.ChangeConfig(delegate(Dictionary<string, object> config) {
            var dns = Object(Object(config["patchClashConfig"])["dns"]);
            dns["nameserver"] = new[] { "https://dns.alidns.com/dns-query" };
            // YAML has the same policy key/count but the value 223.5.5.5.
            dns["nameserver-policy"] = new Dictionary<string, object> { { "geosite:cn", "114.114.114.114" } };
        });
        byte[] before = File.ReadAllBytes(f.Preferences), yamlBefore = File.ReadAllBytes(f.Yaml);
        var snapshot = f.Service.Inspect(); Check(snapshot.Available, "different policy values with equal policy count are inspectable");
        string chinese = snapshot.Format(false), english = snapshot.Format(true);
        Check(chinese.Contains("主 DNS、回退 DNS 和策略数量"), "Chinese comparison explicitly limits scope to main DNS, fallback DNS and policy count");
        Check(english.IndexOf("main DNS", StringComparison.OrdinalIgnoreCase) >= 0 && english.IndexOf("fallback DNS", StringComparison.OrdinalIgnoreCase) >= 0 && english.IndexOf("policy count", StringComparison.OrdinalIgnoreCase) >= 0, "English comparison explicitly limits scope to main DNS, fallback DNS and policy count");
        Check(!chinese.Contains("DNS 文件字段相符") && !english.Contains("File DNS fields match"), "equal policy counts cannot be presented as complete DNS equivalence");
        Safe(chinese, "limited comparison Chinese"); Safe(english, "limited comparison English");
        Check(File.ReadAllBytes(f.Preferences).SequenceEqual(before) && File.ReadAllBytes(f.Yaml).SequenceEqual(yamlBefore), "limited file comparison changes no input bytes");
    }

    private static void ProcessAndStaleGuards() {
        var f = new Fixture("process"); var plan = f.Service.Preview("FixtureGroup"); string before = File.ReadAllText(f.Preferences);
        f.Running = true; var blocked = f.Service.Apply(plan);
        Check(!blocked.Ok && File.ReadAllText(f.Preferences) == before, "running client or core blocks apply without changes"); Safe(blocked.Format(true), "running error");
        f.Running = false;
        var stalePrefs = f.Service.Preview("FixtureGroup"); File.AppendAllText(f.Preferences, " ", Utf8); string changed = File.ReadAllText(f.Preferences);
        Check(!f.Service.Apply(stalePrefs).Ok && File.ReadAllText(f.Preferences) == changed, "preferences changed after preview rejects stale plan");
        var staleYaml = f.Service.Preview("FixtureGroup"); File.AppendAllText(f.Yaml, "# independent change\n", Utf8); changed = File.ReadAllText(f.Preferences);
        Check(!f.Service.Apply(staleYaml).Ok && File.ReadAllText(f.Preferences) == changed, "generated YAML changed after preview rejects stale plan");

        var race = new Fixture("process-race"); var racePlan = race.Service.Preview("FixtureGroup"); before = File.ReadAllText(race.Preferences); int probes = 0;
        race.Probe = delegate { probes++; return probes >= 2; };
        Check(!race.Service.Apply(racePlan).Ok && probes >= 2 && File.ReadAllText(race.Preferences) == before, "process appeared during apply is rejected before replacement");
        race.Probe = delegate { return false; };
        Check(!race.Service.HasUndo && File.ReadAllText(race.Preferences) == before, "unapplied aborted backup does not offer an undo or change original file");

        var a = new Fixture("cross-plan-a"); var b = new Fixture("cross-plan-b"); before = File.ReadAllText(b.Preferences);
        Check(!b.Service.Apply(a.Service.Preview("FixtureGroup")).Ok && File.ReadAllText(b.Preferences) == before, "plan cannot be applied by another service instance");
    }

    private static void FailedLaterApplyKeepsEarlierUndo() {
        var f = new Fixture("older-undo-survives-failure");
        var originalConfig = f.Config(); var originalOuter = f.Outer(); byte[] originalYaml = File.ReadAllBytes(f.Yaml);
        Check(f.Service.Apply(f.Service.Preview("FixtureGroup")).Ok, "first successful operation creates an undo");
        byte[] firstApplied = File.ReadAllBytes(f.Preferences);
        var secondPlan = f.Service.Preview("Work Proxy"); Check(secondPlan.Valid, "second distinct group produces a valid plan");
        int probes = 0; f.Probe = delegate { probes++; return probes >= 2; };
        var secondResult = f.Service.Apply(secondPlan);
        Check(!secondResult.Ok && probes >= 2 && File.ReadAllBytes(f.Preferences).SequenceEqual(firstApplied), "later process-race failure preserves exact first applied file bytes");
        f.Probe = delegate { return false; };
        Check(f.NewService().HasUndo, "aborted later backup cannot hide an earlier successful undo across service instances");
        var undone = f.NewService().Undo(); Check(undone.Ok, "undo still targets the earlier successful operation"); Safe(undone.Format(true), "older undo result");
        Check(Equal(f.Config(), originalConfig), "earlier undo restores original config field values");
        var restoredOuter = f.Outer();
        Check(originalOuter.Where(x => x.Key != "flutter.config").All(x => restoredOuter.ContainsKey(x.Key) && Equal(x.Value, restoredOuter[x.Key])), "earlier undo preserves all unrelated outer fields");
        Check(File.ReadAllBytes(f.Yaml).SequenceEqual(originalYaml), "failed later apply and earlier undo never modify generated YAML");
        Check(!f.NewService().HasUndo, "aborted later operation stays excluded after earlier operation is undone");
    }

    private static void ApplyPreservationAndBackup() {
        var f = new Fixture("apply"); string beforeText = File.ReadAllText(f.Preferences); byte[] beforeBytes = File.ReadAllBytes(f.Preferences); string yaml = File.ReadAllText(f.Yaml);
        var beforeOuter = f.Outer(); var beforeConfig = f.Config(); var beforeDns = f.Dns();
        var result = f.Service.Apply(f.Service.Preview("Work Proxy"));
        Check(result.Ok, "valid preview applies"); Safe(result.Format(false), "apply result Chinese"); Safe(result.Format(true), "apply result English");
        var afterOuter = f.Outer(); var afterConfig = f.Config(); var afterDns = f.Dns();
        Check((bool)afterConfig["overrideDns"], "DNS override enabled");
        Check(Equal(afterDns["nameserver"], new[] { "https://1.1.1.1/dns-query#Work%20Proxy", "https://8.8.8.8/dns-query#Work%20Proxy" }), "encrypted resolvers use safely escaped user-selected existing group");
        Check(ArrayOf(afterDns["fallback"]).Length == 0, "parallel fallback cleared");
        var policy = Object(afterDns["nameserver-policy"]);
        Check(!policy.ContainsKey("www.baidu.com") && !policy.ContainsKey("geosite:cn"), "explicit domestic policy exceptions removed");
        foreach (string key in new[] { "+.internal.crop.com", "geosite:private", "geosite:cn,private", "unknown.fixture.example" }) Check(policy.ContainsKey(key) && Equal(policy[key], Object(beforeDns["nameserver-policy"])[key]), "private and unknown policy entries retained");
        foreach (var item in beforeDns.Where(x => x.Key != "nameserver" && x.Key != "fallback" && x.Key != "nameserver-policy")) Check(afterDns.ContainsKey(item.Key) && Equal(item.Value, afterDns[item.Key]), "other DNS field retained: " + item.Key);
        foreach (var item in beforeConfig.Where(x => x.Key != "overrideDns" && x.Key != "patchClashConfig")) Check(Equal(item.Value, afterConfig[item.Key]), "other config field retained: " + item.Key);
        var oldPatch = Object(beforeConfig["patchClashConfig"]); var newPatch = Object(afterConfig["patchClashConfig"]);
        foreach (var item in oldPatch.Where(x => x.Key != "dns")) Check(Equal(item.Value, newPatch[item.Key]), "non-DNS patch field retained: " + item.Key);
        foreach (var item in beforeOuter.Where(x => x.Key != "flutter.config")) Check(Equal(item.Value, afterOuter[item.Key]), "outside flutter.config field retained: " + item.Key);
        Check(File.ReadAllText(f.Yaml) == yaml, "generated YAML is read-only");
        Check(File.ReadAllText(f.Preferences).Contains("\"numeric\":1.2500"), "unrelated JSON lexical representation preserved");
        string backupFile = f.Backup("preferences.before.bin");
        Check(File.ReadAllBytes(backupFile).SequenceEqual(beforeBytes), "full backup readback equals exact original bytes");
        var acl = Directory.GetAccessControl(Path.GetDirectoryName(backupFile));
        Check(acl.AreAccessRulesProtected, "backup directory ACL inheritance disabled");
        var sid = WindowsIdentity.GetCurrent().User;
        Check(acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().Where(x => x.AccessControlType == AccessControlType.Allow).All(x => x.IdentityReference.Equals(sid)), "backup allow grants restricted to current user");
        string receipt = File.ReadAllText(f.Backup("receipt.json"), Utf8); Safe(receipt, "limited undo receipt");
        Check(!receipt.Contains("flutter.account") && !receipt.Contains("fixture-theme") && !receipt.Contains("subscription"), "receipt contains no unrelated configuration fields");
        Check(f.Service.HasUndo && f.NewService().HasUndo, "undo discoverable across service instances");
        f.Running = true; beforeText = File.ReadAllText(f.Preferences); Check(!f.Service.Undo().Ok && File.ReadAllText(f.Preferences) == beforeText, "running client or core blocks undo");
    }

    private static void UndoPreservesLaterChanges() {
        var f = new Fixture("undo"); var before = f.Config(); var beforeDns = f.Dns();
        Check(f.Service.Apply(f.Service.Preview("FixtureGroup")).Ok, "undo fixture applies");
        f.ChangeConfig(delegate(Dictionary<string, object> config) {
            config["theme"] = "new-later-theme";
            var patch = Object(config["patchClashConfig"]); patch["ipv6"] = true;
            var dns = Object(patch["dns"]); dns["custom-dns-option"] = "later-dns-option";
            Object(dns["nameserver-policy"])["later.private.fixture"] = "10.10.0.1";
            Object(dns["nameserver-policy"])["unknown.fixture.example"] = "10.20.0.1";
        });
        var outer = f.Outer(); outer["laterTopLevel"] = "later-top-level"; Write(f.Preferences, Json.Serialize(outer));
        var result = f.NewService().Undo(); Check(result.Ok, "field-level undo works after unrelated later edits"); Safe(result.Format(false), "undo result");
        var after = f.Config(); var dnsAfter = f.Dns();
        Check(Equal(after["overrideDns"], before["overrideDns"]) && Equal(dnsAfter["nameserver"], beforeDns["nameserver"]) && Equal(dnsAfter["fallback"], beforeDns["fallback"]), "undo restores only changed scalar and list fields");
        var policies = Object(dnsAfter["nameserver-policy"]); var oldPolicies = Object(beforeDns["nameserver-policy"]);
        Check(oldPolicies.Where(x => x.Key != "unknown.fixture.example").All(x => policies.ContainsKey(x.Key) && Equal(x.Value, policies[x.Key])) && (string)policies["later.private.fixture"] == "10.10.0.1" && (string)policies["unknown.fixture.example"] == "10.20.0.1", "undo restores removed policy entries and retains later new and edited unknown entries");
        Check((string)after["theme"] == "new-later-theme" && (bool)Object(after["patchClashConfig"])["ipv6"] && (string)dnsAfter["custom-dns-option"] == "later-dns-option" && (string)f.Outer()["laterTopLevel"] == "later-top-level", "undo preserves later non-owned changes");
        Check(File.Exists(f.Backup("preferences.before.bin")), "undo retains full original backup");
    }

    private static void UndoConflicts() {
        foreach (string field in new[] { "nameserver", "fallback", "overrideDns", "removed-policy" }) {
            var f = new Fixture("conflict-" + field); Check(f.Service.Apply(f.Service.Preview("FixtureGroup")).Ok, "conflict fixture applies");
            f.ChangeConfig(delegate(Dictionary<string, object> config) {
                var dns = Object(Object(config["patchClashConfig"])["dns"]);
                if (field == "overrideDns") config["overrideDns"] = false;
                else if (field == "removed-policy") Object(dns["nameserver-policy"])["geosite:cn"] = "independently-edited";
                else dns[field] = new[] { "https://9.9.9.9/dns-query" };
            });
            string before = File.ReadAllText(f.Preferences);
            var result = f.Service.Undo(); Check(!result.Ok && File.ReadAllText(f.Preferences) == before, "conflicting tool-owned field refuses whole undo without overwrite"); Safe(result.Format(true), "conflict result");
        }
    }

    private static void BackupAndReceiptTampering() {
        var backup = new Fixture("tampered-backup"); Check(backup.Service.Apply(backup.Service.Preview("FixtureGroup")).Ok, "backup tampering fixture applies");
        File.AppendAllText(backup.Backup("preferences.before.bin"), "tampered", Utf8); string before = File.ReadAllText(backup.Preferences);
        Check(!backup.NewService().Undo().Ok && File.ReadAllText(backup.Preferences) == before, "tampered full backup cannot restore");
        var receipt = new Fixture("tampered-receipt"); Check(receipt.Service.Apply(receipt.Service.Preview("FixtureGroup")).Ok, "receipt tampering fixture applies");
        Write(receipt.Backup("receipt.json"), "{}"); before = File.ReadAllText(receipt.Preferences);
        Check(!receipt.NewService().Undo().Ok && File.ReadAllText(receipt.Preferences) == before, "invalid receipt cannot restore");
    }

    private static void MakeJunction(string link, string target) {
        string prefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        Check(Path.GetFullPath(link).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Path.GetFullPath(target).StartsWith(prefix, StringComparison.OrdinalIgnoreCase), "junction targets confined to temporary fixture root");
        var start = new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + link + "\" \"" + target + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (var process = Process.Start(start)) { process.StandardOutput.ReadToEnd(); process.StandardError.ReadToEnd(); process.WaitForExit(); Check(process.ExitCode == 0, "fixture junction created"); }
    }
    private static void ReparsePaths() {
        var f = new Fixture("reparse-source"); string link = Path.Combine(f.DirectoryPath, "linked-data"); MakeJunction(link, f.Data);
        var service = new NetworkDnsService(link, f.Backups, delegate { return false; }); string before = File.ReadAllText(f.Preferences);
        Check(!service.Inspect().Available && !service.Preview("FixtureGroup").Valid && File.ReadAllText(f.Preferences) == before, "reparse data path refused");
        var backup = new Fixture("reparse-backup"); string outside = Path.Combine(backup.DirectoryPath, "outside-backup"); Directory.CreateDirectory(outside); MakeJunction(backup.Backups, outside); before = File.ReadAllText(backup.Preferences);
        Check(!backup.Service.Apply(backup.Service.Preview("FixtureGroup")).Ok && File.ReadAllText(backup.Preferences) == before && Directory.GetFileSystemEntries(outside).Length == 0, "reparse backup path refused before writing through it");
    }

    private static int HoldMutexFixture(string[] args) {
        if (args.Length != 4) return 31;
        string temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (string path in args.Skip(1)) if (!Path.GetFullPath(path).StartsWith(temporary, StringComparison.OrdinalIgnoreCase) || path.IndexOf("whale-network-dns-tests-", StringComparison.OrdinalIgnoreCase) < 0) return 32;
        using (var mutex = new Mutex(false, NetworkDnsService.OperationMutexName(args[1]))) {
            if (!mutex.WaitOne(1000)) return 33;
            try {
                Write(args[2], "ready"); var watch = Stopwatch.StartNew();
                while (!File.Exists(args[3]) && watch.ElapsedMilliseconds < 10000) Thread.Sleep(20);
                return File.Exists(args[3]) ? 0 : 34;
            } finally { mutex.ReleaseMutex(); }
        }
    }
    private static void WhileOtherProcessOwnsMutex(Fixture fixture, Action action) {
        string ready = Path.Combine(fixture.DirectoryPath, "mutex-ready-" + Guid.NewGuid().ToString("N"));
        string release = Path.Combine(fixture.DirectoryPath, "mutex-release-" + Guid.NewGuid().ToString("N"));
        var start = new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,
            "--hold-dns-mutex \"" + fixture.Preferences + "\" \"" + ready + "\" \"" + release + "\"") {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        using (var worker = Process.Start(start)) {
            try {
                var watch = Stopwatch.StartNew();
                while (!File.Exists(ready) && !worker.HasExited && watch.ElapsedMilliseconds < 5000) Thread.Sleep(20);
                Check(File.Exists(ready), "separate fixture process acquired shared operation mutex");
                action();
            } finally { Write(release, "release"); Check(worker.WaitForExit(12000) && worker.ExitCode == 0, "fixture mutex holder released and exited normally"); }
        }
    }
    private static void CrossProcessOperationGuard() {
        var fixture = new Fixture("cross-process-lock"); string before = File.ReadAllText(fixture.Preferences); var plan = fixture.Service.Preview("FixtureGroup");
        WhileOtherProcessOwnsMutex(fixture, delegate {
            var watch = Stopwatch.StartNew(); var result = fixture.Service.Apply(plan);
            Check(!result.Ok && result.Code == "operation_busy" && watch.ElapsedMilliseconds < 2000, "concurrent apply returns promptly with busy explanation");
            Check(File.ReadAllText(fixture.Preferences) == before && !Directory.Exists(fixture.Backups), "busy apply creates neither backup nor preference writes");
            Safe(result.Format(false), "busy apply result");
        });
        Check(fixture.Service.Apply(plan).Ok, "apply succeeds after other process releases mutex");
        string after = File.ReadAllText(fixture.Preferences), receipt = File.ReadAllText(fixture.Backup("receipt.json"));
        WhileOtherProcessOwnsMutex(fixture, delegate {
            var result = fixture.NewService().Undo();
            Check(!result.Ok && result.Code == "operation_busy", "concurrent undo uses the same operation mutex");
            Check(File.ReadAllText(fixture.Preferences) == after && File.ReadAllText(fixture.Backup("receipt.json")) == receipt, "busy undo changes neither preferences nor receipt");
        });
        Check(fixture.NewService().Undo().Ok, "undo succeeds after other process releases mutex");
    }

    public static int Main(string[] args) {
        if (args.Length != 0 && args[0] == "--hold-dns-mutex") return HoldMutexFixture(args);
        root = Path.Combine(Path.GetTempPath(), "whale-network-dns-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        Run("inspect and preview", InspectAndPreview);
        Run("invalid selections and inputs", InvalidSelectionsAndInputs);
        Run("nonstandard existing structures", NonstandardStructuresRefused);
        Run("file comparison scope", SnapshotComparisonScope);
        Run("process and stale guards", ProcessAndStaleGuards);
        Run("earlier undo survives later failed apply", FailedLaterApplyKeepsEarlierUndo);
        Run("apply, preservation and private backup", ApplyPreservationAndBackup);
        Run("field-level undo preserves later changes", UndoPreservesLaterChanges);
        Run("undo conflicts", UndoConflicts);
        Run("backup and receipt tampering", BackupAndReceiptTampering);
        Run("reparse-point guards", ReparsePaths);
        Run("cross-process operation guard", CrossProcessOperationGuard);
        Console.WriteLine("NetworkDnsTests: " + checks + " checks, " + failures + " failed groups. Temporary fixtures only; no real CuteCloud configuration, client, core or network operations.");
        Console.WriteLine("Fixture evidence: " + root);
        return failures == 0 ? 0 : 1;
    }
}
