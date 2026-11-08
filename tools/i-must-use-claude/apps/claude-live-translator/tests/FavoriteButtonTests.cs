using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ClaudeCompanion;

internal static class FavoriteButtonTests {
    private static int checks;
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,Private).GetValue(value);}
    private static void Set(object value,string name,object field){value.GetType().GetField(name,Private).SetValue(value,field);}
    private static object Call(object value,string name,params object[] args){return value.GetType().GetMethod(name,Private).Invoke(value,args);}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
    private static bool LocallyVisible(Control control){return (bool)typeof(Control).GetMethod("GetState",Private).Invoke(control,new object[]{2});}
    private static int Files(string folder){return Directory.Exists(folder)?Directory.GetFiles(folder,"*.md").Length:0;}
    [STAThread] private static int Main(){
        try {
            string data=Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR");Check(!String.IsNullOrWhiteSpace(data),"isolated data directory required");
            string folder=Path.Combine(data,"saved-"+Guid.NewGuid().ToString("N"));FavoritesPreferences.SetFolder(folder);
            Ui.English=false;Preferences.Offline=false;Preferences.Pin=false;Preferences.Target="zh-CN";
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new CompanionForm(true)){
                Field<IDisposable>(form,"watcher").Dispose();
                var favorite=Field<Button>(form,"favoriteButton");
                Check(!favorite.Enabled,"instruction text cannot be saved");Call(form,"ToggleFavorite");Check(Files(folder)==0,"initial instructions never create markdown");
                Call(form,"ApplyFavoritePreview","Keep the original.","请保留原文。","google","auto:zh-CN","Claude · First chat");
                Check(favorite.Enabled&&!favorite.Text.Contains("★"),"successful reply enables explicit save");Check(Files(folder)==0,"rendering a reply never auto-saves");
                Call(form,"SetFavoriteTag","教程");Check(Files(folder)==0&&FavoritesPreferences.LastTag=="教程","tag selection stores only metadata before saving");
                using(var menu=(ContextMenuStrip)Call(form,"BuildFavoriteTagMenu")){
                    bool found=false;foreach(ToolStripItem item in menu.Items){var tag=item as ToolStripMenuItem;if(tag!=null&&tag.Text=="教程")found=tag.Checked;}
                    Check(found,"tag menu reflects the selected last-used tag");
                }
                Call(form,"ToggleFavorite");var store=new FavoritesStore(folder);var rows=store.Load();
                Check(rows.Count==1,"one click saves one markdown reply");var saved=rows[0];
                Check(saved.SourceText=="Keep the original."&&saved.Translation=="请保留原文。","source and translation stay paired");
                Check(saved.Engine=="google"&&saved.Direction=="auto:zh-CN"&&saved.SourceTitle=="Claude · First chat","saved context records generation-time metadata");
                Check(saved.Tag=="教程"&&favorite.Text.Contains("★")&&favorite.Text.Contains("教程"),"saved pill names its tag");
                Check(Field<Label>(form,"status").Text.Contains("回复收藏本"),"status explains where the reply was saved");
                Check(Field<TableLayoutPanel>(form,"footerLayout").GetColumnSpan(Field<Label>(form,"status"))==2&&!LocallyVisible(Field<Control>(form,"footerRight")),"save notice can use the entire footer");
                Call(form,"SetFavoriteTag","工作");rows=store.Load();Check(rows.Count==1&&rows[0].Tag=="工作","changing saved tag edits the same markdown");
                Call(form,"ToggleFavorite");Check(Files(folder)==0&&!favorite.Text.Contains("★"),"second click unsaves the corresponding markdown");
                Call(form,"ToggleFavorite");saved=store.Load()[0];File.Delete(saved.FilePath);Call(form,"RefreshFavoriteButton",true);
                Check(!favorite.Text.Contains("★"),"external deletion is reflected without rewriting the file");
                Check(Files(folder)==0,"refresh after external deletion does not save");
                Preferences.Offline=true;Preferences.Target="ja";Set(form,"locked",new SourceWindow {Handle=new IntPtr(900),Title="Different chat",App="claude"});
                Call(form,"ToggleFavorite");saved=store.Load()[0];
                Check(saved.Engine=="google"&&saved.Direction=="auto:zh-CN"&&saved.SourceTitle=="Claude · First chat","changing current mode or window never re-labels an older reply");
                Call(form,"ApplyFavoritePreview","Second source","第二条译文","offline","pair:en:zh","Document · Two");
                Call(form,"NavigateHistory",-1);Check(Field<TextBox>(form,"translated").Text=="请保留原文。","history restores the first translation");
                Check(favorite.Text.Contains("★"),"history recognizes its existing saved reply");
                Call(form,"ToggleFavorite");Check(Files(folder)==0,"unsave in history targets only that reply");
                Call(form,"NavigateHistory",1);Call(form,"ToggleFavorite");saved=store.Load()[0];
                Check(saved.Engine=="offline"&&saved.Direction=="pair:en:zh"&&saved.SourceTitle=="Document · Two","history saves its original offline context");
                Field<TextBox>(form,"original").Text="A different unsent source";Check(!favorite.Enabled,"mismatched original cannot be saved with an old result");
                Call(form,"ToggleFavorite");Check(Files(folder)==1,"mismatched display cannot toggle a saved reply");
                for(int i=0;i<27;i++)Call(form,"ApplyFavoritePreview","source "+i,"译文 "+i,"google","auto:zh-CN","Synthetic window");
                Check(Field<TranslationHistory>(form,"history").Count==20,"existing history remains bounded at twenty");
                Check(Field<IDictionary>(form,"favoriteHistory").Count==20&&Field<ICollection>(form,"favoriteHistoryOrder").Count==20,"favorite metadata releases evicted session replies");
                Check(Files(folder)==1,"twenty-seven successful previews still never auto-save");
                FavoritesPreferences.LastTag="收藏本内新标签";Call(form,"ToggleFavorite");
                Check(store.FindMatch("source 26","译文 26","google","auto:zh-CN","Synthetic window").Tag=="收藏本内新标签","next save uses the last tag changed in the notebook too");
                for(int i=0;i<5;i++)Call(form,"ApplyFavoritePreview",new string('a',30000)+i,new string('b',30000),"google","auto:zh-CN","Large synthetic reply");
                Check(Field<TranslationHistory>(form,"history").Count==4&&Field<IDictionary>(form,"favoriteHistory").Count==4,"metadata follows the history character limit as well as entry count");
                string blocked=Path.Combine(data,"blocked-"+Guid.NewGuid().ToString("N"));File.WriteAllText(blocked,"fixture");FavoritesPreferences.SetFolder(blocked);Call(form,"ToggleFavorite");
                Check(!favorite.Text.Contains("★")&&Field<Label>(form,"status").Text.Contains("收藏未完成"),"save failure never marks a reply saved or reports success");
                Check(File.ReadAllText(blocked)=="fixture","failed save preserves the conflicting file");FavoritesPreferences.SetFolder(folder);
                Ui.SetEnglish(true);Check(favorite.Text=="☆ Save","favorite action is localized in English");
                Check(Field<Button>(form,"favoriteTagButton").AccessibleName.Contains("tag"),"tag arrow has English accessible name");
                var settings=(SettingsPanelModel)Call(form,"CreateSettingsModel");Check(settings.OpenFavorites!=null,"settings delegates the notebook opener");
                Call(form,"SetStatus","Ordinary status",false);Check(Field<TableLayoutPanel>(form,"footerLayout").GetColumnSpan(Field<Label>(form,"status"))==1&&LocallyVisible(Field<Control>(form,"footerRight")),"ordinary statuses restore the original privacy footer");
                form.Close();
            }
            string settingsPath=Path.Combine(Preferences.DataDirectory,"favorites-settings.json");byte[] goodSettings=File.ReadAllBytes(settingsPath);
            const string damaged="{ intentionally invalid favorites configuration";File.WriteAllText(settingsPath,damaged);
            try {
                using(var form=new CompanionForm(true)){
                    Field<IDisposable>(form,"watcher").Dispose();
                    Check(!Field<Button>(form,"favoriteButton").Enabled,"bad favorites settings do not prevent main-window startup");
                    Call(form,"ApplyFavoritePreview","This reply still works.","这条译文仍然可以显示。","google","auto:zh-CN","Synthetic Claude");
                    Check(Field<TextBox>(form,"translated").Text=="这条译文仍然可以显示。"&&Field<TextBox>(form,"original").Text=="This reply still works.","favorites failure cannot block source or translated reply display");
                    Check(Field<Button>(form,"copy").Enabled&&!Field<Button>(form,"favoriteButton").Enabled&&!Field<Button>(form,"favoriteTagButton").Enabled,"only favorites are disabled; existing copy action remains available");
                    Call(form,"ShowFavoriteTagMenu");Call(form,"ToggleFavorite");Call(form,"RefreshFavoriteButton",true);
                    Check(((SettingsPanelModel)Call(form,"CreateSettingsModel")).OpenFavorites!=null,"settings remain accessible with broken favorites configuration");
                    Check(File.ReadAllText(settingsPath)==damaged,"automatic refresh, failed save and tag menu never overwrite corrupt favorites settings");
                    File.WriteAllBytes(settingsPath,goodSettings);Call(form,"RefreshFavoriteButton",true);
                    Check(Field<Button>(form,"favoriteButton").Enabled,"favorite controls recover after external configuration repair");form.Close();
                }
            }finally{File.WriteAllBytes(settingsPath,goodSettings);}
            Console.WriteLine("FavoriteButtonTests passed: "+checks+" checks; synthetic replies, isolated Markdown and no network or clipboard.");return 0;
        }catch(Exception error){Console.WriteLine("FavoriteButtonTests failed: "+error.GetBaseException().Message);Console.WriteLine(error.GetBaseException().StackTrace);return 1;}
    }
}
