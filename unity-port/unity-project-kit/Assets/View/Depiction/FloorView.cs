// The floor under the figures (battle-visual-v1 §4.2, #242): the line of cells, the cells the omen
// aims at in red stripes (#350) — 6 px apart on those that meet the boundary its action reads, and
// a violet dashed frame on the cell a moving omen takes the enemy to (#455) — the cells a hovered or
// held card reaches in amber, and the one gap number between the player and the enemy. Every cell comes from the script (DepictionFrame.Floor,
// CardFace.ReachCells); this only draws them and says where a cell stands on the screen, so the
// figures can stand on theirs.
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
            public Image Stripes;
            public Image AimFrame;
            public Image[] MoveFrame;
            public Image ReachTop;
            /// <summary>Which stripes the cell holds now, so the sprite is only swapped when it changes.</summary>
            public bool Dense;
        }

        private readonly RectTransform _root;
        private readonly RectTransform _canvas;
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly RectTransform _tag;
        private readonly Text _tagText;
        private readonly Image _bracketLeft;
        private readonly Image _bracketRight;
        private FloorFrame _floor;
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
            _tagText.fontStyle = FontStyle.Bold;
            _tagText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// Draws <paramref name="floor"/>: the cells, the aimed ones (dense where they meet the omen's
        /// boundary), the cells a moving omen lands on, and the gap. The floor's top goes
        /// 10 px above <paramref name="playerFeet"/> (a world point). A floor with no cells hides it all.
        /// </summary>
        public void Bind(FloorFrame floor, Vector3 playerFeet, int layer)
        {
            _floor = floor;
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
                bool aimed = floor.AimCells.Contains(i + 1);
                cell.Stripes.enabled = aimed;
                cell.AimFrame.enabled = aimed;
                cell.AimLight.enabled = aimed;
                // §4.2 境目を満たすマス: the same red stripes, 6 px apart instead of 11 (#455).
                bool dense = aimed && floor.DenseCells.Contains(i + 1);
                if (dense != cell.Dense)
                {
                    cell.Dense = dense;
                    cell.Stripes.sprite = VisualArt.Stripes(0, dense);
                }
                // 敵が動く予兆の移る先: a violet dashed frame, never stripes (battle_ui_ux_v2 §5.8, #455).
                bool moveTo = floor.MoveCells.Contains(i + 1);
                foreach (Image edge in cell.MoveFrame) edge.enabled = moveTo;
            }
            HideReach();
            SetGap(floor.Gap, floor.PlayerCell, floor.PlayerSize, floor.EnemyCell);
            // The gap and its brackets sit over the cells.
            _bracketLeft.transform.SetAsLastSibling();
            _bracketRight.transform.SetAsLastSibling();
            _tag.SetAsLastSibling();
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
            string before = _tagText.text;
            Vector3 fromPosition = _tag.position;
            Vector2 fromSize = _tag.sizeDelta;
            SetGap(gap, playerCell, playerSize, enemyCell); // the end state, read back to tween towards
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
            cell.AimLight = Outset(cell.Root, "AimLight", ProceduralArt.Glow, BattleTheme.WithAlpha(BattleTheme.AimRed, 0.30f), AimGlow);
            cell.Tile = VisualArt.Panel(cell.Root, "Tile", _tile, 6);
            cell.Edge = VisualArt.Ring(cell.Root, "Edge", BattleTheme.Line, 6, 1);
            cell.ReachFill = UiKit.Image(cell.Root, "ReachFill", VisualArt.Fade, BattleTheme.WithAlpha(BattleTheme.Amber, 0.55f), Vector2.zero, Vector2.one);
            // §4.2: the stripes and the frame at 100%; the cell's inside is never filled red (§2.3).
            cell.Stripes = UiKit.Image(cell.Root, "Stripes", VisualArt.Stripes(0, false), BattleTheme.AimRed, Vector2.zero, Vector2.one);
            cell.Stripes.type = Image.Type.Tiled;
            cell.Stripes.rectTransform.offsetMin = new Vector2(2f, 2f);
            cell.Stripes.rectTransform.offsetMax = new Vector2(-2f, -2f);
            cell.AimFrame = VisualArt.Ring(cell.Root, "AimFrame", BattleTheme.AimRed, 6, 2);
            // §3.2 破線 (3 px dashes 3 px apart) at the 2 px of a frame, in violet: where a moving omen lands.
            cell.MoveFrame = VisualArt.DashedFrame(cell.Root, BattleTheme.Violet, 2f);
            foreach (Image edge in cell.MoveFrame) edge.enabled = false;
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
