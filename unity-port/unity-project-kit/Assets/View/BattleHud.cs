// L2 Hud (top band + status band) and the L3 stamina band / log / end-turn corner.
// Holds "shown" values so the director can animate bars and pips beat by beat and
// then settle them with ApplyStatic.
#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using BattleCore;
using UnityEngine;
using UnityEngine.UI;

public sealed class HpBar
{
    public RectTransform Root;
    public Image Trail;
    public Image FillImg;
    public Text Label;
    public RectTransform Shield;
    public Image ShieldImg;
    public Text ShieldText;
    public float Width;
    public int ShownHp;
    public int ShownGuard;
    public int Max = 1;

    public void Set(int hp, int max)
    {
        ShownHp = hp;
        Max = Mathf.Max(1, max);
        float w = Width * Mathf.Clamp01((float)hp / Max);
        FillImg.rectTransform.sizeDelta = new Vector2(w, FillImg.rectTransform.sizeDelta.y);
        Trail.rectTransform.sizeDelta = new Vector2(w, Trail.rectTransform.sizeDelta.y);
        Label.text = $"HP {hp} / {max}";
    }

    public void SetGuard(int guard)
    {
        ShownGuard = guard;
        ShieldText.text = guard.ToString();
        float a = guard > 0 ? 1f : 0.35f;
        ShieldImg.color = BattleTheme.WithAlpha(BattleTheme.Guard, a);
        ShieldText.color = BattleTheme.WithAlpha(BattleTheme.Ink, a);
    }
}

public sealed class BattleHud
{
    private readonly MonoBehaviour _host;

    // top band
    private Text _timeLabel;
    private readonly List<Image> _timeCells = new List<Image>();
    private Text _miasmaLabel;
    private readonly List<Image> _miasmaCells = new List<Image>();
    private Text _centerLabel;
    private Text _enemyNameLabel;
    public Button JournalButton { get; private set; }

    // status band
    public HpBar PlayerBar { get; private set; }
    public HpBar EnemyBar { get; private set; }
    private readonly List<Image> _enemyPips = new List<Image>();
    private RectTransform _enemyPipRow;
    public int EnemyStaminaShown { get; private set; }

    // stamina band
    private Text _staminaLabel;
    private RectTransform _pipRow;
    private readonly List<RectTransform> _pips = new List<RectTransform>();
    private readonly List<Image> _pipCores = new List<Image>();
    private readonly List<Image> _pipRings = new List<Image>();
    private RectTransform _reserveLine;
    private Text _reserveLineLabel;
    public int StaminaShown { get; private set; }
    private int _maxShown;
    private int _penaltyShown;
    private int _previewCount;
    private float _blinkTime;

    // corners
    private Text _log;
    private Text _piles;
    private Text _reservePreview;
    public Button EndTurnButton { get; private set; }

    private string _phaseText = "あなたの番";
    private int _floor = 1;
    private int _turn = 1;

    private const float PipStep = 42f;
    private const float PipSize = 32f;

    public BattleHud(MonoBehaviour host)
    {
        _host = host;
    }

    // ---- build ----------------------------------------------------------------------

    public void Build(RectTransform hudLayer, RectTransform handLayer, Action onJournal, Action onEndTurn)
    {
        BuildTopBand(hudLayer, onJournal);
        BuildStatusBand(hudLayer);
        BuildStaminaBand(handLayer);
        BuildCorners(handLayer, onEndTurn);
    }

    private void BuildTopBand(RectTransform layer, Action onJournal)
    {
        var band = UiKit.Panel(layer, "TopBand", 0f, BattleTheme.TopBand.x, 1f, BattleTheme.TopBand.y).rectTransform;
        UiKit.Image(band, "BottomLine", ProceduralArt.White, BattleTheme.Line, new Vector2(0f, 0f), new Vector2(1f, 0f))
            .rectTransform.offsetMax = new Vector2(0f, 1f);

        // Left: time limit + miasma.
        var left = UiKit.Box(band, "Left", 0.01f, 0f, 0.36f, 1f);
        UiKit.Label(left, "TimeLabel", 20, TextAnchor.MiddleLeft, BattleTheme.Ink2,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(90f, 40f), new Vector2(6f, 0f), "刻限");
        var timeRow = UiKit.Point(left, "TimeCells", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(200f, 16f), new Vector2(70f, 0f));
        for (int i = 0; i < 10; i++)
        {
            var c = UiKit.Sprite(timeRow, $"T{i}", ProceduralArt.White, BattleTheme.Ink,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 14f), new Vector2(i * 18f, 0f));
            _timeCells.Add(c);
        }
        _timeLabel = UiKit.Label(left, "TimeText", 20, TextAnchor.MiddleLeft, BattleTheme.Ink,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(90f, 40f), new Vector2(256f, 0f), "6/10");

        UiKit.Label(left, "MiasmaLabel", 20, TextAnchor.MiddleLeft, BattleTheme.Ink2,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(90f, 40f), new Vector2(330f, 0f), "瘴気");
        var miasmaRow = UiKit.Point(left, "MiasmaCells", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(200f, 16f), new Vector2(390f, 0f));
        for (int i = 0; i < 10; i++)
        {
            var c = UiKit.Sprite(miasmaRow, $"M{i}", ProceduralArt.White, BattleTheme.Omen,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 14f), new Vector2(i * 18f, 0f));
            _miasmaCells.Add(c);
        }
        _miasmaLabel = UiKit.Label(left, "MiasmaText", 18, TextAnchor.MiddleLeft, BattleTheme.Ink,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(260f, 40f), new Vector2(576f, 0f), "");

        // Centre: floor / turn / phase.
        _centerLabel = UiKit.Text(UiKit.Box(band, "Center", 0.45f, 0f, 0.70f, 1f), "Center", 22, TextAnchor.MiddleCenter, BattleTheme.Ink);
        _centerLabel.fontStyle = FontStyle.Bold;

        // Right: enemy name + journal button.
        var right = UiKit.Box(band, "Right", 0.70f, 0f, 0.99f, 1f);
        _enemyNameLabel = UiKit.Text(UiKit.Box(right, "EnemyName", 0f, 0f, 0.5f, 1f), "Name", 22, TextAnchor.MiddleRight, BattleTheme.Ink);
        _enemyNameLabel.fontStyle = FontStyle.Bold;
        JournalButton = UiKit.Button(right, "JournalButton", "手記を開く（読むだけ）", onJournal,
            BattleTheme.WithAlpha(BattleTheme.Paper, 0.12f), BattleTheme.Ink, 17, new Vector2(0.53f, 0.18f), new Vector2(1f, 0.82f));
        UiKit.Frame(JournalButton.GetComponent<RectTransform>(), BattleTheme.Line, 1f);
    }

    private void BuildStatusBand(RectTransform layer)
    {
        var band = UiKit.Box(layer, "StatusBand", 0f, BattleTheme.StatusBand.x, 1f, BattleTheme.StatusBand.y);
        PlayerBar = MakeBar(UiKit.Box(band, "PlayerStatus", 0.04f, 0f, 0.34f, 1f), "あなた", false);
        EnemyBar = MakeBar(UiKit.Box(band, "EnemyStatus", 0.66f, 0f, 0.96f, 1f), "", true);

        // Enemy stamina pips under the enemy bar (10 max).
        _enemyPipRow = UiKit.Point(EnemyBar.Root, "EnemyPips", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(260f, 18f), new Vector2(0f, 4f));
        UiKit.Label(EnemyBar.Root, "EnemyPipLabel", 16, TextAnchor.MiddleRight, BattleTheme.Ink2,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(120f, 20f), new Vector2(-270f, 4f), "スタミナ");
        for (int i = 0; i < 14; i++)
        {
            var p = UiKit.Sprite(_enemyPipRow, $"EP{i}", ProceduralArt.Circle, BattleTheme.Omen,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(14f, 14f), new Vector2(-i * 18f, 0f));
            _enemyPips.Add(p);
        }
    }

    private HpBar MakeBar(RectTransform root, string title, bool rightAligned)
    {
        var bar = new HpBar { Root = root, Width = 440f };
        float ax = rightAligned ? 1f : 0f;
        var pivot = new Vector2(ax, 0.5f);
        if (title.Length > 0)
        {
            UiKit.Label(root, "Title", 18, rightAligned ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, BattleTheme.Ink2,
                new Vector2(ax, 1f), new Vector2(ax, 1f), new Vector2(200f, 24f), new Vector2(0f, -2f), title);
        }
        var track = UiKit.Sprite(root, "Track", ProceduralArt.White, BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.6f),
            new Vector2(ax, 0.5f), pivot, new Vector2(bar.Width, 18f), new Vector2(0f, 6f));
        var trackRt = track.rectTransform;
        bar.Trail = UiKit.Sprite(trackRt, "Trail", ProceduralArt.White, BattleTheme.Whiff,
            new Vector2(ax, 0.5f), pivot, new Vector2(bar.Width, 18f), Vector2.zero);
        bar.FillImg = UiKit.Sprite(trackRt, "Fill", ProceduralArt.White, BattleTheme.Warm,
            new Vector2(ax, 0.5f), pivot, new Vector2(bar.Width, 18f), Vector2.zero);
        // Ticks every 5 HP (drawn for a 30-HP scale; re-laid on Set if max differs).
        for (int i = 1; i < 6; i++)
        {
            float x = bar.Width * i / 6f;
            UiKit.Sprite(trackRt, $"Tick{i}", ProceduralArt.White, BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.5f),
                new Vector2(ax, 0.5f), pivot, new Vector2(1f, 18f), new Vector2(rightAligned ? -x : x, 0f));
        }
        UiKit.Frame(trackRt, BattleTheme.Line, 1f);
        bar.Label = UiKit.Label(root, "HpLabel", 22, rightAligned ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, BattleTheme.Ink,
            new Vector2(ax, 0.5f), pivot, new Vector2(160f, 30f), new Vector2(rightAligned ? -bar.Width - 10f : bar.Width + 10f, 6f));
        bar.Label.fontStyle = FontStyle.Bold;

        bar.Shield = UiKit.Point(root, "Shield", new Vector2(ax, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(44f, 50f),
            new Vector2(rightAligned ? -bar.Width - 205f : bar.Width + 205f, 6f));
        bar.ShieldImg = UiKit.Image(bar.Shield, "Icon", ProceduralArt.Shield, BattleTheme.Guard, Vector2.zero, Vector2.one);
        bar.ShieldText = UiKit.Text(bar.Shield, "Value", 20, TextAnchor.MiddleCenter, BattleTheme.Ink, "0");
        bar.ShieldText.fontStyle = FontStyle.Bold;
        bar.ShieldText.rectTransform.offsetMin = new Vector2(0f, 4f);
        bar.ShieldText.rectTransform.offsetMax = new Vector2(0f, 0f);
        bar.Set(30, 30);
        bar.SetGuard(0);
        return bar;
    }

    private void BuildStaminaBand(RectTransform layer)
    {
        var band = UiKit.Box(layer, "StaminaBand", 0.3f, BattleTheme.StaminaBand.x, 0.7f, BattleTheme.StaminaBand.y);
        _staminaLabel = UiKit.Label(band, "Label", 20, TextAnchor.MiddleCenter, BattleTheme.Ink,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(700f, 28f), new Vector2(0f, -2f), "スタミナ");
        _pipRow = UiKit.Point(band, "Pips", new Vector2(0.5f, 0.42f), new Vector2(0f, 0.5f), new Vector2(PipStep * 14f, PipSize), Vector2.zero);
        // The stamina label sits to the left of the pip row (same line) so the chip row above stays clear.
        _staminaLabel.rectTransform.SetParent(_pipRow, false);
        _staminaLabel.alignment = TextAnchor.MiddleRight;
        _staminaLabel.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        _staminaLabel.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        _staminaLabel.rectTransform.pivot = new Vector2(1f, 0.5f);
        _staminaLabel.rectTransform.sizeDelta = new Vector2(420f, 28f);
        _staminaLabel.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
        for (int i = 0; i < 14; i++)
        {
            var pip = UiKit.Point(_pipRow, $"Pip{i}", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(PipSize, PipSize),
                new Vector2(i * PipStep + PipSize / 2f, 0f));
            var ring = UiKit.Image(pip, "Ring", ProceduralArt.Circle, BattleTheme.Accent, Vector2.zero, Vector2.one);
            var glass = UiKit.Image(pip, "Glass", ProceduralArt.Circle, BattleTheme.Ground, Vector2.zero, Vector2.one);
            glass.rectTransform.offsetMin = new Vector2(3f, 3f);
            glass.rectTransform.offsetMax = new Vector2(-3f, -3f);
            var core = UiKit.Image(pip, "Core", ProceduralArt.SoftCircle, BattleTheme.Warm, Vector2.zero, Vector2.one);
            core.rectTransform.offsetMin = new Vector2(7f, 7f);
            core.rectTransform.offsetMax = new Vector2(-7f, -7f);
            _pips.Add(pip);
            _pipRings.Add(ring);
            _pipCores.Add(core);
        }
        _reserveLine = UiKit.Point(_pipRow, "ReserveLine", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(2f, PipSize + 14f),
            new Vector2(3f * PipStep - (PipStep - PipSize) / 2f, 0f));
        UiKit.Fill(_reserveLine, "Line", BattleTheme.WithAlpha(BattleTheme.Ink, 0.6f));
        _reserveLineLabel = UiKit.Label(_reserveLine, "Label", 14, TextAnchor.UpperCenter, BattleTheme.Ink2,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(320f, 18f), new Vector2(0f, -1f), "構え線（残 3 で Guard +2）");
    }

    private void BuildCorners(RectTransform layer, Action onEndTurn)
    {
        var left = UiKit.Box(layer, "LeftBottom", BattleTheme.LeftBottomX.x, 0.02f, BattleTheme.LeftBottomX.y, BattleTheme.HandBand.y - 0.01f);
        _log = UiKit.Text(left, "Log", 15, TextAnchor.LowerLeft, BattleTheme.Ink2);
        _log.rectTransform.offsetMin = new Vector2(6f, 34f);
        _piles = UiKit.Label(left, "Piles", 16, TextAnchor.LowerLeft, BattleTheme.Ink2,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(200f, 26f), new Vector2(6f, 4f), "山 0 / 捨 0");

        var right = UiKit.Box(layer, "RightBottom", BattleTheme.RightBottomX.x, 0.02f, BattleTheme.RightBottomX.y, BattleTheme.HandBand.y - 0.01f);
        EndTurnButton = UiKit.Button(right, "EndTurn", "ターン終了", onEndTurn, BattleTheme.WithAlpha(BattleTheme.Accent, 0.14f), BattleTheme.Ink, 28,
            new Vector2(0f, 0f), new Vector2(1f, 0.42f));
        UiKit.Frame(EndTurnButton.GetComponent<RectTransform>(), BattleTheme.Accent, 2f);
        _reservePreview = UiKit.Text(UiKit.Box(right, "ReservePreview", 0f, 0.45f, 1f, 0.75f), "Text", 16, TextAnchor.LowerCenter, BattleTheme.Ink2);
    }

    // ---- per-frame -------------------------------------------------------------------------

    public void Tick(float dt)
    {
        if (_previewCount <= 0) return;
        _blinkTime += dt;
        bool on = Mathf.Repeat(_blinkTime, 0.6f) < 0.3f;
        for (int i = StaminaShown - _previewCount; i < StaminaShown; i++)
        {
            if (i < 0 || i >= _pips.Count) continue;
            _pipCores[i].color = BattleTheme.WithAlpha(BattleTheme.Warm, on ? 1f : 0.25f);
        }
    }

    // ---- settled values -----------------------------------------------------------------------

    public void ApplyStatic(BattleViewModel vm)
    {
        _floor = vm.Floor;
        _turn = vm.Turn;
        SetPhase(_phaseText);
        _enemyNameLabel.text = vm.EnemyName;

        for (int i = 0; i < _timeCells.Count; i++)
        {
            bool lit = i < vm.TimeLimitLeft * 10 / Mathf.Max(1, vm.TimeLimitMax);
            _timeCells[i].color = lit ? BattleTheme.Ink : BattleTheme.WithAlpha(BattleTheme.Ink, 0.18f);
        }
        _timeLabel.text = $"{vm.TimeLimitLeft}/{vm.TimeLimitMax}";
        for (int i = 0; i < _miasmaCells.Count; i++)
        {
            bool lit = i < vm.MiasmaPercent / 10;
            _miasmaCells[i].color = lit ? BattleTheme.Omen : BattleTheme.WithAlpha(BattleTheme.Omen, 0.18f);
        }
        _miasmaLabel.text = $"{vm.MiasmaPercent}%  濃度 {vm.MiasmaDensity}  スタミナ −{vm.MiasmaPenalty}";

        PlayerBar.Set(vm.PlayerHp, vm.PlayerMaxHp);
        PlayerBar.SetGuard(vm.PlayerGuard);
        EnemyBar.Set(vm.EnemyHp, vm.EnemyMaxHp);
        EnemyBar.SetGuard(vm.EnemyGuard);
        SetEnemyStamina(vm.EnemyStamina, vm.EnemyMaxStamina);

        SetStaminaLayout(vm.PlayerMaxStamina, vm.MiasmaPenalty);
        SetStamina(vm.PlayerStamina);
        SetPreview(0);

        var lines = new List<string>();
        for (int i = Mathf.Max(0, vm.Log.Count - 3); i < vm.Log.Count; i++) lines.Add(vm.Log[i].Text);
        _log.text = string.Join("\n", lines);
        _piles.text = $"山 {vm.DrawPileCount} / 捨 {vm.DiscardPileCount}";
        _reservePreview.text = vm.ReservePreview;
    }

    public void SetPhase(string phase)
    {
        _phaseText = phase;
        _centerLabel.text = $"第 {_floor} 階層  /  ターン {_turn}  /  {phase}";
    }

    public void SetEnemyStamina(int stamina, int max)
    {
        EnemyStaminaShown = stamina;
        for (int i = 0; i < _enemyPips.Count; i++)
        {
            bool exists = i < max;
            _enemyPips[i].gameObject.SetActive(exists);
            if (!exists) continue;
            _enemyPips[i].color = i < stamina ? BattleTheme.Omen : BattleTheme.WithAlpha(BattleTheme.Omen, 0.18f);
            _enemyPips[i].rectTransform.anchoredPosition = new Vector2(-i * 18f, 0f);
            _enemyPips[i].rectTransform.localScale = Vector3.one;
        }
    }

    private void SetStaminaLayout(int max, int penalty)
    {
        _maxShown = max;
        _penaltyShown = penalty;
        int total = Mathf.Min(_pips.Count, max + penalty);
        _pipRow.sizeDelta = new Vector2(total * PipStep, PipSize);
        _pipRow.anchoredPosition = new Vector2(-total * PipStep / 2f, 0f);
        for (int i = 0; i < _pips.Count; i++)
        {
            bool exists = i < total;
            _pips[i].gameObject.SetActive(exists);
            if (!exists) continue;
            bool lost = i >= max; // slots the miasma took: dashed empty frames
            _pipRings[i].color = lost ? BattleTheme.WithAlpha(BattleTheme.Whiff, 0.45f) : BattleTheme.Accent;
            _pips[i].localScale = lost ? new Vector3(0.8f, 0.8f, 1f) : Vector3.one;
            _pips[i].anchoredPosition = new Vector2(i * PipStep + PipSize / 2f, 0f);
        }
        _reserveLine.gameObject.SetActive(max >= 3);
    }

    public void SetStamina(int stamina)
    {
        StaminaShown = stamina;
        for (int i = 0; i < _pips.Count; i++)
        {
            bool lit = i < stamina && i < _maxShown;
            _pipCores[i].color = BattleTheme.WithAlpha(BattleTheme.Warm, lit ? 1f : 0f);
            _pipRings[i].color = i >= _maxShown
                ? BattleTheme.WithAlpha(BattleTheme.Whiff, 0.45f)
                : BattleTheme.WithAlpha(BattleTheme.Accent, lit ? 1f : 0.35f);
        }
        string penalty = _penaltyShown > 0 ? $"（最大 {_maxShown + _penaltyShown} − 瘴気 {_penaltyShown}）" : "";
        _staminaLabel.text = $"スタミナ {stamina} / {_maxShown}{penalty}";
    }

    /// <summary>Blink the pips a selected chip would spend (消費予告). 0 clears.</summary>
    public void SetPreview(int invest)
    {
        _previewCount = invest;
        _blinkTime = 0f;
        if (invest == 0) SetStamina(StaminaShown);
    }

    public void SetInteractable(bool on)
    {
        EndTurnButton.interactable = on;
        JournalButton.interactable = on;
    }

    // ---- beats ---------------------------------------------------------------------------------

    /// <summary>Light pips one by one, 80 ms apart.</summary>
    public IEnumerator LightPips(int from, int to)
    {
        for (int i = from; i < to; i++)
        {
            SetStamina(i + 1);
            yield return UiTween.Wait(80f);
        }
        SetStamina(to);
    }

    /// <summary>Spend pips one by one, 60 ms apart.</summary>
    public IEnumerator SpendPips(int count)
    {
        for (int i = 0; i < count; i++)
        {
            SetStamina(Mathf.Max(0, StaminaShown - 1));
            yield return UiTween.Wait(60f);
        }
    }

    /// <summary>構え: pips 1–3 glow for 250 ms.</summary>
    public IEnumerator GlowReservePips()
    {
        yield return UiTween.Run(250f, Ease.Out, t =>
        {
            float s = 1f + 0.25f * Mathf.Sin(t * Mathf.PI);
            for (int i = 0; i < 3 && i < _pips.Count; i++)
            {
                _pips[i].localScale = new Vector3(s, s, 1f);
                _pipRings[i].color = Color.Lerp(BattleTheme.Accent, Color.white, Mathf.Sin(t * Mathf.PI) * 0.6f);
            }
        });
        for (int i = 0; i < 3 && i < _pips.Count; i++) _pips[i].localScale = Vector3.one;
        SetStamina(StaminaShown);
    }

    public IEnumerator EnemyLightPips(int from, int to, int max)
    {
        for (int i = from; i < to; i++)
        {
            SetEnemyStamina(i + 1, max);
            yield return UiTween.Wait(80f);
        }
    }

    public IEnumerator EnemySpendPips(int count, int max)
    {
        for (int i = 0; i < count; i++)
        {
            SetEnemyStamina(Mathf.Max(0, EnemyStaminaShown - 1), max);
            yield return UiTween.Wait(60f);
        }
    }

    /// <summary>崩し: the top-most lit pip shatters (drops 40 px and fades) over 300 ms.</summary>
    public IEnumerator ShatterPip(Actor target, int max)
    {
        if (target == Actor.Enemy)
        {
            int idx = Mathf.Clamp(EnemyStaminaShown - 1, 0, _enemyPips.Count - 1);
            var img = _enemyPips[idx];
            Vector2 start = img.rectTransform.anchoredPosition;
            yield return UiTween.Run(300f, Ease.In, t =>
            {
                img.rectTransform.anchoredPosition = start + new Vector2(0f, -40f * t);
                img.color = BattleTheme.WithAlpha(BattleTheme.Omen, 1f - t);
            });
            SetEnemyStamina(Mathf.Max(0, EnemyStaminaShown - 1), max);
        }
        else
        {
            int idx = Mathf.Clamp(StaminaShown - 1, 0, _pips.Count - 1);
            var pip = _pips[idx];
            Vector2 start = pip.anchoredPosition;
            yield return UiTween.Run(300f, Ease.In, t =>
            {
                pip.anchoredPosition = start + new Vector2(0f, -40f * t);
                _pipCores[idx].color = BattleTheme.WithAlpha(BattleTheme.Warm, 1f - t);
            });
            pip.anchoredPosition = start;
            SetStamina(Mathf.Max(0, StaminaShown - 1));
        }
    }

    /// <summary>HP bar drains over 300 ms; the grey trail follows 500 ms later.</summary>
    public IEnumerator DrainHp(HpBar bar, int to)
    {
        int from = bar.ShownHp;
        float w0 = bar.Width * Mathf.Clamp01((float)from / bar.Max);
        float w1 = bar.Width * Mathf.Clamp01((float)to / bar.Max);
        bar.ShownHp = to;
        bar.Label.text = $"HP {to} / {bar.Max}";
        _host.StartCoroutine(UiTween.Run(500f, Ease.Out, t =>
            bar.Trail.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(w0, w1, Mathf.Clamp01((t - 0.4f) / 0.6f)), 18f)));
        yield return UiTween.Run(300f, Ease.Out, t => bar.FillImg.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(w0, w1, t), 18f));
    }

    public IEnumerator PopShield(HpBar bar, int value)
    {
        bar.SetGuard(value);
        yield return UiTween.Pop(bar.Shield, 300f);
    }

    public IEnumerator CrackShield(HpBar bar, int value)
    {
        yield return UiTween.Pop(bar.Shield, 200f);
        bar.SetGuard(value);
    }

    public IEnumerator FadeShield(HpBar bar)
    {
        int shown = 0;
        yield return UiTween.Run(150f, Ease.Linear, t => bar.ShieldImg.color = BattleTheme.WithAlpha(BattleTheme.Guard, Mathf.Lerp(1f, 0.35f, t)));
        bar.SetGuard(shown);
    }
}
#endif
