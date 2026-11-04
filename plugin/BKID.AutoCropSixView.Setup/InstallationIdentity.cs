using System.Collections.Generic;
using System.IO;

namespace BKID.AutoCropSixView.Setup
{
    // Deliberately independent from the original BKID.AutoCrop identity and files.
    internal static class InstallationIdentity
    {
        internal const string PluginId = "EBA693A2-8ACF-5DC1-9A01-FC9CCB4D3DE0";
        internal static IEnumerable<string> EnumeratePluginFiles(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                foreach (var file in Directory.EnumerateFiles(directory, "BKID.AutoCropSixView.rhp"))
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) yield return file;
                foreach (var child in Directory.EnumerateDirectories(directory)) pending.Push(child);
            }
        }
    }
}
