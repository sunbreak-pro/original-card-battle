using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// §6 / §6.1 and roster §1.2: how an enemy picks its next action and what it shows of it.
    ///
    /// There is no randomness here. The tree is walked top-down and the first action that can be
    /// paid for wins, so the same board always gives the same omen.
    /// </summary>
    public static class EnemyAi
    {
        /// <summary>The action id an omen carries when nothing in the branch can be paid for.</summary>
        public const string RestActionId = "rest";

        public static readonly OmenLabel RestLabel = new OmenLabel(OmenKind.Rest);

        /// <summary>§6.1: an enemy without a position branches on the player side, two ways.</summary>
        public static IReadOnlyList<string> BranchFor(EnemyDef def, Position playerPosition)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (def.HasPosition)
            {
                // Elites and bosses branch four ways on both sides (§6.1). That tree is #50.
                throw new NotSupportedException(
                    $"Enemy \"{def.Id}\" carries a position; the four-branch tree is not in the slice.");
            }
            return playerPosition == Position.Near ? def.BranchWhenPlayerNear : def.BranchWhenPlayerFar;
        }

        /// <summary>
        /// roster §1.2: walk the branch from the top and take the first action whose cost fits in
        /// <paramref name="stamina"/>. Null when none does — the enemy rests.
        ///
        /// The caller decides which stamina to pass. The turn loop passes what the enemy will hold
        /// when the omen is carried out (after its recovery), so an omen only turns into a rest when
        /// something drained the enemy in between.
        /// </summary>
        public static EnemyActionDef? ChooseAction(EnemyDef def, Position playerPosition, int stamina)
        {
            foreach (var actionId in BranchFor(def, playerPosition))
            {
                var action = def.Actions[actionId];
                if (Combat.CanPay(action.Cost, stamina)) return action;
            }
            return null;
        }

        /// <summary>§6: the one-step omen for the next action — 種別 + 咎める側の一字, as the roster wrote it.</summary>
        public static Omen DecideOmen(EnemyDef def, Position playerPosition, int stamina)
        {
            var action = ChooseAction(def, playerPosition, stamina);
            return action == null
                ? new Omen(RestActionId, RestLabel)
                : new Omen(action.Id, action.Omen);
        }

        /// <summary>
        /// §6: the omen is a commitment. The declared action is carried out as shown, and when it can
        /// no longer be paid for the enemy rests instead of picking something cheaper.
        /// </summary>
        public static EnemyActionDef? ActionToExecute(EnemyDef def, Omen omen, int stamina)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (omen == null) throw new ArgumentNullException(nameof(omen));
            if (omen.ActionId == RestActionId) return null;

            var action = def.Actions[omen.ActionId];
            return Combat.CanPay(action.Cost, stamina) ? action : null;
        }

        /// <summary>§6 / §7.1: the omen as the screen words it — "攻撃・遠", "守り", "休み".</summary>
        public static string ToText(this OmenLabel label)
        {
            if (label == null) throw new ArgumentNullException(nameof(label));
            string kind = label.Kind switch
            {
                OmenKind.Attack => "攻撃",
                OmenKind.Guard => "守り",
                OmenKind.Move => "動",
                OmenKind.Skill => "技",
                OmenKind.Stance => "構え",
                OmenKind.Rest => "休み",
                _ => throw new ArgumentOutOfRangeException(nameof(label), label.Kind, null),
            };
            return label.Side.HasValue ? $"{kind}・{label.Side.Value.ToLabel()}" : kind;
        }
    }
}
