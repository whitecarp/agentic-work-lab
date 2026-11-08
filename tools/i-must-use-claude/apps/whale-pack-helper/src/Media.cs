using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace WhalePack {
    public sealed class VideoSegment {
        public double StartSeconds;
        public double DurationSeconds;
        public int VideoBitrate;
    }
    public sealed class VideoPlan {
        public const long TargetBytes = 27500000;
        public const int MinimumVideoBitrate = 800000;
        public const int AudioBitrate = 128000;
        public List<VideoSegment> Segments = new List<VideoSegment>();
        public static VideoPlan Build(double duration, bool hasAudio) {
            if (Double.IsNaN(duration) || Double.IsInfinity(duration) || duration <= 0)
                throw new ArgumentException("Invalid video duration / 视频时长无效。");
            double bits = TargetBytes * 8.0 * 0.95;
            int audio = hasAudio ? AudioBitrate : 0;
            double maximumDuration = bits / (MinimumVideoBitrate + audio);
            int count = checked((int)Math.Ceiling(duration / maximumDuration));
            if (count > 10000) throw new InvalidOperationException("Video is too long / 视频过长，分段数量超过上限。");
            count = Math.Max(1, count);
            double length = duration / count;
            VideoPlan plan = new VideoPlan();
            for (int i = 0; i < count; i++) {
                double current = i == count - 1 ? duration - i * length : length;
                int bitrate = (int)Math.Min(8000000, Math.Floor(bits / current - audio));
                plan.Segments.Add(new VideoSegment { StartSeconds = i * length, DurationSeconds = current, VideoBitrate = Math.Max(MinimumVideoBitrate, bitrate) });
            }
            return plan;
        }
    }
    public sealed class VideoInfo {
        public int Width;
        public int Height;
        public double Duration;
        public string FrameRate = "";
        public string AverageFrameRate = "";
        public bool HasAudio;
        public string ColorSpace = "";
        public string ColorPrimaries = "";
        public string ColorTransfer = "";
        public string ColorRange = "";
        public int Rotation;
        public double SampleAspectRatio = 1;
    }
    public sealed class MediaProcessor : IMediaProcessor {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        // Embedded media containers only. Playlist demuxers may follow local/UNC references
        // even with HTTP disabled, so HLS, concat, SDP and image sequences are not allowed.
        private const string VideoFormats = "mov,mp4,m4a,3gp,3g2,mj2,matroska,webm,avi,mpeg,mpegts,flv,asf,ogg,h264,hevc,m4v,mxf,nut,rm,dv";

        public static string FindFfmpeg(AppSettings settings) {
            string candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (IsLocalExecutable(candidate)) return candidate;
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string raw in path.Split(Path.PathSeparator)) {
                string directory = raw.Trim().Trim('"');
                if (directory.Length == 0) continue;
                try { candidate = Path.Combine(directory, "ffmpeg.exe"); if (IsLocalExecutable(candidate)) return Path.GetFullPath(candidate); }
                catch (ArgumentException) { }
                catch (NotSupportedException) { }
            }
            if (settings != null && IsLocalExecutable(settings.FfmpegPath)) return Path.GetFullPath(settings.FfmpegPath);
            return null;
        }
        private static bool IsLocalExecutable(string path) {
            return IsLocalPath(path) && File.Exists(path);
        }
        private static bool IsLocalPath(string path) {
            try { LocalPath.Require(path); return true; }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
        private static string NeedFfmpeg(AppSettings settings) {
            string path = FindFfmpeg(settings);
            if (path == null) throw new FileNotFoundException("FFmpeg was not found. Select an existing ffmpeg.exe in Settings; no download is performed. / 未找到 FFmpeg，请在设置中选择本机的 ffmpeg.exe；程序不会下载。");
            return path;
        }
        public List<PreparedFile> Prepare(InputItem item, string workDirectory, AppSettings settings, Action<PackProgress> progress, CancellationToken cancellation) {
            cancellation.ThrowIfCancellationRequested();
            if (item == null || String.IsNullOrWhiteSpace(item.Path)) throw new FileNotFoundException("Media source is missing / 媒体源文件不存在。");
            if (!IsLocalPath(item.Path) || !IsLocalPath(workDirectory)) throw new IOException("Network paths are not supported / 仅支持本机文件，不读取网络路径。");
            if (!File.Exists(item.Path)) throw new FileNotFoundException("Media source is missing / 媒体源文件不存在。");
            Directory.CreateDirectory(workDirectory);
            if (item.Mode == ProcessingMode.CompressVideo) return PrepareVideo(item, workDirectory, settings, progress, cancellation);
            if (item.Mode == ProcessingMode.ResizeImage) return PrepareImage(item, workDirectory, settings, progress, cancellation);
            throw new InvalidOperationException("Unsupported media mode / 此处理方式不属于媒体转换。");
        }

        public static VideoInfo ProbeVideo(string input, string ffmpeg, CancellationToken cancellation) {
            if (!IsLocalPath(input) || !IsLocalExecutable(ffmpeg)) throw new IOException("Only local media and FFmpeg paths are supported / 仅支持本机媒体和 FFmpeg 路径。");
            string probe = Path.Combine(Path.GetDirectoryName(ffmpeg), "ffprobe.exe");
            if (File.Exists(probe)) {
                ProcessResult result = Run(probe, new string[] { "-v", "error", "-protocol_whitelist", "file,pipe", "-format_whitelist", VideoFormats, "-show_streams", "-show_format", "-of", "json", input }, cancellation, null, 0, 0);
                if (result.ExitCode != 0) throw new InvalidOperationException("Cannot read video metadata / 无法读取视频信息。" + SafeError(result.Error));
                Dictionary<string, object> root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(result.Output);
                VideoInfo info = new VideoInfo();
                object streamsObject;
                if (root.TryGetValue("streams", out streamsObject)) {
                    IEnumerable streams = streamsObject as IEnumerable;
                    if (streams != null) foreach (object streamObject in streams) {
                        Dictionary<string, object> stream = streamObject as Dictionary<string, object>;
                        if (stream == null) continue;
                        string type = Get(stream, "codec_type");
                        if (type == "audio") info.HasAudio = true;
                        if (type != "video" || info.Width > 0) continue;
                        info.Width = Integer(Get(stream, "width")); info.Height = Integer(Get(stream, "height"));
                        info.FrameRate = Get(stream, "r_frame_rate"); info.AverageFrameRate = Get(stream, "avg_frame_rate");
                        info.ColorSpace = Get(stream, "color_space"); info.ColorTransfer = Get(stream, "color_transfer");
                        info.ColorPrimaries = Get(stream, "color_primaries"); info.ColorRange = Get(stream, "color_range");
                        info.Duration = Number(Get(stream, "duration"));
                        string[] sar = Get(stream, "sample_aspect_ratio").Split(':');
                        if (sar.Length == 2 && Number(sar[1]) > 0 && Number(sar[0]) > 0) info.SampleAspectRatio = Number(sar[0]) / Number(sar[1]);
                        object tagsObject; if (stream.TryGetValue("tags", out tagsObject)) {
                            Dictionary<string, object> tags = tagsObject as Dictionary<string, object>;
                            if (tags != null) info.Rotation = Integer(Get(tags, "rotate"));
                        }
                        object sideDataObject; if (stream.TryGetValue("side_data_list", out sideDataObject)) {
                            IEnumerable sideData = sideDataObject as IEnumerable;
                            if (sideData != null) foreach (object entry in sideData) {
                                Dictionary<string, object> side = entry as Dictionary<string, object>;
                                if (side != null && side.ContainsKey("rotation")) info.Rotation = Integer(Get(side, "rotation"));
                            }
                        }
                    }
                }
                object formatObject; if (root.TryGetValue("format", out formatObject)) {
                    Dictionary<string, object> format = formatObject as Dictionary<string, object>;
                    if (format != null && info.Duration <= 0) info.Duration = Number(Get(format, "duration"));
                }
                ValidateVideo(info); return info;
            }
            // ffprobe is optional: ffmpeg prints stream metadata before reporting its expected no-output error.
            ProcessResult fallback = Run(ffmpeg, new string[] { "-hide_banner", "-nostdin", "-protocol_whitelist", "file,pipe", "-format_whitelist", VideoFormats, "-i", input }, cancellation, null, 0, 0);
            VideoInfo legacy = new VideoInfo();
            Match duration = Regex.Match(fallback.Error, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
            if (duration.Success) legacy.Duration = Number(duration.Groups[1].Value) * 3600 + Number(duration.Groups[2].Value) * 60 + Number(duration.Groups[3].Value);
            Match video = Regex.Match(fallback.Error, @"Video:[^\r\n]+");
            if (video.Success) {
                Match size = Regex.Match(video.Value, @"\b(\d{2,6})x(\d{2,6})\b");
                if (size.Success) { legacy.Width = Integer(size.Groups[1].Value); legacy.Height = Integer(size.Groups[2].Value); }
                Match fps = Regex.Match(video.Value, @"([\d.]+) fps"); if (fps.Success) legacy.FrameRate = fps.Groups[1].Value;
                legacy.ColorSpace = video.Value.Contains("bt709") ? "bt709" : "";
                if (video.Value.Contains("smpte2084") || video.Value.Contains("arib-std-b67")) legacy.ColorTransfer = "HDR";
                Match sar = Regex.Match(video.Value, @"SAR (\d+):(\d+)");
                if (sar.Success && Number(sar.Groups[2].Value) > 0) legacy.SampleAspectRatio = Number(sar.Groups[1].Value) / Number(sar.Groups[2].Value);
            }
            Match rotation = Regex.Match(fallback.Error, @"rotation of\s+(-?[\d.]+)");
            if (rotation.Success) legacy.Rotation = (int)Math.Round(Number(rotation.Groups[1].Value));
            legacy.HasAudio = fallback.Error.Contains("Audio:"); ValidateVideo(legacy); return legacy;
        }
        private static void ValidateVideo(VideoInfo info) {
            if (info.Width <= 0 || info.Height <= 0 || info.Duration <= 0 || Double.IsInfinity(info.Duration))
                throw new InvalidOperationException("Video has no readable picture or duration / 视频没有可读的画面或时长。");
            if (info.ColorTransfer == "smpte2084" || info.ColorTransfer == "arib-std-b67" || info.ColorTransfer == "HDR")
                throw new InvalidOperationException("HDR video requires a reviewed SDR conversion first; it will not be silently relabeled bt709. / 此视频为 HDR，请先转成 SDR；程序不会直接改标签造成偏色。");
        }
        public static Size OutputSize(VideoInfo info) {
            double width = info.Width * info.SampleAspectRatio, height = info.Height;
            if (Math.Abs(info.Rotation) % 180 == 90) { double swap = width; width = height; height = swap; }
            double factor = Math.Min(1, 720.0 / Math.Min(width, height));
            return new Size(Math.Max(2, (int)Math.Floor(width * factor / 2) * 2), Math.Max(2, (int)Math.Floor(height * factor / 2) * 2));
        }
        public static long EstimateVideoBytes(VideoInfo info) {
            VideoPlan plan = VideoPlan.Build(info.Duration, info.HasAudio); double bytes = 0;
            foreach (VideoSegment segment in plan.Segments) bytes += Math.Min(VideoPlan.TargetBytes, (segment.VideoBitrate + (info.HasAudio ? VideoPlan.AudioBitrate : 0)) * segment.DurationSeconds / 8 * 1.02 + 4096);
            return (long)Math.Ceiling(bytes);
        }
        private static string VideoFilter(VideoInfo info) {
            Size size = OutputSize(info);
            // Convert matrix, primaries and transfer for tagged SDR. Missing HD metadata assumes the usual BT.709.
            string space = Known(info.ColorSpace) ? info.ColorSpace : (info.Height < 720 ? "smpte170m" : "bt709");
            string primaries = Known(info.ColorPrimaries) ? info.ColorPrimaries : "bt709";
            string transfer = Known(info.ColorTransfer) ? info.ColorTransfer : "bt709";
            string range = info.ColorRange == "pc" ? "pc" : "tv";
            return "colorspace=ispace=" + space + ":iprimaries=" + primaries + ":itrc=" + transfer + ":irange=" + range + ":all=bt709:range=tv,scale=" + size.Width + ":" + size.Height + ":flags=lanczos,setsar=1";
        }
        private static bool Known(string value) { return !String.IsNullOrEmpty(value) && value != "unknown" && value != "unspecified"; }
        private List<PreparedFile> PrepareVideo(InputItem item, string work, AppSettings settings, Action<PackProgress> progress, CancellationToken token) {
            string ffmpeg = NeedFfmpeg(settings);
            VideoInfo info = ProbeVideo(item.Path, ffmpeg, token);
            VideoPlan plan = VideoPlan.Build(info.Duration, info.HasAudio);
            List<PreparedFile> files = new List<PreparedFile>();
            string directory = Path.Combine(work, "video-" + Guid.NewGuid().ToString("N").Substring(0, 12)); Directory.CreateDirectory(directory);
            string name = SafeStem(item.Path);
            for (int index = 0; index < plan.Segments.Count; index++) {
                VideoSegment segment = plan.Segments[index];
                string suffix = plan.Segments.Count == 1 ? "" : "-第" + (index + 1).ToString(Invariant) + "段 " + FileTime(segment.StartSeconds) + "–" + FileTime(segment.StartSeconds + segment.DurationSeconds);
                string filename = name + suffix + ".mp4";
                string output = Path.Combine(directory, filename);
                int bitrate = segment.VideoBitrate;
                bool success = false;
                for (int attempt = 0; attempt < 3; attempt++) {
                    string passlog = Path.Combine(directory, "pass-" + index.ToString(Invariant) + "-" + attempt.ToString(Invariant));
                    for (int pass = 1; pass <= 2; pass++) {
                        token.ThrowIfCancellationRequested();
                        string message = (settings.Language == "en" ? "Video part " : "视频第 ") + (index + 1) + " / " + plan.Segments.Count + (settings.Language == "en" ? " · pass " : " 段 · 第 ") + pass + " / 2";
                        List<string> args = new List<string>(new string[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-protocol_whitelist", "file,pipe", "-format_whitelist", VideoFormats, "-ss", D(segment.StartSeconds), "-i", item.Path, "-t", D(segment.DurationSeconds), "-map", "0:v:0", "-map_metadata", "-1", "-map_chapters", "-1", "-vf", VideoFilter(info), "-c:v", "libx264", "-preset", "medium", "-threads", "2", "-b:v", bitrate.ToString(Invariant), "-pix_fmt", "yuv420p", "-vsync", "0", "-pass", pass.ToString(Invariant), "-passlogfile", passlog, "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv", "-progress", "pipe:1", "-nostats" });
                        if (pass == 1) args.AddRange(new string[] { "-an", "-f", "null", "NUL" });
                        else {
                            if (info.HasAudio) args.AddRange(new string[] { "-map", "0:a:0", "-c:a", "aac", "-b:a", VideoPlan.AudioBitrate.ToString(Invariant) }); else args.Add("-an");
                            args.AddRange(new string[] { "-movflags", "+faststart", output });
                        }
                        int basePercent = (index * 2 + pass - 1) * 100 / (plan.Segments.Count * 2);
                        if (progress != null) progress(new PackProgress { Message = message, Percent = basePercent });
                        ProcessResult encoded = Run(ffmpeg, args.ToArray(), token, delegate(double seconds) {
                            if (progress != null) progress(new PackProgress { Message = message, Percent = Math.Min(99, basePercent + (int)(Math.Min(1, seconds / segment.DurationSeconds) * 100 / (plan.Segments.Count * 2))) });
                        }, segment.DurationSeconds, 0);
                        if (encoded.ExitCode != 0) throw new InvalidOperationException("Video encoding failed / 视频压缩失败。" + SafeError(encoded.Error));
                    }
                    long size = new FileInfo(output).Length;
                    if (size > 0 && size <= VideoPlan.TargetBytes) { success = true; break; }
                    if (size <= 0) throw new IOException("Video encoder produced an empty file / 视频编码输出为空。");
                    // Never quietly cross the quality floor during a size retry. The initial
                    // plan reserves container space; a pathological encoder must fail closed.
                    bitrate = Math.Max(VideoPlan.MinimumVideoBitrate, (int)(bitrate * (VideoPlan.TargetBytes * 0.96 / size)));
                }
                if (!success) throw new IOException("Video remains above the size budget after bounded retries / 视频在有限重试后仍超过大小预算，未交付超限文件。");
                files.Add(new PreparedFile { FullPath = output, ArchivePath = filename });
            }
            if (plan.Segments.Count > 1) {
                string timing = Path.Combine(directory, name + "-分段时间.txt");
                StringBuilder note = new StringBuilder("Video segments / 视频分段时间（每段可独立播放）\r\n");
                for (int i = 0; i < plan.Segments.Count; i++) note.Append(files[i].ArchivePath).Append(" : ").Append(Clock(plan.Segments[i].StartSeconds)).Append(" – ").Append(Clock(plan.Segments[i].StartSeconds + plan.Segments[i].DurationSeconds)).Append("\r\n");
                note.Append("Filenames use '-' in place of ':' because Windows filenames cannot contain ':'. / 文件名以短横线代替冒号。\r\n");
                File.WriteAllText(timing, note.ToString(), new UTF8Encoding(false)); files.Add(new PreparedFile { FullPath = timing, ArchivePath = Path.GetFileName(timing) });
            }
            if (progress != null) progress(new PackProgress { Message = settings.Language == "en" ? "Video ready" : "视频处理完成", Percent = 100 });
            return files;
        }
        private List<PreparedFile> PrepareImage(InputItem item, string work, AppSettings settings, Action<PackProgress> progress, CancellationToken token) {
            string directory = Path.Combine(work, "image-" + Guid.NewGuid().ToString("N").Substring(0, 12)); Directory.CreateDirectory(directory);
            string source = item.Path, extension = Path.GetExtension(source).ToLowerInvariant();
            token.ThrowIfCancellationRequested();
            if (extension == ".webp") {
                string decoded = Path.Combine(directory, "decoded.png");
                ProcessResult result = Run(NeedFfmpeg(settings), new string[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-protocol_whitelist", "file,pipe", "-f", "webp_pipe", "-i", source, "-frames:v", "1", decoded }, token, null, 0, 0);
                if (result.ExitCode != 0 || !File.Exists(decoded)) throw new InvalidOperationException("Cannot decode WebP / WebP 图片解码失败。" + SafeError(result.Error));
                source = decoded;
            }
            string output;
            using (Image image = Image.FromFile(source)) {
                ApplyOrientation(image);
                if (image.Width > 100000 || image.Height > 100000 || (long)image.Width * image.Height > 180000000)
                    throw new InvalidOperationException("Image dimensions exceed the safe decode budget / 图片尺寸过大，请先裁剪。");
                Size target = ImageSize(image.Width, image.Height);
                bool alpha = HasVisibleTransparency(image);
                bool png = extension == ".png" || alpha;
                bool largePhoto = new FileInfo(item.Path).Length > 5000000 && !png;
                bool noResize = target.Width == image.Width && target.Height == image.Height;
                if (noResize && !largePhoto && (extension == ".jpg" || extension == ".jpeg" || extension == ".png") && !HasOrientation(image)) {
                    output = Path.Combine(directory, Path.GetFileName(item.Path)); File.Copy(item.Path, output, false);
                } else {
                    string outExtension = png || (extension == ".bmp" && !largePhoto) || (extension == ".webp" && !largePhoto) ? ".png" : ".jpg";
                    output = Path.Combine(directory, SafeStem(item.Path) + outExtension);
                    using (Bitmap bitmap = new Bitmap(target.Width, target.Height, outExtension == ".png" ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb)) {
                        using (Graphics graphics = Graphics.FromImage(bitmap)) {
                            graphics.Clear(outExtension == ".png" ? Color.Transparent : Color.White);
                            graphics.CompositingQuality = CompositingQuality.HighQuality; graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            using (ImageAttributes attributes = new ImageAttributes()) { attributes.SetWrapMode(WrapMode.TileFlipXY); graphics.DrawImage(image, new Rectangle(0, 0, target.Width, target.Height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes); }
                        }
                        if (outExtension == ".png") bitmap.Save(output, ImageFormat.Png); else SaveJpeg(bitmap, output, 90);
                    }
                }
            }
            token.ThrowIfCancellationRequested();
            if (progress != null) progress(new PackProgress { Message = settings.Language == "en" ? "Image ready" : "图片处理完成", Percent = 100 });
            return new List<PreparedFile> { new PreparedFile { FullPath = output, ArchivePath = Path.GetFileName(output) } };
        }
        public static Size ImageSize(int width, int height) {
            if (width <= 0 || height <= 0) throw new ArgumentException("Invalid image dimensions");
            double factor = Math.Min(1, 2560.0 / Math.Max(width, height));
            return new Size(Math.Max(1, (int)Math.Round(width * factor)), Math.Max(1, (int)Math.Round(height * factor)));
        }
        private static void SaveJpeg(Image image, string path, long quality) {
            ImageCodecInfo jpeg = null; foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders()) if (codec.MimeType == "image/jpeg") jpeg = codec;
            if (jpeg == null) throw new InvalidOperationException("JPEG encoder is unavailable");
            using (EncoderParameters parameters = new EncoderParameters(1)) { parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality); image.Save(path, jpeg, parameters); }
        }
        private static bool HasOrientation(Image image) { return Array.IndexOf(image.PropertyIdList, 0x0112) >= 0; }
        private static bool HasVisibleTransparency(Image image) {
            if (!Image.IsAlphaPixelFormat(image.PixelFormat)) return false;
            Bitmap source = image as Bitmap;
            if (source == null) return true;
            BitmapData data = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try {
                byte[] row = new byte[source.Width * 4];
                for (int y = 0; y < source.Height; y++) {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    for (int x = 3; x < row.Length; x += 4) if (row[x] < 255) return true;
                }
                return false;
            } finally { source.UnlockBits(data); }
        }
        private static void ApplyOrientation(Image image) {
            if (!HasOrientation(image)) return;
            try {
                int orientation = image.GetPropertyItem(0x0112).Value[0];
                RotateFlipType[] transforms = new RotateFlipType[] { RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipNone };
                if (orientation >= 1 && orientation <= 8) image.RotateFlip(transforms[orientation]);
                // Keep property so the caller knows a re-encode is necessary, but it is not copied into the new bitmap.
            } catch (ArgumentException) { }
        }
        private static string SafeStem(string path) {
            string value = Path.GetFileNameWithoutExtension(path); foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            if (value.Length > 80) value = value.Substring(0, 80);
            return value.Length == 0 ? "media" : value;
        }
        private static string Clock(double seconds) { TimeSpan t = TimeSpan.FromSeconds(seconds); return ((int)t.TotalHours).ToString("00", Invariant) + ":" + t.Minutes.ToString("00", Invariant) + ":" + t.Seconds.ToString("00", Invariant) + "." + t.Milliseconds.ToString("000", Invariant); }
        private static string FileTime(double seconds) { return Clock(seconds).Replace(':', '-'); }
        private static string D(double number) { return number.ToString("0.######", Invariant); }
        private static int Integer(string value) { int result; return Int32.TryParse(value, NumberStyles.Integer, Invariant, out result) ? result : 0; }
        private static double Number(string value) { double result; return Double.TryParse(value, NumberStyles.Float, Invariant, out result) ? result : 0; }
        private static string Get(Dictionary<string, object> dictionary, string key) { object value; return dictionary.TryGetValue(key, out value) && value != null ? Convert.ToString(value, Invariant) : ""; }
        private static string SafeError(string value) {
            // Do not echo arbitrary source metadata, filenames, or stderr into the user-facing report.
            if (value.IndexOf("Unknown encoder", StringComparison.OrdinalIgnoreCase) >= 0) return " FFmpeg needs libx264 and AAC support. / FFmpeg 需要 libx264 与 AAC 编码器。";
            if (value.IndexOf("No such filter", StringComparison.OrdinalIgnoreCase) >= 0) return " FFmpeg needs the colorspace and scale filters. / FFmpeg 缺少 colorspace 或 scale 滤镜。";
            return " Check that the file is readable and FFmpeg supports its codec. / 请检查文件是否可读，以及 FFmpeg 是否支持其编码。";
        }
        public static string QuoteArgument(string value) {
            if (value.IndexOf('\0') >= 0) throw new ArgumentException("Invalid argument");
            StringBuilder result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value) {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { result.Append('\\', slashes * 2 + 1).Append('"'); slashes = 0; continue; }
                result.Append('\\', slashes).Append(c); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
        private sealed class ProcessResult { public int ExitCode; public string Output = ""; public string Error = ""; }
        private static ProcessResult Run(string executable, string[] arguments, CancellationToken token, Action<double> progress, double duration, int unused) {
            StringBuilder command = new StringBuilder(); foreach (string argument in arguments) { if (command.Length > 0) command.Append(' '); command.Append(QuoteArgument(argument)); }
            StringBuilder output = new StringBuilder(), error = new StringBuilder(); object sync = new object();
            using (Process process = new Process()) {
                process.StartInfo = new ProcessStartInfo { FileName = executable, Arguments = command.ToString(), UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) {
                    if (e.Data == null) return;
                    lock (sync) { if (output.Length < 2000000) output.AppendLine(e.Data); }
                    if (progress != null && e.Data.StartsWith("out_time_us=", StringComparison.Ordinal)) { double time = Number(e.Data.Substring(12)) / 1000000; try { progress(time); } catch (ObjectDisposedException) { } }
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (sync) { if (error.Length < 2000000) error.AppendLine(e.Data); } };
                token.ThrowIfCancellationRequested(); process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                Stopwatch timer = Stopwatch.StartNew(); double timeout = duration <= 0 ? 90 : Math.Max(180, Math.Min(21600, duration * 40));
                while (!process.WaitForExit(150)) {
                    if (token.IsCancellationRequested || timer.Elapsed.TotalSeconds > timeout) {
                        try { process.Kill(); process.WaitForExit(5000); } catch (InvalidOperationException) { }
                        token.ThrowIfCancellationRequested(); throw new TimeoutException("Media processing exceeded its bounded time limit / 媒体处理超时，已停止该进程。");
                    }
                }
                process.WaitForExit(); token.ThrowIfCancellationRequested();
                return new ProcessResult { ExitCode = process.ExitCode, Output = output.ToString(), Error = error.ToString() };
            }
        }
    }
}
