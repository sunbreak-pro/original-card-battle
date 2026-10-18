using System;

namespace DungeonCore
{
    /// <summary>
    /// How many cells the battle behind a combat node is fought on
    /// (`.claude/docs/danjeon_document/seven_layers_v4.md` §2.4, Issue #168). The cell rules are the
    /// battle core's (battle_core_v4 §7); this table only holds the per-layer values, and #99 hands
    /// them across.
    ///
    /// 通常戦闘 is 6 on layers 1〜3, 7 on 4〜6 and 8 on 7. 精鋭 is one cell wider than the layer.
    /// 階層ボス is 8 on every layer. A line is never narrower than every enemy of the fight needs:
    /// 5 + the sum of their sizes, since the player stands on cell 2 and the first enemy on cell 6.
    /// </summary>
    public static class BattleField
    {
        /// <summary>battle_core_v4 §7.1: the narrowest line a battle may be handed.</summary>
        public const int MinCells = 5;

        /// <summary>battle_core_v4 §7.1: the widest line. No fight that needs more is put on a node.</summary>
        public const int MaxCells = 8;

        /// <summary>
        /// §2.4: the cells before the first enemy's near edge — the player's cell 2 plus the opening gap
        /// of 3. A tool that moves the player back to cell 1 widens the gap instead, so this stays.
        /// </summary>
        public const int CellsBeforeEnemies = 5;

        /// <summary>The layer's line for a node of this kind, before the enemies' sizes are counted.</summary>
        public static int Cells(int layer, NodeKind kind)
        {
            if (layer < 1 || layer > SevenLayers.Count)
                throw new ArgumentOutOfRangeException(nameof(layer), layer, $"the lair has {SevenLayers.Count} layers");

            int normal = layer <= 3 ? 6 : layer <= 6 ? 7 : 8;
            return kind switch
            {
                NodeKind.Battle => normal,
                NodeKind.Elite => Math.Min(MaxCells, normal + 1),
                NodeKind.Boss => MaxCells,
                _ => throw new ArgumentException($"a {kind} node holds no fight", nameof(kind)),
            };
        }

        /// <summary>
        /// §2.4: the layer's line, widened until every enemy of the fight can stand on it.
        /// <paramref name="enemySizes"/> is the sum of the cells the fight's enemies use.
        /// </summary>
        public static int CellsFor(int layer, NodeKind kind, int enemySizes)
        {
            if (enemySizes < 1) throw new ArgumentOutOfRangeException(nameof(enemySizes), enemySizes, "a fight has at least one enemy");

            int needed = CellsBeforeEnemies + enemySizes;
            if (needed > MaxCells)
                throw new ArgumentException($"enemies of {enemySizes} cells need {needed} cells; a line holds {MaxCells}", nameof(enemySizes));
            return Math.Max(Cells(layer, kind), needed);
        }
    }
}
