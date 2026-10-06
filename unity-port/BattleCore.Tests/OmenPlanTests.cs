using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace BattleCore.Tests
{
    /// <summary>
    /// #50: the two-step omen of an elite or a boss (§9 step 12, §17.6 F11) — the second action is
    /// shown as a 予定, the tree is read again after the first, and a different answer is a 予定変更 —
    /// and the one hook a boss's tree is swapped through (a stage by HP and an adaptation by the
    /// player's history are the same thing). The enemies here are fixtures; the roster's conditions
    /// are #51.
    /// </summary>
    public class OmenPlanTests
    {
        private const int Cells = 8;

        private static readonly IRng NoRng = new FixedRng(0.9999999);

        // ---- Fixtures ----

        private static EnemyActionDef Hit(string id, int power = 1, Reach? reach = null, int column = 1) =>
            Fixtures.EnemyAction(id, column, new Face(Power: power, Reach: reach));

        private static EnemyActionDef Retreat(string id, int cells) =>
            Fixtures.EnemyAction(id, face: new Face(Move: -cells), attributes: BattleAttribute.None, targets: TargetKind.Self);

        /// <summary>
        /// An enemy that acts twice a phase. Gap 0 reads <paramref name="atZero"/> in order, 1〜2 reads
        /// <paramref name="atMid"/>, 3 or more reads <paramref name="atFar"/>.
        /// </summary>
        private static EnemyDef Twice(
            IReadOnlyList<EnemyActionDef> atZero,
            IReadOnlyList<EnemyActionDef> atMid,
            IReadOnlyList<EnemyActionDef> atFar,
            int actionsPerPhase = 2,
            int maxStamina = 10,
            int recovery = 2,
            IReadOnlyList<TreeSwitch>? switches = null,
            int maxHp = 60)
        {
            var actions = new Dictionary<string, EnemyActionDef>();
            foreach (var a in atZero.Concat(atMid).Concat(atFar)) actions[a.Id] = a;
            return new EnemyDef(
                "twice", "twice", maxHp, MaxStamina: maxStamina, Recovery: recovery, Size: 1,
                atZero.Select(a => a.Id).ToList(), atMid.Select(a => a.Id).ToList(), atFar.Select(a => a.Id).ToList(),
                actions, EnemyRank.Elite, actionsPerPhase, switches);
        }

        private static readonly CardDef Filler =
            Fixtures.Card("filler", 1, new Face(Guard: 1), BattleAttribute.Guard, targets: TargetKind.Self);

        private static List<CardInstance> Deck(params CardDef[] cards)
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < cards.Length; i++) deck.Add(new CardInstance(cards[i].Id + "-" + i, cards[i]));
            for (int i = deck.Count; i < Constants.DeckMin; i++) deck.Add(new CardInstance(Filler.Id + "-" + i, Filler));
            return deck;
        }

        private static StepResult Started(EnemyDef enemy, int gap = 0, params CardDef[] cards) =>
            TurnLoop.Start(new BattleSetup(enemy, Deck(cards), Cells, StartGap: gap), NoRng);

        private static BattleState Opened(EnemyDef enemy, int gap = 0, params CardDef[] cards) =>
            TurnLoop.BeginPlayerTurn(Started(enemy, gap, cards).State, NoRng).State;

        private static StepResult End(BattleState s) => TurnLoop.EndTurn(s, NoRng);

        // ---- 予定: the second step of the omen ----

        [Test]
        public void AnEliteShowsItsSecondActionAsAPlan_AtTheBattleStart_AndTheNextTurnRestatesIt()
        {
            // Gap 0 reads [strike, jab]: the omen is strike, the plan is jab (strike is out of the running).
            var enemy = Twice(new[] { Hit("strike"), Hit("jab") }, new[] { Hit("shot", reach: new Reach(1, 2)) }, new[] { Retreat("far", 0) });
            var start = Started(enemy);

            var begin = TurnLoop.BeginPlayerTurn(start.State, NoRng);

            Assert.Multiple(() =>
            {
                Assert.That(start.State.Omen!.ActionId, Is.EqualTo("strike"));
                Assert.That(start.State.Enemies[0].Plan!.ActionId, Is.EqualTo("jab"));
                Assert.That(start.Events.OfType<PlanSet>().Single().Plan.ActionId, Is.EqualTo("jab"));
                Assert.That(start.Events.OfType<PlanSet>().Single().Decided, Is.True);
                Assert.That(begin.Events.OfType<PlanSet>().Single().Decided, Is.False, "step 5 shows the plan standing since step 12");
                Assert.That(begin.Events.OfType<PlanSet>().Single().Plan.ActionId, Is.EqualTo("jab"));
            });
        }

        [Test]
        public void AnEnemyThatActsOnce_HasNoPlan()
        {
            var enemy = Twice(new[] { Hit("strike"), Hit("jab") }, new[] { Hit("shot", reach: new Reach(1, 2)) }, new[] { Retreat("far", 0) }, actionsPerPhase: 1);
            var start = Started(enemy);

            Assert.Multiple(() =>
            {
                Assert.That(start.State.Enemies[0].Plan, Is.Null);
                Assert.That(start.Events.OfType<PlanSet>(), Is.Empty);
                Assert.That(TurnLoop.BeginPlayerTurn(start.State, NoRng).Events.OfType<PlanSet>(), Is.Empty);
            });
        }

        [Test]
        public void ThePlanCountsTheFirstActionsCost_SoASecondItCannotPayForIsPlannedAsARest()
        {
            // The enemy holds at most 3 stamina and both actions cost 3: after the first there is
            // nothing left, so the plan is a rest — and when the phase gets there, nothing changed.
            var enemy = Twice(new[] { Hit("strike", column: 3), Hit("heavy", column: 3) }, new[] { Hit("shot", reach: new Reach(1, 2)) },
                new[] { Retreat("far", 0) }, maxStamina: 3, recovery: 3);
            var start = Started(enemy);

            var end = End(TurnLoop.BeginPlayerTurn(start.State, NoRng).State);

            Assert.Multiple(() =>
            {
                Assert.That(start.State.Omen!.ActionId, Is.EqualTo("strike"));
                Assert.That(start.State.Enemies[0].Plan!.ActionId, Is.EqualTo(EnemyAi.RestActionId));
                Assert.That(end.Events.OfType<ActionExecuted>().Select(e => e.Action.Id), Is.EqualTo(new[] { "strike" }));
                Assert.That(end.Events.OfType<PlanChanged>(), Is.Empty, "a rest was planned and a rest came");
            });
        }

        [Test]
        public void WhenTheFirstActionCannotBePaid_TheEnemyRests_AndThePlanShownIsReplacedByTheRest()
        {
            var enemy = Twice(new[] { Hit("strike", column: 3), Hit("heavy", column: 3) }, new[] { Hit("shot", reach: new Reach(1, 2)) }, new[] { Retreat("far", 0) });
            var s = Started(enemy).State;
            Assert.That(s.Enemies[0].Plan!.ActionId, Is.EqualTo("heavy"));

            // Something drained it between the omen and the phase: 0 + 2 recovery is short of 3.
            s = s.WithEnemy(s.Enemy with { Stamina = 0 });
            var end = End(TurnLoop.BeginPlayerTurn(s, NoRng).State);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<Rested>().Single().Declared.ActionId, Is.EqualTo("strike"));
                var changed = end.Events.OfType<PlanChanged>().Single();
                Assert.That(changed.Was.ActionId, Is.EqualTo("heavy"));
                Assert.That(changed.Now.ActionId, Is.EqualTo(EnemyAi.RestActionId),
                    "the phase ends on the rest, so the plan that was shown is replaced by it");
            });
        }

        [Test]
        public void TheRosterElitesAndBosses_ShowAPlan_AndTheNormalEnemiesDoNot()
        {
            foreach (var enemy in Enemies.All)
            {
                var start = TurnLoop.Start(new BattleSetup(enemy, Deck(), Cells), NoRng).State;
                bool twice = enemy.ActionsPerPhase >= 2;
                Assert.That(start.Enemies[0].Plan != null, Is.EqualTo(twice && start.Omen!.ActionId != EnemyAi.RestActionId), enemy.Id);
            }
            Assert.That(Enemies.All.Count(e => e.ActionsPerPhase >= 2), Is.EqualTo(6), "three elites and three bosses");
        }

        // ---- 予定変更: read the tree again after the first action ----

        [Test]
        public void WhenTheTreeAnswersTheSameAfterTheFirstAction_NothingChanges()
        {
            var enemy = Twice(new[] { Hit("strike"), Hit("jab") }, new[] { Hit("shot", reach: new Reach(1, 2)) }, new[] { Retreat("far", 0) });
            var end = End(Opened(enemy));

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<ActionExecuted>().Select(e => e.Action.Id), Is.EqualTo(new[] { "strike", "jab" }));
                Assert.That(end.Events.OfType<PlanChanged>(), Is.Empty, "the plan held, so no 予定変更");
            });
        }

        [Test]
        public void WhenTheFirstActionMovesTheEnemyOffTheBand_ThePlanChanges_AndTheNewActionIsTheOneTaken()
        {
            // Gap 0 reads [back_off, strike]. The plan is strike. back_off takes two cells back, so the
            // tree is read on the 1〜2 band and answers shot instead: 予定変更 strike → shot.
            var enemy = Twice(
                new[] { Retreat("back_off", 2), Hit("strike") },
                new[] { Hit("shot", reach: new Reach(1, 2)) },
                new[] { Retreat("far", 0) });
            var opened = Opened(enemy);
            Assert.That(opened.Enemies[0].Plan!.ActionId, Is.EqualTo("strike"));

            var end = End(opened);
            var changed = end.Events.OfType<PlanChanged>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(changed.Was.ActionId, Is.EqualTo("strike"));
                Assert.That(changed.Now.ActionId, Is.EqualTo("shot"));
                Assert.That(end.Events.OfType<ActionExecuted>().Select(e => e.Action.Id), Is.EqualTo(new[] { "back_off", "shot" }));

                var kinds = end.Events.Select(e => e.GetType()).ToList();
                int change = kinds.IndexOf(typeof(PlanChanged));
                int second = kinds.LastIndexOf(typeof(ActionExecuted));
                Assert.That(change, Is.LessThan(second), "the change is shown before the second action acts");
                Assert.That(change, Is.GreaterThan(kinds.IndexOf(typeof(ActionExecuted))), "and after the first");
            });
        }

        [Test]
        public void AfterThePhase_TheNextOmenDecidesAFreshPlan()
        {
            var enemy = Twice(new[] { Hit("strike"), Hit("jab") }, new[] { Hit("shot", reach: new Reach(1, 2)) }, new[] { Retreat("far", 0) });
            var end = End(Opened(enemy));

            Assert.Multiple(() =>
            {
                Assert.That(end.State.Enemies[0].Plan!.ActionId, Is.EqualTo("jab"));
                Assert.That(end.Events.OfType<PlanSet>().Last().Decided, Is.True);
                Assert.That(end.Events.OfType<OmenSet>().Last().Decided, Is.True);
            });
        }

        // ---- the hook: one door for an adaptation and a stage ----

        private static readonly EnemyActionDef Base = Hit("base_hit");
        private static readonly EnemyActionDef Rage = Hit("rage_hit", power: 3);
        private static readonly EnemyActionDef Turtle = Fixtures.EnemyAction("shell", face: new Face(Guard: 4), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        private static EnemyDef Boss(params TreeSwitch[] switches) =>
            Twice(new[] { Base, Rage, Turtle }, new[] { Hit("shot", reach: new Reach(1, 2)) }, new[] { Retreat("far", 0) },
                actionsPerPhase: 1, switches: switches);

        [Test]
        public void AStageByHp_SwapsTheTree_ThroughTheHook()
        {
            var stage = new TreeSwitch("stage_2", view => view.Hp * 2 <= view.MaxHp, BranchAtGapZero: new[] { "rage_hit" });
            var enemy = Boss(stage);
            var opened = Opened(enemy);
            Assert.That(opened.Omen!.ActionId, Is.EqualTo("base_hit"), "the base tree while the condition does not hold");

            // HP falls to half: the next omen is read from the stage's branch.
            var hurt = opened.WithEnemy(opened.Enemy with { Hp = 30 });
            var end = End(hurt);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<TreeSwitched>().Single(), Is.EqualTo(new TreeSwitched(Actor.Enemy, null, "stage_2")));
                Assert.That(end.State.Omen!.ActionId, Is.EqualTo("rage_hit"));
                Assert.That(end.State.Enemies[0].ActiveSwitch, Is.EqualTo("stage_2"));
            });
        }

        [Test]
        public void AnAdaptationByTheHistory_SwapsTheTreeThroughTheSameHook()
        {
            // The player's last two cards were both attacks: the enemy turns to its shell.
            var adapt = new TreeSwitch("turtle",
                view => view.Player.Cards.Count >= 2 && view.Player.Cards.Skip(view.Player.Cards.Count - 2).All(c => c == BattleAttribute.Attack),
                BranchAtGapZero: new[] { "shell" });
            var enemy = Boss(adapt);
            var jab = Fixtures.Card("jab", 1, new Face(Power: 1));
            var s = Opened(enemy, 0, jab, Filler, Filler, Filler, Filler, Filler, jab);

            var afterOne = TurnLoop.PlayCard(s, s.Hand.First(c => c.Def.Id == "jab").InstanceId, NoRng).State;
            Assert.That(End(afterOne).State.Omen!.ActionId, Is.EqualTo("base_hit"), "one attack is not yet two in a row");

            var next = TurnLoop.BeginPlayerTurn(End(afterOne).State, NoRng).State;
            var afterTwo = TurnLoop.PlayCard(next, next.Hand.First(c => c.Def.Id == "jab").InstanceId, NoRng).State;
            var end = End(afterTwo);

            Assert.Multiple(() =>
            {
                Assert.That(afterTwo.History.Cards, Is.EqualTo(new[] { BattleAttribute.Attack, BattleAttribute.Attack }));
                Assert.That(end.Events.OfType<TreeSwitched>().Single().To, Is.EqualTo("turtle"));
                Assert.That(end.State.Omen!.ActionId, Is.EqualTo("shell"));
            });
        }

        [Test]
        public void OfTheSwitchesThatHold_TheLastWins_AndABranchItLeavesNullKeepsTheBase()
        {
            var stage2 = new TreeSwitch("stage_2", view => view.Hp * 3 <= view.MaxHp * 2, BranchAtGapZero: new[] { "rage_hit" });
            var stage3 = new TreeSwitch("stage_3", view => view.Hp * 3 <= view.MaxHp, BranchAtGapZero: new[] { "shell" });
            var enemy = Boss(stage2, stage3);
            var opened = Opened(enemy);

            var both = End(opened.WithEnemy(opened.Enemy with { Hp = 15 }));
            var onlyFirst = End(opened.WithEnemy(opened.Enemy with { Hp = 35 }));

            Assert.Multiple(() =>
            {
                Assert.That(both.State.Enemies[0].ActiveSwitch, Is.EqualTo("stage_3"), "both hold: the later stage");
                Assert.That(both.State.Omen!.ActionId, Is.EqualTo("shell"));
                Assert.That(onlyFirst.State.Enemies[0].ActiveSwitch, Is.EqualTo("stage_2"));
                Assert.That(onlyFirst.State.Omen!.ActionId, Is.EqualTo("rage_hit"));
            });

            // The 1〜2 band was not named by either stage, so the base branch still answers there.
            Assert.That(EnemyAi.BranchFor(enemy, 2, stage3), Is.EqualTo(enemy.BranchAtGapOneToTwo));
        }

        [Test]
        public void ASwitchThatStopsHolding_LetsTheBaseTreeBack_AndIsAnnounced()
        {
            var stage = new TreeSwitch("stage_2", view => view.Hp * 2 <= view.MaxHp, BranchAtGapZero: new[] { "rage_hit" });
            var enemy = Boss(stage);
            var opened = Opened(enemy);
            var hurt = End(opened.WithEnemy(opened.Enemy with { Hp = 30 })).State;
            Assert.That(hurt.Enemies[0].ActiveSwitch, Is.EqualTo("stage_2"));

            var healed = hurt.WithEnemy(hurt.Enemy with { Hp = 60 });
            var end = End(TurnLoop.BeginPlayerTurn(healed, NoRng).State);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<TreeSwitched>().Single(), Is.EqualTo(new TreeSwitched(Actor.Enemy, "stage_2", null)));
                Assert.That(end.State.Omen!.ActionId, Is.EqualTo("base_hit"));
            });
        }

        [Test]
        public void AnEnemyWithoutSwitches_NeverAsksTheHook()
        {
            var opened = Opened(Boss());
            var end = End(opened);

            Assert.Multiple(() =>
            {
                Assert.That(end.Events.OfType<TreeSwitched>(), Is.Empty);
                Assert.That(end.State.Enemies[0].ActiveSwitch, Is.Null);
            });
        }

        [Test]
        public void TheHistory_RecordsTheAttributesPlayed_AndEachTurnsEnd()
        {
            var guard = Fixtures.Card("guard4", 1, new Face(Guard: 4), BattleAttribute.Guard, targets: TargetKind.Self);
            var step = Fixtures.Card("step", 1, new Face(Move: 1), BattleAttribute.None, targets: TargetKind.Self);
            var s = Opened(Twice(new[] { Base }, new[] { Base }, new[] { Retreat("far", 0) }, actionsPerPhase: 1), 2, guard, step);

            s = TurnLoop.PlayCard(s, s.Hand.First(c => c.Def.Id == "guard4").InstanceId, NoRng).State;
            s = TurnLoop.PlayCard(s, s.Hand.First(c => c.Def.Id == "step").InstanceId, NoRng).State;
            var end = End(s);

            Assert.Multiple(() =>
            {
                Assert.That(end.State.History.Cards, Is.EqualTo(new[] { BattleAttribute.Guard, BattleAttribute.Skill }), "a card of movement only counts as スキル (§2.1)");
                var turn = end.State.History.TurnEnds.Single();
                Assert.That(turn.Guard, Is.GreaterThanOrEqualTo(4), "the Guard held once 構え was judged");
                Assert.That(turn.Moved, Is.True);
                Assert.That(turn.Gap, Is.EqualTo(1), "N to the nearest enemy at the turn end");
            });
        }
    }
}
