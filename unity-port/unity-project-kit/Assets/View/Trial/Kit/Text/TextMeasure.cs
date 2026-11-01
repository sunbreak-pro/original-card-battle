// Text measurement with TMP's own layout (kit.md §4.9): what is measured is what is drawn. One hidden measuring label
// per layout style (font, size, line height, spacing, white-space), outside every canvas so it never renders.
// Feeds the card text fitting, the flex layout and the labels' auto sizes.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Trial.Kit
{
    public static class TextMeasure
    {
        private static GameObject _host;
        private static readonly Dictionary<string, TextMeshProUGUI> Probes = new Dictionary<string, TextMeshProUGUI>();

        private const float Huge = 100000f;

        private static TextMeshProUGUI Probe(TextStyle style)
        {
            string key = style.LayoutKey();
            if (Probes.TryGetValue(key, out TextMeshProUGUI t) && t) return t;
            if (!_host)
            {
                // TMP lays text out only under an enabled Canvas: a canvas of its own that never shows (display 8, alpha 0).
                _host = new GameObject("TrialTextMeasure", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(_host);
                var canvas = _host.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.targetDisplay = 7;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
                _host.AddComponent<CanvasGroup>().alpha = 0f;
            }
            var go = new GameObject("probe " + key, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(_host.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(Huge, Huge);
            t = go.AddComponent<TextMeshProUGUI>();
            TextStyle s = style.Clone();
            s.Shadows = null;
            s.Balance = false;
            s.LineClamp = 0;
            s.Overflow = TextOverflow.Visible;
            Label.Apply(t, s);
            Probes[key] = t;
            return t;
        }

        private static string Shown(string text, TextStyle style)
        {
            string s = Label.Prepare(text ?? "", style.WhiteSpace);
            return style.AutoPhrase ? Wrap.JaPhrases(s) : s;
        }

        /// <summary>The max-content width of the text (no wrapping but at its newlines), in px.</summary>
        public static float Width(string text, TextStyle style)
        {
            if (style == null) throw new ArgumentNullException(nameof(style));
            if (string.IsNullOrEmpty(text)) return 0f;
            TextMeshProUGUI t = Probe(style);
            // Includes the trailing letter-spacing (Label's right margin) when positive; a negative one is added here.
            return Math.Max(0f, t.GetPreferredValues(Shown(text, style), Huge, Huge).x + Math.Min(0f, style.LetterSpacingPx));
        }

        /// <summary>How many lines the text takes at a box width (the style's wrapping; NoWrap = its newlines only).</summary>
        public static int Lines(string text, TextStyle style, float width)
        {
            if (style == null) throw new ArgumentNullException(nameof(style));
            if (string.IsNullOrEmpty(text)) return 0;
            TextMeshProUGUI t = Probe(style);
            t.rectTransform.sizeDelta = new Vector2(Math.Max(1f, width), Huge);
            t.text = Shown(text, style);
            t.ForceMeshUpdate(true, true);
            int n = Math.Max(1, t.textInfo.lineCount);
            if (style.LineClamp > 0) n = Math.Min(n, style.LineClamp);
            return n;
        }

        /// <summary>The box height at a width: lines × line height (CSS line boxes).</summary>
        public static float Height(string text, TextStyle style, float width) => Lines(text, style, width) * style.LineHeightPx;

        /// <summary>The CSS box size: (max-content width, or `width` when given) × lines × line height.</summary>
        public static Vector2 Size(string text, TextStyle style, float? width = null)
        {
            float w = width ?? Width(text, style);
            return new Vector2(w, Height(text, style, w + 0.01f));
        }

        /// <summary>The widest line when wrapped at `width` (the shrink-to-fit width of a wrapped box).</summary>
        public static float WidestLine(string text, TextStyle style, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            TextMeshProUGUI t = Probe(style);
            t.rectTransform.sizeDelta = new Vector2(Math.Max(1f, width), Huge);
            t.text = Shown(text, style);
            t.ForceMeshUpdate(true, true);
            float w = 0f;
            TMP_TextInfo info = t.textInfo;
            for (int i = 0; i < info.lineCount; i++) w = Math.Max(w, info.lineInfo[i].length);
            return w;
        }

        /// <summary>Drops the measuring labels (fonts changed, or to free them).</summary>
        public static void Clear()
        {
            foreach (TextMeshProUGUI t in Probes.Values) if (t) UnityEngine.Object.Destroy(t.gameObject);
            Probes.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Probes.Clear();
            // The host is HideAndDontSave, so it outlives a Play session; its probes point at destroyed fonts.
            if (_host) UnityEngine.Object.DestroyImmediate(_host);
            _host = null;
        }
    }
}
