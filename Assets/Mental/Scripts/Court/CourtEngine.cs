// ============================================================
// Mental — 공정재판 엔진 (CASE 3·4)
// 원본: mental00/case3.html 의 상태머신·심리·판결 로직 1:1 포팅.
//   title → open → hearing(증언·증거제시·의사봉·질서/신뢰도)
//         → verdict(저울 판결) → ending  /  mistrial → gameover
// 프레젠테이션은 UGUI 코드 생성(플레이스홀더 지향), 로직·데이터는 원본 그대로.
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mental
{
    public class CourtEngine : MonoBehaviour
    {
        public enum Scene { Title, Open, Hearing, Verdict, Ending, Gameover }

        // ---------- 구성 ----------
        GameConfig _cfg;
        CourtDb _db;
        CourtLocale _T;
        CourtCopy _copy;
        string _caseMode = "3";
        string _activeLang = "ko";
        Action _onExit;
        SfxSynth _audio;

        // ---------- 레이어 ----------
        RectTransform _root, _bgLayer, _sceneLayer, _portraitLayer, _hudLayer, _dlgLayer, _overlayLayer, _fxLayer, _persistLayer;
        DialogueBox _dlg;
        Image _flashImg, _vignette;
        RawImage _bg;

        // ---------- 리소스 ----------
        Texture2D _titleArt, _prosArt, _courtBg, _case4Bg;
        Sprite _judgeSprite, _mayoiSprite, _prosSprite;

        // ---------- 상태 (원본 G / H / VERD / scaleUI) ----------
        Scene _scene = Scene.Title;
        float _time;

        class HearingState
        {
            public string side = "pros";
            public int idx;
            public bool prosTrimmed, defTrimmed;
            public float order;
            public int trust;
            public bool recordOpen;
            public int sel;
            public bool pendingGavel;
            public bool locked;
            public float banner;
            public Dictionary<string, int> misses = new Dictionary<string, int>();
        }
        HearingState H = new HearingState();

        string _verdCapacity, _verdDisp;
        float _scalePros = 1, _scaleDef = 1, _scaleAngle, _scaleGlow;
        bool _fanfarePlayed;

        float _hintT; string _hintMsg = "";
        float _shakeAmp, _shakeT, _shakeDur;

        // ---------- 씬별 위젯 참조 ----------
        readonly List<UiKit.UiButton> _buttons = new List<UiKit.UiButton>();
        UiKit.UiButton _btnPrev, _btnNext, _btnRecord, _btnGavel, _btnVerdict;
        readonly List<UiKit.UiButton> _verdCapBtns = new List<UiKit.UiButton>();
        readonly List<UiKit.UiButton> _verdDispBtns = new List<UiKit.UiButton>();
        RectTransform _stmtPanel;
        Text _stmtName, _stmtBody, _stmtOvermark, _stmtCounter, _hintText;
        Image _stmtEdge, _stmtTag, _hintBg;
        RectTransform _recordRoot, _gavelGuide, _bannerRoot;
        Text _bannerText;
        readonly List<Image> _trustCells = new List<Image>();
        Image _orderFill;
        RectTransform _scaleRoot, _scaleBeam;
        Image _panL, _panR, _weightL, _weightR;
        RectTransform _neutralBadge;
        RectTransform _portraitNode;
        Image _portraitImg, _portraitFallback;
        Text _portraitInitial;

        // ============================================================
        //  생성
        // ============================================================
        public static CourtEngine Create(Transform parent, string caseMode, Action onExit)
        {
            RectTransform rt = UiKit.Stretch(UiKit.Node("CourtEngine", parent));
            CourtEngine e = rt.gameObject.AddComponent<CourtEngine>();
            e._caseMode = caseMode;
            e._onExit = onExit;
            e.Build(rt);
            return e;
        }

        void Build(RectTransform rt)
        {
            _cfg = GameConfig.Instance;
            _db = GameRoot.Db;
            _audio = GameRoot.Audio;
            _activeLang = CourtDb.ActiveLang(_caseMode, Loc.Lang);
            _T = _db.GetLocale(_caseMode, Loc.Lang);
            _copy = _db.GetCopy(_caseMode, Loc.Lang);

            _root = UiKit.Stretch(UiKit.Node("Root", rt));

            // 배경
            _bgLayer = UiKit.Stretch(UiKit.Node("BG", _root));
            _titleArt = Resources.Load<Texture2D>("MentalArt/title_art");
            _prosArt = Resources.Load<Texture2D>("MentalArt/pros_art");
            _courtBg = Resources.Load<Texture2D>("MentalArt/court_bg");
            _case4Bg = Resources.Load<Texture2D>("MentalArt/case4_bg");
            if (_titleArt != null)
            {
                _judgeSprite = UiKit.CropSprite(_titleArt, _cfg.judgePhoto);
                _mayoiSprite = UiKit.CropSprite(_titleArt, _cfg.mayoiPhoto);
            }
            if (_prosArt != null)
                _prosSprite = Sprite.Create(_prosArt, new Rect(0, 0, _prosArt.width, _prosArt.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);

            Texture2D bgTex = _caseMode == "4" ? _case4Bg : _courtBg;
            if (bgTex != null) _bg = UiKit.CoverImage(_bgLayer, bgTex, "CourtBg");
            Image dim = UiKit.Panel(_bgLayer, 0, 0, _cfg.refWidth, _cfg.refHeight, new Color(0, 0, 0, .38f), 1, "Dim");
            UiKit.Stretch(dim.rectTransform);

            // 전체 탭 캐처 (버튼보다 뒤)
            UiKit.TapCatcher(_root, OnSceneTap);

            // 콘텐츠 레이어는 중앙 고정 1280×720 (와이드 화면에서 잘림/치우침 방지),
            // 배경(_bgLayer)·FX 는 풀블리드 유지
            _sceneLayer = UiKit.DesignBox("Scene", _root);
            _portraitLayer = UiKit.DesignBox("Portraits", _root);
            _hudLayer = UiKit.DesignBox("HUD", _root);
            _dlgLayer = UiKit.DesignBox("Dialogue", _root);
            _overlayLayer = UiKit.DesignBox("Overlay", _root);
            _fxLayer = UiKit.Stretch(UiKit.Node("FX", _root));
            _persistLayer = UiKit.DesignBox("Persist", _root);

            // 붉은 비네트 (질서 저하) — 원본은 radial gradient: 중심 투명, 가장자리만 붉게.
            // 평면 패널로 두면 화면 전체가 빨개진다 (퀀텀4 실기 리포트, 2026-07-17)
            _vignette = UiKit.Panel(_fxLayer, 0, 0, _cfg.refWidth, _cfg.refHeight, _cfg.C(_cfg.vignetteColor, 0f), 1, "Vignette");
            _vignette.sprite = UiKit.RadialVignette();
            _vignette.type = Image.Type.Simple;
            UiKit.Stretch(_vignette.rectTransform);

            // 플래시
            _flashImg = UiKit.Panel(_fxLayer, 0, 0, _cfg.refWidth, _cfg.refHeight, new Color(0, 0, 0, 0), 1, "Flash");
            UiKit.Stretch(_flashImg.rectTransform);

            // 대사창
            _dlg = DialogueBox.Create(_dlgLayer);

            // 초상 뷰 (단일 — 현재 화자)
            _portraitNode = UiKit.Place(UiKit.Node("Portrait", _portraitLayer), 0, 0, 360, 400);
            _portraitImg = _portraitNode.gameObject.AddComponent<Image>();
            _portraitImg.preserveAspect = true;
            _portraitImg.raycastTarget = false;
            _portraitFallback = UiKit.Panel(_portraitNode, 60, 40, 240, 320, _cfg.C(_cfg.navy), 18, "fallback");
            _portraitInitial = UiKit.Label(_portraitFallback.transform, "", 0, 90, 240, 120, 84, _cfg.C(_cfg.white), TextAnchor.MiddleCenter, FontStyle.Bold, "initial");
            _portraitNode.gameObject.SetActive(false);

            // 상단 고정: ← 메인 / 사운드 토글
            UiKit.MakeButton(_persistLayer, _T.main, 14, 10, 108, 40, "ghost", () => { _audio.StopScore(); _onExit?.Invoke(); }, 17, "BtnMain");
            UiKit.UiButton mute = null;
            mute = UiKit.MakeButton(_persistLayer, "♪", _cfg.refWidth - 58, 10, 44, 40, "ghost", () =>
            {
                bool m = _audio.ToggleMute();
                mute.label.text = m ? "×" : "♪";
            }, 20, "BtnMute");

            EnterTitle();
        }

        // ============================================================
        //  공용 헬퍼
        // ============================================================
        Color TagColor(string who)
        {
            switch (who)
            {
                case "pros": return _cfg.C(_cfg.crimson);
                case "def": return _cfg.C(_cfg.navy);
                case "judge":
                case "mayoi": return _cfg.C(_cfg.purpleHi);
                default: return _cfg.C("#555555");
            }
        }

        string CharName(string who)
        {
            switch (who)
            {
                case "judge": return _T.names.judge;
                case "mayoi": return _T.names.mayoi;
                case "pros": return _T.names.pros;
                case "def": return _T.names.def;
                case "accused": return _T.names.accused;
                default: return who;
            }
        }

        void Say(string who, string text, Action onDone, string expr = "calm")
        {
            _audio.Tap();
            _dlg.Say(who, CharName(who), TagColor(who), text, onDone, expr);
            ShowPortrait(who, expr);
        }

        void ShowPortrait(string who, string expr)
        {
            if (string.IsNullOrEmpty(who)) { _portraitNode.gameObject.SetActive(false); return; }
            float x = _cfg.refWidth / 2f;
            if (who == "pros") x = 955; else if (who == "def") x = 325;
            _portraitNode.anchoredPosition = new Vector2(x - 180, -(460 - 200));

            Sprite sp = who == "judge" ? _judgeSprite : who == "mayoi" ? _mayoiSprite : who == "pros" ? _prosSprite : null;
            bool hasArt = sp != null;
            _portraitImg.enabled = hasArt;
            if (hasArt) _portraitImg.sprite = sp;
            _portraitFallback.gameObject.SetActive(!hasArt);
            if (!hasArt)
            {
                _portraitFallback.color = who == "def" ? _cfg.C(_cfg.navy) : who == "accused" ? _cfg.C("#55504a") : _cfg.C(_cfg.purple);
                string nm = CharName(who);
                _portraitInitial.text = string.IsNullOrEmpty(nm) ? "?" : nm.Substring(0, 1);
            }
            // 표정 뉘앙스 (플레이스홀더): shock 확대 / sad 어둡게 / tense 붉은 기 / smile 밝게
            Color tint = Color.white;
            Vector3 scl = Vector3.one;
            switch (expr)
            {
                case "shock": scl = Vector3.one * 1.05f; break;
                case "sad": tint = new Color(.8f, .8f, .85f); break;
                case "tense": tint = new Color(1f, .9f, .88f); break;
                case "smile": tint = new Color(1.03f, 1.02f, .98f); break;
            }
            _portraitImg.color = tint;
            _portraitNode.localScale = scl;
            _portraitNode.gameObject.SetActive(true);
        }

        void ClearSceneLayer()
        {
            foreach (Transform c in _sceneLayer) Destroy(c.gameObject);
            foreach (Transform c in _hudLayer) Destroy(c.gameObject);
            foreach (Transform c in _overlayLayer) Destroy(c.gameObject);
            _buttons.Clear();
            _verdCapBtns.Clear(); _verdDispBtns.Clear();
            _btnPrev = _btnNext = _btnRecord = _btnGavel = _btnVerdict = null;
            _stmtPanel = null; _recordRoot = null; _gavelGuide = null; _bannerRoot = null;
            _scaleRoot = null; _trustCells.Clear(); _orderFill = null; _hintBg = null;
            _dlg.Hide();
            _portraitNode.gameObject.SetActive(false);
        }

        UiKit.UiButton Btn(Transform parent, string label, float x, float y, float w, float h, string style, Action on, int fs = 22)
        {
            UiKit.UiButton b = UiKit.MakeButton(parent, label, x, y, w, h, style, () => on?.Invoke(), fs);
            _buttons.Add(b);
            return b;
        }

        void Hint(string m)
        {
            _hintMsg = UiKit.Sanitize(m);
            _hintT = _cfg.hintDuration;
            _audio.Tap();
            if (_hintBg == null && _scene == Scene.Hearing) BuildHintBox(_hudLayer);
            if (_hintText != null) _hintText.text = _hintMsg;
        }

        void BuildHintBox(Transform parent)
        {
            float w = _cfg.refWidth - 260;
            _hintBg = UiKit.Panel(parent, 130, 150, w, 84, new Color(20 / 255f, 16 / 255f, 10 / 255f, .92f), 10, "Hint");
            Image edge = UiKit.Panel(_hintBg.transform, 0, 0, w, 84, _cfg.C(_cfg.gold, 0), 10, "hintEdge");
            UiKit.Stretch(edge.rectTransform);
            _hintText = UiKit.Label(_hintBg.transform, "", 20, 10, w - 40, 64, 20, _cfg.C(_cfg.goldHi), TextAnchor.MiddleCenter, FontStyle.Normal, "hintText");
            _hintBg.gameObject.SetActive(false);
        }

        void Burst(string text, Color col, bool big)
        {
            StartCoroutine(CoBurst(UiKit.Sanitize(text), col, big));
            Shake(big ? 16 : 9, .35f);
        }

        IEnumerator CoBurst(string text, Color col, bool big)
        {
            int fs = big ? 84 : 52;
            Text t = UiKit.Label(_fxLayer, text, 0, big ? 240 : 180, _cfg.refWidth, 120, fs, col, TextAnchor.MiddleCenter, FontStyle.Bold, "Burst");
            Outline o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, .8f); o.effectDistance = new Vector2(3, -3);
            float life = _cfg.burstLife, e = 0;
            RectTransform rt = t.rectTransform;
            while (e < life)
            {
                e += Time.deltaTime;
                float k = e / life;
                rt.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.Min(1, k * 4));
                Color c = t.color; c.a = 1f - Mathf.Pow(k, 2.2f); t.color = c;
                Color oc = o.effectColor; oc.a = c.a * .8f; o.effectColor = oc;
                rt.anchoredPosition += new Vector2(0, Time.deltaTime * 14f);
                yield return null;
            }
            Destroy(t.gameObject);
        }

        void Flash(Color col, float dur) { StartCoroutine(CoFlash(col, dur)); }
        IEnumerator CoFlash(Color col, float dur)
        {
            float e = 0;
            while (e < dur)
            {
                e += Time.deltaTime;
                Color c = col; c.a = col.a * (1f - e / dur);
                _flashImg.color = c;
                yield return null;
            }
            _flashImg.color = new Color(0, 0, 0, 0);
        }

        void Shake(float amp, float t)
        {
            _shakeAmp = Mathf.Max(_shakeAmp, amp);
            _shakeT = Mathf.Max(_shakeT, t);
            _shakeDur = _shakeT;
        }

        void SpawnParticles(float x, float y, int n, Color col, float power = 3f)
        {
            for (int i = 0; i < n; i++) StartCoroutine(CoParticle(x, y, col, power));
        }

        IEnumerator CoParticle(float x, float y, Color col, float power)
        {
            float size = UnityEngine.Random.Range(4f, 9f);
            Image p = UiKit.Panel(_fxLayer, x, y, size, size, col, 2, "P");
            RectTransform rt = p.rectTransform;
            Vector2 v = new Vector2(UnityEngine.Random.Range(-1f, 1f) * power * 60f, UnityEngine.Random.Range(.5f, 1.6f) * power * 90f);
            float life = UnityEngine.Random.Range(.7f, 1.25f), e = 0;
            Vector2 pos = rt.anchoredPosition;
            while (e < life)
            {
                e += Time.deltaTime;
                v.y -= 430f * Time.deltaTime;
                pos += v * Time.deltaTime;
                rt.anchoredPosition = pos;
                Color c = p.color; c.a = 1f - e / life; p.color = c;
                yield return null;
            }
            Destroy(p.gameObject);
        }

        // ============================================================
        //  탭 라우팅 (원본 handleTap → sceneTap → dlgTap)
        // ============================================================
        void OnSceneTap()
        {
            if (H.recordOpen) return;            // 오버레이가 자체 처리
            if (_dlg.Tap()) return;              // 대사 진행
        }

        // ============================================================
        //  씬: 타이틀
        // ============================================================
        void EnterTitle()
        {
            _scene = Scene.Title;
            _audio.SetMood("title");
            _audio.StartScore();
            ClearSceneLayer();
            ResetScaleVisual(1, 1);

            float VW = _cfg.refWidth, VH = _cfg.refHeight;
            bool art = _caseMode != "4" && _titleArt != null;

            if (art)
            {
                // 키아트 상단 크롭 (원본 ART_CROP.title: 1448x620 상단)
                RectTransform artRt = UiKit.Place(UiKit.Node("TitleArt", _sceneLayer), 0, 0, VW, 438);
                RawImage ri = artRt.gameObject.AddComponent<RawImage>();
                ri.texture = _titleArt;
                ri.raycastTarget = false;
                float uvH = 620f / _titleArt.height;
                ri.uvRect = new Rect(0, 1f - uvH, 1f, uvH);
                Image grad = UiKit.Panel(_sceneLayer, 0, 360, VW, 120, _cfg.C(_cfg.bg1, .85f), 1, "artFade");
            }

            float ty = art || _caseMode == "4" ? 508 : 340;
            UiKit.Label(_sceneLayer, _T.title.sub, 0, ty - 64, VW, 30, 22, _cfg.C(_cfg.goldHi), TextAnchor.MiddleCenter, FontStyle.Bold, "TitleSub");
            UiKit.Label(_sceneLayer, _T.title.main, 0, ty - 30, VW, 86, 64, _cfg.C(_cfg.cream), TextAnchor.MiddleCenter, FontStyle.Bold, "TitleMain");
            UiKit.Label(_sceneLayer, _T.title.tag, 0, ty + 58, VW, 28, 18, _cfg.C(_cfg.gold), TextAnchor.MiddleCenter, FontStyle.Normal, "TitleTag");
            UiKit.Label(_sceneLayer, _T.title.note, 0, ty + 88, VW, 24, 15, _cfg.C(_cfg.cream, .75f), TextAnchor.MiddleCenter, FontStyle.Normal, "TitleNote");

            Btn(_hudLayer, _T.title.start, VW / 2 - 170, VH - 104, 340, 62, "gold", () => EnterOpen(), 24);
            UiKit.Label(_sceneLayer, _T.hint, 0, VH - 34, VW, 22, 14, _cfg.C(_cfg.cream, .55f), TextAnchor.MiddleCenter, FontStyle.Normal, "TitleHint");
        }

        // ============================================================
        //  씬: 개정(오프닝 스크립트)
        // ============================================================
        void EnterOpen()
        {
            _scene = Scene.Open;
            _audio.SetMood("opening");
            ClearSceneLayer();
            RunScript(_T.open, 0, () => EnterHearing());
        }

        void RunScript(List<CourtLine> lines, int i, Action onEnd)
        {
            if (lines == null || i >= lines.Count) { onEnd?.Invoke(); return; }
            CourtLine l = lines[i];
            Say(l.who, l.text, () => RunScript(lines, i + 1, onEnd), l.expr);
        }

        // ============================================================
        //  씬: 심리 (메인)
        // ============================================================
        List<CourtStatement> CurList() => H.side == "pros" ? _T.pros : _T.def;
        CourtStatement CurStmt()
        {
            List<CourtStatement> l = CurList();
            return (H.idx >= 0 && H.idx < l.Count) ? l[H.idx] : null;
        }

        DeductionRule ActiveDeduction()
        {
            CourtStatement st = CurStmt();
            if (st == null || string.IsNullOrEmpty(st.over)) return null;
            return _db.GetDeduction(_caseMode, H.side);
        }

        void EnterHearing()
        {
            _scene = Scene.Hearing;
            _audio.SetMood("hearing");
            _audio.StartScore();
            ClearSceneLayer();

            // 원본 resetHearing
            H = new HearingState { order = _cfg.orderStart, trust = _cfg.trustStart, banner = _cfg.bannerDuration };
            ResetScaleVisual(1, 1);
            _fanfarePlayed = false;
            _verdCapacity = _verdDisp = null;

            float VW = _cfg.refWidth, VH = _cfg.refHeight;

            BuildScaleWidget(_sceneLayer);
            BuildTrustAndOrder(_sceneLayer);

            // 측 배너 (좌상단 칩)
            _bannerRoot = UiKit.Place(UiKit.Node("SideChip", _sceneLayer), 40, 40, 420, 80);
            // 원본 좌표: 카운터 텍스트 베이스라인 y=108 (질서 라벨 y112~132 와 겹치지 않게 y48=절대 88~110)
            _stmtCounter = UiKit.Label(_bannerRoot, "", 4, 48, 400, 22, 18, _cfg.C(_cfg.green), TextAnchor.MiddleLeft, FontStyle.Bold, "counter");

            // 증언 패널 — 원본 drawHearing 은 초상 위에 그린다(초상→게이지→증언→버튼 순).
            // 초상 레이어보다 위인 HUD 레이어에 두어 긴 진술이 초상에 가려지지 않게 한다.
            float bx = 100, by = VH - 190, bw = VW - 200, bh = 150;
            _stmtPanel = UiKit.Place(UiKit.Node("Statement", _hudLayer), bx, by, bw, bh);
            _stmtEdge = UiKit.Panel(_stmtPanel, 0, 0, bw, bh, _cfg.C(_cfg.crimsonHi), 16, "edge");
            UiKit.Panel(_stmtEdge.transform, 2.5f, 2.5f, bw - 5, bh - 5, new Color(12 / 255f, 10 / 255f, 8 / 255f, .9f), 14, "fill");
            _stmtTag = UiKit.Panel(_stmtPanel, 24, -18, 200, 36, _cfg.C(_cfg.crimson), 8, "tag");
            _stmtName = UiKit.Label(_stmtTag.transform, "", 17, 0, 340, 36, 20, _cfg.C(_cfg.white), TextAnchor.MiddleLeft, FontStyle.Bold, "tagText");
            _stmtBody = UiKit.Label(_stmtPanel, "", 40, 32, bw - 90, bh - 42, 25, _cfg.C(_cfg.cream), TextAnchor.UpperLeft, FontStyle.Normal, "body");
            _stmtOvermark = UiKit.Label(_stmtPanel, "", bw - 340, bh - 34, 320, 24, 15, _cfg.C(_cfg.gold), TextAnchor.MiddleRight, FontStyle.Bold, "overmark");

            // 하단 버튼 (원본 layoutHearing 좌표)
            _btnPrev = Btn(_hudLayer, _T.hearing.prev, 120, VH - 88, 150, 56, "wood", PrevStmt, 20);
            _btnNext = Btn(_hudLayer, _T.hearing.next, 280, VH - 88, 150, 56, "wood", NextStmt, 20);
            _btnRecord = Btn(_hudLayer, _T.hearing.record, VW / 2 - 90, VH - 88, 180, 56, "navy", OpenRecord, 20);
            _btnGavel = Btn(_hudLayer, _T.hearing.gavel, VW - 270, VH - 88, 150, 56, "gold", DoGavel, 20);
            _btnVerdict = Btn(_hudLayer, _T.hearing.verdict, VW - 115, VH - 88, 90, 56, "ghost", TryVerdictBtn, 16);

            // 의사봉 가이드
            _gavelGuide = UiKit.Place(UiKit.Node("GavelGuide", _hudLayer), 300, 154, 680, 52);
            Image gg = UiKit.Panel(_gavelGuide, 0, 0, 680, 52, new Color(18 / 255f, 14 / 255f, 9 / 255f, .94f), 10, "bg");
            UiKit.Label(_gavelGuide, _copy != null ? _copy.gavel : "", 10, 0, 660, 52, 18, _cfg.C(_cfg.goldHi), TextAnchor.MiddleCenter, FontStyle.Bold, "text");
            _gavelGuide.gameObject.SetActive(false);

            BuildHintBox(_hudLayer);

            BannerSay();
            RefreshStatement();
        }

        void BannerSay()
        {
            H.banner = _cfg.bannerDuration;
            _audio.Page();
            string t = H.side == "pros" ? _T.hearing.prosBanner : _T.hearing.defBanner;
            StartCoroutine(CoBannerSplash(t));
            RefreshSideChip();
        }

        IEnumerator CoBannerSplash(string text)
        {
            Text t = UiKit.Label(_fxLayer, text, 0, 130, _cfg.refWidth, 80, 40, _cfg.C(_cfg.goldHi), TextAnchor.MiddleCenter, FontStyle.Bold, "BannerSplash");
            Outline o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, .85f); o.effectDistance = new Vector2(3, -3);
            float life = 1.6f, e = 0;
            while (e < life)
            {
                e += Time.deltaTime;
                float a = e < .2f ? e / .2f : e > life - .5f ? (life - e) / .5f : 1f;
                Color c = t.color; c.a = a; t.color = c;
                yield return null;
            }
            Destroy(t.gameObject);
        }

        void RefreshSideChip()
        {
            if (_bannerRoot == null) return;
            foreach (Transform c in _bannerRoot) if (c.name == "chip") Destroy(c.gameObject);
            string label = H.side == "pros" ? _T.hearing.prosTitle : _T.hearing.defTitle;
            Color col = H.side == "pros" ? _cfg.C(_cfg.crimsonHi) : _cfg.C(_cfg.navyHi);
            Image chip = UiKit.Panel(_bannerRoot, 0, 0, 30 + label.Length * 24, 44, col, 10, "chip");
            UiKit.Label(chip.transform, label, 0, 0, 30 + label.Length * 24, 44, 24, _cfg.C(_cfg.white), TextAnchor.MiddleCenter, FontStyle.Bold, "label");
        }

        void RefreshStatement()
        {
            CourtStatement st = CurStmt();
            if (st == null || _stmtPanel == null) return;
            bool over = !string.IsNullOrEmpty(st.over);
            Color side = H.side == "pros" ? _cfg.C(_cfg.crimsonHi) : _cfg.C(_cfg.navyHi);
            _stmtEdge.color = side;
            _stmtTag.color = TagColor(st.who);
            _stmtName.text = CharName(st.who);
            _stmtTag.rectTransform.sizeDelta = new Vector2(Mathf.Max(90, _stmtName.preferredWidth + 34), 36);
            _stmtBody.text = UiKit.Sanitize(st.text);
            _stmtBody.color = over ? _cfg.C(_cfg.goldHi) : _cfg.C(_cfg.cream);
            _stmtOvermark.text = over ? _T.hearing.overmark : "";
            _stmtCounter.text = UiKit.Sanitize(_T.hearing.testifying.Replace("{0}", (H.idx + 1) + " / " + CurList().Count));
            if (!_dlg.Active) ShowPortrait(st.who, st.expr);
        }

        void PrevStmt() { if (H.locked) return; if (H.idx > 0) { H.idx--; _audio.Tap(); RefreshStatement(); } }

        void NextStmt()
        {
            if (H.locked) return;
            List<CourtStatement> l = CurList();
            if (H.idx < l.Count - 1) { H.idx++; _audio.Tap(); RefreshStatement(); }
            else
            {
                if (H.side == "pros" && !H.prosTrimmed) Hint(_T.hearing.prosHint);
                else if (H.side == "def" && !H.defTrimmed) Hint(_T.hearing.defHint);
            }
        }

        void TryVerdictBtn()
        {
            if (H.prosTrimmed && H.defTrimmed) EnterVerdict();
            else Hint(_T.hearing.tooEarly);
        }

        // ---------------- 증거 기록 오버레이 ----------------
        void OpenRecord()
        {
            if (H.locked || H.recordOpen) return;
            H.recordOpen = true; H.sel = 0;
            _audio.Page();
            BuildRecord();
        }

        void CloseRecord()
        {
            H.recordOpen = false;
            if (_recordRoot != null) { Destroy(_recordRoot.gameObject); _recordRoot = null; }
        }

        void BuildRecord()
        {
            if (_recordRoot != null) Destroy(_recordRoot.gameObject);
            float VW = _cfg.refWidth, VH = _cfg.refHeight;
            _recordRoot = UiKit.Stretch(UiKit.Node("Record", _overlayLayer));

            // 배경 흡수
            Image absorb = _recordRoot.gameObject.AddComponent<Image>();
            absorb.color = new Color(6 / 255f, 5 / 255f, 10 / 255f, .72f);
            absorb.raycastTarget = true;

            float px = 140, py = 90, pw = VW - 280, ph = VH - 260;
            DeductionRule rule = ActiveDeduction();
            string claim = rule != null ? CourtDb.LocText(rule.claim, _activeLang) : "";
            bool hasClaim = !string.IsNullOrEmpty(claim);
            float gy = py + (hasClaim ? 116 : 70);
            float cellW = 110;

            Image panel = UiKit.Panel(_recordRoot, px, py, pw, ph, _cfg.C("#1a2740"), 16, "panel");
            Image head = UiKit.Panel(_recordRoot, px, py, pw, 46, _cfg.C(_cfg.navyHi), 12, "head");
            UiKit.Label(head.transform, _T.hearing.recordTitle, 22, 0, pw - 44, 46, 24, _cfg.C(_cfg.white), TextAnchor.MiddleLeft, FontStyle.Bold, "title");

            if (hasClaim)
            {
                Image cs = UiKit.Panel(_recordRoot, px + 20, py + 56, pw - 40, 42, _cfg.C("#0c1424", .92f), 8, "claim");
                UiKit.Label(cs.transform, (_copy != null ? _copy.claim : "") + "   「" + claim + "」", 14, 0, pw - 68, 42, 17, _cfg.C(_cfg.cream), TextAnchor.MiddleLeft, FontStyle.Normal, "claimText");
            }

            // 증거 셀
            for (int i = 0; i < _T.evidence.Count; i++)
            {
                int idx = i;
                CourtEvidence ev = _T.evidence[i];
                float cx = px + 24 + i * (cellW + 8);
                UiKit.UiButton cell = UiKit.MakeButton(_recordRoot, "", cx, gy, cellW, cellW, "navy", () => { H.sel = idx; _audio.Tap(); BuildRecord(); }, 15, "cell_" + ev.id);
                cell.fill.color = i == H.sel ? _cfg.C("#2e4778") : _cfg.C("#223a5e");
                cell.edge.color = i == H.sel ? _cfg.C(_cfg.goldHi) : _cfg.C("#43608c");
                UiKit.Label(cell.rt, ev.name.Substring(0, 1), 0, 8, cellW, 58, 42, _cfg.C(ev.col), TextAnchor.MiddleCenter, FontStyle.Bold, "icon");
                UiKit.Label(cell.rt, ev.name, 4, cellW - 34, cellW - 8, 28, 14, _cfg.C(_cfg.white), TextAnchor.MiddleCenter, FontStyle.Normal, "name");
            }

            // 상세
            CourtEvidence sel = _T.evidence[Mathf.Clamp(H.sel, 0, _T.evidence.Count - 1)];
            float dx = px + 24, dy = gy + cellW + 22, dw = pw - 48, dh = ph - (gy - py) - cellW - 40;
            Image detail = UiKit.Panel(_recordRoot, dx, dy, dw, dh, _cfg.C("#12203a"), 10, "detail");
            UiKit.Label(detail.transform, sel.name, 18, 8, dw - 200, 34, 20, _cfg.C(_cfg.goldHi), TextAnchor.MiddleLeft, FontStyle.Bold, "detailName");
            if (hasClaim && _copy != null)
            {
                string rel = VerdictLogic.Relation(_db, _caseMode, H.side, CurStmt(), sel.id);
                string relText = rel == "direct" ? _copy.direct : rel == "near" ? _copy.near : _copy.other;
                Color relCol = rel == "direct" ? _cfg.C(_cfg.green) : rel == "near" ? _cfg.C(_cfg.goldHi) : _cfg.C(_cfg.red);
                UiKit.Label(detail.transform, relText, dw - 420, 8, 400, 34, 15, relCol, TextAnchor.MiddleRight, FontStyle.Bold, "relation");
            }
            UiKit.Label(detail.transform, sel.desc, 18, 46, dw - 36, dh - 60, 19, _cfg.C(_cfg.cream), TextAnchor.UpperLeft, FontStyle.Normal, "desc");

            // 제시 / 닫기
            UiKit.MakeButton(_recordRoot, _T.hearing.present, px + pw - 360, py + ph + 16, 160, 52, "crimson", () => { _audio.Tap(); PresentSel(); }, 22, "BtnPresent");
            UiKit.MakeButton(_recordRoot, _T.hearing.close, px + pw - 180, py + ph + 16, 160, 52, "wood", () => { _audio.Tap(); CloseRecord(); }, 22, "BtnClose");
        }

        // ---------------- 증거 제시 (원본 presentSel 1:1) ----------------
        void PresentSel()
        {
            CourtEvidence ev = _T.evidence[Mathf.Clamp(H.sel, 0, _T.evidence.Count - 1)];
            CourtStatement st = CurStmt();
            if (ev == null || st == null) return;

            if (string.IsNullOrEmpty(st.over))
            {
                CloseRecord();
                Hint(H.side == "pros" ? _T.hearing.prosHint : _T.hearing.defHint);
                return;
            }

            string relation = VerdictLogic.Relation(_db, _caseMode, H.side, st, ev.id);

            if (relation == "direct")
            {
                CloseRecord();
                H.locked = true;
                bool prosSide = st.over == "pros";
                if (prosSide) { H.prosTrimmed = true; _scalePros = 0; }
                else { H.defTrimmed = true; _scaleDef = 0; }

                Burst(_T.hearing.accepted, _cfg.C(_cfg.goldHi), true);
                _audio.Ding(); _audio.ScaleChime();
                Flash(_cfg.C(_cfg.gold, .28f), .4f);
                Shake(12, .4f);

                string speaker = prosSide ? "pros" : "def";
                string admitted = prosSide ? _T.hearing.admittedPros : _T.hearing.admittedDef;
                string judgeLine = prosSide ? _T.hearing.judgePros : _T.hearing.judgeDef;

                Say(speaker, admitted, () =>
                {
                    Say("judge", judgeLine, () =>
                    {
                        // 반대측 소란 → 의사봉 필요
                        H.pendingGavel = true;
                        H.order = Mathf.Max(_cfg.objectionOrderFloor, H.order - _cfg.objectionOrderDrop);
                        string opp = prosSide ? "def" : "pros";
                        Burst(_T.hearing.objection, opp == "pros" ? _cfg.C(_cfg.crimsonHi) : _cfg.C(_cfg.navyHi), false);
                        _audio.Objection();
                        _audio.SetMood("objection");
                        Say(opp, opp == "def" ? _T.hearing.defenseRetort : _T.hearing.prosecutorRetort, () =>
                        {
                            Say("mayoi", _T.hearing.gavelPrompt, () => { _dlg.Hide(); RefreshStatement(); }, "tense");
                        }, "tense");
                    }, "calm");
                }, "shock");
            }
            else if (relation == "near")
            {
                CloseRecord();
                Burst(_T.hearing.rejected, _cfg.C(_cfg.gold), false);
                _audio.Buzz();
                string nh = VerdictLogic.NearMissHint(_db, _caseMode, H.side, ev.id, _activeLang);
                Hint(string.IsNullOrEmpty(nh) ? _T.hearing.chartHint : nh);
            }
            else
            {
                CloseRecord();
                Burst(_T.hearing.rejected, _cfg.C(_cfg.red), false);
                _audio.Buzz();
                Flash(_cfg.C(_cfg.red, .18f), .24f);
                string missKey = _caseMode + ":" + H.side + ":" + H.idx;
                H.misses.TryGetValue(missKey, out int m);
                H.misses[missKey] = m + 1;
                if (m + 1 == 1)
                {
                    H.order = Mathf.Max(0, H.order - _cfg.firstMissOrderPenalty);
                    Hint(_copy != null ? _copy.firstMiss : "");
                }
                else
                {
                    H.trust = Mathf.Max(0, H.trust - _cfg.missTrustPenalty);
                    H.order = Mathf.Max(0, H.order - _cfg.missOrderPenalty);
                    Say("mayoi", _T.hearing.wrongEvidence, () =>
                    {
                        _dlg.Hide(); RefreshStatement();
                        if (H.trust <= 0) Mistrial("evidence");
                    }, "tense");
                }
            }
        }

        // ---------------- 의사봉 (원본 doGavel 1:1) ----------------
        void DoGavel()
        {
            if (H.pendingGavel)
            {
                H.pendingGavel = false;
                H.order = _cfg.orderStart;
                H.locked = false;
                _audio.SetMood(H.side == "pros" ? "hearing" : "verdict");
                Burst(_T.hearing.orderBurst, _cfg.C(_cfg.goldHi), true);
                _audio.Gavel();
                Shake(16, .45f);
                SpawnParticles(_cfg.refWidth - 195, _cfg.refHeight - 60, 14, _cfg.C(_cfg.gold));

                if (H.side == "pros" && H.prosTrimmed)
                {
                    Say("mayoi", _T.hearing.nextDefense, () =>
                    {
                        H.side = "def"; H.idx = 0;
                        _dlg.Hide();
                        BannerSay();
                        RefreshStatement();
                    }, "calm");
                }
                else if (H.side == "def" && H.defTrimmed)
                {
                    Say("mayoi", _T.hearing.readyVerdict, () =>
                    {
                        _dlg.Hide();
                        EnterVerdict();
                    }, "smile");
                }
            }
            else
            {
                if (H.order < _cfg.gavelRecoverThreshold)
                {
                    H.order = Mathf.Min(_cfg.orderStart, H.order + _cfg.gavelRecover);
                    _audio.Gavel();
                    Shake(8, .25f);
                    SpawnParticles(_cfg.refWidth - 195, _cfg.refHeight - 60, 8, _cfg.C(_cfg.gold));
                    Burst(_T.hearing.orderBurst, _cfg.C(_cfg.goldHi), false);
                }
                else _audio.Tap();
            }
        }

        void Mistrial(string cause)
        {
            H.locked = true;
            _audio.SetMood("mistrial");
            Flash(_cfg.C(_cfg.red, .4f), .6f);
            _audio.Lose();
            Say("mayoi", _T.hearing.mistrial, () => EnterGameover(), "sad");
        }

        // ============================================================
        //  씬: 판결
        // ============================================================
        void EnterVerdict()
        {
            _scene = Scene.Verdict;
            _audio.SetMood("verdict");
            ClearSceneLayer();
            _verdCapacity = null; _verdDisp = null;

            float VW = _cfg.refWidth;
            BuildScaleWidget(_sceneLayer);
            BuildHintBox(_hudLayer);

            UiKit.Label(_sceneLayer, _T.verdict.heading, 0, 168, VW, 44, 38, _cfg.C(_cfg.goldHi), TextAnchor.MiddleCenter, FontStyle.Bold, "Heading");
            UiKit.Label(_sceneLayer, _T.verdict.sub, 0, 212, VW, 26, 17, _cfg.C(_cfg.cream, .85f), TextAnchor.MiddleCenter, FontStyle.Normal, "Sub");

            // 확정된 사실 카드
            List<VerdictFact> facts = _db.GetVerdictFacts(_caseMode);
            for (int i = 0; i < facts.Count && i < 2; i++)
            {
                float fx = 150 + i * 500;
                Image card = UiKit.Panel(_sceneLayer, fx, 250, 480, 52, new Color(12 / 255f, 16 / 255f, 28 / 255f, .9f), 10, "Fact" + i);
                UiKit.Label(card.transform, ((_copy != null ? _copy.facts : "") + "  ·  ") + CourtDb.LocText(facts[i], _activeLang), 14, 0, 452, 52, 16, _cfg.C(_cfg.cream), TextAnchor.MiddleLeft, FontStyle.Normal, "text");
            }

            UiKit.Label(_sceneLayer, _T.verdict.capTitle, 130, 300, 600, 26, 19, _cfg.C(_cfg.gold), TextAnchor.MiddleLeft, FontStyle.Bold, "CapTitle");
            _verdCapBtns.Add(Btn(_hudLayer, _T.verdict.capFull, 130, 330, 320, 60, "crimson", () => { _verdCapacity = "full"; _audio.Tap(); }, 20));
            _verdCapBtns.Add(Btn(_hudLayer, _T.verdict.capPartial, 480, 330, 320, 60, "gold", () => { _verdCapacity = "partial"; _audio.Tap(); }, 20));
            _verdCapBtns.Add(Btn(_hudLayer, _T.verdict.capNone, 830, 330, 320, 60, "navy", () => { _verdCapacity = "none"; _audio.Tap(); }, 20));

            UiKit.Label(_sceneLayer, _T.verdict.dispTitle, 130, 430, 600, 26, 19, _cfg.C(_cfg.gold), TextAnchor.MiddleLeft, FontStyle.Bold, "DispTitle");
            _verdDispBtns.Add(Btn(_hudLayer, _T.verdict.dispPunish, 130, 460, 320, 60, "crimson", () => { _verdDisp = "punish"; _audio.Tap(); }, 20));
            _verdDispBtns.Add(Btn(_hudLayer, _T.verdict.dispBoth, 480, 460, 320, 60, "gold", () => { _verdDisp = "both"; _audio.Tap(); }, 20));
            _verdDispBtns.Add(Btn(_hudLayer, _T.verdict.dispNone, 830, 460, 320, 60, "navy", () => { _verdDisp = "none"; _audio.Tap(); }, 20));

            Btn(_hudLayer, _T.verdict.confirm, VW / 2 - 160, 570, 320, 64, "gold", ConfirmVerdict, 24);

            Say("mayoi", _T.verdict.intro, () => { _dlg.Hide(); }, "calm");
        }

        void ConfirmVerdict()
        {
            if (string.IsNullOrEmpty(_verdCapacity) || string.IsNullOrEmpty(_verdDisp)) { Hint(_T.verdict.chooseBoth); return; }
            VerdictResult r = VerdictLogic.Eval(_verdCapacity, _verdDisp, _T.verdict);
            if (r.ok)
            {
                _audio.Gavel();
                EnterEnding();
            }
            else
            {
                _audio.Buzz();
                Burst(_T.verdict.reconsider, _cfg.C(_cfg.red), false);
                Shake(10, .35f);
                Say("mayoi", r.msg, () => { _dlg.Hide(); }, "tense");
            }
        }

        // ============================================================
        //  씬: 엔딩
        // ============================================================
        void EnterEnding()
        {
            _scene = Scene.Ending;
            _audio.SetMood("ending");
            ClearSceneLayer();
            _fanfarePlayed = false;
            ResetScaleVisual(0, 0);
            _scaleGlow = 1;
            BuildScaleWidget(_sceneLayer);
            StartCoroutine(CoEndingSlams());
        }

        IEnumerator CoEndingSlams()
        {
            yield return new WaitForSeconds(_cfg.slamStartDelay);
            for (int n = 1; n <= _cfg.slamCount; n++)
            {
                _audio.Gavel();
                Shake(18, .4f);
                SpawnParticles(_cfg.refWidth / 2f, 300, 18, _cfg.C(_cfg.gold));
                Burst(n < _cfg.slamCount ? _T.ending.bang : _T.ending.bangLast, _cfg.C(_cfg.goldHi), true);
                yield return new WaitForSeconds(_cfg.slamInterval);
            }
            yield return new WaitForSeconds(_cfg.endSeqDelay);
            RunScript(_T.ending.seq, 0, () =>
            {
                PlayFinalFanfare();
                LayoutEndButtons();
            });
        }

        void PlayFinalFanfare()
        {
            if (_fanfarePlayed) return;
            _fanfarePlayed = true;
            _audio.Win();
            Flash(_cfg.C(_cfg.goldHi, .34f), .45f);
            Shake(10, .45f);
            for (int i = 0; i < 52; i++)
            {
                float x = _cfg.refWidth / 2f + UnityEngine.Random.Range(-180f, 180f);
                float y = 300 + UnityEngine.Random.Range(-35f, 35f);
                Color c = i % 4 == 0 ? _cfg.C(_cfg.goldHi) : i % 4 == 1 ? _cfg.C(_cfg.gold) : i % 4 == 2 ? _cfg.C(_cfg.cream) : Color.white;
                SpawnParticles(x, y, 1, c, 4f);
            }

            float VW = _cfg.refWidth;
            UiKit.Label(_sceneLayer, _T.ending.clear, 0, 236, VW, 60, 34, _cfg.C(_cfg.goldHi), TextAnchor.MiddleCenter, FontStyle.Bold, "ClearLine");
            if (!string.IsNullOrEmpty(_T.ending.nextCase))
                UiKit.Label(_sceneLayer, _T.ending.nextCase, 0, 300, VW, 30, 17, _cfg.C(_cfg.cream, .85f), TextAnchor.MiddleCenter, FontStyle.Normal, "NextCase");
        }

        void LayoutEndButtons()
        {
            float VW = _cfg.refWidth, VH = _cfg.refHeight;
            Btn(_hudLayer, _T.ending.retry, VW / 2 - 320, VH - 96, 300, 64, "wood", () => EnterHearing(), 22);
            Btn(_hudLayer, _T.ending.title, VW / 2 + 20, VH - 96, 300, 64, "gold", () => EnterTitle(), 22);
            // 좌우 초상 (마요이 / 재판장)
            ShowEndingPortraits();
        }

        void ShowEndingPortraits()
        {
            MakeStaticPortrait("mayoi", 330, "smile");
            MakeStaticPortrait("judge", _cfg.refWidth - 330, "calm");
        }

        void MakeStaticPortrait(string who, float cx, string expr)
        {
            RectTransform node = UiKit.Place(UiKit.Node("EndPortrait_" + who, _sceneLayer), cx - 160, 250, 320, 360);
            Sprite sp = who == "judge" ? _judgeSprite : who == "mayoi" ? _mayoiSprite : who == "pros" ? _prosSprite : null;
            if (sp != null)
            {
                Image img = node.gameObject.AddComponent<Image>();
                img.sprite = sp; img.preserveAspect = true; img.raycastTarget = false;
            }
            else
            {
                Image fb = UiKit.Panel(node, 40, 20, 240, 320, _cfg.C(_cfg.purple), 18, "fb");
                UiKit.Label(fb.transform, CharName(who).Substring(0, 1), 0, 90, 240, 120, 84, _cfg.C(_cfg.white), TextAnchor.MiddleCenter, FontStyle.Bold, "init");
            }
        }

        // ============================================================
        //  씬: 재판 무효
        // ============================================================
        void EnterGameover()
        {
            _scene = Scene.Gameover;
            _audio.SetMood("mistrial");
            ClearSceneLayer();
            float VW = _cfg.refWidth;

            UiKit.Label(_sceneLayer, _T.gameover.head, 0, 190, VW, 80, 64, _cfg.C(_cfg.red), TextAnchor.MiddleCenter, FontStyle.Bold, "Head");
            UiKit.Label(_sceneLayer, _T.gameover.sub, 0, 280, VW, 32, 21, _cfg.C(_cfg.cream), TextAnchor.MiddleCenter, FontStyle.Normal, "Sub");
            UiKit.Label(_sceneLayer, _T.gameover.hint, 0, 330, VW, 60, 17, _cfg.C(_cfg.goldHi, .9f), TextAnchor.MiddleCenter, FontStyle.Normal, "Hint");

            Btn(_hudLayer, _T.gameover.retry, VW / 2 - 320, 460, 300, 64, "gold", () => EnterHearing(), 22);
            Btn(_hudLayer, _T.gameover.title, VW / 2 + 20, 460, 300, 64, "wood", () => EnterTitle(), 22);
        }

        // ============================================================
        //  저울 / 신뢰도 / 질서 위젯
        // ============================================================
        void ResetScaleVisual(float pros, float def)
        {
            _scalePros = pros; _scaleDef = def; _scaleAngle = 0; _scaleGlow = 0;
        }

        void BuildScaleWidget(Transform parent)
        {
            float VW = _cfg.refWidth;
            _scaleRoot = UiKit.Place(UiKit.Node("Scale", parent), VW / 2 - 170, 40, 340, 130);

            // 기둥
            UiKit.Panel(_scaleRoot, 162, 34, 16, 84, _cfg.C(_cfg.woodHi), 4, "pillar");
            UiKit.Panel(_scaleRoot, 120, 112, 100, 12, _cfg.C(_cfg.woodDk), 4, "base");

            // 빔 (회전)
            _scaleBeam = UiKit.Place(UiKit.Node("beam", _scaleRoot), 170, 40, 0, 0);
            _scaleBeam.pivot = new Vector2(.5f, .5f);
            Image beam = UiKit.Panel(_scaleBeam, -150, -4, 300, 8, _cfg.C(_cfg.gold), 3, "bar");

            // 접시 (빔 자식, 역회전으로 수평 유지)
            _panL = UiKit.Panel(_scaleBeam, -150 - 34, 6, 68, 12, _cfg.C(_cfg.crimsonHi), 5, "panL");
            _panR = UiKit.Panel(_scaleBeam, 150 - 34, 6, 68, 12, _cfg.C(_cfg.navyHi), 5, "panR");
            _weightL = UiKit.Panel(_panL.transform, 18, -22, 32, 22, _cfg.C(_cfg.crimson), 4, "wL");
            _weightR = UiKit.Panel(_panR.transform, 18, -22, 32, 22, _cfg.C(_cfg.navy), 4, "wR");

            // 라벨
            UiKit.Label(_scaleRoot, _T.scale.pros, -40, 92, 120, 24, 16, _cfg.C(_cfg.crimsonHi), TextAnchor.MiddleCenter, FontStyle.Bold, "labL");
            UiKit.Label(_scaleRoot, _T.scale.def, 260, 92, 120, 24, 16, _cfg.C(_cfg.navyHi), TextAnchor.MiddleCenter, FontStyle.Bold, "labR");

            // 중립 배지
            _neutralBadge = UiKit.Place(UiKit.Node("neutral", _scaleRoot), 92, -6, 156, 30);
            Image nb = UiKit.Panel(_neutralBadge, 0, 0, 156, 30, _cfg.C(_cfg.gold), 8, "bg");
            UiKit.Label(_neutralBadge, _T.scale.neutralOn, 0, 0, 156, 30, 16, _cfg.C("#2a2010"), TextAnchor.MiddleCenter, FontStyle.Bold, "text");
            _neutralBadge.gameObject.SetActive(false);
        }

        void BuildTrustAndOrder(Transform parent)
        {
            float VW = _cfg.refWidth;
            // 신뢰도 (상단 우측)
            UiKit.Label(parent, _T.hearing.trust, VW - 470, 28, 156, 22, 15, _cfg.C(_cfg.cream), TextAnchor.MiddleRight, FontStyle.Bold, "TrustLabel");
            _trustCells.Clear();
            for (int i = 0; i < _cfg.trustStart; i++)
            {
                Image cell = UiKit.Panel(parent, VW - 300 + i * 30, 30, 24, 18, _cfg.C(_cfg.gold), 4, "trust" + i);
                _trustCells.Add(cell);
            }
            // 질서 게이지 (좌상단)
            UiKit.Label(parent, _T.hearing.order, 44, 112, 220, 20, 14, _cfg.C(_cfg.cream), TextAnchor.MiddleLeft, FontStyle.Bold, "OrderLabel");
            UiKit.Panel(parent, 44, 134, 220, 16, _cfg.C("#2a2214"), 8, "orderBg");
            _orderFill = UiKit.Panel(parent, 44, 134, 220, 16, _cfg.C(_cfg.green), 8, "orderFill");
        }

        // ============================================================
        //  업데이트 루프 (원본 updateHearing / updateScale / 이펙트)
        // ============================================================
        void Update()
        {
            _time += Time.deltaTime;
            float dt = Time.deltaTime;

            // 흔들림
            if (_shakeT > 0)
            {
                _shakeT -= dt;
                float k = _shakeDur > 0 ? Mathf.Clamp01(_shakeT / _shakeDur) : 0;
                float a = _shakeAmp * k;
                _root.anchoredPosition = new Vector2(UnityEngine.Random.Range(-a, a), UnityEngine.Random.Range(-a, a));
                if (_shakeT <= 0) { _root.anchoredPosition = Vector2.zero; _shakeAmp = 0; }
            }

            // 힌트 타이머
            if (_hintT > 0)
            {
                _hintT -= dt;
                if (_hintBg != null)
                {
                    _hintBg.gameObject.SetActive(_hintT > 0 && !string.IsNullOrEmpty(_hintMsg));
                    float a = Mathf.Min(1f, _hintT);
                    Color c = _hintBg.color; c.a = .92f * a; _hintBg.color = c;
                    if (_hintText != null) { Color tc = _hintText.color; tc.a = a; _hintText.color = tc; }
                }
            }
            else if (_hintBg != null && _hintBg.gameObject.activeSelf) _hintBg.gameObject.SetActive(false);

            // 저울
            float target = (_scalePros - _scaleDef) * _cfg.scaleTiltDeg;
            _scaleAngle = Mathf.Lerp(_scaleAngle, target, dt * _cfg.scaleLerpSpeed);
            if (_scaleBeam != null)
            {
                _scaleBeam.localRotation = Quaternion.Euler(0, 0, _scaleAngle);
                if (_panL != null) _panL.rectTransform.localRotation = Quaternion.Euler(0, 0, -_scaleAngle);
                if (_panR != null) _panR.rectTransform.localRotation = Quaternion.Euler(0, 0, -_scaleAngle);
                if (_weightL != null) _weightL.gameObject.SetActive(_scalePros > 0);
                if (_weightR != null) _weightR.gameObject.SetActive(_scaleDef > 0);
                bool neutral = _scalePros <= 0 && _scaleDef <= 0;
                if (_neutralBadge != null)
                {
                    if (_neutralBadge.gameObject.activeSelf != neutral) _neutralBadge.gameObject.SetActive(neutral);
                    if (neutral) // 중립 확정 시 글로우 펄스
                    {
                        _scaleGlow = Mathf.Min(1f, _scaleGlow + dt * 2f);
                        _neutralBadge.localScale = Vector3.one * (1f + Mathf.Sin(_time * 4f) * .05f * _scaleGlow);
                    }
                }
            }

            if (_scene == Scene.Hearing) UpdateHearing(dt);
            else if (_scene == Scene.Verdict) UpdateVerdictSel();

            // 비네트 (심리 중 질서 저하)
            float va = (_scene == Scene.Hearing && H.order < 55) ? (55 - H.order) / 55f * .4f : 0f;
            Color vc = _vignette.color; vc.a = Mathf.Lerp(vc.a, va, dt * 6f); _vignette.color = vc;
        }

        void UpdateHearing(float dt)
        {
            if (H.banner > 0) H.banner -= dt;

            bool busy = _dlg.Active;

            // 질서 자연 감소 → 소란 → 신뢰도 감소 → 무효
            if (!H.pendingGavel && !busy && !H.recordOpen && !H.locked)
            {
                H.order = Mathf.Max(0, H.order - _cfg.orderDrainPerSec * dt);
                if (H.order <= 0)
                {
                    H.order = _cfg.orderZeroReset;
                    H.trust = Mathf.Max(0, H.trust - 1);
                    Burst(_T.hearing.noisy, _cfg.C(_cfg.red), false);
                    _audio.Buzz();
                    if (H.trust <= 0) { Mistrial("order"); return; }
                }
            }

            // 버튼 활성 (원본 updateHearing)
            if (_btnPrev != null)
            {
                _btnPrev.Enabled = !H.locked && !busy && !H.recordOpen;
                _btnNext.Enabled = !H.locked && !busy && !H.recordOpen;
                _btnRecord.Enabled = !H.locked && !busy;
                _btnGavel.Enabled = !busy;
                _btnVerdict.Enabled = !H.locked && !busy;
            }

            // 증언 패널은 대사 중엔 숨김 (원본과 동일)
            if (_stmtPanel != null && _stmtPanel.gameObject.activeSelf == busy)
                _stmtPanel.gameObject.SetActive(!busy);

            // 의사봉 가이드 + 게이블 버튼 펄스
            if (_gavelGuide != null)
            {
                bool g = H.pendingGavel && !H.recordOpen && !busy;
                if (_gavelGuide.gameObject.activeSelf != g) _gavelGuide.gameObject.SetActive(g);
            }
            if (_btnGavel != null)
            {
                float pulse = H.pendingGavel ? (Mathf.Sin(_time * 7f) * .5f + .5f) : 0f;
                _btnGavel.edge.color = Color.Lerp(_btnGavel.style.edge, _cfg.C(_cfg.goldHi), pulse);
            }

            // 신뢰도 / 질서 게이지
            for (int i = 0; i < _trustCells.Count; i++)
                _trustCells[i].color = i < H.trust ? _cfg.C(_cfg.gold) : _cfg.C("#3a3020");
            if (_orderFill != null)
            {
                float w = Mathf.Max(4, 220 * H.order / _cfg.orderStart);
                _orderFill.rectTransform.sizeDelta = new Vector2(w, 16);
                _orderFill.color = H.order > 55 ? _cfg.C(_cfg.green) : H.order > 25 ? _cfg.C(_cfg.amber) : _cfg.C(_cfg.red);
            }
        }

        void UpdateVerdictSel()
        {
            for (int i = 0; i < _verdCapBtns.Count; i++)
            {
                string key = i == 0 ? "full" : i == 1 ? "partial" : "none";
                _verdCapBtns[i].SetSelected(_verdCapacity == key);
            }
            for (int i = 0; i < _verdDispBtns.Count; i++)
            {
                string key = i == 0 ? "punish" : i == 1 ? "both" : "none";
                _verdDispBtns[i].SetSelected(_verdDisp == key);
            }
        }
    }
}
