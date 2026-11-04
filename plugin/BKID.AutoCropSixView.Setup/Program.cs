using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BKID.AutoCropSixView.Setup
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool first;
            using (var mutex = new System.Threading.Mutex(true, @"Local\BKID.AutoCropSixView.Setup", out first))
            {
                if (!first)
                {
                    MessageBox.Show("BKID 설치 프로그램이 이미 실행 중입니다.");
                    return;
                }
                try { Application.Run(new SetupWindow()); }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }

    internal sealed class SetupWindow : Form
    {
        private readonly Button install = new Button { Text = "설치 / 다시 설치", Dock = DockStyle.Bottom, Height = 42 };
        private readonly Label status = new Label { Dock = DockStyle.Fill, AutoSize = false };
        private readonly Image logo;
        private bool busy;
        public SetupWindow()
        {
            Text = "BKID_AutoCrop_SixView 3.1.1 설치";
            ClientSize = new Size(760, 420);
            Padding = new Padding(0);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BKID.Setup.Logo.png"))
            using (var decoded = new Bitmap(stream))
                logo = new Bitmap(decoded);
            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24) };
            var title = new Label
            {
                Text = "BKID_AutoCrop_SixView 설치",
                Dock = DockStyle.Top,
                Height = 54,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 16, FontStyle.Bold)
            };
            content.Controls.Add(status);
            content.Controls.Add(title);
            content.Controls.Add(install);
            var artwork = new Panel
            {
                Dock = DockStyle.Left,
                Width = (int)Math.Round(ClientSize.Height * (double)logo.Width / logo.Height),
                Padding = new Padding(0),
                BackColor = Color.FromArgb(6, 8, 14)
            };
            artwork.Controls.Add(new PictureBox
            {
                Image = logo,
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.StretchImage
            });
            Controls.Add(content);
            Controls.Add(artwork);
            // Keep the panel at the image aspect ratio, including after DPI scaling.
            ClientSizeChanged += (sender, args) =>
                artwork.Width = (int)Math.Round(ClientSize.Height * (double)logo.Width / logo.Height);
            status.Text = "Rhino를 종료한 뒤 설치하세요.\r\n기존 AutoCrop은 유지하고 SixView를 별도로 설치합니다.\r\n백업하지 않으며, 설치가 중단되어도 이전 버전은 복원되지 않습니다.\r\nRhino Installer에서는 현재 사용자 설치를 선택하세요.";
            install.Click += InstallClicked;
            FormClosing += (sender, args) => { if (busy) args.Cancel = true; };
            FormClosed += (sender, args) => logo.Dispose();
        }

        private async void InstallClicked(object sender, EventArgs args)
        {
            busy = true;
            install.Enabled = false;
            status.Text = "기존 설치를 확인하고 있습니다…\r\nRhino Installer가 열리면 설치를 진행해 주세요.";
            try
            {
                var reinstaller = new Reinstall();
                bool completed = await InstallWorkflow.RunAsync(
                    reinstaller.IsInstalled,
                    () => MessageBox.Show(this,
                        "SixView가 설치되어 있습니다. SixView만 다시 설치할까요?\r\n\r\n기존 버전은 백업하지 않습니다. 새 설치가 취소되거나 실패하면 자동 복원되지 않습니다.",
                        "BKID_AutoCrop_SixView 재설치", MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes,
                    reinstaller.Run);
                if (!completed)
                {
                    status.Text = "기존 설치를 그대로 유지했습니다. 삭제하거나 재설치하지 않았습니다.";
                    install.Enabled = true;
                    return;
                }
                status.Text = "설치 완료. 툴바는 최초 실행 시 한 번 표시됩니다.\r\n이후에는 Rhino에 저장된 도킹·표시 상태를 따릅니다.";
                install.Text = "설치 완료";
            }
            catch (Exception error)
            {
                status.Text = "설치하지 못했습니다. 아래 오류를 확인하세요.\r\n이전 버전은 백업하거나 자동 복원하지 않습니다.";
                MessageBox.Show(this, error.Message, "BKID_AutoCrop_SixView 설치", MessageBoxButtons.OK, MessageBoxIcon.Error);
                install.Enabled = true;
            }
            finally { busy = false; }
        }
    }

    internal sealed class Reinstall
    {
        private const string PluginId = InstallationIdentity.PluginId;
        private readonly string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private readonly ManagedFiles managed = new ManagedFiles();
        private readonly List<string> registry = new List<string>();

        internal bool IsInstalled()
        {
            if (FindInstalledPlugins().Any()) return true;
            foreach (var version in new[] { "6.0", "8.0" })
            foreach (var id in new[] { PluginId, "{" + PluginId + "}" })
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                string path = @"Software\McNeel\Rhinoceros\" + version + @"\Plug-ins\" + id;
                using (var key = hive.OpenSubKey(path))
                {
                    var file = key == null ? null : key.GetValue("FileName") as string;
                    if (!string.IsNullOrEmpty(file) && File.Exists(file)) return true;
                }
            }
            return false;
        }

        internal void Run(bool replacementAllowed)
        {
            if (Process.GetProcessesByName("Rhino").Length != 0)
                throw new InvalidOperationException("열려 있는 Rhino를 모두 종료한 뒤 다시 설치하세요. 작업은 아직 변경하지 않았습니다.");
            InstallWorkflow.RequireConsent(IsInstalled(), replacementAllowed);
            string temporary = Path.Combine(Path.GetTempPath(), "BKID_AutoCrop_SixView_Setup_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try { Install(temporary, replacementAllowed); }
            finally
            {
                // Remove only this run's extracted installer, never prior installations or user output.
                try { Directory.Delete(temporary, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private void Install(string temporary, bool replacementAllowed)
        {
            string package = Path.Combine(temporary, "BKID_AutoCrop_SixView_3.1.1.rhi");
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("BKID.Setup.Package.rhi"))
            using (var output = File.Create(package)) input.CopyTo(output);
            byte[] expectedPlugin;
            byte[] expectedToolbar;
            using (var stream = File.OpenRead(package))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                expectedPlugin = Read(zip.GetEntry("BKID.AutoCropSixView.rhp"));
                expectedToolbar = Read(zip.GetEntry("BKID.AutoCropSixView.rui"));
            }
            var verification = new InstallationVerification(expectedPlugin, expectedToolbar);
            // Discover and validate all candidates before removing any installation.
            var oldPlugins = FindInstalledPlugins().ToList();
            foreach (var version in new[] { "6.0", "8.0" })
            foreach (var id in new[] { PluginId, "{" + PluginId + "}" })
            {
                string keyPath = @"Software\McNeel\Rhinoceros\" + version + @"\Plug-ins\" + id;
                using (var machine = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    var machineFile = machine == null ? null : machine.GetValue("FileName") as string;
                    if (!string.IsNullOrEmpty(machineFile) && File.Exists(machineFile))
                        throw new InvalidOperationException("모든 사용자용 BKID 설치가 있습니다. Rhino에서 해당 설치를 제거한 뒤 현재 사용자용으로 다시 설치하세요.");
                }
                using (var key = Registry.CurrentUser.OpenSubKey(keyPath, true))
                {
                    if (key == null) continue;
                    registry.Add(keyPath);
                }
            }
            var files = new List<string>();
            foreach (var file in oldPlugins)
            {
                files.Add(file);
                files.Add(Path.ChangeExtension(file, ".rui"));
            }
            // Only this independent plugin's current runtime belongs to SixView.
            foreach (var name in new[] { "BKID_AutoCrop_SixView.py", "BKID_AutoCrop_SixView_x1.py", "BKID_AutoCrop_SixView_x2.py", "BKID_AutoCrop_SixView_x3.py", "BKID_AutoCrop_SixView_x4.py" })
                files.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BKID", "BKID_AutoCrop_SixView", "runtime", "3.1.1", name));
            managed.Validate(files);
            // A new installation may have appeared since the wizard checked.
            InstallWorkflow.RequireConsent(IsInstalled(), replacementAllowed);
            try
            {
                foreach (var file in files) managed.Remove(file);
                foreach (var key in registry) Registry.CurrentUser.DeleteSubKeyTree(key, false);
                var start = new ProcessStartInfo(package) { UseShellExecute = true };
                int? installerExitCode = null;
                using (var installer = Process.Start(start))
                {
                    if (installer != null)
                    {
                        installer.WaitForExit();
                        installerExitCode = installer.ExitCode;
                    }
                }
                // The installed new payload is authoritative, including nonzero engine exits.
                InstallationVerification.WaitForComplete(installerExitCode,
                    () => FindRegisteredPlugins().Any(verification.Matches) || FindInstalledPlugins().Any(verification.Matches),
                    () => System.Threading.Thread.Sleep(250));
                return;
            }
            catch (Exception original)
            {
                throw new InvalidOperationException(original.Message + "\r\n이전 버전은 백업하거나 자동 복원하지 않습니다. 새 버전 설치를 다시 진행해 주세요.", original);
            }
        }

        private IEnumerable<string> FindRegisteredPlugins()
        {
            foreach (var version in new[] { "6.0", "8.0" })
            foreach (var id in new[] { PluginId, "{" + PluginId + "}" })
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                string path = @"Software\McNeel\Rhinoceros\" + version + @"\Plug-ins\" + id;
                using (var key = hive.OpenSubKey(path))
                {
                    var file = key == null ? null : key.GetValue("FileName") as string;
                    if (!string.IsNullOrWhiteSpace(file)) yield return file.Trim().Trim('"');
                }
            }
        }

        private IEnumerable<string> FindInstalledPlugins()
        {
            foreach (var version in new[] { "6.0", "8.0" })
            {
                string root = Path.Combine(roaming, "McNeel", "Rhinoceros", version, "Plug-ins");
                if (!Directory.Exists(root)) continue;
                foreach (var file in InstallationIdentity.EnumeratePluginFiles(root))
                {
                    var assembly = Assembly.ReflectionOnlyLoad(File.ReadAllBytes(file));
                    var guid = assembly.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == typeof(System.Runtime.InteropServices.GuidAttribute).FullName);
                    if (guid != null && string.Equals((string)guid.ConstructorArguments[0].Value, PluginId, StringComparison.OrdinalIgnoreCase)) yield return file;
                }
            }
        }

        private static byte[] Read(ZipArchiveEntry entry)
        {
            if (entry == null) throw new InvalidDataException("설치 패키지 파일이 누락되었습니다.");
            using (var stream = entry.Open())
            using (var memory = new MemoryStream()) { stream.CopyTo(memory); return memory.ToArray(); }
        }
    }
}
