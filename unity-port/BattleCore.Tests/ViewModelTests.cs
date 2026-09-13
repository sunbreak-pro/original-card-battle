using System.Collections.Generic;
using NUnit.Framework;
using System.Linq;
using BattleCore;

namespace BattleCore.Tests
{
    public class ViewModelTests
    {
        private static PrototypeCard Card(
            CardDefId defId, string name, CardType type, int cost,
            RangeBand? effRange, int basePower, int shift, int guard,
            string instanceId)
        {
            return new PrototypeCard(instanceId, defId, name, type, cost, effRange, basePower, shift, guard, "");
        }

        private static PrototypeCard Thrust(string instanceId = "thrust-x") =>
            Card(CardDefId.Thrust, "突き", CardType.Attack, 4, RangeBand.Close, 9, 0, 0, instanceId);

        // ---- describeCard damage ----

        [Test]
        public void DescribeCard_OptimalDamage()
        {
            var v = ViewModel.DescribeCard(Thrust(), 20, 0, false);
            Assert.That(v.Damage, Is.Not.Null);
            Assert.That(v.Damage!.Predicted, Is.EqualTo(9));
            Assert.That(v.Damage.OffRange, Is.False);
            Assert.That(v.Damage.Fatigued, Is.False);
            Assert.That(v.Damage.Note, Is.EqualTo("最適"));
            Assert.That(v.Damage.Text, Is.EqualTo("威力 9 → 9（最適）"));
            Assert.That(v.EffectiveRangeLabel, Is.EqualTo("近"));
        }

        [Test]
        public void DescribeCard_OffRangeDamage()
        {
            var v = ViewModel.DescribeCard(Thrust(), 20, 1, false);
            Assert.That(v.Damage!.Predicted, Is.EqualTo(5));
            Assert.That(v.Damage.OffRange, Is.True);
            Assert.That(v.Damage.Note, Is.EqualTo("間合い不適"));
        }

        [Test]
        public void DescribeCard_FatigueDamage()
        {
            var feint = Card(CardDefId.Feint, "牽制", CardType.Attack, 3, RangeBand.Mid, 8, 0, 0, "feint-x");
            var v = ViewModel.DescribeCard(feint, 4, 1, false);
            Assert.That(v.Damage!.Predicted, Is.EqualTo(4));
            Assert.That(v.Damage.Fatigued, Is.True);
            Assert.That(v.Damage.Note, Is.EqualTo("疲労"));
        }

        [Test]
        public void DescribeCard_OffRangeAndFatigueDamage()
        {
            var v = ViewModel.DescribeCard(Thrust(), 4, 1, false);
            Assert.That(v.Damage!.Predicted, Is.EqualTo(2));
            Assert.That(v.Damage.Note, Is.EqualTo("間合い不適・疲労"));
        }

        // ---- playability ----

        [Test]
        public void DescribeCard_AffordableAndOngoingIsPlayable()
        {
            var v = ViewModel.DescribeCard(Thrust(), 20, 0, false);
            Assert.That(v.Playable, Is.True);
            Assert.That(v.DisabledReason, Is.EqualTo(""));
        }

        [Test]
        public void DescribeCard_InsufficientStaminaNotPlayable()
        {
            var card = Card(CardDefId.Lunge, "踏み込み斬り", CardType.Attack, 5, RangeBand.Close, 7, -1, 0, "lunge-x");
            var v = ViewModel.DescribeCard(card, 4, 0, false);
            Assert.That(v.Playable, Is.False);
            Assert.That(v.DisabledReason, Is.EqualTo("気力不足（必要 5）"));
        }

        [Test]
        public void DescribeCard_BattleOverNotPlayableWithNoReason()
        {
            var v = ViewModel.DescribeCard(Thrust(), 20, 0, true);
            Assert.That(v.Playable, Is.False);
            Assert.That(v.DisabledReason, Is.EqualTo(""));
        }

        // ---- move / guard ----

        [Test]
        public void DescribeCard_MoveStepIn()
        {
            var stepIn = Card(CardDefId.StepIn, "足捌き・前", CardType.Move, 2, null, 0, -1, 0, "step_in-x");
            var v = ViewModel.DescribeCard(stepIn, 20, 1, false);
            Assert.That(v.Damage, Is.Null);
            Assert.That(v.EffectiveRangeLabel, Is.Null);
            Assert.That(v.ShiftLabel, Is.EqualTo("間合い −1（詰める）"));
        }

        [Test]
        public void DescribeCard_MoveStepOut()
        {
            var stepOut = Card(CardDefId.StepOut, "足捌き・後", CardType.Move, 1, null, 0, 1, 0, "step_out-x");
            var v = ViewModel.DescribeCard(stepOut, 20, 1, false);
            Assert.That(v.ShiftLabel, Is.EqualTo("間合い +1（退く）"));
        }

        [Test]
        public void DescribeCard_Guard()
        {
            var brace = Card(CardDefId.Brace, "呼吸を整える", CardType.Guard, 2, null, 0, 0, 6, "brace-x");
            var v = ViewModel.DescribeCard(brace, 20, 1, false);
            Assert.That(v.Damage, Is.Null);
            Assert.That(v.Guard, Is.EqualTo(6));
            Assert.That(v.ShiftLabel, Is.Null);
        }

        // ---- describeHand ----

        [Test]
        public void DescribeHand_OngoingAllPlayable()
        {
            var state = new BattleState(
                1, 0, 30, 20, 0, 38, 20,
                new List<PrototypeCard> { Thrust("thrust-0"), Thrust("thrust-1") },
                new List<PrototypeCard>(), new List<PrototypeCard>(),
                new List<LogEntry>(), 0, GameResult.Ongoing);
            var views = ViewModel.DescribeHand(state);
            Assert.That(views.Count, Is.EqualTo(2));
            Assert.That(views.All(v => v.Playable), Is.True);
        }

        [Test]
        public void DescribeHand_WonNonePlayable()
        {
            var state = new BattleState(
                1, 0, 30, 20, 0, 0, 20,
                new List<PrototypeCard> { Thrust("thrust-0"), Thrust("thrust-1") },
                new List<PrototypeCard>(), new List<PrototypeCard>(),
                new List<LogEntry>(), 0, GameResult.Won);
            var views = ViewModel.DescribeHand(state);
            Assert.That(views.All(v => !v.Playable), Is.True);
        }

        // ---- distanceLabel / isBattleOver / enemyRangeHint ----

        [Test]
        public void DistanceLabel_MapsIndexToLabel()
        {
            Assert.That(ViewModel.DistanceLabel(0), Is.EqualTo("近"));
            Assert.That(ViewModel.DistanceLabel(1), Is.EqualTo("中"));
            Assert.That(ViewModel.DistanceLabel(2), Is.EqualTo("遠"));
        }

        [Test]
        public void IsBattleOver_TrueOnlyWhenNotOngoing()
        {
            Assert.That(ViewModel.IsBattleOver(GameResult.Ongoing), Is.False);
            Assert.That(ViewModel.IsBattleOver(GameResult.Won), Is.True);
            Assert.That(ViewModel.IsBattleOver(GameResult.Lost), Is.True);
        }

        [Test]
        public void EnemyRangeHint_ListsMidAndFarThreats()
        {
            var hint = ViewModel.EnemyRangeHint();
            Assert.That(hint, Does.Contain("中（薙ぎ払い）"));
            Assert.That(hint, Does.Contain("遠（穂先の突き）"));
            Assert.That(hint, Does.Not.Contain("石突き"));
            Assert.That(hint, Does.Contain("近に詰めると弱い押し戻ししかできない"));
        }
    }
}
