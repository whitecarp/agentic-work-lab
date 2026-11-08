using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ClaudeCompanion {
    internal sealed class SourceWindow {
        public IntPtr Handle;
        public int ProcessId;
        public long Started;
        public string Title, App;
        public string FriendlyAppName { get {
            switch((App??"").ToLowerInvariant()) {
                case "chrome":return "Google Chrome";
                case "msedge":return "Microsoft Edge";
                case "firefox":return "Firefox";
                case "claude":return "Claude";
                case "codex":return "Codex";
                case "code":return "Visual Studio Code";
                case "devenv":return "Visual Studio";
                case "notepad":return Ui.T("记事本","Notepad");
                case "notepad++":return "Notepad++";
                case "explorer":return Ui.T("文件资源管理器","File Explorer");
                case "winword":return "Microsoft Word";
                case "excel":return "Microsoft Excel";
                case "powerpnt":return "Microsoft PowerPoint";
                case "acrord32":case "acrobat":return "Adobe Acrobat";
                default:return String.IsNullOrWhiteSpace(App)?Ui.T("应用窗口","App window"):App;
            }
        } }
        public Rectangle Bounds { get {
            RECT rect;
            try { if(DwmGetWindowAttribute(Handle,9,out rect,Marshal.SizeOf(typeof(RECT)))==0)return Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom); } catch { }
            return GetWindowRect(Handle,out rect)?Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom):Rectangle.Empty;
        } }
        public static SourceWindow FromHandle(IntPtr handle) {
            handle=GetAncestor(handle,2);
            if(!IsSelectableSurface(handle))return null;
            uint pid;GetWindowThreadProcessId(handle,out pid);
            if(pid==0||pid==(uint)Process.GetCurrentProcess().Id)return null;
            var title=new StringBuilder(512);GetWindowText(handle,title,title.Capacity);
            if(title.Length==0)return null;
            try { using(var process=Process.GetProcessById((int)pid)) {
                if(SelectionClipboard.IsUnsafeProcessName(process.ProcessName))return null;
                return new SourceWindow{Handle=handle,ProcessId=(int)pid,Started=process.StartTime.ToUniversalTime().Ticks,Title=title.ToString(),App=process.ProcessName};
            }}catch{return null;}
        }
        public override string ToString() { return App + " · " + Title; }
        internal bool Matches(IntPtr handle,int pid,long started) {
            return Handle!=IntPtr.Zero && Handle==handle && ProcessId==pid && Started==started;
        }
        public bool IsAlive() {
            uint pid;
            if(!IsWindow(Handle)||GetWindowThreadProcessId(Handle,out pid)==0||pid!=(uint)ProcessId)return false;
            try { using(var process=Process.GetProcessById(ProcessId))return Matches(Handle,ProcessId,process.StartTime.ToUniversalTime().Ticks); } catch { return false; }
        }
        public static List<SourceWindow> List() {
            return List(CancellationToken.None);
        }
        public static List<SourceWindow> List(CancellationToken cancellationToken) {
            var list=new List<SourceWindow>();
            int own=Process.GetCurrentProcess().Id;
            EnumWindows(delegate(IntPtr handle,IntPtr unused) {
                if(cancellationToken.IsCancellationRequested)return false;
                if(!IsSelectableSurface(handle))return true;
                uint pid;GetWindowThreadProcessId(handle,out pid);
                if(pid==0||pid==own)return true;
                var title=new StringBuilder(512);GetWindowText(handle,title,title.Capacity);
                if(title.Length==0)return true;
                try { using(var process=Process.GetProcessById((int)pid)) {
                    if(SelectionClipboard.IsUnsafeProcessName(process.ProcessName))return true;
                    list.Add(new SourceWindow {Handle=handle,ProcessId=(int)pid,Started=process.StartTime.ToUniversalTime().Ticks,Title=title.ToString(),App=process.ProcessName});
                }} catch { }
                return true;
            },IntPtr.Zero);
            cancellationToken.ThrowIfCancellationRequested();
            list.Sort(delegate(SourceWindow a,SourceWindow b) { return String.Compare(a.ToString(),b.ToString(),StringComparison.CurrentCultureIgnoreCase); });
            return list;
        }
        // Window icons are queried off the UI thread. A hung application gets at most 40 ms;
        // borrowed native icons are copied before conversion and are never destroyed here.
        public Bitmap GetSmallIcon() {
            if(!IsAlive())return null;
            IntPtr borrowed=IntPtr.Zero,owned=IntPtr.Zero;
            try {
                IntPtr answer;
                if(SendMessageTimeout(Handle,0x007f,new IntPtr(2),IntPtr.Zero,0x0003,40,out answer)!=IntPtr.Zero)borrowed=answer;
                if(borrowed==IntPtr.Zero)borrowed=ClassIcon(Handle,-34);
                if(borrowed==IntPtr.Zero)borrowed=ClassIcon(Handle,-14);
                if(borrowed==IntPtr.Zero)return null;
                owned=CopyIcon(borrowed);
                if(owned==IntPtr.Zero)return null;
                using(var icon=Icon.FromHandle(owned))return icon.ToBitmap();
            } catch { return null; }
            finally { if(owned!=IntPtr.Zero)DestroyIcon(owned); }
        }
        private static IntPtr ClassIcon(IntPtr window,int index) {
            return IntPtr.Size==8?GetClassLongPtr64(window,index):new IntPtr(unchecked((int)GetClassLong32(window,index)));
        }
        private static bool IsSelectableSurface(IntPtr handle) {
            if(handle==IntPtr.Zero||!IsWindowVisible(handle)||handle==GetShellWindow())return false;
            var name=new StringBuilder(128);GetClassName(handle,name,name.Capacity);
            switch(name.ToString()) {
                case "Progman":case "WorkerW":case "Shell_TrayWnd":case "Shell_SecondaryTrayWnd":case "MultitaskingViewFrame":return false;
            }
            int cloaked;
            try { if(DwmGetWindowAttributeInt(handle,14,out cloaked,sizeof(int))==0&&cloaked!=0)return false; }catch{}
            // Minimized app windows remain valid choices; the user can restore them normally.
            return true;
        }
        private delegate bool EnumProc(IntPtr window,IntPtr parameter);
        [StructLayout(LayoutKind.Sequential)]private struct RECT{public int Left,Top,Right,Bottom;}
        [DllImport("dwmapi.dll")]private static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out RECT value,int size);
        [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")]private static extern int DwmGetWindowAttributeInt(IntPtr window,int attribute,out int value,int size);
        [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr window,out RECT rect);
        [DllImport("user32.dll")]private static extern IntPtr GetAncestor(IntPtr window,uint flags);
        [DllImport("user32.dll")]private static extern IntPtr GetShellWindow();
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(IntPtr window,StringBuilder text,int count);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wParam,IntPtr lParam,uint flags,uint timeout,out IntPtr result);
        [DllImport("user32.dll",EntryPoint="GetClassLongPtrW")]private static extern IntPtr GetClassLongPtr64(IntPtr window,int index);
        [DllImport("user32.dll",EntryPoint="GetClassLongW")]private static extern uint GetClassLong32(IntPtr window,int index);
        [DllImport("user32.dll")]private static extern IntPtr CopyIcon(IntPtr icon);
        [DllImport("user32.dll")]private static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")]private static extern bool EnumWindows(EnumProc callback,IntPtr parameter);
        [DllImport("user32.dll")]private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")]private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
    }
}
