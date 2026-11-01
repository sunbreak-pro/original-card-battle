// Port of test/app/stamp.test.mjs. One save, several instances (tier1 R1-16 「死亡と生の終了は確定と同時に保存し、
// リロードで遺産を選び直せないようにする」, INHERITANCE.md R-21): every write carries a stamp { rev, tab } at the head
// of the stored text, so an instance can tell, before it writes, whether another one has written since it last read
// or wrote the save (SaveKeeper Persist / InSync). These are the save module's halves of that check, plus the
// keeper's.
using System;
using NUnit.Framework;
using static Trial.Bridge.Tests.SaveTestKit;

namespace Trial.Bridge.Tests
{
    public class StampTests
    {
        // js: test/app/stamp.test.mjs:18
        [Test]
        public void WriteSave_StampsTheStoredText_StoredStampReadsItBackWithoutParsingTheSave()
        {
            var st = new MemoryKeyValueStore();
            Assert.That(Save.StoredStamp(st), Is.Null, "nothing stored");
            var lineage = NewLineage(5);
            Assert.That(Save.WriteSave(st, new TrialSave(lineage), new Stamp(3, "abc")), Is.True);
            Assert.That(st.Read(Save.SaveKey), Does.StartWith("{\"v\":1,\"rev\":3,\"tab\":\"abc\","), "the stamp leads the text");
            SameStamp(Save.StoredStamp(st), 3, "abc");
            OpenedSave o = Save.OpenSave(st);
            SameStamp(o.Stamp, 3, "abc", "openSave hands the stamp it read");
            Assert.That(Serialize(o.Save.Lineage), Is.EqualTo(Serialize(lineage)), "the save itself reads as before");

            // a save written by an older build (no stamp) reads as rev 0, and so does text that does not load
            Save.WriteSave(st, new TrialSave(lineage));
            SameStamp(Save.StoredStamp(st), 0, "");
            st.Write(Save.SaveKey, "{\"v\":1,\"lin");
            SameStamp(Save.StoredStamp(st), 0, "");
            SameStamp(Quietly(() => Save.OpenSave(st)).Stamp, 0, "", "the unreadable text keeps a stamp too");

            // a tab id cannot break out of its string
            Save.WriteSave(st, new TrialSave(lineage), new Stamp(4, "a\"b\\c"));
            SameStamp(Save.StoredStamp(st), 4, "abc");
        }

        // js: test/app/stamp.test.mjs:49
        [Test]
        public void SameStamp_TheSameWrite_NothingStoredOnBothSides_AndEveryOtherCaseDiffers()
        {
            Assert.That(Save.SameStamp(null, null), Is.True);
            Assert.That(Save.SameStamp(new Stamp(2, "x"), new Stamp(2, "x")), Is.True);
            Assert.That(Save.SameStamp(new Stamp(2, "x"), new Stamp(2, "y")), Is.False, "two tabs that both wrote rev 2");
            Assert.That(Save.SameStamp(new Stamp(2, "x"), new Stamp(3, "x")), Is.False);
            Assert.That(Save.SameStamp(null, new Stamp(0, "")), Is.False, "a save appeared");
            Assert.That(Save.SameStamp(new Stamp(0, ""), null), Is.False, "the save went away");
            Assert.That(Save.StampOf(""), Is.Null);
            Assert.That(Save.StampOf(null), Is.Null);
        }

        // js: test/app/stamp.test.mjs:63
        // The two-tab story the stamps stop (b13-revive / b12-twotabs): tab B holds the save as it was before tab A
        // recorded a death. B's next write would bring the dead heir back and wipe the trace; the stamp check sees that
        // the stored save is no longer the one B read.
        [Test]
        public void AStalePage_CanTellThatAnotherTabWroteAfterIt()
        {
            var st = new MemoryKeyValueStore();
            var lineage = NewLineage(9);
            Save.WriteSave(st, new TrialSave(lineage), new Stamp(1, "A"));
            Stamp a = Save.OpenSave(st).Stamp;
            Stamp b = Save.OpenSave(st).Stamp;
            Assert.That(Save.SameStamp(Save.StoredStamp(st), a) && Save.SameStamp(Save.StoredStamp(st), b), Is.True, "both tabs are in step");
            // A writes (the death): A's own stamp moves on, B's no longer matches
            var next = new Stamp(a.Rev + 1, "A");
            Save.WriteSave(st, new TrialSave(lineage), next);
            Assert.That(Save.SameStamp(Save.StoredStamp(st), next), Is.True, "A is in step");
            Assert.That(Save.SameStamp(Save.StoredStamp(st), b), Is.False, "B is stale and must not write");
        }

        // ---- the C# side of the same check ----

        [Test]
        public void StoredStamp_ReadsOnlyTheHeadOfTheText()
        {
            var st = new HeadOnlyStore();
            st.Write(Save.SaveKey, "{\"v\":1,\"rev\":12,\"tab\":\"k3j9x0a2lq1b\",\"lineage\":{\"Version\":1}" + new string(' ', 100000) + "}");
            SameStamp(Save.StoredStamp(st), 12, "k3j9x0a2lq1b");
            Assert.That(st.HeadReads, Is.EqualTo(1));
            Assert.That(st.LastHeadChars, Is.EqualTo(Save.StampHeadChars));
        }

        [Test]
        public void StampOf_FollowsTheJsPattern()
        {
            SameStamp(Save.StampOf("{\"v\":1,\"rev\":0,\"tab\":\"\"}"), 0, "");
            SameStamp(Save.StampOf("{\"v\":2,\"rev\":5,\"tab\":\"x\"}"), 0, "", "another version's head is no stamp");
            SameStamp(Save.StampOf("{\"v\":1,\"tab\":\"x\",\"rev\":5}"), 0, "", "the head is read in its order only");
            SameStamp(Save.StampOf("{\"v\":1,\"rev\":٣,\"tab\":\"x\"}"), 0, "", "only ASCII digits (JS \\d)");
            SameStamp(Save.StampOf("{\"v\":1,\"rev\":7,\"tab\":\"日本\",\"lineage\":null}"), 7, "日本");
            SameStamp(Save.StampOf("{\"v\":1,\"rev\":7,\"tab\":\"a\\\"b\"}"), 0, "", "a tab with an escaped quote does not read");
            SameStamp(Save.StampOf("garbage"), 0, "");
        }

        [Test]
        public void TwoKeepers_TheOneThatFallsBehindGoesStale_AndWritesNothing()
        {
            var st = new MemoryKeyValueStore();
            var a = SaveKeeper.Open(st, "A");
            a.Save = new TrialSave(NewLineage(9));
            Assert.That(a.Persist(force: true), Is.True);
            SameStamp(a.Stamp, 1, "A");

            var b = SaveKeeper.Open(st, "B");
            SameStamp(b.Stamp, 1, "A", "B read A's write");
            int staleCalls = 0;
            b.BecameStale += () => staleCalls++;

            Assert.That(a.Persist(), Is.True, "A moves on");
            SameStamp(a.Stamp, 2, "A");
            string stored = st.Read(Save.SaveKey);

            Assert.That(Quietly(() => b.Persist()), Is.False, "B is stale and does not write");
            Assert.That(b.Stale, Is.True);
            Assert.That(staleCalls, Is.EqualTo(1));
            Assert.That(b.InSync(), Is.False);
            Assert.That(b.Persist(), Is.False);
            Assert.That(staleCalls, Is.EqualTo(1), "raised once");
            Assert.That(st.Read(Save.SaveKey), Is.EqualTo(stored), "A's save is untouched");

            Quietly(() => b.Reopen());
            Assert.That(b.Stale, Is.False);
            SameStamp(b.Stamp, 2, "A", "B holds A's save again");
            Assert.That(b.Persist(), Is.True, "and can write after it");
            SameStamp(b.Stamp, 3, "B");
            Assert.That(Quietly(() => a.InSync()), Is.False, "now A is behind");
        }

        [Test]
        public void Force_WritesOverAnotherWrite_StampedPastTheStoredRev()
        {
            var st = new MemoryKeyValueStore();
            Save.WriteSave(st, new TrialSave(NewLineage(1)), new Stamp(7, "other"));
            var k = new SaveKeeper(st, "me");   // never read the save: its stamp is null
            k.Save = new TrialSave(NewLineage(2));
            Assert.That(Quietly(() => k.Persist()), Is.False, "out of step");
            Assert.That(k.Stale, Is.True);

            var fresh = new SaveKeeper(st, "me");
            fresh.Save = new TrialSave(NewLineage(2));
            Assert.That(fresh.Persist(force: true), Is.True, "はじめから replaces the record");
            SameStamp(fresh.Stamp, 8, "me");
            SameStamp(Save.StoredStamp(st), 8, "me");
        }

        [Test]
        public void SyncIdle_TakesUpAnotherWriteWithoutAWord()
        {
            var st = new MemoryKeyValueStore();
            var title = SaveKeeper.Open(st, "T");
            Assert.That(title.HasSave, Is.False);
            var other = SaveKeeper.Open(st, "O");
            other.Save = new TrialSave(NewLineage(4));
            other.Persist(force: true);

            title.SyncIdle();
            Assert.That(title.HasSave, Is.True);
            Assert.That(title.Stale, Is.False);
            SameStamp(title.Stamp, 1, "O");
            Assert.That(Serialize(title.Save.Lineage), Is.EqualTo(Serialize(other.Save.Lineage)));
        }
    }
}
