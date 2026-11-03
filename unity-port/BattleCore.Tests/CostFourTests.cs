using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// #823: the cost cap went from 3 to 4, so a card written into column 4 costs 4. 集中 and 伸び
    /// still climb a cost-3 card into column 4 without raising what it costs.
    ///
    /// Every scenario runs through the real TurnLoop on an 8-cell line, the deck dealt in order
    /// (FixedRng(0.999…) makes the shuffle a no-op), against an enemy that never acts on the player.
    /// </summary>
    public class CostFourTests
    {
        private const int Cells = 8;

        private static readonly IRng NoRng = new FixedRng(0.9999999);

        private static readonly EnemyActionDef Wait =
            Fixtures.EnemyAction("wait", face: new Face(), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        private static readonly EnemyDef Idle = Fixtures.Enemy("idle", atZero: Wait, atOneToTwo: Wait, atThreePlus: Wait);

        private static readonly CardDef Filler =
            Fixtures.Card("filler", 1, new Face(Guard: 1), BattleAttribute.Guard, targets: TargetKind.Self);

        /// <summary>A lone attack face authored into column 4 (§3.1's single attack at column 4 is 33).</summary>
        private static readonly CardDef Awakening = Fixtures.Card("awakening", 4, new Face(Power: 33));

        /// <summary>A lone attack face in column 3 that 集中 lifts to column 4.</summary>
        private static readonly CardDef Heavy = Fixtures.Card("heavy", 3, new Face(Power: 24));

        private static List<CardInstance> Deck(params CardDef[] cards)
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < cards.Length; i++) deck.Add(new CardInstance(cards[i].Id + "-" + i, cards[i]));
            for (int i = deck.Count; i < Constants.HandDraw; i++) deck.Add(new CardInstance(Filler.Id + "-" + i, Filler));
            return deck;
        }

        /// <summary>The player's first turn, opened, with the enemy at gap 1 and the stamina set.</summary>
        private static BattleState Opened(int stamina, params CardDef[] cards)
        {
            var started = TurnLoop.Start(new BattleSetup(Idle, Deck(cards), Cells, StartGap: 1), NoRng).State;
            var s = TurnLoop.BeginPlayerTurn(started, NoRng).State;
            return s with { Player = s.Player with { Stamina = stamina } };
        }

        private static string InHand(BattleState s, string cardId) => s.Hand.First(c => c.Def.Id == cardId).InstanceId;

        [Test]
        public void ACardInColumnFour_CostsFour()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Awakening.Cost, Is.EqualTo(4));
                Assert.That(Fixtures.EnemyAction("big_swing", column: 4, face: new Face(Power: 20)).Cost, Is.EqualTo(4));
            });
        }

        [Test]
        public void ACostFourCard_IsPlayed_WithFourStamina_AndSpendsFour()
        {
            var s = Opened(4, Awakening);

            Assert.That(TurnLoop.CanPlay(s, InHand(s, "awakening")), Is.EqualTo(PlayRefusal.None));

            var play = TurnLoop.PlayCard(s, InHand(s, "awakening"), NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<StaminaSpent>().Single().Amount, Is.EqualTo(4));
                Assert.That(play.State.Player.Stamina, Is.EqualTo(0));
                Assert.That(play.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(33));
            });
        }

        [Test]
        public void ACostFourCard_IsRefused_WithThreeStamina()
        {
            var s = Opened(3, Awakening);

            Assert.Multiple(() =>
            {
                Assert.That(TurnLoop.CanPlay(s, InHand(s, "awakening")), Is.EqualTo(PlayRefusal.NotEnoughStamina));
                Assert.That(Combat.CanPay(Awakening.Cost, stamina: 3), Is.False);
            });
        }

        [Test]
        public void Focus_LiftsACostThreeCardIntoColumnFour_ButItStillCostsThree()
        {
            // §5 集中: the column-3 face resolves with column 4's values (24 + the +9 step = 33), and
            // the card's own column, and so its cost, does not move.
            var s = Opened(3, Heavy);
            s = s with { Player = s.Player with { Statuses = StatusSet.Of((StatusKind.Focus, 1)) } };

            Assert.That(TurnLoop.CanPlay(s, InHand(s, "heavy")), Is.EqualTo(PlayRefusal.None), "3 stamina still pays a focused cost-3 card");

            var play = TurnLoop.PlayCard(s, InHand(s, "heavy"), NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(33));
                Assert.That(play.Events.OfType<StaminaSpent>().Single().Amount, Is.EqualTo(3));
                Assert.That(play.State.Player.Stamina, Is.EqualTo(0));
                Assert.That(Heavy.Cost, Is.EqualTo(3));
            });
        }

        [Test]
        public void Extend_LiftsACostThreeCardIntoColumnFour_AndPinsCostThree()
        {
            // §3.2 伸び: the column moves to 4, the cost is pinned to the 3 it had. Without that pin the
            // column would now read cost 4, which is why FixedCost carries it.
            var extended = Mastery.Master(Heavy, 1, new MasteryProfile(Heavy.Id, MasteryKind.Extend));

            Assert.Multiple(() =>
            {
                Assert.That(extended.Column, Is.EqualTo(4));
                Assert.That(extended.Cost, Is.EqualTo(3));
                Assert.That((extended with { FixedCost = null }).Cost, Is.EqualTo(4));
            });
        }

        [Test]
        public void Extend_OnAColumnFourCard_NeitherClimbsNorChangesTheCost()
        {
            // The issue keeps column 4 as the last rung: there is no column 5 for a cost-4 card.
            var extended = Mastery.Master(Awakening, 3, new MasteryProfile(Awakening.Id, MasteryKind.Extend));

            Assert.Multiple(() =>
            {
                Assert.That(extended.Column, Is.EqualTo(4));
                Assert.That(extended.Face.Power, Is.EqualTo(33));
                Assert.That(extended.Cost, Is.EqualTo(4));
                Assert.That(FocusStep.Of(Awakening.Attributes, Awakening.Column, Awakening.Face), Is.EqualTo(FocusStep.None));
            });
        }
    }
}
