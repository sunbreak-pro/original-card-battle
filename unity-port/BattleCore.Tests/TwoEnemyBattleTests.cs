using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// §7.4 with roster §9's one pair (#52): 錆槍の竜兵 in front, 灰弩の竜兵 one cell behind it, on
    /// 7 cells. The v4.2 shape of a leader with a minion is gone (2026-09-23); what is left of
    /// "the minion reads no position" is that the enemy behind decides from its own N alone and
    /// never from where the one in front stands.
    /// </summary>
    public class TwoEnemyBattleTests
    {
        private static readonly IRng NoRng = new FixedRng(0.9999999);

        private static readonly CardDef Lance = Fixtures.Card("lance", face: new Face(Power: 6, Reach: new Reach(0, 4)));

        private static BattleState Pair(params CardDef[] hand)
        {
            var deck = Cards.BuildDeck(hand.Length == 0 ? new[] { Fixtures.Card("filler") } : hand, copies: 1);
            var battle = ChainOrder.Nine.Single(b => b.EnemyIds.Count == 2);
            var setup = new BattleSetup(battle.Defs[0], deck, battle.FieldCells, MoreEnemies: new[] { battle.Defs[1] });
            return TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoRng).State, NoRng).State;
        }

        private static string InHand(BattleState s, string id) => s.Hand.First(c => c.Def.Id == id).InstanceId;

        [Test]
        public void ThePair_StandsAsRosterSection9Says()
        {
            var s = Pair();
            Assert.Multiple(() =>
            {
                Assert.That(s.FieldCells, Is.EqualTo(7));
                Assert.That(s.Enemies.Select(u => u.Def.Id), Is.EqualTo(new[] { "polearm_warped", "crossbow_hunter" }));
                Assert.That(Enumerable.Range(0, 2).Select(s.GapTo), Is.EqualTo(new[] { 3, 4 }), "開始の間合い 3, and 4 for the one behind");
                Assert.That(s.Enemies.All(u => u.Omen != null), Is.True, "each shows its own omen");
            });
        }

        [Test]
        public void EachEnemy_TakesItsOwnPhaseInOrder_AndTheBattleGoesOnAfterOneFalls()
        {
            var s = Pair(Lance);
            s = s with { Player = s.Player with { Stamina = 0 } };

            var first = TurnLoop.EndTurn(s, NoRng);
            var events = first.Events.ToList();
            Assert.Multiple(() =>
            {
                Assert.That(events.OfType<ActionExecuted>().Select(a => a.Unit), Is.EqualTo(new[] { 0, 1 }));
                Assert.That(events.OfType<OmenSet>().Where(o => o.Decided).Select(o => o.Unit), Is.EqualTo(new[] { 0, 1 }));
                int omenOfFront = events.FindIndex(e => e is OmenSet o && o.Decided && o.Unit == 0);
                int actOfBack = events.FindIndex(e => e is ActionExecuted a && a.Unit == 1);
                Assert.That(omenOfFront, Is.LessThan(actOfBack), "the front finishes step 12 before the back one's step 10");
            });

            // The polearm falls to the lance; the crossbow stands and the battle goes on.
            var next = TurnLoop.BeginPlayerTurn(first.State, NoRng).State;
            next = next.WithEnemy(0, next.Enemies[0].Body with { Hp = 1, Guard = 0 });
            var play = TurnLoop.PlayCard(next, InHand(next, "lance"), NoRng, target: 0);
            Assert.Multiple(() =>
            {
                Assert.That(play.Events.OfType<EnemyDefeated>().Single().Unit, Is.EqualTo(0));
                Assert.That(play.State.Result, Is.EqualTo(GameResult.Ongoing));
                Assert.That(play.State.Living, Is.EqualTo(new[] { 1 }));
            });

            var after = TurnLoop.EndTurn(play.State, NoRng);
            Assert.Multiple(() =>
            {
                Assert.That(after.Events.OfType<ActionExecuted>().Select(a => a.Unit), Is.EqualTo(new[] { 1 }), "only the crossbow takes a phase");
                Assert.That(after.State.Result, Is.EqualTo(GameResult.Ongoing));
            });
            var third = TurnLoop.BeginPlayerTurn(after.State, NoRng);
            Assert.That(third.Events.OfType<OmenSet>().Select(o => o.Unit), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void TheEnemyBehind_DecidesFromItsOwnGap_NotFromWhereTheFrontOneStands()
        {
            // The crossbow keeps cell 7 (N 4). The one in front is a fixture that hits or walks and
            // never moves the player, placed on each cell between them, or fallen.
            var front = Fixtures.Enemy("front");
            var deck = Cards.BuildDeck(new[] { Fixtures.Card("filler") }, copies: 1);
            var start = TurnLoop.Start(new BattleSetup(front, deck, 7, MoreEnemies: new[] { Enemies.CrossbowHunter }), NoRng).State;
            start = TurnLoop.BeginPlayerTurn(start, NoRng).State;
            start = start with { Player = start.Player with { Stamina = 0 } };
            Assert.That(start.GapTo(1), Is.EqualTo(4));

            var decided = new List<Omen>();
            var frontOmens = new List<string>();
            foreach (int cell in new[] { 3, 4, 5, 6 })
            {
                var s = start.WithEnemy(0, start.Enemies[0].Body with { Cell = cell });
                var end = TurnLoop.EndTurn(s, NoRng).State;
                Assert.That(end.GapTo(1), Is.EqualTo(4), "nothing moved the player or the crossbow");
                decided.Add(end.Enemies[1].Omen!);
                frontOmens.Add(end.Enemies[0].Omen!.ActionId);
            }
            var fallen = start.WithEnemy(0, start.Enemies[0].Body with { Hp = 0 });
            decided.Add(TurnLoop.EndTurn(fallen, NoRng).State.Enemies[1].Omen!);

            // The same crossbow alone on the same cell, as a one-enemy battle.
            var alone = TurnLoop.Start(new BattleSetup(Enemies.CrossbowHunter, deck, 7, StartGap: 4), NoRng).State;
            alone = TurnLoop.BeginPlayerTurn(alone, NoRng).State;
            alone = alone with { Player = alone.Player with { Stamina = 0 } };
            var aloneOmen = TurnLoop.EndTurn(alone, NoRng).State.Enemies[0].Omen!;

            Assert.Multiple(() =>
            {
                Assert.That(frontOmens.Distinct().Count(), Is.GreaterThan(1), "the front one's own choice does move with its cell");
                Assert.That(decided, Is.All.EqualTo(aloneOmen), "the crossbow's does not");
            });
        }
    }
}
