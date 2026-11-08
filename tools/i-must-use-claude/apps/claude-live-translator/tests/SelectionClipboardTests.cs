using System;
using System.Collections.Generic;
using System.Drawing;
using ClaudeCompanion;
using System.Windows.Forms;

// Pure guard and clipboard-transaction tests; no desktop, clipboard, or input is accessed.
internal static class SelectionClipboardTests {
    private static void Equal(string expected, string actual, string label) {
        if (!String.Equals(expected, actual, StringComparison.Ordinal))
            throw new Exception(label + " expected <" + expected + "> actual <" + actual + ">");
    }
    private static void True(bool value, string label) { if (!value) throw new Exception(label); }

    public static int Main() {
        try {
            Equal("invalid_handle", SelectionClipboard.CheckTarget(IntPtr.Zero, new IntPtr(1), false, false), "zero target rejected");
            Equal("not_foreground", SelectionClipboard.CheckTarget(new IntPtr(1), new IntPtr(2), false, false), "foreground switch rejected");
            Equal("modifier_held", SelectionClipboard.CheckTarget(new IntPtr(1), new IntPtr(1), true, false), "held modifier rejected");
            Equal("mouse_down", SelectionClipboard.CheckTarget(new IntPtr(1), new IntPtr(1), false, true), "held mouse button rejected");
            Equal(null, SelectionClipboard.CheckTarget(new IntPtr(1), new IntPtr(1), false, false), "eligible target accepted");
            Equal(null, SelectionClipboard.CheckTargetProcess(55, 55), "locked target PID accepted");
            Equal("window_changed", SelectionClipboard.CheckTargetProcess(56, 55), "reused window handle with different PID rejected");
            Equal(IntPtr.Size == 8 ? "40" : "28", SelectionClipboard.InputStructureSize.ToString(), "INPUT union includes keyboard and mouse structures");

            True(SelectionClipboard.IsUnsafeProcessName("powershell"), "PowerShell is blocked");
            True(SelectionClipboard.IsUnsafeProcessName("WindowsTerminal"), "Windows Terminal is blocked");
            True(SelectionClipboard.IsUnsafeProcessName("StarCraft"), "game process is blocked");
            True(SelectionClipboard.IsUnsafeWindowClass("ConsoleWindowClass"), "console window is blocked");
            True(!SelectionClipboard.IsUnsafeProcessName("claude"), "Claude remains eligible");
            True(!SelectionClipboard.IsUnsafeProcessName("chrome"), "browser remains eligible");
            Equal("focus_unavailable", SelectionClipboard.CheckPasswordFocus(false, false, false), "unknown native focused control rejected");
            Equal("password_denied", SelectionClipboard.CheckPasswordFocus(true, true, true), "native password input rejected");
            Equal(null, SelectionClipboard.CheckPasswordFocus(true, true, false), "regular focused control accepted");
            True(SelectionClipboard.IsMaterializableFormat("UnicodeText"), "Unicode text snapshot supported");
            True(SelectionClipboard.IsMaterializableFormat("FileDrop"), "file list snapshot supported");
            True(!SelectionClipboard.IsMaterializableFormat("VendorPrivateFormat"), "unknown lazy clipboard formats are not restored");
            DataObject oldText = new DataObject();
            oldText.SetData(DataFormats.UnicodeText, false, "previous clipboard text");
            oldText.SetData(DataFormats.Rtf, false, "{\\rtf1 previous}");
            DataObject copiedText; List<IDisposable> copiedResources; string snapshotStatus;
            True(SelectionClipboard.TryMaterializeDataObject(oldText, out copiedText, out copiedResources, out snapshotStatus), "standard text formats materialize from memory");
            Equal("previous clipboard text", (string)copiedText.GetData(DataFormats.UnicodeText, false), "UnicodeText snapshot stays independent");
            Equal("{\\rtf1 previous}", (string)copiedText.GetData(DataFormats.Rtf, false), "RTF snapshot stays independent");
            foreach (IDisposable disposable in copiedResources) disposable.Dispose();

            var oldBitmap = new Bitmap(2, 2);
            DataObject bitmapData = new DataObject(); bitmapData.SetData(DataFormats.Bitmap, false, oldBitmap);
            DataObject bitmapCopy; List<IDisposable> bitmapResources;
            True(SelectionClipboard.TryMaterializeDataObject(bitmapData, out bitmapCopy, out bitmapResources, out snapshotStatus), "bitmap snapshot materializes in memory");
            object clonedBitmap = bitmapCopy.GetData(DataFormats.Bitmap, false);
            True(clonedBitmap is Image && !Object.ReferenceEquals(oldBitmap, clonedBitmap), "bitmap is cloned rather than lazily referenced");
            foreach (IDisposable disposable in bitmapResources) disposable.Dispose();
            oldBitmap.Dispose();

            DataObject privateFormat = new DataObject(); privateFormat.SetData("VendorPrivateFormat", false, "opaque");
            DataObject ignoredCopy; List<IDisposable> ignoredResources;
            True(!SelectionClipboard.TryMaterializeDataObject(privateFormat, out ignoredCopy, out ignoredResources, out snapshotStatus), "unknown clipboard format opts out of restoration");
            Equal("snapshot_unavailable", snapshotStatus, "unsupported snapshot reports metadata status");
            foreach (IDisposable disposable in ignoredResources) disposable.Dispose();

            True(SelectionClipboard.ShouldRestore(42, 42), "clipboard restore allowed only for captured sequence");
            True(!SelectionClipboard.ShouldRestore(43, 42), "newer clipboard sequence must not be overwritten");
            Equal("same text", SelectionClipboard.NormalizeText("  same text\0  "), "copy succeeds based on new sequence, not changed text content");
            Equal("no_text", SelectionClipboard.ValidateText(" \r\n "), "empty selection rejected");
            Equal(null, SelectionClipboard.ValidateText("selected words"), "selection text accepted");
            Equal("text_too_long", SelectionClipboard.ValidateText(new string('x', 32001)), "oversized selection rejected");
            Equal(null, SelectionClipboard.ValidateText(new string('x', 32000)), "maximum selection length accepted");

            Console.WriteLine("SelectionClipboardTests passed");
            return 0;
        } catch (Exception e) {
            Console.WriteLine("SelectionClipboardTests failed: " + e.Message);
            return 1;
        }
    }
}
