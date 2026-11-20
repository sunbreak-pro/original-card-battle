using System;

namespace DungeonCore
{
    /// <summary>
    /// Which part of the lair a layer belongs to (seven_layers_v4.md §3.1). Layers 1 and 2 are shared; at the end of
    /// layer 2 the explorer picks one of the three regions, and layers 3 to 7 belong to it. Layer 7 is the region's
    /// king room.
    /// </summary>
    public enum Region
    {
        /// <summary>Layers 1 and 2, walked by every life.</summary>
        Common,

        /// <summary>竜の領域. Its king is 竜王.</summary>
        Dragon,

        /// <summary>寄生の領域. Its king is 爛れた世界樹.</summary>
        Parasite,

        /// <summary>亡霊の領域. Its king is 死霊王.</summary>
        Ghost,
    }

    public static class RegionExtensions
    {
        /// <summary>Stable lowercase token, for saves (the lineage's Calendar keys) and bindings.</summary>
        public static string ToToken(this Region region) => region switch
        {
            Region.Common => "common",
            Region.Dragon => "dragon",
            Region.Parasite => "parasite",
            Region.Ghost => "ghost",
            _ => throw new ArgumentOutOfRangeException(nameof(region), region, null),
        };

        /// <summary>The region as the screen names it. UI text, so Japanese.</summary>
        public static string Label(this Region region) => region switch
        {
            Region.Common => "共通",
            Region.Dragon => "竜",
            Region.Parasite => "寄生",
            Region.Ghost => "亡霊",
            _ => throw new ArgumentOutOfRangeException(nameof(region), region, null),
        };

        /// <summary>The region a token names, or false for a token no region has.</summary>
        public static bool TryParseToken(string? token, out Region region)
        {
            foreach (Region r in (Region[])Enum.GetValues(typeof(Region)))
            {
                if (string.Equals(r.ToToken(), token, StringComparison.Ordinal))
                {
                    region = r;
                    return true;
                }
            }
            region = Region.Common;
            return false;
        }

        /// <summary>One of the three regions that have a king (every region but Common).</summary>
        public static bool HasKing(this Region region) =>
            region == Region.Dragon || region == Region.Parasite || region == Region.Ghost;
    }
}
