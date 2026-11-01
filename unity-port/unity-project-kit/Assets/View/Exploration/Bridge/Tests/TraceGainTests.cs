// What a trace and a survivor's journal give *this* heir (the browser trial's docs/EXTENSIONS.md X-3 / X-11,
// docs/INHERITANCE.md R-11 / R-13 / R-27): Expedition.Inherit, TraceGain, the 受け取った遺産 card, and the save data of
// a loadout. Ports test/ext/explore-results.test.mjs:484-565 and the Expedition half of test/app/inherit-gains.test.mjs,
// and holds TraceGain to the JS over a grid of heirs, rows, picks and modes (TraceGain.parity.mjs wrote
// TraceGain.parity.txt from the browser trial; rerun it when the grid changes). The heir's life is built the way
// app/life.js lifeSetup builds it (Trial.Bridge's Life), written out here.
// Runs under `dotnet test` (Exploration.Bridge.Tests). Unity skips this file until Exploration.Bridge.Tests.asmdef
// references InheritanceCore (these tests name its types; the asmdef lists only what it had before the trial port):
// add "InheritanceCore" to that asmdef's references and remove the #if / #endif around this file.
#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleCore;
using DungeonContent;
using DungeonCore;
using InheritanceCore;
using NUnit.Framework;
using static Exploration.Bridge.Tests.TrialTestKit;

namespace Exploration.Bridge.Tests
{
    public class TraceGainTests
    {
        private const string Memo = "一代目のメモ";

        /// <summary>A trace's card row as the scene's list holds it, with the 段 in its Detail only (js: explore-results.test.mjs:490).</summary>
        private static CandidateView CardRow(string id, int rank) =>
            new CandidateView("card:" + id, CandidateKind.Card, "技術", id, rank + " 段目");

        /// <summary>The book with these cards at these 段 (js: explore-results.test.mjs:491 withRanks).</summary>
        private static Expedition WithRanks(Expedition ex, params (string Id, int Rank)[] ranks)
        {
            int[] ticks = { 0, 3, 8, 15 };
            var records = new Dictionary<string, CardMastery>(StringComparer.Ordinal);
            foreach (var (id, rank) in ranks) records[id] = new CardMastery(id, ticks[rank], rank);
            ex.Mastery = ex.Mastery with { Records = records };
            return ex;
        }

        private static TakenLegacy Taken(string traceId, params (string Id, int Rank, int Ticks)[] cards) =>
            new TakenLegacy(traceId, cards.Select(c => new TakenCard(c.Id, c.Rank, c.Ticks, c.Id)), new InheritanceCore.Journal(), 0);

        /// <summary>The take as the save's log carries it: through its JSON and back (js: the recorder's <c>clone(r.Taken)</c>).</summary>
        private static TakenLegacy Logged(TakenLegacy taken) => TakenLegacy.FromJson(Json.Parse(Json.Stringify(taken.ToJson())));

        // ---------------------------------------------------------------- X-11 (explore-results.test.mjs)

        [Test]
        public void X11_TheInheritPreview_IsWhatTheTakeDoes()
        {
            // js: test/ext/explore-results.test.mjs:496 — a 3 段 already held elsewhere caps the inherited 3 段 at 2; a card
            // held at its 段 adds none; the deck has room
            var cards = new[] { new TraceCard("feint", 3), new TraceCard("brace", 2), new TraceCard("kesa_cut", 1) };
            Expedition ex = WithRanks(Trial(3), ("thrust", 3), ("brace", 2));
            IReadOnlyList<InheritedCard> preview = TraceGain.InheritPreview(ex, cards);
            Assert.That(preview.Select(r => (r.CardId, r.From, r.To, r.Joins)),
                Is.EqualTo(new[] { ("feint", 0, 2, true), ("brace", 2, 2, true), ("kesa_cut", 0, 1, true) }));
            int deck = ex.Deck.Count;
            ex.Inherit(Taken("trace-1", ("feint", 3, 0), ("brace", 2, 0), ("kesa_cut", 1, 0)));
            Assert.That(ex.Deck.Count, Is.EqualTo(deck + 3));
            foreach (InheritedCard r in preview) Assert.That(ex.Mastery.RankOf(r.CardId), Is.EqualTo(r.To), r.CardId);
        }

        [Test]
        public void X11_AFullDeck_TakesNoCopy_AndTheCardsSaySo()
        {
            // js: test/ext/explore-results.test.mjs:508
            Expedition ex = WithRanks(Trial(3, limits: new DeckLimits(deckMax: 20)), ("boar_rush", 2));
            Assert.That(TraceGain.DeckIsFull(ex), Is.True);
            var rows = new[]
            {
                CardRow("boar_rush", 2),
                CardRow("feint", 1),
                new CandidateView("page:page-1", CandidateKind.DungeonPage, "ダンジョンの頁", "希少な頁 1", "手記の頁"),
            };
            TraceCard parsed = TraceGain.CandidateCard(rows[0]);
            Assert.That((parsed.CardId, parsed.Rank), Is.EqualTo(("boar_rush", 2)));
            Assert.That(TraceGain.CandidateCard(rows[2]), Is.Null);
            IReadOnlyDictionary<string, TraceGainRow> before = TraceGain.TraceGains(ex, rows, new string[0]);
            Assert.That(before["card:boar_rush"].Nothing, Is.True, "held at that 段 with no room: nothing at all");
            Assert.That(before["card:boar_rush"].Notes, Is.EqualTo(new[] { TraceGainText.Nothing(TraceGainText.Now(2), false) }));
            Assert.That(before["card:feint"].Notes, Is.EqualTo(new[] { TraceGainText.OwnedOnly(null, false) }), "the deck cap is the card lead's");
            Assert.That(before["page:page-1"].Notes, Is.Empty, "a dungeon page always adds one: nothing to say");
            Assert.That(before["page:page-1"].Nothing, Is.False);
            IReadOnlyDictionary<string, TraceGainRow> after = TraceGain.TraceGains(ex, rows, new[] { "card:feint", "card:boar_rush" }, done: true);
            Assert.That(after["card:feint"].Notes,
                Is.EqualTo(new[] { $"{TraceGainText.RankUp(0, 1)}・{TraceGainText.OwnedOnly(TraceGainText.DeckCap(20), true)}" }));
            ex.Inherit(Taken("trace-1", ("feint", 1, 3)));
            Assert.That(ex.Deck.Count, Is.EqualTo(20), "the take agrees: owned only");
            Assert.That(ex.Owned.Contains("feint"), Is.True);
        }

        [Test]
        public void X11_TheOneCardRuleOnThreeDan_IsNamed_BeforeAndAfter()
        {
            // js: test/ext/explore-results.test.mjs:527
            Expedition ex = WithRanks(Trial(3), ("thrust", 3), ("feint", 2));
            var rows = new[] { CardRow("feint", 3) };
            IReadOnlyDictionary<string, TraceGainRow> before = TraceGain.TraceGains(ex, rows, new string[0]);
            // the row is printed at this heir's 段 and name; a first line says what the trace held
            Assert.That(before["card:feint"].Name, Is.EqualTo(TraceGain.CardNameAt("feint", 2)));
            Assert.That(before["card:feint"].Detail, Is.EqualTo("2 段目"));
            Assert.That(before["card:feint"].Notes, Is.EqualTo(new[]
            {
                TraceGainText.InTrace("feint", 3),
                $"{TraceGainText.Now(2)}（{TraceGainText.Capped(2)}）・{TraceGainText.Joins}",
            }));
            IReadOnlyDictionary<string, TraceGainRow> after = TraceGain.TraceGains(ex, rows, new[] { "card:feint" }, done: true);
            Assert.That(after["card:feint"].Notes, Is.EqualTo(new[]
            {
                TraceGainText.InTrace("feint", 3),
                $"{TraceGainText.Already(2)}（{TraceGainText.Capped(2)}）・{TraceGainText.Joined}",
            }));
        }

        // ---------------------------------------------------------------- X-3: the received card survives a reload

        private static LegacyReceipt Receipt() => new LegacyReceipt("第 1 層", "第 1 代 踏破者 カリナの痕跡",
            new[] { new LegacyReceiptRow(CandidateKind.Card, "技術", "牽制", "1 段目", new[] { "習熟 0 → 1 段目・デッキに 1 枚入った" }, false) }, 4);

        [Test]
        public void X3_TheReceivedCard_WaitsUntilClosed_AndAReplayBringsItBack()
        {
            // js: test/ext/explore-results.test.mjs:541 (the replay: the same calls on a fresh expedition, the card
            // through its JSON as the save's log carries it)
            LegacyReceipt card = Receipt();
            Expedition ex = Trial(5);
            Assert.That(ex.ShowTaken(card), Is.False, "nothing was taken yet");
            ex.Inherit(Taken("trace-1", ("feint", 1, 3)));
            Assert.That(ex.ShowTaken(card), Is.True);
            Assert.That(ex.PendingTaken, Is.SameAs(card));

            Expedition again = Trial(5);
            again.Inherit(Logged(Taken("trace-1", ("feint", 1, 3))));
            again.ShowTaken(LegacyReceipt.FromJson(Json.Parse(Json.Stringify(card.ToJson()))));
            Assert.That(Json.Stringify(again.PendingTaken.ToJson()), Is.EqualTo(Json.Stringify(card.ToJson())));

            Assert.That(ex.CloseTaken(), Is.True);
            Assert.That(ex.PendingTaken, Is.Null);

            var plain = new Expedition(new RunStart { Seed = 5, Loadout = RunStart.TrialLoadout() });
            plain.Inherit(Taken("trace-1", ("feint", 1, 3)));
            Assert.That(plain.InheritedTraceId, Is.EqualTo("trace-1"));
            Assert.That(plain.ShowTaken(card), Is.False, "outside trial nothing waits");
            Assert.That(plain.PendingTaken, Is.Null);
        }

        [Test]
        public void X3_TheReceivedCard_IsDroppedOnceTheExplorationMovesOn()
        {
            // js: bridge/exploration/Expedition.js:435 (like PendingResult)
            Expedition ex = Trial(5);
            Win(ex);
            ex.CloseResult();
            ClearChoices(ex.Session);
            ex.Inherit(Taken("trace-1", ("feint", 1, 3)));
            Assert.That(ex.ShowTaken(Receipt()), Is.True);
            Assert.That(ex.PendingTaken, Is.Not.Null);
            Assert.That(ex.Session.Step(PathTo(ex.Session.State, _ => true)[0]), Is.True);
            Assert.That(ex.PendingTaken, Is.Null);
        }

        [Test]
        public void TheReceipt_WritesTheJsShape_AndReadsItBack()
        {
            // js: scenes/explore/ExploreScene.js:1934 takenCard (plain data the expedition keeps and the save logs)
            LegacyReceipt card = Receipt();
            string json = Json.Stringify(card.ToJson());
            Assert.That(json, Is.EqualTo(
                "{\"PlaceLine\":\"第 1 層\",\"HeirLine\":\"第 1 代 踏破者 カリナの痕跡\",\"Rows\":[{\"Kind\":\"Card\",\"KindLabel\":\"技術\"," +
                "\"Name\":\"牽制\",\"Detail\":\"1 段目\",\"Notes\":[\"習熟 0 → 1 段目・デッキに 1 枚入った\"],\"Nothing\":false}],\"Lost\":4}"));
            LegacyReceipt back = LegacyReceipt.FromJson(Json.Parse(json));
            Assert.That(Json.Stringify(back.ToJson()), Is.EqualTo(json));
            Assert.That(back.Rows[0].Kind, Is.EqualTo(CandidateKind.Card));
            Assert.Throws<ArgumentException>(() => LegacyReceipt.FromJson("{}"));
            Assert.Throws<ArgumentException>(() => LegacyReceipt.FromJson(Json.Parse("{\"Rows\":[{\"Kind\":\"Nope\"}]}")));
        }

        // ---------------------------------------------------------------- the heir of inherit-gains.test.mjs

        private static string CardName(string id, int rank)
        {
            // js: app/names.js cardName (Trial.Bridge's Names)
            string grown = MasteryProfiles.Find(MasteryProfiles.All, id)?.NameAt(rank);
            if (grown != null) return grown;
            return CardCatalog.All.Any(d => d.Id == id) ? CardCatalog.ById(id).Name : id;
        }

        private static EndOptions Opts() => new EndOptions
        {
            CardName = CardName,
            EnemyName = id => Enemies.All.Any(e => e.Id == id) ? Enemies.ById(id).Name : id,
        };

        private static Dictionary<string, int> Levels(params (string Id, int Level)[] levels) =>
            levels.ToDictionary(l => l.Id, l => l.Level, StringComparer.Ordinal);

        /// <summary>
        /// gen 1 dies on layer 3 with feint and thrust at 3 段, kesa_cut at 2, brace at 1, three enemies known, 2 rare
        /// pages and a memo; gen 2 survives with 4 pages and two enemies known; gen 3 is the heir: the survivor's journal
        /// and gen 1's trace on layer 3 (js: test/app/inherit-gains.test.mjs:32).
        /// </summary>
        private static Lineage MakeLineage()
        {
            Lineage l = Inheritance.NewLineage(77);
            LifeStart s = Inheritance.StartLife(l, 1);
            l = Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(3, 4, NodeKind.Battle, RunPhase.Fallen),
                new[] { new CardEntry("thrust", 3), new CardEntry("feint", 3), new CardEntry("kesa_cut", 2), new CardEntry("brace", 1) },
                InheritanceCore.Journal.WithPageCount(2, Levels(("polearm_warped", 2), ("shadow_hound", 1), ("rusted_revenant", 1)), Memo), Opts());
            s = Inheritance.StartLife(l, 2);
            return Inheritance.RecordSurvival(s.Lineage, s.Life, new LifePlace(2, 3, NodeKind.Rest, RunPhase.Survived), new CardEntry[0],
                InheritanceCore.Journal.WithPageCount(4, Levels(("polearm_warped", 1), ("rusted_revenant", 2)), ""), Opts());
        }

        /// <summary>The heir's life (gen 3) as app/life.js lifeSetup builds it, and the trace node's view on layer 3 (js: inherit-gains.test.mjs:45).</summary>
        private static (LifeStart Start, Expedition Raw, TraceNodeView View) Heir()
        {
            LifeStart ls = Inheritance.StartLife(MakeLineage(), 3);
            InheritanceCore.Journal journal = Inheritance.HeirJournal(ls.Journal, ls.JournalFrom);
            var limits = new DeckLimits(ls.DeckMin, ls.DeckMax, ls.ToolSlots, ls.ConsumableSlots);
            RunLoadout loadout = Loadouts.ToRunLoadout(Loadout.Of(new string[0], new string[0], ls.ToolSlots), ls.ToolSlots);
            var raw = new Expedition(new RunStart { Seed = ls.Seed, Loadout = loadout }, PrototypeDeck.Build(), new string[0], null, null,
                journal.Disclosure, new ExpeditionOptions { Trial = true, Limits = limits.IsDefault ? null : limits, StartPages = journal.Pages.Count });
            TraceNodeView view = Inheritance.TraceNodeView(ls.Lineage, 3);
            return (ls, raw, view);
        }

        [Test]
        public void R27_ThePagesASurvivorHandedDown_AreTheLifes_InheritedNotFound()
        {
            // js: test/app/inherit-gains.test.mjs:55
            var (_, raw, _) = Heir();
            Assert.That(raw.Pages, Is.EqualTo(4));
            Assert.That(raw.InheritedPages, Is.EqualTo(4));
            Assert.That(raw.FoundPages, Is.EqualTo(0), "nothing found yet: 拾った物 lists no page");
            Assert.That(raw.DeckMax, Is.EqualTo(45), "the survivor's bonus");
            // a trace's page is inherited too
            raw.Inherit(new TakenLegacy("trace-1", new TakenCard[0],
                new InheritanceCore.Journal(null, new[] { new InheritanceCore.JournalPage("page-1", "p") }, "", null), 0));
            Assert.That(raw.Pages, Is.EqualTo(5));
            Assert.That(raw.InheritedPages, Is.EqualTo(5));
            Assert.That(raw.FoundPages, Is.EqualTo(0));
            // a page found in this life counts as found (the C# adds them to the same count)
            raw.TakeChest(1, 0, new ChestContents(0, 0, 1));
            Assert.That(raw.Pages, Is.EqualTo(6));
            Assert.That(raw.FoundPages, Is.EqualTo(1));
        }

        [Test]
        public void X11_AnEnemyPage_GivesOnlyWhatGoesAboveTheJournal_TheRowReadsThisLifesLevel()
        {
            // js: test/app/inherit-gains.test.mjs:105 — the journal: polearm 1 (the survivor's), rusted 2, shadow_hound 0;
            // the trace: polearm 2, rusted 1, shadow 1
            var (_, raw, view) = Heir();
            IReadOnlyDictionary<string, TraceGainRow> g = TraceGain.TraceGains(raw, view.Candidates, new string[0]);
            TraceGainRow up = g["enemy:polearm_warped"];
            Assert.That(up.Detail, Is.EqualTo(InheritanceText.DisclosureDetail(2)));
            Assert.That(up.Notes, Is.EqualTo(new[] { TraceGainText.PageUp(1, 2) }));
            Assert.That(up.Nothing, Is.False);
            TraceGainRow none = g["enemy:rusted_revenant"];
            Assert.That(none.Nothing, Is.True, "the journal already reads 2: the page adds nothing");
            Assert.That(none.Detail, Is.EqualTo(InheritanceText.DisclosureDetail(2)), "the heir's 開示度, not the dead heir's 1");
            Assert.That(none.Notes, Is.EqualTo(new[] { TraceGainText.PageInTrace(1), TraceGainText.Nothing(TraceGainText.PageNow(2), false) }));
            TraceGainRow fresh = g["enemy:shadow_hound"];
            Assert.That(fresh.Detail, Is.EqualTo(InheritanceText.DisclosureDetail(1)));
            Assert.That(fresh.Notes, Is.Empty, "a new page: the row says it all");
        }

        [Test]
        public void X11_TheTakeReturnsWhatItDid_AndTheReceiptIsWrittenFromIt()
        {
            // js: test/app/inherit-gains.test.mjs:122 — the heir already holds a 3 段 card (mastered in this life): an
            // inherited 3 段 stops at 2
            var (ls, raw, view) = Heir();
            raw.Mastery = raw.Mastery with
            {
                Records = new Dictionary<string, CardMastery>(StringComparer.Ordinal) { ["rend"] = new CardMastery("rend", 15, 3) },
            };
            var picks = new[] { "card:feint", "enemy:rusted_revenant", "enemy:polearm_warped" };
            IReadOnlyDictionary<string, TraceGainRow> before = TraceGain.TraceGains(raw, view.Candidates, picks, done: true);
            TakeResult r = Inheritance.TakeTrace(ls.Lineage, view.TraceId, picks, 3);
            InheritResult result = raw.Inherit(Logged(r.Taken));
            Assert.That(result.Cards.Select(c => (c.CardId, c.Rank, c.From, c.To, c.Joins)), Is.EqualTo(new[] { ("feint", 3, 0, 2, true) }));
            Assert.That(result.Enemies.Select(e => (e.EnemyId, e.Disclosure, e.From, e.To)),
                Is.EqualTo(new[] { ("polearm_warped", 2, 1, 2), ("rusted_revenant", 1, 2, 2) }));
            Assert.That(raw.Mastery.RankOf("feint"), Is.EqualTo(2));
            Assert.That(raw.DisclosureOf("rusted_revenant"), Is.EqualTo(2), "the journal keeps the higher 開示度");
            Assert.That(raw.InheritedTraceId, Is.EqualTo(view.TraceId));
            Assert.That(raw.InheritedLayer, Is.EqualTo(raw.Session.State.Profile.Layer));
            IReadOnlyDictionary<string, TraceGainRow> after = TraceGain.TraceGains(raw, view.Candidates, picks, done: true,
                result: result.WithScribbles(new ScribbleRow[0]));
            TraceGainRow feint = after["card:feint"];
            Assert.That(feint.Name, Is.EqualTo(TraceGain.CardNameAt("feint", 2)), "the name at the 段 this heir holds");
            Assert.That(feint.Detail, Is.EqualTo("2 段目"));
            Assert.That(feint.Notes, Is.EqualTo(new[]
            {
                TraceGainText.InTrace(view.Candidates.First(c => c.Id == "card:feint").Name, 3),
                $"{TraceGainText.RankUp(0, 2)}（{TraceGainText.Capped(2)}）・{TraceGainText.Joined}",
            }));
            Assert.That(after["card:feint"].Notes, Is.EqualTo(before["card:feint"].Notes), "the preview before the take said the same");
            TraceGainRow rusted = after["enemy:rusted_revenant"];
            Assert.That(rusted.Nothing, Is.True);
            Assert.That(rusted.Notes, Is.EqualTo(new[] { TraceGainText.PageInTrace(1), TraceGainText.Nothing(TraceGainText.PageAlready(2), true) }));
            Assert.That(after["enemy:polearm_warped"].Notes, Is.EqualTo(new[] { TraceGainText.PageRaised(1, 2) }));
            Assert.That(after.ContainsKey("card:thrust"), Is.False, "the receipt lists the picks only");
        }

        [Test]
        public void X11_AScribbleTheLifeAlreadyCarries_AddsNothing()
        {
            // js: test/app/inherit-gains.test.mjs:155 (addScribbles is Trial.Bridge's Life: its rows are written out here)
            var (_, raw, view) = Heir();
            CandidateView memo = view.Candidates.First(c => c.Id == "memo");
            var held = new[] { new Scribble(1, memo.From, Memo) };
            Assert.That(TraceGain.TraceGains(raw, view.Candidates, new string[0], scribbles: new Scribble[0])["memo"].Nothing, Is.False);
            TraceGainRow g = TraceGain.TraceGains(raw, view.Candidates, new string[0], scribbles: held)["memo"];
            Assert.That(g.Nothing, Is.True);
            Assert.That(g.Notes, Is.EqualTo(new[] { TraceGainText.Nothing(TraceGainText.ScribbleHeld, false) }));
            var rows = new[] { new ScribbleRow(1, memo.From, Memo, false), new ScribbleRow(2, "x", "new", true) };
            TraceGainRow done = TraceGain.TraceGains(raw, view.Candidates, new[] { "memo" }, done: true,
                result: new InheritResult(null, null, 0, rows))["memo"];
            Assert.That(done.Notes, Is.EqualTo(new[] { TraceGainText.Nothing(TraceGainText.ScribbleHeld, true) }));
        }

        [Test]
        public void TraceGains_KeepTheJsMapsOrder_PicksFirst()
        {
            // js: bridge/exploration/TraceGain.js:135-163 (the picked cards, the other cards, then the rest in order)
            var (_, raw, view) = Heir();
            var picks = new[] { "card:kesa_cut", "enemy:shadow_hound" };
            IReadOnlyDictionary<string, TraceGainRow> g = TraceGain.TraceGains(raw, view.Candidates, picks);
            var cardIds = view.Candidates.Where(c => c.Kind == CandidateKind.Card).Select(c => c.Id).ToList();
            var expected = new[] { "card:kesa_cut" }
                .Concat(cardIds.Where(id => id != "card:kesa_cut"))
                .Concat(view.Candidates.Where(c => c.Kind != CandidateKind.Card).Select(c => c.Id));
            Assert.That(g.Keys, Is.EqualTo(expected));
            IReadOnlyDictionary<string, TraceGainRow> done = TraceGain.TraceGains(raw, view.Candidates, picks, done: true);
            Assert.That(done.Keys, Is.EqualTo(picks));
        }

        // ---------------------------------------------------------------- the rows (the words are TraceGainTextTests')

        [Test]
        public void CandidatePage_ReadsTheLevel_OrItsDetail()
        {
            // js: bridge/exploration/TraceGain.js:99
            var typed = new CandidateView("enemy:shadow_hound", CandidateKind.EnemyPage, "敵の頁", "瘴牙の走竜", "開示度 1 / 2", enemyId: "shadow_hound", disclosure: 1);
            var bare = new CandidateView("enemy:shadow_hound", CandidateKind.EnemyPage, "敵の頁", "瘴牙の走竜", "開示度 2 / 2");
            Assert.That((TraceGain.CandidatePage(typed).EnemyId, TraceGain.CandidatePage(typed).Disclosure), Is.EqualTo(("shadow_hound", 1)));
            Assert.That((TraceGain.CandidatePage(bare).EnemyId, TraceGain.CandidatePage(bare).Disclosure), Is.EqualTo(("shadow_hound", 2)));
            Assert.That(TraceGain.CandidatePage(new CandidateView("enemy:x", CandidateKind.EnemyPage, "敵の頁", "x", "?")), Is.Null);
            Assert.That(TraceGain.CandidatePage(CardRow("feint", 1)), Is.Null);
            Assert.That(TraceGain.CandidateCard(new CandidateView("card:feint", CandidateKind.Card, "技術", "牽制", "4 段目")), Is.Null);
        }

        // ---------------------------------------------------------------- the loadout as save data (world.test.mjs:166)

        [Test]
        public void RunLoadoutData_RoundTripsThroughJson_TheTrialFlagIncluded()
        {
            // js: test/ext/world.test.mjs:165-167, bridge/exploration/Loadouts.js:32-45
            RunLoadout run = Loadouts.ToRunLoadout(
                Loadout.Of(new[] { Mask, Sand, Book, Wick }, new[] { "joka_no_ko", "yakuso", "shirube_no_ishi" }, 4), 4);
            string json = Json.Stringify(Loadouts.RunLoadoutToData(run));
            Assert.That(json, Is.EqualTo(
                "{\"tools\":[[\"bosho_no_men\",\"Density\",1],[\"kokugen_no_suna\",\"Time\",1],[\"utsushi_no_tobari\",\"Survey\",1],[\"toshin_no_kakera\",\"Interlude\",15]]," +
                "\"consumables\":[[\"joka_no_ko\",\"Gauge\",10],[\"yakuso\",\"Heal\",20],[\"shirube_no_ishi\",\"Teleport\",1]],\"slots\":4,\"trial\":true}"));
            RunLoadout back = Loadouts.RunLoadoutFromData(Json.Parse(json));
            Assert.That(back.Tools, Is.EqualTo(run.Tools));
            Assert.That(back.Consumables, Is.EqualTo(run.Consumables));
            Assert.That(back.ToolSlotCount, Is.EqualTo(4));
            Assert.That(back.Trial, Is.True);

            // a save without `trial` reads as the C#'s RunLoadout
            JsonObject plain = Loadouts.RunLoadoutToData(RunStart.TrialLoadout());
            plain.Remove("trial");
            Assert.That(Loadouts.RunLoadoutFromData(plain).Trial, Is.False);
            // ... and one without `slots` has the RunLoadout's default (the JS hands undefined to the constructor)
            JsonObject noSlots = Loadouts.RunLoadoutToData(Loadouts.ToRunLoadout(Loadout.Of(new[] { Mask }, null, 4), 4));
            noSlots.Remove("slots");
            Assert.That(Loadouts.RunLoadoutFromData(noSlots).ToolSlotCount, Is.EqualTo(RunLoadout.ToolSlots));
            Assert.Throws<ArgumentException>(() => Loadouts.RunLoadoutFromData("{}"));
            Assert.Throws<ArgumentException>(() => Loadouts.RunLoadoutFromData(Json.Parse("{\"tools\":[[\"x\",\"Nope\",1]],\"consumables\":[],\"slots\":3}")));
            Assert.Throws<ArgumentException>(() => Loadouts.RunLoadoutFromData(Json.Parse("{\"tools\":[],\"consumables\":[],\"slots\":\"3\"}")));
        }

        [Test]
        public void L5_HandOff_EveryToolsLoadout_ComesBackFromTheSaveAsItWent()
        {
            // js: test/ext/limits.test.mjs:407 savedRunLoadout and :475 (the Expedition half is ExpeditionTrialTests')
            int slots = DeckLimits.Default.ToolSlots;
            foreach (ItemDef tool in ItemCatalogue.Tools)
            {
                RunLoadout run = Loadouts.ToRunLoadout(Loadout.Of(new[] { tool.Id, tool.Id, Mask }, new[] { "joka_no_ko" }, slots), slots);
                RunLoadout saved = Loadouts.RunLoadoutFromData(Json.Parse(Json.Stringify(Loadouts.RunLoadoutToData(run))));
                Assert.That(saved.Tools, Is.EqualTo(run.Tools), tool.Id);
                Assert.That(saved.Consumables, Is.EqualTo(run.Consumables), tool.Id);
                Assert.That(saved.ToolSlotCount, Is.EqualTo(3), tool.Id);
                Assert.That(saved.Trial, Is.True, tool.Id);
            }
        }

        // ---------------------------------------------------------------- the JS over a grid (TraceGain.parity.mjs)

        /// <summary>
        /// The grid of TraceGain.parity.mjs, which wrote TraceGain.parity.txt from the browser trial's TraceGain: the
        /// same heirs, trace rows, picks and modes, in the same order. Each entry is one scenario's gains as the JS
        /// writes them (<c>[index, [[id, Name, Detail, Notes, Nothing, From, To]…]]</c>).
        /// </summary>
        private static IEnumerable<string> ParityGrid()
        {
            int?[] caps = { null, 20, 21 };
            var ranks = new[]
            {
                new (string, int)[0],
                new[] { ("thrust", 3), ("brace", 2) },
                new[] { ("feint", 2), ("kesa_cut", 1), ("rend", 3) },
            };
            var picks = new[]
            {
                new string[0],
                new[] { "card:feint" },
                new[] { "card:thrust", "card:feint", "enemy:shadow_hound" },
                new[] { "card:no_such_card", "card:boar_rush", "scribble:0" },
                new[] { "card:feint", "card:feint", "memo", "page:page-1", "enemy:polearm_warped" },
            };
            string[] modes = { "preview", "done", "take" };
            int index = 0;
            foreach (int? cap in caps)
            {
                foreach ((string, int)[] rank in ranks)
                {
                    foreach (string[] pick in picks)
                    {
                        foreach (string mode in modes)
                        {
                            bool some = index % 2 == 1;
                            Expedition ex = WithRanks(Trial(3, limits: cap == null ? null : new DeckLimits(deckMax: cap),
                                journal: some ? Levels(("shadow_hound", 1), ("polearm_warped", 2)) : null), rank);
                            IReadOnlyList<Scribble> held = some ? new[] { new Scribble(1, "A", "memo") } : new Scribble[0];
                            IReadOnlyList<CandidateView> rows = ParityRows();
                            IReadOnlyDictionary<string, TraceGainRow> gains;
                            if (mode == "preview") gains = TraceGain.TraceGains(ex, rows, pick, scribbles: held);
                            else if (mode == "done") gains = TraceGain.TraceGains(ex, rows, pick, done: true, scribbles: held);
                            else gains = TakeAndWrite(ex, rows, pick, held);
                            var output = gains.Select(p => (object)new List<object>
                            {
                                p.Key, p.Value.Name, p.Value.Detail, p.Value.Notes.Select(n => (object)n).ToList(), p.Value.Nothing,
                                p.Value.CardResult?.From ?? p.Value.PageResult?.From, p.Value.CardResult?.To ?? p.Value.PageResult?.To,
                            }).ToList();
                            yield return Json.Stringify(new List<object> { index, output });
                            index++;
                        }
                    }
                }
            }
        }

        /// <summary>The trace rows of the grid (TraceGain.parity.mjs candidates()): typed rows and rows with the Detail only.</summary>
        private static IReadOnlyList<CandidateView> ParityRows()
        {
            string[] cards = { "thrust", "feint", "brace", "kesa_cut", "boar_rush", "rend", "no_such_card" };
            var enemies = new[] { ("shadow_hound", 1), ("polearm_warped", 2), ("rusted_revenant", 1) };
            var rows = new List<CandidateView>();
            for (int i = 0; i < cards.Length; i++)
            {
                int rank = i % 3 + 1;
                bool typed = i % 2 == 0;
                rows.Add(new CandidateView("card:" + cards[i], CandidateKind.Card, "技術", "trace-" + cards[i], rank + " 段目",
                    cardId: typed ? cards[i] : null, rank: typed ? rank : (int?)null));
            }
            for (int i = 0; i < enemies.Length; i++)
            {
                var (id, level) = enemies[i];
                bool typed = i % 2 == 0;
                rows.Add(new CandidateView("enemy:" + id, CandidateKind.EnemyPage, "敵の頁", "page-" + id, "開示度 " + level + " / 2",
                    enemyId: typed ? id : null, disclosure: typed ? level : (int?)null));
            }
            rows.Add(new CandidateView("page:page-1", CandidateKind.DungeonPage, "頁", "希少な頁 1", "手記の頁", pageId: "page-1"));
            rows.Add(new CandidateView("scribble:0", CandidateKind.Scribble, "走り書き", "第 1 代", "", generation: 1, from: "A", text: "memo"));
            rows.Add(new CandidateView("memo", CandidateKind.Scribble, "走り書き", "第 2 代", null, generation: 2, from: "B", text: "mine"));
            return rows;
        }

        /// <summary>The grid's take: the picks (each once) taken into the life, and the receipt written from what it did.</summary>
        private static IReadOnlyDictionary<string, TraceGainRow> TakeAndWrite(Expedition ex, IReadOnlyList<CandidateView> rows,
            IReadOnlyList<string> picks, IReadOnlyList<Scribble> held)
        {
            var picked = picks.Distinct().Select(id => rows.First(c => c.Id == id)).ToList();
            var cards = picked.Where(c => c.Kind == CandidateKind.Card && c.Id != "card:no_such_card")
                .Select(c => new TakenCard(c.Id.Substring(5), c.Detail[0] - '0', 0, c.Id.Substring(5)));
            var disclosure = picked.Where(c => c.Kind == CandidateKind.EnemyPage)
                .Select(c => new KeyValuePair<string, int>(c.Id.Substring(6), c.Detail.Split(' ')[1][0] - '0'));
            var pages = picked.Where(c => c.Kind == CandidateKind.DungeonPage).Select(c => new InheritanceCore.JournalPage(c.PageId, c.Name));
            InheritResult result = ex.Inherit(new TakenLegacy("trace-1", cards, new InheritanceCore.Journal(disclosure, pages, "", null), 0));
            var list = held.ToList();
            var scribbles = new List<ScribbleRow>();
            foreach (CandidateView c in picked.Where(x => x.Kind == CandidateKind.Scribble))
            {
                bool dup = list.Any(t => t.Generation == c.Generation && t.Text == c.Text);
                if (!dup) list.Add(new Scribble(c.Generation.Value, c.From, c.Text));
                scribbles.Add(new ScribbleRow(c.Generation.Value, c.From, c.Text, !dup));
            }
            return TraceGain.TraceGains(ex, rows, picks, done: true, result: result.WithScribbles(scribbles), scribbles: held);
        }

        [Test]
        public void TraceGains_AgreeWithTheJs_OverTheGrid()
        {
            // js: bridge/exploration/TraceGain.js (traceGains, inheritPreview, pagePreview, cardGain, pageGain) and
            // app/life.js:119 (Inherit), run by TraceGain.parity.mjs over the same grid
            string path = ParityPath();
#if UNITY_5_3_OR_NEWER
            // unity:sync copies .cs and .asmdef only: the JS's gains are read under dotnet test
            if (!File.Exists(path)) Assert.Ignore("the JS's gains are read under dotnet test (" + path + " is not in this project)");
#endif
            Assert.That(File.Exists(path), Is.True, "the JS's gains are missing: " + path);
            string[] js = File.ReadAllLines(path).Where(l => l.Length > 0).ToArray();
            List<string> cs = ParityGrid().ToList();
            Assert.That(cs.Count, Is.EqualTo(js.Length), "the same grid");
            for (int i = 0; i < js.Length; i++) Assert.That(cs[i], Is.EqualTo(js[i]), "scenario " + i);
        }

        private static string ParityPath([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
            Path.Combine(Path.GetDirectoryName(here) ?? "", "TraceGain.parity.txt");

        // ---------------------------------------------------------------- a new life of the same expedition

        [Test]
        public void ANewLife_LeavesTheTakesGiftsBehind_ButKeepsTheTraceTaken()
        {
            // js: app/life.js:119-152 (Inherit) and bridge/exploration/Expedition.js:466 (BeginLife): the gifts are the
            // life's (deck, owned, 段, journal, pages), the trace taken is the expedition's (the map hook reads it:
            // docs/rules.md §6.5 「JS でも NewLife で戻らない」)
            Expedition ex = Trial(3);
            int deck = ex.Deck.Count;
            ex.Inherit(new TakenLegacy("trace-1", new[] { new TakenCard("feint", 2, 8, "feint") },
                new InheritanceCore.Journal(new[] { new KeyValuePair<string, int>("shadow_hound", 2) }, null, "", null), 0));
            Assert.That(ex.Deck.Count, Is.EqualTo(deck + 1));
            Assert.That(ex.Mastery.RankOf("feint"), Is.EqualTo(2));
            Assert.That(ex.DisclosureOf("shadow_hound"), Is.EqualTo(2));
            int layer = ex.InheritedLayer.Value;
            EndEveryFight(ex.StartEncounter(), BattleCore.GameResult.Lost, 0, 0);
            ex.Return();
            Assert.That(ex.Session.Act(ScreenAction.NewLife), Is.True);
            Assert.That(ex.Deck.Count, Is.EqualTo(deck));
            Assert.That(ex.Mastery.RankOf("feint"), Is.EqualTo(0));
            Assert.That(ex.DisclosureOf("shadow_hound"), Is.EqualTo(0));
            Assert.That(ex.InheritedTraceId, Is.EqualTo("trace-1"));
            Assert.That(ex.InheritedLayer, Is.EqualTo(layer));
        }
    }
}
#endif
