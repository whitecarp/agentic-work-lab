using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Actual WinForms rendering in isolated 96/144 DPI thread contexts. No resampling,
// system scaling changes, user window reads, input hooks, clipboard or network.
internal static class FavoritesCaptureHarness {
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static Assembly app;
    static string output;
    static uint expectedDpi;
    static readonly List<string> receipts=new List<string>();
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr window,int index,int value);
    static Type T(string name){return app.GetType("ClaudeCompanion."+name,true);}
    static object New(string name,params object[] args){return Activator.CreateInstance(T(name),Flags,null,args,null);}
    static object Get(object obj,string field){return obj.GetType().GetField(field,Flags).GetValue(obj);}
    static void Set(object obj,string field,object value){obj.GetType().GetField(field,Flags).SetValue(obj,value);}
    static object Call(object obj,string method,params object[] args){return obj.GetType().GetMethod(method,Flags).Invoke(obj,args);}
    static void Static(string name,string field,object value){T(name).GetField(field,Flags).SetValue(null,value);}
    static void Pump(){for(int i=0;i<4;i++){Application.DoEvents();Thread.Sleep(20);}}
    static void Show(Form form){
        form.TopMost=false;form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-14000,-14000);
        IntPtr h=form.Handle;SetWindowLong(h,-20,GetWindowLong(h,-20)|0x08000000);
        form.Show();Pump();
    }
    static void Save(Control control,string name){
        uint dpi=GetDpiForWindow(control.Handle);
        if(dpi!=expectedDpi)throw new InvalidOperationException("Unexpected actual control DPI: "+dpi+" expected "+expectedDpi);
        using(var image=new Bitmap(control.Width,control.Height)){
            image.SetResolution(dpi,dpi);control.DrawToBitmap(image,new Rectangle(Point.Empty,control.Size));image.Save(Path.Combine(output,name+".png"));
        }
        receipts.Add(name+" | "+control.Width+"x"+control.Height+" | actual window DPI="+dpi);
        var rows=new List<string>();Bounds(control,0,rows);File.WriteAllLines(Path.Combine(output,name+"-bounds.txt"),rows);
    }
    static void Bounds(Control control,int depth,List<string> rows){
        rows.Add(new String(' ',depth*2)+control.GetType().Name+" "+control.Name+" "+control.Bounds+" visible="+control.Visible+" font="+control.Font+" text="+control.Text.Replace('\r',' ').Replace('\n',' '));
        foreach(Control child in control.Controls)Bounds(child,depth+1,rows);
    }
    static void Menu(ContextMenuStrip menu,string name){
        // Never Show: Windows clamps even an offscreen popup onto the monitor.
        menu.CreateControl();menu.PerformLayout();menu.Size=menu.GetPreferredSize(Size.Empty);menu.PerformLayout();Save(menu,name);
    }
    [STAThread] static int Main(string[] args){
        try {
            int percent=Int32.Parse(args[2]);expectedDpi=(uint)(percent*96/100);
            SetThreadDpiAwarenessContext(new IntPtr(percent==100?-1:-2));
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
            Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR",Path.Combine(output,"isolated-settings"));
            app=Assembly.LoadFrom(Path.GetFullPath(args[0]));
            using(var probe=new Form()){
                probe.AutoScaleDimensions=new SizeF(96,96);probe.AutoScaleMode=AutoScaleMode.Dpi;probe.ClientSize=new Size(240,120);
                probe.Controls.Add(new Label{Text="DPI probe / 中文",Dock=DockStyle.Fill,Font=new Font("Microsoft YaHei UI",12)});
                Show(probe);Save(probe,"dpi-probe");probe.Close();
            }
            if(args.Length>3&&args[3]=="--probe")return 0;
            CaptureFavorites();
            CaptureReadme();
            File.WriteAllLines(Path.Combine(output,"capture-log.txt"),receipts);
            return 0;
        }catch(Exception error){Console.WriteLine(error);return 1;}
    }
    // README previews: the app's own fixed-sample RenderPreview paths, plus the glossary
    // editor showing an excerpt of the built-in terms. Synthetic data only; no network.
    static void CaptureReadme(){
        Static("Preferences","Pin",false);Static("Preferences","Offline",false);Static("Preferences","Target","zh-CN");
        var terms=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("GlossaryEntry")));
        object[][] rows={
            new object[]{"Claude Code","Claude Code","*",false},new object[]{"Codex","Codex","*",false},
            new object[]{"Skills","Skills","*",true},new object[]{"Sonnet","Sonnet","*",true},
            new object[]{"context window","上下文窗口","zh-CN",false},new object[]{"system prompt","系统提示词","zh-CN",false},
            new object[]{"rate limit","速率限制","zh-CN",false},new object[]{"提示词","prompt","en",false},new object[]{"代码审查","code review","en",false}};
        foreach(object[] row in rows){
            object term=New("GlossaryEntry");Set(term,"source",row[0]);Set(term,"replacement",row[1]);Set(term,"target",row[2]);Set(term,"matchCase",row[3]);terms.Add(term);
        }
        StaticCall("GlossaryStore","Save",terms);
        var packs=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("LanguagePack")));
        object pack=New("LanguagePack");Set(pack,"From","en");Set(pack,"To","zh");Set(pack,"FromName","English");Set(pack,"ToName","Chinese");Set(pack,"Installed",true);packs.Add(pack);
        foreach(bool english in new[]{false,true}){
            Static("Ui","English",english);string prefix=english?"en":"zh";
            CheckRetryStatusFits(prefix);
            object chat=New("WindowChoice");Set(chat,"Handle","0");Set(chat,"Title",english?"Demo chat":"演示聊天");
            using(var compose=(Form)New("ComposeForm",chat,true))Preview(compose,prefix+"-09-compose");
            using(var main=(Form)New("CompanionForm",true))Preview(main,prefix+"-10-offline-retry",true);
            using(var retry=(Form)New("OfflineRetryForm",packs))Preview(retry,prefix+"-11-retry-dialog");
            using(var editor=(Form)New("GlossaryForm")){
                Show(editor);var grid=(DataGridView)Get(editor,"grid");grid.ClearSelection();Pump();
                Check(grid.Columns.Contains("matchCase")&&grid.Rows.Count>=9,"Glossary editor shows the sample terms and the Match case column ("+prefix+")");
                Save(editor,prefix+"-12-glossary");editor.Close();
            }
        }
    }
    // Regression check: when Google is rate-limited, the status beside "Use offline" must not be cut off,
    // at the default size or at the minimum window size.
    static void CheckRetryStatusFits(string prefix){
        using(var main=(Form)New("CompanionForm",true)){
            Show(main);
            object snapshot=New("Snapshot");Set(snapshot,"Conversation","preview");Set(snapshot,"MessageId","retry");Set(snapshot,"Text","Keep the original files.");
            Set(snapshot,"Direction",main.GetType().GetProperty("SelectedDirection",Flags).GetValue(main,null));Set(main,"latest",snapshot);
            Check((bool)Call(main,"RecordOfflineRetry",snapshot,Get(main,"epoch"),false,New("TranslationFailure","rate_limited")),"Rate-limited Google offers the offline retry ("+prefix+")");
            var status=(Label)Get(main,"status");
            foreach(Size size in new[]{main.Size,main.MinimumSize}){
                main.Size=size;Pump();
                int need=TextRenderer.MeasureText(status.Text,status.Font).Width;
                Check(need<=status.ClientSize.Width,"Offline-retry status fits beside Use offline at "+size.Width+"x"+size.Height+" ("+prefix+": "+need+" <= "+status.ClientSize.Width+" px)");
            }
            main.Close();
        }
    }
    static void Preview(Form form,string name,params object[] extra){
        // RenderPreview fills the app's own fixed sample, draws the form and writes the PNG.
        var args=new List<object>{Path.Combine(output,name+".png")};args.AddRange(extra);
        form.GetType().GetMethod("RenderPreview",Flags).Invoke(form,args.ToArray());Pump();
        using(var image=Image.FromFile((string)args[0]))receipts.Add(name+" | "+image.Width+"x"+image.Height+" | app RenderPreview under the "+expectedDpi+"-DPI thread context");
    }
    static object StaticCall(string type,string method,params object[] args){return T(type).GetMethod(method,Flags).Invoke(null,args);}
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);receipts.Add("PASS: "+message);}
    static object Entry(string source,string result,string tag,int minutes){
        object entry=New("FavoriteEntry");
        Set(entry,"SourceTitle","Claude · Project notes");Set(entry,"SourceText",source);Set(entry,"Translation",result);
        Set(entry,"Engine","google");Set(entry,"Direction","auto:zh-CN");Set(entry,"Tag",tag);
        Set(entry,"Notes","演示备注：保留原始文件，再检查结果。 / Demo note: keep original files.");
        Set(entry,"CreatedAt",new DateTimeOffset(2026,11,1,18,40,0,TimeSpan.FromHours(-6)).AddMinutes(-minutes));return entry;
    }
    static void CaptureFavorites(){
        Static("Preferences","Pin",false);Static("Preferences","Offline",false);Static("Preferences","Target","zh-CN");
        foreach(bool english in new[]{false,true}){
            Static("Ui","English",english);string prefix=english?"en":"zh";
            string folder=Path.Combine(output,prefix+"-notebook-data");object store=New("FavoritesStore",folder);
            StaticCall("FavoritesPreferences","SetFolder",folder);
            foreach(string tag in new[]{"剪辑 Video","代码 Code","日常 Daily"})StaticCall("FavoritesPreferences","CreateTag",tag);
            T("FavoritesPreferences").GetProperty("LastTag",Flags).SetValue(null,"剪辑 Video",null);
            string source="Keep the original video and audio unchanged.\r\n\r\n1. Check every frame after export.\r\n2. Save the accepted result in your project folder.\r\n3. Keep the source files for later edits.";
            string result="保留原始视频和音频。\r\n\r\n1. 导出后逐帧检查。\r\n2. 将验收通过的成片保存在项目文件夹中。\r\n3. 保留源文件，方便以后修改。";
            using(var main=(Form)New("CompanionForm",true)){
                Show(main);Call(main,"ApplyFavoritePreview",source,result,"google","auto:zh-CN","Claude · Video editing");Pump();
                Check(!Directory.Exists(folder),"Opening and previewing a reply do not create a notebook folder ("+prefix+")");
                Save(main,prefix+"-01-before-save");
                Call(main,"ToggleFavorite");Pump();
                Check(Directory.GetFiles(folder,"*.md").Length==1,"Explicit Save writes exactly one Markdown file ("+prefix+")");
                Save(main,prefix+"-02-after-save");
                using(var menu=(ContextMenuStrip)Call(main,"BuildFavoriteTagMenu"))Menu(menu,prefix+"-03-tag-menu");
                using(var settings=(Form)New("SettingsPanel",Call(main,"CreateSettingsModel"))){Show(settings);Save(settings,prefix+"-04-settings");settings.Close();}
                main.Close();
            }
            Call(store,"Save",Entry("A clear title helps readers understand the update.","清楚的标题让读者更容易了解更新内容。","日常 Daily",10));
            Call(store,"Save",Entry("Run all tests before publishing the code.","发布代码前运行所有测试，并保留检查结果。","代码 Code",20));
            Call(store,"Save",Entry("Keep the video reference and the final export together.","把视频参考和最终导出文件保存在一起。","剪辑 Video",30));
            using(var book=(Form)New("FavoritesForm",store,true)){
                Show(book);Check(book.ClientSize==new Size((int)(600*expectedDpi/96),(int)(560*expectedDpi/96)),"Notebook stays 600 x 560 logical pixels ("+prefix+")");
                Save(book,prefix+"-05-notebook");
                ((TextBox)Get(book,"search")).Text=english?"video":"视频";Pump();
                Check(((ListBox)Get(book,"list")).Items.Count==2,"Bilingual search finds two video replies ("+prefix+")");
                Save(book,prefix+"-06-notebook-search");
                ((TextBox)Get(book,"search")).Text="no-match-xyz";Pump();Save(book,prefix+"-07-no-matches");book.Close();
            }
            object emptyStore=New("FavoritesStore",Path.Combine(output,prefix+"-empty-notebook"));
            using(var empty=(Form)New("FavoritesForm",emptyStore,true)){Show(empty);Save(empty,prefix+"-08-empty-notebook");empty.Close();}
        }
    }
}
