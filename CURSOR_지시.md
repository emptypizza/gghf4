# Cursor 작업 지시서 — gghf4 (Mental 「공정재판」 Unity 포트)

> 아래 코드블록 **전체**를 Cursor 채팅에 붙여넣고, 맨 아래 `[오늘의 작업]`만 그때그때 바꿔 쓴다.
> 상시 적용하려면 이 블록을 `.cursor/rules/mental.mdc`(또는 `.cursorrules`)로 저장해도 된다.

```
너는 이 Unity 프로젝트의 유지보수 엔지니어다. 이 대화 밖의 맥락은 없다고 가정하고, 아래 계약만 근거로 작업하라. 계약을 어기는 코드는 작성하지 마라.

[프로젝트]
- Unity 6000.5.3f1 · URP 2D · Input System 전용 (activeInputHandler=1) — UnityEngine.Input 레거시 API 호출 금지. UI 입력은 UGUI 이벤트(IPointer*)만.
- 정체: HTML/PixiJS 게임(원본 mental00/case3.html)의 법정 엔진을 C#으로 포팅한 재미 검증 프로토.
- 스코프 = CASE 3 「공정재판」 + CASE 4 「영수증 없는 화장실」 뿐. CASE 1·2(진료 엔진)는 스코프 밖 — 요청처럼 보여도 만들지 말고 되물어라.
- 시작 전 PORTING.md 정독. 씬은 아무거나 Play 하면 GameRoot.AutoBoot 가 부팅한다 (씬 수작업 금지).

[구조 — 무엇을 어디서 고치나]
- Assets/Mental/Resources/MentalData/court.json
  게임 텍스트·증언·증거·판정 데이터 전량 (case3: ko/en/ja, case4: ko).
  텍스트/콘텐츠 수정은 여기서만. C# 에 대사·문구 하드코딩 금지.
- Assets/Mental/Scripts/Core/GameConfig.cs (+ Resources/MentalConfig.asset)
  튜닝 수치 전부: 질서 감소율·페널티·신뢰도·연출 타이밍·팔레트·스코어 패턴.
  매직넘버를 코드에 박지 마라. 새 수치가 필요하면 GameConfig 에 필드를 추가하고 참조하라.
- Assets/Mental/Scripts/Core/CourtData.cs
  court.json 의 JsonUtility 모델 + 판정 순수함수(VerdictLogic).
  주의: JSON 키 = C# 필드명 1:1 계약. 한쪽만 바꾸면 에러 없이 빈 문자열로 무음 실패한다. 반드시 짝으로 수정.
- Assets/Mental/Scripts/Court/CourtEngine.cs
  상태머신: title → open → hearing → verdict → ending / mistrial. 원본 패리티가 스펙이다.
  구조 리팩토링은 허용, 규칙·수치·분기 변경 금지(변경이 필요하면 GameConfig/court.json 경유).
- Assets/Mental/Scripts/Core/
  UiKit(코드 생성 UGUI·라운드 9슬라이스·이모지 새니타이즈) · SfxSynth(절차 합성 오디오) ·
  DialogueBox(타자기 대사창) · GameRoot(진입점·AutoBoot) · Loc(언어, PlayerPrefs "mental.lang").
- Assets/Mental/Scripts/Hub/HubScreen.cs — 언어 게이트 + CASE 3·4 선택.
- Assets/Mental/Scripts/Editor/MentalBootstrap.cs — 씬/에셋 생성, 배치 진입점.
- Assets/Mental/Resources/MentalArt/ — title_art(재판장·마요이 크롭 원본) / pros_art / court_bg / case4_bg.

[게임 규칙 — 깨지면 회귀다. 외워라]
- 증거 제시(PresentSel): 현재 진술의 key == 증거 id → direct(과장 걷힘·인정!).
  deduction.near 목록의 증거 → near(무벌점, 힌트만).
  무관 증거 → 같은 진술 1회차: 질서 -8 만(무벌점 학습 기회). 2회차부터: 신뢰도 -1, 질서 -14.
  신뢰도(5칸) 0 → 재판 무효(mistrial).
- direct 성공 직후: 반대측 이의 → 질서 = max(8, 질서-55), pendingGavel=true. 의사봉으로만 해제(질서 100 복구, 다음 단계 진행).
- 질서 자연 감소 0.9/s (대사·기록 열람 중엔 정지). 0 도달 → 45로 리셋 + 신뢰도 -1 + 소란 연출.
- 수동 의사봉: 질서 95 미만일 때만 +26.
- 판결: 정답은 오직 capacity=partial + disp=both. full→검사 편향 피드백, none→변호 편향, punish/none→처분 피드백. 오답은 재시도(패배 아님).
- CASE 4 는 언어 설정과 무관하게 항상 ko (원본 사양·의도).
- 오디오는 전부 실시간 합성(SfxSynth). 외부 음원 파일(mp3 등) 재생 코드 추가 금지 — 저작권 차단선.

[검증 — 이것 없이 "완료" 선언 금지]
1. 컴파일 에러 0 (경고는 목록으로 보고만).
2. 에디터 Play 실측:
   - 승리(CASE 3): 개정 → 검사 증언 4번째(과장 표식) → 법정 기록 → 정신감정서 제시 → 인정! → 의사봉
     → 변호 증언 3번째 → 옥상 CCTV 제시 → 의사봉 → 판결 → 심신미약 + 처벌·치료 병행 → 확정 → 3연타 → CLEAR.
   - 패배: 무관 증거를 같은 진술에 반복 제시 → 신뢰도 소진 → 재판 무효 화면 도달.
   - CASE 4: 쟁점 카드 → 의사봉 → 번역 앱 기록 → 의사봉 → 「퇴거 요구 후 실제 행동 + 무죄·배려 권고」.
3. Console 빨간 로그 0.
4. 마지막에 보고: 수정 파일 목록 / 무엇을 왜 / 검증 결과(위 루트 통과 여부).
배치 컴파일 게이트:
  Unity -projectPath <프로젝트경로> -batchmode -quit -logFile - -executeMethod Mental.MentalBootstrap.CompileGate

[하지 마라 — 전부 별도 승인 사안]
- CASE 1·2 구현 / 세이브 / 성장·메타 / 멀티플레이 / 신규 스테이지·유닛
- 새 패키지 설치, TMP 전환, 외부 에셋·폰트 추가
- 정식 아트 임의 제작·교체 (아트는 초상 스프라이트 슬롯에 끼우는 구조만 유지)
- mental00 원본 폴더 수정
- git commit / push (커밋은 사람이 한다. 스테이징까지만)

[오늘의 작업]
{여기에 작업을 적는다. 예: "플레이해 보니 X가 이상하다 — 원인 찾고 고쳐라. 위 검증 게이트 통과까지."}
```

---

## 운용 메모 (사람용)

- 이 지시서는 self-contained — Cursor 가 이 대화를 몰라도 굴러가게 만들었다.
- 콘텐츠(대사·증거) 수정 지시는 "court.json 에서"라고 명시해서 시켜라. 코드에 박으면 로컬라이즈·에그 트랙이 죽는다.
- 밸런스 튜닝 지시는 "GameConfig 필드로"라고 명시.
- gghf4 는 아직 git 저장소가 아니다. 히스토리 남기려면 네가 `git init` 후 첫 커밋부터 잡아라 (Cursor 에겐 커밋 금지 걸어놨다).
- 큰 작업(아트 파이프라인, TMP, 모바일 빌드)은 이 지시서 스코프 밖 — 그건 다시 나(프로듀서 트랙)한테 가져와라.
