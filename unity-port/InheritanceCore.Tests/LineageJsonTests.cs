using System.Collections.Generic;
using BattleCore;
using InheritanceCore;
using NUnit.Framework;

namespace InheritanceCore.Tests
{
    /// <summary>
    /// The lineage-JSON half of the trial's test/app/stamp.test.mjs (the save reads its lineage back as written; the
    /// stamp and tab handling are the save module's), plus the JSON shapes the save and the recorder hand in.
    /// </summary>
    public class LineageJsonTests
    {
        [Test]
        public void TheSaveItselfReadsAsBefore([Values(5UL, 9UL)] ulong seed)
        {
            var lineage = Inheritance.NewLineage(seed);
            string text = Json.Stringify(new JsonObject { ["v"] = 1, ["rev"] = 3, ["tab"] = "abc", ["lineage"] = Inheritance.ToJson(lineage), ["life"] = null });
            var back = Inheritance.FromJson(((JsonObject)Json.Parse(text)!)["lineage"]);
            Assert.That(Inheritance.Serialize(back), Is.EqualTo(Inheritance.Serialize(lineage)));
        }

        [Test]
        public void ANumericSeedReadsAsItsDecimalText()
        {
            var json = Inheritance.ToJson(Inheritance.NewLineage(42));
            json["Seed"] = 42;
            Assert.That(Inheritance.FromJson(json).Seed, Is.EqualTo("42"));
            json["Seed"] = -1;
            Assert.That(Inheritance.FromJson(json).Seed, Is.EqualTo("18446744073709551615"), "BigInt.asUintN(64, -1)");
            json["Seed"] = 1.5;
            Assert.Throws<InheritanceRuleException>(() => Inheritance.FromJson(json));
        }

        [Test]
        public void AnOpenLifeMustSayItHasNoEnding()
        {
            var s = Inheritance.StartLife(Inheritance.NewLineage(42), 1);
            var json = Inheritance.ToJson(s.Lineage);
            ((JsonObject)json["Current"]!).Remove("Ending");
            var e = Assert.Throws<InheritanceRuleException>(() => Inheritance.FromJson(json));
            Assert.That(e!.Message, Is.EqualTo("bad save: the current life has an ending"), "`l.Ending !== null` refuses an absent Ending too");
        }

        [Test]
        public void JournalFromJson_TakesTheJsShapes()
        {
            var j = Inheritance.JournalFromJson(Json.Parse(
                "{\"disclosure\":{\"b\":1,\"a\":2,\"c\":0},\"pages\":3,\"memo\":\"m\",\"scribbles\":[{\"generation\":1,\"name\":\"N\",\"text\":\"t\"}]}"));
            Assert.That(Json.Stringify(j.ToJson()), Is.EqualTo(
                "{\"Disclosure\":{\"a\":2,\"b\":1},\"Pages\":[{\"Id\":\"page-1\",\"Title\":\"希少な頁 1\"},{\"Id\":\"page-2\",\"Title\":\"希少な頁 2\"},"
                + "{\"Id\":\"page-3\",\"Title\":\"希少な頁 3\"}],\"Memo\":\"m\",\"Scribbles\":[{\"Generation\":1,\"Name\":\"N\",\"Text\":\"t\"}]}"));
            var ids = Inheritance.JournalFromJson(Json.Parse("{\"Pages\":[\"x\",{\"Id\":\"y\",\"Text\":\"  \"},{\"Id\":\"z\",\"Title\":\"T\",\"Text\":\"w\"}]}"));
            Assert.That(Json.Stringify(ids.ToJson()["Pages"]),
                Is.EqualTo("[{\"Id\":\"x\",\"Title\":\"希少な頁 1\"},{\"Id\":\"y\",\"Title\":\"希少な頁 2\"},{\"Id\":\"z\",\"Title\":\"T\",\"Text\":\"w\"}]"));
            Assert.That(Json.Stringify(Inheritance.JournalFromJson(null).ToJson()), Is.EqualTo("{\"Disclosure\":{},\"Pages\":[],\"Memo\":\"\",\"Scribbles\":[]}"));
            Refused(() => Inheritance.JournalFromJson(Json.Parse("{\"Pages\":\"x\"}")), "journal pages must be a count or a list");
            Refused(() => Inheritance.JournalFromJson(Json.Parse("{\"Memo\":3}")), "the memo is text");
            Refused(() => Inheritance.JournalFromJson(Json.Parse("{\"Pages\":[\"a\",\"a\"]}")), "page a is in the journal twice");
            Refused(() => Inheritance.JournalFromJson(Json.Parse("{\"Disclosure\":{\"a\":1.5}}")),
                "disclosure of a must be an integer in 0..2 (got 1.5)");
        }

        private static void Refused(TestDelegate fn, string message)
        {
            var e = Assert.Throws<InheritanceRuleException>(fn);
            Assert.That(e!.Message, Is.EqualTo(message));
        }

        private static JsonObject Saved(Lineage l) => (JsonObject)Json.Parse(Inheritance.Serialize(l))!;

        private static JsonObject First(JsonObject save, string list) => (JsonObject)((List<object?>)save[list]!)[0]!;

        [Test]
        public void AnEnumIsReadByItsMemberName_NotByANumber()
        {
            // isMember(LifeEnding, '42') is false; Enum.TryParse alone reads "42" as (LifeEnding)42.
            var dead = InheritanceTests.DieAt(Inheritance.NewLineage(42), 4);
            var save = Saved(dead);
            First(save, "Lives")["Ending"] = "42";
            Refused(() => Inheritance.FromJson(save), "bad save: ending 42");
            save = Saved(dead);
            First(save, "Lives")["Ending"] = "1";
            Refused(() => Inheritance.FromJson(save), "bad save: ending 1");
            save = Saved(dead);
            First(save, "Traces")["Ending"] = "5";
            Refused(() => Inheritance.FromJson(save), "bad save: ending 5 of trace-1");
            save = Saved(dead);
            First(save, "Traces")["NodeKind"] = "42";
            Refused(() => Inheritance.FromJson(save), "bad save: unknown node kind 42");
            save = Saved(dead);
            ((JsonObject)((List<object?>)First(save, "Traces")["Candidates"]!)[0]!)["Kind"] = "9";
            Refused(() => Inheritance.FromJson(save), "bad save: a candidate of trace-1");
        }

        [Test]
        public void ASavedPageWhoseTextIsNotTextIsAnEmptyPage()
        {
            // isFilledPage needs typeof Text === 'string': the hall leaves such a page out, and taking it gives no text.
            var dead = InheritanceTests.DieAt(Inheritance.NewLineage(42), 4);
            var save = Saved(dead);
            ((List<object?>)First(save, "Traces")["Candidates"]!).Add(new JsonObject
            {
                ["Id"] = "page:p",
                ["Kind"] = "DungeonPage",
                ["PageId"] = "p",
                ["Name"] = "頁",
                ["Text"] = 7,
            });
            var l = Inheritance.FromJson(save);
            Assert.That(Inheritance.HallView(l).Traces[0].CountLine, Is.EqualTo("遺産の候補：技術 3 ／ 頁 2 ／ 走り書き 1"));
            var r = Inheritance.TakeTrace(Inheritance.StartLife(l, 1).Lineage, "trace-1", new[] { "page:p" });
            Assert.That(Json.Stringify(r.Taken.Journal.ToJson()["Pages"]), Is.EqualTo("[{\"Id\":\"p\",\"Title\":\"頁\"}]"));
        }

        [Test]
        public void ANullDeckIsNoCards()
        {
            // masteredCards(null) is []: a null book or list converts to no deck, not to an exception.
            var s = Inheritance.StartLife(Inheritance.NewLineage(42), 1);
            MasteryBook? book = null;
            CardEntry[]? entries = null;
            var a = Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(4), book, null);
            var b = Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(4), entries, null);
            Assert.That(a.Traces[0].Candidates, Is.Empty);
            Assert.That(Inheritance.Serialize(b), Is.EqualTo(Inheritance.Serialize(a)));
        }

        [Test]
        public void AMissingPlaceIsRefusedAsAMissingLayer()
        {
            var s = Inheritance.StartLife(Inheritance.NewLineage(42), 1);
            Refused(() => Inheritance.RecordDeath(s.Lineage, s.Life, null!, null, null), "layer must be an integer in 1..7 (got undefined)");
            Refused(() => Inheritance.RecordSurvival(s.Lineage, s.Life, null!, null, null), "layer must be an integer in 1..7 (got undefined)");
            Refused(() => Inheritance.RecordEnd(s.Lineage, s.Life, null!, null, null), "undefined does not end a life");
        }

        [Test]
        public void ATakeReadsBackFromItsJson()
        {
            var l = InheritanceTests.DieAt(Inheritance.NewLineage(42), 4);
            var s = Inheritance.StartLife(l, 1);
            var r = Inheritance.TakeTrace(s.Lineage, "trace-1", new[] { "card:kansatsu", "enemy:sabiyari", "memo" });
            string text = Json.Stringify(r.Taken.ToJson());
            var back = TakenLegacy.FromJson(Json.Parse(text));
            Assert.That(Json.Stringify(back.ToJson()), Is.EqualTo(text));
            Assert.That(back.Cards[0].CardId, Is.EqualTo("kansatsu"));
            Assert.That(back.Journal.Disclosure["sabiyari"], Is.EqualTo(2));
        }

        [Test]
        public void HeirJournal_TurnsTheSurvivorsMemoIntoTheirScribble()
        {
            var l = InheritanceTests.SurviveAt(Inheritance.NewLineage(42), 3);
            var s = Inheritance.StartLife(l, 1);
            Assert.That(s.JournalFrom!.Generation, Is.EqualTo(1));
            var heir = Inheritance.HeirJournal(s.Journal, s.JournalFrom);
            Assert.That(heir.Memo, Is.EqualTo(""));
            Assert.That(heir.Scribbles.Count, Is.EqualTo(1));
            Assert.That((heir.Scribbles[0].Generation, heir.Scribbles[0].Name, heir.Scribbles[0].Text),
                Is.EqualTo((1, (string?)l.Lives[0].Name, (string?)"門竜は押すと崩れる")));
            Assert.That(Json.Stringify(Inheritance.HeirJournal(heir, s.JournalFrom).ToJson()), Is.EqualTo(Json.Stringify(heir.ToJson())), "once only");
            Assert.That(Inheritance.HeirJournal(s.Journal).Memo, Is.EqualTo("門竜は押すと崩れる"), "without a writer the memo stays");
        }
    }
}
