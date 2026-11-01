// #573: N (battle_core_v4 §7.2) is counted in one place, Field.Gap. With several enemies the core reads
// the nearest standing one for the player's own N (TurnLoop's OwnGap: GapTo(Nearest)); the writer's floor
// follows that enemy (its FocusUnit, #589). These hold the N the script carries against the core's. Runs under `dotnet test`
// (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class GapCountTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static readonly CardDef[] Hand =
        {
            CardCatalog.KesaCut, CardCatalog.ReachThrust, CardCatalog.Brace, CardCatalog.BackLeap, CardCatalog.RockStance,
        };

        /// <summary>Two 瘴牙の走竜 on an 8-cell line, the first <paramref name="gap"/> away, and the writer past the opening.</summary>
        private static (BattleState State, CoreScriptWriter Writer, DepictionFrame Opening) TwoHounds(int gap)
        {
            var setup = new BattleSetup(Enemies.ShadowHound, Cards.BuildDeck(Hand, 1), 8,
                StartGap: gap, MoreEnemies: new[] { Enemies.ShadowHound });
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(setup.Enemy);
            DepictionFrame opening = writer.Opening(state);
            return (state, writer, opening);
        }

        /// <summary>The player's own N as the core reads it with several enemies (TurnLoop's OwnGap).</summary>
        private static int CoreOwnGap(BattleState state) => state.GapTo(state.Nearest);

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void WithTwoEnemies_TheScriptsN_IsTheCoresOwnGap(int gap)
        {
            var (state, writer, opening) = TwoHounds(gap);
            Assert.That(state.Enemies.Count, Is.EqualTo(2));
            Assert.That(state.GapTo(1), Is.GreaterThan(state.GapTo(0)), "the second hound stands behind the first");

            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            DepictionFrame begun = writer.Write(begin.Events, begin.State).Last().After;

            Assert.Multiple(() =>
            {
                Assert.That(opening.Floor.Gap, Is.EqualTo(CoreOwnGap(state)), "opening");
                Assert.That(begun.Floor.Gap, Is.EqualTo(CoreOwnGap(begin.State)), "turn 1 begun");
                Assert.That(begun.Floor.Gap, Is.EqualTo(Field.Gap(begin.State.Player.Cell, begin.State.Player.Size, begin.State.Enemy.Cell)));
            });
        }

        /// <summary>#589 (2026-10-19's measurement): the first hound falls and the second stands; the floor reads the second.</summary>
        [Test]
        public void TheFirstEnemyFallen_TheFloorsN_IsTheCoresOwnGap()
        {
            var setup = new BattleSetup(Enemies.ShadowHound, Cards.BuildDeck(Hand, 1), 8,
                StartGap: 0, MoreEnemies: new[] { Enemies.ShadowHound });
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            state = state.WithUnit(0, state.Enemies[0] with { Body = state.Enemies[0].Body with { Hp = 1 } });
            var writer = new CoreScriptWriter(setup.Enemy);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);

            CardInstance card = begin.State.Hand.First(c => TurnLoop.CanPlay(begin.State, c.InstanceId, 0) == PlayRefusal.None
                && c.Def.Face.Move == 0 && TurnLoop.Preview(begin.State, c.InstanceId, 0).Attacks);
            StepResult played = TurnLoop.PlayCard(begin.State, card.InstanceId, NoShuffle, 0);
            System.Collections.Generic.List<DepictionEvent> events = writer.Write(played.Events, played.State);

            Assert.That(played.State.Enemies[0].Alive, Is.False, "the first hound fell");
            Assert.That(played.State.Enemies[1].Alive, Is.True, "the second stands");
            int coreN = CoreOwnGap(played.State);
            Assert.That(coreN, Is.GreaterThan(0), "the second hound stands away from the player");
            Assert.That(events.Last().Kind, Is.EqualTo(DepictionEventKind.Defeat), "the fall is a beat of its own");
            foreach (DepictionEvent ev in events)
            {
                Assert.That(ev.After.Floor.Gap, Is.EqualTo(coreN), ev.Title + ": the floor reads the enemy that stands");
                Assert.That(ev.After.Floor.EnemyCell, Is.EqualTo(played.State.Enemies[1].Body.Cell), ev.Title + ": and its cell");
            }
        }

        /// <summary>#589 (2026-10-19's measurement): the first hound has gone behind the second (cells 6 and 4); the floor reads the second.</summary>
        [Test]
        public void TheFirstEnemyBehindTheSecond_TheFloorsN_IsTheCoresOwnGap()
        {
            var setup = new BattleSetup(Enemies.ShadowHound, Cards.BuildDeck(Hand, 1), 8,
                StartGap: 0, MoreEnemies: new[] { Enemies.ShadowHound });
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            state = state.WithUnit(0, state.Enemies[0] with { Body = state.Enemies[0].Body with { Cell = 6 } });
            state = state.WithUnit(1, state.Enemies[1] with { Body = state.Enemies[1].Body with { Cell = 4 } });
            Assert.That(state.Nearest, Is.EqualTo(1), "the second hound stands nearer");

            var writer = new CoreScriptWriter(setup.Enemy);
            DepictionFrame opening = writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            DepictionFrame begun = writer.Write(begin.Events, begin.State).Last().After;

            Assert.Multiple(() =>
            {
                Assert.That(opening.Floor.Gap, Is.EqualTo(CoreOwnGap(state)), "opening");
                Assert.That(opening.Floor.Gap, Is.EqualTo(1), "the player on cell 2, the second hound on cell 4");
                Assert.That(opening.Floor.EnemyCell, Is.EqualTo(4));
                Assert.That(begun.Floor.Gap, Is.EqualTo(CoreOwnGap(begin.State)), "turn 1 begun");
            });
        }
    }
}
