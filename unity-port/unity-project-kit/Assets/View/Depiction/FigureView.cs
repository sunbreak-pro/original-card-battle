// A standing figure shared by the player and enemies, with the one-glyph range tag
// ("近" / "遠") at its feet. The silhouette comes from ProceduralArt until real art
// arrives; SetSprite is the single replacement point.
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public class FigureView : MonoBehaviour
    {
        public UnitSide side;
        public bool useSpearSilhouette;
        [Tooltip("Tints the silhouette toward the role colour so it reads against the dark backdrop. Turn off for real art.")]
        public bool tintByRole = true;
        public Image body;
        public RectTransform chest;
        public RectTransform head;
        public RectTransform rangeTag;
        public Image rangeTagFrame;
        public Text rangeGlyph;

        public RectTransform Rect => (RectTransform)transform;
        public Color RoleColor => side == UnitSide.Player ? BattleTheme.Accent : BattleTheme.Omen;

        private void Awake()
        {
            if (body && body.sprite == null)
            {
                body.sprite = useSpearSilhouette ? ProceduralArt.FigureWithSpear : ProceduralArt.Figure;
                if (tintByRole) body.color = Color.Lerp(BattleTheme.InkBlack, RoleColor, 0.42f);
            }
        }

        /// <summary>Replacement point for real character art.</summary>
        public void SetSprite(Sprite sprite)
        {
            if (!body || sprite == null) return;
            body.sprite = sprite;
            body.color = Color.white;
            body.preserveAspect = true;
        }

        public void SetRange(bool hasRange, RangeSide range)
        {
            if (rangeTag) rangeTag.gameObject.SetActive(hasRange);
            if (!hasRange) return;
            if (rangeGlyph)
            {
                rangeGlyph.text = Glyph(range);
                rangeGlyph.color = RoleColor;
            }
            if (rangeTagFrame) rangeTagFrame.color = RoleColor;
        }

        /// <summary>Flips the tag over in <paramref name="ms"/> and swaps the glyph at the half-way point.</summary>
        public IEnumerator FlipRangeTag(RangeSide range, float ms)
        {
            if (!rangeTag) yield break;
            yield return UiTween.Run(ms * 0.5f, Ease.In, t => { if (rangeTag) rangeTag.localScale = new Vector3(1f - t, 1f, 1f); });
            if (rangeGlyph) rangeGlyph.text = Glyph(range);
            yield return UiTween.Run(ms * 0.5f, Ease.Out, t => { if (rangeTag) rangeTag.localScale = new Vector3(t, 1f, 1f); });
        }

        public static string Glyph(RangeSide range)
        {
            return range == RangeSide.Near ? "近" : "遠";
        }
    }
}
#endif
