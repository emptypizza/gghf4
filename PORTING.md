# Mental → Unity 포팅 (CASE 3·4 「공정재판」)

원본 `mental00/case3.html`(HTML/PixiJS·Canvas2D)의 **법정 엔진을 Unity 6 (URP 2D)로 포팅**.
스코프 = CASE 3 「공정재판」 + CASE 4 「영수증 없는 화장실」 **만** (지시대로 CASE 1·2 진료 엔진 제외).

## 전략

**로직·데이터 100% / 프레젠테이션은 UGUI 재구성.** 원본 비주얼이 전부 절차적 드로잉이라 1:1 그림 포팅은 연출 선투입 — 하지 않았다. 게임플레이(증언 심리 → 증거 제시 → 의사봉 → 저울 판결 → 엔딩/무효), 케이스 텍스트 전량(ko/en/ja), 판정 수식, 절차 사운드는 원본과 동일. 도토 아트가 들어올 자리는 비워둠.

## 구조

```
Assets/Mental/
├─ Resources/
│  ├─ MentalData/court.json     ← 케이스 콘텐츠 전량 (에그 수정 구역 — 코드 몰라도 됨)
│  ├─ MentalArt/                ← 원본 이관: title_art / pros_art / court_bg / case4_bg
│  ├─ MentalFonts/              ← WebGL/모바일용 임베드 폰트 (Noto Sans KR Subset, OFL)
│  └─ MentalConfig.asset        ← 튜닝 SO (메뉴로 생성 — 없어도 코드 기본값으로 동작)
├─ Scripts/
│  ├─ Core/   CourtData(모델·판정 순수함수) · GameConfig(SO) · Loc · UiKit · SfxSynth · DialogueBox · GameRoot
│  ├─ Court/  CourtEngine (상태머신: title→open→hearing→verdict→ending / mistrial)
│  ├─ Hub/    HubScreen (언어 게이트 + CASE 3·4 선택)
│  ├─ QA/     PlayRouteDriver (Play 모드 자동 주행, 에디터 전용)
│  └─ Editor/ MentalBootstrap · AndroidBuild · WebGLBuild · PlayRouteValidator
└─ Scenes/Main.unity            ← 부트스트랩이 생성
```

철칙 준수: 매직넘버는 전부 `MentalConfig`(질서 감소율·신뢰도·페널티·연출 타이밍·팔레트·스코어 패턴). 텍스트·증거·판정 데이터는 전부 `court.json`.

## 실행 (에디터)

1. 유니티로 `gghf4` 열기 (6000.5.5f1)
2. 메뉴 **Mental → Setup Main Scene (씬 생성)** — 씬·설정 에셋 자동 구성
3. Play → 언어 선택 → 허브 → CASE 3 / CASE 4

## 실행 (unitycli / 커맨드라인 — 표준 유니티 배치 기준)

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity
PROJ=~/gghf4

# 1) 컴파일 게이트 — 에러 있으면 exit != 0
"$UNITY" -projectPath "$PROJ" -batchmode -quit -logFile - \
  -executeMethod Mental.MentalBootstrap.CompileGate

# 2) 씬 + 설정 에셋 생성 (1회)
"$UNITY" -projectPath "$PROJ" -batchmode -quit -logFile - \
  -executeMethod Mental.MentalBootstrap.BatchSetup
```

## 자동 플레이 검증 (QA 드라이버)

체크리스트 3개 루트는 `Scripts/QA/PlayRouteDriver.cs`(에디터 전용, 빌드 미포함)가
실제 Play 모드에서 UGUI 클릭으로 실주행한다. 게임 코드는 건드리지 않는다(상태 확인은 리플렉션).

```bash
# A) 배치 일회성 (루트당 에디터 1회 부팅, 종료코드 0=PASS / 2=FAIL)
MENTAL_ROUTE=win MENTAL_QA_OUT=/tmp/win.json \
"$UNITY" -projectPath "$PROJ" -batchmode -logFile - \
  -executeMethod Mental.PlayRouteValidator.Run     # -quit 붙이지 말 것

# B) unity-cli-bridge 상주 에디터 (에디터 재부팅 없이 반복; 헤디드 필수 — 브리지는 batchmode 미기동)
unity-cli execute --code "Mental.PlayRouteValidator.Prepare(\"win\", \"/tmp/win.json\");" --force
unity-cli play        # 완주하면 드라이버가 Play 를 스스로 멈추고 결과 JSON 을 남긴다
unity-cli read-console --type error
```

루트: `win`(CASE 3 승리 전 구간) / `lose`(무관 증거 반복 → 재판 무효) /
`case4`(언어 en 상태로 시작해 항상-ko 사양까지 확인). 결과 JSON 에 pass 여부·스텝 로그·콘솔 에러가 담긴다.

unity-cli-bridge 는 `Packages/com.yhc509.unity-cli-bridge` 에 **embedded + 로컬 패치**로 들어있다
(패치 사유·업그레이드 절차는 그 안의 `PATCHED-FOR-GGHF4.md`). CLI 는 `~/.unity-cli-bridge/unity-cli/`.
에디터를 kill 로 내리면 토큰 사이드카(`~/Library/Application Support/unity-cli/tokens/*.token`)가
스테일로 남아 UNAUTHORIZED 가 난다 — 해당 `.token` 파일 삭제 후 에디터 재시작으로 복구.

## 플레이 검증 체크리스트 (완료 선언 최소선)

**승리 루트 (CASE 3):** 개정 대사 진행 → 검사 증언 4번째(과장 표식)에서 「법정 기록」→ **정신감정서** 제시 → 인정! → 이의 발생 → **의사봉** → 변호 측 전환 → 변호 증언 3번째에서 **옥상 CCTV** 제시 → 의사봉 → 중립 판결 진입 → **심신미약 + 처벌·치료 병행** → 판결 확정 → 3연타 의사봉 → 엔딩 CLEAR.
**패배 루트:** 무관한 증거를 같은 진술에 2회+ 제시(첫 실수는 무벌점) × 신뢰도 5 소진 → 재판 무효. 또는 방치로 질서 0 반복.
**CASE 4:** 쟁점 카드 → 의사봉 → 번역 앱 기록 → 의사봉 → 「퇴거 요구 후 실제 행동 + 무죄·배려 권고」. 항상 한국어(원본 사양).
**저울:** 시작 양쪽 추 → 과장 걷을 때마다 한쪽씩 제거 → 양쪽 제거 시 ◆중립◆ 배지.

## 원본 대비 의도된 차이 (플레이스홀더)

- 변호사·피고인 초상 = 컬러 블록 + 이니셜 (재판장·마요이·검사는 원본 아트 크롭 사용)
- 컬러 이모지 제거(유니티 동적 폰트 한계) — ⚖ ◀▶ 등 텍스트 기호는 유지
- BGM = 원본 SCORE 테이블 기반 절차 합성(무드 7종). mp3 재생 훅 없음 (저작권 음원 이관 안 함)
- 파티클·잉크 질감 등 장식 간소화. 폰트는 **임베드 Noto Sans KR** 우선(WebGL/모바일), 에디터/데스크톱은 OS 동적 폰트 폴백

## 해상도 대응 (비-16:9 화면)

갤럭시 퀀텀4(2408×1080, 약 20:9) 실기 테스트에서 하단 버튼 잘림·HUD 겹침 발견 → 수정 (2026-07-16):
- CanvasScaler `Expand` — 캔버스가 항상 기준(1280×720) 이상, 세로 잘림 원천 차단
- 콘텐츠 레이어는 `UiKit.DesignBox`(중앙 고정 1280×720), 배경·FX 는 풀블리드 유지 (16:9 에선 캔버스와 일치 = 무변화)
- `UiCoverFitter` — 배경 커버핏을 실제 렉트 기준으로 재계산 (와이드 화면 가로 왜곡 방지)
- 증언 패널을 HUD 레이어로 이동 — 원본 drawHearing 순서(초상 → 증언 → 버튼) 복원, 긴 진술이 초상에 가려지지 않음
- 증언 카운터 y 원본 좌표(베이스라인 108)로 정정 — 질서 게이지 라벨과 겹침 해소
- 검증: 2408×1080 에서 win·lose·case4 3루트 + 1920×1080 win 루트 전부 PASS, 콘솔 에러 0

## 실기 렌더 수정 (퀀텀4, 2026-07-17)

긴장 비네트(질서<55)가 평면 풀스크린 패널로 포팅돼 있어 실기 플레이 속도(질서가 실제로 떨어짐)에서 **화면 전체가 붉게 물듦** → 원본 패리티로 수정:
- `UiKit.RadialVignette()` — 원본 `createRadialGradient`(중심 r240 투명 → r760 가장자리 붉음, 1280×720 px 기준) 등가 스프라이트를 절차 생성. 색은 `Image.color`, 알파 그라데이션은 스프라이트가 담당 (기존 알파 애니메이션 코드 무변경).
- 반경·색은 `GameConfig.vignetteInnerRadius / vignetteOuterRadius / vignetteColor`(#961414 = 원본 rgba(150,20,20)).
- QA 드라이버는 빠르게 진행해 질서가 55 아래로 안 떨어지므로 자동 검증에 안 걸렸음 — 저질서 구간 비주얼은 실기/수동 확인 필요.
- 검증: CompileGate 통과 · win 루트 PASS(61스텝, 콘솔 에러 0) · `Builds/Mental.apk` 재빌드(에러 0).

## Android 빌드 (모바일 버전)

`Scripts/Editor/AndroidBuild.cs` — 패키지 `com.gghf.mental`, 가로 전용, IL2CPP·ARM64.
에디터 메뉴 **Mental → Build Android APK** 또는 배치:

```bash
MENTAL_APK_OUT=$PROJ/Builds/Mental.apk \
JAVA_HOME=/Applications/Unity/Hub/Editor/6000.5.5f1/PlaybackEngines/AndroidPlayer/OpenJDK \
"$UNITY" -projectPath "$PROJ" -batchmode -logFile - \
  -buildTarget Android -executeMethod Mental.AndroidBuild.BatchBuild   # 종료코드 0=성공
```

빌드 트러블슈팅 (2026-07-14 실측):
- **JDK not found / JAVA_HOME invalid** — EditorPrefs `Jdk17Path`가 삭제된 구버전 에디터(6000.5.2f1)를
  가리키고 있었음. `defaults write com.unity3d.UnityEditor5.x Jdk17Path -string ".../6000.5.3f1/.../OpenJDK"` 로 수정.
- **Gradle checkReleaseDuplicateClasses (kotlin-stdlib 충돌)** — 미사용 `com.unity.purchasing` 패키지가
  구버전 kotlin-stdlib-jdk7/8(1.6.21)을 끌어와 발생. manifest.json 에서 제거로 해결.
- SDK/NDK 는 `~/Library/Android/sdk` (NDK 27.2) 사용, JDK 는 에디터 임베디드 OpenJDK.

## WebGL 빌드 (브라우저 버전)

`Scripts/Editor/WebGLBuild.cs` — Gzip + decompression fallback, 기본 1280×720.
에디터 메뉴 **Mental → Build WebGL** 또는 배치:

```bash
MENTAL_WEBGL_OUT=$PROJ/Builds/WebGL \
"$UNITY" -projectPath "$PROJ" -batchmode -logFile - \
  -buildTarget WebGL -executeMethod Mental.WebGLBuild.BatchBuild   # 종료코드 0=성공
```

로컬 확인 (빌드 산출물 정적 서빙 — `file://` 로는 로드 실패):

```bash
cd "$PROJ/Builds/WebGL" && python3 -m http.server 8080
# → http://localhost:8080
```

웹 특이사항:
- **폰트** — OS 동적 폰트 불가. `Resources/MentalFonts/NotoSansKR-Regular.otf`(Noto Sans KR Subset, OFL)를
  `UiKit.GetFont` 가 우선 로드. CJK(ko/ja) 표시에 필수.
- **오디오** — 브라우저 autoplay 정책. 언어 선택·케이스 시작 클릭에서 `SfxSynth.UnlockAudio()`
  (무음 PlayOneShot)로 AudioContext resume.
- **호스팅** — Gzip 사용. 서버가 `.gz` 에 `Content-Encoding: gzip` 을 안 주면
  decompression fallback 이 JS 쪽에서 풀어줌(GitHub Pages·단순 정적 호스트 OK).
  Brotli 전용 호스트가 아니면 Gzip 유지.
- **용량** — URP 런타임 + 임베드 폰트(~4MB) 포함. itch.io / GitHub Pages 배포 가능 수준.

## 다음 줄 후보 (지금 안 함)

도토 아트 슬롯 연결(초상 스프라이트 교체만으로 적용되는 구조) · TMP(선택) · 실기기 터치 확인(APK 설치) · CASE 1·2 진료 엔진(별도 지시 시) · WebGL 빌드 실기 브라우저 스모크(win 루트)
