using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ClaudeCompanion;

// Isolated control contracts: no user windows, clipboard, translation or network.
internal static class ThemeUiTests {
    private static int checks;
    private static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    private static T Field<T>(object owner,string name){return (T)owner.GetType().GetField(name,Hidden).GetValue(owner);}
    private static object Call(object owner,string name,params object[] values){return owner.GetType().GetMethod(name,Hidden).Invoke(owner,values);}
    [STAThread] public static int Main(){
        string prior=Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR");bool priorEnglish=Ui.English;
        try{
            Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR",Path.Combine(Path.GetTempPath(),"dafeiyu-theme-fixture-"+Guid.NewGuid().ToString("N")));
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            CheckComboContract();CheckSwitchContract();CheckPaintCorners();CheckGlossaryMenu();
            Console.WriteLine("Theme UI tests passed: "+checks+" checks (isolated controls; no desktop automation).");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{Environment.SetEnvironmentVariable("DAFEIYU_DATA_DIR",prior);Ui.English=priorEnglish;}
    }
    private static void CheckComboContract(){
        using(var form=new Form{ShowInTaskbar=false,Location=new Point(-30000,-30000)})
        using(var combo=new ThemedComboBox{Size=new Size(210,32)}){
            form.Controls.Add(combo);combo.Items.Add(new LanguageChoice("en","English"));combo.Items.Add(new LanguageChoice("zh-CN","简体中文"));combo.Items.Add(new LanguageChoice("fr","Français"));
            combo.SelectedIndex=0;int changed=0;combo.SelectedIndexChanged+=delegate{changed++;};
            using(var menu=combo.BuildChoicesMenu()){
                Check(menu.Renderer is CreamMenuRenderer&&menu.Items.Count==3,"custom menu preserves every language choice");
                Check(((ToolStripMenuItem)menu.Items[0]).Checked,"current choice is visibly checked");
                Check(menu.Items[1].Text=="简体中文","display labels are not language codes");
                menu.Items[1].PerformClick();Check(changed==1&&combo.SelectedIndex==1,"one menu choice produces one selection event");
                Check(((LanguageChoice)combo.SelectedItem).Code=="zh-CN","selection keeps the original typed language object");
                menu.Items[1].PerformClick();Check(changed==1,"reselecting current choice does not duplicate change events");
                foreach(ToolStripItem item in menu.Items)Check(item.Height>=TextRenderer.MeasureText(item.Text,menu.Font).Height,"DPI-scaled menu row contains its font");
            }
            using(var cancelled=combo.BuildChoicesMenu()){cancelled.Close();}
            Check(combo.SelectedIndex==1&&changed==1,"closing a menu without choice preserves selection");
            IntPtr handle=combo.Handle;
            SendMessage(handle,0x14F,new IntPtr(1),IntPtr.Zero);
            Check(!combo.DroppedDown,"native dropdown never opens through CB_SHOWDROPDOWN");
            Check(Field<ContextMenuStrip>(combo,"popup")==null,"hidden parent cannot open popup");
            Message message=Message.Create(handle,0x100,IntPtr.Zero,IntPtr.Zero);
            object[] arguments={message,Keys.F4};
            Check((bool)typeof(ThemedComboBox).GetMethod("ProcessCmdKey",Hidden).Invoke(combo,arguments),"F4 routes to custom dropdown");
            arguments=new object[]{message,Keys.Alt|Keys.Down};
            Check((bool)typeof(ThemedComboBox).GetMethod("ProcessCmdKey",Hidden).Invoke(combo,arguments),"Alt+Down routes to custom dropdown");
            combo.Enabled=false;combo.ShowChoices();Check(Field<ContextMenuStrip>(combo,"popup")==null,"disabled combo never opens popup");
            Check(changed==1,"open/close/keyboard attempts do not mutate selected value");
        }
    }
    private static void CheckSwitchContract(){
        using(var control=new SelectionSwitch{Text="Selection",Size=new Size(120,28)}){
            int clicks=0;control.Click+=delegate{clicks++;control.Selected=!control.Selected;};
            Check((control.AccessibilityObject.State&AccessibleStates.Checked)==0,"new switch exposes unchecked state");
            control.AccessibilityObject.DoDefaultAction();Check(clicks==1&&control.Selected,"accessible action runs existing click handler once");
            Check((control.AccessibilityObject.State&AccessibleStates.Checked)!=0,"selected switch exposes checked state");
            control.Enabled=false;control.AccessibilityObject.DoDefaultAction();Check(clicks==1&&control.Selected,"disabled accessible action does not toggle state");
        }
    }
    private static void CheckPaintCorners(){
        using(var parent=new Panel{BackColor=Theme.Cream,Size=new Size(300,200)}){
            foreach(Control control in new Control[]{Theme.Button("Test",true),Theme.Button("测试",false),new RoundedCard(),new SelectionSwitch(),new ThemedComboBox()}){
                using(control){control.Size=new Size(200,45);parent.Controls.Add(control);
                    foreach(bool enabled in new[]{true,false}){control.Enabled=enabled;using(var image=new Bitmap(control.Width,control.Height)){
                        control.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
                        Check(image.GetPixel(0,0).ToArgb()==Theme.Cream.ToArgb(),"painted corners use parent colour, including disabled controls");
                    }}parent.Controls.Remove(control);
                }
            }
        }
    }
    private static void CheckGlossaryMenu(){
        GlossaryStore.Save(new[]{new GlossaryEntry{source="Custom term",replacement="自定义",target="*"}});
        string saved=File.ReadAllText(GlossaryStore.FilePath);
        foreach(bool english in new[]{true,false}){
            Ui.English=english;
            using(var form=new GlossaryForm()){
                var grid=Field<DataGridView>(form,"grid");var column=(DataGridViewComboBoxColumn)grid.Columns["target"];
                IntPtr handle=form.Handle;Size initialSize=form.Size;Call(form,"OnLoad",EventArgs.Empty);form.PerformLayout();
                var layout=Field<TableLayoutPanel>(form,"layout");var actions=Field<FlowLayoutPanel>(form,"actions");
                var note=(Label)layout.GetControlFromPosition(0,0);
                Check(form.Size==initialSize,"glossary layout fix never enlarges the form");
                Check(note.Height>=note.GetPreferredSize(new Size(note.Width,0)).Height,"all glossary explanation lines fit at current DPI");
                Check(grid.ColumnHeadersHeight>=TextRenderer.MeasureText("Term / phrase",grid.Font).Height,"grid header text fits at current DPI");
                Check(grid.RowTemplate.Height>=TextRenderer.MeasureText("All languages",grid.Font).Height+Theme.Scale(form,8),"target capsule row keeps readable vertical padding");
                foreach(Control button in actions.Controls)Check(button.Width>=TextRenderer.MeasureText(button.Text,button.Font).Width+Theme.Scale(form,20),"footer button labels fit without ellipsis");
                Check(column.ReadOnly&&column.DisplayStyle==DataGridViewComboBoxDisplayStyle.Nothing,"glossary cannot activate native combo editor");
                using(var menu=(ContextMenuStrip)Call(form,"BuildTargetMenu",0)){
                    Check(menu.Renderer is CreamMenuRenderer&&menu.Items.Count==LanguageChoice.All.Length+1,"glossary uses all existing target values in themed menu");
                    Check(menu.Items[0].Text==(english?"All languages":"所有语言"),"wildcard label follows UI language");
                    foreach(ToolStripItem item in menu.Items)if((string)item.Tag=="en")item.PerformClick();
                    Check(Convert.ToString(grid.Rows[0].Cells["target"].Value)=="en","glossary dropdown writes stable language code");
                    Check(File.ReadAllText(GlossaryStore.FilePath)==saved,"choosing a target does not save until Save");
                }
                using(var cancelled=(ContextMenuStrip)Call(form,"BuildTargetMenu",0)){cancelled.Close();}
                Check(Convert.ToString(grid.Rows[0].Cells["target"].Value)=="en","dismissed target menu keeps staged value");
                Check(!form.Visible,"fixture never shows a user-facing window");
            }
            Check(File.ReadAllText(GlossaryStore.FilePath)==saved,"closing glossary preserves saved dictionary");
        }
    }
}
