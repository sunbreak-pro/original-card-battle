using System;

namespace DungeonContent
{
    /// <summary>
    /// What the battle-side items amount to as a battle starts (tools_and_consumables_v4.md §2,
    /// battle_core_v4 §9.1): the cells the start moves back, the stamina added at the start, the Guard
    /// before the first enemy blow, and the stacks the boss-only words sit lower.
    ///
    /// The one place an <see cref="ItemEffect"/> of <see cref="EffectSide.Battle"/> turns into these four
    /// numbers (#648). The exploration's hand-over (Exploration.Bridge.Expedition) and the departure
    /// screen (Depiction.View.DepartureSupplies) both read it, so a fifth battle effect cannot reach one
    /// and not the other. It lives here because both of them already reference DungeonContent: the
    /// departure screen in Unity's Assembly-CSharp and in the dotnet-only Depiction.Bridge.Tests alike.
    ///
    /// A struct of four ints, so two of them are equal when their numbers are (ValueType.Equals).
    /// </summary>
    public readonly struct BattleToolAmounts
    {
        public static readonly BattleToolAmounts None = new BattleToolAmounts(0, 0, 0, 0);

        public BattleToolAmounts(int startCellsBack, int startStamina, int openingGuard, int bossWordWard)
        {
            StartCellsBack = startCellsBack;
            StartStamina = startStamina;
            OpeningGuard = openingGuard;
            BossWordWard = bossWordWard;
        }

        /// <summary><see cref="ItemEffect.WiderStart"/>: cells the player's start moves back.</summary>
        public int StartCellsBack { get; }

        /// <summary><see cref="ItemEffect.OpeningStamina"/>: stamina added at the start of a battle.</summary>
        public int StartStamina { get; }

        /// <summary><see cref="ItemEffect.OpeningGuard"/>: the Guard before the first enemy blow.</summary>
        public int OpeningGuard { get; }

        /// <summary><see cref="ItemEffect.BluntBossState"/>: the stacks the boss-only words sit lower.</summary>
        public int BossWordWard { get; }

        /// <summary>
        /// One effect at <paramref name="amount"/>. An exploration-side effect does not reach the battle
        /// and reads <see cref="None"/>. A battle-side effect with no mapping here throws, so a new one
        /// cannot go missing quietly.
        /// </summary>
        public static BattleToolAmounts Of(ItemEffect effect, int amount)
        {
            switch (effect)
            {
                case ItemEffect.WiderStart: return new BattleToolAmounts(amount, 0, 0, 0);
                case ItemEffect.OpeningStamina: return new BattleToolAmounts(0, amount, 0, 0);
                case ItemEffect.OpeningGuard: return new BattleToolAmounts(0, 0, amount, 0);
                case ItemEffect.BluntBossState: return new BattleToolAmounts(0, 0, 0, amount);
            }
            if (effect.Side() == EffectSide.Exploration) return None;
            throw new ArgumentOutOfRangeException(nameof(effect), effect, "a battle-side effect with no mapping to the battle's start");
        }

        /// <summary>One item, by its <see cref="ItemDef.Amount"/>.</summary>
        public static BattleToolAmounts Of(ItemDef item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            return Of(item.Effect, item.Amount);
        }

        /// <summary>Two sets of amounts added together (the same tool may fill two slots).</summary>
        public BattleToolAmounts Plus(BattleToolAmounts other) => new BattleToolAmounts(
            StartCellsBack + other.StartCellsBack,
            StartStamina + other.StartStamina,
            OpeningGuard + other.OpeningGuard,
            BossWordWard + other.BossWordWard);

        public override string ToString() => $"(back {StartCellsBack}, stamina {StartStamina}, guard {OpeningGuard}, ward {BossWordWard})";
    }
}
