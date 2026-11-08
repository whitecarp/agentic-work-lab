using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ClaudeCompanion;

internal static class FavoritesFormTests {
    private static int checks;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object instance, string name) { return (T)instance.GetType().GetField(name, Private).GetValue(instance); }
    private static void Check(bool valid, string message) { if (!valid) throw new Exception(message); checks++; }
    private static FavoriteEntry Entry(string translation, string original, string tag, int hour) {
        return new FavoriteEntry { Translation = translation, SourceText = original, Tag = tag, Notes = "", Engine = "google", Direction = "auto:zh-CN", SourceTitle = "Claude · Fixture", CreatedAt = new DateTimeOffset(2026, 11, 2, hour, 30, 0, TimeSpan.Zero) };
    }
    private static string Fingerprint(string directory) {
        var names = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.md") : new string[0]; Array.Sort(names, StringComparer.Ordinal);
        var text = new System.Text.StringBuilder(); foreach (string name in names) text.Append(name).Append('\n').Append(File.ReadAllText(name)).Append('\n'); return text.ToString();
    }

    [STAThread] private static int Main() {
        try {
            string data = Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR");
            Check(!String.IsNullOrWhiteSpace(data), "requires an isolated DAFEIYU_DATA_DIR");
            string fixture = Path.Combine(data, "favorites-ui-" + Guid.NewGuid().ToString("N"));
            var store = new FavoritesStore(fixture);
            FavoritesPreferences.CreateTag("工作"); FavoritesPreferences.CreateTag("Ideas");
            FavoriteEntry first = store.Save(Entry("请保留原始文件。", "Keep the original file.", "工作", 11));
            FavoriteEntry second = store.Save(Entry("An ENGLISH search result.", "这是中文原文。", "Ideas", 10));
            string originalDisk = Fingerprint(fixture);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            foreach (bool english in new[] { false, true }) {
                Ui.English = english;
                using (var form = new FavoritesForm(store, true)) {
                    float dpi; using (var g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX / 96F;
                    Check(form.ClientSize.Width == (int)Math.Round(600 * dpi) && form.ClientSize.Height == (int)Math.Round(560 * dpi), "window stays at 600 by 560 logical pixels");
                    Check(form.Text.Contains(english ? "Reply notebook" : "回复收藏本"), "window title localizes");
                    Check(form.SelectedEntry != null && form.SelectedEntry.Id == first.Id, "newest saved file is initially selected");
                    Check(Field<ListBox>(form, "list").Items.Count == 2, "all saved files appear");
                    Check(!Field<Button>(form, "save").Enabled, "unmodified tag and note cannot save");
                    Check(Field<TextBox>(form, "translated").ReadOnly && Field<TextBox>(form, "original").ReadOnly, "stored source and translation are read-only in UI");
                    Check(!Field<TextBox>(form, "notes").ReadOnly, "note can be edited for a selection");
                    string copied = null; form.CopyText = delegate(string text) { copied = text; };
                    form.CopySelected(false); Check(copied == first.Translation, "copy translation uses saved translation, no clipboard in this test");
                    form.CopySelected(true); Check(copied == first.SourceText, "copy original uses original text");
                    Field<TextBox>(form, "search").Text = "english";
                    Check(Field<ListBox>(form, "list").Items.Count == 1 && form.SelectedEntry.Id == second.Id, "English translation search is case insensitive");
                    Field<TextBox>(form, "search").Text = "中文原文";
                    Check(Field<ListBox>(form, "list").Items.Count == 1 && form.SelectedEntry.Id == second.Id, "Chinese original search works");
                    Field<TextBox>(form, "search").Text = "not present anywhere";
                    Check(form.SelectedEntry == null && Field<ListBox>(form, "list").Items.Count == 0, "no-match search clears detail selection");
                    Check(!Field<Button>(form, "remove").Enabled && !Field<Button>(form, "copyOriginal").Enabled, "empty results disable destructive and copy actions");
                    Field<TextBox>(form, "search").Clear(); form.SelectTag("工作");
                    Check(Field<ListBox>(form, "list").Items.Count == 1 && form.SelectedEntry.Id == first.Id, "tag filter selects expected saved files");
                    form.SelectTag(null);
                    bool asked = false; form.ConfirmDelete = delegate(FavoriteEntry entry) { asked = true; return false; };
                    Check(!form.DeleteSelected() && asked && File.Exists(first.FilePath), "delete cancellation leaves the Markdown file untouched");
                    Check(Fingerprint(fixture) == originalDisk, "construction, searching, filtering, copying and cancellation do not write reply files");
                }
            }
            Ui.English = true;
            using (var form = new FavoritesForm(store, true)) {
                var search = Field<TextBox>(form, "search"); var notes = Field<TextBox>(form, "notes");
                notes.Text = "A note typed locally.";
                Check(form.HasUnsavedChanges && Field<Button>(form, "save").Enabled, "typing a note marks explicit-save state");
                Check(Fingerprint(fixture) == originalDisk, "typing a note alone does not write it");
                int prompts = 0; form.ConfirmUnsaved = delegate { prompts++; return DialogResult.Cancel; };
                search.Text = "english";
                Check(prompts == 1 && search.Text == "" && notes.Text == "A note typed locally." && form.SelectedEntry.Id == first.Id, "cancelled navigation preserves draft and selection");
                Check(form.SaveEdits(), "explicit Save writes tag and note");
                Check(!form.HasUnsavedChanges && store.Load()[0].Notes == "A note typed locally.", "saved note round-trips to file and clean UI state");
                var tag = Field<ThemedComboBox>(form, "detailTag");
                for (int i = 0; i < tag.Items.Count; i++) if (tag.Items[i].ToString() == "Ideas") tag.SelectedIndex = i;
                Check(form.HasUnsavedChanges && form.SaveEdits(), "tag edits use the same explicit Save action");
                Check(store.Load()[0].Tag == "Ideas", "new tag is stored in Markdown");
                FavoriteEntry external = store.Load()[0]; external.Translation = "Changed outside the app."; store.Update(external);
                notes.Text = "A stale draft must not overwrite the external edit.";
                Check(!form.SaveEdits(), "saving stale detail rejects an external edit");
                Check(store.Load()[0].Translation == "Changed outside the app.", "external text remains intact after conflict");
                Check(form.HasUnsavedChanges, "conflicting note remains in the UI for the user");
                form.ConfirmUnsaved = delegate { return DialogResult.No; }; form.RefreshFromDisk();
                Check(Field<TextBox>(form, "translated").Text == "Changed outside the app." && !form.HasUnsavedChanges, "refresh with discard loads the disk version");
                File.Delete(first.FilePath); form.RefreshFromDisk();
                Check(Field<ListBox>(form, "list").Items.Count == 1 && form.SelectedEntry.Id == second.Id, "refresh respects manual deletion");
                string ordinary = Path.Combine(fixture, "personal-notes.md"); File.WriteAllText(ordinary, "Keep this ordinary Markdown file.");
                form.ConfirmDelete = delegate(FavoriteEntry entry) { return true; };
                Check(form.DeleteSelected() && !File.Exists(second.FilePath), "confirmed deletion removes the corresponding favorite file");
                Check(File.Exists(ordinary) && File.ReadAllText(ordinary) == "Keep this ordinary Markdown file.", "unrelated Markdown file is preserved");
                Check(form.SelectedEntry == null && Field<ListBox>(form, "list").Items.Count == 0, "last deletion enters the empty state");
                string captured = null; form.CopyText = delegate(string value) { captured = value; }; form.CopySelected(true);
                Check(captured == null, "empty state cannot copy stale text");
            }
            string missing = Path.Combine(data, "never-created-" + Guid.NewGuid().ToString("N"));
            using (var empty = new FavoritesForm(new FavoritesStore(missing), true)) {
                Check(!Directory.Exists(missing) && empty.SelectedEntry == null, "opening an empty notebook never creates a reply folder");
                Check(Field<TextBox>(empty, "notes").ReadOnly, "empty notebook notes are not editable");
            }
            Console.WriteLine("FavoritesFormTests passed: " + checks + " checks. Isolated files only; no clipboard, network, user windows or real dialogs."); return 0;
        } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
