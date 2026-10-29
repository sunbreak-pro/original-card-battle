// Transient effects for the depiction, all driven by UiTween (no DOTween, no Animator).
// Strength comes in as a 1..4 tier that the script already decided (battle_ui_ux_v2 §5.3;
// since 2026-10-03 every script writes one tier, #30); this file only maps a tier to size.
// The shape comes in as the system the script wrote (Cue.System for a blow,
// Cue.Fx for the rest, #57): one form per system, never chosen here.
#if UNITY_2021_2_OR_NEWER
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public static class DepictionFx
    {
        private static readonly float[] SlashLength = { 300f, 380f, 460f, 560f };
        private static readonly float[] SlashThickness = { 8f, 12f, 16f, 22f };
        private static readonly int[] NumberSize = { 56, 72, 88, 108 };

        public static int NumberFont(int tier) => NumberSize[Index(tier)];

        private static int Index(int tier) => Mathf.Clamp(tier, 1, 4) - 1;

        /// <summary>Local point inside <paramref name="layer"/> that sits on top of <paramref name="target"/>.</summary>
        public static Vector2 PointOn(RectTransform layer, Transform target)
        {
            // Effects are centre-anchored, so the offset is measured from the layer's centre.
            Vector2 local = layer.InverseTransformPoint(target.position);
            return local - layer.rect.center;
        }

        /// <summary>A diagonal streak that sweeps across the target, then fades on its own.</summary>
        public static IEnumerator Slash(MonoBehaviour host, RectTransform layer, Vector2 at, Color color, int tier, bool towardLeft, float ms = 120f)
        {
            return Streak(host, layer, at, color, towardLeft ? 38f : -38f, SlashLength[Index(tier)], SlashThickness[Index(tier)], ms);
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.3: the blow's shape by system, all drawn in <paramref name="ms"/> so a
        /// system changes the form and not the length. 斬 an arc, 突 one straight line, 払 a wide band,
        /// 打 a ripple where it lands. A blowless system falls back to the arc.
        /// </summary>
        public static IEnumerator Strike(MonoBehaviour host, RectTransform layer, Vector2 at, Color color, int tier, bool towardLeft, StrikeSystem system, float ms)
        {
            float length = SlashLength[Index(tier)];
            float thickness = SlashThickness[Index(tier)];
            switch (system)
            {
                case StrikeSystem.Thrust:
                    return Streak(host, layer, at, color, 0f, length * 1.25f, Mathf.Max(4f, thickness * 0.45f), ms);
                case StrikeSystem.Sweep:
                    return Streak(host, layer, at, BattleTheme.WithAlpha(color, 0.75f), towardLeft ? 8f : -8f, length * 1.4f, thickness * 2.4f, ms);
                case StrikeSystem.Strike:
                    return Ripple(host, layer, at, color, length * 0.7f, ms);
                default:
                    return Slash(host, layer, at, color, tier, towardLeft, ms);
            }
        }

        /// <summary>§5.3 大きさ: 0.6 / 1.0 / 1.3 / 1.7 by tier. Every script writes tier 2 for now (#30), so 1.0.</summary>
        private static readonly float[] MarkScale = { 0.6f, 1f, 1.3f, 1.7f };

        /// <summary>
        /// battle_ui_ux_v2 §5.3 (#57): the shape of a system that is not a blow, drawn beside the beat it
        /// belongs to. The form is the grammar's (<see cref="EffectGrammar.FormOf"/>), one per system, so the
        /// same system always looks the same: 盾 a ring standing in front of the figure, 型 a crest at its
        /// feet whose light fades 600 ms later, 気 a mark falling from above onto the chest. A blow draws
        /// nothing here (its shape is <see cref="Strike"/>), and neither does 息, whose form is the pips
        /// lighting (a heal's chest is the player's <c>Heave</c>). <paramref name="facing"/> is +1 when the
        /// figure faces right. <paramref name="crestLight"/> false draws the 型 crest without its body light
        /// (§5.11 順 2: from the second stance on only the crest is drawn again, <see cref="StanceGlow.CrestCarriesLight"/>).
        /// </summary>
        public static IEnumerator Mark(MonoBehaviour host, RectTransform layer, Vector2 chest, Vector2 head, Vector2 feet,
            float facing, FxSystem system, int tier, float ms, bool crestLight = true)
        {
            float scale = MarkScale[Index(tier)];
            switch (EffectGrammar.FormOf(system))
            {
                case FxForm.Ring:
                    return StandingRing(host, layer, chest + new Vector2(70f * facing, 0f), scale, ms);
                case FxForm.Crest:
                    return Crest(host, layer, feet, scale, ms, crestLight);
                case FxForm.Sigil:
                    return Sigil(host, layer, head + new Vector2(0f, 60f), chest, scale, ms);
                default:
                    return Nothing();
            }
        }

        /// <summary>
        /// §5.9 順 1 (J2, #57): a trait fired on this blow. Two thin rings flare out from the point of the
        /// blow and are gone, laid over the shape without touching its tier, size or length.
        /// </summary>
        public static IEnumerator TraitAccent(MonoBehaviour host, RectTransform layer, Vector2 at, float ms)
        {
            Image outer = UiKit.Sprite(layer, "TraitAccent", VisualArt.CircleRing(1f), BattleTheme.WithAlpha(BattleTheme.Wick, 0.95f), Half, Half, new Vector2(150f, 150f), at);
            Image inner = UiKit.Sprite(layer, "TraitAccentInner", VisualArt.CircleRing(1.5f), BattleTheme.WithAlpha(BattleTheme.Amber, 0.9f), Half, Half, new Vector2(96f, 96f), at);
            outer.raycastTarget = false;
            inner.raycastTarget = false;
            RectTransform a = outer.rectTransform, b = inner.rectTransform;
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                float s = Mathf.Lerp(0.5f, 1.35f, t);
                if (a) a.localScale = new Vector3(s, s, 1f);
                if (b) b.localScale = new Vector3(s * 0.9f, s * 0.9f, 1f);
            });
            host.StartCoroutine(FadeAndDestroy(outer, 120f));
            host.StartCoroutine(FadeAndDestroy(inner, 120f));
        }

        private static IEnumerator Nothing()
        {
            yield break;
        }

        /// <summary>盾: a ring seen edge-on, rising to full height in front of the figure.</summary>
        private static IEnumerator StandingRing(MonoBehaviour host, RectTransform layer, Vector2 at, float scale, float ms)
        {
            Image ring = UiKit.Sprite(layer, "ShieldRing", VisualArt.CircleRing(2f), BattleTheme.WithAlpha(BattleTheme.Guard, 0.95f), Half, Half, new Vector2(110f * scale, 230f * scale), at);
            ring.raycastTarget = false;
            Burst(host, layer, at, BattleTheme.Guard, 160f * scale);
            RectTransform rt = ring.rectTransform;
            yield return UiTween.Run(ms, Ease.Out, t => { if (rt) rt.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, t), t, 1f); });
            host.StartCoroutine(FadeAndDestroy(ring, 220f));
        }

        /// <summary>
        /// 型: two flat rings open under the feet, and the crest's light lingers before it fades. Without
        /// <paramref name="withLight"/> only the rings are drawn (a unit that already keeps a stance light).
        /// </summary>
        private static IEnumerator Crest(MonoBehaviour host, RectTransform layer, Vector2 feet, float scale, float ms, bool withLight)
        {
            Image outer = UiKit.Sprite(layer, "FormCrest", VisualArt.CircleRing(1.5f), BattleTheme.WithAlpha(BattleTheme.Amber, 0.95f), Half, Half, new Vector2(280f * scale, 72f * scale), feet);
            Image inner = UiKit.Sprite(layer, "FormCrestInner", VisualArt.CircleRing(1f), BattleTheme.WithAlpha(BattleTheme.Wick, 0.85f), Half, Half, new Vector2(170f * scale, 44f * scale), feet);
            Image glow = withLight
                ? UiKit.Sprite(layer, "FormGlow", ProceduralArt.Glow, BattleTheme.WithAlpha(BattleTheme.Amber, 0.35f), Half, Half, new Vector2(200f * scale, 320f * scale), feet + new Vector2(0f, 140f * scale))
                : null;
            outer.raycastTarget = inner.raycastTarget = false;
            if (glow) glow.raycastTarget = false;
            RectTransform a = outer.rectTransform, b = inner.rectTransform;
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (a) a.localScale = new Vector3(Mathf.Lerp(0.3f, 1f, t), Mathf.Lerp(0.3f, 1f, t), 1f);
                if (b) b.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, t), Mathf.Lerp(0.6f, 1f, t), 1f);
            });
            // §5.3 「300 + 600 で薄れる」: the light stays a while beside the flow. The light that lasts while the
            // stance list holds one is §5.11 順 2's StanceGlow (FigureView, #501), not this.
            host.StartCoroutine(FadeAndDestroy(outer, 600f));
            host.StartCoroutine(FadeAndDestroy(inner, 600f));
            if (glow) host.StartCoroutine(FadeAndDestroy(glow, 600f));
        }

        /// <summary>気: a diamond mark drops from above the head onto the chest and bursts there.</summary>
        private static IEnumerator Sigil(MonoBehaviour host, RectTransform layer, Vector2 from, Vector2 to, float scale, float ms)
        {
            RectTransform rt = UiKit.Point(layer, "SpiritMark", Half, Half, new Vector2(40f * scale, 40f * scale), from);
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ProceduralArt.White;
            img.color = BattleTheme.WithAlpha(BattleTheme.Phosphor, 0.95f);
            img.raycastTarget = false;
            yield return UiTween.Run(ms, Ease.In, t => { if (rt) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t); });
            Burst(host, layer, to, BattleTheme.Phosphor, 140f * scale);
            host.StartCoroutine(FadeAndDestroy(img, 150f));
        }

        private static IEnumerator Streak(MonoBehaviour host, RectTransform layer, Vector2 at, Color color, float degrees, float length, float thickness, float ms)
        {
            RectTransform rt = UiKit.Point(layer, "Strike", Half, Half, new Vector2(length, thickness), at);
            rt.localRotation = Quaternion.Euler(0f, 0f, degrees);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ProceduralArt.White;
            img.color = color;
            img.raycastTarget = false;
            yield return UiTween.Run(ms, Ease.Out, t => { if (rt) rt.localScale = new Vector3(t, 1f + (1f - t), 1f); });
            host.StartCoroutine(FadeAndDestroy(img, 180f));
        }

        private static IEnumerator Ripple(MonoBehaviour host, RectTransform layer, Vector2 at, Color color, float size, float ms)
        {
            Image ring = UiKit.Sprite(layer, "Ripple", ProceduralArt.Circle, BattleTheme.WithAlpha(color, 0.9f), Half, Half, new Vector2(size, size), at);
            ring.raycastTarget = false;
            RectTransform rt = ring.rectTransform;
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (!rt) return;
                float s = Mathf.Lerp(0.3f, 1.2f, t);
                rt.localScale = new Vector3(s, s, 1f);
            });
            host.StartCoroutine(FadeAndDestroy(ring, 180f));
        }

        /// <summary>Soft burst behind a number or a cue.</summary>
        public static void Burst(MonoBehaviour host, RectTransform layer, Vector2 at, Color color, float size)
        {
            Image img = UiKit.Sprite(layer, "Burst", ProceduralArt.Glow, BattleTheme.WithAlpha(color, 0.8f), Half, Half, new Vector2(size, size), at);
            host.StartCoroutine(BurstRoutine(img));
        }

        private static IEnumerator BurstRoutine(Image img)
        {
            RectTransform rt = img.rectTransform;
            Color from = img.color;
            yield return UiTween.Run(320f, Ease.Out, t =>
            {
                if (!rt) return;
                float s = Mathf.Lerp(0.4f, 1.5f, t);
                rt.localScale = new Vector3(s, s, 1f);
                img.color = BattleTheme.WithAlpha(from, from.a * (1f - t));
            });
            if (img) Object.Destroy(img.gameObject);
        }

        /// <summary>Text that rises and fades without blocking the caller.</summary>
        public static void FloatText(MonoBehaviour host, RectTransform layer, Vector2 at, string text, Color color, int fontSize, bool struck = false, float rise = 96f)
        {
            Text label = UiKit.Label(layer, "Float", fontSize, TextAnchor.MiddleCenter, color, Half, Half, new Vector2(420f, fontSize * 1.5f), at, text);
            label.fontStyle = FontStyle.Bold;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = BattleTheme.WithAlpha(BattleTheme.InkBlack, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);
            if (struck)
            {
                Image line = UiKit.Sprite(label.rectTransform, "Strike", ProceduralArt.White, color, Half, Half,
                    new Vector2(fontSize * 0.62f * text.Length + 16f, 4f), Vector2.zero);
                line.raycastTarget = false;
            }
            host.StartCoroutine(FloatRoutine(label, at, rise));
        }

        /// <summary>
        /// battle-visual-v1 §5.5 / battle_ui_ux_v2 §5.0 (#455): a hit's number on its small black 80%
        /// plate — the shield and the Guard colour at 34 px for Guard lost, the heart and the HP ink at
        /// 48 px for HP — with the cause beside it at 16 px. Its centre rises from the chest to the head
        /// of a figure <paramref name="height"/> px tall standing on <paramref name="feet"/>, then fades.
        /// <paramref name="paired"/> sets it beside the other number of the same blow (FloatNumberSpec.CentreX).
        /// Never blocks the caller.
        /// </summary>
        public static void FloatPlate(MonoBehaviour host, RectTransform layer, Vector2 feet, float height, bool paired,
            FloatNumberKind kind, int amount, string label, float ms)
        {
            Color ink = BattleTheme.Hex(FloatNumberSpec.ColorHex(kind));
            int font = FloatNumberSpec.FontPx(kind);
            RectTransform plate = UiKit.Point(layer, "FloatPlate", Half, Half, Vector2.zero, Vector2.zero);
            UiKit.Image(plate, "Back", VisualArt.Rounded(FloatNumberSpec.PlateCorner),
                BattleTheme.WithAlpha(Color.black, FloatNumberSpec.PlateAlpha), Vector2.zero, Vector2.one).type = Image.Type.Sliced;

            Text number = UiKit.Label(plate, "Number", font, TextAnchor.MiddleCenter, ink, new Vector2(0f, 0.5f), Half,
                new Vector2(10f, font * 1.25f), Vector2.zero, FloatNumberSpec.NumberText(kind, amount));
            number.font = UiKit.NumberFont;
            number.fontStyle = FontStyle.Bold;
            number.horizontalOverflow = HorizontalWrapMode.Overflow;
            Text side = null;
            if (!string.IsNullOrEmpty(label))
            {
                side = UiKit.Label(plate, "Label", FloatNumberSpec.LabelFontPx, TextAnchor.MiddleCenter,
                    BattleTheme.Hex(FloatNumberSpec.LabelInkHex), new Vector2(0f, 0.5f), Half,
                    new Vector2(10f, FloatNumberSpec.LabelFontPx * 1.5f), Vector2.zero, label);
                side.font = UiKit.BodyBoldFont;
                side.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            FloatPlateLayout layout = FloatNumberSpec.Layout(kind, number.preferredWidth, side ? side.preferredWidth : 0f);
            plate.sizeDelta = new Vector2(layout.Width, layout.Height);
            int iconPx = FloatNumberSpec.IconPx(kind);
            Sprite icon = FloatNumberSpec.LeadsWithShield(kind) ? ProceduralArt.Shield : VisualArt.Heart;
            Image mark = UiKit.Sprite(plate, "Icon", icon, ink, new Vector2(0f, 0.5f), Half,
                new Vector2(iconPx, iconPx), new Vector2(layout.IconCentre, 0f));
            mark.preserveAspect = true;
            mark.raycastTarget = false;
            number.rectTransform.sizeDelta = new Vector2(number.preferredWidth, number.rectTransform.sizeDelta.y);
            number.rectTransform.anchoredPosition = new Vector2(layout.NumberCentre, 0f);
            if (side)
            {
                side.rectTransform.sizeDelta = new Vector2(side.preferredWidth, side.rectTransform.sizeDelta.y);
                side.rectTransform.anchoredPosition = new Vector2(layout.LabelCentre, 0f);
            }

            float centreX = FloatNumberSpec.CentreX(kind, paired, layout.Width);
            Vector2 from = feet + new Vector2(centreX, FloatNumberSpec.StartAbove(height));
            Vector2 to = feet + new Vector2(centreX, FloatNumberSpec.EndAbove(height));
            host.StartCoroutine(PlateRoutine(plate, from, to, ms));
        }

        private static IEnumerator PlateRoutine(RectTransform rt, Vector2 from, Vector2 to, float ms)
        {
            CanvasGroup group = UiKit.Group(rt);
            group.blocksRaycasts = false;
            rt.anchoredPosition = from;
            yield return UiTween.Run(ms, Ease.Out, t =>
            {
                if (!rt) return;
                rt.anchoredPosition = Vector2.Lerp(from, to, t);
                float pop = t < 0.18f ? Mathf.Lerp(1.2f, 1f, t / 0.18f) : 1f;
                rt.localScale = new Vector3(pop, pop, 1f);
                group.alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            });
            if (rt) Object.Destroy(rt.gameObject);
        }

        private static IEnumerator FloatRoutine(Text label, Vector2 at, float rise)
        {
            RectTransform rt = label.rectTransform;
            CanvasGroup group = UiKit.Group(rt);
            yield return UiTween.Run(1000f, Ease.Out, t =>
            {
                if (!rt) return;
                rt.anchoredPosition = at + new Vector2(0f, rise * t);
                float pop = t < 0.18f ? Mathf.Lerp(1.35f, 1f, t / 0.18f) : 1f;
                rt.localScale = new Vector3(pop, pop, 1f);
                group.alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            });
            if (label) Object.Destroy(label.gameObject);
        }

        /// <summary>A shield that snaps up in front of the target and shows how much Guard stopped.</summary>
        public static IEnumerator ShieldBlock(MonoBehaviour host, RectTransform layer, Vector2 at, string blocked, System.Func<IEnumerator> hold = null)
        {
            Image shield = UiKit.Sprite(layer, "ShieldBlock", ProceduralArt.Shield, BattleTheme.WithAlpha(BattleTheme.Guard, 0.92f), Half, Half, new Vector2(150f, 170f), at);
            Text number = UiKit.Text(shield.rectTransform, "Blocked", 56, TextAnchor.MiddleCenter, BattleTheme.InkBlack, blocked);
            number.fontStyle = FontStyle.Bold;
            Burst(host, layer, at, BattleTheme.Guard, 320f);
            yield return UiTween.Pop(shield.rectTransform, 240f);
            yield return UiTween.Wait(80f);
            if (hold != null) yield return hold();
            host.StartCoroutine(FadeAndDestroy(shield, 220f, number));
        }

        /// <summary>Brief tint toward <paramref name="flash"/> and back.</summary>
        public static IEnumerator Flash(Graphic g, Color flash, float ms)
        {
            if (!g) yield break;
            Color home = g.color;
            yield return UiTween.Tint(g, flash, home, ms, Ease.Out);
            if (g) g.color = home;
        }

        private static IEnumerator FadeAndDestroy(Graphic g, float ms, Graphic child = null)
        {
            if (!g) yield break;
            Color from = g.color;
            Color childFrom = child ? child.color : Color.clear;
            yield return UiTween.Run(ms, Ease.In, t =>
            {
                if (g) g.color = BattleTheme.WithAlpha(from, from.a * (1f - t));
                if (child) child.color = BattleTheme.WithAlpha(childFrom, childFrom.a * (1f - t));
            });
            if (g) Object.Destroy(g.gameObject);
        }

        private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);
    }
}
#endif
