// Phase 3 — the UGUI battle screen.
//
// This file references UnityEngine and ONLY compiles inside a Unity project.
// It is deliberately kept OUT of the headless dotnet library (unity-port/
// BattleCore*), which stays engine-free so `dotnet test` can run it. The
// #if guard means it is inert anywhere that is not Unity.
//
// Design notes
// - Thin MonoBehaviour: every number shown comes from BattleViewModel; the
//   only logic here is layout. Rules live in BattleCore (noEngineReferences).
// - The whole hierarchy is built in code, so no scene/prefab YAML has to be
//   hand-edited and `Bootstrap` can stand the screen up in any scene.
// - Distance is shown as real on-screen spacing between two posture figures
//   (2026-07-04 decision — not a tab/track). `vm.DistanceLabel` is still
//   printed underneath as text.
// - Trace replay: `Resources/trace-actions.txt` (generated from the parity
//   fixture by `npm run unity:trace`) can be auto-played; every state is
//   logged as one compact line so the Unity run can be diffed against the
//   Web trace (`unity-project-kit/expected-trace.txt`).

#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BattleCore;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public sealed class BattleScreenView : MonoBehaviour, IBattleView
{
    // ---- configuration --------------------------------------------------

    [Header("RNG")]
    [Tooltip("true: FixedRng(fixedRngValue) so the run mirrors the Web parity trace. false: SystemRng for real play.")]
    [SerializeField] private bool useFixedRng = true;
    [SerializeField] private double fixedRngValue = 0.0;

    [Header("Trace replay (debug)")]
    [Tooltip("Auto-play Resources/trace-actions.txt on start and log one trace line per state.")]
    [SerializeField] private bool autoReplayTrace = false;
    [SerializeField] private float replayStepSeconds = 0.5f;

    private const string TraceResource = "trace-actions";
    private const string TracePrefix = "[Trace]";

    // Real on-screen gap (reference pixels) between the two figures per range.
    private static readonly Dictionary<RangeBand, float> FigureGap = new Dictionary<RangeBand, float>
    {
        [RangeBand.Close] = 260f,
        [RangeBand.Mid] = 520f,
        [RangeBand.Far] = 820f,
    };

    // Posture per range: (z-rotation degrees, y-scale). Close = leaning in,
    // Far = upright/relaxed. Stand-ins until real posture sprites exist.
    private static readonly Dictionary<RangeBand, (float rot, float scaleY)> Posture =
        new Dictionary<RangeBand, (float, float)>
        {
            [RangeBand.Close] = (12f, 1.08f),
            [RangeBand.Mid] = (0f, 1.0f),
            [RangeBand.Far] = (-8f, 0.94f),
        };

    private static readonly Color PlayerColor = new Color(0.30f, 0.55f, 0.95f);
    private static readonly Color EnemyColor = new Color(0.90f, 0.35f, 0.30f);
    private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.92f);
    private static readonly Color CardColor = new Color(0.16f, 0.18f, 0.24f);
    private static readonly Color CardDisabledColor = new Color(0.10f, 0.10f, 0.12f);

    // ---- runtime ----------------------------------------------------------

    private BattleStore _store;
    private Action _unsubscribe;
    private Font _font;
    private Sprite _white;

    private Text _playerStats;
    private Text _enemyStats;
    private Text _enemyHint;
    private Text _turnLabel;
    private Text _distanceLabel;
    private RectTransform _playerFigure;
    private RectTransform _enemyFigure;
    private RectTransform _handRoot;
    private Text _logText;
    private Button _endTurnButton;
    private GameObject _overlay;
    private Text _overlayText;
    private Text _rngButtonLabel;

    private bool _replaying;
    private int _traceSeq;

    // ---- bootstrap ----------------------------------------------------------

    /// <summary>
    /// Stands the screen up in whatever scene is loaded, so nothing has to be
    /// placed by hand. A BattleScreenView already in the scene wins.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<BattleScreenView>() != null) return;
        var go = new GameObject("BattleScreenView");
        var view = go.AddComponent<BattleScreenView>();
        view.autoReplayTrace = Environment.GetCommandLineArgs().Contains("-replayTrace");
    }

    private void Start()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _white = MakeWhiteSprite();
        BuildHierarchy();

        CreateStore();
        if (autoReplayTrace) StartReplay();
    }

    /// <summary>
    /// (Re)build the store with the configured RNG. FixedRng mirrors the Web
    /// parity trace; SystemRng is real play. Switching restarts the battle.
    /// </summary>
    public void SetFixedRng(bool fixedRng)
    {
        useFixedRng = fixedRng;
        CreateStore();
    }

    private void CreateStore()
    {
        _unsubscribe?.Invoke();
        IRng rng = useFixedRng ? new FixedRng(fixedRngValue) : new SystemRng();
        _store = new BattleStore(rng);
        _traceSeq = 0;
        Debug.Log($"{TracePrefix} rng={(useFixedRng ? $"fixed({fixedRngValue})" : "system")}");
        _unsubscribe = _store.Subscribe(state =>
        {
            var vm = BattleViewModel.From(state);
            Debug.Log(TraceLine(++_traceSeq, state, vm));
            Render(vm);
        });
        if (_rngButtonLabel != null) _rngButtonLabel.text = RngButtonText();
    }

    private string RngButtonText() => useFixedRng ? "乱数: 固定（トレース用）" : "乱数: 実戦";

    private void OnDestroy() => _unsubscribe?.Invoke();

    // ---- IBattleView ----------------------------------------------------------

    public void Render(BattleViewModel vm)
    {
        _turnLabel.text = $"ターン {vm.Turn}";
        _playerStats.text = $"自分\nHP {vm.PlayerHp}\n気力 {vm.PlayerStamina}\nガード {vm.PlayerGuard}";
        _enemyStats.text = $"敵\nHP {vm.EnemyHp}\n気力 {vm.EnemyStamina}";
        _enemyHint.text = vm.EnemyRangeHint;

        RenderDistance(vm.DistanceLabel);
        RenderHand(vm.Hand);
        RenderLog(vm.Log);

        _endTurnButton.interactable = !vm.BattleOver && !_replaying;
        _overlay.SetActive(vm.BattleOver);
        if (vm.BattleOver)
        {
            _overlayText.text = vm.Result == GameResult.Won ? "勝利" : "敗北";
        }
    }

    public void OnCardClicked(string instanceId) => _store.PlayCard(instanceId);
    public void OnEndTurnClicked() => _store.EndTurn();
    public void OnRestartClicked() => _store.Restart();

    // ---- render pieces ----------------------------------------------------------

    private void RenderDistance(string label)
    {
        _distanceLabel.text = $"間合い「{label}」";
        RangeBand range = Constants.RangeOrder.First(r => Constants.RangeLabel[r] == label);

        float gap = FigureGap[range];
        _playerFigure.anchoredPosition = new Vector2(-gap / 2f, 0f);
        _enemyFigure.anchoredPosition = new Vector2(gap / 2f, 0f);

        var (rot, scaleY) = Posture[range];
        // Player leans toward the right, enemy toward the left.
        _playerFigure.localRotation = Quaternion.Euler(0f, 0f, -rot);
        _enemyFigure.localRotation = Quaternion.Euler(0f, 0f, rot);
        _playerFigure.localScale = new Vector3(1f, scaleY, 1f);
        _enemyFigure.localScale = new Vector3(1f, scaleY, 1f);
    }

    private void RenderHand(IReadOnlyList<CardView> hand)
    {
        for (int i = _handRoot.childCount - 1; i >= 0; i--)
        {
            // Detach first: Destroy is deferred to end-of-frame, and the layout
            // group must not count the doomed card meanwhile.
            Transform old = _handRoot.GetChild(i);
            old.SetParent(null, false);
            Destroy(old.gameObject);
        }

        foreach (CardView card in hand)
        {
            string id = card.InstanceId;
            bool enabled = card.Playable && !_replaying;
            var button = MakeButton(_handRoot, $"Card {id}", BuildCardText(card), () => OnCardClicked(id),
                enabled ? CardColor : CardDisabledColor, 20, TextAnchor.UpperLeft);
            button.interactable = enabled;
            var le = button.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 230f;
            le.preferredHeight = 290f;
        }
    }

    private static string BuildCardText(CardView card)
    {
        var sb = new StringBuilder();
        sb.Append(card.Name).Append("  [").Append(card.Cost).Append("]\n");
        if (card.EffectiveRangeLabel != null) sb.Append("有効: ").Append(card.EffectiveRangeLabel).Append('\n');
        sb.Append('\n').Append(card.Description).Append('\n');
        if (card.Damage != null) sb.Append('\n').Append(card.Damage.Text);
        if (card.Guard > 0) sb.Append("\nガード +").Append(card.Guard);
        if (card.ShiftLabel != null) sb.Append('\n').Append(card.ShiftLabel);
        if (!card.Playable && card.DisabledReason.Length > 0) sb.Append("\n\n<").Append(card.DisabledReason).Append('>');
        return sb.ToString();
    }

    private void RenderLog(IReadOnlyList<LogEntry> log)
    {
        const int maxLines = 14;
        var sb = new StringBuilder();
        for (int i = log.Count - 1, shown = 0; i >= 0 && shown < maxLines; i--, shown++)
        {
            sb.Append(log[i].Text).Append('\n');
        }
        _logText.text = sb.ToString();
    }

    // ---- trace replay ----------------------------------------------------------

    /// <summary>
    /// One numbered line per store notification so a Unity run can be diffed
    /// against expected-trace.txt. The number matters: the Editor console
    /// collapses identical messages, and Restart reproduces earlier states.
    /// </summary>
    private static string TraceLine(int seq, BattleState state, BattleViewModel vm)
    {
        string hand = string.Join(",", vm.Hand.Select(c => c.InstanceId));
        int lastLog = state.Log.Count > 0 ? state.Log[state.Log.Count - 1].Id : -1;
        return $"{TracePrefix} #{seq} T{state.Turn} d{state.DistanceIndex} " +
               $"P{state.PlayerHp}/{state.PlayerStamina}/{state.PlayerGuard} " +
               $"E{state.EnemyHp}/{state.EnemyStamina} {vm.Result.ToString().ToLowerInvariant()} " +
               $"hand=[{hand}] log#{lastLog}";
    }

    private void StartReplay()
    {
        var asset = Resources.Load<TextAsset>(TraceResource);
        if (asset == null)
        {
            Debug.LogWarning($"{TracePrefix} no Resources/{TraceResource}.txt — run `npm run unity:trace` then `npm run unity:sync`.");
            return;
        }
        StartCoroutine(Replay(asset.text));
    }

    private IEnumerator Replay(string script)
    {
        _replaying = true;
        Render(_store.ToViewModel());
        Debug.Log($"{TracePrefix} replay start");
        foreach (string raw in script.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            yield return new WaitForSeconds(replayStepSeconds);
            string[] parts = line.Split(' ');
            switch (parts[0])
            {
                case "play": _store.PlayCard(parts[1]); break;
                case "end": _store.EndTurn(); break;
                case "restart": _store.Restart(); break;
                default: Debug.LogWarning($"{TracePrefix} unknown line: {line}"); break;
            }
        }
        _replaying = false;
        Render(_store.ToViewModel());
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
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = canvasGo.GetComponent<RectTransform>();

        // Background
        MakeImage(root, "Background", new Color(0.05f, 0.06f, 0.08f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Top bar
        _turnLabel = MakeText(MakePanel(root, "TurnBar", new Vector2(0.4f, 0.93f), new Vector2(0.6f, 1f)), "Turn", 34, TextAnchor.MiddleCenter);

        // Side panels
        var playerPanel = MakePanel(root, "PlayerPanel", new Vector2(0.01f, 0.72f), new Vector2(0.18f, 0.98f));
        _playerStats = MakeText(playerPanel, "Stats", 30, TextAnchor.UpperLeft);
        _playerStats.color = PlayerColor;

        var enemyPanel = MakePanel(root, "EnemyPanel", new Vector2(0.82f, 0.72f), new Vector2(0.99f, 0.98f));
        _enemyStats = MakeText(enemyPanel, "Stats", 30, TextAnchor.UpperLeft);
        _enemyStats.color = EnemyColor;

        var hintPanel = MakePanel(root, "EnemyHint", new Vector2(0.60f, 0.60f), new Vector2(0.99f, 0.70f));
        _enemyHint = MakeText(hintPanel, "Hint", 20, TextAnchor.MiddleLeft);

        // Arena: two figures whose spacing IS the distance.
        var arena = MakeRect(root, "Arena", new Vector2(0.2f, 0.36f), new Vector2(0.8f, 0.72f));
        MakeImage(arena, "Ground", new Color(0.18f, 0.16f, 0.14f), new Vector2(0f, 0.08f), new Vector2(1f, 0.11f), Vector2.zero, Vector2.zero);
        _playerFigure = MakeFigure(arena, "PlayerFigure", PlayerColor);
        _enemyFigure = MakeFigure(arena, "EnemyFigure", EnemyColor);
        _distanceLabel = MakeText(MakeRect(arena, "DistanceLabel", new Vector2(0.3f, 0.86f), new Vector2(0.7f, 1f)), "Label", 28, TextAnchor.MiddleCenter);

        // Log (newest first)
        var logPanel = MakePanel(root, "LogPanel", new Vector2(0.01f, 0.36f), new Vector2(0.19f, 0.70f));
        _logText = MakeText(logPanel, "Log", 17, TextAnchor.UpperLeft);

        // Hand
        var handPanel = MakeRect(root, "Hand", new Vector2(0.01f, 0.02f), new Vector2(0.80f, 0.33f));
        var layout = handPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childAlignment = TextAnchor.MiddleLeft;
        // Control child sizes so each card's LayoutElement preferred size is honoured.
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        _handRoot = handPanel;

        // Action buttons
        var actions = MakeRect(root, "Actions", new Vector2(0.82f, 0.02f), new Vector2(0.99f, 0.33f));
        var vlayout = actions.gameObject.AddComponent<VerticalLayoutGroup>();
        vlayout.spacing = 16f;
        vlayout.childControlWidth = true;
        vlayout.childControlHeight = true;
        vlayout.childForceExpandHeight = false;
        _endTurnButton = MakeButton(actions, "EndTurn", "ターン終了", OnEndTurnClicked, new Color(0.25f, 0.5f, 0.3f), 30, TextAnchor.MiddleCenter);
        _endTurnButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 90f;
        var restart = MakeButton(actions, "Restart", "リスタート", OnRestartClicked, new Color(0.4f, 0.3f, 0.3f), 26, TextAnchor.MiddleCenter);
        restart.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
        var replay = MakeButton(actions, "Replay", "トレース再生", StartReplay, new Color(0.3f, 0.3f, 0.45f), 22, TextAnchor.MiddleCenter);
        replay.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
        var rngToggle = MakeButton(actions, "RngToggle", RngButtonText(), () => SetFixedRng(!useFixedRng), new Color(0.3f, 0.35f, 0.35f), 20, TextAnchor.MiddleCenter);
        rngToggle.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;
        _rngButtonLabel = rngToggle.GetComponentInChildren<Text>();

        // Result overlay
        var overlay = MakeImage(root, "ResultOverlay", new Color(0f, 0f, 0f, 0.75f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        _overlay = overlay.gameObject;
        _overlayText = MakeText(MakeRect(overlay, "Result", new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.7f)), "Text", 80, TextAnchor.MiddleCenter);
        var overlayRestart = MakeButton(MakeRect(overlay, "RestartHolder", new Vector2(0.4f, 0.35f), new Vector2(0.6f, 0.45f)),
            "Restart", "もう一度", OnRestartClicked, new Color(0.4f, 0.3f, 0.3f), 32, TextAnchor.MiddleCenter);
        Stretch(overlayRestart.GetComponent<RectTransform>());
        _overlay.SetActive(false);
    }

    // ---- UGUI helpers ----------------------------------------------------------

    private static Sprite MakeWhiteSprite()
    {
        var tex = new Texture2D(4, 4);
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
    }

    private static RectTransform MakeRect(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private RectTransform MakeImage(RectTransform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = MakeRect(parent, name, anchorMin, anchorMax);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = _white;
        img.color = color;
        return rt;
    }

    private RectTransform MakePanel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        return MakeImage(parent, name, PanelColor, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
    }

    private RectTransform MakeFigure(RectTransform arena, string name, Color color)
    {
        // Anchored at the arena's bottom-centre; pivot at the feet so posture
        // rotation/scale happens around the ground contact point.
        var rt = MakeRect(arena, name, new Vector2(0.5f, 0.11f), new Vector2(0.5f, 0.11f));
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(110f, 220f);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = _white;
        img.color = color;
        return rt;
    }

    private Text MakeText(RectTransform parent, string name, int size, TextAnchor anchor)
    {
        var rt = MakeRect(parent, name, Vector2.zero, Vector2.one);
        rt.offsetMin = new Vector2(12f, 8f);
        rt.offsetMax = new Vector2(-12f, -8f);
        var text = rt.gameObject.AddComponent<Text>();
        text.font = _font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private Button MakeButton(RectTransform parent, string name, string label, Action onClick,
        Color color, int fontSize, TextAnchor anchor)
    {
        var rt = MakeImage(parent, name, color, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = rt.GetComponent<Image>();
        var colors = button.colors;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        button.colors = colors;
        button.onClick.AddListener(() => onClick());
        MakeText(rt, "Label", fontSize, anchor).text = label;
        return button;
    }
}
#endif
