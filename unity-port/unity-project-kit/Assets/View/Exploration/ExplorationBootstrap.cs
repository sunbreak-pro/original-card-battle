// Starts the exploration screen: attach to an empty GameObject in an empty scene and press Play.
// It builds its own canvas and EventSystem, so no scene or prefab changes are needed. It decides
// nothing: the Inspector fields become a RunStart, clicks go to ExplorationSession, and every frame
// is ExplorationSession.Screen() handed to the view.
//
// #99: a combat node's battle is fought in the battle scene (Assets/Scenes/Battle.unity), loaded on
// top of this one and unloaded when it ends. This scene stays loaded underneath with its objects
// hidden, so the exploration keeps its state in memory and the battle scene stays as the Unity repo
// authored it. Expedition (pure C#) builds what goes in and takes what comes back; this class only
// loads, waits and unloads.
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using Depiction.Bridge;
using Depiction.View;
using DungeonCore;
using Exploration.Bridge;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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

        [Header("Battle (#99)")]
        [Tooltip("Fights each combat node in the battle scene, loaded on top of this one. Off: the stand-in card's 「勝ったことにする」 / 「倒れたことにする」.")]
        public bool realBattles = true;
        [Tooltip("The battle scene's path in the project. A player build needs it in Build Settings; the Editor loads it from the path.")]
        public string battleScenePath = "Assets/Scenes/Battle.unity";
        [Tooltip("Seconds the battle's result card stays on screen before the exploration comes back.")]
        public float resultSeconds = 2f;

        private Expedition _expedition;
        private ExplorationSession _session;
        private ExplorationScreenView _view;
        private GameObject _canvas;
        private bool _inBattle;
        // Set when a battle could not be shown: every combat node after it takes the stand-in card.
        // Kept apart from realBattles, which stays what the Inspector set.
        private bool _standInOnly;

        private void Start()
        {
            int used = randomSeed ? Random.Range(1, int.MaxValue) : Mathf.Max(0, seed);
            _expedition = new Expedition(new RunStart
            {
                Seed = (ulong)used,
                Loadout = trialLoadout ? RunStart.TrialLoadout() : RunLoadout.Empty,
                MiasmaPercent = startMiasma,
            });
            _session = _expedition.Session;

            EnsureEventSystem();
            _view = new ExplorationScreenView(BuildCanvas(), Step, Act, UseConsumable, Toggle, PickCard);
            Render();
            Debug.Log("[ExplorationBootstrap] seed " + used);
        }

        // J, C and Esc do what the band's three buttons do; Esc's order is the session's (§6.2).
        // Every frame the 瘴気 gauge and the shade move on (their rise and breath, #613).
        private void Update()
        {
            if (_view == null || _inBattle) return;
            _view.Tick();
            if (Pressed(KeyCode.J)) Toggle(Overlay.Journal);
            if (Pressed(KeyCode.C)) Toggle(Overlay.Cards);
            if (Pressed(KeyCode.Escape) && _session.Escape()) Render();
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

        private void Toggle(Overlay overlay)
        {
            if (_session.Toggle(overlay)) Render();
        }

        // #626: a card pressed on the pick screen goes to Expedition.Learn or Train, as CardPicks decides.
        private void PickCard(string cardId)
        {
            if (CardPicks.Choose(_expedition, cardId)) Render();
        }

        private void Render()
        {
            _view.Render(_session.Screen());
            _view.ShowPick(CardPicks.For(_expedition));
            if (realBattles && !_standInOnly && !_inBattle && _session.HasPendingBattle && _expedition.Battle == null) StartCoroutine(FightPendingBattle());
        }

        // ---- the battle scene (#99) ----

        /// <summary>
        /// Fights the pending node's encounter, one battle scene per fight, then brings the result back
        /// and shows the map again. A scene that cannot be loaded leaves the stand-in card's buttons.
        /// </summary>
        private IEnumerator FightPendingBattle()
        {
            EncounterBattle battle = _expedition.StartEncounter();
            if (battle == null) yield break;
            _inBattle = true;
            Scene home = gameObject.scene;

            while (!battle.Over)
            {
                CoreBattleSource source = battle.StartNext();
                var before = new HashSet<GameObject>(home.GetRootGameObjects());
                List<GameObject> hidden = HideHome(home);

                BattleBootstrap.HandIn(source);
                AsyncOperation load = LoadBattleScene();
                if (load == null)
                {
                    BattleBootstrap.HandIn(null);
                    Debug.LogError("[ExplorationBootstrap] the battle scene " + battleScenePath + " is not in Build Settings; the stand-in card stays.");
                    Show(hidden);
                    Debug.LogWarning("[ExplorationBootstrap] the battle scene could not be loaded: every combat node from here takes the stand-in card.");
                    _expedition.Abandon();
                    _inBattle = false;
                    _standInOnly = true;
                    Render();
                    yield break;
                }
                yield return load;

                Scene arena = SceneManager.GetSceneByPath(battleScenePath);
                if (arena.IsValid()) SceneManager.SetActiveScene(arena);
                DepictionPlayer player = FindPlayer(arena);
                if (!player) Debug.LogError("[ExplorationBootstrap] the battle scene has no DepictionPlayer; the fight is taken as it stands.");
                while (player && player.enabled && !player.Finished) yield return null;
                yield return new WaitForSeconds(resultSeconds);

                SceneManager.SetActiveScene(home);
                if (arena.IsValid()) yield return SceneManager.UnloadSceneAsync(arena);
                // Whatever the battle created in this scene (an EventSystem, while this one was hidden) goes with it.
                foreach (GameObject root in home.GetRootGameObjects())
                {
                    if (!before.Contains(root)) Destroy(root);
                }
                Show(hidden);

                if (!battle.TryFinish(source))
                {
                    // The battle never reached its end (no player to play it): the node waits on the stand-in card.
                    Debug.LogWarning("[ExplorationBootstrap] the battle ended before its result (no DepictionPlayer, or it was stopped or disabled): every combat node from here takes the stand-in card.");
                    _expedition.Abandon();
                    _inBattle = false;
                    _standInOnly = true;
                    Render();
                    yield break;
                }
            }

            BattleReturn back = _expedition.Return();
            Debug.Log("[ExplorationBootstrap] battle " + (back.Won ? "won" : "lost") + " / HP " + back.Hp + " / stamina " + back.Stamina
                + " / learned " + string.Join(", ", back.Learned)
                + (_expedition.Offer.Count > 0 ? " / to pick: " + string.Join(", ", _expedition.Offer) : "")
                + (back.Reward.Consumables.Count > 0 ? " / found: " + string.Join(", ", back.Reward.Consumables) : "")
                + (back.Reward.Pages > 0 ? " / pages +" + back.Reward.Pages : ""));
            _inBattle = false;
            Render();
        }

        private AsyncOperation LoadBattleScene()
        {
            var parameters = new LoadSceneParameters(LoadSceneMode.Additive);
            if (SceneUtility.GetBuildIndexByScenePath(battleScenePath) >= 0) return SceneManager.LoadSceneAsync(battleScenePath, parameters);
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(battleScenePath, parameters);
#else
            return null;
#endif
        }

        /// <summary>Hides every object of this scene but this one (the map's canvas, the camera, the EventSystem) while the battle shows.</summary>
        private List<GameObject> HideHome(Scene home)
        {
            var hidden = new List<GameObject>();
            foreach (GameObject root in home.GetRootGameObjects())
            {
                if (root == gameObject || !root.activeSelf) continue;
                root.SetActive(false);
                hidden.Add(root);
            }
            return hidden;
        }

        private static void Show(List<GameObject> hidden)
        {
            foreach (GameObject root in hidden)
            {
                if (root) root.SetActive(true);
            }
        }

        private static DepictionPlayer FindPlayer(Scene arena)
        {
            if (!arena.IsValid()) return null;
            foreach (GameObject root in arena.GetRootGameObjects())
            {
                DepictionPlayer player = root.GetComponentInChildren<DepictionPlayer>(true);
                if (player) return player;
            }
            return null;
        }

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

        private static bool Pressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return false;
            return key switch
            {
                KeyCode.J => keyboard.jKey.wasPressedThisFrame,
                KeyCode.C => keyboard.cKey.wasPressedThisFrame,
                KeyCode.Escape => keyboard.escapeKey.wasPressedThisFrame,
                _ => false,
            };
#else
            return Input.GetKeyDown(key);
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
