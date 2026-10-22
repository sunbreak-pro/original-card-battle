// 俊敏's choice on the floor (docs/BATTLE_SCENE.md §4.8, battle-views §2.5): the marks over the open ways (前へ / 後ろへ, 64×40
// in amber) and 留まる on the player's tile (64×28), the amber focus edge, the word on a line tip over the hovered or
// focused mark, shown 150 ms (out, rising 8), hidden 120 ms (in). In layer.marks; positions follow geom.cellBox (the arena's
// slide). Pointer enter focuses, click picks (uGUI handlers; the marks are `.ocbg-battle-hit`). Port of
// scenes/battle/field/freestep.js.
//
// battle_ui_ux_v2 §3.8 / §5.1 順 7 / §5.7 順 10; scene addition with the source's JS extension CanTakeFreeStep /
// TakeFreeStep — Unity offers no free step. While the choice waits, an arrow stands over the cell before and the cell
// behind the player — only where the source says the way is open — and a 留まる mark on the player's own cell. A click on a
// mark chooses it; ← / → pick back / forward and Enter chooses (the focus is the amber edge, as every focus in the battle).
// The marks only show what they are handed: which ways are open is the source's, and the step itself plays as the source's
// event (the walk on the floor).
//
// Review 19 (§1.5: a resting screen's fixed labels are at most 10 characters, each at most 2 — 「← 後ろへ」「→ 前へ」「留まる」
// beside ターン終了 made 13; battle-visual-v1 §11 lists 俊敏の矢印 and 留まる as icons): the marks are icons only — 付録 A's
// 前へ / 後ろへ in amber and, for 留まる, a stand-in line icon of a figure on its own cell (#92 draws the real one) — and the
// mark's word shows as a tip only while it is hovered or focused (a tip is no resting label).
//
// Where they stand (scene choice): the arrows over their cells, their bottom 8 px over the floor's top, so the gap number on
// the cells between the player and the enemy stays readable under them; the 留まる mark on the player's own tile, which the
// gap number never covers — a lower plate (28 tall) on the tile's lower part, its top 2 px under the feet.
using System;
using System.Collections.Generic;
using Trial.Geometry;
using Trial.Kit;
using UnityEngine;

namespace Trial.Battle
{
    /// <summary>createFreeStep's o: { onPick(dir), onFocus(dir) } — dir −1 back, 0 stay, +1 forward.</summary>
    // js: scenes/battle/field/freestep.js:69
    public sealed class FreeStepOpts
    {
        public Action<int> OnPick;
        public Action<int> OnFocus;
    }

    /// <summary>createFreeStep(env, o) → the marks' api (freestep.js:141).</summary>
    // js: scenes/battle/field/freestep.js:71
    public sealed class FieldFreeStep
    {
        /// <summary>TOP: FloorView: the floor's top (y 560; its tiles are 40 tall, to y 600).</summary>
        // js: scenes/battle/field/freestep.js:22
        private const float Top = 560f;
        /// <summary>MARK_H / ARROW_W / STAY_W: the marks: 40 tall; an arrow 64 wide, 留まる 64.</summary>
        // js: scenes/battle/field/freestep.js:24
        private const float MarkH = 40f;
        private const float ArrowW = 64f;
        private const float StayW = 64f;
        /// <summary>STAY_H / STAY_UNDER_FEET / FEET_Y: 留まる: 28 tall, from 2 px under the feet (FEET_Y 570) to the tile's bottom (600).</summary>
        // js: scenes/battle/field/freestep.js:28
        private const float StayH = 28f;
        private const float StayUnderFeet = 2f;
        private const float FeetY = 570f;
        /// <summary>OVER_FLOOR: the arrows stand this far over the floor.</summary>
        // js: scenes/battle/field/freestep.js:31
        private const float OverFloor = 8f;
        /// <summary>ICON / STAY_ICON: the icons' sizes.</summary>
        // js: scenes/battle/field/freestep.js:32
        private const float IconPx = 30f;
        private const float StayIconPx = 24f;
        /// <summary>TIP_GAP: the word's tip: 8 px over the mark.</summary>
        // js: scenes/battle/field/freestep.js:35
        private const float TipGap = 8f;
        /// <summary>SHOW_MS / HIDE_MS / RISE: §5.1 順 7: the marks come up over 150 ms (out); they go in 120.</summary>
        // js: scenes/battle/field/freestep.js:37
        private const float ShowMs = 150f;
        private const float HideMs = 120f;
        private const float Rise = 8f;

        /// <summary>WORDS: scene text (battle_ui_ux_v2 §3.8): the marks' words, on their tips; 前へ / 後ろへ are the 付録 A icons' own names.</summary>
        // js: scenes/battle/field/freestep.js:42
        private static readonly IReadOnlyDictionary<int, string> Words = new Dictionary<int, string>
        {
            [-1] = "後ろへ", [0] = "留まる", [1] = "前へ",
        };

        /// <summary>ICONS: the arrows' DS icons.</summary>
        // js: scenes/battle/field/freestep.js:43
        private static readonly IReadOnlyDictionary<int, string> IconOf = new Dictionary<int, string>
        {
            [-1] = "back", [1] = "forward",
        };

        /// <summary>
        /// STAY_PATH: 留まる's stand-in icon (battle-visual-v1 §11: 「演出の印 6 … 俊敏の矢印・留まる」 are to be drawn, #92): 付録 A's
        /// line style (24×24, 2.2 px, round ends) — a figure standing on its own cell, the cell's outline under its feet.
        /// </summary>
        // js: scenes/battle/field/freestep.js:48
        private const string StayPath = "M12 3.5a2.6 2.6 0 110 5.2a2.6 2.6 0 010-5.2zM12 8.7v6.6M8.6 11.6l3.4-1.6 3.4 1.6M9.4 19.6l2.6-4.3 2.6 4.3M3.5 21h17";

        /// <summary>stayIcon's svg: viewBox 0 0 24 24, fill none, stroke var(--amber) 2.2, round caps and joins.</summary>
        // js: scenes/battle/field/freestep.js:49 (stayIcon(size): drawn by PathGraphic.Icon in Build)
        private static readonly IconDef StayIconDef = new IconDef("stay", StayPath, new RectF(0f, 0f, 24f, 24f), false);

        // ---- field.css:36-41 (verbatim) -------------------------------------------------------------------------

        /// <summary>field.css:36 .ocbg-battle .ocbg-battle-freestep</summary>
        private const string RootCss = "position: absolute; left: 0; top: 0;";
        /// <summary>field.css:37 .ocbg-battle .ocbg-battle-fs-mark</summary>
        private const string MarkCss = "position: absolute; box-sizing: border-box; display: flex; align-items: center; justify-content: center; gap: var(--space-6); background: var(--panel-solid); border: var(--stroke-rule) solid var(--steel); border-radius: var(--radius-panel); font: 700 20px/1 var(--font-body); color: var(--ink); white-space: nowrap; cursor: pointer;";
        /// <summary>field.css:38 .ocbg-battle .ocbg-battle-fs-mark.is-stay</summary>
        private const string StayCss = "font-size: 18px;";
        /// <summary>field.css:40 .ocbg-battle .ocbg-battle-fs-tip</summary>
        private const string TipCss = "position: absolute; transform: translate(-50%, -100%); pointer-events: none; z-index: 2;";
        /// <summary>field.css:41 .ocbg-battle .ocbg-battle-fs-mark.is-focus</summary>
        private const string FocusCss = "border: 2px solid var(--amber); background: color-mix(in srgb, var(--amber) 16%, var(--panel-solid)); box-shadow: 0 0 12px color-mix(in srgb, var(--amber) 40%, transparent);";

        /// <summary>One mark: { dir, el, cell, w } and what its inline style holds (left / top as written; display).</summary>
        private sealed class Mark
        {
            public int Dir;
            public RectTransform El;
            public UiNode Node;
            public BoxGraphic Box;
            public int Cell;
            public float W;
            public float Left = float.NaN;
            public float Top = float.NaN;
            public bool Hidden;
            public bool Focused;
        }

        private readonly BattleViewEnv _env;
        private readonly FreeStepOpts _o;
        private RectTransform _root;
        private UiNode _rootNode;
        private readonly List<Mark> _marks = new List<Mark>();
        private bool _shown;
        private int? _focus;
        private float _alpha;
        private float _rise = Rise;
        private int _token;
        /// <summary>hoverDir: the mark under the pointer.</summary>
        private int? _hoverDir;
        private RectTransform _tip;
        private UiNode _tipNode;
        private string _tipWord;
        private Action _offFrame;

        private FieldFreeStep(BattleViewEnv env, FreeStepOpts o)
        {
            _env = env;
            _o = o ?? new FreeStepOpts();
            RectTransform layer = env != null ? env.Layer.Marks : null;
            if (!layer) return;
            // js: scenes/battle/field/freestep.js:73
            _root = Ocb.El(layer, "ocbg-battle-freestep", RootCss);
            _rootNode = UiNode.Of(_root);
            _rootNode.Visible = false;
            // js: scenes/battle/field/freestep.js:139 (const offFrame = clock.onFrame(place))
            if (env.Clock != null) _offFrame = env.Clock.OnFrame((dt, dtReal) => Place());
        }

        /// <summary>createFreeStep(env, o): the marks' root in layer.marks (hidden); the frame loop places them.</summary>
        // js: scenes/battle/field/freestep.js:71
        public static FieldFreeStep Create(BattleViewEnv env, FreeStepOpts o) => new FieldFreeStep(env, o);

        /// <summary>build(playerCell, ways): the marks for the open ways and 留まる, each a hit with its pointer listeners.</summary>
        // js: scenes/battle/field/freestep.js:84
        private void Build(int playerCell, FreeStepWays ways)
        {
            ClearMarks();
            RemoveTip();
            _hoverDir = null;
            if (!_root) return;
            foreach (int dir in new[] { -1, 0, 1 })
            {
                if (dir != 0 && !ways[dir]) continue;
                bool stay = dir == 0;
                string css = MarkCss + (stay ? " " + StayCss : "");
                RectTransform el = Ocb.El(_root, "ocbg-battle-fs-mark ocbg-battle-hit" + (stay ? " is-stay" : ""), css);
                var m = new Mark
                {
                    Dir = dir,
                    El = el,
                    Node = UiNode.Of(el),
                    Box = el.GetComponent<BoxGraphic>(),
                    Cell = playerCell + dir,
                    W = stay ? StayW : ArrowW,
                };
                // the icon centred in the plate (display: flex; align-items / justify-content: center)
                float h = stay ? StayH : MarkH;
                PathGraphic icon = stay
                    ? PathGraphic.Icon(el, StayIconDef, StayIconPx, DsTokens.Amber)
                    : Ocb.Icon(el, IconOf[dir], IconPx, DsTokens.Amber);
                float size = stay ? StayIconPx : IconPx;
                if (icon) Ui.Place(icon.rectTransform, (m.W - size) / 2f, (h - size) / 2f);
                // role: button, aria-label WORDS[dir]: the pointer's listeners (.ocbg-battle-hit: a press on it never fast-forwards)
                BattleHitTag.Mark(el);
                ClickArea area = ClickArea.On(el);
                int d = dir;
                area.Enter += _ =>
                {
                    _hoverDir = d;
                    DrawTip();
                    if (_shown) _o.OnFocus?.Invoke(d);
                };
                area.Exit += _ =>
                {
                    if (_hoverDir == d) { _hoverDir = null; DrawTip(); }
                };
                // click: e.stopPropagation() (the BattleHitTag keeps the scene's own press handling off it); the pick
                area.Click += _ =>
                {
                    if (_shown) _o.OnPick?.Invoke(d);
                };
                _marks.Add(m);
            }
            DrawFocus();
        }

        // js: scenes/battle/field/freestep.js:102
        private void DrawFocus()
        {
            foreach (Mark m in _marks)
            {
                bool on = _focus.HasValue && m.Dir == _focus.Value;
                if (on == m.Focused) continue;
                m.Focused = on;
                // classList.toggle('is-focus', on)
                if (m.Box) m.Box.SetCss(MarkCss + (m.Dir == 0 ? " " + StayCss : "") + (on ? " " + FocusCss : ""));
            }
            DrawTip();
        }

        /// <summary>drawTip(): the word of the mark under the pointer, else of the focused one, on a tip over it.</summary>
        // js: scenes/battle/field/freestep.js:110
        private void DrawTip()
        {
            int? dir = _hoverDir ?? _focus;
            Mark m = dir.HasValue && _shown ? _marks.Find(x => x.Dir == dir.Value) : null;
            if (m == null || m.Hidden)
            {
                if (_tipNode) _tipNode.Visible = false;
                return;
            }
            EnsureTip(Words[dir.Value]);
            if (!_tipNode) return;
            _tipNode.Visible = true;
            float left = float.IsNaN(m.Left) ? 0f : m.Left;   // parseFloat(m.el.style.left) || 0
            float top = float.IsNaN(m.Top) ? 0f : m.Top;
            _tipNode.X = Round1(left + m.W / 2f);
            _tipNode.Y = Round1(top - TipGap);
        }

        /// <summary>
        /// The tip h('div', { class: 'ocbg-battle-linetip ocbg-battle-fs-tip' }, h('span')) with `word` as its span's text: built
        /// again when the word changes (HudUtil.LineTip lays it out to its text), translate(−50%, −100%), no pointer, z 2.
        /// </summary>
        // js: scenes/battle/field/freestep.js:108
        private void EnsureTip(string word)
        {
            if (_tip && _tipWord == word) return;
            if (!_root) return;
            RemoveTip();
            _tip = HudUtil.LineTip(word, _root, TipCss);
            _tipWord = word;
            _tipNode = UiNode.Of(_tip);
            _tipNode.TranslatePctX = -0.5f;
            _tipNode.TranslatePctY = -1f;
            _tipNode.PointerEvents = PointerEvents.None;
            _tipNode.Z = 2;
            _tip.SetAsLastSibling();
        }

        private void RemoveTip()
        {
            if (_tip) Ui.Remove(_tip);
            _tip = null;
            _tipNode = null;
            _tipWord = null;
        }

        private void ClearMarks()
        {
            foreach (Mark m in _marks) if (m.El) Ui.Remove(m.El);
            _marks.Clear();
        }

        /// <summary>place(): every frame while the choice is up or fading: the root's alpha, each mark over its cell (geom.cellBox).</summary>
        // js: scenes/battle/field/freestep.js:122
        private void Place()
        {
            if (!_shown && _alpha <= 0f) return;
            if (!_root) return;
            _rootNode.Opacity = _alpha >= 1f ? 1f : Round3(_alpha);
            Func<int, StageBox?> cellBox = _env != null && _env.Geom != null ? _env.Geom.CellBox : null;
            foreach (Mark m in _marks)
            {
                StageBox? b = cellBox != null ? cellBox(m.Cell) : null;
                if (b == null)
                {
                    m.Hidden = true;
                    if (m.Node) m.Node.Visible = false;
                    continue;
                }
                m.Hidden = false;
                if (!m.Node) continue;
                m.Node.Visible = true;
                bool stay = m.Dir == 0;
                float top = stay ? FeetY + StayUnderFeet : Top - OverFloor - MarkH + _rise;
                m.Left = Round1(b.Value.X + b.Value.W / 2f - m.W / 2f);
                m.Top = Round1(top);
                m.Node.Place(m.Left, m.Top, m.W, stay ? StayH : MarkH);
            }
            DrawTip();
        }

        /// <summary>shown.</summary>
        // js: scenes/battle/field/freestep.js:142
        public bool Shown => _shown;

        /// <summary>focus: −1 / 0 / +1, or null.</summary>
        // js: scenes/battle/field/freestep.js:143
        public int? Focus => _focus;

        /// <summary>show(playerCell, ways): the marks for a player on `playerCell` (the ways the source opens + 留まる), 150 ms (out).</summary>
        // js: scenes/battle/field/freestep.js:145
        public Promise Show(int playerCell, FreeStepWays ways)
        {
            int mine = ++_token;
            _shown = true;
            _focus = null;
            Build(playerCell, ways);
            if (_rootNode) _rootNode.Visible = true;
            _alpha = 0f;
            _rise = Rise;
            Place();
            if (_env == null || _env.Clock == null) return Promise.Resolved();
            return _env.Clock.Run(ShowMs, Ease.Out, t =>
            {
                if (mine != _token) return;
                _alpha = t;
                _rise = Rise * (1f - t);
                Place();
            });
        }

        /// <summary>setFocus(dir): the amber edge on mark `dir`; a way not shown takes no focus. Returns the focus it took.</summary>
        // js: scenes/battle/field/freestep.js:162
        public int? SetFocus(int? dir)
        {
            _focus = dir.HasValue && _marks.Exists(m => m.Dir == dir.Value) ? dir : null;
            DrawFocus();
            return _focus;
        }

        /// <summary>dirs(): which marks stand, [−1?, 0, +1?].</summary>
        // js: scenes/battle/field/freestep.js:168
        public IReadOnlyList<int> Dirs()
        {
            var dirs = new int[_marks.Count];
            for (int i = 0; i < _marks.Count; i++) dirs[i] = _marks[i].Dir;
            return dirs;
        }

        /// <summary>
        /// span(cellBox): the x span the marks take while the choice is up, from `cellBox(cell)` (the floor's own box, before
        /// the arena's slide); null when none stands. The drawer's slide brings them out with the hero (field.js).
        /// </summary>
        // js: scenes/battle/field/freestep.js:173
        public (float Left, float Right)? Span(Func<int, StageBox?> cellBox)
        {
            if (!_shown || _marks.Count == 0 || cellBox == null) return null;
            float left = float.PositiveInfinity;
            float right = float.NegativeInfinity;
            foreach (Mark m in _marks)
            {
                StageBox? b = cellBox(m.Cell);
                if (b == null) continue;
                left = Math.Min(left, b.Value.X + b.Value.W / 2f - m.W / 2f);
                right = Math.Max(right, b.Value.X + b.Value.W / 2f + m.W / 2f);
            }
            return left < right ? (left, right) : ((float Left, float Right)?)null;
        }

        /// <summary>hide(ms = 120): the marks go (in); 0 at once.</summary>
        // js: scenes/battle/field/freestep.js:186
        public Promise Hide(float ms = HideMs)
        {
            if (!_shown && _alpha <= 0f) return Promise.Resolved();
            int mine = ++_token;
            _shown = false;
            float from = _alpha;
            Action done = () =>
            {
                if (mine != _token) return;
                _alpha = 0f;
                if (_rootNode) _rootNode.Visible = false;
                // root.innerHTML = '': the marks and the tip go
                ClearMarks();
                RemoveTip();
                _focus = null;
            };
            if (!(ms > 0f) || _env == null || _env.Clock == null) { done(); return Promise.Resolved(); }
            return _env.Clock.Run(ms, Ease.In, t =>
            {
                if (mine != _token) return;
                _alpha = from * (1f - t);
                Place();
            }).Then(done);
        }

        /// <summary>boxes(): every mark's box as it stands (the arena's slide included): the card detail keeps off them.</summary>
        // js: scenes/battle/field/freestep.js:200
        public IReadOnlyList<FreeStepBox> Boxes()
        {
            if (!_shown) return Array.Empty<FreeStepBox>();
            var boxes = new List<FreeStepBox>();
            foreach (Mark m in _marks)
            {
                if (m.Hidden || float.IsNaN(m.Left) || float.IsNaN(m.Top)) continue;
                boxes.Add(new FreeStepBox(m.Left, m.Top, m.W, m.Dir == 0 ? StayH : MarkH, m.Dir));
            }
            return boxes;
        }

        /// <summary>markCentre(dir): the middle of mark `dir` in stage px (tests and the playtest click it), or null.</summary>
        // js: scenes/battle/field/freestep.js:209
        public StagePoint? MarkCentre(int dir)
        {
            Mark m = _marks.Find(x => x.Dir == dir);
            if (m == null || float.IsNaN(m.Left) || float.IsNaN(m.Top)) return null;
            return new StagePoint(m.Left + m.W / 2f, m.Top + (m.Dir == 0 ? StayH : MarkH) / 2f);
        }

        /// <summary>destroy().</summary>
        // js: scenes/battle/field/freestep.js:216
        public void Destroy()
        {
            _token++;
            _offFrame?.Invoke();
            _offFrame = null;
            if (_root) Ui.Remove(_root);
            _root = null;
        }

        /// <summary>The number a style write of toFixed(1) reads back as.</summary>
        private static float Round1(float v) => (float)Math.Round(v, 1, MidpointRounding.AwayFromZero);

        /// <summary>The number a style write of toFixed(3) reads back as.</summary>
        private static float Round3(float v) => (float)Math.Round(v, 3, MidpointRounding.AwayFromZero);
    }
}
