using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace WhaleClaudeHelper {
    public sealed class FontEntry {
        public string Name, Path, Hint;
        public bool IsUser, CanRemove;
        internal string RegistryName, RegistryData, Stamp;
    }
    public sealed class FontResult {
        public bool Ok;
        public string BackupDirectory, Message;
    }
    internal sealed class FontContext {
        internal string UserFonts = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
        internal string SystemFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        internal string BackupRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DafeiyuClaudeHelper", "Backups", "Fonts");
    }
    internal sealed class FontRegistration {
        internal string Name, Data;
        internal RegistryValueKind Kind;
    }
    internal interface IFontPlatform {
        List<FontRegistration> Read(bool user);
        void Register(string name, string path);
        void Unregister(string name);
        bool Load(string path);
        bool Unload(string path);
        void Notify();
        string[] Families(string path);
        void Recycle(string path);
    }
    internal sealed class WindowsFontPlatform : IFontPlatform {
        private const string FontKey = @"Software\Microsoft\Windows NT\CurrentVersion\Fonts";
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern int AddFontResourceEx(string name, uint flags, IntPtr reserved);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveFontResourceEx(string name, uint flags, IntPtr reserved);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
        public List<FontRegistration> Read(bool user) {
            var result = new List<FontRegistration>();
            using (var key = (user ? Registry.CurrentUser : Registry.LocalMachine).OpenSubKey(FontKey, false)) {
                if (key == null) return result;
                foreach (var name in key.GetValueNames()) {
                    var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    result.Add(new FontRegistration { Name = name, Data = value as string, Kind = key.GetValueKind(name) });
                }
            }
            return result;
        }
        public void Register(string name, string path) {
            using (var key = Registry.CurrentUser.CreateSubKey(FontKey)) {
                if (key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("font_registry_conflict");
                key.SetValue(name, path, RegistryValueKind.String);
            }
        }
        public void Unregister(string name) { using (var key = Registry.CurrentUser.OpenSubKey(FontKey, true)) { if (key == null) throw new InvalidOperationException("font_registry_missing"); key.DeleteValue(name, true); } }
        public bool Load(string path) { return AddFontResourceEx(path, 0, IntPtr.Zero) > 0; }
        public bool Unload(string path) { return RemoveFontResourceEx(path, 0, IntPtr.Zero); }
        public void Notify() { UIntPtr result; SendMessageTimeout(new IntPtr(0xffff), 0x001d, UIntPtr.Zero, IntPtr.Zero, 2, 1500, out result); }
        public string[] Families(string path) {
            using (var fonts = new PrivateFontCollection()) {
                fonts.AddFontFile(path);
                var names = new List<string>();
                foreach (var family in fonts.Families) using (family) names.Add(family.Name);
                return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
        }
        public void Recycle(string path) {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin, Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
        }
    }
    public sealed class FontBackupManifest {
        public int Version = 1;
        public string Operation, CreatedUtc, OriginalPath, RegistryName, RegistryData, Sha256, BackupFile, Status;
        public long Length;
    }
    // Only explicit, current-user font operations. No system font writes or external downloads.
    public sealed class FontService {
        private readonly FontContext context;
        private readonly IFontPlatform platform;
        private readonly object gate = new object();
        private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 65536, RecursionLimit = 8 };
        private const long MaxFontBytes = 32L * 1024 * 1024;
        public FontService() : this(new FontContext(), new WindowsFontPlatform()) { }
        internal FontService(FontContext context, IFontPlatform platform) {
            this.context = context; this.platform = platform;
            foreach (var root in new[] { context.UserFonts, context.SystemFonts, context.BackupRoot }) RequireAbsolute(root);
        }
        public List<FontEntry> ListFonts() {
            lock (gate) {
                var result = new List<FontEntry>();
                try {
                    var user = platform.Read(true); var system = platform.Read(false);
                    foreach (var row in user) {
                        var entry = new FontEntry { Name = row.Name, Path = row.Data ?? "", IsUser = true, RegistryName = row.Name, RegistryData = row.Data, Hint = "font_user_protected" };
                        try {
                            if (row.Kind == RegistryValueKind.String && UserPath(row.Data) && UniqueReference(user, row.Data) && !system.Any(x => SamePath(x.Data, row.Data)) && File.Exists(row.Data)) {
                                SafePath(row.Data); ValidateSize(row.Data);
                                entry.Stamp = MetadataStamp(row.Data); entry.CanRemove = true; entry.Hint = "font_user_removable";
                            }
                        } catch { entry.Hint = "font_path_unavailable_or_linked"; }
                        result.Add(entry);
                    }
                    foreach (var row in system) {
                        string path = row.Data ?? "";
                        if (!String.IsNullOrWhiteSpace(path) && !System.IO.Path.IsPathRooted(path)) path = System.IO.Path.Combine(context.SystemFonts, path);
                        result.Add(new FontEntry { Name = row.Name, Path = path, IsUser = false, CanRemove = false, Hint = "font_system_protected" });
                    }
                } catch { result.Add(new FontEntry { Name = "font_inventory_unavailable", Path = "", Hint = "font_inventory_unavailable" }); }
                return result.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            }
        }
        // Registry display labels alone are insufficient for automatic font selection.
        // A candidate must also contain exactly one matching, allowlisted font family.
        public bool IsExactSignalFamily(FontEntry entry, IEnumerable<string> allowedNames) {
            lock (gate) {
                try {
                    if (entry == null || !entry.IsUser || !entry.CanRemove || allowedNames == null || !UserPath(entry.Path)) return false;
                    SafePath(entry.Path); ValidateSize(entry.Path);
                    if (String.IsNullOrEmpty(entry.Stamp) || entry.Stamp != MetadataStamp(entry.Path)) return false;
                    if (!String.Equals(entry.Name, entry.RegistryName, StringComparison.Ordinal)) return false;
                    string candidate = entry.RegistryName.Trim();
                    foreach (string suffix in new[] { " (TrueType)", " (OpenType)" }) {
                        if (candidate.EndsWith(suffix, StringComparison.Ordinal)) { candidate = candidate.Substring(0, candidate.Length - suffix.Length).Trim(); break; }
                    }
                    if (String.IsNullOrWhiteSpace(candidate) || candidate.Length > 128 || candidate.Any(Char.IsControl)) return false;
                    var allowed = allowedNames.Take(1025).ToList();
                    if (allowed.Count > 1024 || allowed.Any(x => String.IsNullOrWhiteSpace(x) || x.Length > 128 || x.Any(Char.IsControl))) return false;
                    if (!allowed.Any(x => String.Equals(x.Trim(), candidate, StringComparison.OrdinalIgnoreCase))) return false;
                    var manifest = ManifestForRemoval(entry); RequireRegistration(manifest);
                    if (!AutomaticAliasesSafe(entry)) return false;
                    string[] families = platform.Families(entry.Path);
                    if (families == null || families.Length != 1 || !String.Equals(families[0], candidate, StringComparison.OrdinalIgnoreCase)) return false;
                    // Re-read both sources after the font parser; never promote a stale preview.
                    RequireRegistration(manifest); Verify(entry.Path, manifest);
                    if (!AutomaticAliasesSafe(entry)) return false;
                    return entry.Stamp == MetadataStamp(entry.Path);
                } catch { return false; }
            }
        }
        private bool AutomaticAliasesSafe(FontEntry entry) {
            string candidateFile = System.IO.Path.GetFileName(entry.Path);
            foreach (bool user in new[] { true, false }) {
                foreach (var row in platform.Read(user)) {
                    if (user && String.Equals(row.Name, entry.RegistryName, StringComparison.OrdinalIgnoreCase) && String.Equals(row.Data, entry.RegistryData, StringComparison.Ordinal)) continue;
                    if (String.IsNullOrWhiteSpace(row.Data)) continue;
                    // Expansion is for conservative alias comparison only. Mutation paths remain literal.
                    string alias = Environment.ExpandEnvironmentVariables(row.Data.Trim());
                    if (alias.IndexOf('%') >= 0 || alias.IndexOf('~') >= 0) return false;
                    string aliasFile;
                    try { aliasFile = System.IO.Path.GetFileName(alias).Split(':')[0]; }
                    catch { return false; }
                    bool sameFileName = String.Equals(aliasFile, candidateFile, StringComparison.OrdinalIgnoreCase);
                    if (Regex.IsMatch(alias, @"^[A-Za-z]:[\\/]")) {
                        try { if (SamePath(alias, entry.Path)) return false; RequireAbsolute(alias); Full(alias); }
                        catch { if (sameFileName) return false; }
                    } else if (System.IO.Path.IsPathRooted(alias)) {
                        // A drive-relative/UNC spelling is not a verified local alias.
                        if (sameFileName) return false;
                    } else if (user) {
                        // Per-user relative aliases have ambiguous resolution across Windows versions.
                        if (sameFileName) return false;
                    } else {
                        try {
                            string resolved = Full(System.IO.Path.Combine(context.SystemFonts, alias));
                            if (SamePath(resolved, entry.Path)) return false;
                        } catch { if (sameFileName) return false; }
                    }
                }
            }
            return true;
        }
        public FontResult Install(string path) {
            lock (gate) {
                string backup = null; FontBackupManifest manifest = null;
                bool copied = false, loaded = false, registered = false;
                try {
                    RequireAbsolute(path); SafePath(path); ValidateSize(path);
                    string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
                    if (extension != ".ttf" && extension != ".otf") return Result(false, null, "font_choose_ttf_or_otf");
                    string hash = Hash(path);
                    var families = platform.Families(path);
                    if (families == null || families.Length == 0 || families.Length > 32 || families.Any(x => String.IsNullOrWhiteSpace(x) || x.Length > 128 || x.Any(Char.IsControl))) return Result(false, null, "font_invalid_family");
                    string name = String.Join(", ", families) + (extension == ".otf" ? " (OpenType)" : " (TrueType)");
                    if (name.Length > 512) return Result(false, null, "font_invalid_family");
                    string target = System.IO.Path.Combine(context.UserFonts, "Dafeiyu-" + hash.Substring(0, 24) + extension);
                    RequireNoConflict(target, name);
                    manifest = new FontBackupManifest { Operation = "install", CreatedUtc = DateTime.UtcNow.ToString("o"), OriginalPath = target, RegistryName = name, RegistryData = target, Sha256 = hash, Length = new FileInfo(path).Length, BackupFile = "font" + extension, Status = "prepared" };
                    backup = Backup(path, manifest);
                    RequireNoConflict(target, name); SafePath(context.UserFonts);
                    Directory.CreateDirectory(context.UserFonts); SafePath(context.UserFonts);
                    File.Copy(System.IO.Path.Combine(backup, manifest.BackupFile), target, false); copied = true;
                    Verify(target, manifest);
                    if (!platform.Load(target)) throw new InvalidOperationException("font_load_failed");
                    loaded = true; platform.Register(name, target); registered = true;
                    RequireRegistration(manifest);
                    manifest.Status = "installed"; Save(backup, manifest); platform.Notify();
                    return Result(true, backup, "font_installed_reopen_apps");
                } catch {
                    bool rolledBack = true;
                    if (manifest != null) {
                        if (registered) try { RemoveExactRegistration(manifest); } catch { rolledBack = false; }
                        if (loaded) try { platform.Unload(manifest.OriginalPath); } catch { rolledBack = false; }
                        if (copied) try { RequireUnreferenced(manifest.OriginalPath); Verify(manifest.OriginalPath, manifest); platform.Recycle(manifest.OriginalPath); } catch { rolledBack = false; }
                        RecordStatus(backup, manifest, rolledBack ? "install-rolled-back" : "install-rollback-incomplete");
                    }
                    return Result(false, backup, rolledBack ? "font_install_failed_rolled_back" : "font_install_failed_review_backup");
                }
            }
        }
        public FontResult Remove(FontEntry entry) {
            lock (gate) {
                string backup = null; FontBackupManifest manifest = null;
                bool unregistered = false, unloaded = false;
                try {
                    if (entry == null || !entry.IsUser || !entry.CanRemove || !UserPath(entry.Path)) return Result(false, null, "font_system_or_unmanaged_protected");
                    SafePath(entry.Path); ValidateSize(entry.Path);
                    if (entry.Stamp != MetadataStamp(entry.Path)) return Result(false, null, "font_changed_refresh_list");
                    manifest = ManifestForRemoval(entry);
                    RequireRegistration(manifest);
                    backup = Backup(entry.Path, manifest);
                    RequireRegistration(manifest); Verify(entry.Path, manifest);
                    unloaded = platform.Unload(entry.Path);
                    RemoveExactRegistration(manifest); unregistered = true;
                    RequireUnreferenced(entry.Path);
                    platform.Recycle(entry.Path);
                    if (File.Exists(entry.Path)) throw new IOException("font_recycle_incomplete");
                    manifest.Status = "removed"; Save(backup, manifest); platform.Notify();
                    return Result(true, backup, "font_removed_reopen_apps");
                } catch {
                    bool recovered = true;
                    if (manifest != null && File.Exists(manifest.OriginalPath)) {
                        if (unregistered) try { Verify(manifest.OriginalPath, manifest); platform.Register(manifest.RegistryName, manifest.RegistryData); } catch { recovered = false; }
                        if (unloaded) try { platform.Load(manifest.OriginalPath); } catch { recovered = false; }
                    } else if (unregistered) recovered = false;
                    RecordStatus(backup, manifest, recovered ? "remove-rolled-back" : "remove-review-backup");
                    return Result(false, backup, recovered ? "font_remove_failed_no_loss" : "font_remove_failed_restore_backup");
                }
            }
        }
        public FontResult Restore(string backupDirectory) {
            lock (gate) {
                try {
                    FontBackupManifest manifest = Read(backupDirectory);
                    if (manifest.Operation == "install") return UndoInstall(backupDirectory, manifest);
                    if (File.Exists(manifest.OriginalPath) || Directory.Exists(manifest.OriginalPath)) {
                        // A previous restore may have succeeded before its receipt could be saved.
                        // An exact original file and registration need no file, registry or GDI writes.
                        if (Directory.Exists(manifest.OriginalPath)) return Result(false, backupDirectory, "font_restore_conflict_file_exists");
                        try { RequireRegistration(manifest); Verify(manifest.OriginalPath, manifest); }
                        catch { return Result(false, backupDirectory, "font_restore_conflict_file_exists"); }
                        try {
                            manifest.Status = "restored"; Save(backupDirectory, manifest);
                            return Result(true, backupDirectory, "font_restore_already_matches");
                        } catch { return Result(false, backupDirectory, "font_restore_matches_receipt_unwritten"); }
                    }
                    RequireNoConflict(manifest.OriginalPath, manifest.RegistryName);
                    Verify(System.IO.Path.Combine(backupDirectory, manifest.BackupFile), manifest);
                    bool copied = false, loaded = false, registered = false;
                    try {
                        SafePath(context.UserFonts); Directory.CreateDirectory(context.UserFonts); SafePath(context.UserFonts);
                        File.Copy(System.IO.Path.Combine(backupDirectory, manifest.BackupFile), manifest.OriginalPath, false); copied = true;
                        Verify(manifest.OriginalPath, manifest);
                        if (!platform.Load(manifest.OriginalPath)) throw new InvalidOperationException("font_load_failed");
                        loaded = true; platform.Register(manifest.RegistryName, manifest.RegistryData); registered = true;
                        RequireRegistration(manifest);
                        manifest.Status = "restored"; Save(backupDirectory, manifest); platform.Notify();
                        return Result(true, backupDirectory, "font_restored_reopen_apps");
                    } catch {
                        bool rollback = true;
                        if (registered) try { RemoveExactRegistration(manifest); } catch { rollback = false; }
                        if (loaded) try { platform.Unload(manifest.OriginalPath); } catch { rollback = false; }
                        if (copied) try { RequireUnreferenced(manifest.OriginalPath); Verify(manifest.OriginalPath, manifest); platform.Recycle(manifest.OriginalPath); } catch { rollback = false; }
                        RecordStatus(backupDirectory, manifest, rollback ? "restore-rolled-back" : "restore-rollback-incomplete");
                        return Result(false, backupDirectory, rollback ? "font_restore_failed_backup_preserved" : "font_restore_failed_review_backup");
                    }
                } catch { return Result(false, backupDirectory, "font_backup_invalid_or_conflict"); }
            }
        }
        private FontResult UndoInstall(string backup, FontBackupManifest manifest) {
            if (manifest.Status != "installed") return Result(false, backup, "font_install_not_active");
            RequireRegistration(manifest); Verify(manifest.OriginalPath, manifest);
            Verify(System.IO.Path.Combine(backup, manifest.BackupFile), manifest);
            bool removed = false, unloaded = false;
            try {
                unloaded = platform.Unload(manifest.OriginalPath);
                RemoveExactRegistration(manifest); removed = true;
                RequireUnreferenced(manifest.OriginalPath);
                platform.Recycle(manifest.OriginalPath);
                if (File.Exists(manifest.OriginalPath)) throw new IOException("font_recycle_incomplete");
                manifest.Status = "installation-undone"; Save(backup, manifest); platform.Notify();
                return Result(true, backup, "font_installation_undone");
            } catch {
                bool recovered = File.Exists(manifest.OriginalPath);
                if (recovered) {
                    if (removed) try { Verify(manifest.OriginalPath, manifest); platform.Register(manifest.RegistryName, manifest.RegistryData); } catch { recovered = false; }
                    if (unloaded) try { platform.Load(manifest.OriginalPath); } catch { recovered = false; }
                }
                RecordStatus(backup, manifest, recovered ? "installed" : "undo-review-backup");
                return Result(false, backup, recovered ? "font_undo_failed_no_loss" : "font_undo_failed_review_backup");
            }
        }
        public List<string> ListBackups() {
            lock (gate) {
                var result = new List<string>();
                try {
                    SafePath(context.BackupRoot);
                    if (!Directory.Exists(context.BackupRoot)) return result;
                    foreach (var directory in Directory.EnumerateDirectories(context.BackupRoot, "*", SearchOption.TopDirectoryOnly).Take(1000)) {
                        try { Read(directory); result.Add(directory); } catch { }
                    }
                } catch { }
                return result.OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
        private FontBackupManifest ManifestForRemoval(FontEntry entry) {
            if (!String.Equals(entry.RegistryData, entry.Path, StringComparison.OrdinalIgnoreCase) || String.IsNullOrEmpty(entry.RegistryName)) throw new InvalidOperationException("font_selection_invalid");
            return new FontBackupManifest { Operation = "remove", CreatedUtc = DateTime.UtcNow.ToString("o"), OriginalPath = Full(entry.Path), RegistryName = entry.RegistryName, RegistryData = entry.RegistryData, Sha256 = Hash(entry.Path), Length = new FileInfo(entry.Path).Length, BackupFile = "font" + System.IO.Path.GetExtension(entry.Path).ToLowerInvariant(), Status = "prepared" };
        }
        private string Backup(string source, FontBackupManifest manifest) {
            SafePath(context.BackupRoot);
            string backup = System.IO.Path.Combine(context.BackupRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backup); SafePath(backup); Protect(backup);
            File.Copy(source, System.IO.Path.Combine(backup, manifest.BackupFile), false);
            Verify(source, manifest); Verify(System.IO.Path.Combine(backup, manifest.BackupFile), manifest);
            Save(backup, manifest); return backup;
        }
        private void RequireNoConflict(string path, string name) {
            if (!UserPath(path)) throw new InvalidOperationException("font_path_protected");
            SafePath(path);
            if (File.Exists(path) || Directory.Exists(path)) throw new InvalidOperationException("font_file_conflict");
            if (platform.Read(true).Any(x => String.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) || SamePath(x.Data, path)) || platform.Read(false).Any(x => String.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("font_registry_conflict");
        }
        private void RequireRegistration(FontBackupManifest manifest) {
            if (!UserPath(manifest.OriginalPath)) throw new InvalidOperationException("font_path_protected");
            SafePath(manifest.OriginalPath);
            var rows = platform.Read(true);
            var row = rows.SingleOrDefault(x => String.Equals(x.Name, manifest.RegistryName, StringComparison.OrdinalIgnoreCase));
            if (row == null || row.Kind != RegistryValueKind.String || !String.Equals(row.Data, manifest.RegistryData, StringComparison.Ordinal) || !SamePath(row.Data, manifest.OriginalPath) || !UniqueReference(rows, manifest.OriginalPath) || platform.Read(false).Any(x => SamePath(x.Data, manifest.OriginalPath))) throw new InvalidOperationException("font_registry_changed");
        }
        private void RequireUnreferenced(string path) { if (platform.Read(true).Concat(platform.Read(false)).Any(x => SamePath(x.Data, path))) throw new InvalidOperationException("font_file_still_referenced"); }
        private void RemoveExactRegistration(FontBackupManifest manifest) { RequireRegistration(manifest); platform.Unregister(manifest.RegistryName); }
        private static bool UniqueReference(List<FontRegistration> rows, string path) { return rows.Count(x => SamePath(x.Data, path)) == 1; }
        private FontBackupManifest Read(string backup) {
            RequireAbsolute(backup);
            if (!String.Equals(System.IO.Path.GetDirectoryName(Full(backup)), Full(context.BackupRoot), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("font_backup_outside_root");
            SafePath(backup); string file = System.IO.Path.Combine(backup, "font.json"); SafePath(file);
            if (!File.Exists(file) || new FileInfo(file).Length > 65536) throw new InvalidOperationException("font_manifest_invalid");
            var m = json.Deserialize<FontBackupManifest>(File.ReadAllText(file, Encoding.UTF8));
            if (m == null || m.Version != 1 || (m.Operation != "install" && m.Operation != "remove") || !UserPath(m.OriginalPath) || !SamePath(m.OriginalPath, m.RegistryData) || String.IsNullOrWhiteSpace(m.RegistryName) || m.RegistryName.Length > 512 || m.RegistryName.Any(Char.IsControl) || m.Sha256 == null || !Regex.IsMatch(m.Sha256, "^[a-f0-9]{64}$") || m.Length < 1 || m.Length > MaxFontBytes || m.BackupFile != "font" + System.IO.Path.GetExtension(m.OriginalPath).ToLowerInvariant()) throw new InvalidOperationException("font_manifest_invalid");
            if (m.Operation == "install" && !String.Equals(System.IO.Path.GetFileName(m.OriginalPath), "Dafeiyu-" + m.Sha256.Substring(0, 24) + System.IO.Path.GetExtension(m.OriginalPath), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("font_install_manifest_invalid");
            SafePath(m.OriginalPath); SafePath(System.IO.Path.Combine(backup, m.BackupFile));
            Verify(System.IO.Path.Combine(backup, m.BackupFile), m);
            return m;
        }
        private void Save(string backup, FontBackupManifest manifest) {
            SafePath(backup);
            string target = System.IO.Path.Combine(backup, "font.json"), pending = System.IO.Path.Combine(backup, "font.pending-" + Guid.NewGuid().ToString("N"));
            SafePath(target); File.WriteAllText(pending, json.Serialize(manifest), new UTF8Encoding(false));
            if (File.Exists(target)) File.Replace(pending, target, null); else File.Move(pending, target);
        }
        private void RecordStatus(string backup, FontBackupManifest manifest, string status) { if (backup == null || manifest == null) return; try { manifest.Status = status; Save(backup, manifest); } catch { } }
        private bool UserPath(string path) {
            try {
                RequireAbsolute(path);
                string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
                return (extension == ".ttf" || extension == ".otf") && String.Equals(System.IO.Path.GetDirectoryName(Full(path)), Full(context.UserFonts), StringComparison.OrdinalIgnoreCase);
            } catch { return false; }
        }
        private static bool SamePath(string left, string right) { try { RequireAbsolute(left); RequireAbsolute(right); return String.Equals(Full(left), Full(right), StringComparison.OrdinalIgnoreCase); } catch { return false; } }
        private static string Full(string path) { return System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar); }
        private static void RequireAbsolute(string path) { if (String.IsNullOrWhiteSpace(path) || !Regex.IsMatch(path, @"^[A-Za-z]:[\\/]") || path.Substring(2).Contains(":")) throw new InvalidOperationException("font_path_invalid"); }
        private static void SafePath(string path) {
            RequireAbsolute(path); string current = Full(path);
            while (!String.IsNullOrEmpty(current)) {
                try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("font_reparse_protected"); }
                catch (FileNotFoundException) { } catch (DirectoryNotFoundException) { }
                current = System.IO.Path.GetDirectoryName(current);
            }
        }
        private static void ValidateSize(string path) { var info = new FileInfo(path); if (!info.Exists || info.Length < 1 || info.Length > MaxFontBytes) throw new InvalidOperationException("font_invalid_size"); }
        private static string MetadataStamp(string path) { var info = new FileInfo(path); return info.Length.ToString() + ":" + info.LastWriteTimeUtc.Ticks.ToString() + ":" + Hash(path); }
        private static string Hash(string path) { SafePath(path); using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        private static void Verify(string path, FontBackupManifest manifest) { SafePath(path); ValidateSize(path); if (new FileInfo(path).Length != manifest.Length || Hash(path) != manifest.Sha256) throw new InvalidOperationException("font_hash_mismatch"); }
        private static void Protect(string path) {
            var owner = WindowsIdentity.GetCurrent().User; var acl = new DirectorySecurity();
            acl.SetAccessRuleProtection(true, false); acl.SetOwner(owner);
            acl.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.SetAccessControl(path, acl);
        }
        private static FontResult Result(bool ok, string backup, string message) { return new FontResult { Ok = ok, BackupDirectory = backup, Message = message }; }
    }
}
