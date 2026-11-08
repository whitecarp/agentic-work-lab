using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCompanion {
    internal static class ScreenWindowPicker {
        private static int active;

        public static Task<SourceWindow> PickAsync() {
            if (Interlocked.CompareExchange(ref active, 1, 0) != 0)
                return Task.FromResult<SourceWindow>(null);
            var completion = new TaskCompletionSource<SourceWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(delegate() {
                SourceWindow chosen = null;
                Exception failure = null;
                try { using (var session = new PickerSession()) chosen = session.Run(); }
                catch { failure = new InvalidOperationException(Ui.T("无法打开窗口框选，请重试。", "The window picker could not open. Please try again.")); }
                finally { Interlocked.Exchange(ref active, 0); }
                // Images, HWND and the helper thread's DPI scope are disposed before resuming Main.
                if (failure == null) completion.TrySetResult(chosen);
                else completion.TrySetException(failure);
            }) { IsBackground = true, Name = "Whale window picker" };
            thread.SetApartmentState(ApartmentState.STA);
            try { thread.Start(); }
            catch { Interlocked.Exchange(ref active, 0); completion.TrySetException(new InvalidOperationException(Ui.T("无法打开窗口框选，请重试。", "The window picker could not open. Please try again."))); }
            return completion.Task;
        }

        private sealed class PickerSession : IDisposable {
            private readonly Native.WindowProc callback;
            private readonly List<PickerWindowFrame> windows = new List<PickerWindowFrame>();
            private IntPtr hwnd, instance, previousDpi;
            private ushort atom;
            private string className;
            private Bitmap screenshot, buffer;
            private Rectangle desktop, monitor, selection;
            private Point origin;
            private bool dragging, closing, everActivated;
            private string notice, previewTitle = "";
            private SourceWindow result;
            private Exception failure;

            public PickerSession() { callback = WindowProcedure; }

            public SourceWindow Run() {
                previousDpi = Native.SetThreadDpiAwarenessContext(new IntPtr(-4));
                if (previousDpi == IntPtr.Zero) throw new InvalidOperationException();
                desktop = new Rectangle(Native.GetSystemMetrics(76), Native.GetSystemMetrics(77), Native.GetSystemMetrics(78), Native.GetSystemMetrics(79));
                if (!PickerGeometry.Valid(desktop) || (long)desktop.Width * desktop.Height > 33554432L)
                    throw new InvalidOperationException();
                Native.POINT pointer;
                monitor = desktop;
                if (Native.GetPhysicalCursorPos(out pointer)) {
                    var info = new Native.MONITORINFO { Size = Marshal.SizeOf(typeof(Native.MONITORINFO)) };
                    if (Native.GetMonitorInfo(Native.MonitorFromPoint(pointer, 2), ref info)) monitor = info.Monitor.ToRectangle();
                }
                CaptureWindows();
                screenshot = new Bitmap(desktop.Width, desktop.Height, PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(screenshot))
                    graphics.CopyFromScreen(desktop.Left, desktop.Top, 0, 0, desktop.Size, CopyPixelOperation.SourceCopy);
                buffer = new Bitmap(desktop.Width, desktop.Height, PixelFormat.Format32bppPArgb);
                notice = Ui.T("框选要锁定的程序窗口", "Drag over the app window to lock");
                className = "WhaleWindowPicker_" + Guid.NewGuid().ToString("N");
                instance = Native.GetModuleHandle(null);
                var definition = new Native.WNDCLASS {
                    style = 0x0003, lpfnWndProc = Marshal.GetFunctionPointerForDelegate(callback),
                    hInstance = instance, hCursor = Native.LoadCursor(IntPtr.Zero, new IntPtr(32515)), lpszClassName = className
                };
                atom = Native.RegisterClass(ref definition);
                if (atom == 0) throw new InvalidOperationException();
                hwnd = Native.CreateWindowEx(0x00000008 | 0x00000080, className,
                    Ui.T("大肥鱼 · 框选窗口", "Whale · Pick a window"), 0x80000000,
                    desktop.X, desktop.Y, desktop.Width, desktop.Height, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
                if (hwnd == IntPtr.Zero) throw new InvalidOperationException();
                if (!Native.SetWindowPos(hwnd, new IntPtr(-1), desktop.X, desktop.Y, desktop.Width, desktop.Height, 0x0040))
                    throw new InvalidOperationException();
                Native.ShowWindow(hwnd, 5);
                Native.SetForegroundWindow(hwnd);
                Native.SetFocus(hwnd);
                Native.MSG message;
                int status;
                while ((status = Native.GetMessage(out message, IntPtr.Zero, 0, 0)) > 0) {
                    Native.TranslateMessage(ref message);
                    Native.DispatchMessage(ref message);
                }
                if (status < 0 || failure != null) throw new InvalidOperationException();
                return result;
            }

            private void CaptureWindows() {
                uint own = (uint)Process.GetCurrentProcess().Id;
                bool overflow = false;
                bool complete = Native.EnumWindows(delegate(IntPtr handle, IntPtr unused) {
                    if (!Native.IsWindowVisible(handle) || Native.IsIconic(handle) || Cloaked(handle)) return true;
                    Rectangle bounds = WindowBounds(handle);
                    if (!PickerGeometry.Valid(bounds) || !bounds.IntersectsWith(desktop)) return true;
                    uint processId;
                    Native.GetWindowThreadProcessId(handle, out processId);
                    // Disallowed windows remain occluders. Never select an app behind
                    // the translator itself or another excluded surface.
                    SourceWindow source = processId != 0 && processId != own ? SourceWindow.FromHandle(handle) : null;
                    windows.Add(new PickerWindowFrame { Handle = handle, Bounds = bounds, Source = source });
                    if (windows.Count <= 512) return true;
                    overflow = true;
                    return false;
                }, IntPtr.Zero);
                if (!complete || overflow) throw new InvalidOperationException();
            }

            private Point CursorPoint() {
                Native.POINT point;
                if (!Native.GetPhysicalCursorPos(out point)) throw new InvalidOperationException();
                return new Point(Math.Max(desktop.Left, Math.Min(desktop.Right - 1, point.X)),
                    Math.Max(desktop.Top, Math.Min(desktop.Bottom - 1, point.Y)));
            }

            private IntPtr WindowProcedure(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam) {
                try {
                    if (message == 0x000F) {
                        Native.PAINTSTRUCT paint;
                        IntPtr dc = Native.BeginPaint(handle, out paint);
                        try { if (dc != IntPtr.Zero) Paint(dc); }
                        finally { Native.EndPaint(handle, ref paint); }
                        return IntPtr.Zero;
                    }
                    if (message == 0x0014) return new IntPtr(1); // No separate erase/flicker.
                    if (message == 0x0100 && wParam.ToInt32() == 27) { Finish(null); return IntPtr.Zero; }
                    if (message == 0x0204 || message == 0x0205) { Finish(null); return IntPtr.Zero; }
                    if (message == 0x0201) {
                        origin = CursorPoint(); selection = Rectangle.Empty; previewTitle = "";
                        notice = Ui.T("松开鼠标，锁定框内程序", "Release to lock the app in this box");
                        dragging = true; Native.SetCapture(handle); Native.InvalidateRect(handle, IntPtr.Zero, false);
                        return IntPtr.Zero;
                    }
                    if (message == 0x0200 && dragging) {
                        selection = PickerGeometry.Normalize(origin, CursorPoint(), desktop);
                        PickerWindowFrame hovered = PickerGeometry.Pick(selection, desktop, windows);
                        previewTitle = hovered == null ? "" : hovered.Source.Title;
                        Native.InvalidateRect(handle, IntPtr.Zero, false); return IntPtr.Zero;
                    }
                    if (message == 0x0202 && dragging) {
                        selection = PickerGeometry.Normalize(origin, CursorPoint(), desktop);
                        dragging = false; Native.ReleaseCapture();
                        Choose(); return IntPtr.Zero;
                    }
                    if (message == 0x0215 && dragging) {
                        dragging = false; selection = Rectangle.Empty; previewTitle = "";
                        notice = Ui.T("请重新框选一个程序窗口", "Please draw over one app window again");
                        Native.InvalidateRect(handle, IntPtr.Zero, false); return IntPtr.Zero;
                    }
                    if (message == 0x0006) {
                        if ((wParam.ToInt64() & 0xffff) != 0) everActivated = true;
                        else if (everActivated && !closing) Finish(null);
                    }
                    if (message == 0x007E || message == 0x02E0 || message == 0x0010) { Finish(null); return IntPtr.Zero; }
                    if (message == 0x0002) { Native.PostQuitMessage(0); return IntPtr.Zero; }
                    return Native.DefWindowProc(handle, message, wParam, lParam);
                } catch {
                    failure = new InvalidOperationException();
                    Finish(null);
                    return IntPtr.Zero;
                }
            }

            private void Choose() {
                PickerWindowFrame chosen = PickerGeometry.Pick(selection, desktop, windows);
                if (chosen == null) {
                    notice = Ui.T("请在同一个程序窗口内重新框选", "Please draw inside one app window");
                    previewTitle = ""; Native.InvalidateRect(hwnd, IntPtr.Zero, false); return;
                }
                SourceWindow current = SourceWindow.FromHandle(chosen.Handle);
                if (current == null || !chosen.Source.IsAlive() || !current.Matches(chosen.Source.Handle, chosen.Source.ProcessId, chosen.Source.Started) ||
                    WindowBounds(chosen.Handle) != chosen.Bounds) {
                    notice = Ui.T("窗口已变化，请按 Esc 后重新框选", "The window changed. Press Esc and try again");
                    previewTitle = ""; Native.InvalidateRect(hwnd, IntPtr.Zero, false); return;
                }
                Finish(current);
            }

            private void Paint(IntPtr dc) {
                if (screenshot == null || buffer == null) return;
                Rectangle localSelection = selection;
                if (!localSelection.IsEmpty) localSelection.Offset(-desktop.Left, -desktop.Top);
                Rectangle localMonitor = monitor;
                localMonitor.Offset(-desktop.Left, -desktop.Top);
                using (Graphics graphics = Graphics.FromImage(buffer))
                    PickerArtwork.Draw(graphics, screenshot, localSelection, localMonitor, notice, previewTitle);
                using (Graphics graphics = Graphics.FromHdc(dc)) graphics.DrawImageUnscaled(buffer, 0, 0);
            }

            private void Finish(SourceWindow chosen) {
                if (closing) return;
                closing = true; result = chosen; dragging = false;
                Native.ReleaseCapture();
                if (hwnd != IntPtr.Zero) Native.DestroyWindow(hwnd);
            }

            public void Dispose() {
                closing = true;
                try { if (hwnd != IntPtr.Zero && Native.IsWindow(hwnd)) Native.DestroyWindow(hwnd); }
                finally {
                    hwnd = IntPtr.Zero;
                    try { if (buffer != null) buffer.Dispose(); }
                    finally {
                        buffer = null;
                        try { if (screenshot != null) screenshot.Dispose(); }
                        finally {
                            screenshot = null;
                            try { if (atom != 0) Native.UnregisterClass(className, instance); }
                            finally {
                                atom = 0;
                                if (previousDpi != IntPtr.Zero) {
                                    Native.SetThreadDpiAwarenessContext(previousDpi);
                                    previousDpi = IntPtr.Zero;
                                }
                            }
                        }
                    }
                }
            }

            private static bool Cloaked(IntPtr handle) {
                int value;
                try { return Native.DwmGetWindowAttribute(handle, 14, out value, sizeof(int)) == 0 && value != 0; }
                catch { return false; }
            }
            private static Rectangle WindowBounds(IntPtr handle) {
                Native.RECT rect;
                try { if (Native.DwmGetWindowAttribute(handle, 9, out rect, Marshal.SizeOf(typeof(Native.RECT))) == 0) return rect.ToRectangle(); }
                catch { }
                return Native.GetWindowRect(handle, out rect) ? rect.ToRectangle() : Rectangle.Empty;
            }
        }

        private static class Native {
            [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
            [StructLayout(LayoutKind.Sequential)] internal struct RECT {
                public int Left, Top, Right, Bottom;
                public Rectangle ToRectangle() { return Rectangle.FromLTRB(Left, Top, Right, Bottom); }
            }
            [StructLayout(LayoutKind.Sequential)] internal struct MONITORINFO { public int Size; public RECT Monitor, Work; public uint Flags; }
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct WNDCLASS {
                public uint style; public IntPtr lpfnWndProc; public int cbClsExtra, cbWndExtra;
                public IntPtr hInstance, hIcon, hCursor, hbrBackground; public string lpszMenuName, lpszClassName;
            }
            [StructLayout(LayoutKind.Sequential)] internal struct MSG {
                public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; public uint lPrivate;
            }
            [StructLayout(LayoutKind.Sequential)] internal struct PAINTSTRUCT {
                public IntPtr hdc; [MarshalAs(UnmanagedType.Bool)] public bool fErase; public RECT rcPaint;
                [MarshalAs(UnmanagedType.Bool)] public bool fRestore; [MarshalAs(UnmanagedType.Bool)] public bool fIncUpdate;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] reserved;
            }
            internal delegate bool EnumWindowProc(IntPtr hwnd, IntPtr extra);
            internal delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
            [DllImport("user32.dll")] internal static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
            [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
            [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowProc callback, IntPtr extra);
            [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
            [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
            [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
            [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
            [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
            [DllImport("user32.dll")] internal static extern bool GetPhysicalCursorPos(out POINT point);
            [DllImport("user32.dll")] internal static extern IntPtr MonitorFromPoint(POINT point, uint flags);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
            [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
            [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string module);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern ushort RegisterClass(ref WNDCLASS value);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool UnregisterClass(string name, IntPtr instance);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameters);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
            [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
            [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hwnd, int command);
            [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
            [DllImport("user32.dll")] internal static extern IntPtr SetFocus(IntPtr hwnd);
            [DllImport("user32.dll")] internal static extern IntPtr SetCapture(IntPtr hwnd);
            [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
            [DllImport("user32.dll")] internal static extern bool InvalidateRect(IntPtr hwnd, IntPtr rect, bool erase);
            [DllImport("user32.dll")] internal static extern bool DestroyWindow(IntPtr hwnd);
            [DllImport("user32.dll")] internal static extern void PostQuitMessage(int exitCode);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetMessage(out MSG message, IntPtr hwnd, uint first, uint last);
            [DllImport("user32.dll")] internal static extern bool TranslateMessage(ref MSG message);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr DispatchMessage(ref MSG message);
            [DllImport("user32.dll")] internal static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT paint);
            [DllImport("user32.dll")] internal static extern bool EndPaint(IntPtr hwnd, ref PAINTSTRUCT paint);
        }
    }

    internal sealed class PickerWindowFrame {
        public IntPtr Handle;
        public Rectangle Bounds;
        public SourceWindow Source;
    }

    internal static class PickerGeometry {
        internal static bool Valid(Rectangle rect) {
            return rect.Width > 0 && rect.Height > 0 && rect.Width <= 65536 && rect.Height <= 65536 &&
                Math.Abs((long)rect.Left) < 2000000 && Math.Abs((long)rect.Top) < 2000000 &&
                Math.Abs((long)rect.Left + rect.Width) < 2000000 && Math.Abs((long)rect.Top + rect.Height) < 2000000;
        }
        internal static Rectangle Normalize(Point start, Point end, Rectangle desktop) {
            if (!Valid(desktop)) return Rectangle.Empty;
            int left = Math.Max(desktop.Left, Math.Min(start.X, end.X));
            int top = Math.Max(desktop.Top, Math.Min(start.Y, end.Y));
            int right = Math.Min(desktop.Right, Math.Max(start.X, end.X));
            int bottom = Math.Min(desktop.Bottom, Math.Max(start.Y, end.Y));
            return right > left && bottom > top ? Rectangle.FromLTRB(left, top, right, bottom) : Rectangle.Empty;
        }
        internal static PickerWindowFrame Pick(Rectangle selection, Rectangle desktop, IList<PickerWindowFrame> zOrder) {
            if (!Valid(desktop) || !Valid(selection) || selection.Width < 8 || selection.Height < 8 ||
                !desktop.Contains(selection) || zOrder == null || zOrder.Count > 512) return null;
            Point center = new Point(selection.Left + selection.Width / 2, selection.Top + selection.Height / 2);
            int index = -1;
            for (int i = 0; i < zOrder.Count; i++) {
                if (zOrder[i] != null && Valid(zOrder[i].Bounds) && zOrder[i].Bounds.Contains(center)) { index = i; break; }
            }
            if (index < 0 || zOrder[index].Handle == IntPtr.Zero || zOrder[index].Source == null) return null;
            var visible = new List<Rectangle> { Rectangle.Intersect(selection, zOrder[index].Bounds) };
            for (int i = 0; i < index; i++) {
                if (zOrder[i] == null || !Valid(zOrder[i].Bounds)) continue;
                var next = new List<Rectangle>();
                foreach (Rectangle part in visible) Subtract(part, zOrder[i].Bounds, next);
                visible = next;
                if (visible.Count == 0 || visible.Count > 2048) return null;
            }
            long area = 0;
            foreach (Rectangle part in visible) area += (long)part.Width * part.Height;
            return area * 100 >= (long)selection.Width * selection.Height * 60 ? zOrder[index] : null;
        }
        private static void Subtract(Rectangle source, Rectangle obstruction, IList<Rectangle> output) {
            Rectangle cut = Rectangle.Intersect(source, obstruction);
            if (cut.Width <= 0 || cut.Height <= 0) { output.Add(source); return; }
            Add(output, Rectangle.FromLTRB(source.Left, source.Top, source.Right, cut.Top));
            Add(output, Rectangle.FromLTRB(source.Left, cut.Bottom, source.Right, source.Bottom));
            Add(output, Rectangle.FromLTRB(source.Left, cut.Top, cut.Left, cut.Bottom));
            Add(output, Rectangle.FromLTRB(cut.Right, cut.Top, source.Right, cut.Bottom));
        }
        private static void Add(IList<Rectangle> output, Rectangle value) { if (value.Width > 0 && value.Height > 0) output.Add(value); }
    }

    internal static class PickerArtwork {
        // Rendering takes a supplied bitmap: geometry/artwork tests never capture the desktop.
        internal static void Draw(Graphics graphics, Bitmap screenshot, Rectangle selection, Rectangle hintMonitor, string notice, string title) {
            graphics.DrawImageUnscaled(screenshot, 0, 0);
            using (var shade = new SolidBrush(Color.FromArgb(145, 10, 16, 28)))
                graphics.FillRectangle(shade, 0, 0, screenshot.Width, screenshot.Height);
            if (selection.Width > 0 && selection.Height > 0) {
                graphics.DrawImage(screenshot, selection, selection, GraphicsUnit.Pixel);
                using (var border = new Pen(Color.FromArgb(92, 210, 255), 2)) graphics.DrawRectangle(border, selection);
                using (var corner = new SolidBrush(Color.FromArgb(196, 239, 255))) {
                    foreach (Point point in new[] { selection.Location, new Point(selection.Right, selection.Top),
                        new Point(selection.Left, selection.Bottom), new Point(selection.Right, selection.Bottom) })
                        graphics.FillRectangle(corner, point.X - 3, point.Y - 3, 6, 6);
                }
            }
            int width = Math.Max(120, Math.Min(700, hintMonitor.Width - 32));
            var box = new Rectangle(hintMonitor.Left + (hintMonitor.Width - width) / 2, hintMonitor.Top + 24, width, 116);
            using (var panel = new SolidBrush(Color.FromArgb(235, 25, 37, 55))) graphics.FillRectangle(panel, box);
            using (var font = new Font("Microsoft YaHei UI", 20, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var small = new Font("Microsoft YaHei UI", 15, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var white = new SolidBrush(Color.White))
            using (var muted = new SolidBrush(Color.FromArgb(186, 209, 226)))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) {
                graphics.DrawString(notice ?? "", font, white, new Rectangle(box.Left + 12, box.Top + 6, box.Width - 24, 34), format);
                string detail = String.IsNullOrWhiteSpace(title) ? Ui.T("只选择程序窗口 · 截图仅在本机临时显示", "Select an app window · Preview stays on this device") : title;
                graphics.DrawString(detail, small, muted, new Rectangle(box.Left + 12, box.Top + 43, box.Width - 24, 26), format);
                graphics.DrawString(Ui.T("松开确定 · Esc / 右键取消", "Release to select · Esc / right-click to cancel"), small, muted,
                    new Rectangle(box.Left + 12, box.Top + 76, box.Width - 24, 26), format);
            }
        }
    }
}
