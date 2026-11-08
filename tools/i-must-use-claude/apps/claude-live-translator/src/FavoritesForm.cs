using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace ClaudeCompanion {
    // A local file browser. Opening, searching and refreshing never write reply content.
    internal sealed class FavoritesForm : Form {
        private FavoritesStore store;
        private readonly bool preview;
        private readonly float displayScale;
        private readonly TextBox search = new TextBox();
        private readonly ListBox list = new ListBox();
        private readonly FlowLayoutPanel tags = new FlowLayoutPanel();
        private readonly ThemedComboBox detailTag = new ThemedComboBox();
        private readonly TextBox translated = new TextBox(), original = new TextBox(), notes = new TextBox();
        private readonly Label metadata = new Label(), empty = new Label(), status = new Label(), location = new Label();
        private readonly Button save, copyTranslation, copyOriginal, remove;
        private readonly ToolTip tips = new ToolTip { AutoPopDelay = 30000 };
        private List<FavoriteEntry> entries = new List<FavoriteEntry>();
        private FavoriteEntry selected;
        private string filterTag, acceptedSearch = "", statusDetails = "";
        private bool updating, statusError, disposed;
        internal Action<string> CopyText;
        internal Action<string> OpenDirectory;
        internal Func<FavoriteEntry, bool> ConfirmDelete;
        internal Func<DialogResult> ConfirmUnsaved;
        internal Action<string> ReportFailure;
        internal FavoritesStore Store { get { return store; } }
        internal FavoriteEntry SelectedEntry { get { return selected; } }
        internal bool HasUnsavedChanges { get { return selected != null && (CanonicalLines(notes.Text) != CanonicalLines(selected.Notes) || !String.Equals(SelectedTagValue(), selected.Tag, StringComparison.OrdinalIgnoreCase)); } }

        private static string T(string zh, string en) { return Ui.T(zh, en); }
        private int S(float value) { return (int)Math.Round(value * displayScale); }
        private Rectangle R(int x, int y, int width, int height) { return new Rectangle(S(x), S(y), S(width), S(height)); }
        internal static string TagLabel(string tag) { return tag == FavoritesPreferences.DefaultTag ? T("未分类", "Unsorted") : tag; }

        internal FavoritesForm() : this(new FavoritesStore(FavoritesPreferences.Folder), false) { }
        internal FavoritesForm(FavoritesStore favorites, bool isPreview) {
            if (favorites == null) throw new ArgumentNullException("favorites");
            store = favorites; preview = isPreview;
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero)) displayScale = graphics.DpiX / 96F;
            SuspendLayout(); AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(S(600), S(560));
            Font = new Font("Microsoft YaHei UI", 9F); BackColor = Theme.Cream; ForeColor = Theme.Ink;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent; Text = T("回复收藏本 · 大肥鱼", "Reply notebook · Whale");
            Ui.ApplyIcon(this);
            CopyText = delegate(string text) { Clipboard.SetText(text); };
            OpenDirectory = delegate(string path) { Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); };
            ConfirmDelete = delegate(FavoriteEntry entry) {
                if (preview) return false;
                return MessageBox.Show(this, T("删除这条收藏对应的 Markdown 文件？\r\n", "Delete this saved reply's Markdown file?\r\n") + Path.GetFileName(entry.FilePath),
                    T("删除收藏", "Delete saved reply"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            };
            ConfirmUnsaved = delegate {
                if (preview) return DialogResult.Cancel;
                return MessageBox.Show(this, T("标签或备注尚未保存。先保存修改吗？\r\n选「否」会放弃未保存的修改。", "Your tag or note has unsaved changes. Save them first?\r\nChoose No to discard your edits."),
                    T("未保存的修改", "Unsaved changes"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            };
            ReportFailure = delegate(string text) { if (!preview) MessageBox.Show(this, text, T("收藏本", "Reply notebook"), MessageBoxButtons.OK, MessageBoxIcon.Warning); };

            var whale = new WhaleBadge { Bounds = R(16, 10, 57, 51) }; Controls.Add(whale);
            Controls.Add(new Label { Text = T("回复收藏本", "Reply notebook"), Font = new Font(Font.FontFamily, 17F, FontStyle.Bold), Bounds = R(83, 11, 490, 30), ForeColor = Theme.Ink });
            Controls.Add(new Label { Text = T("只保存你点过收藏的回复，存在这台电脑上", "Only replies you save are kept, on this computer."), Bounds = R(84, 45, 490, 21), Font = new Font(Font.FontFamily, 8F), ForeColor = Theme.Muted });

            var searchCard = new FavoriteSearchPanel(search, T("搜索原文或译文…", "Search original or translation…")) { Bounds = R(16, 76, 386, 34) }; Controls.Add(searchCard);
            search.AccessibleName = T("搜索原文或译文", "Search original or translation");
            search.TextChanged += delegate { if (!updating) ApplyFilter(); };
            var refresh = ActionButton(T("刷新", "Refresh"), false, R(410, 76, 76, 34));
            refresh.Click += delegate { RefreshFromDisk(); };
            var folder = ActionButton(T("保存位置…", "Location…"), false, R(494, 76, 90, 34));
            folder.AccessibleName = T("更改收藏保存位置", "Change notebook save location"); folder.Click += delegate { ChooseLocation(); };
            tips.SetToolTip(folder, T("切换收藏文件夹；原有文件保留在原处。", "Switch folder. Existing files stay in their original folder."));
            tags.Bounds = R(16, 116, 568, 44); tags.AutoScroll = true; tags.WrapContents = false; tags.FlowDirection = FlowDirection.LeftToRight; tags.Margin = Padding.Empty; Controls.Add(tags);

            var left = new RoundedCard { Bounds = R(16, 166, 200, 288) }; Controls.Add(left);
            list.Bounds = R(7, 8, 186, 270); list.BorderStyle = BorderStyle.None; list.BackColor = Color.White; list.ForeColor = Theme.Ink;
            list.DrawMode = DrawMode.OwnerDrawFixed; list.ItemHeight = S(54); list.IntegralHeight = false;
            list.AccessibleName = T("已收藏的回复", "Saved replies"); list.DrawItem += DrawEntry; list.SelectedIndexChanged += OnSelected;
            left.Controls.Add(list);
            empty.Bounds = R(15, 66, 170, 130); empty.ForeColor = Theme.Muted; empty.TextAlign = ContentAlignment.MiddleCenter; empty.BackColor = Color.White; left.Controls.Add(empty);

            var right = new RoundedCard { Bounds = R(224, 166, 360, 288), BackColor = Color.White }; Controls.Add(right);
            right.Controls.Add(new Label { Text = T("标签", "Tag"), Bounds = R(12, 13, 38, 22), ForeColor = Theme.Muted, BackColor = Color.White });
            detailTag.Bounds = R(53, 10, 220, 28); detailTag.AccessibleName = T("收藏标签", "Saved reply tag"); detailTag.SelectedIndexChanged += delegate { if (!updating) UpdateActions(); }; right.Controls.Add(detailTag);
            save = ActionButton(T("保存", "Save"), true, R(281, 8, 66, 31), right); save.AccessibleName = T("保存标签和备注", "Save tag and note"); save.Click += delegate { SaveEdits(); };
            metadata.Bounds = R(12, 44, 335, 30); metadata.ForeColor = Theme.Muted; metadata.Font = new Font(Font.FontFamily, 7.5F); metadata.BackColor = Color.White; metadata.AutoEllipsis = true; right.Controls.Add(metadata);
            AddDetailLabel(right, T("译文", "Translation"), 77);
            Editor(right, translated, R(12, 95, 334, 60), true);
            AddDetailLabel(right, T("原文", "Original"), 158);
            Editor(right, original, R(12, 176, 334, 36), true);
            AddDetailLabel(right, T("备注 · 修改后点保存", "Note · click Save after editing"), 217);
            Editor(right, notes, R(12, 237, 334, 37), false); notes.BackColor = Theme.Mist; notes.TextChanged += delegate { if (!updating) UpdateActions(); };
            translated.AccessibleName = T("收藏译文", "Saved translation"); original.AccessibleName = T("收藏原文", "Saved original"); notes.AccessibleName = T("收藏备注", "Saved reply note");

            copyTranslation = ActionButton(T("复制译文", "Copy translation"), false, R(16, 466, 136, 35)); copyTranslation.Click += delegate { CopySelected(false); };
            copyOriginal = ActionButton(T("复制原文", "Copy original"), false, R(160, 466, 136, 35)); copyOriginal.Click += delegate { CopySelected(true); };
            remove = ActionButton(T("删除", "Delete"), false, R(304, 466, 136, 35)); remove.ForeColor = Color.FromArgb(190, 86, 108); remove.Click += delegate { DeleteSelected(); };
            var open = ActionButton(T("打开文件夹", "Open folder"), false, R(448, 466, 136, 35)); open.Click += delegate { try { OpenDirectory(store.RootDirectory); } catch (Exception ex) { Fail(ex); } };
            location.Bounds = R(18, 508, 565, 20); location.ForeColor = Theme.Muted; location.Font = new Font(Font.FontFamily, 7.5F); location.AutoEllipsis = true; Controls.Add(location);
            status.Bounds = R(18, 533, 565, 20); status.Font = new Font(Font.FontFamily, 8F); status.ForeColor = Theme.BlueDark; status.AutoEllipsis = true; status.Click += delegate { if (statusError && statusDetails.Length > 0) ReportFailure(statusDetails); }; Controls.Add(status);
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (!CanLeaveEdits()) e.Cancel = true; };
            ResumeLayout(false); LoadEntries(null); UpdateLocation();
        }

        private Button ActionButton(string text, bool primary, Rectangle bounds) { return ActionButton(text, primary, bounds, this); }
        private Button ActionButton(string text, bool primary, Rectangle bounds, Control parent) {
            var button = Theme.Button(text, primary); button.Bounds = bounds; button.Margin = Padding.Empty;
            button.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold); parent.Controls.Add(button); return button;
        }
        private void AddDetailLabel(Control parent, string text, int top) { parent.Controls.Add(new Label { Text = text, Bounds = R(12, top, 334, 18), Font = new Font(Font.FontFamily, 8F), ForeColor = Theme.Muted, BackColor = Color.White }); }
        private void Editor(Control parent, TextBox box, Rectangle bounds, bool readOnly) {
            box.Bounds = bounds; box.Multiline = true; box.ScrollBars = ScrollBars.Vertical; box.BorderStyle = BorderStyle.None; box.ReadOnly = readOnly;
            box.BackColor = Color.White; box.ForeColor = Theme.Ink; box.Font = new Font(Font.FontFamily, 9F); box.MaxLength = 0; parent.Controls.Add(box);
        }
        private void UpdateLocation() { location.Text = T("保存在：", "Saved in: ") + store.RootDirectory; tips.SetToolTip(location, store.RootDirectory); }
        private void Status(string text, bool error) { statusDetails = text; statusError = error; status.Text = text; status.ForeColor = error ? Color.FromArgb(178, 74, 91) : Theme.BlueDark; tips.SetToolTip(status, text); }
        private void Fail(Exception error) { Status(T("未完成：", "Not completed: ") + ErrorText(error), true); }
        internal static string ErrorText(Exception error) {
            if (Ui.English) return error.Message;
            switch (error.Message) {
                case "This favorite was deleted outside the app. Refresh the notebook.": return "收藏文件已在外部删除，请刷新收藏本。";
                case "This favorite changed outside the app. Refresh before saving or deleting it.": return "收藏文件已在外部修改，请先刷新；本次没有覆盖或删除文件。";
                case "Favorites settings could not be read. The file has not been overwritten.": return "无法读取收藏设置文件；原文件未被覆盖。";
                case "Favorites must be stored in a local folder.": return "请选择本机文件夹，不能使用网络目录。";
                case "Choose a local favorites folder.": return "请选择本机收藏文件夹。";
                case "Linked folders and files are not supported for favorites.": return "收藏不支持链接文件或链接目录，请选择普通本机文件夹。";
                case "The favorite file is missing.": return "没有找到收藏文件，请刷新。";
                case "The favorite file is outside the selected folder.": return "该收藏文件不在当前文件夹中。";
                case "There is no translation to save.": return "还没有可以保存的译文。";
                case "The favorite is too large to save.": return "这条收藏过大，未写入文件。";
                case "A tag can contain at most 60 characters.": return "标签最多包含 60 个字符。";
                case "A tag cannot contain control characters.": return "标签不能包含控制字符。";
                default: return error is UnauthorizedAccessException ? "没有文件访问权限，请检查保存位置。" : "请检查文件是否可访问，以及是否正被其他程序占用。";
            }
        }

        internal void RefreshFromDisk() {
            if (!CanLeaveEdits()) return;
            string id = selected == null ? null : selected.Id;
            LoadEntries(id);
        }
        private void LoadEntries(string keepId) {
            try {
                entries = store.Load();
                RebuildTags(); RefreshVisible(keepId, false);
                Status(store.LastLoadErrors.Count == 0 ? T("仅本机保存 · 共 ", "Local only · ") + entries.Count + T(" 条收藏", " saved replies")
                    : T("部分文件未能读取（", "Some files could not be read (") + store.LastLoadErrors.Count + T("）。原文件保持不变。", "). Files were left unchanged."), store.LastLoadErrors.Count > 0);
            } catch (Exception ex) { Fail(ex); }
        }
        private void RebuildTags() {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string tag in FavoritesPreferences.Tags) if (!counts.ContainsKey(tag)) counts[tag] = 0;
            foreach (FavoriteEntry entry in entries) { string tag = entry.Tag ?? FavoritesPreferences.DefaultTag; if (!counts.ContainsKey(tag)) counts[tag] = 0; counts[tag]++; }
            if (!String.IsNullOrEmpty(filterTag) && !counts.ContainsKey(filterTag)) filterTag = null;
            tags.SuspendLayout();
            while (tags.Controls.Count > 0) { Control child = tags.Controls[0]; tags.Controls.RemoveAt(0); child.Dispose(); }
            AddTagButton(T("全部", "All"), entries.Count, null);
            var names = new List<string>(counts.Keys); names.Sort(StringComparer.CurrentCultureIgnoreCase);
            foreach (string name in names) AddTagButton(TagLabel(name), counts[name], name);
            var add = Theme.Button(T("＋ 标签", "+ Tag"), false); add.Width = S(72); add.Height = S(26); add.Margin = new Padding(0, 0, S(6), 0); add.Font = new Font(Font.FontFamily, 8F);
            add.Click += delegate { CreateTag(); }; tags.Controls.Add(add); tags.ResumeLayout(true);
            string selectedTag = selected == null ? FavoritesPreferences.LastTag : SelectedTagValue();
            bool wasUpdating = updating; updating = true;
            detailTag.Items.Clear(); foreach (string name in names) detailTag.Items.Add(new FavoriteTagChoice(name));
            for (int i = 0; i < detailTag.Items.Count; i++) if (String.Equals(((FavoriteTagChoice)detailTag.Items[i]).Value, selectedTag, StringComparison.OrdinalIgnoreCase)) detailTag.SelectedIndex = i;
            updating = wasUpdating;
        }
        private void AddTagButton(string label, int count, string value) {
            string text = label + " " + count.ToString(CultureInfo.InvariantCulture);
            var button = Theme.Button(text, String.Equals(filterTag, value, StringComparison.OrdinalIgnoreCase));
            button.Font = new Font(Font.FontFamily, 8F); button.Width = Math.Min(S(185), Math.Max(S(58), TextRenderer.MeasureText(text, button.Font).Width + S(22)));
            button.Height = S(26); button.Margin = new Padding(0, 0, S(6), 0); button.Tag = value;
            button.AccessibleName = label + ": " + count.ToString(CultureInfo.InvariantCulture);
            button.Click += delegate { SelectTag(value); }; tags.Controls.Add(button);
        }
        internal void SelectTag(string tag) {
            if (!CanLeaveEdits()) return;
            filterTag = tag; string id = selected == null ? null : selected.Id;
            RebuildTags(); RefreshVisible(id, false);
        }
        private void ApplyFilter() {
            string id = selected == null ? null : selected.Id;
            List<FavoriteEntry> found = FavoritesStore.Search(entries, search.Text, filterTag);
            bool keep = false; foreach (FavoriteEntry entry in found) if (entry.Id == id) { keep = true; break; }
            if (!keep && !CanLeaveEdits()) { updating = true; search.Text = acceptedSearch; search.SelectionStart = search.TextLength; updating = false; return; }
            acceptedSearch = search.Text; RefreshVisible(id, keep && HasUnsavedChanges);
        }
        private void RefreshVisible(string keepId, bool keepEdits) {
            updating = true;
            List<FavoriteEntry> found = FavoritesStore.Search(entries, search.Text, filterTag);
            list.BeginUpdate(); list.Items.Clear(); int chosen = -1;
            foreach (FavoriteEntry entry in found) { int index = list.Items.Add(entry); if (entry.Id == keepId) chosen = index; }
            if (chosen < 0 && list.Items.Count > 0) chosen = 0;
            list.SelectedIndex = chosen; list.EndUpdate();
            empty.Text = entries.Count == 0 ? T("还没有收藏\r\n\r\n在译文卡片上点\r\n「☆ 收藏」即可保存", "No saved replies yet\r\n\r\nClick ☆ Save on a\r\ntranslation to keep it here.")
                : T("没有符合条件的收藏\r\n\r\n换个词或标签试试", "No matching replies\r\n\r\nTry another search or tag.");
            empty.Visible = list.Items.Count == 0; list.Visible = !empty.Visible;
            if (!keepEdits) ShowEntry(list.SelectedItem as FavoriteEntry);
            updating = false; UpdateActions();
        }
        private void OnSelected(object sender, EventArgs args) {
            if (updating) return;
            FavoriteEntry next = list.SelectedItem as FavoriteEntry;
            if (next == selected) return;
            if (!CanLeaveEdits()) { updating = true; list.SelectedItem = selected; updating = false; return; }
            ShowEntry(next);
        }
        private void ShowEntry(FavoriteEntry entry) {
            bool previous = updating; updating = true; selected = entry;
            translated.Text = entry == null ? "" : DisplayLines(entry.Translation); original.Text = entry == null ? "" : DisplayLines(entry.SourceText); notes.Text = entry == null ? "" : DisplayLines(entry.Notes);
            metadata.Text = entry == null ? T("选择一条收藏查看详情", "Choose a saved reply to view it") : entry.CreatedAt.LocalDateTime.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture) + "\r\n" + entry.SourceTitle;
            tips.SetToolTip(metadata, entry == null ? "" : entry.SourceTitle + "\r\n" + entry.Engine + " · " + entry.Direction + "\r\n" + entry.FilePath);
            detailTag.SelectedIndex = -1;
            if (entry != null) for (int i = 0; i < detailTag.Items.Count; i++) if (String.Equals(((FavoriteTagChoice)detailTag.Items[i]).Value, entry.Tag, StringComparison.OrdinalIgnoreCase)) { detailTag.SelectedIndex = i; break; }
            updating = previous; UpdateActions();
        }
        private string SelectedTagValue() { var value = detailTag.SelectedItem as FavoriteTagChoice; return value == null ? (selected == null ? FavoritesPreferences.LastTag : selected.Tag) : value.Value; }
        private void UpdateActions() {
            bool exists = selected != null; copyTranslation.Enabled = exists; copyOriginal.Enabled = exists; remove.Enabled = exists;
            detailTag.Enabled = exists; notes.ReadOnly = !exists; save.Enabled = exists && HasUnsavedChanges;
        }
        private bool CanLeaveEdits() {
            if (!HasUnsavedChanges) return true;
            DialogResult choice = ConfirmUnsaved();
            if (choice == DialogResult.Yes) return SaveEdits();
            return choice == DialogResult.No;
        }
        internal bool SaveEdits() {
            if (selected == null) return false;
            string oldTag = selected.Tag, oldNotes = selected.Notes, id = selected.Id;
            try {
                selected.Tag = SelectedTagValue(); selected.Notes = notes.Text;
                store.Update(selected);
                string savedTag = selected.Tag;
                LoadEntries(id);
                try { FavoritesPreferences.LastTag = savedTag; }
                catch (Exception) { Status(T("收藏已保存，但未能记住上次标签。请检查设置文件。", "Reply saved, but the last-used tag could not be remembered. Check the settings file."), true); return true; }
                Status(T("标签和备注已保存到 Markdown", "Tag and note saved to Markdown"), false); return true;
            } catch (Exception ex) { selected.Tag = oldTag; selected.Notes = oldNotes; Fail(ex); UpdateActions(); return false; }
        }
        internal void CopySelected(bool source) {
            if (selected == null) return;
            try { CopyText(source ? selected.SourceText : selected.Translation); Status(source ? T("原文已复制", "Original copied") : T("译文已复制", "Translation copied"), false); }
            catch (Exception ex) { Fail(ex); }
        }
        internal bool DeleteSelected() {
            if (selected == null || !ConfirmDelete(selected)) return false;
            try { store.Delete(selected); selected = null; LoadEntries(null); Status(T("已删除对应的 Markdown 文件", "The Markdown file was deleted"), false); return true; }
            catch (Exception ex) { Fail(ex); return false; }
        }
        private void CreateTag() {
            string tag; if (!FavoriteTagPrompt.TryPrompt(this, out tag)) return;
            try { FavoritesPreferences.CreateTag(tag); RebuildTags(); Status(T("标签已创建；请选择需要的标签", "Tag created. Choose it when saving a reply."), false); }
            catch (Exception ex) { Fail(ex); }
        }
        private void ChooseLocation() {
            if (!CanLeaveEdits()) return;
            using (var dialog = new FolderBrowserDialog { Description = T("选择收藏保存位置。原有文件保留在原处，不会自动移动。", "Choose a notebook folder. Existing files will stay where they are."), SelectedPath = store.RootDirectory, ShowNewFolderButton = true }) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try {
                    var replacement = new FavoritesStore(dialog.SelectedPath); replacement.Load();
                    FavoritesPreferences.SetFolder(replacement.RootDirectory); store = replacement; selected = null; filterTag = null;
                    updating = true; search.Clear(); acceptedSearch = ""; updating = false;
                    LoadEntries(null); UpdateLocation(); Status(T("保存位置已切换；原文件仍在原处", "Folder changed. Existing files remain in their original folder."), false);
                } catch (Exception ex) { Fail(ex); }
            }
        }
        private static string CanonicalLines(string text) { return (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n"); }
        private static string DisplayLines(string text) { return CanonicalLines(text).Replace("\n", "\r\n"); }
        private static string Snippet(string text) { return (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim(); }
        private void DrawEntry(object sender, DrawItemEventArgs e) {
            using (var background = new SolidBrush(Color.White)) e.Graphics.FillRectangle(background, e.Bounds);
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            var entry = (FavoriteEntry)list.Items[e.Index]; var rect = Rectangle.Inflate(e.Bounds, -S(2), -S(3));
            if ((e.State & DrawItemState.Selected) != 0) using (var path = Theme.RoundedRect(rect, S(9))) using (var fill = new SolidBrush(Theme.Blue)) e.Graphics.FillPath(fill, path);
            var first = new Rectangle(rect.Left + S(7), rect.Top + S(5), rect.Width - S(14), S(19));
            var second = new Rectangle(first.Left, rect.Top + S(25), first.Width, S(20));
            using (var small = new Font(Font.FontFamily, 7.7F)) TextRenderer.DrawText(e.Graphics, entry.CreatedAt.LocalDateTime.ToString("MM/dd HH:mm", CultureInfo.InvariantCulture) + " · " + entry.SourceTitle, small, first, Theme.Muted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, Snippet(entry.Translation), Font, second, Theme.Ink, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if ((e.State & DrawItemState.Focus) != 0) ControlPaint.DrawFocusRectangle(e.Graphics, rect, Theme.BlueDark, Theme.Blue);
        }
        protected override void Dispose(bool disposing) { if (disposing && !disposed) { disposed = true; tips.Dispose(); } base.Dispose(disposing); }

        private sealed class FavoriteTagChoice {
            internal readonly string Value;
            internal FavoriteTagChoice(string value) { Value = value; }
            public override string ToString() { return TagLabel(Value); }
        }
    }

    internal sealed class FavoriteSearchPanel : Panel {
        private readonly TextBox input;
        private readonly Label hint;
        internal FavoriteSearchPanel(TextBox textBox, string placeholder) {
            input = textBox; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Theme.Cream; input.BorderStyle = BorderStyle.None; input.BackColor = Color.White; input.ForeColor = Theme.Ink; input.Font = new Font("Microsoft YaHei UI", 9F);
            hint = new Label { Text = placeholder, BackColor = Color.White, ForeColor = Theme.Muted, AutoEllipsis = true, Font = input.Font, Cursor = Cursors.IBeam };
            Controls.Add(input); Controls.Add(hint); hint.Click += delegate { input.Focus(); };
            input.TextChanged += delegate { RefreshHint(); }; input.GotFocus += delegate { RefreshHint(); }; input.LostFocus += delegate { RefreshHint(); };
        }
        private void RefreshHint() { hint.Visible = input.TextLength == 0 && !input.Focused; }
        protected override void OnResize(EventArgs e) {
            base.OnResize(e); if (input == null) return;
            float scale; using (var g = CreateGraphics()) scale = g.DpiX / 96F;
            int left = (int)(31 * scale), pad = (int)(10 * scale);
            input.SetBounds(left, Math.Max(0, (Height - input.PreferredHeight) / 2), Math.Max(0, Width - left - pad), input.PreferredHeight);
            hint.Bounds = input.Bounds; RefreshHint();
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = e.Graphics.DpiX / 96F;
            using (var path = Theme.RoundedRect(new RectangleF(.5F, .5F, Width - 1, Height - 1), 12 * scale))
            using (var fill = new SolidBrush(Color.White)) using (var pen = new Pen(Theme.Line, scale)) { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(pen, path); }
            float x = 12 * scale, y = Height / 2F - 5 * scale;
            using (var pen = new Pen(Theme.Muted, 1.4F * scale)) { e.Graphics.DrawEllipse(pen, x, y, 8 * scale, 8 * scale); e.Graphics.DrawLine(pen, x + 7 * scale, y + 7 * scale, x + 11 * scale, y + 11 * scale); }
        }
    }

    internal sealed class FavoriteTagPrompt : Form {
        private readonly TextBox text = new TextBox();
        private FavoriteTagPrompt() {
            SuspendLayout(); AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96F, 96F);
            Font = new Font("Microsoft YaHei UI", 9F); ClientSize = new Size(340, 130); BackColor = Theme.Cream; ForeColor = Theme.Ink;
            FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; Ui.ApplyIcon(this);
            Text = Ui.T("新建标签", "New tag");
            Controls.Add(new Label { Text = Ui.T("标签名", "Tag name"), Bounds = new Rectangle(16, 12, 300, 22) });
            text.SetBounds(16, 40, 308, 25); text.MaxLength = 40; Controls.Add(text);
            var save = Theme.Button(Ui.T("创建", "Create"), true); save.SetBounds(218, 83, 106, 34); save.DialogResult = DialogResult.OK; Controls.Add(save);
            var cancel = Theme.Button(Ui.T("取消", "Cancel"), false); cancel.SetBounds(104, 83, 106, 34); cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel);
            AcceptButton = save; CancelButton = cancel; AutoScaleDimensions = new SizeF(96F, 96F); ResumeLayout(true);
        }
        internal static bool TryPrompt(IWin32Window owner, out string tag) {
            using (var dialog = new FavoriteTagPrompt()) {
                if (dialog.ShowDialog(owner) != DialogResult.OK) { tag = null; return false; }
                tag = dialog.text.Text.Trim(); return tag.Length > 0;
            }
        }
    }
}
