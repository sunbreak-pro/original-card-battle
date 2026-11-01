// Plays whatever an IDepictionSource hands it: the battle the BattleCore rules run (a bridge source
// given through UseSource / Restart), or the fixed script kept for filming (scriptedPlayback). With
// neither it logs an error and stops (#571). Some events wait for the player; the rest run on their own. This
// class computes no rule: every number, label, verdict and line of text it shows comes from the
// source (DepictionFrame / DepictionEvent / Cue / PlayVerdict). Positions come from the scene
// objects at run time, so prefab instances can be moved in the Editor freely. How long an event
// holds the flow is EffectPlan's (Script/Effects.cs, #495): HoldBeat waits each step the plan lists, so
// the EditMode tests that sum the plan measure the screen's own waits.
//
// Several enemies (battle_ui_ux_v2 §2.2, §3.9, §5.15, #589). Every enemy has its parts on the screen: a
// figure, a number panel, an omen badge and a home (EnemyParts, index k matching DepictionFrame.Enemies[k]).
// Enemy 0 wears the scene's own instances. The scene belongs to the Unity project, so the others are copies
// made at run time from templates taken before anything has laid itself out (Awake). A cue goes to the enemy
// it names, and which enemy that is, like every layout, is worked out in Script/ (EnemyScreen, OmenRowLayout,
// StatusPanelLayout, ReceiverDrop). With one enemy no copy is made and every path is as it was.
//
// The class is split by job (#585). This file: the Inspector's fields and the battle's start, restart and
// speed. .Layout.cs: the enemies' parts, the parts made at run time, keeping them beside the figures.
// .Flow.cs: playing the events one by one, cue by cue. .Beats.cs: what a cue draws. .Cards.cs: the cards
// that fly, and how long each effect holds the flow. .Hand.cs: a frame on the screen, the hand, hover and
// the panels around it. .Drag.cs: holding and releasing a card.
#if UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Depiction.View
{
    // Before the views' Awakes: the templates of a second enemy's parts are copies of parts that have not
    // yet laid themselves out (TakeEnemyTemplates).
    [DefaultExecutionOrder(-200)]
    public partial class DepictionPlayer : MonoBehaviour
    {
        [Header("Prefabs")]
        public CardView cardPrefab;

        [Header("Scene instances")]
        public Image backdrop;
        public CornerInfoView cornerInfo;
        public FigureView playerFigure;
        public FigureView enemyFigure;
        public StatusBarView playerStatus;
        public StatusBarView enemyStatus;
        public OmenBadgeView omenBadge;
        public ReceiverView receiver;
        public ThrowLineView throwLine;
        public RectTransform handArea;
        public RectTransform deckPoint;
        public RectTransform discardPoint;
        public RectTransform endTurn;
        public Image stanceHintIcon;
        public Text stanceHintText;
        public RectTransform fxLayer;
        [Tooltip("One line above the hand: the card the script plays next, or why a released card came back.")]
        public Text handGuide;

        [Header("Hand layout")]
        [Tooltip("Distance between neighbouring cards for a hand of five or fewer. Six, seven and eight close in to 150 / 140 / 126 of 160 (battle-visual-v1 §4.8). Below the card width (216) the cards overlap like a spread bundle.")]
        public float cardSpacing = 160f;
        [Tooltip("The hand is a shallow fan: each step away from the middle tilts a card by this many degrees, about the middle of its bottom edge (battle-visual-v1 §4.8).")]
        [Range(0f, 15f)]
        public float fanDegreesPerCard = 2.5f;

        [Header("Guide")]
        [Tooltip("How long the reason a card came back stays on the guide line.")]
        public float refusalSeconds = RefusalLineLayout.Seconds; // §5.4: 3.5 s

        [Header("Effects (#75-#77)")]
        [Tooltip("Effects to switch off, by id (EffectId: CardDraw, HandFan, CardHover, CardGrab, ReceiverShow, "
                 + "ThrowLineShow, ReceiverSnap, CardRelease, CardToDiscard, CardReturn, RefusalShake, HandDiscard, "
                 + "UnpayableDim). A switched-off effect settles at once and the battle goes on.")]
        public string[] effectsOff = new string[0];
        [Tooltip("Writes every effect that played (id, event, start, measured and nominal ms) to the log once the source finishes. For #78.")]
        public bool logEffectTrace;

        [Header("Speed (#348)")]
        [Tooltip("Starts at the speed saved by the last click (PlayerPrefs). Off: starts at 1.25x, the speed the 2.0 s cap is measured at (PlayMode tests switch it off). "
                 + "Filming (scriptedPlayback, or a debugCaptureDir) always starts at 1.25x.")]
        public bool useSavedSpeed = true;
        [Tooltip("Shows the provisional speed switch in the top-right corner while a battle runs. Never shown when filming (scriptedPlayback, or a debugCaptureDir).")]
        public bool showSpeedButton = true;

        [Header("Mode")]
        [Tooltip("Filming only: replays TurnSliceScript in its written order, refusing any other card. "
                 + "A played battle leaves it off and gets its source from BattleBootstrap or the demo flow; "
                 + "with neither, the player stops with an error.")]
        public bool scriptedPlayback;

        [Header("Debug")]
        [Tooltip("Performs the source's suggested drags by itself. A fixed script suggests its next card; "
                 + "a core battle started with auto play suggests the leftmost card it can pay for.")]
        public bool autoPlayDrags;
        [Tooltip("With Auto Play Drags: ends the turn by itself once the source suggests no card. For unattended runs.")]
        public bool autoEndTurn;
        [Tooltip("Freezes at named gates (hover / impact / end) until DebugStep() is called (the component's ⋮ menu: Debug Step). For screenshots.")]
        public bool debugStepMode;
        [Tooltip("Folder (absolute, or relative to the project) that receives one PNG per gate. Empty = no capture.")]
        public string debugCaptureDir = "";

        /// <summary>Name of the gate the playback is frozen at ("3-impact"), or empty.</summary>
        public string DebugGate { get; private set; } = "";
        public bool Finished { get; private set; }
        /// <summary>Seconds each event took from its start (or from the confirmed drop) to its settled frame.</summary>
        public readonly List<float> EventSeconds = new List<float>();
        /// <summary>Every effect that played or was switched off, with its measured length (#78 reads it).</summary>
        public EffectTrace Trace { get; private set; }
        /// <summary>
        /// The battle speed in force (#348). Kept across <see cref="Restart"/>. Nothing in the code reads
        /// it: the PlayMode test DemoFlowPlaybackTests reads it by reflection to check the speed switch,
        /// so keep the name.
        /// </summary>
        public BattleSpeedStep Speed => _speed;

        /// <summary>Where the speed the player last picked is kept between launches.</summary>
        public const string SavedSpeedKey = "Depiction.BattleSpeed";

        private IDepictionSource _source;
        private IDepictionSource _givenSource;
        /// <summary>Start has run (the screen is set up); a <see cref="Restart"/> before it only hands the source over.</summary>
        private bool _started;
        /// <summary><see cref="Hold"/>: Start waits for a battle instead of building its own.</summary>
        private bool _hold;
        /// <summary><see cref="Halt"/>: the battle takes no input and pulls no event until the next <see cref="Restart"/>.</summary>
        private bool _halted;
        private readonly List<CardView> _hand = new List<CardView>();
        private bool _busy;
        private bool _stepRequested;
        private CardView _dragging;
        private Vector3 _dragHome;
        private Vector3 _grabOffset;
        private CardView _hovered;
        private readonly Dictionary<CardView, float> _hoverWeight = new Dictionary<CardView, float>();
        private Coroutine _refusal;
        private EffectSwitches _effects = EffectSwitches.AllOn();
        private Coroutine _zoneFade;
        private int _hoverHandle = -1;
        private Vector3 _dragPointerWorld;
        private bool _zoneHot;
        private float _snapWeight;
        private int _snapHandle = -1;
        private BattleSpeedStep _speed = BattleSpeed.Default;
        private BattleSpeedButton _speedButton;

        /// <summary>How far a held card is drawn toward the dish once it is over it (EffectId.ReceiverSnap).</summary>
        private const float SnapPull = 0.35f;
        private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);
        private GameObject _resultCard;

        // ---- battle_ui_ux_v2 §2.2 (#589): one enemy's parts --------------------------------------------

        /// <summary>One enemy's parts on the screen: enemy k of DepictionFrame.Enemies is the k-th.</summary>
        private sealed class EnemyParts
        {
            public FigureView Figure;
            public StatusBarView Status;
            public OmenBadgeView Badge;
            /// <summary>Where the figure stands between its moves; every motion returns it here.</summary>
            public Vector2 Home;
            /// <summary>The panel's left edge on the 1920 screen now, eased toward StatusPanelLayout's.</summary>
            public float PanelLeft;
            /// <summary>§5.8: how far the badge stands off the head now, eased toward OmenRowLayout's.</summary>
            public float BadgeSlide;
        }

        /// <summary>The parts of every enemy made so far. The first is the scene's own; a battle with fewer enemies hides the rest.</summary>
        private readonly List<EnemyParts> _enemies = new List<EnemyParts>();
        /// <summary>How many of <see cref="_enemies"/> the battle on screen uses, at least one.</summary>
        private int _enemyCount = 1;
        /// <summary>Copies of the scene's parts, taken before their Awakes and kept under an inactive holder, so a further enemy's parts lay themselves out as the first's did.</summary>
        private FigureView _figureTemplate;
        private StatusBarView _statusTemplate;
        private OmenBadgeView _badgeTemplate;
        /// <summary>§3.9: the enemy the held card is over now (the one a release plays it on), or −1.</summary>
        private int _pickedUnit = -1;
        /// <summary>The enemy the receiver stands over: the last one the held card was over, or the one it would go to unnamed; −1 is the focus enemy.</summary>
        private int _aimedUnit = -1;
        /// <summary>The next placing puts the badges straight on their places (a battle's first frame).</summary>
        private bool _snapBadges = true;

        // ---- battle-visual-v1 §4.2 / §4.7 (#242): parts made at run time --------------------------
        /// <summary>The floor of cells under the figures; drawn when the script gives one (DepictionFrame.Floor).</summary>
        private FloorView _floor;
        /// <summary>The player's stamina tag above the piles.</summary>
        private StaminaTagView _staminaTag;
        /// <summary>The card whose reach the floor lights now, or null.</summary>
        private CardView _reachFor;
        /// <summary>battle-visual-v1 §4.7 (#455): the draw, discard and exile piles under the stamina tag.</summary>
        private PilesView _piles;
        /// <summary>#606: the layered backdrop (battle-visual-v1 §2.4) this battle hands its layer to, once found.</summary>
        private Backdrop.View.BackdropView _backdropView;
        /// <summary>battle_ui_ux_v2 §1.1 C4 (#455): the drawer a click on a pile opens with its list.</summary>
        private PileListView _pileList;
        /// <summary>battle-visual-v1 §4.6 (#455): each side's stance list at its edge.</summary>
        private StanceListView _stanceLists;
        /// <summary>battle-visual-v1 §4.9 (#455): the four frames of the attributes played this turn.</summary>
        private PlayedAttributesView _played;
        /// <summary>battle-visual-v1 §4.9 (#455): the 構えの予告 at x 1560, y 984.</summary>
        private ReserveHintView _reserveHint;
        /// <summary>battle-visual-v1 §5.4 (#455): the one line saying why a released card went back.</summary>
        private RefusalLineView _refusalLine;
        /// <summary>battle-visual-v1 §5.5 (#455): the black gradient below y 790 on the enemy's turn.</summary>
        private EnemyTurnShadeView _enemyShade;
        /// <summary>The 構えの予告 of the frame on screen, which a held card's own hint stands in for.</summary>
        private string _frameStanceHint = "";

        // ---- battle-visual-v1 §4.5 (#455): the number panels under the figures ------------------------
        /// <summary>Where the player figure stands between its motions (its cell or slot); a lunge does not move it.</summary>
        private Vector3 _playerHome;
        private bool _playerHomeSet;
        /// <summary>The panels' left edges on the 1920 screen now, eased toward StatusPanelLayout's.</summary>
        private float _playerPanelLeft;
        /// <summary>The next placing puts the panels straight on their places (a battle's first frame).</summary>
        private bool _snapPanels = true;
        /// <summary>How fast a panel closes on its place, per second: a 320 ms step is about done when the figure lands.</summary>
        private const float PanelFollowRate = 14f;

        /// <summary>
        /// Hands the player the source to play instead of the two it can build by itself. Call it
        /// from Awake (BattleBootstrap does): Start reads it once.
        /// </summary>
        public void UseSource(IDepictionSource source)
        {
            _givenSource = source;
            _hold = false;
        }

        /// <summary>
        /// The demo flow (#190): start with no battle at all. Start sets the screen up and waits for
        /// <see cref="Restart"/> instead of stopping for want of a source. Call it from
        /// Awake, like <see cref="UseSource"/>.
        /// </summary>
        public void Hold()
        {
            _hold = true;
        }

        /// <summary>
        /// Plays a new battle from its opening frame (#190: the deck screen's 「戦闘へ」, #191's next
        /// battle). Whatever the last one left — its coroutines, its hand, its result card — goes first.
        /// </summary>
        public void Restart(IDepictionSource source)
        {
            if (source == null) throw new System.ArgumentNullException(nameof(source));
            if (!_started)
            {
                _givenSource = source;
                _hold = false;
                return;
            }
            StopAllCoroutines();
            _halted = false;
            // A gate stopped mid-way by StopAllCoroutines must not leave the tweens frozen at 0. The
            // battle speed carries over from the last battle.
            DebugGate = "";
            ApplyTweenSpeed();
            ShowSpeedButton(true);
            _busy = false;
            _dragging = null;
            _hovered = null;
            _refusal = null;
            _refusalLine?.Hide();
            // A card held or hovered when the last battle stopped may have left these up.
            _staminaTag?.ShowCost(0);
            HideHoverPanels();
            HideTargetMarks();
            ShowHpLoss(null);
            SetActingExcept(-1); // a battle stopped on the enemy's turn
            _pickedUnit = -1;
            _aimedUnit = -1;
            Finished = false;
            EventSeconds.Clear();
            if (_resultCard)
            {
                Destroy(_resultCard);
                _resultCard = null;
            }
            ClearHand();
            SnapEnemiesHome();
            // The last battle's effects were stopped mid-way: no flash, tilt or stretch is left over.
            if (playerFigure) playerFigure.Settle();
            foreach (EnemyParts foe in _enemies)
            {
                if (foe.Figure) foe.Figure.Settle();
            }
            _hoverHandle = -1;
            _snapHandle = -1;
            _planned.Clear(); // the last battle's event was stopped mid-way: its plan goes with it
            _plannedEvent = null;
            Trace = new EffectTrace(() => Time.unscaledTimeAsDouble) { Speed = _speed };
            _source = source;
            _snapPanels = true;
            _snapBadges = true;
            _stanceLists?.ResetOrder();
            EnsureEnemies(EnemyScreen.CountOf(_source.Frame));
            SnapStanceGlows(_source.Frame);
            ApplyFrame(_source.Frame);
            SnapEnemiesHome(); // onto their cells, when the floor placed them
            RefreshPlayableLook(); // the last battle's outcome line goes with it
            StartCoroutine(RunAutomaticEvents());
        }

        /// <summary>Raised once a battle has played its last event (#190 / #191: the flow shows what comes next).</summary>
        public event System.Action BattleFinished;

        /// <summary>
        /// The demo's 「降参する」 (#203): the battle stops where it stands. The hand is let go and takes
        /// no more input, the end-turn plate stops answering, and no further event is pulled from the
        /// source. The event already on screen plays out at reduced-motion speed, so its effects clean
        /// up after themselves before the next button can be pressed. <see cref="BattleFinished"/> is
        /// not raised: the flow ended the battle itself. The next <see cref="Restart"/> clears the rest.
        /// </summary>
        public void Halt()
        {
            if (!_started)
            {
                _givenSource = null;
                _hold = true;
                return;
            }
            _halted = true;
            ApplyTweenSpeed(); // BattleSpeed.HaltedTweenSpeed, whatever speed was picked
            ShowSpeedButton(false);
            if (_dragging != null) LetGo();
            SetHandInteractable(false);
        }

        /// <summary>
        /// The battle speed (#348): 1.0, 1.25 or 1.5 times. Takes effect at once, on the tweens and
        /// waits already running, and is kept for the next launch.
        /// </summary>
        public void SetSpeed(BattleSpeedStep step)
        {
            _speed = step;
            if (Trace != null) Trace.Speed = step;
            ApplyTweenSpeed();
            if (_speedButton != null) _speedButton.Show(BattleSpeed.Label(step), BattleSpeed.Chevrons(step));
            PlayerPrefs.SetString(SavedSpeedKey, BattleSpeed.Save(step));
            PlayerPrefs.Save();
        }

        private void CycleSpeed() => SetSpeed(BattleSpeed.Next(_speed));

        /// <summary>
        /// The factor the per-frame motions (the fan's hover, the pull toward the dish) advance by: the
        /// battle speed UiTween.Speed also gets, read from BattleSpeed rather than from UiTween so a debug
        /// gate's freeze and a halt leave them as they were before #348.
        /// </summary>
        private float FrameMotionSpeed => BattleSpeed.TweenSpeed(_speed);

        /// <summary>
        /// The one place that writes UiTween.Speed while the battle runs: the picked speed, or the
        /// reduced-motion speed once halted. A debug gate holds it at 0 and calls this again on its way out.
        /// </summary>
        private void ApplyTweenSpeed()
        {
            if (!string.IsNullOrEmpty(DebugGate)) return;
            UiTween.Speed = BattleSpeed.TweenSpeed(_speed, _halted);
        }

        /// <summary>
        /// The speed switch: top right, left of the journal button (battle-visual-v1 §4.1, #389). It is
        /// never made non-interactable, so it can be pressed on the enemy's turn as well.
        /// </summary>
        private void BuildSpeedButton()
        {
            RectTransform canvas = handArea ? handArea.parent as RectTransform : null;
            if (!canvas)
            {
                Debug.LogWarning("[Depiction] no canvas above the hand area; the speed switch is not shown");
                return;
            }
            _speedButton = new BattleSpeedButton(canvas, CycleSpeed);
            _speedButton.Show(BattleSpeed.Label(_speed), BattleSpeed.Chevrons(_speed));
        }

        /// <summary>Puts the speed switch up while a battle runs and takes it down when it ends or is given up.</summary>
        private void ShowSpeedButton(bool on)
        {
            if (_speedButton != null) _speedButton.Visible = on;
        }

        private void Start()
        {
            // Keep playing while the Editor is unfocused (captures and remote-driven checks rely on it).
            Application.runInBackground = true;
            EnsureEventSystem();
            _effects = EffectSwitches.WithOff(effectsOff, out List<string> unknownEffects);
            foreach (string name in unknownEffects) Debug.LogWarning("[Depiction] effectsOff: no effect is called \"" + name + "\"");
            // Filming (the fixed script, or frames captured at the debug gates) plays at the default
            // speed with no switch on screen, so the shots look as they did before #348.
            bool filming = scriptedPlayback || !string.IsNullOrEmpty(debugCaptureDir);
            _speed = useSavedSpeed && !filming
                ? BattleSpeed.Load(PlayerPrefs.HasKey(SavedSpeedKey), PlayerPrefs.GetString(SavedSpeedKey, ""))
                : BattleSpeed.Default;
            Trace = new EffectTrace(() => Time.unscaledTimeAsDouble) { Speed = _speed };
            DebugGate = "";
            ApplyTweenSpeed(); // also clears a 0 a debug gate left behind when domain reload is off
            if (showSpeedButton && !filming) BuildSpeedButton(); // before the demo flow's early return, so its battles get it too
            // Layer 1's gradient stays under the layered backdrop: it is what shows where BackdropView has
            // not attached (or has no art), so the battle never stands on a blank image (#606).
            if (backdrop && backdrop.sprite == null)
            {
                BattleTheme.FloorPalette palette = BattleTheme.Floor(1);
                backdrop.sprite = ProceduralArt.VerticalGradient(palette.Top, palette.Bottom);
                backdrop.color = Color.white;
            }
            if (stanceHintIcon && stanceHintIcon.sprite == null) stanceHintIcon.sprite = ProceduralArt.Shield;
            StyleEndTurn();

            _enemies.Add(new EnemyParts
            {
                Figure = enemyFigure, Status = enemyStatus, Badge = omenBadge,
                Home = enemyFigure ? enemyFigure.Rect.anchoredPosition : Vector2.zero,
            });
            BuildVisualParts();
            _started = true;
            if (_givenSource == null && _hold)
            {
                ShowSpeedButton(false); // no battle yet: Restart puts the switch up with it
                return; // the demo flow starts the battle (Restart)
            }

            if (_givenSource != null) _source = _givenSource;
            else if (scriptedPlayback) _source = new DepictionRunner(TurnSliceScript.Build());
            else
            {
                // A played battle runs on the BattleCore rules, which only a bridge source brings
                // (BattleBootstrap, the demo flow). With none given there is nothing to play: stop
                // instead of making up a battle (#571).
                Debug.LogError("[Depiction] no source to play: add a BattleBootstrap, or turn on Scripted Playback to film the fixed script.");
                enabled = false;
                return;
            }
            EnsureEnemies(EnemyScreen.CountOf(_source.Frame));
            SnapStanceGlows(_source.Frame);
            ApplyFrame(_source.Frame);
            SnapEnemiesHome(); // onto their cells, when the floor placed them
            StartCoroutine(RunAutomaticEvents());
        }

        private void Awake()
        {
            TakeEnemyTemplates();
        }

        private void OnEnable()
        {
            // OnDisable put the shared speed back to 1; a re-enabled battle takes its own speed back.
            if (_started) ApplyTweenSpeed();
        }

        private void OnDisable()
        {
            // A debug gate freezes every tween through the shared UiTween.Speed. Never leave it frozen
            // for the next scene (View v1.1 uses the same static when domain reload is off).
            UiTween.Speed = 1f;
            // Stopping the component also stops the refusal coroutine; do not leave the guide line stuck.
            _refusal = null;
            _refusalLine?.Hide();
        }

        private void Update()
        {
            // battle_ui_ux_v2 §3.4: Esc closes the drawer (the pile lists, #455).
            if (_pileList != null && _pileList.IsOpen && TryEscape()) _pileList.Close();
            UpdateHover();
            UpdateSnap();
            UpdateEndTurn();
            UpdateFloorReach();
            FollowFigures();
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
