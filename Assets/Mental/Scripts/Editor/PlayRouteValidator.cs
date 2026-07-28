// ============================================================
// Mental — Play 실측 게이트 진입점 (에디터)
// PORTING.md 「플레이 검증 체크리스트」를 QA/PlayRouteDriver 로 실주행한다.
//
// A) 배치 일회성 (에디터 프로세스 하나로 루트 1개):
//    MENTAL_ROUTE=win|lose|case4 MENTAL_QA_OUT=<결과.json> \
//    Unity -projectPath <proj> -batchmode -logFile - \
//          -executeMethod Mental.PlayRouteValidator.Run
//    (주의: -quit 금지 — 드라이버가 완주 후 종료코드 0/2 로 Exit 한다)
//
// B) unity-cli-bridge 상주 에디터에서 (에디터 재부팅 없이 루트 반복):
//    unity-cli execute --code "Mental.PlayRouteValidator.Prepare(\"win\", \"/tmp/win.json\");" --force
//    unity-cli play   → 완주 시 드라이버가 Play 를 스스로 멈춘다
// ============================================================
using System;
using UnityEditor;
using UnityEngine;

namespace Mental
{
    public static class PlayRouteValidator
    {
        // 루트 사전 상태 + 드라이버 지정. Play 는 걸지 않는다.
        public static void Prepare(string route, string outPath)
        {
            if (route != "win" && route != "lose" && route != "case4")
                throw new ArgumentException("route must be win|lose|case4: " + route);

            // 언어 사전 조건 — win: 언어 게이트부터 검증(초기화),
            // lose: ko 고정, case4: en 상태에서도 ko 로 나오는지 검증
            if (route == "win") PlayerPrefs.DeleteKey(Loc.PrefKey);
            else if (route == "case4") Loc.Lang = "en";
            else Loc.Lang = "ko";
            PlayerPrefs.Save();

            SessionState.SetString("mental.qa.route", route);
            SessionState.SetString("mental.qa.out", outPath ?? "");
            Debug.Log("[MentalQA] Prepare route=" + route + " out=" + outPath);
        }

        // 배치 일회성 진입점 (-executeMethod)
        public static void Run()
        {
            string route = Environment.GetEnvironmentVariable("MENTAL_ROUTE");
            string outPath = Environment.GetEnvironmentVariable("MENTAL_QA_OUT");
            if (string.IsNullOrEmpty(route)) route = "win";
            Prepare(route, outPath);
            SessionState.SetString("mental.qa.exitOnFinish", "1");
            EditorApplication.EnterPlaymode();
        }
    }
}
