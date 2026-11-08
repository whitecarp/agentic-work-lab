using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace WhaleClaudeHelper {
    // The mascot: a chubby milk-blue whale with a cream belly, pink cheeks and a little spout.
    // Drawn in code in a 100 x 70 design box, so it scales cleanly with the control.
    internal sealed class WhaleBadge : Control {
        private static readonly Image Mascot=LoadMascot();
        private static Image LoadMascot(){try{using(var stream=typeof(WhaleBadge).Assembly.GetManifestResourceStream("HelperMascot")){if(stream==null)return null;using(var original=Image.FromStream(stream))return new Bitmap(original);}}catch{return null;}}
        public WhaleBadge() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); BackColor = Color.Transparent; }
        protected override void OnPaint(PaintEventArgs e) { Draw(e.Graphics, Width, Height); }

        internal static void Draw(Graphics g, float width, float height) {
            if(Mascot!=null){var saved=g.Save();g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;float size=System.Math.Min(width,height);g.DrawImage(Mascot,new RectangleF((width-size)/2,(height-size)/2,size,size));g.Restore(saved);return;}
            GraphicsState state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(width / 100f, height / 70f);
            using (var outline = new Pen(Color.FromArgb(70, 140, 190), 1.7f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var spout = new Pen(Color.FromArgb(118, 199, 236), 2.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var drop = new SolidBrush(Color.FromArgb(164, 222, 246)))
            using (var finFill = new SolidBrush(Color.FromArgb(104, 176, 222)))
            using (var belly = new SolidBrush(Color.FromArgb(250, 252, 250)))
            using (var gloss = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
            using (var ink = new SolidBrush(Color.FromArgb(44, 70, 97)))
            using (var white = new SolidBrush(Color.White))
            using (var cheek = new SolidBrush(Color.FromArgb(200, 250, 152, 174)))
            using (var smile = new Pen(Color.FromArgb(44, 70, 97), 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var star = new SolidBrush(Color.FromArgb(255, 208, 102)))
            using (var bubble = new Pen(Color.FromArgb(150, 210, 238), 1.2f))
            using (var skin = new LinearGradientBrush(new PointF(0f, 14f), new PointF(0f, 68f), Color.FromArgb(154, 213, 243), Color.FromArgb(106, 180, 227)))
            using (var body = new GraphicsPath())
            using (var bodyLine = new GraphicsPath())
            using (var tail = new GraphicsPath())
            using (var tailLine = new GraphicsPath())
            using (var fin = new GraphicsPath())
            using (var sparkle = new GraphicsPath()) {
                // Spout behind the head: three little jets with round droplets.
                g.DrawBezier(spout, 39f, 17f, 38f, 13f, 39.5f, 10f, 39f, 7f);
                g.DrawBezier(spout, 36.5f, 17f, 34.5f, 12.5f, 31.5f, 10.5f, 28.5f, 10f);
                g.DrawBezier(spout, 41.5f, 17f, 43.5f, 12.5f, 46.5f, 10.5f, 49.5f, 10f);
                g.FillEllipse(drop, 25.6f, 6.6f, 4.6f, 4.6f);
                g.FillEllipse(drop, 36.7f, 1.8f, 4.8f, 4.8f);
                g.FillEllipse(drop, 48.4f, 6.6f, 4.6f, 4.6f);

                // Heart-shaped tail fluke rising from a slim tail stalk. It shares the body's gradient and
                // its outline continues the body's, so the join is seamless.
                tailLine.AddBezier(86f, 37f, 83f, 32f, 76f, 25f, 78.5f, 19f);
                tailLine.AddBezier(78.5f, 19f, 81f, 13.5f, 88f, 15f, 89.5f, 24f);
                tailLine.AddBezier(89.5f, 24f, 92f, 15f, 99f, 13.5f, 98.5f, 20f);
                tailLine.AddBezier(98.5f, 20f, 98f, 29f, 91f, 38f, 86.5f, 44f);
                tail.AddPath(tailLine, true);
                tail.CloseFigure();
                g.FillPath(skin, tail);

                // Round head, soft back narrowing into the tail stalk, full round belly.
                bodyLine.AddBezier(86.5f, 44f, 80f, 48f, 75f, 55f, 66f, 59f);
                bodyLine.AddBezier(66f, 59f, 55f, 65f, 43f, 67f, 32f, 67f);
                bodyLine.AddBezier(32f, 67f, 19f, 67f, 11f, 63f, 8f, 57f);
                bodyLine.AddBezier(8f, 57f, 6f, 53f, 5f, 49f, 5f, 45f);
                bodyLine.AddBezier(5f, 45f, 5f, 27f, 20f, 16f, 39f, 16f);
                bodyLine.AddBezier(39f, 16f, 56f, 16f, 67f, 24f, 76f, 33f);
                bodyLine.AddBezier(76f, 33f, 79f, 36f, 83f, 38f, 86f, 37f);
                body.AddPath(bodyLine, true);
                body.CloseFigure();
                g.FillPath(skin, body);
                Region oldClip = g.Clip;
                g.SetClip(body, CombineMode.Intersect);
                g.FillEllipse(belly, -6f, 51f, 88f, 34f);   // cream belly following the body's curve
                g.FillEllipse(gloss, 15f, 19.5f, 27f, 10f);  // soft shine on the head
                g.Clip = oldClip;
                oldClip.Dispose();
                g.DrawPath(outline, bodyLine);
                g.DrawPath(outline, tailLine);

                // Little flipper waving from the belly.
                fin.AddBezier(43f, 56f, 46f, 63f, 52f, 67f, 57f, 65f);
                fin.AddBezier(57f, 65f, 56f, 60f, 50f, 55f, 43f, 56f);
                fin.CloseFigure();
                g.FillPath(finFill, fin);
                g.DrawPath(outline, fin);

                // Big glossy eye, blush and a tiny smile.
                g.FillEllipse(ink, 25.5f, 37f, 8.5f, 9.5f);
                g.FillEllipse(white, 28.6f, 38.6f, 3.6f, 3.6f);
                g.FillEllipse(white, 26.7f, 42.9f, 1.7f, 1.7f);
                g.FillEllipse(cheek, 16.5f, 47f, 10.5f, 5.6f);
                g.DrawArc(smile, 10.5f, 45f, 8f, 6.5f, 25f, 130f);

                // A tiny sparkle and a bubble.
                sparkle.AddPolygon(new PointF[] {
                    new PointF(68f, 5f), new PointF(69.3f, 9.7f), new PointF(74f, 11f), new PointF(69.3f, 12.3f),
                    new PointF(68f, 17f), new PointF(66.7f, 12.3f), new PointF(62f, 11f), new PointF(66.7f, 9.7f) });
                g.FillPath(star, sparkle);
                g.DrawEllipse(bubble, 77f, 3.5f, 5f, 5f);
            }
            g.Restore(state);
        }
    }
}
