// The battle's floor of cells (FloorView, docs/BATTLE_SCENE.md §6b, battle-views §2.2): the tiles (FloorSpec, the layer's
// tile fill), the reach light, the enemies' aimed cells hatched in their own directions (dense on the boundary), the move
// frames (in front of another enemy's red frame), the aim reveal / fade / retire, the gap chip with its brackets and its
// recount, and the wall edge's flash. In layer.field (it slides with the arena). Port of scenes/battle/field/floor.js.
//
// The floor under the figures: a port of unity-project-kit/Assets/View/Depiction/FloorView.cs (battle-visual-v1 §4.2).
// The line of cells; the cells the omen aims at in red stripes (6 px apart on those that meet the boundary its action
// reads) with a red frame and an 8 px light; a violet dashed frame on the cell a moving omen takes the enemy to; the cells
// a hovered or held card reaches in amber; and the one gap number between the player and the enemy. Every cell comes
// from the script (DepictionFrame.Floor, CardFace.ReachCells); FloorSpec holds the arithmetic. Looks: OCB.FloorTile and
// the DistanceChip's parts (Ocb.FloorTile; the chip's parts are built here, as the JS builds them, so the number can be
// counted again in place).
//
// Stacking (field.css:26): the gap tag stands at z-index 20 in the field layer's stacking context (the floor's root is
// no stacking context), over the figures field.js paints at z 2–9. In uGUI a child cannot rise over its parent's
// siblings, so the tag is a sibling of the floor's root in layer.field with UiNode.Z = 20; the root's display (no cells)
// and its removal take the tag with them.
using System;
using System.Collections.Generic;
using System.Globalization;
using Depiction;
using TMPro;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    /// <summary>bind's o.reveal: { unit, ms } — an omen shown for enemy `unit` draws that enemy's new cells left to right, `ms` a cell.</summary>
    // js: scenes/battle/field/floor.js:216
    public sealed class FloorReveal
    {
        /// <summary>reveal.unit: the enemy the omen is for (−1: the frame's one enemy, JS undefined).</summary>
        public int Unit = -1;
        /// <summary>reveal.ms: per cell (null = AIM_DRAW_MS, 30; 0 = at once).</summary>
        public float? Ms;
    }

    /// <summary>floor.bind's o: { reveal, unread }, and the frame's enemies.</summary>
    // js: scenes/battle/field/floor.js:215
    public sealed class FloorBindOpts
    {
        /// <summary>
        /// The frame's enemies (#589's DepictionFrame.Enemies: each one's cells, its own N and the cells its shown omen aims
        /// at), which the JS writer kept on the floor (FloorFrame.Enemies). Null or empty: the floor's one enemy.
        /// </summary>
        public IReadOnlyList<EnemyFrame> Enemies;
        public FloorReveal Reveal;
        /// <summary>unread(unit) → the enemy is read to 開示度 0 (its cells are not drawn); −1 asks for the frame's one omen.</summary>
        public Func<int, bool> Unread;
    }

    /// <summary>createFloor(env, tile) → the floor's api (floor.js:206).</summary>
    // js: scenes/battle/field/floor.js:38
    public sealed class FieldFloor
    {
        // ---- floor.js constants -----------------------------------------------------------------------------

        /// <summary>TOP: FloorView: the floor's top (y 560: the feet at 570 less FeetBelowTop).</summary>
        // js: scenes/battle/field/floor.js:13
        private const float Top = 560f;
        /// <summary>TAG_TOP: §4.2 間合いの数字: the tag 3 px below the floor's top, 34 px tall.</summary>
        // js: scenes/battle/field/floor.js:15
        private const float TagTop = 3f;
        /// <summary>RECOUNT_TRAVEL: battle_ui_ux_v2 §5.7 順 2: how far the old number rises out and the new one rises in from.</summary>
        // js: scenes/battle/field/floor.js:17
        private const float RecountTravel = 14f;
        /// <summary>AIM_ANGLE: §4.2: the stripes' direction for the enemy the frame draws (the first enemy: 45°).</summary>
        // js: scenes/battle/field/floor.js:19
        private const int AimAngle = 45;
        /// <summary>UNFOCUSED_ALPHA: battle_ui_ux_v2 §2.2.2: while one enemy's omen badge is hovered, the other enemies' stripes stand at this.</summary>
        // js: scenes/battle/field/floor.js:21
        private const float UnfocusedAlpha = 0.2f;
        /// <summary>REACH_IN_MS / REACH_OUT_MS: battle_ui_ux_v2 §5.2: the reach light comes up over 150 ms (out) and goes over 120 ms (linear).</summary>
        // js: scenes/battle/field/floor.js:23
        private const float ReachInMs = 150f;
        private const float ReachOutMs = 120f;
        /// <summary>
        /// AIM_DRAW_MS / AIM_FADE_MS: battle_ui_ux_v2 §5.8 (review 16; Unity redraws the stripes with each frame): as an omen is
        /// shown, its aimed cells' stripes are drawn left to right, AIM_DRAW_MS a cell; after the action has run, they fade over AIM_FADE_MS.
        /// </summary>
        // js: scenes/battle/field/floor.js:29
        private const float AimDrawMs = 30f;
        private const float AimFadeMs = 120f;
        /// <summary>WALL_EDGE: §5.7 順 7 壁: the lit end of the floor — 10 px wide, from 80 px over the floor to its bottom; it fades over `fade` after its 80 ms.</summary>
        // js: scenes/battle/field/floor.js:32
        private const float WallEdgeW = 10f;
        private const float WallEdgeUp = 80f;
        private const float WallEdgeFade = 160f;

        // ---- field.css (the floor's rules, verbatim) -----------------------------------------------------------

        /// <summary>field.css:18 .ocbg-battle .ocbg-battle-floor</summary>
        private const string FloorCss = "position: absolute; left: 0; top: 0;";
        /// <summary>field.css:21 .ocbg-battle .ocbg-battle-tile .ocb-tile-reach-line</summary>
        private const string ReachLineCss = "left: 4px; right: 4px;";
        /// <summary>field.css:24 .ocbg-battle .ocbg-battle-tile .ocb-tile-move.ocbg-battle-move-over</summary>
        private const string MoveOverCss = "inset: 4px; box-shadow: 0 0 0 2px var(--panel-solid), inset 0 0 0 2px var(--panel-solid);";
        /// <summary>field.css:26 .ocbg-battle .ocbg-battle-gap</summary>
        private const string GapCss = "z-index: 20;";
        /// <summary>field.css:27 .ocbg-battle .ocbg-battle-gap .ocb-dist-chip</summary>
        private const string GapChipCss = "white-space: nowrap; box-sizing: border-box; justify-content: center;";
        /// <summary>field.css:28 .ocbg-battle .ocbg-battle-gap-num</summary>
        private const string GapNumCss = "display: inline-block; min-width: 18px; text-align: center;";
        /// <summary>field.css:29 .ocbg-battle .ocbg-battle-gap-arm</summary>
        private const string GapArmCss = "position: absolute; top: 9px; height: 17px; display: none;";
        /// <summary>field.css:30 .ocbg-battle .ocbg-battle-gap-arm::after</summary>
        private const string GapArmAfterCss = "content: ''; position: absolute; left: 0; right: 0; top: 8px; height: 1px; background: color-mix(in srgb, var(--ink) 55%, transparent);";
        /// <summary>field.css:31 .ocbg-battle .ocbg-battle-gap-arm.is-left</summary>
        private const string GapArmLeftCss = "left: 8px; right: 50%; border-left: 1px solid color-mix(in srgb, var(--ink) 55%, transparent);";
        /// <summary>field.css:32 .ocbg-battle .ocbg-battle-gap-arm.is-right</summary>
        private const string GapArmRightCss = "right: 8px; left: 50%; border-right: 1px solid color-mix(in srgb, var(--ink) 55%, transparent);";
        /// <summary>field.css:111 .ocbg-battle .ocbg-battle-wall-edge</summary>
        private const string WallEdgeCss = "position: absolute; border-radius: 5px; background: linear-gradient(180deg, transparent, var(--ink) 55%, var(--ink)); box-shadow: 0 0 14px 4px color-mix(in srgb, var(--ink) 70%, transparent); pointer-events: none;";

        /// <summary>The chip's flex gap and its icon (bundle.css:78 gap: 6px; OCB.icon('range', 22)).</summary>
        private const float ChipGap = 6f;
        private const float ChipIcon = 22f;
        /// <summary>The gap number's line box (font: 700 28px/1) and its min-width (field.css:28).</summary>
        private const float NumLine = 28f;
        private const float NumMinW = 18f;
        /// <summary>.ocb-dist / .ocb-dist-chip height (bundle.css:77-78).</summary>
        private const float DistH = 34f;

        private static readonly List<OcbThreat> NoThreats = new List<OcbThreat>();
        private static readonly List<int> NoCells = new List<int>();

        // ---- state ---------------------------------------------------------------------------------------

        /// <summary>One tile: { el, key, lit, reachA, reachToken, threat, fade } (floor.js:101).</summary>
        private sealed class TileState
        {
            public OcbFloorTile El;
            public string Key = "";
            public bool Lit;
            public float ReachA;
            public int ReachToken;
            public bool Threat;
            public int Fade;
        }

        /// <summary>gapState: { gap, pc, ps, ec }.</summary>
        private struct GapState
        {
            public int Gap;
            public int Pc;
            public int Ps;
            public int Ec;
        }

        /// <summary>gapPlace's answer: { left, width, spanW, broken, chipDx, run }.</summary>
        private struct GapPlacement
        {
            public float Left;
            public float Width;
            public float SpanW;
            public bool Broken;
            public float ChipDx;
            public float Run;
        }

        /// <summary>One threat on a cell: { angle, boundary, faded }.</summary>
        private struct Threat
        {
            public int Angle;
            public bool Boundary;
            public bool Faded;
        }

        /// <summary>
        /// One enemy on the floor (the JS writer's FloorEnemy): enemy `Unit` of the frame's EnemyFrame list (#589), its stripe
        /// direction, its cells, its own N (−1 once it has fallen in the core), whether it is down, and its shown omen's cells.
        /// </summary>
        private sealed class EnemyCells
        {
            public int Unit;
            public int Hatch;
            public int Cell;
            public int Size = 1;
            public int Gap = -1;
            public bool Down;
            public List<int> AimCells = NoCells;
            public List<int> DenseCells = NoCells;
            public List<int> MoveCells = NoCells;
        }

        /// <summary>The frame's enemies as the floor reads them: enemy k is the k-th share.</summary>
        private static List<EnemyCells> CellsOf(IReadOnlyList<EnemyFrame> enemies)
        {
            var list = new List<EnemyCells>(enemies != null ? enemies.Count : 0);
            if (enemies == null) return list;
            for (int k = 0; k < enemies.Count; k++)
            {
                EnemyFrame e = enemies[k];
                if (e == null) { list.Add(null); continue; }
                list.Add(new EnemyCells
                {
                    Unit = k,
                    Hatch = e.StripeDirection,
                    Cell = e.Cell,
                    Size = e.Size,
                    Gap = e.Gap,
                    Down = e.Unit != null && e.Unit.Down,
                    AimCells = e.AimCells ?? NoCells,
                    DenseCells = e.DenseCells ?? NoCells,
                    MoveCells = e.MoveCells ?? NoCells,
                });
            }
            return list;
        }

        /// <summary>An enemy as bind reads it (an EnemyCells entry, or the one enemy of a frame with no enemy list: Unit −1).</summary>
        private sealed class Foe
        {
            public int Unit = -1;
            public int Hatch;
            public List<int> AimCells = NoCells;
            public List<int> DenseCells = NoCells;
            public List<int> MoveCells = NoCells;
        }

        private readonly BattleViewEnv _env;
        private readonly BattleClock _clock;
        private RectTransform _root;
        private UiNode _rootNode;
        /// <summary>tileFill: rgba(tile) or null for the DS default.</summary>
        private readonly string _tileFill;

        private int _cells;
        private readonly List<TileState> _tiles = new List<TileState>();
        private List<int> _reach = new List<int>();
        private GapState? _gapState;
        private int _recount;
        private FloorFrame _lastFloor;
        /// <summary>The enemies the last bind drew from (after the reveal kept the others' cells), beside _lastFloor.</summary>
        private List<EnemyCells> _lastEnemies = new List<EnemyCells>();
        /// <summary>focusUnit: the enemy whose aimed cells alone stay lit (its omen badge is hovered), or null.</summary>
        private int? _focusUnit;
        /// <summary>gapHidden: the battle is over: the gap number is put away (review fix: it measured to a fallen enemy's empty cell).</summary>
        private bool _gapHidden;
        /// <summary>
        /// aimAlpha / aimRetired: the battle is over (review fix: on a loss with enemies standing, their aimed cells stayed red under
        /// the result card while their omen badges faded): the omens' floor half — the aimed, dense and move cells — retires with
        /// the badges. `aimAlpha` runs 1 → 0, then every cell is cleared and stays clear (`aimRetired`).
        /// </summary>
        private float _aimAlpha = 1f;
        private bool _aimRetired;
        private int _aimToken;
        /// <summary>
        /// retiredUnits / soloRetired: battle_ui_ux_v2 §5.15 順 5 (review fix: a fallen enemy's aimed cells stayed striped through
        /// its whole fall, a second after its badge had gone): the enemies whose omen went with their fall (retireUnit). Their
        /// aimed, dense and move cells leave the floor as the fall starts (the 120 ms fade of §5.8), and stay off whatever a later
        /// frame says. `soloRetired`: the same for the one enemy of a frame that carries no enemy list.
        /// </summary>
        private readonly HashSet<int> _retiredUnits = new HashSet<int>();
        private bool _soloRetired;
        /// <summary>
        /// unreadOf: battle_ui_ux_v2 §7 / §5.8 提示 「斜線…（開示度 1 以上）」 (review 21): which enemies are read to 0 — their omen
        /// shows the kind alone, so their aimed, dense and move cells are not drawn. A function (unit → bool; the one enemy of a
        /// frame with no enemy list asks with −1), kept from the last bind that gave one.
        /// </summary>
        private Func<int, bool> _unreadOf = _ => false;

        // ---- the gap tag: DS DistanceChip's parts, so it can be counted again in place ----
        private RectTransform _gapEl;
        private UiNode _gapNode;
        private RectTransform _armL;
        private RectTransform _armR;
        private RectTransform _armLAfter;
        private RectTransform _armRAfter;
        private RectTransform _chip;
        private UiNode _chipNode;
        private PathGraphic _icon;
        private TextMeshProUGUI _num;
        private UiNode _numNode;
        private TextStyle _numStyle;
        private string _numText = "0";
        /// <summary>chip.style.minWidth = TagAtZero (at gap ≤ 0).</summary>
        private bool _chipMinW;
        /// <summary>The chip's border-box width as laid out (chip.offsetWidth).</summary>
        private float _chipW;
        /// <summary>The text and min-width the chip was last laid out for.</summary>
        private string _laidText;
        private bool _laidMinW;
        /// <summary>gapEl.style.display: '' (true) or 'none'; the root's display goes with it.</summary>
        private bool _gapDisplay = true;
        private bool _rootDisplay = true;
        /// <summary>gapEl has-brackets.</summary>
        private bool _hasBrackets;
        /// <summary>gapEl.style.left / width and chip.style.translate as written (toFixed(1)); null = '' (never written).</summary>
        private float? _gapLeftCss;
        private float? _gapWidthCss;
        private float _chipDxCss;
        private bool _destroyed;

        private FieldFloor(BattleViewEnv env, global::Backdrop.Rgba? tile)
        {
            _env = env;
            _clock = env?.Clock;
            // js: scenes/battle/field/floor.js:42 (const tileFill = tile ? rgba(tile) : null; rgba is art.js's)
            _tileFill = tile.HasValue ? FieldArt.RgbaCss(tile.Value) : null;
            RectTransform layer = env != null ? env.Layer.Field : null;
            if (!layer) return;
            // js: scenes/battle/field/floor.js:40
            _root = Ocb.El(layer, "ocbg-battle-floor", FloorCss);
            _rootNode = UiNode.Of(_root);
            BuildGap(layer);
        }

        /// <summary>createFloor(env, tile): the floor's root in layer.field; `tile` the layer's tile fill (null: the DS default).</summary>
        // js: scenes/battle/field/floor.js:38
        public static FieldFloor Create(BattleViewEnv env, global::Backdrop.Rgba? tile) => new FieldFloor(env, tile);

        /// <summary>The gap tag (gapEl): .ocb-dist with its two arms and the chip (the `range` icon and the number).</summary>
        // js: scenes/battle/field/floor.js:78
        private void BuildGap(RectTransform layer)
        {
            _gapEl = Ocb.El(layer, "ocb-dist ocbg-battle-gap", Ocb.DistCss + " " + GapCss);
            _gapNode = UiNode.Of(_gapEl);
            _armL = Ocb.El(_gapEl, "ocbg-battle-gap-arm is-left", GapArmCss + " " + GapArmLeftCss);
            _armLAfter = Ocb.El(_armL, "::after", GapArmAfterCss);
            _armR = Ocb.El(_gapEl, "ocbg-battle-gap-arm is-right", GapArmCss + " " + GapArmRightCss);
            _armRAfter = Ocb.El(_armR, "::after", GapArmAfterCss);
            UiNode.Of(_armL).Visible = false;
            UiNode.Of(_armR).Visible = false;
            string chipCss = Ocb.DistChipCss + " " + GapChipCss;
            _chip = Ocb.El(_gapEl, "ocb-dist-chip", chipCss);
            _chipNode = UiNode.Of(_chip);
            _icon = Ocb.Icon(_chip, "range", ChipIcon, DsTokens.Ink);
            _numStyle = Ocb.TextOf(Ocb.DistCss + " " + chipCss + " " + GapNumCss);
            _num = Label.Make(_chip, _numText, _numStyle, 0f, 0f, NumMinW, NumLine, "ocbg-battle-gap-num");
            _numNode = UiNode.Of(_num.rectTransform);
            LayoutChip();
            // gapEl.style.top = TOP + TAG_TOP; left / width until the first placeGap: 0 and the chip's own (shrink to fit)
            _gapNode.Place(0f, Top + TagTop, _chipW, DistH);
            LayoutGapParts(_chipW, 0f);
            _gapNode.Z = 20;
        }

        // ---- api ---------------------------------------------------------------------------------------------

        /// <summary>cells: the line's cells (0 without a floor).</summary>
        // js: scenes/battle/field/floor.js:207
        public int Cells => _cells;

        /// <summary>shown: cells &gt; 0.</summary>
        // js: scenes/battle/field/floor.js:208
        public bool Shown => _cells > 0;

        /// <summary>build(n): the tiles for a line of n cells (OCB.FloorTile each, in the layer's tile fill).</summary>
        // js: scenes/battle/field/floor.js:89
        private void Build(int n)
        {
            foreach (TileState t in _tiles) t.El?.Remove();
            _tiles.Clear();
            _cells = n;
            float w = n > 0 ? FloorSpec.TileWidth(n) : 0f;
            for (int c = 1; c <= n; c++)
            {
                OcbFloorTile el = Ocb.FloorTile(_root, w);
                el.Root.name = "ocb-tile ocbg-battle-tile";
                el.Node.Place(FloorSpec.TileLeft(n, c), Top);
                if (_tileFill != null && el.Box) el.Box.BackgroundColor = CssColor.Parse(_tileFill);
                // root.insertBefore(el, gapEl): the tiles stand before anything else the root holds (the wall edges)
                el.Root.SetSiblingIndex(c - 1);
                // field.css:21 (ReachLineCss): the reach bar 4 px in from the tile's padding box on both sides
                if (el.Box && el.ReachLine != null)
                {
                    Insets bw = el.Box.Style.BorderWidths;
                    float pw = Math.Max(0f, w - bw.Horizontal);
                    el.ReachLine.Node.Place(bw.Left + 4f, bw.Top, Math.Max(0f, pw - 8f), el.ReachLine.H);
                }
                var t = new TileState { El = el };
                DrawReach(t);
                _tiles.Add(t);
            }
        }

        // js: scenes/battle/field/floor.js:107
        private static void DrawReach(TileState t)
        {
            if (t.El == null || !t.El.Alive) return;
            float a = t.ReachA > 0.001f ? Round3(t.ReachA) : 0f;
            t.El.ReachOpacity = a;
        }

        /// <summary>
        /// applyReach(): §4.2 届くマスの光, battle_ui_ux_v2 §5.2 (review 16; Unity switches it at once): a cell's light comes up over
        /// REACH_IN_MS (out) with the target frame and goes over REACH_OUT_MS (linear) when the card is let go or put back.
        /// </summary>
        // js: scenes/battle/field/floor.js:116
        private void ApplyReach()
        {
            for (int i = 0; i < _tiles.Count; i++)
            {
                TileState t = _tiles[i];
                bool lit = _reach.IndexOf(i + 1) >= 0;
                if (lit == t.Lit) continue;
                t.Lit = lit;
                int mine = ++t.ReachToken;
                float from = t.ReachA;
                float to = lit ? 1f : 0f;
                _clock.Run(lit ? ReachInMs : ReachOutMs, lit ? Ease.Out : Ease.Linear, k =>
                {
                    if (mine != t.ReachToken) return;
                    t.ReachA = from + (to - from) * k;
                    DrawReach(t);
                }).Forget();
            }
        }

        /// <summary>
        /// gapPlace(g): the tag's place for a gap: { left, width, spanW, broken } (FloorView.SetGap with the DS chip's own width).
        /// Several enemies (review fix, battle_ui_ux_v2 §2.2 「あいだにある空きマスの上」): when another standing enemy stands
        /// between the player and the enemy the number is read to (a far one chosen with Tab or framed by a held card), the cells
        /// between are not all empty, and the middle of the whole span fell under that enemy's feet, reading as its own N. The tag
        /// then stands over the run of empty cells in the span nearest the chosen enemy (with no bracket: the span is broken).
        /// Review 17: only the run right before the chosen enemy was looked at, so with the front enemy on that cell the tag fell
        /// to the border between the two enemies' feet and read as the distance between them; the empty cell nearer the hero now
        /// carries it. With no empty cell in the span at all, it stands on the chosen enemy's near edge.
        /// </summary>
        // js: scenes/battle/field/floor.js:143
        private GapPlacement GapPlace(GapState g)
        {
            FloorSpec.GapSpan(_cells, g.Pc, g.Ps, g.Ec, out float spanFrom, out float spanTo);
            float from = spanFrom;
            float to = spanTo;
            bool broken = false;
            int first = g.Pc + g.Ps;
            int last = g.Ec - 1;
            if (last >= first && _lastFloor != null && _lastEnemies.Count > 1)
            {
                var taken = new HashSet<int>();
                foreach (EnemyCells e in _lastEnemies)
                {
                    // Review fix: an enemy whose fall is still to play (HP 0, not yet Down) has already left its cells in the core —
                    // its Gap is −1 and N is read past it (port notes) — so its cell counts as empty, as N counts it.
                    if (e == null || e.Down || e.Gap < 0 || e.Cell == g.Ec) continue;
                    for (int c = e.Cell; c < e.Cell + Math.Max(1, e.Size); c++) taken.Add(c);
                }
                bool blocked = false;
                for (int c = first; c <= last; c++) if (taken.Contains(c)) blocked = true;
                if (blocked)
                {
                    broken = true;
                    float w = FloorSpec.CellWidth(_cells);
                    // The empty cell of the span nearest the chosen enemy, and the run of empty cells it ends.
                    int end = last;
                    while (end >= first && taken.Contains(end)) end--;
                    if (end < first)
                    {
                        from = to = FloorSpec.Left + (g.Ec - 1) * w;
                    }
                    else
                    {
                        int start = end;
                        while (start - 1 >= first && !taken.Contains(start - 1)) start--;
                        from = FloorSpec.Left + (start - 1) * w; // the left edge of cell `start`
                        to = FloorSpec.Left + end * w;           // the right edge of cell `end`
                    }
                }
            }
            float middle = (from + to) / 2f;
            float spanW = Math.Max(0f, to - from);
            float width = g.Gap <= 0 ? FloorSpec.TagAtZero : Math.Max(FloorSpec.TagAtZero, spanW);
            if (!broken) return new GapPlacement { Left = middle - width / 2f, Width = width, SpanW = spanW, Broken = false, ChipDx = 0f };
            // Review 19 (a Tab at rest moved the N from the front enemy to the back one, and the tag stood over the same empty
            // cells both times): the brackets run over the whole span the number counts — from the hero's cells to the chosen
            // enemy's, past the enemy standing between — and the plate sits over the empty cells in it (chipDx from the middle).
            float fullLeft = Math.Min(spanFrom, middle - width / 2f);
            float fullRight = Math.Max(spanTo, middle + width / 2f);
            return new GapPlacement
            {
                Left = fullLeft,
                Width = fullRight - fullLeft,
                SpanW = fullRight - fullLeft,
                Broken = true,
                ChipDx = middle - (fullLeft + fullRight) / 2f,
                Run = spanW,
            };
        }

        // js: scenes/battle/field/floor.js:187
        private GapPlacement? PlaceGap(GapState? gs, bool withBrackets = true)
        {
            if (_cells == 0 || gs == null || _gapHidden || !_gapEl)
            {
                SetGapDisplay(false);
                return null;
            }
            GapState g = gs.Value;
            SetGapDisplay(true);
            // z-index 20 (field.css:26) is the highest in layer.field: the tag stays over whatever the field put in the layer after it
            Transform layer = _gapEl.parent;
            if (layer && _gapEl.GetSiblingIndex() != layer.childCount - 1) _gapEl.SetAsLastSibling();
            GapPlacement p = GapPlace(g);
            _gapLeftCss = Round1(p.Left);
            _gapWidthCss = Round1(p.Width);
            _chipDxCss = p.ChipDx != 0f ? Round1(p.ChipDx) : 0f;
            // FloorView.SetGap (review fix): at gap 0 the plate itself is TagAtZero (100) wide over the two cells' border,
            // not only its transparent box (the DS chip kept its content width, 68).
            _chipMinW = g.Gap <= 0;
            SetNumText(g.Gap.ToString(CultureInfo.InvariantCulture));
            LayoutGapParts(_gapWidthCss.Value, _chipDxCss);
            _gapNode.X = _gapLeftCss.Value;
            // FloorView: the brackets only when there is room for them beside the tag (over a broken span, always: they say
            // which enemy the number is read to).
            bool room = g.Gap > 0 && (p.Broken || p.SpanW > _chipW + 8f);
            SetBrackets(withBrackets && room);
            // gapEl.classList.toggle('is-broken', p.broken): a state class with no look of its own in field.css.
            return p;
        }

        /// <summary>The number's text (num.textContent) and the chip laid out again around it.</summary>
        private void SetNumText(string text)
        {
            if (!_num) return;
            if (text != _numText)
            {
                _numText = text ?? "";
                Label.SetText(_num, _numText);
            }
            // laid out again only when the text or the min-width changed (the recount writes the text every frame)
            if (_laidText == _numText && _laidMinW == _chipMinW) return;
            LayoutChip();
        }

        /// <summary>
        /// The chip's flex row as the browser lays it out (bundle.css:78 + field.css:27-28): border 1, padding 0 10, the
        /// `range` icon 22, gap 6, the number (inline-block, min-width 18, centred); min-width 100 at gap ≤ 0 with the row
        /// centred (justify-content: center); both items centred on the 34 px line (align-items: center).
        /// </summary>
        private void LayoutChip()
        {
            if (!_chip) return;
            _laidText = _numText;
            _laidMinW = _chipMinW;
            BoxGraphic box = _chip.GetComponent<BoxGraphic>();
            Insets bw = box ? box.Style.BorderWidths : Insets.All(1f);
            Insets pad = box ? box.Style.Padding : new Insets(0f, 10f, 0f, 10f);
            float numW = Math.Max(NumMinW, Ocb.TextWidth(_numText, _numStyle));
            float content = ChipIcon + ChipGap + numW;
            float natural = bw.Horizontal + pad.Horizontal + content;
            _chipW = _chipMinW ? Math.Max(natural, FloorSpec.TagAtZero) : natural;
            float inner = _chipW - bw.Horizontal - pad.Horizontal;
            float x0 = bw.Left + pad.Left + (inner - content) / 2f;
            float lineH = DistH - bw.Vertical;
            _chipNode.W = _chipW;
            _chipNode.H = DistH;
            if (_icon) Ui.Place(_icon.rectTransform, x0, bw.Top + (lineH - ChipIcon) / 2f);
            _numNode.Place(x0 + ChipIcon + ChipGap, bw.Top + (lineH - NumLine) / 2f, numW, NumLine);
        }

        /// <summary>
        /// gapEl's width with its children: the chip centred in it (.ocb-dist: flex, centred) and slid by `dx` (chip.style.translate),
        /// the arms from 8 px in to the middle (field.css:29-32: the end tick is the arm's own 1 px border, the line its ::after at 8 px).
        /// </summary>
        private void LayoutGapParts(float width, float dx)
        {
            if (!_gapEl) return;
            _gapNode.Y = Top + TagTop;
            _gapNode.W = width;
            _gapNode.H = DistH;
            _chipNode.X = (width - _chipW) / 2f;
            _chipNode.Y = (DistH - _chipNode.H) / 2f;
            _chipNode.PropTranslateX = dx;
            float half = width * 0.5f;
            float armW = Math.Max(0f, half - 8f);
            UiNode.Of(_armL).Place(8f, 9f, armW, 17f);
            UiNode.Of(_armLAfter).Place(1f, 8f, Math.Max(0f, armW - 1f), 1f);
            float armRW = Math.Max(0f, width - 8f - half);
            UiNode.Of(_armR).Place(half, 9f, armRW, 17f);
            UiNode.Of(_armRAfter).Place(0f, 8f, Math.Max(0f, armRW - 1f), 1f);
        }

        /// <summary>gapEl.classList.toggle('has-brackets', on): the arms show (field.css:33).</summary>
        private void SetBrackets(bool on)
        {
            _hasBrackets = on;
            if (!_gapEl) return;
            UiNode.Of(_armL).Visible = on;
            UiNode.Of(_armR).Visible = on;
        }

        /// <summary>gapEl.style.display = on ? '' : 'none' (the root's display goes with it: the tag lives beside the root).</summary>
        private void SetGapDisplay(bool on)
        {
            _gapDisplay = on;
            if (_gapNode) _gapNode.Visible = _rootDisplay && _gapDisplay;
        }

        /// <summary>root.style.display = on ? '' : 'none'.</summary>
        private void SetRootDisplay(bool on)
        {
            _rootDisplay = on;
            if (_rootNode) _rootNode.Visible = on;
            if (_gapNode) _gapNode.Visible = _rootDisplay && _gapDisplay;
        }

        /// <summary>
        /// bind(floor, o) = FloorView.Bind: the cells, the aimed ones (dense where they meet the boundary), the move frame, the
        /// gap. o.Reveal: the newly aimed cells drawn left to right; cells that stop being aimed fade over 120 ms.
        /// </summary>
        // js: scenes/battle/field/floor.js:215
        public void Bind(FloorFrame floor, FloorBindOpts o = null)
        {
            BindCells(floor, CellsOf(o != null ? o.Enemies : null), o);
        }

        /// <summary>Bind with the enemies already read (a rebind of the last floor passes the last enemies as they were drawn).</summary>
        private void BindCells(FloorFrame floor, List<EnemyCells> enemies, FloorBindOpts o)
        {
            if (_destroyed || !_root) return;
            FloorReveal reveal = o != null ? o.Reveal : null;
            if (o != null && o.Unread != null) _unreadOf = o.Unread;
            // §5.8 提示 (review 21: with two or three enemies the first omen drew every enemy's new stripes, the back enemy's 240 ms
            // before its own badge dropped): an omen shown for enemy `reveal.unit` draws that enemy's new cells only; the others
            // keep the cells they showed until their own omen comes in (or the event's frame lands).
            if (reveal != null && reveal.Unit >= 0 && floor != null && enemies.Count > 1 && _lastFloor != null)
            {
                List<EnemyCells> was = _lastEnemies;
                var kept = new List<EnemyCells>(enemies.Count);
                foreach (EnemyCells e in enemies)
                {
                    if (e == null || e.Unit == reveal.Unit) { kept.Add(e); continue; }
                    EnemyCells p = was.Find(x => x != null && x.Unit == e.Unit);
                    kept.Add(p != null ? CopyEnemy(e, p.AimCells, p.DenseCells, p.MoveCells) : e);
                }
                enemies = kept;
            }
            _lastFloor = floor;
            _lastEnemies = enemies;
            int n = floor != null && floor.Cells > 0 ? floor.Cells : 0;
            SetRootDisplay(n > 0);
            if (n != _cells) Build(n);
            if (n == 0) return;
            var drawn = new List<(TileState T, int Mine)>();
            // battle-visual-v1 §4.2 (several enemies, #589's DepictionFrame.Enemies): each standing enemy's aimed cells in its
            // own direction (EnemyFrame.StripeDirection), crossing where two overlap; else the one enemy's at 45°.
            // Review 19: a one-enemy frame carries its one share too, so the fallen one's cells went with `soloRetired`
            // nowhere and stayed striped through its whole fall.
            bool several = enemies.Count > 1;
            List<Foe> foes = FoesOf(floor, enemies, several);
            for (int i = 0; i < _tiles.Count; i++)
            {
                TileState t = _tiles[i];
                int c = i + 1;
                var threats = new List<Threat>();
                bool moveTo = false;
                bool moveFaded = true;
                bool focusing = _focusUnit.HasValue && foes.Count > 1;
                foreach (Foe e in foes)
                {
                    bool faded = focusing && e.Unit != _focusUnit.Value;
                    if ((e.AimCells ?? NoCells).IndexOf(c) >= 0)
                    {
                        threats.Add(new Threat
                        {
                            Angle = foes.Count > 1 || e.Hatch != 0 ? BattleLayout.HatchAngle(e.Hatch) : AimAngle,
                            Boundary = (e.DenseCells ?? NoCells).IndexOf(c) >= 0,
                            Faded = faded,
                        });
                    }
                    if ((e.MoveCells ?? NoCells).IndexOf(c) >= 0) { moveTo = true; if (!faded) moveFaded = false; }
                }
                string key = KeyOf(threats, moveTo, moveFaded && focusing);
                if (key == t.Key) continue;
                bool had = t.Threat;
                t.Key = key;
                t.Threat = threats.Count > 0;
                RectTransform box = t.El.ThreatBox;
                int mine = ++t.Fade;
                if (box) t.El.ClearThreatClip();
                // §5.8 実行の後: stripes that go with nothing in their place fade out (120, linear), then the cell is cleared.
                if (had && threats.Count == 0 && !moveTo && !_aimRetired && box && box.childCount > 0)
                {
                    float from = _aimAlpha;
                    TileState tt = t;
                    _clock.Run(AimFadeMs, Ease.Linear, k => { if (tt.Fade == mine) SetBoxOpacity(tt, Round3(from * (1f - k))); })
                        .Then(() =>
                        {
                            if (tt.Fade != mine || !tt.El.Alive) return;
                            tt.El.SetThreat(NoThreats, false);
                            SetBoxOpacity(tt, 1f);
                        }).Forget();
                    continue;
                }
                t.El.SetThreat(ToOcb(threats), moveTo);
                // battle-visual-v1 §4.2 / §5.8 (review fix, two or three enemies): one enemy's move destination on a cell another
                // enemy aims at. The DS tile draws the violet dashed frame first and the red threat frame over it at the same rect,
                // so the destination vanished; it stands in front instead, 4 px inside the red frame, on a dark keyline.
                if (moveTo && threats.Count > 0 && box)
                {
                    OcbElement mv = t.El.MoveFrame;
                    if (mv != null && mv.Alive)
                    {
                        if (mv.Box) mv.Box.SetCss(Ocb.TileMoveCss + " " + MoveOverCss);
                        UiNode host = UiNode.Of(box);
                        mv.Node.Place(4f, 4f, Math.Max(0f, host.W - 8f), Math.Max(0f, host.H - 8f));   // inset: 4px
                        mv.Root.name = "ocb-tile-move ocbg-battle-move-over";
                        mv.Root.SetAsLastSibling();   // box.appendChild(mv)
                    }
                }
                if (reveal != null && !had && threats.Count > 0 && box) drawn.Add((t, mine));
                // §2.2.2: the hovered badge's enemy keeps its stripes; the others' fade (a cell only theirs fades whole).
                if (box) SetBoxOpacity(t, _aimAlpha < 1f ? Round3(_aimAlpha) : 1f);
                OcbElement threatEl = box ? t.El.ThreatFrame : null;
                OcbElement moveEl = box ? t.El.MoveFrame : null;
                if (moveEl != null && moveEl.Alive) moveEl.Node.Opacity = moveFaded && focusing ? UnfocusedAlpha : 1f;
                if (threatEl != null && threatEl.Alive)
                {
                    bool allFaded = threats.TrueForAll(x => x.Faded);
                    threatEl.Node.Opacity = allFaded ? UnfocusedAlpha : 1f;
                    if (!allFaded)
                    {
                        for (int k = 0; k < t.El.Hatches.Count; k++)
                        {
                            if (k < threats.Count && threats[k].Faded && t.El.Hatches[k]) t.El.Hatches[k].Opacity = UnfocusedAlpha;
                        }
                    }
                }
            }
            // §5.8 提示: the new stripes are drawn left to right, one cell after another (clip from the left edge).
            float per = reveal != null && reveal.Ms.HasValue ? reveal.Ms.Value : AimDrawMs;
            for (int k = 0; k < drawn.Count; k++)
            {
                if (!(per > 0f)) continue;
                TileState t = drawn[k].T;
                int mine = drawn[k].Mine;
                t.El.SetThreatClip(1f);   // inset(0 100% 0 0)
                _clock.Wait(per * k).Then(() => _clock.Run(per, Ease.Linear, q =>
                {
                    if (t.Fade != mine || !t.El.Alive) return;
                    if (q >= 1f) t.El.ClearThreatClip();
                    else t.El.SetThreatClip(Round3(1f - q));   // inset(0 ((1 − q) × 100).toFixed(1)% 0 0)
                })).Forget();
            }
            ApplyReach();
            _recount++;
            _gapState = new GapState { Gap = floor.Gap, Pc = floor.PlayerCell, Ps = Math.Max(1, floor.PlayerSize), Ec = floor.EnemyCell };
            PlaceGap(_gapState);
        }

        /// <summary>
        /// The standing enemies bind draws (floor.js:244-246): none once the aim is retired; each enemy of the frame's list that is
        /// not down nor retired (nor the retired one enemy of a one-enemy list); else the frame's one enemy at 45°, unless it is
        /// retired. An unread enemy (unreadFoe) keeps no aimed, dense or move cells.
        /// </summary>
        // js: scenes/battle/field/floor.js:242
        private List<Foe> FoesOf(FloorFrame floor, List<EnemyCells> enemies, bool several)
        {
            var foes = new List<Foe>();
            if (_aimRetired)
            {
                // aimRetired: []
            }
            else if (enemies.Count > 0)
            {
                foreach (EnemyCells e in enemies)
                {
                    if (e == null || e.Down || _retiredUnits.Contains(e.Unit) || (_soloRetired && enemies.Count == 1)) continue;
                    foes.Add(new Foe
                    {
                        Unit = e.Unit,
                        Hatch = e.Hatch,
                        AimCells = e.AimCells ?? NoCells,
                        DenseCells = e.DenseCells ?? NoCells,
                        MoveCells = e.MoveCells ?? NoCells,
                    });
                }
            }
            else if (!_soloRetired)
            {
                foes.Add(new Foe
                {
                    Unit = -1,
                    Hatch = 0,
                    AimCells = floor.AimCells ?? NoCells,
                    DenseCells = floor.DenseCells ?? NoCells,
                    MoveCells = floor.MoveCells ?? NoCells,
                });
            }
            // unreadFoe: e && unreadOf(several ? e.Unit : undefined) → no aimed, dense or move cells
            foreach (Foe f in foes)
            {
                if (!_unreadOf(several ? f.Unit : -1)) continue;
                f.AimCells = NoCells;
                f.DenseCells = NoCells;
                f.MoveCells = NoCells;
            }
            return foes;
        }

        /// <summary>t.key: the threats' angles (+d dense, +f faded) joined by ',', then m (+f) for a move destination.</summary>
        // js: scenes/battle/field/floor.js:260
        private static string KeyOf(List<Threat> threats, bool moveTo, bool moveFadedFocusing)
        {
            var parts = new string[threats.Count];
            for (int k = 0; k < threats.Count; k++)
            {
                Threat x = threats[k];
                parts[k] = x.Angle.ToString(CultureInfo.InvariantCulture) + (x.Boundary ? "d" : "") + (x.Faded ? "f" : "");
            }
            return string.Join(",", parts) + (moveTo ? "m" + (moveFadedFocusing ? "f" : "") : "");
        }

        private static List<OcbThreat> ToOcb(List<Threat> threats)
        {
            var list = new List<OcbThreat>(threats.Count);
            foreach (Threat t in threats) list.Add(new OcbThreat(t.Angle, t.Boundary));
            return list;
        }

        /// <summary>box.style.opacity on a tile's threat host.</summary>
        private static void SetBoxOpacity(TileState t, float a)
        {
            if (t.El == null || !t.El.Alive || !t.El.ThreatBox) return;
            UiNode.Of(t.El.ThreatBox).Opacity = a;
        }

        /// <summary>
        /// focusAim(unit) (§2.2.2, two or more enemies): only enemy `unit`'s aimed cells stay lit; −1 lights them all. The gap
        /// number is left as it is.
        /// </summary>
        // js: scenes/battle/field/floor.js:314
        public void FocusAim(int unit)
        {
            int? u = unit >= 0 ? unit : (int?)null;
            if (u == _focusUnit) return;
            _focusUnit = u;
            if (_lastFloor == null || _cells == 0) return;
            GapState? keep = _gapState;
            BindCells(_lastFloor, _lastEnemies, null);
            if (keep != null) { _gapState = keep; PlaceGap(_gapState); }
        }

        /// <summary>showReach(cells): the amber reach light on these cells (150 ms in, out).</summary>
        // js: scenes/battle/field/floor.js:325
        public void ShowReach(IReadOnlyList<int> cells)
        {
            _reach = cells != null ? new List<int>(cells) : new List<int>();
            ApplyReach();
        }

        /// <summary>hideReach(): the reach light goes (120 ms, linear).</summary>
        // js: scenes/battle/field/floor.js:329
        public void HideReach()
        {
            _reach = new List<int>();
            ApplyReach();
        }

        /// <summary>setGap(gap, playerCell, playerSize, enemyCell): the gap chip at once (enemyCell ≤ 0 keeps the last).</summary>
        // js: scenes/battle/field/floor.js:334
        public void SetGap(int gap, int playerCell, int playerSize, int enemyCell)
        {
            if (_cells == 0) return;
            _recount++;
            _gapState = new GapState
            {
                Gap = gap,
                Pc = playerCell,
                Ps = Math.Max(1, playerSize),
                Ec = enemyCell > 0 ? enemyCell : _gapState.HasValue ? _gapState.Value.Ec : 1,
            };
            ResetNum();
            PlaceGap(_gapState);
        }

        /// <summary>num.style.transform = ''; num.style.opacity = ''.</summary>
        private void ResetNum()
        {
            if (!_numNode) return;
            _numNode.TranslateY = 0f;
            _numNode.Opacity = 1f;
        }

        /// <summary>
        /// recountGap(…, ms) = FloorView.RecountGap (§5.7 順 2): the old number rises out of the tag, the new one rises in from
        /// below, and the tag slides to the middle of the new empty cells, all in `ms`. The brackets wait for the end.
        /// </summary>
        // js: scenes/battle/field/floor.js:348
        public Promise RecountGap(int gap, int playerCell, int playerSize, int enemyCell, float ms)
        {
            if (_clock == null) return Promise.Resolved();
            if (_cells == 0 || _gapState == null) return _clock.Wait(ms);
            string before = _numText;
            float fromLeft = _gapLeftCss ?? 0f;
            float fromWidth = _gapWidthCss.HasValue && _gapWidthCss.Value != 0f ? _gapWidthCss.Value : FloorSpec.TagAtZero;
            float fromDx = _chipDxCss;
            _gapState = new GapState { Gap = gap, Pc = playerCell, Ps = Math.Max(1, playerSize), Ec = enemyCell > 0 ? enemyCell : _gapState.Value.Ec };
            GapPlacement to = GapPlace(_gapState.Value);
            string after = gap.ToString(CultureInfo.InvariantCulture);
            int mine = ++_recount;
            if (!(ms > 0f)) { PlaceGap(_gapState); return Promise.Resolved(); }
            SetBrackets(false);
            return _clock.Run(ms, Ease.Out, t =>
            {
                if (mine != _recount || !_gapEl) return;
                _gapLeftCss = Round1(fromLeft + (to.Left - fromLeft) * t);
                _gapWidthCss = Round1(fromWidth + (to.Width - fromWidth) * t);
                float dx = fromDx + (to.ChipDx - fromDx) * t;
                _chipDxCss = Math.Abs(dx) > 0.05f ? Round1(dx) : 0f;
                bool leaving = t < 0.5f;
                float half = leaving ? t * 2f : (t - 0.5f) * 2f;
                SetNumText(leaving ? before : after);
                LayoutGapParts(_gapWidthCss.Value, _chipDxCss);
                _gapNode.X = _gapLeftCss.Value;
                _numNode.TranslateY = Round1(leaving ? -RecountTravel * half : RecountTravel * (1f - half));
                _numNode.Opacity = Round3(leaving ? 1f - half : half);
            }).Then(() =>
            {
                if (mine != _recount || !_gapEl) return;
                ResetNum();
                PlaceGap(_gapState);
            });
        }

        /// <summary>retireAim(ms): the battle is over — the aimed / dense / move cells fade (in) and stay clear.</summary>
        // js: scenes/battle/field/floor.js:378
        public Promise RetireAim(float ms)
        {
            if (_aimRetired) return Promise.Resolved();
            int mine = ++_aimToken;
            Action clear = () =>
            {
                if (mine != _aimToken) return;
                _aimRetired = true;
                _aimAlpha = 1f;
                foreach (TileState t in _tiles)
                {
                    t.Key = "";
                    t.Threat = false;
                    t.Fade++;
                    if (t.El == null || !t.El.Alive) continue;
                    t.El.SetThreat(NoThreats, false);
                    if (t.El.ThreatBox)
                    {
                        SetBoxOpacity(t, 1f);
                        t.El.ClearThreatClip();
                    }
                }
            };
            if (!(ms > 0f) || _clock == null) { clear(); return Promise.Resolved(); }
            return _clock.Run(ms, Ease.In, t =>
            {
                if (mine != _aimToken) return;
                _aimAlpha = 1f - t;
                foreach (TileState tl in _tiles) SetBoxOpacity(tl, Round3(_aimAlpha));
            }).Then(clear);
        }

        /// <summary>
        /// retireUnit(unit, solo) (§5.15 順 5): enemy `unit` falls — its cells go now (`solo`: the battle's one enemy, whose frame
        /// may or may not carry its enemy list), with its omen badge (§5.8's 120 ms fade for cells it alone marked), instead of
        /// with the next frame a second later; the gap number is left as it is.
        /// </summary>
        // js: scenes/battle/field/floor.js:399
        public void RetireUnit(int unit, bool solo)
        {
            bool known = unit >= 0;
            int before = _retiredUnits.Count + (_soloRetired ? 1 : 0);
            if (known) _retiredUnits.Add(unit);
            if (solo || !known) _soloRetired = true;
            if (_retiredUnits.Count + (_soloRetired ? 1 : 0) == before) return;
            if (_lastFloor == null || _cells == 0) return;
            GapState? keep = _gapState;
            BindCells(_lastFloor, _lastEnemies, null);
            if (keep != null) { _gapState = keep; PlaceGap(_gapState); }
        }

        /// <summary>setGapHidden(on): the battle is over — the gap number goes; it measured from the hero to an enemy that is no more.</summary>
        // js: scenes/battle/field/floor.js:411
        public void SetGapHidden(bool on)
        {
            if (on == _gapHidden) return;
            _gapHidden = on;
            if (_gapState != null) PlaceGap(_gapState);
            else SetGapDisplay(!on);
        }

        /// <summary>
        /// edgeFlash(dir, ms) (§5.7 順 7, review 19): a pushed figure hit the end of the line toward `dir` (−1 left, +1 right) —
        /// that end of the floor lights for `ms` (out), then fades over 160 (in). Drawn in the floor (it slides with the arena).
        /// </summary>
        // js: scenes/battle/field/floor.js:422
        public Promise EdgeFlash(int dir, float ms)
        {
            if (_cells == 0 || !_root || _clock == null) return Promise.Resolved();
            float x = dir < 0 ? FloorSpec.Left : FloorSpec.Right;
            BoxGraphic el = BoxGraphic.Make(_root, WallEdgeCss, Round1(x - WallEdgeW / 2f), Top - WallEdgeUp, WallEdgeW,
                WallEdgeUp + FloorSpec.Height, "ocbg-battle-wall-edge");
            return _clock.Run(Math.Max(1f, ms), Ease.Out, t => { if (el) el.Opacity = Round3(1f - 0.3f * t); })
                .Then(() => _clock.Run(WallEdgeFade, Ease.In, t => { if (el) el.Opacity = Round3(0.7f * (1f - t)); }))
                .Then(() => { if (el) Ui.Remove(el); });
        }

        /// <summary>feetX(cell, size) = FloorView.FeetOf's x: the middle of the cells a figure takes; null without a floor.</summary>
        // js: scenes/battle/field/floor.js:437
        public float? FeetX(int cell, int size) => _cells > 0 ? FloorSpec.FeetX(_cells, cell, size) : (float?)null;

        /// <summary>cellBox(cell): tile `cell`'s box (before the arena's slide), or null.</summary>
        // js: scenes/battle/field/floor.js:441
        public StageBox? CellBox(int cell)
        {
            if (_cells <= 0 || cell < 1 || cell > _cells) return null;
            return new StageBox(FloorSpec.TileLeft(_cells, cell), Top, FloorSpec.TileWidth(_cells), FloorSpec.Height);
        }

        /// <summary>destroy(): root.remove() (the gap tag, which stands beside the root, with it).</summary>
        // js: scenes/battle/field/floor.js:446
        public void Destroy()
        {
            _destroyed = true;
            if (_root) Ui.Remove(_root);
            if (_gapEl) Ui.Remove(_gapEl);
            _root = null;
            _gapEl = null;
        }

        // ---- helpers ---------------------------------------------------------------------------------------

        /// <summary>The value a style write of toFixed(1) reads back as.</summary>
        private static float Round1(float v) => (float)Math.Round(v, 1, MidpointRounding.AwayFromZero);

        /// <summary>The value a style write of toFixed(3) reads back as.</summary>
        private static float Round3(float v) => (float)Math.Round(v, 3, MidpointRounding.AwayFromZero);

        /// <summary>Object.assign(Object.create(proto), e, { AimCells, DenseCells, MoveCells }): an enemy with another omen's cells.</summary>
        private static EnemyCells CopyEnemy(EnemyCells e, List<int> aim, List<int> dense, List<int> move) => new EnemyCells
        {
            Unit = e.Unit,
            Hatch = e.Hatch,
            Cell = e.Cell,
            Size = e.Size,
            Gap = e.Gap,
            Down = e.Down,
            AimCells = aim,
            DenseCells = dense,
            MoveCells = move,
        };
    }
}
