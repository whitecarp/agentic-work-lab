using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using WhaleCommon;

namespace WhalePack.Tests {
    // Offline fixtures only: no application window, user settings, HTTP or FFmpeg.
    public static class RuntimeAndUpdateTests {
        private static int assertions;
        private static void Assert(bool condition, string message) {
            assertions++;
            if (!condition) throw new Exception("Runtime/update: " + message);
        }
        public static int Run() {
            assertions = 0;
            bool value;
            Assert(AppContext.TryGetSwitch("Switch.System.IO.UseLegacyPathHandling", out value) && !value, "modern path handling configured before I/O");
            Assert(AppContext.TryGetSwitch("Switch.System.IO.BlockLongPaths", out value) && !value, "long paths enabled in code");
            Assembly assembly = Assembly.GetExecutingAssembly();
            TargetFrameworkAttribute target = (TargetFrameworkAttribute)Attribute.GetCustomAttribute(assembly, typeof(TargetFrameworkAttribute));
            Assert(target != null && target.FrameworkName == ".NETFramework,Version=v4.8", "Framework 4.8 target embedded in assembly");
            Assert(assembly.GetName().Version == new Version(0, 1, 1, 0), "0.1.1 assembly version");

            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try {
                string directory = root;
                while (directory.Length < 280) directory = Path.Combine(directory, "synthetic-long-path-0123456789");
                Directory.CreateDirectory(directory);
                string file = Path.Combine(directory, "fixture.txt");
                string text = "Synthetic local long-path fixture / 测试";
                File.WriteAllText(file, text, new UTF8Encoding(false));
                Assert(file.Length > 260, "fixture exceeds legacy MAX_PATH");
                Assert(File.ReadAllText(file, Encoding.UTF8) == text, "long-path content round trip without configuration sidecar");
                InputItem scanned = FileScanner.Scan(file, CancellationToken.None);
                Assert(scanned.Entries.Count == 1 && scanned.OriginalBytes == new FileInfo(file).Length, "production scanner reads long-path file");
            } finally {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }

            Assert(UpdateChecker.AssetName("pack") == "WhalePackHelper.exe", "pack maps to single EXE asset");
            string digest = new string('a', 64);
            string url = "https://github.com/" + UpdateChecker.Repository + "/releases/download/v0.5.0-preview/WhalePackHelper.exe";
            UpdateRelease release = UpdateChecker.SelectRelease(ReleaseJson(url, "sha256:" + digest, "WhalePackHelper.exe"), "pack");
            Assert(release != null && release.Version == "0.1.2" && release.AssetName == "WhalePackHelper.exe", "pack uses app version marker and matching asset");
            Assert(release != null && release.Digest == digest && release.Preview, "pack preserves checksum and preview metadata");
            Assert(release != null && UpdateChecker.IsNewer(release, "0.1.1", true), "new pack version offered");
            Assert(UpdateChecker.SelectRelease(ReleaseJson(url, "", "WhalePackHelper.exe"), "pack") == null, "unverified pack release rejected");
            Assert(UpdateChecker.SelectRelease(ReleaseJson("https://example.invalid/WhalePackHelper.exe", "sha256:" + digest, "WhalePackHelper.exe"), "pack") == null, "non-repository asset rejected");
            Assert(UpdateChecker.SelectRelease(ReleaseJson(url, "sha256:" + digest, "WhalePackHelper.exe.config"), "pack") == null, "configuration cannot be selected as update");
            return assertions;
        }
        private static string ReleaseJson(string url, string digest, string assetName) {
            Dictionary<string, object> asset = new Dictionary<string, object> {
                { "name", assetName }, { "state", "uploaded" }, { "size", 233472 },
                { "browser_download_url", url }, { "digest", digest }
            };
            Dictionary<string, object> release = new Dictionary<string, object> {
                { "tag_name", "v0.5.0-preview" }, { "draft", false }, { "prerelease", true },
                { "body", "<!-- whale-app-versions: {\"pack\":\"0.1.2\",\"helper\":\"99.0.0\"} -->" },
                { "assets", new object[] { asset } }
            };
            return new JavaScriptSerializer().Serialize(new object[] { release });
        }
    }
}
