using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace BKID.AutoCropSixView
{
    internal static class ToolbarBindings
    {
        private const string PreviousCaptureId = "ea5d8f95-b8c4-598b-8316-0df44b2cad46";

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
                .Single(m => IsCommand(m, "BKIDAutoCropSixViewCapture")).Attribute("guid"));
            var macros = document.Root.Element("macros");
            if (macros != null)
                foreach (var macro in macros.Elements("macro_item"))
                    if (macro.Attribute("guid") != null && IsCommand(macro, "BKIDAutoCropSixViewCapture")) ids.Add((string)macro.Attribute("guid"));
            return document.Descendants("tool_bar_item").Any(button => button.Element("left_macro_id") != null && ids.Contains((string)button.Element("left_macro_id")));
        }

        private static bool UpdateDescriptions(XElement target, XElement source)
        {
            bool changed = false;
            foreach (string name in new[] { "tooltip", "help_text", "text", "button_text", "menu_text" })
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
            var left = sourceMacros.Elements("macro_item").Single(m => IsCommand(m, "BKIDAutoCropSixViewCapture"));
            var right = sourceMacros.Elements("macro_item").Single(m => IsCommand(m, "BKIDAutoCropSixViewResolution"));
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
                    (localLeft == null || !IsCommand(localLeft, "BKIDAutoCropSixViewCapture"))) continue;
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
                else
                {
                    changed = UpdateDescriptions(actual, expected) || changed;
                    if (string.Equals((string)document.Root.Attribute("guid"),
                                      (string)source.Root.Attribute("guid"), StringComparison.OrdinalIgnoreCase) &&
                        (string)actual.Attribute("bitmap_id") != (string)expected.Attribute("bitmap_id"))
                    {
                        actual.SetAttributeValue("bitmap_id", (string)expected.Attribute("bitmap_id"));
                        changed = true;
                    }
                }
            }
            // Refresh our own saved collection's name and artwork after an update.
            // External collections with copied buttons keep their own bitmaps/layout.
            if (string.Equals((string)document.Root.Attribute("guid"),
                              (string)source.Root.Attribute("guid"), StringComparison.OrdinalIgnoreCase))
            {
                foreach (var expected in source.Descendants().Where(e =>
                    e.Name == "tool_bar_group" || e.Name == "tool_bar_group_item" || e.Name == "tool_bar"))
                {
                    string id = (string)expected.Attribute("guid");
                    var actual = document.Descendants(expected.Name).FirstOrDefault(e =>
                        string.Equals((string)e.Attribute("guid"), id, StringComparison.OrdinalIgnoreCase));
                    if (actual != null && expected.Attribute("bitmap_id") != null &&
                        (string)actual.Attribute("bitmap_id") != (string)expected.Attribute("bitmap_id"))
                    {
                        actual.SetAttributeValue("bitmap_id", (string)expected.Attribute("bitmap_id"));
                        changed = true;
                    }
                    var caption = expected.Element("text");
                    if (actual != null && caption != null && !XNode.DeepEquals(actual.Element("text"), caption))
                    {
                        if (actual.Element("text") == null) actual.Add(new XElement(caption));
                        else actual.Element("text").ReplaceWith(new XElement(caption));
                        changed = true;
                    }
                }
                var artwork = source.Root.Element("bitmaps");
                if (artwork != null && !XNode.DeepEquals(document.Root.Element("bitmaps"), artwork))
                {
                    if (document.Root.Element("bitmaps") == null) document.Root.Add(new XElement(artwork));
                    else document.Root.Element("bitmaps").ReplaceWith(new XElement(artwork));
                    changed = true;
                }
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
