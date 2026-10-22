// The app saves a fight the moment the core decides it (Recorder.SettleWhenDecided), not after the screen has played the
// fall and held the result card: a reload in between keeps a death and a won fight (tier1 R1-16 「死亡と生の終了は確定と
// 同時に保存し…」, INHERITANCE.md R-21). Port of test/app/settle.test.mjs through the recorded facades (its storage half
// is SaveTests').
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;
using NUnit.Framework;
using static Trial.Bridge.Tests.LifeTestKit;

namespace Trial.Bridge.Tests
{
    public sealed class SettleTests
    {
        private sealed class NewLifeResult
        {
            public LifeStartData Start;
            public Expedition Raw;
            public RecordedExpedition Ex;
        }

        // js: test/app/settle.test.mjs:18
        private static NewLifeResult NewLife(int seed)
        {
            LifeStart ls = Inheritance.StartLife(Inheritance.NewLineage((ulong)seed));
            LifeStartData start = Life.MakeStart(ls, Plain(ls));
            Expedition raw = Life.LifeSetup(Clone(start)).Expedition;
            return new NewLifeResult { Start = start, Raw = raw, Ex = Recorder.RecordExpedition(raw) };
        }

        private static int Finishes(IEnumerable<LogEntry> log) => log.Count(e => e.Tag == "B" && e.Method == "Finish");

        // js: test/app/settle.test.mjs:39
        [Test]
        public void ALostFight_IsFinishedAndSavedWhenTheCoreDecidesIt_AReloadThenKeepsTheDeath()
        {
            NewLifeResult life = NewLife(11);
            RecordedExpedition ex = life.Ex;
            Assert.That(ex.Session.HasPendingBattle, Is.True, "the entry node is a battle");
            RecordedBattle battle = ex.StartEncounter();
            var saves = new List<LifeLog>();
            SettlingFight fight = Recorder.SettleWhenDecided(battle, battle.StartNext(false), () => saves.Add(Saved(ex.Log)));
            // never play a card: only end turns until the heir falls
            for (int guard = 0; guard < 20000 && !fight.Settled; guard++) Step(fight.Source, false);
            Assert.That(fight.Settled, Is.True, "the fight was decided");
            Assert.That(saves.Count, Is.EqualTo(1), "saved once, at the decision");
            Assert.That(battle.Lost, Is.True, "the encounter holds the lost fight");
            // (the save is written inside the call that decided it: the screen has yet to play that event, the fall and
            // the result card)
            LifeLog atDefeat = saves[0];
            Assert.That(Json.Stringify(atDefeat.Last.ToJson()), Is.EqualTo("[\"B\",\"Finish\",[\"$source\"]]"), "the save ends on the finished fight");
            Assert.That(Recorder.TrimUnfinished(atDefeat).Count, Is.EqualTo(atDefeat.Count), "nothing of the decided fight is dropped on load");

            // "reload while the 負け card is up": the fight does not start over, the encounter comes back lost
            Expedition again = Life.LifeSetup(Clone(life.Start)).Expedition;
            Recorder.Replay(again, atDefeat);
            Assert.That(again.Battle != null && again.Battle.Over && again.Battle.Lost, Is.True, "the encounter is over and lost after the reload");
            BattleReturn back = again.Return();
            Assert.That(back.Died, Is.True);
            Assert.That(again.Session.State.Phase, Is.EqualTo(RunPhase.Fallen), "the heir is dead");

            // the screen plays the rest; the app's finish after onFinished changes nothing
            for (int guard = 0; guard < 20000 && !fight.Source.Finished; guard++) fight.Source.AdvanceAuto();
            Assert.That(fight.Finish(), Is.False, "finish after the decision is a no-op");
            Assert.That(Finishes(ex.Log), Is.EqualTo(1), "Finish is logged once");
            Expedition full = Life.LifeSetup(Clone(life.Start)).Expedition;
            Recorder.Replay(full, Saved(ex.Log));
            Assert.That(full.Battle.Lost, Is.True);
            full.Return();
            Assert.That(full.Session.State.Phase, Is.EqualTo(RunPhase.Fallen), "the whole log replays to the same death");
            Assert.That(full.Session.State.Hp, Is.EqualTo(again.Session.State.Hp));
        }

        // js: test/app/settle.test.mjs:76
        [Test]
        public void AWonFight_IsKeptByAReloadDuringItsResultCard()
        {
            NewLifeResult life = NewLife(5);
            RecordedBattle battle = life.Ex.StartEncounter();
            var saves = new List<LifeLog>();
            SettlingFight fight = Recorder.SettleWhenDecided(battle, battle.StartNext(true), () => saves.Add(Saved(life.Ex.Log)));
            for (int guard = 0; guard < 20000 && !fight.Settled; guard++) Step(fight.Source, true);
            Assert.That(saves.Count, Is.EqualTo(1));
            Expedition again = Life.LifeSetup(Clone(life.Start)).Expedition;
            Recorder.Replay(again, saves[0]);
            Assert.That(again.Battle.Fought.Count, Is.EqualTo(1), "the fight is in the save");
            Assert.That(again.Battle.Fought[0].State.Result, Is.EqualTo(battle.Fought[0].State.Result), "with the same outcome");
            Assert.That(again.Battle.HpAfter, Is.EqualTo(battle.HpAfter), "and the same HP");
        }

        // tier1 R1-16 (2026-10-12, #462): 「戦闘中にゲームを閉じたら、閉じた場面から再開する」, its criterion 「戦闘中にプロセスを
        // 落として再開すると、閉じた場面から続き、続きの結果が閉じる前と同じになる」.
        // js: test/app/settle.test.mjs:105
        [TestCase(11, 7)]
        [TestCase(5, 23)]
        [TestCase(8, 2)]
        public void AFightInProgress_ComesBackOnLoadWhereItStood_AndGoesOnToTheSameEnd(int seed, int cut)
        {
            NewLifeResult life = NewLife(seed);
            RecordedBattle battle = life.Ex.StartEncounter();
            SettlingFight fight = Recorder.SettleWhenDecided(battle, battle.StartNext(false), null);
            for (int i = 0; i < cut && !fight.Settled; i++) PlayerStep(fight.Source);
            Assert.That(fight.Settled, Is.False, $"seed {seed}: still undecided at the cut");
            LifeLog log = Saved(life.Ex.Log);

            Expedition again = Life.LifeSetup(Clone(life.Start)).Expedition;
            ReplayResult res = Recorder.ReplayLife(again, log);
            Assert.That(res.Kept.Count, Is.EqualTo(log.Count), "nothing of the fight is dropped");
            Assert.That(res.Open != null && res.Open.Source != null && ReferenceEquals(res.Open.Battle, again.Battle), Is.True, "the fight in progress comes back");
            Assert.That(BattleSnap(res.Open.Source), Is.EqualTo(BattleSnap(fight.Source)), "hand, piles, HP, stamina, Guard, statuses, omen, turn and phase as they were");

            // go on in both: the reloaded one through a recorder like the app's, the same moves
            RecordedExpedition rec2 = Recorder.RecordExpedition(again, res.Kept);
            SettlingFight back = Recorder.SettleWhenDecided(rec2.Battle, rec2.WrapSource(res.Open.Source), null);
            for (int guard = 0; guard < 20000 && !(fight.Settled && back.Settled); guard++)
            {
                Assert.That(BattleSnap(back.Source), Is.EqualTo(BattleSnap(fight.Source)), "every step lands on the same state (the random state came back too)");
                if (!fight.Settled) PlayerStep(fight.Source);
                if (!back.Settled) PlayerStep(back.Source);
            }
            Assert.That(fight.Settled && back.Settled, Is.True, "both fights were decided");
            Assert.That(again.Battle.Fought[0].State.Result, Is.EqualTo(battle.Fought[0].State.Result), "the same outcome");
            Assert.That(again.Battle.HpAfter, Is.EqualTo(battle.HpAfter), "the same HP");
            // and the reloaded life's log, saved again, replays to the same decided fight
            Expedition third = Life.LifeSetup(Clone(life.Start)).Expedition;
            ReplayResult r3 = Recorder.ReplayLife(third, Saved(rec2.Log));
            Assert.That(r3.Open, Is.Null, "decided: nothing in progress");
            Assert.That(third.Battle.HpAfter, Is.EqualTo(battle.HpAfter));
        }

        // js: test/app/settle.test.mjs:139
        [Test]
        public void AFightBroughtBackAlreadyDecided_IsFinishedAtOnce_TrimUnfinishedStillDropsAFightThatMustStartOver()
        {
            NewLifeResult life = NewLife(11);
            RecordedBattle battle = life.Ex.StartEncounter();
            RecordedSource raw = battle.StartNext(false);
            // the source is driven without settling (as if the Finish never reached the save)
            for (int guard = 0; guard < 20000 && raw.State.Result == GameResult.Ongoing; guard++)
            {
                if (raw.WaitingForPlayer) raw.EndTurn();
                else raw.AdvanceAuto();
            }
            LifeLog log = Saved(life.Ex.Log);
            Expedition again = Life.LifeSetup(Clone(life.Start)).Expedition;
            ReplayResult res = Recorder.ReplayLife(again, log);
            Assert.That(res.Open, Is.Not.Null, "the fight is open in the log");
            int saves = 0;
            SettlingFight fight = Recorder.SettleWhenDecided(again.Battle, res.Open.Source, () => saves++);
            Assert.That(fight.Settled && saves == 1, Is.True, "finished and saved as it is handed back");
            Assert.That(again.Battle.Over && again.Battle.Lost, Is.True);

            // the battle scene failed on a fight: the app drops it and the node waits on it again
            Expedition fresh = Life.LifeSetup(Clone(life.Start)).Expedition;
            LifeLog trimmed = Recorder.TrimUnfinished(log);
            Assert.That(trimmed.All(e => e.Tag != "C"), Is.True, "no call of the dropped fight is kept");
            ReplayResult r = Recorder.ReplayLife(fresh, trimmed);
            Assert.That(r.Open, Is.Null);
            Assert.That(fresh.Battle != null && fresh.Battle.Index == 0, Is.True, "the encounter waits at its first fight");
            // read-only calls and properties pass straight through the settling view
            SettlingFight view = Recorder.SettleWhenDecided(fresh.Battle, fresh.Battle.StartNext(false), null);
            Assert.That(view.Source.Frame, Is.Not.Null);
            Assert.That(view.Source.Finished, Is.False);
            int logged = fresh.Battle.Fought.Count;
            Assert.That(view.Source.Inspect("thrust-1"), Is.EqualTo(PlayVerdict.NotWaiting), "a read goes through");
            view.Source.PreviewFor("thrust-1");
            Assert.That(view.Settled, Is.False, "a new fight is not decided");
            Assert.That(fresh.Battle.Fought.Count, Is.EqualTo(logged), "a read finishes nothing");
        }

        /// <summary>A whole life walked like the app does it: every fight through SettleWhenDecided, then drained to its end (js: settle.test.mjs:170).</summary>
        private static (ExplorationState End, int Chains) WalkSettled(RecordedExpedition ex, List<LifeLog> saves)
        {
            int chains = 0;
            for (int guard = 0; guard < 600; guard++)
            {
                RecordedSession s = ex.Session;
                ExplorationState st = s.State;
                if (s.HasPendingBattle)
                {
                    RecordedBattle battle = ex.Battle ?? ex.StartEncounter();
                    if (battle.Count > 1) chains++;
                    while (!battle.Over)
                    {
                        SettlingFight fight = Recorder.SettleWhenDecided(battle, battle.StartNext(true), () => saves.Add(Saved(ex.Log)));
                        for (int g = 0; g < 20000 && !fight.Source.Finished; g++) Step(fight.Source, true);
                        Assert.That(fight.Settled, Is.True, "every fight is finished at its decision");
                        fight.Finish();
                    }
                    ex.Return();
                    if (ex.Offer.Count > 0) ex.Learn(ex.Offer[0]);
                    continue;
                }
                if (s.HasPendingStep) { s.Act(ScreenAction.ConfirmStep); continue; }
                if (s.HasPendingEvent) { s.Act(ScreenAction.AcceptEvent); continue; }
                if (s.HasPendingFound) { if (!s.Act(ScreenAction.TakeFound, "0")) s.Act(ScreenAction.LeaveFound); continue; }
                if (s.HasPendingSurvey) { s.Act(ScreenAction.ChooseSurvey, s.SurveyChoices[0]); continue; }
                if (s.ConfirmingDescend) { s.Act(ScreenAction.ConfirmDescend); continue; }
                if (st.IsOver) return (st, chains);
                if (st.Phase == RunPhase.Interlude) { s.Act(ScreenAction.Descend); continue; }
                if (ExplorationReducer.CanInterlude(st)) { s.Act(ScreenAction.GoToInterlude); continue; }
                List<MapNode> open = st.Map.Nodes.Where(n => ExplorationReducer.CanStep(st, n.Id)).ToList();
                List<MapNode> fresh = open.Where(n => !st.HasResolved(n.Id)).ToList();
                List<MapNode> pick = fresh.Count > 0 ? fresh : open;
                MapNode node = pick.FirstOrDefault(n => n.Kind == NodeKind.Elite || n.Kind == NodeKind.Boss) ?? pick[0];
                Assert.That(s.Step(node.Id), Is.True, "step refused");
            }
            throw new AssertionException("the life did not end");
        }

        // js: test/app/settle.test.mjs:207
        [Test]
        public void WholeLivesSettledFightByFight_ChainsIncluded_ReplayFromEverySaveToTheSameFights()
        {
            int chains = 0;
            foreach (int seed in new[] { 2, 4, 6 })
            {
                NewLifeResult life = NewLife(seed);
                var saves = new List<LifeLog>();
                (ExplorationState end, int c) = WalkSettled(life.Ex, saves);
                chains += c;
                Assert.That(end.IsOver, Is.True);
                // the last save of the life replays to the same state once its encounter is brought back
                Expedition again = Life.LifeSetup(Clone(life.Start)).Expedition;
                Recorder.Replay(again, Saved(life.Ex.Log));
                Assert.That(Snap(again), Is.EqualTo(Snap(life.Raw)), $"life {seed} replays to the same state");
                // and every save made at a decision keeps every fight decided so far
                foreach (LifeLog log in saves)
                {
                    Expedition e = Life.LifeSetup(Clone(life.Start)).Expedition;
                    Recorder.Replay(e, log);
                    Assert.That(e.Battle != null && e.Battle.Fought.Count >= 1, Is.True, "the decided fight is in the save");
                }
            }
            Assert.That(chains, Is.GreaterThanOrEqualTo(1), "some encounter held several fights");
        }
    }
}
