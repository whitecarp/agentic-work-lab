 // Synthetic table and handles for c38-handle-races.test.mjs.
 static Dictionary<uint,PE> Table=new Dictionary<uint,PE>();
 static Dictionary<uint,long> Birth=new Dictionary<uint,long>(), ExitTime=new Dictionary<uint,long>();
 static List<uint> Ended=new List<uint>();
 static void Add(uint pid,uint parent,long born) {
   Table[pid]=new PE {pid=pid,parent=parent,name="fixture"};
   Birth[pid]=born; ExitTime[pid]=0;
 }
 static IntPtr OpenProcess(uint rights,bool inherit,uint pid) {
   if(pid==140) return IntPtr.Zero;
   if(pid==150) Add(150,999,40); // Number recycled after discovery.
   return (IntPtr)(int)pid;
 }
 static bool GetProcessTimes(IntPtr h,out long born,out long exit,out long kernel,out long user){
   uint pid=(uint)h.ToInt32(); born=Birth[pid]; exit=ExitTime[pid]; kernel=0; user=0; return true;
 }
 static bool TerminateProcess(IntPtr h,uint code){
   uint pid=(uint)h.ToInt32(); Ended.Add(pid); ExitTime[pid]=100;
   Table.Remove(pid);
   if(pid==110) Add(160,110,91); // Child born before exit, discovered afterwards.
   return true;
 }
 static uint WaitForSingleObject(IntPtr h,uint timeout){return ExitTime[(uint)h.ToInt32()]==0?258u:0u;}
 static bool CloseHandle(IntPtr h){return true;}
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 public static string Exercise(){
   Add(100,999,10); Add(110,100,20); Add(120,110,30);
   Add(140,100,40); Add(150,100,50);
   var observer=new OwnedProcesses(); observer.Init(100,999,9);
   var initial=observer.Scan();
   Check(initial.Length==3,"held unrelated or missing process");
   Check(observer.Unopened>=1 && observer.UnopenedPresent==1,"missing unresolved child");
   var after=observer.End();
   Check(Ended.Count==3 && Ended[0]==100 && Ended[1]==110 && Ended[2]==120,"termination order");
   Check(Array.Exists(after,r=>r.pid==160 && !r.ended),"late child of exited parent lost");
   Check(observer.UnopenedPresent==1,"still-visible inaccessible child forgotten");
   Table.Remove(140); observer.Scan(); Check(observer.UnopenedPresent==0,"departed child marked present");
   observer.Close(); return "lineage checks passed";
 }
