# API 및 기존 소스 조사

원본: https://github.com/bkidoffice/BKID_Workflow_AutoCrop/tree/5901b365b784a3b197ce14b5e1d45450e04dfe6e

분석: `BKID_AutoCrop_SixView/BKID_AutoCrop_SixView.py`, `plugin/BKID.AutoCropSixView/SixViewPlugin.cs`, `CaptureRunner.cs`, `CaptureScaleDialog.cs`, `ToolbarBindings.cs`, `plugin/BKID.AutoCropSixView.Setup/`, `tools/build_rhi.py`.

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

3.1.3: AutoCrop과 플러그인/명령 GUID, 어셈블리명, RUI 네임스페이스, 툴바 이름, 명령어, AppData 경로, 설정과 설치 mutex를 분리했습니다. 원래 AutoCrop 파일이 존재하는 경로에서 실제 설치 후보 열거기를 실행해 SixView만 선택하고 AutoCrop RHP/RUI를 보존하는 검사를 추가했습니다. 삼면도 아이콘은 이번 요청에 맞춰 새로 생성했습니다.

3.1.3: 기존 AutoCrop WizardImage.png를 원본 바이트 그대로 복원하고 마법사 배경색을 원래 값으로 복구. 아이콘은 흰 채움·검정 테두리로 수정. 독립 플러그인 GUID와 명령어는 유지하고 표시명/배포명은 BKID_AutoCrop_SixView로 변경. 자체 RUI 컬렉션에 한해 새 이름·아이콘 갱신하며 외부 컬렉션의 비트맵/레이아웃은 보존합니다.

3.1.3: 원본 AutoCrop SVG 첫 번째 그룹(파란 프레임)의 path와 fill="#0645F5"를 그대로 복원했습니다. 생성 이미지의 파란색 근삿값을 사용하지 않습니다. SVG와 배포 PNG의 지배적인 불투명 파란 픽셀 RGB(6,69,245)를 검증합니다.

3.1.3: 실제 툴바 버튼의 이전 이미지 캐시를 피하도록 이미지 GUID를 새로 발급하고 자체 저장 RUI의 좌/우 매크로와 툴바 bitmap_id를 갱신합니다. 흰색 채움·검정 테두리 PNG와 원본 파란색은 유지합니다.
