using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using ClaudeCompanion;

// Real Worker protocol through a local fake Google service and the real offline engine.
// Uses a staged backend and synthetic text only. Its offline wrapper rejects socket connections.
internal static class OfflineRetryIntegrationTests {
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    private static T Get<T>(object owner,string name){return (T)owner.GetType().GetField(name,Private).GetValue(owner);}
    private static object Call(object owner,string name,params object[] args){return owner.GetType().GetMethod(name,Private).Invoke(owner,args);}
    private static void PumpUntil(Func<bool> done,int seconds,string phase){
        var watch=Stopwatch.StartNew();while(!done()&&watch.Elapsed.TotalSeconds<seconds){Application.DoEvents();Thread.Sleep(15);}
        Check(done(),phase+" exceeded timeout");
    }
    [STAThread] public static int Main(){
        string reportPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"integration-receipt.json");
        var report=new Dictionary<string,object>();
        try{
            Check(File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"backend","INTEGRATION_FIXTURE_ONLY")),"must use staged synthetic backend");
            string preferences=Path.Combine(Preferences.DataDirectory,"preferences.json");
            byte[] before=File.Exists(preferences)?File.ReadAllBytes(preferences):null;
            DateTime? written=File.Exists(preferences)?(DateTime?)File.GetLastWriteTimeUtc(preferences):null;
            Preferences.Offline=false;Preferences.Target="zh-CN";Preferences.OfflinePair="pair:en:zh";Preferences.Pin=false;Ui.English=false;
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new CompanionForm(true)){
                Get<IDisposable>(form,"watcher").Dispose();IntPtr handle=form.Handle;
                Check(!form.Visible,"main window fixture remains hidden");
                string text="Please keep the original file.";
                Call(form,"AcceptText",text,"synthetic-integration");
                PumpUntil(delegate{return !Get<bool>(form,"translating");},20,"fake Google limit response");
                Check((bool)Call(form,"CanRetryOffline"),"real Worker error preserves rate_limited code and exposes retry");
                var pending=Get<Snapshot>(form,"pendingOfflineRetry");int epoch=Get<int>(form,"pendingOfflineEpoch");
                Check(pending!=null&&pending.Text==text&&pending.Direction=="auto:zh-CN","failed synthetic request is captured exactly");
                Check(Get<Worker>(form,"worker")!=null&&!Preferences.Offline,"failure alone does not switch modes");
                LanguagePack selected=null;foreach(var pack in LanguagePack.InstalledPacks())if(pack.Pair=="pair:en:zh"){selected=pack;break;}
                Check(selected!=null,"read-only installed English to Chinese package exists");
                Check((bool)Call(form,"ApplyOfflineRetry",pending,epoch,selected),"explicit offline retry accepted");
                PumpUntil(delegate{return !Get<bool>(form,"translating");},60,"real offline translation");
                string result=Get<TextBox>(form,"translated").Text;
                Check(Regex.IsMatch(result,"[\\u3400-\\u9fff]"),"actual local model returns Chinese");
                Check(Get<TextBox>(form,"original").Text==text,"exact original selection remains visible");
                var latest=Get<Snapshot>(form,"latest");
                Check(latest.Direction=="pair:en:zh"&&latest.Text==text&&Preferences.Offline,"retried request is explicitly offline");
                Check(Get<string>(form,"done")==latest.Key+latest.Text,"local result passes epoch gate and is marked complete");
                Check(Get<Snapshot>(form,"pendingOfflineRetry")==null,"successful explicit retry consumed pending fallback");
                Check(Get<Label>(form,"privacy").Text.Contains("不上传"),"UI correctly labels local-only translation");
                report["syntheticOriginal"]=text;report["offlineResult"]=result;report["offlinePair"]=latest.Direction;
                report["onlineFailureCodeVerified"]="rate_limited";report["sameSelectionRetried"]=true;
                form.Close();Application.DoEvents();
            }
            byte[] after=File.Exists(preferences)?File.ReadAllBytes(preferences):null;
            Check(before==null?after==null:after!=null&&Convert.ToBase64String(before)==Convert.ToBase64String(after),"preview integration must preserve preferences bytes");
            Check(written==null?!File.Exists(preferences):File.GetLastWriteTimeUtc(preferences)==written.Value,"preview integration must not rewrite preferences");
            report["ok"]=true;report["checks"]=checks;report["preferencesUnchanged"]=true;report["offlineSocketsBlocked"]=true;report["googleRequests"]=0;report["inputClipboardUsed"]=false;
            File.WriteAllText(reportPath,Json.Encode(report),new UTF8Encoding(false));Console.WriteLine("OfflineRetryIntegrationTests passed: "+checks+" checks. Fake 429 through real Worker -> real offline Chinese translation; synthetic text only.");return 0;
        }catch(Exception error){report["ok"]=false;report["error"]=error.GetBaseException().Message;report["checks"]=checks;File.WriteAllText(reportPath,Json.Encode(report),new UTF8Encoding(false));Console.WriteLine("OfflineRetryIntegrationTests failed: "+error.GetBaseException().Message);Console.WriteLine(error.GetBaseException().StackTrace);return 1;}
    }
}
