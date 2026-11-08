using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace WhalePack.Tests {
    public static class IntegrationTests {
        public static int Run(string[] args) {
            if (args.Length < 4) throw new ArgumentException("--integration <ffmpeg.exe> <vertical-video.mp4> <artifact-directory>");
            string ffmpeg = System.IO.Path.GetFullPath(args[1]);
            string video = System.IO.Path.GetFullPath(args[2]);
            string root = System.IO.Path.GetFullPath(args[3]);
            Directory.CreateDirectory(root);
            string source = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "claude-live-translator"));
            string fixtures = System.IO.Path.Combine(root, "fixtures");
            Directory.CreateDirectory(fixtures);
            string secret = System.IO.Path.Combine(fixtures, ".env");
            File.WriteAllText(secret, "TEST_ONLY_NOT_A_REAL_CREDENTIAL=example\n", Encoding.UTF8);
            string shot = System.IO.Path.Combine(fixtures, "Screenshot-3200.png");
            using (Bitmap bitmap = new Bitmap(3200, 1800)) {
                using (Graphics g = Graphics.FromImage(bitmap)) {
                    g.Clear(Color.FromArgb(250, 248, 242));
                    using (Brush blue = new SolidBrush(Color.FromArgb(76, 151, 196))) g.FillRectangle(blue, 100, 100, 2900, 240);
                    using (Font f = new Font("Microsoft YaHei UI", 46)) g.DrawString("大肥鱼打包助手 · PNG screenshot test", f, Brushes.White, 160, 170);
                    for (int y = 450; y < 1700; y += 120) g.DrawLine(Pens.SlateGray, 100, y, 3000, y);
                }
                bitmap.Save(shot, ImageFormat.Png);
            }
            List<InputItem> items = new List<InputItem>();
            items.Add(FileScanner.Scan(video, CancellationToken.None));
            items.Add(FileScanner.Scan(source, CancellationToken.None));
            items.Add(FileScanner.Scan(shot, CancellationToken.None));
            items.Add(FileScanner.Scan(secret, CancellationToken.None));
            if (items[3].Mode != ProcessingMode.Exclude) throw new Exception("Fake .env was not excluded.");
            var serializer = new JavaScriptSerializer { MaxJsonLength = 200000000 };
            File.WriteAllText(System.IO.Path.Combine(root, "video-info.json"), serializer.Serialize(new Dictionary<string, VideoInfo> { { video, MediaProcessor.ProbeVideo(video, ffmpeg, CancellationToken.None) } }), Encoding.UTF8);
            string originalVideoHash = Hash(video);
            var rows = new List<object>();
            PackageResult mixed = null;
            foreach (string scenario in new string[] { "video", "source", "mixed" }) {
                Console.WriteLine("INTEGRATION: " + scenario);
                var selected = scenario == "video" ? new List<InputItem> { items[0] } : scenario == "source" ? new List<InputItem> { items[1] } : items;
                var settings = new AppSettings { OutputDirectory = System.IO.Path.Combine(root, scenario), FfmpegPath = ffmpeg, WriteHandoff = true,
                    Handoff = new HandoffData { Project = "大肥鱼打包助手 0.1.1", Goal = "本机压缩、筛查与交接", Progress = "真实文件集成测试", Verified = "输出 ZIP 实际大小及独立解压、默认排除检查", Unverified = "Claude 上传（未执行上传）", Next = "阅读交接说明与附件" } };
                int last = -10;
                PackageResult result = PackageEngine.Run(selected, settings, new MediaProcessor(), delegate(PackProgress p) { if (p.Percent >= last + 10) { Console.WriteLine("  " + p.Percent + "%"); last = p.Percent; } }, CancellationToken.None);
                int count = 0;
                List<object> outputs = new List<object>();
                foreach (OutputFile file in result.Files) {
                    long length = new FileInfo(file.FullPath).Length;
                    if (length > Limits.MaxOutputBytes) throw new Exception("Oversize output: " + file.Name);
                    outputs.Add(new { name = file.Name, bytes = length, sha256 = Hash(file.FullPath) });
                    if (!file.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                    string extraction = System.IO.Path.Combine(root, "extracted", scenario, System.IO.Path.GetFileNameWithoutExtension(file.Name));
                    Directory.CreateDirectory(extraction);
                    using (ZipArchive zip = ZipFile.OpenRead(file.FullPath)) {
                        foreach (ZipArchiveEntry entry in zip.Entries) {
                            string normalized = entry.FullName.Replace('\\', '/');
                            string[] parts = normalized.Split('/');
                            if (parts.Any(delegate(string p) { return p.Equals(".env", StringComparison.OrdinalIgnoreCase) || p.Equals("runtime", StringComparison.OrdinalIgnoreCase) || p.Equals("models", StringComparison.OrdinalIgnoreCase); }) || normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new Exception("Excluded input appeared in archive.");
                            count++;
                            using (Stream stream = entry.Open()) { byte[] buffer = new byte[81920]; while (stream.Read(buffer, 0, buffer.Length) > 0) { } }
                        }
                    }
                    ZipFile.ExtractToDirectory(file.FullPath, extraction);
                }
                if (count == 0) throw new Exception("Empty integration archive.");
                if (!File.Exists(System.IO.Path.Combine(result.OutputDirectory, "交接说明.md"))) throw new Exception("Missing handoff.");
                rows.Add(new { scenario = scenario, files = outputs, zipEntryCount = count, independentlyExtracted = true, actualLimitsChecked = true });
                if (scenario == "mixed") mixed = result;
            }
            if (Hash(video) != originalVideoHash) throw new Exception("Input video changed.");
            File.WriteAllText(System.IO.Path.Combine(root, "ui-items.json"), serializer.Serialize(items), Encoding.UTF8);
            File.WriteAllText(System.IO.Path.Combine(root, "ui-result.json"), serializer.Serialize(mixed), Encoding.UTF8);
            File.WriteAllText(System.IO.Path.Combine(root, "results.json"), serializer.Serialize(new {
                dateUtc = DateTime.UtcNow.ToString("o"), videoInputBytes = new FileInfo(video).Length, originalVideoUnchanged = true,
                source = new { entries = items[1].Entries.Count, included = items[1].Entries.Count(delegate(SourceEntry e) { return !e.Excluded; }), excluded = items[1].Entries.Count(delegate(SourceEntry e) { return e.Excluded; }) },
                fakeEnvExcluded = true, tests = rows
            }), Encoding.UTF8);
            Console.WriteLine("PASS: all three integration scenarios and extraction checks.");
            return 0;
        }
        private static string Hash(string path) { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
    }
}
