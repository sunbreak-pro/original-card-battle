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
        // ---- Shapes the catalog dropped in swordsman_cards_v4 v4.10 (#824) ----
        // The rules they exercised stay in the core (a stance that guards every turn start, 移動後
        // on a stance, a card with two traits), so the tests keep them as stand-ins (#825).

        /// <summary>The v4.9 #17 岩の構え: stance, 3 Guard at every turn start; +3 Guard now into an attack omen.</summary>
        public static readonly CardDef TurnGuardStance = new CardDef(
            "turn_guard_stance", "試しの受けの構え", BattleAttribute.Stance, 2,
            new Face(Stance: new StanceDef(StanceHook.TurnStart, Guard: 3)),
            new Trait(TraitCondition.OmenIs, TraitEffect.GuardBonus, 3, Omen: OmenKind.Attack),
            Targets: TargetKind.Self);

        /// <summary>The v4.9 #18 流れの構え: stance, +5 on attacks in a turn something moved; draws after a move.</summary>
        public static readonly CardDef MovedPowerStance = new CardDef(
            "moved_power_stance", "試しの動きの構え", BattleAttribute.Stance, 3,
            new Face(Stance: new StanceDef(StanceHook.AttackBonus, StanceWhen.MovedThisTurn, Power: 5)),
            new Trait(TraitCondition.Moved, TraitEffect.Draw, 1),
            Targets: TargetKind.Self);

        /// <summary>The v4.9 #80 背水の陣 (attack): +5 at a gap of 2 or more, and +3 more when desperate.</summary>
        public static readonly CardDef TwoTraitAttack = new CardDef(
            "two_trait_attack", "試しの二つの特性", BattleAttribute.Attack, 1,
            new Face(Power: 8, Reach: new Reach(1, 2)),
            new Trait(TraitCondition.GapAtLeast, TraitEffect.PowerBonus, 5, Threshold: 2),
            ExtraTrait: new Trait(TraitCondition.Desperate, TraitEffect.PowerBonus, 3));

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
            TargetKind targets = TargetKind.One)
        {
            return new EnemyActionDef(id, id, attributes, column, face ?? new Face(), trait, targets, string.Empty);
        }

        public static CombatantState Combatant(
            int hp = Constants.PlayerMaxHp,
            int stamina = Constants.BaseMaxStamina,
            int guard = 0,
            int cell = Constants.PlayerStartCell,
            int size = 1,
            StatusSet? statuses = null)
        {
            return new CombatantState(
                hp, hp, stamina, Constants.BaseMaxStamina, guard,
                cell, size, statuses ?? StatusSet.Empty);
        }

        /// <summary>
        /// A three-branch enemy with one action per branch, the shape #70 fills in. Size 1 unless
        /// asked otherwise; every action reaches 0〜1 unless its face says so.
        /// </summary>
        public static EnemyDef Enemy(
            string id = "dummy",
            int size = 1,
            EnemyActionDef? atZero = null,
            EnemyActionDef? atOneToTwo = null,
            EnemyActionDef? atThreePlus = null)
        {
            atZero ??= EnemyAction("near_hit", face: new Face(Power: 5));
            atOneToTwo ??= EnemyAction("mid_hit", face: new Face(Power: 4, Reach: new Reach(1, 2)));
            atThreePlus ??= EnemyAction("advance", face: new Face(Move: 1), attributes: BattleAttribute.None, targets: TargetKind.Self);
            var actions = new Dictionary<string, EnemyActionDef>();
            foreach (var a in new[] { atZero, atOneToTwo, atThreePlus }) actions[a.Id] = a;
            return new EnemyDef(
                id, id, MaxHp: 60, MaxStamina: 10, Recovery: 2, Size: size,
                BranchAtGapZero: new[] { atZero.Id },
                BranchAtGapOneToTwo: new[] { atOneToTwo.Id },
                BranchAtGapThreePlus: new[] { atThreePlus.Id },
                Actions: actions);
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
