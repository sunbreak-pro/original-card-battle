using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    public class ViewModelTests
    {
        private static BattleState State(int distance = 1, int stamina = 7, int enemyGuard = 0, int disclosure = 1, CardDefId[]? hand = null)
        {
            var s = BattleReducer.InitState(new FixedRng(0), new BattleInit(PlayerMaxStamina: 9, MiasmaPercent: 32, MiasmaDensity: 2, Floor: 2, Disclosure: disclosure));
            return s with
            {
                DistanceIndex = distance,
                PlayerStamina = stamina,
                EnemyGuard = enemyGuard,
                Omen = Enemy.ChooseOmen(distance, 10),
                Hand = (hand ?? new CardDefId[0]).Select((id, i) => new CardInstance($"{id.ToToken()}-{i}", Cards.Def(id))).ToList(),
            };
        }

        [Test]
        public void DescribeCard_TiersStartAtMinInvestAndDefaultKeepsReserve()
        {
            var s = State(distance: 1, stamina: 7, hand: new[] { CardDefId.Thrust });
            var card = ViewModel.DescribeCard(s.Hand[0], s);
            Assert.That(card.Tiers.Select(t => t.Invest), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(card.DefaultInvest, Is.EqualTo(3), "7 − 3 = 4 ≥ 3");
            Assert.That(card.Tiers.Single(t => t.IsDefault).Invest, Is.EqualTo(3));
            Assert.That(card.Tiers[2].PredictedDamage, Is.EqualTo(6), "11 × 0.5 = 5.5 → 6 at mid");
            Assert.That(card.Tiers[2].Summary, Does.Contain("×0.5"));
            Assert.That(card.Playable, Is.True);
        }

        [Test]
        public void DescribeCard_UnaffordableTiersAndUnplayable()
        {
            var s = State(stamina: 0, hand: new[] { CardDefId.Thrust, CardDefId.Feint });
            var thrust = ViewModel.DescribeCard(s.Hand[0], s);
            Assert.That(thrust.Playable, Is.False);
            Assert.That(thrust.DisabledReason, Does.Contain("スタミナ不足"));
            Assert.That(thrust.Tiers.All(t => !t.Affordable), Is.True);

            var feint = ViewModel.DescribeCard(s.Hand[1], s);
            Assert.That(feint.Playable, Is.True);
            Assert.That(feint.DefaultInvest, Is.EqualTo(0));
            Assert.That(feint.Tiers[0].Affordable, Is.True);
            Assert.That(feint.Tiers[1].Affordable, Is.False);
        }

        [Test]
        public void DescribeTier_PredictsGuardMoveAndBreak()
        {
            var s = State(distance: 0, stamina: 9, enemyGuard: 2, hand: new[] { CardDefId.Lunge, CardDefId.StepOut });
            var lunge = ViewModel.DescribeCard(s.Hand[0], s).Tiers.Single(t => t.Invest == 3);
            Assert.That(lunge.PredictedDamage, Is.EqualTo(5), "7 raw − enemy guard 2");
            Assert.That(lunge.BreakStamina, Is.EqualTo(1));
            Assert.That(lunge.Clamped, Is.True);
            Assert.That(lunge.Summary, Does.Contain("崩し 1"));

            var stepOut = ViewModel.DescribeCard(s.Hand[1], s).Tiers.Single(t => t.Invest == 2);
            Assert.That(stepOut.GuardApplies, Is.True);
            Assert.That(stepOut.DistanceAfterLabel, Is.EqualTo("中"));
            Assert.That(stepOut.StaminaAfter, Is.EqualTo(7));
            Assert.That(stepOut.KeepsReserve, Is.True);
        }

        [Test]
        public void DescribeOmen_DisclosureChangesText()
        {
            var omen = new Omen(EnemyActionId.Sweep, RangeBand.Mid);
            var low = ViewModel.DescribeOmen(omen, 1, 0);
            Assert.That(low.BannerText, Is.EqualTo("予兆: 攻撃"));
            Assert.That(low.Mult, Is.Null);

            var mid = ViewModel.DescribeOmen(omen, 1, 1);
            Assert.That(mid.BannerText, Is.EqualTo("予兆: 攻撃 ／ 狙い 中"));
            Assert.That(mid.Diff, Is.EqualTo(0));
            Assert.That(mid.BandLabel, Is.EqualTo("的中"));

            var high = ViewModel.DescribeOmen(omen, 2, 2);
            Assert.That(high.BannerText, Is.EqualTo("予兆: 薙ぎ払い ／ 狙い 中 ／ 威力 4〜8"));
            Assert.That(high.Mult, Is.EqualTo(0.5));
            Assert.That(high.FloorText, Is.EqualTo("このままだと 薙ぎ払い ×0.5"));
        }

        [Test]
        public void OmenMultiplierAt_SupportsGhostPreview()
        {
            var omen = new Omen(EnemyActionId.Sweep, RangeBand.Mid);
            Assert.That(ViewModel.OmenMultiplierAt(omen, 1), Is.EqualTo(1.0));
            Assert.That(ViewModel.OmenMultiplierAt(omen, 2), Is.EqualTo(0.5));
            Assert.That(ViewModel.OmenMultiplierAt(new Omen(EnemyActionId.GuardUp, null), 1), Is.Null);
        }

        [Test]
        public void ReservePreview_Text()
        {
            Assert.That(ViewModel.ReservePreview(7), Is.EqualTo("今終えると 残 7 → 構え Guard +2"));
            Assert.That(ViewModel.ReservePreview(2), Does.StartWith("今終えると 残 2 → 構えなし"));
        }

        [Test]
        public void BattleViewModel_FlattensHudValues()
        {
            var s = State(distance: 1, stamina: 7, disclosure: 2, hand: new[] { CardDefId.Thrust });
            var vm = BattleViewModel.From(s);
            Assert.That(vm.Floor, Is.EqualTo(2));
            Assert.That(vm.MiasmaPercent, Is.EqualTo(32));
            Assert.That(vm.MiasmaPenalty, Is.EqualTo(1));
            Assert.That(vm.PlayerMaxStamina, Is.EqualTo(9));
            Assert.That(vm.EnemyName, Is.EqualTo("長柄の歪み兵"));
            Assert.That(vm.Omen, Is.Not.Null);
            Assert.That(vm.ReserveWillTrigger, Is.True);
            Assert.That(vm.Journal.TendencyLines.Count, Is.EqualTo(3));
            Assert.That(vm.Journal.TendencyLines[1], Does.StartWith("中 → 薙ぎ払い"));
            Assert.That(vm.DrawPileCount + vm.Hand.Count + vm.DiscardPileCount, Is.EqualTo(10), "hand replaced by 1 card; 9 in draw pile");
        }

        [Test]
        public void Journal_LowDisclosureHidesTendencies()
        {
            var j = ViewModel.DescribeJournal(0);
            Assert.That(j.TendencyLines.Single(), Does.Contain("？"));
            Assert.That(j.WeaknessLine, Does.Contain("？"));
        }
    }
}
