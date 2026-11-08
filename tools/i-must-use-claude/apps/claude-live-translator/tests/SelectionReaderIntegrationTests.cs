using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Automation;
using System.Windows.Forms;

// Test-owned UIA providers only. The fixture does not activate its window,
// synthesize input, or inspect any other application's UI.
internal static class SelectionReaderIntegrationTests
{
    private sealed class HiddenFixtureForm : Form
    {
        protected override bool ShowWithoutActivation { get { return true; } }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Select(TextBox box, string value)
    {
        int start = box.Text.IndexOf(value, StringComparison.Ordinal);
        if (start < 0) throw new Exception("Fixture text not found");
        box.Select(start, value.Length);
    }

    private static string ReadSelectedText(TextBox box)
    {
        MethodInfo method = typeof(SelectionReader).GetMethod("ReadElementSelection", BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null) throw new Exception("SelectionReader provider reader not found");
        AutomationElement provider = AutomationElement.FromHandle(box.Handle);
        object result = method.Invoke(null, new object[] { provider, null, null });
        Type resultType = result.GetType();
        bool ok = (bool)resultType.GetField("Ok").GetValue(result);
        string status = (string)resultType.GetField("Status").GetValue(result);
        string text = (string)resultType.GetField("Text").GetValue(result);
        if (!ok) throw new Exception("SelectionReader returned " + status);
        return text;
    }

    private static object NewClaudeContext(Type contextType, AutomationElement document, string url)
    {
        object context = Activator.CreateInstance(contextType, true);
        contextType.GetField("Document").SetValue(context, document);
        contextType.GetField("Url").SetValue(context, url);
        return context;
    }

    private static object ResolveClaudeContext(AutomationElement source, AutomationElement root,
        AutomationElement firstDocument, AutomationElement secondDocument)
    {
        Type contextType = typeof(SelectionReader).GetNestedType("ClaudeContext", BindingFlags.NonPublic);
        if (contextType == null) throw new Exception("Claude context type not found");
        object detected = Activator.CreateInstance(contextType, true);
        contextType.GetField("Detected").SetValue(detected, true);
        contextType.GetField("Root").SetValue(detected, root);
        IList documents = (IList)contextType.GetField("Documents").GetValue(detected);
        documents.Add(NewClaudeContext(contextType, firstDocument, "https://claude.ai/chat/11111111-1111-1111-1111-111111111111"));
        documents.Add(NewClaudeContext(contextType, secondDocument, "https://claude.ai/chat/22222222-2222-2222-2222-222222222222"));

        MethodInfo resolve = typeof(SelectionReader).GetMethod("GetClaudeContextForSource", BindingFlags.NonPublic | BindingFlags.Static);
        if (resolve == null) throw new Exception("Claude source context resolver not found");
        return resolve.Invoke(null, new object[] { source, detected });
    }

    [STAThread]
    public static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            using (var form = new HiddenFixtureForm())
            using (var first = new TextBox())
            using (var second = new TextBox())
            using (var unrelated = new TextBox())
            {
                form.Text = "SelectionReader owned fixture";
                form.ClientSize = new Size(500, 180);
                form.ShowInTaskbar = false;
                form.Opacity = 0;

                first.Multiline = true;
                first.Text = "First header\r\nAlpha selected range\r\nFirst footer";
                first.Bounds = new Rectangle(8, 8, 480, 72);
                second.Multiline = true;
                second.Text = "Second header\r\nBeta selected range\r\nSecond footer";
                second.Bounds = new Rectangle(8, 92, 480, 72);
                unrelated.Multiline = true;
                unrelated.Text = "Unrelated sibling";
                unrelated.Bounds = new Rectangle(8, 176, 480, 32);
                form.Controls.Add(first);
                form.Controls.Add(second);
                form.Controls.Add(unrelated);

                // Materialize the test-owned HWND/UIA providers without activating the form.
                form.Show();
                Application.DoEvents();
                Select(first, "Alpha selected range");
                Equal("Alpha selected range", ReadSelectedText(first), "first TextPattern provider returns only its selected range");

                first.Select(0, 0);
                Select(second, "Beta selected range");
                Equal("Beta selected range", ReadSelectedText(second), "second TextPattern provider is scoped independently");

                AutomationElement root = AutomationElement.FromHandle(form.Handle);
                AutomationElement firstProvider = AutomationElement.FromHandle(first.Handle);
                AutomationElement secondProvider = AutomationElement.FromHandle(second.Handle);
                AutomationElement unrelatedProvider = AutomationElement.FromHandle(unrelated.Handle);
                object fromFirst = ResolveClaudeContext(firstProvider, root, firstProvider, secondProvider);
                object fromSecond = ResolveClaudeContext(secondProvider, root, firstProvider, secondProvider);
                object fromSibling = ResolveClaudeContext(unrelatedProvider, root, firstProvider, secondProvider);
                Type contextType = typeof(SelectionReader).GetNestedType("ClaudeContext", BindingFlags.NonPublic);
                Check(Object.ReferenceEquals(contextType.GetField("Document").GetValue(fromFirst), firstProvider), "first source resolves to its document context");
                Check(Object.ReferenceEquals(contextType.GetField("Document").GetValue(fromSecond), secondProvider), "second source resolves past the first context");
                Check(fromSibling == null, "unrelated sibling has no Claude document context");

                Console.WriteLine("SelectionReaderIntegrationTests passed");
                return 0;
            }
        }
        catch (Exception error)
        {
            Console.WriteLine("SelectionReaderIntegrationTests failed: " + error.GetBaseException().Message);
            return 1;
        }
    }

    private static void Equal(string expected, string actual, string label)
    {
        if (!String.Equals(expected, actual, StringComparison.Ordinal))
            throw new Exception(label + ": expected <" + expected + ">, actual <" + actual + ">");
    }
}
