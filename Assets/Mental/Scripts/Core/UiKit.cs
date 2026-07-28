// ============================================================
// Mental — 코드 생성 UGUI 팩토리
// 원본이 전부 절차적 드로잉(Canvas2D)이므로, 포팅도 씬 수작업 없이
// 코드로 UI를 구성한다 (씬에는 Canvas + GameRoot 만 존재).
// ============================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mental
{
    public static class UiKit
    {
        static Font _font;
        static readonly Dictionary<int, Sprite> _rounded = new Dictionary<int, Sprite>();

        // ---------- 폰트 ----------
        // WebGL/Android 등은 OS 동적 폰트에 CJK 가 없어 두부(□)가 된다.
        // Resources 임베드(Noto Sans KR)를 먼저 쓰고, 에디터/데스크톱은 OS 후보로 폴백.
        public static Font GetFont()
        {
            if (_font != null) return _font;
            GameConfig cfg = GameConfig.Instance;

            if (!string.IsNullOrEmpty(cfg.embeddedFontResource))
            {
                _font = Resources.Load<Font>(cfg.embeddedFontResource);
                if (_font != null) return _font;
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            string[] os = Font.GetOSInstalledFontNames();
            foreach (string want in cfg.fontCandidates)
            {
                foreach (string have in os)
                {
                    if (have == want)
                    {
                        _font = Font.CreateDynamicFontFromOSFont(want, cfg.baseFontSize);
                        if (_font != null) return _font;
                    }
                }
            }
            _font = Font.CreateDynamicFontFromOSFont(cfg.fontCandidates, cfg.baseFontSize);
            if (_font != null) return _font;
#endif
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }

        // 컬러 이모지는 유니티 동적 폰트에서 두부(□)가 되므로 텍스트 기호로 치환
        static readonly (string from, string to)[] Emoji =
        {
            ("⚖️","⚖"), ("🔨",""), ("🔍",""), ("📖",""), ("🗒️",""), ("🗒",""),
            ("📹",""), ("📁",""), ("📱",""), ("📋",""), ("🔑",""), ("🩺",""), ("💊",""), ("🗨","")
        };
        public static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            foreach ((string from, string to) in Emoji) s = s.Replace(from, to);
            return s.Trim();
        }

        // ---------- 라운드 9-슬라이스 스프라이트 ----------
        public static Sprite Rounded(int radius)
        {
            if (_rounded.TryGetValue(radius, out Sprite cached)) return cached;
            int size = radius * 2 + 8;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "MentalRounded" + radius;
            Color32 on = new Color32(255, 255, 255, 255);
            Color32 off = new Color32(255, 255, 255, 0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 네 모서리 원호 밖이면 투명
                    float dx = 0, dy = 0;
                    if (x < radius) dx = radius - x; else if (x >= size - radius) dx = x - (size - radius - 1);
                    if (y < radius) dy = radius - y; else if (y >= size - radius) dy = y - (size - radius - 1);
                    bool inside = (dx * dx + dy * dy) <= radius * radius;
                    tex.SetPixel(x, y, inside ? (Color)on : (Color)off);
                }
            }
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
            _rounded[radius] = sp;
            return sp;
        }

        // ---------- 방사형 비네트 스프라이트 (원본 createRadialGradient 등가) ----------
        // 알파만 그라데이션(중심 투명 → 가장자리 불투명)으로 담고, 색은 Image.color 로 입힌다.
        static Sprite _vignette;
        public static Sprite RadialVignette()
        {
            if (_vignette != null) return _vignette;
            GameConfig cfg = GameConfig.Instance;
            int w = 512, h = 288; // 기준 16:9 축소 해상도 (Simple 스트레치용)
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.name = "MentalVignette";
            float cx = cfg.refWidth * .5f, cy = cfg.refHeight * .5f;
            float inner = cfg.vignetteInnerRadius, span = Mathf.Max(1f, cfg.vignetteOuterRadius - inner);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 텍셀 → 기준 캔버스(1280×720) 좌표에서 중심 거리 → 원본과 같은 선형 알파
                    float px = (x + .5f) / w * cfg.refWidth, py = (y + .5f) / h * cfg.refHeight;
                    float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((d - inner) / span)));
                }
            }
            tex.Apply();
            _vignette = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            return _vignette;
        }

        // ---------- 기본 오브젝트 ----------
        public static RectTransform Node(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        // 가상 1280x720 좌표(좌상단 원점, 원본과 동일)를 UGUI 앵커 좌표로
        public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        // 기준 해상도(1280×720)와 화면 비율이 달라도 설계 좌표가 잘리지 않도록,
        // 부모(캔버스) 중앙에 고정 크기 레이아웃 박스를 만든다. 16:9 에서는 캔버스와 일치.
        public static RectTransform DesignBox(string name, Transform parent)
        {
            GameConfig cfg = GameConfig.Instance;
            RectTransform rt = Node(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(cfg.refWidth, cfg.refHeight);
            return rt;
        }

        public static Image Panel(Transform parent, float x, float y, float w, float h, Color col, int radius = 12, string name = "Panel")
        {
            RectTransform rt = Place(Node(name, parent), x, y, w, h);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded(radius);
            img.type = Image.Type.Sliced;
            img.color = col;
            img.raycastTarget = false;
            return img;
        }

        // 테두리 있는 패널: edge 패널 안에 base 패널
        public static Image BorderPanel(Transform parent, float x, float y, float w, float h, Color edge, Color fill, float border = 2.5f, int radius = 12, string name = "BorderPanel")
        {
            Image e = Panel(parent, x, y, w, h, edge, radius, name);
            Image f = Panel(e.transform, border, border, w - border * 2, h - border * 2, fill, Mathf.Max(2, radius - (int)border), name + "_fill");
            return e; // 자식 추가는 e.transform 에
        }

        public static Text Label(Transform parent, string text, float x, float y, float w, float h,
            int size, Color col, TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal, string name = "Label")
        {
            RectTransform rt = Place(Node(name, parent), x, y, w, h);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = GetFont();
            t.text = Sanitize(text);
            t.fontSize = size;
            t.color = col;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        // ---------- 버튼 (원본 btn 스타일: wood/gold/navy/crimson/ghost) ----------
        public struct BtnStyle { public Color fill, edge, text, disabled; }
        public static BtnStyle Style(string style)
        {
            GameConfig c = GameConfig.Instance;
            switch (style)
            {
                case "gold": return new BtnStyle { fill = c.C(c.gold), edge = c.C(c.goldHi), text = c.C("#2a2010"), disabled = c.C("#6a5a30") };
                case "crimson": return new BtnStyle { fill = c.C(c.crimson), edge = c.C(c.crimsonHi), text = c.C(c.white), disabled = c.C(c.crimson) * .55f };
                case "navy": return new BtnStyle { fill = c.C(c.navy), edge = c.C(c.navyHi), text = c.C(c.white), disabled = c.C(c.navy) * .55f };
                case "ghost": return new BtnStyle { fill = c.C("#282014", .5f), edge = c.C(c.gold, .4f), text = c.C(c.cream), disabled = c.C("#282014", .3f) };
                default: return new BtnStyle { fill = c.C(c.wood), edge = c.C(c.woodHi), text = c.C(c.white), disabled = c.C("#3a2c1a") };
            }
        }

        public class UiButton
        {
            public RectTransform rt;
            public Button button;
            public Image edge, fill;
            public Text label;
            public BtnStyle style;
            public bool Enabled
            {
                get => button.interactable;
                set { button.interactable = value; fill.color = value ? style.fill : style.disabled; label.color = value ? style.text : style.text * .6f; }
            }
            public void SetSelected(bool on)
            {
                edge.color = on ? GameConfig.Instance.C(GameConfig.Instance.goldHi) : style.edge;
                rt.localScale = on ? Vector3.one * 1.04f : Vector3.one;
            }
        }

        public static UiButton MakeButton(Transform parent, string label, float x, float y, float w, float h,
            string style, UnityEngine.Events.UnityAction onClick, int fontSize = 22, string name = "Button")
        {
            BtnStyle st = Style(style);
            RectTransform rt = Place(Node(name, parent), x, y, w, h);
            Image edge = rt.gameObject.AddComponent<Image>();
            edge.sprite = Rounded(10); edge.type = Image.Type.Sliced; edge.color = st.edge;
            edge.raycastTarget = true;

            Image fill = Panel(rt, 2.5f, 2.5f, w - 5, h - 5, st.fill, 8, "fill");

            Text t = Label(rt, label, 6, 4, w - 12, h - 8, fontSize, st.text, TextAnchor.MiddleCenter, FontStyle.Bold, "label");

            Button b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = edge;
            ColorBlock cb = b.colors;
            cb.highlightedColor = new Color(1.08f, 1.08f, 1.02f, 1f);
            cb.pressedColor = new Color(.85f, .85f, .85f, 1f);
            cb.disabledColor = new Color(.7f, .7f, .7f, .8f);
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(onClick);

            return new UiButton { rt = rt, button = b, edge = edge, fill = fill, label = t, style = st };
        }

        // ---------- 전체 화면 탭 캐처 (원본 sceneTap 등가) ----------
        public static Button TapCatcher(Transform parent, UnityEngine.Events.UnityAction onTap, string name = "TapCatcher")
        {
            RectTransform rt = Stretch(Node(name, parent));
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0); // 투명하지만 레이캐스트는 받음
            img.raycastTarget = true;
            Button b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = img;
            if (onTap != null) b.onClick.AddListener(onTap);
            return b;
        }

        // ---------- 배경 커버 핏 (원본 drawCover 등가, RawImage.uvRect 사용) ----------
        public static RawImage CoverImage(Transform parent, Texture2D tex, string name = "Cover")
        {
            RectTransform rt = Stretch(Node(name, parent));
            RawImage ri = rt.gameObject.AddComponent<RawImage>();
            ri.texture = tex;
            ri.raycastTarget = false;
            FitCover(ri, GameConfig.Instance.refWidth, GameConfig.Instance.refHeight);
            ri.gameObject.AddComponent<UiCoverFitter>(); // 실제 렉트 크기 기준으로 커버 핏 유지 (비-16:9 왜곡 방지)
            return ri;
        }

        public static void FitCover(RawImage ri, float dw, float dh)
        {
            if (ri.texture == null) return;
            float tw = ri.texture.width, th = ri.texture.height;
            float scale = Mathf.Max(dw / tw, dh / th);
            float uw = dw / (tw * scale), uh = dh / (th * scale);
            ri.uvRect = new Rect((1 - uw) * .5f, (1 - uh) * .5f, uw, uh);
        }

        // ---------- 초상 스프라이트 (좌상단 원점 크롭 → 유니티 rect 변환) ----------
        // (UiCoverFitter 는 파일 하단 — CoverImage 전용 보조 컴포넌트)
        public static Sprite CropSprite(Texture2D tex, Rect topLeftRect)
        {
            if (tex == null) return null;
            float ry = tex.height - topLeftRect.y - topLeftRect.height;
            Rect r = new Rect(topLeftRect.x, ry, topLeftRect.width, topLeftRect.height);
            r.x = Mathf.Clamp(r.x, 0, tex.width - 1);
            r.y = Mathf.Clamp(r.y, 0, tex.height - 1);
            r.width = Mathf.Min(r.width, tex.width - r.x);
            r.height = Mathf.Min(r.height, tex.height - r.y);
            return Sprite.Create(tex, r, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }

    // CoverImage 의 uvRect 를 실제 렉트 크기에 맞춰 갱신 — 화면 비율이 기준(16:9)과 다를 때
    // 배경이 늘어나 보이는 왜곡을 막는다 (레이아웃 변경 시 자동 재계산).
    public class UiCoverFitter : MonoBehaviour
    {
        RawImage _ri;

        void OnRectTransformDimensionsChange()
        {
            if (_ri == null) _ri = GetComponent<RawImage>();
            if (_ri == null) return;
            Rect r = ((RectTransform)transform).rect;
            if (r.width > 1 && r.height > 1) UiKit.FitCover(_ri, r.width, r.height);
        }
    }
}
