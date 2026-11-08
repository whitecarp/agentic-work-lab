using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace WhaleClaudeHelper {
    internal sealed class CodexProxyChange {
        public bool Success, Changed;
        public string BackupDirectory, Error, RecoveryPath;
    }

    // Changes only .env in the selected Codex directory. Never changes the process,
    // user or machine environment, and never reads another Codex configuration.
    internal sealed class CodexProxySettings {
        private const int MaximumEnvBytes = 2097152;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly string[] RequiredKeys = {
            "HTTP_PROXY", "http_proxy", "HTTPS_PROXY", "https_proxy",
            "ALL_PROXY", "all_proxy", "NO_PROXY", "no_proxy"
        };
        private readonly string configDirectory, envPath, backupRoot;
        private readonly Action<string> recycle;
        private readonly Action beforeCommit;
        private readonly Action<string> mutationWindow;

        public CodexProxySettings(string codexDirectory, string backupDirectory)
            : this(codexDirectory, backupDirectory, RecycleWindows, null) { }

        // Tests supply a local move in place of the Windows Recycle Bin. The hook
        // simulates edits between preparation and commit; neither seam is used by UI.
        internal CodexProxySettings(string codexDirectory, string backupDirectory,
            Action<string> recycler, Action commitHook) : this(codexDirectory, backupDirectory, recycler, commitHook, null) { }

        internal CodexProxySettings(string codexDirectory, string backupDirectory,
            Action<string> recycler, Action commitHook, Action<string> mutationHook) {
            AssertPlainPath(codexDirectory); AssertPlainPath(backupDirectory);
            if (recycler == null) throw new ArgumentNullException("recycler");
            configDirectory = Path.GetFullPath(codexDirectory).TrimEnd('\\', '/');
            backupRoot = Path.GetFullPath(backupDirectory).TrimEnd('\\', '/');
            if (configDirectory.Length <= 2 || backupRoot.Length <= 2 ||
                Inside(configDirectory, backupRoot) || Inside(backupRoot, configDirectory))
                throw new InvalidDataException("codex_backup_overlaps_config");
            envPath = Path.Combine(configDirectory, ".env");
            recycle = recycler; beforeCommit = commitHook; mutationWindow = mutationHook;
        }

        public CodexProxyChange Apply(string httpProxyUri, string allProxyUri) {
            var result = new CodexProxyChange(); Receipt receipt = null;
            try {
                ValidateProxy(httpProxyUri, false); ValidateProxy(allProxyUri, true);
                AssertPlainPath(configDirectory); AssertPlainPath(envPath);
                byte[] before = ReadOptional(envPath);
                byte[] after = Edit(before ?? new byte[0], httpProxyUri, allProxyUri);
                if (before != null && Hash(before) == Hash(after)) { result.Success = true; return result; }
                string transaction = NewPrivateDirectory(); result.BackupDirectory = transaction;
                receipt = new Receipt {
                    Schema = 1, State = "prepared", TargetPath = envPath,
                    OriginalExists = before != null, BeforeSha256 = before == null ? "absent" : Hash(before),
                    AfterSha256 = Hash(after), CreatedUtc = DateTime.UtcNow.ToString("o")
                };
                if (before != null) {
                    string original = Path.Combine(transaction, "env.before.bin");
                    WritePrivate(original, before);
                    if (Hash(ReadBounded(original, MaximumEnvBytes)) != receipt.BeforeSha256)
                        throw new InvalidDataException("codex_backup_verification_failed");
                }
                SaveReceipt(transaction, receipt);
                if (beforeCommit != null) beforeCommit();
                AtomicWrite(envPath, after, receipt.BeforeSha256, delegate { result.Changed = true; }, transaction,
                    PreservationCallback(receipt, result), mutationWindow);
                receipt.State = "applied"; SaveReceipt(transaction, receipt);
                result.Success = true;
            } catch (Exception error) {
                result.Error = SafeError(error);
                // Keep the verified original and prepared receipt if anything fails.
                // Automatic rollback could overwrite a subsequent external edit.
                if (receipt != null && result.BackupDirectory != null) {
                    receipt.State = result.Changed ? "apply_failed_after_write" : "apply_failed";
                    receipt.ReviewRequired = result.Changed;
                    try { SaveReceipt(result.BackupDirectory, receipt); } catch { }
                }
            }
            return result;
        }

        public List<string> ListBackups() {
            var result = new List<string>();
            try {
                AssertPlainPath(backupRoot);
                if (!Directory.Exists(backupRoot)) return result;
                foreach (string directory in Directory.GetDirectories(backupRoot, "codex-proxy-*")) {
                    try { Receipt receipt = ReadReceipt(directory); if (SamePath(receipt.TargetPath, envPath)) result.Add(directory); }
                    catch { }
                }
            } catch { }
            result.Sort(StringComparer.Ordinal); result.Reverse(); return result;
        }

        public CodexProxyChange Restore(string backupDirectory) {
            var result = new CodexProxyChange { BackupDirectory = backupDirectory };
            Receipt receipt = null;
            try {
                receipt = ReadReceipt(backupDirectory);
                if (!SamePath(receipt.TargetPath, envPath)) throw new InvalidDataException("codex_backup_target_mismatch");
                if (receipt.ReviewRequired) {
                    if (receipt.RecoveryFiles != null && receipt.RecoveryFiles.Count > 0) result.RecoveryPath = receipt.RecoveryFiles[receipt.RecoveryFiles.Count - 1].Path;
                    throw new InvalidDataException("codex_concurrent_edit_preserved");
                }
                byte[] before = null;
                if (receipt.OriginalExists) {
                    before = ReadBounded(Path.Combine(backupDirectory, "env.before.bin"), MaximumEnvBytes);
                    if (Hash(before) != receipt.BeforeSha256) throw new InvalidDataException("codex_backup_hash_mismatch");
                }
                string current = OptionalHash(envPath);
                if (current == receipt.BeforeSha256) {
                    receipt.State = "restored"; SaveReceipt(backupDirectory, receipt);
                    result.Success = true; return result;
                }
                if (current != receipt.AfterSha256) throw new InvalidDataException("codex_restore_conflict");
                if (beforeCommit != null) beforeCommit();
                if (receipt.OriginalExists) {
                    AtomicWrite(envPath, before, receipt.AfterSha256, delegate { result.Changed = true; }, backupDirectory,
                        PreservationCallback(receipt, result), mutationWindow);
                } else {
                    // A newly created file is removed only through Recycle Bin.
                    // Retain a second full copy before the external shell operation.
                    byte[] created = ReadBounded(envPath, MaximumEnvBytes);
                    if (Hash(created) != receipt.AfterSha256) throw new InvalidDataException("codex_restore_conflict");
                    string undoCopy = Path.Combine(backupDirectory, "env.created.bin");
                    if (!File.Exists(undoCopy)) WritePrivate(undoCopy, created);
                    if (Hash(ReadBounded(undoCopy, MaximumEnvBytes)) != receipt.AfterSha256)
                        throw new InvalidDataException("codex_backup_hash_mismatch");
                    RecycleCreatedFile(backupDirectory, receipt, result);
                }
                if (OptionalHash(envPath) != receipt.BeforeSha256) throw new InvalidDataException("codex_restore_verification_failed");
                receipt.State = "restored"; SaveReceipt(backupDirectory, receipt); result.Success = true;
            } catch (Exception error) {
                result.Error = SafeError(error);
                if (receipt != null && result.Changed) {
                    receipt.State = "restore_failed_after_write"; receipt.ReviewRequired = true;
                    try { SaveReceipt(backupDirectory, receipt); } catch { }
                }
            }
            return result;
        }

        private void RecycleCreatedFile(string transaction, Receipt receipt, CodexProxyChange result) {
            string stagingDirectory = NewPrivateDirectoryAt(configDirectory, ".whale-codex-undo-");
            string captured = Path.Combine(stagingDirectory, "env.pending.bin");
            try {
                using (var guard = OpenGuard(envPath)) {
                    if (Hash(ReadStream(guard, MaximumEnvBytes)) != receipt.AfterSha256) throw new InvalidDataException("codex_restore_conflict");
                    RequireSameFile(guard, envPath);
                    if (mutationWindow != null) mutationWindow("recycle-rename");
                    // Move is atomic and does not overwrite. Capture whichever object
                    // really occupies .env, then validate that captured object.
                    File.Move(envPath, captured); result.Changed = true;
                }
                if (mutationWindow != null) mutationWindow("recycle-captured");
                string capturedHash = PreserveCaptured(captured, transaction, PreservationCallback(receipt, result));
                if (capturedHash != receipt.AfterSha256) throw new InvalidDataException("codex_concurrent_edit_preserved");
                using (var guard = OpenGuard(captured)) {
                    if (Hash(ReadStream(guard, MaximumEnvBytes)) != receipt.AfterSha256) throw new InvalidDataException("codex_concurrent_edit_preserved");
                    RequireSameFile(guard, captured);
                    if (mutationWindow != null) mutationWindow("recycle");
                    // The shell receives a random, private staging path, never .env.
                    // A new .env created by an editor at this point is left alone.
                    recycle(captured);
                }
                if (File.Exists(captured) || Directory.Exists(captured)) throw new InvalidDataException("codex_recycle_incomplete");
                if (File.Exists(envPath) || Directory.Exists(envPath)) throw new InvalidDataException("codex_restore_conflict");
            } catch {
                // Return a retained staged file only with an atomic non-overwriting
                // move. Never replace a new .env that appeared after our capture.
                if (File.Exists(captured) && result.RecoveryPath != null && !SamePath(result.RecoveryPath, captured) && File.Exists(result.RecoveryPath)) {
                    try { AssertPlainPath(captured); AssertPlainPath(envPath); File.Move(captured, envPath); }
                    catch { }
                }
                throw;
            } finally {
                // Remove only our empty, newly-created staging directory.
                try { AssertPlainPath(stagingDirectory); if (Directory.GetFileSystemEntries(stagingDirectory).Length == 0) Directory.Delete(stagingDirectory); } catch { }
            }
        }

        private static Action<string, string> PreservationCallback(Receipt receipt, CodexProxyChange result) {
            return delegate(string path, string hash) {
                result.RecoveryPath = path;
                if (receipt.RecoveryFiles == null) receipt.RecoveryFiles = new List<RecoveryFile>();
                foreach (RecoveryFile item in receipt.RecoveryFiles) if (SamePath(item.Path, path)) { item.Sha256 = hash; return; }
                receipt.RecoveryFiles.Add(new RecoveryFile { Path = path, Sha256 = hash });
            };
        }

        private static byte[] Edit(byte[] original, string http, string all) {
            bool bom = original.Length >= 3 && original[0] == 239 && original[1] == 187 && original[2] == 191;
            string text = Utf8.GetString(original, bom ? 3 : 0, original.Length - (bom ? 3 : 0));
            foreach (char c in text) if ((c < 32 && c != '\t' && c != '\r' && c != '\n') || c == '\uFEFF')
                throw new InvalidDataException("codex_invalid_env");
            var output = new StringBuilder(); var seen = new HashSet<string>(StringComparer.Ordinal);
            string preferredNewline = null; int start = 0;
            while (start < text.Length) {
                int end = start; while (end < text.Length && text[end] != '\r' && text[end] != '\n') end++;
                string newline = "";
                if (end < text.Length) newline = text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? "\r\n" : text[end].ToString();
                if (preferredNewline == null && newline.Length > 0) preferredNewline = newline;
                string line = text.Substring(start, end - start);
                output.Append(EditLine(line, http, all, seen)); output.Append(newline);
                start = end + newline.Length;
            }
            string nl = preferredNewline ?? "\r\n";
            var missing = new List<string>(); foreach (string key in RequiredKeys) if (!seen.Contains(key)) missing.Add(key);
            if (missing.Count > 0) {
                bool ended = text.Length > 0 && (text[text.Length - 1] == '\r' || text[text.Length - 1] == '\n');
                if (output.Length > 0 && !ended) output.Append(nl);
                for (int i = 0; i < missing.Count; i++) {
                    string key = missing[i]; output.Append(key).Append('=').Append(ValueFor(key, http, all));
                    if (i < missing.Count - 1 || ended || text.Length == 0) output.Append(nl);
                }
            }
            byte[] content = Utf8.GetBytes(output.ToString());
            if (content.Length + (bom ? 3 : 0) > MaximumEnvBytes) throw new InvalidDataException("codex_env_too_large");
            if (!bom) return content;
            byte[] bytes = new byte[content.Length + 3]; bytes[0] = 239; bytes[1] = 187; bytes[2] = 191;
            Buffer.BlockCopy(content, 0, bytes, 3, content.Length); return bytes;
        }

        private static string EditLine(string line, string http, string all, HashSet<string> seen) {
            int p = SkipSpace(line, 0); if (p == line.Length || line[p] == '#') return line;
            if (line.Substring(p).StartsWith("export", StringComparison.Ordinal) && p + 6 < line.Length && IsSpace(line[p + 6])) p = SkipSpace(line, p + 6);
            int keyStart = p;
            if (p >= line.Length || !(Char.IsLetter(line[p]) && line[p] <= 127 || line[p] == '_')) throw new InvalidDataException("codex_invalid_env");
            p++; while (p < line.Length && (line[p] <= 127 && Char.IsLetterOrDigit(line[p]) || line[p] == '_')) p++;
            string key = line.Substring(keyStart, p - keyStart); p = SkipSpace(line, p);
            if (p >= line.Length || line[p++] != '=') throw new InvalidDataException("codex_invalid_env");
            p = SkipSpace(line, p); int valueStart = p, valueEnd;
            if (p < line.Length && (line[p] == '\'' || line[p] == '"')) {
                char quote = line[p++]; valueStart = p; bool closed = false;
                while (p < line.Length) {
                    if (line[p] == '\\' && quote == '"') { p += 2; continue; }
                    if (line[p] == quote) { closed = true; break; } p++;
                }
                if (!closed) throw new InvalidDataException("codex_multiline_env_not_supported");
                valueEnd = p; p = SkipSpace(line, p + 1);
                if (p < line.Length && line[p] != '#') throw new InvalidDataException("codex_invalid_env");
            } else {
                int comment = line.IndexOf('#', p); valueEnd = comment < 0 ? line.Length : comment;
                while (valueEnd > valueStart && IsSpace(line[valueEnd - 1])) valueEnd--;
                for (int i = valueStart; i < valueEnd; i++) if (line[i] == '\'' || line[i] == '"' || line[i] == '`') throw new InvalidDataException("codex_invalid_env");
                if (valueEnd > valueStart && line[valueEnd - 1] == '\\') throw new InvalidDataException("codex_multiline_env_not_supported");
            }
            string replacement = ValueFor(key, http, all);
            if (replacement == null) return line;
            seen.Add(key);
            return line.Substring(0, valueStart) + replacement + line.Substring(valueEnd);
        }

        private static string ValueFor(string key, string http, string all) {
            switch (key.ToUpperInvariant()) {
                case "HTTP_PROXY": case "HTTPS_PROXY": case "WSS_PROXY": return http;
                case "ALL_PROXY": return all;
                case "NO_PROXY": return "localhost,127.0.0.1,::1";
                default: return null;
            }
        }
        private static bool IsSpace(char c) { return c == ' ' || c == '\t'; }
        private static int SkipSpace(string text, int p) { while (p < text.Length && IsSpace(text[p])) p++; return p; }
        private static void ValidateProxy(string text, bool allowSocks) {
            if (text == null || text.Length > 128) throw new InvalidDataException("codex_invalid_proxy_uri");
            var match = Regex.Match(text, @"\A(http|socks5h)://(localhost|127(?:\.[0-9]{1,3}){3}|\[::1\]):([0-9]{1,5})\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            int port;
            if (!match.Success || !allowSocks && !String.Equals(match.Groups[1].Value, "http", StringComparison.OrdinalIgnoreCase) ||
                !Int32.TryParse(match.Groups[3].Value, out port) || port < 1 || port > 65535)
                throw new InvalidDataException("codex_invalid_proxy_uri");
            string host = match.Groups[2].Value;
            if (host.StartsWith("127.", StringComparison.Ordinal)) {
                IPAddress address;
                foreach (string part in host.Split('.')) if (part.Length > 1 && part[0] == '0') throw new InvalidDataException("codex_invalid_proxy_uri");
                if (!IPAddress.TryParse(host, out address) || !IPAddress.IsLoopback(address)) throw new InvalidDataException("codex_invalid_proxy_uri");
            }
        }

        private Receipt ReadReceipt(string directory) {
            AssertPlainPath(directory); string full = Path.GetFullPath(directory).TrimEnd('\\', '/');
            if (!SamePath(Path.GetDirectoryName(full), backupRoot) || !Path.GetFileName(full).StartsWith("codex-proxy-", StringComparison.Ordinal))
                throw new InvalidDataException("codex_invalid_backup_path");
            Receipt receipt = Json().Deserialize<Receipt>(Utf8.GetString(ReadBounded(Path.Combine(full, "receipt.json"), 65536)));
            if (receipt == null || receipt.Schema != 1 || String.IsNullOrEmpty(receipt.TargetPath) ||
                !IsHash(receipt.AfterSha256) || receipt.OriginalExists && !IsHash(receipt.BeforeSha256) ||
                !receipt.OriginalExists && receipt.BeforeSha256 != "absent" ||
                !new HashSet<string>(new[] { "prepared", "applied", "restored", "apply_failed", "apply_failed_after_write", "restore_failed_after_write" }).Contains(receipt.State))
                throw new InvalidDataException("codex_invalid_backup_receipt");
            AssertPlainPath(receipt.TargetPath);
            return receipt;
        }
        private static void SaveReceipt(string directory, Receipt receipt) {
            string path = Path.Combine(directory, "receipt.json"); byte[] bytes = Utf8.GetBytes(Json().Serialize(receipt));
            AtomicWrite(path, bytes, OptionalHash(path), null, directory, null, null);
        }
        private static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = 65536, RecursionLimit = 16 }; }
        private sealed class Receipt {
            public Receipt() { }
            public int Schema;
            public bool OriginalExists, ReviewRequired;
            public string State, TargetPath, BeforeSha256, AfterSha256, CreatedUtc;
            public List<RecoveryFile> RecoveryFiles = new List<RecoveryFile>();
        }
        private sealed class RecoveryFile { public RecoveryFile() { } public string Path, Sha256; }

        private string NewPrivateDirectory() {
            return NewPrivateDirectoryAt(backupRoot, "codex-proxy-");
        }
        private static string NewPrivateDirectoryAt(string root, string prefix) {
            AssertPlainPath(root); Directory.CreateDirectory(root); AssertPlainPath(root);
            string path = Path.Combine(root, prefix + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N"));
            var sid = WindowsIdentity.GetCurrent().User; var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false); security.SetOwner(sid);
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.CreateDirectory(path, security); AssertPlainPath(path); return path;
        }
        private static void WritePrivate(string path, byte[] bytes) {
            AssertPlainPath(path); var sid = WindowsIdentity.GetCurrent().User; var security = new FileSecurity();
            security.SetAccessRuleProtection(true, false); security.SetOwner(sid);
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.WriteThrough, security)) {
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
        }
        private static void AtomicWrite(string path, byte[] bytes, string expectedHash, Action committed,
            string transaction, Action<string, string> preserved, Action<string> mutationHook) {
            AssertPlainPath(path); string parent = Path.GetDirectoryName(path); Directory.CreateDirectory(parent); AssertPlainPath(parent);
            string temporary = Path.Combine(parent, ".whale-codex-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                WritePrivate(temporary, bytes);
                if (Hash(ReadBounded(temporary, MaximumEnvBytes)) != Hash(bytes)) throw new InvalidDataException("codex_write_verification_failed");
                AssertPlainPath(path);
                if (expectedHash == "absent") {
                    if (File.Exists(path) || Directory.Exists(path)) throw new InvalidDataException("codex_env_changed");
                    File.Move(temporary, path); if (committed != null) committed();
                } else {
                    if (!File.Exists(path)) throw new InvalidDataException("codex_env_changed");
                    AssertPlainPath(transaction);
                    string displaced = Path.Combine(transaction, (preserved == null ? "receipt.previous-" : "env.displaced-") + Guid.NewGuid().ToString("N") + ".bin");
                    // ReplaceFile requires an atomic backup on the source volume.
                    // For a backup root on another drive, retain the captured file
                    // in a private local staging directory as well as copying it.
                    if (!String.Equals(Path.GetPathRoot(path), Path.GetPathRoot(transaction), StringComparison.OrdinalIgnoreCase))
                        displaced = Path.Combine(NewPrivateDirectoryAt(parent, ".whale-codex-displaced-"), "env.displaced.bin");
                    using (var guard = OpenGuard(path)) {
                        if (Hash(ReadStream(guard, MaximumEnvBytes)) != expectedHash) throw new InvalidDataException("codex_env_changed");
                        RequireSameFile(guard, path);
                        if (mutationHook != null) mutationHook("replace");
                        // The actual file displaced by this atomic operation is
                        // always retained, even if another editor won the race
                        // after RequireSameFile. Never use a null backup path.
                        File.Replace(temporary, path, displaced); if (committed != null) committed();
                        if (preserved != null) preserved(displaced, null);
                    }
                    if (mutationHook != null) mutationHook("replaced");
                    if (PreserveCaptured(displaced, transaction, preserved) != expectedHash)
                        throw new InvalidDataException("codex_concurrent_edit_preserved");
                }
                if (Hash(ReadBounded(path, MaximumEnvBytes)) != Hash(bytes)) throw new InvalidDataException("codex_write_verification_failed");
            } finally {
                // Only our uncommitted generated staging file is deleted; original
                // .env bytes are retained in the verified transaction backup.
                if (File.Exists(temporary)) { AssertPlainPath(temporary); File.Delete(temporary); }
            }
        }

        private static string PreserveCaptured(string captured, string transaction, Action<string, string> preserved) {
            // Record the retained path before any operation which can fail. An
            // unexpectedly large or unreadable raced file still remains on disk.
            if (preserved != null) preserved(captured, null);
            AssertPlainPath(captured);
            var sid = WindowsIdentity.GetCurrent().User; var security = new FileSecurity();
            security.SetAccessRuleProtection(true, false); security.SetOwner(sid);
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
            File.SetAccessControl(captured, security);
            if (new FileInfo(captured).Length > MaximumEnvBytes) throw new InvalidDataException("codex_concurrent_edit_preserved");
            byte[] bytes = ReadBounded(captured, MaximumEnvBytes); string hash = Hash(bytes);
            if (preserved != null) preserved(captured, hash);
            if (!SamePath(Path.GetDirectoryName(captured), transaction)) {
                string archive = Path.Combine(transaction, "env.displaced-" + Guid.NewGuid().ToString("N") + ".bin");
                WritePrivate(archive, bytes);
                if (Hash(ReadBounded(archive, MaximumEnvBytes)) != hash) throw new InvalidDataException("codex_backup_verification_failed");
                if (preserved != null) preserved(archive, hash);
            }
            return hash;
        }

        private static FileStream OpenGuard(string path) {
            AssertPlainPath(path);
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            try { FileInfoFor(stream); return stream; } catch { stream.Dispose(); throw; }
        }
        private static void RequireSameFile(FileStream guard, string path) {
            AssertPlainPath(path);
            using (var check = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)) {
                NativeFileInfo a = FileInfoFor(guard), b = FileInfoFor(check);
                if (a.VolumeSerial != b.VolumeSerial || a.IndexHigh != b.IndexHigh || a.IndexLow != b.IndexLow)
                    throw new InvalidDataException("codex_env_changed");
            }
        }
        private static byte[] ReadOptional(string path) {
            AssertPlainPath(path);
            if (Directory.Exists(path)) throw new InvalidDataException("codex_env_is_directory");
            return File.Exists(path) ? ReadBounded(path, MaximumEnvBytes) : null;
        }
        private static string OptionalHash(string path) { byte[] bytes = ReadOptional(path); return bytes == null ? "absent" : Hash(bytes); }
        private static byte[] ReadBounded(string path, int maximum) {
            AssertPlainPath(path);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                FileInfoFor(stream); return ReadStream(stream, maximum);
            }
        }
        private static byte[] ReadStream(FileStream stream, int maximum) {
            if (stream.Length > maximum) throw new InvalidDataException("codex_env_too_large");
            byte[] bytes = new byte[(int)stream.Length]; int offset = 0; stream.Position = 0;
            while (offset < bytes.Length) { int count = stream.Read(bytes, offset, bytes.Length - offset); if (count == 0) throw new EndOfStreamException(); offset += count; }
            return bytes;
        }
        private static string Hash(byte[] bytes) { using (var algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static bool IsHash(string value) { return value != null && Regex.IsMatch(value, @"\A[a-f0-9]{64}\z"); }
        private static bool SamePath(string left, string right) { return String.Equals(Path.GetFullPath(left).TrimEnd('\\', '/'), Path.GetFullPath(right).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase); }
        private static bool Inside(string path, string root) { return SamePath(path, root) || path.StartsWith(root.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }

        internal static void AssertPlainPath(string path) {
            if (String.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal) ||
                !Regex.IsMatch(path, @"\A[A-Za-z]:[\\/]")) throw new InvalidDataException("codex_invalid_local_path");
            string tail = path.Substring(Path.GetPathRoot(path).Length);
            foreach (string part in tail.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)) {
                if (part == "." || part == ".." || part.EndsWith(".", StringComparison.Ordinal) || part.EndsWith(" ", StringComparison.Ordinal) ||
                    part.IndexOfAny(new[] { ':', '*', '?', '"', '<', '>', '|' }) >= 0 ||
                    Regex.IsMatch(part.Split('.')[0], @"\A(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\z", RegexOptions.IgnoreCase))
                    throw new InvalidDataException("codex_invalid_local_path");
            }
            string cursor = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(cursor)) {
                FileAttributes attributes;
                try { attributes = File.GetAttributes(cursor); }
                catch (FileNotFoundException) { cursor = Path.GetDirectoryName(cursor); continue; }
                catch (DirectoryNotFoundException) { cursor = Path.GetDirectoryName(cursor); continue; }
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("codex_linked_path_not_supported");
                if ((attributes & FileAttributes.Directory) == 0) using (var stream = new FileStream(cursor, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)) { FileInfoFor(stream); }
                cursor = Path.GetDirectoryName(cursor);
            }
        }
        private static NativeFileInfo FileInfoFor(FileStream stream) {
            NativeFileInfo info;
            if (!GetFileInformationByHandle(stream.SafeFileHandle, out info)) throw new InvalidDataException("codex_file_identity_unavailable");
            if (info.NumberOfLinks != 1) throw new InvalidDataException("codex_hardlinked_path_not_supported");
            if ((info.Attributes & (uint)FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("codex_linked_path_not_supported");
            return info;
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativeFileInfo {
            public uint Attributes; public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
            public uint VolumeSerial, SizeHigh, SizeLow, NumberOfLinks, IndexHigh, IndexLow;
        }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle, out NativeFileInfo information);
        private static void RecycleWindows(string path) {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin, Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
        }
        private static string SafeError(Exception error) {
            if (error is InvalidDataException && Regex.IsMatch(error.Message, @"\Acodex_[a-z_]{1,80}\z")) return error.Message;
            if (error is DecoderFallbackException) return "codex_invalid_utf8";
            if (error is UnauthorizedAccessException) return "codex_access_denied";
            if (error is IOException) return "codex_file_io_failed";
            return "codex_operation_failed";
        }
    }
}
