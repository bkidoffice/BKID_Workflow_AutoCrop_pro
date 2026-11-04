using System;
using Eto.Drawing;
using Eto.Forms;

namespace BKID.AutoCropSixView
{
    internal sealed class CaptureScaleDialog : Dialog<int>
    {
        internal CaptureScaleDialog(int viewportWidth, int viewportHeight, SixViewPlugin plugin)
        {
            Title = "SixView 6 Views · 배율 · 저장 폴더";
            Resizable = false;
            Padding = new Padding(16);
            var layout = new DynamicLayout { Spacing = new Size(8, 8) };
            var folder = new Label { Text = "저장 폴더: " + plugin.CaptureOutputFolder, Width = 420, Wrap = WrapMode.Word };
            layout.AddRow(folder);
            var changeFolder = new Button { Text = "저장 폴더 변경…" };
            changeFolder.Click += (sender, args) =>
            {
                try
                {
                    plugin.ChooseCaptureOutputFolder(this);
                    folder.Text = "저장 폴더: " + plugin.CaptureOutputFolder;
                }
                catch (Exception error)
                {
                    MessageBox.Show(this, "저장 폴더를 변경하지 못했습니다.\n" + error.Message, "AutoCrop_SixView");
                }
            };
            layout.AddRow(changeFolder);
            layout.AddRow(new Label { Text = "변경한 폴더는 이후 좌클릭·우클릭 캡처에도 적용됩니다." });
            layout.AddRow(new Label { Text = "6장 공통 캡처 배율을 선택하세요." });
            foreach (int factor in new[] { 2, 3, 4 })
            {
                int selected = factor;
                var button = new Button
                {
                    Text = string.Format("×{0}  ({1} × {2} px)", factor,
                                         (long)viewportWidth * factor, (long)viewportHeight * factor),
                    Height = 36
                };
                button.Click += (sender, args) => Close(selected);
                layout.AddRow(button);
            }
            layout.AddRow(new Label { Text = "선택 즉시 6장을 저장합니다. 최종 크기는 자동 크롭에 따라 달라집니다." });
            var cancel = new Button { Text = "취소" };
            cancel.Click += (sender, args) => Close(0);
            AbortButton = cancel;
            layout.AddRow(null, cancel);
            Content = layout;
        }
    }
}
