// #455: when the writer marks the enemy's turn on the script (battle-visual-v1 §5.5): from the player's
// turn end, through the enemy's beats and its next omen, until the next turn start — never before
// turn 1 opens, never while the player acts. Runs under `dotnet test` (Depiction.Bridge.Tests.csproj)
// and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class EnemyTurnReadoutTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static readonly CardDef[] Hand =
        {
            CardCatalog.KesaCut, CardCatalog.Feint, CardCatalog.Brace, CardCatalog.ReachThrust, CardCatalog.BackLeap,
        };

        private static (CoreScriptWriter Writer, BattleState Start, DepictionFrame Opening) Opened()
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(Hand, 2), BattleSetup.SliceFieldCells, StartGap: 2);
            BattleState start = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(Enemies.PolearmWarped);
            return (writer, start, writer.Opening(start));
        }

        [Test]
        public void BeforeTurnOne_AndOnThePlayersTurn_ItIsNotTheEnemysTurn()
        {
            var (writer, start, opening) = Opened();
            Assert.That(opening.EnemyTurn, Is.False);
            StepResult begin = TurnLoop.BeginPlayerTurn(start, NoShuffle);
            foreach (DepictionEvent ev in writer.Write(begin.Events, begin.State)) Assert.That(ev.After.EnemyTurn, Is.False, ev.Title);
            CardInstance card = begin.State.Hand.First(c => c.Def == CardCatalog.Brace);
            StepResult played = TurnLoop.PlayCard(begin.State, card.InstanceId, NoShuffle);
            foreach (DepictionEvent ev in writer.Write(played.Events, played.State)) Assert.That(ev.After.EnemyTurn, Is.False, ev.Title);
        }

        [Test]
        public void FromTheTurnEnd_UntilTheNextTurnStart_ItIsTheEnemysTurn()
        {
            var (writer, start, _) = Opened();
            StepResult begin = TurnLoop.BeginPlayerTurn(start, NoShuffle);
            writer.Write(begin.Events, begin.State);
            StepResult end = TurnLoop.EndTurn(begin.State, NoShuffle);
            List<DepictionEvent> enemyTurn = writer.Write(end.Events, end.State);
            Assert.That(enemyTurn.Select(e => e.Kind), Does.Contain(DepictionEventKind.TurnEnd));
            Assert.That(enemyTurn.Select(e => e.Kind), Does.Contain(DepictionEventKind.EnemyAction));
            foreach (DepictionEvent ev in enemyTurn) Assert.That(ev.After.EnemyTurn, Is.True, ev.Title);

            StepResult next = TurnLoop.BeginPlayerTurn(end.State, NoShuffle);
            List<DepictionEvent> turnTwo = writer.Write(next.Events, next.State);
            Assert.That(turnTwo.First().Kind, Is.EqualTo(DepictionEventKind.TurnStart));
            foreach (DepictionEvent ev in turnTwo) Assert.That(ev.After.EnemyTurn, Is.False, ev.Title + ": the screen comes back up");
        }
    }
}
