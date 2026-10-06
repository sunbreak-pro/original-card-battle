// The enemy's omen (battle-visual-v1 4.4, #349): the kind's icon and, for attack and guard, its
// number on the first row; the kind word and the reach on the second; an elite's or a boss's plan
// stacked above as a smaller badge. The badge from before #349 is laid out again here (icon and
// value on the first row, kind word and reach on the second), and the icon and the plan badge are
// made here too: the prefab saved in the Unity project still has the old one-row parts, and
// rebuilding it (DepictionPrefabBuilder) needs the Unity Editor. The runtime layout here wins over
// the builder's until the builder follows (#242).
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

        // battle-visual-v1 2.1. Folded into BattleTheme with #242; BattleTheme still carries the A+ tokens.
        private static readonly Color Phosphor = BattleTheme.Hex("#b9a3ff");
        private static readonly Color GuardInk = BattleTheme.Hex("#8fc4d8");

        private static readonly Vector2 BadgeSize = new Vector2(200f, 98f);
        private static readonly Vector2 PlanSize = new Vector2(170f, 66f);
        private const float PlanGap = 8f;
        private const float PlanAlpha = 0.62f;

        private Image _icon;
        private RectTransform _plan;
        private CanvasGroup _planGroup;
        private Image _planIcon;
        private Text _planValue;
        private Text _planKind;
        private Text _planTag;
        private bool _built;
        /// <summary>Whether the first omen is up, as the last Bind left it; PopIn keeps to it.</summary>
        private bool _shown;

        public RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            EnsureParts();
        }

        private void EnsureParts()
        {
            if (_built) return;
            _built = true;

            Rect.sizeDelta = BadgeSize;
            if (kindText)
            {
                Place(kindText.rectTransform, new Vector2(0f, 0f), new Vector2(110f, 26f), new Vector2(14f, 6f));
                kindText.fontSize = 16;
                kindText.alignment = TextAnchor.MiddleLeft;
            }
            _icon = UiKit.Sprite(Rect, "Icon", null, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(34f, 34f), new Vector2(14f, -14f));
            _icon.preserveAspect = true;
            _icon.enabled = false;
            if (valueText)
            {
                Place(valueText.rectTransform, new Vector2(0f, 1f), new Vector2(120f, 56f), new Vector2(54f, -4f));
                valueText.fontSize = 48;
                valueText.alignment = TextAnchor.MiddleLeft;
            }
            if (sideChip) Place(sideChip, new Vector2(1f, 0f), new Vector2(64f, 28f), new Vector2(-8f, 6f));
            if (sideText)
            {
                sideText.rectTransform.sizeDelta = new Vector2(64f, 28f);
                sideText.fontSize = 20;
            }

            // The plan (#50) stacks above the badge. Its own group ignores the badge's, so the plan stays
            // when the first omen is spent and the badge fades.
            _plan = UiKit.Point(Rect, "Plan", new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), PlanSize, new Vector2(0f, PlanGap));
            _planGroup = UiKit.Group(_plan);
            _planGroup.ignoreParentGroups = true;
            _planGroup.blocksRaycasts = false;
            _planGroup.alpha = 0f;
            UiKit.Fill(_plan, "Back", BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.88f));
            // battle-visual-v1 4.4 draws a dashed frame; a solid one stands in until #242.
            UiKit.Frame(_plan, BattleTheme.Ink2, 2f);
            _planIcon = UiKit.Sprite(_plan, "Icon", null, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, 24f), new Vector2(10f, -8f));
            _planIcon.preserveAspect = true;
            _planIcon.enabled = false;
            _planValue = UiKit.Label(_plan, "Value", 28, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(90f, 36f), new Vector2(40f, -2f));
            _planValue.fontStyle = FontStyle.Bold;
            _planKind = UiKit.Label(_plan, "Kind", 16, TextAnchor.MiddleLeft, BattleTheme.Ink, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(100f, 22f), new Vector2(10f, 4f));
            // battle-visual-v1 2.1: text under 24 px is written in body ink.
            _planTag = UiKit.Label(_plan, "Tag", 14, TextAnchor.MiddleRight, BattleTheme.Ink, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(80f, 20f), new Vector2(-8f, 4f));
        }

        /// <summary>A prefab child moved to one corner of the badge: anchor and pivot at that corner.</summary>
        private static void Place(RectTransform rt, Vector2 corner, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = corner;
            rt.anchorMax = corner;
            rt.pivot = corner;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        public void Bind(OmenFrame omen)
        {
            EnsureParts();
            _shown = omen.Visible;
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
            if (_icon)
            {
                _icon.sprite = OmenIconArt.Of(omen.Icon);
                _icon.enabled = omen.Icon != OmenIcon.None;
                _icon.color = RoleColor(omen.Icon);
            }
            if (valueText)
            {
                valueText.text = omen.ValueText;
                valueText.color = RoleColor(omen.Icon);
            }
            BindPlan(omen);
        }

        private void BindPlan(OmenFrame omen)
        {
            if (!_planGroup) return;
            _planGroup.alpha = omen.PlanVisible ? PlanAlpha : 0f;
            if (!omen.PlanVisible) return;
            if (_planIcon)
            {
                _planIcon.sprite = OmenIconArt.Of(omen.PlanIcon);
                _planIcon.enabled = omen.PlanIcon != OmenIcon.None;
                _planIcon.color = RoleColor(omen.PlanIcon);
            }
            if (_planValue)
            {
                _planValue.text = omen.PlanValueText;
                _planValue.color = RoleColor(omen.PlanIcon);
            }
            if (_planKind) _planKind.text = omen.PlanKindLabel;
            if (_planTag) _planTag.text = omen.PlanTag;
        }

        /// <summary>battle-visual-v1 4.4 / 8: attack in phosphor, guard in the Guard colour, the rest in body ink (as CardView.KindColor maps a card).</summary>
        private static Color RoleColor(OmenIcon icon)
        {
            switch (icon)
            {
                case OmenIcon.Attack: return Phosphor;
                case OmenIcon.Guard: return GuardInk;
                default: return BattleTheme.Ink;
            }
        }

        public IEnumerator PopIn(float ms)
        {
            // A beat that shows the plan alone (予定変更 after the first omen is spent) pops the plan
            // without bringing the empty badge back.
            if (group) group.alpha = _shown ? 1f : 0f;
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

        /// <summary>
        /// The fallen enemy's badge and its plan fade together. FadeOut leaves the plan, whose group
        /// ignores the badge's, because a spent first omen keeps the plan up; a fall takes both.
        /// </summary>
        public IEnumerator FadeOutAll(float ms)
        {
            float badgeFrom = group ? group.alpha : 0f;
            float planFrom = _planGroup ? _planGroup.alpha : 0f;
            yield return UiTween.Run(ms, Ease.In, t =>
            {
                if (group) group.alpha = Mathf.Lerp(badgeFrom, 0f, t);
                if (_planGroup) _planGroup.alpha = Mathf.Lerp(planFrom, 0f, t);
            });
        }

        /// <summary>The badge and its plan gone at once (EffectId.OmenSpend switched off on a fall).</summary>
        public void HideAllNow()
        {
            if (group) group.alpha = 0f;
            if (_planGroup) _planGroup.alpha = 0f;
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
