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
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using Depiction.Bridge;
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
    public class DepictionPlayer : MonoBehaviour
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

        // ---- battle_ui_ux_v2 §2.2 (#589): the enemies' parts ----------------------------------------------

        /// <summary>
        /// Takes copies of the enemy's parts before the parts lay themselves out. Each view builds its
        /// children once, in its Awake, and keeps them in fields no copy gets, so a copy of a live part would
        /// build them all a second time on top of the first. This runs first (DefaultExecutionOrder), and
        /// under an inactive holder a copy wakes nothing until it is moved out.
        /// </summary>
        private void TakeEnemyTemplates()
        {
            if (!enemyFigure || !enemyStatus || !omenBadge) return;
            var holder = new GameObject("EnemyTemplates");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            _figureTemplate = Instantiate(enemyFigure, holder.transform);
            _statusTemplate = Instantiate(enemyStatus, holder.transform);
            _badgeTemplate = Instantiate(omenBadge, holder.transform);
        }

        /// <summary>
        /// Readies the parts for a battle of <paramref name="count"/> enemies: further parts are copied from the
        /// templates, and those beyond the count go hidden until a battle needs them again. The next bind
        /// brings a figure back (FigureView.SetDown stands up whatever is not active).
        /// </summary>
        private void EnsureEnemies(int count)
        {
            while (_enemies.Count < count)
            {
                if (!_figureTemplate)
                {
                    Debug.LogError("[Depiction] the scene has no enemy parts to copy for a battle of " + count + " enemies; only the first is shown");
                    break;
                }
                _enemies.Add(CopyEnemy(_enemies.Count));
            }
            int shown = Mathf.Clamp(count, 1, _enemies.Count);
            if (shown == _enemyCount && _enemies.Count == shown) return;
            for (int k = 1; k < _enemies.Count; k++)
            {
                bool used = k < shown;
                if (!used) _enemies[k].Figure.gameObject.SetActive(false);
                _enemies[k].Status.gameObject.SetActive(used);
                _enemies[k].Badge.gameObject.SetActive(used);
            }
            if (shown != _enemyCount)
            {
                _snapPanels = true;
                _snapBadges = true;
            }
            _enemyCount = shown;
        }

        private EnemyParts CopyEnemy(int k)
        {
            return new EnemyParts
            {
                Figure = CopyPart(_figureTemplate, enemyFigure.transform, "EnemyFigure" + (k + 1)),
                Status = CopyPart(_statusTemplate, enemyStatus.transform, "EnemyStatus" + (k + 1)),
                Badge = CopyPart(_badgeTemplate, omenBadge.transform, "OmenBadge" + (k + 1)),
                Home = enemyFigure.Rect.anchoredPosition,
            };
        }

        /// <summary>A copy of <paramref name="template"/> beside the scene's own: moved out from under the inactive holder, it wakes and lays itself out.</summary>
        private static T CopyPart<T>(T template, Transform scenePart, string partName) where T : Component
        {
            T copy = Instantiate(template, template.transform.parent);
            copy.name = partName;
            copy.transform.SetParent(scenePart.parent, false);
            copy.transform.SetSiblingIndex(scenePart.GetSiblingIndex() + 1);
            return copy;
        }

        /// <summary>The enemy <paramref name="k"/>'s parts; the first's for one the screen does not draw.</summary>
        private EnemyParts EnemyAt(int k)
        {
            return k >= 0 && k < _enemyCount ? _enemies[k] : _enemies[0];
        }

        /// <summary>The enemy a cue goes to (Cue.Unit, EnemyScreen).</summary>
        private EnemyParts EnemyOf(Cue cue) => EnemyAt(EnemyScreen.IndexOf(cue, _enemyCount));

        private bool SeveralEnemies => _enemyCount > 1;

        /// <summary>The frame the screen stands on, or null before a battle.</summary>
        private DepictionFrame FrameNow => _source != null ? _source.Frame : null;

        private void SnapEnemiesHome()
        {
            for (int k = 0; k < _enemyCount; k++)
            {
                if (_enemies[k].Figure) SnapHome(_enemies[k].Figure, _enemies[k].Home);
            }
        }

        /// <summary>§4.4 敵の番: only <paramref name="acting"/>'s omen may keep the acting light; −1 puts them all out.</summary>
        private void SetActingExcept(int acting)
        {
            for (int k = 0; k < _enemies.Count; k++)
            {
                if (k != acting && _enemies[k].Badge) _enemies[k].Badge.SetActing(false);
            }
        }

        /// <summary>§2.2 の 1: the figure nearer the player is drawn in front. The figures trade the places they already hold among the siblings.</summary>
        private void DrawNearerInFront(DepictionFrame frame)
        {
            int[] order = EnemyScreen.BackToFront(frame);
            var places = new int[order.Length];
            for (int i = 0; i < order.Length; i++) places[i] = _enemies[order[i]].Figure.transform.GetSiblingIndex();
            System.Array.Sort(places);
            for (int i = 0; i < order.Length; i++)
            {
                Transform figure = _enemies[order[i]].Figure.transform;
                if (figure.GetSiblingIndex() != places[i]) figure.SetSiblingIndex(places[i]);
            }
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

        // ---- battle-visual-v1 §4.2 / §4.7 (#242) ------------------------------------------------------

        /// <summary>
        /// Makes the parts the scene from before #242 does not have: the floor, under the figures, the
        /// stamina tag, which takes the player's stamina over from the pips in the player's panel, and
        /// the piles under it (#455).
        /// </summary>
        private void BuildVisualParts()
        {
            if (enemyFigure && enemyFigure.Rect.parent is RectTransform arena) _floor = new FloorView(arena);
            RectTransform root = RefScreen.Root(handArea ? handArea : (Transform)transform);
            if (root && playerStatus)
            {
                int below = handArea && handArea.parent == root ? handArea.GetSiblingIndex() : -1;
                _staminaTag = new StaminaTagView(root, below);
                playerStatus.PipsElsewhere = true;
                playerStatus.StaminaShown = (stamina, max) => _staminaTag.SetStamina(stamina, max);
            }
            if (root)
            {
                int below = handArea && handArea.parent == root ? handArea.GetSiblingIndex() : -1;
                _piles = new PilesView(root, below);
                below = handArea && handArea.parent == root ? handArea.GetSiblingIndex() : -1;
                _stanceLists = new StanceListView(root, below);
                below = handArea && handArea.parent == root ? handArea.GetSiblingIndex() : -1;
                _played = new PlayedAttributesView(root, below);
                below = handArea && handArea.parent == root ? handArea.GetSiblingIndex() : -1;
                _reserveHint = new ReserveHintView(root, below);
                // §5.4: over the hand, so a card's back does not hide the reason it came back.
                int above = handArea && handArea.parent == root ? handArea.GetSiblingIndex() + 1 : -1;
                _refusalLine = new RefusalLineView(root, above);
                // §5.5: over the hand and everything else below the throw line.
                above = handArea && handArea.parent == root ? handArea.GetSiblingIndex() + 1 : -1;
                _enemyShade = new EnemyTurnShadeView(root, above);
                // battle_ui_ux_v2 §1.1 C4: a click on a pile opens its list over everything.
                _pileList = new PileListView(root);
                _piles.Clicked += pile => _pileList.Toggle(pile);
                // §4.9: the 構えの予告 is made here at its own place; the scene's icon and number give way to it.
                if (stanceHintIcon) stanceHintIcon.gameObject.SetActive(false);
                if (stanceHintText) stanceHintText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// battle-visual-v1 §5.5 / §4.9 (#455): on the enemy's turn the screen sinks below y 790 and the
        /// end-turn plate stands at 40%. Whose turn it is comes from the script (DepictionFrame.EnemyTurn).
        /// </summary>
        private void ShowEnemyTurn(bool on)
        {
            _enemyShade?.Show(on);
            if (endTurn) UiKit.Group(endTurn).alpha = on ? EnemyTurnShadeLayout.EndTurnAlpha : 1f;
        }

        /// <summary>
        /// §4.9 / §5.3 (#455): while a card is held, the 構えの予告 reads as it would once the card is paid
        /// for (CardFace.StanceHintAfterPay, the script's); otherwise as the frame has it.
        /// </summary>
        private void ShowReserveHint(CardView held)
        {
            if (_reserveHint == null) return;
            string afterPay = !held || held.Face == null ? null
                : SeveralEnemies ? held.Face.StanceHintAfterPayFor(EnemyScreen.FramedUnit(held.Face, _pickedUnit))
                : held.Face.StanceHintAfterPay;
            _reserveHint.Show(afterPay ?? _frameStanceHint);
        }

        /// <summary>
        /// battle-visual-v1 §4.5 / §5.2 HP の減る分 (#455): while a card is held, the HP it would take
        /// off each enemy (CardFace.HpLossFor(k), the script's) shows as wick stripes on that enemy's HP
        /// bar (battle_ui_ux_v2 §2.2 の 4); with no card held, or a loss of 0, there are none.
        /// </summary>
        private void ShowHpLoss(CardView held)
        {
            for (int k = 0; k < _enemyCount && k < _enemies.Count; k++)
            {
                if (_enemies[k].Status) _enemies[k].Status.ShowHpLoss(held && held.Face != null ? held.Face.HpLossFor(k) : 0);
            }
        }

        /// <summary>
        /// §4.7 (#455): the piles with the script's counts. While they are drawn, drawn cards fly from
        /// the draw pile and the discarded hand and the played card's ghost fly to the discard pile
        /// (battle_ui_ux_v2 「スタミナの札と束」), so the scene's two points move onto them.
        /// </summary>
        private void ApplyPiles(PileFrame piles)
        {
            if (_piles == null) return;
            _piles.Bind(piles);
            _pileList?.Show(piles);
            if (!_piles.Shown) return;
            if (deckPoint) deckPoint.position = _piles.MiddleOf(PileKind.Draw);
            if (discardPoint) discardPoint.position = _piles.MiddleOf(PileKind.Discard);
        }

        /// <summary>
        /// §4.2 / §4.3: the floor of <paramref name="frame"/>, with the player standing on its cells now
        /// and each enemy's home on its own (an enemy is moved there by the next return or snap, so a
        /// return already under way is not cut short). With a floor the one gap number is the floor's,
        /// so the figure's own tag stays down.
        /// </summary>
        private void ApplyFloor(DepictionFrame frame)
        {
            if (_floor == null || !playerFigure) return;
            _floor.Bind(frame.Floor, playerFigure.transform.position, frame.Corner.Floor);
            _reachFor = null; // Bind put the reach out; the next Update lights it again for the card still hovered

            if (!_floor.Shown) return;
            FloorFrame floor = frame.Floor;
            playerFigure.SetRange(false, "");
            playerFigure.transform.position = _floor.FeetOf(floor.PlayerCell, floor.PlayerSize);
            SetPlayerHome(playerFigure);
            SetEnemyHomes(frame);
        }

        /// <summary>Each enemy's home is its cell in <paramref name="frame"/>.</summary>
        private void SetEnemyHomes(DepictionFrame frame)
        {
            for (int k = 0; k < _enemyCount; k++)
            {
                EnemyParts foe = _enemies[k];
                if (foe.Figure) foe.Home = AnchoredAt(foe.Figure.Rect, _floor.FeetOf(EnemyScreen.CellOf(frame, k), EnemyScreen.SizeOf(frame, k)));
            }
        }

        /// <summary>The anchored position that puts <paramref name="rt"/>'s pivot on the world point <paramref name="world"/>.</summary>
        private static Vector2 AnchoredAt(RectTransform rt, Vector3 world)
        {
            Vector3 was = rt.position;
            rt.position = world;
            Vector2 anchored = rt.anchoredPosition;
            rt.position = was;
            return anchored;
        }

        /// <summary>
        /// With a floor, the parts tied to an enemy keep to its x: the omen badge and the release zone
        /// over the enemy's home, kept 24 px inside the screen's edges. The badge's bottom also stands
        /// 14 px over the enemy's head (§4.4, #455: the figure is 226 or 264 px tall since §4.3); the
        /// release zone's height stays where the scene put it. The number panels follow their
        /// figures with or without a floor (§4.5). With several enemies the badges share one row (§5.16)
        /// and slide apart (§5.8), and the release zone stands over the enemy a held card is aimed at.
        /// </summary>
        private void FollowFigures()
        {
            if (!enemyFigure || !playerFigure || _enemies.Count == 0) return;
            RectTransform root = RefScreen.Root(enemyFigure.transform);
            if (!root) return;
            if (_floor != null && _floor.Shown)
            {
                if (SeveralEnemies) PlaceBadgeRow(root);
                else if (omenBadge)
                {
                    Vector2 home = EnemyHomeOnScreen(root, _enemies[0]);
                    KeepX(omenBadge.Rect, root, home.x);
                    float head = home.y - FigureFrame.HeightFor(EnemySizeOf(0));
                    KeepBottom(omenBadge.Rect, root, head - FigureFrame.OmenOverHead);
                }
                if (receiver) KeepX((RectTransform)receiver.transform, root, EnemyHomeOnScreen(root, ReceiverEnemy()).x);
            }
            float playerX = RefScreen.Of(root, _playerHomeSet ? _playerHome : playerFigure.transform.position).x;
            FollowPanels(root, playerX);
        }

        /// <summary>The enemy the release zone stands over.</summary>
        private EnemyParts ReceiverEnemy()
        {
            if (_aimedUnit >= 0) return EnemyAt(_aimedUnit);
            return EnemyAt(FrameNow != null ? EnemyScreen.FocusOf(FrameNow) : 0);
        }

        /// <summary>
        /// §2.2 の 2 / §5.8 / §5.16 (#589): each standing enemy's badge over its head, on one row 14 px over
        /// the highest head, slid apart where two would overlap (OmenRowLayout). The slide eases out over
        /// 200 ms, so an enemy that walks next to another has its badge make room as it arrives. A fallen
        /// enemy's badge is hidden and takes no place.
        /// </summary>
        private void PlaceBadgeRow(RectTransform root)
        {
            DepictionFrame frame = FrameNow;
            if (frame == null) return;
            var standing = new List<int>();
            for (int k = 0; k < _enemyCount; k++)
            {
                if (EnemyScreen.Stands(frame, k) && _enemies[k].Badge) standing.Add(k);
            }
            if (standing.Count == 0) return;
            var centres = new float[standing.Count];
            var heights = new float[standing.Count];
            float feetY = 0f;
            for (int i = 0; i < standing.Count; i++)
            {
                Vector2 home = EnemyHomeOnScreen(root, _enemies[standing[i]]);
                centres[i] = home.x;
                heights[i] = FigureFrame.HeightFor(EnemySizeOf(standing[i]));
                feetY = home.y;
            }
            float[] lefts = OmenRowLayout.Lefts(centres);
            float bottom = OmenRowLayout.RowBottom(feetY, heights);
            float share = 1f - Mathf.Exp(-3f * FrameMotionSpeed * Time.unscaledDeltaTime * 1000f / OmenRowLayout.SlideMs);
            for (int i = 0; i < standing.Count; i++)
            {
                EnemyParts foe = _enemies[standing[i]];
                float slide = OmenRowLayout.Slide(lefts[i], centres[i]);
                foe.BadgeSlide = _snapBadges || Mathf.Abs(slide - foe.BadgeSlide) < 0.25f ? slide : Mathf.Lerp(foe.BadgeSlide, slide, share);
                KeepX(foe.Badge.Rect, root, centres[i] + foe.BadgeSlide);
                KeepBottom(foe.Badge.Rect, root, bottom);
            }
            _snapBadges = false;
        }

        /// <summary>The figure's place between its motions is where it stands now (<paramref name="figure"/> the player's only).</summary>
        private void SetPlayerHome(FigureView figure)
        {
            if (figure) SetPlayerHome(figure, figure.transform.position);
        }

        private void SetPlayerHome(FigureView figure, Vector3 world)
        {
            if (!figure || figure != playerFigure) return;
            _playerHome = world;
            _playerHomeSet = true;
        }

        /// <summary>
        /// battle-visual-v1 §4.5 (#455): each number panel at y 614 under its figure's home, 300 wide for
        /// the player and as wide as each enemy's cells allow (StatusPanelLayout), pushed apart where they
        /// would meet. They ease toward their places, all by the same share each frame, so panels
        /// apart before and apart after are never over each other on the way. A fallen enemy's grey panel
        /// keeps its place in the row, under its emptied cell (§2.2 の 6).
        /// </summary>
        private void FollowPanels(RectTransform root, float playerX)
        {
            if (!playerStatus || !enemyStatus) return;
            FloorFrame floor = FrameNow != null ? FrameNow.Floor : null;
            int cells = _floor != null && _floor.Shown && floor != null ? floor.Cells : 0;
            playerStatus.SetWidth(StatusPanelLayout.PlayerWidth);
            for (int k = 0; k < _enemyCount; k++)
            {
                float width = StatusPanelLayout.EnemyPanelWidth(cells, EnemySizeOf(k));
                if (!Mathf.Approximately(width, _enemies[k].Status.LaidOutWidth)) _snapPanels = true; // a new line: no easing across widths
                _enemies[k].Status.SetWidth(width);
            }
            float[] lefts = PanelPlaces(root, playerX);
            if (_snapPanels)
            {
                _playerPanelLeft = lefts[0];
                for (int k = 0; k < _enemyCount; k++) _enemies[k].PanelLeft = lefts[k + 1];
                _snapPanels = false;
            }
            else
            {
                float share = 1f - Mathf.Exp(-PanelFollowRate * FrameMotionSpeed * Time.unscaledDeltaTime);
                _playerPanelLeft = Mathf.Abs(lefts[0] - _playerPanelLeft) < 0.25f ? lefts[0] : Mathf.Lerp(_playerPanelLeft, lefts[0], share);
                for (int k = 0; k < _enemyCount; k++)
                {
                    EnemyParts foe = _enemies[k];
                    foe.PanelLeft = Mathf.Abs(lefts[k + 1] - foe.PanelLeft) < 0.25f ? lefts[k + 1] : Mathf.Lerp(foe.PanelLeft, lefts[k + 1], share);
                }
            }
            playerStatus.PlaceAt(root, new Vector2(_playerPanelLeft, StatusPanelLayout.Top));
            for (int k = 0; k < _enemyCount; k++) _enemies[k].Status.PlaceAt(root, new Vector2(_enemies[k].PanelLeft, StatusPanelLayout.Top));
        }

        /// <summary>Where the panels belong now: the player's first, then each enemy's (StatusPanelLayout, from the figures' homes).</summary>
        private float[] PanelPlaces(RectTransform root, float playerX)
        {
            var centres = new float[_enemyCount + 1];
            var widths = new float[_enemyCount + 1];
            centres[0] = playerX;
            widths[0] = StatusPanelLayout.PlayerWidth;
            for (int k = 0; k < _enemyCount; k++)
            {
                centres[k + 1] = EnemyHomeOnScreen(root, _enemies[k]).x;
                widths[k + 1] = _enemies[k].Status.LaidOutWidth;
            }
            if (SeveralEnemies) return StatusPanelLayout.PlaceByX(centres, widths);
            StatusPanelLayout.Place(centres[0], widths[0], centres[1], widths[1], out float playerLeft, out float enemyLeft);
            return new[] { playerLeft, enemyLeft };
        }

        /// <summary>Where an enemy's home is on the reference screen.</summary>
        private static Vector2 EnemyHomeOnScreen(RectTransform root, EnemyParts foe)
        {
            return RefScreen.Of(root, foe.Figure.Rect.parent.TransformPoint(EnemyHomeLocal(foe)));
        }

        /// <summary>The enemy's home in its parent's local space (its anchored position, back through its anchors).</summary>
        private static Vector3 EnemyHomeLocal(EnemyParts foe)
        {
            RectTransform rt = foe.Figure.Rect;
            Vector2 now = rt.anchoredPosition;
            return rt.localPosition + (Vector3)(foe.Home - now);
        }

        /// <summary>Moves <paramref name="rt"/> so its middle stands on screen x <paramref name="x"/>, its whole width 24 px inside the edges.</summary>
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>
        /// Moves <paramref name="rt"/> up or down so its bottom edge stands on screen y <paramref name="bottom"/>.
        /// Read from its rect and pivot, not its corners, so a pop or a flare scaling it does not move it.
        /// </summary>
        private static void KeepBottom(RectTransform rt, RectTransform root, float bottom)
        {
            float pivotY = bottom - rt.rect.height * rt.pivot.y;
            Vector2 now = RefScreen.Of(root, rt.position);
            if (Mathf.Abs(now.y - pivotY) < 0.5f) return;
            Vector3 to = RefScreen.World(root, new Vector2(now.x, pivotY));
            rt.position = new Vector3(rt.position.x, to.y, rt.position.z);
        }

        private static void KeepX(RectTransform rt, RectTransform root, float x)
        {
            Vector3[] corners = Corners;
            rt.GetWorldCorners(corners);
            float left = RefScreen.Of(root, corners[0]).x;
            float right = RefScreen.Of(root, corners[2]).x;
            float half = (right - left) * 0.5f;
            float target = Mathf.Clamp(x, 24f + half, BattleTheme.RefWidth - 24f - half);
            float shift = target - (left + half);
            if (Mathf.Abs(shift) < 0.5f) return;
            Vector3 from = RefScreen.World(root, new Vector2(left + half, 0f));
            Vector3 to = RefScreen.World(root, new Vector2(target, 0f));
            rt.position += new Vector3(to.x - from.x, 0f, 0f);
        }

        /// <summary>
        /// §4.2 / §5.1 / §5.2: while an enemy-aimed card is hovered or held, the cells it reaches light
        /// up (CardFace.ReachCells, the script's). Anything else puts them out.
        /// </summary>
        private void UpdateFloorReach()
        {
            if (_floor == null) return;
            CardView card = _dragging ? _dragging : (!_busy ? _hovered : null);
            if (!card || card.Face == null || card.Face.Aim != CardAim.Single) card = null;
            // By reference: a played card is destroyed, and Unity's == would call it equal to null.
            if (ReferenceEquals(card, _reachFor)) return;
            _reachFor = card;
            if (card) _floor.ShowReach(card.Face.ReachCells);
            else _floor.HideReach();
        }

        /// <summary>
        /// battle-visual-v1 §4.9 (#242): the end-turn plate in amber — a 2 px amber frame over an amber
        /// 16% fill, its label at 24 px. The plate belongs to the scene, so it is restyled here.
        /// </summary>
        private void StyleEndTurn()
        {
            if (!endTurn) return;
            Image plate = endTurn.GetComponent<Image>();
            if (plate)
            {
                plate.sprite = VisualArt.Rounded(6);
                plate.type = Image.Type.Sliced;
                plate.color = BattleTheme.WithAlpha(BattleTheme.Amber, 0.16f);
            }
            foreach (string edge in new[] { "FrameTop", "FrameBottom", "FrameLeft", "FrameRight" })
            {
                Transform old = endTurn.Find(edge);
                if (old) old.gameObject.SetActive(false);
            }
            if (!endTurn.Find("AmberFrame")) VisualArt.Ring(endTurn, "AmberFrame", BattleTheme.Amber, 6, 2);
            Text label = endTurn.GetComponentInChildren<Text>();
            if (label)
            {
                label.fontSize = 24;
                label.fontStyle = FontStyle.Bold;
                label.color = BattleTheme.Ink;
            }
        }

        /// <summary>
        /// Reads a click on the end-turn plate. The plate is a plain rect in the prefab and the prefab
        /// belongs to the Unity project, not to this repo, so the click is read here instead of being
        /// wired to a Button component that would have to be added there.
        /// </summary>
        private void UpdateEndTurn()
        {
            if (_source == null || _busy || _dragging != null || !endTurn) return;
            if (_halted || !_source.CanEndTurn) return;
            if (!TryPointerPress(out Vector2 pointer)) return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(endTurn, pointer, null)) return;
            StartCoroutine(PlayThenContinue(_source.EndTurn()));
        }

        /// <summary>Plays one event the player asked for, then lets the automatic ones follow.</summary>
        private IEnumerator PlayThenContinue(DepictionEvent ev)
        {
            _busy = true;
            SetHandInteractable(false);
            yield return PlayEvent(ev);
            _busy = false;
            yield return UiTween.Wait(EffectFlow.AfterPlayerEventMs);
            yield return RunAutomaticEvents();
        }

        [ContextMenu("Debug Step")]
        public void DebugStep()
        {
            _stepRequested = true;
        }

        // ---- flow -------------------------------------------------------------------------

        private IEnumerator RunAutomaticEvents()
        {
            _busy = true;
            while (!_halted && !_source.Finished && !_source.WaitingForPlayer)
            {
                DepictionEvent ev = _source.AdvanceAuto();
                yield return PlayEvent(ev);
                yield return UiTween.Wait(EffectFlow.AfterAutoEventMs);
            }
            _busy = false;
            if (_halted) yield break; // given up (#203): nothing follows, and no result card
            if (_source.Finished)
            {
                Finished = true;
                RefreshPlayableLook(); // the guide line goes quiet; the outcome is the result card's and the end screen's (#191)
                Debug.Log("[Depiction] finished. seconds per event: " + string.Join(" / ", EventSeconds.ConvertAll(s => s.ToString("0.00"))));
                if (logEffectTrace) Debug.Log("[Depiction] effects:\n" + Trace.ToCsv());
                BattleOutcome outcome = _source.Frame.Outcome;
                if (outcome != BattleOutcome.Ongoing)
                {
                    StartCoroutine(Effect(EffectId.ResultCard, ShowResult(outcome, _effects.Ms(EffectId.ResultCard)), () => ShowResultNow(outcome)));
                }
                ShowSpeedButton(false); // like the demo's 「降参する」, the switch is for the battle only
                BattleFinished?.Invoke();
                yield break;
            }
            SetHandInteractable(true);
            if (autoPlayDrags) StartCoroutine(AutoDrag());
        }

        private IEnumerator PlayEvent(DepictionEvent ev)
        {
            float startedAt = Time.unscaledTime;
            BeginPlan(ev);
            // §4.4 敵の番 (#455): the acting omen's light lasts through the enemy's beats and goes out
            // with the first event that is not one of them (the next omen, the turn start, a fall), or
            // with the beat of another enemy.
            if (ev.Kind != DepictionEventKind.EnemyAction) SetActingExcept(-1);
            else if (SeveralEnemies) SetActingExcept(ev.Unit);
            // §5.5 (#455): the screen comes back up as the event that ends the enemy's turn starts (the
            // turn start's banner), not after it has played; it sinks when the turn end's frame lands.
            if (ev.After != null && !ev.After.EnemyTurn) ShowEnemyTurn(false);
            if (ev.Kind == DepictionEventKind.TurnStart)
            {
                yield return HoldBeat(EffectId.TurnBanner, Banner(DepictionText.YourTurn, BattleTheme.Accent, _effects.Ms(EffectId.TurnBanner)), null);
                // An omen already standing since the enemy's phase blinks once; a new one drops in with its cue.
                for (int k = 0; k < _enemyCount; k++)
                {
                    int unit = k;
                    if (!EnemyScreen.OmenOf(ev.After, k).Visible) continue;
                    if (ev.Cues.Exists(c => c.Kind == CueKind.OmenShow && EnemyScreen.IndexOf(c, _enemyCount) == unit)) continue;
                    StartCoroutine(Effect(EffectId.OmenBlink, _enemies[k].Badge.Blink(_effects.Ms(EffectId.OmenBlink)), null));
                }
            }
            foreach (Cue cue in ev.Cues)
            {
                yield return PlayCue(ev, cue);
            }
            if (ev.Kind == DepictionEventKind.TurnEnd)
            {
                yield return HoldBeat(EffectId.EnemyTurnBanner, Banner(DepictionText.EnemyTurn, BattleTheme.Omen, _effects.Ms(EffectId.EnemyTurnBanner)), null);
            }
            // §4.2 (#242): with a floor, home is the enemy's cell after the event, so the step back
            // below also carries it onto a cell it was moved to.
            if (_floor != null && _floor.Shown && ev.After != null && ev.After.Floor.Cells > 0) SetEnemyHomes(ev.After);
            for (int k = 0; k < _enemyCount; k++)
            {
                EnemyParts foe = _enemies[k];
                if (!foe.Figure) continue;
                // With others standing a fallen enemy's figure is gone, and has nowhere to step back to.
                if (SeveralEnemies && ev.After != null && EnemyScreen.UnitOf(ev.After, k).Down) continue;
                // An enemy whose blow was all taken by Guard (or who only guarded) is still leaning in:
                // it steps back beside the next event instead of holding this one up.
                if (foe.Figure.Rect.anchoredPosition != foe.Home)
                {
                    StartCoroutine(Effect(EffectId.EnemyReturn, ReturnHome(foe.Figure, foe.Home, _effects.Ms(EffectId.EnemyReturn)), () => SnapHome(foe.Figure, foe.Home)));
                }
                // A move that ends where it began (盾 reaches 0) never returns, and a blow Guard took part of
                // (or one a move follows, #499) skips the player's return: neither may keep its action
                // picture into the next event.
                else foe.Figure.SetPose(FigurePose.Idle);
            }
            if (playerFigure) playerFigure.SetPose(FigurePose.Idle);
            EndPlan();
            ApplyFrame(ev.After);
            float seconds = Time.unscaledTime - startedAt;
            EventSeconds.Add(seconds);
            LogSettled(ev, seconds);
            yield return Gate(ev.Order + "-end");
        }

        private IEnumerator PlayCue(DepictionEvent ev, Cue cue)
        {
            // #589: an enemy's end of the beat is the enemy the cue names.
            EnemyParts foe = EnemyOf(cue);
            OmenBadgeView badge = foe.Badge;
            FigureView target = cue.Target == UnitSide.Player ? playerFigure : foe.Figure;
            StatusBarView status = cue.Target == UnitSide.Player ? playerStatus : foe.Status;
            Vector2 chest = DepictionFx.PointOn(fxLayer, target.chest);
            Vector2 head = DepictionFx.PointOn(fxLayer, target.head);
            // §5.0: a number's centre goes from the chest to the head of this figure, whatever its height,
            // so it stops under the omen badge. Labels start just under the head.
            Vector2 numberAt = DepictionFx.PointOn(fxLayer, target.transform) + new Vector2(0f, FloatNumberSpec.StartAbove(target.Height));
            float numberRise = FloatNumberSpec.RiseBy(target.Height);
            Vector2 labelAt = head + new Vector2(0f, -30f);
            // §5.3 (#57): a beat of a play draws its system's shape beside it; a blow draws its own in Attack,
            // and 息 is the pips lighting, with the chest rising only on a heal.
            switch (EffectGrammar.MarkOf(cue))
            {
                case FxMark.Shape:
                    // §5.11 順 2 (#501): read before the light below changes. A unit that already keeps a
                    // stance light gets the crest alone, with no second light over its body.
                    bool crestLight = StanceGlow.CrestCarriesLight(target.StanceGlowShown);
                    StartCoroutine(Effect(EffectId.GrammarMark, DepictionFx.Mark(this, fxLayer, chest, head, DepictionFx.PointOn(fxLayer, target.transform),
                        target.side == UnitSide.Player ? 1f : -1f, cue.Fx.Value, cue.Intensity, _effects.Ms(EffectId.GrammarMark), crestLight), null));
                    LightStanceGlowWithCrest(ev, cue, target); // §5.11 順 2 (#501)
                    break;
                case FxMark.ChestHeave:
                    StartCoroutine(Effect(EffectId.BreathHeave, Heave(target, _effects.Ms(EffectId.BreathHeave)), null));
                    break;
            }

            switch (cue.Kind)
            {
                case CueKind.GuardReset:
                    status.SetGuard(cue.GuardAfter);
                    break;

                case CueKind.StaminaChange:
                    if (cue.Amount > 0)
                    {
                        // A gain lights its pips one by one and is waited for.
                        yield return HoldBeat(EffectId.StaminaRecover,
                            status.LightPips(cue.StaminaAfter, cue.StaminaMax, _effects.Ms(EffectId.StaminaRecover)),
                            () => status.SetStamina(cue.StaminaAfter, cue.StaminaMax), cue.Amount);
                    }
                    else
                    {
                        // A spend pops beside the card's own beats.
                        status.SetStamina(cue.StaminaAfter, cue.StaminaMax);
                        if (status.pipRow && _effects.IsOn(EffectId.StaminaChange)) StartCoroutine(UiTween.Pop(status.pipRow, _effects.Ms(EffectId.StaminaChange)));
                    }
                    break;

                case CueKind.StatusChange:
                {
                    EffectId beat = StatusBeat.Of(cue);
                    yield return HoldBeat(beat, status.PlayStatus(cue.Text, cue.StacksAfter, beat, _effects.Ms(beat)), () => status.SetStatus(cue.Text, cue.StacksAfter));
                    break;
                }

                case CueKind.DrawHand:
                    // §3.14 (#455): a draw that ran the pile out slides the discard pile onto it mid-draw.
                    yield return DrawHand(ev.After.Hand, cue.Amount, cue.RebuildAfter);
                    break;

                case CueKind.OmenShow:
                    badge.Bind(EnemyScreen.OmenOf(ev.After, EnemyScreen.IndexOf(cue, _enemyCount)));
                    yield return HoldBeat(EffectId.OmenShow, badge.PopIn(_effects.Ms(EffectId.OmenShow)), () => badge.Rect.localScale = Vector3.one);
                    break;

                case CueKind.TraitFire:
                    yield return HoldBeat(EffectId.TraitFire, FireTrait(labelAt, cue.Text), null);
                    break;

                case CueKind.Slash:
                    yield return Attack(ev, cue, target, foe.Figure, status, chest);
                    break;

                case CueKind.GuardGain:
                    yield return HoldBeat(EffectId.GuardGain, GainGuard(cue, status, chest, numberAt, numberRise), () => status.SetGuard(cue.GuardAfter));
                    break;

                case CueKind.RangeSwitch:
                {
                    // Moved by the other side (the polearm's shove): a knock-back, and the muted label says so.
                    if (cue.Pushed) FloatEffect(EffectId.PushMark, labelAt, DepictionText.Pushed, BattleTheme.Ink2, 36);
                    // §5.7 順 1〜2 (#586): the walk, 160 a cell, then the gap number counted again on arrival.
                    // Every source draws the floor since the near / far slots went (#577).
                    int cells = EffectPlan.CellsOf(cue);
                    if (!OnFloor(cue))
                    {
                        // A script bug: the frame shows no floor to walk on. The plan's waits are still held.
                        Debug.LogError("[Depiction] event " + ev.Order + " " + ev.Title + ": a move with no floor shown");
                        yield return HoldBeat(EffectId.FloorMove, UiTween.Wait(_effects.WaitMs(EffectId.FloorMove, cells)), null, cells);
                        yield return HoldBeat(EffectId.GapRecount, UiTween.Wait(_effects.Ms(EffectId.GapRecount)), null);
                        break;
                    }
                    yield return HoldBeat(EffectId.FloorMove, MoveOnFloor(target, cue, cells), () => SnapOnFloor(target, cue), cells);
                    yield return HoldBeat(EffectId.GapRecount,
                        _floor.RecountGap(cue.GapAfter, cue.PlayerCellAfter, PlayerSize, cue.EnemyCellAfter, _effects.Ms(EffectId.GapRecount)),
                        () => _floor.SetGap(cue.GapAfter, cue.PlayerCellAfter, PlayerSize, cue.EnemyCellAfter));
                    break;
                }

                case CueKind.StanceCue:
                    // §5.13 順 1 (#501): pips 1-3 glow first (構え's 息), then the shield and "+3" (順 2).
                    yield return HoldBeat(EffectId.ReservePips, GlowReservePips(cue.Target, _effects.Ms(EffectId.ReservePips)), null);
                    yield return HoldBeat(EffectId.StanceCue, Reserve(cue, status, chest, numberAt, numberRise), () => status.SetGuard(cue.GuardAfter));
                    yield return Gate(ev.Order + "-impact");
                    break;

                case CueKind.DiscardHand:
                    yield return DiscardHand();
                    break;

                case CueKind.EnemyWindup:
                    SetHandInteractable(false);
                    // The moment the enemy does what it foretold: the omen lights up (§4.4 敵の番) and
                    // flares beside its move.
                    badge.SetActing(true);
                    StartCoroutine(Effect(EffectId.OmenExecute, badge.Flare(_effects.Ms(EffectId.OmenExecute)), null));
                    yield return HoldBeat(EffectId.EnemyMotion, EnemyMotion(target, cue.System, _effects.Ms(EffectId.EnemyMotion)),
                        () => target.SetPose(FigurePose.Act));
                    break;

                case CueKind.Defeat:
                {
                    // #288: the fallen enemy's omen goes with it (the frame after the beat keeps it hidden).
                    // #349: and so does an elite's plan, which a plain FadeOut leaves up.
                    if (cue.Target == UnitSide.Enemy && badge)
                    {
                        StartCoroutine(Effect(EffectId.OmenSpend, badge.FadeOutAll(_effects.Ms(EffectId.OmenSpend)), badge.HideAllNow));
                    }
                    // §5.15 順 5 (#589): one enemy falls while others stand. After the fall's 800 the foot
                    // light goes out (300) and the panel turns grey (300). The event's plan lists the 800 only:
                    // EffectPlan is shared with the trial, and its screen holds these two past the plan as well.
                    bool othersStand = cue.Target == UnitSide.Enemy && SeveralEnemies && EnemyScreen.OthersStand(ev.After, EnemyScreen.IndexOf(cue, _enemyCount));
                    yield return HoldBeat(EffectId.Defeat, target.Fall(_effects.Ms(EffectId.Defeat), othersStand), () => target.SetDown(true));
                    if (!othersStand) break;
                    if (_effects.IsOn(EffectId.Defeat))
                    {
                        yield return target.FadeFoot(DefeatShape.FootLightMs);
                        yield return status.GreyOut(DefeatShape.PanelGreyMs);
                    }
                    else status.SetFallen(true);
                    break;
                }

                case CueKind.SideBonusMiss:
                    yield return HoldBeat(EffectId.SideBonusMiss, StrikeOffBonus(cue, badge), badge.StrikeSideNow);
                    break;

                case CueKind.GuardBlock:
                {
                    // The Guard's number is its own colour, and a light stands in for the sound (#76).
                    status.SetGuard(cue.GuardAfter);
                    if (cue.GuardAfter == 0) StartCoroutine(Effect(EffectId.GuardBreak, status.CrackGuard(_effects.Ms(EffectId.GuardBreak)), null));
                    else if (status.guardBadge) StartCoroutine(UiTween.Pop(status.guardBadge, 260f));
                    FloatHit(EffectId.GuardNumber, ev, cue, target, FloatNumberKind.GuardLoss, "");
                    yield return HoldBeat(EffectId.GuardBlock, DepictionFx.ShieldBlock(this, fxLayer, chest, cue.Amount.ToString(), () => Gate(ev.Order + "-block")), null);
                    break;
                }

                case CueKind.HpChange:
                {
                    // 出血 / 再生 / a heal / a 見切り return (#188): the number with its cause beside it
                    // (battle_ui_ux_v2 §5.0 脇ラベル), then the bar.
                    FloatHit(EffectId.DamageNumber, ev, cue, target, FloatNumberSpec.OfHpChange(cue.Amount), cue.Text);
                    yield return DrainHp(status, cue.HpAfter);
                    break;
                }

                case CueKind.Hit:
                {
                    bool strong = EffectStrength.IsStrong(cue.Intensity);
                    FloatHit(EffectId.DamageNumber, ev, cue, target, FloatNumberKind.HpLoss, cue.SideLabel);
                    StartCoroutine(Effect(EffectId.HitFlash, target.Flash(BattleTheme.Omen, _effects.Ms(EffectId.HitFlash)), null));
                    StartCoroutine(Effect(EffectId.TargetRecoil, Recoil(target, strong), null));
                    if (strong) StartCoroutine(Effect(EffectId.ScreenShake, ShakeArena(), null));
                    yield return DrainHp(status, cue.HpAfter);
                    yield return Gate(ev.Order + "-impact");
                    // The player's blow Guard took part of, with a move right after it (#499): as in Attack.
                    if (cue.Target == UnitSide.Enemy && EffectPlan.MoveFollows(ev, cue) && playerFigure) playerFigure.SetPose(FigurePose.Idle);
                    if (cue.Target == UnitSide.Player)
                    {
                        // The blow that lands on the player is the enemy's own: its omen is spent and
                        // it draws back. A blow the player lands does neither.
                        yield return HoldBeat(EffectId.OmenSpend, badge.FadeOut(_effects.Ms(EffectId.OmenSpend)), badge.HideNow);
                        // §5.4 順 2 (#587): the enemy's own move next (退き撃ち) carries it on from the
                        // lunge; the omen fades beside the move (the plan gives OmenSpend no wait), and
                        // the figure walks in its standing pose, as the return would have left it.
                        if (EffectPlan.EnemyMoveFollows(ev, cue))
                        {
                            if (foe.Figure) foe.Figure.SetPose(FigurePose.Idle);
                        }
                        else
                        {
                            yield return HoldBeat(EffectId.EnemyReturn, ReturnHome(foe.Figure, foe.Home, _effects.Ms(EffectId.EnemyReturn)), () => SnapHome(foe.Figure, foe.Home));
                        }
                    }
                    break;
                }
            }
        }

        // ---- beats --------------------------------------------------------------------------

        private IEnumerator Attack(DepictionEvent ev, Cue cue, FigureView target, FigureView enemy, StatusBarView status, Vector2 chest)
        {
            // The script must name the attacker. A missing one is a script bug: say so, then fall back
            // to the opposite of the target so the beat still plays (EffectPlan.AttackerOf, which the plan reads too).
            if (!cue.Source.HasValue) Debug.LogError("[Depiction] event " + ev.Order + ": Slash cue has no Source");
            UnitSide source = EffectPlan.AttackerOf(cue);
            bool byPlayer = source == UnitSide.Player;
            bool strong = EffectStrength.IsStrong(cue.Intensity);
            FigureView attacker = byPlayer ? playerFigure : enemy;
            Vector2 attackerHome = attacker.Rect.anchoredPosition;
            // #288: both sides step in before the blow. The enemy draws back with EnemyReturn once the
            // blow has landed, or beside the next event when Guard took all of it.
            EffectId lunge = byPlayer ? EffectId.AttackLunge : EffectId.EnemyLunge;
            yield return HoldBeat(lunge, Lunge(attacker, 46f, _effects.Ms(lunge)), () => attacker.SetPose(FigurePose.Act));
            Color streak = byPlayer ? BattleTheme.Ink : BattleTheme.Omen;
            // §5.9 J2 (#57): a fired trait lays its short cue over the blow; the tier stays as written.
            if (cue.TraitAccent) StartCoroutine(Effect(EffectId.TraitAccent, DepictionFx.TraitAccent(this, fxLayer, chest, _effects.Ms(EffectId.TraitAccent)), null));
            yield return HoldBeat(EffectId.StrikeShape,
                DepictionFx.Strike(this, fxLayer, chest, streak, cue.Intensity, !byPlayer, cue.System, _effects.Ms(EffectId.StrikeShape)), null);
            EffectId stop = strong ? EffectId.HitStopStrong : EffectId.HitStop;
            yield return HoldBeat(stop, UiTween.Wait(_effects.Ms(stop)), null);
            if (strong) StartCoroutine(Effect(EffectId.ScreenShake, ShakeArena(), null));

            // A slash with no settled HP is the swing alone: the GuardBlock / Hit cues that follow
            // carry the numbers. That is every enemy slash, and any slash Guard takes a bite out of.
            if (cue.HpAfter == Cue.Unchanged) yield break;

            DepictionFx.Burst(this, fxLayer, chest, BattleTheme.Omen, 200f + 60f * cue.Intensity);
            FloatHit(EffectId.DamageNumber, ev, cue, target, FloatNumberKind.HpLoss, cue.SideLabel);
            StartCoroutine(Effect(EffectId.HitFlash, target.Flash(BattleTheme.White, _effects.Ms(EffectId.HitFlash)), null));
            StartCoroutine(Effect(EffectId.TargetRecoil, Recoil(target, strong), null));
            yield return DrainHp(status, cue.HpAfter);
            yield return Gate(ev.Order + "-impact");
            // §5.4 順 2 / §5.5 順 8 (#499): a move right after the blow carries the player on from the
            // lunge (MoveOnFloor starts where the figure stands), so there is no step back first; the
            // figure walks in its standing pose, as the return would have left it.
            if (byPlayer && !EffectPlan.MoveFollows(ev, cue))
            {
                yield return HoldBeat(EffectId.AttackReturn, ReturnHome(attacker, attackerHome, _effects.Ms(EffectId.AttackReturn)), () => SnapHome(attacker, attackerHome));
            }
            else if (byPlayer) attacker.SetPose(FigurePose.Idle);
        }

        // ---- attack and defence beats (#76) -----------------------------------------------------

        /// <summary>
        /// The bar drops (blocking), then the grey band follows it down beside the flow. A move right
        /// after the blow starts 80 ms into the drop (EffectPlan.MoveFollows, #499); the band still
        /// waits for the bar to land, so it follows the settled width.
        /// </summary>
        private IEnumerator DrainHp(StatusBarView status, int hp)
        {
            yield return HoldBeat(EffectId.HpDrain, status.AnimateHp(hp, _effects.Ms(EffectId.HpDrain)), () => status.SetHpKeepingTrail(hp),
                played: () => StartCoroutine(Effect(EffectId.HpTrail, status.AnimateTrail(_effects.Ms(EffectId.HpTrail)), status.SnapTrail)));
        }

        /// <summary>The struck figure leans away from the blow and shakes; a strong blow does both harder.</summary>
        private IEnumerator Recoil(FigureView figure, bool strong)
        {
            float ms = _effects.Ms(EffectId.TargetRecoil);
            RectTransform body = figure.body ? figure.body.rectTransform : null;
            // The player faces right and reels to the left; the enemy the other way round.
            float lean = (strong ? 12f : 7f) * (figure.side == UnitSide.Player ? 1f : -1f);
            figure.SetPose(FigurePose.Hit);
            StartCoroutine(UiTween.Shake(figure.Rect, strong ? 20f : 10f, 3, ms));
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (body) body.localRotation = Quaternion.Euler(0f, 0f, lean * Mathf.Sin(t * Mathf.PI));
            });
            if (body) body.localRotation = Quaternion.identity;
            if (figure) figure.SetPose(FigurePose.Idle);
        }

        /// <summary>§5.3 息 on a heal (#57): the body rises a little and settles once, beside the HP beat.</summary>
        private IEnumerator Heave(FigureView figure, float ms)
        {
            RectTransform body = figure.body ? figure.body.rectTransform : null;
            if (!body) yield break;
            Vector3 home = figure.BodyHomeScale;
            yield return UiTween.Run(ms, Ease.InOut, t =>
            {
                if (body) body.localScale = new Vector3(home.x, home.y * (1f + 0.05f * Mathf.Sin(t * Mathf.PI)), home.z);
            });
            if (body) body.localScale = home;
        }

        /// <summary>A strong blow shakes the whole arena (the parent of the figures).</summary>
        private IEnumerator ShakeArena()
        {
            RectTransform arena = playerFigure ? playerFigure.Rect.parent as RectTransform : null;
            if (!arena) yield break;
            yield return UiTween.Shake(arena, 6f, 3, _effects.Ms(EffectId.ScreenShake));
        }

        /// <summary>
        /// The enemy moves into its action, shaped by its system so the slice's four actions read apart
        /// on one picture: 払 swings across, 突 drives straight in, 打 shoves with its weight, 盾 raises
        /// the haft; a step just steps (EnemyMotionShape). The stretch is taken on the body's scale at
        /// rest, so a mirrored silhouette stays facing the player (#288). The lean stays until the enemy returns.
        /// </summary>
        private IEnumerator EnemyMotion(FigureView figure, StrikeSystem system, float ms)
        {
            RectTransform body = figure.body ? figure.body.rectTransform : null;
            float toward = figure.side == UnitSide.Player ? 1f : -1f;
            Vector2 from = figure.Rect.anchoredPosition;
            float reach = EnemyMotionShape.Reach(system);
            float homeX = figure.BodyHomeScale.x;
            figure.SetPose(FigurePose.Act);
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (!figure) return;
                float bell = Mathf.Sin(t * Mathf.PI); // 0 → 1 → 0
                float rise = system == StrikeSystem.Shield ? 16f * bell : 0f;
                figure.Rect.anchoredPosition = from + new Vector2(reach * toward * t, rise);
                if (!body) return;
                (float x, float y) = FigureFacing.During(homeX, system, bell);
                body.localScale = new Vector3(x, y, 1f);
                switch (system)
                {
                    case StrikeSystem.Sweep: // wound back, then across
                        body.localRotation = Quaternion.Euler(0f, 0f, -toward * Mathf.Lerp(-18f, 14f, t) * bell);
                        break;
                    case StrikeSystem.Strike: // leaning its weight into the push
                        body.localRotation = Quaternion.Euler(0f, 0f, -toward * 10f * bell);
                        break;
                }
            });
            if (body)
            {
                body.localRotation = Quaternion.identity;
                body.localScale = figure.BodyHomeScale;
            }
        }

        private static IEnumerator ReturnHome(FigureView figure, Vector2 home, float ms)
        {
            if (!figure) yield break;
            yield return UiTween.Move(figure.Rect, figure.Rect.anchoredPosition, home, ms, Ease.InOut);
            if (figure) figure.SetPose(FigurePose.Idle);
        }

        private static void SnapHome(FigureView figure, Vector2 home)
        {
            if (!figure) return;
            figure.Rect.anchoredPosition = home;
            figure.SetPose(FigurePose.Idle);
        }

        private IEnumerator GainGuard(Cue cue, StatusBarView status, Vector2 chest, Vector2 numberAt, float numberRise)
        {
            DepictionFx.Burst(this, fxLayer, chest, BattleTheme.Guard, 220f + 40f * cue.Intensity);
            DepictionFx.FloatText(this, fxLayer, numberAt, "+" + cue.Amount, BattleTheme.Guard, FloatNumberSpec.GainFontPx(cue.Intensity), rise: numberRise);
            yield return status.PopGuard(cue.GuardAfter, _effects.Ms(EffectId.GuardGain));
        }

        private IEnumerator Reserve(Cue cue, StatusBarView status, Vector2 chest, Vector2 numberAt, float numberRise)
        {
            if (endTurn) yield return UiTween.Pop(endTurn, 200f);
            DepictionFx.Burst(this, fxLayer, chest, BattleTheme.Guard, 300f);
            DepictionFx.FloatText(this, fxLayer, numberAt, cue.Text, BattleTheme.Guard, 48, rise: numberRise);
            yield return status.PopGuard(cue.GuardAfter);
        }

        /// <summary>
        /// §5.13 順 1 (#501): the player's pips 1-3 on the stamina tag glow. An enemy's stamina is a number
        /// on its panel with no pips (§4.5), and its 構え plays as a GuardGain, so for it the beat only holds.
        /// </summary>
        private IEnumerator GlowReservePips(UnitSide side, float ms)
        {
            if (side == UnitSide.Player && _staminaTag != null) yield return _staminaTag.GlowReserve(ms);
            else yield return UiTween.Wait(ms);
        }

        // ---- §5.11 順 2 (#501): the light a unit keeps while it holds a stance -------------------

        /// <summary>
        /// Brings each figure's stance light to what <paramref name="frame"/> says: on while the unit's
        /// stance list holds one (StanceGlow), faded in or out beside the flow. A light already lit with
        /// the 型 crest (<see cref="LightStanceGlowWithCrest"/>) is kept.
        /// </summary>
        private void ApplyStanceGlows(DepictionFrame frame)
        {
            ApplyStanceGlow(playerFigure, frame.Player);
            for (int k = 0; k < _enemyCount; k++) ApplyStanceGlow(_enemies[k].Figure, EnemyScreen.UnitOf(frame, k));
        }

        private void ApplyStanceGlow(FigureView figure, UnitFrame unit)
        {
            if (!figure) return;
            StanceGlowChange change = StanceGlow.Change(figure.StanceGlowShown, unit);
            if (change == StanceGlowChange.Keep) return;
            bool on = change == StanceGlowChange.Light;
            StartCoroutine(Effect(EffectId.StanceGlow, figure.FadeStanceGlow(on, _effects.Ms(EffectId.StanceGlow)), () => figure.SetStanceGlow(on)));
        }

        /// <summary>A new battle: each light is as the first frame says at once, with no fade.</summary>
        private void SnapStanceGlows(DepictionFrame frame)
        {
            if (playerFigure) playerFigure.SetStanceGlow(StanceGlow.IsOn(frame.Player));
            for (int k = 0; k < _enemyCount; k++)
            {
                if (_enemies[k].Figure) _enemies[k].Figure.SetStanceGlow(StanceGlow.IsOn(EnemyScreen.UnitOf(frame, k)));
            }
        }

        /// <summary>
        /// §5.11 順 2: the light opens with the 型 crest (足元に紋、身体に薄い光), so a 型 beat on a unit whose
        /// list holds a stance once the event has played lights it there, not when the frame lands.
        /// </summary>
        private void LightStanceGlowWithCrest(DepictionEvent ev, Cue cue, FigureView target)
        {
            if (!target || cue.Fx != FxSystem.Form || ev.After == null) return;
            UnitFrame unit = cue.Target == UnitSide.Player ? ev.After.Player : EnemyScreen.UnitOf(ev.After, EnemyScreen.IndexOf(cue, _enemyCount));
            ApplyStanceGlow(target, unit);
        }

        private IEnumerator FireTrait(Vector2 labelAt, string text)
        {
            DepictionFx.Burst(this, fxLayer, labelAt, BattleTheme.Warm, 260f);
            DepictionFx.FloatText(this, fxLayer, labelAt, text, BattleTheme.Warm, 40, rise: 70f);
            yield return UiTween.Wait(_effects.Ms(EffectId.TraitFire));
        }

        private IEnumerator StrikeOffBonus(Cue cue, OmenBadgeView badge)
        {
            DepictionFx.FloatText(this, fxLayer, DepictionFx.PointOn(fxLayer, badge.transform) + new Vector2(0f, -64f),
                cue.Text, BattleTheme.Whiff, 40, struck: true, rise: -50f);
            yield return badge.StrikeSide(_effects.Ms(EffectId.SideBonusMiss));
        }

        // ---- status and turn beats (#77) -------------------------------------------------------

        /// <summary>A band across the arena with one line on it, faded in and out over <paramref name="ms"/>.</summary>
        private IEnumerator Banner(string text, Color color, float ms)
        {
            if (!fxLayer) yield break;
            Image plate = UiKit.Sprite(fxLayer, "Banner", ProceduralArt.White, BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.55f),
                Half, Half, new Vector2(1920f, 120f), new Vector2(0f, 80f));
            plate.raycastTarget = false;
            Text label = UiKit.Text(plate.rectTransform, "Text", 56, TextAnchor.MiddleCenter, color, text);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            CanvasGroup group = UiKit.Group(plate.rectTransform);
            group.alpha = 0f;
            yield return UiTween.Run(ms, Ease.Linear, t =>
            {
                if (group) group.alpha = t < 0.25f ? t / 0.25f : t > 0.75f ? (1f - t) / 0.25f : 1f;
            });
            if (plate) Destroy(plate.gameObject);
        }

        /// <summary>The one card at the end: 勝ち or 負け rises into place and stays (EffectId.ResultCard).</summary>
        private IEnumerator ShowResult(BattleOutcome outcome, float ms)
        {
            CanvasGroup group = BuildResultCard(outcome);
            if (!group) yield break;
            var rt = (RectTransform)group.transform;
            Vector2 at = rt.anchoredPosition;
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (!rt) return;
                group.alpha = t;
                rt.anchoredPosition = at + new Vector2(0f, -40f * (1f - t));
            });
        }

        private void ShowResultNow(BattleOutcome outcome)
        {
            CanvasGroup group = BuildResultCard(outcome);
            if (group) group.alpha = 1f;
        }

        private CanvasGroup BuildResultCard(BattleOutcome outcome)
        {
            if (!fxLayer || _resultCard) return null;
            Image plate = UiKit.Sprite(fxLayer, "ResultCard", ProceduralArt.White, BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.8f),
                Half, Half, new Vector2(520f, 220f), new Vector2(0f, 60f));
            plate.raycastTarget = false;
            Color color = outcome == BattleOutcome.Won ? BattleTheme.Warm : BattleTheme.Omen;
            Text label = UiKit.Text(plate.rectTransform, "Outcome", 72, TextAnchor.MiddleCenter, color, DepictionText.OutcomeText(outcome));
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            _resultCard = plate.gameObject;
            CanvasGroup group = UiKit.Group(plate.rectTransform);
            group.alpha = 0f;
            return group;
        }

        /// <summary>
        /// A rising number, as an effect: it runs on its own for its length, and the trace notes it,
        /// or notes that it was switched off.
        /// </summary>
        private void FloatEffect(EffectId id, Vector2 at, string text, Color color, int fontSize)
        {
            if (!_effects.IsOn(id))
            {
                Trace?.Skip(id, CurrentOrder);
                return;
            }
            if (Trace != null) StartCoroutine(TraceFor(id, _effects.Ms(id)));
            DepictionFx.FloatText(this, fxLayer, at, text, color, fontSize);
        }

        /// <summary>
        /// battle-visual-v1 §5.5 / battle_ui_ux_v2 §5.0, §5.6 順 2〜3 (#455): a hit's number on its plate,
        /// rising from the chest to the head of <paramref name="target"/>. The amount is the cue's; the
        /// pair (Guard left, HP right, 24 px apart) is read off the order the script wrote the cues in.
        /// </summary>
        private void FloatHit(EffectId id, DepictionEvent ev, Cue cue, FigureView target, FloatNumberKind kind, string label)
        {
            if (!_effects.IsOn(id))
            {
                Trace?.Skip(id, CurrentOrder);
                return;
            }
            if (Trace != null) StartCoroutine(TraceFor(id, _effects.Ms(id)));
            int index = ev.Cues.IndexOf(cue);
            bool paired = kind == FloatNumberKind.GuardLoss
                ? FloatNumberSpec.GuardPairsWithHp(ev.Cues, index)
                : FloatNumberSpec.HpPairsWithGuard(ev.Cues, index);
            DepictionFx.FloatPlate(this, fxLayer, DepictionFx.PointOn(fxLayer, target.transform), target.Height, paired,
                kind, cue.Amount, label, _effects.Ms(id));
        }

        private IEnumerator TraceFor(EffectId id, float ms)
        {
            int handle = Trace.Begin(id, CurrentOrder);
            yield return UiTween.Wait(ms);
            Trace.End(handle);
        }

        /// <summary>Steps toward the opponent by <paramref name="pixels"/> (negative steps back).</summary>
        private IEnumerator Lunge(FigureView figure, float pixels, float ms)
        {
            float direction = figure.side == UnitSide.Player ? 1f : -1f;
            Vector2 from = figure.Rect.anchoredPosition;
            Vector2 to = from + new Vector2(pixels * direction, 0f);
            figure.SetPose(FigurePose.Act);
            yield return UiTween.Move(figure.Rect, from, to, ms, Ease.Out);
        }

        /// <summary>
        /// The walk's end state at once, on the floor: both figures on their cells. The gap number is
        /// the recount's to settle (EffectId.GapRecount, #586).
        /// </summary>
        private void SnapOnFloor(FigureView figure, Cue cue)
        {
            figure.transform.position = _floor.FeetOf(cue.PlayerCellAfter, PlayerSize);
            SetPlayerHome(figure);
            int moved = EnemyScreen.MovedUnit(cue, _enemyCount, out int cell);
            if (moved < 0) return;
            EnemyParts foe = EnemyAt(moved);
            if (!foe.Figure) return;
            foe.Home = AnchoredAt(foe.Figure.Rect, _floor.FeetOf(cell, EnemySizeOf(moved)));
            SnapHome(foe.Figure, foe.Home);
        }

        /// <summary>
        /// The move is drawn on the floor: one is shown and the cue says where both stand after it
        /// (EffectPlan.MovesOnFloor). Every source draws the floor (#577), so this only fails on a script bug.
        /// </summary>
        private bool OnFloor(Cue cue)
        {
            return cue != null && _floor != null && _floor.Shown && EffectPlan.MovesOnFloor(cue);
        }

        private int PlayerSize => _source != null && _source.Frame != null ? Mathf.Max(1, _source.Frame.Floor.PlayerSize) : 1;

        /// <summary>How many cells enemy <paramref name="k"/> takes (the script's), at least one.</summary>
        private int EnemySizeOf(int k) => FrameNow != null ? EnemyScreen.SizeOf(FrameNow, k) : 1;

        /// <summary>
        /// §4.2 (#242) / §5.7 順 1 (#586): a move on the floor — the player walks to its new cell, the
        /// enemy that moved (a push, a pull, its own step; EnemyScreen.MovedUnit) slides to its own, 160 ms
        /// a cell (EffectId.FloorMove). The gap number is counted again after it (EffectId.GapRecount).
        /// </summary>
        private IEnumerator MoveOnFloor(FigureView figure, Cue cue, int cells)
        {
            Vector3 from = figure.transform.position;
            Vector3 to = _floor.FeetOf(cue.PlayerCellAfter, PlayerSize);
            int moved = EnemyScreen.MovedUnit(cue, _enemyCount, out int cell);
            EnemyParts foe = moved >= 0 ? EnemyAt(moved) : null;
            FigureView walker = foe != null ? foe.Figure : null;
            Vector2 enemyFrom = walker ? walker.Rect.anchoredPosition : Vector2.zero;
            Vector2 enemyTo = walker ? AnchoredAt(walker.Rect, _floor.FeetOf(cell, EnemySizeOf(moved))) : enemyFrom;
            bool enemyMoves = walker && (enemyTo - foe.Home).sqrMagnitude > 0.25f;
            // §5.4 順 2 (#587): the enemy's own move after its blow starts 80 ms after the number, while
            // the struck player still reels (TargetRecoil); a player whose cell stays is left to it.
            bool playerStays = enemyMoves && !cue.Pushed && _playerHomeSet && to == _playerHome;
            if (enemyMoves) foe.Home = enemyTo;
            SetPlayerHome(figure, to); // the panels set off for the new cells as the figures do
            // 160 ms a cell (EffectSpec.TotalMs of FloorMove); a push starts fast and settles.
            yield return UiTween.Run(_effects.WaitMs(EffectId.FloorMove, cells), cue.Pushed ? Ease.Out : Ease.InOut, t =>
            {
                if (figure && !playerStays) figure.transform.position = Vector3.LerpUnclamped(from, to, t);
                if (enemyMoves && walker) walker.Rect.anchoredPosition = Vector2.LerpUnclamped(enemyFrom, enemyTo, t);
            });
        }

        /// <summary>
        /// Rebuilds the hand from the settled faces, and flies the <paramref name="drawn"/> newest cards
        /// (the right end of the fan) in from the draw pile one stagger apart (EffectId.CardDraw).
        /// A draw that ran the pile out (<paramref name="rebuildAfter"/> ≥ 0, battle_ui_ux_v2 §3.14 /
        /// §5.1 順 6, #455) slides the discard pile onto the draw pile once the cards left on the old pile
        /// have gone, and the rest follow it (EffectPlan.DrawCardStartMs). The flow is held for the plan's
        /// PileRebuild and CardDraw waits in turn, and the draw ends on their sum.
        /// </summary>
        private IEnumerator DrawHand(List<CardFace> faces, int drawn, int rebuildAfter)
        {
            bool rebuild = drawn > 0 && rebuildAfter >= 0;
            if (rebuild && !(_effects.IsOn(EffectId.PileRebuild) && _effects.IsOn(EffectId.CardDraw)))
            {
                // With either beat off there is no draw to slide into: the slide (or its skip) comes first.
                yield return HoldBeat(EffectId.PileRebuild, SlideDiscardOntoDraw(), null);
                rebuild = false;
            }
            BuildHand(faces);
            int count = Mathf.Clamp(drawn, 0, _hand.Count);
            if (count == 0) yield break;
            if (!rebuild)
            {
                yield return HoldBeat(EffectId.CardDraw, FlyDrawn(_hand.Count - count, Cue.Unchanged), null, count);
                yield break;
            }
            bool flown = false;
            StartCoroutine(Then(FlyDrawn(_hand.Count - count, rebuildAfter), () => flown = true));
            yield return HoldBeat(EffectId.PileRebuild, Nothing(), null);
            yield return HoldBeat(EffectId.CardDraw, Until(() => flown), null, count);
        }

        private IEnumerator FlyDrawn(int first, int rebuildAfter)
        {
            Vector3 from = deckPoint ? deckPoint.position : handArea.position;
            var flying = new List<(CardView card, Vector3 home)>();
            for (int i = first; i < _hand.Count; i++)
            {
                CardView card = _hand[i];
                flying.Add((card, card.transform.position));
                card.transform.position = from;
                card.group.alpha = 0f;
            }
            float ms = _effects.Ms(EffectId.CardDraw);
            float clock = 0f;
            for (int i = 0; i < flying.Count; i++)
            {
                if (i == rebuildAfter)
                {
                    // The old pile is empty: the discard pile slides onto it, and the next card leaves the new one.
                    float slideAt = EffectPlan.RebuildStartMs(rebuildAfter, _effects);
                    yield return UiTween.Wait(slideAt - clock);
                    clock = slideAt;
                    StartCoroutine(SlideDiscardOntoDraw());
                }
                float startAt = EffectPlan.DrawCardStartMs(i, rebuildAfter, _effects);
                yield return UiTween.Wait(startAt - clock);
                clock = startAt;
                StartCoroutine(FlyCard(flying[i].card, from, flying[i].home, ms, 0f, 1f, Ease.Out));
            }
            yield return UiTween.Wait(ms);
        }

        private static IEnumerator Then(IEnumerator play, System.Action done)
        {
            yield return play;
            done();
        }

        private static IEnumerator Nothing()
        {
            yield break;
        }

        private static IEnumerator Until(System.Func<bool> done)
        {
            while (!done()) yield return null;
        }

        /// <summary>Turn end: the whole hand flies to the discard pile one stagger apart (EffectId.HandDiscard).</summary>
        private IEnumerator DiscardHand()
        {
            int count = _hand.Count;
            if (count > 0) yield return HoldBeat(EffectId.HandDiscard, FlyDiscarded(), null, count);
            ClearHand();
        }

        private IEnumerator FlyDiscarded()
        {
            Vector3 to = discardPoint ? discardPoint.position : handArea.position;
            float ms = _effects.Ms(EffectId.HandDiscard);
            float stagger = _effects.StaggerMs(EffectId.HandDiscard);
            var cards = new List<CardView>(_hand);
            for (int i = 0; i < cards.Count; i++)
            {
                CardView card = cards[i];
                if (card) StartCoroutine(FlyCard(card, card.transform.position, to, ms, 1f, 0f, Ease.In));
                if (i < cards.Count - 1) yield return UiTween.Wait(stagger);
            }
            yield return UiTween.Wait(ms);
        }

        private static IEnumerator FlyCard(CardView card, Vector3 from, Vector3 to, float ms, float alphaFrom, float alphaTo, Ease ease)
        {
            yield return UiTween.Run(ms, ease, t =>
            {
                if (!card) return;
                card.transform.position = Vector3.LerpUnclamped(from, to, t);
                card.group.alpha = Mathf.Lerp(alphaFrom, alphaTo, t);
            });
        }

        /// <summary>
        /// The played card's ghost drifts from where the card vanished onto the pile the core put it in
        /// (DepictionEvent.PlayedTo): the discard pile (EffectId.CardToDiscard) at once, or for a stance
        /// card the exile pile, 220 ms in on the beat 600 counted from the release's start
        /// (EffectId.CardToExile, battle_ui_ux_v2 §5.11 順 4, #455). The release has already run its
        /// CardRelease length, so only the rest of the 600 is waited, and nothing is drawn while it is.
        /// It runs beside the event's beats and holds nothing up.
        /// </summary>
        private IEnumerator GhostToPile(Vector2 from, PileKind pile)
        {
            bool exiled = pile == PileKind.Exile;
            if (exiled) yield return UiTween.Wait(Mathf.Max(0f, EffectCatalog.CardToExileDelayMs - _effects.Ms(EffectId.CardRelease)));
            yield return Effect(exiled ? EffectId.CardToExile : EffectId.CardToDiscard, FlyGhostTo(from, pile), null);
        }

        private IEnumerator FlyGhostTo(Vector2 from, PileKind pile)
        {
            bool exiled = pile == PileKind.Exile;
            if (!fxLayer) yield break;
            Vector2 to;
            if (exiled && _piles != null && _piles.Shown) to = PointOnFx(_piles.MiddleOf(PileKind.Exile));
            else if (!exiled && discardPoint) to = DepictionFx.PointOn(fxLayer, discardPoint);
            else yield break;
            EffectId id = exiled ? EffectId.CardToExile : EffectId.CardToDiscard;
            Color tint = BattleTheme.WithAlpha(BattleTheme.Ink, 0.55f);
            Image ghost = UiKit.Sprite(fxLayer, exiled ? "ExileGhost" : "DiscardGhost", ProceduralArt.White, tint, Half, Half, new Vector2(76f, 104f), from);
            ghost.raycastTarget = false;
            RectTransform rt = ghost.rectTransform;
            yield return UiTween.Run(_effects.Ms(id), exiled ? Ease.In : Ease.InOut, t =>
            {
                if (!rt) return;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                ghost.color = BattleTheme.WithAlpha(tint, tint.a * (1f - t));
            });
            if (ghost) Destroy(ghost.gameObject);
        }

        /// <summary>A world point as an offset from the fx layer's centre, as DepictionFx.PointOn gives a transform's.</summary>
        private Vector2 PointOnFx(Vector3 world)
        {
            Vector2 local = fxLayer.InverseTransformPoint(world);
            return local - fxLayer.rect.center;
        }

        /// <summary>
        /// battle_ui_ux_v2 §3.14 山札切れ (#455): the discard pile slides onto the draw pile in 200 ms (out)
        /// in the middle of the draw, once the old pile is empty. A card back of the piles' 64×84 crosses
        /// from one to the other; the piles' own thickness follows with the event's settled frame. The
        /// exile pile does not move.
        /// </summary>
        private IEnumerator SlideDiscardOntoDraw()
        {
            if (!fxLayer || _piles == null || !_piles.Shown) yield break;
            Vector2 from = PointOnFx(_piles.MiddleOf(PileKind.Discard));
            Vector2 to = PointOnFx(_piles.MiddleOf(PileKind.Draw));
            RectTransform sheet = UiKit.Point(fxLayer, "RebuildSheet", Half, Half, new Vector2(PileLayout.Width, PileLayout.Height), from);
            VisualArt.Panel(sheet, "Back", BattleTheme.CardGround, 5);
            VisualArt.Ring(sheet, "Edge", BattleTheme.Steel, 5, 1);
            yield return UiTween.Run(_effects.Ms(EffectId.PileRebuild), Ease.Out, t =>
            {
                if (sheet) sheet.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
            });
            if (sheet) Destroy(sheet.gameObject);
        }

        // ---- effects (#75-#77) -----------------------------------------------------------------

        /// <summary>The event the running effect belongs to: the one playing, or the next one while the player acts.</summary>
        private int CurrentOrder => EventSeconds.Count + 1;

        /// <summary>
        /// Plays one effect by id. When it is on, <paramref name="play"/> runs and the trace times it.
        /// When it is off, <paramref name="settle"/> puts the end state on screen at once, the trace
        /// records the skip, and the caller goes on without waiting.
        /// </summary>
        private IEnumerator Effect(EffectId id, IEnumerator play, System.Action settle, int count = 1)
        {
            if (!_effects.IsOn(id))
            {
                settle?.Invoke();
                Trace?.Skip(id, CurrentOrder);
                yield break;
            }
            int handle = Trace != null ? Trace.Begin(id, CurrentOrder, count) : -1;
            yield return play;
            if (handle >= 0) Trace.End(handle);
        }

        // ---- the plan's waits (#495) ------------------------------------------------------------

        /// <summary>The waits EffectPlan lists for the event playing now, taken in order by <see cref="HoldBeat"/>.</summary>
        private readonly Queue<EffectStep> _planned = new Queue<EffectStep>();
        /// <summary>The event <see cref="_planned"/> belongs to, or null between events.</summary>
        private DepictionEvent _plannedEvent;

        /// <summary>
        /// An event starts: the flow will be held at exactly the steps EffectPlan.StepsOf lists for it,
        /// the same steps the EditMode tests add up against the 2.0 s cap.
        /// </summary>
        private void BeginPlan(DepictionEvent ev)
        {
            _planned.Clear();
            _plannedEvent = ev;
            foreach (EffectStep step in EffectPlan.StepsOf(ev)) _planned.Enqueue(step);
        }

        /// <summary>An event has played its beats. A planned wait the screen never held means the two have drifted apart.</summary>
        private void EndPlan()
        {
            if (_plannedEvent != null && _planned.Count > 0)
            {
                var left = new List<string>();
                foreach (EffectStep step in _planned) left.Add(step.Id + " x" + step.Count);
                Debug.LogError("[Depiction] event " + _plannedEvent.Order + " " + _plannedEvent.Title
                    + ": EffectPlan lists waits the screen did not hold: " + string.Join(", ", left));
            }
            _planned.Clear();
            _plannedEvent = null;
        }

        /// <summary>The plan's next step, which must be the beat the screen is about to wait for.</summary>
        private EffectStep TakePlanned(EffectId id, int count)
        {
            if (_plannedEvent == null) return new EffectStep(id, count); // a beat outside any event: nothing to hold it to
            string where = "[Depiction] event " + _plannedEvent.Order + " " + _plannedEvent.Title + ": ";
            if (_planned.Count == 0 || _planned.Peek().Id != id)
            {
                Debug.LogError(where + "the screen waits for " + id + " where EffectPlan lists "
                    + (_planned.Count > 0 ? _planned.Peek().Id.ToString() : "nothing more"));
                return new EffectStep(id, count);
            }
            EffectStep planned = _planned.Dequeue();
            if (planned.Count != count)
            {
                Debug.LogError(where + id + " plays " + count + " items where EffectPlan lists " + planned.Count);
            }
            return planned;
        }

        /// <summary>
        /// Plays one beat the event waits for (#495). The flow is held for the plan's wait for this step
        /// (EffectPlan.WaitMs: the number EffectPlan.BlockingMs adds up for the tests) while
        /// <paramref name="play"/> runs beside it. The animation is built from the same catalog length;
        /// when it runs a frame or so past the wait (UiTween ends on the first frame after its length),
        /// the flow lets it finish, so nothing on screen is cut short. A step the plan gives a lead
        /// (EffectStep.LeadMs, §5.4, #499) holds the flow for the lead only and plays on beside what
        /// follows. Off, the beat settles at once and holds nothing up, like <see cref="Effect"/>.
        /// <paramref name="played"/> runs once the beat has played out (or settled), lead or not.
        /// Beats that run beside the flow keep using Effect.
        /// </summary>
        private IEnumerator HoldBeat(EffectId id, IEnumerator play, System.Action settle, int count = 1, System.Action played = null)
        {
            EffectStep step = TakePlanned(id, count);
            if (!_effects.IsOn(id))
            {
                settle?.Invoke();
                Trace?.Skip(id, CurrentOrder);
                played?.Invoke();
                yield break;
            }
            int handle = Trace != null ? Trace.Begin(id, CurrentOrder, step.Count) : -1;
            if (step.LeadMs.HasValue)
            {
                StartCoroutine(PlayOn(play, handle, played));
                yield return UiTween.Wait(EffectPlan.WaitMs(step, _effects));
                yield break;
            }
            bool waited = false;
            StartCoroutine(WaitThen(EffectPlan.WaitMs(step, _effects), () => waited = true));
            yield return play;
            while (!waited) yield return null;
            if (handle >= 0) Trace.End(handle);
            played?.Invoke();
        }

        /// <summary>The rest of a beat with a lead, beside the flow: it plays out, then is timed and handed on.</summary>
        private IEnumerator PlayOn(IEnumerator play, int handle, System.Action played)
        {
            yield return play;
            if (handle >= 0) Trace.End(handle);
            played?.Invoke();
        }

        private static IEnumerator WaitThen(float ms, System.Action done)
        {
            yield return UiTween.Wait(ms);
            done();
        }

        /// <summary>The settled event in the log, beside what the plan says it should take at the speed in force.</summary>
        private void LogSettled(DepictionEvent ev, float seconds)
        {
            float planned = EffectPlan.BlockingMs(ev, _effects, _speed);
            Debug.Log("[Depiction] event " + ev.Order + " " + ev.Title + " settled in " + seconds.ToString("0.00")
                + " s (EffectPlan " + planned.ToString("0") + " ms)");
        }

        // ---- frame -> screen ------------------------------------------------------------------

        /// <summary>
        /// #606: hands the script's layer to the backdrop (battle-visual-v1 §2.4), so it no longer has to
        /// read the corner info's text back. BackdropView attaches itself after the scene loads (#89), so the
        /// first frames may come before it: it is looked for again until it is there. Show ignores the
        /// layer it already shows, so every frame may call it.
        /// </summary>
        private void ShowBackdropLayer(int layer)
        {
            if (layer < 1 || !backdrop) return;
            if (!_backdropView) _backdropView = FindAnyObjectByType<Backdrop.View.BackdropView>();
            if (_backdropView) _backdropView.Show(layer);
        }

        private void ApplyFrame(DepictionFrame frame)
        {
            if (cornerInfo) cornerInfo.Bind(frame.Corner);
            ShowBackdropLayer(frame.Corner.Floor);
            EnsureEnemies(EnemyScreen.CountOf(frame));
            // #288: the art is the script's to name; the figure looks it up and keeps it while the id stays.
            playerFigure.ShowArt(frame.Player.ArtId);
            playerFigure.SetDown(frame.Player.Down);
            // §4.3 (#455): 226 px tall, 264 for an elite on two cells (the script's sizes).
            playerFigure.StandTall(FigureFrame.HeightFor(frame.Floor.PlayerSize));
            for (int k = 0; k < _enemyCount; k++)
            {
                UnitFrame unit = EnemyScreen.UnitOf(frame, k);
                _enemies[k].Figure.ShowArt(unit.ArtId);
                _enemies[k].Figure.SetDown(unit.Down);
                _enemies[k].Figure.StandTall(FigureFrame.HeightFor(EnemyScreen.SizeOf(frame, k)));
            }
            _staminaTag?.SetDivider(frame.StanceDivider); // before the bind below shows the stamina
            _staminaTag?.SetNextRecovery(frame.Player.NextRecovery); // §4.7 (#455): the dashed dots
            ApplyPiles(frame.Piles);
            _stanceLists?.Bind(frame); // §4.6 (#455)
            ApplyStanceGlows(frame); // §5.11 順 2 (#501): the body's light while the list holds a stance
            playerStatus.Bind(frame.Player);
            playerFigure.SetRange(frame.Player.HasRange, frame.Player.RangeGlyph);
            for (int k = 0; k < _enemyCount; k++)
            {
                UnitFrame unit = EnemyScreen.UnitOf(frame, k);
                _enemies[k].Status.Bind(unit);
                // §2.2 の 6 (#589): the panel of an enemy that fell while others stood stays, grey.
                _enemies[k].Status.SetFallen(SeveralEnemies && unit.Down);
                _enemies[k].Figure.SetRange(unit.HasRange, unit.RangeGlyph);
            }
            SetPlayerHome(playerFigure);
            ApplyFloor(frame); // §4.2: the figures stand on their cells (#577: every source draws the floor)
            for (int k = 0; k < _enemyCount; k++) _enemies[k].Badge.Bind(EnemyScreen.OmenOf(frame, k));
            if (SeveralEnemies) DrawNearerInFront(frame);
            bool hasHint = !string.IsNullOrEmpty(frame.StanceHint);
            if (stanceHintText) stanceHintText.text = frame.StanceHint;
            if (stanceHintIcon) stanceHintIcon.enabled = hasHint;
            _frameStanceHint = frame.StanceHint ?? "";
            ShowReserveHint(_dragging); // §4.9 (#455)
            _played?.Bind(frame.Played, this);
            ShowEnemyTurn(frame.EnemyTurn); // §5.5 (#455): sinks once the turn end has played
            if (!SameHand(frame.Hand)) BuildHand(frame.Hand);
            else for (int i = 0; i < _hand.Count; i++) _hand[i].Bind(frame.Hand[i]);
            LayoutHand();
            ShowHpLoss(_dragging); // §4.5 (#455): the bar was bound again, and so was the held card's face
        }

        private bool SameHand(List<CardFace> faces)
        {
            if (faces.Count != _hand.Count) return false;
            for (int i = 0; i < faces.Count; i++)
            {
                if (_hand[i].CardId != faces[i].Id) return false;
            }
            return true;
        }

        private void BuildHand(List<CardFace> faces)
        {
            ClearHand();
            foreach (CardFace face in faces)
            {
                CardView card = Instantiate(cardPrefab, handArea);
                card.Bind(face);
                card.SetDimmed(false);
                card.DragBegan += OnDragBegan;
                card.DragMoved += OnDragMoved;
                card.DragEnded += OnDragEnded;
                _hand.Add(card);
            }
            LayoutHand();
        }

        private void ClearHand()
        {
            foreach (CardView card in _hand)
            {
                if (card) Destroy(card.gameObject);
            }
            _hand.Clear();
        }

        private void LayoutHand()
        {
            _hovered = null;
            _hoverWeight.Clear();
            for (int i = 0; i < _hand.Count; i++)
            {
                PlaceAtHome(_hand[i], i);
            }
            RestoreCardOrder();
        }

        /// <summary>Cards draw left to right, after anything else placed under the hand area by hand.</summary>
        private void RestoreCardOrder()
        {
            int first = Mathf.Max(0, handArea.childCount - _hand.Count);
            for (int i = 0; i < _hand.Count; i++)
            {
                if (_hand[i]) _hand[i].transform.SetSiblingIndex(first + i);
            }
        }

        /// <summary>
        /// Resting place of the i-th card (battle-visual-v1 §4.8, #242): centred on x 960, the middle
        /// card's top at y 896 so only its upper 184 px show, the k-th from the middle k² × 3.2 px lower,
        /// tilted 2.5° a step about the middle of its bottom edge. Worked out on the 1920×1080 screen and
        /// put into the hand area's space, so where the hand area stands in the scene no longer matters.
        /// </summary>
        private void HomeOf(int index, out Vector2 position, out float degrees)
        {
            FanPlace rest = HandSpec.Rest(_hand.Count, index, HandFan.Spacing(_hand.Count, cardSpacing), fanDegreesPerCard);
            position = HandPoint(new Vector2(rest.X, rest.Y));
            degrees = rest.Degrees;
        }

        /// <summary>§4.8 ホバー: upright, in front, raised until its bottom is 16 px above the screen's (top at y 760).</summary>
        private Vector2 HoverPlace(int index)
        {
            FanPlace hover = HandSpec.Hover(_hand.Count, index, HandFan.Spacing(_hand.Count, cardSpacing));
            return HandPoint(new Vector2(hover.X, hover.Y));
        }

        /// <summary>
        /// A point given on the 1920×1080 screen (x from the left, y from the top) as an anchored position
        /// under the hand area. x is kept from the screen's centre and y from its bottom, so a screen of
        /// another shape keeps the hand on its bottom edge.
        /// </summary>
        private Vector2 HandPoint(Vector2 screen)
        {
            Canvas canvas = handArea ? handArea.GetComponentInParent<Canvas>() : null;
            if (!canvas) return new Vector2(screen.x - BattleTheme.RefWidth * 0.5f, BattleTheme.RefHeight * 0.5f - screen.y);
            var root = (RectTransform)canvas.rootCanvas.transform;
            Rect area = root.rect;
            var local = new Vector2(area.center.x + (screen.x - BattleTheme.RefWidth * 0.5f), area.yMin + (BattleTheme.RefHeight - screen.y));
            Vector3 inHand = handArea.InverseTransformPoint(root.TransformPoint(local));
            return new Vector2(inHand.x, inHand.y);
        }

        private void PlaceAtHome(CardView card, int index)
        {
            HomeOf(index, out Vector2 position, out float degrees);
            card.Rect.anchoredPosition = position;
            card.Rect.localScale = Vector3.one;
            card.Rect.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        // ---- hover ----------------------------------------------------------------------------

        /// <summary>
        /// Lifts the card under the pointer and brings it to the front. A card starts its hover from its
        /// resting place, so it does not flicker when the pointer sits on its bottom edge; once hovered,
        /// its raised place counts as well (<see cref="HoveredRectContains"/>).
        /// </summary>
        private void UpdateHover()
        {
            if (_source == null) return;
            if (_busy || _dragging != null)
            {
                // Tweens own the cards now; they end in LayoutHand, which also drops the hover.
                _hovered = null;
                _hoverWeight.Clear();
                HideHoverPanels();
                return;
            }

            CardView over = CardUnderPointer();
            if (over != _hovered)
            {
                RestoreCardOrder();
                if (over) over.transform.SetAsLastSibling();
                _hovered = over;
                if (_panels != null) _panels.HideDetail(); // the detail belongs to the card it was opened on
                // EffectId.CardHover: timed from the moment a card starts to rise until it is fully up.
                _hoverHandle = -1;
                if (over && Trace != null)
                {
                    if (_effects.IsOn(EffectId.CardHover)) _hoverHandle = Trace.Begin(EffectId.CardHover, CurrentOrder);
                    else Trace.Skip(EffectId.CardHover, CurrentOrder);
                }
            }

            float hoverMs = _effects.Ms(EffectId.CardHover);
            // The battle speed (#348), like every tween. Input timing is not scaled (§6.2 rule 1).
            float step = hoverMs > 0f ? Time.unscaledDeltaTime * FrameMotionSpeed * 1000f / hoverMs : 1f;
            for (int i = 0; i < _hand.Count; i++)
            {
                CardView card = _hand[i];
                if (!card) continue;
                _hoverWeight.TryGetValue(card, out float weight);
                float next = Mathf.MoveTowards(weight, card == over ? 1f : 0f, step);
                if (next == weight && weight == 0f) continue;
                _hoverWeight[card] = next;
                if (card == over && next >= 1f && _hoverHandle >= 0)
                {
                    Trace.End(_hoverHandle);
                    _hoverHandle = -1;
                }
                float eased = next * next * (3f - 2f * next);
                HomeOf(i, out Vector2 home, out float degrees);
                // §4.8 / §6.2: upright and raised to the hover height; the size never changes.
                card.Rect.anchoredPosition = Vector2.Lerp(home, HoverPlace(i), eased);
                card.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(degrees, 0f, eased));
                card.Rect.localScale = Vector3.one;
            }
            UpdateHoverPanels(over);
        }

        // ---- the panels around a hovered card (battle-visual-v1 §5.1 / §7.2 / §7.4, #242) ----

        private CardPanels _panels;

        private CardPanels Panels
        {
            get
            {
                if (_panels != null || !handArea) return _panels;
                Canvas canvas = handArea.GetComponentInParent<Canvas>();
                if (canvas) _panels = new CardPanels((RectTransform)canvas.rootCanvas.transform);
                return _panels;
            }
        }

        /// <summary>
        /// Follows the hovered card with its hover line and, when the script says nobody is in its reach,
        /// the reason on its right. A right click on it opens the detail (§7.4); moving off it closes it.
        /// </summary>
        private void UpdateHoverPanels(CardView over)
        {
            CardPanels panels = Panels;
            if (panels == null) return;
            if (!over)
            {
                HideHoverPanels();
                return;
            }
            if (TryRightPress()) panels.ToggleDetail(over, cardPrefab);
            if (panels.DetailShown) panels.HideHover();
            else
            {
                panels.ShowHover(over);
                // §7.1 (#455): a pointer on an underlined term shows what it means, above the word.
                if (TryPointer(out Vector2 pointer) && over.TryTermAt(pointer, out TermMark term, out RectTransform word)) panels.ShowTerm(word, term);
                else panels.HideTerm();
            }
        }

        private void HideHoverPanels()
        {
            if (_panels == null) return;
            _panels.HideHover();
            _panels.HideDetail();
        }

        /// <summary>True on the frame the right button went down (§7.4 右クリック).</summary>
        private static bool TryRightPress()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(1);
#endif
        }

        /// <summary>True on the frame Esc went down.</summary>
        private static bool TryEscape()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private CardView CardUnderPointer()
        {
            if (!TryPointer(out Vector2 pointer)) return null;
            // A card under the open pile list (#455) is not hovered through it.
            if (_pileList != null && _pileList.Covers(pointer)) return null;
            // The hovered card is drawn in front, so it wins where two cards overlap.
            // It is also caught on its raised part (§4.8: up to 136 px above its resting place), so moving
            // onto what the hover lifted keeps the hover, and the right click (§7.4) works there too.
            if (_hovered && _hovered.Interactable && HoveredRectContains(_hovered, _hand.IndexOf(_hovered), pointer)) return _hovered;
            for (int i = _hand.Count - 1; i >= 0; i--)
            {
                CardView card = _hand[i];
                if (card && card.Interactable && RestingRectContains(card, i, pointer)) return card;
            }
            return null;
        }

        /// <summary>
        /// True when the pointer is over the card as it would lie at rest: at its fan place, tilted,
        /// unlifted and unscaled. Undoing only the lift would leave the hover scale in the test, and
        /// the rim that scale adds would flicker the hover on and off.
        /// </summary>
        private bool RestingRectContains(CardView card, int index, Vector2 pointer)
        {
            if (index < 0) return false;
            HomeOf(index, out Vector2 home, out float degrees);
            return RectAtContains(card, home, degrees, pointer);
        }

        /// <summary>
        /// The hovered card's test: its resting place or its raised one (<see cref="HoverPlace"/>,
        /// upright). Testing only the resting place dropped the hover once the pointer followed the
        /// card up onto the part the hover lifts (§4.8: 136 px).
        /// </summary>
        private bool HoveredRectContains(CardView card, int index, Vector2 pointer)
        {
            if (index < 0) return false;
            return RestingRectContains(card, index, pointer) || RectAtContains(card, HoverPlace(index), 0f, pointer);
        }

        /// <summary>True when the pointer is over the card as it would stand at <paramref name="place"/>, tilted <paramref name="degrees"/>, unscaled.</summary>
        private bool RectAtContains(CardView card, Vector2 place, float degrees, Vector2 pointer)
        {
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(handArea, pointer, null, out Vector3 world)) return false;
            // anchoredPosition and localPosition differ by a constant, so the localPosition at that place
            // is the current one moved by how far the card sits from it.
            Vector3 atLocal = card.Rect.localPosition + (Vector3)(place - card.Rect.anchoredPosition);
            Vector3 inHand = handArea.InverseTransformPoint(world);
            Vector3 inCard = Quaternion.Inverse(Quaternion.Euler(0f, 0f, degrees)) * (inHand - atLocal);
            return card.Rect.rect.Contains(inCard);
        }

        /// <summary>True on the frame the pointer was pressed, with where it was pressed.</summary>
        private static bool TryPointerPress(out Vector2 position)
        {
            position = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Pointer pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return false;
            position = pointer.position.ReadValue();
            return true;
#else
            if (!Input.GetMouseButtonDown(0)) return false;
            position = Input.mousePosition;
            return true;
#endif
        }

        private static bool TryPointer(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Pointer pointer = UnityEngine.InputSystem.Pointer.current;
            position = pointer != null ? pointer.position.ReadValue() : Vector2.zero;
            bool present = pointer != null;
#else
            position = Input.mousePosition;
            bool present = Input.mousePresent;
#endif
            // A pointer that left the game view keeps reporting its last position; that is not a hover.
            return present && new Rect(0f, 0f, Screen.width, Screen.height).Contains(position);
        }

        private void SetHandInteractable(bool on)
        {
            foreach (CardView card in _hand)
            {
                card.Interactable = on;
            }
            if (!on && _refusal != null)
            {
                StopCoroutine(_refusal);
                _refusal = null;
                _refusalLine?.Hide();
            }
            RefreshPlayableLook();
        }

        // ---- guide ----------------------------------------------------------------------------

        /// <summary>
        /// Dims every card the source would refuse right now and prints the source's guide line.
        /// Which cards those are, and what the line says, are the source's answer; the View shows it.
        /// </summary>
        private void RefreshPlayableLook()
        {
            bool waiting = _source != null && !_busy && _source.WaitingForPlayer
                && _hand.Exists(c => c && c.Interactable);
            foreach (CardView card in _hand)
            {
                if (!card || card == _dragging) continue;
                bool dim = waiting && _source.Inspect(card.CardId) != PlayVerdict.Accepted;
                if (dim == card.Dimmed) continue;
                CardView dimmed = card;
                StartCoroutine(Effect(EffectId.UnpayableDim, dimmed.FadeDimmed(dim, _effects.Ms(EffectId.UnpayableDim)), () => dimmed.SetDimmed(dim)));
            }
            if (_refusal != null || !handGuide) return;
            handGuide.color = BattleTheme.Ink;
            handGuide.text = _source != null && (waiting || _source.Finished) ? _source.GuideText : "";
        }

        /// <summary>Says why a released card went back to the hand, then returns to the guide line.</summary>
        private void ShowRefusal(CardView card, PlayVerdict verdict, int target)
        {
            if ((!handGuide && _refusalLine == null) || _source == null) return;
            string text = target >= 0 && TargetedSource != null
                ? TargetedSource.RefusalText(card.CardId, verdict, target)
                : _source.RefusalText(card.CardId, verdict);
            if (string.IsNullOrEmpty(text)) return;
            if (_refusal != null) StopCoroutine(_refusal);
            _refusal = StartCoroutine(Refusal(text));
        }

        /// <summary>
        /// battle-visual-v1 §5.4 (#455): the reason stands on its own line above the hand for
        /// <see cref="refusalSeconds"/> (3.5 s), and the guide line steps aside meanwhile. A scene
        /// without the canvas the line is made on keeps the old guide-line text.
        /// </summary>
        private IEnumerator Refusal(string text)
        {
            if (_refusalLine != null)
            {
                _refusalLine.Show(text);
                if (handGuide) handGuide.text = "";
            }
            else
            {
                handGuide.color = BattleTheme.Omen;
                handGuide.text = text;
            }
            yield return new WaitForSecondsRealtime(refusalSeconds);
            _refusal = null;
            _refusalLine?.Hide();
            RefreshPlayableLook();
        }

        // ---- drag ---------------------------------------------------------------------------

        // The EventSystem hands the drag to whatever it hits, but a lifted card leaves the strip at the
        // bottom of its resting place where the right neighbour is hit instead. The hover decides which
        // card is picked up, so the card that looks lifted is the one that moves; the move and end events
        // then follow the held card, whichever card the EventSystem keeps reporting.
        private CardView _dragSource;

        private void OnDragBegan(CardView card, PointerEventData e)
        {
            if (_busy || _dragging != null) return;
            CardView held = card;
            if (_hovered && _hovered != card && _hovered.Interactable
                && HoveredRectContains(_hovered, _hand.IndexOf(_hovered), e.position)) held = _hovered;
            _dragSource = card;
            BeginHold(held);
            _dragPointerWorld = PointerWorld(e);
            _grabOffset = held.transform.position - _dragPointerWorld;
        }

        private void OnDragMoved(CardView card, PointerEventData e)
        {
            if (_dragging == null || card != _dragSource) return;
            _dragPointerWorld = PointerWorld(e);
            UpdateHot(_dragging, e.position);
            _dragging.transform.position = DragPosition();
        }

        private void OnDragEnded(CardView card, PointerEventData e)
        {
            if (_dragging == null || card != _dragSource) return;
            _dragSource = null;
            _dragPointerWorld = PointerWorld(e);
            CardView held = _dragging;
            Release(held, ZoneAt(held, e.position));
        }

        private void BeginHold(CardView card)
        {
            _dragging = card;
            _pickedUnit = -1;
            _aimedUnit = card.Face.TargetUnit;
            // The card may be lifted by the hover; it must come back to its resting place, not to there.
            Vector3 held = card.transform.position;
            PlaceAtHome(card, _hand.IndexOf(card));
            _dragHome = card.transform.position;
            card.transform.position = held;
            // The hover stops updating while a card is held, so settle any card it left half lifted.
            foreach (CardView other in _hand)
            {
                if (other && other != card) PlaceAtHome(other, _hand.IndexOf(other));
            }
            _hovered = null;
            _hoverWeight.Clear();
            card.transform.SetAsLastSibling();
            card.group.alpha = 0.92f;
            HideHoverPanels();
            // §4.7 / §5.2: the stamina it would pay turns to rings, and "▸ n" shows.
            _staminaTag?.ShowCost(HeldCost(card));
            ShowReserveHint(card); // §4.9 / §5.3 (#455): the 構えの予告 after this card is paid for
            ShowHpLoss(card); // §4.5 / §5.2 (#455): the wick stripe on the enemy's HP bar
            // §6.3: a card aimed at one enemy is judged again on the enemy it frames. The verdict is the
            // script's (CardFace.LitFor); the View only shows it.
            if (card.Face.Aim == CardAim.Single) card.ShowLamp(card.Face.LitFor(EnemyScreen.FramedUnit(card.Face, _pickedUnit)));
            // EffectId.CardGrab: a held card stands upright. §0.1 decision 7: its size and shadow stay.
            Quaternion tilt = card.Rect.localRotation;
            Vector3 scaleFrom = card.Rect.localScale;
            Vector3 heldScale = Vector3.one;
            StartCoroutine(Effect(EffectId.CardGrab,
                UiTween.Run(_effects.Ms(EffectId.CardGrab), Ease.Out, t =>
                {
                    if (!card || _dragging != card) return;
                    card.Rect.localRotation = Quaternion.Slerp(tilt, Quaternion.identity, t);
                    card.Rect.localScale = Vector3.LerpUnclamped(scaleFrom, heldScale, t);
                }),
                () =>
                {
                    card.Rect.localRotation = Quaternion.identity;
                    card.Rect.localScale = heldScale;
                }));
            // The zone takes the drop from the first frame; the fade only changes how it looks.
            if (_zoneFade != null) StopCoroutine(_zoneFade);
            if (card.Face.Aim == CardAim.Single)
            {
                KeepReceiverOnAimed();
                receiver.Show(PreviewFor(card));
                _zoneFade = StartCoroutine(Effect(EffectId.ReceiverShow, receiver.FadeIn(_effects.Ms(EffectId.ReceiverShow)), null));
            }
            else
            {
                throwLine.Show(PreviewFor(card));
                _zoneFade = StartCoroutine(Effect(EffectId.ThrowLineShow, throwLine.FadeIn(_effects.Ms(EffectId.ThrowLineShow)), null));
            }
            ShowTargetMark(card);
        }

        /// <summary>
        /// battle-visual-v1 §5.2 / §5.3 (#242): the four hooks around the figure the card lands on, with
        /// the one predicted value on the top edge. A card aimed at one enemy frames the enemy (steel and
        /// no number when the script says nobody is in reach: CardFace.ReachHint); a self card frames
        /// the figure the script names (CardFace.Affects). With several enemies a card aimed at one frames
        /// every standing one (<see cref="ShowTargetMarks"/>), and the one it lands on without aiming is
        /// the focus enemy.
        /// </summary>
        private void ShowTargetMark(CardView card)
        {
            if (card.Face.Aim == CardAim.Single && SeveralEnemies)
            {
                ShowTargetMarks(card);
                return;
            }
            FigureView figure;
            bool reaches = true;
            if (card.Face.Aim == CardAim.Single)
            {
                figure = enemyFigure;
                reaches = EnemyScreen.Reaches(card.Face, 0, _enemyCount);
            }
            else if (!card.Face.Affects.HasValue)
            {
                Debug.LogError("[Depiction] card " + card.CardId + " is a throw-line card but the script gave no Affects");
                return;
            }
            else figure = card.Face.Affects.Value == UnitSide.Player ? playerFigure : EnemyAt(EnemyScreen.FocusOf(_source.Frame)).Figure;
            if (!figure || !figure.targetMark)
            {
                Debug.LogError("[Depiction] " + (figure ? figure.name : "a figure") + " has no targetMark; run Tools > Depiction > Upgrade Prefabs");
                return;
            }
            CardValueKind kind = ValueKindOf(card);
            figure.targetMark.Show(reaches, BattleTheme.Wick, PreviewFor(card), VisualArt.ValueIcon(kind), VisualArt.ValueColor(kind));
        }

        private static CardValueKind ValueKindOf(CardView card)
        {
            return card.Face.Values != null && card.Face.Values.Count > 0
                ? card.Face.Values[0].Kind
                : card.Face.Kind == CardKind.Guard ? CardValueKind.Guard : CardValueKind.Power;
        }

        /// <summary>
        /// battle_ui_ux_v2 §2.2 の 5 / §3.9 (#589): every standing enemy's figure gets its frame. One in the
        /// card's reach shows the value on that enemy (the source's, read per enemy); one out of it shows
        /// steel hooks and no number (CardFace.ReachOn). A fallen enemy shows none: its figure is gone.
        /// </summary>
        private void ShowTargetMarks(CardView card)
        {
            CardValueKind kind = ValueKindOf(card);
            for (int k = 0; k < _enemyCount; k++)
            {
                TargetMarkView mark = _enemies[k].Figure ? _enemies[k].Figure.targetMark : null;
                if (!mark)
                {
                    Debug.LogError("[Depiction] enemy " + k + "'s figure has no targetMark; run Tools > Depiction > Upgrade Prefabs");
                    continue;
                }
                if (!EnemyScreen.Stands(_source.Frame, k))
                {
                    mark.Hide();
                    continue;
                }
                bool reaches = EnemyScreen.Reaches(card.Face, k, _enemyCount);
                mark.Show(reaches, BattleTheme.Wick, reaches ? PreviewOn(card, k) : "", VisualArt.ValueIcon(kind), VisualArt.ValueColor(kind));
            }
        }

        private void HideTargetMarks()
        {
            if (playerFigure && playerFigure.targetMark) playerFigure.targetMark.Hide();
            foreach (EnemyParts foe in _enemies)
            {
                if (foe.Figure && foe.Figure.targetMark) foe.Figure.targetMark.Hide();
            }
        }

        private void SetZoneHot(CardView card, bool hot)
        {
            if (card.Face.Aim == CardAim.Single)
            {
                receiver.SetHot(hot);
                // §5.15 順 1: only the enemy the card is over answers.
                int framed = EnemyScreen.FramedUnit(card.Face, _pickedUnit);
                for (int k = 0; k < _enemyCount; k++)
                {
                    TargetMarkView mark = _enemies[k].Figure ? _enemies[k].Figure.targetMark : null;
                    if (mark) mark.SetHot(hot && (!SeveralEnemies || k == framed));
                }
                return;
            }
            throwLine.SetHot(hot);
            if (playerFigure.targetMark) playerFigure.targetMark.SetHot(hot);
            for (int k = 0; k < _enemyCount; k++)
            {
                TargetMarkView mark = _enemies[k].Figure ? _enemies[k].Figure.targetMark : null;
                if (mark) mark.SetHot(hot);
            }
        }

        /// <summary>The stamina a held card would pay: on the enemy it frames, with several (CardFace.CostFor).</summary>
        private int HeldCost(CardView card)
        {
            return SeveralEnemies ? card.Face.CostFor(EnemyScreen.FramedUnit(card.Face, _pickedUnit)) : card.Face.Cost;
        }

        /// <summary>
        /// §6.3 (#589): the held card has come over another enemy, or off the last one. The numbers read on the
        /// enemy it frames follow: the lamp, the stamina it would pay and the 構えの予告.
        /// </summary>
        private void PickEnemy(CardView card, int unit)
        {
            if (unit == _pickedUnit) return;
            _pickedUnit = unit;
            if (unit >= 0) _aimedUnit = unit;
            card.ShowLamp(card.Face.LitFor(EnemyScreen.FramedUnit(card.Face, unit)));
            _staminaTag?.ShowCost(HeldCost(card));
            ShowReserveHint(card);
        }

        /// <summary>The release zone goes over the enemy the held card is aimed at at once, for what reads its place this frame.</summary>
        private void KeepReceiverOnAimed()
        {
            RectTransform root = RefScreen.Root(receiver.transform);
            if (SeveralEnemies && root) KeepX(receiver.Rect, root, EnemyHomeOnScreen(root, ReceiverEnemy()).x);
        }

        /// <summary>The one predicted value comes from the source; a card it would refuse shows none.</summary>
        private string PreviewFor(CardView card)
        {
            return _source == null ? "" : _source.PreviewFor(card.CardId);
        }

        /// <summary>The source that answers for a named enemy (CoreBattleSource), or null for one that keeps one enemy.</summary>
        private ITrialSource TargetedSource => _source as ITrialSource;

        /// <summary>The predicted value read on enemy <paramref name="unit"/>.</summary>
        private string PreviewOn(CardView card, int unit)
        {
            return TargetedSource != null ? TargetedSource.PreviewFor(card.CardId, unit) : PreviewFor(card);
        }

        private DropZone ZoneAt(CardView card, Vector2 screenPoint)
        {
            if (card.Face.Aim == CardAim.Single) return ReceiverTakes(card, screenPoint) ? DropZone.Receiver : DropZone.None;
            return throwLine.IsAbove(screenPoint, null) ? DropZone.AboveThrowLine : DropZone.None;
        }

        /// <summary>
        /// #568: the receiver takes a single-target card when half of the card lies on it or the pointer
        /// is inside it (ReceiverDrop.Takes). The card is measured where the pointer carries it, before
        /// the snap draws it in, so the pull cannot hold a card that has already left.
        /// </summary>
        private bool ReceiverTakes(CardView card, Vector2 screenPoint)
        {
            RectTransform root = RefScreen.Root(receiver.transform);
            if (!root) return receiver.Contains(screenPoint, null);
            ScreenBox dish = BoxOn(root, receiver.Rect, Vector3.zero);
            ScreenBox held = BoxOn(root, card.Rect, _dragPointerWorld + _grabOffset - card.transform.position);
            Vector2 pointer = RefScreen.Of(root, _dragPointerWorld);
            if (!SeveralEnemies) return ReceiverDrop.Takes(held, dish, pointer.x, pointer.y);
            // §3.9 (#589): one receiver per standing enemy, as tall as the scene's and over that enemy's home.
            var units = new List<int>();
            var dishes = new List<ScreenBox>();
            for (int k = 0; k < _enemyCount; k++)
            {
                if (!EnemyScreen.Stands(_source.Frame, k)) continue;
                units.Add(k);
                dishes.Add(new ScreenBox(EnemyHomeOnScreen(root, _enemies[k]).x - dish.Width * 0.5f, dish.Y, dish.Width, dish.Height));
            }
            int picked = ReceiverDrop.Pick(held, dishes, pointer.x, pointer.y);
            PickEnemy(card, picked >= 0 ? units[picked] : -1);
            return picked >= 0;
        }

        /// <summary>The box <paramref name="rt"/> covers on the reference screen once moved by <paramref name="shift"/> (world units); a tilted card counts its whole outline.</summary>
        private static ScreenBox BoxOn(RectTransform root, RectTransform rt, Vector3 shift)
        {
            rt.GetWorldCorners(Corners);
            float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
            foreach (Vector3 corner in Corners)
            {
                Vector2 at = RefScreen.Of(root, corner + shift);
                left = Mathf.Min(left, at.x);
                right = Mathf.Max(right, at.x);
                top = Mathf.Min(top, at.y);
                bottom = Mathf.Max(bottom, at.y);
            }
            return new ScreenBox(left, top, right - left, bottom - top);
        }

        private void UpdateHot(CardView card, Vector2 screenPoint)
        {
            _zoneHot = ZoneAt(card, screenPoint) != DropZone.None;
            SetZoneHot(card, _zoneHot);
        }

        /// <summary>Where the held card sits: under the pointer, drawn toward the dish by the snap weight.</summary>
        private Vector3 DragPosition()
        {
            Vector3 free = _dragPointerWorld + _grabOffset;
            if (_snapWeight <= 0f || !receiver) return free;
            float eased = _snapWeight * _snapWeight * (3f - 2f * _snapWeight);
            return Vector3.Lerp(free, receiver.transform.position, SnapPull * eased);
        }

        /// <summary>
        /// EffectId.ReceiverSnap: while a single-target card is over the dish, the pull grows over the
        /// effect's length, and lets go the same way. Switched off, it takes hold at once.
        /// </summary>
        private void UpdateSnap()
        {
            if (_dragging == null)
            {
                _snapWeight = 0f;
                return;
            }
            float target = _dragging.Face.Aim == CardAim.Single && _zoneHot ? 1f : 0f;
            float ms = _effects.Ms(EffectId.ReceiverSnap);
            // The battle speed (#348), like every tween.
            float next = ms > 0f ? Mathf.MoveTowards(_snapWeight, target, Time.unscaledDeltaTime * FrameMotionSpeed * 1000f / ms) : target;
            if (next == _snapWeight) return;
            if (_snapWeight == 0f && next > 0f && Trace != null)
            {
                if (_effects.IsOn(EffectId.ReceiverSnap)) _snapHandle = Trace.Begin(EffectId.ReceiverSnap, CurrentOrder);
                else Trace.Skip(EffectId.ReceiverSnap, CurrentOrder);
            }
            if (next >= 1f && _snapHandle >= 0)
            {
                Trace.End(_snapHandle);
                _snapHandle = -1;
            }
            _snapWeight = next;
            _dragging.transform.position = DragPosition();
        }

        /// <summary>Halt: a held card goes back to its place unplayed, and the drop zone goes.</summary>
        private void LetGo()
        {
            CardView held = _dragging;
            _dragging = null;
            _dragSource = null;
            _pickedUnit = -1;
            _zoneHot = false;
            _snapWeight = 0f;
            _snapHandle = -1;
            if (_zoneFade != null)
            {
                StopCoroutine(_zoneFade);
                _zoneFade = null;
            }
            receiver.Hide();
            throwLine.Hide();
            HideTargetMarks();
            _staminaTag?.ShowCost(0);
            ShowReserveHint(null);
            ShowHpLoss(null);
            if (held)
            {
                held.group.alpha = 1f;
                held.ShowLamp(held.Face.TraitLit);
            }
            LayoutHand();
        }

        private void Release(CardView card, DropZone zone)
        {
            // §3.9 (#589): the enemy it was released over; −1 lets the source send it where its face was read.
            int target = _pickedUnit;
            _pickedUnit = -1;
            _dragging = null;
            _zoneHot = false;
            _snapWeight = 0f;
            _snapHandle = -1;
            if (_zoneFade != null)
            {
                StopCoroutine(_zoneFade);
                _zoneFade = null;
            }
            receiver.Hide();
            throwLine.Hide();
            HideTargetMarks();
            _staminaTag?.ShowCost(0);
            ShowReserveHint(null);
            ShowHpLoss(null);
            DepictionEvent played;
            PlayVerdict verdict = target >= 0 && TargetedSource != null
                ? TargetedSource.TryPlay(card.CardId, zone, target, out played)
                : _source.TryPlay(card.CardId, zone, out played);
            if (verdict == PlayVerdict.Accepted) StartCoroutine(PlayAccepted(card, played));
            else
            {
                ShowRefusal(card, verdict, target);
                // A card released on a zone it belongs to was refused by a rule: shake it. One released
                // on the wrong zone just slides home.
                StartCoroutine(ReturnToHand(card, shake: verdict != PlayVerdict.WrongZone && zone != DropZone.None));
            }
        }

        private IEnumerator PlayAccepted(CardView card, DepictionEvent ev)
        {
            _busy = true;
            SetHandInteractable(false);
            float startedAt = Time.unscaledTime;
            BeginPlan(ev);
            _hand.Remove(card);
            Vector3 from = card.transform.position;
            Vector3 to = ev.Aim == CardAim.Single
                ? receiver.transform.position
                : from + new Vector3(0f, 140f * handArea.lossyScale.y, 0f); // a self card is tossed upward
            // §5.4 順 0 (#499): a trait that fires before the first face shows as the card is released,
            // so the plan gives the release no wait of its own and the card flies off beside the cue.
            yield return HoldBeat(EffectId.CardRelease, UiTween.Run(_effects.Ms(EffectId.CardRelease), Ease.In, t =>
            {
                if (!card) return;
                card.transform.position = Vector3.LerpUnclamped(from, to, t);
                float s = Mathf.Lerp(1f, 0.5f, t);
                card.Rect.localScale = new Vector3(s, s, 1f);
                card.group.alpha = 1f - t;
            }), null, played: () => Released(card, ev.PlayedTo));

            foreach (Cue cue in ev.Cues)
            {
                yield return PlayCue(ev, cue);
            }
            // As in PlayEvent: a blow with no return (Guard took part of it, or a move followed it)
            // may not keep its action picture into the next event.
            if (playerFigure) playerFigure.SetPose(FigurePose.Idle);
            EndPlan();
            ApplyFrame(ev.After);
            float seconds = Time.unscaledTime - startedAt;
            EventSeconds.Add(seconds);
            LogSettled(ev, seconds);
            yield return Gate(ev.Order + "-end");
            yield return UiTween.Wait(EffectFlow.AfterPlayerEventMs);
            yield return RunAutomaticEvents();
        }

        /// <summary>The released card is gone: a ghost flies to the pile the core put it in and the hand closes up.</summary>
        private void Released(CardView card, PileKind playedTo)
        {
            if (!card) return;
            Vector2 vanishedAt = card.Face.Aim == CardAim.Single && receiver
                ? DepictionFx.PointOn(fxLayer, receiver.transform)
                : DepictionFx.PointOn(fxLayer, card.transform);
            Destroy(card.gameObject);
            StartCoroutine(GhostToPile(vanishedAt, playedTo));
            StartCoroutine(Effect(EffectId.HandFan, SettleHand(_effects.Ms(EffectId.HandFan)), LayoutHand));
        }

        /// <summary>
        /// Slides the remaining cards to their new fan places. Each start value is kept with its card,
        /// not by position in the list, so a hand that changes mid-tween cannot pair a card with
        /// another card's start; a card that left the hand is skipped.
        /// </summary>
        private IEnumerator SettleHand(float ms)
        {
            var starts = new List<(CardView card, Vector2 position, float degrees)>();
            foreach (CardView card in _hand)
            {
                if (card) starts.Add((card, card.Rect.anchoredPosition, card.Rect.localEulerAngles.z));
            }
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                foreach ((CardView card, Vector2 position, float degrees) start in starts)
                {
                    int index = start.card ? _hand.IndexOf(start.card) : -1;
                    if (index < 0) continue;
                    HomeOf(index, out Vector2 home, out float homeDegrees);
                    start.card.Rect.anchoredPosition = Vector2.Lerp(start.position, home, t);
                    start.card.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(start.degrees, homeDegrees, t));
                    start.card.Rect.localScale = Vector3.one;
                }
            });
        }

        private IEnumerator ReturnToHand(CardView card, bool shake)
        {
            _busy = true;
            Vector3 from = card.transform.position;
            HomeOf(_hand.IndexOf(card), out _, out float homeDegrees);
            Vector3 home = _dragHome;
            yield return Effect(EffectId.CardReturn, UiTween.Run(_effects.Ms(EffectId.CardReturn), Ease.Out, t =>
            {
                if (!card) return;
                card.transform.position = Vector3.LerpUnclamped(from, home, t);
                card.Rect.localScale = Vector3.one;
                card.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(0f, homeDegrees, t));
            }), null);
            if (card)
            {
                card.group.alpha = 1f;
                card.ShowLamp(card.Face.TraitLit);
            }
            if (shake && card) yield return Effect(EffectId.RefusalShake, UiTween.Shake(card.Rect, 8f, 2, _effects.Ms(EffectId.RefusalShake)), null);
            LayoutHand();
            _busy = false;
            RefreshPlayableLook();
        }

        /// <summary>
        /// Debug only: performs the drag the source suggests, so a slice can be captured without a
        /// mouse. A fixed script suggests its next card, and a core battle started with auto play the leftmost one it can pay for.
        /// </summary>
        private IEnumerator AutoDrag()
        {
            yield return UiTween.Wait(400f);
            if (_halted) yield break;
            string suggested = _source.SuggestedCardId;
            if (string.IsNullOrEmpty(suggested))
            {
                // Nothing left to drag. An unattended run ends the turn the way a click on the plate would.
                if (autoEndTurn && !_busy && _source.CanEndTurn) StartCoroutine(PlayThenContinue(_source.EndTurn()));
                yield break;
            }
            CardView card = _hand.Find(c => c && c.CardId == suggested);
            if (card == null) yield break;
            CardAim aim = card.Face.Aim;
            BeginHold(card);
            Vector3 from = card.transform.position;
            Vector3 to = aim == CardAim.Single
                ? receiver.transform.position
                : new Vector3(from.x, throwLine.transform.position.y + (throwLine.transform.position.y - from.y) * 0.35f, from.z);
            yield return UiTween.Run(450f, Ease.InOut, t => { if (card && !_halted) card.transform.position = Vector3.LerpUnclamped(from, to, t); });
            if (_halted) yield break; // Halt let the card go back to the fan mid-flight
            SetZoneHot(card, true);
            yield return Gate((EventSeconds.Count + 1) + "-hover");
            if (_halted) yield break; // Halt let the card go
            Release(card, DepictionText.RequiredZone(aim));
        }

        private Vector3 PointerWorld(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(handArea, e.position, e.pressEventCamera, out Vector3 world);
            return world;
        }

        // ---- debug gate ---------------------------------------------------------------------

        private IEnumerator Gate(string gateName)
        {
            bool capture = !string.IsNullOrEmpty(debugCaptureDir);
            if (!debugStepMode && !capture) yield break;
            DebugGate = gateName; // set first: ApplyTweenSpeed leaves the 0 alone while a gate holds
            UiTween.Speed = 0f; // freezes every running tween so the frame can be captured
            if (capture)
            {
                System.IO.Directory.CreateDirectory(debugCaptureDir);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(debugCaptureDir, gateName + ".png"));
                for (int i = 0; i < 3; i++) yield return null;
            }
            _stepRequested = false;
            while (debugStepMode && !_stepRequested) yield return null;
            DebugGate = "";
            ApplyTweenSpeed(); // a speed picked while frozen takes effect here
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
