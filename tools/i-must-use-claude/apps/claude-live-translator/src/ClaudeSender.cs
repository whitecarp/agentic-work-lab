using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Automation.Text;

// Explicitly invoked, UIA-only sender. It never focuses, clicks, types, uses clipboard, or logs prompt text.
internal static class ClaudeSender
{
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    private const int MaxTextLength = 32000;
    private sealed class Context { public AutomationElement Root, Doc, Feed, Editor, Button; public string Conversation, Title, Draft, LatestUserId; public bool Busy, LatestUserExists, ValueSupported; public int EditorCount, MatchingEditorCount; }

    public static int Main(string[] args) {
        Console.InputEncoding = new UTF8Encoding(false);
        Console.OutputEncoding = new UTF8Encoding(false);
        try {
            if(args.Length==2 && args[0]=="--probe") { Console.WriteLine(Probe(args[1])); return 0; }
            if(args.Length==2 && args[0]=="--debug") { Console.WriteLine(DebugEditors(args[1])); return 0; }
            if(args.Length==2 && args[0]=="--send") { string json=Console.ReadLine(); Console.WriteLine(Send(args[1],json)); return 0; }
            Console.WriteLine("{\"ok\":false,\"status\":\"usage\"}"); return 2;
        } catch(Exception e) { Console.WriteLine("{\"ok\":false,\"status\":\"error\",\"error\":"+Q(e.GetType().Name)+"}"); return 1; }
    }

    private static string Probe(string hwndArg) {
        IntPtr hwnd; string error; Context c=ReadContext(hwndArg,out hwnd,out error);
        if(c==null) return "{\"ok\":false,\"conversation\":null,\"title\":\"\",\"ready\":false,\"busy\":false,\"draftEmpty\":false,\"editorCount\":0,\"matchingEditorCount\":0,\"editorFound\":false,\"valueSupported\":false,\"status\":"+Q(error)+"}";
        bool empty=c.Editor!=null&&String.IsNullOrEmpty(c.Draft); bool supported=c.Editor!=null&&IsSettable(c.Editor); bool ready=empty&&!c.Busy&&supported;
        string status=ready?"ready":(c.Busy?"busy":(c.Editor==null?"no_editor":(!empty?"draft_exists":"unsupported")));
        return "{\"ok\":true,\"conversation\":"+Q(c.Conversation)+",\"title\":"+Q(c.Title)+",\"ready\":"+Bool(ready)+",\"busy\":"+Bool(c.Busy)+",\"draftEmpty\":"+Bool(empty)+",\"editorCount\":"+c.EditorCount+",\"matchingEditorCount\":"+c.MatchingEditorCount+",\"editorFound\":"+Bool(c.Editor!=null)+",\"valueSupported\":"+Bool(c.ValueSupported)+",\"status\":"+Q(status)+"}";
    }

    private static string DebugEditors(string hwndArg) {
        AutomationElement doc; string error; if(!TryGetChatDocument(hwndArg,out doc,out error)) return "{\"ok\":false,\"status\":"+Q(error)+",\"editors\":[]}";
        var edits=doc.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit));
        var b=new StringBuilder("{\"ok\":true,\"editorCount\":").Append(edits.Count).Append(",\"editors\":[");
        for(int i=0;i<edits.Count;i++) {
            if(i>0)b.Append(','); AutomationElement e=edits[i]; string name=SafeName(e); bool password=false; try{password=e.Current.IsPassword;}catch{}
            b.Append("{\"nameLength\":").Append(name.Length).Append(",\"nameHasClaude\":").Append(Bool(name.IndexOf("Claude",StringComparison.OrdinalIgnoreCase)>=0)).Append(",\"isPassword\":").Append(Bool(password)).Append(",\"role\":").Append(Q(SafeLocalizedType(e))).Append('}');
        }
        return b.Append("]}").ToString();
    }

    private static bool TryGetChatDocument(string arg,out AutomationElement doc,out string error) {
        doc=null; error="window_not_found"; long raw;
        try{raw=arg.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?Convert.ToInt64(arg.Substring(2),16):Int64.Parse(arg,CultureInfo.InvariantCulture);}catch{error="invalid_handle";return false;}
        IntPtr hwnd=new IntPtr(raw); uint pid; GetWindowThreadProcessId(hwnd,out pid); if(pid==0)return false;
        try{using(Process proc=Process.GetProcessById((int)pid))if(!String.Equals(proc.ProcessName,"claude",StringComparison.OrdinalIgnoreCase)){error="not_claude";return false;}}catch{error="window_not_found";return false;}
        try{doc=FindChatDocument(AutomationElement.FromHandle(hwnd));if(doc==null){error="chat_not_found";return false;}error=null;return true;}catch{error="uia_read_failed";return false;}
    }

    private static string Send(string hwndArg,string json) {
        string expected=ReadJsonString(json,"conversation"), text=ReadJsonString(json,"text");
        if(String.IsNullOrEmpty(expected)||String.IsNullOrEmpty(text)) return Outcome(false,"invalid_request");
        if(text.Length>MaxTextLength) return Outcome(false,"text_too_long");
        IntPtr hwnd; string error; Context c=ReadContext(hwndArg,out hwnd,out error);
        if(c==null) return Outcome(false,error);
        if(!String.Equals(c.Conversation,expected,StringComparison.Ordinal)) return Outcome(false,"conversation_changed");
        if(c.Busy) return Outcome(false,"busy");
        if(!String.IsNullOrEmpty(c.Draft)) return Outcome(false,"draft_exists");
        if(c.Editor==null) return Outcome(false,"no_editor");
        string previousUserId=c.LatestUserId;
        if(c.LatestUserExists && String.IsNullOrEmpty(previousUserId)) return Outcome(false,"verification_unavailable");
        try {
            object valueObject;
            if(!c.Editor.TryGetCurrentPattern(ValuePattern.Pattern,out valueObject)) return Outcome(false,"unsupported");
            ValuePattern value=(ValuePattern)valueObject;
            if(value.Current.IsReadOnly) return Outcome(false,"unsupported");
            value.SetValue(text);
            if(!String.Equals(value.Current.Value,text,StringComparison.Ordinal)) return Outcome(false,"draft_verification_failed");
        } catch { return Outcome(false,"draft_set_failed"); }

        // Rebuild the UI context and verify identity, state and exact draft immediately before Invoke.
        Context before=ReadContext(hwndArg,out hwnd,out error);
        if(before==null || !String.Equals(before.Conversation,expected,StringComparison.Ordinal)) return Outcome(false,"conversation_changed");
        if(before.Busy) return Outcome(false,"busy");
        if(before.Editor==null || !String.Equals(ReadValue(before.Editor),text,StringComparison.Ordinal)) return Outcome(false,"draft_verification_failed");
        AutomationElement sendButton=FindUniqueSendButton(before.Doc,before.Editor);
        if(sendButton==null || !IsEnabled(sendButton)) return Outcome(false,"draft_only");
        try { object invoke; if(!sendButton.TryGetCurrentPattern(InvokePattern.Pattern,out invoke)) return Outcome(false,"draft_only"); ((InvokePattern)invoke).Invoke(); }
        catch { return Outcome(false,"send_result_unknown"); }

        // Never retry. Report sent only if the user message is observed and the composer is empty.
        DateTime until=DateTime.UtcNow.AddSeconds(5);
        while(DateTime.UtcNow<until) {
            try {
                Context after=ReadContext(hwndArg,out hwnd,out error);
                if(after==null || !String.Equals(after.Conversation,expected,StringComparison.Ordinal)) return Outcome(false,"send_result_unknown");
                string observedId;
                if(LatestUserEquals(after,text,out observedId) && !String.IsNullOrEmpty(observedId) && !String.Equals(observedId,previousUserId,StringComparison.Ordinal) && IsNewerId(observedId,previousUserId) && String.IsNullOrEmpty(after.Draft)) return Outcome(true,"sent");
            } catch { }
            Thread.Sleep(200);
        }
        return Outcome(false,"send_result_unknown");
    }

    private static Context ReadContext(string arg,out IntPtr hwnd,out string error) {
        hwnd=IntPtr.Zero; error="window_not_found"; long raw;
        try { raw=arg.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?Convert.ToInt64(arg.Substring(2),16):Int64.Parse(arg,CultureInfo.InvariantCulture); hwnd=new IntPtr(raw); } catch { error="invalid_handle"; return null; }
        uint pid; GetWindowThreadProcessId(hwnd,out pid); if(pid==0)return null;
        try { using(Process proc=Process.GetProcessById((int)pid)) if(!String.Equals(proc.ProcessName,"claude",StringComparison.OrdinalIgnoreCase)){error="not_claude";return null;} } catch { error="window_not_found";return null; }
        try {
            AutomationElement root=AutomationElement.FromHandle(hwnd), doc=FindChatDocument(root);
            if(doc==null){error="chat_not_found";return null;}
            string conv=GetConversation(doc); if(String.IsNullOrEmpty(conv)){error="conversation_unavailable";return null;}
            AutomationElement feed=FindFeed(doc); if(feed==null){error="feed_unavailable";return null;}
            var editors=new List<AutomationElement>();
            AutomationElementCollection allEdits=doc.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit));
            foreach(AutomationElement e in allEdits)
                if(IsComposer(e)) editors.Add(e);
            // Multiple edit controls or matching composers are ambiguous; never select one arbitrarily.
            AutomationElement editor=allEdits.Count==1 && editors.Count==1?editors[0]:null;
            bool busy=FindStopButton(doc)!=null;
            AutomationElement button=editor==null?null:FindUniqueSendButton(doc,editor);
            string latestUserId; AutomationElement latestUser=FindLatestUser(feed,out latestUserId);
            return new Context{Root=root,Doc=doc,Feed=feed,Editor=editor,Button=button,Conversation=conv,Title=SafeName(doc),Draft=editor==null?null:ReadValue(editor),Busy=busy,LatestUserId=latestUserId,LatestUserExists=latestUser!=null,EditorCount=allEdits.Count,MatchingEditorCount=editors.Count,ValueSupported=editor!=null&&HasValuePattern(editor)};
        } catch { error="uia_read_failed"; return null; }
    }

    private static AutomationElement FindChatDocument(AutomationElement root) {
        foreach(AutomationElement e in root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document))) {
            string url=ReadValue(e); if(String.IsNullOrEmpty(url)) url=SafeName(e);
            if(url.StartsWith("https://claude.ai/chat/",StringComparison.OrdinalIgnoreCase)) return e;
            if(Regex.IsMatch(url,@"https://claude\.ai/chat/[0-9a-f-]{20,}(?:[/?#]|$)",RegexOptions.IgnoreCase)) return e;
        }
        return null;
    }
    private static string GetConversation(AutomationElement doc) {
        string u=ReadValue(doc); if(String.IsNullOrEmpty(u))u=SafeName(doc);
        Match m=Regex.Match(u,@"https://claude\.ai/chat/([0-9a-f-]{20,})(?:[/?#]|$)",RegexOptions.IgnoreCase);
        return m.Success?m.Groups[1].Value:null;
    }
    private static AutomationElement FindFeed(AutomationElement doc) {
        foreach(AutomationElement e in doc.FindAll(TreeScope.Descendants,Condition.TrueCondition)) {
            string name=(SafeName(e)??"").Trim();
            bool label=name.IndexOf("对话消息",StringComparison.OrdinalIgnoreCase)>=0 || name.IndexOf("Conversation messages",StringComparison.OrdinalIgnoreCase)>=0;
            if(label) return e;
        }
        return null;
    }
    private static bool IsComposer(AutomationElement e) {
        string name=(SafeName(e)??"").Trim(), help=(SafeHelp(e)??"").Trim();
        return ExactAny(name,"输入您发送给 Claude 的提示词") && ExactAny(help,"回复") ||
               ExactAny(name,"Enter your prompt for Claude") && ExactAny(help,"Reply") ||
               ExactAny(name,"输入您发送给 Claude 的提示词") || ExactAny(name,"Enter your prompt for Claude");
    }
    private static AutomationElement FindStopButton(AutomationElement doc) {
        foreach(AutomationElement e in doc.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button)))
            if(ExactAny(SafeName(e),"停止回复","Stop response"))return e;
        return null;
    }
    private static AutomationElement FindUniqueSendButton(AutomationElement doc,AutomationElement editor) {
        AutomationElement parent=TreeWalker.ControlViewWalker.GetParent(editor);
        for(int depth=0;parent!=null && depth<5;depth++,parent=TreeWalker.ControlViewWalker.GetParent(parent)) {
            var matches=new List<AutomationElement>();
            foreach(AutomationElement e in parent.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button)))
                if(ExactAny(SafeName(e),"发送消息","Send message","发送","Send"))matches.Add(e);
            if(matches.Count>1)return null;
            if(matches.Count==1)return matches[0];
            if(parent.Equals(doc))break;
        }
        return null;
    }
    private static bool LatestUserEquals(Context c,string text,out string observedId) {
        observedId=null; var articles=new List<AutomationElement>();
        foreach(AutomationElement e in c.Feed.FindAll(TreeScope.Descendants,Condition.TrueCondition)) if(IsArticle(e)&&!HasArticleAncestor(e,c.Feed))articles.Add(e);
        for(int i=articles.Count-1;i>=0;i--) {
            AutomationElement article=articles[i]; string heading=FindAuthorHeading(article);
            if(String.IsNullOrEmpty(heading)||!(heading.StartsWith("您说：",StringComparison.Ordinal)||heading.StartsWith("You said:",StringComparison.OrdinalIgnoreCase)))continue;
            observedId=ArticleId(SafeName(article)); return Normalize(ExtractArticleText(c.Doc,article,heading))==Normalize(text);
        }
        return false;
    }
    private static AutomationElement FindLatestUser(AutomationElement feed,out string id) {
        id=null; var articles=new List<AutomationElement>();
        foreach(AutomationElement e in feed.FindAll(TreeScope.Descendants,Condition.TrueCondition))if(IsArticle(e)&&!HasArticleAncestor(e,feed))articles.Add(e);
        for(int i=articles.Count-1;i>=0;i--){string h=FindAuthorHeading(articles[i]);if(!String.IsNullOrEmpty(h)&&(h.StartsWith("您说：",StringComparison.Ordinal)||h.StartsWith("You said:",StringComparison.OrdinalIgnoreCase))){id=ArticleId(SafeName(articles[i]));return articles[i];}}
        return null;
    }
    private static string ArticleId(string name){Match m=Regex.Match(name??"",@"^(?:第\s*)?(\d+)(?:\s*条消息(?:，共\s*\d+\s*条消息)?|\s+of\s+\d+)$",RegexOptions.IgnoreCase);return m.Success?m.Groups[1].Value:null;}
    private static bool IsNewerId(string observed,string previous){long n,o;if(!Int64.TryParse(observed,out n))return false;if(!Int64.TryParse(previous,out o))return true;return n>o;}
    private static string Normalize(string s){return (s??"").Replace("\r\n","\n").Replace('\r','\n').Trim();}
    private static string ExtractArticleText(AutomationElement doc,AutomationElement article,string heading) {
        object p; try {if(doc.TryGetCurrentPattern(TextPattern.Pattern,out p)){TextPatternRange range=((TextPattern)p).RangeFromChild(article);AutomationElement toolbar=FindToolbar(article);if(toolbar!=null){TextPatternRange tr=((TextPattern)p).RangeFromChild(toolbar);range.MoveEndpointByRange(TextPatternRangeEndpoint.End,tr,TextPatternRangeEndpoint.Start);}return CleanArticleText(range.GetText(50000),heading);}}catch{}
        var parts=new List<string>();CollectArticleText(article,parts,0);return CleanArticleText(String.Join("\n",parts.ToArray()),heading);
    }
    private static AutomationElement FindToolbar(AutomationElement article){foreach(AutomationElement e in article.FindAll(TreeScope.Descendants,Condition.TrueCondition))if(ExactAny(SafeName(e),"消息操作","Message actions"))return e;return null;}
    private static string CleanArticleText(string s,string heading){s=(s??"").Replace("\r\n","\n").Replace('\r','\n');if(!String.IsNullOrEmpty(heading)){string t=s.TrimStart();if(t.StartsWith(heading,StringComparison.Ordinal))s=s.Remove(s.Length-t.Length,heading.Length);}return Regex.Replace(s,@"\n{3,}","\n\n").Trim();}
    private static void CollectArticleText(AutomationElement e,List<string> parts,int depth){if(depth>40)return;ControlType t;string name=SafeName(e);try{t=e.Current.ControlType;}catch{return;}if(t==ControlType.ToolBar||t==ControlType.Button||t==ControlType.StatusBar||t==ControlType.ProgressBar||ExactAny(name,"消息操作","Message actions"))return;AutomationElementCollection kids;try{kids=e.FindAll(TreeScope.Children,Condition.TrueCondition);}catch{return;}if((t==ControlType.Text||t==ControlType.ListItem||t==ControlType.DataItem||t==ControlType.Hyperlink)&&!String.IsNullOrWhiteSpace(name))parts.Add(name.Trim());if(kids.Count==0)return;foreach(AutomationElement child in kids)CollectArticleText(child,parts,depth+1);}
    private static bool IsArticle(AutomationElement e) {
        string type=SafeLocalizedType(e), role=SafeAriaRole(e), n=SafeName(e);
        return ExactAny(type,"文章","article")||ExactAny(role,"article")||Regex.IsMatch(n,@"^第\s*\d+\s*条消息，共\s*\d+\s*条消息$")||Regex.IsMatch(n,@"^Message\s+\d+\s+of\s+\d+$",RegexOptions.IgnoreCase)||ExactAny(n,"正在流式传输消息","streaming message");
    }
    private static bool HasArticleAncestor(AutomationElement e,AutomationElement feed) { try { AutomationElement p=TreeWalker.ControlViewWalker.GetParent(e); while(p!=null&&!p.Equals(feed)){if(IsArticle(p))return true;p=TreeWalker.ControlViewWalker.GetParent(p);} }catch{} return false; }
    private static string FindAuthorHeading(AutomationElement article) {
        foreach(AutomationElement e in article.FindAll(TreeScope.Descendants,Condition.TrueCondition)) { string n=SafeName(e); if(n.StartsWith("您说：",StringComparison.Ordinal)||n.StartsWith("You said:",StringComparison.OrdinalIgnoreCase)||n.StartsWith("Claude 已回复：",StringComparison.Ordinal)||n.StartsWith("Claude responded:",StringComparison.OrdinalIgnoreCase))return n; }
        return null;
    }
    private static string ReadValue(AutomationElement e){try{object p;if(e.TryGetCurrentPattern(ValuePattern.Pattern,out p))return ((ValuePattern)p).Current.Value??"";}catch{}return "";}
    private static string SafeName(AutomationElement e){try{return e.Current.Name??"";}catch{return "";}}
    private static string SafeHelp(AutomationElement e){try{return e.Current.HelpText??"";}catch{return "";}}
    private static string SafeLocalizedType(AutomationElement e){try{return e.Current.LocalizedControlType??"";}catch{return "";}}
    private static string SafeAriaRole(AutomationElement e){try{object v=e.GetCurrentPropertyValue(AutomationProperty.LookupById(30101),true);return v==AutomationElement.NotSupported?"":(v==null?"":v.ToString());}catch{return "";}}
    private static bool ExactAny(string s,params string[] values){foreach(string v in values)if(String.Equals(s,v,StringComparison.OrdinalIgnoreCase))return true;return false;}
    private static bool IsEnabled(AutomationElement e){try{return e.Current.IsEnabled;}catch{return false;}}
    private static bool HasValuePattern(AutomationElement e){try{object p;return e.TryGetCurrentPattern(ValuePattern.Pattern,out p);}catch{return false;}}
    private static bool IsSettable(AutomationElement e){try{object p;if(!e.TryGetCurrentPattern(ValuePattern.Pattern,out p))return false;return !((ValuePattern)p).Current.IsReadOnly;}catch{return false;}}
    private static string Outcome(bool ok,string status){return "{\"ok\":"+Bool(ok)+",\"status\":"+Q(status)+"}";}
    private static string Bool(bool b){return b?"true":"false";}
    private static string Q(string s){if(s==null)return "null";var b=new StringBuilder("\"");foreach(char c in s){switch(c){case '"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<32)b.Append("\\u").Append(((int)c).ToString("x4"));else b.Append(c);break;}}return b.Append('"').ToString();}

    private static string ReadJsonString(string json,string key) {
        if(json==null)return null; int i=json.IndexOf('"'+key+'"',StringComparison.Ordinal); if(i<0)return null; i=json.IndexOf(':',i+key.Length+2); if(i<0)return null;
        while(++i<json.Length&&Char.IsWhiteSpace(json[i])){} if(i>=json.Length||json[i]!='"')return null;
        var b=new StringBuilder(); for(i++;i<json.Length;i++){char c=json[i];if(c=='"')return b.ToString();if(c!='\\'){b.Append(c);continue;}if(++i>=json.Length)return null;c=json[i];switch(c){case '"':b.Append('"');break;case '\\':b.Append('\\');break;case '/':b.Append('/');break;case 'b':b.Append('\b');break;case 'f':b.Append('\f');break;case 'n':b.Append('\n');break;case 'r':b.Append('\r');break;case 't':b.Append('\t');break;case 'u':if(i+4>=json.Length)return null;int code; if(!Int32.TryParse(json.Substring(i+1,4),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out code))return null;b.Append((char)code);i+=4;break;default:return null;}}
        return null;
    }
}
