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

        /// <summary>The glyph is written by the script ("近" / "遠"); this view only prints it.</summary>
        public void SetRange(bool hasRange, string glyph)
        {
            if (rangeTag) rangeTag.gameObject.SetActive(hasRange);
            if (!hasRange) return;
            if (string.IsNullOrEmpty(glyph)) Debug.LogError("[Depiction] " + name + ": the script gave no range glyph");
            if (rangeGlyph)
            {
                rangeGlyph.text = glyph;
                rangeGlyph.color = RoleColor;
            }
            if (rangeTagFrame) rangeTagFrame.color = RoleColor;
        }

        /// <summary>Flips the tag over in <paramref name="ms"/> and swaps the glyph at the half-way point.</summary>
        public IEnumerator FlipRangeTag(string glyph, float ms)
        {
            if (!rangeTag) yield break;
            yield return UiTween.Run(ms * 0.5f, Ease.In, t => { if (rangeTag) rangeTag.localScale = new Vector3(1f - t, 1f, 1f); });
            if (rangeGlyph) rangeGlyph.text = glyph;
            yield return UiTween.Run(ms * 0.5f, Ease.Out, t => { if (rangeTag) rangeTag.localScale = new Vector3(t, 1f, 1f); });
        }
    }
}
#endif
