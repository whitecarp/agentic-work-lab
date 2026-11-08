using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClaudeCompanion {
    // This popup is a view over the main form's existing setting controls.
    // All writes go through their original handlers; it owns no preferences.
    internal sealed class SettingsPanelModel {
        internal Func<bool> Offline, Pinned;
        internal Func<object[]> Targets;
        internal Func<int> SelectedTarget;
        internal Func<bool> TargetEnabled;
        internal Action<bool> SetOffline, SetPinned, SetEnglish;
        internal Action<int> SelectTarget;
        internal Action ManagePacks, EditGlossary, OpenFavorites, CheckUpdates;
        internal Func<Task> CheckConnection;
    }

    internal sealed class SettingsPanel : Form {
        private readonly SettingsPanelModel model;
        private readonly ToolTip tips = new ToolTip { AutoPopDelay = 20000 };
        private readonly ThemedComboBox targets;
        private readonly RoundedButton online, offline, zh, en;
        private readonly SelectionSwitch pin;
        private readonly Button connection;
        private readonly Label connectionState;
        private bool synchronizing, testing, disposed, showingDetails;
        private string serviceText;
        private bool serviceError;
        private static string T(string zhText, string enText) { return Ui.T(zhText, enText); }

        internal SettingsPanel(SettingsPanelModel state) {
            model = state;
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9F); BackColor = Theme.Cream; ForeColor = Theme.Ink;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(310, 428); Padding = new Padding(16, 12, 16, 12); Ui.ApplyIcon(this);
            Text = T("设置", "Settings"); KeyPreview = true;
            var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 10, Margin = Padding.Empty };
            rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            for (int i = 0; i < 9; i++) rows.RowStyles.Add(new RowStyle(SizeType.Percent, 100F/9F));
            var title = new Label { Text = Text, Font = new Font(Font, FontStyle.Bold), ForeColor = Theme.Ink, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
            rows.Controls.Add(title, 0, 0);

            var mode = CreateRow(T("翻译方式", "Translation"));
            var modeButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty };
            offline = Segment(T("离线", "Offline"), 65); online = Segment(T("Google 在线", "Google"), 91);
            offline.Click += delegate { model.SetOffline(true); RefreshState(); };
            online.Click += delegate { model.SetOffline(false); RefreshState(); };
            modeButtons.Controls.Add(offline); modeButtons.Controls.Add(online); mode.Controls.Add(modeButtons, 1, 0); rows.Controls.Add(mode, 0, 1);
            var targetRow = CreateRow(T("译成", "Translate to"));
            targets = new ThemedComboBox { Dock = DockStyle.Fill, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 6, 0, 6), Font = new Font(Font.FontFamily, 9F) };
            targets.AccessibleName = T("目标语言或离线方向", "Target language or offline direction");
            targets.SelectedIndexChanged += delegate { if (!synchronizing && targets.SelectedIndex >= 0) { model.SelectTarget(targets.SelectedIndex); RefreshState(); } };
            targets.DropDownClosed += delegate { QueueCloseIfInactive(); };
            targetRow.Controls.Add(targets, 1, 0); rows.Controls.Add(targetRow, 0, 2);
            rows.Controls.Add(ActionRow(T("离线语言包", "Offline packs"), delegate { CloseThen(model.ManagePacks); }), 0, 3);
            rows.Controls.Add(ActionRow(T("术语词典", "Glossary"), delegate { CloseThen(model.EditGlossary); }), 0, 4);
            rows.Controls.Add(ActionRow(T("回复收藏本", "Saved replies"), delegate { CloseThen(model.OpenFavorites); }), 0, 5);

            var connectionRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            connectionRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            connectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62)); connectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            connection = Theme.Button(T("检测连接", "Test connection"), false); connection.Dock = DockStyle.Fill; connection.Margin = new Padding(0, 5, 6, 5); connection.Font = new Font(Font.FontFamily, 8.5F);
            connection.Click += async delegate { if (testing) return; testing = true; connection.Enabled = false; try { await model.CheckConnection(); } finally { testing = false; if (!disposed) { connection.Enabled = true; RefreshService(); } } };
            connectionState = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.Muted, Margin = Padding.Empty, Font = new Font(Font.FontFamily, 8F) };
            connectionState.Click += delegate { if (!String.IsNullOrWhiteSpace(serviceText)) { showingDetails = true; try { MessageBox.Show(this, serviceText, T("连接状态", "Connection status"), MessageBoxButtons.OK, serviceError ? MessageBoxIcon.Warning : MessageBoxIcon.Information); } finally { showingDetails = false; } } };
            connectionRow.Controls.Add(connection, 0, 0); connectionRow.Controls.Add(connectionState, 1, 0); rows.Controls.Add(connectionRow, 0, 6);
            rows.Controls.Add(ActionRow(T("检查更新", "Check updates"), delegate { CloseThen(model.CheckUpdates); }), 0, 7);
            var pinRow = CreateRow(T("窗口置顶", "Always on top"));
            pin = new SelectionSwitch { Text = "", Width = 40, Height = 28, Anchor = AnchorStyles.Right, Margin = Padding.Empty, AccessibleName = T("窗口置顶", "Always on top") };
            pin.Click += delegate { model.SetPinned(!model.Pinned()); TopMost = model.Pinned(); RefreshState(); };
            pinRow.Controls.Add(pin, 1, 0); rows.Controls.Add(pinRow, 0, 8);
            var languageRow = CreateRow(T("界面语言", "Language"));
            var languages = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty };
            en = Segment("English", 76); zh = Segment("中文", 60);
            en.Click += delegate { CloseThen(delegate { model.SetEnglish(true); }); }; zh.Click += delegate { CloseThen(delegate { model.SetEnglish(false); }); };
            languages.Controls.Add(en); languages.Controls.Add(zh); languageRow.Controls.Add(languages, 1, 0); rows.Controls.Add(languageRow, 0, 9);
            Controls.Add(rows); RefreshState(); ResumeLayout(true);
        }
        private RoundedButton Segment(string caption, int width) {
            var button = (RoundedButton)Theme.Button(caption, false); button.Width = width; button.Height = 28;
            button.Font = new Font(Font.FontFamily, 8F); button.Margin = new Padding(0, 6, 2, 4); return button;
        }
        private TableLayoutPanel CreateRow(string caption) {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 99)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty, AutoEllipsis = true }, 0, 0);
            return row;
        }
        private Control ActionRow(string caption, Action action) {
            var row = new Button { Text = caption + "    ›", AccessibleName = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat, BackColor = Theme.Cream, ForeColor = Theme.Ink, Cursor = Cursors.Hand, Margin = Padding.Empty, Padding = Padding.Empty, UseVisualStyleBackColor = false, Font = Font };
            row.FlatAppearance.BorderSize = 0; row.FlatAppearance.MouseOverBackColor = Theme.Mist; row.FlatAppearance.MouseDownBackColor = Theme.Blue;
            row.Click += delegate { action(); }; return row;
        }
        private void CloseThen(Action action) {
            var owner = Owner; Close();
            if (owner != null && !owner.IsDisposed && owner.IsHandleCreated) owner.BeginInvoke(action);
        }
        internal void RefreshState() {
            if (disposed) return;
            synchronizing = true;
            try {
                online.Primary = !model.Offline(); offline.Primary = model.Offline(); online.Invalidate(); offline.Invalidate();
                targets.Items.Clear(); targets.Items.AddRange(model.Targets());
                int index = model.SelectedTarget(); targets.SelectedIndex = index >= 0 && index < targets.Items.Count ? index : -1; targets.Enabled = model.TargetEnabled();
                pin.Selected = model.Pinned(); zh.Primary = !Ui.English; en.Primary = Ui.English; zh.Invalidate(); en.Invalidate();
                connection.Text = model.Offline() ? T("检测离线库", "Test offline") : T("检测连接", "Test connection");
            } finally { synchronizing = false; }
        }
        internal void UpdateService(string text, bool error) { serviceText = text; serviceError = error; RefreshService(); }
        private void RefreshService() {
            if (disposed) return;
            connectionState.Text = testing ? T("检测中…", "Testing…") : serviceError ? T("需检查 ›", "Check ›") : T("详情 ›", "Details ›");
            connectionState.ForeColor = serviceError ? Color.FromArgb(190, 72, 72) : Theme.BlueDark;
            tips.SetToolTip(connectionState, serviceText ?? T("尚未检测", "Not tested yet"));
        }
        protected override void OnDeactivate(EventArgs e) {
            base.OnDeactivate(e);
            QueueCloseIfInactive();
        }
        private void QueueCloseIfInactive() { if (IsHandleCreated && !disposed) BeginInvoke((Action)delegate { if (!disposed && !showingDetails && !targets.ChoicesOpen && !ContainsFocus && Form.ActiveForm != this) Close(); }); }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) { if (keyData == Keys.Escape) { Close(); return true; } return base.ProcessCmdKey(ref msg, keyData); }
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e); bool rounded = false;
            try { int choice = 2; rounded = DwmSetWindowAttribute(Handle, 33, ref choice, 4) == 0; } catch { }
            if (!rounded) using (var path = Theme.RoundedRect(new RectangleF(0, 0, Width, Height), 16)) Region = new Region(path);
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.RoundedRect(new RectangleF(.5F, .5F, Width - 1F, Height - 1F), 16))
            using (var pen = new Pen(Theme.Line)) e.Graphics.DrawPath(pen, path);
        }
        protected override void Dispose(bool disposing) { if (disposing && !disposed) { disposed = true; tips.Dispose(); } base.Dispose(disposing); }
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
