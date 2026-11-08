using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WhaleClaudeHelper {
    internal sealed class ProcessCloseDialog : Form {
        private readonly bool demo;
        private readonly ProcessCloseService service;
        private bool busy, populated;
        private CancellationTokenSource cancellation;
        private ListView targets;
        private Label counts;
        private TextBox status;
        private Button normal, force, refresh, stop, done;
        internal bool AllTargetsClosed { get; private set; }
        private static string T(string zh, string en) { return Ui.T(zh, en); }

        internal ProcessCloseDialog(bool demoMode) : this(demoMode, null) { }
        internal ProcessCloseDialog(bool demoMode, ProcessCloseService injectedService) {
            demo = demoMode;
            // Demo does not construct a platform, inspect real processes, or dispatch an action.
            service = demo ? null : (injectedService ?? new ProcessCloseService());
            Text = T("关闭阻挡优化的程序", "Close apps before optimization") + (demo ? " · Demo" : "");
            Font = new Font("Microsoft YaHei UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Theme.Cream; ForeColor = Theme.Ink;
            ClientSize = new Size(850, 600); MinimumSize = new Size(700, 500);
            StartPosition = FormStartPosition.CenterParent;
            Theme.SetWindowIcon(this, true);
            Build();
            if (demo) ShowDemo();
            Shown += async delegate { if (!demo) await RefreshAsync(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (!busy) return;
                e.Cancel = true;
                if (cancellation != null) cancellation.Cancel();
                SetStatus(T("正在停止后续请求。已发出的关闭请求无法撤销，请稍候。", "Stopping further requests. Already-sent close requests cannot be undone; please wait."));
            };
        }

        private void Build() {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(18, 14, 18, 14) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 14, FontStyle.Bold), Text = T("先保存工作，再正常关闭", "Save your work, then close normally"), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = T(
                "仅处理当前 Windows 用户和会话中的 Claude.exe、chrome.exe、msedge.exe。\r\n勾选要关闭的进程，点击「正常关闭」。程序可能显示保存提示，请自行处理。等待后仍在运行的已选进程，才可另行确认强制结束。",
                "Only Claude.exe, chrome.exe and msedge.exe in your current Windows user/session are eligible.\r\nSelect processes and click Close normally. Handle any save prompts yourself. After the wait, remaining selected processes can be force-closed with a separate confirmation."), ForeColor = Theme.Muted }, 0, 1);
            counts = new Label { Dock = DockStyle.Fill, Text = T("尚未读取进程", "Processes have not been listed"), TextAlign = ContentAlignment.MiddleLeft };
            root.Controls.Add(counts, 0, 2);
            targets = new ListView { Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true, HideSelection = false, MultiSelect = false, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, ForeColor = Theme.Ink };
            targets.Columns.Add(T("进程名称", "Process name"), 190);
            targets.Columns.Add("PID", 90);
            targets.Columns.Add(T("处理状态", "Action status"), 460);
            targets.ItemChecked += delegate { UpdateButtons(); };
            targets.Resize += delegate { if (targets.Columns.Count == 3) targets.Columns[2].Width = Math.Max(180, targets.ClientSize.Width - 288); };
            root.Controls.Add(targets, 0, 3);
            status = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = Theme.Cream, ForeColor = Theme.Ink, Margin = new Padding(0, 8, 0, 4) };
            root.Controls.Add(status, 0, 4);
            var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
            foreach (float width in new[] { 24f, 24f, 15f, 15f, 22f }) buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
            normal = AddButton(buttons, 0, T("正常关闭", "Close normally"), true);
            force = AddButton(buttons, 1, T("强制结束…", "Force close…"), false);
            refresh = AddButton(buttons, 2, T("刷新", "Refresh"), false);
            stop = AddButton(buttons, 3, T("停止等待", "Stop wait"), false);
            done = AddButton(buttons, 4, T("返回", "Back"), false);
            normal.Click += async delegate { await CloseSelectedAsync(false); };
            force.Click += async delegate { await CloseSelectedAsync(true); };
            refresh.Click += async delegate { await RefreshAsync(); };
            stop.Click += delegate { if (cancellation != null) cancellation.Cancel(); };
            done.Click += delegate { if (!busy) { DialogResult = AllTargetsClosed ? DialogResult.OK : DialogResult.Cancel; Close(); } };
            root.Controls.Add(buttons, 0, 5);
            Controls.Add(root);
            SetStatus(T("打开此窗口不会关闭任何程序。", "Opening this dialog does not close any application."));
            UpdateButtons();
        }

        private static Button AddButton(TableLayoutPanel panel, int column, string text, bool primary) {
            Button button = Theme.Button(text, primary);
            button.Dock = DockStyle.Fill; button.Margin = new Padding(2, 3, 2, 3);
            panel.Controls.Add(button, column, 0);
            return button;
        }

        private List<ProcessCloseTarget> Selected() {
            return targets.CheckedItems.Cast<ListViewItem>().Select(x => x.Tag as ProcessCloseTarget).Where(x => x != null).ToList();
        }

        private void UpdateButtons() {
            if (normal == null || done == null) return;
            var selected = Selected();
            normal.Enabled = !demo && !busy && selected.Count > 0;
            force.Enabled = !demo && !busy && service != null && selected.Any(service.CanForce);
            refresh.Enabled = !demo && !busy;
            stop.Enabled = busy && cancellation != null;
            targets.Enabled = !busy;
            done.Enabled = !busy;
            done.Text = AllTargetsClosed ? T("继续优化", "Continue") : T("返回", "Back");
        }

        private void ShowDemo() {
            var snapshot = new ProcessCloseSnapshot();
            snapshot.Targets.Add(new ProcessCloseTarget(101, 1, "Claude.exe", "demo", 1));
            snapshot.Targets.Add(new ProcessCloseTarget(102, 2, "chrome.exe", "demo", 1));
            snapshot.Targets.Add(new ProcessCloseTarget(103, 3, "msedge.exe", "demo", 1));
            Populate(snapshot);
            SetStatus(T("演示数据。没有读取或关闭本机进程，所有关闭操作均禁用。", "Demo data. No local processes were read or closed; all close actions are disabled."));
        }

        private void Populate(ProcessCloseSnapshot snapshot) {
            var checkedKeys = new HashSet<string>(Selected().Select(x => x.Key), StringComparer.Ordinal);
            bool initial = !populated;
            targets.BeginUpdate();
            try {
                targets.Items.Clear();
                foreach (ProcessCloseTarget target in snapshot.Targets) {
                    bool canForce = service != null && service.CanForce(target);
                    var row = new ListViewItem(target.Name) { Tag = target, Checked = initial || checkedKeys.Contains(target.Key) };
                    row.SubItems.Add(target.Pid.ToString(CultureInfo.InvariantCulture));
                    row.SubItems.Add(canForce ? T("正常关闭后仍在运行；可单独确认强制结束", "Still running after normal close; eligible for force confirmation") : T("尚未正常关闭；只会处理勾选的进程", "Normal close required; only checked processes are targeted"));
                    targets.Items.Add(row);
                }
            } finally { targets.EndUpdate(); }
            populated = true;
            counts.Text = String.Join("   ·   ", new[] { "Claude.exe", "chrome.exe", "msedge.exe" }.Select(name => name + ": " + snapshot.Targets.Count(x => String.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture))) + T("  个进程", "  processes");
            AllTargetsClosed = snapshot.AllTargetsClosed;
            UpdateButtons();
        }

        private async Task RefreshAsync() {
            if (demo || busy) return;
            busy = true; AllTargetsClosed = false; cancellation = new CancellationTokenSource(); UpdateButtons();
            SetStatus(T("正在读取允许处理的进程…", "Listing eligible processes…"));
            try {
                ProcessCloseSnapshot snapshot = await Task.Run(() => service.Snapshot(cancellation.Token));
                Populate(snapshot);
                SetStatus(SnapshotText(snapshot));
            } catch (OperationCanceledException) { AllTargetsClosed = false; SetStatus(T("读取已取消，没有发出关闭请求。", "Listing cancelled. No close requests were sent.")); }
              catch { AllTargetsClosed = false; SetStatus(T("无法完整读取进程，请稍后刷新。", "The process list could not be completed. Refresh and try again.")); }
            finally { busy = false; cancellation.Dispose(); cancellation = null; UpdateButtons(); }
        }

        private async Task CloseSelectedAsync(bool forceClose) {
            if (demo || busy) return;
            List<ProcessCloseTarget> selected = Selected();
            if (forceClose) selected = selected.Where(service.CanForce).ToList();
            if (selected.Count == 0) return;
            if (forceClose) {
                using (Form confirmation = CreateForceConfirmation(selected)) if (confirmation.ShowDialog(this) != DialogResult.Yes) return;
            }
            busy = true; AllTargetsClosed = false; cancellation = new CancellationTokenSource(); UpdateButtons();
            SetStatus(forceClose ? T("正在结束已明确确认的进程并核验…", "Closing explicitly confirmed processes and checking the result…") : T("已开始正常关闭。请处理程序自己的保存提示；最多等待约 3 秒。", "Requesting normal close. Handle any save prompts in the apps; waiting about 3 seconds at most."));
            try {
                ProcessCloseBatchResult result = await Task.Run(() => forceClose ? service.ForceCloseAsync(selected, true, cancellation.Token) : service.CloseGracefullyAsync(selected, cancellation.Token));
                // Read back after the action. Newly started processes are listed unchecked and are never
                // silently added to the previous selection or to its force-close authorization.
                ProcessCloseSnapshot snapshot = await Task.Run(() => service.Snapshot());
                Populate(snapshot);
                SetStatus(ResultText(result) + "\r\n" + SnapshotText(snapshot));
            } catch { AllTargetsClosed = false; SetStatus(T("本次操作未完成，请刷新进程列表。已发出的请求无法撤销。", "The operation did not complete. Refresh the list. Already-sent requests cannot be undone.")); }
            finally { busy = false; cancellation.Dispose(); cancellation = null; UpdateButtons(); }
        }

        // The full target list remains reviewable even when a browser has hundreds of processes.
        // This form only returns a choice; it never holds a platform or performs a process action.
        internal static Form CreateForceConfirmation(IList<ProcessCloseTarget> selected) {
            var confirmation = new Form {
                Text = T("确认强制结束", "Confirm force close"), StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(740, 500), MinimumSize = new Size(700, 500),
                Font = new Font("Microsoft YaHei UI", 10f), AutoScaleMode = AutoScaleMode.Dpi,
                BackColor = Theme.Cream, ForeColor = Theme.Ink, MinimizeBox = false, MaximizeBox = false
            };
            Theme.SetWindowIcon(confirmation, true);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(18) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            string warning = T("强制结束以下 ", "Force-close these ") + selected.Count.ToString(CultureInfo.InvariantCulture) + T(" 个进程？\r\n所有未保存的工作可能丢失，下载和进行中的任务会中断。此操作无法撤销。请查看完整列表后选择。", " processes?\r\nUnsaved work may be lost. Downloads and active tasks will be interrupted. This cannot be undone. Review the full list before choosing.");
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = warning }, 0, 0);
            string summary = String.Join("   ·   ", selected.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => x.Key + ": " + x.Count().ToString(CultureInfo.InvariantCulture)));
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = summary, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            root.Controls.Add(new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = ScrollBars.Both, BackColor = Color.White, ForeColor = Theme.Ink, Text = String.Join("\r\n", selected.Select(x => x.ToString())) }, 0, 2);
            var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            Button cancel = AddButton(buttons, 0, T("取消，保留进程", "Cancel, keep processes"), true);
            Button confirm = AddButton(buttons, 1, T("确认强制结束", "Confirm force close"), false);
            cancel.DialogResult = DialogResult.No; confirm.DialogResult = DialogResult.Yes;
            confirmation.AcceptButton = cancel; confirmation.CancelButton = cancel;
            root.Controls.Add(buttons, 0, 3); confirmation.Controls.Add(root);
            return confirmation;
        }

        private static string SnapshotText(ProcessCloseSnapshot snapshot) {
            if (snapshot.AllTargetsClosed) return T("已核验：Claude、Chrome、Edge 均已退出。可以返回继续优化。", "Verified: Claude, Chrome and Edge have exited. Return to continue optimization.");
            string message = T("仍有 ", "Still running: ") + snapshot.Targets.Count.ToString(CultureInfo.InvariantCulture) + T(" 个可处理进程。新出现的进程不会自动关闭，请查看勾选项。", " eligible processes. Newly started processes are not closed automatically; review the selection.");
            if (snapshot.ExcludedCount > 0) message += "\r\n" + snapshot.ExcludedCount.ToString(CultureInfo.InvariantCulture) + T(" 个同名进程不属于当前用户/会话或为受保护进程，不能在此关闭。", " matching processes are outside the current user/session or are protected and cannot be closed here.");
            if (snapshot.Errors.Count > 0) message += "\r\n" + T("部分进程无法读取，尚不能确认全部退出。请刷新或自行检查。", "Some processes could not be read; full exit is not confirmed. Refresh or check them manually.");
            return message;
        }

        private static string ResultText(ProcessCloseBatchResult result) {
            string text = T("已发出关闭请求：", "Close requests sent: ") + result.RequestedCount + T("；已核验退出：", "; verified exited: ") + result.ExitedCount + T("；仍在运行：", "; remaining: ") + result.Remaining.Count + ".";
            if (result.Cancelled) text += "\r\n" + T("已停止后续请求/等待。已发出的请求无法撤销。", "Further requests/waiting stopped. Already-sent requests cannot be undone.");
            foreach (ProcessCloseIssue issue in result.Issues.Take(6)) text += "\r\n" + (issue.Target == null ? "" : issue.Target.ToString() + ": ") + IssueText(issue.Code);
            if (result.Issues.Count > 6) text += "\r\n" + T("另有未完成项目，请结合刷新后的进程列表检查。", "Additional items did not complete; review the refreshed process list.");
            return text;
        }

        private static string IssueText(string code) {
            switch (code) {
                case "no_window": return T("没有可正常关闭的窗口；等待后可另行确认强制结束。", "No closable window; force close requires a separate confirmation after waiting.");
                case "normal_close_required": return T("请先对这个进程执行正常关闭。", "Normal close must be attempted for this process first.");
                case "identity_changed": return T("进程身份已变化，未对替代进程操作。", "Process identity changed; the replacement was not targeted.");
                case "not_allowed_or_owned": return T("进程不在允许范围或不属于当前用户/会话。", "Process is not eligible or is outside the current user/session.");
                case "access_denied": return T("Windows 未允许此操作，请自行关闭该程序。", "Windows did not permit this action; close the app manually.");
                case "validation_failed": return T("无法核验进程身份，未继续操作。", "Process identity could not be verified; no further action was taken.");
                case "too_many_processes": return T("进程数量超过安全上限，请分批处理。", "The process count exceeds the limit; handle a smaller batch.");
                case "operation_busy": return T("已有操作正在进行。", "Another operation is running.");
                default: return T("关闭请求未完成，请刷新或自行关闭。", "The close request did not complete; refresh or close the app manually.");
            }
        }

        private void SetStatus(string text) { if (!IsDisposed) status.Text = text; }
        protected override void Dispose(bool disposing) {
            if (disposing && cancellation != null) cancellation.Cancel();
            base.Dispose(disposing);
        }
    }
}
