using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace WhalePack {
    public static class SettingsStore {
        public static string FilePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WhalePack", "settings.json"); } }
        public static AppSettings Load() { return LoadFrom(FilePath); }
        public static AppSettings LoadFrom(string path) {
            path = LocalPath.Require(path);
            AppSettings result = null;
            try { if (File.Exists(path) && new FileInfo(path).Length <= 2000000) result = new JavaScriptSerializer().Deserialize<AppSettings>(File.ReadAllText(path, Encoding.UTF8)); }
            catch (IOException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { } catch (InvalidOperationException) { }
            if (result == null) result = new AppSettings();
            if (result.Handoff == null) result.Handoff = new HandoffData();
            if (String.IsNullOrWhiteSpace(result.OutputDirectory)) result.OutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "发给Claude");
            if (result.Language != "en") result.Language = "zh";
            return result;
        }
        public static void Save(AppSettings settings) { SaveTo(settings, FilePath); }
        public static void SaveTo(AppSettings settings, string path) {
            path = LocalPath.Require(path);
            string text = new JavaScriptSerializer().Serialize(settings);
            if (Encoding.UTF8.GetByteCount(text) > 2000000) throw new InvalidOperationException("交接内容过长 / Handoff is too long.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllText(temp, text, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
