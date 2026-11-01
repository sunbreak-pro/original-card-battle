// Headless lives through the app layer (js: test/app/life.test.mjs, survivor.test.mjs, the life parts of
// inherit-gains.test.mjs): departure data → Expedition with the lineage's maps → walk, fight (unattended, through the
// recorded facades), die or survive → record → next life with a trace node → take it → the inherited cards and pages
// reach the Expedition. Every life is replayed from its saved start and log and must land on the same state.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using DungeonContent;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;
using NUnit.Framework;
using static Trial.Bridge.Tests.LifeTestKit;
using LifeJournalData = InheritanceCore.Journal;

namespace Trial.Bridge.Tests
{
    public sealed class LifeTests
    {
        // ================================================================== life.test.mjs

        // js: test/app/life.test.mjs:89
        [Test]
        public void LivesDie_LaterLivesFindTheTraces_TakeThem_AndEveryLifeReplaysExactly()
        {
            Lineage lineage = Inheritance.NewLineage(7);
            int taken = 0;
            for (int g = 1; g <= 12 && taken < 2; g++)
            {
                LifeStart ls = Inheritance.StartLife(lineage);
                lineage = ls.Lineage;
                LifeStartData start = Life.MakeStart(ls, Departure(ls, g));
                Expedition raw = Life.LifeSetup(Clone(start)).Expedition;
                RecordedExpedition rec = Recorder.RecordExpedition(raw);
                int? tookOn = null;
                ExplorationState end = Walk(rec, PreferTrace, new WalkOptions
                {
                    OnTrace = layer =>
                    {
                        TraceNodeView view = Inheritance.TraceNodeView(lineage, layer);
                        Assert.That(view != null && view.CanTake, Is.True, "the trace node offers its trace");
                        Assert.That(ls.TraceLayers, Does.Contain(layer), "the node stands on a trace layer");
                        List<string> picks = view.Candidates.Take(3).Select(c => c.Id).ToList();
                        TakeResult r = Inheritance.TakeTrace(lineage, view.TraceId, picks, layer);
                        lineage = r.Lineage;
                        rec.Inherit(TakenLegacy.FromJson(Json.Parse(Json.Stringify(r.Taken.ToJson()))));
                        tookOn = layer;
                        taken++;
                        foreach (TakenCard c in r.Taken.Cards)
                        {
                            Assert.That(raw.Owned, Does.Contain(c.CardId), "inherited card is owned");
                            Assert.That(raw.Mastery.RankOf(c.CardId), Is.GreaterThanOrEqualTo(Math.Min(c.Rank, 2)), "inherited rank kept");
                        }
                    },
                });
                Assert.That(end.IsOver, Is.True);
                if (tookOn != null)
                    Assert.That(end.Map.Nodes.All(n => n.Kind != NodeKind.Trace) || end.Profile.Layer == tookOn, Is.True, "no trace node after the take");

                LifeLog log = Saved(rec.Log);
                Expedition again = Life.LifeSetup(Clone(start)).Expedition;
                Recorder.Replay(again, log);
                Assert.That(Snap(again), Is.EqualTo(Snap(raw)), $"life {g} replays to the same state");

                LifePlace place = Inheritance.PlaceFromState(end);
                lineage = Inheritance.RecordEnd(lineage, null, place, raw.Mastery, Names.LifeJournal(raw, g == 1 ? "メモ" : ""), Names.EndOptions(raw, end));
                TestContext.Progress.WriteLine($"life {g}: {place.Ending} on layer {place.Layer}; traces at start [{string.Join(",", ls.TraceLayers)}]; took {tookOn}; log {log.Count} entries / {Json.Stringify(log.ToJson()).Length} chars");
            }
            Assert.That(taken, Is.GreaterThanOrEqualTo(1), "some life took a trace");
            HallView hall = Inheritance.HallView(lineage);
            Assert.That(hall.PastLives.Count, Is.GreaterThanOrEqualTo(2));
        }

        // js: test/app/life.test.mjs:134
        [Test]
        public void AnUnfinishedFight_StaysInTheLog_AndComesBackWhereItStood()
        {
            Lineage lineage = Inheritance.NewLineage(11);
            LifeStart ls = Inheritance.StartLife(lineage);
            LifeStartData start = Life.MakeStart(ls, Departure(ls));
            Expedition raw = Life.LifeSetup(start).Expedition;
            RecordedExpedition ex = Recorder.RecordExpedition(raw);
            Assert.That(ex.Session.HasPendingBattle, Is.True, "the entry node is a battle");
            RecordedBattle battle = ex.StartEncounter();
            RecordedSource src = battle.StartNext(false);
            // play a few steps, then "close the tab"
            for (int i = 0; i < 5 && !src.Finished; i++)
            {
                if (src.WaitingForPlayer) src.EndTurn();
                else src.AdvanceAuto();
            }
            LifeLog log = Saved(ex.Log);
            ReplayResult r = Recorder.ReplayLife(Life.LifeSetup(start).Expedition, log);
            Assert.That(r.Kept.Any(e => e.Tag == "C"), Is.True, "the fight's calls are kept");
            Assert.That(r.Open, Is.Not.Null, "the fight is in progress after the reload");
            Assert.That(r.Open.Source.State.Turn, Is.EqualTo(src.State.Turn));
            Assert.That(r.Open.Source.State.Player.Hp, Is.EqualTo(src.State.Player.Hp));
            Assert.That(r.Open.Source.State.Hand.Select(c => c.InstanceId), Is.EqualTo(src.State.Hand.Select(c => c.InstanceId)));
            Assert.That(Recorder.Replay(Life.LifeSetup(start).Expedition, log).Count, Is.EqualTo(log.Count), "replay keeps every call");
        }

        // js: test/app/life.test.mjs:157
        [Test]
        public void ASurvivor_PassesTheBonus_FourToolSlotsAndDeckCap45_ForTheNextLife()
        {
            Lineage lineage = Inheritance.NewLineage(3);
            LifeStart ls = Inheritance.StartLife(lineage);
            lineage = ls.Lineage;
            Expedition raw = Life.LifeSetup(Life.MakeStart(ls, Departure(ls))).Expedition;
            ExplorationState end = Walk(Recorder.RecordExpedition(raw), (open, st) => open[open.Count - 1], new WalkOptions { SurviveAt = 1 });
            if (end.Phase != RunPhase.Survived) Assert.Inconclusive("died on layer 1 before the interlude (the JS returns here too)");
            lineage = Inheritance.RecordEnd(lineage, null, Inheritance.PlaceFromState(end), raw.Mastery, Names.LifeJournal(raw), Names.EndOptions(raw, end));
            LifeStart next = Inheritance.StartLife(lineage);
            Assert.That(next.ToolSlots, Is.EqualTo(4));
            Assert.That(next.DeckMax, Is.EqualTo(45));

            // ... and the heir's life holds them: the start, the Expedition's deck cap, the loadout's slots.
            LifeStartData heir = Clone(Life.MakeStart(next, Departure(next, 2)));
            Assert.That(heir.ToolSlots, Is.EqualTo(4));
            Assert.That(heir.DeckMax, Is.EqualTo(45));
            Assert.That(heir.SurvivorBonus, Is.True);
            Expedition heirEx = Life.LifeSetup(heir).Expedition;
            Assert.That(heirEx.DeckMax, Is.EqualTo(45));
            Assert.That(heirEx.Session.State.Loadout.ToolSlotCount, Is.EqualTo(4));
        }

        // ================================================================== the start as data (life.js)

        [Test]
        public void ChoiceToData_KeepsTheDeckByInstance_TheToolsAndTheLoadoutAsReliefRows()
        {
            // js: app/life.js:30
            LifeStart ls = Inheritance.StartLife(Inheritance.NewLineage(5));
            DepartureChoice choice = Departure(ls, 1);
            choice.Talent = "thrust";
            ChoiceData data = Life.ChoiceToData(choice, ls.ToolSlots);
            Assert.That(data.Deck.Select(r => r.InstanceId), Is.EqualTo(choice.Deck.Select(c => c.InstanceId)), "instance ids and order kept exactly");
            Assert.That(data.Deck.Select(r => r.DefId), Is.EqualTo(choice.Deck.Select(c => c.Def.Id)));
            Assert.That(data.BattleToolIds, Is.EqualTo(new[] { "ikitsugi_no_fue" }));
            Assert.That(data.Talent, Is.EqualTo("thrust"));
            Assert.That(data.ToolIds, Is.Empty);
            Assert.That(Json.Stringify(data.Loadout), Is.EqualTo(Json.Stringify(Loadouts.RunLoadoutToData(Loadouts.ToRunLoadout(choice.Loadout, ls.ToolSlots)))));
            RunLoadout back = Loadouts.RunLoadoutFromData(Json.Parse(Json.Stringify(data.Loadout)));
            Assert.That(back.Trial, Is.True, "the trial flag rides along");
            Assert.That(back.Tools.Select(t => t.Id), Is.EqualTo(new[] { "bosho_no_men", "kokugen_no_suna", "ikitsugi_no_fue" }));

            // no loadout: the empty RunLoadout; a RunLoadout: kept as it is
            ChoiceData none = Life.ChoiceToData(new DepartureChoice { Deck = PrototypeDeck.Build() });
            Assert.That(Json.Stringify(none.Loadout), Is.EqualTo(Json.Stringify(Loadouts.RunLoadoutToData(RunLoadout.Empty))));
            RunLoadout kept = new RunLoadout(null, null, 4, true);
            Assert.That(Json.Stringify(Life.ChoiceToData(new DepartureChoice { RunLoadout = kept, Loadout = choice.Loadout }).Loadout),
                Is.EqualTo(Json.Stringify(Loadouts.RunLoadoutToData(kept))));
        }

        [Test]
        public void MakeStart_WritesTheLifesStart_AndItReadsBackTheSame()
        {
            // js: app/life.js:55
            Lineage lineage = Inheritance.NewLineage(9);
            LifeStart ls = Inheritance.StartLife(lineage);
            LifeStartData start = Life.MakeStart(ls, Departure(ls));
            Assert.That(start.V, Is.EqualTo(1));
            Assert.That(start.Generation, Is.EqualTo(ls.Life.Generation));
            Assert.That(start.Name, Is.EqualTo(ls.Life.Name));
            Assert.That(start.Seed, Is.EqualTo(ls.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Assert.That(start.SeedValue, Is.EqualTo(ls.Seed));
            Assert.That(start.ToolSlots, Is.EqualTo(3));
            Assert.That(start.DeckMax, Is.EqualTo(40));
            Assert.That(start.SurvivorBonus, Is.False);
            Assert.That(start.TraceLayers, Is.EqualTo(ls.TraceLayers));
            Assert.That(start.CarvingKnown, Is.False);
            Assert.That(Json.Stringify(start.Journal), Is.EqualTo("{\"Disclosure\":{},\"Pages\":[],\"Memo\":\"\",\"Scribbles\":[]}"));
            string text = Json.Stringify(start.ToJson());
            Assert.That(text, Does.StartWith("{\"v\":1,\"generation\":1,\"name\":"));
            Assert.That(Json.Stringify(Clone(start).ToJson()), Is.EqualTo(text), "a start reads back as it was written");

            // A life without the bonus builds the C#'s Expedition with the trial rules on.
            LifeSetupResult setup = Life.LifeSetup(Clone(start));
            Assert.That(setup.Expedition.Trial, Is.True);
            Assert.That(setup.Expedition.DeckMax, Is.EqualTo(40));
            Assert.That(setup.Expedition.Session.RunSeed, Is.EqualTo(ls.Seed));
            Assert.That(setup.Expedition.Session.Intel, Is.SameAs(setup.Intel));
            Assert.That(setup.Intel.Expedition, Is.SameAs(setup.Expedition));
            Assert.That(setup.Life.Expedition, Is.SameAs(setup.Expedition));
            Assert.That(setup.Life.TraceTaken, Is.False);
        }

        [Test]
        public void LifeSetup_RefusesAStartThatDoesNotBuild()
        {
            LifeStart ls = Inheritance.StartLife(Inheritance.NewLineage(9));
            LifeStartData start = Clone(Life.MakeStart(ls, Departure(ls)));
            start.Choice.Loadout = new JsonObject { ["tools"] = "no" };
            Assert.Throws<SaveFormatException>(() => Life.LifeSetup(start));
            LifeStartData badJournal = Clone(Life.MakeStart(ls, Departure(ls)));
            badJournal.Journal = Json.Parse("{\"Disclosure\":{\"polearm_warped\":1.5}}");
            Assert.Throws<SaveFormatException>(() => Life.LifeSetup(badJournal));
            LifeStartData badCard = Clone(Life.MakeStart(ls, Departure(ls)));
            badCard.Choice.Deck[0] = new DeckRow("x-1", "no_such_card");
            Assert.Throws<KeyNotFoundException>(() => Life.LifeSetup(badCard));
        }

        // The map hook (life.js:182): the lineage's trace node on its layers until this life took its trace, the carving
        // dropped once known.
        [Test]
        public void TheMapHook_DrawsTheTraceNodeUntilTheLifeTookItsTrace_AndDropsAKnownCarving()
        {
            LifeStart ls = Inheritance.StartLife(Inheritance.NewLineage(9));
            LifeStartData start = Clone(Life.MakeStart(ls, Plain(ls)));
            start.TraceLayers = new List<int> { 1, 2 };
            start.CarvingKnown = true;
            LifeSetupResult setup = Life.LifeSetup(start);
            ExplorationSession s = setup.Expedition.Session;
            Assert.That(s.State.Map.CountOf(NodeKind.Trace), Is.EqualTo(1), "layer 1 holds the trace node");
            LayerProfile two = SevenLayers.Of(2);
            Assert.That(s.MapOf(two, 1).CountOf(NodeKind.Trace), Is.EqualTo(1));
            Assert.That(s.MapOf(two, 1).CountOf(NodeKind.Carving), Is.EqualTo(0), "the known carving is gone");
            Assert.That(s.CarvingTaken, Is.True, "the session reads the carving as known");

            setup.Expedition.Inherit(new TakenLegacy("trace-1", Array.Empty<TakenCard>(), new LifeJournalData(), 0));
            Assert.That(setup.Life.TraceTaken, Is.True);
            Assert.That(s.MapOf(two, 1).CountOf(NodeKind.Trace), Is.EqualTo(0), "no trace node on the later layers of the life");

            LifeStartData plain = Clone(Life.MakeStart(ls, Plain(ls)));
            ExplorationSession p = Life.LifeSetup(plain).Expedition.Session;
            Assert.That(p.State.Map.CountOf(NodeKind.Trace), Is.EqualTo(0));
            Assert.That(p.MapOf(two, 1).CountOf(NodeKind.Carving), Is.EqualTo(two.MapSpec.Quotas.TryGetValue(NodeKind.Carving, out int q) ? q : 0));
        }

        // ================================================================== survivor.test.mjs

        private const string Memo = "八代目のメモ。生きて戻る。";
        private static readonly Scribble Old = new Scribble(1, "ミナト", "一代目の走り書き");

        private static string Show(IEnumerable<Scribble> list) =>
            string.Join(" | ", list.Select(s => s.Generation + "/" + s.Name + "/" + s.Text));

        /// <summary>gen 1 dies (no memo), gen 2 survives on layer 4 with a memo, 4 rare pages, 2 enemies known and an inherited 走り書き.</summary>
        private static (Lineage Lineage, LifeRecord Survivor) SurvivorLineage()
        {
            Lineage l = Inheritance.NewLineage(21);
            LifeStart s = Inheritance.StartLife(l, 1);
            l = Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(1, 3, null, RunPhase.Fallen), new List<CardEntry>(), LifeJournalData.WithPageCount(0));
            s = Inheritance.StartLife(l, 2);
            LifeJournalData journal = LifeJournalData.WithPageCount(4,
                new[] { new KeyValuePair<string, int>("shadow_hound", 2), new KeyValuePair<string, int>("rust_spear", 1) }, Memo, new[] { Old });
            l = Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(4, 2, null, RunPhase.Survived), new List<CardEntry>(), journal);
            return (l, s.Life);
        }

        // js: test/app/survivor.test.mjs:31
        [Test]
        public void HeirJournal_TheMemoBecomesTheWritersScribble_TheRestPassesAsItIs()
        {
            LifeJournalData j = Inheritance.HeirJournal(LifeJournalData.WithPageCount(2, new[] { new KeyValuePair<string, int>("a", 1) }, Memo, new[] { Old }), new HeirRef(8, "ハル"));
            Assert.That(j.Memo, Is.EqualTo(""), "the heir starts on a sheet of their own");
            Assert.That(Show(j.Scribbles), Is.EqualTo(Show(new[] { Old, new Scribble(8, "ハル", Memo) })));
            Assert.That(j.Disclosure.Select(p => (p.Key, p.Value)), Is.EqualTo(new[] { ("a", 1) }));
            Assert.That(j.Pages.Count, Is.EqualTo(2));
            // nothing to move: as it is
            Assert.That(Inheritance.HeirJournal(LifeJournalData.WithPageCount(0, null, "  "), new HeirRef(2, "x")).Scribbles, Is.Empty);
            Assert.That(Inheritance.HeirJournal(LifeJournalData.WithPageCount(0, null, "m"), null).Memo, Is.EqualTo("m"), "no writer to name: unchanged");
            // the same memo twice is one 走り書き
            Assert.That(Inheritance.HeirJournal(new LifeJournalData(j.Disclosure, j.Pages, Memo, j.Scribbles), new HeirRef(8, "ハル")).Scribbles.Count, Is.EqualTo(2));
        }

        // js: test/app/survivor.test.mjs:45
        [Test]
        public void ASurvivorsHeir_SetsOutWithTheWholeJournal_DisclosurePagesScribblesAndTheMemo()
        {
            (Lineage lineage, LifeRecord survivor) = SurvivorLineage();
            LifeStart ls = Inheritance.StartLife(lineage, 3);
            Assert.That((ls.JournalFrom.Generation, ls.JournalFrom.Name), Is.EqualTo((survivor.Generation, survivor.Name)));
            Assert.That(ls.Journal.Memo, Is.EqualTo(Memo), "StartLife still hands the survivor's journal as it was (R-13)");
            LifeStartData start = Clone(Life.MakeStart(ls, Plain(ls)));
            var journal = (JsonObject)start.Journal;
            Assert.That(journal["Memo"], Is.EqualTo(""), "the heir's own memo starts empty");
            Assert.That(Show(Life.ScribblesFromJson(journal["Scribbles"])), Is.EqualTo(Show(new[] { Old, new Scribble(2, survivor.Name, Memo) })));
            Assert.That(Life.StartPages(start), Is.EqualTo(4));

            Expedition raw = Life.LifeSetup(start).Expedition;
            Assert.That(raw.Pages, Is.EqualTo(4), "the rare pages are the life's from the start (the map's chip, the end journal)");
            Assert.That(raw.DisclosureOf("shadow_hound"), Is.EqualTo(2));
            Assert.That(raw.DisclosureOf("rust_spear"), Is.EqualTo(1));
            // what the router keeps for the メモ tab: the 走り書き handed down, the memo of the survivor among them
            List<Scribble> scribbles = Life.LifeScribbles(start, (object)null, ls.Lineage);
            Assert.That(Show(scribbles), Is.EqualTo(Show(Life.ScribblesFromJson(journal["Scribbles"]))));

            // the heir dies at once: the journal left at the death holds every page and every 走り書き
            LifeJournalData left = Names.LifeJournal(raw, "九代目のメモ", scribbles);
            Assert.That(left.Pages.Count, Is.EqualTo(4));
            Assert.That(Show(left.Scribbles), Is.EqualTo(Show(scribbles)));
            Lineage dead = Inheritance.RecordDeath(ls.Lineage, null, new LifePlace(1, 0, null, RunPhase.Fallen), raw.Mastery, left);
            Trace trace = dead.Traces[dead.Traces.Count - 1];
            List<Candidate> Kinds(CandidateKind k) => trace.Candidates.Where(c => c.Kind == k).ToList();
            Assert.That(Kinds(CandidateKind.DungeonPage).Count, Is.EqualTo(0), "the 4 rare pages hold nothing yet (#107): no pick is spent on them");
            Assert.That(Kinds(CandidateKind.EnemyPage).Count, Is.EqualTo(2));
            Assert.That(Kinds(CandidateKind.Scribble).Select(c => c.Name), Is.EqualTo(new[]
            {
                InheritanceText.ScribbleName(1, Old.Name),
                InheritanceText.ScribbleName(2, survivor.Name),
                InheritanceText.ScribbleName(ls.Life.Generation, ls.Life.Name),
            }), "the survivor's memo stays theirs; the heir's own memo is the heir's");
        }

        // js: test/app/survivor.test.mjs:80
        [Test]
        public void TheStartPages_StayThroughAReplay_AndAddToThePagesATraceGives()
        {
            (Lineage lineage, _) = SurvivorLineage();
            LifeStart ls = Inheritance.StartLife(lineage, 3);
            LifeStartData start = Clone(Life.MakeStart(ls, Plain(ls)));
            Expedition raw = Life.LifeSetup(start).Expedition;
            RecordedExpedition rec = Recorder.RecordExpedition(raw);
            rec.Inherit(TakenLegacy.FromJson(Json.Parse("{\"TraceId\":\"trace-x\",\"Cards\":[],\"Journal\":{\"Disclosure\":{},\"Pages\":[{\"Id\":\"page-1\",\"Title\":\"p\"}],\"Scribbles\":[]}}")));
            Assert.That(raw.Pages, Is.EqualTo(5));
            Expedition again = Life.LifeSetup(Clone(start)).Expedition;
            Recorder.ReplayLife(again, Saved(rec.Log));
            Assert.That(again.Pages, Is.EqualTo(5), "the replay counts the same pages");
        }

        // js: test/app/survivor.test.mjs:93
        [Test]
        public void AStartSavedBeforeTheMemoMoved_StillGivesTheSurvivorsMemoBack_AsAScribble()
        {
            (Lineage lineage, LifeRecord survivor) = SurvivorLineage();
            LifeStart ls = Inheritance.StartLife(lineage, 3);
            // the older MakeStart: the survivor's journal as it was, memo included
            LifeStartData old = Clone(Life.MakeStart(ls, Plain(ls)));
            old.Journal = Json.Parse(Json.Stringify(ls.Journal.ToJson()));
            old = Clone(old);
            List<Scribble> got = Life.LifeScribbles(old, Array.Empty<Scribble>(), ls.Lineage);
            Assert.That(Show(got), Is.EqualTo(Show(new[] { Old, new Scribble(2, survivor.Name, Memo) })));
            // united with what the life holds already (a trace's 走り書き), each once
            var taken = new Scribble(1, "ミナト", "別の走り書き");
            Assert.That(Show(Life.LifeScribbles(old, new[] { Old, taken }, ls.Lineage)), Is.EqualTo(Show(new[] { Old, new Scribble(2, survivor.Name, Memo), taken })));
            Assert.That(Life.StartPages(old), Is.EqualTo(4), "its pages are counted too");
            // the save's own list, as stored (JSON)
            Assert.That(Show(Life.LifeScribbles(old, Life.ScribblesToJson(new[] { Old, taken }), ls.Lineage)), Is.EqualTo(Show(new[] { Old, new Scribble(2, survivor.Name, Memo), taken })));
        }

        // js: app/life.js:74 / :87 (the edges the JS reads leniently)
        [Test]
        public void StartPages_AndScribbles_ReadAStartLeniently()
        {
            var start = new LifeStartData();
            Assert.That(Life.StartPages(start), Is.EqualTo(0));
            start.Journal = Json.Parse("{\"Pages\":3}");
            Assert.That(Life.StartPages(start), Is.EqualTo(3));
            start.Journal = Json.Parse("{\"Pages\":-2}");
            Assert.That(Life.StartPages(start), Is.EqualTo(0));
            start.Journal = Json.Parse("{\"Pages\":2.5}");
            Assert.That(Life.StartPages(start), Is.EqualTo(0));
            start.Journal = Json.Parse("{\"Pages\":[\"a\",\"b\"]}");
            Assert.That(Life.StartPages(start), Is.EqualTo(2));
            Assert.That(Life.StartPages(null), Is.EqualTo(0));

            // a 走り書き without its text is no 走り書き; a memo on the first life names no one
            start.Generation = 1;
            start.Journal = Json.Parse("{\"Memo\":\"m\",\"Scribbles\":[{\"Generation\":1,\"Name\":\"a\"},null,{\"Generation\":2,\"Name\":\"b\",\"Text\":\"t\"}]}");
            Assert.That(Show(Life.LifeScribbles(start, (object)null)), Is.EqualTo("2/b/t"));
            start.Generation = 3;
            Assert.That(Show(Life.LifeScribbles(start, (object)null)), Is.EqualTo("2/b/t | 2//m"), "the previous heir, unnamed without the lineage");
            start.Journal = Json.Parse("{\"Memo\":\" \\n\"}");
            Assert.That(Life.LifeScribbles(start, (object)null), Is.Empty, "a blank memo is none");
        }

        // ================================================================== inherit-gains.test.mjs (the life parts)

        private static readonly EndOptions Opts = Names.EndOptions(null, null);
        private const string FirstMemo = "一代目のメモ";

        /// <summary>
        /// gen 1 dies on layer 3 with feint and thrust at 3 段, kesa_cut at 2, brace at 1, three enemies known, 2 rare pages
        /// and a memo; gen 2 survives with 4 pages and two enemies known; gen 3 is the heir (js: inherit-gains.test.mjs:32).
        /// </summary>
        private static Lineage GainsLineage()
        {
            Lineage l = Inheritance.NewLineage(77);
            LifeStart s = Inheritance.StartLife(l, 1);
            l = Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(3, 4, NodeKind.Battle, RunPhase.Fallen),
                new List<CardEntry> { new CardEntry("thrust", 3), new CardEntry("feint", 3), new CardEntry("kesa_cut", 2), new CardEntry("brace", 1) },
                LifeJournalData.WithPageCount(2, new[]
                {
                    new KeyValuePair<string, int>("polearm_warped", 2), new KeyValuePair<string, int>("shadow_hound", 1), new KeyValuePair<string, int>("rusted_revenant", 1),
                }, FirstMemo), Opts);
            s = Inheritance.StartLife(l, 2);
            l = Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(2, 3, NodeKind.Rest, RunPhase.Survived), new List<CardEntry>(),
                LifeJournalData.WithPageCount(4, new[] { new KeyValuePair<string, int>("polearm_warped", 1), new KeyValuePair<string, int>("rusted_revenant", 2) }, ""), Opts);
            return l;
        }

        private sealed class Heir
        {
            public LifeStart Ls;
            public LifeStartData Start;
            public Expedition Raw;
            public RecordedExpedition Rec;
            public TraceNodeView View;
        }

        /// <summary>The heir's life (gen 3), recorded, and the trace node's view on layer 3 (js: inherit-gains.test.mjs:45).</summary>
        private static Heir MakeHeir()
        {
            LifeStart ls = Inheritance.StartLife(GainsLineage(), 3);
            LifeStartData start = Clone(Life.MakeStart(ls, Plain(ls)));
            Expedition raw = Life.LifeSetup(start).Expedition;
            return new Heir { Ls = ls, Start = start, Raw = raw, Rec = Recorder.RecordExpedition(raw), View = Inheritance.TraceNodeView(ls.Lineage, 3) };
        }

        // js: test/app/inherit-gains.test.mjs:55
        [Test]
        public void R27_ThePagesASurvivorHandedDown_AreTheLifes_Inherited_NotFound_AndTheReplayHoldsTheSplit()
        {
            Heir h = MakeHeir();
            Assert.That(h.Raw.Pages, Is.EqualTo(4));
            Assert.That(h.Raw.InheritedPages, Is.EqualTo(4));
            Assert.That(h.Raw.FoundPages, Is.EqualTo(0), "nothing found yet: 拾った物 lists no page");
            // a trace's page is inherited too
            h.Rec.Inherit(new TakenLegacy("trace-1", Array.Empty<TakenCard>(), new LifeJournalData(null, new[] { new InheritanceCore.JournalPage("page-1", "p") }), 0));
            Assert.That(h.Raw.Pages, Is.EqualTo(5));
            Assert.That(h.Raw.InheritedPages, Is.EqualTo(5));
            Assert.That(h.Raw.FoundPages, Is.EqualTo(0));
            // the replay holds the same split
            Expedition again = Life.LifeSetup(Clone(h.Start)).Expedition;
            Recorder.ReplayLife(again, Saved(h.Rec.Log));
            Assert.That(again.InheritedPages, Is.EqualTo(5));
            Assert.That(again.FoundPages, Is.EqualTo(0));
            // the life's journal at its end still holds them all (a trace or a survivor passes every page on)
            Assert.That(Names.LifeJournal(again, "", Array.Empty<Scribble>()).Pages.Count, Is.EqualTo(5));
        }

        // js: test/app/inherit-gains.test.mjs:122 (the take is a logged call: a replay makes the same life)
        [Test]
        public void X11_TheTake_IsALoggedCall_AReplayMakesTheSameLife()
        {
            Heir h = MakeHeir();
            // the heir already holds a 3 段 card (mastered in this life): an inherited 3 段 stops at 2
            MasteryBook Rend(MasteryBook book) => book with
            {
                Records = new Dictionary<string, CardMastery>(StringComparer.Ordinal) { ["rend"] = new CardMastery("rend", 15, 3) },
            };
            SetMastery(h.Raw, Rend(h.Raw.Mastery));
            var picks = new[] { "card:feint", "enemy:rusted_revenant", "enemy:polearm_warped" };
            TakeResult r = Inheritance.TakeTrace(h.Ls.Lineage, h.View.TraceId, picks, 3);
            InheritResult result = h.Rec.Inherit(TakenLegacy.FromJson(Json.Parse(Json.Stringify(r.Taken.ToJson()))));
            Assert.That(result.Cards.Select(c => (c.CardId, c.Rank, c.From, c.To, c.Joins)), Is.EqualTo(new[] { ("feint", 3, 0, 2, true) }));
            Assert.That(result.Enemies.Select(e => (e.EnemyId, e.Disclosure, e.From, e.To)),
                Is.EqualTo(new[] { ("polearm_warped", 2, 1, 2), ("rusted_revenant", 1, 2, 2) }));
            Assert.That(h.Raw.Mastery.RankOf("feint"), Is.EqualTo(2));
            Assert.That(h.Raw.DisclosureOf("rusted_revenant"), Is.EqualTo(2), "the journal keeps the higher 開示度");
            LogEntry logged = h.Rec.Log.Last;
            Assert.That(logged.ToString(), Is.EqualTo("E.Inherit"));
            Assert.That(Json.Stringify(logged.Args[0]), Is.EqualTo(Json.Stringify(r.Taken.ToJson())), "logged as the take's JSON");

            Expedition again = Life.LifeSetup(Clone(MakeHeir().Start)).Expedition;
            SetMastery(again, Rend(again.Mastery));
            Recorder.ReplayLife(again, Saved(h.Rec.Log));
            Assert.That(again.Mastery.RankOf("feint"), Is.EqualTo(2));
            Assert.That(again.DisclosureOf("rusted_revenant"), Is.EqualTo(2));
            Assert.That(again.InheritedTraceId, Is.EqualTo(h.View.TraceId));
            Assert.That(again.InheritedLayer, Is.EqualTo(1), "the layer the heir stood on (the test takes it from layer 1)");
            Assert.That(Snap(again), Is.EqualTo(Snap(h.Raw)));
        }

        // js: test/app/inherit-gains.test.mjs:155 (addScribbles)
        [Test]
        public void X11_AScribbleTheLifeAlreadyCarries_AddsNothing_AndTheRouterKeepsEachOnce()
        {
            Heir h = MakeHeir();
            CandidateView memo = h.View.Candidates.First(c => c.Id == "memo");
            var held = new List<Scribble> { new Scribble(1, memo.From, FirstMemo) };
            AddedScribbles added = Life.AddScribbles(held, new[] { new Scribble(1, memo.From, FirstMemo), new Scribble(2, "x", "new") });
            Assert.That(added.List.Select(s => s.Text), Is.EqualTo(new[] { FirstMemo, "new" }));
            Assert.That(added.Rows.Select(s => s.Added), Is.EqualTo(new[] { false, true }));
            Assert.That(held.Count, Is.EqualTo(1), "the held list is left as it was");
            Assert.That(Life.AddScribbles(null, new[] { null, new Scribble(1, "a", null) }).Rows, Is.Empty, "no text: no 走り書き");
        }

        // ================================================================== names.js

        [Test]
        public void Names_CardAtItsRank_EnemyName_AndTheClearedLifesRootCards()
        {
            // js: app/names.js:23 / :30 / :39
            MasteryProfile thrust = MasteryProfiles.Find("thrust");
            Assert.That(Names.CardName("thrust"), Is.EqualTo(CardCatalog.ById("thrust").Name));
            Assert.That(Names.CardName("thrust", 2), Is.EqualTo(thrust?.NameAt(2) ?? CardCatalog.ById("thrust").Name));
            Assert.That(Names.CardName("no_such_card", 1), Is.EqualTo("no_such_card"));
            Assert.That(Names.CardName(null), Is.Null);
            Assert.That(Names.EnemyName("abyss_angler"), Is.EqualTo("獄竜 ガルド"));
            Assert.That(Names.EnemyName("rust_spear"), Is.EqualTo("rust_spear"));
            Assert.That(Names.EnemyName(null), Is.Null);

            var ex = new Expedition(new RunStart { Seed = 3 });
            EndOptions dead = Names.EndOptions(ex, ex.Session.State);
            Assert.That(dead.RootCards, Is.Null);
            Assert.That(dead.CardName("thrust", 0), Is.EqualTo(Names.CardName("thrust")));
            Assert.That(dead.EnemyName("shadow_hound"), Is.EqualTo("瘴牙の走竜"));
            ExplorationState done = ex.Session.State.With(phase: RunPhase.Completed);
            EndOptions cleared = Names.EndOptions(ex, done);
            Assert.That(cleared.RootCards.Select(c => c.CardId), Is.EqualTo(UniqueCards.OfBoss("distortion_root")));
            Assert.That(cleared.RootCards.All(c => c.Rank == 0), Is.True);
        }

        [Test]
        public void LifeJournal_HoldsTheKnownEnemiesInOrder_ThePagesAsACount_TheMemo_TheScribbles_TheCarving()
        {
            // js: app/names.js:10
            Expedition ex = new Expedition(new RunStart { Seed = 3 }, null, null, null, null,
                new Dictionary<string, int> { ["shadow_hound"] = 2, ["polearm_warped"] = 0, ["abyss_angler"] = 1 });
            LifeJournalData j = Names.LifeJournal(ex, "m", new[] { Old });
            Assert.That(j.Disclosure.Select(p => p.Key), Is.EqualTo(new[] { "shadow_hound", "abyss_angler" }), "levels above 0, in the life's order");
            Assert.That(j.Pages.Count, Is.EqualTo(0));
            Assert.That(j.Memo, Is.EqualTo("m"));
            Assert.That(Show(j.Scribbles), Is.EqualTo(Show(new[] { Old })));
            Assert.That(j.CarvingTaken, Is.False);
            var live = new FakeLive { Memo = "x", ScribblesJson = Life.ScribblesToJson(new[] { Old }) };
            LifeJournalData fromLive = Names.LifeJournal(ex, live);
            Assert.That(fromLive.Memo, Is.EqualTo("x"));
            Assert.That(Show(fromLive.Scribbles), Is.EqualTo(Show(new[] { Old })));
            Assert.That(Names.LifeJournal(ex).Memo, Is.EqualTo(""));
        }
    }
}
