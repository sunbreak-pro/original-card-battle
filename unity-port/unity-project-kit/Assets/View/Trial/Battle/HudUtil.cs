// Shared helpers of the battle scene's hud module (docs/BATTLE_SCENE.md §6c): token colours, Unity's UiTween shapes
// (Pop, Shake) on the scene clock, the icon look-ups the hud's views share, and the one tooltip the hud shows at a time.
// Presentation only: no rule, no number of the game. Port of scenes/battle/hud/util.js.
//
// The kit's Tweens (Pop / Shake / ScaleTo) run on a TrialClock; the battle's clock is the BattleClock (scaled, paused by
// the overlays, stopped for good on destroy), so these are the util.js ones again on env.Clock.
using System;
using System.Collections.Generic;
using System.Globalization;
using Depiction;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    /// <summary>An icon and its colour (OMEN_ICON's rows): the CSS colour as the JS writes it, and resolved.</summary>
    public readonly struct HudIconLook
    {
        public readonly string Icon;
        /// <summary>The CSS colour ('var(--phosphor)').</summary>
        public readonly string Color;

        public HudIconLook(string icon, string color)
        {
            Icon = icon;
            Color = color;
        }

        /// <summary>The colour as an Rgba (dusk tokens).</summary>
        public Rgba Rgba => CssColor.Parse(Color);
    }

    /// <summary>KIND_LOOK's rows: a card attribute's icon, ink and frame colour (CSS as the JS writes them, and resolved).</summary>
    public readonly struct HudKindLook
    {
        public readonly string Icon;
        public readonly string Ink;
        public readonly string Frame;

        public HudKindLook(string icon, string ink, string frame)
        {
            Icon = icon;
            Ink = ink;
            Frame = frame;
        }

        public Rgba InkRgba => CssColor.Parse(Ink);
        public Rgba FrameRgba => CssColor.Parse(Frame);
    }

    /// <summary>statusIconOf's answer: { id, self } — the DS STATUS row of a status word.</summary>
    public readonly struct HudStatusIcon
    {
        /// <summary>The 付録 A icon id ("bleed").</summary>
        public readonly string Id;
        /// <summary>A status put on oneself (the chip is round, OnSelf).</summary>
        public readonly bool Self;

        public HudStatusIcon(string id, bool self)
        {
            Id = id;
            Self = self;
        }
    }

    // js: scenes/battle/hud/util.js
    public static class HudUtil
    {
        // js: scenes/battle/hud/util.js:9
        /// <summary>tok(name): `var(--name)`.</summary>
        public static string Tok(string name) => "var(--" + name + ")";

        // js: scenes/battle/hud/util.js:11
        /// <summary>mix(name, pct): a token colour at `pct` % (the battle-visual §2.1 alphas: amber 16 %, black 55 % …).</summary>
        public static string Mix(string name, float pct) =>
            "color-mix(in srgb, var(--" + name + ") " + pct.ToString("R", CultureInfo.InvariantCulture) + "%, transparent)";

        // js: scenes/battle/hud/util.js:12
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // js: scenes/battle/hud/util.js:13
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;

        // js: scenes/battle/hud/util.js:16
        /// <summary>UiTween.Pop's curve: scale 1.2 → 0.8 → 1 over the (eased) t.</summary>
        public static float PopScale(float t) => t < 0.5f ? Lerp(1.2f, 0.8f, t * 2f) : Lerp(0.8f, 1f, (t - 0.5f) * 2f);

        // js: scenes/battle/hud/util.js:21
        /// <summary>
        /// UiTween.Pop(rt, ms) on the scene clock (Ease.Out). Uses the CSS `scale` property (PropScale), so left / top and the
        /// transform stay; cleared at the end. No element: the wait alone.
        /// </summary>
        public static Promise Pop(BattleClock clock, UiNode el, float ms)
        {
            if (!el) return clock.Wait(ms);
            return clock.Run(ms, Ease.Out, (t, _) => { if (el) el.PropScale = Fixed(PopScale(t), 4); })
                .Then(() => { if (el) el.PropScale = 1f; });
        }

        /// <summary>Pop over a RectTransform (its UiNode).</summary>
        public static Promise Pop(BattleClock clock, RectTransform el, float ms) => Pop(clock, el ? UiNode.Of(el) : null, ms);

        // js: scenes/battle/hud/util.js:27
        /// <summary>UiTween.Shake(rt, amplitude, cycles, ms): a horizontal shake that dies out (Ease.Linear), on the CSS `translate` property.</summary>
        public static Promise Shake(BattleClock clock, UiNode el, float amplitude, float cycles, float ms)
        {
            if (!el) return clock.Wait(ms);
            return clock.Run(ms, Ease.Linear, (t, _) =>
            {
                if (!el) return;
                float x = (float)(Math.Sin(t * Math.PI * 2.0 * cycles) * amplitude * (1.0 - t));
                el.PropTranslateX = Fixed(x, 2);
                el.PropTranslateY = 0f;
            }).Then(() =>
            {
                if (!el) return;
                el.PropTranslateX = 0f;
                el.PropTranslateY = 0f;
            });
        }

        /// <summary>Shake over a RectTransform (its UiNode).</summary>
        public static Promise Shake(BattleClock clock, RectTransform el, float amplitude, float cycles, float ms) =>
            Shake(clock, el ? UiNode.Of(el) : null, amplitude, cycles, ms);

        // js: scenes/battle/hud/util.js:36
        /// <summary>UiTween.Scale(rt, from, to, ms, ease) on the CSS `scale` property (ease null = out); the end value stays (1 clears).</summary>
        public static Promise ScaleTo(BattleClock clock, UiNode el, float from, float to, float ms, EaseFn ease = null)
        {
            if (!el) return clock.Wait(ms);
            return clock.Run(ms, ease ?? Ease.Out, (t, _) => { if (el) el.PropScale = Fixed(Lerp(from, to, t), 4); })
                .Then(() => { if (el) el.PropScale = to; });
        }

        /// <summary>ScaleTo over a RectTransform (its UiNode).</summary>
        public static Promise ScaleTo(BattleClock clock, RectTransform el, float from, float to, float ms, EaseFn ease = null) =>
            ScaleTo(clock, el ? UiNode.Of(el) : null, from, to, ms, ease);

        /// <summary>Number.prototype.toFixed(digits) read back as a number (the style string the JS writes).</summary>
        private static float Fixed(float x, int digits) => (float)Math.Round((double)x, digits, MidpointRounding.AwayFromZero);

        // ---- icon look-ups (the design system's 付録 A icons; colour by role) ----------------------------------

        // js: scenes/battle/hud/util.js:50
        /// <summary>
        /// OMEN_ICON — OmenIconArt.Of + OmenBadgeView.RoleColor: the omen kind's icon and its colour. Attack in phosphor and
        /// Guard in the Guard colour (as their numbers), 休み in steel (it is drawn as 空き, DS OMEN_KIND), the rest in body ink.
        /// None draws no icon (no row).
        /// </summary>
        public static readonly IReadOnlyDictionary<OmenIcon, HudIconLook> OmenIconLook = new Dictionary<OmenIcon, HudIconLook>
        {
            [Depiction.OmenIcon.Attack] = new HudIconLook("sword", Tok("phosphor")),
            [Depiction.OmenIcon.Guard] = new HudIconLook("shield", Tok("guard")),
            [Depiction.OmenIcon.Status] = new HudIconLook("star", Tok("ink")),
            [Depiction.OmenIcon.Move] = new HudIconLook("back", Tok("ink")),
            [Depiction.OmenIcon.Stance] = new HudIconLook("flag", Tok("ink")),
            [Depiction.OmenIcon.Rest] = new HudIconLook("rest", Tok("steel")),
            [Depiction.OmenIcon.Seal] = new HudIconLook("seal", Tok("ink")),
        };

        // js: scenes/battle/hud/util.js:61
        /// <summary>OmenBadgeView.RoleColor for the omen's number: attack phosphor, guard Guard, the rest body ink.</summary>
        public static string OmenValueColor(OmenIcon icon) =>
            icon == Depiction.OmenIcon.Attack ? Tok("phosphor") : icon == Depiction.OmenIcon.Guard ? Tok("guard") : Tok("ink");

        // js: scenes/battle/hud/util.js:66
        /// <summary>GLYPH_ICON — VisualArt.StatusIcon: the 付録 A icon of a status a 状態付与 omen puts on; None (a boss's word) has none.</summary>
        public static readonly IReadOnlyDictionary<StatusGlyph, string> GlyphIcon = new Dictionary<StatusGlyph, string>
        {
            [StatusGlyph.Bleed] = "bleed",
            [StatusGlyph.Fragile] = "brittle",
            [StatusGlyph.Slow] = "slow",
            [StatusGlyph.Intimidate] = "intimidate",
            [StatusGlyph.Fatigue] = "fatigue",
        };

        // js: scenes/battle/hud/util.js:75
        /// <summary>KIND_LOOK — VisualArt.KindIcon / BattleTheme.KindInk / KindFrame: a card attribute's icon, ink and frame colour.</summary>
        public static readonly IReadOnlyDictionary<CardKind, HudKindLook> KindLook = new Dictionary<CardKind, HudKindLook>
        {
            [CardKind.Attack] = new HudKindLook("sword", Tok("phosphor"), Tok("violet")),
            [CardKind.Guard] = new HudKindLook("shield", Tok("guard"), Tok("guard")),
            [CardKind.Skill] = new HudKindLook("star", Tok("amber"), Tok("amber")),
            [CardKind.Stance] = new HudKindLook("flag", Tok("hp-text"), Tok("crimson")),
        };

        // js: scenes/battle/hud/util.js:83
        /// <summary>PILE_ICON — PilesView.IconOf.</summary>
        public static readonly IReadOnlyDictionary<PileKind, string> PileIcon = new Dictionary<PileKind, string>
        {
            [PileKind.Draw] = "draw",
            [PileKind.Discard] = "discard",
            [PileKind.Exile] = "exile",
        };

        private static Dictionary<string, HudStatusIcon> _statusIds;

        // js: scenes/battle/hud/util.js:91
        /// <summary>
        /// statusIconOf(OCB, label): a status word's 付録 A icon id, looked up by its name in the design system's STATUS table
        /// ("出血" → bleed). Presentation only: the word itself is the script's. Null for a word the table does not draw (a
        /// boss's own statuses, a stance).
        /// </summary>
        public static HudStatusIcon? StatusIconOf(string label)
        {
            if (_statusIds == null)
            {
                _statusIds = new Dictionary<string, HudStatusIcon>(StringComparer.Ordinal);
                foreach (string id in Ocb.StatusIds)
                {
                    if (!Ocb.Status.TryGetValue(id, out OcbStatusInfo s)) continue;
                    // Object.keys order and Map.set: a later row of the same name wins, as in the JS.
                    _statusIds[s.Name] = new HudStatusIcon(id, s.Self);
                }
            }
            return label != null && _statusIds.TryGetValue(label, out HudStatusIcon hit) ? hit : (HudStatusIcon?)null;
        }

        // js: scenes/battle/hud/util.js:101
        /// <summary>
        /// shieldIcon(OCB, size, solid): the solid / dashed shield of battle-visual §4.5 (Guard &gt; 0 / Guard 0, 構えの盾) — solid:
        /// stroked guard, filled guard at 0.2; else stroked steel, dashed. At (0, 0) of parent.
        /// </summary>
        public static PathGraphic ShieldIcon(RectTransform parent, float size, bool solid) =>
            solid
                ? Ocb.Icon(parent, "shield", size, DsTokens.Guard, fill: DsTokens.Guard, fillOpacity: 0.2f)
                : Ocb.Icon(parent, "shield", size, DsTokens.Steel, dashed: true);

        // ---- the one tooltip ----------------------------------------------------------------------------

        // js: scenes/battle/hud/util.js:113
        /// <summary>createTip(env): the hud's one tooltip (see <see cref="HudTip"/>).</summary>
        public static HudTip CreateTip(BattleViewEnv env) => new HudTip(env);

        /// <summary>hud.css:55 .ocbg-battle-linetip (and its span, display: inline-block).</summary>
        public const string LineTipCss =
            "padding: 8px 12px; display: flex; align-items: center; justify-content: center; background: var(--panel-solid); "
            + "border: var(--stroke-rule) solid var(--steel); border-radius: var(--radius-panel); box-shadow: var(--shadow-tip); "
            + "font: 400 16px/22px var(--font-body); color: var(--ink); white-space: nowrap;";

        // js: scenes/battle/hud/util.js:167
        /// <summary>
        /// lineTip(text): a plain one-line tooltip (BattleSpeedButton.BuildTooltip: opaque panel, dull-steel frame, 16 px ink) —
        /// h('div', { class: 'ocbg-battle-linetip' }, h('span', null, text)), laid out to its text. `extraCss`: the
        /// declarations of a class the caller adds to it (piles.js's .ocbg-battle-pilecap, freestep.js's .ocbg-battle-fs-tip),
        /// appended to the rule as the cascade puts them. Built under parent (null: none yet; the tip's Show adopts it).
        /// </summary>
        public static RectTransform LineTip(string text, RectTransform parent = null, string extraCss = null)
        {
            string css = LineTipCss + (string.IsNullOrEmpty(extraCss) ? "" : " " + extraCss);
            RectTransform el = Ocb.El(parent, "ocbg-battle-linetip", css);
            Ocb.Text(el, text ?? "", Ocb.TextOf(css), "span");
            Ocb.Flow(el, css);
            return el;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _statusIds = null;
        }
    }

    /// <summary>
    /// The hud's tooltip (the status chips' 説明パネル, the stance frames, the corner buttons): one at a time, in layer.over,
    /// never taking the pointer (BattleSpeedButton: raycastTarget false). battle_ui_ux_v2 §3.13 (review 21): a right click
    /// pins the panel — it stays (a hover elsewhere shows nothing over it) until a click, Esc or another right click.
    /// </summary>
    // js: scenes/battle/hud/util.js:113 (createTip)
    public sealed class HudTip
    {
        private readonly BattleViewEnv _env;
        private RectTransform _el;
        private object _owner;
        private bool _pinned;

        internal HudTip(BattleViewEnv env)
        {
            _env = env;
        }

        // js: scenes/battle/hud/util.js:123
        /// <summary>
        /// show(node, place, by): shows `node`, then `place(node)` puts it (it is laid out in the layer first). Not while a card
        /// is held (the hand marks the root: the pointer is carrying the card, not reading), nor over a pinned detail — the
        /// node is dropped then (the JS leaves it to the garbage collector).
        /// </summary>
        public void Show(RectTransform node, Action<RectTransform> place = null, object by = null)
        {
            if (!node) return;
            // A refused node is dropped, unless it is the tip standing now (it stays, as the JS leaves `el` alone).
            if (_env != null && _env.Grabbing) { if (node != _el) Ui.Remove(node); return; }
            if (_pinned) { if (node != _el) Ui.Remove(node); return; }
            if (_el && _el != node) Ui.Remove(_el);
            _el = node;
            _owner = by;
            // .ocbg-battle-tip { position: absolute; pointer-events: none; box-sizing: border-box }
            UiNode n = UiNode.Of(node);
            n.PointerEvents = PointerEvents.None;
            RectTransform over = _env != null ? _env.Layer.Over : null;
            if (over && node.parent != over) node.SetParent(over, false);
            if (over) node.SetAsLastSibling();
            place?.Invoke(node);
        }

        // js: scenes/battle/hud/util.js:134
        /// <summary>hide(by): the tip goes, unless it is pinned or another owner's.</summary>
        public void Hide(object by = null)
        {
            if (!_el || _pinned) return;
            if (by != null && _owner != null && !ReferenceEquals(by, _owner)) return;
            Ui.Remove(_el);
            _el = null;
            _owner = null;
        }

        // js: scenes/battle/hud/util.js:142
        /// <summary>
        /// pin(node, place, by): the right click's detail — `node` stays until Unpin(); the same owner's right click again closes
        /// it (toggle, false). Whether it is pinned now.
        /// </summary>
        public bool Pin(RectTransform node, Action<RectTransform> place = null, object by = null)
        {
            if (_pinned && ReferenceEquals(_owner, by))
            {
                RectTransform standing = _el;
                Unpin();
                if (node && node != standing) Ui.Remove(node);
                return false;
            }
            _pinned = false;
            Show(node, place, by);
            if (!_el) return false;
            _pinned = true;
            // .is-pinned: the class only marks it (no look of its own in hud.css).
            return true;
        }

        // js: scenes/battle/hud/util.js:152
        /// <summary>unpin(): closes a pinned detail; returns whether one was open.</summary>
        public bool Unpin()
        {
            if (!_pinned) return false;
            _pinned = false;
            if (_el) Ui.Remove(_el);
            _el = null;
            _owner = null;
            return true;
        }

        // js: scenes/battle/hud/util.js:160
        /// <summary>pinned.</summary>
        public bool Pinned => _pinned;

        // js: scenes/battle/hud/util.js:161
        /// <summary>shown: a tip stands.</summary>
        public bool Shown => _el;

        /// <summary>The tip standing now (its node), or null.</summary>
        public RectTransform Node => _el ? _el : null;
    }
}
