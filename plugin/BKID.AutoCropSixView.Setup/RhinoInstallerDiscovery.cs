using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Win32;

namespace BKID.AutoCropSixView.Setup
{
    internal static class RhinoInstallerDiscovery
    {
        internal static string Find()
        {
            return InstallerEngineLaunch.Select64BitEngine(Candidates().Distinct(StringComparer.OrdinalIgnoreCase), Validate);
        }

        private static bool Validate(string path)
        {
            try
            {
                if (!Path.IsPathRooted(path) || !File.Exists(path)) return false;
                var version = FileVersionInfo.GetVersionInfo(path);
                return InstallerEngineLaunch.IsEngineIdentity(path, version.CompanyName,
                    version.ProductName + " " + version.FileDescription + " " + version.InternalName);
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static IEnumerable<string> Candidates()
        {
            // Read explicit Rhino engine registrations, never .rhi/UserChoice defaults.
            foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using (var root = RegistryKey.OpenBaseKey(hive, view))
                {
                    using (var classes = Open(root, @"SOFTWARE\Classes"))
                    {
                        if (classes != null)
                        foreach (var name in classes.GetSubKeyNames().Where(n =>
                            n.StartsWith("RhinoInstallerEngine", StringComparison.OrdinalIgnoreCase)))
                        using (var command = Open(classes, name + @"\shell\open\command"))
                            if (command != null)
                                yield return InstallerEngineLaunch.ExecutableFromCommand(command.GetValue("") as string);
                    }
                    foreach (var name in InstallerEngineLaunch.ExecutableNames)
                    using (var app = Open(root, @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + name))
                        if (app != null) yield return (app.GetValue("") as string ?? "").Trim('"');
                    foreach (var major in new[] { "8.0", "6.0" })
                    using (var install = Open(root, @"SOFTWARE\McNeel\Rhinoceros\" + major + @"\Install"))
                    {
                        if (install == null) continue;
                        foreach (string value in new[] { "InstallPath", "Path" })
                        {
                            string directory = install.GetValue(value) as string;
                            if (string.IsNullOrWhiteSpace(directory)) continue;
                            foreach (var file in Scan(directory)) yield return file;
                        }
                    }
                }
            }
            foreach (var special in new[] { Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.CommonProgramFiles,
                Environment.SpecialFolder.CommonProgramFilesX86 })
            {
                string directory = Environment.GetFolderPath(special);
                if (string.IsNullOrEmpty(directory)) continue;
                foreach (var relative in new[] { "Rhino 8", "Rhino 6", "McNeel" })
                foreach (var file in Scan(Path.Combine(directory, relative))) yield return file;
            }
        }

        private static RegistryKey Open(RegistryKey root, string path)
        {
            try { return root.OpenSubKey(path); }
            catch (UnauthorizedAccessException) { return null; }
            catch (SecurityException) { return null; }
        }

        private static IEnumerable<string> Scan(string root)
        {
            var pending = new Stack<string>();
            if (Directory.Exists(root)) pending.Push(root);
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                string[] files, children;
                try
                {
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                    files = Directory.GetFiles(directory, "*.exe");
                    children = Directory.GetDirectories(directory);
                }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                foreach (var file in files)
                    if (Array.Exists(InstallerEngineLaunch.ExecutableNames, name =>
                        string.Equals(Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase))) yield return file;
                foreach (var child in children) pending.Push(child);
            }
        }
    }
}
