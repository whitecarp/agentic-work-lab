using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using WhaleClaudeHelper;

internal static class RepairEngineTests {
    private static int checks;
    private static string root;
    private static readonly Encoding Utf8=new UTF8Encoding(false);
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    private sealed class FakeEnvironment : IUserEnvironmentStore {
        internal Dictionary<string,StoredEnvironmentValue> Values=new Dictionary<string,StoredEnvironmentValue>(StringComparer.OrdinalIgnoreCase);
        internal string FailOnce;
        internal int Writes,Notifications;
        public Dictionary<string,StoredEnvironmentValue> Read(IEnumerable<string> names){var wanted=new HashSet<string>(names,StringComparer.OrdinalIgnoreCase);var copy=new Dictionary<string,StoredEnvironmentValue>(StringComparer.OrdinalIgnoreCase);foreach(var item in Values)if(wanted.Contains(item.Key))copy[item.Key]=new StoredEnvironmentValue(item.Value.Value,(RegistryValueKind)item.Value.Kind);return copy;}
        public void Delete(string name){if(FailOnce==name){FailOnce=null;throw new IOException("fixture failure without secrets");}Values.Remove(name);Writes++;}
        public void Set(string name,StoredEnvironmentValue value){Values[name]=new StoredEnvironmentValue(value.Value,(RegistryValueKind)value.Kind);Writes++;}
        public void NotifyChanged(){Notifications++;}
    }
    private sealed class Fixture {
        internal string Config,Backup,Path;
        internal FakeEnvironment Env=new FakeEnvironment();
        internal RepairEngine Engine;
        internal Fixture(string name,string json){Config=System.IO.Path.Combine(root,name,"config");Backup=System.IO.Path.Combine(root,name,"backups");Directory.CreateDirectory(Config);Path=System.IO.Path.Combine(Config,"settings.json");if(json!=null)File.WriteAllText(Path,json,Utf8);Engine=new RepairEngine(Config,Backup,Env);}
    }
    private static StoredEnvironmentValue Value(string value){return new StoredEnvironmentValue(value,RegistryValueKind.String);}
    private static void MainFlow(){
        string json="{\r\n  \"model\":\"third-party-model\",\r\n  \"apiKeyHelper\":\"never execute this\",\r\n  \"env\":{\"ANTHROPIC_BASE_URL\":\"https://example.invalid/?token=SECRET_URL\", \"ANTHROPIC_API_KEY\":\"SECRET_KEY\", \"https_proxy\":\"https://user:SECRET_PROXY@localhost\", \"UNRELATED\":\"keep verbatim\"},\r\n  \"hooks\" : {\"PostToolUse\":[{\"matcher\":\"*\",\"hooks\":[{\"type\":\"command\",\"command\":\"echo never execute\"}]}]},\r\n  \"permissions\":{\"allow\":[\"Read\"]},\r\n  \"mcpServers\":{\"feishu\":{\"token\":\"KEEP_FEISHU_SECRET\"}},\r\n  \"bigNumber\": 123456789012345678901234567890,\r\n  \"userID\":\"KEEP_ID\", \"machineID\":\"KEEP_MACHINE\"\r\n}\r\n";
        var fixture=new Fixture("main",json);fixture.Env.Values["ANTHROPIC_AUTH_TOKEN"]=Value("SECRET_USER_TOKEN");fixture.Env.Values["HTTP_PROXY"]=Value("KEEP_SHARED_PROXY");fixture.Env.Values["AWS_SECRET_ACCESS_KEY"]=Value("KEEP_AWS");
        byte[] original=File.ReadAllBytes(fixture.Path);var scan=fixture.Engine.Scan();
        Check(scan.SettingsExists&&scan.SettingsValid,"valid settings detected");Check(scan.OfficialSettingKeys.Count==4,"official exact keys detected");Check(scan.ProxySettingKeys.SequenceEqual(new[]{"env.https_proxy"}),"case insensitive scoped proxy");Check(scan.UserEnvironmentKeys.Count==1,"only Claude user variables detected");
        string report=new JavaScriptSerializer().Serialize(scan);Check(!report.Contains("SECRET")&&!report.Contains("never execute")&&!report.Contains("KEEP_ID"),"scan never exposes values");Check(fixture.Env.Writes==0&&File.ReadAllBytes(fixture.Path).SequenceEqual(original),"scan entirely readonly");
        var repaired=fixture.Engine.Apply(scan,new RepairOptions{SettingsOfficial=true});Check(repaired.Success,"official settings repair success");Check(repaired.Changed.Count==4,"repair records only key names");Check(fixture.Env.Values.ContainsKey("ANTHROPIC_AUTH_TOKEN")&&fixture.Env.Writes==0,"unselected user variables untouched");
        string changed=File.ReadAllText(fixture.Path,Utf8);Check(!changed.Contains("SECRET_URL")&&!changed.Contains("SECRET_KEY")&&!changed.Contains("apiKeyHelper"),"only chosen overrides removed");Check(changed.Contains("SECRET_PROXY"),"unselected proxy preserved");
        foreach(string expected in new[]{"\"hooks\" : {\"PostToolUse\":[{\"matcher\":\"*\",\"hooks\":[{\"type\":\"command\",\"command\":\"echo never execute\"}]}]}","\"bigNumber\": 123456789012345678901234567890","KEEP_FEISHU_SECRET","KEEP_ID","KEEP_MACHINE","\"permissions\":{\"allow\":[\"Read\"]}"})Check(changed.Contains(expected),"protected setting remains verbatim");
        Check(File.ReadAllBytes(System.IO.Path.Combine(repaired.BackupDirectory,"settings.before.bin")).SequenceEqual(original),"backup exact original bytes");string receipt=File.ReadAllText(System.IO.Path.Combine(repaired.BackupDirectory,"receipt.json"));Check(!receipt.Contains("SECRET"),"receipt contains no values");
        var acl=Directory.GetAccessControl(repaired.BackupDirectory);Check(acl.AreAccessRulesProtected,"backup ACL inheritance disabled");var sid=WindowsIdentity.GetCurrent().User;foreach(FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier)))Check(rule.IdentityReference.Equals(sid),"backup grants current user only");
        Check(fixture.Engine.ListBackups().Count==1,"own backup listed");var restored=fixture.Engine.Restore(repaired.BackupDirectory);Check(restored.Success,"restore successful");Check(File.ReadAllBytes(fixture.Path).SequenceEqual(original),"restore exact original bytes");Check(fixture.Env.Values["HTTP_PROXY"].Value=="KEEP_SHARED_PROXY"&&fixture.Env.Values["AWS_SECRET_ACCESS_KEY"].Value=="KEEP_AWS","other tool environment preserved");
    }
    private static void JsonEdges(){
        string[] inputs={"{\"env\":{\"ANTHROPIC_API_KEY\":\"secret\"}}","{\"model\":\"x\",\"keep\":1}","{\"keep\":1,\"model\":\"x\"}","{\"a\":1,\"model\":\"x\",\"b\":2,\"apiKeyHelper\":\"x\"}","{ \"model\": \"x\" , \"apiKeyHelper\":\"x\" }","{\"env\":{\"A\":1,\"ANTHROPIC_API_KEY\":\"x\",\"B\":2,\"HTTP_PROXY\":\"x\"},\"keep\":[true,false,null,1.5e-10]}","{\"env\":{\"anthropic_base_url\":\"x\"},\"MODEL\":\"retain\"}"};
        for(int i=0;i<inputs.Length;i++){var fixture=new Fixture("json-"+i,inputs[i]);var result=fixture.Engine.Apply(fixture.Engine.Scan(),new RepairOptions{SettingsOfficial=true,SettingsProxy=true});Check(result.Success,"JSON span edge "+i);LosslessJson.Parse(File.ReadAllText(fixture.Path,Utf8));Check(fixture.Engine.Scan().OfficialSettingKeys.Count==0,"all selected keys removed "+i);Check(fixture.Engine.Restore(result.BackupDirectory).Success&&File.ReadAllText(fixture.Path,Utf8)==inputs[i],"JSON edge restore "+i);}
        var bom=new Fixture("bom","{\"model\":\"x\",\"keep\":\"中文\"}");File.WriteAllText(bom.Path,"{\"model\":\"x\",\"keep\":\"中文\"}",new UTF8Encoding(true));byte[] before=File.ReadAllBytes(bom.Path);var repaired=bom.Engine.Apply(bom.Engine.Scan(),new RepairOptions{SettingsOfficial=true});Check(repaired.Success&&File.ReadAllBytes(bom.Path).Take(3).SequenceEqual(before.Take(3)),"UTF8 BOM preserved");Check(bom.Engine.Restore(repaired.BackupDirectory).Success&&before.SequenceEqual(File.ReadAllBytes(bom.Path)),"BOM restore exact");
        foreach(string invalid in new[]{"{\"env\":{\"ANTHROPIC_API_KEY\":\"SECRET_BAD\",}}","{\"model\":1,\"model\":2}","{\"env\":\"SECRET_BAD\"}","[]","{\"a\":01}","{\"a\":\"\\q\"}"}){var bad=new Fixture("bad-"+Guid.NewGuid().ToString("N"),invalid);var scan=bad.Engine.Scan();Check(!scan.SettingsValid,"malformed JSON rejected");var result=bad.Engine.Apply(scan,new RepairOptions{SettingsOfficial=true});Check(!result.Success&&File.ReadAllText(bad.Path)==invalid&&!Directory.Exists(bad.Backup),"invalid config never modified");Check(!scan.SettingsError.Contains("SECRET"),"parse error redacted");}
    }
    private static void EnvironmentAndRollback(){
        var fixture=new Fixture("env","{\"env\":{\"NO_PROXY\":\"local\",\"KEEP\":true}}");fixture.Env.Values["ANTHROPIC_BASE_URL"]=new StoredEnvironmentValue("%TEST_HOME%/SECRET",RegistryValueKind.ExpandString);fixture.Env.Values["ANTHROPIC_API_KEY"]=Value("SECRET");fixture.Env.Values["ALL_PROXY"]=Value("KEEP");
        var result=fixture.Engine.Apply(fixture.Engine.Scan(),new RepairOptions{SettingsProxy=true,UserEnvironmentOfficial=true});Check(result.Success&&fixture.Env.Values.Count==1&&fixture.Env.Values["ALL_PROXY"].Value=="KEEP","selected Claude user overrides only");Check(fixture.Env.Notifications==1,"environment change broadcast requested");Check(fixture.Engine.Restore(result.BackupDirectory).Success,"environment restore");Check(fixture.Env.Values["ANTHROPIC_BASE_URL"].Kind==(int)RegistryValueKind.ExpandString&&fixture.Env.Values["ANTHROPIC_BASE_URL"].Value=="%TEST_HOME%/SECRET","raw expandable value and kind preserved");
        var failure=new Fixture("rollback","{\"model\":\"x\",\"keep\":5}");failure.Env.Values["ANTHROPIC_BASE_URL"]=Value("SECRET");failure.Env.FailOnce="ANTHROPIC_BASE_URL";string original=File.ReadAllText(failure.Path);var failed=failure.Engine.Apply(failure.Engine.Scan(),new RepairOptions{SettingsOfficial=true,UserEnvironmentOfficial=true});Check(!failed.Success&&failed.Error.StartsWith("repair_failed_rolled_back"),"partial operation rolls back");Check(File.ReadAllText(failure.Path)==original&&failure.Env.Values.ContainsKey("ANTHROPIC_BASE_URL"),"rollback restores untouched before state");
        var observed=new Fixture("observed","{\"env\":{\"CLAUDE_CODE_PROVIDER_MANAGED_BY_HOST\":\"1\",\"ANTHROPIC_DEFAULT_FABLE_MODEL\":\"name\"}}");observed.Env.Values["CLAUDE_CODE_USE_MANTLE"]=Value("1");var scan=observed.Engine.Scan();Check(scan.OtherOverrideKeys.Count==3,"new provider overrides reported for manual review");var noop=observed.Engine.Apply(scan,new RepairOptions{SettingsOfficial=true,UserEnvironmentOfficial=true});Check(noop.Success&&noop.BackupDirectory==null&&observed.Env.Writes==0,"observe only providers never removed");
    }
    private static void Conflicts(){
        var fixture=new Fixture("conflict","{\"model\":\"x\",\"keep\":1}");var scan=fixture.Engine.Scan();File.WriteAllText(fixture.Path,"{\"model\":\"new\",\"keep\":2}",Utf8);var stale=fixture.Engine.Apply(scan,new RepairOptions{SettingsOfficial=true});Check(!stale.Success&&stale.Error=="settings_changed_rescan"&&!Directory.Exists(fixture.Backup),"stale file scan cannot edit");
        fixture.Env.Values["ANTHROPIC_AUTH_TOKEN"]=Value("old");scan=fixture.Engine.Scan();fixture.Env.Values["ANTHROPIC_AUTH_TOKEN"]=Value("new");var staleEnv=fixture.Engine.Apply(scan,new RepairOptions{UserEnvironmentOfficial=true});Check(!staleEnv.Success&&staleEnv.Error=="environment_changed_rescan"&&fixture.Env.Writes==0,"stale user environment cannot edit");
        var result=fixture.Engine.Apply(fixture.Engine.Scan(),new RepairOptions{SettingsOfficial=true,UserEnvironmentOfficial=true});Check(result.Success,"conflict setup repair");File.AppendAllText(fixture.Path," ",Utf8);string revised=File.ReadAllText(fixture.Path);fixture.Env.Values["ANTHROPIC_AUTH_TOKEN"]=Value("USER_CHANGED_AFTER");var restored=fixture.Engine.Restore(result.BackupDirectory);Check(!restored.Success&&restored.Error=="restore_conflict","restore reports later edits");Check(File.ReadAllText(fixture.Path)==revised&&fixture.Env.Values["ANTHROPIC_AUTH_TOKEN"].Value=="USER_CHANGED_AFTER","restore retains later edits");
        var tamper=new Fixture("tamper","{\"model\":\"x\"}");var applied=tamper.Engine.Apply(tamper.Engine.Scan(),new RepairOptions{SettingsOfficial=true});File.AppendAllText(System.IO.Path.Combine(applied.BackupDirectory,"settings.before.bin"),"tampered",Utf8);var noRestore=tamper.Engine.Restore(applied.BackupDirectory);Check(!noRestore.Success&&File.ReadAllText(tamper.Path)=="{}","tampered backup rejected");
        Check(!tamper.Engine.Restore(root).Success,"outside backup path rejected");
        var empty=new Fixture("absent",null);Check(empty.Engine.Scan().SettingsValid&&!empty.Engine.Scan().SettingsExists,"missing configuration is valid absent state");var none=empty.Engine.Apply(empty.Engine.Scan(),new RepairOptions{SettingsOfficial=true});Check(none.Success&&none.BackupDirectory==null&&!File.Exists(empty.Path),"no configuration created on no-op");
    }
    private static void BackupScope(){
        var fixture=new Fixture("scope-a","{\"model\":\"custom\",\"keep\":1}");fixture.Env.Values["ANTHROPIC_API_KEY"]=Value("SCOPE_SECRET");
        var applied=fixture.Engine.Apply(fixture.Engine.Scan(),new RepairOptions{SettingsOfficial=true,UserEnvironmentOfficial=true});Check(applied.Success,"scope setup repair");
        string otherRoot=System.IO.Path.Combine(root,"scope-b","config");Directory.CreateDirectory(otherRoot);var other=new RepairEngine(otherRoot,fixture.Backup,fixture.Env);int writes=fixture.Env.Writes;
        Check(other.ListBackups().Count==0&&fixture.Engine.ListBackups().Contains(applied.BackupDirectory),"backup list filters different settings target");
        string edited=File.ReadAllText(fixture.Path);string receipt=File.ReadAllText(System.IO.Path.Combine(applied.BackupDirectory,"receipt.json"));
        var denied=other.Restore(applied.BackupDirectory);Check(!denied.Success&&denied.Error=="backup_target_mismatch","cross-folder restoration rejected before actions");
        Check(fixture.Env.Writes==writes&&!fixture.Env.Values.ContainsKey("ANTHROPIC_API_KEY")&&File.ReadAllText(fixture.Path)==edited,"scope mismatch preserves file and environment");
        Check(File.ReadAllText(System.IO.Path.Combine(applied.BackupDirectory,"receipt.json"))==receipt,"scope mismatch leaves receipt unchanged");
        fixture.Env.Values["ANTHROPIC_AUTH_TOKEN"]=Value("ENV_ONLY_SECRET");var envOnly=other.Apply(other.Scan(),new RepairOptions{UserEnvironmentOfficial=true});Check(envOnly.Success,"environment-only scope setup");
        Check(other.ListBackups().Count==1&&other.ListBackups()[0]==envOnly.BackupDirectory&&fixture.Engine.ListBackups().Count==2,"environment-only backups available across user config folders");
        Check(fixture.Engine.Restore(envOnly.BackupDirectory).Success&&fixture.Env.Values["ANTHROPIC_AUTH_TOKEN"].Value=="ENV_ONLY_SECRET","environment-only restoration permitted without foreign file edits");
    }
    private static void Reports(){
        var fixture=new Fixture("report","{\"env\":{\"ANTHROPIC_API_KEY\":\"REPORT_SECRET_KEY\",\"ANTHROPIC_BASE_URL\":\"https://SECRET.invalid?token=TOKEN\"},\"mcpServers\":{\"token\":\"OTHER_SECRET\"}}");
        fixture.Env.Values["ANTHROPIC_AUTH_TOKEN"]=Value("REPORT_SECRET_ENV");var scan=fixture.Engine.Scan();string data=System.IO.Path.Combine(root,"report","data");Directory.CreateDirectory(data);string output=System.IO.Path.Combine(root,"report","safe-report.json");
        ReportExporter.SaveNew(output,scan,fixture.Config,data);string report=File.ReadAllText(output);Check(report.Contains("ANTHROPIC_API_KEY")&&!report.Contains("REPORT_SECRET")&&!report.Contains("SECRET.invalid")&&!report.Contains("OTHER_SECRET")&&!report.Contains("Baseline"),"export explicit projection excludes credentials and raw baselines");
        string sentinel=System.IO.Path.Combine(root,"report","existing.json");File.WriteAllText(sentinel,"REPORT_SENTINEL",Utf8);bool rejected=false;try{ReportExporter.SaveNew(sentinel,scan,fixture.Config,data);}catch(IOException){rejected=true;}Check(rejected&&File.ReadAllText(sentinel)=="REPORT_SENTINEL","existing output sentinel never overwritten");
        byte[] source=File.ReadAllBytes(fixture.Path);rejected=false;try{ReportExporter.SaveNew(fixture.Path,scan,fixture.Config,data);}catch(InvalidDataException){rejected=true;}Check(rejected&&File.ReadAllBytes(fixture.Path).SequenceEqual(source),"source settings protected from report export");
        foreach(string protectedPath in new[]{System.IO.Path.Combine(fixture.Config,"new-report.json"),System.IO.Path.Combine(data,"Backups","nested","report.json"),System.IO.Path.Combine(fixture.Config+".","alias-report.json"),sentinel+":report"}){
            rejected=false;try{ReportExporter.SaveNew(protectedPath,scan,fixture.Config,data);}catch(InvalidDataException){rejected=true;}Check(rejected,"protected folder and Windows alias paths rejected");
        }
        Check(File.ReadAllText(sentinel)=="REPORT_SENTINEL"&&fixture.Env.Writes==0,"report leaves existing file and environment unchanged");
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool CreateHardLink(string newFile,string existingFile,IntPtr reserved);
    private static void LinkedPathsAndAliases(){
        var fixture=new Fixture("hardlink",null);string protectedFile=System.IO.Path.Combine(root,"hardlink-protected.json");File.WriteAllText(protectedFile,"{\"model\":\"keep\",\"hooks\":{}}",Utf8);
        Check(CreateHardLink(fixture.Path,protectedFile,IntPtr.Zero),"fixture settings hardlink created");var scan=fixture.Engine.Scan();Check(!scan.SettingsValid&&scan.SettingsError=="hardlinked_path_not_supported","hardlinked settings rejected");
        var result=fixture.Engine.Apply(scan,new RepairOptions{SettingsOfficial=true});Check(!result.Success&&!Directory.Exists(fixture.Backup)&&File.ReadAllText(protectedFile)=="{\"model\":\"keep\",\"hooks\":{}}","hardlinked external file preserved");
        foreach(string path in new[]{fixture.Config+".",fixture.Config+" ",fixture.Path+":stream",System.IO.Path.Combine(fixture.Config,"..","alias")}) {
            bool denied=false;try{RepairEngine.AssertPlainPath(path);}catch(InvalidDataException){denied=true;}Check(denied,"Windows alias and traversal rejected");
        }
        var backup=new Fixture("hardlink-backup","{\"model\":\"custom\",\"keep\":true}");var applied=backup.Engine.Apply(backup.Engine.Scan(),new RepairOptions{SettingsOfficial=true});Check(applied.Success,"backup hardlink setup repair");
        Check(CreateHardLink(System.IO.Path.Combine(root,"backup-before-alias"),System.IO.Path.Combine(applied.BackupDirectory,"settings.before.bin"),IntPtr.Zero),"fixture backup hardlink created");
        string after=File.ReadAllText(backup.Path);Check(!backup.Engine.Restore(applied.BackupDirectory).Success&&File.ReadAllText(backup.Path)==after,"hardlinked backup cannot restore settings");
    }
    public static int Main(){
        try{root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"whale-repair-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);MainFlow();JsonEdges();EnvironmentAndRollback();Conflicts();BackupScope();Reports();LinkedPathsAndAliases();Console.WriteLine("RepairEngineTests passed: "+checks+" checks. Synthetic files and injected environment only. No user settings or registry changes.");Console.WriteLine("Fixture root: "+root);return 0;}
        catch(Exception error){Console.WriteLine("RepairEngineTests failed: "+error.Message);Console.WriteLine(error.StackTrace);return 1;}
    }
}
