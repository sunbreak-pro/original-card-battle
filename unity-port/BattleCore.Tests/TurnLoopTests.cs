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
    /// under a fixed seed. Re-read on v4.3 (#162): the sides are cells on a line and the numbers
    /// hang on the gap N (§7.2), so a scenario says which gap it starts at.
    /// </summary>
    public class TurnLoopTests
    {
        private const int Seed = 20260921;

        // ---- Helpers ----

        /// <summary>
        /// The gap the hand-built scenarios below were written at (the v4.3 START_GAP until #169
        /// moved the default to 3). They test the loop, not the opening, so they keep it.
        /// </summary>
        private const int ScenarioGap = 2;

        /// <summary>A setup starting at the given gap on the slice's line: the player on cell 2, the polearm 1 + gap further right.</summary>
        private static BattleSetup AtGap(int gap, IReadOnlyList<CardInstance>? deck = null) =>
            new BattleSetup(Enemies.PolearmWarped, deck ?? PrototypeDeck.Build(), BattleSetup.SliceFieldCells, StartGap: gap);

        /// <summary>A battle whose draw order is the deck order: FixedRng(0.999…) makes Fisher-Yates a no-op.</summary>
        private static StepResult StartUnshuffled(params CardDef[] kinds) => StartUnshuffledAt(ScenarioGap, kinds);

        private static StepResult StartUnshuffledAt(int gap, params CardDef[] kinds)
        {
            var deck = Cards.BuildDeck(kinds, copies: 1);
            return TurnLoop.Start(AtGap(gap, deck), new FixedRng(0.9999999));
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
        public void Start_UsesTheCanonValues_AndDecidesTheFirstOmen()
        {
            var start = TurnLoop.Start(BattleSetup.Slice(), new SeededRng(Seed));
            var s = start.State;

            Assert.Multiple(() =>
            {
                Assert.That(s.Turn, Is.EqualTo(0));
                Assert.That(s.Phase, Is.EqualTo(BattlePhase.AwaitingTurnStart));
                Assert.That(s.FieldCells, Is.EqualTo(6));
                Assert.That(s.Player.Hp, Is.EqualTo(50));
                Assert.That(s.Player.Stamina, Is.EqualTo(10));
                Assert.That(s.Player.Guard, Is.EqualTo(0));
                Assert.That(s.Player.Cell, Is.EqualTo(2));
                Assert.That(s.Player.Size, Is.EqualTo(1));
                Assert.That(s.Enemy.Hp, Is.EqualTo(60));
                Assert.That(s.Enemy.Stamina, Is.EqualTo(10));
                Assert.That(s.Enemy.Cell, Is.EqualTo(6));
                Assert.That(s.Enemy.Size, Is.EqualTo(1));
                Assert.That(s.Gap, Is.EqualTo(3));
                Assert.That(s.Hand, Is.Empty);
                Assert.That(s.DrawPile, Has.Count.EqualTo(20));
                // Gap 3 → the first omen is the step forward (§7.3 START_GAP 3, roster branch 3+).
                Assert.That(s.Omen!.ActionId, Is.EqualTo("step_forward"));
                Assert.That(start.Events.Single(), Is.EqualTo(new OmenSet(Actor.Enemy, s.Omen, Decided: true)));
            });
        }

        [Test]
        public void Start_RefusesALineTheSidesDoNotFit()
        {
            var deck = PrototypeDeck.Build();
            Assert.Multiple(() =>
            {
                Assert.That(() => TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 4), NoRng),
                    Throws.InstanceOf<ArgumentOutOfRangeException>(), "5〜8 cells");
                Assert.That(() => TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 9), NoRng),
                    Throws.InstanceOf<ArgumentOutOfRangeException>(), "5〜8 cells");
                Assert.That(() => TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 6, StartGap: -1), NoRng),
                    Throws.InstanceOf<ArgumentOutOfRangeException>(), "a gap is never negative");
                Assert.That(() => TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 6, PlayerStartCell: 6), NoRng),
                    Throws.ArgumentException, "the player stands to the left");
                Assert.That(() => TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, 8, StartGap: 5), NoRng),
                    Throws.Nothing);
            });
        }

        [TestCase(5, 5, 2)] // too short for gap 3: the enemy stands at the right end
        [TestCase(6, 6, 3)]
        [TestCase(7, 6, 3)]
        [TestCase(8, 6, 3)]
        public void Start_PutsTheEnemyStartGapAway_OrAtTheEndOfAShortLine(int fieldCells, int enemyCell, int gap)
        {
            // §7.3 開始のマス (2026-09-23, #169): the width is handed in per battle; the player keeps cell 2.
            var s = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), fieldCells), NoRng).State;
            Assert.That(s.FieldCells, Is.EqualTo(fieldCells));
            Assert.That(s.Player.Cell, Is.EqualTo(Constants.PlayerStartCell));
            Assert.That(s.Enemy.Cell, Is.EqualTo(enemyCell));
            Assert.That(s.Gap, Is.EqualTo(gap));
        }

        [TestCase(6, 1, 6)] // one cell: 6, gap 3
        [TestCase(6, 3, 4)] // a 3-cell enemy on 6 cells: 4〜6, gap 1
        [TestCase(8, 3, 6)] // on 8 cells it keeps the gap: 6〜8
        public void TheStartCell_OfALargeEnemy_FitsItOnTheLine(int fieldCells, int size, int expected)
        {
            Assert.That(Field.EnemyStartCell(fieldCells, Constants.PlayerStartCell, 1, size, Constants.StartGap),
                Is.EqualTo(expected));
        }

        [Test]
        public void TheConstants_AreThe2026_09_23Decisions()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Constants.StartGap, Is.EqualTo(3), "START_GAP");
                Assert.That(Constants.CellCapacity, Is.EqualTo(1), "CELL_CAPACITY");
                Assert.That(Constants.EnemiesMax, Is.EqualTo(3), "ENEMIES_MAX");
                Assert.That(BattleSetup.Slice().FieldCells, Is.EqualTo(6), "the slice's line until #168 hands one in");
            });
        }

        // ---- The seventeen, in order ----

        [Test]
        public void OneTurn_EmitsTheSeventeenInOrder()
        {
            // Deck order: the reach thrust is the first card with a trait that reaches gap 2.
            var rng = NoRng;
            var state = StartUnshuffled(CardCatalog.All.ToArray()).State;

            var begin = TurnLoop.BeginPlayerTurn(state, rng);
            var traitCard = begin.State.Hand.First(c =>
                c.Def.Trait != null && TurnLoop.CanPlay(begin.State, c.InstanceId) == PlayRefusal.None);
            Assert.That(traitCard.Def.Id, Is.EqualTo("reach_thrust"));
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
            // boar_rush is Attack + Move with a trait: trait → attack → move. At gap 2 the trait holds
            // (14 + 6) and the two-cell move then closes to gap 0.
            var state = StartUnshuffled(CardCatalog.BoarRush, CardCatalog.Thrust, CardCatalog.Brace,
                CardCatalog.KesaCut, CardCatalog.Feint).State;
            state = TurnLoop.BeginPlayerTurn(state, NoRng).State;

            var play = TurnLoop.PlayCard(state, InHand(state, "boar_rush"), NoRng);

            Assert.That(TypesOf(play.Events), Is.EqualTo(new[]
            {
                typeof(CardPlayed), typeof(StaminaSpent), typeof(TraitEvaluated),
                typeof(FaceResolved), typeof(DamageDealt),
                typeof(FaceResolved), typeof(CellsMoved),
                typeof(DefeatChecked),
            }));
            var faces = play.Events.OfType<FaceResolved>().Select(f => f.Face).ToList();
            Assert.That(faces, Is.EqualTo(new[] { BattleAttribute.Attack, BattleAttribute.Move }));
            Assert.That(play.Events.OfType<CardPlayed>().Single().GapBefore, Is.EqualTo(2));
            Assert.That(play.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(20), "the gap is read before the move");
            Assert.That(play.Events.OfType<CellsMoved>().Single(), Is.EqualTo(new CellsMoved(Actor.Player, 2, 4, Pushed: false)));
            Assert.That(play.State.Gap, Is.EqualTo(0));
        }

        [Test]
        public void ACardWithoutATrait_EmitsNoTraitEvaluated()
        {
            var state = StartUnshuffledAt(1, CardCatalog.Thrust, CardCatalog.Brace, CardCatalog.KesaCut,
                CardCatalog.Feint, CardCatalog.BoarRush).State;
            state = TurnLoop.BeginPlayerTurn(state, NoRng).State;

            var play = TurnLoop.PlayCard(state, InHand(state, "thrust"), NoRng);
            Assert.That(play.Events.OfType<TraitEvaluated>(), Is.Empty);
        }

        // ---- Reach (§2.4) and the whiff (§6) ----

        [Test]
        public void ACardOutOfReach_IsRefused_AndThePreviewSaysSo()
        {
            // Gap 2: the thrust (0〜1) cannot be released; the reach thrust (1〜2) can; a Guard card reads no reach.
            var s = StartUnshuffled(CardCatalog.All.ToArray()).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;

            Assert.Multiple(() =>
            {
                Assert.That(TurnLoop.CanPlay(s, InHand(s, "thrust")), Is.EqualTo(PlayRefusal.OutOfReach));
                Assert.That(TurnLoop.CanPlay(s, InHand(s, "reach_thrust")), Is.EqualTo(PlayRefusal.None));
                Assert.That(TurnLoop.CanPlay(s, InHand(s, "brace")), Is.EqualTo(PlayRefusal.None));
                Assert.That(TurnLoop.Preview(s, InHand(s, "thrust"))!.InReach, Is.False);
                Assert.That(TurnLoop.Preview(s, InHand(s, "reach_thrust"))!.InReach, Is.True);
                Assert.That(() => TurnLoop.PlayCard(s, InHand(s, "thrust"), NoRng), Throws.InvalidOperationException);
            });

            // Not enough stamina is reported before the reach.
            var broke = s with { Player = s.Player with { Stamina = 1 } };
            Assert.That(TurnLoop.CanPlay(broke, InHand(broke, "thrust")), Is.EqualTo(PlayRefusal.NotEnoughStamina));
        }

        [Test]
        public void AnEnemyBlow_WhiffsWhenThePlayerLeftItsReach_AndTheCostIsStillPaid()
        {
            // The sweep (1〜2) is declared at gap 2; the boar rush closes to gap 0 before it lands.
            var s = StartUnshuffled(CardCatalog.BoarRush, CardCatalog.Thrust, CardCatalog.KesaCut,
                CardCatalog.Brace, CardCatalog.Feint).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
            s = TurnLoop.PlayCard(s, InHand(s, "boar_rush"), NoRng).State;
            Assert.That(s.Gap, Is.EqualTo(0));

            var end = TurnLoop.EndTurn(s, NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<ActionExecuted>().Single().Action.Id, Is.EqualTo("sweep"));
                Assert.That(end.Events.OfType<ActionExecuted>().Single().GapBefore, Is.EqualTo(0));
                Assert.That(end.Events.OfType<StaminaSpent>().Single(e => e.Actor == Actor.Enemy).Amount, Is.EqualTo(2));
                Assert.That(end.Events.OfType<ActionWhiffed>().Single(),
                    Is.EqualTo(new ActionWhiffed(Actor.Enemy, "sweep", 0, new Reach(1, 2))));
                Assert.That(end.Events.OfType<DamageDealt>(), Is.Empty);
                Assert.That(end.Events.OfType<FaceResolved>().Where(f => f.Actor == Actor.Enemy), Is.Empty);
                Assert.That(end.State.Player.Hp, Is.EqualTo(50));
                // Gap 0 now: the next omen comes from the adjacent branch.
                Assert.That(end.State.Omen!.ActionId, Is.EqualTo("shove"));
            });
        }

        [Test]
        public void TheEnemyStepsIn_FromGapThree_AndTheNextOmenIsTheSweep()
        {
            var s = TurnLoop.Start(AtGap(3), NoRng).State;
            Assert.That(s.Omen!.Label.ToText(), Is.EqualTo("動"));
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;

            var end = TurnLoop.EndTurn(s, NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<ActionExecuted>().Single().Action.Id, Is.EqualTo("step_forward"));
                Assert.That(end.Events.OfType<CellsMoved>().Single(), Is.EqualTo(new CellsMoved(Actor.Enemy, 6, 5, Pushed: false)));
                Assert.That(end.Events.OfType<GuardGained>().Single(), Is.EqualTo(new GuardGained(Actor.Enemy, 2, 2)));
                Assert.That(end.Events.OfType<ActionWhiffed>(), Is.Empty, "a self action reads no reach");
                Assert.That(end.State.Gap, Is.EqualTo(2));
                Assert.That(end.State.Omen!.ActionId, Is.EqualTo("sweep"));
            });
        }

        // ---- The eight numbers ----

        [Test]
        public void Number1_Recovery_PlayerThreeCappedAtTen_EnemyTwo()
        {
            var state = StartUnshuffledAt(1, CardCatalog.All.ToArray()).State;

            var begin = TurnLoop.BeginPlayerTurn(state, NoRng);
            var capped = begin.Events.OfType<StaminaRecovered>().Single();
            Assert.That(capped, Is.EqualTo(new StaminaRecovered(Actor.Player, 0, 10, 10)), "already full");

            // Spend 3 + 2, end the turn, and the next turn gives the 3 back.
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

            // The enemy paid 2 for the sweep on turn 1, so its turn-2 recovery is a real +2.
            var end2 = TurnLoop.EndTurn(next.State, NoRng);
            Assert.That(end2.Events.OfType<StaminaRecovered>().Single(),
                Is.EqualTo(new StaminaRecovered(Actor.Enemy, 2, 10, 10)));
        }

        [Test]
        public void Number2_StaminaFalls_ByExactlyTheSumOfTheColumns()
        {
            var state = StartUnshuffledAt(1, CardCatalog.All.ToArray()).State;
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
            var state = StartUnshuffledAt(1, CardCatalog.All.ToArray()).State;
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
            var state = StartUnshuffledAt(1, CardCatalog.All.ToArray()).State;
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
        public void Number4_TheOmenBranch_FollowsTheGapBand()
        {
            Assert.Multiple(() =>
            {
                Assert.That(TurnLoop.Start(AtGap(0), NoRng).State.Omen!.ActionId, Is.EqualTo("shove"));
                Assert.That(TurnLoop.Start(AtGap(1), NoRng).State.Omen!.ActionId, Is.EqualTo("sweep"));
                Assert.That(TurnLoop.Start(AtGap(2), NoRng).State.Omen!.ActionId, Is.EqualTo("sweep"));
                Assert.That(TurnLoop.Start(AtGap(3), NoRng).State.Omen!.ActionId, Is.EqualTo("step_forward"));
            });
        }

        [Test]
        public void Number5_TheSweep_IsElevenAtGapTwo_AndEightAtGapOne()
        {
            // Gap 2 is where the sweep is declared. Staying there eats 11.
            var stay = TurnLoop.BeginPlayerTurn(TurnLoop.Start(AtGap(2), NoRng).State, NoRng).State;
            var stayEnd = TurnLoop.EndTurn(stay, NoRng);
            var eleven = stayEnd.Events.OfType<DamageDealt>().Single();
            // The player kept 10 stamina, so the 構え Guard 3 soaks part of it: 11 − 3 = 8.
            Assert.That(eleven, Is.EqualTo(new DamageDealt(Actor.Enemy, Actor.Player, 11, 3, 8, 0, 42)));

            // Stepping in one with step_in_guard: the committed sweep still comes, still reaches, without its +3.
            var deck = Cards.BuildDeck(new[] { CardCatalog.StepInGuard, CardCatalog.Thrust, CardCatalog.KesaCut,
                CardCatalog.Brace, CardCatalog.Feint }, 1);
            var s = TurnLoop.Start(AtGap(2, deck), NoRng).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
            s = TurnLoop.PlayCard(s, InHand(s, "step_in_guard"), NoRng).State;
            Assert.That(s.Gap, Is.EqualTo(1));
            var end = TurnLoop.EndTurn(s, NoRng);

            Assert.That(end.Events.OfType<ActionExecuted>().Single().Action.Id, Is.EqualTo("sweep"));
            Assert.That(end.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(8));
        }

        [Test]
        public void Number6_TheShove_IsEightAgainstNoGuard_FiveAgainstGuard_AndPushesTwo_IntoTheWall()
        {
            // Adjacent, so the omen is the shove. The player has one cell behind (§7.3): a two-cell
            // push moves one and the other cell is the wall (§7.3 WALL_DAMAGE 3).
            var state = StartUnshuffledAt(0, CardCatalog.All.ToArray()).State;

            // No Guard: 2 stamina and nothing played, so no 構え either.
            var bare = TurnLoop.BeginPlayerTurn(state, NoRng).State;
            bare = bare with { Player = bare.Player with { Stamina = 2 } };
            var bareEnd = TurnLoop.EndTurn(bare, NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(bareEnd.Events.OfType<ActionExecuted>().Single().Action.Id, Is.EqualTo("shove"));
                Assert.That(bareEnd.Events.OfType<DamageDealt>().Single(),
                    Is.EqualTo(new DamageDealt(Actor.Enemy, Actor.Player, 8, 0, 8, 0, 42)));
                Assert.That(bareEnd.Events.OfType<CellsMoved>().Single(),
                    Is.EqualTo(new CellsMoved(Actor.Player, 2, 1, Pushed: true)));
                Assert.That(bareEnd.Events.OfType<WallHit>().Single(),
                    Is.EqualTo(new WallHit(Actor.Player, BlockedCells: 1, Raw: 3, Absorbed: 0, Damage: 3, GuardAfter: 0, HpAfter: 39)));
                Assert.That(bareEnd.State.Gap, Is.EqualTo(1));
                Assert.That(bareEnd.State.Omen!.ActionId, Is.EqualTo("sweep"), "pushed to gap 1, the tree changes branch");
            });

            // With Guard: brace (9) and 構え (3) make 12. The +3 is gone; the push is not; the wall's 3 is soaked too.
            var guarded = TurnLoop.BeginPlayerTurn(state, NoRng).State;
            guarded = TurnLoop.PlayCard(guarded, InHand(guarded, "brace"), NoRng).State;
            var guardedEnd = TurnLoop.EndTurn(guarded, NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(guardedEnd.Events.OfType<DamageDealt>().Single(),
                    Is.EqualTo(new DamageDealt(Actor.Enemy, Actor.Player, 5, 5, 0, 7, 50)));
                Assert.That(guardedEnd.Events.OfType<CellsMoved>().Single(),
                    Is.EqualTo(new CellsMoved(Actor.Player, 2, 1, Pushed: true)));
                Assert.That(guardedEnd.Events.OfType<WallHit>().Single(),
                    Is.EqualTo(new WallHit(Actor.Player, 1, 3, 3, 0, 4, 50)));
            });
        }

        [Test]
        public void Number7_Damage_IsFacePlusTraitMinusGuard_AndNeverBelowZero()
        {
            var state = StartUnshuffledAt(0, CardCatalog.All.ToArray()).State;
            var s = TurnLoop.BeginPlayerTurn(state, NoRng).State;

            // Gap 0: kesa_cut is 13 + 5 against Guard 0.
            var kesa = TurnLoop.PlayCard(s, InHand(s, "kesa_cut"), NoRng);
            Assert.That(kesa.Events.OfType<DamageDealt>().Single(),
                Is.EqualTo(new DamageDealt(Actor.Player, Actor.Enemy, 18, 0, 18, 0, 42)));

            // Gap 0: the thrust gets its plain 23; the reach thrust cannot be released at all.
            var thrust = TurnLoop.PlayCard(s, InHand(s, "thrust"), NoRng);
            Assert.That(thrust.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(23));
            Assert.That(TurnLoop.CanPlay(s, InHand(s, "reach_thrust")), Is.EqualTo(PlayRefusal.OutOfReach));

            // Turn 2: nothing was played, so 構え 3 soaks 3 of the shove's 5 and the wall takes 3 more.
            var end = TurnLoop.EndTurn(s, NoRng);
            var t2 = TurnLoop.BeginPlayerTurn(end.State, NoRng).State;
            Assert.That(t2.Player.Hp, Is.EqualTo(50 - 2 - 3));
            Assert.That(t2.Enemy.Guard, Is.EqualTo(3), "the polearm holds its 構え Guard 3");
            Assert.That(t2.Gap, Is.EqualTo(1), "shoved to cell 1 on turn 1");
            // Turn 2, gap 1: boar_rush is a bare 14 (its bonus needs gap 2), and the Guard 3 soaks 3 of it.
            var intoGuard = TurnLoop.PlayCard(t2, InHand(t2, "boar_rush"), NoRng);
            Assert.That(intoGuard.Events.OfType<DamageDealt>().Single(),
                Is.EqualTo(new DamageDealt(Actor.Player, Actor.Enemy, 14, 3, 11, 0, 49)));
            Assert.That(intoGuard.State.Gap, Is.EqualTo(0), "then it closes in as far as the line allows");
        }

        [Test]
        public void Number7_GuardSoaksFirst_ThenHp()
        {
            var deck = Cards.BuildDeck(new[] { CardCatalog.BodyCheck, CardCatalog.Thrust, CardCatalog.KesaCut,
                CardCatalog.Brace, CardCatalog.Feint, CardCatalog.ReachThrust, CardCatalog.ShieldBash,
                CardCatalog.BoarRush, CardCatalog.StepInGuard, CardCatalog.StepOutGuard }, 1);
            var s = TurnLoop.Start(AtGap(ScenarioGap, deck), NoRng).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
            s = TurnLoop.EndTurn(s, NoRng).State;            // enemy now holds Guard 3; the sweep moved nobody
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;

            // At gap 2, reach_thrust is 13 + 5: Guard 3 soaks 3 and 15 goes through.
            Assert.That(s.Gap, Is.EqualTo(2));
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
        public void EndPoint_ShovedBack_SoTheNextOmenIsTheSweep()
        {
            // The core of the slice: the cell, the push, the tree and the omen in one line.
            var rng = new SeededRng(Seed);
            var s = TurnLoop.Start(AtGap(0), rng).State;
            Assert.That(s.Omen!.Label.ToText(), Is.EqualTo("攻撃・0"));

            s = TurnLoop.BeginPlayerTurn(s, rng).State;
            var end = TurnLoop.EndTurn(s, rng);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<CellsMoved>().Single(), Is.EqualTo(new CellsMoved(Actor.Player, 2, 1, Pushed: true)));
                Assert.That(end.Events.OfType<WallHit>().Single().BlockedCells, Is.EqualTo(1));
                Assert.That(end.State.Player.Cell, Is.EqualTo(1));
                Assert.That(end.State.Gap, Is.EqualTo(1));

                var omens = end.Events.OfType<OmenSet>().ToList();
                Assert.That(omens, Has.Count.EqualTo(1));
                Assert.That(omens[0].Omen.ActionId, Is.EqualTo("sweep"));
                Assert.That(omens[0].Omen.Label.ToText(), Is.EqualTo("攻撃・1〜2"));
                Assert.That(end.State.Omen, Is.EqualTo(omens[0].Omen));

                Assert.That(end.State.Result, Is.EqualTo(GameResult.Ongoing));
                Assert.That(end.State.Player.Hp, Is.GreaterThan(0));
                Assert.That(end.State.Enemy.Hp, Is.GreaterThan(0));
                Assert.That(end.State.Phase, Is.EqualTo(BattlePhase.AwaitingTurnStart));
            });
        }

        // ---- Statuses, moves, reshuffle ----

        [Test]
        public void Slow_LandsOnThePolearm_TicksAtItsTurnStart_AndShortensTheShove()
        {
            var deck = Cards.BuildDeck(new[] { CardCatalog.BodyCheck, CardCatalog.Thrust, CardCatalog.KesaCut,
                CardCatalog.Brace, CardCatalog.Feint }, 1);
            var s = TurnLoop.Start(AtGap(0, deck), NoRng).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;

            var play = TurnLoop.PlayCard(s, InHand(s, "body_check"), NoRng);
            Assert.That(play.Events.OfType<StatusApplied>().Single(),
                Is.EqualTo(new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Slow, 2, 2, false)));
            // Gap 0 → 重撃: 4 + 6, and the next recovery is one short.
            Assert.That(play.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(10));
            Assert.That(play.State.Player.NextTurnRecoveryBonus, Is.EqualTo(-1));

            var end = TurnLoop.EndTurn(play.State, NoRng);
            Assert.That(end.Events.OfType<StatusTicked>().Single(),
                Is.EqualTo(new StatusTicked(Actor.Enemy, StatusKind.Slow, 1)));
            // §5 (v4.3): the slowed polearm's two-cell shove pushes one — to cell 1, and no wall.
            Assert.That(end.Events.OfType<CellsMoved>().Single(), Is.EqualTo(new CellsMoved(Actor.Player, 2, 1, Pushed: true)));
            Assert.That(end.Events.OfType<WallHit>(), Is.Empty);

            var next = TurnLoop.BeginPlayerTurn(end.State, NoRng);
            // 10 − 1 = 9 left; 重撃 makes the recovery 3 − 1 = 2, capped at 10 → +1.
            Assert.That(next.Events.OfType<StaminaRecovered>().Single(),
                Is.EqualTo(new StaminaRecovered(Actor.Player, 1, 10, 10)));
            Assert.That(next.State.Player.NextTurnRecoveryBonus, Is.EqualTo(0));
        }

        [Test]
        public void ASlowedPlayer_PlaysTheMoveCard_ButDoesNotMove()
        {
            var s = StartUnshuffledAt(1, CardCatalog.Feint, CardCatalog.Thrust, CardCatalog.KesaCut,
                CardCatalog.Brace, CardCatalog.BoarRush).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
            s = s with { Player = s.Player with { Statuses = StatusSet.Of((StatusKind.Slow, 1)) } };

            var play = TurnLoop.PlayCard(s, InHand(s, "feint"), NoRng);

            Assert.That(play.Events.OfType<MoveBlocked>().Single(), Is.EqualTo(new MoveBlocked(Actor.Player, StatusKind.Slow)));
            Assert.That(play.Events.OfType<CellsMoved>(), Is.Empty);
            Assert.That(play.State.Player.Cell, Is.EqualTo(2));
            Assert.That(play.Events.OfType<DamageDealt>().Single().Raw, Is.EqualTo(8), "the attack face still lands");
        }

        [Test]
        public void AMoveIntoTheEnemy_StopsShort_AndEmitsNoCellsMoved()
        {
            var s = StartUnshuffledAt(0, CardCatalog.StepInGuard, CardCatalog.Thrust, CardCatalog.KesaCut,
                CardCatalog.Brace, CardCatalog.BoarRush).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;

            var play = TurnLoop.PlayCard(s, InHand(s, "step_in_guard"), NoRng);

            Assert.That(play.Events.OfType<CellsMoved>(), Is.Empty);
            Assert.That(play.Events.OfType<MoveBlocked>(), Is.Empty, "the line stopped it, not 鈍足");
            Assert.That(play.Events.OfType<GuardGained>().Single(), Is.EqualTo(new GuardGained(Actor.Player, 12, 12)));
        }

        [Test]
        public void AGuardTrait_LandsAsGuard_OnACardWithoutAGuardFace()
        {
            // feint is Attack + Move; its 温存 (残 ≥ 4) gives Guard +3 all the same.
            var s = StartUnshuffledAt(1, CardCatalog.Feint, CardCatalog.Thrust, CardCatalog.KesaCut,
                CardCatalog.Brace, CardCatalog.BoarRush).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;

            var play = TurnLoop.PlayCard(s, InHand(s, "feint"), NoRng);

            Assert.That(play.Events.OfType<GuardGained>().Single(), Is.EqualTo(new GuardGained(Actor.Player, 3, 3)));
            Assert.That(play.Events.OfType<FaceResolved>().Select(f => f.Face),
                Is.EqualTo(new[] { BattleAttribute.Attack, BattleAttribute.Move }));
            Assert.That(play.State.Player.Cell, Is.EqualTo(1), "stepped back one");
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

            // boar_rush at gap 2 is 20: the attack face kills, so the move face never runs.
            var play = TurnLoop.PlayCard(s, InHand(s, "boar_rush"), NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(play.State.Enemy.Hp, Is.EqualTo(0));
                Assert.That(play.State.Result, Is.EqualTo(GameResult.Won));
                Assert.That(play.State.Phase, Is.EqualTo(BattlePhase.Finished));
                Assert.That(play.Events.OfType<FaceResolved>().Select(f => f.Face), Is.EqualTo(new[] { BattleAttribute.Attack }));
                Assert.That(play.Events.OfType<CellsMoved>(), Is.Empty);
                Assert.That(play.State.Player.Cell, Is.EqualTo(2));
                Assert.That(play.Events.Last(), Is.EqualTo(new BattleEnded(Actor.Player, GameResult.Won)));
            });

            Assert.That(TurnLoop.CanPlay(play.State, InHand(play.State, "thrust")), Is.EqualTo(PlayRefusal.BattleOver));
            Assert.That(() => TurnLoop.EndTurn(play.State, NoRng), Throws.InvalidOperationException);
            Assert.That(() => TurnLoop.BeginPlayerTurn(play.State, NoRng), Throws.InvalidOperationException);
        }

        [Test]
        public void TheBattleEnds_WhenThePlayerReachesZero_AndNoOmenFollows()
        {
            var s = TurnLoop.Start(AtGap(ScenarioGap), NoRng).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
            s = s with { Player = s.Player with { Hp = 3, Stamina = 0 } };   // no 構え, so the sweep is 11

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
        public void TheWall_CanEndTheBattleToo()
        {
            // Adjacent, HP 4, no Guard: the shove's 8 is fatal on its own here, so give the player
            // Guard 8 to soak it and let the wall's 3 be what ends it.
            var s = TurnLoop.Start(AtGap(0), NoRng).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
            s = s with { Player = s.Player with { Hp = 2, Stamina = 0, Guard = 5 } };

            var end = TurnLoop.EndTurn(s, NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<DamageDealt>().Single(), Is.EqualTo(new DamageDealt(Actor.Enemy, Actor.Player, 5, 5, 0, 0, 2)));
                Assert.That(end.Events.OfType<WallHit>().Single().HpAfter, Is.EqualTo(0));
                Assert.That(end.State.Result, Is.EqualTo(GameResult.Lost));
                Assert.That(end.Events.OfType<OmenSet>(), Is.Empty);
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
            var s = TurnLoop.Start(AtGap(ScenarioGap), NoRng).State;
            s = TurnLoop.BeginPlayerTurn(s, NoRng).State;
            // Nothing in the slice drains the enemy; this stands in for 崩し (#48).
            s = s with { Enemy = s.Enemy with { Stamina = 0, NextTurnRecoveryBonus = -2 } };

            var end = TurnLoop.EndTurn(s, NoRng);

            Assert.That(end.Events.OfType<Rested>().Single().Declared.ActionId, Is.EqualTo("sweep"));
            Assert.That(end.Events.OfType<ActionExecuted>(), Is.Empty);
            Assert.That(end.Events.OfType<DamageDealt>(), Is.Empty);
            Assert.That(end.State.Gap, Is.EqualTo(2), "no action, nobody moved");
        }

        // ---- Three turns under a fixed seed (#72 Definition of Done) ----

        /// <summary>The policy the pinned battle plays: left to right, every card that can be paid for and reaches.</summary>
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
            $"T{s.Turn} P {s.Player.Hp}/{s.Player.Stamina}/{s.Player.Guard}/c{s.Player.Cell} " +
            $"E {s.Enemy.Hp}/{s.Enemy.Stamina}/{s.Enemy.Guard}/c{s.Enemy.Cell}/{s.Enemy.Statuses} gap {s.Gap} omen {s.Omen!.Label.ToText()}";

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

        // Walked by hand once, so these are rules and not just a recording (re-walked at START_GAP 3, #169):
        //  T1 gap 3, 10 stamina. shield_bash and body_check reach 0 only, so: step_out_guard (Guard 12,
        //     cell 1, gap 4) → step_in_guard (24, cell 2) → step_out_guard (36, cell 1), 1 left, no 構え.
        //     The omen from gap 3 is 踏み込み: the polearm steps to cell 5 with Guard 2 and keeps
        //     10 − 1 = 9 → 構え 3, Guard 5. Gap 3 again → 踏み込み again.
        //  T2 gap 3, 1 + 3 stamina. Only brace reaches nobody and is payable: Guard 9, 2 left.
        //     踏み込み: the polearm steps to cell 4 with Guard 2, keeps 9 → 5. Gap 2 → the sweep.
        //  T3 gap 2, 2 + 3 stamina. reach_thrust 13 + 5 − 5 = 13, again 18: 60 − 31 = 29. The sweep
        //     is 8 + 3 into no Guard: 50 − 11 = 39.
        private static readonly string[] PinnedHands =
        {
            "shield_bash,body_check,step_out_guard,step_in_guard,step_out_guard",
            "body_check,kesa_cut,brace,boar_rush,thrust",
            "reach_thrust,feint,feint,kesa_cut,reach_thrust",
        };

        // turn, player hp/stamina/Guard/cell, enemy hp/stamina/Guard/cell/statuses, gap, next omen
        private static readonly string[] PinnedSummaries =
        {
            "T1 P 50/1/36/c1 E 60/9/5/c5/— gap 3 omen 動",
            "T2 P 50/2/9/c1 E 60/9/5/c4/— gap 2 omen 攻撃・1〜2",
            "T3 P 39/1/0/c1 E 29/8/3/c4/— gap 2 omen 攻撃・1〜2",
        };

        private static readonly int[] PinnedEventCounts = { 44, 29, 34 };
    }
}
