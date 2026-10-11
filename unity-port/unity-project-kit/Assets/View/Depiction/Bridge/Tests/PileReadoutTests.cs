// #455: the counts the writer puts on the script for battle-visual-v1 §4.7's three piles — the core's
// draw, discard and exile piles as each move leaves them. Runs under `dotnet test`
// (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class PileReadoutTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static readonly CardDef[] Kinds =
        {
            CardCatalog.RockStance, CardCatalog.Brace, CardCatalog.KesaCut, CardCatalog.Feint, CardCatalog.ReachThrust,
        };

        /// <summary>20 cards, dealt in this order (NoShuffle): the first hand holds 岩の構え and 呼吸を整える (a card on oneself, so the gap does not matter).</summary>
        private static List<CardInstance> Deck() => Cards.BuildDeck(Kinds, 4);

        private static (CoreScriptWriter Writer, BattleState State, DepictionFrame Opening) Opened()
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, Deck(), BattleSetup.SliceFieldCells, StartGap: 2);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(Enemies.PolearmWarped);
            return (writer, state, writer.Opening(state));
        }

        private static void AssertMatches(PileFrame piles, BattleState state)
        {
            Assert.That(piles.Shown, Is.True);
            Assert.That(piles.Draw, Is.EqualTo(state.DrawPile.Count), "draw");
            Assert.That(piles.Discard, Is.EqualTo(state.DiscardPile.Count), "discard");
            Assert.That(piles.Exile, Is.EqualTo(state.Exiled.Count), "exile");
        }

        [Test]
        public void BeforeTheFirstDraw_TheWholeDeckIsTheDrawPile()
        {
            var (_, state, opening) = Opened();
            AssertMatches(opening.Piles, state);
            Assert.That(opening.Piles.Draw, Is.EqualTo(20));
            Assert.That(opening.Piles.Discard, Is.EqualTo(0));
            Assert.That(opening.Piles.Exile, Is.EqualTo(0));
        }

        [Test]
        public void AtTheTurnStart_TheDrawPileHasGivenTheHand()
        {
            var (writer, state, _) = Opened();
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            DepictionFrame frame = writer.Write(begin.Events, begin.State).Last().After;
            AssertMatches(frame.Piles, begin.State);
            Assert.That(frame.Piles.Draw + frame.Hand.Count, Is.EqualTo(20));
        }

        [Test]
        public void APlayedCard_GoesToTheDiscardPile_AndAStanceToTheExilePile()
        {
            var (writer, state, _) = Opened();
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);
            BattleState now = begin.State;

            CardInstance stance = now.Hand.FirstOrDefault(c => c.Def == CardCatalog.RockStance);
            CardInstance other = now.Hand.FirstOrDefault(c => c.Def != CardCatalog.RockStance && c.Def.Cost <= now.Player.Stamina);
            Assert.That(stance, Is.Not.Null, "the deck is dealt unshuffled, 岩の構え first");
            Assert.That(other, Is.Not.Null, "and the self card after it");

            StepResult played = TurnLoop.PlayCard(now, other.InstanceId, NoShuffle);
            DepictionFrame afterOther = writer.Write(played.Events, played.State).Last().After;
            AssertMatches(afterOther.Piles, played.State);
            Assert.That(afterOther.Piles.Discard, Is.EqualTo(1));

            Assert.That(stance.Def.Cost, Is.LessThanOrEqualTo(played.State.Player.Stamina));
            StepResult placed = TurnLoop.PlayCard(played.State, stance.InstanceId, NoShuffle);
            DepictionFrame afterStance = writer.Write(placed.Events, placed.State).Last().After;
            AssertMatches(afterStance.Piles, placed.State);
            Assert.That(afterStance.Piles.Exile, Is.EqualTo(1), "a stance card stays out of this battle");
        }

        [Test]
        public void AtTheTurnEnd_TheHandIsInTheDiscardPile()
        {
            var (writer, state, _) = Opened();
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);
            int hand = begin.State.Hand.Count;
            StepResult end = TurnLoop.EndTurn(begin.State, NoShuffle);
            List<DepictionEvent> written = writer.Write(end.Events, end.State);

            DepictionFrame last = written.Last().After;
            AssertMatches(last.Piles, end.State);
            Assert.That(last.Piles.Discard, Is.EqualTo(hand));
        }
    }
}
