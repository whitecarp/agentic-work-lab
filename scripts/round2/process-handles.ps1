$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class OwnedProcesses {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct PE {public uint size,usage,pid;public IntPtr heap;public uint module,threads,parent;public int priority;public uint flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string name;}
 [DllImport("kernel32.dll")] static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint pid);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool Process32FirstW(IntPtr h,ref PE p);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool Process32NextW(IntPtr h,ref PE p);
 [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
 [DllImport("kernel32.dll")] static extern bool GetProcessTimes(IntPtr h,out long born,out long exit,out long kernel,out long user);
 [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr h,uint code);
 [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h,uint ms);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 public class Row {public uint pid,parent;public string born,image;public bool ended;internal IntPtr handle;}
 readonly Dictionary<uint,Row> held = new Dictionary<uint,Row>();
 readonly HashSet<uint> failedOpens = new HashSet<uint>();
 public int Unopened { get { return failedOpens.Count; } }
 public int UnopenedPresent { get; private set; }
 static Row Hold(PE expected) {
   var handle = OpenProcess(0x101001,false,expected.pid);
   if(handle==IntPtr.Zero) return null;
   try {
     // Re-read the parent *after* opening this object. Never trust the PID and
     // parent pair from discovery alone. Keep the handle through termination.
     PE current;
     long born,exit,kernel,user;
     if(!Snapshot().TryGetValue(expected.pid,out current) || current.parent!=expected.parent ||
        !GetProcessTimes(handle,out born,out exit,out kernel,out user)) return null;
     var row = new Row { pid=current.pid, parent=current.parent, born=born.ToString(), image=current.name, handle=handle };
     handle=IntPtr.Zero;
     return row;
   } finally { if(handle!=IntPtr.Zero) CloseHandle(handle); }
 }
 static Dictionary<uint,PE> Snapshot() {
   var snapshot=CreateToolhelp32Snapshot(2,0);
   if(snapshot==new IntPtr(-1)) throw new Exception("process_snapshot_failed");
   try {
     var list=new Dictionary<uint,PE>(); var entry=new PE(); entry.size=(uint)Marshal.SizeOf(entry);
     if(!Process32FirstW(snapshot,ref entry)) throw new Exception("process_snapshot_failed");
     do { list[entry.pid]=entry; } while(Process32NextW(snapshot,ref entry));
     if(Marshal.GetLastWin32Error()!=18) throw new Exception("process_snapshot_failed");
     return list;
   } finally { CloseHandle(snapshot); }
 }
 public void Init(uint root,uint parent,long earliest) {
   PE entry;
   if(!Snapshot().TryGetValue(root,out entry)) throw new Exception("starter_missing");
   if(entry.parent!=parent) throw new Exception("starter_identity_missing");
   var row=Hold(entry);
   if(row==null || long.Parse(row.born)<earliest || WaitForSingleObject(row.handle,0)!=258) {
     if(row!=null) CloseHandle(row.handle);
     throw new Exception("starter_identity_missing");
   }
   held.Add(root,row);
 }
 static bool BornDuringParent(Row child,Row parent) {
   long born,exit,kernel,user;
   if(!GetProcessTimes(parent.handle,out born,out exit,out kernel,out user)) throw new Exception("process_identity_changed");
   long childBirth=long.Parse(child.born);
   return childBirth>=born && (exit==0 || childBirth<=exit);
 }
 public Row[] Scan() {
   var listed=Snapshot(); var unresolved=new HashSet<uint>(); bool added;
   do {
     added=false;
     foreach(var entry in listed.Values) {
       Row parent;
       if(held.ContainsKey(entry.pid) || !held.TryGetValue(entry.parent,out parent)) continue;
       var child=Hold(entry);
       if(child==null) { failedOpens.Add(entry.pid); unresolved.Add(entry.pid); continue; }
       try {
         if(!BornDuringParent(child,parent)) continue;
         held.Add(child.pid,child); unresolved.Remove(child.pid); added=true; child=null;
       } finally { if(child!=null) CloseHandle(child.handle); }
     }
   } while(added);
   // Only a still-listed, unresolved possible descendant blocks a gone result.
   var finalList=Snapshot(); UnopenedPresent=0;
   foreach(var entry in finalList.Values) {
     PE prior;
     if(!held.ContainsKey(entry.pid) && held.ContainsKey(entry.parent) &&
        (!listed.TryGetValue(entry.pid,out prior) || prior.parent!=entry.parent)) {
       unresolved.Add(entry.pid); failedOpens.Add(entry.pid);
     }
   }
   foreach(uint pid in unresolved) {
     PE row;
     if(finalList.TryGetValue(pid,out row) && held.ContainsKey(row.parent)) UnopenedPresent++;
   }
   foreach(var row in held.Values) {
     uint state=WaitForSingleObject(row.handle,0);
     if(state!=0 && state!=258) throw new Exception("process_identity_changed");
     row.ended=state==0;
   }
   return new List<Row>(held.Values).ToArray();
 }
 public Row[] End() {
   Scan();
   var rows=new List<Row>(held.Values);
   rows.Sort((a,b)=>long.Parse(a.born).CompareTo(long.Parse(b.born)));
   foreach(var row in rows) {
     long born,exit,kernel,user;
     if(!GetProcessTimes(row.handle,out born,out exit,out kernel,out user) || born.ToString()!=row.born)
       throw new Exception("process_identity_changed");
     if(WaitForSingleObject(row.handle,0)==258) TerminateProcess(row.handle,1);
   }
   foreach(var row in rows) WaitForSingleObject(row.handle,3000);
   // Includes descendants discovered after a parent has already exited.
   return Scan();
 }
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
   @{id=$request.id;ok=$true;rows=$rows;unopened=$owned.Unopened;unopened_present=$owned.UnopenedPresent} | ConvertTo-Json -Compress -Depth 5 | ForEach-Object { [Console]::WriteLine($_) }
  } catch {
   $reason='process_controller_failed'
   foreach ($word in @('starter_missing','starter_identity_missing','process_snapshot_failed','process_identity_changed','terminate_handle_failed')) {
    if ($_.Exception.Message.Contains($word)) { $reason=$word }
   }
   @{id=$request.id;ok=$false;reason=$reason} | ConvertTo-Json -Compress | ForEach-Object { [Console]::WriteLine($_) }
  }
 }
} finally { $owned.Close() }
