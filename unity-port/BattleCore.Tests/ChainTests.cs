using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// §12 連戦 (#191): HP and current stamina carry into the next battle and nothing else does (the
    /// deck is shuffled afresh, 2026-09-23), the rest between battles, and the tally of a battle.
    /// </summary>
    public class ChainTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        [Test]
        public void ACarriedStart_BeginsWithThatHpAndStamina_ClampedToTheMaximum()
        {
            var deck = PrototypeDeck.Build();
            Assert.Multiple(() =>
            {
                var carried = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 6, PlayerStartHp: 23, PlayerStartStamina: 4), NoShuffle).State;
                Assert.That(carried.Player.Hp, Is.EqualTo(23));
                Assert.That(carried.Player.MaxHp, Is.EqualTo(Constants.PlayerMaxHp));
                Assert.That(carried.Player.Stamina, Is.EqualTo(4));

                var fresh = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 6), NoShuffle).State;
                Assert.That(fresh.Player.Hp, Is.EqualTo(Constants.PlayerMaxHp), "null is full");
                Assert.That(fresh.Player.Stamina, Is.EqualTo(Constants.BaseMaxStamina));

                var clamped = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 6, PlayerStartHp: 99, PlayerStartStamina: -3), NoShuffle).State;
                Assert.That(clamped.Player.Hp, Is.EqualTo(Constants.PlayerMaxHp));
                Assert.That(clamped.Player.Stamina, Is.EqualTo(0));
                Assert.That(new BattleSetup(Enemies.PolearmWarped, deck, 6, PlayerStartHp: 0).StartHp, Is.EqualTo(1), "a carried HP never starts the player fallen");
            });
        }

        [Test]
        public void TheNextBattle_StartsFromAFreshShuffle_WithNoStanceStatusOrGuard()
        {
            // Fight a turn with a stance and a status on, then carry into the next battle.
            var deck = Cards.BuildDeck(new[] { CardCatalog.RockStance, CardCatalog.Focus }, 1);
            for (int i = 0; i < 18; i++) deck.Add(new CardInstance("brace-" + i, CardCatalog.Brace));
            var state = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 6), NoShuffle).State;
            state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
            state = TurnLoop.PlayCard(state, "rock_stance-0", NoShuffle).State;
            state = TurnLoop.PlayCard(state, "focus-0", NoShuffle).State;
            Assert.That(state.Player.StanceList, Has.Count.EqualTo(1));
            Assert.That(state.Exiled, Has.Count.EqualTo(1));

            var (hp, stamina) = Chain.Carry(state);
            var next = TurnLoop.Start(new BattleSetup(Enemies.ShadowHound, deck, 6, PlayerStartHp: hp, PlayerStartStamina: stamina), new SeededRng(3)).State;

            Assert.Multiple(() =>
            {
                Assert.That(next.Player.Hp, Is.EqualTo(state.Player.Hp));
                Assert.That(next.Player.Stamina, Is.EqualTo(state.Player.Stamina));
                Assert.That(next.Player.StanceList, Is.Empty);
                Assert.That(next.Player.Statuses.KindCount, Is.EqualTo(0));
                Assert.That(next.Player.Guard, Is.EqualTo(0));
                Assert.That(next.Exiled, Is.Empty, "the exiled stance card is back in the deck");
                Assert.That(next.DrawPile.Select(c => c.InstanceId), Is.EquivalentTo(deck.Select(c => c.InstanceId)), "the whole deck, shuffled afresh");
                Assert.That(next.Hand, Is.Empty);
                Assert.That(next.DiscardPile, Is.Empty);
            });
        }

        [TestCase(20, 35, 10)]
        [TestCase(40, 50, 10)]
        [TestCase(50, 50, 10)]
        [TestCase(1, 16, 10)]
        public void TheRest_GivesBackThirtyPercentAndFullStamina(int hp, int expectedHp, int expectedStamina)
        {
            // §12 / §3.1: 階層間の休憩 is HP 30% (15 of 50) and stamina full.
            var (restedHp, restedStamina) = Chain.Rest(hp, Constants.PlayerMaxHp, Constants.BaseMaxStamina);
            Assert.That(restedHp, Is.EqualTo(expectedHp));
            Assert.That(restedStamina, Is.EqualTo(expectedStamina));
        }

        [Test]
        public void TheTally_CountsTheCardsTheirAttributesAndTheTraitsThatHeld()
        {
            // §12's result items, counted from the events of a battle fought to the end.
            var rng = new SeededRng(11);
            var start = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), 6), rng);
            var state = start.State;
            var events = new List<BattleEvent>(start.Events);
            while (state.Result == GameResult.Ongoing && state.Turn < 60)
            {
                var step = TurnLoop.BeginPlayerTurn(state, rng);
                events.AddRange(step.Events);
                state = step.State;
                while (state.Result == GameResult.Ongoing)
                {
                    var card = state.Hand.FirstOrDefault(c => TurnLoop.CanPlay(state, c.InstanceId) == PlayRefusal.None);
                    if (card == null) break;
                    step = TurnLoop.PlayCard(state, card.InstanceId, rng);
                    events.AddRange(step.Events);
                    state = step.State;
                }
                if (state.Result != GameResult.Ongoing) break;
                step = TurnLoop.EndTurn(state, rng);
                events.AddRange(step.Events);
                state = step.State;
            }

            var tally = Chain.Tally(state, events);
            var played = events.OfType<CardPlayed>().ToList();
            Assert.Multiple(() =>
            {
                Assert.That(tally.EnemyId, Is.EqualTo("polearm_warped"));
                Assert.That(tally.Result, Is.EqualTo(state.Result));
                Assert.That(tally.Turns, Is.EqualTo(state.Turn));
                Assert.That(tally.HpLeft, Is.EqualTo(state.Player.Hp));
                Assert.That(tally.CardsPlayed, Is.EqualTo(played.Count));
                Assert.That(tally.CountOf(BattleAttribute.Attack), Is.EqualTo(played.Count(p => p.Card.Def.Attribute == BattleAttribute.Attack)));
                Assert.That(tally.CountOf(BattleAttribute.Guard), Is.EqualTo(played.Count(p => p.Card.Def.Attribute == BattleAttribute.Guard)));
                Assert.That(tally.TraitsFired, Is.EqualTo(events.OfType<TraitEvaluated>().Count(t => t.Actor == Actor.Player && t.Outcome.Triggered)));
                Assert.That(tally.TraitsFired, Is.GreaterThan(0));
                Assert.That(tally.Attributes.Values.Sum(), Is.EqualTo(tally.CardsPlayed), "a card counts once, as the one attribute it folds to (v4.4)");
            });
        }

        [Test]
        public void TheTally_CountsATwoFaceCardOnce_AsTheAttributeItFoldsTo()
        {
            // battle_core_v4 §2.1 / §24.1 (v4.4, #304): one card, one attribute, 攻撃 ＞ 防御 ＞ スキル.
            // An 攻撃 + 防御 card is one 攻撃, a 防御 + スキル card one 防御, and a card that only moves
            // with a Guard on it one 防御. The enemy's cards are not the player's tally.
            var strikeAndBrace = Fixtures.Card("strike_brace", attributes: BattleAttribute.Attack | BattleAttribute.Guard,
                face: new Face(Power: 4, Guard: 3));
            var braceAndDraw = Fixtures.Card("brace_draw", attributes: BattleAttribute.Guard | BattleAttribute.Skill,
                face: new Face(Guard: 3, Draw: 1));
            var stepBack = Fixtures.Card("step_back", attributes: BattleAttribute.None, face: new Face(Guard: 4, Move: -1));
            var foeStrike = Fixtures.Card("foe_strike", attributes: BattleAttribute.Attack | BattleAttribute.Guard,
                face: new Face(Power: 4, Guard: 3));

            var state = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), 6), NoShuffle).State;
            var events = new List<BattleEvent>
            {
                new CardPlayed(Actor.Player, new CardInstance("strike_brace-0", strikeAndBrace), 2),
                new CardPlayed(Actor.Player, new CardInstance("brace_draw-0", braceAndDraw), 2),
                new CardPlayed(Actor.Player, new CardInstance("step_back-0", stepBack), 2),
                new CardPlayed(Actor.Enemy, new CardInstance("foe_strike-0", foeStrike), 2),
            };

            var tally = Chain.Tally(state, events);
            var total = Chain.Total(new[] { tally, tally });
            Assert.Multiple(() =>
            {
                Assert.That(tally.CardsPlayed, Is.EqualTo(3));
                Assert.That(tally.CountOf(BattleAttribute.Attack), Is.EqualTo(1));
                Assert.That(tally.CountOf(BattleAttribute.Guard), Is.EqualTo(2));
                Assert.That(tally.CountOf(BattleAttribute.Skill), Is.EqualTo(0), "the skill face of 防御 + スキル is not counted again");
                Assert.That(tally.CountOf(BattleAttribute.Stance), Is.EqualTo(0));
                Assert.That(tally.Attributes.Values.Sum(), Is.EqualTo(tally.CardsPlayed));
                Assert.That(total.CountOf(BattleAttribute.Attack), Is.EqualTo(2));
                Assert.That(total.CountOf(BattleAttribute.Guard), Is.EqualTo(4));
                Assert.That(total.Attributes.Values.Sum(), Is.EqualTo(total.CardsPlayed), "a chain adds up the same way");
            });
        }
    }
}
