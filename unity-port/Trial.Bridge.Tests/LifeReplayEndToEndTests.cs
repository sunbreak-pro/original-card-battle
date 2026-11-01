// The save reproduces a lineage exactly (tier1 R1-16, #462, INHERITANCE.md R-21): three lives played the way the app
// plays them — through the recorded facades, every fight settled at its decision, a trace taken and its receipt shown,
// the memo written, a survivor — saved at every step (SaveKeeper.Persist after every logged call, as the app's
// PersistSoon, and at the app's own moments), and every saved point loaded again (Save.OpenSave → Life.BeginLife, the
// router's つづきから) must land on the state the life stood in when it was written: the Expedition (session, deck,
// mastery, journal, pages, encounter), the screen, the fight in progress (core state and frame), the lineage, the memo
// and the 走り書き.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BattleCore;
using Depiction.Bridge;
using DungeonContent;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;
using NUnit.Framework;
using static Trial.Bridge.Tests.LifeTestKit;

namespace Trial.Bridge.Tests
{
    public sealed class LifeReplayEndToEndTests
    {
        /// <summary>One saved point: the stored text, and what the game stood on when it was written.</summary>
        private sealed class SavePoint
        {
            public string Label;
            public int Life;
            public string Text;
            public string Lineage;
            public string State;      // hash of Snap(raw), null between lives
            public string Fight;      // hash of BattleSnap(the fight in progress), or null
            public string Memo;
            public string Scribbles;
        }

        /// <summary>The app's half that this test plays: the store, the keeper, the life in progress and the saved points.</summary>
        private sealed class App
        {
            public readonly MemoryKeyValueStore Store = new MemoryKeyValueStore();
            public SaveKeeper Keeper;
            public LiveLife Live;
            public int LifeNo;
            public RecordedSource Fighting;   // the fight whose B.StartNext has no B.Finish yet
            public readonly List<SavePoint> Points = new List<SavePoint>();

            public App(Lineage lineage)
            {
                Keeper = new SaveKeeper(Store, "tab-e2e") { Save = new TrialSave(lineage) };
                Assert.That(Keeper.Persist(force: true), Is.True, "はじめから");
                Point("はじめから");
            }

            public Lineage Lineage
            {
                get => Keeper.Save.Lineage;
                set => Keeper.Save.Lineage = value;
            }

            /// <summary>main.js persist: write the save, then note what the game stands on.</summary>
            public void Persist(string label)
            {
                Assert.That(Keeper.Persist(), Is.True, "the save is written (" + label + ")");
                Point(label);
            }

            /// <summary>The recorder's onCall (the app's PersistSoon, written at once here).</summary>
            public void OnCall(string tag, string method)
            {
                if (tag == "B" && method == "Finish") Fighting = null;
                LogEntry last = Live.Log.Last;
                Seen.Add(method == "Act" ? "S.Act:" + last.Args[0] : tag + "." + method);
                Persist(tag + "." + method);
            }

            /// <summary>Every kind of call the lives made (Act by its action).</summary>
            public readonly SortedSet<string> Seen = new SortedSet<string>(StringComparer.Ordinal);

            /// <summary>
            /// The point B.StartNext saved: its fight's source is handed back only once the call has returned (and the
            /// save is written inside it), so RunEncounter fills its Fight before any call on the source.
            /// </summary>
            public SavePoint Started;

            private void Point(string label)
            {
                Expedition raw = Live?.Raw;
                if (label == "B.StartNext") Started = null;
                Points.Add(new SavePoint
                {
                    Label = label,
                    Life = LifeNo,
                    Text = Store.Read(Save.SaveKey),
                    Lineage = Inheritance.Serialize(Keeper.Save.Lineage),
                    State = raw == null ? null : Hash(Snap(raw)),
                    Fight = raw == null || Fighting == null ? null : Hash(BattleSnap(Fighting)),
                    Memo = Live?.Memo,
                    Scribbles = Live == null ? null : Json.Stringify(Live.ScribblesJson),
                });
                if (label == "B.StartNext") Started = Points[Points.Count - 1];
            }
        }

        /// <summary>How the fights are fought: only the turn's end, the leftmost card that can be played, or the source's AutoCardId (main.js autoFight).</summary>
        private enum Play
        {
            Pass,
            Leftmost,
            Auto,
        }

        /// <summary>
        /// main.js autoFight's step: the source's AutoCardId released on the enemy it goes to (TryPlay with the target, as
        /// player.js:1677 logs it), else the turn's end; the next event while not waiting. 俊敏's free cell is taken forward
        /// when it is open.
        /// </summary>
        private static void AutoStep(ITrialSource source)
        {
            if (source.WaitingForPlayer)
            {
                if (source.CanTakeFreeStep(1))
                {
                    source.TakeFreeStep(1);
                    return;
                }
                string id = source.AutoCardId;
                Depiction.CardFace face = string.IsNullOrEmpty(id) ? null : Depiction.DepictionText.Find(source.Frame.Hand, id);
                if (face != null) source.TryPlay(id, Depiction.DepictionText.RequiredZone(face.Aim), source.TargetFor(id), out Depiction.DepictionEvent _);
                else source.EndTurn();
            }
            else source.AdvanceAuto();
        }

        private static string Hash(string text)
        {
            using (SHA256 sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text))) + ":" + text.Length;
        }

        /// <summary>出立 (main.js onSetOut + beginLife): the lineage moves to the life in progress, the life is saved before it begins.</summary>
        private static void SetOut(App app, LifeStart ls, DepartureChoice choice)
        {
            object before = Inheritance.ToJson(app.Lineage);
            app.Lineage = ls.Lineage;
            app.Keeper.Save.Life = LifeSave.Departing(before);
            if (choice.DeckSave != null) app.Keeper.Save.DeckSave = choice.DeckSave;
            LifeStartData start = Life.MakeStart(ls, choice);
            app.LifeNo++;
            app.Live = Life.BeginLife(start, new LifeLog(), "", null, app.Lineage, app.OnCall);
            app.Keeper.Live = app.Live;
            app.Persist("出立");
        }

        /// <summary>main.js endLife: the end is recorded on the lineage and the life dropped, in one write.</summary>
        private static Lineage EndLife(App app)
        {
            Expedition raw = app.Live.Raw;
            ExplorationState state = raw.Session.State;
            Assert.That(state.IsOver && state.Phase != RunPhase.Exploring, Is.True);
            LifePlace place = Inheritance.PlaceFromState(state);
            app.Lineage = Inheritance.RecordEnd(app.Lineage, null, place, raw.Mastery, Names.LifeJournal(raw, app.Live), Names.EndOptions(raw, state));
            app.Keeper.Save.Life = null;
            app.Live = null;
            app.Keeper.Live = null;
            app.Fighting = null;
            app.Persist("生の終わり");
            return app.Lineage;
        }

        /// <summary>main.js runEncounter: every fight settled at its decision (and saved), then played out.</summary>
        private static void RunEncounter(App app, Play play)
        {
            RecordedExpedition ex = app.Live.Expedition;
            RecordedBattle battle = ex.Battle ?? ex.StartEncounter();
            while (!battle.Over)
            {
                RecordedSource src = battle.StartNext(false);
                app.Fighting = src;
                app.Started.Fight = Hash(BattleSnap(src));
                SettlingFight fight = Recorder.SettleWhenDecided(battle, src, () => app.Persist("決着"));
                for (int g = 0; g < 20000 && !fight.Source.Finished; g++)
                {
                    if (play == Play.Leftmost) PlayerStep(fight.Source);
                    else if (play == Play.Auto) AutoStep(fight.Source);
                    else Step(fight.Source, false);
                }
                fight.Finish();
                app.Persist("戦闘の後");
            }
        }

        /// <summary>main.js inheritance.take: the lineage's take and the logged Inherit in one write, the 走り書き added, the receipt shown.</summary>
        private static void TakeTrace(App app, int layer)
        {
            TraceNodeView view = Inheritance.TraceNodeView(app.Lineage, layer);
            Assert.That(view != null && view.CanTake, Is.True, "the trace node offers its trace");
            List<string> picks = view.Candidates.Take(3).Select(c => c.Id).ToList();
            TakeResult r = Inheritance.TakeTrace(app.Lineage, view.TraceId, picks, layer);
            app.Lineage = r.Lineage;
            // The take as Inheritance.TakeTrace returns it (the JS clones it through JSON first; the replay reads the
            // logged JSON back, so this also holds TakenLegacy's round trip).
            TakenLegacy taken = r.Taken;
            InheritResult result = app.Live.Expedition.Inherit(taken);
            AddedScribbles added = Life.AddScribbles(app.Live.Scribbles, taken.Journal.Scribbles);
            app.Live.Scribbles = added.List;
            app.Persist("痕跡を受け取った");
            // the 受け取った遺産 card, written from what the take did, shown and closed (ExploreScene takenCard)
            var rows = view.Candidates.Where(c => picks.Contains(c.Id))
                .Select(c => new LegacyReceiptRow(c.Kind, c.KindLabel, c.Name, c.Detail, new[] { result.Cards.Count + " 枚" }, false));
            Assert.That(app.Live.Expedition.ShowTaken(new LegacyReceipt("place", "heir", rows, taken.Lost)), Is.True);
            Assert.That(app.Live.Raw.PendingTaken, Is.Not.Null);
            app.Live.Expedition.CloseTaken();
        }

        /// <summary>
        /// Uses the first consumable that would change something (CanUseSlot); 標の石 aims, then jumps to the first node the
        /// screen offers a jump to (or the aim is put away with Esc). False when no slot can be used.
        /// </summary>
        private static bool UseAnItem(RecordedSession s)
        {
            ExplorationState st = s.State;
            for (int i = 0; i < st.Loadout.Consumables.Count; i++)
            {
                if (!s.CanUseSlot(i)) continue;
                bool teleport = st.Loadout.Consumables[i].Kind == ReliefKind.Teleport;
                Assert.That(s.UseConsumable(i), Is.True);
                if (teleport && s.IsAiming)
                {
                    NodeCard target = s.Screen().Nodes.FirstOrDefault(n => n.Options.Any(o => o.Jump));
                    if (target != null) s.Jump(target.Id, target.Options.First(o => o.Jump).Choice);
                    else s.Escape();
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Walks the life in progress the way the explore scene does: fights through RunEncounter, the result card closed
        /// (or the pick learned), trial choices answered, a trace node on the way taken, and — with surviveAt — the life
        /// ended alive at that layer's interlude. <paramref name="play"/>: how the fights are fought.
        /// </summary>
        private static ExplorationState WalkLife(App app, Play play, int? surviveAt = null, string memoAt = null, bool useItems = false)
        {
            RecordedExpedition ex = app.Live.Expedition;
            RecordedSession s = ex.Session;
            int usedOnLayer = 0;
            for (int guard = 0; guard < 800; guard++)
            {
                ExplorationState st = s.State;
                if (s.HasPendingBattle)
                {
                    RunEncounter(app, play);
                    ex.Return();
                    app.Persist("戦闘から戻った");
                    if (ex.Offer.Count > 0) ex.Learn(ex.Offer[0]);
                    else if (ex.PendingResult != null) ex.CloseResult();
                    continue;
                }
                if (s.HasPendingStep) { s.Act(ScreenAction.ConfirmStep); continue; }
                if (s.HasPendingEvent) { s.Act(ScreenAction.AcceptEvent); continue; }
                if (s.HasPendingFound) { if (!s.Act(ScreenAction.TakeFound, "0")) s.Act(ScreenAction.LeaveFound); continue; }
                if (s.HasPendingSurvey) { s.Act(ScreenAction.ChooseSurvey, s.SurveyChoices[0]); continue; }
                if (ex.Offer.Count > 0) { ex.Learn(ex.Offer[0]); continue; }
                if (ex.PendingResult != null) { ex.CloseResult(); continue; }
                if (s.TrainingWaiting)
                {
                    // the training card: an owned card trained (life 3), or put off
                    if (!(useItems && ex.Train(ex.Deck[0].Def.Id))) s.PutOffTraining();
                    continue;
                }
                if (s.ConfirmingDescend) { s.Act(ScreenAction.ConfirmDescend); continue; }
                if (st.IsOver) return st;
                if (useItems && usedOnLayer != st.Profile.Layer && st.Phase == RunPhase.Exploring && UseAnItem(s))
                {
                    usedOnLayer = st.Profile.Layer;
                    continue;
                }
                if (memoAt != null && app.Live.Memo.Length == 0)
                {
                    // the journal's memo, typed and saved (journal.js onMemo → persist)
                    app.Live.Memo = memoAt;
                    app.Persist("メモ");
                }
                if (st.Phase == RunPhase.Interlude)
                {
                    if (surviveAt.HasValue && st.Profile.Layer >= surviveAt.Value)
                    {
                        s.Act(ScreenAction.AskEndLife);
                        s.Act(ScreenAction.ConfirmEndLife);
                        continue;
                    }
                    s.Act(ScreenAction.Descend);
                    continue;
                }
                if (ExplorationReducer.CanInterlude(st)) { s.Act(ScreenAction.GoToInterlude); continue; }
                List<MapNode> open = st.Map.Nodes.Where(n => ExplorationReducer.CanStep(st, n.Id)).ToList();
                List<MapNode> fresh = open.Where(n => !st.HasResolved(n.Id)).ToList();
                List<MapNode> pick = fresh.Count > 0 ? fresh : open;
                // toward the trace node while this life has not taken its trace
                MapNode trace = app.Live.Raw.InheritedTraceId == null
                    ? st.Map.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Trace && !st.HasResolved(n.Id))
                    : null;
                MapNode node = null;
                if (trace != null)
                {
                    int first = RouteTo(st.Map, st.CurrentNodeId, trace.Id).First();
                    node = open.FirstOrDefault(n => n.Id == first);
                }
                node = node ?? pick[0];
                Assert.That(s.Step(node.Id), Is.True, "step refused");
                if (node.Kind == NodeKind.Trace && !s.State.IsOver) TakeTrace(app, st.Profile.Layer);
            }
            throw new AssertionException("the life did not end");
        }

        /// <summary>Loads a saved point as つづきから does and returns what it stands on (hashes, as the point keeps them).</summary>
        private static SavePoint Load(SavePoint p)
        {
            var store = new MemoryKeyValueStore();
            store.Write(Save.SaveKey, p.Text);
            OpenedSave opened = Save.OpenSave(store);
            Assert.That(opened.Unreadable, Is.Null, p.Label);
            TrialSave save = opened.Save;
            var got = new SavePoint { Label = p.Label, Life = p.Life, Text = p.Text, Lineage = Inheritance.Serialize(save.Lineage) };
            if (save.Life == null || !save.Life.Has("start")) return got;
            LiveLife live = Life.BeginLife(save.Life.ReadStart(), save.Life.ReadLog(), save.Life.Memo, save.Life.Scribbles, save.Lineage);
            got.State = Hash(Snap(live.Raw));
            got.Fight = live.OpenSource == null ? null : Hash(BattleSnap(live.OpenSource));
            got.Memo = live.Memo;
            got.Scribbles = Json.Stringify(live.ScribblesJson);
            return got;
        }

        // Lineage 5: the survivor's heir dies on layer 3 (636 saved points). Lineage 1: it reaches layer 6 (1031 saved points;
        // descents, events, found consumables, the survey's pick, consumables and a 標の石 jump on the way). Lineages 6 and 8
        // lose life 2 on layer 1, so they do not play this story.
        [TestCase(5UL)]
        [TestCase(1UL)]
        public void ThreeLives_ADeath_ATraceTaken_ASurvivor_ReplayFromEverySavedPoint_ToTheStateTheyWereSavedIn(ulong lineageSeed)
        {
            var app = new App(Inheritance.NewLineage(lineageSeed));

            // ---- life 1: dies in its first fight (no card played) and leaves its trace on layer 1
            LifeStart ls1 = Inheritance.StartLife(app.Lineage);
            SetOut(app, ls1, Departure(ls1, 1));
            ExplorationState end1 = WalkLife(app, Play.Pass);
            Assert.That(end1.Phase, Is.EqualTo(RunPhase.Fallen), "life 1 falls");
            int traceLayer = end1.Profile.Layer;
            EndLife(app);
            Assert.That(app.Lineage.Traces.Count, Is.EqualTo(1), "life 1 left a trace");

            // ---- life 2: takes that trace, writes a memo, and ends alive at its layer's interlude
            LifeStart ls2 = Inheritance.StartLife(app.Lineage);
            Assert.That(ls2.TraceLayers, Does.Contain(traceLayer));
            SetOut(app, ls2, Departure(ls2, 2));
            Expedition raw2 = app.Live.Raw;
            ExplorationState end2 = WalkLife(app, Play.Leftmost, surviveAt: traceLayer, memoAt: "二代目のメモ");
            Assert.That(raw2.InheritedTraceId, Is.Not.Null, "life 2 took the trace");
            Assert.That(end2.Phase, Is.EqualTo(RunPhase.Survived), "life 2 survives");
            EndLife(app);
            Assert.That(app.Lineage.Survivor, Is.Not.Null);

            // ---- life 3: the survivor's heir (4 tool slots, a 45-card cap, the survivor's journal), played until it ends
            LifeStart ls3 = Inheritance.StartLife(app.Lineage);
            Assert.That(ls3.SurvivorBonus && ls3.ToolSlots == 4 && ls3.DeckMax == 45, Is.True);
            DepartureChoice choice3 = Departure(ls3, 1);
            choice3.Loadout = Loadout.Of(new[] { "bosho_no_men", "kokugen_no_suna", "ikitsugi_no_fue", "maai_no_kutsu" },
                new[] { "shirube_no_ishi", "yakuso", "koi_hoshiniku" }, 4);
            choice3.BattleToolIds = new[] { "ikitsugi_no_fue", "maai_no_kutsu" };
            SetOut(app, ls3, choice3);
            Assert.That(app.Live.Scribbles.Any(sc => sc.Text == "二代目のメモ"), Is.True, "the survivor's memo is the heir's 走り書き");
            Assert.That(app.Live.Raw.DeckMax, Is.EqualTo(45));
            ExplorationState end3 = WalkLife(app, Play.Auto, useItems: true);
            TestContext.Progress.WriteLine($"life 3: {end3.Phase} on layer {end3.Profile.Layer}");
            EndLife(app);
            Assert.That(app.Lineage.Lives.Count, Is.EqualTo(3));

            // ---- every saved point, loaded again
            int lives = 0, fights = 0;
            foreach (SavePoint p in app.Points)
            {
                SavePoint got = Load(p);
                string at = $"point {app.Points.IndexOf(p)} ({p.Label}, life {p.Life})";
                Assert.That(got.Lineage, Is.EqualTo(p.Lineage), at + ": the lineage");
                Assert.That(got.State, Is.EqualTo(p.State), at + ": the life's state and screen");
                Assert.That(got.Fight, Is.EqualTo(p.Fight), at + ": the fight in progress");
                Assert.That(got.Memo, Is.EqualTo(p.Memo), at + ": the memo");
                Assert.That(got.Scribbles, Is.EqualTo(p.Scribbles), at + ": the 走り書き");
                if (p.State != null) lives++;
                if (p.Fight != null) fights++;
            }
            TestContext.Progress.WriteLine($"{app.Points.Count} saved points, {lives} inside a life, {fights} inside a fight; " +
                $"the last save {app.Points[app.Points.Count - 1].Text.Length} chars; calls: {string.Join(" ", app.Seen)}");
            Assert.That(fights, Is.GreaterThan(10), "fights were saved mid-way");
            Assert.That(app.Points.Any(p => p.Label == "E.Inherit") && app.Points.Any(p => p.Label == "E.ShowTaken") && app.Points.Any(p => p.Label == "メモ"), Is.True);
        }
    }
}
