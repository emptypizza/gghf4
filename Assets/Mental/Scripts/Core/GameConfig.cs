// ============================================================
// Mental — 튜닝 ScriptableObject (철칙: 수치는 코드에 박지 않는다)
// 기본값 = 원본 case3.html 의 확정값. 에셋(Resources/MentalConfig)
// 을 만들면 코드 수정 없이 튜닝 가능. 에셋이 없어도 기본값으로 돈다.
// ============================================================
using System;
using UnityEngine;

namespace Mental
{
    [Serializable]
    public class ScoreMood
    {
        public string name;
        public float step;      // 스텝 간격(초)
        public float root;      // 루트 주파수(Hz)
        public int[] bass;      // 반음 오프셋, REST(-999) = 쉼표
        public int[] lead;
        public int[] hit;       // 1 = 노이즈 틱
        public float level;     // 무드 볼륨
        public const int REST = -999;
        public ScoreMood() { } // Unity 직렬화용
        public ScoreMood(string n, float st, float r, int[] b, int[] l, int[] h, float lv)
        { name = n; step = st; root = r; bass = b; lead = l; hit = h; level = lv; }
    }

    [CreateAssetMenu(fileName = "MentalConfig", menuName = "Mental/Game Config")]
    public class GameConfig : ScriptableObject
    {
        const int R = ScoreMood.REST;

        [Header("화면")]
        public int refWidth = 1280;
        public int refHeight = 720;

        [Header("대화")]
        public float typeSpeed = 44f;          // 원본 Dlg.speed

        [Header("심리(법정) 튜닝 — 원본 H 상태값")]
        public int trustStart = 5;             // 재판 신뢰도
        public float orderStart = 100f;        // 법정 질서
        public float orderDrainPerSec = 0.9f;  // H.drain
        public float objectionOrderDrop = 55f; // 이의 발생 시 order-55
        public float objectionOrderFloor = 8f; // max(8, ...)
        public float firstMissOrderPenalty = 8f;
        public float missOrderPenalty = 14f;
        public int missTrustPenalty = 1;
        public float orderZeroReset = 45f;     // 질서 0 → 소란 후 45로 리셋 + 신뢰도 -1
        public float gavelRecover = 26f;       // 수동 정숙 회복량
        public float gavelRecoverThreshold = 95f;
        public float hintDuration = 3.2f;
        public float bannerDuration = 1.3f;
        public float burstLife = 1.15f;
        public float vignetteInnerRadius = 240f; // 긴장 비네트 radial gradient 내반경 (원본 createRadialGradient, 1280×720 px)
        public float vignetteOuterRadius = 760f; // 외반경 — 중심은 투명, 가장자리로 갈수록 붉게
        public string vignetteColor = "#961414"; // 원본 rgba(150,20,20)

        [Header("엔딩 연출")]
        public int slamCount = 3;
        public float slamStartDelay = 0.3f;
        public float slamInterval = 0.52f;
        public float endSeqDelay = 0.5f;

        [Header("저울")]
        public float scaleTiltDeg = 12f;       // 한쪽 기울기 최대 각
        public float scaleLerpSpeed = 6f;

        [Header("오디오")]
        public float masterVolume = 0.5f;
        public float scoreVolume = 0.15f;      // 절차 스코어 버스
        public float sfxVolume = 1f;

        [Header("스코어 무드 (원본 SCORE 테이블)")]
        public ScoreMood[] scoreMoods = new ScoreMood[]
        {
            new ScoreMood("title",     .24f, 110f,    new[]{0,R,0,R,-5,R,-7,R},   new[]{12,R,7,R,10,R,5,R},    new[]{1,0,0,0,1,0,0,0}, .48f),
            new ScoreMood("opening",   .21f, 123.47f, new[]{0,R,-3,R,-5,R,-7,R},  new[]{7,10,12,R,5,8,10,R},   new[]{1,0,0,1,0,0,1,0}, .42f),
            new ScoreMood("hearing",   .155f,123.47f, new[]{0,R,0,-5,0,R,-3,-7},  new[]{12,7,10,5,12,8,10,7},  new[]{1,0,1,0,1,0,1,0}, .58f),
            new ScoreMood("objection", .115f,110f,    new[]{0,0,-2,0,-5,0,-2,0},  new[]{12,10,7,12,14,10,7,5}, new[]{1,1,0,1,1,0,1,0}, .66f),
            new ScoreMood("verdict",   .18f, 130.81f, new[]{0,R,-5,R,-3,R,-7,R},  new[]{12,9,7,10,14,10,9,7},  new[]{1,0,0,1,1,0,0,1}, .50f),
            new ScoreMood("ending",    .20f, 146.83f, new[]{0,R,-5,R,-7,R,-3,R},  new[]{12,16,19,14,17,21,19,24},new[]{1,0,1,0,1,0,1,0},.54f),
            new ScoreMood("mistrial",  .28f, 98f,     new[]{0,R,-2,R,-5,R,-7,R},  new[]{7,R,5,R,3,R,0,R},      new[]{1,0,0,0,1,0,0,0}, .38f),
        };

        [Header("팔레트 (원본 C — hex)")]
        public string bg0 = "#241a12";
        public string bg1 = "#12100c";
        public string wood = "#5b4026";
        public string woodHi = "#7c5836";
        public string woodDk = "#3a2915";
        public string gold = "#d8b24a";
        public string goldHi = "#f4dd90";
        public string goldDk = "#8a6a20";
        public string cream = "#f4ead2";
        public string ink = "#171c2c";
        public string purple = "#4a3568";
        public string purpleHi = "#8163b4";
        public string banner = "#3b2a55";
        public string crimson = "#7c2333";
        public string crimsonHi = "#d24455";
        public string navy = "#25406e";
        public string navyHi = "#5183d0";
        public string white = "#f7f3ea";
        public string paper = "#efe4c8";
        public string green = "#43c47a";
        public string red = "#e64a54";
        public string amber = "#e6a43a";

        [Header("초상 크롭 (title_art 기준, 좌상단 원점 px)")]
        public Rect judgePhoto = new Rect(842, 118, 452, 470);
        public Rect mayoiPhoto = new Rect(296, 118, 410, 452);

        [Header("폰트")]
        // WebGL/모바일은 OS 폰트 불가 — Resources 임베드 우선 (Noto Sans KR Subset, OFL)
        public string embeddedFontResource = "MentalFonts/NotoSansKR-Regular";
        public string[] fontCandidates = new[]
        {
            "Apple SD Gothic Neo", "AppleGothic", "Malgun Gothic",
            "Noto Sans CJK KR", "Noto Sans KR", "Hiragino Sans", "Arial"
        };
        public int baseFontSize = 25;

        // ---------- 헬퍼 ----------
        public Color C(string hex, float a = 1f)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color c)) { c.a = a; return c; }
            return Color.magenta;
        }

        public ScoreMood Mood(string name)
        {
            foreach (ScoreMood m in scoreMoods) if (m.name == name) return m;
            return scoreMoods.Length > 0 ? scoreMoods[0] : null;
        }

        static GameConfig _inst;
        public static GameConfig Instance
        {
            get
            {
                if (_inst == null)
                {
                    _inst = Resources.Load<GameConfig>("MentalConfig");
                    if (_inst == null) _inst = CreateInstance<GameConfig>(); // 에셋 없어도 기본값으로 그린 유지
                }
                return _inst;
            }
        }
    }
}
