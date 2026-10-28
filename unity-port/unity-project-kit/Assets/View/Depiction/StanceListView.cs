// The stance lists at the two edges of the screen (battle-visual-v1 §4.6 スタンス枠, #455). v4.4 keeps
// every stance put down on a list with no cap (battle_core_v4 §4), so each stance takes one §4.6 frame,
// 250×52, stacked upward from the slot at y 470 (StanceList). The player's frames stand 24 px from the
// left edge with the stance colour's 2 px frame and flag; the enemy's 24 px from the right with the
// violet frame and a phosphor flag. A stance held twice shows "×2" beside its name. A side holding
// none shows the empty frame: no fill, a steel dashed edge and a steel flag. A list longer than the
// frames folds its oldest stances into a "+n" frame on top. No lamp is drawn (§4.6: a trait is judged
// once, when the stance is put down).
//
// #605: a frame takes the hover and shows the §7.3 説明パネル beside it (the player's on its right,
// the enemy's on its left): the flag and "岩の構え ×2" as the heading, "永続" on the right, and the
// 効き方 / 減り方 lines. The "+n" frame's panel lists the folded stances in the order put down. The
// stances are not status chips any more, so this panel is the only place their effect is written.
//
// The rows are the script's (UnitFrame.Stances, their panel text included); this draws them. The scene belongs to the Unity
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
        private const float PanelPad = 16f;
        private const float PanelTitleHeight = 26f;
        private const float PanelIcon = 20f;
        private const float PanelGroupWidth = 110f;

        private sealed class Frame
        {
            public RectTransform Root;
            public Image Fill;
            public Image Edge;
            public Image[] Dashed;
            public Image Flag;
            public Text Name;
            public Text Count;
            public UnitSide Unit;
            public int FromBottom;
            /// <summary>The stance this frame shows, or null for the "+n" and the empty frame.</summary>
            public StanceRow Row;
            /// <summary>The "+n" frame: the rows it folds, oldest first, for its panel.</summary>
            public List<StanceRow> Folded;
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
        private RectTransform _panel;
        private Image _panelFlag;
        private Text _panelTitle;
        private Text _panelGroup;
        private Text _panelBody;
        private Frame _hovered;

        public StanceListView(RectTransform canvasRoot, int siblingIndex)
        {
            UiKit.EnsureFont();
            _canvas = canvasRoot;
            _root = UiKit.Rect(canvasRoot, "StanceLists", Vector2.zero, Vector2.one);
            if (siblingIndex >= 0) _root.SetSiblingIndex(siblingIndex);
            // The frames take the hover (#605); nothing on them is clicked.
            CanvasGroup group = UiKit.Group(_root);
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
            if (!shown)
            {
                Hover(null);
                return;
            }
            BindSide(_player, frame.Player);
            BindSide(_enemy, frame.Enemy);
            // A frame that lands under the pointer keeps its panel, with the new rows' text.
            Hover(_hovered);
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
                f.Row = null;
                f.Folded = null;
                if (!used) continue;
                if (rows.Count == 0)
                {
                    ShowEmpty(f);
                    continue;
                }
                // Slot 0 (the §4.6 slot itself) holds the newest stance; older ones stand above it.
                bool foldRow = folded > 0 && i == frames - 1;
                if (foldRow)
                {
                    f.Folded = rows.GetRange(0, folded);
                    ShowRow(side.Unit, f, "+" + folded, "");
                }
                else
                {
                    StanceRow row = rows[rows.Count - 1 - i];
                    f.Row = row;
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
            var f = new Frame { Unit = unit, FromBottom = fromBottom };
            var size = new Vector2(StanceList.Width, StanceList.RowHeight);
            f.Root = UiKit.Point(parent, "Stance" + fromBottom, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), size, Vector2.zero);
            RefScreen.Place(f.Root, _canvas, new Vector2(StanceList.LeftOf(unit), StanceList.TopOf(fromBottom)), size);
            // パネル (88%) and the side's 2 px frame, 6 px corners (§3.2).
            f.Fill = VisualArt.Panel(f.Root, "Back", BattleTheme.Panel, 6);
            // The hover target (#605): the fill, so the empty frame (no fill, nothing to explain) takes none.
            f.Fill.raycastTarget = true;
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
            UiKit.OnPointer(f.Root.gameObject, UnityEngine.EventSystems.EventTriggerType.PointerEnter, _ => Hover(f));
            UiKit.OnPointer(f.Root.gameObject, UnityEngine.EventSystems.EventTriggerType.PointerExit, _ =>
            {
                if (_hovered == f) Hover(null);
            });
            return f;
        }

        // ---- the panel (§7.3 説明パネル, beside the frame §4.6, #605) ----------------------------------

        /// <summary>Shows the panel of <paramref name="f"/>, or hides it for null, an unused frame or the empty one.</summary>
        private void Hover(Frame f)
        {
            bool has = f != null && f.Root.gameObject.activeInHierarchy && (f.Row != null || f.Folded != null);
            // A frame that went away or emptied under the pointer gets no exit event, so forget it here.
            _hovered = has ? f : null;
            if (!has)
            {
                if (_panel) _panel.gameObject.SetActive(false);
                return;
            }
            EnsurePanel();
            _panelFlag.color = f.Unit == UnitSide.Player ? BattleTheme.HpInk : BattleTheme.Phosphor;
            _panelGroup.text = StanceList.PanelGroup;
            if (f.Folded != null)
            {
                // 「+n」: every folded stance as 「名前 ×数：効き方」, in the order put down.
                _panelTitle.text = "+" + f.Folded.Count;
                _panelBody.text = string.Join("\n", StanceList.FoldedLines(f.Folded, f.Folded.Count));
            }
            else
            {
                _panelTitle.text = StanceList.PanelTitle(f.Row);
                _panelBody.text = StanceList.PanelBody(f.Row);
            }
            float bodyHeight = _panelBody.preferredHeight;
            _panelBody.rectTransform.sizeDelta = new Vector2(StanceList.PanelWidth - 2f * PanelPad, bodyHeight + 4f);
            float height = 12f + PanelTitleHeight + 8f + bodyHeight + 12f;
            var size = new Vector2(StanceList.PanelWidth, height);
            var topLeft = new Vector2(StanceList.PanelLeft(f.Unit), StanceList.PanelTop(f.FromBottom, height));
            RefScreen.Place(_panel, _canvas, topLeft, size);
            _panel.SetAsLastSibling();
            _panel.gameObject.SetActive(true);
        }

        private void EnsurePanel()
        {
            if (_panel) return;
            // On the canvas itself, so it stands over the number panels and the hand, like the chips' panel.
            _panel = UiKit.Point(_canvas, "StancePanel", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f),
                new Vector2(StanceList.PanelWidth, 100f), Vector2.zero);
            CanvasGroup group = UiKit.Group(_panel);
            group.blocksRaycasts = false;
            group.interactable = false;
            VisualArt.Panel(_panel, "Back", BattleTheme.PanelOpaque, 6);
            VisualArt.Ring(_panel, "Edge", BattleTheme.Steel, 6, 1);
            // The heading: the flag at 20 px and "岩の構え ×2" (18 px, bold); "永続" on the right (14 px, supporting ink).
            _panelFlag = UiKit.Sprite(_panel, "Flag", OmenIconArt.Of(OmenIcon.Stance), BattleTheme.HpInk, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(PanelIcon, PanelIcon), new Vector2(PanelPad, -12f - (PanelTitleHeight - PanelIcon) * 0.5f));
            _panelFlag.preserveAspect = true;
            float titleLeft = PanelPad + PanelIcon + 6f;
            _panelTitle = UiKit.Label(_panel, "Title", 18, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(StanceList.PanelWidth - titleLeft - PanelPad - PanelGroupWidth, PanelTitleHeight), new Vector2(titleLeft, -12f));
            _panelTitle.fontStyle = FontStyle.Bold;
            _panelGroup = UiKit.Label(_panel, "Group", 14, TextAnchor.MiddleRight, BattleTheme.Ink2, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(PanelGroupWidth, PanelTitleHeight), new Vector2(-PanelPad, -12f));
            // 効き方 / 減り方 at 16 px.
            _panelBody = UiKit.Label(_panel, "Body", 16, TextAnchor.UpperLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(StanceList.PanelWidth - 2f * PanelPad, 60f), new Vector2(PanelPad, -12f - PanelTitleHeight - 8f));
            _panel.gameObject.SetActive(false);
        }
    }
}
#endif
