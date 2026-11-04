using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Command = Rhino.Commands.Command;
using Rhino.PlugIns;
using Rhino.UI;

namespace BKID.AutoCrop
{
    [Guid("3881C10B-30A8-4FE5-BB48-9E53A6FEA1F4")]
    public sealed class AutoCropPlugin : PlugIn
    {
        private const string BindingRevision = "six-view-3.0.0";
        private const string BindingRevisionKey = "ToolbarBindingRevision";
        private const string ToolbarInitializedKey = "NativeToolbarInitialized";
        private const string CaptureOutputFolderKey = "CaptureOutputFolder";
        private bool waitingForStartup;
        private int startupAttempts;
        public static AutoCropPlugin Instance { get; private set; }

        public AutoCropPlugin() { Instance = this; }
        public override PlugInLoadTime LoadTime { get { return PlugInLoadTime.AtStartup; } }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            RhinoApp.WriteLine("BKID Workflowboost " + GetType().Assembly.GetName().Version + " loaded | Rhino " + RhinoApp.Version + " | CLR " + Environment.Version);
            // Afterwards Rhino restores the user's docking and visibility state.
            if (!Settings.GetBool(ToolbarInitializedKey, false) || Settings.GetString(BindingRevisionKey, "") != BindingRevision)
            {
                waitingForStartup = true;
                RhinoApp.Idle += OnStartupIdle;
            }
            return LoadReturnCode.Success;
        }

        private void OnStartupIdle(object sender, EventArgs e)
        {
            // AtStartup can run before Rhino has a document and an owner window.
            if (RhinoDoc.ActiveDoc == null || RhinoApp.MainWindowHandle() == IntPtr.Zero) return;
            startupAttempts++;
            try
            {
                if (startupAttempts == 1)
                {
                    try { RemoveLegacyRegistration(); }
                    catch (Exception legacyError) { RhinoApp.WriteLine("AutoCrop legacy cleanup: " + legacyError); }
                }
                if (Settings.GetString(BindingRevisionKey, "") != BindingRevision)
                {
                    try { RefreshToolbarBindings(); }
                    catch (Exception refreshError)
                    {
                        // An optional saved-toolbar migration must not block first display.
                        RhinoApp.WriteLine("AutoCrop saved-toolbar refresh: " + refreshError);
                        RhinoApp.WriteLine("Run BKIDAutoCropRefresh to retry the saved-toolbar update.");
                    }
                }
                if (!Settings.GetBool(ToolbarInitializedKey, false)) ShowToolbar();
                StopWaitingForStartup();
            }
            catch (Exception error)
            {
                // Rhino 8 may finish importing a sidecar on a later UI idle cycle.
                if (startupAttempts >= 5)
                {
                    StopWaitingForStartup();
                    RhinoApp.WriteLine("BKID AutoCrop toolbar startup: " + error);
                    RhinoApp.WriteLine("Run BKIDAutoCrop to retry opening the toolbar.");
                }
            }
        }

        private void StopWaitingForStartup()
        {
            if (!waitingForStartup) return;
            RhinoApp.Idle -= OnStartupIdle;
            waitingForStartup = false;
        }

        private void RemoveLegacyRegistration()
        {
            foreach (var alias in new[] { "RunAutoCrop", "ShowAutoCrop" })
            {
                var macro = Rhino.ApplicationSettings.CommandAliasList.GetMacro(alias);
                if (macro != null && (macro.IndexOf("BKID_AutoCrop.py", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      macro.IndexOf("Show_AutoCrop.py", StringComparison.OrdinalIgnoreCase) >= 0))
                    Rhino.ApplicationSettings.CommandAliasList.Delete(alias);
            }
            var major = RhinoApp.Version.Major.ToString();
            var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                      "BKID", "AutoCrop", "Rhino" + major, "BKID_AutoCrop.rui");
            var old = RhinoApp.ToolbarFiles.FindByPath(legacy);
            if (old != null) old.Close(false);
        }

        internal void RefreshToolbarBindings()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "BKID.AutoCrop.rui");
            int refreshed = ToolbarRefresh.Apply(path);
            RhinoApp.WriteLine("AutoCrop toolbar binding refresh: " + refreshed + " collection(s).");
            Settings.SetString(BindingRevisionKey, BindingRevision);
        }

        internal void ShowToolbar()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "BKID.AutoCrop.rui");
            if (!File.Exists(path)) throw new FileNotFoundException("Reinstall BKID AutoCrop: toolbar sidecar missing.", path);
            var toolbar = RhinoApp.ToolbarFiles.FindByPath(path) ?? RhinoApp.ToolbarFiles.Open(path);
            if (toolbar == null) throw new InvalidOperationException("Rhino could not open toolbar: " + path);
            var group = toolbar.GetGroup("BKID Workflowboost");
            if (group == null)
            {
                var names = new System.Collections.Generic.List<string>();
                for (int i = 0; i < toolbar.GroupCount; i++) names.Add(toolbar.GetGroup(i).Name);
                throw new InvalidOperationException("BKID AutoCrop toolbar group unavailable; groups: " + string.Join(", ", names));
            }
            group.Visible = true;
            if (!group.Visible) throw new InvalidOperationException("Rhino did not display the AutoCrop toolbar group.");
            // Mark only successful initialization; failed first starts can retry.
            Settings.SetBool(ToolbarInitializedKey, true);
            StopWaitingForStartup();
        }

        internal string CaptureOutputFolder
        {
            get
            {
                string saved = Settings.GetString(CaptureOutputFolderKey, "");
                return string.IsNullOrWhiteSpace(saved)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "BKID_Captures")
                    : saved;
            }
        }

        internal bool ChooseCaptureOutputFolder(Eto.Forms.Window owner)
        {
            using (var dialog = new Eto.Forms.SelectFolderDialog { Title = "PNG 저장 폴더 선택" })
            {
                string current = CaptureOutputFolder;
                dialog.Directory = Directory.Exists(current) ? current :
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (dialog.ShowDialog(owner) != Eto.Forms.DialogResult.Ok) return false;
                string selected = Path.GetFullPath(dialog.Directory);
                if (!Directory.Exists(selected)) throw new DirectoryNotFoundException("선택한 저장 폴더가 없습니다.");
                Settings.SetString(CaptureOutputFolderKey, selected);
                RhinoApp.WriteLine("PNG 저장 폴더: " + selected);
                return true;
            }
        }

        internal string PrepareCaptureScript(int scale)
        {
            if (scale < 1 || scale > 4) throw new ArgumentOutOfRangeException("scale");
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                      "BKID", "AutoCrop", "runtime", "3.0.0");
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, "BKID_AutoCrop.py");
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("BKID.AutoCrop.Capture.py"))
            {
                if (input == null) throw new InvalidOperationException("Missing embedded capture script.");
                using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.Read))
                    input.CopyTo(output);
            }
            var runner = Path.Combine(folder, "BKID_AutoCrop_x" + scale + ".py");
            File.WriteAllText(runner, CaptureRunner.Build(scale, CaptureOutputFolder), new System.Text.UTF8Encoding(false));
            return runner;
        }

        protected override void OnShutdown()
        {
            StopWaitingForStartup();
            base.OnShutdown();
        }
    }

    [Guid("E63167B6-42F8-4B81-86B3-31565BCB239E")]
    public sealed class AutoCropToolbarCommand : Command
    {
        public override string EnglishName { get { return "BKIDAutoCrop"; } }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try { AutoCropPlugin.Instance.ShowToolbar(); return Result.Success; }
            catch (Exception error) { RhinoApp.WriteLine("BKID AutoCrop: " + error); return Result.Failure; }
        }
    }

    [Guid("6D052703-1374-40E0-91BE-AC84F3BF03EB")]
    public sealed class AutoCropRefreshCommand : Command
    {
        public override string EnglishName { get { return "BKIDAutoCropRefresh"; } }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                AutoCropPlugin.Instance.RefreshToolbarBindings();
                RhinoApp.WriteLine("AutoCrop: resolution dialog command is BKIDAutoCropResolution.");
                return Result.Success;
            }
            catch (Exception error) { RhinoApp.WriteLine("AutoCrop toolbar update: " + error); return Result.Failure; }
        }
    }

    [Guid("BB6FF93A-344E-4027-BAA4-A8AC780418C7")]
    [CommandStyle(Style.ScriptRunner)]
    public sealed class AutoCropCaptureCommand : Command
    {
        public override string EnglishName { get { return "BKIDAutoCropCapture"; } }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return CaptureExecution.Run(doc, 1);
        }
    }

    [Guid("FA041EA0-386A-4F7D-B7B0-F473ED36C926")]
    public sealed class AutoCropFolderCommand : Command
    {
        public override string EnglishName { get { return "BKIDAutoCropFolder"; } }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                return AutoCropPlugin.Instance.ChooseCaptureOutputFolder(RhinoEtoApp.MainWindow)
                    ? Result.Success : Result.Cancel;
            }
            catch (Exception error) { RhinoApp.WriteLine("PNG 저장 폴더 변경: " + error); return Result.Failure; }
        }
    }

    [Guid("EB3D4348-12E2-49A3-B36C-940FE280DADB")]
    [CommandStyle(Style.ScriptRunner)]
    public sealed class AutoCropResolutionCommand : Command
    {
        public override string EnglishName { get { return "BKIDAutoCropResolution"; } }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var view = doc == null ? null : doc.Views.ActiveView;
            if (view == null || view is Rhino.Display.RhinoPageView)
            {
                RhinoApp.WriteLine("AutoCrop: use an active model viewport.");
                return Result.Failure;
            }
            try
            {
                int scale;
                using (var dialog = new CaptureScaleDialog(view.ActiveViewport.Size.Width, view.ActiveViewport.Size.Height, AutoCropPlugin.Instance))
                    scale = dialog.ShowModal(RhinoEtoApp.MainWindow);
                if (scale < 2 || scale > 4) return Result.Cancel;
                return CaptureExecution.Run(doc, scale);
            }
            catch (Exception error) { RhinoApp.WriteLine("BKID AutoCrop: " + error); return Result.Failure; }
        }
    }

    internal static class CaptureExecution
    {
        internal static Result Run(RhinoDoc doc, int scale)
        {
            if (doc == null) return Result.Failure;
            try
            {
                var path = AutoCropPlugin.Instance.PrepareCaptureScript(scale);
                bool started = RhinoApp.RunScript("_-RunPythonScript \"" + path + "\"", false);
                if (!started)
                    RhinoApp.WriteLine("AutoCrop: _RunPythonScript를 실행하지 못했습니다. RhinoPython 활성 상태와 F2 명령 기록을 확인해 주세요.");
                return started ? Result.Success : Result.Failure;
            }
            catch (Exception error) { RhinoApp.WriteLine("BKID AutoCrop: " + error); return Result.Failure; }
        }
    }
}
