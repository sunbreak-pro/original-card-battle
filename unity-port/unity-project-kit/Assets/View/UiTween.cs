// Minimal coroutine tween so the View needs no DOTween. Every duration in the
// spec (battle_ui_ux_v1.md §5) is written in ms and passed here unchanged; the
// global Speed factor implements fast-forward (×8) and reduced motion (×1000)
// without touching the settled values.
#if UNITY_2021_2_OR_NEWER
using System;
using System.Collections;
using UnityEngine;

public enum Ease
{
    Linear,
    In,
    Out,
    InOut,
}

public static class UiTween
{
    /// <summary>1 = spec timing, 8 = fast-forward, 1000 = reduced motion.</summary>
    public static float Speed = 1f;

    public static float Apply(Ease ease, float t)
    {
        t = Mathf.Clamp01(t);
        switch (ease)
        {
            case Ease.In: return t * t * t;
            case Ease.Out: { float u = 1f - t; return 1f - u * u * u; }
            case Ease.InOut: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
            default: return t;
        }
    }

    /// <summary>Drives <paramref name="apply"/> with an eased 0..1 over <paramref name="ms"/>.</summary>
    public static IEnumerator Run(float ms, Ease ease, Action<float> apply)
    {
        float duration = ms / 1000f;
        if (duration <= 0f)
        {
            apply(1f);
            yield break;
        }
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * Speed / duration;
            apply(Apply(ease, Mathf.Clamp01(t)));
            yield return null;
        }
        apply(1f);
    }

    public static IEnumerator Wait(float ms)
    {
        float duration = ms / 1000f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime * Speed;
            yield return null;
        }
    }

    // ---- common shapes ----------------------------------------------------------

    public static IEnumerator Move(RectTransform rt, Vector2 from, Vector2 to, float ms, Ease ease)
    {
        return Run(ms, ease, t => { if (rt) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t); });
    }

    public static IEnumerator Fade(CanvasGroup group, float from, float to, float ms, Ease ease)
    {
        return Run(ms, ease, t => { if (group) group.alpha = Mathf.Lerp(from, to, t); });
    }

    public static IEnumerator Scale(RectTransform rt, Vector3 from, Vector3 to, float ms, Ease ease)
    {
        return Run(ms, ease, t => { if (rt) rt.localScale = Vector3.LerpUnclamped(from, to, t); });
    }

    public static IEnumerator Tint(UnityEngine.UI.Graphic g, Color from, Color to, float ms, Ease ease)
    {
        return Run(ms, ease, t => { if (g) g.color = Color.Lerp(from, to, t); });
    }

    /// <summary>scale 1.2 → 0.8 → 1 "pop / crack" used by the shield badge.</summary>
    public static IEnumerator Pop(RectTransform rt, float ms)
    {
        return Run(ms, Ease.Out, t =>
        {
            float s = t < 0.5f ? Mathf.Lerp(1.2f, 0.8f, t * 2f) : Mathf.Lerp(0.8f, 1f, (t - 0.5f) * 2f);
            if (rt) rt.localScale = new Vector3(s, s, 1f);
        });
    }

    /// <summary>Horizontal shake of ±amplitude px, <paramref name="cycles"/> round trips over ms.</summary>
    public static IEnumerator Shake(RectTransform rt, float amplitude, int cycles, float ms)
    {
        if (!rt) yield break;
        Vector2 origin = rt.anchoredPosition;
        yield return Run(ms, Ease.Linear, t =>
        {
            float x = Mathf.Sin(t * Mathf.PI * 2f * cycles) * amplitude * (1f - t);
            if (rt) rt.anchoredPosition = origin + new Vector2(x, 0f);
        });
        if (rt) rt.anchoredPosition = origin;
    }
}
#endif
