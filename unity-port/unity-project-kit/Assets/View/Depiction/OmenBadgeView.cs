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

        /// <summary>The standing omen blinks once at the player's turn start (EffectId.OmenBlink).</summary>
        public IEnumerator Blink(float ms)
        {
            if (!group) yield break;
            float from = group.alpha;
            yield return UiTween.Run(ms, Ease.InOut, t => { if (group) group.alpha = Mathf.Lerp(from, 0.25f, Mathf.Sin(t * Mathf.PI)); });
            if (group) group.alpha = from;
        }

        /// <summary>The omen flares as the enemy carries it out (EffectId.OmenExecute).</summary>
        public IEnumerator Flare(float ms)
        {
            Color from = kindText ? kindText.color : Color.white;
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                float s = 1f + 0.18f * Mathf.Sin(t * Mathf.PI);
                Rect.localScale = new Vector3(s, s, 1f);
                if (kindText) kindText.color = Color.Lerp(BattleTheme.White, from, t);
            });
            Rect.localScale = Vector3.one;
            if (kindText) kindText.color = from;
        }

        /// <summary>The badge gone at once (EffectId.OmenSpend switched off).</summary>
        public void HideNow()
        {
            if (group) group.alpha = 0f;
        }

        /// <summary>The side struck off at once, without the shake (EffectId.SideBonusMiss switched off).</summary>
        public void StrikeSideNow()
        {
            if (sideStrike) sideStrike.enabled = true;
            if (sideText) sideText.color = BattleTheme.Whiff;
            if (sideChipFrame) sideChipFrame.color = BattleTheme.Whiff;
        }
    }
}
#endif
