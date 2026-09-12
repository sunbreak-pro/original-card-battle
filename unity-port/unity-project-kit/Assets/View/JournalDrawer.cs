// L4 Drawer: the enemy's journal page (手記). Read-only in battle; opening it
// changes nothing about the omen. Paper, fibres, a torn corner for low
// disclosure, and ink-blot "？" rows for what is not yet known.
#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using BattleCore;
using UnityEngine;
using UnityEngine.UI;

public sealed class JournalDrawer
{
    private const float Width = BattleTheme.RefWidth * 0.30f;

    private readonly MonoBehaviour _host;
    private RectTransform _panel;
    private RectTransform _content;
    private RectTransform _tear;
    private Text _title;
    private Text _disclosure;
    private readonly List<Text> _tendencyRows = new List<Text>();
    private readonly List<Text> _specialRows = new List<Text>();
    private Text _weakness;
    private Text _adaptation;
    private readonly List<GameObject> _blots = new List<GameObject>();
    private bool _open;
    private Coroutine _anim;

    public bool IsOpen => _open;
    public Action<bool> OnOpenChanged;

    public JournalDrawer(MonoBehaviour host)
    {
        _host = host;
    }

    public void Build(RectTransform drawerLayer)
    {
        _panel = UiKit.Rect(drawerLayer, "JournalDrawer", new Vector2(1f, 0f), new Vector2(1f, 1f));
        _panel.pivot = new Vector2(1f, 0.5f);
        _panel.sizeDelta = new Vector2(Width, 0f);
        _panel.anchoredPosition = new Vector2(Width, 0f); // parked off screen

        var shadow = UiKit.Image(_panel, "Shadow", ProceduralArt.White, BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.55f),
            new Vector2(0f, 0f), new Vector2(0f, 1f));
        shadow.rectTransform.offsetMin = new Vector2(-18f, 0f);
        shadow.rectTransform.offsetMax = new Vector2(0f, 0f);
        var paper = UiKit.Fill(_panel, "Paper", BattleTheme.Paper);
        paper.raycastTarget = true; // swallow clicks so the arena below is not hit
        var grain = UiKit.Fill(_panel, "Grain", Color.white);
        grain.sprite = ProceduralArt.PaperGrain;
        grain.type = Image.Type.Tiled;
        UiKit.Image(_panel, "Spine", ProceduralArt.White, BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.25f),
            new Vector2(0f, 0f), new Vector2(0f, 1f)).rectTransform.offsetMax = new Vector2(3f, 0f);

        // Torn corner: bigger when less is disclosed.
        _tear = UiKit.Point(_panel, "Tear", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(140f, 140f), Vector2.zero);
        UiKit.Image(_tear, "Dark", ProceduralArt.Triangle, BattleTheme.Ground, Vector2.zero, Vector2.one);

        // Tabs.
        var tabs = UiKit.Box(_panel, "Tabs", 0.04f, 0.90f, 0.96f, 0.96f);
        string[] names = { "敵の頁", "ダンジョンの頁", "メモ" };
        for (int i = 0; i < 3; i++)
        {
            float x0 = i / 3f, x1 = (i + 1) / 3f - 0.02f;
            var b = UiKit.Button(tabs, $"Tab{i}", names[i], null,
                i == 0 ? BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.12f) : BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.04f),
                BattleTheme.PaperInk, 17, new Vector2(x0, 0f), new Vector2(x1, 1f));
            b.interactable = i == 0;
            UiKit.Frame(b.GetComponent<RectTransform>(), BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.3f), 1f);
        }

        _content = UiKit.Box(_panel, "Content", 0.06f, 0.06f, 0.94f, 0.88f);
        _title = UiKit.Label(_content, "Title", 34, TextAnchor.MiddleLeft, BattleTheme.PaperInk,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Width * 0.8f, 44f), new Vector2(0f, 0f));
        _title.fontStyle = FontStyle.Bold;
        _disclosure = UiKit.Label(_content, "Disclosure", 16, TextAnchor.MiddleLeft, BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.7f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Width * 0.8f, 24f), new Vector2(0f, -46f));
        Rule(-74f);

        float y = -90f;
        y = Section("間合いごとの傾向", y);
        for (int i = 0; i < 3; i++) _tendencyRows.Add(Row(ref y));
        Rule(y - 4f);
        y -= 18f;
        y = Section("特殊行動", y);
        for (int i = 0; i < 2; i++) _specialRows.Add(Row(ref y));
        Rule(y - 4f);
        y -= 18f;
        y = Section("弱点・耐性", y);
        _weakness = Row(ref y);
        y = Section("適応", y);
        _adaptation = Row(ref y);
        Rule(y - 4f);
        y -= 18f;
        y = Section("メモ", y);
        var memo = Row(ref y);
        memo.text = "（戦闘中は書けない）";
        memo.color = BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.55f);

        var close = UiKit.Button(_panel, "Close", "閉じる", () => Close(), BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.10f), BattleTheme.PaperInk, 20,
            new Vector2(0.62f, 0.015f), new Vector2(0.94f, 0.055f));
        UiKit.Frame(close.GetComponent<RectTransform>(), BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.4f), 1f);
    }

    private void Rule(float y)
    {
        // A brushed rule: slightly uneven thickness suggested by two overlapping lines.
        UiKit.Sprite(_content, "Rule", ProceduralArt.White, BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.35f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Width * 0.86f, 2f), new Vector2(0f, y));
        UiKit.Sprite(_content, "Rule2", ProceduralArt.White, BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.18f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Width * 0.7f, 2f), new Vector2(Width * 0.05f, y - 2f));
    }

    private float Section(string heading, float y)
    {
        var h = UiKit.Label(_content, "Heading", 20, TextAnchor.MiddleLeft, BattleTheme.PaperInk,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Width * 0.8f, 28f), new Vector2(0f, y), heading);
        h.fontStyle = FontStyle.Bold;
        return y - 30f;
    }

    private Text Row(ref float y)
    {
        var blot = UiKit.Sprite(_content, "Blot", ProceduralArt.SoftCircle, BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.5f),
            new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(46f, 26f), new Vector2(8f, y - 11f));
        blot.gameObject.SetActive(false);
        _blots.Add(blot.gameObject);
        var t = UiKit.Label(_content, "Row", 17, TextAnchor.MiddleLeft, BattleTheme.PaperInk,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Width * 0.86f, 24f), new Vector2(12f, y), "");
        y -= 26f;
        return t;
    }

    public void Apply(JournalView journal)
    {
        _title.text = journal.EnemyName;
        _disclosure.text = $"開示度 {journal.Disclosure} / {journal.DisclosureMax}";
        float tear = Mathf.Lerp(200f, 40f, (float)journal.Disclosure / Mathf.Max(1, journal.DisclosureMax));
        _tear.sizeDelta = new Vector2(tear, tear);

        foreach (var b in _blots) b.SetActive(false);
        Fill(_tendencyRows, journal.TendencyLines);
        Fill(_specialRows, journal.SpecialLines);
        SetRow(_weakness, journal.WeaknessLine);
        SetRow(_adaptation, journal.AdaptationLine);
    }

    private void Fill(List<Text> rows, IReadOnlyList<string> lines)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            SetRow(rows[i], i < lines.Count ? lines[i] : "");
        }
    }

    private void SetRow(Text row, string text)
    {
        row.text = text;
        bool unknown = text.Contains("？");
        row.color = unknown ? BattleTheme.WithAlpha(BattleTheme.PaperInk, 0.6f) : BattleTheme.PaperInk;
        int idx = _blots.FindIndex(b => b.transform.GetSiblingIndex() == row.transform.GetSiblingIndex() - 1);
        if (idx >= 0) _blots[idx].SetActive(unknown && text.Length > 0);
    }

    public void Open()
    {
        if (_open) return;
        _open = true;
        OnOpenChanged?.Invoke(true);
        if (_anim != null) _host.StopCoroutine(_anim);
        _anim = _host.StartCoroutine(UiTween.Move(_panel, _panel.anchoredPosition, Vector2.zero, 220f, Ease.Out));
    }

    public void Close()
    {
        if (!_open) return;
        _open = false;
        OnOpenChanged?.Invoke(false);
        if (_anim != null) _host.StopCoroutine(_anim);
        _anim = _host.StartCoroutine(UiTween.Move(_panel, _panel.anchoredPosition, new Vector2(Width, 0f), 220f, Ease.Out));
    }

    public void Toggle()
    {
        if (_open) Close(); else Open();
    }
}
#endif
