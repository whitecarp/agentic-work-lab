using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WhaleClaudeHelper;

internal static class UiReadOnlyTests {
    private static int checks;
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static T Field<T>(object value, string name) { return (T)value.GetType().GetField(name, PrivateInstance).GetValue(value); }
    private static void Invoke(object value, string name, params object[] args) { value.GetType().GetMethod(name, PrivateInstance).Invoke(value, args); }
    private static void TypeQuery(ComboBox box, string query) {
        box.SelectedIndex = -1;
        box.Text = query;
        box.SelectionStart = query.Length;
        typeof(ComboBox).GetMethod("OnTextUpdate", PrivateInstance).Invoke(box, new object[] { EventArgs.Empty });
    }
    [STAThread]
    private static void Main(string[] args) {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string beforeZone = SystemTools.CurrentTimeZoneId();
        string[] beforeBackups = SystemTools.ListTimeZoneBackups().ToArray();
        Ui.English = true;
        using (var form = new HelperForm(true)) {
            Check(form.Text.StartsWith("Whale Claude Helper"), "English application title");
            var box = Field<ComboBox>(form, "zones");
            int originalCount = box.Items.Count;
            Check(originalCount > 50, "Windows zones populated");
            TypeQuery(box, "Central");
            Check(box.Items.Count > 0 && box.Items.Count < originalCount, "English region search narrows actual Windows zones");
            Check(box.Items.Cast<ZoneRow>().All(x => x.ToString().IndexOf("Central", StringComparison.OrdinalIgnoreCase) >= 0), "all English matches contain query");
            var central = box.Items.Cast<ZoneRow>().FirstOrDefault(x => x.Zone.Id == "Central Standard Time");
            Check(central != null, "US Central can be found despite UTC prefix");
            box.SelectedItem = central;
            Check(box.Text == central.ToString(), "selection resolves visible label to a valid zone");
            // Preview Review() returns false before dispatching any mutation.
            Invoke(form, "ApplyZone");
            Check(!Field<bool>(form, "busy"), "valid selected zone reaches preview review, without dispatching apply");
            TypeQuery(box, "新加坡");
            Check(box.Items.Count > 0 && box.Items.Count < originalCount, "Chinese region search finds Singapore on this Windows locale");
            Check(box.Items.Cast<ZoneRow>().All(x => x.ToString().Contains("新加坡")), "Chinese result labels contain query");
            var singapore = box.Items.Cast<ZoneRow>().First();
            box.SelectedItem = singapore;
            Invoke(form, "ApplyZone");
            Check(!Field<bool>(form, "busy"), "Chinese selection resolves without starting mutation");
            TypeQuery(box, "Central Standard Time");
            Invoke(form, "ApplyZone");
            Check(!Field<bool>(form, "busy"), "typed exact Windows ID resolves without mutation in preview");
            TypeQuery(box, "NoSuchTimeZone-OnlyFixture");
            Check(box.Items.Count == 0, "unknown query does not silently select a zone");
            TypeQuery(box, "");
            Check(box.Items.Count == originalCount, "clearing query restores all zones");
            Invoke(form, "UpdateZone");
            Check((box.SelectedItem as ZoneRow).Zone.Id == beforeZone, "refresh resolves current zone safely");
            string path = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "whale-timezone-ui-preview.png");
            form.RenderPreview(path, 2);
            Check(File.Exists(path) && new FileInfo(path).Length > 1000, "English preview rendered");
        }
        Check(beforeZone == SystemTools.CurrentTimeZoneId(), "actual system timezone unchanged");
        Check(beforeBackups.SequenceEqual(SystemTools.ListTimeZoneBackups()), "no timezone mutation or backup created");
        Console.WriteLine("PASS " + checks + " read-only UI checks; no system settings changed.");
    }
}
