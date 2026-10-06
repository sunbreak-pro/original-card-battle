// #50: the two-step omen of an elite or a boss in the script. Runs under `dotnet test`
// (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class CoreOmenPlanTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static EnemyActionDef Strike(string id) =>
            new EnemyActionDef(id, id, BattleAttribute.Attack, 1, new Face(Power: 1));

        private static EnemyActionDef BackOff(string id, int cells) =>
            new EnemyActionDef(id, id, BattleAttribute.None, 1, new Face(Move: -cells), Targets: TargetKind.Self);

        private static EnemyActionDef Shell(string id) =>
            new EnemyActionDef(id, id, BattleAttribute.Guard, 1, new Face(Guard: 2), Targets: TargetKind.Self);

        /// <summary>An elite: gap 0 reads <paramref name="atZero"/> in order, 1〜2 reads <paramref name="atMid"/>, 3 or more the far one.</summary>
        private static EnemyDef Elite(EnemyActionDef[] atZero, EnemyActionDef[] atMid)
        {
            EnemyActionDef far = BackOff("far", 0);
            var actions = new Dictionary<string, EnemyActionDef>();
            foreach (EnemyActionDef a in atZero.Concat(atMid).Append(far)) actions[a.Id] = a;
            return new EnemyDef(
                "elite", "elite", 60, MaxStamina: 10, Recovery: 2, Size: 1,
                atZero.Select(a => a.Id).ToList(), atMid.Select(a => a.Id).ToList(), new[] { far.Id },
                actions, EnemyRank.Elite, ActionsPerPhase: 2);
        }

        private static List<CardInstance> Deck()
        {
            CardDef filler = new CardDef("filler", "filler", BattleAttribute.Guard, 1, new Face(Guard: 1), Targets: TargetKind.Self);
            var deck = new List<CardInstance>();
            for (int i = 0; i < Constants.DeckMin; i++) deck.Add(new CardInstance("filler-" + i, filler));
            return deck;
        }

        /// <summary>The battle dealt at gap 0, with the writer past the opening, then the player's turn begun and ended.</summary>
        private static (List<DepictionEvent> Turn, List<DepictionEvent> Enemy) Play(EnemyDef enemy)
        {
            var setup = new BattleSetup(enemy, Deck(), 8, StartGap: 0);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(enemy);
            writer.Opening(state);

            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            List<DepictionEvent> turn = writer.Write(begin.Events, begin.State);
            StepResult end = TurnLoop.EndTurn(begin.State, NoShuffle);
            return (turn, writer.Write(end.Events, end.State));
        }

        [Test]
        public void TheSecondStep_ComesUpWithTheOmen_AsAPlanBesideIt()
        {
            EnemyDef enemy = Elite(new[] { Strike("strike"), Shell("shell") }, new[] { Strike("shot") });
            var (turn, _) = Play(enemy);

            OmenFrame omen = turn.Single().After.Omen;

            Assert.Multiple(() =>
            {
                Assert.That(omen.Visible, Is.True);
                Assert.That(omen.PlanVisible, Is.True, "the 予定 is beside the omen at the turn start");
                Assert.That(omen.PlanKindLabel, Is.EqualTo("防御"));
                Assert.That(omen.PlanIcon, Is.EqualTo(OmenIcon.Guard), "#349: the plan wears its kind's icon");
                Assert.That(omen.PlanValueText, Is.EqualTo("2"), "#349: shell's face Guard");
                Assert.That(omen.PlanChanged, Is.False);
                Assert.That(omen.PlanTag, Is.EqualTo("予定"));
            });
        }

        [Test]
        public void APlanThatMoves_WearsTheMoveIcon_AndShowsNoNumber()
        {
            // Gap 0 reads [strike, back_off]: the first omen is strike, the plan is back_off (移動).
            EnemyDef enemy = Elite(new[] { Strike("strike"), BackOff("back_off", 1) }, new[] { Strike("shot") });
            var (turn, _) = Play(enemy);

            OmenFrame omen = turn.Single().After.Omen;

            Assert.Multiple(() =>
            {
                Assert.That(omen.PlanVisible, Is.True);
                Assert.That(omen.PlanIcon, Is.EqualTo(OmenIcon.Move));
                Assert.That(omen.PlanKindLabel, Is.EqualTo("移動"));
                Assert.That(omen.PlanValueText, Is.Empty, "#349: only attack and guard carry a number");
            });
        }

        [Test]
        public void ANormalEnemy_ShowsNoPlan()
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, Deck(), BattleSetup.SliceFieldCells, StartGap: 3);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(setup.Enemy);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);

            Assert.That(writer.Write(begin.Events, begin.State).Single().After.Omen.PlanVisible, Is.False);
        }

        [Test]
        public void APlanThatHolds_StaysUpAfterTheFirstOmenIsSpent_IsSpentBySecondAction_AndNeedsNoChangeBeat()
        {
            EnemyDef enemy = Elite(new[] { Strike("strike"), Shell("shell") }, new[] { Strike("shot") });
            var (_, phase) = Play(enemy);

            List<DepictionEvent> actions = phase.Where(e => e.Kind == DepictionEventKind.EnemyAction).ToList();
            DepictionEvent first = actions.First(e => e.Title == "strike");
            DepictionEvent second = actions.First(e => e.Title == "shell");
            DepictionEvent next = phase.Last();

            Assert.Multiple(() =>
            {
                Assert.That(phase.Any(e => e.Title == "予定変更"), Is.False, "nothing changed, so no beat");
                Assert.That(first.After.Omen.Visible, Is.False, "the first omen is spent by its blow");
                Assert.That(first.After.Omen.PlanVisible, Is.True, "and the 予定 waits for the second action");
                Assert.That(second.After.Omen.PlanVisible, Is.False, "the second action spends it");
                Assert.That(next.Kind, Is.EqualTo(DepictionEventKind.NextOmen));
                Assert.That(next.After.Omen.PlanVisible, Is.True, "the next omen brings a fresh plan");
            });
        }

        [Test]
        public void AChangedPlan_IsABeatOfItsOwn_BeforeTheSecondAction()
        {
            // Gap 0 reads [back_off, strike]: the plan is strike (攻). back_off takes two cells back, the
            // tree is read on the 1〜2 band and answers shell (防), so the plan changes before it acts.
            EnemyDef enemy = Elite(new[] { BackOff("back_off", 2), Strike("strike") }, new[] { Shell("shell") });
            var (turn, phase) = Play(enemy);
            OmenFrame planned = turn.Single().After.Omen;
            Assert.Multiple(() =>
            {
                Assert.That(planned.PlanKindLabel, Is.EqualTo("攻撃"), "planned as an attack");
                Assert.That(planned.PlanIcon, Is.EqualTo(OmenIcon.Attack));
                Assert.That(planned.PlanValueText, Is.EqualTo("1"), "strike's face power");
                Assert.That(planned.PlanTag, Is.EqualTo("予定"));
            });

            int change = phase.FindIndex(e => e.Title == "予定変更");
            int second = phase.FindIndex(e => e.Title == "shell");
            DepictionEvent beat = phase[change];

            Assert.Multiple(() =>
            {
                Assert.That(change, Is.GreaterThan(0));
                Assert.That(change, Is.LessThan(second), "shown before the second action acts");
                Assert.That(beat.Kind, Is.EqualTo(DepictionEventKind.EnemyAction));
                Assert.That(beat.Cues.Select(c => c.Kind), Is.EqualTo(new[] { CueKind.OmenShow }));
                Assert.That(beat.Cues[0].Text, Is.EqualTo("予定変更"));
                Assert.That(beat.After.Omen.PlanVisible, Is.True);
                Assert.That(beat.After.Omen.PlanChanged, Is.True);
                Assert.That(beat.After.Omen.PlanKindLabel, Is.EqualTo("防御"), "the plan now reads what the tree said");
                Assert.That(beat.After.Omen.PlanIcon, Is.EqualTo(OmenIcon.Guard));
                Assert.That(beat.After.Omen.PlanValueText, Is.EqualTo("2"));
                Assert.That(beat.After.Omen.PlanTag, Is.EqualTo("予定変更"));
                Assert.That(phase[second].After.Omen.PlanVisible, Is.False);
            });
        }

        [Test]
        public void AFallenElite_TakesItsPlanWithIt()
        {
            // #349: the badge draws the plan now, so a plan left up would hang over the fallen elite.
            EnemyDef enemy = Elite(new[] { Strike("strike"), Shell("shell") }, new[] { Strike("shot") });
            var setup = new BattleSetup(enemy, Deck(), 8, StartGap: 0);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(enemy);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            Assert.That(writer.Write(begin.Events, begin.State).Single().After.Omen.PlanVisible, Is.True);

            var stream = new List<BattleEvent>
            {
                new TurnStarted(Actor.Player, 2),
                new BattleEnded(Actor.Player, GameResult.Won),
            };
            DepictionEvent fall = writer.Write(stream, begin.State).Last();

            Assert.That(fall.Kind, Is.EqualTo(DepictionEventKind.Defeat));
            Assert.That(fall.After.Omen.PlanVisible, Is.False);
        }

        [Test]
        public void ThePlanBeats_HaveEffectsToPlay_SoTheChangeCostsTimeAndThrowsNothing()
        {
            EnemyDef enemy = Elite(new[] { BackOff("back_off", 2), Strike("strike") }, new[] { Shell("shell") });
            var (_, phase) = Play(enemy);

            DepictionEvent beat = phase.First(e => e.Title == "予定変更");

            Assert.That(EffectPlan.StepsOf(beat).Select(s => s.Id), Is.EqualTo(new[] { EffectId.OmenShow }));
            Assert.That(phase.All(e => EffectPlan.StepsOf(e) != null), Is.True);
        }
    }
}
