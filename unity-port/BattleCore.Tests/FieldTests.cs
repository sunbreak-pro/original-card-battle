using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// §7: the line of cells — N, moving as far as the line allows, no overtaking, push / pull by
    /// cells, the wall, and the large enemy that refuses a push. The ten prototype cards carry no
    /// push, so these use fixtures.
    /// </summary>
    public class FieldTests
    {
        private static readonly IRng NoRng = new FixedRng(0.9999999);

        private static BattleState Battle(EnemyDef enemy, int enemyCell, int fieldCells = 6, params CardDef[] hand)
        {
            var deck = Cards.BuildDeck(hand.Length == 0 ? new[] { Fixtures.Card("filler") } : hand, copies: 1);
            var setup = new BattleSetup(enemy, deck, fieldCells, StartGap: enemyCell - Constants.PlayerStartCell - 1);
            Assert.That(setup.EnemyStartCell, Is.EqualTo(enemyCell), "the fixture asks for a cell the line holds");
            var s = TurnLoop.Start(setup, NoRng).State;
            return TurnLoop.BeginPlayerTurn(s, NoRng).State;
        }

        private static string InHand(BattleState s, string id) => s.Hand.First(c => c.Def.Id == id).InstanceId;

        // ---- N and the bands ----

        [TestCase(2, 5, 2)]
        [TestCase(1, 6, 4)]
        [TestCase(2, 3, 0)]
        public void TheGap_IsTheEmptyCellsBetween(int player, int enemy, int gap)
        {
            Assert.That(Field.GapBetween(Fixtures.Combatant(cell: player), Fixtures.Combatant(cell: enemy)), Is.EqualTo(gap));
        }

        [Test]
        public void ALargeEnemy_IsCountedFromItsNearEdge()
        {
            // §7.2: a dragon on 4〜6 and the player on 2 are at gap 1.
            var dragon = Fixtures.Combatant(hp: 200, cell: 4, size: 3);
            Assert.That(dragon.FarCell, Is.EqualTo(6));
            Assert.That(Field.GapBetween(Fixtures.Combatant(cell: 2), dragon), Is.EqualTo(1));
        }

        [TestCase(0, GapBand.Zero)]
        [TestCase(1, GapBand.OneToTwo)]
        [TestCase(2, GapBand.OneToTwo)]
        [TestCase(3, GapBand.ThreePlus)]
        [TestCase(6, GapBand.ThreePlus)]
        public void TheBand_SplitsAtOneAndThree(int gap, GapBand band)
        {
            Assert.That(gap.ToBand(), Is.EqualTo(band));
        }

        [Test]
        public void Reach_IsInclusive_AndPrintsAsARange()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Reach.Default, Is.EqualTo(new Reach(0, 1)));
                Assert.That(new Reach(1, 2).Contains(1), Is.True);
                Assert.That(new Reach(1, 2).Contains(2), Is.True);
                Assert.That(new Reach(1, 2).Contains(0), Is.False);
                Assert.That(new Reach(1, 2).Contains(3), Is.False);
                Assert.That(new Reach(1, 2).ToText(), Is.EqualTo("1〜2"));
                Assert.That(Reach.Only(0).ToText(), Is.EqualTo("0"));
                Assert.That(new Face().ReachOrDefault, Is.EqualTo(Reach.Default));
            });
        }

        // ---- Field.Move ----

        [Test]
        public void ThePlayer_StopsOneShortOfTheEnemy_AndAtCellOne()
        {
            var s = Battle(Fixtures.Enemy(), enemyCell: 5);   // player 2, enemy 5
            Assert.Multiple(() =>
            {
                Assert.That(Field.Move(s, Actor.Player, 1), Is.EqualTo(new Shift(2, 3, 0)));
                Assert.That(Field.Move(s, Actor.Player, 2), Is.EqualTo(new Shift(2, 4, 0)));
                Assert.That(Field.Move(s, Actor.Player, 3), Is.EqualTo(new Shift(2, 4, 1)), "no overtaking (§7.1)");
                Assert.That(Field.Move(s, Actor.Player, -1), Is.EqualTo(new Shift(2, 1, 0)));
                Assert.That(Field.Move(s, Actor.Player, -2), Is.EqualTo(new Shift(2, 1, 1)));
                Assert.That(Field.Move(s, Actor.Player, 0), Is.EqualTo(new Shift(2, 2, 0)));
            });
        }

        [Test]
        public void TheEnemy_StopsOnePastThePlayer_AndAtTheLinesEnd()
        {
            var s = Battle(Fixtures.Enemy(), enemyCell: 5);   // player 2, enemy 5, 6 cells
            Assert.Multiple(() =>
            {
                Assert.That(Field.Move(s, Actor.Enemy, 1), Is.EqualTo(new Shift(5, 4, 0)));
                Assert.That(Field.Move(s, Actor.Enemy, 2), Is.EqualTo(new Shift(5, 3, 0)));
                Assert.That(Field.Move(s, Actor.Enemy, 3), Is.EqualTo(new Shift(5, 3, 1)));
                Assert.That(Field.Move(s, Actor.Enemy, -1), Is.EqualTo(new Shift(5, 6, 0)));
                Assert.That(Field.Move(s, Actor.Enemy, -2), Is.EqualTo(new Shift(5, 6, 1)));
            });
        }

        [Test]
        public void ALargeEnemy_KeepsItsWholeBodyOnTheLine()
        {
            // Size 2 on 4〜5 of 6: one cell of room behind, and it is the far edge that hits the end.
            var s = Battle(Fixtures.Enemy(size: 2), enemyCell: 4);
            Assert.That(Field.Move(s, Actor.Enemy, -2), Is.EqualTo(new Shift(4, 5, 1)));
            Assert.That(Field.Move(s, Actor.Enemy, 1), Is.EqualTo(new Shift(4, 3, 0)));
        }

        // ---- Push, pull, the wall (§7.3) ----

        [Test]
        public void APlayerPush_SendsTheEnemyBack_AndTheWallHurtsIt()
        {
            // §7.3 / §21.3: a card may push since v4.3. Enemy on 5 of 6: a two-cell push moves one and hits the wall once.
            var shove = Fixtures.Card("shove", column: 2, face: new Face(Power: 5, Push: 2, Reach: new Reach(0, 2)),
                attributes: BattleAttribute.Attack | BattleAttribute.Move);
            var s = Battle(Fixtures.Enemy(), enemyCell: 5, hand: shove);

            var play = TurnLoop.PlayCard(s, InHand(s, "shove"), NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<CellsMoved>().Single(), Is.EqualTo(new CellsMoved(Actor.Enemy, 5, 6, Pushed: true)));
                Assert.That(play.Events.OfType<WallHit>().Single(),
                    Is.EqualTo(new WallHit(Actor.Enemy, 1, Raw: 3, Absorbed: 0, Damage: 3, GuardAfter: 0, HpAfter: 60 - 5 - 3)));
                Assert.That(play.State.Gap, Is.EqualTo(3));
            });
        }

        [Test]
        public void APull_DrawsTheOpponentIn_AndHasNoWall()
        {
            // The enemy pulls the player in by two from gap 2: the player stops one short (cell 4)
            // and the blocked cell costs nothing.
            var pull = Fixtures.EnemyAction("hook", column: 1, face: new Face(Power: 2, Push: -2, Reach: new Reach(0, 3)),
                attributes: BattleAttribute.Attack | BattleAttribute.Move);
            var s = Battle(Fixtures.Enemy(atOneToTwo: pull), enemyCell: 5);
            Assert.That(s.Omen!.ActionId, Is.EqualTo("hook"));

            var end = TurnLoop.EndTurn(s, NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<CellsMoved>().Single(), Is.EqualTo(new CellsMoved(Actor.Player, 2, 4, Pushed: true)));
                Assert.That(end.Events.OfType<WallHit>(), Is.Empty);
                Assert.That(end.State.Gap, Is.EqualTo(0));
            });
        }

        [Test]
        public void ALargeEnemy_RefusesThePush_AndTakesNoWall()
        {
            var shove = Fixtures.Card("shove", column: 2, face: new Face(Power: 5, Push: 2, Reach: new Reach(0, 2)),
                attributes: BattleAttribute.Attack | BattleAttribute.Move);
            var s = Battle(Fixtures.Enemy(size: 2), enemyCell: 5, hand: shove);

            var play = TurnLoop.PlayCard(s, InHand(s, "shove"), NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<PushRefused>().Single(), Is.EqualTo(new PushRefused(Actor.Enemy, 2)));
                Assert.That(play.Events.OfType<CellsMoved>(), Is.Empty);
                Assert.That(play.Events.OfType<WallHit>(), Is.Empty);
                Assert.That(play.State.Enemy.Cell, Is.EqualTo(5));
                Assert.That(play.State.Enemy.Hp, Is.EqualTo(55), "the attack face still lands");
            });
        }

        [Test]
        public void ASlowedPusher_PushesOneCellLess_AndTheTargetsSlowChangesNothing()
        {
            var shove = Fixtures.Card("shove", column: 2, face: new Face(Push: 2), attributes: BattleAttribute.Move);
            var s = Battle(Fixtures.Enemy(), enemyCell: 4, hand: shove);   // player 2, enemy 4, gap 1

            var slowedPusher = s with { Player = s.Player with { Statuses = StatusSet.Of((StatusKind.Slow, 1)) } };
            var a = TurnLoop.PlayCard(slowedPusher, InHand(slowedPusher, "shove"), NoRng);
            Assert.That(a.Events.OfType<CellsMoved>().Single(), Is.EqualTo(new CellsMoved(Actor.Enemy, 4, 5, Pushed: true)));

            var slowedTarget = s with { Enemy = s.Enemy with { Statuses = StatusSet.Of((StatusKind.Slow, 1)) } };
            var b = TurnLoop.PlayCard(slowedTarget, InHand(slowedTarget, "shove"), NoRng);
            Assert.That(b.Events.OfType<CellsMoved>().Single(), Is.EqualTo(new CellsMoved(Actor.Enemy, 4, 6, Pushed: true)));

            var onlyOne = s with { Player = s.Player with { Statuses = StatusSet.Of((StatusKind.Slow, 1)) } };
            var oneCell = Fixtures.Card("nudge", column: 1, face: new Face(Push: 1), attributes: BattleAttribute.Move);
            var c = TurnLoop.PlayCard(
                onlyOne with { Hand = new[] { new CardInstance("nudge-0", oneCell) } }, "nudge-0", NoRng);
            Assert.That(c.Events.OfType<MoveBlocked>().Single(), Is.EqualTo(new MoveBlocked(Actor.Player, StatusKind.Slow)));
        }

        [Test]
        public void AWhiffedPush_MovesNobody_ButTheSelfFacesStillResolve()
        {
            // The enemy's shove reaches 0 only; declared adjacent, the player then steps back.
            var shove = Fixtures.EnemyAction("shove", column: 2,
                face: new Face(Power: 5, Push: 2, Guard: 2, Reach: Reach.Only(0)),
                attributes: BattleAttribute.Attack | BattleAttribute.Move | BattleAttribute.Guard);
            var back = Fixtures.Card("back", column: 1, face: new Face(Move: -1), attributes: BattleAttribute.Move, targets: TargetKind.Self);
            var s = Battle(Fixtures.Enemy(atZero: shove), enemyCell: 3, hand: back);
            Assert.That(s.Omen!.ActionId, Is.EqualTo("shove"));
            s = TurnLoop.PlayCard(s, InHand(s, "back"), NoRng).State;

            var end = TurnLoop.EndTurn(s, NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<ActionWhiffed>().Single().Gap, Is.EqualTo(1));
                Assert.That(end.Events.OfType<CellsMoved>().Where(m => m.Pushed), Is.Empty);
                Assert.That(end.Events.OfType<DamageDealt>(), Is.Empty);
                Assert.That(end.Events.OfType<GuardGained>().Single(e => e.Actor == Actor.Enemy).Amount, Is.EqualTo(2));
                Assert.That(end.Events.OfType<FaceResolved>().Where(f => f.Actor == Actor.Enemy).Select(f => f.Face),
                    Is.EqualTo(new[] { BattleAttribute.Move, BattleAttribute.Guard }));
            });
        }

        // ---- Validation ----

        [Test]
        public void Validate_RefusesWhatTheLineCannotHold()
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => Field.Validate(6, 2, 1, 5, 1), Throws.Nothing);
                Assert.That(() => Field.Validate(4, 2, 1, 3, 1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
                Assert.That(() => Field.Validate(9, 2, 1, 5, 1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
                Assert.That(() => Field.Validate(6, 2, 1, 5, 2), Throws.Nothing, "5〜6 fits");
                Assert.That(() => Field.Validate(6, 2, 1, 5, 3), Throws.InstanceOf<System.ArgumentOutOfRangeException>(), "5〜7 does not");
                Assert.That(() => Field.Validate(6, 2, 1, 5, 4), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
                Assert.That(() => Field.Validate(6, 0, 1, 5, 1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
                Assert.That(() => Field.Validate(6, 5, 1, 5, 1), Throws.ArgumentException);
            });
        }
    }
}
