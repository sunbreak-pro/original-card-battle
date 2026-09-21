using System.Collections.Generic;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// Minimal stand-ins so a test can say what it is about in one line. These are not the real
    /// prototype cards (#71) or the real polearm (#70) — those arrive as data on top of these types.
    /// </summary>
    internal static class Fixtures
    {
        public static CardDef Card(
            string id,
            int column = 1,
            Face? face = null,
            BattleAttribute attributes = BattleAttribute.Attack,
            Trait? trait = null,
            TargetKind targets = TargetKind.One)
        {
            return new CardDef(id, id, attributes, column, face ?? new Face(), trait, targets, string.Empty);
        }

        public static EnemyActionDef EnemyAction(
            string id,
            int column = 1,
            Face? face = null,
            BattleAttribute attributes = BattleAttribute.Attack,
            Trait? trait = null,
            OmenLabel? omen = null)
        {
            return new EnemyActionDef(
                id, id, attributes, column, face ?? new Face(),
                omen ?? new OmenLabel(OmenKind.Attack), trait, string.Empty);
        }

        public static CombatantState Combatant(
            int hp = Constants.PlayerMaxHp,
            int stamina = Constants.BaseMaxStamina,
            int guard = 0,
            Position? position = null,
            StatusSet? statuses = null)
        {
            return new CombatantState(
                hp, hp, stamina, Constants.BaseMaxStamina, guard,
                position, statuses ?? StatusSet.Empty);
        }

        /// <summary>A 20-card deck of ten kinds × 2, the shape the slice's prototype deck takes (§8).</summary>
        public static List<CardInstance> TwentyCardDeck()
        {
            var defs = new List<CardDef>();
            for (int i = 0; i < 10; i++)
            {
                defs.Add(Card($"card_{i}", column: (i % 3) + 1, face: new Face(Power: 6 + i)));
            }
            return Cards.BuildDeck(defs, copies: 2);
        }
    }
}
