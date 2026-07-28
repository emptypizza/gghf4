// ============================================================
// Mental — 대사창 (원본 say / dlgTap / updateDlg / drawDlg 포팅)
// 타자기 연출 + 이름표 + 진행 화살표. 초상은 CourtEngine 이 별도 표시.
// ============================================================
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mental
{
    public class DialogueBox : MonoBehaviour
    {
        RectTransform _rt;
        Image _panelEdge, _tagBg;
        Text _tagText, _body, _arrow;
        GameConfig _cfg;

        string _full = "";
        float _shown;
        bool _done;
        Action _onDone;
        float _blink;

        public bool Active { get; private set; }
        public bool Done => _done;
        public string SpeakerKey { get; private set; } = "";
        public string Expr { get; private set; } = "calm";

        public static DialogueBox Create(Transform parent)
        {
            GameConfig cfg = GameConfig.Instance;
            float VW = cfg.refWidth, VH = cfg.refHeight;
            float bx = 90, by = VH - 186, bw = VW - 180, bh = 150;

            RectTransform rt = UiKit.Place(UiKit.Node("DialogueBox", parent), bx, by, bw, bh);
            DialogueBox dlg = rt.gameObject.AddComponent<DialogueBox>();
            dlg._cfg = cfg;
            dlg._rt = rt;

            // 본체 패널 (짙은 배경 + 금테)
            dlg._panelEdge = UiKit.Panel(rt, 0, 0, bw, bh, cfg.C(cfg.gold, .5f), 16, "edge");
            UiKit.Panel(dlg._panelEdge.transform, 2.5f, 2.5f, bw - 5, bh - 5, new Color(12 / 255f, 10 / 255f, 8 / 255f, .92f), 14, "fill");
            UiKit.Panel(dlg._panelEdge.transform, 7, 7, bw - 14, bh - 14, new Color(0, 0, 0, 0), 12, "inner"); // 장식 자리

            // 이름표
            dlg._tagBg = UiKit.Panel(rt, 24, -18, 220, 36, cfg.C(cfg.purpleHi), 8, "tag");
            dlg._tagText = UiKit.Label(dlg._tagBg.transform, "", 17, 0, 220 - 34, 36, 20, cfg.C(cfg.white), TextAnchor.MiddleLeft, FontStyle.Bold, "tagText");

            // 본문
            dlg._body = UiKit.Label(rt, "", 40, 34, bw - 90, bh - 44, 25, cfg.C(cfg.cream), TextAnchor.UpperLeft, FontStyle.Normal, "body");

            // 진행 화살표
            dlg._arrow = UiKit.Label(rt, "▼", bw - 52, bh - 40, 30, 30, 20, cfg.C(cfg.gold), TextAnchor.MiddleCenter, FontStyle.Bold, "arrow");
            dlg._arrow.gameObject.SetActive(false);

            rt.gameObject.SetActive(false);
            return dlg;
        }

        public void Say(string speakerKey, string displayName, Color tagColor, string text, Action onDone, string expr = "calm")
        {
            Active = true;
            SpeakerKey = speakerKey;
            Expr = string.IsNullOrEmpty(expr) ? "calm" : expr;
            _full = UiKit.Sanitize(text);
            _shown = 0;
            _done = false;
            _onDone = onDone;

            gameObject.SetActive(true);
            _tagText.text = displayName;
            _tagBg.color = tagColor;
            // 이름 길이에 맞게 태그 폭 조정
            float w = _tagText.preferredWidth + 34;
            _tagBg.rectTransform.sizeDelta = new Vector2(Mathf.Max(90, w), 36);
            _body.text = "";
            _arrow.gameObject.SetActive(false);
        }

        public void Hide()
        {
            Active = false;
            _onDone = null;
            gameObject.SetActive(false);
        }

        // 원본 dlgTap: 진행 중이면 즉시 완성, 완료 상태면 닫고 onDone
        public bool Tap()
        {
            if (!Active) return false;
            if (!_done)
            {
                _shown = _full.Length;
                _done = true;
                _body.text = _full;
                _arrow.gameObject.SetActive(true);
                return true;
            }
            Action f = _onDone;
            Hide();
            f?.Invoke();
            return true;
        }

        void Update()
        {
            if (!Active) return;
            if (!_done)
            {
                _shown += _cfg.typeSpeed * Time.deltaTime;
                if (_shown >= _full.Length) { _shown = _full.Length; _done = true; _arrow.gameObject.SetActive(true); }
                _body.text = _full.Substring(0, Mathf.Min(_full.Length, Mathf.FloorToInt(_shown)));
            }
            else
            {
                _blink += Time.deltaTime * 6f;
                Color c = _arrow.color;
                c.a = 0.4f + (Mathf.Sin(_blink) * .5f + .5f) * .6f;
                _arrow.color = c;
            }
        }
    }
}
