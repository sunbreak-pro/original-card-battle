// The two stance lists of the battle's hud (StanceListView, docs/BATTLE_SCENE.md §6c, battle-views §3.3): 250×52 frames
// at x 24 (player) / 1646 (enemy) from y 470 up, ≤ 3 with the oldest folded into 「+n」, the empty frame, the new-stance
// ring, the hover / pinned tip, and the high place in the top bar when a figure's art would come under a list. In layer
// panels. Port of scenes/battle/hud/stances.js.
//
// The stance lists at the two edges of the screen: a port of StanceListView (battle-visual-v1 §4.6 スタンス枠,
// battle_core_v4 §4 v4.4, #455) in the design system's StanceSlot look. Each stance takes one 250×52 frame, stacked upward
// from the slot at y 470 (StanceList), the newest in the slot. The player's frames stand 24 px from the left edge in the
// stance colour; the enemy's 24 px from the right in violet with a phosphor flag. A stance held twice shows "×2". A side
// holding none shows the empty frame (steel dashed, steel flag). A list longer than three folds its oldest into a "+n"
// frame on top. Hovering a stance shows its chip's 説明 (the lasting chip the same unit carries: UnitFrame.Statuses), as
// the design system's StanceTip does.
//
// Two or more enemies (battle-visual-v1 §4.6 敵の枠: 「敵が 2 体以上なら左端に色見本を置きます」; review fix: only
// Frame.Enemy's stances were drawn): the enemy list holds every standing enemy's stances, each frame with its own enemy's
// hatch swatch on the left. Unity draws one enemy, so the order across enemies is the scene's: a stance goes to the slot
// when it is put down (or stacked again, §3.7), and the frames of one enemy keep the order its UnitFrame.Stances gives.
//
// uGUI: each frame is the DS port's Ocb.StanceSlot (its BoxGraphic takes the pointer: ClickArea for mouseenter /
// mouseleave and the right button's release, the contextmenu of a Windows browser; BattleHitTag = .ocbg-battle-hit).
// The ring is the frame's inline box-shadow, kept over the .is-hover restyle as an inline style is.
using System;
using System.Collections.Generic;
using System.Globalization;
using Depiction;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Trial.Battle
{
    /// <summary>createStanceLists's place: { highTop(), highLeft(side) } — the high place's top and left edge (the hud measures the corner and the speed switch).</summary>
    // js: scenes/battle/hud/stances.js:51
    public sealed class StancePlace
    {
        public Func<float> HighTop;
        public Func<UnitSide, float> HighLeft;
    }

    /// <summary>keepClear's rows: a standing figure's art box { x0, x1, y0, y1 } (stage px).</summary>
    // js: scenes/battle/hud.js:418
    public readonly struct ArtBox
    {
        public readonly float X0;
        public readonly float X1;
        public readonly float Y0;
        public readonly float Y1;

        public ArtBox(float x0, float x1, float y0, float y1)
        {
            X0 = x0;
            X1 = x1;
            Y0 = y0;
            Y1 = y1;
        }
    }

    /// <summary>createStanceLists(env, tip, place) → { bind, boxOf, slotOf, keepClear, isHigh, elOf, destroy } (stances.js:231).</summary>
    // js: scenes/battle/hud/stances.js:43
    public sealed class HudStanceLists
    {
        // ---- stances.js:20-41 -------------------------------------------------------------------------------------------

        // js: scenes/battle/hud/stances.js:20
        private const string LastingPrefix = "構え・";
        /// <summary>The 説明 beside a frame: 340 wide (the chips' panel), 12 px off the frame (DS renderStances).</summary>
        // js: scenes/battle/hud/stances.js:22
        private const float TipWidth = 340f;
        // js: scenes/battle/hud/stances.js:23
        private const float TipGap = 12f;
        /// <summary>The 説明's row heads, as the chips' panel (StatusBarView.ShowPanel). scene text</summary>
        // js: scenes/battle/hud/stances.js:25
        private const string EffectHead = "効き方";
        // js: scenes/battle/hud/stances.js:26
        private const string DecayHead = "減り方";
        /// <summary>
        /// SET_RING — DS 試作 (b) スタンス枠が立つときの光: a 4 px ring round the frame a stance was just put in, fading over 300 ms
        /// — it shows where the played card went. In the stance colour on the player's side, violet on the enemy's.
        /// </summary>
        // js: scenes/battle/hud/stances.js:31
        private const float SetRingPx = 4f;
        private const float SetRingMs = 300f;
        /// <summary>
        /// Review 21 (the frames at the screen's edges were drawn across the figures — a slid arena beside an open drawer, the
        /// hound's tail on the last of 8 cells, the hero beside the journal): a list that a standing figure's art would come
        /// under moves to its high place, in the top bar beside its side's corner chrome — the player's 12 px right of the
        /// corner information, the enemy's 12 px left of the speed switch (or the gear) — one frame there, the newest, with
        /// 「+n」 on it for the rest (its hover lists them all). It comes back to y 470 once the figures leave the place. A look
        /// call: Unity's StanceListView has one place, and battle-visual-v1 §4.6 none for a slid arena.
        /// </summary>
        // js: scenes/battle/hud/stances.js:40
        private const float HighGap = 12f;
        // js: scenes/battle/hud/stances.js:41
        private const float SwitchFadeMs = 150f;
        /// <summary>The real-time step of the high place's fade (env.real.wait(16)).</summary>
        private const float FadeStepMs = 16f;
        /// <summary>keepClear's margin: a figure within 4 px of a list comes under it.</summary>
        private const float ClearPad = 4f;
        /// <summary>The tip's distance from the screen's edges (clamp(…, 16, …)).</summary>
        private const float TipEdge = 16f;
        /// <summary>[UnitSide.Player, UnitSide.Enemy] (keepClear's walk; it runs every frame).</summary>
        private static readonly UnitSide[] BothSides = { UnitSide.Player, UnitSide.Enemy };

        // ---- hud.css / bundle.css (verbatim) ------------------------------------------------------------------------------

        /// <summary>hud.css:92 .ocbg-battle-stances.</summary>
        private const string HostCss = "position: absolute; left: 0; top: 0;";
        /// <summary>hud.css:93 .ocbg-battle-stance (+ el.style.position = 'absolute').</summary>
        private const string StanceBattleCss = "box-sizing: border-box; color: var(--ink); position: absolute;";
        /// <summary>hud.css:94 .ocbg-battle-stance > span.</summary>
        private const string StanceSpanCss = "white-space: nowrap; overflow: hidden; text-overflow: ellipsis;";
        /// <summary>hud.css:194 .ocbg-battle-stance-more (with .ocb-mono, bundle.css:6).</summary>
        private const string StanceMoreCss = "margin-left: auto; font: 600 16px/1 var(--font-mono); color: var(--ink); white-space: nowrap;";
        /// <summary>hud.css:195 .ocb-x + .ocbg-battle-stance-more.</summary>
        private const float StanceMoreAfterXMargin = 6f;
        /// <summary>hud.css:50 .ocbg-battle-chiptip .ocb-tip-head.</summary>
        private const string ChipTipHeadCss = "color: var(--ink);";
        /// <summary>hud.css:51 .ocbg-battle-chiptip .ocb-tip-head small.</summary>
        private const string ChipTipSmallCss = "color: var(--ink); font-weight: 400;";
        /// <summary>hud.css:52 .ocbg-battle-chiptip p.</summary>
        private const string ChipTipPCss = "color: var(--ink);";
        /// <summary>hud.css:53 .ocbg-battle-chiptip p + p.</summary>
        private const string ChipTipPPlusPCss = "margin-top: 4px;";
        /// <summary>hud.css:54 .ocbg-battle-chiptip p span.</summary>
        private const string ChipTipPSpanCss = "color: var(--ink); font-weight: 700; margin-right: 10px;";

        // ---- state --------------------------------------------------------------------------------------------------------

        /// <summary>One row of a side's list: { Name, Count, [unitFrame], [angle], [stamp], [order] } (merged rows carry the rest).</summary>
        private sealed class Row
        {
            public string Name;
            public int Count;
            public UnitFrame UnitFrame;
            /// <summary>`r.angle !== undefined`: a merged row carries its own enemy's swatch angle (null for none).</summary>
            public bool HasAngle;
            public float? Angle;
            public int Stamp;
            public int Order;
        }

        /// <summary>One frame on screen (s.el's child): the DS slot and its inline box-shadow (the new-stance ring).</summary>
        private sealed class FrameView
        {
            public OcbStanceSlot Slot;
            public BoxShadow? Ring;
        }

        /// <summary>sides[side]: { el, key, frames, high, … } (stances.js:47).</summary>
        private sealed class Side
        {
            public RectTransform El;
            public string Key;
            public bool High;
            public UnitFrame Unit;
            /// <summary>s.args = [unit, swatchAngle, merged].</summary>
            public bool HasArgs;
            public UnitFrame ArgUnit;
            /// <summary>Which enemy ArgUnit is (its index in DepictionFrame.Enemies, #589); −1 for the player's.</summary>
            public int ArgUnitNo = -1;
            public float? ArgSwatch;
            public List<Row> ArgMerged;
            public int RowsCount;
            public int? Held;
            public string Who;
            public int? Newest;
            public int FadeToken;
            public readonly List<FrameView> Frames = new List<FrameView>();
        }

        /// <summary>A stamp of the merged enemy list: { stamp, count }.</summary>
        private sealed class Stamp
        {
            public int Value;
            public int Count;
        }

        private readonly BattleViewEnv _env;
        private readonly HudTip _tip;
        private readonly StancePlace _place;
        private RectTransform _host;
        private readonly Side _player = new Side();
        private readonly Side _enemy = new Side();
        /// <summary>host.style.display === 'none' (bind with a frame that keeps no lists).</summary>
        private bool _hidden;
        private readonly Dictionary<string, Stamp> _stamps = new Dictionary<string, Stamp>(StringComparer.Ordinal);
        private int _stampNow;

        // js: scenes/battle/hud/stances.js:43
        private HudStanceLists(BattleViewEnv env, HudTip tip, StancePlace place)
        {
            _env = env;
            _tip = tip;
            _place = place;
            if (env != null && env.Layer.Panels)
            {
                // const host = h('div', { class: 'ocbg-battle-stances' }); layer.panels.appendChild(host);
                _host = Ui.Div(env.Layer.Panels, "ocbg-battle-stances", 0f, 0f);
                UiNode.Of(_host);
                // sides[side].el = h('div'); host.append(player.el, enemy.el)
                _player.El = Ui.Div(_host, "player", 0f, 0f);
                _enemy.El = Ui.Div(_host, "enemy", 0f, 0f);
                UiNode.Of(_player.El);
                UiNode.Of(_enemy.El);
            }
        }

        /// <summary>createStanceLists(env, tip, place): the host in layer.panels with a list per side.</summary>
        // js: scenes/battle/hud/stances.js:43
        public static HudStanceLists Create(BattleViewEnv env, HudTip tip, StancePlace place) => new HudStanceLists(env, tip, place);

        private Side SideOf(UnitSide side) => side == UnitSide.Player ? _player : _enemy;

        /// <summary>Number.prototype.toFixed(digits) read back as a number.</summary>
        private static float Fixed(float x, int digits) => (float)Math.Round((double)x, digits, MidpointRounding.AwayFromZero);

        /// <summary>String(x) of a JS number or null ("null").</summary>
        private static string Str(float? x) => x.HasValue ? Ocb.Num(x.Value) : "null";

        /// <summary>Text made safe for TMP rich text (a literal '&lt;' cannot open a tag).</summary>
        private static string Esc(string text) => string.IsNullOrEmpty(text) ? "" : text.Replace("<", "<noparse><</noparse>");

        // ---- stances.js:52-139 --------------------------------------------------------------------------------------------

        /// <summary>`place.highTop()`: the high place's top (20 without a place).</summary>
        // js: scenes/battle/hud/stances.js:52
        private float HighTop() => _place != null && _place.HighTop != null ? _place.HighTop() : 20f;

        /// <summary>`place.highLeft(side)`: the high place's left edge (the hud measures the corner and the speed switch).</summary>
        // js: scenes/battle/hud/stances.js:53
        private float HighLeft(UnitSide side) => _place != null && _place.HighLeft != null ? _place.HighLeft(side) : StanceList.LeftOf(side);

        /// <summary>Where frame i of `side`'s list stands now: { left, top }.</summary>
        // js: scenes/battle/hud/stances.js:55
        private Vector2 FrameAt(UnitSide side, int i) =>
            SideOf(side).High ? new Vector2(HighLeft(side), HighTop()) : new Vector2(StanceList.LeftOf(side), StanceList.TopOf(i));

        /// <summary>The lasting chip the unit carries for the stance `name` (「構え・name」 or `name`), or null.</summary>
        // js: scenes/battle/hud/stances.js:60
        private static StatusChip ChipFor(UnitFrame unit, string name)
        {
            List<StatusChip> list = unit != null ? unit.Statuses : null;
            if (list == null) return null;
            foreach (StatusChip c in list)
                if (c != null && c.Kind == ChipKind.Lasting && (c.Label == LastingPrefix + name || c.Label == name)) return c;
            return null;
        }

        // js: scenes/battle/hud/stances.js:64
        private void ShowTip(UnitSide side, int i, UnitFrame unit, Row row, List<Row> folded, bool pin)
        {
            if (_tip == null || _env == null) return;
            RectTransform over = _env.Layer.Over;
            if (!over) return;
            bool foe = side == UnitSide.Enemy;
            RectTransform node = folded != null ? FoldedTip(over, folded, unit) : RowTip(over, unit, row, foe);
            Vector2 at = FrameAt(side, i);
            Side s = SideOf(side);
            bool high = s.High;
            Action<RectTransform> putTip = n =>
            {
                if (!n) return;
                UiNode nn = UiNode.Of(n);
                // Beside the frame (the player's on its right, the enemy's on its left); under it in the high place.
                if (high)
                {
                    nn.Place(HudUtil.Clamp(at.x + StanceList.Width / 2f - TipWidth / 2f, TipEdge, L.W - TipEdge - TipWidth),
                        at.y + StanceList.RowHeight + TipGap);
                    return;
                }
                nn.Place(foe ? at.x - TipGap - TipWidth : at.x + StanceList.Width + TipGap,
                    HudUtil.Clamp(at.y - 10f, TipEdge, L.H - TipEdge - nn.H));
            };
            // §3.13 (review 21): a right click pins it (the same detail) until a click, Esc or another right click.
            if (pin) _tip.Pin(node, putTip, s);
            else _tip.Show(node, putTip, s);
        }

        /// <summary>
        /// The 説明 of one stance: div.ocb-tip.ocbg-battle-chiptip (340 wide) — the head (flag 20 in phosphor for the enemy's,
        /// hp-text for the player's; the title; the family as small at the right), then 効き方 / 減り方. Review 16: an enemy's
        /// stance has no lasting chip in UnitFrame.Statuses (the writer writes the player's only), so its hover showed nothing;
        /// the panel then says what the frame has — the name and ×n (StanceList.CountText).
        /// </summary>
        // js: scenes/battle/hud/stances.js:75
        private static RectTransform RowTip(RectTransform over, UnitFrame unit, Row row, bool foe)
        {
            StatusChip c = row != null ? ChipFor(unit, row.Name) : null;
            string count = StanceList.CountText(row != null ? row.Count : 0);
            string name = row != null ? row.Name : "";
            string title = c != null && !string.IsNullOrEmpty(c.Title) ? c.Title : name + (count.Length > 0 ? " " + count : "");
            var rows = new List<string>();
            string bold = BoldOpen();
            float spanGap = SpanGap();
            if (c != null && !string.IsNullOrEmpty(c.Effect)) rows.Add(bold + Esc(EffectHead) + "</font><space=" + Ocb.Num(spanGap) + ">" + Esc(c.Effect));
            if (c != null && !string.IsNullOrEmpty(c.Decay)) rows.Add(bold + Esc(DecayHead) + "</font><space=" + Ocb.Num(spanGap) + ">" + Esc(c.Decay));
            return TipNode(over, true, foe ? DsTokens.Phosphor : DsTokens.HpText, title, c != null && !string.IsNullOrEmpty(c.Group) ? c.Group : null, rows);
        }

        /// <summary>The 説明 of a 「+n」 frame: one p per folded stance, 「name ×n：effect」.</summary>
        // js: scenes/battle/hud/stances.js:68
        private static RectTransform FoldedTip(RectTransform over, List<Row> folded, UnitFrame unit)
        {
            var rows = new List<string>();
            foreach (Row r in folded)
            {
                StatusChip c = ChipFor(r.UnitFrame ?? unit, r.Name);
                string count = StanceList.CountText(r.Count);
                rows.Add(Esc(r.Name + (count.Length > 0 ? " " + count : "") + (c != null && !string.IsNullOrEmpty(c.Effect) ? "：" + c.Effect : "")));
            }
            return TipNode(over, false, default, null, null, rows);
        }

        /// <summary>p span { font-weight: 700 }: the head word in the body face's bold (registered with TMP by TrialFonts).</summary>
        private static string BoldOpen()
        {
            TrialFonts.Get(Family.Body, 700);
            return "<font=\"" + TrialFonts.Tag(Family.Body, 700) + "\">";
        }

        /// <summary>p span { margin-right: 10px }.</summary>
        private static float SpanGap() => Ocb.Px(Ocb.Rule(Ocb.TipPSpanCss + ChipTipPSpanCss).Layout.MarginRight) ?? 10f;

        /// <summary>
        /// h('div', { class: 'ocb-tip ocbg-battle-chiptip' }, [head], p…) with node.style.width = 340: laid out as the DS's .ocb-tip
        /// with hud.css's chip-tip rules over it; its height follows the text.
        /// </summary>
        private static RectTransform TipNode(RectTransform over, bool withHead, Rgba iconColor, string title, string small, List<string> rows)
        {
            string tipCss = Ocb.TipCss + "width: " + Ocb.Num(TipWidth) + "px;";
            RectTransform node = Ocb.El(over, "ocb-tip ocbg-battle-chiptip", tipCss);
            UiNode nn = UiNode.Of(node);
            BoxStyle bs = Ocb.BoxOf(tipCss);
            Insets bw = bs.BorderWidths, pad = bs.Padding;
            float innerW = nn.W - bw.Horizontal - pad.Horizontal;
            float x = bw.Left + pad.Left, y = bw.Top + pad.Top;

            if (withHead)
            {
                // h('div', { class: 'ocb-tip-head' }, OCB.icon('flag', 20, …), h('span', null, title), c && c.Group ? h('small', null, c.Group) : null)
                string headCss = Ocb.TipHeadCss + ChipTipHeadCss;
                RectTransform head = Ocb.El(node, "ocb-tip-head", headCss);
                Ocb.Icon(head, "flag", 20f, iconColor);
                Ocb.Text(head, title ?? "", Ocb.TextOf(Ocb.TipCss + headCss), "span");
                if (small != null)
                {
                    TextMeshProUGUI sm = Ocb.Text(head, small, Ocb.TextOf(Ocb.TipCss + headCss + Ocb.TipHeadSmallCss + ChipTipSmallCss), "small");
                    Ocb.AutoMarginLeft(sm);
                }
                Ocb.Flow(head, headCss, null, innerW);
                UiNode headNode = UiNode.Of(head);
                headNode.Place(x, y);
                y += headNode.H + (Ocb.Px(Ocb.Rule(Ocb.TipHeadCss).Layout.MarginBottom) ?? 6f);
            }

            TextStyle ps = Ocb.TextOf(Ocb.TipCss + Ocb.TipPCss + ChipTipPCss);
            ps.RichText = true;
            float pGap = Ocb.Px(Ocb.Rule(ChipTipPPlusPCss).Layout.MarginTop) ?? 4f;
            for (int k = 0; k < rows.Count; k++)
            {
                if (k > 0) y += pGap; // p + p { margin-top: 4px }
                float h = Ocb.TextHeight(rows[k], ps, innerW);
                Label.Make(node, rows[k], ps, x, y, innerW, h, "p");
                y += h;
            }
            nn.H = y + pad.Bottom + bw.Bottom;
            return node;
        }

        /// <summary>The ring round the newest frame (slot 0) as a stance is put down.</summary>
        // js: scenes/battle/hud/stances.js:103
        private void RingNewest(UnitSide side)
        {
            Side s = SideOf(side);
            FrameView v = s.Frames.Count > 0 ? s.Frames[0] : null; // sides[side].el.firstChild
            if (v == null || v.Slot == null || !v.Slot.Root || _env == null) return;
            Rgba color = side == UnitSide.Player ? DsTokens.HpText : DsTokens.Violet;
            _env.Clock.Run(SetRingMs, Ease.Linear, t =>
            {
                if (!v.Slot.Root) return;
                // '0 0 0 4px ' + mix(color, Math.round(90 * (1 - t))), '' at the end
                v.Ring = t < 1f
                    ? new BoxShadow(0f, 0f, 0f, SetRingPx, color.MulAlpha((float)Math.Floor(90.0 * (1.0 - t) + 0.5) / 100f))
                    : (BoxShadow?)null;
                ApplyRing(v);
            }).Forget();
        }

        /// <summary>el.style.boxShadow: the inline ring over the slot's stylesheet box (which has no shadow).</summary>
        private static void ApplyRing(FrameView v)
        {
            BoxGraphic g = v.Slot != null && v.Slot.Root ? v.Slot.Root.GetComponent<BoxGraphic>() : null;
            if (!g) return;
            g.SetShadows(v.Ring.HasValue ? new[] { v.Ring.Value } : null);
        }

        /// <summary>
        /// The enemy list's rows (several enemies): every standing enemy's stances, oldest first, each with its owner's
        /// UnitFrame and swatch angle. A stance new to the list, or stacked again, takes a newer stamp (it goes to the slot);
        /// the rest keep theirs.
        /// </summary>
        // js: scenes/battle/hud/stances.js:119
        private List<Row> MergedRows(DepictionFrame frame, bool first)
        {
            // Each enemy with its index in DepictionFrame.Enemies (#589's EnemyFrame: its panel, stripe direction, swatch).
            var list = new List<(int Unit, EnemyFrame E)>();
            if (frame.Enemies != null && frame.Enemies.Count > 0)
            {
                for (int k = 0; k < frame.Enemies.Count; k++) list.Add((k, frame.Enemies[k]));
            }
            else if (frame.Enemy != null)
            {
                list.Add((0, new EnemyFrame { Unit = frame.Enemy }));
            }
            var standing = new List<(int Unit, EnemyFrame E)>();
            foreach (var x in list) if (x.E != null && x.E.Unit != null && !x.E.Unit.Down) standing.Add(x);
            List<(int Unit, EnemyFrame E)> from = standing.Count > 0 ? standing : list; // all fallen (a won battle): the list fades as it stood
            var rows = new List<Row>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int k = 0; k < from.Count; k++)
            {
                EnemyFrame e = from[k].E;
                UnitFrame u = e?.Unit;
                if (u == null) continue;
                int unit = from[k].Unit;
                float? angle = e.Swatch ? BattleLayout.HatchAngle(e.StripeDirection) : (float?)null;
                if (u.Stances == null) continue;
                foreach (StanceRow r in u.Stances)
                {
                    if (r == null) continue;
                    string id = unit.ToString(CultureInfo.InvariantCulture) + ":" + r.Name;
                    seen.Add(id);
                    _stamps.TryGetValue(id, out Stamp was);
                    if (was == null || r.Count > was.Count) _stamps[id] = new Stamp { Value = first && was == null ? 0 : ++_stampNow, Count = r.Count };
                    else was.Count = r.Count;
                    rows.Add(new Row
                    {
                        Name = r.Name, Count = r.Count, UnitFrame = u, HasAngle = true, Angle = angle,
                        Stamp = _stamps[id].Value, Order = rows.Count,
                    });
                }
            }
            var gone = new List<string>();
            foreach (string id in _stamps.Keys) if (!seen.Contains(id)) gone.Add(id);
            foreach (string id in gone) _stamps.Remove(id);
            rows.Sort((a, b) => a.Stamp != b.Stamp ? a.Stamp.CompareTo(b.Stamp) : a.Order.CompareTo(b.Order));
            return rows;
        }

        // js: scenes/battle/hud/stances.js:141
        private void BindSide(UnitSide side, UnitFrame unit, int unitNo, float? swatchAngle, bool first, List<Row> merged)
        {
            Side s = SideOf(side);
            // rows: { Name, Count, [unitFrame], [angle] } oldest first; `merged` carries the enemies' own.
            List<Row> rows = merged;
            if (rows == null)
            {
                rows = new List<Row>();
                if (unit != null && unit.Stances != null)
                    foreach (StanceRow r in unit.Stances)
                        if (r != null) rows.Add(new Row { Name = r.Name, Count = r.Count });
            }
            float? AngleOf(Row r) => r != null && r.HasAngle ? r.Angle : swatchAngle;
            var key = new System.Text.StringBuilder();
            for (int k = 0; k < rows.Count; k++)
            {
                if (k > 0) key.Append('|');
                key.Append(rows[k].Name).Append('×').Append(rows[k].Count.ToString(CultureInfo.InvariantCulture)).Append('#').Append(Str(AngleOf(rows[k])));
            }
            key.Append('@').Append(Str(swatchAngle));
            if (s.High) key.Append("@high").Append(Ocb.Num(HighLeft(side)));
            s.Unit = unit;
            s.HasArgs = true;
            s.ArgUnit = unit;
            s.ArgUnitNo = unitNo;
            s.ArgSwatch = swatchAngle;
            s.ArgMerged = merged;
            s.RowsCount = rows.Count;
            string keyText = key.ToString();
            if (keyText == s.Key) return;
            // A stance was put down (the list holds more than it did, or a newer stamp leads it): the newest frame lights once.
            int held = 0;
            foreach (Row r in rows) held += r.Count;
            string who = merged != null ? "merged" : unit != null ? "u" + unitNo.ToString(CultureInfo.InvariantCulture) : null;
            int? newest = merged != null && rows.Count > 0 ? rows[rows.Count - 1].Stamp : (int?)null;
            bool grew = !first && s.Held.HasValue && s.Who == who && (held > s.Held.Value || (newest.HasValue && newest.Value > (s.Newest ?? 0)));
            s.Held = held;
            s.Who = who;
            s.Newest = newest;
            s.Key = keyText;
            _tip?.Hide(s);
            // s.el.innerHTML = ''
            if (s.El) Ui.Clear(s.El);
            s.Frames.Clear();
            if (!s.El) return;
            bool foe = side == UnitSide.Enemy;
            // The high place (review 21) holds one frame: the newest, carrying 「+n」 for the others.
            int frames = s.High ? 1 : StanceList.Frames(rows.Count);
            int folded = s.High ? 0 : StanceList.Folded(rows.Count);
            for (int i = 0; i < frames; i++)
            {
                var d = new OcbStanceData { Foe = foe };
                Row row = null;
                List<Row> rest = null;
                if (rows.Count == 0) d.Empty = true;
                else if (folded > 0 && i == frames - 1)
                {
                    d.More = folded;
                    rest = rows.GetRange(0, folded);
                    rest.Reverse();
                }
                else
                {
                    row = rows[rows.Count - 1 - i];
                    d.Name = row.Name;
                    d.Count = row.Count;
                }
                float? angle = row != null ? AngleOf(row) : swatchAngle;
                if (foe && angle.HasValue && !d.Empty) d.SwatchAngle = angle;
                OcbStanceSlot slot = Ocb.StanceSlot(s.El, d);
                var view = new FrameView { Slot = slot };
                // el.classList.add('ocbg-battle-stance'); el.style.position = 'absolute'
                string slotCss = Ocb.StanceSlotCss(d.Foe, d.Empty, false) + StanceBattleCss;
                // .ocbg-battle-stance > span { white-space: nowrap; overflow: hidden; text-overflow: ellipsis } (min-width 0 as a scroll container)
                if (slot.NameLabel)
                {
                    Label.Apply(slot.NameLabel, Ocb.TextOf(slotCss + StanceSpanCss));
                    FlexBox.Attrs(slot.NameLabel.rectTransform).MinMain = 0f;
                }
                if (slot.CountLabel)
                {
                    // the 「×n」 span is a `> span` too: overflow hidden makes its min-width 0, so it shrinks with the name
                    Label.Apply(slot.CountLabel, Ocb.TextOf(slotCss + StanceSpanCss + Ocb.StanceXCss));
                    FlexBox.Attrs(slot.CountLabel.rectTransform).MinMain = 0f;
                }
                Vector2 at = FrameAt(side, i);
                slot.Node.Place(at.x, at.y);
                if (s.High && rows.Count > 1)
                {
                    // The others, folded on the newest's frame; its hover lists every one (oldest first) after the newest's own panel.
                    TextMeshProUGUI more = Ocb.Text(slot.Root, "+" + (rows.Count - 1).ToString(CultureInfo.InvariantCulture),
                        Ocb.TextOf(slotCss + Ocb.MonoCss + StanceMoreCss), "ocbg-battle-stance-more");
                    FlexAttrs ma = FlexBox.Attrs(more.rectTransform);
                    if (slot.CountLabel) ma.MarginL = StanceMoreAfterXMargin; // .ocb-x + .ocbg-battle-stance-more { margin-left: 6px }
                    else ma.AutoMarginL = true;
                    rest = new List<Row>(rows);
                    rest.Reverse();
                }
                Ocb.Flow(slot.Root, slotCss);
                if (!d.Empty) Listen(side, s, i, view, row, rest);
                s.Frames.Add(view);
            }
            if (grew) RingNewest(side);
        }

        /// <summary>A frame's listeners (.ocbg-battle-hit): mouseenter / mouseleave (.is-hover and the 説明), contextmenu (the pinned 説明).</summary>
        // js: scenes/battle/hud/stances.js:191
        private void Listen(UnitSide side, Side s, int i, FrameView view, Row row, List<Row> rest)
        {
            OcbStanceSlot slot = view.Slot;
            BattleHitTag.Mark(slot.Root);
            BoxGraphic box = slot.Root.GetComponent<BoxGraphic>();
            ClickArea area = ClickArea.On(box ? (Component)box : slot.Root);
            UnitFrame UnitNow() => (row != null ? row.UnitFrame : null) ?? s.Unit;
            // js: scenes/battle/hud/stances.js:193
            area.Enter += _ =>
            {
                SetHover(view, true);
                ShowTip(side, i, UnitNow(), row, rest, false);
            };
            // js: scenes/battle/hud/stances.js:194
            area.Exit += _ =>
            {
                SetHover(view, false);
                _tip?.Hide(s);
            };
            // js: scenes/battle/hud/stances.js:195 — contextmenu fires on the right button's release (Windows), on the frame under it.
            area.Up += e =>
            {
                if (e == null || e.button != PointerEventData.InputButton.Right || !area.Hovered) return;
                _env?.MarkContextPinned(); // e.preventDefault(); e.ocbgPinned = true
                ShowTip(side, i, UnitNow(), row, rest, true);
            };
        }

        /// <summary>el.classList.toggle('is-hover'): the slot's ground rgba(40,44,58,.92); the inline ring stays.</summary>
        private static void SetHover(FrameView view, bool on)
        {
            if (view.Slot == null || !view.Slot.Root) return;
            view.Slot.Hover = on;
            if (view.Ring.HasValue) ApplyRing(view);
        }

        /// <summary>The list's frames as they would stand at rest (y 470 up), { x, y, w, h } — what the figures are tested against.</summary>
        // js: scenes/battle/hud/stances.js:203
        private StageBox RestBox(UnitSide side)
        {
            Side s = SideOf(side);
            int n = Math.Max(1, StanceList.Frames(s.RowsCount));
            float top = StanceList.TopOf(n - 1);
            return new StageBox(StanceList.LeftOf(side), top, StanceList.Width, StanceList.SlotTop + StanceList.RowHeight - top);
        }

        /// <summary>Moves `side`'s list to its high place or back (a short fade in at the new place, on real time).</summary>
        // js: scenes/battle/hud/stances.js:210
        private void SetHigh(UnitSide side, bool high)
        {
            Side s = SideOf(side);
            if (high == s.High) return;
            s.High = high;
            _tip?.Hide(s);
            s.Key = null;
            if (s.HasArgs) BindSide(side, s.ArgUnit, s.ArgUnitNo, s.ArgSwatch, true, s.ArgMerged);
            if (!s.El || _env == null) return;
            // On real time, as the drawers' slide that moves the figures (the journal holds the scene clock still).
            UiNode n = UiNode.Of(s.El);
            n.Opacity = 0f;
            int mine = ++s.FadeToken;
            float t0 = _env.Real.Now;
            void Step()
            {
                if (mine != s.FadeToken || !s.El) return;
                float t = Math.Min(1f, (_env.Real.Now - t0) / SwitchFadeMs);
                n.Opacity = t >= 1f ? 1f : Fixed(1f - (1f - t) * (1f - t), 3);
                if (t < 1f) _env.Real.Wait(FadeStepMs).Then(Step).Forget();
            }
            Step();
        }

        // ---- api (stances.js:231) -------------------------------------------------------------------------------------------

        /// <summary>bind(frame, first) = StanceListView.Bind: both sides' lists of the frame (several enemies: every standing enemy's merged); a source that keeps none hides them.</summary>
        // js: scenes/battle/hud/stances.js:233
        public void Bind(DepictionFrame frame, bool first)
        {
            bool shown = frame != null && frame.StanceListsShown;
            _hidden = !shown;
            if (_host) UiNode.Of(_host).Visible = shown;
            if (!shown)
            {
                if (_tip != null)
                {
                    _tip.Hide(_player);
                    _tip.Hide(_enemy);
                }
                return;
            }
            BindSide(UnitSide.Player, frame.Player, -1, null, first, null);
            // The frame's enemy is the focus enemy's share (#589): its swatch, when the battle draws them.
            int focus = frame.Enemies != null && frame.Enemy != null ? frame.Enemies.FindIndex(e => e != null && e.Unit == frame.Enemy) : -1;
            EnemyFrame share = focus >= 0 ? frame.Enemies[focus] : null;
            float? swatch = share != null && share.Swatch ? BattleLayout.HatchAngle(share.StripeDirection) : (float?)null;
            bool several = frame.Enemies != null && frame.Enemies.Count > 1;
            BindSide(UnitSide.Enemy, frame.Enemy, focus, swatch, first, several ? MergedRows(frame, first) : null);
        }

        /// <summary>boxOf(side): `side`'s list over every frame it shows (the empty one too), at rest or high; null when hidden.</summary>
        // js: scenes/battle/hud/stances.js:243
        public StageBox? BoxOf(UnitSide side)
        {
            if (_hidden) return null;
            Side s = SideOf(side);
            if (s.High) return new StageBox(HighLeft(side), HighTop(), StanceList.Width, StanceList.RowHeight);
            int n = Math.Max(1, s.Frames.Count);
            float top = StanceList.TopOf(n - 1);
            return new StageBox(StanceList.LeftOf(side), top, StanceList.Width, StanceList.SlotTop + StanceList.RowHeight - top);
        }

        /// <summary>slotOf(side) (§5.11 順 1): the frame a stance just put down goes to — the slot at y 470, or the high place's one frame; null when hidden.</summary>
        // js: scenes/battle/hud/stances.js:252
        public StageBox? SlotOf(UnitSide side)
        {
            if (_hidden) return null;
            Vector2 at = FrameAt(side, 0);
            return new StageBox(at.x, at.y, StanceList.Width, StanceList.RowHeight);
        }

        /// <summary>keepClear(arts) (review 21): a list any standing figure's art comes under (4 px kept) goes to its high place; back when none does.</summary>
        // js: scenes/battle/hud/stances.js:262
        public void KeepClear(IReadOnlyList<ArtBox> arts)
        {
            if (_hidden) return;
            foreach (UnitSide side in BothSides)
            {
                StageBox b = RestBox(side);
                bool under = false;
                if (arts != null)
                    foreach (ArtBox a in arts)
                        if (a.X0 < b.X + b.W + ClearPad && a.X1 > b.X - ClearPad && a.Y0 < b.Y + b.H + ClearPad && a.Y1 > b.Y - ClearPad)
                        {
                            under = true;
                            break;
                        }
                SetHigh(side, under);
            }
        }

        /// <summary>isHigh(side).</summary>
        // js: scenes/battle/hud/stances.js:270
        public bool IsHigh(UnitSide side) => SideOf(side).High;

        /// <summary>elOf(side): the element of `side`'s list (the hud fades it at the battle's end).</summary>
        // js: scenes/battle/hud/stances.js:272
        public RectTransform ElOf(UnitSide side) => SideOf(side).El;

        /// <summary>destroy().</summary>
        // js: scenes/battle/hud/stances.js:276
        public void Destroy()
        {
            if (_host) Ui.Remove(_host);
            _host = null;
        }
    }
}
