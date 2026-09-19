// Square corner brackets around a figure, shown only while a throw-line card is held.
// They answer "who does this card land on" - the throw line itself only says where to
// release. Which figure gets the mark comes from the script (CardFace.Affects).
#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public class TargetMarkView : MonoBehaviour
    {
        public CanvasGroup group;
        public Image[] bars = new Image[0];
        [Tooltip("How far the brackets breathe in and out while shown, in pixels.")]
        public float pulsePixels = 6f;
        public float pulseSeconds = 0.9f;

        public RectTransform Rect => (RectTransform)transform;

        private bool _shown;
        private Vector2 _restMin;
        private Vector2 _restMax;

        private void Awake()
        {
            _restMin = Rect.offsetMin;
            _restMax = Rect.offsetMax;
            Hide();
        }

        public void Show(Color color)
        {
            _shown = true;
            if (group) group.alpha = 1f;
            foreach (Image bar in bars)
            {
                if (bar) bar.color = color;
            }
            SetHot(false);
        }

        public void Hide()
        {
            _shown = false;
            if (group) group.alpha = 0f;
            Rect.offsetMin = _restMin;
            Rect.offsetMax = _restMax;
        }

        /// <summary>Hot = the card is above the throw line and releasing it would play it.</summary>
        public void SetHot(bool hot)
        {
            if (group && _shown) group.alpha = hot ? 1f : 0.55f;
        }

        private void Update()
        {
            if (!_shown || pulseSeconds <= 0f) return;
            float wave = (Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / pulseSeconds) + 1f) * 0.5f;
            Vector2 inset = new Vector2(wave * pulsePixels, wave * pulsePixels);
            Rect.offsetMin = _restMin + inset;
            Rect.offsetMax = _restMax - inset;
        }
    }
}
#endif
