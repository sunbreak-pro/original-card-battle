// The recording facades (src/app/recorder.js wrap / recordExpedition, as C# facades): which calls are logged and how,
// that a throwing call logs nothing, that every public mutator of the four recorded types is either logged, a read, or a
// call the game never makes from a screen, and the replay switch for the Expedition's trial calls.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BattleCore;
using Depiction;
using Depiction.Bridge;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;
using NUnit.Framework;
using static Trial.Bridge.Tests.LifeTestKit;

namespace Trial.Bridge.Tests
{
    public sealed class RecorderTests
    {
        [SetUp]
        public void ResetShared() => ReplayTable.ResetShared();

        [TearDown]
        public void ResetSharedAfter() => ReplayTable.ResetShared();

        private static string Text(LifeLog log) => Json.Stringify(log.ToJson());

        private static Expedition TrialLife(ulong seed)
        {
            LifeStart ls = Inheritance.StartLife(Inheritance.NewLineage(seed));
            return Life.LifeSetup(Clone(Life.MakeStart(ls, Departure(ls)))).Expedition;
        }

        // js: app/recorder.js:23 / :47 / :56
        [Test]
        public void EachCall_IsLoggedOnceItReturns_WithItsArgumentsAsJson_ThenOnCallRuns()
        {
            var calls = new List<string>();
            var log = new LifeLog();
            RecordedExpedition ex = Recorder.RecordExpedition(new Expedition(new RunStart { Seed = 11 }), log, (t, m) => calls.Add(t + "." + m + "@" + log.Calls));
            Assert.That(ex.Log, Is.SameAs(log));
            Assert.That(ex.Battle, Is.Null);
            RecordedBattle battle = ex.StartEncounter();
            Assert.That(ex.Battle, Is.SameAs(battle), "the encounter's facade is kept per encounter");
            Assert.That(battle.Raw, Is.SameAs(ex.Raw.Battle));
            RecordedSource src = battle.StartNext(false);
            int advances = 0;
            while (!src.WaitingForPlayer)
            {
                src.AdvanceAuto();
                advances++;
            }
            Assert.That(advances, Is.GreaterThanOrEqualTo(1), "the opening's script events");
            CardFace card = src.Frame.Hand.First(f => src.Inspect(f.Id) == PlayVerdict.Accepted);
            DropZone zone = DepictionText.RequiredZone(card.Aim);
            src.TryPlay(card.Id, zone, out DepictionEvent _);
            src.TryPlay(card.Id, zone, 0, out DepictionEvent _);   // refused (gone from the hand): logged too, it replays the same
            src.PreviewFor(card.Id);
            src.Inspect(card.Id, 0);
            src.CanTakeFreeStep();
            src.TargetFor(card.Id);
            string json = Text(log);
            Assert.That(json, Is.EqualTo("[[\"E\",\"StartEncounter\",[]],[\"B\",\"StartNext\",[false]],[\"C\",\"AdvanceAuto\",[]" + (advances > 1 ? "," + advances : "") + "]," +
                "[\"C\",\"TryPlay\",[\"" + card.Id + "\",\"" + zone + "\"]],[\"C\",\"TryPlay\",[\"" + card.Id + "\",\"" + zone + "\",0]]]"));
            Assert.That(log.Count, Is.EqualTo(5), "reads are not logged");
            var expected = new List<string> { "E.StartEncounter@1", "B.StartNext@2" };
            for (int i = 1; i <= advances; i++) expected.Add("C.AdvanceAuto@" + (2 + i));
            expected.Add("C.TryPlay@" + (3 + advances));
            expected.Add("C.TryPlay@" + (4 + advances));
            Assert.That(calls, Is.EqualTo(expected), "onCall runs after each logged call, the run counted up first");

            // a call that throws changed nothing and is not logged
            Assert.Throws<InvalidOperationException>(() => ex.StartEncounter());
            Assert.Throws<InvalidOperationException>(() => ex.Return());
            Assert.Throws<InvalidOperationException>(() => battle.StartNext(false));
            Assert.That(log.Count, Is.EqualTo(5));
            Assert.That(calls.Count, Is.EqualTo(expected.Count));

            // the replay goes on from where the log stands
            Expedition again = new Expedition(new RunStart { Seed = 11 });
            ReplayResult r = Recorder.ReplayLife(again, Saved(log));
            Assert.That(BattleSnap(r.Open.Source), Is.EqualTo(BattleSnap(src)));
        }

        [Test]
        public void TheSessionsCalls_AreLoggedWithTheirEnumsByName()
        {
            Expedition raw = new Expedition(new RunStart { Seed = 11 });
            RecordedExpedition ex = Recorder.RecordExpedition(raw);
            RecordedSession s = ex.Session;
            Assert.That(s.Raw, Is.SameAs(raw.Session));
            Assert.That(ex.Session, Is.SameAs(s));
            s.Act(ScreenAction.WinBattle);
            MapNode next = s.State.Map.Nodes.First(n => ExplorationReducer.CanStep(s.State, n.Id));
            s.Step(next.Id, RestChoice.Rest);
            s.Toggle(Overlay.Journal);
            s.Escape();
            s.Act(ScreenAction.TakeFound, "0");
            s.UseConsumable(9);
            s.OpenTraining();
            s.PutOffTraining();
            s.Jump(1);
            s.Screen();
            s.CanOpen(Overlay.Cards);
            Assert.That(Text(ex.Log), Is.EqualTo(
                "[[\"S\",\"Act\",[\"WinBattle\"]],[\"S\",\"Step\",[" + next.Id + ",\"Rest\"]],[\"S\",\"Toggle\",[\"Journal\"]],[\"S\",\"Escape\",[]]," +
                "[\"S\",\"Act\",[\"TakeFound\",\"0\"]],[\"S\",\"UseConsumable\",[9]],[\"S\",\"OpenTraining\",[]],[\"S\",\"PutOffTraining\",[]],[\"S\",\"Jump\",[1,\"Rest\"]]]"),
                "a refused call (false) is logged too: it replays to the same refusal");
            Expedition again = new Expedition(new RunStart { Seed = 11 });
            Recorder.Replay(again, Saved(ex.Log));
            Assert.That(Snap(again), Is.EqualTo(Snap(raw)));
        }

        // The JS Proxy logs everything but READ_ONLY; the facades log a fixed list. This holds the list against the four
        // types: a public method of theirs is logged, is a read (the JS's READ_ONLY, or one the C# has that the JS has not),
        // or is a call the cores make on themselves and no screen makes.
        [Test]
        public void EveryPublicMethodOfTheFourTypes_IsLogged_ARead_OrAnInternalCall()
        {
            var reads = new HashSet<string>(Recorder.ReadOnly, StringComparer.Ordinal)
            {
                "E.EnemyName",        // the JS reads it through the raw Expedition (not in READ_ONLY: a screen must not call it through the recorder)
                "S.LayerSeed", "S.EncounterSeed", "S.EventSeed", "S.LayerEnemies", "S.CarvingKnownAt",
                "E.RewardSeed",
                "C.Slice",            // statics
                "S.CanUseConsumable", // #651: the C# slot's read (trial screens read CanUseSlot)
            };
            var internalCalls = new HashSet<string>(StringComparer.Ordinal)
            {
                "E.TakeChest", "E.TakeSurvey",                                   // the session's Keeper
                "S.ReportBattle", "S.SpendTrainingMark", "S.Receive", "S.TurnDownFind", // the Expedition's own calls on its session
                "B.TryFinish",                                                   // RecordedBattle.TryFinish logs it as B.Finish
                "S.TakeConsumable",                                              // #651: the Expedition's own call (Stow / SwapConsumable)
                // #651: a found consumable waiting outside trial mode; the trial session asks for the swap itself
                // (S.Act TakeFound / LeaveFound), so nothing waits here and no trial screen makes these.
                "E.SwapConsumable", "E.DiscardConsumable",
            };
            var types = new (string Tag, Type Type)[]
            {
                ("E", typeof(Expedition)), ("S", typeof(ExplorationSession)), ("B", typeof(EncounterBattle)), ("C", typeof(CoreBattleSource)),
            };
            var unaccounted = new List<string>();
            foreach ((string tag, Type type) in types)
            {
                foreach (MethodInfo m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.IsSpecialName) continue;   // property accessors (getters are never logged)
                    if (m.DeclaringType == typeof(object)) continue;
                    string key = tag + "." + m.Name;
                    if (Recorder.Logged.Contains(key) || reads.Contains(m.Name) || reads.Contains(key) || internalCalls.Contains(key)) continue;
                    if (m.Name == "Equals" || m.Name == "GetHashCode" || m.Name == "ToString" || m.Name == "GetType") continue;
                    unaccounted.Add(key);
                }
            }
            Assert.That(unaccounted.Distinct(), Is.Empty, "a public method neither logged nor known as a read");

            // and each logged call has its facade method and its replay entry
            ReplayTable table = Recorder.TrialTable();
            var facades = new Dictionary<string, Type>
            {
                ["E"] = typeof(RecordedExpedition), ["S"] = typeof(RecordedSession), ["B"] = typeof(RecordedBattle), ["C"] = typeof(RecordedSource),
            };
            foreach (string key in Recorder.Logged)
            {
                string[] parts = key.Split('.');
                Assert.That(facades[parts[0]].GetMethods().Any(m => m.Name == parts[1]), Is.True, key + " has its facade method");
                Assert.That(table.Has(parts[0], parts[1]), Is.True, key + " replays");
            }

            // Reads go through (the JS Proxy hands every getter and every READ_ONLY method of the raw object on): each public
            // instance property of the four types, and each instance method that is a read, is on its facade.
            var missing = new List<string>();
            foreach ((string tag, Type type) in types)
            {
                Type facade = facades[tag];
                foreach (PropertyInfo p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (p.GetIndexParameters().Length > 0) continue;
                    if (facade.GetProperty(p.Name, BindingFlags.Public | BindingFlags.Instance) == null) missing.Add(tag + "." + p.Name);
                }
                foreach (MethodInfo m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (m.IsSpecialName) continue;
                    string key = tag + "." + m.Name;
                    if (Recorder.Logged.Contains(key) || internalCalls.Contains(key)) continue;
                    if (m.Name == "Equals" || m.Name == "GetHashCode" || m.Name == "ToString" || m.Name == "GetType") continue;
                    if (!facade.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(f => f.Name == m.Name)) missing.Add(key + "()");
                }
            }
            Assert.That(missing.Distinct(), Is.Empty, "a read of the raw object that its facade does not hand on");
        }

        // The facades' reads are the raw objects' (the explore scene reads the pending cards through the recorded session).
        [Test]
        public void TheFacadesReads_AreTheRawObjects()
        {
            Expedition raw = TrialLife(31);
            RecordedExpedition ex = Recorder.RecordExpedition(raw);
            RecordedSession s = ex.Session;
            Assert.That(s.PendingBattleNode, Is.EqualTo(raw.Session.PendingBattleNode));
            Assert.That(s.PendingEncounter, Is.SameAs(raw.Session.PendingEncounter));
            Assert.That(s.Keeper, Is.SameAs(raw));
            Assert.That(s.CanUseConsumables, Is.EqualTo(raw.Session.CanUseConsumables));
            Assert.That(s.MapOf(SevenLayers.Of(2), 5).Fingerprint(), Is.EqualTo(raw.Session.MapOf(SevenLayers.Of(2), 5).Fingerprint()));
            Assert.That(s.CarvingKnownAt(raw.Session.RunSeed), Is.EqualTo(raw.Session.CarvingKnownAt(raw.Session.RunSeed)));
            Assert.That(ex.EnemyName("shadow_hound"), Is.EqualTo("瘴牙の走竜"));
            Assert.That(ex.Log, Is.Empty, "reads are not logged");
        }

        [Test]
        public void TheTrialTable_IsTheStandardOne_WithTheExpeditionsTrialCalls_AndTheSharedOneIsLeftAlone()
        {
            ReplayTable t = Recorder.TrialTable();
            Assert.That(t.Keys.Except(ReplayTable.Standard().Keys), Is.EqualTo(new[] { "E.CloseResult", "E.CloseTaken", "E.Inherit", "E.ShowTaken" }));
            Assert.That(ReplayTable.Shared.Has("E", "Inherit"), Is.False);
            ReplayTable shared = Recorder.Register(ReplayTable.Shared);
            Assert.That(shared, Is.SameAs(ReplayTable.Shared));
            Assert.That(ReplayTable.Shared.Has("E", "Inherit"), Is.True, "Register adds to the table it is given");
        }

        // The Expedition's trial calls: logged by the facade with JSON arguments, replayed by the table, to the same life.
        [Test]
        public void InheritAndTheReceipt_AreLoggedAsJson_AndReplayToTheSameLife()
        {
            Expedition raw = TrialLife(31);
            RecordedExpedition ex = Recorder.RecordExpedition(raw);
            Assert.That(ex.ShowTaken(new LegacyReceipt("p", "h", null, 0)), Is.False, "no trace taken yet: refused");
            var taken = new TakenLegacy("trace-9", new[] { new TakenCard("feint", 2, 6, "フェイント") },
                new InheritanceCore.Journal(new[] { new KeyValuePair<string, int>("shadow_hound", 2) }, new[] { new InheritanceCore.JournalPage("page-1", "希少な頁 1") },
                    "", new[] { new Scribble(1, "ミナト", "走り書き") }), 2);
            InheritResult result = ex.Inherit(taken);
            Assert.That(result.Cards.Single().CardId, Is.EqualTo("feint"));
            var card = new LegacyReceipt("place", "heir", new[]
            {
                new LegacyReceiptRow(CandidateKind.Card, "技", "フェイント", "2 段目", new[] { "いま 0 段目 → 2 段目" }, false),
            }, 2);
            Assert.That(ex.ShowTaken(card), Is.True);
            Assert.That(raw.PendingTaken, Is.SameAs(card));
            Assert.That(ex.Log.Select(e => e.ToString()), Is.EqualTo(new[] { "E.ShowTaken", "E.Inherit", "E.ShowTaken" }));
            Assert.That(Json.Stringify(ex.Log[1].Args[0]), Is.EqualTo(Json.Stringify(taken.ToJson())));
            Assert.That(Json.Stringify(ex.Log[2].Args[0]), Is.EqualTo(Json.Stringify(card.ToJson())));

            Expedition again = TrialLife(31);
            Recorder.Replay(again, Saved(ex.Log));
            Assert.That(again.InheritedTraceId, Is.EqualTo("trace-9"));
            Assert.That(again.Mastery.RankOf("feint"), Is.EqualTo(2));
            Assert.That(again.DisclosureOf("shadow_hound"), Is.EqualTo(2));
            Assert.That(again.InheritedPages, Is.EqualTo(raw.InheritedPages));
            Assert.That(Json.Stringify(again.PendingTaken.ToJson()), Is.EqualTo(Json.Stringify(card.ToJson())), "the receipt comes back after a reload");

            ex.CloseTaken();
            ex.CloseResult();
            Assert.That(raw.PendingTaken, Is.Null);
            Expedition third = TrialLife(31);
            Recorder.Replay(third, Saved(ex.Log));
            Assert.That(third.PendingTaken, Is.Null);
            Assert.That(Snap(third), Is.EqualTo(Snap(raw)));

            // an argument that does not read stops the replay
            var bad = new LifeLog();
            bad.Add(new LogEntry("E", "Inherit", new object[] { "not a take" }));
            Assert.Throws<ReplayException>(() => Recorder.ReplayLife(TrialLife(31), bad));
            var badCard = new LifeLog();
            badCard.Add(new LogEntry("E", "ShowTaken", new object[] { new JsonObject { ["Rows"] = new List<object> { new JsonObject { ["Kind"] = "Nope" } } } }));
            Assert.Throws<ReplayException>(() => Recorder.ReplayLife(TrialLife(31), badCard));
            // a null take is Inherit(null), as the JS's Inherit(undefined)
            var none = new LifeLog();
            none.Add(new LogEntry("E", "Inherit", new object[] { null }));
            Expedition nothing = TrialLife(31);
            Recorder.ReplayLife(nothing, none);
            Assert.That(nothing.InheritedTraceId, Is.Null);
        }

        // The C#'s EncounterBattle.TryFinish (no JS counterpart) is logged as the B.Finish it is, only when it finished.
        [Test]
        public void TryFinish_IsLoggedAsFinish_OnlyWhenItFinished()
        {
            RecordedExpedition ex = Recorder.RecordExpedition(new Expedition(new RunStart { Seed = 11 }));
            RecordedBattle battle = ex.StartEncounter();
            RecordedSource src = battle.StartNext(false);
            Assert.That(battle.TryFinish(src), Is.False);
            Assert.That(ex.Log.Count(e => e.Method == "Finish"), Is.EqualTo(0));
            for (int guard = 0; guard < 20000 && !Decided(src); guard++)
            {
                if (src.WaitingForPlayer) src.EndTurn();
                else src.AdvanceAuto();
            }
            Assert.That(battle.TryFinish(src), Is.True);
            Assert.That(Json.Stringify(ex.Log.Last.ToJson()), Is.EqualTo("[\"B\",\"Finish\",[\"$source\"]]"));
            Expedition again = new Expedition(new RunStart { Seed = 11 });
            ReplayResult r = Recorder.ReplayLife(again, Saved(ex.Log));
            Assert.That(r.Open, Is.Null);
            Assert.That(again.Battle.Lost, Is.True);
        }

        // js: app/main.js:306 (beginLife: a fight whose calls no longer replay starts over, with a warning)
        [Test]
        public void BeginLife_ReplaysTheLog_ResumesTheOpenFight_AndStartsOverAFightThatNoLongerReplays()
        {
            LifeStart ls = Inheritance.StartLife(Inheritance.NewLineage(11));
            LifeStartData start = Clone(Life.MakeStart(ls, Departure(ls)));
            var calls = new List<string>();
            LiveLife live = Life.BeginLife(start, new LifeLog(), "", null, ls.Lineage, (t, m) => calls.Add(t + "." + m));
            Assert.That(live.OpenSource, Is.Null);
            RecordedBattle battle = live.Expedition.StartEncounter();
            RecordedSource src = battle.StartNext(false);
            for (int i = 0; i < 6; i++) PlayerStep(src);
            Assert.That(calls.Count, Is.GreaterThanOrEqualTo(8));
            LifeLog saved = Saved(live.Log);

            LiveLife back = Life.BeginLife(start, saved, "m", Life.ScribblesToJson(new[] { new Scribble(1, "a", "t") }), ls.Lineage);
            Assert.That(back.OpenSource, Is.Not.Null, "the fight in progress comes back");
            Assert.That(BattleSnap(back.OpenSource), Is.EqualTo(BattleSnap(src)));
            Assert.That(back.Memo, Is.EqualTo("m"));
            Assert.That(back.Scribbles.Single().Text, Is.EqualTo("t"));
            Assert.That(back.Log.Count, Is.EqualTo(saved.Count));
            PlayerStep(back.OpenSource);
            Assert.That(back.Log.Last.Tag, Is.EqualTo("C"), "the resumed fight records into the same log");
            Assert.That(back.Log.Calls, Is.EqualTo(saved.Calls + 1));

            // a fight call that no longer replays: the fight starts over (the rest of the life stands)
            var broken = new LifeLog(saved.Select(e => e.Copy()));
            broken.Add(new LogEntry("C", "TakeFreeStep", new object[] { 5 }));
            string warned = null;
            Action<string, Exception> warn = Save.Warn;
            Save.Warn = (msg, e) => warned = msg;
            try
            {
                LiveLife over = Life.BeginLife(start, broken);
                Assert.That(over.OpenSource, Is.Null);
                Assert.That(over.Log.All(e => e.Tag != "C"), Is.True);
                Assert.That(over.Raw.Battle, Is.Not.Null, "the node waits on the same fight");
                Assert.That(warned, Is.EqualTo("the fight in progress did not replay; it starts over"));
            }
            finally { Save.Warn = warn; }

            // a call before the last fight that does not replay: thrown as it is (the router's 「生を再開できません」)
            var bad = new LifeLog();
            bad.Add(new LogEntry("S", "Act", new object[] { "NoSuchAction" }));
            Assert.Throws<ReplayException>(() => Life.BeginLife(start, bad));
        }
    }
}
