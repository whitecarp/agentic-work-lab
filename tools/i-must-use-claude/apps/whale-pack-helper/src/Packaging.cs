using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace WhalePack {
    public static class VolumePlanner {
        public static long EntryBound(long bytes, string name) {
            if (bytes < 0) throw new ArgumentOutOfRangeException("bytes");
            return checked(bytes + bytes / 1000 + 4096 + Encoding.UTF8.GetByteCount(name ?? "") * 4L);
        }
        public static List<List<int>> Plan(IList<long> sizes, IList<string> names) {
            if (sizes.Count != names.Count) throw new ArgumentException("Names and sizes do not match.");
            List<List<int>> groups = new List<List<int>>(); List<long> used = new List<long>();
            List<int> order = Enumerable.Range(0, sizes.Count).OrderByDescending(delegate(int i) { return sizes[i]; }).ToList();
            foreach (int i in order) {
                long bound = EntryBound(sizes[i], names[i]);
                if (bound > Limits.MaxOutputBytes - 1024) throw new InvalidOperationException("文件需要先切片 / File must be split before planning.");
                int group = -1;
                for (int j = 0; j < groups.Count; j++) if (used[j] + bound <= Limits.MaxOutputBytes - 1024) { group = j; break; }
                if (group < 0) { group = groups.Count; groups.Add(new List<int>()); used.Add(0); }
                groups[group].Add(i); used[group] += bound;
            }
            return groups;
        }
        public static List<long> SplitSizes(long bytes) {
            if (bytes < 0) throw new ArgumentOutOfRangeException("bytes");
            List<long> parts = new List<long>();
            if (bytes == 0) { parts.Add(0); return parts; }
            while (bytes > 0) { long part = Math.Min(bytes, Limits.ZipPayloadBytes); parts.Add(part); bytes -= part; }
            return parts;
        }
    }
    public static class PackageEngine {
        private static string Txt(AppSettings settings, string zh, string en) { return settings.Language == "en" ? en : zh; }
        private static void Report(Action<PackProgress> progress, string text, int percent) { if (progress != null) progress(new PackProgress { Message = text, Percent = percent }); }
        public static PackageResult Run(List<InputItem> items, AppSettings settings, IMediaProcessor media, Action<PackProgress> progress, CancellationToken cancellation) {
            if (items == null || items.Count == 0) throw new InvalidOperationException(Txt(settings, "请先添加文件或文件夹。", "Add files or folders first."));
            if (String.IsNullOrWhiteSpace(settings.OutputDirectory)) throw new InvalidOperationException(Txt(settings, "请选择输出位置。", "Choose an output folder."));
            string baseOutput = LocalPath.Require(settings.OutputDirectory);
            if (FileScanner.HasReparseAncestor(baseOutput)) throw new IOException(Txt(settings, "输出位置不能是链接目录。", "Output cannot use a linked folder."));
            Directory.CreateDirectory(baseOutput);
            string runRoot = Path.Combine(baseOutput, "打包-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(runRoot);
            // Keep staging short: FFmpeg supports long Unicode paths, but .NET Framework file
            // APIs can still reject deeply nested paths before the application manifest loads.
            string workParent = LocalPath.Require(Path.Combine(Path.GetTempPath(), "WhalePack-work")); Directory.CreateDirectory(workParent);
            string work = LocalPath.Require(Path.Combine(workParent, Guid.NewGuid().ToString("N").Substring(0, 12))); Directory.CreateDirectory(work);
            PackageResult result = new PackageResult { OutputDirectory = runRoot };
            List<PreparedFile> prepared = new List<PreparedFile>();
            HashSet<string> roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int copied = 0, excludedCount = 0; bool complete = false;
            try {
                for (int index = 0; index < items.Count; index++) {
                    cancellation.ThrowIfCancellationRequested(); InputItem item = items[index];
                    if (item.Mode == ProcessingMode.Exclude) { excludedCount += item.Entries.Count; AddWarning(result, item.DisplayName + " — " + Txt(settings, "已排除", "Excluded")); continue; }
                    if (item.IsDirectory && (item.Mode == ProcessingMode.CompressVideo || item.Mode == ProcessingMode.ResizeImage)) throw new InvalidOperationException(Txt(settings, "文件夹请选择“打包源码”或“原样”。", "Choose Source archive or Original for a folder."));
                    string rootName = UniqueName(SafeName(item.DisplayName), roots);
                    if (item.Entries.Count == 0) AddWarning(result, rootName + " — " + Txt(settings, "空文件夹或没有可读取文件", "Empty folder or no readable files"));
                    foreach (SourceEntry entry in item.Entries) {
                        cancellation.ThrowIfCancellationRequested();
                        if (entry.Excluded || entry.CannotInclude || entry.IsDirectory) { excludedCount++; AddWarning(result, entry.RelativePath + " — " + entry.Reason); continue; }
                        if (FileScanner.HasReparseAncestor(entry.FullPath)) throw new IOException(Txt(settings, "输入路径已变成链接，已停止：", "Input became a linked path; stopped: ") + entry.RelativePath);
                        string relative = item.IsDirectory ? rootName + "/" + SafeArchivePath(entry.RelativePath) : rootName;
                        string inputFolder = Path.Combine(work, "input-" + (++copied).ToString("D6", CultureInfo.InvariantCulture)); Directory.CreateDirectory(inputFolder);
                        string staged = Path.Combine(inputFolder, SafeName(Path.GetFileName(entry.FullPath)));
                        Report(progress, Txt(settings, "核对并整理：", "Checking: ") + relative, Math.Min(60, index * 60 / Math.Max(1, items.Count)));
                        CopyLocal(entry.FullPath, staged, cancellation);
                        // Check staged bytes, not just an old scan. Re-inclusion is valid only for exactly reviewed bytes.
                        string secret = SecretDetector.CheckFile(staged, cancellation);
                        string sourceNameReason = SecretDetector.NameReason(entry.FullPath);
                        if (sourceNameReason.Length > 0) secret = sourceNameReason;
                        bool approvalRequired = entry.UserIncluded || entry.Sensitive || secret.Length > 0 || entry.Reason.Length > 0;
                        if (approvalRequired && !FileScanner.ApprovedContent(entry, staged)) {
                            File.Delete(staged); excludedCount++;
                            AddWarning(result, entry.RelativePath + " — " + Txt(settings, "复核时排除：需要重新查看并确认", "Excluded on recheck: review and confirm this version")); continue;
                        }
                        if (!item.IsDirectory && (item.Mode == ProcessingMode.CompressVideo || item.Mode == ProcessingMode.ResizeImage)) {
                            if (media == null) throw new InvalidOperationException("媒体处理器不可用 / Media processor unavailable.");
                            InputItem safeItem = new InputItem { Path = staged, DisplayName = rootName, OriginalBytes = new FileInfo(staged).Length, Mode = item.Mode };
                            safeItem.Entries.Add(new SourceEntry { FullPath = staged, RelativePath = rootName, Length = safeItem.OriginalBytes });
                            string mediaWork = Path.Combine(inputFolder, "processed"); Directory.CreateDirectory(mediaWork);
                            int stageIndex = index;
                            int stagePeak = stageIndex * 60 / Math.Max(1, items.Count);
                            Action<PackProgress> mediaProgress = delegate(PackProgress p) {
                                int overall = (stageIndex * 60 + Math.Max(0, Math.Min(100, p.Percent)) * 60 / 100) / Math.Max(1, items.Count);
                                stagePeak = Math.Max(stagePeak, overall);
                                Report(progress, p.Message, stagePeak);
                            };
                            List<PreparedFile> outputs = media.Prepare(safeItem, mediaWork, settings, mediaProgress, cancellation);
                            if (outputs == null || outputs.Count == 0) throw new IOException("媒体处理未生成文件 / Media processing produced no file.");
                            foreach (PreparedFile output in outputs) {
                                if (!IsWithin(output.FullPath, inputFolder)) throw new IOException("媒体输出超出工作目录 / Media output outside staging folder.");
                                prepared.Add(new PreparedFile { FullPath = output.FullPath, ArchivePath = SafeArchivePath(output.ArchivePath) });
                            }
                        } else prepared.Add(new PreparedFile { FullPath = staged, ArchivePath = relative });
                    }
                }
                if (prepared.Count == 0) throw new InvalidOperationException(Txt(settings, "没有可打包的文件。请查看排除明细。", "No files remain to package. Review excluded items."));
                if (excludedCount > 0) AddWarning(result, Txt(settings, "共排除 ", "Excluded ") + excludedCount.ToString(CultureInfo.InvariantCulture) + Txt(settings, " 项。", " entries."));
                prepared = EnsureUniqueArchiveNames(prepared);
                List<PreparedFile> payload = new List<PreparedFile>(); int splitIndex = 0;
                foreach (PreparedFile file in prepared) {
                    cancellation.ThrowIfCancellationRequested();
                    long size = new FileInfo(file.FullPath).Length;
                    if (size <= Limits.MaxOutputBytes && VolumePlanner.EntryBound(size, file.ArchivePath) <= Limits.MaxOutputBytes - 1024) { payload.Add(file); continue; }
                    string splitPath = "__split_" + (++splitIndex).ToString("D3", CultureInfo.InvariantCulture) + "/" + file.ArchivePath;
                    List<long> sizes = VolumePlanner.SplitSizes(size);
                    string splitWork = Path.Combine(work, "split-" + splitIndex.ToString(CultureInfo.InvariantCulture)); Directory.CreateDirectory(splitWork);
                    using (FileStream input = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                        for (int part = 0; part < sizes.Count; part++) {
                            string suffix = "." + (part + 1).ToString("D3", CultureInfo.InvariantCulture);
                            string output = Path.Combine(splitWork, "part" + suffix);
                            using (FileStream target = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None)) CopyCount(input, target, sizes[part], cancellation);
                            payload.Add(new PreparedFile { FullPath = output, ArchivePath = splitPath + suffix });
                        }
                    }
                    result.MergeInstructions.Add(MergeText(splitPath, sizes.Count, size, FileScanner.HashFile(file.FullPath), settings));
                }
                List<long> lengths = payload.Select(delegate(PreparedFile f) { return new FileInfo(f.FullPath).Length; }).ToList();
                List<string> names = payload.Select(delegate(PreparedFile f) { return f.ArchivePath; }).ToList();
                List<List<int>> planned = VolumePlanner.Plan(lengths, names);
                List<List<PreparedFile>> groups = new List<List<PreparedFile>>();
                foreach (List<int> indices in planned) groups.Add(indices.Select(delegate(int i) { return payload[i]; }).ToList());
                int sequence = 1;
                foreach (List<PreparedFile> group in groups) WriteVolume(group, runRoot, ref sequence, result, settings, progress, cancellation);
                bool writeHandoff = settings.WriteHandoff || result.MergeInstructions.Count > 0;
                if (!settings.WriteHandoff && writeHandoff) AddWarning(result, Txt(settings, "包含原样切片，已自动写交接说明以保留合并方法。", "Handoff was written because split files require merge instructions."));
                if (writeHandoff) WriteHandoff(result, settings);
                string attachments = String.Join("、", result.Files.Where(delegate(OutputFile f) { return f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase); }).Select(delegate(OutputFile f) { return f.Name; }));
                result.CopyText = writeHandoff ? "请先读「交接说明.md」，再看附件 " + attachments + "。" : "请查看附件 " + attachments + "。";
                foreach (OutputFile output in result.Files) if (new FileInfo(output.FullPath).Length > Limits.MaxOutputBytes) throw new IOException("最终文件超过大小上限 / Final output exceeds size limit.");
                DeleteOwnedDirectory(work, workParent);
                complete = true;
                Report(progress, Txt(settings, "打包完成，每个文件已核对大小。", "Done. Every output size has been checked."), 100);
                return result;
            } finally {
                if (Directory.Exists(work)) { try { DeleteOwnedDirectory(work, workParent); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
                if (!complete && Directory.Exists(runRoot)) { try { DeleteOwnedDirectory(runRoot, baseOutput); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            }
        }
        private static void DeleteOwnedDirectory(string path, string parent) {
            string full = LocalPath.Require(path);
            if (!IsWithin(full, parent) || String.Equals(full.TrimEnd('\\'), Path.GetFullPath(parent).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) throw new IOException("工作目录清理边界不匹配 / Cleanup boundary mismatch.");
            Directory.Delete(full, true);
        }
        public static void WriteHandoff(PackageResult result, AppSettings settings) {
            string directory = LocalPath.Require(result.OutputDirectory);
            if (!Directory.Exists(directory) || FileScanner.HasReparseAncestor(directory)) throw new IOException("输出目录不可用 / Output folder is unavailable.");
            string handoff = Path.Combine(directory, "交接说明.md");
            if (File.Exists(handoff) && FileScanner.HasReparseAncestor(handoff)) throw new IOException("交接文件不可为链接 / Handoff cannot be a linked file.");
            foreach (OutputFile file in result.Files) if (file.Name != "交接说明.md") {
                if (!IsWithin(file.FullPath, directory) || !File.Exists(file.FullPath)) throw new IOException("附件缺失或路径已改变 / Attachment missing or changed.");
                file.Length = new FileInfo(file.FullPath).Length;
                if (file.Length > Limits.MaxOutputBytes) throw new IOException("附件超过 29 MB / Attachment exceeds 29 MB.");
            }
            string text = HandoffWriter.Create(settings, result);
            if (Encoding.UTF8.GetByteCount(text) > Limits.MaxOutputBytes) throw new IOException("交接说明超过 29 MB / Handoff exceeds 29 MB.");
            if (SecretDetector.CheckText(text).Length > 0) throw new IOException("交接说明含疑似密钥，请检查填写内容或文件名后重试 / Handoff contains a secret-like value; review the fields or filenames.");
            string temporary = handoff + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllText(temporary, text, new UTF8Encoding(false));
                if (File.Exists(handoff)) File.Replace(temporary, handoff, null); else File.Move(temporary, handoff);
            } finally { if (File.Exists(temporary)) File.Delete(temporary); }
            result.Files.RemoveAll(delegate(OutputFile f) { return f.Name == "交接说明.md"; });
            result.Files.Add(new OutputFile { Name = "交接说明.md", FullPath = handoff, Length = new FileInfo(handoff).Length });
            string attachments = String.Join("、", result.Files.Where(delegate(OutputFile f) { return f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase); }).Select(delegate(OutputFile f) { return f.Name; }));
            result.CopyText = "请先读「交接说明.md」，再看附件 " + attachments + "。";
        }
        private static void WriteVolume(List<PreparedFile> group, string outputRoot, ref int sequence, PackageResult result, AppSettings settings, Action<PackProgress> progress, CancellationToken cancellation) {
            cancellation.ThrowIfCancellationRequested();
            string name = "发给Claude-" + sequence.ToString(CultureInfo.InvariantCulture) + ".zip";
            string output = Path.Combine(outputRoot, name);
            Report(progress, Txt(settings, "正在写入 ", "Writing ") + name, 75);
            using (FileStream stream = new FileStream(output, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Create, false, Encoding.UTF8)) {
                foreach (PreparedFile file in group) {
                    cancellation.ThrowIfCancellationRequested();
                    ZipArchiveEntry entry = zip.CreateEntry(SafeArchivePath(file.ArchivePath), CompressionLevel.Optimal);
                    using (Stream target = entry.Open()) using (FileStream source = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read)) CopyCount(source, target, source.Length, cancellation);
                }
            }
            long length = new FileInfo(output).Length;
            if (length > Limits.MaxOutputBytes) {
                File.Delete(output);
                if (group.Count < 2) throw new IOException(Txt(settings, "压缩包超出限制，未交付超大文件。", "ZIP exceeded limit; no oversized file was delivered."));
                int half = group.Count / 2;
                WriteVolume(group.GetRange(0, half), outputRoot, ref sequence, result, settings, progress, cancellation);
                WriteVolume(group.GetRange(half, group.Count - half), outputRoot, ref sequence, result, settings, progress, cancellation);
                return;
            }
            result.Files.Add(new OutputFile { Name = name, FullPath = output, Length = length }); sequence++;
        }
        private static void AddWarning(PackageResult result, string message) { if (result.Warnings.Count < 301) result.Warnings.Add(message); }
        private static string UniqueName(string name, HashSet<string> names) {
            string candidate = name; int suffix = 2;
            while (!names.Add(candidate)) { candidate = Path.GetFileNameWithoutExtension(name) + "-" + (suffix++).ToString(CultureInfo.InvariantCulture) + Path.GetExtension(name); }
            return candidate;
        }
        private static List<PreparedFile> EnsureUniqueArchiveNames(List<PreparedFile> files) {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); List<PreparedFile> result = new List<PreparedFile>();
            foreach (PreparedFile file in files) {
                string path = SafeArchivePath(file.ArchivePath); string candidate = path; int index = 2;
                while (!seen.Add(candidate)) { candidate = "duplicate-" + (index++).ToString(CultureInfo.InvariantCulture) + "/" + path; }
                result.Add(new PreparedFile { FullPath = file.FullPath, ArchivePath = candidate });
            }
            return result;
        }
        public static string SafeArchivePath(string path) {
            if (String.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) throw new IOException("归档路径无效 / Invalid archive path.");
            string[] parts = path.Replace('\\', '/').Split('/');
            foreach (string part in parts) if (part == ".." || part == "." || part.Length == 0 || part.IndexOf(':') >= 0) throw new IOException("归档路径不安全 / Unsafe archive path.");
            return String.Join("/", parts);
        }
        private static string SafeName(string name) {
            if (String.IsNullOrWhiteSpace(name)) return "file";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            name = name.TrimEnd('.', ' '); if (name.Length == 0 || name == "." || name == "..") return "file";
            return name;
        }
        private static bool IsWithin(string path, string directory) { return Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
        private static void CopyLocal(string from, string to, CancellationToken cancellation) {
            using (FileStream input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read)) using (FileStream output = new FileStream(to, FileMode.CreateNew, FileAccess.Write, FileShare.None)) CopyCount(input, output, input.Length, cancellation);
        }
        private static void CopyCount(Stream from, Stream to, long count, CancellationToken cancellation) {
            byte[] buffer = new byte[1024 * 1024];
            while (count > 0) {
                cancellation.ThrowIfCancellationRequested(); int n = from.Read(buffer, 0, (int)Math.Min(count, buffer.Length));
                if (n <= 0) throw new EndOfStreamException("输入在打包时被截断 / Input was truncated while packaging.");
                to.Write(buffer, 0, n); count -= n;
            }
        }
        private static string PsLiteral(string value) { return "'" + value.Replace("'", "''") + "'"; }
        private static string MergeText(string archivePath, int count, long bytes, string hash, AppSettings settings) {
            StringBuilder text = new StringBuilder();
            text.AppendLine("### " + archivePath.Replace("`", ""));
            text.AppendLine(Txt(settings, "这是原文件的字节切片，不是视频可播放分段。先把所有 zip 解压到同一个空目录，保留子目录，然后在此目录打开 PowerShell 执行：", "These are byte slices, not playable video segments. Extract all ZIPs into the same empty folder, preserve subfolders, and run PowerShell there:"));
            text.AppendLine("```powershell");
            text.AppendLine("$base = Join-Path (Get-Location) " + PsLiteral(archivePath.Replace('/', '\\')));
            text.AppendLine("$count = " + count.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("for ($i = 1; $i -le $count; $i++) { if (!(Test-Path -LiteralPath ($base + ('.{0:D3}' -f $i)))) { throw 'Missing part' } }");
            text.AppendLine("$out = [IO.File]::Open($base, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)");
            text.AppendLine("try { for ($i = 1; $i -le $count; $i++) { $part = [IO.File]::OpenRead($base + ('.{0:D3}' -f $i)); try { $part.CopyTo($out) } finally { $part.Dispose() } } } finally { $out.Dispose() }");
            text.AppendLine("if ((Get-FileHash -LiteralPath $base -Algorithm SHA256).Hash -ne '" + hash + "') { throw 'SHA256 mismatch' }");
            text.AppendLine("```");
            text.AppendLine("SHA-256: `" + hash + "`; " + bytes.ToString(CultureInfo.InvariantCulture) + " bytes; " + count.ToString(CultureInfo.InvariantCulture) + " parts.");
            return text.ToString();
        }
    }
    public static class HandoffWriter {
        public static string Create(AppSettings settings, PackageResult result) {
            bool en = settings.Language == "en"; StringBuilder text = new StringBuilder();
            text.AppendLine(en ? "# Handoff" : "# 交接说明"); text.AppendLine();
            text.AppendLine(en ? "## Attachments" : "## 附件"); text.AppendLine();
            foreach (OutputFile file in result.Files) if (file.Name != "交接说明.md") text.AppendLine("- `" + file.Name.Replace("`", "") + "` — " + (file.Length / 1000000.0).ToString("0.00", CultureInfo.InvariantCulture) + " MB (" + file.Length.ToString(CultureInfo.InvariantCulture) + " bytes)");
            text.AppendLine(); text.AppendLine(en ? "Each ZIP extracts independently. Extract all ZIPs to one empty folder, preserving paths. MB = 1,000,000 bytes; each generated attachment is at most 29,000,000 bytes." : "每个 ZIP 均可独立解压。请将所有 ZIP 解压到同一个空目录并保留路径。MB 按 1,000,000 字节计，每个生成的附件均不超过 29,000,000 字节。");
            text.AppendLine();
            HandoffData data = settings.Handoff ?? new HandoffData();
            Section(text, en ? "Project" : "项目", data.Project); Section(text, en ? "Goal" : "目标", data.Goal); Section(text, en ? "Progress" : "做到哪了", data.Progress);
            Section(text, en ? "Verified" : "已验证", data.Verified); Section(text, en ? "Not yet verified" : "还没验证", data.Unverified); Section(text, en ? "Next steps" : "下一步", data.Next);
            if (result.MergeInstructions.Count > 0) { text.AppendLine(en ? "## Reassemble split originals" : "## 原文件切片合并方法"); text.AppendLine(); foreach (string instruction in result.MergeInstructions) text.AppendLine(instruction); }
            if (result.Warnings.Count > 0) { text.AppendLine(en ? "## Exclusions and notes" : "## 排除与备注"); text.AppendLine(); foreach (string warning in result.Warnings) text.AppendLine("- " + warning.Replace("\r", " ").Replace("\n", " ")); }
            text.AppendLine(); text.AppendLine(en ? "Prepared locally by Whale Pack Helper 0.1.1. Nothing was uploaded. Secret detection is a heuristic, not a guarantee; review attachments before sharing." : "由大肥鱼打包助手 0.1.1 在本机整理，未上传任何内容。密钥检测是启发式筛查，不能保证识别所有隐私信息；发送前请复核附件。");
            return text.ToString();
        }
        private static void Section(StringBuilder text, string heading, string value) { text.AppendLine("## " + heading); text.AppendLine(); text.AppendLine(value ?? ""); text.AppendLine(); }
    }
}
