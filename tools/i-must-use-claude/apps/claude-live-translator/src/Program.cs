using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly:System.Reflection.AssemblyVersion("0.9.6.0")]
[assembly:System.Reflection.AssemblyFileVersion("0.9.6.0")]

namespace ClaudeCompanion {
    internal static class Json {
        public static string Encode(object value) { return new JavaScriptSerializer { MaxJsonLength = 2000000 }.Serialize(value); }
        public static Dictionary<string, object> Decode(string value) { return new JavaScriptSerializer { MaxJsonLength = 2000000 }.Deserialize<Dictionary<string, object>>(value); }
        public static string Str(Dictionary<string, object> d, string key) { return d.ContainsKey(key) && d[key] != null ? Convert.ToString(d[key]) : ""; }
        public static bool Bool(Dictionary<string, object> d, string key) { return d.ContainsKey(key) && d[key] is bool && (bool)d[key]; }
    }
    internal sealed class Snapshot {
        public string Conversation, MessageId, Text;
        public string Direction = "en_zh";
        public bool Streaming;
        public IntPtr SourceHandle;
        public Rectangle SourceBounds;
        public List<Rectangle> Highlights = new List<Rectangle>();
        public DateTime ReadStartedAt;
        public HighlightTrackingInfo Tracking;
        public string Key { get { return Conversation + "\n" + MessageId; } }
    }
    internal static class HighlightGate {
        public static bool CanMark(Snapshot displayed, Snapshot current) {
            return displayed != null && current != null && displayed.Conversation != "manual" &&
                !String.IsNullOrWhiteSpace(displayed.Text) && ReadGate.SameReply(displayed, current) &&
                current.Text.StartsWith(displayed.Text, StringComparison.Ordinal);
        }
        public static Rectangle ReadRectangle(Dictionary<string, object> row) {
            try {
                double x=Convert.ToDouble(row["x"]), y=Convert.ToDouble(row["y"]), width=Convert.ToDouble(row["width"]), height=Convert.ToDouble(row["height"]);
                if (Double.IsNaN(x+y+width+height) || Double.IsInfinity(x+y+width+height) || Math.Abs(x)>100000 || Math.Abs(y)>100000 || width<1 || height<1 || width>20000 || height>20000) return Rectangle.Empty;
                return new Rectangle((int)x,(int)y,(int)width,(int)height);
            } catch { return Rectangle.Empty; }
        }
        public static void AddGeometry(Snapshot shot, Dictionary<string, object> data, string handle, DateTime started) {
            try { shot.SourceHandle = new IntPtr(Convert.ToInt64(handle.StartsWith("0x",StringComparison.OrdinalIgnoreCase) ? handle.Substring(2) : handle, handle.StartsWith("0x",StringComparison.OrdinalIgnoreCase) ? 16 : 10)); } catch { return; }
            shot.ReadStartedAt = started;
            object ids;
            var anchors = new List<int[]>();
            if(data.TryGetValue("highlightAnchors", out ids) && ids is System.Collections.IEnumerable) {
                foreach(object item in (System.Collections.IEnumerable)ids) { int[] id=RuntimeId(item); if(id.Length>0)anchors.Add(id); if(anchors.Count>512){anchors.Clear();break;} }
            }
            object documentId, articleId, feedId; data.TryGetValue("documentRuntimeId", out documentId); data.TryGetValue("articleRuntimeId", out articleId); data.TryGetValue("feedRuntimeId", out feedId);
            shot.Tracking=new HighlightTrackingInfo { Anchors=anchors.ToArray(),DocumentRuntimeId=RuntimeId(documentId),ArticleRuntimeId=RuntimeId(articleId),FeedRuntimeId=RuntimeId(feedId),ConversationUrl=Json.Str(data,"conversationUrl") };
            object bounds, rows;
            if(data.TryGetValue("windowBounds", out bounds) && bounds is Dictionary<string,object>) shot.SourceBounds=ReadRectangle((Dictionary<string,object>)bounds);
            if(data.TryGetValue("highlights", out rows) && rows is System.Collections.IEnumerable) {
                foreach(object row in (System.Collections.IEnumerable)rows) {
                    if(shot.Highlights.Count>=512) { shot.Highlights.Clear(); break; }
                    var map=row as Dictionary<string,object>; if(map==null)continue;
                    Rectangle rectangle=ReadRectangle(map); if(!rectangle.IsEmpty)shot.Highlights.Add(rectangle);
                }
            }
        }
        private static int[] RuntimeId(object raw) {
            var result=new List<int>(); var values=raw as System.Collections.IEnumerable;
            if(values==null||raw is string)return result.ToArray();
            try { foreach(object part in values){result.Add(Convert.ToInt32(part));if(result.Count>32)return new int[0];} } catch { return new int[0]; }
            return result.ToArray();
        }
    }
    internal static class ResultGate {
        public static bool CanShow(int requestEpoch, int currentEpoch, Snapshot request, Snapshot current) {
            return requestEpoch == currentEpoch && current != null && request.Key == current.Key && request.Direction == current.Direction &&
                (request.Text == current.Text || current.Text.StartsWith(request.Text, StringComparison.Ordinal));
        }
    }
    internal static class ReadGate {
        // A failed read (Claude minimized, a UI hiccup, a question just sent) must not wipe the
        // translation the user is reading. Clear only when Claude is known to show another chat.
        public static bool ShouldClear(Snapshot latest, string conversation) {
            return latest == null || (!String.IsNullOrEmpty(conversation) && latest.Conversation != conversation);
        }
        // Same reply, including the moment a streaming reply is finalized and gets its message number.
        public static bool SameReply(Snapshot previous, Snapshot current) {
            if (previous == null || current == null) return false;
            if (previous.Key == current.Key) return true;
            return previous.Conversation == current.Conversation && previous.MessageId == "streaming" && current.MessageId != "streaming" &&
                current.Text.StartsWith(previous.Text, StringComparison.Ordinal);
        }
    }
    internal static class ReaderStatus {
        public static string Describe(string reason, bool hasPrevious) {
            string suffix = hasPrevious ? " · 显示的是上一条译文" : "";
            switch (reason) {
                case "waiting": return "等待 Claude 的文字回复…" + suffix;
                case "conversation_document_not_found": return "未识别到聊天页面，请在 Claude 打开普通聊天或 Code 会话" + suffix;
                case "message_feed_not_found": return "聊天内容尚未加载，请保持 Claude 未最小化" + suffix;
                case "messages_not_found": return "没有可读取的消息，请在 Claude 打开已有会话" + suffix;
                case "visible_reply_not_found": return "当前画面没有可读取的回复，滚动到要看的文字后会更新" + suffix;
                case "message_too_long_split_and_paste": return "这条回复超过 32,000 字符，请分段复制后粘贴翻译" + suffix;
                case "window_not_found": return "Claude 窗口已关闭，请点刷新重新选择" + suffix;
                case "window_minimized": return "请恢复 Claude 窗口" + suffix;
                default: return "暂时读不到 Claude，请保持聊天窗口未最小化" + suffix;
            }
        }
        public static string Failure(Exception error) {
            if (error is TimeoutException) return "读取 Claude 超时，稍后自动重试";
            if (error is FileNotFoundException) return "缺少 ClaudeReader.exe，请保留完整软件文件夹";
            if (error is System.ComponentModel.Win32Exception) return "读取组件启动失败，请检查软件是否被系统拦截";
            return "读取组件返回异常，可点刷新重试或粘贴翻译";
        }
    }
    internal static class SelectionStatus {
        public static string Describe(string reason,bool hasPrevious) {
            string suffix=hasPrevious?" · 保留上一段译文":"";
            switch(reason) {
                case "password_denied": return "密码输入框不会读取"+suffix;
                case "text_too_long": return "选中文字超过 32,000 字符，请少选一些"+suffix;
                case "foreground_changed": case "not_foreground": return "窗口已切换，请在原文处重新划选"+suffix;
                case "ambiguous_selection": return "有多处选区，请只选中一段文字"+suffix;
                case "scan_limit": return "页面内容较多，选区读取未完成；请重新划选"+suffix;
                case "claude_filter_unavailable": return "暂时无法识别这页的正文选区；可复制后点「粘贴翻译」"+suffix;
                case "claude_document_not_found": return "未识别到所选文字所属的聊天页面；可重新划选"+suffix;
                case "pattern_unavailable": return "这个位置没有提供文字选区；可复制后点「粘贴翻译」"+suffix;
                case "uia_read_failed": return "选区接口暂未响应；请重新划选"+suffix;
                default: return "未读到选中文字；可复制后点「粘贴翻译」"+suffix;
            }
        }
    }
    internal static class SelectionRetry {
        // A newly painted blue selection can reach the accessibility provider a little later.
        // Never retry a security refusal, a changed source window, or an ambiguous range.
        internal static bool CanRetry(string reason,int attempt) {
            return attempt==0 && (reason=="no_selection" || reason=="pattern_unavailable" || reason=="uia_read_failed");
        }
    }
    internal static class SelectionDiagnostics {
        internal static Dictionary<string,object> SafeFields(Dictionary<string,object> data) {
            var safe=new Dictionary<string,object>();
            safe["status"]=Json.Str(data,"status");
            object raw;
            if(data.TryGetValue("diagnostics",out raw)) {
                var fields=raw as Dictionary<string,object>;
                if(fields!=null)foreach(string key in new[]{"route","source","phase","nodesScanned","patternProviders","selectionCandidates","filterVisited","filterPieces","pointOutcome","pointTypes","pointAncestors","pointPatterns","pointRanges","pointLength","focusOutcome","focusTypes","focusAncestors","focusPatterns","focusRanges","focusLength","readFailures","lastReadStatus"}) {
                    object value;if(fields.TryGetValue(key,out value)&&(value is string||value is int||value is long||value is double))safe[key]=value;
                }
            }
            return safe;
        }
        internal static void Save(Dictionary<string,object> data,int attempt) {
            try {
                var safe=SafeFields(data);safe["attempt"]=attempt;safe["version"]=Program.Version;
                string folder=Path.Combine(Preferences.DataDirectory,"diagnostics");
                Directory.CreateDirectory(folder);
                // One bounded diagnostic record only: never selected text, title, URL, or credentials.
                File.WriteAllText(Path.Combine(folder,"selection-last.json"),Json.Encode(safe),new UTF8Encoding(false));
            } catch { }
        }
    }
    // Shows how long a Google request has been pending, so a slow network does not look frozen.
    internal sealed class WaitTicker : IDisposable {
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 1000 };
        private DateTime started;
        private string message;
        private bool offline;
        private Action<string> show;
        public WaitTicker() { timer.Tick += delegate { if (show == null) { timer.Stop(); return; } show(Describe(message, (int)(DateTime.UtcNow - started).TotalSeconds,offline)); }; }
        public void Start(string text, Action<string> sink,bool offlineMode=false) { offline=offlineMode; message = text; show = sink; started = DateTime.UtcNow; sink(text); timer.Start(); }
        public void Stop() { show = null; timer.Stop(); }
        public void RefreshMessage(string value) { message=value; }
        internal static string Describe(string message, int seconds,bool offlineMode=false) {
            if (seconds < 3) return message;
            if(offlineMode)return message+Ui.T(" 已等 "+seconds+" 秒 · 本机处理，不联网"," Waited "+seconds+" seconds · On this device, no network");
            if (seconds < 20) return message + Ui.T(" 已等 " + seconds + " 秒", " Waited " + seconds + " seconds");
            return Ui.T("网络有点慢，还在等 Google… 已等 " + seconds + " 秒（最多 2 分钟）", "Google is taking a while… waited " + seconds + " seconds (up to 2 minutes)");
        }
        public void Dispose() { Stop(); timer.Dispose(); }
    }
    internal static class TextBoxScroll {
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE, EM_LINESCROLL = 0x00B6;
        // Replacing TextBox.Text jumps to the top; keep the reader where they were while a reply grows.
        public static void SetKeepingScroll(TextBox box, string text) {
            if (box.Text == text) return;
            if (!box.IsHandleCreated) { box.Text = text; return; }
            int first = 0;
            try { first = SendMessage(box.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32(); } catch { }
            box.Text = text;
            if (first > 0) { try { SendMessage(box.Handle, EM_LINESCROLL, IntPtr.Zero, new IntPtr(first)); } catch { } }
        }
    }
    internal sealed class WindowChoice {
        public string Handle, Title;
        public override string ToString() { return Title; }
    }
    internal sealed class TranslationFailure : InvalidOperationException {
        internal readonly string Code;
        internal TranslationFailure(string code) : base(Worker.ErrorMessage(code)) { Code=code; }
    }
    internal sealed class Worker : IDisposable {
        private Process process;
        private int sequence;
        private bool processOffline;
        public async Task<string> Translate(string text, string direction = "en_zh",bool? offline=null) {
            bool local=offline??Preferences.Offline;
            if(local&&direction!="en_zh"&&direction!="zh_en"&&direction!="auto:zh-CN"&&direction!="auto:en"&&!System.Text.RegularExpressions.Regex.IsMatch(direction,"^pair:[a-z]{2,3}:[a-z]{2,3}$"))throw new InvalidOperationException(ErrorMessage("offline_language"));
            if(process!=null&&local!=processOffline)Dispose();
            if (process == null || process.HasExited) {
                Dispose();
                processOffline=local;
                string root = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(root, "backend", "launcher.json");
                if (!File.Exists(configPath)) throw new InvalidOperationException(Ui.T("翻译组件尚未准备好。","Translation component is not ready."));
                var config = Json.Decode(File.ReadAllText(configPath, Encoding.UTF8));
                string python = Path.GetFullPath(Path.Combine(root, Json.Str(config, "python")));
                string script = Path.GetFullPath(Path.Combine(root, local?"backend/offline_worker.py":Json.Str(config, "script")));
                if (!File.Exists(python) || !File.Exists(script)) throw new InvalidOperationException(Ui.T("翻译组件缺失，请重新打开单文件版。","Translation component is missing. Reopen the single-file app."));
                var start = new ProcessStartInfo(python, "-B -u \"" + script + "\"") {
                    WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                start.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                start.EnvironmentVariables["PYTHONUTF8"] = "1";
                process = new Process { StartInfo = start };
                process.ErrorDataReceived += delegate { /* No chat or diagnostic text written to disk. */ };
                process.Start(); process.BeginErrorReadLine();
                string ready = await ReadLine(10000, Ui.T("本机翻译组件启动超时，请重试。","Translation component startup timed out. Try again."));
                if (ready == null || !Json.Bool(Json.Decode(ready), "ready")) throw new InvalidOperationException(local?Ui.T("本地翻译组件启动失败。","Offline translation component failed to start."):Ui.T("Google 翻译组件启动失败。","Google translation component failed to start."));
            }
            int id = ++sequence;
            // Explicit UTF-8 bytes; Windows console encodings must not corrupt Chinese input.
            byte[] bytes = new UTF8Encoding(false).GetBytes(Json.Encode(new { id = id, text = text, direction = direction, glossary=GlossaryStore.Load() }) + "\n");
            await process.StandardInput.BaseStream.WriteAsync(bytes, 0, bytes.Length);
            await process.StandardInput.BaseStream.FlushAsync();
            // Allow the worker's 120-second deadline plus one in-flight 15-second request.
            string line = await ReadLine(140000,local?Ui.T("本地翻译等待超时，请缩短文字后重试。","Offline translation timed out. Try a shorter selection."):null);
            if (line == null) throw new InvalidOperationException(Ui.T("翻译进程已退出，请重新开始。","Translation process stopped. Try again."));
            var response = Json.Decode(line);
            if (Json.Str(response, "id") != id.ToString()) throw new InvalidOperationException(Ui.T("翻译响应不匹配，请重新开始。","Translation response did not match. Try again."));
            if(response.ContainsKey("error")){
                string code=Json.Str(response,"error");
                if(!local&&code=="translation_failed")throw new InvalidOperationException(Ui.T("Google 翻译未完成，请重试。","Google translation failed. Try again."));
                throw new TranslationFailure(code);
            }
            return Json.Str(response, "translation");
        }
        internal static string ErrorMessage(string code) {
            switch (code) {
                case "offline_language": case "unsupported_language": case "offline_unsupported_target": return Ui.T("请选择已安装的离线翻译方向。","Choose an installed offline language pair.");
                case "offline_model_missing": return Ui.T("尚未安装这个方向的语言包，请点「离线语言包」。","This language pair is not installed. Open Offline packs.");
                case "model_unavailable": case "engine_load_failed": case "offline_model_load_failed": return Ui.T("语言包无法加载，请在「离线语言包」中重新安装。","Cannot load this language pack. Reinstall it in Offline packs.");
                case "offline_runtime_unavailable": return Ui.T("离线运行组件缺失，请重新打开单文件版。","Offline runtime is missing. Reopen the single-file app.");
                case "offline_output_limit": case "offline_deadline": return Ui.T("本地翻译等待超时，请缩短文字后重试。","Offline translation timed out. Try a shorter selection.");
                case "translation_failed": return Ui.T("本地翻译未完成，请缩短文字后重试。","Offline translation failed. Try a shorter selection.");
                case "network_error": return Ui.T("连不上 Google，请检查网络或代理后重试。","Cannot reach Google. Check your network or proxy.");
                case "proxy_authentication": return Ui.T("代理需要身份验证，请在代理软件中处理后重试。","Your proxy requires authentication. Check your proxy app.");
                case "rate_limited": return Ui.T("Google 暂时限流，可改用已安装的离线语言包。","Google is rate limiting requests. You can use an installed offline pack.");
                case "google_blocked": return Ui.T("Google 要求人机验证，可稍后再试或改用离线包。","Google requires verification. Try later or use an offline pack.");
                case "access_denied": return Ui.T("Google 拒绝了请求，请稍后再试。","Google denied the request. Try again later.");
                case "unexpected_redirect": return Ui.T("Google 返回了异常跳转，请稍后再试或检查代理。","Google returned an unexpected redirect. Check your proxy or try later.");
                case "text_too_long": return Ui.T("内容超过 32,000 字符，请分段翻译。","Text exceeds 32,000 characters. Translate smaller sections.");
                case "deadline": return Ui.T("Google 翻译用时过长，请分段重试。","Google took too long. Try a smaller selection.");
                case "invalid_direction": return Ui.T("请选择支持的目标语言。","Choose a supported target language.");
                case "invalid_glossary": return Ui.T("术语词典格式有误，请检查术语条目。","The glossary format is invalid. Check its entries.");
                default: return Ui.T("Google 翻译暂不可用，请稍后重试。","Google Translate is unavailable. Try again later.");
            }
        }
        private async Task<string> ReadLine(int timeout, string timeoutMessage = null) {
            if(timeoutMessage==null)timeoutMessage=Ui.T("等待 Google 超时，请检查网络后重试。","Google request timed out. Check your network and try again.");
            var read = process.StandardOutput.ReadLineAsync();
            if (await Task.WhenAny(read, Task.Delay(timeout)) != read) { Dispose(); throw new InvalidOperationException(timeoutMessage); }
            return await read;
        }
        public void Dispose() {
            var old = process; process = null;
            if (old != null) { try { if (!old.HasExited) old.Kill(); } catch { } old.Dispose(); }
        }
    }
    internal static class Program {
        internal const string Version = "0.9.6";
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
        [STAThread] static void Main(string[] args) {
            // All forms use a 96-DPI design baseline; let WinForms scale once.
            try { SetProcessDPIAware(); } catch (EntryPointNotFoundException) { }
            SetCurrentProcessExplicitAppUserModelID("Dafeiyu.Translator.Desktop");
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Preferences.Load();
            if(args.Length==2&&(args[0]=="--self-test-offline"||args[0]=="--self-test-online")) { TranslationSelfTest(args[1],args[0]=="--self-test-offline");return; }
            if(args.Length==2&&args[0]=="--self-test-packs"){
                var report=new Dictionary<string,object>();report["version"]=Version;
                try{var result=PackService.Run("--list",null,null,CancellationToken.None).GetAwaiter().GetResult();report["ok"]=Json.Bool(result,"ok");report["catalogCount"]=LanguagePack.ReadList(result,"catalog").Count;report["installedCount"]=LanguagePack.ReadList(result,"installed").Count;report["uiInstalledCount"]=LanguagePack.InstalledPacks().Count;}
                catch(Exception ex){report["ok"]=false;report["error"]=ex.Message;}
                File.WriteAllText(args[1],Json.Encode(report),new UTF8Encoding(false));return;
            }
            if(args.Length==2&&(args[0]=="--render-packs-preview"||args[0]=="--render-packs-preview-en")){Ui.English=args[0].EndsWith("-en",StringComparison.Ordinal);using(var f=new LanguagePacksForm(true))f.RenderPreview(args[1]);return;}
            if(args.Length==2&&(args[0]=="--render-retry-preview"||args[0]=="--render-retry-preview-en")){Ui.English=args[0].EndsWith("-en",StringComparison.Ordinal);Preferences.Offline=false;Preferences.Target="zh-CN";using(var f=new CompanionForm(true))f.RenderPreview(args[1],true);return;}
            if(args.Length==2&&(args[0]=="--render-retry-dialog"||args[0]=="--render-retry-dialog-en")){Ui.English=args[0].EndsWith("-en",StringComparison.Ordinal);using(var f=new OfflineRetryForm(new List<LanguagePack>{new LanguagePack{From="en",To="zh",FromName="English",ToName="Chinese",Installed=true}}))f.RenderPreview(args[1]);return;}
            if (args.Length == 2 && args[0] == "--render-preview") { Ui.English=false; using (var f = new CompanionForm(true)) f.RenderPreview(args[1]); return; }
            if (args.Length == 2 && args[0] == "--render-preview-en") { Ui.English=true; using (var f = new CompanionForm(true)) f.RenderPreview(args[1]); return; }
            if (args.Length == 2 && args[0] == "--render-compose-preview") { using (var f = new ComposeForm(new WindowChoice { Handle = "0", Title = "演示聊天" }, true)) f.RenderPreview(args[1]); return; }
            if (args.Length == 2 && (args[0] == "--render-picker-preview" || args[0] == "--render-picker-preview-en")) {
                Ui.English=args[0].EndsWith("-en",StringComparison.Ordinal);
                var examples=new [] {
                    new SourceWindow{Handle=new IntPtr(1),ProcessId=1,Started=1,App="claude",Title="Project notes — Claude"},
                    new SourceWindow{Handle=new IntPtr(2),ProcessId=2,Started=2,App="chrome",Title="Reading a tutorial — Google Chrome"},
                    new SourceWindow{Handle=new IntPtr(3),ProcessId=3,Started=3,App="winword",Title="旅行计划 / Travel plans — Word"},
                    new SourceWindow{Handle=new IntPtr(4),ProcessId=4,Started=4,App="notepad",Title="今日灵感 / Ideas for today"}
                };
                using(var f=new SourceWindowPickerForm(examples[0],examples))f.RenderPreview(args[1]);return;
            }
            bool created;
            using (var mutex = new Mutex(true, "Local\\ClaudeChineseCompanion_" + Environment.UserName, out created)) {
                if (!created) { MessageBox.Show(Ui.T("大肥鱼翻译已经在运行，请查看任务栏。","Whale Translator is already running. Check the taskbar."), Ui.T("大肥鱼翻译","Whale Translator")); return; }
                Application.Run(new CompanionForm(false));
            }
        }
        private static void TranslationSelfTest(string path,bool offline){
            var report=new Dictionary<string,object>();report["version"]=Version;report["offline"]=offline;
            try {
                using(var worker=new Worker()){
                    string chinese=worker.Translate("Please keep the original file.","auto:zh-CN",offline).GetAwaiter().GetResult();
                    string english=worker.Translate("请保留原始文件。","auto:en",offline).GetAwaiter().GetResult();
                    string formatted=worker.Translate("Keep the original file.\n\n`DO_NOT_EDIT`\nhttps://example.com/demo","auto:zh-CN",offline).GetAwaiter().GetResult();
                    bool ok=System.Text.RegularExpressions.Regex.IsMatch(chinese,"[\\u3400-\\u9fff]")&&System.Text.RegularExpressions.Regex.IsMatch(english,"[A-Za-z]")&&formatted.Contains("\n\n")&&formatted.Contains("`DO_NOT_EDIT`")&&formatted.Contains("https://example.com/demo");
                    report["ok"]=ok;report["englishToChinese"]=chinese;report["chineseToEnglish"]=english;report["formattingPreserved"]=formatted.Contains("\n\n`DO_NOT_EDIT`\nhttps://example.com/demo");
                    if(!ok)Environment.ExitCode=1;
                }
            }catch(Exception ex){report["ok"]=false;report["error"]=ex is InvalidOperationException?ex.Message:ex.GetType().Name;Environment.ExitCode=1;}
            File.WriteAllText(Path.GetFullPath(path),Json.Encode(report),new UTF8Encoding(false));
        }
    }
}
