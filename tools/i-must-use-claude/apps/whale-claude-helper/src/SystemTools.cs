using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace WhaleClaudeHelper {
    internal sealed class SystemChangeResult {
        public bool Ok;
        public string BackupDirectory = "";
        public string Message = "";
        public string MessageEnglish = "";
    }

    internal interface ITimeZonePlatform {
        List<TimeZoneInfo> GetTimeZones();
        string ReadSetting();
        void WriteSetting(string setting);
    }

    internal sealed class WindowsTimeZonePlatform : ITimeZonePlatform {
        public List<TimeZoneInfo> GetTimeZones() { return TimeZoneInfo.GetSystemTimeZones().ToList(); }
        public string ReadSetting() {
            string result = Run("/g").Trim();
            // tzutil /g may omit the disabled-DST flag; preserve it separately.
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\TimeZoneInformation", false)) {
                if (key != null && Convert.ToInt32(key.GetValue("DynamicDaylightTimeDisabled", 0)) != 0 && !result.EndsWith("_dstoff", StringComparison.OrdinalIgnoreCase)) result += "_dstoff";
            }
            return result;
        }
        public void WriteSetting(string setting) {
            string basic = TimeZoneChangeService.BaseId(setting);
            if (!GetTimeZones().Any(x => String.Equals(x.Id, basic, StringComparison.Ordinal)) || setting.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0) throw new InvalidDataException("invalid_timezone");
            Run("/s \"" + setting + "\"");
            TimeZoneInfo.ClearCachedData();
        }
        private static string Run(string arguments) {
            string utility = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tzutil.exe");
            if (!File.Exists(utility)) throw new FileNotFoundException("tzutil_unavailable");
            var start = new ProcessStartInfo(utility, arguments) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = new Process { StartInfo = start }) {
                process.Start();
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(8000)) {
                    try { process.Kill(); process.WaitForExit(1000); } catch { }
                    throw new TimeoutException("timezone_timeout");
                }
                if (process.ExitCode != 0) throw new InvalidOperationException("timezone_permission_or_policy");
                if (!output.Wait(1000) || !error.Wait(1000)) throw new TimeoutException("timezone_timeout");
                return output.Result;
            }
        }
    }

    internal sealed class TimeZoneReceipt {
        public int Format = 1;
        public string Kind = "whale-timezone";
        public string Before, After, State, CreatedUtc;
    }

    internal sealed class TimeZoneChangeService {
        private static readonly object Gate = new object();
        private readonly ITimeZonePlatform platform;
        private readonly string backupRoot;
        internal TimeZoneChangeService(ITimeZonePlatform adapter, string backupDirectory) {
            platform = adapter;
            backupRoot = Path.GetFullPath(backupDirectory);
        }
        internal static string BaseId(string setting) {
            if (String.IsNullOrEmpty(setting)) return "";
            return setting.EndsWith("_dstoff", StringComparison.OrdinalIgnoreCase) ? setting.Substring(0, setting.Length - 7) : setting;
        }
        private bool ValidSetting(string setting) {
            return !String.IsNullOrWhiteSpace(setting) && setting.Length <= 256 && setting.IndexOfAny(new[] { '"', '\r', '\n' }) < 0 && platform.GetTimeZones().Any(x => String.Equals(x.Id, BaseId(setting), StringComparison.Ordinal));
        }
        internal SystemChangeResult Apply(string id) {
            lock (Gate) {
                var result = new SystemChangeResult();
                try {
                    if (!ValidSetting(id) || id.EndsWith("_dstoff", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("invalid_timezone");
                    string before = platform.ReadSetting();
                    if (!ValidSetting(before)) throw new InvalidDataException("unrecognized_current_timezone");
                    if (String.Equals(before, id, StringComparison.Ordinal)) { result.Ok = true; result.Message = "当前已是所选时区，无需修改。"; result.MessageEnglish = "The selected time zone is already active. No change was needed."; return result; }
                    AssertPlainPath(backupRoot);
                    Directory.CreateDirectory(backupRoot);
                    PrivateDirectory(backupRoot);
                    string folder = Path.Combine(backupRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(folder);
                    PrivateDirectory(folder);
                    result.BackupDirectory = folder;
                    var receipt = new TimeZoneReceipt { Before = before, After = id, State = "prepared", CreatedUtc = DateTime.UtcNow.ToString("o") };
                    SaveReceipt(folder, receipt);
                    var saved = ReadReceipt(folder);
                    if (saved.Before != before || saved.After != id) throw new IOException("backup_verification_failed");
                    if (!String.Equals(platform.ReadSetting(), before, StringComparison.Ordinal)) throw new InvalidOperationException("timezone_changed_rescan");
                    platform.WriteSetting(id);
                    if (!String.Equals(platform.ReadSetting(), id, StringComparison.Ordinal)) throw new IOException("timezone_verification_failed");
                    receipt.State = "applied";
                    SaveReceipt(folder, receipt);
                    result.Ok = true;
                    result.Message = "系统时区已修改并核验；原设置已备份，可恢复。";
                    result.MessageEnglish = "The Windows time zone was changed and verified. The original setting was backed up and can be restored.";
                } catch (Exception error) { result.Message = Describe(error); result.MessageEnglish = DescribeEnglish(error); }
                return result;
            }
        }
        internal SystemChangeResult Restore(string directory) {
            lock (Gate) {
                var result = new SystemChangeResult();
                try {
                    string folder = Path.GetFullPath(directory ?? "");
                    string prefix = backupRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (!folder.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !String.Equals(Path.GetDirectoryName(folder), backupRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("invalid_backup");
                    AssertPlainPath(folder);
                    TimeZoneReceipt receipt = ReadReceipt(folder);
                    result.BackupDirectory = folder;
                    if (receipt.Format != 1 || receipt.Kind != "whale-timezone" || !ValidSetting(receipt.Before) || !ValidSetting(receipt.After) || (receipt.State != "prepared" && receipt.State != "applied" && receipt.State != "restored")) throw new InvalidDataException("invalid_backup");
                    string current = platform.ReadSetting();
                    if (String.Equals(current, receipt.Before, StringComparison.Ordinal)) {
                        // Prepared transactions can exist when applying lacked permission or failed before writing.
                        // Treat an unchanged original as a successful no-op, without issuing tzutil /s.
                        if (receipt.State != "restored") { receipt.State = "restored"; SaveReceipt(folder, receipt); }
                        result.Ok = true;
                        result.Message = "当前已经是原时区，无需重复修改。";
                        result.MessageEnglish = "The current time zone already matches the original. No system change was needed.";
                        return result;
                    }
                    if (!String.Equals(current, receipt.After, StringComparison.Ordinal)) throw new InvalidOperationException("timezone_restore_conflict");
                    platform.WriteSetting(receipt.Before);
                    if (!String.Equals(platform.ReadSetting(), receipt.Before, StringComparison.Ordinal)) throw new IOException("timezone_verification_failed");
                    receipt.State = "restored";
                    SaveReceipt(folder, receipt);
                    result.Ok = true;
                    result.Message = "原系统时区及夏令时设置已恢复并核验。";
                    result.MessageEnglish = "The original Windows time zone and daylight saving setting were restored and verified.";
                } catch (Exception error) { result.Message = Describe(error); result.MessageEnglish = DescribeEnglish(error); }
                return result;
            }
        }
        internal List<string> ListBackups() {
            var result = new List<string>();
            if (!Directory.Exists(backupRoot)) return result;
            AssertPlainPath(backupRoot);
            foreach (string folder in Directory.GetDirectories(backupRoot)) {
                try {
                    AssertPlainPath(folder);
                    TimeZoneReceipt receipt = ReadReceipt(folder);
                    if (receipt.Format == 1 && receipt.Kind == "whale-timezone" && ValidSetting(receipt.Before) && ValidSetting(receipt.After) && (receipt.State == "prepared" || receipt.State == "applied" || receipt.State == "restored")) result.Add(folder);
                } catch { }
            }
            return result.OrderByDescending(x => Path.GetFileName(x), StringComparer.Ordinal).ToList();
        }
        private static void AssertPlainPath(string path) {
            string current = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current)) {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("linked_path_not_allowed");
                current = Path.GetDirectoryName(current);
            }
        }
        private static void PrivateDirectory(string path) {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            SecurityIdentifier user = WindowsIdentity.GetCurrent().User;
            security.SetOwner(user);
            security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.SetAccessControl(path, security);
        }
        private static void SaveReceipt(string folder, TimeZoneReceipt receipt) {
            string path = Path.Combine(folder, "timezone.json");
            AssertPlainPath(path);
            string json = new JavaScriptSerializer().Serialize(receipt);
            string temporary = Path.Combine(folder, "receipt-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                byte[] data = new UTF8Encoding(false).GetBytes(json);
                file.Write(data, 0, data.Length);
                file.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        private static TimeZoneReceipt ReadReceipt(string folder) {
            string path = Path.Combine(folder, "timezone.json");
            AssertPlainPath(path);
            if (!File.Exists(path) || new FileInfo(path).Length > 8192) throw new InvalidDataException("invalid_backup");
            TimeZoneReceipt receipt = new JavaScriptSerializer().Deserialize<TimeZoneReceipt>(File.ReadAllText(path, new UTF8Encoding(false, true)));
            if (receipt == null) throw new InvalidDataException("invalid_backup");
            return receipt;
        }
        private static string Describe(Exception error) {
            switch (error.Message) {
                case "invalid_timezone": return "请选择列表中的有效 Windows 时区。";
                case "timezone_changed_rescan": return "系统时区刚被其他程序更改，未执行修改；请刷新后重试。";
                case "timezone_restore_conflict": return "系统时区已被另行更改，已停止恢复，避免覆盖您的新设置。";
                case "timezone_permission_or_policy": return "Windows 未允许修改时区，请检查账户权限或组织策略。";
                case "timezone_verification_failed": return "未能核验时区变更；请刷新当前状态。原设置备份已保留。";
                case "timezone_timeout": return "Windows 时区操作超时，请刷新当前状态后重试。";
                case "unrecognized_current_timezone": return "无法识别当前系统时区，未执行修改。";
                case "linked_path_not_allowed": return "备份路径包含链接，已停止操作。";
                case "invalid_backup": return "这不是有效的时区备份，未执行恢复。";
                default: return "时区操作未完成（" + error.GetType().Name + "）；请检查权限及备份目录。";
            }
        }
        private static string DescribeEnglish(Exception error) {
            switch (error.Message) {
                case "invalid_timezone": return "Select a valid Windows time zone from the list.";
                case "timezone_changed_rescan": return "Another application just changed the time zone. Nothing was applied; refresh and try again.";
                case "timezone_restore_conflict": return "The time zone has changed since this backup. Restoration stopped to preserve your newer setting.";
                case "timezone_permission_or_policy": return "Windows did not allow the time zone change. Check your account permissions or organization policy.";
                case "timezone_verification_failed": return "The time zone change could not be verified. Refresh the current state. The original setting backup was retained.";
                case "timezone_timeout": return "The Windows time zone operation timed out. Refresh the current state before trying again.";
                case "unrecognized_current_timezone": return "The current Windows time zone could not be recognized. Nothing was changed.";
                case "linked_path_not_allowed": return "The backup path contains a filesystem link. The operation was stopped.";
                case "invalid_backup": return "This is not a valid time zone backup. Nothing was restored.";
                default: return "The time zone operation did not complete (" + error.GetType().Name + "). Check permissions and the backup folder.";
            }
        }
    }

    internal static class SystemTools {
        private static readonly string BackupRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DafeiyuClaudeHelper", "Backups", "Timezone");
        public static List<TimeZoneInfo> ListTimeZones() { return new WindowsTimeZonePlatform().GetTimeZones(); }
        public static string CurrentTimeZoneId() { return TimeZoneChangeService.BaseId(new WindowsTimeZonePlatform().ReadSetting()); }
        public static SystemChangeResult ApplyTimeZone(string validWindowsId) { return new TimeZoneChangeService(new WindowsTimeZonePlatform(), BackupRoot).Apply(validWindowsId); }
        public static SystemChangeResult RestoreTimeZone(string backupDirectory) { return new TimeZoneChangeService(new WindowsTimeZonePlatform(), BackupRoot).Restore(backupDirectory); }
        public static List<string> ListTimeZoneBackups() { return new TimeZoneChangeService(new WindowsTimeZonePlatform(), BackupRoot).ListBackups(); }
    }
}
