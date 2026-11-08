using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace ClaudeCompanion {
    internal sealed class FavoriteEntry {
        internal string Id, FilePath, SourceTitle, SourceText, Translation, Engine, Direction, Tag, Notes;
        internal DateTimeOffset CreatedAt;
        // Snapshot of the actual Markdown bytes, used only in memory for conflict detection.
        internal string Revision;
    }

    // Markdown is the sole persistent store of reply content. No index, cache or log is written.
    internal sealed class FavoritesStore {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private const int MaximumFileBytes = 8 * 1024 * 1024;
        internal string RootDirectory { get; private set; }
        internal List<string> LastLoadErrors { get; private set; }

        internal FavoritesStore(string directory) {
            RootDirectory = ValidateDirectory(directory);
            LastLoadErrors = new List<string>();
        }

        internal static string ValidateDirectory(string directory) {
            if (String.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Choose a local favorites folder.");
            string full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (full.Length == 2 && full[1] == ':') full += Path.DirectorySeparatorChar;
            if (full.StartsWith("\\\\", StringComparison.Ordinal) || new Uri(full).IsUnc)
                throw new IOException("Favorites must be stored in a local folder.");
            string volume = Path.GetPathRoot(full);
            if (!String.IsNullOrEmpty(volume) && new DriveInfo(volume).DriveType == DriveType.Network)
                throw new IOException("Favorites must be stored in a local folder.");
            AssertNoLinks(full);
            return full;
        }

        private static void AssertNoLinks(string path) {
            string current = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current)) {
                if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked folders and files are not supported for favorites.");
                string parent = Path.GetDirectoryName(current);
                if (parent == current) break;
                current = parent;
            }
        }

        private string CheckedFile(string path) {
            if (String.IsNullOrWhiteSpace(path)) throw new IOException("The favorite file is missing.");
            string full = Path.GetFullPath(path);
            if (!String.Equals(Path.GetDirectoryName(full).TrimEnd('\\', '/'), RootDirectory.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
                || !String.Equals(Path.GetExtension(full), ".md", StringComparison.OrdinalIgnoreCase))
                throw new IOException("The favorite file is outside the selected folder.");
            AssertNoLinks(full);
            return full;
        }

        internal List<FavoriteEntry> Load() {
            LastLoadErrors.Clear();
            var entries = new List<FavoriteEntry>();
            AssertNoLinks(RootDirectory);
            if (!Directory.Exists(RootDirectory)) return entries;
            foreach (string path in Directory.GetFiles(RootDirectory, "*.md", SearchOption.TopDirectoryOnly)) {
                try {
                    FavoriteEntry entry = ReadFile(CheckedFile(path));
                    if (entry != null) entries.Add(entry);
                } catch (Exception error) {
                    if (!(error is IOException) && !(error is UnauthorizedAccessException) && !(error is FormatException) && !(error is ArgumentException)) throw;
                    // Never include exception details: a parser error can otherwise contain private text.
                    LastLoadErrors.Add(Path.GetFileName(path) + ": " + (error is UnauthorizedAccessException ? "access denied" : "unreadable or invalid Markdown"));
                }
            }
            entries.Sort(delegate(FavoriteEntry a, FavoriteEntry b) {
                int date = b.CreatedAt.CompareTo(a.CreatedAt);
                return date != 0 ? date : StringComparer.OrdinalIgnoreCase.Compare(a.FilePath, b.FilePath);
            });
            return entries;
        }

        internal FavoriteEntry Save(FavoriteEntry entry) {
            if (entry == null) throw new ArgumentNullException("entry");
            if (String.IsNullOrWhiteSpace(entry.Translation)) throw new InvalidOperationException("There is no translation to save.");
            AssertNoLinks(RootDirectory);
            Directory.CreateDirectory(RootDirectory);
            AssertNoLinks(RootDirectory);
            var value = Copy(entry);
            value.Id = Guid.NewGuid().ToString("N");
            if (value.CreatedAt == default(DateTimeOffset)) value.CreatedAt = DateTimeOffset.Now;
            value.Tag = NormalizeTag(value.Tag);
            string body = Serialize(value);
            byte[] bytes = Utf8.GetBytes(body);
            if (bytes.Length > MaximumFileBytes) throw new IOException("The favorite is too large to save.");
            string stem = value.CreatedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + SafeFileName(value.Translation);
            for (int index = 0; index < 10000; index++) {
                string path = Path.Combine(RootDirectory, stem + (index == 0 ? "" : "-" + (index + 1).ToString(CultureInfo.InvariantCulture)) + ".md");
                CheckedFile(path);
                if (File.Exists(path)) continue;
                try { WriteNew(path, bytes); }
                catch (IOException) { if (File.Exists(path)) continue; throw; }
                value.FilePath = path; value.Revision = Hash(bytes);
                return value;
            }
            throw new IOException("There are too many favorites with this file name.");
        }

        internal void Update(FavoriteEntry entry) {
            if (entry == null) throw new ArgumentNullException("entry");
            string path = CheckedFile(entry.FilePath);
            EnsureUnchanged(entry, path);
            var value = Copy(entry); value.Tag = NormalizeTag(value.Tag);
            byte[] bytes = Utf8.GetBytes(Serialize(value));
            if (bytes.Length > MaximumFileBytes) throw new IOException("The favorite is too large to save.");
            string temporary = NewTemporaryPath();
            try {
                WriteBytes(temporary, bytes);
                // Recheck after serialization/write, before atomically replacing the old file.
                EnsureUnchanged(entry, path);
                File.Replace(temporary, path, null);
                entry.Tag = value.Tag; entry.Revision = Hash(bytes);
            } finally { DeleteTemporary(temporary); }
        }

        internal void Delete(FavoriteEntry entry) {
            if (entry == null) throw new ArgumentNullException("entry");
            string path = CheckedFile(entry.FilePath);
            EnsureUnchanged(entry, path);
            File.Delete(path);
        }

        internal FavoriteEntry FindMatch(string source, string result, string engine, string direction, string sourceTitle) {
            foreach (var entry in Load())
                if (String.Equals(entry.SourceText, source ?? "", StringComparison.Ordinal)
                    && String.Equals(entry.Translation, result ?? "", StringComparison.Ordinal)
                    && String.Equals(entry.Engine, engine ?? "", StringComparison.Ordinal)
                    && String.Equals(entry.Direction, direction ?? "", StringComparison.Ordinal)
                    && String.Equals(entry.SourceTitle, sourceTitle ?? "", StringComparison.Ordinal)) return entry;
            return null;
        }

        internal static List<FavoriteEntry> Search(IEnumerable<FavoriteEntry> entries, string query, string tag) {
            var result = new List<FavoriteEntry>(); string needle = (query ?? "").Trim();
            foreach (var entry in entries) {
                if (!String.IsNullOrEmpty(tag) && !String.Equals(entry.Tag, tag, StringComparison.OrdinalIgnoreCase)) continue;
                if (needle.Length == 0 || (entry.SourceText ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                    || (entry.Translation ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) result.Add(entry);
            }
            return result;
        }

        internal static string NormalizeTag(string tag) {
            string value = (tag ?? "").Trim();
            if (value.Length == 0) return FavoritesPreferences.DefaultTag;
            if (value.Length > 60) throw new ArgumentException("A tag can contain at most 60 characters.");
            foreach (char character in value) if (Char.IsControl(character)) throw new ArgumentException("A tag cannot contain control characters.");
            return value;
        }

        internal static string SafeFileName(string text) {
            var cleaned = new StringBuilder();
            string value = (text ?? "").Trim();
            int elements = 0; var enumerator = StringInfo.GetTextElementEnumerator(value);
            while (enumerator.MoveNext() && elements < 24) {
                string element = enumerator.GetTextElement();
                var safe = new StringBuilder();
                foreach (char character in element) {
                    if (Char.IsControl(character) || "<>:\"/\\|?*".IndexOf(character) >= 0) continue;
                    safe.Append(Char.IsWhiteSpace(character) ? ' ' : character);
                }
                if (safe.Length == 0) continue;
                cleaned.Append(safe); elements++;
            }
            string name = Regex.Replace(cleaned.ToString(), " +", " ").Trim(' ', '.');
            if (name.Length == 0) name = "reply";
            string head = name.Split('.')[0];
            if (Regex.IsMatch(head, "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase)) name = "_" + name;
            return name;
        }

        private static FavoriteEntry Copy(FavoriteEntry e) {
            return new FavoriteEntry { Id = e.Id, FilePath = e.FilePath, SourceTitle = e.SourceTitle ?? "", SourceText = e.SourceText ?? "", Translation = e.Translation ?? "", Engine = e.Engine ?? "", Direction = e.Direction ?? "", Tag = e.Tag, Notes = e.Notes ?? "", CreatedAt = e.CreatedAt, Revision = e.Revision };
        }
        private static string Encode(string value) { return new JavaScriptSerializer().Serialize(value ?? ""); }
        private static string Serialize(FavoriteEntry entry) {
            string boundary;
            do { boundary = Guid.NewGuid().ToString("N"); }
            while ((entry.SourceText ?? "").Contains(boundary) || (entry.Translation ?? "").Contains(boundary) || (entry.Notes ?? "").Contains(boundary));
            var text = new StringBuilder("---\ndafeiyu-favorite: 1\n");
            text.Append("id: ").Append(Encode(entry.Id)).Append('\n');
            text.Append("created: ").Append(Encode(entry.CreatedAt.ToString("o", CultureInfo.InvariantCulture))).Append('\n');
            text.Append("source-window: ").Append(Encode(entry.SourceTitle)).Append('\n');
            text.Append("engine: ").Append(Encode(entry.Engine)).Append('\n');
            text.Append("direction: ").Append(Encode(entry.Direction)).Append('\n');
            text.Append("tag: ").Append(Encode(entry.Tag)).Append('\n');
            text.Append("boundary: ").Append(Encode(boundary)).Append("\n---\n\n# Saved reply / 回复收藏\n\n");
            AppendSection(text, boundary, "translation", "Translation / 译文", entry.Translation);
            AppendSection(text, boundary, "original", "Original / 原文", entry.SourceText);
            AppendSection(text, boundary, "notes", "Notes / 备注", entry.Notes);
            return text.ToString();
        }
        private static void AppendSection(StringBuilder target, string boundary, string key, string title, string body) {
            target.Append("## ").Append(title).Append("\n<!-- dafeiyu:").Append(boundary).Append(':').Append(key).Append(":start -->\n");
            target.Append(body ?? "");
            target.Append("\n<!-- dafeiyu:").Append(boundary).Append(':').Append(key).Append(":end -->\n\n");
        }
        private FavoriteEntry ReadFile(string path) {
            byte[] bytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                if (stream.Length > MaximumFileBytes) throw new IOException("The favorite file is too large.");
                bytes = new byte[(int)stream.Length]; int offset = 0;
                while (offset < bytes.Length) {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new IOException("The favorite file could not be completely read.");
                    offset += count;
                }
            }
            string text = Utf8.GetString(bytes).TrimStart('\uFEFF');
            if (!text.StartsWith("---\n", StringComparison.Ordinal) && !text.StartsWith("---\r\n", StringComparison.Ordinal)) return null;
            int headerEnd = text.IndexOf("\n---", 4, StringComparison.Ordinal);
            if (headerEnd < 0 || headerEnd > 16384) return null;
            string header = text.Substring(0, headerEnd); var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string raw in header.Split('\n')) {
                string line = raw.TrimEnd('\r'); int colon = line.IndexOf(':');
                if (colon < 0) continue;
                string key = line.Substring(0, colon).Trim(), value = line.Substring(colon + 1).Trim();
                if (fields.ContainsKey(key)) throw new FormatException("Duplicate favorite metadata.");
                fields.Add(key, value);
            }
            string schema;
            if (!fields.TryGetValue("dafeiyu-favorite", out schema) || schema != "1") return null;
            string id = Decode(fields, "id"), boundary = Decode(fields, "boundary"); Guid parsedId, parsedBoundary;
            if (!Guid.TryParseExact(id, "N", out parsedId) || !Guid.TryParseExact(boundary, "N", out parsedBoundary)) throw new FormatException("Invalid favorite identity.");
            DateTimeOffset created;
            if (!DateTimeOffset.TryParse(Decode(fields, "created"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out created)) throw new FormatException("Invalid favorite date.");
            return new FavoriteEntry { Id = id, FilePath = path, CreatedAt = created, SourceTitle = Decode(fields, "source-window"), Engine = Decode(fields, "engine"), Direction = Decode(fields, "direction"), Tag = NormalizeTag(Decode(fields, "tag")), Translation = ReadSection(text, boundary, "translation"), SourceText = ReadSection(text, boundary, "original"), Notes = ReadSection(text, boundary, "notes"), Revision = Hash(bytes) };
        }
        private static string Decode(Dictionary<string, string> fields, string key) {
            string value;
            if (!fields.TryGetValue(key, out value)) throw new FormatException("Missing favorite metadata.");
            try { return new JavaScriptSerializer().Deserialize<string>(value) ?? ""; }
            catch { throw new FormatException("Invalid favorite metadata."); }
        }
        private static string ReadSection(string text, string boundary, string key) {
            string start = "<!-- dafeiyu:" + boundary + ":" + key + ":start -->", end = "<!-- dafeiyu:" + boundary + ":" + key + ":end -->";
            int first = text.IndexOf(start, StringComparison.Ordinal), last = text.IndexOf(end, StringComparison.Ordinal);
            if (first < 0 || last < first || text.IndexOf(start, first + start.Length, StringComparison.Ordinal) >= 0 || text.IndexOf(end, last + end.Length, StringComparison.Ordinal) >= 0) throw new FormatException("Invalid favorite sections.");
            string newline = text.StartsWith("---\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            int markerEnd = first + start.Length;
            if (markerEnd + newline.Length > text.Length || text.Substring(markerEnd, newline.Length) != newline) throw new FormatException("Invalid favorite section boundary.");
            int bodyStart = markerEnd + newline.Length, bodyEnd = last - newline.Length;
            if (bodyEnd < 0 || text.Substring(bodyEnd, newline.Length) != newline) throw new FormatException("Invalid favorite section boundary.");
            if (bodyEnd < bodyStart) throw new FormatException("Invalid favorite section boundary.");
            return text.Substring(bodyStart, bodyEnd - bodyStart);
        }
        private void EnsureUnchanged(FavoriteEntry entry, string path) {
            if (!File.Exists(path)) throw new IOException("This favorite was deleted outside the app. Refresh the notebook.");
            FavoriteEntry current = ReadFile(path);
            if (current == null || current.Id != entry.Id || String.IsNullOrEmpty(entry.Revision) || current.Revision != entry.Revision)
                throw new IOException("This favorite changed outside the app. Refresh before saving or deleting it.");
        }
        private string NewTemporaryPath() { return Path.Combine(RootDirectory, ".dafeiyu-" + Guid.NewGuid().ToString("N") + ".tmp"); }
        private void WriteNew(string path, byte[] bytes) {
            string temporary = NewTemporaryPath();
            try { WriteBytes(temporary, bytes); CheckedFile(path); File.Move(temporary, path); }
            finally { DeleteTemporary(temporary); }
        }
        private static void WriteBytes(string path, byte[] bytes) { using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); } }
        private static void DeleteTemporary(string path) { if (File.Exists(path)) File.Delete(path); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }

    // Only folder/tag preferences are persisted here; reply text never enters this file.
    internal static class FavoritesPreferences {
        internal const string DefaultTag = "未分类";
        private sealed class State { public string Folder, LastTag; public List<string> Tags = new List<string>(); }
        private static string SettingsPath { get { return Path.Combine(Preferences.DataDirectory, "favorites-settings.json"); } }
        internal static string Folder { get { var state = Read(); return String.IsNullOrWhiteSpace(state.Folder) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "大肥鱼收藏") : state.Folder; } }
        internal static string LastTag { get { return Read().LastTag; } set { var state = Read(); state.LastTag = FavoritesStore.NormalizeTag(value); AddTag(state, state.LastTag); Write(state); } }
        internal static List<string> Tags { get { return Read().Tags; } }
        internal static void SetFolder(string directory) { var state = Read(); state.Folder = FavoritesStore.ValidateDirectory(directory); Write(state); }
        internal static void CreateTag(string tag) { var state = Read(); AddTag(state, FavoritesStore.NormalizeTag(tag)); Write(state); }
        private static void AddTag(State state, string value) { foreach (string tag in state.Tags) if (String.Equals(tag, value, StringComparison.OrdinalIgnoreCase)) return; state.Tags.Add(value); }
        private static State Read() {
            var state = new State { LastTag = DefaultTag }; state.Tags.Add(DefaultTag);
            string path = SettingsPath;
            if (!File.Exists(path)) return state;
            try {
                var json = new JavaScriptSerializer();
                var data = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
                object value;
                if (data.TryGetValue("folder", out value) && value is string && !String.IsNullOrWhiteSpace((string)value)) state.Folder = FavoritesStore.ValidateDirectory((string)value);
                if (data.TryGetValue("lastTag", out value) && value is string) state.LastTag = FavoritesStore.NormalizeTag((string)value);
                if (data.TryGetValue("tags", out value) && value is System.Collections.IEnumerable) foreach (object tag in (System.Collections.IEnumerable)value) if (tag is string) AddTag(state, FavoritesStore.NormalizeTag((string)tag));
                AddTag(state, state.LastTag);
                return state;
            } catch (Exception error) {
                if (error is IOException || error is UnauthorizedAccessException) throw;
                throw new IOException("Favorites settings could not be read. The file has not been overwritten.");
            }
        }
        private static void Write(State state) {
            string path = SettingsPath, folder = Path.GetDirectoryName(path); Directory.CreateDirectory(folder);
            string temporary = Path.Combine(folder, ".favorites-settings-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                string json = new JavaScriptSerializer().Serialize(new { schema = 1, folder = state.Folder ?? "", lastTag = state.LastTag, tags = state.Tags });
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
