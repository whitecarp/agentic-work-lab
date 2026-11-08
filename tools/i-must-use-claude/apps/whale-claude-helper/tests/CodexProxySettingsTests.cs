using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using WhaleClaudeHelper;

internal static class CodexProxySettingsTests {
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private const string Http = "http://127.0.0.1:7890", All = "socks5h://127.0.0.1:7891";
    private static string root;
    private static int checks;

    private sealed class Fixture {
        internal string Directory, Config, Env, Backup, Recycled;
        internal int Recycles;
        internal Fixture(string name, byte[] bytes) {
            Directory = Path.Combine(root, name); Config = Path.Combine(Directory, "codex");
            Env = Path.Combine(Config, ".env"); Backup = Path.Combine(Directory, "backups");
            Recycled = Path.Combine(Directory, "fixture-recycle-bin");
            System.IO.Directory.CreateDirectory(Config);
            if (bytes != null) File.WriteAllBytes(Env, bytes);
        }
        internal Fixture(string name, string text) : this(name, text == null ? null : Utf8.GetBytes(text)) { }
        internal CodexProxySettings Store(Action hook = null, Action<string> mutationHook = null) {
            return new CodexProxySettings(Config, Backup, delegate(string path) {
                Check(path.StartsWith(Config + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(path) == "env.pending.bin" && Path.GetFileName(Path.GetDirectoryName(path)).StartsWith(".whale-codex-undo-"),
                    "recycler receives only captured file in private staging");
                System.IO.Directory.CreateDirectory(Recycled);
                File.Move(path, Path.Combine(Recycled, "env-" + Recycles + ".bin")); Recycles++;
            }, hook, mutationHook);
        }
        internal byte[] Bytes() { return File.ReadAllBytes(Env); }
        internal string Text() { return Utf8.GetString(Bytes()); }
    }
    private static void Check(bool condition, string message) {
        checks++; if (!condition) throw new Exception("Check failed: " + message);
    }
    private static void Exact(byte[] expected, byte[] actual, string message) { Check(expected.SequenceEqual(actual), message); }
    private static byte[] WithBom(string text) { return new byte[] {239, 187, 191}.Concat(Utf8.GetBytes(text)).ToArray(); }
    private static Dictionary<string, object> Receipt(string directory) {
        return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(directory, "receipt.json")));
    }

    private static void PreservationAndUndo() {
        string original = "# 秘钥和格式保留\r\nAPI_TOKEN='fixture-PRIVATE-value'  # never report\n" +
            "  export HTTP_PROXY = \"http://old.invalid:123\"  # keep proxy comment\r\n" +
            "HTTPS_PROXY='old'\nALL_PROXY = old\rWSS_PROXY = 'old' # ws\r\n" +
            "no_proxy=old\nKEEP=  unquoted text  # keep spacing\r\nLAST=尾巴";
        byte[] bytes = WithBom(original); var f = new Fixture("preserve", bytes); var store = f.Store();
        string sentinel = Path.Combine(f.Config, "config.toml"); File.WriteAllText(sentinel, "protected", Utf8);
        string sibling = Path.Combine(f.Directory, "other-codex.env"); File.WriteAllText(sibling, "HTTP_PROXY=untouched", Utf8);
        var changed = store.Apply(Http, All);
        Check(changed.Success && changed.Changed && changed.BackupDirectory != null, "existing env applied with backup");
        Exact(bytes, File.ReadAllBytes(Path.Combine(changed.BackupDirectory, "env.before.bin")), "complete original backup is byte exact");
        string text = f.Text();
        Check(text.StartsWith("\uFEFF# 秘钥和格式保留\r\nAPI_TOKEN='fixture-PRIVATE-value'  # never report\n"), "BOM Unicode comments and untouched secret bytes preserved");
        Check(text.Contains("  export HTTP_PROXY = \"" + Http + "\"  # keep proxy comment\r\n"), "export spacing quotes inline comment CRLF preserved");
        Check(text.Contains("HTTPS_PROXY='" + Http + "'\nALL_PROXY = " + All + "\rWSS_PROXY = '" + Http + "' # ws\r\n"), "mixed newline and WSS preserved");
        Check(text.Contains("KEEP=  unquoted text  # keep spacing\r\nLAST=尾巴\r\n"), "unrelated suffix remains exact before appended keys");
        Check(!text.EndsWith("\r") && !text.EndsWith("\n"), "missing terminal newline stays missing");
        Check(!text.Contains("wss_proxy="), "absent WSS variant is not introduced");
        foreach (string key in new[] {"HTTP_PROXY", "http_proxy", "HTTPS_PROXY", "https_proxy", "ALL_PROXY", "all_proxy", "NO_PROXY", "no_proxy"}) Check(text.Contains(key), "both canonical cases present " + key);
        string receipt = File.ReadAllText(Path.Combine(changed.BackupDirectory, "receipt.json"));
        Check(!receipt.Contains("PRIVATE") && !receipt.Contains(Http) && !receipt.Contains(All) && !receipt.Contains("API_TOKEN"), "receipt contains no env values or proxy URI");
        Check((string)Receipt(changed.BackupDirectory)["State"] == "applied", "applied receipt persisted");
        Check(File.ReadAllText(sentinel) == "protected" && File.ReadAllText(sibling) == "HTTP_PROXY=untouched", "other files and config instances untouched");
        byte[] after = f.Bytes(); int backups = store.ListBackups().Count;
        var again = store.Apply(Http, All);
        Check(again.Success && !again.Changed && again.BackupDirectory == null, "repeat apply is no-op");
        Exact(after, f.Bytes(), "no-op byte exact"); Check(store.ListBackups().Count == backups, "no-op does not create extra backup");
        var undone = store.Restore(changed.BackupDirectory);
        Check(undone.Success && undone.Changed, "existing env undo succeeds"); Exact(bytes, f.Bytes(), "undo restores original bytes exactly");
        var twice = store.Restore(changed.BackupDirectory); Check(twice.Success && !twice.Changed, "restore idempotent");
        Check(f.Recycles == 0, "restoring existing file does not invoke recycler");
    }

    private static void NewlinesAndDuplicates() {
        foreach (string nl in new[] {"\n", "\r\n", "\r"}) {
            var f = new Fixture("newline-" + nl.Length + "-" + (int)nl[0], "KEEP=原样" + nl);
            Check(f.Store().Apply(Http, Http).Success, "newline variant applies");
            string text = f.Text(); Check(text.EndsWith(nl), "terminal newline preserved");
            string without = text.Replace(nl, ""); Check(!without.Contains("\r") && !without.Contains("\n"), "new keys use existing newline convention");
            Check(!text.Contains("WSS_PROXY") && !text.Contains("wss_proxy"), "WSS not created");
        }
        string source = "HTTP_PROXY=old\nHTTP_PROXY='other' # duplicate\nhttp_proxy=third\nHtTp_PrOxY=fourth\n" +
            "WSS_PROXY=old\nwss_proxy=old\nNO_PROXY=external\nno_proxy=other\nALL_PROXY=old\nall_proxy=old\n";
        var duplicate = new Fixture("duplicates", source); Check(duplicate.Store().Apply(Http, All).Success, "duplicates updated safely");
        string output = duplicate.Text();
        Check(output.Contains("HTTP_PROXY=" + Http + "\nHTTP_PROXY='" + Http + "' # duplicate\nhttp_proxy=" + Http + "\nHtTp_PrOxY=" + Http + "\n"), "all duplicate and mixed case occurrences synchronized");
        Check(output.Contains("WSS_PROXY=" + Http + "\nwss_proxy=" + Http), "both existing WSS keys synchronized");
        Check(output.Contains("NO_PROXY=localhost,127.0.0.1,::1\nno_proxy=localhost,127.0.0.1,::1\n"), "NO_PROXY exact canonical local list");
        Check(!output.Contains("old") && !output.Contains("other") && !output.Contains("third") && !output.Contains("fourth"), "no ambiguous old duplicate values remain");
        var allKeys = new Fixture("all-present-no-newline", "HTTP_PROXY=old\nhttp_proxy=old\nHTTPS_PROXY=old\nhttps_proxy=old\nALL_PROXY=old\nall_proxy=old\nNO_PROXY=old\nno_proxy=old");
        Check(allKeys.Store().Apply(Http, Http).Success && !allKeys.Text().EndsWith("\n"), "no added newline when all canonical keys already exist");
    }

    private static void RejectMalformed() {
        string[] bad = {
            "HTTP_PROXY=\"one\ntwo\"\n", "HTTP_PROXY='one\ntwo'\n", "HTTP_PROXY=value\\\nKEEP=x\n",
            "HTTP_PROXY value\n", "HTTP_PROXY=\"value\" trailing\n", "HTTP_PROXY=value`command`\n",
            "KEEP=\"unfinished\n", "KEEP=x\0y", "KEEP=x\uFEFFy", "invalid command", "9INVALID=x", "export HTTP_PROXY old", "HTTP_PROXY=a'b"
        };
        for (int i = 0; i < bad.Length; i++) {
            var f = new Fixture("malformed-" + i, bad[i]); byte[] original = f.Bytes(); var result = f.Store().Apply(Http, All);
            Check(!result.Success && !result.Changed && result.BackupDirectory == null, "malformed input rejected before backup " + i);
            Exact(original, f.Bytes(), "malformed env untouched " + i);
            Check(!Directory.Exists(f.Backup), "malformed input causes no backup writes " + i);
        }
        byte[][] encodings = {new byte[] {0xC3, 0x28}, new byte[] {0xff, 0xfe, 0x61, 0}, new byte[] {0xc0, 0xaf}, new byte[] {0xed, 0xa0, 0x80}};
        for (int i = 0; i < encodings.Length; i++) {
            var f = new Fixture("invalid-encoding-" + i, encodings[i]); var result = f.Store().Apply(Http, All);
            Check(!result.Success && result.Error == "codex_invalid_utf8", "non UTF8 encoding rejected " + i); Exact(encodings[i], f.Bytes(), "invalid encoding preserved");
        }
        var big = new Fixture("too-large", new byte[2097153]); Check(big.Store().Apply(Http, All).Error == "codex_env_too_large", "bounded env read");
        string[] invalidUris = {"http://example.com:80", "https://127.0.0.1:443", "socks5://127.0.0.1:7890", "http://user:pass@127.0.0.1:7890", "http://127.0.0.1:0", "http://127.0.0.1:65536", "http://127.0.0.1:80/path", "http://127.0.0.1:80\nHTTP_PROXY=evil", "http://127.01.0.1:80", "http://127.999.1.1:80", " http://127.0.0.1:80", "http://127.0.0.1:80/", "http://localhost", "socks5h://[::1]"};
        var invalid = new Fixture("bad-uri", "KEEP=ok\n");
        foreach (string uri in invalidUris) Check(invalid.Store().Apply(Http, uri).Error == "codex_invalid_proxy_uri", "unsafe proxy URI rejected");
        Check(invalid.Store().Apply(All, All).Error == "codex_invalid_proxy_uri", "HTTP target requires HTTP proxy");
        Check(!Directory.Exists(invalid.Backup) && invalid.Text() == "KEEP=ok\n", "invalid URI has no side effects");
        string[] validUris = {"http://localhost:80", "http://[::1]:8080", "http://127.0.0.2:8080", "HTTP://LOCALHOST:80", "http://127.0.0.1:1", "http://127.0.0.1:65535"};
        for (int i = 0; i < validUris.Length; i++) {
            // The root is already unique. A second GUID here pushes receipt staging
            // paths beyond legacy .NET MAX_PATH under longer CI user profiles.
            string uri = validUris[i]; var f = new Fixture("valid-uri-" + i, "");
            var accepted = f.Store().Apply(uri, "socks5h://[::1]:1080");
            Check(accepted.Success, "loopback URI accepted " + uri + "; error=" + accepted.Error + "; changed=" + accepted.Changed);
            Check(f.Text().Contains("HTTP_PROXY=" + uri + "\r\n"), "explicit HTTP port and original spelling preserved " + uri);
            Check(f.Text().Contains("ALL_PROXY=socks5h://[::1]:1080\r\n"), "explicit SOCKS IPv6 port preserved");
        }
    }

    private static void MissingAndRecycle() {
        var f = new Fixture("new-env", (string)null); var store = f.Store();
        var applied = store.Apply(Http, All); Check(applied.Success && applied.Changed, "missing env created");
        Check(!File.Exists(Path.Combine(applied.BackupDirectory, "env.before.bin")), "missing source not represented as empty file");
        Check(!(bool)Receipt(applied.BackupDirectory)["OriginalExists"] && (string)Receipt(applied.BackupDirectory)["BeforeSha256"] == "absent", "missing state recorded");
        byte[] created = f.Bytes(); Check(store.Restore(applied.BackupDirectory).Success, "new env undo succeeds");
        Check(!File.Exists(f.Env) && f.Recycles == 1, "new env moved with injected recycler");
        Exact(created, File.ReadAllBytes(Path.Combine(f.Recycled, "env-0.bin")), "recycled generated file retained exactly");
        Exact(created, File.ReadAllBytes(Path.Combine(applied.BackupDirectory, "env.created.bin")), "extra recovery copy of generated env verified");
        Check(store.Restore(applied.BackupDirectory).Success && f.Recycles == 1, "new file undo idempotent");
        var empty = new Fixture("empty-existing", new byte[0]); var r = empty.Store().Apply(Http, All);
        Check(empty.Store().Restore(r.BackupDirectory).Success && File.Exists(empty.Env) && empty.Bytes().Length == 0 && empty.Recycles == 0, "empty original restored as existing empty file");
        var bom = new Fixture("bom-only", new byte[] {239,187,191}); r = bom.Store().Apply(Http, All);
        Check(r.Success && bom.Bytes().Take(3).SequenceEqual(new byte[] {239,187,191}), "BOM-only original encoding retained");
        Check(bom.Store().Restore(r.BackupDirectory).Success && bom.Bytes().Length == 3, "BOM-only original restored exactly");
        var fail = new Fixture("recycle-fails", (string)null); r = fail.Store().Apply(Http, All); byte[] before = fail.Bytes();
        var failing = new CodexProxySettings(fail.Config, fail.Backup, delegate(string path) { throw new IOException("synthetic error"); }, null);
        var result = failing.Restore(r.BackupDirectory);
        Check(!result.Success && result.Changed && File.Exists(fail.Env), "failed recycler reports staged mutation honestly"); Exact(before, fail.Bytes(), "failed recycler returns generated env without overwriting");
        Check(File.Exists(result.RecoveryPath), "failed recycler retains independent verified recovery copy");
        var noOpFixture = new Fixture("recycle-noop", (string)null); var noOpApplied = noOpFixture.Store().Apply(Http, All);
        var noop = new CodexProxySettings(noOpFixture.Config, noOpFixture.Backup, delegate(string path) { }, null);
        Check(noop.Restore(noOpApplied.BackupDirectory).Error == "codex_recycle_incomplete", "no-op recycler detected");
        var missingDirectory = new Fixture("missing-directory", (string)null); Directory.Move(missingDirectory.Config, missingDirectory.Config + "-empty-original");
        Check(missingDirectory.Store().Apply(Http, All).Success && File.Exists(missingDirectory.Env), "missing selected config directory created");
    }

    private static void ConcurrentAndRestoreConflicts() {
        var f = new Fixture("concurrent-apply", "KEEP=before\n"); byte[] original = f.Bytes();
        var applied = f.Store(delegate { File.AppendAllText(f.Env, "EXTERNAL=concurrent\n", Utf8); }).Apply(Http, All);
        Check(!applied.Success && !applied.Changed && applied.Error == "codex_env_changed", "concurrent apply edit rejected");
        Check(f.Text() == "KEEP=before\nEXTERNAL=concurrent\n", "concurrent edit preserved");
        Exact(original, File.ReadAllBytes(Path.Combine(applied.BackupDirectory, "env.before.bin")), "pre-conflict original backup retained");
        Check((string)Receipt(applied.BackupDirectory)["State"] == "apply_failed", "failed preparation receipt accurate");
        var absent = new Fixture("concurrent-create", (string)null);
        var created = absent.Store(delegate { File.WriteAllText(absent.Env, "EXTERNAL=new\n", Utf8); }).Apply(Http, All);
        Check(!created.Success && !created.Changed && absent.Text() == "EXTERNAL=new\n", "concurrent creation never overwritten");
        var c = new Fixture("restore-conflict", "KEEP=original\n"); var store = c.Store(); applied = store.Apply(Http, All);
        File.AppendAllText(c.Env, "LATER=preserve\n", Utf8); byte[] edited = c.Bytes();
        var restored = store.Restore(applied.BackupDirectory);
        Check(!restored.Success && !restored.Changed && restored.Error == "codex_restore_conflict", "undo refuses subsequent edits"); Exact(edited, c.Bytes(), "undo conflict preserves latest bytes");
        var during = new Fixture("during-restore", "KEEP=original\n"); applied = during.Store().Apply(Http, All);
        restored = during.Store(delegate { File.AppendAllText(during.Env, "LATER=preserve\n", Utf8); }).Restore(applied.BackupDirectory);
        Check(!restored.Success && !restored.Changed && during.Text().EndsWith("LATER=preserve\n"), "restore rechecks after preparation");
        var deleted = new Fixture("external-deleted", "KEEP=x"); applied = deleted.Store().Apply(Http, All);
        File.Move(deleted.Env, Path.Combine(deleted.Directory, "user-moved.env"));
        Check(deleted.Store().Restore(applied.BackupDirectory).Error == "codex_restore_conflict" && !File.Exists(deleted.Env), "undo does not recreate externally removed original");
        var newChanged = new Fixture("new-changed", (string)null); applied = newChanged.Store().Apply(Http, All); File.AppendAllText(newChanged.Env, "LATER=x", Utf8);
        Check(newChanged.Store().Restore(applied.BackupDirectory).Error == "codex_restore_conflict" && newChanged.Recycles == 0, "new env edits block recycle undo");
        var locked = new Fixture("locked", "KEEP=x");
        using (var stream = new FileStream(locked.Env, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) {
            Check(!locked.Store().Apply(Http, All).Success, "open writer prevents apply");
        }
        Check(locked.Text() == "KEEP=x", "writer lock preserves original");
        var stacked = new Fixture("stacked", "KEEP=original"); var stackedStore = stacked.Store();
        var first = stackedStore.Apply(Http, All); var second = stackedStore.Apply("http://127.0.0.1:8080", "socks5h://127.0.0.1:1080");
        Check(stackedStore.Restore(first.BackupDirectory).Error == "codex_restore_conflict", "older undo cannot overwrite newer proxy change");
        Check(stackedStore.Restore(second.BackupDirectory).Success && stacked.Text().Contains(Http), "latest undo returns preceding proxy state");
        Check(stackedStore.Restore(first.BackupDirectory).Success && stacked.Text() == "KEEP=original", "ordered undo restores original env");
    }

    private static void BackupAndPathGuards() {
        var f = new Fixture("scope", "SECRET=fixture-private\n"); var store = f.Store(); var r = store.Apply(Http, All);
        Check(store.ListBackups().Contains(r.BackupDirectory), "own backup listed");
        var other = new Fixture("foreign", "KEEP=foreign\n"); var foreign = new CodexProxySettings(other.Config, f.Backup, delegate(string path) { throw new Exception("must not recycle"); }, null);
        Check(foreign.ListBackups().Count == 0, "foreign target backup excluded");
        Check(foreign.Restore(r.BackupDirectory).Error == "codex_backup_target_mismatch" && other.Text() == "KEEP=foreign\n", "cross target restore rejected");
        Check(!store.Restore(root).Success && !store.Restore(r.BackupDirectory + "\\..").Success, "outside and traversal backup paths rejected");
        byte[] after = f.Bytes(); File.AppendAllText(Path.Combine(r.BackupDirectory, "env.before.bin"), "tamper", Utf8);
        Check(store.Restore(r.BackupDirectory).Error == "codex_backup_hash_mismatch", "tampered full backup rejected"); Exact(after, f.Bytes(), "tampered backup cannot change env");
        string[] bad = { f.Config + ".", f.Config + " ", f.Env + ":stream", Path.Combine(f.Config, "..", "alias"), "relative", "\\\\server\\share\\codex", "C:relative", Path.Combine(f.Directory, "NUL.txt") };
        foreach (string path in bad) { bool denied = false; try { new CodexProxySettings(path, f.Backup); } catch { denied = true; } Check(denied, "unsafe config path rejected"); }
        foreach (string path in new[] {f.Config, Path.Combine(f.Config, "backups"), f.Directory}) {
            bool denied = false; try { new CodexProxySettings(f.Config, path); } catch { denied = true; } Check(denied, "overlapping backup tree rejected");
        }
        var directory = new Fixture("env-directory", (string)null); Directory.CreateDirectory(directory.Env);
        Check(directory.Store().Apply(Http, All).Error == "codex_env_is_directory", "directory named .env rejected");
        var hard = new Fixture("hardlink", (string)null); string protectedPath = Path.Combine(hard.Directory, "protected.env"); File.WriteAllText(protectedPath, "SECRET=fixture-original", Utf8);
        Check(CreateHardLink(hard.Env, protectedPath, IntPtr.Zero), "hardlink fixture created");
        Check(hard.Store().Apply(Http, All).Error == "codex_hardlinked_path_not_supported", "hardlinked source rejected");
        Check(File.ReadAllText(protectedPath) == "SECRET=fixture-original" && !Directory.Exists(hard.Backup), "hardlinked source and external alias retained");
        var backup = new Fixture("hardlink-backup", "KEEP=x"); r = backup.Store().Apply(Http, All);
        Check(CreateHardLink(Path.Combine(backup.Directory, "before-alias"), Path.Combine(r.BackupDirectory, "env.before.bin"), IntPtr.Zero), "backup hardlink created");
        Check(backup.Store().Restore(r.BackupDirectory).Error == "codex_hardlinked_path_not_supported", "hardlinked backup rejected");
        var receipt = new Fixture("hardlink-receipt", "KEEP=x"); r = receipt.Store().Apply(Http, All);
        Check(CreateHardLink(Path.Combine(receipt.Directory, "receipt-alias"), Path.Combine(r.BackupDirectory, "receipt.json"), IntPtr.Zero), "receipt hardlink created");
        Check(receipt.Store().ListBackups().Count == 0 && !receipt.Store().Restore(r.BackupDirectory).Success, "hardlinked receipt not listed or restored");
        var linkTarget = new Fixture("symlink-target", "KEEP=protected"); string link = Path.Combine(root, "linked-codex");
        if (CreateSymbolicLink(link, linkTarget.Config, 3)) {
            bool denied = false; try { new CodexProxySettings(link, Path.Combine(root, "linked-backups")); } catch { denied = true; }
            Check(denied && linkTarget.Text() == "KEEP=protected", "symlink config rejected");
        } else Console.WriteLine("Symbolic link fixture unavailable (privilege/developer mode); hardlink and alias guards tested.");
        string junction = Path.Combine(root, "junction-codex");
        var junctionStart = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
            "/d /c mklink /J \"" + junction + "\" \"" + linkTarget.Config + "\"") {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        using (var process = Process.Start(junctionStart)) {
            process.StandardOutput.ReadToEnd(); process.StandardError.ReadToEnd(); process.WaitForExit();
            Check(process.ExitCode == 0, "directory junction fixture created without elevation");
        }
        bool junctionDenied = false;
        try { new CodexProxySettings(junction, Path.Combine(root, "junction-backups")); }
        catch (InvalidDataException error) { junctionDenied = error.Message == "codex_linked_path_not_supported"; }
        Check(junctionDenied && linkTarget.Text() == "KEEP=protected", "reparse directory junction rejected without touching target");
        var swapped = new Fixture("swap-to-junction", "KEEP=original");
        var swappedResult = swapped.Store(delegate {
            Directory.Move(swapped.Config, swapped.Config + "-original");
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                "/d /c mklink /J \"" + swapped.Config + "\" \"" + linkTarget.Config + "\"") {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var process = Process.Start(start)) {
                process.StandardOutput.ReadToEnd(); process.StandardError.ReadToEnd(); process.WaitForExit();
                Check(process.ExitCode == 0, "concurrent junction fixture created");
            }
        }).Apply(Http, All);
        Check(!swappedResult.Success && !swappedResult.Changed && swappedResult.Error == "codex_linked_path_not_supported", "reparse introduced after preparation rejected");
        Check(linkTarget.Text() == "KEEP=protected" && File.ReadAllText(Path.Combine(swapped.Config + "-original", ".env")) == "KEEP=original", "concurrent reparse preserves both source and linked target");
        var invalidReceipt = new Fixture("invalid-receipt", "KEEP=x"); r = invalidReceipt.Store().Apply(Http, All);
        File.WriteAllText(Path.Combine(r.BackupDirectory, "receipt.json"), "{\"Schema\":99}", Utf8);
        Check(invalidReceipt.Store().ListBackups().Count == 0 && invalidReceipt.Store().Restore(r.BackupDirectory).Error == "codex_invalid_backup_receipt", "invalid receipt skipped and restore rejected");
    }

    private static void EditorReplace(Fixture f, byte[] bytes) {
        string candidate = Path.Combine(f.Directory, "editor-next-" + Guid.NewGuid().ToString("N") + ".env");
        string previous = Path.Combine(f.Directory, "editor-previous-" + Guid.NewGuid().ToString("N") + ".env");
        File.WriteAllBytes(candidate, bytes); File.Replace(candidate, f.Env, previous);
    }

    private static void NarrowMutationRaces() {
        var apply = new Fixture("replace-race", "KEEP=original\n");
        byte[] later = WithBom("SECRET=fixture-LATER-EDIT\r\n");
        var raced = apply.Store(null, delegate(string phase) { if (phase == "replace") EditorReplace(apply, later); }).Apply(Http, All);
        Check(!raced.Success && raced.Changed && raced.Error == "codex_concurrent_edit_preserved", "replacement race reports partial write and preserved conflict");
        Check(raced.RecoveryPath != null && Path.GetDirectoryName(raced.RecoveryPath) == raced.BackupDirectory, "actual displaced file retained in transaction backup");
        Exact(later, File.ReadAllBytes(raced.RecoveryPath), "actual later edit displaced after identity check retained byte exact");
        Check(apply.Text().Contains(Http), "failed raced apply reports actual committed output without claiming rollback");
        string receiptText = File.ReadAllText(Path.Combine(raced.BackupDirectory, "receipt.json"));
        Check(!receiptText.Contains("LATER-EDIT") && !receiptText.Contains("SECRET="), "recovery receipt excludes captured secrets");
        Check((bool)Receipt(raced.BackupDirectory)["ReviewRequired"] && (string)Receipt(raced.BackupDirectory)["State"] == "apply_failed_after_write", "race recorded as manual recovery required");
        byte[] current = apply.Bytes(); var retry = apply.Store().Restore(raced.BackupDirectory);
        Check(!retry.Success && !retry.Changed && retry.Error == "codex_concurrent_edit_preserved", "ordinary undo cannot discard unresolved captured edit");
        Exact(current, apply.Bytes(), "retry leaves current target alone pending recovery");

        var doubleRace = new Fixture("replace-race-followed-by-edit", "KEEP=original\n");
        byte[] newest = Utf8.GetBytes("KEEP=even-newer-external-edit\n");
        raced = doubleRace.Store(null, delegate(string phase) {
            if (phase == "replace") EditorReplace(doubleRace, later);
            if (phase == "replaced") EditorReplace(doubleRace, newest);
        }).Apply(Http, All);
        Check(!raced.Success && raced.Changed, "double race reports partial mutation");
        Exact(later, File.ReadAllBytes(raced.RecoveryPath), "first raced edit retained after second edit");
        Exact(newest, doubleRace.Bytes(), "no automatic rollback overwrites edit following our replace");

        var restore = new Fixture("restore-replace-race", "KEEP=original\n"); var initial = restore.Store().Apply(Http, All);
        raced = restore.Store(null, delegate(string phase) { if (phase == "replace") EditorReplace(restore, later); }).Restore(initial.BackupDirectory);
        Check(!raced.Success && raced.Changed && raced.Error == "codex_concurrent_edit_preserved", "existing-file restore catches same replacement race");
        Exact(later, File.ReadAllBytes(raced.RecoveryPath), "restore preserves actually displaced later edit");
        Check((string)Receipt(initial.BackupDirectory)["State"] == "restore_failed_after_write", "partial restore state recorded");

        var large = new Fixture("replace-race-large-edit", "KEEP=original\n"); byte[] largeEdit = new byte[2097153]; largeEdit[0] = 123;
        raced = large.Store(null, delegate(string phase) { if (phase == "replace") EditorReplace(large, largeEdit); }).Apply(Http, All);
        Check(!raced.Success && raced.Changed && raced.Error == "codex_concurrent_edit_preserved", "oversized raced file reported without dropping capture");
        Exact(largeEdit, File.ReadAllBytes(raced.RecoveryPath), "oversized captured edit remains fully recoverable");

        var recycle = new Fixture("recycle-rename-race", (string)null); initial = recycle.Store().Apply(Http, All);
        raced = recycle.Store(null, delegate(string phase) { if (phase == "recycle-rename") EditorReplace(recycle, later); }).Restore(initial.BackupDirectory);
        Check(!raced.Success && raced.Changed && raced.Error == "codex_concurrent_edit_preserved" && recycle.Recycles == 0, "new-file undo never recycles file swapped after identity check");
        Exact(later, recycle.Bytes(), "raced file safely returned when original name remains vacant");
        Exact(later, File.ReadAllBytes(raced.RecoveryPath), "recycle race also has verified independent transaction copy");

        var recycleLarge = new Fixture("recycle-rename-large-race", (string)null); initial = recycleLarge.Store().Apply(Http, All);
        raced = recycleLarge.Store(null, delegate(string phase) { if (phase == "recycle-rename") EditorReplace(recycleLarge, largeEdit); }).Restore(initial.BackupDirectory);
        Check(!raced.Success && raced.Changed && raced.Error == "codex_concurrent_edit_preserved" && recycleLarge.Recycles == 0, "oversized raced recycle capture refused");
        Check(File.Exists(raced.RecoveryPath), "oversized recycle capture retains valid recovery location");
        Exact(largeEdit, File.ReadAllBytes(raced.RecoveryPath), "oversized recycle capture retained byte exact in private staging");

        var recreate = new Fixture("recycle-rename-race-recreated", (string)null); initial = recreate.Store().Apply(Http, All);
        raced = recreate.Store(null, delegate(string phase) {
            if (phase == "recycle-rename") EditorReplace(recreate, later);
            if (phase == "recycle-captured") File.WriteAllBytes(recreate.Env, newest);
        }).Restore(initial.BackupDirectory);
        Check(!raced.Success && raced.Changed && recreate.Recycles == 0, "raced capture is preserved instead of recycled");
        Exact(newest, recreate.Bytes(), "return of captured file cannot overwrite recreated .env");
        Exact(later, File.ReadAllBytes(raced.RecoveryPath), "captured intermediate edit recoverable while newest env retained");

        var duringRecycle = new Fixture("edit-at-recycle-window", (string)null); initial = duringRecycle.Store().Apply(Http, All);
        byte[] generated = duringRecycle.Bytes();
        raced = duringRecycle.Store(null, delegate(string phase) { if (phase == "recycle") File.WriteAllBytes(duringRecycle.Env, newest); }).Restore(initial.BackupDirectory);
        Check(!raced.Success && raced.Changed && raced.Error == "codex_restore_conflict", "new edit at recycle window reported as conflict");
        Check(duringRecycle.Recycles == 1, "only verified captured original sent to injected recycler");
        Exact(generated, File.ReadAllBytes(Path.Combine(duringRecycle.Recycled, "env-0.bin")), "recycler receives expected captured bytes");
        Exact(newest, duringRecycle.Bytes(), "new .env at recycling time is never a recycle target");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateHardLink(string newFile, string existingFile, IntPtr reserved);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool CreateSymbolicLink(string link, string target, uint flags);
    public static int Main() {
        try {
            root = Path.Combine(Path.GetTempPath(), "whale-codex-proxy-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            PreservationAndUndo(); NewlinesAndDuplicates(); RejectMalformed(); MissingAndRecycle(); ConcurrentAndRestoreConflicts(); BackupAndPathGuards(); NarrowMutationRaces();
            Console.WriteLine("CodexProxySettingsTests passed: " + checks + " checks. Temp fixtures and injected recycler only; no real user config, registry, network or Recycle Bin access.");
            Console.WriteLine("Fixture root: " + root); return 0;
        } catch (Exception error) { Console.WriteLine(error); Console.WriteLine("Fixture root: " + root); return 1; }
    }
}
