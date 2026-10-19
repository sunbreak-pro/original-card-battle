// How a card's name and trait line fit their boxes (#210). Pure arithmetic over a measure the View
// hands in (Unity's Text.preferredWidth / preferredHeight with the prefab's font), so the EditMode
// tests can hold the choice with a stand-in measure. The boxes and the largest size are the prefab's.
// The description rows fit the same way, wrapping only once shrinking alone runs out (#567).
using System;
using System.Collections.Generic;

namespace Depiction
{
    /// <summary>The printed size of <paramref name="text"/> at font size <paramref name="size"/>, one line per "\n".</summary>
    public delegate void TextMeasure(string text, int size, out float width, out float height);

    /// <summary>What to print and how large. <see cref="Text"/> carries "\n" where the line breaks.</summary>
    public readonly struct TextFit
    {
        public readonly string Text;
        public readonly int Size;
        /// <summary>False when even the smallest size overflows the box; the View then wraps and clips as a last guard.</summary>
        public readonly bool Fits;

        public TextFit(string text, int size, bool fits)
        {
            Text = text;
            Size = size;
            Fits = fits;
        }
    }

    public static class CardTextFit
    {
        /// <summary>CoreText.TraitLines joins two traits with this; one trait has one space, between its condition and its effect.</summary>
        public const string TraitJoin = " ／ ";

        /// <summary>
        /// The name on one line, at the largest size from <paramref name="maxSize"/> down to
        /// <paramref name="minSize"/> that stays inside the box. A name that wraps lands on the type line.
        /// </summary>
        public static TextFit Name(string name, int maxSize, int minSize, float width, float height, TextMeasure measure)
        {
            return Largest(name, maxSize, minSize, width, height, measure);
        }

        /// <summary>
        /// The trait line on one line, or on two when two lines print larger: two traits one per line
        /// (背水の陣), one trait broken between its condition and its effect. A tie keeps one line.
        /// </summary>
        public static TextFit Trait(string trait, int maxSize, int minSize, float width, float height, TextMeasure measure)
        {
            TextFit one = Largest(trait, maxSize, minSize, width, height, measure);
            string broken = TwoLines(trait);
            if (broken == null || (one.Fits && one.Size >= maxSize)) return one;
            TextFit two = Largest(broken, maxSize, minSize, width, height, measure);
            bool twoIsBetter = two.Fits != one.Fits ? two.Fits : two.Size > one.Size;
            return twoIsBetter ? two : one;
        }

        /// <summary>The trait line broken in two, or null when it has nowhere to break.</summary>
        public static string TwoLines(string trait)
        {
            if (string.IsNullOrEmpty(trait)) return null;
            int join = trait.IndexOf(TraitJoin, StringComparison.Ordinal);
            if (join >= 0) return trait.Substring(0, join) + "\n" + trait.Substring(join + TraitJoin.Length);
            int space = trait.IndexOf(' ');
            return space >= 0 ? trait.Substring(0, space) + "\n" + trait.Substring(space + 1) : null;
        }

        // ---- the description (#567) ---------------------------------------------------------------

        /// <summary>Characters that never open a line (行頭禁則): closing marks, punctuation, small kana, the long vowel.</summary>
        public const string NoLineStart = "、。，．,.：；:;！？!?）)」』】〕］]｝}〉》・ー～…％%ぁぃぅぇぉっゃゅょァィゥェォッャュョ々";

        /// <summary>Characters that never close a line (行末禁則): opening marks.</summary>
        public const string NoLineEnd = "（(「『【〔［[｛{〈《";

        /// <summary>The mark at the head of a lasting row (§6.1 スタンスの札); the View colours it.</summary>
        public const string LastingMark = "∞";

        /// <summary>Whether the face prints lasting rows, which start higher and take four lines.</summary>
        public static bool PrintsLasting(CardFace face)
        {
            return face.Lasting && face.TextLines != null && face.TextLines.Count > 0;
        }

        /// <summary>
        /// The rows the description prints before fitting: <see cref="CardFace.TextLines"/> (each lasting
        /// row behind <see cref="LastingMark"/>), or for a v4.2 face one row per sentence of its description.
        /// </summary>
        public static List<string> DescriptionRows(CardFace face)
        {
            var rows = new List<string>();
            if (face.TextLines != null && face.TextLines.Count > 0)
            {
                bool lasting = PrintsLasting(face);
                foreach (string row in face.TextLines) rows.Add(lasting ? LastingMark + " " + row : row);
                return rows;
            }
            foreach (string sentence in (face.Description ?? "").Replace("。", "。\n").Split('\n'))
            {
                if (sentence.Trim().Length > 0) rows.Add(sentence);
            }
            return rows;
        }

        /// <summary>
        /// The description rows inside the box, one row per line where they fit. First the largest size
        /// from <paramref name="maxSize"/> down to <paramref name="minSize"/> that holds every row on one
        /// line; when even <paramref name="minSize"/> overflows, the rows are wrapped (no punctuation at a
        /// line start, no number or katakana word cut in two) at the largest size whose lines stay inside the box; when
        /// that also overflows, the lines past the box's foot are dropped and <see cref="TextFit.Fits"/> is false.
        /// </summary>
        public static TextFit Description(IList<string> rows, int maxSize, int minSize, float width, float height, TextMeasure measure)
        {
            minSize = Math.Min(minSize, maxSize);
            if (rows == null || rows.Count == 0) return new TextFit("", maxSize, true);
            TextFit shrunk = Largest(string.Join("\n", rows), maxSize, minSize, width, height, measure);
            if (shrunk.Fits) return shrunk;
            for (int size = maxSize; size >= minSize; size--)
            {
                string wrapped = string.Join("\n", Wrap(rows, size, width, measure));
                measure(wrapped, size, out _, out float h);
                if (h <= height) return new TextFit(wrapped, size, true);
            }
            List<string> lines = Wrap(rows, minSize, width, measure);
            while (lines.Count > 1)
            {
                measure(string.Join("\n", lines), minSize, out _, out float h);
                if (h <= height) break;
                lines.RemoveAt(lines.Count - 1);
            }
            return new TextFit(string.Join("\n", lines), minSize, false);
        }

        /// <summary>Each row broken into lines no wider than <paramref name="width"/> at <paramref name="size"/>.</summary>
        public static List<string> Wrap(IList<string> rows, int size, float width, TextMeasure measure)
        {
            var lines = new List<string>();
            foreach (string row in rows)
            {
                string rest = (row ?? "").Trim();
                while (true)
                {
                    if (rest.Length <= 1 || WidthOf(rest, size, measure) <= width)
                    {
                        lines.Add(rest);
                        break;
                    }
                    int cut = 1;
                    while (cut < rest.Length && WidthOf(rest.Substring(0, cut + 1), size, measure) <= width) cut++;
                    int at = cut;
                    while (at > 0 && !CanBreakBefore(rest, at)) at--;
                    if (at == 0) at = cut; // nowhere allowed: cut where the width runs out
                    lines.Add(rest.Substring(0, at).TrimEnd());
                    rest = rest.Substring(at).TrimStart();
                }
            }
            return lines;
        }

        /// <summary>Whether a line may break between <c>text[at - 1]</c> and <c>text[at]</c>.</summary>
        public static bool CanBreakBefore(string text, int at)
        {
            if (at <= 0 || at >= text.Length) return false;
            char before = text[at - 1];
            char after = text[at];
            if (NoLineStart.IndexOf(after) >= 0 || NoLineEnd.IndexOf(before) >= 0) return false;
            if (IsKatakana(before) && IsKatakana(after)) return false; // アタック, スタミナ stay whole
            return !(IsWordChar(before) && IsWordChar(after));
        }

        private static bool IsKatakana(char c)
        {
            return c >= 'ァ' && c <= 'ヺ';
        }

        // "+12", "×2", "Guard": a run of half-width letters, digits and signs stays on one line.
        private static bool IsWordChar(char c)
        {
            return (c < 0x80 && char.IsLetterOrDigit(c)) || c == '+' || c == '-' || c == '−' || c == '×' || c == '＋' || c == '－';
        }

        private static float WidthOf(string text, int size, TextMeasure measure)
        {
            measure(text, size, out float w, out _);
            return w;
        }

        private static TextFit Largest(string text, int maxSize, int minSize, float width, float height, TextMeasure measure)
        {
            minSize = Math.Min(minSize, maxSize);
            for (int size = maxSize; size >= minSize; size--)
            {
                measure(text, size, out float w, out float h);
                if (w <= width && h <= height) return new TextFit(text, size, true);
            }
            return new TextFit(text, minSize, false);
        }
    }
}
