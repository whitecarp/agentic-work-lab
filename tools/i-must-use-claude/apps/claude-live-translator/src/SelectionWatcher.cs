using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClaudeCompanion {
    // Observes only mouse-button state and window identity. It never changes a selection.
    internal sealed class SelectionWatcher : IDisposable {
        private readonly Timer timer = new Timer { Interval = 50 };
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly SelectionGestureState gesture = new SelectionGestureState();
        private readonly uint ownProcessId = (uint)Process.GetCurrentProcess().Id;
        private bool disposed;

        public event Action<IntPtr, Point> SelectionReleased;

        public SelectionWatcher() { timer.Tick += OnTick; }

        // Call on the application's UI thread, as with the other Forms timers.
        public void Start() {
            if (disposed || timer.Enabled) return;
            gesture.Reset();
            timer.Start();
        }
        public void Pause() {
            if (disposed) return;
            timer.Stop();
            gesture.Reset();
        }
        public void Dispose() {
            if (disposed) return;
            Pause();
            disposed = true;
            timer.Tick -= OnTick;
            timer.Dispose();
            clock.Stop();
            SelectionReleased = null;
        }

        private void OnTick(object sender, EventArgs args) {
            if (disposed) return;
            IntPtr foreground = Native.GetForegroundWindow();
            Native.POINT cursor;
            uint processId;
            if (foreground == IntPtr.Zero || !Native.GetPhysicalCursorPos(out cursor) ||
                Native.GetWindowThreadProcessId(foreground, out processId) == 0 ||
                processId == 0 || foreground != Native.GetForegroundWindow()) {
                gesture.Reset();
                return;
            }
            bool down = (Native.GetAsyncKeyState(0x01) & 0x8000) != 0;
            IntPtr released = gesture.Observe(down, new Point(cursor.X, cursor.Y), foreground,
                processId, ownProcessId, clock.ElapsedMilliseconds);
            if (released == IntPtr.Zero) return;
            Point releasePoint = gesture.ReleasePoint;
            Action<IntPtr, Point> handler = SelectionReleased;
            if (handler != null) handler(released, releasePoint);
        }

        private static class Native {
            [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
            [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int virtualKey);
            // Physical coordinates keep the drag threshold independent of the main Form's DPI.
            [DllImport("user32.dll")] internal static extern bool GetPhysicalCursorPos(out POINT point);
            [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
            [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        }
    }

    // Pure state machine: tests do not inspect the desktop or synthesize any input.
    internal sealed class SelectionGestureState {
        internal const int MinimumDragPixels = 4;
        internal const int ReleaseDelayMilliseconds = 120;
        private const int MaximumSampleGapMilliseconds = 1000;
        private bool initialized, wasDown, tracking, dragged;
        private Point pressPoint, pendingPoint;
        private IntPtr pressWindow, pendingWindow;
        private uint pressProcessId, pendingProcessId;
        private long lastSampleMilliseconds, releasedAtMilliseconds;
        // Valid only when the most recent Observe returned a nonzero window handle.
        internal Point ReleasePoint { get; private set; }

        internal void Reset() {
            initialized = wasDown = tracking = dragged = false;
            pressWindow = pendingWindow = IntPtr.Zero;
            pressPoint = pendingPoint = ReleasePoint = Point.Empty;
            pressProcessId = pendingProcessId = 0;
            lastSampleMilliseconds = releasedAtMilliseconds = 0;
        }

        internal IntPtr Observe(bool down, Point physicalPoint, IntPtr foreground, uint processId,
            uint ownProcessId, long milliseconds) {
            ReleasePoint = Point.Empty;
            if (!initialized || milliseconds < lastSampleMilliseconds ||
                milliseconds - lastSampleMilliseconds > MaximumSampleGapMilliseconds) {
                Reset();
                initialized = true;
                wasDown = down;
                lastSampleMilliseconds = milliseconds;
                return IntPtr.Zero;
            }
            lastSampleMilliseconds = milliseconds;
            bool eligible = foreground != IntPtr.Zero && processId != 0 && processId != ownProcessId;
            if (pendingWindow != IntPtr.Zero && (!eligible || foreground != pendingWindow || processId != pendingProcessId))
                ClearPending();
            if (tracking && (!eligible || foreground != pressWindow || processId != pressProcessId))
                tracking = false;

            if (down && !wasDown) {
                // A new press always cancels the previous delayed notification.
                ClearPending();
                tracking = eligible;
                dragged = false;
                pressPoint = physicalPoint;
                pressWindow = foreground;
                pressProcessId = processId;
            }
            if (tracking) {
                long dx = (long)physicalPoint.X - pressPoint.X;
                long dy = (long)physicalPoint.Y - pressPoint.Y;
                if (Math.Abs(dx) >= MinimumDragPixels || Math.Abs(dy) >= MinimumDragPixels ||
                    dx * dx + dy * dy >= MinimumDragPixels * MinimumDragPixels) dragged = true;
            }
            if (!down && wasDown) {
                if (tracking && dragged) {
                    pendingWindow = pressWindow;
                    pendingProcessId = pressProcessId;
                    pendingPoint = physicalPoint;
                    releasedAtMilliseconds = milliseconds;
                }
                tracking = false;
            }
            wasDown = down;
            if (!down && pendingWindow != IntPtr.Zero &&
                milliseconds - releasedAtMilliseconds >= ReleaseDelayMilliseconds) {
                IntPtr released = pendingWindow;
                ReleasePoint = pendingPoint;
                ClearPending();
                return released;
            }
            return IntPtr.Zero;
        }

        private void ClearPending() {
            pendingWindow = IntPtr.Zero;
            pendingProcessId = 0;
            pendingPoint = Point.Empty;
            releasedAtMilliseconds = 0;
        }
    }
}
