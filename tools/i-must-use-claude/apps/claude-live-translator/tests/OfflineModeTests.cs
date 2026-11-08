using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ClaudeCompanion;

internal static class OfflineModeTests {
    private static int checks;
    private static string fixtureRoot;
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void Check(bool condition, string description) {
        if (!condition) throw new Exception(description);
        checks++;
    }
    private static T Field<T>(object instance, string name) {
        return (T)instance.GetType().GetField(name, PrivateInstance).GetValue(instance);
    }
    private static void Put(object instance, string name, object value) {
        instance.GetType().GetField(name, PrivateInstance).SetValue(instance, value);
    }
    private static object Call(object instance, string name, params object[] arguments) {
        return instance.GetType().GetMethod(name, PrivateInstance).Invoke(instance, arguments);
    }
    private static CompanionForm Preview() {
        var form = new CompanionForm(true);
        Field<IDisposable>(form, "watcher").Dispose();
        // A hidden native handle lets Close run the normal cleanup without showing a window.
        IntPtr hiddenHandle = form.Handle;
        Check(!form.Visible, "Preview fixture must remain hidden");
        return form;
    }
    private static Snapshot SeedVisibleContent(CompanionForm form) {
        var snapshot = new Snapshot {
            Conversation = "offline-fixture", MessageId = "1",
            Text = "Fixed source text for local regression tests.", Direction = "auto:zh-CN"
        };
        Put(form, "latest", snapshot);
        Put(form, "seen", snapshot.Key + snapshot.Text);
        Put(form, "done", snapshot.Key + snapshot.Text);
        Field<TextBox>(form, "original").Text = snapshot.Text;
        Field<TextBox>(form, "translated").Text = "保留已有译文。";
        // Hold the translation gate closed: even a regression must not start a backend process.
        Put(form, "translating", true);
        return snapshot;
    }
    private static void ContentRetained(CompanionForm form, Snapshot snapshot, string action) {
        Check(Object.ReferenceEquals(Field<Snapshot>(form, "latest"), snapshot), action + " replaced the existing snapshot");
        Check(Field<TextBox>(form, "original").Text == snapshot.Text, action + " changed the visible original");
        Check(Field<TextBox>(form, "translated").Text == "保留已有译文。", action + " changed the visible translation");
        Check(Field<string>(form, "done") == snapshot.Key + snapshot.Text, action + " scheduled the old text for translation again");
        var worker = Field<Worker>(form, "worker");
        Check(Field<object>(worker, "process") == null && Field<int>(worker, "sequence") == 0,
            action + " started a translation backend or sent a request");
    }
    private static void SelectTarget(ComboBox selector, string code) {
        foreach (LanguageChoice item in selector.Items) {
            if (item.Code == code) { selector.SelectedItem = item; return; }
        }
        throw new Exception("Missing language option: " + code);
    }
    private static void SelectPair(ComboBox selector, string pair) {
        foreach (LanguagePack item in selector.Items) {
            if (item.Pair == pair) { selector.SelectedItem = item; return; }
        }
        throw new Exception("Missing installed language pair: " + pair);
    }
    private static void Package(string from, string to) {
        string id = "translate-" + from + "_" + to + "-1_9";
        string folder = Path.Combine(fixtureRoot, "packages", id);
        Directory.CreateDirectory(Path.Combine(folder, "model"));
        File.WriteAllText(Path.Combine(folder, "metadata.json"), Json.Encode(new {
            type = "translate", from_code = from, to_code = to,
            from_name = from, to_name = to, package_version = "1.9"
        }), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(folder, ".install.json"), Json.Encode(new { id = id, complete = true }), new UTF8Encoding(false));
        // Placeholder bytes satisfy the local metadata scan. They are never loaded as models.
        foreach (string file in new[] { "sentencepiece.model", "model/model.bin", "model/config.json", "model/shared_vocabulary.json" })
            File.WriteAllText(Path.Combine(folder, file.Replace('/', Path.DirectorySeparatorChar)), "fixture only", new UTF8Encoding(false));
    }
    private static void OfflineTargets(ComboBox selector) {
        Check(selector.Enabled && selector.Items.Count == 3, "Offline mode must offer exactly the three installed directions");
        bool chinese = false, english = false, french = false;
        foreach (LanguagePack item in selector.Items) {
            Check(item.Pair == "pair:en:zh" || item.Pair == "pair:zh:en" || item.Pair == "pair:en:fr", "Offline mode offers an uninstalled direction: " + item.Pair);
            chinese |= item.Pair == "pair:en:zh"; english |= item.Pair == "pair:zh:en"; french |= item.Pair == "pair:en:fr";
        }
        Check(chinese && english && french, "Offline mode is missing an installed language direction");
    }
    private static void CheckDefaultsAndLanguages() {
        Check(!Preferences.Offline, "New installs should default to Google without downloading a language pack");
        Check(Preferences.DataDirectory == fixtureRoot, "The data-directory override must isolate preferences and packages from user data");
        Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR", null);
        try {
            Check(Preferences.DataDirectory == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DafeiyuTranslator"),
                "Default preferences and package storage must stay independent of the executable location");
        } finally { Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR", fixtureRoot); }
        string settingsFile = (string)typeof(Preferences).GetProperty("FilePath", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null);
        Check(settingsFile == Path.Combine(Preferences.DataDirectory, "preferences.json"), "Moving or extracting the application would change its preference-file location");
        Preferences.Target = "fr";
        using (var fresh = Preview()) {
            Check(Field<ComboBox>(fresh, "engine").SelectedIndex == 1, "A fresh user must start with Google selected");
            Check(Field<ComboBox>(fresh, "direction").Items.Count == LanguageChoice.All.Length, "A fresh user must retain the full online target catalog");
            fresh.Close();
        }
        Preferences.Offline = true;
        foreach (bool english in new bool[] { false, true }) {
            Ui.SetEnglish(english);
            using (var empty = Preview()) {
                var targets = Field<ComboBox>(empty, "direction");
                Check(!targets.Enabled && targets.Items.Count == 1 && !(targets.SelectedItem is LanguagePack), "An empty installation must not imply bundled offline models");
                Check(Field<Label>(empty, "googleStatus").Text.Contains(english ? "download while online" : "联网时按需下载"), "Missing packs must explain explicit installation");
                Check(Field<object>(Field<Worker>(empty, "worker"), "process") == null, "Missing packages must not start a backend or download");
                empty.Close();
            }
        }
        Package("en", "zh"); Package("zh", "en"); Package("en", "fr");
        Preferences.OfflinePair = "pair:en:ja";
        foreach (bool english in new bool[] { false, true }) {
            Ui.SetEnglish(english);
            using (var form = Preview()) {
                OfflineTargets(Field<ComboBox>(form, "direction"));
                Check(Preferences.Target == "fr", "Offline direction selection must preserve the saved Google target");
                Check(((LanguagePack)Field<ComboBox>(form, "direction").SelectedItem).Pair == Preferences.OfflinePair, "Unavailable saved offline direction must fall back to an installed pair");
                Check(Field<ComboBox>(form, "engine").SelectedIndex == 0, "Offline preference did not initialize the engine selector");
                Check(Field<Label>(form, "privacy").Text.Contains(english ? "No upload" : "不上传"), "Offline privacy label is missing or in the wrong language");
                Check(Field<ToolTip>(form, "tips").GetToolTip(Field<Label>(form, "privacy")).Contains(english ? "not uploaded" : "不上传文字"), "Detailed offline disclosure must remain available beside the compact caption");
                Check(Field<Label>(form, "subtitle").Text.Contains(english ? "Offline" : "离线"), "Offline subtitle is misleading");
                Check(Field<Button>(form, "checkConnection").Text == (english ? "Test offline" : "检测离线库"), "Offline test button still describes Google");
                Check(Field<Label>(form, "googleStatus").Text.Contains(english ? "Translation needs no network" : "翻译无需联网"), "Installed offline engine status still implies a network service");
                form.Close();
            }
        }
    }
    private static void CheckModeSwitch(bool english) {
        Preferences.Offline = true; Preferences.Target = "zh-CN"; Preferences.OfflinePair = "pair:en:zh"; Ui.SetEnglish(english);
        using (var form = Preview()) {
            var snapshot = SeedVisibleContent(form);
            var engine = Field<ComboBox>(form, "engine");
            var direction = Field<ComboBox>(form, "direction");
            int priorEpoch = Field<int>(form, "epoch");
            int priorSelection = Field<int>(form, "selectionEpoch");
            using (var pendingCapture = new CancellationTokenSource()) {
                Put(form, "captureCancellation", pendingCapture);
                engine.SelectedIndex = 1;
                Check(pendingCapture.IsCancellationRequested && Field<object>(form, "captureCancellation") == null, "Changing engine did not cancel a pending capture");
            }
            Check(!Preferences.Offline, "Choosing Google did not change the mode preference");
            Check(Field<int>(form, "epoch") > priorEpoch && Field<int>(form, "selectionEpoch") > priorSelection, "Changing engine did not invalidate pending results and captures");
            Check(Field<bool>(form, "needsFreshSelection"), "Google mode may reuse text captured while offline");
            Check(direction.Items.Count == LanguageChoice.All.Length, "Online mode did not restore the full target-language catalog");
            Check(Field<Label>(form, "privacy").Text.Contains(english ? "Selection → Google" : "发送给 Google"), "Online privacy label does not disclose text upload");
            Check(Field<ToolTip>(form, "tips").GetToolTip(Field<Label>(form, "privacy")).Contains(english ? "sent to Google" : "发送给 Google"), "Detailed online disclosure must explain the compact Google caption");
            Check(Field<Label>(form, "googleStatus").Text.Contains(english ? "next translation sends text to Google" : "下次翻译会发送文字给 Google"), "Engine change does not explain the next upload");
            Check(Field<Label>(form, "status").Text.Contains(english ? "Select new text" : "重新划选"), "Engine change does not request fresh input");
            ContentRetained(form, snapshot, "Switching to Google");

            SelectTarget(direction, "fr");
            Check(Preferences.Target == "fr" && Field<bool>(form, "needsFreshSelection"), "Changing target bypassed the fresh-input guard");
            ContentRetained(form, snapshot, "Changing target after engine switch");
            Ui.SetEnglish(!english);
            ContentRetained(form, snapshot, "Switching interface language");
            Check(Field<bool>(form, "needsFreshSelection"), "Language change cleared the fresh-input guard");
            Ui.SetEnglish(english);

            engine.SelectedIndex = 0;
            Check(Preferences.Offline, "Choosing offline did not restore local mode");
            OfflineTargets(direction);
            Check(Preferences.Target == "fr" && Preferences.OfflinePair == "pair:en:zh", "Returning offline must restore its saved pair and preserve the separate online target");
            Check(Field<bool>(form, "needsFreshSelection"), "Returning offline cleared the fresh-input guard");
            ContentRetained(form, snapshot, "Returning to offline mode");
            SelectPair(direction, "pair:zh:en");
            Check(Preferences.OfflinePair == "pair:zh:en" && Preferences.Target == "fr", "Changing offline pair must not change the saved online target");
            ContentRetained(form, snapshot, "Changing offline target before fresh input");

            engine.SelectedIndex = 1;
            Check(!Preferences.Offline && Field<bool>(form, "needsFreshSelection"), "Switching back to Google bypassed the fresh-input guard");
            ContentRetained(form, snapshot, "Switching back to Google without new text");
            Call(form, "AcceptText", "New explicitly supplied fixture text.", "manual-fixture");
            Check(!Field<bool>(form, "needsFreshSelection"), "Fresh explicit input did not release the mode-switch guard");
            Check(Field<Snapshot>(form, "latest").Text == "New explicitly supplied fixture text.", "Fresh input did not replace the old snapshot");
            Check(Field<object>(Field<Worker>(form, "worker"), "process") == null, "A fixture started the backend despite its closed translation gate");
            Check(!form.Visible, "Mode-switch fixture unexpectedly showed a window");
            form.Close();
        }
    }
    private static void CheckComposeModeSnapshot() {
        foreach (bool offline in new bool[] { true, false }) {
            Preferences.Offline = offline; Ui.SetEnglish(false);
            using (var form = new ComposeForm(new WindowChoice { Handle = "0", Title = "Fixed fixture" }, true)) {
                IntPtr hiddenHandle = form.Handle;
                Check(Field<bool>(form, "offline") == offline, "Compose did not capture its initial translation mode");
                Field<TextBox>(form, "chinese").Text = "保留原文";
                Field<TextBox>(form, "english").Text = "Keep the original text.";
                Preferences.Offline = !offline;
                foreach (bool english in new bool[] { false, true }) {
                    Ui.SetEnglish(english);
                    Check(Field<bool>(form, "offline") == offline, "Compose mode changed with the main-window preference");
                    Check(Field<Label>(form, "subtitleLabel").Text.Contains(offline ? (english ? "Offline" : "离线") : "Google"), "Compose subtitle does not reflect its fixed translation mode");
                    Check(Field<TextBox>(form, "chinese").Text == "保留原文" && Field<TextBox>(form, "english").Text == "Keep the original text.", "Compose language change altered draft text");
                    Put(form, "waitStartedAt", DateTime.UtcNow.AddSeconds(-25));
                    Call(form, "RefreshWaitStatus");
                    string status = Field<Label>(form, "status").Text;
                    Check(offline ? !status.Contains("Google") && !status.Contains("网络") && status.Contains(english ? "locally" : "本机") : status.Contains("Google"), "Compose wait status describes the wrong translation mode");
                }
                Check(Field<object>(Field<Worker>(form, "worker"), "process") == null, "Compose fixture unexpectedly started translation");
                Check(!form.Visible && !Field<Button>(form, "send").Enabled, "Compose fixture showed a window or enabled sending without a conversation");
                form.Close();
            }
        }
    }
    private static void CheckOfflineErrorLabels() {
        string[,] labels = {
            { "offline_language", "请选择已安装的离线翻译方向。", "Choose an installed offline language pair." },
            { "unsupported_language", "请选择已安装的离线翻译方向。", "Choose an installed offline language pair." },
            { "offline_unsupported_target", "请选择已安装的离线翻译方向。", "Choose an installed offline language pair." },
            { "offline_model_missing", "尚未安装这个方向的语言包，请点「离线语言包」。", "This language pair is not installed. Open Offline packs." },
            { "model_unavailable", "语言包无法加载，请在「离线语言包」中重新安装。", "Cannot load this language pack. Reinstall it in Offline packs." },
            { "engine_load_failed", "语言包无法加载，请在「离线语言包」中重新安装。", "Cannot load this language pack. Reinstall it in Offline packs." },
            { "offline_model_load_failed", "语言包无法加载，请在「离线语言包」中重新安装。", "Cannot load this language pack. Reinstall it in Offline packs." },
            { "offline_runtime_unavailable", "离线运行组件缺失，请重新打开单文件版。", "Offline runtime is missing. Reopen the single-file app." },
            { "offline_output_limit", "本地翻译等待超时，请缩短文字后重试。", "Offline translation timed out. Try a shorter selection." },
            { "offline_deadline", "本地翻译等待超时，请缩短文字后重试。", "Offline translation timed out. Try a shorter selection." },
            { "translation_failed", "本地翻译未完成，请缩短文字后重试。", "Offline translation failed. Try a shorter selection." }
        };
        foreach (bool english in new bool[] { false, true }) {
            Ui.SetEnglish(english);
            for (int row = 0; row < labels.GetLength(0); row++) {
                string code = labels[row, 0];
                string message = Worker.ErrorMessage(code);
                Check(!message.Contains("Google") && !message.Contains("网络") && !message.Contains("network"), "Offline error incorrectly asks for network access: " + code);
                Check(message == labels[row, english ? 2 : 1], "Offline error must explain the applicable recovery in the selected interface language: " + code);
            }
        }
    }
    [STAThread]
    private static int Main() {
        bool originalOffline = Preferences.Offline, originalEnglish = Ui.English;
        string originalTarget = Preferences.Target, originalPair = Preferences.OfflinePair;
        string originalData = Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR");
        fixtureRoot = Path.Combine(Path.GetTempPath(), "dfy-offline-mode-tests-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR", fixtureRoot);
        string preferencesPath = Path.Combine(Preferences.DataDirectory, "preferences.json");
        Directory.CreateDirectory(fixtureRoot);
        File.WriteAllText(preferencesPath, "{\"fixture\":true,\"offline\":false,\"target\":\"fr\"}", new UTF8Encoding(false));
        byte[] settingsBefore = File.Exists(preferencesPath) ? File.ReadAllBytes(preferencesPath) : null;
        DateTime? settingsTimestamp = File.Exists(preferencesPath) ? (DateTime?)File.GetLastWriteTimeUtc(preferencesPath) : null;
        try {
            Check(!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backend", "launcher.json")), "Run tests in an isolated directory without a backend so a regression cannot start network translation");
            Application.EnableVisualStyles();
            CheckDefaultsAndLanguages();
            CheckModeSwitch(false); CheckModeSwitch(true);
            CheckComposeModeSnapshot(); CheckOfflineErrorLabels();
            byte[] settingsAfter = File.Exists(preferencesPath) ? File.ReadAllBytes(preferencesPath) : null;
            Check(settingsBefore == null ? settingsAfter == null : settingsAfter != null && Convert.ToBase64String(settingsBefore) == Convert.ToBase64String(settingsAfter), "Preview-mode tests changed user preferences on disk");
            Check(settingsTimestamp == null ? !File.Exists(preferencesPath) : File.GetLastWriteTimeUtc(preferencesPath) == settingsTimestamp.Value, "Preview-mode tests rewrote the user preference file");
            Console.WriteLine("OfflineModeTests passed: " + checks + " checks. Hidden fixtures and fixed text only; no network, clipboard, visible UI, translation backend, or preference writes.");
            return 0;
        } catch (Exception error) {
            Console.WriteLine("OfflineModeTests failed: " + error.GetBaseException().Message);
            Console.WriteLine(error.GetBaseException().StackTrace);
            return 1;
        } finally {
            Preferences.Offline = originalOffline; Preferences.Target = originalTarget; Preferences.OfflinePair = originalPair; Ui.English = originalEnglish;
            Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR", originalData);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string resolvedFixture = Path.GetFullPath(fixtureRoot);
            if (resolvedFixture.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolvedFixture).StartsWith("dfy-offline-mode-tests-", StringComparison.Ordinal) && Directory.Exists(resolvedFixture))
                Directory.Delete(resolvedFixture, true);
        }
    }
}
