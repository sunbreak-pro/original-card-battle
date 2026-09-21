using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// §9 end to end. The pass condition is the one written in
    /// vision/plans/2026-09-21-vertical-slice-polearm.md 「縦切りの合格条件」: the seventeen events in
    /// order, the eight numbers, and the three end points — then the same thing three turns running
    /// under a fixed seed.
    /// </summary>
    public class TurnLoopTests
    {
        private const int Seed = 20260921;

        // ---- Helpers ----

        /// <summary>A battle whose draw order is the deck order: FixedRng(0.999…) makes Fisher-Yates a no-op.</summary>
        private static StepResult StartUnshuffled(params CardDef[] kinds)
        {
            var deck = Cards.BuildDeck(kinds, copies: 1);
            return TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck), new FixedRng(0.9999999));
        }

        private static readonly IRng NoRng = new FixedRng(0.9999999);

        private static string InHand(BattleState state, string cardId) =>
            state.Hand.First(c => c.Def.Id == cardId).InstanceId;

        private static List<Type> TypesOf(IEnumerable<BattleEvent> events) => events.Select(e => e.GetType()).ToList();

        /// <summary>Asserts that <paramref name="expected"/> appears in <paramref name="actual"/> in this order (gaps allowed).</summary>
        private static void AssertInOrder(IReadOnlyList<BattleEvent> actual, params Type[] expected)
        {
            int cursor = 0;
            foreach (var type in expected)
            {
                int found = -1;
                for (int i = cursor; i < actual.Count; i++)
                {
                    if (actual[i].GetType() == type) { found = i; break; }
                }
                Assert.That(found, Is.GreaterThanOrEqualTo(0),
                    $"{type.Name} is missing after index {cursor} in: {string.Join(", ", actual.Select(e => e.GetType().Name))}");
                cursor = found + 1;
            }
        }

        // ---- Start ----

        [Test]
        public void Start_UsesTheProvisionalValues_AndDecidesTheFirstOmen()
        {
            var start = TurnLoop.Start(BattleSetup.Slice(), new SeededRng(Seed));
            var s = start.State;

            Assert.Multiple(() =>
            {
                Assert.That(s.Turn, Is.EqualTo(0));
                Assert.That(s.Phase, Is.EqualTo(BattlePhase.AwaitingTurnStart));
                Assert.That(s.Player.Hp, Is.EqualTo(50));
                Assert.That(s.Player.Stamina, Is.EqualTo(10));
                Assert.That(s.Player.Guard, Is.EqualTo(0));
                Assert.That(s.Player.Position, Is.EqualTo(Position.Near));
                Assert.That(s.Enemy.Hp, Is.EqualTo(60));
                Assert.That(s.Enemy.Stamina, Is.EqualTo(10));
                Assert.That(s.Enemy.Position, Is.Null);
                Assert.That(s.Hand, Is.Empty);
                Assert.That(s.DrawPile, Has.Count.EqualTo(20));
                // Near start → the first omen is the shove (plan: 開始位置 近間).
                Assert.That(s.Omen!.ActionId, Is.EqualTo("shove"));
                Assert.That(start.Events.Single(), Is.EqualTo(new OmenSet(Actor.Enemy, s.Omen, Decided: true)));
            });
        }

        // ---- The seventeen, in order ----

        [Test]
        public void OneTurn_EmitsTheSeventeenInOrder()
        {
            var rng = new SeededRng(Seed);
            var state = TurnLoop.Start(BattleSetup.Slice(), rng).State;

            var begin = TurnLoop.BeginPlayerTurn(state, rng);
            var traitCard = begin.State.Hand.First(c => c.Def.Trait != null && Combat.CanPay(c.Def.Cost, 10));
            var play = TurnLoop.PlayCard(begin.State, traitCard.InstanceId, rng);
            var end = TurnLoop.EndTurn(play.State, rng);

            var all = begin.Events.Concat(play.Events).Concat(end.Events).ToList();

            AssertInOrder(all,
                typeof(TurnStarted),        // 1
                typeof(GuardCleared),       // 2
                typeof(StaminaRecovered),   // 3
                typeof(Drawn), typeof(Drawn), typeof(Drawn), typeof(Drawn), typeof(Drawn), // 5
                typeof(OmenSet),            // 6
                typeof(CardPlayed),         // 7
                typeof(StaminaSpent),
                typeof(TraitEvaluated),     // 8
                typeof(FaceResolved),       // 9
                typeof(DefeatChecked),      // 10
                typeof(ReserveChecked),     // 11
                typeof(HandDiscarded),      // 12
                typeof(TurnEnded),          // 13
                typeof(GuardCleared),       // 14
                typeof(StaminaRecovered),
                typeof(ActionExecuted),     // 15
                typeof(DefeatChecked),      // 16
                typeof(OmenSet));           // 17

            Assert.That(all.OfType<Drawn>().Count(), Is.EqualTo(5));
            Assert.That(all.Last(), Is.InstanceOf<OmenSet>(), "the turn closes on the next omen");
            Assert.That(end.Events.OfType<OmenSet>().Count(), Is.EqualTo(1));
            Assert.That(((OmenSet)all.Last()).Decided, Is.True);
        }

        [Test]
        public void BeginPlayerTurn_IsStepsOneToFive_Exactly()
        {
            var state = StartUnshuffled(CardCatalog.All.ToArray()).State;
            var begin = TurnLoop.BeginPlayerTurn(state, NoRng);

            Assert.That(TypesOf(begin.Events), Is.EqualTo(new[]
            {
                typeof(TurnStarted), typeof(GuardCleared), typeof(StaminaRecovered),
                typeof(Drawn), typeof(Drawn), typeof(Drawn), typeof(Drawn), typeof(Drawn),
                typeof(OmenSet),
            }));
            Assert.That(((OmenSet)begin.Events.Last()).Decided, Is.False, "step 5 shows the standing omen");
            Assert.That(begin.State.Phase, Is.EqualTo(BattlePhase.PlayerAction));
            Assert.That(begin.State.Turn, Is.EqualTo(1));
        }

        [Test]
        public void TheTraitIsJudgedBeforeAnyFace_AndFacesComeInOrder()
        {
            // boar_rush is Attack + Move with a trait: trait → attack → move.
            var state = StartUnshuffled(CardCatalog.BoarRush, CardCatalog.Thrust, CardCatalog.Brace,
                CardCatalog.KesaCut, CardCatalog.Feint).State;
            state = TurnLoop.BeginPlayerTurn(state, NoRng).State;

            var play = TurnLoop.PlayCard(state, InHand(state, "boar_rush"), NoRng);

            Assert.That(TypesOf(play.Events), Is.EqualTo(new[]
            {
                typeof(CardPlayed), typeof(StaminaSpent), typeof(TraitEvaluated),
                typeof(FaceResolved), typeof(DamageDealt),
                typeof(FaceResolved),
                typeof(DefeatChecked),
            }));
            var faces = play.Events.OfType<FaceResolved>().Select(f => f.Face).ToList();
            Assert.That(faces, Is.EqualTo(new[] { BattleAttribute.Attack, BattleAttribute.Move }));
        }

        [Test]
        public void ACardWithoutATrait_EmitsNoTraitEvaluated()
        {
            var state = StartUnshuffled(CardCatalog.Thrust, CardCatalog.Brace, CardCatalog.KesaCut,
                CardCatalog.Feint, CardCatalog.BoarRush).State;
            state = TurnLoop.BeginPlayerTurn(state, NoRng).State;

            var play = TurnLoop.PlayCard(state, InHand(state, "thrust"), NoRng);
            Assert.That(play.Events.OfType<TraitEvaluated>(), Is.Empty);
        }

        // ---- The eight numbers ----

        [Test]
        public void Number1_Recovery_PlayerThreeCappedAtTen_EnemyTwo()
        {
            var state = StartUnshuffled(CardCatalog.All.ToArray()).State;

            var begin = TurnLoop.BeginPlayerTurn(state, NoRng);
            var capped = begin.Events.OfType<StaminaRecovered>().Single();
            Assert.That(capped, Is.EqualTo(new StaminaRecovered(Actor.Player, 0, 10, 10)), "already full");

            // Spend 3 + 3 + 2, end the turn, and the next turn gives the 3 back.
            var s = begin.State;
            s = TurnLoop.PlayCard(s, InHand(s, "thrust"), NoRng).State;
            s = TurnLoop.PlayCard(s, InHand(s, "kesa_cut"), NoRng).State;
            var end = TurnLoop.EndTurn(s, NoRng);

            var enemy = end.Events.OfType<StaminaRecovered>().Single();
            Assert.That(enemy.Actor, Is.EqualTo(Actor.Enemy));
            Assert.That(enemy.Amount, Is.EqualTo(0), "the polearm also starts full");

            var next = TurnLoop.BeginPlayerTurn(end.State, NoRng);
            Assert.That(next.Events.OfType<StaminaRecovered>().Single(),
                Is.EqualTo(new StaminaRecovered(Actor.Player, 3, 8, 10)));

            // The enemy paid 2 for the shove on turn 1, so its turn-2 recovery is a real +2.
            var end2 = TurnLoop.EndTurn(next.State, NoRng);
            Assert.That(end2.Events.OfType<StaminaRecovered>().Single(),
                Is.EqualTo(new StaminaRecovered(Actor.Enemy, 2, 10, 10)));
        }

        [Test]
        public void Number2_StaminaFalls_ByExactlyTheSumOfTheColumns()
        {
            var state = StartUnshuffled(CardCatalog.All.ToArray()).State;
            var s = TurnLoop.BeginPlayerTurn(state, NoRng).State;

            // Deck order: thrust(3) kesa_cut(2) reach_thrust(2) brace(2) feint(2).
            var played = new[] { "thrust", "kesa_cut", "brace" };
            foreach (var id in played) s = TurnLoop.PlayCard(s, InHand(s, id), NoRng).State;

            Assert.That(s.Player.Stamina, Is.EqualTo(10 - (3 + 2 + 2)));
            Assert.That(s.Hand, Has.Count.EqualTo(2));
            Assert.That(s.DiscardPile.Select(c => c.Def.Id), Is.EqualTo(played));
        }

        [Test]
        public void Number2_AnUnpayableCard_IsRefused()
        {
            var state = StartUnshuffled(CardCatalog.All.ToArray()).State;
            var s = TurnLoop.BeginPlayerTurn(state, NoRng).State;
            foreach (var id in new[] { "thrust", "kesa_cut", "reach_thrust", "brace" })
            {
                s = TurnLoop.PlayCard(s, InHand(s, id), NoRng).State;
            }

            Assert.That(s.Player.Stamina, Is.EqualTo(1));
            string feint = InHand(s, "feint");
            Assert.That(TurnLoop.CanPlay(s, feint), Is.EqualTo(PlayRefusal.NotEnoughStamina));
            Assert.That(() => TurnLoop.PlayCard(s, feint, NoRng), Throws.InvalidOperationException);
            Assert.That(TurnLoop.CanPlay(s, "nothing-0"), Is.EqualTo(PlayRefusal.NotInHand));
        }

        [Test]
        public void Number3_Reserve_OnlyWithThreeOrMoreLeft()
        {
            var state = StartUnshuffled(CardCatalog.All.ToArray()).State;
            var s = TurnLoop.BeginPlayerTurn(state, NoRng).State;

            // 10 − 3 − 2 − 2 = 3 left → Guard +3.
            var rich = s;
            foreach (var id in new[] { "thrust", "kesa_cut", "reach_thrust" }) rich = TurnLoop.PlayCard(rich, InHand(rich, id), NoRng).State;
            var richEnd = TurnLoop.EndTurn(rich, NoRng);
            Assert.That(richEnd.Events.OfType<ReserveChecked>().First(),
                Is.EqualTo(new ReserveChecked(Actor.Player, 3, 3, 3)));

            // One more 2-cost card → 1 left → nothing. (brace, not feint: a fourth attack would end the battle.)
            var poor = TurnLoop.PlayCard(rich, InHand(rich, "brace"), NoRng).State;
            var poorEnd = TurnLoop.EndTurn(poor, NoRng);
            var check = poorEnd.Events.OfType<ReserveChecked>().First();
            Assert.That(check, Is.EqualTo(new ReserveChecked(Actor.Player, 1, 0, 9)), "brace's 9 stands; 構え adds nothing");
        }

        [Test]
        public void Number3_TheEnemyTakesItsReserveToo()
        {
            var state = StartUnshuffled(CardCatalog.All.ToArray()).State;
            var s = TurnLoop.BeginPlayerTurn(state, NoRng).State;
            var end = TurnLoop.EndTurn(s, NoRng);

            var enemyCheck = end.Events.OfType<ReserveChecked>().Last();
            Assert.That(enemyCheck, Is.EqualTo(new ReserveChecked(Actor.Enemy, 8, 3, 3)));
            Assert.That(end.State.Enemy.Guard, Is.EqualTo(3), "it stands through the player's next turn");
        }

        [Test]
        public void Number4_TheOmenBranch_FollowsThePlayerSide()
        {
            var near = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), Position.Near), NoRng);
            var far = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), Position.Far), NoRng);

            Assert.That(near.State.Omen!.ActionId, Is.EqualTo("shove"));
            Assert.That(far.State.Omen!.ActionId, Is.EqualTo("sweep"));
        }

        [Test]
        public void Number5_TheSweep_IsElevenFromFar_AndEightFromNear()
        {
            // Start far so the omen is the sweep. Staying far eats 11.
            var setup = new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), Position.Far);
            var stay = TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoRng).State, NoRng).State;
            var stayEnd = TurnLoop.EndTurn(stay, NoRng);
            var eleven = stayEnd.Events.OfType<DamageDealt>().Single();
            // The player kept 10 stamina, so the 構え Guard 3 soaks part of it: 11 − 3 = 8.
            Assert.That(eleven, Is.EqualTo(new DamageDealt(Actor.Enemy, Actor.Player, 11, 3, 8, 0, 42)));

            // Stepping near with step_in_guard: the committed sweep still comes, without its +3.
            var deck = Cards.BuildDeck(new[] { CardCatalog.StepInGuard, CardCatalog.Thrust, CardCatalog.KesaCut,
                CardCatalog.Brace, CardCatalog.Feint }, 1);
            var s = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, Position.Far), NoRng).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
            s = TurnLoop.PlayCard(s, InHand(s, "step_in_guard"), NoRng).State;
            var end = TurnLoop.EndTurn(s, NoRng);

            Assert.That(end.Events.OfType<ActionExecuted>().Single().Action.Id, Is.EqualTo("sweep"));
            Assert.That(end.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(8));
        }

        [Test]
        public void Number6_TheShove_IsEightAgainstNoGuard_FiveAgainstGuard_AndPushesEitherWay()
        {
            // No Guard: 3 stamina, then feint (2) leaves 1 — too little for its 温存 and for 構え.
            var state = StartUnshuffled(CardCatalog.All.ToArray()).State;
            var bare = TurnLoop.BeginPlayerTurn(state, NoRng).State;
            bare = bare with { Player = bare.Player with { Stamina = 3 } };
            bare = TurnLoop.PlayCard(bare, InHand(bare, "feint"), NoRng).State;
            Assert.That(bare.Player.Guard, Is.EqualTo(0));
            Assert.That(bare.Player.Position, Is.EqualTo(Position.Far));
            // The shove was committed while the player stood near, so it still comes.
            var bareEnd = TurnLoop.EndTurn(bare, NoRng);

            Assert.That(bareEnd.Events.OfType<ActionExecuted>().Single().Action.Id, Is.EqualTo("shove"));
            Assert.That(bareEnd.Events.OfType<DamageDealt>().Single(),
                Is.EqualTo(new DamageDealt(Actor.Enemy, Actor.Player, 8, 0, 8, 0, 42)));
            Assert.That(bareEnd.Events.OfType<PositionChanged>().Single(),
                Is.EqualTo(new PositionChanged(Actor.Player, Position.Far, Position.Near, Pushed: true)),
                "the push flips whichever side the player is on");

            // With Guard: brace (9). The +3 is gone, the push is not.
            var guarded = TurnLoop.BeginPlayerTurn(state, NoRng).State;
            foreach (var id in new[] { "thrust", "kesa_cut", "reach_thrust", "brace" }) guarded = TurnLoop.PlayCard(guarded, InHand(guarded, id), NoRng).State;
            var guardedEnd = TurnLoop.EndTurn(guarded, NoRng);

            Assert.That(guardedEnd.Events.OfType<DamageDealt>().Single(),
                Is.EqualTo(new DamageDealt(Actor.Enemy, Actor.Player, 5, 5, 0, 4, 50)));
            Assert.That(guardedEnd.Events.OfType<PositionChanged>().Single(),
                Is.EqualTo(new PositionChanged(Actor.Player, Position.Near, Position.Far, Pushed: true)));
        }

        [Test]
        public void Number7_Damage_IsFacePlusTraitMinusGuard_AndNeverBelowZero()
        {
            var state = StartUnshuffled(CardCatalog.All.ToArray()).State;
            var s = TurnLoop.BeginPlayerTurn(state, NoRng).State;

            // Near: kesa_cut is 13 + 5 against Guard 0.
            var kesa = TurnLoop.PlayCard(s, InHand(s, "kesa_cut"), NoRng);
            Assert.That(kesa.Events.OfType<DamageDealt>().Single(),
                Is.EqualTo(new DamageDealt(Actor.Player, Actor.Enemy, 18, 0, 18, 0, 42)));

            // Near: reach_thrust gets nothing for being far.
            var reach = TurnLoop.PlayCard(s, InHand(s, "reach_thrust"), NoRng);
            Assert.That(reach.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(13));

            // Turn 2: the polearm holds its 構え Guard 3, so 13 lands as 10.
            var end = TurnLoop.EndTurn(s, NoRng);
            var t2 = TurnLoop.BeginPlayerTurn(end.State, NoRng).State;
            Assert.That(t2.Enemy.Guard, Is.EqualTo(3));
            Assert.That(t2.Player.Position, Is.EqualTo(Position.Far), "shoved on turn 1");
            // Turn 2, from far: body_check is a bare 4 (no 重撃), and the polearm's 構え Guard 3 soaks 3 of it.
            var intoGuard = TurnLoop.PlayCard(t2, InHand(t2, "body_check"), NoRng);
            Assert.That(intoGuard.Events.OfType<DamageDealt>().Single(),
                Is.EqualTo(new DamageDealt(Actor.Player, Actor.Enemy, 4, 3, 1, 0, 59)));
        }

        [Test]
        public void Number7_GuardSoaksFirst_ThenHp()
        {
            var deck = Cards.BuildDeck(new[] { CardCatalog.BodyCheck, CardCatalog.Thrust, CardCatalog.KesaCut,
                CardCatalog.Brace, CardCatalog.Feint, CardCatalog.ReachThrust, CardCatalog.ShieldBash,
                CardCatalog.BoarRush, CardCatalog.StepInGuard, CardCatalog.StepOutGuard }, 1);
            var s = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck), NoRng).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
            s = TurnLoop.EndTurn(s, NoRng).State;            // enemy now holds Guard 3; player was pushed far
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;

            // From far, reach_thrust is 13 + 5: Guard 3 soaks 3 and 15 goes through.
            Assert.That(s.Player.Position, Is.EqualTo(Position.Far));
            var hit = TurnLoop.PlayCard(s, InHand(s, "reach_thrust"), NoRng);
            Assert.That(hit.Events.OfType<DamageDealt>().Single(),
                Is.EqualTo(new DamageDealt(Actor.Player, Actor.Enemy, 18, 3, 15, 0, 45)));
        }

        [Test]
        public void Number8_TheHand_IsZeroAtTurnEnd_AndFiveAtTheNextStart()
        {
            var rng = new SeededRng(Seed);
            var s = TurnLoop.Start(BattleSetup.Slice(), rng).State;
            s = TurnLoop.BeginPlayerTurn(s, rng).State;
            Assert.That(s.Hand, Has.Count.EqualTo(5));

            var end = TurnLoop.EndTurn(s, rng);
            Assert.That(end.State.Hand, Is.Empty);
            Assert.That(end.Events.OfType<HandDiscarded>().Single().Count, Is.EqualTo(5));
            Assert.That(end.State.DiscardPile, Has.Count.EqualTo(5));

            var next = TurnLoop.BeginPlayerTurn(end.State, rng);
            Assert.That(next.State.Hand, Has.Count.EqualTo(5));
        }

        // ---- The three end points ----

        [Test]
        public void EndPoint_PushedFar_SoTheNextOmenIsTheSweep()
        {
            // The core of the slice: position, push, the tree and the omen in one line.
            var rng = new SeededRng(Seed);
            var s = TurnLoop.Start(BattleSetup.Slice(), rng).State;
            Assert.That(s.Omen!.Label.ToText(), Is.EqualTo("攻撃・近"));

            s = TurnLoop.BeginPlayerTurn(s, rng).State;
            var end = TurnLoop.EndTurn(s, rng);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<PositionChanged>().Single().To, Is.EqualTo(Position.Far));
                Assert.That(end.State.Player.Position, Is.EqualTo(Position.Far));

                var omens = end.Events.OfType<OmenSet>().ToList();
                Assert.That(omens, Has.Count.EqualTo(1));
                Assert.That(omens[0].Omen.ActionId, Is.EqualTo("sweep"));
                Assert.That(omens[0].Omen.Label.ToText(), Is.EqualTo("攻撃・遠"));
                Assert.That(end.State.Omen, Is.EqualTo(omens[0].Omen));

                Assert.That(end.State.Result, Is.EqualTo(GameResult.Ongoing));
                Assert.That(end.State.Player.Hp, Is.GreaterThan(0));
                Assert.That(end.State.Enemy.Hp, Is.GreaterThan(0));
                Assert.That(end.State.Phase, Is.EqualTo(BattlePhase.AwaitingTurnStart));
            });
        }

        // ---- Statuses, moves, reshuffle ----

        [Test]
        public void Slow_LandsOnThePolearm_TicksAtItsTurnStart_AndStopsNothing()
        {
            var deck = Cards.BuildDeck(new[] { CardCatalog.BodyCheck, CardCatalog.Thrust, CardCatalog.KesaCut,
                CardCatalog.Brace, CardCatalog.Feint }, 1);
            var s = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck), NoRng).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;

            var play = TurnLoop.PlayCard(s, InHand(s, "body_check"), NoRng);
            Assert.That(play.Events.OfType<StatusApplied>().Single(),
                Is.EqualTo(new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Slow, 2, 2, false)));
            // Near → 重撃: 4 + 6, and the next recovery is one short.
            Assert.That(play.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(10));
            Assert.That(play.State.Player.NextTurnRecoveryBonus, Is.EqualTo(-1));

            var end = TurnLoop.EndTurn(play.State, NoRng);
            Assert.That(end.Events.OfType<StatusTicked>().Single(),
                Is.EqualTo(new StatusTicked(Actor.Enemy, StatusKind.Slow, 1)));
            // #116: the polearm has no position to pin, so the shove still pushes.
            Assert.That(end.Events.OfType<PositionChanged>().Single().Pushed, Is.True);

            var next = TurnLoop.BeginPlayerTurn(end.State, NoRng);
            // 10 − 1 = 9 left; 重撃 makes the recovery 3 − 1 = 2, capped at 10 → +1.
            Assert.That(next.Events.OfType<StaminaRecovered>().Single(),
                Is.EqualTo(new StaminaRecovered(Actor.Player, 1, 10, 10)));
            Assert.That(next.State.Player.NextTurnRecoveryBonus, Is.EqualTo(0));
        }

        [Test]
        public void ASlowedPlayer_PlaysTheMoveCard_ButDoesNotSwitchSides()
        {
            var s = StartUnshuffled(CardCatalog.Feint, CardCatalog.Thrust, CardCatalog.KesaCut,
                CardCatalog.Brace, CardCatalog.BoarRush).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
            s = s with { Player = s.Player with { Statuses = StatusSet.Of((StatusKind.Slow, 1)) } };

            var play = TurnLoop.PlayCard(s, InHand(s, "feint"), NoRng);

            Assert.That(play.Events.OfType<MoveBlocked>().Single(), Is.EqualTo(new MoveBlocked(Actor.Player, StatusKind.Slow)));
            Assert.That(play.Events.OfType<PositionChanged>(), Is.Empty);
            Assert.That(play.State.Player.Position, Is.EqualTo(Position.Near));
            Assert.That(play.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(8), "the attack face still lands");
        }

        [Test]
        public void ADirectedMove_FromItsOwnSide_EmitsNoPositionChange()
        {
            var s = StartUnshuffled(CardCatalog.StepInGuard, CardCatalog.Thrust, CardCatalog.KesaCut,
                CardCatalog.Brace, CardCatalog.BoarRush).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;

            var play = TurnLoop.PlayCard(s, InHand(s, "step_in_guard"), NoRng);

            Assert.That(play.Events.OfType<PositionChanged>(), Is.Empty);
            Assert.That(play.Events.OfType<GuardGained>().Single(), Is.EqualTo(new GuardGained(Actor.Player, 12, 12)));
        }

        [Test]
        public void AGuardTrait_LandsAsGuard_OnACardWithoutAGuardFace()
        {
            // feint is Attack + Move; its 温存 (残 ≥ 4) gives Guard +3 all the same.
            var s = StartUnshuffled(CardCatalog.Feint, CardCatalog.Thrust, CardCatalog.KesaCut,
                CardCatalog.Brace, CardCatalog.BoarRush).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;

            var play = TurnLoop.PlayCard(s, InHand(s, "feint"), NoRng);

            Assert.That(play.Events.OfType<GuardGained>().Single(), Is.EqualTo(new GuardGained(Actor.Player, 3, 3)));
            Assert.That(play.Events.OfType<FaceResolved>().Select(f => f.Face),
                Is.EqualTo(new[] { BattleAttribute.Attack, BattleAttribute.Move }));
            Assert.That(play.State.Player.Position, Is.EqualTo(Position.Far));
        }

        [Test]
        public void TheDrawPileRunsOut_OnTurnFive_AndTheDiscardPileComesBack()
        {
            var rng = new SeededRng(Seed);
            var s = TurnLoop.Start(BattleSetup.Slice(), rng).State;

            for (int turn = 1; turn <= 4; turn++)
            {
                var begin = TurnLoop.BeginPlayerTurn(s, rng);
                Assert.That(begin.Events.OfType<DeckReshuffled>(), Is.Empty, $"turn {turn}");
                // Ending each turn without playing: 20 cards last exactly four hands of five.
                s = TurnLoop.EndTurn(begin.State, rng).State;
            }

            var fifth = TurnLoop.BeginPlayerTurn(s, rng);
            Assert.That(fifth.Events.OfType<DeckReshuffled>().Single().DrawPileCount, Is.EqualTo(20));
            Assert.That(fifth.State.Hand, Has.Count.EqualTo(5));
            Assert.That(fifth.State.DrawPile, Has.Count.EqualTo(15));
            Assert.That(fifth.State.DiscardPile, Is.Empty);
        }

        // ---- The end of the battle ----

        [Test]
        public void TheBattleEnds_TheMomentTheEnemyReachesZero()
        {
            var s = StartUnshuffled(CardCatalog.BoarRush, CardCatalog.Thrust, CardCatalog.KesaCut,
                CardCatalog.Brace, CardCatalog.Feint).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
            s = s with { Enemy = s.Enemy with { Hp = 10 } };

            // boar_rush from near is a bare 14: the attack face kills, so the move face never runs.
            var play = TurnLoop.PlayCard(s, InHand(s, "boar_rush"), NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(play.State.Enemy.Hp, Is.EqualTo(0));
                Assert.That(play.State.Result, Is.EqualTo(GameResult.Won));
                Assert.That(play.State.Phase, Is.EqualTo(BattlePhase.Finished));
                Assert.That(play.Events.OfType<FaceResolved>().Select(f => f.Face), Is.EqualTo(new[] { BattleAttribute.Attack }));
                Assert.That(play.Events.Last(), Is.EqualTo(new BattleEnded(Actor.Player, GameResult.Won)));
            });

            Assert.That(TurnLoop.CanPlay(play.State, InHand(play.State, "thrust")), Is.EqualTo(PlayRefusal.BattleOver));
            Assert.That(() => TurnLoop.EndTurn(play.State, NoRng), Throws.InvalidOperationException);
            Assert.That(() => TurnLoop.BeginPlayerTurn(play.State, NoRng), Throws.InvalidOperationException);
        }

        [Test]
        public void TheBattleEnds_WhenThePlayerReachesZero_AndNoOmenFollows()
        {
            var s = TurnLoop.Start(BattleSetup.Slice(), NoRng).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
            s = s with { Player = s.Player with { Hp = 3, Stamina = 0 } };   // no 構え, so the shove is 8

            var end = TurnLoop.EndTurn(s, NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(end.State.Player.Hp, Is.EqualTo(0));
                Assert.That(end.State.Result, Is.EqualTo(GameResult.Lost));
                Assert.That(end.Events.OfType<OmenSet>(), Is.Empty);
                Assert.That(end.Events.Last(), Is.EqualTo(new BattleEnded(Actor.Enemy, GameResult.Lost)));
            });
        }

        [Test]
        public void MovesOutOfPhase_AreRefused()
        {
            var start = TurnLoop.Start(BattleSetup.Slice(), NoRng).State;
            Assert.That(() => TurnLoop.EndTurn(start, NoRng), Throws.InvalidOperationException);

            var open = TurnLoop.BeginPlayerTurn(start, NoRng).State;
            Assert.That(() => TurnLoop.BeginPlayerTurn(open, NoRng), Throws.InvalidOperationException);
            Assert.That(TurnLoop.CanPlay(start, "thrust-0"), Is.EqualTo(PlayRefusal.NotPlayerTurn));
        }

        [Test]
        public void AnOmenThatCanNoLongerBePaidFor_BecomesARest()
        {
            var s = TurnLoop.Start(BattleSetup.Slice(), NoRng).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
            // Nothing in the slice drains the enemy; this stands in for 崩し (#48).
            s = s with { Enemy = s.Enemy with { Stamina = 0, NextTurnRecoveryBonus = -2 } };

            var end = TurnLoop.EndTurn(s, NoRng);

            Assert.That(end.Events.OfType<Rested>().Single().Declared.ActionId, Is.EqualTo("shove"));
            Assert.That(end.Events.OfType<ActionExecuted>(), Is.Empty);
            Assert.That(end.Events.OfType<DamageDealt>(), Is.Empty);
            Assert.That(end.State.Player.Position, Is.EqualTo(Position.Near), "no shove, no push");
        }

        // ---- Three turns under a fixed seed (#72 Definition of Done) ----

        /// <summary>The policy the pinned battle plays: left to right, every card that can be paid for.</summary>
        private static (BattleState State, List<BattleEvent> Events) PlayGreedyTurn(BattleState state, IRng rng)
        {
            var events = new List<BattleEvent>();
            var begin = TurnLoop.BeginPlayerTurn(state, rng);
            events.AddRange(begin.Events);
            state = begin.State;

            bool played = true;
            while (played && state.Result == GameResult.Ongoing)
            {
                played = false;
                foreach (var card in state.Hand)
                {
                    if (TurnLoop.CanPlay(state, card.InstanceId) != PlayRefusal.None) continue;
                    var step = TurnLoop.PlayCard(state, card.InstanceId, rng);
                    events.AddRange(step.Events);
                    state = step.State;
                    played = true;
                    break;
                }
            }

            if (state.Result == GameResult.Ongoing)
            {
                var end = TurnLoop.EndTurn(state, rng);
                events.AddRange(end.Events);
                state = end.State;
            }
            return (state, events);
        }

        private static string Summary(BattleState s) =>
            $"T{s.Turn} P {s.Player.Hp}/{s.Player.Stamina}/{s.Player.Guard}/{s.Player.Position!.Value.ToLabel()} " +
            $"E {s.Enemy.Hp}/{s.Enemy.Stamina}/{s.Enemy.Guard}/{s.Enemy.Statuses} omen {s.Omen!.Label.ToText()}";

        [Test]
        public void ThreeTurns_UnderAFixedSeed_ArePinned()
        {
            var rng = new SeededRng(Seed);
            var state = TurnLoop.Start(BattleSetup.Slice(), rng).State;

            var summaries = new List<string>();
            var hands = new List<string>();
            var counts = new List<int>();
            for (int turn = 0; turn < 3; turn++)
            {
                var (next, events) = PlayGreedyTurn(state, rng);
                hands.Add(string.Join(",", events.OfType<Drawn>().Select(d => d.Card.Def.Id)));
                summaries.Add(Summary(next));
                counts.Add(events.Count);
                Assert.That(events.Last(), Is.InstanceOf<OmenSet>(), $"turn {turn + 1} closes on the next omen");
                state = next;
            }

            TestContext.Out.WriteLine(string.Join("\n", hands));
            TestContext.Out.WriteLine(string.Join("\n", summaries));
            TestContext.Out.WriteLine(string.Join(",", counts));

            Assert.That(hands, Is.EqualTo(PinnedHands));
            Assert.That(summaries, Is.EqualTo(PinnedSummaries));
            Assert.That(counts, Is.EqualTo(PinnedEventCounts));
        }

        [Test]
        public void TheSameSeed_ReplaysTheSameEventStream()
        {
            List<BattleEvent> Run()
            {
                var rng = new SeededRng(Seed);
                var state = TurnLoop.Start(BattleSetup.Slice(), rng).State;
                var all = new List<BattleEvent>();
                for (int turn = 0; turn < 3; turn++)
                {
                    var (next, events) = PlayGreedyTurn(state, rng);
                    all.AddRange(events);
                    state = next;
                }
                return all;
            }

            Assert.That(Run(), Is.EqualTo(Run()));
        }

        [Test]
        public void ADifferentSeed_DealsADifferentHand()
        {
            var a = TurnLoop.BeginPlayerTurn(TurnLoop.Start(BattleSetup.Slice(), new SeededRng(1)).State, new SeededRng(1));
            var b = TurnLoop.BeginPlayerTurn(TurnLoop.Start(BattleSetup.Slice(), new SeededRng(2)).State, new SeededRng(2));
            Assert.That(a.State.Hand.Select(c => c.InstanceId), Is.Not.EqualTo(b.State.Hand.Select(c => c.InstanceId)));
        }

        // Walked by hand once, so these are rules and not just a recording:
        //  T1 near, 10 stamina. shield_bash 8 (Guard 6 + 3) → body_check 4 + 6, 鈍足 2 → step_out_guard →
        //     step_in_guard: 60 − 18 = 42, Guard 33, 1 stamina left, no 構え. The shove is 5 into the
        //     Guard (28 left) and pushes the player far. The polearm keeps 8 stamina → 構え Guard 3.
        //  T2 far. 重撃 cost one recovery: 1 + 2 = 3. body_check 4 − 3 = 1, kesa_cut 13 (no near bonus):
        //     42 − 14 = 28. The sweep is 8 + 3 into no Guard: 50 − 11 = 39.
        //  T3 far, 0 + 3 stamina. reach_thrust 13 + 5 − 3 = 15: 28 − 15 = 13. The sweep again: 39 − 11 = 28.
        private static readonly string[] PinnedHands =
        {
            "shield_bash,body_check,step_out_guard,step_in_guard,step_out_guard",
            "body_check,kesa_cut,brace,boar_rush,thrust",
            "reach_thrust,feint,feint,kesa_cut,reach_thrust",
        };

        // turn, player hp/stamina/Guard/side, enemy hp/stamina/Guard/statuses, next omen
        private static readonly string[] PinnedSummaries =
        {
            "T1 P 50/1/28/遠 E 42/8/3/鈍足 1 omen 攻撃・遠",
            "T2 P 39/0/0/遠 E 28/8/3/鈍足 2 omen 攻撃・遠",
            "T3 P 28/1/0/遠 E 13/8/3/鈍足 1 omen 攻撃・遠",
        };

        private static readonly int[] PinnedEventCounts = { 55, 37, 29 };
    }
}
