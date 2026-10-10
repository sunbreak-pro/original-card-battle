using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace BattleCore.Tests
{
    /// <summary>
    /// #51: the six boss-only words (battle_core_v4 §5 ボス専用の状態, enemy_roster_v4 §4.1 / §5.1 /
    /// §6.2), each working by its own effect — not by the common word that stood in for it in the demo
    /// (瘴気纏い / 深み / 枯らし → 疲労, 呪縛 / 鉤爪 → 鈍足, 根張り → 出血) — and 伸びる根's push that
    /// turns into a pull.
    /// </summary>
    public class BossStatusTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private const int Cells = 8;

        private static readonly CardDef Filler = Fixtures.Card("filler", face: new Face(Guard: 1), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        private static List<CardInstance> Deck(params CardDef[] first)
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < first.Length; i++) deck.Add(new CardInstance(first[i].Id + "-" + i, first[i]));
            for (int i = deck.Count; i < Constants.DeckMin; i++) deck.Add(new CardInstance("filler-" + i, Filler));
            return deck;
        }

        /// <summary>An enemy that does nothing but raise 1 Guard: whatever happens to the player is the word's doing.</summary>
        private static readonly EnemyDef Idle = Fixtures.Enemy(
            "idle",
            atZero: Fixtures.EnemyAction("wait", face: new Face(Guard: 1), attributes: BattleAttribute.Guard, targets: TargetKind.Self),
            atOneToTwo: Fixtures.EnemyAction("wait", face: new Face(Guard: 1), attributes: BattleAttribute.Guard, targets: TargetKind.Self),
            atThreePlus: Fixtures.EnemyAction("wait", face: new Face(Guard: 1), attributes: BattleAttribute.Guard, targets: TargetKind.Self));

        private static BattleState Opened(EnemyDef enemy, int gap, params CardDef[] first) =>
            TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(enemy, Deck(first), Cells, StartGap: gap), NoShuffle).State, NoShuffle).State;

        private static BattleState WithPlayer(BattleState s, params (StatusKind Kind, int Stacks)[] words) =>
            s with { Player = s.Player with { Statuses = StatusSet.Of(words) } };

        private static string InHand(BattleState s, string id) => s.Hand.First(c => c.Def.Id == id).InstanceId;

        // ---- The words themselves ----

        [TestCase(StatusKind.MiasmaShroud, StatusDecay.Lasting, "miasma_shroud", "瘴気纏い")]
        [TestCase(StatusKind.Binding, StatusDecay.OnTurn, "binding", "呪縛")]
        [TestCase(StatusKind.Hook, StatusDecay.OnUse, "hook", "鉤爪")]
        [TestCase(StatusKind.Depths, StatusDecay.Lasting, "depths", "深み")]
        [TestCase(StatusKind.Rooting, StatusDecay.Lasting, "rooting", "根張り")]
        [TestCase(StatusKind.Withering, StatusDecay.Lasting, "withering", "枯らし")]
        public void EachBossWord_HasItsDecay_ItsSide_AndItsName(StatusKind kind, StatusDecay decay, string token, string label)
        {
            // roster: 呪縛 is スタック分のターン (ターンで減る型), 鉤爪 使うと減る型, the other four 戦闘終了まで.
            Assert.Multiple(() =>
            {
                Assert.That(Statuses.DecayOf(kind), Is.EqualTo(decay));
                Assert.That(Statuses.IsOwn(kind), Is.False, "相手に");
                Assert.That(Statuses.IsBossOnly(kind), Is.True);
                Assert.That(kind.ToToken(), Is.EqualTo(token));
                Assert.That(kind.ToLabel(), Is.EqualTo(label));
            });
        }

        [Test]
        public void TheLastingWords_SurviveTheTurnStart()
        {
            var set = StatusSet.Of((StatusKind.MiasmaShroud, 1), (StatusKind.Depths, 2), (StatusKind.Rooting, 1), (StatusKind.Withering, 2), (StatusKind.Binding, 2));
            var ticked = set.TickTurnStart();
            Assert.Multiple(() =>
            {
                Assert.That(ticked.Stacks(StatusKind.MiasmaShroud), Is.EqualTo(1));
                Assert.That(ticked.Stacks(StatusKind.Depths), Is.EqualTo(2));
                Assert.That(ticked.Stacks(StatusKind.Rooting), Is.EqualTo(1));
                Assert.That(ticked.Stacks(StatusKind.Withering), Is.EqualTo(2));
                Assert.That(ticked.Stacks(StatusKind.Binding), Is.EqualTo(1), "呪縛 loses a stack like 鈍足");
            });
        }

        // ---- 瘴気纏い: the maximum stamina (battle_core_v4 §6.2) ----

        [Test]
        public void MiasmaShroud_LowersTheMaximumStamina_AndCutsWhatIsHeld_NotTheRecovery()
        {
            // セルク at gap 3 opens with 瘴気の儀: the player's maximum goes 10 → 9 and the 10 held is cut to 9.
            var s = Opened(Enemies.MiasmaPriest, 3);
            Assert.That(s.Omen!.ActionId, Is.EqualTo("miasma_rite"));
            var end = TurnLoop.EndTurn(s, NoShuffle);
            var next = TurnLoop.BeginPlayerTurn(end.State with { Player = end.State.Player with { Stamina = 0 } }, NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(end.State.Player.Statuses.Stacks(StatusKind.MiasmaShroud), Is.EqualTo(1));
                Assert.That(end.State.Player.Statuses.Has(StatusKind.Fatigue), Is.False, "not the 疲労 that stood in for it");
                Assert.That(end.Events.OfType<MaxStaminaChanged>().Single(), Is.EqualTo(new MaxStaminaChanged(Actor.Player, -1, 9, 9)));
                Assert.That(end.State.Player.MaxStamina, Is.EqualTo(9));
                Assert.That(end.State.Player.Stamina, Is.EqualTo(9));
                // The recovery is untouched (3), only the ceiling moved.
                Assert.That(next.Events.OfType<StaminaRecovered>().Single(r => r.Actor == Actor.Player), Is.EqualTo(new StaminaRecovered(Actor.Player, 3, 3, 9)));
            });
        }

        [Test]
        public void MiasmaShroud_StaysAtOneUntilTheSecondStage_ThenReachesTwo()
        {
            // roster §4.1 / §4.4: 「2 スタックまで重なり −2」 and the second stage 「瘴気纏いが 2 スタックまで重なる」.
            var s = Opened(Enemies.MiasmaPriest, 3);
            var first = TurnLoop.EndTurn(s, NoShuffle).State;                           // 瘴気の儀 → 1
            s = TurnLoop.BeginPlayerTurn(first, NoShuffle).State;
            s = s.WithEnemy(s.Enemy with { Hp = 68 });                                   // + 再生 2 at its turn start = 70: the second stage at step 12
            var second = TurnLoop.EndTurn(s, NoShuffle);                                 // 瘴気の儀 again: held at 1
            s = TurnLoop.BeginPlayerTurn(second.State, NoShuffle).State;
            var third = TurnLoop.EndTurn(s, NoShuffle);                                  // the stage's 瘴気の儀 → 2

            var secondRite = second.Events.OfType<StatusApplied>().Single(a => a.Kind == StatusKind.MiasmaShroud);
            Assert.Multiple(() =>
            {
                Assert.That(secondRite.StacksAfter, Is.EqualTo(1));
                Assert.That(secondRite.Landed, Is.EqualTo(0), "the stack past the cap is dropped");
                Assert.That(second.State.Player.MaxStamina, Is.EqualTo(9));
                Assert.That(second.State.Enemies[0].ActiveSwitch, Is.EqualTo("second_stage"));
                Assert.That(third.Events.OfType<ActionExecuted>().First().Action.Id, Is.EqualTo("miasma_rite"));
                Assert.That(third.State.Player.Statuses.Stacks(StatusKind.MiasmaShroud), Is.EqualTo(2));
                Assert.That(third.State.Player.MaxStamina, Is.EqualTo(8), "−2");
            });
        }

        // ---- 呪縛: no own movement ----

        [Test]
        public void Binding_StopsTheHoldersOwnMove_AndKeepsTheMovementCardsOutOfPlay()
        {
            // roster §4.1: 「付いている間、自分の移動の効果が働かない。移動が中心の札は使えない」.
            var s = WithPlayer(Opened(Idle, 3, CardCatalog.Footwork, CardCatalog.StepInGuard), (StatusKind.Binding, 1));
            var step = TurnLoop.PlayCard(s, InHand(s, "step_in_guard"), NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(TurnLoop.CanPlay(s, InHand(s, "footwork")), Is.EqualTo(PlayRefusal.Bound), "足運び moves and does nothing else");
                Assert.That(TurnLoop.CanPlay(s, InHand(s, "step_in_guard")), Is.EqualTo(PlayRefusal.None), "a guard card with a step can be played");
                Assert.That(step.State.Player.Cell, Is.EqualTo(s.Player.Cell), "but the step does not resolve");
                Assert.That(step.Events.OfType<MoveBlocked>().Single(), Is.EqualTo(new MoveBlocked(Actor.Player, StatusKind.Binding)));
                Assert.That(step.Events.OfType<CellsMoved>(), Is.Empty);
                Assert.That(step.State.Player.Guard, Is.EqualTo(12), "the Guard face resolves");
            });
        }

        [Test]
        public void Binding_KeepsOutExactlyTheSixMovementCards_OfBattleCoreSection3()
        {
            // battle_core_v4 §3 「移動が中心の札 6 種」: the catalog's cards that declare nothing and move.
            var movement = CardCatalog.All
                .Where(c => AttributeRule.IsMovementCard(c.Attributes, c.Face))
                .Select(c => c.Id);
            Assert.That(movement, Is.EquivalentTo(new[] { "footwork", "back_leap", "slide_step", "break_off", "leap_back", "dash_in" }));
        }

        [Test]
        public void Binding_FromTheBindingWord_LastsTheNextPlayerTurnOnly()
        {
            // roster §4.1: 縛りの言葉 puts 2 on; 1 is left through the next player turn, none the turn after.
            var s = Opened(Enemies.MiasmaPriest, 2, CardCatalog.Footwork);
            s = s with { Player = s.Player with { Cell = s.Enemy.Cell - 3 } };
            var bound = TurnLoop.EndTurn(s, NoShuffle);
            var next = TurnLoop.BeginPlayerTurn(bound.State, NoShuffle).State;
            var after = TurnLoop.BeginPlayerTurn(TurnLoop.EndTurn(next, NoShuffle).State, NoShuffle).State;

            Assert.Multiple(() =>
            {
                Assert.That(bound.Events.OfType<StatusApplied>().Single(a => a.Kind == StatusKind.Binding).StacksAfter, Is.EqualTo(2));
                Assert.That(bound.State.Player.Statuses.Has(StatusKind.Slow), Is.False, "not the 鈍足 that stood in for it");
                Assert.That(next.Player.Statuses.Stacks(StatusKind.Binding), Is.EqualTo(1), "bound through this turn");
                Assert.That(after.Player.Statuses.Has(StatusKind.Binding), Is.False, "free the turn after");
            });
        }

        [Test]
        public void Binding_IsNotSlow_APushFromTheOtherSideStillMovesTheHolder()
        {
            // 呪縛 stops the holder's own moves only; 払いのけ still throws a bound player two cells back.
            var s = Opened(Enemies.MiasmaPriest, 0);
            Assert.That(s.Omen!.ActionId, Is.EqualTo("push_back"));
            s = WithPlayer(s, (StatusKind.Binding, 2));
            var end = TurnLoop.EndTurn(s, NoShuffle);
            Assert.That(end.Events.OfType<CellsMoved>().Any(m => m.Actor == Actor.Player && m.Pushed), Is.True);
        }

        // ---- 鉤爪: a move back is caught ----

        [Test]
        public void Hook_CatchesAMoveBack_SpendingAStack_AndLetsAMoveForwardThrough()
        {
            // roster §5.1: 「後ろへ動く手を止める（後ろへ n の面が 0 マスになる）。止めるたびに 1 減る。前へ動く手は通る」.
            var s = WithPlayer(Opened(Idle, 3, CardCatalog.BackLeap, CardCatalog.BackLeap, CardCatalog.Footwork), (StatusKind.Hook, 1));
            s = s with { Player = s.Player with { Cell = 3 } };
            var forward = TurnLoop.PlayCard(s, InHand(s, "footwork"), NoShuffle);
            var caught = TurnLoop.PlayCard(forward.State, InHand(forward.State, "back_leap"), NoShuffle);
            var free = TurnLoop.PlayCard(caught.State, InHand(caught.State, "back_leap"), NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(forward.State.Player.Cell, Is.EqualTo(4), "前へ goes through");
                Assert.That(forward.State.Player.Statuses.Stacks(StatusKind.Hook), Is.EqualTo(1), "and spends nothing");

                Assert.That(caught.State.Player.Cell, Is.EqualTo(4), "後ろへ 2 becomes 0 cells");
                Assert.That(caught.Events.OfType<MoveBlocked>().Single(), Is.EqualTo(new MoveBlocked(Actor.Player, StatusKind.Hook)));
                Assert.That(caught.Events.OfType<StatusConsumed>().Single(), Is.EqualTo(new StatusConsumed(Actor.Player, StatusKind.Hook, 0)));
                Assert.That(caught.State.History.HookSnags, Is.EqualTo(new[] { 1 }), "ガルドの snap counts it");
                Assert.That(caught.State.Player.Guard, Is.GreaterThan(0), "the Guard face still resolves");

                Assert.That(free.State.Player.Cell, Is.EqualTo(2), "with no stack left, the next 後ろへ 2 goes");
            });
        }

        [Test]
        public void Hook_IsWhatTheAnglersDrownReads()
        {
            // roster §5.2 引きずり込む: 相手の状態（鉤爪）: 威力 +3 — 鉤爪, not the 鈍足 that stood in for it.
            var drown = Enemies.AbyssAngler.Actions["drown"];
            var hooked = new TraitContext(Gap: 0, OpponentGuard: 0, StaminaAfterUse: 5, StaminaBefore: 7, OpponentStamina: 5,
                OpponentStatuses: StatusSet.Of((StatusKind.Hook, 1)));
            var slowed = hooked with { OpponentStatuses = StatusSet.Of((StatusKind.Slow, 2)) };
            Assert.Multiple(() =>
            {
                Assert.That(Traits.Evaluate(drown.Trait!, hooked).PowerBonus, Is.EqualTo(3));
                Assert.That(Traits.Evaluate(drown.Trait!, slowed).PowerBonus, Is.EqualTo(0));
            });
        }

        [Test]
        public void Hook_DoesNotSpendAStack_OnAMoveBackTheEdgeStopsAnyway()
        {
            // A player on the first cell cannot go back: there is no move for 鉤爪 to catch, so it stays.
            var s = WithPlayer(Opened(Idle, 3, CardCatalog.BackLeap), (StatusKind.Hook, 1));
            s = s with { Player = s.Player with { Cell = 1 } };
            var played = TurnLoop.PlayCard(s, InHand(s, "back_leap"), NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(played.State.Player.Cell, Is.EqualTo(1));
                Assert.That(played.State.Player.Statuses.Stacks(StatusKind.Hook), Is.EqualTo(1), "the stack is kept");
                Assert.That(played.Events.OfType<MoveBlocked>(), Is.Empty);
                Assert.That(played.State.History.HookSnags, Is.Empty, "ガルドの snap does not count it");
            });
        }

        [Test]
        public void Hook_HasNoStackLimit()
        {
            // roster §5.1 / §5.4 give 鉤爪 no 「2 スタックまで」: a 鉤縄 on a player holding 2 leaves 3.
            var s = Opened(Enemies.AbyssAngler, 3);
            Assert.That(s.Omen!.ActionId, Is.EqualTo("hook_cast"));
            s = WithPlayer(s, (StatusKind.Hook, 2));
            var end = TurnLoop.EndTurn(s, NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<StatusApplied>().Single(a => a.Kind == StatusKind.Hook).StacksAfter, Is.EqualTo(3));
                Assert.That(Enemies.AbyssAngler.Switches!.Single(w => w.Id == "second_stage").Override("hook_cast")!.Face.StatusList.Single().Cap,
                    Is.EqualTo(0), "the stage-2 鉤縄 is not capped either");
            });
        }

        // ---- 深み / 枯らし: the recovery ----

        [TestCase(0, 2, 1)]
        [TestCase(1, 2, 3)]
        [TestCase(0, 1, 2)]
        public void Depths_ThinsTheRecovery_OnlyWhenTheTurnStartsAdjacent(int gap, int stacks, int recovered)
        {
            // roster §5.1: 「ガルドとの間合いが 0 でターンを始めると、回復 −1。2 スタックで −2」.
            var s = TurnLoop.Start(new BattleSetup(Idle, Deck(), Cells, StartGap: gap), NoShuffle).State;
            s = s with { Player = s.Player with { Stamina = 0, Statuses = StatusSet.Of((StatusKind.Depths, stacks)) } };
            var begin = TurnLoop.BeginPlayerTurn(s, NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(begin.Events.OfType<StaminaRecovered>().Single().Amount, Is.EqualTo(recovered));
                Assert.That(begin.State.Player.Statuses.Stacks(StatusKind.Depths), Is.EqualTo(stacks), "it lasts the battle");
            });
        }

        [TestCase(1, 0, 2)]
        [TestCase(2, 0, 1)]
        [TestCase(2, 1, 0)]
        public void Withering_ThinsEveryRecovery_DownToZero(int stacks, int fatigue, int recovered)
        {
            // roster §6.2: 「ターン開始の回復 −1。2 スタックで −2（0 が下限）」, at any gap; 疲労 adds its own −1.
            var s = TurnLoop.Start(new BattleSetup(Idle, Deck(), Cells, StartGap: 3), NoShuffle).State;
            var words = fatigue > 0
                ? StatusSet.Of((StatusKind.Withering, stacks), (StatusKind.Fatigue, fatigue))
                : StatusSet.Of((StatusKind.Withering, stacks));
            s = s with { Player = s.Player with { Stamina = 0, Statuses = words } };
            var begin = TurnLoop.BeginPlayerTurn(s, NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(begin.Events.OfType<StaminaRecovered>().Single().Amount, Is.EqualTo(recovered));
                Assert.That(begin.State.Player.Statuses.Stacks(StatusKind.Withering), Is.EqualTo(stacks), "it lasts the battle");
            });
        }

        [Test]
        public void Withering_FromTheBreath_IsCappedAtOne_BeforeTheSecondStage()
        {
            // roster §6.2 / §6.4 stage_2: 「枯らしが 2 スタックまで重なる」 only from the stage on.
            var breath = Enemies.DistortionRoot.Actions["wither_breath"];
            Assert.Multiple(() =>
            {
                Assert.That(breath.Face.StatusList.Single(g => g.Kind == StatusKind.Withering).Cap, Is.EqualTo(1));
                var stage = Enemies.DistortionRoot.Switches!.Single(sw => sw.Id == "stage_2");
                Assert.That(stage.Override("wither_breath")!.Face.StatusList.Single(g => g.Kind == StatusKind.Withering).Cap, Is.EqualTo(2));
                Assert.That(stage.Override("wither_breath")!.Face.ReachOrDefault, Is.EqualTo(new Reach(0, 4)), "「届く間合いが 0〜4 に広がり」");
            });
        }

        // ---- 根張り: staying on one cell ----

        [TestCase(1, false, -4)]
        [TestCase(2, false, -8)]
        [TestCase(1, true, 0)]
        public void Rooting_CostsHp_AfterTwoTurnsEndedOnTheSameCell(int stacks, bool moveInTheSecondTurn, int hpChange)
        {
            // roster §6.2: 「同じマスで 2 ターン続けてターンを終えると、ターン開始に HP −4。2 スタックで −8」.
            // The first hand is six fillers; the second holds the two steps.
            var s = WithPlayer(Opened(Idle, 3, Filler, Filler, Filler, Filler, Filler, Filler, CardCatalog.Footwork, CardCatalog.BreakOff), (StatusKind.Rooting, stacks));
            s = TurnLoop.EndTurn(s, NoShuffle).State;
            s = TurnLoop.BeginPlayerTurn(s, NoShuffle).State;
            if (moveInTheSecondTurn)
            {
                // A step forward and a step back: the same cell again, but it moved (数え直し).
                s = TurnLoop.PlayCard(s, InHand(s, "footwork"), NoShuffle).State;
                s = TurnLoop.PlayCard(s, InHand(s, "break_off"), NoShuffle).State;
            }
            s = TurnLoop.EndTurn(s, NoShuffle).State;
            int hpBefore = s.Player.Hp;
            var third = TurnLoop.BeginPlayerTurn(s, NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(third.State.Player.Hp - hpBefore, Is.EqualTo(hpChange));
                Assert.That(third.Events.OfType<StatusHpChanged>().Any(e => e.Kind == StatusKind.Rooting), Is.EqualTo(hpChange != 0));
                Assert.That(third.State.Player.Statuses.Has(StatusKind.Bleed), Is.False, "not the 出血 that stood in for it");
            });
        }

        [Test]
        public void Rooting_IsWhatTheRootsSweepReads()
        {
            // roster §6.3 根の横払い: 相手の状態（根張り）: 威力 +5.
            var sweep = Enemies.DistortionRoot.Actions["sweep"];
            var rooted = new TraitContext(Gap: 1, OpponentGuard: 0, StaminaAfterUse: 5, StaminaBefore: 7, OpponentStamina: 5,
                OpponentStatuses: StatusSet.Of((StatusKind.Rooting, 1)));
            var bleeding = rooted with { OpponentStatuses = StatusSet.Of((StatusKind.Bleed, 1)) };
            Assert.Multiple(() =>
            {
                Assert.That(Traits.Evaluate(sweep.Trait!, rooted).PowerBonus, Is.EqualTo(5));
                Assert.That(Traits.Evaluate(sweep.Trait!, bleeding).PowerBonus, Is.EqualTo(0));
            });
        }

        // ---- 伸びる根: push at 1 or less, pull at 2 or more ----

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, -1)]
        [TestCase(4, -1)]
        public void TheCreepingRoot_PushesANearPlayer_AndPullsAFarOne(int gap, int push)
        {
            // roster §6.3: 「相手が間合い 1 以下なら 1 マス押し、2 以上なら 1 マス引く」.
            var creeping = Enemies.DistortionRoot.Actions["creeping_root"];
            Assert.That(EnemyAi.FaceAt(creeping, gap).Push, Is.EqualTo(push));
        }

        [TestCase(1, 2)]
        [TestCase(2, 1)]
        [TestCase(3, 2)]
        public void TheCreepingRoot_BringsThePlayerTowardOneToTwo_InPlay(int gap, int gapAfter)
        {
            var creeping = Enemies.DistortionRoot.Actions["creeping_root"];
            var only = new[] { creeping.Id };
            var root = new EnemyDef("creeper", "creeper", 200, 14, 3, 2, only, only, only,
                new Dictionary<string, EnemyActionDef> { { creeping.Id, creeping } }, EnemyRank.Boss, 1);
            var s = Opened(root, gap);
            var end = TurnLoop.EndTurn(s, NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(end.State.Gap, Is.EqualTo(gapAfter));
                Assert.That(end.Events.OfType<CellsMoved>().Single(m => m.Actor == Actor.Player).Pushed, Is.True);
            });
        }
    }
}
