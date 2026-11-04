using Rhino;
using Rhino.Display;
internal static class CaptureApi
{
 private static void OnEscape(object sender, System.EventArgs args) { }
 internal static void Check(RhinoDoc doc, RhinoView view)
 {
  var vp = view.ActiveViewport;
  var target = vp.CameraTarget;
  vp.SetCameraTarget(target, false);
  var info = new Rhino.DocObjects.ViewportInfo(vp);
  double l,r,b,t,n,f;
  bool frustum = info.GetFrustum(out l,out r,out b,out t,out n,out f);
  bool setFrustum = info.SetFrustum(l,r,b,t,n,f);
  bool applied = vp.SetViewProjection(info, false);
  bool projection = vp.SetProjection(DefinedViewportProjection.Front, "FRONT", false);
  bool fit = vp.ZoomBoundingBox(Rhino.Geometry.BoundingBox.Empty);
  RhinoApp.EscapeKeyPressed += OnEscape;
  RhinoApp.EscapeKeyPressed -= OnEscape;
  RhinoApp.Wait();
  var directions = new[] { DefinedViewportProjection.Front, DefinedViewportProjection.Top,
   DefinedViewportProjection.Left, DefinedViewportProjection.Right,
   DefinedViewportProjection.Bottom, DefinedViewportProjection.Back };
  var x = vp.CameraX; var y = vp.CameraY;
  foreach(var obj in doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.AnyObject)) {
   bool visible = obj.Visible && !obj.IsHidden && !obj.IsDeleted;
   doc.Objects.Hide(obj.Id, true); doc.Objects.Show(obj.Id, true);
  }
  var plane = view.ActiveViewport.GetConstructionPlane();
  plane.ShowZAxis = false;
  view.ActiveViewport.SetConstructionPlane(plane);
  var capture = new ViewCapture();
  capture.TransparentBackground = true;
  capture.ScaleScreenItems = false;
  capture.DrawAxes = false;
  capture.DrawGrid = false;
  capture.DrawGridAxes = false;
  capture.Width = view.ActiveViewport.Size.Width;
  capture.Height = view.ActiveViewport.Size.Height;
  using (var bitmap = capture.CaptureToBitmap(view)) { }
  foreach (var obj in doc.Objects.GetSelectedObjects(false, false))
  {
   int selected = obj.IsSelected(false);
   var parts = obj.GetSelectedSubObjects();
   if(parts != null) foreach(var part in parts) obj.SelectSubObject(part, false, true);
   obj.Select(selected != 0);
  }
 }
}
