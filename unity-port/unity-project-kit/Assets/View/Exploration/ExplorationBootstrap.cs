// Starts the exploration screen: attach to an empty GameObject in an empty scene and press Play.
// It builds its own canvas and EventSystem, so no scene or prefab changes are needed. It decides
// nothing: the Inspector fields become a RunStart, clicks go to ExplorationSession, and every frame
// is ExplorationSession.Screen() handed to the view.
#if UNITY_2021_2_OR_NEWER
using DungeonCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Exploration.View
{
    public class ExplorationBootstrap : MonoBehaviour
    {
        [Header("Run")]
        [Tooltip("Fixes every layer's map. The same seed draws the same maps here and under dotnet test.")]
        public int seed = 1;
        [Tooltip("Draws a fresh seed on every Play and logs it, so a run worth replaying can be typed back in.")]
        public bool randomSeed;
        [Tooltip("Sets out with 防瘴の面 and two 浄化の香, the items the exploration core already honours.")]
        public bool trialLoadout = true;
        [Tooltip("The 瘴気 gauge the life starts at, to try the deep end of the gauge without walking there.")]
        [Range(0, 99)]
        public int startMiasma;

        private ExplorationSession _session;
        private ExplorationScreenView _view;
        private GameObject _canvas;

        private void Start()
        {
            int used = randomSeed ? Random.Range(1, int.MaxValue) : Mathf.Max(0, seed);
            _session = new ExplorationSession(new RunStart
            {
                Seed = (ulong)used,
                Loadout = trialLoadout ? RunStart.TrialLoadout() : RunLoadout.Empty,
                MiasmaPercent = startMiasma,
            });

            EnsureEventSystem();
            _view = new ExplorationScreenView(BuildCanvas(), Step, Act, UseConsumable);
            Render();
            Debug.Log("[ExplorationBootstrap] seed " + used);
        }

        private void Update()
        {
            if (_view != null && JournalKeyPressed()) _view.ToggleJournal();
        }

        private void OnDestroy()
        {
            if (_canvas) Destroy(_canvas);
        }

        private void Step(int nodeId, RestChoice choice)
        {
            if (_session.Step(nodeId, choice)) Render();
        }

        private void Act(ScreenAction action)
        {
            if (!_session.Act(action)) return;
            if (action == ScreenAction.NewLife) Debug.Log("[ExplorationBootstrap] new life " + _session.Life + " / seed " + _session.RunSeed);
            Render();
        }

        private void UseConsumable(int index)
        {
            if (_session.UseConsumable(index)) Render();
        }

        private void Render() => _view.Render(_session.Screen());

        private RectTransform BuildCanvas()
        {
            _canvas = new GameObject("ExplorationCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)_canvas.transform;
        }

        private static bool JournalKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.jKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.J);
#endif
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
#if ENABLE_INPUT_SYSTEM
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
#else
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
        }
    }
}
#endif
