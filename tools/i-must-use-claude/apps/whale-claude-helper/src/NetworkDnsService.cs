using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace WhaleClaudeHelper {
    internal sealed class NetworkDnsSnapshot {
        internal bool Available, ClientRunning;
        internal string[] ProxyGroups = new string[0];
        internal bool OverrideDns, FilesDiffer;
        internal int PreferenceServers, GeneratedServers, RetainedPolicies;
        internal string Code;
        internal DateTime PreferencesTime, GeneratedTime;
        internal string Format(bool english) {
            if (!Available) return NetworkDnsService.ErrorText(Code, english);
            if (english) return "CuteCloud DNS file inspection (not live core memory)\r\n" +
                "Client or core running: " + (ClientRunning ? "yes" : "no") + "; DNS override: " + (OverrideDns ? "on" : "off") + ".\r\n" +
                "Preferences: " + PreferenceServers + " main resolvers; generated YAML: " + GeneratedServers + ". Compared only main DNS, fallback DNS and policy count: " + (FilesDiffer ? "different" : "matching") + ". Policy values were not checked; other DNS fields were not compared.\r\n" +
                "Preferences UTC: " + Stamp(PreferencesTime) + "; YAML UTC: " + Stamp(GeneratedTime) + ".\r\n" +
                "Existing outbound groups: " + ProxyGroups.Length + "; retained internal/unknown DNS policies: " + RetainedPolicies + ".\r\n" +
                "No credentials, subscriptions or node addresses are displayed. This does not establish which configuration is loaded in memory, nor verify DNS leakage.";
            return "CuteCloud DNS 文件核对（不是核心内存状态）\r\n客户端或核心正在运行：" + (ClientRunning ? "是" : "否") + "；覆盖 DNS：" + (OverrideDns ? "开启" : "关闭") + "。\r\n" +
                "偏好中主 DNS 数量：" + PreferenceServers + "；生成 YAML 中：" + GeneratedServers + "。仅对照主 DNS、回退 DNS 和策略数量：" + (FilesDiffer ? "存在差异" : "相符") + "；策略值未核对，其他 DNS 字段未逐项比较。\r\n" +
                "偏好 UTC：" + Stamp(PreferencesTime) + "；YAML UTC：" + Stamp(GeneratedTime) + "。\r\n已有出站组：" + ProxyGroups.Length + "；保留的内网或未知 DNS 例外：" + RetainedPolicies + "。\r\n" +
                "不展示账户、订阅或节点地址；文件核对不能证明核心当前加载状态，也不代表 DNS 泄露测试通过。";
        }
        private static string Stamp(DateTime time) { return time.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture); }
    }

    internal sealed class NetworkDnsPlan {
        internal readonly bool Valid;
        internal readonly object Owner;
        internal readonly string PreferencesHash, YamlHash, AfterHash, Group, Code, TargetPath;
        internal readonly string[] RemovedRuleNames;
        internal readonly int RemovedPolicies, RetainedPolicies, Changes;
        internal NetworkDnsPlan(object owner, string prefHash, string yamlHash, string afterHash, string group, string targetPath, string[] removedRules, int retained, int changes) {
            Valid = true; Owner = owner; PreferencesHash = prefHash; YamlHash = yamlHash; AfterHash = afterHash; Group = group;
            TargetPath = targetPath; RemovedRuleNames = removedRules; RemovedPolicies = removedRules.Length; RetainedPolicies = retained; Changes = changes;
        }
        internal NetworkDnsPlan(string code) { Code = code; }
        internal string Format(bool english) {
            if (!Valid) return NetworkDnsService.ErrorText(Code, english);
            string endpoints = "https://1.1.1.1/dns-query#" + Uri.EscapeDataString(Group) + "\r\nhttps://8.8.8.8/dns-query#" + Uri.EscapeDataString(Group);
            if (english) return "Preview only; nothing has been written.\r\nTarget: " + TargetPath + "\r\nOutbound group: " + Group + "\r\n" +
                "Enable DNS override; write exactly these main DNS endpoints and clear fallback:\r\n" + endpoints + "\r\n" +
                "Remove " + RemovedPolicies + " explicitly recognized domestic DNS exceptions: " + (RemovedRuleNames.Length == 0 ? "none" : String.Join(", ", RemovedRuleNames)) + ". Retain " + RetainedPolicies + " internal, mixed or unknown policies. Retained policies can still select other resolvers.\r\n" +
                "Bootstrap DNS, proxy-node DNS, IPv6 and every other setting remain unchanged. Proxy bootstrap queries can still reach the retained resolvers. " + Changes + " fields need a change.\r\n" +
                "Apply requires both CuteCloud and FlClashCore to be fully exited. A private, verified full backup and a limited-field undo receipt will be saved first. This helper never stops or starts them.\r\n" +
                "After applying, start CuteCloud yourself and check its effective DNS settings, then run Check network in this helper again. A successful file edit is not a leakage fix verdict.";
            return "仅预览，尚未写入。\r\n目标文件：" + TargetPath + "\r\n选定出站组：" + Group + "\r\n开启覆盖 DNS；主 DNS 精确写入以下两条端点，并清空回退 DNS：\r\n" + endpoints + "\r\n" +
                "移除 " + RemovedPolicies + " 条明确识别的国内 DNS 例外：" + (RemovedRuleNames.Length == 0 ? "无" : String.Join("、", RemovedRuleNames)) + "。保留 " + RetainedPolicies + " 条内网、混合或未知策略。保留策略仍可能使用其他解析器。\r\n" +
                "引导 DNS、代理节点 DNS、IPv6 和其他设置均保留；代理节点引导查询仍可能使用保留的解析器。需要修改 " + Changes + " 个字段。\r\n" +
                "应用前必须正常退出 CuteCloud 和 FlClashCore；先建立私密完整备份并读回核验，再保存有限字段撤回记录。本助手不会停止或启动它们。\r\n" +
                "应用后请自行启动 CuteCloud、核对其生效设置，再回到助手点击「检查网络」重新实测。文件修改成功不等于泄露已修好。";
        }
    }

    internal sealed class NetworkDnsActionResult {
        internal bool Ok;
        internal string Code;
        internal bool MayHaveChanged;
        internal string Format(bool english) {
            if (Ok && Code == "undone") return english ? "The latest tool-owned DNS fields were restored and verified; unrelated later settings were preserved. Start CuteCloud yourself, then use Check network here for a fresh test. Undo itself did not run a DNS test." : "最近一次由本工具修改的 DNS 字段已撤回并核验，后续其他设置保留。请自行启动 CuteCloud后，在助手点击「检查网络」重新实测；撤回操作本身未检测 DNS。";
            if (Ok && Code == "no_changes") return english ? "These preferences already match the preview; no file was changed. This is not a DNS leakage verdict." : "偏好已符合预览，无需修改文件；这不代表 DNS 泄露测试通过。";
            if (Ok) return english ? "DNS preferences were written atomically and read back successfully. A verified private backup and limited-field undo receipt were retained. Start CuteCloud yourself, inspect the applied settings, and run a fresh DNS test. No process was started and no leakage result was claimed." : "DNS 偏好已原子写入并读回核验，私密完整备份与有限字段撤回记录已保留。请自行启动 CuteCloud、核对生效设置并重新测试 DNS；本助手未启动进程，也未判定泄露修复成功。";
            return NetworkDnsService.ErrorText(Code, english) + (MayHaveChanged ? (english ? "\r\nThe preference file may already have changed; the backup is retained. Inspect or use Undo before trying again." : "\r\n偏好文件可能已经写入；备份保留，请核对或使用撤回后再操作。") : "");
        }
    }

    // Offline, explicitly invoked CuteCloud preference editing only. No network, IPC,
    // process launch/termination, system DNS, generated YAML or subscription edits.
    internal sealed class NetworkDnsService {
        private const int Limit = 8 * 1024 * 1024;
        private const int ReceiptLimit = 128 * 1024;
        private static readonly object Gate = new object();
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly object owner = new object();
        private readonly string dataDirectory, backupRoot, preferencesPath, yamlPath;
        private readonly Func<bool> runningProbe;
        private static readonly string[] BasePath = { "patchClashConfig", "dns" };
        private static readonly HashSet<string> DomesticKeys = new HashSet<string>(new[] { "www.baidu.com", "+.baidu.com", "geosite:cn", "geosite:geolocation-cn" }, StringComparer.OrdinalIgnoreCase);

        internal NetworkDnsService() : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cutecloud", "CuteCloud"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DafeiyuClaudeHelper", "Backups", "NetworkDns"),
            ClientsRunning) { }

        // Injection is used by fixture tests; the public workflow uses only the defaults above.
        internal NetworkDnsService(string dataDirectory, string backupRoot, Func<bool> runningProbe) {
            if (dataDirectory == null || backupRoot == null || runningProbe == null) throw new ArgumentException("invalid_context");
            this.dataDirectory = Path.GetFullPath(dataDirectory); this.backupRoot = Path.GetFullPath(backupRoot); this.runningProbe = runningProbe;
            preferencesPath = Path.Combine(this.dataDirectory, "shared_preferences.json"); yamlPath = Path.Combine(this.dataDirectory, "config.yaml");
        }

        internal NetworkDnsSnapshot Inspect() {
            var snapshot = new NetworkDnsSnapshot();
            lock (Gate) {
                try {
                    snapshot.ClientRunning = runningProbe();
                    var inputs = Load();
                    snapshot.ProxyGroups = inputs.Yaml.Groups.ToArray();
                    snapshot.OverrideDns = BooleanField(inputs.Config, new[] { "overrideDns" });
                    snapshot.PreferenceServers = StringList(Field(inputs.Config, DnsPath("nameserver")), inputs.Config).Count;
                    snapshot.GeneratedServers = inputs.Yaml.NameServers.Count;
                    // A bounded field comparison only. No claim about the core's in-memory config.
                    snapshot.FilesDiffer = !StringList(Field(inputs.Config, DnsPath("nameserver")), inputs.Config).SequenceEqual(inputs.Yaml.NameServers, StringComparer.Ordinal) ||
                        !StringList(Field(inputs.Config, DnsPath("fallback")), inputs.Config).SequenceEqual(inputs.Yaml.Fallback, StringComparer.Ordinal) ||
                        PolicyCount(inputs.Config) != inputs.Yaml.PolicyCount;
                    snapshot.RetainedPolicies = PolicyCount(inputs.Config) - RemovablePolicies(inputs.Config).Count;
                    snapshot.PreferencesTime = File.GetLastWriteTimeUtc(preferencesPath); snapshot.GeneratedTime = File.GetLastWriteTimeUtc(yamlPath);
                    snapshot.Available = true;
                } catch (Exception error) { snapshot.Code = ErrorCode(error); }
            }
            return snapshot;
        }

        internal NetworkDnsPlan Preview(string proxyGroup) {
            lock (Gate) {
                try {
                    var input = Load(); RequireGroup(proxyGroup, input.Yaml.Groups);
                    var edit = Project(input.Config, proxyGroup);
                    byte[] after = EncodeLike(ReplaceFlutter(input.Preferences, edit.Text), input.Bytes);
                    string[] removedNames = edit.Fields.Where(f => f.Path.Count == 4 && f.Path[2] == "nameserver-policy").Select(f => f.Path[3]).ToArray();
                    return new NetworkDnsPlan(owner, Hash(input.Bytes), Hash(input.YamlBytes), Hash(after), proxyGroup, preferencesPath, removedNames, edit.Retained, edit.Fields.Count);
                } catch (Exception error) { return new NetworkDnsPlan(ErrorCode(error)); }
            }
        }

        internal NetworkDnsActionResult Apply(NetworkDnsPlan plan) {
            lock (Gate) {
                var result = new NetworkDnsActionResult();
                string preparedFolder = null; Receipt pendingReceipt = null;
                IDisposable operation = null;
                try {
                    if (plan == null || !plan.Valid || !Object.ReferenceEquals(owner, plan.Owner)) throw Problem("invalid_plan");
                    operation = new OperationLock(preferencesPath);
                    RequireClosed(); var input = Load();
                    if (Hash(input.Bytes) != plan.PreferencesHash || Hash(input.YamlBytes) != plan.YamlHash) throw Problem("file_changed");
                    RequireGroup(plan.Group, input.Yaml.Groups);
                    var edit = Project(input.Config, plan.Group);
                    byte[] after = EncodeLike(ReplaceFlutter(input.Preferences, edit.Text), input.Bytes);
                    if (Hash(after) != plan.AfterHash) throw Problem("invalid_plan");
                    if (edit.Fields.Count == 0) { result.Ok = true; result.Code = "no_changes"; return result; }
                    string folder = NewBackup(); preparedFolder = folder;
                    string blob = Path.Combine(folder, "preferences.before.bin");
                    WritePrivate(blob, input.Bytes); RequirePrivateFile(blob);
                    if (Hash(Read(blob, Limit)) != plan.PreferencesHash) throw Problem("backup_invalid");
                    var receipt = new Receipt { Version = 1, Kind = "whale-network-dns", State = "prepared", BeforeHash = plan.PreferencesHash,
                        AfterHash = plan.AfterHash, YamlHash = plan.YamlHash, Group = plan.Group, Fields = edit.Fields };
                    pendingReceipt = receipt;
                    SaveReceipt(folder, receipt);
                    // Prove the durable receipt and complete original are readable before touching preferences.
                    VerifyReceipt(folder);
                    RequireClosed(); RequireHash(yamlPath, plan.YamlHash); RequireHash(preferencesPath, plan.PreferencesHash);
                    ReplaceChecked(preferencesPath, after, plan.PreferencesHash, delegate { RequireClosed(); RequireHash(yamlPath, plan.YamlHash); }, delegate { result.MayHaveChanged = true; });
                    receipt.State = "applied"; SaveReceipt(folder, receipt);
                    result.Ok = true; result.MayHaveChanged = false; result.Code = "applied";
                } catch (Exception error) {
                    result.Code = ErrorCode(error);
                    // A pre-write failure must not hide an older, usable undo record.
                    if (pendingReceipt != null && preparedFolder != null) {
                        try {
                            if (!result.MayHaveChanged || Hash(Read(preferencesPath, Limit)) == pendingReceipt.BeforeHash) {
                                pendingReceipt.State = "aborted"; SaveReceipt(preparedFolder, pendingReceipt); result.MayHaveChanged = false;
                            }
                        } catch { /* Keep the verified prepared backup if state finalization is unavailable. */ }
                    }
                } finally { if (operation != null) operation.Dispose(); }
                return result;
            }
        }

        internal bool HasUndo {
            get { lock (Gate) { try { string folder = LatestBackup(); return folder != null && ReadReceipt(folder).State != "undone"; } catch { return false; } } }
        }

        internal NetworkDnsActionResult Undo() {
            lock (Gate) {
                var result = new NetworkDnsActionResult();
                IDisposable operation = null;
                try {
                    operation = new OperationLock(preferencesPath);
                    RequireClosed(); string folder = LatestBackup(); if (folder == null) throw Problem("no_undo");
                    var saved = VerifyReceipt(folder); if (saved.Receipt.State == "undone") throw Problem("no_undo");
                    byte[] currentBytes = Read(preferencesPath, Limit); string current = Decode(currentBytes), currentConfig = ExtractFlutter(current);
                    if (saved.Receipt.State == "prepared" && Hash(currentBytes) == saved.Receipt.BeforeHash) {
                        saved.Receipt.State = "undone"; SaveReceipt(folder, saved.Receipt);
                        result.Ok = true; result.Code = "no_changes"; return result;
                    }
                    string originalConfig = ExtractFlutter(Decode(saved.Before));
                    var projected = Project(originalConfig, saved.Receipt.Group);
                    // Validate every owned field first. A conflict aborts the entire undo, never a partial overwrite.
                    foreach (var record in projected.Fields) {
                        string[] path = record.Path.ToArray(); var actual = Field(currentConfig, path);
                        if (FieldHash(currentConfig, actual) != record.AfterHash) throw Problem("undo_conflict");
                    }
                    foreach (var record in projected.Fields) {
                        string[] path = record.Path.ToArray(); var before = Field(originalConfig, path);
                        currentConfig = Put(currentConfig, path, before == null ? null : Literal(originalConfig, before));
                    }
                    byte[] restored = EncodeLike(ReplaceFlutter(current, currentConfig), currentBytes);
                    RequireClosed();
                    ReplaceChecked(preferencesPath, restored, Hash(currentBytes), RequireClosed, delegate { result.MayHaveChanged = true; });
                    saved.Receipt.State = "undone"; SaveReceipt(folder, saved.Receipt);
                    result.Ok = true; result.MayHaveChanged = false; result.Code = "undone";
                } catch (Exception error) { result.Code = ErrorCode(error); }
                finally { if (operation != null) operation.Dispose(); }
                return result;
            }
        }

        private Input Load() {
            byte[] bytes = Read(preferencesPath, Limit), yaml = Read(yamlPath, Limit);
            string pref = Decode(bytes), config = ExtractFlutter(pref);
            ValidateConfig(config);
            return new Input { Bytes = bytes, YamlBytes = yaml, Preferences = pref, Config = config, Yaml = ReadYaml(Decode(yaml)) };
        }

        private static Edit Project(string config, string group) {
            ValidateGroup(group); ValidateConfig(config);
            var edit = new Edit { Text = config };
            List<string> removed = RemovablePolicies(config); edit.Removed = removed.Count; edit.Retained = PolicyCount(config) - removed.Count;
            Change(edit, new[] { "overrideDns" }, "true");
            string fragment = Uri.EscapeDataString(group);
            string[] servers = { "https://1.1.1.1/dns-query#" + fragment, "https://8.8.8.8/dns-query#" + fragment };
            foreach (string server in servers) ValidateEndpoint(server, group);
            Change(edit, DnsPath("nameserver"), Json().Serialize(servers));
            Change(edit, DnsPath("fallback"), "[]");
            foreach (string key in removed) Change(edit, new[] { "patchClashConfig", "dns", "nameserver-policy", key }, null);
            return edit;
        }

        private static void Change(Edit edit, string[] path, string replacement) {
            var existing = Field(edit.Text, path); string beforeHash = FieldHash(edit.Text, existing);
            string changed = Put(edit.Text, path, replacement); string afterHash = FieldHash(changed, Field(changed, path));
            if (beforeHash == afterHash) return;
            edit.Fields.Add(new FieldRecord { Path = path.ToList(), BeforeHash = beforeHash, AfterHash = afterHash }); edit.Text = changed;
        }

        private static void ValidateConfig(string config) {
            RequireObject(Field(config, BasePath));
            Node flag = Field(config, new[] { "overrideDns" });
            if (flag == null || (Literal(config, flag) != "true" && Literal(config, flag) != "false")) throw Problem("invalid_json");
            Node nameserver = Field(config, DnsPath("nameserver"));
            if (nameserver == null || nameserver.Kind != '[') throw Problem("invalid_json");
            StringList(nameserver, config); StringList(Field(config, DnsPath("fallback")), config); PolicyCount(config);
        }

        private static List<string> RemovablePolicies(string config) {
            var policy = Field(config, DnsPath("nameserver-policy"));
            if (policy == null || Literal(config, policy) == "null") return new List<string>();
            RequireObject(policy);
            return policy.Members.Where(m => DomesticKeys.Contains(m.Name) && IsDomesticValue(config, m.Value)).Select(m => m.Name).ToList();
        }
        private static int PolicyCount(string config) {
            var policy = Field(config, DnsPath("nameserver-policy")); if (policy == null || Literal(config, policy) == "null") return 0;
            RequireObject(policy); return policy.Members.Count;
        }
        private static bool IsDomesticValue(string text, Node value) {
            List<string> values;
            if (value.Kind == '"') values = new List<string> { Json().Deserialize<string>(Literal(text, value)) };
            else if (value.Kind == '[') values = StringList(value, text);
            else return false;
            return values.Count != 0 && values.All(IsDomesticResolver);
        }
        private static bool IsDomesticResolver(string value) {
            if (new[] { "223.5.5.5", "223.6.6.6", "119.29.29.29", "119.28.28.28", "114.114.114.114", "114.114.115.115", "180.76.76.76" }.Contains(value, StringComparer.Ordinal)) return true;
            Uri uri; if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || !String.IsNullOrEmpty(uri.UserInfo) || !String.IsNullOrEmpty(uri.Query) || !String.IsNullOrEmpty(uri.Fragment)) return false;
            return (uri.Scheme == "https" && uri.AbsolutePath == "/dns-query" && new[] { "dns.alidns.com", "doh.pub" }.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)) ||
                (uri.Scheme == "tls" && new[] { "dns.alidns.com", "dot.pub" }.Contains(uri.Host, StringComparer.OrdinalIgnoreCase));
        }
        private static void ValidateGroup(string group) {
            if (String.IsNullOrWhiteSpace(group) || group.Length > 160 || group != group.Trim() || group.Any(c => Char.IsControl(c) || Char.GetUnicodeCategory(c) == UnicodeCategory.Format || Char.GetUnicodeCategory(c) == UnicodeCategory.LineSeparator || Char.GetUnicodeCategory(c) == UnicodeCategory.ParagraphSeparator || "#&%?=\\".IndexOf(c) >= 0)) throw Problem("invalid_group");
        }
        private static void RequireGroup(string group, IList<string> groups) { ValidateGroup(group); if (!groups.Contains(group, StringComparer.Ordinal)) throw Problem("invalid_group"); }
        private static void ValidateEndpoint(string endpoint, string group) {
            Uri uri; if (!Uri.TryCreate(endpoint, UriKind.Absolute, out uri) || uri.Scheme != "https" || (uri.Host != "1.1.1.1" && uri.Host != "8.8.8.8") ||
                uri.AbsolutePath != "/dns-query" || !uri.IsDefaultPort || uri.UserInfo != "" || uri.Query != "" || Uri.UnescapeDataString(uri.Fragment.TrimStart('#')) != group) throw Problem("invalid_group");
        }

        // Reads only group names and selected DNS scalars. It deliberately refuses unsupported
        // YAML shapes instead of guessing. Node definitions, URLs and credentials are not decoded.
        private static YamlInfo ReadYaml(string yaml) {
            var info = new YamlInfo(); string section = null, dnsField = null; int sequenceIndent = -1, policyIndent = -1;
            foreach (string source in yaml.Replace("\r\n", "\n").Split('\n')) {
                if (String.IsNullOrWhiteSpace(source) || source.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
                int indent = source.Length - source.TrimStart(' ').Length; string line = source.Trim();
                if (indent == 0) {
                    section = line.StartsWith("proxy-groups:", StringComparison.Ordinal) ? "groups" : line.StartsWith("dns:", StringComparison.Ordinal) ? "dns" : null;
                    dnsField = null; sequenceIndent = -1; policyIndent = -1;
                    if (section == "groups" && line != "proxy-groups:" && !Regex.IsMatch(line, @"^proxy-groups:\s*\[\s*\]\s*(#.*)?$")) throw Problem("unsupported_yaml");
                    continue;
                }
                if (section == null) continue;
                if (source.IndexOf('\t') >= 0) throw Problem("unsupported_yaml");
                if (section == "groups") {
                    if (line.StartsWith("-", StringComparison.Ordinal) && (sequenceIndent < 0 || indent == sequenceIndent)) {
                        if (sequenceIndent < 0) sequenceIndent = indent;
                        string item = line.Substring(1).Trim();
                        if (item.StartsWith("{", StringComparison.Ordinal)) {
                            var names = FlowMapMembers(item).Where(part => Regex.IsMatch(part, @"^name\s*:")).ToList();
                            if (names.Count != 1) throw Problem("unsupported_yaml");
                            AddGroup(info, Scalar(names[0].Substring(names[0].IndexOf(':') + 1)));
                        } else if (item.StartsWith("name:", StringComparison.Ordinal)) AddGroup(info, Scalar(item.Substring(5)));
                    } else if (sequenceIndent >= 0 && indent == sequenceIndent + 2 && line.StartsWith("name:", StringComparison.Ordinal)) AddGroup(info, Scalar(line.Substring(5)));
                } else {
                    if (indent == 2 && !line.StartsWith("-", StringComparison.Ordinal)) {
                        dnsField = line.StartsWith("nameserver:", StringComparison.Ordinal) ? "nameserver" : line.StartsWith("fallback:", StringComparison.Ordinal) ? "fallback" : line.StartsWith("nameserver-policy:", StringComparison.Ordinal) ? "policy" : null;
                        policyIndent = -1;
                        if (dnsField == "nameserver" || dnsField == "fallback") {
                            string tail = line.Substring(line.IndexOf(':') + 1).Trim();
                            if (tail.Length != 0 && tail != "null" && !tail.StartsWith("#", StringComparison.Ordinal)) {
                                if (!tail.StartsWith("[", StringComparison.Ordinal)) throw Problem("unsupported_yaml");
                                foreach (string scalar in FlowList(tail)) (dnsField == "nameserver" ? info.NameServers : info.Fallback).Add(scalar);
                            }
                        }
                    } else if ((dnsField == "nameserver" || dnsField == "fallback") && line.StartsWith("- ", StringComparison.Ordinal)) {
                        (dnsField == "nameserver" ? info.NameServers : info.Fallback).Add(Scalar(line.Substring(2)));
                    } else if (dnsField == "policy" && !line.StartsWith("-", StringComparison.Ordinal)) {
                        if (policyIndent < 0) policyIndent = indent;
                        if (indent == policyIndent) info.PolicyCount++;
                    }
                }
            }
            return info;
        }
        private static IEnumerable<string> FlowList(string source) {
            int end = source.LastIndexOf(']'); if (end < 0) throw Problem("unsupported_yaml");
            string text = source.Substring(1, end - 1); var parts = new List<string>(); int start = 0; char quote = '\0'; bool escape = false;
            for (int i = 0; i < text.Length; i++) {
                char c = text[i]; if (escape) { escape = false; continue; }
                if (quote != '\0') { if (c == '\\' && quote == '"') escape = true; else if (c == quote) quote = '\0'; }
                else if (c == '"' || c == '\'') quote = c;
                else if (c == ',') { parts.Add(Scalar(text.Substring(start, i - start))); start = i + 1; }
            }
            if (quote != '\0') throw Problem("unsupported_yaml");
            if (!String.IsNullOrWhiteSpace(text.Substring(start))) parts.Add(Scalar(text.Substring(start))); return parts;
        }
        private static List<string> FlowMapMembers(string source) {
            // Only top-level members may supply the group name. A nested node's name
            // must never be mistaken for an existing outbound group.
            var result = new List<string>(); var nesting = new Stack<char>(); char quote = '\0'; bool escape = false; int start = 1;
            if (source.Length < 2 || source[0] != '{') throw Problem("unsupported_yaml");
            for (int i = 1; i < source.Length; i++) {
                char c = source[i];
                if (escape) { escape = false; continue; }
                if (quote != '\0') {
                    if (quote == '"' && c == '\\') escape = true;
                    else if (c == quote) { if (quote == '\'' && i + 1 < source.Length && source[i + 1] == '\'') i++; else quote = '\0'; }
                    continue;
                }
                if (c == '\'' || c == '"') { quote = c; continue; }
                if (c == '{' || c == '[') { nesting.Push(c); continue; }
                if (c == '}' || c == ']') {
                    if (nesting.Count != 0) { char open = nesting.Pop(); if ((open == '{' && c != '}') || (open == '[' && c != ']')) throw Problem("unsupported_yaml"); continue; }
                    if (c != '}') throw Problem("unsupported_yaml");
                    string tail = source.Substring(i + 1).Trim(); if (tail.Length != 0 && !tail.StartsWith("#", StringComparison.Ordinal)) throw Problem("unsupported_yaml");
                    if (!String.IsNullOrWhiteSpace(source.Substring(start, i - start))) result.Add(source.Substring(start, i - start).Trim());
                    return result;
                }
                if (c == ',' && nesting.Count == 0) { result.Add(source.Substring(start, i - start).Trim()); start = i + 1; }
            }
            throw Problem("unsupported_yaml");
        }
        private static string Scalar(string source) {
            string value = source.Trim();
            if (value.StartsWith("\"", StringComparison.Ordinal)) {
                var parser = new SpanJson(value); Node node = parser.ReadSingleString(); return Json().Deserialize<string>(value.Substring(node.Start, node.End - node.Start));
            }
            if (value.StartsWith("'", StringComparison.Ordinal)) {
                var match = Regex.Match(value, "^'((?:''|[^'])*)'\\s*(?:#.*)?$"); if (!match.Success) throw Problem("unsupported_yaml"); return match.Groups[1].Value.Replace("''", "'");
            }
            int comment = value.IndexOf(" #", StringComparison.Ordinal); if (comment >= 0) value = value.Substring(0, comment).TrimEnd();
            if (value.Length == 0 || value.StartsWith("!", StringComparison.Ordinal) || value.StartsWith("*", StringComparison.Ordinal) || value.StartsWith("&", StringComparison.Ordinal)) throw Problem("unsupported_yaml");
            return value;
        }
        private static void AddGroup(YamlInfo info, string name) {
            ValidateGroup(name); if (info.Groups.Contains(name, StringComparer.Ordinal)) throw Problem("unsupported_yaml"); info.Groups.Add(name);
            if (info.Groups.Count > 512) throw Problem("unsupported_yaml");
        }

        private string NewBackup() {
            PlainPath(backupRoot); Directory.CreateDirectory(backupRoot); PlainPath(backupRoot);
            string folder = Path.Combine(backupRoot, "dns-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff'Z'", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
            var sid = WindowsIdentity.GetCurrent().User; var security = new DirectorySecurity(); security.SetAccessRuleProtection(true, false); security.SetOwner(sid);
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.CreateDirectory(folder, security); PlainPath(folder); return folder;
        }
        private string LatestBackup() {
            PlainPath(backupRoot); if (!Directory.Exists(backupRoot)) return null;
            var folders = Directory.GetDirectories(backupRoot, "dns-*"); if (folders.Length > 2000) throw Problem("backup_invalid");
            foreach (string folder in folders.Where(x => Regex.IsMatch(Path.GetFileName(x), @"\Adns-[0-9]{8}T[0-9]{9}Z-[a-f0-9]{32}\z")).OrderByDescending(x => x, StringComparer.Ordinal)) {
                PlainPath(folder);
                if (!File.Exists(Path.Combine(folder, "receipt.json"))) continue; // No preferences are changed before a verified receipt exists.
                Receipt receipt = ReadReceipt(folder);
                if (receipt.State == "aborted") continue;
                // A crash before replacement can leave a prepared record without an abort marker.
                if (receipt.State == "prepared" && Hash(Read(preferencesPath, Limit)) == receipt.BeforeHash) continue;
                return folder;
            }
            return null;
        }
        private static Receipt ReadReceipt(string folder) {
            string file = Path.Combine(folder, "receipt.json"); RequirePrivateFile(file);
            string text = Decode(Read(file, ReceiptLimit)); SpanJson.Parse(text); Receipt receipt = Json().Deserialize<Receipt>(text);
            if (receipt == null || receipt.Version != 1 || receipt.Kind != "whale-network-dns" || !new[] { "prepared", "applied", "undone", "aborted" }.Contains(receipt.State) ||
                !IsHash(receipt.BeforeHash) || !IsHash(receipt.AfterHash) || !IsHash(receipt.YamlHash) || receipt.Fields == null || receipt.Fields.Count > 10) throw Problem("backup_invalid");
            ValidateGroup(receipt.Group); return receipt;
        }
        private static Saved VerifyReceipt(string folder) {
            var receipt = ReadReceipt(folder); string path = Path.Combine(folder, "preferences.before.bin"); RequirePrivateFile(path); byte[] before = Read(path, Limit);
            if (Hash(before) != receipt.BeforeHash) throw Problem("backup_invalid");
            string pref = Decode(before), original = ExtractFlutter(pref); var projected = Project(original, receipt.Group);
            byte[] after = EncodeLike(ReplaceFlutter(pref, projected.Text), before);
            if (Hash(after) != receipt.AfterHash || Json().Serialize(projected.Fields) != Json().Serialize(receipt.Fields)) throw Problem("backup_invalid");
            return new Saved { Receipt = receipt, Before = before };
        }
        private static void SaveReceipt(string folder, Receipt receipt) {
            string path = Path.Combine(folder, "receipt.json"); byte[] bytes = Utf8.GetBytes(Json().Serialize(receipt));
            if (File.Exists(path)) ReplaceChecked(path, bytes, Hash(Read(path, ReceiptLimit)), null); else WritePrivate(path, bytes);
            RequirePrivateFile(path); if (Hash(Read(path, ReceiptLimit)) != Hash(bytes)) throw Problem("backup_invalid");
        }

        private void RequireClosed() { if (runningProbe()) throw Problem("client_running"); }
        private static bool ClientsRunning() {
            foreach (string name in new[] { "CuteCloud", "FlClashCore" }) {
                Process[] processes = Process.GetProcessesByName(name);
                try { if (processes.Length != 0) return true; } finally { foreach (Process process in processes) process.Dispose(); }
            }
            return false;
        }
        internal static string OperationMutexName(string preferencesFile) {
            string identity = WindowsIdentity.GetCurrent().User.Value + "|" + Path.GetFullPath(preferencesFile).ToUpperInvariant();
            return @"Global\DafeiyuNetworkDns-" + Hash(Utf8.GetBytes(identity));
        }
        private sealed class OperationLock : IDisposable {
            private Mutex mutex;
            private bool held;
            internal OperationLock(string preferencesFile) {
                var security = new MutexSecurity(); var sid = WindowsIdentity.GetCurrent().User;
                security.SetAccessRuleProtection(true, false); security.AddAccessRule(new MutexAccessRule(sid, MutexRights.FullControl, AccessControlType.Allow));
                bool created; mutex = new Mutex(false, OperationMutexName(preferencesFile), out created, security);
                try {
                    try { held = mutex.WaitOne(0); } catch (AbandonedMutexException) { held = true; }
                    if (!held) throw Problem("operation_busy");
                } catch { mutex.Dispose(); mutex = null; throw; }
            }
            public void Dispose() { if (mutex != null) { try { if (held) mutex.ReleaseMutex(); } finally { mutex.Dispose(); mutex = null; held = false; } } }
        }
        private static void PlainPath(string path) {
            string full = Path.GetFullPath(path);
            if (full.StartsWith(@"\\", StringComparison.Ordinal) || full.IndexOf(':', 2) >= 0) throw Problem("unsafe_path");
            string cursor = full;
            while (!String.IsNullOrEmpty(cursor)) {
                if (File.Exists(cursor) || Directory.Exists(cursor)) { if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0) throw Problem("unsafe_path"); }
                string parent = Path.GetDirectoryName(cursor); if (parent == cursor) break; cursor = parent;
            }
        }
        private static byte[] Read(string path, int maximum) {
            PlainPath(path); if (!File.Exists(path) || (File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.Device)) != 0) throw Problem("missing_files");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                if (stream.Length > maximum) throw Problem("file_limit"); byte[] data = new byte[(int)stream.Length]; int position = 0;
                while (position < data.Length) { int count = stream.Read(data, position, data.Length - position); if (count == 0) throw new EndOfStreamException(); position += count; }
                return data;
            }
        }
        private static void WritePrivate(string path, byte[] bytes) {
            PlainPath(path); var sid = WindowsIdentity.GetCurrent().User; var security = new FileSecurity(); security.SetAccessRuleProtection(true, false); security.SetOwner(sid);
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.None, security)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        }
        private static void RequirePrivateFile(string path) {
            PlainPath(path); var security = File.GetAccessControl(path); var sid = WindowsIdentity.GetCurrent().User;
            if (!security.AreAccessRulesProtected || !security.GetOwner(typeof(SecurityIdentifier)).Equals(sid)) throw Problem("backup_invalid");
            foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier))) {
                if (rule.AccessControlType == AccessControlType.Allow && !rule.IdentityReference.Equals(sid)) throw Problem("backup_invalid");
            }
        }
        private static void RequireHash(string path, string hash) { if (Hash(Read(path, Limit)) != hash) throw Problem("file_changed"); }
        private static void ReplaceChecked(string path, byte[] bytes, string expected, Action finalCheck, Action replacing = null) {
            PlainPath(path); string temporary = Path.Combine(Path.GetDirectoryName(path), ".whale-dns-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                WritePrivate(temporary, bytes); RequireHash(path, expected); if (finalCheck != null) finalCheck();
                PlainPath(path); PlainPath(temporary); RequireHash(path, expected); if (finalCheck != null) finalCheck();
                if (replacing != null) replacing(); File.Replace(temporary, path, null);
                if (Hash(Read(path, Limit)) != Hash(bytes)) throw Problem("write_failed");
            } finally { if (File.Exists(temporary)) { PlainPath(temporary); File.Delete(temporary); } }
        }
        private static string Decode(byte[] bytes) { int start = bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191 ? 3 : 0; return Utf8.GetString(bytes, start, bytes.Length - start); }
        private static byte[] EncodeLike(string text, byte[] original) {
            byte[] content = Utf8.GetBytes(text); if (original.Length < 3 || original[0] != 239 || original[1] != 187 || original[2] != 191) return content;
            byte[] result = new byte[content.Length + 3]; result[0] = 239; result[1] = 187; result[2] = 191; Buffer.BlockCopy(content, 0, result, 3, content.Length); return result;
        }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static bool IsHash(string value) { return value != null && Regex.IsMatch(value, @"\A[a-f0-9]{64}\z"); }
        private static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = Limit, RecursionLimit = 64 }; }
        private static InvalidDataException Problem(string code) { return new InvalidDataException(code); }
        private static string ErrorCode(Exception error) {
            if (error is InvalidDataException && new[] { "invalid_plan", "invalid_group", "client_running", "file_changed", "undo_conflict", "no_undo", "unsafe_path", "missing_files", "file_limit", "unsupported_yaml", "invalid_json", "backup_invalid", "write_failed", "operation_busy" }.Contains(error.Message)) return error.Message;
            if (error is UnauthorizedAccessException) return "access_denied"; if (error is IOException) return "io_error"; return "invalid_config";
        }
        internal static string ErrorText(string code, bool english) {
            switch (code) {
                case "client_running": return english ? "Close CuteCloud normally, including FlClashCore, then retry. No process was stopped or started." : "请正常退出 CuteCloud，并确认 FlClashCore 也已退出后再试；本助手不会停止或启动进程。";
                case "operation_busy": return english ? "Another helper instance is applying or undoing DNS preferences. Wait for it to finish, then inspect and preview again. No backup or preference was written by this attempt." : "另一个助手窗口正在应用或撤回 DNS，请等它完成后重新检查并预览。本次未创建备份，也未写入偏好。";
                case "file_changed": return english ? "An input file changed after preview. Nothing further will be overwritten; inspect and create a new preview." : "预览后输入文件发生变化，已停止后续覆盖；请重新检查并预览。";
                case "undo_conflict": return english ? "A DNS field owned by this undo was changed later. Undo was refused without overwriting it." : "要撤回的 DNS 字段已有后续修改；已拒绝撤回，不覆盖这些修改。";
                case "invalid_group": return english ? "Select an existing outbound group with a safe name; fragments, separators and control characters are not accepted." : "请选择配置中确实存在且名称安全的出站组；不接受片段注入、分隔符或控制字符。";
                case "invalid_plan": return english ? "A valid preview from this window is required before applying." : "应用前必须使用当前窗口生成有效预览。";
                case "no_undo": return english ? "No pending DNS undo record was found." : "没有待撤回的 DNS 操作记录。";
                case "unsafe_path": return english ? "Linked, device, alternate-stream or network paths are not supported; the operation stopped." : "不支持链接、设备、备用数据流或网络路径，操作已停止。";
                case "missing_files": return english ? "Supported CuteCloud preference and generated configuration files were not found." : "未找到受支持的 CuteCloud 偏好文件和生成配置文件。";
                case "backup_invalid": return english ? "Backup or limited-field receipt verification failed; the operation stopped." : "备份或有限字段撤回记录未通过核验，操作已停止。";
                case "unsupported_yaml": return english ? "The YAML shape is not safely supported. No group or DNS value was guessed; use CuteCloud's settings UI." : "此 YAML 结构暂不支持安全识别，未猜测出站组或 DNS 值；请使用 CuteCloud 设置界面。";
                case "access_denied": return english ? "File access was denied; existing backups were retained." : "文件访问被拒绝；已有备份保留。";
                case "io_error": return english ? "Local file I/O failed or a file was locked; existing backups were retained." : "本地文件读写失败或被占用；已有备份保留。";
                case "write_failed": return english ? "Read-back verification failed; retain the backup and inspect before retrying." : "写入后读回核验失败，请保留备份并核对后再试。";
                case "file_limit": return english ? "The file exceeds the bounded read limit." : "文件超过安全读取大小限制。";
                default: return english ? "The DNS configuration could not be safely parsed. No configuration contents or exception details were exposed." : "无法安全解析 DNS 配置；未输出配置原文或异常详情。";
            }
        }

        private static string[] DnsPath(string key) { return new[] { "patchClashConfig", "dns", key }; }
        private static Node Field(string text, string[] path) {
            Node node = SpanJson.Parse(text); foreach (string key in path) { RequireObject(node); var member = node.Members.FirstOrDefault(x => x.Name == key); if (member == null) return null; node = member.Value; } return node;
        }
        private static void RequireObject(Node node) { if (node == null || node.Kind != '{') throw Problem("invalid_json"); }
        private static string Literal(string text, Node node) { return text.Substring(node.Start, node.End - node.Start); }
        private static bool BooleanField(string text, string[] path) { Node node = Field(text, path); if (node == null) return false; string value = Literal(text, node); if (value != "true" && value != "false") throw Problem("invalid_json"); return value == "true"; }
        private static List<string> StringList(Node node, string text) {
            if (node == null || Literal(text, node) == "null") return new List<string>(); if (node.Kind != '[') throw Problem("invalid_json");
            object[] items = Json().DeserializeObject(Literal(text, node)) as object[]; if (items == null || items.Length > 1024 || items.Any(x => !(x is string))) throw Problem("invalid_json"); return items.Cast<string>().ToList();
        }
        private static string FieldHash(string text, Node node) {
            // Canonicalize only the owned DNS field, never the account/subscription-containing document.
            return Hash(Utf8.GetBytes(node == null ? "missing" : "present:" + Json().Serialize(Json().DeserializeObject(Literal(text, node)))));
        }
        private static string ExtractFlutter(string preferences) {
            Node node = Field(preferences, new[] { "flutter.config" }); if (node == null || node.Kind != '"') throw Problem("invalid_json");
            string text = Json().Deserialize<string>(Literal(preferences, node)); RequireObject(SpanJson.Parse(text)); return text;
        }
        private static string ReplaceFlutter(string preferences, string config) { return Put(preferences, new[] { "flutter.config" }, Json().Serialize(config)); }
        private static string Put(string text, string[] path, string value) {
            if (path.Length == 0) throw Problem("invalid_json"); Node parent = SpanJson.Parse(text);
            foreach (string key in path.Take(path.Length - 1)) { RequireObject(parent); var next = parent.Members.FirstOrDefault(m => m.Name == key); if (next == null) throw Problem("invalid_json"); parent = next.Value; }
            RequireObject(parent); int index = parent.Members.FindIndex(m => m.Name == path[path.Length - 1]);
            if (index < 0) {
                if (value == null) return text; int position = parent.Members.Count == 0 ? parent.Start + 1 : parent.Members[parent.Members.Count - 1].End;
                text = text.Insert(position, (parent.Members.Count == 0 ? "" : ",") + Json().Serialize(path[path.Length - 1]) + ":" + value);
            } else if (value != null) {
                Node old = parent.Members[index].Value; text = text.Substring(0, old.Start) + value + text.Substring(old.End);
            } else {
                int start, end;
                if (index + 1 < parent.Members.Count) { start = parent.Members[index].Start; end = parent.Members[index + 1].Start; }
                else if (index > 0) { start = parent.Members[index - 1].End; end = parent.Members[index].End; }
                else { start = parent.Members[index].Start; end = parent.Members[index].End; }
                text = text.Remove(start, end - start);
            }
            SpanJson.Parse(text); return text;
        }

        private sealed class Input { internal byte[] Bytes, YamlBytes; internal string Preferences, Config; internal YamlInfo Yaml; }
        private sealed class YamlInfo { internal List<string> Groups = new List<string>(), NameServers = new List<string>(), Fallback = new List<string>(); internal int PolicyCount; }
        private sealed class Edit { internal string Text; internal int Removed, Retained; internal List<FieldRecord> Fields = new List<FieldRecord>(); }
        private sealed class Saved { internal Receipt Receipt; internal byte[] Before; }
        private sealed class Receipt { public int Version; public string Kind, State, BeforeHash, AfterHash, YamlHash, Group; public List<FieldRecord> Fields; }
        private sealed class FieldRecord { public List<string> Path; public string BeforeHash, AfterHash; }
        private sealed class Node { internal int Start, End; internal char Kind; internal List<Member> Members = new List<Member>(); }
        private sealed class Member { internal int Start, End; internal string Name; internal Node Value; }

        // Span-only strict JSON parser. Unknown strings remain opaque source spans; duplicate
        // keys and excessive nesting are rejected before lossless, allowlisted edits.
        private sealed class SpanJson {
            private static readonly Regex Primitive = new Regex(@"\G(?:true|false|null|-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)", RegexOptions.CultureInvariant);
            private readonly string text; private int position, nodes;
            internal SpanJson(string text) { this.text = text; }
            internal static Node Parse(string text) { var parser = new SpanJson(text); Node root = parser.Value(0); parser.Space(); if (parser.position != text.Length) throw Problem("invalid_json"); return root; }
            internal Node ReadSingleString() { Space(); if (position >= text.Length || text[position] != '"') throw Problem("invalid_json"); Node node = String(); Space(); if (position < text.Length && text[position] != '#') throw Problem("invalid_json"); return node; }
            private void Space() { while (position < text.Length && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n')) position++; }
            private Node Value(int depth) {
                if (depth > 64 || ++nodes > 100000) throw Problem("file_limit"); Space(); if (position >= text.Length) throw Problem("invalid_json");
                if (text[position] == '"') return String(); Node node = new Node { Start = position, Kind = text[position] };
                if (text[position] == '{') {
                    position++; Space(); var names = new HashSet<string>(StringComparer.Ordinal);
                    if (Take('}')) { node.End = position; return node; }
                    while (true) {
                        Space(); int start = position; Node key = String(); string name = Json().Deserialize<string>(Literal(text, key)); if (!names.Add(name)) throw Problem("invalid_json");
                        Space(); if (!Take(':')) throw Problem("invalid_json"); Node child = Value(depth + 1); node.Members.Add(new Member { Start = start, End = child.End, Name = name, Value = child });
                        Space(); if (Take('}')) break; if (!Take(',')) throw Problem("invalid_json");
                    }
                } else if (text[position] == '[') {
                    position++; Space(); if (!Take(']')) { while (true) { Value(depth + 1); Space(); if (Take(']')) break; if (!Take(',')) throw Problem("invalid_json"); } }
                } else {
                    var match = Primitive.Match(text, position);
                    if (!match.Success || match.Index != position) throw Problem("invalid_json"); position += match.Length;
                }
                node.End = position; return node;
            }
            private Node String() {
                if (position >= text.Length || text[position] != '"') throw Problem("invalid_json"); Node node = new Node { Start = position, Kind = '"' }; position++;
                while (position < text.Length) {
                    char c = text[position++]; if (c == '"') { node.End = position; return node; } if (c < 32) throw Problem("invalid_json");
                    if (c == '\\') {
                        if (position >= text.Length) throw Problem("invalid_json"); char escape = text[position++];
                        if (escape == 'u') { if (position + 4 > text.Length || !Regex.IsMatch(text.Substring(position, 4), @"\A[0-9a-fA-F]{4}\z")) throw Problem("invalid_json"); position += 4; }
                        else if ("\"\\/bfnrt".IndexOf(escape) < 0) throw Problem("invalid_json");
                    }
                }
                throw Problem("invalid_json");
            }
            private bool Take(char c) { if (position < text.Length && text[position] == c) { position++; return true; } return false; }
        }
    }
}
