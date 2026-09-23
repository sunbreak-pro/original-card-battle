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

        /// <summary>§6.1: every enemy branches three ways on the gap band at the moment the omen is decided.</summary>
        public static IReadOnlyList<string> BranchFor(EnemyDef def, int gap)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            return def.Branch(gap.ToBand());
        }

        /// <summary>
        /// roster §1.2: walk the branch from the top and take the first action whose cost fits in
        /// <paramref name="stamina"/>. Null when none does — the enemy rests.
        ///
        /// The caller decides which stamina to pass. The turn loop passes what the enemy will hold
        /// when the omen is carried out (after its recovery), so an omen only turns into a rest when
        /// something drained the enemy in between.
        /// </summary>
        public static EnemyActionDef? ChooseAction(EnemyDef def, int gap, int stamina)
        {
            foreach (var actionId in BranchFor(def, gap))
            {
                var action = def.Actions[actionId];
                if (Combat.CanPay(action.Cost, stamina)) return action;
            }
            return null;
        }

        /// <summary>§6: the one-step omen for the next action — 種別 + 狙うマス.</summary>
        public static Omen DecideOmen(EnemyDef def, int gap, int stamina)
        {
            var action = ChooseAction(def, gap, stamina);
            return action == null
                ? new Omen(RestActionId, RestLabel)
                : new Omen(action.Id, LabelOf(action));
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

        /// <summary>
        /// §2.4 / §6: whether a face is aimed at the opponent, which is what a reach applies to — an
        /// attack, a status put on the opponent, or a push / pull. A Self-targeted action reads no
        /// reach even when it carries those rows.
        /// </summary>
        public static bool IsOpponentDirected(BattleAttribute attributes, Face face, TargetKind targets)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            if (targets != TargetKind.One) return false;
            return attributes.HasFlag(BattleAttribute.Attack) || face.Status.HasValue || face.Push != 0;
        }

        /// <summary>
        /// §6: the omen label, read off the action. The kind is the first attribute in the order
        /// Attack, Move, Guard, Skill, Stance — a move is put before a guard here (unlike the card's
        /// colour role, CoreText.KindOf) because where the enemy will stand is what the player has to
        /// read. The reach is the action's own when it has an opponent-directed face, else null.
        /// </summary>
        public static OmenLabel LabelOf(EnemyActionDef action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            OmenKind kind =
                action.Attributes.HasFlag(BattleAttribute.Attack) ? OmenKind.Attack
                : action.Attributes.HasFlag(BattleAttribute.Move) ? OmenKind.Move
                : action.Attributes.HasFlag(BattleAttribute.Guard) ? OmenKind.Guard
                : action.Attributes.HasFlag(BattleAttribute.Skill) ? OmenKind.Skill
                : OmenKind.Stance;
            Reach? reach = IsOpponentDirected(action.Attributes, action.Face, action.Targets)
                ? action.Face.ReachOrDefault
                : null;
            return new OmenLabel(kind, reach);
        }

        /// <summary>
        /// §6: the cells an omen aims at, counted from the enemy's near edge right now (the player
        /// side of the line). Empty for an action that aims at nobody, or when nothing it reaches is
        /// on the line. Lowest cell first.
        /// </summary>
        public static IReadOnlyList<int> TargetCells(OmenLabel label, CombatantState enemy)
        {
            if (label == null) throw new ArgumentNullException(nameof(label));
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            var cells = new List<int>();
            if (label.Reach == null) return cells;
            for (int gap = label.Reach.Max; gap >= label.Reach.Min; gap--)
            {
                int cell = enemy.Cell - 1 - gap;
                if (cell >= 1) cells.Add(cell);
            }
            return cells;
        }

        /// <summary>§6: the omen as the screen words it — "攻撃・1〜2", "守り", "休み".</summary>
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
            return label.Reach != null ? $"{kind}・{label.Reach.ToText()}" : kind;
        }
    }
}
