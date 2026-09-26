// HP bar, Guard badge, stamina pips and status chips for one unit. Shows the values it
// is handed; the bar width is the only thing derived here (a drawing scale, not a rule).
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using System.Collections.Generic;
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
        /// <summary>The chips as shown, in order; the chip Texts are filled from it.</summary>
        private readonly List<StatusChip> _statuses = new List<StatusChip>();
        private Color[] _chipColors = new Color[0];
        /// <summary>
        /// The stance in the slot (#188), handed in as a chip with no stacks. It gets its own wide line
        /// under the chips, made here because the prefab belongs to the Unity project, so it neither
        /// takes one of the six chips the status words need nor wraps inside a chip's width.
        /// </summary>
        private Text _stanceLabel;
        private string _stance = "";

        /// <summary>The stamina the pips show now (the start of a recovery that lights them one by one).</summary>
        public int Stamina { get; private set; }

        private void Awake()
        {
            if (guardIcon && guardIcon.sprite == null) guardIcon.sprite = ProceduralArt.Shield;
            foreach (Image pip in pips)
            {
                if (pip && pip.sprite == null) pip.sprite = ProceduralArt.Circle;
            }
            if (hpFillImage) hpFillImage.color = side == UnitSide.Player ? BattleTheme.Accent : BattleTheme.Omen;
            EnsureTrail();
            _chipColors = new Color[chips.Length];
            for (int i = 0; i < chips.Length; i++) _chipColors[i] = chips[i] ? chips[i].color : Color.white;
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
            _statuses.Clear();
            _stance = "";
            foreach (StatusChip chip in unit.Statuses)
            {
                if (chip.Stacks <= 0) _stance = chip.Label;
                else _statuses.Add(new StatusChip { Label = chip.Label, Stacks = chip.Stacks });
            }
            RenderChips();
        }

        private void RenderChips()
        {
            RenderStance();
            for (int i = 0; i < chips.Length; i++)
            {
                if (!chips[i]) continue;
                bool used = i < _statuses.Count;
                chips[i].gameObject.SetActive(used);
                if (!used) continue;
                chips[i].text = _statuses[i].Label + _statuses[i].Stacks;
                chips[i].rectTransform.localScale = Vector3.one;
                if (i < _chipColors.Length) chips[i].color = _chipColors[i];
            }
        }

        private void RenderStance()
        {
            if (!_stanceLabel && _stance.Length > 0 && chips.Length > 0 && chips[0])
            {
                var row = chips[0].rectTransform.parent as RectTransform;
                if (row)
                {
                    _stanceLabel = UiKit.Label(row, "Stance", 20, TextAnchor.MiddleLeft, BattleTheme.Warm,
                        new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(400f, 24f), new Vector2(0f, -30f));
                }
            }
            if (!_stanceLabel) return;
            _stanceLabel.gameObject.SetActive(_stance.Length > 0);
            _stanceLabel.text = _stance;
        }

        /// <summary>One word's chip, settled at once: added, restacked, or gone at 0.</summary>
        public void SetStatus(string label, int stacks)
        {
            int i = _statuses.FindIndex(s => s.Label == label);
            if (stacks <= 0)
            {
                if (i >= 0) _statuses.RemoveAt(i);
            }
            else if (i >= 0)
            {
                _statuses[i].Stacks = stacks;
            }
            else
            {
                _statuses.Add(new StatusChip { Label = label, Stacks = stacks });
            }
            RenderChips();
        }

        /// <summary>
        /// The same change played as one of the four chip beats (battle_ui_ux_v2 §5.10): a new chip
        /// swells in, more stacks pop it, fewer shrink it for a moment, and the last stack fades it out.
        /// </summary>
        public IEnumerator PlayStatus(string label, int stacks, EffectId beat, float ms)
        {
            if (beat == EffectId.StatusVanish)
            {
                Text going = ChipOf(label);
                if (going)
                {
                    Color from = going.color;
                    RectTransform rt = going.rectTransform;
                    yield return UiTween.Run(ms, Ease.Linear, t =>
                    {
                        if (!going) return;
                        going.color = BattleTheme.WithAlpha(from, from.a * (1f - t));
                        float s = Mathf.Lerp(1f, 0.7f, t);
                        rt.localScale = new Vector3(s, s, 1f);
                    });
                }
                SetStatus(label, 0);
                yield break;
            }

            SetStatus(label, stacks);
            Text shown = ChipOf(label);
            if (!shown) yield break;
            RectTransform chip = shown.rectTransform;
            switch (beat)
            {
                case EffectId.StatusApply:
                    yield return UiTween.Run(ms, Ease.Out, t =>
                    {
                        float s = t < 0.6f ? Mathf.Lerp(0.3f, 1.2f, t / 0.6f) : Mathf.Lerp(1.2f, 1f, (t - 0.6f) / 0.4f);
                        if (chip) chip.localScale = new Vector3(s, s, 1f);
                    });
                    break;
                case EffectId.StatusStack:
                    yield return UiTween.Pop(chip, ms);
                    break;
                default: // StatusTick
                    yield return UiTween.Run(ms, Ease.Out, t =>
                    {
                        float s = 1f - 0.18f * Mathf.Sin(t * Mathf.PI);
                        if (chip) chip.localScale = new Vector3(s, s, 1f);
                    });
                    break;
            }
            if (chip) chip.localScale = Vector3.one;
        }

        private Text ChipOf(string label)
        {
            int i = _statuses.FindIndex(s => s.Label == label);
            return i >= 0 && i < chips.Length ? chips[i] : null;
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

        /// <summary>
        /// The recovered pips light one at a time, <paramref name="perPipMs"/> apart
        /// (EffectId.StaminaRecover, battle_ui_ux_v2 §5.1 順 3).
        /// </summary>
        public IEnumerator LightPips(int stamina, int staminaMax, float perPipMs)
        {
            int from = Mathf.Clamp(Stamina, 0, staminaMax);
            if (stamina <= from)
            {
                SetStamina(stamina, staminaMax);
                yield break;
            }
            SetStamina(from, staminaMax);
            for (int next = from + 1; next <= stamina; next++)
            {
                yield return UiTween.Wait(perPipMs);
                SetStamina(next, staminaMax);
                int pip = next - 1;
                if (pip < pips.Length && pips[pip]) StartCoroutine(UiTween.Pop(pips[pip].rectTransform, perPipMs));
            }
        }

        public void SetStamina(int stamina, int staminaMax)
        {
            Stamina = stamina;
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
