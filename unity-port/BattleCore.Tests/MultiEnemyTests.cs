using System;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// §7.1 / §7.4 (#47): two and three enemies on one line — one per cell, each with its own cells,
    /// omen and phase. The prototype data has one enemy, so these use fixtures: every enemy is
    /// Fixtures.Enemy (a 5-power hit at 0〜1, a 4-power hit at 1〜2, a one-cell step at 3+).
    /// </summary>
    public class MultiEnemyTests
    {
        private static readonly IRng NoRng = new FixedRng(0.9999999);

        private static EnemyDef E(string id, int size = 1) => Fixtures.Enemy(id, size);

        /// <summary>A battle past its first turn start, the deck dealt in order.</summary>
        private static BattleState Battle(int fieldCells, int startGap, EnemyDef[] enemies, params CardDef[] hand)
        {
            var deck = Cards.BuildDeck(hand.Length == 0 ? new[] { Fixtures.Card("filler") } : hand, copies: 1);
            var setup = new BattleSetup(enemies[0], deck, fieldCells, StartGap: startGap, MoreEnemies: enemies.Skip(1).ToArray());
            var s = TurnLoop.Start(setup, NoRng).State;
            return TurnLoop.BeginPlayerTurn(s, NoRng).State;
        }

        /// <summary>Moves the enemies to the given near-edge cells, in unit order.</summary>
        private static BattleState Place(BattleState s, params int[] cells)
        {
            for (int i = 0; i < cells.Length; i++) s = s.WithEnemy(i, s.Enemies[i].Body with { Cell = cells[i] });
            return s;
        }

        private static string InHand(BattleState s, string id) => s.Hand.First(c => c.Def.Id == id).InstanceId;

        private static readonly CardDef Jab = Fixtures.Card("jab", face: new Face(Power: 5, Reach: Reach.Only(0)));

        // ---- Start (§7.4 2 体目からの開始のマス) ----

        [Test]
        public void EachEnemy_StartsRightBehindTheOneBefore()
        {
            Assert.Multiple(() =>
            {
                var three = TurnLoop.Start(new BattleSetup(E("a"), Fixtures.TwentyCardDeck(), 8, MoreEnemies: new[] { E("b"), E("c") }), NoRng);
                Assert.That(three.State.Enemies.Select(u => u.Body.Cell), Is.EqualTo(new[] { 6, 7, 8 }));
                Assert.That(Enumerable.Range(0, 3).Select(three.State.GapTo), Is.EqualTo(new[] { 3, 4, 5 }));
                Assert.That(three.Events.OfType<OmenSet>().Select(o => o.Unit), Is.EqualTo(new[] { 0, 1, 2 }), "every enemy decides an omen");

                var close = TurnLoop.Start(new BattleSetup(E("a"), Fixtures.TwentyCardDeck(), 6, StartGap: 1, MoreEnemies: new[] { E("b"), E("c") }), NoRng);
                Assert.That(close.State.Enemies.Select(u => u.Body.Cell), Is.EqualTo(new[] { 4, 5, 6 }));

                var large = TurnLoop.Start(new BattleSetup(E("a"), Fixtures.TwentyCardDeck(), 8, MoreEnemies: new[] { E("b", size: 2) }), NoRng);
                Assert.That(large.State.Enemies.Select(u => u.Body.Cell), Is.EqualTo(new[] { 6, 7 }), "the large one uses 7〜8");
            });
        }

        [Test]
        public void ALineTooShortForEveryone_OrAFourthEnemy_IsRefused()
        {
            var deck = Fixtures.TwentyCardDeck();
            Assert.Multiple(() =>
            {
                Assert.That(() => TurnLoop.Start(new BattleSetup(E("a"), deck, 6, MoreEnemies: new[] { E("b") }), NoRng),
                    Throws.InstanceOf<ArgumentOutOfRangeException>(), "gap 3 puts the first on 6 and the second off the line");
                Assert.That(() => TurnLoop.Start(new BattleSetup(E("a"), deck, 8, StartGap: 0, MoreEnemies: new[] { E("b"), E("c"), E("d") }), NoRng),
                    Throws.InstanceOf<ArgumentOutOfRangeException>(), "ENEMIES_MAX 3");
                Assert.That(() => Field.Validate(8, 2, 1, new[] { (4, 2), (5, 1) }), Throws.ArgumentException, "CELL_CAPACITY 1: no two enemies on one cell");
            });
        }

        [Test]
        public void EachEnemy_DecidesFromItsOwnGap()
        {
            var s = Battle(8, 0, new[] { E("a"), E("b"), E("c") });   // cells 3, 4, 5: gaps 0, 1, 2
            Assert.That(s.Enemies.Select(u => u.Omen!.ActionId), Is.EqualTo(new[] { "near_hit", "mid_hit", "mid_hit" }));
        }

        // ---- Whom a card aims at ----

        [Test]
        public void TheFrontEnemy_CoversTheOneBehind_ByReachAlone()
        {
            // §7.4: adjacent to a (N 0), b behind it is at N 1, and a card that reaches 0 only cannot get there.
            var s = Battle(8, 0, new[] { E("a"), E("b") }, Jab);
            string jab = InHand(s, "jab");
            Assert.Multiple(() =>
            {
                Assert.That(TurnLoop.CanPlay(s, jab, target: 0), Is.EqualTo(PlayRefusal.None));
                Assert.That(TurnLoop.CanPlay(s, jab, target: 1), Is.EqualTo(PlayRefusal.OutOfReach));
                Assert.That(TurnLoop.CanPlay(s, jab, target: 2), Is.EqualTo(PlayRefusal.NoSuchTarget));
                Assert.That(TurnLoop.CanPlay(s, jab, target: -1), Is.EqualTo(PlayRefusal.NoSuchTarget));
            });

            var play = TurnLoop.PlayCard(s, jab, NoRng, target: 0);
            var hit = play.Events.OfType<DamageDealt>().Single();
            Assert.That(hit.Unit, Is.EqualTo(0));
            Assert.That(play.State.Enemies[0].Body.Hp, Is.EqualTo(55));
            Assert.That(play.State.Enemies[1].Body.Hp, Is.EqualTo(60));
            Assert.That(play.Events.OfType<CardPlayed>().Single().Unit, Is.EqualTo(0));
        }

        [Test]
        public void ACardAimedAtAll_HitsEveryoneInReach_AndNobodyElse()
        {
            var sweep = Fixtures.Card("sweep", face: new Face(Power: 5, Reach: new Reach(0, 1)), targets: TargetKind.All);
            var s = Battle(8, 0, new[] { E("a"), E("b"), E("c") }, sweep);   // gaps 0, 1, 2

            var play = TurnLoop.PlayCard(s, InHand(s, "sweep"), NoRng);

            Assert.That(play.Events.OfType<DamageDealt>().Select(d => d.Unit), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(play.State.Enemies.Select(u => u.Body.Hp), Is.EqualTo(new[] { 55, 55, 60 }));

            var far = Battle(8, 3, new[] { E("a"), E("b") }, sweep);   // gaps 3, 4
            Assert.That(TurnLoop.CanPlay(far, InHand(far, "sweep")), Is.EqualTo(PlayRefusal.OutOfReach));
        }

        [Test]
        public void ASelfCard_ReadsTheNearestEnemy_NotTheFirstInOrder()
        {
            // §7.4 自分向きの札: b (unit 1) has walked in front of a.
            var brace = Fixtures.Card("brace", face: new Face(Guard: 2), attributes: BattleAttribute.Guard,
                trait: new Trait(TraitCondition.GapAtMost, TraitEffect.GuardBonus, 3, Threshold: 0), targets: TargetKind.Self);
            var s = Place(Battle(8, 3, new[] { E("a"), E("b") }, brace), 6, 3);
            Assert.That(s.Nearest, Is.EqualTo(1));

            var play = TurnLoop.PlayCard(s, InHand(s, "brace"), NoRng);

            Assert.That(play.Events.OfType<CardPlayed>().Single(), Is.EqualTo(new CardPlayed(Actor.Player, s.Hand.First(c => c.Def.Id == "brace"), 0) { Unit = 1 }));
            Assert.That(play.Events.OfType<GuardGained>().Single().Amount, Is.EqualTo(5));
        }

        [Test]
        public void ThePreview_ReadsTheEnemyTheCardIsHeldOver()
        {
            var cut = Fixtures.Card("cut", face: new Face(Power: 6, Reach: new Reach(0, 1)));
            var s = Battle(8, 0, new[] { E("a"), E("b") }, cut);
            s = s.WithEnemy(1, s.Enemies[1].Body with { Guard = 3 });
            string id = InHand(s, "cut");

            Assert.That(TurnLoop.Preview(s, id, target: 0)!.Damage, Is.EqualTo(6));
            Assert.That(TurnLoop.Preview(s, id, target: 1)!.Damage, Is.EqualTo(3));
            Assert.That(TurnLoop.Preview(s, id, target: 1)!.InReach, Is.True);
            Assert.That(TurnLoop.Preview(s, id, target: 4)!.InReach, Is.False, "nobody stands there");
        }

        // ---- The line with several enemies (§7.1) ----

        [Test]
        public void ThePlayer_StopsShortOfTheNearestEnemy()
        {
            var s = Place(Battle(8, 3, new[] { E("a"), E("b") }), 6, 4);
            Assert.That(Field.Move(s, Actor.Player, 3), Is.EqualTo(new Shift(2, 3, 2)));
        }

        [Test]
        public void Enemies_PassEachOther_WhenTheCellTheyLandOnIsFree()
        {
            var s = Place(Battle(8, 3, new[] { E("a"), E("b") }), 4, 5);   // player 2, a 4, b 5
            Assert.Multiple(() =>
            {
                Assert.That(Field.Move(s, Actor.Enemy, 2, unit: 1), Is.EqualTo(new Shift(5, 3, 0)), "b steps past a onto 3");
                Assert.That(Field.Move(s, Actor.Enemy, 1, unit: 1), Is.EqualTo(new Shift(5, 5, 1)), "4 is a's: b cannot land there");
                Assert.That(Field.Move(s, Actor.Enemy, -1, unit: 0), Is.EqualTo(new Shift(4, 4, 1)), "5 is b's");
                Assert.That(Field.Move(s, Actor.Enemy, -2, unit: 0), Is.EqualTo(new Shift(4, 6, 0)), "a steps past b onto 6");
                Assert.That(Field.Move(s, Actor.Enemy, 2, unit: 0), Is.EqualTo(new Shift(4, 3, 1)), "and nobody passes the player");
            });

            var aDown = s.WithEnemy(0, s.Enemies[0].Body with { Hp = 0 });
            Assert.That(Field.Move(aDown, Actor.Enemy, 1, unit: 1), Is.EqualTo(new Shift(5, 4, 0)), "a fallen enemy holds no cells");
        }

        [Test]
        public void ALargeEnemy_KeepsItsCellsToItself()
        {
            var s = Place(Battle(8, 3, new[] { E("a"), E("b", size: 2) }), 4, 5);   // a 4, b 5〜6
            Assert.That(Field.Move(s, Actor.Enemy, -2, unit: 0), Is.EqualTo(new Shift(4, 4, 2)), "5 and 6 are b's");
            Assert.That(Field.Move(s, Actor.Enemy, -3, unit: 0), Is.EqualTo(new Shift(4, 7, 0)), "but a free cell past it can be landed on");
        }

        [Test]
        public void PushedIntoANeighbour_TheWallIsThatNeighbour()
        {
            // §7.3 入れないマス: a is pushed two cells toward b's cell and stops one short.
            var shove = Fixtures.Card("shove", face: new Face(Push: 2, Reach: new Reach(0, 2)),
                attributes: BattleAttribute.Attack | BattleAttribute.Move);
            var s = Place(Battle(8, 3, new[] { E("a"), E("b") }, shove), 3, 5);

            var play = TurnLoop.PlayCard(s, InHand(s, "shove"), NoRng, target: 0);

            Assert.That(play.Events.OfType<CellsMoved>().Single(), Is.EqualTo(new CellsMoved(Actor.Enemy, 3, 4, Pushed: true) { Unit = 0 }));
            Assert.That(play.Events.OfType<WallHit>().Single(), Is.EqualTo(new WallHit(Actor.Enemy, 1, 3, 0, 3, 0, 57) { Unit = 0 }));
            Assert.That(play.State.Enemies[1].Body.Cell, Is.EqualTo(5));
        }

        // ---- Phases and falls ----

        [Test]
        public void TheEnemies_TakeTheirPhasesInOrder_EachThroughStepTwelve()
        {
            var s = Battle(8, 0, new[] { E("a"), E("b"), E("c") });   // gaps 0, 1, 2: 5 + 4 + 4
            s = s with { Player = s.Player with { Stamina = 0 } };      // no 構え

            var end = TurnLoop.EndTurn(s, NoRng);
            var events = end.Events.ToList();

            Assert.That(events.OfType<ActionExecuted>().Select(a => a.Unit), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(events.OfType<DamageDealt>().Select(d => (d.Unit, d.Damage)), Is.EqualTo(new[] { (0, 5), (1, 4), (2, 4) }));
            Assert.That(end.State.Player.Hp, Is.EqualTo(50 - 13));

            // Steps 9-12 finish for one enemy before the next one's step 9.
            int omenOfA = events.FindIndex(e => e is OmenSet o && o.Decided && o.Unit == 0);
            int guardOfB = events.FindIndex(e => e is GuardCleared g && g.Actor == Actor.Enemy && g.Unit == 1);
            int actOfA = events.FindIndex(e => e is ActionExecuted a && a.Unit == 0);
            Assert.That(actOfA, Is.LessThan(omenOfA));
            Assert.That(omenOfA, Is.LessThan(guardOfB));
            Assert.That(events.OfType<OmenSet>().Select(o => o.Unit), Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void OneEnemyFalls_TheCardGoesOn_AndTheBattleToo()
        {
            var bash = Fixtures.Card("bash", column: 2, face: new Face(Power: 5, Guard: 4, Reach: Reach.Only(0)),
                attributes: BattleAttribute.Attack | BattleAttribute.Guard);
            var s = Battle(8, 0, new[] { E("a"), E("b") }, bash);   // a 3, b 4
            s = s.WithEnemy(0, s.Enemies[0].Body with { Hp = 5 });

            var play = TurnLoop.PlayCard(s, InHand(s, "bash"), NoRng, target: 0);

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<EnemyDefeated>().Single().Unit, Is.EqualTo(0));
                Assert.That(play.Events.OfType<GuardGained>().Single().Amount, Is.EqualTo(4), "the Guard face still resolves");
                Assert.That(play.State.Result, Is.EqualTo(GameResult.Ongoing));
                Assert.That(play.Events.OfType<DefeatChecked>().Single().Result, Is.EqualTo(GameResult.Ongoing));
                Assert.That(play.State.Enemies[0].Omen, Is.Null);
                Assert.That(play.State.Living, Is.EqualTo(new[] { 1 }));
                Assert.That(Field.Move(play.State, Actor.Player, 2), Is.EqualTo(new Shift(2, 3, 1)), "a's cell is free; b still stops the player");
            });

            var end = TurnLoop.EndTurn(play.State, NoRng);
            Assert.That(end.Events.OfType<ActionExecuted>().Select(a => a.Unit), Is.EqualTo(new[] { 1 }));
            var next = TurnLoop.BeginPlayerTurn(end.State, NoRng);
            Assert.That(next.Events.OfType<OmenSet>().Select(o => o.Unit), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void TheLastEnemyToFall_EndsTheBattle()
        {
            var sweep = Fixtures.Card("sweep", face: new Face(Power: 5, Reach: new Reach(0, 1)), targets: TargetKind.All);
            var s = Battle(8, 0, new[] { E("a"), E("b") }, sweep);
            s = s.WithEnemy(0, s.Enemies[0].Body with { Hp = 5 }).WithEnemy(1, s.Enemies[1].Body with { Hp = 5 });

            var play = TurnLoop.PlayCard(s, InHand(s, "sweep"), NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<EnemyDefeated>().Select(e => e.Unit), Is.EqualTo(new[] { 0 }), "the first falls with the other standing");
                Assert.That(play.State.Result, Is.EqualTo(GameResult.Won));
                Assert.That(play.Events.Last(), Is.EqualTo(new BattleEnded(Actor.Player, GameResult.Won)));
            });
        }
    }
}
