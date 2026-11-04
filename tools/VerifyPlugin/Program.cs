using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

bool setup = args.Length == 4 && args[0] == "--setup";
if (setup) args = args.Skip(1).ToArray();
if (!setup && args.Length != 3) throw new ArgumentException("Usage: VerifyPlugin plugin.rhp capture.py icon.png");
using var input = File.OpenRead(args[0]);
using var pe = new PEReader(input);
if (!pe.HasMetadata) throw new Exception("The RHP is not a compiled .NET assembly.");
var reader = pe.GetMetadataReader();
var assembly = reader.GetAssemblyDefinition();
if (reader.GetString(assembly.Name) != (setup ? "BKID_AutoCrop_Setup_3.0.0" : "BKID.AutoCrop")) throw new Exception("Wrong assembly identity.");
if (assembly.Version != new Version(3, 0, 0, 0)) throw new Exception("Wrong assembly version.");
bool validGuid = false;
foreach (var handle in assembly.GetCustomAttributes())
{
    var attr = reader.GetCustomAttribute(handle);
    if (attr.Constructor.Kind != HandleKind.MemberReference) continue;
    var constructor = reader.GetMemberReference((MemberReferenceHandle)attr.Constructor);
    if (constructor.Parent.Kind != HandleKind.TypeReference) continue;
    var type = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
    if (reader.GetString(type.Name) != "GuidAttribute") continue;
    var value = reader.GetBlobReader(attr.Value);
    if (value.ReadUInt16() != 1) throw new Exception("Invalid GUID attribute encoding.");
    validGuid = value.ReadSerializedString() == "3881C10B-30A8-4FE5-BB48-9E53A6FEA1F4";
}
if (!setup && !validGuid) throw new Exception("Missing Rhino installer assembly GUID.");
var resources = new Dictionary<string, byte[]>();
var resourceSection = pe.GetSectionData(pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress);
foreach (var handle in reader.ManifestResources)
{
    var resource = reader.GetManifestResource(handle);
    if (!resource.Implementation.IsNil) throw new Exception("Unexpected external resource.");
    var bytes = resourceSection.GetReader((int)resource.Offset, resourceSection.Length - (int)resource.Offset);
    resources.Add(reader.GetString(resource.Name), bytes.ReadBytes(bytes.ReadInt32()));
}
if (setup)
{
    if (!resources["BKID.Setup.Package.rhi"].SequenceEqual(File.ReadAllBytes(args[1])))
        throw new Exception("Setup embeds a different RHI.");
    if (!resources["BKID.Setup.Logo.png"].SequenceEqual(File.ReadAllBytes(args[2])))
        throw new Exception("Setup artwork differs from the supplied original.");
    Console.WriteLine("Verified Windows setup executable, version, embedded RHI and exact original wizard artwork.");
    return;
}
if (!resources["BKID.AutoCrop.Capture.py"].SequenceEqual(File.ReadAllBytes(args[1])))
    throw new Exception("Embedded capture differs from repository source.");
if (!resources["BKID.AutoCrop.Icon.png"].SequenceEqual(File.ReadAllBytes(args[2])))
    throw new Exception("Embedded logo differs from repository asset.");
var versions = reader.AssemblyReferences.Select(h => reader.GetAssemblyReference(h))
    .ToDictionary(reference => reader.GetString(reference.Name), reference => reference.Version);
var references = versions.Keys.ToHashSet();
if (!references.Contains("RhinoCommon"))
    throw new Exception("Missing host SDK references.");
if (versions["RhinoCommon"] != new Version(6, 0, 18016, 23451) ||
    versions["Rhino.UI"] != new Version(6, 0, 18016, 23451) ||
    versions["Eto"] != new Version(2, 3, 6591, 18824))
    throw new Exception("Plugin references exceed the selected initial Rhino 6 SDK baseline.");
Console.WriteLine("Verified compiled RHP, version, GUID, Rhino 6.0/Eto 2.3 references, capture source and logo resources.");
