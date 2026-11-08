using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClaudeCompanion {
    // Lists window identities only. No desktop capture or accessibility-tree scan is used.
    internal sealed class SourceWindowPickerForm : Form {
        private readonly SourceWindow current;
        private readonly bool fixtureMode;
        private readonly List<SourceWindow> fixtures;
        private readonly Dictionary<string,Bitmap> icons=new Dictionary<string,Bitmap>();
        private readonly HashSet<string> iconAttempts=new HashSet<string>();
        private readonly ToolTip tooltips=new ToolTip { AutoPopDelay=15000,InitialDelay=400,ReshowDelay=100 };
        private readonly TextBox search;
        private readonly PickerSearchField searchField;
        private readonly FlowLayoutPanel cardsPanel;
        private readonly Label heading,description,hint,footnote;
        private readonly Button refresh,cancel;
        private List<SourceWindow> allSources=new List<SourceWindow>();
        private readonly List<SourceWindowCard> cards=new List<SourceWindowCard>();
        private CancellationTokenSource loadCancellation;
        private int generation,iconGeneration=-1;
        private bool loading,stopping,closedHint;
        public SourceWindow SelectedSource { get; private set; }

        public SourceWindowPickerForm(SourceWindow current) : this(current,null) { }
        // Fixtures deliberately bypass all enumeration and native icon retrieval.
        internal SourceWindowPickerForm(SourceWindow current,IEnumerable<SourceWindow> fixtures) {
            SuspendLayout();
            this.current=current;fixtureMode=fixtures!=null;
            if(fixtureMode)this.fixtures=new List<SourceWindow>(fixtures);
            Ui.ApplyIcon(this);
            Font=new Font("Microsoft YaHei UI",10F);BackColor=Theme.Cream;ForeColor=Theme.Ink;
            AutoScaleDimensions=new SizeF(96F,96F);AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterParent;
            Size=new Size(770,660);MinimumSize=new Size(610,480);ShowInTaskbar=false;
            MinimizeBox=false;MaximizeBox=false;KeyPreview=true;
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(22,14,22,16) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,92));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,50));
            var header=new Panel { Dock=DockStyle.Fill,Margin=new Padding(0) };
            header.Controls.Add(new WhaleBadge { Location=new Point(0,9),Size=new Size(72,60) });
            heading=new Label { Location=new Point(86,8),Height=34,AutoSize=false,Font=new Font(Font.FontFamily,18,FontStyle.Bold),ForeColor=Theme.Ink };
            description=new Label { Location=new Point(88,48),Height=36,AutoSize=false,Font=new Font(Font.FontFamily,9F),ForeColor=Theme.Muted };
            header.Controls.Add(heading);header.Controls.Add(description);
            header.Resize+=delegate { heading.Width=Math.Max(1,header.ClientSize.Width-heading.Left);description.Width=Math.Max(1,header.ClientSize.Width-description.Left); };
            layout.Controls.Add(header,0,0);

            var searchRow=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0) };
            searchRow.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,99));
            searchField=new PickerSearchField { Dock=DockStyle.Fill,Margin=new Padding(0,3,12,5),TabIndex=0 };
            search=searchField.Input;
            search.TextChanged+=delegate { RenderCards(); };
            refresh=Theme.Button("",false);refresh.Dock=DockStyle.Fill;refresh.Margin=new Padding(0,3,0,5);refresh.TabIndex=1;
            refresh.Click+=async delegate { closedHint=false;await RefreshWindowsAsync(); };
            searchRow.Controls.Add(searchField,0,0);searchRow.Controls.Add(refresh,1,0);
            layout.Controls.Add(searchRow,0,1);
            hint=new Label { Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted,Font=new Font(Font.FontFamily,9F),AutoEllipsis=true,Margin=new Padding(0) };
            layout.Controls.Add(hint,0,2);
            cardsPanel=new FlowLayoutPanel { Dock=DockStyle.Fill,AutoScroll=true,WrapContents=true,FlowDirection=FlowDirection.LeftToRight,BackColor=Theme.Cream,Margin=new Padding(0),Padding=new Padding(0,3,0,0),TabIndex=2 };
            cardsPanel.Resize+=delegate { ResizeCards(); };
            layout.Controls.Add(cardsPanel,0,3);
            var footer=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0,5,0,0) };
            footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,106));
            footnote=new Label { Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted,Font=new Font(Font.FontFamily,8.5F),Margin=new Padding(0,0,12,0) };
            cancel=Theme.Button("",false);cancel.Dock=DockStyle.Fill;cancel.DialogResult=DialogResult.Cancel;cancel.Margin=new Padding(0,4,0,1);cancel.TabIndex=3;
            footer.Controls.Add(footnote,0,0);footer.Controls.Add(cancel,1,0);layout.Controls.Add(footer,0,4);
            Controls.Add(layout);CancelButton=cancel;
            Ui.LanguageChanged+=RefreshLanguage;
            if(fixtureMode)allSources=new List<SourceWindow>(this.fixtures);
            RefreshLanguage();
            Shown+=async delegate {
                search.Focus();
                if(!fixtureMode)await RefreshWindowsAsync();
            };
            // Child handle creation during search/card layout can observe the
            // monitor DPI before construction finishes. Establish the design
            // baseline only once the whole hierarchy is ready.
            AutoScaleDimensions=new SizeF(96F,96F);ResumeLayout(true);
        }

        private string T(string zh,string en) { return Ui.T(zh,en); }
        private void RefreshLanguage() {
            if(stopping)return;
            Text=T("选择翻译窗口 · 大肥鱼","Choose a window · Whale Translator");
            heading.Text=T("这次想读哪个窗口？","Which window shall we read?");
            description.Text=T("点一下窗口卡片，就可以开始划选翻译。","Click a window card, then select text to translate.");
            searchField.Placeholder=T("搜索窗口或程序名","Search windows or apps");search.AccessibleName=T("搜索程序或窗口标题","Search app or window title");
            refresh.Text=T("刷新","Refresh");cancel.Text=T("取消","Cancel");
            footnote.Text=T("只读取所选窗口里的划选文字。方向键选择，Enter 确认。","Only selected text is read. Arrow keys to choose, Enter to confirm.");
            RenderCards();
        }

        private async Task RefreshWindowsAsync() {
            if(stopping||loading)return;
            loading=true;refresh.Enabled=false;iconAttempts.Clear();
            if(loadCancellation!=null) { loadCancellation.Cancel();loadCancellation.Dispose(); }
            loadCancellation=new CancellationTokenSource();var cancellation=loadCancellation.Token;
            int request=++generation;
            hint.Text=closedHint?T("这个窗口已关闭，正在刷新列表…","That window closed. Refreshing the list…"):T("正在寻找打开的窗口…","Finding your open windows…");
            try {
                var found=fixtureMode?new List<SourceWindow>(fixtures):await Task.Run(delegate { return SourceWindow.List(cancellation); },cancellation);
                if(stopping||request!=generation||cancellation.IsCancellationRequested)return;
                allSources=found;loading=false;RenderCards();
            } catch(OperationCanceledException) { }
            catch {
                if(!stopping&&request==generation)hint.Text=T("暂时无法获取窗口，请点刷新重试。","Could not list windows. Click Refresh to retry.");
            } finally {
                if(!stopping&&request==generation) { loading=false;refresh.Enabled=true; }
            }
        }

        private void RenderCards() {
            if(stopping||cardsPanel==null)return;
            string query=(search.Text??"").Trim();
            cardsPanel.SuspendLayout();tooltips.RemoveAll();
            while(cardsPanel.Controls.Count>0)cardsPanel.Controls[0].Dispose();
            cards.Clear();
            foreach(var source in OrderedSources(allSources)) {
                if(source==null||!MatchesSearch(source,query))continue;
                Bitmap icon;icons.TryGetValue(IdentityKey(source),out icon);
                bool isCurrent=current!=null&&source.Matches(current.Handle,current.ProcessId,current.Started);
                var card=new SourceWindowCard(source,isCurrent,icon,Font) { TabIndex=cards.Count,Margin=new Padding(0,0,12,10) };
                card.Click+=delegate(object sender,EventArgs args) { ChooseSource(((SourceWindowCard)sender).Source); };
                tooltips.SetToolTip(card,source.FriendlyAppName+Environment.NewLine+(source.Title??""));
                cards.Add(card);cardsPanel.Controls.Add(card);
            }
            if(cards.Count==0&&!loading) {
                var empty=new Label { AutoSize=false,Height=84,Margin=new Padding(12,20,12,0),ForeColor=Theme.Muted,TextAlign=ContentAlignment.TopLeft,
                    Text=query.Length>0?T("没有找到匹配的窗口。\r\n试试程序名或窗口标题里的其他词。","No matching windows.\r\nTry another app name or title."):T("还没有可选择的窗口。\r\n打开要阅读的应用，再点「刷新」。","No windows to choose yet.\r\nOpen the app you want to read, then click Refresh.") };
                cardsPanel.Controls.Add(empty);
            }
            ResizeCards();cardsPanel.ResumeLayout();UpdateHint();BeginIconLoad();
        }
        internal static bool MatchesSearch(SourceWindow source,string query) {
            return String.IsNullOrWhiteSpace(query)||(source.Title??"").IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0||
                source.FriendlyAppName.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0||(source.App??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
        }
        // Keep every selectable window. Only presentation priority changes; identity checks stay intact.
        internal static int WindowPriority(SourceWindow source) {
            string app=(source.App??"").ToLowerInvariant();
            switch(app) {
                case "claude":case "chatgpt":case "chrome":case "msedge":case "firefox":case "brave":case "opera":case "vivaldi":
                case "winword":case "wps":case "wpp":case "et":case "acrobat":case "acrord32":case "sumatrapdf":case "foxitpdfreader":case "foxitreader":return 0;
                case "ashotplugctrl":case "asmonitorcontrol":case "taskmgr":case "regedit":case "mmc":case "nvcplui":case "radeonsoftware":case "control":return 2;
                default:return app.StartsWith("asmonitor",StringComparison.Ordinal)||app.StartsWith("ashotplug",StringComparison.Ordinal)?2:1;
            }
        }
        internal static List<SourceWindow> OrderedSources(IEnumerable<SourceWindow> sources) {
            var result=new List<SourceWindow>();
            for(int priority=0;priority<3;priority++)foreach(var source in sources)if(source!=null&&WindowPriority(source)==priority)result.Add(source);
            return result;
        }
        private int CardColumns {
            get { return cards.Count>1&&cards[0].Top==cards[1].Top?2:1; }
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
            Keys key=keyData&Keys.KeyCode;
            if((keyData&Keys.Modifiers)!=Keys.None||cards.Count==0)return base.ProcessCmdKey(ref msg,keyData);
            int selected=cards.FindIndex(delegate(SourceWindowCard card){return card.Focused;});
            if(selected<0&&search.Focused&&(key==Keys.Down||key==Keys.Up)) {
                FocusCard(key==Keys.Down?0:cards.Count-1);return true;
            }
            if(selected>=0&&(key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down)) {
                FocusCard(NavigationIndex(selected,cards.Count,CardColumns,key));return true;
            }
            return base.ProcessCmdKey(ref msg,keyData);
        }
        private void FocusCard(int index) {
            cards[index].Focus();cardsPanel.ScrollControlIntoView(cards[index]);
        }
        internal static int NavigationIndex(int index,int count,int columns,Keys key) {
            int step=key==Keys.Left?-1:key==Keys.Right?1:key==Keys.Up?-columns:columns;
            return Math.Max(0,Math.Min(count-1,index+step));
        }
        private void ResizeCards() {
            if(cardsPanel==null)return;
            float scale;
            using(var graphics=CreateGraphics())scale=graphics.DpiX/96F;
            int available=Math.Max(120,cardsPanel.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-(int)Math.Round(4*scale));
            int columns=available>=650*scale?2:1;int width=available/columns-(int)Math.Round(12*scale);
            foreach(Control child in cardsPanel.Controls) {
                child.Width=width;
                // Search/refresh creates cards after WinForms' initial DPI pass.
                // Derive these dimensions from the device each time, not from
                // previously scaled values, so fresh and initial cards match.
                if(child is SourceWindowCard) {
                    child.Height=(int)Math.Round(65*scale);
                    child.Margin=new Padding(0,0,(int)Math.Round(12*scale),(int)Math.Round(10*scale));
                } else {
                    child.Height=(int)Math.Round(84*scale);
                    child.Margin=new Padding((int)Math.Round(12*scale),(int)Math.Round(20*scale),(int)Math.Round(12*scale),0);
                }
            }
        }
        protected override void OnLoad(EventArgs e) { base.OnLoad(e);ResizeCards(); }
        private void UpdateHint() {
            if(loading)return;
            hint.ForeColor=closedHint?Color.FromArgb(190,72,72):Theme.Muted;
            hint.Text=closedHint?T("这个窗口已关闭，请选择其他卡片。","That window closed. Please choose another card."):
                T("找到 ","Found ")+cards.Count+T(" 个窗口 · 点击卡片即可选择"," windows · Click a card to choose");
        }
        private async void ChooseSource(SourceWindow source) {
            if(stopping||loading)return;
            if(source==null||!source.IsAlive()) {
                closedHint=true;await RefreshWindowsAsync();return;
            }
            SelectedSource=source;DialogResult=DialogResult.OK;Close();
        }
        private static string IdentityKey(SourceWindow source) { return source.Handle.ToInt64()+":"+source.ProcessId+":"+source.Started; }
        private async void BeginIconLoad() {
            if(stopping||fixtureMode||loading||loadCancellation==null||iconGeneration==generation)return;
            int request=generation;var cancellation=loadCancellation.Token;iconGeneration=request;
            try {
                // One bounded icon query at a time; a search change changes the next candidate.
                while(!stopping&&request==generation&&!cancellation.IsCancellationRequested) {
                    SourceWindow next=null;string key=null;
                    foreach(var card in cards) {
                        string candidate=IdentityKey(card.Source);
                        if(!icons.ContainsKey(candidate)&&!iconAttempts.Contains(candidate)) { next=card.Source;key=candidate;break; }
                    }
                    if(next==null)break;
                    Bitmap icon=await Task.Run(delegate { return cancellation.IsCancellationRequested?null:next.GetSmallIcon(); });
                    if(stopping||request!=generation||cancellation.IsCancellationRequested) { if(icon!=null)icon.Dispose();return; }
                    iconAttempts.Add(key);
                    if(icon!=null) {
                        icons[key]=icon;
                        foreach(var card in cards)if(IdentityKey(card.Source)==key) { card.IconImage=icon;card.Invalidate(); }
                    }
                }
            } catch { /* A missing icon does not make a window unavailable. */ }
            finally { if(iconGeneration==request)iconGeneration=-1; }
        }
        protected override void OnFormClosing(FormClosingEventArgs e) {
            base.OnFormClosing(e);if(e.Cancel)return;
            stopping=true;++generation;if(loadCancellation!=null)loadCancellation.Cancel();
        }
        protected override void Dispose(bool disposing) {
            if(disposing&&!stopping) { stopping=true;++generation;if(loadCancellation!=null)loadCancellation.Cancel(); }
            if(disposing) {
                Ui.LanguageChanged-=RefreshLanguage;tooltips.Dispose();
                if(loadCancellation!=null) { loadCancellation.Dispose();loadCancellation=null; }
                foreach(var icon in icons.Values)icon.Dispose();icons.Clear();
            }
            base.Dispose(disposing);
        }
        internal void RenderPreview(string path) {
            if(!fixtureMode)throw new InvalidOperationException("Preview requires synthetic window fixtures.");
            TopMost=false;ShowInTaskbar=false;Opacity=0;Show();Application.DoEvents();
            using(var bitmap=new Bitmap(Width,Height)) { DrawToBitmap(bitmap,new Rectangle(Point.Empty,Size));bitmap.Save(path); }
            Close();
        }
    }

    internal sealed class SourceWindowCard : Control {
        internal readonly SourceWindow Source;
        internal readonly bool IsCurrent;
        internal Bitmap IconImage;
        private readonly Font appFont,titleFont;
        private bool hover;
        internal SourceWindowCard(SourceWindow source,bool current,Bitmap icon,Font font) {
            Source=source;IsCurrent=current;IconImage=icon;Font=font;
            appFont=new Font(font.FontFamily,9F,FontStyle.Bold);titleFont=new Font(font.FontFamily,9F);
            Height=65;TabStop=true;Cursor=Cursors.Hand;
            AccessibleRole=AccessibleRole.PushButton;AccessibleName=source.FriendlyAppName+" · "+source.Title;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover=true;Invalidate();base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover=false;Invalidate();base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if(e.Button==MouseButtons.Left)Focus();base.OnMouseDown(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate();base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate();base.OnLostFocus(e); }
        protected override void OnKeyDown(KeyEventArgs e) {
            if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space) { e.Handled=true;e.SuppressKeyPress=true;OnClick(EventArgs.Empty); }
            base.OnKeyDown(e);
        }
        protected override bool IsInputKey(Keys keyData) { return (keyData&Keys.KeyCode)==Keys.Enter||base.IsInputKey(keyData); }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g=e.Graphics;g.Clear(Theme.Cream);g.SmoothingMode=SmoothingMode.AntiAlias;
            if(Width<20||Height<20)return;
            float scale=g.DpiX/96F;
            var face=new RectangleF(scale,scale,Width-3*scale,Height-4*scale);
            using(var path=Theme.RoundedRect(face,12f*scale))
            using(var fill=new SolidBrush(IsCurrent?Theme.Blue:hover?Theme.Mist:Color.White))
            using(var border=new Pen(IsCurrent||Focused||hover?Theme.BlueDark:Theme.Line,(Focused?2f:1f)*scale)) { g.FillPath(fill,path);g.DrawPath(border,path); }
            var iconBox=Scaled(14,17,28,28,scale);
            if(IconImage!=null)g.DrawImage(IconImage,iconBox);
            else {
                using(var block=Theme.RoundedRect(iconBox,7f*scale))using(var brush=new SolidBrush(Theme.Blue))g.FillPath(brush,block);
                string appName=Source.FriendlyAppName;string initial=String.IsNullOrWhiteSpace(appName)?"?":System.Globalization.StringInfo.GetNextTextElement(appName).ToUpperInvariant();
                TextRenderer.DrawText(g,initial,appFont,iconBox,Theme.BlueDark,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
            }
            int textLeft=(int)(54*scale),textWidth=Math.Max(1,Width-textLeft-(int)(14*scale));
            if(IsCurrent) {
                string badge=Ui.T("正在使用","Current");int badgeWidth=TextRenderer.MeasureText(badge,appFont).Width+(int)(8*scale);
                TextRenderer.DrawText(g,badge,appFont,new Rectangle(Width-badgeWidth-(int)(13*scale),(int)(9*scale),badgeWidth,(int)(22*scale)),Theme.BlueDark,TextFormatFlags.Right|TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter);
                textWidth=Math.Max(1,textWidth-badgeWidth-(int)(8*scale));
            }
            TextRenderer.DrawText(g,Source.FriendlyAppName,appFont,new Rectangle(textLeft,(int)(9*scale),textWidth,(int)(22*scale)),Theme.Ink,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g,Source.Title??"",titleFont,new Rectangle(textLeft,(int)(31*scale),Math.Max(1,Width-textLeft-(int)(14*scale)),(int)(22*scale)),Theme.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter);
            if(Focused&&ShowFocusCues)using(var focus=Theme.RoundedRect(RectangleF.Inflate(face,-4*scale,-4*scale),9f*scale))using(var pen=new Pen(Theme.BlueDark,scale) { DashStyle=DashStyle.Dot })g.DrawPath(pen,focus);
        }
        private static Rectangle Scaled(int x,int y,int width,int height,float scale){return new Rectangle((int)(x*scale),(int)(y*scale),(int)(width*scale),(int)(height*scale));}
        protected override void Dispose(bool disposing) { if(disposing) { appFont.Dispose();titleFont.Dispose(); }base.Dispose(disposing); }
    }

    internal sealed class PickerSearchField : Panel {
        internal readonly TextBox Input;
        private string placeholder="";
        internal string Placeholder { get{return placeholder;}set{placeholder=value;SetCue();} }
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr param,string text);
        internal PickerSearchField() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            BackColor=Theme.Cream;Padding=new Padding(39,9,12,6);
            Input=new TextBox { BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink,TabIndex=0 };
            Input.HandleCreated+=delegate{SetCue();};Input.GotFocus+=delegate{Invalidate();};Input.LostFocus+=delegate{Invalidate();};Controls.Add(Input);
            Click+=delegate{Input.Focus();};
        }
        private void SetCue(){if(Input.IsHandleCreated)SendMessage(Input.Handle,0x1501,new IntPtr(1),placeholder);}
        protected override void OnLayout(LayoutEventArgs e) {
            base.OnLayout(e);if(Input==null)return;
            float scale;using(var graphics=CreateGraphics())scale=graphics.DpiX/96F;
            int left=(int)Math.Round(39*scale),right=(int)Math.Round(12*scale);
            Input.SetBounds(left,Math.Max(0,(Height-Input.PreferredHeight)/2),Math.Max(1,Width-left-right),Input.PreferredHeight);
        }
        protected override void OnPaint(PaintEventArgs e){
            var g=e.Graphics;g.Clear(Parent==null?Theme.Cream:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;float scale=g.DpiX/96F;
            using(var path=Theme.RoundedRect(new RectangleF(scale,scale,Width-3*scale,Height-3*scale),12*scale))
            using(var fill=new SolidBrush(Color.White))using(var pen=new Pen(Input.Focused?Theme.BlueDark:Theme.Line,scale)){g.FillPath(fill,path);g.DrawPath(pen,path);}
            float iconTop=(Height-18*scale)/2;
            using(var pen=new Pen(Theme.Muted,1.6f*scale)){g.DrawEllipse(pen,14*scale,iconTop,10*scale,10*scale);g.DrawLine(pen,23*scale,iconTop+9*scale,28*scale,iconTop+14*scale);}
        }
    }
}
