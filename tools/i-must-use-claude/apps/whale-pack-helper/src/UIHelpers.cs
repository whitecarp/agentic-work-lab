using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace WhalePack {
    internal static class Ui {
        internal static string T(AppSettings s, string zh, string en) { return s.Language == "en" ? en : zh; }
        internal static Font Font(float size, bool bold) { return new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point); }
        internal static void Setup(Form f, string title, int width, int height) {
            f.SuspendLayout();
            f.Font = Font(9f, false); f.BackColor = Theme.Cream; f.ForeColor = Theme.Ink;
            f.AutoScaleDimensions = new SizeF(96, 96); f.AutoScaleMode = AutoScaleMode.Dpi;
            f.Text = title; f.ClientSize = new Size(width, height); f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedSingle; f.MaximizeBox = false;
            f.Icon = LoadIcon();
        }
        internal static Icon LoadIcon() {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("WhalePack.icon.ico")) {
                if (s != null) using (Icon icon = new Icon(s)) return (Icon)icon.Clone();
            }
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "icon.ico");
            return File.Exists(file) ? new Icon(file) : SystemIcons.Application;
        }
        internal static Image LoadWhale() {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("WhalePack.whale.png")) {
                if (s != null) using (Image img = Image.FromStream(s)) return CompositeWhale(img);
            }
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "whale.png");
            if (!File.Exists(file)) return null;
            using (Image img = Image.FromFile(file)) return CompositeWhale(img);
        }
        // The supplied WEBP is RGB; clear only edge-connected white at display time.
        // White highlights surrounded by the drawing stay opaque and source assets stay untouched.
        private static Bitmap CompositeWhale(Image image) {
            Bitmap bitmap = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap)) g.DrawImageUnscaled(image, 0, 0);
            Queue<Point> queue = new Queue<Point>(); bool[,] visited = new bool[bitmap.Width, bitmap.Height];
            for (int x = 0; x < bitmap.Width; x++) { queue.Enqueue(new Point(x, 0)); queue.Enqueue(new Point(x, bitmap.Height - 1)); }
            for (int y = 0; y < bitmap.Height; y++) { queue.Enqueue(new Point(0, y)); queue.Enqueue(new Point(bitmap.Width - 1, y)); }
            while (queue.Count > 0) {
                Point p = queue.Dequeue();
                if (p.X < 0 || p.Y < 0 || p.X >= bitmap.Width || p.Y >= bitmap.Height || visited[p.X, p.Y]) continue;
                visited[p.X, p.Y] = true; Color c = bitmap.GetPixel(p.X, p.Y);
                if (c.R < 247 || c.G < 247 || c.B < 247) continue;
                bitmap.SetPixel(p.X, p.Y, Color.Transparent);
                queue.Enqueue(new Point(p.X - 1, p.Y)); queue.Enqueue(new Point(p.X + 1, p.Y)); queue.Enqueue(new Point(p.X, p.Y - 1)); queue.Enqueue(new Point(p.X, p.Y + 1));
            }
            return bitmap;
        }
        internal static Label Label(Control p, string text, int x, int y, int w, int h, float size, bool bold, Color color) {
            Label l = new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h), Font = Font(size, bold), ForeColor = color,
                BackColor = Color.Transparent, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            p.Controls.Add(l); return l;
        }
        internal static Button Button(Control p, string text, int x, int y, int w, int h, bool primary, EventHandler click) {
            Button b = Theme.Button(text, primary); b.SetBounds(x, y, w, h); b.Margin = Padding.Empty;
            if (click != null) b.Click += click; p.Controls.Add(b); return b;
        }
        internal static string Size(long bytes) {
            if (bytes < 1000) return bytes.ToString("N0") + " B";
            if (bytes < 1000000) return (bytes / 1000.0).ToString("0.#") + " KB";
            return (bytes / 1000000.0).ToString("0.#") + " MB";
        }
        internal static string Mode(AppSettings s, ProcessingMode mode) {
            switch (mode) {
                case ProcessingMode.CompressVideo: return T(s, "压缩视频", "Compress video");
                case ProcessingMode.ResizeImage: return T(s, "图片缩小", "Resize image");
                case ProcessingMode.SourceArchive: return T(s, "打包源码", "Pack source");
                case ProcessingMode.Exclude: return T(s, "排除", "Exclude");
                default: return T(s, "原样", "Keep original");
            }
        }
        internal static void Error(IWin32Window owner, AppSettings settings, Exception ex) {
            MessageBox.Show(owner, ex.Message, T(settings, "未能完成", "Could not complete"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    internal sealed class RoundCheckBox : CheckBox {
        internal RoundCheckBox() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand; }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(Parent == null ? Theme.Cream : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float diameter = Math.Min(17f * Height / 30f, Height - 4); RectangleF r = new RectangleF(1, (Height - diameter) / 2, diameter, diameter);
            using (Brush b = new SolidBrush(Checked ? Theme.BlueDark : Color.White)) e.Graphics.FillEllipse(b, r);
            using (Pen p = new Pen(Checked ? Theme.BlueDark : Theme.Line, 1.3f)) e.Graphics.DrawEllipse(p, r);
            if (Checked) using (Pen p = new Pen(Color.White, 2f)) e.Graphics.DrawLines(p, new PointF[] { new PointF(r.X + diameter * .24f, r.Y + diameter * .52f), new PointF(r.X + diameter * .44f, r.Y + diameter * .71f), new PointF(r.X + diameter * .78f, r.Y + diameter * .29f) });
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle((int)diameter + 8, 0, Math.Max(1, Width - (int)diameter - 8), Height), Enabled ? ForeColor : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
        }
    }

    internal sealed class DropCard : Panel {
        internal DropCard() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); BackColor = Theme.Mist; }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(Parent == null ? Theme.Cream : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Theme.RoundedRect(new RectangleF(1, 1, Width - 3, Height - 3), 16f))
            using (Brush b = new SolidBrush(Theme.Mist))
            using (Pen p = new Pen(Color.FromArgb(133, 187, 214), 1.2f) { DashStyle = DashStyle.Dash }) { e.Graphics.FillPath(b, path); e.Graphics.DrawPath(p, path); }
        }
    }

    internal sealed class SizeBar : Control {
        internal long Bytes;
        internal string Caption = "";
        internal SizeBar() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true); BackColor = Color.White; }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = Height / 24f; int labelWidth = (int)(144 * scale); int valueWidth = (int)(85 * scale);
            TextRenderer.DrawText(e.Graphics, Caption, Font, new Rectangle(0, 0, labelWidth - 5, Height), Theme.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            RectangleF bar = new RectangleF(labelWidth, Height / 2f - 4 * scale, Math.Max(12, Width - labelWidth - valueWidth - 8), 8 * scale);
            using (GraphicsPath path = Theme.RoundedRect(bar, 4 * scale)) using (Brush b = new SolidBrush(Theme.Line)) e.Graphics.FillPath(b, path);
            float proportion = Math.Max(0f, Math.Min(1f, Bytes / 30000000f));
            if (proportion > 0f) {
                RectangleF fill = bar; fill.Width = Math.Max(1, bar.Width * proportion);
                using (GraphicsPath path = Theme.RoundedRect(fill, 4 * scale))
                using (Brush b = new LinearGradientBrush(new PointF(bar.Left, 0), new PointF(bar.Right, 0), Color.FromArgb(142, 206, 234), Theme.BlueDark)) e.Graphics.FillPath(b, path);
            }
            TextRenderer.DrawText(e.Graphics, (Bytes / 1000000.0).ToString("0.#") + " / 30 MB", Font, new Rectangle(Width - valueWidth, 0, valueWidth, Height), Bytes > Limits.MaxOutputBytes ? Color.DarkOrange : Theme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }

    internal sealed class FileGlyph : Control {
        internal ProcessingMode Mode; internal bool Directory; internal bool Warning;
        internal FileGlyph() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics; g.Clear(Parent == null ? Color.White : Parent.BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            float k = Math.Min(Width, Height) / 28f; g.ScaleTransform(k, k);
            if (Warning) {
                using (Brush b = new SolidBrush(Color.FromArgb(255, 211, 110))) g.FillPolygon(b, new PointF[] { new PointF(14, 2), new PointF(27, 25), new PointF(1, 25) });
                using (Font f = Ui.Font(14, true)) g.DrawString("!", f, Brushes.SaddleBrown, 9, 6);
            } else if (Directory || Mode == ProcessingMode.SourceArchive) {
                using (Brush b = new SolidBrush(Color.FromArgb(242, 190, 92))) g.FillRectangle(b, 2, 3, 12, 8);
                using (Brush b = new SolidBrush(Color.FromArgb(255, 214, 125))) using (GraphicsPath p = Theme.RoundedRect(new RectangleF(1, 8, 26, 18), 4)) g.FillPath(b, p);
            } else {
                using (Brush b = new SolidBrush(Theme.Blue)) using (GraphicsPath p = Theme.RoundedRect(new RectangleF(1, 1, 26, 26), 5)) g.FillPath(b, p);
                if (Mode == ProcessingMode.CompressVideo) using (Brush b = new SolidBrush(Theme.BlueDark)) g.FillPolygon(b, new PointF[] { new PointF(10, 6), new PointF(10, 22), new PointF(21, 14) });
                else if (Mode == ProcessingMode.ResizeImage) {
                    using (Brush b = new SolidBrush(Color.FromArgb(132, 192, 148))) g.FillPolygon(b, new PointF[] { new PointF(3, 23), new PointF(12, 10), new PointF(20, 23) });
                    using (Brush b = new SolidBrush(Color.FromArgb(250, 206, 111))) g.FillEllipse(b, 19, 4, 6, 6);
                } else using (Pen p = new Pen(Theme.BlueDark, 1.5f)) { g.DrawLine(p, 7, 8, 21, 8); g.DrawLine(p, 7, 14, 21, 14); g.DrawLine(p, 7, 20, 17, 20); }
            }
        }
    }
}
