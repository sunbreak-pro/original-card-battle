// #626: the screen that picks a card on the way — a real elite's two, the card pile's three, and the
// card a 習熟訓練 mark goes to — as CardPicks builds it from Expedition, and what a pressed card does.
// Runs under `dotnet test` (Exploration.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction.Bridge;
using DungeonCore;
using NUnit.Framework;

namespace Exploration.Bridge.Tests
{
    public class CardPickTests
    {
        [Test]
        public void NothingToPick_ShowsNoScreen()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            Assert.That(CardPicks.For(expedition).IsShown, Is.False, "the entry battle waits; nothing is offered yet");
            Assert.That(CardPicks.Choose(expedition, CardCatalog.Thrust.Id), Is.False);
        }

        [Test]
        public void ARealElite_LaysOutItsTwoCards_AndTheOnePickedIsOwnedAndInTheDeck()
        {
            // Layer 7's elite is always 統牙の長竜, and no boss before it teaches its cards.
            var expedition = new Expedition(new RunStart { Seed = 5 });
            WalkToLayer(expedition, 7);
            WalkToAndWin(expedition, NodeKind.Elite);

            CardPick pick = CardPicks.For(expedition);
            var ids = pick.Choices.Select(c => c.CardId).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(pick.Kind, Is.EqualTo(PickKind.EliteCard));
                Assert.That(pick.Title, Is.EqualTo(CardPicks.EliteTitle));
                Assert.That(ids, Is.EqualTo(UniqueCards.OfElite("pack_alpha")));
                Assert.That(pick.Choices.Select(c => c.Face.Id), Is.EqualTo(ids), "each card printed with its own face");
                Assert.That(pick.Choices.Select(c => c.Face.Name), Is.EqualTo(ids.Select(id => CardCatalog.ById(id).Name)));
            });

            Assert.That(CardPicks.Choose(expedition, ids[1]), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(expedition.Owned.Contains(ids[1]), Is.True);
                Assert.That(expedition.Deck.Any(c => c.Def.Id == ids[1]), Is.True);
                Assert.That(expedition.Owned.Contains(ids[0]), Is.False, "one of the two");
                Assert.That(CardPicks.For(expedition).Kind, Is.Not.EqualTo(PickKind.EliteCard), "the offer is spent");
            });
        }

        [Test]
        public void AWonBattlesCardReward_LaysOutThreeCards_OneToPick()
        {
            Expedition expedition = WinUntilACardIsOffered();
            CardPick pick = CardPicks.For(expedition);
            var ids = pick.Choices.Select(c => c.CardId).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(pick.Kind, Is.EqualTo(PickKind.PileCard));
                Assert.That(pick.Title, Is.EqualTo(CardPicks.PileTitle));
                Assert.That(ids.Count, Is.EqualTo(CardPile.Choices));
                Assert.That(ids.All(CardPile.Of(1).Contains), Is.True, "layer 1 deals its own pile");
            });

            Assert.That(CardPicks.Choose(expedition, ids[2]), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(expedition.Owned.Contains(ids[2]), Is.True);
                Assert.That(expedition.Deck.Any(c => c.Def.Id == ids[2]), Is.True);
                Assert.That(expedition.Owned.Contains(ids[0]) || expedition.Owned.Contains(ids[1]), Is.False, "one of the three");
                Assert.That(CardPicks.For(expedition).IsShown, Is.False);
            });
        }

        [Test]
        public void ACardNotLaidOut_IsNotTaken()
        {
            Expedition expedition = WinUntilACardIsOffered();
            Assert.That(CardPicks.Choose(expedition, CardCatalog.LastStand.Id), Is.False, "#80 is not in layer 1's pile");
            Assert.That(CardPicks.For(expedition).Kind, Is.EqualTo(PickKind.PileCard), "the offer still waits");
        }

        [Test]
        public void ATrainingMark_LaysOutTheDecksCards_AndThePickedOneTakesTheTicks()
        {
            var expedition = new Expedition(new RunStart { Seed = 3 });
            WalkToAndWin(expedition, NodeKind.Rest, RestChoice.Train);
            Assert.That(expedition.Session.State.TrainingMarks, Is.EqualTo(1));

            CardPick pick = CardPicks.For(expedition);
            var ids = pick.Choices.Select(c => c.CardId).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(pick.Kind, Is.EqualTo(PickKind.Training));
                Assert.That(pick.Title, Is.EqualTo(CardPicks.TrainingTitle));
                Assert.That(ids, Is.EqualTo(expedition.Deck.Select(c => c.Def.Id).Distinct()), "each kind of the deck once, in deck order");
                Assert.That(ids.Take(PrototypeDeck.Kinds.Count), Is.EqualTo(PrototypeDeck.Kinds.Select(d => d.Id)), "the deck set out with comes first");
                Assert.That(pick.Choices[0].Caption, Is.EqualTo("刻み 0"));
            });

            string card = ids[0];
            int before = expedition.Mastery.TicksOf(card);
            Assert.That(CardPicks.Choose(expedition, card), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(expedition.Mastery.TicksOf(card), Is.EqualTo(before + Constants.TrainingTicks));
                Assert.That(expedition.Session.State.TrainingMarks, Is.EqualTo(0));
                Assert.That(CardPicks.For(expedition).IsShown, Is.False, "the mark is spent");
            });
        }

        [Test]
        public void NoPick_ShowsDuringABattle()
        {
            Expedition expedition = WinUntilACardIsOffered();
            ExplorationSession session = expedition.Session;
            int next = session.State.ReachableUnresolved().First(id => session.State.Map.Node(id).Kind.IsCombat());
            Assert.That(session.Step(next), Is.True);
            if (session.HasPendingStep) Assert.That(session.Act(ScreenAction.ConfirmStep), Is.True);
            expedition.StartEncounter();
            Assert.That(CardPicks.For(expedition).IsShown, Is.False);
        }

        // ---- helpers ----

        /// <summary>The first seed whose entry battle, won, lays out the card pile to pick from.</summary>
        private static Expedition WinUntilACardIsOffered()
        {
            for (ulong seed = 1; seed < 100; seed++)
            {
                var expedition = new Expedition(new RunStart { Seed = seed });
                WinPending(expedition);
                if (CardPicks.For(expedition).Kind == PickKind.PileCard) return expedition;
            }
            Assert.Fail("no seed in 1〜99 dealt a card at the entry battle");
            return null;
        }

        /// <summary>Wins whatever battle waits, at full HP, without playing it out.</summary>
        private static void WinPending(Expedition expedition)
        {
            EncounterBattle battle = expedition.StartEncounter();
            Assert.That(battle, Is.Not.Null);
            while (!battle.Over)
            {
                CoreBattleSource source = battle.StartNext();
                BattleState state = source.State;
                BattleState won = state with { Result = GameResult.Won, Player = state.Player with { Hp = Constants.PlayerMaxHp, Stamina = 5 } };
                battle.Finish(won, new List<BattleEvent>(source.History));
            }
            expedition.Return();
        }

        /// <summary>
        /// Wins the battle, then picks the first card of a pile offer it leaves, as a player would before
        /// walking on. An elite's offer is left to the next battle to replace, so no elite's card is
        /// owned before the elite a test is about.
        /// </summary>
        private static void WinAndPick(Expedition expedition)
        {
            WinPending(expedition);
            CardPick pick = CardPicks.For(expedition);
            if (pick.Kind == PickKind.PileCard)
                Assert.That(CardPicks.Choose(expedition, pick.Choices[0].CardId), Is.True);
        }

        /// <summary>
        /// Walks the shortest way to the nearest unresolved node of this kind, winning every battle on the
        /// way and picking from its offer, and steps onto it with the choice given. The last battle's
        /// offer is left waiting.
        /// </summary>
        private static void WalkToAndWin(Expedition expedition, NodeKind kind, RestChoice choice = RestChoice.Rest)
        {
            ExplorationSession session = expedition.Session;
            if (session.HasPendingBattle) WinAndPick(expedition);
            IReadOnlyList<int> path = PathTo(session.State, kind);
            foreach (int node in path)
            {
                bool last = node == path[path.Count - 1];
                Assert.That(session.Step(node, last ? choice : RestChoice.Rest), Is.True, $"step to {node}");
                if (session.HasPendingStep) Assert.That(session.Act(ScreenAction.ConfirmStep), Is.True);
                if (!session.HasPendingBattle) continue;
                if (last) WinPending(expedition);
                else WinAndPick(expedition);
            }
        }

        private static void WalkToLayer(Expedition expedition, int layer)
        {
            ExplorationSession session = expedition.Session;
            while (session.State.Profile.Layer < layer)
            {
                WalkToAndWin(expedition, NodeKind.Boss);
                Assert.That(session.Act(ScreenAction.GoToInterlude), Is.True);
                Assert.That(session.Act(ScreenAction.Descend), Is.True);
                Assert.That(session.ConfirmingDescend, Is.False, "the descent does not end the life");
            }
        }

        /// <summary>Breadth-first over the map: the nodes from where the player stands to the nearest unresolved node of this kind, that node last.</summary>
        private static IReadOnlyList<int> PathTo(ExplorationState state, NodeKind kind)
        {
            LayerMap map = state.Map;
            var from = new Dictionary<int, int> { [state.CurrentNodeId] = -1 };
            var queue = new Queue<int>();
            queue.Enqueue(state.CurrentNodeId);
            int target = -1;
            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                if (map.Node(node).Kind == kind && !state.HasResolved(node))
                {
                    target = node;
                    break;
                }
                foreach (int next in map.Neighbours(node))
                {
                    if (from.ContainsKey(next)) continue;
                    from[next] = node;
                    queue.Enqueue(next);
                }
            }
            Assert.That(target, Is.Not.EqualTo(-1), $"a {kind} node is reachable");
            var path = new List<int>();
            for (int node = target; node != state.CurrentNodeId; node = from[node]) path.Add(node);
            path.Reverse();
            return path;
        }
    }
}
