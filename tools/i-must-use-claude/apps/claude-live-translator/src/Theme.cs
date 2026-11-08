using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace ClaudeCompanion {
    // Pill-shaped candy button. Everything is painted here, including the parent's colour in the
    // corners: an unpainted double buffer is black, which is what showed up as black corners.
    internal sealed class RoundedButton : Button {
        internal bool Primary;
        private bool hover, pressed;
        public RoundedButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if(e.KeyCode==Keys.Space){pressed=true;Invalidate();}base.OnKeyDown(e); }
        protected override void OnKeyUp(KeyEventArgs e) { pressed=false;Invalidate();base.OnKeyUp(e); }
        protected override void OnLostFocus(EventArgs e) { pressed=false;Invalidate();base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.Clear(Parent == null ? Theme.Cream : Parent.BackColor);
            if (Width < 8 || Height < 8) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool down = pressed && Enabled;
            // The face sits 2 px above a darker "candy" edge; pressing pushes it down onto the edge.
            float s=g.DpiX/96F;
            var face = new RectangleF(0.5f*s, (down ? 2f : 0.5f)*s, Width - 2f*s, Height - 3.5f*s);
            float radius = face.Height / 2f;
            if (Enabled && !down)
                using (var edgePath = Theme.RoundedRect(new RectangleF(face.X, face.Y + 2f*s, face.Width, face.Height), radius))
                using (var edge = new SolidBrush(Primary ? Theme.BlueEdge : Theme.Line)) g.FillPath(edge, edgePath);
            Color fill = !Enabled ? Theme.Disabled : Primary ? (down ? Theme.BluePressed : hover ? Theme.BlueHover : Theme.BlueDark) : (down ? Theme.Blue : hover ? Theme.Mist : Color.White);
            using (var path = Theme.RoundedRect(face, radius)) {
                using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                if (!Primary && Enabled) using (var line = new Pen(Theme.Line, s)) g.DrawPath(line, path);
            }
            if (Focused && ShowFocusCues)
                using (var ring = Theme.RoundedRect(RectangleF.Inflate(face, -3f*s, -3f*s), radius - 3f*s))
                using (var pen = new Pen(Primary ? Color.FromArgb(210, Color.White) : Theme.BlueHover, 1.5f*s) { DashStyle = DashStyle.Dot }) g.DrawPath(pen, ring);
            TextRenderer.DrawText(g, Text, Font, Rectangle.Round(face), !Enabled ? Theme.Muted : (Primary ? Color.White : Theme.Ink),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }
    // Soft white bubble behind a text box. Painted with anti-aliasing instead of being clipped with
    // a Region, which gives jagged corners.
    internal sealed class RoundedCard : Panel {
        public RoundedCard() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.Clear(Parent == null ? Theme.Cream : Parent.BackColor);
            if (Width < 8 || Height < 8) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s=g.DpiX/96F;
            var face = new RectangleF(0.5f*s, 0.5f*s, Width - 2f*s, Height - 3.5f*s);
            using (var shadowPath = Theme.RoundedRect(new RectangleF(face.X, face.Y + 2.5f*s, face.Width, face.Height), 18f*s))
            using (var shadow = new SolidBrush(Theme.CardShadow)) g.FillPath(shadow, shadowPath);
            using (var path = Theme.RoundedRect(face, 18f*s))
            using (var fill = new SolidBrush(Color.White))
            using (var line = new Pen(Theme.Line, s)) { g.FillPath(fill, path); g.DrawPath(line, path); }
        }
    }
    internal static class Theme {
        public static readonly Color Cream = Color.FromArgb(250,248,242);
        public static readonly Color Blue = Color.FromArgb(226,242,250);
        public static readonly Color Mist = Color.FromArgb(240,248,252);
        public static readonly Color BlueDark = Color.FromArgb(76,151,196);
        public static readonly Color BlueHover = Color.FromArgb(94,167,209);
        public static readonly Color BluePressed = Color.FromArgb(64,136,180);
        public static readonly Color BlueEdge = Color.FromArgb(52,118,160);
        public static readonly Color Line = Color.FromArgb(212,231,242);
        public static readonly Color CardShadow = Color.FromArgb(231,237,238);
        public static readonly Color Disabled = Color.FromArgb(240,243,243);
        public static readonly Color Ink = Color.FromArgb(56,78,95);
        public static readonly Color Muted = Color.FromArgb(122,146,160);
        public static readonly Color Pink = Color.FromArgb(244,158,177);
        public static GraphicsPath RoundedRect(RectangleF r, float radius) {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            if (d < 1f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
            return p;
        }
        public static Control Card(Control child, Padding margin) {
            var card = new RoundedCard { Dock = DockStyle.Fill, Padding = new Padding(14, 12, 10, 14), Margin = margin };
            card.Controls.Add(child);
            return card;
        }
        public static Button Button(string text, bool primary) {
            var b = new RoundedButton { Text=text, Primary=primary, Height=38, FlatStyle=FlatStyle.Flat, BackColor=primary?BlueDark:Color.White,
                ForeColor=primary?Color.White:Ink, Font=new Font("Microsoft YaHei UI",9.5f,FontStyle.Bold),
                Cursor=Cursors.Hand, Margin=new Padding(0,4,8,4), UseVisualStyleBackColor=false };
            b.FlatAppearance.BorderSize=0;
            return b;
        }
        public static ContextMenuStrip CreateMenu() {
            int padding=Scale(null,5);
            return new CreamContextMenu { Renderer=new CreamMenuRenderer(),BackColor=Cream,ForeColor=Ink,
                Font=new Font("Microsoft YaHei UI",9F),ShowImageMargin=false,ShowCheckMargin=true,Padding=new Padding(padding),AutoClose=true };
        }
        public static void MenuItem(ToolStripItem item) { item.AutoSize=false;item.Height=Scale(null,28);item.Width=Scale(null,210); }
        internal static int Scale(Control control,float value) {
            using(var g=control!=null&&control.IsHandleCreated?control.CreateGraphics():Graphics.FromHwnd(IntPtr.Zero))return (int)Math.Round(value*g.DpiX/96F);
        }
    }

    internal sealed class CreamContextMenu:ContextMenuStrip {
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            try { int corner=3;DwmSetWindowAttribute(Handle,33,ref corner,4); } catch(DllNotFoundException) { } catch(EntryPointNotFoundException) { }
        }
    }

    internal sealed class SelectionSwitch : Button {
        private bool selected;
        internal bool Selected { get { return selected; } set { if(selected==value)return;selected=value;Invalidate();if(IsHandleCreated)AccessibilityNotifyClients(AccessibleEvents.StateChange,-1); } }
        internal SelectionSwitch() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.CheckButton;
            Font=new Font("Microsoft YaHei UI",8.5F);Height=28;
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.Clear(Parent==null?Theme.Cream:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            float scale=g.DpiX/96F;float h=16*scale,w=30*scale,y=(Height-h)/2;
            using(var path=Theme.RoundedRect(new RectangleF(1,y,w,h),h/2))
            using(var fill=new SolidBrush(!Enabled?Theme.Disabled:selected?Theme.BlueDark:Color.FromArgb(190,208,217)))g.FillPath(fill,path);
            using(var fill=new SolidBrush(Enabled?Color.White:Color.FromArgb(216,225,229)))g.FillEllipse(fill,selected?1+w-h+2*scale:1+2*scale,y+2*scale,h-4*scale,h-4*scale);
            TextRenderer.DrawText(g,Text,Font,new Rectangle((int)(w+7*scale),0,Math.Max(1,Width-(int)(w+7*scale)),Height),Enabled?Theme.Ink:Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(ClientRectangle,-2,-2));
        }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e);Invalidate(); }
        protected override AccessibleObject CreateAccessibilityInstance() { return new SwitchAccessibility(this); }
        private sealed class SwitchAccessibility:Control.ControlAccessibleObject {
            private readonly SelectionSwitch owner;
            internal SwitchAccessibility(SelectionSwitch control):base(control) { owner=control; }
            public override AccessibleStates State { get { return base.State|(owner.Selected?AccessibleStates.Checked:0); } }
            public override void DoDefaultAction() { if(owner.Enabled)owner.PerformClick(); }
        }
    }

    internal sealed class WindowLineIcon : Control {
        internal WindowLineIcon() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.Clear(Parent==null?Color.White:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            float s=Math.Min(Width/36F,Height/34F);var state=g.Save();g.TranslateTransform((Width-34*s)/2,(Height-28*s)/2);g.ScaleTransform(s,s);
            using(var p=Theme.RoundedRect(new RectangleF(1,1,31,26),5))using(var pen=new Pen(Theme.BlueDark,1.3F))using(var fill=new SolidBrush(Theme.Mist)){
                g.FillPath(fill,p);g.DrawPath(pen,p);g.DrawLine(pen,2,8,31,8);
                using(var dot=new SolidBrush(Theme.BlueDark)){g.FillEllipse(dot,5,4,2,2);g.FillEllipse(dot,10,4,2,2);}
            }g.Restore(state);
        }
    }

    internal sealed class CreamMenuRenderer : ToolStripProfessionalRenderer {
        internal CreamMenuRenderer() { RoundedEdges=false; }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(Theme.Cream); }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float s=e.Graphics.DpiX/96F;
            using(var p=Theme.RoundedRect(new RectangleF(.5F*s,.5F*s,e.ToolStrip.Width-s,e.ToolStrip.Height-s),10*s))using(var pen=new Pen(Theme.Line,s))e.Graphics.DrawPath(pen,p);
        }
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
            if(!e.Item.Selected||!e.Item.Enabled)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float s=e.Graphics.DpiX/96F;
            using(var p=Theme.RoundedRect(new RectangleF(2*s,s,e.Item.Width-4*s,e.Item.Height-2*s),7*s))using(var brush=new SolidBrush(Theme.Blue))e.Graphics.FillPath(brush,p);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor=e.Item.Enabled?Theme.Ink:Theme.Muted;base.OnRenderItemText(e); }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float s=g.DpiX/96F;var r=new RectangleF(e.ImageRectangle.X+2*s,e.Item.Height/2F-6*s,12*s,12*s);
            using(var b=new SolidBrush(Theme.BlueDark))g.FillEllipse(b,r);
            using(var p=new Pen(Color.White,1.5F*s))g.DrawLines(p,new[]{new PointF(r.X+3*s,r.Y+6*s),new PointF(r.X+5*s,r.Y+8*s),new PointF(r.X+9*s,r.Y+4*s)});
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) { float s=e.Graphics.DpiX/96F;using(var pen=new Pen(Theme.Line,s))e.Graphics.DrawLine(pen,10*s,e.Item.Height/2,e.Item.Width-10*s,e.Item.Height/2); }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor=Theme.Muted;base.OnRenderArrow(e); }
    }

    // Retains ComboBox's binding/selection contract; both the face and its popup are drawn by us.
    internal sealed class ThemedComboBox : ComboBox {
        private ContextMenuStrip popup;
        internal bool ChoicesOpen { get { return popup!=null&&popup.Visible; } }
        internal ThemedComboBox() {
            DropDownStyle=ComboBoxStyle.DropDownList;DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=28;FlatStyle=FlatStyle.Flat;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        }
        protected override void WndProc(ref Message m) {
            if((m.Msg==0x201||m.Msg==0x203)&&Enabled) { Focus();ShowChoices();return; }
            // Every native dropdown request (including keyboard/accessibility)
            // routes to the same menu. No unthemed ComboLBox is opened.
            if(m.Msg==0x14F) { if(m.WParam!=IntPtr.Zero)ShowChoices();else CloseChoices();return; }
            base.WndProc(ref m);
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
            if(keyData==Keys.F4||keyData==(Keys.Alt|Keys.Down)||keyData==Keys.Space||keyData==Keys.Enter) { ShowChoices();return true; }
            if(keyData==Keys.Escape&&popup!=null&&popup.Visible){CloseChoices();return true;}
            return base.ProcessCmdKey(ref msg,keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e) {
            if(e.KeyCode==Keys.F4||e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter||(e.Alt&&e.KeyCode==Keys.Down)) { e.Handled=true;e.SuppressKeyPress=true;ShowChoices();return; }
            base.OnKeyDown(e);
        }
        internal void ShowChoices() {
            if(!Enabled||!Visible||Items.Count==0||!IsHandleCreated)return;if(popup!=null&&popup.Visible)return;if(popup!=null){popup.Dispose();popup=null;}
            popup=BuildChoicesMenu();
            popup.Closed+=delegate{if(!IsDisposed)OnDropDownClosed(EventArgs.Empty);};
            OnDropDown(EventArgs.Empty);
            popup.Show(this,new Point(0,Height));
            if(SelectedIndex>=0&&SelectedIndex<popup.Items.Count)popup.Items[SelectedIndex].Select();
        }
        internal ContextMenuStrip BuildChoicesMenu() {
            var menu=Theme.CreateMenu();
            int width=Math.Max(Theme.Scale(this,180),Width-Theme.Scale(this,12));
            for(int i=0;i<Items.Count;i++) { int index=i;string label=GetItemText(Items[i]);var item=new ToolStripMenuItem(label){Checked=i==SelectedIndex,ToolTipText=label};Theme.MenuItem(item);item.Width=width;item.Click+=delegate{if(index>=0&&index<Items.Count&&SelectedIndex!=index)SelectedIndex=index;};menu.Items.Add(item); }
            return menu;
        }
        internal void CloseChoices() { if(popup!=null)popup.Close(); }
        protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e);Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { if(!Enabled)CloseChoices();base.OnEnabledChanged(e);Invalidate(); }
        protected override void OnVisibleChanged(EventArgs e) { if(!Visible)CloseChoices();base.OnVisibleChanged(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e);Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e);Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.Clear(Parent==null?Theme.Cream:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            if(Width<4||Height<4)return;float s=g.DpiX/96F;
            using(var p=Theme.RoundedRect(new RectangleF(.5F*s,.5F*s,Width-s,Height-s),Height/2F))using(var fill=new SolidBrush(Enabled?Color.White:Theme.Disabled))using(var pen=new Pen(Focused?Theme.BlueDark:Theme.Line,s)) { g.FillPath(fill,p);g.DrawPath(pen,p); }
            TextRenderer.DrawText(g,SelectedItem==null?Text:GetItemText(SelectedItem),Font,new Rectangle((int)(9*s),0,Math.Max(1,Width-(int)(30*s)),Height),Enabled?Theme.Ink:Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
            using(var pen=new Pen(Theme.Muted,1.3F*s))g.DrawLines(pen,new[]{new PointF(Width-18*s,Height/2F-2*s),new PointF(Width-14*s,Height/2F+2*s),new PointF(Width-10*s,Height/2F-2*s)});
        }
        protected override void OnDrawItem(DrawItemEventArgs e) {
            using(var b=new SolidBrush((e.State&DrawItemState.Selected)!=0?Theme.Blue:Theme.Cream))e.Graphics.FillRectangle(b,e.Bounds);
            if(e.Index>=0)TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,e.Bounds,Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
        }
        protected override void Dispose(bool disposing) { if(disposing&&popup!=null)popup.Dispose();base.Dispose(disposing); }
    }
}
