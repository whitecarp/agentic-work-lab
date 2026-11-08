using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using WhaleClaudeHelper;

internal static class CacheServiceTests {
    private static string root;
    private static int checks,recycled;
    private static readonly CancellationToken None=CancellationToken.None;
    private static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024};
    private static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    private static string Write(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text,new UTF8Encoding(false));return path;}
    private static CacheContext Context(string name){string home=Path.Combine(root,name);return new CacheContext{Home=Path.Combine(home,"home"),Roaming=Path.Combine(home,"roaming"),Local=Path.Combine(home,"local"),BackupRoot=Path.Combine(home,"backups")};}
    private static CacheService Service(CacheContext context,Func<List<string>> processes=null){return new CacheService(context,delegate(string path,bool directory){string trash=Path.Combine(root,"fixture-recycle",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.GetDirectoryName(trash));if(directory)Directory.Move(path,trash);else File.Move(path,trash);recycled++;},processes??delegate{return new List<string>();});}
    private static CacheTarget Find(CacheService service,string suffix){return service.Audit(None).Single(x=>x.Path.EndsWith(suffix,StringComparison.OrdinalIgnoreCase));}
    private static void PreserveAndRestore() {
        var context=Context("basic");string cache=Path.Combine(context.Home,".claude","cache");
        Write(Path.Combine(cache,"one.txt"),"cache-one");Write(Path.Combine(cache,"nested","two.txt"),"cache-two");Directory.CreateDirectory(Path.Combine(cache,"empty"));
        string settings=Write(Path.Combine(context.Home,".claude","settings.json"),"{\"keep\":true}");
        string history=Write(Path.Combine(context.Home,".claude","projects","history.jsonl"),"history-preserved");
        string skill=Write(Path.Combine(context.Home,".claude","skills","keep.md"),"skill-preserved");
        string cookies=Write(Path.Combine(context.Local,"Claude-Data","claude_fixture","Network","Cookies"),"cookies-preserved");
        Write(Path.Combine(context.Local,"Claude-Data","claude_fixture","Code Cache","entry"),"native-cache");
        Write(Path.Combine(context.Home,".claude","stats-cache.json"),"{\"usage\":123}");
        var service=Service(context);var audit=service.Audit(None);
        Check(audit.Count==3,"only allowlisted cache targets discovered");
        Check(audit.All(x=>x.Eligible),"fixture caches eligible");
        Check(audit.All(x=>!x.Path.EndsWith("settings.json")&&!x.Path.Contains("projects")&&!x.Path.Contains("skills")&&!x.Path.Contains("Cookies")),"protected paths excluded");
        var choice=audit.Single(x=>x.Path==cache);Check(choice.FileCount==2&&choice.Bytes==18,"audit counts and bytes accurate");
        int before=recycled;var cleaned=service.Clean(new[]{choice},None);
        Check(cleaned.Ok&&cleaned.ChangedCount==1&&recycled==before+1,"selected cache sent through recycler once");
        Check(!Directory.Exists(cache),"selected cache gone after recycle");
        Check(File.ReadAllText(settings)=="{\"keep\":true}"&&File.ReadAllText(history)=="history-preserved"&&File.ReadAllText(skill)=="skill-preserved"&&File.ReadAllText(cookies)=="cookies-preserved","protected files preserved exactly");
        Check(File.Exists(Path.Combine(context.Home,".claude","stats-cache.json")),"unselected cache untouched");
        Check(File.Exists(Path.Combine(cleaned.BackupDirectory,"manifest.json")),"backup receipt exists");
        var manifest=Json.Deserialize<CacheBackupManifest>(File.ReadAllText(Path.Combine(cleaned.BackupDirectory,"manifest.json")));
        Check(manifest.Targets[0].Status=="recycled"&&manifest.Targets[0].Files.All(x=>x.Sha256.Length==64),"backup has verified hashes and recycle status");
        var acl=Directory.GetAccessControl(cleaned.BackupDirectory);
        Check(acl.AreAccessRulesProtected,"backup ACL inheritance disabled");
        var current=WindowsIdentity.GetCurrent().User;
        Check(acl.GetAccessRules(true,true,typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().All(x=>x.IdentityReference.Equals(current)),"backup grants only current user");
        Check(service.ListBackups().Contains(cleaned.BackupDirectory),"backup listed for restore");
        var restored=service.Restore(cleaned.BackupDirectory,None);
        Check(restored.Ok&&restored.ChangedCount==1,"restore missing cache succeeds");
        Check(File.ReadAllText(Path.Combine(cache,"one.txt"))=="cache-one"&&File.ReadAllText(Path.Combine(cache,"nested","two.txt"))=="cache-two","restored contents exact");
        Check(Directory.Exists(Path.Combine(cache,"empty")),"empty directory restored");
        Write(Path.Combine(cache,"one.txt"),"new-cache");var again=service.Restore(cleaned.BackupDirectory,None);
        Check(!again.Ok&&again.ChangedCount==0&&again.SkippedCount==1,"existing source blocks restore");
        Check(File.ReadAllText(Path.Combine(cache,"one.txt"))=="new-cache","restore cannot overwrite recreated cache");
    }
    private static void StaleAndForgedSelections() {
        var context=Context("stale");string cache=Write(Path.Combine(context.Home,".claude","stats-cache.json"),"old");var service=Service(context);var target=Find(service,"stats-cache.json");
        File.WriteAllText(cache,"changed-longer");int before=recycled;var result=service.Clean(new[]{target},None);
        Check(!result.Ok&&result.ChangedCount==0&&recycled==before,"changed cache skipped without recycling");
        Check(File.ReadAllText(cache)=="changed-longer","changed source preserved");
        target=Find(service,"stats-cache.json");DateTime timestamp=File.GetLastWriteTimeUtc(cache);File.WriteAllText(cache,"changed-SAME!!");File.SetLastWriteTimeUtc(cache,timestamp);
        result=service.Clean(new[]{target},None);Check(!result.Ok&&result.ChangedCount==0&&recycled==before,"same-length same-timestamp content change rejected by hash stamp");
        string settings=Write(Path.Combine(context.Home,".claude","settings.json"),"protected");
        var forged=new CacheTarget{Path=settings,Id=target.Id,Eligible=true,Stamp=target.Stamp,Name="fake"};result=service.Clean(new[]{forged},None);
        Check(!result.Ok&&result.ChangedCount==0&&File.ReadAllText(settings)=="protected","forged selection cannot touch configuration");
        result=service.Clean(new[]{new CacheTarget{Path=cache,Id="forged",Eligible=true,Stamp=target.Stamp}},None);
        Check(!result.Ok&&result.ChangedCount==0,"forged allowlist identity rejected");
    }
    private static void StoreAndProtectionScope() {
        var context=Context("store-scope");string store=Path.Combine(context.Local,"Packages","Claude_pzs8sxrjxfjjc");
        string desktop=Path.Combine(store,"LocalCache","Roaming","Claude");
        string modern=Path.Combine(store,"LocalCache","Local","Claude-Data","claude_fixture");
        Write(Path.Combine(desktop,"Cache","entry"),"cache");Write(Path.Combine(modern,"logs","entry.log"),"log");
        Write(Path.Combine(context.Home,".claude","telemetry","event"),"telemetry");Write(Path.Combine(context.Home,".claude","logs","event"),"log");
        var preserved=new List<string>();
        foreach(string name in new[]{"Cookies","Local Storage","Session Storage","IndexedDB","Network","Preferences","Local State","config.json","claude_desktop_config.json"})preserved.Add(Write(Path.Combine(desktop,name,"sentinel"),"keep-desktop"));
        foreach(string name in new[]{"projects","skills","plugins","hooks","settings","sessions"})preserved.Add(Write(Path.Combine(context.Home,".claude",name,"sentinel"),"keep-code"));
        foreach(string name in new[]{"settings.json","settings.local.json",".credentials.json","history.jsonl"})preserved.Add(Write(Path.Combine(context.Home,".claude",name),"keep-config"));
        preserved.Add(Write(Path.Combine(context.Home,".claude.json"),"keep-userID-machineID"));
        preserved.Add(Write(Path.Combine(store,"LocalState","Cache","sentinel"),"keep-persistent-store"));
        preserved.Add(Write(Path.Combine(context.Local,"Packages","NotClaude_pzs8sxrjxfjjc","LocalCache","Roaming","Claude","Cache","sentinel"),"keep-other-package"));
        preserved.Add(Write(Path.Combine(context.Local,"Claude-Data","not_claude_profile","Cache","sentinel"),"keep-other-profile"));
        var originals=preserved.ToDictionary(x=>x,x=>File.ReadAllBytes(x));var service=Service(context);var audit=service.Audit(None);
        Check(audit.Count==4&&audit.All(x=>x.Eligible),"only explicit Store, log and telemetry candidates discovered");
        var cleaned=service.Clean(audit,None);Check(cleaned.Ok&&cleaned.ChangedCount==4,"explicit fixture caches cleaned");
        Check(originals.All(x=>File.Exists(x.Key)&&File.ReadAllBytes(x.Key).SequenceEqual(x.Value)),"login storage chats skills plugins hooks identities and unknown packages unchanged");
        var restored=service.Restore(cleaned.BackupDirectory,None);
        Console.WriteLine("Store fixture restore: changed="+restored.ChangedCount+", skipped="+restored.SkippedCount+", messages="+String.Join(" | ",restored.Messages));
        Check(restored.Ok,"Store fixture cache restore succeeds");
    }
    private static string StoreProfile(CacheContext context){return Path.Combine(context.Local,"Packages","Claude_pzs8sxrjxfjjc","LocalCache","Local","Claude-Data","claude_fixture");}
    private static CacheContext LongProfileContext(string name,int parentLength) {
        var context=Context(name);string parent=StoreProfile(context);
        int padding=parentLength-parent.Length-1;if(padding<1)throw new Exception("Fixture root is too long for the path-limit regression.");
        context.Local=Path.Combine(context.Local,new string('p',padding));return context;
    }
    private static void LongRestorePaths() {
        // A supported cache can fit MAX_PATH while a 47-character temporary name does not.
        // Lengths are deliberate and independent of runner usernames and TEMP locations.
        var context=LongProfileContext("long-restore",220);string cache=Path.Combine(StoreProfile(context),"logs");
        string original=Write(Path.Combine(cache,"x"),"long-path-cache");var service=Service(context);var cleaned=service.Clean(service.Audit(None),None);
        Check(cleaned.Ok,"long source cache is backed up and recycled");var restored=service.Restore(cleaned.BackupDirectory,None);
        Console.WriteLine("Long-path fixture restore: changed="+restored.ChangedCount+", skipped="+restored.SkippedCount+", messages="+String.Join(" | ",restored.Messages));
        Check(restored.Ok&&File.ReadAllText(original)=="long-path-cache","long cache restores using a short collision-safe staging name");

        var boundary=LongProfileContext("restore-limit",240);string boundaryCache=Path.Combine(StoreProfile(boundary),"logs");
        Write(Path.Combine(boundaryCache,"x"),"backup-must-survive");var boundaryService=Service(boundary);var boundaryClean=boundaryService.Clean(boundaryService.Audit(None),None);
        Check(boundaryClean.Ok,"near-limit source cache backup succeeds");
        string receipt=Path.Combine(boundaryClean.BackupDirectory,"manifest.json");byte[] before=File.ReadAllBytes(receipt);var manifest=Json.Deserialize<CacheBackupManifest>(File.ReadAllText(receipt));
        string backup=Path.Combine(boundaryClean.BackupDirectory,manifest.Targets[0].BackupRelativePath,"x");int recycledBefore=recycled;
        var limited=boundaryService.Restore(boundaryClean.BackupDirectory,None);
        Console.WriteLine("Path-limit fixture restore: changed="+limited.ChangedCount+", skipped="+limited.SkippedCount+", messages="+String.Join(" | ",limited.Messages));
        Check(!limited.Ok&&limited.ChangedCount==0&&limited.Messages.Any(x=>x.Contains("path-too-long")),"unsupported restore path fails with an explicit path-limit result");
        Check(!Directory.Exists(boundaryCache)&&recycled==recycledBefore,"failed restore neither creates a partial target nor recycles data");
        Check(File.ReadAllBytes(receipt).SequenceEqual(before)&&File.ReadAllText(backup)=="backup-must-survive","path-limit failure retains complete verified backup and receipt");
    }
    private static void ProcessAndBackupFailure() {
        var context=Context("process");string path=Write(Path.Combine(context.Home,".claude","cache","cache.txt"),"one");var service=Service(context);var row=Find(service,Path.Combine(".claude","cache"));
        var blocked=Service(context,delegate{return new List<string>{"claude.exe (PID 123)"};});int before=recycled;
        var result=blocked.Clean(new[]{row},None);Check(!result.Ok&&recycled==before&&File.Exists(path),"active Claude blocks cleanup");
        int calls=0;var appeared=Service(context,delegate{calls++;return calls>=2?new List<string>{"claude.exe (PID 123)"}:new List<string>();});result=appeared.Clean(new[]{row},None);
        Check(!result.Ok&&recycled==before&&File.Exists(path),"Claude started after backup blocks recycling");
        Check(result.BackupDirectory!=null&&Directory.Exists(result.BackupDirectory),"completed backup retained when process starts");
        var missing=context;string other=Write(Path.Combine(context.Home,".claude","debug","locked.txt"),"locked");var rows=service.Audit(None).Where(x=>x.Path.EndsWith("cache")||x.Path.EndsWith("debug")).ToArray();
        using(var locked=new FileStream(other,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){result=service.Clean(rows,None);}
        Check(!result.Ok&&recycled==before&&File.Exists(path)&&File.Exists(other),"failed later backup prevents all source recycling");
        var cancelled=new CancellationTokenSource();cancelled.Cancel();bool canceled=false;try{service.Audit(cancelled.Token);}catch(OperationCanceledException){canceled=true;}Check(canceled,"audit supports cancellation");
    }
    private static void TamperedBackups() {
        var context=Context("tamper");string original=Write(Path.Combine(context.Home,".claude","stats-cache.json"),"fixture-statistics");var service=Service(context);var target=Find(service,"stats-cache.json");var cleaned=service.Clean(new[]{target},None);
        Check(cleaned.Ok,"file cache clean succeeds");
        string manifestFile=Path.Combine(cleaned.BackupDirectory,"manifest.json");var manifest=Json.Deserialize<CacheBackupManifest>(File.ReadAllText(manifestFile));string copy=Path.Combine(cleaned.BackupDirectory,manifest.Targets[0].BackupRelativePath);
        File.WriteAllText(copy,"tampered");var restored=service.Restore(cleaned.BackupDirectory,None);
        Check(!restored.Ok&&!File.Exists(original),"hash-mismatched backup not restored");
        string outside=Write(Path.Combine(root,"outside-restore","manifest.json"),"{}");restored=service.Restore(Path.GetDirectoryName(outside),None);
        Check(!restored.Ok&&restored.Messages.Contains("invalid-backup-location"),"backup outside helper root rejected");
        manifest.Targets[0].OriginalPath=Path.Combine(context.Home,".claude","settings.json");File.WriteAllText(manifestFile,Json.Serialize(manifest));restored=service.Restore(cleaned.BackupDirectory,None);
        Check(!restored.Ok&&!File.Exists(manifest.Targets[0].OriginalPath),"manifest cannot restore protected settings");
        manifest.Targets[0].OriginalPath=original;manifest.Targets[0].BackupRelativePath="../outside";File.WriteAllText(manifestFile,Json.Serialize(manifest));restored=service.Restore(cleaned.BackupDirectory,None);
        Check(!restored.Ok&&!File.Exists(original),"manifest path traversal rejected");
    }
    private static void ReparsePoints() {
        var context=Context("reparse");string cache=Path.Combine(context.Home,".claude","cache");Directory.CreateDirectory(cache);
        string outside=Path.Combine(root,"junction-protected");Write(Path.Combine(outside,"keep.txt"),"protected-outside");string junction=Path.Combine(cache,"link");
        var command=new ProcessStartInfo("cmd.exe","/d /c mklink /J \""+junction+"\" \""+outside+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var process=Process.Start(command)){process.StandardOutput.ReadToEnd();process.StandardError.ReadToEnd();process.WaitForExit();Check(process.ExitCode==0,"fixture junction created");}
        var service=Service(context);var target=Find(service,Path.Combine(".claude","cache"));
        Check(!target.Eligible&&target.Warning=="reparse-point-skipped","junction inside cache blocks whole target");int before=recycled;var result=service.Clean(new[]{target},None);
        Check(!result.Ok&&recycled==before&&File.ReadAllText(Path.Combine(outside,"keep.txt"))=="protected-outside","junction never followed or recycled");
        var forced=new CacheTarget{Path=target.Path,Id=target.Id,Eligible=true,Stamp="forced",Name="forced"};result=service.Clean(new[]{forced},None);
        Check(!result.Ok&&recycled==before,"forged eligibility cannot bypass reparse guard");
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool CreateHardLink(string newFile,string existingFile,IntPtr reserved);
    private static void HardlinksAndUnsafeRoots() {
        var context=Context("hardlinks");string source=Write(Path.Combine(root,"hardlink-protected","settings.json"),"protected");string cache=Path.Combine(context.Home,".claude","stats-cache.json");Directory.CreateDirectory(Path.GetDirectoryName(cache));
        Check(CreateHardLink(cache,source,IntPtr.Zero),"fixture hardlink created");var service=Service(context);var target=Find(service,"stats-cache.json");int before=recycled;
        Check(!target.Eligible&&target.Warning=="hardlink-skipped","hardlink cache is ineligible");Check(!service.Clean(new[]{target},None).Ok&&recycled==before&&File.ReadAllText(source)=="protected","linked source preserved and never recycled");
        var backupContext=Context("backup-hardlink");string original=Write(Path.Combine(backupContext.Home,".claude","stats-cache.json"),"saved");var backupService=Service(backupContext);var cleaned=backupService.Clean(backupService.Audit(None),None);
        var manifest=Json.Deserialize<CacheBackupManifest>(File.ReadAllText(Path.Combine(cleaned.BackupDirectory,"manifest.json")));string copy=Path.Combine(cleaned.BackupDirectory,manifest.Targets[0].BackupRelativePath);
        Check(CreateHardLink(Path.Combine(root,"backup-hardlink-alias"),copy,IntPtr.Zero),"fixture backup hardlink created");
        Check(!backupService.Restore(cleaned.BackupDirectory,None).Ok&&!File.Exists(original),"hardlinked backup cannot restore");
        foreach(string suffix in new[]{"."," ",":stream"}) {var invalid=Context("unsafe");invalid.Home+=suffix;bool denied=false;try{Service(invalid);}catch{denied=true;}Check(denied,"Win32 alias root denied");}
        var overlap=Context("overlap");string overlapCache=Path.Combine(overlap.Home,".claude","cache");Write(Path.Combine(overlapCache,"entry"),"keep");overlap.BackupRoot=Path.Combine(overlapCache,"backups");var overlapService=Service(overlap);
        Check(!overlapService.Clean(overlapService.Audit(None),None).Ok&&Directory.Exists(overlapCache)&&!Directory.Exists(overlap.BackupRoot),"backup cannot be created within selected cache");
    }
    public static int Main() {
        try {
            root=Path.Combine(Path.GetTempPath(),"whale-cache-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            PreserveAndRestore();StaleAndForgedSelections();StoreAndProtectionScope();LongRestorePaths();ProcessAndBackupFailure();TamperedBackups();ReparsePoints();HardlinksAndUnsafeRoots();
            Console.WriteLine("CacheServiceTests passed: "+checks+" checks. Fixture-only recycler; no real user data or Recycle Bin touched.");Console.WriteLine("Fixture evidence: "+root);return 0;
        }catch(Exception error){Console.WriteLine("CacheServiceTests failed: "+error.Message);Console.WriteLine(error.StackTrace);return 1;}
    }
}
