using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]

namespace DafeiyuPortable {
    internal sealed class PayloadFile { public string Path; public long Length; public string Sha256; }
    internal sealed class PayloadManifest { public int Schema; public string Version; public long TotalBytes; public PayloadFile[] Files; }
    internal sealed class CheckReport {
        public bool Ok,Extracted; public string Version,PayloadSha256,ManifestSha256,CacheDirectory,Error;
        public string Distribution="base-with-on-demand-language-packs";
        public bool LanguagePacksIncluded=false;
        public int FileCount; public long UncompressedBytes,ElapsedMilliseconds;
    }
    internal static class PortableLauncher {
        private static readonly JavaScriptSerializer Json=new JavaScriptSerializer { MaxJsonLength=16*1024*1024 };
        private static readonly Encoding Utf8=new UTF8Encoding(false,true);
        [STAThread] private static int Main(string[] args) {
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling",false);
            AppContext.SetSwitch("Switch.System.IO.BlockLongPaths",false);
            string originalDirectory=Environment.CurrentDirectory;
            if(args.Length>0&&args[0]=="--portable-check") {
                if(args.Length!=2&&!(args.Length==4&&args[2]=="--cache-root"))return 2;
                var timer=Stopwatch.StartNew();CheckReport report;
                try { report=PrepareRuntime(args.Length==4?Path.GetFullPath(args[3]):DefaultCacheRoot(),null); }
                catch(Exception error) { report=new CheckReport { Ok=false,Version=PortableBuildInfo.Version,PayloadSha256=PortableBuildInfo.PayloadSha256,Error=error.Message }; }
                report.ElapsedMilliseconds=timer.ElapsedMilliseconds;
                try { WriteReport(Path.GetFullPath(args[1]),report); }catch { return 3; }
                return report.Ok?0:1;
            }
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            int exitCode=0;
            using(var progress=new PrepareForm()) {
                progress.Shown+=async delegate {
                    try {
                        var report=await Task.Run(delegate { return PrepareRuntime(DefaultCacheRoot(),progress.SetProgress); });
                        LaunchApplication(report.CacheDirectory,args,originalDirectory);
                    }catch(Exception error) {
                        exitCode=1;
                        MessageBox.Show(progress,progress.StartFailure(error.Message),progress.Text,MessageBoxButtons.OK,MessageBoxIcon.Error);
                    }finally { progress.Finish(); }
                };
                Application.Run(progress);
            }
            return exitCode;
        }
        private static string DefaultCacheRoot() {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DafeiyuTranslator","runtime");
        }
        internal static CheckReport PrepareRuntime(string cacheRoot,Action<string> progress) {
            if(!Environment.Is64BitOperatingSystem)throw new InvalidOperationException("需要 64 位 Windows 10/11。 / Windows 10/11 x64 is required.");
            cacheRoot=Path.GetFullPath(cacheRoot).TrimEnd(Path.DirectorySeparatorChar);
            AssertNoReparseAncestors(cacheRoot);Directory.CreateDirectory(cacheRoot);AssertNoReparseAncestors(cacheRoot);
            string manifestText;using(var resource=OpenResource("payload.manifest.json"))using(var reader=new StreamReader(resource,Utf8))manifestText=reader.ReadToEnd();
            if(HashBytes(Utf8.GetBytes(manifestText))!=PortableBuildInfo.ManifestSha256)throw new InvalidDataException("Embedded file manifest is damaged.");
            var manifest=Json.Deserialize<PayloadManifest>(manifestText);ValidateManifest(manifest);
            string cache=Path.Combine(cacheRoot,PortableBuildInfo.PayloadSha256);
            string sid;using(var identity=WindowsIdentity.GetCurrent())sid=identity.User.Value;
            string mutexName="Global\\DafeiyuPortable_"+HashBytes(Utf8.GetBytes(sid)).Substring(0,16)+"_"+PortableBuildInfo.PayloadSha256;
            bool acquired=false,extracted=false;
            using(var mutex=new Mutex(false,mutexName)) {
                try {
                    Notify(progress,"正在检查运行文件… / Checking application files…");
                    try { acquired=mutex.WaitOne(TimeSpan.FromMinutes(4)); }catch(AbandonedMutexException) { acquired=true; }
                    if(!acquired)throw new IOException("另一个大肥鱼正在准备文件，请稍后重试。 / Another copy is preparing files; try again shortly.");
                    if(!ValidateRuntime(cache,manifest)) {
                        Notify(progress,"首次启动正在准备应用文件… / Preparing application files for first launch…");
                        string temporary=Path.Combine(cacheRoot,".tmp-"+PortableBuildInfo.PayloadSha256.Substring(0,12)+"-"+Guid.NewGuid().ToString("N").Substring(0,16));
                        Directory.CreateDirectory(temporary);
                        try {
                            ExtractPayload(temporary,manifest,progress);
                            File.WriteAllText(Path.Combine(temporary,".portable-ready"),PortableBuildInfo.PayloadSha256+"\n"+PortableBuildInfo.ManifestSha256,Utf8);
                            if(!ValidateRuntime(temporary,manifest))throw new InvalidDataException("Extracted application files did not pass verification.");
                            if(Directory.Exists(cache)) {
                                AssertNoReparseAncestors(cache);
                                // Preserve an incomplete or altered cache instead of deleting user data.
                                string previous=Path.Combine(cacheRoot,PortableBuildInfo.PayloadSha256+".previous-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N"));
                                Directory.Move(cache,previous);
                            }
                            Directory.Move(temporary,cache);extracted=true;
                        }finally { if(Directory.Exists(temporary))TryDeleteOwnedTemporary(cacheRoot,temporary); }
                    }
                }finally { if(acquired)mutex.ReleaseMutex(); }
            }
            return new CheckReport { Ok=true,Extracted=extracted,Version=PortableBuildInfo.Version,PayloadSha256=PortableBuildInfo.PayloadSha256,
                ManifestSha256=PortableBuildInfo.ManifestSha256,CacheDirectory=cache,FileCount=manifest.Files.Length,UncompressedBytes=manifest.TotalBytes };
        }
        private static void Notify(Action<string> progress,string message) { if(progress!=null)progress(message); }
        private static Stream OpenResource(string name) {
            Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if(stream==null)throw new InvalidDataException("Missing embedded resource: "+name);return stream;
        }
        internal static void ValidateManifest(PayloadManifest manifest) {
            if(manifest==null||manifest.Schema!=1||manifest.Version!=PortableBuildInfo.Version||manifest.Files==null||manifest.Files.Length==0||manifest.Files.Length>20000)
                throw new InvalidDataException("Invalid payload manifest.");
            long total=0;var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var file in manifest.Files) {
                if(file==null||!IsSafeRelativePath(file.Path)||!IsAllowedPayloadPath(file.Path)||file.Length<0||file.Length>1024L*1024*1024||!IsHash(file.Sha256)||!paths.Add(file.Path))
                    throw new InvalidDataException("Invalid payload file entry.");
                total=checked(total+file.Length);
            }
            if(total!=manifest.TotalBytes||total>2L*1024*1024*1024||!paths.Contains("ClaudeChinese.exe")||!paths.Contains("runtime/python/python.exe")||!paths.Contains("backend/offline_worker.py")||!paths.Contains("backend/glossary.py")||!paths.Contains("backend/package_manager.py")||!paths.Contains("backend/package_store.py")||!paths.Contains("backend/argos_catalog.json"))
                throw new InvalidDataException("Payload is incomplete or too large.");
        }
        internal static bool IsSafeRelativePath(string path) {
            if(String.IsNullOrEmpty(path)||path.Length>240||path.StartsWith("/",StringComparison.Ordinal)||path.IndexOf('\\')>=0||path.IndexOf(':')>=0)return false;
            foreach(char character in path)if(Char.IsControl(character)||character=='*'||character=='?'||character=='"'||character=='<'||character=='>'||character=='|')return false;
            foreach(string part in path.Split('/')) {
                if(part.Length==0||part=="."||part==".."||part.EndsWith(".",StringComparison.Ordinal)||part.EndsWith(" ",StringComparison.Ordinal))return false;
                string stem=part.Split('.')[0].ToUpperInvariant();
                if(stem=="CON"||stem=="PRN"||stem=="AUX"||stem=="NUL"||(stem.Length==4&&(stem.StartsWith("COM",StringComparison.Ordinal)||stem.StartsWith("LPT",StringComparison.Ordinal))&&stem[3]>='0'&&stem[3]<='9'))return false;
            }
            return true;
        }
        private static bool IsAllowedPayloadPath(string path) {
            if(path.EndsWith(".argosmodel",StringComparison.OrdinalIgnoreCase)||path.EndsWith("/model.bin",StringComparison.OrdinalIgnoreCase)||path.EndsWith("/sentencepiece.model",StringComparison.OrdinalIgnoreCase))return false;
            return path=="ClaudeChinese.exe"||path=="ClaudeReader.exe"||path=="SelectionReader.exe"||path=="ClaudeSender.exe"||
                path=="backend/google_worker.py"||path=="backend/offline_worker.py"||path=="backend/glossary.py"||path=="backend/launcher.json"||path=="backend/MODEL-NOTICES.md"||
                path=="backend/package_manager.py"||path=="backend/package_store.py"||path=="backend/argos_catalog.json"||
                path.StartsWith("runtime/python/",StringComparison.Ordinal)||path.StartsWith("runtime/venv/Lib/site-packages/",StringComparison.Ordinal)||
                path.StartsWith("licenses/",StringComparison.Ordinal);
        }
        private static string ResolveInside(string root,string relative) {
            if(!IsSafeRelativePath(relative))throw new InvalidDataException("Unsafe archive path.");
            string full=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
            if(!full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Archive path escaped its destination.");
            return full;
        }
        private static void ExtractPayload(string directory,PayloadManifest manifest,Action<string> progress) {
            var expected=new Dictionary<string,PayloadFile>(StringComparer.OrdinalIgnoreCase);
            foreach(var file in manifest.Files)expected.Add(file.Path,file);
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int count=0;
            using(var resource=OpenResource("payload.lzms"))
            using(var decoded=DecodeArchive(resource,PortableBuildInfo.PayloadSha256,PortableBuildInfo.ArchiveSha256,PortableBuildInfo.ArchiveBytes))
            using(var archive=new ZipArchive(decoded,ZipArchiveMode.Read,false)) {
                foreach(var entry in archive.Entries) {
                    PayloadFile file;
                    if(!IsSafeRelativePath(entry.FullName)||!expected.TryGetValue(entry.FullName,out file)||!String.Equals(entry.FullName,file.Path,StringComparison.Ordinal)||!seen.Add(entry.FullName)||entry.Length!=file.Length||
                        ((entry.ExternalAttributes>>16)&0xf000)==0xa000||(entry.ExternalAttributes&(int)FileAttributes.ReparsePoint)!=0)
                        throw new InvalidDataException("Unexpected or unsafe archive entry.");
                    string destination=ResolveInside(directory,file.Path);Directory.CreateDirectory(Path.GetDirectoryName(destination));AssertNoReparseAncestors(destination);
                    using(var input=entry.Open())using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                        byte[] buffer=new byte[128*1024];long copied=0;int read;
                        while((read=input.Read(buffer,0,buffer.Length))>0) { copied=checked(copied+read);if(copied>file.Length)throw new InvalidDataException("Archive length exceeded manifest.");output.Write(buffer,0,read); }
                        if(copied!=file.Length)throw new InvalidDataException("Archive entry was truncated.");
                    }
                    if(HashFile(destination)!=file.Sha256)throw new InvalidDataException("Application file checksum mismatch.");
                    if(++count%50==0)Notify(progress,"正在准备文件 "+count+" / "+manifest.Files.Length+"… / Preparing files…");
                }
            }
            if(seen.Count!=expected.Count)throw new InvalidDataException("Application archive is incomplete.");
        }
        internal static MemoryStream DecodeArchive(Stream resource,string payloadHash,string archiveHash,long archiveBytes) {
            if(resource==null||!resource.CanSeek||!IsHash(payloadHash)||!IsHash(archiveHash)||archiveBytes<=0||archiveBytes>NativeCompression.MaximumArchiveBytes)
                throw new InvalidDataException("Invalid embedded archive metadata.");
            long remaining=resource.Length-resource.Position;
            if(remaining<=0||remaining>NativeCompression.MaximumPayloadBytes)throw new InvalidDataException("Invalid embedded payload size.");
            byte[] compressed=new byte[checked((int)remaining)];int offset=0,read;
            while(offset<compressed.Length&&(read=resource.Read(compressed,offset,compressed.Length-offset))>0)offset+=read;
            if(offset!=compressed.Length||HashBytes(compressed)!=payloadHash)throw new InvalidDataException("Embedded application payload is damaged.");
            byte[] decoded=NativeCompression.Decompress(compressed,archiveBytes);
            if(HashBytes(decoded)!=archiveHash)throw new InvalidDataException("Decompressed application archive is damaged.");
            return new MemoryStream(decoded,false);
        }
        private static bool ValidateRuntime(string directory,PayloadManifest manifest) {
            if(!Directory.Exists(directory))return false;
            AssertNoReparseAncestors(directory);
            string ready=Path.Combine(directory,".portable-ready");
            AssertNoReparseAncestors(ready);
            if(!File.Exists(ready)||File.ReadAllText(ready,Utf8)!=PortableBuildInfo.PayloadSha256+"\n"+PortableBuildInfo.ManifestSha256)return false;
            foreach(var file in manifest.Files) {
                string path=ResolveInside(directory,file.Path);AssertNoReparseAncestors(path);
                var info=new FileInfo(path);
                if(!info.Exists||info.Length!=file.Length||HashFile(path)!=file.Sha256)return false;
            }
            return true;
        }
        private static void AssertNoReparseAncestors(string path) {
            string cursor=Path.GetFullPath(path);
            while(!String.IsNullOrEmpty(cursor)) {
                if((File.Exists(cursor)||Directory.Exists(cursor))&&(File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0)throw new IOException("运行目录包含链接，无法安全准备文件。 / The runtime path contains a filesystem link.");
                cursor=Path.GetDirectoryName(cursor);
            }
        }
        private static void TryDeleteOwnedTemporary(string root,string temporary) {
            try {
                string full=Path.GetFullPath(temporary);
                if(!String.Equals(Path.GetDirectoryName(full),Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith(".tmp-"+PortableBuildInfo.PayloadSha256.Substring(0,12)+"-",StringComparison.Ordinal))return;
                DeletePlainTree(full);
            }catch { /* A failed private staging directory can safely remain for later inspection. */ }
        }
        private static void DeletePlainTree(string directory) {
            AssertNoReparseAncestors(directory);
            foreach(string child in Directory.GetDirectories(directory))DeletePlainTree(child);
            foreach(string file in Directory.GetFiles(directory)) { AssertNoReparseAncestors(file);File.Delete(file); }
            Directory.Delete(directory,false);
        }
        private static bool IsHash(string value) { if(value==null||value.Length!=64)return false;foreach(char c in value)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')))return false;return true; }
        private static string HashFile(string path) { using(var stream=File.OpenRead(path))return HashStream(stream); }
        private static string HashBytes(byte[] bytes) { using(var stream=new MemoryStream(bytes,false))return HashStream(stream); }
        private static string HashStream(Stream stream) { using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant(); }
        private static void WriteReport(string path,CheckReport report) {
            string parent=Path.GetDirectoryName(path);if(!String.IsNullOrEmpty(parent))Directory.CreateDirectory(parent);
            File.WriteAllText(path,Json.Serialize(report),Utf8);
        }
        private static void LaunchApplication(string cache,string[] args,string workingDirectory) {
            var quoted=new List<string>();foreach(string arg in args)quoted.Add(QuoteArgument(arg));
            var info=new ProcessStartInfo(Path.Combine(cache,"ClaudeChinese.exe"),String.Join(" ",quoted.ToArray())) { UseShellExecute=false,WorkingDirectory=workingDirectory };
            info.EnvironmentVariables["DAFEIYU_PORTABLE_EXE"]=Assembly.GetExecutingAssembly().Location;
            info.EnvironmentVariables["DAFEIYU_PORTABLE_CACHE"]=cache;
            using(var process=Process.Start(info)) { if(process==null)throw new IOException("Application did not start."); }
        }
        internal static string QuoteArgument(string argument) {
            var result=new StringBuilder("\"");int slashes=0;
            foreach(char character in argument??"") {
                if(character=='\\') { ++slashes;continue; }
                if(character=='"') { result.Append('\\',slashes*2+1);result.Append('"');slashes=0;continue; }
                result.Append('\\',slashes);slashes=0;result.Append(character);
            }
            result.Append('\\',slashes*2);result.Append('"');return result.ToString();
        }
    }
    internal sealed class PrepareForm : Form {
        private readonly Label message;
        private readonly bool english;
        private bool finished;
        internal PrepareForm() : this(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName!="zh") { }
        internal PrepareForm(bool english) {
            SuspendLayout();
            this.english=english;
            AutoScaleDimensions=new SizeF(96F,96F);AutoScaleMode=AutoScaleMode.Dpi;
            Text=english?"Whale Translator":"大肥鱼翻译";ClientSize=new Size(490,194);StartPosition=FormStartPosition.CenterScreen;
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ControlBox=true;BackColor=Color.FromArgb(250,248,242);
            Font=new Font("Microsoft YaHei UI",10F);ClaudeCompanion.WindowIcon.Apply(this);
            Controls.Add(new ClaudeCompanion.WhaleBadge { Location=new Point(22,18),Size=new Size(78,60) });
            Controls.Add(new Label { Text=english?"Your whale is getting ready…":"大肥鱼正在准备出发…",Location=new Point(110,22),Size=new Size(358,34),Font=new Font(Font.FontFamily,15F,FontStyle.Bold),ForeColor=Color.FromArgb(76,151,196) });
            message=new Label { Text=english?"Checking application files…":"正在检查运行文件…",Location=new Point(111,61),Size=new Size(356,42),ForeColor=Color.FromArgb(56,78,95) };Controls.Add(message);
            Controls.Add(new Label { Text=english?"The first launch takes a moment. Next time will be quicker.":"第一次打开需要准备一下，之后就快了",Location=new Point(25,119),Size=new Size(440,25),Font=new Font(Font.FontFamily,9F),ForeColor=Color.FromArgb(122,146,160) });
            Controls.Add(new PrepareProgress { Location=new Point(25,158),Size=new Size(440,14),BackColor=BackColor });
            AutoScaleDimensions=new SizeF(96F,96F);ResumeLayout(true);
        }
        internal static string LocalizeProgress(string text,bool english) {
            // The preparation callbacks and extraction code keep their original messages.
            // Only presentation chooses one language, including counts in the extraction phase.
            if(text==null)return "";
            const string prefix="正在准备文件 ";
            if(text.StartsWith(prefix,StringComparison.Ordinal)) {
                int end=text.IndexOf('…');
                string counts=end<0?text.Substring(prefix.Length):text.Substring(prefix.Length,end-prefix.Length);
                return (english?"Preparing files ":prefix)+counts+"…";
            }
            int split=text.LastIndexOf(" / ",StringComparison.Ordinal);
            return split<0?text:english?text.Substring(split+3):text.Substring(0,split);
        }
        internal string StartFailure(string error) {
            return english?"Whale Translator could not start.\r\n\r\n"+LocalizeProgress(error,true)+"\r\n\r\nCheck free space and close this version before trying again.":
                "大肥鱼暂时无法启动。\r\n\r\n"+LocalizeProgress(error,false)+"\r\n\r\n请检查磁盘空间，并关闭正在使用此版本的大肥鱼后重试。";
        }
        internal void SetProgress(string text) { if(IsDisposed||!IsHandleCreated)return;try { BeginInvoke((Action)delegate { if(!IsDisposed)message.Text=LocalizeProgress(text,english); }); }catch(InvalidOperationException){} }
        internal void Finish() { finished=true;Close(); }
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);EnableMenuItem(GetSystemMenu(Handle,false),0xF060,0x0001);
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern IntPtr GetSystemMenu(IntPtr handle,bool reset);
        [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern uint EnableMenuItem(IntPtr menu,uint item,uint flags);
        protected override void OnFormClosing(FormClosingEventArgs e) { if(!finished&&e.CloseReason==CloseReason.UserClosing)e.Cancel=true;base.OnFormClosing(e); }
    }
    internal sealed class PrepareProgress : Control {
        private readonly System.Windows.Forms.Timer animation=new System.Windows.Forms.Timer { Interval=30 };
        private float phase;
        internal PrepareProgress() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            AccessibleRole=AccessibleRole.ProgressBar;
            animation.Tick+=delegate { phase=(phase+0.012f)%1f;Invalidate(); };
        }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e);animation.Enabled=Visible; }
        private static GraphicsPath Pill(RectangleF box) {
            var path=new GraphicsPath();float diameter=Math.Min(box.Width,box.Height);
            path.AddArc(box.X,box.Y,diameter,diameter,90,180);path.AddArc(box.Right-diameter,box.Y,diameter,diameter,270,180);path.CloseFigure();return path;
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            if(Width<4||Height<4)return;
            var box=new RectangleF(0.5f,0.5f,Width-1f,Height-1f);
            using(var track=Pill(box))using(var fill=new SolidBrush(Color.FromArgb(226,242,250))) {
                g.FillPath(fill,track);var state=g.Save();g.SetClip(track);
                float span=Width*0.32f,left=(Width+span)*phase-span;
                using(var bar=Pill(new RectangleF(left,box.Y,span,box.Height)))
                using(var blue=new SolidBrush(Color.FromArgb(125,193,225)))g.FillPath(blue,bar);
                g.Restore(state);
            }
        }
        protected override void Dispose(bool disposing) { if(disposing)animation.Dispose();base.Dispose(disposing); }
    }
}
