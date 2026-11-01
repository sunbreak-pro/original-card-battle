// The two CSS line-breaking features TMP has no rule for (kit.md §4.9, §6 gaps):
// - text-wrap: balance → Balance: the narrowest width that keeps the line count (Chrome balances up to 6 lines),
//   applied as side margins so the box itself keeps its width and alignment.
// - word-break: auto-phrase → JaPhrases: a heuristic bunsetsu split (Chrome uses a phrase model) whose phrases are
//   wrapped in <nobr>, so lines break between phrases only. Review the screens by eye; it is not Chrome's segmenter.
using System;
using System.Text;
using TMPro;
using UnityEngine;

namespace Trial.Kit
{
    public static class Wrap
    {
        /// <summary>Chrome balances blocks of at most this many lines.</summary>
        public const int BalanceMaxLines = 6;

        private const int DefaultMaxPhraseLength = 12;

        /// <summary>Phrases longer than this stay breakable (an unbreakable run would overflow).</summary>
        public static int MaxPhraseLength = DefaultMaxPhraseLength;

        // Domain reload is off: a value tuned in one Play session must not leak into the next.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => MaxPhraseLength = DefaultMaxPhraseLength;

        /// <summary>
        /// text-wrap: balance on a wrapped label: finds the narrowest wrap width that keeps the line count and moves
        /// the free room into the margins (right for left-aligned text, left for right-aligned, both for centred).
        /// </summary>
        public static void Balance(TMP_Text t)
        {
            if (!t || string.IsNullOrEmpty(t.text)) return;
            if (t.textWrappingMode == TextWrappingModes.NoWrap || t.textWrappingMode == TextWrappingModes.PreserveWhitespaceNoWrap) return;
            Vector4 m = t.margin;
            float top = m.y, bottom = m.w;
            LabelState st = t.GetComponent<LabelState>();
            float baseRight = st ? st.MarginRight : 0f; // the trailing letter-spacing (Label)
            float full = t.rectTransform.rect.width;
            if (full <= 0f) return;
            int lines = LinesAt(t, full, top, bottom, baseRight);
            if (lines <= 1 || lines > BalanceMaxLines)
            {
                t.margin = new Vector4(0f, top, baseRight, bottom);
                t.ForceMeshUpdate(true, true);
                return;
            }
            float lo = full / lines * 0.5f, hi = full;
            for (int i = 0; i < 14 && hi - lo > 0.25f; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (LinesAt(t, mid, top, bottom, baseRight) <= lines) hi = mid; else lo = mid;
            }
            float extra = Math.Max(0f, full - hi);
            switch (t.horizontalAlignment)
            {
                case HorizontalAlignmentOptions.Right: t.margin = new Vector4(extra, top, baseRight, bottom); break;
                case HorizontalAlignmentOptions.Center: t.margin = new Vector4(extra * 0.5f, top, baseRight + extra * 0.5f, bottom); break;
                default: t.margin = new Vector4(0f, top, baseRight + extra, bottom); break;
            }
            t.ForceMeshUpdate(true, true);
        }

        private static int LinesAt(TMP_Text t, float width, float top, float bottom, float baseRight)
        {
            float full = t.rectTransform.rect.width;
            t.margin = new Vector4(0f, top, baseRight + Math.Max(0f, full - width), bottom);
            t.ForceMeshUpdate(true, true);
            return t.textInfo.lineCount;
        }

        private const string Particles = "はがをにでとのへもや";
        private const string BreakAfter = "、。，．！？!?」』）)】〕》〉";
        private const string BreakBefore = "「『（(【〔《〈";

        public static bool IsHiragana(char c) => c >= 'ぁ' && c <= 'ゟ';
        public static bool IsKatakana(char c) => (c >= '゠' && c <= 'ヿ') || (c >= 'ㇰ' && c <= 'ㇿ');
        public static bool IsKanji(char c) => (c >= '一' && c <= '鿿') || (c >= '㐀' && c <= '䶿') || c == '々' || c == '〆' || c == 'ヶ';

        /// <summary>Whether a phrase ends between a and b (the auto-phrase heuristic).</summary>
        public static bool PhraseBreak(char a, char b)
        {
            if (char.IsWhiteSpace(a) || char.IsWhiteSpace(b)) return true;
            if (BreakAfter.IndexOf(a) >= 0) return BreakAfter.IndexOf(b) < 0; // keep 。」 together
            if (BreakBefore.IndexOf(b) >= 0) return true;
            if (BreakAfter.IndexOf(b) >= 0 || b == 'ー' || b == '〜' || b == '…') return false;
            bool aHira = IsHiragana(a), bHira = IsHiragana(b);
            if (aHira && Particles.IndexOf(a) >= 0 && !bHira) return true;      // 技と|記憶は|次の|生へ
            if (aHira && !bHira && (IsKanji(b) || IsKatakana(b) || char.IsLetterOrDigit(b))) return true; // 変わる|記憶
            if (!aHira && !IsKanji(a) && !IsKatakana(a) && IsKanji(b)) return true;
            return false;
        }

        /// <summary>
        /// word-break: auto-phrase: wraps every phrase of 2..MaxPhraseLength characters in &lt;nobr&gt; (TMP rich text must
        /// be on). Text that already has tags is returned unchanged.
        /// </summary>
        public static string JaPhrases(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') >= 0) return text ?? "";
            var sb = new StringBuilder(text.Length + 32);
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                bool end = i == text.Length - 1 || text[i] == '\n' || text[i + 1] == '\n' || PhraseBreak(text[i], text[i + 1]);
                if (!end) continue;
                Append(sb, text, start, i + 1);
                start = i + 1;
            }
            if (start < text.Length) Append(sb, text, start, text.Length);
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, string text, int from, int to)
        {
            int len = to - from;
            bool wrap = len >= 2 && len <= MaxPhraseLength && text.IndexOf('\n', from, len) < 0;
            if (wrap) sb.Append("<nobr>");
            sb.Append(text, from, len);
            if (wrap) sb.Append("</nobr>");
        }
    }
}
