using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClaudeCompanion {
    internal sealed class ComposeForm : Form {
        private readonly WindowChoice target;
        private bool offline;
        private readonly bool isPreview;
        private readonly Worker worker = new Worker();
        private readonly WaitTicker wait = new WaitTicker();
        private TextBox chinese, english;
        private Label titleLabel, subtitleLabel, targetLabel, chineseLabel, englishLabel, status;
        private Button translate, copy, send, offlineRetry;
        private string pendingRetryText;
        private int pendingRetryRevision;
        private string conversation = "", translatedFor = "";
        private string targetTitle = "", targetStatusZh = "正在确认目标聊天…", targetStatusEn = "Checking target chat…";
        private string statusZh, statusEn;
        private bool busy, sending, closed, uncertain, waitingTranslation;
        private DateTime waitStartedAt;
        private int revision;
        private Process helper;
        public ComposeForm(WindowChoice choice, bool preview = false) {
            SuspendLayout();
            Ui.ApplyIcon(this); AutoScaleDimensions=new SizeF(96F,96F); AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 10);
            isPreview=preview;
            offline = Preferences.Offline;
            statusZh = offline ? "离线翻译仅在本机处理原文。确认英文后，点击发送。" : "原文会发送给 Google 翻译。确认英文后，点击发送。";
            statusEn = offline ? "Offline translation stays on this device. Review the English before sending." : "Your text will be sent to Google Translate. Review the English before sending.";
            target = choice; Size = new Size(590, 740); MinimumSize = new Size(540, 650);
            BackColor = Theme.Cream; ForeColor = Theme.Ink;
            StartPosition = FormStartPosition.CenterScreen; TopMost = true;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, Padding = new Padding(22, 18, 22, 18) };
            foreach (float h in new float[] { 76, 32, 25 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            var titleRow = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            titleRow.Controls.Add(new WhaleBadge { Location=new Point(0,0), Size = new Size(96, 68) });
            titleLabel = new Label { AutoSize = true, Font = new Font(Font.FontFamily, 20, FontStyle.Bold), Location=new Point(108,5) };
            subtitleLabel = new Label { AutoSize=true, ForeColor=Theme.Muted, Location=new Point(111,40) };
            titleRow.Controls.Add(titleLabel); titleRow.Controls.Add(subtitleLabel); layout.Controls.Add(titleRow, 0, 0);
            targetLabel = new Label { Dock = DockStyle.Fill, ForeColor = Theme.Muted }; layout.Controls.Add(targetLabel, 0, 1);
            chineseLabel = LabelFor(""); layout.Controls.Add(chineseLabel, 0, 2);
            chinese = Editor(); chinese.TextChanged += delegate { ++revision; translatedFor = ""; send.Enabled = false; ClearOfflineRetry(); }; layout.Controls.Add(Card(chinese), 0, 3);
            var translationActions=ActionRow();
            translate = ButtonFor("", true);translate.Width=180;translate.Click += async delegate { await Translate(); };
            offlineRetry=ButtonFor("",false);offlineRetry.Width=165;offlineRetry.Visible=false;offlineRetry.Click+=async delegate{await RetryOffline();};
            translationActions.Controls.Add(translate,0,0);translationActions.Controls.Add(offlineRetry,1,0);layout.Controls.Add(translationActions,0,4);
            englishLabel = LabelFor(""); layout.Controls.Add(englishLabel, 0, 5);
            english = Editor(); english.TextChanged += delegate { UpdateSend(); }; layout.Controls.Add(Card(english), 0, 6);
            var actions = ActionRow();
            copy = ButtonFor("", false); copy.Click += delegate { try { if (!String.IsNullOrWhiteSpace(english.Text)) { Clipboard.SetText(english.Text); SetStatus("英文已复制，可以手动粘贴。", "English copied. You can paste it manually."); } } catch { SetStatus("剪贴板忙，请重试。", "Clipboard is busy. Please try again."); } };
            send = ButtonFor("", true); send.Enabled = false; send.Width = 155; send.Click += async delegate { await Send(); };
            actions.Controls.Add(copy,0,0); actions.Controls.Add(send,1,0); layout.Controls.Add(actions, 0, 7);
            status = new Label { Dock = DockStyle.Fill, ForeColor = Theme.Muted }; layout.Controls.Add(status, 0, 8); Controls.Add(layout);
            Ui.LanguageChanged += OnLanguageChanged;
            RefreshLocalizedText();
            Shown += async delegate { if (!preview) await BindConversation(); };
            FormClosing += delegate { closed = true; ++revision; Ui.LanguageChanged -= OnLanguageChanged; wait.Dispose(); worker.Dispose(); if (helper != null) { try { helper.Kill(); } catch { } } };
            ResumeLayout(true);
        }
        public void RenderPreview(string path) {
            targetStatusZh = "发送目标：规划重构 · 演示"; targetStatusEn = "Send target: Refactoring Plan · Demo"; targetTitle = ""; RefreshTargetLabel();
            chinese.Text = "请先帮我检查这个视频，保留原始文件。";
            english.Text = "Please check this video first and keep the original file.";
            SetStatus(offline ? "演示预览 · 本机离线翻译 · 尚未翻译或发送。" : "演示预览 · Google 在线模式 · 尚未翻译或发送。",
                offline ? "Demo preview · Offline translation · No translation or sending performed." : "Demo preview · Google online mode · No translation or sending performed.");
            TopMost = false; ShowInTaskbar = false; Opacity = 0; Show(); Application.DoEvents();
            using (var bmp = new Bitmap(Width, Height)) { DrawToBitmap(bmp, new Rectangle(Point.Empty, Size)); bmp.Save(path); }
            Close();
        }
        private Label LabelFor(string text) { return new Label { Text = text, Dock = DockStyle.Fill, ForeColor = Theme.Muted, Margin = new Padding(0) }; }
        private Control Card(Control child) { return Theme.Card(child, new Padding(0, 0, 0, 8)); }
        private TextBox Editor() { return new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, BackColor = Color.White, ForeColor = ForeColor, BorderStyle = BorderStyle.None, Font = new Font(Font.FontFamily, 12), Margin = new Padding(0), MaxLength = 32000 }; }
        private Button ButtonFor(string text, bool primary) { var b=Theme.Button(text,primary); b.Width=135; b.Margin=new Padding(0,4,10,4); return b; }
        private TableLayoutPanel ActionRow() {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 3, RowCount = 1 };
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return row;
        }
        private void FitButton(Button button, int minimumWidth) {
            // Measure in design pixels, then use the already-scaled height on language changes.
            using (var font = new Font(button.Font.FontFamily, button.Font.SizeInPoints * 96F / 72F, button.Font.Style, GraphicsUnit.Pixel)) {
                int content = TextRenderer.MeasureText(button.Text, font, Size.Empty, TextFormatFlags.SingleLine).Width + 24;
                button.Width = (int)Math.Ceiling(Math.Max(minimumWidth, content) * (button.Height / 38F));
            }
        }
        private void OnLanguageChanged() {
            if (closed || IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke((Action)RefreshLocalizedText); } catch { } return; }
            RefreshLocalizedText();
        }
        private void RefreshLocalizedText() {
            if (closed || IsDisposed) return;
            Text = Ui.T("大肥鱼翻译 · 中文提问", "Whale Translator · Compose in Chinese");
            titleLabel.Text = Ui.T("中文写，英文发", "Write Chinese, send English");
            titleLabel.Font = new Font(Font.FontFamily, Ui.English ? 16 : 20, FontStyle.Bold);
            subtitleLabel.Text = offline ? Ui.T("本机离线翻译 · 先预览，再决定", "Offline translation · Review before sending")
                : Ui.T("Google 在线翻译 · 先预览，再决定", "Google Translate · Review before sending");
            chineseLabel.Text = Ui.T("你想说的话 · 中文", "Your message · Chinese");
            englishLabel.Text = Ui.T("英文预览 · 可以继续修改，确认后再发送", "English preview · Edit before sending");
            chinese.AccessibleName = Ui.T("中文问题", "Chinese message");
            english.AccessibleName = Ui.T("英文预览", "English preview");
            translate.Text = Ui.T("翻成英文", "Translate to English");
            offlineRetry.Text=Ui.T("改用离线翻译","Use offline");
            copy.Text = Ui.T("复制英文", "Copy English");
            send.Text = Ui.T("发送到 Claude", "Send to Claude");
            FitButton(translate,180); FitButton(offlineRetry,165); FitButton(copy,135); FitButton(send,155);
            RefreshTargetLabel();
            if (waitingTranslation) RefreshWaitStatus();
            else status.Text = Ui.T(statusZh, statusEn);
        }
        private void RefreshTargetLabel() {
            if (targetLabel == null) return;
            string text = Ui.T(targetStatusZh, targetStatusEn);
            if (!String.IsNullOrEmpty(targetTitle)) text += Ui.T("：", ": ") + targetTitle;
            targetLabel.Text = text;
        }
        private void SetTargetStatus(string zh, string en, string title = "") {
            targetStatusZh = zh; targetStatusEn = en; targetTitle = title; RefreshTargetLabel();
        }
        private void SetStatus(string zh, string en) {
            statusZh = zh; statusEn = en;
            if (!closed && status != null) status.Text = Ui.T(statusZh, statusEn);
        }
        private async Task BindConversation() {
            if (target.Handle == "0") { SetTargetStatus("未连接 Claude；仍可翻译并复制英文。", "Claude is not connected; you can still translate and copy English."); return; }
            try {
                var context = await CallHelper("--probe", null);
                if (closed) return;
                if (!Json.Bool(context, "ok")) { SetTargetStatus("无法确认聊天；仍可翻译并复制英文。", "Could not confirm the chat; you can still translate and copy English."); return; }
                conversation = Json.Str(context, "conversation");
                SetTargetStatus("发送目标", "Send target", Json.Str(context, "title")); UpdateSend();
            } catch { if (!closed) SetTargetStatus("未连接到聊天；仍可翻译并复制英文。", "Not connected to a chat; you can still translate and copy English."); }
        }
        private void UpdateSend() { if (send != null) send.Enabled = !busy && !sending && !uncertain && conversation.Length > 0 && translatedFor == chinese.Text && !String.IsNullOrWhiteSpace(english.Text); }
        private void ClearOfflineRetry(){pendingRetryText=null;pendingRetryRevision=0;if(offlineRetry!=null)offlineRetry.Visible=false;}
        private bool CanRetryOffline(){return !closed&&!offline&&!busy&&!sending&&pendingRetryText!=null&&pendingRetryRevision==revision&&pendingRetryText==chinese.Text;}
        private bool RecordOfflineRetry(string input,int startRevision,bool requestOffline,Exception error){
            var failure=error as TranslationFailure;
            if(closed||offline||requestOffline||startRevision!=revision||input!=chinese.Text||failure==null||(failure.Code!="rate_limited"&&failure.Code!="google_blocked"))return false;
            pendingRetryText=input;pendingRetryRevision=revision;offlineRetry.Visible=true;
            SetStatus("Google 暂不可用，点「改用离线翻译」继续这段。","Google is unavailable. Click Use offline to translate this message.");return true;
        }
        private static bool HasChineseEnglishPack(){foreach(var pair in LanguagePack.InstalledPacks())if(pair.From=="zh"&&pair.To=="en")return true;return false;}
        private bool ApplyOfflineRetry(string input,int startRevision){
            if(!CanRetryOffline()||startRevision!=revision||input!=pendingRetryText||!HasChineseEnglishPack())return false;
            ClearOfflineRetry();worker.Dispose();offline=true;++revision;translatedFor="";UpdateSend();RefreshLocalizedText();return true;
        }
        private async Task RetryOffline(){
            if(!CanRetryOffline()){ClearOfflineRetry();return;}
            string input=pendingRetryText;int startRevision=pendingRetryRevision;
            if(!HasChineseEnglishPack()){
                SetStatus("请安装「简体中文 → 英文」，然后再点「改用离线翻译」。","Install Chinese (Simplified) → English, then click Use offline again.");
                using(var manager=new LanguagePacksForm(isPreview)){manager.TopMost=TopMost;manager.ShowDialog(this);}
                if(CanRetryOffline())SetStatus("原文已保留；装好「简体中文 → 英文」后，点「改用离线翻译」。","Original retained. After installing Chinese (Simplified) → English, click Use offline.");
                return;
            }
            if(ApplyOfflineRetry(input,startRevision))await Translate();
        }
        private async Task Translate() {
            if (busy || sending || String.IsNullOrWhiteSpace(chinese.Text)) return;
            ClearOfflineRetry();translatedFor="";busy = true; translate.Enabled = false; UpdateSend(); int startRevision = revision; string input = chinese.Text;bool requestOffline=offline;
            waitingTranslation = true; waitStartedAt = DateTime.UtcNow;
            wait.Start(offline ? Ui.T("正在本机翻成英文…", "Translating to English on this device…")
                : Ui.T("正在通过 Google 翻成英文…", "Translating to English with Google…"), delegate { if (!closed) RefreshWaitStatus(); });
            try {
                string result = await worker.Translate(input, "zh_en", offline);
                StopTranslationWait();
                if (closed) return;
                if (startRevision != revision) { SetStatus("中文已变化，请重新翻译。", "The Chinese text changed. Please translate again."); return; }
                english.Text = result; translatedFor = input; uncertain = false;
                SetStatus(offline ? "离线翻译完成。请检查英文，点击发送后才会发给目标聊天。" : "Google 翻译完成。请检查英文，点击发送后才会发给目标聊天。",
                    offline ? "Offline translation complete. Review the English; click Send to send it to the target chat." : "Google translation complete. Review the English; click Send to send it to the target chat.");
            } catch (Exception ex) {
                StopTranslationWait();
                if (!closed&&startRevision==revision&&input==chinese.Text) {
                    if (offline) SetStatus(OfflineError(ex.Message, false), OfflineError(ex.Message, true));
                    else SetStatus(ex is InvalidOperationException ? ex.Message : "Google 翻译没有完成，请重试。", ex is InvalidOperationException ? EnglishError(ex.Message) : "Google Translate did not finish. Please try again.");
                    RecordOfflineRetry(input,startRevision,requestOffline,ex);
                }
                worker.Dispose();
            }
            finally { StopTranslationWait(); busy = false; if (!closed) { translate.Enabled = true; UpdateSend(); } }
        }
        private async Task Send() {
            if (sending || !send.Enabled) return;
            sending = true; translate.Enabled = false; english.ReadOnly = true; chinese.ReadOnly = true; UpdateSend();
            SetStatus("正在核对目标和草稿，然后发送一次…", "Checking the target and draft, then sending once…");
            try {
                var result = await CallHelper("--send", new { conversation = conversation, text = english.Text });
                if (closed) return;
                string state = Json.Str(result, "status");
                if (state == "sent") { SetStatus("英文已发送到 Claude。回复可在伴读窗口查看。", "English sent to Claude. You can read the reply in the companion window."); uncertain = true; }
                else if (state == "draft_only") { SetStatus("英文已填入 Claude 草稿，请到 Claude 手动发送。", "English was placed in Claude's draft. Send it manually in Claude."); uncertain = true; }
                else if (state == "busy") SetStatus("Claude 还在回复，请等完成后再发送。", "Claude is still responding. Wait for it to finish before sending.");
                else if (state == "draft_exists") SetStatus("Claude 已有草稿。请先处理草稿，再发送。", "Claude already has a draft. Resolve it before sending.");
                else if (state == "conversation_changed" || state == "changed") { SetStatus("Claude 已切换聊天。请关闭本窗口后重新打开中文提问。", "Claude switched chats. Close this window and reopen Compose in Chinese."); uncertain = true; }
                else if (state == "unsupported" || state == "no_editor") SetStatus("当前输入框不支持直接发送。请复制英文手动粘贴。", "This input field does not support direct sending. Copy and paste the English manually.");
                else { SetStatus("未能确认发送结果，请先到 Claude 检查；不会自动重发。", "The send result could not be confirmed. Check Claude first; it will not be resent automatically."); uncertain = true; }
            } catch { if (!closed) SetStatus("未能确认发送结果，请先到 Claude 检查；不会自动重发。", "The send result could not be confirmed. Check Claude first; it will not be resent automatically."); uncertain = true; }
            finally { sending = false; if (!closed) { translate.Enabled = true; english.ReadOnly = false; chinese.ReadOnly = false; UpdateSend(); } }
        }
        private async Task<Dictionary<string, object>> CallHelper(string mode, object input) {
            var p = new Process { StartInfo = new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ClaudeSender.exe"), mode + " " + target.Handle) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            } }; helper = p;
            try {
                p.Start(); var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync();
                if (input != null) { byte[] bytes = new UTF8Encoding(false).GetBytes(Json.Encode(input) + "\n"); await p.StandardInput.BaseStream.WriteAsync(bytes, 0, bytes.Length); await p.StandardInput.BaseStream.FlushAsync(); } p.StandardInput.Close();
                if (await Task.WhenAny(output, Task.Delay(18000)) != output) { try { p.Kill(); } catch { } throw new TimeoutException(); }
                return Json.Decode(await output);
            } finally { if (helper == p) helper = null; try { if (!p.HasExited) p.Kill(); } catch { } p.Dispose(); }
        }
        private void RefreshWaitStatus() {
            if (status == null || closed) return;
            int seconds = Math.Max(0, (int)(DateTime.UtcNow - waitStartedAt).TotalSeconds);
            if (offline) {
                if (seconds < 3) status.Text = Ui.T("正在本机翻成英文…", "Translating to English on this device…");
                else if (seconds < 20) status.Text = Ui.T("正在本机翻译… 已等 " + seconds + " 秒", "Translating locally… waited " + seconds + " seconds");
                else status.Text = Ui.T("正在本机翻译，加载模型可能较慢… 已等 " + seconds + " 秒", "Translating locally; loading the model may take time… waited " + seconds + " seconds");
            } else {
                if (seconds < 3) status.Text = Ui.T("正在通过 Google 翻成英文…", "Translating to English with Google…");
                else if (seconds < 20) status.Text = Ui.T("正在通过 Google 翻译… 已等 " + seconds + " 秒", "Translating with Google… waited " + seconds + " seconds");
                else status.Text = Ui.T("网络有点慢，还在等 Google… 已等 " + seconds + " 秒（最多 2 分钟）", "Google is taking a while… waited " + seconds + " seconds (up to 2 minutes)");
            }
        }
        private void StopTranslationWait() {
            waitingTranslation = false;
            wait.Stop();
        }
        private static string OfflineError(string message, bool english) {
            switch (message) {
                case "请选择已安装的离线翻译方向。":case "Choose an installed offline language pair.":
                    return english?"Choose an installed offline language pair.":"请选择已安装的离线翻译方向。";
                case "尚未安装这个方向的语言包，请点「离线语言包」。":case "This language pair is not installed. Open Offline packs.":
                    return english?"Install Chinese → English in Offline packs in the main window.":"请在主界面的「离线语言包」中安装「简体中文 → 英文」。";
                case "语言包无法加载，请在「离线语言包」中重新安装。":case "Cannot load this language pack. Reinstall it in Offline packs.":
                    return english?"Cannot load this language pack. Reinstall it in Offline packs.":"语言包无法加载，请在「离线语言包」中重新安装。";
                case "离线运行组件缺失，请重新打开单文件版。":case "Offline runtime is missing. Reopen the single-file app.":
                    return english?"Offline runtime is missing. Reopen the single-file app.":"离线运行组件缺失，请重新打开单文件版。";
                case "翻译组件缺失，请重新打开单文件版。":
                case "Translation component is missing. Reopen the single-file app.":
                    return english ? "Translation component is missing. Reopen the single-file app." : "翻译组件缺失，请重新打开单文件版。";
                case "本地翻译未完成，请缩短文字后重试。":
                case "Offline translation failed. Try a shorter selection.":
                    return english ? "Offline translation failed. Try a shorter selection." : "本地翻译未完成，请缩短文字后重试。";
                case "本地翻译组件启动失败。":
                case "Offline translation component failed to start.":
                    return english ? "Offline translation component failed to start." : "本地翻译组件启动失败。";
                case "本地翻译等待超时，请缩短文字后重试。":
                case "Offline translation timed out. Try a shorter selection.":
                    return english ? "Offline translation timed out. Try a shorter selection." : "本地翻译等待超时，请缩短文字后重试。";
                case "离线语言仅支持简体中文与英文。":
                case "Offline mode supports Simplified Chinese and English only.":
                    return english ? "Offline mode supports Simplified Chinese and English only." : "离线语言仅支持简体中文与英文。";
                case "离线模型缺失或损坏，请重新打开单文件版。":
                case "Offline model is missing or damaged. Reopen the single-file app.":
                    return english ? "Offline model is missing or damaged. Reopen the single-file app." : "离线模型缺失或损坏，请重新打开单文件版。";
                case "内容超过 32,000 字符，请分段翻译。":
                case "Text exceeds 32,000 characters. Translate smaller sections.":
                    return english ? "Offline translation accepts up to 32,000 characters. Translate smaller sections." : "离线翻译内容超过 32,000 字符，请分段翻译。";
                case "翻译组件尚未准备好。":
                case "Translation component is not ready.":
                case "找不到本机翻译组件，请保留完整软件文件夹。":
                case "Translation component is missing. Keep the complete application folder.":
                    return english ? "The offline translation component is not ready. Restart the application and try again." : "本机离线翻译组件尚未准备好，请重新启动软件后重试。";
                case "本机翻译超时，请重试。":
                case "本机翻译组件启动超时，请重试。":
                case "Translation component startup timed out. Try again.":
                    return english ? "Offline translation timed out. Retry with a shorter text." : "本机离线翻译超时，请缩短文本后重试。";
                case "翻译进程已退出，请重新开始。":
                case "Translation process stopped. Try again.":
                    return english ? "The offline translation process stopped. Please retry." : "本机离线翻译进程已退出，请重试。";
                case "翻译响应不匹配，请重新开始。":
                case "Translation response did not match. Try again.":
                    return english ? "The offline translation response did not match. Please retry." : "本机离线翻译响应不匹配，请重试。";
                default:
                    return english ? "Offline translation did not finish. Please retry." : "本机离线翻译没有完成，请重试。";
            }
        }
        private static string EnglishError(string message) {
            if (Ui.English && !ContainsCjk(message)) return message;
            switch (message) {
                case "翻译组件尚未准备好。": return "The translation component is not ready.";
                case "找不到本机翻译组件，请保留完整软件文件夹。": return "The local translation component is missing. Keep the full application folder.";
                case "Google 翻译组件启动失败。": return "The Google Translate component could not start.";
                case "翻译进程已退出，请重新开始。": return "The translation process exited. Please retry.";
                case "翻译响应不匹配，请重新开始。": return "The translation response did not match. Please retry.";
                case "等待 Google 超时，请检查网络后重试。": return "Google timed out. Check your connection and retry.";
                case "本机翻译超时，请重试。": return "The local translation timed out. Please retry.";
                case "连不上 Google，请检查网络或代理后重试。": return "Cannot connect to Google. Check your network or proxy and retry.";
                case "代理需要身份验证，请在代理软件中处理后重试。": return "The proxy requires authentication. Resolve it in your proxy app and retry.";
                case "Google 暂时限流，请过一两分钟再试。": return "Google is rate-limiting requests. Please retry in a minute or two.";
                case "Google 暂时限流，可改用已安装的离线语言包。": return "Google is rate limiting requests. You can use an installed offline pack.";
                case "Google 要求人机验证，请稍后再试或换条代理线路。": return "Google requires a CAPTCHA. Please retry later or use another proxy route.";
                case "Google 要求人机验证，可稍后再试或改用离线包。": return "Google requires verification. Try later or use an offline pack.";
                case "Google 拒绝了请求，请稍后再试。": return "Google rejected the request. Please try again later.";
                case "Google 返回了异常跳转，请稍后再试或检查代理。": return "Google returned an unexpected redirect. Try again later or check your proxy.";
                case "内容超过 32,000 字符，请分段翻译。": return "The text exceeds 32,000 characters. Translate it in smaller sections.";
                case "Google 翻译用时过长，请分段重试。": return "Google Translate took too long. Try smaller sections.";
                default: return "Google Translate is unavailable. Please try again.";
            }
        }
        private static bool ContainsCjk(string value) {
            if (String.IsNullOrEmpty(value)) return false;
            foreach (char c in value) if (c >= '\u3400' && c <= '\u9fff') return true;
            return false;
        }
    }
}
