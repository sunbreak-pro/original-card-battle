// The stance lists at the two edges of the screen (battle-visual-v1 §4.6 スタンス枠, #455). v4.4 keeps
// every stance put down on a list with no cap (battle_core_v4 §4), so each stance takes one §4.6 frame,
// 250×52, stacked upward from the slot at y 470 (StanceList). The player's frames stand 24 px from the
// left edge with the stance colour's 2 px frame and flag; the enemy's 24 px from the right with the
// violet frame and a phosphor flag. A stance held twice shows "×2" beside its name. A side holding
// none shows the empty frame: no fill, a steel dashed edge and a steel flag. A list longer than the
// frames folds its oldest stances into a "+n" frame on top. No lamp is drawn (§4.6: a trait is judged
// once, when the stance is put down).
//
// The rows are the script's (UnitFrame.Stances); this draws them. The scene belongs to the Unity
// project, so the frames are made at run time on the canvas, like the piles.
#if UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public sealed class StanceListView
    {
        private const float PadLeft = 12f;
        private const float PadRight = 12f;
        private const float FlagSize = 24f;
        private const float FlagGap = 8f;
        private const int NameSize = 20;
        private const int CountSize = 16;
        private const float CountWidth = 40f;

        private sealed class Frame
        {
            public RectTransform Root;
            public Image Fill;
            public Image Edge;
            public Image[] Dashed;
            public Image Flag;
            public Text Name;
            public Text Count;
        }

        private sealed class Side
        {
            public UnitSide Unit;
            public RectTransform Root;
            public readonly List<Frame> Frames = new List<Frame>();
        }

        private readonly RectTransform _canvas;
        private readonly RectTransform _root;
        private readonly Side _player;
        private readonly Side _enemy;

        public StanceListView(RectTransform canvasRoot, int siblingIndex)
        {
            UiKit.EnsureFont();
            _canvas = canvasRoot;
            _root = UiKit.Rect(canvasRoot, "StanceLists", Vector2.zero, Vector2.one);
            if (siblingIndex >= 0) _root.SetSiblingIndex(siblingIndex);
            CanvasGroup group = UiKit.Group(_root);
            group.blocksRaycasts = false;
            group.interactable = false;
            _player = MakeSide(UnitSide.Player);
            _enemy = MakeSide(UnitSide.Enemy);
            _root.gameObject.SetActive(false);
        }

        /// <summary>Draws both sides' lists of <paramref name="frame"/>; a source that keeps none hides them.</summary>
        public void Bind(DepictionFrame frame)
        {
            bool shown = frame != null && frame.StanceListsShown;
            _root.gameObject.SetActive(shown);
            if (!shown) return;
            BindSide(_player, frame.Player);
            BindSide(_enemy, frame.Enemy);
        }

        private void BindSide(Side side, UnitFrame unit)
        {
            List<StanceRow> rows = unit != null && unit.Stances != null ? unit.Stances : new List<StanceRow>();
            int frames = StanceList.Frames(rows.Count);
            int folded = StanceList.Folded(rows.Count);
            for (int i = 0; i < side.Frames.Count; i++)
            {
                Frame f = side.Frames[i];
                bool used = i < frames;
                f.Root.gameObject.SetActive(used);
                if (!used) continue;
                if (rows.Count == 0)
                {
                    ShowEmpty(f);
                    continue;
                }
                // Slot 0 (the §4.6 slot itself) holds the newest stance; older ones stand above it.
                bool foldRow = folded > 0 && i == frames - 1;
                if (foldRow) ShowRow(side.Unit, f, "+" + folded, "");
                else
                {
                    StanceRow row = rows[rows.Count - 1 - i];
                    ShowRow(side.Unit, f, row.Name, StanceList.CountText(row.Count));
                }
            }
        }

        private static void ShowRow(UnitSide unit, Frame f, string name, string count)
        {
            f.Fill.enabled = true;
            f.Edge.enabled = true;
            f.Edge.color = unit == UnitSide.Player ? BattleTheme.HpInk : BattleTheme.Violet;
            foreach (Image edge in f.Dashed) edge.enabled = false;
            f.Flag.color = unit == UnitSide.Player ? BattleTheme.HpInk : BattleTheme.Phosphor;
            f.Name.enabled = true;
            f.Name.text = name;
            f.Count.text = count;
            f.Count.enabled = count.Length > 0;
        }

        /// <summary>§4.6 空きの枠: no fill, a steel dashed frame and a steel flag only.</summary>
        private static void ShowEmpty(Frame f)
        {
            f.Fill.enabled = false;
            f.Edge.enabled = false;
            foreach (Image edge in f.Dashed) edge.enabled = true;
            f.Flag.color = BattleTheme.Steel;
            f.Name.enabled = false;
            f.Count.enabled = false;
        }

        private Side MakeSide(UnitSide unit)
        {
            var side = new Side { Unit = unit };
            side.Root = UiKit.Rect(_root, unit + "Stances", Vector2.zero, Vector2.one);
            for (int i = 0; i < StanceList.MaxRows; i++) side.Frames.Add(MakeFrame(side.Root, unit, i));
            return side;
        }

        private Frame MakeFrame(RectTransform parent, UnitSide unit, int fromBottom)
        {
            var f = new Frame();
            var size = new Vector2(StanceList.Width, StanceList.RowHeight);
            f.Root = UiKit.Point(parent, "Stance" + fromBottom, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), size, Vector2.zero);
            RefScreen.Place(f.Root, _canvas, new Vector2(StanceList.LeftOf(unit), StanceList.TopOf(fromBottom)), size);
            // パネル (88%) and the side's 2 px frame, 6 px corners (§3.2).
            f.Fill = VisualArt.Panel(f.Root, "Back", BattleTheme.Panel, 6);
            f.Edge = VisualArt.Ring(f.Root, "Edge", BattleTheme.HpInk, 6, 2);
            // §3.2 破線 for 空き, in steel, 2 px like a frame.
            f.Dashed = VisualArt.DashedFrame(f.Root, BattleTheme.Steel, 2f);
            f.Flag = UiKit.Sprite(f.Root, "Flag", OmenIconArt.Of(OmenIcon.Stance), BattleTheme.HpInk, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(FlagSize, FlagSize), new Vector2(PadLeft, 0f));
            f.Flag.preserveAspect = true;
            float nameLeft = PadLeft + FlagSize + FlagGap;
            // Shippori Mincho 700 at 20 px in §4.6; the built-in font stands in until the fonts arrive.
            f.Name = UiKit.Label(f.Root, "Name", NameSize, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(StanceList.Width - nameLeft - PadRight - CountWidth, StanceList.RowHeight), new Vector2(nameLeft, 0f));
            f.Name.fontStyle = FontStyle.Bold;
            f.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
            // §7.3 永続: the count as "×2", IBM Plex Mono at 16 px, in body ink (text under 24 px).
            f.Count = UiKit.Label(f.Root, "Count", CountSize, TextAnchor.MiddleRight, BattleTheme.Ink, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(CountWidth, StanceList.RowHeight), new Vector2(-PadRight, 0f));
            f.Count.fontStyle = FontStyle.Bold;
            f.Count.horizontalOverflow = HorizontalWrapMode.Overflow;
            return f;
        }
    }
}
#endif
