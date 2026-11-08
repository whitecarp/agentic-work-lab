using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WhaleCommon {
    internal sealed class UpdateRelease {
        public string Version, Tag, Url, Digest, AssetName;
        public long Size;
        public bool Preview;
    }

    // Compiled into each standalone EXE. Checking and downloading are user initiated.
    internal static class UpdateChecker {
        internal const string Repository = "whitecarp/I-Must-Use-Claude";
        internal const string ReleasesUrl = "https://github.com/" + Repository + "/releases";
        internal const long MaxAssetSize = 250L * 1024 * 1024;
        internal static string AssetName(string app) {
            switch(app) {
                case "translator": return "WhaleTranslator.exe";
                case "clock": return "HomeTimeClock.exe";
                case "helper": return "WhaleEnvironmentHelper.exe";
                case "pack": return "WhalePackHelper.exe";
                default: throw new ArgumentException("Unknown application.");
            }
        }
        public static void Show(IWin32Window owner, string app, string version, bool english, bool currentPreview = true) {
            using(var form = new UpdateForm(app, version, english, currentPreview)) {
                form.ShowInTaskbar=false;
                Icon appIcon=null;
                try {
                    try { var parent=owner as Form; appIcon=parent!=null&&parent.Icon!=null?(Icon)parent.Icon.Clone():Icon.ExtractAssociatedIcon(Application.ExecutablePath);form.Icon=appIcon; } catch { }
                    form.ShowDialog(owner);
                } finally { if(appIcon!=null)appIcon.Dispose(); }
            }
        }
        internal static bool IsNewer(UpdateRelease candidate,string current,bool currentPreview) {
            var available=new Version(candidate.Version);var installed=new Version(current);
            return available>installed || (available==installed && currentPreview && !candidate.Preview);
        }
        internal static string Value(IDictionary<string, object> obj, string key) {
            object result; return obj.TryGetValue(key, out result) && result != null ? Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture) : "";
        }
        internal static bool AllowedUrl(string text) {
            Uri uri;
            return Uri.TryCreate(text, UriKind.Absolute, out uri) && uri.Scheme == "https" && uri.Port == 443 && String.IsNullOrEmpty(uri.UserInfo)
                && (uri.Host == "api.github.com" || uri.Host == "github.com" || uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com");
        }
        internal static UpdateRelease SelectRelease(string json, string app) {
            string assetName = AssetName(app);
            var serializer = new JavaScriptSerializer { MaxJsonLength = 2 * 1024 * 1024 };
            var list = serializer.DeserializeObject(json) as object[];
            if(list == null) throw new InvalidDataException("Invalid release list.");
            UpdateRelease best = null; Version bestVersion = null;
            foreach(var item in list) {
                var release = item as IDictionary<string, object>;
                if(release == null || Value(release,"draft").Equals("True",StringComparison.OrdinalIgnoreCase)) continue;
                string body = Value(release,"body"), tag = Value(release,"tag_name");
                if(!Regex.IsMatch(tag, @"^v[0-9]+\.[0-9]+\.[0-9]+(?:-[a-zA-Z0-9.-]+)?$")) continue;
                var marker = Regex.Match(body, @"<!--\s*whale-app-versions:\s*(\{[^\r\n]*\})\s*-->");
                if(!marker.Success) continue;
                IDictionary<string, object> versions;
                try { versions = serializer.DeserializeObject(marker.Groups[1].Value) as IDictionary<string, object>; } catch { continue; }
                Version version;
                if(versions == null || !Version.TryParse(Value(versions,app), out version)) continue;
                bool preview = Value(release,"prerelease").Equals("True",StringComparison.OrdinalIgnoreCase);
                if(best != null && (version < bestVersion || (version == bestVersion && (!best.Preview || preview)))) continue;
                object assetsObject; var assets = release.TryGetValue("assets",out assetsObject) ? assetsObject as object[] : null;
                if(assets == null) continue;
                foreach(var assetObject in assets) {
                    var asset = assetObject as IDictionary<string, object>;
                    if(asset == null || Value(asset,"name") != assetName || Value(asset,"state") != "uploaded") continue;
                    string url = Value(asset,"browser_download_url"), digest = Value(asset,"digest"); long size;
                    string expected = "https://github.com/" + Repository + "/releases/download/" + tag + "/" + assetName;
                    if(url != expected || !Int64.TryParse(Value(asset,"size"),out size) || size < 1 || size > MaxAssetSize) continue;
                    // Do not turn a missing/malformed checksum into a verified download.
                    if(!Regex.IsMatch(digest,@"^sha256:[0-9a-fA-F]{64}$")) continue;
                    best = new UpdateRelease { Version=version.ToString(),Tag=tag,Url=url,Digest=digest.Substring(7).ToLowerInvariant(),Size=size,AssetName=assetName,Preview=preview };
                    bestVersion = version;
                    break;
                }
            }
            return best;
        }
        private static HttpWebResponse Open(string url, CancellationToken token) {
            for(int redirects=0; redirects<6; redirects++) {
                token.ThrowIfCancellationRequested();
                if(!AllowedUrl(url)) throw new InvalidDataException("Untrusted download location.");
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = "WhaleClaudeCompanions/0.3.0";
                request.Accept = "application/vnd.github+json";
                request.Timeout = 15000; request.ReadWriteTimeout = 15000; request.AllowAutoRedirect = false;
                HttpWebResponse response;
                using(token.Register(request.Abort)) { response = (HttpWebResponse)request.GetResponse(); }
                int code = (int)response.StatusCode;
                if(code >= 300 && code < 400) {
                    string location = response.Headers["Location"]; response.Dispose();
                    Uri next; if(String.IsNullOrEmpty(location) || !Uri.TryCreate(new Uri(url),location,out next)) throw new InvalidDataException("Invalid redirect.");
                    url = next.AbsoluteUri; continue;
                }
                if(code != 200) { response.Dispose(); throw new WebException("Unexpected response."); }
                return response;
            }
            throw new WebException("Too many redirects.");
        }
        internal static void EnableModernTls() {
            // Preserve OS TLS defaults on .NET 4.8; extend only legacy explicit policies.
            if(ServicePointManager.SecurityProtocol != SecurityProtocolType.SystemDefault)
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }
        internal static string ReadReleaseList(CancellationToken token) {
            EnableModernTls();
            using(var response = Open("https://api.github.com/repos/"+Repository+"/releases?per_page=100",token))
            using(token.Register(response.Close))
            using(var input = response.GetResponseStream())
            using(var memory = new MemoryStream()) {
                var buffer = new byte[8192]; int count;
                while((count=input.Read(buffer,0,buffer.Length))>0) {
                    token.ThrowIfCancellationRequested();
                    if(memory.Length + count > 2*1024*1024) throw new InvalidDataException("Release response too large.");
                    memory.Write(buffer,0,count);
                }
                return Encoding.UTF8.GetString(memory.ToArray());
            }
        }
        // Input stream injection keeps tests entirely offline and outside personal app data.
        internal static void SaveVerified(Stream input, UpdateRelease release, string destination, CancellationToken token, Action<int> progress) {
            if(release == null || release.Size < 1 || release.Size > MaxAssetSize || !Regex.IsMatch(release.Digest??"",@"^[0-9a-f]{64}$")) throw new InvalidDataException("Missing verified asset details.");
            string full = Path.GetFullPath(destination);
            if(!String.Equals(Path.GetExtension(full),".exe",StringComparison.OrdinalIgnoreCase) || File.Exists(full) || Directory.Exists(full)) throw new IOException("Choose a new EXE file name.");
            token.ThrowIfCancellationRequested();
            string part = Path.Combine(Path.GetDirectoryName(full), Path.GetRandomFileName()+".partial");
            bool created = false;
            using(token.Register(input.Dispose))try {
                using(var output = new FileStream(part,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                    created = true; var buffer=new byte[65536]; long total=0; int count, lastProgress=-1;
                    while((count=input.Read(buffer,0,buffer.Length))>0) {
                        token.ThrowIfCancellationRequested(); total+=count;
                        if(total>release.Size) throw new InvalidDataException("Download size mismatch.");
                        output.Write(buffer,0,count);
                        int percent=(int)(total*100/release.Size);
                        if(progress!=null && percent!=lastProgress){lastProgress=percent;progress(percent);}
                    }
                    if(total!=release.Size) throw new InvalidDataException("Incomplete download.");
                    output.Flush(true);
                }
                token.ThrowIfCancellationRequested();
                string actual;
                using(var sha=SHA256.Create())using(var file=File.OpenRead(part))actual=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
                if(actual!=release.Digest)throw new InvalidDataException("Checksum mismatch.");
                token.ThrowIfCancellationRequested();
                File.Move(part,full); created=false; // No overwrite, including a race at the destination.
            } catch(Exception) { token.ThrowIfCancellationRequested(); throw; }
            finally { if(created)try{File.Delete(part);}catch{} }
        }
        internal static void Download(UpdateRelease release,string path,CancellationToken token,Action<int> progress) {
            using(var response=Open(release.Url,token))using(token.Register(response.Close))using(var stream=response.GetResponseStream())SaveVerified(stream,release,path,token,progress);
        }
    }

    internal sealed class UpdateForm : Form {
        private readonly string app,version; private readonly bool english,currentPreview;
        private readonly Label status; private readonly Button check,download,cancel; private readonly ProgressBar progress;
        private CancellationTokenSource cancellation; private bool busy; private UpdateRelease release;
        private string T(string zh,string en){return english?en:zh;}
        internal UpdateForm(string appId,string current,bool en,bool preview = true) {
            app=appId; version=current; english=en;currentPreview=preview;
            Text=T("检查更新","Check for updates"); Font=new Font("Microsoft YaHei UI",10);
            BackColor=Color.FromArgb(250,248,241); ForeColor=Color.FromArgb(43,72,90);
            ClientSize=new Size(550,290); MinimumSize=new Size(520,320); StartPosition=FormStartPosition.CenterParent;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),RowCount=5,ColumnCount=1};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,35));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,25));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,45));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            layout.Controls.Add(new Label{Text=T("当前版本：","Current version: ")+version+(currentPreview?T("（预览版）"," (preview)"):""),AutoSize=true},0,0);
            status=new Label{Dock=DockStyle.Fill,Text=T("手动检查 GitHub 上的新版本（包含预览版）。","Check GitHub for a newer version, including previews.")};layout.Controls.Add(status,0,1);
            progress=new ProgressBar{Dock=DockStyle.Fill,Visible=false};layout.Controls.Add(progress,0,2);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
            check=new Button{Text=T("检查更新","Check updates"),AutoSize=true,Height=34};download=new Button{Text=T("下载新版 EXE","Download new EXE"),AutoSize=true,Enabled=false,Height=34};cancel=new Button{Text=T("取消","Cancel"),AutoSize=true,Enabled=false,Height=34};
            buttons.Controls.AddRange(new Control[]{check,download,cancel});layout.Controls.Add(buttons,0,3);
            layout.Controls.Add(new Label{Dock=DockStyle.Fill,Text=T("下载到新文件并校验 SHA-256。不会替换或运行文件；旧版本和设置保留。","Saves a new file and verifies SHA-256. No replacement or auto-launch; existing apps and settings stay intact.")},0,4);
            Controls.Add(layout);
            check.Click+=async delegate{await Check();};download.Click+=async delegate{await Download();};cancel.Click+=delegate{if(cancellation!=null)cancellation.Cancel();};
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy){e.Cancel=true;cancellation.Cancel();status.Text=T("正在取消，请稍候…","Cancelling, please wait…");}};
            Shown+=async delegate{await Check();};
        }
        private void SetBusy(bool value){busy=value;check.Enabled=!value;download.Enabled=!value&&release!=null;cancel.Enabled=value;}
        private string Error(Exception error) {
            var web=error as WebException;var response=web==null?null:web.Response as HttpWebResponse;
            if(response!=null&&((int)response.StatusCode==403||(int)response.StatusCode==429))return T("GitHub 暂时限制请求，请稍后再试。","GitHub temporarily limited requests. Try again later.");
            if(error is InvalidDataException)return T("下载信息、文件大小或校验值不匹配；未保存新版。","Release details, size or checksum did not match; no update was saved.");
            if(error is IOException||error is UnauthorizedAccessException)return T("无法保存文件。请选择有权限的新文件名，旧文件不会覆盖。","Could not save. Choose a new writable file name; existing files are never overwritten.");
            return T("连接失败或超时，请稍后重试。当前软件可以继续使用。","Connection failed or timed out. Try later; the current app still works.");
        }
        private async Task Check(){
            if(busy)return;release=null;cancellation=new CancellationTokenSource();SetBusy(true);status.Text=T("正在检查 GitHub…","Checking GitHub…");
            try {
                var candidate=await Task.Run(()=>UpdateChecker.SelectRelease(UpdateChecker.ReadReleaseList(cancellation.Token),app));
                if(cancellation.IsCancellationRequested)throw new OperationCanceledException();
                if(candidate==null)status.Text=T("暂未找到带完整校验信息的兼容发布，请稍后再试。","No compatible release with complete verification details was found. Try later.");
                else if(!UpdateChecker.IsNewer(candidate,version,currentPreview))status.Text=T("已是当前最新版本。","You have the latest available version.");
                else{release=candidate;status.Text=T("发现新版：","New version: ")+candidate.Version+(candidate.Preview?T("（预览版）"," (preview)"):"")+"\r\n"+candidate.AssetName+" · "+(candidate.Size/1048576.0).ToString("0.0")+" MB";}
            }catch(Exception ex){status.Text=cancellation.IsCancellationRequested?T("已取消。","Cancelled."):Error(ex);}
            finally{SetBusy(false);cancellation.Dispose();cancellation=null;}
        }
        private async Task Download(){
            if(busy||release==null)return;
            string path;
            using(var dialog=new SaveFileDialog{Filter="Windows application (*.exe)|*.exe",FileName=Path.GetFileNameWithoutExtension(release.AssetName)+"-"+release.Version+".exe",OverwritePrompt=true,AddExtension=true}){
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;path=dialog.FileName;
            }
            if(File.Exists(path)){status.Text=T("该文件已存在，请换一个文件名；旧文件会保留。","That file exists. Choose another name; the old file is kept.");return;}
            cancellation=new CancellationTokenSource();SetBusy(true);progress.Value=0;progress.Visible=true;status.Text=T("正在下载并校验…","Downloading and verifying…");
            var reporter=new Progress<int>(value=>{if(!IsDisposed)progress.Value=Math.Max(0,Math.Min(100,value));});
            try{await Task.Run(()=>UpdateChecker.Download(release,path,cancellation.Token,value=>((IProgress<int>)reporter).Report(value)));status.Text=T("已校验并保存：\r\n","Verified and saved:\r\n")+path;}
            catch(Exception ex){status.Text=cancellation.IsCancellationRequested?T("已取消下载；旧版本保留。","Download cancelled; the old version is kept."):Error(ex);}
            finally{progress.Visible=false;SetBusy(false);cancellation.Dispose();cancellation=null;}
        }
    }
}
