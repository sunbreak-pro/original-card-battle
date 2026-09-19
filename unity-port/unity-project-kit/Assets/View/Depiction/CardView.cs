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
        public Text valueText;
        public GameObject traitBox;
        public Text traitText;
        public Image traitLamp;
        public CanvasGroup group;

        public event Action<CardView, PointerEventData> DragBegan;
        public event Action<CardView, PointerEventData> DragMoved;
        public event Action<CardView, PointerEventData> DragEnded;

        public string CardId { get; private set; }
        public CardFace Face { get; private set; }
        public bool Interactable { get; set; }
        public RectTransform Rect => (RectTransform)transform;

        public void Bind(CardFace face)
        {
            Face = face;
            CardId = face.Id;
            gameObject.name = "Card_" + face.Id;
            if (costText) costText.text = face.Cost.ToString();
            if (nameText) nameText.text = face.Name;
            if (valueText) valueText.text = face.ValueText;
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

        public void SetDimmed(bool dimmed)
        {
            if (group) group.alpha = dimmed ? 0.45f : 1f;
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
