// The player's stamina tag above the piles (battle-visual-v1 §4.7, #242): the amber dot and number on
// the first row, with "▸ n" on its right while a card is held; one dot per point of the maximum on
// the second, a divider after the third (the 構え mark), the stamina there now in amber, the points a
// held card would pay as amber rings, the rest as steel rings. It shows the numbers it is handed
// (UnitFrame / StaminaChange cue through StatusBarView, the held card's CardFace.Cost) and works out
// nothing.
//
// The scene belongs to the Unity project, so the tag is made at run time on the canvas, at x 32,
// y 846, 236×84 as §4.7 gives it.
#if UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public sealed class StaminaTagView
    {
        private static readonly Vector2 At = new Vector2(32f, 846f);
        private static readonly Vector2 Size = new Vector2(236f, 84f);
        private const float PadX = 12f;
        private const float PadY = 10f;
        private const float DotSize = 16f;
        private const float DotGap = 4f;
        /// <summary>§4.7: the divider follows this many dots — the stamina 構え needs left (the script's DepictionFrame.StanceDivider). 0 draws none.</summary>
        private int _dividerAfter;

        private readonly RectTransform _root;
        private readonly Text _number;
        private readonly Text _pay;
        private readonly RectTransform _row;
        private readonly Image _divider;
        private readonly List<Image> _dots = new List<Image>();
        private int _stamina;
        private int _max;
        private int _cost;

        public StaminaTagView(RectTransform canvasRoot, int siblingIndex)
        {
            UiKit.EnsureFont();
            _root = UiKit.Point(canvasRoot, "StaminaTag", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Size, Vector2.zero);
            if (siblingIndex >= 0) _root.SetSiblingIndex(siblingIndex);
            RefScreen.Place(_root, canvasRoot, At, Size);
            CanvasGroup group = UiKit.Group(_root);
            group.blocksRaycasts = false;
            group.interactable = false;
            // カードの地、鈍鋼の枠、角丸 5 px.
            VisualArt.Panel(_root, "Back", BattleTheme.CardGround, 5);
            VisualArt.Ring(_root, "Edge", BattleTheme.Steel, 5, 1);

            // 1 行目: an 18 px dot and the number at 34 px in amber; "▸ n" in wick at 24 px on the right.
            UiKit.Sprite(_root, "Dot", ProceduralArt.Circle, BattleTheme.Amber, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, 18f), new Vector2(PadX, -PadY - 20f));
            _number = UiKit.Label(_root, "Number", 34, TextAnchor.MiddleLeft, BattleTheme.Amber, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(90f, 40f), new Vector2(PadX + 18f + 8f, -PadY - 20f));
            _number.fontStyle = FontStyle.Bold;
            _number.horizontalOverflow = HorizontalWrapMode.Overflow;
            _pay = UiKit.Label(_root, "Pay", 24, TextAnchor.MiddleRight, BattleTheme.Wick, new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(90f, 40f), new Vector2(-PadX, -PadY - 20f));
            _pay.fontStyle = FontStyle.Bold;
            _pay.horizontalOverflow = HorizontalWrapMode.Overflow;
            _pay.enabled = false;

            // 2 行目: the dots, 16 px, 4 px apart.
            _row = UiKit.Point(_root, "Dots", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(Size.x - PadX * 2f, 20f), new Vector2(PadX, PadY));
            _divider = UiKit.Sprite(_row, "Divider", ProceduralArt.White, BattleTheme.WithAlpha(BattleTheme.Ink, 0.7f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(2f, 20f), Vector2.zero);
        }

        /// <summary>The stamina there is now, out of the maximum (one dot each).</summary>
        public void SetStamina(int stamina, int max)
        {
            _stamina = Mathf.Max(0, stamina);
            _max = Mathf.Max(0, max);
            Render();
        }

        /// <summary>Where the divider stands: after <paramref name="after"/> dots, or nowhere for 0.</summary>
        public void SetDivider(int after)
        {
            int next = Mathf.Max(0, after);
            if (next == _dividerAfter) return;
            _dividerAfter = next;
            Render();
        }

        /// <summary>
        /// §4.7 / §5.2: while a card is held, the points it would pay turn to rings and "▸ n" shows.
        /// 0 puts both away.
        /// </summary>
        public void ShowCost(int cost)
        {
            _cost = Mathf.Max(0, cost);
            Render();
        }

        private void Render()
        {
            _number.text = _stamina.ToString();
            _pay.enabled = _cost > 0;
            _pay.text = "▸ " + _cost;
            while (_dots.Count < _max) _dots.Add(UiKit.Sprite(_row, "Dot" + _dots.Count, ProceduralArt.Circle, BattleTheme.Amber,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(DotSize, DotSize), Vector2.zero));
            int paying = Mathf.Min(_cost, _stamina);
            float x = 0f;
            for (int i = 0; i < _dots.Count; i++)
            {
                Image dot = _dots[i];
                bool used = i < _max;
                dot.gameObject.SetActive(used);
                if (!used) continue;
                if (_dividerAfter > 0 && i == _dividerAfter)
                {
                    // The divider stands in the gap after the third dot, the gap widened to hold it.
                    _divider.rectTransform.anchoredPosition = new Vector2(x + DotGap * 0.5f, 0f);
                    x += DotGap + 2f;
                }
                dot.rectTransform.anchoredPosition = new Vector2(x, 0f);
                x += DotSize + DotGap;
                if (i < _stamina - paying)
                {
                    dot.sprite = ProceduralArt.Circle; // there now: an amber fill
                    dot.color = BattleTheme.Amber;
                }
                else if (i < _stamina)
                {
                    dot.sprite = VisualArt.CircleRing(3f); // to be paid: an amber 3 px ring
                    dot.color = BattleTheme.Amber;
                }
                else
                {
                    dot.sprite = VisualArt.CircleRing(2f); // empty: a steel 2 px ring
                    dot.color = BattleTheme.Steel;
                }
            }
            _divider.enabled = _dividerAfter > 0 && _max > _dividerAfter;
        }
    }
}
#endif
