using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace WhalePack {
    public static class LocalPath {
        private static string NormalizeLocalRoot(string path) {
            if (String.IsNullOrWhiteSpace(path)) throw new IOException("路径为空 / Path is empty.");
            string normalized = path.Replace('/', '\\');
            // Reject UNC and device namespaces before any File/Directory operation can touch a server.
            if (normalized.StartsWith("\\\\", StringComparison.Ordinal)) throw new IOException("仅支持本机磁盘，不读取网络位置 / Local disks only; network paths are not supported.");
            string full = Path.GetFullPath(path); string root = Path.GetPathRoot(full);
            if (String.IsNullOrEmpty(root) || (root.Length > 1 && root[1] != ':')) throw new IOException("只支持本机路径 / Local paths only.");
            if (new DriveInfo(root).DriveType == DriveType.Network) throw new IOException("不读取映射网络盘 / Mapped network drives are not supported.");
            return full;
        }
        public static string Require(string path) {
            string full = NormalizeLocalRoot(path);
            if (HasReparseComponentUnchecked(full)) throw new IOException("不读取符号链接或重解析目录 / Symbolic links and reparse paths are not supported.");
            return full;
        }
        public static bool HasReparseComponent(string path) { return HasReparseComponentUnchecked(NormalizeLocalRoot(path)); }
        private static bool HasReparseComponentUnchecked(string full) {
            string root = Path.GetPathRoot(full); string current = root;
            string remainder = full.Substring(root.Length);
            // Walk from drive root to leaf, never asking File.Exists about a descendant before
            // confirming that its parent is not a junction to a remote or unrelated location.
            string[] components = remainder.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = -1; i < components.Length; i++) {
                if (i >= 0) current = Path.Combine(current, components[i]);
                try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true; }
                catch (FileNotFoundException) { return false; }
                catch (DirectoryNotFoundException) { return false; }
            }
            return false;
        }
    }
    public static class SecretDetector {
        private static readonly Regex Token = new Regex(@"(?:sk-[A-Za-z0-9_\-]{16,}|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|AKIA[0-9A-Z]{16}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        private static readonly HashSet<string> TextExtensions = new HashSet<string>(new string[] { ".txt", ".md", ".json", ".jsonl", ".xml", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf", ".config", ".cs", ".js", ".ts", ".tsx", ".jsx", ".py", ".ps1", ".bat", ".cmd", ".sh", ".html", ".css", ".csv", ".log", ".sql", ".env", ".properties" }, StringComparer.OrdinalIgnoreCase);
        public static string CheckText(string text) { return Token.IsMatch(text ?? "") ? "文本含疑似密钥样式 / Secret-like text detected" : ""; }
        public static string NameReason(string path) {
            string name = System.IO.Path.GetFileName(path).ToLowerInvariant();
            string ext = System.IO.Path.GetExtension(name);
            if (name == ".env" || name.StartsWith(".env.", StringComparison.Ordinal) || name.StartsWith("id_rsa", StringComparison.Ordinal) || name.StartsWith("id_ed25519", StringComparison.Ordinal) || name.StartsWith("credentials", StringComparison.Ordinal) || name == "cookies" || name.StartsWith("cookies.", StringComparison.Ordinal) || name == "login data" || name == "web data" || ext == ".pem" || ext == ".key" || ext == ".pfx" || ext == ".p12")
                return "可能含密钥的文件名 / Potential credential filename";
            return BrowserReason(path);
        }
        public static string BrowserReason(string path) {
            string p = path.Replace('/', '\\').ToLowerInvariant();
            string[] markers = { "\\user data\\", "\\firefox\\profiles\\", "\\mozilla\\firefox\\", "\\google\\chrome\\", "\\microsoft\\edge\\", "\\bravesoftware\\", "\\browser-profiles\\", "\\browser_profiles\\" };
            foreach (string marker in markers) if ((p + "\\").IndexOf(marker, StringComparison.Ordinal) >= 0) return "浏览器配置目录 / Browser profile";
            return "";
        }
        public static string CheckFile(string path) { return CheckFile(path, CancellationToken.None); }
        public static string CheckFile(string path, CancellationToken cancellation) {
            path = LocalPath.Require(path);
            string named = NameReason(path);
            if (named.Length > 0) return named;
            using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                byte[] head = new byte[4096]; int n = input.Read(head, 0, head.Length); input.Position = 0;
                bool utf16 = n >= 2 && ((head[0] == 255 && head[1] == 254) || (head[0] == 254 && head[1] == 255));
                bool likelyUtf16LE = false, likelyUtf16BE = false;
                if (!utf16 && n >= 8) {
                    int evenZero = 0, oddZero = 0;
                    for (int i = 0; i < n; i++) if (head[i] == 0) { if ((i & 1) == 0) evenZero++; else oddZero++; }
                    likelyUtf16LE = oddZero > n / 4 && evenZero < n / 20;
                    likelyUtf16BE = evenZero > n / 4 && oddZero < n / 20;
                }
                bool knownText = TextExtensions.Contains(System.IO.Path.GetExtension(path));
                bool binary = false;
                if (!utf16 && !likelyUtf16LE && !likelyUtf16BE) for (int i = 0; i < n; i++) if (head[i] == 0) { binary = true; break; }
                if (binary && !knownText) return "";
                Encoding encoding = likelyUtf16LE ? Encoding.Unicode : likelyUtf16BE ? Encoding.BigEndianUnicode : Encoding.UTF8;
                using (StreamReader reader = new StreamReader(input, encoding, true, 65536)) {
                    char[] buffer = new char[65536]; string tail = ""; int count;
                    while ((count = reader.Read(buffer, 0, buffer.Length)) > 0) {
                        cancellation.ThrowIfCancellationRequested();
                        string chunk = tail + new string(buffer, 0, count);
                        if (Token.IsMatch(chunk)) return "文本含疑似密钥样式 / Secret-like text detected";
                        tail = chunk.Substring(Math.Max(0, chunk.Length - 256));
                    }
                }
            }
            return "";
        }
    }
    public static class FileScanner {
        private sealed class Approval { public string Hash; }
        private static readonly ConditionalWeakTable<SourceEntry, Approval> Approvals = new ConditionalWeakTable<SourceEntry, Approval>();
        private static readonly HashSet<string> SkippedDirectories = new HashSet<string>(new string[] { ".git", "bin", "obj", "node_modules", "__pycache__", ".vs", "artifacts", "runtime", "models" }, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SkippedExtensions = new HashSet<string>(new string[] { ".exe", ".dll", ".pdb", ".zip", ".7z", ".iso", ".msi" }, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Videos = new HashSet<string>(new string[] { ".mp4", ".mov", ".mkv", ".webm", ".m4v", ".avi", ".mts", ".m2ts" }, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Images = new HashSet<string>(new string[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp" }, StringComparer.OrdinalIgnoreCase);
        public static ProcessingMode Classify(string path) {
            LocalPath.Require(path);
            if (Directory.Exists(path)) return ProcessingMode.SourceArchive;
            if (SecretDetector.NameReason(path).Length > 0) return ProcessingMode.Exclude;
            string extension = System.IO.Path.GetExtension(path);
            if (Videos.Contains(extension)) return ProcessingMode.CompressVideo;
            if (Images.Contains(extension)) return ProcessingMode.ResizeImage;
            return ProcessingMode.Original;
        }
        public static bool IsSkippedDirectory(string name) { return SkippedDirectories.Contains(name); }
        public static string SkipReason(string path, bool inSource) {
            if (!inSource) return "";
            if (SkippedExtensions.Contains(System.IO.Path.GetExtension(path))) return "默认跳过二进制或归档 / Binary or archive";
            if (new FileInfo(path).Length > Limits.MaxSourceFileBytes) return "单个源码文件超过 20 MB / Source file exceeds 20 MB";
            return "";
        }
        public static InputItem Scan(string path, CancellationToken cancellation) {
            path = LocalPath.Require(path);
            if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("找不到所选项目 / Selected item not found", path);
            InputItem result = new InputItem { Path = path, DisplayName = System.IO.Path.GetFileName(path.TrimEnd('\\', '/')), IsDirectory = Directory.Exists(path), Mode = Classify(path) };
            if (result.DisplayName.Length == 0) result.DisplayName = "files";
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) {
                result.Entries.Add(new SourceEntry { FullPath = path, RelativePath = result.DisplayName, Excluded = true, CannotInclude = true, IsDirectory = result.IsDirectory, Reason = "链接或重解析点不打包 / Reparse point excluded" });
                result.Mode = ProcessingMode.Exclude;
            } else if (result.IsDirectory) Walk(path, path, "", false, result, cancellation);
            else AddFile(path, result.DisplayName, "", false, false, result, cancellation);
            if (!result.IsDirectory && result.Entries.Count > 0 && result.Entries[0].Sensitive) result.Mode = ProcessingMode.Exclude;
            RefreshEstimate(result);
            return result;
        }
        private static void Walk(string root, string directory, string inheritedReason, bool inheritedSensitive, InputItem item, CancellationToken cancellation) {
            cancellation.ThrowIfCancellationRequested();
            if (item.Entries.Count >= 200000) throw new IOException("文件超过 200,000 个，请拆成较小的文件夹 / Too many files; select a smaller folder.");
            string reason = inheritedReason; bool sensitive = inheritedSensitive;
            string browser = SecretDetector.BrowserReason(directory);
            if (browser.Length > 0) { reason = browser; sensitive = true; }
            if (reason.Length == 0 && SkippedDirectories.Contains(System.IO.Path.GetFileName(directory))) reason = "默认跳过目录 / Skipped folder: " + System.IO.Path.GetFileName(directory);
            string[] children;
            try { children = Directory.GetFileSystemEntries(directory); }
            catch (Exception ex) {
                if (!(ex is IOException) && !(ex is UnauthorizedAccessException)) throw;
                item.Entries.Add(new SourceEntry { FullPath = directory, RelativePath = Relative(root, directory), IsDirectory = true, CannotInclude = true, Excluded = true, Reason = "目录无法读取 / Folder unreadable" }); return;
            }
            Array.Sort(children, StringComparer.OrdinalIgnoreCase);
            foreach (string child in children) {
                cancellation.ThrowIfCancellationRequested();
                try {
                    FileAttributes attrs = File.GetAttributes(child);
                    bool dir = (attrs & FileAttributes.Directory) != 0;
                    if ((attrs & FileAttributes.ReparsePoint) != 0) {
                        item.Entries.Add(new SourceEntry { FullPath = child, RelativePath = Relative(root, child), IsDirectory = dir, Excluded = true, CannotInclude = true, Reason = "链接或重解析点不打包 / Reparse point excluded" });
                    } else if (dir) Walk(root, child, reason, sensitive, item, cancellation);
                    else AddFile(child, Relative(root, child), reason, sensitive, true, item, cancellation);
                } catch (Exception ex) {
                    if (!(ex is IOException) && !(ex is UnauthorizedAccessException)) throw;
                    item.Entries.Add(new SourceEntry { FullPath = child, RelativePath = Relative(root, child), Excluded = true, CannotInclude = true, Reason = "文件无法读取 / File unreadable" });
                }
            }
        }
        private static string Relative(string root, string path) { return path.Length > root.Length ? path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/') : System.IO.Path.GetFileName(path); }
        private static void AddFile(string path, string relative, string inherited, bool inheritedSensitive, bool inSource, InputItem item, CancellationToken cancellation) {
            if (item.Entries.Count >= 200000) throw new InvalidOperationException("文件超过 200,000 个，请选择较小文件夹 / More than 200,000 files; select a smaller folder.");
            SourceEntry entry = new SourceEntry { FullPath = path, RelativePath = relative, Length = new FileInfo(path).Length, Reason = inherited, Sensitive = inheritedSensitive };
            item.OriginalBytes = checked(item.OriginalBytes + entry.Length);
            string name = SecretDetector.NameReason(path);
            if (name.Length > 0) { entry.Reason = name; entry.Sensitive = true; }
            if (entry.Reason.Length == 0) entry.Reason = SkipReason(path, inSource);
            // Do not read files already excluded by a source rule. Review invokes a fresh scan before approval.
            if (entry.Reason.Length == 0) { string secret = SecretDetector.CheckFile(path, cancellation); if (secret.Length > 0) { entry.Reason = secret; entry.Sensitive = true; } }
            entry.Excluded = entry.Reason.Length > 0;
            item.Entries.Add(entry);
        }
        public static string ReviewReason(SourceEntry entry) {
            if (entry.CannotInclude || entry.IsDirectory) return entry.Reason;
            string reason = SecretDetector.CheckFile(entry.FullPath);
            if (reason.Length > 0) { entry.Sensitive = true; entry.Reason = reason; }
            return entry.Reason;
        }
        public static void Approve(SourceEntry entry) {
            if (entry.CannotInclude || entry.IsDirectory) throw new InvalidOperationException("该项目不能放回 / This item cannot be included.");
            if (HasReparseAncestor(entry.FullPath)) throw new IOException("链接路径不能放回 / Reparse path cannot be included.");
            string reason = SecretDetector.CheckFile(entry.FullPath);
            if (reason.Length > 0) { entry.Sensitive = true; entry.Reason = reason; }
            lock (Approvals) { Approvals.Remove(entry); Approvals.Add(entry, new Approval { Hash = HashFile(entry.FullPath) }); }
            entry.Length = new FileInfo(entry.FullPath).Length;
            entry.UserIncluded = true; entry.Excluded = false;
        }
        internal static bool ApprovedContent(SourceEntry entry, string stagedPath) {
            Approval value;
            lock (Approvals) return entry.UserIncluded && Approvals.TryGetValue(entry, out value) && String.Equals(value.Hash, HashFile(stagedPath), StringComparison.Ordinal);
        }
        public static bool HasReparseAncestor(string path) {
            return LocalPath.HasReparseComponent(path);
        }
        public static string HashFile(string path) {
            path = LocalPath.Require(path);
            using (SHA256 sha = SHA256.Create()) using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }
        public static void RefreshEstimate(InputItem item) {
            if (item.Mode == ProcessingMode.Exclude) { item.EstimatedBytes = 0; return; }
            long included = 0;
            foreach (SourceEntry entry in item.Entries) if (!entry.Excluded && !entry.IsDirectory && !entry.CannotInclude) included = checked(included + entry.Length);
            if (item.Mode == ProcessingMode.CompressVideo) item.EstimatedBytes = Math.Min(included, 27500000);
            else if (item.Mode == ProcessingMode.ResizeImage) item.EstimatedBytes = Math.Min(included, 5000000);
            else item.EstimatedBytes = included;
        }
    }
}
