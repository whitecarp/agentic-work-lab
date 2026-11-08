using System;
using System.Collections.Generic;

namespace ClaudeCompanion {
    // Deliberately no persistence: at most 20 successful translations / 256K characters per session.
    internal sealed class TranslationHistory {
        internal sealed class Entry {
            internal readonly string Source, Result, Direction;
            internal readonly bool Offline;
            internal Entry(string source,string result,string direction,bool offline){Source=source;Result=result;Direction=direction;Offline=offline;}
        }
        private readonly List<Entry> entries=new List<Entry>();
        private int characters,index;
        internal int Count{get{return entries.Count;}}
        internal int Position{get{return index;}}
        internal bool CanPrevious{get{return entries.Count>0&&index>0;}}
        internal bool CanNext{get{return index>=0&&index<entries.Count-1;}}
        internal Entry Current{get{return index>=0&&index<entries.Count?entries[index]:null;}}
        internal void LeaveView(){index=entries.Count;}
        internal void Add(string source,string result,string direction,bool offline){
            if(String.IsNullOrWhiteSpace(source)||String.IsNullOrWhiteSpace(result)||source.Length+result.Length>256000){LeaveView();return;}
            var entry=new Entry(source,result,direction,offline);entries.Add(entry);characters+=source.Length+result.Length;
            while(entries.Count>20||characters>256000){characters-=entries[0].Source.Length+entries[0].Result.Length;entries.RemoveAt(0);}
            index=entries.Count-1;
        }
        internal Entry Move(int delta){if(delta==-1&&CanPrevious)--index;else if(delta==1&&CanNext)++index;else return null;return Current;}
        internal void Clear(){entries.Clear();characters=0;index=0;}
    }
}
