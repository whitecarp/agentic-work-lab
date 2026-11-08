using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WhalePack {
    public sealed class MainForm : Form {
        public bool PreviewOnly { get; set; }
        private readonly AppSettings settings;
        private readonly List<InputItem> items = new List<InputItem>();
        private readonly Dictionary<string, VideoInfo> videos = new Dictionary<string, VideoInfo>(StringComparer.OrdinalIgnoreCase);
        private Panel itemList, estimateList;
        private Label estimateTitle, countLabel;
        private TextBox output;
        private Button pack, handoff, language, config, chooseFiles, chooseFolder, browse, clear;
        private RoundCheckBox writeHandoff;
        private CancellationTokenSource cancellation;
        private bool busy;
        private PackageResult result;
        private string finalStatus = "";
        private readonly ToolTip tips = new ToolTip();

        public MainForm(AppSettings settings) {
            this.settings = settings;
            if (String.IsNullOrWhiteSpace(settings.OutputDirectory)) settings.OutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "发给Claude");
            Ui.Setup(this, T("大肥鱼打包助手", "Big Whale Pack Helper") + "  ·  0.1.1", 560, 640);
            StartPosition = FormStartPosition.CenterScreen; AllowDrop = true;
            DragEnter += OnDragEnter; DragDrop += OnDragDrop; FormClosing += HandleClosing;
            Build(); ResumeLayout(false);
        }
        private string T(string zh, string en) { return Ui.T(settings, zh, en); }
        private void Build() {
            SuspendLayout();
            foreach (Control c in Controls.Cast<Control>().ToArray()) c.Dispose(); Controls.Clear();
            Text = T("大肥鱼打包助手", "Big Whale Pack Helper") + "  ·  0.1.1";
            PictureBox whale = new PictureBox { Image = Ui.LoadWhale(), SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(20, 12), Size = new Size(100, 70) };
            Controls.Add(whale);
            Ui.Label(this, T("大肥鱼打包助手", "Big Whale Pack"), 130, 21, 300, 35, 19f, true, Theme.Ink);
            Ui.Label(this, T("把文件整理成能直接发给 Claude 的样子", "Small attachments. A clear handoff."), 132, 57, 310, 24, 9f, false, Theme.Muted);
            language = Ui.Button(this, settings.Language == "en" ? "中文" : "English", 457, 13, 82, 27, false, delegate { SaveFields(); settings.Language = settings.Language == "en" ? "zh" : "en"; SaveSettings(); RebuildScaled(); });
            language.Font = Ui.Font(8f, false);
            config = Ui.Button(this, T("设置", "Settings"), 457, 48, 82, 27, false, delegate { ShowSettings(); }); config.Font = Ui.Font(8f, false);
            DropCard drop = new DropCard { Bounds = new Rectangle(20, 94, 520, 82), AllowDrop = true };
            Controls.Add(drop); drop.DragEnter += OnDragEnter; drop.DragDrop += OnDragDrop;
            Label dragText = Ui.Label(drop, T("把文件或文件夹拖到这里", "Drop files or folders here"), 15, 8, 490, 25, 12f, true, Theme.Ink); dragText.TextAlign = ContentAlignment.MiddleCenter;
            chooseFiles = Ui.Button(drop, T("选择文件", "Choose files"), 131, 40, 120, 29, false, delegate { PickFiles(); }); chooseFiles.Font = Ui.Font(9f, true);
            chooseFolder = Ui.Button(drop, T("选择文件夹", "Choose folder"), 269, 40, 120, 29, false, delegate { PickFolder(); }); chooseFolder.Font = Ui.Font(9f, true);

            RoundedCard listCard = new RoundedCard { Bounds = new Rectangle(20, 187, 520, 215), BackColor = Color.White }; Controls.Add(listCard);
            Ui.Label(listCard, T("要发的东西", "Things to send"), 16, 8, 170, 23, 10f, true, Theme.Ink);
            countLabel = Ui.Label(listCard, "", 182, 9, 110, 22, 8f, false, Theme.Muted); countLabel.TextAlign = ContentAlignment.MiddleLeft;
            clear = Ui.Button(listCard, T("清空", "Clear"), 436, 8, 65, 24, false, delegate { items.Clear(); result = null; finalStatus = ""; RenderItems(); RenderEstimates(); }); clear.Font = Ui.Font(8f, false);
            itemList = new Panel { Bounds = new Rectangle(12, 36, 496, 165), BackColor = Color.White, AutoScroll = true }; listCard.Controls.Add(itemList);

            RoundedCard estimated = new RoundedCard { Bounds = new Rectangle(20, 413, 520, 101), BackColor = Color.White }; Controls.Add(estimated);
            estimateTitle = Ui.Label(estimated, "", 16, 9, 488, 25, 9.5f, true, Theme.Ink);
            estimateList = new Panel { Bounds = new Rectangle(16, 38, 488, 53), BackColor = Color.White, AutoScroll = true }; estimated.Controls.Add(estimateList);
            Ui.Label(this, T("输出到", "Save to"), 21, 526, 53, 29, 8.5f, false, Theme.Muted);
            RoundedCard outputCard = new RoundedCard { Bounds = new Rectangle(77, 524, 241, 32), BackColor = Color.White }; Controls.Add(outputCard);
            output = new TextBox { BorderStyle = BorderStyle.None, Bounds = new Rectangle(10, 7, 220, 21), Text = settings.OutputDirectory, Font = Ui.Font(8.5f, false), ForeColor = Theme.Ink }; outputCard.Controls.Add(output);
            tips.SetToolTip(output, settings.OutputDirectory);
            browse = Ui.Button(this, "…", 325, 524, 32, 30, false, delegate { ChooseOutput(); });
            writeHandoff = new RoundCheckBox { Text = T("同时写交接说明", "Write handoff note"), Checked = settings.WriteHandoff, Bounds = new Rectangle(370, 524, 172, 30), Font = Ui.Font(8.4f, false), ForeColor = Theme.Ink }; Controls.Add(writeHandoff);
            handoff = Ui.Button(this, T("写交接说明…", "Handoff note…"), 20, 571, 154, 36, false, delegate { ShowHandoff(); });
            pack = Ui.Button(this, T("开始打包", "Start packing"), 384, 571, 156, 36, true, delegate { StartOrCancel(); });
            Ui.Label(this, T("●  只在本机整理文件：不上传；只有手动检查更新时才联网", "●  Local files only. No uploads. Network only for manual update checks."), 22, 612, 518, 22, 8.5f, false, Theme.Muted);
            RenderItems(); RenderEstimates(); SetBusy(busy); ResumeLayout(false);
        }
        // Recreate at logical size, then restore the existing DPI once. This also keeps language switching stable at 150%.
        private void RebuildScaled() {
            float scale = ClientSize.Width / 560f;
            Build(); if (Math.Abs(scale - 1f) > .01f) foreach (Control c in Controls) c.Scale(new SizeF(scale, scale));
        }
        private void RenderItems() {
            if (itemList == null) return;
            foreach (Control c in itemList.Controls.Cast<Control>().ToArray()) c.Dispose(); itemList.Controls.Clear();
            countLabel.Text = items.Count == 0 ? "" : items.Count + T(" 项", " items");
            if (items.Count == 0) {
                Label blank = Ui.Label(itemList, T("还没有文件\n视频、源码、图片，一起拖进来", "Nothing here yet\nAdd videos, source folders and images"), 12, 35, 458, 76, 10f, false, Theme.Muted); blank.TextAlign = ContentAlignment.MiddleCenter;
                clear.Enabled = false; return;
            }
            clear.Enabled = !busy;
            int y = 0; float scale = itemList.Width / 496f;
            foreach (InputItem item in items) {
                Panel row = BuildRow(item); row.Top = y;
                if (Math.Abs(scale - 1f) > .01f) row.Scale(new SizeF(scale, scale));
                row.Top = y; itemList.Controls.Add(row); y += row.Height;
            }
            itemList.AutoScrollMinSize = new Size(0, y);
        }
        private Panel BuildRow(InputItem item) {
            Panel row = new Panel { Size = new Size(476, 41), BackColor = Color.White };
            bool warning = item.Entries.Any(delegate(SourceEntry e) { return e.Sensitive; });
            FileGlyph glyph = new FileGlyph { Bounds = new Rectangle(3, 6, 26, 27), Mode = item.Mode, Directory = item.IsDirectory, Warning = warning }; row.Controls.Add(glyph);
            Label name = Ui.Label(row, item.DisplayName, 38, 0, 181, 22, 9.1f, warning, warning ? Color.FromArgb(152, 111, 37) : Theme.Ink); tips.SetToolTip(name, item.DisplayName);
            string description = Description(item);
            Label desc = Ui.Label(row, description, 38, 22, 298, 17, 7.7f, false, warning ? Color.FromArgb(152, 111, 37) : Theme.Muted); tips.SetToolTip(desc, DescriptionDetails(item)); desc.Cursor = Cursors.Hand; desc.Click += delegate { Review(item); };
            string estimated = item.Mode == ProcessingMode.Exclude ? T("排除", "excluded") : item.Mode == ProcessingMode.CompressVideo && !videos.ContainsKey(item.Path) ? T("待估算", "pending") : "≈" + Ui.Size(item.EstimatedBytes);
            Label sizes = Ui.Label(row, Ui.Size(item.OriginalBytes) + " → " + estimated, 219, 1, 117, 21, 7.2f, false, Theme.Ink); sizes.TextAlign = ContentAlignment.MiddleRight;
            Button mode = Ui.Button(row, Ui.Mode(settings, item.Mode) + " ▾", 341, 7, 113, 28, false, delegate { ShowModes(item, row); }); mode.Font = Ui.Font(7.7f, false); mode.Enabled = !busy;
            Button remove = new Button { Text = "×", Bounds = new Rectangle(455, 8, 21, 22), FlatStyle = FlatStyle.Flat, ForeColor = Theme.Muted, BackColor = Color.White, Font = Ui.Font(9f, false), Cursor = Cursors.Hand, TabStop = true }; remove.FlatAppearance.BorderSize = 0;
            remove.Click += delegate { items.Remove(item); result = null; RenderItems(); RenderEstimates(); }; remove.Enabled = !busy; row.Controls.Add(remove); tips.SetToolTip(remove, T("移除这一项", "Remove this item"));
            row.Paint += delegate(object sender, PaintEventArgs e) { using (Pen pen = new Pen(Color.FromArgb(239, 242, 243))) e.Graphics.DrawLine(pen, 36, row.Height - 1, row.Width - 6, row.Height - 1); };
            return row;
        }
        private string Description(InputItem item) {
            int excluded = item.Entries.Count(delegate(SourceEntry e) { return e.Excluded; });
            int sensitive = item.Entries.Count(delegate(SourceEntry e) { return e.Sensitive && e.Excluded; });
            if (item.IsDirectory) {
                string groups = String.Join("/", ExcludedCategories(item).Take(3).ToArray());
                return item.Entries.Count.ToString("N0") + T("文件 · 跳过", " files · skip ") + (groups.Length > 0 ? groups : excluded.ToString()) + (sensitive > 0 ? T(" · 疑似密钥", " · secrets ") + sensitive : T(" · 查看", " · Details"));
            }
            if (sensitive > 0) return T("已排除 ", "Excluded ") + sensitive + T(" 个疑似密钥文件 · 点击查看", " potential secret files · Review");
            if (item.Mode == ProcessingMode.CompressVideo) {
                VideoInfo info;
                if (videos.TryGetValue(item.Path, out info)) return info.Width + "×" + info.Height + " · " + info.Duration.ToString("0.#") + "s · " + VideoPlan.Build(info.Duration, info.HasAudio).Segments.Count + T(" 段 · 短边≤720", " parts · short edge≤720");
                return T("视频时长未读取，选好 FFmpeg 后估算 · 点此查看", "Duration not read; select FFmpeg to estimate · details");
            }
            if (item.Mode == ProcessingMode.ResizeImage) return T("最长边 ≤2560 · PNG 截图保留 PNG", "Longest edge ≤2560 · keep PNG screenshots");
            if (item.Mode == ProcessingMode.Exclude) return T("不会放进附件；点明细检查", "Not included; open Details to review");
            return T("原样收入独立 ZIP；大文件安全分卷", "Keep original; split large files safely");
        }
        private List<string> ExcludedCategories(InputItem item) {
            HashSet<string> found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] folders = { "runtime", "models", ".git", "bin", "obj", "node_modules", "__pycache__", ".vs", "artifacts" };
            foreach (SourceEntry entry in item.Entries.Where(delegate(SourceEntry e) { return e.Excluded; })) {
                string[] parts = entry.RelativePath.Replace('\\', '/').Split('/');
                foreach (string folder in folders) if (parts.Any(delegate(string p) { return String.Equals(p, folder, StringComparison.OrdinalIgnoreCase); })) found.Add(folder);
                string extension = System.IO.Path.GetExtension(entry.RelativePath).ToLowerInvariant();
                if (new string[] { ".exe", ".dll", ".pdb", ".zip", ".7z", ".iso", ".msi" }.Contains(extension)) found.Add(extension);
                if (entry.Length > Limits.MaxSourceFileBytes) found.Add(">20 MB");
            }
            string[] priority = { ".exe", "runtime", "models", ".git", "bin", "obj", "node_modules", "__pycache__", ".vs", "artifacts", ".dll", ".pdb", ".zip", ".7z", ".iso", ".msi", ">20 MB" };
            return priority.Where(delegate(string category) { return found.Contains(category); }).ToList();
        }
        private string DescriptionDetails(InputItem item) {
            if (!item.IsDirectory) return Description(item);
            int excluded = item.Entries.Count(delegate(SourceEntry e) { return e.Excluded; });
            int sensitive = item.Entries.Count(delegate(SourceEntry e) { return e.Excluded && e.Sensitive; });
            return item.Entries.Count.ToString("N0") + T(" 个文件；已排除 ", " files; excluded ") + excluded.ToString("N0") + "\n" + T("跳过类型：", "Skipped categories: ") + String.Join(", ", ExcludedCategories(item).ToArray()) + "\n" + T("其中可能含密钥：", "Potential secret files: ") + sensitive.ToString("N0") + "\n" + T("点击此行说明查看逐项明细，可复核后恢复。", "Click this description for full details and reviewed restore options.");
        }
        private void ShowModes(InputItem item, Control row) {
            if (busy) return;
            ContextMenuStrip menu = new ContextMenuStrip(); menu.Font = Ui.Font(9f, false);
            foreach (ProcessingMode value in Enum.GetValues(typeof(ProcessingMode))) {
                ProcessingMode captured = value;
                ToolStripMenuItem option = new ToolStripMenuItem(Ui.Mode(settings, value)); option.Checked = item.Mode == value;
                option.Enabled = value == ProcessingMode.Exclude || value == ProcessingMode.Original || (value == ProcessingMode.SourceArchive ? item.IsDirectory : !item.IsDirectory);
                option.Click += delegate {
                    if (captured != ProcessingMode.Exclude && item.Entries.Any(delegate(SourceEntry e) { return e.Sensitive && e.Excluded; })) { Review(item); if (item.Entries.All(delegate(SourceEntry e) { return e.Excluded; })) return; }
                    ChangeMode(item, captured);
                };
                menu.Items.Add(option);
            }
            menu.Closed += delegate { menu.Dispose(); }; menu.Show(row, new Point(row.Width - 120, row.Height - 3));
        }
        private void Review(InputItem item) {
            if (busy) return;
            using (ReviewForm form = new ReviewForm(settings, item)) form.ShowDialog(this);
            FileScanner.RefreshEstimate(item); ApplyVideoEstimate(item); result = null; finalStatus = ""; RenderItems(); RenderEstimates();
        }
        private void RenderEstimates() {
            if (estimateList == null) return;
            foreach (Control c in estimateList.Controls.Cast<Control>().ToArray()) c.Dispose(); estimateList.Controls.Clear();
            if (result != null) {
                estimateTitle.Text = T("打包完成 · 所有附件均已核验大小", "Ready · every attachment size verified");
                int y = 0; foreach (OutputFile f in result.Files) { AddBar(f.Name, f.Length, y); y += ScaledBarHeight(); }
                estimateList.AutoScrollMinSize = new Size(0, y); pack.Text = T("再次打包", "Pack again"); handoff.Text = T("查看结果…", "View results…"); return;
            }
            if (items.Any(delegate(InputItem i) { return i.Mode == ProcessingMode.CompressVideo && !videos.ContainsKey(i.Path); })) {
                estimateTitle.Text = finalStatus.Length > 0 ? finalStatus : T("视频时长尚未读取 · 分段数和大小待确认", "Video duration needed · sizes and part count pending");
                Ui.Label(estimateList, T("在设置中选择 FFmpeg 后估算；成品仍逐个核验 ≤29 MB", "Choose FFmpeg in Settings. Every output is checked ≤29 MB."), 0, 5, estimateList.Width - 8, 35, 8.3f, false, Theme.Muted); return;
            }
            long total = items.Where(delegate(InputItem i) { return i.Mode != ProcessingMode.Exclude; }).Sum(delegate(InputItem i) { return Math.Max(0, i.EstimatedBytes); });
            int count = total == 0 ? (items.Any(delegate(InputItem i) { return i.Mode != ProcessingMode.Exclude && i.Entries.Any(delegate(SourceEntry e) { return !e.Excluded; }); }) ? 1 : 0) : (int)Math.Min(100000, (total + Limits.ZipPayloadBytes - 1) / Limits.ZipPayloadBytes);
            estimateTitle.Text = count == 0 ? T("每个附件 ≤29 MB，预留 1 MB 余量", "Each file ≤29 MB, with 1 MB to spare") : T("预计生成 ", "About ") + count + T(" 个 ZIP · 完成后核验实际大小", " ZIP files · checked after packing");
            if (count == 0) {
                Ui.Label(estimateList, T("添加文件后，会在这里显示预计结果", "Estimated sizes will appear when you add files"), 0, 5, estimateList.Width - 8, 30, 8.6f, false, Theme.Muted);
            } else {
                long left = total; int y = 0; int visible = Math.Min(count, 200);
                for (int i = 0; i < visible; i++) { long n = Math.Min(Limits.ZipPayloadBytes, left); AddBar("发给Claude-" + (i + 1) + ".zip", n, y); left -= n; y += ScaledBarHeight(); }
                estimateList.AutoScrollMinSize = new Size(0, y);
            }
            if (!busy) { pack.Text = T("开始打包", "Start packing"); handoff.Text = T("写交接说明…", "Handoff note…"); }
            if (finalStatus.Length > 0) estimateTitle.Text = finalStatus;
        }
        private int ScaledBarHeight() { return Math.Max(24, (int)(24 * estimateList.Width / 488f)); }
        private void AddBar(string name, long size, int y) {
            SizeBar bar = new SizeBar { Caption = name, Bytes = size, Bounds = new Rectangle(0, y, estimateList.Width - SystemInformation.VerticalScrollBarWidth - 2, ScaledBarHeight()), Font = Ui.Font(8f, false) }; estimateList.Controls.Add(bar);
        }
        public void SetItemsForPreview(List<InputItem> values) { items.Clear(); items.AddRange(values); result = null; RenderItems(); RenderEstimates(); }
        public void SetVideoInfoForPreview(string path, VideoInfo info) {
            videos[path] = info;
            foreach (InputItem item in items) if (String.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)) ApplyVideoEstimate(item);
            RenderItems(); RenderEstimates();
        }
        public void SetResultForPreview(PackageResult value) { result = value; RenderEstimates(); }
        private void PickFiles() {
            using (OpenFileDialog dialog = new OpenFileDialog { Multiselect = true, Title = T("选择要整理的文件", "Choose files to pack"), Filter = T("所有文件|*.*", "All files|*.*") }) if (dialog.ShowDialog(this) == DialogResult.OK) AddPaths(dialog.FileNames);
        }
        private void PickFolder() {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog { Description = T("选择源码或其他文件夹", "Choose a source folder or any folder"), ShowNewFolderButton = false }) if (dialog.ShowDialog(this) == DialogResult.OK) AddPaths(new string[] { dialog.SelectedPath });
        }
        private void OnDragEnter(object sender, DragEventArgs e) { e.Effect = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; }
        private void OnDragDrop(object sender, DragEventArgs e) { if (!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) AddPaths((string[])e.Data.GetData(DataFormats.FileDrop)); }
        private async void AddPaths(string[] paths) {
            if (busy) return; cancellation = new CancellationTokenSource(); finalStatus = ""; SetBusy(true); estimateTitle.Text = T("正在本机检查文件和排除项…", "Checking files and exclusions locally…");
            try {
                CancellationToken token = cancellation.Token;
                string ffmpeg = MediaProcessor.FindFfmpeg(settings);
                List<InputItem> found = await Task.Run(delegate { List<InputItem> list = new List<InputItem>(); foreach (string path in paths) { token.ThrowIfCancellationRequested(); InputItem item = FileScanner.Scan(path, token); ProbeEstimate(item, ffmpeg, token); list.Add(item); } return list; }, token);
                foreach (InputItem item in found) if (!items.Any(delegate(InputItem i) { return String.Equals(i.Path, item.Path, StringComparison.OrdinalIgnoreCase); })) items.Add(item);
                result = null;
            } catch (OperationCanceledException) { finalStatus = T("已取消添加，已有列表保留", "Adding cancelled; existing list kept"); } catch (Exception ex) { Ui.Error(this, settings, ex); }
            finally { SetBusy(false); cancellation.Dispose(); cancellation = null; RenderItems(); RenderEstimates(); }
        }
        private void SaveFields() { settings.OutputDirectory = output.Text.Trim(); settings.WriteHandoff = writeHandoff.Checked; }
        private void SaveSettings() { if (PreviewOnly) return; try { SettingsStore.Save(settings); } catch (Exception ex) { Ui.Error(this, settings, ex); } }
        private void ChooseOutput() {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog { Description = T("选择输出位置", "Choose output folder"), SelectedPath = output.Text }) if (dialog.ShowDialog(this) == DialogResult.OK) { output.Text = dialog.SelectedPath; SaveFields(); SaveSettings(); tips.SetToolTip(output, output.Text); }
        }
        private void ShowHandoff() { SaveFields(); using (HandoffForm form = new HandoffForm(settings, result)) form.ShowDialog(this); writeHandoff.Checked = settings.WriteHandoff; RenderEstimates(); }
        private async void StartOrCancel() {
            if (busy) { if (cancellation != null) cancellation.Cancel(); pack.Enabled = false; estimateTitle.Text = T("正在安全取消…", "Cancelling safely…"); return; }
            if (items.Count == 0 || items.All(delegate(InputItem i) { return i.Mode == ProcessingMode.Exclude; })) { MessageBox.Show(this, T("请先添加需要打包的文件。", "Add at least one file to pack."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            SaveFields();
            if (String.IsNullOrWhiteSpace(settings.OutputDirectory)) { MessageBox.Show(this, T("请选择输出文件夹。", "Choose an output folder."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            bool needsFfmpeg = items.Any(delegate(InputItem i) { return i.Mode == ProcessingMode.CompressVideo || (i.Mode == ProcessingMode.ResizeImage && String.Equals(Path.GetExtension(i.Path), ".webp", StringComparison.OrdinalIgnoreCase)); });
            if (needsFfmpeg && String.IsNullOrEmpty(MediaProcessor.FindFfmpeg(settings))) {
                MessageBox.Show(this, T("没有找到 ffmpeg.exe。请选择本机已有的 FFmpeg；本程序不会下载 FFmpeg。", "ffmpeg.exe was not found. Select a local copy; this app does not download FFmpeg."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (!PickFfmpeg()) return;
            }
            SaveSettings(); cancellation = new CancellationTokenSource(); result = null; finalStatus = ""; SetBusy(true); estimateTitle.Text = T("开始整理文件…", "Preparing your files…");
            try {
                CancellationToken token = cancellation.Token;
                Progress<PackProgress> progress = new Progress<PackProgress>(delegate(PackProgress p) { if (!IsDisposed) { estimateTitle.Text = p.Percent + "% · " + p.Message; tips.SetToolTip(estimateTitle, p.Message); } });
                IProgress<PackProgress> report = progress;
                result = await Task.Run(delegate { return PackageEngine.Run(new List<InputItem>(items), settings, new MediaProcessor(), delegate(PackProgress p) { report.Report(p); }, token); }, token);
            } catch (OperationCanceledException) { finalStatus = T("已取消，原文件未改动", "Cancelled; original files unchanged"); }
            catch (Exception ex) { finalStatus = T("本次未完成，请检查提示后重试", "Packing did not complete; review the error"); Ui.Error(this, settings, ex); }
            finally { SetBusy(false); cancellation.Dispose(); cancellation = null; RenderItems(); RenderEstimates(); }
            if (result != null) { using (HandoffForm form = new HandoffForm(settings, result)) form.ShowDialog(this); writeHandoff.Checked = settings.WriteHandoff; RenderEstimates(); }
        }
        private void SetBusy(bool value) {
            busy = value; if (pack == null) return;
            foreach (Control c in new Control[] { handoff, language, config, chooseFiles, chooseFolder, browse, clear, writeHandoff, output, itemList }) c.Enabled = !value;
            clear.Enabled = !value && items.Count > 0;
            pack.Enabled = true; pack.Text = value ? T("取消", "Cancel") : (result != null ? T("再次打包", "Pack again") : T("开始打包", "Start packing"));
            UseWaitCursor = value;
        }
        private bool PickFfmpeg() {
            using (OpenFileDialog dialog = new OpenFileDialog { Title = T("选择 ffmpeg.exe", "Select ffmpeg.exe"), Filter = "ffmpeg.exe|ffmpeg.exe|EXE|*.exe", CheckFileExists = true }) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                settings.FfmpegPath = dialog.FileName; SaveSettings(); return true;
            }
        }
        private void ShowSettings() {
            using (Form form = new Form()) {
                Ui.Setup(form, T("设置 · 大肥鱼打包助手", "Settings · Big Whale Pack"), 480, 240); form.MinimizeBox = false;
                Ui.Label(form, "FFmpeg", 20, 17, 400, 30, 15f, true, Theme.Ink);
                Ui.Label(form, T("查找顺序：程序目录 → PATH → 下方手动路径", "Search: app folder → PATH → selected path below"), 20, 52, 440, 25, 8.5f, false, Theme.Muted);
                TextBox path = new TextBox { Text = settings.FfmpegPath, ReadOnly = true, Bounds = new Rectangle(20, 88, 340, 28), Font = Ui.Font(8.5f, false) }; form.Controls.Add(path);
                Ui.Button(form, T("选择…", "Browse…"), 370, 85, 90, 32, false, delegate { if (PickFfmpeg()) path.Text = settings.FfmpegPath; });
                Ui.Label(form, T("只用本机文件：不下载 FFmpeg，不上传。\n只有点「检查更新」时才会联网访问 GitHub。", "Local files only: no FFmpeg download, no uploads.\nNetwork only when you choose Check updates."), 20, 131, 440, 48, 9f, false, Theme.Muted);
                Ui.Button(form, T("检查更新…", "Check updates…"), 20, 193, 150, 32, false, delegate { WhaleCommon.UpdateChecker.Show(form, "pack", "0.1.1", settings.Language == "en"); });
                Ui.Button(form, T("完成", "Done"), 350, 193, 110, 32, true, delegate { form.Close(); }); form.ResumeLayout(false); form.ShowDialog(this);
            }
            ReestimateVideos();
        }
        private void ApplyVideoEstimate(InputItem item) { VideoInfo info; if (item.Mode == ProcessingMode.CompressVideo && videos.TryGetValue(item.Path, out info)) item.EstimatedBytes = MediaProcessor.EstimateVideoBytes(info); }
        private void ProbeEstimate(InputItem item, string ffmpeg, CancellationToken token) {
            if (item.Mode != ProcessingMode.CompressVideo || String.IsNullOrEmpty(ffmpeg)) return;
            try { VideoInfo info = MediaProcessor.ProbeVideo(item.Path, ffmpeg, token); videos[item.Path] = info; item.EstimatedBytes = MediaProcessor.EstimateVideoBytes(info); }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { videos.Remove(item.Path); }
        }
        private async void ChangeMode(InputItem item, ProcessingMode mode) {
            if (busy) return; item.Mode = mode; FileScanner.RefreshEstimate(item); result = null; finalStatus = "";
            if (mode == ProcessingMode.CompressVideo) {
                cancellation = new CancellationTokenSource(); SetBusy(true);
                try { string ffmpeg = MediaProcessor.FindFfmpeg(settings); CancellationToken token = cancellation.Token; await Task.Run(delegate { ProbeEstimate(item, ffmpeg, token); }, token); }
                catch (OperationCanceledException) { finalStatus = T("已取消估算", "Estimate cancelled"); }
                finally { SetBusy(false); cancellation.Dispose(); cancellation = null; }
            }
            RenderItems(); RenderEstimates();
        }
        private async void ReestimateVideos() {
            if (busy || !items.Any(delegate(InputItem i) { return i.Mode == ProcessingMode.CompressVideo; })) return;
            cancellation = new CancellationTokenSource(); SetBusy(true); finalStatus = "";
            try { string ffmpeg = MediaProcessor.FindFfmpeg(settings); CancellationToken token = cancellation.Token; await Task.Run(delegate { foreach (InputItem item in items) { token.ThrowIfCancellationRequested(); ProbeEstimate(item, ffmpeg, token); } }, token); }
            catch (OperationCanceledException) { finalStatus = T("已取消估算", "Estimate cancelled"); }
            finally { SetBusy(false); cancellation.Dispose(); cancellation = null; RenderItems(); RenderEstimates(); }
        }
        private void HandleClosing(object sender, FormClosingEventArgs e) {
            if (busy) { e.Cancel = true; if (cancellation != null) cancellation.Cancel(); estimateTitle.Text = T("正在取消，请稍后关闭窗口", "Cancelling; close again when finished"); return; }
            SaveFields(); SaveSettings(); tips.Dispose();
        }
    }
}
