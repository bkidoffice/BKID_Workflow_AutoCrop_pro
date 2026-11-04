# API 및 기존 소스 조사

원본: https://github.com/bkidoffice/BKID_Workflow_AutoCrop/tree/5901b365b784a3b197ce14b5e1d45450e04dfe6e

분석: `BKID_AutoCrop/BKID_AutoCrop.py`, `plugin/BKID.AutoCrop/AutoCropPlugin.cs`, `CaptureRunner.cs`, `CaptureScaleDialog.cs`, `ToolbarBindings.cs`, `plugin/BKID.AutoCrop.Setup/`, `tools/build_rhi.py`.

호출 경로: 좌/우 RUI 매크로 → C# ScriptRunner → 내장 IronPython 추출 → per-call 배율·폴더 → 투명 ViewCapture → 알파 크롭 → 저장 PNG 재개방 검증.

변경: 기존 단일 이미지 저장을 `save_capture`로 분리. 새 `run`은 6뷰 투영·프레이밍·선택 격리·상태 복원·실행 단위 파일 정리 담당.

공식 McNeel 소스에서 실제로 확인한 API:

- https://github.com/mcneel/rhinocommon/blob/master/dotnet/rhino/rhinosdkviewport.cs
  - `SetProjection(projection, viewName, updateConstructionPlane)`의 세 번째 인자는 작업평면 갱신 여부. false 사용.
  - `SetViewProjection(ViewportInfo, updateTargetLocation)`로 투영 복사. 원래 CameraTarget은 별도 저장·복원.
  - `ZoomBoundingBox`로 모델 중심과 깊이를 맞춤.
- https://github.com/mcneel/rhinocommon/blob/master/dotnet/opennurbs/opennurbs_viewport.cs
  - `ViewportInfo(RhinoViewport)`, `GetFrustum(out ...)`, `SetFrustum(left, right, bottom, top, near, far)` 확인.
  - 프러스텀을 캔버스 종횡비와 투영 외곽에 맞추고 캡처 사방 4px 확보.

최소 버전 검사는 잠금된 공식 NuGet `RhinoCommon 6.0.18016.23451`에 `tools/CheckRhino6/CaptureApi.cs`를 컴파일함. 최신 소스 존재만으로 Rhino 6 호환성을 가정하지 않음.

McNeel 소스는 API 조사에 사용했고 새 코드로 복사하지 않았습니다. 기존 사용자 소스에는 LICENSE 파일이 없어 외부 재배포 조건은 별도 확인이 필요합니다. Windows Rhino에서 실제 렌더링·설치 검증은 수행하지 못했습니다.
