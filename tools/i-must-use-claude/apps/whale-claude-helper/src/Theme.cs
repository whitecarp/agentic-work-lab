using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace WhaleClaudeHelper {
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
        protected override void OnEnabledChanged(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.Clear(Parent == null ? Theme.Cream : Parent.BackColor);
            if (Width < 8 || Height < 8) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool down = pressed && Enabled;
            // The face sits 2 px above a darker "candy" edge; pressing pushes it down onto the edge.
            var face = new RectangleF(0.5f, down ? 2f : 0.5f, Width - 2f, Height - 3.5f);
            float radius = face.Height / 2f;
            if (Enabled && !down)
                using (var edgePath = Theme.RoundedRect(new RectangleF(face.X, face.Y + 2f, face.Width, face.Height), radius))
                using (var edge = new SolidBrush(Primary ? Theme.BlueEdge : Theme.Line)) g.FillPath(edge, edgePath);
            Color fill = !Enabled ? Theme.Disabled : Primary ? (down ? Theme.BluePressed : hover ? Theme.BlueHover : Theme.BlueDark) : (down ? Theme.Blue : hover ? Theme.Mist : Color.White);
            using (var path = Theme.RoundedRect(face, radius)) {
                using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                if (!Primary && Enabled) using (var line = new Pen(Theme.Line, 1f)) g.DrawPath(line, path);
            }
            if (Focused && ShowFocusCues)
                using (var ring = Theme.RoundedRect(RectangleF.Inflate(face, -3f, -3f), radius - 3f))
                using (var pen = new Pen(Primary ? Color.FromArgb(210, Color.White) : Theme.BlueHover, 1.5f) { DashStyle = DashStyle.Dot }) g.DrawPath(pen, ring);
            TextRenderer.DrawText(g, Text, Font, Rectangle.Round(face), !Enabled ? Theme.Muted : (Primary ? Color.White : Theme.Ink),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
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
            var face = new RectangleF(0.5f, 0.5f, Width - 2f, Height - 3.5f);
            using (var shadowPath = Theme.RoundedRect(new RectangleF(face.X, face.Y + 2.5f, face.Width, face.Height), 18f))
            using (var shadow = new SolidBrush(Theme.CardShadow)) g.FillPath(shadow, shadowPath);
            using (var path = Theme.RoundedRect(face, 18f))
            using (var fill = new SolidBrush(Color.White))
            using (var line = new Pen(Theme.Line, 1f)) { g.FillPath(fill, path); g.DrawPath(line, path); }
        }
    }
    internal static class Theme {
        internal static void SetWindowIcon(Form form,bool modal){try{form.Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}if(modal)form.ShowInTaskbar=false;}
        public static readonly Color Cream = Color.FromArgb(255,249,241);
        public static readonly Color Blue = Color.FromArgb(255,224,197);
        public static readonly Color Mist = Color.FromArgb(255,241,226);
        public static readonly Color BlueDark = Color.FromArgb(172,72,37);
        public static readonly Color BlueHover = Color.FromArgb(190,84,42);
        public static readonly Color BluePressed = Color.FromArgb(148,59,28);
        public static readonly Color BlueEdge = Color.FromArgb(121,49,29);
        public static readonly Color Line = Color.FromArgb(236,213,191);
        public static readonly Color CardShadow = Color.FromArgb(243,226,208);
        public static readonly Color Disabled = Color.FromArgb(235,222,209);
        public static readonly Color Ink = Color.FromArgb(67,48,41);
        public static readonly Color Muted = Color.FromArgb(125,96,79);
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
    }
}
