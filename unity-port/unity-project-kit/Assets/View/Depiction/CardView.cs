// One hand card. Layout lives in the Card prefab; this binds a CardFace, shrinks the name and
// the trait line to fit their boxes (CardTextFit), and forwards drag events. It decides nothing
// about whether the card may be played.
#if UNITY_2021_2_OR_NEWER
using System;
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
        [Tooltip("In the fanned hand the right neighbour covers each card from about this far in (px from the card's left edge, down at the trait line), so the trait text ends before it.")]
        public float traitVisibleRight = 150f;

        public event Action<CardView, PointerEventData> DragBegan;
        public event Action<CardView, PointerEventData> DragMoved;
        public event Action<CardView, PointerEventData> DragEnded;

        public string CardId { get; private set; }
        public CardFace Face { get; private set; }
        public bool Interactable { get; set; }
        public bool Dimmed { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        public void Bind(CardFace face)
        {
            Face = face;
            CardId = face.Id;
            gameObject.name = "Card_" + face.Id;
            RememberPrefabSizes();
            if (costText) costText.text = face.Cost.ToString();
            if (nameText)
            {
                RectTransform box = nameText.rectTransform;
                Print(nameText, CardTextFit.Name(face.Name, _nameSize, MinFontSize, box.rect.width, box.rect.height, MeasureWith(nameText)));
            }
            if (typeText)
            {
                typeText.text = face.TypeLabel;
                typeText.color = KindColor(face.Kind);
            }
            if (valueText) valueText.text = face.ValueText;
            // UI Text breaks lines only at spaces and knows no Japanese line-breaking rules, so a line
            // could start with "。" or split "Guard". Each sentence gets its own line instead, and the
            // spaces inside a sentence are made non-breaking.
            if (descriptionText) descriptionText.text = face.Description.Replace(' ', '\u00A0').Replace("。", "。\n").TrimEnd('\n');
            if (kindStripe) kindStripe.color = KindColor(face.Kind);
            if (valueText) valueText.color = KindColor(face.Kind);
            bool hasTrait = !string.IsNullOrEmpty(face.TraitText);
            if (traitBox) traitBox.SetActive(hasTrait);
            if (traitText)
            {
                if (hasTrait)
                {
                    float height = traitText.rectTransform.rect.height;
                    Print(traitText, CardTextFit.Trait(face.TraitText, _traitSize, MinFontSize, TraitWidth(), height, MeasureWith(traitText)));
                }
                else traitText.text = "";
                traitText.color = face.TraitLit ? BattleTheme.Warm : BattleTheme.Ink2;
            }
            if (traitLamp)
            {
                traitLamp.enabled = hasTrait;
                traitLamp.color = face.TraitLit ? BattleTheme.Warm : BattleTheme.WithAlpha(BattleTheme.Ink2, 0.35f);
            }
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

        private float _shadeAlpha = 0.5f;
        private int _dimTween;

        // ---- fitting the name and the trait line (#210) ----------------------------------------

        private const int MinFontSize = 12;
        // The prefab's font sizes, read once: a card bound again starts from them, not from its last fit.
        private int _nameSize;
        private int _traitSize;

        private void RememberPrefabSizes()
        {
            if (_nameSize == 0 && nameText) _nameSize = nameText.fontSize;
            if (_traitSize == 0 && traitText) _traitSize = traitText.fontSize;
        }

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

        /// <summary>The trait text's box, cut where the right neighbour in the fan starts covering the card.</summary>
        private float TraitWidth()
        {
            RectTransform box = traitText.rectTransform;
            Vector3 boxLeft = box.TransformPoint(new Vector3(box.rect.xMin, 0f, 0f));
            float fromCardLeft = Rect.InverseTransformPoint(boxLeft).x - Rect.rect.xMin;
            return Mathf.Min(box.rect.width, traitVisibleRight - fromCardLeft);
        }

        public static Color KindColor(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Attack: return BattleTheme.Omen;
                case CardKind.Guard: return BattleTheme.Guard;
                case CardKind.Stance: return BattleTheme.Warm;
                case CardKind.Skill: return BattleTheme.Accent;
                default: return BattleTheme.Ink;
            }
        }

        private void Awake()
        {
            if (traitLamp && traitLamp.sprite == null) traitLamp.sprite = ProceduralArt.Circle;
            if (shade) _shadeAlpha = shade.color.a;
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
}
#endif
