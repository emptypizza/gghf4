// ============================================================
// Mental — WebGL 빌드 진입점 (에디터)
//   Mental.WebGLBuild.Configure   : WebGL 플레이어 설정 (Gzip + decompression fallback)
//   Mental.WebGLBuild.BatchBuild  : 설정 + Builds/WebGL 빌드 (배치용, 종료코드 0=성공/1=실패)
//     MENTAL_WEBGL_OUT=<출력폴더> Unity -projectPath <proj> -batchmode -logFile - \
//       -buildTarget WebGL -executeMethod Mental.WebGLBuild.BatchBuild
//   메뉴 Mental/Build WebGL        : 에디터에서 수동 빌드 (Builds/WebGL)
// ============================================================
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mental
{
    public static class WebGLBuild
    {
        const string ScenePath = "Assets/Mental/Scenes/Main.unity";
        const string DefaultOutPath = "Builds/WebGL";

        [MenuItem("Mental/Configure WebGL Player (웹 설정)")]
        public static void Configure()
        {
            PlayerSettings.companyName = "GGHF";
            PlayerSettings.productName = "Mental";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.WebGL, "com.gghf.mental");

            // 정적 호스팅(GitHub Pages 등)에서도 .gz 가 깨지지 않도록 Gzip + 클라이언트 폴백
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.initialMemorySize = 64;
            PlayerSettings.WebGL.maximumMemorySize = 1024;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            // 가로 기준 캔버스(1280×720) — 기본 웹 해상도
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Mental] WebGL 설정 완료 — Gzip + decompressionFallback / 1280×720");
        }

        static BuildReport BuildWeb(string outPath)
        {
            Configure();
            if (Directory.Exists(outPath))
                Directory.Delete(outPath, true);
            Directory.CreateDirectory(outPath);

            return BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
        }

        // ---------- 배치(unitycli) 진입점 ----------
        public static void BatchBuild()
        {
            string outPath = Environment.GetEnvironmentVariable("MENTAL_WEBGL_OUT");
            if (string.IsNullOrEmpty(outPath)) outPath = DefaultOutPath;

            BuildSummary s = BuildWeb(outPath).summary;
            Debug.Log("[Mental] WebGL 빌드 " + s.result + " — " + outPath
                      + " (" + (s.totalSize / (1024 * 1024)) + "MB, 에러 " + s.totalErrors
                      + ", 경고 " + s.totalWarnings + ")");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Mental/Build WebGL")]
        public static void BuildFromMenu()
        {
            BuildSummary s = BuildWeb(DefaultOutPath).summary;
            Debug.Log("[Mental] WebGL 빌드 " + s.result + " — " + DefaultOutPath);
        }
    }
}
