using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WhaleClaudeHelper;

internal static class MaintenanceUiFixtureTests {
    private static int checks;
    private static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
    private static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(var child in All(c))yield return child;}}
    private static object Field(object obj,string name){return obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);}
    [STAThread] private static int Main(string[] args){
        try{Run(args);return 0;}catch(Exception ex){Console.Error.WriteLine("FAIL: "+ex.Message);Console.Error.WriteLine(ex.StackTrace);return 1;}
    }
    private static void Run(string[] args){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var framework=Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute),false).Cast<System.Runtime.Versioning.TargetFrameworkAttribute>().Single();
        Check(framework.FrameworkName==".NETFramework,Version=v4.8","runtime targets .NET 4.8");
        Check(System.Net.ServicePointManager.SecurityProtocol==System.Net.SecurityProtocolType.SystemDefault,"HTTPS uses operating system TLS defaults without a network request");
        foreach(bool english in new[]{false,true}){
            Ui.English=english;
            using(var form=new MaintenanceForm(true)){
                Check(!form.Visible,"fixture never opens a user window");
                Check(Field(form,"repair")==null&&Field(form,"cache")==null&&Field(form,"diagnostics")==null,"demo never constructs live services");
                var tabs=All(form).OfType<TabControl>().Single();
                Check(tabs.TabCount==4,"four maintenance pages available");
                Check(tabs.TabPages[0].Text==(english?"Connection & installation":"连接与安装"),"localized diagnostic page");
                Check(tabs.TabPages[2].Text==(english?"Caches & logs":"缓存与日志"),"localized cache page");
                Check(!All(form).OfType<CheckBox>().Any(x=>x.Checked||x.Enabled),"no mutation selected in fixture");
                var list=All(form).OfType<ListView>().Single();
                Check(list.Items.Count==1&&!((CacheTarget)list.Items[0].Tag).Eligible,"sample cache cannot be selected");
                var selecting=new ItemCheckEventArgs(0,CheckState.Checked,CheckState.Unchecked);
                typeof(ListView).GetMethod("OnItemCheck",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(list,new object[]{selecting});
                Check(selecting.NewValue==CheckState.Unchecked,"ineligible cache checkbox is rejected");
                var click=typeof(Button).GetMethod("OnClick",BindingFlags.NonPublic|BindingFlags.Instance);
                foreach(var button in All(form).OfType<Button>().Where(x=>x.Text!=(english?"Cancel check":"取消检查")&&x.Text!="English"&&x.Text!="中文").ToArray())click.Invoke(button,new object[]{EventArgs.Empty});
                Check(!(bool)Field(form,"busy"),"demo buttons never dispatch work");
                Check(Field(form,"configRoot")==null,"demo does not resolve personal config directory");
                Check(All(form).OfType<TextBox>().All(x=>x.ReadOnly),"results cannot edit source data");
                click.Invoke(Field(form,"language"),new object[]{EventArgs.Empty});
                Check(Ui.English!=english,"demo language toggle works without saving preferences");
                click.Invoke(Field(form,"language"),new object[]{EventArgs.Empty});
                tabs=All(form).OfType<TabControl>().Single();
                // Optional image is rendered offscreen; Show/ShowDialog is never called.
                if(args.Length>0){
                    tabs.SelectedIndex=2;
                    var surface=form.Controls[0];surface.Font=form.Font;surface.BackColor=form.BackColor;surface.ForeColor=form.ForeColor;form.Controls.Remove(surface);surface.Size=form.ClientSize;
                    surface.CreateControl();surface.PerformLayout();
                    using(var bitmap=new Bitmap(surface.Width,surface.Height)){surface.DrawToBitmap(bitmap,new Rectangle(Point.Empty,surface.Size));bitmap.Save(Path.Combine(args[0],english?"maintenance-demo-en.png":"maintenance-demo-zh.png"));}
                    surface.Dispose();
                }
            }
            using(var main=new OptimizerForm(true)){
                Check(All(main).OfType<LinkLabel>().Any(x=>x.Text==(english?"Maintenance & cleanup":"维护与清理")),"main screen exposes maintenance");
                Check(Field(main,"service")==null&&!main.Visible,"main demo stays isolated");
                Check(main.Text.Contains("0.7.0"),"new version visible");
                Check(All(main).OfType<LinkLabel>().Any(x=>x.Text==(english?"Codex connection repair":"Codex 连接修复")),"generic Codex proxy entry visible");
                Check(All(main).OfType<LinkLabel>().Any(x=>x.Text==(english?"Check updates":"检查更新")),"manual update entry is localized");
            }
        }
        Console.WriteLine("PASS "+checks+" isolated UI checks. No windows shown; no live probes or mutations.");
    }
}
