using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ClaudeCompanion {
    // Keep the original multi-image ICO intact so Windows can choose its separately drawn
    // 16/24/32/48/64/128/256 frame. The executable's shell icon is a different resource.
    internal static class WindowIcon {
        internal const string ResourceName="Dafeiyu.WindowIcon.ico";
        internal static void Apply(Form form) {
            Stream resource=typeof(WindowIcon).Assembly.GetManifestResourceStream(ResourceName);
            if(resource==null)throw new InvalidOperationException("Missing embedded window icon.");
            Icon icon;
            try { icon=new Icon(resource); }
            catch { resource.Dispose();throw; }
            form.Icon=icon;
            form.Disposed+=delegate { icon.Dispose();resource.Dispose(); };
        }
    }
}
