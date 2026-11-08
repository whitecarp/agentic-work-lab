using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Forms;

namespace ClaudeCompanion {
    internal sealed class HighlightTrackingInfo {
        public int[][] Anchors;
        public int[] DocumentRuntimeId, ArticleRuntimeId, FeedRuntimeId;
        public string ConversationUrl;
    }
    // A separate, non-activating paint surface. It never changes Claude or its selection.
    internal sealed class SourceHighlighter : IDisposable {
        private const int MaxAgeMilliseconds = 2500;
        private readonly Timer timer = new Timer { Interval = 100 };
        private readonly uint ownProcessId = (uint)Process.GetCurrentProcess().Id;
        private readonly Native.WinEventProc eventCallback;
        private IntPtr target, locationHook, scrollHook, moveHook;
        private uint targetProcessId;
        private Rectangle expectedWindow;
        private IList<Rectangle> rectangles = new Rectangle[0];
        private DateTime capturedAtUtc, invalidatedAtUtc = DateTime.MinValue;
        private NativeOverlaySurface overlay;
        private bool disposed;
        private HighlightTrackingInfo tracking;
        private string trackingIdentity = "", trackingAnchors = "";
        private int generation;
        private Task<TrackingMeasurement> pendingMeasurement;
        private DateTime measurementStartedUtc, lastMeasurementUtc = DateTime.MinValue, nextMeasurementUtc = DateTime.MinValue;
        private TrackingCache measurementCache;
        private bool forceMeasurement;
        private bool measurementStalled;

        public SourceHighlighter() {
            eventCallback = OnTargetEvent;
            timer.Tick += delegate { RefreshOverlay(); };
        }

        public void Update(IntPtr handle, Rectangle windowBounds, IList<Rectangle> screenRectangles) {
            Update(handle, windowBounds, screenRectangles, DateTime.UtcNow);
        }

        // The timestamp must be the start of the corresponding read, not its completion.
        // A scroll/move that occurred while the read was in flight invalidates that read.
        public void Update(IntPtr handle, Rectangle windowBounds, IList<Rectangle> screenRectangles, DateTime snapshotStartedAtUtc) {
            Update(handle, windowBounds, screenRectangles, snapshotStartedAtUtc, null);
        }

        public void Update(IntPtr handle, Rectangle windowBounds, IList<Rectangle> screenRectangles, DateTime snapshotStartedAtUtc, HighlightTrackingInfo trackingInfo) {
            if (disposed) return;
            HighlightTrackingInfo safeTracking = CloneTracking(trackingInfo);
            if (handle == IntPtr.Zero || !HighlightGeometry.IsValid(windowBounds) || (safeTracking == null && (screenRectangles == null || screenRectangles.Count == 0 || screenRectangles.Count > 512))) { Hide(); return; }
            string identity = TrackingIdentity(safeTracking);
            string anchors = safeTracking == null ? "" : AnchorKey(safeTracking.Anchors);
            bool changedIdentity = target != handle || trackingIdentity != identity;
            if (target != handle) {
                UnhookEvents(); HidePaint(); target = handle;
                Native.GetWindowThreadProcessId(target, out targetProcessId);
                invalidatedAtUtc = DateTime.MinValue;
                if (!IsClaudeProcess(targetProcessId)) { Hide(); return; }
                // OUTOFCONTEXT is a notification subscription, not an input hook.
                locationHook = Native.SetWinEventHook(0x800B, 0x800B, IntPtr.Zero, eventCallback, targetProcessId, 0, 0);
                scrollHook = Native.SetWinEventHook(0x0012, 0x0013, IntPtr.Zero, eventCallback, targetProcessId, 0, 0);
                moveHook = Native.SetWinEventHook(0x000A, 0x000B, IntPtr.Zero, eventCallback, targetProcessId, 0, 0);
            }
            if (changedIdentity) {
                ++generation; HidePaint();
                lastMeasurementUtc = DateTime.MinValue; nextMeasurementUtc = DateTime.MinValue;
                expectedWindow = windowBounds;
                rectangles = screenRectangles == null ? (IList<Rectangle>)new Rectangle[0] : new List<Rectangle>(screenRectangles);
                capturedAtUtc = snapshotStartedAtUtc;
            } else if (safeTracking == null || lastMeasurementUtc == DateTime.MinValue) {
                expectedWindow = windowBounds;
                rectangles = screenRectangles == null ? (IList<Rectangle>)new Rectangle[0] : new List<Rectangle>(screenRectangles);
                capturedAtUtc = snapshotStartedAtUtc;
            }
            if (trackingAnchors != anchors) forceMeasurement = true;
            tracking = safeTracking; trackingIdentity = identity; trackingAnchors = anchors;
            timer.Start(); RefreshOverlay();
        }

        public void Hide() {
            if (disposed) return;
            timer.Stop(); rectangles = new Rectangle[0]; target = IntPtr.Zero;
            capturedAtUtc = DateTime.MinValue; UnhookEvents(); HidePaint();
            tracking = null; trackingIdentity = trackingAnchors = ""; ++generation;
            lastMeasurementUtc = DateTime.MinValue; nextMeasurementUtc = DateTime.MinValue;
        }

        private static bool IsClaudeProcess(uint processId) {
            try { using (var p = Process.GetProcessById((int)processId)) return String.Equals(p.ProcessName, "claude", StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        private void OnTargetEvent(IntPtr hook, uint kind, IntPtr hwnd, int objectId, int childId, uint thread, uint timestamp) {
            if (disposed || target == IntPtr.Zero || hwnd == IntPtr.Zero) return;
            if (hwnd != target && !Native.IsChild(target, hwnd)) return;
            // Scrolling and text layout changes request a fresh measurement; hiding on
            // every such event caused flashing while a response streamed or scrolled.
            forceMeasurement = true;
            if (kind == 0x000A || kind == 0x000B) { invalidatedAtUtc = DateTime.UtcNow; HidePaint(); }
        }

        private void RefreshOverlay() {
            if (disposed || target == IntPtr.Zero) { HidePaint(); return; }
            if (tracking != null) PollTracking();
            if (measurementStalled) { HighlightTrace.Record("measurement_stalled"); HidePaint(); return; }
            DateTime freshness = tracking != null && lastMeasurementUtc != DateTime.MinValue ? lastMeasurementUtc : capturedAtUtc;
            if (rectangles.Count == 0) { HighlightTrace.Record("no_rectangles"); HidePaint(); return; }
            if (!HighlightGeometry.IsFresh(freshness, DateTime.UtcNow, invalidatedAtUtc, MaxAgeMilliseconds)) { HighlightTrace.Record("stale_measurement", rectangles.Count); HidePaint(); return; }
            if (!Native.IsWindow(target) || !Native.IsWindowVisible(target) || Native.IsIconic(target) || IsCloaked(target)) { HighlightTrace.Record("target_hidden"); HidePaint(); return; }
            uint currentTargetProcess;
            Native.GetWindowThreadProcessId(target, out currentTargetProcess);
            if (currentTargetProcess != targetProcessId) { HighlightTrace.Record("target_process_changed"); HidePaint(); return; }
            IntPtr foreground = Native.GetForegroundWindow(); uint foregroundProcess;
            Native.GetWindowThreadProcessId(foreground, out foregroundProcess);
            if (!HighlightGeometry.ForegroundAllowed(foreground == target, foregroundProcess == ownProcessId)) { HighlightTrace.Record("other_foreground"); HidePaint(); return; }
            // No DPI context change, UIA call or secondary WinForms Form on this UI thread.
            if (overlay == null) overlay = new NativeOverlaySurface(ownProcessId);
            overlay.Update(target, targetProcessId, expectedWindow, rectangles, freshness);
        }

        private void PollTracking() {
            if (pendingMeasurement != null) {
                if (!pendingMeasurement.IsCompleted) {
                    measurementStalled = (DateTime.UtcNow - measurementStartedUtc).TotalMilliseconds > 1800;
                    return; // One remote UIA call at a time, even if that call has stalled.
                }
                try {
                    TrackingMeasurement result = pendingMeasurement.GetAwaiter().GetResult();
                    measurementCache = result.Cache;
                    if (result.Generation == generation && result.Identity == trackingIdentity) {
                        if (result.Valid) {
                            expectedWindow = result.Window;
                            rectangles = result.Rectangles;
                            lastMeasurementUtc = result.MeasuredAtUtc;
                            HighlightTrace.Record("measured", rectangles.Count, (long)(DateTime.UtcNow - measurementStartedUtc).TotalMilliseconds);
                        } else {
                            HighlightTrace.Record("measurement_invalid", 0, (long)(DateTime.UtcNow - measurementStartedUtc).TotalMilliseconds);
                            rectangles = new Rectangle[0];
                            HidePaint();
                        }
                    }
                } catch { HighlightTrace.Record("measurement_exception"); measurementCache = null; rectangles = new Rectangle[0]; HidePaint(); }
                finally { pendingMeasurement = null; measurementStalled = false; }
            }
            if (tracking == null || target == IntPtr.Zero) return;
            if (!forceMeasurement && DateTime.UtcNow < nextMeasurementUtc) return;
            forceMeasurement = false;
            nextMeasurementUtc = DateTime.UtcNow.AddMilliseconds(160);
            var request = new TrackingRequest { Handle = target, Info = tracking, Identity = trackingIdentity, AnchorKey = trackingAnchors, Generation = generation };
            TrackingCache prior = measurementCache;
            measurementStartedUtc = DateTime.UtcNow;
            pendingMeasurement = Task.Run(delegate { return Measure(request, prior); });
        }

        private sealed class TrackingRequest {
            public IntPtr Handle;
            public HighlightTrackingInfo Info;
            public string Identity, AnchorKey;
            public int Generation;
        }
        private sealed class TrackingMeasurement {
            public bool Valid;
            public int Generation;
            public string Identity;
            public DateTime MeasuredAtUtc;
            public Rectangle Window;
            public IList<Rectangle> Rectangles = new Rectangle[0];
            public TrackingCache Cache;
        }
        private sealed class TrackingCache {
            public IntPtr Handle;
            public string Identity, AnchorKey;
            public AutomationElement Document, Feed, Article;
            public IList<TextPatternRange> Ranges;
        }

        private static TrackingMeasurement Measure(TrackingRequest request, TrackingCache cache) {
            var result = new TrackingMeasurement { Generation = request.Generation, Identity = request.Identity };
            try {
                if (cache == null || cache.Handle != request.Handle || cache.Identity != request.Identity || cache.AnchorKey != request.AnchorKey) cache = ResolveRanges(request);
                result.Cache = cache;
                if (cache == null || !SameUrl(cache.Document, request.Info.ConversationUrl) || !RuntimeMatches(cache.Article, request.Info.ArticleRuntimeId) || !IsUnder(cache.Article, cache.Feed) || !IsUnder(cache.Feed, cache.Document)) return result;
                using (var dpi = new PhysicalPixelScope()) {
                    if (!dpi.Available || !Native.IsWindow(request.Handle) || !Native.IsWindowVisible(request.Handle) || Native.IsIconic(request.Handle)) return result;
                    Native.RECT windowRect, clientRect;
                    if (!Native.GetWindowRect(request.Handle, out windowRect) || !Native.GetClientRect(request.Handle, out clientRect)) return result;
                    var origin = new Native.POINT();
                    if (!Native.ClientToScreen(request.Handle, ref origin)) return result;
                    Rectangle window = windowRect.ToRectangle();
                    Rectangle client = new Rectangle(origin.X, origin.Y, clientRect.Right - clientRect.Left, clientRect.Bottom - clientRect.Top);
                    Rectangle clip = Rectangle.Intersect(window, client);
                    clip = Rectangle.Intersect(clip, UiaBounds(cache.Document));
                    clip = Rectangle.Intersect(clip, UiaBounds(cache.Feed));
                    if (!HighlightGeometry.IsValid(clip)) return result;
                    var positions = new List<Rectangle>();
                    foreach (TextPatternRange range in cache.Ranges) {
                        System.Windows.Rect[] raw = range.GetBoundingRectangles();
                        foreach (System.Windows.Rect item in raw) {
                            Rectangle visible = BoundsFromNumbers(item.X, item.Y, item.Width, item.Height);
                            if (!HighlightGeometry.IsValid(visible)) continue;
                            visible = Rectangle.Intersect(visible, clip);
                            if (!HighlightGeometry.IsValid(visible)) continue;
                            positions.Add(visible);
                            if (positions.Count > 512) return result;
                        }
                    }
                    Native.RECT after;
                    if (!Native.GetWindowRect(request.Handle, out after) || after.ToRectangle() != window || !SameUrl(cache.Document, request.Info.ConversationUrl) || !IsUnder(cache.Article, cache.Feed)) return result;
                    result.Window = window; result.Rectangles = positions;
                    result.MeasuredAtUtc = DateTime.UtcNow; result.Valid = true;
                    return result;
                }
            } catch { result.Cache = null; return result; }
        }

        private static TrackingCache ResolveRanges(TrackingRequest request) {
            AutomationElement root = AutomationElement.FromHandle(request.Handle);
            AutomationElement document = null;
            foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document))) {
                if (RuntimeMatches(element, request.Info.DocumentRuntimeId) && SameUrl(element, request.Info.ConversationUrl)) { document = element; break; }
            }
            if (document == null) return null;
            AutomationElement feed = document.FindFirst(TreeScope.Descendants, new OrCondition(new PropertyCondition(AutomationElement.NameProperty, "对话消息"), new PropertyCondition(AutomationElement.NameProperty, "Conversation messages")));
            if (feed == null || (request.Info.FeedRuntimeId != null && !RuntimeMatches(feed, request.Info.FeedRuntimeId))) return null;
            object patternObject;
            if (!document.TryGetCurrentPattern(TextPattern.Pattern, out patternObject)) return null;
            TextPattern pattern = (TextPattern)patternObject;
            var wanted = new HashSet<string>();
            foreach (int[] runtime in request.Info.Anchors) wanted.Add(RuntimeKey(runtime));
            var resolved = new Dictionary<string, TextPatternRange>();
            AutomationElement article = null;
            foreach (AutomationElement element in feed.FindAll(TreeScope.Descendants, Condition.TrueCondition)) {
                string runtime;
                try { runtime = RuntimeKey(element.GetRuntimeId()); } catch { continue; }
                if (runtime == RuntimeKey(request.Info.ArticleRuntimeId)) article = element;
                if (!wanted.Contains(runtime)) continue;
                try { resolved[runtime] = pattern.RangeFromChild(element); } catch { }
            }
            if (article == null || !IsUnder(article, feed)) return null;
            var ranges = new List<TextPatternRange>();
            foreach (int[] runtime in request.Info.Anchors) { TextPatternRange range; if (resolved.TryGetValue(RuntimeKey(runtime), out range)) ranges.Add(range); }
            if (ranges.Count == 0) return null;
            return new TrackingCache { Handle = request.Handle, Identity = request.Identity, AnchorKey = request.AnchorKey, Document = document, Feed = feed, Article = article, Ranges = ranges };
        }

        private static bool SameUrl(AutomationElement document, string expected) {
            object value;
            return document != null && document.TryGetCurrentPattern(ValuePattern.Pattern, out value) && String.Equals(((ValuePattern)value).Current.Value, expected, StringComparison.Ordinal);
        }
        private static bool RuntimeMatches(AutomationElement element, int[] expected) { return element != null && RuntimeKey(element.GetRuntimeId()) == RuntimeKey(expected); }
        private static bool IsUnder(AutomationElement element, AutomationElement ancestor) {
            AutomationElement next = element;
            for (int depth = 0; next != null && depth < 40; depth++) {
                if (next.Equals(ancestor)) return true;
                next = TreeWalker.ControlViewWalker.GetParent(next);
            }
            return false;
        }
        private static Rectangle UiaBounds(AutomationElement element) { var bounds = element.Current.BoundingRectangle; return BoundsFromNumbers(bounds.X, bounds.Y, bounds.Width, bounds.Height); }
        private static Rectangle BoundsFromNumbers(double x, double y, double width, double height) {
            if (Double.IsNaN(x) || Double.IsNaN(y) || Double.IsNaN(width) || Double.IsNaN(height) || Double.IsInfinity(x) || Double.IsInfinity(y) || Double.IsInfinity(width) || Double.IsInfinity(height) || Math.Abs(x) > 1000000 || Math.Abs(y) > 1000000 || width <= 0 || height <= 0 || width > 32768 || height > 32768) return Rectangle.Empty;
            return Rectangle.FromLTRB((int)Math.Floor(x), (int)Math.Floor(y), (int)Math.Ceiling(x + width), (int)Math.Ceiling(y + height));
        }
        private static HighlightTrackingInfo CloneTracking(HighlightTrackingInfo info) {
            if (info == null || info.Anchors == null || info.Anchors.Length == 0 || info.Anchors.Length > 512 || !RuntimeValid(info.DocumentRuntimeId) || !RuntimeValid(info.ArticleRuntimeId) || String.IsNullOrEmpty(info.ConversationUrl) || !info.ConversationUrl.StartsWith("https://claude.ai/", StringComparison.OrdinalIgnoreCase)) return null;
            var anchors = new int[info.Anchors.Length][];
            for (int i = 0; i < anchors.Length; i++) { if (!RuntimeValid(info.Anchors[i])) return null; anchors[i] = (int[])info.Anchors[i].Clone(); }
            return new HighlightTrackingInfo { Anchors = anchors, DocumentRuntimeId = (int[])info.DocumentRuntimeId.Clone(), ArticleRuntimeId = (int[])info.ArticleRuntimeId.Clone(), FeedRuntimeId = info.FeedRuntimeId == null ? null : (int[])info.FeedRuntimeId.Clone(), ConversationUrl = info.ConversationUrl };
        }
        private static bool RuntimeValid(int[] values) { return values != null && values.Length > 0 && values.Length <= 32; }
        private static string RuntimeKey(int[] values) {
            if (values == null) return "";
            var builder = new System.Text.StringBuilder(); foreach (int value in values) builder.Append(value).Append(','); return builder.ToString();
        }
        internal static string TrackingIdentity(HighlightTrackingInfo info) { return info == null ? "" : RuntimeKey(info.DocumentRuntimeId) + "|" + RuntimeKey(info.ArticleRuntimeId) + "|" + info.ConversationUrl; }
        private static string AnchorKey(int[][] values) {
            var builder = new System.Text.StringBuilder(); foreach (int[] value in values) builder.Append(RuntimeKey(value)).Append('|'); return builder.ToString();
        }

        private static bool IsCloaked(IntPtr hwnd) {
            try { int value; return Native.DwmGetWindowAttribute(hwnd, 14, out value, sizeof(int)) == 0 && value != 0; }
            catch { return false; }
        }
        private static string ShapeKey(Rectangle window, IList<Rectangle> rects) {
            var value = new System.Text.StringBuilder(window.ToString());
            foreach (Rectangle r in rects) value.Append('|').Append(r.X).Append(',').Append(r.Y).Append(',').Append(r.Width).Append(',').Append(r.Height);
            return value.ToString();
        }
        private void HidePaint() { if (overlay != null) overlay.Hide(); }
        private void UnhookEvents() {
            foreach (IntPtr hook in new[] { locationHook, scrollHook, moveHook }) if (hook != IntPtr.Zero) Native.UnhookWinEvent(hook);
            locationHook = scrollHook = moveHook = IntPtr.Zero;
        }
        public void Dispose() {
            if (disposed) return;
            Hide(); disposed = true; timer.Dispose();
            if (overlay != null) { overlay.Dispose(); overlay = null; }
        }

        // A native HWND on its own STA message-loop thread. Its fixed DPI context never
        // reaches WinForms' main thread or its autoscaling/creation caches.
        private sealed class NativeOverlaySurface : IDisposable {
            private const uint ApplyMessage = 0x8001;
            private readonly object sync = new object();
            private readonly uint ownProcessId;
            private readonly Native.WindowProc windowProc;
            private readonly System.Threading.Thread thread;
            private PaintRequest request;
            private IntPtr hwnd, brush;
            private bool stopped, isShown;
            private string lastShape = "";
            private sealed class PaintRequest {
                public IntPtr Target;
                public uint TargetProcessId;
                public Rectangle Bounds;
                public IList<Rectangle> Rectangles;
                public DateTime MeasuredAtUtc;
            }

            public NativeOverlaySurface(uint processId) {
                ownProcessId = processId; windowProc = WndProc;
                thread = new System.Threading.Thread(Run) { IsBackground = true, Name = "Whale source marker" };
                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.Start();
            }
            public void Update(IntPtr target, uint processId, Rectangle bounds, IList<Rectangle> rects, DateTime measured) {
                IntPtr handle;
                lock (sync) {
                    if (stopped) return;
                    request = new PaintRequest { Target = target, TargetProcessId = processId, Bounds = bounds, Rectangles = new List<Rectangle>(rects), MeasuredAtUtc = measured };
                    handle = hwnd;
                }
                if (handle != IntPtr.Zero) Native.PostMessage(handle, ApplyMessage, IntPtr.Zero, IntPtr.Zero);
            }
            public void Hide() {
                IntPtr handle;
                lock (sync) { request = null; handle = hwnd; }
                if (handle != IntPtr.Zero) Native.PostMessage(handle, ApplyMessage, IntPtr.Zero, IntPtr.Zero);
            }
            public void Dispose() {
                IntPtr handle;
                lock (sync) { if (stopped) return; stopped = true; request = null; handle = hwnd; }
                if (handle != IntPtr.Zero) Native.PostMessage(handle, 0x0010, IntPtr.Zero, IntPtr.Zero);
                // Never block the primary UI waiting for another thread's message queue.
            }

            private void Run() {
                string className = "WhaleSourceMarker_" + Guid.NewGuid().ToString("N");
                IntPtr instance = Native.GetModuleHandle(null);
                ushort atom = 0;
                using (var dpi = new PhysicalPixelScope()) {
                    if (!dpi.Available) { HighlightTrace.Record("surface_dpi_unavailable"); return; }
                    try {
                        brush = Native.CreateSolidBrush(0x0041DCFF);
                        var definition = new Native.WNDCLASS { style = 0x0003, lpfnWndProc = Marshal.GetFunctionPointerForDelegate(windowProc), hInstance = instance, hbrBackground = brush, lpszClassName = className };
                        atom = Native.RegisterClass(ref definition);
                        if (atom == 0) { HighlightTrace.Record("surface_register_failed", Marshal.GetLastWin32Error()); return; }
                        IntPtr created = Native.CreateWindowEx(0x00080000 | 0x00000020 | 0x08000000 | 0x00000080, className, "大肥鱼原文标记", 0x80000000, 0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
                        if (created == IntPtr.Zero) { HighlightTrace.Record("surface_create_failed", Marshal.GetLastWin32Error()); return; }
                        lock (sync) { hwnd = created; }
                        if (!Native.SetLayeredWindowAttributes(created, 0, 61, 0x00000002)) { HighlightTrace.Record("surface_alpha_failed", Marshal.GetLastWin32Error()); return; }
                        Native.SetTimer(created, new UIntPtr(1), 100, IntPtr.Zero);
                        HighlightTrace.Record("surface_created");
                        lock (sync) { if (stopped) Native.PostMessage(created, 0x0010, IntPtr.Zero, IntPtr.Zero); }
                        Native.PostMessage(created, ApplyMessage, IntPtr.Zero, IntPtr.Zero);
                        Native.MSG message;
                        int status;
                        while ((status = Native.GetMessage(out message, IntPtr.Zero, 0, 0)) > 0) {
                            Native.TranslateMessage(ref message); Native.DispatchMessage(ref message);
                        }
                        if (status < 0) HighlightTrace.Record("surface_message_failed", Marshal.GetLastWin32Error());
                    } catch { HighlightTrace.Record("surface_exception"); }
                    finally {
                        IntPtr handle;
                        lock (sync) { handle = hwnd; hwnd = IntPtr.Zero; stopped = true; }
                        if (handle != IntPtr.Zero && Native.IsWindow(handle)) Native.DestroyWindow(handle);
                        if (atom != 0) Native.UnregisterClass(className, instance);
                        if (brush != IntPtr.Zero) Native.DeleteObject(brush);
                        brush = IntPtr.Zero;
                    }
                }
            }
            private IntPtr WndProc(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam) {
                if (message == 0x0084) return new IntPtr(-1); // HTTRANSPARENT
                if (message == 0x0021) return new IntPtr(3); // MA_NOACTIVATE
                if (message == ApplyMessage || message == 0x0113) { Apply(handle); return IntPtr.Zero; }
                if (message == 0x000F) {
                    Native.PAINTSTRUCT paint;
                    IntPtr dc = Native.BeginPaint(handle, out paint);
                    if (dc != IntPtr.Zero && brush != IntPtr.Zero) Native.FillRect(dc, ref paint.rcPaint, brush);
                    Native.EndPaint(handle, ref paint); return IntPtr.Zero;
                }
                if (message == 0x0010) { Native.DestroyWindow(handle); return IntPtr.Zero; }
                if (message == 0x0002) { Native.PostQuitMessage(0); return IntPtr.Zero; }
                return Native.DefWindowProc(handle, message, wParam, lParam);
            }
            private void Apply(IntPtr handle) {
                PaintRequest current;
                lock (sync) { current = stopped ? null : request; }
                if (current == null) { HideWindow(handle); return; }
                if (!HighlightGeometry.IsFresh(current.MeasuredAtUtc, DateTime.UtcNow, DateTime.MinValue, MaxAgeMilliseconds)) { HighlightTrace.Record("surface_stale"); HideWindow(handle); return; }
                uint processId; Native.GetWindowThreadProcessId(current.Target, out processId);
                if (processId != current.TargetProcessId || !Native.IsWindowVisible(current.Target) || Native.IsIconic(current.Target) || IsCloaked(current.Target)) { HighlightTrace.Record("surface_target_hidden"); HideWindow(handle); return; }
                IntPtr foreground = Native.GetForegroundWindow(); uint foregroundProcess;
                Native.GetWindowThreadProcessId(foreground, out foregroundProcess);
                if (!HighlightGeometry.ForegroundAllowed(foreground == current.Target, foregroundProcess == ownProcessId)) { HighlightTrace.Record("surface_other_foreground"); HideWindow(handle); return; }
                Native.RECT targetBounds;
                if (!Native.GetWindowRect(current.Target, out targetBounds) || targetBounds.ToRectangle() != current.Bounds) { HighlightTrace.Record("surface_bounds_changed"); HideWindow(handle); return; }
                var occluders = new List<Rectangle>(); bool found = false;
                Native.EnumWindows(delegate(IntPtr other, IntPtr unused) {
                    if (other == current.Target) { found = true; return false; }
                    if (other == handle || !Native.IsWindowVisible(other) || Native.IsIconic(other) || IsCloaked(other)) return true;
                    Native.RECT value;
                    if (Native.GetWindowRect(other, out value)) {
                        Rectangle bounds = value.ToRectangle();
                        if (HighlightGeometry.IsValid(bounds) && bounds.IntersectsWith(current.Bounds)) occluders.Add(bounds);
                    }
                    return occluders.Count <= 256;
                }, IntPtr.Zero);
                if (!found) { HighlightTrace.Record("surface_zorder_target_missing"); HideWindow(handle); return; }
                IList<Rectangle> visible = HighlightGeometry.VisibleParts(current.Rectangles, current.Bounds, occluders);
                if (visible.Count == 0) { HighlightTrace.Record("surface_occluded", occluders.Count); HideWindow(handle); return; }
                string shape = ShapeKey(current.Bounds, visible);
                if (shape != lastShape) {
                    IntPtr region = Native.CreateRectRgn(0, 0, 0, 0);
                    if (region == IntPtr.Zero) { HighlightTrace.Record("surface_region_failed"); HideWindow(handle); return; }
                    foreach (Rectangle rect in visible) {
                        IntPtr part = Native.CreateRectRgn(rect.Left - current.Bounds.Left, rect.Top - current.Bounds.Top, rect.Right - current.Bounds.Left, rect.Bottom - current.Bounds.Top);
                        if (part != IntPtr.Zero) { Native.CombineRgn(region, region, part, 2); Native.DeleteObject(part); }
                    }
                    if (Native.SetWindowRgn(handle, region, true) == 0) { Native.DeleteObject(region); HighlightTrace.Record("surface_region_apply_failed"); HideWindow(handle); return; }
                    lastShape = shape; // Windows owns the successfully assigned HRGN.
                }
                if (!Native.SetWindowPos(handle, new IntPtr(-1), current.Bounds.X, current.Bounds.Y, current.Bounds.Width, current.Bounds.Height, 0x0010 | 0x0200 | 0x0040)) { HighlightTrace.Record("surface_position_failed", Marshal.GetLastWin32Error()); HideWindow(handle); return; }
                if (!isShown) { isShown = true; HighlightTrace.Record("surface_visible", visible.Count); }
            }
            private void HideWindow(IntPtr handle) { if (isShown) { Native.ShowWindow(handle, 0); isShown = false; HighlightTrace.Record("surface_hidden"); } }
        }

        private sealed class PhysicalPixelScope : IDisposable {
            private readonly IntPtr previous;
            public bool Available { get { return previous != IntPtr.Zero; } }
            public PhysicalPixelScope() { try { previous = Native.SetThreadDpiAwarenessContext(new IntPtr(-4)); } catch { previous = IntPtr.Zero; } }
            public void Dispose() { if (previous != IntPtr.Zero) Native.SetThreadDpiAwarenessContext(previous); }
        }
        private static class Native {
            [StructLayout(LayoutKind.Sequential)] internal struct RECT {
                public int Left, Top, Right, Bottom;
                public Rectangle ToRectangle() { return Rectangle.FromLTRB(Left, Top, Right, Bottom); }
            }
            [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct WNDCLASS {
                public uint style;
                public IntPtr lpfnWndProc;
                public int cbClsExtra, cbWndExtra;
                public IntPtr hInstance, hIcon, hCursor, hbrBackground;
                public string lpszMenuName, lpszClassName;
            }
            [StructLayout(LayoutKind.Sequential)] internal struct MSG {
                public IntPtr hwnd;
                public uint message;
                public IntPtr wParam, lParam;
                public uint time;
                public POINT pt;
                public uint lPrivate;
            }
            [StructLayout(LayoutKind.Sequential)] internal struct PAINTSTRUCT {
                public IntPtr hdc;
                [MarshalAs(UnmanagedType.Bool)] public bool fErase;
                public RECT rcPaint;
                [MarshalAs(UnmanagedType.Bool)] public bool fRestore;
                [MarshalAs(UnmanagedType.Bool)] public bool fIncUpdate;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] reserved;
            }
            internal delegate bool EnumWindowProc(IntPtr hwnd, IntPtr extra);
            internal delegate void WinEventProc(IntPtr hook, uint kind, IntPtr hwnd, int objectId, int childId, uint thread, uint time);
            internal delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
            [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowProc callback, IntPtr extra);
            [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
            [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
            [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);
            [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
            [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
            [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
            [DllImport("user32.dll")] internal static extern bool IsChild(IntPtr parent, IntPtr child);
            [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
            [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
            [DllImport("user32.dll")] internal static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
            [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
            [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);
            [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
            [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string module);
            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern ushort RegisterClass(ref WNDCLASS value);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool UnregisterClass(string name, IntPtr instance);
            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameters);
            [DllImport("user32.dll")] internal static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
            [DllImport("user32.dll")] internal static extern bool DestroyWindow(IntPtr hwnd);
            [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hwnd, int command);
            [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint color, byte alpha, uint flags);
            [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
            [DllImport("user32.dll")] internal static extern void PostQuitMessage(int code);
            [DllImport("user32.dll", SetLastError = true)] internal static extern int GetMessage(out MSG message, IntPtr hwnd, uint first, uint last);
            [DllImport("user32.dll")] internal static extern bool TranslateMessage(ref MSG message);
            [DllImport("user32.dll")] internal static extern IntPtr DispatchMessage(ref MSG message);
            [DllImport("user32.dll")] internal static extern UIntPtr SetTimer(IntPtr hwnd, UIntPtr id, uint milliseconds, IntPtr callback);
            [DllImport("user32.dll")] internal static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT paint);
            [DllImport("user32.dll")] internal static extern bool EndPaint(IntPtr hwnd, ref PAINTSTRUCT paint);
            [DllImport("user32.dll")] internal static extern int FillRect(IntPtr dc, ref RECT rect, IntPtr brush);
            [DllImport("user32.dll")] internal static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
            [DllImport("gdi32.dll")] internal static extern IntPtr CreateSolidBrush(uint color);
            [DllImport("gdi32.dll")] internal static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
            [DllImport("gdi32.dll")] internal static extern int CombineRgn(IntPtr destination, IntPtr first, IntPtr second, int operation);
            [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr value);
        }
    }

    internal static class HighlightTrace {
        private static readonly string path = Environment.GetEnvironmentVariable("WHALE_HIGHLIGHT_TRACE_PATH");
        private static readonly object sync = new object();
        private static readonly Dictionary<string, DateTime> last = new Dictionary<string, DateTime>();
        internal static string Format(string reason, int count, long elapsedMs, DateTime utc) {
            if (!System.Text.RegularExpressions.Regex.IsMatch(reason ?? "", "^[a-z0-9_]{1,48}$")) reason = "invalid_reason";
            return utc.ToString("o", System.Globalization.CultureInfo.InvariantCulture) + " " + reason + " count=" + count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ms=" + elapsedMs.ToString(System.Globalization.CultureInfo.InvariantCulture) + Environment.NewLine;
        }
        internal static void Record(string reason, int count = 0, long elapsedMs = 0) {
            if (String.IsNullOrWhiteSpace(path)) return;
            lock (sync) {
                DateTime now = DateTime.UtcNow, previous;
                if (last.TryGetValue(reason, out previous) && (now - previous).TotalSeconds < 1) return;
                last[reason] = now;
                try { System.IO.File.AppendAllText(path, Format(reason, count, elapsedMs, now), new System.Text.UTF8Encoding(false)); } catch { }
            }
        }
    }

    internal static class HighlightGeometry {
        internal static bool IsValid(Rectangle rect) {
            return rect.Width > 0 && rect.Height > 0 && rect.Width <= 32768 && rect.Height <= 32768 &&
                Math.Abs((long)rect.X) < 2000000 && Math.Abs((long)rect.Y) < 2000000 &&
                Math.Abs((long)rect.X + rect.Width) < 2000000 && Math.Abs((long)rect.Y + rect.Height) < 2000000;
        }
        internal static bool IsFresh(DateTime captured, DateTime now, DateTime invalidated, int maximumAgeMilliseconds) {
            double age = (now - captured).TotalMilliseconds;
            return captured > invalidated && age >= 0 && age <= maximumAgeMilliseconds;
        }
        internal static bool ForegroundAllowed(bool targetIsForeground, bool translatorIsForeground) { return targetIsForeground || translatorIsForeground; }
        internal static IList<Rectangle> VisibleParts(IList<Rectangle> sources, Rectangle window, IList<Rectangle> occluders) {
            var result = new List<Rectangle>();
            if (!IsValid(window) || sources == null || sources.Count > 512 || occluders == null || occluders.Count > 256) return result;
            foreach (Rectangle source in sources) {
                if (!IsValid(source)) continue;
                Rectangle clipped = Rectangle.Intersect(source, window);
                if (clipped.Width <= 0 || clipped.Height <= 0) continue;
                var parts = new List<Rectangle> { clipped };
                foreach (Rectangle obstacle in occluders) {
                    if (!IsValid(obstacle)) continue;
                    var next = new List<Rectangle>();
                    foreach (Rectangle part in parts) Subtract(part, obstacle, next);
                    parts = next;
                    if (parts.Count == 0) break;
                    if (parts.Count + result.Count > 2048) return new List<Rectangle>();
                }
                result.AddRange(parts);
                if (result.Count > 2048) return new List<Rectangle>();
            }
            return result;
        }
        private static void Subtract(Rectangle source, Rectangle obstacle, IList<Rectangle> output) {
            Rectangle cut = Rectangle.Intersect(source, obstacle);
            if (cut.Width <= 0 || cut.Height <= 0) { output.Add(source); return; }
            Add(output, Rectangle.FromLTRB(source.Left, source.Top, source.Right, cut.Top));
            Add(output, Rectangle.FromLTRB(source.Left, cut.Bottom, source.Right, source.Bottom));
            Add(output, Rectangle.FromLTRB(source.Left, cut.Top, cut.Left, cut.Bottom));
            Add(output, Rectangle.FromLTRB(cut.Right, cut.Top, source.Right, cut.Bottom));
        }
        private static void Add(IList<Rectangle> output, Rectangle value) { if (value.Width > 0 && value.Height > 0) output.Add(value); }
    }
}
