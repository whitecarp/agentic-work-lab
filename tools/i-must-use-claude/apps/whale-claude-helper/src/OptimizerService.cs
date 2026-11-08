using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace WhaleClaudeHelper {
    internal sealed class OptimizationResult {
        public bool Ok;
        public string OperationDirectory,Summary;
        public List<string> Details=new List<string>();
    }
    internal sealed class OptimizationReceipt {
        public int Format=1;
        public string Kind="whale-fixed-local-optimizer",Preset="en-US-UTC-user-fonts-v1",OperationId,CreatedUtc,State;
        public List<OptimizationStep> Steps=new List<OptimizationStep>();
        public string BaselineBrowser,BaselineUtc;
        public int? BaselineScore;
        public int BaselineMeasuredWeight;
    }
    internal sealed class OptimizationStep {
        public string Id,Kind,Label,State="pending",BackupName,UndoState="none";
        public bool Ok;
        public List<string> Details=new List<string>();
    }

    // Fixed local-only preset: no Claude credentials, connection settings, caches,
    // network adapters, proxies, IPs or browser identity are read or changed here.
    // New orchestrator: static review only; never run against the user's settings.
    internal sealed class OptimizerService {
        private static readonly object Gate=new object();
        private readonly string operationRoot;
        private static readonly Encoding Utf8=new UTF8Encoding(false,true);
        private static readonly HashSet<string> SignalFamilies=new HashSet<string>(new[]{
            "Microsoft YaHei","Microsoft YaHei UI","SimSun","NSimSun","SimHei","KaiTi","FangSong","DengXian","PingFang SC","Hiragino Sans GB","STHeiti","STSong","Songti SC","Source Han Sans CN","Source Han Sans SC","Noto Sans CJK SC","Noto Serif CJK SC","WenQuanYi Micro Hei","WenQuanYi Zen Hei",
            "Microsoft JhengHei","PMingLiU","MingLiU","DFKai-SB","PingFang TC","PingFang HK","Source Han Sans TW","Noto Sans CJK TC",
            "MiSans","MIUI","HarmonyOS Sans SC","HarmonyOS Sans","HONOR Sans","OPPO Sans","vivo Sans","Alibaba PuHuiTi","Alibaba Sans","DingTalk JinBuTi","Douyin Sans","HYQiHei","FZShuSong-Z01S","FZKai-Z03S","FZHei-B01S","FZFangSong-Z02S"
        },StringComparer.OrdinalIgnoreCase);

        public OptimizerService():this(null){}
        internal OptimizerService(string dataRoot){
            string root=String.IsNullOrWhiteSpace(dataRoot)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DafeiyuClaudeHelper"):dataRoot;
            operationRoot=PlainAbsolute(Path.Combine(root,"Backups","Optimization"));
        }
        public bool HasUndo {get{return FindLatest()!=null;}}

        public OptimizationResult Optimize(ScoreReport baseline){
            lock(Gate){
                var result=new OptimizationResult();OptimizationReceipt receipt=null;string operation=null;
                if(baseline==null||!baseline.CanOptimize){result.Summary="optimization_not_started";result.Details.Add("请先完成检测；本地可处理项目必须已测，且结果不超过10分钟。 / Measure first. Actionable checks must be available and less than 10 minutes old.");return result;}
                bool changeTime=baseline.Hit("timezone")||baseline.Hit("timezoneOffset");
                bool languageHit=baseline.Hit("language")||baseline.Hit("intlLocale");
                bool changeLanguage=languageHit&&(baseline.BrowserId=="Chrome"||baseline.BrowserId=="Edge");
                var measuredFonts=new HashSet<string>(baseline.Get("fonts").Fonts.Concat(baseline.Get("vendorFonts").Fonts),StringComparer.OrdinalIgnoreCase);
                if(!baseline.Hit("fonts")&&!baseline.Hit("vendorFonts"))measuredFonts.Clear();
                if(languageHit&&!changeLanguage)result.Details.Add("当前浏览器暂不支持自动语言调整，保留原设置。 / Automatic language changes are unsupported for this browser; settings retained.");
                if(!changeTime&&!changeLanguage&&measuredFonts.Count==0){result.Ok=true;result.Summary="optimization_complete";result.Details.Add("没有可自动处理的命中项，未修改设置。 / No actionable matches. No settings changed.");return result;}
                using(var mutex=OperationMutex()){
                    bool held=false;
                    try{
                        held=Acquire(mutex);if(!held)throw new InvalidOperationException("operation_already_running");
                        RequireClosed();
                        var inventoryService=new FontService();var inventory=inventoryService.ListFonts();
                        // Registry labels alone are insufficient: require the actual font metadata
                        // to contain exactly one family, with an exact allowlist/name match.
                        var targets=inventory.Where(x=>x.IsUser&&x.CanRemove&&measuredFonts.Contains(NormalizeFamily(x.Name))&&inventoryService.IsExactSignalFamily(x,measuredFonts)).ToList();
                        var retained=inventory.Where(x=>MatchesSignal(x.Name)&&!targets.Contains(x)).ToList();
                        bool fontAuditIncomplete=inventory.Any(x=>x.Hint=="font_inventory_unavailable");
                        RequireClosed();
                        operation=CreateOperation();result.OperationDirectory=operation;
                        receipt=new OptimizationReceipt{OperationId=Path.GetFileName(operation),CreatedUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture),State="running",BaselineBrowser=baseline.BrowserId,BaselineUtc=baseline.CollectedUtc.ToString("o"),BaselineScore=baseline.IsComplete?(int?)baseline.MeasuredScore:null,BaselineMeasuredWeight=baseline.MeasuredWeight};
                        receipt.Steps.Add(NewStep(0,"timezone","UTC"));
                        receipt.Steps.Add(NewStep(1,"browser",baseline.BrowserId+": en-US"));
                        foreach(var target in targets)receipt.Steps.Add(NewStep(receipt.Steps.Count,"font",NormalizeFamily(target.Name)));
                        Save(operation,receipt);
                        result.Details.Add("只处理实测命中项：时区、被测浏览器语言及确切匹配个人字体。 / Only measured matches: timezone, measured browser language and verified personal fonts.");
                        result.Details.Add("网络、IP、代理、Claude 配置和登录数据不修改。 / Network, IP, proxy, Claude settings and login data stay unchanged.");
                        result.Details.Add("保留的匹配字体："+retained.Count+"；系统字体和不明确的注册关系不处理。 / Retained matching fonts: "+retained.Count+"; system fonts and ambiguous registrations stay unchanged.");
                        if(targets.Count==0)result.Details.Add("没有符合条件的用户字体。 / No eligible matching user fonts.");
                        if(fontAuditIncomplete)result.Details.Add("字体清单读取不完整，未推测或扩大移除范围。 / Font inventory incomplete; no removal targets were guessed.");
                        bool halted=false;
                        for(int i=0;i<receipt.Steps.Count;i++){
                            var step=receipt.Steps[i];
                            if((i==0&&!changeTime)||(i==1&&!changeLanguage)){step.State="skipped";step.Ok=true;step.Details.Add("检测未命中或不支持，无须修改。 / No measured match or unsupported; unchanged.");AddStepResult(result,step);continue;}
                            if(halted){step.State="skipped";step.Ok=false;step.Details.Add("前序安全检查或记录失败，未执行。 / Not run after a safety or receipt failure.");AddStepResult(result,step);continue;}
                            try{
                                RequireClosed();step.State="started";Save(operation,receipt);
                                string root=StepRoot(operation,step);
                                if(step.Kind=="timezone"){
                                    var changed=new TimeZoneChangeService(new WindowsTimeZonePlatform(),root).Apply("UTC");
                                    step.Ok=changed.Ok;step.Details.Add(changed.Message+" / "+changed.MessageEnglish);LinkBackup(operation,step,changed.BackupDirectory);
                                }else if(step.Kind=="browser"){
                                    var changed=new BrowserLocaleService(root).ApplyForBrowser(baseline.BrowserId);
                                    step.Ok=changed.Ok;step.Details.AddRange(changed.Details);LinkBackup(operation,step,changed.BackupDirectory);
                                }else{
                                    var service=Fonts(root);var target=targets[i-2];
                                    if(!service.IsExactSignalFamily(target,measuredFonts))throw new InvalidOperationException("font_target_changed_or_ambiguous");
                                    var changed=service.Remove(target);
                                    step.Ok=changed.Ok;step.Details.Add(changed.Message);LinkBackup(operation,step,changed.BackupDirectory);
                                    if(!changed.Ok&&changed.Message=="font_remove_failed_no_loss")step.UndoState="none";
                                }
                                step.State=step.Ok?"complete":"failed";Save(operation,receipt);
                            }catch(Exception error){
                                step.Ok=false;step.State="failed";step.Details.Add(SafeError(error));
                                try{RecoverBackup(operation,step);Save(operation,receipt);}catch{step.Details.Add("receipt_update_failed_backup_retained");}
                                halted=true;
                            }
                            AddStepResult(result,step);
                        }
                        result.Ok=receipt.Steps.All(x=>x.Ok)&&!fontAuditIncomplete;
                        receipt.State=result.Ok?"complete":"partial";Save(operation,receipt);
                        result.Summary=result.Ok?"optimization_complete":"optimization_partial";
                        result.Details.Add("这是本地变更，不是网页实测分数；重开浏览器后复查。不能保证归零或账号可用。 / Local changes are not a measured website score. Reopen and recheck; no zero-score or account-access guarantee.");
                    }catch(Exception error){
                        result.Ok=false;result.Summary=operation==null?"optimization_not_started":"optimization_partial";result.Details.Add(SafeError(error));
                        if(receipt!=null)try{receipt.State="partial";Save(operation,receipt);}catch{result.Details.Add("receipt_update_failed_backup_retained");}
                    }finally{if(held)mutex.ReleaseMutex();}
                }
                return result;
            }
        }

        public OptimizationResult UndoLast(){
            lock(Gate){
                var result=new OptimizationResult();
                using(var mutex=OperationMutex()){
                    bool held=false;
                    try{
                        held=Acquire(mutex);if(!held)throw new InvalidOperationException("operation_already_running");
                        RequireClosed();string operation=FindLatest();
                        if(operation==null){result.Ok=true;result.Summary="nothing_to_undo";return result;}
                        result.OperationDirectory=operation;var receipt=Read(operation);bool ok=true,halted=false;
                        foreach(var step in receipt.Steps.AsEnumerable().Reverse()){
                            if(step.UndoState=="done"){result.Details.Add(step.Label+": 已撤回 / "+step.Label+": already undone");continue;}
                            if(halted){ok=false;result.Details.Add(step.Label+": 安全检查未通过，未执行撤销 / "+step.Label+": undo skipped after a safety check failed");continue;}
                            try{
                                RequireClosed();RecoverBackup(operation,step);
                                if(step.UndoState=="none"||String.IsNullOrEmpty(step.BackupName)){result.Details.Add(step.Label+": 无须恢复 / "+step.Label+": no changes to restore");continue;}
                                string root=StepRoot(operation,step),backup=BackupPath(operation,step);bool restored;var detail=new List<string>();
                                if(step.Kind=="timezone"){
                                    var r=new TimeZoneChangeService(new WindowsTimeZonePlatform(),root).Restore(backup);restored=r.Ok;detail.Add(r.Message+" / "+r.MessageEnglish);
                                }else if(step.Kind=="browser"){
                                    var r=new BrowserLocaleService(root).Restore(backup);restored=r.Ok;detail.AddRange(r.Details);
                                }else{
                                    var r=Fonts(root).Restore(backup);restored=r.Ok;detail.Add(r.Message);
                                }
                                step.UndoState=restored?"done":"conflict";step.Details.AddRange(detail);ok&=restored;Save(operation,receipt);
                                result.Details.Add(step.Label+": "+(restored?"已撤销":"未完整撤销")+" / "+step.Label+": "+(restored?"restored":"not fully restored"));result.Details.AddRange(detail);
                            }catch(Exception error){ok=false;step.UndoState="conflict";result.Details.Add(step.Label+": "+SafeError(error));try{Save(operation,receipt);}catch{}halted=true;}
                        }
                        receipt.State=ok?"undone":"undo-partial";Save(operation,receipt);result.Ok=ok;result.Summary=ok?"optimization_undone":"undo_partial";
                        result.Details.Add("后续修改有冲突时保留；备份不会删除。 / Later edits are retained on conflict. Backups are kept.");
                    }catch(Exception error){result.Ok=false;result.Summary="undo_not_completed";result.Details.Add(SafeError(error));}
                    finally{if(held)mutex.ReleaseMutex();}
                }
                return result;
            }
        }

        private string FindLatest(){
            try{
                PlainAbsolute(operationRoot);if(!Directory.Exists(operationRoot))return null;
                foreach(string path in Directory.EnumerateDirectories(operationRoot,"opt-*").OrderByDescending(x=>Path.GetFileName(x),StringComparer.Ordinal).Take(1000)){
                    try{ValidateOperation(path);}catch{continue;}
                    try{var receipt=Read(path);if(receipt.State!="undone"&&receipt.Steps.Any(x=>x.UndoState=="pending"||x.UndoState=="conflict"||x.State=="started"||HasUnlinkedBackup(path,x)))return path;}
                    catch{
                        // An unreadable newest receipt must not silently make Undo target an
                        // older operation. Return this candidate; UndoLast will fail closed.
                        if(File.Exists(Path.Combine(path,"optimization.json"))||Directory.Exists(Path.Combine(path,"steps")))return path;
                    }
                }
            }catch{}
            return null;
        }
        private static OptimizationStep NewStep(int index,string kind,string label){return new OptimizationStep{Id=index.ToString("D3",CultureInfo.InvariantCulture)+"-"+kind,Kind=kind,Label=label};}
        private static FontService Fonts(string root){return new FontService(new FontContext{BackupRoot=root},new WindowsFontPlatform());}
        private static string NormalizeFamily(string name){
            if(name==null)return "";string value=name.Trim();
            foreach(string suffix in new[]{" (TrueType)"," (OpenType)"})if(value.EndsWith(suffix,StringComparison.OrdinalIgnoreCase))return value.Substring(0,value.Length-suffix.Length).Trim();
            return value;
        }
        internal static bool MatchesSignal(string registryName){return SignalFamilies.Contains(NormalizeFamily(registryName));}
        private static void AddStepResult(OptimizationResult result,OptimizationStep step){result.Details.Add(step.Label+": "+(step.Ok?"已完成":"未完整完成")+" / "+step.Label+": "+(step.Ok?"completed":"not fully completed"));result.Details.AddRange(step.Details.Where(x=>!String.IsNullOrWhiteSpace(x)));}
        private static void RequireClosed(){
            var active=new List<string>();
            foreach(string name in new[]{"chrome","msedge","Claude"})foreach(var process in Process.GetProcessesByName(name))using(process)active.Add(name+" (PID "+process.Id.ToString(CultureInfo.InvariantCulture)+")");
            if(active.Count>0)throw new InvalidOperationException("close_browsers_and_claude_first");
        }
        private static Mutex OperationMutex(){return new Mutex(false,"Local\\Dafeiyu-ClaudeOptimizer-"+WindowsIdentity.GetCurrent().User.Value);}
        private static bool Acquire(Mutex mutex){try{return mutex.WaitOne(0);}catch(AbandonedMutexException){return true;}}
        private string CreateOperation(){
            PlainAbsolute(operationRoot);Directory.CreateDirectory(operationRoot);PlainAbsolute(operationRoot);
            string folder=Path.Combine(operationRoot,"opt-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N"));
            var security=new DirectorySecurity();var sid=WindowsIdentity.GetCurrent().User;security.SetAccessRuleProtection(true,false);security.SetOwner(sid);
            security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));Directory.CreateDirectory(folder,security);PlainAbsolute(folder);return folder;
        }
        private string ValidateOperation(string operation){
            string full=PlainAbsolute(operation);
            if(!String.Equals(Path.GetDirectoryName(full),operationRoot,StringComparison.OrdinalIgnoreCase)||!Regex.IsMatch(Path.GetFileName(full),"^opt-[0-9]{8}-[0-9]{6}-[0-9]{7}-[a-f0-9]{32}$"))throw new InvalidDataException("invalid_operation_directory");
            return full;
        }
        private static string StepRoot(string operation,OptimizationStep step){return PlainAbsolute(Path.Combine(operation,"steps",step.Id));}
        private static string BackupPath(string operation,OptimizationStep step){
            if(String.IsNullOrEmpty(step.BackupName)||step.BackupName!=Path.GetFileName(step.BackupName)||step.BackupName=="."||step.BackupName=="..")throw new InvalidDataException("invalid_step_backup");
            string root=StepRoot(operation,step),path=PlainAbsolute(Path.Combine(root,step.BackupName));
            if(!String.Equals(Path.GetDirectoryName(path),root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("invalid_step_backup");return path;
        }
        private static void LinkBackup(string operation,OptimizationStep step,string backup){
            if(String.IsNullOrEmpty(backup))return;string full=PlainAbsolute(backup);
            if(!String.Equals(Path.GetDirectoryName(full),StepRoot(operation,step),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("invalid_step_backup");
            step.BackupName=Path.GetFileName(full);step.UndoState="pending";BackupPath(operation,step);
        }
        private static bool HasUnlinkedBackup(string operation,OptimizationStep step){
            if(!String.IsNullOrEmpty(step.BackupName)||step.State=="pending"||step.State=="skipped")return false;
            string root=StepRoot(operation,step);return Directory.Exists(root)&&Directory.EnumerateDirectories(root).Any();
        }
        private static void RecoverBackup(string operation,OptimizationStep step){
            if(!String.IsNullOrEmpty(step.BackupName)){BackupPath(operation,step);return;}
            if(!HasUnlinkedBackup(operation,step))return;
            string root=StepRoot(operation,step);var children=Directory.GetDirectories(root);
            if(children.Length!=1)throw new InvalidDataException("ambiguous_step_backup");LinkBackup(operation,step,children[0]);
        }
        private OptimizationReceipt Read(string operation){
            operation=ValidateOperation(operation);string path=PlainAbsolute(Path.Combine(operation,"optimization.json"));
            if(!File.Exists(path)||new FileInfo(path).Length>1048576)throw new InvalidDataException("invalid_operation_receipt");
            string text=File.ReadAllText(path,Utf8);LosslessJson.Parse(text);
            var receipt=Serializer().Deserialize<OptimizationReceipt>(text);
            if(receipt==null||receipt.Format!=1||receipt.Kind!="whale-fixed-local-optimizer"||receipt.Preset!="en-US-UTC-user-fonts-v1"||receipt.OperationId!=Path.GetFileName(operation)||receipt.Steps==null||receipt.Steps.Count<2||receipt.Steps.Count>1000||!new[]{"running","complete","partial","undone","undo-partial"}.Contains(receipt.State))throw new InvalidDataException("invalid_operation_receipt");
            for(int i=0;i<receipt.Steps.Count;i++){
                var step=receipt.Steps[i];string kind=i==0?"timezone":i==1?"browser":"font";
                if(step==null||step.Kind!=kind||step.Id!=i.ToString("D3",CultureInfo.InvariantCulture)+"-"+kind||String.IsNullOrWhiteSpace(step.Label)||step.Label.Length>512||step.Details==null||step.Details.Count>1000||!new[]{"pending","started","complete","failed","skipped"}.Contains(step.State)||!new[]{"none","pending","done","conflict"}.Contains(step.UndoState))throw new InvalidDataException("invalid_operation_receipt");
                if(kind=="font"&&!SignalFamilies.Contains(step.Label))throw new InvalidDataException("invalid_font_target");
                if(!String.IsNullOrEmpty(step.BackupName))BackupPath(operation,step);
            }
            return receipt;
        }
        private void Save(string operation,OptimizationReceipt receipt){
            operation=ValidateOperation(operation);string path=PlainAbsolute(Path.Combine(operation,"optimization.json"));
            byte[] data=Utf8.GetBytes(Serializer().Serialize(receipt));if(data.Length>1048576)throw new InvalidDataException("operation_receipt_too_large");
            string temporary=Path.Combine(operation,"optimization-"+Guid.NewGuid().ToString("N")+".tmp");
            var security=new FileSecurity();var sid=WindowsIdentity.GetCurrent().User;security.SetAccessRuleProtection(true,false);security.SetOwner(sid);security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,AccessControlType.Allow));
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileSystemRights.FullControl,FileShare.None,4096,FileOptions.None,security)){stream.Write(data,0,data.Length);stream.Flush(true);}
            PlainAbsolute(path);if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        }
        private static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=12};}
        private static string PlainAbsolute(string path){
            if(String.IsNullOrWhiteSpace(path)||!Regex.IsMatch(path,@"^[A-Za-z]:[\\/]"))throw new InvalidDataException("invalid_local_path");
            string full=Path.GetFullPath(path);if(full.Substring(2).Contains(":"))throw new InvalidDataException("invalid_local_path");
            foreach(string component in full.Substring(Path.GetPathRoot(full).Length).Split(new[]{'\\','/'},StringSplitOptions.RemoveEmptyEntries))if(component.EndsWith(".",StringComparison.Ordinal)||component.EndsWith(" ",StringComparison.Ordinal))throw new InvalidDataException("invalid_local_path");
            RepairEngine.AssertPlainPath(full);return full.Length>3?full.TrimEnd('\\','/'):full;
        }
        private static string SafeError(Exception error){
            if(error is InvalidOperationException||error is InvalidDataException)if(Regex.IsMatch(error.Message??"","^[a-z_]{1,80}$"))return error.Message;
            if(error is UnauthorizedAccessException)return "access_denied";if(error is IOException)return "file_operation_failed";return "operation_failed";
        }
    }
}
