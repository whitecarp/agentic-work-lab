using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ClaudeCompanion;

#if FAVORITES_ISOLATED_TESTS
namespace ClaudeCompanion {
    internal static class Preferences {
        internal static string DataDirectory { get { return Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR"); } }
    }
}
#endif

internal static class FavoritesTests {
    private static int checks;
    private static void Check(bool result, string name) { checks++; if (!result) throw new Exception(name); }
    private static void Throws(Action operation, string name) {
        bool failed = false;
        try { operation(); } catch (IOException) { failed = true; } catch (ArgumentException) { failed = true; } catch (InvalidOperationException) { failed = true; }
        Check(failed, name);
    }
    private static FavoriteEntry Entry(string source, string translation) {
        return new FavoriteEntry { CreatedAt = new DateTimeOffset(2026, 11, 2, 15, 4, 3, TimeSpan.FromHours(8)), SourceTitle = "Claude · synthetic project", SourceText = source, Translation = translation, Engine = "google", Direction = "auto:zh-CN", Tag = "工作", Notes = "Only a synthetic fixture." };
    }
    private static void SaveAndRead(string root) {
        string folder = Path.Combine(root, "round-trip"); var store = new FavoritesStore(folder);
        Check(store.Load().Count == 0, "new notebook is empty");
        Check(!Directory.Exists(folder), "constructing and reading a notebook never creates a folder or saves replies");
        string original = "First line\r\n## Original / 原文\n```csharp\nstring x = \"hello\";\n```\n<!-- dafeiyu:literal:notes:end -->\n多语言 🌊\r";
        var saved = store.Save(Entry(original, "保存译文\n\n含有 ## Notes / 备注 和 🐳 emoji。"));
        var loaded = store.Load().Single();
        Check(loaded.SourceText == original, "arbitrary Markdown and trailing CR round-trip exactly");
        Check(loaded.Translation == saved.Translation, "translation including blank lines round-trips");
        Check(loaded.Notes == saved.Notes, "notes round-trip");
        Check(loaded.SourceTitle == saved.SourceTitle, "window title round-trips");
        Check(loaded.CreatedAt.Equals(saved.CreatedAt), "creation time and offset round-trip");
        Check(loaded.Direction == "auto:zh-CN" && loaded.Engine == "google", "language and translation method round-trip");
        Check(loaded.Tag == "工作" && loaded.Id == saved.Id, "tag and stable identity round-trip");
        Check(loaded.Revision == saved.Revision && loaded.Revision.Length == 64, "revision is a content fingerprint held in memory");
        string content = File.ReadAllText(saved.FilePath);
        Check(content.Contains("## Translation / 译文") && content.Contains(original), "file is readable Markdown with the actual original text");
        Check(Directory.GetFiles(folder).Length == 1 && Directory.GetFiles(folder, "*.md").Length == 1, "saving writes only one Markdown file with no index or sidecar");
        Check(Path.GetFileName(saved.FilePath).StartsWith("20261102-150403-", StringComparison.Ordinal), "file name begins with creation time");
        var second = store.Save(Entry(original, saved.Translation));
        Check(second.FilePath != saved.FilePath && store.Load().Count == 2, "same second and snippet never overwrite an existing favorite");
        Check(File.ReadAllText(saved.FilePath) == content, "collision handling preserves previous bytes");
        Check(store.FindMatch(original, saved.Translation, "google", "auto:zh-CN", saved.SourceTitle) != null, "current reply can find its saved item");
        Check(store.FindMatch(original, saved.Translation, "offline", "auto:zh-CN", saved.SourceTitle) == null, "matching respects translation method");
        Check(store.FindMatch(original, saved.Translation, "google", "auto:en", saved.SourceTitle) == null, "matching respects language");
        Check(store.FindMatch(original, saved.Translation, "google", "auto:zh-CN", "other") == null, "matching respects source identity");
        Throws(delegate { store.Save(Entry("anything", " \n ")); }, "empty translations cannot be saved");
        Check(store.Load().Count == 2, "invalid save does not create a file");
    }
    private static void SearchAndTags(string root) {
        var store = new FavoritesStore(Path.Combine(root, "search"));
        store.Save(Entry("Review the BUILD plan", "检查构建方案"));
        var second = Entry("Keep the original video", "保留原始视频"); second.Tag = "视频"; second.Notes = "not-searchable-note"; store.Save(second);
        var list = store.Load();
        Check(FavoritesStore.Search(list, "build", null).Count == 1, "English original search is case-insensitive");
        Check(FavoritesStore.Search(list, "构建", null).Count == 1, "Chinese translation search works");
        Check(FavoritesStore.Search(list, "原始", "视频").Count == 1, "search and tag filter compose");
        Check(FavoritesStore.Search(list, "构建", "视频").Count == 0, "tag filter excludes other tags");
        Check(FavoritesStore.Search(list, "", "视频").Count == 1, "blank query lists a tag");
        Check(FavoritesStore.Search(list, "  ", null).Count == 2, "blank query and no tag list everything");
        Check(FavoritesStore.Search(list, "not-searchable-note", null).Count == 0, "search does not leak unrelated metadata into matching");
        Check(FavoritesStore.NormalizeTag("  工作  ") == "工作", "tag whitespace normalizes");
        Check(FavoritesStore.NormalizeTag(null) == FavoritesPreferences.DefaultTag, "empty tag has a stable default");
        Throws(delegate { FavoritesStore.NormalizeTag("bad\r\ntag"); }, "multiline tags rejected");
        Throws(delegate { FavoritesStore.NormalizeTag(new string('a', 61)); }, "excessively long tags rejected");
    }
    private static void FileNames() {
        string name = FavoritesStore.SafeFileName("  <>:/\\?*\"| hello. \n世界 ");
        Check(name == "hello. 世界", "invalid filename characters and extra whitespace removed");
        Check(FavoritesStore.SafeFileName("... /\\\r\n") == "reply", "empty cleaned snippet uses fallback");
        Check(FavoritesStore.SafeFileName("CON") == "_CON", "Windows reserved device name guarded");
        Check(FavoritesStore.SafeFileName("lpt9.txt") == "_lpt9.txt", "reserved device stem with extension guarded");
        Check(!FavoritesStore.SafeFileName("trailing. ").EndsWith("."), "trailing dots removed");
        string emoji = String.Concat(Enumerable.Repeat("🐳", 30).ToArray());
        string shortEmoji = FavoritesStore.SafeFileName(emoji);
        Check(new StringInfo(shortEmoji).LengthInTextElements == 24, "snippet length counts Unicode text elements");
        Check(shortEmoji.Length == 48 && Char.IsLowSurrogate(shortEmoji[shortEmoji.Length - 1]), "emoji surrogate pairs are not split");
        string combining = String.Concat(Enumerable.Repeat("e\u0301", 30).ToArray());
        Check(new StringInfo(FavoritesStore.SafeFileName(combining)).LengthInTextElements == 24, "combining characters are not split");
    }
    private static void ExternalChanges(string root) {
        string folder = Path.Combine(root, "external"); var store = new FavoritesStore(folder);
        var item = store.Save(Entry("Source before edit", "译文初版")); string path = item.FilePath;
        item.Tag = "已审阅"; item.Notes = "A useful note\n第二行备注。"; store.Update(item);
        var edited = store.Load().Single();
        Check(edited.Notes == item.Notes && edited.Tag == "已审阅", "notes and tag edits persist");
        Check(edited.SourceText == "Source before edit" && edited.Translation == "译文初版", "editing metadata preserves both reply bodies");
        Check(edited.Id == item.Id && edited.FilePath == path, "metadata edits retain identity and file path");
        Check(!Directory.GetFiles(folder, "*.tmp").Any(), "atomic update leaves no transient file");
        string externallyEdited = File.ReadAllText(path).Replace("译文初版", "用户手动修改的译文"); File.WriteAllText(path, externallyEdited, new UTF8Encoding(false));
        item.Notes = "stale pending note";
        Throws(delegate { store.Update(item); }, "stale update refuses to overwrite external edit");
        Check(File.ReadAllText(path) == externallyEdited, "conflict preserves exact external bytes");
        Throws(delegate { store.Delete(item); }, "stale delete requires refreshed confirmation");
        Check(File.Exists(path), "stale delete preserves the file");
        var fresh = store.Load().Single();
        Check(fresh.Translation == "用户手动修改的译文", "refresh reads manual Markdown changes");
        Check(fresh.Revision != item.Revision, "manual edits change revision");
        store.Delete(fresh); Check(store.Load().Count == 0 && !File.Exists(path), "delete removes exactly the loaded Markdown");
        Throws(delegate { store.Update(fresh); }, "externally missing file is not silently recreated");
        var renamed = store.Save(Entry("rename", "重命名")); string newPath = Path.Combine(folder, "renamed by user.md"); File.Move(renamed.FilePath, newPath);
        Check(store.Load().Single().FilePath == newPath, "refresh respects user file rename");
        File.Delete(newPath); Check(store.Load().Count == 0, "refresh respects manual file deletion");
        var crlf = store.Save(Entry("source\nnext", "译文\n下一行")); string crlfText = File.ReadAllText(crlf.FilePath).Replace("\n", "\r\n"); File.WriteAllText(crlf.FilePath, crlfText, new UTF8Encoding(true));
        var crlfEntry = store.Load().Single();
        Check(crlfEntry.SourceText == "source\r\nnext" && crlfEntry.Translation == "译文\r\n下一行", "BOM and Windows editor line ending changes remain readable");
    }
    private static void Protection(string root) {
        string folder = Path.Combine(root, "guards"); var store = new FavoritesStore(folder);
        var item = store.Save(Entry("private-test-marker", "fixture text"));
        string ordinary = Path.Combine(folder, "ordinary.md"); File.WriteAllText(ordinary, "# An unrelated personal note\nDo not touch.");
        Check(store.Load().Count == 1, "unrelated Markdown is excluded");
        var fake = Entry("fake", "fake"); fake.Id = item.Id; fake.FilePath = ordinary; fake.Revision = item.Revision;
        Throws(delegate { store.Delete(fake); }, "unrelated Markdown cannot be deleted through a forged entry");
        Check(File.ReadAllText(ordinary).Contains("Do not touch"), "unrelated Markdown is unchanged");
        fake.FilePath = Path.Combine(root, "outside.md"); File.WriteAllText(fake.FilePath, "outside");
        Throws(delegate { store.Delete(fake); }, "outside-root deletion rejected");
        Throws(delegate { store.Update(fake); }, "outside-root update rejected");
        Check(File.ReadAllText(fake.FilePath) == "outside", "outside file is unchanged");
        fake.FilePath = Path.Combine(folder, "..", "outside.md");
        Throws(delegate { store.Delete(fake); }, "parent traversal is rejected");
        fake.FilePath = item.FilePath; fake.Id = Guid.NewGuid().ToString("N");
        Throws(delegate { store.Update(fake); }, "mismatched identity cannot replace a favorite");
        string corrupt = Path.Combine(folder, "broken.md"); File.WriteAllText(corrupt, File.ReadAllText(item.FilePath).Replace("tag: \"工作\"", "tag: private-test-marker"));
        Check(store.Load().Count == 1 && store.LastLoadErrors.Count == 1, "malformed favorite is reported and valid ones still load");
        Check(!store.LastLoadErrors[0].Contains("private-test-marker"), "errors do not reveal file content");
        Throws(delegate { new FavoritesStore("\\\\localhost\\share\\favorites"); }, "UNC/network folders rejected");
        Check(File.Exists(item.FilePath) && File.Exists(corrupt), "loading does not rewrite or remove malformed files");
    }
    private static void PreferencesTests(string root) {
        string settings = Path.Combine(Preferences.DataDirectory, "favorites-settings.json");
        Check(!File.Exists(settings), "settings are absent until an explicit setting changes");
        Check(FavoritesPreferences.LastTag == FavoritesPreferences.DefaultTag, "first-use last tag is stable");
        Check(FavoritesPreferences.Tags.Contains(FavoritesPreferences.DefaultTag), "default tag exists");
        Check(!File.Exists(settings), "reading default preferences does not write a file");
        string defaultFolder = FavoritesPreferences.Folder;
        FavoritesPreferences.CreateTag("First tag"); FavoritesPreferences.CreateTag("Second tag");
        Check(FavoritesPreferences.Tags.Contains("First tag") && FavoritesPreferences.Tags.Contains("Second tag"), "tags can be created before choosing a custom folder");
        Check(FavoritesPreferences.Folder == defaultFolder, "empty stored folder retains the default Documents location");
        FavoritesPreferences.LastTag = "First tag";
        Check(FavoritesPreferences.LastTag == "First tag" && FavoritesPreferences.Folder == defaultFolder, "last tag round-trips before a custom folder is selected");
        string folder = Path.Combine(root, "chosen-folder"); FavoritesPreferences.SetFolder(folder);
        Check(FavoritesPreferences.Folder == Path.GetFullPath(folder), "selected folder persists");
        Check(!Directory.Exists(folder), "changing preference does not save a reply or create notebook content");
        FavoritesPreferences.CreateTag("Research"); FavoritesPreferences.CreateTag("research");
        Check(FavoritesPreferences.Tags.Count(delegate(string t) { return t.Equals("Research", StringComparison.OrdinalIgnoreCase); }) == 1, "tags deduplicate case-insensitively");
        FavoritesPreferences.LastTag = "视频";
        Check(FavoritesPreferences.LastTag == "视频" && FavoritesPreferences.Tags.Contains("视频"), "last-used tag persists and enters tag choices");
        Check(!File.ReadAllText(settings).Contains("private-test-marker"), "preferences contain no reply text");
        Check(Directory.GetFiles(Preferences.DataDirectory, "*.tmp").Length == 0, "settings leave no temporary file");
        string previous = File.ReadAllText(settings); Throws(delegate { FavoritesPreferences.SetFolder("\\\\server\\share"); }, "network setting rejected");
        Check(File.ReadAllText(settings) == previous, "invalid preference update retains previous bytes");
        File.WriteAllText(settings, "invalid synthetic JSON");
        Throws(delegate { FavoritesPreferences.CreateTag("Do not overwrite"); }, "unreadable settings do not get silently reset");
        Check(File.ReadAllText(settings) == "invalid synthetic JSON", "unreadable settings remain recoverable");
    }
    [STAThread] private static int Main() {
        string previous = Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR");
        string root = Path.Combine(String.IsNullOrWhiteSpace(previous) ? Path.GetTempPath() : previous, "FavoritesTests-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR", Path.Combine(root, "settings"));
        try {
            Directory.CreateDirectory(root);
            SaveAndRead(root); SearchAndTags(root); FileNames(); ExternalChanges(root); Protection(root); PreferencesTests(root);
            Console.WriteLine("FavoritesTests passed: " + checks + " isolated checks. Synthetic Markdown only; no user folders, clipboard, network or reply index used.");
            return 0;
        } catch (Exception error) { Console.Error.WriteLine("FavoritesTests failed: " + error.Message); return 1; }
        finally { Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR", previous); }
    }
}
