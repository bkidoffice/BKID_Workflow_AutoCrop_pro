using System.Diagnostics;
using BKID.AutoCrop;

if (args.Length != 1) throw new ArgumentException("Pass the Python executable path.");
var folder = Path.Combine(Path.GetTempPath(), "BKID 배율 테스트 " + Guid.NewGuid());
Directory.CreateDirectory(folder);
try
{
    string capture = Path.Combine(folder, "BKID_AutoCrop.py");
    string source = "import os\nimport json\nCAPTURE_SCALE = 1\ndef run(capture_scale=None, output_folder=None):\n    with open(os.path.join(os.path.dirname(__file__), 'result.txt'), 'w') as f:\n        json.dump([capture_scale, CAPTURE_SCALE, output_folder], f)\n    if output_folder and os.path.isdir(output_folder):\n        with open(os.path.join(output_folder, 'capture_' + str(capture_scale) + '.png'), 'wb') as f:\n            f.write(b'capture-test')\n";
    File.WriteAllText(capture, source);
    var selectedFolder = Path.Combine(folder, "PNG 결과 '프로젝트 A'");
    Directory.CreateDirectory(selectedFolder);
    foreach (string outputFolder in new[] { null, selectedFolder, @"\\server\공유 폴더\고객 'A'", @"D:\작업 결과\한글 폴더" })
    foreach (int scale in new[] { 1, 2, 3, 4 })
    {
        string driver = Path.Combine(folder, "capture.py");
        File.WriteAllText(driver, CaptureRunner.Build(scale, outputFolder));
        var start = new ProcessStartInfo(args[0]) { UseShellExecute = false, RedirectStandardError = true };
        start.ArgumentList.Add(driver);
        using (var process = Process.Start(start)!)
        {
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new Exception(error);
        }
        using var result = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "result.txt")));
        var values = result.RootElement;
        if (values[0].GetInt32() != scale || values[1].GetInt32() != 1 || values[2].GetString() != outputFolder)
            throw new Exception("Scale/output folder changed or the module default was mutated.");
        if (outputFolder == selectedFolder && !File.Exists(Path.Combine(selectedFolder, "capture_" + scale + ".png")))
            throw new Exception("Capture did not write into the selected folder.");
        if (File.ReadAllText(capture) != source) throw new Exception("Capture source was modified.");
    }
    foreach (int invalid in new[] { -1, 0, 5 })
    {
        bool rejected = false;
        try { CaptureRunner.Build(invalid); } catch (ArgumentOutOfRangeException) { rejected = true; }
        if (!rejected) throw new Exception("Invalid high-resolution factor accepted.");
    }
    foreach (string invalid in new[] { "", "   " })
    {
        bool rejected = false;
        try { CaptureRunner.Build(1, invalid); } catch (ArgumentException) { rejected = true; }
        if (!rejected) throw new Exception("Empty output folder accepted.");
    }
    if (File.Exists(Path.Combine(folder, "capture_1.png"))) throw new Exception("Output used the runtime folder.");
    Console.WriteLine("PASS: actual generated runners pass x1/x2/x3/x4 and chosen folders, write to the selected directory, preserve defaults/source, support Korean/quoted/UNC/Windows paths and reject invalid factors/empty folders.");
}
finally { Directory.Delete(folder, true); }
