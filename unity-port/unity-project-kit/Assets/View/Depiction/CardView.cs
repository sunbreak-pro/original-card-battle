// One hand card: battle-visual-v1 §6 (案 C, 216×304, #242). This binds a CardFace and forwards drag
// events; it decides nothing about whether the card may be played, and lights the lamp only as the
// script says (CardFace.TraitLit, or CardFace.LitFor while a card frames an enemy).
//
// The Card prefab saved in the Unity project is the 190×260 one from before #242, and rebuilding it
// needs the Editor. So the face is laid out again here at run time (EnsureParts): the prefab's own
// children are moved to their §6.1 places and the parts it lacks are made, the way OmenBadgeView
// does it for the omen badge (#349). DepictionPrefabBuilder builds a new prefab the same way.
#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Depiction.View
{
    public class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Image background;
        public Image kindStripe;
        public Text costText;
        public Text nameText;
        public Text typeText;
        public Text valueText;
        public Text descriptionText;
        public GameObject traitBox;
        public Text traitText;
        public Image traitLamp;
        [Tooltip("Dark sheet drawn over the card when it is dimmed. Without it the card fades through its alpha.")]
        public Image shade;
        public CanvasGroup group;

        // ---- battle-visual-v1 §6.1 (px from the card's top-left) ------------------------------
        public static readonly Vector2 Size = new Vector2(216f, 304f);
        private const float BandLeft = 8f;
        private const float BandTop = 5f;
        private const float FrameWidth = 2f;
        private const float ShadowDrop = 6f;  // 影 0 6px 14px
        private const float ShadowBlur = 14f;
        private const float CostSize = 44f;
        private static readonly Vector2 CostAt = new Vector2(16f, 11f);
        private const float KindIconSize = 24f;
        private const float ReachHeight = 32f;
        private const float ReachRight = 12f;
        private const float ReachTop = 11f;
        private static readonly Vector2 NameAt = new Vector2(16f, 60f);
        private const int NameSize = 22;
        private const float NameHeight = 28f;
        private static readonly Vector2 ValuesAt = new Vector2(16f, 94f);
        private const float ValueColumn = 86f;
        private const float ValueGap = 14f;
        private static readonly Vector2 TextAt = new Vector2(16f, 156f);
        private const int TextSize = 16;
        private const float TextLine = 22f;
        // 特性の箱: left 12, right 7, bottom 7, height 62.
        private const float TraitLeft = 12f;
        private const float TraitRight = 7f;
        private const float TraitBottom = 7f;
        private const float TraitHeight = 62f;
        private const float LampSize = 16f;
        private const float LampOffSize = 10f;

        public event Action<CardView, PointerEventData> DragBegan;
        public event Action<CardView, PointerEventData> DragMoved;
        public event Action<CardView, PointerEventData> DragEnded;

        public string CardId { get; private set; }
        public CardFace Face { get; private set; }
        public bool Interactable { get; set; }
        public bool Dimmed { get; private set; }
        /// <summary>Whether the lamp is lit as the card shows it now (the face's, or the framed enemy's).</summary>
        public bool Lit { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        private bool _built;
        private Image[] _frame = new Image[0];
        private Image[] _litRing = new Image[0];
        private Image _glow;
        private Image _bandLeft;
        private Image _costRing;
        private Image _costFill;
        private RectTransform _costDrop;
        private Text _costDropText;
        private Image _kindIcon;
        private RectTransform _reach;
        private Image _reachIcon;
        private Text _reachText;
        private Text _reachSelf;
        private readonly List<ValueSlot> _values = new List<ValueSlot>();
        private Image _traitBack;
        private Image[] _traitFrame = new Image[0];
        private Image[] _traitDashed = new Image[0];
        private Text _traitBottom;
        private Image _lampGlow;

        private sealed class ValueSlot
        {
            public RectTransform Root;
            public Image Icon;
            public Text Number;
            public Text Word;
        }

        private void Awake()
        {
            EnsureParts();
            if (shade) _shadeAlpha = shade.color.a;
        }

        /// <summary>Lays the face out as §6.1 draws it. Safe to call again; the parts are made once.</summary>
        public void EnsureParts()
        {
            if (_built) return;
            _built = true;
            UiKit.EnsureFont();
            RectTransform root = Rect;
            root.sizeDelta = Size;

            // The lit card's outer light (§6.3), behind everything else.
            _glow = UiKit.Image(root, "LitGlow", ProceduralArt.Glow, BattleTheme.WithAlpha(BattleTheme.Wick, 0.5f), Vector2.zero, Vector2.one);
            _glow.rectTransform.offsetMin = new Vector2(-28f, -28f);
            _glow.rectTransform.offsetMax = new Vector2(28f, 28f);
            _glow.transform.SetAsFirstSibling();
            _glow.enabled = false;

            // §6.1 影: 0 6px 14px rgba(0,0,0,.45), behind the light. §0.1 decision 7: a held card keeps it as it is.
            Image shadow = UiKit.Image(root, "Shadow", VisualArt.SoftShadow(6, (int)ShadowBlur), BattleTheme.WithAlpha(Color.black, 0.45f), Vector2.zero, Vector2.one);
            shadow.type = Image.Type.Sliced;
            shadow.raycastTarget = false;
            shadow.rectTransform.offsetMin = new Vector2(-ShadowBlur, -ShadowBlur - ShadowDrop);
            shadow.rectTransform.offsetMax = new Vector2(ShadowBlur, ShadowBlur - ShadowDrop);
            shadow.transform.SetAsFirstSibling();

            if (background)
            {
                UiKit.Stretch(background.rectTransform);
                background.sprite = VisualArt.Rounded(6);
                background.type = Image.Type.Sliced;
                background.color = BattleTheme.CardGround;
            }

            // The prefab's plain frame (FrameTop...) gives way to one rounded 2 px frame.
            foreach (string edge in new[] { "FrameTop", "FrameBottom", "FrameLeft", "FrameRight" })
            {
                Transform old = root.Find(edge);
                if (old) old.gameObject.SetActive(false);
            }
            _frame = new[] { VisualArt.Ring(root, "Frame", BattleTheme.Steel, 6, (int)FrameWidth) };
            _litRing = new[] { VisualArt.Ring(root, "LitRing", BattleTheme.Wick, 8, 2, 2f) };
            _litRing[0].enabled = false;

            // 属性の帯: 8 px down the left, 5 px across the top.
            _bandLeft = UiKit.Image(root, "BandLeft", ProceduralArt.White, BattleTheme.Steel, new Vector2(0f, 0f), new Vector2(0f, 1f));
            _bandLeft.rectTransform.offsetMin = new Vector2(0f, 2f);
            _bandLeft.rectTransform.offsetMax = new Vector2(BandLeft, -2f);
            if (!kindStripe) kindStripe = UiKit.Image(root, "KindStripe", ProceduralArt.White, BattleTheme.Steel, new Vector2(0f, 1f), new Vector2(1f, 1f));
            RectTransform stripe = kindStripe.rectTransform;
            stripe.anchorMin = new Vector2(0f, 1f);
            stripe.anchorMax = new Vector2(1f, 1f);
            stripe.pivot = new Vector2(0.5f, 1f);
            stripe.offsetMin = new Vector2(2f, -BandTop);
            stripe.offsetMax = new Vector2(-2f, 0f);

            BuildCost(root);

            _kindIcon = UiKit.Sprite(root, "KindIcon", null, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(KindIconSize, KindIconSize), new Vector2(CostAt.x + CostSize + 8f, -(CostAt.y + (CostSize - KindIconSize) * 0.5f)));
            _kindIcon.preserveAspect = true;

            BuildReach(root);

            if (!nameText) nameText = UiKit.Label(root, "Name", NameSize, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            Place(nameText.rectTransform, NameAt, new Vector2(Size.x - NameAt.x - 12f, NameHeight));
            nameText.fontSize = NameSize;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = BattleTheme.Ink;

            // The type line and the single big value are gone from the face (§6.1: the attribute is
            // the icon only; the values are columns).
            if (typeText) typeText.gameObject.SetActive(false);
            if (valueText) valueText.gameObject.SetActive(false);
            for (int i = 0; i < CardFaceLimits.Values; i++) _values.Add(BuildValue(root, i));

            if (!descriptionText) descriptionText = UiKit.Label(root, "Description", TextSize, TextAnchor.UpperLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            Place(descriptionText.rectTransform, TextAt, new Vector2(Size.x - TextAt.x - 12f, TextLine * 3f));
            descriptionText.fontSize = TextSize;
            descriptionText.alignment = TextAnchor.UpperLeft;
            descriptionText.color = BattleTheme.Ink;
            descriptionText.lineSpacing = TextLine / (TextSize * 1.15f);
            descriptionText.horizontalOverflow = HorizontalWrapMode.Overflow;
            descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
            descriptionText.supportRichText = true; // the ∞ at the head of a lasting row takes its own colour

            BuildTrait(root);

            if (shade)
            {
                UiKit.Stretch(shade.rectTransform);
                shade.sprite = VisualArt.Rounded(6);
                shade.type = Image.Type.Sliced;
                shade.color = BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.6f); // §6.2: 黒 60% の板
                shade.transform.SetAsLastSibling();
            }
        }

        private void BuildCost(RectTransform root)
        {
            RectTransform cost;
            if (costText && costText.rectTransform.parent != root) cost = (RectTransform)costText.rectTransform.parent;
            else cost = UiKit.Point(root, "Cost", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            Place(cost, CostAt, new Vector2(CostSize, CostSize));
            _costRing = cost.GetComponent<Image>();
            if (!_costRing) _costRing = cost.gameObject.AddComponent<Image>();
            // §6.1: a 2 px amber ring over a black 45% fill (the Fill below), not an amber disc. A rounded
            // ring whose corner radius is half its side is a circle, sliced so the 2 px hold at 44 px.
            _costRing.sprite = VisualArt.RoundedRing(Mathf.RoundToInt(CostSize * 0.5f), 2);
            _costRing.type = Image.Type.Sliced;
            _costRing.color = BattleTheme.Amber;
            _costRing.raycastTarget = false;
            _costFill = UiKit.Sprite(cost, "Fill", ProceduralArt.Circle, BattleTheme.WithAlpha(Color.black, 0.45f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(CostSize - 4f, CostSize - 4f), Vector2.zero);
            _costFill.transform.SetAsFirstSibling();
            if (!costText) costText = UiKit.Label(cost, "CostText", 28, TextAnchor.MiddleCenter, BattleTheme.Amber, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            costText.rectTransform.sizeDelta = new Vector2(CostSize, CostSize);
            costText.rectTransform.anchoredPosition = Vector2.zero;
            costText.fontSize = 28;
            costText.fontStyle = FontStyle.Bold;
            costText.transform.SetAsLastSibling();

            // ▼1: the tag at the circle's lower right when a discount took a cost off.
            _costDrop = UiKit.Point(cost, "Drop", new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(30f, 18f), new Vector2(-2f, 2f));
            VisualArt.Panel(_costDrop, "Back", BattleTheme.PanelOpaque, 3);
            VisualArt.Ring(_costDrop, "Edge", BattleTheme.Amber, 3, 1);
            _costDropText = UiKit.Label(_costDrop, "Text", 14, TextAnchor.MiddleCenter, BattleTheme.Ink, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 18f), Vector2.zero);
            _costDrop.gameObject.SetActive(false);
        }

        private void BuildReach(RectTransform root)
        {
            // 届く間合い: the number tag at the top right (⊢⊣ 1〜2), or the self mark (人型 + 自分).
            _reach = UiKit.Point(root, "Reach", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(84f, ReachHeight), new Vector2(-ReachRight, -ReachTop));
            VisualArt.Panel(_reach, "Back", BattleTheme.PanelOpaque, 4);
            VisualArt.Ring(_reach, "Edge", BattleTheme.Steel, 4, 1);
            _reachIcon = UiKit.Sprite(_reach, "Icon", VisualArt.Icon(VisualIcon.Range), BattleTheme.Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(18f, 18f), new Vector2(8f, 0f));
            _reachText = UiKit.Label(_reach, "Text", 20, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(58f, ReachHeight), new Vector2(30f, 0f));
            _reachText.fontStyle = FontStyle.Bold;
            _reachText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _reachSelf = UiKit.Label(_reach, "Self", 14, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(40f, ReachHeight), new Vector2(34f, 0f), "自分");
        }

        private ValueSlot BuildValue(RectTransform root, int index)
        {
            var slot = new ValueSlot();
            slot.Root = UiKit.Point(root, "Value" + index, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ValueColumn, 60f),
                new Vector2(ValuesAt.x + index * (ValueColumn + ValueGap), -ValuesAt.y));
            slot.Icon = UiKit.Sprite(slot.Root, "Icon", null, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, 24f), new Vector2(0f, -8f));
            slot.Icon.preserveAspect = true;
            slot.Number = UiKit.Label(slot.Root, "Number", 36, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(ValueColumn - 28f, 40f), new Vector2(28f, 0f));
            slot.Number.fontStyle = FontStyle.Bold;
            slot.Number.horizontalOverflow = HorizontalWrapMode.Overflow;
            slot.Word = UiKit.Label(slot.Root, "Word", 16, TextAnchor.UpperLeft, BattleTheme.Ink2, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(ValueColumn, 20f), new Vector2(28f, -40f));
            slot.Word.horizontalOverflow = HorizontalWrapMode.Overflow;
            slot.Root.gameObject.SetActive(false);
            return slot;
        }

        private void BuildTrait(RectTransform root)
        {
            RectTransform box;
            if (traitBox) box = (RectTransform)traitBox.transform;
            else
            {
                box = UiKit.Rect(root, "Trait", Vector2.zero, Vector2.one);
                traitBox = box.gameObject;
            }
            box.anchorMin = new Vector2(0f, 0f);
            box.anchorMax = new Vector2(1f, 0f);
            box.pivot = new Vector2(0.5f, 0f);
            box.offsetMin = new Vector2(TraitLeft, TraitBottom);
            box.offsetMax = new Vector2(-TraitRight, TraitBottom + TraitHeight);

            _traitBack = box.GetComponent<Image>();
            if (!_traitBack) _traitBack = box.gameObject.AddComponent<Image>();
            _traitBack.sprite = VisualArt.Rounded(4);
            _traitBack.type = Image.Type.Sliced;
            _traitBack.color = BattleTheme.WithAlpha(Color.black, 0.34f);
            _traitBack.raycastTarget = false;
            _traitFrame = new[] { VisualArt.Ring(box, "Edge", BattleTheme.Line, 4, 1) };
            _traitDashed = VisualArt.DashedFrame(box, BattleTheme.Steel, 1f);

            // The lamp sits in a 16 px place at the left; lit, it carries a 10 px light.
            _lampGlow = UiKit.Sprite(box, "LampGlow", ProceduralArt.Glow, BattleTheme.WithAlpha(BattleTheme.Wick, 0.6f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(LampSize + 20f, LampSize + 20f), new Vector2(8f + LampSize * 0.5f, 0f));
            if (!traitLamp)
            {
                traitLamp = UiKit.Sprite(box, "Lamp", ProceduralArt.Circle, BattleTheme.Steel, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            }
            traitLamp.sprite = ProceduralArt.Circle;
            RectTransform lamp = traitLamp.rectTransform;
            lamp.anchorMin = lamp.anchorMax = new Vector2(0f, 0.5f);
            lamp.pivot = new Vector2(0.5f, 0.5f);
            lamp.anchoredPosition = new Vector2(8f + LampSize * 0.5f, 0f);
            traitLamp.transform.SetAsLastSibling();

            float textLeft = 8f + LampSize + 8f;
            float textWidth = Size.x - TraitLeft - TraitRight - textLeft - 4f;
            if (!traitText) traitText = UiKit.Label(box, "TraitText", TextSize, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            RectTransform top = traitText.rectTransform;
            top.anchorMin = top.anchorMax = new Vector2(0f, 1f);
            top.pivot = new Vector2(0f, 1f);
            top.sizeDelta = new Vector2(textWidth, 26f);
            top.anchoredPosition = new Vector2(textLeft, -6f);
            traitText.fontSize = TextSize;
            traitText.alignment = TextAnchor.MiddleLeft;
            traitText.horizontalOverflow = HorizontalWrapMode.Overflow;
            traitText.verticalOverflow = VerticalWrapMode.Overflow;
            _traitBottom = UiKit.Label(box, "TraitEffect", TextSize, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(textWidth, 26f), new Vector2(textLeft, -32f));
            _traitBottom.fontStyle = FontStyle.Bold;
            _traitBottom.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        /// <summary>Moves a child to (x, y) px from the card's top-left, with its own top-left as the pivot.</summary>
        private static void Place(RectTransform rt, Vector2 at, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(at.x, -at.y);
        }

        // ---- bind ---------------------------------------------------------------------------------

        public void Bind(CardFace face)
        {
            EnsureParts();
            Face = face;
            CardId = face.Id;
            gameObject.name = "Card_" + face.Id;
            Color frame = BattleTheme.KindFrame(face.Kind);
            Color ink = BattleTheme.KindInk(face.Kind);

            if (kindStripe) kindStripe.color = frame;
            if (_bandLeft) _bandLeft.color = frame;
            if (_kindIcon)
            {
                _kindIcon.sprite = VisualArt.KindIcon(face.Kind);
                _kindIcon.color = ink;
            }
            BindCost(face);
            BindReach(face);
            if (nameText)
            {
                RectTransform box = nameText.rectTransform;
                Print(nameText, CardTextFit.Name(face.Name, NameSize, MinFontSize, box.rect.width, box.rect.height, MeasureWith(nameText)));
            }
            BindValues(face);
            BindText(face);
            BindTrait(face);
            ShowLamp(face.TraitLit);
        }

        private void BindCost(CardFace face)
        {
            if (costText) costText.text = face.Cost.ToString();
            int drop = face.CostDrop;
            // §6.1: a lowered cost fills the circle with amber and writes the number in the card's ground colour.
            if (_costFill) _costFill.color = drop > 0 ? BattleTheme.Amber : BattleTheme.WithAlpha(Color.black, 0.45f);
            if (costText) costText.color = drop > 0 ? BattleTheme.CardGround : BattleTheme.Amber;
            if (_costDrop) _costDrop.gameObject.SetActive(drop > 0);
            if (_costDropText) _costDropText.text = "▼" + drop;
        }

        private void BindReach(CardFace face)
        {
            if (!_reach) return;
            bool aimsAtSomeone = face.Aim == CardAim.Single || !string.IsNullOrEmpty(face.RequiredRangeGlyph);
            _reach.gameObject.SetActive(true);
            if (aimsAtSomeone && !string.IsNullOrEmpty(face.RequiredRangeGlyph))
            {
                _reachIcon.sprite = VisualArt.Icon(VisualIcon.Range);
                _reachIcon.rectTransform.sizeDelta = new Vector2(18f, 18f);
                _reachText.gameObject.SetActive(true);
                _reachSelf.gameObject.SetActive(false);
                _reachText.text = face.RequiredRangeGlyph;
                _reach.sizeDelta = new Vector2(8f + 18f + 4f + Mathf.Max(20f, _reachText.preferredWidth) + 8f, ReachHeight);
            }
            else if (face.Aim == CardAim.Self)
            {
                _reachIcon.sprite = VisualArt.Icon(VisualIcon.Self);
                _reachIcon.rectTransform.sizeDelta = new Vector2(22f, 22f);
                _reachText.gameObject.SetActive(false);
                _reachSelf.gameObject.SetActive(true);
                _reach.sizeDelta = new Vector2(8f + 22f + 4f + 30f + 8f, ReachHeight);
            }
            else _reach.gameObject.SetActive(false);
        }

        private void BindValues(CardFace face)
        {
            List<CardValue> values = face.Values;
            // A v4.2 source writes one value string only: it stands as the one column.
            if ((values == null || values.Count == 0) && !string.IsNullOrEmpty(face.ValueText) && !face.Lasting)
            {
                values = new List<CardValue>
                {
                    new CardValue { Kind = face.Kind == CardKind.Guard ? CardValueKind.Guard : CardValueKind.Power, Number = face.ValueText, Word = "" },
                };
            }
            for (int i = 0; i < _values.Count; i++)
            {
                ValueSlot slot = _values[i];
                bool used = values != null && i < values.Count;
                slot.Root.gameObject.SetActive(used);
                if (!used) continue;
                CardValue value = values[i];
                Color color = VisualArt.ValueColor(value.Kind);
                slot.Icon.sprite = VisualArt.ValueIcon(value.Kind);
                slot.Icon.color = color;
                slot.Number.text = value.Number;
                slot.Number.color = color;
                slot.Word.text = value.Word;
            }
        }

        private void BindText(CardFace face)
        {
            if (!descriptionText) return;
            List<string> rows = face.TextLines;
            string text;
            if (rows != null && rows.Count > 0)
            {
                var lines = new List<string>();
                foreach (string row in rows)
                {
                    string line = row.Replace(' ', ' ');
                    lines.Add(face.Lasting ? "<color=#" + ColorUtility.ToHtmlStringRGB(BattleTheme.HpInk) + ">∞</color> " + line : line);
                }
                text = string.Join("\n", lines);
            }
            else
            {
                // UI Text breaks lines only at spaces and knows no Japanese line-breaking rules, so
                // each sentence of a v4.2 description gets its own line instead.
                text = face.Description.Replace(' ', ' ').Replace("。", "。\n").TrimEnd('\n');
            }
            descriptionText.text = text;
            // §6.1 スタンスの札: the lasting rows start where the value columns would.
            bool lasting = face.Lasting && rows != null && rows.Count > 0;
            Place(descriptionText.rectTransform, lasting ? ValuesAt : TextAt, new Vector2(Size.x - TextAt.x - 12f, TextLine * (lasting ? 4f : 3f)));
        }

        private void BindTrait(CardFace face)
        {
            string top = face.TraitTop;
            string bottom = face.TraitBottom;
            // A v4.2 source writes the trait as one line.
            if (string.IsNullOrEmpty(top) && string.IsNullOrEmpty(bottom) && !string.IsNullOrEmpty(face.TraitText))
            {
                top = face.TraitText;
                bottom = "";
            }
            bool hasBox = !string.IsNullOrEmpty(top) || !string.IsNullOrEmpty(bottom);
            if (traitBox) traitBox.SetActive(hasBox);
            if (!hasBox) return;
            bool plain = face.Plain;
            // §6.1 素直な札: a dashed frame, no lamp, both rows in the note colour.
            foreach (Image edge in _traitFrame) edge.enabled = !plain;
            foreach (Image edge in _traitDashed) edge.enabled = plain;
            if (traitText)
            {
                traitText.text = top ?? "";
                traitText.color = plain ? BattleTheme.Ink2 : BattleTheme.Ink;
                traitText.fontStyle = FontStyle.Normal;
            }
            if (_traitBottom)
            {
                _traitBottom.text = bottom ?? "";
                _traitBottom.color = plain ? BattleTheme.Ink2 : BattleTheme.Ink;
                _traitBottom.fontStyle = plain ? FontStyle.Normal : FontStyle.Bold;
            }
        }

        /// <summary>
        /// §6.3: lights or puts out the lamp as the script says — the face's own lamp in the hand, or
        /// the framed enemy's verdict (CardFace.LitFor) while the card is held. Lit, the whole edge
        /// turns to wick and takes the outer light; a plain card never lights.
        /// </summary>
        public void ShowLamp(bool lit)
        {
            if (Face == null) return;
            bool hasLamp = !Face.Plain && traitBox && traitBox.activeSelf;
            Lit = lit && hasLamp;
            if (traitLamp)
            {
                traitLamp.enabled = hasLamp;
                float size = Lit ? LampSize : LampOffSize;
                traitLamp.rectTransform.sizeDelta = new Vector2(size, size);
                traitLamp.color = Lit ? BattleTheme.Wick : BattleTheme.Steel;
            }
            if (_lampGlow) _lampGlow.enabled = Lit;
            Color frame = Lit ? BattleTheme.Wick : BattleTheme.KindFrame(Face.Kind);
            foreach (Image edge in _frame) edge.color = frame;
            foreach (Image ring in _litRing) ring.enabled = Lit;
            if (_glow) _glow.enabled = Lit;
        }

        /// <summary>
        /// Darkened but opaque and still readable: a dimmed card can be picked up to learn why it cannot
        /// be played. Fading through the alpha would let the overlapped neighbour show through.
        /// </summary>
        public void SetDimmed(bool dimmed)
        {
            Dimmed = dimmed;
            _dimTween = 0;
            if (shade)
            {
                shade.enabled = dimmed;
                shade.color = BattleTheme.WithAlpha(shade.color, _shadeAlpha);
            }
            else if (group) group.alpha = dimmed ? 0.55f : 1f;
        }

        /// <summary>
        /// The same change, faded over <paramref name="ms"/> (EffectId.UnpayableDim). A newer call wins
        /// over one still running, so a card that flips twice in a row ends in the last state asked.
        /// </summary>
        public System.Collections.IEnumerator FadeDimmed(bool dimmed, float ms)
        {
            Dimmed = dimmed;
            int tween = ++_dimTween;
            if (shade)
            {
                shade.enabled = true;
                float from = shade.color.a;
                float to = dimmed ? _shadeAlpha : 0f;
                yield return UiTween.Run(ms, Ease.Linear, t =>
                {
                    if (shade && tween == _dimTween) shade.color = BattleTheme.WithAlpha(shade.color, Mathf.Lerp(from, to, t));
                });
                if (shade && tween == _dimTween)
                {
                    shade.enabled = dimmed;
                    shade.color = BattleTheme.WithAlpha(shade.color, _shadeAlpha);
                }
                yield break;
            }
            if (!group) yield break;
            float alphaFrom = group.alpha;
            float alphaTo = dimmed ? 0.55f : 1f;
            yield return UiTween.Run(ms, Ease.Linear, t => { if (group && tween == _dimTween) group.alpha = Mathf.Lerp(alphaFrom, alphaTo, t); });
        }

        private float _shadeAlpha = 0.6f;
        private int _dimTween;

        // ---- fitting the name (#210) ----------------------------------------------------------------

        private const int MinFontSize = 12;

        /// <summary>
        /// Prints a fit without wrapping. Only a text that overflows even at the smallest size wraps,
        /// and then it is clipped at its box rather than drawn over the line below.
        /// </summary>
        private static void Print(Text text, TextFit fit)
        {
            text.text = fit.Text;
            text.fontSize = fit.Size;
            text.horizontalOverflow = fit.Fits ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
            text.verticalOverflow = fit.Fits ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
        }

        /// <summary>Unity's own measure with the text's font: one line per "\n", nothing wrapped.</summary>
        private static TextMeasure MeasureWith(Text text)
        {
            return (string s, int size, out float width, out float height) =>
            {
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.text = s;
                text.fontSize = size;
                width = text.preferredWidth;
                height = text.preferredHeight;
            };
        }

        /// <summary>§2.2: the colour role of a card's attribute, for the parts outside the card (the target frame).</summary>
        public static Color KindColor(CardKind kind)
        {
            return BattleTheme.KindFrame(kind);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Interactable) DragBegan?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Interactable) DragMoved?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (Interactable) DragEnded?.Invoke(this, eventData);
        }
    }

    /// <summary>battle-visual-v1 §6.1's counts the View lays out room for.</summary>
    public static class CardFaceLimits
    {
        /// <summary>§6.1: two value columns at most.</summary>
        public const int Values = 2;
    }
}
#endif
