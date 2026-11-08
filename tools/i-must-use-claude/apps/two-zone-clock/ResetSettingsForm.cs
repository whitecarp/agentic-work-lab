using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace TwoZoneClock {
    internal static class ResetCaptureClient {
        internal static Task<ResetWindowResult> ReadAsync(ResetSourceWindow window) {
            return Task.Run(delegate {
                if (window == null) return new ResetWindowResult { Status="window_closed" };
                try {
                    var start=new ProcessStartInfo(Application.ExecutablePath,
                        "--read-reset-window "+window.Handle.ToString(CultureInfo.InvariantCulture)+" "+window.ProcessId.ToString(CultureInfo.InvariantCulture)+" "+window.StartTicks.ToString(CultureInfo.InvariantCulture)) {
                        UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
                        RedirectStandardOutput=true,RedirectStandardError=true,
                        StandardOutputEncoding=new System.Text.UTF8Encoding(false),StandardErrorEncoding=new System.Text.UTF8Encoding(false)
                    };
                    using (var child=new Process { StartInfo=start }) {
                        child.Start();
                        var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();
                        if (!child.WaitForExit(4500)) {
                            try { child.Kill();child.WaitForExit(1000); } catch (InvalidOperationException) { }
                            return new ResetWindowResult { Status="timeout" };
                        }
                        string json=output.GetAwaiter().GetResult();error.GetAwaiter().GetResult();
                        if(child.ExitCode!=0||json.Length>100000)return new ResetWindowResult { Status="read_failed" };
                        var result=new JavaScriptSerializer { MaxJsonLength=100000 }.Deserialize<ResetWindowResult>(json);
                        if(result==null||result.Text!=null&&result.Text.Length>16000)return new ResetWindowResult { Status="read_failed" };
                        return result;
                    }
                } catch { return new ResetWindowResult { Status="read_failed" }; }
            });
        }
    }

    internal sealed class ResetSettingsForm : Form {
        private readonly bool english,preview;
        private readonly CheckBox enabled,auto;
        private readonly ComboBox windows,zones;
        private readonly Label result;
        private readonly TextBox pasted;
        private readonly CheckBox sessionSet,weeklySet;
        private readonly DateTimePicker sessionDate,weeklyDate;
        private readonly Button read;
        private readonly Button apply;
        private readonly CandyButton[] modeButtons=new CandyButton[3];
        private readonly Panel[] pages=new Panel[3];
        private readonly RoundedCard contentCard,statusCard;
        private readonly Icon themedIcon;
        private readonly ResetSourceWindow initialWindow;
        private bool busy;
        private int captureGeneration;
        internal ResetSnapshot Snapshot { get; private set; }
        internal ResetSourceWindow SourceWindow { get; private set; }
        internal bool CountdownEnabled { get { return enabled.Checked; } }
        internal bool AutoRead { get { return auto.Checked&&SourceWindow!=null; } }
        internal string DisplayZoneId { get { return zones.SelectedItem is RegionChoice ? ((RegionChoice)zones.SelectedItem).Id : "local"; } }
        private string T(string zh,string en){return english?en:zh;}

        internal ResetSettingsForm(ResetSnapshot saved,bool show,bool useEnglish,string zoneId,ResetSourceWindow source,bool automatic,bool isPreview) {
            SuspendLayout();
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            english=useEnglish;preview=isPreview;initialWindow=source;SourceWindow=source;
            Snapshot=Copy(saved);Text=T("Claude 重置倒计时","Claude reset countdown");
            themedIcon=WhaleTheme.CreateIcon();Icon=themedIcon;ShowInTaskbar=false;
            ClientSize=new Size(580,430);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;
            BackColor=WhaleTheme.Surface;ForeColor=WhaleTheme.Ink;Font=new Font("Microsoft YaHei UI",9f);DoubleBuffered=true;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=6,BackColor=WhaleTheme.Surface};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));root.RowStyles.Add(new RowStyle(SizeType.Absolute,26));root.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,78));root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
            root.Controls.Add(new ResetHeader(english){Dock=DockStyle.Fill,Margin=Padding.Empty},0,0);
            enabled=new CheckBox{Dock=DockStyle.Fill,Checked=show,Margin=Padding.Empty,Text=T("在时钟下面显示本轮和每周倒计时","Show session and weekly countdowns below the clock")};root.Controls.Add(enabled,0,1);
            var zoneRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Margin=new Padding(0,0,0,4)};zoneRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,126));zoneRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            zoneRow.Controls.Add(new Label{Dock=DockStyle.Fill,Margin=Padding.Empty,Text=T("页面显示的时区","Page timezone"),TextAlign=ContentAlignment.MiddleLeft},0,0);
            zones=new ComboBox{Anchor=AnchorStyles.Left|AnchorStyles.Right,DropDownStyle=ComboBoxStyle.DropDownList,Margin=Padding.Empty,BackColor=Color.White,ForeColor=WhaleTheme.Ink};
            foreach(var zone in RegionChoice.All(english))zones.Items.Add(zone);
            foreach(RegionChoice zone in zones.Items)if(zone.Id==zoneId)zones.SelectedItem=zone;
            if(zones.SelectedIndex<0)foreach(RegionChoice zone in zones.Items)if(zone.Id=="local")zones.SelectedItem=zone;
            zoneRow.Controls.Add(zones,1,0);root.Controls.Add(zoneRow,0,2);
            var body=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};
            var segments=new TableLayoutPanel{Dock=DockStyle.Top,Height=32,ColumnCount=3,RowCount=1,Margin=Padding.Empty};
            for(int i=0;i<3;i++)segments.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3f));
            string[] labels={T("从 Claude 读取","Read Claude"),T("粘贴用量文字","Paste usage text"),T("手动设置","Set manually")};
            for(int i=0;i<3;i++) {
                int index=i;var segment=new CandyButton{Name="resetMode"+i.ToString(CultureInfo.InvariantCulture),Text=labels[i],Dock=DockStyle.Fill,Margin=new Padding(i==0?0:3,0,i==2?0:3,2),TabIndex=i};
                segment.AccessibleName=labels[i];segment.Click+=delegate{SelectPage(index);};modeButtons[i]=segment;segments.Controls.Add(segment,i,0);
            }
            contentCard=new RoundedCard{Name="resetContentCard",Dock=DockStyle.Fill,Padding=new Padding(10,8,10,8),Margin=Padding.Empty,FillColor=Color.White,Radius=12};
            var capture=new Panel{Name="capturePage",Dock=DockStyle.Fill,BackColor=Color.White};pages[0]=capture;
            var captureGrid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=4,Margin=Padding.Empty,BackColor=Color.White};captureGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));captureGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,112));
            captureGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,30));captureGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,32));captureGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,26));captureGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            windows=new ComboBox{Anchor=AnchorStyles.Left|AnchorStyles.Right,DropDownStyle=ComboBoxStyle.DropDownList,Margin=new Padding(0,0,6,0)};captureGrid.Controls.Add(windows,0,0);
            var refresh=Button(T("刷新列表","Refresh list"));refresh.Dock=DockStyle.Fill;refresh.Margin=Padding.Empty;refresh.Click+=delegate{if(!preview)RefreshWindows();};captureGrid.Controls.Add(refresh,1,0);
            read=Button(T("读取用量页","Read usage page"));WhaleTheme.StyleButton(read,true);read.Click+=async delegate{await CaptureUsage();};captureGrid.Controls.Add(read,0,1);captureGrid.SetColumnSpan(read,2);
            auto=new CheckBox{Dock=DockStyle.Fill,Margin=Padding.Empty,Checked=automatic,Text=T("每 60 秒自动读取（本次打开时钟期间）","Read every 60 seconds (until the clock closes)")};captureGrid.Controls.Add(auto,0,2);captureGrid.SetColumnSpan(auto,2);
            var captureHelp=new Label{Dock=DockStyle.Fill,Margin=Padding.Empty,Text=T("仅读取可见的用量文字；失败时保留并标注上次结果。\n先读取成功，再点应用，即可自动读取。","Reads visible usage text; failed reads keep the previous result.\nRead successfully, then Apply to enable automatic reading."),ForeColor=WhaleTheme.Muted,Font=new Font("Microsoft YaHei UI",8.3f)};captureGrid.Controls.Add(captureHelp,0,3);captureGrid.SetColumnSpan(captureHelp,2);capture.Controls.Add(captureGrid);
            var pastePage=new Panel{Name="pastePage",Dock=DockStyle.Fill,BackColor=Color.White};pages[1]=pastePage;
            var pasteGrid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};pasteGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100));pasteGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
            pasted=new TextBox{Multiline=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,MaxLength=16000,Margin=Padding.Empty,BorderStyle=BorderStyle.FixedSingle};pasteGrid.Controls.Add(pasted,0,0);
            var parse=Button(T("识别粘贴内容","Read pasted text"));parse.Click+=delegate{StageText();};pasteGrid.Controls.Add(parse,0,1);pastePage.Controls.Add(pasteGrid);
            var manualPage=new Panel{Name="manualPage",Dock=DockStyle.Fill,BackColor=Color.White};pages[2]=manualPage;
            var manual=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=4,Margin=Padding.Empty};manual.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));manual.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(float height in new[]{30f,30f,32f})manual.RowStyles.Add(new RowStyle(SizeType.Absolute,height));manual.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            sessionSet=new CheckBox{Dock=DockStyle.Fill,Margin=Padding.Empty,Text=T("本轮重置","Session reset"),Checked=ResetCountdown.ParseUtc(Snapshot.SessionResetUtc).HasValue};weeklySet=new CheckBox{Dock=DockStyle.Fill,Margin=Padding.Empty,Text=T("每周重置","Weekly reset"),Checked=ResetCountdown.ParseUtc(Snapshot.WeeklyResetUtc).HasValue};
            sessionDate=DateInput(Snapshot.SessionResetUtc);weeklyDate=DateInput(Snapshot.WeeklyResetUtc);
            manual.Controls.Add(sessionSet,0,0);manual.Controls.Add(sessionDate,1,0);manual.Controls.Add(weeklySet,0,1);manual.Controls.Add(weeklyDate,1,1);
            var useManual=Button(T("使用手动时间","Use manual times"));useManual.Click+=delegate{StageManualTimes();};manual.Controls.Add(useManual,0,2);manual.SetColumnSpan(useManual,2);
            var manualHelp=new Label{Dock=DockStyle.Fill,Margin=Padding.Empty,Text=T("按上方选择的时区填写日期和时间。\n不自动推迟五小时，也不会修改电脑时区。","Enter dates and times in the selected timezone.\nNo automatic five-hour rollover or system timezone changes."),ForeColor=WhaleTheme.Muted,Font=new Font("Microsoft YaHei UI",8.3f)};manual.Controls.Add(manualHelp,0,3);manual.SetColumnSpan(manualHelp,2);manualPage.Controls.Add(manual);
            foreach(var page in pages)contentCard.Controls.Add(page);
            body.Controls.Add(contentCard);body.Controls.Add(segments);root.Controls.Add(body,0,3);SelectPage(0);
            statusCard=new RoundedCard{Name="resetStatusCard",Dock=DockStyle.Fill,Padding=new Padding(10,5,10,5),Margin=new Padding(0,4,0,4),Radius=11,DrawShadow=false};
            result=new Label{Dock=DockStyle.Fill,Margin=Padding.Empty,ForeColor=WhaleTheme.Ink,BackColor=Color.Transparent};statusCard.Controls.Add(result);root.Controls.Add(statusCard,0,4);
            var actions=new FlowLayoutPanel{Name="resetActions",Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Margin=Padding.Empty,WrapContents=false};
            apply=Button(T("应用","Apply"));WhaleTheme.StyleButton(apply,true);apply.Click+=delegate{if(busy)return;DialogResult=DialogResult.OK;Close();};var cancel=Button(T("取消","Cancel"));cancel.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};var clear=Button(T("清空倒计时","Clear countdowns"));clear.Click+=delegate{captureGeneration++;Snapshot=new ResetSnapshot();SourceWindow=null;auto.Checked=false;ShowResult();};actions.Controls.AddRange(new Control[]{apply,cancel,clear});root.Controls.Add(actions,0,5);
            Controls.Add(root);CancelButton=cancel;Shown+=delegate{if(!preview)RefreshWindows();};zones.SelectedIndexChanged+=delegate{captureGeneration++;ShowResult();};windows.SelectedIndexChanged+=delegate{captureGeneration++;};auto.CheckedChanged+=delegate{captureGeneration++;};ShowResult();
            ResumeLayout(true);
        }
        private void SelectPage(int index) {
            for(int i=0;i<pages.Length;i++){pages[i].Visible=i==index;WhaleTheme.StyleButton(modeButtons[i],i==index);}
            pages[index].BringToFront();
        }
        internal void SelectPreviewPage(int index) { if(!preview)throw new InvalidOperationException("Preview only.");if(index<0||index>=pages.Length)throw new ArgumentOutOfRangeException("index");SelectPage(index); }
        private sealed class ResetHeader : Control {
            private readonly bool english;
            internal ResetHeader(bool useEnglish) { english=useEnglish;DoubleBuffered=true;BackColor=WhaleTheme.Surface; }
            protected override void OnPaint(PaintEventArgs e) {
                base.OnPaint(e);float scale=Height/58f;var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                WhaleTheme.DrawMascot(g,new RectangleF(0,0,54*scale,54*scale));
                using(var title=new Font("Microsoft YaHei UI",16*scale,FontStyle.Bold,GraphicsUnit.Pixel))
                using(var body=new Font("Microsoft YaHei UI",10.5f*scale,FontStyle.Regular,GraphicsUnit.Pixel))
                using(var ink=new SolidBrush(WhaleTheme.Ink))using(var muted=new SolidBrush(WhaleTheme.Muted)) {
                    g.DrawString(english?"Claude reset countdown":"Claude 重置倒计时",title,ink,new RectangleF(62*scale,0,Width-62*scale,24*scale));
                    g.DrawString(english?"Open Claude Settings → Usage, then read its reset times.\nPage estimates do not confirm restored quota.":"打开 Claude「设置 → 用量」，再读取重置时间。\n页面值仅供估算，不代表额度已经恢复。",body,muted,new RectangleF(62*scale,25*scale,Width-62*scale,31*scale));
                }
            }
        }
        private Button Button(string text){var button=new CandyButton{Text=text,Height=30,Width=104,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowOnly,FlatStyle=FlatStyle.Flat,Margin=new Padding(3)};WhaleTheme.StyleButton(button,false);return button;}
        internal static ResetSnapshot Copy(ResetSnapshot s){s=s??new ResetSnapshot();return new ResetSnapshot{SessionResetUtc=s.SessionResetUtc,WeeklyResetUtc=s.WeeklyResetUtc,ObservedUtc=s.ObservedUtc,Source=s.Source,Estimated=s.Estimated,SessionEvidence=s.SessionEvidence,WeeklyEvidence=s.WeeklyEvidence};}
        private DateTimePicker DateInput(string utc){var parsed=ResetCountdown.ParseUtc(utc);return new DateTimePicker{Dock=DockStyle.Fill,Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd HH:mm",Value=TimeZoneInfo.ConvertTime(parsed??DateTimeOffset.UtcNow,ClockTime.ResolveZone(DisplayZoneId)).DateTime,Margin=new Padding(0,3,0,3)};}
        private string ManualUtc(DateTime time){var zone=ClockTime.ResolveZone(DisplayZoneId);time=DateTime.SpecifyKind(time,DateTimeKind.Unspecified);if(zone.IsInvalidTime(time)||zone.IsAmbiguousTime(time))throw new FormatException();return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(time,zone)).ToString("o");}
        private void RefreshWindows(){if(busy)return;windows.Items.Clear();foreach(var window in ResetWindowReader.ListWindows())windows.Items.Add(window);foreach(ResetSourceWindow window in windows.Items)if(initialWindow!=null&&initialWindow.Handle==window.Handle&&initialWindow.ProcessId==window.ProcessId&&initialWindow.StartTicks==window.StartTicks)windows.SelectedItem=window;if(windows.SelectedIndex<0&&windows.Items.Count==1)windows.SelectedIndex=0;if(windows.Items.Count==0)SetStatus(T("没有找到 Claude 窗口，请先打开 Claude 的用量页。","No Claude window found. Open its Usage page first."),1);}
        private void StageText(){captureGeneration++;try{Snapshot=ResetCountdown.ParseUsageText(pasted.Text,DateTimeOffset.UtcNow,ClockTime.ResolveZone(DisplayZoneId));SourceWindow=null;auto.Checked=false;ShowResult();}catch(FormatException){ShowParseError();}}
        private void StageManualTimes(){captureGeneration++;try{var candidate=new ResetSnapshot{SessionResetUtc=sessionSet.Checked?ManualUtc(sessionDate.Value):null,WeeklyResetUtc=weeklySet.Checked?ManualUtc(weeklyDate.Value):null,ObservedUtc=DateTimeOffset.UtcNow.ToString("o"),Source="Manual",Estimated=false};Snapshot=ResetCountdown.Validate(candidate,DateTimeOffset.UtcNow);SourceWindow=null;auto.Checked=false;ShowResult();}catch(FormatException){ShowParseError();}}
        private async Task CaptureUsage(){
            if(busy||preview)return;var window=windows.SelectedItem as ResetSourceWindow;if(window==null){SetStatus(T("请先选择 Claude 的用量窗口。","Select the Claude Usage window first."),1);return;}
            busy=true;int generation=++captureGeneration;string zone=DisplayZoneId;read.Enabled=false;apply.Enabled=false;SetStatus(T("正在读取…","Reading…"),0);
            try{var capture=await ResetCaptureClient.ReadAsync(window);if(IsDisposed||generation!=captureGeneration)return;if(!capture.Ok){ShowCaptureError(capture.Status);return;}
                var updated=ResetCountdown.ParseUsageText(capture.Text,DateTimeOffset.UtcNow,ClockTime.ResolveZone(zone));
                Snapshot=SourceWindow!=null&&SourceWindow.Handle==window.Handle&&SourceWindow.ProcessId==window.ProcessId&&SourceWindow.StartTicks==window.StartTicks?ResetCountdown.MergeObserved(Snapshot,updated):updated;
                SourceWindow=window;auto.Checked=true;ShowResult();
            }catch(FormatException){if(!IsDisposed)ShowParseError();}finally{busy=false;if(!IsDisposed){read.Enabled=true;apply.Enabled=true;}}
        }
        private void ShowCaptureError(string status){
            string reason=status=="not_visible"?T("所选窗口已隐藏或最小化。","The selected window is hidden or minimized."):
                status=="wrong_window"||status=="window_closed"?T("所选窗口已关闭或发生变化，请刷新后重新选择。","The selected window closed or changed. Refresh and select it again."):
                status=="timeout"||status=="scan_limit"?T("读取超时或页面过长，请让用量卡片保持可见后重试。","Reading timed out or the page was too large. Keep the usage cards visible and retry."):
                status=="usage_not_found"?T("窗口可访问，但没有识别到可见的用量卡片。请打开「设置 → 用量」。","The window is accessible, but visible usage cards were not recognized. Open Settings → Usage."):
                T("未读取到窗口中的可见文字。","The window's visible text could not be read.");
            SetStatus(reason+"\n"+T("也可粘贴用量文字或手动设置；原倒计时未更改。","You can paste usage text or enter times manually; the previous countdown is unchanged."),2);
        }
        private void ShowParseError(){SetStatus(T("无法明确识别重置时间（格式或时区有歧义）。请手动设置；原倒计时未更改。","Reset time is unclear (format or timezone ambiguity). Set it manually; the previous countdown is unchanged."),1);}
        private void ShowResult(){if(result==null)return;var zone=ClockTime.ResolveZone(DisplayZoneId);SetStatus(T("本轮：","Session: ")+DateLabel(Snapshot.SessionResetUtc,zone)+"\n"+T("每周：","Weekly: ")+DateLabel(Snapshot.WeeklyResetUtc,zone)+"\n"+(Snapshot.Estimated?T("≈ 页面估算 · ","≈ Page estimate · "):T("指定时刻 · ","Specified time · "))+T("点击应用后显示","Apply to display"),0);}
        private void SetStatus(string text,int severity) {
            result.Text=text;
            statusCard.FillColor=severity==2?Color.FromArgb(255,236,241):severity==1?Color.FromArgb(255,247,221):Color.FromArgb(235,245,252);
            statusCard.BorderColor=severity==2?Color.FromArgb(244,204,213):severity==1?Color.FromArgb(240,219,168):Color.FromArgb(212,231,242);
            result.ForeColor=severity==2?Color.FromArgb(162,71,92):severity==1?Color.FromArgb(138,96,24):WhaleTheme.Ink;
            statusCard.Invalidate();
        }
        private string DateLabel(string value,TimeZoneInfo zone){var date=ResetCountdown.ParseUtc(value);return date.HasValue?TimeZoneInfo.ConvertTime(date.Value,zone).ToString("yyyy-MM-dd HH:mm zzz",CultureInfo.InvariantCulture):T("未读取","Not read");}
        internal void RenderPreview(string path){float scale;using(var graphics=CreateGraphics())scale=graphics.DpiX/96f;RenderPreview(path,scale);}
        internal void RenderPreview(string path,float previewScale){
            if(previewScale<0.5f||previewScale>3f)throw new ArgumentOutOfRangeException("previewScale");
            float? previousScale=WhaleTheme.RenderScaleOverride;WhaleTheme.RenderScaleOverride=previewScale;
            try {
            ShowInTaskbar=false;TopMost=false;Opacity=0;StartPosition=FormStartPosition.Manual;Location=new Point(-30000,-30000);
            Show();Application.DoEvents();
            float currentScale=ClientSize.Width/580f;
            AutoScaleMode=AutoScaleMode.None;MinimumSize=Size.Empty;
            float ratio=previewScale/currentScale;if(Math.Abs(ratio-1f)>0.001f)Scale(new SizeF(ratio,ratio));
            ClientSize=new Size((int)Math.Round(580*previewScale),(int)Math.Round(430*previewScale));
            RegionSettingsForm.NormalizePreviewFonts(this,previewScale);
            PerformLayout();Application.DoEvents();
            using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(path);}
            Hide();
            } finally { WhaleTheme.RenderScaleOverride=previousScale; }
        }
        protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing&&themedIcon!=null)themedIcon.Dispose();}
    }
}
