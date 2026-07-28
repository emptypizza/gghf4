// ============================================================
// Mental — Android 빌드 진입점 (에디터)
//   Mental.AndroidBuild.Configure   : Android 플레이어 설정 (패키지·가로 전용·IL2CPP/ARM64)
//   Mental.AndroidBuild.BatchBuild  : 설정 + APK 빌드 (배치용, 종료코드 0=성공/1=실패)
//     MENTAL_APK_OUT=<출력.apk> Unity -projectPath <proj> -batchmode -logFile - \
//       -buildTarget Android -executeMethod Mental.AndroidBuild.BatchBuild
//   메뉴 Mental/Build Android APK   : 에디터에서 수동 빌드 (Builds/Mental.apk)
// ============================================================
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mental
{
    public static class AndroidBuild
    {
        const string ScenePath = "Assets/Mental/Scenes/Main.unity";
        const string DefaultApkPath = "Builds/Mental.apk";

        [MenuItem("Mental/Configure Android Player (모바일 설정)")]
        public static void Configure()
        {
            PlayerSettings.companyName = "GGHF";
            PlayerSettings.productName = "Mental";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.gghf.mental");

            // 가상 1280×720 캔버스 기준 — 가로 전용
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // 64비트 전용 최신 기기 대응 — IL2CPP + ARM64
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            AssetDatabase.SaveAssets();
            Debug.Log("[Mental] Android 설정 완료 — com.gghf.mental / 가로 전용 / IL2CPP·ARM64");
        }

        static BuildReport BuildApk(string outPath)
        {
            Configure();
            return BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
        }

        // ---------- 배치(unitycli) 진입점 ----------
        public static void BatchBuild()
        {
            string outPath = Environment.GetEnvironmentVariable("MENTAL_APK_OUT");
            if (string.IsNullOrEmpty(outPath)) outPath = DefaultApkPath;

            BuildSummary s = BuildApk(outPath).summary;
            Debug.Log("[Mental] Android 빌드 " + s.result + " — " + outPath
                      + " (" + (s.totalSize / (1024 * 1024)) + "MB, 에러 " + s.totalErrors
                      + ", 경고 " + s.totalWarnings + ")");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Mental/Build Android APK")]
        public static void BuildFromMenu()
        {
            BuildSummary s = BuildApk(DefaultApkPath).summary;
            Debug.Log("[Mental] Android 빌드 " + s.result + " — " + DefaultApkPath);
        }
    }
}
