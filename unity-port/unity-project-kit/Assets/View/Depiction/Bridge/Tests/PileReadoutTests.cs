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
            List<DepictionEvent> otherEvents = writer.Write(played.Events, played.State);
            DepictionFrame afterOther = otherEvents.Last().After;
            AssertMatches(afterOther.Piles, played.State);
            Assert.That(afterOther.Piles.Discard, Is.EqualTo(1));
            Assert.That(otherEvents.Single(e => e.Kind == DepictionEventKind.PlayCard).PlayedTo, Is.EqualTo(PileKind.Discard));

            Assert.That(stance.Def.Cost, Is.LessThanOrEqualTo(played.State.Player.Stamina));
            StepResult placed = TurnLoop.PlayCard(played.State, stance.InstanceId, NoShuffle);
            List<DepictionEvent> stanceEvents = writer.Write(placed.Events, placed.State);
            DepictionFrame afterStance = stanceEvents.Last().After;
            AssertMatches(afterStance.Piles, placed.State);
            Assert.That(afterStance.Piles.Exile, Is.EqualTo(1), "a stance card stays out of this battle");
            // battle_ui_ux_v2 §5.11 順 4 (#455): the core's CardExiled sends the card's ghost to the exile pile.
            Assert.That(stanceEvents.Single(e => e.Kind == DepictionEventKind.PlayCard).PlayedTo, Is.EqualTo(PileKind.Exile));
            Assert.That(afterStance.Piles.ExileCards.Select(c => c.RowText), Is.EqualTo(new[] { CardCatalog.RockStance.Name + " 1 / 4" }));
        }

        [Test]
        public void ThePileLists_CountEachKind_AgainstTheWholeDeck_InTheCatalogsOrder()
        {
            // battle_ui_ux_v2 §1.1 C4 (#455): 同種の残り「突き 2 / 3」.
            var (writer, state, _) = Opened();
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            PileFrame piles = writer.Write(begin.Events, begin.State).Last().After.Piles;

            Assert.Multiple(() =>
            {
                Assert.That(piles.DrawCards.Sum(c => c.InPile), Is.EqualTo(piles.Draw), "every card of the draw pile is on a row");
                Assert.That(piles.DrawCards.Select(c => c.Name), Is.Unique, "one row per kind");
                Assert.That(piles.DrawCards.All(c => c.InDeck == 4), Is.True, "4 of each kind in the deck, the hand's included");
                Assert.That(piles.DiscardCards, Is.Empty);
                Assert.That(piles.ExileCards, Is.Empty);
                // The rows follow the catalogue, not the pile: the order of the draw pile stays hidden.
                List<int> order = piles.DrawCards.Select(c => CardCatalog.All.ToList().FindIndex(d => d.Name == c.Name)).ToList();
                Assert.That(order, Is.Ordered);
                Assert.That(piles.CardsOf(PileKind.Draw), Is.SameAs(piles.DrawCards));
            });
        }

        [Test]
        public void ADrawThatRunsThePileOut_MarksWhereThePileWasMadeAgain()
        {
            // battle_ui_ux_v2 §3.14 山札切れ (#455): the turn start whose draw meets the end of the pile
            // carries how many cards came off the old pile before the discard pile was shuffled in.
            var (writer, state, _) = Opened();
            for (int turn = 0; turn < 8; turn++)
            {
                int pileBefore = state.DrawPile.Count;
                StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
                List<DepictionEvent> events = writer.Write(begin.Events, begin.State);
                Cue draw = events.SelectMany(e => e.Cues).FirstOrDefault(c => c.Kind == CueKind.DrawHand);
                bool rebuilt = begin.Events.Any(e => e is DeckReshuffled);
                Assert.That(draw, Is.Not.Null);
                if (rebuilt)
                {
                    Assert.That(draw.RebuildAfter, Is.EqualTo(pileBefore), "the cards left on the old pile come first");
                    Assert.That(draw.Amount, Is.GreaterThan(pileBefore));
                    List<EffectStep> steps = EffectPlan.StepsOf(events.First(e => e.Cues.Contains(draw)));
                    Assert.That(steps.Select(s => s.Id), Does.Contain(EffectId.PileRebuild));
                    return;
                }
                Assert.That(draw.RebuildAfter, Is.EqualTo(Cue.Unchanged));
                StepResult end = TurnLoop.EndTurn(begin.State, NoShuffle);
                writer.Write(end.Events, end.State);
                state = end.State;
                Assert.That(state.Result, Is.EqualTo(GameResult.Ongoing), "the battle lasts until the pile runs out");
            }
            Assert.Fail("the 20-card deck never ran its draw pile out");
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
