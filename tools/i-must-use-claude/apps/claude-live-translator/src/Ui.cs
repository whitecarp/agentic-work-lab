using System;
using System.IO;
using System.Text;

namespace ClaudeCompanion {
    internal static class Ui {
        public static bool English;
        public static void ApplyIcon(System.Windows.Forms.Form form) { WindowIcon.Apply(form); }
        public static event Action LanguageChanged;
        public static string T(string zh, string en) { return English ? en : zh; }
        public static void SetEnglish(bool value) {
            English = value;
            var handler = LanguageChanged;
            if (handler != null) handler();
        }
    }
    internal sealed class LanguageChoice {
        public readonly string Code, Name;
        public LanguageChoice(string code, string name) { Code=code; Name=name; }
        public override string ToString() { return Name; }
        public static readonly LanguageChoice[] All = {
            new LanguageChoice("zh-CN", "简体中文"), new LanguageChoice("zh-TW", "繁體中文"),
            new LanguageChoice("en", "English"), new LanguageChoice("ja", "日本語"),
            new LanguageChoice("ko", "한국어"), new LanguageChoice("fr", "Français"),
            new LanguageChoice("de", "Deutsch"), new LanguageChoice("es", "Español"),
            new LanguageChoice("it", "Italiano"), new LanguageChoice("pt", "Português"),
            new LanguageChoice("ru", "Русский"), new LanguageChoice("ar", "العربية"),
            new LanguageChoice("hi", "हिन्दी"), new LanguageChoice("th", "ไทย"),
            new LanguageChoice("vi", "Tiếng Việt"), new LanguageChoice("id", "Bahasa Indonesia"),
            new LanguageChoice("ms", "Bahasa Melayu")
        };
    }
    internal static class Preferences {
        public static string Target = "zh-CN";
        public static bool Pin = true;
        public static bool Offline = false;
        public static string OfflinePair = "pair:en:zh";
        internal static string DataDirectory { get { var custom=Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR");return String.IsNullOrWhiteSpace(custom)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DafeiyuTranslator"):Path.GetFullPath(custom); } }
        private static string FilePath { get { return Path.Combine(DataDirectory,"preferences.json"); } }
        public static void Load() {
            try {
                string path=FilePath;
                if(!File.Exists(path))path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preferences.json");
                var data=Json.Decode(File.ReadAllText(path,Encoding.UTF8));
                Ui.English=Json.Bool(data,"english");
                if(data.ContainsKey("pin")) Pin=Json.Bool(data,"pin");
                if(data.ContainsKey("offline")) Offline=Json.Bool(data,"offline");
                string pair=Json.Str(data,"offlinePair");if(System.Text.RegularExpressions.Regex.IsMatch(pair,"^pair:[a-z]{2,3}:[a-z]{2,3}$"))OfflinePair=pair;
                string code=Json.Str(data,"target");
                foreach(var choice in LanguageChoice.All) if(choice.Code==code) Target=code;
            } catch { }
        }
        public static void Save() {
            try { Directory.CreateDirectory(DataDirectory);File.WriteAllText(FilePath,Json.Encode(new { english=Ui.English,target=Target,pin=Pin,offline=Offline,offlinePair=OfflinePair }),new UTF8Encoding(false)); } catch { }
        }
    }
}
