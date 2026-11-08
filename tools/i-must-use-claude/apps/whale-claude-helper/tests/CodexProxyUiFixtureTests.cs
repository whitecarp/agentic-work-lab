using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WhaleClaudeHelper;

internal static class CodexProxyUiFixtureTests {
    private static int checks;
    private static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
    private static object Field(object value,string name){return value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    private static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(var item in All(c))yield return item;}}
    private static ProxyProbeResult Probe(string host,int port,bool http,bool socks){return new ProxyProbeResult{Endpoint=new ProxyEndpoint{Host=host,Port=port},Http=http,Socks5=socks};}
    [STAThread] private static int Main(string[] args){
        try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Run(args.Length==0?null:args[0]);Console.WriteLine("PASS "+checks+" proxy UI/plan fixture checks; no user configuration, network or visible windows.");return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    private static void Run(string output){
        var http=Probe("127.0.0.1",12345,true,false);var mixed=Probe("127.0.0.1",34567,true,true);var socks=Probe("::1",54321,false,true);
        var plan=CodexProxyPlan.Create(http,null,false);Check(plan.HttpUri=="http://127.0.0.1:12345"&&plan.AllUri==plan.HttpUri,"HTTP-only compatible fallback");
        plan=CodexProxyPlan.Create(mixed,null,false);Check(plan.AllUri=="socks5h://127.0.0.1:34567","mixed automatic SOCKS5");
        plan=CodexProxyPlan.Create(http,socks,true);Check(plan.AllUri=="socks5h://[::1]:54321","separate IPv6 SOCKS5 arbitrary port");
        foreach(var item in new[]{socks,Probe("127.0.0.1",11111,false,false),null}){bool failed=false;try{CodexProxyPlan.Create(item,null,false);}catch(InvalidOperationException){failed=true;}Check(failed,"SOCKS-only and unverified HTTP refused");}
        bool invalid=false;try{CodexProxyPlan.Create(http,Probe("127.0.0.1",10000,true,false),true);}catch(InvalidOperationException){invalid=true;}Check(invalid,"explicit SOCKS must verify");
        invalid=false;try{CodexProxyPlan.Create(Probe("192.0.2.1",1234,true,false),null,false);}catch(InvalidOperationException){invalid=true;}Check(invalid,"plan rejects injected remote address");
        foreach(bool english in new[]{false,true}){
            Ui.English=english;
            using(var form=new CodexProxyForm(true)){
                Check(!form.Visible&&Field(form,"configRoot")==null&&Field(form,"backupRoot")==null,"demo resolves no personal directories");
                Check(!form.ShowInTaskbar&&form.Icon!=null,"proxy dialog shares application identity without default taskbar entry");
                Check(((ComboBox)Field(form,"candidates")).Items.Count==0,"demo does not discover live ports");
                Check(!((Button)Field(form,"apply")).Enabled,"write gated until verified");
                Check(((TextBox)Field(form,"report")).ReadOnly,"read-only result presentation");
                var tabs=(TabControl)Field(form,"tabs");Check(tabs.TabCount==2,"connection and undo tabs");
                typeof(CodexProxyForm).GetField("verified",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(form,plan);
                ((TextBox)Field(form,"httpPort")).Text="22222";Check(Field(form,"verified")==null,"editing invalidates verified plan");
                foreach(var button in All(form).OfType<Button>().Where(x=>x.Text!="English"&&x.Text!="中文"&&x.Text!=(english?"Cancel check":"取消检测"))){typeof(Button).GetMethod("OnClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(button,new object[]{EventArgs.Empty});}
                Check(!(bool)Field(form,"busy"),"demo actions never dispatch live work");
                if(output!=null){Directory.CreateDirectory(output);Render(form,Path.Combine(output,"proxy-"+(english?"en":"zh")+".png"),0);}
            }
            using(var undo=new CodexProxyForm(true)){if(output!=null)Render(undo,Path.Combine(output,"undo-"+(english?"en":"zh")+".png"),1);}
            using(var main=new OptimizerForm(true)){if(output!=null)Render(main,Path.Combine(output,"helper-main-"+(english?"en":"zh")+".png"),-1);}
            using(var closing=new ProcessCloseDialog(true)){Check(!closing.Visible&&!closing.ShowInTaskbar&&closing.Icon!=null,"process dialog icon and hidden demo");if(output!=null)Render(closing,Path.Combine(output,"close-apps-"+(english?"en":"zh")+".png"),-1);}
            string failure=CodexProxyForm.FailureText(new CodexProxyChange{Success=false,Changed=true,Error="codex_concurrent_edit_preserved"});
            Check(failure.Contains(english?"Configuration changed":"配置已发生变更"),"partial mutation reported clearly");
            Check(failure.Contains(english?"backup":"备份"),"partial failure includes recovery guidance");
        }
    }
    private static void Render(Form form,string path,int selected){
        var tabs=All(form).OfType<TabControl>().FirstOrDefault();if(tabs!=null)tabs.SelectedIndex=selected;
        var surface=form.Controls[0];surface.Font=form.Font;surface.BackColor=form.BackColor;surface.ForeColor=form.ForeColor;
        form.Controls.Remove(surface);surface.Size=form.ClientSize;surface.CreateControl();surface.PerformLayout();
        using(var bitmap=new Bitmap(surface.Width,surface.Height)){surface.DrawToBitmap(bitmap,new Rectangle(Point.Empty,surface.Size));bitmap.Save(path);}
        surface.Dispose();
    }
}
