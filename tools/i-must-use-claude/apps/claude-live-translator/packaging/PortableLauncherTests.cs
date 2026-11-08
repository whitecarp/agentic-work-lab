using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using DafeiyuPortable;

internal static class PortableLauncherTests {
    private static void Check(bool value,string name) { if(!value)throw new Exception(name); }
    private static PayloadFile File(string path) { return new PayloadFile { Path=path,Length=0,Sha256=new string('0',64) }; }
    private static void RejectManifest(PayloadFile[] files,string name) {
        try { PortableLauncher.ValidateManifest(new PayloadManifest { Schema=1,Version=PortableBuildInfo.Version,TotalBytes=0,Files=files }); }
        catch(System.IO.InvalidDataException) { return; }
        throw new Exception(name);
    }
    private static void QuoteRoundTrip(string value) {
        int count;IntPtr argv=CommandLineToArgvW("test.exe "+PortableLauncher.QuoteArgument(value),out count);
        try { Check(argv!=IntPtr.Zero&&count==2,"argument count survives quoting");Check(Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv,IntPtr.Size))==value,"argument preserves exact contents"); }
        finally { if(argv!=IntPtr.Zero)LocalFree(argv); }
    }
    private static void RejectData(Action action,string name) {
        try { action(); }
        catch(InvalidDataException) { return; }
        throw new Exception(name);
    }
    private static string Hash(byte[] bytes) {
        using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
    }
    private static void SameBytes(byte[] expected,byte[] actual,string name) {
        Check(actual!=null&&expected.Length==actual.Length&&Hash(expected)==Hash(actual),name);
    }
    private static byte[] CompressRoundTrip(byte[] original,string name) {
        string before=Hash(original);byte[] compressed=NativeCompression.Compress(original);
        Check(Hash(original)==before,name+" preserves the input buffer");
        SameBytes(original,NativeCompression.Decompress(compressed,original.LongLength),name);
        return compressed;
    }
    private static void CompressionTests() {
        CompressRoundTrip(new byte[]{42},"single-byte native round trip");
        byte[] repetitive=new byte[256*1024];
        for(int i=0;i<repetitive.Length;i++)repetitive[i]=(byte)(i%251);
        byte[] compressed=CompressRoundTrip(repetitive,"compressible native round trip");
        byte[] random=new byte[64*1024];new Random(9127).NextBytes(random);
        CompressRoundTrip(random,"incompressible native round trip");
        RejectData(delegate { NativeCompression.Compress(null); },"null compression input rejected");
        RejectData(delegate { NativeCompression.Compress(new byte[0]); },"empty compression input rejected");
        RejectData(delegate { NativeCompression.Decompress(null,repetitive.Length); },"null compressed input rejected");
        RejectData(delegate { NativeCompression.Decompress(new byte[0],repetitive.Length); },"empty compressed input rejected");
        foreach(long length in new long[]{-1,0,repetitive.Length-1,repetitive.Length+1,256L*1024*1024+1,Int64.MaxValue}) {
            long expected=length;
            RejectData(delegate { NativeCompression.Decompress(compressed,expected); },"invalid archive size rejected: "+expected);
        }
        foreach(int length in new[]{1,compressed.Length/2,compressed.Length-1}) {
            byte[] truncated=new byte[length];Array.Copy(compressed,truncated,length);
            RejectData(delegate { NativeCompression.Decompress(truncated,repetitive.Length); },"truncated compressed buffer rejected: "+length);
        }
        byte[] corrupt=(byte[])compressed.Clone();corrupt[0]^=255;
        RejectData(delegate { NativeCompression.Decompress(corrupt,repetitive.Length); },"corrupt compressed header rejected");
        ArchiveDecodeTests();
    }
    private static void ArchiveDecodeTests() {
        byte[] archive;
        byte[] contents=Encoding.UTF8.GetBytes("Portable fixture / \u79bb\u7ebf\u8bd1\u6587 / preserve original bytes\n");
        using(var buffer=new MemoryStream()) {
            using(var zip=new ZipArchive(buffer,ZipArchiveMode.Create,true)) {
                var entry=zip.CreateEntry("licenses/fixture.txt",CompressionLevel.NoCompression);
                using(var stream=entry.Open())stream.Write(contents,0,contents.Length);
            }
            archive=buffer.ToArray();
        }
        byte[] compressed=NativeCompression.Compress(archive);
        string payloadHash=Hash(compressed),archiveHash=Hash(archive);
        using(var source=new MemoryStream(compressed,false))
        using(var decoded=PortableLauncher.DecodeArchive(source,payloadHash,archiveHash,archive.Length)) {
            SameBytes(archive,decoded.ToArray(),"verified archive preserves every original byte");
            Check(decoded.Position==0,"verified archive is ready to read");
            using(var zip=new ZipArchive(decoded,ZipArchiveMode.Read,true))
            using(var entry=zip.GetEntry("licenses/fixture.txt").Open())
            using(var copy=new MemoryStream()) {
                entry.CopyTo(copy);SameBytes(contents,copy.ToArray(),"verified archive remains a usable ZIP");
            }
        }
        RejectData(delegate { using(var source=new MemoryStream(compressed,false))using(var decoded=PortableLauncher.DecodeArchive(source,new string('0',64),archiveHash,archive.Length)){} },"compressed payload hash mismatch rejected");
        RejectData(delegate { using(var source=new MemoryStream(compressed,false))using(var decoded=PortableLauncher.DecodeArchive(source,payloadHash,new string('0',64),archive.Length)){} },"decompressed archive hash mismatch rejected");
        foreach(string invalid in new[]{"",new string('0',63),new string('G',64),payloadHash.ToUpperInvariant()}) {
            string hash=invalid;
            RejectData(delegate { using(var source=new MemoryStream(compressed,false))using(var decoded=PortableLauncher.DecodeArchive(source,hash,archiveHash,archive.Length)){} },"malformed payload hash rejected");
            RejectData(delegate { using(var source=new MemoryStream(compressed,false))using(var decoded=PortableLauncher.DecodeArchive(source,payloadHash,hash,archive.Length)){} },"malformed archive hash rejected");
        }
        foreach(long length in new long[]{-1,0,archive.Length-1,archive.Length+1,256L*1024*1024+1,Int64.MaxValue}) {
            long expected=length;
            RejectData(delegate { using(var source=new MemoryStream(compressed,false))using(var decoded=PortableLauncher.DecodeArchive(source,payloadHash,archiveHash,expected)){} },"archive envelope size rejected: "+expected);
        }
        byte[] truncated=new byte[compressed.Length-1];Array.Copy(compressed,truncated,truncated.Length);
        RejectData(delegate { using(var source=new MemoryStream(truncated,false))using(var decoded=PortableLauncher.DecodeArchive(source,Hash(truncated),archiveHash,archive.Length)){} },"truncated payload rejected even with a matching compressed hash");
        byte[] corrupt=(byte[])compressed.Clone();corrupt[corrupt.Length/2]^=128;
        RejectData(delegate { using(var source=new MemoryStream(corrupt,false))using(var decoded=PortableLauncher.DecodeArchive(source,Hash(corrupt),archiveHash,archive.Length)){} },"corrupt payload rejected by decoder or archive hash");
        RejectData(delegate { using(var source=new OversizedLengthStream())using(var decoded=PortableLauncher.DecodeArchive(source,payloadHash,archiveHash,archive.Length)){} },"oversized compressed stream rejected before reading");
    }
    private sealed class OversizedLengthStream : MemoryStream {
        public override long Length { get { return 128L*1024*1024+1; } }
        public override int Read(byte[] buffer,int offset,int count) { throw new Exception("Oversized payload must be rejected before reading."); }
    }
    private static void PrepareUiTests() {
        Check(PrepareForm.LocalizeProgress("正在检查运行文件… / Checking application files…",false)=="正在检查运行文件…","Chinese preparation text has no English");
        Check(PrepareForm.LocalizeProgress("正在检查运行文件… / Checking application files…",true)=="Checking application files…","English preparation text has no Chinese");
        Check(PrepareForm.LocalizeProgress("正在准备文件 150 / 900… / Preparing files…",false)=="正在准备文件 150 / 900…","Chinese extraction count retained");
        Check(PrepareForm.LocalizeProgress("正在准备文件 150 / 900… / Preparing files…",true)=="Preparing files 150 / 900…","English extraction count retained");
        foreach(bool english in new[]{false,true})using(var form=new PrepareForm(english)) {
            Check(form.Icon!=null,"launcher receives the embedded icon");
            Check(form.Text==(english?"Whale Translator":"大肥鱼翻译"),"launcher title follows selected system language");
            Check(form.AutoScaleMode==AutoScaleMode.Dpi,"launcher uses DPI layout");
            bool mascot=false,progress=false,hint=false;
            foreach(Control control in form.Controls) {
                Check(!(control is ProgressBar),"launcher never creates the system green progress bar");
                if(control is ClaudeCompanion.WhaleBadge)mascot=true;
                if(control is PrepareProgress)progress=true;
                if(control.Text.Contains(english?"Next time will be quicker":"之后就快了"))hint=true;
                Check(!control.Text.Contains("… / "),"launcher labels display a single language");
            }
            Check(mascot&&progress&&hint,"launcher has mascot, custom progress and first-launch explanation");
            Check(!form.Visible,"launcher UI fixtures do not open a window or prepare runtime");
        }
        using(var stream=typeof(ClaudeCompanion.WindowIcon).Assembly.GetManifestResourceStream(ClaudeCompanion.WindowIcon.ResourceName))
        using(var reader=new BinaryReader(stream)) {
            Check(reader.ReadUInt16()==0&&reader.ReadUInt16()==1,"embedded resource is an ICO");
            Check(reader.ReadUInt16()==7,"embedded ICO preserves seven authored sizes");
            var sizes=new System.Collections.Generic.HashSet<int>();
            for(int i=0;i<7;i++) { int width=reader.ReadByte(),height=reader.ReadByte();Check(width==height,"square icon frame");sizes.Add(width==0?256:width);reader.ReadBytes(14); }
            foreach(int size in new[]{16,24,32,48,64,128,256})Check(sizes.Contains(size),"authored icon size retained: "+size);
        }
    }
    [STAThread] public static int Main() {
        try {
            foreach(string path in new[]{"ClaudeChinese.exe","runtime/python/python312.zip","runtime/venv/Lib/site-packages/numpy/_core/_multiarray_umath.cp312-win_amd64.pyd","licenses/第三方许可.txt"})Check(PortableLauncher.IsSafeRelativePath(path),"safe path accepted");
            foreach(string path in new[]{"","/escape","../escape","runtime/../escape","./file","runtime//file","runtime\\python\\file","C:/escape","runtime/file:stream","runtime/NUL.txt","runtime/COM1","runtime/LPT0.txt","runtime/file.","runtime/file ","runtime/a?b","runtime/a\nb"})Check(!PortableLauncher.IsSafeRelativePath(path),"unsafe path rejected");
            var required=new[]{File("ClaudeChinese.exe"),File("runtime/python/python.exe"),File("backend/offline_worker.py"),File("backend/package_manager.py"),File("backend/package_store.py"),File("backend/argos_catalog.json"),File("backend/glossary.py")};
            PortableLauncher.ValidateManifest(new PayloadManifest { Schema=1,Version=PortableBuildInfo.Version,Files=required,TotalBytes=0 });
            var withoutGlossary=new PayloadFile[required.Length-1];Array.Copy(required,withoutGlossary,withoutGlossary.Length);RejectManifest(withoutGlossary,"glossary module required by both workers");
            RejectManifest(new[]{File("ClaudeChinese.exe")},"incomplete manifest rejected");
            RejectManifest(new[]{required[0],required[1],required[2]},"package manager and catalog required");
            RejectExtra(required,"claudechinese.exe","case-insensitive duplicate rejected");
            RejectExtra(required,"preferences.json","unapproved payload file rejected");
            RejectExtra(required,"backend/glossary.json","user glossary data must not be bundled");
            RejectExtra(required,"backend/credentials.json","credential file must not be bundled");
            RejectExtra(required,".claude/.credentials.json","Claude credential file must not be bundled");
            RejectExtra(required,"runtime/../escape","traversal manifest rejected");
            RejectExtra(required,"runtime/model/model/model.bin","bundled English-Chinese model rejected");
            RejectExtra(required,"runtime/model-zh-en/sentencepiece.model","bundled Chinese-English tokenizer rejected");
            RejectExtra(required,"runtime/venv/Lib/site-packages/extra/model.bin","model weights cannot hide inside allowed package roots");
            RejectExtra(required,"licenses/package.argosmodel","model archive cannot hide inside license root");
            foreach(string value in new[]{"","simple","has spaces","中文参数","a\"quote","C:\\directory with spaces\\","ends\\\\","line1\nline2"})QuoteRoundTrip(value);
            CompressionTests();
            PrepareUiTests();
            Console.WriteLine("PortableLauncherTests passed: traversal/device-name guards, base-only payload, required glossary module, credential/user-data rejection, required package manager/catalog, manifest duplicates, Windows argument round trips, native LZMS round trips, corruption/truncation, payload/archive hashes and bounded archive sizes.");return 0;
        }catch(Exception error) { Console.WriteLine("PortableLauncherTests failed: "+error.Message);return 1; }
    }
    private static void RejectExtra(PayloadFile[] required,string path,string name) {
        var files=new PayloadFile[required.Length+1];Array.Copy(required,files,required.Length);files[required.Length]=File(path);RejectManifest(files,name);
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr CommandLineToArgvW(string command,out int count);
    [DllImport("kernel32.dll")]private static extern IntPtr LocalFree(IntPtr value);
}
