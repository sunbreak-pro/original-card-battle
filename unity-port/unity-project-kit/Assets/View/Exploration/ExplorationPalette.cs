// Stand-in colours for the exploration screen: shades of grey and one accent (dungeon_exploration_v4.md
// §6.10). Deliberately not BattleTheme — the visual direction is the design lane's, and the real
// colours for the map and its nodes come with #90. Replacing the look starts here.
#if UNITY_2021_2_OR_NEWER
using UnityEngine;

namespace Exploration.View
{
    public static class ExplorationPalette
    {
        public static readonly Color Ground = Grey(0.10f);
        public static readonly Color Band = Grey(0.16f);
        public static readonly Color Panel = Grey(0.13f);
        public static readonly Color Button = Grey(0.26f);
        public static readonly Color Empty = Grey(0.24f);
        public static readonly Color Line = Grey(0.05f);
        public static readonly Color Link = Grey(0.30f);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);

        public static readonly Color Ink = Grey(0.92f);
        public static readonly Color InkSoft = Grey(0.58f);
        public static readonly Color InkOnNode = Grey(0.06f);
        public static readonly Color InkOnAccent = Grey(0.06f);

        /// <summary>The one accent: what can be done now, and the gauge.</summary>
        public static readonly Color Accent = new Color(0.93f, 0.74f, 0.32f, 1f);
        public static readonly Color AccentGhost = new Color(0.93f, 0.74f, 0.32f, 0.35f);

        /// <summary>A step that ends the life. The accent, darkened, so the palette keeps one hue.</summary>
        public static readonly Color Warning = new Color(0.62f, 0.36f, 0.12f, 1f);

        public static Color NodeFill(NodeStanding standing) => standing switch
        {
            NodeStanding.Current => Grey(0.88f),
            NodeStanding.Reachable => Grey(0.66f),
            NodeStanding.Revisit => Grey(0.50f),
            NodeStanding.Resolved => Grey(0.30f),
            _ => Grey(0.20f),
        };

        public static Color FrameColour(NodeStanding standing) =>
            standing == NodeStanding.Current || standing == NodeStanding.Reachable ? Accent : Grey(0.80f);

        public static float FrameThickness(NodeStanding standing) => standing switch
        {
            NodeStanding.Current => 6f,
            NodeStanding.Reachable => 4f,
            NodeStanding.Revisit => 2f,
            _ => 0f,
        };

        private static Color Grey(float v) => new Color(v, v, v, 1f);
    }
}
#endif
