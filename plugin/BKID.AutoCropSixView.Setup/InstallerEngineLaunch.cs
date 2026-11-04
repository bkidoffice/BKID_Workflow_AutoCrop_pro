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
