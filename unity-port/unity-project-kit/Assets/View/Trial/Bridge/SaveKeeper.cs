// One save, several instances (port of the data half of src/app/main.js:121-210): the save this instance holds,
// its stamp, and the write that refuses to go over a newer save another instance wrote (tier1 R1-16,
// INHERITANCE.md R-21). The router (Trial.Boot) keeps the screens; this keeps the data and says when the save went
// stale (BecameStale) or a write failed (SaveFailed), so the router can show the title and the notices.
//
// persist / persistSoon / inSync / markStale / syncIdle keep their JS names; reloadStored's data half is Reopen.
// persistSoon queues one write per turn of the microtask queue (Trial.Microtasks, as queueMicrotask in the JS);
// FlushIfDirty writes a queued one at once (application quit / pause).
//
// Engine-free (no UnityEngine), main thread only.
using System;
using System.Text;
using Saves = Trial.Bridge.Save;

namespace Trial.Bridge
{
    /// <summary>The life in progress as persist writes it (main.js `live`: start, log, memo, scribbles).</summary>
    public interface ILiveLife
    {
        LifeStartData Start { get; }

        /// <summary>The log the recorder appends to (written as it stands at each write).</summary>
        LifeLog Log { get; }

        /// <summary>The heir's memo sheet.</summary>
        string Memo { get; }

        /// <summary>The 走り書き the life carries, as JSON ([{ Generation, Name, Text }]).</summary>
        object ScribblesJson { get; }
    }

    public sealed class SaveKeeper
    {
        private bool _persistQueued;

        /// <param name="store">Where the save lives.</param>
        /// <param name="tab">This instance's id in the stamps; null makes one (<see cref="NewTab"/>).</param>
        public SaveKeeper(IKeyValueStore store, string tab = null)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
            Tab = string.IsNullOrEmpty(tab) ? NewTab() : tab;
        }

        // js: app/main.js:26
        /// <summary>The boot's half of main.js: the stored save opened (save, unreadable, stamp) and the storage probed.</summary>
        public static SaveKeeper Open(IKeyValueStore store, string tab = null)
        {
            var keeper = new SaveKeeper(store, tab);
            OpenedSave opened = Saves.OpenSave(store);
            keeper.Save = opened.Save;
            keeper.Unreadable = opened.Unreadable;
            keeper.Stamp = opened.Stamp;
            keeper.StorageOk = Saves.StorageWorks(store);
            return keeper;
        }

        public IKeyValueStore Store { get; }

        /// <summary>This instance's id in the stamps it writes.</summary>
        public string Tab { get; }

        /// <summary>The save this instance holds ({ lineage, life, deckSave }), or null.</summary>
        public TrialSave Save { get; set; }

        /// <summary>
        /// A stored save that did not load: it stays under its key, copied aside, until the player starts anew. While it
        /// is there the title says so and はじめから asks before writing over it. Null when none.
        /// </summary>
        public Unreadable Unreadable { get; set; }

        /// <summary>The stored save as this instance last read or wrote it.</summary>
        public Stamp Stamp { get; private set; }

        /// <summary>Another instance wrote after this one: nothing more is written until <see cref="Reopen"/>.</summary>
        public bool Stale { get; private set; }

        /// <summary>Storage keeps what is written (false: the notices say nothing outlives the session).</summary>
        public bool StorageOk { get; set; }

        /// <summary>The life in progress, or null (main.js `live`).</summary>
        public ILiveLife Live { get; set; }

        /// <summary>A write is queued by <see cref="PersistSoon"/> and has not run yet.</summary>
        public bool PersistQueued => _persistQueued;

        /// <summary>Whether a lineage is held (the title's hasSave).</summary>
        public bool HasSave => Save != null && Save.Lineage != null;

        /// <summary>
        /// Raised once when the save went stale (markStale). The router answers it as main.js does, a turn later
        /// (setTimeout 0): cancel the open confirm cards, <see cref="Reopen"/>, drop the live life, show the title and
        /// AppText.StaleText.
        /// </summary>
        public event Action BecameStale;

        /// <summary>Raised when a write fails after storage had worked (the router shows AppText.SaveFailedText once).</summary>
        public event Action SaveFailed;

        // js: app/main.js:128
        /// <summary>
        /// Writes the save, unless another instance has written since this one last read or wrote it: then this save is
        /// stale and is never written over the newer one (<see cref="MarkStale"/>). <paramref name="force"/> writes anyway
        /// (はじめから: the player chose to replace the record), stamped past the stored rev. With a live life the save's
        /// life is composed from it first. Returns whether the save was written.
        /// </summary>
        public bool Persist(bool force = false)
        {
            if (Save == null) return false;
            if (!force && !InSync()) return false;
            if (Live != null) Save.Life = LifeSave.Compose(Live.Start, Live.Log, Live.Memo, Live.ScribblesJson, Save.Life);
            Stamp stored = force ? Saves.StoredStamp(Store) : null;
            var next = new Stamp(Math.Max(Stamp != null ? Stamp.Rev : 0, stored != null ? stored.Rev : 0) + 1, Tab);
            bool ok = Saves.WriteSave(Store, Save, next);
            if (ok)
            {
                Stamp = next;
                StorageOk = true;
            }
            else if (StorageOk)
            {
                // It worked until now (storage full, or taken away): say so once, over whatever is on screen.
                StorageOk = false;
                Saves.Warn?.Invoke("the save could not be written", null);
                SaveFailed?.Invoke();
            }
            return ok;
        }

        // js: app/main.js:149
        /// <summary>
        /// A recorded call is saved once the code that made it has run: one write per turn of the microtask queue, so a
        /// fight in progress is in the save after every move (tier1 R1-16). Writes only while a life is live.
        /// </summary>
        public void PersistSoon()
        {
            if (_persistQueued) return;
            _persistQueued = true;
            Microtasks.Post(() =>
            {
                if (!_persistQueued) return;   // FlushIfDirty wrote it already
                _persistQueued = false;
                if (Live != null) Persist();
            });
        }

        /// <summary>Writes a write PersistSoon queued, now (application quit / pause / focus lost). Returns whether it wrote.</summary>
        public bool FlushIfDirty()
        {
            if (!_persistQueued) return false;
            _persistQueued = false;
            return Live != null && Persist();
        }

        // js: app/main.js:157
        /// <summary>Whether the stored save is still the one this instance last read or wrote; marks it stale when not.</summary>
        public bool InSync()
        {
            if (Stale) return false;
            if (Saves.SameStamp(Saves.StoredStamp(Store), Stamp)) return true;
            MarkStale();
            return false;
        }

        // js: app/main.js:165
        /// <summary>Another instance wrote the save: this one writes nothing more (<see cref="BecameStale"/> is raised once).</summary>
        public void MarkStale()
        {
            if (Stale) return;
            Stale = true;
            Saves.Warn?.Invoke("the save was written by another instance; this one reloads it", null);
            BecameStale?.Invoke();
        }

        // js: app/main.js:173 (the data half of reloadStored)
        /// <summary>Reads the stored save into this instance again: save, unreadable and stamp; not stale; no live life.</summary>
        public void Reopen()
        {
            OpenedSave o = Saves.OpenSave(Store);
            Save = o.Save;
            Unreadable = o.Unreadable;
            Stamp = o.Stamp;
            Stale = false;
            Live = null;
        }

        // js: app/main.js:189
        /// <summary>On the title (nothing of the save is on screen): take up a save another instance wrote, without a word.</summary>
        public void SyncIdle()
        {
            if (Saves.SameStamp(Saves.StoredStamp(Store), Stamp)) return;
            OpenedSave o = Saves.OpenSave(Store);
            Save = o.Save;
            Unreadable = o.Unreadable;
            Stamp = o.Stamp;
        }

        // js: app/main.js:35
        /// <summary>An instance id: 8 random base-36 characters and the last 4 of the time in base 36.</summary>
        public static string NewTab() => NewTab(new Random(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        /// <summary><see cref="NewTab()"/> with the random source and the time (ms since 1970) given.</summary>
        public static string NewTab(Random random, long nowMs)
        {
            var sb = new StringBuilder(12);
            for (int i = 0; i < 8; i++) sb.Append(Base36[random.Next(36)]);
            string time = ToBase36(nowMs);
            sb.Append(time.Length <= 4 ? time : time.Substring(time.Length - 4));
            return sb.ToString();
        }

        // js: app/main.js:225
        /// <summary>はじめから's lineage seed: (Date.now() ^ (Math.random() * 0x7fffffff)) >>> 0.</summary>
        public static uint NewLineageSeed(long nowMs, double random01) =>
            unchecked((uint)(ToInt32(nowMs) ^ ToInt32(Math.Floor(random01 * 0x7fffffff))));

        // js: app/main.js:263
        /// <summary>The defensive hall's lineage seed: Date.now() >>> 0.</summary>
        public static uint HallLineageSeed(long nowMs) => unchecked((uint)ToInt32(nowMs));

        private const string Base36 = "0123456789abcdefghijklmnopqrstuvwxyz";

        private static string ToBase36(long value)
        {
            if (value <= 0) return "0";
            var chars = new StringBuilder();
            while (value > 0)
            {
                chars.Insert(0, Base36[(int)(value % 36)]);
                value /= 36;
            }
            return chars.ToString();
        }

        /// <summary>JS ToInt32 of an integral number: its value modulo 2^32, as a signed 32-bit integer.</summary>
        private static int ToInt32(double integral) => unchecked((int)(long)(integral % 4294967296.0));

        private static int ToInt32(long value) => unchecked((int)value);
    }
}
