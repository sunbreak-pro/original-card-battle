// The log of a life (src/app/recorder.js, the log half): the run-length rule, the JSON form, trimUnfinished, and the
// replay dispatcher on today's C# cores. The facades that record a live game (and the settle / life tests that drive
// them) are Recorder's; here a small hand recorder logs each call after it returns, as the JS wrap does.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleCore;
using Depiction;
using Depiction.Bridge;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;
using NUnit.Framework;

namespace Trial.Bridge.Tests
{
    public class LifeLogTests
    {
        [SetUp]
        public void ResetShared() => ReplayTable.ResetShared();

        [TearDown]
        public void ResetSharedAfter() => ReplayTable.ResetShared();

        // ---- the entries ----

        // js: app/recorder.js:47
        [Test]
        public void Push_RunsOfCallsWithoutArguments_AreOneEntry()
        {
            var log = new LifeLog();
            log.Push("C", "AdvanceAuto");
            log.Push("C", "AdvanceAuto");
            log.Push("C", "AdvanceAuto");
            log.Push("C", "EndTurn");
            log.Push("C", "AdvanceAuto");
            log.Push("B", "AdvanceAuto");
            log.Push("S", "Step", 3, RestChoice.Rest);
            log.Push("S", "Step", 3, RestChoice.Rest);
            log.Push("S", "Escape");

            Assert.That(log.Select(e => e.ToString()), Is.EqualTo(new[]
            {
                "C.AdvanceAuto ×3", "C.EndTurn", "C.AdvanceAuto", "B.AdvanceAuto", "S.Step", "S.Step", "S.Escape",
            }), "only a call without arguments joins the entry before it, and only the same tag and method");
            Assert.That(log.Calls, Is.EqualTo(9));
            Assert.That(Json.Stringify(log.ToJson()), Is.EqualTo(
                "[[\"C\",\"AdvanceAuto\",[],3],[\"C\",\"EndTurn\",[]],[\"C\",\"AdvanceAuto\",[]],[\"B\",\"AdvanceAuto\",[]]," +
                "[\"S\",\"Step\",[3,\"Rest\"]],[\"S\",\"Step\",[3,\"Rest\"]],[\"S\",\"Escape\",[]]]"),
                "the JS form: enums by name, the count only on a run");
        }

        [Test]
        public void Push_TurnsArgumentsIntoJsonValues()
        {
            var log = new LifeLog();
            var taken = new JsonObject { ["TraceId"] = "t-1", ["Cards"] = new List<object>() };
            log.Push("C", "TryPlay", "thrust-1", DropZone.Receiver);
            log.Push("B", "StartNext", false);
            log.Push("B", "Finish", LifeLog.SourceMarker);
            log.Push("C", "TakeFreeStep", -1);
            log.Push("E", "Inherit", taken);
            log.Push("S", "Act", ScreenAction.ConfirmStep, null);
            Assert.That(Json.Stringify(log.ToJson()), Is.EqualTo(
                "[[\"C\",\"TryPlay\",[\"thrust-1\",\"Receiver\"]],[\"B\",\"StartNext\",[false]],[\"B\",\"Finish\",[\"$source\"]]," +
                "[\"C\",\"TakeFreeStep\",[-1]],[\"E\",\"Inherit\",[{\"TraceId\":\"t-1\",\"Cards\":[]}]],[\"S\",\"Act\",[\"ConfirmStep\",null]]]"));
            Assert.That(log[0].Args[1], Is.EqualTo("Receiver"));
            Assert.That(log[1].Args[0], Is.EqualTo(false));
            Assert.That(log[3].Args[0], Is.EqualTo(-1L));

            var lists = new LifeLog();
            lists.Push("E", "Pick", new List<object> { 1, 2 });
            lists.PushArgs("E", "Pick", new List<object> { 1, 2 });
            lists.PushArgs("C", "Next", new object[0]);
            lists.PushArgs("C", "Next", null);
            Assert.That(Json.Stringify(lists.ToJson()), Is.EqualTo("[[\"E\",\"Pick\",[[1,2]]],[\"E\",\"Pick\",[1,2]],[\"C\",\"Next\",[],2]]"),
                "a single list given to Push is one argument; PushArgs takes the argument list");
        }

        [Test]
        public void TheLog_ReadsBackFromItsJson()
        {
            var log = new LifeLog();
            log.Push("E", "StartEncounter");
            log.Push("B", "StartNext", false);
            log.Push("C", "AdvanceAuto");
            log.Push("C", "AdvanceAuto");
            log.Push("C", "TryPlay", "thrust-1", DropZone.Receiver);
            string text = Json.Stringify(log.ToJson());
            LifeLog back = LifeLog.FromJson(Json.Parse(text));
            Assert.That(Json.Stringify(back.ToJson()), Is.EqualTo(text));
            Assert.That(back[2].Count, Is.EqualTo(2));

            back.Push("C", "TryPlay", "thrust-2", DropZone.Receiver);
            back.Push("C", "AdvanceAuto");
            back.Push("C", "AdvanceAuto");
            Assert.That(back.Last.Count, Is.EqualTo(2), "a log read back goes on the same way");

            Assert.That(LifeLog.FromJson(null).Count, Is.EqualTo(0), "l.log || []");
            Assert.That(LifeLog.FromJson(false).Count, Is.EqualTo(0));
            Assert.That(LifeLog.FromJson(0).Count, Is.EqualTo(0));
            Assert.That(LifeLog.FromJson(Json.Parse("[[\"C\",\"AdvanceAuto\",[],0]]"))[0].Count, Is.EqualTo(1), "(n || 1)");
            Assert.That(LifeLog.FromJson(Json.Parse("[[\"C\",\"AdvanceAuto\",[],null]]"))[0].Count, Is.EqualTo(1), "(n || 1)");
            Assert.Throws<SaveFormatException>(() => LifeLog.FromJson(Json.Parse("[[\"C\",\"AdvanceAuto\",[],-2]]")),
                "a negative count (the JS would skip the call) is refused, not replayed once");
            Assert.Throws<SaveFormatException>(() => LifeLog.FromJson(Json.Parse("[[\"C\",\"AdvanceAuto\",[],2.5]]")));
            Assert.Throws<SaveFormatException>(() => LifeLog.FromJson("x"));
            Assert.Throws<SaveFormatException>(() => LifeLog.FromJson(Json.Parse("{}")));
            Assert.Throws<SaveFormatException>(() => LifeLog.FromJson(Json.Parse("[[\"C\",\"AdvanceAuto\"]]")), "no argument list");
            Assert.Throws<SaveFormatException>(() => LifeLog.FromJson(Json.Parse("[[\"C\",\"\",[]]]")), "no method");
            Assert.Throws<SaveFormatException>(() => LifeLog.FromJson(Json.Parse("[[\"C\",\"AdvanceAuto\",\"[]\"]]")), "arguments that are not a list");
            Assert.Throws<SaveFormatException>(() => LifeLog.FromJson(Json.Parse("[[\"C\",\"AdvanceAuto\",[],\"2\"]]")), "a count that is not a number");
            Assert.Throws<SaveFormatException>(() => LifeLog.FromJson(Json.Parse("[3]")));
        }

        [Test]
        public void CopyAndReplaceAll_LeaveTheOtherLogAlone()
        {
            var log = new LifeLog();
            log.Push("C", "AdvanceAuto");
            LifeLog copy = log.Copy();
            copy.Push("C", "AdvanceAuto");
            Assert.That(log[0].Count, Is.EqualTo(1), "e.slice(): each entry copied");
            Assert.That(copy[0].Count, Is.EqualTo(2));

            log.ReplaceAll(copy);
            Assert.That(log.Count, Is.EqualTo(1));
            Assert.That(log[0].Count, Is.EqualTo(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LogEntry("C", "Next", null, 0));
            Assert.Throws<ArgumentException>(() => new LogEntry("", "Next"));
        }

        // js: app/recorder.js:161
        [Test]
        public void TrimUnfinished_DropsTheFightLeftWithoutAFinish()
        {
            LifeLog Log(params string[] calls)
            {
                var l = new LifeLog();
                foreach (string c in calls)
                {
                    string[] tm = c.Split('.');
                    if (tm[1] == "Finish") l.Push(tm[0], tm[1], LifeLog.SourceMarker);
                    else l.Push(tm[0], tm[1]);
                }
                return l;
            }
            string Names(LifeLog l) => string.Join(" ", l.Select(e => e.ToString()));

            LifeLog unfinished = Log("S.Escape", "E.StartEncounter", "B.StartNext", "C.AdvanceAuto", "C.AdvanceAuto", "C.EndTurn");
            Assert.That(Names(LifeLog.TrimUnfinished(unfinished)), Is.EqualTo("S.Escape E.StartEncounter"));

            LifeLog finished = Log("E.StartEncounter", "B.StartNext", "C.EndTurn", "B.Finish", "C.AdvanceAuto");
            Assert.That(LifeLog.TrimUnfinished(finished).Count, Is.EqualTo(finished.Count), "a decided fight is kept, its last events too");

            LifeLog second = Log("E.StartEncounter", "B.StartNext", "C.EndTurn", "B.Finish", "B.StartNext", "C.AdvanceAuto");
            Assert.That(Names(LifeLog.TrimUnfinished(second)), Is.EqualTo("E.StartEncounter B.StartNext C.EndTurn B.Finish"),
                "the second fight of an encounter starts over, the first stays won");

            LifeLog none = Log("S.Escape", "S.Escape");
            LifeLog same = LifeLog.TrimUnfinished(none);
            Assert.That(Names(same), Is.EqualTo("S.Escape ×2"));
            same.Push("S", "Escape");
            Assert.That(none[0].Count, Is.EqualTo(2), "a log of its own");
        }

        // ---- the method table ----

        [Test]
        public void TheStandardTable_HoldsTheCallsTodaysCoresHave()
        {
            Assert.That(ReplayTable.Standard().Keys, Is.EqualTo(new[]
            {
                "B.Finish", "B.StartNext",
                "C.AdvanceAuto", "C.EndTurn", "C.Next", "C.TakeFreeStep", "C.TryPlay",
                "E.Abandon", "E.Learn", "E.Return", "E.StartEncounter", "E.Train",
                "S.Act", "S.Escape", "S.Jump", "S.OpenTraining", "S.PutOffTraining", "S.Step", "S.Toggle", "S.UseConsumable",
            }));
        }

        // app.md §2.4: every call the shipped game logs. The ones that are not in the table yet are the Expedition's
        // (inheritance and the receipt cards), which another unit adds to the cores.
        [Test]
        public void TheStandardTable_LacksOnlyTheExpeditionsTrialCalls()
        {
            string[] logged =
            {
                "E.StartEncounter", "E.Return", "E.Abandon", "E.Learn", "E.Train", "E.Take", "E.CloseResult", "E.ShowTaken",
                "E.CloseTaken", "E.Inherit", "S.Step", "S.Jump", "S.UseConsumable", "S.Act", "S.OpenTraining",
                "S.PutOffTraining", "S.Escape", "S.Toggle", "B.StartNext", "B.Finish", "C.AdvanceAuto", "C.Next", "C.EndTurn",
                "C.TryPlay", "C.TakeFreeStep",
            };
            ReplayTable t = ReplayTable.Standard();
            Assert.That(logged.Where(k => !t.Has(k.Split('.')[0], k.Split('.')[1])),
                Is.EqualTo(new[] { "E.Take", "E.CloseResult", "E.ShowTaken", "E.CloseTaken", "E.Inherit" }));
        }

        [Test]
        public void ACallTheTableDoesNotKnow_OrWhoseTargetIsNotOpen_StopsTheReplay()
        {
            var ex = new Expedition(new RunStart { Seed = 3 });
            var log = new LifeLog();
            log.Add(new LogEntry("E", "Inherit", new object[] { new JsonObject() }));
            var e1 = Assert.Throws<ReplayException>(() => LifeReplay.ReplayLife(ex, log));
            Assert.That(e1.Message, Is.EqualTo("replay: cannot call E.Inherit"));

            var noBattle = new LifeLog();
            noBattle.Push("B", "StartNext", false);
            var e2 = Assert.Throws<ReplayException>(() => LifeReplay.ReplayLife(new Expedition(new RunStart { Seed = 3 }), noBattle));
            Assert.That(e2.Message, Is.EqualTo("replay: cannot call B.StartNext"));

            var badArg = new LifeLog();
            badArg.Add(new LogEntry("S", "Act", new object[] { "NoSuchAction" }));
            var e3 = Assert.Throws<ReplayException>(() => LifeReplay.ReplayLife(new Expedition(new RunStart { Seed = 3 }), badArg));
            Assert.That(e3.Message, Does.StartWith("replay: S.Act argument 0 is not a ScreenAction name"));

            var threeArgs = new LifeLog();
            threeArgs.Add(new LogEntry("S", "Act", new object[] { "AcceptEvent", "x", 1 }));
            var e4 = Assert.Throws<ReplayException>(() => LifeReplay.ReplayLife(new Expedition(new RunStart { Seed = 3 }), threeArgs));
            Assert.That(e4.Message, Is.EqualTo("replay: cannot call S.Act with 3 arguments"));

            var oddArg = new LifeLog();
            oddArg.Add(new LogEntry("S", "Act", new object[] { "TakeFound", 1.5 }));
            var e5 = Assert.Throws<ReplayException>(() => LifeReplay.ReplayLife(new Expedition(new RunStart { Seed = 3 }), oddArg));
            Assert.That(e5.Message, Is.EqualTo("replay: S.Act argument 1 is not a string or an integer"));

            var notAName = new LifeLog();
            notAName.Add(new LogEntry("S", "Toggle", new object[] { " Journal" }));
            Assert.Throws<ReplayException>(() => LifeReplay.ReplayLife(new Expedition(new RunStart { Seed = 3 }), notAName),
                "an enum is read by its exact name only (JS defineEnum)");

            var core = new LifeLog();
            core.Push("E", "Return");
            Assert.Throws<InvalidOperationException>(() => LifeReplay.ReplayLife(new Expedition(new RunStart { Seed = 3 }), core),
                "a call that throws in the core throws as it is");
        }

        [Test]
        public void OtherUnits_ExtendTheTable_OnTheirOwnOrOnTheSharedOne()
        {
            var log = new LifeLog();
            log.Add(new LogEntry("E", "Inherit", new object[] { new JsonObject { ["TraceId"] = "t-1" } }));
            var seen = new List<string>();
            ReplayTable own = ReplayTable.Standard().Add("E", "Inherit", (c, a) => { seen.Add("own " + ((JsonObject)a[0])["TraceId"]); return null; });
            LifeReplay.ReplayLife(new Expedition(new RunStart { Seed = 3 }), log, own);
            Assert.That(seen, Is.EqualTo(new[] { "own t-1" }));
            Assert.That(ReplayTable.Shared.Has("E", "Inherit"), Is.False, "the shared table is untouched");

            ReplayTable.Shared.Add("E", "Inherit", (c, a) => { seen.Add("shared"); return null; });
            LifeReplay.ReplayLife(new Expedition(new RunStart { Seed = 3 }), log);
            Assert.That(seen.Last(), Is.EqualTo("shared"));
            ReplayTable.ResetShared();
            Assert.That(ReplayTable.Shared.Has("E", "Inherit"), Is.False);

            ReplayTable replaced = ReplayTable.Standard().Add("C", "Next", (c, a) => "mine");
            Assert.That(replaced.TryGet("C", "Next", out ReplayCall next) && (string)next(null, null) == "mine", Is.True, "Add replaces");
            Assert.That(replaced.Remove("C", "Next"), Is.True);
            Assert.That(replaced.Has("C", "Next"), Is.False);
        }

        [Test]
        public void ReplayArgs_ReadWhatTheParserGives()
        {
            var log = new LifeLog();
            log.Add(new LogEntry("S", "Probe", Json.Parse("[3, 2.5, \"x\", true, null, \"Rest\", \"$source\", 12345678901]") as List<object>));
            var read = new List<object>();
            var table = new ReplayTable().Add("S", "Probe", (c, a) =>
            {
                read.Add(a.Int(0));
                read.Add(a.Double(1));
                read.Add(a.String(2));
                read.Add(a.Bool(3));
                read.Add(a.String(4));
                read.Add(a.Enum<RestChoice>(5));
                read.Add(a.Source(6));
                read.Add(a.Long(7));
                read.Add(a.Bool(8, true));
                read.Add(a.IsEnum<RestChoice>(2));
                read.Add(a.Text(0));
                read.Add(a.Text(2));
                read.Add(a.Text(4));
                read.Add(a.Text(7));
                Assert.Throws<ReplayException>(() => a.Int(1));
                Assert.Throws<ReplayException>(() => a.Int(9));
                Assert.Throws<ReplayException>(() => a.Source(2));
                Assert.Throws<ReplayException>(() => a.Text(1), "2.5 is no slot");
                Assert.Throws<ReplayException>(() => a.Text(3));
                Assert.Throws<ReplayException>(() => a.Enum<RestChoice>(0), "a number is not an enum name");
                return null;
            });
            LifeReplay.ReplayLife(new Expedition(new RunStart { Seed = 3 }), log, table);
            Assert.That(read, Is.EqualTo(new object[] { 3, 2.5, "x", true, null, RestChoice.Rest, null, 12345678901L, true, false, "3", "x", null, "12345678901" }));

            var names = new LifeLog();
            names.Add(new LogEntry("S", "Probe", Json.Parse("[\"Rest\", \"rest\", \" Rest\", \"Rest, Train\", \"1\", \"Train\"]") as List<object>));
            var exact = new List<bool>();
            var nameTable = new ReplayTable().Add("S", "Probe", (c, a) =>
            {
                for (int i = 0; i < a.Count; i++) exact.Add(a.IsEnum<RestChoice>(i));
                return null;
            });
            LifeReplay.ReplayLife(new Expedition(new RunStart { Seed = 3 }), names, nameTable);
            Assert.That(exact, Is.EqualTo(new[] { true, false, false, false, false, true }), "only a member's exact name");
        }

        // The calls the trial added to today's cores, dispatched to the real methods (the exploration's trial calls on a
        // session without the trial answer false and change nothing; a free step that cannot be taken throws in the core).
        [Test]
        public void TheTrialsCalls_AreDispatchedToTheCores()
        {
            var ex = new Expedition(new RunStart { Seed = 3 });
            var log = LifeLog.FromJson(Json.Parse(
                "[[\"S\",\"OpenTraining\",[]],[\"S\",\"PutOffTraining\",[]],[\"S\",\"Jump\",[1,null]]," +
                "[\"S\",\"Act\",[\"TakeFound\",0]],[\"S\",\"Act\",[\"ChooseSurvey\",\"polearm_warped\"]],[\"S\",\"Act\",[\"Cancel\"]]]"));
            Assert.DoesNotThrow(() => LifeReplay.ReplayLife(ex, log));

            var freeStep = LifeLog.FromJson(Json.Parse("[[\"E\",\"StartEncounter\",[]],[\"B\",\"StartNext\",[false]],[\"C\",\"TakeFreeStep\",[1]]]"));
            var e = Assert.Throws<InvalidOperationException>(() => LifeReplay.ReplayLife(new Expedition(new RunStart { Seed = 11 }), freeStep));
            Assert.That(e.Message, Is.EqualTo("The free step cannot be taken right now."), "the core's own refusal: the call reached it");
        }

        // player.js:1677 logs TryPlay(card, zone) or TryPlay(card, zone, target); a null target is TargetFor's (JS ??).
        [Test]
        public void TryPlay_WithItsTarget_ReplaysToTheSameFight()
        {
            var first = new HandRecorder(new Expedition(new RunStart { Seed = 11 }));
            EncounterBattle battle = first.StartEncounter();
            CoreBattleSource src = first.StartNext(battle, false);
            int played = 0;
            for (int guard = 0; guard < 400 && !Decided(src) && played < 6; guard++)
            {
                if (!src.WaitingForPlayer) { first.AdvanceAuto(src); continue; }
                CardFace card = src.Frame.Hand.FirstOrDefault(f => src.Inspect(f.Id) == PlayVerdict.Accepted);
                if (card == null) { first.EndTurn(src); continue; }
                int target = src.TargetFor(card.Id);
                DropZone zone = DepictionText.RequiredZone(card.Aim);
                if (played % 2 == 0)
                {
                    src.TryPlay(card.Id, zone, target, out DepictionEvent _);
                    first.Log.Push("C", "TryPlay", card.Id, zone, target);
                }
                else
                {
                    src.TryPlay(card.Id, zone, out DepictionEvent _);
                    first.Log.Push("C", "TryPlay", card.Id, zone, null);   // the browser's TryPlay(id, zone, undefined)
                }
                played++;
            }
            Assert.That(played, Is.GreaterThanOrEqualTo(2), "cards were played both ways");
            Assert.That(first.Log.Any(x => x.Method == "TryPlay" && x.Args.Count == 3 && x.Args[2] is long), Is.True);

            var again = new Expedition(new RunStart { Seed = 11 });
            ReplayResult r = LifeReplay.ReplayLife(again, LifeLog.FromJson(Json.Parse(Json.Stringify(first.Log.ToJson()))));
            Assert.That(r.Open, Is.Not.Null);
            Assert.That(Snapshot(r.Open.Source), Is.EqualTo(Snapshot(src)));
        }

        // ---- replay on the cores ----

        // The JS settle test's story on the dispatcher (test/app/settle.test.mjs:105): a fight cut at a move comes back
        // on load where it stood, and goes on to the same end.
        [TestCase(11UL, 7)]
        [TestCase(5UL, 23)]
        [TestCase(8UL, 2)]
        public void AFightInProgress_ComesBackWhereItStood_AndGoesOnToTheSameEnd(ulong seed, int cut)
        {
            var first = new HandRecorder(new Expedition(new RunStart { Seed = seed }));
            EncounterBattle battle = first.StartEncounter();
            CoreBattleSource src = first.StartNext(battle, false);
            for (int i = 0; i < cut && !Decided(src); i++) first.PlayerStep(src);
            Assert.That(Decided(src), Is.False, "still undecided at the cut");

            LifeLog saved = LifeLog.FromJson(Json.Parse(Json.Stringify(first.Log.ToJson())));   // through the save's text
            var again = new Expedition(new RunStart { Seed = seed });
            ReplayResult res = LifeReplay.ReplayLife(again, saved);
            Assert.That(res.Kept.Count, Is.EqualTo(saved.Count), "nothing of the fight is dropped");
            Assert.That(res.Kept, Is.Not.SameAs(saved));
            Assert.That(res.Open, Is.Not.Null, "the fight in progress comes back");
            Assert.That(res.Open.Battle, Is.SameAs(again.Battle));
            Assert.That(Snapshot(res.Open.Source), Is.EqualTo(Snapshot(src)), "hand, piles, HP, stamina, Guard, turn and phase as they were");

            // go on in both, the reloaded one recorded into the log it came back with
            var second = new HandRecorder(again, res.Kept);
            CoreBattleSource back = res.Open.Source;
            for (int guard = 0; guard < 20000 && !(src.Finished && back.Finished); guard++)
            {
                Assert.That(Snapshot(back), Is.EqualTo(Snapshot(src)), "every step lands on the same state (the random state came back too)");
                if (!src.Finished) first.PlayerStep(src);
                if (!back.Finished) second.PlayerStep(back);
            }
            first.Finish(battle, src);
            second.Finish(again.Battle, back);
            Assert.That(again.Battle.Fought[0].State.Result, Is.EqualTo(battle.Fought[0].State.Result), "the same outcome");
            Assert.That(again.Battle.HpAfter, Is.EqualTo(battle.HpAfter), "the same HP");

            // and the reloaded life's log, saved again, replays to the same decided fight
            var third = new Expedition(new RunStart { Seed = seed });
            ReplayResult r3 = LifeReplay.ReplayLife(third, second.Log);
            Assert.That(r3.Open, Is.Null, "decided: nothing in progress");
            Assert.That(third.Battle.HpAfter, Is.EqualTo(battle.HpAfter));
        }

        // test/app/settle.test.mjs:39 on the dispatcher: a lost fight, finished at the decision, comes back lost; the
        // rest of its script after the Finish replays too.
        [Test]
        public void ALostFight_FinishedAtItsDecision_ReplaysToTheSameDeath()
        {
            var rec = new HandRecorder(new Expedition(new RunStart { Seed = 11 }));
            Assert.That(rec.Expedition.Session.HasPendingBattle, Is.True, "the entry node is a battle");
            EncounterBattle battle = rec.StartEncounter();
            CoreBattleSource src = rec.StartNext(battle, false);
            for (int guard = 0; guard < 20000 && !Decided(src); guard++)
            {
                if (src.WaitingForPlayer) rec.EndTurn(src); else rec.AdvanceAuto(src);
            }
            Assert.That(src.State.Result, Is.EqualTo(GameResult.Lost));
            rec.Finish(battle, src);
            LifeLog atDefeat = rec.Log.Copy();
            Assert.That(atDefeat.Last.ToString(), Is.EqualTo("B.Finish"));
            Assert.That(LifeLog.TrimUnfinished(atDefeat).Count, Is.EqualTo(atDefeat.Count), "nothing of the decided fight is dropped on load");

            var again = new Expedition(new RunStart { Seed = 11 });
            ReplayResult r = LifeReplay.ReplayLife(again, atDefeat);
            Assert.That(r.Open, Is.Null);
            Assert.That(again.Battle.Over && again.Battle.Lost, Is.True, "the encounter is over and lost after the reload");
            Assert.That(again.Return().Died, Is.True);
            Assert.That(again.Session.State.Phase, Is.EqualTo(RunPhase.Fallen), "the heir is dead");

            // the screen plays the rest of the script, then the explore scene brings the result back
            for (int guard = 0; guard < 20000 && !src.Finished; guard++) rec.AdvanceAuto(src);
            rec.Return();
            var full = new Expedition(new RunStart { Seed = 11 });
            LifeReplay.Replay(full, rec.Log);
            Assert.That(full.Session.State.Phase, Is.EqualTo(RunPhase.Fallen), "the whole log replays to the same death");
            Assert.That(full.Session.State.Hp, Is.EqualTo(again.Session.State.Hp));
            Assert.That(full.Battle, Is.Null);
        }

        // test/app/settle.test.mjs walkSettled with today's actions: a whole life walked the way the app does it (every
        // fight finished at its decision, the explore calls on the session), replayed from its log to the same state.
        [TestCase(2UL)]
        [TestCase(4UL)]
        [TestCase(6UL)]
        public void AWholeLife_ReplaysFromItsLog_ToTheSameState(ulong seed)
        {
            var ex = new Expedition(new RunStart { Seed = seed });
            var rec = new HandRecorder(ex);
            int fights = 0;
            for (int guard = 0; guard < 600; guard++)
            {
                ExplorationSession s = ex.Session;
                ExplorationState st = s.State;
                if (s.HasPendingBattle)
                {
                    EncounterBattle battle = ex.Battle ?? rec.StartEncounter();
                    while (!battle.Over)
                    {
                        CoreBattleSource src = rec.StartNext(battle, true);
                        for (int g = 0; g < 20000 && !Decided(src); g++) rec.SuggestedStep(src);
                        rec.Finish(battle, src);
                        for (int g = 0; g < 20000 && !src.Finished; g++) rec.AdvanceAuto(src);
                        fights++;
                    }
                    rec.Return();
                    if (ex.Offer.Count > 0) rec.Learn(ex.Offer[0]);
                    continue;
                }
                if (s.HasPendingStep) { rec.Act(ScreenAction.ConfirmStep); continue; }
                if (s.ConfirmingDescend) { rec.Act(ScreenAction.ConfirmDescend); continue; }
                if (st.IsOver) break;
                if (st.Phase == RunPhase.Interlude) { rec.Act(ScreenAction.Descend); continue; }
                if (ExplorationReducer.CanInterlude(st)) { rec.Act(ScreenAction.GoToInterlude); continue; }
                var open = st.Map.Nodes.Where(n => ExplorationReducer.CanStep(st, n.Id)).ToList();
                var fresh = open.Where(n => !st.HasResolved(n.Id)).ToList();
                var pick = fresh.Count > 0 ? fresh : open;
                MapNode node = pick.FirstOrDefault(n => n.Kind == NodeKind.Elite || n.Kind == NodeKind.Boss) ?? pick[0];
                Assert.That(rec.Step(node.Id), Is.True, "step refused");
            }
            Assert.That(ex.Session.State.IsOver, Is.True, "the life ended");
            Assert.That(fights, Is.GreaterThan(1));
            Assert.That(rec.Log.Any(e => e.Tag == "S" && e.Method == "Step"), Is.True);

            var again = new Expedition(new RunStart { Seed = seed });
            ReplayResult r = LifeReplay.ReplayLife(again, LifeLog.FromJson(Json.Parse(Json.Stringify(rec.Log.ToJson()))));
            Assert.That(r.Open, Is.Null);
            ExplorationState a = again.Session.State;
            ExplorationState b = ex.Session.State;
            Assert.Multiple(() =>
            {
                Assert.That(a.Phase, Is.EqualTo(b.Phase));
                Assert.That(a.Hp, Is.EqualTo(b.Hp));
                Assert.That(a.Stamina, Is.EqualTo(b.Stamina));
                Assert.That(a.CurrentNodeId, Is.EqualTo(b.CurrentNodeId));
                Assert.That(a.Profile.Layer, Is.EqualTo(b.Profile.Layer));
                Assert.That(again.Deck.Select(c => c.InstanceId), Is.EqualTo(ex.Deck.Select(c => c.InstanceId)));
                Assert.That(again.Owned.OrderBy(x => x, StringComparer.Ordinal), Is.EqualTo(ex.Owned.OrderBy(x => x, StringComparer.Ordinal)));
                Assert.That(again.Pages, Is.EqualTo(ex.Pages));
            });
        }

        [Test]
        public void AStepLoggedWithoutItsChoice_ReplaysWithTheDefault()
        {
            var ex = new Expedition(new RunStart { Seed = 3 });
            var log = new LifeLog();
            log.Add(new LogEntry("S", "Step", new object[] { 999, null }));
            log.Add(new LogEntry("S", "Act", new object[] { "Cancel", null }));
            Assert.DoesNotThrow(() => LifeReplay.ReplayLife(ex, log), "Step(node, undefined) and Act(action, null) as the browser logs them");
        }

        [Test]
        public void ATrimmedLog_LeavesTheEncounterWaitingAtItsFirstFight()
        {
            var rec = new HandRecorder(new Expedition(new RunStart { Seed = 11 }));
            EncounterBattle battle = rec.StartEncounter();
            CoreBattleSource src = rec.StartNext(battle, false);
            for (int i = 0; i < 5; i++) rec.PlayerStep(src);
            LifeLog trimmed = LifeLog.TrimUnfinished(rec.Log);
            Assert.That(trimmed.All(e => e.Tag != "C"), Is.True, "no call of the dropped fight is kept");
            var fresh = new Expedition(new RunStart { Seed = 11 });
            ReplayResult r = LifeReplay.ReplayLife(fresh, trimmed);
            Assert.That(r.Open, Is.Null);
            Assert.That(fresh.Battle, Is.Not.Null);
            Assert.That(fresh.Battle.Index, Is.EqualTo(0), "the encounter waits at its first fight");
        }

        // ---- helpers ----

        private static bool Decided(CoreBattleSource src) => src.State.Result != GameResult.Ongoing;

        private static string Snapshot(CoreBattleSource src)
        {
            BattleState s = src.State;
            var sb = new StringBuilder();
            sb.Append(s.Turn).Append('|').Append(s.Phase).Append('|').Append(s.Result).Append('|');
            Unit(sb, s.Player);
            foreach (EnemyUnit e in s.Enemies) Unit(sb, e.Body);
            sb.Append(string.Join(",", s.Hand.Select(c => c.InstanceId))).Append('|');
            sb.Append(string.Join(",", s.DrawPile.Select(c => c.InstanceId))).Append('|');
            sb.Append(string.Join(",", s.DiscardPile.Select(c => c.InstanceId))).Append('|');
            sb.Append(string.Join(",", src.Frame.Hand.Select(f => f.Id))).Append('|');
            sb.Append(src.Finished).Append('|').Append(src.WaitingForPlayer);
            return sb.ToString();
        }

        private static void Unit(StringBuilder sb, CombatantState u) =>
            sb.Append(u.Hp).Append('/').Append(u.Stamina).Append('/').Append(u.Guard).Append('/').Append(u.Cell).Append('|');

        /// <summary>Calls the raw objects and logs each call after it returns (the JS wrap, by hand).</summary>
        private sealed class HandRecorder
        {
            public HandRecorder(Expedition expedition, LifeLog log = null)
            {
                Expedition = expedition;
                Log = log ?? new LifeLog();
            }

            public Expedition Expedition { get; }
            public LifeLog Log { get; }

            public EncounterBattle StartEncounter()
            {
                EncounterBattle r = Expedition.StartEncounter();
                Log.Push("E", "StartEncounter");
                return r;
            }

            public BattleReturn Return()
            {
                BattleReturn r = Expedition.Return();
                Log.Push("E", "Return");
                return r;
            }

            public bool Learn(string cardId)
            {
                bool r = Expedition.Learn(cardId);
                Log.Push("E", "Learn", cardId);
                return r;
            }

            public bool Step(int nodeId)
            {
                bool r = Expedition.Session.Step(nodeId);
                Log.Push("S", "Step", nodeId, null);   // the browser's Step(id, undefined): [id, null]
                return r;
            }

            public bool Act(ScreenAction action)
            {
                bool r = Expedition.Session.Act(action);
                Log.Push("S", "Act", action);
                return r;
            }

            /// <summary>The unattended run's move: the suggested card, else the turn's end; the next event while not waiting.</summary>
            public void SuggestedStep(CoreBattleSource src)
            {
                if (!src.WaitingForPlayer) { AdvanceAuto(src); return; }
                string id = src.SuggestedCardId;
                CardFace face = string.IsNullOrEmpty(id) ? null : DepictionText.Find(src.Frame.Hand, id);
                if (face != null) TryPlay(src, id, DepictionText.RequiredZone(face.Aim));
                else EndTurn(src);
            }

            public CoreBattleSource StartNext(EncounterBattle b, bool suggest)
            {
                CoreBattleSource r = b.StartNext(suggest);
                Log.Push("B", "StartNext", suggest);
                return r;
            }

            public void Finish(EncounterBattle b, CoreBattleSource src)
            {
                b.Finish(src);
                Log.Push("B", "Finish", LifeLog.SourceMarker);
            }

            public void AdvanceAuto(CoreBattleSource src)
            {
                src.AdvanceAuto();
                Log.Push("C", "AdvanceAuto");
            }

            public void EndTurn(CoreBattleSource src)
            {
                src.EndTurn();
                Log.Push("C", "EndTurn");
            }

            public void TryPlay(CoreBattleSource src, string id, DropZone zone)
            {
                src.TryPlay(id, zone, out DepictionEvent _);
                Log.Push("C", "TryPlay", id, zone);
            }

            /// <summary>
            /// test/app/settle.test.mjs playerStep: the leftmost card the source would take, else the turn's end; the
            /// next script event while it is not waiting.
            /// </summary>
            public void PlayerStep(CoreBattleSource src)
            {
                if (src.WaitingForPlayer)
                {
                    CardFace card = src.Frame.Hand.FirstOrDefault(f => src.Inspect(f.Id) == PlayVerdict.Accepted);
                    if (card != null) TryPlay(src, card.Id, DepictionText.RequiredZone(card.Aim));
                    else EndTurn(src);
                }
                else AdvanceAuto(src);
            }
        }
    }
}
