// L5 Overlay: phase banner, corner vignette, the fast-forward catcher shown while
// a sequence plays, debug buttons, and the two endings (敗北 / 撃破).
#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections;
using BattleCore;
using UnityEngine;
using UnityEngine.UI;

public sealed class ResultOverlay
{
    private readonly MonoBehaviour _host;
    private RectTransform _layer;

    private CanvasGroup _phaseGroup;
    private Text _phaseText;
    private readonly Image[] _vignette = new Image[4];
    private Button _catcher;

    // defeat
    private RectTransform _inkFlood;
    private Image _inkImg;
    private Text _defeatText;
    private CanvasGroup _defeatGroup;
    private RectTransform _page;
    private CanvasGroup _pageGroup;
    private Text _pageFloor;

    // victory
    private RectTransform _victory;
    private CanvasGroup _victoryGroup;
    private Text _victoryTitle;
    private Text _carryText;

    private RectTransform _debug;

    public Action OnFastForward;
    public Action OnContinue;
    public Action OnRestart;
    public Action OnToggleRng;
    public Action OnReplayTrace;
    private Text _rngLabel;

    public ResultOverlay(MonoBehaviour host)
    {
        _host = host;
    }

    public void Build(RectTransform overlayLayer, bool showDebug)
    {
        _layer = overlayLayer;

        // Vignette: four corners, always faint; turns to omen at low HP.
        for (int i = 0; i < 4; i++)
        {
            Vector2 anchor = new Vector2(i % 2, i / 2);
            var v = UiKit.Sprite(_layer, $"Vignette{i}", ProceduralArt.Vignette, BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.08f),
                anchor, anchor, new Vector2(700f, 700f), Vector2.zero);
            v.rectTransform.localScale = new Vector3(i % 2 == 0 ? 1f : -1f, i / 2 == 0 ? 1f : -1f, 1f);
            _vignette[i] = v;
        }

        // Fast-forward catcher: full-screen transparent button, active only while locked.
        _catcher = UiKit.Button(_layer, "FastForward", "", () => OnFastForward?.Invoke(), new Color(0f, 0f, 0f, 0f), BattleTheme.Ink, 1,
            Vector2.zero, Vector2.one);
        _catcher.gameObject.SetActive(false);

        // Phase banner.
        var phase = UiKit.Point(_layer, "PhaseBanner", new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.5f), new Vector2(520f, 80f), Vector2.zero);
        _phaseGroup = UiKit.Group(phase);
        _phaseGroup.alpha = 0f;
        _phaseGroup.blocksRaycasts = false;
        UiKit.Fill(phase, "Bg", BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.55f));
        UiKit.Image(phase, "Line", ProceduralArt.White, BattleTheme.Accent, new Vector2(0.3f, 0f), new Vector2(0.7f, 0f)).rectTransform.offsetMax = new Vector2(0f, 2f);
        _phaseText = UiKit.Text(phase, "Text", 40, TextAnchor.MiddleCenter, BattleTheme.Ink);
        _phaseText.fontStyle = FontStyle.Bold;

        BuildDefeat();
        BuildVictory();
        BuildDebug(showDebug);
    }

    private void BuildDefeat()
    {
        _inkFlood = UiKit.Rect(_layer, "InkFlood", new Vector2(0f, 0f), new Vector2(1f, 0f));
        _inkFlood.pivot = new Vector2(0.5f, 0f);
        _inkFlood.sizeDelta = new Vector2(0f, 0f);
        _inkImg = UiKit.Fill(_inkFlood, "Ink", BattleTheme.InkBlack);
        _inkImg.raycastTarget = true;
        _inkFlood.gameObject.SetActive(false);

        var defeat = UiKit.Point(_layer, "DefeatText", new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), new Vector2(900f, 120f), Vector2.zero);
        _defeatGroup = UiKit.Group(defeat);
        _defeatGroup.alpha = 0f;
        _defeatGroup.blocksRaycasts = false;
        _defeatText = UiKit.Text(defeat, "Text", 64, TextAnchor.MiddleCenter, BattleTheme.Ink, "力尽きた");
        _defeatText.fontStyle = FontStyle.Bold;

        // The journal page that rises from below.
        _page = UiKit.Point(_layer, "LegacyPage", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(760f, 420f), new Vector2(0f, -440f));
        _pageGroup = UiKit.Group(_page);
        _pageGroup.alpha = 0f;
        _pageGroup.blocksRaycasts = false;
        var paper = UiKit.Fill(_page, "Paper", BattleTheme.Paper);
        paper.raycastTarget = true;
        var grain = UiKit.Fill(_page, "Grain", Color.white);
        grain.sprite = ProceduralArt.PaperGrain;
        grain.type = Image.Type.Tiled;
        UiKit.Frame(_page, BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.3f), 1f, 8f);
        var head = UiKit.Label(_page, "Head", 30, TextAnchor.MiddleLeft, BattleTheme.PaperInk,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(700f, 44f), new Vector2(30f, -22f), "手記は ここに 残る");
        head.fontStyle = FontStyle.Bold;
        _pageFloor = UiKit.Label(_page, "Floor", 18, TextAnchor.MiddleLeft, BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.75f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(700f, 26f), new Vector2(30f, -68f), "第 2 階層 / 石室の回廊");
        UiKit.Sprite(_page, "Rule", ProceduralArt.White, BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.35f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(700f, 2f), new Vector2(30f, -90f));
        UiKit.Label(_page, "Pages", 18, TextAnchor.MiddleLeft, BattleTheme.PaperInk,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(700f, 26f), new Vector2(30f, -108f),
            "手記 12 頁　／　敵の頁 3　／　ダンジョンの頁 5　／　メモ 4");
        var legacyHead = UiKit.Label(_page, "LegacyHead", 20, TextAnchor.MiddleLeft, BattleTheme.PaperInk,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(700f, 28f), new Vector2(30f, -152f), "遺産候補（次の生で 3 つまで選べる）");
        legacyHead.fontStyle = FontStyle.Bold;
        string[] legacy = { "突き（習熟 3）", "長柄の歪み兵の頁", "呼吸を整える（習熟 2）" };
        for (int i = 0; i < legacy.Length; i++)
        {
            UiKit.Label(_page, $"Legacy{i}", 18, TextAnchor.MiddleLeft, BattleTheme.PaperInk,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(700f, 26f), new Vector2(50f, -186f - i * 28f), "・ " + legacy[i]);
        }
        // Wax seal.
        var seal = UiKit.Sprite(_page, "Seal", ProceduralArt.Circle, BattleTheme.Omen,
            new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(64f, 64f), new Vector2(-60f, 56f));
        UiKit.Image(seal.rectTransform, "Inner", ProceduralArt.Circle, BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.35f), Vector2.zero, Vector2.one)
            .rectTransform.offsetMin = new Vector2(14f, 14f);
        seal.rectTransform.GetChild(0).GetComponent<RectTransform>().offsetMax = new Vector2(-14f, -14f);
        var go = UiKit.Button(_page, "Continue", "継承の間へ", () => OnContinue?.Invoke(), BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.12f), BattleTheme.PaperInk, 22,
            new Vector2(0.06f, 0.07f), new Vector2(0.40f, 0.19f));
        UiKit.Frame(go.GetComponent<RectTransform>(), BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.45f), 1f);
    }

    private void BuildVictory()
    {
        _victory = UiKit.Point(_layer, "Victory", new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(620f, 240f), Vector2.zero);
        _victoryGroup = UiKit.Group(_victory);
        _victoryGroup.alpha = 0f;
        _victoryGroup.blocksRaycasts = false;
        var bg = UiKit.Fill(_victory, "Bg", BattleTheme.WithAlpha(BattleTheme.Ground, 0.92f));
        bg.raycastTarget = true;
        UiKit.Frame(_victory, BattleTheme.Accent, 2f);
        _victoryTitle = UiKit.Label(_victory, "Title", 48, TextAnchor.MiddleCenter, BattleTheme.Ink,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(600f, 70f), new Vector2(0f, -12f), "撃破");
        _victoryTitle.fontStyle = FontStyle.Bold;
        _carryText = UiKit.Label(_victory, "Carry", 22, TextAnchor.MiddleCenter, BattleTheme.Ink,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(600f, 40f), new Vector2(0f, -100f), "");
        UiKit.Label(_victory, "Note", 15, TextAnchor.MiddleCenter, BattleTheme.Ink2,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(600f, 24f), new Vector2(0f, -136f), "HP とスタミナは次の戦闘へ持ち越す");
        var next = UiKit.Button(_victory, "Next", "次へ", () => OnContinue?.Invoke(), BattleTheme.WithAlpha(BattleTheme.Accent, 0.16f), BattleTheme.Ink, 22,
            new Vector2(0.35f, 0.08f), new Vector2(0.65f, 0.26f));
        UiKit.Frame(next.GetComponent<RectTransform>(), BattleTheme.Accent, 1f);
    }

    private void BuildDebug(bool show)
    {
        _debug = UiKit.Point(_layer, "Debug", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(420f, 30f), new Vector2(-8f, -84f));
        var rng = UiKit.Button(_debug, "Rng", "乱数: 固定", () => OnToggleRng?.Invoke(), BattleTheme.WithAlpha(BattleTheme.Ink, 0.08f), BattleTheme.Ink2, 13,
            new Vector2(0f, 0f), new Vector2(0.4f, 1f));
        _rngLabel = rng.GetComponentInChildren<Text>();
        UiKit.Button(_debug, "Restart", "リスタート", () => OnRestart?.Invoke(), BattleTheme.WithAlpha(BattleTheme.Ink, 0.08f), BattleTheme.Ink2, 13,
            new Vector2(0.42f, 0f), new Vector2(0.7f, 1f));
        UiKit.Button(_debug, "Trace", "トレース再生", () => OnReplayTrace?.Invoke(), BattleTheme.WithAlpha(BattleTheme.Ink, 0.08f), BattleTheme.Ink2, 13,
            new Vector2(0.72f, 0f), new Vector2(1f, 1f));
        _debug.gameObject.SetActive(show);
    }

    public void SetRngLabel(string text)
    {
        if (_rngLabel != null) _rngLabel.text = text;
    }

    public void SetCatcher(bool on) => _catcher.gameObject.SetActive(on);

    public void SetLowHp(bool low)
    {
        Color c = low ? BattleTheme.WithAlpha(BattleTheme.Omen, 0.25f) : BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.08f);
        foreach (var v in _vignette) v.color = c;
    }

    /// <summary>One-shot omen-coloured pulse of the corners (被弾, HP < 30%).</summary>
    public IEnumerator PulseVignette()
    {
        Color from = BattleTheme.WithAlpha(BattleTheme.Omen, 0.5f);
        Color to = BattleTheme.WithAlpha(BattleTheme.Omen, 0.25f);
        yield return UiTween.Run(200f, Ease.Out, t => { foreach (var v in _vignette) v.color = Color.Lerp(from, to, t); });
    }

    public IEnumerator ShowPhase(string text, float ms)
    {
        _phaseText.text = text;
        yield return UiTween.Fade(_phaseGroup, 0f, 1f, ms, Ease.Out);
        yield return UiTween.Wait(320f);
        yield return UiTween.Fade(_phaseGroup, 1f, 0f, 200f, Ease.In);
    }

    public void HideAll()
    {
        _phaseGroup.alpha = 0f;
        _inkFlood.gameObject.SetActive(false);
        _inkFlood.sizeDelta = new Vector2(0f, 0f);
        _defeatGroup.alpha = 0f;
        _defeatGroup.blocksRaycasts = false;
        _page.anchoredPosition = new Vector2(0f, -440f);
        _pageGroup.alpha = 0f;
        _pageGroup.blocksRaycasts = false;
        _victoryGroup.alpha = 0f;
        _victoryGroup.blocksRaycasts = false;
        _victory.localScale = Vector3.one;
    }

    /// <summary>§4.6: ink floods 800 ms ease-in → 「力尽きた」 300 ms → 1 s → page rises 400 ms ease-out.</summary>
    public IEnumerator PlayDefeat(int floor)
    {
        _pageFloor.text = $"第 {floor} 階層 / 石室の回廊";
        _inkFlood.gameObject.SetActive(true);
        float h = BattleTheme.RefHeight;
        yield return UiTween.Run(800f, Ease.In, t => _inkFlood.sizeDelta = new Vector2(0f, h * t));
        _defeatGroup.blocksRaycasts = true;
        yield return UiTween.Run(300f, Ease.Out, t =>
        {
            _defeatGroup.alpha = t;
            _defeatText.rectTransform.localScale = new Vector3(Mathf.Lerp(1.35f, 1f, t), 1f, 1f); // letter-spacing stand-in
        });
        yield return UiTween.Wait(1000f);
        _pageGroup.alpha = 1f;
        _pageGroup.blocksRaycasts = true;
        yield return UiTween.Move(_page, new Vector2(0f, -440f), new Vector2(0f, 60f), 400f, Ease.Out);
    }

    /// <summary>「撃破」 300 ms then the carry-over panel (values from the vm).</summary>
    public IEnumerator PlayVictory(BattleViewModel vm)
    {
        _carryText.text = $"持ち越し:  HP {vm.PlayerHp} / {vm.PlayerMaxHp}  ・  スタミナ {vm.PlayerStamina} / {vm.PlayerMaxStamina}";
        _victoryGroup.blocksRaycasts = true;
        yield return UiTween.Run(300f, Ease.Out, t =>
        {
            _victoryGroup.alpha = t;
            _victory.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, t);
        });
    }
}
#endif
