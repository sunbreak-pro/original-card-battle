// The throw line: a dotted line above the hand, shown only while a self-target card is
// held. Releasing above it confirms the play. The line's own Y is the threshold, so
// moving the prefab instance in the Editor moves the threshold with it.
#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public class ThrowLineView : MonoBehaviour
    {
        public CanvasGroup group;
        public Image[] dashes = new Image[0];
        public Text previewText;

        public RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
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

        /// <summary>Fades the line in (EffectId.ThrowLineShow). The threshold works from the first frame.</summary>
        public System.Collections.IEnumerator FadeIn(float ms)
        {
            if (!group) yield break;
            yield return UiTween.Fade(group, 0f, 1f, ms, Ease.Out);
        }

        public void SetHot(bool hot)
        {
            Color c = BattleTheme.WithAlpha(hot ? BattleTheme.Guard : BattleTheme.Ink, hot ? 0.95f : 0.5f);
            foreach (Image dash in dashes)
            {
                if (dash) dash.color = c;
            }
            if (previewText) previewText.color = hot ? BattleTheme.Guard : BattleTheme.Ink2;
        }

        public bool IsAbove(Vector2 screenPoint, Camera eventCamera)
        {
            Vector2 lineOnScreen = RectTransformUtility.WorldToScreenPoint(eventCamera, Rect.position);
            return screenPoint.y > lineOnScreen.y;
        }
    }
}
#endif
