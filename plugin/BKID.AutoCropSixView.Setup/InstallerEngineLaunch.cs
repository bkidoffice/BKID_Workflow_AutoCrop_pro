using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace BKID.AutoCropSixView.Setup
{
    internal static class InstallerEngineLaunch
    {
        internal static readonly string[] ExecutableNames = {
            "rhiexec.exe", "RhinoInstallerEngine.exe", "Rhino Installer Engine.exe"
        };

        internal static string ExecutableFromCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            string text = Environment.ExpandEnvironmentVariables(command.Trim());
            if (text.StartsWith("\"", StringComparison.Ordinal))
            {
                int end = text.IndexOf('"', 1);
                return end > 1 ? text.Substring(1, end - 1) : null;
            }
            int extension = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return extension >= 0 ? text.Substring(0, extension + 4) : null;
        }

        internal static bool IsEngineIdentity(string path, string company, string description)
        {
            string name = Path.GetFileName(path);
            return Array.Exists(ExecutableNames, item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase)) &&
                   (company ?? "").IndexOf("McNeel", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   (description ?? "").IndexOf("Rhino", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static string SelectEngine(IEnumerable<string> candidates, Func<string, bool> validate)
        {
            foreach (string candidate in candidates)
                if (!string.IsNullOrWhiteSpace(candidate) && validate(candidate)) return candidate;
            throw new FileNotFoundException("Rhino 설치 엔진을 찾거나 읽을 수 없습니다. 기존 SixView 파일은 변경하지 않았습니다. Rhino 6/8 설치를 복구한 뒤 다시 실행하세요. .rhi 연결 프로그램은 사용하지 않습니다.");
        }

        internal static string Select64BitEngine(IEnumerable<string> candidates, Func<string, bool> validate)
        {
            foreach (string path in candidates)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                // The x86 engine only detected Rhino 4/5 in the reported log.
                // Never fall back to it, even if registered as the default engine.
                var segments = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                if (Array.Exists(segments, part => string.Equals(part, "x86", StringComparison.OrdinalIgnoreCase))) continue;
                if (validate(path) && Is64BitExecutable(path)) return path;
            }
            throw new FileNotFoundException("Rhino 6/8용 64비트 설치 엔진을 찾지 못했습니다. 32비트(x86) 엔진으로 대체 실행하지 않습니다. 기존 SixView 파일은 변경하지 않았습니다. Rhino 설치 복구 후 다시 실행하세요.");
        }

        internal static bool Is64BitExecutable(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream))
                {
                    if (stream.Length < 64 || reader.ReadUInt16() != 0x5a4d) return false;
                    stream.Position = 0x3c;
                    int pe = reader.ReadInt32();
                    if (pe < 64 || pe > stream.Length - 24) return false;
                    stream.Position = pe;
                    if (reader.ReadUInt32() != 0x4550) return false;
                    ushort machine = reader.ReadUInt16();
                    ushort sections = reader.ReadUInt16();
                    stream.Position = pe + 20;
                    ushort optionalSize = reader.ReadUInt16();
                    long optional = pe + 24;
                    if (optionalSize < 2 || optional + optionalSize > stream.Length) return false;
                    stream.Position = optional;
                    ushort magic = reader.ReadUInt16();
                    if (machine == 0x8664 && magic == 0x20b) return true; // AMD64
                    if (machine != 0x14c || magic != 0x10b || optionalSize < 216) return false;
                    // Managed AnyCPU can be 64-bit, unless Requires32Bit/Prefers32Bit is set.
                    stream.Position = optional + 208;
                    uint cliRva = reader.ReadUInt32();
                    if (cliRva == 0) return false; // native x86
                    for (int i = 0; i < sections; i++)
                    {
                        stream.Position = optional + optionalSize + 40L * i + 8;
                        uint virtualSize = reader.ReadUInt32();
                        uint virtualAddress = reader.ReadUInt32();
                        uint rawSize = reader.ReadUInt32();
                        uint rawPointer = reader.ReadUInt32();
                        if (cliRva < virtualAddress || cliRva - virtualAddress >= Math.Max(virtualSize, rawSize)) continue;
                        stream.Position = (long)rawPointer + cliRva - virtualAddress + 16;
                        uint flags = reader.ReadUInt32();
                        return (flags & 1) != 0 && (flags & (2 | 0x20000)) == 0;
                    }
                    return false;
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (ArgumentException) { return false; }
        }

        internal static ProcessStartInfo CreateStartInfo(string engine, string package)
        {
            if (string.IsNullOrWhiteSpace(engine) || !File.Exists(engine))
                throw new FileNotFoundException("Rhino 설치 엔진이 없습니다. 기존 설치를 변경하지 않았습니다.", engine);
            if (string.IsNullOrWhiteSpace(package) || !File.Exists(package))
                throw new FileNotFoundException("설치 RHI 파일이 없습니다.", package);
            if (package.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0)
                throw new ArgumentException("Invalid package path.", "package");
            return new ProcessStartInfo
            {
                FileName = engine,
                Arguments = "\"" + Path.GetFullPath(package) + "\"",
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(engine)),
                UseShellExecute = false
            };
        }
    }
}
