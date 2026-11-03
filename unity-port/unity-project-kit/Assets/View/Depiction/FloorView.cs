// The floor under the figures (battle-visual-v1 §4.2, #242): the line of cells, the cells the omen
// aims at in red stripes (#350) — 6 px apart on those that meet the boundary its action reads, and
// a violet dashed frame on the cell a moving omen takes the enemy to (#455) — the cells a hovered or
// held card reaches in amber, and the one gap number between the player and the enemy. Every cell comes from the script (DepictionFrame.Floor,
// DepictionFrame.Enemies, CardFace.ReachCells); this only draws them and says where a cell stands on the
// screen, so the figures can stand on theirs. With several enemies (#589) each enemy's stripes run in its
// own direction and cross where two overlap, under one frame and one light (FloorStripes says which).
// A hover on one enemy's omen badge fades the others' (#762), and the gap number counts to the enemy
// FloorFocus names: the one a held card is over, the one Tab chose, or the nearest.
//
// The scene belongs to the Unity project, so the floor is made at run time under the figures'
// parent, behind them. It spans x 300〜1620 as §4.2 gives it. Its top sits 10 px above the player's
// feet, the way §4.2 and §4.3 put the floor at y 560 and the feet at y 570, so it stays under the
// figures wherever the scene stands them.
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public sealed class FloorView
    {
        // §4.2: the band and its cells (FloorSpec holds the arithmetic).
        private const float BandHeight = FloorSpec.Height;
        private const float CellInset = FloorSpec.Inset;
        private const float FeetBelowTop = FloorSpec.FeetBelowTop;
        // §4.2 狙うマス: a 2 px red frame and an 8 px light of red 30% outside it.
        private const float AimGlow = 8f;
        private const float AimLightAlpha = 0.30f;
        // §4.2 届くマスの光: a 3 px amber line along the top and a 22 px amber light outside.
        private const float ReachGlow = 22f;
        // §4.2 間合いの数字: a tag 34 px tall, 3 px below the floor's top; 100 px wide at gap 0.
        private const float TagTop = 3f;
        private const float TagHeight = 34f;
        private const float TagAtZero = FloorSpec.TagAtZero;
        // battle_ui_ux_v2 §5.7 順 2: how far the old number rises out and the new one rises in from.
        private const float RecountTravel = 14f;

        private sealed class Cell
        {
            public RectTransform Root;
            public Image ReachLight;
            public Image AimLight;
            public Image Tile;
            public Image Edge;
            public Image ReachFill;
            /// <summary>The first enemy's stripes on this cell, and every enemy's after it, made when first needed (#589).</summary>
            public Image Stripes;
            public readonly List<Image> MoreStripes = new List<Image>();
            public Image AimFrame;
            public RectTransform MoveRoot;
            public Image[] MoveFrame;
            /// <summary>The panel-coloured band under a move's dashes on a cell another enemy aims at.</summary>
            public Image[] MoveKeyline;
            public Image ReachTop;
            /// <summary>The stripes the cell shows, or is drawing in or fading towards (#766).</summary>
            public IReadOnlyList<StripeMark> Marks = NoMarks;
            /// <summary>The stripes the cell's images hold now: <see cref="Marks"/>, save while a fade takes the old ones out.</summary>
            public IReadOnlyList<StripeMark> Shown = NoMarks;
            /// <summary>How far a draw-in or a fade has got, 0〜1; 1 when the cell is set.</summary>
            public float Share = 1f;
            /// <summary>Whether a draw-in or a fade is taking the cell to <see cref="Marks"/>; a Bind to the same marks leaves it running.</summary>
            public bool Sweeping;
            /// <summary>Bumped whenever the cell is set anew, so a sweep that no longer owns the cell stops touching it.</summary>
            public int Token;
        }

        private static readonly StripeMark[] NoMarks = new StripeMark[0];

        private readonly RectTransform _root;
        private readonly RectTransform _canvas;
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly RectTransform _tag;
        private readonly Text _tagText;
        private readonly Image _bracketLeft;
        private readonly Image _bracketRight;
        private FloorFrame _floor;
        private DepictionFrame _frame;
        /// <summary>The enemy whose aims a badge hover keeps, or −1 (FloorStripes.Hovered).</summary>
        private int _hovered = -1;
        /// <summary>Whose gap the tag shows: an enemy, −1 for the floor's own, or <see cref="GapByHand"/>.</summary>
        private int _gapUnit = GapByHand;
        /// <summary>The tag was last set from a cue's numbers (a move's recount), not from an enemy's.</summary>
        private const int GapByHand = int.MinValue;
        private float _top;
        private bool _placed;
        private Color _tile = BattleTheme.Floor(1).Tile;

        /// <summary>Whether a floor is drawn (the script gave one with cells).</summary>
        public bool Shown => _floor != null && _floor.Cells > 0;

        public FloorView(RectTransform figureParent)
        {
            UiKit.EnsureFont();
            _canvas = RefScreen.Root(figureParent);
            _root = UiKit.Rect(figureParent, "Floor", Vector2.zero, Vector2.one);
            _root.SetAsFirstSibling(); // behind the figures
            CanvasGroup group = UiKit.Group(_root);
            group.blocksRaycasts = false;
            group.interactable = false;

            // The two brackets reach from the tag to the ends of the empty cells (本文色の 55%).
            Color bracket = BattleTheme.WithAlpha(BattleTheme.Ink, 0.55f);
            _bracketLeft = UiKit.Sprite(_root, "BracketLeft", ProceduralArt.White, bracket, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _bracketRight = UiKit.Sprite(_root, "BracketRight", ProceduralArt.White, bracket, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _tag = UiKit.Point(_root, "Gap", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(TagAtZero, TagHeight), Vector2.zero);
            VisualArt.Panel(_tag, "Back", BattleTheme.PanelOpaque, 6);
            VisualArt.Ring(_tag, "Edge", BattleTheme.Steel, 6, 1);
            UiKit.Sprite(_tag, "Icon", VisualArt.Icon(VisualIcon.Range), BattleTheme.Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 22f), new Vector2(10f, 0f));
            _tagText = UiKit.Label(_tag, "Number", 28, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(48f, TagHeight), new Vector2(38f, 0f));
            BattleFonts.Use(_tagText, BattleFonts.SmallNumber);
            _tagText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// Draws the floor of <paramref name="frame"/>: the cells, the aimed ones (dense where they meet the
        /// omen's boundary), the cells a moving omen lands on, and the gap. The floor's top goes
        /// 10 px above <paramref name="playerFeet"/> (a world point). A floor with no cells hides it all.
        /// The gap counts to enemy <paramref name="gapUnit"/> (FloorFocus.GapUnit), and a hover on enemy
        /// <paramref name="hovered"/>'s badge fades the others' aims.
        /// </summary>
        public void Bind(DepictionFrame frame, Vector3 playerFeet, int gapUnit = -1, int hovered = -1)
        {
            FloorFrame floor = frame.Floor;
            int layer = frame.Corner.Floor;
            _floor = floor;
            _frame = frame;
            _hovered = FloorStripes.Hovered(frame, hovered);
            bool shown = _canvas && floor != null && floor.Cells > 0;
            _root.gameObject.SetActive(shown);
            if (!shown) return;
            // Measured once: later the figures stand on the floor, so their feet follow it, not it them.
            if (!_placed)
            {
                _top = RefScreen.Of(_canvas, playerFeet).y - FeetBelowTop;
                _placed = true;
            }
            _tile = BattleTheme.Floor(layer).Tile;
            while (_cells.Count < floor.Cells) _cells.Add(MakeCell(_cells.Count));
            for (int i = 0; i < _cells.Count; i++)
            {
                Cell cell = _cells[i];
                bool used = i < floor.Cells;
                cell.Root.gameObject.SetActive(used);
                if (!used) continue;
                RefScreen.Place(cell.Root, _canvas, new Vector2(FloorSpec.TileLeft(floor.Cells, i + 1), _top),
                    new Vector2(FloorSpec.TileWidth(floor.Cells), BandHeight));
                cell.Tile.color = _tile;
                PaintAims(cell, i + 1);
            }
            HideReach();
            _gapUnit = GapByHand; // a new frame: the gap is drawn again, whoever it counts to
            ShowGapOf(gapUnit);
            // The gap and its brackets sit over the cells.
            _bracketLeft.transform.SetAsLastSibling();
            _bracketRight.transform.SetAsLastSibling();
            _tag.SetAsLastSibling();
        }

        /// <summary>
        /// battle_ui_ux_v2 §2.2 の 2 (#762): a hover on enemy <paramref name="hovered"/>'s omen badge keeps its
        /// aimed cells and fades every other enemy's; −1 shows them all again. The floor keeps the frame
        /// it was last bound to, so the hover can come and go between frames.
        /// </summary>
        public void ShowHovered(int hovered)
        {
            if (!Shown) return;
            hovered = FloorStripes.Hovered(_frame, hovered);
            if (hovered == _hovered) return;
            _hovered = hovered;
            for (int i = 0; i < _cells.Count && i < _floor.Cells; i++) PaintAims(_cells[i], i + 1);
        }

        /// <summary>
        /// §2.2 の 3 / §1.4 (#762): the gap number counts to enemy <paramref name="unit"/> (FloorFocus.GapUnit;
        /// −1 is the floor's own, the nearest). Nothing changes while it already does.
        /// </summary>
        public void ShowGapOf(int unit)
        {
            if (!Shown || unit == _gapUnit) return;
            _gapUnit = unit;
            GapReading gap = FloorFocus.GapOf(_frame, unit);
            DrawGap(gap.Gap, gap.PlayerCell, gap.PlayerSize, gap.EnemyCell);
        }

        /// <summary>
        /// The aims on cell <paramref name="number"/> (1-based) of the bound frame, under the badge hover:
        /// §4.2 one frame and one light however many enemies aim at it, and a moving omen's violet frame.
        /// </summary>
        private void PaintAims(Cell cell, int number)
        {
            IReadOnlyList<StripeMark> marks = FloorStripes.Of(_frame, number, _hovered);
            // #766: a draw-in or a fade already heading to these stripes plays on, and a badge hover only
            // re-tints a draw-in at its share; anything else is set at once.
            if (cell.Sweeping && SameMarks(cell.Marks, marks))
            {
                if (marks.Count > 0)
                {
                    cell.Marks = cell.Shown = marks;
                    TintAims(cell, cell.Share);
                }
            }
            else SetAim(cell, marks, 1f);
            // 敵が動く予兆の移る先: a violet dashed frame, never stripes (battle_ui_ux_v2 §5.8, #455).
            ShowMoveFrame(cell, FloorStripes.MoveTo(_frame, number), FloorStripes.MoveInset(_frame, number),
                FloorStripes.MoveFaint(_frame, number, _hovered) ? FloorStripes.FaintAlpha : 1f);
        }

        /// <summary>
        /// §4.2: each enemy's stripes in its own direction, crossing where two overlap. 境目を満たすマス: the
        /// same red stripes, 6 px apart instead of 11 (#455), enemy by enemy. Their colour is
        /// <see cref="TintAims"/>'s, which also fades another enemy's under a badge hover (#762).
        /// </summary>
        private static void ShowStripes(Cell cell, IReadOnlyList<StripeMark> marks)
        {
            cell.Stripes.enabled = marks.Count > 0;
            if (marks.Count > 0) PaintStripes(cell.Stripes, marks[0]);
            while (cell.MoreStripes.Count < marks.Count - 1) cell.MoreStripes.Add(MakeStripes(cell));
            for (int k = 0; k < cell.MoreStripes.Count; k++)
            {
                Image stripes = cell.MoreStripes[k];
                bool used = k + 1 < marks.Count;
                stripes.enabled = used;
                if (used) PaintStripes(stripes, marks[k + 1]);
            }
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.8 提示 (#766): the cells that gain stripes in <paramref name="after"/> are set to
        /// them unseen now, and the returned play draws them in from the left, each cell one
        /// EffectId.StripeDraw stagger after the one before (EffectPlan.StripeDrawStartMs). The cells are
        /// taken at once, so a Bind to <paramref name="after"/> before the play ends leaves it running.
        /// Not played (the effect switched off), <see cref="SettleStripes"/> shows them.
        /// </summary>
        public IEnumerator DrawStripesIn(DepictionFrame after, EffectSwitches effects, out int drawnCells)
        {
            drawnCells = 0;
            if (!Shown || after == null || effects == null) return Nothing();
            List<int> drawn = StripeSweep.DrawnIn(Holding(), after);
            if (drawn.Count == 0) return Nothing();
            var cells = new List<Cell>();
            var tokens = new List<int>();
            foreach (int number in drawn)
            {
                if (number > _cells.Count) break;
                Cell cell = _cells[number - 1];
                SetAim(cell, FloorStripes.Of(after, number, FloorStripes.Hovered(after, _hovered)), 0f);
                cell.Sweeping = true;
                cells.Add(cell);
                tokens.Add(cell.Token);
            }
            drawnCells = cells.Count;
            float totalMs = EffectPlan.StripeDrawMs(cells.Count, effects);
            return Sweep(cells, tokens, totalMs, elapsed =>
            {
                for (int k = 0; k < cells.Count; k++)
                {
                    if (cells[k].Token == tokens[k]) TintAims(cells[k], EffectPlan.StripeDrawShare(k, elapsed, effects));
                }
            });
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.8 実行 (#766): the cells that lose every stripe in <paramref name="after"/> keep
        /// theirs for now, and the returned play fades them out together over EffectId.StripeFade. Called
        /// before the Bind of <paramref name="after"/>, which then leaves the fade running.
        /// Not played (the effect switched off), <see cref="SettleStripes"/> takes them away.
        /// </summary>
        public IEnumerator FadeStripesOut(DepictionFrame after, EffectSwitches effects, out int fadedCells)
        {
            fadedCells = 0;
            if (!Shown || after == null || effects == null) return Nothing();
            List<int> faded = StripeSweep.FadedOut(Holding(), after);
            if (faded.Count == 0) return Nothing();
            var cells = new List<Cell>();
            var tokens = new List<int>();
            foreach (int number in faded)
            {
                if (number > _cells.Count) break;
                Cell cell = _cells[number - 1];
                cell.Marks = NoMarks;
                cell.Sweeping = true;
                cell.Token++;
                cells.Add(cell);
                tokens.Add(cell.Token);
            }
            fadedCells = cells.Count;
            float fadeMs = EffectPlan.StripeFadeMs(effects);
            return Sweep(cells, tokens, fadeMs, elapsed =>
            {
                float left = fadeMs > 0f ? 1f - Mathf.Clamp01(elapsed / fadeMs) : 0f;
                for (int k = 0; k < cells.Count; k++)
                {
                    if (cells[k].Token == tokens[k]) TintAims(cells[k], left);
                }
            });
        }

        /// <summary>Ends every draw-in and fade where it was heading: the settle of both effects.</summary>
        public void SettleStripes()
        {
            foreach (Cell cell in _cells)
            {
                if (cell.Sweeping) SetAim(cell, cell.Marks, 1f);
            }
        }

        private IEnumerator Sweep(List<Cell> cells, List<int> tokens, float totalMs, System.Action<float> atMs)
        {
            yield return UiTween.Run(totalMs, Ease.Linear, t =>
            {
                if (!_root) return;
                atMs(t * totalMs);
            });
            if (!_root) yield break;
            for (int k = 0; k < cells.Count; k++)
            {
                if (cells[k].Token == tokens[k]) SetAim(cells[k], cells[k].Marks, 1f);
            }
        }

        /// <summary>Whether each cell of the floor shown holds stripes now, or is drawing them in; a fading cell holds none.</summary>
        private List<bool> Holding()
        {
            var holding = new List<bool>();
            for (int i = 0; i < _floor.Cells && i < _cells.Count; i++) holding.Add(_cells[i].Marks.Count > 0);
            return holding;
        }

        private static IEnumerator Nothing()
        {
            yield break;
        }

        /// <summary>Sets the cell's stripes, its one frame and its one light (§4.2) to <paramref name="marks"/> at once, at <paramref name="alpha"/>.</summary>
        private static void SetAim(Cell cell, IReadOnlyList<StripeMark> marks, float alpha)
        {
            cell.Token++;
            cell.Sweeping = false;
            cell.Marks = cell.Shown = marks;
            ShowStripes(cell, marks);
            // §4.2: one frame and one light, however many enemies aim at the cell.
            cell.AimFrame.enabled = marks.Count > 0;
            cell.AimLight.enabled = marks.Count > 0;
            TintAims(cell, alpha);
        }

        /// <summary>
        /// Colours the stripes, the frame and the light the cell shows: a faint enemy's at
        /// FloorStripes.FaintAlpha under a badge hover (#762), and all of it times <paramref name="share"/>,
        /// how far a draw-in or a fade has got (#766).
        /// </summary>
        private static void TintAims(Cell cell, float share)
        {
            cell.Share = share;
            IReadOnlyList<StripeMark> shown = cell.Shown;
            if (shown.Count > 0) cell.Stripes.color = StripeRed(shown[0], share);
            for (int k = 0; k < cell.MoreStripes.Count && k + 1 < shown.Count; k++) cell.MoreStripes[k].color = StripeRed(shown[k + 1], share);
            float aimAlpha = (FloorStripes.AimFaint(shown) ? FloorStripes.FaintAlpha : 1f) * share;
            cell.AimFrame.color = BattleTheme.WithAlpha(BattleTheme.AimRed, aimAlpha);
            cell.AimLight.color = BattleTheme.WithAlpha(BattleTheme.AimRed, AimLightAlpha * aimAlpha);
        }

        private static Color StripeRed(StripeMark mark, float share) =>
            BattleTheme.WithAlpha(BattleTheme.AimRed, (mark.Faint ? FloorStripes.FaintAlpha : 1f) * share);

        /// <summary>The same stripes by direction and density; a hover's faintness only re-tints them, so it does not end a sweep.</summary>
        private static bool SameMarks(IReadOnlyList<StripeMark> a, IReadOnlyList<StripeMark> b)
        {
            if (a.Count != b.Count) return false;
            for (int k = 0; k < a.Count; k++)
            {
                if (a[k].Direction != b[k].Direction || a[k].Dense != b[k].Dense) return false;
            }
            return true;
        }

        private static void PaintStripes(Image stripes, StripeMark mark)
        {
            stripes.sprite = VisualArt.Stripes(mark.Direction, mark.Dense);
        }

        private static void ShowMoveFrame(Cell cell, bool shown, float inset, float alpha = 1f)
        {
            foreach (Image edge in cell.MoveFrame)
            {
                edge.enabled = shown;
                edge.color = BattleTheme.WithAlpha(BattleTheme.Violet, alpha);
            }
            foreach (Image edge in cell.MoveKeyline)
            {
                edge.enabled = shown && inset > 0f;
                edge.color = BattleTheme.WithAlpha(BattleTheme.PanelOpaque, alpha);
            }
            cell.MoveRoot.offsetMin = new Vector2(inset, inset);
            cell.MoveRoot.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>§4.2: the stripes and the frame at 100%; the cell's inside is never filled red (§2.3). Just over the stripes made before it, under the frame.</summary>
        private static Image MakeStripes(Cell cell)
        {
            Image stripes = UiKit.Image(cell.Root, "Stripes", VisualArt.Stripes(0, false), BattleTheme.AimRed, Vector2.zero, Vector2.one);
            stripes.type = Image.Type.Tiled;
            stripes.rectTransform.offsetMin = new Vector2(2f, 2f);
            stripes.rectTransform.offsetMax = new Vector2(-2f, -2f);
            if (cell.AimFrame) stripes.transform.SetSiblingIndex(cell.AimFrame.transform.GetSiblingIndex());
            return stripes;
        }

        /// <summary>§4.2 届くマスの光: lights <paramref name="cells"/> while an enemy-aimed card is hovered or held.</summary>
        public void ShowReach(IList<int> cells)
        {
            if (!Shown) return;
            for (int i = 0; i < _cells.Count; i++)
            {
                bool lit = cells != null && cells.Contains(i + 1);
                _cells[i].ReachFill.enabled = lit;
                _cells[i].ReachTop.enabled = lit;
                _cells[i].ReachLight.enabled = lit;
            }
        }

        public void HideReach()
        {
            foreach (Cell cell in _cells)
            {
                cell.ReachFill.enabled = false;
                cell.ReachTop.enabled = false;
                cell.ReachLight.enabled = false;
            }
        }

        /// <summary>
        /// §4.2 間合いの数字: one number over the empty cells between the two, with a bracket to each end
        /// of them; at gap 0 a 100 px tag over the border of the two cells. No colour says good or bad.
        /// </summary>
        public void SetGap(int gap, int playerCell, int playerSize, int enemyCell)
        {
            if (!Shown) return;
            _gapUnit = GapByHand; // a cue's numbers: the next ShowGapOf draws its enemy's again
            DrawGap(gap, playerCell, playerSize, enemyCell);
        }

        private void DrawGap(int gap, int playerCell, int playerSize, int enemyCell)
        {
            _tagText.text = gap.ToString();
            FloorSpec.GapSpan(_floor.Cells, playerCell, playerSize, enemyCell, out float spanLeft, out float spanRight);
            float middle = (spanLeft + spanRight) * 0.5f;
            float tagWidth = gap <= 0 ? TagAtZero : Mathf.Min(10f + 22f + 6f + Mathf.Max(18f, _tagText.preferredWidth) + 12f, Mathf.Max(60f, spanRight - spanLeft));
            float top = _top + TagTop;
            RefScreen.Place(_tag, _canvas, new Vector2(middle - tagWidth * 0.5f, top), new Vector2(tagWidth, TagHeight));
            float y = top + TagHeight * 0.5f;
            bool brackets = gap > 0 && spanRight - spanLeft > tagWidth + 8f;
            _bracketLeft.enabled = brackets;
            _bracketRight.enabled = brackets;
            if (!brackets) return;
            float inset = CellInset + 4f;
            PlaceBracket(_bracketLeft, spanLeft + inset, middle - tagWidth * 0.5f, y);
            PlaceBracket(_bracketRight, middle + tagWidth * 0.5f, spanRight - inset, y);
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.7 順 2 (#586): the gap number is counted again on arrival. The old number
        /// rises out of the tag, the new one rises in from below, and the tag slides to the middle of the
        /// new empty cells, all in <paramref name="ms"/> (EffectId.GapRecount). The brackets wait for the
        /// end and stand where <see cref="SetGap"/> puts them.
        /// </summary>
        public IEnumerator RecountGap(int gap, int playerCell, int playerSize, int enemyCell, float ms)
        {
            if (!Shown) yield break;
            _gapUnit = GapByHand;
            string before = _tagText.text;
            Vector3 fromPosition = _tag.position;
            Vector2 fromSize = _tag.sizeDelta;
            DrawGap(gap, playerCell, playerSize, enemyCell); // the end state, read back to tween towards
            string after = _tagText.text;
            Vector3 toPosition = _tag.position;
            Vector2 toSize = _tag.sizeDelta;
            bool brackets = _bracketLeft.enabled;
            _bracketLeft.enabled = _bracketRight.enabled = false;
            RectTransform number = _tagText.rectTransform;
            Vector2 rest = number.anchoredPosition;
            Color ink = _tagText.color;
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (!_tag) return;
                _tag.position = Vector3.LerpUnclamped(fromPosition, toPosition, t);
                _tag.sizeDelta = Vector2.LerpUnclamped(fromSize, toSize, t);
                bool leaving = t < 0.5f;
                float half = leaving ? t * 2f : (t - 0.5f) * 2f;
                _tagText.text = leaving ? before : after;
                number.anchoredPosition = rest + new Vector2(0f, leaving ? RecountTravel * half : -RecountTravel * (1f - half));
                _tagText.color = BattleTheme.WithAlpha(ink, ink.a * (leaving ? 1f - half : half));
            });
            if (!_tag) yield break;
            _tag.position = toPosition;
            _tag.sizeDelta = toSize;
            _tagText.text = after;
            number.anchoredPosition = rest;
            _tagText.color = ink;
            _bracketLeft.enabled = _bracketRight.enabled = brackets;
        }

        /// <summary>
        /// Where a figure on <paramref name="cell"/> taking <paramref name="size"/> cells stands: the middle
        /// of its cells, at the feet's height (10 px under the floor's top), as a world point.
        /// </summary>
        public Vector3 FeetOf(int cell, int size)
        {
            float x = FloorSpec.FeetX(_floor != null ? _floor.Cells : 1, cell, size);
            return RefScreen.World(_canvas, new Vector2(x, _top + FeetBelowTop));
        }

        private void PlaceBracket(Image line, float from, float to, float y)
        {
            RectTransform rt = line.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, to - from), 2f);
            rt.position = RefScreen.World(_canvas, new Vector2(from, y));
        }

        private Cell MakeCell(int index)
        {
            var cell = new Cell();
            cell.Root = UiKit.Point(_root, "Cell" + (index + 1), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(100f, BandHeight), Vector2.zero);
            // Lights first, then (§4.2's order when they meet) tile, amber fill, red stripes, red frame, amber top line.
            cell.ReachLight = Outset(cell.Root, "ReachLight", ProceduralArt.Glow, BattleTheme.WithAlpha(BattleTheme.Amber, 0.45f), ReachGlow);
            cell.AimLight = Outset(cell.Root, "AimLight", ProceduralArt.Glow, BattleTheme.WithAlpha(BattleTheme.AimRed, AimLightAlpha), AimGlow);
            cell.Tile = VisualArt.Panel(cell.Root, "Tile", _tile, 6);
            cell.Edge = VisualArt.Ring(cell.Root, "Edge", BattleTheme.Line, 6, 1);
            cell.ReachFill = UiKit.Image(cell.Root, "ReachFill", VisualArt.Fade, BattleTheme.WithAlpha(BattleTheme.Amber, 0.55f), Vector2.zero, Vector2.one);
            cell.Stripes = MakeStripes(cell);
            cell.AimFrame = VisualArt.Ring(cell.Root, "AimFrame", BattleTheme.AimRed, 6, 2);
            // §3.2 破線 (3 px dashes 3 px apart) at the 2 px of a frame, in violet: where a moving omen lands.
            // Over a cell another enemy aims at it steps inside the red frame on a band of the panel colour,
            // 2 px either side of the dashes (Trial/Battle/Floor.cs MoveOverCss), so the red never hides it.
            cell.MoveRoot = UiKit.Rect(cell.Root, "MoveFrame", Vector2.zero, Vector2.one);
            cell.MoveKeyline = UiKit.Frame(cell.MoveRoot, BattleTheme.PanelOpaque, 6f, -2f);
            cell.MoveFrame = VisualArt.DashedFrame(cell.MoveRoot, BattleTheme.Violet, 2f);
            ShowMoveFrame(cell, false, 0f);
            cell.ReachTop = UiKit.Image(cell.Root, "ReachTop", ProceduralArt.White, BattleTheme.Amber, new Vector2(0f, 1f), new Vector2(1f, 1f));
            cell.ReachTop.rectTransform.offsetMin = new Vector2(4f, -3f);
            cell.ReachTop.rectTransform.offsetMax = new Vector2(-4f, 0f);
            cell.ReachFill.enabled = cell.ReachTop.enabled = cell.ReachLight.enabled = false;
            cell.Stripes.enabled = cell.AimFrame.enabled = cell.AimLight.enabled = false;
            return cell;
        }

        private static Image Outset(RectTransform parent, string name, Sprite sprite, Color color, float outset)
        {
            Image image = UiKit.Image(parent, name, sprite, color, Vector2.zero, Vector2.one);
            image.rectTransform.offsetMin = new Vector2(-outset, -outset);
            image.rectTransform.offsetMax = new Vector2(outset, outset);
            return image;
        }
    }
}
#endif
