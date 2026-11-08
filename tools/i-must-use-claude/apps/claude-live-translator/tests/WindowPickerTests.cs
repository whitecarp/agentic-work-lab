using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClaudeCompanion;

internal static class WindowPickerTests {
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    static void Check(bool value,string name){if(!value)throw new Exception(name);}
    static object Get(object o,string field){return o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}
    static List<SourceWindowCard> Cards(SourceWindowPickerForm picker){return (List<SourceWindowCard>)Get(picker,"cards");}
    static SourceWindow Source(Form fixture,string app,string title){
        using(var process=Process.GetCurrentProcess())
            return new SourceWindow{Handle=fixture.Handle,ProcessId=process.Id,Started=process.StartTime.ToUniversalTime().Ticks,App=app,Title=title};
    }
    static void FixturePrivacy(SourceWindowPickerForm picker,string phase){
        Check((bool)Get(picker,"fixtureMode"),phase+" uses fixture mode");
        Check(((IDictionary)Get(picker,"icons")).Count==0,phase+" does not retrieve window icons");
        Check(((HashSet<string>)Get(picker,"iconAttempts")).Count==0,phase+" never attempts native icon retrieval");
        Check(!picker.Visible,phase+" picker stays hidden");
    }
    static void FilteringTests(SourceWindow current,SourceWindow other){
        var reused=new SourceWindow{Handle=current.Handle,ProcessId=current.ProcessId,Started=current.Started+1,App="Reused app",Title="Reused identity"};
        using(var picker=new SourceWindowPickerForm(current,new[]{current,other,reused,null})){
            Check(picker.SelectedSource==null,"no source is committed by opening picker");
            Check(Cards(picker).Count==3,"null fixtures skipped");
            Check(Cards(picker)[0].IsCurrent&&!Cards(picker)[1].IsCurrent&&!Cards(picker)[2].IsCurrent,"current badge uses full identity");
            Check(Object.ReferenceEquals(Cards(picker)[0].Source,current),"cards retain exact window identity");
            Check(Cards(picker)[0].AccessibleRole==AccessibleRole.PushButton,"card exposes button accessibility role");
            Check(Cards(picker)[0].AccessibleName.Contains(current.Title),"card accessibility includes window title");
            FixturePrivacy(picker,"initial render");

            var search=(TextBox)Get(picker,"search");
            search.Text="  PROJECT ALPHA  ";
            Check(Cards(picker).Count==1&&Object.ReferenceEquals(Cards(picker)[0].Source,current),"title search is trimmed and case insensitive");
            search.Text=other.App.ToUpperInvariant();
            Check(Cards(picker).Count==1&&Object.ReferenceEquals(Cards(picker)[0].Source,other),"app search is case insensitive");
            search.Text="中文";
            Check(Cards(picker).Count==1&&Object.ReferenceEquals(Cards(picker)[0].Source,other),"Chinese title is searchable");
            search.Text="there is no matching window";
            Check(Cards(picker).Count==0,"missing query produces empty results");
            Check(((FlowLayoutPanel)Get(picker,"cardsPanel")).Controls.Count==1,"empty results offer a visible explanation control");
            Ui.SetEnglish(true);
            Check(search.Text=="there is no matching window","language switch preserves search text");
            Check(search.AccessibleName=="Search app or window title","English search accessibility label");
            Check(((Label)((FlowLayoutPanel)Get(picker,"cardsPanel")).Controls[0]).Text.Contains("No matching windows"),"empty state translates into English");
            Check(picker.SelectedSource==null,"filtering never chooses a window");
            Ui.SetEnglish(false);
            Check(search.AccessibleName=="搜索程序或窗口标题","Chinese search accessibility label");
            search.Text="";
            Check(Cards(picker).Count==3,"clearing search restores all cards");
            ((Task)Call(picker,"RefreshWindowsAsync")).GetAwaiter().GetResult();
            Check(Cards(picker).Count==3,"fixture refresh keeps supplied window list");
            FixturePrivacy(picker,"search and refresh");
        }
        using(var picker=new SourceWindowPickerForm(null,new SourceWindow[0])){
            Check(Cards(picker).Count==0&&picker.SelectedSource==null,"empty list cannot preselect another window");
            Check(((FlowLayoutPanel)Get(picker,"cardsPanel")).Controls.Count==1,"empty list has guidance");
            FixturePrivacy(picker,"empty list");
        }
    }
    static void ChoiceTests(SourceWindow current,SourceWindow other){
        using(var picker=new SourceWindowPickerForm(current,new[]{current,other})){
            Call(Cards(picker)[1],"OnClick",EventArgs.Empty);
            Check(Object.ReferenceEquals(picker.SelectedSource,other),"one card click chooses the exact requested window");
            Check(picker.DialogResult==DialogResult.OK,"card click confirms selection");
        }
        foreach(var key in new[]{Keys.Enter,Keys.Space})using(var picker=new SourceWindowPickerForm(null,new[]{current,other})){
            var args=new KeyEventArgs(key);
            Call(Cards(picker)[0],"OnKeyDown",args);
            Check(args.Handled&&args.SuppressKeyPress,"card keyboard selection consumes the activation key");
            Check(Object.ReferenceEquals(picker.SelectedSource,current)&&picker.DialogResult==DialogResult.OK,"keyboard activates exact card");
        }
        using(var picker=new SourceWindowPickerForm(current,new[]{current,other})){
            Call(Get(picker,"cancel"),"OnClick",EventArgs.Empty);
            Check(picker.SelectedSource==null,"cancel never commits a source");
            Check(picker.DialogResult==DialogResult.Cancel,"cancel returns cancelled result");
            FixturePrivacy(picker,"cancel");
        }
    }
    static void StaleTests(SourceWindow valid){
        SourceWindow closed;
        using(var fixture=new Form())closed=Source(fixture,"Closed app","Closed window");
        var wrongProcess=new SourceWindow{Handle=valid.Handle,ProcessId=valid.ProcessId+1,Started=valid.Started,App="Wrong process",Title="Wrong window"};
        var reusedProcess=new SourceWindow{Handle=valid.Handle,ProcessId=valid.ProcessId,Started=valid.Started+1,App="Reused process",Title="Reused window"};
        foreach(var invalid in new[]{closed,wrongProcess,reusedProcess})using(var picker=new SourceWindowPickerForm(valid,new[]{invalid,valid})){
            Call(Cards(picker)[0],"OnClick",EventArgs.Empty);
            Check(picker.SelectedSource==null&&picker.DialogResult!=DialogResult.OK,"closed or mismatched identity cannot commit");
            Check((bool)Get(picker,"closedHint"),"stale card explains that window is unavailable");
            Check(!picker.IsDisposed,"stale card leaves picker available for another choice");
            FixturePrivacy(picker,"stale choice");
        }
    }
    static void CompactPickerTests(SourceWindow fixture){
        var background=new SourceWindow{App="AsMonitorControl",Title="Monitor"};
        var regular=new SourceWindow{App="Custom editor",Title="Notes"};
        var chat=new SourceWindow{App="ChatGPT",Title="Reading"};
        var browser=new SourceWindow{App="chrome",Title="Browser"};
        var ordered=SourceWindowPickerForm.OrderedSources(new[]{background,regular,chat,null,browser});
        Check(ordered.Count==4,"sorting retains every non-null window");
        Check(ordered[0]==chat&&ordered[1]==browser&&ordered[2]==regular&&ordered[3]==background,"common readers precede normal apps and driver controls");
        Check(SourceWindowPickerForm.NavigationIndex(0,8,2,Keys.Up)==0,"up at first card is bounded");
        Check(SourceWindowPickerForm.NavigationIndex(1,8,2,Keys.Down)==3,"down follows the same column");
        Check(SourceWindowPickerForm.NavigationIndex(3,8,2,Keys.Up)==1,"up follows the same column");
        Check(SourceWindowPickerForm.NavigationIndex(4,8,2,Keys.Left)==3,"left moves to previous card");
        Check(SourceWindowPickerForm.NavigationIndex(7,8,2,Keys.Right)==7,"right at final card is bounded");
        Check(SourceWindowPickerForm.NavigationIndex(1,8,1,Keys.Down)==2,"narrow one-column layouts navigate vertically");
        using(var picker=new SourceWindowPickerForm(null,new[]{fixture})){
            int expectedHeight;using(var graphics=picker.CreateGraphics())expectedHeight=(int)Math.Round(65*graphics.DpiX/96F);
            Check(Cards(picker)[0].Height==expectedHeight,"compact card follows the current device DPI");
            ((TextBox)Get(picker,"search")).Text="__missing__";
            ((TextBox)Get(picker,"search")).Text="";
            Check(Cards(picker)[0].Height==expectedHeight,"cards created after search retain the initial DPI dimensions");
            Check(Cards(picker)[0].IconImage==null,"missing icons use the painted initial instead of a system icon");
            var field=(PickerSearchField)Get(picker,"searchField");
            picker.PerformLayout();field.Parent.PerformLayout();
            Check(field.Bottom<=field.Parent.ClientSize.Height,"search field stays inside its DPI-scaled row");
            Check(field.Input.Top>=0&&field.Input.Bottom<=field.ClientSize.Height,"search text is vertically contained at device DPI");
            var cancel=(Button)Get(picker,"cancel");cancel.Parent.PerformLayout();
            Check(cancel.Bottom<=cancel.Parent.ClientSize.Height,"cancel button stays inside its footer row");
            Check(field.Placeholder=="搜索窗口或程序名","Chinese search cue is present");
            Ui.SetEnglish(true);Check(field.Placeholder=="Search windows or apps","English search cue is present");Ui.SetEnglish(false);
            Check(picker.Icon!=null,"picker receives a window icon");
            FixturePrivacy(picker,"compact fixture");
        }
    }
    [STAThread] static int Main(){
        try{SetProcessDPIAware();return Run();}
        catch(Exception error){Console.WriteLine("WindowPickerTests failed: "+error.GetBaseException().Message);Console.WriteLine(error.GetBaseException().StackTrace);return 1;}
    }
    static int Run(){
        Application.EnableVisualStyles();Ui.English=false;
        // Native fixture handles are never shown, activated, copied, or inspected for content.
        using(var first=new Form())using(var second=new Form()){
            var current=Source(first,"FixtureEditor","Project Alpha — original files");
            var other=Source(second,"FixtureBrowser","中文阅读 · 第二个窗口");
            Check(current.IsAlive()&&other.IsAlive(),"hidden source fixtures are live");
            FilteringTests(current,other);ChoiceTests(current,other);StaleTests(current);CompactPickerTests(current);
            Check(!first.Visible&&!second.Visible,"source fixtures remain hidden");
        }
        Console.WriteLine("WindowPickerTests passed: bilingual search/empty states, exact identity badges, single click/keyboard/cancel, stale identities, fixture privacy. No enumeration, clipboard, synthesized input, or network used.");
        return 0;
    }
}
