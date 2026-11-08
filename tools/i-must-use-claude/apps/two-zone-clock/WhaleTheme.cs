using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TwoZoneClock {
    internal enum DayPhase { Night, Dawn, Day, Dusk }
    internal sealed class RegionPalette {
        internal readonly DayPhase Phase;
        internal readonly Color Top, Bottom, Border, Accent;
        internal RegionPalette(DayPhase phase,Color top,Color bottom,Color border,Color accent){Phase=phase;Top=top;Bottom=bottom;Border=border;Accent=accent;}
    }
    internal static class WhaleTheme {
        internal static readonly Color Surface=Color.FromArgb(251,249,244),Border=Color.FromArgb(231,228,242),Ink=Color.FromArgb(46,50,88),Muted=Color.FromArgb(138,143,181);
        internal static readonly Color Blue=Color.FromArgb(76,151,196),BlueEdge=Color.FromArgb(52,118,160),ButtonBorder=Color.FromArgb(212,231,242),Pink=Color.FromArgb(255,124,154);
        internal static readonly Color ResetTop=Color.FromArgb(255,242,245),ResetBottom=Color.FromArgb(255,230,236),ResetBorder=Color.FromArgb(244,204,213),ResetInk=Color.FromArgb(162,71,92);
        internal static readonly Color ProgressTrack=Color.FromArgb(247,214,222),ProgressTop=Color.FromArgb(255,170,190),ProgressBottom=Color.FromArgb(255,112,146);
        // Every local-hour boundary, including wraparound night, lives here.
        internal const int DawnStarts=5,DayStarts=8,DuskStarts=17,NightStarts=19;
        private static readonly RegionPalette[] Palettes={
            new RegionPalette(DayPhase.Night,Color.FromArgb(229,228,255),Color.FromArgb(207,203,250),Color.FromArgb(195,190,243),Color.FromArgb(83,86,168)),
            new RegionPalette(DayPhase.Dawn,Color.FromArgb(255,240,232),Color.FromArgb(255,219,203),Color.FromArgb(245,200,182),Color.FromArgb(160,84,58)),
            new RegionPalette(DayPhase.Day,Color.FromArgb(255,248,222),Color.FromArgb(255,231,176),Color.FromArgb(242,215,154),Color.FromArgb(138,96,24)),
            new RegionPalette(DayPhase.Dusk,Color.FromArgb(246,229,248),Color.FromArgb(255,214,200),Color.FromArgb(232,198,226),Color.FromArgb(128,78,140))
        };
        internal static readonly Color Day=Palettes[2].Top,Night=Palettes[0].Top;
        private static readonly Image Mascot=LoadMascot();
        private static readonly string DigitFamily=FindDigitFamily();
        // DrawToBitmap keeps a 96-DPI Graphics even when preview controls are laid
        // out at another DPI. Only isolated preview callers set this, then restore it.
        internal static float? RenderScaleOverride;
        internal static float DrawingScale(Graphics graphics){return RenderScaleOverride??(graphics.DpiX/96f);}
        private static readonly ConditionalWeakTable<Button,PlainButtonStyle> PlainStyles=new ConditionalWeakTable<Button,PlainButtonStyle>();
        private static readonly ConditionalWeakTable<ToolStripDropDown,object> StyledMenus=new ConditionalWeakTable<ToolStripDropDown,object>();
        internal static RegionPalette ThemeForHour(int hour){if(hour<0||hour>23)throw new ArgumentOutOfRangeException("hour");return ThemeForPhase(hour<DawnStarts||hour>=NightStarts?DayPhase.Night:hour<DayStarts?DayPhase.Dawn:hour<DuskStarts?DayPhase.Day:DayPhase.Dusk);}
        internal static RegionPalette ThemeForPhase(DayPhase phase){if((int)phase<0||(int)phase>=Palettes.Length)throw new ArgumentOutOfRangeException("phase");return Palettes[(int)phase];}
        private static string FindDigitFamily(){foreach(string name in new[]{"Segoe UI Variable Display Semibold","Segoe UI Variable Display Semib","Segoe UI Semibold","Segoe UI Variable Display"})using(var font=new Font(name,12,FontStyle.Regular,GraphicsUnit.Pixel))if(String.Equals(font.FontFamily.Name,name,StringComparison.OrdinalIgnoreCase))return name;return "Segoe UI";}
        internal static Font NewDigitFont(float pixels){return new Font(DigitFamily,pixels,DigitFamily.IndexOf("Semib",StringComparison.OrdinalIgnoreCase)>=0?FontStyle.Regular:FontStyle.Bold,GraphicsUnit.Pixel);}
        private static Image LoadMascot(){using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TwoZoneClock.Whalegirl.png")){if(stream==null)throw new InvalidOperationException("The embedded whalegirl artwork is missing.");using(var image=Image.FromStream(stream))return new Bitmap(image);}}
        internal static Icon CreateIcon(){using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TwoZoneClock.Whalegirl.ico")){if(stream==null)throw new InvalidOperationException("The embedded whalegirl icon is missing.");using(var icon=new Icon(stream))return (Icon)icon.Clone();}}
        internal static void DrawMascot(Graphics g,RectangleF bounds){var state=g.Save();g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;float ratio=Math.Min(bounds.Width/Mascot.Width,bounds.Height/Mascot.Height);float w=Mascot.Width*ratio,h=Mascot.Height*ratio;g.DrawImage(Mascot,bounds.X+(bounds.Width-w)/2,bounds.Y+(bounds.Height-h)/2,w,h);g.Restore(state);}
        internal static GraphicsPath Rounded(RectangleF r,float radius){var path=new GraphicsPath();float d=Math.Max(0,Math.Min(radius*2,Math.Min(r.Width,r.Height)));if(r.Width<=0||r.Height<=0)return path;if(d<1){path.AddRectangle(r);return path;}path.AddArc(r.X,r.Y,d,d,180,90);path.AddArc(r.Right-d,r.Y,d,d,270,90);path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.X,r.Bottom-d,d,d,90,90);path.CloseFigure();return path;}
        internal static void DrawShadow(Graphics g,RectangleF bounds,float radius){for(int layer=3;layer>=1;--layer)using(var path=Rounded(new RectangleF(bounds.X,bounds.Y+layer*.75f,bounds.Width,bounds.Height),radius))using(var shade=new SolidBrush(Color.FromArgb(3+layer,91,84,126)))g.FillPath(shade,path);}
        internal static void DrawCard(Graphics g,RectangleF bounds,Color color,float radius){DrawGradientCard(g,bounds,color,color,Border,radius);}
        internal static void DrawGradientCard(Graphics g,RectangleF bounds,RegionPalette palette,float radius){DrawGradientCard(g,bounds,palette.Top,palette.Bottom,palette.Border,radius);}
        internal static void DrawGradientCard(Graphics g,RectangleF bounds,Color top,Color bottom,Color border,float radius){if(bounds.Width<=0||bounds.Height<=0)return;DrawShadow(g,bounds,radius);using(var path=Rounded(bounds,radius))using(var fill=new LinearGradientBrush(bounds,top,bottom,90f))using(var line=new Pen(border,1)){g.FillPath(fill,path);g.DrawPath(line,path);}}
        internal static void DrawDayIcon(Graphics g,RectangleF bounds,RegionPalette palette,RectangleF cardBounds){
            if(bounds.Width<=0||bounds.Height<=0)return;var state=g.Save();g.SmoothingMode=SmoothingMode.AntiAlias;
            float u=Math.Min(bounds.Width,bounds.Height)/24f,x=bounds.X+(bounds.Width-24*u)/2,y=bounds.Y+(bounds.Height-24*u)/2;
            using(var brush=new SolidBrush(palette.Accent))using(var pen=new Pen(palette.Accent,1.35f*u){StartCap=LineCap.Round,EndCap=LineCap.Round}){
                if(palette.Phase==DayPhase.Night){
                    g.FillEllipse(brush,x+2*u,y+3*u,16*u,16*u);
                    // Exact card gradient coordinates prevent a solid-colour halo at the crescent edge.
                    using(var background=new LinearGradientBrush(cardBounds,palette.Top,palette.Bottom,90f))g.FillEllipse(background,x+7*u,y,16*u,16*u);
                    DrawStar(g,brush,x+19*u,y+7*u,2.2f*u);DrawStar(g,brush,x+20.5f*u,y+16.5f*u,1.4f*u);
                }else if(palette.Phase==DayPhase.Day){g.FillEllipse(brush,x+7*u,y+7*u,10*u,10*u);for(int ray=0;ray<8;ray++){double a=ray*Math.PI/4;g.DrawLine(pen,x+(12+(float)Math.Cos(a)*7)*u,y+(12+(float)Math.Sin(a)*7)*u,x+(12+(float)Math.Cos(a)*10)*u,y+(12+(float)Math.Sin(a)*10)*u);}}
                else{var clip=g.Save();g.SetClip(new RectangleF(x,y,24*u,15*u));g.FillEllipse(brush,x+6*u,y+9*u,12*u,12*u);g.Restore(clip);g.DrawLine(pen,x+2*u,y+15*u,x+22*u,y+15*u);g.DrawLine(pen,x+5*u,y+19*u,x+19*u,y+19*u);if(palette.Phase==DayPhase.Dawn){g.DrawLine(pen,x+12*u,y+3*u,x+12*u,y+6*u);g.DrawLine(pen,x+3.5f*u,y+7*u,x+6*u,y+9*u);g.DrawLine(pen,x+20.5f*u,y+7*u,x+18*u,y+9*u);}else{g.DrawLine(pen,x+12*u,y+2*u,x+12*u,y+6*u);g.DrawLine(pen,x+10*u,y+4*u,x+12*u,y+6*u);g.DrawLine(pen,x+14*u,y+4*u,x+12*u,y+6*u);}}
            }g.Restore(state);
        }
        private static void DrawStar(Graphics g,Brush b,float x,float y,float r){g.FillPolygon(b,new[]{new PointF(x,y-r),new PointF(x+r*.35f,y-r*.35f),new PointF(x+r,y),new PointF(x+r*.35f,y+r*.35f),new PointF(x,y+r),new PointF(x-r*.35f,y+r*.35f),new PointF(x-r,y),new PointF(x-r*.35f,y-r*.35f)});}
        internal static bool RoundWindow(IntPtr hwnd,bool small){if(hwnd==IntPtr.Zero)return false;try{int preference=small?3:2;return DwmSetWindowAttribute(hwnd,33,ref preference,4)==0;}catch(DllNotFoundException){return false;}catch(EntryPointNotFoundException){return false;}}
        [DllImport("dwmapi.dll",PreserveSig=true)]private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);

        internal static void StyleButton(Button button,bool primary){
            button.UseVisualStyleBackColor=false;button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=0;button.Cursor=Cursors.Hand;button.BackColor=primary?Blue:Color.White;button.ForeColor=primary?Color.White:Ink;
            if(button.Region!=null){var old=button.Region;button.Region=null;old.Dispose();}
            var candy=button as CandyButton;if(candy!=null){candy.Primary=primary;button.Invalidate();return;}
            PlainButtonStyle style;if(!PlainStyles.TryGetValue(button,out style)){style=new PlainButtonStyle(button);PlainStyles.Add(button,style);}style.Primary=primary;button.Invalidate();
        }
        private sealed class PlainButtonStyle{
            internal bool Primary;private bool hover,pressed;
            internal PlainButtonStyle(Button button){
                button.Paint+=delegate(object sender,PaintEventArgs e){CandyButton.PaintCandy(button,e.Graphics,Primary,hover,pressed,button.Focused);};
                button.MouseEnter+=delegate{hover=true;button.Invalidate();};button.MouseLeave+=delegate{hover=false;pressed=false;button.Invalidate();};button.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){pressed=true;button.Invalidate();}};button.MouseUp+=delegate{pressed=false;button.Invalidate();};
                button.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Space){pressed=true;button.Invalidate();}};button.KeyUp+=delegate{pressed=false;button.Invalidate();};button.EnabledChanged+=delegate{hover=false;pressed=false;button.Invalidate();};button.GotFocus+=delegate{button.Invalidate();};button.LostFocus+=delegate{pressed=false;button.Invalidate();};
            }
        }
        internal static void StyleMenu(ContextMenuStrip menu){menu.BackColor=Surface;menu.ForeColor=Ink;menu.Font=new Font("Microsoft YaHei UI",9f);menu.ShowImageMargin=false;menu.ShowCheckMargin=true;StyleDropDown(menu);menu.Opening+=delegate{StyleChildren(menu);};}
        private static void StyleChildren(ToolStripDropDown menu){foreach(ToolStripItem child in menu.Items){var item=child as ToolStripMenuItem;if(item!=null&&item.HasDropDownItems){StyleDropDown(item.DropDown);StyleChildren(item.DropDown);}}}
        private static void StyleDropDown(ToolStripDropDown menu){object marker;if(StyledMenus.TryGetValue(menu,out marker))return;StyledMenus.Add(menu,new object());menu.BackColor=Surface;menu.ForeColor=Ink;menu.Renderer=new WhaleMenuRenderer();menu.HandleCreated+=delegate{RoundWindow(menu.Handle,true);};menu.Opened+=delegate{RoundWindow(menu.Handle,true);};}
        private sealed class WhaleMenuRenderer:ToolStripProfessionalRenderer{
            internal WhaleMenuRenderer():base(new MenuColors()){RoundedEdges=false;}
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(Surface);}
            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){}
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){using(var line=new Pen(Border,1))e.Graphics.DrawRectangle(line,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1);}
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){float s=DrawingScale(e.Graphics);var state=e.Graphics.Save();e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;if(e.Item.Selected&&e.Item.Enabled)using(var path=Rounded(new RectangleF(4*s,s,e.Item.Width-8*s,e.Item.Height-2*s),6*s))using(var fill=new SolidBrush(Color.FromArgb(227,240,250)))e.Graphics.FillPath(fill,path);var item=e.Item as ToolStripMenuItem;if(item!=null&&item.CheckOnClick&&!item.Checked)DrawToggle(e.Graphics,item,false,s);e.Graphics.Restore(state);}
            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e){var item=e.Item as ToolStripMenuItem;if(item!=null){var state=e.Graphics.Save();e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;DrawToggle(e.Graphics,item,true,DrawingScale(e.Graphics));e.Graphics.Restore(state);}}
            private static void DrawToggle(Graphics g,ToolStripMenuItem item,bool selected,float s){float d=12*s,x=8*s,y=(item.Height-d)/2f;if(selected){using(var fill=new SolidBrush(Blue))g.FillEllipse(fill,x,y,d,d);using(var pen=new Pen(Color.White,1.4f*s){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})g.DrawLines(pen,new[]{new PointF(x+3*s,y+6*s),new PointF(x+5*s,y+8*s),new PointF(x+9*s,y+3.8f*s)});}else using(var pen=new Pen(Color.FromArgb(201,211,220),1.2f*s))g.DrawEllipse(pen,x,y,d,d);}
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){float s=DrawingScale(e.Graphics);using(var line=new Pen(Color.FromArgb(236,232,242),1))e.Graphics.DrawLine(line,10*s,e.Item.Height/2f,e.Item.Width-10*s,e.Item.Height/2f);}
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=!e.Item.Enabled?Muted:e.Item.Text=="退出"||e.Item.Text=="Exit"?Color.FromArgb(194,86,107):Ink;base.OnRenderItemText(e);}
        }
        private sealed class MenuColors:ProfessionalColorTable{
            public override Color ToolStripDropDownBackground{get{return Surface;}}public override Color MenuBorder{get{return Border;}}
            public override Color ImageMarginGradientBegin{get{return Surface;}}public override Color ImageMarginGradientMiddle{get{return Surface;}}public override Color ImageMarginGradientEnd{get{return Surface;}}
        }
    }
    internal sealed class CandyButton:Button{
        internal bool Primary;private bool hover,pressed;
        public CandyButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);UseVisualStyleBackColor=false;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){pressed=true;Invalidate();}base.OnMouseDown(e);}protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){pressed=true;Invalidate();}base.OnKeyDown(e);}protected override void OnKeyUp(KeyEventArgs e){pressed=false;Invalidate();base.OnKeyUp(e);}
        protected override void OnEnabledChanged(EventArgs e){hover=false;pressed=false;Invalidate();base.OnEnabledChanged(e);}protected override void OnLostFocus(EventArgs e){pressed=false;Invalidate();base.OnLostFocus(e);}protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
        protected override void OnPaintBackground(PaintEventArgs e){}protected override void OnPaint(PaintEventArgs e){PaintCandy(this,e.Graphics,Primary,hover,pressed,Focused&&ShowFocusCues);}
        internal static void PaintCandy(Button button,Graphics g,bool primary,bool hover,bool pressed,bool focus){
            g.Clear(button.Parent==null?WhaleTheme.Surface:button.Parent.BackColor);if(button.Width<8||button.Height<8)return;g.SmoothingMode=SmoothingMode.AntiAlias;float s=WhaleTheme.DrawingScale(g);bool down=pressed&&button.Enabled;
            var face=new RectangleF(.5f*s,down?2.5f*s:.5f*s,button.Width-2*s,button.Height-3.5f*s);float r=face.Height/2;
            Color fill=!button.Enabled?Color.FromArgb(229,235,240):primary?(down?Color.FromArgb(61,133,178):hover?Color.FromArgb(94,167,209):WhaleTheme.Blue):(down?Color.FromArgb(226,240,249):hover?Color.FromArgb(242,248,252):Color.White);
            if(button.Enabled&&!down&&primary)using(var edge=WhaleTheme.Rounded(new RectangleF(face.X,face.Y+2*s,face.Width,face.Height),r))using(var brush=new SolidBrush(WhaleTheme.BlueEdge))g.FillPath(brush,edge);
            using(var path=WhaleTheme.Rounded(face,r)){using(var brush=new SolidBrush(fill))g.FillPath(brush,path);if(!primary||!button.Enabled)using(var line=new Pen(WhaleTheme.ButtonBorder,s))g.DrawPath(line,path);}
            if(focus&&button.Enabled)using(var ring=WhaleTheme.Rounded(RectangleF.Inflate(face,-3*s,-3*s),Math.Max(1,r-3*s)))using(var pen=new Pen(primary?Color.FromArgb(225,Color.White):WhaleTheme.Blue,s){DashStyle=DashStyle.Dot})g.DrawPath(pen,ring);
            TextRenderer.DrawText(g,button.Text,button.Font,Rectangle.Round(face),!button.Enabled?WhaleTheme.Muted:primary?Color.White:WhaleTheme.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
        }
    }
    internal sealed class RoundedCard:Panel{
        internal Color FillColor=Color.White,BorderColor=WhaleTheme.Border;internal float Radius=12;internal bool DrawShadow=true;
        public RoundedCard(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=WhaleTheme.Surface;}
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent==null?WhaleTheme.Surface:Parent.BackColor);if(Width<8||Height<8)return;g.SmoothingMode=SmoothingMode.AntiAlias;float s=WhaleTheme.DrawingScale(g);var r=new RectangleF(.5f*s,.5f*s,Width-2*s,Height-3.5f*s);if(DrawShadow)WhaleTheme.DrawShadow(g,r,Radius*s);using(var path=WhaleTheme.Rounded(r,Radius*s))using(var fill=new SolidBrush(FillColor))using(var line=new Pen(BorderColor,s)){g.FillPath(fill,path);g.DrawPath(line,path);}base.OnPaint(e);}
    }
}
