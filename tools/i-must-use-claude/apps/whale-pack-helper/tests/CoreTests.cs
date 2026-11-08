using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using WhalePack;

namespace WhalePack.Tests {
    public static class CoreTests {
        private static int assertions;
        private static void Assert(bool condition, string message) { assertions++; if (!condition) throw new Exception("Core: " + message); }
        public static int Run() {
            assertions = 0;
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "core-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try { Classification(root); Detection(root); Planner(); Packaging(root); Settings(root); ActualVolumes(root); }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            return assertions;
        }
        private static string Write(string root, string relative, string contents) {
            string path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, contents, new UTF8Encoding(false)); return path;
        }
        private static void Classification(string root) {
            Assert(FileScanner.Classify("x.MP4") == ProcessingMode.CompressVideo, "MP4 classification");
            Assert(FileScanner.Classify("x.mov") == ProcessingMode.CompressVideo, "MOV classification");
            Assert(FileScanner.Classify("x.webp") == ProcessingMode.ResizeImage, "WebP classification");
            Assert(FileScanner.Classify("x.BMP") == ProcessingMode.ResizeImage, "BMP classification");
            Assert(FileScanner.Classify(".env") == ProcessingMode.Exclude, "env classification");
            Assert(FileScanner.Classify("readme.md") == ProcessingMode.Original, "original classification");
            Assert(FileScanner.Classify(root) == ProcessingMode.SourceArchive, "folder classification");
            foreach (string name in new string[] { ".git", "bin", "obj", "node_modules", "__pycache__", ".vs", "artifacts", "runtime", "models", "BIN" }) Assert(FileScanner.IsSkippedDirectory(name), "skip " + name);
            string project = Path.Combine(root, "scan-project");
            Write(project, "src/main.cs", "class Main { }"); Write(project, "bin/test.exe", "binary placeholder"); Write(project, "runtime/lang.txt", "runtime"); Write(project, "models/vocab.txt", "model"); Write(project, ".git/config", "git"); Write(project, ".env", "fake settings"); Write(project, "lib.dll", "binary");
            string large = Path.Combine(project, "large.dat"); using (FileStream fs = File.Create(large)) fs.SetLength(20000001);
            InputItem scanned = FileScanner.Scan(project, CancellationToken.None);
            Assert(scanned.Entries.Count == 8, "all source entries enumerated");
            Assert(scanned.Entries.Count(delegate(SourceEntry e) { return !e.Excluded; }) == 1, "only source retained");
            Assert(scanned.Entries.Single(delegate(SourceEntry e) { return e.RelativePath == ".env"; }).Sensitive, "env marked sensitive");
            Assert(scanned.Entries.Single(delegate(SourceEntry e) { return e.RelativePath == "large.dat"; }).Excluded, "20MB rule");
            Assert(FileScanner.Scan(Path.Combine(project, "lib.dll"), CancellationToken.None).Mode == ProcessingMode.Original, "direct non-source DLL still original");
            Assert(FileScanner.Classify(Path.Combine(project, "lib.dll")) == ProcessingMode.Original, "source extension exclusion is folder-only");
        }
        private static void Detection(string root) {
            string secret = "sk-" + new string('a', 34);
            foreach (string filename in new string[] { ".env", ".env.local", "a.pem", "a.key", "a.pfx", "id_rsa.pub", "credentials.json", "cookies", "Cookies.sqlite", "User Data/Default/Preferences", "Mozilla/Firefox/Profiles/x/prefs.js" }) Assert(SecretDetector.NameReason(Path.Combine(root, filename)).Length > 0, "secret filename " + filename);
            Assert(SecretDetector.NameReason(Path.Combine(root, "credentials-help.md")).Length > 0, "credentials wildcard");
            Assert(SecretDetector.CheckFile(Write(root, "clean.json", "{\"hello\":\"world\"}")).Length == 0, "clean text");
            string detected = SecretDetector.CheckFile(Write(root, "test-secret.txt", "prefix " + secret + " suffix"));
            Assert(detected.Length > 0 && detected.IndexOf(secret, StringComparison.Ordinal) < 0, "secret without disclosure");
            Assert(SecretDetector.CheckFile(Write(root, "boundary.txt", new string('x', 65530) + " " + secret)).Length > 0, "cross chunk token");
            string utf16 = Path.Combine(root, "utf16.txt"); File.WriteAllText(utf16, "value=" + secret, Encoding.Unicode); Assert(SecretDetector.CheckFile(utf16).Length > 0, "UTF16 BOM");
            string utf16NoBom = Path.Combine(root, "utf16-nobom.txt"); File.WriteAllBytes(utf16NoBom, Encoding.Unicode.GetBytes("value=" + secret)); Assert(SecretDetector.CheckFile(utf16NoBom).Length > 0, "UTF16 without BOM");
            string utf16Be = Path.Combine(root, "utf16-be.txt"); File.WriteAllText(utf16Be, "value=" + secret, Encoding.BigEndianUnicode); Assert(SecretDetector.CheckFile(utf16Be).Length > 0, "UTF16 big endian");
            Assert(SecretDetector.CheckFile(Write(root, "github.txt", "ghp_" + new string('Z', 36))).Length > 0, "github token");
            Assert(SecretDetector.CheckFile(Write(root, "aws.txt", "AKIA" + new string('A', 16))).Length > 0, "AWS ID");
            Assert(SecretDetector.CheckFile(Write(root, "private.txt", "-----BEGIN PRIVATE KEY-----")).Length > 0, "private key marker");
            byte[] binary = new byte[10000]; binary[0] = 0; binary[5] = 244; File.WriteAllBytes(Path.Combine(root, "binary.bin"), binary); Assert(SecretDetector.CheckFile(Path.Combine(root, "binary.bin")).Length == 0, "binary ignored");
        }
        private static void Planner() {
            Random random = new Random(20261101);
            for (int iteration = 0; iteration < 80; iteration++) {
                List<long> sizes = new List<long>(); List<string> names = new List<string>();
                int count = iteration + 1;
                for (int i = 0; i < count; i++) { sizes.Add(iteration % 4 == 0 ? 0 : random.Next(0, 28000001)); names.Add("项目/文件-" + i + ".dat"); }
                List<List<int>> volumes = VolumePlanner.Plan(sizes, names); HashSet<int> seen = new HashSet<int>();
                foreach (List<int> volume in volumes) {
                    long bound = 1024;
                    foreach (int i in volume) { Assert(seen.Add(i), "planner duplicated an input"); bound += VolumePlanner.EntryBound(sizes[i], names[i]); }
                    Assert(bound <= Limits.MaxOutputBytes, "planner exceeds 29MB including ZIP overhead");
                }
                Assert(seen.Count == count, "planner lost inputs");
            }
            foreach (long total in new long[] { 0, 1, 28000000, 28000001, 29000000, 58000001, 112000000 }) {
                List<long> parts = VolumePlanner.SplitSizes(total); Assert(parts.Sum() == total, "split sum"); Assert(parts.All(delegate(long b) { return b <= Limits.ZipPayloadBytes; }), "split bound");
            }
            bool rejected = false; try { VolumePlanner.Plan(new long[] { 29000000 }, new string[] { "file" }); } catch (InvalidOperationException) { rejected = true; } Assert(rejected, "raw limit must reserve ZIP metadata");
            rejected = false; try { PackageEngine.SafeArchivePath("../escape.txt"); } catch (IOException) { rejected = true; } Assert(rejected, "zip traversal guard");
            rejected = false; try { LocalPath.Require(@"\\server\share\file.txt"); } catch (IOException) { rejected = true; } Assert(rejected, "UNC refused before file I/O");
            rejected = false; try { LocalPath.Require(@"\\?\UNC\server\share\file.txt"); } catch (IOException) { rejected = true; } Assert(rejected, "extended UNC refused before I/O");
        }
        private static void Packaging(string root) {
            string source = Path.Combine(root, "pack-project");
            string sourceFile = Write(source, "src/main.cs", "class Example { }"); Write(source, "runtime/r.txt", "skip"); Write(source, "models/m.txt", "skip"); Write(source, "bin/app.exe", "skip"); Write(source, ".env", "PRIVATE=example");
            string hash = FileScanner.HashFile(sourceFile);
            AppSettings settings = new AppSettings { OutputDirectory = Path.Combine(root, "outputs") }; settings.Handoff.Project = "测试项目";
            InputItem item = FileScanner.Scan(source, CancellationToken.None);
            PackageResult first = PackageEngine.Run(new List<InputItem> { item }, settings, null, null, CancellationToken.None);
            Assert(first.Files.Count == 2, "ZIP plus handoff"); Assert(first.CopyText.IndexOf("交接说明.md", StringComparison.Ordinal) >= 0, "copy text includes handoff");
            using (ZipArchive zip = ZipFile.OpenRead(first.Files[0].FullPath)) {
                Assert(zip.Entries.Count == 1, "default exclusions actually absent");
                Assert(zip.Entries[0].FullName == "pack-project/src/main.cs", "relative tree preserved");
                using (StreamReader reader = new StreamReader(zip.Entries[0].Open())) Assert(reader.ReadToEnd() == "class Example { }", "source round trip");
            }
            Assert(FileScanner.HashFile(sourceFile) == hash, "original unchanged");
            string handoff = File.ReadAllText(first.Files[1].FullPath); Assert(handoff.IndexOf(first.Files[0].Name, StringComparison.Ordinal) >= 0, "handoff lists actual attachment");
            Assert(handoff.IndexOf(first.Files[0].Length.ToString(), StringComparison.Ordinal) >= 0, "handoff exact bytes");
            settings.Handoff.Next = "继续验证"; PackageEngine.WriteHandoff(first, settings); Assert(File.ReadAllText(first.Files[1].FullPath).Contains("继续验证"), "handoff update after editing");
            settings.Handoff.Goal = "sk-" + new string('d', 32); bool noteBlocked = false;
            try { PackageEngine.WriteHandoff(first, settings); } catch (IOException) { noteBlocked = true; }
            Assert(noteBlocked && !File.ReadAllText(first.Files[1].FullPath).Contains(settings.Handoff.Goal), "handoff secret guard preserves prior note"); settings.Handoff.Goal = "";
            SourceEntry runtime = item.Entries.Single(delegate(SourceEntry e) { return e.RelativePath == "runtime/r.txt"; }); FileScanner.Approve(runtime);
            PackageResult second = PackageEngine.Run(new List<InputItem> { item }, settings, null, null, CancellationToken.None);
            Assert(first.OutputDirectory != second.OutputDirectory && Directory.Exists(first.OutputDirectory), "no overwrite");
            using (ZipArchive zip = ZipFile.OpenRead(second.Files[0].FullPath)) Assert(zip.Entries.Count == 2, "explicit reinclude works");
            SourceEntry env = item.Entries.Single(delegate(SourceEntry e) { return e.RelativePath == ".env"; }); FileScanner.Approve(env);
            File.WriteAllText(env.FullPath, "CHANGED=sk-" + new string('b', 35));
            PackageResult mutated = PackageEngine.Run(new List<InputItem> { item }, settings, null, null, CancellationToken.None);
            using (ZipArchive zip = ZipFile.OpenRead(mutated.Files[0].FullPath)) Assert(!zip.Entries.Any(delegate(ZipArchiveEntry e) { return e.FullName.EndsWith(".env"); }), "mutated approved secret excluded");
            InputItem original = FileScanner.Scan(Write(root, "changed.txt", "benign"), CancellationToken.None);
            File.WriteAllText(original.Path, "now=sk-" + new string('c', 35));
            PackageResult newSecret = PackageEngine.Run(new List<InputItem> { item, original }, settings, null, null, CancellationToken.None);
            using (ZipArchive zip = ZipFile.OpenRead(newSecret.Files[0].FullPath)) Assert(!zip.Entries.Any(delegate(ZipArchiveEntry e) { return e.FullName == "changed.txt"; }), "secret introduced after scan excluded");
            CancellationTokenSource cancelled = new CancellationTokenSource(); cancelled.Cancel(); bool stopped = false;
            try { PackageEngine.Run(new List<InputItem> { item }, settings, null, null, cancelled.Token); } catch (OperationCanceledException) { stopped = true; }
            Assert(stopped, "cancellation respected");
        }
        private static void Settings(string root) {
            string file = Path.Combine(root, "settings.json"); AppSettings settings = new AppSettings { Language = "en", OutputDirectory = "sample" }; settings.Handoff.Progress = "多行\nline 2 \"quoted\"";
            SettingsStore.SaveTo(settings, file); AppSettings restored = SettingsStore.LoadFrom(file); Assert(restored.Language == "en" && restored.Handoff.Progress == settings.Handoff.Progress, "settings round trip");
            settings.Handoff.Goal = "new goal"; SettingsStore.SaveTo(settings, file); Assert(SettingsStore.LoadFrom(file).Handoff.Goal == "new goal", "atomic overwrite settings");
            File.WriteAllText(file, "not json"); Assert(SettingsStore.LoadFrom(file).Handoff != null, "malformed settings recover");
        }
        private static void ActualVolumes(string root) {
            string source = Path.Combine(root, "random-large.dat"); byte[] block = new byte[1048576]; Random random = new Random(5709); long remaining = 58001003;
            using (FileStream file = File.Create(source)) while (remaining > 0) { random.NextBytes(block); int n = (int)Math.Min(remaining, block.Length); file.Write(block, 0, n); remaining -= n; }
            InputItem item = FileScanner.Scan(source, CancellationToken.None);
            AppSettings settings = new AppSettings { OutputDirectory = Path.Combine(root, "split-output"), WriteHandoff = false };
            PackageResult result = PackageEngine.Run(new List<InputItem> { item }, settings, null, null, CancellationToken.None);
            Assert(result.Files.Count(delegate(OutputFile f) { return f.Name.EndsWith(".zip"); }) == 3, "58MB incompressible split into three independent ZIPs");
            Assert(result.Files.All(delegate(OutputFile f) { return f.Length <= Limits.MaxOutputBytes; }), "actual output byte limits");
            Assert(result.Files.Any(delegate(OutputFile f) { return f.Name == "交接说明.md"; }), "forced handoff for splits");
            Assert(result.MergeInstructions.Count == 1 && result.MergeInstructions[0].Contains("CreateNew") && result.MergeInstructions[0].Contains("Get-FileHash"), "safe merge instructions and verification");
            List<string> extracted = new List<string>(); int index = 0;
            foreach (OutputFile output in result.Files) if (output.Name.EndsWith(".zip")) {
                string folder = Path.Combine(root, "extract-" + (++index)); Directory.CreateDirectory(folder); ZipFile.ExtractToDirectory(output.FullPath, folder);
                extracted.AddRange(Directory.GetFiles(folder, "*", SearchOption.AllDirectories));
                using (ZipArchive zip = ZipFile.OpenRead(output.FullPath)) Assert(zip.Entries.Count > 0, "independent readable archive");
            }
            extracted = extracted.OrderBy(delegate(string p) { return Path.GetExtension(p); }, StringComparer.Ordinal).ToList();
            string rebuilt = Path.Combine(root, "rebuilt.dat"); using (FileStream output = File.Create(rebuilt)) foreach (string part in extracted) using (FileStream input = File.OpenRead(part)) input.CopyTo(output);
            Assert(new FileInfo(rebuilt).Length == new FileInfo(source).Length, "split reassembled size"); Assert(FileScanner.HashFile(rebuilt) == FileScanner.HashFile(source), "split SHA256 round trip");
        }
    }
}
