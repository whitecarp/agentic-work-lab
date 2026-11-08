using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using ClaudeCompanion;

// Local fixtures only. Compile outside the application directory without a backend.
internal static class ComposeOfflineRetryTests {
    private static int checks;
    private static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
    private static object Field(object owner,string name){return owner.GetType().GetField(name,Fields).GetValue(owner);}
    private static void Put(object owner,string name,object value){owner.GetType().GetField(name,Fields).SetValue(owner,value);}
    private static object Call(object owner,string name,params object[] args){return owner.GetType().GetMethod(name,Fields).Invoke(owner,args);}
    private static ComposeForm Form(){var form=new ComposeForm(new WindowChoice{Handle="0",Title="Fixture"},true);IntPtr handle=form.Handle;return form;}
    private static void Pack(string root,string from,string to){
        string id="translate-"+from+"_"+to+"-1_9",folder=Path.Combine(root,"packages",id);
        Directory.CreateDirectory(Path.Combine(folder,"model"));
        File.WriteAllText(Path.Combine(folder,"metadata.json"),Json.Encode(new{type="translate",from_code=from,to_code=to,from_name=from,to_name=to,package_version="1.9"}),new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(folder,".install.json"),Json.Encode(new{id=id,complete=true}),new UTF8Encoding(false));
        foreach(string path in new[]{"sentencepiece.model","model/model.bin","model/config.json","model/shared_vocabulary.json"})File.WriteAllText(Path.Combine(folder,path.Replace('/',Path.DirectorySeparatorChar)),"fixture");
    }
    [STAThread] public static int Main(){
        string priorRoot=Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR");bool priorOffline=Preferences.Offline,priorEnglish=Ui.English;
        string root=Path.Combine(Path.GetTempPath(),"dfy-compose-offline-retry-"+Guid.NewGuid().ToString("N"));
        try{
            Check(!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"backend","launcher.json")),"runner must be isolated from backend");
            Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR",root);Preferences.Offline=false;Ui.English=false;
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var form=Form()){
                var input=(TextBox)Field(form,"chinese");input.Text="请保留原始文件。";int revision=(int)Field(form,"revision");
                Check(!(bool)Call(form,"CanRetryOffline"),"no retry offered before Google failure");
                Check(!(bool)Call(form,"RecordOfflineRetry",input.Text,revision,false,new InvalidOperationException(Worker.ErrorMessage("rate_limited"))),"localized message alone cannot trigger retry");
                Check(!(bool)Call(form,"RecordOfflineRetry",input.Text,revision,false,new TranslationFailure("network_error")),"ordinary error does not trigger rate-limit retry");
                Check(!(bool)Call(form,"RecordOfflineRetry",input.Text,revision,true,new TranslationFailure("rate_limited")),"offline request cannot trigger Google retry");
                Check(!(bool)Call(form,"RecordOfflineRetry",input.Text,revision-1,false,new TranslationFailure("rate_limited")),"obsolete revision cannot offer retry");
                Check((bool)Call(form,"RecordOfflineRetry",input.Text,revision,false,new TranslationFailure("rate_limited")),"current rate limited request offers retry");
                Check((bool)Call(form,"CanRetryOffline"),"matching current original can retry");
                Check(Field(Field(form,"worker"),"process")==null,"offering retry never starts any worker");
                Check(!(bool)Call(form,"ApplyOfflineRetry",input.Text,revision),"missing pack cannot switch or translate");
                Check(!(bool)Field(form,"offline")&&!(bool)Preferences.Offline,"missing pack preserves online mode");
                Pack(root,"en","zh");
                Check(!(bool)Call(form,"ApplyOfflineRetry",input.Text,revision),"reverse direction package cannot satisfy Chinese to English");
                Pack(root,"zh","en");
                Put(form,"sending",true);Check(!(bool)Call(form,"CanRetryOffline"),"sending cannot be interrupted by retry");Put(form,"sending",false);
                Put(form,"busy",true);Check(!(bool)Call(form,"CanRetryOffline"),"active translation cannot be interrupted by retry");Put(form,"busy",false);
                Check(!(bool)Call(form,"ApplyOfflineRetry","different original",revision),"retry refuses a different failed original");
                Check((bool)Call(form,"ApplyOfflineRetry",input.Text,revision),"explicit valid retry accepts exact installed direction");
                Check((bool)Field(form,"offline"),"compose switches locally to offline");
                Check(!Preferences.Offline,"compose retry does not silently change the main window mode");
                Check(input.Text=="请保留原始文件。","retry retains exact original");
                Check(Field(form,"pendingRetryText")==null,"retry consumes pending failure");
                Check(Field(Field(form,"worker"),"process")==null&&Field(form,"helper")==null,"mode transition neither starts network nor sends a message");
                Check(!((Button)Field(form,"send")).Enabled,"retry cannot automatically send to Claude");
                Ui.English=true;Call(form,"RefreshLocalizedText");
                Check(((Label)Field(form,"subtitleLabel")).Text.Contains("Offline"),"English UI reflects local mode");
                Check(((Button)Field(form,"offlineRetry")).Text=="Use offline","retry button is bilingual");
                form.Close();
            }
            Preferences.Offline=false;
            using(var form=Form()){
                var input=(TextBox)Field(form,"chinese");input.Text="旧文本";int revision=(int)Field(form,"revision");
                Check((bool)Call(form,"RecordOfflineRetry",input.Text,revision,false,new TranslationFailure("google_blocked")),"verification block also offers offline retry");
                input.Text="新文本";
                Check(Field(form,"pendingRetryText")==null&&!(bool)Call(form,"CanRetryOffline"),"editing original invalidates pending retry");
                input.Text="旧文本";
                Check(!(bool)Call(form,"RecordOfflineRetry",input.Text,revision,false,new TranslationFailure("rate_limited")),"typing original back cannot resurrect old request");
                Check(Field(form,"helper")==null,"editing and retry gates do not send");form.Close();
            }
            Check(!File.Exists(Path.Combine(root,"preferences.json")),"preview tests never modify saved preferences");
            Console.WriteLine("ComposeOfflineRetryTests passed: "+checks+" checks; no network, model loading, clipboard or sending.");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex.GetBaseException());return 1;}
        finally{Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR",priorRoot);Preferences.Offline=priorOffline;Ui.English=priorEnglish;}
    }
}
