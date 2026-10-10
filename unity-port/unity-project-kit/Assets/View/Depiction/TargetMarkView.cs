// The frame around the figure a held card is aimed at (battle-visual-v1 §5.2 / §5.3, #242): four
// corner hooks and no sides, so nothing covers the figure or its omen. A card that reaches draws them
// in wick with a weak amber light and carries the one predicted value on a tag in the middle of the
// top edge; an enemy the card cannot reach gets steel hooks and no number. Which figure is framed,
// and the value, come from the script (CardFace.Aim / Affects, the source's PreviewFor).
#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public class TargetMarkView : MonoBehaviour
    {
        public CanvasGroup group;
        public Image[] bars = new Image[0];
        [Tooltip("How far the brackets breathe in and out while shown, in pixels. battle-visual-v1 §5.2 draws them still (0).")]
        public float pulsePixels = 0f;
        public float pulseSeconds = 0.9f;

        // §5.2: hooks 26 px long and 3 px thick, the frame 46 px out on the left and right, 22 above, 12 below.
        private const float HookLength = 26f;
        private const float HookThickness = 3f;
        private const float SteelThickness = 2f;
        private static readonly Vector2 OutMin = new Vector2(-46f, -12f);
        private static readonly Vector2 OutMax = new Vector2(46f, 22f);
        private const float TagHeight = 60f;

        public RectTransform Rect => (RectTransform)transform;

        private bool _shown;
        private Vector2 _restMin;
        private Vector2 _restMax;
        private RectTransform _tag;
        private Image _tagIcon;
        private Text _tagText;
        private Image _light;

        private void Awake()
        {
            // The prefab's frame (8 px out, 5 px hooks of 46) takes §5.2's measures.
            Rect.anchorMin = Vector2.zero;
            Rect.anchorMax = Vector2.one;
            Rect.offsetMin = OutMin;
            Rect.offsetMax = OutMax;
            _restMin = Rect.offsetMin;
            _restMax = Rect.offsetMax;
            pulsePixels = 0f;
            BuildParts();
            Hide();
        }

        private void BuildParts()
        {
            _light = UiKit.Image(Rect, "Light", ProceduralArt.Glow, BattleTheme.WithAlpha(BattleTheme.Amber, 0.35f), Vector2.zero, Vector2.one);
            _light.rectTransform.offsetMin = new Vector2(-10f, -10f);
            _light.rectTransform.offsetMax = new Vector2(10f, 10f);
            _light.transform.SetAsFirstSibling();
            foreach (Image bar in bars) SizeHook(bar, HookThickness);

            // §5.2 予測値: one tag on the middle of the top edge; the top hooks show either side of it.
            _tag = UiKit.Point(Rect, "Preview", new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(150f, TagHeight), Vector2.zero);
            VisualArt.Panel(_tag, "Back", BattleTheme.PanelOpaque, 6);
            VisualArt.Ring(_tag, "Edge", BattleTheme.Wick, 6, 2);
            _tagIcon = UiKit.Sprite(_tag, "Icon", null, BattleTheme.Phosphor, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(32f, 32f), new Vector2(14f, 0f));
            _tagIcon.preserveAspect = true;
            _tagText = UiKit.Label(_tag, "Value", 48, TextAnchor.MiddleLeft, BattleTheme.Wick, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(110f, TagHeight), new Vector2(54f, 0f));
            _tagText.fontStyle = FontStyle.Bold;
            _tagText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _tag.gameObject.SetActive(false);
        }

        /// <summary>A hook bar keeps its corner and takes the length and thickness asked.</summary>
        private static void SizeHook(Image bar, float thickness)
        {
            if (!bar) return;
            RectTransform rt = bar.rectTransform;
            bool across = rt.sizeDelta.x >= rt.sizeDelta.y;
            rt.sizeDelta = across ? new Vector2(HookLength, thickness) : new Vector2(thickness, HookLength);
        }

        /// <summary>The frame of the A+ screen, kept for callers that only colour it.</summary>
        public void Show(Color color)
        {
            Show(true, color, "", null, BattleTheme.Ink);
        }

        /// <summary>
        /// §5.2 / §5.3: wick hooks with the light and, when given, the predicted value with its kind's
        /// icon (<paramref name="reaches"/>); steel hooks, no light and no number otherwise.
        /// </summary>
        public void Show(bool reaches, Color hookColor, string preview, Sprite icon, Color iconColor)
        {
            _shown = true;
            if (group) group.alpha = 1f;
            foreach (Image bar in bars)
            {
                if (!bar) continue;
                bar.color = reaches ? hookColor : BattleTheme.Steel;
                SizeHook(bar, reaches ? HookThickness : SteelThickness);
            }
            if (_light) _light.enabled = reaches;
            bool hasPreview = reaches && !string.IsNullOrEmpty(preview);
            if (_tag) _tag.gameObject.SetActive(hasPreview);
            if (hasPreview)
            {
                _tagText.text = preview;
                _tagIcon.sprite = icon;
                _tagIcon.enabled = icon != null;
                _tagIcon.color = iconColor;
                float width = 14f + (icon != null ? 32f + 8f : 0f) + Mathf.Max(30f, _tagText.preferredWidth) + 14f;
                _tag.sizeDelta = new Vector2(width, TagHeight);
                _tagText.rectTransform.anchoredPosition = new Vector2(icon != null ? 54f : 14f, 0f);
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

        /// <summary>Hot = releasing the card now would play it.</summary>
        public void SetHot(bool hot)
        {
            if (group && _shown) group.alpha = hot ? 1f : 0.75f;
        }

        private void Update()
        {
            if (!_shown || pulseSeconds <= 0f || pulsePixels <= 0f) return;
            float wave = (Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / pulseSeconds) + 1f) * 0.5f;
            Vector2 inset = new Vector2(wave * pulsePixels, wave * pulsePixels);
            Rect.offsetMin = _restMin + inset;
            Rect.offsetMax = _restMax - inset;
        }
    }
}
#endif
