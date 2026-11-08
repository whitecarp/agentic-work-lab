using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using WhaleClaudeHelper;

internal static class FontServiceTests {
    private static int checks;
    private static string root;
    private static void Check(bool passed, string name) { if (!passed) throw new Exception(name); checks++; }
    private sealed class FakePlatform : IFontPlatform {
        internal List<FontRegistration> User = new List<FontRegistration>(), System = new List<FontRegistration>();
        internal int Loads, Unloads, Recycles, Notifications;
        internal bool LoadFails, RegisterFails, RecycleFails;
        internal string Family = "Fixture Family", RecycleRoot;
        internal Action BeforeUnload, AfterUnregister;
        public List<FontRegistration> Read(bool user) { return (user ? User : System).Select(x => new FontRegistration { Name = x.Name, Data = x.Data, Kind = x.Kind }).ToList(); }
        public void Register(string name, string path) { if (RegisterFails) throw new IOException("fixture"); if (User.Any(x => x.Name == name)) throw new IOException("conflict"); User.Add(new FontRegistration { Name = name, Data = path, Kind = RegistryValueKind.String }); }
        public void Unregister(string name) { var item = User.Single(x => x.Name == name); User.Remove(item); if (AfterUnregister != null) AfterUnregister(); }
        public bool Load(string path) { Loads++; return !LoadFails; }
        public bool Unload(string path) { Unloads++; if (BeforeUnload != null) BeforeUnload(); return true; }
        public void Notify() { Notifications++; }
        public string[] Families(string path) { return new[] { Family }; }
        public void Recycle(string path) {
            if (RecycleFails) throw new IOException("fixture recycle failure");
            if (!Path.GetFullPath(path).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new Exception("outside fixture");
            Directory.CreateDirectory(RecycleRoot); File.Move(path, Path.Combine(RecycleRoot, Guid.NewGuid().ToString("N") + Path.GetExtension(path))); Recycles++;
        }
    }
    private sealed class Fixture {
        internal FontContext Context;
        internal FakePlatform Platform;
        internal FontService Service;
        internal string Source;
        internal Fixture(string name) {
            string dir = Path.Combine(root, name); Directory.CreateDirectory(dir);
            Context = new FontContext { UserFonts = Path.Combine(dir, "user-fonts"), SystemFonts = Path.Combine(dir, "system-fonts"), BackupRoot = Path.Combine(dir, "backups") };
            Directory.CreateDirectory(Context.UserFonts); Directory.CreateDirectory(Context.SystemFonts);
            Platform = new FakePlatform { RecycleRoot = Path.Combine(dir, "fixture-recycle") };
            Service = new FontService(Context, Platform); Source = Path.Combine(dir, "chosen.ttf"); File.WriteAllText(Source, "fixture font bytes");
        }
        internal FontEntry Existing(string filename, string name) {
            string file = Path.Combine(Context.UserFonts, filename); File.WriteAllText(file, "existing fixture bytes");
            Platform.User.Add(new FontRegistration { Name = name, Data = file, Kind = RegistryValueKind.String });
            return Service.ListFonts().Single(x => x.Name == name);
        }
    }
    private static void Main() {
        root = Path.Combine(Path.GetTempPath(), "whale-font-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var normal = new Fixture("install");
        var installed = normal.Service.Install(normal.Source);
        Check(installed.Ok && normal.Platform.User.Count == 1 && normal.Platform.Loads == 1 && normal.Platform.Notifications == 1, "install registers once and notifies");
        string installedPath = normal.Platform.User[0].Data;
        Check(File.ReadAllText(installedPath) == File.ReadAllText(normal.Source), "installed source bytes identical");
        Check(Path.GetFileName(installedPath).StartsWith("Dafeiyu-") && normal.Service.ListFonts().Single().CanRemove, "hash generated current-user destination");
        Check(File.Exists(Path.Combine(installed.BackupDirectory, "font.ttf")) && normal.Service.ListBackups().Contains(installed.BackupDirectory), "installation backup discoverable");
        var acl = Directory.GetAccessControl(installed.BackupDirectory);
        Check(acl.AreAccessRulesProtected, "backup ACL protected");
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier))) Check(rule.IdentityReference.Equals(WindowsIdentity.GetCurrent().User), "backup current-user grants only");
        int recycleCount = normal.Platform.Recycles;
        Check(!normal.Service.Install(normal.Source).Ok && normal.Platform.Recycles == recycleCount && File.Exists(installedPath), "duplicate installation does not overwrite or recycle");
        Check(normal.Service.Restore(installed.BackupDirectory).Ok && !File.Exists(installedPath) && normal.Platform.User.Count == 0 && normal.Platform.Recycles == recycleCount + 1, "undo installation recycles only exact font");
        Check(File.Exists(Path.Combine(installed.BackupDirectory, "font.ttf")) && !normal.Service.Restore(installed.BackupDirectory).Ok, "undo backup retained and cannot repeat");

        var removal = new Fixture("remove"); var entry = removal.Existing("mine.ttf", "My Font (TrueType)"); string original = File.ReadAllText(entry.Path);
        var removed = removal.Service.Remove(entry);
        Check(removed.Ok && !File.Exists(entry.Path) && removal.Platform.User.Count == 0 && removal.Platform.Recycles == 1, "user font removal uses recycler");
        Check(File.ReadAllText(Path.Combine(removed.BackupDirectory, "font.ttf")) == original, "removal backed up exact bytes");
        Check(removal.Service.Restore(removed.BackupDirectory).Ok && File.ReadAllText(entry.Path) == original && removal.Platform.User.Single().Data == entry.Path, "restore exact file and registry");
        int restoreLoads = removal.Platform.Loads, restoreUnloads = removal.Platform.Unloads, restoreRecycles = removal.Platform.Recycles;
        Check(removal.Service.Restore(removed.BackupDirectory).Ok && removal.Platform.User.Count == 1 && File.ReadAllText(entry.Path) == original, "repeated exact restore succeeds without overwriting");
        Check(removal.Platform.Loads == restoreLoads && removal.Platform.Unloads == restoreUnloads && removal.Platform.Recycles == restoreRecycles, "idempotent restore makes no GDI or recycler calls");

        var conflicts = new Fixture("conflicts"); var conflictEntry = conflicts.Existing("mine.otf", "My OTF"); var conflictBackup = conflicts.Service.Remove(conflictEntry);
        File.WriteAllText(conflictEntry.Path, "replacement");
        Check(!conflicts.Service.Restore(conflictBackup.BackupDirectory).Ok && File.ReadAllText(conflictEntry.Path) == "replacement", "recreated file protected");
        File.Move(conflictEntry.Path, Path.Combine(root, "moved-replacement.otf"));
        conflicts.Platform.User.Add(new FontRegistration { Name = conflictEntry.Name, Data = "C:\\other.ttf", Kind = RegistryValueKind.String });
        Check(!conflicts.Service.Restore(conflictBackup.BackupDirectory).Ok && !File.Exists(conflictEntry.Path), "conflicting registry protected");

        var stale = new Fixture("stale"); var staleEntry = stale.Existing("stale.ttf", "Stale"); File.AppendAllText(staleEntry.Path, "edited");
        Check(!stale.Service.Remove(staleEntry).Ok && stale.Platform.Recycles == 0, "changed selection refused");
        staleEntry = stale.Service.ListFonts().Single(); stale.Platform.User[0].Data = stale.Source;
        Check(!stale.Service.Remove(staleEntry).Ok && File.Exists(staleEntry.Path), "changed registry refused");

        var shared = new Fixture("shared"); var sharedEntry = shared.Existing("shared.ttf", "First");
        shared.Platform.User.Add(new FontRegistration { Name = "Second", Data = sharedEntry.Path, Kind = RegistryValueKind.String });
        Check(shared.Service.ListFonts().All(x => !x.CanRemove), "shared file entries protected");
        Check(!shared.Service.Remove(sharedEntry).Ok && shared.Platform.Recycles == 0, "stale shared registry rechecked");
        shared.Platform.User.RemoveAt(1); shared.Platform.System.Add(new FontRegistration { Name = "System alias", Data = sharedEntry.Path, Kind = RegistryValueKind.String });
        Check(!shared.Service.ListFonts().Single(x => x.Name == "First").CanRemove && !shared.Service.Remove(sharedEntry).Ok, "machine registry alias protects user path");
        var system = new Fixture("system"); string systemPath = Path.Combine(system.Context.SystemFonts, "system.ttf"); File.WriteAllText(systemPath, "system fixture");
        system.Platform.System.Add(new FontRegistration { Name = "Built-in", Data = "system.ttf", Kind = RegistryValueKind.String });
        Check(!system.Service.ListFonts().Single().CanRemove && !system.Service.ListFonts().Single().IsUser && system.Service.ListFonts().Single().Path == systemPath, "system font read-only inventory");
        Check(!system.Service.Remove(new FontEntry { Name = "Built-in", Path = systemPath, IsUser = true, CanRemove = true }).Ok && File.Exists(systemPath), "forged system selection refused");
        system.Platform.User.Add(new FontRegistration { Name = "Outside user fonts", Data = system.Source, Kind = RegistryValueKind.String });
        Check(!system.Service.ListFonts().Single(x => x.Name == "Outside user fonts").CanRemove, "external user registry path protected");

        var badLoad = new Fixture("loadfailure"); badLoad.Platform.LoadFails = true; var loadResult = badLoad.Service.Install(badLoad.Source);
        Check(!loadResult.Ok && badLoad.Platform.User.Count == 0 && Directory.GetFiles(badLoad.Context.UserFonts).Length == 0 && badLoad.Platform.Recycles == 1, "font resource failure rolls back new file via recycler");
        Check(File.Exists(Path.Combine(loadResult.BackupDirectory, "font.ttf")), "failed install backup retained");
        var badRegister = new Fixture("regfailure"); badRegister.Platform.RegisterFails = true; var regResult = badRegister.Service.Install(badRegister.Source);
        Check(!regResult.Ok && badRegister.Platform.User.Count == 0 && Directory.GetFiles(badRegister.Context.UserFonts).Length == 0 && badRegister.Platform.Unloads == 1, "registry failure unloads and recycles new file");
        var badRecycle = new Fixture("recyclefailure"); var badEntry = badRecycle.Existing("retain.ttf", "Retain"); badRecycle.Platform.RecycleFails = true;
        Check(!badRecycle.Service.Remove(badEntry).Ok && File.Exists(badEntry.Path) && badRecycle.Platform.User.Count == 1 && badRecycle.Platform.Loads == 1, "failed removal restores original registration and resource");

        var race = new Fixture("race"); var raceEntry = race.Existing("race.ttf", "Race");
        race.Platform.BeforeUnload = delegate { race.Platform.User.Add(new FontRegistration { Name = "New reference", Data = raceEntry.Path, Kind = RegistryValueKind.String }); };
        Check(!race.Service.Remove(raceEntry).Ok && File.Exists(raceEntry.Path) && race.Platform.Recycles == 0, "new reference before unregister blocks removal");
        var lateRace = new Fixture("late-race"); var lateEntry = lateRace.Existing("late.ttf", "Late");
        lateRace.Platform.AfterUnregister = delegate { lateRace.Platform.User.Add(new FontRegistration { Name = "New alias", Data = lateEntry.Path, Kind = RegistryValueKind.String }); };
        Check(!lateRace.Service.Remove(lateEntry).Ok && File.Exists(lateEntry.Path) && lateRace.Platform.Recycles == 0 && lateRace.Platform.User.Any(x => x.Name == lateEntry.Name), "late reference preserves file and restores selected registration");

        var invalid = new Fixture("invalid"); string wrongExtension = Path.Combine(root, "not-font.exe"); File.WriteAllText(wrongExtension, "fixture");
        Check(!invalid.Service.Install(wrongExtension).Ok && invalid.Platform.Loads == 0, "non-font extension refused");
        invalid.Platform.Family = "";
        Check(!invalid.Service.Install(invalid.Source).Ok && invalid.Platform.Loads == 0, "missing family refused");
        invalid.Platform.Family = "Valid";
        using (var stream = File.OpenWrite(invalid.Source)) stream.SetLength(32L * 1024 * 1024 + 1);
        Check(!invalid.Service.Install(invalid.Source).Ok && invalid.Platform.Loads == 0, "oversize source refused before loading");
        Check(!invalid.Service.Install("C:relative.ttf").Ok && !invalid.Service.Install(@"\\server\share\font.ttf").Ok && !invalid.Service.Install("C:\\file.ttf:stream").Ok, "relative UNC and alternate stream paths refused");

        var tamper = new Fixture("tamper"); var tamperEntry = tamper.Existing("tamper.ttf", "Tamper"); var tamperBackup = tamper.Service.Remove(tamperEntry);
        string payload = Path.Combine(tamperBackup.BackupDirectory, "font.ttf"); File.AppendAllText(payload, "changed");
        Check(!tamper.Service.Restore(tamperBackup.BackupDirectory).Ok && !File.Exists(tamperEntry.Path), "backup hash mismatch refuses restore");
        Check(!tamper.Service.Restore(root).Ok, "backup outside selected root refused");
        var manifestTamper = new Fixture("manifest-tamper"); var mtEntry = manifestTamper.Existing("one.ttf", "One"); var mtBackup = manifestTamper.Service.Remove(mtEntry);
        string manifestPath = Path.Combine(mtBackup.BackupDirectory, "font.json"); var serializer = new JavaScriptSerializer(); var metadata = serializer.Deserialize<FontBackupManifest>(File.ReadAllText(manifestPath));
        metadata.OriginalPath = systemPath; metadata.RegistryData = systemPath; File.WriteAllText(manifestPath, serializer.Serialize(metadata));
        Check(!manifestTamper.Service.Restore(mtBackup.BackupDirectory).Ok && File.ReadAllText(systemPath) == "system fixture", "tampered restore target cannot reach system fonts");
        metadata.OriginalPath = mtEntry.Path; metadata.RegistryData = mtEntry.Path; metadata.BackupFile = "..\\chosen.ttf"; File.WriteAllText(manifestPath, serializer.Serialize(metadata));
        Check(!manifestTamper.Service.Restore(mtBackup.BackupDirectory).Ok, "manifest payload traversal refused");

        var linked = new Fixture("reparse"); string outside = Path.Combine(root, "reparse-target"); Directory.CreateDirectory(outside); File.WriteAllText(Path.Combine(outside, "linked.ttf"), "linked fixture");
        string junction = Path.Combine(root, "font-junction");
        var psi = new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + junction + "\" \"" + outside + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (var process = Process.Start(psi)) { process.StandardOutput.ReadToEnd(); process.StandardError.ReadToEnd(); process.WaitForExit(); Check(process.ExitCode == 0, "fixture junction created"); }
        Check(!linked.Service.Install(Path.Combine(junction, "linked.ttf")).Ok && linked.Platform.Loads == 0, "reparse source blocked");
        var linkedContext = new FontContext { UserFonts = junction, SystemFonts = linked.Context.SystemFonts, BackupRoot = linked.Context.BackupRoot };
        linked.Platform.User.Add(new FontRegistration { Name = "Linked", Data = Path.Combine(junction, "linked.ttf"), Kind = RegistryValueKind.String });
        var linkedService = new FontService(linkedContext, linked.Platform);
        Check(!linkedService.ListFonts().Single().CanRemove && !linkedService.Install(linked.Source).Ok && File.Exists(Path.Combine(outside, "linked.ttf")), "reparse destination protected");

        Console.WriteLine("FontServiceTests passed: " + checks + " checks. Fixture registry, resource loader and recycler only; no real fonts or registry changed.");
        Console.WriteLine("Fixture evidence: " + root);
    }
}
