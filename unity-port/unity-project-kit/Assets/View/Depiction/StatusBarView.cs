// HP bar, Guard badge, stamina pips and status chips for one unit. Shows the values it
// is handed; the bar width is the only thing derived here (a drawing scale, not a rule).
// The chips and their hover panel follow battle-visual-v1 §7.3 (#242); their words are the
// script's (StatusChip.Title / Group / Effect / Decay).
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public class StatusBarView : MonoBehaviour
    {
        public UnitSide side;
        public RectTransform hpFill;
        public Image hpFillImage;
        [Tooltip("The grey band that follows the HP bar down, late (battle_ui_ux_v2 §5.5 順 7). Built behind the bar when left empty.")]
        public RectTransform hpTrail;
        public Text hpText;
        public RectTransform guardBadge;
        public Image guardIcon;
        public Text guardText;
        public RectTransform pipRow;
        public Image[] pips = new Image[0];
        [Tooltip("The chip Texts of the prefab from before #242. They are hidden; the chips are made at run time in their row.")]
        public Text[] chips = new Text[0];

        private int _hpMax = 1;
        /// <summary>The chips as shown, in order: the stances (lasting) first, then the status words.</summary>
        private readonly List<StatusChip> _statuses = new List<StatusChip>();

        // ---- battle-visual-v1 §7.3 / §4.5 (#242) ----------------------------------------------------
        // The prefab's chips are plain Texts; the chips are made here instead (the prefab belongs to
        // the Unity project): 30 px tall, 6 / 8 px padding, the label and the stacks at 16 px, black 35%,
        // a square corner for the words put on the opponent and a round one for those put on oneself.
        private const float ChipHeight = 30f;
        private const float ChipGap = 5f;
        private const float RowWidth = 400f;
        private const int PlayerChipsMax = 6;
        private const int EnemyChipsMax = 4;
        private const float PanelWidth = 340f;
        private const float PanelAbove = 38f;

        private sealed class ChipView
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Back;
            public Image Frame;
            public Image Inner;
            public Text Label;
            public StatusChip Chip;
            /// <summary>The "+n" chip: the chips it gathers, for its panel.</summary>
            public List<StatusChip> Rest;
        }

        private readonly List<ChipView> _chipViews = new List<ChipView>();
        private RectTransform _chipRow;
        private RectTransform _panel;
        private Text _panelTitle;
        private Text _panelGroup;
        private Text _panelBody;

        /// <summary>The stamina the pips show now (the start of a recovery that lights them one by one).</summary>
        public int Stamina { get; private set; }

        /// <summary>
        /// battle-visual-v1 §4.7 (#242): the player's stamina lives on the tag above the piles, not in
        /// this panel. Set, the pips stay hidden and every stamina shown is handed on through
        /// <see cref="StaminaShown"/> (stamina, maximum), the one-by-one recovery included.
        /// </summary>
        public bool PipsElsewhere { get; set; }

        public System.Action<int, int> StaminaShown;

        // §4.5: a black 55% mark across the HP bar every 10 HP.
        private const int HpPerTick = 10;
        private readonly List<Image> _ticks = new List<Image>();
        private int _ticksFor = -1;

        private RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            if (guardIcon && guardIcon.sprite == null) guardIcon.sprite = ProceduralArt.Shield;
            foreach (Image pip in pips)
            {
                if (pip && pip.sprite == null) pip.sprite = ProceduralArt.Circle;
            }
            // §4.5: the HP bar is scarlet on both sides; whose it is, its place under the figure tells.
            if (hpFillImage) hpFillImage.color = BattleTheme.Scarlet;
            EnsureTrail();
            foreach (Text chip in chips)
            {
                if (chip) chip.gameObject.SetActive(false);
            }
            _chipRow = chips.Length > 0 && chips[0] ? chips[0].rectTransform.parent as RectTransform : null;
            if (!_chipRow) _chipRow = UiKit.Point(Rect, "Statuses", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(RowWidth, 0f), new Vector2(76f, 0f));
        }

        /// <summary>The prefab belongs to the Unity project, so a missing band is made here, just behind the bar.</summary>
        private void EnsureTrail()
        {
            if (hpTrail || !hpFill || !hpFill.parent) return;
            var go = new GameObject("HpTrail", typeof(RectTransform), typeof(Image));
            hpTrail = (RectTransform)go.transform;
            hpTrail.SetParent(hpFill.parent, false);
            hpTrail.SetSiblingIndex(hpFill.GetSiblingIndex());
            hpTrail.anchorMin = hpFill.anchorMin;
            hpTrail.anchorMax = hpFill.anchorMax;
            hpTrail.pivot = hpFill.pivot;
            hpTrail.offsetMin = hpFill.offsetMin;
            hpTrail.offsetMax = hpFill.offsetMax;
            var image = go.GetComponent<Image>();
            image.sprite = hpFillImage ? hpFillImage.sprite : null;
            image.type = hpFillImage ? hpFillImage.type : Image.Type.Simple;
            image.color = BattleTheme.WithAlpha(BattleTheme.Whiff, 0.85f);
            image.raycastTarget = false;
        }

        public void Bind(UnitFrame unit)
        {
            _hpMax = Mathf.Max(1, unit.HpMax);
            RenderTicks();
            SetHp(unit.Hp);
            SetGuard(unit.Guard);
            if (pipRow) pipRow.gameObject.SetActive(unit.ShowStamina && !PipsElsewhere);
            if (unit.ShowStamina) SetStamina(unit.Stamina, unit.StaminaMax);
            _statuses.Clear();
            foreach (StatusChip chip in unit.Statuses) _statuses.Add(Copy(chip));
            RenderChips();
        }

        private static StatusChip Copy(StatusChip chip)
        {
            // A source from before #242 marks a stance as a chip with no stacks and no family.
            bool lasting = chip.Kind == ChipKind.Lasting
                || (chip.Stacks <= 0 && chip.Label != null && chip.Label.StartsWith("構え・", System.StringComparison.Ordinal));
            return new StatusChip
            {
                Label = chip.Label,
                Stacks = chip.Stacks,
                Kind = lasting ? ChipKind.Lasting : chip.Kind,
                Title = chip.Title,
                Group = chip.Group,
                Effect = chip.Effect,
                Decay = chip.Decay,
            };
        }

        /// <summary>
        /// §4.5 / §7.3: the chips in a row that wraps, 5 px apart. The player shows six and an enemy four;
        /// the rest gather in one "+n" chip whose panel lists them.
        /// </summary>
        private void RenderChips()
        {
            int most = side == UnitSide.Player ? PlayerChipsMax : EnemyChipsMax;
            bool gather = _statuses.Count > most;
            int shown = gather ? most - 1 : _statuses.Count;
            int needed = gather ? most : _statuses.Count;
            while (_chipViews.Count < needed) _chipViews.Add(MakeChip(_chipViews.Count));
            float x = 0f;
            float y = -4f;
            for (int i = 0; i < _chipViews.Count; i++)
            {
                ChipView view = _chipViews[i];
                bool used = i < needed;
                view.Root.gameObject.SetActive(used);
                if (!used) continue;
                if (i < shown) BindChip(view, _statuses[i], null);
                else BindChip(view, new StatusChip { Label = "+" + (_statuses.Count - shown), Kind = ChipKind.OnSelf }, _statuses.GetRange(shown, _statuses.Count - shown));
                float width = view.Root.sizeDelta.x;
                if (x > 0f && x + width > RowWidth)
                {
                    x = 0f;
                    y -= ChipHeight + ChipGap;
                }
                view.Root.anchoredPosition = new Vector2(x, y);
                view.Root.localScale = Vector3.one;
                view.Group.alpha = 1f;
                x += width + ChipGap;
            }
            HidePanel();
        }

        private ChipView MakeChip(int index)
        {
            var view = new ChipView();
            view.Root = UiKit.Point(_chipRow, "Chip" + index, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(60f, ChipHeight), Vector2.zero);
            view.Group = UiKit.Group(view.Root);
            view.Back = view.Root.gameObject.AddComponent<Image>();
            view.Back.type = Image.Type.Sliced;
            view.Back.raycastTarget = true; // the hover target
            view.Frame = UiKit.Image(view.Root, "Frame", null, BattleTheme.Ink2, Vector2.zero, Vector2.one);
            view.Frame.type = Image.Type.Sliced;
            view.Inner = UiKit.Image(view.Root, "Inner", null, BattleTheme.Boss, Vector2.zero, Vector2.one);
            view.Inner.type = Image.Type.Sliced;
            view.Inner.rectTransform.offsetMin = new Vector2(3f, 3f);
            view.Inner.rectTransform.offsetMax = new Vector2(-3f, -3f);
            view.Label = UiKit.Label(view.Root, "Label", 16, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(60f, ChipHeight), new Vector2(6f, 0f));
            view.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.OnPointer(view.Root.gameObject, UnityEngine.EventSystems.EventTriggerType.PointerEnter, _ => Hover(view, true));
            UiKit.OnPointer(view.Root.gameObject, UnityEngine.EventSystems.EventTriggerType.PointerExit, _ => Hover(view, false));
            return view;
        }

        private static void BindChip(ChipView view, StatusChip chip, List<StatusChip> rest)
        {
            view.Chip = chip;
            view.Rest = rest;
            view.Label.text = ChipText(chip);
            // §7.3: the corner tells the family — 3 px for a word put on the opponent, 15 px for one put on oneself.
            int radius = chip.Kind == ChipKind.OnFoe || chip.Kind == ChipKind.Boss ? 3 : 15;
            view.Back.sprite = VisualArt.Rounded(radius);
            view.Back.color = BattleTheme.WithAlpha(Color.black, 0.35f);
            view.Frame.sprite = VisualArt.RoundedRing(radius, 1);
            view.Frame.color = ChipEdge(chip.Kind);
            // ボス専用の状態: the boss colour's double frame.
            view.Inner.sprite = VisualArt.RoundedRing(radius, 1);
            view.Inner.enabled = chip.Kind == ChipKind.Boss;
            float width = 6f + view.Label.preferredWidth + 8f;
            view.Root.sizeDelta = new Vector2(Mathf.Max(ChipHeight, width), ChipHeight);
            view.Label.rectTransform.sizeDelta = new Vector2(width, ChipHeight);
        }

        /// <summary>The chip's own words: "出血 2", a stance "岩の構え ∞ ×2" (no "×1"), or "+n".</summary>
        private static string ChipText(StatusChip chip)
        {
            string label = chip.Label ?? "";
            if (chip.Kind == ChipKind.Lasting)
            {
                string name = label.StartsWith("構え・", System.StringComparison.Ordinal) ? label.Substring("構え・".Length) : label;
                return name + " ∞" + (chip.Stacks > 1 ? " ×" + chip.Stacks : "");
            }
            if (label.StartsWith("+", System.StringComparison.Ordinal)) return label;
            return label + " " + chip.Stacks;
        }

        private static Color ChipEdge(ChipKind kind)
        {
            switch (kind)
            {
                case ChipKind.OnFoe: return BattleTheme.Ink2;
                case ChipKind.Boss: return BattleTheme.Boss;
                default: return BattleTheme.Steel;
            }
        }

        // ---- the panel (§7.3 説明パネル) ---------------------------------------------------------------

        private void Hover(ChipView view, bool on)
        {
            if (view.Chip == null) return;
            // §7.3 ホバー中のチップ: the edge turns to the body colour and the chip takes a white 10% fill.
            view.Frame.color = on ? BattleTheme.Ink : ChipEdge(view.Chip.Kind);
            view.Back.color = on ? BattleTheme.WithAlpha(Color.white, 0.10f) : BattleTheme.WithAlpha(Color.black, 0.35f);
            if (on) ShowPanel(view);
            else HidePanel();
        }

        private void ShowPanel(ChipView view)
        {
            EnsurePanel();
            if (!_panel) return;
            StatusChip chip = view.Chip;
            string body;
            if (view.Rest != null)
            {
                // 「+n」: every gathered word as 「名前 数：効き方」.
                _panelTitle.text = chip.Label;
                _panelGroup.text = "";
                var lines = new List<string>();
                foreach (StatusChip other in view.Rest) lines.Add(ChipText(other) + "：" + other.Effect);
                body = string.Join("\n", lines);
            }
            else
            {
                _panelTitle.text = string.IsNullOrEmpty(chip.Title) ? ChipText(chip) : chip.Title;
                _panelGroup.text = chip.Group ?? "";
                body = string.IsNullOrEmpty(chip.Effect) && string.IsNullOrEmpty(chip.Decay)
                    ? ""
                    : "効き方　" + chip.Effect + "\n減り方　" + chip.Decay;
            }
            _panelBody.text = body;
            float bodyHeight = _panelBody.preferredHeight;
            _panel.sizeDelta = new Vector2(PanelWidth, 12f + 26f + 8f + bodyHeight + 12f);
            _panelBody.rectTransform.sizeDelta = new Vector2(PanelWidth - 32f, bodyHeight + 4f);
            // Above the chip: its bottom 38 px over the chip's bottom, kept inside the screen.
            var corners = new Vector3[4];
            view.Root.GetWorldCorners(corners);
            var parent = (RectTransform)_panel.parent;
            Vector3 bottomLeft = parent.InverseTransformPoint(corners[0]);
            Rect area = parent.rect;
            float x = Mathf.Clamp(bottomLeft.x, area.xMin + 16f, area.xMax - 16f - PanelWidth);
            _panel.anchoredPosition = new Vector2(x, bottomLeft.y + PanelAbove);
            _panel.SetAsLastSibling();
            _panel.gameObject.SetActive(true);
        }

        private void HidePanel()
        {
            if (_panel) _panel.gameObject.SetActive(false);
        }

        private void EnsurePanel()
        {
            if (_panel) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (!canvas) return;
            UiKit.EnsureFont();
            var root = (RectTransform)canvas.rootCanvas.transform;
            _panel = UiKit.Point(root, "StatusPanel" + side, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(PanelWidth, 100f), Vector2.zero);
            CanvasGroup group = UiKit.Group(_panel);
            group.blocksRaycasts = false;
            VisualArt.Panel(_panel, "Back", BattleTheme.PanelOpaque, 6);
            VisualArt.Ring(_panel, "Edge", BattleTheme.Steel, 6, 1);
            _panelTitle = UiKit.Label(_panel, "Title", 18, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(PanelWidth - 32f - 110f, 26f), new Vector2(16f, -12f));
            _panelTitle.fontStyle = FontStyle.Bold;
            _panelGroup = UiKit.Label(_panel, "Group", 14, TextAnchor.MiddleRight, BattleTheme.Ink2, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(110f, 26f), new Vector2(-16f, -12f));
            _panelBody = UiKit.Label(_panel, "Body", 16, TextAnchor.UpperLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(PanelWidth - 32f, 60f), new Vector2(16f, -46f));
            _panel.gameObject.SetActive(false);
        }

        /// <summary>One word's chip, settled at once: added, restacked, or gone at 0. The panel text stays with the word.</summary>
        public void SetStatus(string label, int stacks)
        {
            int i = _statuses.FindIndex(s => s.Label == label);
            if (stacks <= 0)
            {
                if (i >= 0) _statuses.RemoveAt(i);
            }
            else if (i >= 0)
            {
                _statuses[i].Stacks = stacks;
                if (_statuses[i].Kind != ChipKind.Lasting) _statuses[i].Title = label + " " + stacks;
            }
            else
            {
                // The cue names the word only; the frame that settles the event brings its panel text.
                _statuses.Add(new StatusChip { Label = label, Stacks = stacks, Title = label + " " + stacks });
            }
            RenderChips();
        }

        /// <summary>
        /// The same change played as one of the four chip beats (battle_ui_ux_v2 §5.10): a new chip
        /// swells in, more stacks pop it, fewer shrink it for a moment, and the last stack fades it out.
        /// </summary>
        public IEnumerator PlayStatus(string label, int stacks, EffectId beat, float ms)
        {
            if (beat == EffectId.StatusVanish)
            {
                ChipView going = ChipOf(label);
                if (going != null)
                {
                    RectTransform rt = going.Root;
                    CanvasGroup fade = going.Group;
                    yield return UiTween.Run(ms, Ease.Linear, t =>
                    {
                        if (!rt) return;
                        if (fade) fade.alpha = 1f - t;
                        float s = Mathf.Lerp(1f, 0.7f, t);
                        rt.localScale = new Vector3(s, s, 1f);
                    });
                }
                SetStatus(label, 0);
                yield break;
            }

            SetStatus(label, stacks);
            ChipView shown = ChipOf(label);
            if (shown == null) yield break;
            RectTransform chip = shown.Root;
            switch (beat)
            {
                case EffectId.StatusApply:
                    yield return UiTween.Run(ms, Ease.Out, t =>
                    {
                        float s = t < 0.6f ? Mathf.Lerp(0.3f, 1.2f, t / 0.6f) : Mathf.Lerp(1.2f, 1f, (t - 0.6f) / 0.4f);
                        if (chip) chip.localScale = new Vector3(s, s, 1f);
                    });
                    break;
                case EffectId.StatusStack:
                    yield return UiTween.Pop(chip, ms);
                    break;
                default: // StatusTick
                    yield return UiTween.Run(ms, Ease.Out, t =>
                    {
                        float s = 1f - 0.18f * Mathf.Sin(t * Mathf.PI);
                        if (chip) chip.localScale = new Vector3(s, s, 1f);
                    });
                    break;
            }
            if (chip) chip.localScale = Vector3.one;
        }

        /// <summary>The chip drawn for a word, or null when it is gathered into "+n" or gone.</summary>
        private ChipView ChipOf(string label)
        {
            foreach (ChipView view in _chipViews)
            {
                if (view.Root.gameObject.activeSelf && view.Rest == null && view.Chip != null && view.Chip.Label == label) return view;
            }
            return null;
        }

        public void SetHp(int hp)
        {
            SetHpKeepingTrail(hp);
            SnapTrail();
        }

        /// <summary>The bar and the number move at once; the grey band stays where it was until it is told to follow.</summary>
        public void SetHpKeepingTrail(int hp)
        {
            if (hpText) hpText.text = hp.ToString();
            if (hpFill) hpFill.anchorMax = new Vector2(Width(hp), hpFill.anchorMax.y);
            if (hpTrail && hpFill && hpTrail.anchorMax.x < hpFill.anchorMax.x) SnapTrail(); // a heal leaves no band behind
        }

        /// <summary>Slides the bar from its current width to <paramref name="hp"/>; the number lands at once.</summary>
        public IEnumerator AnimateHp(int hp, float ms)
        {
            if (hpText) hpText.text = hp.ToString();
            if (!hpFill) yield break;
            float from = hpFill.anchorMax.x;
            float to = Width(hp);
            if (hpTrail && hpTrail.anchorMax.x < to) hpTrail.anchorMax = new Vector2(to, hpTrail.anchorMax.y);
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (hpFill) hpFill.anchorMax = new Vector2(Mathf.Lerp(from, to, t), hpFill.anchorMax.y);
            });
        }

        /// <summary>The grey band catches up with the bar (EffectId.HpTrail).</summary>
        public IEnumerator AnimateTrail(float ms)
        {
            if (!hpTrail || !hpFill) yield break;
            float from = hpTrail.anchorMax.x;
            float to = hpFill.anchorMax.x;
            yield return UiTween.Run(ms, Ease.InOut, t =>
            {
                if (hpTrail) hpTrail.anchorMax = new Vector2(Mathf.Lerp(from, to, t), hpTrail.anchorMax.y);
            });
        }

        public void SnapTrail()
        {
            if (hpTrail && hpFill) hpTrail.anchorMax = new Vector2(hpFill.anchorMax.x, hpTrail.anchorMax.y);
        }

        private float Width(int hp) => Mathf.Clamp01(hp / (float)_hpMax);

        public void SetGuard(int guard)
        {
            if (guardText) guardText.text = guard.ToString();
            // §4.5: with no Guard the shield turns steel and the number the note colour, at 70%.
            Color c = guard > 0 ? BattleTheme.Guard : BattleTheme.WithAlpha(BattleTheme.Steel, 0.7f);
            if (guardIcon) guardIcon.color = c;
            if (guardText) guardText.color = guard > 0 ? BattleTheme.Ink : BattleTheme.WithAlpha(BattleTheme.Ink2, 0.7f);
        }

        public IEnumerator PopGuard(int guard, float ms = 260f)
        {
            SetGuard(guard);
            if (guardBadge) yield return UiTween.Pop(guardBadge, ms);
        }

        /// <summary>A blow took the Guard to 0: the badge flashes and shakes as it breaks (EffectId.GuardBreak).</summary>
        public IEnumerator CrackGuard(float ms)
        {
            if (!guardBadge) yield break;
            if (guardIcon) StartCoroutine(UiTween.Tint(guardIcon, BattleTheme.White, guardIcon.color, ms, Ease.Out));
            yield return UiTween.Shake(guardBadge, 7f, 2, ms);
        }

        /// <summary>
        /// The recovered pips light one at a time, <paramref name="perPipMs"/> apart
        /// (EffectId.StaminaRecover, battle_ui_ux_v2 §5.1 順 3).
        /// </summary>
        public IEnumerator LightPips(int stamina, int staminaMax, float perPipMs)
        {
            int from = Mathf.Clamp(Stamina, 0, staminaMax);
            if (stamina <= from)
            {
                SetStamina(stamina, staminaMax);
                yield break;
            }
            SetStamina(from, staminaMax);
            for (int next = from + 1; next <= stamina; next++)
            {
                yield return UiTween.Wait(perPipMs);
                SetStamina(next, staminaMax);
                int pip = next - 1;
                if (pip < pips.Length && pips[pip]) StartCoroutine(UiTween.Pop(pips[pip].rectTransform, perPipMs));
            }
        }

        public void SetStamina(int stamina, int staminaMax)
        {
            Stamina = stamina;
            for (int i = 0; i < pips.Length; i++)
            {
                if (!pips[i]) continue;
                pips[i].gameObject.SetActive(i < staminaMax);
                pips[i].color = i < stamina ? BattleTheme.Warm : BattleTheme.WithAlpha(BattleTheme.Ink2, 0.25f);
            }
            StaminaShown?.Invoke(stamina, staminaMax);
        }

        /// <summary>§4.5: the HP bar's marks, one every 10 HP of the maximum, over the fill.</summary>
        private void RenderTicks()
        {
            if (!hpFill || !hpFill.parent || _ticksFor == _hpMax) return;
            _ticksFor = _hpMax;
            var bar = (RectTransform)hpFill.parent;
            int count = (_hpMax - 1) / HpPerTick;
            while (_ticks.Count < count)
            {
                Image tick = UiKit.Image(bar, "Tick" + _ticks.Count, ProceduralArt.White, BattleTheme.WithAlpha(Color.black, 0.55f), Vector2.zero, Vector2.one);
                _ticks.Add(tick);
            }
            for (int i = 0; i < _ticks.Count; i++)
            {
                Image tick = _ticks[i];
                bool used = i < count;
                tick.gameObject.SetActive(used);
                if (!used) continue;
                float at = (i + 1) * HpPerTick / (float)_hpMax;
                RectTransform rt = tick.rectTransform;
                rt.anchorMin = new Vector2(at, 0f);
                rt.anchorMax = new Vector2(at, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(-1f, 0f);
                rt.offsetMax = new Vector2(1f, 0f);
                // Over the fill and the trail, under the number written on the bar.
                rt.SetSiblingIndex(Mathf.Min(hpFill.GetSiblingIndex() + 1, bar.childCount - 1));
            }
            if (hpText && hpText.transform.parent == bar) hpText.transform.SetAsLastSibling();
        }
    }
}
#endif
