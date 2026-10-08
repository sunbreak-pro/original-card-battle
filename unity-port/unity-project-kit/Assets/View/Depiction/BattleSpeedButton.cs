// The battle-speed switch (#348), in the place and shape battle-visual-v1 §4.1 gives it (#389): top
// right, 12 left of the journal button, 120 x 56. It shows the speed's icon (one, two or three
// right-pointing chevrons, 付録 A's placeholder lines until the UI art of #92) and the number
// ("×1.25"), with no word. Hovering lights the frame, washes the face white 10% and opens a tooltip
// 8 under the button, right-aligned. It is never dimmed and never made non-interactable, so it
// stays pressable on the enemy's turn too (unlike ターン終了, §4.9).
//
// Built in code with UiKit on the battle canvas. The box comes from Script/CornerLayout, the number,
// chevron count and tooltip line from Script/BattleSpeed; the button computes nothing. The colours
// are battle-visual-v1 §2.1's tokens, written here until BattleTheme takes them on (#241 / #242), and
// the text is the built-in font until the typefaces arrive (IBM Plex Mono 600 for the number).
#if UNITY_2021_2_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Depiction.View
{
    /// <summary>One press moves the battle speed on: ×1.0 → ×1.25 → ×1.5 → ×1.0.</summary>
    public sealed class BattleSpeedButton
    {
        // battle-visual-v1 §2.1.
        private static readonly Color PanelColor = BattleTheme.Hex("#10141e", 0.88f); // rgba(16,20,30,.88)
        private static readonly Color OpaquePanel = BattleTheme.Hex("#141925");
        private static readonly Color Ink = BattleTheme.Hex("#ecebf2");
        private static readonly Color Rule = BattleTheme.Hex("#9da6ba", 0.26f); // rgba(157,166,186,.26)
        private static readonly Color DullSteel = BattleTheme.Hex("#6d7787");
        private static readonly Color HoverWash = new Color(1f, 1f, 1f, 0.10f);

        private const int NumberSize = 20;
        private const int TooltipSize = 16;

        private readonly GameObject _button;
        private readonly Image _icon;
        private readonly Text _label;
        private readonly Image _frame;
        private readonly GameObject _wash;
        private readonly GameObject _tooltip;

        public BattleSpeedButton(RectTransform parent, Action cycle)
        {
            ScreenBox box = CornerLayout.SpeedButton;
            Button button = UiKit.Button(parent, "BattleSpeed", "", () => cycle(), PanelColor, Ink, NumberSize,
                new Vector2(1f, 1f), new Vector2(1f, 1f));
            // No tint on hover or press: the hover look is §4.1's own, and the button itself does not move.
            button.transition = Selectable.Transition.None;
            var rt = (RectTransform)button.transform;
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(box.Width, box.Height);
            rt.anchoredPosition = new Vector2(-box.FromRight, -box.Y);
            Rounded(button.GetComponent<Image>(), RoundedArt.Fill);
            _button = button.gameObject;

            Image wash = UiKit.Image(rt, "Hover", RoundedArt.Fill, HoverWash, Vector2.zero, Vector2.one);
            Rounded(wash, RoundedArt.Fill);
            _wash = wash.gameObject;
            _wash.SetActive(false);
            _frame = UiKit.Image(rt, "Frame", RoundedArt.Ring, Rule, Vector2.zero, Vector2.one);
            Rounded(_frame, RoundedArt.Ring);

            _icon = UiKit.Sprite(rt, "Icon", null, Ink, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(CornerLayout.SpeedIconSize, CornerLayout.SpeedIconSize), new Vector2(CornerLayout.SpeedPadding, 0f));
            _icon.preserveAspect = true;

            // UiKit.Button's own "Label" (the PlayMode test reads it by that name) becomes the number.
            _label = button.GetComponentInChildren<Text>();
            var labelRect = _label.rectTransform;
            labelRect.SetAsLastSibling();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(CornerLayout.SpeedPadding + CornerLayout.SpeedIconSize + CornerLayout.SpeedIconGap, 0f);
            labelRect.offsetMax = new Vector2(-CornerLayout.SpeedPadding, 0f);
            _label.alignment = TextAnchor.MiddleLeft;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;

            _tooltip = BuildTooltip(rt);
            UiKit.OnPointer(_button, EventTriggerType.PointerEnter, _ => SetHover(true));
            UiKit.OnPointer(_button, EventTriggerType.PointerExit, _ => SetHover(false));
        }

        /// <summary>Writes the speed in force on the button: the number (BattleSpeed.Label) and the chevrons (BattleSpeed.Chevrons).</summary>
        public void Show(string label, int chevrons)
        {
            if (_label) _label.text = label;
            if (_icon) _icon.sprite = ChevronArt.Of(chevrons);
        }

        public bool Visible
        {
            get { return _button.activeSelf; }
            set
            {
                // Hidden mid-hover (the battle ended under the pointer) must not come back lit.
                if (!value) SetHover(false);
                _button.SetActive(value);
            }
        }

        private void SetHover(bool on)
        {
            if (_wash) _wash.SetActive(on);
            if (_frame) _frame.color = on ? Ink : Rule;
            if (_tooltip) _tooltip.SetActive(on);
        }

        /// <summary>The tooltip: an opaque panel with a dull-steel 1 px frame, 8 under the button, right edges aligned.</summary>
        private static GameObject BuildTooltip(RectTransform button)
        {
            RectTransform tip = UiKit.Point(button, "Tooltip", new Vector2(1f, 0f), new Vector2(1f, 1f), Vector2.zero,
                new Vector2(0f, -CornerLayout.SpeedTooltipGap));
            Image back = tip.gameObject.AddComponent<Image>();
            back.color = OpaquePanel;
            back.raycastTarget = false; // the pointer stays on the button while the tooltip is open
            Rounded(back, RoundedArt.Fill);
            Image frame = UiKit.Image(tip, "Frame", RoundedArt.Ring, DullSteel, Vector2.zero, Vector2.one);
            Rounded(frame, RoundedArt.Ring);

            Text line = UiKit.Text(tip, "Text", TooltipSize, TextAnchor.MiddleCenter, Ink, BattleSpeed.Tooltip);
            line.horizontalOverflow = HorizontalWrapMode.Overflow;
            line.rectTransform.offsetMin = new Vector2(CornerLayout.SpeedTooltipPadX, CornerLayout.SpeedTooltipPadY);
            line.rectTransform.offsetMax = new Vector2(-CornerLayout.SpeedTooltipPadX, -CornerLayout.SpeedTooltipPadY);
            ScreenBox box = CornerLayout.SpeedTooltip(Mathf.Ceil(line.preferredWidth), Mathf.Ceil(line.preferredHeight));
            tip.sizeDelta = new Vector2(box.Width, box.Height);
            tip.gameObject.SetActive(false);
            return tip.gameObject;
        }

        /// <summary>A 9-sliced rounded sprite: the 6 px corners keep their size whatever the box.</summary>
        private static void Rounded(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }
    }

    /// <summary>
    /// Rounded-corner sprites for §3.2's 6 px radius: a filled face and a 1 px ring. Drawn in code,
    /// like ProceduralArt, and 9-sliced so one small texture serves any box.
    /// </summary>
    internal static class RoundedArt
    {
        private const int Size = 16;
        private const float Radius = 6f;
        private const float Border = 7f;
        private static Sprite _fill;
        private static Sprite _ring;

        // Domain reload on entering Play Mode is off, so the statics outlive the sprites that stopping
        // Play Mode destroys; Unity's own null check sees through that (ProceduralArt.Cached).
        public static Sprite Fill => (_fill && _fill.texture) ? _fill : (_fill = Make(d => Coverage(d)));
        public static Sprite Ring => (_ring && _ring.texture) ? _ring : (_ring = Make(d => Coverage(d) - Coverage(d + 1f)));

        /// <summary>How much of a pixel lies inside an edge <paramref name="distance"/> px away (negative = inside).</summary>
        private static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

        /// <summary>The signed distance from a pixel centre to the rounded square's edge.</summary>
        private static float Distance(float x, float y)
        {
            float half = Size / 2f;
            float qx = Mathf.Abs(x - half) - (half - Radius);
            float qy = Mathf.Abs(y - half) - (half - Radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - Radius;
        }

        private static Sprite Make(Func<float, float> alpha)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha(Distance(x + 0.5f, y + 0.5f)));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(Border, Border, Border, Border));
        }
    }

    /// <summary>
    /// The speed icon, 付録 A 「速さ」: one, two or three right-pointing chevrons in the 24 x 24 box,
    /// 2.2 px lines with round ends, in white so the Image tints it. Placeholder until #92.
    /// </summary>
    internal static class ChevronArt
    {
        private const int Scale = 2;
        private const int Size = 24 * Scale;
        private const float HalfWidth = 1.1f;
        private static readonly Sprite[] Shapes = new Sprite[4];

        // 付録 A, in the SVG's own 24 x 24 coordinates (y counted downwards): each chevron is the
        // polyline (x0, 6) → (tip, 12) → (x0, 18).
        //   ×1.0  M9 6l6 6-6 6
        //   ×1.25 M6 6l6 6-6 6M12 6l6 6-6 6
        //   ×1.5  M4 6l5 6-5 6M10 6l5 6-5 6M16 6l5 6-5 6
        private static readonly float[][] Chevrons =
        {
            new float[0],
            new[] { 9f, 15f },
            new[] { 6f, 12f, 12f, 18f },
            new[] { 4f, 9f, 10f, 15f, 16f, 21f },
        };

        /// <summary>The icon with <paramref name="count"/> chevrons (1 to 3).</summary>
        public static Sprite Of(int count)
        {
            if (count < 1 || count > 3) throw new ArgumentOutOfRangeException(nameof(count), count, "The speed icon has one to three chevrons.");
            if (!Shapes[count] || !Shapes[count].texture) Shapes[count] = Make(Chevrons[count]);
            return Shapes[count];
        }

        private static Sprite Make(float[] chevrons)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[Size * Size];
            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    // Back to the SVG's coordinates: the texture counts y upwards.
                    float x = (px + 0.5f) / Scale;
                    float y = 24f - (py + 0.5f) / Scale;
                    float nearest = float.MaxValue;
                    for (int i = 0; i < chevrons.Length; i += 2)
                    {
                        float x0 = chevrons[i], tip = chevrons[i + 1];
                        nearest = Mathf.Min(nearest, Segment(x, y, x0, 6f, tip, 12f));
                        nearest = Mathf.Min(nearest, Segment(x, y, tip, 12f, x0, 18f));
                    }
                    float alpha = Mathf.Clamp01((HalfWidth - nearest) * Scale + 0.5f);
                    pixels[py * Size + px] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>The distance from (px, py) to the segment (x0, y0)–(x1, y1).</summary>
        private static float Segment(float px, float py, float x0, float y0, float x1, float y1)
        {
            float vx = x1 - x0, vy = y1 - y0;
            float len2 = vx * vx + vy * vy;
            float t = len2 <= 0f ? 0f : Mathf.Clamp01(((px - x0) * vx + (py - y0) * vy) / len2);
            float dx = px - (x0 + vx * t), dy = py - (y0 + vy * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
#endif
