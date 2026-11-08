using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using ClaudeCompanion;

// Run from an isolated directory without backend/launcher.json. No network, clipboard or user text.
internal static class OfflineRetryTests {
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    private static string root;
    private static int checks;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    private static T Get<T>(object owner,string name){return (T)owner.GetType().GetField(name,Private).GetValue(owner);}
    private static void Put(object owner,string name,object value){owner.GetType().GetField(name,Private).SetValue(owner,value);}
    private static object Call(object owner,string name,params object[] args){return owner.GetType().GetMethod(name,Private).Invoke(owner,args);}
    private static bool Can(CompanionForm form){return (bool)Call(form,"CanRetryOffline");}
    private static bool Record(CompanionForm form,Snapshot shot,int epoch,bool offline,Exception error){return (bool)Call(form,"RecordOfflineRetry",shot,epoch,offline,error);}
    private static void Idle(CompanionForm form,string phase){Check(Get<object>(Get<Worker>(form,"worker"),"process")==null,phase+" must not start any backend");}
    private static CompanionForm Form(string name){
        Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR",Path.Combine(root,name));
        Preferences.Offline=false;Preferences.Target="zh-CN";Preferences.OfflinePair="pair:en:zh";Preferences.Pin=false;
        var form=new CompanionForm(true);Get<IDisposable>(form,"watcher").Dispose();IntPtr handle=form.Handle;
        Check(!form.Visible,"fixture remains hidden");return form;
    }
    private static Snapshot Seed(CompanionForm form,string text="Please keep this exact selection.\n\n`KEEP_CODE`"){
        var shot=new Snapshot{Conversation="fixture-source",MessageId="7",Text=text,Direction="auto:zh-CN"};
        Put(form,"epoch",7);Put(form,"latest",shot);Put(form,"needsFreshSelection",false);
        Get<TextBox>(form,"original").Text=shot.Text;Get<TextBox>(form,"translated").Text="Prior result";return shot;
    }
    private static LanguagePack Package(string from,string to){
        var pack=new LanguagePack{Id="translate-"+from+"_"+to+"-1_9",From=from,To=to,FromName=from,ToName=to,Version="1.9",Installed=true};
        string path=Path.Combine(Preferences.DataDirectory,"packages",pack.Id);Directory.CreateDirectory(Path.Combine(path,"model"));
        File.WriteAllText(Path.Combine(path,"metadata.json"),Json.Encode(new{type="translate",from_code=from,to_code=to,from_name=from,to_name=to,package_version="1.9"}),Utf8);
        File.WriteAllText(Path.Combine(path,".install.json"),Json.Encode(new{id=pack.Id,complete=true}),Utf8);
        foreach(string file in new[]{"sentencepiece.model","model/model.bin","model/config.json","model/shared_vocabulary.json"})File.WriteAllText(Path.Combine(path,file.Replace('/',Path.DirectorySeparatorChar)),"fixture only",Utf8);
        return pack;
    }
    private static void FailureClassification(){
        foreach(bool english in new[]{false,true}){
            Ui.English=english;
            using(var form=Form("classify-"+english)){
                var shot=Seed(form);
                foreach(string code in new[]{"rate_limited","google_blocked"}){
                    var error=new TranslationFailure(code);Check(error.Code==code&&error is InvalidOperationException,"failure retains code and existing exception compatibility");
                    Check(Record(form,shot,7,false,error)&&Can(form),"limited and verification errors expose explicit retry");
                    Check(Object.ReferenceEquals(Get<Snapshot>(form,"pendingOfflineRetry"),shot),"retry retains the exact failed request");
                    Check(!Preferences.Offline,"recording an error never silently switches mode");Idle(form,"record failure");
                    Check(Get<Button>(form,"offlineRetry").Text==(english?"Use offline":"改用离线翻译"),"retry action is bilingual");
                    Call(form,"ClearOfflineRetry");Check(!Can(form)&&Get<Snapshot>(form,"pendingOfflineRetry")==null,"clear removes original from retry state");
                }
                foreach(string code in new[]{"network_error","access_denied","offline_model_missing","text_too_long","translation_failed"})Check(!Record(form,shot,7,false,new TranslationFailure(code)),"unrelated failure cannot offer limit recovery: "+code);
                Check(!Record(form,shot,7,false,new InvalidOperationException(Worker.ErrorMessage("rate_limited"))),"localized message text is not treated as an error code");
                Check(!Record(form,shot,7,true,new TranslationFailure("rate_limited")),"offline failure cannot create online fallback");
                Check(!Record(form,shot,6,false,new TranslationFailure("rate_limited")),"late failure epoch rejected");
                Check(!Record(form,new Snapshot{Conversation=shot.Conversation,MessageId=shot.MessageId,Text=shot.Text,Direction=shot.Direction},7,false,new TranslationFailure("rate_limited")),"another snapshot with same text rejected");
                form.Close();
            }
        }
    }
    private static void TargetFilteringAndDialog(){
        Ui.English=false;
        var packs=new List<LanguagePack>{new LanguagePack{From="en",To="zh",Installed=true},new LanguagePack{From="fr",To="zh",Installed=true},new LanguagePack{From="en",To="zt",Installed=true},new LanguagePack{From="zh",To="en",Installed=true},new LanguagePack{From="en",To="fr",Installed=true}};
        Check(OfflineRetryForm.ForTarget(packs,"auto:zh-CN").Count==2,"simplified Chinese maps to zh without choosing a source");
        Check(OfflineRetryForm.ForTarget(packs,"auto:zh-TW")[0].To=="zt","traditional Chinese maps only to zt");
        Check(OfflineRetryForm.ForTarget(packs,"auto:en")[0].Pair=="pair:zh:en","English target never reverses the pair");
        Check(OfflineRetryForm.ForTarget(packs,"auto:ja").Count==0,"missing target cannot silently substitute another target");
        Check(OfflineRetryForm.ForTarget(packs,"pair:en:zh").Count==0&&OfflineRetryForm.ForTarget(packs,null).Count==0,"non-Google direction rejected");
        foreach(bool english in new[]{false,true}){
            Ui.English=english;
            using(var empty=new OfflineRetryForm(new List<LanguagePack>())){
                Check(!Get<Button>(empty,"confirm").Enabled&&empty.SelectedPack==null,"missing pack cannot confirm translation");
                Check(!Get<ComboBox>(empty,"pairs").Enabled,"missing pack explains unavailable directions");
            }
            using(var dialog=new OfflineRetryForm(OfflineRetryForm.ForTarget(packs,"auto:zh-CN"))){
                var choices=Get<ComboBox>(dialog,"pairs");Check(choices.Items.Count==2,"multiple possible sources remain available to the user");
                choices.SelectedIndex=1;Check(dialog.SelectedPack.From=="fr"&&dialog.SelectedPack.To=="zh","user-selected source is preserved exactly");
                Check(Get<Button>(dialog,"confirm").Enabled,"installed candidates allow explicit confirmation");
            }
        }
    }
    private static void StaleRequestsAndClearing(){
        Ui.English=false;
        using(var form=Form("stale")){
            var shot=Seed(form);Record(form,shot,7,false,new TranslationFailure("rate_limited"));Put(form,"epoch",8);
            Check(!Can(form),"stale epoch cannot retry");Put(form,"epoch",7);Check(Can(form),"current matching state can retry");
            Put(form,"latest",new Snapshot{Text="new text",Direction="auto:zh-CN"});Check(!Can(form),"new selection cannot be replaced by stale failed text");Put(form,"latest",shot);
            Preferences.Offline=true;Check(!Can(form),"already switched offline cannot reuse Google retry");Preferences.Offline=false;
            Call(form,"Pause");Check(Can(form)&&Get<int>(form,"pendingOfflineEpoch")==Get<int>(form,"epoch"),"pausing and opening pack manager may retain explicit original retry");
            Ui.SetEnglish(true);Check(Can(form)&&Get<Button>(form,"offlineRetry").Text=="Use offline","language switch retains retry without a backend");Idle(form,"UI language change");
            Call(form,"UnlockSource",false);Check(!Can(form)&&Get<Snapshot>(form,"pendingOfflineRetry")==null,"source unlock discards pending retry");form.Close();
        }
        using(var form=Form("target-change")){
            var shot=Seed(form);Record(form,shot,7,false,new TranslationFailure("rate_limited"));Put(form,"translating",true);
            Get<ComboBox>(form,"direction").SelectedIndex=2;
            Check(!Can(form)&&Get<Snapshot>(form,"pendingOfflineRetry")==null,"target change clears retry");Idle(form,"target change held behind translation gate");form.Close();
        }
        using(var form=Form("mode-change")){
            var shot=Seed(form);Record(form,shot,7,false,new TranslationFailure("rate_limited"));Get<ComboBox>(form,"engine").SelectedIndex=0;
            Check(!Can(form)&&Get<Snapshot>(form,"pendingOfflineRetry")==null,"ordinary mode change clears retry");Check(Get<bool>(form,"needsFreshSelection"),"ordinary mode change still needs fresh input");Idle(form,"normal mode change");form.Close();
        }
        using(var form=Form("fresh-text")){
            var shot=Seed(form);Record(form,shot,7,false,new TranslationFailure("rate_limited"));Put(form,"translating",true);Call(form,"AcceptText","Replacement selection","new-source");
            Check(!Can(form)&&Get<Snapshot>(form,"pendingOfflineRetry")==null,"accepting new text clears old retry");Idle(form,"new text held behind translation gate");form.Close();
        }
    }
    private static void ApplyExactAndRevalidate(){
        Ui.English=false;
        using(var form=Form("apply")){
            var english=Package("en","zh");var french=Package("fr","zh");var wrong=Package("zh","en");
            var shot=Seed(form,"Veuillez conserver ce texte.\n\n`KEEP_LITERAL`\nhttps://example.com/item");
            Record(form,shot,7,false,new TranslationFailure("rate_limited"));
            Check(!(bool)Call(form,"ApplyOfflineRetry",shot,6,french),"stale confirmation epoch cannot apply");
            Check(!(bool)Call(form,"ApplyOfflineRetry",shot,7,wrong),"wrong target package cannot apply");
            Check(!(bool)Call(form,"ApplyOfflineRetry",shot,7,new LanguagePack{From="de",To="zh"}),"uninstalled pair cannot apply");
            Check(!(bool)Call(form,"ApplyOfflineRetry",shot,7,null),"null choice cannot apply");
            File.Delete(Path.Combine(Preferences.DataDirectory,"packages",english.Id,".install.json"));
            Check(!(bool)Call(form,"ApplyOfflineRetry",shot,7,english),"removed package is revalidated on confirm");
            Check(!Preferences.Offline&&Can(form),"failed confirmations keep Google and preserve retry");Idle(form,"rejected confirmations");
            Put(form,"translating",true);Check((bool)Call(form,"ApplyOfflineRetry",shot,7,french),"explicit selected matching installed pair applies");
            var actual=Get<Snapshot>(form,"latest");Check(Preferences.Offline&&Preferences.OfflinePair=="pair:fr:zh","explicit retry switches to exact chosen offline source and target");
            Check(actual.Text==shot.Text&&actual.Conversation==shot.Conversation&&actual.Direction=="pair:fr:zh","original failed text, formatting and source are retained exactly");
            Check(!Object.ReferenceEquals(actual,shot)&&Get<int>(form,"epoch")>7,"explicit retry creates a fresh request epoch");
            Check(!Can(form)&&Get<Snapshot>(form,"pendingOfflineRetry")==null&&!Get<bool>(form,"needsFreshSelection"),"only explicit retry consumes pending text and permits translation without reselection");
            Check(Preferences.Target=="zh-CN","Google target preference is retained for later mode changes");Idle(form,"explicit retry held behind gate");
            Get<ComboBox>(form,"engine").SelectedIndex=1;Check(!Preferences.Offline&&Get<bool>(form,"needsFreshSelection"),"returning online never automatically uploads local text");Idle(form,"return online after retry");form.Close();
        }
    }
    [STAThread] public static int Main(){
        string oldData=Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR");
        try{
            Check(!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"backend","launcher.json")),"test must run from an isolated directory without backend");
            root=Path.Combine(Path.GetTempPath(),"dfy-offline-retry-tests-"+Guid.NewGuid().ToString("N"));
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            FailureClassification();TargetFilteringAndDialog();StaleRequestsAndClearing();ApplyExactAndRevalidate();
            Application.DoEvents();Console.WriteLine("OfflineRetryTests passed: "+checks+" checks. Synthetic fixtures only; no network, clipboard, user text or backend process.");return 0;
        }catch(Exception error){Console.WriteLine("OfflineRetryTests failed: "+error.GetBaseException().Message);Console.WriteLine(error.GetBaseException().StackTrace);return 1;}
        finally{Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR",oldData);}
    }
}
