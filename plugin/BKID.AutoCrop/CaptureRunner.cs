using System;
using System.Globalization;
using System.Text;

namespace BKID.AutoCrop
{
    // Explicit per-call argument; never rewrite the capture module's defaults.
    internal static class CaptureRunner
    {
        internal static string Build(int scale, string outputFolder = null)
        {
            if (scale < 1 || scale > 4) throw new ArgumentOutOfRangeException("scale");
            if (outputFolder != null && string.IsNullOrWhiteSpace(outputFolder))
                throw new ArgumentException("Choose a PNG output folder.", "outputFolder");
            // Encode paths as data: Korean names, backslashes and quotes must survive Python 2 literals.
            string folderArgument = outputFolder == null ? "" : ", output_folder=base64.b64decode('" +
                Convert.ToBase64String(Encoding.UTF8.GetBytes(outputFolder)) + "').decode('utf-8')";
            return "# -*- coding: utf-8 -*-\n" +
                   "import os\nimport runpy\nimport traceback\nimport base64\n" +
                   "try:\n" +
                   "    scope = runpy.run_path(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'BKID_AutoCrop.py'))\n" +
                   "    scope['run'](capture_scale=" + scale.ToString(CultureInfo.InvariantCulture) + folderArgument + ")\n" +
                   "except Exception:\n" +
                   "    import Rhino\n" +
                   "    Rhino.RhinoApp.WriteLine(traceback.format_exc())\n";
        }
    }
}
