using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WhaleClaudeHelper {
    // Demo mode never constructs a live service, starts a probe, or saves preferences.
    internal sealed class MaintenanceForm : Form {
        private readonly bool demo;
        private bool busy;
        private string configRoot;
        private RepairEngine repair;
        private RepairScan scan;
        private CacheService cache;
        private DiagnosticsService diagnostics;
        private CancellationTokenSource cancellation;
        private TabControl tabs;
        private Label status,configPath;
        private TextBox diagnosticText,configText;
        private CheckBox official,proxy,userEnvironment;
        private ListView caches;
        private ListBox backups;
        private Button cancel,language;
        private sealed class BackupItem {
            internal string Kind,Path;
            public override string ToString(){return Kind+" · "+System.IO.Path.GetFileName(Path);}
        }
        private static string T(string zh,string en){return Ui.T(zh,en);}
        internal MaintenanceForm(bool demoMode){
            demo=demoMode;
            if(!demo){
                configRoot=Ui.ConfigRoot;
                try{repair=NewRepair();}catch{repair=null;}
                try{cache=new CacheService();}catch{cache=null;}
                diagnostics=new DiagnosticsService();
            }
            Font=new Font("Microsoft YaHei UI",10f);AutoScaleMode=AutoScaleMode.Dpi;
            BackColor=Theme.Cream;ForeColor=Theme.Ink;ClientSize=new Size(970,760);MinimumSize=new Size(930,720);
            StartPosition=FormStartPosition.CenterParent;Theme.SetWindowIcon(this,true);Build();
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy){e.Cancel=true;SetStatus(T("正在完成操作。可取消检查，修改操作请等待结果。","An operation is running. Checks can be cancelled; wait for changes to finish."));}};
        }
        private RepairEngine NewRepair(){return new RepairEngine(configRoot,Path.Combine(Ui.DataRoot,"Backups","Configuration"));}
        private void Build(){
            var old=Controls.Cast<Control>().ToArray();Controls.Clear();foreach(var c in old)c.Dispose();
            Text=T("维护与清理 · 大肥鱼环境助手 0.7.0","Maintenance & cleanup · Whale Environment Helper 0.7.0")+(demo?" · Demo":"");
            var root=Rows(95,-1,48);root.Padding=new Padding(22,16,22,10);
            var head=new Panel{Dock=DockStyle.Fill};head.Controls.Add(new WhaleBadge{Size=new Size(92,76)});
            head.Controls.Add(new Label{Text=T("检查环境，清理缓存","Check your setup. Clear caches."),Font=new Font(Font.FontFamily,20,FontStyle.Bold),Location=new Point(108,7),Size=new Size(670,39)});
            head.Controls.Add(new Label{Text=T("先检查 → 选择项目 → 备份后处理 → 随时查看恢复记录","Inspect → select items → back up and apply → review restore records"),ForeColor=Theme.Muted,Location=new Point(110,51),Size=new Size(770,30)});
            language=Theme.Button(T("English","中文"),false);language.Width=90;
            language.Click+=delegate{if(busy)return;int page=tabs.SelectedIndex;Ui.English=!Ui.English;if(!demo)Ui.Save();scan=null;Build();tabs.SelectedIndex=page;};
            head.Controls.Add(language);head.Resize+=delegate{language.Location=new Point(head.Width-95,0);};root.Controls.Add(head,0,0);
            tabs=new TabControl{Dock=DockStyle.Fill,Padding=new Point(18,9)};BuildDiagnostics();BuildConfiguration();BuildCaches();BuildBackups();root.Controls.Add(tabs,0,1);
            var bottom=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));
            status=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true,ForeColor=Theme.BlueDark,Text=T("● 点击检查后才读取对应项目。","● Each check starts only when you request it.")};
            cancel=Theme.Button(T("取消检查","Cancel check"),false);cancel.Dock=DockStyle.Fill;cancel.Visible=false;cancel.Click+=delegate{if(cancellation!=null)cancellation.Cancel();};
            bottom.Controls.Add(status,0,0);bottom.Controls.Add(cancel,1,0);root.Controls.Add(bottom,0,2);Controls.Add(root);
            if(demo)ShowDemo();
            else if(repair==null||cache==null)SetStatus(T("部分路径未通过安全检查，相关功能暂停。可选择有效的配置目录。","Some paths failed safety checks; affected features are paused. You can choose a valid configuration folder."));
        }
        private TableLayoutPanel Rows(params float[] heights){var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=heights.Length,Margin=new Padding(0)};foreach(var h in heights)p.RowStyles.Add(new RowStyle(h<0?SizeType.Percent:SizeType.Absolute,h<0?100:h));return p;}
        private TabPage Page(string zh,string en){var p=new TabPage(T(zh,en)){BackColor=Theme.Cream,Padding=new Padding(16)};tabs.TabPages.Add(p);return p;}
        private Label Note(string text){return new Label{Text=text,Dock=DockStyle.Fill,ForeColor=Theme.Muted,TextAlign=ContentAlignment.MiddleLeft};}
        private TextBox ReadBox(){return new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink};}
        private FlowLayoutPanel Actions(){return new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0)};}
        private Button ActionButton(string text,Action action,bool primary,int width){var b=Theme.Button(text,primary);b.Width=width;b.Click+=delegate{if(busy)return;if(demo){SetStatus(T("演示模式：使用示例数据，不执行本机操作。","Demo mode: sample data only; no local operation."));return;}action();};return b;}
        private void BuildDiagnostics(){
            var p=Rows(62,50,-1,64);Page("连接与安装","Connection & installation").Controls.Add(p);
            p.Controls.Add(Note(T("检查 Claude 安装位置和代理设置情况。连接体检会额外请求 Claude 官方站点，检查 DNS 与 HTTPS。","Check Claude installation locations and whether proxy variables are set. Connection checks also request official Claude hosts to test DNS and HTTPS.")),0,0);
            var a=Actions();a.Controls.Add(ActionButton(T("检查本地安装","Check installation"),delegate{CheckDiagnostics(false);},true,200));a.Controls.Add(ActionButton(T("连接体检","Check connection"),delegate{CheckDiagnostics(true);},false,200));p.Controls.Add(a,0,1);
            diagnosticText=ReadBox();diagnosticText.Text=T("尚未检查。连接体检不会携带你的 Claude 登录凭据。","Not checked. Connection probes do not send your Claude credentials.");p.Controls.Add(Theme.Card(diagnosticText,new Padding(0,8,0,10)),0,2);
            p.Controls.Add(Note(T("只检查，不改网络、DNS 或共用代理。未在常见位置找到不等于未安装。\r\n网络可达不代表账号、订阅或服务地区符合要求。","Read-only: network, DNS and shared proxies stay unchanged. Unrecognized installations may be missed.\r\nReachability does not establish account, subscription or region eligibility.")),0,3);
        }
        private async void CheckDiagnostics(bool includeNetwork){diagnosticText.Text=T("正在检查…","Checking…");await Run(T("正在检查…","Checking…"),async token=>{var report=await diagnostics.RunAsync(includeNetwork,token);return (Action)delegate{ShowDiagnostics(report);};},true);}
        private void ShowDiagnostics(DiagnosticReport report){var s=new StringBuilder();foreach(var item in report.Checks){s.AppendLine("["+StatusText(item.Status)+"] "+(Ui.English?item.TitleEn:item.TitleZh));s.AppendLine(Ui.English?item.DetailEn:item.DetailZh);s.AppendLine();}diagnosticText.Text=s.ToString();SetStatus(report.IsDemo?T("模拟结果 · 未运行本机检查","Simulated results · no local check performed"):T("检查完成。请查看每项结果。","Check complete. Review each result."));}
        private static string StatusText(DiagnosticStatus value){return value==DiagnosticStatus.Pass?T("通过","Pass"):value==DiagnosticStatus.Fail?T("失败","Fail"):T("需查看","Review");}
        private void BuildConfiguration(){
            var p=Rows(44,48,-1,35,35,35,51,64);Page("官方配置","Official configuration").Controls.Add(p);
            configPath=Note(demo?T("演示配置目录","Demo configuration folder"):configRoot);configPath.AutoEllipsis=true;p.Controls.Add(configPath,0,0);
            var a=Actions();a.Controls.Add(ActionButton(T("检查配置","Check settings"),ScanConfiguration,true,170));a.Controls.Add(ActionButton(T("选择配置目录","Choose folder"),ChooseRoot,false,180));a.Controls.Add(ActionButton(T("导出无密钥报告","Export redacted report"),ExportReport,false,210));p.Controls.Add(a,0,1);
            configText=ReadBox();configText.Text=T("检查地址、认证和模型覆盖项；只显示设置项名称。","Inspect endpoint, authentication and model overrides. Only setting names are shown.");p.Controls.Add(Theme.Card(configText,new Padding(0,7,0,7)),0,2);
            official=new CheckBox{Text=T("移除旧的第三方地址、认证与模型覆盖","Remove old endpoint, auth and model overrides"),Dock=DockStyle.Fill,Enabled=false};
            proxy=new CheckBox{Text=T("移除所选 Claude 配置内的代理（可选）","Remove proxies in the selected Claude settings (optional)"),Dock=DockStyle.Fill,Enabled=false};
            userEnvironment=new CheckBox{Text=T("移除当前用户的 Claude 专用环境覆盖项","Remove current-user Claude environment overrides"),Dock=DockStyle.Fill,Enabled=false};
            p.Controls.Add(official,0,3);p.Controls.Add(proxy,0,4);p.Controls.Add(userEnvironment,0,5);
            var b=Actions();b.Controls.Add(ActionButton(T("预览并恢复官方配置","Review & restore official"),RepairSelected,true,260));p.Controls.Add(b,0,6);
            p.Controls.Add(Note(T("保留会话、登录数据、Skills、插件、Hooks、MCP 与身份标识。\r\nCC Switch / Cockpit 接管、项目配置与组织策略需另行检查；此处不会自动切换供应商。","Preserve chats, login data, skills, plugins, hooks, MCP and identity.\r\nCC Switch / Cockpit takeover, project settings and managed policy need separate review; no provider is switched here.")),0,7);
        }
        private void ChooseRoot(){using(var d=new FolderBrowserDialog{SelectedPath=configRoot,ShowNewFolderButton=false,Description=T("选择 Claude Code 配置目录（通常为 .claude）","Choose the Claude Code configuration folder (usually .claude)")})if(d.ShowDialog(this)==DialogResult.OK){
            try{var next=new RepairEngine(d.SelectedPath,Path.Combine(Ui.DataRoot,"Backups","Configuration"));configRoot=d.SelectedPath;repair=next;configPath.Text=configRoot;backups.Items.Clear();ScanConfiguration();}
            catch{Info(T("目录未通过安全检查；已保留之前的选择。请使用非链接的普通目录。","The folder failed safety checks. The previous selection is retained. Choose a normal folder without linked paths."));}
        }}
        private async void ScanConfiguration(){
            if(repair==null){Info(T("配置路径未通过安全检查，请重新选择配置目录。","The configuration path failed safety checks. Choose a different folder."));return;}
            scan=null;official.Checked=proxy.Checked=userEnvironment.Checked=false;official.Enabled=proxy.Enabled=userEnvironment.Enabled=false;
            configText.Text=T("正在检查；上次结果已清除。","Checking; previous results have been cleared.");
            await Run(T("正在只读检查配置…","Reading configuration…"),token=>Task.Run(()=>{var next=repair.Scan();return (Action)delegate{scan=next;ShowScan();};}),false);
        }
        private void ShowScan(){
            official.Checked=proxy.Checked=userEnvironment.Checked=false;
            official.Enabled=scan.SettingsValid&&scan.OfficialSettingKeys.Count>0;proxy.Enabled=scan.SettingsValid&&scan.ProxySettingKeys.Count>0;userEnvironment.Enabled=String.IsNullOrEmpty(scan.EnvironmentError)&&scan.UserEnvironmentKeys.Count>0;
            var s=new StringBuilder();Keys(s,T("地址 / 认证 / 模型覆盖","Endpoint / authentication / model overrides"),scan.OfficialSettingKeys);Keys(s,T("Claude 配置内的代理","Proxies inside Claude settings"),scan.ProxySettingKeys);Keys(s,T("当前用户环境覆盖","Current-user environment overrides"),scan.UserEnvironmentKeys);Keys(s,T("仅检查，需手动核对","Read-only; manual review needed"),scan.OtherOverrideKeys);
            if(!scan.SettingsExists)s.AppendLine(T("未找到 settings.json；不会创建或替换。","settings.json not found; it will not be created or replaced."));
            if(!scan.SettingsValid)s.AppendLine(T("配置未通过安全检查，已禁止修改。","Configuration safety check did not pass; editing is disabled."));
            if(!String.IsNullOrEmpty(scan.EnvironmentError))s.AppendLine(T("环境变量检查未完成，已禁止修改该部分。","Environment check incomplete; editing this section is disabled."));
            configText.Text=s.ToString();SetStatus(T("配置检查完成，请勾选需要处理的项目。","Configuration checked. Select the items to handle."));
        }
        private static void Keys(StringBuilder s,string title,IEnumerable<string> keys){s.AppendLine(title);var items=keys.ToArray();s.AppendLine(items.Length==0?T("  未发现覆盖项","  No overrides found"):"  "+String.Join("\r\n  ",items));s.AppendLine();}
        private bool RequireClosed(){var active=CacheService.ActiveClaudeProcesses();if(active.Count==0)return true;Info(T("请先保存工作并正常退出 Claude 和 Claude Code，再重试。","Save your work and exit Claude and Claude Code normally before retrying."));return false;}
        private static void CheckClosed(){if(CacheService.ActiveClaudeProcesses().Count>0)throw new InvalidOperationException("close_claude_first");}
        private async void RepairSelected(){
            if(scan==null){Info(T("请先检查配置。","Check settings first."));return;}
            var options=new RepairOptions{SettingsOfficial=official.Checked,SettingsProxy=proxy.Checked,UserEnvironmentOfficial=userEnvironment.Checked};
            var names=new List<string>();if(options.SettingsOfficial)names.AddRange(scan.OfficialSettingKeys);if(options.SettingsProxy)names.AddRange(scan.ProxySettingKeys);if(options.UserEnvironmentOfficial)names.AddRange(scan.UserEnvironmentKeys);
            if(names.Count==0){Info(T("请选择需要处理的项目。","Select items to handle."));return;}
            if(!Review(T("先备份，再移除以下覆盖项。用户环境变量影响之后打开的程序。","Back up first, then remove these overrides. User variables affect newly opened processes.")+"\r\n\r\n"+String.Join("\r\n",names))||!RequireClosed())return;
            await Run(T("正在备份与恢复配置…","Backing up and restoring settings…"),token=>Task.Run(()=>{CheckClosed();var result=repair.Apply(scan,options);var next=repair.Scan();return (Action)delegate{scan=next;ShowScan();Result(result.Success,result.BackupDirectory,String.Join("\r\n",result.Changed.Concat(result.Skipped))+(String.IsNullOrEmpty(result.Error)?"":"\r\n"+result.Error));};}),false);
        }
        private void ExportReport(){if(scan==null){Info(T("请先检查配置。","Check settings first."));return;}using(var d=new SaveFileDialog{Filter="JSON|*.json",FileName="Claude-check-redacted.json",OverwritePrompt=false})if(d.ShowDialog(this)==DialogResult.OK){try{ReportExporter.SaveNew(d.FileName,scan,configRoot,Ui.DataRoot);SetStatus(T("已导出无密钥值报告。","Report exported without credential values."));}catch{Info(T("请使用配置和备份目录以外的新文件名。","Use a new filename outside configuration and backup folders."));}}}
        private void BuildCaches(){
            var p=Rows(72,48,-1,52,64);Page("缓存与日志","Caches & logs").Controls.Add(p);
            p.Controls.Add(Note(T("扫描 Claude Code / Desktop 已知路径中的缓存、统计、遥测与日志。\r\n先列出具体位置与大小；清理前完整备份并核验，再移入回收站。","Scan known Claude Code / Desktop cache, statistics, telemetry and log paths.\r\nReview exact locations and sizes. Verify complete backups before moving originals to the Recycle Bin.")),0,0);
            var a=Actions();a.Controls.Add(ActionButton(T("扫描缓存","Scan caches"),ScanCaches,true,180));p.Controls.Add(a,0,1);
            caches=new ListView{Dock=DockStyle.Fill,View=View.Details,CheckBoxes=true,FullRowSelect=true,HideSelection=false,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink};caches.Columns.Add(T("项目","Item"),185);caches.Columns.Add(T("大小","Size"),85);caches.Columns.Add(T("位置 / 状态","Location / status"),570);
            caches.ItemCheck+=delegate(object sender,ItemCheckEventArgs e){var item=caches.Items[e.Index].Tag as CacheTarget;if(item==null||!item.Eligible)e.NewValue=CheckState.Unchecked;};p.Controls.Add(Theme.Card(caches,new Padding(0,7,0,7)),0,2);
            var b=Actions();b.Controls.Add(ActionButton(T("预览并清理所选","Review & clean selected"),CleanCaches,true,260));p.Controls.Add(b,0,3);
            p.Controls.Add(Note(T("不删除账号凭据、Cookie、聊天、Skills、插件、Hooks、自建规则或桌面持久数据。\r\n缓存可能重新生成；清理后不会强制重新登录。未知目录与链接路径跳过。","Preserve credentials, cookies, chats, skills, plugins, hooks, custom rules and persistent data.\r\nCaches may regenerate; cleanup does not intentionally sign you out. Unknown or linked paths are skipped.")),0,4);
        }
        private async void ScanCaches(){if(cache==null){Info(T("缓存或备份路径未通过安全检查，不能清理。","A cache or backup path failed safety checks; cleanup is unavailable."));return;}caches.Items.Clear();await Run(T("正在扫描缓存…","Scanning caches…"),token=>Task.Run(()=>{var found=cache.Audit(token);return (Action)delegate{ShowCaches(found);};}),true);}
        private void ShowCaches(List<CacheTarget> found){caches.Items.Clear();foreach(var item in found){var row=new ListViewItem(item.Name){Tag=item};row.SubItems.Add(SizeText(item.Bytes));row.SubItems.Add(item.Eligible?item.Path:item.Path+" · "+(demo?item.Warning:ResultText.Render(item.Warning,Ui.English)));if(!item.Eligible)row.ForeColor=Theme.Muted;caches.Items.Add(row);}SetStatus(found.Count==0?T("未发现可识别的缓存。","No recognized cache found."):T("请勾选要清理的项目。","Select the caches you want to clean."));}
        private async void CleanCaches(){
            var selected=caches.CheckedItems.Cast<ListViewItem>().Select(x=>x.Tag as CacheTarget).Where(x=>x!=null&&x.Eligible).ToList();
            if(selected.Count==0){Info(T("请先扫描并勾选缓存。","Scan and select caches first."));return;}
            if(!Review(String.Join("\r\n\r\n",selected.Select(x=>x.Name+" · "+SizeText(x.Bytes)+"\r\n"+x.Path))+"\r\n\r\n"+T("备份核验后，以上项目才会移入回收站。","These items go to the Recycle Bin only after backup verification."))||!RequireClosed())return;
            await Run(T("正在备份与清理…","Backing up and cleaning…"),token=>Task.Run(()=>{var r=cache.Clean(selected,CancellationToken.None);return (Action)delegate{caches.Items.Clear();Result(r.Ok,r.BackupDirectory,ResultText.Render(String.Join("\r\n",r.Messages),Ui.English));};}),false);
        }
        private void BuildBackups(){
            var p=Rows(76,48,-1,52,64);Page("恢复备份","Restore backups").Controls.Add(p);
            p.Controls.Add(Note(T("查看本工具保存的配置与缓存备份。还原前核验内容，跳过已发生后续修改的目标。\r\n备份可能含私人信息，请留在本机。","Review configuration and cache backups created by this tool. Restore verifies content and skips conflicting later changes.\r\nBackups may contain private data; keep them on your PC.")),0,0);
            var a=Actions();a.Controls.Add(ActionButton(T("读取备份记录","List backups"),LoadBackups,true,200));p.Controls.Add(a,0,1);
            backups=new ListBox{Dock=DockStyle.Fill,HorizontalScrollbar=true,IntegralHeight=false,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink};p.Controls.Add(Theme.Card(backups,new Padding(0,7,0,7)),0,2);
            var b=Actions();b.Controls.Add(ActionButton(T("恢复所选备份","Restore selected"),RestoreBackup,true,230));p.Controls.Add(b,0,3);
            p.Controls.Add(Note(T("请先正常退出 Claude。重新生成的缓存不会被覆盖。\r\n时区、浏览器偏好与字体优化，请返回主界面使用「撤回上次优化」。","Exit Claude normally first. Recreated caches are not overwritten.\r\nFor timezone, browser preference and font optimization, use Undo last optimization on the main screen.")),0,4);
        }
        private async void LoadBackups(){if(repair==null||cache==null){Info(T("配置或缓存备份路径未通过安全检查，暂不读取恢复记录。","Configuration or cache backup paths failed safety checks; restore records are unavailable."));return;}await Run(T("正在读取备份记录…","Reading backup records…"),token=>Task.Run(()=>{var found=repair.ListBackups().Select(x=>new BackupItem{Kind="Configuration",Path=x}).Concat(cache.ListBackups().Select(x=>new BackupItem{Kind="Cache",Path=x})).OrderByDescending(x=>Path.GetFileName(x.Path)).ToList();return (Action)delegate{backups.Items.Clear();foreach(var item in found)backups.Items.Add(item);SetStatus(T("找到 ","Found ")+found.Count+T(" 份备份。"," backups."));};}),false);}
        private async void RestoreBackup(){
            var selected=backups.SelectedItem as BackupItem;if(selected==null){Info(T("请先选择备份。","Select a backup first."));return;}
            if(!Review(selected.Path+"\r\n\r\n"+T("核验后恢复；保留发生冲突的后续修改。","Verify and restore; preserve conflicting later edits."))||!RequireClosed())return;
            await Run(T("正在核验并恢复…","Verifying and restoring…"),token=>Task.Run(()=>{CheckClosed();if(selected.Kind=="Configuration"){var r=repair.Restore(selected.Path);return (Action)delegate{scan=null;official.Enabled=proxy.Enabled=userEnvironment.Enabled=false;Result(r.Success,selected.Path,String.Join("\r\n",r.Changed.Concat(r.Skipped))+(String.IsNullOrEmpty(r.Error)?"":"\r\n"+r.Error));};}var restored=cache.Restore(selected.Path,CancellationToken.None);return (Action)delegate{Result(restored.Ok,selected.Path,ResultText.Render(String.Join("\r\n",restored.Messages),Ui.English));};}),false);
        }
        private async Task Run(string text,Func<CancellationToken,Task<Action>> work,bool cancellable){
            if(demo||busy)return;busy=true;tabs.Enabled=false;language.Enabled=false;cancel.Visible=cancellable;cancellation=new CancellationTokenSource();SetStatus(text);
            try{var done=await work(cancellation.Token);if(!IsDisposed){SetStatus(T("操作返回，请查看结果。","Operation returned. Review the result."));done();}}
            catch(OperationCanceledException){SetStatus(T("检查已取消。","Check cancelled."));}
            catch(Exception ex){SetStatus(ex.Message=="close_claude_first"?T("Claude 正在运行，请先正常退出。","Claude is running; exit normally first."):T("操作未完成：","Operation incomplete: ")+ex.GetType().Name);}
            finally{busy=false;if(!IsDisposed){tabs.Enabled=true;language.Enabled=true;cancel.Visible=false;}cancellation.Dispose();cancellation=null;}
        }
        private void ShowDemo(){ShowDiagnostics(DiagnosticsService.DemoReport());configText.Text=T("示例：发现 ANTHROPIC_BASE_URL / ANTHROPIC_MODEL 覆盖项。\r\n没有读取本机配置。","Sample: ANTHROPIC_BASE_URL / ANTHROPIC_MODEL overrides found.\r\nNo local configuration was read.");ShowCaches(new List<CacheTarget>{new CacheTarget{Name="Code cache (sample)",Path=@"Example\.claude\cache",Bytes=2097152,Eligible=false,Warning=T("仅演示","Demo only")}});backups.Items.Add(T("示例备份 · 尚未执行清理","Sample backup · cleanup has not run"));SetStatus(T("演示模式 · 全部为示例数据","Demo mode · sample data only"));}
        private void SetStatus(string text){status.Text="● "+text;}
        private void Info(string text){MessageBox.Show(this,text,T("大肥鱼提示","Whale Helper"),MessageBoxButtons.OK,MessageBoxIcon.Information);}
        private bool Review(string text){if(demo)return false;using(var d=new Form{Icon=this.Icon,ShowInTaskbar=false,Text=T("确认处理范围","Review the changes"),StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(720,480),MinimumSize=new Size(650,420),Font=Font,BackColor=Theme.Cream,ForeColor=Theme.Ink}){var p=Rows(-1,50);p.Padding=new Padding(18);var box=ReadBox();box.Text=text;p.Controls.Add(Theme.Card(box,new Padding(0,0,0,10)),0,0);var a=Actions();var yes=Theme.Button(T("备份并应用","Back up & apply"),true);yes.Width=220;yes.DialogResult=DialogResult.OK;var no=Theme.Button(T("取消","Cancel"),false);no.Width=130;no.DialogResult=DialogResult.Cancel;a.Controls.Add(yes);a.Controls.Add(no);p.Controls.Add(a,0,1);d.Controls.Add(p);d.CancelButton=no;return d.ShowDialog(this)==DialogResult.OK;}}
        private void Result(bool ok,string backup,string detail){SetStatus(ok?T("操作完成。","Operation complete."):T("未全部完成，请查看结果。","Not fully completed; review the result."));Info((ok?T("操作完成。","Operation complete."):T("未全部完成。","Not fully completed."))+"\r\n"+detail+(String.IsNullOrEmpty(backup)?"":"\r\n\r\n"+T("备份：","Backup: ")+backup));}
        private static string SizeText(long n){return n<1048576?(n/1024d).ToString("0.0")+" KB":(n/1048576d).ToString("0.00")+" MB";}
    }
}
