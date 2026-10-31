// One enemy's omen badge and its plan (OmenBadgeView, docs/BATTLE_SCENE.md §6c, battle-views §3.2): the 200×98 DS omen
// plate (icon, value or statuses, kind word, hit mark or side glyph, swatch), the ▼ tail over its enemy's head, the plan
// 8 px above (dashed, 62 %), the acting light, and the beats (PopIn, Blink, Flare, Reveal, TraitWord, PromotePlan,
// FadeOut(All), StrikeSide). In layer.panels. Port of scenes/battle/hud/omen.js.
//
// The enemy's omen badge: a port of OmenBadgeView and OmenIconArt (battle-visual-v1 §4.4, #349 / #455), drawn with the
// design system's omen plate and plan badge looks. 200×98, an opaque panel with a 2 px violet frame: the kind's icon (34)
// and its number (48) on the first row — or, for a 状態付与 omen, the statuses it puts on at 28 px (a boss's word written
// by name in the boss colour's double frame) — with the enemy's hatch swatch at the right; the kind word (16) and the hit
// mark on the second. 休み draws a steel dashed frame. While the enemy carries the omen out it wears a phosphor edge and a
// violet light (§4.4 敵の番). An elite's or a boss's plan stacks above it, 170×66 at 62 %, dashed, with 予定 / 予定変更.
// What the journal has not read is already off the frame (OmenDisclosure): this draws what is there.
//
// The DOM's pieces in uGUI: the wrap (.ocbg-battle-omenwrap, its UiNode carries the place, the CSS scale and the z-index)
// holds the plan (.ocbg-battle-plan, with its ::before dashed frame as a child) and the badge (.ocb-omen.ocbg-battle-omen,
// its BoxGraphic is the plate); the tail (.ocbg-battle-omen-tail, a CSS border triangle) is a PathGraphic kept across the
// badge's redraws (the JS appends the same <i> again). `visibility: hidden` on the wrap is SetActive(false) (nothing in
// the badge takes the pointer); `display: none` (show(false)) the same. overflow: hidden is a RectMask2D.
using System;
using System.Collections.Generic;
using Depiction;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace Trial.Battle
{
    /// <summary>createOmenBadge(env) → the badge's api (omen.js:151). Implements hud.omenOf(unit)'s contract.</summary>
    // js: scenes/battle/hud/omen.js:44
    public sealed class HudOmenBadge : IOmenBadge
    {
        /// <summary>OMEN_BADGE / BADGE: the badge's box, 200×98.</summary>
        // js: scenes/battle/hud/omen.js:15
        public const float BadgeW = 200f;
        public const float BadgeH = 98f;

        /// <summary>PLAN_DROP_MS: an elite's plan drops into the lower slot over this (§3.10).</summary>
        // js: scenes/battle/hud/omen.js:26
        public const float PlanDropMs = 150f;

        // ---- OmenBadgeView's own numbers (omen.js:15-31) ------------------------------------------------------------

        /// <summary>PLAN: { w: 170, h: 66, gap: 8, alpha: 0.62 }.</summary>
        // js: scenes/battle/hud/omen.js:16
        private const float PlanW = 170f;
        private const float PlanH = 66f;
        private const float PlanGap = 8f;
        private const float PlanAlpha = 0.62f;
        // js: scenes/battle/hud/omen.js:17
        private const float IconPx = 34f;
        // js: scenes/battle/hud/omen.js:18
        private const float PlanIconPx = 24f;
        // js: scenes/battle/hud/omen.js:19
        private const float StatusIconPx = 28f;
        // js: scenes/battle/hud/omen.js:20
        private const float PlanStatusIconPx = 20f;
        // js: scenes/battle/hud/omen.js:21
        private const float SwatchPx = 22f;
        // js: scenes/battle/hud/omen.js:22
        private const float HitIconPx = 18f;
        /// <summary>TAIL: the ▼ that ties a badge to its enemy's head: 16 wide, 9 tall, in the 14 px between badge and head.</summary>
        // js: scenes/battle/hud/omen.js:24
        private const float TailW = 16f;
        private const float TailH = 9f;
        /// <summary>DROP_PX — §5.8 提示 「頭上へ降りる」: how far over its place an omen starts its drop (scene choice).</summary>
        // js: scenes/battle/hud/omen.js:28
        private const float DropPx = 24f;
        /// <summary>§5.9 順 12: an enemy's fired trait's word stands this long after swelling in, then fades over the next (scene choice).</summary>
        // js: scenes/battle/hud/omen.js:30
        private const float TraitWordHoldMs = 900f;
        // js: scenes/battle/hud/omen.js:31
        private const float TraitWordFadeMs = 200f;

        // ---- hud.css (verbatim) -------------------------------------------------------------------------------------

        /// <summary>hud.css:61 .ocbg-battle-omenwrap (z-index 2; .is-behind 1, hud.css:63; .is-trait 4, hud.css:191).</summary>
        private const string WrapCss = "position: absolute; left: 0; top: 0; width: 200px; height: 98px; transform-origin: 50% 50%; z-index: 2;";
        private const int WrapZ = 2;
        private const int WrapBehindZ = 1;
        private const int WrapTraitZ = 4;
        /// <summary>hud.css:64-65 .ocbg-battle-omen; battle.css:49 .ocbg-battle .ocb-omen { transition: none }.</summary>
        private const string BattleOmenCss = "box-sizing: border-box; color: var(--ink); position: relative; transition: none;";
        /// <summary>hud.css:66 .ocbg-battle-omen .ocb-omen-row1.</summary>
        private const string BattleRow1Css = "gap: 8px; padding-right: 26px; overflow: hidden;";
        /// <summary>hud.css:67 .ocbg-battle-omen-swatch.</summary>
        private const string SwatchPosCss = "position: absolute; right: 12px; top: 14px;";
        /// <summary>hud.css:69-71 .ocbg-battle-omen-tail (violet; .is-active phosphor; .is-rest steel).</summary>
        private const string TailCss = "position: absolute; width: 0; height: 0; border-left: 8px solid transparent; border-right: 8px solid transparent; border-top: 9px solid var(--violet); pointer-events: none;";
        /// <summary>The CSS border triangle as a path in its 16×9 box.</summary>
        private const string TailPath = "M0 0 L16 0 L8 9 Z";
        /// <summary>hud.css:72 .ocbg-battle-omen .ocb-omen-row1 b.</summary>
        private const string BattleRow1BCss = "font-variant-numeric: tabular-nums;";
        /// <summary>hud.css:73 .ocbg-battle-omen-noicon.</summary>
        private const string NoIconCss = "display: inline-block; width: 34px; height: 34px; flex: none;";
        /// <summary>hud.css:74 .ocbg-battle-omen-kind.</summary>
        private const string KindCss = "color: var(--ink); white-space: nowrap;";
        /// <summary>hud.css:75 .ocbg-battle-omen-statuses.</summary>
        private const string StatusesCss = "display: inline-flex; align-items: center; gap: 4px; min-width: 0; overflow: hidden;";
        /// <summary>hud.css:76 .ocbg-battle-omen .ocbg-battle-omen-statuses.</summary>
        private const string StatusesInBadgeCss = "max-width: 106px;";
        private const float StatusesInBadgeMax = 106f;
        /// <summary>hud.css:77 .ocbg-battle-bossword.</summary>
        private const string BossWordCss = "display: inline-flex; align-items: center; flex: none; box-sizing: border-box; padding: 0 2px; letter-spacing: -0.04em; border: 3px double var(--boss); border-radius: var(--radius-chip-foe); font: 700 16px/1 var(--font-body); color: var(--ink); white-space: nowrap;";
        /// <summary>hud.css:78 .ocbg-battle-omen-side.</summary>
        private const string SideCss = "display: inline-flex; align-items: center; justify-content: center; position: relative; box-sizing: border-box; width: 64px; height: 28px; border: var(--stroke-frame) solid var(--violet); border-radius: var(--radius-tag); font: 600 20px/1 var(--font-mono); color: var(--ink);";
        /// <summary>hud.css:79 .ocbg-battle-omen-side.is-struck.</summary>
        private const string SideStruckCss = "border-style: dashed; border-color: var(--miss); opacity: 0.8;";
        /// <summary>hud.css:80 .ocbg-battle-omen-side.is-struck::after.</summary>
        private const string SideStruckAfterCss = "content: \"\"; position: absolute; left: 4px; right: 4px; top: 50%; height: 2px; background: var(--miss); transform: rotate(-24deg);";
        /// <summary>hud.css:81 .ocbg-battle-plan.</summary>
        private const string PlanCss = "position: absolute; box-sizing: border-box; width: 170px; height: 66px; padding: 4px 10px 5px; display: flex; flex-direction: column; justify-content: space-between; background: var(--panel-solid); border-radius: var(--radius-panel); color: var(--ink);";
        /// <summary>hud.css:82 .ocbg-battle-plan::before (its opacity: var(--plan-alpha, 0.62), set from PlanAlpha).</summary>
        private const string PlanBeforeCss = "content: \"\"; position: absolute; inset: 0; border: var(--stroke-frame) dashed var(--ink-muted); border-radius: var(--radius-panel); pointer-events: none;";
        /// <summary>hud.css:85 .ocbg-battle-plan-row1 (hud.css:83 .ocbg-battle-plan > div { opacity: var(--plan-alpha, 0.62) }).</summary>
        private const string PlanRow1Css = "display: flex; align-items: center; gap: 8px; height: 32px;";
        /// <summary>hud.css:86 .ocbg-battle-plan-row1 b.</summary>
        private const string PlanRow1BCss = "font: 700 28px/32px var(--font-mono); font-variant-numeric: tabular-nums;";
        /// <summary>hud.css:87 .ocbg-battle-plan-row2.</summary>
        private const string PlanRow2Css = "display: flex; align-items: center; justify-content: space-between; gap: 6px; height: 20px; font: 400 16px/20px var(--font-body); white-space: nowrap;";
        /// <summary>hud.css:88 .ocbg-battle-plan-tag.</summary>
        private const string PlanTagCss = "font: 400 14px/16px var(--font-body); color: var(--ink); border: var(--stroke-rule) dashed var(--ink-muted); border-radius: 3px; padding: 0 4px;";
        /// <summary>hud.css:89 .ocbg-battle-plan-tag.is-changed.</summary>
        private const string PlanTagChangedCss = "border-style: solid; border-color: var(--ink);";
        /// <summary>hud.css:189 .ocbg-battle-omen-trait.</summary>
        private const string TraitCss = "position: absolute; right: 8px; top: 50%; translate: 0 -50%; padding: 4px 10px; background: var(--panel-solid); border: var(--stroke-frame) solid var(--phosphor); border-radius: var(--radius-tag, 4px); font: 400 16px/22px var(--font-body); color: var(--ink); white-space: nowrap; pointer-events: none; z-index: 2; box-shadow: 0 0 0 3px var(--panel-solid);";

        // ---- state (omen.js:54) ----------------------------------------------------------------------------------------

        private readonly BattleViewEnv _env;
        private RectTransform _wrap;
        private UiNode _wrapNode;
        private RectTransform _plan;
        private UiNode _planNode;
        private RectTransform _planBefore;
        private RectTransform _badge;
        private UiNode _badgeNode;
        /// <summary>The ▼ (the JS's one <i>, appended to the badge again on every redraw).</summary>
        private PathGraphic _tail;
        private TextMeshProUGUI _kindEl;
        private RectTransform _sideEl;
        private RectTransform _hitEl;
        /// <summary>The trait words standing in the badge now (badge.querySelector('.ocbg-battle-omen-trait')).</summary>
        private readonly List<RectTransform> _traitTags = new List<RectTransform>();

        private OmenFrame _frame;
        private bool _shown;
        private float _badgeAlpha;
        private float _planAlpha;
        private float _x;
        private float _y;
        private bool _acting;
        private bool _struck;
        private float _blink = 1f;
        private int _moveDir;
        private bool _swatches;
        private bool _aimed;
        /// <summary>The way this badge's swatch turns: its enemy's EnemyFrame.StripeDirection (#589).</summary>
        private int _stripe;
        private float? _tailAt;
        private float _drop;
        private bool _behind;
        /// <summary>The badge's classes as last drawn: .is-rest (the frame's icon is a rest).</summary>
        private bool _rest;
        /// <summary>wrap.style.left was written once (place()'s early return needs it).</summary>
        private bool _placed;
        /// <summary>wrap.style.display (show()).</summary>
        private bool _display = true;
        /// <summary>wrap.style.visibility = 'hidden' (drawAlpha: nothing of the badge or its plan shows).</summary>
        private bool _hidden;
        private bool _traitClass;

        // js: scenes/battle/hud/omen.js:44
        private HudOmenBadge(BattleViewEnv env)
        {
            _env = env;
            RectTransform panels = env != null ? env.Layer.Panels : null;
            if (!panels) return;
            // The wrap carries the badge's place and scale; the plan is its child (it scales with the badge, as in Unity) but
            // keeps its own alpha (its CanvasGroup ignores the badge's).
            _wrap = Ui.Div(panels, "ocbg-battle-omenwrap");
            _wrapNode = UiNode.Of(_wrap);
            _wrapNode.Place(0f, 0f, BadgeW, BadgeH);
            _wrapNode.Z = WrapZ;
            // const plan = h('div', { class: 'ocbg-battle-plan' }); (first: under the badge)
            _plan = Ocb.El(_wrap, "ocbg-battle-plan", PlanCss);
            _planNode = UiNode.Of(_plan);
            _planBefore = Ocb.El(_plan, "before", PlanBeforeCss);
            Ocb.Absolute(_planBefore);
            UiNode before = UiNode.Of(_planBefore);
            before.Place(0f, 0f, PlanW, PlanH); // inset: 0
            before.Opacity = PlanAlpha; // opacity: var(--plan-alpha, 0.62)
            // const badge = h('div', { class: 'ocb-omen ocbg-battle-omen' });
            _badge = Ocb.El(_wrap, "ocb-omen ocbg-battle-omen", BadgeCss());
            _badgeNode = UiNode.Of(_badge);
            _badgeNode.Place(0f, 0f, BadgeW, BadgeH);
            // const tail = h('i', { class: 'ocbg-battle-omen-tail' }); tail.style.display = 'none';
            _tail = PathGraphic.Make(_badge, 0f, 0f, TailW, TailH, "ocbg-battle-omen-tail");
            Ocb.Absolute(_tail);
            TailColor();
            UiNode.Of(_tail.rectTransform).Visible = false;
            // tail.style.top = BADGE.h - 2 + 'px' (from under the 2 px frame: the badge's box is border-box)
            PlaceTail();
            // plan.style.left = (BADGE.w - PLAN.w) / 2; plan.style.top = -(PLAN.h + PLAN.gap)
            _planNode.Place((BadgeW - PlanW) / 2f, -(PlanH + PlanGap), PlanW, PlanH);
            // .ocbg-battle-plan:empty { display: none }
            _planNode.Visible = false;
            DrawAlpha();
        }

        /// <summary>createOmenBadge(env): the wrap (badge + plan + tail) in layer.panels, hidden until bound.</summary>
        // js: scenes/battle/hud/omen.js:44
        public static HudOmenBadge Create(BattleViewEnv env) => new HudOmenBadge(env);

        /// <summary>el: the wrap (badge, plan, tail) — the hud fades it.</summary>
        // js: scenes/battle/hud/omen.js:152
        public RectTransform El => _wrap;

        /// <summary>The badge plate itself (.ocbg-battle-omen, without the plan): hud.flyOmenToStances flies a copy of it.</summary>
        // js: scenes/battle/hud.js:619 (v.omen.el.querySelector('.ocbg-battle-omen'))
        public RectTransform Face => _badge ? _badge : _wrap;

        // ---- helpers ----------------------------------------------------------------------------------------------------

        /// <summary>Number.prototype.toFixed(digits) read back as a number (the style string the JS writes).</summary>
        private static float Fixed(float x, int digits) => (float)Math.Round((double)x, digits, MidpointRounding.AwayFromZero);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>The badge's rule in force: .ocb-omen, the battle's, then .is-active and .is-rest (bundle.css:56-57 order).</summary>
        private string BadgeCss() =>
            Ocb.OmenCss + BattleOmenCss + (_acting ? Ocb.OmenActiveCss : "") + (_rest ? Ocb.OmenRestCss : "");

        /// <summary>The badge's border widths and padding (2 px frame, padding 8 12).</summary>
        private BoxStyle BadgeBox() => Ocb.BoxOf(BadgeCss());

        /// <summary>badge.classList.toggle('is-rest' / 'is-active'): the plate's frame and light, and the tail's colour.</summary>
        private void RestyleBadge()
        {
            if (!_badge) return;
            Ocb.Restyle(_badge, BadgeCss());
            TailColor();
        }

        /// <summary>hud.css:69-71: the ▼ in the badge's frame colour (violet; .is-active phosphor; .is-rest steel — the later rule).</summary>
        private void TailColor()
        {
            if (!_tail) return;
            Rgba c = _rest ? DsTokens.Steel : _acting ? DsTokens.Phosphor : DsTokens.Violet;
            _tail.Clear();
            _tail.AddFill(TailPath, c);
        }

        /// <summary>The tail at st.tail: left from the badge's padding box (inside its 2 px frame), top BADGE.h − 2 from it.</summary>
        // js: scenes/battle/hud/omen.js:187 (left) and :343 (top)
        private void PlaceTail()
        {
            if (!_tail) return;
            Insets bw = BadgeBox().BorderWidths;
            float left = _tailAt.HasValue ? Fixed(_tailAt.Value - TailW / 2f - 2f, 1) : 0f;
            UiNode.Of(_tail.rectTransform).Place(bw.Left + left, bw.Top + (BadgeH - 2f), TailW, TailH);
        }

        /// <summary>wrap shown = display (show) and visibility (drawAlpha).</summary>
        private void ApplyShown()
        {
            if (!_wrap) return;
            bool on = _display && !_hidden;
            if (_wrap.gameObject.activeSelf != on) _wrap.gameObject.SetActive(on);
        }

        /// <summary>The wrap's z-index: 2; .is-behind 1; .is-trait 4 (the later rule of the same weight wins).</summary>
        private void ApplyZ()
        {
            if (!_wrapNode) return;
            _wrapNode.Z = _traitClass ? WrapTraitZ : _behind ? WrapBehindZ : WrapZ;
        }

        /// <summary>badge.innerHTML = '': every child goes but the tail (appended again after the rows).</summary>
        private void ClearBadge()
        {
            if (!_badge) return;
            for (int i = _badge.childCount - 1; i >= 0; i--)
            {
                Transform c = _badge.GetChild(i);
                if (_tail && c == _tail.transform) continue;
                Ui.Remove(c);
            }
        }

        /// <summary>plan.innerHTML = '': the rows go (the ::before frame is the stylesheet's).</summary>
        private void ClearPlan()
        {
            if (!_plan) return;
            for (int i = _plan.childCount - 1; i >= 0; i--)
            {
                Transform c = _plan.GetChild(i);
                if (_planBefore && c == _planBefore) continue;
                Ui.Remove(c);
            }
            _planNode.Visible = false; // .ocbg-battle-plan:empty { display: none }
        }

        // ---- the badge's pieces -----------------------------------------------------------------------------------------

        /// <summary>
        /// OmenBadgeView.StatusRow: at most two statuses, icons in body ink, a word with no icon by name in the boss double
        /// frame. `inherited`: the ancestors' rules (the text's font and colour). In the badge it is at most 106 wide.
        /// </summary>
        // js: scenes/battle/hud/omen.js:34
        private static RectTransform StatusRow(RectTransform parent, IReadOnlyList<OmenStatus> statuses, float size, bool inBadge, string inherited)
        {
            string css = StatusesCss + (inBadge ? StatusesInBadgeCss : "");
            RectTransform row = Ocb.El(parent, "ocbg-battle-omen-statuses", css);
            int n = 0;
            if (statuses != null)
            {
                foreach (OmenStatus s in statuses)
                {
                    if (n++ >= 2) break;
                    StatusGlyph glyph = s != null ? s.Glyph : StatusGlyph.None;
                    if (HudUtil.GlyphIcon.TryGetValue(glyph, out string icon) && glyph != StatusGlyph.None) Ocb.Icon(row, icon, size, DsTokens.Ink);
                    else BossWord(row, s != null ? s.Label : "", size, inherited + css);
                }
            }
            Ocb.Flow(row, css);
            // min-width: 0 and overflow: hidden (it may shrink in its row and clips what does not fit); max-width 106 in the badge
            FlexAttrs a = FlexBox.Attrs(row);
            a.MinMain = 0f;
            if (inBadge) a.MaxMain = StatusesInBadgeMax;
            row.gameObject.AddComponent<RectMask2D>();
            return row;
        }

        /// <summary>h('span', { class: 'ocbg-battle-bossword', style: { height: size + 'px' } }, s.Label || '').</summary>
        // js: scenes/battle/hud/omen.js:39
        private static void BossWord(RectTransform row, string label, float size, string inherited)
        {
            string css = BossWordCss + "height: " + Ocb.Num(size) + "px;";
            RectTransform el = Ocb.El(row, "ocbg-battle-bossword", css);
            Ocb.NoShrink(el);
            Ocb.Text(el, label ?? "", Ocb.TextOf(inherited + css), "text");
            Ocb.Flow(el, css);
        }

        /// <summary>h('span', { class: 'ocbg-battle-omen-side' + (struck ? ' is-struck' : '') }, glyph): 64×28, the v4.2 reach glyph.</summary>
        // js: scenes/battle/hud/omen.js:129
        private RectTransform SideGlyph(RectTransform row2, string glyph, bool struck, string inherited)
        {
            string css = SideCss + (struck ? SideStruckCss : "");
            RectTransform el = Ocb.El(row2, "ocbg-battle-omen-side", css);
            Ocb.Fixed(el);
            Ocb.Text(el, glyph ?? "", Ocb.TextOf(inherited + SideCss), "text");
            Ocb.Flow(el, css);
            if (struck) StruckLine(el);
            return el;
        }

        /// <summary>sideEl.classList.add('is-struck'): dashed --miss frame at .8, and the ::after slash.</summary>
        private static void StrikeEl(RectTransform el)
        {
            if (!el) return;
            Ocb.Restyle(el, SideCss + SideStruckCss, null, opacity: true);
            StruckLine(el);
        }

        /// <summary>.is-struck::after: a 2 px --miss line from left 4 to right 4 at top 50 % of the padding box, rotated −24°.</summary>
        private static void StruckLine(RectTransform el)
        {
            if (!el || el.Find("after")) return;
            Insets bw = Ocb.BoxOf(SideCss).BorderWidths;
            UiNode n = UiNode.Of(el);
            float pw = n.W - bw.Horizontal, ph = n.H - bw.Vertical;
            RectTransform after = Ocb.El(el, "after", SideStruckAfterCss);
            Ocb.Absolute(after);
            UiNode an = UiNode.Of(after);
            an.Place(bw.Left + 4f, bw.Top + ph * 0.5f, pw - 8f, 2f);
            an.SetStyleTransform(rot: -24f);
        }

        // ---- omen.js:66-149 ---------------------------------------------------------------------------------------------

        /// <summary>The badge's place on the stage, with the drop it is coming in from (popIn).</summary>
        // js: scenes/battle/hud/omen.js:66
        private void PlaceNow()
        {
            if (!_wrapNode) return;
            _wrapNode.Place(Fixed(_x, 1), Fixed(_y + _drop, 1));
            _placed = true;
        }

        // js: scenes/battle/hud/omen.js:71
        private void DrawAlpha()
        {
            if (!_wrap) return;
            _badgeNode.Opacity = Fixed(_badgeAlpha * _blink, 3);
            // The plan reads at 62 % (OmenBadgeView.PlanAlpha) through its contents and frame; its panel stays opaque, so the
            // backdrop of a bright layer does not wash it out (DS: numbers and words stand on an opaque panel).
            _planNode.Opacity = Fixed(_planAlpha, 3);
            // plan.style.setProperty('--plan-alpha', String(PLAN.alpha)): the ::before and the rows carry PlanAlpha (DrawPlan).
            _hidden = _badgeAlpha <= 0.001f && _planAlpha <= 0.001f;
            ApplyShown();
        }

        /// <summary>
        /// The kind's icon. A move points the way the enemy moves on screen (battle-visual-v1 §4.4; review fix: every move drew
        /// ←, and Unity always draws 前へ →): the frame's own destination cells (FloorFrame.MoveCells) against the enemy's cell
        /// give the way (setMoveDir); with none known it keeps the design system's ← (an enemy's step in).
        /// </summary>
        // js: scenes/battle/hud/omen.js:85
        private static HudIconLook? IconOf(OmenIcon icon, int moveDir)
        {
            if (!HudUtil.OmenIconLook.TryGetValue(icon, out HudIconLook look)) return null;
            if (icon == OmenIcon.Move && moveDir > 0) return new HudIconLook("forward", look.Color);
            return look;
        }

        // js: scenes/battle/hud/omen.js:91
        private void DrawBadge(OmenFrame o)
        {
            if (!_badge || o == null) return;
            ClearBadge();
            _traitTags.Clear();
            HudIconLook? look = IconOf(o.Icon, _moveDir);
            bool rests = o.Icon == OmenIcon.Rest;
            // badge.classList.toggle('is-rest', rests); badge.classList.toggle('is-active', st.acting);
            _rest = rests;
            RestyleBadge();
            string badgeCss = BadgeCss();
            BoxStyle bs = BadgeBox();
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            float innerW = BadgeW - bw.Horizontal - pad.Horizontal;
            string row1Css = Ocb.OmenRow1Css + BattleRow1Css;

            RectTransform row1 = Ocb.El(_badge, "ocb-omen-row1", row1Css);
            row1.gameObject.AddComponent<RectMask2D>(); // overflow: hidden
            if (look.HasValue && o.Icon != OmenIcon.None) Ocb.Icon(row1, look.Value.Icon, IconPx, look.Value.Rgba);
            else Ocb.NoShrink(Ocb.El(row1, "ocbg-battle-omen-noicon", NoIconCss));
            if (o.Statuses != null && o.Statuses.Count > 0) StatusRow(row1, o.Statuses, StatusIconPx, true, badgeCss + row1Css);
            else if (!string.IsNullOrEmpty(o.ValueText))
            {
                TextStyle vs = Ocb.TextOf(badgeCss + row1Css + Ocb.OmenRow1BCss + BattleRow1BCss)
                    .WithColor(CssColor.Parse(HudUtil.OmenValueColor(o.Icon)));
                Ocb.Text(row1, o.ValueText, vs, "b");
            }
            Ocb.Flow(row1, row1Css, null, innerW);
            bool marked = o.Hit != OmenHit.None;

            // §4.4: the swatch at the top right ties the badge to this enemy's red stripes on the floor; it stands with the hit
            // mark (OmenBadgeView.BuildHitParts: 22×22, 12 px from the right, 14 from the top). With several enemies it stands
            // whenever this enemy's omen hatches cells on the floor (EnemyFrame.AimCells). Review 16: the DS OmenPlate's
            // 「移動の行動に赤の色見本を付けないでください」 (angle null for an action that aims at no cell) and battle-visual-v1 §2.3
            // keep the red to the cells an action reaches, so a move omen (its cells are the violet dashed frame) and a rest
            // carry none; the badge's ▼ and the panels' swatches say whose it is. battle_ui_ux_v2 §7 (review 21): an enemy read
            // to 0 shows the kind alone — no aimed cells on the floor, so no swatch for them; the hit mark stays (§7
            // 「当たり外れの印はどの開示度でも出します」).
            bool unread = o.Disclosure < 1;
            if (!unread && (marked || (_swatches && _aimed && !rests)))
            {
                OcbElement sw = Ocb.Swatch(_badge, BattleLayout.HatchAngle(_stripe), SwatchPx);
                Ocb.Absolute(sw.Root);
                // position: absolute; right: 12px; top: 14px (from the padding box)
                sw.Node.Place(BadgeW - bw.Right - 12f - SwatchPx, bw.Top + 14f);
            }

            string row2Css = Ocb.OmenRow2Css;
            RectTransform row2 = Ocb.El(_badge, "ocb-omen-row2", row2Css);
            _kindEl = Ocb.Text(row2, o.KindLabel ?? "", Ocb.TextOf(badgeCss + row2Css + KindCss), "ocbg-battle-omen-kind");
            _sideEl = null;
            _hitEl = null;
            if (marked)
            {
                bool lands = o.Hit == OmenHit.Lands;
                // h('span', { class: 'ocb-hitmark is-hit|is-miss' }, OCB.icon('person', HIT_ICON, phosphor | miss))
                _hitEl = Ocb.HitMark(row2, lands).Root;
            }
            else if (!string.IsNullOrEmpty(o.SideGlyph))
            {
                // The v4.2 sources' reach glyph; the hit mark takes its place once the floor shows the aimed cells.
                _sideEl = SideGlyph(row2, o.SideGlyph, _struck, badgeCss + row2Css);
            }
            Ocb.Flow(row2, row2Css, null, innerW);
            // badge.appendChild(tail)
            if (_tail) _tail.transform.SetAsLastSibling();
            Ocb.Flow(_badge, badgeCss);
            PlaceTail();
        }

        // js: scenes/battle/hud/omen.js:136
        private void DrawPlan(OmenFrame o)
        {
            if (!_plan || o == null) return;
            ClearPlan();
            // plan.style.translate = ''
            _planNode.PropTranslateX = 0f;
            _planNode.PropTranslateY = 0f;
            if (!o.PlanVisible) return;
            _planNode.Visible = true;
            BoxStyle bs = Ocb.BoxOf(PlanCss);
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            float innerW = PlanW - bw.Horizontal - pad.Horizontal;

            RectTransform row1 = Ocb.El(_plan, "ocbg-battle-plan-row1", PlanRow1Css);
            bool hasLook = HudUtil.OmenIconLook.TryGetValue(o.PlanIcon, out HudIconLook look);
            if (hasLook && o.PlanIcon != OmenIcon.None) Ocb.Icon(row1, look.Icon, PlanIconPx, look.Rgba);
            if (o.PlanStatuses != null && o.PlanStatuses.Count > 0) StatusRow(row1, o.PlanStatuses, PlanStatusIconPx, false, PlanCss + PlanRow1Css);
            else if (!string.IsNullOrEmpty(o.PlanValueText))
            {
                TextStyle vs = Ocb.TextOf(PlanCss + PlanRow1Css + PlanRow1BCss).WithColor(CssColor.Parse(HudUtil.OmenValueColor(o.PlanIcon)));
                Ocb.Text(row1, o.PlanValueText, vs, "b");
            }
            Ocb.Flow(row1, PlanRow1Css, null, innerW);

            RectTransform row2 = Ocb.El(_plan, "ocbg-battle-plan-row2", PlanRow2Css);
            Ocb.Text(row2, o.PlanKindLabel ?? "", Ocb.TextOf(PlanCss + PlanRow2Css), "span");
            string tagCss = PlanTagCss + (o.PlanChanged ? PlanTagChangedCss : "");
            RectTransform tag = Ocb.El(row2, "ocb-plan ocbg-battle-plan-tag", tagCss);
            Ocb.Text(tag, o.PlanTag ?? "", Ocb.TextOf(PlanCss + PlanRow2Css + tagCss), "text");
            Ocb.Flow(tag, tagCss);
            Ocb.Flow(row2, PlanRow2Css, null, innerW);

            // .ocbg-battle-plan > div { opacity: var(--plan-alpha, 0.62) }
            UiNode.Of(row1).Opacity = PlanAlpha;
            UiNode.Of(row2).Opacity = PlanAlpha;
            Ocb.Flow(_plan, PlanCss);
        }

        /// <summary>The frame promotePlan draws: the plan's words and number as the badge's (no side glyph, no hit mark, no plan).</summary>
        // js: scenes/battle/hud/omen.js:216
        private static OmenFrame Promoted(OmenFrame o) => new OmenFrame
        {
            Visible = true,
            Icon = o.PlanIcon,
            KindLabel = o.PlanKindLabel,
            SideGlyph = "",
            ValueText = o.PlanValueText,
            Hit = OmenHit.None,
            Statuses = o.PlanStatuses ?? new List<OmenStatus>(),
            Disclosure = o.Disclosure,
            PlanVisible = false,
            PlanIcon = o.PlanIcon,
            PlanKindLabel = o.PlanKindLabel,
            PlanValueText = o.PlanValueText,
            PlanStatuses = o.PlanStatuses,
            PlanSideGlyph = o.PlanSideGlyph,
            PlanChanged = o.PlanChanged,
        };

        // ---- api (omen.js:151) ------------------------------------------------------------------------------------------

        /// <summary>OmenBadgeView.Bind: the badge as the frame has it (alpha 1 when Visible), the plan at 62 % when PlanVisible.</summary>
        // js: scenes/battle/hud/omen.js:154
        public void Bind(OmenFrame o)
        {
            if (o == null) return;
            _frame = o;
            _shown = o.Visible;
            _struck = false;
            _badgeAlpha = _shown ? 1f : 0f;
            _planAlpha = o.PlanVisible ? 1f : 0f;
            DrawBadge(o);
            DrawPlan(o);
            DrawAlpha();
        }

        /// <summary>
        /// Several enemies (EnemyFrame.Swatch): the swatch stands on the badge while `aimed` — this enemy's omen hatches cells
        /// on the floor (EnemyFrame.AimCells); never on a move (DS OmenPlate) or a rest. `stripe` is the enemy's
        /// EnemyFrame.StripeDirection, the way the swatch turns.
        /// </summary>
        // js: scenes/battle/hud/omen.js:169
        public void SetSwatchShown(bool on, bool aimed, int stripe = 0)
        {
            if (on == _swatches && aimed == _aimed && stripe == _stripe) return;
            _swatches = on;
            _aimed = aimed;
            _stripe = stripe;
            if (_frame != null) DrawBadge(_frame);
        }

        /// <summary>
        /// The ▼ under the badge at `dx` px from its left edge (its enemy's head x), or null for none. It stays inside the
        /// badge's rounded corners.
        /// </summary>
        // js: scenes/battle/hud/omen.js:181
        public void SetTail(float? dx)
        {
            float? want = dx.HasValue ? Math.Max(TailW / 2f + 6f, Math.Min(BadgeW - TailW / 2f - 6f, dx.Value)) : (float?)null;
            if (want == _tailAt || (want.HasValue && _tailAt.HasValue && Math.Abs(want.Value - _tailAt.Value) < 0.25f)) return;
            _tailAt = want;
            if (!_tail) return;
            UiNode.Of(_tail.rectTransform).Visible = want.HasValue;
            // left from the badge's padding box (inside its 2 px frame)
            if (want.HasValue) PlaceTail();
        }

        /// <summary>A walking enemy's badge stands behind the other badges until it arrives (hud.follow; z-order only).</summary>
        // js: scenes/battle/hud/omen.js:190
        public void SetBehind(bool on)
        {
            if (on == _behind) return;
            _behind = on;
            ApplyZ(); // wrap.classList.toggle('is-behind', on)
        }

        /// <summary>The way a move omen's enemy goes on screen: −1 left, +1 right, 0 unknown (the icon keeps ←).</summary>
        // js: scenes/battle/hud/omen.js:197
        public void SetMoveDir(int dir)
        {
            int d = dir > 0 ? 1 : dir < 0 ? -1 : 0;
            if (d == _moveDir) return;
            _moveDir = d;
            if (_frame != null) DrawBadge(_frame);
        }

        /// <summary>
        /// battle_ui_ux_v2 §3.10 (review fix): an elite's or a boss's second action. Its first omen is spent (the badge hidden)
        /// and the plan still stands over the empty slot: the plan drops into the lower slot over `ms` (Ease.Out), turns solid
        /// (the full badge, as the plan's own words and number) and is the omen the enemy acts on, so the acting light and a
        /// struck bonus have their badge. The next frame binds what the script says. Resolves true when it promoted the plan,
        /// false when there was nothing to promote.
        /// </summary>
        // js: scenes/battle/hud/omen.js:210
        public Promise<bool> PromotePlan(float ms)
        {
            OmenFrame o = _frame;
            if (o == null || !o.PlanVisible || _badgeAlpha > 0.001f || _planAlpha <= 0.001f) return Promise<bool>.FromResult(false);
            // Review 16: no PlanSideGlyph (the v4.2 reach text, which no other badge shows) where the hit mark goes. The frame
            // carries no hit mark and no aimed cells for the plan (OmenFrame has no PlanHit, the floor no plan cells), so the
            // promoted badge shows neither the mark nor the swatch, and the floor no stripes, until the writer writes them.
            OmenFrame promoted = Promoted(o);
            float drop = PlanH + PlanGap;
            _frame = promoted;
            _shown = true;
            DrawBadge(promoted);
            if (!(ms > 0f) || _env == null)
            {
                FinishPromote();
                return Promise<bool>.FromResult(true);
            }
            var done = new PromiseSource<bool>();
            _env.Clock.Run(ms, Ease.Out, t =>
            {
                if (_planNode) _planNode.PropTranslateY = Fixed(drop * t, 1);
                _planAlpha = 1f - t;
                _badgeAlpha = t;
                DrawAlpha();
            }).Then(() =>
            {
                FinishPromote();
                done.Resolve(true);
            }).Forget();
            return done.Promise;
        }

        // js: scenes/battle/hud/omen.js:224 (finish)
        private void FinishPromote()
        {
            _badgeAlpha = 1f;
            _planAlpha = 0f;
            if (_planNode)
            {
                _planNode.PropTranslateX = 0f;
                _planNode.PropTranslateY = 0f;
            }
            ClearPlan();
            DrawAlpha();
        }

        /// <summary>
        /// The omen comes in (battle_ui_ux_v2 §5.8 提示 「敵ごとの札が頭上へ降りる」 260 out, §5.16 順 1 the two rows together;
        /// review 16 — Unity's PopIn scales 0.6 → 1): the badge (or the plan alone, a 予定変更 after the first omen is spent)
        /// drops DROP_PX into place and fades in over `ms` (Ease.Out).
        /// </summary>
        // js: scenes/battle/hud/omen.js:244
        public Promise PopIn(float ms)
        {
            float to = _shown ? 1f : 0f;
            float planTo = _planAlpha;
            if (!(ms > 0f) || _env == null)
            {
                _badgeAlpha = to;
                _drop = 0f;
                DrawAlpha();
                PlaceNow();
                return Promise.Resolved();
            }
            // From the first frame on: over its place and clear (the bind just before put it up whole).
            _drop = -DropPx;
            _badgeAlpha = 0f;
            _planAlpha = 0f;
            DrawAlpha();
            PlaceNow();
            return _env.Clock.Run(ms, Ease.Out, t =>
            {
                _drop = -DropPx * (1f - t);
                _badgeAlpha = to * t;
                _planAlpha = planTo * t;
                DrawAlpha();
                PlaceNow();
            }).Then(() =>
            {
                _drop = 0f;
                _badgeAlpha = to;
                _planAlpha = planTo;
                DrawAlpha();
                PlaceNow();
            });
        }

        /// <summary>Blink: the standing omen dips to 25 % and back once (Ease.InOut).</summary>
        // js: scenes/battle/hud/omen.js:263
        public Promise Blink(float ms)
        {
            if (_env == null) return Promise.Resolved();
            return _env.Clock.Run(ms, Ease.InOut, t =>
            {
                _blink = Lerp(1f, 0.25f, (float)Math.Sin(t * Math.PI));
                DrawAlpha();
            }).Then(() =>
            {
                _blink = 1f;
                DrawAlpha();
            });
        }

        /// <summary>Flare: the omen swells 18 % and back as the enemy carries it out, its kind word flashing white (Ease.Out).</summary>
        // js: scenes/battle/hud/omen.js:268
        public Promise Flare(float ms)
        {
            if (_env == null) return Promise.Resolved();
            return _env.Clock.Run(ms, Ease.Out, t =>
            {
                if (_wrapNode) _wrapNode.PropScale = Fixed(1f + 0.18f * (float)Math.Sin(t * Math.PI), 4);
                if (_kindEl) UiNode.Of(_kindEl.rectTransform).Filter = t < 1f ? new ColorFilter(Fixed(Lerp(1.6f, 1f, t), 3)) : ColorFilter.None;
            }).Then(() =>
            {
                if (_wrapNode) _wrapNode.PropScale = 1f;
                if (_kindEl) UiNode.Of(_kindEl.rectTransform).Filter = ColorFilter.None;
            });
        }

        /// <summary>
        /// Scene addition: the badge was read further (OmenFrame.Disclosure rose between two frames, the journal's 開示度 handed
        /// in mid-battle): it swells once as its numbers and plan come in. Unity has no such beat.
        /// </summary>
        // js: scenes/battle/hud/omen.js:278
        public Promise Reveal(float ms)
        {
            if (_env == null) return Promise.Resolved();
            return _env.Clock.Run(ms, Ease.Out, t =>
            {
                if (_wrapNode) _wrapNode.PropScale = Fixed(1f + 0.12f * (float)Math.Sin(t * Math.PI), 4);
                if (_badgeNode) _badgeNode.Filter = t < 1f ? new ColorFilter(Fixed(Lerp(1.5f, 1f, t), 3)) : ColorFilter.None;
            }).Then(() =>
            {
                if (_wrapNode) _wrapNode.PropScale = 1f;
                if (_badgeNode) _badgeNode.Filter = ColorFilter.None;
            });
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.9 順 12 (review 19; Unity floats the line over the enemy's head): the enemy's action fires a trait —
        /// its condition word swells in at the right end of the badge over `ms` (out), stands while the action plays and fades.
        /// 16 px body ink on an opaque tag with the enemy's phosphor edge.
        /// </summary>
        // js: scenes/battle/hud/omen.js:289
        public Promise TraitWord(string word, float ms)
        {
            if (string.IsNullOrEmpty(word)) return Promise.Resolved();
            if (_env == null || !_badge) return Promise.Resolved();
            // Review 21 (the tag hung half outside the badge's right edge, on its border, and with three enemies the next badge
            // painted over it): it stands inside the badge's right end (§5.9 順 12 「1 手目の札の右端」), over the swatch and the hit
            // mark for its moment, and the badge stands over its neighbours while it shows.
            RectTransform tag = Ocb.El(_badge, "ocbg-battle-omen-trait", TraitCss);
            Ocb.Absolute(tag);
            Ocb.Text(tag, word, Ocb.TextOf(BadgeCss() + TraitCss), "text");
            Ocb.Flow(tag, TraitCss);
            UiNode tn = UiNode.Of(tag);
            tn.PointerEvents = PointerEvents.None;
            // right: 8px; top: 50%; translate: 0 -50% (in the badge's padding box)
            Insets bw = BadgeBox().BorderWidths;
            tn.Place(BadgeW - bw.Right - 8f - tn.W, bw.Top + (BadgeH - bw.Vertical) * 0.5f);
            tn.PropTranslateY = -tn.H * 0.5f;
            _traitTags.Add(tag);
            _traitClass = true; // wrap.classList.add('is-trait')
            ApplyZ();
            BattleClock clock = _env.Clock;
            Promise grow = ms > 0f
                ? clock.Run(ms, Ease.Out, t =>
                {
                    if (!tag) return;
                    tn.PropScale = Fixed(0.6f + 0.4f * t, 3);
                    tn.Opacity = Fixed(t, 3);
                })
                : Promise.Resolved();
            return grow.Then(() => clock.Wait(TraitWordHoldMs))
                .Then(() => clock.Run(TraitWordFadeMs, Ease.In, t => { if (tag) tn.Opacity = Fixed(1f - t, 3); }))
                .Then(() =>
                {
                    _traitTags.Remove(tag);
                    if (tag) Ui.Remove(tag);
                    if (!HasTraitTag())
                    {
                        _traitClass = false; // wrap.classList.remove('is-trait')
                        ApplyZ();
                    }
                });
        }

        /// <summary>badge.querySelector('.ocbg-battle-omen-trait'): a trait word still standing in the badge.</summary>
        private bool HasTraitTag()
        {
            _traitTags.RemoveAll(t => !t || t.parent != _badge);
            return _traitTags.Count > 0;
        }

        /// <summary>§4.4 敵の番: the phosphor edge and the violet light while the enemy acts on this omen.</summary>
        // js: scenes/battle/hud/omen.js:302
        public void SetActing(bool on)
        {
            _acting = on;
            RestyleBadge(); // badge.classList.toggle('is-active', st.acting)
        }

        /// <summary>FadeOut: the badge fades (Ease.In); the plan stays (a spent first omen keeps it up).</summary>
        // js: scenes/battle/hud/omen.js:307
        public Promise FadeOut(float ms)
        {
            float from = _badgeAlpha;
            if (_env == null)
            {
                _badgeAlpha = 0f;
                DrawAlpha();
                return Promise.Resolved();
            }
            return _env.Clock.Run(ms, Ease.In, t =>
            {
                _badgeAlpha = Lerp(from, 0f, t);
                DrawAlpha();
            });
        }

        // js: scenes/battle/hud/omen.js:311
        public void HideNow()
        {
            _badgeAlpha = 0f;
            DrawAlpha();
        }

        /// <summary>FadeOutAll: a fall takes the badge and its plan together.</summary>
        // js: scenes/battle/hud/omen.js:313
        public Promise FadeOutAll(float ms)
        {
            float b = _badgeAlpha;
            float p = _planAlpha;
            if (_env == null)
            {
                HideAllNow();
                return Promise.Resolved();
            }
            return _env.Clock.Run(ms, Ease.In, t =>
            {
                _badgeAlpha = Lerp(b, 0f, t);
                _planAlpha = Lerp(p, 0f, t);
                DrawAlpha();
            });
        }

        // js: scenes/battle/hud/omen.js:318
        public void HideAllNow()
        {
            _badgeAlpha = 0f;
            _planAlpha = 0f;
            DrawAlpha();
        }

        /// <summary>StrikeSide: the punished side did not apply — the reach glyph greys out, is struck and shakes.</summary>
        // js: scenes/battle/hud/omen.js:320
        public Promise StrikeSide(float ms)
        {
            _struck = true;
            if (_env == null) return Promise.Resolved();
            if (!_sideEl) return _env.Clock.Wait(ms);
            StrikeEl(_sideEl);
            return HudUtil.Shake(_env.Clock, _sideEl, 6f, 2f, ms);
        }

        // js: scenes/battle/hud/omen.js:326
        public void StrikeSideNow()
        {
            _struck = true;
            if (_sideEl) StrikeEl(_sideEl);
        }

        /// <summary>place(left, top) = KeepX / KeepBottom: the badge's top left (the caller's clamp).</summary>
        // js: scenes/battle/hud/omen.js:331
        public void Place(float left, float top)
        {
            if (Math.Abs(left - _x) < 0.05f && Math.Abs(top - _y) < 0.05f && _placed) return;
            _x = left;
            _y = top;
            PlaceNow();
        }

        // js: scenes/battle/hud/omen.js:337
        public StageBox Box() => new StageBox(_x, _y, BadgeW, BadgeH);

        /// <summary>show(on): the wrap shown or not (display).</summary>
        // js: scenes/battle/hud/omen.js:338
        public void Show(bool on)
        {
            _display = on;
            ApplyShown();
        }

        // js: scenes/battle/hud/omen.js:339
        public bool Visible => _badgeAlpha > 0.001f;

        /// <summary>destroy().</summary>
        // js: scenes/battle/hud/omen.js:340
        public void Destroy()
        {
            if (_wrap) Ui.Remove(_wrap);
            _wrap = null;
        }
    }
}
