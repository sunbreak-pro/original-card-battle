// HP bar, Guard badge, stamina pips and status chips for one unit. Shows the values it
// is handed; the bar width is the only thing derived here (a drawing scale, not a rule).
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public class StatusBarView : MonoBehaviour
    {
        public UnitSide side;
        public RectTransform hpFill;
        public Image hpFillImage;
        [Tooltip("The grey band that follows the HP bar down, late (battle_ui_ux_v2 §5.5 順 7). Built behind the bar when left empty.")]
        public RectTransform hpTrail;
        public Text hpText;
        public RectTransform guardBadge;
        public Image guardIcon;
        public Text guardText;
        public RectTransform pipRow;
        public Image[] pips = new Image[0];
        public Text[] chips = new Text[0];

        private int _hpMax = 1;

        private void Awake()
        {
            if (guardIcon && guardIcon.sprite == null) guardIcon.sprite = ProceduralArt.Shield;
            foreach (Image pip in pips)
            {
                if (pip && pip.sprite == null) pip.sprite = ProceduralArt.Circle;
            }
            if (hpFillImage) hpFillImage.color = side == UnitSide.Player ? BattleTheme.Accent : BattleTheme.Omen;
            EnsureTrail();
        }

        /// <summary>The prefab belongs to the Unity project, so a missing band is made here, just behind the bar.</summary>
        private void EnsureTrail()
        {
            if (hpTrail || !hpFill || !hpFill.parent) return;
            var go = new GameObject("HpTrail", typeof(RectTransform), typeof(Image));
            hpTrail = (RectTransform)go.transform;
            hpTrail.SetParent(hpFill.parent, false);
            hpTrail.SetSiblingIndex(hpFill.GetSiblingIndex());
            hpTrail.anchorMin = hpFill.anchorMin;
            hpTrail.anchorMax = hpFill.anchorMax;
            hpTrail.pivot = hpFill.pivot;
            hpTrail.offsetMin = hpFill.offsetMin;
            hpTrail.offsetMax = hpFill.offsetMax;
            var image = go.GetComponent<Image>();
            image.sprite = hpFillImage ? hpFillImage.sprite : null;
            image.type = hpFillImage ? hpFillImage.type : Image.Type.Simple;
            image.color = BattleTheme.WithAlpha(BattleTheme.Whiff, 0.85f);
            image.raycastTarget = false;
        }

        public void Bind(UnitFrame unit)
        {
            _hpMax = Mathf.Max(1, unit.HpMax);
            SetHp(unit.Hp);
            SetGuard(unit.Guard);
            if (pipRow) pipRow.gameObject.SetActive(unit.ShowStamina);
            if (unit.ShowStamina) SetStamina(unit.Stamina, unit.StaminaMax);
            for (int i = 0; i < chips.Length; i++)
            {
                if (!chips[i]) continue;
                bool used = i < unit.Statuses.Count;
                chips[i].gameObject.SetActive(used);
                if (used) chips[i].text = unit.Statuses[i].Label + unit.Statuses[i].Stacks;
            }
        }

        public void SetHp(int hp)
        {
            SetHpKeepingTrail(hp);
            SnapTrail();
        }

        /// <summary>The bar and the number move at once; the grey band stays where it was until it is told to follow.</summary>
        public void SetHpKeepingTrail(int hp)
        {
            if (hpText) hpText.text = hp.ToString();
            if (hpFill) hpFill.anchorMax = new Vector2(Width(hp), hpFill.anchorMax.y);
            if (hpTrail && hpFill && hpTrail.anchorMax.x < hpFill.anchorMax.x) SnapTrail(); // a heal leaves no band behind
        }

        /// <summary>Slides the bar from its current width to <paramref name="hp"/>; the number lands at once.</summary>
        public IEnumerator AnimateHp(int hp, float ms)
        {
            if (hpText) hpText.text = hp.ToString();
            if (!hpFill) yield break;
            float from = hpFill.anchorMax.x;
            float to = Width(hp);
            if (hpTrail && hpTrail.anchorMax.x < to) hpTrail.anchorMax = new Vector2(to, hpTrail.anchorMax.y);
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (hpFill) hpFill.anchorMax = new Vector2(Mathf.Lerp(from, to, t), hpFill.anchorMax.y);
            });
        }

        /// <summary>The grey band catches up with the bar (EffectId.HpTrail).</summary>
        public IEnumerator AnimateTrail(float ms)
        {
            if (!hpTrail || !hpFill) yield break;
            float from = hpTrail.anchorMax.x;
            float to = hpFill.anchorMax.x;
            yield return UiTween.Run(ms, Ease.InOut, t =>
            {
                if (hpTrail) hpTrail.anchorMax = new Vector2(Mathf.Lerp(from, to, t), hpTrail.anchorMax.y);
            });
        }

        public void SnapTrail()
        {
            if (hpTrail && hpFill) hpTrail.anchorMax = new Vector2(hpFill.anchorMax.x, hpTrail.anchorMax.y);
        }

        private float Width(int hp) => Mathf.Clamp01(hp / (float)_hpMax);

        public void SetGuard(int guard)
        {
            if (guardText) guardText.text = guard.ToString();
            Color c = guard > 0 ? BattleTheme.Guard : BattleTheme.WithAlpha(BattleTheme.Ink2, 0.45f);
            if (guardIcon) guardIcon.color = c;
            if (guardText) guardText.color = guard > 0 ? BattleTheme.Ink : BattleTheme.Ink2;
        }

        public IEnumerator PopGuard(int guard, float ms = 260f)
        {
            SetGuard(guard);
            if (guardBadge) yield return UiTween.Pop(guardBadge, ms);
        }

        /// <summary>A blow took the Guard to 0: the badge flashes and shakes as it breaks (EffectId.GuardBreak).</summary>
        public IEnumerator CrackGuard(float ms)
        {
            if (!guardBadge) yield break;
            if (guardIcon) StartCoroutine(UiTween.Tint(guardIcon, BattleTheme.White, guardIcon.color, ms, Ease.Out));
            yield return UiTween.Shake(guardBadge, 7f, 2, ms);
        }

        public void SetStamina(int stamina, int staminaMax)
        {
            for (int i = 0; i < pips.Length; i++)
            {
                if (!pips[i]) continue;
                pips[i].gameObject.SetActive(i < staminaMax);
                pips[i].color = i < stamina ? BattleTheme.Warm : BattleTheme.WithAlpha(BattleTheme.Ink2, 0.25f);
            }
        }
    }
}
#endif
