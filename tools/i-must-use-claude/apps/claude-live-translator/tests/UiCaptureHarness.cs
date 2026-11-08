using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Synthetic visual fixtures only: no real windows, clipboard, network or sends.
// Native capture uses the current monitor DPI. Smaller copies are explicitly
// labelled image-scale previews, not independently tested Windows DPI settings.
internal static class UiCaptureHarness {
    static Assembly app;
    static string output;
    static readonly List<string> log=new List<string>();
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr window,int index,int value);
    static Type Type(string name){return app.GetType("ClaudeCompanion."+name,true);}
    static object New(string name,params object[] args){return Activator.CreateInstance(Type(name),Flags,null,args,null);}
    static void Set(object value,string name,object field){value.GetType().GetField(name,Flags).SetValue(value,field);}
    static object Get(object value,string name){return value.GetType().GetField(name,Flags).GetValue(value);}
    static object Call(object value,string name,params object[] args){return value.GetType().GetMethod(name,Flags).Invoke(value,args);}
    static void Static(string type,string name,object value){Type(type).GetField(name,Flags).SetValue(null,value);}
    static void Pump(){for(int i=0;i<5;i++){Application.DoEvents();Thread.Sleep(20);}}
    static object Source(Form owner,string application,string title){
        var source=New("SourceWindow");Set(source,"Handle",owner.Handle);Set(source,"App",application);Set(source,"Title",title);
        using(var process=Process.GetCurrentProcess()){Set(source,"ProcessId",process.Id);Set(source,"Started",process.StartTime.ToUniversalTime().Ticks);}
        return source;
    }
    static void ShowFixture(Form form){
        form.TopMost=false;form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-10000,-10000);
        IntPtr handle=form.Handle;
        SetWindowLong(handle,-20,GetWindowLong(handle,-20)|0x08000000); // WS_EX_NOACTIVATE
        form.Show();Pump();
    }
    static void Save(Control control,string name){
        uint dpi=GetDpiForWindow(control.Handle);if(dpi==0)dpi=96;
        string nativeDir=Path.Combine(output,"native-"+(dpi*100/96));Directory.CreateDirectory(nativeDir);
        using(var bitmap=new Bitmap(control.Width,control.Height)){
            bitmap.SetResolution(dpi,dpi);
            // WM_PRINT through DrawToBitmap is deterministic for our hidden
            // fixtures. This renders classic non-client chrome, not DWM chrome.
            control.DrawToBitmap(bitmap,new Rectangle(Point.Empty,control.Size));
            bitmap.Save(Path.Combine(nativeDir,name+".png"));
            foreach(int percent in new[]{100,125,150}){
                string scaled=Path.Combine(output,"scaled-preview-"+percent);Directory.CreateDirectory(scaled);
                float factor=(percent/100F)/(dpi/96F);
                using(var resized=new Bitmap(Math.Max(1,(int)Math.Round(bitmap.Width*factor)),Math.Max(1,(int)Math.Round(bitmap.Height*factor))))
                using(var g=Graphics.FromImage(resized)){
                    g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    g.DrawImage(bitmap,new Rectangle(Point.Empty,resized.Size));resized.Save(Path.Combine(scaled,name+".png"));
                }
            }
        }
        log.Add(name+" | "+control.Width+"x"+control.Height+" | native DPI="+dpi+" | icon="+(control is Form?((Form)control).Icon!=null:true));
        Console.WriteLine(log[log.Count-1]);
        var bounds=new List<string>();DumpBounds(control,control,0,bounds);
        File.WriteAllLines(Path.Combine(output,name+"-bounds.txt"),bounds);
    }
    static void DumpBounds(Control root,Control control,int depth,List<string> rows){
        string field="";foreach(var candidate in root.GetType().GetFields(Flags)){if(candidate.GetValue(root)==control){field=candidate.Name;break;}}
        rows.Add(new String(' ',depth*2)+control.GetType().Name+" "+field+" "+control.Bounds+" client="+control.ClientSize+" visible="+control.Visible+" dock="+control.Dock+" anchor="+control.Anchor+" font="+control.Font+" text="+control.Text.Replace('\r',' ').Replace('\n',' '));
        foreach(Control child in control.Controls)DumpBounds(root,child,depth+1,rows);
    }
    static void Capture(Form form,string name){
        ShowFixture(form);Save(form,name);
        if((name.EndsWith("-main-online-history")||name.EndsWith("-picker")||name.EndsWith("-compose")||name.EndsWith("-language-packs"))&&form.MinimumSize.Width>0&&form.MinimumSize.Height>0){
            form.Size=form.MinimumSize;Pump();Save(form,name+"-minimum");
        }
        form.Close();Pump();
    }
    static void CaptureMenu(ContextMenuStrip menu,string name){
        // Never Show a popup: Windows clamps popup coordinates onto the user's
        // monitor even when the fixture owner is offscreen.
        Call(menu,"OnOpening",new CancelEventArgs());
        menu.CreateControl();menu.PerformLayout();menu.Size=menu.GetPreferredSize(Size.Empty);menu.PerformLayout();Save(menu,name);
    }
    static object Pack(string from,string to){
        var pack=New("LanguagePack");Set(pack,"From",from);Set(pack,"To",to);Set(pack,"FromName",from=="en"?"English":"Chinese");Set(pack,"ToName",to=="en"?"English":"Chinese");Set(pack,"Version","1.9");Set(pack,"Id","translate-"+from+"_"+to+"-1_9");Set(pack,"Installed",true);return pack;
    }
    static void PackFixtures(){
        string data=Path.Combine(output,"isolated-settings");Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR",data);
        foreach(string pair in new[]{"en_zh","zh_en"}){
            string id="translate-"+pair+"-1_9",folder=Path.Combine(data,"packages",id);Directory.CreateDirectory(Path.Combine(folder,"model"));
            var codes=pair.Split('_');
            File.WriteAllText(Path.Combine(folder,"metadata.json"),"{\"from_code\":\""+codes[0]+"\",\"to_code\":\""+codes[1]+"\",\"from_name\":\""+(codes[0]=="en"?"English":"Chinese")+"\",\"to_name\":\""+(codes[1]=="en"?"English":"Chinese")+"\",\"package_version\":\"1.9\"}");
            File.WriteAllText(Path.Combine(folder,".install.json"),"{\"id\":\""+id+"\",\"complete\":true}");
            foreach(string file in new[]{"sentencepiece.model","model/model.bin","model/config.json","model/shared_vocabulary.json"})File.WriteAllText(Path.Combine(folder,file),"Synthetic UI fixture only. Never use as a model.");
        }
    }
    static void FillMain(Form main,object source,bool selected,bool withHistory,bool english,bool offline){
        if(selected){Set(main,"locked",source);Set(main,"active",true);Call(main,"RefreshLockLabels");}
        if(!withHistory)return;
        string text="I will review the project structure first, then arrange the changes.\r\nWhen finished, I will explain what changed and keep the original files.";
        string result="我会先查看这个项目的结构，再整理需要调整的部分。\r\n\r\n完成后，会说明改了什么，并保留原始文件。";
        var snapshot=New("Snapshot");Set(snapshot,"Text",text);Set(snapshot,"Conversation","synthetic-preview");Set(snapshot,"Direction",offline?"pair:en:zh":"auto:zh-CN");Set(main,"latest",snapshot);
        ((TextBox)Get(main,"translated")).Text=result;((TextBox)Get(main,"original")).Text=text;
        for(int i=0;i<3;i++)Call(Get(main,"history"),"Add",text,result,"auto:zh-CN",offline);
        Call(main,"RefreshHistoryButtons");Call(main,"SetStatus",english?"Translation updated · 18:42":"译文已更新 · 18:42",false);
    }
    static void MainFixtures(bool english,object chosen){
        string lang=english?"en":"zh";
        foreach(bool offline in new[]{false,true})foreach(string state in new[]{"empty","selected","history"}){
            Static("Preferences","Offline",offline);Static("Preferences","OfflinePair","pair:en:zh");
            using(var main=(Form)New("CompanionForm",true)){
                FillMain(main,chosen,state!="empty",state=="history",english,offline);
                Capture(main,lang+"-main-"+(offline?"offline":"online")+"-"+state);
            }
        }
        Static("Preferences","Offline",false);
        using(var main=(Form)New("CompanionForm",true)){
            FillMain(main,chosen,true,true,english,false);ShowFixture(main);
            object model=Call(main,"CreateSettingsModel");using(var settings=(Form)New("SettingsPanel",model)){
                ShowFixture(settings);Save(settings,lang+"-settings");
                using(var choices=(ContextMenuStrip)Call(Get(settings,"targets"),"BuildChoicesMenu"))CaptureMenu(choices,lang+"-settings-language-menu");
                settings.Close();
            }
            using(var menu=(ContextMenuStrip)Call(main,"BuildModeMenu"))CaptureMenu(menu,lang+"-mode-menu");
            var original=(TextBox)Get(main,"original");if(original.ContextMenuStrip!=null)CaptureMenu(original.ContextMenuStrip,lang+"-text-menu");
            main.Close();
        }
    }
    static void OtherFixtures(bool english,Form fixture,object chosen){
        string lang=english?"en":"zh";
        Array windows=Array.CreateInstance(Type("SourceWindow"),10);string[] names={"claude","ChatGPT","chrome","winword","wps","acrobat","notepad","explorer","AsHotplugCtrl","AsMonitorControl"};
        var owners=new List<Form>();
        for(int i=0;i<names.Length;i++){
            Form owner=fixture;if(i>0){owner=new Form();owners.Add(owner);}
            windows.SetValue(Source(owner,names[i],names[i]+" · "+(english?"A document with a long title for reading":"一份用于阅读和翻译的文档窗口标题")),i);
        }
        using(var picker=(Form)New("SourceWindowPickerForm",chosen,windows))Capture(picker,lang+"-picker");
        using(var picker=(Form)New("SourceWindowPickerForm",chosen,windows)){
            ShowFixture(picker);((TextBox)Get(picker,"search")).Text="chrome";Pump();Save(picker,lang+"-picker-search");picker.Close();
        }
        foreach(var owner in owners)owner.Dispose();
        var choice=New("WindowChoice");Set(choice,"Handle",fixture.Handle.ToInt64().ToString());Set(choice,"Title",english?"Claude · Example project":"Claude · 示例项目");
        using(var compose=(Form)New("ComposeForm",choice,true))Capture(compose,lang+"-compose");
        using(var glossary=(Form)New("GlossaryForm")){
            ShowFixture(glossary);Save(glossary,lang+"-glossary");
            using(var menu=(ContextMenuStrip)Call(glossary,"BuildTargetMenu",0))CaptureMenu(menu,lang+"-glossary-language-menu");
            glossary.Close();
        }
        var packs=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Type("LanguagePack")));packs.Add(Pack("en","zh"));packs.Add(Pack("zh","en"));
        using(var manager=(Form)New("LanguagePacksForm",true)){Set(manager,"catalog",packs);Call(manager,"Populate");Capture(manager,lang+"-language-packs");}
        using(var retry=(Form)New("OfflineRetryForm",packs))Capture(retry,lang+"-offline-retry");
    }
    [STAThread] static int Main(string[] args){
        try{
            SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            app=Assembly.LoadFrom(Path.GetFullPath(args[0]));output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);PackFixtures();
            foreach(bool english in new[]{false,true}){
                Static("Ui","English",english);Static("Preferences","Pin",false);Static("Preferences","Target","zh-CN");
                using(var fixture=new Form()){
                    var chosen=Source(fixture,"claude",english?"Claude · Project refactor":"Claude · 规划重构");
                    if(args.Length<3||args[2]!="--others")MainFixtures(english,chosen);
                    OtherFixtures(english,fixture,chosen);
                }
            }
            if(args.Length>2&&args[2]!="--others"){
                var launcher=Assembly.LoadFrom(Path.GetFullPath(args[2]));var type=launcher.GetType("DafeiyuPortable.PrepareForm",true);
                foreach(bool english in new[]{false,true})using(var form=(Form)Activator.CreateInstance(type,Flags,null,new object[]{english},null)){
                    ShowFixture(form);Call(form,"SetProgress","正在准备文件 150 / 320… / Preparing files…");Pump();
                    Save(form,(english?"en":"zh")+"-launcher");Set(form,"finished",true);form.Close();
                }
            }
            File.WriteAllLines(Path.Combine(output,"capture-log.txt"),log);
            File.WriteAllText(Path.Combine(output,"CAPTURE-NOTES.txt"),"Native captures render actual Windows Forms controls offscreen at the current monitor DPI via DrawToBitmap. This produces classic non-client chrome; DWM titlebar appearance and taskbar presentation are not verified. No system display setting was changed. scaled-preview-100/125/150 are resampled visual size comparisons only: they are NOT independent real-DPI layout verification. Only synthetic source windows and sample texts are used. Popup menus are rendered without Show and never placed onscreen. No real window enumeration, clipboard content, translation request or send action is performed. Isolated package folders contain dummy UI fixtures, not usable offline models.\r\n");
            return 0;
        }catch(Exception error){Console.WriteLine(error);return 1;}
    }
}
