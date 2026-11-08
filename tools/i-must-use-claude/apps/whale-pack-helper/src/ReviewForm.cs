using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace WhalePack {
    public sealed class ReviewForm : Form {
        private readonly AppSettings settings;
        private readonly InputItem item;
        private readonly EntryList list;
        private readonly Label summary;
        private readonly RoundCheckBox all;
        private string T(string zh, string en) { return Ui.T(settings, zh, en); }
        public ReviewForm(AppSettings settings, InputItem item) {
            this.settings = settings; this.item = item;
            Ui.Setup(this, T("文件明细 · 大肥鱼打包助手", "Review files · Big Whale Pack"), 560, 540); MinimizeBox = false;
            Ui.Label(this, item.DisplayName, 20, 16, 520, 32, 14f, true, Theme.Ink);
            Ui.Label(this, T("圆圈打勾 = 加入附件。仅显示路径和原因，不展示密钥内容。", "Checked = include. Only paths and reasons are shown, never secret values."), 20, 53, 520, 31, 8.5f, false, Theme.Muted);
            summary = Ui.Label(this, "", 20, 87, 320, 25, 9f, true, Theme.Ink);
            all = new RoundCheckBox { Text = T("显示全部文件", "Show all files"), Checked = item.Entries.All(delegate(SourceEntry e) { return !e.Excluded; }), Bounds = new Rectangle(374, 84, 166, 30), Font = Ui.Font(8.5f, false) }; Controls.Add(all);
            RoundedCard card = new RoundedCard { Bounds = new Rectangle(20, 123, 520, 353), BackColor = Color.White }; Controls.Add(card);
            list = new EntryList(settings) { Bounds = new Rectangle(10, 10, 500, 331) }; card.Controls.Add(list); list.Toggle += ToggleEntry;
            all.CheckedChanged += delegate { RefreshList(); };
            Ui.Label(this, T("符号链接不跟随；重新加入的文件打包前会再次检查。", "Links are not followed. Restored files are checked again before packing."), 21, 481, 380, 45, 8f, false, Theme.Muted);
            Ui.Button(this, T("完成", "Done"), 425, 491, 115, 33, true, delegate { Close(); });
            RefreshList(); ResumeLayout(false);
        }
        private void RefreshList() {
            int excluded = item.Entries.Count(delegate(SourceEntry e) { return e.Excluded; });
            summary.Text = T("已排除 ", "Excluded ") + excluded.ToString("N0") + " / " + item.Entries.Count.ToString("N0") + T(" 个文件", " files");
            list.Entries = all.Checked ? item.Entries.ToList() : item.Entries.Where(delegate(SourceEntry e) { return e.Excluded; }).ToList(); list.UpdateEntries();
        }
        private void ToggleEntry(SourceEntry entry) {
            if (entry.CannotInclude) { MessageBox.Show(this, T("此项是链接、不可访问文件或不安全路径，不能重新加入。", "This is a link, inaccessible file or unsafe path and cannot be restored."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (!entry.Excluded) { entry.Excluded = true; entry.UserIncluded = false; RefreshList(); return; }
            try {
                FileScanner.ReviewReason(entry);
                if (entry.Sensitive && MessageBox.Show(this, T("这个文件可能包含密钥或账户信息。只有你确认它适合发送给别人时才加入。\n\n确定把这个文件放回附件？", "This file may contain secrets or account data. Only include it if you have checked that it is safe to share.\n\nInclude this file in the attachments?") + "\n\n" + entry.RelativePath, T("确认加入可能含密钥的文件", "Confirm potentially sensitive file"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                FileScanner.Approve(entry); entry.Excluded = false; entry.UserIncluded = true;
                if (item.Mode == ProcessingMode.Exclude) {
                    item.Mode = item.IsDirectory ? ProcessingMode.SourceArchive : FileScanner.Classify(item.Path);
                    if (item.Mode == ProcessingMode.Exclude) item.Mode = ProcessingMode.Original;
                }
            } catch (Exception ex) { Ui.Error(this, settings, ex); }
            RefreshList();
        }
    }

    internal sealed class EntryList : Control {
        internal List<SourceEntry> Entries = new List<SourceEntry>();
        internal event Action<SourceEntry> Toggle;
        private readonly AppSettings settings;
        private readonly VScrollBar scroll;
        private int selected;
        internal EntryList(AppSettings settings) {
            this.settings = settings; BackColor = Color.White; TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            scroll = new VScrollBar { Dock = DockStyle.Right, SmallChange = 1 }; scroll.ValueChanged += delegate { Invalidate(); }; Controls.Add(scroll);
            Resize += delegate { UpdateEntries(); };
        }
        private float DisplayScale { get { return Math.Max(.5f, Width / 500f); } }
        private int RowHeight { get { return Math.Max(30, (int)(46 * DisplayScale)); } }
        internal void UpdateEntries() {
            int visible = Math.Max(1, Height / RowHeight); scroll.Minimum = 0; scroll.Maximum = Math.Max(0, Entries.Count - 1); scroll.LargeChange = visible;
            scroll.Value = Math.Min(scroll.Value, Math.Max(0, Entries.Count - visible)); scroll.Visible = Entries.Count > visible; Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(Color.White); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float s = DisplayScale; int w = Width - (scroll.Visible ? scroll.Width : 0) - 3; int end = Math.Min(Entries.Count, scroll.Value + Height / RowHeight + 1);
            if (Entries.Count == 0) { TextRenderer.DrawText(e.Graphics, Ui.T(settings, "没有排除项。", "No excluded files."), Font, ClientRectangle, Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); return; }
            for (int i = scroll.Value; i < end; i++) {
                SourceEntry entry = Entries[i]; int y = (i - scroll.Value) * RowHeight;
                if (Focused && selected == i) using (Brush bg = new SolidBrush(Theme.Mist)) e.Graphics.FillRectangle(bg, 0, y, w, RowHeight);
                RectangleF circle = new RectangleF(5 * s, y + RowHeight / 2 - 8 * s, 16 * s, 16 * s);
                using (Brush b = new SolidBrush(!entry.Excluded ? Theme.BlueDark : Color.White)) e.Graphics.FillEllipse(b, circle);
                using (Pen p = new Pen(entry.CannotInclude ? Color.LightGray : Theme.Line, 1.2f)) e.Graphics.DrawEllipse(p, circle);
                if (!entry.Excluded) using (Pen p = new Pen(Color.White, 1.8f * s)) e.Graphics.DrawLines(p, new PointF[] { new PointF(8 * s, circle.Top + 8 * s), new PointF(12 * s, circle.Top + 12 * s), new PointF(18 * s, circle.Top + 4 * s) });
                TextRenderer.DrawText(e.Graphics, entry.RelativePath, Font, new Rectangle((int)(31 * s), y + (int)(3 * s), w - (int)(37 * s), (int)(22 * s)), entry.Sensitive ? Color.FromArgb(155, 111, 33) : Theme.Ink, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
                string reason = entry.Excluded ? Reason(entry) : Ui.T(settings, "已加入", "Included");
                using (Font small = Ui.Font(8f, false)) TextRenderer.DrawText(e.Graphics, Ui.Size(entry.Length) + " · " + reason, small, new Rectangle((int)(31 * s), y + (int)(24 * s), w - (int)(37 * s), (int)(18 * s)), Theme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                using (Pen line = new Pen(Color.FromArgb(239, 242, 243))) e.Graphics.DrawLine(line, 30 * s, y + RowHeight - 1, w, y + RowHeight - 1);
            }
        }
        private string Reason(SourceEntry entry) {
            if (settings.Language == "en") return String.IsNullOrWhiteSpace(entry.Reason) ? "Excluded by choice" : entry.Reason;
            string reason = entry.Reason ?? "";
            if (entry.CannotInclude) return "链接或不可安全读取，不能加入";
            if (entry.Sensitive) return "可能含密钥或账户信息（默认排除）";
            if (reason.IndexOf("20", StringComparison.OrdinalIgnoreCase) >= 0) return "单个文件超过 20 MB（默认排除）";
            if (reason.IndexOf("directory", StringComparison.OrdinalIgnoreCase) >= 0 || reason.IndexOf("folder", StringComparison.OrdinalIgnoreCase) >= 0) return "位于默认跳过的文件夹";
            if (reason.IndexOf("extension", StringComparison.OrdinalIgnoreCase) >= 0 || reason.IndexOf("binary", StringComparison.OrdinalIgnoreCase) >= 0) return "默认跳过的构建产物或压缩包";
            return String.IsNullOrWhiteSpace(reason) ? "手动排除" : reason;
        }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); int index = scroll.Value + e.Y / RowHeight; if (index >= 0 && index < Entries.Count) { selected = index; if (Toggle != null) Toggle(Entries[index]); } }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); int max = Math.Max(0, Entries.Count - Math.Max(1, Height / RowHeight)); scroll.Value = Math.Max(0, Math.Min(max, scroll.Value - Math.Sign(e.Delta) * 3)); }
        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Space || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e) {
            if (e.KeyCode == Keys.Space && selected >= 0 && selected < Entries.Count && Toggle != null) { Toggle(Entries[selected]); e.Handled = true; }
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) { selected = Math.Max(0, Math.Min(Entries.Count - 1, selected + (e.KeyCode == Keys.Down ? 1 : -1))); if (selected < scroll.Value) scroll.Value = selected; else if (selected >= scroll.Value + Math.Max(1, Height / RowHeight)) scroll.Value = Math.Max(0, selected - Height / RowHeight + 1); e.Handled = true; Invalidate(); }
            base.OnKeyDown(e);
        }
    }
}
