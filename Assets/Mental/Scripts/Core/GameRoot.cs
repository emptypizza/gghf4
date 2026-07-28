// ============================================================
// Mental — 진입점. 씬에는 이 컴포넌트 하나만 있으면 된다.
// 카메라/리스너/캔버스/이벤트시스템이 없으면 만들어서라도 돈다.
// 허브 ↔ 법정(CASE 3·4) 화면 전환 담당.
// ============================================================
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Mental
{
    public class GameRoot : MonoBehaviour
    {
        public static CourtDb Db { get; private set; }
        public static SfxSynth Audio { get; private set; }

        // 어느 씬에서 Play 해도 부팅 (SampleScene 포함) — 씬 수작업 의존 제거
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindFirstObjectByType<GameRoot>() == null)
                new GameObject("GameRoot").AddComponent<GameRoot>();
        }

        Canvas _canvas;
        GameObject _current;

        void Awake()
        {
            GameConfig cfg = GameConfig.Instance;
            Db = CourtDb.Load();

            // 카메라 / 오디오 리스너 보장
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cfg.C(cfg.bg1);
            cam.orthographic = true;

            AudioListener listener = Object.FindFirstObjectByType<AudioListener>();
            if (listener == null) listener = cam.gameObject.AddComponent<AudioListener>();
            // 이 GO 에 AudioSource 를 추가하면 SfxSynth 의 OnAudioFilterRead 바인딩이 깨진다 (SfxSynth 헤더 참조).
            Audio = listener.gameObject.GetComponent<SfxSynth>();
            if (Audio == null) Audio = listener.gameObject.AddComponent<SfxSynth>();

            // 이벤트 시스템 보장 (Input System 전용 프로젝트)
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            // 캔버스 (가상 1280×720)
            GameObject cgo = new GameObject("MentalCanvas");
            _canvas = cgo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(cfg.refWidth, cfg.refHeight);
            // Expand: 캔버스가 항상 기준 해상도 이상 — 20:9 폰(갤럭시 등)에서 하단 UI 잘림 방지.
            // 콘텐츠는 UiKit.DesignBox(중앙 1280×720)에, 배경/FX는 풀블리드 레이어에 배치한다.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = .5f;
            cgo.AddComponent<GraphicRaycaster>();

            ShowHub();
        }

        void ClearCurrent()
        {
            if (_current != null) Destroy(_current);
            _current = null;
        }

        public void ShowHub()
        {
            ClearCurrent();
            HubScreen hub = HubScreen.Create(_canvas.transform, StartCase);
            _current = hub.gameObject;
        }

        public void StartCase(string caseMode)
        {
            if (Audio != null) Audio.UnlockAudio();
            ClearCurrent();
            CourtEngine court = CourtEngine.Create(_canvas.transform, caseMode, ShowHub);
            _current = court.gameObject;
        }
    }
}
