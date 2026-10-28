using DungeonCore;

namespace DungeonCore.Tests
{
    /// <summary>
    /// #651: the consumables beyond 浄化の香 (tools_and_consumables_v4.md §3) — 薬草, 濃い干し肉 and
    /// 忘れ水 act on the run, 標の石 rides in a slot but waits for a screen that picks where to jump —
    /// and picking one up: into a free slot, or swapped for a named one when every slot is full.
    /// </summary>
    public class ConsumableTests
    {
        // battle_core_v4.md §10: PLAYER_MAX_HP 50.
        private const int PlayerMaxHp = 50;

        private static readonly ReliefItem Herb = ReliefItem.HealHp("yakuso", 20);
        private static readonly ReliefItem Jerky = ReliefItem.MaxStamina("koi_hoshiniku", 1);
        private static readonly ReliefItem Water = ReliefItem.RefillStamina("wasuremizu");
        private static readonly ReliefItem Stone = ReliefItem.Teleport("shirube_no_ishi");
        private static readonly ReliefItem Incense = ReliefItem.Gauge("joka_no_ko", 10);

        // ---- using them ----

        [Test]
        public void AHerb_GivesBackAFifthOfTheHpMaximum()
        {
            var state = Enter(new[] { Herb }, hp: 20);
            var used = ExplorationReducer.UseConsumable(state, 0);
            Assert.That(used.Hp, Is.EqualTo(30), "20% of 50 is 10");
            Assert.That(used.Loadout.Consumables, Is.Empty);
            Assert.That(used.TimeLeft, Is.EqualTo(state.TimeLeft), "a consumable costs no 刻限");
        }

        [Test]
        public void AHerb_NeverHealsPastTheMaximum()
        {
            var used = ExplorationReducer.UseConsumable(Enter(new[] { Herb }, hp: 45), 0);
            Assert.That(used.Hp, Is.EqualTo(PlayerMaxHp));
        }

        [Test]
        public void Jerky_RaisesMaxStaminaByOne()
        {
            var state = Enter(new[] { Jerky });
            var used = ExplorationReducer.UseConsumable(state, 0);
            Assert.That(used.TempMaxStaminaMod, Is.EqualTo(state.TempMaxStaminaMod + 1));
            Assert.That(used.MaxStamina, Is.EqualTo(state.MaxStamina + 1));
            Assert.That(used.Stamina, Is.EqualTo(state.Stamina), "the maximum goes up, the stamina in hand does not");
        }

        [Test]
        public void Jerky_StaysInsideThePlusMinusFourClamp()
        {
            var state = Enter(new[] { Jerky }, tempMaxStaminaMod: Miasma.TempModClamp);
            var used = ExplorationReducer.UseConsumable(state, 0);
            Assert.That(used.TempMaxStaminaMod, Is.EqualTo(Miasma.TempModClamp));
            Assert.That(used.MaxStamina, Is.EqualTo(state.MaxStamina));
        }

        [Test]
        public void ForgottenWater_FillsStaminaToTheMaximum()
        {
            var state = Enter(new[] { Water }, stamina: 1);
            Assert.That(state.Stamina, Is.LessThan(state.MaxStamina), "the entry node's 刻限 gives back 3, not all");
            var used = ExplorationReducer.UseConsumable(state, 0);
            Assert.That(used.Stamina, Is.EqualTo(state.MaxStamina));
        }

        [Test]
        public void AStone_CannotBeUsedYet_AndStaysInItsSlot()
        {
            var state = Enter(new[] { Stone });
            Assert.That(ExplorationReducer.CanUseConsumable(state, 0), Is.False);
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.UseConsumable(state, 0));
            Assert.That(state.Loadout.Consumables, Is.EqualTo(new[] { Stone }));
        }

        [Test]
        public void CanUseConsumable_IsFalse_ForAnEmptySlot()
        {
            var state = Enter(new[] { Herb });
            Assert.That(ExplorationReducer.CanUseConsumable(state, 0), Is.True);
            Assert.That(ExplorationReducer.CanUseConsumable(state, 1), Is.False);
            Assert.That(ExplorationReducer.CanUseConsumable(state, -1), Is.False);
        }

        // ---- picking them up ----

        [Test]
        public void AConsumablePickedUp_GoesIntoAFreeSlot()
        {
            var state = Enter(new[] { Incense });
            var taken = ExplorationReducer.TakeConsumable(state, Herb);
            Assert.That(taken.Loadout.Consumables, Is.EqualTo(new[] { Incense, Herb }));
            Assert.That(taken.TimeLeft, Is.EqualTo(state.TimeLeft));
        }

        [Test]
        public void AFreeSlot_TakesTheItem_WhateverSlotIsNamed()
        {
            var taken = ExplorationReducer.TakeConsumable(Enter(new[] { Incense }), Herb, replaceSlot: 0);
            Assert.That(taken.Loadout.Consumables, Is.EqualTo(new[] { Incense, Herb }), "nothing is swapped while a slot is free");
        }

        [Test]
        public void FullSlots_SwapOnlyTheSlotNamed()
        {
            var state = Enter(new[] { Incense, Incense, Incense });
            Assert.That(state.Loadout.ConsumableSlotsFull, Is.True);
            var swapped = ExplorationReducer.TakeConsumable(state, Herb, replaceSlot: 1);
            Assert.That(swapped.Loadout.Consumables, Is.EqualTo(new[] { Incense, Herb, Incense }));
        }

        [Test]
        public void FullSlots_RefuseASwapThatNamesNoSlot()
        {
            var state = Enter(new[] { Incense, Incense, Incense });
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.TakeConsumable(state, Herb));
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.TakeConsumable(state, Herb, replaceSlot: 3));
        }

        [Test]
        public void NothingIsPickedUp_OnceTheLifeIsOver()
        {
            var over = ExplorationReducer.EndLife(Enter(new[] { Incense }));
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.TakeConsumable(over, Herb));
        }

        private static ExplorationState Enter(ReliefItem[] consumables, int hp = PlayerMaxHp, int stamina = 10, int tempMaxStaminaMod = 0)
        {
            var profile = SevenLayers.Of(1);
            return ExplorationReducer.Enter(
                profile, profile.Map(11UL), new RunLoadout(null, consumables), hp, PlayerMaxHp, stamina, tempMaxStaminaMod: tempMaxStaminaMod);
        }
    }
}
