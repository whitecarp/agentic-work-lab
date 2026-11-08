using System;
using System.Drawing;
using ClaudeCompanion;

internal static class SelectionWatcherTests {
    private static readonly IntPtr Source = new IntPtr(101), Other = new IntPtr(102);
    private const uint SourceProcess = 1001, OwnProcess = 2001;
    private static void Check(bool result, string label) { if (!result) throw new Exception(label); }
    private static IntPtr Read(SelectionGestureState state, bool down, int x, int y, long time) {
        return state.Observe(down, new Point(x, y), Source, SourceProcess, OwnProcess, time);
    }
    private static SelectionGestureState Ready() {
        var state = new SelectionGestureState(); Read(state, false, 0, 0, 0); return state;
    }
    private static SelectionGestureState Pending() {
        var state = Ready();
        Read(state, true, 100, 100, 50); Read(state, true, 140, 100, 100); Read(state, false, 140, 100, 150);
        return state;
    }
    public static int Main() {
        var state = Pending();
        Check(Read(state, false, 140, 100, 269) == IntPtr.Zero, "wait for the selection to settle");
        Check(Read(state, false, 140, 100, 270) == Source, "emit at 120 ms after release");
        Check(state.ReleasePoint == new Point(140, 100), "the emitted point is the physical release position");
        Check(Read(state, false, 140, 100, 400) == IntPtr.Zero, "one event per drag");
        Check(state.ReleasePoint == Point.Empty, "release point is not retained on later samples");

        state = Pending();
        Check(Read(state, false, 900, 700, 200) == IntPtr.Zero && state.ReleasePoint == Point.Empty, "pending point is not exposed before release delay finishes");
        Check(Read(state, false, 1000, 800, 300) == Source && state.ReleasePoint == new Point(140, 100),
            "mouse movement during debounce cannot replace the release point");

        state = Ready();
        Read(state, true, 100, 100, 50); Read(state, false, 102, 102, 100);
        Check(Read(state, false, 102, 102, 250) == IntPtr.Zero, "a normal click and minor jitter are ignored");
        Read(state, true, -100, -100, 300); Read(state, false, -96, -100, 350);
        Check(Read(state, false, -96, -100, 500) == Source, "release position detects a short drag on a negative-origin monitor");

        state = Ready();
        Read(state, true, 400, 200, 50);
        for (long tick = 100; tick <= 4100; tick += 50)
            Read(state, true, 401, 520, tick);
        Read(state, false, 401, 520, 4150);
        Check(Read(state, false, 401, 520, 4300) == Source, "long vertical multi-line drag still emits after release");

        state = Ready();
        state.Observe(true, new Point(100, 100), Other, SourceProcess + 1, OwnProcess, 50);
        state.Observe(false, new Point(100, 300), Other, SourceProcess + 1, OwnProcess, 100);
        Check(state.Observe(false, new Point(100, 300), Other, SourceProcess + 1, OwnProcess, 250) == Other,
            "any distinct external process is accepted without app identity or brand filtering");

        state = Pending();
        Read(state, true, 140, 100, 200);
        Check(state.ReleasePoint == Point.Empty, "new press clears the prior release point");
        Check(Read(state, true, 160, 100, 300) == IntPtr.Zero, "second drag cancels the first delayed event");
        Read(state, false, 160, 100, 350);
        Check(Read(state, false, 160, 100, 470) == Source, "second drag receives its own delay");
        Check(state.ReleasePoint == new Point(160, 100), "second drag receives only its own point");

        state = Pending();
        state.Observe(false, new Point(140, 100), Other, SourceProcess, OwnProcess, 200);
        Check(Read(state, false, 140, 100, 300) == IntPtr.Zero, "switching windows cancels pending work even in the same process");
        Check(state.ReleasePoint == Point.Empty, "cancelled window cannot expose its pending point");
        state = Pending();
        state.Observe(false, new Point(140, 100), Source, OwnProcess, OwnProcess, 200);
        Check(Read(state, false, 140, 100, 300) == IntPtr.Zero, "the translator itself cannot trigger delayed selection work");

        state = Ready();
        state.Observe(true, new Point(0, 0), Source, OwnProcess, OwnProcess, 50);
        state.Observe(false, new Point(30, 0), Source, OwnProcess, OwnProcess, 100);
        Check(Read(state, false, 30, 0, 250) == IntPtr.Zero, "drag started in own process is excluded");
        Read(state, true, 0, 0, 300);
        state.Observe(true, new Point(30, 0), Other, SourceProcess, OwnProcess, 350);
        Read(state, false, 40, 0, 400);
        Check(Read(state, false, 40, 0, 550) == IntPtr.Zero, "leaving then returning to a window invalidates the gesture");

        state = Ready();
        Read(state, true, 100, 100, 50);
        state.Observe(true, new Point(100, 300), Source, SourceProcess + 1, OwnProcess, 100);
        state.Observe(false, new Point(100, 300), Source, SourceProcess + 1, OwnProcess, 150);
        Check(state.Observe(false, new Point(100, 300), Source, SourceProcess + 1, OwnProcess, 300) == IntPtr.Zero,
            "reused window with a changed PID cannot complete an old gesture");

        state = new SelectionGestureState();
        Read(state, true, 0, 0, 0); Read(state, false, 40, 0, 50);
        Check(Read(state, false, 40, 0, 200) == IntPtr.Zero, "starting while the button is held never translates an incomplete gesture");
        state = Pending(); state.Reset();
        Check(Read(state, false, 140, 100, 300) == IntPtr.Zero, "pause or reset clears all pending work");
        state = Pending();
        Check(Read(state, false, 140, 100, 2000) == IntPtr.Zero, "a blocked UI loop cannot release stale selection work");
        state = Pending();
        Check(Read(state, false, 140, 100, 100) == IntPtr.Zero, "backward time invalidates pending work");
        state = Ready();
        Read(state, true, Int32.MinValue, 0, 50); Read(state, false, Int32.MaxValue, 0, 100);
        Check(Read(state, false, 0, 0, 250) == Source, "large coordinate differences do not overflow the distance check");
        Console.WriteLine("SelectionWatcherTests passed (drag, debounce, window/process guards and lifecycle; no desktop access).");
        return 0;
    }
}
