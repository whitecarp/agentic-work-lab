using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WhaleClaudeHelper {
    internal static class Ui {
        internal static bool English;
        internal static string T(string zh,string en){return English?en:zh;}
        internal static string DataRoot {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DafeiyuClaudeHelper");}}
        internal static string ConfigRoot {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude");}}
        internal static void Load(){try{English=File.ReadAllText(Path.Combine(DataRoot,"language.txt")).Trim()=="en";}catch{}}
        internal static void Save(){try{Directory.CreateDirectory(DataRoot);File.WriteAllText(Path.Combine(DataRoot,"language.txt"),English?"en":"zh",new UTF8Encoding(false));}catch{}}
    }
    internal static class Program {
        [STAThread] private static void Main(string[] args){
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            bool demo=args.Length==1&&args[0]=="--demo";
            if(!demo)Ui.Load();
            Application.Run(new OptimizerForm(demo));
        }
    }
}
