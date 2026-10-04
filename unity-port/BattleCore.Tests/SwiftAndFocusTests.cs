using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// #48: the two status words the demo (#188) left open — 俊敏 (§5, §9 step 3), now in the core,
    /// and 集中 (§5), now read as "the next card resolves one column to the right" down to the
    /// stacks of the 出血 / 再生 it gives.
    ///
    /// Every scenario runs through the real TurnLoop on an 8-cell line with the player on cell 2,
    /// the deck dealt in order (FixedRng(0.999…) makes the shuffle a no-op), so a start gap of n
    /// puts the first enemy on cell 3 + n.
    /// </summary>
    public class SwiftAndFocusTests
    {
        private const int Cells = 8;

        private static readonly IRng NoRng = new FixedRng(0.9999999);

        // ---- Fixtures ----

        private static readonly EnemyActionDef Wait =
            Fixtures.EnemyAction("wait", face: new Face(), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        /// <summary>An enemy that never touches the player and whose omen aims at nobody.</summary>
        private static readonly EnemyDef Idle = Fixtures.Enemy("idle", atZero: Wait, atOneToTwo: Wait, atThreePlus: Wait);

        /// <summary>At gap 1〜2 it declares a blow that reaches gap 0 only.</summary>
        private static readonly EnemyDef Lunger = Fixtures.Enemy(
            "lunger", atOneToTwo: Fixtures.EnemyAction("lunge", face: new Face(Power: 5, Reach: Reach.Only(0))));

        /// <summary>At gap 1〜2 it declares a thrust that reaches gap 2〜3 only.</summary>
        private static readonly EnemyDef Spearman = Fixtures.Enemy(
            "spearman", atOneToTwo: Fixtures.EnemyAction("long_thrust", face: new Face(Power: 4, Reach: new Reach(2, 3))));

        private static readonly CardDef Filler =
            Fixtures.Card("filler", 1, new Face(Guard: 1), BattleAttribute.Guard, targets: TargetKind.Self);

        // ---- Helpers ----

        private static List<CardInstance> Deck(IReadOnlyList<CardDef> cards)
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < cards.Count; i++) deck.Add(new CardInstance(cards[i].Id + "-" + i, cards[i]));
            for (int i = deck.Count; i < Constants.HandDraw; i++) deck.Add(new CardInstance(Filler.Id + "-" + i, Filler));
            return deck;
        }

        private static BattleState Started(int gap, EnemyDef enemy, params CardDef[] cards) =>
            TurnLoop.Start(new BattleSetup(enemy, Deck(cards), Cells, StartGap: gap), NoRng).State;

        private static BattleState Opened(int gap, EnemyDef enemy, params CardDef[] cards) =>
            TurnLoop.BeginPlayerTurn(Started(gap, enemy, cards), NoRng).State;

        private static string InHand(BattleState s, string cardId) => s.Hand.First(c => c.Def.Id == cardId).InstanceId;

        private static StepResult Play(BattleState s, string cardId) => TurnLoop.PlayCard(s, InHand(s, cardId), NoRng);

        private static StepResult Begin(BattleState s) => TurnLoop.BeginPlayerTurn(s, NoRng);

        private static StepResult End(BattleState s) => TurnLoop.EndTurn(s, NoRng);

        private static BattleState WithPlayerStatuses(BattleState s, params (StatusKind Kind, int Stacks)[] entries) =>
            s with { Player = s.Player with { Statuses = StatusSet.Of(entries) } };

        private static BattleState WithEnemyStatuses(BattleState s, params (StatusKind Kind, int Stacks)[] entries) =>
            s.WithEnemy(s.Enemy with { Statuses = StatusSet.Of(entries) });

        private static int IndexOf<T>(IReadOnlyList<BattleEvent> events) where T : BattleEvent
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] is T) return i;
            }
            return -1;
        }

        // ---- §5 俊敏: the player ----

        [Test]
        public void Swift_OpensOneFreeCell_AtTheTurnStart_ForwardOrBack()
        {
            // §5 俊敏 / §9 step 3: held at the turn start, so one free cell, either way, at no stamina.
            var t1 = Begin(WithPlayerStatuses(Started(2, Idle), (StatusKind.Swift, 2)));

            var forward = TurnLoop.TakeFreeStep(t1.State, 1);
            var back = TurnLoop.TakeFreeStep(t1.State, -1);

            Assert.Multiple(() =>
            {
                Assert.That(TurnLoop.CanTakeFreeStep(t1.State), Is.True);
                Assert.That(forward.Events, Is.EqualTo(new BattleEvent[]
                {
                    new FreeStepTaken(Actor.Player, 1),
                    new CellsMoved(Actor.Player, 2, 3, Pushed: false),
                }));
                Assert.That(forward.State.Player.Cell, Is.EqualTo(3));
                Assert.That(forward.State.Player.Stamina, Is.EqualTo(t1.State.Player.Stamina), "the cell is free");
                Assert.That(forward.State.Player.Moved, Is.True, "§2.3 移動後 counts the free cell");
                Assert.That(back.State.Player.Cell, Is.EqualTo(1));

                Assert.That(TurnLoop.CanTakeFreeStep(forward.State), Is.False, "one cell a turn");
                Assert.Throws<InvalidOperationException>(() => TurnLoop.TakeFreeStep(forward.State, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => TurnLoop.TakeFreeStep(t1.State, 2));
            });
        }

        [Test]
        public void Swift_WithoutTheWord_OpensNoFreeCell()
        {
            var t1 = Opened(2, Idle);

            Assert.Multiple(() =>
            {
                Assert.That(TurnLoop.CanTakeFreeStep(t1), Is.False);
                Assert.Throws<InvalidOperationException>(() => TurnLoop.TakeFreeStep(t1, 1));
            });
        }

        [Test]
        public void Swift_LosesOneStackPerTurn_AndGivesOneTurnPerStack()
        {
            // §5 ターンで減る型: read before the tick, as 再生 / 出血 are, so 2 stacks are two turns of the free cell.
            var t1 = Begin(WithPlayerStatuses(Started(2, Idle), (StatusKind.Swift, 2)));
            var t2 = Begin(End(t1.State).State);
            var t3 = Begin(End(t2.State).State);

            Assert.Multiple(() =>
            {
                Assert.That(t1.Events.OfType<StatusTicked>().Single(), Is.EqualTo(new StatusTicked(Actor.Player, StatusKind.Swift, 1)));
                Assert.That(TurnLoop.CanTakeFreeStep(t1.State), Is.True);

                Assert.That(t2.Events.OfType<StatusTicked>().Single(), Is.EqualTo(new StatusTicked(Actor.Player, StatusKind.Swift, 0)));
                Assert.That(t2.State.Player.Statuses.Has(StatusKind.Swift), Is.False);
                Assert.That(TurnLoop.CanTakeFreeStep(t2.State), Is.True, "the last stack still opens its turn");

                Assert.That(t3.Events.OfType<StatusTicked>(), Is.Empty);
                Assert.That(TurnLoop.CanTakeFreeStep(t3.State), Is.False);
            });
        }

        [Test]
        public void Swift_StopsAtFourStacks()
        {
            // §5 上限 (#205): a ターンで減る型 word holds at most four; 3 more on 3 lands 1 and drops 2.
            var quicken = Fixtures.Card(
                "quicken", 1, new Face(Statuses: new[] { new StatusGrant(StatusKind.Swift, 3, OnSelf: true) }),
                BattleAttribute.Skill, targets: TargetKind.Self);
            var s = WithPlayerStatuses(Opened(2, Idle, quicken), (StatusKind.Swift, 3));

            var play = Play(s, "quicken");
            var next = Begin(End(play.State).State);

            Assert.Multiple(() =>
            {
                Assert.That(Statuses.DecayOf(StatusKind.Swift), Is.EqualTo(StatusDecay.OnTurn));
                Assert.That(StatusSet.Empty.Add(StatusKind.Swift, 9).Stacks(StatusKind.Swift), Is.EqualTo(Constants.TurnDecayStackMax));

                var applied = play.Events.OfType<StatusApplied>().Single();
                Assert.That(applied, Is.EqualTo(new StatusApplied(Actor.Player, Actor.Player, StatusKind.Swift, 3, 4, Refused: false) { Dropped = 2 }));
                Assert.That(play.State.Player.Statuses.Stacks(StatusKind.Swift), Is.EqualTo(4));
                Assert.That(next.Events.OfType<StatusTicked>().Single(), Is.EqualTo(new StatusTicked(Actor.Player, StatusKind.Swift, 3)));
            });
        }

        [Test]
        public void Swift_PlayingACard_OrEndingTheTurn_LetsTheFreeCellGo()
        {
            // §5 俊敏 (#48): the free cell is taken before the first card or not at all.
            var t1 = Begin(WithPlayerStatuses(Started(2, Idle), (StatusKind.Swift, 2)));

            var played = Play(t1.State, "filler");
            var ended = End(t1.State);

            Assert.Multiple(() =>
            {
                Assert.That(TurnLoop.CanTakeFreeStep(played.State), Is.False);
                Assert.Throws<InvalidOperationException>(() => TurnLoop.TakeFreeStep(played.State, 1));
                Assert.That(ended.State.Player.FreeStep, Is.False);
                Assert.That(ended.State.Player.Cell, Is.EqualTo(2), "letting it go moves nothing");
            });
        }

        [Test]
        public void Swift_UnderSlow_IsSpentWithoutMoving()
        {
            // §5 鈍足: the holder's own cell changes are one shorter, and the free cell is one of them.
            var t1 = Begin(WithPlayerStatuses(Started(2, Idle), (StatusKind.Swift, 2), (StatusKind.Slow, 2)));

            var step = TurnLoop.TakeFreeStep(t1.State, 1);

            Assert.Multiple(() =>
            {
                Assert.That(step.Events, Is.EqualTo(new BattleEvent[]
                {
                    new FreeStepTaken(Actor.Player, 1),
                    new MoveBlocked(Actor.Player, StatusKind.Slow),
                }));
                Assert.That(step.State.Player.Cell, Is.EqualTo(2));
                Assert.That(step.State.Player.Moved, Is.False);
                Assert.That(TurnLoop.CanTakeFreeStep(step.State), Is.False);
            });
        }

        [Test]
        public void Swift_TheFreeCell_CountsAsMoved_ForTheCardAfterIt()
        {
            // §2.3 移動後: "自分の移動の効果と、俊敏の無料の 1 マスが数えます".
            var afterStep = Fixtures.Card(
                "after_step", 1, new Face(Power: 6), trait: new Trait(TraitCondition.Moved, TraitEffect.PowerBonus, Amount: 3));
            var t1 = TurnLoop.BeginPlayerTurn(WithPlayerStatuses(Started(1, Idle, afterStep), (StatusKind.Swift, 1)), NoRng);

            var step = TurnLoop.TakeFreeStep(t1.State, 1);
            var play = Play(step.State, "after_step");

            Assert.That(play.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(9));
        }

        // ---- §5 俊敏: an enemy ----

        [TestCase(3, 0, 1, 1, TestName = "FreeStepDirection_Forward_WhenThePlayerIsBeyondTheReach")]
        [TestCase(0, 2, 3, -1, TestName = "FreeStepDirection_Back_WhenThePlayerIsInsideTheReach")]
        [TestCase(2, 1, 2, 0, TestName = "FreeStepDirection_Stays_WhenTheOmenAlreadyReaches")]
        public void FreeStepDirection_ServesTheOmensReach(int gap, int min, int max, int expected)
        {
            var action = Fixtures.EnemyAction("blow", face: new Face(Power: 5, Reach: new Reach(min, max)));
            Assert.That(EnemyAi.FreeStepDirection(action, gap), Is.EqualTo(expected));
        }

        [Test]
        public void FreeStepDirection_Stays_ForAnOmenAimedAtNobody_AndForARest()
        {
            Assert.Multiple(() =>
            {
                Assert.That(EnemyAi.FreeStepDirection(Wait, 5), Is.EqualTo(0));
                Assert.That(EnemyAi.FreeStepDirection(null, 5), Is.EqualTo(0));
            });
        }

        [Test]
        public void AnEnemy_WithSwift_StepsForward_IntoItsOmensReach()
        {
            // §5 俊敏 / §9 step 9: the lunge reaches gap 0 only; at gap 1 the free cell brings it in.
            var opened = Opened(1, Lunger);
            var swift = WithEnemyStatuses(opened, (StatusKind.Swift, 1));

            var plain = End(opened);
            var stepped = End(swift);

            Assert.Multiple(() =>
            {
                Assert.That(plain.Events.OfType<ActionWhiffed>(), Has.Exactly(1).Items, "without 俊敏 it whiffs");

                Assert.That(stepped.Events.OfType<FreeStepTaken>().Single(), Is.EqualTo(new FreeStepTaken(Actor.Enemy, 1)));
                Assert.That(stepped.Events.OfType<CellsMoved>().First(), Is.EqualTo(new CellsMoved(Actor.Enemy, 4, 3, Pushed: false)));
                Assert.That(stepped.Events.OfType<ActionWhiffed>(), Is.Empty);
                Assert.That(stepped.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(5));
                Assert.That(IndexOf<FreeStepTaken>(stepped.Events), Is.LessThan(IndexOf<ActionExecuted>(stepped.Events)), "turn start, before the action");
                Assert.That(IndexOf<StatusTicked>(stepped.Events), Is.LessThan(IndexOf<FreeStepTaken>(stepped.Events)));

                Assert.That(TurnLoop.PreviewOmen(opened, 0)!.Lands, Is.False);
                Assert.That(TurnLoop.PreviewOmen(swift, 0)!.Lands, Is.True, "#248: the preview plays the enemy's turn start, free cell included");
            });
        }

        [Test]
        public void AnEnemy_WithSwift_StepsBack_WhenThePlayerIsTooClose()
        {
            // The long thrust reaches gap 2〜3 only; at gap 1 the free cell takes the enemy back one.
            var s = WithEnemyStatuses(Opened(1, Spearman), (StatusKind.Swift, 1));

            var end = End(s);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<FreeStepTaken>().Single(), Is.EqualTo(new FreeStepTaken(Actor.Enemy, -1)));
                Assert.That(end.Events.OfType<CellsMoved>().First(), Is.EqualTo(new CellsMoved(Actor.Enemy, 4, 5, Pushed: false)));
                Assert.That(end.Events.OfType<ActionWhiffed>(), Is.Empty);
                Assert.That(end.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(4));
            });
        }

        [Test]
        public void AnEnemy_WithSwift_Stays_WhenItsOmenAlreadyReaches_OrAimsAtNobody()
        {
            // The fixture's mid_hit reaches 1〜2, so at gap 1 it has no reason to move; Idle's omen aims at nobody.
            var inReach = End(WithEnemyStatuses(Opened(1, Fixtures.Enemy()), (StatusKind.Swift, 2)));
            var idle = End(WithEnemyStatuses(Opened(1, Idle), (StatusKind.Swift, 2)));

            Assert.Multiple(() =>
            {
                Assert.That(inReach.Events.OfType<FreeStepTaken>(), Is.Empty);
                Assert.That(inReach.Events.OfType<StatusTicked>().Single(), Is.EqualTo(new StatusTicked(Actor.Enemy, StatusKind.Swift, 1)));
                Assert.That(idle.Events.OfType<FreeStepTaken>(), Is.Empty);
                Assert.That(idle.Events.OfType<CellsMoved>(), Is.Empty);
            });
        }

        // ---- §5 集中: one column to the right, statuses included ----

        [Test]
        public void Focus_TakesTheBleedStacks_FromTheRightColumn_WithThePower()
        {
            // §5 集中 / §3.1: a column-2 attack (13, 出血 2) resolves as column 3: 21 and 出血 3.
            var cut = Fixtures.Card("cut", 2, new Face(Power: 13, Statuses: new[] { new StatusGrant(StatusKind.Bleed, 2) }));
            var s = WithPlayerStatuses(Opened(1, Idle, cut), (StatusKind.Focus, 1));

            var play = Play(s, "cut");

            Assert.Multiple(() =>
            {
                Assert.That(FocusStep.Of(BattleAttribute.Attack, 2, cut.Face), Is.EqualTo(new FocusStep(8, 0, 0, 1)));
                Assert.That(play.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(21));
                Assert.That(play.Events.OfType<StatusApplied>().Single(), Is.EqualTo(new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Bleed, 3, 3, Refused: false)));
                Assert.That(play.State.Enemy.Statuses.Stacks(StatusKind.Bleed), Is.EqualTo(3));
            });
        }

        [Test]
        public void Focus_TakesTheRegenStacks_AndTheHeal_FromTheRightColumn()
        {
            // §5 集中 / §3.1: a column-3 heal (13, 再生 3 on self) resolves as column 4: 19 and 再生 4.
            var mend = Fixtures.Card(
                "mend", 3, new Face(Heal: 13, Statuses: new[] { new StatusGrant(StatusKind.Regen, 3, OnSelf: true) }),
                BattleAttribute.Skill, targets: TargetKind.Self);
            var s = Opened(1, Idle, mend);
            s = s with { Player = s.Player with { Hp = 20, Statuses = StatusSet.Of((StatusKind.Focus, 1)) } };

            var play = Play(s, "mend");

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<Healed>().Single(), Is.EqualTo(new Healed(Actor.Player, 19, 39)));
                Assert.That(play.Events.OfType<StatusApplied>().Single(), Is.EqualTo(new StatusApplied(Actor.Player, Actor.Player, StatusKind.Regen, 4, 4, Refused: false)));
            });
        }

        [Test]
        public void Focus_AtColumnFour_LeavesTheStacksAsTheyAre()
        {
            // §5 集中 (列 4 は超えない).
            var deep = Fixtures.Card("deep_cut", 4, new Face(Power: 30, Statuses: new[] { new StatusGrant(StatusKind.Bleed, 4) }));
            var s = WithPlayerStatuses(Opened(1, Idle, deep), (StatusKind.Focus, 1));

            var play = Play(s, "deep_cut");

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(30));
                Assert.That(play.State.Enemy.Statuses.Stacks(StatusKind.Bleed), Is.EqualTo(4));
            });
        }

        [Test]
        public void Focus_LeavesTheOtherWords_TheBreak_AndTheTraitsGrant_AsTheyAre()
        {
            // §3.1: the other words are 2 stacks in every column and 崩し is not on the scale; a trait
            // belongs to the card, not to its column. Only the face's 出血 grows.
            var card = Fixtures.Card(
                "rend", 2,
                new Face(Power: 13, Break: 1, Statuses: new[] { new StatusGrant(StatusKind.Bleed, 2), new StatusGrant(StatusKind.Fragile, 2) }),
                trait: new Trait(TraitCondition.FirstPlay, TraitEffect.Status, Grant: new StatusGrant(StatusKind.Regen, 1, OnSelf: true)));
            var s = WithPlayerStatuses(Opened(1, Idle, card), (StatusKind.Focus, 1));

            var play = Play(s, "rend");

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<StatusApplied>(), Is.EqualTo(new[]
                {
                    new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Bleed, 3, 3, Refused: false),
                    new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Fragile, 2, 2, Refused: false),
                    new StatusApplied(Actor.Player, Actor.Player, StatusKind.Regen, 1, 1, Refused: false),
                }));
                Assert.That(play.Events.OfType<StaminaBroken>().Single().Amount, Is.EqualTo(1));
            });
        }

        [Test]
        public void WithoutFocus_TheStacksAreTheCardsOwn()
        {
            var cut = Fixtures.Card("cut", 2, new Face(Power: 13, Statuses: new[] { new StatusGrant(StatusKind.Bleed, 2) }));

            var play = Play(Opened(1, Idle, cut), "cut");

            Assert.That(play.State.Enemy.Statuses.Stacks(StatusKind.Bleed), Is.EqualTo(2));
        }
    }
}
