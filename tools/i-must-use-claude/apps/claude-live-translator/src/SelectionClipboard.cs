using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClaudeCompanion {
    internal sealed class SelectionClipboardResult {
        public bool Ok;
        public string Status, Text;
        public IntPtr SourceHandle;
        public int Length;
        public uint SequenceBefore, SequenceAfter;
        public bool ClipboardRestored, ClipboardChangedAgain;
        public string RestoreStatus;
        public uint SourceProcessId;
    }

    // Copies only the selection in the already-foreground target window. It never activates,
    // focuses, pastes, or logs clipboard contents. All clipboard APIs run on a private STA thread.
    internal static class SelectionClipboard {
        private const int MaxTextLength = 32000;
        private const int CopyTimeoutMilliseconds = 1800;
        private const int PollMilliseconds = 20;
        private const uint InputKeyboard = 1;
        private const uint KeyUp = 0x0002;
        private const int GwlStyle = -16;
        private const long EsPassword = 0x20;
        private const string CsvFormat = "Csv";
        private const string DibV5Format = "DeviceIndependentBitmapV5";
        private const int MaxSnapshotStringLength = 1000000;
        private const int MaxSnapshotBytes = 1000000;
        private static readonly SemaphoreSlim CaptureGate = new SemaphoreSlim(1, 1);

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);
        [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] private static extern IntPtr GetClipboardOwner();
        [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] private static extern int GetWindowLong32(IntPtr hwnd, int index);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool Process32FirstW(IntPtr snapshot, ref PROCESSENTRY32 entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool Process32NextW(IntPtr snapshot, ref PROCESSENTRY32 entry);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct GUITHREADINFO {
            public int Size; public uint Flags; public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret; public RECT CaretRect;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct PROCESSENTRY32 {
            public uint Size, Usage, ProcessId; public IntPtr DefaultHeap; public uint ModuleId, Threads, ParentProcessId; public int BasePriority; public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExecutableFile;
        }

        [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint Type; public INPUTUNION Union; }
        [StructLayout(LayoutKind.Explicit)] private struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT Mouse; [FieldOffset(0)] public KEYBDINPUT Keyboard; }
        [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT {
            public int Dx, Dy; public uint MouseData, Flags, Time; public UIntPtr ExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT {
            public ushort VirtualKey, ScanCode;
            public uint Flags, Time;
            public UIntPtr ExtraInfo;
        }

        public static Task<SelectionClipboardResult> CaptureAsync(IntPtr targetWindow, CancellationToken cancellationToken) {
            return CaptureAsync(targetWindow, 0, cancellationToken);
        }
        public static Task<SelectionClipboardResult> CaptureAsync(IntPtr targetWindow, uint expectedProcessId, CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) return Task.FromResult(Failure("cancelled", targetWindow));
            var completion = new TaskCompletionSource<SelectionClipboardResult>();
            var thread = new Thread(delegate() {
                SelectionClipboardResult result;
                bool entered = false;
                try {
                    CaptureGate.Wait(cancellationToken); entered = true;
                    result = CaptureOnSta(targetWindow, expectedProcessId, cancellationToken);
                }
                catch (OperationCanceledException) { result = Failure("cancelled", targetWindow); }
                catch { result = Failure("clipboard_unavailable", targetWindow); }
                finally { if (entered) CaptureGate.Release(); }
                completion.TrySetResult(result);
            });
            thread.IsBackground = true;
            thread.Name = "Selection clipboard capture";
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static SelectionClipboardResult CaptureOnSta(IntPtr targetWindow, uint expectedProcessId, CancellationToken cancellationToken) {
            IntPtr foreground = GetForegroundWindow();
            if (targetWindow == IntPtr.Zero) return Failure("invalid_handle", targetWindow);
            string guard = CheckTarget(targetWindow, foreground, ReadModifiersHeld(), ReadMouseButtonDown());
            if (guard != null) return Failure(guard, targetWindow);

            uint targetProcessId;
            if (GetWindowThreadProcessId(targetWindow, out targetProcessId) == 0 || targetProcessId == 0 ||
                CheckTargetProcess(targetProcessId, expectedProcessId) != null) return Failure("window_changed", targetWindow);
            string unsafeStatus = InspectTarget(targetWindow, targetProcessId);
            if (unsafeStatus != null) return Failure(unsafeStatus, targetWindow);
            if (cancellationToken.IsCancellationRequested) return Failure("cancelled", targetWindow);

            uint snapshotSequence = GetClipboardSequenceNumber();
            ClipboardSnapshot previous;
            try { previous = MaterializeClipboardSnapshot(); }
            catch { previous = new ClipboardSnapshot { CanRestore = false, Status = "snapshot_unavailable" }; }
            uint before = GetClipboardSequenceNumber();
            if (before != snapshotSequence) { previous.CanRestore = false; previous.Status = "changed_before_copy"; }
            var result = new SelectionClipboardResult { SourceHandle = targetWindow, SequenceBefore = before, SourceProcessId = targetProcessId, RestoreStatus = previous.Status };
            uint ownedSequence = before;
            bool clipboardChanged = false;
            bool restoreAllowed = false;
            string abortStatus = null;
            try {
                guard = CheckTarget(targetWindow, GetForegroundWindow(), ReadModifiersHeld(), ReadMouseButtonDown());
                if (guard != null) { result.Status = guard; return result; }
                if (!IsExpectedTarget(targetWindow, targetProcessId)) { result.Status = "window_changed"; return result; }
                if (cancellationToken.IsCancellationRequested) { result.Status = "cancelled"; return result; }
                if (!SendCopyChord()) { result.Status = "copy_input_failed"; return result; }

                var timer = Stopwatch.StartNew();
                while (timer.ElapsedMilliseconds < CopyTimeoutMilliseconds) {
                    if (cancellationToken.IsCancellationRequested && abortStatus == null) abortStatus = "cancelled";
                    if ((GetForegroundWindow() != targetWindow || !IsExpectedTarget(targetWindow, targetProcessId)) && abortStatus == null) abortStatus = "foreground_changed";
                    if ((ReadModifiersHeld() || ReadMouseButtonDown()) && abortStatus == null) abortStatus = "input_state_changed";
                    uint current = GetClipboardSequenceNumber();
                    if (current != before) {
                        clipboardChanged = true;
                        ownedSequence = current;
                        result.SequenceAfter = current;
                        if (!IsClipboardOwnedByTarget(targetProcessId)) { result.ClipboardChangedAgain = true; result.RestoreStatus = "skipped_foreign_owner"; result.Status = abortStatus ?? "clipboard_changed"; return result; }
                        restoreAllowed = true;
                        if ((GetForegroundWindow() != targetWindow || !IsExpectedTarget(targetWindow, targetProcessId)) && abortStatus == null) abortStatus = "foreground_changed";
                        if (abortStatus != null) { result.Status = abortStatus; return result; }
                        string text;
                        try {
                            text = Clipboard.ContainsText(TextDataFormat.UnicodeText)
                                ? Clipboard.GetText(TextDataFormat.UnicodeText) : "";
                        } catch { result.Status = "clipboard_unavailable"; return result; }
                        uint afterRead = GetClipboardSequenceNumber();
                        result.SequenceAfter = afterRead;
                        if (afterRead != current) { result.ClipboardChangedAgain = true; result.Status = "clipboard_changed"; return result; }
                        ownedSequence = afterRead;
                        text = NormalizeText(text);
                        result.Length = text.Length;
                        string textStatus = ValidateText(text);
                        if (textStatus != null) { result.Status = textStatus; return result; }
                        if (cancellationToken.IsCancellationRequested) { result.Status = "cancelled"; return result; }
                        result.Text = text;
                        result.Status = "selected";
                        result.Ok = true;
                        return result;
                    }
                    Thread.Sleep(PollMilliseconds);
                }
                result.Status = abortStatus ?? "copy_timeout";
                return result;
            } finally {
                if (clipboardChanged && restoreAllowed) {
                    if (MayRestoreClipboard(targetProcessId, ownedSequence)) {
                        if (!previous.CanRestore) result.RestoreStatus = previous.Status;
                        else {
                            try {
                                RestoreClipboardSnapshot(previous);
                                result.ClipboardRestored = GetClipboardSequenceNumber() != ownedSequence;
                                result.RestoreStatus = result.ClipboardRestored ? "restored" : "restore_failed";
                            } catch { result.RestoreStatus = "restore_failed"; }
                        }
                    } else {
                        result.ClipboardChangedAgain = true;
                        result.RestoreStatus = "skipped_concurrent_change";
                    }
                } else if (clipboardChanged && !restoreAllowed) result.RestoreStatus = "skipped_foreign_owner";
                else if (!clipboardChanged) result.RestoreStatus = previous.CanRestore ? "not_needed" : previous.Status;
                previous.DisposeResources();
            }
        }

        private static string InspectTarget(IntPtr targetWindow, uint processId) {
            try {
                using (Process process = Process.GetProcessById((int)processId)) {
                    string name = process.ProcessName ?? "";
                    if (IsUnsafeProcessName(name)) return "unsafe_target";
                }
            } catch { return "window_not_found"; }
            var className = new StringBuilder(256);
            try { GetClassName(targetWindow, className, className.Capacity); } catch { }
            if (IsUnsafeWindowClass(className.ToString())) return "unsafe_target";
            uint threadId = GetWindowThreadProcessId(targetWindow, out processId);
            var gui = new GUITHREADINFO { Size = Marshal.SizeOf(typeof(GUITHREADINFO)) };
            bool guiAvailable = threadId != 0 && GetGUIThreadInfo(threadId, ref gui);
            string focusedClass = "";
            if (guiAvailable && gui.Focus != IntPtr.Zero) { var focusedClassName = new StringBuilder(128); GetClassName(gui.Focus, focusedClassName, focusedClassName.Capacity); focusedClass = focusedClassName.ToString(); }
            string passwordStatus = CheckPasswordFocus(guiAvailable, gui.Focus != IntPtr.Zero,
                IsNativeEditClass(focusedClass) && (GetWindowStyle(gui.Focus) & EsPassword) != 0);
            if (passwordStatus != null) return passwordStatus;
            return null;
        }
        internal static string CheckPasswordFocus(bool infoAvailable, bool hasFocus, bool isPassword) {
            if (!infoAvailable || !hasFocus) return "focus_unavailable";
            return isPassword ? "password_denied" : null;
        }
        internal static bool IsNativeEditClass(string className) {
            string n = (className ?? "").ToLowerInvariant();
            return n == "edit" || n == "richedit" || n.StartsWith("richedit20", StringComparison.Ordinal) || n.StartsWith("richedit50", StringComparison.Ordinal);
        }
        private static bool IsClipboardOwnedByTarget(uint targetProcessId) {
            IntPtr owner = GetClipboardOwner();
            if (owner == IntPtr.Zero) return false;
            uint ownerProcess;
            return GetWindowThreadProcessId(owner, out ownerProcess) != 0 && ownerProcess != 0 && IsSameOrChildProcess(ownerProcess, targetProcessId);
        }
        private static bool IsExpectedTarget(IntPtr target, uint expectedProcessId) {
            uint currentProcess;
            return target != IntPtr.Zero && expectedProcessId != 0 &&
                GetWindowThreadProcessId(target, out currentProcess) != 0 && currentProcess == expectedProcessId;
        }
        private static long GetWindowStyle(IntPtr hwnd) { return IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, GwlStyle).ToInt64() : GetWindowLong32(hwnd, GwlStyle); }
        private static bool IsSameOrChildProcess(uint processId, uint targetProcessId) {
            if (processId == targetProcessId) return true;
            IntPtr snapshot = CreateToolhelp32Snapshot(2, 0);
            if (snapshot == IntPtr.Zero || snapshot.ToInt64() == -1) return false;
            try {
                var parents = new Dictionary<uint, uint>();
                var entry = new PROCESSENTRY32 { Size = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32)) };
                bool has = Process32FirstW(snapshot, ref entry);
                int count = 0;
                while (has && count++ < 8192) {
                    parents[entry.ProcessId] = entry.ParentProcessId;
                    entry.Size = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                    has = Process32NextW(snapshot, ref entry);
                }
                uint current = processId;
                for (int depth = 0; depth < 64; depth++) {
                    uint parent;
                    if (!parents.TryGetValue(current, out parent) || parent == 0 || parent == current) return false;
                    if (parent == targetProcessId) return true;
                    current = parent;
                }
                return false;
            } finally { CloseHandle(snapshot); }
        }

        internal sealed class ClipboardSnapshot {
            public DataObject Data = new DataObject();
            public bool CanRestore = true, HasData;
            public string Status = "ready";
            public readonly List<IDisposable> OwnedResources = new List<IDisposable>();
            public void DisposeResources() { foreach (IDisposable resource in OwnedResources) { try { resource.Dispose(); } catch { } } OwnedResources.Clear(); }
        }
        private static ClipboardSnapshot MaterializeClipboardSnapshot() {
            var snapshot = new ClipboardSnapshot();
            IDataObject current;
            try { current = Clipboard.GetDataObject(); }
            catch { snapshot.CanRestore = false; snapshot.Status = "snapshot_unavailable"; return snapshot; }
            if (current == null) return snapshot;
            DataObject materialized; List<IDisposable> resources; string status;
            if (!TryMaterializeDataObject(current, out materialized, out resources, out status)) {
                snapshot.CanRestore = false; snapshot.Status = status; snapshot.DisposeResources(); snapshot.OwnedResources.AddRange(resources); return snapshot;
            }
            snapshot.Data = materialized; snapshot.OwnedResources.AddRange(resources);
            try { snapshot.HasData = materialized.GetFormats(false).Length > 0; }
            catch { snapshot.CanRestore = false; snapshot.Status = "snapshot_unavailable"; }
            return snapshot;
        }
        internal static bool TryMaterializeDataObject(IDataObject current, out DataObject materialized, out List<IDisposable> resources, out string status) {
            materialized = new DataObject(); resources = new List<IDisposable>(); status = "ready";
            if (current == null) return true;
            string[] formats;
            try { formats = current.GetFormats(false); }
            catch { status = "snapshot_unavailable"; return false; }
            if (formats == null || formats.Length == 0) return true;
            if (formats.Length > 16) { status = "snapshot_unavailable"; return false; }
            foreach (string format in formats) {
                if (!IsMaterializableFormat(format)) { status = "snapshot_unavailable"; return false; }
                object data;
                try { data = current.GetData(format, false); }
                catch { status = "snapshot_unavailable"; return false; }
                object copy;
                IDisposable resource;
                if (!TryCloneFormatData(format, data, out copy, out resource)) { status = "snapshot_unavailable"; return false; }
                if (resource != null) resources.Add(resource);
                try { materialized.SetData(format, false, copy); }
                catch { status = "snapshot_unavailable"; return false; }
            }
            return true;
        }
        internal static bool IsMaterializableFormat(string format) {
            return format == DataFormats.Text || format == DataFormats.UnicodeText || format == DataFormats.OemText ||
                format == DataFormats.Rtf || format == DataFormats.Html || format == CsvFormat ||
                format == DataFormats.StringFormat ||
                format == DataFormats.FileDrop || format == DataFormats.Locale || format == DataFormats.Bitmap ||
                format == DataFormats.Dib || format == DibV5Format;
        }
        private static bool TryCloneFormatData(string format, object data, out object clone, out IDisposable resource) {
            clone = null; resource = null;
            if (data == null) return false;
            string text = data as string;
            if (text != null && (format == DataFormats.Text || format == DataFormats.UnicodeText || format == DataFormats.OemText || format == DataFormats.Rtf || format == DataFormats.Html || format == CsvFormat || format == DataFormats.StringFormat)) {
                if (text.Length > MaxSnapshotStringLength) return false;
                clone = text; return true;
            }
            string[] files = data as string[];
            if (files != null && format == DataFormats.FileDrop) {
                return TryCloneFiles(files, out clone);
            }
            StringCollection fileList = data as StringCollection;
            if (fileList != null && format == DataFormats.FileDrop) { var paths = new string[fileList.Count]; fileList.CopyTo(paths, 0); return TryCloneFiles(paths, out clone); }
            CultureInfo culture = data as CultureInfo;
            if (culture != null && format == DataFormats.Locale) { clone = CultureInfo.ReadOnly((CultureInfo)culture.Clone()); return true; }
            if (data is int && format == DataFormats.Locale) { clone = data; return true; }
            byte[] bytes = data as byte[];
            if (bytes != null && IsSnapshotByteFormat(format)) { if (bytes.Length > MaxSnapshotBytes) return false; clone = (byte[])bytes.Clone(); return true; }
            Stream stream = data as Stream;
            if (stream != null && IsSnapshotByteFormat(format)) {
                try { using (var copy = new MemoryStream()) { byte[] buffer = new byte[8192]; int read; while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) { if (copy.Length + read > MaxSnapshotBytes) return false; copy.Write(buffer, 0, read); } clone = copy.ToArray(); return true; } } catch { return false; }
            }
            Image image = data as Image;
            if (image != null && format == DataFormats.Bitmap) {
                try { if (image.Width < 1 || image.Height < 1 || image.Width > 8192 || image.Height > 8192 || (long)image.Width * image.Height > 32000000) return false; Bitmap bitmap = new Bitmap(image); clone = bitmap; resource = bitmap; return true; }
                catch { return false; }
            }
            return false;
        }
        private static bool IsSnapshotByteFormat(string format) {
            return format == DataFormats.Text || format == DataFormats.UnicodeText || format == DataFormats.OemText ||
                format == DataFormats.Rtf || format == DataFormats.Html || format == CsvFormat || format == DataFormats.Dib || format == DibV5Format;
        }
        private static bool TryCloneFiles(string[] files, out object clone) {
            clone = null;if(files==null||files.Length>256)return false;int chars=0;
            foreach(string file in files){if(file==null||file.Length>4096)return false;chars+=file.Length;if(chars>MaxSnapshotStringLength)return false;}
            clone=(string[])files.Clone();return true;
        }
        private static void RestoreClipboardSnapshot(ClipboardSnapshot snapshot) {
            if (snapshot.HasData) Clipboard.SetDataObject(snapshot.Data, true);
            else Clipboard.Clear();
        }

        internal static bool IsUnsafeProcessName(string processName) {
            string n = (processName ?? "").ToLowerInvariant();
            string[] exact = { "cmd", "powershell", "pwsh", "windowsterminal", "wt", "conhost", "openconsole", "bash", "wsl", "steam", "steamwebhelper", "overwatch", "starcraft", "sc2_x64", "wow", "minecraft", "roblox", "fortnite", "valorant", "leagueclient", "dota2", "cs2", "eldenring", "gta5" };
            foreach (string item in exact) if (n == item || n == item + ".exe") return true;
            return n.Contains("game") || n.Contains("terminal");
        }
        internal static bool IsUnsafeWindowClass(string className) {
            string n = (className ?? "").ToLowerInvariant();
            return n == "consolewindowclass" || n == "cascadia_hosting_window_class";
        }

        private static bool ReadModifiersHeld() {
            int[] keys = { 0x10, 0x11, 0x12, 0x5B, 0x5C };
            foreach (int key in keys) if ((GetAsyncKeyState(key) & unchecked((short)0x8000)) != 0) return true;
            return false;
        }
        private static bool ReadMouseButtonDown() {
            int[] keys = { 0x01, 0x02, 0x04, 0x05, 0x06 };
            foreach (int key in keys) if ((GetAsyncKeyState(key) & unchecked((short)0x8000)) != 0) return true;
            return false;
        }
        internal static string CheckTarget(IntPtr target, IntPtr foreground, bool modifierHeld, bool mouseDown) {
            if (target == IntPtr.Zero) return "invalid_handle";
            if (target != foreground) return "not_foreground";
            if (modifierHeld) return "modifier_held";
            if (mouseDown) return "mouse_down";
            return null;
        }
        internal static string CheckTargetProcess(uint actualProcessId, uint expectedProcessId) {
            return expectedProcessId != 0 && actualProcessId != expectedProcessId ? "window_changed" : null;
        }
        internal static bool ShouldRestore(uint currentSequence, uint capturedSequence) { return currentSequence == capturedSequence; }
        private static bool MayRestoreClipboard(uint targetProcessId, uint capturedSequence) {
            uint first = GetClipboardSequenceNumber();
            if (!ShouldRestore(first, capturedSequence) || !IsClipboardOwnedByTarget(targetProcessId)) return false;
            return ShouldRestore(GetClipboardSequenceNumber(), capturedSequence);
        }
        internal static string NormalizeText(string text) { return (text ?? "").Replace("\u0000", "").Trim(); }
        internal static int InputStructureSize { get { return Marshal.SizeOf(typeof(INPUT)); } }
        internal static string ValidateText(string text) {
            if (String.IsNullOrWhiteSpace(text)) return "no_text";
            return text.Length > MaxTextLength ? "text_too_long" : null;
        }

        private static bool SendCopyChord() {
            var input = new[] {
                Key(0x11, false), Key(0x43, false), Key(0x43, true), Key(0x11, true)
            };
            uint sent = SendInput((uint)input.Length, input, Marshal.SizeOf(typeof(INPUT)));
            if (sent == input.Length) return true;
            var release = new List<INPUT>();
            if (sent == 2 && IsKeyDown(0x43)) release.Add(Key(0x43, true));
            if (sent >= 1 && IsKeyDown(0x11)) release.Add(Key(0x11, true));
            if (release.Count > 0) SendInput((uint)release.Count, release.ToArray(), Marshal.SizeOf(typeof(INPUT)));
            return false;
        }
        private static bool IsKeyDown(int key) { return (GetAsyncKeyState(key) & unchecked((short)0x8000)) != 0; }
        private static INPUT Key(ushort key, bool up) {
            return new INPUT { Type = InputKeyboard, Union = new INPUTUNION { Keyboard = new KEYBDINPUT { VirtualKey = key, Flags = up ? KeyUp : 0 } } };
        }
        private static SelectionClipboardResult Failure(string status, IntPtr handle) {
            return new SelectionClipboardResult { Ok = false, Status = status, Text = "", SourceHandle = handle };
        }
        private static void ReleaseComObject(object value) { if (value != null && Marshal.IsComObject(value)) { try { Marshal.ReleaseComObject(value); } catch { } } }
    }
}
