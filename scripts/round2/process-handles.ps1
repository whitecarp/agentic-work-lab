$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class OwnedProcesses {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct PE {public uint size,usage,pid;public IntPtr heap;public uint module,threads,parent;public int priority;public uint flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string name;}
 [DllImport("kernel32.dll")] static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint pid);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern bool Process32FirstW(IntPtr h,ref PE p);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern bool Process32NextW(IntPtr h,ref PE p);
 [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
 [DllImport("kernel32.dll")] static extern bool GetProcessTimes(IntPtr h,out long born,out long exit,out long kernel,out long user);
 [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr h,uint code);
 [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h,uint ms);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 public class Row {public uint pid,parent;public string born,image;public bool ended;internal IntPtr handle;}
 Dictionary<uint,Row> held=new Dictionary<uint,Row>();
 static Row Hold(PE p){var h=OpenProcess(0x101001,false,p.pid);if(h==IntPtr.Zero)return null;long b,e,k,u;if(!GetProcessTimes(h,out b,out e,out k,out u)){CloseHandle(h);return null;}return new Row{pid=p.pid,parent=p.parent,born=b.ToString(),image=p.name,handle=h};}
 static List<PE> Snapshot(){var h=CreateToolhelp32Snapshot(2,0);if(h==new IntPtr(-1))throw new Exception("process_snapshot_failed");var list=new List<PE>();try{var p=new PE();p.size=(uint)Marshal.SizeOf(p);if(Process32FirstW(h,ref p))do{list.Add(p);}while(Process32NextW(h,ref p));}finally{CloseHandle(h);}return list;}
 public void Init(uint root,uint parent,long earliest){foreach(var p in Snapshot())if(p.pid==root){var r=Hold(p);if(r==null||r.parent!=parent||long.Parse(r.born)<earliest){if(r!=null)CloseHandle(r.handle);throw new Exception("starter_identity_missing");}held.Add(root,r);return;}throw new Exception("starter_missing");}
 public Row[] Scan(){var pending=Snapshot();bool added=true;while(added){added=false;foreach(var p in pending){if(held.ContainsKey(p.pid)||!held.ContainsKey(p.parent))continue;var parent=held[p.parent];if(WaitForSingleObject(parent.handle,0)!=258)continue;var r=Hold(p);if(r==null)continue;if(long.Parse(r.born)<long.Parse(parent.born)){CloseHandle(r.handle);continue;}held.Add(r.pid,r);added=true;}}foreach(var r in held.Values)r.ended=WaitForSingleObject(r.handle,0)!=258;var rows=new List<Row>(held.Values);return rows.ToArray();}
 public Row[] End(){Scan();var rows=new List<Row>(held.Values);rows.Sort((a,b)=>long.Parse(b.born).CompareTo(long.Parse(a.born)));foreach(var r in rows){long b,e,k,u;if(!GetProcessTimes(r.handle,out b,out e,out k,out u)||b.ToString()!=r.born)throw new Exception("process_identity_changed");if(WaitForSingleObject(r.handle,0)==258){TerminateProcess(r.handle,1); /* A child may exit between wait and termination. Keep closing the other owned handles and judge final observations. */}}foreach(var r in rows)WaitForSingleObject(r.handle,3000);return Scan();}
 public void Close(){foreach(var r in held.Values)CloseHandle(r.handle);held.Clear();}
}
'@
$owned = New-Object OwnedProcesses
try {
 while ($null -ne ($line = [Console]::ReadLine())) {
  try {
   $request = $line | ConvertFrom-Json
   switch ($request.op) {
    'init' { $owned.Init([uint32]$request.root,[uint32]$request.parent,[long]$request.earliest); $rows=@($owned.Scan()) }
    'scan' { $rows=@($owned.Scan()) }
    'end' { $rows=@($owned.End()) }
    'close' { break }
    default { throw 'unknown_process_operation' }
   }
   if ($request.op -eq 'close') { break }
   @{id=$request.id;ok=$true;rows=$rows} | ConvertTo-Json -Compress -Depth 5 | ForEach-Object { [Console]::WriteLine($_) }
  } catch {
   $reason='process_controller_failed'
   foreach ($word in @('starter_missing','starter_identity_missing','process_snapshot_failed','process_identity_changed','terminate_handle_failed')) {
    if ($_.Exception.Message.Contains($word)) { $reason=$word }
   }
   @{id=$request.id;ok=$false;reason=$reason} | ConvertTo-Json -Compress | ForEach-Object { [Console]::WriteLine($_) }
  }
 }
} finally { $owned.Close() }
