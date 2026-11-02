// The battle screen's typefaces, one entry per row of battle-visual-v1 §3.1 (#612), so a label names the
// row it belongs to and the table has one place to follow. UiKit.Label / UiKit.Text still make every label
// with UiKit.Font (UiKit.cs is the design lane's), so each view swaps the face after making it.
//
// The faces are the 700 / 600 files themselves: Unity's legacy Text thickens a face again for
// FontStyle.Bold, so Use puts the style back to Normal. IBM Plex Mono has no Japanese glyphs, so a label
// that mixes words and digits keeps its words in a body face and its digits in a Text of their own.
#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEngine.UI;

namespace Depiction.View
{
    public static class BattleFonts
    {
        /// <summary>§3.1 64 px, Shippori Mincho 800 (the 700 file for now): the turn banners.</summary>
        public static Font Banner => UiKit.HeadingFont;

        /// <summary>§3.1 48 px, IBM Plex Mono 700: the omen's value, the held card's preview, the enemy turn's HP hit.</summary>
        public static Font OmenNumber => UiKit.NumberFont;

        /// <summary>§3.1 36 px, IBM Plex Mono 700: a card's values.</summary>
        public static Font CardValue => UiKit.NumberFont;

        /// <summary>§3.1 34 px, IBM Plex Mono 700: the stamina count at the lower left, the enemy turn's Guard hit.</summary>
        public static Font StaminaNumber => UiKit.NumberFont;

        /// <summary>§3.1 28 px, IBM Plex Mono 700: the gap, a card's cost, the plan's value.</summary>
        public static Font SmallNumber => UiKit.NumberFont;

        /// <summary>§3.1 24 px, IBM Plex Mono 700: HP, Guard, an enemy's stamina, the pay preview "▸ 5".</summary>
        public static Font PanelNumber => UiKit.NumberFont;

        /// <summary>§3.1 24 px, Zen Kaku Gothic New 700: the end-turn button.</summary>
        public static Font PanelWord => UiKit.BodyBoldFont;

        /// <summary>§3.1 22 px, Shippori Mincho 700: a card's name.</summary>
        public static Font CardName => UiKit.HeadingFont;

        /// <summary>§3.1 20 px, IBM Plex Mono 600: the corner info's numbers and the battle speed.</summary>
        public static Font CornerNumber => UiKit.NumberSemiBoldFont;

        /// <summary>§3.1 20 px, Shippori Mincho 700: a stance's name.</summary>
        public static Font StanceName => UiKit.HeadingFont;

        /// <summary>§3.1 18 px, Zen Kaku Gothic New 700: a tooltip's heading.</summary>
        public static Font TipHeading => UiKit.BodyBoldFont;

        /// <summary>§3.1 16 px, Zen Kaku Gothic New 400: card text and traits, chips, tooltip bodies, the omen's kind word.</summary>
        public static Font Body => UiKit.BodyFont;

        /// <summary>§6.1: the trait box's effect row, 16 px set off in the 700 face.</summary>
        public static Font BodyStrong => UiKit.BodyBoldFont;

        /// <summary>§7.3 / §4.6: a chip's stacks and a stance's "×2" (16 px, monospaced).</summary>
        public static Font Stacks => UiKit.NumberSemiBoldFont;

        /// <summary>§3.1 14 px, Zen Kaku Gothic New: 「予定」, the played-attribute counts, "▼1", the tooltip's family word, a card's 「自分」.</summary>
        public static Font Fine => UiKit.BodyFont;

        /// <summary>Puts <paramref name="face"/> on <paramref name="text"/> with no synthetic bold over it.</summary>
        public static Text Use(Text text, Font face)
        {
            if (!text || !face) return text;
            text.font = face;
            text.fontStyle = FontStyle.Normal;
            return text;
        }
    }
}
#endif
