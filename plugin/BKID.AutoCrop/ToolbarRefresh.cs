using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Rhino;
using Rhino.UI;

namespace BKID.AutoCrop
{
    internal static class ToolbarRefresh
    {
        internal static int Apply(string templatePath)
        {
            int refreshed = 0;
            string template = File.ReadAllText(templatePath);
            var ownedId = ToolbarBindings.CollectionId(template);
            var loaded = new List<ToolbarFile>();
            foreach (ToolbarFile file in RhinoApp.ToolbarFiles) loaded.Add(file);
            foreach (var file in loaded)
            {
                string path = file.Path;
                if (!File.Exists(path) && file.Id != ownedId) continue;
                // A correct on-disk RUI can still have stale controls in Rhino's UI cache.
                // Reload only collections containing our button, once per binding revision.
                if (File.Exists(path) && !ToolbarBindings.ContainsCaptureButton(File.ReadAllText(path), template)) continue;
                if (!file.Save()) throw new IOException("Cannot save toolbar layout before updating AutoCrop: " + path);
                string original = File.ReadAllText(path);
                string updated = ToolbarBindings.Patch(original, template) ?? original;
                var visibility = new Dictionary<Guid, bool>();
                for (int i = 0; i < file.GroupCount; i++)
                {
                    var group = file.GetGroup(i);
                    visibility[group.Id] = group.Visible;
                }
                file.Close(false);
                if (RhinoApp.ToolbarFiles.FindByPath(path) != null)
                    throw new IOException("Cannot close toolbar for AutoCrop update: " + path);
                try
                {
                    Write(path, updated);
                    Reopen(path, visibility);
                    refreshed++;
                }
                catch
                {
                    // Preserve the user's saved layout if loading the edited RUI fails.
                    var partial = RhinoApp.ToolbarFiles.FindByPath(path);
                    if (partial != null) partial.Close(false);
                    // If atomic replacement failed, the original is still intact;
                    // do not require another write just to reopen that saved layout.
                    if (!File.Exists(path) || File.ReadAllText(path) != original) Write(path, original);
                    Reopen(path, visibility);
                    throw;
                }
            }
            return refreshed;
        }

        private static void Reopen(string path, Dictionary<Guid, bool> visibility)
        {
            var restored = RhinoApp.ToolbarFiles.Open(path);
            if (restored == null) throw new IOException("Cannot reopen toolbar: " + path);
            for (int i = 0; i < restored.GroupCount; i++)
            {
                var group = restored.GetGroup(i);
                bool visible;
                if (visibility.TryGetValue(group.Id, out visible)) group.Visible = visible;
            }
        }

        private static void Write(string path, string text)
        {
            string staging = path + ".bkid-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(text);
                if (File.Exists(path)) File.Replace(staging, path, null);
                else File.Move(staging, path);
            }
            finally { if (File.Exists(staging)) File.Delete(staging); }
        }
    }
}
