using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace WhalePack {
    public sealed class HandoffForm : Form {
        public bool PreviewOnly { get; set; }
        private readonly AppSettings settings;
        private readonly PackageResult result;
        private readonly TextBox[] fields = new TextBox[6];
        private Panel filesPanel;
        private Label prompt;
        private bool changed;
        private string T(string zh, string en) { return Ui.T(settings, zh, en); }
        public HandoffForm(AppSettings settings, PackageResult result) {
            this.settings = settings; this.result = result;
            if (settings.Handoff == null) settings.Handoff = new HandoffData();
            Ui.Setup(this, T("交接说明 · 大肥鱼打包助手", "Handoff note · Big Whale Pack"), 520, 640); MinimizeBox = false;
            Ui.Label(this, T("写给下一个 Claude 的交接说明", "A handoff for your next Claude"), 20, 16, 480, 34, 15f, true, Theme.Ink);
            Ui.Label(this, T("存为「交接说明.md」，和压缩包放在一起；各项可留空", "Saved as 交接说明.md beside the ZIPs. All fields are optional."), 20, 52, 480, 25, 8.2f, false, Theme.Muted);
            string[] zh = { "项目", "目标", "做到哪了", "已验证", "还没验证", "下一步" };
            string[] en = { "Project", "Goal", "Progress", "Verified", "Unverified", "Next" };
            string[] values = { settings.Handoff.Project, settings.Handoff.Goal, settings.Handoff.Progress, settings.Handoff.Verified, settings.Handoff.Unverified, settings.Handoff.Next };
            for (int i = 0; i < 6; i++) {
                int y = 88 + i * 43;
                Ui.Label(this, T(zh[i], en[i]), 20, y, 75, 33, 9f, false, Theme.Muted);
                RoundedCard card = new RoundedCard { Bounds = new Rectangle(100, y, 400, 35), BackColor = Color.White }; Controls.Add(card);
                TextBox field = new TextBox { Text = values[i] ?? "", Bounds = new Rectangle(11, 8, 376, 21), BorderStyle = BorderStyle.None, Font = Ui.Font(9f, false), ForeColor = Theme.Ink, MaxLength = 200000, Multiline = false };
                field.TextChanged += delegate { changed = true; }; fields[i] = field; card.Controls.Add(field);
            }
            RoundedCard outputs = new RoundedCard { Bounds = new Rectangle(20, 359, 480, 139), BackColor = Color.White }; Controls.Add(outputs);
            Ui.Label(outputs, result == null ? T("准备好再打包", "Ready when you are") : T("打包完成", "Packing complete"), 16, 9, 447, 25, 11f, true, Theme.Ink);
            Panel files = new Panel { Bounds = new Rectangle(14, 39, 450, 89), BackColor = Color.White, AutoScroll = true }; outputs.Controls.Add(files); filesPanel = files;
            if (result == null) {
                Ui.Label(files, T("填写后会自动记住，下次打开接着写。\n打包完成后，这里会列出文件名和实际大小。", "Your draft is remembered on this computer.\nAfter packing, actual files and sizes appear here."), 4, 2, 426, 61, 9f, false, Theme.Muted);
            } else {
                int y = 0;
                foreach (OutputFile f in result.Files) {
                    FileGlyph glyph = new FileGlyph { Mode = ProcessingMode.Original, Directory = f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase), Bounds = new Rectangle(3, y + 2, 23, 23) }; files.Controls.Add(glyph);
                    Ui.Label(files, f.Name, 36, y, 270, 27, 9f, false, Theme.Ink);
                    Label length = Ui.Label(files, Ui.Size(f.Length) + "  ✓", 306, y, 120, 27, 8.7f, false, Theme.BlueDark); length.TextAlign = ContentAlignment.MiddleRight; y += 29;
                }
                files.AutoScrollMinSize = new Size(0, y);
            }
            Ui.Label(this, T("复制给 Claude 的第一句话", "Your first message to Claude"), 20, 505, 480, 24, 8.5f, false, Theme.Muted);
            DropCard copyCard = new DropCard { Bounds = new Rectangle(20, 532, 480, 53) }; Controls.Add(copyCard);
            string copy = result == null ? T("打包完成后，会根据实际附件生成这句话。", "The message will list your actual attachments after packing.") : result.CopyText;
            prompt = Ui.Label(copyCard, copy, 14, 3, 453, 46, 8.6f, false, Theme.Ink); prompt.AutoEllipsis = true;
            if (result == null) {
                Ui.Label(this, T("只保存在本机", "Stored locally only"), 22, 597, 240, 31, 8.5f, false, Theme.Muted);
                Ui.Button(this, T("保存草稿", "Save draft"), 350, 596, 150, 33, true, delegate { Persist(); Close(); });
            } else {
                Ui.Button(this, T("打开文件夹", "Open folder"), 20, 596, 144, 33, false, delegate {
                    try { if (Directory.Exists(result.OutputDirectory)) Process.Start(new ProcessStartInfo(result.OutputDirectory) { UseShellExecute = true }); }
                    catch (Exception ex) { Ui.Error(this, settings, ex); }
                });
                Ui.Button(this, T("保存说明", "Save note"), 179, 596, 140, 33, false, delegate { settings.WriteHandoff = true; Persist(); SaveNote(); });
                Ui.Button(this, T("复制这句话", "Copy message"), 334, 596, 166, 33, true, delegate {
                    try { Clipboard.SetText(result.CopyText ?? ""); MessageBox.Show(this, T("已复制，可粘贴给 Claude。", "Copied. Paste it into Claude."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information); }
                    catch (Exception ex) { Ui.Error(this, settings, ex); }
                });
            }
            FormClosing += delegate { if (!PreviewOnly) { Persist(); if (changed && result != null && settings.WriteHandoff) SaveNote(); } };
            Shown += delegate { if (PreviewOnly) { foreach (TextBox field in fields) { field.SelectionStart = 0; field.SelectionLength = 0; } ActiveControl = null; } };
            ResumeLayout(false);
        }
        private void Persist() {
            if (PreviewOnly) return;
            settings.Handoff.Project = fields[0].Text; settings.Handoff.Goal = fields[1].Text; settings.Handoff.Progress = fields[2].Text;
            settings.Handoff.Verified = fields[3].Text; settings.Handoff.Unverified = fields[4].Text; settings.Handoff.Next = fields[5].Text;
            try { SettingsStore.Save(settings); } catch (Exception ex) { Ui.Error(this, settings, ex); }
        }
        private void SaveNote() {
            if (PreviewOnly || result == null) return;
            try { PackageEngine.WriteHandoff(result, settings); changed = false; RefreshOutputs(); } catch (Exception ex) { Ui.Error(this, settings, ex); }
        }
        private void RefreshOutputs() {
            foreach (Control c in filesPanel.Controls.Cast<Control>().ToArray()) c.Dispose(); filesPanel.Controls.Clear();
            float scale = filesPanel.Width / 450f; int y = 0;
            foreach (OutputFile file in result.Files) {
                Panel row = new Panel { Bounds = new Rectangle(0, 0, 430, 29), BackColor = Color.White };
                FileGlyph glyph = new FileGlyph { Mode = ProcessingMode.Original, Directory = file.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase), Bounds = new Rectangle(3, 2, 23, 23) }; row.Controls.Add(glyph);
                Ui.Label(row, file.Name, 36, 0, 270, 27, 9f, false, Theme.Ink);
                Label length = Ui.Label(row, Ui.Size(file.Length) + "  ✓", 306, 0, 120, 27, 8.7f, false, Theme.BlueDark); length.TextAlign = ContentAlignment.MiddleRight;
                row.Scale(new SizeF(scale, scale)); row.Top = y; y += row.Height; filesPanel.Controls.Add(row);
            }
            filesPanel.AutoScrollMinSize = new Size(0, y); prompt.Text = result.CopyText;
        }
    }
}
