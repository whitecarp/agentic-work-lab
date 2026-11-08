using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClaudeCompanion {
    internal sealed class CompanionForm : Form {
        private ComboBox direction,engine;
        private Label subtitle,privacy;
        private bool changingOptions,needsFreshSelection;
        private Button toggle, paste, copy, checkConnection, chooseSource, clearSource, language;
        private Label sourceName;
        private PictureBox sourceIcon;
        private SourceWindowPickerForm sourcePicker;
        private bool pickingSource, sourceActiveBeforePick;
        private SourceWindow sourceBeforePick;
        private int sourceIconEpoch;
        private CheckBox pin;
        private TextBox translated, original;
        private Label status, googleStatus, lockInfo;
        private readonly List<Action> labels = new List<Action>();
        private readonly SelectionWatcher watcher = new SelectionWatcher();
        private readonly System.Windows.Forms.Timer lockTimer = new System.Windows.Forms.Timer { Interval=1000 };
        private readonly WaitTicker wait = new WaitTicker(), connectionWait = new WaitTicker();
        private readonly Worker worker = new Worker();
        private Worker connectionWorker;
        private Process composeReaderProcess;
        private CancellationTokenSource captureCancellation;
        private readonly List<Task> captureTransactions = new List<Task>();
        private SourceWindow locked;
        private Snapshot latest;
        private Snapshot pendingOfflineRetry;
        private int pendingOfflineEpoch;
        private Button offlineRetry;
        private string seen="", done="";
        private int epoch, selectionEpoch;
        private bool active, translating, closing,viewingHistory;
        private readonly TranslationHistory history=new TranslationHistory();
        private Button previousResult,nextResult;
        private Label historyPosition;
        private Button settingsButton,modePill;
        private Control sourceOptions,historyTools;
        private TableLayoutPanel actionsLayout;
        private SettingsPanel settingsPanel;
        private ContextMenuStrip modeMenu;
        private readonly List<ContextMenuStrip> ownedMenus = new List<ContextMenuStrip>();
        private readonly ToolTip tips=new ToolTip { AutoPopDelay=20000 };
        private string serviceMessage="";
        private bool serviceError;
        private Button favoriteButton,favoriteTagButton;
        private ContextMenuStrip favoriteTagMenu;
        private TableLayoutPanel footerLayout;
        private Control footerRight;
        private string latestSourceTitle="";
        private FavoriteContext currentFavorite;
        private FavoriteEntry currentFavoriteEntry;
        private string selectedFavoriteTag;
        private readonly Dictionary<TranslationHistory.Entry,FavoriteContext> favoriteHistory=new Dictionary<TranslationHistory.Entry,FavoriteContext>();
        private readonly Queue<TranslationHistory.Entry> favoriteHistoryOrder=new Queue<TranslationHistory.Entry>();
        private sealed class FavoriteContext {
            internal string SourceText,Translation,Engine,Direction,SourceTitle;
            internal DateTimeOffset CreatedAt;
        }
        private readonly bool preview;
        private string T(string zh,string en) { return Ui.T(zh,en); }

        public CompanionForm(bool isPreview) {
            preview=isPreview;
            SuspendLayout();
            AutoScaleDimensions=new SizeF(96F,96F);AutoScaleMode=AutoScaleMode.Dpi;
            Font=new Font("Microsoft YaHei UI",10F);BackColor=Theme.Cream;ForeColor=Theme.Ink;
            Size=new Size(560,772);MinimumSize=new Size(540,690);
            StartPosition=FormStartPosition.CenterScreen;TopMost=Preferences.Pin;Ui.ApplyIcon(this);
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Padding=new Padding(16,10,16,10),Margin=new Padding(0) };
            foreach(float height in new float[]{76,92,46})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,25));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,112));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
            var heading=new Panel { Dock=DockStyle.Fill,Margin=new Padding(0) };
            heading.Controls.Add(new WhaleBadge { Location=Point.Empty,Size=new Size(80,66) });
            var title=Label("大肥鱼翻译","Whale Translator");title.Font=new Font(Font.FontFamily,17F,FontStyle.Bold);title.ForeColor=Theme.Ink;title.Location=new Point(86,8);heading.Controls.Add(title);
            subtitle=new Label{AutoSize=true,ForeColor=Theme.Muted,Location=new Point(88,43),Font=new Font(Font.FontFamily,8F)};heading.Controls.Add(subtitle);
            language=Button("English","中文",false,72);language.Height=32;language.Click+=delegate { if(settingsPanel!=null)settingsPanel.Close();Ui.SetEnglish(!Ui.English);if(!preview)Preferences.Save(); };
            settingsButton=Button("⚙","⚙",false,32);settingsButton.Height=32;settingsButton.Font=new Font("Segoe UI Symbol",13F);settingsButton.AccessibleName=T("设置","Settings");settingsButton.Click+=delegate{ShowSettings();};
            heading.Controls.AddRange(new Control[]{language,settingsButton});
            heading.Resize+=delegate{settingsButton.Location=new Point(heading.ClientSize.Width-settingsButton.Width,5);language.Location=new Point(settingsButton.Left-language.Width-6,5);};
            layout.Controls.Add(heading,0,0);

            var sourceRows=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=2,Margin=new Padding(0),BackColor=Color.White };
            sourceRows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,40));sourceRows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));sourceRows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sourceRows.RowStyles.Add(new RowStyle(SizeType.Absolute,28));sourceRows.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            sourceIcon=new PictureBox { Visible=false };
            var windowIcon=new WindowLineIcon { Dock=DockStyle.Fill,Margin=new Padding(0,0,7,0) };sourceRows.Controls.Add(windowIcon,0,0);sourceRows.SetRowSpan(windowIcon,2);
            sourceName=new Label {Dock=DockStyle.Fill,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(0,0,4,0),Font=new Font(Font.FontFamily,9F,FontStyle.Bold),ForeColor=Theme.Ink};
            sourceRows.Controls.Add(sourceName,1,0);
            chooseSource=Button("选择翻译窗口","Choose window",true,130);chooseSource.Height=34;chooseSource.Anchor=AnchorStyles.Right;chooseSource.Margin=new Padding(8,0,0,0);chooseSource.Click+=delegate{ChooseSource();};
            sourceRows.Controls.Add(chooseSource,2,0);sourceRows.SetRowSpan(chooseSource,2);
            clearSource=Button("取消选择","Clear selection",false,120);clearSource.Visible=false;clearSource.Click+=delegate{UnlockSource(false);};
            var lower=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0),BackColor=Color.White};
            lockInfo=new Label{Dock=DockStyle.Fill,ForeColor=Theme.Muted,Font=new Font(Font.FontFamily,8F),AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};
            sourceOptions=new FlowLayoutPanel { Dock=DockStyle.Fill,Margin=new Padding(0),WrapContents=false,BackColor=Color.White };
            toggle=new SelectionSwitch { Width=106,Height=27,Margin=new Padding(0) };toggle.Click+=delegate{if(active)Pause();else Start();};
            modePill=Button("","",false,174);modePill.Height=25;modePill.Font=new Font(Font.FontFamily,8F);modePill.Margin=new Padding(0);modePill.Click+=delegate{ShowModeMenu();};
            sourceOptions.Controls.AddRange(new Control[]{toggle,modePill});lower.Controls.Add(lockInfo);lower.Controls.Add(sourceOptions);sourceRows.Controls.Add(lower,1,1);
            var sourceCard=Theme.Card(sourceRows,new Padding(0,1,0,6));sourceCard.Padding=new Padding(12,10,12,10);layout.Controls.Add(sourceCard,0,1);
            var sourceMenu=Theme.CreateMenu();var clearItem=new ToolStripMenuItem();Theme.MenuItem(clearItem);clearItem.Click+=delegate{UnlockSource(false);};sourceMenu.Items.Add(clearItem);
            sourceMenu.Opening+=delegate{clearItem.Text=T("取消窗口选择","Clear window selection");clearItem.Enabled=locked!=null;};sourceCard.ContextMenuStrip=sourceMenu;ownedMenus.Add(sourceMenu);

            var actions=new TableLayoutPanel {Dock=DockStyle.Fill,Margin=new Padding(0),ColumnCount=2,RowCount=1};
            actionsLayout=actions;actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,114));
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0)};
            paste=Button("粘贴翻译","Paste",false,90);paste.Height=32;paste.Click+=delegate{PasteText();};
            copy=Button("复制译文","Copy result",false,94);copy.Height=32;copy.Click+=delegate{try{if(!String.IsNullOrWhiteSpace(translated.Text)){Clipboard.SetText(translated.Text);SetStatus(T("译文已复制","Translation copied"),false);}}catch{SetStatus(T("剪贴板忙，请重试","Clipboard is busy; try again"),true);}};
            var compose=Button("中文提问","Write in Chinese",true,126);compose.Height=32;compose.Click+=async delegate{await OpenCompose();};
            buttons.Controls.AddRange(new Control[]{paste,copy,compose});actions.Controls.Add(buttons,0,0);
            historyTools=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=false,WrapContents=false,Margin=new Padding(0)};
            previousResult=Button("‹","‹",false,28);previousResult.Height=28;previousResult.Margin=new Padding(0,3,0,0);previousResult.Click+=delegate{NavigateHistory(-1);};
            nextResult=Button("›","›",false,28);nextResult.Height=28;nextResult.Margin=new Padding(0,3,0,0);nextResult.Click+=delegate{NavigateHistory(1);};
            historyPosition=new Label{Width=52,Height=31,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Theme.Muted,Margin=new Padding(0),Font=new Font(Font.FontFamily,8F)};
            historyTools.Controls.AddRange(new Control[]{previousResult,historyPosition,nextResult});actions.Controls.Add(historyTools,1,0);layout.Controls.Add(actions,0,2);

            // These models keep the original change/save handlers; the popup presents their controls.
            engine=new ThemedComboBox{Visible=false};engine.SelectedIndexChanged+=delegate{if(!changingOptions)ChangeEngine();};
            direction=new ThemedComboBox{Visible=false};PopulateTargets();
            direction.SelectedIndexChanged+=delegate{if(changingOptions||direction.SelectedItem==null)return;if(Preferences.Offline){var pair=direction.SelectedItem as LanguagePack;if(pair==null)return;Preferences.OfflinePair=pair.Pair;}else Preferences.Target=((LanguageChoice)direction.SelectedItem).Code;if(!preview)Preferences.Save();RefreshModePill();ChangeDirection();};
            pin=new CheckBox {Checked=Preferences.Pin,Visible=false};pin.CheckedChanged+=delegate{TopMost=pin.Checked;Preferences.Pin=pin.Checked;if(!preview)Preferences.Save();};
            checkConnection=Button("检测连接","Test connection",false,100);checkConnection.Visible=false;checkConnection.Click+=async delegate{await CheckGoogleConnection();};
            googleStatus=new Label{Visible=false};
            Controls.AddRange(new Control[]{engine,direction,pin,checkConnection,googleStatus,clearSource,sourceIcon});
            translated=TextBox(13F,Theme.Ink);translated.TextChanged+=delegate{copy.Enabled=!String.IsNullOrWhiteSpace(translated.Text)&&latest!=null;RefreshFavoriteButton(false);};
            var translationRows=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty,BackColor=Color.White};
            translationRows.RowStyles.Add(new RowStyle(SizeType.Absolute,30));translationRows.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var favoriteActions=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Margin=Padding.Empty,BackColor=Color.White};
            favoriteTagButton=Theme.Button("▾",false);favoriteTagButton.Width=26;favoriteTagButton.Height=24;favoriteTagButton.Margin=new Padding(3,0,0,2);
            favoriteTagButton.Click+=delegate{ShowFavoriteTagMenu();};
            favoriteButton=Theme.Button("",false);favoriteButton.Width=156;favoriteButton.Height=24;favoriteButton.Margin=new Padding(0,0,0,2);favoriteButton.Font=new Font(Font.FontFamily,8F);
            favoriteButton.Click+=delegate{ToggleFavorite();};
            favoriteActions.Controls.Add(favoriteTagButton);favoriteActions.Controls.Add(favoriteButton);translationRows.Controls.Add(favoriteActions,0,0);translationRows.Controls.Add(translated,0,1);
            layout.Controls.Add(Theme.Card(translationRows,new Padding(0,2,0,3)),0,3);
            var originalLabel=Label("原文对照","Original selection");originalLabel.Dock=DockStyle.Fill;originalLabel.TextAlign=ContentAlignment.BottomLeft;originalLabel.Font=new Font(Font.FontFamily,8.5F);layout.Controls.Add(originalLabel,0,4);
            original=TextBox(9F,Theme.Muted);original.TextChanged+=delegate{RefreshFavoriteButton(false);};layout.Controls.Add(Theme.Card(original,new Padding(0,4,0,3)),0,5);
            AddTextMenu(translated);AddTextMenu(original);
            var footer=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0)};
            footerLayout=footer;
            footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,39));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,61));
            status=new Label {Dock=DockStyle.Fill,ForeColor=Theme.BlueDark,Font=new Font(Font.FontFamily,7.5F),AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(0)};
            privacy=new Label{Dock=DockStyle.Fill,ForeColor=Theme.Muted,Font=new Font(Font.FontFamily,7F),TextAlign=ContentAlignment.MiddleRight,Margin=new Padding(0)};
            var footRight=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0)};footRight.Controls.Add(privacy);
            footerRight=footRight;
            offlineRetry=Button("改用离线翻译","Use offline",true,145);offlineRetry.Height=25;offlineRetry.Dock=DockStyle.Right;offlineRetry.Margin=new Padding(0);offlineRetry.Visible=false;
            offlineRetry.Click+=delegate{RetryOffline();};footRight.Controls.Add(offlineRetry);
            footer.Controls.Add(status,0,0);footer.Controls.Add(footRight,1,0);layout.Controls.Add(footer,0,6);Controls.Add(layout);layout.BringToFront();
            status.Click+=delegate{if(serviceError)ShowSettings();};
            watcher.SelectionReleased+=async delegate(IntPtr handle,Point point){await ReadSelection(handle);};
            lockTimer.Tick+=delegate{if(locked!=null&&!locked.IsAlive())UnlockSource(true);};
            Ui.LanguageChanged+=RefreshLanguage;RefreshLanguage();
            Activated+=delegate{RefreshFavoriteButton(true);};
            Shown+=delegate{if(!preview)lockTimer.Start();};
            FormClosing+=CloseSafely;ResumeLayout(true);
        }

        private Label Label(string zh,string en){var l=new Label{AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(0)};Bind(l,zh,en);return l;}
        private void Bind(Control c,string zh,string en){Action update=delegate{c.Text=T(zh,en);};labels.Add(update);update();}
        private Button Button(string zh,string en,bool primary,int width){var b=Theme.Button(T(zh,en),primary);b.Width=width;b.Margin=new Padding(0,3,5,3);Bind(b,zh,en);return b;}
        private TextBox TextBox(float size,Color color){return new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=color,Font=new Font(Font.FontFamily,size),WordWrap=true,ScrollBars=ScrollBars.Vertical,MaxLength=0};}
        private void AddTextMenu(TextBox box){
            var menu=Theme.CreateMenu();var copyItem=new ToolStripMenuItem();var allItem=new ToolStripMenuItem();Theme.MenuItem(copyItem);Theme.MenuItem(allItem);
            copyItem.Click+=delegate{try{if(box.SelectionLength>0)box.Copy();}catch{SetStatus(T("剪贴板忙，请重试","Clipboard is busy; try again"),true);}};
            allItem.Click+=delegate{box.Focus();box.SelectAll();};menu.Items.Add(copyItem);menu.Items.Add(allItem);
            menu.Opening+=delegate{copyItem.Text=T("复制所选文字","Copy selection");copyItem.Enabled=box.SelectionLength>0;allItem.Text=T("全选","Select all");allItem.Enabled=box.TextLength>0;};
            box.ContextMenuStrip=menu;ownedMenus.Add(menu);
        }
        private SettingsPanelModel CreateSettingsModel(){
            return new SettingsPanelModel{
                Offline=delegate{return Preferences.Offline;},Pinned=delegate{return pin.Checked;},TargetEnabled=delegate{return direction.Enabled;},
                Targets=delegate{var values=new object[direction.Items.Count];direction.Items.CopyTo(values,0);return values;},SelectedTarget=delegate{return direction.SelectedIndex;},
                SetOffline=delegate(bool value){engine.SelectedIndex=value?0:1;},SetPinned=delegate(bool value){pin.Checked=value;},
                SelectTarget=delegate(int index){if(index>=0&&index<direction.Items.Count)direction.SelectedIndex=index;},
                SetEnglish=delegate(bool value){Ui.SetEnglish(value);if(!preview)Preferences.Save();},
                ManagePacks=ManagePacks,EditGlossary=EditGlossary,OpenFavorites=OpenFavorites,
                CheckConnection=CheckGoogleConnection,CheckUpdates=delegate{WhaleCommon.UpdateChecker.Show(this,"translator",Program.Version,Ui.English);}
            };
        }
        private void ShowSettings(){
            if(closing)return;
            if(settingsPanel!=null&&!settingsPanel.IsDisposed){settingsPanel.Close();return;}
            var popup=new SettingsPanel(CreateSettingsModel());settingsPanel=popup;popup.TopMost=TopMost;
            popup.UpdateService(serviceMessage,serviceError);
            popup.FormClosed+=delegate{if(settingsPanel==popup)settingsPanel=null;popup.Dispose();};
            popup.Show(this);
            Point anchor=settingsButton.PointToScreen(new Point(settingsButton.Width,settingsButton.Height+6));Rectangle work=Screen.FromControl(this).WorkingArea;
            popup.Location=new Point(Math.Max(work.Left,Math.Min(anchor.X-popup.Width,work.Right-popup.Width)),Math.Max(work.Top,Math.Min(anchor.Y,work.Bottom-popup.Height)));
        }
        private void ShowModeMenu(){
            if(closing)return;if(modeMenu!=null)modeMenu.Dispose();modeMenu=BuildModeMenu();
            modeMenu.Show(modePill,new Point(0,modePill.Height));
        }
        private ContextMenuStrip BuildModeMenu(){
            var menu=Theme.CreateMenu();
            var onlineItem=new ToolStripMenuItem(T("Google 在线翻译","Google Translate · Online")){Checked=!Preferences.Offline};Theme.MenuItem(onlineItem);onlineItem.Click+=delegate{engine.SelectedIndex=1;};
            var offlineItem=new ToolStripMenuItem(T("离线翻译","Offline translation")){Checked=Preferences.Offline};Theme.MenuItem(offlineItem);offlineItem.Click+=delegate{engine.SelectedIndex=0;};
            menu.Items.Add(onlineItem);menu.Items.Add(offlineItem);menu.Items.Add(new ToolStripSeparator());
            for(int i=0;i<direction.Items.Count;i++){int selected=i;var item=new ToolStripMenuItem(direction.GetItemText(direction.Items[i])){Checked=i==direction.SelectedIndex,Enabled=direction.Enabled};Theme.MenuItem(item);item.Click+=delegate{direction.SelectedIndex=selected;};menu.Items.Add(item);}
            return menu;
        }
        private void RefreshModePill(){
            if(modePill==null||direction==null)return;
            string target=direction.SelectedItem==null?T("选择语言","Choose language"):direction.GetItemText(direction.SelectedItem);
            string mode=Preferences.Offline?T("离线","Offline"):"Google";modePill.Text=mode+" → "+target+" ▾";
            tips.SetToolTip(modePill,modePill.Text);modePill.AccessibleName=T("翻译方式与目标语言：","Translation mode and target: ")+mode+" → "+target;
            if(settingsPanel!=null&&!settingsPanel.IsDisposed)settingsPanel.RefreshState();
        }
        private static string ComparableText(string text){return (text??"").Replace("\r\n","\n");}
        private bool CanFavorite(){return currentFavorite!=null&&!String.IsNullOrWhiteSpace(currentFavorite.Translation)&&ComparableText(currentFavorite.Translation)==ComparableText(translated.Text)&&ComparableText(currentFavorite.SourceText)==ComparableText(original.Text);}
        private string FavoriteTag(){return currentFavoriteEntry!=null?currentFavoriteEntry.Tag:FavoritesPreferences.LastTag;}
        private string FavoriteTagLabel(string tag){return tag==FavoritesPreferences.DefaultTag?T("未分类","Unsorted"):tag;}
        private void RefreshFavoriteButton(bool reload){
            if(favoriteButton==null||original==null||closing)return;
            try {
                bool available=CanFavorite();
                if(!available)currentFavoriteEntry=null;
                else if(reload){
                    var store=new FavoritesStore(FavoritesPreferences.Folder);currentFavoriteEntry=store.FindMatch(currentFavorite.SourceText,currentFavorite.Translation,currentFavorite.Engine,currentFavorite.Direction,currentFavorite.SourceTitle);
                }
                // Validate preference reads inside this feature boundary, including startup/help state.
                string tag=FavoriteTag();
                favoriteButton.Enabled=available;favoriteTagButton.Enabled=available;
                favoriteButton.Text=currentFavoriteEntry==null?T("☆ 收藏","☆ Save"):T("★ 已收藏 · ","★ Saved · ")+FavoriteTagLabel(currentFavoriteEntry.Tag);
                favoriteButton.AccessibleName=currentFavoriteEntry==null?T("收藏这条回复","Save this reply"):T("取消收藏：","Unsave reply: ")+FavoriteTagLabel(currentFavoriteEntry.Tag);
                favoriteTagButton.AccessibleName=T("选择或新建收藏标签","Choose or create a saved-reply tag");
                tips.SetToolTip(favoriteButton,available?favoriteButton.Text:T("有译文后才可以收藏","A translation is needed before saving"));
                tips.SetToolTip(favoriteTagButton,T("收藏标签：","Save tag: ")+FavoriteTagLabel(tag));
                var rounded=favoriteButton as RoundedButton;if(rounded!=null){rounded.Primary=currentFavoriteEntry!=null;rounded.Invalidate();}
            }catch(Exception error){SetFavoriteUnavailable(error);}
        }
        private void SetFavoriteUnavailable(Exception error){
            currentFavoriteEntry=null;
            favoriteButton.Enabled=false;favoriteTagButton.Enabled=false;
            favoriteButton.Text=T("☆ 收藏暂不可用","☆ Save unavailable");
            string message=T("收藏暂不可用：","Saved replies unavailable: ")+FavoritesForm.ErrorText(error);
            favoriteButton.AccessibleName=message;favoriteTagButton.AccessibleName=message;
            tips.SetToolTip(favoriteButton,message);tips.SetToolTip(favoriteTagButton,message);
            var rounded=favoriteButton as RoundedButton;if(rounded!=null){rounded.Primary=false;rounded.Invalidate();}
        }
        private void RememberFavoriteResult(Snapshot request,string result,bool offlineMode,string sourceTitle){
            currentFavorite=new FavoriteContext {SourceText=request.Text,Translation=result,Engine=offlineMode?"offline":"google",Direction=request.Direction,SourceTitle=sourceTitle??"",CreatedAt=DateTimeOffset.Now};
            currentFavoriteEntry=null;
            var entry=history.Current;
            if(entry!=null&&entry.Source==request.Text&&entry.Result==result){
                if(!favoriteHistory.ContainsKey(entry))favoriteHistoryOrder.Enqueue(entry);
                favoriteHistory[entry]=currentFavorite;
            }
            // Mirror the existing bounded history without retaining evicted text in this feature.
            while(favoriteHistoryOrder.Count>history.Count){var old=favoriteHistoryOrder.Dequeue();favoriteHistory.Remove(old);}
            RefreshFavoriteButton(true);
        }
        private void ToggleFavorite(){
            if(closing||!CanFavorite())return;
            try {
                var store=new FavoritesStore(FavoritesPreferences.Folder);
                var found=store.FindMatch(currentFavorite.SourceText,currentFavorite.Translation,currentFavorite.Engine,currentFavorite.Direction,currentFavorite.SourceTitle);
                if(found!=null){store.Delete(found);currentFavoriteEntry=null;RefreshFavoriteButton(false);SetFavoriteStatus(T("已取消收藏 · 对应 Markdown 文件已删除","Reply unsaved · Its Markdown file was deleted"),false);return;}
                string tag=FavoritesPreferences.LastTag;
                var entry=new FavoriteEntry {SourceTitle=currentFavorite.SourceTitle,SourceText=currentFavorite.SourceText,Translation=currentFavorite.Translation,Engine=currentFavorite.Engine,Direction=currentFavorite.Direction,Tag=tag,Notes="",CreatedAt=DateTimeOffset.Now};
                FavoritesPreferences.LastTag=tag;currentFavoriteEntry=store.Save(entry);selectedFavoriteTag=currentFavoriteEntry.Tag;
                RefreshFavoriteButton(false);SetFavoriteStatus(T("已收藏到「","Saved to “")+FavoriteTagLabel(selectedFavoriteTag)+T("」· 在 ⚙ → 回复收藏本 里查看","” · View in ⚙ → Saved replies"),false);
            } catch(Exception error) {RefreshFavoriteButton(true);SetFavoriteStatus(T("收藏未完成：","Could not update saved reply: ")+FavoritesForm.ErrorText(error),true);}
        }
        private void SetFavoriteTag(string tag){
            if(closing||String.IsNullOrWhiteSpace(tag))return;
            try {
                FavoritesPreferences.CreateTag(tag);FavoritesPreferences.LastTag=tag;selectedFavoriteTag=FavoritesPreferences.LastTag;
                if(CanFavorite()){
                    var store=new FavoritesStore(FavoritesPreferences.Folder);
                    var found=store.FindMatch(currentFavorite.SourceText,currentFavorite.Translation,currentFavorite.Engine,currentFavorite.Direction,currentFavorite.SourceTitle);
                    if(found!=null){found.Tag=selectedFavoriteTag;store.Update(found);}
                }
                RefreshFavoriteButton(true);
                SetFavoriteStatus(currentFavoriteEntry==null?T("下次收藏到「","Next save goes to “")+FavoriteTagLabel(selectedFavoriteTag)+T("」","”"):T("已移动到「","Moved to “")+FavoriteTagLabel(selectedFavoriteTag)+T("」","”"),false);
            }catch(Exception error){RefreshFavoriteButton(true);SetFavoriteStatus(T("标签未更新：","Could not update tag: ")+FavoritesForm.ErrorText(error),true);}
        }
        private ContextMenuStrip BuildFavoriteTagMenu(){
            var tags=new List<string>(FavoritesPreferences.Tags);
            try{foreach(var entry in new FavoritesStore(FavoritesPreferences.Folder).Load())if(!tags.Contains(entry.Tag))tags.Add(entry.Tag);}catch{}
            string selected=FavoriteTag();var menu=Theme.CreateMenu();
            foreach(string value in tags){string tag=value;var item=new ToolStripMenuItem(FavoriteTagLabel(tag)){Checked=tag==selected};Theme.MenuItem(item);item.Click+=delegate{SetFavoriteTag(tag);};menu.Items.Add(item);}
            menu.Items.Add(new ToolStripSeparator());var add=new ToolStripMenuItem(T("+ 新建标签…","+ New tag…"));Theme.MenuItem(add);add.Click+=delegate{PromptFavoriteTag();};menu.Items.Add(add);return menu;
        }
        private void ShowFavoriteTagMenu(){
            if(closing||!CanFavorite())return;
            try{if(favoriteTagMenu!=null){favoriteTagMenu.Dispose();favoriteTagMenu=null;}favoriteTagMenu=BuildFavoriteTagMenu();favoriteTagMenu.Show(favoriteTagButton,new Point(0,favoriteTagButton.Height));}
            catch(Exception error){SetFavoriteUnavailable(error);SetFavoriteStatus(T("收藏标签未打开：","Could not open saved-reply tags: ")+FavoritesForm.ErrorText(error),true);}
        }
        private void PromptFavoriteTag(){
            using(var dialog=new Form {Text=T("新建收藏标签","New saved-reply tag"),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,BackColor=Theme.Cream,ForeColor=Theme.Ink,Font=Font,ClientSize=new Size(330,130),TopMost=TopMost}){
                dialog.SuspendLayout();dialog.AutoScaleDimensions=new SizeF(96F,96F);dialog.AutoScaleMode=AutoScaleMode.Dpi;Ui.ApplyIcon(dialog);
                var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(16),RowCount=2,ColumnCount=1};layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
                var name=new TextBox {Dock=DockStyle.Top,MaxLength=40,AccessibleName=T("标签名称","Tag name")};layout.Controls.Add(name,0,0);
                var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};
                var save=Theme.Button(T("新建","Create"),true);save.Width=110;save.DialogResult=DialogResult.OK;
                var cancel=Theme.Button(T("取消","Cancel"),false);cancel.Width=110;cancel.DialogResult=DialogResult.Cancel;
                actions.Controls.Add(save);actions.Controls.Add(cancel);layout.Controls.Add(actions,0,1);dialog.Controls.Add(layout);dialog.AcceptButton=save;dialog.CancelButton=cancel;dialog.ResumeLayout(true);
                if(dialog.ShowDialog(this)==DialogResult.OK&&!String.IsNullOrWhiteSpace(name.Text))SetFavoriteTag(name.Text.Trim());
            }
        }
        private void OpenFavorites(){
            if(closing)return;
            try{using(var dialog=new FavoritesForm()){dialog.TopMost=TopMost;dialog.ShowDialog(this);}}
            catch(Exception error){SetFavoriteStatus(T("收藏本未打开：","Could not open saved replies: ")+FavoritesForm.ErrorText(error),true);}
            finally{RefreshFavoriteButton(true);}
        }
        private void SetFavoriteStatus(string value,bool error){SetStatus(value,error);if(closing)return;footerRight.Visible=false;footerLayout.SetColumnSpan(status,2);status.AutoEllipsis=true;}
        // Isolated preview entry point: no clipboard read, input watcher, translation request, or save.
        private void ApplyFavoritePreview(string source,string result,string mode,string selectedDirection,string title){
            if(!preview)throw new InvalidOperationException("Synthetic reply setup is available only in preview mode.");
            currentFavorite=null;latest=new Snapshot {Conversation="preview",MessageId=Guid.NewGuid().ToString("N"),Text=source,Direction=selectedDirection};latestSourceTitle=title;
            original.Text=DisplayLines(source);translated.Text=DisplayLines(result);history.Add(source,result,selectedDirection,mode=="offline");
            RememberFavoriteResult(latest,result,mode=="offline",title);RefreshHistoryButtons();
        }
        private string SelectedDirection{get{var pair=direction.SelectedItem as LanguagePack;if(Preferences.Offline)return pair==null?Preferences.OfflinePair:pair.Pair;var choice=direction.SelectedItem as LanguageChoice;return "auto:"+(choice==null?Preferences.Target:choice.Code);}}
        private void RefreshLanguage(){
            if(closing)return;
            foreach(var update in labels)update();
            if(settingsPanel!=null&&!settingsPanel.IsDisposed)settingsPanel.Close();if(modeMenu!=null)modeMenu.Close();
            if(favoriteTagMenu!=null)favoriteTagMenu.Close();
            Text=T("大肥鱼翻译 ","Whale Translator ")+Program.Version;
            RefreshHistoryButtons();
            RefreshEngineLabels();
            chooseSource.AccessibleName=T("选择要翻译的程序窗口","Choose a program window to translate");direction.AccessibleName=T("目标语言","Target language");
            translated.AccessibleName=T("译文","Translation");original.AccessibleName=T("所选原文","Selected text");
            settingsButton.AccessibleName=T("设置","Settings");tips.SetToolTip(settingsButton,T("设置","Settings"));
            if(latest==null)translated.Text=T("① 点上方「选择翻译窗口」\r\n在卡片中找到 Claude、浏览器或文档。\r\n\r\n② 点一下那张窗口卡片\r\n\r\n③ 回到该窗口划选文字\r\n松开鼠标，大肥鱼就会自动翻译。","1. Click Choose window above.\r\nFind Claude, your browser or a document.\r\n\r\n2. Click its window card.\r\n\r\n3. Select text in that window.\r\nRelease the mouse to translate automatically.");
            SetGoogleStatus(Preferences.Offline?PackStatus():T("可点「检测连接」检查服务","Use Test Google to check the service"),false);
            RefreshLockLabels();
            wait.RefreshMessage(TranslationWait());
            connectionWait.RefreshMessage(T("正在用固定示例检测…","Testing with a fixed sample…"));
            SetStatus(locked==null?T("请选择窗口 · 或粘贴翻译","Choose a window · Or paste"):active?T("划选翻译已开启","Selection translation is on"):T("划选暂停 · 仍可粘贴","Paused · Paste is available"),false);
            if(CanRetryOffline()){
                SetGoogleStatus(T("这段在线翻译未完成，可用离线包重试","This online translation failed. You can retry with an offline pack"),true);
                SetStatus(T("原文已保留，点「改用离线翻译」继续","Original retained. Click Use offline to continue"),true);
            }
            if(viewingHistory&&history.Current!=null)ShowHistoryStatus(history.Current);
            RefreshFavoriteButton(false);
        }
        private string TranslationWait(){return Preferences.Offline?T("大肥鱼正在本机翻译…","Your whale is translating on this device…"):T("大肥鱼正在请 Google 翻译…","Your whale is asking Google to translate…");}
        private void RefreshEngineLabels(){
            changingOptions=true;engine.Items.Clear();engine.Items.Add(T("离线翻译","Offline translation"));engine.Items.Add(T("Google 在线翻译","Google Translate · Online"));engine.SelectedIndex=Preferences.Offline?0:1;changingOptions=false;
            engine.AccessibleName=T("翻译方式","Translation mode");
            subtitle.Text=Preferences.Offline?T("离线语言包 · 没网也能读","Offline language packs · Read anywhere"):T("Google 在线翻译 · 陪你读懂每一句","Google Translate · Read with your whale");
            privacy.Text=Preferences.Offline?T("本机离线 · 不上传 · 不自动保存","Offline · No upload · No auto-save"):T("所选文字发送给 Google · 不自动保存","Selection → Google · No auto-save");
            tips.SetToolTip(privacy,Preferences.Offline?T("本机离线翻译，不上传文字，不自动保存原文。","Translation stays on this device. Text is not uploaded or automatically saved."):T("只把你选中或粘贴的文字发送给 Google，不自动保存原文。","Only selected or pasted text is sent to Google. Source text is not automatically saved."));
            checkConnection.Text=Preferences.Offline?T("检测离线库","Test offline"):T("检测连接","Test Google");
            direction.Width=Preferences.Offline?300:150;
            if(Preferences.Offline)PopulateTargets();
            RefreshModePill();
        }
        private void PopulateTargets(){
            changingOptions=true;direction.Items.Clear();
            if(Preferences.Offline){
                foreach(var item in LanguagePack.InstalledPacks())direction.Items.Add(item);
                if(direction.Items.Count==0){direction.Items.Add(T("先下载离线语言包…","Download a language pack first…"));direction.SelectedIndex=0;direction.Enabled=false;}
                else{direction.Enabled=true;direction.SelectedIndex=0;foreach(LanguagePack item in direction.Items)if(item.Pair==Preferences.OfflinePair)direction.SelectedItem=item;Preferences.OfflinePair=((LanguagePack)direction.SelectedItem).Pair;}
            }else{direction.Enabled=true;foreach(var item in LanguageChoice.All)direction.Items.Add(item);direction.SelectedIndex=0;foreach(LanguageChoice item in direction.Items)if(item.Code==Preferences.Target)direction.SelectedItem=item;}
            changingOptions=false;RefreshModePill();
        }
        private string PackStatus(){return direction.SelectedItem is LanguagePack?T("语言包已安装 · 翻译无需联网","Pack installed · Translation needs no network"):T("点「离线语言包」，联网时按需下载","Open Offline packs to download while online");}
        private void ClearOfflineRetry(){pendingOfflineRetry=null;pendingOfflineEpoch=0;ShowOfflineRetry(false);}
        private void ShowOfflineRetry(bool show){
            offlineRetry.Visible=show;privacy.Visible=!show;if(footerLayout==null)return;
            // The button needs far less room than the privacy note: give the status the rest so it is not cut off.
            ColumnStyle left=footerLayout.ColumnStyles[0],right=footerLayout.ColumnStyles[1];
            left.SizeType=SizeType.Percent;left.Width=show?100:39;
            right.SizeType=show?SizeType.Absolute:SizeType.Percent;right.Width=show?offlineRetry.Width:61;
        }
        private bool CanRetryOffline(){return !closing&&!Preferences.Offline&&pendingOfflineRetry!=null&&Object.ReferenceEquals(pendingOfflineRetry,latest)&&pendingOfflineEpoch==epoch&&pendingOfflineRetry.Direction==SelectedDirection;}
        private bool RecordOfflineRetry(Snapshot request,int requestedEpoch,bool offlineMode,Exception error){
            var failure=error as TranslationFailure;
            if(request==null||offlineMode||Preferences.Offline||closing||requestedEpoch!=epoch||!Object.ReferenceEquals(request,latest)||request.Direction!=SelectedDirection||failure==null||(failure.Code!="rate_limited"&&failure.Code!="google_blocked"))return false;
            pendingOfflineRetry=request;pendingOfflineEpoch=epoch;ShowOfflineRetry(true);
            SetStatus(T("Google 暂不可用，点「改用离线翻译」继续这段","Google is unavailable. Use offline for this selection."),true);return true;
        }
        private void RetryOffline(){
            if(!CanRetryOffline()){ClearOfflineRetry();return;}
            var request=pendingOfflineRetry;bool resume=active;Pause();int requestEpoch=pendingOfflineEpoch;
            DialogResult result;LanguagePack selected;
            using(var dialog=new OfflineRetryForm(OfflineRetryForm.ForTarget(LanguagePack.InstalledPacks(),request.Direction))){dialog.TopMost=TopMost;result=dialog.ShowDialog(this);selected=dialog.SelectedPack;}
            if(closing)return;
            if(resume&&locked!=null&&locked.IsAlive())Start();
            if(result==DialogResult.OK)ApplyOfflineRetry(request,requestEpoch,selected);
            else if(result==DialogResult.Retry){ManagePacks();if(CanRetryOffline())SetStatus(T("安装后，点「改用离线翻译」继续刚才那段","After installing, click Use offline to retry that selection"),false);}
            else if(CanRetryOffline())SetStatus(T("原文已保留，可稍后改用离线翻译","Original retained. You can retry offline later"),false);
        }
        private bool ApplyOfflineRetry(Snapshot request,int requestEpoch,LanguagePack pair){
            if(!CanRetryOffline()||requestEpoch!=epoch||!Object.ReferenceEquals(request,pendingOfflineRetry)||pair==null)return false;
            LanguagePack installed=null;foreach(var candidate in OfflineRetryForm.ForTarget(LanguagePack.InstalledPacks(),request.Direction))if(candidate.Pair==pair.Pair){installed=candidate;break;}
            if(installed==null){SetStatus(T("对应语言包已不可用，请打开「离线语言包」检查","The matching pack is unavailable. Check Offline packs"),true);return false;}
            ClearOfflineRetry();CancelCapture();++epoch;worker.Dispose();wait.Stop();
            if(connectionWorker!=null){connectionWorker.Dispose();connectionWait.Stop();}
            Preferences.Offline=true;Preferences.OfflinePair=installed.Pair;PopulateTargets();RefreshEngineLabels();if(!preview)Preferences.Save();
            SetGoogleStatus(PackStatus(),false);
            // This explicit button authorizes retrying only this failed selection locally.
            AcceptTextCore(request.Text,request.Conversation,latestSourceTitle);return true;
        }
        private void ManagePacks(){
            bool resume=active;Pause();worker.Dispose();needsFreshSelection=true;
            using(var manager=new LanguagePacksForm(preview)){manager.TopMost=TopMost;manager.ShowDialog(this);}
            if(closing)return;PopulateTargets();if(!preview)Preferences.Save();SetGoogleStatus(Preferences.Offline?PackStatus():T("语言包已更新，可切换离线模式","Packs updated. You can switch to offline mode"),false);
            if(resume&&locked!=null&&locked.IsAlive())Start();
        }
        private void ChangeEngine(){
            if(engine.SelectedIndex<0||closing)return;
            ClearOfflineRetry();
            CancelCapture();++epoch;worker.Dispose();wait.Stop();needsFreshSelection=true;
            if(connectionWorker!=null){connectionWorker.Dispose();connectionWait.Stop();}
            Preferences.Offline=engine.SelectedIndex==0;PopulateTargets();if(!preview)Preferences.Save();RefreshEngineLabels();
            SetGoogleStatus(Preferences.Offline?PackStatus():T("在线模式 · 下次翻译会发送文字给 Google","Online mode · Your next translation sends text to Google"),false);
            SetStatus(T("已切换翻译方式，请重新划选或粘贴","Mode changed. Select new text or use Paste"),false);
        }
        private void RefreshLockLabels(){
            chooseSource.Text=locked==null?T("选择翻译窗口","Choose window"):T("换个窗口","Change window");chooseSource.Enabled=!pickingSource;
            var sourceButton=chooseSource as RoundedButton;if(sourceButton!=null){sourceButton.Primary=locked==null;sourceButton.Invalidate();}
            // Height is already scaled by WinForms even before a native handle exists.
            float scale=chooseSource.Height/34F;chooseSource.Width=(int)Math.Round((locked==null?130:118)*scale);
            clearSource.Visible=false;clearSource.Enabled=locked!=null&&!pickingSource;
            toggle.Text=T("划选翻译","Selection");toggle.Enabled=locked!=null&&!pickingSource;((SelectionSwitch)toggle).Selected=active;
            sourceOptions.Visible=locked!=null;lockInfo.Visible=locked==null;
            sourceName.Text=locked==null?T("还没选择翻译窗口","No window selected"):T("已选窗口 · ","Selected window · ")+locked.Title;
            tips.SetToolTip(sourceName,locked==null?sourceName.Text:locked.FriendlyAppName+" · "+locked.Title);
            lockInfo.Text=locked==null?T("点右侧按钮，再点一张窗口卡片。","Click Choose window, then a window card."):active?T("✓ 正在翻译这个窗口的划选文字","✓ Translating selections from this window"):T("已选好这个窗口 · 划选已暂停","This window is selected · Translation paused");
        }
        private bool BeginSourcePick(){
            if(closing||pickingSource)return false;
            sourceBeforePick=locked;sourceActiveBeforePick=active;pickingSource=true;
            Pause();RefreshLockLabels();return true;
        }
        private void ChooseSource(){
            if(!BeginSourcePick())return;
            SourceWindow choice=null;
            try{
                using(var picker=new SourceWindowPickerForm(locked)){
                    sourcePicker=picker;picker.TopMost=TopMost;
                    if(picker.ShowDialog(this)==DialogResult.OK)choice=picker.SelectedSource;
                }
            }finally{sourcePicker=null;CompleteSourcePick(choice);}
        }
        private void CompleteSourcePick(SourceWindow choice){
            if(!pickingSource)return;
            bool resume=sourceActiveBeforePick;var prior=sourceBeforePick;
            pickingSource=false;sourceBeforePick=null;sourceActiveBeforePick=false;
            if(closing)return;
            if(choice!=null&&ApplySource(choice))return;
            if(locked!=null&&!locked.IsAlive())UnlockSource(true);
            else if(locked!=null&&locked==prior&&resume)Start();
            else{RefreshLockLabels();SetStatus(locked==null?T("先选择一个窗口 · 也可粘贴翻译","Choose a window first · Or use Paste"):T("保持原来的窗口选择 · 划选已暂停","Previous window retained · Translation paused"),false);}
            if(choice!=null)SetStatus(T("这个窗口已关闭，请重新选择","That window has closed; choose another"),true);
        }
        private bool ApplySource(SourceWindow source){
            if(closing||source==null||!source.IsAlive())return false;
            ClearOfflineRetry();
            CancelCapture();++epoch;if(translating)worker.Dispose();wait.Stop();
            locked=source;UpdateSourceIcon(source);Start();return true;
        }
        private void UpdateSourceIcon(SourceWindow source){++sourceIconEpoch;var old=sourceIcon.Image;sourceIcon.Image=null;if(old!=null)old.Dispose();}
        private void UnlockSource(bool closed){
            ClearOfflineRetry();
            active=false;watcher.Pause();CancelCapture();++epoch;if(translating)worker.Dispose();wait.Stop();locked=null;
            UpdateSourceIcon(null);RefreshLockLabels();
            SetStatus(closed?T("来源窗口已关闭，请重新选择","Source window closed; choose another window"):T("已取消选择，可重新选择翻译窗口","Selection cleared; choose a window to translate"),false);
        }
        private void Start(){if(closing||pickingSource)return;if(locked!=null&&!locked.IsAlive()){UnlockSource(true);return;}if(locked==null){RefreshLockLabels();SetStatus(T("请先选择翻译窗口","Choose a window first"),false);return;}active=true;watcher.Start();RefreshLockLabels();SetStatus(T("回到所选窗口，划选文字后松开就翻译","Select text in your chosen window; release to translate"),false);}
        private void Pause(){active=false;watcher.Pause();CancelCapture();++epoch;if(pendingOfflineRetry!=null)pendingOfflineEpoch=epoch;if(translating)worker.Dispose();wait.Stop();RefreshLockLabels();SetStatus(T("划选已暂停；仍可粘贴翻译","Selection paused; Paste is still available"),false);}
        private void CancelCapture(){++selectionEpoch;var old=captureCancellation;captureCancellation=null;if(old!=null)old.Cancel();}
        private void RefreshHistoryButtons(){
            if(previousResult==null)return;previousResult.Enabled=history.CanPrevious;nextResult.Enabled=history.CanNext;
            historyTools.Visible=history.Count>0;
            actionsLayout.ColumnStyles[1].Width=history.Count>0?previousResult.Width+historyPosition.Width+nextResult.Width+6*(previousResult.Height/28F):0;
            historyPosition.Text=history.Count==0?"":(history.Current==null?history.Count.ToString():(history.Position+1).ToString())+" / "+history.Count;
            previousResult.AccessibleName=T("上一条译文","Previous translation");nextResult.AccessibleName=T("下一条译文","Next translation");
            tips.SetToolTip(previousResult,previousResult.AccessibleName);tips.SetToolTip(nextResult,nextResult.AccessibleName);
        }
        private void NavigateHistory(int delta){
            if(closing)return;var entry=history.Move(delta);if(entry==null)return;
            ClearOfflineRetry();CancelCapture();++epoch;worker.Dispose();wait.Stop();needsFreshSelection=true;
            latest=new Snapshot{Conversation="history",MessageId=epoch.ToString(),Text=entry.Source,Direction=entry.Direction};
            viewingHistory=true;
            original.Text=DisplayLines(entry.Source);translated.Text=DisplayLines(entry.Result);done=seen=latest.Key+latest.Text;
            favoriteHistory.TryGetValue(entry,out currentFavorite);currentFavoriteEntry=null;RefreshFavoriteButton(true);
            RefreshHistoryButtons();ShowHistoryStatus(entry);
        }
        private void ShowHistoryStatus(TranslationHistory.Entry entry){
            string code=entry.Direction;int separator=code.LastIndexOf(':');if(separator>=0)code=code.Substring(separator+1);if(code=="en_zh"||code=="zh")code="zh-CN";else if(code=="zh_en")code="en";else if(code=="zt")code="zh-TW";
            string target=code;foreach(var choice in LanguageChoice.All)if(choice.Code==code){target=choice.Name;break;}
            SetStatus(T("本次记录 · 译成 ","Session history · To ")+target+" · "+(entry.Offline?T("离线","Offline"):"Google"),false);
        }
        private void EditGlossary(){
            bool resume=active;Pause();
            try{using(var dialog=new GlossaryForm()){dialog.TopMost=TopMost;if(dialog.ShowDialog(this)==DialogResult.OK){needsFreshSelection=true;SetStatus(T("词典已保存；下次划选或粘贴时生效","Glossary saved; applies to your next selection or paste"),false);}}}
            catch(InvalidOperationException error){MessageBox.Show(this,error.Message,T("术语词典","Glossary"),MessageBoxButtons.OK,MessageBoxIcon.Information);}
            finally{if(resume&&!closing&&locked!=null&&locked.IsAlive())Start();}
        }
        private async void CloseSafely(object sender,FormClosingEventArgs e){
            if(closing)return;
            closing=true;++epoch;++sourceIconEpoch;CancelCapture();watcher.Dispose();lockTimer.Dispose();wait.Dispose();connectionWait.Dispose();worker.Dispose();
            if(settingsPanel!=null&&!settingsPanel.IsDisposed)settingsPanel.Close();if(modeMenu!=null)modeMenu.Dispose();if(favoriteTagMenu!=null)favoriteTagMenu.Dispose();foreach(var menu in ownedMenus)menu.Dispose();tips.Dispose();
            history.Clear();favoriteHistory.Clear();favoriteHistoryOrder.Clear();currentFavorite=null;currentFavoriteEntry=null;
            if(sourcePicker!=null&&!sourcePicker.IsDisposed)sourcePicker.Close();
            var icon=sourceIcon.Image;sourceIcon.Image=null;if(icon!=null)icon.Dispose();
            if(connectionWorker!=null)connectionWorker.Dispose();
            if(composeReaderProcess!=null)try{composeReaderProcess.Kill();}catch{}
            Ui.LanguageChanged-=RefreshLanguage;
            if(captureTransactions.Count>0){
                e.Cancel=true;
                // Let any already-issued Copy finish its bounded clipboard cleanup before exit.
                var pending=Task.WhenAll(captureTransactions.ToArray());
                await Task.WhenAny(pending,Task.Delay(3500));
                if(!IsDisposed)Close();
            }
        }
        private void ChangeDirection(){ClearOfflineRetry();CancelCapture();if(latest==null||closing||needsFreshSelection)return;history.LeaveView();RefreshHistoryButtons();++epoch;if(translating)worker.Dispose();wait.Stop();latest=new Snapshot{Conversation=latest.Conversation,MessageId=epoch.ToString(),Text=latest.Text,Direction=SelectedDirection};translated.Clear();done="";seen=latest.Key+latest.Text;TranslateWhenIdle(epoch);}
        private void SetStatus(string value,bool error){if(!closing){if(footerLayout!=null){footerLayout.SetColumnSpan(status,1);footerRight.Visible=true;}status.Text="●  "+value;status.ForeColor=error?Color.FromArgb(190,72,72):Theme.BlueDark;tips.SetToolTip(status,value);}}
        private void SetGoogleStatus(string value,bool error){if(!closing){serviceMessage=(Preferences.Offline?T("离线：","Offline: "):"Google: ")+value;serviceError=error;googleStatus.Text=serviceMessage;googleStatus.ForeColor=error?Color.FromArgb(190,72,72):Theme.Muted;if(settingsPanel!=null&&!settingsPanel.IsDisposed)settingsPanel.UpdateService(serviceMessage,error);}}
        private async Task ReadSelection(IntPtr handle){
            if(!active||closing||pickingSource||locked==null||handle!=locked.Handle)return;
            if(!locked.IsAlive()){UnlockSource(true);return;}
            CancelCapture();var cancellation=new CancellationTokenSource();captureCancellation=cancellation;
            int requestEpoch=epoch,requestSelection=selectionEpoch;var sourceWindow=locked;
            Task<SelectionClipboardResult> captureTask=null;
            SetStatus(T("正在复制选中文字…","Copying your selection…"),false);
            try{
                captureTask=SelectionClipboard.CaptureAsync(handle,(uint)sourceWindow.ProcessId,cancellation.Token);
                captureTransactions.Add(captureTask);
                var data=await captureTask;
                if(!active||closing||requestEpoch!=epoch||requestSelection!=selectionEpoch||locked!=sourceWindow)return;
                SelectionDiagnostics.Save(new Dictionary<string,object>{{"status",data.Status},{"diagnostics",new Dictionary<string,object>{{"route","clipboard"},{"pointLength",data.Length},{"phase",data.RestoreStatus??"unknown"}}}},0);
                if(!data.Ok){SetStatus(CopyStatus(data.Status),false);return;}
                string source="selection:"+handle.ToInt64().ToString("X");
                if(!needsFreshSelection&&latest!=null&&latest.Conversation==source&&latest.Text==data.Text&&latest.Direction==SelectedDirection&&done==seen){SetStatus(T("这段文字的译文已经显示","This selection is already translated"),false);return;}
                AcceptText(data.Text,source);
            }catch{if(requestEpoch==epoch&&requestSelection==selectionEpoch&&!closing)SetStatus(T("复制未完成，请重新划选或使用粘贴翻译","Could not copy; select again or use Paste"),true);}
            finally{if(captureTask!=null)captureTransactions.Remove(captureTask);if(captureCancellation==cancellation)captureCancellation=null;cancellation.Dispose();}
        }
        private string CopyStatus(string reason){
            string message;
            switch(reason){
                case "password_denied":message=T("密码输入框不会读取","Password fields are not read");break;
                case "unsafe_target":message=T("这个窗口不支持自动复制，请手动复制后粘贴","Auto-copy is disabled for this window; copy manually and use Paste");break;
                case "text_too_long":message=T("选中文字超过 32,000 字符，请少选一些","Selection exceeds 32,000 characters; select less text");break;
                case "foreground_changed":case "not_foreground":message=T("窗口已切换；请回到所选窗口重新划选","Window changed; select again in your chosen window");break;
                case "modifier_held":case "mouse_down":message=T("请松开鼠标和快捷键后重新划选","Release the mouse and modifier keys, then select again");break;
                case "clipboard_changed":message=T("剪贴板同时被修改，请重新划选","Clipboard changed during copying; select again");break;
                case "copy_input_failed":message=T("系统未接受复制快捷键；请手动复制后粘贴","Windows did not accept Copy; copy manually and use Paste");break;
                default:message=T("没有复制到新文字；可重新划选或使用粘贴翻译","No new text was copied; select again or use Paste");break;
            }
            return message+(latest!=null?T(" · 保留上一段译文"," · Previous translation retained"):"");
        }
        private string CaptureFavoriteSourceTitle(string source){
            if(source=="manual")return T("粘贴的文字","Pasted text");
            if(locked==null)return T("所选文字","Selected text");
            if(!preview)try{var current=SourceWindow.FromHandle(locked.Handle);if(current!=null&&current.Matches(locked.Handle,locked.ProcessId,locked.Started))return current.Title;}catch{}
            return locked.Title??"";
        }
        private void AcceptText(string text,string source){AcceptTextCore(text,source,CaptureFavoriteSourceTitle(source));}
        private void AcceptTextCore(string text,string source,string sourceTitle){
            ClearOfflineRetry();
            currentFavorite=null;currentFavoriteEntry=null;latestSourceTitle=sourceTitle;
            needsFreshSelection=false;
            viewingHistory=false;
            history.LeaveView();RefreshHistoryButtons();
            ++epoch;if(translating)worker.Dispose();wait.Stop();
            latest=new Snapshot{Conversation=source,MessageId=epoch.ToString(),Text=text,Direction=SelectedDirection};
            original.Text=DisplayLines(text);translated.Clear();done="";seen=latest.Key+text;TranslateWhenIdle(epoch);
        }
        private void PasteText(){
            CancelCapture();
            try{string text=Clipboard.ContainsText()?Clipboard.GetText():"";if(String.IsNullOrWhiteSpace(text)){SetStatus(T("剪贴板里没有文字","No text in the clipboard"),true);return;}if(text.Length>32000){SetStatus(T("请分段粘贴，每次不超过 32,000 字符","Paste up to 32,000 characters at a time"),true);return;}AcceptText(text,"manual");}
            catch{SetStatus(T("剪贴板暂不可用，请重试","Clipboard unavailable; try again"),true);}
        }
        private async void TranslateWhenIdle(int requested){while(translating&&!closing&&requested==epoch)await Task.Delay(100);if(!closing&&requested==epoch)await TranslateLatest();}
        private async Task TranslateLatest(){
            if(translating||latest==null||closing)return;
            if(Preferences.Offline&&!(direction.SelectedItem is LanguagePack)){SetStatus(T("请先点「离线语言包」安装所需方向","Open Offline packs and install the direction you need"),true);return;}
            var request=latest;int requestedEpoch=epoch;bool offlineMode=Preferences.Offline;string sourceTitle=latestSourceTitle;translating=true;
            wait.Start(TranslationWait(),delegate(string text){SetStatus(text,false);},Preferences.Offline);
            try{string result=await worker.Translate(request.Text,request.Direction);if(!closing&&ResultGate.CanShow(requestedEpoch,epoch,request,latest)){wait.Stop();translated.Text=DisplayLines(result);done=request.Key+request.Text;history.Add(request.Text,result,request.Direction,offlineMode);RememberFavoriteResult(request,result,offlineMode,sourceTitle);RefreshHistoryButtons();if(connectionWorker==null)SetGoogleStatus(T("上次翻译完成 · ","Last translation completed · ")+DateTime.Now.ToString("HH:mm"),false);SetStatus(T("译文已更新 · ","Translation updated · ")+DateTime.Now.ToString("HH:mm"),false);}}
            catch(Exception ex){worker.Dispose();wait.Stop();if(!closing&&requestedEpoch==epoch){SetStatus(Preferences.Offline?T("离线翻译未完成，可检测离线库后重试","Offline translation failed; test the offline library and retry"):T("翻译未完成，可检测连接后重新划选","Translation failed; test Google and select again"),true);SetGoogleStatus(ex is InvalidOperationException?ex.Message:T("翻译未完成，请重试","Translation failed; try again"),true);RecordOfflineRetry(request,requestedEpoch,offlineMode,ex);}}
            finally{translating=false;wait.Stop();}
        }
        private async Task CheckGoogleConnection(){
            if(connectionWorker!=null||closing)return;
            if(Preferences.Offline&&!(direction.SelectedItem is LanguagePack)){SetGoogleStatus(PackStatus(),true);return;}
            checkConnection.Enabled=false;var probe=new Worker();connectionWorker=probe;bool offlineMode=Preferences.Offline;string probeDirection=offlineMode?SelectedDirection:"auto:zh-CN";
            string sample="Please keep the original file.";
            if(offlineMode){string source=((LanguagePack)direction.SelectedItem).From;switch(source){case "zh":case "zt":sample="请保留原始文件。";break;case "ja":sample="元のファイルを保存してください。";break;case "ko":sample="원본 파일을 보관하세요.";break;case "de":sample="Bitte behalten Sie die Originaldatei.";break;case "fr":sample="Veuillez conserver le fichier original.";break;case "es":sample="Conserve el archivo original.";break;case "ru":sample="Сохраните исходный файл.";break;}}
            connectionWait.Start(T("正在用固定示例检测…","Testing with a fixed sample…"),delegate(string text){SetGoogleStatus(text,false);},offlineMode);
            try{string result=await probe.Translate(sample,probeDirection,offlineMode);connectionWait.Stop();if(offlineMode!=Preferences.Offline||(offlineMode&&probeDirection!=SelectedDirection))return;if(String.IsNullOrWhiteSpace(result)||(!offlineMode&&!System.Text.RegularExpressions.Regex.IsMatch(result,"[\\u3400-\\u9fff]")))throw new InvalidOperationException(T("服务返回异常，请重试","Unexpected service response; try again"));SetGoogleStatus((offlineMode?T("离线库可用 · ","Offline library ready · "):T("连接成功 · ","Connected · "))+DateTime.Now.ToString("HH:mm"),false);}
            catch(Exception ex){connectionWait.Stop();if(offlineMode==Preferences.Offline)SetGoogleStatus(ex is InvalidOperationException?ex.Message:T("检测未完成，请重试","Test failed; try again"),true);}
            finally{probe.Dispose();connectionWorker=null;if(!closing)checkConnection.Enabled=true;}
        }
        private async Task OpenCompose(){
            WindowChoice choice=null;
            try{if(composeReaderProcess!=null)return;
                var p=new Process{StartInfo=new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ClaudeReader.exe"),"--list"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8}};composeReaderProcess=p;
                try{p.Start();var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();if(await Task.WhenAny(output,Task.Delay(6000))==output){var data=Json.Decode(await output);object rows;if(data.TryGetValue("windows",out rows))foreach(var row in (System.Collections.IEnumerable)rows){var item=row as Dictionary<string,object>;if(item!=null){choice=new WindowChoice{Handle=Json.Str(item,"handle"),Title=Json.Str(item,"title")};break;}}}}
                finally{composeReaderProcess=null;try{if(!p.HasExited)p.Kill();}catch{}p.Dispose();}
            }catch{}
            if(!closing)new ComposeForm(choice??new WindowChoice{Handle="0",Title=T("未连接 Claude","Claude not connected")}){TopMost=TopMost}.Show();
        }
        private static string DisplayLines(string text){return text.Replace("\r\n","\n").Replace("\n","\r\n");}
        public void RenderPreview(string path,bool retry=false){
            translated.Text=T("我会保留原始文件。\r\n\r\n导出前，请先检查预览。\r\n只翻译你选中的文字。","I will keep the original files.\r\n\r\nCheck the preview before exporting.\r\nOnly the text you selected is translated.");
            original.Text="Keep the original files.\r\n\r\nCheck the preview before exporting.\r\nTranslate only the text you selected.";
            SetStatus(T("固定示例 · 不是实时翻译结果","Fixed sample · Not a live translation"),false);
            if(retry){
                translated.Clear();latest=new Snapshot{Conversation="preview",MessageId="retry",Text=original.Text,Direction=SelectedDirection};
                var error=new TranslationFailure("rate_limited");SetGoogleStatus(error.Message,true);RecordOfflineRetry(latest,epoch,false,error);
            }
            TopMost=false;ShowInTaskbar=false;Opacity=0;Show();Application.DoEvents();
            using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(Point.Empty,Size));bitmap.Save(path);}Close();
        }
    }
}
