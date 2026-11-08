using System;
using System.Collections.Generic;
using System.Drawing;
using ClaudeCompanion;

internal static class HighlightTests {
    private static void Check(bool result, string label) { if (!result) throw new Exception(label); }
    private static long Area(IList<Rectangle> rects) { long total = 0; foreach (Rectangle rect in rects) total += (long)rect.Width * rect.Height; return total; }
    public static int Main() {
        var window = new Rectangle(-100, 100, 500, 400);
        var line = new Rectangle(-50, 150, 300, 30);
        Check(HighlightGeometry.IsValid(window), "negative monitor origins are valid");
        Check(!HighlightGeometry.IsValid(new Rectangle(0, 0, 0, 30)), "empty text rect rejected");
        Check(!HighlightGeometry.IsValid(new Rectangle(Int32.MaxValue, 0, 300, 30)), "overflow coordinates rejected");
        var untouched = HighlightGeometry.VisibleParts(new[] { line }, window, new Rectangle[0]);
        Check(untouched.Count == 1 && untouched[0] == line, "unoccluded text retained");
        var clipped = HighlightGeometry.VisibleParts(new[] { new Rectangle(-200, 90, 300, 40) }, window, new Rectangle[0]);
        Check(clipped.Count == 1 && clipped[0] == new Rectangle(-100, 100, 200, 30), "clip to physical target window");
        var obstacle = new Rectangle(50, 140, 100, 60);
        var split = HighlightGeometry.VisibleParts(new[] { line }, window, new[] { obstacle });
        Check(split.Count == 2 && Area(split) == 6000, "foreground overlay splits the marked line");
        foreach (Rectangle rect in split) Check(!rect.IntersectsWith(obstacle), "no yellow over foreground window");
        var allHidden = HighlightGeometry.VisibleParts(new[] { line }, window, new[] { window });
        Check(allHidden.Count == 0, "fully occluded line hides");
        var center = HighlightGeometry.VisibleParts(new[] { new Rectangle(0, 100, 100, 100) }, window, new[] { new Rectangle(25, 125, 50, 50) });
        Check(center.Count == 4 && Area(center) == 7500, "rectangle subtraction does not duplicate corner areas");
        DateTime now = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        Check(HighlightGeometry.IsFresh(now.AddMilliseconds(-1000), now, DateTime.MinValue, 2500), "fresh coordinates accepted");
        Check(!HighlightGeometry.IsFresh(now.AddMilliseconds(-3000), now, DateTime.MinValue, 2500), "expired snapshot hidden");
        Check(!HighlightGeometry.IsFresh(now.AddMilliseconds(-500), now, now.AddMilliseconds(-100), 2500), "scroll during read invalidates coordinates");
        Check(!HighlightGeometry.IsFresh(now.AddMilliseconds(500), now, DateTime.MinValue, 2500), "clock reversal fails closed");
        Check(HighlightGeometry.ForegroundAllowed(true, false), "Claude foreground allowed");
        Check(HighlightGeometry.ForegroundAllowed(false, true), "translator foreground allowed with occlusion clipping");
        Check(!HighlightGeometry.ForegroundAllowed(false, false), "unrelated foreground hides marker");
        var tracked = new HighlightTrackingInfo { DocumentRuntimeId = new[] { 1, 10 }, ArticleRuntimeId = new[] { 1, 20 }, Anchors = new[] { new[] { 1, 30 } }, ConversationUrl = "https://claude.ai/chat/test" };
        var grown = new HighlightTrackingInfo { DocumentRuntimeId = new[] { 1, 10 }, ArticleRuntimeId = new[] { 1, 20 }, Anchors = new[] { new[] { 1, 30 }, new[] { 1, 31 } }, ConversationUrl = "https://claude.ai/chat/test" };
        var otherReply = new HighlightTrackingInfo { DocumentRuntimeId = new[] { 1, 10 }, ArticleRuntimeId = new[] { 1, 21 }, Anchors = new[] { new[] { 1, 30 } }, ConversationUrl = "https://claude.ai/chat/test" };
        var otherChat = new HighlightTrackingInfo { DocumentRuntimeId = new[] { 1, 10 }, ArticleRuntimeId = new[] { 1, 20 }, Anchors = new[] { new[] { 1, 30 } }, ConversationUrl = "https://claude.ai/chat/other" };
        Check(SourceHighlighter.TrackingIdentity(tracked) == SourceHighlighter.TrackingIdentity(grown), "same reply growth keeps marker identity");
        Check(SourceHighlighter.TrackingIdentity(tracked) != SourceHighlighter.TrackingIdentity(otherReply), "another visible reply invalidates marker identity");
        Check(SourceHighlighter.TrackingIdentity(tracked) != SourceHighlighter.TrackingIdentity(otherChat), "different chat URL invalidates marker even with recycled runtime IDs");
        string trace = HighlightTrace.Format("measured", 12, 20, now);
        Check(trace.Contains("measured count=12 ms=20"), "opt-in trace contains only reason and numbers");
        Check(!HighlightTrace.Format("https://private.example/chat", 0, 0, now).Contains("private"), "trace rejects accidental URLs or text");
        Console.WriteLine("HighlightTests passed (geometry, occlusion, freshness and foreground guards; no desktop access).");
        return 0;
    }
}
