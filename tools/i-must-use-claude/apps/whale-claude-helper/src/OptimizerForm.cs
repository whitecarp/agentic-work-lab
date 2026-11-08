using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WhaleClaudeHelper {
    internal sealed class OptimizerForm : Form {
        private readonly bool demo;
        private bool busy,measuring,awaitingAfter;
        private OptimizerService service;
        private ScoreReport before,after;
        private CancellationTokenSource measurementCancellation;
        private Label state,score,hint;
        private Button optimize,undo,language,cancel;
        private LinkLabel reset,maintenance,updates,codexProxy,closeApps,network;
        private TextBox details;
        private string operationDetails="";
        private static string T(string zh,string en){return Ui.T(zh,en);}
        internal OptimizerForm(bool demoMode){
            demo=demoMode;if(!demo)service=new OptimizerService();
            Font=new Font("Microsoft YaHei UI",10f);AutoScaleMode=AutoScaleMode.Dpi;
            ClientSize=new Size(820,760);MinimumSize=new Size(790,730);
            StartPosition=FormStartPosition.CenterScreen;BackColor=Theme.Cream;ForeColor=Theme.Ink;
            try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
            Build();
            FormClosing+=delegate(object sender,FormClosingEventArgs e){
                if(measuring){measurementCancellation.Cancel();return;}
                if(busy){e.Cancel=true;state.Text=T("正在完成备份记录，请稍候关闭。","Finishing backup receipts; please wait before closing.");}
            };
        }
        private void Build(){
            var old=Controls.Cast<Control>().ToArray();Controls.Clear();foreach(var c in old)c.Dispose();
            Text=T("大肥鱼环境助手 0.7.0 预览版","Whale Environment Helper 0.7.0 Preview")+(demo?" · Demo":"");
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Padding=new Padding(24,16,24,14)};
            foreach(float h in new float[]{100,54,85,66,92,42,-1,72})root.RowStyles.Add(new RowStyle(h<0?SizeType.Percent:SizeType.Absolute,h<0?100:h));
            var header=new Panel{Dock=DockStyle.Fill};header.Controls.Add(new WhaleBadge{Size=new Size(110,88)});
            header.Controls.Add(new Label{Text=T("大肥鱼环境助手","Whale Environment Helper"),Font=new Font(Font.FontFamily,Ui.English?19:23,FontStyle.Bold),Location=new Point(122,15),Size=new Size(520,42)});
            header.Controls.Add(new Label{Text=T("先检测 → 按命中项优化 → 再测一次","Measure → optimize matched items → measure again"),ForeColor=Theme.Muted,Location=new Point(124,62),Size=new Size(600,32)});
            language=Theme.Button(T("English","中文"),false);language.Size=new Size(90,34);language.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            header.Controls.Add(language);header.Resize+=delegate{language.Location=new Point(Math.Max(0,header.Width-language.Width),0);};
            language.Click+=delegate{Ui.English=!Ui.English;if(!demo)Ui.Save();Build();};root.Controls.Add(header,0,0);
            state=new Label{Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,12,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft};root.Controls.Add(state,0,1);
            score=new Label{Dock=DockStyle.Fill,BackColor=Color.White,Font=new Font(Font.FontFamily,13,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft};root.Controls.Add(Theme.Card(score,new Padding(0,0,0,7)),0,2);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,7,0,0)};
            optimize=Theme.Button("",true);optimize.Size=new Size(238,47);optimize.Click+=async delegate{if(before==null||awaitingAfter||after!=null||!before.CanOptimize)await Measure();else await Change(false);};
            undo=Theme.Button(T("撤回上次优化","Undo last optimization"),false);undo.Size=new Size(238,47);undo.Click+=async delegate{await Change(true);};
            cancel=Theme.Button(T("取消检测","Cancel check"),false);cancel.Size=new Size(150,47);cancel.Click+=delegate{if(measurementCancellation!=null)measurementCancellation.Cancel();};
            buttons.Controls.Add(optimize);buttons.Controls.Add(undo);buttons.Controls.Add(cancel);root.Controls.Add(buttons,0,3);
            hint=new Label{Dock=DockStyle.Fill,ForeColor=Theme.Muted,UseMnemonic=false};root.Controls.Add(hint,0,4);
            root.Controls.Add(new Label{Dock=DockStyle.Fill,Text=T("检测明细 · 未知项不会按 0 分处理","Measured details · unknown checks are never scored zero"),TextAlign=ContentAlignment.MiddleLeft},0,5);
            details=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink};root.Controls.Add(Theme.Card(details,new Padding(0,0,0,5)),0,6);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill};
            reset=new LinkLabel{AutoSize=true,Text=T("重新检测","Measure again"),LinkColor=Theme.BlueDark,Margin=new Padding(0,8,24,0)};reset.LinkClicked+=async delegate{await Measure();};footer.Controls.Add(reset);
            maintenance=new LinkLabel{AutoSize=true,Text=T("维护与清理","Maintenance & cleanup"),LinkColor=Theme.BlueDark,Margin=new Padding(0,8,24,0)};
            maintenance.LinkClicked+=delegate{if(busy)return;try{using(var form=new MaintenanceForm(demo))form.ShowDialog(this);Build();}catch{MessageBox.Show(this,T("维护页面无法打开。请检查目录访问权限后重试。","Maintenance could not open. Check folder access and try again."),T("大肥鱼提示","Whale Helper"));}};footer.Controls.Add(maintenance);
            codexProxy=new LinkLabel{AutoSize=true,Text=T("Codex 连接修复","Codex connection repair"),LinkColor=Theme.BlueDark,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(0,8,24,0)};
            codexProxy.LinkClicked+=delegate{if(busy)return;using(var form=new CodexProxyForm(demo))form.ShowDialog(this);Build();};footer.Controls.Add(codexProxy);
            closeApps=new LinkLabel{AutoSize=true,Text=T("关闭占用程序","Close blocking apps"),LinkColor=Theme.BlueDark,Margin=new Padding(0,8,24,0)};
            closeApps.LinkClicked+=delegate{if(busy)return;using(var form=new ProcessCloseDialog(demo))form.ShowDialog(this);};footer.Controls.Add(closeApps);
            network=new LinkLabel{AutoSize=true,UseMnemonic=false,Text=T("网络与 DNS","Network & DNS"),LinkColor=Theme.BlueDark,Margin=new Padding(0,8,24,0)};
            network.LinkClicked+=delegate{if(busy)return;using(var form=new NetworkAssistantForm(demo))form.ShowDialog(this);RefreshView();};footer.Controls.Add(network);
            var about=new LinkLabel{AutoSize=true,Text=T("说明与许可","Notes & license"),LinkColor=Theme.BlueDark,Margin=new Padding(0,8,0,0)};about.LinkClicked+=delegate{ShowNotes();};footer.Controls.Add(about);
            updates=new LinkLabel{AutoSize=true,Text=T("检查更新","Check updates"),LinkColor=Theme.BlueDark,Margin=new Padding(24,8,0,0)};
            updates.LinkClicked+=delegate{if(!busy&&!demo)WhaleCommon.UpdateChecker.Show(this,"helper","0.7.0",Ui.English);};footer.Controls.Add(updates);
            root.Controls.Add(footer,0,7);Controls.Add(root);RefreshView();
        }
        private bool SafeHasUndo(){try{return !demo&&service.HasUndo;}catch{return false;}}
        internal static string ScoreText(ScoreReport report,bool english){
            if(report==null)return english?"Not measured":"尚未检测";
            return report.IsComplete?report.MeasuredScore+" / 100":(english?"Measured subtotal ":"已测小计 ")+report.MeasuredScore+(english?"; measured weight ":"；覆盖权重 ")+report.MeasuredWeight+" / 100";
        }
        private void RefreshView(){
            optimize.Enabled=undo.Enabled=language.Enabled=reset.Enabled=maintenance.Enabled=updates.Enabled=codexProxy.Enabled=closeApps.Enabled=network.Enabled=!busy;
            undo.Enabled=!busy&&SafeHasUndo();cancel.Visible=measuring;
            optimize.Text=awaitingAfter?T("复测结果","Measure after"):(after!=null?T("开始新一轮检测","Start a new check"):(before!=null&&before.CanOptimize?T("一键优化","Optimize"):T("开始检测","Measure first")));
            score.Text=before==null?T("尚未检测 · 不猜测分数","Not measured · no assumed score"):T("优化前：","Before: ")+ScoreText(before,Ui.English);
            if(after!=null){score.Text+="\r\n"+T("复测：","After: ")+ScoreText(after,Ui.English);if(before.CanCompare(after))score.Text+="  "+T("变化：","Change: ")+(after.MeasuredScore-before.MeasuredScore).ToString("+0;-0;0");else score.Text+=T(" · 暂不比较差值"," · no comparable delta");}
            state.Text=awaitingAfter?T("设置已处理，等待实测结果。","Settings processed; awaiting measurement."):(after!=null?T("复测已完成，请查看实际变化。","Measured again. Review the actual results."):(before==null?T("先测出分数，再决定需要改什么。","Measure first, then decide what needs changing."):(before.CanOptimize?T("检测已收到，仅处理命中的本地项目。","Results received. Only matched local items will be handled."):T("部分必要项目未测或已过期，请重新检测。","Required checks are missing or expired. Measure again."))));
            hint.Text=T("检测会打开浏览器，请点击「开始测量」；复测时使用同一浏览器和资料。\r\n先保存工作并退出 Claude、Chrome、Edge。优化只改命中的时区、语言和个人字体；DNS 在「网络与 DNS」单独预览确认。","Measurement opens your browser; click Start measurement. Reuse the same browser/profile.\r\nSave work and close Claude, Chrome and Edge. Optimize changes matched UTC, language and personal fonts; use Network & DNS for separate DNS preview and confirmation.");
            var report=after??before;
            details.Text=report==null?T("尚无真实检测结果。\r\n构建与测试记录请查看仓库 Actions。","No real measurement yet.\r\nSee repository Actions for build and test records."):ReportText(report,Ui.English);
            if(operationDetails.Length>0)details.Text=operationDetails+"\r\n\r\n"+details.Text;
            if(demo){state.Text=T("演示模式 · 不进行检测或系统修改","Demo mode · no measurement or system changes");optimize.Text=T("查看流程示例","View sample flow");}
        }
        internal static string ReportText(ScoreReport report,bool english){
            string[] zh={"时区","语言","字体","厂商字体","WebRTC","浏览器","设备","Intl 语言","时间偏移","Emoji"};
            string text=report.BrowserId+" · "+report.CollectedUtc.ToString("yyyy-MM-dd HH:mm:ss")+" UTC\r\n";
            for(int i=0;i<report.Signals.Count;i++){var s=report.Signals[i];text+=(english?s.Id:zh[i])+": "+(s.Points.HasValue?s.Points+" / "+s.Weight:(english?"Unknown":"未知"))+" — "+SignalDetailText(s,english)+"\r\n";}
            return text;
        }
        private static string SignalDetailText(MeasuredSignal signal,bool english){
            string status;
            switch(signal.Status){
                case "timeout":status=english?"Timed out":"检测超时";break;
                case "server_error":status=english?"STUN probe failed (server availability is unconfirmed)":"STUN 探测失败（不能据此判断服务器宕机）";break;
                case "unsupported":status=english?"Browser unsupported":"浏览器不支持";break;
                default:return signal.Display;
            }
            return status+(String.IsNullOrEmpty(signal.Display)?"":" · "+signal.Display);
        }
        private async Task Measure(){
            if(busy)return;
            if(demo){details.Text=T("虚拟流程：未检测 → 收到样本分数 → 按命中项处理 → 等待复测 → 比较两次实测。\r\n没有打开浏览器、读取系统或执行修改。","Sample flow: unmeasured → sample baseline → handle matches → await recheck → compare measurements.\r\nNo browser opened, system read or settings changed.");return;}
            bool isAfter=awaitingAfter;
            busy=measuring=true;measurementCancellation=new CancellationTokenSource();RefreshView();
            state.Text=T("在已打开的浏览器页面点击「开始测量」…","Click Start measurement in the browser page…");
            string error=null;
            try{using(var host=new ScoreMeasurementHost()){
                var report=await host.MeasureAsync(measurementCancellation.Token);
                if(isAfter){after=report;awaitingAfter=false;}else{before=report;after=null;operationDetails="";}
            }}catch(OperationCanceledException){error=T("检测已取消，原结果保留。","Check cancelled; previous results kept.");}
            catch(TimeoutException){error=T("等待检测超时，原结果保留。请重新检测。","Check timed out; previous results kept. Try again.");}
            catch{error=T("未能收到有效检测结果，原结果保留。","No valid result received; previous results kept.");}
            finally{busy=measuring=false;measurementCancellation.Dispose();measurementCancellation=null;}
            if(IsDisposed)return;RefreshView();if(error!=null)state.Text=error;
        }
        private async Task Change(bool restoring,bool retried=false){
            if(busy||demo)return;
            busy=true;RefreshView();state.Text=restoring?T("正在核对备份并撤回…","Checking backups and undoing…"):T("正在备份并处理命中项…","Backing up and processing matched items…");
            OptimizationResult result;
            try{result=await Task.Run(()=>restoring?service.UndoLast():service.Optimize(before));}
            catch{result=new OptimizationResult{Summary="operation_unexpected_error"};}
            busy=false;if(IsDisposed)return;
            operationDetails=ResultText.Render(result.Summary,Ui.English)+"\r\n"+String.Join("\r\n",result.Details.Select(x=>ResultText.Render(x,Ui.English)))+(String.IsNullOrEmpty(result.OperationDirectory)?"":"\r\n"+T("备份：","Backup: ")+result.OperationDirectory);
            if(restoring){before=after=null;awaitingAfter=false;}
            else if(!String.IsNullOrEmpty(result.OperationDirectory)){after=null;awaitingAfter=true;}
            RefreshView();
            if(!result.Ok)state.Text=T("未全部完成，请查看说明；有变更则需复测。","Not fully completed. Review details; remeasure if changes occurred.");
            if(!result.Ok && result.Details.Contains("close_browsers_and_claude_first")){
                state.Text=T("浏览器或 Claude 仍在运行。先保存工作，再关闭占用程序。","A browser or Claude is still running. Save your work, then close blocking apps.");
                if(!retried && String.IsNullOrEmpty(result.OperationDirectory))using(var dialog=new ProcessCloseDialog(false)){
                    if(dialog.ShowDialog(this)==DialogResult.OK)await Change(restoring,true);
                }
            }
        }
        private void ShowNotes(){string text=T("依据：LinXiaoTao / FuckClaude 的浏览器检测权重。\r\n\r\n这是该项目的启发式分数，不是官方账号资格评分。Windows 时区直接影响24分时区项和最多2分偏移项；Chrome/Edge语言预设的效果需浏览器重启与网页实测。\r\n\r\n评分优化不会清空 Claude 配置、清缓存或移除登录凭据。独立的「维护与清理」入口提供配置检查、缓存清理和恢复备份，不计入该评分。系统字体受保护；只有身份、路径与注册关系明确的个人字体才可移除。\r\n\r\n本页环境优化不改网络；网络与 DNS 可单独检查、预览、备份应用和撤回 CuteCloud DNS。不同浏览器、托管策略和同步可能影响结果，不承诺0分。\r\n\r\n","Based on LinXiaoTao / FuckClaude browser signal weights.\r\n\r\nThis is a heuristic project score, not official account eligibility. Windows timezone affects the 24-point timezone item and up to 2 offset points. Chrome/Edge language changes need browser restart and a browser check.\r\n\r\nScore optimization does not reset Claude configuration, clean caches or remove credentials. Maintenance & cleanup separately offers configuration checks, cache cleanup and restore; these do not affect the score. System fonts are protected. Only verified, uniquely registered personal fonts can be removed.\r\n\r\nEnvironment optimization leaves the network unchanged. Network & DNS separately inspects, previews, backs up, applies and undoes CuteCloud DNS changes. Browsers, managed policy and sync can affect the result. A zero score is not promised.\r\n\r\n");
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("UpstreamLicense"))if(stream!=null)using(var reader=new StreamReader(stream))text+=reader.ReadToEnd();
            using(var dialog=new Form{Icon=this.Icon,ShowInTaskbar=false,Text=T("说明与许可","Notes & license"),Size=new Size(700,600),StartPosition=FormStartPosition.CenterParent,Font=Font,BackColor=Theme.Cream}){dialog.Controls.Add(new TextBox{Text=text,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill});dialog.ShowDialog(this);}
        }
    }
}
