using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// The demo vocabulary (#188): the nine status words (§5 / §5.1 / §19.5 S13), the trait
    /// conditions and effects (§2.3), heal and 崩し, and the stance slot with its exile pile (§4).
    ///
    /// Every scenario runs through the real TurnLoop (Start → BeginPlayerTurn → PlayCard → EndTurn).
    /// A state is hand-set only where a test needs a status, a number or a stance the opening does
    /// not give. The deck is dealt in order (FixedRng(0.999…) makes the shuffle a no-op) on an
    /// 8-cell line with the player on cell 2, so a start gap of n puts the first enemy on cell 3 + n.
    /// </summary>
    public class VocabularyTests
    {
        private const int Cells = 8;

        private static readonly IRng NoRng = new FixedRng(0.9999999);

        // ---- Fixtures ----

        private static readonly EnemyActionDef Wait =
            Fixtures.EnemyAction("wait", face: new Face(), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        /// <summary>An enemy that never touches the player: every branch is a Guard action with no Guard. HP 60, stamina 10, recovery 2.</summary>
        private static readonly EnemyDef Idle = Fixtures.Enemy("idle", atZero: Wait, atOneToTwo: Wait, atThreePlus: Wait);

        /// <summary>A fixture enemy whose 1〜2 branch is a cost-3 blow, so one point of 崩し decides whether it can pay.</summary>
        private static EnemyDef Brute() =>
            Fixtures.Enemy("brute", atOneToTwo: Fixtures.EnemyAction("heavy", column: 3, face: new Face(Power: 9, Reach: new Reach(1, 2))));

        private static readonly CardDef Filler =
            Fixtures.Card("filler", 1, new Face(Guard: 1), BattleAttribute.Guard, targets: TargetKind.Self);

        private static readonly CardDef Block =
            Fixtures.Card("block", 1, new Face(Guard: 4), BattleAttribute.Guard, targets: TargetKind.Self);

        private static readonly CardDef StepIn =
            Fixtures.Card("step_in", 1, new Face(Move: 1), BattleAttribute.Move, targets: TargetKind.Self);

        private static readonly CardDef StepOut =
            Fixtures.Card("step_out", 1, new Face(Move: -1), BattleAttribute.Move, targets: TargetKind.Self);

        private static readonly CardDef Breathe =
            Fixtures.Card("breathe", 1, new Face(StaminaGain: 1), BattleAttribute.Skill, targets: TargetKind.Self);

        /// <summary>A single attack of the given power at reach 0〜1.</summary>
        private static CardDef Jab(int power, int column = 1) => Fixtures.Card("jab" + power, column, new Face(Power: power));

        // ---- Helpers ----

        /// <summary>The cards in this order, padded with fillers to one full hand.</summary>
        private static List<CardInstance> Deck(IReadOnlyList<CardDef> cards)
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < cards.Count; i++) deck.Add(new CardInstance(cards[i].Id + "-" + i, cards[i]));
            for (int i = deck.Count; i < Constants.HandDraw; i++) deck.Add(new CardInstance(Filler.Id + "-" + i, Filler));
            return deck;
        }

        private static BattleState Started(int gap, EnemyDef enemy, params CardDef[] cards) =>
            TurnLoop.Start(new BattleSetup(enemy, Deck(cards), Cells, StartGap: gap), NoRng).State;

        /// <summary>A battle on turn 1, waiting for the player's first card.</summary>
        private static BattleState Opened(int gap, EnemyDef enemy, params CardDef[] cards) =>
            TurnLoop.BeginPlayerTurn(Started(gap, enemy, cards), NoRng).State;

        /// <summary>Two fixture enemies side by side: "a" adjacent (a 5-power hit at 0〜1), "b" at gap 1 (a 4-power hit at 1〜2).</summary>
        private static BattleState OpenedAgainstTwo(params CardDef[] cards)
        {
            var setup = new BattleSetup(
                Fixtures.Enemy("a"), Deck(cards), Cells, StartGap: 0, MoreEnemies: new[] { Fixtures.Enemy("b") });
            return TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoRng).State, NoRng).State;
        }

        private static string InHand(BattleState s, string cardId) => s.Hand.First(c => c.Def.Id == cardId).InstanceId;

        private static StepResult Play(BattleState s, string cardId) => TurnLoop.PlayCard(s, InHand(s, cardId), NoRng);

        private static StepResult Begin(BattleState s) => TurnLoop.BeginPlayerTurn(s, NoRng);

        private static StepResult End(BattleState s) => TurnLoop.EndTurn(s, NoRng);

        private static BattleState WithPlayerStatuses(BattleState s, params (StatusKind Kind, int Stacks)[] entries) =>
            s with { Player = s.Player with { Statuses = StatusSet.Of(entries) } };

        private static BattleState WithEnemyStatuses(BattleState s, params (StatusKind Kind, int Stacks)[] entries) =>
            s.WithEnemy(s.Enemy with { Statuses = StatusSet.Of(entries) });

        /// <summary>Asserts that <paramref name="expected"/> appears in <paramref name="actual"/> in this order (gaps allowed).</summary>
        private static void AssertInOrder(IReadOnlyList<BattleEvent> actual, params Type[] expected)
        {
            int cursor = 0;
            foreach (var type in expected)
            {
                int found = -1;
                for (int i = cursor; i < actual.Count; i++)
                {
                    if (actual[i].GetType() == type) { found = i; break; }
                }
                Assert.That(found, Is.GreaterThanOrEqualTo(0),
                    $"{type.Name} is missing after index {cursor} in: {string.Join(", ", actual.Select(e => e.GetType().Name))}");
                cursor = found + 1;
            }
        }

        // ---- §5: the two decay types and the two sides ----

        [TestCase(StatusKind.Slow, StatusDecay.OnTurn, false)]
        [TestCase(StatusKind.Bleed, StatusDecay.OnTurn, false)]
        [TestCase(StatusKind.Fatigue, StatusDecay.OnTurn, false)]
        [TestCase(StatusKind.Regen, StatusDecay.OnTurn, true)]
        [TestCase(StatusKind.Fragile, StatusDecay.OnUse, false)]
        [TestCase(StatusKind.Intimidate, StatusDecay.OnUse, false)]
        [TestCase(StatusKind.Empower, StatusDecay.OnUse, true)]
        [TestCase(StatusKind.Focus, StatusDecay.OnUse, true)]
        [TestCase(StatusKind.Parry, StatusDecay.OnUse, true)]
        public void EachWord_HasItsDecayType_AndItsSide(StatusKind kind, StatusDecay decay, bool own)
        {
            // §5 の表: 減り方 (ターンで減る / 使うと減る) and 向き (自分に = own).
            Assert.That(Statuses.DecayOf(kind), Is.EqualTo(decay));
            Assert.That(Statuses.IsOwn(kind), Is.EqualTo(own));
        }

        [Test]
        public void TheTurnStartTick_TakesAStackOffTheTurnWords_AndLeavesTheUseWords()
        {
            // §5: ターンで減る型 lose one stack at the holder's turn start; 使うと減る型 wait to be used. The demo has nine words (俊敏 left out).
            var kinds = Enum.GetValues(typeof(StatusKind)).Cast<StatusKind>().ToList();
            var ticked = StatusSet.Of(kinds.Select(k => (k, 2)).ToArray()).TickTurnStart();

            Assert.Multiple(() =>
            {
                Assert.That(kinds, Has.Count.EqualTo(9));
                foreach (var kind in kinds)
                {
                    int expected = Statuses.DecayOf(kind) == StatusDecay.OnTurn ? 1 : 2;
                    Assert.That(ticked.Stacks(kind), Is.EqualTo(expected), kind.ToToken());
                }
            });
        }

        // ---- §5 出血 ----

        [Test]
        public void Bleed_TakesTwoPerStack_AtTheHoldersTurnStart_ThenLosesAStack()
        {
            // §5 出血 / §9 step 3: HP −2 × the stacks held before the tick, then one stack goes (2 → −4, then −2, then gone).
            var s = WithPlayerStatuses(Started(1, Idle), (StatusKind.Bleed, 2));

            var t1 = Begin(s);
            var t2 = Begin(End(t1.State).State);
            var t3 = Begin(End(t2.State).State);

            Assert.Multiple(() =>
            {
                Assert.That(t1.Events.OfType<StatusHpChanged>().Single(), Is.EqualTo(new StatusHpChanged(Actor.Player, StatusKind.Bleed, -4, 46)));
                Assert.That(t1.Events.OfType<StatusTicked>().Single(), Is.EqualTo(new StatusTicked(Actor.Player, StatusKind.Bleed, 1)));
                AssertInOrder(t1.Events, typeof(GuardCleared), typeof(StaminaRecovered), typeof(StatusHpChanged), typeof(StatusTicked), typeof(Drawn));
                Assert.That(t1.State.Player.Hp, Is.EqualTo(46));

                Assert.That(t2.Events.OfType<StatusHpChanged>().Single(), Is.EqualTo(new StatusHpChanged(Actor.Player, StatusKind.Bleed, -2, 44)));
                Assert.That(t2.Events.OfType<StatusTicked>().Single(), Is.EqualTo(new StatusTicked(Actor.Player, StatusKind.Bleed, 0)));
                Assert.That(t2.State.Player.Statuses.Has(StatusKind.Bleed), Is.False);

                Assert.That(t3.Events.OfType<StatusHpChanged>(), Is.Empty);
                Assert.That(t3.State.Player.Hp, Is.EqualTo(44));
            });
        }

        [Test]
        public void Bleed_OnAnEnemy_TicksAtItsOwnTurnStart_BeforeItActs()
        {
            // §5 出血 / §9 step 9: the enemy's own turn start is where its bleed takes HP.
            var s = WithEnemyStatuses(Opened(1, Idle), (StatusKind.Bleed, 2));

            var first = End(s);
            var second = End(Begin(first.State).State);

            Assert.Multiple(() =>
            {
                Assert.That(first.Events.OfType<StatusHpChanged>().Single(), Is.EqualTo(new StatusHpChanged(Actor.Enemy, StatusKind.Bleed, -4, 56)));
                Assert.That(first.Events.OfType<StatusTicked>().Single(), Is.EqualTo(new StatusTicked(Actor.Enemy, StatusKind.Bleed, 1)));
                AssertInOrder(first.Events, typeof(GuardCleared), typeof(StatusHpChanged), typeof(ActionExecuted));
                Assert.That(first.State.Enemy.Hp, Is.EqualTo(56));

                Assert.That(second.Events.OfType<StatusHpChanged>().Single(), Is.EqualTo(new StatusHpChanged(Actor.Enemy, StatusKind.Bleed, -2, 54)));
                Assert.That(second.State.Enemy.Statuses.Has(StatusKind.Bleed), Is.False);
            });
        }

        [Test]
        public void Bleed_CanEndTheBattle_AtThePlayersTurnStart_BeforeAnyDraw()
        {
            // §17.6 F9: HP 0 from the bleed tick is a loss on the spot; step 4 (the draw) never comes.
            var s = Started(1, Idle);
            s = s with { Player = s.Player with { Hp = 3, Statuses = StatusSet.Of((StatusKind.Bleed, 2)) } };

            var begin = Begin(s);

            Assert.Multiple(() =>
            {
                Assert.That(begin.Events.OfType<StatusHpChanged>().Single(), Is.EqualTo(new StatusHpChanged(Actor.Player, StatusKind.Bleed, -3, 0)));
                Assert.That(begin.State.Player.Hp, Is.EqualTo(0));
                Assert.That(begin.State.Result, Is.EqualTo(GameResult.Lost));
                Assert.That(begin.State.Phase, Is.EqualTo(BattlePhase.Finished));
                Assert.That(begin.Events.OfType<Drawn>(), Is.Empty);
                Assert.That(begin.Events.OfType<OmenSet>(), Is.Empty);
                Assert.That(begin.State.Hand, Is.Empty);
                Assert.That(begin.State.DrawPile, Has.Count.EqualTo(5));
                Assert.That(begin.Events.Last(), Is.EqualTo(new BattleEnded(Actor.Enemy, GameResult.Lost)));
            });
        }

        [Test]
        public void Bleed_KillingALoneEnemy_AtItsTurnStart_WinsBeforeItActs()
        {
            // §17.6 F9 / §9 step 9: the polearm bleeds out before its sweep, so it never acts.
            var s = Opened(2, Enemies.PolearmWarped);
            Assert.That(s.Omen!.ActionId, Is.EqualTo("sweep"));
            s = s.WithEnemy(s.Enemy with { Hp = 3, Statuses = StatusSet.Of((StatusKind.Bleed, 2)) });

            var end = End(s);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<StatusHpChanged>().Single(), Is.EqualTo(new StatusHpChanged(Actor.Enemy, StatusKind.Bleed, -3, 0)));
                Assert.That(end.Events.OfType<ActionExecuted>(), Is.Empty);
                Assert.That(end.Events.OfType<Rested>(), Is.Empty);
                Assert.That(end.Events.OfType<OmenSet>(), Is.Empty);
                Assert.That(end.State.Player.Hp, Is.EqualTo(50));
                Assert.That(end.State.Result, Is.EqualTo(GameResult.Won));
                Assert.That(end.State.Phase, Is.EqualTo(BattlePhase.Finished));
                Assert.That(end.Events.Last(), Is.EqualTo(new BattleEnded(Actor.Player, GameResult.Won)));
            });
        }

        [Test]
        public void Bleed_KillingOneOfTwoEnemies_SkipsItsAction_AndTheOtherStillActs()
        {
            // §7.4 / §17.6 F9: the one that bled out leaves the line without acting; the battle goes on.
            var s = OpenedAgainstTwo();
            s = s.WithEnemy(0, s.Enemies[0].Body with { Hp = 3, Statuses = StatusSet.Of((StatusKind.Bleed, 2)) });

            var end = End(s);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<EnemyDefeated>().Single().Unit, Is.EqualTo(0));
                Assert.That(end.Events.OfType<ActionExecuted>().Select(e => e.Unit), Is.EqualTo(new[] { 1 }));
                Assert.That(end.State.Enemies[0].Alive, Is.False);
                Assert.That(end.State.Enemies[0].Omen, Is.Null);
                Assert.That(end.State.Result, Is.EqualTo(GameResult.Ongoing));
            });
        }

        // ---- §5 再生 / 疲労 ----

        [Test]
        public void Regen_GivesTwoPerStack_AtTheHoldersTurnStart_CappedAtMaxHp()
        {
            // §5 再生: HP +2 × stacks at the holder's turn start, never past the maximum.
            var s = Started(1, Idle);
            s = s with { Player = s.Player with { Hp = 40, Statuses = StatusSet.Of((StatusKind.Regen, 3)) } };

            var t1 = Begin(s);
            var between = End(t1.State).State;
            between = between with { Player = between.Player with { Hp = 48 } };
            var t2 = Begin(between);

            Assert.Multiple(() =>
            {
                Assert.That(t1.Events.OfType<StatusHpChanged>().Single(), Is.EqualTo(new StatusHpChanged(Actor.Player, StatusKind.Regen, 6, 46)));
                Assert.That(t1.Events.OfType<StatusTicked>().Single(), Is.EqualTo(new StatusTicked(Actor.Player, StatusKind.Regen, 2)));
                Assert.That(t1.State.Player.Hp, Is.EqualTo(46));

                // Regen 2 would give 4; the maximum lets 2 through.
                Assert.That(t2.Events.OfType<StatusHpChanged>().Single(), Is.EqualTo(new StatusHpChanged(Actor.Player, StatusKind.Regen, 2, 50)));
                Assert.That(t2.State.Player.Hp, Is.EqualTo(50));
                Assert.That(t2.State.Player.Statuses.Stacks(StatusKind.Regen), Is.EqualTo(1));
            });
        }

        [Test]
        public void Fatigue_TakesOneOffTheHoldersRecovery_HoweverManyStacks()
        {
            // §5 疲労 / §9 steps 2 and 9: the recovery is 1 less while it is held — flat, not per stack.
            var s = Started(1, Idle);
            s = s with { Player = s.Player with { Stamina = 2, Statuses = StatusSet.Of((StatusKind.Fatigue, 3)) } };

            var begin = Begin(s);
            var tired = begin.State.WithEnemy(begin.State.Enemy with { Stamina = 4, Statuses = StatusSet.Of((StatusKind.Fatigue, 2)) });
            var end = End(tired);

            Assert.Multiple(() =>
            {
                Assert.That(begin.Events.OfType<StaminaRecovered>().Single(), Is.EqualTo(new StaminaRecovered(Actor.Player, 2, 4, 10)));
                Assert.That(begin.Events.OfType<StatusTicked>().Single(), Is.EqualTo(new StatusTicked(Actor.Player, StatusKind.Fatigue, 2)));
                Assert.That(begin.State.Player.Stamina, Is.EqualTo(4));

                Assert.That(end.Events.OfType<StaminaRecovered>().Single(), Is.EqualTo(new StaminaRecovered(Actor.Enemy, 1, 5, 10)));
                Assert.That(end.Events.OfType<StatusTicked>().Single(), Is.EqualTo(new StatusTicked(Actor.Enemy, StatusKind.Fatigue, 1)));
            });
        }

        // ---- §5 強化 / 脆化 (§5.1, §19.5 S13) ----

        [Test]
        public void Empower_MakesTheNextAttackFaceOneAndAHalf_RoundedAwayFromZero_AndIsSpentOnce()
        {
            // §5 強化 / §5.1: ×1.5 then AwayFromZero (5 → 7.5 → 8); a card with no attack face leaves it alone.
            var s = WithPlayerStatuses(Opened(1, Idle, Block, Jab(5), Jab(5)), (StatusKind.Empower, 1));

            var guard = Play(s, "block");
            var preview = TurnLoop.Preview(guard.State, InHand(guard.State, "jab5"))!;
            var first = Play(guard.State, "jab5");
            var second = Play(first.State, "jab5");

            Assert.Multiple(() =>
            {
                Assert.That(guard.Events.OfType<StatusConsumed>(), Is.Empty);
                Assert.That(guard.State.Player.Statuses.Stacks(StatusKind.Empower), Is.EqualTo(1));

                Assert.That(preview.RawPower, Is.EqualTo(8));
                Assert.That(first.Events.OfType<DamageDealt>().Single(), Is.EqualTo(new DamageDealt(Actor.Player, Actor.Enemy, 8, 0, 8, 0, 52)));
                Assert.That(first.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Player, StatusKind.Empower, 0)));
                Assert.That(first.State.Player.Statuses.Has(StatusKind.Empower), Is.False);

                Assert.That(second.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(5));
            });
        }

        [Test]
        public void Fragile_MakesTheHoldersNextReceivedHitOneAndAHalf_AndIsSpent()
        {
            // §5 脆化: the next attack the holder takes is ×1.5, and one stack goes with it.
            var s = WithEnemyStatuses(Opened(1, Idle, Jab(10), Jab(10)), (StatusKind.Fragile, 1));

            var first = Play(s, "jab10");
            var second = Play(first.State, "jab10");

            Assert.Multiple(() =>
            {
                Assert.That(first.Events.OfType<DamageDealt>().Single(), Is.EqualTo(new DamageDealt(Actor.Player, Actor.Enemy, 15, 0, 15, 0, 45)));
                Assert.That(first.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Enemy, StatusKind.Fragile, 0)));
                Assert.That(first.State.Enemy.Statuses.Has(StatusKind.Fragile), Is.False);
                Assert.That(second.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(10));
            });
        }

        [Test]
        public void Fragile_OnThePlayer_MakesTheEnemysBlowOneAndAHalf()
        {
            // §5 脆化 works on whoever holds it: the fixture enemy's 5 becomes 7.5 → 8.
            var s = Opened(0, Fixtures.Enemy());
            s = s with { Player = s.Player with { Stamina = 0, Statuses = StatusSet.Of((StatusKind.Fragile, 2)) } };

            var end = End(s);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<DamageDealt>().Single(), Is.EqualTo(new DamageDealt(Actor.Enemy, Actor.Player, 8, 0, 8, 0, 42)));
                Assert.That(end.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Player, StatusKind.Fragile, 1)));
                Assert.That(end.State.Player.Statuses.Stacks(StatusKind.Fragile), Is.EqualTo(1));
            });
        }

        [Test]
        public void FragileAndEmpower_OnOneBlow_OnlyFragileApplies_AndEmpowerWaitsForTheNext()
        {
            // §19.5 S13: one multiplier per blow, 脆化 first; 強化 is not spent and goes to the next blow.
            var s = Opened(1, Idle, Jab(10), Jab(10));
            s = WithPlayerStatuses(s, (StatusKind.Empower, 1));
            s = WithEnemyStatuses(s, (StatusKind.Fragile, 1));

            var preview = TurnLoop.Preview(s, InHand(s, "jab10"))!;
            var first = Play(s, "jab10");
            var second = Play(first.State, "jab10");

            Assert.Multiple(() =>
            {
                Assert.That(preview.RawPower, Is.EqualTo(15));
                Assert.That(first.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(15), "not 10 × 2.25 = 23");
                Assert.That(first.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Enemy, StatusKind.Fragile, 0)));
                Assert.That(first.State.Player.Statuses.Stacks(StatusKind.Empower), Is.EqualTo(1));

                Assert.That(second.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(15));
                Assert.That(second.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Player, StatusKind.Empower, 0)));
            });
        }

        // ---- §5 威圧 ----

        [Test]
        public void Intimidate_TakesThreeOffPowerAndGuard_OfTheNextActionWithEither_OneStackEach()
        {
            // §5 威圧: −3 on power and on Guard (floor 0), spent only by an action that has one of them.
            var s = WithPlayerStatuses(
                Opened(1, Idle, Breathe, CardCatalog.ShieldBash, Jab(2), Filler), (StatusKind.Intimidate, 3));

            var skill = Play(s, "breathe");
            var preview = TurnLoop.Preview(skill.State, InHand(skill.State, "shield_bash"))!;
            var bash = Play(skill.State, "shield_bash");
            var tiny = Play(bash.State, "jab2");
            var guard = Play(tiny.State, "filler");

            Assert.Multiple(() =>
            {
                // A card with neither power nor Guard leaves it.
                Assert.That(skill.Events.OfType<StatusConsumed>(), Is.Empty);
                Assert.That(skill.State.Player.Statuses.Stacks(StatusKind.Intimidate), Is.EqualTo(3));

                // 盾打ち (8 / 6): both faces lose 3, and one stack goes for the card.
                Assert.That(preview.RawPower, Is.EqualTo(5));
                Assert.That(preview.GuardGain, Is.EqualTo(3));
                Assert.That(bash.Events.OfType<DamageDealt>().Single(), Is.EqualTo(new DamageDealt(Actor.Player, Actor.Enemy, 5, 0, 5, 0, 55)));
                Assert.That(bash.Events.OfType<GuardGained>().Single(), Is.EqualTo(new GuardGained(Actor.Player, 3, 3)));
                Assert.That(bash.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Player, StatusKind.Intimidate, 2)));

                // Floor 0 on the power and on the Guard.
                Assert.That(tiny.Events.OfType<DamageDealt>().Single(), Is.EqualTo(new DamageDealt(Actor.Player, Actor.Enemy, 0, 0, 0, 0, 55)));
                Assert.That(tiny.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Player, StatusKind.Intimidate, 1)));
                Assert.That(guard.Events.OfType<GuardGained>(), Is.Empty);
                Assert.That(guard.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Player, StatusKind.Intimidate, 0)));
                Assert.That(guard.State.Player.Statuses.Has(StatusKind.Intimidate), Is.False);
            });
        }

        [Test]
        public void Intimidate_OnTheEnemy_WeakensItsNextBlow()
        {
            // §5 威圧 (相手に): the fixture enemy's 5-power hit lands as 2, and one stack goes.
            var s = Opened(0, Fixtures.Enemy());
            s = s with { Player = s.Player with { Stamina = 0 } };
            s = WithEnemyStatuses(s, (StatusKind.Intimidate, 2));

            var end = End(s);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<DamageDealt>().Single(), Is.EqualTo(new DamageDealt(Actor.Enemy, Actor.Player, 2, 0, 2, 0, 48)));
                Assert.That(end.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Enemy, StatusKind.Intimidate, 1)));
            });
        }

        // ---- §5 集中 (the demo reading, #188) ----

        [Test]
        public void Focus_AddsTheColumnStep_ToASingleAttack()
        {
            // §5 集中 (demo: one column right = the scale's step added): a column-2 single attack 13 hits as column 3's 21.
            var s = WithPlayerStatuses(Opened(1, Idle, Jab(13, column: 2)), (StatusKind.Focus, 2));

            var preview = TurnLoop.Preview(s, InHand(s, "jab13"))!;
            var play = Play(s, "jab13");

            Assert.Multiple(() =>
            {
                Assert.That(preview.RawPower, Is.EqualTo(21));
                Assert.That(play.Events.OfType<DamageDealt>().Single(), Is.EqualTo(new DamageDealt(Actor.Player, Actor.Enemy, 21, 0, 21, 0, 39)));
                Assert.That(play.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Player, StatusKind.Focus, 1)));
            });
        }

        [Test]
        public void Focus_AddsTheColumnStep_ToADualGuard()
        {
            // §5 集中 / §3.1 二属性のガード面: a column-3 dual Guard 10 becomes 14.
            var guardSkill = Fixtures.Card("guard_skill", 3, new Face(Guard: 10), BattleAttribute.Guard | BattleAttribute.Skill, targets: TargetKind.Self);
            var s = WithPlayerStatuses(Opened(1, Idle, guardSkill), (StatusKind.Focus, 1));

            var preview = TurnLoop.Preview(s, InHand(s, "guard_skill"))!;
            var play = Play(s, "guard_skill");

            Assert.Multiple(() =>
            {
                Assert.That(preview.GuardGain, Is.EqualTo(14));
                Assert.That(play.Events.OfType<GuardGained>().Single(), Is.EqualTo(new GuardGained(Actor.Player, 14, 14)));
                Assert.That(play.State.Player.Statuses.Has(StatusKind.Focus), Is.False);
            });
        }

        [Test]
        public void Focus_AddsTheColumnStep_ToAHeal()
        {
            // §5 集中 / §3.1 単属性の回復: 応急処置 (column 3, heal 15) gains the step to column 4, +6.
            var s = Opened(1, Idle, CardCatalog.FirstAid);
            s = s with { Player = s.Player with { Hp = 20, Statuses = StatusSet.Of((StatusKind.Focus, 1)) } };

            var play = Play(s, "first_aid");

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<Healed>().Single(), Is.EqualTo(new Healed(Actor.Player, 21, 41)));
                Assert.That(play.State.Player.Hp, Is.EqualTo(41));
                Assert.That(play.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Player, StatusKind.Focus, 0)));
            });
        }

        [Test]
        public void Focus_IsSpentByAnyNextCard_EvenOneWithNothingToGrow()
        {
            // §5 集中: 次の札 is whatever card comes next; a one-cell move stays one cell.
            var s = WithPlayerStatuses(Opened(1, Idle, StepIn), (StatusKind.Focus, 1));

            var play = Play(s, "step_in");

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Player, StatusKind.Focus, 0)));
                Assert.That(play.Events.OfType<CellsMoved>().Single(), Is.EqualTo(new CellsMoved(Actor.Player, 2, 3, Pushed: false)));
                Assert.That(play.State.Player.Statuses.Has(StatusKind.Focus), Is.False);
            });
        }

        [Test]
        public void Focus_GivesNothingAtColumnFour_OrToAFaceItsAttributeDoesNotOwn()
        {
            // §5 集中 (列 4 は超えない): column 4 does not grow, and a move card's small Guard does not either.
            var s = WithPlayerStatuses(Opened(1, Idle, Jab(30, column: 4)), (StatusKind.Focus, 1));
            var play = Play(s, "jab30");

            Assert.Multiple(() =>
            {
                Assert.That(FocusStep.Of(BattleAttribute.Attack, 4, new Face(Power: 30)), Is.EqualTo(FocusStep.None));
                Assert.That(FocusStep.Of(BattleAttribute.Move, 1, new Face(Move: -2, Guard: 4)), Is.EqualTo(FocusStep.None));
                Assert.That(FocusStep.Of(BattleAttribute.Attack | BattleAttribute.Guard, 2, new Face(Power: 8, Guard: 6)),
                    Is.EqualTo(new FocusStep(6, 4, 0)), "a dual card reads the dual scales");

                Assert.That(play.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(30));
                Assert.That(play.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Player, StatusKind.Focus, 0)));
            });
        }

        // ---- §5 見切り ----

        [Test]
        public void Parry_ReturnsHalfOfWhatTheGuardAbsorbed_RoundedUp_ThroughTheAttackersGuard()
        {
            // §5 見切り: the enemy's Guard soaks 5, so 3 (5 / 2 up) goes back; the player's own Guard 1 takes part of it.
            var s = Opened(1, Idle, Jab(10));
            s = s with { Player = s.Player with { Guard = 1 } };
            s = s.WithEnemy(s.Enemy with { Guard = 5, Statuses = StatusSet.Of((StatusKind.Parry, 2)) });

            var play = Play(s, "jab10");

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<DamageDealt>().Single(), Is.EqualTo(new DamageDealt(Actor.Player, Actor.Enemy, 10, 5, 5, 0, 55)));
                Assert.That(play.Events.OfType<Reflected>().Single(), Is.EqualTo(new Reflected(Actor.Enemy, Actor.Player, 3, 1, 2, 0, 48)));
                Assert.That(play.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Enemy, StatusKind.Parry, 1)));
                AssertInOrder(play.Events, typeof(DamageDealt), typeof(StatusConsumed), typeof(Reflected));
                Assert.That(play.State.Player.Hp, Is.EqualTo(48));
                Assert.That(play.State.Player.Guard, Is.EqualTo(0));
            });
        }

        [Test]
        public void Parry_Stays_WhenTheGuardAbsorbedNothing()
        {
            // §5 見切り: it is spent only when there is something to return.
            var s = WithEnemyStatuses(Opened(1, Idle, Jab(10)), (StatusKind.Parry, 2));

            var play = Play(s, "jab10");

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<Reflected>(), Is.Empty);
                Assert.That(play.Events.OfType<StatusConsumed>(), Is.Empty);
                Assert.That(play.State.Enemy.Statuses.Stacks(StatusKind.Parry), Is.EqualTo(2));
                Assert.That(play.State.Player.Hp, Is.EqualTo(50));
            });
        }

        [Test]
        public void Parry_CanTakeTheAttackingEnemyDown_AndALoneEnemyLoses()
        {
            // §5 見切り / §17.6 F9: HP 0 from a return is a fall like any other.
            var s = Opened(0, Fixtures.Enemy());
            s = s with { Player = s.Player with { Stamina = 0, Guard = 10, Statuses = StatusSet.Of((StatusKind.Parry, 1)) } };
            s = s.WithEnemy(s.Enemy with { Hp = 2 });

            var end = End(s);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<DamageDealt>().Single(), Is.EqualTo(new DamageDealt(Actor.Enemy, Actor.Player, 5, 5, 0, 5, 50)));
                Assert.That(end.Events.OfType<Reflected>().Single(), Is.EqualTo(new Reflected(Actor.Player, Actor.Enemy, 3, 0, 3, 0, 0)));
                Assert.That(end.State.Result, Is.EqualTo(GameResult.Won));
                Assert.That(end.Events.OfType<OmenSet>(), Is.Empty);
                Assert.That(end.Events.Last(), Is.EqualTo(new BattleEnded(Actor.Player, GameResult.Won)));
            });
        }

        // ---- §5 上限 ----

        [Test]
        public void ThePlayer_RefusesASeventhKind_ButKeepsStackingAHeldOne()
        {
            // §5 上限 (6 kinds): 覚悟 at 死力 gives 強化 (a seventh kind: refused) and 再生 (already held: stacked).
            var six = StatusSet.Of(
                (StatusKind.Slow, 1), (StatusKind.Bleed, 1), (StatusKind.Fragile, 1),
                (StatusKind.Intimidate, 1), (StatusKind.Fatigue, 1), (StatusKind.Regen, 2));
            var s = Opened(1, Idle, CardCatalog.Resolve);
            s = s with { Player = s.Player with { Stamina = 2, Statuses = six } };

            var play = Play(s, "resolve");

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<StatusApplied>(), Is.EqualTo(new[]
                {
                    new StatusApplied(Actor.Player, Actor.Player, StatusKind.Empower, 2, 0, Refused: true),
                    new StatusApplied(Actor.Player, Actor.Player, StatusKind.Regen, 1, 3, Refused: false),
                }));
                Assert.That(play.State.Player.Statuses.KindCount, Is.EqualTo(6));
                Assert.That(play.State.Player.Statuses.Has(StatusKind.Empower), Is.False);
                Assert.That(play.State.Player.Statuses.Stacks(StatusKind.Regen), Is.EqualTo(3));
            });
        }

        [Test]
        public void AnEnemy_TakesAnyNumberOfKinds()
        {
            // §5 上限 (敵には置かない): 翻し足 lands three more words on an enemy already holding six.
            var s = Opened(1, Idle, CardCatalog.TwistAway);
            s = WithEnemyStatuses(s,
                (StatusKind.Bleed, 1), (StatusKind.Fragile, 1), (StatusKind.Empower, 1),
                (StatusKind.Focus, 1), (StatusKind.Parry, 1), (StatusKind.Regen, 1));

            var play = Play(s, "twist_away");

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<StatusApplied>(), Is.EqualTo(new[]
                {
                    new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Slow, 2, 2, Refused: false),
                    new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Fatigue, 2, 2, Refused: false),
                    new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Intimidate, 2, 2, Refused: false),
                }));
                Assert.That(play.State.Enemy.Statuses.KindCount, Is.EqualTo(9));
            });
        }

        // ---- §2.3 conditions ----

        private static TestCaseData Edge(string name, Trait trait, TraitContext context, bool holds) =>
            new TestCaseData(trait, context, holds).SetName("TraitCondition_" + name);

        private static IEnumerable<TestCaseData> ConditionEdges()
        {
            var none = Array.Empty<BattleAttribute>();

            var combo = new Trait(TraitCondition.Combo, TraitEffect.PowerBonus, 1, Attribute: BattleAttribute.Move);
            yield return Edge("Combo_Holds_AfterADualCardCarryingTheAttribute", combo,
                new TraitContext(Played: new[] { BattleAttribute.Attack | BattleAttribute.Move }), true);
            yield return Edge("Combo_Fails_WhenOnlyOtherAttributesWerePlayed", combo,
                new TraitContext(Played: new[] { BattleAttribute.Attack, BattleAttribute.Guard }), false);
            yield return Edge("Combo_Fails_WithNothingPlayed", combo, new TraitContext(Played: none), false);

            var omen = new Trait(TraitCondition.OmenIs, TraitEffect.PowerBonus, 1, Omen: OmenKind.Attack);
            yield return Edge("OmenIs_Holds_OnTheNamedKind", omen, new TraitContext(OpponentOmen: OmenKind.Attack), true);
            yield return Edge("OmenIs_Fails_OnAnotherKind", omen, new TraitContext(OpponentOmen: OmenKind.Guard), false);
            yield return Edge("OmenIs_Fails_WithNoOmen", omen, new TraitContext(OpponentOmen: null), false);

            var desperate = new Trait(TraitCondition.Desperate, TraitEffect.PowerBonus, 1);
            yield return Edge("Desperate_Holds_AtTwoStaminaBeforePaying", desperate, new TraitContext(StaminaBefore: 2), true);
            yield return Edge("Desperate_Fails_AtThree", desperate, new TraitContext(StaminaBefore: 3), false);

            var first = new Trait(TraitCondition.FirstPlay, TraitEffect.PowerBonus, 1);
            yield return Edge("FirstPlay_Holds_WithNothingPlayed", first, new TraitContext(Played: none), true);
            yield return Edge("FirstPlay_Fails_AfterOnePlay", first, new TraitContext(Played: new[] { BattleAttribute.Skill }), false);

            var finisher = new Trait(TraitCondition.Finisher, TraitEffect.PowerBonus, 1);
            yield return Edge("Finisher_Holds_OnTheThirdPlay", finisher,
                new TraitContext(Played: new[] { BattleAttribute.Attack, BattleAttribute.Guard }), true);
            yield return Edge("Finisher_Fails_OnTheSecondPlay", finisher, new TraitContext(Played: new[] { BattleAttribute.Attack }), false);

            var broken = new Trait(TraitCondition.Broken, TraitEffect.PowerBonus, 1);
            yield return Edge("Broken_Holds_BelowThreeOpponentStamina", broken, new TraitContext(OpponentStamina: 2), true);
            yield return Edge("Broken_Fails_AtThree", broken, new TraitContext(OpponentStamina: 3), false);

            var chain = new Trait(TraitCondition.Chain, TraitEffect.PowerBonus, 1);
            yield return Edge("Chain_Holds_WhenThePreviousPlaySharesAnAttribute", chain,
                new TraitContext(Played: new[] { BattleAttribute.Guard, BattleAttribute.Attack }, Attributes: BattleAttribute.Attack | BattleAttribute.Move), true);
            yield return Edge("Chain_ReadsOnlyThePreviousPlay", chain,
                new TraitContext(Played: new[] { BattleAttribute.Attack, BattleAttribute.Guard }, Attributes: BattleAttribute.Attack), false);
            yield return Edge("Chain_Fails_OnTheFirstPlay", chain, new TraitContext(Played: none, Attributes: BattleAttribute.Attack), false);

            var thin = new Trait(TraitCondition.Thin, TraitEffect.Draw, 1);
            yield return Edge("Thin_Holds_WithTwoCardsLeftAfterThePlay", thin, new TraitContext(HandAfterPlay: 2), true);
            yield return Edge("Thin_Fails_WithThree", thin, new TraitContext(HandAfterPlay: 3), false);

            var foeHas = new Trait(TraitCondition.FoeHas, TraitEffect.PowerBonus, 1, Watch: StatusKind.Bleed);
            yield return Edge("FoeHas_Holds_WhileTheOpponentHoldsTheWord", foeHas,
                new TraitContext(OpponentStatuses: StatusSet.Of((StatusKind.Bleed, 1))), true);
            yield return Edge("FoeHas_Fails_OnAnotherWord", foeHas,
                new TraitContext(OpponentStatuses: StatusSet.Of((StatusKind.Slow, 2))), false);
            yield return Edge("FoeHas_IgnoresTheOnePlayingsOwnWords", foeHas,
                new TraitContext(SelfStatuses: StatusSet.Of((StatusKind.Bleed, 1))), false);

            var selfHas = new Trait(TraitCondition.SelfHas, TraitEffect.GuardBonus, 3, Watch: StatusKind.Parry);
            yield return Edge("SelfHas_Holds_WhileTheOnePlayingHoldsTheWord", selfHas,
                new TraitContext(SelfStatuses: StatusSet.Of((StatusKind.Parry, 1))), true);
            yield return Edge("SelfHas_Fails_OnAnotherWord", selfHas,
                new TraitContext(SelfStatuses: StatusSet.Of((StatusKind.Empower, 2))), false);
            yield return Edge("SelfHas_IgnoresTheOpponentsWords", selfHas,
                new TraitContext(OpponentStatuses: StatusSet.Of((StatusKind.Parry, 1))), false);
        }

        [TestCaseSource(nameof(ConditionEdges))]
        public void TraitCondition_HoldsAndFailsAtItsEdge(Trait trait, TraitContext context, bool holds)
        {
            // §2.3 条件の表 (手薄 = 2 or fewer left after the play, decided 2026-09-23 #193): Holds and Evaluate agree.
            Assert.That(Traits.Holds(trait, context), Is.EqualTo(holds));
            Assert.That(Traits.Evaluate(trait, context).Triggered, Is.EqualTo(holds));
        }

        // ---- §2.3 effects ----

        [Test]
        public void EachEffect_FillsItsOwnOutcomeField()
        {
            // §2.3 効果の表: スタミナ / ドロー / 状態 / コスト −1 / 転換 / 追撃, and 崩し +n for enemy actions (roster §1.6).
            var holds = new TraitContext(Played: Array.Empty<BattleAttribute>());
            var grant = new StatusGrant(StatusKind.Fragile, 2);
            TraitOutcome On(TraitEffect effect, int amount = 0, StatusGrant? g = null) =>
                Traits.Evaluate(new Trait(TraitCondition.FirstPlay, effect, amount, Grant: g), holds);

            Assert.Multiple(() =>
            {
                Assert.That(On(TraitEffect.StaminaGain, 1), Is.EqualTo(new TraitOutcome(true, StaminaGain: 1)));
                Assert.That(On(TraitEffect.Draw, 1), Is.EqualTo(new TraitOutcome(true, Draw: 1)));
                Assert.That(On(TraitEffect.Status, g: grant).Triggered, Is.True);
                Assert.That(On(TraitEffect.Status, g: grant).GrantList, Is.EqualTo(new[] { grant }));
                Assert.That(On(TraitEffect.CostDown, 1), Is.EqualTo(new TraitOutcome(true, CostDown: 1)));
                Assert.That(On(TraitEffect.Convert), Is.EqualTo(new TraitOutcome(true, Convert: true)));
                Assert.That(On(TraitEffect.FollowUp, Constants.FollowUpPower), Is.EqualTo(new TraitOutcome(true, FollowUp: 5)));
                Assert.That(On(TraitEffect.BreakBonus, 1), Is.EqualTo(new TraitOutcome(true, BreakBonus: 1)));

                var unmet = new TraitContext(Played: new[] { BattleAttribute.Attack });
                Assert.That(Traits.Evaluate(new Trait(TraitCondition.FirstPlay, TraitEffect.Draw, 1), unmet), Is.EqualTo(TraitOutcome.None));
            });
        }

        [Test]
        public void TwoOutcomes_AddUp_FieldByField()
        {
            // §2.3 / swordsman_cards_v4 §3 (背水の陣): two traits judged at once add their bonuses.
            var bleed = new StatusGrant(StatusKind.Bleed, 1);
            var regen = new StatusGrant(StatusKind.Regen, 1, OnSelf: true);
            var a = new TraitOutcome(true, PowerBonus: 5, StaminaGain: 1, Grants: new[] { bleed }, FollowUp: 5);
            var b = new TraitOutcome(false, PowerBonus: 3, GuardBonus: 2, NextTurnRecoveryBonus: -1, Draw: 1,
                Grants: new[] { regen }, CostDown: 1, Convert: true, BreakBonus: 1);

            var sum = a.Plus(b);

            Assert.Multiple(() =>
            {
                Assert.That(sum.Triggered, Is.True);
                Assert.That(sum.PowerBonus, Is.EqualTo(8));
                Assert.That(sum.GuardBonus, Is.EqualTo(2));
                Assert.That(sum.NextTurnRecoveryBonus, Is.EqualTo(-1));
                Assert.That(sum.StaminaGain, Is.EqualTo(1));
                Assert.That(sum.Draw, Is.EqualTo(1));
                Assert.That(sum.GrantList, Is.EqualTo(new[] { bleed, regen }));
                Assert.That(sum.CostDown, Is.EqualTo(1));
                Assert.That(sum.Convert, Is.True);
                Assert.That(sum.FollowUp, Is.EqualTo(5));
                Assert.That(sum.BreakBonus, Is.EqualTo(1));
                Assert.That(TraitOutcome.None.Plus(TraitOutcome.None), Is.EqualTo(TraitOutcome.None));
            });
        }

        [TestCase(2, 2, 8)]
        [TestCase(2, 3, 5)]
        [TestCase(1, 2, 3)]
        [TestCase(1, 3, 0)]
        public void LastStand_JudgesBothTraits_AndAddsThem(int gap, int staminaBefore, int bonus)
        {
            // swordsman_cards_v4 #80 / §2.3: 間合い 2 以上 +5 and 死力 +3, judged on the same board.
            var outcome = Traits.EvaluateAll(CardCatalog.LastStand.AllTraits, new TraitContext(Gap: gap, StaminaBefore: staminaBefore));

            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.LastStand.AllTraits, Has.Count.EqualTo(2));
                Assert.That(outcome.PowerBonus, Is.EqualTo(bonus));
                Assert.That(outcome.Triggered, Is.EqualTo(bonus > 0));
            });
        }

        // ---- §2.3 effects in the loop ----

        [Test]
        public void CostDown_MakesAnUnpayableCardPlayable_AndTheLoweredCostIsWhatIsPaid()
        {
            // §2.3 コスト −1: 水の構え (2) costs 1 after a move; CanPlay, CostNow and Preview agree, and 1 is spent.
            var s = Opened(1, Idle, CardCatalog.WaterStance, StepOut);
            var poor = s with { Player = s.Player with { Stamina = 1 } };
            var moved = Play(s with { Player = s.Player with { Stamina = 2 } }, "step_out").State;
            string water = InHand(moved, "water_stance");

            var play = TurnLoop.PlayCard(moved, water, NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(TurnLoop.CanPlay(poor, InHand(poor, "water_stance")), Is.EqualTo(PlayRefusal.NotEnoughStamina));
                Assert.That(TurnLoop.CostNow(poor, CardCatalog.WaterStance), Is.EqualTo(2));
                Assert.That(TurnLoop.Preview(poor, InHand(poor, "water_stance"))!.Cost, Is.EqualTo(2));

                Assert.That(moved.Player.Stamina, Is.EqualTo(1));
                Assert.That(TurnLoop.CanPlay(moved, water), Is.EqualTo(PlayRefusal.None));
                Assert.That(TurnLoop.CostNow(moved, CardCatalog.WaterStance), Is.EqualTo(1));
                Assert.That(TurnLoop.Preview(moved, water)!.Cost, Is.EqualTo(1));
                Assert.That(TurnLoop.Preview(moved, water)!.TraitHolds, Is.True);

                Assert.That(play.Events.OfType<StaminaSpent>().Single(), Is.EqualTo(new StaminaSpent(Actor.Player, 1, 0)));
                Assert.That(play.State.Player.Stamina, Is.EqualTo(0));
            });
        }

        [Test]
        public void FollowUp_FromPackHowl_LandsOnTheNextAttackFace_ThenIsGone()
        {
            // §2.3 追撃 / §5.1: 群れの咆哮 with 強化 held waits +5 for the next attack face; added first, then ×1.5.
            var s = WithPlayerStatuses(Opened(1, Idle, CardCatalog.PackHowl, Jab(10), Jab(10)), (StatusKind.Empower, 1));

            var howl = Play(s, "pack_howl");
            var preview = TurnLoop.Preview(howl.State, InHand(howl.State, "jab10"))!;
            var first = Play(howl.State, "jab10");
            var second = Play(first.State, "jab10");

            Assert.Multiple(() =>
            {
                Assert.That(howl.Events.OfType<TraitEvaluated>().Single().Outcome.FollowUp, Is.EqualTo(5));
                Assert.That(howl.Events.OfType<DamageDealt>(), Is.Empty);
                Assert.That(howl.State.Player.FollowUp, Is.EqualTo(5));
                Assert.That(howl.State.Player.Statuses.Stacks(StatusKind.Empower), Is.EqualTo(3), "1 held + 2 from the howl");

                Assert.That(preview.RawPower, Is.EqualTo(23));
                Assert.That(first.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(23), "(10 + 5) × 1.5 = 22.5 → 23");
                Assert.That(first.State.Player.FollowUp, Is.EqualTo(0));

                Assert.That(second.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(15), "10 × 1.5, no follow-up left");
            });
        }

        [Test]
        public void FollowUp_WaitsForTheNextAttackFace_NotTheCardsOwn()
        {
            // §2.3 追撃 (このターンの次のアタック面): 浄化の一閃 into an attack omen hits its own 8 and hands +5 on.
            var s = Opened(1, Fixtures.Enemy(), CardCatalog.PurgeFlash, Jab(10));
            Assert.That(s.Omen!.Label.Kind, Is.EqualTo(OmenKind.Attack));

            var flash = Play(s, "purge_flash");
            var jab = Play(flash.State, "jab10");

            Assert.Multiple(() =>
            {
                Assert.That(flash.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(8));
                Assert.That(flash.State.Player.FollowUp, Is.EqualTo(5));
                Assert.That(jab.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(15));
                Assert.That(jab.State.Player.FollowUp, Is.EqualTo(0));
            });
        }

        [Test]
        public void AnUnusedFollowUp_IsGoneAtTurnEnd()
        {
            // §17.6 F7: 追撃 lasts until the end of the holder's turn.
            var s = WithPlayerStatuses(Opened(1, Idle, CardCatalog.PackHowl), (StatusKind.Empower, 1));

            var howl = Play(s, "pack_howl");
            var end = End(howl.State);
            var next = Begin(end.State);

            Assert.Multiple(() =>
            {
                Assert.That(howl.State.Player.FollowUp, Is.EqualTo(5));
                Assert.That(end.State.Player.FollowUp, Is.EqualTo(0));
                Assert.That(next.State.Player.FollowUp, Is.EqualTo(0));
            });
        }

        [Test]
        public void Convert_OnCrescentCut_AsTheThirdPlay_AddsHalfItsGuardToItsPower()
        {
            // §2.3 締め → 転換: 8 + ceil(6 / 2) = 11 as the third play, a plain 8 as the first; the Guard face still lands.
            var s = Opened(1, Idle, Block, Block, CardCatalog.CrescentCut);

            var first = Play(s, "crescent_cut");
            var third = Play(Play(Play(s, "block").State, "block").State, "crescent_cut");

            Assert.Multiple(() =>
            {
                Assert.That(first.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(8));
                Assert.That(third.Events.OfType<TraitEvaluated>().Single().Outcome.Convert, Is.True);
                Assert.That(third.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(11));
                Assert.That(third.Events.OfType<GuardGained>().Single(), Is.EqualTo(new GuardGained(Actor.Player, 6, 14)));
            });
        }

        [Test]
        public void Draw_OnObserve_WithAThinHand_DrawsThree()
        {
            // §2.3 手薄 → ドロー +1 (#193: 2 or fewer left): 観察 draws 2, or 3 when it leaves the hand thin.
            var s = Opened(1, Idle, CardCatalog.Observe, Block, Block, Filler, Filler, Jab(5), Jab(6), Jab(7));

            var full = Play(s, "observe");
            var thinned = Play(Play(s, "block").State, "block").State;
            var thin = Play(thinned, "observe");

            Assert.Multiple(() =>
            {
                Assert.That(full.Events.OfType<TraitEvaluated>().Single().Outcome.Triggered, Is.False);
                Assert.That(full.Events.OfType<Drawn>().Count(), Is.EqualTo(2));

                Assert.That(thinned.Hand, Has.Count.EqualTo(3));
                Assert.That(thin.Events.OfType<TraitEvaluated>().Single().Outcome.Draw, Is.EqualTo(1));
                Assert.That(thin.Events.OfType<Drawn>().Select(d => d.Card.Def.Id), Is.EqualTo(new[] { "jab5", "jab6", "jab7" }));
                Assert.That(thin.State.Hand, Has.Count.EqualTo(5));
            });
        }

        [Test]
        public void StaminaGain_OnFootwork_AsTheFirstPlay_GivesOneBack()
        {
            // §2.3 初手 → スタミナ +1: 足運び pays 1 and gets 1 back as the turn's first card, not later.
            var s = Opened(1, Idle, CardCatalog.Footwork, Block);
            s = s with { Player = s.Player with { Stamina = 5 } };

            var first = Play(s, "footwork");
            var later = Play(Play(s, "block").State, "footwork");

            Assert.Multiple(() =>
            {
                Assert.That(first.Events.OfType<StaminaGained>().Single(), Is.EqualTo(new StaminaGained(Actor.Player, 1, 5)));
                Assert.That(first.State.Player.Stamina, Is.EqualTo(5));
                Assert.That(later.Events.OfType<StaminaGained>(), Is.Empty);
                Assert.That(later.State.Player.Stamina, Is.EqualTo(3));
            });
        }

        // ---- Heal and 崩し (#188) ----

        [Test]
        public void Heal_RestoresHp_CappedAtTheMaximum()
        {
            // §3.1 単属性の回復 (#188 heal face): 応急処置 heals 15, never past max HP.
            var s = Opened(1, Idle, CardCatalog.FirstAid);

            var hurt = Play(s with { Player = s.Player with { Hp = 30 } }, "first_aid");
            var scratched = Play(s with { Player = s.Player with { Hp = 40 } }, "first_aid");

            Assert.Multiple(() =>
            {
                Assert.That(hurt.Events.OfType<Healed>().Single(), Is.EqualTo(new Healed(Actor.Player, 15, 45)));
                Assert.That(hurt.State.Player.Hp, Is.EqualTo(45));
                Assert.That(scratched.Events.OfType<Healed>().Single(), Is.EqualTo(new Healed(Actor.Player, 10, 50)));
                Assert.That(scratched.State.Player.Hp, Is.EqualTo(50));
            });
        }

        [Test]
        public void Break_TakesStaminaOffTheEnemy_NeverBelowZero()
        {
            // §3.1 崩し (相手のスタミナ −n): 峰打ち takes 1 off whoever it hits, after the damage, floor 0.
            var s = Opened(1, Brute(), CardCatalog.FlatStrike);

            var full = Play(s, "flat_strike");
            var empty = Play(s.WithEnemy(s.Enemy with { Stamina = 0 }), "flat_strike");

            Assert.Multiple(() =>
            {
                Assert.That(full.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(6));
                Assert.That(full.Events.OfType<StaminaBroken>().Single(), Is.EqualTo(new StaminaBroken(Actor.Player, Actor.Enemy, 1, 9)));
                AssertInOrder(full.Events, typeof(DamageDealt), typeof(StaminaBroken));
                Assert.That(full.State.Enemy.Stamina, Is.EqualTo(9));

                Assert.That(empty.Events.OfType<StaminaBroken>().Single(), Is.EqualTo(new StaminaBroken(Actor.Player, Actor.Enemy, 0, 0)));
                Assert.That(empty.State.Enemy.Stamina, Is.EqualTo(0));
            });
        }

        [Test]
        public void Break_CanTurnTheStandingOmenIntoARest()
        {
            // §6 / §3 (払えない予兆は休み): after 峰打ち, 1 → 0 stamina recovers only to 2, short of the declared cost-3 blow.
            var s = Opened(1, Brute(), CardCatalog.FlatStrike);
            Assert.That(s.Omen!.ActionId, Is.EqualTo("heavy"));
            s = s.WithEnemy(s.Enemy with { Stamina = 1 });

            var unbroken = End(s);
            var broken = End(Play(s, "flat_strike").State);

            Assert.Multiple(() =>
            {
                Assert.That(unbroken.Events.OfType<ActionExecuted>().Single().Action.Id, Is.EqualTo("heavy"));
                Assert.That(broken.Events.OfType<ActionExecuted>(), Is.Empty);
                Assert.That(broken.Events.OfType<Rested>().Single().Declared.ActionId, Is.EqualTo("heavy"));
            });
        }

        // ---- §4 stance ----

        [Test]
        public void AStanceFace_TakesTheSlot_ASecondReplacesIt_AndBothCardsAreExiled()
        {
            // §4: one slot; a new stance ends the old one; a stance card goes to the exile pile, never the discard pile.
            var s = Opened(1, Idle, CardCatalog.RockStance, CardCatalog.WaterStance);

            var rock = Play(s, "rock_stance");
            var water = Play(rock.State, "water_stance");
            var end = End(water.State);
            var next = Begin(end.State);

            Assert.Multiple(() =>
            {
                Assert.That(rock.Events.OfType<StanceSet>().Single(), Is.EqualTo(new StanceSet(
                    Actor.Player, "rock_stance", CardCatalog.RockStance.Name, CardCatalog.RockStance.Face.Stance!, Replaced: null)));
                Assert.That(rock.State.Player.Stance, Is.EqualTo(CardCatalog.RockStance.Face.Stance));
                Assert.That(rock.State.Player.StanceSource, Is.EqualTo("rock_stance"));
                Assert.That(rock.Events.OfType<CardExiled>().Single().Card.Def.Id, Is.EqualTo("rock_stance"));
                Assert.That(rock.State.Exiled.Select(c => c.Def.Id), Is.EqualTo(new[] { "rock_stance" }));
                Assert.That(rock.State.DiscardPile, Is.Empty);

                Assert.That(water.Events.OfType<StanceSet>().Single(), Is.EqualTo(new StanceSet(
                    Actor.Player, "water_stance", CardCatalog.WaterStance.Name, CardCatalog.WaterStance.Face.Stance!, Replaced: "rock_stance")));
                Assert.That(water.State.Player.Stance, Is.EqualTo(CardCatalog.WaterStance.Face.Stance));
                Assert.That(water.State.Player.StanceSource, Is.EqualTo("water_stance"));
                Assert.That(water.State.Exiled.Select(c => c.Def.Id), Is.EqualTo(new[] { "rock_stance", "water_stance" }));

                // The rest of the hand goes to the discard pile; the stance cards stay out of the deck.
                Assert.That(end.State.DiscardPile.Select(c => c.Def.Id), Is.All.EqualTo("filler"));
                Assert.That(end.State.DiscardPile, Has.Count.EqualTo(3));
                Assert.That(end.State.Exiled, Has.Count.EqualTo(2));
                Assert.That(next.State.Hand.Select(c => c.Def.Id), Is.All.EqualTo("filler"));
                Assert.That(next.State.Hand, Has.Count.EqualTo(3));
            });
        }

        [Test]
        public void RockStance_GivesThreeGuard_AtTheNextTurnStart_AfterTheGuardIsCleared()
        {
            // §4 ターン開始付与 / §9 steps 1-3: Guard goes to 0 first, then the stance's +3.
            var s = Opened(1, Idle, CardCatalog.RockStance);

            var end = End(Play(s, "rock_stance").State);
            var next = Begin(end.State);

            Assert.Multiple(() =>
            {
                Assert.That(end.State.Player.Guard, Is.EqualTo(3), "構え");
                Assert.That(next.Events.OfType<GuardCleared>().Single(), Is.EqualTo(new GuardCleared(Actor.Player, 3)));
                Assert.That(next.Events.OfType<StanceFired>().Single(), Is.EqualTo(new StanceFired(Actor.Player, "rock_stance", StanceHook.TurnStart)));
                Assert.That(next.Events.OfType<GuardGained>().Single(), Is.EqualTo(new GuardGained(Actor.Player, 3, 3)));
                AssertInOrder(next.Events, typeof(GuardCleared), typeof(StanceFired), typeof(GuardGained), typeof(Drawn));
                Assert.That(next.State.Player.Guard, Is.EqualTo(3));
            });
        }

        [TestCase(2, true, 5)]
        [TestCase(3, true, 5)]
        [TestCase(1, false, 3)]
        public void WaterStance_AddsTwoRecovery_OnlyWhenTheNearestEnemyIsTwoOrMoreAway(int gap, bool fires, int recovered)
        {
            // §4 when (いちばん近い敵との間合いが 2 以上のターン開始だけ): 水の構え adds +2 to step 2.
            var s = Opened(gap, Idle, CardCatalog.WaterStance);
            s = s with { Player = s.Player with { Stamina = 2 } };

            var next = Begin(End(Play(s, "water_stance").State).State);

            Assert.Multiple(() =>
            {
                Assert.That(next.Events.OfType<StaminaRecovered>().Single(), Is.EqualTo(new StaminaRecovered(Actor.Player, recovered, recovered, 10)));
                Assert.That(next.Events.OfType<StanceFired>().Any(e => e.Hook == StanceHook.TurnStart), Is.EqualTo(fires));
            });
        }

        [TestCase(1, true, 8)]
        [TestCase(0, false, 3)]
        public void RootStride_GivesFiveGuard_AtTurnEnd_WhenTheGapIsTwoOrMore_BeforeReserve(int startGap, bool fires, int guardAfterReserve)
        {
            // §4 (turn-end stance) / §9 step 7: 根渡り steps back one; at turn end +5 if N ≥ 2, then 構え.
            var s = Opened(startGap, Idle, CardCatalog.RootStride);

            var played = Play(s, "root_stride").State;
            var end = End(played);

            Assert.Multiple(() =>
            {
                Assert.That(played.Gap, Is.EqualTo(startGap + 1));
                Assert.That(end.Events.OfType<StanceFired>().Any(e => e.Actor == Actor.Player && e.Hook == StanceHook.TurnEnd), Is.EqualTo(fires));
                Assert.That(end.Events.OfType<ReserveChecked>().First(), Is.EqualTo(new ReserveChecked(Actor.Player, 7, 3, guardAfterReserve)));
                if (fires)
                {
                    Assert.That(end.Events.OfType<GuardGained>().First(), Is.EqualTo(new GuardGained(Actor.Player, 5, 5)));
                    AssertInOrder(end.Events, typeof(StanceFired), typeof(GuardGained), typeof(ReserveChecked), typeof(HandDiscarded));
                }
                else
                {
                    Assert.That(end.Events.OfType<GuardGained>().Where(e => e.Actor == Actor.Player), Is.Empty);
                }
            });
        }

        [Test]
        public void FlowStance_AddsFive_OnlyAfterAMoveCardThisTurn()
        {
            // §4 条件付き加算 (流れの構え: when = a move was played earlier this turn).
            var s = Opened(1, Idle, CardCatalog.FlowStance, Jab(10), StepIn, Jab(10));

            var flow = Play(s, "flow_stance");
            var still = Play(flow.State, "jab10");
            var stepped = Play(still.State, "step_in");
            var preview = TurnLoop.Preview(stepped.State, InHand(stepped.State, "jab10"))!;
            var moved = Play(stepped.State, "jab10");

            Assert.Multiple(() =>
            {
                Assert.That(still.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(10));
                Assert.That(still.Events.OfType<StanceFired>(), Is.Empty);
                Assert.That(preview.RawPower, Is.EqualTo(15));
                Assert.That(moved.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(15));
                Assert.That(moved.Events.OfType<StanceFired>().Single(), Is.EqualTo(new StanceFired(Actor.Player, "flow_stance", StanceHook.AttackBonus)));
            });
        }

        [Test]
        public void AbyssStance_AddsFive_OnlyAgainstATargetAtGapZero()
        {
            // §4 条件付き加算 (深淵の構え: the foe hit stands at N 0). The card's own blow lands before its stance is set.
            var s = Opened(1, Idle, CardCatalog.AbyssStance, Jab(10), StepIn, Jab(10));

            var abyss = Play(s, "abyss_stance");
            var far = Play(abyss.State, "jab10");
            var close = Play(Play(far.State, "step_in").State, "jab10");

            Assert.Multiple(() =>
            {
                Assert.That(abyss.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(14));
                Assert.That(far.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(10), "gap 1: no bonus");
                Assert.That(far.Events.OfType<StanceFired>(), Is.Empty);
                Assert.That(close.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(15), "gap 0: +5");
                Assert.That(close.Events.OfType<StanceFired>().Single().Hook, Is.EqualTo(StanceHook.AttackBonus));
            });
        }

        [Test]
        public void WolfStance_AddsThree_OnlyAgainstABleedingTarget()
        {
            // §4 条件付き加算 (狼の構え: the foe hit holds 出血). 裂き斬り's own blow comes before its bleed.
            var s = Opened(1, Idle, CardCatalog.WolfStance, Jab(10), CardCatalog.Rend, Jab(10));

            var wolf = Play(s, "wolf_stance");
            var dry = Play(wolf.State, "jab10");
            var rend = Play(dry.State, "rend");
            var bleeding = Play(rend.State, "jab10");

            Assert.Multiple(() =>
            {
                Assert.That(wolf.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(8));
                Assert.That(dry.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(10));
                Assert.That(rend.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(4));
                Assert.That(rend.Events.OfType<StatusApplied>().Single(), Is.EqualTo(new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Bleed, 1, 1, false)));
                Assert.That(bleeding.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(13));
                Assert.That(bleeding.Events.OfType<StanceFired>().Single(), Is.EqualTo(new StanceFired(Actor.Player, "wolf_stance", StanceHook.AttackBonus)));
            });
        }

        [Test]
        public void PriestPrayer_ReactsToTheFirstHitOfATurn_Only()
        {
            // §4 被弾時の反応 (司祭の祈り: stamina +1 and Guard +3, once a turn).
            var s = OpenedAgainstTwo(CardCatalog.PriestPrayer);

            var turn1 = End(Play(s, "priest_prayer").State);
            var turn2 = End(Begin(turn1.State).State);

            Assert.Multiple(() =>
            {
                Assert.That(turn1.Events.OfType<DamageDealt>().Select(d => d.Unit), Is.EqualTo(new[] { 0, 1 }), "both enemies hit");
                Assert.That(turn1.Events.OfType<StanceFired>().Single(),
                    Is.EqualTo(new StanceFired(Actor.Player, "priest_prayer", StanceHook.OnHit) { Unit = 0 }));
                Assert.That(turn1.Events.OfType<StaminaGained>().Single(), Is.EqualTo(new StaminaGained(Actor.Player, 1, 9) { Unit = 0 }));
                Assert.That(turn1.Events.OfType<GuardGained>().Where(e => e.Actor == Actor.Player).Single(),
                    Is.EqualTo(new GuardGained(Actor.Player, 3, 3) { Unit = 0 }));
                // 構え 3 soaks 3 of the 5 (2 through), the prayer's 3 soaks 3 of the 4 (1 through).
                Assert.That(turn1.State.Player.Hp, Is.EqualTo(47));
                Assert.That(turn1.State.Player.Stamina, Is.EqualTo(9));

                Assert.That(turn2.Events.OfType<StanceFired>().Select(e => e.Unit), Is.EqualTo(new[] { 0 }), "a new turn, one reaction again");
            });
        }

        [Test]
        public void SpearWall_BleedsEveryAttacker_OnEveryHit()
        {
            // §4 被弾時の反応 (槍衾: 出血 1 on the attacker for each hit taken).
            var s = OpenedAgainstTwo(CardCatalog.SpearWall);

            var end = End(Play(s, "spear_wall").State);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<StanceFired>().Select(e => e.Unit), Is.EqualTo(new[] { 0, 1 }));
                Assert.That(end.Events.OfType<StatusApplied>(), Is.EqualTo(new[]
                {
                    new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Bleed, 1, 1, false) { Unit = 0 },
                    new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Bleed, 1, 1, false) { Unit = 1 },
                }));
                Assert.That(end.State.Enemies.Select(e => e.Body.Statuses.Stacks(StatusKind.Bleed)), Is.EqualTo(new[] { 1, 1 }));
            });
        }

        [Test]
        public void AnchorStance_RefusesThePolearmsShove()
        {
            // §4 (錨の構え) / §7.3: push and pull no longer move the holder, so there is no wall either.
            var s = Opened(0, Enemies.PolearmWarped, CardCatalog.AnchorStance);
            Assert.That(s.Omen!.ActionId, Is.EqualTo("shove"));

            var end = End(Play(s, "anchor_stance").State);

            Assert.Multiple(() =>
            {
                // 錨 3 + 構え 3 = 6: the shove loses its 無防備 +3 and the Guard soaks its 5.
                Assert.That(end.Events.OfType<DamageDealt>().Single(), Is.EqualTo(new DamageDealt(Actor.Enemy, Actor.Player, 5, 5, 0, 1, 50)));
                Assert.That(end.Events.OfType<StanceFired>().Single(), Is.EqualTo(new StanceFired(Actor.Player, "anchor_stance", StanceHook.PushImmune)));
                Assert.That(end.Events.OfType<PushRefused>().Single(), Is.EqualTo(new PushRefused(Actor.Player, 1) { ByStance = true }));
                Assert.That(end.Events.OfType<CellsMoved>(), Is.Empty);
                Assert.That(end.Events.OfType<WallHit>(), Is.Empty);
                Assert.That(end.State.Player.Cell, Is.EqualTo(2));
                Assert.That(end.State.Gap, Is.EqualTo(0));
            });
        }

        [Test]
        public void RootBind_BreaksOneStamina_WhenAnEnemyMovesItself()
        {
            // §4 (根縛り) : the polearm's own 踏み込み costs it 1 stamina; the player's own move and a whiff do not.
            var s = Opened(2, Enemies.PolearmWarped, CardCatalog.RootBind, StepOut);

            var bound = Play(s, "root_bind");
            var backed = Play(bound.State, "step_out");
            var turn1 = End(backed.State);
            var turn2 = End(Begin(turn1.State).State);

            Assert.Multiple(() =>
            {
                Assert.That(bound.Events.OfType<StanceSet>().Single().SourceId, Is.EqualTo("root_bind"));
                Assert.That(bound.Events.OfType<StatusApplied>().Single(), Is.EqualTo(new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Slow, 2, 2, false)));
                Assert.That(backed.State.Gap, Is.EqualTo(3));
                Assert.That(backed.Events.OfType<StaminaBroken>(), Is.Empty, "the player's own move does not count");

                // Turn 1: the sweep declared at gap 2 whiffs at gap 3; nobody moved. From gap 3 the next omen is 踏み込み.
                Assert.That(turn1.Events.OfType<ActionWhiffed>().Single().SourceId, Is.EqualTo("sweep"));
                Assert.That(turn1.Events.OfType<StaminaBroken>(), Is.Empty);
                Assert.That(turn1.State.Omen!.ActionId, Is.EqualTo("step_forward"));

                // Turn 2: 鈍足 has worn off, the polearm steps in 5 → 4 and the bind takes 1: 8 + 2 − 1 (cost) − 1 = 8.
                Assert.That(turn2.Events.OfType<StatusTicked>().Single(), Is.EqualTo(new StatusTicked(Actor.Enemy, StatusKind.Slow, 0)));
                Assert.That(turn2.Events.OfType<CellsMoved>().Single(), Is.EqualTo(new CellsMoved(Actor.Enemy, 5, 4, Pushed: false)));
                Assert.That(turn2.Events.OfType<StanceFired>().Single(), Is.EqualTo(new StanceFired(Actor.Player, "root_bind", StanceHook.BreakOnFoeMove)));
                Assert.That(turn2.Events.OfType<StaminaBroken>().Single(), Is.EqualTo(new StaminaBroken(Actor.Player, Actor.Enemy, 1, 8)));
                AssertInOrder(turn2.Events, typeof(CellsMoved), typeof(StanceFired), typeof(StaminaBroken));
                Assert.That(turn2.State.Enemy.Stamina, Is.EqualTo(8));
            });
        }

        [Test]
        public void TheNextOmen_CountsTheFatigueTheEnemyAlreadyHolds()
        {
            // §9 step 12 / #70: the omen is judged on the stamina the enemy will hold when it acts, and
            // 疲労 already on it takes 1 off that recovery. The polearm at gap 1 holds 1 + 疲労 2: it
            // recovers 1 (2 − 1), sweeps for 2 and is left with 0 and 疲労 1. Next time it recovers only
            // 1, so the omen must be the cost-1 thrust, not a sweep it could not pay for.
            var s = Opened(1, Enemies.PolearmWarped, Filler);
            s = s.WithEnemy(s.Enemy with { Stamina = 1, Statuses = StatusSet.Of((StatusKind.Fatigue, 2)) });
            var end = End(s);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<ActionExecuted>().Single().Action.Id, Is.EqualTo("sweep"));
                Assert.That(end.State.Enemy.Stamina, Is.EqualTo(0));
                Assert.That(end.State.Enemy.Statuses.Stacks(StatusKind.Fatigue), Is.EqualTo(1));
                Assert.That(end.State.Omen!.ActionId, Is.EqualTo("reach_thrust"));
            });
        }
    }
}
