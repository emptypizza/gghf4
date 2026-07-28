// ============================================================
// Mental — 언어 상태 (원본 readLang / localStorage 등가)
// ============================================================
using UnityEngine;

namespace Mental
{
    public static class Loc
    {
        public const string PrefKey = "mental.lang";
        public static readonly string[] Supported = { "ko", "en", "ja" };

        public static string Lang
        {
            get
            {
                string v = PlayerPrefs.GetString(PrefKey, "");
                foreach (string s in Supported) if (s == v) return v;
                return "ko";
            }
            set
            {
                foreach (string s in Supported)
                    if (s == value) { PlayerPrefs.SetString(PrefKey, value); PlayerPrefs.Save(); return; }
            }
        }

        // 최초 실행 언어 게이트 (index.html 의 first-start language gate 등가)
        public static bool HasChosen => PlayerPrefs.HasKey(PrefKey);
    }
}
