using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

[assembly: System.Reflection.AssemblyVersion("0.5.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.5.0.0")]

namespace TwoZoneClock {
    public sealed class Preferences {
        // Keep reading the old setting so upgrades preserve the single-column preference.
        public bool ShowSingapore = true;
        public bool ShouldSerializeShowSingapore() { return false; }
        public bool ShowSource { get { return ShowSingapore; } set { ShowSingapore = value; } }
        public string SourceTimeZoneId = "Singapore Standard Time";
        public string TargetTimeZoneId = "Eastern Standard Time";
        public bool English = false;
        public bool TwentyFourHours = false;
        public bool ShowSeconds = false;
        public bool AlwaysOnTop = true;
        public bool Locked = false;
        public bool HasPosition = false;
        public int X, Y;
        public bool ShowResetCountdown = true;
        public string ResetDisplayZoneId = "local";
        public ResetSnapshot Reset = new ResetSnapshot();
        public void Normalize() {
            if (!ClockTime.IsValidZone(SourceTimeZoneId)) SourceTimeZoneId = "Singapore Standard Time";
            if (!ClockTime.IsValidZone(TargetTimeZoneId)) TargetTimeZoneId = "Eastern Standard Time";
            if (!ClockTime.IsValidZone(ResetDisplayZoneId)) ResetDisplayZoneId = "local";
            try { Reset=ResetCountdown.Validate(Reset??new ResetSnapshot(),DateTimeOffset.UtcNow); }
            catch (FormatException) { Reset=new ResetSnapshot(); }
        }
    }

    public sealed class ClockReading {
        public DateTimeOffset Local;
        public string Time, Date, Zone;
    }

    public static class ClockTime {
        public static bool IsValidZone(string id) {
            if (id == "local") return true;
            if (String.IsNullOrWhiteSpace(id)) return false;
            try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
            catch (TimeZoneNotFoundException) { return false; }
            catch (InvalidTimeZoneException) { return false; }
        }
        public static TimeZoneInfo ResolveZone(string id) {
            if (id == "local" || String.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Local;
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
            catch (InvalidTimeZoneException) { return TimeZoneInfo.Local; }
        }
        public static ClockReading Read(DateTimeOffset instant, bool singapore, bool use24, bool seconds) {
            return Read(instant, singapore ? "Singapore Standard Time" : "Eastern Standard Time", use24, seconds);
        }
        public static ClockReading Read(DateTimeOffset instant, string zoneId, bool use24, bool seconds) {
            return Read(instant, zoneId, use24, seconds, false);
        }
        public static ClockReading Read(DateTimeOffset instant, string zoneId, bool use24, bool seconds, bool english) {
            var zone = ResolveZone(zoneId);
            var local = TimeZoneInfo.ConvertTime(instant, zone);
            string format = use24 ? (seconds ? "HH:mm:ss" : "HH:mm") : (seconds ? "h:mm:ss tt" : "h:mm tt");
            string[] days = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            return new ClockReading {
                Local = local, Time = local.ToString(format, CultureInfo.InvariantCulture),
                Date = english ? local.ToString("ddd, MMM d", CultureInfo.InvariantCulture) : local.ToString("M月d日", CultureInfo.InvariantCulture) + " " + days[(int)local.DayOfWeek],
                Zone = ZoneLabel(zone, instant, local.Offset)
            };
        }
        public static string OffsetLabel(TimeSpan offset) {
            return "UTC" + (offset < TimeSpan.Zero ? "−" : "+") + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }
        public static string DifferenceLabel(DateTimeOffset instant, string sourceZoneId, string targetZoneId, bool english) {
            var source = TimeZoneInfo.ConvertTime(instant, ResolveZone(sourceZoneId));
            var target = TimeZoneInfo.ConvertTime(instant, ResolveZone(targetZoneId));
            // Subtract the offsets, not the DateTimeOffsets: both readings represent the same instant.
            var difference = target.Offset - source.Offset;
            int minutes = (int)Math.Abs(difference.TotalMinutes);
            string duration = minutes / 60 > 0 ? (minutes / 60).ToString(CultureInfo.InvariantCulture) + (english ? "h" : "小时") : "";
            if (minutes % 60 > 0) duration += (english && duration.Length > 0 ? " " : "") + (minutes % 60).ToString(CultureInfo.InvariantCulture) + (english ? "m" : "分");
            string relative = minutes == 0 ? (english ? "Same time" : "同一时间")
                : english ? duration + (difference > TimeSpan.Zero ? " ahead" : " behind")
                : (difference > TimeSpan.Zero ? "快" : "慢") + duration;
            int days = (target.Date - source.Date).Days;
            string day;
            switch (days) {
                case -1: day = english ? "yesterday" : "昨天"; break;
                case 0: return relative;
                case 1: day = english ? "tomorrow" : "明天"; break;
                default: day = english ? Math.Abs(days).ToString(CultureInfo.InvariantCulture) + (days < 0 ? " days earlier" : " days later")
                    : (days < 0 ? "早" : "晚") + Math.Abs(days).ToString(CultureInfo.InvariantCulture) + "天"; break;
            }
            return relative + " · " + day;
        }
        private static string ZoneLabel(TimeZoneInfo zone, DateTimeOffset instant, TimeSpan offset) {
            bool daylight = zone.IsDaylightSavingTime(instant);
            switch (zone.Id) {
                case "Singapore Standard Time": return "SGT";
                case "China Standard Time": return "UTC+08:00";
                case "Eastern Standard Time": return daylight ? "EDT" : "EST";
                case "Central Standard Time": return daylight ? "CDT" : "CST";
                case "Mountain Standard Time": return daylight ? "MDT" : "MST";
                case "Pacific Standard Time": return daylight ? "PDT" : "PST";
                default: return OffsetLabel(offset);
            }
        }
    }

    // Presentation only: these durations describe the progress bars, never alter reset dates.
    internal static class ClockPresentation {
        internal static readonly TimeSpan SessionWindow = TimeSpan.FromHours(5);
        internal static readonly TimeSpan WeeklyWindow = TimeSpan.FromDays(7);
        internal static double ElapsedFraction(DateTimeOffset reset,DateTimeOffset instant,bool weekly) {
            double duration=(weekly?WeeklyWindow:SessionWindow).TotalSeconds;
            return Math.Max(0,Math.Min(1,1-(reset-instant).TotalSeconds/duration));
        }
        internal static string Remaining(DateTimeOffset reset,DateTimeOffset instant,bool english) {
            long minutes=(long)Math.Ceiling(Math.Max(0,(reset-instant).TotalMinutes));
            long days=minutes/1440,hours=minutes%1440/60,rest=minutes%60;
            if(days>0)return days.ToString(CultureInfo.InvariantCulture)+(english?"d ":"天")+hours.ToString(CultureInfo.InvariantCulture)+(english?"h":"小时");
            if(hours>0)return hours.ToString(CultureInfo.InvariantCulture)+(english?"h ":"小时")+rest.ToString(CultureInfo.InvariantCulture)+(english?"m":"分");
            return minutes.ToString(CultureInfo.InvariantCulture)+(english?"m":"分");
        }
        internal static string LastRead(DateTimeOffset observed,bool english) {
            return (english?"Read ":"上次 ")+observed.ToString(english?"MMM d HH:mm":"M月d日 HH:mm",CultureInfo.InvariantCulture);
        }
    }

    internal sealed class ClockForm : Form {
        private readonly Preferences prefs;
        private readonly System.Windows.Forms.Timer timer;
        private readonly EventWaitHandle showSignal;
        private readonly bool preview;
        private readonly NotifyIcon tray;
        private readonly Icon clockIcon;
        private float scale = 1;
        private DayPhase? previewPhase;
        private bool dragging;
        private Point dragCursor, dragPosition;
        private string lastStamp = "";
        private DateTimeOffset now;
        private ContextMenuStrip menu;
        private ToolStripMenuItem sourceItem, formatItem, secondsItem, topItem, lockItem;
        private ResetSourceWindow resetWindow;
        private bool autoReadReset,readingReset,resetReadFailed;
        private bool configuringReset;
        private int resetGeneration;
        private DateTimeOffset nextResetRead=DateTimeOffset.MaxValue;
        private readonly ToolTip resetTip=new ToolTip();
        private static readonly Color Surface = WhaleTheme.Surface;
        private static readonly Color Border = WhaleTheme.Border;
        private static readonly Color Ink = WhaleTheme.Ink;
        private static readonly Color Muted = WhaleTheme.Muted;
        private static string SettingsPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TwoZoneClock", "settings.xml"); } }

        public ClockForm(bool isPreview, EventWaitHandle signal) {
            preview = isPreview; showSignal = signal;
            prefs = preview ? new Preferences() : LoadSettings();
            prefs.Normalize();
            Text = T("鲸娘两地时钟 · 原本地区 / 目标地区", "Whalegirl · Two-Zone Clock");
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None;
            BackColor = Surface; ForeColor = Ink; TopMost = prefs.AlwaysOnTop;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            using (var g = CreateGraphics()) scale = g.DpiX / 96f;
            clockIcon = CreateClockIcon(); Icon = clockIcon;
            menu = BuildMenu(); ContextMenuStrip = menu;
            ResizeClock();
            if (prefs.HasPosition) Location = KeepVisible(new Point(prefs.X, prefs.Y)); else SnapToCorner();
            now = DateTimeOffset.UtcNow;
            timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += delegate {
                if (showSignal != null && showSignal.WaitOne(0)) Reveal();
                now = DateTimeOffset.UtcNow;
                string stamp = now.ToString(prefs.ShowSeconds||prefs.ShowResetCountdown ? "yyyyMMddHHmmss" : "yyyyMMddHHmm", CultureInfo.InvariantCulture);
                if (stamp != lastStamp) { lastStamp = stamp; Invalidate(); }
                if(autoReadReset&&!readingReset&&!configuringReset&&now>=nextResetRead)ReadResetAutomatically();
            };
            if (!preview) {
                tray = new NotifyIcon { Icon = clockIcon, Text = T("两地时钟 · 双击显示，右键设置", "Two-Zone Clock · Right-click for settings"), Visible = true, ContextMenuStrip = menu };
                tray.DoubleClick += delegate { Reveal(); };
                timer.Start();
            }
            MouseDown += delegate(object sender, MouseEventArgs e) { if(e.Button==MouseButtons.Left&&prefs.ShowResetCountdown&&e.Y/scale>=ResetCardTop){ConfigureReset();return;} if (e.Button == MouseButtons.Left && !prefs.Locked) { dragging = true; dragCursor = Cursor.Position; dragPosition = Location; Capture = true; } };
            MouseMove += delegate { if (dragging) { var cursor = Cursor.Position; Location = new Point(dragPosition.X + cursor.X - dragCursor.X, dragPosition.Y + cursor.Y - dragCursor.Y); } };
            MouseUp += delegate { if (dragging) { dragging = false; Capture = false; Location = KeepVisible(Location); SaveSettings(); } };
            MouseCaptureChanged += delegate { if (dragging && !Capture) { dragging = false; Location = KeepVisible(Location); SaveSettings(); } };
            DoubleClick += delegate { if (!prefs.Locked) { SnapToCorner(); SaveSettings(); } };
            FormClosing += delegate { autoReadReset=false; if (!preview) SaveSettings(); timer.Stop(); if (tray != null) tray.Visible = false; };
            UpdateResetTip();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
            Microsoft.Win32.SystemEvents.TimeChanged += TimeChanged;
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        private bool visualReview;
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; if(!visualReview)cp.ExStyle |= 0x80; return cp; } }
        private string T(string zh, string en) { return prefs.English ? en : zh; }
        private int PanelWidth { get { return prefs.ShowSeconds ? 192 : 168; } }
        private const int HeaderHeight = 44;
        private const int CompactRowHeight = 32;
        private const int ResetHeight = 72;
        // Single-region/no-countdown mode retains its previous 104 px maximum height.
        private int PrimaryRowHeight { get { return !prefs.ShowSource&&!prefs.ShowResetCountdown?50:62; } }
        private int BaseHeight { get { return HeaderHeight+4+PrimaryRowHeight+(prefs.ShowSource?4+CompactRowHeight+10:6); } }
        private int ResetCardTop { get { return BaseHeight-2; } }
        private void ResizeClock() {
            int oldRight = Right;
            int oldBottom = Bottom;
            ClientSize = new Size((int)Math.Round(PanelWidth * scale), (int)Math.Round((BaseHeight+(prefs.ShowResetCountdown?ResetHeight:0)) * scale));
            ApplyWindowCorners();
            if (IsHandleCreated && oldRight > 0) { Left = oldRight - Width; Top = oldBottom - Height; }
            Invalidate();
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e);ApplyWindowCorners(); }
        private void ApplyWindowCorners() {
            if(IsHandleCreated&&WhaleTheme.RoundWindow(Handle,false)) { var previous=Region;Region=null;if(previous!=null)previous.Dispose();return; }
            using(var path=Rounded(new RectangleF(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1)),16*scale)) { var previous=Region;Region=new Region(path);if(previous!=null)previous.Dispose(); }
        }
        private static GraphicsPath Rounded(RectangleF r, float radius) {
            var p = new GraphicsPath(); float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right-d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right-d, r.Bottom-d, d, d, 0, 90); p.AddArc(r.X, r.Bottom-d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        protected override void OnPaint(PaintEventArgs e) { DrawFace(e.Graphics, now); }
        private void DrawFace(Graphics g, DateTimeOffset instant) {
            g.Clear(Surface); g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.ScaleTransform(scale, scale);
            float w = ClientSize.Width / scale, h = ClientSize.Height / scale;
            using (var p = Rounded(new RectangleF(0.5f, 0.5f, w-1.5f, h-1.5f), 14))
            using (var pen = new Pen(Border, 1)) g.DrawPath(pen, p);
            WhaleTheme.DrawMascot(g, new RectangleF(5, 0, 48, 48));
            using (var titleFont = new Font("Microsoft YaHei UI", prefs.English ? 10.5f : 11.5f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var detailFont = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var fg = new SolidBrush(Ink)) using (var muted = new SolidBrush(Muted))
            using (var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter }) {
                g.DrawString(T("鲸娘两地时钟", "Whalegirl Clock"), titleFont, fg, new RectangleF(57, 7, w-64, 18), fmt);
                g.DrawString(T("陪你看昼夜", "Two places, one day"), detailFont, muted, new RectangleF(57, 25, w-64, 14), fmt);
            }
            if(prefs.ShowSource)DrawRow(g,HeaderHeight+4,T("原本","From")+" · "+RegionChoice.ShortName(prefs.SourceTimeZoneId,prefs.English),ClockTime.Read(instant,prefs.SourceTimeZoneId,prefs.TwentyFourHours,prefs.ShowSeconds,prefs.English),true,instant);
            DrawRow(g,HeaderHeight+4+(prefs.ShowSource?PrimaryRowHeight+4:0),T("目标","To")+" · "+RegionChoice.ShortName(prefs.TargetTimeZoneId,prefs.English),ClockTime.Read(instant,prefs.TargetTimeZoneId,prefs.TwentyFourHours,prefs.ShowSeconds,prefs.English),false,instant);
            if(prefs.ShowResetCountdown)DrawReset(g,instant);
        }
        private void DrawReset(Graphics g,DateTimeOffset instant) {
            int y=ResetCardTop;
            var card=new RectangleF(5,y,PanelWidth-11,ResetHeight-4);
            WhaleTheme.DrawGradientCard(g,card,Color.FromArgb(255,242,245),Color.FromArgb(255,230,236),Color.FromArgb(244,204,213),11);
            Color titleColor=Color.FromArgb(162,71,92),soft=Color.FromArgb(173,123,140);
            var session=ResetCountdown.ParseUtc(prefs.Reset.SessionResetUtc);var weekly=ResetCountdown.ParseUtc(prefs.Reset.WeeklyResetUtc);
            var observed=ResetCountdown.ParseUtc(prefs.Reset.ObservedUtc);
            string caption=T("Claude · 重置倒计时","Claude · reset countdown");
            float titleWidth=observed.HasValue?PanelWidth-89:PanelWidth-24;
            DrawFit(g,caption,new RectangleF(12,y+2,titleWidth,14),9,FontStyle.Bold,titleColor,StringAlignment.Near);
            if(observed.HasValue)DrawFit(g,ClockPresentation.LastRead(observed.Value.ToLocalTime(),prefs.English),new RectangleF(PanelWidth-76,y+3,64,12),7.2f,FontStyle.Regular,soft,StringAlignment.Far);
            if(!session.HasValue&&!weekly.HasValue) {
                DrawFit(g,resetReadFailed?T("读取失败 · 点击设置","Read failed · settings"):T("还没读取重置时间","Reset times not read yet"),new RectangleF(12,y+21,PanelWidth-24,14),9,FontStyle.Regular,soft,StringAlignment.Near);
                var button=new RectangleF(12,y+44,prefs.English?107:75,18);
                using(var path=WhaleTheme.Rounded(button,9))using(var brush=new SolidBrush(Color.FromArgb(255,124,154)))g.FillPath(brush,path);
                DrawFit(g,T("读取用量页","Read Usage page"),button,9,FontStyle.Bold,Color.White,StringAlignment.Center);
                return;
            }
            DateTimeOffset?[] dates={session,weekly};
            for(int i=0;i<2;i++) {
                int row=y+17+i*21;
                DrawFit(g,i==0?T("本轮","Session"):T("每周","Weekly"),new RectangleF(12,row, prefs.English?34:23,15),8.5f,FontStyle.Regular,soft,StringAlignment.Near);
                var date=dates[i];
                string value=!date.HasValue?T("未读取","Not read"):date.Value<=instant?T("到期 · 待刷新","Due · refresh"):ClockPresentation.Remaining(date.Value,instant,prefs.English);
                if(date.HasValue&&date.Value>instant&&prefs.Reset.Estimated)value="≈"+value;
                float left=prefs.English?47:36;
                float resetWidth=date.HasValue&&date.Value>instant?(prefs.English?47:42):0;
                DrawFit(g,value,new RectangleF(left,row,PanelWidth-12-left-resetWidth,15),11.5f,FontStyle.Bold,Ink,StringAlignment.Near);
                if(resetWidth>0) {
                    var local=TimeZoneInfo.ConvertTime(date.Value,ClockTime.ResolveZone(prefs.ResetDisplayZoneId));
                    string resetText=local.ToString("HH:mm",CultureInfo.InvariantCulture)+T(" 重置"," reset");
                    DrawFit(g,resetText,new RectangleF(PanelWidth-12-resetWidth,row+1,resetWidth,13),7.4f,FontStyle.Regular,soft,StringAlignment.Far);
                }
                var track=new RectangleF(12,row+16,PanelWidth-24,4);
                using(var path=WhaleTheme.Rounded(track,2))using(var background=new SolidBrush(Color.FromArgb(247,214,222)))g.FillPath(background,path);
                if(date.HasValue) {
                    float filled=(float)ClockPresentation.ElapsedFraction(date.Value,instant,i==1)*track.Width;
                    if(filled>0) {
                        var state=g.Save();using(var clip=WhaleTheme.Rounded(track,2))g.SetClip(clip);
                        using(var fill=new LinearGradientBrush(track,Color.FromArgb(255,170,190),Color.FromArgb(255,112,146),LinearGradientMode.Horizontal)) {
                            if(filled<4)g.FillEllipse(fill,track.X,track.Y,filled,4);
                            else using(var path=WhaleTheme.Rounded(new RectangleF(track.X,track.Y,filled,4),2))g.FillPath(fill,path);
                        }g.Restore(state);
                    }
                }
            }
            string note=resetReadFailed?T("读取失败 · 点击设置","Read failed · settings"):prefs.Reset.Source=="Manual"?T("手动时间 · 点击设置","Manual · settings"):"";
            if(note.Length>0)DrawFit(g,note,new RectangleF(12,y+59,PanelWidth-24,8),7,FontStyle.Regular,titleColor,StringAlignment.Near);
        }
        private void UpdateResetTip() {
            var observed=ResetCountdown.ParseUtc(prefs.Reset.ObservedUtc);
            resetTip.SetToolTip(this,T("点击 Claude 倒计时设置。到期不代表额度已恢复。\n","Click the Claude countdown to configure. Due does not mean quota is restored.\n")+
                (observed.HasValue?T("上次读取：","Last observed: ")+observed.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"):T("尚未读取重置时间。","No reset time has been read.")));
        }
        internal void ConfigureReset() {
            if(configuringReset)return;configuringReset=true;resetGeneration++;
            try{using(var dialog=new ResetSettingsForm(prefs.Reset,prefs.ShowResetCountdown,prefs.English,prefs.ResetDisplayZoneId,resetWindow,autoReadReset,preview)) {
                dialog.TopMost=TopMost;if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                prefs.Reset=ResetSettingsForm.Copy(dialog.Snapshot);prefs.ShowResetCountdown=dialog.CountdownEnabled;prefs.ResetDisplayZoneId=dialog.DisplayZoneId;
                resetWindow=dialog.SourceWindow;autoReadReset=dialog.AutoRead&&!preview;resetReadFailed=false;nextResetRead=DateTimeOffset.UtcNow.AddSeconds(60);
                ResizeClock();Location=KeepVisible(Location);UpdateResetTip();SaveSettings();
            }}finally{configuringReset=false;}
        }
        private async void ReadResetAutomatically() {
            if(preview||readingReset||!autoReadReset||resetWindow==null)return;
            readingReset=true;var capturedWindow=resetWindow;int generation=resetGeneration;nextResetRead=DateTimeOffset.UtcNow.AddSeconds(60);
            try {
                var capture=await ResetCaptureClient.ReadAsync(capturedWindow);
                if(IsDisposed||!autoReadReset||capturedWindow!=resetWindow||generation!=resetGeneration)return;
                if(!capture.Ok){resetReadFailed=true;return;}
                var updated=ResetCountdown.ParseUsageText(capture.Text,DateTimeOffset.UtcNow,ClockTime.ResolveZone(prefs.ResetDisplayZoneId));
                prefs.Reset=ResetCountdown.MergeObserved(prefs.Reset,updated);resetReadFailed=false;SaveSettings();
            } catch(FormatException){resetReadFailed=true;}
            finally{readingReset=false;if(!IsDisposed){UpdateResetTip();Invalidate();}}
        }
        private void DrawRow(Graphics g,int y,string title,ClockReading time,bool source,DateTimeOffset instant) {
            var palette=previewPhase.HasValue?WhaleTheme.ThemeForPhase(previewPhase.Value):WhaleTheme.ThemeForHour(time.Local.Hour);
            bool secondary=!source&&prefs.ShowSource;
            int rowHeight=secondary?CompactRowHeight:PrimaryRowHeight;
            var card=new RectangleF(5,y,PanelWidth-11,rowHeight);
            WhaleTheme.DrawGradientCard(g,card,palette,11);
            if(secondary) {
                float timeWidth=prefs.ShowSeconds?84:64;
                var icon=new RectangleF(10,y+8,15,15);WhaleTheme.DrawDayIcon(g,icon,palette,card);
                DrawFit(g,title,new RectangleF(28,y+2,PanelWidth-timeWidth-40,14),9,FontStyle.Bold,palette.Accent,StringAlignment.Near);
                DrawFit(g,time.Date,new RectangleF(28,y+17,PanelWidth-timeWidth-40,11),8,FontStyle.Regular,Muted,StringAlignment.Near);
                DrawStableTime(g,time.Time,new RectangleF(PanelWidth-timeWidth-11,y+4,timeWidth,24),16,true);
            } else {
                WhaleTheme.DrawDayIcon(g,new RectangleF(PanelWidth-30,y+4,18,18),palette,card);
                DrawFit(g,title,new RectangleF(12,y+3,PanelWidth-47,14),10,FontStyle.Bold,palette.Accent,StringAlignment.Near);
                bool compact=rowHeight<62;
                DrawStableTime(g,time.Time,new RectangleF(11,y+(compact?12:16),PanelWidth-23,compact?26:33),compact?23:27,false);
                string date=time.Date+" · "+(source?T("目标","Target: "):"")+ClockTime.DifferenceLabel(instant,prefs.SourceTimeZoneId,prefs.TargetTimeZoneId,prefs.English);
                DrawFit(g,date,new RectangleF(12,y+rowHeight-14,PanelWidth-25,12),8.5f,FontStyle.Regular,Muted,StringAlignment.Near);
            }
        }
        private static void DrawFit(Graphics g,string text,RectangleF bounds,float pixels,FontStyle style,Color color,StringAlignment alignment) {
            using(var original=new Font("Microsoft YaHei UI",pixels,style,GraphicsUnit.Pixel))
            using(var format=new StringFormat(StringFormat.GenericTypographic){Alignment=alignment,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap})
            using(var brush=new SolidBrush(color)) {
                float measured=g.MeasureString(text,original,Int32.MaxValue,format).Width;
                float size=Math.Min(pixels,pixels*Math.Max(1,bounds.Width)/Math.Max(1,measured));
                using(var fitted=new Font(original.FontFamily,size,style,GraphicsUnit.Pixel))g.DrawString(text,fitted,brush,bounds,format);
            }
        }
        // Use fixed glyph slots, including a reserved first hour digit and AM/PM slot.
        // No current digit or second can affect the starting X coordinate or font size.
        internal static float DigitBlockWidth(Graphics graphics,Font font,bool seconds,bool ampm) {
            using(var format=new StringFormat(StringFormat.GenericTypographic)) {
                float digit=graphics.MeasureString("8",font,Int32.MaxValue,format).Width;
                float colon=graphics.MeasureString(":",font,Int32.MaxValue,format).Width;
                return digit*(seconds?6:4)+colon*(seconds?2:1)+(ampm?font.Size*1.48f:0);
            }
        }
        private void DrawStableTime(Graphics g,string value,RectangleF bounds,float pixels,bool right) {
            bool ampm=!prefs.TwentyFourHours;
            using(var probe=WhaleTheme.NewDigitFont(pixels)) {
                float measured=DigitBlockWidth(g,probe,prefs.ShowSeconds,ampm);
                float size=pixels*Math.Min(1,bounds.Width/Math.Max(1,measured));
                using(var font=WhaleTheme.NewDigitFont(size))
                using(var brush=new SolidBrush(Ink))
                using(var format=new StringFormat(StringFormat.GenericTypographic){LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap}) {
                    float digit=g.MeasureString("8",font,Int32.MaxValue,format).Width;
                    float colon=g.MeasureString(":",font,Int32.MaxValue,format).Width;
                    float block=DigitBlockWidth(g,font,prefs.ShowSeconds,ampm);
                    float x=right?bounds.Right-block:bounds.X;
                    string numeric=ampm?value.Substring(0,value.LastIndexOf(' ')):value;
                    if(numeric.IndexOf(':')==1)numeric=" "+numeric;
                    foreach(char character in numeric) {
                        float width=character==':'?colon:digit;
                        if(character!=' ')g.DrawString(character.ToString(),font,brush,new RectangleF(x,bounds.Y,width+1,bounds.Height),format);
                        x+=width;
                    }
                    if(ampm)using(var suffix=new Font("Segoe UI",size*.44f,FontStyle.Regular,GraphicsUnit.Pixel))g.DrawString(value.Substring(value.LastIndexOf(' ')+1),suffix,brush,new RectangleF(x+3,bounds.Y+size*.3f,size*1.4f,bounds.Height-size*.3f),format);
                }
            }
        }
        private ContextMenuStrip BuildMenu() {
            var result = new ContextMenuStrip();
            WhaleTheme.StyleMenu(result);
            var regions = new ToolStripMenuItem(T("设置原本地区和目标地区…", "Choose original and target regions…"));
            regions.Click += delegate { ConfigureRegions(); };
            var swap = new ToolStripMenuItem(T("交换两个地区", "Swap regions"));
            swap.Click += delegate { string previous = prefs.SourceTimeZoneId; prefs.SourceTimeZoneId = prefs.TargetTimeZoneId; prefs.TargetTimeZoneId = previous; Invalidate(); SaveSettings(); };
            sourceItem = new ToolStripMenuItem(T("同时显示原本地区", "Show original region too")) { Checked = prefs.ShowSource, CheckOnClick = true };
            sourceItem.Click += delegate { prefs.ShowSource = sourceItem.Checked; ResizeClock(); Location = KeepVisible(Location); SaveSettings(); };
            formatItem = new ToolStripMenuItem(T("24 小时制", "24-hour time")) { Checked = prefs.TwentyFourHours, CheckOnClick = true };
            formatItem.Click += delegate { prefs.TwentyFourHours = formatItem.Checked; Invalidate(); SaveSettings(); };
            secondsItem = new ToolStripMenuItem(T("显示秒数", "Show seconds")) { Checked = prefs.ShowSeconds, CheckOnClick = true };
            secondsItem.Click += delegate { prefs.ShowSeconds = secondsItem.Checked; ResizeClock(); Location = KeepVisible(Location); SaveSettings(); };
            topItem = new ToolStripMenuItem(T("保持置顶", "Always on top")) { Checked = prefs.AlwaysOnTop, CheckOnClick = true };
            topItem.Click += delegate { prefs.AlwaysOnTop = topItem.Checked; TopMost = prefs.AlwaysOnTop; SaveSettings(); };
            lockItem = new ToolStripMenuItem(T("锁定位置", "Lock position")) { Checked = prefs.Locked, CheckOnClick = true };
            lockItem.Click += delegate { prefs.Locked = lockItem.Checked; SaveSettings(); };
            var reset = new ToolStripMenuItem(T("放回屏幕右下角", "Move to bottom right")); reset.Click += delegate { SnapToCorner(); Show(); SaveSettings(); };
            var show = new ToolStripMenuItem(T("显示时钟", "Show clock")); show.Click += delegate { Reveal(); };
            var hide = new ToolStripMenuItem(T("暂时隐藏", "Hide clock")); hide.Click += delegate { Hide(); };
            var exit = new ToolStripMenuItem(T("退出", "Exit")); exit.Click += delegate { Close(); };
            var update = new ToolStripMenuItem(T("检查更新…", "Check for updates…"));
            update.Click += delegate { WhaleCommon.UpdateChecker.Show(this, "clock", "0.5.0", prefs.English); };
            var countdown=new ToolStripMenuItem(T("Claude 重置倒计时…","Claude reset countdown…"));countdown.Click+=delegate{ConfigureReset();};
            var language = new ToolStripMenuItem("中文 / English");
            var chinese = new ToolStripMenuItem("中文") { Checked = !prefs.English };
            var english = new ToolStripMenuItem("English") { Checked = prefs.English };
            chinese.Click += delegate { SetLanguage(false); };
            english.Click += delegate { SetLanguage(true); };
            language.DropDownItems.AddRange(new ToolStripItem[] { chinese, english });
            result.Items.AddRange(new ToolStripItem[] { regions, swap, countdown, language, new ToolStripSeparator(), show, hide, sourceItem, formatItem, secondsItem, topItem, lockItem, new ToolStripSeparator(), reset, update, exit });
            return result;
        }
        internal void ConfigureRegions() {
            using (var dialog = new RegionSettingsForm(prefs.SourceTimeZoneId, prefs.TargetTimeZoneId, prefs.TwentyFourHours, prefs.English)) {
                dialog.TopMost = TopMost;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                prefs.SourceTimeZoneId = dialog.SourceTimeZoneId;
                prefs.TargetTimeZoneId = dialog.TargetTimeZoneId;
                SetLanguage(dialog.English);
                now = DateTimeOffset.UtcNow;
                Invalidate(); SaveSettings();
            }
        }
        private void SetLanguage(bool english) {
            prefs.English = english;
            Text = T("鲸娘两地时钟 · 原本地区 / 目标地区", "Whalegirl · Two-Zone Clock");
            var oldMenu = menu;
            menu = BuildMenu(); ContextMenuStrip = menu;
            if (tray != null) {
                tray.ContextMenuStrip = menu;
                tray.Text = T("两地时钟 · 双击显示，右键设置", "Two-Zone Clock · Right-click for settings");
            }
            if (oldMenu != null) oldMenu.Dispose();
            UpdateResetTip(); Invalidate(); SaveSettings();
        }
        private void Reveal() { Location = KeepVisible(Location); Show(); Invalidate(); }
        private void SnapToCorner() {
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            int gap = (int)Math.Round(10*scale);
            Location = new Point(work.Right-Width-gap, work.Bottom-Height-gap);
        }
        private Point KeepVisible(Point proposed) {
            var bounds = new Rectangle(proposed, Size); Rectangle work = Screen.FromRectangle(bounds).WorkingArea;
            return new Point(Math.Max(work.Left, Math.Min(proposed.X, work.Right-Width)), Math.Max(work.Top, Math.Min(proposed.Y, work.Bottom-Height)));
        }
        private void DisplayChanged(object sender, EventArgs e) {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(new Action(delegate { Location = KeepVisible(Location); Invalidate(); })); } catch (InvalidOperationException) { }
        }
        private void TimeChanged(object sender, EventArgs e) {
            TimeZoneInfo.ClearCachedData();
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(new Action(delegate { now = DateTimeOffset.UtcNow; lastStamp = ""; Invalidate(); })); }
            catch (InvalidOperationException) { }
        }
        private static Preferences LoadSettings() {
            try { using (var f = File.OpenRead(SettingsPath)) return (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(f); } catch { return new Preferences(); }
        }
        private void SaveSettings() {
            if (preview) return;
            prefs.X = Left; prefs.Y = Top; prefs.HasPosition = true;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                string temp = SettingsPath + ".tmp";
                using (var f = File.Create(temp)) new XmlSerializer(typeof(Preferences)).Serialize(f, prefs);
                if (File.Exists(SettingsPath)) File.Replace(temp, SettingsPath, null); else File.Move(temp, SettingsPath);
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        private static Icon CreateClockIcon() {
            return WhaleTheme.CreateIcon();
        }
        internal void RenderPreview(string path) {
            RenderPreview(path,1,DateTimeOffset.UtcNow);
        }
        internal void RenderPreview(string path,float previewScale,DateTimeOffset instant) {
            if(!preview||previewScale<1||previewScale>1.5f)throw new ArgumentOutOfRangeException("previewScale");
            scale=previewScale;now=instant;ResizeClock();
            using(var bmp=new Bitmap(ClientSize.Width,ClientSize.Height))using(var g=Graphics.FromImage(bmp)) { DrawFace(g,instant);bmp.Save(path,System.Drawing.Imaging.ImageFormat.Png); }
        }
        internal void RenderThemePreview(string path,DayPhase phase,float previewScale) {
            previewPhase=phase;var instant=DateTimeOffset.Parse("2026-11-01T10:08:00Z",CultureInfo.InvariantCulture);
            prefs.TwentyFourHours=true;RenderPreview(path,previewScale,instant);
        }
        internal void StartVisualReview() { if(!preview)throw new InvalidOperationException();visualReview=true;ShowInTaskbar=true;RecreateHandle();timer.Start(); }
        internal void RenderPreview(string path, bool english, bool seconds) {
            prefs.English = english; prefs.ShowSeconds = seconds;
            RenderPreview(path);
        }
        internal void RenderResetPreview(string path,bool english) {
            prefs.English=english;var instant=DateTimeOffset.UtcNow;
            prefs.Reset=new ResetSnapshot{SessionResetUtc=instant.AddHours(2).AddMinutes(15).ToString("o"),WeeklyResetUtc=instant.AddDays(3).AddHours(4).ToString("o"),ObservedUtc=instant.ToString("o"),Source="Usage text",Estimated=true};
            prefs.ShowResetCountdown=true;RenderPreview(path);
        }
        protected override void Dispose(bool disposing) {
            if (disposing) {
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
                Microsoft.Win32.SystemEvents.TimeChanged -= TimeChanged;
                autoReadReset=false;resetTip.Dispose();if (timer != null) timer.Dispose(); if (tray != null) tray.Dispose(); if (menu != null) menu.Dispose(); if (clockIcon != null) clockIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal static class Program {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [DllImport("shell32.dll", CharSet=CharSet.Unicode)] private static extern int SetCurrentProcessExplicitAppUserModelID(string id);
        [STAThread] private static void Main(string[] args) {
            if(ResetWindowReader.Run(args))return;
            SetProcessDPIAware(); SetCurrentProcessExplicitAppUserModelID("TwoZoneClock.Desktop");
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length==1&&args[0]=="--visual-check") { using(var f=new ClockForm(true,null)){f.StartVisualReview();Application.Run(f);}return; }
            if((args.Length==3||args.Length==5)&&args[0]=="--preview-theme") {
                DayPhase phase;float dpiScale=1;
                if(!Enum.TryParse<DayPhase>(args[1],true,out phase)||!Enum.IsDefined(typeof(DayPhase),phase))throw new ArgumentException("Theme must be night, dawn, day or dusk.");
                if(args.Length==5&&(args[3]!="--scale"||!Single.TryParse(args[4],NumberStyles.Float,CultureInfo.InvariantCulture,out dpiScale)))throw new ArgumentException("Use --scale 1, 1.25 or 1.5.");
                using(var f=new ClockForm(true,null))f.RenderThemePreview(Path.GetFullPath(args[2]),phase,dpiScale);return;
            }
            if(args.Length==2&&(args[0]=="--preview-reset"||args[0]=="--preview-reset-en")){using(var f=new ClockForm(true,null))f.RenderResetPreview(Path.GetFullPath(args[1]),args[0].EndsWith("-en"));return;}
            if(args.Length==2&&(args[0]=="--preview-reset-settings"||args[0]=="--preview-reset-settings-en")){using(var f=new ResetSettingsForm(new ResetSnapshot(),true,args[0].EndsWith("-en"),"local",null,false,true))f.RenderPreview(Path.GetFullPath(args[1]));return;}
            if (args.Length == 2 && args[0] == "--preview") { using (var f = new ClockForm(true,null)) f.RenderPreview(Path.GetFullPath(args[1])); return; }
            if (args.Length == 2 && (args[0] == "--preview-en" || args[0] == "--preview-seconds")) {
                using (var f = new ClockForm(true,null)) f.RenderPreview(Path.GetFullPath(args[1]), true, args[0] == "--preview-seconds"); return;
            }
            if (args.Length == 2 && args[0] == "--preview-settings") {
                using (var f = new RegionSettingsForm("Singapore Standard Time", "Eastern Standard Time", true)) f.RenderPreview(Path.GetFullPath(args[1])); return;
            }
            if (args.Length == 2 && args[0] == "--preview-settings-en") {
                using (var f = new RegionSettingsForm("Singapore Standard Time", "Eastern Standard Time", true, true)) f.RenderPreview(Path.GetFullPath(args[1])); return;
            }
            string suffix = Environment.UserName;
            bool created;
            using (var mutex = new Mutex(true,"Local\\TwoZoneClock_"+suffix,out created))
            using (var signal = new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\TwoZoneClock_Show_"+suffix)) {
                if (!created) { signal.Set(); return; }
                var clock = new ClockForm(false,signal);
                if (args.Length == 1 && args[0] == "--settings") clock.Shown += delegate { clock.BeginInvoke(new Action(clock.ConfigureRegions)); };
                Application.Run(clock);
            }
        }
    }
}
