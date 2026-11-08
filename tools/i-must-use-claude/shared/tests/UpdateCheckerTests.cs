using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using WhaleCommon;

internal static class UpdateCheckerTests {
    static int checks;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
    static void Reject(Action action,string label){try{action();}catch(InvalidDataException){checks++;return;}catch(IOException){checks++;return;}catch(OperationCanceledException){checks++;return;}throw new Exception("Expected rejection: "+label);}
    static string Sha(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
    static object Release(string version,string app="translator",bool draft=false,bool preview=true,string digest=null,string url=null,string state="uploaded"){
        string name=UpdateChecker.AssetName(app),tag="v"+version+"-preview";
        return new {draft=draft,prerelease=preview,tag_name=tag,body="<!-- whale-app-versions: {\""+app+"\":\""+version+"\"} -->",assets=new[]{new{name=name,state=state,size=4,browser_download_url=url??("https://github.com/whitecarp/I-Must-Use-Claude/releases/download/"+tag+"/"+name),digest=digest??("sha256:"+new string('a',64))}}};
    }
    static UpdateRelease Parse(params object[] releases){return UpdateChecker.SelectRelease(new JavaScriptSerializer().Serialize(releases),"translator");}
    [STAThread] static int Main(){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Run();Console.WriteLine("PASS "+checks+" updater fixture checks; no network or live app settings; no windows shown.");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
    static void Run(){
        Check(Parse(Release("0.9.0"),Release("0.10.0")).Version=="0.10.0","numeric version order");
        Check(Parse(Release("0.11.0",draft:true),Release("0.9.0")).Version=="0.9.0","skip drafts");
        Check(Parse(Release("0.9.0",preview:true),Release("0.9.0",preview:false)).Preview==false,"stable preferred at same app version");
        var stable=Parse(Release("0.9.0",preview:false));var preview=Parse(Release("0.9.0",preview:true));
        Check(UpdateChecker.IsNewer(stable,"0.9.0",true),"same-version preview upgrades to stable");
        Check(!UpdateChecker.IsNewer(stable,"0.9.0",false),"same-version stable is not an update");
        Check(!UpdateChecker.IsNewer(preview,"0.9.0",false),"same-version stable never downgrades to preview");
        Check(!UpdateChecker.IsNewer(preview,"0.9.0",true),"same preview version is not an update");
        Check(!UpdateChecker.IsNewer(stable,"0.10.0",true),"older stable never downgrades a newer preview");
        Check(UpdateChecker.IsNewer(Parse(Release("0.10.0",preview:true)),"0.9.0",false),"newer preview remains available as advertised");
        Check(!UpdateChecker.IsNewer(Parse(Release("0.9.9",preview:false)),"0.10.0",false),"update gate compares numeric components");
        TlsPolicy();
        Check(Parse(Release("0.9.0",app:"clock"))==null,"correct application asset only");
        Check(UpdateChecker.AssetName("pack")=="WhalePackHelper.exe","pack helper asset name");
        Check(UpdateChecker.SelectRelease(new JavaScriptSerializer().Serialize(new object[]{Release("0.1.1",app:"pack")}),"pack").Version=="0.1.1","pack helper releases select its own asset");
        Check(UpdateChecker.SelectRelease(new JavaScriptSerializer().Serialize(new object[]{Release("0.1.1",app:"pack")}),"translator")==null,"pack helper asset is never offered to another app");
        Check(Parse(Release("0.9.0",digest:""))==null,"missing checksum rejected");
        Check(Parse(Release("0.9.0",digest:"md5:abcd"))==null,"wrong checksum algorithm rejected");
        Check(Parse(Release("0.9.0",state:"new"))==null,"unfinished upload rejected");
        Check(Parse(Release("0.9.0",url:"https://example.com/WhaleTranslator.exe"))==null,"foreign repo rejected");
        Check(Parse(Release("0.9.0",url:"http://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.9.0-preview/WhaleTranslator.exe"))==null,"HTTP rejected");
        Check(Parse(new {tag_name="v0.9.0",body="No machine-readable manifest",assets=new object[0]})==null,"legacy info does not invent app versions");
        Check(UpdateChecker.AllowedUrl("https://release-assets.githubusercontent.com/a?x=1"),"allow GitHub asset CDN");
        Check(!UpdateChecker.AllowedUrl("https://github.com.attacker.example/a"),"suffix attack rejected");
        Check(!UpdateChecker.AllowedUrl("https://user:password@github.com/a"),"credentials in URL rejected");
        Check(!UpdateChecker.AllowedUrl("https://github.com:8080/a"),"non HTTPS port rejected");
        Check(!UpdateChecker.AllowedUrl("file:///c:/data"),"file scheme rejected");
        string root=Path.Combine(Path.GetTempPath(),"whale-updater-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        byte[] bytes=Encoding.UTF8.GetBytes("fixture exe");
        var release=new UpdateRelease{Size=bytes.Length,Digest=Sha(bytes)};
        string dest=Path.Combine(root,"verified.exe");int progress=0;
        using(var stream=new MemoryStream(bytes))UpdateChecker.SaveVerified(stream,release,dest,CancellationToken.None,p=>progress=p);
        Check(File.ReadAllBytes(dest).SequenceEqual(bytes)&&progress==100,"verified bytes saved");
        Reject(()=>{using(var s=new MemoryStream(bytes))UpdateChecker.SaveVerified(s,release,dest,CancellationToken.None,null);},"do not overwrite");
        Check(File.ReadAllBytes(dest).SequenceEqual(bytes),"existing file unchanged");
        string shortFile=Path.Combine(root,"short.exe");
        Reject(()=>{using(var s=new MemoryStream(new byte[2]))UpdateChecker.SaveVerified(s,release,shortFile,CancellationToken.None,null);},"incomplete");
        Check(!File.Exists(shortFile),"incomplete never finalizes");
        string badFile=Path.Combine(root,"bad.exe");
        Reject(()=>{using(var s=new MemoryStream(new byte[bytes.Length]))UpdateChecker.SaveVerified(s,release,badFile,CancellationToken.None,null);},"checksum");
        Check(!File.Exists(badFile),"bad digest never finalizes");
        string largeFile=Path.Combine(root,"large.exe");
        Reject(()=>{using(var s=new MemoryStream(new byte[bytes.Length+1]))UpdateChecker.SaveVerified(s,release,largeFile,CancellationToken.None,null);},"too long");
        string cancelled=Path.Combine(root,"cancelled.exe");var cancel=new CancellationTokenSource();cancel.Cancel();
        Reject(()=>{using(var s=new MemoryStream(bytes))UpdateChecker.SaveVerified(s,release,cancelled,cancel.Token,null);},"cancel");
        Check(!File.Exists(cancelled),"cancelled never finalizes");
        string race=Path.Combine(root,"race.exe");
        Reject(()=>{using(var s=new MemoryStream(bytes))UpdateChecker.SaveVerified(s,release,race,CancellationToken.None,p=>File.WriteAllText(race,"existing"));},"destination appeared during download");
        Check(File.ReadAllText(race)=="existing","racing file unchanged");
        Check(Directory.GetFiles(root,"*.partial").Length==0,"owned partial files removed");
        StalledBodyCancellation(root,release);
        LongDestination(root,bytes,release);
        RenderForms();
        Console.WriteLine("Fixture evidence: "+root);
    }
    static void TlsPolicy(){
        // ServicePointManager is process-local. No request is created, and the old policy is restored.
        var original=ServicePointManager.SecurityProtocol;
        try{
            ServicePointManager.SecurityProtocol=SecurityProtocolType.SystemDefault;UpdateChecker.EnableModernTls();
            Check(ServicePointManager.SecurityProtocol==SecurityProtocolType.SystemDefault,"modern OS TLS defaults preserved");
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;UpdateChecker.EnableModernTls();
            Check(ServicePointManager.SecurityProtocol==SecurityProtocolType.Tls12,"explicit modern TLS policy retained");
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls;UpdateChecker.EnableModernTls();
            Check(ServicePointManager.SecurityProtocol==(SecurityProtocolType.Tls|SecurityProtocolType.Tls12),"legacy process policy gains TLS 1.2 without clearing existing flags");
        }finally{ServicePointManager.SecurityProtocol=original;}
        Check(ServicePointManager.SecurityProtocol==original,"fixture restores original process TLS policy");
    }
    sealed class StalledStream : Stream {
        internal readonly ManualResetEventSlim Blocked=new ManualResetEventSlim(false);
        readonly ManualResetEventSlim released=new ManualResetEventSlim(false);
        internal bool DisposedByCancellation;
        int reads;
        public override int Read(byte[] buffer,int offset,int count){
            if(Interlocked.Increment(ref reads)==1){buffer[offset]=(byte)'x';return 1;}
            Blocked.Set();if(!released.Wait(10000))throw new IOException("Synthetic stalled-body safety timeout.");
            throw new ObjectDisposedException("Synthetic stalled body");
        }
        protected override void Dispose(bool disposing){DisposedByCancellation=true;released.Set();base.Dispose(disposing);}
        public override bool CanRead{get{return true;}}public override bool CanSeek{get{return false;}}public override bool CanWrite{get{return false;}}
        public override long Length{get{throw new NotSupportedException();}}public override long Position{get{throw new NotSupportedException();}set{throw new NotSupportedException();}}
        public override void Flush(){throw new NotSupportedException();}public override long Seek(long offset,SeekOrigin origin){throw new NotSupportedException();}
        public override void SetLength(long value){throw new NotSupportedException();}public override void Write(byte[] buffer,int offset,int count){throw new NotSupportedException();}
    }
    static void StalledBodyCancellation(string root,UpdateRelease release){
        string destination=Path.Combine(root,"stalled.exe");var input=new StalledStream();Exception failure=null;
        using(var cancel=new CancellationTokenSource()){
            var task=Task.Run(()=>{try{UpdateChecker.SaveVerified(input,release,destination,cancel.Token,null);}catch(Exception ex){failure=ex;}});
            try{
                Check(input.Blocked.Wait(5000),"synthetic body reaches a blocked read after writing a partial chunk");
                cancel.Cancel();Check(task.Wait(3000),"cancellation interrupts stalled body promptly");
                Check(input.DisposedByCancellation&&failure is OperationCanceledException,"cancelled body disposal is reported as cancellation");
                Check(!File.Exists(destination),"stalled cancelled download never finalizes");
                Check(Directory.GetFiles(root,"*.partial").Length==0,"stalled cancellation removes owned partial data");
            }finally{input.Dispose();task.Wait(5000);}
        }
    }
    static void LongDestination(string root,byte[] bytes,UpdateRelease release){
        int padding=230-root.Length-1;if(padding<1)throw new Exception("Fixture root is too long for the download path regression.");
        string folder=Path.Combine(root,new string('p',padding));Directory.CreateDirectory(folder);
        string destination=Path.Combine(folder,"new.exe");
        using(var input=new MemoryStream(bytes))UpdateChecker.SaveVerified(input,release,destination,CancellationToken.None,null);
        Check(File.Exists(destination)&&File.ReadAllBytes(destination).SequenceEqual(bytes),"long valid EXE destination succeeds with shorter temporary name");
        Check(Directory.GetFiles(folder,"*.partial").Length==0,"long-path download leaves no partial file");
    }
    static void RenderForms(){
        foreach(bool english in new[]{false,true})using(var form=new UpdateForm("translator","0.9.0",english,true)){
            Check(!form.Visible,"update fixture never shows a window");
            Check(form.Text==(english?"Check for updates":"检查更新"),"update form title is localized");
            var panel=(TableLayoutPanel)form.Controls[0];panel.Font=form.Font;panel.BackColor=form.BackColor;panel.ForeColor=form.ForeColor;
            form.Controls.Remove(panel);panel.Size=form.ClientSize;panel.CreateControl();panel.PerformLayout();
            foreach(Control child in panel.Controls)child.PerformLayout();
            using(var bitmap=new Bitmap(panel.Width,panel.Height)){
                panel.DrawToBitmap(bitmap,new Rectangle(Point.Empty,panel.Size));
                bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,english?"update-demo-en.png":"update-demo-zh.png"));
            }
            Check(form.GetType().GetField("cancellation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form)==null,"offscreen rendering never starts release checks");
            Check(!(bool)form.GetType().GetField("busy",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form),"offscreen update form remains idle");
            panel.Dispose();
        }
        Console.WriteLine("Offscreen bilingual fixture images: "+AppDomain.CurrentDomain.BaseDirectory);
    }
}
