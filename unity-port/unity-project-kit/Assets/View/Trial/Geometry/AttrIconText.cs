// #828: a card's trait text names the four attributes (攻撃 / 防御 / スキル / スタンス) by their icons, not their words.
// The text is split here, engine-free: each word becomes one placeholder character that the label prints invisibly
// (so measuring, fitting and wrapping see the slot the icon takes), and the kit draws the attribute's icon over it.
// A move card is a skill on screen (CardFaceText.AttributeWord), so there is no word for it here.
using System;
using System.Collections.Generic;
using System.Text;

namespace Trial.Geometry
{
    public sealed class AttrIconText
    {
        /// <summary>
        /// The slot an icon takes: one full-width character that the game's text never prints (the geta mark), so the
        /// k-th one in a laid-out label is the k-th icon even after the text was wrapped.
        /// </summary>
        public const char Slot = '〓';

        /// <summary>The words, in the DS's attribute order, and the attribute key (Ocb.Attr) each one stands for.</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> Words = new[]
        {
            new KeyValuePair<string, string>("攻撃", "attack"),
            new KeyValuePair<string, string>("防御", "defense"),
            new KeyValuePair<string, string>("スキル", "skill"),
            new KeyValuePair<string, string>("スタンス", "stance"),
        };

        // TMP hides a character only through rich text; the slot keeps its advance, so widths do not change. A clear
        // colour rather than <alpha>: TMP has no </alpha> (it would print as eight literal characters and leave the rest
        // of the label at alpha 0), and </color> pops back to the label's own colour, so a translucent label stays so.
        private const string HideOpen = "<color=#00000000>";
        private const string HideClose = "</color>";

        private readonly int[] _map;

        private AttrIconText(string original, string display, IReadOnlyList<string> attrs, int[] map)
        {
            Original = original;
            Display = display;
            Attrs = attrs;
            _map = map;
        }

        /// <summary>The text as the data wrote it.</summary>
        public string Original { get; }

        /// <summary>The text with each attribute word replaced by <see cref="Slot"/>: what is measured and laid out.</summary>
        public string Display { get; }

        /// <summary>The attribute key of each slot, in order.</summary>
        public IReadOnlyList<string> Attrs { get; }

        /// <summary>The text has at least one attribute word.</summary>
        public bool HasIcons => Attrs.Count > 0;

        /// <summary>What a label prints: the rich text of <see cref="Display"/> when there are icons, else the text untouched.</summary>
        public string Printed => HasIcons ? Rich(Display) : Original;

        /// <summary>Splits <paramref name="text"/> (null reads as "").</summary>
        public static AttrIconText Of(string text)
        {
            text = text ?? "";
            var sb = new StringBuilder(text.Length);
            var attrs = new List<string>();
            var map = new int[text.Length + 1];
            int i = 0;
            while (i < text.Length)
            {
                string word = null, attr = null;
                foreach (KeyValuePair<string, string> w in Words)
                {
                    if (string.CompareOrdinal(text, i, w.Key, 0, w.Key.Length) != 0) continue;
                    word = w.Key;
                    attr = w.Value;
                    break;
                }
                if (word == null)
                {
                    map[i] = sb.Length;
                    sb.Append(text[i]);
                    i++;
                    continue;
                }
                // Every character of the word maps to its slot, so a range that starts or ends inside it still lands on it.
                for (int k = 0; k < word.Length; k++) map[i + k] = sb.Length;
                sb.Append(Slot);
                attrs.Add(attr);
                i += word.Length;
            }
            map[text.Length] = sb.Length;
            return new AttrIconText(text, sb.ToString(), attrs, map);
        }

        /// <summary>The position in <see cref="Display"/> of position <paramref name="index"/> of <see cref="Original"/> (clamped).</summary>
        public int MapIndex(int index)
        {
            if (index <= 0) return 0;
            return index >= _map.Length ? _map[_map.Length - 1] : _map[index];
        }

        /// <summary>
        /// A range of <see cref="Original"/> as a range of <see cref="Display"/>: a range that ends inside a word takes the
        /// whole slot. Length 0 when nothing of the range is printed as text.
        /// </summary>
        public void MapRange(int start, int length, out int displayStart, out int displayLength)
        {
            displayStart = MapIndex(start);
            displayLength = 0;
            if (length <= 0) return;
            int end = start + length;
            int displayEnd = MapIndex(end);
            // the end falls inside a word: the slot it stands in counts as part of the range
            if (end > 0 && end < Original.Length && displayEnd > 0 && MapIndex(end - 1) == displayEnd) displayEnd++;
            displayLength = Math.Max(0, displayEnd - displayStart);
        }

        /// <summary>
        /// TMP rich text for a display string (wrapped or not): each slot hidden, every other run in noparse so a '<' in
        /// the text stays text.
        /// </summary>
        public static string Rich(string display)
        {
            if (string.IsNullOrEmpty(display)) return display ?? "";
            var sb = new StringBuilder(display.Length + 16);
            int run = 0;
            for (int i = 0; i <= display.Length; i++)
            {
                if (i < display.Length && display[i] != Slot) continue;
                if (i > run) AppendText(sb, display.Substring(run, i - run));
                if (i < display.Length) sb.Append(HideOpen).Append(Slot).Append(HideClose);
                run = i + 1;
            }
            return sb.ToString();
        }

        private static void AppendText(StringBuilder sb, string text)
        {
            if (text.IndexOf('<') < 0) sb.Append(text);
            else sb.Append("<noparse>").Append(text).Append("</noparse>");
        }
    }
}
