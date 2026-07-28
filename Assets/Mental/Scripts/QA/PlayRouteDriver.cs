// ============================================================
// Mental — QA 자동 주행 드라이버 (에디터 전용, 빌드 미포함)
// PORTING.md 의 「플레이 검증 체크리스트」 3개 루트를 실제 Play 모드에서
// UGUI 클릭(ExecuteEvents)으로 실주행하고 결과를 JSON 으로 남긴다.
//   win   : CASE 3 승리 루트 (검사 4번째→정신감정서→의사봉→변호 3번째→CCTV→판결→CLEAR)
//   lose  : 무관 증거 반복 제시 → 신뢰도 소진 → 재판 무효
//   case4 : CASE 4 (언어 en 상태에서도 항상 ko) 승리 루트
// 게임 코드는 수정하지 않는다 — 상태 확인은 리플렉션, 입력은 실제 버튼 클릭.
// 진입은 Editor/PlayRouteValidator.Run (batch) 이 SessionState 로 지정한다.
// ============================================================
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Mental
{
    public class PlayRouteDriver : MonoBehaviour
    {
        const float StepTimeout = 40f;
        const float RouteTimeout = 300f;
        const float TapInterval = 0.09f;

        string _route = "";
        string _outPath = "";
        readonly List<string> _steps = new List<string>();
        readonly List<string> _errors = new List<string>();
        readonly List<string> _warnings = new List<string>();
        bool _finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string route = UnityEditor.SessionState.GetString("mental.qa.route", "");
            if (string.IsNullOrEmpty(route)) return;
            GameObject go = new GameObject("MentalQaDriver");
            DontDestroyOnLoad(go);
            PlayRouteDriver d = go.AddComponent<PlayRouteDriver>();
            d._route = route;
            d._outPath = UnityEditor.SessionState.GetString("mental.qa.out", "");
        }

        void Awake()
        {
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        void OnLog(string cond, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _errors.Add(type + ": " + cond);
            else if (type == LogType.Warning)
                _warnings.Add(cond);
        }

        void Start()
        {
            StartCoroutine(CoWatchdog());
            StartCoroutine(CoRun());
        }

        IEnumerator CoWatchdog()
        {
            yield return new WaitForSeconds(RouteTimeout);
            if (!_finished) Finish(false, "TIMEOUT after " + RouteTimeout + "s");
        }

        IEnumerator CoRun()
        {
            IEnumerator body =
                _route == "lose" ? RouteLose() :
                _route == "case4" ? RouteCase4() : RouteWin();
            while (true)
            {
                object cur;
                try { if (!body.MoveNext()) break; cur = body.Current; }
                catch (Exception ex) { Finish(false, "EXCEPTION: " + ex); yield break; }
                yield return cur;
            }
        }

        // ---------------- 리플렉션 헬퍼 ----------------
        static object F(object o, string name)
            => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)?.GetValue(o);

        CourtEngine Engine => FindFirstObjectByType<CourtEngine>();
        string SceneName() { CourtEngine e = Engine; return e == null ? "" : F(e, "_scene").ToString(); }
        object HState() { CourtEngine e = Engine; return e == null ? null : F(e, "H"); }
        T Hv<T>(string field) { object h = HState(); return h == null ? default : (T)F(h, field); }
        DialogueBox Dlg() { CourtEngine e = Engine; return e == null ? null : (DialogueBox)F(e, "_dlg"); }
        bool DlgActive() { DialogueBox d = Dlg(); return d != null && d.Active; }
        CourtLocale Locale() { CourtEngine e = Engine; return e == null ? null : (CourtLocale)F(e, "_T"); }
        UiKit.UiButton EngineBtn(string field) { CourtEngine e = Engine; return e == null ? null : (UiKit.UiButton)F(e, field); }
        List<UiKit.UiButton> EngineBtnList(string field) { CourtEngine e = Engine; return e == null ? null : (List<UiKit.UiButton>)F(e, field); }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        static Transform FindDeepAnywhere(string name)
        {
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                Transform t = FindDeep(c.transform, name);
                if (t != null) return t;
            }
            return null;
        }

        // ---------------- 입력 (실제 UGUI 클릭 경로) ----------------
        static void Click(GameObject go)
        {
            PointerEventData ev = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(go, ev, ExecuteEvents.pointerClickHandler);
        }

        static bool Clickable(UiKit.UiButton b)
            => b != null && b.rt != null && b.rt.gameObject.activeInHierarchy && b.button.interactable;

        IEnumerator WaitCond(Func<bool> cond, string desc, float timeout = StepTimeout)
        {
            float t = 0;
            while (!cond())
            {
                t += Time.deltaTime;
                if (t > timeout) throw new Exception("WaitCond timeout: " + desc);
                yield return null;
            }
            Step("ok: " + desc);
        }

        IEnumerator ClickWhenReady(Func<UiKit.UiButton> get, string desc)
        {
            yield return WaitCond(() => Clickable(get()), "clickable " + desc);
            Click(get().rt.gameObject);
            Step("click: " + desc);
        }

        // 대사창을 계속 탭해서 cond 가 될 때까지 진행 (기록 오버레이 열려 있으면 탭 중지)
        IEnumerator TapUntil(Func<bool> cond, string desc, float timeout = 90f)
        {
            float t = 0;
            while (!cond())
            {
                t += TapInterval;
                if (t > timeout) throw new Exception("TapUntil timeout: " + desc);
                if (!Hv<bool>("recordOpen"))
                {
                    CourtEngine e = Engine;
                    if (e != null)
                    {
                        Transform catcher = FindDeep(e.transform, "TapCatcher");
                        if (catcher != null) Click(catcher.gameObject);
                    }
                }
                yield return new WaitForSeconds(TapInterval);
            }
            Step("ok: " + desc);
        }

        void Step(string s) { _steps.Add(s); Debug.Log("[MentalQA] " + s); }

        void Assert(bool cond, string what)
        {
            if (!cond) throw new Exception("ASSERT FAILED: " + what);
            Step("assert: " + what);
        }

        // ---------------- 공통 구간 ----------------
        IEnumerator EnterCase(string caseMode, bool expectLangGate)
        {
            yield return WaitCond(() => FindDeepAnywhere("Hub") != null || FindDeepAnywhere("lang_ko") != null, "hub visible");
            if (expectLangGate)
            {
                Transform gate = FindDeepAnywhere("lang_ko");
                Assert(gate != null, "언어 게이트 표시됨");
                Click(gate.gameObject);
                Step("click: 언어 게이트 한국어");
            }
            yield return WaitCond(() => FindDeepAnywhere("play" + caseMode) != null, "case card " + caseMode);
            Click(FindDeepAnywhere("play" + caseMode).gameObject);
            Step("click: CASE " + caseMode + " 시작");

            yield return WaitCond(() => Engine != null && SceneName() == "Title", "CourtEngine Title");
            // 타이틀 시작 버튼 (타이틀 씬의 유일한 씬 버튼)
            yield return ClickWhenReady(() =>
            {
                List<UiKit.UiButton> bs = EngineBtnList("_buttons");
                return bs != null && bs.Count > 0 ? bs[0] : null;
            }, "타이틀 시작");
            yield return TapUntil(() => SceneName() == "Hearing", "개정 스크립트 → 심리 진입");
        }

        IEnumerator NextTimes(int n)
        {
            for (int i = 0; i < n; i++)
            {
                int want = Hv<int>("idx") + 1;
                yield return ClickWhenReady(() => EngineBtn("_btnNext"), "다음 진술");
                yield return WaitCond(() => Hv<int>("idx") == want, "idx → " + want);
            }
        }

        IEnumerator PresentEvidence(string evidenceId)
        {
            yield return ClickWhenReady(() => EngineBtn("_btnRecord"), "법정 기록 열기");
            yield return WaitCond(() => Hv<bool>("recordOpen"), "기록 오버레이 열림");
            Transform cell = FindDeep(Engine.transform, "cell_" + evidenceId);
            Assert(cell != null, "증거 셀 존재: " + evidenceId);
            Click(cell.gameObject);
            yield return null;
            Transform present = FindDeep(Engine.transform, "BtnPresent");
            Assert(present != null, "제시 버튼 존재");
            Click(present.gameObject);
            Step("present: " + evidenceId);
        }

        IEnumerator DirectHitThenGavel(string evidenceId, string trimmedField)
        {
            yield return PresentEvidence(evidenceId);
            yield return TapUntil(() => Hv<bool>("pendingGavel") && !DlgActive(), "인정!→이의→의사봉 대기");
            Assert(Hv<bool>(trimmedField), "과장 걷힘: " + trimmedField);
            yield return ClickWhenReady(() => EngineBtn("_btnGavel"), "의사봉");
        }

        // ---------------- 루트: CASE 3 승리 ----------------
        IEnumerator RouteWin()
        {
            yield return EnterCase("3", expectLangGate: true);

            CourtLocale T = Locale();
            Assert(T.caseMode == "3" && T.lang == "ko", "CASE3 ko 로케일");

            // 검사 증언 4번째(과장 표식)
            yield return NextTimes(3);
            Assert(Hv<string>("side") == "pros" && Hv<int>("idx") == 3, "검사 4번째 진술");
            Assert(!string.IsNullOrEmpty(T.pros[3].over), "4번째 진술에 과장 표식");

            // 정신감정서 → 인정! → 의사봉 → 변호 측 전환
            yield return DirectHitThenGavel("eval", "prosTrimmed");
            yield return TapUntil(() => Hv<string>("side") == "def" && !DlgActive(), "변호 측 전환");

            // 변호 증언 3번째 → 옥상 CCTV → 의사봉 → 판결 진입
            yield return NextTimes(2);
            Assert(Hv<int>("idx") == 2, "변호 3번째 진술");
            yield return DirectHitThenGavel("cctv", "defTrimmed");
            yield return TapUntil(() => SceneName() == "Verdict", "판결 진입");

            // 저울: 양쪽 과장 걷힘 → 중립 배지
            yield return TapUntil(() => !DlgActive(), "판결 안내 대사 종료");
            Transform neutral = FindDeep(Engine.transform, "neutral");
            yield return WaitCond(() => neutral.gameObject.activeSelf, "◆중립◆ 배지 표시");

            // 심신미약(partial) + 처벌·치료 병행(both) → 확정
            yield return ClickWhenReady(() => EngineBtnList("_verdCapBtns")[1], "심신미약(부분책임)");
            yield return ClickWhenReady(() => EngineBtnList("_verdDispBtns")[1], "처벌+치료 병행");
            yield return ClickWhenReady(() =>
            {
                foreach (UiKit.UiButton b in EngineBtnList("_buttons"))
                    if (b.label != null && b.label.text == UiKit.Sanitize(T.verdict.confirm)) return b;
                return null;
            }, "판결 확정");

            yield return WaitCond(() => SceneName() == "Ending", "엔딩 진입(3연타 시작)");
            yield return TapUntil(() => FindDeep(Engine.transform, "ClearLine") != null, "엔딩 시퀀스 → CLEAR", 120f);
            Assert(FindDeep(Engine.transform, "ClearLine").GetComponent<UnityEngine.UI.Text>().text
                       == UiKit.Sanitize(T.ending.clear), "CLEAR 문구 일치");
            Finish(true, "CASE3 승리 루트 완주");
        }

        // ---------------- 루트: 패배 (신뢰도 소진 → 재판 무효) ----------------
        IEnumerator RouteLose()
        {
            yield return EnterCase("3", expectLangGate: false);

            // 과장 표식 진술(4번째)에 무관 증거(gavel)를 반복 제시
            yield return NextTimes(3);
            int startTrust = Hv<int>("trust");
            Assert(startTrust == GameConfig.Instance.trustStart, "신뢰도 시작치 " + GameConfig.Instance.trustStart);

            for (int i = 1; i <= startTrust + 1 && SceneName() == "Hearing"; i++)
            {
                yield return PresentEvidence("gavel");
                if (i == 1)
                {
                    // 1회차: 무벌점 학습 기회 (질서만 감소, 대사 없음)
                    yield return new WaitForSeconds(0.4f);
                    Assert(Hv<int>("trust") == startTrust, "1회차 무벌점");
                }
                else if (i <= startTrust)
                {
                    int want = startTrust - (i - 1);
                    yield return TapUntil(() => !DlgActive() && !Hv<bool>("recordOpen"), "오제시 피드백 " + i);
                    Assert(Hv<int>("trust") == want, "신뢰도 " + want);
                }
                else
                {
                    yield return TapUntil(() => SceneName() == "Gameover", "신뢰도 소진 → 재판 무효", 60f);
                }
            }

            Assert(SceneName() == "Gameover", "재판 무효 화면 도달");
            CourtLocale T = Locale();
            Transform head = FindDeep(Engine.transform, "Head");
            Assert(head != null && head.GetComponent<UnityEngine.UI.Text>().text == UiKit.Sanitize(T.gameover.head),
                "재판 무효 헤드라인 표시");
            Finish(true, "패배(재판 무효) 루트 완주");
        }

        // ---------------- 루트: CASE 4 (언어 en 이어도 항상 ko) ----------------
        IEnumerator RouteCase4()
        {
            yield return EnterCase("4", expectLangGate: false);

            CourtLocale T = Locale();
            Assert(T.caseMode == "4" && T.lang == "ko", "CASE4 는 언어 설정(en)과 무관하게 ko");
            Assert((string)F(Engine, "_activeLang") == "ko", "_activeLang == ko");

            // 검사 4번째 → 판단 기준 카드(law) → 의사봉
            yield return NextTimes(3);
            yield return DirectHitThenGavel("law", "prosTrimmed");
            yield return TapUntil(() => Hv<string>("side") == "def" && !DlgActive(), "변호 측 전환");

            // 변호 3번째 → 번역 앱 기록(translation) → 의사봉
            yield return NextTimes(2);
            yield return DirectHitThenGavel("translation", "defTrimmed");
            yield return TapUntil(() => SceneName() == "Verdict", "판결 진입");
            yield return TapUntil(() => !DlgActive(), "판결 안내 대사 종료");

            // 「퇴거 요구 후 실제 행동」 + 「무죄·배려 권고」
            Assert(T.verdict.capPartial.Contains("퇴거 요구"), "capPartial = 퇴거 요구 후 실제 행동");
            Assert(T.verdict.dispBoth.Contains("무죄"), "dispBoth = 무죄+배려 권고");
            yield return ClickWhenReady(() => EngineBtnList("_verdCapBtns")[1], "퇴거 요구 후 실제 행동");
            yield return ClickWhenReady(() => EngineBtnList("_verdDispBtns")[1], "무죄+배려 권고");
            yield return ClickWhenReady(() =>
            {
                foreach (UiKit.UiButton b in EngineBtnList("_buttons"))
                    if (b.label != null && b.label.text == UiKit.Sanitize(T.verdict.confirm)) return b;
                return null;
            }, "판결 확정");

            yield return WaitCond(() => SceneName() == "Ending", "엔딩 진입");
            yield return TapUntil(() => FindDeep(Engine.transform, "ClearLine") != null, "엔딩 시퀀스 → CLEAR", 120f);
            Finish(true, "CASE4 루트 완주");
        }

        // ---------------- 종료 ----------------
        void Finish(bool pass, string summary)
        {
            if (_finished) return;
            _finished = true;
            bool cleanConsole = _errors.Count == 0;
            bool ok = pass && cleanConsole;

            StringBuilder sb = new StringBuilder();
            sb.Append("{\n  \"route\": \"").Append(_route).Append("\",\n");
            sb.Append("  \"pass\": ").Append(ok ? "true" : "false").Append(",\n");
            sb.Append("  \"summary\": ").Append(Json(summary)).Append(",\n");
            sb.Append("  \"consoleErrors\": [");
            for (int i = 0; i < _errors.Count; i++) sb.Append(i > 0 ? "," : "").Append("\n    ").Append(Json(_errors[i]));
            sb.Append(_errors.Count > 0 ? "\n  " : "").Append("],\n");
            sb.Append("  \"warningCount\": ").Append(_warnings.Count).Append(",\n");
            sb.Append("  \"warnings\": [");
            int wn = Mathf.Min(_warnings.Count, 12);
            for (int i = 0; i < wn; i++) sb.Append(i > 0 ? "," : "").Append("\n    ").Append(Json(_warnings[i]));
            sb.Append(wn > 0 ? "\n  " : "").Append("],\n");
            sb.Append("  \"steps\": [");
            for (int i = 0; i < _steps.Count; i++) sb.Append(i > 0 ? "," : "").Append("\n    ").Append(Json(_steps[i]));
            sb.Append(_steps.Count > 0 ? "\n  " : "").Append("]\n}\n");

            Debug.Log("[MentalQA] FINISH route=" + _route + " pass=" + ok + " — " + summary);
            if (!string.IsNullOrEmpty(_outPath))
            {
                try { System.IO.File.WriteAllText(_outPath, sb.ToString()); }
                catch (Exception ex) { Debug.Log("[MentalQA] result write failed: " + ex.Message); }
            }
            UnityEditor.SessionState.EraseString("mental.qa.route");
            bool exitOnFinish = UnityEditor.SessionState.GetString("mental.qa.exitOnFinish", "") == "1";
            if (exitOnFinish) UnityEditor.EditorApplication.Exit(ok ? 0 : 2);
            else UnityEditor.EditorApplication.isPlaying = false;
        }

        static string Json(string s)
        {
            StringBuilder b = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') b.Append('\\').Append(c);
                else if (c == '\n') b.Append("\\n");
                else if (c == '\r') { }
                else if (c < ' ') b.Append(' ');
                else b.Append(c);
            }
            return b.Append('"').ToString();
        }
    }
}
#endif
