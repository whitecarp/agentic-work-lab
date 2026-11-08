using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ClaudeCompanion {
    internal sealed class GlossaryEntry {
        public string source, replacement, target;
        // Off by default: "claude code" and "CLAUDE CODE" both use the "Claude Code" entry.
        public bool matchCase;
    }
    internal static class GlossaryStore {
        internal static string FilePath { get { return Path.Combine(Preferences.DataDirectory,"glossary.json"); } }
        internal const int MaxEntries=200;
        // Names that are also everyday English words keep their exact spelling, so
        // "improve your skills" or "a short sonnet" still translate normally.
        internal static readonly string[] ExactCaseNames={"Skills","Artifacts","Hooks","Opus","Sonnet","Haiku","Fable","Gemini","Grok","Llama","Mistral","Perplexity","Windsurf","React","Rust","Swift","Cursor","Copilot"};
        internal static List<GlossaryEntry> Defaults(){
            var rows=new List<GlossaryEntry>();
            foreach(string name in new[]{"Claude","Claude Code","Claude Desktop","Claude in Chrome","Claude Agent SDK","Claude Pro","Claude Max","Claude Team","Claude Enterprise","Claude Console","Cowork","CLAUDE.md","AGENTS.md","Codex","Anthropic","OpenAI","ChatGPT","DeepSeek","Qwen","Kimi","Hugging Face","Ollama","Midjourney","GitHub","GitHub Actions","Git","MCP","API","CLI","SDK","JSON","YAML","TOML","Markdown","Python","JavaScript","TypeScript","PowerShell","Node.js","Docker","Visual Studio Code","VS Code","Visual Studio","UTF-8","Cockpit Tools","CC Switch"})
                rows.Add(new GlossaryEntry{source=name,replacement=name,target="*"});
            foreach(string name in ExactCaseNames)
                rows.Add(new GlossaryEntry{source=name,replacement=name,target="*",matchCase=true});
            // Use technical phrases rather than ambiguous words such as build or token.
            string[,] phrases={
                {"context window","上下文窗口"},{"system prompt","系统提示词"},{"tool call","工具调用"},
                {"tool calls","工具调用"},{"tool use","工具调用"},{"function calling","函数调用"},
                {"rate limit","速率限制"},{"rate limiting","限流"},{"usage limit","用量上限"},
                {"prompt engineering","提示词工程"},{"prompt caching","提示缓存"},{"extended thinking","扩展思考"},
                {"structured output","结构化输出"},{"large language model","大语言模型"},{"knowledge cutoff","知识截止日期"},
                {"fine-tuning","微调"},{"slash command","斜杠命令"},{"plan mode","计划模式"},
                {"code review","代码审查"},{"pull request","拉取请求"},{"merge conflict","合并冲突"},
                {"commit message","提交信息"},{"feature branch","功能分支"},{"repository","仓库"},
                {"unit test","单元测试"},{"integration test","集成测试"},{"end-to-end test","端到端测试"},
                {"regression test","回归测试"},{"test coverage","测试覆盖率"},{"type checking","类型检查"},
                {"linter","代码检查工具"},{"stack trace","堆栈跟踪"},{"breaking change","破坏性变更"},
                {"edge case","边界情况"},{"race condition","竞态条件"},{"memory leak","内存泄漏"},
                {"dry run","试运行"},{"happy path","正常路径"},{"hot reload","热重载"},
                {"environment variable","环境变量"},{"API key","API 密钥"},{"access token","访问令牌"},
                {"command line","命令行"},{"working directory","工作目录"},{"source code","源代码"},
                {"read-only","只读"}};
            // Terms ignore case, so one row covers "context window", "Context window" and "CONTEXT WINDOW".
            for(int i=0;i<phrases.GetLength(0);i++)rows.Add(new GlossaryEntry{source=phrases[i,0],replacement=phrases[i,1],target="zh-CN"});
            string[,] reverse={
                {"上下文窗口","context window"},{"系统提示词","system prompt"},{"提示词","prompt"},
                {"提示词工程","prompt engineering"},{"工具调用","tool call"},{"速率限制","rate limit"},
                {"扩展思考","extended thinking"},{"大语言模型","large language model"},{"单元测试","unit test"},
                {"集成测试","integration test"},{"端到端测试","end-to-end test"},{"回归测试","regression test"},
                {"代码审查","code review"},{"拉取请求","pull request"},{"合并冲突","merge conflict"},
                {"提交信息","commit message"},{"仓库","repository"},{"代码库","codebase"},
                {"源代码","source code"},{"命令行","command line"},{"工作目录","working directory"},
                {"环境变量","environment variable"},{"访问令牌","access token"},{"边界情况","edge case"},
                {"报错信息","error message"}};
            for(int i=0;i<reverse.GetLength(0);i++)rows.Add(new GlossaryEntry{source=reverse[i,0],replacement=reverse[i,1],target="en"});
            return Validate(rows);
        }
        // Saved dictionaries are user-owned: only an explicit editor action adds defaults.
        internal static List<GlossaryEntry> MergeRecommended(IEnumerable<GlossaryEntry> current){
            var rows=Validate(current);
            foreach(var recommended in Defaults()){
                bool conflict=rows.Exists(delegate(GlossaryEntry row){return String.Equals(row.source,recommended.source,StringComparison.OrdinalIgnoreCase)&&(row.target==recommended.target||row.target=="*"||recommended.target=="*");});
                if(!conflict)rows.Add(recommended);
            }
            return Validate(rows);
        }
        internal static List<GlossaryEntry> Validate(IEnumerable<GlossaryEntry> source){
            var result=new List<GlossaryEntry>();var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in source){
                if(item==null||String.IsNullOrWhiteSpace(item.source)||String.IsNullOrWhiteSpace(item.replacement)||item.target==null||
                    item.source.Length>64||item.replacement.Length>128||Regex.IsMatch(item.source+item.replacement,"[\\x00-\\x1F]")||
                    !Regex.IsMatch(item.target,"^(\\*|[a-z]{2,3}(?:-[A-Z]{2})?)$"))
                    throw new InvalidOperationException(Ui.T("请填写原词、固定译法和目标语言；原词最多 64 字，译法最多 128 字。","Enter a term, replacement and target. Limit: 64 characters per term, 128 per replacement."));
                string target=item.target=="zh"?"zh-CN":item.target=="zt"?"zh-TW":item.target;
                var entry=new GlossaryEntry{source=item.source.Trim(),replacement=item.replacement.Trim(),target=target,matchCase=item.matchCase};
                // Same rule as backend/glossary.py: ignore-case terms that differ only in case are one term.
                string key=target+"\n"+(entry.matchCase?"=\n"+entry.source:"~\n"+entry.source.ToLowerInvariant());
                if(!seen.Add(key))throw new InvalidOperationException(Ui.T("同一目标语言不能重复添加相同原词；没勾「区分大小写」时，只差大小写也算重复。","A term can appear only once for each target language. Without Match case, terms that differ only in case count as the same."));
                result.Add(entry);if(result.Count>MaxEntries)throw new InvalidOperationException(Ui.T("最多保存 "+MaxEntries+" 条术语。","Save up to "+MaxEntries+" terms."));
            }
            return result;
        }
        internal static List<GlossaryEntry> Load(){
            if(!File.Exists(FilePath))return Defaults();
            try{
                if(new FileInfo(FilePath).Length>256000)throw new InvalidDataException();
                var data=Json.Decode(File.ReadAllText(FilePath,Encoding.UTF8));object raw;
                if(!data.TryGetValue("entries",out raw)||!(raw is IEnumerable)||raw is string)throw new InvalidDataException();
                object version;int format=1;if(data.TryGetValue("version",out version)&&version is int)format=(int)version;
                var rows=new List<GlossaryEntry>();foreach(var value in (IEnumerable)raw){var row=value as Dictionary<string,object>;if(row==null)throw new InvalidDataException();rows.Add(new GlossaryEntry{source=Json.Str(row,"source"),replacement=Json.Str(row,"replacement"),target=Json.Str(row,"target"),matchCase=Json.Bool(row,"matchCase")});}
                return Validate(format<2?FromCaseSensitiveFormat(rows):rows);
            }catch{throw new InvalidOperationException(Ui.T("术语词典无法读取，请检查 glossary.json；原文件未改动。","Cannot read glossary.json. Check the file; it has not been changed."));}
        }
        // Version 1 files matched every term case by case. Everyday-word names stay exact; capitalised
        // copies with the same translation fold into one ignore-case row; copies whose translations
        // differ (Apple / apple) stay exact so nothing changes meaning.
        internal static List<GlossaryEntry> FromCaseSensitiveFormat(List<GlossaryEntry> rows){
            var groups=new Dictionary<string,List<GlossaryEntry>>(StringComparer.Ordinal);
            foreach(var row in rows){
                string source=(row.source??"").Trim();
                row.matchCase=Array.IndexOf(ExactCaseNames,source)>=0;
                if(row.matchCase)continue;
                string key=(row.target??"")+"\n"+source.ToLowerInvariant();
                List<GlossaryEntry> group;if(!groups.TryGetValue(key,out group))groups[key]=group=new List<GlossaryEntry>();
                group.Add(row);
            }
            var drop=new HashSet<GlossaryEntry>();
            foreach(var group in groups.Values){
                if(group.Count<2)continue;
                string first=(group[0].replacement??"").Trim();
                bool same=group.TrueForAll(delegate(GlossaryEntry row){return (row.replacement??"").Trim()==first;});
                for(int i=1;i<group.Count;i++)if(same)drop.Add(group[i]);
                if(!same)foreach(var row in group)row.matchCase=true;
            }
            return rows.FindAll(delegate(GlossaryEntry row){return !drop.Contains(row);});
        }
        internal static void Save(IEnumerable<GlossaryEntry> rows){
            var entries=Validate(rows);Directory.CreateDirectory(Preferences.DataDirectory);
            string temporary=Path.Combine(Preferences.DataDirectory,"glossary-"+Guid.NewGuid().ToString("N")+".tmp");
            try{File.WriteAllText(temporary,Json.Encode(new{version=2,entries=entries}),new UTF8Encoding(false));
                if(File.Exists(FilePath))File.Replace(temporary,FilePath,null);else File.Move(temporary,FilePath);
            }finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
    internal sealed class GlossaryForm:Form {
        private readonly DataGridView grid;
        private readonly TableLayoutPanel layout;
        private readonly FlowLayoutPanel actions;
        private readonly List<GlossaryTarget> targetsList=new List<GlossaryTarget>();
        private ContextMenuStrip targetMenu;
        internal GlossaryForm(){
            SuspendLayout();Ui.ApplyIcon(this);
            Text=Ui.T("术语词典","Glossary");Size=new Size(800,520);MinimumSize=new Size(780,440);StartPosition=FormStartPosition.CenterParent;
            BackColor=Theme.Cream;ForeColor=Theme.Ink;Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;
            layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=1,RowCount=3};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,100));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            layout.Controls.Add(new Label{Dock=DockStyle.Fill,Text=Ui.T("内置 Claude 名称与常用技术术语，在线和离线都生效。\n默认不区分大小写；勾选「区分大小写」的词条只认一模一样的写法。\n较长词组优先，链接和代码保持原样；「补充内置词条」保留你的译法。\n点击保存才会生效。只保存词典，翻译记录不会保存到磁盘。","Built-in Claude names and technical terms work online and offline.\nTerms ignore case unless Match case is ticked.\nLonger phrases win; links and code stay unchanged. Add built-in terms keeps your edits.\nSave applies the changes. Only the glossary is saved; history stays in memory."),ForeColor=Theme.Muted},0,0);
            grid=new DataGridView{Dock=DockStyle.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AllowUserToAddRows=true,AllowUserToDeleteRows=true,RowHeadersWidth=28,SelectionMode=DataGridViewSelectionMode.CellSelect};
            grid.EnableHeadersVisualStyles=false;grid.GridColor=Theme.Line;grid.ColumnHeadersDefaultCellStyle.BackColor=Theme.Mist;grid.ColumnHeadersDefaultCellStyle.ForeColor=Theme.Ink;
            grid.DefaultCellStyle.ForeColor=Theme.Ink;grid.DefaultCellStyle.SelectionBackColor=Theme.Blue;grid.DefaultCellStyle.SelectionForeColor=Theme.Ink;
            grid.RowHeadersDefaultCellStyle.BackColor=Theme.Cream;grid.RowHeadersDefaultCellStyle.SelectionBackColor=Theme.Blue;grid.RowTemplate.Height=34;
            grid.Columns.Add("source",Ui.T("原词 / 词组","Term / phrase"));grid.Columns.Add("replacement",Ui.T("固定译法","Replacement"));
            var targets=new DataGridViewComboBoxColumn{Name="target",HeaderText=Ui.T("译成此语言时生效","Apply when translating to"),DisplayMember="Name",ValueMember="Code",FlatStyle=FlatStyle.Flat,DisplayStyle=DataGridViewComboBoxDisplayStyle.Nothing,ReadOnly=true};
            var choices=targetsList;choices.Add(new GlossaryTarget{Code="*",Name=Ui.T("所有语言","All languages")});
            foreach(var choice in LanguageChoice.All)choices.Add(new GlossaryTarget{Code=choice.Code,Name=choice.Name});
            var entries=GlossaryStore.Load();foreach(var item in entries)if(!choices.Exists(delegate(GlossaryTarget c){return c.Code==item.target;}))choices.Add(new GlossaryTarget{Code=item.target,Name=item.target});
            targets.DataSource=choices;grid.Columns.Add(targets);grid.Columns["target"].FillWeight=115;
            grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="matchCase",HeaderText=Ui.T("区分大小写","Match case"),FillWeight=52,FlatStyle=FlatStyle.Flat});
            grid.CellPainting+=PaintTargetCell;
            grid.CellMouseClick+=delegate(object sender,DataGridViewCellMouseEventArgs args){if(args.Button==MouseButtons.Left&&args.ColumnIndex==2&&args.RowIndex>=0)ShowTargetMenu(args.RowIndex);};
            grid.KeyDown+=delegate(object sender,KeyEventArgs args){if(grid.CurrentCell!=null&&grid.CurrentCell.ColumnIndex==2&&(args.KeyCode==Keys.F4||args.KeyCode==Keys.Space||args.KeyCode==Keys.Enter||(args.Alt&&args.KeyCode==Keys.Down))){args.Handled=true;args.SuppressKeyPress=true;ShowTargetMenu(grid.CurrentCell.RowIndex);}};
            grid.DefaultValuesNeeded+=delegate(object sender,DataGridViewRowEventArgs args){args.Row.Cells["target"].Value="*";args.Row.Cells["matchCase"].Value=false;};
            foreach(var entry in entries)grid.Rows.Add(entry.source,entry.replacement,entry.target,entry.matchCase);
            layout.Controls.Add(grid,0,1);
            actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};
            var save=Theme.Button(Ui.T("保存","Save"),true);save.Click+=delegate{SaveRows();};
            var cancel=Theme.Button(Ui.T("取消","Cancel"),false);cancel.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};
            var remove=Theme.Button(Ui.T("删除此行","Remove row"),false);remove.Width=112;remove.Click+=delegate{if(grid.CurrentRow!=null&&!grid.CurrentRow.IsNewRow)grid.Rows.Remove(grid.CurrentRow);};
            var recommended=Theme.Button(Ui.T("补充内置词条","Add built-in terms"),false);recommended.Width=170;recommended.Click+=delegate{AddRecommended();};
            actions.Controls.AddRange(new Control[]{save,cancel,remove,recommended});layout.Controls.Add(actions,0,2);Controls.Add(layout);CancelButton=cancel;
            AutoScaleDimensions=new SizeF(96F,96F);ResumeLayout(true);
        }
        protected override void OnLoad(EventArgs args){
            base.OnLoad(args);
            // Framework 4 does not reliably scale nested TableLayoutPanel row
            // styles or DataGridView row/header metrics. Set these from the
            // 96-DPI design once the form has its real monitor graphics context.
            layout.SuspendLayout();actions.SuspendLayout();
            layout.Padding=new Padding(Theme.Scale(this,16));
            layout.RowStyles[0].Height=Theme.Scale(this,100);
            layout.RowStyles[2].Height=Theme.Scale(this,52);
            foreach(Control child in layout.Controls)child.Margin=new Padding(Theme.Scale(this,3));
            foreach(Control button in actions.Controls){
                button.Height=Theme.Scale(this,38);
                button.Width=Math.Max(Theme.Scale(this,80),TextRenderer.MeasureText(button.Text,button.Font).Width+Theme.Scale(this,28));
                button.Margin=new Padding(0,Theme.Scale(this,4),Theme.Scale(this,8),Theme.Scale(this,4));
            }
            grid.RowHeadersWidth=Theme.Scale(this,28);
            grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight=Theme.Scale(this,32);
            grid.RowTemplate.Height=Theme.Scale(this,34);
            foreach(DataGridViewRow row in grid.Rows)row.Height=grid.RowTemplate.Height;
            actions.ResumeLayout(true);layout.ResumeLayout(true);
        }
        private void PaintTargetCell(object sender,DataGridViewCellPaintingEventArgs args){
            if(args.ColumnIndex!=2||args.RowIndex<0)return;
            args.Paint(args.ClipBounds,args.PaintParts&~(DataGridViewPaintParts.ContentForeground|DataGridViewPaintParts.Focus));
            var g=args.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;float s=g.DpiX/96F;
            var face=new RectangleF(args.CellBounds.X+5*s,args.CellBounds.Y+4*s,args.CellBounds.Width-10*s,args.CellBounds.Height-8*s);
            if(face.Width>20*s&&face.Height>4*s){
                using(var path=Theme.RoundedRect(face,face.Height/2))using(var fill=new SolidBrush(Color.White))using(var pen=new Pen(Theme.Line,s)){g.FillPath(fill,path);g.DrawPath(pen,path);}
                string code=Convert.ToString(grid.Rows[args.RowIndex].Cells[2].Value),label=Convert.ToString(args.FormattedValue);
                if(String.IsNullOrEmpty(code))label=Ui.T("所有语言","All languages");
                TextRenderer.DrawText(g,label,args.CellStyle.Font,new Rectangle((int)(face.X+8*s),(int)face.Y,Math.Max(1,(int)(face.Width-29*s)),(int)face.Height),Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
                float x=face.Right-12*s,y=face.Y+face.Height/2;
                using(var pen=new Pen(Theme.Muted,1.3F*s))g.DrawLines(pen,new[]{new PointF(x-4*s,y-2*s),new PointF(x,y+2*s),new PointF(x+4*s,y-2*s)});
                if(grid.CurrentCell==grid.Rows[args.RowIndex].Cells[2]&&grid.Focused)using(var path=Theme.RoundedRect(face,face.Height/2))using(var pen=new Pen(Theme.BlueDark,s))g.DrawPath(pen,path);
            }
            args.Handled=true;
        }
        private ContextMenuStrip BuildTargetMenu(int rowIndex){
            var menu=Theme.CreateMenu();if(rowIndex<0||rowIndex>=grid.Rows.Count)return menu;
            var cell=grid.Rows[rowIndex].Cells["target"];string current=Convert.ToString(cell.Value);
            foreach(var choice in targetsList){
                string value=choice.Code;var item=new ToolStripMenuItem(choice.Name){Checked=value==(String.IsNullOrEmpty(current)?"*":current),Tag=value};Theme.MenuItem(item);
                item.Click+=delegate{if(cell.DataGridView!=grid||cell.RowIndex<0)return;grid.EndEdit();if(Convert.ToString(cell.Value)!=value)cell.Value=value;grid.InvalidateCell(cell);};menu.Items.Add(item);
            }return menu;
        }
        private void ShowTargetMenu(int rowIndex){
            if(targetMenu!=null){targetMenu.Dispose();targetMenu=null;}targetMenu=BuildTargetMenu(rowIndex);
            if(targetMenu.Items.Count==0)return;
            Rectangle cell=grid.GetCellDisplayRectangle(2,rowIndex,true);targetMenu.Show(grid,new Point(cell.Left,cell.Bottom));
            foreach(ToolStripMenuItem item in targetMenu.Items)if(item.Checked){item.Select();break;}
        }
        protected override void Dispose(bool disposing){if(disposing&&targetMenu!=null){targetMenu.Dispose();targetMenu=null;}base.Dispose(disposing);}
        private List<GlossaryEntry> ReadRows(){
            grid.EndEdit();var entries=new List<GlossaryEntry>();foreach(DataGridViewRow row in grid.Rows){if(row.IsNewRow)continue;entries.Add(new GlossaryEntry{source=Convert.ToString(row.Cells[0].Value),replacement=Convert.ToString(row.Cells[1].Value),target=Convert.ToString(row.Cells[2].Value),matchCase=row.Cells[3].Value is bool&&(bool)row.Cells[3].Value});}return entries;
        }
        private void AddRecommended(){
            try{var entries=GlossaryStore.MergeRecommended(ReadRows());grid.Rows.Clear();foreach(var entry in entries)grid.Rows.Add(entry.source,entry.replacement,entry.target,entry.matchCase);}
            catch(InvalidOperationException error){MessageBox.Show(this,error.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);}
        }
        private void SaveRows(){
            try{GlossaryStore.Save(ReadRows());DialogResult=DialogResult.OK;Close();}
            catch(Exception error){MessageBox.Show(this,error is InvalidOperationException?error.Message:Ui.T("词典未保存，请检查文件夹写入权限。","Glossary was not saved. Check folder write permissions."),Text,MessageBoxButtons.OK,MessageBoxIcon.Information);}
        }
        private sealed class GlossaryTarget { public string Code{get;set;}public string Name{get;set;} }
    }
}
