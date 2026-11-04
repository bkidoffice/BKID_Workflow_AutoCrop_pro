# BKID_AutoCrop_SixView — 3.1.2

Windows Rhino 6 / 8용 플러그인. 기존 방식의 툴바·파란색 프레임·EXE → Rhino Installer 설치 흐름을 유지하고 6개 정투상을 투명 PNG로 출력합니다.

[설치 EXE 다운로드](https://github.com/bkidoffice/BKID_Workflow_AutoCrop_pro/raw/refs/heads/main/installers/BKID_AutoCrop_SixView_Setup_3.1.2.exe)

## 설치

1. Rhino를 모두 종료합니다.
2. `installers/BKID_AutoCrop_SixView_Setup_3.1.2.exe`를 실행합니다.
3. Rhino Installer에서 현재 사용자 설치를 완료합니다. 기존 AutoCrop을 제거할 필요가 없습니다.
4. Rhino를 시작합니다. 툴바가 닫혀 있으면 `BKIDAutoCropSixView`을 실행합니다.

기존 **BKID AutoCrop과 나란히 설치되는 독립 플러그인**입니다. 별도 플러그인 ID·명령어·툴바·설정·실행 경로를 사용하며 AutoCrop의 파일·레지스트리·버튼·별칭을 삭제하거나 변경하지 않습니다. SixView가 이미 설치된 경우에는 SixView만 재설치합니다. 재설치가 취소되거나 실패해도 이전 BKID_AutoCrop_SixView 버전은 자동 복원되지 않습니다. RHI 직접 설치도 가능합니다.

툴바 이름은 **BKID_AutoCrop_SixView**, 아이콘은 기존 파란색 크롭 프레임 안에 흰색 채움·검정 테두리 사각형 3개를 배치한 삼면도입니다. 명령어는 `BKIDAutoCropSixView`, `BKIDAutoCropSixViewCapture`, `BKIDAutoCropSixViewResolution`, `BKIDAutoCropSixViewFolder`입니다. 기본 저장 폴더는 바탕화면 `BKID_AutoCrop_SixView_Captures`입니다.

이전에 배포한 AutoCrop 3.0.0은 교체 방식이므로 사용하지 마세요. 3.0.0이 이미 원래 AutoCrop을 교체했다면 원본 저장소의 AutoCrop 2.5.3 설치기로 원래 기능을 복구하고, 이 BKID_AutoCrop_SixView 3.1.2을 추가 설치하세요. BKID_AutoCrop_SixView 설치기는 AutoCrop 3.0.0도 자동 삭제하지 않습니다.

설치 마법사는 원래 AutoCrop의 이미지·배치·배경색을 사용하며, 제품명은 BKID_AutoCrop_SixView로 표시합니다. 기존 SixView 3.1.0에서 업데이트하면 저장된 툴바의 이름과 아이콘도 갱신합니다.

## 사용

- 좌클릭: 1배로 6장 저장.
- 우클릭: ×2 / ×3 / ×4 선택 후 6장 저장. 다음 좌클릭은 다시 1배.
- 우클릭 → 저장 폴더 변경: 이후 실행에도 유지. `BKIDAutoCropSixViewFolder` 명령도 사용 가능.
- 선택 객체가 있으면 선택한 표시 객체만 캡처합니다. 없으면 보이는 모델 전체를 캡처합니다. 모델별 출력이 필요하면 해당 모델만 선택하고 실행하세요. 여러 모델 파일을 자동으로 여는 일괄 처리는 포함하지 않습니다.

실행별 `BKID_6VIEW_날짜_고유번호` 폴더에 `FRONT.png`, `TOP.png`, `LEFT.png`, `RIGHT.png`, `BOTTOM.png`, `BACK.png`를 저장합니다.

Rhino 월드 좌표 기준 표준 뷰를 사용합니다. 뷰마다 모델 외곽이 가로 또는 세로 한계를 채우도록 맞춥니다. 앤티앨리어싱을 위해 캡처 전 사방 약 4px를 확보하고 알파 채널로 크롭하여 최종 사방 1px 투명 여백을 남깁니다. 형상의 종횡비는 유지합니다.

1배의 **크롭 전** 해상도는 실행 시 활성 뷰포트 크기입니다. 배율은 가로·세로 픽셀 수에 적용하며 최종 크기는 뷰마다 달라집니다. 큰 PNG가 필요하면 뷰포트를 최대화하거나 우클릭 배율을 사용하세요. 무제한 해상도를 뜻하지 않습니다.

현재 표시 모드를 사용하며 그리드·축·선택 강조는 제외합니다. 숨겨진 객체는 포함하지 않습니다. 그림자·클리핑 평면 효과가 필요 없다면 실행 전에 끄세요. 이미지 가장자리에 픽셀이 닿으면 해당 실행의 파일을 정리하고 오류를 알립니다. 완료·오류 후 카메라·뷰 이름·선택·임시 숨김 상태를 복원합니다. Esc는 뷰 사이에서 취소합니다.

## 개발 및 검증

.NET SDK 8 및 Python 3 필요. Python 테스트 중 이미지 검사는 Pillow가 필요합니다.

```sh
python3 tools/build_rhi.py --dotnet /path/to/dotnet
python3 -m unittest discover -s tests -p 'test_capture_scale.py' -v
python3 -m unittest discover -s tests -p 'test_six_views.py' -v
```

전체 테스트 중 과거 Python 설치기 검사는 `dist/Install_BKID_AutoCrop_SixView.py` 생성이 필요합니다. 권장 배포는 EXE/RHI입니다. 기존 아이콘을 재생성하지 않고 테스트용 Python 설치기를 준비하는 방법:

```sh
python3 -c 'import tools.build_release as b; b.build_rui = lambda: None; b.main()'
python3 -m unittest discover -s tests -v
```

Rhino 6 초기 SDK의 C# 빌드·새 API 검사, RHP/EXE 내장 데이터·RHI 무결성, 설치 파일 관리 로직, 버튼 매크로·배율 전달, 모의 호스트의 6뷰 순서·프레이밍·실패 복원을 검증했습니다. **Windows Rhino 렌더링·IronPython 실제 실행·설치 UI는 미검증**입니다. Rhino 8에서도 RunPythonScript의 IronPython 2.7을 사용합니다. CPython 3 직접 실행은 지원하지 않습니다.

실기기에서는 앞뒤·좌우가 다른 모델, 흰색·검은색·얇은 부품으로 방향·외곽 보존·투명도·6개 파일을 확인하세요. 우클릭 배율 후 좌클릭 1배와 원래 뷰 복원도 확인하세요.

원본: [bkidoffice/BKID_Workflow_AutoCrop](https://github.com/bkidoffice/BKID_Workflow_AutoCrop), 커밋 `5901b365b784a3b197ce14b5e1d45450e04dfe6e`. 사용자 요청에 따라 소스·설치 구성·아트워크를 확장했습니다. 원본에 LICENSE 파일이 없어 제3자 재배포 허가를 별도로 주장하지 않습니다. [API 조사 기록](RESEARCH.md).
