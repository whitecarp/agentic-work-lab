using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace WhalePack.Tests {
    public static class MediaTests {
        private static int count;
        private static void Assert(bool condition, string name) { count++; if (!condition) throw new Exception("Media test failed: " + name); }
        public static int Run() {
            count = 0;
            foreach (bool audio in new bool[] { false, true }) {
                foreach (double duration in new double[] { 0.1, 1, 40, 60.733333, 200, 225, 226, 300, 600, 3600, 86400 }) {
                    VideoPlan plan = VideoPlan.Build(duration, audio); double total = 0;
                    Assert(plan.Segments.Count >= 1, "At least one segment");
                    foreach (VideoSegment segment in plan.Segments) {
                        Assert(segment.VideoBitrate >= 800000, "Minimum bitrate respected");
                        Assert(Math.Abs(segment.StartSeconds - total) < 0.0001, "Contiguous cuts");
                        total += segment.DurationSeconds;
                        double estimate = (segment.VideoBitrate + (audio ? 128000 : 0)) * segment.DurationSeconds / 8;
                        Assert(estimate <= VideoPlan.TargetBytes * 0.951, "Conservative video byte budget");
                    }
                    Assert(Math.Abs(total - duration) < 0.0001, "No omitted or duplicated duration");
                }
            }
            Assert(VideoPlan.Build(60, true).Segments.Count == 1, "One-minute video remains one file");
            Assert(VideoPlan.Build(600, true).Segments.Count > 1, "Low bitrate video splits");
            bool rejected = false; try { VideoPlan.Build(Double.NaN, true); } catch (ArgumentException) { rejected = true; } Assert(rejected, "Reject NaN duration");
            rejected = false; try { VideoPlan.Build(0, false); } catch (ArgumentException) { rejected = true; } Assert(rejected, "Reject zero duration");
            Assert(MediaProcessor.OutputSize(new VideoInfo { Width = 1080, Height = 1920 }) == new Size(720, 1280), "Portrait resize");
            Assert(MediaProcessor.OutputSize(new VideoInfo { Width = 1920, Height = 1080 }) == new Size(1280, 720), "Landscape resize");
            Assert(MediaProcessor.OutputSize(new VideoInfo { Width = 640, Height = 360 }) == new Size(640, 360), "No upscale");
            Assert(MediaProcessor.OutputSize(new VideoInfo { Width = 1920, Height = 1080, Rotation = -90 }) == new Size(720, 1280), "Rotation-aware dimensions");
            Assert(MediaProcessor.ImageSize(4000, 3000) == new Size(2560, 1920), "Image longest edge");
            Assert(MediaProcessor.ImageSize(800, 1200) == new Size(800, 1200), "Image no upscale");
            Assert(MediaProcessor.QuoteArgument("a b") == "\"a b\"", "Space quoting");
            Assert(MediaProcessor.QuoteArgument("a\"b") == "\"a\\\"b\"", "Quote escaping");
            Assert(MediaProcessor.QuoteArgument("C:\\test\\") == "\"C:\\test\\\\\"", "Trailing slash escaping");
            string testDirectory = Path.Combine(Path.GetTempPath(), "whale-pack-image-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            try {
                string source = Path.Combine(testDirectory, "screenshot.png");
                using (Bitmap image = new Bitmap(3000, 1500, PixelFormat.Format32bppArgb)) {
                    using (Graphics graphics = Graphics.FromImage(image)) { graphics.Clear(Color.Transparent); using (Brush brush = new SolidBrush(Color.FromArgb(255, 30, 140, 200))) graphics.FillRectangle(brush, 100, 100, 1500, 1000); }
                    image.Save(source, ImageFormat.Png);
                }
                List<PreparedFile> result = new MediaProcessor().Prepare(new InputItem { Path = source, Mode = ProcessingMode.ResizeImage }, testDirectory, new AppSettings(), null, CancellationToken.None);
                Assert(result.Count == 1 && result[0].ArchivePath.EndsWith(".png"), "PNG screenshot remains PNG");
                using (Bitmap output = new Bitmap(result[0].FullPath)) {
                    Assert(output.Width == 2560 && output.Height == 1280, "Actual resized PNG dimensions");
                    Assert(output.GetPixel(0, 0).A == 0, "PNG alpha retained");
                }
                Assert(File.Exists(source), "Original remains");
                string photo = Path.Combine(testDirectory, "large-photo.bmp");
                using (Bitmap image = new Bitmap(1800, 1200, PixelFormat.Format24bppRgb)) { using (Graphics graphics = Graphics.FromImage(image)) graphics.Clear(Color.CornflowerBlue); image.Save(photo, ImageFormat.Bmp); }
                Assert(new FileInfo(photo).Length > 5000000, "Photo fixture above 5MB");
                result = new MediaProcessor().Prepare(new InputItem { Path = photo, Mode = ProcessingMode.ResizeImage }, testDirectory, new AppSettings(), null, CancellationToken.None);
                Assert(result[0].ArchivePath.EndsWith(".jpg"), "Large photo converts JPEG");
                using (Image output = Image.FromFile(result[0].FullPath)) Assert(output.Width == 1800 && output.Height == 1200, "Photo dimensions retained");
                CancellationTokenSource canceled = new CancellationTokenSource(); canceled.Cancel(); bool canceledObserved = false;
                try { new MediaProcessor().Prepare(new InputItem { Path = source, Mode = ProcessingMode.ResizeImage }, testDirectory, new AppSettings(), null, canceled.Token); } catch (OperationCanceledException) { canceledObserved = true; }
                Assert(canceledObserved, "Cancellation respected");
            } finally { if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true); }
            return count;
        }
    }
}
