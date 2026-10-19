// #573: N (battle_core_v4 §7.2) is counted in one place, Field.Gap. With several enemies the core reads
// the nearest standing one for the player's own N (TurnLoop's OwnGap: GapTo(Nearest)); the writer draws
// the first enemy. These hold the N the script carries against the core's. Runs under `dotnet test`
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
    }
}
