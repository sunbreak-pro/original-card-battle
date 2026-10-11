using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace BattleCore.Tests
{
    /// <summary>
    /// #452 (battle_core_v4 §9.1): the three departure tools besides 間合いの履 that act inside a
    /// battle — 息継ぎの笛 (stamina +3 at the start, past the maximum), 竜鱗の欠片 (Guard 5 before the
    /// first enemy blow or wall that reaches the player) and 竜語の符 (the boss-only words sit 1 stack
    /// lower). Each test fights the same battle with and without the tool.
    /// </summary>
    public class DepartureToolTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private const int Cells = 8;

        private static readonly CardDef Filler = Fixtures.Card("filler", face: new Face(Guard: 1), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        private static List<CardInstance> Deck()
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < Constants.DeckMin; i++) deck.Add(new CardInstance("filler-" + i, Filler));
            return deck;
        }

        private static EnemyDef Always(EnemyActionDef action) => Fixtures.Enemy("always", atZero: action, atOneToTwo: action, atThreePlus: action);

        /// <summary>Strikes 9 at any gap up to 4: the opening gap 3 is inside it.</summary>
        private static readonly EnemyDef Striker = Always(Fixtures.EnemyAction("strike", face: new Face(Power: 9, Reach: new Reach(0, 4))));

        private static BattleSetup Setup(EnemyDef enemy, int gap = 3, int staminaBonus = 0, int firstHitGuard = 0, int ward = 0, int? startStamina = null) =>
            new BattleSetup(enemy, Deck(), Cells, StartGap: gap, PlayerStartStamina: startStamina,
                PlayerStartStaminaBonus: staminaBonus, FirstHitGuard: firstHitGuard, BossWordWard: ward);

        private static BattleState Opened(BattleSetup setup) =>
            TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoShuffle).State, NoShuffle).State;

        /// <summary>The player's turn ended with no stamina left, so 構え (§9 step 7) adds no Guard of its own.</summary>
        private static StepResult EndSpent(BattleState s) =>
            TurnLoop.EndTurn(s with { Player = s.Player with { Stamina = 0 } }, NoShuffle);

        // ---- 息継ぎの笛 ----

        [Test]
        public void Flute_OpensThreePastTheMaximum_AndTurnOneRecoveryNeitherAddsNorCuts()
        {
            var plain = TurnLoop.Start(Setup(Striker), NoShuffle).State;
            var start = TurnLoop.Start(Setup(Striker, staminaBonus: 3), NoShuffle).State;
            var turn1 = TurnLoop.BeginPlayerTurn(start, NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(plain.Player.Stamina, Is.EqualTo(10));
                Assert.That(start.Player.Stamina, Is.EqualTo(13), "10 + 3, past the maximum");
                Assert.That(start.Player.MaxStamina, Is.EqualTo(10), "the maximum does not move");
                Assert.That(turn1.State.Player.Stamina, Is.EqualTo(13));
                Assert.That(turn1.Events.OfType<StaminaRecovered>().Single(r => r.Actor == Actor.Player), Is.EqualTo(new StaminaRecovered(Actor.Player, 0, 13, 10)));
            });
        }

        [Test]
        public void Flute_TheExtraIsSpent_ThenRecoveryStopsAtTheMaximum()
        {
            var s = Opened(Setup(Striker, staminaBonus: 3));
            var spent = s with { Player = s.Player with { Stamina = 11 } };           // 2 spent of 13: still past the maximum
            var next = TurnLoop.BeginPlayerTurn(TurnLoop.EndTurn(spent, NoShuffle).State, NoShuffle).State;
            var low = s with { Player = s.Player with { Stamina = 8 } };
            var after = TurnLoop.BeginPlayerTurn(TurnLoop.EndTurn(low, NoShuffle).State, NoShuffle).State;

            Assert.Multiple(() =>
            {
                Assert.That(next.Player.Stamina, Is.EqualTo(11), "nothing added to stamina above the maximum");
                Assert.That(after.Player.Stamina, Is.EqualTo(10), "8 + 3 stops at the maximum");
                Assert.That(Combat.RecoverStamina(current: 13, max: 10, recovery: 3), Is.EqualTo(13));
            });
        }

        [Test]
        public void Flute_AddsToTheCarriedStamina_AndNeverPassesTheCeiling()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Setup(Striker, staminaBonus: 3, startStamina: 4).StartStamina, Is.EqualTo(7), "§12: carried 4, then +3");
                Assert.That(Setup(Striker, staminaBonus: 3, startStamina: 12).StartStamina, Is.EqualTo(13), "carried is cut to the maximum first");
                Assert.That(Setup(Striker, staminaBonus: 6).StartStamina, Is.EqualTo(Constants.MaxStaminaCeil), "two flutes: 16 stops at 14");
                Assert.That(Setup(Striker).StartStamina, Is.EqualTo(10));
            });
        }

        [Test]
        public void Flute_ReachesTheBattleThroughTheDeparture()
        {
            var departure = new Departure(Deck(), new[] { "ikitsugi_no_fue" }, new string[0], 0, "filler", StartStaminaBonus: 3);
            var setup = departure.SetupFor(Striker, Cells);
            Assert.That(TurnLoop.Start(setup, NoShuffle).State.Player.Stamina, Is.EqualTo(13));
            Assert.That(DeckRules.Validate(departure with { StartStaminaBonus = -1 }, new[] { "filler" }).Errors,
                Has.Some.Contains("never negative"));
        }

        // ---- 竜鱗の欠片 ----

        [Test]
        public void Scale_GuardsTheFirstBlowOnly()
        {
            var plain = EndSpent(Opened(Setup(Striker)));
            var scaled = EndSpent(Opened(Setup(Striker, firstHitGuard: 5)));
            var second = EndSpent(TurnLoop.BeginPlayerTurn(scaled.State, NoShuffle).State);

            Assert.Multiple(() =>
            {
                Assert.That(plain.State.Player.Hp, Is.EqualTo(Constants.PlayerMaxHp - 9));
                Assert.That(scaled.State.Player.Hp, Is.EqualTo(Constants.PlayerMaxHp - 4), "9 meets Guard 5 first");
                Assert.That(scaled.Events.OfType<GuardGained>().Single(g => g.Actor == Actor.Player), Is.EqualTo(new GuardGained(Actor.Player, 5, 5)));
                Assert.That(scaled.State.Tools.ScaleGuard, Is.EqualTo(0), "once a battle");
                Assert.That(second.State.Player.Hp, Is.EqualTo(Constants.PlayerMaxHp - 4 - 9), "the next blow meets no scale");
                Assert.That(second.Events.OfType<GuardGained>().Any(g => g.Actor == Actor.Player), Is.False);
            });
        }

        [Test]
        public void Scale_WaitsThroughTurnsWithoutABlow()
        {
            // An enemy that only steps forward until adjacent: turn 1 and 2 bring no blow, so the scale is still there.
            var walker = Fixtures.Enemy("walker",
                atZero: Fixtures.EnemyAction("bite", face: new Face(Power: 7)),
                atOneToTwo: Fixtures.EnemyAction("step", face: new Face(Move: 1), attributes: BattleAttribute.None, targets: TargetKind.Self),
                atThreePlus: Fixtures.EnemyAction("walk", face: new Face(Move: 1), attributes: BattleAttribute.None, targets: TargetKind.Self));
            var s = Opened(Setup(walker, firstHitGuard: 5));
            for (int turn = 0; turn < 3; turn++)
            {
                s = TurnLoop.BeginPlayerTurn(EndSpent(s).State, NoShuffle).State;
            }
            Assert.That(s.Gap, Is.EqualTo(0));
            Assert.That(s.Player.Hp, Is.EqualTo(Constants.PlayerMaxHp), "no blow yet");
            Assert.That(s.Tools.ScaleGuard, Is.EqualTo(5));

            var bitten = EndSpent(s);
            Assert.That(bitten.State.Player.Hp, Is.EqualTo(Constants.PlayerMaxHp - 2), "7 meets Guard 5");
            Assert.That(bitten.State.Tools.ScaleGuard, Is.EqualTo(0));
        }

        [Test]
        public void Scale_ShowsInTheOmenPreview_UntilItIsUsed()
        {
            var plain = Opened(Setup(Striker));
            var scaled = Opened(Setup(Striker, firstHitGuard: 5));
            var spentPlain = plain with { Player = plain.Player with { Stamina = 0 } };
            var spentScaled = scaled with { Player = scaled.Player with { Stamina = 0 } };

            Assert.That(TurnLoop.PreviewOmen(spentPlain, 0)!.Damage, Is.EqualTo(9));
            Assert.That(TurnLoop.PreviewOmen(spentScaled, 0)!.Damage, Is.EqualTo(4));
        }

        // ---- 竜語の符 ----

        private static BattleState OpenedPriest(int ward) => Opened(Setup(Enemies.MiasmaPriest, ward: ward));

        [Test]
        public void Charm_KeepsTheFirstStackOfMiasmaShroudOff_SoTheMaximumHolds()
        {
            var plain = TurnLoop.EndTurn(OpenedPriest(0), NoShuffle);
            var warded = TurnLoop.EndTurn(OpenedPriest(1), NoShuffle);
            var rite = warded.Events.OfType<StatusApplied>().Single(a => a.Kind == StatusKind.MiasmaShroud);

            Assert.Multiple(() =>
            {
                Assert.That(plain.State.Player.Statuses.Stacks(StatusKind.MiasmaShroud), Is.EqualTo(1));
                Assert.That(plain.State.Player.MaxStamina, Is.EqualTo(9));
                Assert.That(warded.State.Player.Statuses.Stacks(StatusKind.MiasmaShroud), Is.EqualTo(0), "1 stack lower");
                Assert.That(warded.State.Player.MaxStamina, Is.EqualTo(10));
                Assert.That(warded.Events.OfType<MaxStaminaChanged>().Any(), Is.False);
                Assert.That(rite.Warded, Is.EqualTo(1));
                Assert.That(rite.Landed, Is.EqualTo(0));
                Assert.That(rite.Refused, Is.False, "the ward is not the kind limit");
            });
        }

        [Test]
        public void Charm_ALastingWordWithACapOfTwo_SitsOneLowerAtEveryStep()
        {
            // 深み 1 a time, up to 2 (roster §5.1): without the charm 1, 2, 2; with it 0, 1, 1.
            var deep = Always(Fixtures.EnemyAction("deep", face: new Face(Reach: new Reach(0, 4), Statuses: new[] { new StatusGrant(StatusKind.Depths, 1, Cap: 2) }),
                attributes: BattleAttribute.Skill));
            int[] Run(int ward)
            {
                var s = Opened(Setup(deep, ward: ward));
                var held = new List<int>();
                for (int turn = 0; turn < 3; turn++)
                {
                    var end = TurnLoop.EndTurn(s, NoShuffle).State;
                    held.Add(end.Player.Statuses.Stacks(StatusKind.Depths));
                    s = TurnLoop.BeginPlayerTurn(end, NoShuffle).State;
                }
                return held.ToArray();
            }

            Assert.That(Run(0), Is.EqualTo(new[] { 1, 2, 2 }));
            Assert.That(Run(1), Is.EqualTo(new[] { 0, 1, 1 }));
        }

        [Test]
        public void Charm_AWordThatWearsOff_ComesOnOneLowerEachTime()
        {
            // 呪縛 2 (roster §4.1, ターンで減る型): with the charm it lands as 1.
            var binder = Always(Fixtures.EnemyAction("bind", face: new Face(Reach: new Reach(0, 4), Statuses: new[] { new StatusGrant(StatusKind.Binding, 2) }),
                attributes: BattleAttribute.Skill));
            var plain = TurnLoop.EndTurn(Opened(Setup(binder)), NoShuffle).State;
            var warded = TurnLoop.EndTurn(Opened(Setup(binder, ward: 1)), NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(plain.Player.Statuses.Stacks(StatusKind.Binding), Is.EqualTo(2));
                Assert.That(warded.State.Player.Statuses.Stacks(StatusKind.Binding), Is.EqualTo(1));
                Assert.That(warded.Events.OfType<StatusApplied>().Single(a => a.Kind == StatusKind.Binding).Warded, Is.EqualTo(1));
            });
        }

        [Test]
        public void Charm_LeavesTheCommonWordsAlone()
        {
            // 疲労 is one of the ten, not a boss word: the charm does not touch it.
            var tiring = Always(Fixtures.EnemyAction("tire", face: new Face(Reach: new Reach(0, 4), Statuses: new[] { new StatusGrant(StatusKind.Fatigue, 1) }),
                attributes: BattleAttribute.Skill));
            var warded = TurnLoop.EndTurn(Opened(Setup(tiring, ward: 1)), NoShuffle).State;
            Assert.That(warded.Player.Statuses.Stacks(StatusKind.Fatigue), Is.EqualTo(1));
        }
    }
}
