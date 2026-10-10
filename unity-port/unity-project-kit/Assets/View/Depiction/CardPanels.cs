// The panels around a hovered hand card (battle-visual-v1, #242): the one line above it (§7.2), the
// reason it cannot be played from here on its right (§5.1, #261), and the detail a right click opens
// above it (§7.4). Every word comes from the card's face (CardFace.HoverLine, ReachHint, Detail); the
// panels only place and print them.
#if UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public sealed class CardPanels
    {
        // §7.2: a 420 px tag above the card, 18 px text, a lamp-like dot on its left.
        private const float HoverWidth = 420f;
        private const float HoverMinHeight = 44f;
        private const float HoverGap = 8f;
        // §5.1: 280×76 on the card's right, its top on the card's top, 12 px apart; on the left past x 1620.
        private static readonly Vector2 ReachSize = new Vector2(280f, 76f);
        private const float ReachGap = 12f;
        private const float ReachRightLimit = 1620f;
        // §7.4: 560×280 straight above the card, 16 px padding, the card at 80% on the left.
        private static readonly Vector2 DetailSize = new Vector2(560f, 280f);
        private const float DetailPad = 16f;
        private const float MiniScale = 0.8f;

        private readonly RectTransform _layer;
        private readonly RectTransform _hover;
        private readonly Image _hoverDot;
        private readonly Text _hoverText;
        private readonly RectTransform _reach;
        private readonly Text _reachText;
        private readonly RectTransform _detail;
        private readonly RectTransform _detailCardSlot;
        private readonly Text _detailName;
        private readonly Text _detailKind;
        private readonly Text _detailBody;
        private readonly RectTransform _detailTerms;
        private readonly Text _detailTermsText;
        private CardView _detailCard;
        private string _detailFor = "";

        public CardPanels(RectTransform canvasRoot)
        {
            UiKit.EnsureFont();
            _layer = UiKit.Rect(canvasRoot, "CardPanels", Vector2.zero, Vector2.one);
            CanvasGroup layerGroup = UiKit.Group(_layer);
            layerGroup.blocksRaycasts = false;
            layerGroup.interactable = false;
            _layer.SetAsLastSibling();

            _hover = UiKit.Point(_layer, "HoverLine", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(HoverWidth, HoverMinHeight), Vector2.zero);
            VisualArt.Panel(_hover, "Back", BattleTheme.PanelOpaque, 6);
            VisualArt.Ring(_hover, "Edge", BattleTheme.Steel, 6, 1);
            _hoverDot = UiKit.Sprite(_hover, "Dot", ProceduralArt.Circle, BattleTheme.Steel, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(12f, 12f), new Vector2(20f, 0f));
            _hoverText = UiKit.Label(_hover, "Text", 18, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(HoverWidth - 36f - 12f, HoverMinHeight), new Vector2(36f, 0f));
            _hover.gameObject.SetActive(false);

            _reach = UiKit.Point(_layer, "ReachHint", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), ReachSize, Vector2.zero);
            VisualArt.Panel(_reach, "Back", BattleTheme.PanelOpaque, 6);
            VisualArt.DashedFrame(_reach, BattleTheme.Whiff, 1f);
            UiKit.Sprite(_reach, "Icon", VisualArt.Icon(VisualIcon.Range), BattleTheme.Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 22f), new Vector2(16f, 0f));
            _reachText = UiKit.Label(_reach, "Text", 18, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(ReachSize.x - 50f - 16f, ReachSize.y - 24f), new Vector2(50f, 0f));
            _reachText.lineSpacing = 26f / (18f * 1.15f);
            _reach.gameObject.SetActive(false);

            _detail = UiKit.Point(_layer, "Detail", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), DetailSize, Vector2.zero);
            VisualArt.Panel(_detail, "Back", BattleTheme.PanelOpaque, 6);
            VisualArt.Ring(_detail, "Edge", BattleTheme.Steel, 6, 2);
            Vector2 mini = CardView.Size * MiniScale;
            _detailCardSlot = UiKit.Point(_detail, "Card", new Vector2(0f, 1f), new Vector2(0f, 1f), mini, new Vector2(DetailPad, -DetailPad));
            float textLeft = DetailPad + mini.x + DetailPad;
            float textWidth = DetailSize.x - textLeft - DetailPad;
            _detailName = UiKit.Label(_detail, "Name", 24, TextAnchor.UpperLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(textWidth, 32f), new Vector2(textLeft, -DetailPad));
            _detailName.fontStyle = FontStyle.Bold;
            _detailKind = UiKit.Label(_detail, "Kind", 16, TextAnchor.UpperLeft, BattleTheme.Ink2, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(textWidth, 22f), new Vector2(textLeft, -DetailPad - 34f));
            _detailBody = UiKit.Label(_detail, "Body", 16, TextAnchor.UpperLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(textWidth, 104f), new Vector2(textLeft, -DetailPad - 60f));
            _detailBody.verticalOverflow = VerticalWrapMode.Truncate;
            // The terms the card uses, in a black 35% box along the bottom.
            _detailTerms = UiKit.Point(_detail, "Terms", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(textWidth, 82f), new Vector2(textLeft, DetailPad));
            UiKit.Image(_detailTerms, "Back", VisualArt.Rounded(4), BattleTheme.WithAlpha(Color.black, 0.35f), Vector2.zero, Vector2.one).type = Image.Type.Sliced;
            _detailTermsText = UiKit.Text(_detailTerms, "Text", 16, TextAnchor.UpperLeft, BattleTheme.Ink);
            _detailTermsText.verticalOverflow = VerticalWrapMode.Truncate;
            _detail.gameObject.SetActive(false);
        }

        public bool DetailShown => _detail.gameObject.activeSelf;

        // ---- §7.2 / §5.1 ------------------------------------------------------------------------

        /// <summary>Places the hover line above <paramref name="card"/> and, when the script says nobody is in reach, the reason on its right.</summary>
        public void ShowHover(CardView card)
        {
            if (!card || card.Face == null)
            {
                HideHover();
                return;
            }
            CardFace face = card.Face;
            Rect box = Bounds(card.Rect);

            bool hasLine = !string.IsNullOrEmpty(face.HoverLine);
            _hover.gameObject.SetActive(hasLine);
            if (hasLine)
            {
                _hoverText.text = face.HoverLine;
                _hoverDot.enabled = !face.Plain;
                _hoverDot.color = card.Lit ? BattleTheme.Wick : BattleTheme.Steel;
                float height = Mathf.Max(HoverMinHeight, _hoverText.preferredHeight + 16f);
                _hover.sizeDelta = new Vector2(HoverWidth, height);
                _hoverText.rectTransform.sizeDelta = new Vector2(HoverWidth - 36f - 12f, height);
                _hover.anchoredPosition = ClampX(new Vector2(box.center.x, box.yMax + HoverGap), HoverWidth);
            }

            bool hasReach = !string.IsNullOrEmpty(face.ReachHint);
            _reach.gameObject.SetActive(hasReach);
            if (hasReach)
            {
                // §5.1: the break goes after 「相手との間合いが」.
                _reachText.text = BreakAfter(face.ReachHint, "が ");
                float limit = Center.x + (ReachRightLimit - BattleTheme.RefWidth * 0.5f);
                bool right = box.xMax + ReachGap + ReachSize.x <= limit;
                _reach.pivot = new Vector2(right ? 0f : 1f, 1f);
                _reach.anchoredPosition = new Vector2(right ? box.xMax + ReachGap : box.xMin - ReachGap, box.yMax);
            }
        }

        public void HideHover()
        {
            _hover.gameObject.SetActive(false);
            _reach.gameObject.SetActive(false);
        }

        // ---- §7.4 ---------------------------------------------------------------------------------

        /// <summary>Opens the detail of <paramref name="card"/> straight above it, or closes it when it is already open for that card.</summary>
        public void ToggleDetail(CardView card, CardView prefab)
        {
            if (DetailShown && card && _detailFor == card.CardId)
            {
                HideDetail();
                return;
            }
            ShowDetail(card, prefab);
        }

        private void ShowDetail(CardView card, CardView prefab)
        {
            if (!card || card.Face == null || card.Face.Detail == null)
            {
                HideDetail();
                return;
            }
            CardDetail detail = card.Face.Detail;
            _detailFor = card.CardId;
            _detail.gameObject.SetActive(true);
            _detailName.text = detail.Name;
            _detailKind.text = detail.KindLine;
            _detailBody.text = detail.Body;
            var terms = new List<string>();
            foreach (TermDefinition term in detail.Terms) terms.Add(term.Word + "：" + term.Meaning);
            _detailTerms.gameObject.SetActive(terms.Count > 0);
            _detailTermsText.text = string.Join("\n", terms);

            if (_detailCard) Object.Destroy(_detailCard.gameObject);
            _detailCard = null;
            if (prefab)
            {
                _detailCard = Object.Instantiate(prefab, _detailCardSlot);
                RectTransform rt = _detailCard.Rect;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.localRotation = Quaternion.identity;
                rt.localScale = new Vector3(MiniScale, MiniScale, 1f);
                _detailCard.Bind(card.Face);
                _detailCard.SetDimmed(false);
                _detailCard.Interactable = false;
                if (_detailCard.group) _detailCard.group.blocksRaycasts = false;
            }

            Rect box = Bounds(card.Rect);
            _detail.anchoredPosition = ClampX(new Vector2(box.center.x, box.yMax + HoverGap), DetailSize.x);
            _hover.gameObject.SetActive(false); // the detail stands where the hover line was
        }

        public void HideDetail()
        {
            _detailFor = "";
            if (_detailCard) Object.Destroy(_detailCard.gameObject);
            _detailCard = null;
            _detail.gameObject.SetActive(false);
        }

        // ---- helpers ------------------------------------------------------------------------------

        private Vector2 Center => _layer.rect.center;

        /// <summary>The card's box in the layer's space (its upright bounds).</summary>
        private Rect Bounds(RectTransform card)
        {
            var corners = new Vector3[4];
            card.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector3 local = _layer.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>Keeps a panel <paramref name="width"/> wide, centred on <paramref name="at"/>, inside the screen with 16 px to spare.</summary>
        private Vector2 ClampX(Vector2 at, float width)
        {
            Rect area = _layer.rect;
            float half = width * 0.5f;
            at.x = Mathf.Clamp(at.x, area.xMin + 16f + half, area.xMax - 16f - half);
            return at;
        }

        private static string BreakAfter(string text, string mark)
        {
            int at = text.IndexOf(mark, System.StringComparison.Ordinal);
            return at < 0 ? text : text.Substring(0, at + 1) + "\n" + text.Substring(at + mark.Length);
        }
    }
}
#endif
