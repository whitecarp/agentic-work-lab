using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.VisualBasic.FileIO;

namespace WhaleClaudeHelper {
    public sealed class CacheContext {
        public string Home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public string Roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public string Local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public string BackupRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DafeiyuClaudeHelper","Backups","Cache");
    }
    public sealed class CacheTarget {
        public string Id,Path,Name,Category,Warning,Stamp;
        public long Bytes;
        public int FileCount;
        public bool Eligible;
    }
    public sealed class CacheOperationResult {
        public bool Ok;
        public string BackupDirectory;
        public List<string> Messages=new List<string>();
        public int ChangedCount,SkippedCount;
    }
    public sealed class CacheBackupFile {
        public string RelativePath,Sha256;
        public long Length;
    }
    public sealed class CacheBackupTarget {
        public string Id,OriginalPath,BackupRelativePath,Status;
        public bool Directory;
        public List<string> Directories=new List<string>();
        public List<CacheBackupFile> Files=new List<CacheBackupFile>();
    }
    public sealed class CacheBackupManifest {
        public int Version=1;
        public string CreatedUtc;
        public List<CacheBackupTarget> Targets=new List<CacheBackupTarget>();
    }

    // This service never deletes persistent storage, credentials, histories or whole profiles.
    // The injected recycler/process probe are used only by fixture tests; production uses Windows.
    public sealed class CacheService {
        private readonly CacheContext context;
        private readonly Action<string,bool> recycle;
        private readonly Func<List<string>> processes;
        private static readonly string[] DesktopCaches={"Cache","Code Cache","GPUCache","DawnCache","GPUPersistentCache","DawnGraphiteCache","DawnWebGPUCache","GrShaderCache","ShaderCache","logs"};
        private static readonly string[] CodeCaches={"cache","stats-cache.json","telemetry","usage-data","usage.jsonl","usage.with-fix.jsonl","debug","logs"};
        private const long MaxBytes=10L*1024*1024*1024;
        private const int MaxFiles=100000;
        private readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=64};
        public CacheService(CacheContext context=null) : this(context,null,null) {}
        internal CacheService(CacheContext context,Action<string,bool> recycler,Func<List<string>> processProbe) {
            this.context=context??new CacheContext();
            foreach(string root in new[]{this.context.Home,this.context.Roaming,this.context.Local,this.context.BackupRoot})RequireRoot(root);
            recycle=recycler??RecycleWindows;
            processes=processProbe??ActiveClaudeProcesses;
        }
        public static List<string> ActiveClaudeProcesses() {
            var result=new List<string>();
            try {
                using(var query=new ManagementObjectSearcher("SELECT Name, ProcessId, CommandLine FROM Win32_Process WHERE Name='Claude.exe' OR Name='claude.exe' OR Name='node.exe' OR Name='bun.exe' OR Name='nodejs.exe'")) {
                    foreach(ManagementObject item in query.Get())using(item) {
                        string name=Convert.ToString(item["Name"]),command=Convert.ToString(item["CommandLine"]);
                        bool claude=String.Equals(name,"claude.exe",StringComparison.OrdinalIgnoreCase);
                        if(!claude&&!String.IsNullOrEmpty(command))claude=command.IndexOf("@anthropic-ai\\claude-code",StringComparison.OrdinalIgnoreCase)>=0||command.IndexOf("@anthropic-ai/claude-code",StringComparison.OrdinalIgnoreCase)>=0;
                        if(claude)result.Add(name+" (PID "+Convert.ToString(item["ProcessId"],CultureInfo.InvariantCulture)+")");
                        // Missing command-line access means the safety check cannot conclusively rule out a CLI.
                        else if(String.IsNullOrWhiteSpace(command))result.Add("process-check-incomplete (PID "+Convert.ToString(item["ProcessId"],CultureInfo.InvariantCulture)+")");
                    }
                }
            }catch { result.Add("process-check-unavailable"); }
            return result;
        }
        public List<CacheTarget> Audit(CancellationToken cancel) {
            var result=new List<CacheTarget>();
            foreach(var candidate in Candidates()) {
                cancel.ThrowIfCancellationRequested();
                if(!File.Exists(candidate.Path)&&!Directory.Exists(candidate.Path))continue;
                try {
                    var inventory=Inventory(candidate.Path,cancel,false);
                    candidate.Bytes=inventory.Files.Sum(f=>f.Length);candidate.FileCount=inventory.Files.Count;
                    candidate.Stamp=Stamp(candidate.Path,cancel);candidate.Eligible=true;
                }catch(OperationCanceledException){throw;}
                catch(Exception ex){candidate.Eligible=false;candidate.Warning=SafeError(ex);}
                result.Add(candidate);
            }
            return result;
        }
        public CacheOperationResult Clean(IEnumerable<CacheTarget> selected,CancellationToken cancel) {
            var result=new CacheOperationResult();
            var active=processes();if(active.Count>0){result.Messages.Add("close-claude-first: "+String.Join(", ",active));return result;}
            var allowed=Candidates().ToDictionary(x=>x.Path,StringComparer.OrdinalIgnoreCase);
            var inputs=(selected??new CacheTarget[0]).Where(x=>x!=null).GroupBy(x=>x.Path??"",StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).ToList();
            if(inputs.Count==0){result.Messages.Add("nothing-selected");return result;}
            var approved=new List<CacheTarget>();
            foreach(var input in inputs) {
                cancel.ThrowIfCancellationRequested();CacheTarget current;
                if(String.IsNullOrWhiteSpace(input.Path)||!allowed.TryGetValue(input.Path,out current)||current.Id!=input.Id||!input.Eligible){result.SkippedCount++;result.Messages.Add("not-allowlisted");continue;}
                try {
                    if(String.Equals(current.Path,Path.GetFullPath(context.BackupRoot),StringComparison.OrdinalIgnoreCase)||Under(current.Path,context.BackupRoot))throw new IOException("backup-overlaps-cache");
                    if(String.IsNullOrEmpty(input.Stamp)||Stamp(current.Path,cancel)!=input.Stamp)throw new IOException("changed-since-audit");
                    current.Stamp=input.Stamp;approved.Add(current);
                }catch(OperationCanceledException){throw;}
                catch(Exception ex){result.SkippedCount++;result.Messages.Add(current.Name+": "+SafeError(ex));}
            }
            // An invalid/stale/unreadable selected item aborts the entire batch before recycling.
            if(approved.Count==0||result.SkippedCount>0)return result;
            string backup=null;
            var manifest=new CacheBackupManifest{CreatedUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)};
            try {
                EnsurePlain(context.BackupRoot);Directory.CreateDirectory(context.BackupRoot);EnsurePlain(context.BackupRoot);
                backup=Path.Combine(context.BackupRoot,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N").Substring(0,10));
                Directory.CreateDirectory(backup);Restrict(backup);result.BackupDirectory=backup;
                // Finish and verify every selected backup before any source is moved to Recycle Bin.
                foreach(var current in approved) {
                    cancel.ThrowIfCancellationRequested();
                    if(Stamp(current.Path,cancel)!=current.Stamp)throw new IOException("changed-since-audit");
                    var inventory=Inventory(current.Path,cancel,true);inventory.Id=current.Id;inventory.OriginalPath=current.Path;
                    inventory.BackupRelativePath="data/"+manifest.Targets.Count.ToString("D4",CultureInfo.InvariantCulture);inventory.Status="backed-up";
                    string destination=SafeChild(backup,inventory.BackupRelativePath);
                    CopyVerified(current.Path,destination,inventory,cancel);
                    Verify(current.Path,inventory,cancel);Verify(destination,inventory,cancel);
                    manifest.Targets.Add(inventory);WriteManifest(backup,manifest);
                }
                if(processes().Count>0)throw new IOException("close-claude-first");
                foreach(var item in manifest.Targets) {
                    cancel.ThrowIfCancellationRequested();
                    if(processes().Count>0)throw new IOException("close-claude-first");
                    Verify(item.OriginalPath,item,cancel);EnsurePlain(item.OriginalPath);
                    recycle(item.OriginalPath,item.Directory);
                    if(File.Exists(item.OriginalPath)||Directory.Exists(item.OriginalPath))throw new IOException("recycle-not-completed");
                    item.Status="recycled";result.ChangedCount++;WriteManifest(backup,manifest);
                }
                result.Ok=result.SkippedCount==0;result.Messages.Add("cache-recycled-backup-verified");
            }catch(OperationCanceledException){result.Messages.Add("cancelled-backup-retained");}
            catch(Exception ex){result.Messages.Add(SafeError(ex));}
            return result;
        }
        public List<string> ListBackups() {
            var result=new List<string>();
            try {
                EnsurePlain(context.BackupRoot);if(!Directory.Exists(context.BackupRoot))return result;
                foreach(string directory in Directory.GetDirectories(context.BackupRoot)) {
                    EnsurePlain(directory);
                    if(File.Exists(Path.Combine(directory,"manifest.json")))result.Add(directory);
                }
            }catch { }
            result.Sort(StringComparer.OrdinalIgnoreCase);result.Reverse();return result;
        }
        public CacheOperationResult Restore(string backupDirectory,CancellationToken cancel) {
            var result=new CacheOperationResult{BackupDirectory=backupDirectory};
            if(processes().Count>0){result.Messages.Add("close-claude-first");return result;}
            try {
                if(!Under(context.BackupRoot,backupDirectory)||!String.Equals(Path.GetDirectoryName(Path.GetFullPath(backupDirectory)),Path.GetFullPath(context.BackupRoot).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))throw new IOException("invalid-backup-location");
                EnsurePlain(backupDirectory);string receipt=Path.Combine(backupDirectory,"manifest.json");EnsurePlain(receipt);
                if(new FileInfo(receipt).Length>32*1024*1024)throw new IOException("invalid-backup-manifest");
                var manifest=json.Deserialize<CacheBackupManifest>(File.ReadAllText(receipt,Encoding.UTF8));
                if(manifest==null||manifest.Version!=1||manifest.Targets==null||manifest.Targets.Count>10000)throw new IOException("invalid-backup-manifest");
                var allowed=Candidates().ToDictionary(x=>x.Path,StringComparer.OrdinalIgnoreCase);
                foreach(var item in manifest.Targets) {
                    cancel.ThrowIfCancellationRequested();CacheTarget allowedTarget;
                    if(item==null||String.IsNullOrEmpty(item.OriginalPath)||!allowed.TryGetValue(item.OriginalPath,out allowedTarget)||allowedTarget.Id!=item.Id){result.SkippedCount++;result.Messages.Add("restore-not-allowlisted");continue;}
                    if(File.Exists(item.OriginalPath)||Directory.Exists(item.OriginalPath)){result.SkippedCount++;result.Messages.Add(allowedTarget.Name+": restore-conflict-original-exists");continue;}
                    try {
                        if(processes().Count>0)throw new IOException("close-claude-first");
                        EnsurePlain(item.OriginalPath);string source=SafeChild(backupDirectory,item.BackupRelativePath);Verify(source,item,cancel);
                        string parent=Path.GetDirectoryName(item.OriginalPath);EnsurePlain(parent);Directory.CreateDirectory(parent);EnsurePlain(parent);
                        // Store profiles already consume much of .NET Framework's path budget.
                        // Keep the same-directory atomic move, but use a short random 8.3 name.
                        // CopyVerified still refuses existing destinations; no overwrite retry.
                        string staging=Path.Combine(parent,Path.GetRandomFileName());
                        CopyVerified(source,staging,item,cancel);Verify(staging,item,cancel);
                        if(processes().Count>0)throw new IOException("close-claude-first");
                        EnsurePlain(item.OriginalPath);
                        if(item.Directory)Directory.Move(staging,item.OriginalPath);else File.Move(staging,item.OriginalPath);
                        Verify(item.OriginalPath,item,cancel);result.ChangedCount++;
                    }catch(OperationCanceledException){throw;}
                    catch(Exception ex){result.SkippedCount++;result.Messages.Add(allowedTarget.Name+": "+SafeError(ex));}
                }
                result.Ok=result.SkippedCount==0&&result.ChangedCount>0;
                result.Messages.Add(result.Ok?"restore-verified":"restore-finished-with-skips");
            }catch(OperationCanceledException){result.Messages.Add("cancelled");}
            catch(Exception ex){result.Messages.Add(SafeError(ex));}
            return result;
        }
        private List<CacheTarget> Candidates() {
            var result=new List<CacheTarget>();
            Action<string,string,string> add=delegate(string path,string name,string category){result.Add(new CacheTarget{Path=Path.GetFullPath(path),Name=name,Category=category,Id=HashText(Path.GetFullPath(path).ToUpperInvariant())});};
            foreach(string name in CodeCaches)add(Path.Combine(context.Home,".claude",name),"Claude Code / "+name,"code");
            foreach(string root in new[]{Path.Combine(context.Roaming,"Claude"),Path.Combine(context.Local,"Claude")})foreach(string name in DesktopCaches)add(Path.Combine(root,name),"Claude Desktop / "+name,"desktop");
            AddProfiles(Path.Combine(context.Local,"Claude-Data"),add);
            // Explicit supported Microsoft Store package family only, never *Claude* matching.
            // LocalState, settings, credential stores and the package root are not cache targets.
            string package=Path.Combine(context.Local,"Packages","Claude_pzs8sxrjxfjjc","LocalCache");
            foreach(string local in new[]{"Roaming","Local"}) {
                string scope=Path.Combine(package,local);
                foreach(string name in DesktopCaches)add(Path.Combine(scope,"Claude",name),"Claude Desktop (Store) / "+name,"desktop");
                AddProfiles(Path.Combine(scope,"Claude-Data"),add);
            }
            return result.GroupBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).ToList();
        }
        private static void AddProfiles(string modern,Action<string,string,string> add) {
            try {
                EnsurePlain(modern);
                if(Directory.Exists(modern))foreach(string profile in Directory.GetDirectories(modern,"claude_*",System.IO.SearchOption.TopDirectoryOnly)) {
                    // Exclude unexpected profile names and symbolic links; never enumerate other products.
                    if(!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(profile),"^claude_[A-Za-z0-9_-]+$"))continue;
                    foreach(string name in DesktopCaches)add(Path.Combine(profile,name),"Claude Desktop / "+Path.GetFileName(profile)+" / "+name,"desktop");
                }
            }catch { }
        }
        private static CacheBackupTarget Inventory(string path,CancellationToken cancel,bool hashes) {
            EnsurePlain(path);var result=new CacheBackupTarget{Directory=Directory.Exists(path)};
            var timer=Stopwatch.StartNew();long bytes=0;
            if(!result.Directory&&!File.Exists(path))throw new IOException("source-missing");
            Action<string,string> add=delegate(string full,string relative) {
                cancel.ThrowIfCancellationRequested();EnsurePlain(full);
                var info=new FileInfo(full);bytes=checked(bytes+info.Length);
                if(bytes>MaxBytes||result.Files.Count>=MaxFiles||timer.Elapsed.TotalSeconds>30)throw new IOException("scan-limit");
                result.Files.Add(new CacheBackupFile{RelativePath=relative,Length=info.Length,Sha256=hashes?HashFile(full):null});
            };
            if(!result.Directory)add(path,"");
            else {
                var pending=new Stack<string>();pending.Push(path);
                while(pending.Count>0) {
                    cancel.ThrowIfCancellationRequested();string dir=pending.Pop();EnsurePlain(dir);
                    if(result.Directories.Count>MaxFiles||timer.Elapsed.TotalSeconds>30)throw new IOException("scan-limit");
                    foreach(string child in Directory.GetFileSystemEntries(dir)) {
                        EnsurePlain(child);string relative=Path.GetFullPath(child).Substring(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).Length+1);
                        if(Directory.Exists(child)){result.Directories.Add(relative);pending.Push(child);}else add(child,relative);
                    }
                }
            }
            result.Files.Sort((a,b)=>StringComparer.OrdinalIgnoreCase.Compare(a.RelativePath,b.RelativePath));result.Directories.Sort(StringComparer.OrdinalIgnoreCase);return result;
        }
        private static string Stamp(string path,CancellationToken cancel) {
            var data=Inventory(path,cancel,true);var text=new StringBuilder();text.Append(data.Directory?"D":"F");
            foreach(string dir in data.Directories)text.Append('\n').Append("D:").Append(dir);
            foreach(var file in data.Files)text.Append('\n').Append(file.RelativePath).Append('|').Append(file.Length).Append('|').Append(file.Sha256);
            return HashText(text.ToString());
        }
        private static void CopyVerified(string source,string destination,CacheBackupTarget inventory,CancellationToken cancel) {
            EnsurePlain(source);EnsurePlain(destination);
            if(File.Exists(destination)||Directory.Exists(destination))throw new IOException("destination-exists");
            if(inventory.Directory) {
                Directory.CreateDirectory(destination);Restrict(destination);
                foreach(string relative in inventory.Directories){cancel.ThrowIfCancellationRequested();Directory.CreateDirectory(SafeChild(destination,relative));}
                foreach(var file in inventory.Files) {cancel.ThrowIfCancellationRequested();string src=SafeChild(source,file.RelativePath),dst=SafeChild(destination,file.RelativePath);EnsurePlain(src);EnsurePlain(dst);Directory.CreateDirectory(Path.GetDirectoryName(dst));File.Copy(src,dst,false);}
            }else {Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(source,destination,false);RestrictFile(destination);}
            Verify(destination,inventory,cancel);
        }
        private static void Verify(string root,CacheBackupTarget inventory,CancellationToken cancel) {
            if(inventory.Files==null||inventory.Directories==null||inventory.Files.Count>MaxFiles||inventory.Directories.Count>MaxFiles)throw new IOException("invalid-backup-manifest");
            EnsurePlain(root);
            if(inventory.Directory!=Directory.Exists(root))throw new IOException("content-changed");
            var current=Inventory(root,cancel,false);
            if(current.Files.Count!=inventory.Files.Count||current.Directories.Count!=inventory.Directories.Count)throw new IOException("content-changed");
            for(int i=0;i<current.Directories.Count;i++)if(!String.Equals(current.Directories[i],inventory.Directories[i],StringComparison.OrdinalIgnoreCase))throw new IOException("content-changed");
            for(int i=0;i<inventory.Files.Count;i++) {
                cancel.ThrowIfCancellationRequested();var expected=inventory.Files[i];var actual=current.Files[i];
                if(expected==null||!String.Equals(actual.RelativePath,expected.RelativePath,StringComparison.OrdinalIgnoreCase)||actual.Length!=expected.Length)throw new IOException("content-changed");
                string file=inventory.Directory?SafeChild(root,expected.RelativePath):root;
                if(String.IsNullOrEmpty(expected.Sha256)||HashFile(file)!=expected.Sha256)throw new IOException("content-changed");
            }
        }
        private void WriteManifest(string backup,CacheBackupManifest manifest) {
            string path=Path.Combine(backup,"manifest.json"),temporary=Path.Combine(backup,"manifest.pending");EnsurePlain(temporary);EnsurePlain(path);
            File.WriteAllText(temporary,json.Serialize(manifest),new UTF8Encoding(false));
            if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        }
        private static void RecycleWindows(string path,bool directory) {
            EnsurePlain(path);
            if(directory)FileSystem.DeleteDirectory(path,UIOption.OnlyErrorDialogs,RecycleOption.SendToRecycleBin,UICancelOption.ThrowException);
            else FileSystem.DeleteFile(path,UIOption.OnlyErrorDialogs,RecycleOption.SendToRecycleBin,UICancelOption.ThrowException);
        }
        private static void Restrict(string path) {
            var sid=WindowsIdentity.GetCurrent().User;if(sid==null)throw new IOException("backup-permissions-unavailable");
            var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);security.SetOwner(sid);
            security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            Directory.SetAccessControl(path,security);
        }
        private static void RestrictFile(string path) {
            var sid=WindowsIdentity.GetCurrent().User;if(sid==null)throw new IOException("backup-permissions-unavailable");
            var security=new FileSecurity();security.SetAccessRuleProtection(true,false);security.SetOwner(sid);security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,AccessControlType.Allow));File.SetAccessControl(path,security);
        }
        private static void RequireRoot(string path){if(String.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path)||Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar)==Path.GetPathRoot(path).TrimEnd(Path.DirectorySeparatorChar))throw new ArgumentException("Unsafe root");EnsurePlain(path);}
        private static bool Under(string root,string path){return !String.IsNullOrEmpty(path)&&Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
        private static string SafeChild(string root,string relative){if(String.IsNullOrEmpty(relative)||Path.IsPathRooted(relative)||relative.Split('/','\\').Any(x=>x==".."||x=="."||x.Length==0))throw new IOException("invalid-relative-path");string full=Path.GetFullPath(Path.Combine(root,relative));if(!Under(root,full))throw new IOException("path-outside-root");EnsurePlain(full);return full;}
        private static void EnsurePlain(string path) {
            if(String.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path)||path.StartsWith("\\\\",StringComparison.Ordinal))throw new IOException("unsafe-path");
            string tail=path.Substring(Path.GetPathRoot(path).Length);
            foreach(string part in tail.Split(new[]{'\\','/'},StringSplitOptions.RemoveEmptyEntries)) {
                if(part=="."||part==".."||part.EndsWith(".",StringComparison.Ordinal)||part.EndsWith(" ",StringComparison.Ordinal)||part.IndexOf(':')>=0||System.Text.RegularExpressions.Regex.IsMatch(part.Split('.')[0],"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))throw new IOException("unsafe-path");
            }
            string current=Path.GetFullPath(path);
            while(!String.IsNullOrEmpty(current)) {
                FileAttributes attributes;
                try{attributes=File.GetAttributes(current);}catch(FileNotFoundException){current=Path.GetDirectoryName(current);continue;}catch(DirectoryNotFoundException){current=Path.GetDirectoryName(current);continue;}
                if((attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("reparse-point-skipped");
                if((attributes&FileAttributes.Directory)==0)using(var stream=new FileStream(current,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                    NativeFileInfo info;if(!GetFileInformationByHandle(stream.SafeFileHandle,out info))throw new IOException("file-identity-unavailable");
                    if(info.NumberOfLinks!=1)throw new IOException("hardlink-skipped");
                }
                current=Path.GetDirectoryName(current);
            }
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativeFileInfo {
            public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;
            public uint VolumeSerial,SizeHigh,SizeLow,NumberOfLinks,IndexHigh,IndexLow;
        }
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle,out NativeFileInfo information);
        private static string HashFile(string path){EnsurePlain(path);using(var sha=SHA256.Create())using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        private static string HashText(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
        private static string SafeError(Exception error) {
            string[] codes={"changed-since-audit","source-missing","scan-limit","close-claude-first","recycle-not-completed","invalid-backup-location","invalid-backup-manifest","invalid-relative-path","path-outside-root","reparse-point-skipped","content-changed","destination-exists","backup-permissions-unavailable","unsafe-path","hardlink-skipped","file-identity-unavailable","backup-overlaps-cache"};
            if(codes.Contains(error.Message))return error.Message;
            if(error is UnauthorizedAccessException)return "access-denied";
            if(error is PathTooLongException)return "path-too-long";
            if(error is IOException)return "file-busy-or-io-error";
            return "operation-failed";
        }
    }
}
