using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WhalePack.Tests {
    internal static class TestRunner {
        [STAThread]
        private static int Main(string[] args) {
            Program.ConfigureRuntime();
            Console.OutputEncoding = Encoding.UTF8;
            try {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if (args.Length > 0 && args[0] == "--integration") return IntegrationTests.Run(args);
                if (args.Length > 0 && args[0] == "--screenshots") return ScreenshotTests.Run(args);
                int core = CoreTests.Run();
                int media = MediaTests.Run();
                int runtime = RuntimeAndUpdateTests.Run();
                Console.WriteLine("PASS: core=" + core + ", media=" + media + ", runtime-update=" + runtime);
                return 0;
            } catch (Exception ex) {
                Console.Error.WriteLine("FAIL: " + ex);
                return 1;
            }
        }
    }
}
