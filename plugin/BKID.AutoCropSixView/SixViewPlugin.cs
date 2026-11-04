using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Command = Rhino.Commands.Command;
using Rhino.PlugIns;
using Rhino.UI;

namespace BKID.AutoCropSixView
{
    [Guid("EBA693A2-8ACF-5DC1-9A01-FC9CCB4D3DE0")]
    public sealed class SixViewPlugin : PlugIn
    {
        private const string BindingRevision = "six-view-3.1.0";
        private const string BindingRevisionKey = "ToolbarBindingRevision";
        private const string ToolbarInitializedKey = "NativeToolbarInitialized";
        private const string CaptureOutputFolderKey = "CaptureOutputFolder";
        private bool waitingForStartup;
        private int startupAttempts;
        public static SixViewPlugin Instance { get; private set; }

        public SixViewPlugin() { Instance = this; }
        public override PlugInLoadTime LoadTime { get { return PlugInLoadTime.AtStartup; } }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            RhinoApp.WriteLine("AutoCrop_SixView " + GetType().Assembly.GetName().Version + " loaded | Rhino " + RhinoApp.Version + " | CLR " + Environment.Version);
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
                if (Settings.GetString(BindingRevisionKey, "") != BindingRevision)
                {
                    try { RefreshToolbarBindings(); }
                    catch (Exception refreshError)
                    {
                        // An optional saved-toolbar migration must not block first display.
                        RhinoApp.WriteLine("SixView saved-toolbar refresh: " + refreshError);
                        RhinoApp.WriteLine("Run BKIDAutoCropSixViewRefresh to retry the saved-toolbar update.");
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
                    RhinoApp.WriteLine("AutoCrop_SixView toolbar startup: " + error);
                    RhinoApp.WriteLine("Run BKIDAutoCropSixView to retry opening the toolbar.");
                }
            }
        }

        private void StopWaitingForStartup()
        {
            if (!waitingForStartup) return;
            RhinoApp.Idle -= OnStartupIdle;
            waitingForStartup = false;
        }

        internal void RefreshToolbarBindings()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "BKID.AutoCropSixView.rui");
            int refreshed = ToolbarRefresh.Apply(path);
            RhinoApp.WriteLine("SixView toolbar binding refresh: " + refreshed + " collection(s).");
            Settings.SetString(BindingRevisionKey, BindingRevision);
        }

        internal void ShowToolbar()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "BKID.AutoCropSixView.rui");
            if (!File.Exists(path)) throw new FileNotFoundException("Reinstall AutoCrop_SixView: toolbar sidecar missing.", path);
            var toolbar = RhinoApp.ToolbarFiles.FindByPath(path) ?? RhinoApp.ToolbarFiles.Open(path);
            if (toolbar == null) throw new InvalidOperationException("Rhino could not open toolbar: " + path);
            var group = toolbar.GetGroup("AutoCrop_SixView");
            if (group == null)
            {
                var names = new System.Collections.Generic.List<string>();
                for (int i = 0; i < toolbar.GroupCount; i++) names.Add(toolbar.GetGroup(i).Name);
                throw new InvalidOperationException("AutoCrop_SixView toolbar group unavailable; groups: " + string.Join(", ", names));
            }
            group.Visible = true;
            if (!group.Visible) throw new InvalidOperationException("Rhino did not display the SixView toolbar group.");
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
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AutoCrop_SixView_Captures")
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
                                      "BKID", "AutoCrop_SixView", "runtime", "3.1.0");
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, "AutoCrop_SixView.py");
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("BKID.AutoCropSixView.Capture.py"))
            {
                if (input == null) throw new InvalidOperationException("Missing embedded capture script.");
                using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.Read))
                    input.CopyTo(output);
            }
            var runner = Path.Combine(folder, "AutoCrop_SixView_x" + scale + ".py");
            File.WriteAllText(runner, CaptureRunner.Build(scale, CaptureOutputFolder), new System.Text.UTF8Encoding(false));
            return runner;
        }

        protected override void OnShutdown()
        {
            StopWaitingForStartup();
            base.OnShutdown();
        }
    }

    [Guid("7A8330A5-1097-55D0-86F3-EC6ED77A9C2C")]
    public sealed class SixViewToolbarCommand : Command
    {
        public override string EnglishName { get { return "BKIDAutoCropSixView"; } }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try { SixViewPlugin.Instance.ShowToolbar(); return Result.Success; }
            catch (Exception error) { RhinoApp.WriteLine("AutoCrop_SixView: " + error); return Result.Failure; }
        }
    }

    [Guid("ECFDD284-31A8-5395-BE35-D9DF55932867")]
    public sealed class SixViewRefreshCommand : Command
    {
        public override string EnglishName { get { return "BKIDAutoCropSixViewRefresh"; } }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                SixViewPlugin.Instance.RefreshToolbarBindings();
                RhinoApp.WriteLine("SixView: resolution dialog command is BKIDAutoCropSixViewResolution.");
                return Result.Success;
            }
            catch (Exception error) { RhinoApp.WriteLine("SixView toolbar update: " + error); return Result.Failure; }
        }
    }

    [Guid("367A9734-0ACC-504C-8DBD-E73817DF7FD8")]
    [CommandStyle(Style.ScriptRunner)]
    public sealed class SixViewCaptureCommand : Command
    {
        public override string EnglishName { get { return "BKIDAutoCropSixViewCapture"; } }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return CaptureExecution.Run(doc, 1);
        }
    }

    [Guid("C08B573F-7567-5F7D-A794-C27AB56C5993")]
    public sealed class SixViewFolderCommand : Command
    {
        public override string EnglishName { get { return "BKIDAutoCropSixViewFolder"; } }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                return SixViewPlugin.Instance.ChooseCaptureOutputFolder(RhinoEtoApp.MainWindow)
                    ? Result.Success : Result.Cancel;
            }
            catch (Exception error) { RhinoApp.WriteLine("PNG 저장 폴더 변경: " + error); return Result.Failure; }
        }
    }

    [Guid("F996C01F-F841-5C51-9AFE-D4EB40592B7E")]
    [CommandStyle(Style.ScriptRunner)]
    public sealed class SixViewResolutionCommand : Command
    {
        public override string EnglishName { get { return "BKIDAutoCropSixViewResolution"; } }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var view = doc == null ? null : doc.Views.ActiveView;
            if (view == null || view is Rhino.Display.RhinoPageView)
            {
                RhinoApp.WriteLine("SixView: use an active model viewport.");
                return Result.Failure;
            }
            try
            {
                int scale;
                using (var dialog = new CaptureScaleDialog(view.ActiveViewport.Size.Width, view.ActiveViewport.Size.Height, SixViewPlugin.Instance))
                    scale = dialog.ShowModal(RhinoEtoApp.MainWindow);
                if (scale < 2 || scale > 4) return Result.Cancel;
                return CaptureExecution.Run(doc, scale);
            }
            catch (Exception error) { RhinoApp.WriteLine("AutoCrop_SixView: " + error); return Result.Failure; }
        }
    }

    internal static class CaptureExecution
    {
        internal static Result Run(RhinoDoc doc, int scale)
        {
            if (doc == null) return Result.Failure;
            try
            {
                var path = SixViewPlugin.Instance.PrepareCaptureScript(scale);
                bool started = RhinoApp.RunScript("_-RunPythonScript \"" + path + "\"", false);
                if (!started)
                    RhinoApp.WriteLine("SixView: _RunPythonScript를 실행하지 못했습니다. RhinoPython 활성 상태와 F2 명령 기록을 확인해 주세요.");
                return started ? Result.Success : Result.Failure;
            }
            catch (Exception error) { RhinoApp.WriteLine("AutoCrop_SixView: " + error); return Result.Failure; }
        }
    }
}
