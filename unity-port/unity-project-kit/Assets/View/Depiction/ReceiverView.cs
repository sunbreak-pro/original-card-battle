// The receiver: a large dish over a target figure, shown only while a single-target
// card is held. Releasing on it confirms the play. It carries one predicted value.
#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public class ReceiverView : MonoBehaviour
    {
        public CanvasGroup group;
        public Image dish;
        public Image ring;
        public Text previewText;

        public RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            if (dish && dish.sprite == null) dish.sprite = ProceduralArt.SoftCircle;
            if (ring && ring.sprite == null) ring.sprite = ProceduralArt.Circle;
            Hide();
        }

        public void Show(string preview)
        {
            if (previewText) previewText.text = preview;
            if (group) group.alpha = 1f;
            SetHot(false);
        }

        public void Hide()
        {
            if (group) group.alpha = 0f;
        }

        /// <summary>Fades the dish in (EffectId.ReceiverShow). It takes drops from the first frame.</summary>
        public System.Collections.IEnumerator FadeIn(float ms)
        {
            if (!group) yield break;
            yield return UiTween.Fade(group, 0f, 1f, ms, Ease.Out);
        }

        public void SetHot(bool hot)
        {
            if (dish) dish.color = BattleTheme.WithAlpha(BattleTheme.Omen, hot ? 0.55f : 0.28f);
            if (ring) ring.color = BattleTheme.WithAlpha(BattleTheme.Omen, hot ? 0.35f : 0.12f);
            transform.localScale = hot ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
        }

        public bool Contains(Vector2 screenPoint, Camera eventCamera)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(Rect, screenPoint, eventCamera);
        }
    }
}
#endif
