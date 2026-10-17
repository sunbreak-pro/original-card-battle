// The pile lists (battle_ui_ux_v2 §1.1 C4 / §3.13, #455): a click on the draw, discard or exile pile
// opens a drawer from the left — the journal drawer's mirror image (Journal.View.JournalDrawerView
// takes the right 30%) — with a tab per pile and one row per kind of card, "突き 2 / 3": how many the
// pile holds and how many the deck holds in all. The rows are the script's (PileFrame.CardsOf); the
// drawer only prints them, in the order given, which never follows the draw pile.
//
// The look is a stand-in, as the journal drawer's is: the opaque panel, the rule frame and the body
// text of battle-visual-v1 §2.1. Its size, type and slide are the design lane's to set. C4's fourth
// tab (the full log) waits for a battle log on the script.
#if UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public sealed class PileListView
    {
        private static readonly PileKind[] Tabs = { PileKind.Draw, PileKind.Discard, PileKind.Exile };

        private readonly RectTransform _root;
        private readonly RectTransform _tabs;
        private readonly Text _body;
        private PileFrame _piles;
        private PileKind _open = PileKind.Draw;

        /// <param name="canvasRoot">The battle canvas. The drawer takes its left 30%, over everything.</param>
        public PileListView(RectTransform canvasRoot)
        {
            UiKit.EnsureFont();
            _root = UiKit.Box(canvasRoot, "PileList", 0f, 0f, PileList.WidthShare, 1f);
            Image back = UiKit.Fill(_root, "Back", BattleTheme.PanelOpaque);
            back.raycastTarget = true; // the cards and the piles under the drawer take no click through it
            UiKit.Frame(_root, BattleTheme.Line, 1f);

            RectTransform head = UiKit.Box(_root, "Head", 0f, 0.92f, 1f, 1f);
            UiKit.Button(head, "Close", PileList.CloseLabel, Close, BattleTheme.CardGround, BattleTheme.Ink, 18,
                new Vector2(0.70f, 0.15f), new Vector2(0.96f, 0.85f));
            _tabs = UiKit.Box(_root, "Tabs", 0.03f, 0.85f, 0.97f, 0.91f);
            _body = UiKit.Text(UiKit.Box(_root, "Body", 0.04f, 0.03f, 0.96f, 0.83f), "Text", PileList.RowSize, TextAnchor.UpperLeft, BattleTheme.Ink);
            _body.lineSpacing = 1.2f;
            _root.gameObject.SetActive(false);
        }

        public bool IsOpen => _root.gameObject.activeSelf;

        /// <summary>Opens the list of <paramref name="pile"/>, or closes the drawer when that list is already open.</summary>
        public void Toggle(PileKind pile)
        {
            if (IsOpen && _open == pile)
            {
                Close();
                return;
            }
            _open = pile;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Redraw();
        }

        public void Close() => _root.gameObject.SetActive(false);

        /// <summary>Hands the drawer the piles of the frame now; an open list follows them.</summary>
        public void Show(PileFrame piles)
        {
            _piles = piles;
            if (piles == null || !piles.Shown)
            {
                Close();
                return;
            }
            if (IsOpen) Redraw();
        }

        /// <summary>True when the drawer is open and covers <paramref name="screenPoint"/>, so a card under it takes no hover.</summary>
        public bool Covers(Vector2 screenPoint)
        {
            return IsOpen && RectTransformUtility.RectangleContainsScreenPoint(_root, screenPoint, null);
        }

        private void Redraw()
        {
            UiKit.ClearChildren(_tabs);
            float width = 1f / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
            {
                PileKind pile = Tabs[i];
                bool on = pile == _open;
                Button tab = UiKit.Button(_tabs, "Tab" + pile, PileList.TabLabel(pile), () => { _open = pile; Redraw(); },
                    on ? BattleTheme.CardGround : BattleTheme.PanelOpaque, on ? BattleTheme.Ink : BattleTheme.Ink2, 18,
                    new Vector2(i * width, 0f), new Vector2((i + 1) * width - 0.02f, 1f));
                UiKit.Frame((RectTransform)tab.transform, on ? BattleTheme.Ink : BattleTheme.Line, 1f);
            }
            List<PileCard> rows = _piles != null ? _piles.CardsOf(_open) : null;
            _body.text = PileList.BodyText(rows);
        }
    }
}
#endif
