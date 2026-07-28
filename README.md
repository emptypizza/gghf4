# 공정재판 (Mental — CASE 3·4)

법정 추리 게임 **「공정재판」**의 Unity 6 포트입니다. HTML/PixiJS(Canvas2D)로 만든 원본
`mental00/case3.html`의 법정 엔진을 **Unity 6 (URP 2D) + UGUI**로 옮긴 재미 검증 프로토타입으로,
스코프는 **CASE 3 「공정재판」 + CASE 4 「영수증 없는 화장실」** 두 에피소드입니다.

증언을 심리하고, 과장된 진술에 증거를 들이대고, 의사봉을 두드려 법정 질서를 지키며,
저울이 중립에 도달하면 판결을 내립니다. 잘못된 증거를 남발하면 신뢰도가 깎여 **재판 무효**가 됩니다.

- 지원 언어: 한국어 · English · 日本語 (CASE 4는 원본 사양대로 항상 한국어)
- 플랫폼: 에디터 / **Android**(IL2CPP·ARM64) / **WebGL**
- 로직·데이터·판정 수식·절차 합성 사운드는 원본과 동일, 비주얼은 UGUI로 재구성 (도트 아트 슬롯은 비워둠)

## 요구 사항

- **Unity 6000.5.5f1** (URP 2D 템플릿 기반)
- Input System 전용 (`activeInputHandler=1`) — 레거시 `UnityEngine.Input` 미사용

## 실행 (에디터)

1. Unity로 프로젝트 열기
2. 메뉴 **Mental → Setup Main Scene (씬 생성)** — 씬·설정 에셋 자동 구성 (최초 1회)
3. Play → 언어 선택 → 허브 → CASE 3 / CASE 4

씬을 수작업으로 만들 필요는 없습니다. 아무 씬에서 Play해도 `GameRoot.AutoBoot`가 부팅합니다.

## 빌드

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity
PROJ=$(pwd)

# Android APK (com.gghf.mental, 가로 전용)
MENTAL_APK_OUT=$PROJ/Builds/Mental.apk \
"$UNITY" -projectPath "$PROJ" -batchmode -logFile - \
  -buildTarget Android -executeMethod Mental.AndroidBuild.BatchBuild

# WebGL (Gzip + decompression fallback, 1280×720)
MENTAL_WEBGL_OUT=$PROJ/Builds/WebGL \
"$UNITY" -projectPath "$PROJ" -batchmode -logFile - \
  -buildTarget WebGL -executeMethod Mental.WebGLBuild.BatchBuild

# WebGL 로컬 확인 (file:// 로는 로드되지 않음)
cd "$PROJ/Builds/WebGL" && python3 -m http.server 8080   # → http://localhost:8080
```

에디터 메뉴 **Mental → Build Android APK / Build WebGL**로도 동일하게 빌드됩니다.
빌드 트러블슈팅(JDK 경로, Gradle 충돌 등)은 [PORTING.md](PORTING.md)의 빌드 절 참고.

## 프로젝트 구조

```
Assets/Mental/
├─ Resources/
│  ├─ MentalData/court.json     ← 케이스 텍스트·증거·판정 데이터 전량 (콘텐츠 수정은 여기서만)
│  ├─ MentalArt/                ← 원본 아트 이관 (타이틀·검사·법정 배경 등)
│  ├─ MentalFonts/              ← WebGL/모바일용 임베드 폰트 (Noto Sans KR Subset, OFL)
│  └─ MentalConfig.asset        ← 튜닝 SO (질서·신뢰도·페널티·연출 타이밍·팔레트)
├─ Scripts/
│  ├─ Core/    CourtData(모델·판정 순수함수) · GameConfig · Loc · UiKit · SfxSynth · DialogueBox · GameRoot
│  ├─ Court/   CourtEngine (상태머신: title → open → hearing → verdict → ending / mistrial)
│  ├─ Hub/     HubScreen (언어 게이트 + 케이스 선택)
│  ├─ QA/      PlayRouteDriver (Play 모드 자동 주행 검증, 빌드 미포함)
│  └─ Editor/  MentalBootstrap · AndroidBuild · WebGLBuild · PlayRouteValidator
└─ Scenes/Main.unity
```

설계 원칙: **매직넘버는 전부 `MentalConfig`, 텍스트·데이터는 전부 `court.json`.**
코드에 대사나 수치를 하드코딩하지 않습니다.

## 자동 플레이 검증 (QA)

승리 · 패배 · CASE 4 세 루트를 실제 Play 모드에서 UGUI 클릭으로 자동 주행합니다.

```bash
# 배치 일회성 (종료코드 0=PASS / 2=FAIL)
MENTAL_ROUTE=win MENTAL_QA_OUT=/tmp/win.json \
"$UNITY" -projectPath "$PROJ" -batchmode -logFile - \
  -executeMethod Mental.PlayRouteValidator.Run     # -quit 붙이지 말 것
```

상주 에디터에서 반복 실행하는 unity-cli 흐름은 [PORTING.md](PORTING.md) 참고.

## 개발 도구

- **unity-cli-bridge** — 에디터 CLI 제어 브리지. `Packages/com.yhc509.unity-cli-bridge`에
  **embedded v0.4.3 + 로컬 패치 2건**으로 포함 (사유·업그레이드 절차:
  [`PATCHED-FOR-GGHF4.md`](Packages/com.yhc509.unity-cli-bridge/PATCHED-FOR-GGHF4.md))
- 포팅 전략·게임 규칙·해상도 대응·실기 검증 이력 등 상세 문서: **[PORTING.md](PORTING.md)**

## 라이선스

- 코드: [MIT](LICENSE) © 2026 [잉여피자어부](https://gghf.itch.io/)
- 폰트: Noto Sans KR Subset — [SIL Open Font License](https://openfontlicense.org/)
- 사운드: 전량 실시간 절차 합성 (외부 음원 미사용)
