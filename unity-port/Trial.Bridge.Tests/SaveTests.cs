// The save module (src/app/save.js) and the keeper's writes (main.js persist / persistSoon). The first two tests
// port the storage parts of test/app/settle.test.mjs: the storage probe, the copy kept of a save whose life would
// not replay, and openSave's three cases.
using System;
using System.Collections.Generic;
using Depiction;
using InheritanceCore;
using NUnit.Framework;
using static Trial.Bridge.Tests.SaveTestKit;

namespace Trial.Bridge.Tests
{
    public class SaveTests
    {
        [SetUp]
        public void ResetQueue() => Microtasks.Reset();

        // js: test/app/settle.test.mjs:241
        [Test]
        public void StorageWorks_TellsBlockedStorageApart_AndLeavesNothingBehind_KeepBrokenSave_CopiesTheSaveAside()
        {
            var ok = new MemoryKeyValueStore();
            Assert.That(Save.StorageWorks(ok), Is.True);
            Assert.That(ok.Keys, Is.Empty, "the probe is removed");
            var lineage = NewLineage(9);
            var life = LifeSave.FromJson(Json.Parse("{\"start\":{\"seed\":\"1\"},\"log\":[[\"S\",\"Step\",[3]]]}"));
            Assert.That(Save.WriteSave(ok, new TrialSave(lineage, life)), Is.True);
            Assert.That(Save.ReadBrokenSave(ok), Is.Null);
            Assert.That(Save.KeepBrokenSave(ok), Is.True);
            Assert.That(Save.ReadBrokenSave(ok), Is.EqualTo(ok.Read(Save.SaveKey)), "the copy is the save as stored");

            var broken = new MemoryKeyValueStore { Blocked = true };
            Assert.That(Save.StorageWorks(broken), Is.False);
            Assert.That(Save.WriteSave(broken, new TrialSave(lineage)), Is.False, "a write reports the failure");
            Assert.That(Save.KeepBrokenSave(broken), Is.False);

            Assert.That(Save.StorageWorks(null), Is.False, "no storage at all");
        }

        // js: test/app/settle.test.mjs:270
        // A stored lineage that does not load (another build's version, a write cut off) is never dropped without a
        // word: openSave says so and copies the text aside, and leaves it under its own key.
        [Test]
        public void OpenSave_NothingStored_AGoodSave_AndAnUnreadableOne_KeptAsideAndLeftInPlace()
        {
            Quietly(() =>
            {
                var st = new MemoryKeyValueStore();
                OpenedSave none = Save.OpenSave(st);
                Assert.That(none.Save, Is.Null, "nothing stored is not an error");
                Assert.That(none.Unreadable, Is.Null);
                Assert.That(none.Stamp, Is.Null);

                var lineage = NewLineage(11);
                Save.WriteSave(st, new TrialSave(lineage, null, "D"));
                string good = st.Read(Save.SaveKey);
                OpenedSave r = Save.OpenSave(st);
                Assert.That(r.Unreadable, Is.Null);
                Assert.That(Serialize(r.Save.Lineage), Is.EqualTo(Serialize(lineage)));
                Assert.That(r.Save.DeckSave, Is.EqualTo("D"));
                Assert.That(Save.ReadBrokenSave(st), Is.Null, "a save that loads is not copied");

                var parsed = (JsonObject)Json.Parse(good);
                var lin = (JsonObject)parsed["lineage"];
                lin["Version"] = (Json.IsNumber(lin["Version"]) ? (int)Json.ToDouble(lin["Version"]) : 1) + 1;
                string future = Json.Stringify(parsed);
                var variants = new Dictionary<string, string>
                {
                    ["future"] = future,
                    ["truncated"] = good.Substring(0, (int)Math.Floor(good.Length * 0.7)),
                    ["noLineage"] = "{\"v\":1,\"lineage\":null}",
                    ["nullText"] = "null",
                };
                foreach (var pair in variants)
                {
                    string name = pair.Key;
                    string text = pair.Value;
                    st.Write(Save.SaveKey, text);
                    st.Remove(Save.BrokenKey);
                    OpenedSave b = Save.OpenSave(st);
                    Assert.That(b.Save, Is.Null, name);
                    Assert.That(b.Unreadable != null && b.Unreadable.Error != null, Is.True, name + ": says why");
                    Assert.That(b.Unreadable.Kept, Is.True, name);
                    Assert.That(Save.ReadBrokenSave(st), Is.EqualTo(text), name + ": the copy is the text as stored");
                    Assert.That(st.Read(Save.SaveKey), Is.EqualTo(text), name + ": nothing is overwritten until the player starts anew");
                    Assert.That(Save.LoadSave(st), Is.Null, name + ": loadSave stays null");
                }

                // Storage that reads but refuses writes (full): the save is still reported, the copy is not claimed.
                var full = new MemoryKeyValueStore();
                full.Write(Save.SaveKey, variants["truncated"]);
                full.RefuseWrites = true;
                OpenedSave f = Save.OpenSave(full);
                Assert.That(f.Save, Is.Null);
                Assert.That(f.Unreadable.Kept, Is.False);
            });
        }

        // ---- the save's text ----

        [Test]
        public void SaveText_HasTheJsKeyOrder_AndTheJsFallbacks()
        {
            var lineage = NewLineage(3);
            string lin = Serialize(lineage);
            Assert.That(Save.SaveText(new TrialSave(lineage), new Stamp(2, "t")),
                Is.EqualTo("{\"v\":1,\"rev\":2,\"tab\":\"t\",\"lineage\":" + lin + ",\"life\":null,\"deckSave\":null}"));
            Assert.That(Save.SaveText(new TrialSave(lineage, null, "")), Is.EqualTo("{\"v\":1,\"lineage\":" + lin + ",\"life\":null,\"deckSave\":null}"),
                "no stamp: no rev and tab; an empty deck string is null (deckSave || null)");
            var life = LifeSave.Departing(lineage.ToJson());
            Assert.That(Save.SaveText(new TrialSave(lineage, life, "thrust:2"), new Stamp(5, "x")),
                Is.EqualTo("{\"v\":1,\"rev\":5,\"tab\":\"x\",\"lineage\":" + lin + ",\"life\":{\"before\":" + lin + "},\"deckSave\":\"thrust:2\"}"));
            Assert.That(Save.WriteSave(new MemoryKeyValueStore(), new TrialSave(null)), Is.False, "a save without its lineage is not written");
            Assert.That(Save.WriteSave(new MemoryKeyValueStore(), null), Is.False);
        }

        [Test]
        public void OpenSave_KeepsTheLifeAsStored_EvenWhenItWouldNotResume()
        {
            var st = new MemoryKeyValueStore();
            string lin = Serialize(NewLineage(3));
            st.Write(Save.SaveKey, "{\"v\":1,\"rev\":1,\"tab\":\"a\",\"lineage\":" + lin + ",\"life\":{\"memo\":5,\"log\":\"x\"},\"deckSave\":7}");
            OpenedSave o = Save.OpenSave(st);
            Assert.That(o.Unreadable, Is.Null, "a broken life does not make the lineage unreadable");
            Assert.That(o.Save.Life, Is.Not.Null);
            Assert.That(o.Save.DeckSave, Is.Null, "a deckSave that is not a string reads as null");
            Assert.That(o.Save.Life.Memo, Is.EqualTo(""));
            Assert.Throws<SaveFormatException>(() => o.Save.Life.ReadStart(), "no start: the resume fails");
            Assert.Throws<SaveFormatException>(() => o.Save.Life.ReadLog(), "a log that is not a list");
            Assert.That(Save.SaveText(o.Save, new Stamp(2, "a")), Does.Contain(",\"life\":{\"memo\":5,\"log\":\"x\"},"), "written back as it was");

            st.Write(Save.SaveKey, "{\"v\":1,\"lineage\":" + lin + ",\"life\":0}");
            Assert.That(Save.OpenSave(st).Save.Life, Is.Null, "a falsy life is none (data.life || null)");
            Assert.That(LifeSave.FromJson(null), Is.Null);
            Assert.That(LifeSave.FromJson(false), Is.Null);
            Assert.That(LifeSave.FromJson(""), Is.Null);
            Assert.That(LifeSave.FromJson(0), Is.Null, "so a falsy life is never written back as itself (save.life || null)");
            st.Write(Save.SaveKey, "{\"v\":1,\"lineage\":" + lin + ",\"life\":[1]}");
            LifeSave odd = Save.OpenSave(st).Save.Life;
            Assert.That(odd, Is.Not.Null, "a truthy life that is not an object is still a life");
            Assert.Throws<SaveFormatException>(() => odd.ReadStart());
            Assert.That(odd.Before, Is.Null);
            Assert.That(odd.Has("before"), Is.False);
            Assert.That(Json.Stringify(odd.ToJson()), Is.EqualTo("[1]"));
        }

        [Test]
        public void OpenSave_ReadsTheBrowsersBigIntTag_AsADecimalString()
        {
            var st = new MemoryKeyValueStore();
            var lineage = (JsonObject)NewLineage(3).ToJson();
            string seed = (string)lineage["Seed"];
            lineage["Seed"] = new JsonObject { ["$big"] = seed };
            st.Write(Save.SaveKey, Json.Stringify(new JsonObject { ["v"] = 1, ["lineage"] = lineage, ["life"] = null }));
            OpenedSave o = Save.OpenSave(st);
            Assert.That(o.Unreadable, Is.Null);
            Assert.That(o.Save.Lineage.Seed, Is.EqualTo(seed));
        }

        [Test]
        public void Revive_ReadsTheReplacersTags()
        {
            object v = Save.Revive(Json.Parse("{\"a\":{\"$big\":\"18446744073709551615\"},\"m\":{\"$map\":[[\"x\",1],[\"y\",2]]},\"s\":{\"$set\":[\"p\",\"q\"]},\"l\":[{\"$big\":\"7\"}]}"));
            Assert.That(Json.Stringify(v), Is.EqualTo("{\"a\":\"18446744073709551615\",\"m\":{\"x\":1,\"y\":2},\"s\":[\"p\",\"q\"],\"l\":[\"7\"]}"));
        }

        // ---- the app's words ----

        [Test]
        public void AppText_IsTheJsWords()
        {
            Assert.That(string.Join("", AppText.NoStorageLines), Is.EqualTo("この環境では保存できません。ページを閉じると、ここまでの記録は失われます。"));
            Assert.That(AppText.UnreadableLines(false)[1], Is.EqualTo("控えを残せないため、はじめからを選ぶと失われます。"));
            Assert.That(AppText.TitleNoticeLines(true, true, false).Count, Is.EqualTo(4));
            Assert.That(AppText.TitleNoticeY(4), Is.EqualTo(790));
            Assert.That(AppText.TitleNoticeY(1), Is.EqualTo(840));
            Assert.That(AppText.SettingsNoticeY(60), Is.EqualTo(16));
            Assert.That(AppText.SettingsNoticeY(300), Is.EqualTo(224));
            Assert.That(AppText.SettingsNoticeFallbackY, Is.EqualTo(40));
            Assert.That((AppText.StageNoticeX, AppText.StageNoticeY, AppText.StageNoticeWidth, AppText.SaveFailedMs, AppText.StaleMs),
                Is.EqualTo((560, 104, 800, 8000, 10000)), "noticeLine(text, 560, 104, 800, 8000 / 10000)");
            Assert.That((AppText.TitleNoticeX, AppText.TitleNoticeWidth, AppText.SettingsNoticeX, AppText.SettingsNoticeWidth),
                Is.EqualTo((152, 560, 540, 840)));
            Assert.That(AppText.TitleNoticeLines(false, false, true), Is.Empty);
            Assert.That(AppText.TitleNoticeLines(true, false, false), Is.EqualTo(new[]
            {
                "保存された記録を読み込めませんでした。", "控えを残せないため、はじめからを選ぶと失われます。",
                "この環境では保存できません。", "ページを閉じると、ここまでの記録は失われます。",
            }), "the unreadable save's lines, then the blocked storage's");
            Assert.That(AppText.NewLineage.Title + AppText.NewLineage.Body + AppText.NewLineage.Ok + AppText.NewLineage.Cancel,
                Is.EqualTo("新しい系譜を始めるこれまでの生と痕跡の記録は消えます。はじめからやめる"));
            Assert.That(AppText.NewLineage.Danger, Is.True);
            Assert.That(AppText.BattleFailed(new Exception("z")).Body, Is.EqualTo("この戦闘をはじめからやり直します。（z）"));
            Assert.That(AppText.StaleText, Is.EqualTo("別のタブでこの記録が先に進みました。最新の記録を読み込み直しました。"));
            Assert.That(AppText.SaveFailedText, Is.EqualTo("保存できませんでした。ページを閉じると、ここまでの記録は失われます。"));
            Assert.That(TrialSettings.Keys, Is.EqualTo(new[] { "speed", "shortFx", "master", "effects", "ambience", "muted" }));
            Assert.That(TrialSettings.Keys, Is.Not.InstanceOf<string[]>(), "the stored keys cannot be changed through a cast");
            Assert.That(AppText.ReasonOf(new SaveFormatException("bad save: no lineage")), Is.EqualTo("（bad save: no lineage）"));
            Assert.That(AppText.MessageOf(new Exception(new string('あ', 100))), Has.Length.EqualTo(80));
            Assert.That(AppText.MessageOf(null), Is.EqualTo("null"));
            ConfirmText c = AppText.CannotResume(true, new Exception("x"));
            Assert.That(c.Body, Is.EqualTo("保存された生を読み込めませんでした。記録は消さずに控えとして残しています。あきらめると、この生は無かったことになり、出立の前から出直します。（x）"));
            Assert.That((c.Ok, c.Cancel, c.Danger), Is.EqualTo(("あきらめる", "タイトルへ", true)));
            Assert.That(AppText.NewOverUnreadable(false, new Exception("y")).Body, Is.EqualTo("読み込めなかった記録に、新しい系譜を上書きします。控えを残せなかったため、記録は失われます。（y）"));
            Assert.That(AppText.BattleFailed(new Exception("z")).Danger, Is.False);
        }

        // ---- the life ----

        private const string StartText =
            "{\"v\":1,\"generation\":3,\"name\":\"灯の三代\",\"seed\":\"12345678901234567890\",\"toolSlots\":3,\"deckMax\":40," +
            "\"survivorBonus\":false,\"traceLayers\":[2,5],\"carvingKnown\":false," +
            "\"journal\":{\"Disclosure\":{\"polearm_warped\":1},\"Pages\":[{\"Id\":\"page-1\",\"Title\":\"一\"}],\"Memo\":\"\",\"Scribbles\":[{\"Generation\":2,\"Name\":\"二\",\"Text\":\"右へ\"}]}," +
            "\"choice\":{\"deck\":[[\"thrust-1\",\"thrust\"],[\"thrust-2\",\"thrust\"]],\"battleToolIds\":[\"ikitsugi_no_fue\"]," +
            "\"loadout\":{\"tools\":[[\"bosho_no_men\",\"Density\",1]],\"consumables\":[[\"yakuso\",\"Heal\",20]],\"slots\":3,\"trial\":true}," +
            "\"talent\":null,\"toolIds\":[],\"consumableIds\":[\"yakuso\"]}}";

        [Test]
        public void LifeStart_ReadsAndWritesTheJsShape_ByteForByte()
        {
            LifeStartData s = LifeStartData.FromJson(Json.Parse(StartText));
            Assert.Multiple(() =>
            {
                Assert.That(s.Generation, Is.EqualTo(3));
                Assert.That(s.Name, Is.EqualTo("灯の三代"));
                Assert.That(s.Seed, Is.EqualTo("12345678901234567890"));
                Assert.That(s.SeedValue, Is.EqualTo(12345678901234567890UL));
                Assert.That(s.ToolSlots, Is.EqualTo(3));
                Assert.That(s.DeckMax, Is.EqualTo(40));
                Assert.That(s.TraceLayers, Is.EqualTo(new[] { 2, 5 }));
                Assert.That(s.Choice.Deck.Count, Is.EqualTo(2));
                Assert.That(s.Choice.Deck[1].InstanceId, Is.EqualTo("thrust-2"));
                Assert.That(s.Choice.Deck[1].DefId, Is.EqualTo("thrust"));
                Assert.That(s.Choice.BattleToolIds, Is.EqualTo(new[] { "ikitsugi_no_fue" }));
                Assert.That(s.Choice.ConsumableIds, Is.EqualTo(new[] { "yakuso" }));
                Assert.That(s.Choice.Talent, Is.Null);
                Assert.That(s.Journal, Is.InstanceOf<JsonObject>());
            });
            Assert.That(Json.Stringify(s.ToJson()), Is.EqualTo(StartText));
        }

        [Test]
        public void LifeStart_FailsOnlyOnWhatTheLifeCannotBeBuiltWithout()
        {
            Assert.Throws<SaveFormatException>(() => LifeStartData.FromJson(null));
            Assert.Throws<SaveFormatException>(() => LifeStartData.FromJson(Json.Parse("{\"seed\":\"1\"}")), "no choice");
            Assert.Throws<SaveFormatException>(() => LifeStartData.FromJson(Json.Parse("{\"seed\":\"-1\",\"choice\":{\"deck\":[]}}")), "a seed that is not a u64");
            Assert.Throws<SaveFormatException>(() => LifeStartData.FromJson(Json.Parse("{\"seed\":\"1\",\"choice\":{\"deck\":[[\"a\"]]}}")), "a deck row without its kind");
            Assert.Throws<SaveFormatException>(() => LifeStartData.FromJson(Json.Parse("{\"seed\":\"1\",\"choice\":{}}")), "no deck");

            LifeStartData bare = LifeStartData.FromJson(Json.Parse("{\"seed\":{\"$big\":\"42\"},\"choice\":{\"deck\":[]}}"));
            Assert.That(bare.SeedValue, Is.EqualTo(42UL));
            Assert.That(bare.Generation, Is.Null);
            Assert.That(bare.ToolSlots, Is.Null);
            Assert.That(bare.DeckMax, Is.Null);
            Assert.That(bare.TraceLayers, Is.Empty);
            Assert.That(bare.Choice.BattleToolIds, Is.Empty);
            Assert.That(Json.Stringify(bare.ToJson()), Is.EqualTo(
                "{\"v\":1,\"name\":\"\",\"seed\":\"42\",\"survivorBonus\":false,\"traceLayers\":[],\"carvingKnown\":false,\"journal\":null," +
                "\"choice\":{\"deck\":[],\"battleToolIds\":[],\"loadout\":null,\"talent\":null,\"toolIds\":[],\"consumableIds\":[]}}"),
                "what the JS leaves out stays out");
        }

        [Test]
        public void LifeSave_Compose_KeepsTheBeforeOfTheLifeItReplaces()
        {
            LifeStartData start = LifeStartData.FromJson(Json.Parse(StartText));
            var log = new LifeLog();
            log.Push("E", "StartEncounter");
            var scribbles = new List<object> { new JsonObject { ["Generation"] = 2, ["Name"] = "二", ["Text"] = "右へ" } };
            var before = new JsonObject { ["Version"] = 1 };

            LifeSave first = LifeSave.Compose(start, log, "めも", scribbles, LifeSave.Departing(before));
            Assert.That(Json.Stringify(first.ToJson()), Is.EqualTo(
                "{\"start\":" + StartText + ",\"log\":[[\"E\",\"StartEncounter\",[]]],\"memo\":\"めも\"," +
                "\"scribbles\":[{\"Generation\":2,\"Name\":\"二\",\"Text\":\"右へ\"}],\"before\":{\"Version\":1}}"));

            log.Push("E", "Return");
            Assert.That(Json.Stringify(first.ToJson()), Does.Contain("\"log\":[[\"E\",\"StartEncounter\",[]]]"), "a write is a snapshot of the log");

            LifeSave second = LifeSave.Compose(start, log, "", scribbles, first);
            Assert.That(second.Before, Is.SameAs(before));
            Assert.That(Json.Stringify(LifeSave.Compose(start, log, "", scribbles, null).ToJson()), Does.EndWith(",\"before\":null}"),
                "no life before: before is null (save.life && save.life.before)");
            LifeSave noBefore = LifeSave.FromJson(Json.Parse("{\"memo\":\"\"}"));
            Assert.That(Json.Stringify(LifeSave.Compose(start, log, "", scribbles, noBefore).ToJson()), Does.Not.Contain("before"),
                "a life without before: the key is left out (undefined)");

            Assert.That(second.ReadStart().Seed, Is.EqualTo("12345678901234567890"));
            Assert.That(second.ReadLog().Count, Is.EqualTo(2));
        }

        // ---- settings ----

        [Test]
        public void Settings_DefaultsAreTheJs()
        {
            var d = TrialSettings.Defaults();
            Assert.Multiple(() =>
            {
                Assert.That(d.Speed, Is.EqualTo(1.25));
                Assert.That(d.SpeedStep, Is.EqualTo(BattleSpeedStep.Normal));
                Assert.That(d.ShortFx, Is.False);
                Assert.That(d.Master, Is.EqualTo(0.8));
                Assert.That(d.Effects, Is.EqualTo(0.9));
                Assert.That(d.Ambience, Is.EqualTo(0.6));
                Assert.That(d.Muted, Is.False);
                Assert.That(d.Reverb, Is.EqualTo(0.18));
            });
            Assert.That(Json.Stringify(d.ToJson()), Is.EqualTo("{\"speed\":1.25,\"shortFx\":false,\"master\":0.8,\"effects\":0.9,\"ambience\":0.6,\"muted\":false}"));
        }

        [Test]
        public void Settings_WriteStoresExactlyTheDefaultsKeys_AndLoadKeepsOnlyValuesOfTheDefaultsType()
        {
            var st = new MemoryKeyValueStore();
            Assert.That(Save.LoadSettings(st).Count, Is.EqualTo(0), "nothing stored");
            var s = TrialSettings.Defaults();
            s.SpeedStep = BattleSpeedStep.Fast;
            s.Master = 0.35;
            s.Muted = true;
            s.Reverb = 0.5;
            Assert.That(Save.WriteSettings(st, s), Is.True);
            Assert.That(st.Read(Save.SettingsKey), Is.EqualTo("{\"speed\":1.5,\"shortFx\":false,\"master\":0.35,\"effects\":0.9,\"ambience\":0.6,\"muted\":true}"),
                "reverb is never stored");
            TrialSettings back = TrialSettings.Load(st);
            Assert.That(back.SpeedStep, Is.EqualTo(BattleSpeedStep.Fast));
            Assert.That(back.Master, Is.EqualTo(0.35));
            Assert.That(back.Muted, Is.True);
            Assert.That(back.Reverb, Is.EqualTo(TrialSettings.DefaultReverb));

            st.Write(Save.SettingsKey, "{\"speed\":\"1.5\",\"shortFx\":1,\"master\":0.1,\"effects\":true,\"muted\":false,\"extra\":3}");
            JsonObject partial = Save.LoadSettings(st);
            Assert.That(partial.Keys, Is.EqualTo(new[] { "master", "muted" }), "a value of another type is dropped");
            TrialSettings mixed = TrialSettings.Load(st);
            Assert.That(mixed.Speed, Is.EqualTo(1.25));
            Assert.That(mixed.Master, Is.EqualTo(0.1));
            Assert.That(mixed.Effects, Is.EqualTo(0.9));

            st.Write(Save.SettingsKey, "{\"speed\":");
            Assert.That(Save.LoadSettings(st).Count, Is.EqualTo(0), "text that does not read");
            st.Write(Save.SettingsKey, "[1,2]");
            Assert.That(Save.LoadSettings(st).Count, Is.EqualTo(0));
            Assert.That(TrialSettings.Load(new MemoryKeyValueStore { Blocked = true }).Speed, Is.EqualTo(1.25));
        }

        [Test]
        public void Settings_AStoredSpeedOffTheStepsReadsAsTheDefaultStep_AndIsKeptUntilChanged()
        {
            var st = new MemoryKeyValueStore();
            st.Write(Save.SettingsKey, "{\"speed\":1.37}");
            TrialSettings s = TrialSettings.Load(st);
            Assert.That(s.SpeedStep, Is.EqualTo(BattleSpeedStep.Normal));
            Save.WriteSettings(st, s);
            Assert.That(st.Read(Save.SettingsKey), Does.StartWith("{\"speed\":1.37,"));
            Assert.That(TrialSettings.StepOf(1.0000001), Is.EqualTo(BattleSpeedStep.Slow), "within 1e-6");
            Assert.That(TrialSettings.StepOf(1.5), Is.EqualTo(BattleSpeedStep.Fast));
            Assert.That(TrialSettings.SpeedOf(BattleSpeedStep.Slow), Is.EqualTo(1));
        }

        [Test]
        public void Settings_AssignCopiesEverything_AndChangedIsRaisedOnRequest()
        {
            var a = TrialSettings.Defaults();
            var b = TrialSettings.Defaults();
            b.Effects = 0.25;
            b.Reverb = 0.4;
            b.ShortFx = true;
            int changed = 0;
            a.Changed += () => changed++;
            a.Assign(b);
            Assert.That(a.Effects, Is.EqualTo(0.25));
            Assert.That(a.Reverb, Is.EqualTo(0.4));
            Assert.That(a.ShortFx, Is.True);
            Assert.That(changed, Is.EqualTo(0));
            a.NotifyChanged();
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(a.Clone().Effects, Is.EqualTo(0.25));
        }

        // ---- the keeper's writes ----

        [Test]
        public void Persist_WithALiveLife_WritesTheLifeAsMainJsComposesIt()
        {
            var st = new MemoryKeyValueStore();
            var k = SaveKeeper.Open(st, "K");
            var lineage = NewLineage(6);
            k.Save = new TrialSave(lineage);
            Assert.That(k.Persist(force: true), Is.True);
            k.Save.Life = LifeSave.Departing(lineage.ToJson());
            var live = new FakeLive { Start = LifeStartData.FromJson(Json.Parse(StartText)), Memo = "m" };
            live.Log.Push("E", "StartEncounter");
            k.Live = live;
            Assert.That(k.Persist(), Is.True);

            OpenedSave o = Save.OpenSave(st);
            Assert.That(o.Save.Life.Memo, Is.EqualTo("m"));
            Assert.That(o.Save.Life.ReadLog().Count, Is.EqualTo(1));
            Assert.That(Json.Stringify(o.Save.Life.Before), Is.EqualTo(Serialize(lineage)), "before is carried over");
            Assert.That(Json.Stringify(o.Save.Life.ReadStart().ToJson()), Is.EqualTo(StartText));
            SameStamp(o.Stamp, 2, "K");

            Assert.That(new SaveKeeper(st).Persist(), Is.False, "a keeper with no save writes nothing");
        }

        [Test]
        public void PersistSoon_IsOneWritePerTurnOfTheQueue_AndOnlyWhileALifeIsLive()
        {
            var st = new MemoryKeyValueStore();
            var k = SaveKeeper.Open(st, "K");
            k.Save = new TrialSave(NewLineage(6));
            k.Persist(force: true);
            int writes = st.Writes;

            k.PersistSoon();
            Microtasks.Drain();
            Assert.That(st.Writes, Is.EqualTo(writes), "no live life: nothing is written");

            k.Live = new FakeLive { Start = LifeStartData.FromJson(Json.Parse(StartText)) };
            k.PersistSoon();
            k.PersistSoon();
            k.PersistSoon();
            Assert.That(k.PersistQueued, Is.True);
            Assert.That(st.Writes, Is.EqualTo(writes), "not inside the call");
            Microtasks.Drain();
            Assert.That(st.Writes, Is.EqualTo(writes + 1), "one write for the turn");
            Assert.That(k.PersistQueued, Is.False);

            k.PersistSoon();
            Assert.That(k.FlushIfDirty(), Is.True, "a queued write goes at once");
            Assert.That(st.Writes, Is.EqualTo(writes + 2));
            Microtasks.Drain();
            Assert.That(st.Writes, Is.EqualTo(writes + 2), "and the queued turn finds nothing left to write");
            Assert.That(k.FlushIfDirty(), Is.False, "nothing queued");
        }

        [Test]
        public void Persist_AFailingWrite_IsReportedOnce_AndStorageCountsAsWorkingAgainAfterAWrite()
        {
            Quietly(() =>
            {
                var st = new MemoryKeyValueStore();
                var k = SaveKeeper.Open(st, "K");
                Assert.That(k.StorageOk, Is.True);
                k.Save = new TrialSave(NewLineage(6));
                Assert.That(k.Persist(force: true), Is.True);
                int failed = 0;
                k.SaveFailed += () => failed++;
                st.RefuseWrites = true;
                Assert.That(k.Persist(), Is.False);
                Assert.That(k.Persist(), Is.False);
                Assert.That(failed, Is.EqualTo(1), "said once");
                Assert.That(k.StorageOk, Is.False);
                SameStamp(k.Stamp, 1, "K", "a failed write keeps the stamp");
                st.RefuseWrites = false;
                Assert.That(k.Persist(), Is.True);
                Assert.That(k.StorageOk, Is.True);

                var blocked = SaveKeeper.Open(new MemoryKeyValueStore { Blocked = true });
                Assert.That(blocked.StorageOk, Is.False, "the probe fails");
                Assert.That(blocked.HasSave, Is.False);
            });
        }

        [Test]
        public void Reopen_DropsTheLiveLife_AndReadsTheStoredSave()
        {
            var st = new MemoryKeyValueStore();
            var k = SaveKeeper.Open(st, "K");
            k.Save = new TrialSave(NewLineage(6));
            k.Persist(force: true);
            k.Live = new FakeLive();
            k.Reopen();
            Assert.That(k.Live, Is.Null);
            Assert.That(k.HasSave, Is.True);
            SameStamp(k.Stamp, 1, "K");
        }

        [Test]
        public void NewTab_IsTwelveBase36Characters_EndingInTheTime()
        {
            string tab = SaveKeeper.NewTab(new Random(1), 1761140000123);
            Assert.That(tab, Has.Length.EqualTo(12));
            Assert.That(tab, Does.Match("^[0-9a-z]{12}$"));
            Assert.That(tab, Does.EndWith("a92z"), "(1761140000123).toString(36).slice(-4)");
            Assert.That(SaveKeeper.NewTab(), Does.Match("^[0-9a-z]{12}$"));
            Assert.That(new SaveKeeper(new MemoryKeyValueStore()).Tab, Has.Length.EqualTo(12));
        }

        [Test]
        public void LineageSeeds_AreTheJsExpressions()
        {
            // node: (1761140000123 ^ (0.5 * 0x7fffffff)) >>> 0, … ^ (0.123456789 * 0x7fffffff), 1761140000123 >>> 0
            Assert.That(SaveKeeper.NewLineageSeed(1761140000123, 0.5), Is.EqualTo(870333060u));
            Assert.That(SaveKeeper.NewLineageSeed(1761140000123, 0.123456789), Is.EqualTo(64138208u));
            Assert.That(SaveKeeper.HallLineageSeed(1761140000123), Is.EqualTo(203408763u));
        }
    }
}
