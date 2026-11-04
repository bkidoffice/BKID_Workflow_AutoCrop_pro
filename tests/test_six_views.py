import importlib.util
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace as NS
import unittest
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[1]


class SixViewTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location('six_views', ROOT / 'AutoCrop_SixView/AutoCrop_SixView.py')
        self.m = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.m)

    def test_fit_maximizes_one_dimension_with_eight_pixel_guard(self):
        for w, h in ((10, 30), (100, 1), (0, 0), (1, 100)):
            for scale in (1, 2, 3, 4):
                pw, ph = 800 * scale, 600 * scale
                hw, hh = self.m.fitted_half_size(w, h, pw, ph)
                self.assertAlmostEqual(hw / hh, pw / ph)
                projected_w, projected_h = w / (2*hw) * pw, h / (2*hh) * ph
                self.assertLessEqual(projected_w, pw-8 + 1e-8)
                self.assertLessEqual(projected_h, ph-8 + 1e-8)
                if w or h:
                    self.assertTrue(abs(projected_w-(pw-8)) < 1e-7 or
                                    abs(projected_h-(ph-8)) < 1e-7)

    def host(self, selected=True):
        events, projections, restored, hidden, shown = [], [], [], [], []
        class Event:
            def __iadd__(self, handler):
                events.append(handler)
                return self
            def __isub__(self, handler):
                events.remove(handler)
                return self
        class Box:
            IsValid = True
            def Union(self, other): pass
        class Boxes:
            @property
            def Empty(self): return Box()
        objects = [NS(Id=i, IsHidden=False, IsDeleted=False, Visible=True,
                      ObjectType=1, Geometry=NS(GetBoundingBox=lambda _: Box())) for i in (1, 2)]
        vp = NS(Name='Original', CameraTarget='original-target',
                SetCameraTarget=lambda target, _: None, SetProjection=lambda p, name, _: projections.append(name) or True,
                ZoomBoundingBox=lambda box: True,
                SetViewProjection=lambda saved, flag: restored.append(saved) or True)
        view = NS(ActiveViewport=vp, Redraw=lambda: None)
        doc = NS(Views=NS(ActiveView=view), Objects=NS(
            GetObjectList=lambda _: objects,
            GetSelectedObjects=lambda *_: objects[:1] if selected else [],
            Hide=lambda id, _: hidden.append(id) or True,
            Show=lambda id, _: shown.append(id) or True))
        app = NS(EscapeKeyPressed=Event(), Wait=lambda: None, WriteLine=lambda _: None)
        modules = {
            'Rhino': NS(RhinoDoc=NS(ActiveDoc=doc), RhinoApp=app,
                        Display=NS(RhinoPageView=type('Page', (), {}))),
            'Rhino.Geometry': NS(BoundingBox=Boxes()),
            'Rhino.DocObjects': NS(ObjectType=NS(AnyObject=0, Light=2, Grip=3),
                                   ViewportInfo=lambda vp: 'original-view'),
            'Rhino.Display': NS(DefinedViewportProjection=NS(**{
                name.title(): name for name in self.m.VIEW_NAMES})),
            'System': NS(DateTime=NS(Now=NS(ToString=lambda _: 'timestamp')),
                         Guid=NS(NewGuid=lambda: NS(ToString=lambda _: 'uniqueid')))}
        return modules, events, projections, restored, hidden, shown, vp

    def exercise(self, selected=True, fail_at=None):
        modules, events, views, restored, hidden, shown, vp = self.host(selected)
        calls = []
        def save(doc, view, scale, folder, name):
            if fail_at == name:
                raise RuntimeError('render failure')
            path = Path(folder) / name
            path.write_bytes(b'host-placeholder')
            calls.append((name, scale))
            return str(path)
        with tempfile.TemporaryDirectory() as tmp, patch.dict(sys.modules, modules), \
                patch.object(self.m, 'initialize_runtime'), patch.object(self.m, 'verify_bitmap_runtime'), \
                patch.object(self.m, 'fit_viewport'), patch.object(self.m, 'save_capture', side_effect=save):
            if fail_at:
                with self.assertRaisesRegex(RuntimeError, 'render failure'):
                    self.m.run(3, tmp)
                self.assertEqual(list(Path(tmp).iterdir()), [])
            else:
                paths = self.m.run(3, tmp)
                self.assertEqual([Path(p).name for p in paths], [v+'.png' for v in self.m.VIEW_NAMES])
                self.assertTrue(all(Path(p).exists() for p in paths))
                self.assertEqual(calls, [(v+'.png', 3) for v in self.m.VIEW_NAMES])
            self.assertEqual(restored, ['original-view'])
            self.assertEqual(vp.Name, 'Original')
            self.assertEqual(events, [])
            self.assertEqual(hidden, [2] if selected else [])
            self.assertEqual(shown, hidden)
        return views

    def test_six_projections_scale_and_selection_restored(self):
        self.assertEqual(self.exercise(), list(self.m.VIEW_NAMES))

    def test_whole_visible_model_without_selection(self):
        self.assertEqual(self.exercise(selected=False), list(self.m.VIEW_NAMES))

    def test_partial_batch_removed_and_view_restored_on_failure(self):
        self.exercise(fail_at='LEFT.png')


if __name__ == '__main__':
    unittest.main()
