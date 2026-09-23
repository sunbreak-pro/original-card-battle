// #192: the corners a battle must pass without stopping — a turn with nothing to play, a draw pile
// that runs out, a hand that reaches nobody, no stamina left — driven through CoreBattleSource the
// way the screen drives it.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class LoopEdgeTests
    {
        private static List<CardInstance> Copies(CardDef def, int count)
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < count; i++) deck.Add(new CardInstance(def.Id + "-" + i, def));
            return deck;
        }

        /// <summary>Runs until the player waits (or the battle is over) and returns what was played on the way.</summary>
        private static List<DepictionEvent> ToThePlayer(CoreBattleSource source)
        {
            var events = new List<DepictionEvent>();
            while (!source.Finished && !source.WaitingForPlayer) events.Add(source.AdvanceAuto());
            return events;
        }

        [Test]
        public void ATurnWithNothingToPlay_EndsAndTheBattleGoesOn()
        {
            // Twenty 突き (cost 3, reach 0〜1) at the opening gap of 3: nothing reaches.
            var source = new CoreBattleSource(new BattleSetup(Enemies.PolearmWarped, Copies(CardCatalog.Thrust, 20), 6), 1, suggestCards: true);
            ToThePlayer(source);

            Assert.Multiple(() =>
            {
                Assert.That(source.WaitingForPlayer, Is.True);
                Assert.That(source.Frame.Hand.All(f => source.Inspect(f.Id) != PlayVerdict.Accepted), Is.True);
                Assert.That(source.SuggestedCardId, Is.EqualTo(""));
                Assert.That(source.GuideText, Is.EqualTo("出せる札がありません。「ターン終了」を押してください"));
                Assert.That(source.CanEndTurn, Is.True);
            });

            int turn = source.State.Turn;
            source.EndTurn();
            ToThePlayer(source);
            Assert.That(source.State.Turn, Is.EqualTo(turn + 1));
            Assert.That(source.WaitingForPlayer || source.Finished, Is.True);
        }

        [Test]
        public void ARunOutDrawPile_IsRefilledFromTheDiscardPile()
        {
            // 20 cards, 5 a turn: the fifth turn start finds the draw pile empty and reshuffles.
            var deck = Copies(CardCatalog.Brace, 20);
            var source = new CoreBattleSource(new BattleSetup(Enemies.PolearmWarped, deck, 6), 2, suggestCards: false);
            int reshuffles = 0;
            for (int turn = 1; turn <= 6 && !source.Finished; turn++)
            {
                ToThePlayer(source);
                if (source.Finished) break;
                BattleState state = source.State;
                int cards = state.Hand.Count + state.DrawPile.Count + state.DiscardPile.Count + state.Exiled.Count;
                Assert.That(cards, Is.EqualTo(20), "turn " + turn + ": no card lost or doubled");
                Assert.That(state.Hand, Has.Count.EqualTo(Constants.HandDraw), "turn " + turn + ": a full hand every turn");
                reshuffles = source.History.OfType<DeckReshuffled>().Count();
                source.EndTurn();
            }
            Assert.That(reshuffles, Is.GreaterThanOrEqualTo(1), "the discard pile came back as the draw pile");
        }

        [Test]
        public void AHandThatReachesNobody_NeverStopsTheBattle()
        {
            // A deck of nothing but 投げ刃 (reach 1〜3) against 瘴牙の走竜, starting adjacent: its gap-0 branch bites and never steps away,
            // so the whole hand reaches nobody every turn, and the battle must still run to its end.
            var source = new CoreBattleSource(new BattleSetup(Enemies.ShadowHound, Copies(CardCatalog.ThrowBlade, 20), 6, StartGap: 0), 4, suggestCards: true);
            int turnsWithNothing = 0;
            int guard = 0;
            while (!source.Finished && guard++ < 3000)
            {
                if (!source.WaitingForPlayer)
                {
                    source.AdvanceAuto();
                    continue;
                }
                string card = source.SuggestedCardId;
                if (card.Length == 0)
                {
                    if (source.Frame.Hand.Count > 0 && source.Frame.Hand.All(f => source.Inspect(f.Id) == PlayVerdict.OutOfRange)) turnsWithNothing++;
                    source.EndTurn();
                    continue;
                }
                CardFace face = DepictionText.Find(source.Frame.Hand, card);
                DepictionEvent played;
                source.TryPlay(card, DepictionText.RequiredZone(face.Aim), out played);
            }
            Assert.That(source.Finished, Is.True);
            Assert.That(source.State.Result, Is.Not.EqualTo(GameResult.Ongoing));
            Assert.That(turnsWithNothing, Is.GreaterThan(0), "the scenario did reach a hand that reaches nobody");
        }

        [Test]
        public void NoStaminaLeft_TheTurnStillEnds_AndRecoveryComesBack()
        {
            var source = new CoreBattleSource(new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), 6, PlayerStartStamina: 0), 5, suggestCards: true);
            ToThePlayer(source);
            // Turn 1 recovers 3 from 0: the leftmost payable cards spend it, then nothing is payable.
            while (source.SuggestedCardId.Length > 0)
            {
                string card = source.SuggestedCardId;
                CardFace face = DepictionText.Find(source.Frame.Hand, card);
                DepictionEvent played;
                source.TryPlay(card, DepictionText.RequiredZone(face.Aim), out played);
                ToThePlayer(source);
                if (source.Finished) return;
            }
            Assert.That(source.CanEndTurn, Is.True);
            int stamina = source.State.Player.Stamina;
            source.EndTurn();
            ToThePlayer(source);
            if (source.Finished) return;
            Assert.That(source.State.Player.Stamina, Is.GreaterThan(stamina), "the next turn start recovers");
        }

        [Test]
        public void TheWholeDemo_EveryEnemyOnceMore_WithTheProtoypeDeck_EndsWithinTheLimit()
        {
            // The prototype deck (no closer guaranteed) against every enemy, a few seeds: no stall either.
            foreach (EnemyDef enemy in Enemies.All)
            {
                for (int seed = 1; seed <= 5; seed++)
                {
                    CoreBattleSource source = SweepTests.Fight(enemy, PrototypeDeck.Build(), seed);
                    Assert.That(source, Is.Not.Null, enemy.Id + " seed " + seed + " stalled");
                }
            }
        }
    }
}
