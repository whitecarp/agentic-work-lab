using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace WhaleClaudeHelper {
    internal sealed class RepairOptions {
        public bool SettingsOfficial, SettingsProxy, UserEnvironmentOfficial;
    }
    internal sealed class RepairScan {
        public string SettingsPath, SettingsError, SettingsSha256, EnvironmentError;
        public bool SettingsExists, SettingsValid;
        public List<string> OfficialSettingKeys=new List<string>();
        public List<string> ProxySettingKeys=new List<string>();
        public List<string> UserEnvironmentKeys=new List<string>();
        public List<string> OtherOverrideKeys=new List<string>();
        internal byte[] BaselineSettings;
        internal Dictionary<string,StoredEnvironmentValue> BaselineEnvironment;
    }
    internal sealed class RepairResult {
        public bool Success;
        public string BackupDirectory, Error;
        public List<string> Changed=new List<string>();
        public List<string> Skipped=new List<string>();
    }
    internal sealed class StoredEnvironmentValue {
        public string Value;
        public int Kind;
        public StoredEnvironmentValue() {}
        internal StoredEnvironmentValue(string value,RegistryValueKind kind){Value=value;Kind=(int)kind;}
    }
    // Only allowlisted Claude variables are requested; never enumerate unrelated values.
    internal interface IUserEnvironmentStore {
        Dictionary<string,StoredEnvironmentValue> Read(IEnumerable<string> names);
        void Delete(string name);
        void Set(string name,StoredEnvironmentValue value);
        void NotifyChanged();
    }
    internal sealed class WindowsUserEnvironmentStore : IUserEnvironmentStore {
        public Dictionary<string,StoredEnvironmentValue> Read(IEnumerable<string> names){
            var wanted=new HashSet<string>(names,StringComparer.OrdinalIgnoreCase);
            var result=new Dictionary<string,StoredEnvironmentValue>(StringComparer.OrdinalIgnoreCase);
            using(var key=Registry.CurrentUser.OpenSubKey("Environment",false)){
                if(key==null)return result;
                foreach(string name in key.GetValueNames())if(wanted.Contains(name)){
                    RegistryValueKind kind=key.GetValueKind(name);
                    if(kind!=RegistryValueKind.String&&kind!=RegistryValueKind.ExpandString)throw new InvalidDataException("unsupported_environment_type");
                    string value=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                    if(value==null||value.Length>65536)throw new InvalidDataException("invalid_environment_value");
                    result.Add(name,new StoredEnvironmentValue(value,kind));
                }
            }
            return result;
        }
        public void Delete(string name){using(var key=Registry.CurrentUser.OpenSubKey("Environment",true)){if(key!=null)key.DeleteValue(name,false);}}
        public void Set(string name,StoredEnvironmentValue value){using(var key=Registry.CurrentUser.CreateSubKey("Environment")){key.SetValue(name,value.Value,(RegistryValueKind)value.Kind);}}
        public void NotifyChanged(){IntPtr ignored;SendMessageTimeout(new IntPtr(0xffff),0x001a,IntPtr.Zero,"Environment",2,1500,out ignored);}
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint msg,IntPtr wparam,string lparam,uint flags,uint timeout,out IntPtr result);
    }
    internal sealed class RepairEngine {
        internal static readonly string[] OfficialEnvironmentNames={
            "ANTHROPIC_BASE_URL","ANTHROPIC_API_KEY","ANTHROPIC_AUTH_TOKEN","ANTHROPIC_MODEL",
            "ANTHROPIC_DEFAULT_HAIKU_MODEL","ANTHROPIC_DEFAULT_SONNET_MODEL","ANTHROPIC_DEFAULT_OPUS_MODEL",
            "ANTHROPIC_SMALL_FAST_MODEL","CLAUDE_CODE_USE_BEDROCK","CLAUDE_CODE_USE_VERTEX","CLAUDE_CODE_USE_FOUNDRY","ANTHROPIC_CUSTOM_HEADERS"
        };
        private static readonly HashSet<string> Official=new HashSet<string>(OfficialEnvironmentNames,StringComparer.OrdinalIgnoreCase);
        private static readonly string[] ObserveOnlyNames={"ANTHROPIC_DEFAULT_FABLE_MODEL","ANTHROPIC_DEFAULT_MODEL","CLAUDE_CODE_USE_ANTHROPIC_AWS","CLAUDE_CODE_USE_MANTLE","ANTHROPIC_AWS_BASE_URL","ANTHROPIC_BEDROCK_BASE_URL","ANTHROPIC_VERTEX_BASE_URL","CLAUDE_CODE_PROVIDER_MANAGED_BY_HOST"};
        private static readonly HashSet<string> ObserveOnly=new HashSet<string>(ObserveOnlyNames,StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Proxies=new HashSet<string>(new[]{"HTTP_PROXY","HTTPS_PROXY","ALL_PROXY","NO_PROXY"},StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> RootOfficial=new HashSet<string>(new[]{"apiKeyHelper","model"},StringComparer.Ordinal);
        private static readonly Encoding StrictUtf8=new UTF8Encoding(false,true);
        private readonly string configDirectory,settingsPath,backupRoot;
        private readonly IUserEnvironmentStore environment;
        public RepairEngine(string configurationDirectory,string backupDirectory):this(configurationDirectory,backupDirectory,new WindowsUserEnvironmentStore()){}
        internal RepairEngine(string configurationDirectory,string backupDirectory,IUserEnvironmentStore store){
            AssertPlainPath(configurationDirectory);AssertPlainPath(backupDirectory);
            if(store==null)throw new ArgumentNullException("store");
            configDirectory=Path.GetFullPath(configurationDirectory);settingsPath=Path.Combine(configDirectory,"settings.json");backupRoot=Path.GetFullPath(backupDirectory);environment=store;
        }
        public RepairScan Scan(){
            var scan=new RepairScan{SettingsPath=settingsPath,SettingsValid=true,SettingsError="",EnvironmentError="",SettingsSha256="absent"};
            try{
                AssertPlainPath(configDirectory);AssertPlainPath(settingsPath);
                scan.SettingsExists=File.Exists(settingsPath);
                if(scan.SettingsExists){
                    scan.BaselineSettings=ReadBounded(settingsPath,2097152);scan.SettingsSha256=Hash(scan.BaselineSettings);
                    var document=LosslessJson.Parse(Decode(scan.BaselineSettings));
                    foreach(var member in document.Root.Members){
                        if(RootOfficial.Contains(member.Name))scan.OfficialSettingKeys.Add(member.Name);
                        if(member.Name=="env"){
                            if(member.Value.Kind!='o')throw new InvalidDataException("invalid_env_object");
                            foreach(var entry in member.Value.Members){
                                if(Official.Contains(entry.Name))scan.OfficialSettingKeys.Add("env."+entry.Name);
                                if(Proxies.Contains(entry.Name))scan.ProxySettingKeys.Add("env."+entry.Name);
                                if(ObserveOnly.Contains(entry.Name))scan.OtherOverrideKeys.Add("settings.env."+entry.Name);
                            }
                        }
                    }
                }
            }catch(Exception error){scan.SettingsValid=false;scan.SettingsError=SafeError(error);scan.OfficialSettingKeys.Clear();scan.ProxySettingKeys.Clear();}
            try{scan.BaselineEnvironment=environment.Read(OfficialEnvironmentNames);scan.UserEnvironmentKeys.AddRange(scan.BaselineEnvironment.Keys.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase));foreach(string name in environment.Read(ObserveOnlyNames).Keys)scan.OtherOverrideKeys.Add("user-env."+name);}
            catch(Exception error){scan.EnvironmentError=SafeError(error);scan.BaselineEnvironment=null;}
            return scan;
        }
        public RepairResult Apply(RepairScan scan,RepairOptions options){
            var result=new RepairResult();string transaction=null;
            try{
                if(scan==null||options==null||!String.Equals(scan.SettingsPath,settingsPath,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("invalid_scan");
                bool editSettings=options.SettingsOfficial||options.SettingsProxy;
                if(editSettings&&!scan.SettingsValid)throw new InvalidDataException("invalid_settings");
                if(options.UserEnvironmentOfficial&&scan.BaselineEnvironment==null)throw new InvalidDataException("invalid_environment");
                byte[] before=null,after=null;var keys=new List<string>();
                if(editSettings&&scan.SettingsExists){
                    AssertPlainPath(settingsPath);before=ReadBounded(settingsPath,2097152);
                    if(Hash(before)!=scan.SettingsSha256)throw new InvalidDataException("settings_changed_rescan");
                    string text=Decode(before);var document=LosslessJson.Parse(text);
                    string edited=document.Remove(options.SettingsOfficial?RootOfficial:new HashSet<string>(),delegate(string name){return (options.SettingsOfficial&&Official.Contains(name))||(options.SettingsProxy&&Proxies.Contains(name));},keys);
                    after=EncodeLike(edited,before);
                    if(Hash(after)==Hash(before)){before=null;after=null;}
                }else if(editSettings&&File.Exists(settingsPath))throw new InvalidDataException("settings_changed_rescan");
                var envBefore=new Dictionary<string,StoredEnvironmentValue>(StringComparer.OrdinalIgnoreCase);
                if(options.UserEnvironmentOfficial){
                    var current=environment.Read(OfficialEnvironmentNames);
                    if(!SameEnvironment(current,scan.BaselineEnvironment))throw new InvalidDataException("environment_changed_rescan");
                    foreach(var entry in current)envBefore.Add(entry.Key,entry.Value);
                }
                if(before==null&&envBefore.Count==0){result.Success=true;return result;}
                AssertPlainPath(backupRoot);transaction=NewPrivateDirectory(backupRoot);result.BackupDirectory=transaction;
                var receipt=new RepairReceipt{Schema=1,State="prepared",SettingsPath=before==null?null:settingsPath,SettingsBefore=before==null?null:Hash(before),SettingsAfter=after==null?null:Hash(after),SettingsKeys=keys};
                if(before!=null){WritePrivate(Path.Combine(transaction,"settings.before.bin"),before);if(Hash(ReadBounded(Path.Combine(transaction,"settings.before.bin"),2097152))!=receipt.SettingsBefore)throw new InvalidDataException("backup_verification_failed");}
                if(envBefore.Count>0){
                    foreach(var item in envBefore)receipt.Environment.Add(new EnvironmentReceipt{Name=item.Key,Before=EnvironmentHash(item.Value)});
                    WritePrivate(Path.Combine(transaction,"environment.before.json"),StrictUtf8.GetBytes(Json().Serialize(envBefore)));
                }
                SaveReceipt(transaction,receipt);
                // Recheck every precondition immediately before the first mutation.
                if(before!=null&&Hash(ReadBounded(settingsPath,2097152))!=receipt.SettingsBefore)throw new InvalidDataException("settings_changed_rescan");
                if(envBefore.Count>0&&!SameEnvironment(environment.Read(OfficialEnvironmentNames),envBefore))throw new InvalidDataException("environment_changed_rescan");
                if(before!=null){AtomicReplace(settingsPath,after,receipt.SettingsBefore);result.Changed.AddRange(keys.Select(x=>"settings."+x));}
                foreach(var item in envBefore){
                    var current=environment.Read(new[]{item.Key});StoredEnvironmentValue value;
                    if(!current.TryGetValue(item.Key,out value)||EnvironmentHash(value)!=EnvironmentHash(item.Value))throw new InvalidDataException("environment_changed_rescan");
                    environment.Delete(item.Key);if(environment.Read(new[]{item.Key}).ContainsKey(item.Key))throw new InvalidDataException("environment_write_failed");
                    result.Changed.Add("user-env."+item.Key);
                }
                if(before!=null&&Hash(ReadBounded(settingsPath,2097152))!=receipt.SettingsAfter)throw new InvalidDataException("settings_write_failed");
                receipt.State="complete";SaveReceipt(transaction,receipt);if(envBefore.Count>0)environment.NotifyChanged();result.Success=true;return result;
            }catch(Exception error){
                result.Error=SafeError(error);result.Success=false;
                if(transaction!=null){
                    var restored=Restore(transaction);
                    if(restored.Success){result.Changed.Clear();result.Error="repair_failed_rolled_back: "+result.Error;}
                    else{result.Skipped.AddRange(restored.Skipped);result.Error="repair_failed_review_backup: "+result.Error;}
                }
                return result;
            }
        }
        public List<string> ListBackups(){
            var result=new List<string>();
            try{AssertPlainPath(backupRoot);if(!Directory.Exists(backupRoot))return result;
                foreach(string path in Directory.GetDirectories(backupRoot,"repair-*"))try{var receipt=ReadReceipt(path);if(CompatibleTarget(receipt))result.Add(path);}catch{}
            }catch{}
            result.Sort(StringComparer.Ordinal);result.Reverse();return result;
        }
        public RepairResult Restore(string backupDirectory){
            var result=new RepairResult{BackupDirectory=backupDirectory};bool conflict=false,envChanged=false;
            try{
                var receipt=ReadReceipt(backupDirectory);
                // Reject a different configuration scope before restoring either files or user variables.
                // Environment-only receipts remain compatible because those values are user-wide.
                if(!CompatibleTarget(receipt))throw new InvalidDataException("backup_target_mismatch");
                if(receipt.SettingsPath!=null){
                    try{
                        AssertPlainPath(settingsPath);AssertPlainPath(Path.Combine(backupDirectory,"settings.before.bin"));
                        byte[] before=ReadBounded(Path.Combine(backupDirectory,"settings.before.bin"),2097152);
                        if(Hash(before)!=receipt.SettingsBefore)throw new InvalidDataException("backup_hash_mismatch");
                        string current=File.Exists(settingsPath)?Hash(ReadBounded(settingsPath,2097152)):"absent";
                        if(current==receipt.SettingsBefore)result.Skipped.Add("settings: already original");
                        else if(current!=receipt.SettingsAfter){conflict=true;result.Skipped.Add("settings: changed since repair; retained");}
                        else{AtomicReplace(settingsPath,before,receipt.SettingsAfter);result.Changed.Add("settings restored");}
                    }catch{conflict=true;result.Skipped.Add("settings: could not safely restore");}
                }
                if(receipt.Environment.Count>0){
                    AssertPlainPath(Path.Combine(backupDirectory,"environment.before.json"));
                    var data=Json().Deserialize<Dictionary<string,StoredEnvironmentValue>>(StrictUtf8.GetString(ReadBounded(Path.Combine(backupDirectory,"environment.before.json"),2097152)));
                    foreach(var item in receipt.Environment){
                        try{
                            StoredEnvironmentValue saved;if(!Official.Contains(item.Name)||!data.TryGetValue(item.Name,out saved)||!ValidEnvironmentValue(saved)||EnvironmentHash(saved)!=item.Before)throw new InvalidDataException();
                            var current=environment.Read(new[]{item.Name});StoredEnvironmentValue existing;
                            if(current.TryGetValue(item.Name,out existing)){
                                if(EnvironmentHash(existing)==item.Before)result.Skipped.Add("user-env."+item.Name+": already original");
                                else{conflict=true;result.Skipped.Add("user-env."+item.Name+": changed since repair; retained");}
                            }else{environment.Set(item.Name,saved);var check=environment.Read(new[]{item.Name});if(!check.ContainsKey(item.Name)||EnvironmentHash(check[item.Name])!=item.Before)throw new InvalidDataException();envChanged=true;result.Changed.Add("user-env."+item.Name+" restored");}
                        }catch{conflict=true;result.Skipped.Add("user-env."+item.Name+": could not safely restore");}
                    }
                }
                if(envChanged)environment.NotifyChanged();receipt.State=conflict?"restore-conflict":"restored";SaveReceipt(backupDirectory,receipt);result.Success=!conflict;result.Error=conflict?"restore_conflict":null;return result;
            }catch(Exception error){result.Success=false;result.Error=SafeError(error);return result;}
        }
        private bool CompatibleTarget(RepairReceipt receipt){return receipt.SettingsPath==null||String.Equals(Path.GetFullPath(receipt.SettingsPath),settingsPath,StringComparison.OrdinalIgnoreCase);}
        private RepairReceipt ReadReceipt(string directory){
            string full=Path.GetFullPath(directory),root=backupRoot.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!String.Equals(Path.GetDirectoryName(full),backupRoot.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith("repair-",StringComparison.Ordinal))throw new InvalidDataException("invalid_backup_path");
            AssertPlainPath(full);string path=Path.Combine(full,"receipt.json");AssertPlainPath(path);
            var receipt=Json().Deserialize<RepairReceipt>(StrictUtf8.GetString(ReadBounded(path,262144)));
            if(receipt==null||receipt.Schema!=1||receipt.Environment==null||receipt.Environment.Count>Official.Count)throw new InvalidDataException("invalid_backup_receipt");
            if(receipt.SettingsPath!=null&&(!IsHash(receipt.SettingsBefore)||!IsHash(receipt.SettingsAfter)))throw new InvalidDataException("invalid_backup_receipt");
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var item in receipt.Environment)if(item==null||!Official.Contains(item.Name)||!IsHash(item.Before)||!seen.Add(item.Name))throw new InvalidDataException("invalid_backup_receipt");
            return receipt;
        }
        private static void SaveReceipt(string directory,RepairReceipt receipt){
            byte[] bytes=StrictUtf8.GetBytes(Json().Serialize(receipt));string path=Path.Combine(directory,"receipt.json");AssertPlainPath(path);
            if(File.Exists(path))AtomicReplace(path,bytes,Hash(ReadBounded(path,262144)));else WritePrivate(path,bytes);
        }
        internal static string Hash(byte[] bytes){using(var algorithm=SHA256.Create())return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        private static string EnvironmentHash(StoredEnvironmentValue value){return Hash(StrictUtf8.GetBytes(value.Kind.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"+value.Value));}
        private static bool ValidEnvironmentValue(StoredEnvironmentValue value){return value!=null&&value.Value!=null&&value.Value.Length<=65536&&(value.Kind==(int)RegistryValueKind.String||value.Kind==(int)RegistryValueKind.ExpandString);}
        private static bool SameEnvironment(Dictionary<string,StoredEnvironmentValue> a,Dictionary<string,StoredEnvironmentValue> b){if(a==null||b==null||a.Count!=b.Count)return false;foreach(var item in a){StoredEnvironmentValue value;if(!b.TryGetValue(item.Key,out value)||EnvironmentHash(item.Value)!=EnvironmentHash(value))return false;}return true;}
        private static bool IsHash(string value){return value!=null&&System.Text.RegularExpressions.Regex.IsMatch(value,"^[a-f0-9]{64}$");}
        private static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=2097152,RecursionLimit=64};}
        private static string Decode(byte[] bytes){int offset=bytes.Length>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191?3:0;return StrictUtf8.GetString(bytes,offset,bytes.Length-offset);}
        private static byte[] EncodeLike(string text,byte[] original){var content=StrictUtf8.GetBytes(text);if(original.Length<3||original[0]!=239||original[1]!=187||original[2]!=191)return content;var result=new byte[content.Length+3];result[0]=239;result[1]=187;result[2]=191;Buffer.BlockCopy(content,0,result,3,content.Length);return result;}
        private static byte[] ReadBounded(string path,int maximum){AssertPlainPath(path);using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(stream.Length>maximum)throw new InvalidDataException("file_too_large");var bytes=new byte[stream.Length];int offset=0;while(offset<bytes.Length){int n=stream.Read(bytes,offset,bytes.Length-offset);if(n==0)throw new EndOfStreamException();offset+=n;}return bytes;}}
        internal static void AssertPlainPath(string path){
            if(String.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path))throw new InvalidDataException("invalid_local_path");
            if(path.StartsWith("\\\\",StringComparison.Ordinal))throw new InvalidDataException("network_path_not_supported");
            string tail=path.Substring(Path.GetPathRoot(path).Length);
            foreach(string part in tail.Split(new[]{'\\','/'},StringSplitOptions.RemoveEmptyEntries)) {
                if(part=="."||part==".."||part.EndsWith(".",StringComparison.Ordinal)||part.EndsWith(" ",StringComparison.Ordinal)||part.IndexOf(':')>=0||System.Text.RegularExpressions.Regex.IsMatch(part.Split('.')[0],"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))throw new InvalidDataException("invalid_local_path");
            }
            string cursor=Path.GetFullPath(path);
            while(!String.IsNullOrEmpty(cursor)) {
                FileAttributes attributes;
                try{attributes=File.GetAttributes(cursor);}catch(FileNotFoundException){cursor=Path.GetDirectoryName(cursor);continue;}catch(DirectoryNotFoundException){cursor=Path.GetDirectoryName(cursor);continue;}
                if((attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("linked_path_not_supported");
                if((attributes&FileAttributes.Directory)==0)using(var stream=new FileStream(cursor,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                    NativeFileInfo info;if(!GetFileInformationByHandle(stream.SafeFileHandle,out info))throw new InvalidDataException("file_identity_unavailable");
                    if(info.NumberOfLinks!=1)throw new InvalidDataException("hardlinked_path_not_supported");
                }
                cursor=Path.GetDirectoryName(cursor);
            }
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativeFileInfo {
            public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;
            public uint VolumeSerial,SizeHigh,SizeLow,NumberOfLinks,IndexHigh,IndexLow;
        }
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle,out NativeFileInfo information);
        private static string NewPrivateDirectory(string root){
            AssertPlainPath(root);Directory.CreateDirectory(root);AssertPlainPath(root);
            string path=Path.Combine(root,"repair-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,12));
            var sid=WindowsIdentity.GetCurrent().User;var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);security.SetOwner(sid);security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            Directory.CreateDirectory(path,security);AssertPlainPath(path);return path;
        }
        private static void WritePrivate(string path,byte[] bytes){
            AssertPlainPath(path);
            // Apply the ACL when creating the file, before any credential bytes are written.
            // This also protects the adjacent temporary file used for atomic settings replacement.
            var sid=WindowsIdentity.GetCurrent().User;var security=new FileSecurity();
            security.SetAccessRuleProtection(true,false);security.SetOwner(sid);
            security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,AccessControlType.Allow));
            using(var stream=new FileStream(path,FileMode.CreateNew,FileSystemRights.FullControl,FileShare.None,4096,FileOptions.None,security)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        }
        private static void AtomicReplace(string path,byte[] bytes,string expectedHash){
            AssertPlainPath(path);string temporary=Path.Combine(Path.GetDirectoryName(path),".whale-repair-"+Guid.NewGuid().ToString("N")+".tmp");
            try{WritePrivate(temporary,bytes);AssertPlainPath(path);if(!File.Exists(path)||Hash(ReadBounded(path,2097152))!=expectedHash)throw new InvalidDataException("file_changed_rescan");File.Replace(temporary,path,null);if(Hash(ReadBounded(path,2097152))!=Hash(bytes))throw new InvalidDataException("write_verification_failed");}
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        private static string SafeError(Exception error){
            string code=error is InvalidDataException?error.Message:null;
            if(code!=null&&System.Text.RegularExpressions.Regex.IsMatch(code,"^[a-z_]{1,80}$"))return code;
            if(error is UnauthorizedAccessException)return "access_denied";if(error is DecoderFallbackException)return "invalid_utf8";if(error is IOException)return "file_io_failed";return "operation_failed";
        }
        private sealed class RepairReceipt {
            public RepairReceipt(){}
            public int Schema;public string State,SettingsPath,SettingsBefore,SettingsAfter;public List<string> SettingsKeys=new List<string>();public List<EnvironmentReceipt> Environment=new List<EnvironmentReceipt>();
        }
        private sealed class EnvironmentReceipt {public EnvironmentReceipt(){} public string Name,Before;}
    }
    // Strict parsing plus span removal preserves every unmodified JSON value and its formatting.
    // Duplicate members, comments, trailing commas and excessive nesting are rejected rather than guessed.
    internal sealed class LosslessJson {
        internal sealed class Node {internal char Kind;internal int Start,End;internal List<Member> Members=new List<Member>();}
        internal sealed class Member {internal string Name;internal int Start,End;internal Node Value;}
        private sealed class Span {internal int Start,End;}
        private readonly string text;private int offset,nodes;internal Node Root;
        private LosslessJson(string value){text=value;}
        internal static LosslessJson Parse(string text){var parser=new LosslessJson(text);parser.Root=parser.Value(0);parser.White();if(parser.offset!=text.Length||parser.Root.Kind!='o')throw new InvalidDataException("invalid_json");return parser;}
        internal string Remove(HashSet<string> rootNames,Func<string,bool> envNames,List<string> removed){
            var spans=new List<Span>();Collect(Root,delegate(Member member){return rootNames.Contains(member.Name);},spans,removed,"");
            foreach(var member in Root.Members)if(member.Name=="env"){if(member.Value.Kind!='o')throw new InvalidDataException("invalid_env_object");Collect(member.Value,delegate(Member entry){return envNames(entry.Name);},spans,removed,"env.");}
            var output=new StringBuilder(text);foreach(var span in spans.OrderByDescending(x=>x.Start))output.Remove(span.Start,span.End-span.Start);string result=output.ToString();Parse(result);return result;
        }
        private static void Collect(Node obj,Func<Member,bool> remove,List<Span> spans,List<string> names,string prefix){
            int i=0;while(i<obj.Members.Count){if(!remove(obj.Members[i])){i++;continue;}int start=i;while(i<obj.Members.Count&&remove(obj.Members[i])){names.Add(prefix+obj.Members[i].Name);i++;}
                int end=i-1;if(i<obj.Members.Count)spans.Add(new Span{Start=obj.Members[start].Start,End=obj.Members[i].Start});
                else if(start>0)spans.Add(new Span{Start=obj.Members[start-1].End,End=obj.Members[end].End});
                else spans.Add(new Span{Start=obj.Members[start].Start,End=obj.Members[end].End});
            }
        }
        private Node Value(int depth){
            if(depth>64||++nodes>100000)throw new InvalidDataException("json_limit_exceeded");White();if(offset>=text.Length)throw new InvalidDataException("invalid_json");var node=new Node{Start=offset,Kind=text[offset]};
            if(text[offset]=='{'){
                node.Kind='o';offset++;White();var keys=new HashSet<string>(StringComparer.Ordinal);if(Take('}')){node.End=offset;return node;}
                while(true){White();int start=offset;string name=String();if(!keys.Add(name))throw new InvalidDataException("duplicate_json_key");White();Require(':');var value=Value(depth+1);node.Members.Add(new Member{Name=name,Start=start,End=value.End,Value=value});White();if(Take('}'))break;Require(',');}
            }else if(text[offset]=='['){offset++;White();if(!Take(']'))while(true){Value(depth+1);White();if(Take(']'))break;Require(',');}}
            else if(text[offset]=='"'){String();}
            else if(text[offset]=='t'){Literal("true");}else if(text[offset]=='f'){Literal("false");}else if(text[offset]=='n'){Literal("null");}
            else{var match=System.Text.RegularExpressions.Regex.Match(text.Substring(offset),@"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?");if(!match.Success)throw new InvalidDataException("invalid_json");offset+=match.Length;}
            node.End=offset;return node;
        }
        private string String(){int start=offset;Require('"');while(offset<text.Length){char c=text[offset++];if(c=='"'){try{return new JavaScriptSerializer().Deserialize<string>(text.Substring(start,offset-start));}catch{throw new InvalidDataException("invalid_json");}}if(c<32)throw new InvalidDataException("invalid_json");if(c=='\\'){if(offset>=text.Length)throw new InvalidDataException("invalid_json");char escaped=text[offset++];if(escaped=='u'){for(int i=0;i<4;i++)if(offset>=text.Length||!Uri.IsHexDigit(text[offset++]))throw new InvalidDataException("invalid_json");}else if("\"\\/bfnrt".IndexOf(escaped)<0)throw new InvalidDataException("invalid_json");}}throw new InvalidDataException("invalid_json");}
        private void Literal(string literal){if(offset+literal.Length>text.Length||text.Substring(offset,literal.Length)!=literal)throw new InvalidDataException("invalid_json");offset+=literal.Length;}
        private void White(){while(offset<text.Length&&(text[offset]==' '||text[offset]=='\r'||text[offset]=='\n'||text[offset]=='\t'))offset++;}
        private bool Take(char c){if(offset<text.Length&&text[offset]==c){offset++;return true;}return false;}
        private void Require(char c){if(!Take(c))throw new InvalidDataException("invalid_json");}
    }
}
