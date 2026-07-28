// ============================================================
// Mental — 에디터 부트스트랩
// 메뉴 한 번 / unitycli -executeMethod 한 번으로 씬·설정 에셋 구성.
//   Mental.MentalBootstrap.BatchSetup   : 씬 + 설정 에셋 생성 (배치용)
//   Mental.MentalBootstrap.CompileGate  : 컴파일 게이트 (배치 -quit 와 조합)
// ============================================================
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Mental
{
    public static class MentalBootstrap
    {
        const string ScenePath = "Assets/Mental/Scenes/Main.unity";
        const string ConfigPath = "Assets/Mental/Resources/MentalConfig.asset";

        [MenuItem("Mental/Setup Main Scene (씬 생성)")]
        public static void BuildMainScene()
        {
            EnsureConfig();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameConfig cfg = GameConfig.Instance;

            // 카메라 + 리스너
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cfg.C(cfg.bg1);
            cam.orthographic = true;
            camGo.AddComponent<AudioListener>();

            // 이벤트 시스템 (Input System 전용)
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif

            // 진입점
            new GameObject("GameRoot").AddComponent<GameRoot>();

            System.IO.Directory.CreateDirectory("Assets/Mental/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Mental] Main.unity 생성 완료 — Play 로 바로 검증 가능");
        }

        [MenuItem("Mental/Create Config Asset (튜닝 에셋)")]
        public static void EnsureConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) != null) return;
            System.IO.Directory.CreateDirectory("Assets/Mental/Resources");
            GameConfig cfg = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Mental] MentalConfig.asset 생성 — 수치 튜닝은 여기서");
        }

        // ---------- 배치(unitycli) 진입점 ----------
        public static void BatchSetup()
        {
            BuildMainScene();
            Debug.Log("[Mental] BatchSetup OK");
        }

        public static void CompileGate()
        {
            // -executeMethod 는 컴파일 성공 시에만 도달한다.
            Debug.Log("[Mental] CompileGate OK — 컴파일 에러 0");
        }
    }
}
