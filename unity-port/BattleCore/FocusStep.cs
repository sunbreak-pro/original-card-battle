using System;
using System.Linq;

namespace BattleCore
{
    /// <summary>
    /// §5 集中 (#188, #48): the next card resolves one column to the right, never beyond column 4.
    /// A card is written into one column and keeps no table of the others, so the column to the
    /// right is read off the <see cref="Columns"/> scales: the step to the next column is added to
    /// its power (the v4.4 attack rows §5 keeps, #364), its Guard and its heal (§3.1's rows), and
    /// to the stacks of each 出血 / 再生 its face gives (the stack scale 1 / 2 / 3 / 4). A face only grows on the attribute that owns it (a
    /// move card's small Guard does not), and column 4 does not grow.
    ///
    /// What is the same in every column stays: the other words (2 stacks whatever the column),
    /// 崩し (§3.1: not on the scale), the cost, the reach, and a trait's grant (the trait belongs
    /// to the card, not to its column). The skill face's "kinds" row is not read: which word a
    /// column would add is the card author's choice, not a number.
    /// </summary>
    public sealed record FocusStep(int Power, int Guard, int Heal, int TickStacks = 0)
    {
        public static readonly FocusStep None = new FocusStep(0, 0, 0);

        public static FocusStep Of(BattleAttribute attributes, int column, Face face)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            if (!Columns.IsValid(column)) return None;
            // A face beside a movement effect is one of two things the card does (§2.1: a 面 を 2 つ持つ札
            // reads the 65% scale); the movement is no longer a declared flag, so read it off the face.
            bool single = (attributes == BattleAttribute.Attack || attributes == BattleAttribute.Guard
                || attributes == BattleAttribute.Skill) && !AttributeRule.HasMovement(face);
            int power = attributes.HasFlag(BattleAttribute.Attack) && face.Power > 0
                ? Columns.StepRight(single ? Columns.SingleAttackPower : Columns.DualAttackPower, column) : 0;
            int guard = attributes.HasFlag(BattleAttribute.Guard) && face.Guard > 0
                ? Columns.StepRight(single ? Columns.SingleGuard : Columns.DualGuard, column) : 0;
            int heal = attributes.HasFlag(BattleAttribute.Skill) && face.Heal > 0
                ? Columns.StepRight(single ? Columns.SingleHeal : Columns.DualHeal, column) : 0;
            int ticks = face.StatusList.Any(grant => GrowsWithColumn(grant.Kind))
                ? Columns.StepRight(Columns.TickStacks, column) : 0;
            return new FocusStep(power, guard, heal, ticks);
        }

        /// <summary>The stacks <paramref name="grant"/> lands with: one column further on the 出血 / 再生 scale, any other word as written.</summary>
        public int StacksOf(StatusGrant grant)
        {
            if (grant == null) throw new ArgumentNullException(nameof(grant));
            return GrowsWithColumn(grant.Kind) ? grant.Stacks + TickStacks : grant.Stacks;
        }

        /// <summary>§3.1: 出血 and 再生 are the two words whose stacks the column decides.</summary>
        private static bool GrowsWithColumn(StatusKind kind) => kind == StatusKind.Bleed || kind == StatusKind.Regen;
    }
}
