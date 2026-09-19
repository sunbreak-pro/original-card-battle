// The enemy's omen at disclosure 1: kind + the side it punishes (one glyph) + value.
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public class OmenBadgeView : MonoBehaviour
    {
        public CanvasGroup group;
        public Text kindText;
        public RectTransform sideChip;
        public Image sideChipFrame;
        public Text sideText;
        public Image sideStrike;
        public Text valueText;

        public RectTransform Rect => (RectTransform)transform;

        public void Bind(OmenFrame omen)
        {
            if (group) group.alpha = omen.Visible ? 1f : 0f;
            if (kindText) kindText.text = omen.KindLabel;
            bool hasSide = !string.IsNullOrEmpty(omen.SideGlyph);
            if (sideChip) sideChip.gameObject.SetActive(hasSide);
            if (sideText)
            {
                sideText.text = omen.SideGlyph;
                sideText.color = BattleTheme.Omen;
            }
            if (sideChipFrame) sideChipFrame.color = BattleTheme.Omen;
            if (sideStrike) sideStrike.enabled = false;
            if (valueText) valueText.text = omen.ValueText;
        }

        public IEnumerator PopIn(float ms)
        {
            if (group) group.alpha = 1f;
            yield return UiTween.Scale(Rect, new Vector3(0.6f, 0.6f, 1f), Vector3.one, ms, Ease.Out);
        }

        /// <summary>The punished side did not apply: grey the glyph out and strike it.</summary>
        public IEnumerator StrikeSide(float ms)
        {
            if (sideStrike) sideStrike.enabled = true;
            if (sideText) sideText.color = BattleTheme.Whiff;
            if (sideChipFrame) sideChipFrame.color = BattleTheme.Whiff;
            if (sideChip) yield return UiTween.Shake(sideChip, 6f, 2, ms);
        }

        public IEnumerator FadeOut(float ms)
        {
            if (group) yield return UiTween.Fade(group, group.alpha, 0f, ms, Ease.In);
        }
    }
}
#endif
