using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace WhaleClaudeHelper {
    internal sealed class BrowserLocaleResult {
        public bool Ok;
        public string BackupDirectory;
        public List<string> Details = new List<string>();
    }

    // Offline, opt-in preset for the default stable Chrome/Edge data roots only.
    // No browser launch, process termination, network, registry, or Secure Preferences edits.
    internal sealed class BrowserLocaleService {
        private const int FileLimit = 16 * 1024 * 1024;
        private const int ReceiptLimit = 512 * 1024;
        private const int MaximumFiles = 256;
        private static readonly object Gate = new object();
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly string backupRoot;
        private readonly Dictionary<string, string> roots;

        public BrowserLocaleService(string backupRoot = null) : this(backupRoot, null) { }

        // CI supplies fresh temporary profiles; the public constructor keeps the
        // exact default stable-browser roots and process checks used by the UI.
        internal BrowserLocaleService(string backupRoot, IDictionary<string, string> browserRoots) {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            this.backupRoot = Path.GetFullPath(backupRoot ?? Path.Combine(local, "DafeiyuClaudeHelper", "Backups", "BrowserLocale"));
            roots = new Dictionary<string, string>(StringComparer.Ordinal) {
                { "Chrome", Path.Combine(local, "Google", "Chrome", "User Data") },
                { "Edge", Path.Combine(local, "Microsoft", "Edge", "User Data") }
            };
            if (browserRoots != null) {
                if (browserRoots.Count != 2 || !browserRoots.ContainsKey("Chrome") || !browserRoots.ContainsKey("Edge"))
                    throw new ArgumentException("Only explicit Chrome and Edge roots are supported.", "browserRoots");
                foreach (string browser in new[] { "Chrome", "Edge" }) {
                    if (String.IsNullOrWhiteSpace(browserRoots[browser]) || !Path.IsPathRooted(browserRoots[browser]))
                        throw new ArgumentException("Browser roots must be absolute.", "browserRoots");
                    roots[browser] = Path.GetFullPath(browserRoots[browser]);
                    RepairEngine.AssertPlainPath(roots[browser]);
                }
            }
        }

        public BrowserLocaleResult ApplyAll() { return ApplySelected(null); }

        // Measurement-driven optimization may only change the browser that was measured.
        public BrowserLocaleResult ApplyForBrowser(string browserId) {
            if (browserId != "Chrome" && browserId != "Edge") return new BrowserLocaleResult {
                Ok = false,
                Details = new List<string> { "实测浏览器不支持此语言修复，未修改任何浏览器。 / The measured browser is not supported for this language change; no browser was modified." }
            };
            return ApplySelected(browserId);
        }

        private BrowserLocaleResult ApplySelected(string browserId) {
            lock (Gate) {
                var result = new BrowserLocaleResult();
                try {
                    RequireBrowsersClosed();
                    var pending = Prepare(result.Details, browserId);
                    if (pending.Count == 0) {
                        result.Ok = true;
                        result.Details.Add("未发现需要修改的受支持浏览器文件；未验证网页评分。 / No supported browser file needed a change; the webpage score was not verified.");
                        return result;
                    }
                    string folder = NewBackupDirectory();
                    result.BackupDirectory = folder;
                    var receipt = new LocaleReceipt { Format = 1, Kind = "whale-browser-locale", State = "prepared" };
                    for (int index = 0; index < pending.Count; index++) {
                        var item = pending[index];
                        item.Record.Blob = "before-" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + ".bin";
                        string blob = Path.Combine(folder, item.Record.Blob);
                        WritePrivate(blob, item.Before);
                        if (Hash(Read(blob, FileLimit)) != item.Record.BeforeHash) throw new InvalidDataException("backup_verification_failed");
                        receipt.Files.Add(item.Record);
                    }
                    SaveReceipt(folder, receipt);
                    // Validate all originals again before the first browser-file mutation.
                    RequireBrowsersClosed();
                    foreach (var item in pending) RequireHash(TargetPath(item.Record), item.Record.BeforeHash);
                    foreach (var item in pending) {
                        RequireBrowsersClosed();
                        ReplaceChecked(TargetPath(item.Record), item.After, item.Record.BeforeHash);
                        result.Details.Add(Label(item.Record) + "：语言设置已写入并核验。 / Language preferences written and verified.");
                    }
                    receipt.State = "applied";
                    SaveReceipt(folder, receipt);
                    result.Ok = true;
                    result.Details.Add("重新打开 Chrome/Edge 后生效；策略、同步或启动参数可能覆盖，尚未检查实际网页结果。 / Reopen Chrome/Edge; policy, sync or launch arguments may override these preferences. Actual webpage behavior has not been checked.");
                } catch (Exception error) { result.Details.Add(ErrorText(error)); }
                return result;
            }
        }

        public BrowserLocaleResult Restore(string backupDirectory) {
            lock (Gate) {
                var result = new BrowserLocaleResult();
                try {
                    string folder = ValidateBackupDirectory(backupDirectory);
                    result.BackupDirectory = folder;
                    var receipt = ReadReceipt(folder);
                    RequireBrowsersClosed();
                    var pending = new List<PreparedFile>();
                    bool conflict = false;
                    // Validate all backups and plan field-level inverses before any restore.
                    foreach (var entry in receipt.Files) {
                        string target = TargetPath(entry);
                        byte[] original = Read(Path.Combine(folder, entry.Blob), FileLimit);
                        if (Hash(original) != entry.BeforeHash) throw new InvalidDataException("backup_verification_failed");
                        // Also prove the recorded after image is exactly this preset, not arbitrary content.
                        byte[] projected = EncodeLike(Edit(Decode(original), entry.FileKind == "state"), original);
                        if (Hash(projected) != entry.AfterHash) throw new InvalidDataException("invalid_receipt");
                        byte[] current = Read(target, FileLimit);
                        string currentHash = Hash(current);
                        if (currentHash == entry.BeforeHash) {
                            result.Details.Add(Label(entry) + "：已经是原文件。 / Already matches the original file.");
                        } else if (currentHash == entry.AfterHash) {
                            pending.Add(new PreparedFile { Record = entry, After = original, ExpectedHash = currentHash });
                        } else {
                            List<string> conflicts;
                            string restored = RestoreText(Decode(original), Decode(current), entry.FileKind == "state", out conflicts);
                            byte[] restoredBytes = EncodeLike(restored, current);
                            foreach (string key in conflicts) {
                                conflict = true;
                                result.Details.Add(Label(entry) + " / intl." + key + "：已被另外修改，保留当前值。 / Changed independently; the current value was retained.");
                            }
                            if (Hash(restoredBytes) != currentHash) pending.Add(new PreparedFile { Record = entry, After = restoredBytes, ExpectedHash = currentHash });
                            else if (conflicts.Count == 0) result.Details.Add(Label(entry) + "：原语言偏好已存在，其他更改保留。 / Original language preferences are already present; other changes were retained.");
                        }
                    }
                    RequireBrowsersClosed();
                    foreach (var item in pending) RequireHash(TargetPath(item.Record), item.ExpectedHash);
                    foreach (var item in pending) {
                        RequireBrowsersClosed();
                        ReplaceChecked(TargetPath(item.Record), item.After, item.ExpectedHash);
                        result.Details.Add(Label(item.Record) + "：可恢复的语言偏好已核验，其他设置保留。 / Restorable language preferences were verified; other settings were retained.");
                    }
                    receipt.State = conflict ? "restore-partial" : "restored";
                    SaveReceipt(folder, receipt);
                    result.Ok = !conflict;
                } catch (Exception error) { result.Details.Add(ErrorText(error)); }
                return result;
            }
        }

        private List<PreparedFile> Prepare(List<string> details, string selectedBrowser) {
            var result = new List<PreparedFile>();
            foreach (var browser in roots) {
                if (selectedBrowser != null && browser.Key != selectedBrowser) continue;
                RepairEngine.AssertPlainPath(browser.Value);
                if (!Directory.Exists(browser.Value)) {
                    details.Add(browser.Key + "：未找到默认数据目录，跳过。 / Default data directory not found; skipped.");
                    continue;
                }
                string state = Path.Combine(browser.Value, "Local State");
                RepairEngine.AssertPlainPath(state);
                if (!File.Exists(state)) {
                    details.Add(browser.Key + "：没有 Local State，跳过该浏览器。 / Local State is missing; browser skipped.");
                    continue;
                }
                AddPrepared(result, new FileReceipt { Browser = browser.Key, FileKind = "state", Profile = "" });
                foreach (string directory in Directory.GetDirectories(browser.Value).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) {
                    string profile = Path.GetFileName(directory);
                    if (!IsProfile(profile)) continue;
                    RepairEngine.AssertPlainPath(directory);
                    string preferences = Path.Combine(directory, "Preferences");
                    RepairEngine.AssertPlainPath(preferences);
                    if (!File.Exists(preferences)) continue;
                    AddPrepared(result, new FileReceipt { Browser = browser.Key, FileKind = "profile", Profile = profile });
                }
            }
            return result;
        }

        private void AddPrepared(List<PreparedFile> output, FileReceipt entry) {
            string path = TargetPath(entry);
            byte[] original = Read(path, FileLimit);
            byte[] changed = EncodeLike(Edit(Decode(original), entry.FileKind == "state"), original);
            entry.BeforeHash = Hash(original);
            entry.AfterHash = Hash(changed);
            if (entry.BeforeHash == entry.AfterHash) return;
            if (output.Count >= MaximumFiles) throw new InvalidDataException("too_many_profiles");
            output.Add(new PreparedFile { Record = entry, Before = original, After = changed });
        }

        // Uses strict span parsing; other values (including credentials/account blobs) are never reserialized.
        internal static string Edit(string text, bool localState) {
            if (localState) return SetIntlString(text, "app_locale", "en-US");
            text = SetIntlString(text, "selected_languages", "en-US,en");
            return SetIntlString(text, "accept_languages", "en-US,en");
        }

        // Undo only fields this preset actually changed. Current unrelated JSON spans stay intact.
        // Original literals are restored exactly; new keys are removed without deleting newer siblings.
        internal static string RestoreText(string original, string current, bool localState, out List<string> conflicts) {
            conflicts = new List<string>();
            string expected = Edit(original, localState);
            LosslessJson.Parse(current);
            foreach (string key in localState ? new[] { "app_locale" } : new[] { "selected_languages", "accept_languages" }) {
                var before = Field(original, key);
                var after = Field(expected, key);
                if (SameField(before, after)) continue;
                var now = Field(current, key);
                if (SameField(now, before)) continue;
                if (!SameField(now, after)) { conflicts.Add(key); continue; }
                current = before.Exists ? ReplaceIntlLiteral(current, key, before.Literal) : RemoveIntlMember(current, key);
            }
            LosslessJson.Parse(current);
            return current;
        }

        private static FieldValue Field(string text, string key) {
            var root = LosslessJson.Parse(text).Root;
            var intl = root.Members.FirstOrDefault(x => x.Name == "intl");
            if (intl == null) return new FieldValue();
            if (intl.Value.Kind != 'o') return new FieldValue { Blocked = true };
            var member = intl.Value.Members.FirstOrDefault(x => x.Name == key);
            if (member == null) return new FieldValue();
            string literal = text.Substring(member.Value.Start, member.Value.End - member.Value.Start);
            return new FieldValue {
                Exists = true, Kind = member.Value.Kind, Literal = literal,
                Value = member.Value.Kind == '"' ? Json().Deserialize<string>(literal) : null
            };
        }
        private static bool SameField(FieldValue first, FieldValue second) {
            if (first.Blocked || second.Blocked || first.Exists != second.Exists) return false;
            if (!first.Exists) return true;
            return first.Kind == second.Kind && (first.Kind == '"'
                ? String.Equals(first.Value, second.Value, StringComparison.Ordinal)
                : String.Equals(first.Literal, second.Literal, StringComparison.Ordinal));
        }
        private static string ReplaceIntlLiteral(string text, string key, string literal) {
            var root = LosslessJson.Parse(text).Root;
            var intl = root.Members.First(x => x.Name == "intl").Value;
            var value = intl.Members.First(x => x.Name == key).Value;
            string changed = text.Substring(0, value.Start) + literal + text.Substring(value.End);
            LosslessJson.Parse(changed);
            return changed;
        }
        private static string RemoveIntlMember(string text, string key) {
            var root = LosslessJson.Parse(text).Root;
            var intl = root.Members.First(x => x.Name == "intl").Value;
            int index = intl.Members.FindIndex(x => x.Name == key);
            if (index < 0) return text;
            int start, end;
            if (index + 1 < intl.Members.Count) {
                start = intl.Members[index].Start;
                end = intl.Members[index + 1].Start;
            } else if (index > 0) {
                start = intl.Members[index - 1].End;
                end = intl.Members[index].End;
            } else {
                start = intl.Members[index].Start;
                end = intl.Members[index].End;
            }
            // Keeping an empty intl object is intentional when other current data changed:
            // undo owns the inserted key, not a potentially newer parent-object decision.
            string changed = text.Remove(start, end - start);
            LosslessJson.Parse(changed);
            return changed;
        }

        private static string SetIntlString(string text, string key, string value) {
            var document = LosslessJson.Parse(text);
            var intl = document.Root.Members.FirstOrDefault(x => x.Name == "intl");
            string literal = Json().Serialize(value);
            string edited;
            if (intl == null) {
                edited = InsertMember(text, document.Root, "intl", "{" + Json().Serialize(key) + ":" + literal + "}");
            } else {
                if (intl.Value.Kind != 'o') throw new InvalidDataException("invalid_intl_preferences");
                var member = intl.Value.Members.FirstOrDefault(x => x.Name == key);
                if (member == null) edited = InsertMember(text, intl.Value, key, literal);
                else {
                    if (member.Value.Kind != '"') throw new InvalidDataException("invalid_language_preference");
                    string existing = Json().Deserialize<string>(text.Substring(member.Value.Start, member.Value.End - member.Value.Start));
                    if (String.Equals(existing, value, StringComparison.Ordinal)) return text;
                    edited = text.Substring(0, member.Value.Start) + literal + text.Substring(member.Value.End);
                }
            }
            LosslessJson.Parse(edited);
            return edited;
        }

        private static string InsertMember(string text, LosslessJson.Node node, string key, string literal) {
            int position = node.Members.Count == 0 ? node.Start + 1 : node.Members[node.Members.Count - 1].End;
            string insertion = (node.Members.Count == 0 ? "" : ",") + Json().Serialize(key) + ":" + literal;
            return text.Insert(position, insertion);
        }

        private string TargetPath(FileReceipt entry) {
            string root;
            if (entry == null || entry.Browser == null || !roots.TryGetValue(entry.Browser, out root)) throw new InvalidDataException("invalid_receipt");
            string path;
            if (entry.FileKind == "state" && entry.Profile == "") path = Path.Combine(root, "Local State");
            else if (entry.FileKind == "profile" && IsProfile(entry.Profile)) path = Path.Combine(root, entry.Profile, "Preferences");
            else throw new InvalidDataException("invalid_receipt");
            path = Path.GetFullPath(path);
            if (!path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("invalid_receipt");
            RepairEngine.AssertPlainPath(path);
            return path;
        }

        private static bool IsProfile(string name) {
            return name == "Default" || (name != null && Regex.IsMatch(name, @"\AProfile [0-9]{1,6}\z"));
        }
        private static string Label(FileReceipt entry) {
            return entry.Browser + (entry.FileKind == "state" ? " / Local State" : " / " + entry.Profile);
        }
        private static void RequireBrowsersClosed() {
            foreach (string name in new[] { "chrome", "msedge" }) {
                Process[] processes = Process.GetProcessesByName(name);
                try { if (processes.Length != 0) throw new InvalidDataException("browser_running"); }
                finally { foreach (Process process in processes) process.Dispose(); }
            }
        }

        private string NewBackupDirectory() {
            RepairEngine.AssertPlainPath(backupRoot);
            Directory.CreateDirectory(backupRoot);
            RepairEngine.AssertPlainPath(backupRoot);
            string folder = Path.Combine(backupRoot, "locale-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            var sid = WindowsIdentity.GetCurrent().User;
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            security.SetOwner(sid);
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.CreateDirectory(folder, security);
            RepairEngine.AssertPlainPath(folder);
            return folder;
        }
        private string ValidateBackupDirectory(string value) {
            if (String.IsNullOrEmpty(value)) throw new InvalidDataException("invalid_receipt");
            string folder = Path.GetFullPath(value);
            if (!String.Equals(Path.GetDirectoryName(folder), backupRoot, StringComparison.OrdinalIgnoreCase) || !Regex.IsMatch(Path.GetFileName(folder), @"\Alocale-[0-9]{8}-[0-9]{6}-[a-f0-9]{12}\z")) throw new InvalidDataException("invalid_receipt");
            RepairEngine.AssertPlainPath(folder);
            return folder;
        }
        private LocaleReceipt ReadReceipt(string folder) {
            string text = Decode(Read(Path.Combine(folder, "receipt.json"), ReceiptLimit));
            LosslessJson.Parse(text);
            var receipt = Json().Deserialize<LocaleReceipt>(text);
            if (receipt == null || receipt.Format != 1 || receipt.Kind != "whale-browser-locale" || (receipt.State != "prepared" && receipt.State != "applied" && receipt.State != "restored" && receipt.State != "restore-partial") || receipt.Files == null || receipt.Files.Count == 0 || receipt.Files.Count > MaximumFiles) throw new InvalidDataException("invalid_receipt");
            var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenBlobs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in receipt.Files) {
                if (item == null || !IsHash(item.BeforeHash) || !IsHash(item.AfterHash) || item.Blob == null || !Regex.IsMatch(item.Blob, @"\Abefore-[0-9]{3}\.bin\z") || !seenBlobs.Add(item.Blob) || !seenTargets.Add(TargetPath(item))) throw new InvalidDataException("invalid_receipt");
            }
            return receipt;
        }
        private static void SaveReceipt(string folder, LocaleReceipt receipt) {
            string path = Path.Combine(folder, "receipt.json");
            byte[] content = Utf8.GetBytes(Json().Serialize(receipt));
            if (File.Exists(path)) ReplaceChecked(path, content, Hash(Read(path, ReceiptLimit)));
            else WritePrivate(path, content);
        }
        private static byte[] Read(string path, int maximum) {
            RepairEngine.AssertPlainPath(path);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                if (stream.Length > maximum) throw new InvalidDataException("file_too_large");
                byte[] result = new byte[(int)stream.Length];
                int position = 0;
                while (position < result.Length) {
                    int count = stream.Read(result, position, result.Length - position);
                    if (count == 0) throw new EndOfStreamException();
                    position += count;
                }
                return result;
            }
        }
        private static void RequireHash(string path, string expected) {
            if (!File.Exists(path) || Hash(Read(path, FileLimit)) != expected) throw new InvalidDataException("file_changed");
        }
        private static void WritePrivate(string path, byte[] content) {
            RepairEngine.AssertPlainPath(path);
            var sid = WindowsIdentity.GetCurrent().User;
            var security = new FileSecurity();
            security.SetAccessRuleProtection(true, false);
            security.SetOwner(sid);
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.None, security)) {
                stream.Write(content, 0, content.Length);
                stream.Flush(true);
            }
        }
        private static void ReplaceChecked(string path, byte[] content, string expectedHash) {
            RepairEngine.AssertPlainPath(path);
            string temporary = Path.Combine(Path.GetDirectoryName(path), ".whale-locale-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                WritePrivate(temporary, content);
                RequireHash(path, expectedHash);
                // File.Replace retains the destination file's metadata/ACL; no original is deleted first.
                File.Replace(temporary, path, null);
                if (Hash(Read(path, FileLimit)) != Hash(content)) throw new InvalidDataException("write_verification_failed");
            } finally {
                // Only a uniquely named temporary file made by this method is ever deleted.
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        private static string Decode(byte[] bytes) {
            int start = bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191 ? 3 : 0;
            return Utf8.GetString(bytes, start, bytes.Length - start);
        }
        private static byte[] EncodeLike(string text, byte[] original) {
            byte[] content = Utf8.GetBytes(text);
            if (original.Length < 3 || original[0] != 239 || original[1] != 187 || original[2] != 191) return content;
            byte[] result = new byte[content.Length + 3];
            result[0] = 239; result[1] = 187; result[2] = 191;
            Buffer.BlockCopy(content, 0, result, 3, content.Length);
            return result;
        }
        private static string Hash(byte[] content) {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(content)).Replace("-", "").ToLowerInvariant();
        }
        private static bool IsHash(string value) { return value != null && Regex.IsMatch(value, @"\A[a-f0-9]{64}\z"); }
        private static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = FileLimit, RecursionLimit = 64 }; }
        private static string ErrorText(Exception error) {
            string code = error is InvalidDataException ? error.Message : "";
            if (code == "browser_running") return "请正常关闭 Chrome 和 Edge（包括后台进程）后再试；程序不会替你关闭。 / Close Chrome and Edge, including background processes, then retry. This app does not close them.";
            if (code == "restore_conflict" || code == "file_changed") return "浏览器文件已发生其他变更，已停止以免覆盖；备份保留。 / Browser files changed independently; stopped to avoid overwriting them. Backups are retained.";
            if (code == "linked_path_not_supported" || code == "network_path_not_supported") return "遇到链接或网络路径，已停止。 / A linked or network path was rejected.";
            if (code == "file_too_large" || code == "too_many_profiles" || code == "json_limit_exceeded") return "浏览器配置超过安全读取限制，已停止。 / Browser configuration exceeds the bounded read limits; stopped.";
            if (code == "invalid_receipt" || code == "backup_verification_failed") return "备份或记录未通过核验，已停止。 / Backup or receipt validation failed; stopped.";
            if (code == "write_verification_failed") return "写入后核验未通过；可能已有部分修改，请保留备份并查看撤销结果。 / Read-back verification failed. Some files may have changed; retain the backup and review the undo result.";
            if (error is UnauthorizedAccessException) return "没有文件访问权限；保留现有备份。 / File access was denied; existing backups are retained.";
            if (error is IOException) return "浏览器文件无法读写，可能被占用；可能已有部分修改，备份保留。 / Browser file I/O failed or files were locked. Some changes may have applied; backups are retained.";
            return "浏览器语言操作已停止；未输出配置内容。若已有备份，可尝试撤销。 / Browser language operation stopped without exposing configuration contents. If a backup was created, undo is available.";
        }
        private sealed class PreparedFile { internal FileReceipt Record; internal byte[] Before, After; internal string ExpectedHash; }
        private sealed class FieldValue { internal bool Exists, Blocked; internal char Kind; internal string Literal, Value; }
        private sealed class LocaleReceipt {
            public LocaleReceipt() { }
            public int Format;
            public string Kind, State;
            public List<FileReceipt> Files = new List<FileReceipt>();
        }
        private sealed class FileReceipt {
            public FileReceipt() { }
            public string Browser, FileKind, Profile, Blob, BeforeHash, AfterHash;
        }
    }
}
