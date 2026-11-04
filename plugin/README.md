# BKID_AutoCrop_SixView 6 Views 3.1.4 — 개발

저장소 루트의 README.md와 RESEARCH.md를 참고하세요.

.NET SDK 8에서 `python3 tools/build_rhi.py`를 실행하면 Rhino 6 초기 SDK(net45) 기반 RHP, RHI, Windows 설치 EXE를 생성하고 내장 스크립트·패키지·툴바·파일 관리 로직을 검증합니다. `--dotnet /path/to/dotnet`으로 SDK 경로 지정이 가능합니다.

기존 AutoCrop과 독립된 GUID·어셈블리·툴바·명령어를 사용하여 나란히 설치됩니다. 기존 배율 창·폴더 설정·최초 툴바 표시·도킹 보존·재설치 흐름을 유지합니다. 3.1.4에서 표준 6뷰 자동 프레이밍·배치 저장과 상태 복원을 추가했습니다.

실제 Windows Rhino 6/8의 IronPython 실행·렌더링·설치 UI 검증은 별도로 필요합니다. EXE는 Authenticode 서명되지 않았습니다.
