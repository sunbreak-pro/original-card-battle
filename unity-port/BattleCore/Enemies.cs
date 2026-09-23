using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// Enemy data, written as records from enemy_document/enemy_roster_v4.md. Nothing here decides
    /// anything: the decision tree is walked by <see cref="EnemyAi"/> and the numbers are resolved by
    /// the turn loop (#72).
    ///
    /// The slice carries the first normal enemy only (roster §2.1). #51 widens <see cref="All"/> to
    /// the other eleven without changing the record shape — every row below is an
    /// <see cref="EnemyActionDef"/> written off the same face table the cards use (§6.1). The omen
    /// label is not authored any more: <see cref="EnemyAi.LabelOf"/> reads it off the action.
    /// </summary>
    public static class Enemies
    {
        /// <summary>The roster id. Renaming it (and the display name) is #160's.</summary>
        public const string PolearmWarpedId = "polearm_warped";

        /// <summary>
        /// roster §2.1: 長柄の歪み兵. HP 60 / max stamina 10 / recovery 2 / size 1 / punishes the
        /// player who keeps a gap. Five actions, a three-branch tree, a one-step omen, no statuses.
        ///
        /// v4.3 (#162): the reaches, the gap thresholds and 踏み込み (the move the 3+ branch needs,
        /// §6.1) are provisional and match the desk test of battle_core_v4 §21.4; the roster is still
        /// written in 近間 / 遠間 and #160 (cards lane) settles them.
        /// </summary>
        public static readonly EnemyDef PolearmWarped = Build(
            PolearmWarpedId,
            "長柄の歪み兵",
            maxHp: 60,
            maxStamina: 10,
            recovery: 2,
            size: 1,
            branchAtGapZero: new[] { "shove", "guard_up", "reach_thrust" },
            branchAtGapOneToTwo: new[] { "sweep", "reach_thrust", "guard_up" },
            branchAtGapThreePlus: new[] { "step_forward", "guard_up" },
            actions: new[]
            {
                new EnemyActionDef(
                    "sweep", "薙ぎ払い", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: new Reach(1, 2)),
                    new Trait(TraitCondition.GapAtLeast, TraitEffect.PowerBonus, Amount: 3, Threshold: 2),
                    Description: "間合い 1〜2 に届く。間合い 2 以上: 威力 +3"),

                new EnemyActionDef(
                    "shove", "石突きの押し込み", BattleAttribute.Attack | BattleAttribute.Move, 2,
                    new Face(Power: 5, Push: 2, Reach: Reach.Only(0)),
                    new Trait(TraitCondition.Unguarded, TraitEffect.PowerBonus, Amount: 3),
                    Description: "間合い 0 に届く。相手を 2 マス押す。無防備: 相手の Guard が 0 なら威力 +3"),

                new EnemyActionDef(
                    "reach_thrust", "穂先の突き", BattleAttribute.Attack, 1,
                    new Face(Power: 4, Reach: new Reach(0, 2)),
                    Description: "間合い 0〜2 に届く"),

                new EnemyActionDef(
                    "guard_up", "柄で受ける", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    new Trait(TraitCondition.Reserve, TraitEffect.NextTurnRecovery, Amount: 1, Threshold: 4),
                    TargetKind.Self,
                    "温存: 残 4 以上で次の回復 +1"),

                new EnemyActionDef(
                    "step_forward", "踏み込み", BattleAttribute.Move | BattleAttribute.Guard, 1,
                    new Face(Move: 1, Guard: 2),
                    Targets: TargetKind.Self,
                    Description: "前へ 1 動き、Guard 2 を得る"),
            });

        /// <summary>Every enemy the core knows, in roster order. One for the slice; twelve after #51.</summary>
        public static readonly IReadOnlyList<EnemyDef> All = new[] { PolearmWarped };

        public static EnemyDef ById(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            foreach (var def in All)
            {
                if (string.Equals(def.Id, id, StringComparison.Ordinal)) return def;
            }
            throw new KeyNotFoundException($"Unknown enemy id \"{id}\".");
        }

        /// <summary>
        /// Builds a normal enemy (roster §2) and refuses a tree that names an action the enemy does
        /// not have, an empty branch (§6.1: every branch holds something), or a face outside §7.3's
        /// cell limits, so a typo in the data fails at load and not mid-battle.
        /// </summary>
        private static EnemyDef Build(
            string id,
            string name,
            int maxHp,
            int maxStamina,
            int recovery,
            int size,
            IReadOnlyList<string> branchAtGapZero,
            IReadOnlyList<string> branchAtGapOneToTwo,
            IReadOnlyList<string> branchAtGapThreePlus,
            IReadOnlyList<EnemyActionDef> actions)
        {
            if (size < 1 || size > Constants.EnemySizeMax)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, $"Enemy \"{id}\": size is 1..{Constants.EnemySizeMax}.");
            }
            var map = new Dictionary<string, EnemyActionDef>(StringComparer.Ordinal);
            foreach (var action in actions)
            {
                if (map.ContainsKey(action.Id))
                {
                    throw new ArgumentException($"Enemy \"{id}\" defines action \"{action.Id}\" twice.");
                }
                if (Math.Abs(action.Face.Move) > Constants.MoveStepMax || Math.Abs(action.Face.Push) > Constants.MoveStepMax)
                {
                    throw new ArgumentException($"Enemy \"{id}\": action \"{action.Id}\" moves more than {Constants.MoveStepMax} cells.");
                }
                map[action.Id] = action;
            }

            foreach (var branch in new[] { branchAtGapZero, branchAtGapOneToTwo, branchAtGapThreePlus })
            {
                if (branch.Count == 0) throw new ArgumentException($"Enemy \"{id}\" has an empty branch (§6.1).");
                foreach (var actionId in branch)
                {
                    if (!map.ContainsKey(actionId))
                    {
                        throw new ArgumentException($"Enemy \"{id}\" branches to unknown action \"{actionId}\".");
                    }
                }
            }

            return new EnemyDef(
                id, name, maxHp, maxStamina, recovery, size,
                branchAtGapZero, branchAtGapOneToTwo, branchAtGapThreePlus, map);
        }
    }
}
