// The receiver: the place over a target figure where a single-target card is released, live only
// while one is held. Since #242 it draws nothing; TargetMarkView frames the figure instead.
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
            // battle-visual-v1 §0.1 decision 10 (#242): the round dish and its number are gone. The
            // receiver stays as the place a card is released on; the frame around the enemy
            // (TargetMarkView) shows who is aimed at and carries the predicted value.
            if (dish) dish.enabled = false;
            if (ring) ring.enabled = false;
            if (previewText) previewText.enabled = false;
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
