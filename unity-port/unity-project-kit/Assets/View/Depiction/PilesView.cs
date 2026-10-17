// The three piles under the stamina tag (battle-visual-v1 §4.7, #455): draw, discard and exile, each
// a 64×84 card back with its 付録 A icon at 28 px, 22 px apart from x 32, y 948. A pile's thickness
// is sheets laid 4 px apart behind the front one, as many as its count calls for (PileLayout); a pile
// with no card is an empty slot in a steel dashed frame. No number is written: battle_ui_ux_v2 §1.1
// B9 reads the piles by their thickness. The counts are the script's (DepictionFrame.Piles).
//
// The scene belongs to the Unity project, so the piles are made at run time on the canvas, like the
// stamina tag above them. Their middles are where drawn cards fly from and discarded ones fly to.
// A click on a pile opens its list (battle_ui_ux_v2 §1.1 C4, PileListView); the piles say which.
#if UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public sealed class PilesView
    {
        private sealed class Pile
        {
            public PileKind Kind;
            public readonly List<GameObject> Behind = new List<GameObject>();
            public Image FrontFill;
            public Image FrontEdge;
            public Image[] EmptyFrame;
            public Image Icon;
        }

        private static readonly PileKind[] Kinds = { PileKind.Draw, PileKind.Discard, PileKind.Exile };

        private readonly RectTransform _canvas;
        private readonly RectTransform _root;
        private readonly List<Pile> _piles = new List<Pile>();

        public PilesView(RectTransform canvasRoot, int siblingIndex)
        {
            _canvas = canvasRoot;
            _root = UiKit.Rect(canvasRoot, "Piles", Vector2.zero, Vector2.one);
            if (siblingIndex >= 0) _root.SetSiblingIndex(siblingIndex);
            // Only each pile's own hit area takes a click (C4); the rest of the full-screen root lets them through.
            foreach (PileKind kind in Kinds) _piles.Add(MakePile(kind));
            _root.gameObject.SetActive(false);
        }

        /// <summary>battle_ui_ux_v2 §1.1 C4 (#455): a pile was clicked; its list opens.</summary>
        public event System.Action<PileKind> Clicked;

        /// <summary>Whether the piles are drawn (the script keeps them).</summary>
        public bool Shown => _root.gameObject.activeSelf;

        /// <summary>Draws the piles of <paramref name="piles"/>; a frame whose source keeps none hides them.</summary>
        public void Bind(PileFrame piles)
        {
            bool shown = piles != null && piles.Shown;
            _root.gameObject.SetActive(shown);
            if (!shown) return;
            foreach (Pile pile in _piles)
            {
                int count = PileLayout.CountOf(piles, pile.Kind);
                int behind = PileLayout.SheetsBehind(count);
                for (int i = 0; i < pile.Behind.Count; i++) pile.Behind[i].SetActive(i < behind);
                bool empty = PileLayout.IsEmpty(count);
                pile.FrontFill.enabled = !empty;
                pile.FrontEdge.enabled = !empty;
                foreach (Image edge in pile.EmptyFrame) edge.enabled = empty;
                pile.Icon.color = empty ? BattleTheme.Steel : BattleTheme.Ink;
            }
        }

        /// <summary>The middle of <paramref name="kind"/>'s front card, as a world point.</summary>
        public Vector3 MiddleOf(PileKind kind)
        {
            return RefScreen.World(_canvas, new Vector2(PileLayout.LeftOf(kind) + PileLayout.Width * 0.5f, PileLayout.Top + PileLayout.Height * 0.5f));
        }

        private Pile MakePile(PileKind kind)
        {
            var pile = new Pile { Kind = kind };
            var size = new Vector2(PileLayout.Width, PileLayout.Height);
            RectTransform root = UiKit.Point(_root, kind + "Pile", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), size, Vector2.zero);
            RefScreen.Place(root, _canvas, new Vector2(PileLayout.LeftOf(kind), PileLayout.Top), size);

            // The farthest sheet first, so each nearer one covers it: 4 px higher per step back.
            for (int i = PileLayout.MaxSheetsBehind; i >= 1; i--)
            {
                RectTransform sheet = UiKit.Point(root, "Sheet" + i, new Vector2(0f, 1f), new Vector2(0f, 1f), size, new Vector2(0f, PileLayout.SheetStep * i));
                Card(sheet);
                pile.Behind.Insert(0, sheet.gameObject);
            }

            RectTransform front = UiKit.Point(root, "Front", new Vector2(0f, 1f), new Vector2(0f, 1f), size, Vector2.zero);
            Image[] card = Card(front);
            pile.FrontFill = card[0];
            pile.FrontEdge = card[1];
            // §3.2 破線 (3 px dashes 3 px apart) for 空き, in steel.
            pile.EmptyFrame = VisualArt.DashedFrame(front, BattleTheme.Steel, 1f);
            foreach (Image edge in pile.EmptyFrame) edge.enabled = false;
            pile.Icon = UiKit.Sprite(front, "Icon", VisualArt.Icon(IconOf(kind)), BattleTheme.Ink, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(PileLayout.IconSize, PileLayout.IconSize), Vector2.zero);
            pile.Icon.preserveAspect = true;

            // C4: the whole 64×84 of the pile, empty or not, opens its list.
            Image hit = UiKit.Image(root, "Hit", ProceduralArt.White, Color.clear, Vector2.zero, Vector2.one);
            hit.raycastTarget = true;
            UiKit.OnPointer(hit.gameObject, UnityEngine.EventSystems.EventTriggerType.PointerClick, _ => Clicked?.Invoke(kind));
            return pile;
        }

        /// <summary>One card of the pile, as the stamina tag is drawn: カードの地, a steel 1 px frame, 5 px corners.</summary>
        private static Image[] Card(RectTransform parent)
        {
            Image fill = VisualArt.Panel(parent, "Back", BattleTheme.CardGround, 5);
            Image edge = VisualArt.Ring(parent, "Edge", BattleTheme.Steel, 5, 1);
            return new[] { fill, edge };
        }

        private static VisualIcon IconOf(PileKind kind)
        {
            switch (kind)
            {
                case PileKind.Discard: return VisualIcon.Discard;
                case PileKind.Exile: return VisualIcon.Exile;
                default: return VisualIcon.Draw;
            }
        }
    }
}
#endif
