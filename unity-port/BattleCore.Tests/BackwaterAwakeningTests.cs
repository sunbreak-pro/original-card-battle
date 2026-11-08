using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// #826: the two stances v4.10 added. 背水の陣 multiplies the holder's attack blows by 3 in a slot
    /// of its own (on top of 脆化 / 強化) and the attack blows it takes by 2 before Guard; a second one
    /// adds nothing. 覚醒 adds +5 to each attack blow, +5 Guard and +1 cell of the holder's own move to
    /// every later card; a second one adds again, but the move stops at 3 cells. Every number is held
    /// against what the hand preview shows, so the screen's number is the one dealt.
    /// </summary>
    public class BackwaterAwakeningTests
    {
        private const int Cells = 8;

        private static readonly IRng NoRng = new FixedRng(0.9999999);

        private static readonly EnemyActionDef Wait =
            Fixtures.EnemyAction("wait", face: new Face(), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        private static readonly EnemyDef Idle = Fixtures.Enemy("idle", atZero: Wait, atOneToTwo: Wait, atThreePlus: Wait);

        private static readonly CardDef Filler =
            Fixtures.Card("filler", 1, new Face(Guard: 1), BattleAttribute.Guard, targets: TargetKind.Self);

        private static readonly CardDef Ten = Fixtures.Card("ten", 1, new Face(Power: 10));

        private static readonly CardDef CutAndGuard =
            Fixtures.Card("cut_and_guard", 2, new Face(Power: 10, Guard: 6), BattleAttribute.Attack | BattleAttribute.Guard);

        private static readonly CardDef TwoFives = Fixtures.Card("two_fives", 1, new Face(Power: 5, Hits: 2));

        private static readonly CardDef StepTwo = Fixtures.Card("step_two", 1, new Face(Move: 2), BattleAttribute.None, targets: TargetKind.Self);

        private static readonly CardDef StepOne = Fixtures.Card("step_one", 1, new Face(Move: 1), BattleAttribute.None, targets: TargetKind.Self);

        private static StanceDef BackwaterStance => CardCatalog.Backwater.Face.Stance!;

        private static StanceDef AwakeningStance => CardCatalog.Awakening.Face.Stance!;

        // ---- Helpers ----

        private static BattleState Opened(EnemyDef enemy, int gap, params CardDef[] cards)
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < cards.Length; i++) deck.Add(new CardInstance(cards[i].Id + "-" + i, cards[i]));
            for (int i = deck.Count; i < Constants.HandDraw; i++) deck.Add(new CardInstance(Filler.Id + "-" + i, Filler));
            var start = TurnLoop.Start(new BattleSetup(enemy, deck, Cells, StartGap: gap), NoRng).State;
            var s = TurnLoop.BeginPlayerTurn(start, NoRng).State;
            return s with { Player = s.Player with { Stamina = 10 } };
        }

        private static BattleState Holding(BattleState s, params StanceDef[] stances)
        {
            var player = s.Player;
            for (int i = 0; i < stances.Length; i++) player = player.WithStance(stances[i], "stance-" + i);
            return s with { Player = player };
        }

        private static string InHand(BattleState s, string cardId) => s.Hand.First(c => c.Def.Id == cardId).InstanceId;

        /// <summary>Plays the card and holds its preview against what it dealt: the raw and the damage on enemy 0.</summary>
        private static (int Raw, int Damage, int Guard) PlayChecked(BattleState s, string cardId)
        {
            var preview = TurnLoop.Preview(s, InHand(s, cardId))!;
            var events = TurnLoop.PlayCard(s, InHand(s, cardId), NoRng).Events;
            int raw = events.OfType<DamageDealt>().Where(d => d.Actor == Actor.Player).Sum(d => d.Raw);
            int damage = events.OfType<DamageDealt>().Where(d => d.Actor == Actor.Player).Sum(d => d.Damage);
            int guard = events.OfType<GuardGained>().Where(g => g.Actor == Actor.Player).Sum(g => g.Amount);
            Assert.That((preview.RawPower, preview.Damage, preview.GuardGain), Is.EqualTo((raw, damage, guard)), "the preview is what the card does");
            return (raw, damage, guard);
        }

        // ---- The two cards ----

        [Test]
        public void TheTwoCards_AreStancesOfColumnThreeAndFour_WithTheCanonNumbers()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.Backwater.Name, Is.EqualTo("背水の陣"));
                Assert.That(CardCatalog.Backwater.Cost, Is.EqualTo(3));
                Assert.That(BackwaterStance, Is.EqualTo(new StanceDef(StanceHook.Multiplier, PowerTimes: 3, TakenTimes: 2)));
                Assert.That(CardCatalog.Awakening.Name, Is.EqualTo("覚醒"));
                Assert.That(CardCatalog.Awakening.Cost, Is.EqualTo(4));
                Assert.That(AwakeningStance, Is.EqualTo(new StanceDef(StanceHook.CardBoost, Guard: 5, Power: 5, Move: 1)));
                Assert.That(Cards.StanceStandsAlone(CardCatalog.Backwater), Is.True);
                Assert.That(Cards.StanceStandsAlone(CardCatalog.Awakening), Is.True);
            });
        }

        // ---- 背水の陣 ----

        [Test]
        public void TheFormula_MultipliesTheStanceSlotOnTopOfTheBlowSlot_AndRoundsOnce()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Combat.ComputeRawPower(10, stanceTimes: 3), Is.EqualTo(30));
                Assert.That(Combat.ComputeRawPower(10, blowMult: Constants.EmpowerMult, stanceTimes: 3), Is.EqualTo(45));
                Assert.That(Combat.ComputeRawPower(5, blowMult: Constants.FragileMult, stanceTimes: 3), Is.EqualTo(23), "22.5 rounds once, away from zero");
                Assert.That(Combat.TakenRaw(8, 2), Is.EqualTo(16));
                Assert.That(Combat.TakenRaw(8, 1), Is.EqualTo(8));
            });
        }

        [Test]
        public void PlayingBackwater_PutsItOnTheList_AndTheNextAttackIsTripled()
        {
            var s = Opened(Idle, 1, CardCatalog.Backwater, Ten);
            var placed = TurnLoop.PlayCard(s, InHand(s, "backwater"), NoRng);
            s = placed.State;

            Assert.That(s.Player.StanceList.Select(e => e.Def), Is.EqualTo(new[] { BackwaterStance }));
            Assert.That(PlayChecked(s, "ten"), Is.EqualTo((30, 30, 0)), "10 → 30");
        }

        [Test]
        public void Backwater_WithEmpower_Is45()
        {
            var s = Holding(Opened(Idle, 1, Ten), BackwaterStance);
            s = s with { Player = s.Player with { Statuses = StatusSet.Of((StatusKind.Empower, 1)) } };

            Assert.That(PlayChecked(s, "ten"), Is.EqualTo((45, 45, 0)), "10 × 1.5 × 3");
        }

        [Test]
        public void TwoBackwaters_TripleOnce()
        {
            var s = Holding(Opened(Idle, 1, Ten), BackwaterStance, BackwaterStance);

            Assert.That(PlayChecked(s, "ten"), Is.EqualTo((30, 30, 0)), "×3, not ×9");
        }

        [Test]
        public void Backwater_TriplesEachBlowAgainstGuard()
        {
            // 5 × 3 = 15 a blow: 15 into Guard 4 → 11, then 15 → 15.
            var s = Holding(Opened(Idle, 1, TwoFives), BackwaterStance);
            s = s.WithEnemy(s.Enemy with { Guard = 4 });

            Assert.That(PlayChecked(s, "two_fives"), Is.EqualTo((30, 26, 0)));
        }

        [Test]
        public void ABlowTaken_IsDoubledBeforeGuard_AndThePreviewSaysSo()
        {
            // An 8 into a player holding 背水の陣 and Guard 4: 8 × 2 = 16 − 4 = 12.
            var hit = Fixtures.EnemyAction("eight", face: new Face(Power: 8));
            var s = Opened(Fixtures.Enemy("eight_fixture", atZero: hit, atOneToTwo: hit), 1);
            s = Holding(s, BackwaterStance, BackwaterStance);
            s = s with { Player = s.Player with { Stamina = 0, Guard = 4 } };
            Assert.That(s.Omen!.ActionId, Is.EqualTo("eight"));

            var omen = TurnLoop.PreviewOmen(s, 0)!;
            var dealt = TurnLoop.EndTurn(s, NoRng).Events.OfType<DamageDealt>().Where(d => d.Target == Actor.Player).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(dealt.Select(d => (d.Raw, d.Damage)), Is.EqualTo(new[] { (16, 12) }), "two 背水の陣 still double once");
                Assert.That((omen.RawPower, omen.Damage), Is.EqualTo((16, 12)), "the omen's number");
            });
        }

        [Test]
        public void AStoppedBackwater_MultipliesNothing()
        {
            // #334: a stopped entry does nothing for its turns.
            var s = Holding(Opened(Idle, 1, Ten), BackwaterStance);
            var list = s.Player.StanceList.Select(e => e with { StoppedThrough = s.Turn }).ToList();
            s = s with { Player = s.Player with { Stances = list } };

            Assert.That(PlayChecked(s, "ten"), Is.EqualTo((10, 10, 0)));
        }

        // ---- 覚醒 ----

        [Test]
        public void PlayingAwakening_BoostsTheCardsAfterIt_AndNotItself()
        {
            var s = Opened(Idle, 1, CardCatalog.Awakening, CutAndGuard);
            var placed = TurnLoop.PlayCard(s, InHand(s, "awakening"), NoRng);
            Assert.That(placed.Events.OfType<StanceFired>(), Is.Empty, "覚醒 does not add to itself");
            s = placed.State;

            Assert.That(PlayChecked(s, "cut_and_guard"), Is.EqualTo((15, 15, 11)), "attack +5 and Guard +5 on one card");
        }

        [Test]
        public void TwoAwakenings_AddTen()
        {
            var s = Holding(Opened(Idle, 1, CutAndGuard), AwakeningStance, AwakeningStance);

            Assert.That(PlayChecked(s, "cut_and_guard"), Is.EqualTo((20, 20, 16)));
        }

        [Test]
        public void Awakening_RidesEveryBlow()
        {
            // Like a stance's 条件付き加算: 5 + 5 a blow, twice.
            var s = Holding(Opened(Idle, 1, TwoFives), AwakeningStance);

            Assert.That(PlayChecked(s, "two_fives"), Is.EqualTo((20, 20, 0)));
        }

        [Test]
        public void Awakening_AndBackwater_AddFirstThenTriple()
        {
            // (10 + 5) × 3.
            var s = Holding(Opened(Idle, 1, Ten), AwakeningStance, BackwaterStance);

            Assert.That(PlayChecked(s, "ten"), Is.EqualTo((45, 45, 0)));
        }

        [TestCase(0, 4, TestName = "AMoveOfTwo_WithoutAwakening_GoesTwo")]
        [TestCase(1, 5, TestName = "AMoveOfTwo_WithAwakening_GoesThree")]
        [TestCase(2, 5, TestName = "AMoveOfTwo_WithTwoAwakenings_StillGoesThree")]
        public void AwakeningsMove_StopsAtThreeCells(int awakenings, int cellAfter)
        {
            // Player on cell 2, the enemy at gap 5 (cell 8): forward 2 (+1 a 覚醒, 3 at most).
            var s = Opened(Idle, 5, StepTwo);
            s = Holding(s, Enumerable.Repeat(AwakeningStance, awakenings).ToArray());

            var after = TurnLoop.PlayCard(s, InHand(s, "step_two"), NoRng).State;

            Assert.That(after.Player.Cell, Is.EqualTo(cellAfter));
        }

        [Test]
        public void AMoveOfOne_WithTwoAwakenings_GoesThree()
        {
            var s = Holding(Opened(Idle, 5, StepOne), AwakeningStance, AwakeningStance);

            var after = TurnLoop.PlayCard(s, InHand(s, "step_one"), NoRng).State;

            Assert.That(after.Player.Cell, Is.EqualTo(5));
        }

        [Test]
        public void Awakening_AddsNoGuardToACardWithoutOne_AndNoPowerToACardThatDoesNotAttack()
        {
            var s = Holding(Opened(Idle, 1, Ten, StepOne), AwakeningStance);

            Assert.Multiple(() =>
            {
                Assert.That(TurnLoop.Preview(s, InHand(s, "ten"))!.GuardGain, Is.EqualTo(0));
                var step = TurnLoop.PlayCard(s, InHand(s, "step_one"), NoRng).Events;
                Assert.That(step.OfType<GuardGained>(), Is.Empty);
                Assert.That(step.OfType<DamageDealt>(), Is.Empty);
            });
        }
    }
}
