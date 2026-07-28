// ============================================================
// Mental — 허브 (케이스 선택 + 언어)
// 원본 index.html 의 CASE 3·4 부분만. 최초 실행 시 언어 게이트.
// ============================================================
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mental
{
    public class HubScreen : MonoBehaviour
    {
        GameConfig _cfg;
        CourtDb _db;
        Action<string> _onPlay;
        RectTransform _rt;

        // 허브 UI 크롬 문자열 (게임 콘텐츠 아님 — 콘텐츠는 전부 court.json)
        static string HubTitle(string lang) => lang == "en" ? "Fair Trial" : lang == "ja" ? "公正裁判" : "공정재판";
        static string HubSub(string lang) => lang == "en" ? "Side Story · Select a case" : lang == "ja" ? "外伝 · ケースを選択" : "외전 · 플레이할 케이스를 선택하세요";
        static string LangGateTitle => "언어를 선택하세요 · Select Language · 言語を選択";
        static string KoOnly(string lang) => lang == "ko" ? "" : "한국어 전용 · Korean only";

        public static HubScreen Create(Transform parent, Action<string> onPlay)
        {
            RectTransform rt = UiKit.Stretch(UiKit.Node("Hub", parent));
            HubScreen h = rt.gameObject.AddComponent<HubScreen>();
            h._rt = rt;
            h._cfg = GameConfig.Instance;
            h._db = GameRoot.Db;
            h._onPlay = onPlay;
            if (Loc.HasChosen) h.BuildHub();
            else h.BuildLangGate();
            return h;
        }

        void Clear()
        {
            foreach (Transform c in _rt) Destroy(c.gameObject);
        }

        void BuildLangGate()
        {
            Clear();
            float VW = _cfg.refWidth, VH = _cfg.refHeight;
            UiKit.Stretch(UiKit.Panel(_rt, 0, 0, VW, VH, _cfg.C(_cfg.bg1), 1, "bg").rectTransform); // 풀블리드
            RectTransform box = UiKit.DesignBox("Content", _rt); // 콘텐츠는 중앙 1280×720
            UiKit.Label(box, LangGateTitle, 0, 200, VW, 40, 26, _cfg.C(_cfg.cream), TextAnchor.MiddleCenter, FontStyle.Bold, "gateTitle");
            string[] names = { "한국어", "English", "日本語" };
            for (int i = 0; i < Loc.Supported.Length; i++)
            {
                string lang = Loc.Supported[i];
                UiKit.MakeButton(box, names[i], VW / 2 - 160, 290 + i * 84, 320, 64, "gold", () =>
                {
                    GameRoot.Audio?.UnlockAudio();
                    Loc.Lang = lang;
                    BuildHub();
                }, 24, "lang_" + lang);
            }
        }

        void BuildHub()
        {
            Clear();
            float VW = _cfg.refWidth, VH = _cfg.refHeight;
            string lang = Loc.Lang;

            UiKit.Stretch(UiKit.Panel(_rt, 0, 0, VW, VH, _cfg.C(_cfg.bg1), 1, "bg").rectTransform); // 풀블리드

            // 배경 아트 (있으면) — 풀블리드
            Texture2D art = Resources.Load<Texture2D>("MentalArt/title_art");
            if (art != null)
            {
                RawImage ri = UiKit.CoverImage(_rt, art, "bgArt");
                ri.color = new Color(1, 1, 1, .22f);
            }

            RectTransform box = UiKit.DesignBox("Content", _rt); // 콘텐츠는 중앙 1280×720

            UiKit.Label(box, HubTitle(lang), 0, 84, VW, 80, 58, _cfg.C(_cfg.goldHi), TextAnchor.MiddleCenter, FontStyle.Bold, "title");
            UiKit.Label(box, HubSub(lang), 0, 168, VW, 28, 19, _cfg.C(_cfg.cream, .85f), TextAnchor.MiddleCenter, FontStyle.Normal, "sub");

            BuildCaseCard(box, "3", 128, lang);
            BuildCaseCard(box, "4", 668, lang);

            // 언어 스위치
            string[] names = { "한국어", "English", "日本語" };
            for (int i = 0; i < Loc.Supported.Length; i++)
            {
                string l = Loc.Supported[i];
                UiKit.UiButton b = UiKit.MakeButton(box, names[i], VW / 2 - 250 + i * 170, VH - 76, 160, 48, l == lang ? "gold" : "ghost", () =>
                {
                    GameRoot.Audio?.UnlockAudio();
                    Loc.Lang = l;
                    BuildHub();
                }, 18, "lang_" + l);
            }
        }

        void BuildCaseCard(Transform parent, string caseMode, float x, string lang)
        {
            CourtLocale loc = _db.GetLocale(caseMode, lang);
            if (loc == null) return;
            float y = 226, w = 484, h = 330;

            Image card = UiKit.Panel(parent, x, y, w, h, _cfg.C("#1c1610", .94f), 16, "case" + caseMode);
            Image edge = UiKit.Panel(card.transform, 0, 0, w, h, _cfg.C(_cfg.gold, .45f), 16, "edge");
            UiKit.Stretch(edge.rectTransform);
            Image edgeIn = UiKit.Panel(card.transform, 2.5f, 2.5f, w - 5, h - 5, _cfg.C("#1c1610", .96f), 14, "in");

            // 케이스 배경 미리보기
            Texture2D bg = Resources.Load<Texture2D>(caseMode == "4" ? "MentalArt/case4_bg" : "MentalArt/court_bg");
            if (bg != null)
            {
                RectTransform prevRt = UiKit.Place(UiKit.Node("preview", card.transform), 14, 14, w - 28, 150);
                RawImage prev = prevRt.gameObject.AddComponent<RawImage>();
                prev.texture = bg;
                prev.raycastTarget = false;
                UiKit.FitCover(prev, w - 28, 150);
            }

            UiKit.Label(card.transform, "CASE " + caseMode, 20, 172, 200, 24, 17, _cfg.C(_cfg.gold), TextAnchor.MiddleLeft, FontStyle.Bold, "label");
            UiKit.Label(card.transform, loc.title.main, 20, 196, w - 40, 40, 30, _cfg.C(_cfg.cream), TextAnchor.MiddleLeft, FontStyle.Bold, "name");
            UiKit.Label(card.transform, loc.title.tag, 20, 238, w - 40, 26, 15, _cfg.C(_cfg.cream, .7f), TextAnchor.MiddleLeft, FontStyle.Normal, "tag");

            string koOnly = caseMode == "4" ? KoOnly(lang) : "";
            if (!string.IsNullOrEmpty(koOnly))
                UiKit.Label(card.transform, koOnly, 20, 262, w - 40, 22, 13, _cfg.C(_cfg.amber), TextAnchor.MiddleLeft, FontStyle.Bold, "koOnly");

            UiKit.MakeButton(card.transform, loc.title.start, w - 220, h - 62, 200, 48, "gold", () =>
            {
                GameRoot.Audio?.UnlockAudio();
                _onPlay?.Invoke(caseMode);
            }, 19, "play" + caseMode);
        }
    }
}
