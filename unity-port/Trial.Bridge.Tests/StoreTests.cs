// The stores under the save (save.js's guarded localStorage): one file per key with atomic writes, and the store in
// memory that tests and the noStorage switch use.
using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Trial.Bridge.Tests
{
    public class StoreTests
    {
        private string _folder;

        [SetUp]
        public void MakeFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "trial-store-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void DropFolder()
        {
            try { if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true); }
            catch (IOException) { /* a scanner holds it; the temp folder is the OS's */ }
        }

        [Test]
        public void TheJsKeys_MapToThePortsFileNames()
        {
            Assert.That(FileKeyValueStore.FileNameOf(Save.SaveKey), Is.EqualTo("save.v1.json"));
            Assert.That(FileKeyValueStore.FileNameOf(Save.BrokenKey), Is.EqualTo("save.v1.broken.json"));
            Assert.That(FileKeyValueStore.FileNameOf(Save.SettingsKey), Is.EqualTo("settings.v1.json"));
            Assert.That(FileKeyValueStore.FileNameOf(Save.DepartureDraftKey), Is.EqualTo("departure-draft.v1.json"));
            Assert.That(FileKeyValueStore.FileNameOf(Save.ProbeKey), Is.EqualTo("probe.tmp"));
            Assert.That(FileKeyValueStore.FileNameOf("my key/../x"), Is.EqualTo("my_key_.._x.json"), "any other key stays inside the folder");
        }

        [Test]
        public void AFileStore_WritesReadsAndRemoves_CreatingItsFolder()
        {
            var st = new FileKeyValueStore(_folder);
            Assert.That(st.Read(Save.SaveKey), Is.Null, "nothing stored (no folder yet)");
            Assert.That(st.ReadHead(Save.SaveKey, 10), Is.Null);
            Assert.That(st.Write(Save.SaveKey, "{\"v\":1}"), Is.True);
            Assert.That(Directory.Exists(_folder), Is.True);
            Assert.That(st.Read(Save.SaveKey), Is.EqualTo("{\"v\":1}"));

            Assert.That(st.Write(Save.SaveKey, "二度目の記録"), Is.True, "over an existing file");
            Assert.That(st.Read(Save.SaveKey), Is.EqualTo("二度目の記録"));
            byte[] bytes = File.ReadAllBytes(Path.Combine(_folder, "save.v1.json"));
            Assert.That(bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), "UTF-8 without a BOM");
            Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo("二度目の記録"));
            Assert.That(Directory.GetFiles(_folder).Select(Path.GetFileName), Is.EqualTo(new[] { "save.v1.json" }), "no tmp or backup is left behind");

            st.Remove(Save.SaveKey);
            Assert.That(st.Read(Save.SaveKey), Is.Null);
            st.Remove(Save.SaveKey);   // removing twice is nothing
            Assert.That(st.Write(Save.SaveKey, null), Is.False);
        }

        [Test]
        public void ReadHead_ReadsOnlyTheFirstCharacters()
        {
            var st = new FileKeyValueStore(_folder);
            string text = "{\"v\":1,\"rev\":3,\"tab\":\"日本語のタブ\"," + new string('x', 50000) + "}";
            st.Write(Save.SaveKey, text);
            Assert.That(st.ReadHead(Save.SaveKey, 30), Is.EqualTo(text.Substring(0, 30)));
            Assert.That(st.ReadHead(Save.SaveKey, 0), Is.EqualTo(""));
            st.Write("short", "ab");
            Assert.That(st.ReadHead("short", 10), Is.EqualTo("ab"));
            SameStampOf(st);
        }

        private static void SameStampOf(IKeyValueStore st)
        {
            Stamp s = Save.StoredStamp(st);
            Assert.That(s.Rev, Is.EqualTo(3));
            Assert.That(s.Tab, Is.EqualTo("日本語のタブ"));
        }

        [Test]
        public void AFileStore_ThatCannotWrite_SaysSo_AndAProbeTellsIt()
        {
            // a file where the folder should be: nothing can be created under it
            string blocker = _folder + ".file";
            File.WriteAllText(blocker, "x");
            try
            {
                var st = new FileKeyValueStore(Path.Combine(blocker, "trial"));
                Assert.That(st.Write(Save.SaveKey, "x"), Is.False);
                Assert.That(st.Read(Save.SaveKey), Is.Null);
                Assert.That(Save.StorageWorks(st), Is.False);
                st.Remove(Save.SaveKey);
            }
            finally { File.Delete(blocker); }

            var ok = new FileKeyValueStore(_folder);
            Assert.That(Save.StorageWorks(ok), Is.True);
            Assert.That(File.Exists(Path.Combine(_folder, "probe.tmp")), Is.False, "the probe is removed");
        }

        [Test]
        public void AFileStore_ReplacesALockedFile_ByCopyingWhenItCannotMoveIt()
        {
            var st = new FileKeyValueStore(_folder);
            st.Write(Save.SaveKey, "old");
            string path = Path.Combine(_folder, "save.v1.json");
            // A reader that shares read/write but not delete (an antivirus scanning the file): the replace fails, the copy works.
            using (var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bool written = st.Write(Save.SaveKey, "new");
                if (!written) Assert.Ignore("this file system refuses even the copy while the file is open");
            }
            Assert.That(st.Read(Save.SaveKey), Is.EqualTo("new"));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void ReadHead_OfNothingStored_IsNull_WhateverItsLength()
        {
            var st = new FileKeyValueStore(_folder);
            Assert.That(st.ReadHead(Save.SaveKey, 0), Is.Null, "null means nothing stored, also for a head of 0");
            Assert.That(new MemoryKeyValueStore().ReadHead(Save.SaveKey, 0), Is.Null);
            st.Write(Save.SaveKey, "x");
            Assert.That(st.ReadHead(Save.SaveKey, 0), Is.EqualTo(""));
            Assert.That(st.ReadHead(Save.SaveKey, -3), Is.EqualTo(""));
        }

        [Test]
        public void Reads_ShareTheFileWithAnotherInstanceThatHasItOpen()
        {
            var st = new FileKeyValueStore(_folder);
            st.Write(Save.SaveKey, "{\"v\":1,\"rev\":3,\"tab\":\"日本語のタブ\",\"lineage\":{}}");
            string path = Path.Combine(_folder, "save.v1.json");
            // another instance writing the file in place (the copy fallback): it shares read and write, not delete
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                Assert.That(st.Read(Save.SaveKey), Does.StartWith("{\"v\":1,\"rev\":3,"), "the whole text");
                SameStampOf(st);
            }
            // a handle that shares nothing: both tries fail, and the read says nothing is there (save.js read)
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(st.Read(Save.SaveKey), Is.Null);
                Assert.That(Save.StoredStamp(st), Is.Null);
            }
            SameStampOf(st);
        }

        // The stamp check across two instances on the same folder (an Editor play session and a built player).
        [Test]
        public void TwoKeepers_OnOneFolder_TheOneBehindGoesStale()
        {
            var a = SaveKeeper.Open(new FileKeyValueStore(_folder), "A");
            Assert.That(a.StorageOk, Is.True);
            a.Save = new TrialSave(SaveTestKit.NewLineage(9));
            Assert.That(a.Persist(force: true), Is.True);

            var b = SaveKeeper.Open(new FileKeyValueStore(_folder), "B");
            Assert.That(b.HasSave, Is.True);
            Assert.That(b.Stamp.Rev, Is.EqualTo(1));
            Assert.That(b.Persist(), Is.True, "B is in step and writes");

            SaveTestKit.Quietly(() =>
            {
                Assert.That(a.Persist(), Is.False, "A is behind: its write would go over B's");
                Assert.That(a.Stale, Is.True);
            });
            Assert.That(Save.StoredStamp(b.Store).Tab, Is.EqualTo("B"));
            Assert.That(Directory.GetFiles(_folder).Select(Path.GetFileName), Is.EqualTo(new[] { "save.v1.json" }), "nothing else is left behind");
        }

        [Test]
        public void TheMemoryStore_BlockedOrFull()
        {
            var st = new MemoryKeyValueStore();
            Assert.That(st.Write("a", "1"), Is.True);
            Assert.That(st.Write("b", "2"), Is.True);
            Assert.That(st.Keys, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(st.ReadHead("a", 0), Is.EqualTo(""));
            Assert.That(st.Writes, Is.EqualTo(2));

            st.RefuseWrites = true;
            Assert.That(st.Write("a", "3"), Is.False);
            Assert.That(st.Read("a"), Is.EqualTo("1"), "reads still work");
            st.Remove("b");
            Assert.That(st.Keys, Is.EqualTo(new[] { "a" }), "removes still work");

            st.RefuseWrites = false;
            st.Blocked = true;
            Assert.That(st.Read("a"), Is.Null);
            Assert.That(st.ReadHead("a", 5), Is.Null);
            Assert.That(st.Write("a", "4"), Is.False);
            st.Remove("a");
            st.Blocked = false;
            Assert.That(st.Read("a"), Is.EqualTo("1"), "a blocked store touches nothing");
        }
    }
}
