// How a card's name and trait line fit their boxes (#210). Pure arithmetic over a measure the View
// hands in (Unity's Text.preferredWidth / preferredHeight with the prefab's font), so the EditMode
// tests can hold the choice with a stand-in measure. The boxes and the largest size are the prefab's.
using System;

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
