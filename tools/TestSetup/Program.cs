using BKID.AutoCropSixView.Setup;

static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
var root = Path.Combine(Path.GetTempPath(), "BKID 삭제 설치 테스트 " + Guid.NewGuid());
Directory.CreateDirectory(root);
try
{
    var previous = Path.Combine(root, "BKID.AutoCropSixView.rhp");
    var userFile = Path.Combine(root, "사용자 메모.txt");
    File.WriteAllBytes(previous, new byte[] { 1, 2, 3, 4 });
    File.WriteAllText(userFile, "preserve");
    var managed = new ManagedFiles();
    managed.Validate(new[] { previous });
    managed.Remove(previous);
    managed.Remove(previous); // Repeat/missing-file deletion is harmless.
    Assert(!File.Exists(previous), "Previous plugin was not deleted.");
    Assert(File.ReadAllText(userFile) == "preserve", "Unrelated file changed.");
    Assert(Directory.GetFiles(root).Length == 1 && Directory.GetDirectories(root).Length == 0, "Unexpected backup created.");
    Console.WriteLine("PASS: deletion without backup, repeat/missing input, Korean paths and unrelated file preservation.");

    File.WriteAllBytes(previous, new byte[] { 5, 6 });
    var linked = Path.Combine(root, "linked.rhp");
    File.CreateSymbolicLink(linked, previous);
    bool refused = false;
    try { managed.Validate(new[] { previous, linked }); }
    catch (IOException) { refused = true; }
    Assert(refused && File.Exists(previous) && File.Exists(linked), "Preflight should reject a link before any file removal.");
    Console.WriteLine("PASS: linked installation rejected before deletion.");

    File.SetAttributes(previous, FileAttributes.ReadOnly);
    refused = false;
    try { managed.Validate(new[] { previous }); }
    catch (IOException) { refused = true; }
    Assert(refused && File.Exists(previous), "Read-only file should fail preflight without deletion.");
    File.SetAttributes(previous, FileAttributes.Normal);
    Console.WriteLine("PASS: read-only installation rejected before deletion.");
    // Exercise the same asynchronous confirmation flow used by the real wizard.
    int questions = 0;
    bool ran = false;
    File.WriteAllText(previous, "existing version");
    bool completed = await InstallWorkflow.RunAsync(
        () => File.Exists(previous),
        () => { questions++; return false; },
        allowed => { ran = true; managed.Remove(previous); });
    Assert(!completed && !ran && questions == 1 && File.ReadAllText(previous) == "existing version",
           "No must leave the old installation unchanged and never enter installation.");

    completed = await InstallWorkflow.RunAsync(
        () => File.Exists(previous), () => true,
        allowed =>
        {
            InstallWorkflow.RequireConsent(File.Exists(previous), allowed);
            managed.Remove(previous);
            File.WriteAllText(previous, "new version");
        });
    Assert(completed && File.ReadAllText(previous) == "new version", "Yes must allow deletion and replacement.");

    managed.Remove(previous);
    completed = await InstallWorkflow.RunAsync(
        () => File.Exists(previous), () => throw new Exception("Fresh install must not ask for replacement."),
        allowed => { Assert(!allowed, "Fresh install has no replacement consent."); File.WriteAllText(previous, "fresh"); });
    Assert(completed && File.ReadAllText(previous) == "fresh", "Fresh installation did not run.");

    bool rejectedRace = false;
    try { InstallWorkflow.RequireConsent(true, false); }
    catch (InvalidOperationException) { rejectedRace = true; }
    Assert(rejectedRace, "A newly detected installation must not be deleted without confirmation.");
    Console.WriteLine("PASS: actual wizard workflow preserves existing files on No, replaces on Yes, skips prompt for fresh installs and rejects unapproved new installations.");

    var verifiedFolder = Path.Combine(root, "verified");
    Directory.CreateDirectory(verifiedFolder);
    var plugin = Path.Combine(verifiedFolder, "BKID.AutoCropSixView.rhp");
    var rui = Path.ChangeExtension(plugin, ".rui");
    byte[] payload = new byte[] { 0x4d, 0x5a, 7, 9 };
    string toolbar = "<RhinoUI major_ver='2' guid='own-collection'><tool_bar_groups><tool_bar_group><dock_bar_info floating='True'/></tool_bar_group></tool_bar_groups><macros><macro_item guid='left'><script>! _BKIDAutoCropSixViewCapture</script></macro_item><macro_item guid='right'><script>! _BKIDAutoCropSixViewResolution</script></macro_item></macros></RhinoUI>";
    var verification = new InstallationVerification(payload, System.Text.Encoding.UTF8.GetBytes(toolbar));
    File.WriteAllBytes(plugin, payload);
    File.WriteAllText(rui, toolbar);
    int waits = 0;
    InstallationVerification.WaitForComplete(403, () => verification.Matches(plugin), () => waits++, 2);
    Assert(waits == 0, "Verified 403 should complete immediately without an error.");
    InstallationVerification.WaitForComplete(0, () => verification.Matches(plugin), () => waits++, 2);
    Console.WriteLine("PASS: verified installation succeeds with both 403 and zero exit codes.");

    var edited = System.Xml.Linq.XDocument.Parse(toolbar);
    edited.Root!.SetAttributeValue("major_ver", "3");
    edited.Descendants("dock_bar_info").First().SetAttributeValue("floating", "False");
    edited.Descendants("dock_bar_info").First().SetAttributeValue("float_point", "200,300");
    edited.Save(rui);
    Assert(verification.Matches(plugin), "Rhino-saved schema/layout changes should not cause failure.");
    Console.WriteLine("PASS: rewritten toolbar formatting, docking and schema remain valid.");

    File.WriteAllBytes(plugin, new byte[] { 0x4d, 0x5a, 1 });
    Assert(!verification.Matches(plugin), "Old/different plugin must not count as the new installation.");
    bool failed = false;
    try { InstallationVerification.WaitForComplete(0, () => verification.Matches(plugin), () => { }, 1); }
    catch (InvalidOperationException) { failed = true; }
    Assert(failed, "Exit zero alone must not hide a missing new plugin.");
    File.Delete(plugin);
    failed = false;
    try { InstallationVerification.WaitForComplete(403, () => verification.Matches(plugin), () => { }, 1); }
    catch (InvalidOperationException error) { failed = error.Message.Contains("403"); }
    Assert(failed, "Unverified 403 must remain a failure with the diagnostic code.");

    InstallationVerification.WaitForComplete(403, () => verification.Matches(plugin), () => File.WriteAllBytes(plugin, payload), 2);
    File.Delete(rui);
    Assert(!verification.Matches(plugin), "Missing toolbar must fail verification.");
    var missingCommand = System.Xml.Linq.XDocument.Parse(toolbar);
    missingCommand.Descendants("macro_item").Last().Remove();
    missingCommand.Save(rui);
    Assert(!verification.Matches(plugin), "Missing resolution command must fail verification.");
    Console.WriteLine("PASS: old/missing files and missing commands remain failures; delayed valid files after 403 succeed.");

    // Use the installer's actual candidate enumerator with both products present.
    var coexist = Path.Combine(root, "coexist");
    Directory.CreateDirectory(coexist);
    var original = Path.Combine(coexist, "AutoCrop");
    var six = Path.Combine(coexist, "BKID_AutoCrop_SixView");
    Directory.CreateDirectory(original);
    Directory.CreateDirectory(six);
    var originalPlugin = Path.Combine(original, "BKID.AutoCrop.rhp");
    var originalToolbar = Path.Combine(original, "BKID.AutoCrop.rui");
    File.WriteAllText(originalPlugin, "original-plugin");
    File.WriteAllText(originalToolbar, "original-toolbar");
    var independent = Path.Combine(six, "BKID.AutoCropSixView.rhp");
    File.WriteAllText(independent, "sixview-plugin");
    var candidates = InstallationIdentity.EnumeratePluginFiles(coexist).ToArray();
    Assert(candidates.SequenceEqual(new[] { independent }), "AutoCrop must never enter SixView deletion candidates.");
    foreach (var candidate in candidates) managed.Remove(candidate);
    Assert(File.ReadAllText(originalPlugin) == "original-plugin", "AutoCrop RHP was changed.");
    Assert(File.ReadAllText(originalToolbar) == "original-toolbar", "AutoCrop RUI was changed.");
    Assert(!string.Equals(InstallationIdentity.PluginId, "3881C10B-30A8-4FE5-BB48-9E53A6FEA1F4", StringComparison.OrdinalIgnoreCase),
           "SixView registry identity must differ from AutoCrop.");
    Console.WriteLine("PASS: actual SixView installer discovery excludes original AutoCrop; original RHP/RUI survive removal.");

    // Reproduce a hostile .rhi default handler: select only a McNeel engine.
    Assert(!InstallerEngineLaunch.IsEngineIdentity("/fake/7z.exe", "Igor Pavlov", "7-Zip"), "7-Zip must not be accepted as the Rhino engine.");
    Assert(!InstallerEngineLaunch.IsEngineIdentity("/fake/rhiexec.exe", "Other company", "Rhino"), "Filename alone cannot identify the engine.");
    string selectedEngine = InstallerEngineLaunch.SelectEngine(new[] { "/fake/7z.exe", "/fake/rhiexec.exe" },
        candidate => InstallerEngineLaunch.IsEngineIdentity(candidate, "Robert McNeel & Associates", "Rhino Installer Engine"));
    Assert(selectedEngine == "/fake/rhiexec.exe", "Wrong RHI association affected engine selection.");
    Assert(InstallerEngineLaunch.ExecutableFromCommand("\"C:\\Program Files\\McNeel\\rhiexec.exe\" \"%1\"") == "C:\\Program Files\\McNeel\\rhiexec.exe", "Registered command parsing failed.");
    bool missingEngine = false;
    try { InstallerEngineLaunch.SelectEngine(new[] { "/fake/7z.exe" }, candidate => false); }
    catch (FileNotFoundException) { missingEngine = true; }
    Assert(missingEngine && File.Exists(originalPlugin), "Missing engine must stop without changing installed files.");
    if (!OperatingSystem.IsWindows())
    {
        string launchFolder = Path.Combine(root, "엔진 경로 '테스트'");
        Directory.CreateDirectory(launchFolder);
        string fakeEngine = Path.Combine(launchFolder, "rhiexec.exe");
        string fakePackage = Path.Combine(launchFolder, "설치 파일 '한글'.rhi");
        File.WriteAllText(fakePackage, "RHI-placeholder");
        File.WriteAllText(fakeEngine, "#!/bin/sh\nprintf '%s' \"$1\" > received-package.txt\nexit 37\n");
        File.SetUnixFileMode(fakeEngine, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var direct = InstallerEngineLaunch.CreateStartInfo(fakeEngine, fakePackage);
        Assert(!direct.UseShellExecute && direct.FileName == fakeEngine, "Must launch engine directly, never shell-open the RHI.");
        using (var process = System.Diagnostics.Process.Start(direct)!)
        {
            process.WaitForExit();
            Assert(process.ExitCode == 37, "Exit status must come from the explicitly selected engine.");
        }
        Assert(File.ReadAllText(Path.Combine(launchFolder, "received-package.txt")) == fakePackage,
               "Unicode/space/quoted RHI path must arrive as exactly one argument.");
    }
    Console.WriteLine("PASS: 7-Zip rejected, McNeel engine selected, missing engine stops, direct process receives exact RHI path without shell association.");

    // Reported failure: a registered x86 engine is found before the x64 engine.
    string architectureRoot = Path.Combine(root, "engine-bitness");
    Directory.CreateDirectory(architectureRoot);
    string WritePe(string folder, ushort machine, bool managed, uint flags)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "rhiexec.exe");
        using (var output = File.Create(path))
        using (var writer = new BinaryWriter(output))
        {
            output.SetLength(0x300);
            writer.Write((ushort)0x5a4d); output.Position = 0x3c; writer.Write(0x80);
            output.Position = 0x80; writer.Write(0x4550); writer.Write(machine); writer.Write((ushort)1);
            output.Position = 0x94; writer.Write((ushort)224);
            output.Position = 0x98; writer.Write((ushort)(machine == 0x8664 ? 0x20b : 0x10b));
            if (managed)
            {
                output.Position = 0x98 + 208; writer.Write(0x2000u);
                output.Position = 0x98 + 224 + 8;
                writer.Write(0x100u); writer.Write(0x2000u); writer.Write(0x100u); writer.Write(0x200u);
                output.Position = 0x210; writer.Write(flags);
            }
        }
        return path;
    }
    var x86Engine = WritePe(Path.Combine(architectureRoot, "x86"), 0x14c, true, 3);
    var x64Engine = WritePe(Path.Combine(architectureRoot, "x64"), 0x8664, false, 0);
    var anyCpuEngine = WritePe(Path.Combine(architectureRoot, "anycpu"), 0x14c, true, 1);
    var prefer32Engine = WritePe(Path.Combine(architectureRoot, "prefer32"), 0x14c, true, 0x20001);
    var native32Engine = WritePe(Path.Combine(architectureRoot, "native32"), 0x14c, false, 0);
    Assert(!InstallerEngineLaunch.Is64BitExecutable(x86Engine), "Requires32Bit engine accepted.");
    Assert(InstallerEngineLaunch.Is64BitExecutable(x64Engine), "AMD64 engine rejected.");
    Assert(InstallerEngineLaunch.Is64BitExecutable(anyCpuEngine), "64-bit-capable AnyCPU engine rejected.");
    Assert(!InstallerEngineLaunch.Is64BitExecutable(prefer32Engine), "Prefers32Bit engine accepted.");
    Assert(!InstallerEngineLaunch.Is64BitExecutable(native32Engine), "Native x86 engine accepted.");
    Assert(InstallerEngineLaunch.Select64BitEngine(new[] { x86Engine, prefer32Engine, native32Engine, x64Engine }, _ => true) == x64Engine,
           "An earlier x86 registration must not override the x64 engine.");
    missingEngine = false;
    try { InstallerEngineLaunch.Select64BitEngine(new[] { x86Engine, prefer32Engine }, _ => true); }
    catch (FileNotFoundException) { missingEngine = true; }
    Assert(missingEngine && File.Exists(originalPlugin), "Only-32-bit discovery must stop without deleting existing files.");
    File.WriteAllBytes(Path.Combine(architectureRoot, "invalid.exe"), new byte[] { 1, 2, 3 });
    Assert(!InstallerEngineLaunch.Is64BitExecutable(Path.Combine(architectureRoot, "invalid.exe")), "Invalid PE file accepted.");
    Console.WriteLine("PASS: logged x86-first failure reproduced; x64 chosen, native/managed/preferred x86 rejected, AnyCPU64 accepted, no x86 fallback.");

}
finally { Directory.Delete(root, true); }
