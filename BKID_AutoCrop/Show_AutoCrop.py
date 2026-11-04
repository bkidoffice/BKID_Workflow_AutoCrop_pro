# -*- coding: utf-8 -*-
"""Modeless logo button for Rhino 6/8 when native toolbar groups are unavailable."""
import os
import Rhino
import Eto.Forms as forms
import Eto.Drawing as drawing
import scriptcontext as sc

KEY = 'BKID.AutoCrop.LogoWindow'


def show():
    previous = sc.sticky.get(KEY)
    if previous is not None:
        previous.BringToFront()
        return previous
    image = drawing.Bitmap(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                       'assets', 'icon_32.png'))
    window = forms.Form()
    window.Title = 'BKID AutoCrop'
    window.Resizable = False
    window.Padding = drawing.Padding(12)
    window.Owner = Rhino.UI.RhinoEtoApp.MainWindow
    button = forms.Button()
    button.Text = 'Run AutoCrop'
    button.Image = image
    button.Size = drawing.Size(160, 64)

    def clicked(sender, event):
        if not Rhino.RhinoApp.RunScript('! RunAutoCrop', False):
            Rhino.RhinoApp.WriteLine('AutoCrop command failed. Check command history (F2).')

    def closed(sender, event):
        if sc.sticky.get(KEY) is window:
            del sc.sticky[KEY]
        image.Dispose()

    button.Click += clicked
    window.Closed += closed
    window.Content = button
    sc.sticky[KEY] = window
    try:
        window.Show()
    except Exception:
        del sc.sticky[KEY]
        image.Dispose()
        raise
    return window


if __name__ == '__main__':
    show()
