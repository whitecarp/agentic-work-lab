using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClaudeCompanion;

internal static class MainUiTests {
    private static int checks;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object value, string name) { return (T)value.GetType().GetField(name, Private).GetValue(value); }
    private static void Set(object value, string name, object field) { value.GetType().GetField(name, Private).SetValue(value, field); }
    private static object Call(object value, string name, params object[] args) { return value.GetType().GetMethod(name, Private).Invoke(value, args); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    private static bool LocallyVisible(Control control) { return (bool)typeof(Control).GetMethod("GetState", Private).Invoke(control, new object[] { 2 }); }

    [STAThread] private static int Main() {
        try {
            string fixture = Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR");
            Check(!String.IsNullOrWhiteSpace(fixture), "requires isolated preference directory");
            string preferencePath = Path.Combine(fixture, "preferences.json");
            string before = File.Exists(preferencePath) ? File.ReadAllText(preferencePath) : null;
            Preferences.Pin = false; Preferences.Offline = false; Preferences.Target = "zh-CN"; Ui.English = false;
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new CompanionForm(true)) {
                Field<IDisposable>(form, "watcher").Dispose();
                Check(form.Width <= 560 && form.Height <= 772, "new design must be compact before DPI scaling");
                Check(form.Text.Contains(Program.Version), "main title uses shared version");
                Check(!Field<Button>(form, "copy").Enabled, "instruction text is not presented as a copyable translation");
                Check(!LocallyVisible(Field<Control>(form, "historyTools")), "empty history is hidden");
                Check(!LocallyVisible(Field<Control>(form, "sourceOptions")), "selection controls hidden before choosing window");
                Check(LocallyVisible(Field<Label>(form, "lockInfo")), "source instructions visible before choosing window");
                Check(!LocallyVisible(Field<Button>(form, "clearSource")), "legacy clear button never floats over new layout");
                Check(!Field<Button>(form, "toggle").Enabled, "selection switch disabled without window");
                Set(form, "locked", new SourceWindow { Handle = new IntPtr(123), Title = "Claude · Synthetic document", App = "claude" });
                Set(form, "active", true); Call(form, "RefreshLockLabels");
                Check(LocallyVisible(Field<Control>(form, "sourceOptions")) && !LocallyVisible(Field<Label>(form, "lockInfo")), "selected state swaps instructions for controls");
                Check(Field<SelectionSwitch>(form, "toggle").Selected, "selection switch reflects running state");
                Check(!Field<Label>(form, "sourceName").Text.Contains("\n"), "window label stays on one line");
                Check(!((RoundedButton)Field<Button>(form, "chooseSource")).Primary, "selected source uses secondary change action");
                Set(form, "active", false); Call(form, "RefreshLockLabels");
                Check(!Field<SelectionSwitch>(form, "toggle").Selected, "paused state clears selection switch");
                Check(Field<Label>(form, "sourceName").Text.StartsWith("已选窗口", StringComparison.Ordinal), "paused source label does not claim active translation");
                var history = Field<TranslationHistory>(form, "history");
                history.Add("First source", "First translation", "auto:zh-CN", false);
                history.Add("Second source", "Second translation", "auto:zh-CN", false); Call(form, "RefreshHistoryButtons");
                Check(LocallyVisible(Field<Control>(form, "historyTools")), "nonempty history exposes navigation");
                Check(Field<Label>(form, "historyPosition").Text == "2 / 2", "history position uses compact index");
                Call(form, "NavigateHistory", -1);
                Check(Field<TextBox>(form, "translated").Text == "First translation" && Field<TextBox>(form, "original").Text == "First source", "history still restores matching original and translation");
                Check(Field<Button>(form, "copy").Enabled, "real history result enables copy");
                var model = (SettingsPanelModel)Call(form, "CreateSettingsModel");
                model.SetPinned(true); Check(Preferences.Pin && form.TopMost, "settings pin delegates existing state and window behavior");
                model.SetPinned(false);
                // No translation request: clear the synthetic last result before changing targets.
                Set(form, "latest", null); model.SelectTarget(2);
                Check(Preferences.Target == "en" && Field<Button>(form, "modePill").Text.Contains("English"), "settings target delegates original combo and updates mode capsule");
                model.SetOffline(true); Check(Preferences.Offline && Field<Button>(form, "modePill").Text.Contains("离线"), "settings mode delegates original engine handler");
                Check(Field<Label>(form, "privacy").Text.Contains("不上传"), "offline footer retains local-only disclosure");
                model.SetOffline(false); Check(!Preferences.Offline && Field<Button>(form, "modePill").Text.Contains("Google"), "switching online restores Google label");
                using (var menu = (ContextMenuStrip)Call(form, "BuildModeMenu")) {
                    Check(!menu.Visible && ((ToolStripMenuItem)menu.Items[0]).Checked && !((ToolStripMenuItem)menu.Items[1]).Checked, "mode menu builds hidden with the current mode selected");
                    ((ToolStripMenuItem)menu.Items[1]).PerformClick();
                    Check(Preferences.Offline, "mode menu offline action delegates the original engine flow");
                }
                model.SetOffline(false);
                Call(form, "SetGoogleStatus", "Synthetic detailed failure", true);
                Check(Field<string>(form, "serviceMessage").Contains("Synthetic detailed failure") && Field<bool>(form, "serviceError"), "service details survive compact footer");
                foreach (string name in new[] { "translated", "original" }) Check(Field<TextBox>(form, name).ContextMenuStrip != null && Field<TextBox>(form, name).ContextMenuStrip.Items.Count == 2, "text menus retain Copy and Select all");
                model.SetEnglish(true); Check(form.Text.StartsWith("Whale Translator", StringComparison.Ordinal), "settings language delegates bilingual refresh");
                Check(Field<Button>(form, "settingsButton").AccessibleName == "Settings", "gear is accessible in English");
                Check(Field<Label>(form, "sourceName").Text.StartsWith("Selected window", StringComparison.Ordinal), "selected source label is accurate in English");
                form.Close();
            }
            Check(File.Exists(preferencePath) ? before != null && File.ReadAllText(preferencePath) == before : before == null, "preview controls never persist fixture preferences");
            TestSettingsCallbacks();
            Console.WriteLine("MainUiTests passed: " + checks + " isolated UI checks; no clipboard, input monitoring or network used.");
            return 0;
        } catch (Exception error) { Console.WriteLine("MainUiTests failed: " + error.GetBaseException().Message); Console.WriteLine(error.GetBaseException().StackTrace); return 1; }
    }
    private static void TestSettingsCallbacks() {
        bool offline = false, pinned = false; int selected = 0, targetChanges = 0, modeChanges = 0;
        var model = new SettingsPanelModel {
            Offline = delegate { return offline; }, Pinned = delegate { return pinned; }, TargetEnabled = delegate { return true; },
            Targets = delegate { return new object[] { "简体中文", "English" }; }, SelectedTarget = delegate { return selected; },
            SetOffline = delegate(bool value) { offline = value; modeChanges++; }, SetPinned = delegate(bool value) { pinned = value; },
            SelectTarget = delegate(int index) { selected = index; targetChanges++; }, SetEnglish = delegate(bool value) { },
            ManagePacks = delegate { }, EditGlossary = delegate { }, OpenFavorites = delegate { }, CheckUpdates = delegate { }, CheckConnection = delegate { return Task.FromResult(0); }
        };
        using (var popup = new SettingsPanel(model)) {
            Check(targetChanges == 0 && modeChanges == 0, "creating settings does not mutate preferences");
            Check(popup.ClientSize.Width == 310 && popup.ClientSize.Height == 428, "settings adds one compact saved-replies row");
            Check(!popup.ShowInTaskbar && popup.Icon != null, "settings popup uses app icon without extra taskbar item");
            var targets = Field<ThemedComboBox>(popup, "targets"); targets.SelectedIndex = 1;
            Check(selected == 1 && targetChanges == 1, "target selection emits once despite model refresh");
            popup.RefreshState(); Check(targetChanges == 1, "refreshing settings is read-only");
            Field<RoundedButton>(popup, "offline").PerformClick();
            // PerformClick on an unshown control may not fire; invoke OnClick in the hidden fixture.
            if (modeChanges == 0) typeof(Button).GetMethod("OnClick", Private).Invoke(Field<RoundedButton>(popup, "offline"), new object[] { EventArgs.Empty });
            Check(offline && modeChanges == 1, "offline segmented action delegates exactly once");
            popup.UpdateService("Synthetic error details", true);
            Check(Field<Label>(popup, "connectionState").ForeColor == Color.FromArgb(190, 72, 72), "connection problem has visible warning state");
            Check(Field<ToolTip>(popup, "tips").GetToolTip(Field<Label>(popup, "connectionState")) == "Synthetic error details", "full service error remains available");
            // A modal details dialog temporarily owns focus. Its popup owner must survive.
            IntPtr unusedHandle = popup.Handle; Set(popup, "showingDetails", true);
            Call(popup, "QueueCloseIfInactive"); Application.DoEvents();
            Check(!popup.IsDisposed, "settings stays alive while service details own focus");
            Set(popup, "showingDetails", false);
        }
    }
}
