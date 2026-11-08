using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ClaudeCompanion;

internal static class GlossaryHistoryTests {
    private static int checks;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    private static GlossaryEntry Find(List<GlossaryEntry> entries,string source,string target){return entries.Find(delegate(GlossaryEntry entry){return entry.source==source&&entry.target==target;});}
    private static string EntriesText(List<GlossaryEntry> entries){return Json.Encode(entries);}
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static T Get<T>(object owner,string name){return (T)owner.GetType().GetField(name,Private).GetValue(owner);}
    private static object Call(object owner,string name,params object[] args){return owner.GetType().GetMethod(name,Private).Invoke(owner,args);}
    private static void Render(Form form,string filename){form.TopMost=false;form.ShowInTaskbar=false;form.Opacity=0;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);form.Show();Application.DoEvents();form.PerformLayout();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,filename));}form.Hide();}
    [STAThread] public static int Main(){
        Encoding originalInputEncoding=Console.InputEncoding;
        try{
            string fixture=Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR");
            Check(!String.IsNullOrWhiteSpace(fixture)&&Path.GetFileName(fixture).StartsWith("glossary-history-fixture-",StringComparison.Ordinal),"isolated fixture directory required");
            Check(!Directory.Exists(fixture),"fixture must start empty");
            var defaults=GlossaryStore.Load();
            Check(defaults.Count>3&&defaults.Count<=GlossaryStore.MaxEntries,"expanded starter glossary available within shared worker capacity");
            Check(EntriesText(GlossaryStore.Validate(defaults))==EntriesText(defaults),"all starter terms already pass normalization and validation");
            foreach(string brand in new[]{"Claude","Claude Code","Codex","Skills","GitHub","MCP"}){
                var term=Find(defaults,brand,"*");Check(term!=null&&term.replacement==brand,"starter glossary preserves "+brand+" in every language");
            }
            Check(Find(defaults,"context window","zh-CN")!=null&&Find(defaults,"context window","zh-CN").replacement=="上下文窗口","English technical phrase has Simplified Chinese target");
            Check(Find(defaults,"上下文窗口","en")!=null&&Find(defaults,"上下文窗口","en").replacement=="context window","Chinese technical phrase has English target");
            foreach(string name in GlossaryStore.ExactCaseNames){var term=Find(defaults,name,"*");Check(term!=null&&term.matchCase,"everyday-word name "+name+" keeps exact case");}
            Check(!Find(defaults,"Claude","*").matchCase&&!Find(defaults,"context window","zh-CN").matchCase,"names and phrases ignore case by default");
            Check(Find(defaults,"Context window","zh-CN")==null,"one ignore-case row replaces capitalised phrase copies");
            foreach(string name in new[]{"Claude in Chrome","Claude Agent SDK","Claude Max","Hugging Face","GitHub Actions","Visual Studio Code","Artifacts","Hooks","Fable","Rust","Cursor","Copilot"})Check(Find(defaults,name,"*")!=null,"starter glossary includes "+name);
            Check(Find(defaults,"Rust","*").matchCase&&!Find(defaults,"Hugging Face","*").matchCase,"new everyday-word names keep exact case, product names ignore it");
            Check(Find(defaults,"仓库","en")!=null&&Find(defaults,"仓库","en").replacement=="repository"&&Find(defaults,"edge case","zh-CN").replacement=="边界情况","new phrases cover both directions");
            bool duplicate=false;try{GlossaryStore.Validate(new[]{new GlossaryEntry{source="Claude",replacement="a",target="*"},new GlossaryEntry{source="claude",replacement="b",target="*"}});}catch(InvalidOperationException){duplicate=true;}
            Check(duplicate,"ignore-case terms that differ only in case are duplicates");
            Check(GlossaryStore.Validate(new[]{new GlossaryEntry{source="Apple",replacement="苹果公司",target="*",matchCase=true},new GlossaryEntry{source="apple",replacement="苹果",target="*"}}).Count==2,"an exact-case term may sit beside an ignore-case one");
            Check(!Directory.Exists(fixture),"reading defaults does not create files");
            var custom=new List<GlossaryEntry>{
                new GlossaryEntry{source="Claude",replacement="克劳德自定义",target="*"},
                new GlossaryEntry{source="Claude Code",replacement="自定义编程助手",target="zh-CN"},
                new GlossaryEntry{source="context window",replacement="自定义上下文",target="*"},
                new GlossaryEntry{source="上下文窗口",replacement="custom context",target="en"},
                new GlossaryEntry{source="Custom Widget",replacement="自定义部件",target="zh-CN"}};
            string customBefore=EntriesText(custom);
            var merged=GlossaryStore.MergeRecommended(custom);
            Check(EntriesText(custom)==customBefore,"adding recommended terms does not mutate the editor input list");
            Check(EntriesText(merged.GetRange(0,custom.Count))==customBefore,"custom rows and their order remain unchanged after merge");
            Check(Find(merged,"Claude Code","*")==null,"recommended wildcard cannot broaden an existing target-specific custom term");
            Check(Find(merged,"context window","zh-CN")==null,"recommended specific target cannot shadow an existing custom wildcard");
            Check(merged.Count>custom.Count&&Find(merged,"GitHub","*")!=null,"recommended terms fill unrelated gaps");
            Check(EntriesText(GlossaryStore.MergeRecommended(merged))==EntriesText(merged),"repeated recommendation merge is idempotent");
            Check(!Directory.Exists(fixture),"staging recommended terms does not create saved settings");
            GlossaryStore.Save(new[]{new GlossaryEntry{source="Skills",replacement="技能",target="zh-CN"}});
            Check(GlossaryStore.Load()[0].replacement=="技能","UTF-8 glossary survives read-back");
            Check(GlossaryStore.Load().Count==1,"reading a saved custom glossary leaves deleted starter terms absent");
            string legacy="{\"version\":1,\"entries\":[{\"source\":\"context window\",\"replacement\":\"上下文窗口\",\"target\":\"zh-CN\"},{\"source\":\"Context window\",\"replacement\":\"上下文窗口\",\"target\":\"zh-CN\"},"+
                "{\"source\":\"Apple\",\"replacement\":\"苹果公司\",\"target\":\"*\"},{\"source\":\"apple\",\"replacement\":\"苹果\",\"target\":\"*\"},{\"source\":\"Skills\",\"replacement\":\"技能\",\"target\":\"zh-CN\"}]}";
            File.WriteAllText(GlossaryStore.FilePath,legacy,new UTF8Encoding(false));
            var migrated=GlossaryStore.Load();
            Check(migrated.Count==4&&!Find(migrated,"context window","zh-CN").matchCase&&Find(migrated,"Context window","zh-CN")==null,"version 1 capitalised copies fold into one ignore-case row");
            Check(Find(migrated,"Apple","*").matchCase&&Find(migrated,"apple","*").matchCase,"version 1 rows that differ in meaning keep exact case");
            Check(Find(migrated,"Skills","zh-CN").matchCase,"version 1 everyday-word names keep exact case");
            Check(File.ReadAllText(GlossaryStore.FilePath)==legacy,"reading a version 1 file does not rewrite it");
            GlossaryStore.Save(migrated);
            Check(File.ReadAllText(GlossaryStore.FilePath).Contains("\"version\":2")&&Find(GlossaryStore.Load(),"Apple","*").matchCase,"saving writes version 2 and keeps Match case");
            GlossaryStore.Save(new[]{new GlossaryEntry{source="Skills",replacement="技能",target="zh-CN"}});
            if(!String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DAFEIYU_FIXTURE_BACKEND"))){
                // Match UTF-8 Windows CI consoles. Framework Process creates an AutoFlush
                // stdin writer, which may emit a BOM before our explicit UTF-8 payload bytes.
                Console.InputEncoding=new UTF8Encoding(true);
                using(var worker=new Worker()){
                    Check(worker.Translate("Keep Skills with Claude.","auto:zh-CN",false).GetAwaiter().GetResult()=="Keep 技能 with Claude.","real Worker protocol carries glossary into Google engine with synthetic transport");
                    Check(worker.Translate("Keep Skills with Claude.","en_zh",true).GetAwaiter().GetResult()=="Keep 技能 with Claude.","real Worker protocol carries glossary into offline engine with synthetic transport");
                    GlossaryStore.Save(new[]{new GlossaryEntry{source="Skills",replacement="能力",target="zh-CN"}});
                    Check(worker.Translate("Keep Skills with Claude.","en_zh",true).GetAwaiter().GetResult()=="Keep 能力 with Claude.","next request uses edited dictionary while worker remains running");
                    GlossaryStore.Save(GlossaryStore.Defaults());
                    string technical="Claude Code context window; source code; API key. `context window` https://example.org/API";
                    string technicalChinese="Claude Code 上下文窗口; 源代码; API 密钥. `context window` https://example.org/API";
                    foreach(bool offline in new[]{false,true}){
                        Check(worker.Translate(technical,"auto:zh-CN",offline).GetAwaiter().GetResult()==technicalChinese,"complete defaults cross real Worker protocol, prefer longer API key, and protect code/URL in "+(offline?"offline":"online")+" fixture");
                        Check(worker.Translate(technical,"auto:en",offline).GetAwaiter().GetResult()==technical,"Chinese fixed terms do not leak into English target in "+(offline?"offline":"online")+" fixture");
                        Check(worker.Translate("上下文窗口","auto:en",offline).GetAwaiter().GetResult()=="context window","reverse technical phrase reaches "+(offline?"offline":"online")+" worker");
                    }
                    Check(worker.Translate(technical,"auto:ja",false).GetAwaiter().GetResult()==technical,"Chinese fixed terms do not leak into Japanese target");
                    string mixedCase="Open claude code, check the CONTEXT WINDOW and improve your skills with Skills.";
                    foreach(bool offline in new[]{false,true})
                        Check(worker.Translate(mixedCase,"auto:zh-CN",offline).GetAwaiter().GetResult()=="Open Claude Code, check the 上下文窗口 and improve your skills with Skills.","defaults ignore case but leave everyday words alone in "+(offline?"offline":"online")+" fixture");
                }
            }
            string before=File.ReadAllText(GlossaryStore.FilePath);
            bool refused=false;try{GlossaryStore.Save(new[]{new GlossaryEntry{source="",replacement="x",target="*"}});}catch(InvalidOperationException){refused=true;}
            Check(refused&&File.ReadAllText(GlossaryStore.FilePath)==before,"invalid edit preserves previous dictionary");
            var full=new List<GlossaryEntry>();for(int i=0;i<GlossaryStore.MaxEntries;i++)full.Add(new GlossaryEntry{source="CustomTerm"+i,replacement="CustomValue"+i,target="*"});
            string fullBefore=EntriesText(full);refused=false;try{GlossaryStore.MergeRecommended(full);}catch(InvalidOperationException){refused=true;}
            Check(refused&&EntriesText(full)==fullBefore&&File.ReadAllText(GlossaryStore.FilePath)==before,"overflow refuses the merge without partial edit or save");
            string corrupt="{\"entries\":\"invalid\"}";File.WriteAllText(GlossaryStore.FilePath,corrupt,new UTF8Encoding(false));
            refused=false;try{GlossaryStore.Load();}catch(InvalidOperationException){refused=true;}
            Check(refused&&File.ReadAllText(GlossaryStore.FilePath)==corrupt,"corrupt dictionary is reported without overwriting it with recommendations");
            File.WriteAllText(GlossaryStore.FilePath,before,new UTF8Encoding(false));
            GlossaryStore.Save(new GlossaryEntry[0]);Check(GlossaryStore.Load().Count==0,"empty glossary disables all defaults explicitly");
            Check(GlossaryStore.Load().Count==0,"reading an explicitly empty glossary never restores recommended terms");
            var history=new TranslationHistory();Check(!history.CanPrevious&&!history.CanNext,"initial history has no navigation");
            history.Add("one","一","auto:zh-CN",false);history.Add("two","二","auto:zh-CN",true);
            Check(history.Current.Source=="two"&&history.CanPrevious&&!history.CanNext,"latest result is current");
            Check(history.Move(-1).Result=="一"&&history.CanNext,"previous result exact");
            Check(history.Move(1).Offline,"engine retained");
            history.LeaveView();Check(history.Move(-1).Source=="two","pending selection can return to last successful result");
            for(int i=0;i<30;i++)history.Add("source"+i,"result"+i,"auto:en",false);
            Check(history.Count==20,"session entry count bounded");
            for(int i=0;i<20;i++)history.Add(new string('x',32000),new string('y',32000),"auto:en",false);
            Check(history.Count==4,"session character total bounded");history.Clear();Check(history.Count==0,"closing can clear memory");
            Preferences.Offline=false;Preferences.Pin=false;Ui.English=true;
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new CompanionForm(true)){
                var session=Get<TranslationHistory>(form,"history");session.Add("source one","result one","auto:en",false);session.Add("source two","result two","auto:fr",true);
                int beforeEpoch=Get<int>(form,"epoch");Call(form,"NavigateHistory",-1);
                Check(Get<int>(form,"epoch")>beforeEpoch,"history invalidates in-flight result epoch");
                Check(Get<TextBox>(form,"original").Text=="source one"&&Get<TextBox>(form,"translated").Text=="result one","history restores both panes");
                var snapshot=Get<Snapshot>(form,"latest");Check(snapshot.Conversation=="history"&&Get<bool>(form,"needsFreshSelection"),"history cannot be mistaken for new source selection");
                Check(!ResultGate.CanShow(beforeEpoch,Get<int>(form,"epoch"),snapshot,snapshot),"late worker result rejected after history navigation");
                Call(form,"NavigateHistory",1);Check(Get<TextBox>(form,"translated").Text=="result two","next restores forward entry");
                Check(Get<Label>(form,"status").Text.Contains("Offline"),"historical engine clearly labelled");
                Check(!form.Visible,"fixture form never shown");
                Check(Get<Button>(form,"previousResult").AccessibleName=="Previous translation"&&Get<Button>(form,"nextResult").AccessibleName=="Next translation","history arrows expose English accessible names");
                form.Size=new Size(660,900);Render(form,"translator-090-en.png");
                Ui.SetEnglish(false);Check(Get<Button>(form,"previousResult").Text=="‹"&&Get<Button>(form,"previousResult").AccessibleName=="上一条译文"&&Get<Button>(form,"nextResult").AccessibleName=="下一条译文","compact history arrows retain Chinese accessible names");
                Render(form,"translator-090-zh.png");
                Check(Directory.GetFiles(fixture).Length==1,"history has no disk persistence");
                form.Close();
            }
            GlossaryStore.Save(new[]{new GlossaryEntry{source="Claude",replacement="我的助手",target="*"}});
            string editorBefore=File.ReadAllText(GlossaryStore.FilePath);
            using(var glossary=new GlossaryForm()){
                Call(glossary,"AddRecommended");
                var staged=(List<GlossaryEntry>)Call(glossary,"ReadRows");
                Check(staged.Count>1&&Find(staged,"Claude","*").replacement=="我的助手","editor stages recommendations while retaining custom replacement");
                Check(Find(staged,"Skills","*")!=null&&Find(staged,"Skills","*").matchCase&&!Find(staged,"Claude Code","*").matchCase,"editor round-trips the Match case column");
                Check(File.ReadAllText(GlossaryStore.FilePath)==editorBefore,"adding recommendations in editor does not save them");
            }
            Check(File.ReadAllText(GlossaryStore.FilePath)==editorBefore,"closing editor without Save discards staged recommendations");
            using(var glossary=new GlossaryForm()){Call(glossary,"AddRecommended");Call(glossary,"SaveRows");}
            Check(GlossaryStore.Load().Count>1&&Find(GlossaryStore.Load(),"Claude","*").replacement=="我的助手","Save applies staged recommendations and retains custom replacement");
            GlossaryStore.Save(GlossaryStore.Defaults());
            foreach(bool english in new[]{true,false}){Ui.English=english;using(var glossary=new GlossaryForm()){
                Check(!glossary.Visible,"glossary editor initially hidden");Render(glossary,english?"glossary-090-en.png":"glossary-090-zh.png");
                glossary.Size=glossary.MinimumSize;Render(glossary,english?"glossary-091-min-en.png":"glossary-091-min-zh.png");
                var actions=(FlowLayoutPanel)((TableLayoutPanel)glossary.Controls[0]).GetControlFromPosition(0,2);
                foreach(Control button in actions.Controls)Check(button.Left>=0&&button.Top>=0&&button.Right<=actions.ClientSize.Width&&button.Bottom<=actions.ClientSize.Height,"glossary action remains within minimum-size editor: "+button.Text);
            }}
            Console.WriteLine("GlossaryHistoryTests passed: "+checks+" isolated checks; no desktop selection, clipboard or network used.");return 0;
        }catch(Exception error){Console.WriteLine(error.GetBaseException());return 1;}
        finally{Console.InputEncoding=originalInputEncoding;}
    }
}
