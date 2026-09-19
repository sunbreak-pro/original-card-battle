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
            if (hpText) hpText.text = hp.ToString();
            if (hpFill) hpFill.anchorMax = new Vector2(Mathf.Clamp01(hp / (float)_hpMax), hpFill.anchorMax.y);
        }

        /// <summary>Slides the bar from its current width to <paramref name="hp"/>; the number lands at once.</summary>
        public IEnumerator AnimateHp(int hp, float ms)
        {
            if (hpText) hpText.text = hp.ToString();
            if (!hpFill) yield break;
            float from = hpFill.anchorMax.x;
            float to = Mathf.Clamp01(hp / (float)_hpMax);
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (hpFill) hpFill.anchorMax = new Vector2(Mathf.Lerp(from, to, t), hpFill.anchorMax.y);
            });
        }

        public void SetGuard(int guard)
        {
            if (guardText) guardText.text = guard.ToString();
            Color c = guard > 0 ? BattleTheme.Guard : BattleTheme.WithAlpha(BattleTheme.Ink2, 0.45f);
            if (guardIcon) guardIcon.color = c;
            if (guardText) guardText.color = guard > 0 ? BattleTheme.Ink : BattleTheme.Ink2;
        }

        public IEnumerator PopGuard(int guard)
        {
            SetGuard(guard);
            if (guardBadge) yield return UiTween.Pop(guardBadge, 260f);
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
