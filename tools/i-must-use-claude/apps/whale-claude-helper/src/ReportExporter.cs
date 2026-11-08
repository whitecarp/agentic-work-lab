using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;

namespace WhaleClaudeHelper {
    internal static class ReportExporter {
        // Never overwrite a file, write into configuration/backups, or follow linked ancestors.
        // The explicit projection is intentional: future scan fields cannot accidentally leak secrets.
        public static void SaveNew(string path,RepairScan scan,string configRoot,string dataRoot){
            if(scan==null)throw new InvalidDataException("missing_scan");
            string output=CanonicalPlainPath(path),configuration=CanonicalPlainPath(configRoot),data=CanonicalPlainPath(dataRoot);
            string backups=Path.Combine(data,"Backups");
            if(Within(output,configuration)||Within(output,backups))throw new InvalidDataException("report_destination_protected");
            if(!String.IsNullOrEmpty(scan.SettingsPath)&&String.Equals(output,CanonicalPlainPath(scan.SettingsPath),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("report_destination_protected");
            if(File.Exists(output)||Directory.Exists(output))throw new IOException("report_already_exists");
            var payload=new {
                version="0.1.0",readOnly=true,
                configuration=new {
                    scan.SettingsPath,scan.SettingsError,scan.SettingsSha256,scan.EnvironmentError,scan.SettingsExists,scan.SettingsValid,
                    OfficialSettingKeys=scan.OfficialSettingKeys.ToArray(),ProxySettingKeys=scan.ProxySettingKeys.ToArray(),
                    UserEnvironmentKeys=scan.UserEnvironmentKeys.ToArray(),OtherOverrideKeys=scan.OtherOverrideKeys.ToArray()
                }
            };
            byte[] bytes=new UTF8Encoding(false,true).GetBytes(new JavaScriptSerializer{MaxJsonLength=2097152}.Serialize(payload));
            var sid=WindowsIdentity.GetCurrent().User;var security=new FileSecurity();security.SetAccessRuleProtection(true,false);security.SetOwner(sid);
            security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,AccessControlType.Allow));
            RepairEngine.AssertPlainPath(output);
            using(var stream=new FileStream(output,FileMode.CreateNew,FileSystemRights.FullControl,FileShare.None,4096,FileOptions.None,security)){
                stream.Write(bytes,0,bytes.Length);stream.Flush(true);
            }
        }
        private static bool Within(string path,string directory){return String.Equals(path,directory,StringComparison.OrdinalIgnoreCase)||path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
        private static string CanonicalPlainPath(string value){
            if(String.IsNullOrWhiteSpace(value))throw new InvalidDataException("invalid_report_path");
            string full;
            try{full=Path.GetFullPath(value);}catch(ArgumentException){throw new InvalidDataException("invalid_report_path");}catch(NotSupportedException){throw new InvalidDataException("invalid_report_path");}catch(PathTooLongException){throw new InvalidDataException("invalid_report_path");}
            string tail=full.Substring(Path.GetPathRoot(full).Length);
            foreach(string part in tail.Split(new[]{Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar},StringSplitOptions.RemoveEmptyEntries)){
                // Win32 aliases such as folder. and alternate data streams must not bypass scope checks.
                if(part.EndsWith(".",StringComparison.Ordinal)||part.EndsWith(" ",StringComparison.Ordinal)||part.IndexOf(':')>=0)throw new InvalidDataException("invalid_report_path");
                string stem=part.Split('.')[0];
                if(System.Text.RegularExpressions.Regex.IsMatch(stem,"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))throw new InvalidDataException("invalid_report_path");
            }
            RepairEngine.AssertPlainPath(full);
            return full.Length>Path.GetPathRoot(full).Length?full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar):full;
        }
    }
}
