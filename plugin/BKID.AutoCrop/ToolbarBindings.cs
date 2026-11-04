using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace BKID.AutoCrop
{
    internal static class ToolbarBindings
    {
        private const string PreviousCaptureId = "654e9b41-09dc-51b6-8de2-4ee942d03864";

        private static XDocument Read(string text)
        {
            using (var input = new StringReader(text))
            using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        private static bool IsCommand(XElement macro, string command)
        {
            var script = (string)macro.Element("script") ?? "";
            return string.Equals(script.Trim().TrimStart('!').Trim().TrimStart('_'), command, StringComparison.OrdinalIgnoreCase);
        }

        internal static Guid CollectionId(string template)
        {
            return new Guid((string)Read(template).Root.Attribute("guid"));
        }

        internal static bool ContainsCaptureButton(string current, string template)
        {
            var document = Read(current);
            var source = Read(template);
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ids.Add(PreviousCaptureId);
            ids.Add((string)source.Root.Element("macros").Elements("macro_item")
                .Single(m => IsCommand(m, "BKIDAutoCropCapture")).Attribute("guid"));
            var macros = document.Root.Element("macros");
            if (macros != null)
                foreach (var macro in macros.Elements("macro_item"))
                    if (macro.Attribute("guid") != null && IsCommand(macro, "BKIDAutoCropCapture")) ids.Add((string)macro.Attribute("guid"));
            return document.Descendants("tool_bar_item").Any(button => button.Element("left_macro_id") != null && ids.Contains((string)button.Element("left_macro_id")));
        }

        private static bool UpdateDescriptions(XElement target, XElement source)
        {
            bool changed = false;
            foreach (string name in new[] { "tooltip", "help_text" })
            {
                var expected = source.Element(name);
                if (expected == null) continue;
                var current = target.Element(name);
                if (current != null && current.Elements().Select(e => e.Name + "=" + e.Value)
                    .SequenceEqual(expected.Elements().Select(e => e.Name + "=" + e.Value))) continue;
                if (current == null) target.Add(new XElement(expected));
                else current.ReplaceWith(new XElement(expected));
                changed = true;
            }
            return changed;
        }

        // Also recognizes buttons copied to a different collection with newly assigned IDs.
        internal static string Patch(string current, string template)
        {
            var document = Read(current);
            var source = Read(template);
            var sourceMacros = source.Root.Element("macros");
            var left = sourceMacros.Elements("macro_item").Single(m => IsCommand(m, "BKIDAutoCropCapture"));
            var right = sourceMacros.Elements("macro_item").Single(m => IsCommand(m, "BKIDAutoCropResolution"));
            var macros = document.Root.Element("macros") ?? new XElement("macros");
            var lookup = new System.Collections.Generic.Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in macros.Elements("macro_item").Where(m => m.Attribute("guid") != null))
                lookup[(string)item.Attribute("guid")] = item;
            bool changed = false;
            bool found = false;
            string newLeftId = (string)left.Attribute("guid");
            string newRightId = (string)right.Attribute("guid");
            foreach (var button in document.Descendants("tool_bar_item"))
            {
                string leftId = (string)button.Element("left_macro_id");
                if (leftId == null) continue;
                XElement localLeft;
                lookup.TryGetValue(leftId, out localLeft);
                if (!string.Equals(leftId, newLeftId, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(leftId, PreviousCaptureId, StringComparison.OrdinalIgnoreCase) &&
                    (localLeft == null || !IsCommand(localLeft, "BKIDAutoCropCapture"))) continue;
                found = true;
                // Fresh macro IDs avoid reusing Rhino's cached, shared hover descriptions.
                if (!string.Equals(leftId, newLeftId, StringComparison.OrdinalIgnoreCase))
                {
                    button.SetElementValue("left_macro_id", newLeftId);
                    changed = true;
                }
                if (!string.Equals((string)button.Element("right_macro_id"), newRightId, StringComparison.OrdinalIgnoreCase))
                {
                    button.SetElementValue("right_macro_id", newRightId);
                    changed = true;
                }
            }
            if (!found) return null;
            foreach (var expected in new[] { left, right })
            {
                XElement actual;
                string id = (string)expected.Attribute("guid");
                if (!lookup.TryGetValue(id, out actual))
                {
                    macros.Add(new XElement(expected));
                    changed = true;
                }
                else if (!string.Equals((string)actual.Element("script"), (string)expected.Element("script"), StringComparison.OrdinalIgnoreCase))
                {
                    actual.ReplaceWith(new XElement(expected));
                    changed = true;
                }
                else changed = UpdateDescriptions(actual, expected) || changed;
            }
            if (!changed) return null;
            if (macros.Parent == null)
            {
                var following = document.Root.Element("bitmaps") ?? document.Root.Element("scripts");
                if (following != null) following.AddBeforeSelf(macros);
                else document.Root.Add(macros);
            }
            return document.ToString(SaveOptions.DisableFormatting);
        }
    }
}
