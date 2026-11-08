using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClaudeCompanion {
    internal sealed class LanguagePack {
        public string Id,From,To,FromName,ToName,Version;
        public bool Installed;
        public string Pair { get { return "pair:"+From+":"+To; } }
        public string Display { get { return Name(From,FromName)+" → "+Name(To,ToName); } }
        public override string ToString(){return Display;}
        internal static string Name(string code,string fallback){
            if(Ui.English){if(code=="zh")return "Chinese (Simplified)";if(code=="zt")return "Chinese (Traditional)";return String.IsNullOrEmpty(fallback)?code:fallback;}
            string[] codes={"en","zh","zt","ja","ko","fr","de","es","it","pt","ru","ar","hi","th","vi","id","ms","uk","nl","pl","tr","sv","fi","el","he","cs","da","ro","hu","fa","bn","ta","te","ur"};
            string[] names={"英文","简体中文","繁体中文","日文","韩文","法文","德文","西班牙文","意大利文","葡萄牙文","俄文","阿拉伯文","印地文","泰文","越南文","印尼文","马来文","乌克兰文","荷兰文","波兰文","土耳其文","瑞典文","芬兰文","希腊文","希伯来文","捷克文","丹麦文","罗马尼亚文","匈牙利文","波斯文","孟加拉文","泰米尔文","泰卢固文","乌尔都文"};
            int i=Array.IndexOf(codes,code);return i<0?(String.IsNullOrEmpty(fallback)?code:fallback):names[i];
        }
        public static LanguagePack Read(Dictionary<string,object> item){return new LanguagePack{Id=Json.Str(item,"id"),From=Json.Str(item,"from_code"),To=Json.Str(item,"to_code"),FromName=Json.Str(item,"from_name"),ToName=Json.Str(item,"to_name"),Version=Json.Str(item,"package_version")};}
        public static List<LanguagePack> ReadList(Dictionary<string,object> data,string key){
            var result=new List<LanguagePack>();object raw;
            if(data.TryGetValue(key,out raw)&&raw is System.Collections.IEnumerable)foreach(var row in (System.Collections.IEnumerable)raw){var item=row as Dictionary<string,object>;if(item!=null)result.Add(Read(item));}
            return result;
        }
        public static List<LanguagePack> InstalledPacks(){
            var result=new List<LanguagePack>();string root=Path.Combine(Preferences.DataDirectory,"packages");
            try{if(!Directory.Exists(root)||(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)return result;
                foreach(string folder in Directory.GetDirectories(root,"translate-*")){
                    try{if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)continue;
                        string model=Path.Combine(folder,"model");if(!Directory.Exists(model)||(File.GetAttributes(model)&FileAttributes.ReparsePoint)!=0)continue;
                        var files=new List<string>{"metadata.json",".install.json","sentencepiece.model","model/model.bin","model/config.json"};
                        if(File.Exists(Path.Combine(model,"shared_vocabulary.json")))files.Add("model/shared_vocabulary.json");else{files.Add("model/source_vocabulary.json");files.Add("model/target_vocabulary.json");}
                        bool complete=true;foreach(string file in files){var info=new FileInfo(Path.Combine(folder,file));if(!info.Exists||info.Length==0||(info.Attributes&FileAttributes.ReparsePoint)!=0){complete=false;break;}}
                        if(!complete)continue;
                        var item=Read(Json.Decode(File.ReadAllText(Path.Combine(folder,"metadata.json"),Encoding.UTF8)));
                        if(!System.Text.RegularExpressions.Regex.IsMatch(item.From+":"+item.To,"^[a-z]{2,3}:[a-z]{2,3}$"))continue;
                        item.Id="translate-"+item.From+"_"+item.To+"-"+item.Version.Replace('.','_');
                        var receipt=Json.Decode(File.ReadAllText(Path.Combine(folder,".install.json"),Encoding.UTF8));
                        if(Path.GetFileName(folder)!=item.Id||Json.Str(receipt,"id")!=item.Id||!Json.Bool(receipt,"complete"))continue;
                        item.Installed=true;result.Add(item);
                    }catch{}
                }
            }catch{}
            result.Sort(delegate(LanguagePack a,LanguagePack b){return String.Compare(a.Display,b.Display,StringComparison.CurrentCulture);});return result;
        }
    }

    internal sealed class OfflineRetryForm : Form {
        private readonly ComboBox pairs;
        private readonly Button confirm;
        internal LanguagePack SelectedPack { get { return pairs.SelectedItem as LanguagePack; } }
        internal static List<LanguagePack> ForTarget(IEnumerable<LanguagePack> installed,string direction) {
            var result=new List<LanguagePack>();
            if(direction==null||!direction.StartsWith("auto:",StringComparison.Ordinal))return result;
            string target=direction.Substring(5);if(target=="zh-CN")target="zh";else if(target=="zh-TW")target="zt";
            var seen=new HashSet<string>();
            foreach(var pack in installed)if(pack.Installed&&pack.To==target&&seen.Add(pack.Pair))result.Add(pack);
            return result;
        }
        internal OfflineRetryForm(List<LanguagePack> choices) {
            SuspendLayout(); Ui.ApplyIcon(this);
            Text=Ui.T("改用离线翻译","Use offline translation");Font=new Font("Microsoft YaHei UI",10F);
            AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(430,240);BackColor=Theme.Cream;ForeColor=Theme.Ink;
            StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=4};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            layout.Controls.Add(new Label{Dock=DockStyle.Fill,Text=Ui.T("这段文字可以留在本机翻译","Translate this selection on your device"),Font=new Font(Font.FontFamily,12F,FontStyle.Bold)},0,0);
            layout.Controls.Add(new Label{Dock=DockStyle.Fill,Text=choices.Count==0?Ui.T("尚未安装对应目标语言的包。\r\n点「安装语言包」，自行选择原文 → 目标语言。","No installed pack matches your target language.\r\nInstall a pack for the source → target direction."):Ui.T("请确认原文语言对应的翻译方向。\r\n目标语言不变，刚才的原文无需重新划选。","Confirm the source → target direction below.\r\nYour target and original selection are retained."),ForeColor=Theme.Muted},0,1);
            pairs=new ThemedComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Color.White,AccessibleName=Ui.T("离线翻译方向","Offline translation direction")};
            foreach(var pair in choices)pairs.Items.Add(pair);
            if(choices.Count>0){pairs.SelectedIndex=0;foreach(LanguagePack pair in pairs.Items)if(pair.Pair==Preferences.OfflinePair)pairs.SelectedItem=pair;}else{pairs.Items.Add(Ui.T("没有对应的已安装语言包","No matching installed language pack"));pairs.SelectedIndex=0;pairs.Enabled=false;}
            layout.Controls.Add(pairs,0,2);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};
            confirm=Theme.Button(Ui.T("翻译这段","Translate selection"),true);confirm.Width=175;confirm.Enabled=choices.Count>0;confirm.DialogResult=DialogResult.OK;
            var install=Theme.Button(Ui.T("安装语言包","Install packs"),choices.Count==0);install.Width=170;install.DialogResult=DialogResult.Retry;
            actions.Controls.Add(confirm);actions.Controls.Add(install);layout.Controls.Add(actions,0,3);Controls.Add(layout);AcceptButton=confirm;
            AutoScaleDimensions=new SizeF(96F,96F);ResumeLayout(true);
            Shown+=delegate{
                // Framework WinForms can scale the form before late-created child controls.
                // Apply absolute design metrics once against the actual device DPI.
                layout.Padding=new Padding(Theme.Scale(this,20));
                layout.RowStyles[0].Height=Theme.Scale(this,32);
                layout.RowStyles[2].Height=Theme.Scale(this,42);
                layout.RowStyles[3].Height=Theme.Scale(this,46);
                PackLayout.FitButton(confirm,this,175);PackLayout.FitButton(install,this,150);
            };
        }
        internal void RenderPreview(string path){TopMost=false;Opacity=0;Show();Application.DoEvents();using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(Point.Empty,Size));bitmap.Save(path);}Close();}
    }

    internal static class PackLayout {
        internal static void FitButton(Button button,Control host,int minimum) {
            button.Width=Math.Max(Theme.Scale(host,minimum),TextRenderer.MeasureText(button.Text,button.Font).Width+Theme.Scale(host,30));
            button.Height=Theme.Scale(host,38);
        }
    }

    internal static class PackService {
        internal static async Task<Dictionary<string,object>> Run(string command,string argument,Action<Dictionary<string,object>> progress,CancellationToken cancel){
            if(command!="--list"&&command!="--refresh"&&command!="--install"&&command!="--reinstall")throw new ArgumentException("command");
            if(argument!=null&&!System.Text.RegularExpressions.Regex.IsMatch(argument,"^[a-zA-Z0-9_.-]+$"))throw new ArgumentException("package id");
            string root=AppDomain.CurrentDomain.BaseDirectory;
            var config=Json.Decode(File.ReadAllText(Path.Combine(root,"backend","launcher.json"),Encoding.UTF8));
            var start=new ProcessStartInfo(Path.GetFullPath(Path.Combine(root,Json.Str(config,"python"))),"-B -u \""+Path.Combine(root,"backend","package_manager.py")+"\" "+command+(argument==null?"":" "+argument)){
                WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
            start.EnvironmentVariables["PYTHONIOENCODING"]="utf-8";start.EnvironmentVariables["PYTHONUTF8"]="1";
            using(var process=new Process{StartInfo=start}){
                process.ErrorDataReceived+=delegate{};process.Start();process.BeginErrorReadLine();
                using(cancel.Register(delegate{try{process.StandardInput.WriteLine("cancel");process.StandardInput.Flush();}catch{}})){
                    try{
                        var deadline=Task.Delay(command=="--list"?15000:1800000);
                        Dictionary<string,object> result=null;
                        while(true){
                            var read=process.StandardOutput.ReadLineAsync();
                            var cancelled=Task.Delay(-1,cancel);
                            var completed=await Task.WhenAny(read,deadline,cancelled);
                            if(completed==cancelled){await Task.Run(delegate{return process.WaitForExit(5000);});throw new OperationCanceledException();}
                            if(completed==deadline)throw new TimeoutException();
                            string line=await read;if(line==null)break;if(line.Length>2000000)throw new InvalidDataException();
                            var row=Json.Decode(line);
                            if(Json.Str(row,"event")=="result"){result=row;break;}
                            if(progress!=null)progress(row);
                        }
                        await Task.Run(delegate{return process.WaitForExit(2000);});cancel.ThrowIfCancellationRequested();
                        if(result==null)throw new InvalidDataException();
                        if(!Json.Bool(result,"ok"))throw new InvalidOperationException(Error(Json.Str(result,"error")));
                        return result;
                    }finally{try{if(!process.HasExited)process.Kill();}catch{}}
                }
            }
        }
        internal static string Error(string code){
            if(code=="package_busy")return Ui.T("这个语言包正在使用，请关闭中文提问窗口后重试。","This pack is in use. Close the Write in Chinese window and retry.");
            if(code=="cancelled")return Ui.T("下载已取消，已安装的语言包不受影响。","Download cancelled. Installed packs are unchanged.");
            if(code=="checksum_mismatch")return Ui.T("下载到的语言包与已知校验值不一致，已拒绝安装。请稍后重试或换个网络。","The downloaded pack did not match its known checksum and was rejected. Retry later or on another network.");
            if(code.Contains("network")||code.Contains("download")||code.Contains("http"))return Ui.T("语言包下载失败，请检查网络后重试。","Download failed. Check your connection and retry.");
            if(code.Contains("unsupported"))return Ui.T("这个语言包暂不兼容，请选择其他包。","This pack is not compatible yet. Choose another pack.");
            if(code.Contains("space"))return Ui.T("磁盘空间不足，请腾出空间后重试。","Not enough disk space. Free up space and retry.");
            return Ui.T("语言包未安装成功，已安装的包保持不变。","The pack could not be installed. Existing packs are unchanged.");
        }
    }

    internal sealed class LanguagePacksForm:Form {
        private readonly TextBox search=new TextBox();
        private readonly ListView list=new ListView();
        private readonly Label status=new Label();
        private readonly ProgressBar progress=new ProgressBar();
        private readonly Button install=Theme.Button(Ui.T("下载所选语言包","Download selected"),true);
        private readonly Button refresh=Theme.Button(Ui.T("更新目录","Refresh catalog"),false);
        private readonly Button cancel=Theme.Button(Ui.T("取消下载","Cancel download"),false);
        private List<LanguagePack> catalog=new List<LanguagePack>();
        private CancellationTokenSource cancellation;
        private bool busy,closing;
        private readonly bool preview;
        public LanguagePacksForm(bool isPreview=false){
            SuspendLayout();Ui.ApplyIcon(this);AutoScaleMode=AutoScaleMode.Dpi;
            preview=isPreview;Text=Ui.T("大肥鱼 · 离线语言包","Whale · Offline language packs");Size=new Size(740,580);MinimumSize=new Size(640,480);StartPosition=FormStartPosition.CenterParent;
            Font=new Font("Microsoft YaHei UI",10F);BackColor=Theme.Cream;ForeColor=Theme.Ink;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(20)};
            foreach(float h in new float[]{75,35})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));foreach(float h in new float[]{26,48,42})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
            var top=new Panel{Dock=DockStyle.Fill};top.Controls.Add(new WhaleBadge{Size=new Size(75,64),Location=Point.Empty});
            top.Controls.Add(new Label{AutoSize=true,Location=new Point(85,4),Font=new Font(Font.FontFamily,15,FontStyle.Bold),Text=Ui.T("有网时下载，没网也能读","Download now. Read anywhere.")});
            top.Controls.Add(new Label{AutoSize=true,Location=new Point(85,38),ForeColor=Theme.Muted,Font=new Font(Font.FontFamily,9),Text=Ui.T("按方向选装；来回需两个包。仅更新目录或下载时联网。","Install each direction. Only refresh and downloads use internet.")});root.Controls.Add(top,0,0);
            var searchRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0)};searchRow.RowStyles.Add(new RowStyle(SizeType.Percent,100));searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,75));searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            searchRow.Controls.Add(new Label{Text=Ui.T("搜索语言","Search"),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);search.Dock=DockStyle.Fill;search.AccessibleName=Ui.T("搜索语言或代码","Search language or code");search.TextChanged+=delegate{Populate();};searchRow.Controls.Add(search,1,0);root.Controls.Add(searchRow,0,1);
            list.Dock=DockStyle.Fill;list.View=View.Details;list.FullRowSelect=true;list.MultiSelect=false;list.HideSelection=false;list.BorderStyle=BorderStyle.None;list.Columns.Add(Ui.T("翻译方向","Language direction"),340);list.Columns.Add(Ui.T("版本","Version"),80);list.Columns.Add(Ui.T("状态","Status"),130);list.SelectedIndexChanged+=delegate{UpdateButtons();};root.Controls.Add(list,0,2);
            progress.Dock=DockStyle.Fill;progress.Height=15;progress.Margin=new Padding(0,7,0,3);root.Controls.Add(progress,0,3);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0)};install.Width=210;refresh.Width=155;cancel.Width=165;actions.Controls.AddRange(new Control[]{install,refresh,cancel});root.Controls.Add(actions,0,4);
            status.Dock=DockStyle.Fill;status.ForeColor=Theme.Muted;status.Font=new Font(Font.FontFamily,9);root.Controls.Add(status,0,5);Controls.Add(root);
            // Explicit download also repairs an incomplete previous installation atomically.
            install.Click+=async delegate{if(list.SelectedItems.Count>0){var item=(LanguagePack)list.SelectedItems[0].Tag;await Execute("--reinstall",item.Id);}};
            refresh.Click+=async delegate{await Execute("--refresh",null);};cancel.Click+=delegate{if(cancellation!=null)cancellation.Cancel();};
            Shown+=async delegate{if(!preview)await Execute("--list",null);};
            FormClosing+=delegate{closing=true;if(cancellation!=null)cancellation.Cancel();};UpdateButtons();
            AutoScaleDimensions=new SizeF(96F,96F);ResumeLayout(true);
            Shown+=delegate{
                root.Padding=new Padding(Theme.Scale(this,20));
                root.RowStyles[0].Height=Theme.Scale(this,75);root.RowStyles[1].Height=Theme.Scale(this,35);
                root.RowStyles[3].Height=Theme.Scale(this,26);root.RowStyles[4].Height=Theme.Scale(this,48);root.RowStyles[5].Height=Theme.Scale(this,42);
                searchRow.ColumnStyles[0].Width=Theme.Scale(this,85);
                top.Controls[0].Size=new Size(Theme.Scale(this,75),Theme.Scale(this,64));
                top.Controls[1].Location=new Point(Theme.Scale(this,85),Theme.Scale(this,4));
                top.Controls[2].Location=new Point(Theme.Scale(this,85),Theme.Scale(this,38));
                list.Columns[0].Width=Theme.Scale(this,340);list.Columns[1].Width=Theme.Scale(this,80);list.Columns[2].Width=Theme.Scale(this,130);
                PackLayout.FitButton(install,this,210);PackLayout.FitButton(refresh,this,155);PackLayout.FitButton(cancel,this,165);
            };
        }
        private void SetData(Dictionary<string,object> data){
            catalog=LanguagePack.ReadList(data,"catalog");var installed=LanguagePack.ReadList(data,"installed");var ids=new HashSet<string>();foreach(var p in installed)ids.Add(p.Id);
            foreach(var p in catalog)p.Installed=ids.Contains(p.Id);Populate();
        }
        private void Populate(){
            string query=search.Text.Trim();list.BeginUpdate();list.Items.Clear();
            foreach(var p in catalog){string hay=p.Display+" "+p.FromName+" "+p.ToName+" "+p.From+" "+p.To;
                if(query.Length>0&&hay.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)<0)continue;
                var row=new ListViewItem(p.Display){Tag=p};row.SubItems.Add(p.Version);row.SubItems.Add(p.Installed?Ui.T("已安装","Installed"):Ui.T("可下载","Available"));if(p.Installed)row.ForeColor=Theme.BlueDark;list.Items.Add(row);
            }list.EndUpdate();UpdateButtons();
        }
        private void UpdateButtons(){var item=list.SelectedItems.Count>0?list.SelectedItems[0].Tag as LanguagePack:null;install.Enabled=!busy&&item!=null;install.Text=item!=null&&item.Installed?Ui.T("重新安装所选包","Reinstall selected"):Ui.T("下载所选语言包","Download selected");refresh.Enabled=!busy;cancel.Enabled=busy;search.Enabled=!busy;list.Enabled=!busy;}
        private async Task Execute(string operation,string id){
            if(busy||closing)return;busy=true;UpdateButtons();cancellation=new CancellationTokenSource();progress.Style=ProgressBarStyle.Marquee;
            status.Text=operation=="--list"?Ui.T("正在读取本机语言包…","Reading local language packs…"):operation=="--refresh"?Ui.T("正在更新语言包目录…","Updating the language catalog…"):Ui.T("正在下载，完成后即可离线使用…","Downloading. This pack will work offline once installed…");
            try{
                var result=await PackService.Run(operation,id,delegate(Dictionary<string,object> row){if(closing)return;object done,total;
                    double bytes=row.TryGetValue("bytes",out done)?Convert.ToDouble(done):0;
                    double size=row.TryGetValue("total_bytes",out total)?Convert.ToDouble(total):0;
                    string phase=Json.Str(row,"phase");
                    if(size>0){progress.Style=ProgressBarStyle.Continuous;progress.Value=Math.Max(0,Math.Min(100,(int)(100*bytes/size)));}
                    status.Text=phase=="download"?Ui.T("已下载 ","Downloaded ")+(bytes/1048576).ToString("0.0")+" MB"+(size>0?" / "+(size/1048576).ToString("0.0")+" MB":""):Ui.T("正在检查并安装语言包…","Checking and installing the pack…");
                },cancellation.Token);
                if(operation!="--list")result=await PackService.Run("--list",null,null,cancellation.Token);
                if(closing)return;SetData(result);progress.Style=ProgressBarStyle.Continuous;progress.Value=operation=="--install"||operation=="--reinstall"?100:0;
                status.Text=operation=="--install"||operation=="--reinstall"?Ui.T("安装完成。回到主界面，选择「离线翻译」和对应方向。","Installed. Choose Offline and this language direction in the main window."):Ui.T("语言包独立保存，更新软件不需要重下。下载大小会在传输时显示。","Packs survive app updates. Download size appears during transfer.");
            }catch(OperationCanceledException){if(!closing)status.Text=PackService.Error("cancelled");}
            catch(Exception ex){if(!closing)status.Text=ex is InvalidOperationException?ex.Message:Ui.T("暂时无法读取或下载语言包，请重试。","Could not read or download packs. Please retry.");}
            finally{if(cancellation!=null)cancellation.Dispose();cancellation=null;busy=false;if(!closing){progress.Style=ProgressBarStyle.Continuous;UpdateButtons();}}
        }
        internal void RenderPreview(string path){
            catalog=new List<LanguagePack>{new LanguagePack{Id="fixture-en-zh",From="en",To="zh",FromName="English",ToName="Chinese",Version="1.9",Installed=true},new LanguagePack{From="zh",To="en",FromName="Chinese",ToName="English",Version="1.9",Installed=true},new LanguagePack{From="en",To="ja",FromName="English",ToName="Japanese",Version="1.1"},new LanguagePack{From="de",To="en",FromName="German",ToName="English",Version="1.3"}};
            Populate();status.Text=Ui.T("界面示例 · 安装状态仅作演示","UI sample · Example installation status");TopMost=false;ShowInTaskbar=false;Opacity=0;Show();Application.DoEvents();using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(path);}Close();
        }
    }
}
