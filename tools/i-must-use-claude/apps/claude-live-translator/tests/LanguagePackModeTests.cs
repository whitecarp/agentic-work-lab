using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using ClaudeCompanion;

// Compile this runner in an isolated output directory without backend/launcher.json.
// Fixtures contain only metadata and tiny placeholder bytes: never load a model or call a service.
internal static class LanguagePackModeTests {
    private static readonly Encoding Utf8=new UTF8Encoding(false);
    private static string fixtureRoot;
    private static void Check(bool value,string name) { if(!value)throw new Exception(name); }
    private static object Get(object owner,string name) { return owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner); }
    private static void Put(object owner,string name,object value) { owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value); }
    private static object Call(object owner,string name,params object[] args) { return owner.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,args); }
    private static string Direction(CompanionForm form) { return (string)form.GetType().GetProperty("SelectedDirection",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form,null); }
    private static Button FindButton(Control root,string text) {
        foreach(Control child in root.Controls) {
            var button=child as Button;if(button!=null&&(button.Text==text||button.AccessibleName==text))return button;
            var nested=FindButton(child,text);if(nested!=null)return nested;
        }
        return null;
    }
    private static CompanionForm Preview() {
        var form=new CompanionForm(true);IntPtr handle=form.Handle;
        ((IDisposable)Get(form,"watcher")).Dispose();
        Check(!form.Visible,"preview fixture is never shown");return form;
    }
    private static void Reset(string directory) {
        Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR",directory);
        Ui.English=false;Preferences.Target="zh-CN";Preferences.Pin=true;Preferences.Offline=false;Preferences.OfflinePair="pair:en:zh";
    }
    private static string Package(string directory,string from,string to,string version,bool complete) {
        string id="translate-"+from+"_"+to+"-"+version.Replace('.','_');
        string folder=Path.Combine(directory,"packages",id);Directory.CreateDirectory(Path.Combine(folder,"model"));
        File.WriteAllText(Path.Combine(folder,"metadata.json"),Json.Encode(new { type="translate",from_code=from,to_code=to,from_name=from=="en"?"English":from=="zh"?"Chinese":from,to_name=to=="en"?"English":to=="zh"?"Chinese":to,package_version=version }),Utf8);
        foreach(string path in new[]{"model/model.bin","model/config.json","model/shared_vocabulary.json","sentencepiece.model"})File.WriteAllText(Path.Combine(folder,path.Replace('/',Path.DirectorySeparatorChar)),path.EndsWith(".json",StringComparison.Ordinal)?"{}":"fixture only",Utf8);
        if(complete)File.WriteAllText(Path.Combine(folder,".install.json"),Json.Encode(new { id=id,complete=true }),Utf8);
        return folder;
    }
    private static void CheckWorkerIdle(CompanionForm form,string phase) {
        Check(Get(Get(form,"worker"),"process")==null,phase+" never starts a translation process");
        Check(!(bool)Get(form,"translating"),phase+" never queues a translation");
    }
    private static void FreshUserAndMissingPack() {
        string directory=Path.Combine(fixtureRoot,"fresh");Reset(directory);Preferences.Load();
        Check(!Preferences.Offline,"new user starts in Google online mode");
        using(var form=Preview()) {
            var engine=(ComboBox)Get(form,"engine");var targets=(ComboBox)Get(form,"direction");
            Check(engine.SelectedIndex==1,"Google is selected for a fresh user");
            Check(targets.Enabled&&targets.Items.Count==LanguageChoice.All.Length,"online mode retains all target languages");
            engine.SelectedIndex=0;
            Check(Preferences.Offline,"offline mode remains selectable before installing any pack");
            Check(!targets.Enabled&&targets.Items.Count==1&&!(targets.SelectedItem is LanguagePack),"missing packs show a disabled explanatory direction row");
            var settingsModel=(SettingsPanelModel)Call(form,"CreateSettingsModel");
            using(var settings=new SettingsPanel(settingsModel)){
                var manage=FindButton(settings,"离线语言包");
                Check(manage!=null&&manage.Enabled,"settings keeps the language pack manager available without an installed pack");
                Check(settingsModel.ManagePacks!=null&&settingsModel.ManagePacks.Method.Name=="ManagePacks","settings pack entry delegates to the original pack-management flow");
                Check(!((ComboBox)Get(settings,"targets")).Enabled,"settings reflects disabled offline directions until a pack is installed");
            }
            Check(((Label)Get(form,"googleStatus")).Text.Contains("离线语言包"),"missing packs point the user to the pack manager");
            CheckWorkerIdle(form,"mode change without a pack");
            Check(!File.Exists(Path.Combine(directory,"preferences.json")),"preview mode never creates a preference file");
            form.Close();
        }
        using(var manager=new LanguagePacksForm(true)) {
            IntPtr handle=manager.Handle;
            Check((bool)Get(manager,"preview")&&!manager.Visible,"manager preview can be opened as an entirely local hidden fixture");
            Check(!(bool)Get(manager,"busy")&&Get(manager,"cancellation")==null,"manager preview does not start its list/download service");
            Check(((Button)Get(manager,"refresh")).Enabled,"catalog refresh remains an explicit available action");
            Check(!((Button)Get(manager,"install")).Enabled,"opening the manager never implicitly selects a download");
            manager.Close();
        }
    }
    private static void InstalledPairSelection() {
        string directory=Path.Combine(fixtureRoot,"installed");Reset(directory);
        Package(directory,"en","zh","1.9",true);Package(directory,"zh","en","1.9",true);
        Package(directory,"en","ja","1.1",false);
        string incomplete=Package(directory,"de","en","1.3",true);File.Delete(Path.Combine(incomplete,"model","config.json"));
        var installed=LanguagePack.InstalledPacks();
        Check(installed.Count==2,"half-installed packages and missing model files are excluded from the UI");
        Preferences.Offline=true;Preferences.OfflinePair="pair:zh:en";
        using(var form=Preview()) {
            var targets=(ComboBox)Get(form,"direction");
            Check(targets.Enabled&&targets.Items.Count==2,"only complete installed pairs appear in offline directions");
            Check(Direction(form)=="pair:zh:en","saved installed language pair is restored");
            foreach(LanguagePack pair in targets.Items)if(pair.Pair=="pair:en:zh") { targets.SelectedItem=pair;break; }
            Check(Preferences.OfflinePair=="pair:en:zh"&&Direction(form)=="pair:en:zh","changing offline pair stores and requests the exact installed direction");
            CheckWorkerIdle(form,"pair selection without text");
            form.Close();
        }
    }
    private static void ModeSwitchRequiresFreshText() {
        string directory=Path.Combine(fixtureRoot,"mode-switch");Reset(directory);
        Package(directory,"en","zh","1.9",true);Package(directory,"zh","en","1.9",true);
        Preferences.Offline=true;
        using(var form=Preview()) {
            var snapshot=new Snapshot { Conversation="fixture",MessageId="only-local",Text="PRIVATE_LOCAL_FIXTURE",Direction="pair:en:zh" };
            Put(form,"latest",snapshot);Put(form,"needsFreshSelection",false);
            ((TextBox)Get(form,"original")).Text=snapshot.Text;
            ((TextBox)Get(form,"translated")).Text="Existing local result";
            int epoch=(int)Get(form,"epoch");
            ((ComboBox)Get(form,"engine")).SelectedIndex=1;
            Check(!Preferences.Offline&&(bool)Get(form,"needsFreshSelection"),"switching to Google requires a fresh selection or paste");
            Check((int)Get(form,"epoch")>epoch,"mode change invalidates earlier asynchronous results");
            Check(Object.ReferenceEquals(Get(form,"latest"),snapshot),"switching engine does not construct a new request from old text");
            Check(((TextBox)Get(form,"original")).Text==snapshot.Text&&((TextBox)Get(form,"translated")).Text=="Existing local result","mode switch preserves the visible old result without retranslating it");
            CheckWorkerIdle(form,"offline to online change");
            int afterSwitch=(int)Get(form,"epoch");
            var targets=(ComboBox)Get(form,"direction");targets.SelectedIndex=targets.SelectedIndex==1?2:1;
            Application.DoEvents();
            Check((int)Get(form,"epoch")==afterSwitch&&Object.ReferenceEquals(Get(form,"latest"),snapshot),"changing Google target after mode switch still cannot upload old content");
            Check(((TextBox)Get(form,"translated")).Text=="Existing local result","target change preserves old result until fresh input");
            CheckWorkerIdle(form,"target change after switching to Google");
            ((ComboBox)Get(form,"engine")).SelectedIndex=0;
            Check(Preferences.Offline&&(bool)Get(form,"needsFreshSelection"),"switching back to offline also requires fresh input");
            CheckWorkerIdle(form,"switching back to offline");form.Close();
        }
    }
    private static void PreviewDoesNotWritePreferences() {
        string directory=Path.Combine(fixtureRoot,"preview-preferences");Reset(directory);Directory.CreateDirectory(directory);
        Package(directory,"en","zh","1.9",true);
        string path=Path.Combine(directory,"preferences.json");
        string original="{\"preview_fixture\":true,\"english\":false,\"offline\":false}";
        File.WriteAllText(path,original,Utf8);DateTime written=File.GetLastWriteTimeUtc(path);
        using(var form=Preview()) {
            ((CheckBox)Get(form,"pin")).Checked=false;
            Call(Get(form,"language"),"OnClick",EventArgs.Empty);
            ((ComboBox)Get(form,"engine")).SelectedIndex=0;
            ((ComboBox)Get(form,"engine")).SelectedIndex=1;
            ((ComboBox)Get(form,"direction")).SelectedIndex=1;
            Check(File.ReadAllText(path,Utf8)==original&&File.GetLastWriteTimeUtc(path)==written,"preview interactions do not rewrite saved preferences");
            CheckWorkerIdle(form,"preview interactions");form.Close();
        }
    }
    [STAThread] public static int Main() {
        string previousData=Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR");
        bool oldEnglish=Ui.English,oldOffline=Preferences.Offline,oldPin=Preferences.Pin;string oldTarget=Preferences.Target,oldPair=Preferences.OfflinePair;
        try {
            Check(!oldOffline,"default preference is Google online before any settings are loaded");
            Check(!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"backend","launcher.json")),"run this test executable from an isolated directory without a backend so regressions cannot access a network worker");
            fixtureRoot=Path.Combine(Path.GetTempPath(),"dfy-language-mode-tests-"+Guid.NewGuid().ToString("N"));
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            FreshUserAndMissingPack();InstalledPairSelection();ModeSwitchRequiresFreshText();PreviewDoesNotWritePreferences();
            Console.WriteLine("LanguagePackModeTests passed: Google default, no-pack manager entry, complete installed pairs, no retransmission after mode change, preview preference isolation. Local fixtures only; no model loading, worker process, network, clipboard or input used.");return 0;
        }catch(Exception error) { Console.WriteLine("LanguagePackModeTests failed: "+error.GetBaseException().Message);Console.WriteLine(error.GetBaseException().StackTrace);return 1; }
        finally { Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR",previousData);Ui.English=oldEnglish;Preferences.Offline=oldOffline;Preferences.Pin=oldPin;Preferences.Target=oldTarget;Preferences.OfflinePair=oldPair; }
    }
}
