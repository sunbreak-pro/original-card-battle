// The floor the screen draws (battle-visual-v1 §4.2, #242): which cells a card reaches from where the
// player stands, which cells the shown omen aims at, and whether the player stands in them. Every
// cell is read from the core (EnemyAi.TargetCells, the card's printed reach, Field's gap), so the
// View only draws. Pure C#: BattleCore + Depiction.Script, no UnityEngine.
using System;
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    public static class FloorCells
    {
        /// <summary>
        /// §4.2 届くマスの光: the cells a card aimed at an opponent reaches, from the player's front edge
        /// out: gap g of the card's reach is the cell g + 1 past it (battle_core_v4 §7.2's N). Cells past
        /// the end of the line are left out. Empty for a card that aims at nobody.
        /// </summary>
        public static List<int> ReachOf(CardDef def, int playerCell, int playerSize, int fieldCells)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var cells = new List<int>();
            if (!EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets)) return cells;
            Reach reach = def.Face.ReachOrDefault;
            int front = playerCell + Math.Max(1, playerSize) - 1;
            for (int gap = reach.Min; gap <= reach.Max; gap++)
            {
                int cell = front + 1 + gap;
                if (cell >= 1 && cell <= fieldCells) cells.Add(cell);
            }
            return cells;
        }

        /// <summary>
        /// §4.2 狙うマス: the cells the omen aims at with the enemy standing on <paramref name="enemyCell"/>
        /// (EnemyAi.TargetCells, lowest first). Empty for an omen that aims at nobody.
        /// </summary>
        public static List<int> AimOf(Omen omen, CombatantState enemy, int enemyCell)
        {
            var cells = new List<int>();
            if (omen == null || enemy == null) return cells;
            cells.AddRange(EnemyAi.TargetCells(omen.Label, enemy with { Cell = enemyCell }));
            return cells;
        }

        /// <summary>
        /// §4.4 当たり外れの印: Lands when one of the player's cells is aimed at, Misses when none is, and
        /// None for an omen that aims at nobody (自分にだけ効く行動には出しません).
        /// </summary>
        public static OmenHit HitOf(Omen omen, IReadOnlyList<int> aim, int playerCell, int playerSize)
        {
            if (omen == null || omen.Label.Reach == null) return OmenHit.None;
            for (int cell = playerCell; cell < playerCell + Math.Max(1, playerSize); cell++)
            {
                if (Contains(aim, cell)) return OmenHit.Lands;
            }
            return OmenHit.Misses;
        }

        private static bool Contains(IReadOnlyList<int> cells, int cell)
        {
            foreach (int c in cells)
            {
                if (c == cell) return true;
            }
            return false;
        }
    }
}
