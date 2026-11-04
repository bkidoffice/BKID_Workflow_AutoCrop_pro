using System.Xml;
using System.Xml.Linq;
using BKID.AutoCrop;
static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
string template = File.ReadAllText(args[0]);
var old = XDocument.Parse(template);
var macros = old.Root!.Element("macros")!;
var capture = macros.Elements("macro_item").Single(m => (string)m.Element("script") == "! _BKIDAutoCropCapture");
var resolution = macros.Elements("macro_item").Single(m => (string)m.Element("script") == "! _BKIDAutoCropResolution");
string rightId = (string)resolution.Attribute("guid")!;
string newLeftId = (string)capture.Attribute("guid")!;
capture.SetAttributeValue("guid", "654e9b41-09dc-51b6-8de2-4ee942d03864");
resolution.Remove();
var button = old.Descendants("tool_bar_item").First();
button.SetElementValue("left_macro_id", (string)capture.Attribute("guid")!);
button.SetElementValue("right_macro_id", (string)capture.Attribute("guid")!); // Actual pre-2.3 binding.
capture.Element("tooltip")!.SetElementValue("locale_1033", "좌클릭: 바로 저장 / 우클릭: ×2·×3·×4 해상도 선택");
var dock = old.Descendants("dock_bar_info").First();
dock.SetAttributeValue("floating", "False");
dock.SetAttributeValue("visible", "False");
dock.SetAttributeValue("dock_location", "bottom");
var originalDock = new XElement(dock);
var other = new XElement("tool_bar_item", new XAttribute("guid", Guid.NewGuid()), new XElement("left_macro_id", "unrelated"), new XElement("right_macro_id", "keep-this"));
button.Parent!.Add(other);
string originalOther = other.ToString();
string repaired = ToolbarBindings.Patch(old.ToString(), template);
Assert(ToolbarBindings.ContainsCaptureButton(old.ToString(), template), "Old collection was not recognized.");
Assert(ToolbarBindings.ContainsCaptureButton(template, template), "Correct RUI must still be eligible for UI-cache reload.");
Assert(!ToolbarBindings.ContainsCaptureButton("<RhinoUI><macros/><tool_bars/></RhinoUI>", template), "Unrelated collection must not be reloaded.");
Assert(repaired != null, "Old binding was not repaired.");
var result = XDocument.Parse(repaired!);
var resultButton = result.Descendants("tool_bar_item").First();
Assert((string)resultButton.Element("right_macro_id") == rightId, "Right-click not changed.");
Assert((string)resultButton.Element("left_macro_id") == newLeftId, "Left hover macro was not refreshed.");
Assert((string)resultButton.Element("left_macro_id") != (string)button.Element("left_macro_id"), "Old hover identity was reused.");
Assert((string)result.Descendants("macro_item").Single(m => (string)m.Attribute("guid") == newLeftId).Element("script") == "! _BKIDAutoCropCapture", "Left capture behavior changed.");
Assert(XNode.DeepEquals(result.Descendants("dock_bar_info").First(), originalDock), "Docking/visibility XML changed.");
Assert(result.Descendants("tool_bar_item").Last().ToString() == originalOther, "Unrelated button changed.");
Assert(ToolbarBindings.Patch(repaired!, template) == null, "Repair should be idempotent.");
Console.WriteLine("PASS: real old right-click binding repaired, left-click/docking/visibility/unrelated button preserved, repeat repair skipped.");

// Copying a native button into Default.rui can assign it new macro/button IDs.
var copied = XDocument.Parse(old.ToString());
var copiedCapture = copied.Descendants("macro_item").First();
string copiedId = Guid.NewGuid().ToString();
copiedCapture.SetAttributeValue("guid", copiedId);
var copiedButton = copied.Descendants("tool_bar_item").First();
copiedButton.SetAttributeValue("guid", Guid.NewGuid());
copiedButton.SetElementValue("left_macro_id", copiedId);
copiedButton.SetElementValue("right_macro_id", copiedId);
Assert(ToolbarBindings.Patch(copied.ToString(), template) != null, "Copied button not recognized by capture command.");
Assert(ToolbarBindings.Patch("<RhinoUI><macros/><tool_bars/></RhinoUI>", template) == null, "Unrelated collection changed.");
var external = new XDocument(new XElement("RhinoUI", new XElement("tool_bars", new XElement("tool_bar",
    new XElement("tool_bar_item", new XElement("left_macro_id", (string)capture.Attribute("guid")!),
                 new XElement("right_macro_id", (string)capture.Attribute("guid")!))))));
Assert(ToolbarBindings.Patch(external.ToString(), template) != null, "Cross-collection capture reference without local macros was not repaired.");
// Existing correct bindings must also receive the newly separated descriptions.
var shared = XDocument.Parse(template);
foreach (var macro in shared.Root!.Element("macros")!.Elements("macro_item"))
{
    macro.Element("tooltip")!.SetElementValue("locale_1033", "old shared description");
    macro.Element("help_text")!.SetElementValue("locale_1033", "old shared description");
}
string updatedDescriptions = ToolbarBindings.Patch(shared.ToString(), template);
Assert(updatedDescriptions != null, "Correct bindings with old descriptions were not refreshed.");
var described = XDocument.Parse(updatedDescriptions!);
var describedMacros = described.Root!.Element("macros")!.Elements("macro_item").ToList();
Assert(describedMacros[0].Element("tooltip")!.Value.Contains("1배"), "Left description not updated.");
Assert(describedMacros[1].Element("tooltip")!.Value.Contains("×2"), "Right description not updated.");
Assert(describedMacros[0].Element("tooltip")!.Value != describedMacros[1].Element("tooltip")!.Value, "Descriptions are still identical.");
Assert(ToolbarBindings.Patch(updatedDescriptions!, template) == null, "Description migration should be idempotent.");
Console.WriteLine("PASS: existing macros with correct bindings get distinct left/right descriptions without requiring rebinding.");
bool dtdRejected = false;
try { ToolbarBindings.Patch("<!DOCTYPE RhinoUI [<!ENTITY x SYSTEM 'file:///never-read'>]><RhinoUI/>", template); }
catch (XmlException) { dtdRejected = true; }
Assert(dtdRejected, "DTD must not be read.");
Console.WriteLine("PASS: copied native button recognized, unrelated collection skipped and external-entity XML rejected.");
