using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WhalePack.Tests {
    public static class ScreenshotTests {
        public static int Run(string[] args) {
            if (args.Length < 3) throw new ArgumentException("--screenshots <integration-directory> <output-directory>");
            var serializer = new JavaScriptSerializer { MaxJsonLength = 200000000 };
            var items = serializer.Deserialize<List<InputItem>>(File.ReadAllText(System.IO.Path.Combine(args[1], "ui-items.json"), Encoding.UTF8));
            var result = serializer.Deserialize<PackageResult>(File.ReadAllText(System.IO.Path.Combine(args[1], "ui-result.json"), Encoding.UTF8));
            string metadataPath = System.IO.Path.Combine(args[1], "video-info.json");
            var videoInfo = File.Exists(metadataPath) ? serializer.Deserialize<Dictionary<string, VideoInfo>>(File.ReadAllText(metadataPath, Encoding.UTF8)) : new Dictionary<string, VideoInfo>();
            Directory.CreateDirectory(args[2]);
            List<object> manifest = new List<object>();
            bool native = args.Length > 3 && args[3] == "--native";
            foreach (string language in new string[] { "zh", "en" }) foreach (float scale in (native ? new float[] { 1f } : new float[] { 1f, 1.5f })) {
                var settings = new AppSettings { Language = language, OutputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\发给Claude", Handoff = new HandoffData {
                    Project = "大肥鱼打包助手 0.1.1", Goal = language == "zh" ? "整理发给 Claude 的文件" : "Prepare a local handoff for Claude", Progress = language == "zh" ? "三组文件已完成打包" : "Three packaging scenarios completed", Verified = language == "zh" ? "实际大小、独立解压、敏感文件排除" : "Actual sizes, extraction, secret exclusions", Unverified = language == "zh" ? "Claude 上传（未执行上传）" : "Claude uploads (not performed)", Next = language == "zh" ? "先读说明，再看附件" : "Read the handoff, then the attachments" } };
                foreach (string state in new string[] { "empty", "loaded", "completed", "handoff" }) {
                    Form form;
                    if (state == "handoff") form = new HandoffForm(settings, result) { PreviewOnly = true };
                    else {
                        var main = new MainForm(settings) { PreviewOnly = true };
                        if (state != "empty") {
                            main.SetItemsForPreview(items);
                            foreach (KeyValuePair<string, VideoInfo> pair in videoInfo) main.SetVideoInfoForPreview(pair.Key, pair.Value);
                        }
                        if (state == "completed") main.SetResultForPreview(result); form = main;
                    }
                    using (form) {
                        float effectiveScale = native ? form.ClientSize.Width / (state == "handoff" ? 520f : 560f) : scale;
                        if (!native) form.AutoScaleMode = AutoScaleMode.None;
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = new Point(-20000, -20000);
                        form.ShowInTaskbar = false;
                        // A deterministic native-control render at 96/144 logical DPI. Does not change user's Windows display settings.
                        if (!native && scale != 1f) {
                            var fonts = new Dictionary<Control, Font>();
                            CaptureFonts(form, fonts);
                            form.Scale(new SizeF(scale, scale));
                            foreach (KeyValuePair<Control, Font> pair in fonts) pair.Key.Font = new Font(pair.Value.FontFamily, pair.Value.SizeInPoints * scale, pair.Value.Style, GraphicsUnit.Point);
                        }
                        form.Show();
                        form.PerformLayout();
                        Application.DoEvents();
                        string filename = language + "-" + ((int)Math.Round(effectiveScale * 100)) + "-" + state + ".png";
                        using (Bitmap bitmap = new Bitmap(form.Width, form.Height)) {
                            form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                            bitmap.Save(System.IO.Path.Combine(args[2], filename), ImageFormat.Png);
                        }
                        manifest.Add(new { name = filename, width = form.Width, height = form.Height, scale = effectiveScale, language = language, state = state, method = native ? "DPI-aware native WinForms controls at actual Windows DPI; offscreen DrawToBitmap; no global display-setting change" : "WinForms native-control DrawToBitmap with explicit logical scale; not a Windows display-setting switch" });
                        form.Close();
                    }
                }
            }
            File.WriteAllText(System.IO.Path.Combine(args[2], "manifest.json"), serializer.Serialize(manifest), Encoding.UTF8);
            Console.WriteLine("PASS: " + manifest.Count + " WinForms renders; mode=" + (native ? "native Windows DPI" : "96/144 logical DPI"));
            return 0;
        }
        private static void CaptureFonts(Control control, Dictionary<Control, Font> fonts) {
            fonts.Add(control, control.Font);
            foreach (Control child in control.Controls) CaptureFonts(child, fonts);
        }
    }
}
