// L3 Hand: up to three cards, each with its invest chips (T0..T3). One play is two
// operations: click the card (select) → click a chip (play). Hovering a chip
// previews the ghost / target band; the default chip is the "構え維持" invest.
#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using BattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CardWidget
{
    public CardView Data;
    public RectTransform Root;
    public Image Bg;
    public Image[] Frame;
    public Button Button;
    public CanvasGroup Group;
    public RectTransform ChipRow;
    public CanvasGroup ChipGroup;
    public List<Button> Chips = new List<Button>();
    public Vector2 RestPosition;
}

public sealed class HandView
{
    public const float CardWidth = 230f;
    public const float CardHeight = 250f;
    private const float CardGap = 16f;
    private const float ChipWidth = 156f;
    private const float ChipHeight = 66f;

    private readonly MonoBehaviour _host;
    private RectTransform _root;
    private readonly List<CardWidget> _cards = new List<CardWidget>();
    private int _selected = -1;
    private bool _interactable = true;

    public Action<string, int> OnPlay;
    public Action<TierView> OnTierHover;      // null = hover ended
    public Action<CardView> OnSelectionChanged; // null = deselected

    public int SelectedIndex => _selected;
    public CardView Selected => _selected >= 0 && _selected < _cards.Count ? _cards[_selected].Data : null;

    public HandView(MonoBehaviour host)
    {
        _host = host;
    }

    public void Build(RectTransform handLayer)
    {
        _root = UiKit.Box(handLayer, "Hand", BattleTheme.HandX.x, BattleTheme.HandBand.x, BattleTheme.HandX.y, BattleTheme.HandBand.y);
    }

    // ---- settled --------------------------------------------------------------------------

    public void Rebuild(IReadOnlyList<CardView> hand)
    {
        _selected = -1;
        UiKit.ClearChildren(_root);
        _cards.Clear();
        int n = hand.Count;
        float totalW = n * CardWidth + Mathf.Max(0, n - 1) * CardGap;
        for (int i = 0; i < n; i++)
        {
            float x = -totalW / 2f + CardWidth / 2f + i * (CardWidth + CardGap);
            _cards.Add(MakeCard(hand[i], i, new Vector2(x, 6f)));
        }
        ApplyInteractable();
    }

    private CardWidget MakeCard(CardView data, int index, Vector2 position)
    {
        var w = new CardWidget { Data = data, RestPosition = position };
        w.Root = UiKit.Point(_root, $"Card {data.InstanceId}", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(CardWidth, CardHeight), position);
        w.Group = UiKit.Group(w.Root);
        w.Bg = UiKit.Fill(w.Root, "Bg", BattleTheme.Panel);
        w.Bg.raycastTarget = true;
        w.Button = w.Root.gameObject.AddComponent<Button>();
        w.Button.targetGraphic = w.Bg;
        var colors = w.Button.colors;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.disabledColor = Color.white;
        w.Button.colors = colors;
        int captured = index;
        w.Button.onClick.AddListener(() => Select(captured));
        // Emboss: outer line + brighter inner line.
        UiKit.Frame(w.Root, BattleTheme.Line, 1f);
        w.Frame = UiKit.Frame(w.Root, BattleTheme.WithAlpha(BattleTheme.Ink, 0.10f), 1f, 3f);

        var kind = data.Type switch
        {
            CardType.Move => ProceduralArt.IconKind.Footprint,
            CardType.Guard => ProceduralArt.IconKind.Shield,
            CardType.Heal => ProceduralArt.IconKind.Heal,
            _ => ProceduralArt.IconKind.Sword,
        };
        UiKit.Sprite(w.Root, "Icon", ProceduralArt.Icon(kind), BattleTheme.Ink2,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, 24f), new Vector2(14f, -14f));

        var name = UiKit.Label(w.Root, "Name", 24, TextAnchor.MiddleLeft, BattleTheme.Ink,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(CardWidth - 50f, 34f), new Vector2(44f, -10f), data.Name);
        name.fontStyle = FontStyle.Bold;
        string kindText = data.EffectiveRangeLabel != null ? $"{data.TypeLabel}・{data.EffectiveRangeLabel}" : data.TypeLabel;
        if (data.MinInvest > 0) kindText += $"　最低投入 {data.MinInvest}";
        UiKit.Label(w.Root, "Kind", 15, TextAnchor.MiddleLeft, BattleTheme.Ink2,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(CardWidth - 24f, 22f), new Vector2(14f, -50f), kindText);
        var rest = UiKit.Label(w.Root, "Rest", 19, TextAnchor.UpperLeft, BattleTheme.Ink,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(CardWidth - 28f, 80f), new Vector2(14f, -78f), data.RestText);
        rest.fontStyle = FontStyle.Bold;
        UiKit.Label(w.Root, "Desc", 14, TextAnchor.LowerLeft, BattleTheme.Ink2,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(CardWidth - 28f, 60f), new Vector2(14f, 10f), data.Description);
        if (!data.Playable && data.DisabledReason.Length > 0)
        {
            UiKit.Label(w.Root, "Disabled", 14, TextAnchor.LowerRight, BattleTheme.Omen,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(CardWidth - 28f, 22f), new Vector2(-14f, 8f), data.DisabledReason);
        }

        // Chips row: above the card, hidden until the card is selected.
        int chipCount = data.Tiers.Count;
        float rowW = chipCount * ChipWidth + Mathf.Max(0, chipCount - 1) * 8f;
        // Sits above the stamina band (between the HP row and the pips) so it never covers the pips.
        w.ChipRow = UiKit.Point(w.Root, "Chips", new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(rowW, ChipHeight), new Vector2(0f, 46f));
        w.ChipGroup = UiKit.Group(w.ChipRow);
        w.ChipGroup.alpha = 0f;
        w.ChipGroup.blocksRaycasts = false;
        w.ChipGroup.interactable = false;
        for (int i = 0; i < chipCount; i++)
        {
            var tier = data.Tiers[i];
            float cx = -rowW / 2f + ChipWidth / 2f + i * (ChipWidth + 8f);
            w.Chips.Add(MakeChip(w, tier, new Vector2(cx, 0f)));
        }
        return w;
    }

    private Button MakeChip(CardWidget card, TierView tier, Vector2 position)
    {
        var rt = UiKit.Point(card.ChipRow, $"Chip{tier.Invest}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(ChipWidth, ChipHeight), position);
        var bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = ProceduralArt.White;
        bg.color = BattleTheme.WithAlpha(BattleTheme.Ground, 0.96f);
        bg.raycastTarget = true;
        var frame = UiKit.Frame(rt, tier.IsDefault ? BattleTheme.Accent : BattleTheme.Line, tier.IsDefault ? 2f : 1f);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
        button.colors = colors;
        button.interactable = tier.Affordable;

        var head = UiKit.Label(rt, "Head", 16, TextAnchor.UpperLeft, tier.IsDefault ? BattleTheme.Accent : BattleTheme.Ink2,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ChipWidth - 12f, 20f), new Vector2(8f, -4f), $"投入 {tier.Invest}");
        head.fontStyle = FontStyle.Bold;
        UiKit.Label(rt, "Summary", 14, TextAnchor.UpperLeft, BattleTheme.Ink,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ChipWidth - 12f, 40f), new Vector2(8f, -24f), tier.Summary);
        string note = !tier.Affordable ? "スタミナ不足" : tier.IsDefault ? "構え維持" : tier.KeepsReserve ? "" : "構えなし";
        UiKit.Label(rt, "Note", 12, TextAnchor.LowerRight, !tier.Affordable ? BattleTheme.Omen : BattleTheme.Accent,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(ChipWidth - 12f, 16f), new Vector2(-6f, 3f), note);
        if (!tier.Affordable)
        {
            var g = UiKit.Group(rt);
            g.alpha = 0.4f;
        }

        string id = card.Data.InstanceId;
        int invest = tier.Invest;
        button.onClick.AddListener(() =>
        {
            if (!_interactable) return;
            OnTierHover?.Invoke(null);
            OnPlay?.Invoke(id, invest);
        });
        UiKit.OnPointer(rt.gameObject, EventTriggerType.PointerEnter, _ => { if (_interactable) OnTierHover?.Invoke(tier); });
        UiKit.OnPointer(rt.gameObject, EventTriggerType.PointerExit, _ => OnTierHover?.Invoke(null));
        return button;
    }

    // ---- selection --------------------------------------------------------------------------

    public void Select(int index)
    {
        if (!_interactable) return;
        if (index < 0 || index >= _cards.Count) return;
        if (!_cards[index].Data.Playable) return;
        if (_selected == index)
        {
            Deselect();
            return;
        }
        Deselect();
        _selected = index;
        var w = _cards[index];
        UiKit.SetFrameColor(w.Frame, BattleTheme.Accent);
        _host.StartCoroutine(UiTween.Move(w.Root, w.RestPosition, w.RestPosition + new Vector2(0f, 12f), 120f, Ease.Out));
        w.ChipGroup.blocksRaycasts = true;
        w.ChipGroup.interactable = true;
        _host.StartCoroutine(UiTween.Fade(w.ChipGroup, 0f, 1f, 150f, Ease.Out));
        OnSelectionChanged?.Invoke(w.Data);
    }

    public void Deselect()
    {
        if (_selected < 0) return;
        var w = _cards[_selected];
        _selected = -1;
        if (w.Root != null)
        {
            UiKit.SetFrameColor(w.Frame, BattleTheme.WithAlpha(BattleTheme.Ink, 0.10f));
            w.Root.anchoredPosition = w.RestPosition;
            w.ChipGroup.alpha = 0f;
            w.ChipGroup.blocksRaycasts = false;
            w.ChipGroup.interactable = false;
        }
        OnTierHover?.Invoke(null);
        OnSelectionChanged?.Invoke(null);
    }

    /// <summary>Keyboard 0–3: play the selected card at that invest, if such a chip exists and is affordable.</summary>
    public bool PlaySelected(int invest)
    {
        var data = Selected;
        if (data == null || !_interactable) return false;
        foreach (var t in data.Tiers)
        {
            if (t.Invest == invest && t.Affordable)
            {
                OnTierHover?.Invoke(null);
                OnPlay?.Invoke(data.InstanceId, invest);
                return true;
            }
        }
        return false;
    }

    public void SetInteractable(bool on)
    {
        _interactable = on;
        if (!on) Deselect();
        ApplyInteractable();
    }

    private void ApplyInteractable()
    {
        foreach (var w in _cards)
        {
            bool playable = _interactable && w.Data.Playable;
            w.Button.interactable = playable;
            w.Group.alpha = w.Data.Playable ? (_interactable ? 1f : 0.6f) : 0.4f;
        }
    }

    // ---- beats ------------------------------------------------------------------------------

    /// <summary>Cards slide up from below, 220 ms each, 90 ms apart.</summary>
    public IEnumerator SlideIn()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            var w = _cards[i];
            w.Root.anchoredPosition = w.RestPosition + new Vector2(0f, -CardHeight);
            w.Group.alpha = 0f;
        }
        for (int i = 0; i < _cards.Count; i++)
        {
            var w = _cards[i];
            _host.StartCoroutine(UiTween.Run(220f, Ease.Out, t =>
            {
                w.Root.anchoredPosition = w.RestPosition + new Vector2(0f, -CardHeight * (1f - t));
                w.Group.alpha = t;
            }));
            if (i < _cards.Count - 1) yield return UiTween.Wait(90f);
        }
        yield return UiTween.Wait(220f);
        ApplyInteractable();
    }

    /// <summary>The played card flies to the centre and vanishes (220 ms ease-in).</summary>
    public IEnumerator FlyOut(string instanceId)
    {
        CardWidget w = null;
        foreach (var c in _cards) if (c.Data.InstanceId == instanceId) w = c;
        if (w == null) yield break;
        w.ChipGroup.alpha = 0f;
        w.ChipGroup.blocksRaycasts = false;
        Vector2 from = w.Root.anchoredPosition;
        Vector2 to = new Vector2(0f, CardHeight * 1.6f);
        yield return UiTween.Run(220f, Ease.In, t =>
        {
            w.Root.anchoredPosition = Vector2.Lerp(from, to, t);
            w.Root.localScale = Vector3.one * (1f - 0.35f * t);
            w.Group.alpha = 1f - t;
        });
        w.Root.gameObject.SetActive(false);
    }

    /// <summary>Remaining cards drop off the bottom (200 ms ease-in).</summary>
    public IEnumerator DropAll()
    {
        foreach (var w in _cards)
        {
            if (!w.Root.gameObject.activeSelf) continue;
            var c = w;
            Vector2 from = c.Root.anchoredPosition;
            _host.StartCoroutine(UiTween.Run(200f, Ease.In, t =>
            {
                c.Root.anchoredPosition = from + new Vector2(0f, -CardHeight * 1.2f * t);
                c.Group.alpha = 1f - t;
            }));
        }
        yield return UiTween.Wait(200f);
    }
}
#endif
