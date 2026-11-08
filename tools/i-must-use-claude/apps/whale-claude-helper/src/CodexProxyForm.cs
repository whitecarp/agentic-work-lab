using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WhaleClaudeHelper {
    // A verified HTTP tunnel is required for the desktop's HTTP/WebSocket clients.
    // SOCKS-only settings must not be presented as universal desktop support.
    internal sealed class CodexProxyPlan {
        internal string HttpUri, AllUri;
        internal static CodexProxyPlan Create(ProxyProbeResult http, ProxyProbeResult socks, bool separateSocks) {
            if(http==null || !http.Http)throw new InvalidOperationException("http_required");
            if(separateSocks && (socks==null || !socks.Socks5))throw new InvalidOperationException("socks_required");
            var result=new CodexProxyPlan { HttpUri=Address("http",http.Endpoint) };
            result.AllUri=separateSocks?Address("socks5h",socks.Endpoint):http.Socks5?Address("socks5h",http.Endpoint):result.HttpUri;
            return result;
        }
        private static string Address(string scheme,ProxyEndpoint endpoint) {
            ProxyEndpoint validated;
            if(endpoint==null || !ProxyDiscovery.TryCreateEndpoint(endpoint.Host,endpoint.Port,out validated))throw new InvalidOperationException("invalid_endpoint");
            return scheme+"://"+(validated.Host.Contains(":")?"["+validated.Host+"]":validated.Host)+":"+validated.Port;
        }
    }

    internal sealed class CodexProxyForm : Form {
        private readonly bool demo;
        private bool busy;
        private string configRoot,backupRoot;
        private TabControl tabs;
        private TextBox host,httpPort,socksHost,socksPort,report;
        private ComboBox candidates;
        private Label target,status;
        private Button language,cancel,apply;
        private ListBox backups;
        private CancellationTokenSource cancellation;
        private CodexProxyPlan verified;
        private static string T(string zh,string en){return Ui.T(zh,en);}
        internal CodexProxyForm(bool demoMode) {
            demo=demoMode;
            // Demo construction does not even resolve the user's configuration paths.
            if(!demo){configRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");backupRoot=Path.Combine(Ui.DataRoot,"Backups","CodexProxy");}
            SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            Font=new Font("Microsoft YaHei UI",10f);BackColor=Theme.Cream;ForeColor=Theme.Ink;
            ClientSize=new Size(880,740);MinimumSize=new Size(850,710);StartPosition=FormStartPosition.CenterParent;
            Theme.SetWindowIcon(this,true);
            Build();ResumeLayout(true);
            FormClosing+=delegate(object s,FormClosingEventArgs e){if(busy){e.Cancel=true;Status(T("操作进行中；检测可以取消，写入请等待完成。","Operation in progress. Cancel a check or wait for writes to finish."));}};
        }
        private void Build() {
            foreach(var old in Controls.Cast<Control>().ToArray()){Controls.Remove(old);old.Dispose();}
            verified=null;
            Text=T("Codex 连接修复 · 大肥鱼环境助手 0.7.0","Codex connection repair · Whale Helper 0.7.0")+(demo?" · Demo":"");
            var root=Rows(90,-1,58);root.Padding=new Padding(20,15,20,10);
            var header=new Panel{Dock=DockStyle.Fill};header.Controls.Add(new WhaleBadge{Size=new Size(90,76)});
            header.Controls.Add(new Label{Text=T("让 Codex 连上你的代理","Connect Codex to your proxy"),Font=new Font(Font.FontFamily,19,FontStyle.Bold),Location=new Point(108,8),Size=new Size(600,38)});
            header.Controls.Add(new Label{Text=T("不限软件、不固定端口 · 检测 → 备份并配置 → 重启 Codex","Any local proxy app · Test → back up & apply → restart Codex"),ForeColor=Theme.Muted,Location=new Point(110,52),Size=new Size(690,28)});
            language=Theme.Button(T("English","中文"),false);language.Width=90;header.Controls.Add(language);header.Resize+=delegate{language.Location=new Point(header.Width-94,0);};
            language.Click+=delegate{if(busy)return;Ui.English=!Ui.English;if(!demo)Ui.Save();Build();};root.Controls.Add(header,0,0);
            tabs=new TabControl{Dock=DockStyle.Fill,Padding=new Point(18,8)};BuildConnection();BuildBackups();root.Controls.Add(tabs,0,1);
            var bottom=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,145));
            status=Note(T("点击查找或手动填写，再检测。未更改任何设置。","Find a proxy or enter it manually, then test. No settings changed."));status.ForeColor=Theme.BlueDark;
            cancel=Theme.Button(T("取消检测","Cancel check"),false);cancel.Width=140;cancel.Visible=false;cancel.Click+=delegate{if(cancellation!=null)cancellation.Cancel();};bottom.Controls.Add(status,0,0);bottom.Controls.Add(cancel,1,0);root.Controls.Add(bottom,0,2);Controls.Add(root);
            if(demo)Status(T("演示模式 · 示例端口，不读取或修改本机配置","Demo mode · sample ports; no local configuration read or changed"));
        }
        private static TableLayoutPanel Rows(params float[] heights){var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=heights.Length,Margin=new Padding(0)};foreach(float h in heights)p.RowStyles.Add(new RowStyle(h<0?SizeType.Percent:SizeType.Absolute,h<0?100:h));return p;}
        private static Label Note(string text){return new Label{Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted};}
        private static FlowLayoutPanel Actions(){return new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0)};}
        private Button ActionButton(string text,Action action,bool primary,int width){var b=Theme.Button(text,primary);b.Width=width;b.Click+=delegate{if(busy)return;if(demo){Status(T("演示模式：按钮不执行本机操作。","Demo mode: buttons do not perform local operations."));return;}action();};return b;}
        private TabPage Page(string text){var page=new TabPage(text){BackColor=Theme.Cream,Padding=new Padding(16,12,16,8)};tabs.TabPages.Add(page);return page;}
        private void BuildConnection(){
            var page=Page(T("代理连接","Proxy connection"));var p=Rows(55,44,45,82,48,-1,54,55);page.Controls.Add(p);
            p.Controls.Add(Note(T("支持本地 HTTP / mixed，可另设 SOCKS5 端口。保持代理软件运行。\r\n仅 SOCKS5 时，请先在代理软件开启 HTTP / mixed；本工具不改系统代理。","Supports local HTTP / mixed, with an optional separate SOCKS5 port. Keep your proxy app running.\r\nSOCKS-only: enable HTTP / mixed first. System proxy settings stay unchanged.")),0,0);
            var pathRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,175));
            target=Note(demo?T("目标：示例目录 \\.codex\\.env","Target: example \\.codex\\.env"):T("目标：","Target: ")+Path.Combine(configRoot,".env"));target.AutoEllipsis=true;
            pathRow.Controls.Add(target,0,0);pathRow.Controls.Add(ActionButton(T("选择 Codex 目录","Codex folder"),ChooseFolder,false,168),1,0);p.Controls.Add(pathRow,0,1);
            var discoverRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};discoverRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,195));discoverRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            discoverRow.Controls.Add(ActionButton(T("查找本地代理","Find local proxies"),FindProxies,true,185),0,0);
            candidates=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Margin=new Padding(0,9,0,0)};candidates.SelectedIndexChanged+=delegate{var endpoint=candidates.SelectedItem as ProxyEndpoint;if(endpoint==null)return;host.Text=endpoint.Host;httpPort.Text=endpoint.Port.ToString();socksHost.Text=endpoint.Host;socksPort.Text="";InvalidatePlan();};discoverRow.Controls.Add(candidates,1,0);p.Controls.Add(discoverRow,0,2);
            var fields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=2};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,152));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,68));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));
            fields.RowStyles.Add(new RowStyle(SizeType.Percent,50));fields.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            host=Input("127.0.0.1");httpPort=Input(demo?"10809":"");socksHost=Input("127.0.0.1");socksPort=Input("");
            fields.Controls.Add(Note("HTTP / mixed"),0,0);fields.Controls.Add(host,1,0);fields.Controls.Add(Note(T("端口","Port")),2,0);fields.Controls.Add(httpPort,3,0);
            fields.Controls.Add(Note(T("SOCKS5（可选）","SOCKS5 (optional)")),0,1);fields.Controls.Add(socksHost,1,1);fields.Controls.Add(Note(T("端口","Port")),2,1);fields.Controls.Add(socksPort,3,1);p.Controls.Add(fields,0,3);
            var action=Actions();action.Controls.Add(ActionButton(T("检测协议","Test protocols"),delegate{Test(false);},false,185));apply=ActionButton(T("备份并配置 Codex","Back up & apply"),delegate{Test(true);},true,230);apply.Enabled=false;action.Controls.Add(apply);p.Controls.Add(action,0,4);
            report=new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink};
            report.Text=T("未检测。查找会读取系统代理与代理进程的本地监听端口；也可直接手动填写。\r\nSOCKS5 留空时：mixed 自动使用 SOCKS5；HTTP-only 使用 HTTP。\r\n检测向所填端口发出代理握手，不携带 Codex 凭据。","Not tested. Discovery reads the system proxy and local listeners of proxy processes; manual entry is also available.\r\nLeave SOCKS5 blank: mixed uses SOCKS5 automatically; HTTP-only uses HTTP.\r\nTests send proxy handshakes to the entered ports without Codex credentials.");p.Controls.Add(Theme.Card(report,new Padding(0,4,0,6)),0,5);
            p.Controls.Add(Note(T("仅更新 .env 的代理项（大小写同步），其余配置保留；已有 WSS_PROXY 同步。\r\nNO_PROXY 设为 localhost,127.0.0.1,::1。备份留在本机，可从「撤回」恢复。","Only proxy entries in .env are updated (both cases); existing WSS_PROXY is synchronized.\r\nNO_PROXY becomes localhost,127.0.0.1,::1. Backups stay local and can be restored from Undo.")),0,6);
            p.Controls.Add(Note(T("应用后，完成当前任务再彻底退出并重新打开 Codex。保持代理运行。\r\n这不会强制云端工具或不读取这些变量的程序流量，也不验证账号或 Reconnecting 已修复。","After applying, finish current tasks, fully quit and reopen Codex. Keep the proxy running.\r\nThis does not force cloud tools or clients ignoring these variables, or verify account/reconnection status.")),0,7);
        }
        private TextBox Input(string value){var box=new TextBox{Text=value,Dock=DockStyle.Fill,Margin=new Padding(0,7,8,0)};box.TextChanged+=delegate{InvalidatePlan();};return box;}
        private void InvalidatePlan(){verified=null;if(apply!=null)apply.Enabled=false;}
        private void ChooseFolder(){using(var picker=new FolderBrowserDialog{SelectedPath=configRoot,ShowNewFolderButton=false,Description=T("选择目标 Codex 配置目录（通常是 .codex）；只处理该目录中的 .env。","Select the target Codex configuration folder (usually .codex); only its .env is changed.")})if(picker.ShowDialog(this)==DialogResult.OK){configRoot=picker.SelectedPath;target.Text=T("目标：","Target: ")+Path.Combine(configRoot,".env");InvalidatePlan();backups.Items.Clear();}}
        private async void FindProxies(){
            InvalidatePlan();await Run(T("查找本地代理端口…","Finding local proxy ports…"),true,async token=>{
                var found=await Task.Run(()=>ProxyDiscovery.Discover(),token);token.ThrowIfCancellationRequested();
                candidates.Items.Clear();foreach(var candidate in found)candidates.Items.Add(candidate);
                if(found.Count==1)candidates.SelectedIndex=0;
                Status(found.Count==0?T("未识别到代理。请在代理软件查看 HTTP / mixed 端口后手动填写。","No proxy identified. Enter the HTTP / mixed port shown in your proxy app."):found.Count==1?T("已填入候选端口，点击「检测协议」核验。","Candidate filled in. Click Test protocols to verify."):T("找到多个候选，请从列表选择后检测；不会自动替你选。","Multiple candidates found. Select one and test; no automatic choice was made."));
            });
        }
        private async void Test(bool write){
            if(write && verified==null)return;
            ProxyEndpoint http,socks=null;bool separate=!String.IsNullOrWhiteSpace(socksPort.Text);
            if(!ProxyDiscovery.TryCreateEndpoint(host.Text.Trim(),httpPort.Text.Trim(),out http) || (separate&&!ProxyDiscovery.TryCreateEndpoint(socksHost.Text.Trim(),socksPort.Text.Trim(),out socks))){Status(T("请输入本机回环地址（127.0.0.1 / ::1）及 1–65535 的端口。","Enter a local loopback address (127.0.0.1 / ::1) and a port from 1 to 65535."));InvalidatePlan();return;}
            InvalidatePlan();
            await Run(T("检测代理协议…","Testing proxy protocols…"),!write,async token=>{
                var first=await ProxyDiscovery.ProbeAsync(http,token);
                var second=separate?await ProxyDiscovery.ProbeAsync(socks,token):null;token.ThrowIfCancellationRequested();
                report.Text=ProbeText(first)+(second==null?"":"\r\n\r\n"+ProbeText(second));
                try{verified=CodexProxyPlan.Create(first,second,separate);}catch(InvalidOperationException){Status(T("协议未通过。需要可用的 HTTP / mixed；填写的 SOCKS5 端口也必须通过。","Protocol check failed. A working HTTP / mixed endpoint is required, plus SOCKS5 if entered."));return;}
                report.AppendText("\r\n\r\nHTTP_PROXY / HTTPS_PROXY = "+verified.HttpUri+"\r\nALL_PROXY = "+verified.AllUri+"\r\nNO_PROXY = localhost,127.0.0.1,::1");
                if(!write){Status(T("协议通过。确认以上地址后，点击「备份并配置 Codex」。","Protocols verified. Review the addresses, then click Back up & apply."));return;}
                var plan=verified;var directory=configRoot;var backup=backupRoot;
                Status(T("正在备份并写入所选 .env…","Backing up and updating the selected .env…"));
                var result=await Task.Run(()=>new CodexProxySettings(directory,backup).Apply(plan.HttpUri,plan.AllUri));
                ShowResult(result,false);
            });
        }
        private static string ProbeText(ProxyProbeResult item){return item.Endpoint.ToString()+"\r\nHTTP CONNECT: "+(item.Http?T("通过","passed"):T("未通过","not verified"))+"   SOCKS5: "+(item.Socks5?T("通过","passed"):T("未通过","not verified"))+(item.AuthenticationRequired?"\r\n"+T("检测到代理认证要求；本版不保存或发送代理密码。","Proxy authentication required; this version does not store or send proxy passwords."):"");}
        private void BuildBackups(){
            var page=Page(T("撤回","Undo"));var p=Rows(80,48,-1,52,80);page.Controls.Add(p);
            p.Controls.Add(Note(T("只列出所选 Codex 目录的代理备份。恢复前会核验文件是否被再次修改。\r\n如果之后有修改，保留现状并提示冲突，不会覆盖。完整备份可能含其他密钥，请只留在本机。","Lists proxy backups for the selected Codex folder. Restore checks for edits made since applying.\r\nLater edits are preserved and reported as conflicts. Full backups may contain other secrets; keep them local.")),0,0);
            var top=Actions();top.Controls.Add(ActionButton(T("读取备份记录","Load backups"),LoadBackups,false,205));p.Controls.Add(top,0,1);
            backups=new ListBox{Dock=DockStyle.Fill,HorizontalScrollbar=true,BorderStyle=BorderStyle.None,BackColor=Color.White};p.Controls.Add(Theme.Card(backups,new Padding(0,8,0,8)),0,2);
            var bottom=Actions();bottom.Controls.Add(ActionButton(T("撤回所选修改","Undo selected change"),Restore,true,235));p.Controls.Add(bottom,0,3);
            p.Controls.Add(Note(T("恢复原 .env；如果原本不存在，会把新建的 .env 移入回收站。\r\n撤回后也需彻底退出并重新打开 Codex。本工具不会关闭 Codex 或代理软件。","Restores the original .env; if none existed, the new .env goes to the Recycle Bin.\r\nFully quit and reopen Codex after undoing. This tool does not close Codex or your proxy app.")),0,4);
        }
        private async void LoadBackups(){await Run(T("读取本机备份记录…","Reading local backup records…"),false,async token=>{var found=await Task.Run(()=>new CodexProxySettings(configRoot,backupRoot).ListBackups());backups.Items.Clear();foreach(var path in found)backups.Items.Add(path);Status(T("备份数量：","Backups: ")+found.Count);});}
        private async void Restore(){var selected=backups.SelectedItem as string;if(selected==null){Status(T("先选中一条备份。","Select a backup first."));return;}await Run(T("核验并恢复原配置…","Verifying and restoring original settings…"),false,async token=>{var result=await Task.Run(()=>new CodexProxySettings(configRoot,backupRoot).Restore(selected));InvalidatePlan();ShowResult(result,true);});}
        private void ShowResult(CodexProxyChange result,bool restore){
            if(!result.Success){Status(FailureText(result));report.AppendText("\r\n\r\n"+FailureText(result));if(!String.IsNullOrEmpty(result.BackupDirectory))report.AppendText("\r\n"+T("备份：","Backup: ")+result.BackupDirectory);if(!String.IsNullOrEmpty(result.RecoveryPath))report.AppendText("\r\n"+T("并发修改的恢复副本：","Recovery copy of concurrent changes: ")+result.RecoveryPath);if(restore)tabs.SelectedIndex=0;return;}
            string text=restore?T("原配置已恢复。请彻底退出并重新打开 Codex。","Original settings restored. Fully quit and reopen Codex."):result.Changed?T("已备份并配置。完成当前任务后，彻底退出并重新打开 Codex；代理保持运行。","Backed up and configured. Finish current tasks, fully quit and reopen Codex; keep the proxy running."):T("代理配置已一致，无需重复写入。请重启 Codex 后检查连接。","Proxy settings already match; no rewrite needed. Restart Codex and check the connection.");
            Status(text);if(!String.IsNullOrEmpty(result.BackupDirectory))report.AppendText("\r\n\r\n"+T("备份：","Backup: ")+result.BackupDirectory);
        }
        private static string ErrorText(string code){
            // Service errors are codes, never raw .env content or exception messages.
            if(code=="codex_concurrent_edit_preserved")return T("检测到同时修改，请核对备份目录内保留的恢复副本。","Concurrent changes detected; review the retained recovery copy in the backup directory.");
            if(!String.IsNullOrEmpty(code)&&(code.IndexOf("conflict",StringComparison.OrdinalIgnoreCase)>=0||code.IndexOf("changed",StringComparison.OrdinalIgnoreCase)>=0))return T("配置有后续修改，已保留现状。请核对备份。","Settings changed later. Current data was preserved; review the backup.");
            return T("文件格式、路径或权限未通过检查；请核对目标目录。","File format, path or access checks failed; review the target folder.")+(String.IsNullOrEmpty(code)?"":" ["+code+"]");
        }
        internal static string FailureText(CodexProxyChange result){return result.Changed?T("配置已发生变更，但核验或收据保存未完成。请先核对本机备份，再撤回或重启 Codex。","Configuration changed, but verification or receipt saving failed. Review the local backup before undoing or restarting Codex.")+" "+ErrorText(result.Error):T("未完成：","Not completed: ")+ErrorText(result.Error);}
        private async Task Run(string text,bool cancellable,Func<CancellationToken,Task> operation){
            if(demo||busy)return;busy=true;tabs.Enabled=false;language.Enabled=false;cancel.Visible=cancellable;cancellation=new CancellationTokenSource();Status(text);
            try{await operation(cancellation.Token);}catch(OperationCanceledException){InvalidatePlan();Status(T("检测已取消，未写入配置。","Check cancelled; configuration was not written."));}catch{InvalidatePlan();Status(T("操作未完成，请检查端口、目录权限或备份记录。","Operation incomplete. Check ports, folder access or backup records."));}
            finally{busy=false;if(!IsDisposed){tabs.Enabled=true;language.Enabled=true;cancel.Visible=false;apply.Enabled=verified!=null;}cancellation.Dispose();cancellation=null;}
        }
        private void Status(string text){if(status!=null)status.Text="● "+text;}
    }
}
