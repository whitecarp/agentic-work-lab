using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WhaleClaudeHelper {
    internal sealed class BackupRow {
        internal string Kind, Path;
        public override string ToString(){return Kind+"  ·  "+System.IO.Path.GetFileName(Path);}
    }
    internal sealed class ZoneRow {
        internal TimeZoneInfo Zone;
        public override string ToString(){return Zone.DisplayName+"  ·  "+Zone.Id;}
    }
    internal sealed class HelperForm : Form {
        private readonly bool preview;
        private bool busy,fontsLoaded;
        private string configRoot=Ui.ConfigRoot;
        private RepairEngine repair;
        private RepairScan scan;
        private readonly CacheService cache=new CacheService();
        private readonly FontService fonts=new FontService();
        private List<FontEntry> fontEntries=new List<FontEntry>();
        private TabControl tabs;
        private Label status,pathLabel,zoneCurrent,fontInfo;
        private TextBox scanText,fontSearch;
        private CheckBox official,proxies,userEnv;
        private ListView cacheList,fontList;
        private ComboBox zones;
        private List<ZoneRow> allZones=new List<ZoneRow>();
        private bool filteringZones;
        private ListBox backups;
        private string lastBackup;
        private static string T(string zh,string en){return Ui.T(zh,en);}
        internal HelperForm(bool previewMode){
            preview=previewMode;Font=new Font("Microsoft YaHei UI",10f);BackColor=Theme.Cream;ForeColor=Theme.Ink;
            AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(960,750);MinimumSize=new Size(860,650);
            StartPosition=FormStartPosition.CenterScreen;
            try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
            Build();Shown+=delegate{if(!preview)ScanConfiguration();};
            FormClosing+=delegate(object s,FormClosingEventArgs e){if(busy){e.Cancel=true;SetStatus(T("正在完成操作，请稍候再关闭。","An operation is finishing. Please wait before closing."));}};
        }
        private void Build(){
            Text=T("大肥鱼 Claude 助手 0.1.0","Whale Claude Helper 0.1.0");Controls.Clear();fontsLoaded=false;
            repair=new RepairEngine(configRoot,Path.Combine(Ui.DataRoot,"Backups","Configuration"));scan=null;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(24,15,24,12)};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,105));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));
            var head=new Panel{Dock=DockStyle.Fill};
            head.Controls.Add(new WhaleBadge{Location=new Point(2,0),Size=new Size(115,90)});
            var title=new Label{Text=T("大肥鱼 Claude 助手","Whale Claude Helper"),Font=new Font(Font.FontFamily,23,FontStyle.Bold),Location=new Point(130,9),Size=new Size(575,42)};
            head.Controls.Add(title);head.Controls.Add(new Label{Text=T("检查清楚，再帮你改好。每次修改，都留一份备份。","Check first. Fix what you choose. Keep a backup."),Location=new Point(132,56),Size=new Size(630,30),ForeColor=Theme.Muted});
            var lang=Button(T("English","中文"),delegate{if(busy)return;Ui.English=!Ui.English;Ui.Save();int page=tabs.SelectedIndex;Build();tabs.SelectedIndex=page;if(!preview)ScanConfiguration();},false,100);
            lang.Anchor=AnchorStyles.Top|AnchorStyles.Right;lang.Location=new Point(780,15);head.Controls.Add(lang);
            head.Resize+=delegate{lang.Left=head.ClientSize.Width-lang.Width-5;};layout.Controls.Add(head,0,0);
            tabs=new TabControl{Dock=DockStyle.Fill,Padding=new Point(20,10)};
            BuildRepair();BuildCache();BuildZone();BuildFonts();BuildBackups();BuildAbout();
            tabs.SelectedIndexChanged+=delegate{if(preview||busy)return;if(tabs.SelectedIndex==3&&!fontsLoaded)LoadFonts();if(tabs.SelectedIndex==4)LoadBackups();};
            layout.Controls.Add(tabs,0,1);
            status=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.BlueDark,AutoEllipsis=true,Text=T("● 先检查，再选择需要修改的项目。","● Start with a check, then choose what to change.")};
            layout.Controls.Add(status,0,2);Controls.Add(layout);
        }
        private Button Button(string caption,Action action,bool primary,int width){
            var b=Theme.Button(caption,primary);b.Width=width;b.Click+=delegate{if(!busy)action();};return b;
        }
        private TabPage Page(string zh,string en){var p=new TabPage(T(zh,en)){BackColor=Theme.Cream,Padding=new Padding(18)};tabs.TabPages.Add(p);return p;}
        private Label Note(string text,int height){return new Label{Text=text,Dock=DockStyle.Fill,Height=height,ForeColor=Theme.Muted,TextAlign=ContentAlignment.MiddleLeft};}
        private TextBox ReadBox(){return new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink};}
        private FlowLayoutPanel Actions(){return new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Margin=new Padding(0)};}
        private TableLayoutPanel Rows(params float[] heights){
            var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=heights.Length,Margin=new Padding(0)};
            foreach(float h in heights)p.RowStyles.Add(new RowStyle(h<0?SizeType.Percent:SizeType.Absolute,h<0?100:h));return p;
        }
        private void BuildRepair(){
            var page=Page("体检与修复","Check & repair");var p=Rows(52,45,-1,36,36,36,50,48);page.Controls.Add(p);
            pathLabel=Note(configRoot,52);pathLabel.AutoEllipsis=true;p.Controls.Add(pathLabel,0,0);
            var top=Actions();top.Controls.Add(Button(T("开始体检","Run check"),ScanConfiguration,true,125));
            top.Controls.Add(Button(T("选择配置目录","Choose folder"),ChooseRoot,false,155));top.Controls.Add(Button(T("导出检查结果","Export report"),ExportReport,false,160));p.Controls.Add(top,0,1);
            scanText=ReadBox();scanText.Text=T("检查 Claude Code 的连接设置、模型覆盖与代理。\r\n这里只显示设置项名称，不显示密钥。","Check Claude Code connection, model and proxy overrides.\r\nOnly setting names are displayed; credentials stay private.");p.Controls.Add(Theme.Card(scanText,new Padding(0,8,0,10)),0,2);
            official=new CheckBox{Text=T("恢复官方连接：移除配置中的地址、认证和模型覆盖","Official connection: remove endpoint, auth and model overrides"),Dock=DockStyle.Fill,Enabled=false};
            proxies=new CheckBox{Text=T("移除 Claude 配置里的代理（独立选项）","Remove proxies inside Claude settings (optional)"),Dock=DockStyle.Fill,Enabled=false};
            userEnv=new CheckBox{Text=T("移除当前用户的 Claude 专用环境变量","Remove current-user Claude environment overrides"),Dock=DockStyle.Fill,Enabled=false};
            p.Controls.Add(official,0,3);p.Controls.Add(proxies,0,4);p.Controls.Add(userEnv,0,5);
            var action=Actions();action.Controls.Add(Button(T("预览并修复所选","Review & repair"),RepairSelected,true,185));p.Controls.Add(action,0,6);
            p.Controls.Add(Note(T("保留会话、登录凭据、Skills、插件、Hooks、MCP 和身份标识。\r\n修改前请退出 Claude；修复后重新打开 Claude 和相关终端。","Keep chats, login data, skills, plugins, hooks, MCP and identity.\r\nExit Claude before repair; reopen Claude and related terminals afterwards."),48),0,7);
        }
        private void ChooseRoot(){using(var d=new FolderBrowserDialog{Description=T("选择 Claude Code 配置目录（通常是 .claude）","Choose the Claude Code configuration folder (usually .claude)"),SelectedPath=configRoot,ShowNewFolderButton=false})if(d.ShowDialog(this)==DialogResult.OK){configRoot=d.SelectedPath;repair=new RepairEngine(configRoot,Path.Combine(Ui.DataRoot,"Backups","Configuration"));pathLabel.Text=configRoot;ScanConfiguration();}}
        private void ScanConfiguration(){Run(T("正在只读检查…","Checking without changing files…"),delegate{var result=repair.Scan();return (Action)delegate{scan=result;ShowScan();};});}
        private void ShowScan(){
            official.Checked=proxies.Checked=userEnv.Checked=false;
            official.Enabled=scan.SettingsValid&&scan.OfficialSettingKeys.Count>0;proxies.Enabled=scan.SettingsValid&&scan.ProxySettingKeys.Count>0;userEnv.Enabled=String.IsNullOrEmpty(scan.EnvironmentError)&&scan.UserEnvironmentKeys.Count>0;
            var s=new StringBuilder();s.AppendLine(T("连接与模型覆盖","Connection & model overrides"));AddKeys(s,scan.OfficialSettingKeys);
            s.AppendLine();s.AppendLine(T("Claude 配置内的代理","Proxies inside Claude settings"));AddKeys(s,scan.ProxySettingKeys);
            s.AppendLine();s.AppendLine(T("当前用户的 Claude 环境变量","Current-user Claude environment overrides"));AddKeys(s,scan.UserEnvironmentKeys);
            if(scan.OtherOverrideKeys.Count>0){s.AppendLine();s.AppendLine(T("需手动核对（本版只检查）","Review manually (not changed by this version)"));AddKeys(s,scan.OtherOverrideKeys);}
            if(!scan.SettingsExists)s.AppendLine(T("未找到 settings.json；不会新建或替换。","settings.json was not found; it will not be created or replaced."));
            if(!String.IsNullOrEmpty(scan.SettingsError))s.AppendLine(T("配置无法安全处理：","Settings cannot be safely edited: ")+scan.SettingsError);
            if(!String.IsNullOrEmpty(scan.EnvironmentError))s.AppendLine(T("环境变量检查未完成：","Environment check incomplete: ")+scan.EnvironmentError);
            s.AppendLine();s.AppendLine(T("检查范围：所选用户配置和当前用户环境变量。\r\n项目配置、组织策略、已运行进程和 CC Switch 接管仍需另行核对。","Scope: selected user settings and current-user environment.\r\nProject settings, managed policy, running processes and CC Switch takeover need separate review."));
            if(!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR")))s.AppendLine(T("检测到 CLAUDE_CONFIG_DIR；如需检查该目录，请用「选择配置目录」自行选择。","CLAUDE_CONFIG_DIR is set. Use Choose folder if you want to inspect that location."));
            scanText.Text=s.ToString();SetStatus(T("只读检查完成。勾选需要恢复的项目。","Check complete. Select the items you want to restore."));
        }
        private static void AddKeys(StringBuilder s,IEnumerable<string> keys){var all=keys.ToArray();s.AppendLine(all.Length==0?T("  未发现覆盖项","  No overrides found"):"  "+String.Join("\r\n  ",all));}
        private bool ClaudeClosed(){var processes=CacheService.ActiveClaudeProcesses();if(processes.Count==0)return true;Info(T("请先正常退出 Claude 和 Claude Code，再执行此操作。\r\n","Exit Claude and Claude Code normally before this operation.\r\n")+String.Join(", ",processes));return false;}
        private void RepairSelected(){
            if(scan==null){Info(T("请先体检。","Run a check first."));return;}
            var options=new RepairOptions{SettingsOfficial=official.Checked,SettingsProxy=proxies.Checked,UserEnvironmentOfficial=userEnv.Checked};
            if(!options.SettingsOfficial&&!options.SettingsProxy&&!options.UserEnvironmentOfficial){Info(T("请勾选要修复的项目。","Select at least one item to repair."));return;}
            var changes=new List<string>();if(options.SettingsOfficial)changes.AddRange(scan.OfficialSettingKeys.Select(x=>"settings: "+x));if(options.SettingsProxy)changes.AddRange(scan.ProxySettingKeys.Select(x=>"settings.env: "+x));if(options.UserEnvironmentOfficial)changes.AddRange(scan.UserEnvironmentKeys.Select(x=>"HKCU: "+x));
            if(!Review(T("修复这些设置","Repair these settings"),T("先完整备份目标配置，再移除以下覆盖项：\r\n","Back up the target configuration, then remove these overrides:\r\n")+String.Join("\r\n",changes)+"\r\n\r\n"+T("用户环境变量会影响新打开的 Claude 程序；不会修改全局共用代理。","User environment changes affect newly opened Claude processes. Shared global proxies stay unchanged.")))return;
            if(!ClaudeClosed())return;
            Run(T("正在备份和修复…","Backing up and repairing…"),delegate{RequireClaudeClosed();var r=repair.Apply(scan,options);var next=repair.Scan();return (Action)delegate{lastBackup=r.BackupDirectory;scan=next;ShowScan();Result(r.Success,r.BackupDirectory,String.Join("\r\n",r.Changed.Concat(r.Skipped))+(r.Error==null?"":"\r\n"+r.Error));};});
        }
        private void ExportReport(){if(scan==null){Info(T("请先体检。","Run a check first."));return;}using(var d=new SaveFileDialog{Filter="JSON|*.json",FileName="Claude-check-redacted.json",OverwritePrompt=false})if(d.ShowDialog(this)==DialogResult.OK){try{ReportExporter.SaveNew(d.FileName,scan,configRoot,Ui.DataRoot);SetStatus(T("已导出检查结果（无密钥值）。","Report exported without credential values."));}catch{Info(T("请使用一个新的文件名，并保存到配置与备份目录之外。不会覆盖已有文件。","Use a new filename outside configuration and backup folders. Existing files are never overwritten."));}}}
        private void BuildCache(){
            var page=Page("缓存","Cache");var p=Rows(68,48,-1,56,46);page.Controls.Add(p);
            p.Controls.Add(Note(T("只清理白名单内的缓存与日志；先备份，再移入回收站。\r\n包括默认 .claude 和原生 Claude Desktop 缓存；不清空整个目录。","Clean only allowlisted caches and logs, with a backup and Recycle Bin.\r\nCovers default .claude and native Claude Desktop caches."),68),0,0);
            var action=Actions();action.Controls.Add(Button(T("检查缓存","Scan caches"),ScanCaches,true,145));p.Controls.Add(action,0,1);
            cacheList=List(new[]{T("缓存项目","Cache"),T("大小","Size"),T("位置 / 状态","Location / status")},new[]{200,95,490});cacheList.CheckBoxes=true;
            cacheList.ItemCheck+=delegate(object s,ItemCheckEventArgs e){var item=cacheList.Items[e.Index].Tag as CacheTarget;if(item!=null&&!item.Eligible)e.NewValue=CheckState.Unchecked;};p.Controls.Add(Theme.Card(cacheList,new Padding(0,6,0,6)),0,2);
            var bottom=Actions();bottom.Controls.Add(Button(T("预览并清理所选","Review & clean"),CleanCaches,true,185));p.Controls.Add(bottom,0,3);
            p.Controls.Add(Note(T("会话、项目、Cookie、登录状态、桌面端持久数据和其他实例配置均保留。","Chats, projects, cookies, login state, persistent desktop data and other instance settings are kept."),46),0,4);
        }
        private void ScanCaches(){Run(T("正在统计缓存…","Scanning caches…"),delegate{var found=cache.Audit(CancellationToken.None);return (Action)delegate{cacheList.Items.Clear();foreach(var c in found){var row=new ListViewItem(c.Name){Tag=c};row.SubItems.Add(SizeText(c.Bytes));row.SubItems.Add(c.Eligible?c.Path:c.Warning);if(!c.Eligible)row.ForeColor=Theme.Muted;cacheList.Items.Add(row);}SetStatus(T("找到 ","Found ")+found.Count+T(" 项。选择需要清理的缓存。"," items. Select caches to clean."));};});}
        private void CleanCaches(){var selected=cacheList.CheckedItems.Cast<ListViewItem>().Select(x=>(CacheTarget)x.Tag).Where(x=>x.Eligible).ToList();if(selected.Count==0){Info(T("请先检查并勾选缓存。","Scan and select caches first."));return;}
            if(!Review(T("清理所选缓存","Clean selected caches"),String.Join("\r\n",selected.Select(x=>x.Name+"  "+SizeText(x.Bytes)+"\r\n"+x.Path))+"\r\n\r\n"+T("备份核验成功后，这些文件才会进入回收站。","Files go to the Recycle Bin only after backups are verified.")))return;if(!ClaudeClosed())return;
            Run(T("正在备份并清理…","Backing up and cleaning…"),delegate{var r=cache.Clean(selected,CancellationToken.None);return (Action)delegate{lastBackup=r.BackupDirectory;Result(r.Ok,r.BackupDirectory,String.Join("\r\n",r.Messages));cacheList.Items.Clear();};});}
        private void BuildZone(){
            var page=Page("时区","Time zone");var p=Rows(65,60,50,72,58,-1);page.Controls.Add(p);
            p.Controls.Add(Note(T("在这里直接更改 Windows 时区。输入地区名或时区名称查找。","Change the Windows time zone here. Type a region or time zone name to find it."),65),0,0);
            zoneCurrent=new Label{Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,12,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft};p.Controls.Add(zoneCurrent,0,1);
            zones=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDown,AutoCompleteMode=AutoCompleteMode.None,MaxDropDownItems=12,DropDownHeight=300};
            try{allZones=SystemTools.ListTimeZones().Select(z=>new ZoneRow{Zone=z}).ToList();zones.Items.AddRange(allZones.Cast<object>().ToArray());UpdateZone();}catch{zoneCurrent.Text=T("无法读取系统时区。","Cannot read the system time zone.");}
            zones.TextUpdate+=delegate{FilterZones(zones.Text);};zones.DropDown+=delegate{if(!filteringZones&&allZones.Any(x=>x.ToString()==zones.Text))FilterZones("");};p.Controls.Add(zones,0,2);
            p.Controls.Add(Note(T("会影响整台电脑显示的本地时间；不会修改网络出口。\r\n保留原时区备份，可在「备份」页恢复。","This changes local time display across Windows, not your network location.\r\nThe original setting is saved for restoration in Backups."),72),0,3);
            var a=Actions();a.Controls.Add(Button(T("应用所选时区","Apply time zone"),ApplyZone,true,190));p.Controls.Add(a,0,4);
            var help=ReadBox();help.Text=T("小提示\r\n\r\n新加坡 / 北京：UTC+08:00\r\n美国中部：Central Standard Time（自动跟随夏令时）\r\n\r\n地区名称由 Windows 提供，可能随系统语言显示。\r\n系统若拒绝权限，软件会保留原设置并说明结果。","Quick tips\r\n\r\nSingapore / Beijing: UTC+08:00\r\nUS Central: Central Standard Time (observes daylight saving time)\r\n\r\nTime zone names are supplied by Windows in its system language.\r\nIf Windows denies permission, the original setting is retained.");p.Controls.Add(Theme.Card(help,new Padding(0,10,0,0)),0,5);
        }
        private void FilterZones(string query){if(filteringZones)return;filteringZones=true;try{string text=zones.Text;int caret=zones.SelectionStart;zones.BeginUpdate();zones.Items.Clear();zones.Items.AddRange(allZones.Where(x=>x.ToString().IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0).Cast<object>().ToArray());zones.EndUpdate();zones.Text=text;zones.SelectionStart=Math.Min(caret,text.Length);zones.SelectionLength=0;if(zones.Focused&&!zones.DroppedDown)zones.DroppedDown=true;}finally{filteringZones=false;}}
        private void UpdateZone(){try{string id=SystemTools.CurrentTimeZoneId();zoneCurrent.Text=T("当前时区：","Current time zone: ")+id;filteringZones=true;zones.Items.Clear();zones.Items.AddRange(allZones.Cast<object>().ToArray());foreach(ZoneRow z in zones.Items)if(z.Zone.Id==id){zones.SelectedItem=z;break;}}catch{zoneCurrent.Text=T("当前时区读取失败，可稍后重新打开本页核对。","Current time zone could not be refreshed. Reopen this page to check.");}finally{filteringZones=false;}}
        private void ApplyZone(){var selected=zones.SelectedItem as ZoneRow;if(selected==null||zones.Text!=selected.ToString()){selected=allZones.FirstOrDefault(x=>x.Zone.Id.Equals(zones.Text.Trim(),StringComparison.OrdinalIgnoreCase)||x.ToString()==zones.Text);}if(selected==null){Info(T("请从列表选中一个有效时区。","Select a valid time zone from the list."));return;}
            string id=selected.Zone.Id;if(!Review(T("更改 Windows 时区","Change Windows time zone"),T("原时区：","From: ")+SystemTools.CurrentTimeZoneId()+"\r\n"+T("新时区：","To: ")+id+"\r\n\r\n"+T("将直接应用到整台电脑。原设置先备份。","Apply to Windows now, after backing up the original setting.")))return;
            Run(T("正在应用时区…","Applying time zone…"),delegate{var r=SystemTools.ApplyTimeZone(id);return (Action)delegate{lastBackup=r.BackupDirectory;UpdateZone();Result(r.Ok,r.BackupDirectory,Ui.English?r.MessageEnglish:r.Message);};});}
        private void BuildFonts(){
            var page=Page("字体","Fonts");var p=Rows(50,48,42,-1,42,51);page.Controls.Add(p);
            p.Controls.Add(Note(T("直接安装 .ttf / .otf 字体到当前用户。系统字体只读，用户字体可备份移除。","Install .ttf / .otf fonts for your Windows user. System fonts are protected."),50),0,0);
            var a=Actions();a.Controls.Add(Button(T("刷新字体","Refresh fonts"),LoadFonts,false,145));a.Controls.Add(Button(T("选择字体并安装","Install font file"),InstallFont,true,180));p.Controls.Add(a,0,1);
            var searchRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,80));searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));searchRow.Controls.Add(Note(T("搜索字体","Search"),38),0,0);fontSearch=new TextBox{Dock=DockStyle.Fill};fontSearch.TextChanged+=delegate{FilterFonts();};searchRow.Controls.Add(fontSearch,1,0);p.Controls.Add(searchRow,0,2);
            fontList=List(new[]{T("字体","Font"),T("范围","Scope"),T("文件","File")},new[]{295,150,360});fontList.SelectedIndexChanged+=delegate{var f=SelectedFont();fontInfo.Text=f==null?"":(f.CanRemove?T("可备份移除：","Can be backed up and removed: "):T("保护 / 只读：","Protected / read-only: "))+f.Name;};p.Controls.Add(Theme.Card(fontList,new Padding(0,6,0,6)),0,3);
            fontInfo=Note(T("打开此页后读取已安装字体。","Installed fonts load when you open this page."),42);fontInfo.AutoEllipsis=true;p.Controls.Add(fontInfo,0,4);
            var b=Actions();b.Controls.Add(Button(T("备份并移除所选","Back up & remove"),RemoveFont,false,200));b.Controls.Add(Button(T("打开系统字体设置","Windows font settings"),delegate{Open("ms-settings:fonts");},false,220));p.Controls.Add(b,0,5);
        }
        private static ListView List(string[] titles,int[] widths){var v=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false,MultiSelect=false,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink};for(int i=0;i<titles.Length;i++)v.Columns.Add(titles[i],widths[i]);return v;}
        private FontEntry SelectedFont(){return fontList.SelectedItems.Count==0?null:fontList.SelectedItems[0].Tag as FontEntry;}
        private void LoadFonts(){Run(T("正在读取字体…","Reading installed fonts…"),delegate{var found=fonts.ListFonts();return (Action)delegate{fontEntries=found;fontsLoaded=true;FilterFonts();};});}
        private void FilterFonts(){if(fontList==null)return;string query=fontSearch.Text.Trim();fontList.BeginUpdate();fontList.Items.Clear();foreach(var f in fontEntries.Where(x=>query.Length==0||x.Name.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)){var row=new ListViewItem(f.Name){Tag=f};row.SubItems.Add(f.IsUser?T("当前用户","Current user"):T("系统 · 保护","System · protected"));row.SubItems.Add(f.Path);fontList.Items.Add(row);}fontList.EndUpdate();fontInfo.Text=T("显示 ","Showing ")+fontList.Items.Count+T(" 项。系统字体不会被移除。"," fonts. System fonts cannot be removed.");}
        private void InstallFont(){using(var d=new OpenFileDialog{Filter=T("字体文件 (*.ttf;*.otf)|*.ttf;*.otf","Font files (*.ttf;*.otf)|*.ttf;*.otf"),Multiselect=false,CheckFileExists=true})if(d.ShowDialog(this)==DialogResult.OK){string path=d.FileName;if(!Review(T("安装字体","Install font"),Path.GetFileName(path)+"\r\n\r\n"+T("直接安装到当前 Windows 用户。\r\n保留安装记录，可从备份页撤销。已打开的软件可能需要重新打开。","Install for the current Windows user.\r\nKeep a receipt so this installation can be undone from Backups. Open apps may need a restart.")))return;
                Run(T("正在安装字体…","Installing font…"),delegate{var r=fonts.Install(path);return (Action)delegate{lastBackup=r.BackupDirectory;Result(r.Ok,r.BackupDirectory,r.Message);fontsLoaded=false;fontEntries=fonts.ListFonts();FilterFonts();};});}}
        private void RemoveFont(){var f=SelectedFont();if(f==null){Info(T("请先选择一个用户字体。","Select a user-installed font first."));return;}if(!f.CanRemove){Info(T("此字体受保护或注册关系不明确，本工具不会移除。","This font is protected or its registration is ambiguous; it cannot be removed here."));return;}
            if(!Review(T("移除用户字体","Remove user font"),f.Name+"\r\n"+f.Path+"\r\n\r\n"+T("备份后移入回收站。使用此字体的文档可能显示替代字体；可在备份页恢复。","Back up, then move to the Recycle Bin. Documents may use a fallback font until you restore it from Backups.")))return;
            Run(T("正在备份并移除字体…","Backing up and removing font…"),delegate{var r=fonts.Remove(f);return (Action)delegate{lastBackup=r.BackupDirectory;Result(r.Ok,r.BackupDirectory,r.Message);fontEntries=fonts.ListFonts();FilterFonts();};});}
        private void BuildBackups(){var page=Page("备份","Backups");var p=Rows(70,48,-1,55,44);page.Controls.Add(p);p.Controls.Add(Note(T("修改前会保存原内容。恢复时核对当前状态，保留后来改过的内容。\r\n配置备份可能含密钥，请留在本机。","Original data is saved before changes. Restore checks for later edits and preserves conflicts.\r\nConfiguration backups may contain credentials; keep them on this PC."),70),0,0);
            var a=Actions();a.Controls.Add(Button(T("刷新备份","Refresh backups"),LoadBackups,false,165));a.Controls.Add(Button(T("打开备份目录","Open backups"),delegate{string root=Path.Combine(Ui.DataRoot,"Backups");if(Directory.Exists(root))Open(root);else Info(T("还没有备份；首次修改时会创建。","No backups yet. They are created with the first change."));},false,170));p.Controls.Add(a,0,1);
            backups=new ListBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,HorizontalScrollbar=true,ItemHeight=32,IntegralHeight=false,BackColor=Color.White,ForeColor=Theme.Ink};p.Controls.Add(Theme.Card(backups,new Padding(0,8,0,8)),0,2);
            var b=Actions();b.Controls.Add(Button(T("恢复所选备份","Restore selected"),RestoreBackup,true,190));p.Controls.Add(b,0,3);p.Controls.Add(Note(T("缓存原路径若已有新数据会跳过；字体安装记录可用于撤销安装。","Recreated cache paths are skipped. A font installation receipt can undo the installation."),44),0,4);
        }
        private void LoadBackups(){Run(T("正在读取备份记录…","Reading backup records…"),delegate{var rows=new List<BackupRow>();foreach(var x in repair.ListBackups())rows.Add(new BackupRow{Kind="Claude",Path=x});foreach(var x in cache.ListBackups())rows.Add(new BackupRow{Kind="Cache",Path=x});foreach(var x in SystemTools.ListTimeZoneBackups())rows.Add(new BackupRow{Kind="Timezone",Path=x});foreach(var x in fonts.ListBackups())rows.Add(new BackupRow{Kind="Fonts",Path=x});return (Action)delegate{backups.Items.Clear();foreach(var row in rows.OrderByDescending(x=>Path.GetFileName(x.Path)))backups.Items.Add(row);SetStatus(T("找到 ","Found ")+rows.Count+T(" 份备份。"," backups."));};});}
        private void RestoreBackup(){var row=backups.SelectedItem as BackupRow;if(row==null){Info(T("请先选择备份。","Select a backup first."));return;}if(!Review(T("恢复备份","Restore backup"),row.Path+"\r\n\r\n"+T("核对备份和当前状态后恢复。后续修改过的目标会保留并报告。","Verify the backup and current state before restoration. Targets changed since then are kept and reported.")))return;if((row.Kind=="Claude"||row.Kind=="Cache")&&!ClaudeClosed())return;
            Run(T("正在核验并恢复…","Verifying and restoring…"),delegate{bool ok;string detail;if(row.Kind=="Claude"){RequireClaudeClosed();var r=repair.Restore(row.Path);ok=r.Success;detail=String.Join("\r\n",r.Changed.Concat(r.Skipped))+"\r\n"+r.Error;}else if(row.Kind=="Cache"){var r=cache.Restore(row.Path,CancellationToken.None);ok=r.Ok;detail=String.Join("\r\n",r.Messages);}else if(row.Kind=="Timezone"){var r=SystemTools.RestoreTimeZone(row.Path);ok=r.Ok;detail=Ui.English?r.MessageEnglish:r.Message;}else{var r=fonts.Restore(row.Path);ok=r.Ok;detail=r.Message;}return (Action)delegate{Result(ok,row.Path,detail);fontsLoaded=false;UpdateZone();};});}
        private void BuildAbout(){var page=Page("关于","About");var p=Rows(-1,54);page.Controls.Add(p);var text=ReadBox();text.Text=T("大肥鱼 Claude 助手 · 0.1.0\r\n\r\n一个能检查、修改和恢复的本地工具。\r\n\r\n• 体检：只显示配置项名称，不显示密码、令牌和认证头。\r\n• 修复：针对选定配置，不影响历史会话和其他工具数据。\r\n• 时区与字体：在软件内直接应用，不只是跳转系统设置。\r\n• 备份：保存到本机，恢复时核对是否发生后续更改。\r\n\r\n本地修改不能决定账号资格、服务端地区限制或订阅状态。\r\n本工具不是 Anthropic 官方产品。\r\n\r\n参考 LinXiaoTao / FuckClaude（MIT）。该项目也提供浏览器扩展；本工具是独立 Windows 实现。\r\n","Whale Claude Helper · 0.1.0\r\n\r\nA local tool to inspect, change and restore settings.\r\n\r\n• Check: show setting names, never passwords, tokens or authentication headers.\r\n• Repair: edit selected configuration while preserving chats and other tools.\r\n• Time zones & fonts: apply changes inside this app.\r\n• Backups: keep originals locally and detect later changes before restoring.\r\n\r\nLocal changes cannot determine account eligibility, service-side region restrictions or subscription status. This is not an Anthropic product.\r\n\r\nInspired by LinXiaoTao / FuckClaude (MIT), which also provides a browser extension. This tool is an independent Windows implementation.\r\n");
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("UpstreamLicense"))if(stream!=null)using(var r=new StreamReader(stream))text.AppendText("\r\n"+r.ReadToEnd());p.Controls.Add(Theme.Card(text,new Padding(0,0,0,8)),0,0);
            var a=Actions();a.Controls.Add(Button(T("上游项目","Upstream project"),delegate{Open("https://github.com/LinXiaoTao/FuckClaude");},false,175));a.Controls.Add(Button(T("Claude 官方配置文档","Official Claude settings"),delegate{Open("https://code.claude.com/docs/en/settings");},false,235));p.Controls.Add(a,0,1);
        }
        private static void RequireClaudeClosed(){if(CacheService.ActiveClaudeProcesses().Count>0)throw new InvalidOperationException("close_claude_first");}
        private void Run(string label,Func<Action> work){if(busy)return;busy=true;tabs.Enabled=false;SetStatus(label);Task.Factory.StartNew(delegate{Action done;try{done=work();}catch(Exception ex){string category=ex.Message=="close_claude_first"?T("请先正常退出 Claude，再试一次。","Please exit Claude normally and try again."):ex.GetType().Name;done=delegate{Info(T("操作未完成：","Operation did not complete: ")+category);};}if(IsDisposed)return;try{BeginInvoke((Action)delegate{busy=false;tabs.Enabled=true;SetStatus(T("操作已返回，请查看结果。","Operation returned. Review the result."));try{done();}catch{Info(T("结果界面未能刷新。请到备份页核对记录后再操作。","The result view could not refresh. Check the backup records before trying again.")+(String.IsNullOrEmpty(lastBackup)?"":"\r\n"+lastBackup));}});}catch(InvalidOperationException){} });}
        private void SetStatus(string text){if(status!=null)status.Text="● "+text;}
        private void Info(string text){MessageBox.Show(this,text,T("大肥鱼提示","Whale Helper"),MessageBoxButtons.OK,MessageBoxIcon.Information);}
        private bool Review(string title,string details){if(preview)return false;using(var dialog=new Form{Text=title,StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(650,440),MinimumSize=new Size(600,400),Font=Font,BackColor=Theme.Cream,ForeColor=Theme.Ink,MinimizeBox=false,MaximizeBox=false}){var p=Rows(-1,55);p.Padding=new Padding(18);dialog.Controls.Add(p);var t=ReadBox();t.Text=details;p.Controls.Add(Theme.Card(t,new Padding(0,0,0,10)),0,0);var a=Actions();var yes=Theme.Button(T("确认应用","Apply changes"),true);yes.Width=180;yes.DialogResult=DialogResult.OK;var no=Theme.Button(T("取消","Cancel"),false);no.Width=125;no.DialogResult=DialogResult.Cancel;a.Controls.Add(yes);a.Controls.Add(no);p.Controls.Add(a,0,1);dialog.CancelButton=no;return dialog.ShowDialog(this)==DialogResult.OK;}}
        private void Result(bool ok,string backup,string details){SetStatus(ok?T("所选操作已完成。","Selected operation completed."):T("操作未全部完成，请查看说明。","Operation not fully completed. Review details."));Info((ok?T("操作完成。","Operation completed."):T("未全部完成。","Not fully completed."))+"\r\n"+details+(String.IsNullOrEmpty(backup)?"":"\r\n\r\n"+T("备份：","Backup: ")+backup));}
        private void Open(string target){try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}catch(Exception ex){Info(T("无法打开：","Could not open: ")+ex.GetType().Name);}}
        private static string SizeText(long bytes){return bytes<1024?bytes+" B":bytes<1048576?(bytes/1024d).ToString("0.0")+" KB":(bytes/1048576d).ToString("0.00")+" MB";}
        internal void RenderPreview(string path){RenderPreview(path,0);}
        internal void RenderPreview(string path,int page){
            if(!preview)throw new InvalidOperationException();
            scan=new RepairScan{SettingsExists=true,SettingsValid=true};scan.OfficialSettingKeys.Add("ANTHROPIC_BASE_URL");scan.OfficialSettingKeys.Add("ANTHROPIC_MODEL");scan.ProxySettingKeys.Add("HTTPS_PROXY");ShowScan();pathLabel.Text=T("演示预览 · 配置路径与检查项为示例","Demo preview · example settings and path");
            fontEntries=new List<FontEntry>{new FontEntry{Name="Microsoft YaHei UI",Path="C:\\Windows\\Fonts\\msyh.ttc",IsUser=false,CanRemove=false},new FontEntry{Name="Example User Font",Path="…\\Microsoft\\Windows\\Fonts\\example.ttf",IsUser=true,CanRemove=true}};FilterFonts();
            tabs.SelectedIndex=Math.Min(Math.Max(0,page),tabs.TabCount-1);SetStatus(T("演示预览 · 没有执行任何修改","Demo preview · no changes applied"));
            ShowInTaskbar=false;Opacity=0;Show();Application.DoEvents();using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(0,0,Width,Height));b.Save(path,System.Drawing.Imaging.ImageFormat.Png);}Close();
        }
    }
}
