using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClaudeCompanion;

internal static class WindowLockTests {
    static void Check(bool value,string name){if(!value)throw new Exception(name);}
    static object Get(object o,string field){return o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    static void Put(object o,string field,object value){o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);}
    static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}
    static CompanionForm Preview(){
        var form=new CompanionForm(true);
        // Start may update app state, but these tests never monitor global mouse input.
        ((IDisposable)Get(form,"watcher")).Dispose();
        return form;
    }
    static SourceWindow Source(Form fixture,string title){
        using(var process=Process.GetCurrentProcess())
            return new SourceWindow{Handle=fixture.Handle,ProcessId=process.Id,Started=process.StartTime.ToUniversalTime().Ticks,App="Fixture editor",Title=title};
    }
    static void Seed(CompanionForm form,SourceWindow source,bool active){
        Put(form,"locked",source);Put(form,"active",active);
        Put(form,"latest",new Snapshot{Conversation="fixture",MessageId="1",Text="Original source",Direction="auto:zh-CN"});
        ((TextBox)Get(form,"translated")).Text="已翻译的文字";
        ((TextBox)Get(form,"original")).Text="Original source";
        Call(form,"RefreshLockLabels");
    }
    static void ContentRetained(CompanionForm form,string phase){
        Check(((TextBox)Get(form,"translated")).Text=="已翻译的文字",phase+" preserves translation");
        Check(((TextBox)Get(form,"original")).Text=="Original source",phase+" preserves original");
        Check(((Snapshot)Get(form,"latest")).Text=="Original source",phase+" preserves snapshot");
    }
    static void GuardTests(){
        var identity=new SourceWindow{Handle=new IntPtr(123),ProcessId=7,Started=100};
        Check(identity.Matches(new IntPtr(123),7,100),"exact source identity");
        Check(!identity.Matches(new IntPtr(456),7,100),"same app different window rejected");
        Check(!identity.Matches(new IntPtr(123),8,100),"reused window other process rejected");
        Check(!identity.Matches(new IntPtr(123),7,200),"reused process rejected");
        Check(LanguageChoice.All.Length==17,"target catalog");
        using(var form=Preview()){
            Check(!(bool)Get(form,"active"),"startup is not capturing");
            Check(!((Button)Get(form,"toggle")).Enabled,"resume disabled until source locked");
            Check(((Button)Get(form,"chooseSource")).Enabled,"source picker available at startup");
            Check(!((Button)Get(form,"clearSource")).Enabled,"clear disabled without a source");
            int before=(int)Get(form,"selectionEpoch");
            ((Task)Call(form,"ReadSelection",new IntPtr(999))).GetAwaiter().GetResult();
            Check((int)Get(form,"selectionEpoch")==before,"unlocked selection ignored before any capture");
            Seed(form,identity,true);
            ((Task)Call(form,"ReadSelection",new IntPtr(456))).GetAwaiter().GetResult();
            Check((int)Get(form,"selectionEpoch")==before,"other window ignored before capture");
            Ui.SetEnglish(true);
            Check(form.Text=="Whale Translator "+Program.Version,"English title");
            Check(((Button)Get(form,"language")).Text=="中文","Chinese switch label");
            ContentRetained(form,"English toggle");
            Check(((ComboBox)Get(form,"direction")).AccessibleName=="Target language","English accessibility label");
            Ui.SetEnglish(false);
            Check(form.Text=="大肥鱼翻译 "+Program.Version,"Chinese title");
            ContentRetained(form,"Chinese toggle");
            int priorEpoch=(int)Get(form,"epoch");Call(form,"Pause");
            Check(!(bool)Get(form,"active")&&(int)Get(form,"epoch")>priorEpoch,"pause invalidates pending results");
            form.Close();
        }
    }
    static void PickerLifecycleTests(SourceWindow oldSource,SourceWindow nextSource){
        using(var form=Preview()){
            Seed(form,oldSource,true);
            int priorEpoch=(int)Get(form,"epoch"),priorSelection=(int)Get(form,"selectionEpoch");
            using(var pending=new CancellationTokenSource()){
                Put(form,"captureCancellation",pending);
                Check((bool)Call(form,"BeginSourcePick"),"first picker can begin");
                Check(pending.IsCancellationRequested,"opening picker cancels pending capture");
                Check(Get(form,"captureCancellation")==null,"pending capture detached");
            }
            Check(!(bool)Get(form,"active")&&(bool)Get(form,"pickingSource"),"picker pauses capture");
            Check(Object.ReferenceEquals(Get(form,"locked"),oldSource),"opening picker keeps locked identity");
            Check((int)Get(form,"epoch")>priorEpoch&&(int)Get(form,"selectionEpoch")>priorSelection,"opening picker invalidates pending results");
            int openedEpoch=(int)Get(form,"epoch");
            Check(!(bool)Call(form,"BeginSourcePick"),"second picker rejected");
            Check((int)Get(form,"epoch")==openedEpoch,"reentry does not alter picker transaction");
            int before=(int)Get(form,"selectionEpoch");
            ((Task)Call(form,"ReadSelection",oldSource.Handle)).GetAwaiter().GetResult();
            Check((int)Get(form,"selectionEpoch")==before,"selection ignored while picker open");
            ContentRetained(form,"opening picker");
            Call(form,"CompleteSourcePick",new object[]{null});
            Check(!(bool)Get(form,"pickingSource")&&(bool)Get(form,"active"),"cancel restores previous active state");
            Check(Object.ReferenceEquals(Get(form,"locked"),oldSource),"cancel keeps old source");
            ContentRetained(form,"cancel");

            Call(form,"Pause");
            Check((bool)Call(form,"BeginSourcePick"),"paused source can open picker");
            Call(form,"CompleteSourcePick",new object[]{null});
            Check(!(bool)Get(form,"active"),"cancel retains previous paused state");
            ContentRetained(form,"paused cancel");

            Check((bool)Call(form,"BeginSourcePick"),"replacement picker begins");
            Call(form,"CompleteSourcePick",nextSource);
            Check(Object.ReferenceEquals(Get(form,"locked"),nextSource)&&(bool)Get(form,"active"),"live choice commits and starts");
            Check(!(bool)Get(form,"pickingSource"),"committed picker transaction ends");
            Check(((Label)Get(form,"sourceName")).Text.Contains(nextSource.Title),"selected title appears in source card");
            ContentRetained(form,"replacement");
            before=(int)Get(form,"selectionEpoch");
            ((Task)Call(form,"ReadSelection",oldSource.Handle)).GetAwaiter().GetResult();
            Check((int)Get(form,"selectionEpoch")==before,"previous source ignored after replacement");
            Call(form,"Pause");form.Close();
        }
        using(var form=Preview()){
            Check((bool)Call(form,"BeginSourcePick"),"unlocked source can open picker");
            Call(form,"CompleteSourcePick",new object[]{null});
            Check(Get(form,"locked")==null&&!(bool)Get(form,"active"),"startup cancel remains unlocked and paused");
            Check(!((Button)Get(form,"toggle")).Enabled,"startup cancel cannot resume");
            Check((bool)Call(form,"ApplySource",nextSource),"live source commit returns success");
            Check(Object.ReferenceEquals(Get(form,"locked"),nextSource)&&(bool)Get(form,"active"),"successful commit installs and starts live source");
            Call(form,"Pause");form.Close();
        }
    }
    static void StaleChoiceTests(SourceWindow oldSource,SourceWindow liveSource){
        SourceWindow closed;
        using(var fixture=new Form())closed=Source(fixture,"Closed fixture");
        var wrongProcess=new SourceWindow{Handle=liveSource.Handle,ProcessId=liveSource.ProcessId+1,Started=liveSource.Started,Title="Wrong process",App="Fixture"};
        var reusedProcess=new SourceWindow{Handle=liveSource.Handle,ProcessId=liveSource.ProcessId,Started=liveSource.Started+1,Title="Reused process",App="Fixture"};
        foreach(var invalid in new[]{closed,wrongProcess,reusedProcess}){
            Check(!invalid.IsAlive(),"invalid fixture rejected by identity guard");
            foreach(bool wasActive in new[]{false,true})using(var form=Preview()){
                Seed(form,oldSource,wasActive);
                int beforeApply=(int)Get(form,"epoch");
                Check(!(bool)Call(form,"ApplySource",invalid),"stale source commit returns failure");
                Check(Object.ReferenceEquals(Get(form,"locked"),oldSource)&&(int)Get(form,"epoch")==beforeApply,"final source guard rejects stale identity before mutation");
                Check((bool)Call(form,"BeginSourcePick"),"invalid choice picker begins");
                string priorStatus=((Label)Get(form,"status")).Text;
                Call(form,"CompleteSourcePick",invalid);
                Check(Object.ReferenceEquals(Get(form,"locked"),oldSource),"stale choice keeps old source");
                Check((bool)Get(form,"active")==wasActive,"stale choice restores prior active or paused state");
                Check(!(bool)Get(form,"pickingSource"),"stale picker transaction ends");
                Check(((Label)Get(form,"status")).Text!=priorStatus,"stale choice reports a status");
                ContentRetained(form,"stale choice");
                Call(form,"Pause");form.Close();
            }
        }
        using(var fixture=new Form())using(var form=Preview()){
            Seed(form,Source(fixture,"Source closing during picker"),true);
            Check((bool)Call(form,"BeginSourcePick"),"source closing fixture picker begins");
            fixture.Dispose();
            Call(form,"CompleteSourcePick",new object[]{null});
            Check(Get(form,"locked")==null&&!(bool)Get(form,"active"),"cancel cannot resume a source that closed during picker");
            ContentRetained(form,"source closed during picker");
            form.Close();
        }
        using(var form=Preview()){
            Seed(form,oldSource,true);
            Check((bool)Call(form,"BeginSourcePick"),"closing fixture picker begins");
            Put(form,"closing",true);
            Call(form,"CompleteSourcePick",liveSource);
            Check(!(bool)Get(form,"active"),"closing cannot restart capture");
            Check(Object.ReferenceEquals(Get(form,"locked"),oldSource),"closing cannot replace source");
            Check(!(bool)Call(form,"BeginSourcePick"),"closing cannot open picker");
            Put(form,"closing",false);form.Close();
        }
    }
    [STAThread] static int Main(){
        try{return Run();}
        catch(Exception error){Console.WriteLine("WindowLockTests failed: "+error.GetBaseException().Message);Console.WriteLine(error.GetBaseException().StackTrace);return 1;}
    }
    static int Run(){
        Application.EnableVisualStyles();Ui.English=false;
        GuardTests();
        // Creating a native handle is enough for identity checks. Never Show/Activate these forms.
        using(var oldFixture=new Form())using(var nextFixture=new Form()){
            var oldSource=Source(oldFixture,"Old window");var nextSource=Source(nextFixture,"Next window");
            Check(oldSource.IsAlive()&&nextSource.IsAlive(),"hidden native fixture identities are live");
            Check(!oldFixture.Visible&&!nextFixture.Visible,"fixture windows stay hidden");
            PickerLifecycleTests(oldSource,nextSource);StaleChoiceTests(oldSource,nextSource);
            Check(!oldFixture.Visible&&!nextFixture.Visible,"fixtures were never shown");
        }
        using(var compose=new ComposeForm(new WindowChoice{Handle="0",Title="Test"},true)){
            var chinese=(TextBox)Get(compose,"chinese");var english=(TextBox)Get(compose,"english");
            chinese.Text="保留原始文件";english.Text="Keep original files";
            Ui.SetEnglish(true);
            Check(compose.Text.Contains("Compose in Chinese"),"compose English UI");
            Check(chinese.Text=="保留原始文件"&&english.Text=="Keep original files","compose toggle preserves input");
            Ui.SetEnglish(false);compose.Close();
        }
        Console.WriteLine("WindowLockTests passed: identity guards, picker pause/cancel/reentry/commit/stale/closing, and bilingual content preservation. Hidden fixtures only; no clipboard, input monitoring, or network used.");return 0;
    }
}
