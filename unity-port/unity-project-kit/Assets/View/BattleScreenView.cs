// The UGUI battle screen (battle_ui_ux_v1.md, L1 layout, skin A+).
//
// This file references UnityEngine and ONLY compiles inside a Unity project; the
// #if guard keeps it inert in the headless dotnet library.
//
// Design notes
// - Thin MonoBehaviour: every number shown comes from BattleViewModel / the
//   reducer's events. Rules live in BattleCore (noEngineReferences).
// - The whole hierarchy is built in code (no scene / prefab YAML), so `Bootstrap`
//   can stand the screen up in any scene.
// - Distance is shown as real on-screen spacing between two figures (2026-07-04).
// - Rendering is two-phase: BattleDirector plays vm.Events (§5 timings), then the
//   settled values are applied. Space / click fast-forwards without changing results.
// - Trace replay: Resources/trace-actions.txt lines `play <instanceId> [invest]`,
//   `end`, `restart`; every state is logged as one compact [Trace] line.

#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections;
using System.Linq;
using System.Text;
using BattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public sealed class BattleScreenView : MonoBehaviour, IBattleView
{
    // ---- configuration --------------------------------------------------

    [Header("RNG")]
    [Tooltip("true: FixedRng(fixedRngValue) for reproducible runs. false: SystemRng for real play.")]
    [SerializeField] private bool useFixedRng = true;
    [SerializeField] private double fixedRngValue = 0.0;

    [Header("Battle input (what exploration hands to the battle)")]
    [SerializeField] private int floor = 2;
    [SerializeField] private int miasmaPercent = 32;
    [SerializeField] private int miasmaDensity = 2;
    [SerializeField] private int timeLimitLeft = 6;
    [SerializeField] private int timeLimitMax = 10;
    [SerializeField] private int disclosure = 1;
    [SerializeField] private int playerMaxStamina = 9;

    [Header("Presentation")]
    [Tooltip("Collapse every animation to ~1 ms (results are unchanged).")]
    [SerializeField] private bool reduceMotion = false;
    [SerializeField] private bool showDebugButtons = true;

    [Header("Trace replay (debug)")]
    [SerializeField] private bool autoReplayTrace = false;
    [SerializeField] private float replayStepSeconds = 0.5f;

    private const string TraceResource = "trace-actions";
    private const string TracePrefix = "[Trace]";

    // ---- runtime ----------------------------------------------------------

    private BattleStore _store;
    private Action _unsubscribe;
    private ArenaView _arena;
    private BattleHud _hud;
    private HandView _hand;
    private JournalDrawer _journal;
    private ResultOverlay _overlay;
    private BattleDirector _director;
    private bool _replaying;
    private int _traceSeq;
    private BattleViewModel _vm;

    // ---- bootstrap ----------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<BattleScreenView>() != null) return;
        var go = new GameObject("BattleScreenView");
        var view = go.AddComponent<BattleScreenView>();
        var args = Environment.GetCommandLineArgs();
        view.autoReplayTrace = args.Contains("-replayTrace");
        // -captureDir <dir> [-captureEvery <seconds>]: save a screenshot periodically (headless visual checks).
        int ci = Array.IndexOf(args, "-captureDir");
        if (ci >= 0 && ci + 1 < args.Length)
        {
            view._captureDir = args[ci + 1];
            int ei = Array.IndexOf(args, "-captureEvery");
            if (ei >= 0 && ei + 1 < args.Length && float.TryParse(args[ei + 1], out float every)) view._captureEvery = every;
        }
    }

    private string _captureDir;
    private float _captureEvery = 4f;

    private IEnumerator CaptureLoop()
    {
        System.IO.Directory.CreateDirectory(_captureDir);
        bool demo = Array.IndexOf(Environment.GetCommandLineArgs(), "-demoUi") >= 0;
        for (int i = 1; ; i++)
        {
            yield return new WaitForSecondsRealtime(_captureEvery);
            // -demoUi: show the journal drawer and a selected card (with its invest chips) in the first captures.
            if (demo && !_replaying)
            {
                if (i == 1) _journal.Open();
                if (i == 3) _journal.Close();
                if (i == 4 && !_director.Playing) _hand.Select(0);
                if (i == 6) _hand.Deselect();
            }
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(_captureDir, $"cap-{i:D3}.png"));
        }
    }

    private void Start()
    {
        // Keep animating when the window is not focused (headless captures, trace replay).
        Application.runInBackground = true;
        UiKit.EnsureFont();
        BuildHierarchy();
        CreateStore();
        if (autoReplayTrace) StartReplay();
        if (!string.IsNullOrEmpty(_captureDir)) StartCoroutine(CaptureLoop());
    }

    private BattleInit MakeInit() => new BattleInit(
        PlayerMaxStamina: playerMaxStamina,
        Floor: floor,
        MiasmaPercent: miasmaPercent,
        MiasmaDensity: miasmaDensity,
        TimeLimitLeft: timeLimitLeft,
        TimeLimitMax: timeLimitMax,
        Disclosure: disclosure);

    public void SetFixedRng(bool fixedRng)
    {
        useFixedRng = fixedRng;
        CreateStore();
    }

    private void CreateStore()
    {
        _unsubscribe?.Invoke();
        IRng rng = useFixedRng ? new FixedRng(fixedRngValue) : new SystemRng();
        _store = new BattleStore(rng, MakeInit());
        _traceSeq = 0;
        Debug.Log($"{TracePrefix} rng={(useFixedRng ? $"fixed({fixedRngValue})" : "system")}");
        _unsubscribe = _store.Subscribe(state =>
        {
            var vm = BattleViewModel.From(state);
            Debug.Log(TraceLine(++_traceSeq, state, vm));
            Render(vm);
        });
        _overlay.SetRngLabel(RngButtonText());
    }

    private string RngButtonText() => useFixedRng ? "乱数: 固定" : "乱数: 実戦";

    private void OnDestroy() => _unsubscribe?.Invoke();

    // ---- IBattleView ----------------------------------------------------------

    public void Render(BattleViewModel vm)
    {
        _vm = vm;
        _director.Enqueue(vm);
    }

    public void OnCardPlayed(string instanceId, int invest)
    {
        if (_replaying || _director.Playing || _journal.IsOpen) return;
        _store.PlayCard(instanceId, invest);
    }

    public void OnEndTurnClicked()
    {
        if (_replaying || _director.Playing) return;
        _hand.Deselect();
        _store.EndTurn();
    }

    public void OnRestartClicked()
    {
        if (_replaying) return;
        _journal.Close();
        _store.Restart();
    }

    // ---- per-frame ------------------------------------------------------------

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _arena?.Tick(dt);
        _hud?.Tick(dt);
        HandleKeys();
    }

    private enum Hotkey { D0, D1, D2, D3, Space, Escape, J, Enter }

    private static bool Pressed(Hotkey key)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        switch (key)
        {
            case Hotkey.D0: return kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame;
            case Hotkey.D1: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            case Hotkey.D2: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
            case Hotkey.D3: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
            case Hotkey.Space: return kb.spaceKey.wasPressedThisFrame;
            case Hotkey.Escape: return kb.escapeKey.wasPressedThisFrame;
            case Hotkey.J: return kb.jKey.wasPressedThisFrame;
            case Hotkey.Enter: return kb.enterKey.wasPressedThisFrame;
        }
        return false;
#else
        switch (key)
        {
            case Hotkey.D0: return Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0);
            case Hotkey.D1: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
            case Hotkey.D2: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
            case Hotkey.D3: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
            case Hotkey.Space: return Input.GetKeyDown(KeyCode.Space);
            case Hotkey.Escape: return Input.GetKeyDown(KeyCode.Escape);
            case Hotkey.J: return Input.GetKeyDown(KeyCode.J);
            case Hotkey.Enter: return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        }
        return false;
#endif
    }

    private void HandleKeys()
    {
        if (_director == null) return;
        if (Pressed(Hotkey.Space))
        {
            _director.FastForward();
            return;
        }
        if (Pressed(Hotkey.Escape))
        {
            if (_journal.IsOpen) _journal.Close();
            else _hand.Deselect();
            return;
        }
        if (Pressed(Hotkey.J))
        {
            if (!_director.Playing) _journal.Toggle();
            return;
        }
        if (_director.Playing || _replaying || _journal.IsOpen || _vm == null || _vm.BattleOver) return;

        if (Pressed(Hotkey.Enter))
        {
            if (_hand.SelectedIndex < 0) OnEndTurnClicked();
            return;
        }
        int digit = Pressed(Hotkey.D0) ? 0 : Pressed(Hotkey.D1) ? 1 : Pressed(Hotkey.D2) ? 2 : Pressed(Hotkey.D3) ? 3 : -1;
        if (digit < 0) return;
        if (_hand.SelectedIndex >= 0)
        {
            _hand.PlaySelected(digit);
        }
        else if (digit >= 1)
        {
            _hand.Select(digit - 1);
        }
    }

    // ---- trace ----------------------------------------------------------------

    private static string TraceLine(int seq, BattleState state, BattleViewModel vm)
    {
        string hand = string.Join(",", vm.Hand.Select(c => c.InstanceId));
        string omen = state.Omen != null ? state.Omen.ActionId.ToToken() : "-";
        return $"{TracePrefix} #{seq} T{state.Turn} d{state.DistanceIndex} " +
               $"P{state.PlayerHp}/{state.PlayerStamina}/{state.PlayerGuard} " +
               $"E{state.EnemyHp}/{state.EnemyStamina}/{state.EnemyGuard} " +
               $"{vm.Result.ToToken()} omen={omen} hand=[{hand}]";
    }

    private void StartReplay()
    {
        if (_replaying) return;
        var asset = Resources.Load<TextAsset>(TraceResource);
        if (asset == null)
        {
            Debug.LogWarning($"{TracePrefix} no Resources/{TraceResource}.txt");
            return;
        }
        StartCoroutine(Replay(asset.text));
    }

    private IEnumerator Replay(string script)
    {
        _replaying = true;
        Debug.Log($"{TracePrefix} replay start");
        foreach (string raw in script.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            yield return new WaitForSecondsRealtime(replayStepSeconds);
            while (_director.Playing) yield return null;
            string[] parts = line.Split(' ');
            switch (parts[0])
            {
                case "play":
                {
                    int invest;
                    if (parts.Length < 3 || !int.TryParse(parts[2], out invest))
                    {
                        var card = _vm?.Hand.FirstOrDefault(c => c.InstanceId == parts[1]);
                        invest = card?.DefaultInvest ?? 0;
                    }
                    _store.PlayCard(parts[1], invest);
                    break;
                }
                case "end": _store.EndTurn(); break;
                case "restart": _store.Restart(); break;
                default: Debug.LogWarning($"{TracePrefix} unknown line: {line}"); break;
            }
        }
        while (_director.Playing) yield return null;
        _replaying = false;
        Debug.Log($"{TracePrefix} replay done");
    }

    // ---- hierarchy ----------------------------------------------------------

    private void BuildHierarchy()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
#if ENABLE_INPUT_SYSTEM
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
#else
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
        }

        var canvasGo = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(BattleTheme.RefWidth, BattleTheme.RefHeight);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = canvasGo.GetComponent<RectTransform>();

        // §6.1 layers, bottom to top.
        var l0 = UiKit.Rect(root, "L0 Background", Vector2.zero, Vector2.one);
        var ground = UiKit.Fill(l0, "Ground", BattleTheme.Ground);
        // Clicks that hit nothing interactive land here and deselect the card.
        ground.raycastTarget = true;
        UiKit.OnPointer(ground.gameObject, EventTriggerType.PointerClick, _ => _hand.Deselect());
        var l1 = UiKit.Rect(root, "L1 Arena", new Vector2(0f, BattleTheme.Arena.x), new Vector2(1f, BattleTheme.Arena.y));
        var l2 = UiKit.Rect(root, "L2 Hud", Vector2.zero, Vector2.one);
        var l3 = UiKit.Rect(root, "L3 Hand", Vector2.zero, Vector2.one);
        var l4 = UiKit.Rect(root, "L4 Drawer", Vector2.zero, Vector2.one);
        var l5 = UiKit.Rect(root, "L5 Overlay", Vector2.zero, Vector2.one);

        _arena = new ArenaView(this);
        _arena.Build(l0, l1);

        _hud = new BattleHud(this);
        _hud.Build(l2, l3, () => { if (!_director.Playing) _journal.Toggle(); }, OnEndTurnClicked);

        _hand = new HandView(this);
        _hand.Build(l3);
        _hand.OnPlay = OnCardPlayed;
        _hand.OnTierHover = tier => _director.PreviewTier(tier);
        _hand.OnSelectionChanged = card =>
        {
            if (card == null) _director.PreviewTier(null);
            else
            {
                var def = card.Tiers.FirstOrDefault(t => t.IsDefault);
                _director.PreviewTier(def);
            }
        };

        _journal = new JournalDrawer(this);
        _journal.Build(l4);
        _journal.OnOpenChanged = open =>
        {
            if (open) _hand.Deselect();
            _hand.SetInteractable(!open && !_director.Playing && _vm != null && !_vm.BattleOver);
        };

        _overlay = new ResultOverlay(this);
        _overlay.Build(l5, showDebugButtons);
        _overlay.OnFastForward = () => _director.FastForward();
        _overlay.OnContinue = OnRestartClicked;
        _overlay.OnRestart = OnRestartClicked;
        _overlay.OnToggleRng = () => SetFixedRng(!useFixedRng);
        _overlay.OnReplayTrace = StartReplay;

        _director = new BattleDirector(this, _arena, _hud, _hand, _journal, _overlay);
        _director.SetReduceMotion(reduceMotion);
    }
}
#endif
