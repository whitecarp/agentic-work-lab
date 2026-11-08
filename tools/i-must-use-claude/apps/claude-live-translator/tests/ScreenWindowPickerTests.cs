using System;
using System.Drawing;
using ClaudeCompanion;

internal static class ScreenWindowPickerTests {
    private static void Check(bool result, string label) { if (!result) throw new Exception(label); }
    private static PickerWindowFrame Window(int handle, Rectangle bounds, bool selectable = true) {
        return new PickerWindowFrame { Handle = new IntPtr(handle), Bounds = bounds,
            Source = selectable ? new SourceWindow { Handle = new IntPtr(handle), ProcessId = handle, Started = 1, Title = "Synthetic app" } : null };
    }
    public static int Main(string[] args) {
        Rectangle desktop = new Rectangle(-1920, -200, 4480, 1640);
        var back = Window(1, new Rectangle(0, 0, 1200, 1000));
        var front = Window(2, new Rectangle(600, 0, 600, 1000));
        Rectangle box = new Rectangle(650, 100, 300, 300);
        Check(PickerGeometry.Pick(box, desktop, new[] { front, back }) == front, "center resolves to topmost visible window");
        Check(PickerGeometry.Pick(new Rectangle(200, 100, 800, 300), desktop, new[] { front, back }) == null,
            "half in each app is ambiguous even when center belongs to the top window");
        Check(PickerGeometry.Pick(new Rectangle(400, 100, 600, 300), desktop, new[] { front, back }) == front,
            "a centered app covering more than 60 percent can be selected");
        var blockLeft = Window(3, new Rectangle(0, 0, 450, 1000), false);
        var blockRight = Window(4, new Rectangle(650, 0, 350, 1000), false);
        var full = Window(5, new Rectangle(0, 0, 1000, 1000));
        Check(PickerGeometry.Pick(new Rectangle(0, 0, 1000, 500), desktop, new[] { blockLeft, blockRight, full }) == null,
            "occlusion is subtracted from area even if the covered app owns the box center");
        Check(PickerGeometry.Pick(new Rectangle(650, 100, 300, 300), desktop, new[] { Window(6, front.Bounds, false), back }) == null,
            "an excluded center window blocks selection of the app behind it");
        var fortyPercent = Window(7, new Rectangle(0, 0, 400, 500), false);
        Check(PickerGeometry.Pick(new Rectangle(0, 0, 1000, 500), desktop, new[] { fortyPercent, full }) == full,
            "exactly 60 percent visible is accepted");
        var overFortyPercent = Window(8, new Rectangle(0, 0, 401, 500), false);
        Check(PickerGeometry.Pick(new Rectangle(0, 0, 1000, 500), desktop, new[] { overFortyPercent, full }) == null,
            "less than 60 percent visible is rejected");
        var negative = Window(9, new Rectangle(-1800, -100, 1200, 1000));
        Rectangle reversed = PickerGeometry.Normalize(new Point(-800, 700), new Point(-1600, 0), desktop);
        Check(reversed == new Rectangle(-1600, 0, 800, 700), "reverse-direction drag preserves physical negative coordinates");
        Check(PickerGeometry.Pick(reversed, desktop, new[] { negative, back }) == negative, "secondary monitor left of primary can be selected");
        Check(PickerGeometry.Normalize(new Point(-5000, -900), new Point(100, 100), desktop) == new Rectangle(-1920, -200, 2020, 300),
            "drag clamps to virtual desktop");
        Check(PickerGeometry.Pick(new Rectangle(700, 100, 1, 1), desktop, new[] { front }) == null, "plain click is not a screen region");
        Check(PickerGeometry.Pick(new Rectangle(3000, 100, 100, 100), desktop, new[] { front }) == null, "outside virtual screen is rejected");
        Check(PickerGeometry.Pick(new Rectangle(-1900, 1200, 100, 100), desktop, new[] { front, back }) == null, "empty desktop does not choose an unrelated window");
        Check(!PickerGeometry.Valid(new Rectangle(Int32.MaxValue, 0, 20, 20)), "overflow rectangles fail closed");

        // Synthetic pixels only: this test does not call PickAsync, EnumWindows or CopyFromScreen.
        using (var fixture = new Bitmap(900, 600))
        using (var canvas = new Bitmap(900, 600)) {
            using (Graphics graphics = Graphics.FromImage(fixture)) {
                graphics.Clear(Color.FromArgb(160, 190, 220));
                using (var fill = new SolidBrush(Color.FromArgb(220, 180, 120))) graphics.FillRectangle(fill, 450, 0, 450, 600);
            }
            var selected = new Rectangle(80, 220, 300, 250);
            using (Graphics graphics = Graphics.FromImage(canvas))
                PickerArtwork.Draw(graphics, fixture, selected, new Rectangle(0, 0, 900, 600), "Select a program window", "Synthetic editor");
            Check(canvas.GetPixel(160, 300).ToArgb() == fixture.GetPixel(160, 300).ToArgb(), "selected preview stays undimmed");
            Check(canvas.GetPixel(800, 500).R < fixture.GetPixel(800, 500).R, "outside preview is dimmed");
            Check(canvas.GetPixel(80, 300).B > canvas.GetPixel(80, 300).R, "selection has a visible blue border");
            if (args.Length == 2 && args[0] == "--synthetic-preview") canvas.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
        }
        Console.WriteLine("ScreenWindowPickerTests passed (visible-area geometry, negative monitors and synthetic artwork; no desktop access).");
        return 0;
    }
}

#if PICKER_ISOLATED_TESTS
// Link-time substitutes only for the standalone geometry/artwork test executable.
namespace ClaudeCompanion {
    internal sealed class SourceWindow {
        public IntPtr Handle;
        public int ProcessId;
        public long Started;
        public string Title;
        public static SourceWindow FromHandle(IntPtr handle) { throw new InvalidOperationException("Desktop access is forbidden in isolated tests."); }
        public bool IsAlive() { throw new InvalidOperationException("Desktop access is forbidden in isolated tests."); }
        public bool Matches(IntPtr handle, int processId, long started) { return Handle == handle && ProcessId == processId && Started == started; }
    }
    internal static class Ui { public static string T(string zh, string en) { return en; } }
}
#endif
