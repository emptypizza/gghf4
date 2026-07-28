// ============================================================
// Mental — CASE 3·4 공정재판 데이터 모델
// 원본: mental00/case3.html 의 I18N / CASE4_KO / COURT_COPY /
//       DEDUCTION / VERDICT_FACTS 를 node 추출기로 변환한
//       Resources/MentalData/court.json 을 로드한다.
// 텍스트·케이스 내용 수정은 court.json 에서 (코드 수정 금지).
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mental
{
    [Serializable] public class CourtLine { public string who; public string expr; public string text; }
    [Serializable] public class CourtStatement { public string who; public string expr; public string text; public string key; public string over; }
    [Serializable] public class CourtNames { public string judge; public string mayoi; public string pros; public string def; public string accused; }
    [Serializable] public class CourtRoles { public string judge; public string mayoi; }
    [Serializable] public class CourtScaleText { public string pros; public string def; public string neutral; public string neutralOn; }
    [Serializable] public class CourtTitleText { public string start; public string note; public string main; public string sub; public string tag; }

    [Serializable] public class CourtHearingText
    {
        public string prosBanner, defBanner, prosTitle, defTitle, testifying;
        public string prev, next, record, gavel, verdict;
        public string prosHint, defHint, tooEarly;
        public string trust, order, overmark;
        public string recordTitle, present, close;
        public string accepted, rejected, objection, orderBurst, noisy;
        public string admittedPros, judgePros, admittedDef, judgeDef;
        public string defenseRetort, prosecutorRetort;
        public string gavelPrompt, chartHint, wrongEvidence;
        public string nextDefense, readyVerdict, mistrial;
    }

    [Serializable] public class VerdictFeedback { public string full, none, disp, dispPunish, dispNone, ok; }

    [Serializable] public class CourtVerdictText
    {
        public string intro, chooseBoth, reconsider, heading, sub;
        public string capTitle, dispTitle, confirm;
        public string capFull, capPartial, capNone;
        public string dispPunish, dispBoth, dispNone;
        public VerdictFeedback feedback;
    }

    [Serializable] public class CourtEndingText
    {
        public string bang, bangLast;
        public List<CourtLine> seq;
        public string retry, title, clear, nextCase;
    }

    [Serializable] public class CourtGameoverText { public string retry, title, head, sub, hint; }
    [Serializable] public class CourtEvidence { public string id; public string name; public string icon; public string col; public string desc; }

    [Serializable] public class CourtLocale
    {
        public string lang;
        public string caseMode;
        public string docTitle, main, hint, loading;
        public CourtNames names;
        public CourtRoles roles;
        public CourtScaleText scale;
        public CourtTitleText title;
        public List<CourtLine> open;
        public CourtHearingText hearing;
        public CourtVerdictText verdict;
        public CourtEndingText ending;
        public CourtGameoverText gameover;
        public List<CourtEvidence> evidence;
        public List<CourtStatement> pros;
        public List<CourtStatement> def;
    }

    [Serializable] public class CourtCopy
    {
        public string lang;
        public string claim, direct, near, other, firstMiss, facts, gavel, evidenceFail, orderFail;
    }

    [Serializable] public class DeductionNear { public string evidenceId; public string ko; public string en; public string ja; }
    [Serializable] public class DeductionRule
    {
        public string caseMode;
        public string side;            // "pros" | "def"
        public DeductionNear claim;    // evidenceId 미사용 — ko/en/ja 만 (반박할 주장 문구)
        public List<DeductionNear> near;
    }

    [Serializable] public class VerdictFact { public string icon; public string ko; public string en; public string ja; }
    [Serializable] public class VerdictFactSet { public string caseMode; public List<VerdictFact> facts; }

    [Serializable] public class CourtDb
    {
        public List<CourtLocale> locales;
        public List<CourtCopy> courtCopy;
        public List<DeductionRule> deduction;
        public List<VerdictFactSet> verdictFacts;

        // ---------- 로드 ----------
        public static CourtDb Load()
        {
            TextAsset ta = Resources.Load<TextAsset>("MentalData/court");
            if (ta == null)
            {
                Debug.LogError("[Mental] MentalData/court.json 이 Resources 에 없습니다.");
                return new CourtDb { locales = new List<CourtLocale>(), courtCopy = new List<CourtCopy>(), deduction = new List<DeductionRule>(), verdictFacts = new List<VerdictFactSet>() };
            }
            CourtDb db = JsonUtility.FromJson<CourtDb>(ta.text);
            return db;
        }

        // ---------- 조회 (원본 activeLocale / localText / courtCopy 의 등가) ----------
        // CASE 4 는 원본과 동일하게 항상 ko 로 고정된다.
        public static string ActiveLang(string caseMode, string lang) => caseMode == "4" ? "ko" : lang;

        public CourtLocale GetLocale(string caseMode, string lang)
        {
            string want = ActiveLang(caseMode, lang);
            CourtLocale koFallback = null;
            foreach (CourtLocale l in locales)
            {
                if (l.caseMode != caseMode) continue;
                if (l.lang == want) return l;
                if (l.lang == "ko") koFallback = l;
            }
            if (koFallback != null) return koFallback;
            // caseMode 자체가 없으면 3 으로
            return caseMode != "3" ? GetLocale("3", lang) : (locales.Count > 0 ? locales[0] : null);
        }

        public CourtCopy GetCopy(string caseMode, string lang)
        {
            string want = ActiveLang(caseMode, lang);
            CourtCopy ko = null;
            foreach (CourtCopy c in courtCopy)
            {
                if (c.lang == want) return c;
                if (c.lang == "ko") ko = c;
            }
            return ko;
        }

        public DeductionRule GetDeduction(string caseMode, string side)
        {
            foreach (DeductionRule d in deduction)
                if (d.caseMode == caseMode && d.side == side) return d;
            return null;
        }

        public static string LocText(DeductionNear t, string activeLang)
        {
            if (t == null) return "";
            switch (activeLang)
            {
                case "en": return string.IsNullOrEmpty(t.en) ? t.ko : t.en;
                case "ja": return string.IsNullOrEmpty(t.ja) ? t.ko : t.ja;
                default: return t.ko;
            }
        }

        public static string LocText(VerdictFact t, string activeLang)
        {
            if (t == null) return "";
            switch (activeLang)
            {
                case "en": return string.IsNullOrEmpty(t.en) ? t.ko : t.en;
                case "ja": return string.IsNullOrEmpty(t.ja) ? t.ko : t.ja;
                default: return t.ko;
            }
        }

        public List<VerdictFact> GetVerdictFacts(string caseMode)
        {
            VerdictFactSet fallback = null;
            foreach (VerdictFactSet v in verdictFacts)
            {
                if (v.caseMode == caseMode) return v.facts;
                if (v.caseMode == "3") fallback = v;
            }
            return fallback != null ? fallback.facts : new List<VerdictFact>();
        }
    }

    // ---------- 판결 평가 (원본 evalVerdict 의 1:1 포팅 — 순수함수) ----------
    public enum VerdictLean { Center, Pros, Def, Disp }
    public struct VerdictResult { public bool ok; public VerdictLean lean; public string msg; }

    public static class VerdictLogic
    {
        // capacity: "full" | "partial" | "none",  disp: "punish" | "both" | "none"
        public static VerdictResult Eval(string capacity, string disp, CourtVerdictText t)
        {
            VerdictFeedback fb = t.feedback;
            if (capacity == "full") return new VerdictResult { ok = false, lean = VerdictLean.Pros, msg = fb.full };
            if (capacity == "none") return new VerdictResult { ok = false, lean = VerdictLean.Def, msg = fb.none };
            if (disp == "punish") return new VerdictResult { ok = false, lean = VerdictLean.Disp, msg = string.IsNullOrEmpty(fb.dispPunish) ? fb.disp : fb.dispPunish };
            if (disp == "none") return new VerdictResult { ok = false, lean = VerdictLean.Disp, msg = string.IsNullOrEmpty(fb.dispNone) ? fb.disp : fb.dispNone };
            if (disp != "both") return new VerdictResult { ok = false, lean = VerdictLean.Disp, msg = fb.disp };
            return new VerdictResult { ok = true, lean = VerdictLean.Center, msg = fb.ok };
        }

        // 원본 checkPresent — 증거 id 가 진술의 key 와 일치해야 직접 반박
        public static bool CheckPresent(string evidenceId, CourtStatement stmt)
            => stmt != null && !string.IsNullOrEmpty(stmt.key) && stmt.key == evidenceId;

        // 원본 evidenceRelation — direct / near / other
        public static string Relation(CourtDb db, string caseMode, string side, CourtStatement stmt, string evidenceId)
        {
            if (CheckPresent(evidenceId, stmt)) return "direct";
            if (stmt == null || string.IsNullOrEmpty(stmt.over)) return "other";
            DeductionRule rule = db.GetDeduction(caseMode, side);
            if (rule != null && rule.near != null)
                foreach (DeductionNear n in rule.near)
                    if (n.evidenceId == evidenceId) return "near";
            return "other";
        }

        public static string NearMissHint(CourtDb db, string caseMode, string side, string evidenceId, string activeLang)
        {
            DeductionRule rule = db.GetDeduction(caseMode, side);
            if (rule == null || rule.near == null) return "";
            foreach (DeductionNear n in rule.near)
                if (n.evidenceId == evidenceId) return CourtDb.LocText(n, activeLang);
            return "";
        }
    }
}
