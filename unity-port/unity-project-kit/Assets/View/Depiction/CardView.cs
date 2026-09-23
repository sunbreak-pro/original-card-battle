// One hand card. Layout lives in the Card prefab; this only binds a CardFace and
// forwards drag events. It decides nothing about whether the card may be played.
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
            if (costText) costText.text = face.Cost.ToString();
            if (nameText) nameText.text = face.Name;
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
                traitText.text = face.TraitText;
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
