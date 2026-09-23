// Transient effects for the depiction, all driven by UiTween (no DOTween, no Animator).
// Strength comes in as a 1..4 tier that the script already decided from the settled
// value (battle_ui_ux_v2 §11.4 J1); this file only maps a tier to size, hit-stop and shake.
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
        private static readonly float[] HitStopMs = { 0f, 40f, 70f, 110f };
        private static readonly float[] ShakeAmplitude = { 6f, 10f, 16f, 24f };
        private static readonly int[] NumberSize = { 56, 72, 88, 108 };

        public static float HitStop(int tier) => HitStopMs[Index(tier)];
        public static float Shake(int tier) => ShakeAmplitude[Index(tier)];
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
