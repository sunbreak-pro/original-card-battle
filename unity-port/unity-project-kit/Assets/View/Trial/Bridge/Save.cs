// Storage for the lineage, the life in progress and the settings (port of src/app/save.js). Every access is
// guarded: storage can be missing or throw, and the game must still run without it.
//
// Every write is stamped { rev, tab }: a counter and the id of the instance that wrote it. An instance keeps the
// stamp of the save it last read or wrote, and before it writes it checks that the stored stamp is still that one;
// if another instance of the game (a second player window, the Editor and a build on the same folder) has written
// since, this instance's save is stale and must not be written over the newer one (it would bring back a recorded
// death, wipe a trace, or let a taken trace be taken again: tier1 R1-16, INHERITANCE.md R-21). The stamp leads the
// stored text, so it is read without parsing the whole save (SaveKeeper does the checking).
//
// The JS functions take the page's localStorage; here they take the store (IKeyValueStore) as their first argument.
//
// Engine-free (no UnityEngine).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Depiction;
using InheritanceCore;

namespace Trial.Bridge
{
    /// <summary>The stamp of one write: { rev, tab }. Null stands for "nothing stored".</summary>
    public sealed class Stamp
    {
        public Stamp(long rev, string tab)
        {
            Rev = rev;
            Tab = tab ?? "";
        }

        /// <summary>The write's counter.</summary>
        public long Rev { get; }

        /// <summary>The id of the instance that wrote it.</summary>
        public string Tab { get; }

        public override string ToString() => "{ rev: " + Rev.ToString(CultureInfo.InvariantCulture) + ", tab: '" + Tab + "' }";
    }

    /// <summary>A stored save that did not load: why, and whether the copy aside was written (openSave's unreadable).</summary>
    public sealed class Unreadable
    {
        public Unreadable(Exception error, bool kept)
        {
            Error = error;
            Kept = kept;
        }

        public Exception Error { get; }
        public bool Kept { get; }
    }

    /// <summary>What <see cref="Save.OpenSave"/> read: { save, unreadable, stamp }.</summary>
    public sealed class OpenedSave
    {
        public OpenedSave(TrialSave save, Unreadable unreadable, Stamp stamp)
        {
            Save = save;
            Unreadable = unreadable;
            Stamp = stamp;
        }

        /// <summary>The save, or null when nothing is stored or it does not load.</summary>
        public TrialSave Save { get; }

        /// <summary>Null, or why the stored save did not load.</summary>
        public Unreadable Unreadable { get; }

        /// <summary>The stored text's stamp (<see cref="Save.StampOf"/>): null when nothing is stored.</summary>
        public Stamp Stamp { get; }
    }

    /// <summary>
    /// The settings (save.js DEFAULT_SETTINGS, app.md §3): one shared instance the scenes read live. Speed, ShortFx,
    /// Master, Effects, Ambience and Muted are stored; Reverb is the audio's and is never stored.
    /// </summary>
    public sealed class TrialSettings
    {
        public const double DefaultSpeed = 1.25;   // ×1.25 is Unity's default (BattleSpeed.Default)
        public const bool DefaultShortFx = false;   // 演出短縮
        public const double DefaultMaster = 0.8;
        public const double DefaultEffects = 0.9;
        public const double DefaultAmbience = 0.6;
        public const bool DefaultMuted = false;
        public const double DefaultReverb = 0.18;  // Audio.js SOUND_SETTINGS_DEFAULTS; not persisted

        /// <summary>The stored keys, in the JS order.</summary>
        public static readonly IReadOnlyList<string> Keys =
            Array.AsReadOnly(new[] { "speed", "shortFx", "master", "effects", "ambience", "muted" });

        /// <summary>The battle speed as the multiplier (1, 1.25 or 1.5; anything else reads as the default step).</summary>
        public double Speed { get; set; } = DefaultSpeed;
        public bool ShortFx { get; set; } = DefaultShortFx;
        public double Master { get; set; } = DefaultMaster;
        public double Effects { get; set; } = DefaultEffects;
        public double Ambience { get; set; } = DefaultAmbience;
        public bool Muted { get; set; } = DefaultMuted;

        /// <summary>The audio's reverb send (never stored).</summary>
        public double Reverb { get; set; } = DefaultReverb;

        /// <summary>Raised by <see cref="NotifyChanged"/> (the router, after it assigned a change).</summary>
        public event Action Changed;

        /// <summary>The step of <see cref="Speed"/>; setting it stores the step's multiplier.</summary>
        public BattleSpeedStep SpeedStep
        {
            get => StepOf(Speed);
            set => Speed = SpeedOf(value);
        }

        /// <summary>{ ...DEFAULT_SETTINGS }.</summary>
        public static TrialSettings Defaults() => new TrialSettings();

        // js: app/main.js:22
        /// <summary>{ ...DEFAULT_SETTINGS, ...loadSettings() }.</summary>
        public static TrialSettings Load(IKeyValueStore store)
        {
            var s = new TrialSettings();
            s.Apply(Save.LoadSettings(store));
            return s;
        }

        // js: scenes/meta/settings.js:10
        /// <summary>A stored speed → its step: a step's multiplier within 1e-6, else the default (×1.25).</summary>
        public static BattleSpeedStep StepOf(double speed)
        {
            foreach (BattleSpeedStep s in BattleSpeed.Steps)
            {
                if (Math.Abs(BattleSpeed.Multiplier(s) - speed) < 1e-6) return s;
            }
            return BattleSpeed.Default;
        }

        // js: scenes/battle/clock.js:25
        /// <summary>A step → the number Speed keeps (1, 1.25, 1.5).</summary>
        public static double SpeedOf(BattleSpeedStep step) =>
            step == BattleSpeedStep.Slow ? 1 : step == BattleSpeedStep.Fast ? 1.5 : 1.25;

        /// <summary>Object.assign of the stored keys that <paramref name="partial"/> holds with the default's type (loadSettings' result).</summary>
        public void Apply(JsonObject partial)
        {
            if (partial == null) return;
            if (partial.TryGet("speed", out object speed) && JsonNumber.TryDouble(speed, out double sp)) Speed = sp;
            if (partial.TryGet("shortFx", out object shortFx) && shortFx is bool fx) ShortFx = fx;
            if (partial.TryGet("master", out object master) && JsonNumber.TryDouble(master, out double m)) Master = m;
            if (partial.TryGet("effects", out object effects) && JsonNumber.TryDouble(effects, out double e)) Effects = e;
            if (partial.TryGet("ambience", out object ambience) && JsonNumber.TryDouble(ambience, out double a)) Ambience = a;
            if (partial.TryGet("muted", out object muted) && muted is bool mu) Muted = mu;
        }

        /// <summary>Object.assign(settings, other): every value, Reverb included.</summary>
        public void Assign(TrialSettings other)
        {
            if (other == null) return;
            Speed = other.Speed;
            ShortFx = other.ShortFx;
            Master = other.Master;
            Effects = other.Effects;
            Ambience = other.Ambience;
            Muted = other.Muted;
            Reverb = other.Reverb;
        }

        public TrialSettings Clone()
        {
            var c = new TrialSettings();
            c.Assign(this);
            return c;
        }

        public void NotifyChanged() => Changed?.Invoke();

        /// <summary>The stored keys and their values, in the JS order (what writeSettings stores).</summary>
        public JsonObject ToJson()
        {
            var o = new JsonObject();
            o["speed"] = Speed;
            o["shortFx"] = ShortFx;
            o["master"] = Master;
            o["effects"] = Effects;
            o["ambience"] = Ambience;
            o["muted"] = Muted;
            return o;
        }
    }

    public static class Save
    {
        public const string SaveKey = "ocb.save.v1";
        public const string SettingsKey = "ocb.settings.v1";

        /// <summary>A save whose life would not replay, kept before it is dropped.</summary>
        public const string BrokenKey = "ocb.save.v1.broken";

        public const string ProbeKey = "ocb.probe";

        /// <summary>出立's half-built deck (departure.js:62). The departure scene owns its content; the app owns the store.</summary>
        public const string DepartureDraftKey = "ocb.departure.draft.v1";

        /// <summary>How much of the stored text <see cref="StoredStamp"/> reads (the stamp leads the text).</summary>
        public const int StampHeadChars = 256;

        /// <summary>Where a save that does not load is reported (console.warn). Tests silence it.</summary>
        public static Action<string, Exception> Warn = (message, e) => Console.Error.WriteLine(message + (e == null ? "" : ": " + e.Message));

        // js: app/save.js:51 (STAMP_RE; [0-9] is JS's ASCII \d)
        private static readonly Regex StampRe = new Regex("^\\{\"v\":1,\"rev\":([0-9]+),\"tab\":\"([^\"\\\\]*)\"", RegexOptions.CultureInvariant);

        // js: app/save.js:54
        /// <summary>The stamp of a stored text: null when nothing is stored, { rev: 0, tab: '' } for a save written without one.</summary>
        public static Stamp StampOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            Match m = StampRe.Match(text);
            if (!m.Success) return new Stamp(0, "");
            if (!long.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long rev)) rev = long.MaxValue;
            return new Stamp(rev, m.Groups[2].Value);
        }

        // js: app/save.js:61
        /// <summary>The stamp of the save as it is stored now (<see cref="StampOf"/>), read from the head of the text only.</summary>
        public static Stamp StoredStamp(IKeyValueStore store) => StampOf(store.ReadHead(SaveKey, StampHeadChars));

        // js: app/save.js:64
        /// <summary>Whether two stamps name the same write (null = nothing stored).</summary>
        public static bool SameStamp(Stamp a, Stamp b)
        {
            if (a == null || b == null) return a == null && b == null;
            return a.Rev == b.Rev && a.Tab == b.Tab;
        }

        // js: app/save.js:78
        /// <summary>
        /// Reads the stored save. Nothing stored: all null. A save that does not load (another build's version, a write
        /// cut off half way) is not dropped: the text stays under its key until the player starts anew, and is first
        /// copied aside (<see cref="KeepBrokenSave"/>; Unreadable.Kept says whether the copy was written).
        /// </summary>
        public static OpenedSave OpenSave(IKeyValueStore store)
        {
            string text = store.Read(SaveKey);
            if (string.IsNullOrEmpty(text)) return new OpenedSave(null, null, null);
            Stamp stamp = StampOf(text);
            try
            {
                object data = Revive(Json.Parse(text));
                // writeSave never stores a save without its lineage: one without it is broken, not empty.
                if (!(data is JsonObject o) || !JsonNumber.Truthy(o["lineage"])) throw new SaveFormatException("bad save: no lineage");
                Lineage lineage = LineageCodec.Read(o["lineage"]);
                LifeSave life = LifeSave.FromJson(o["life"]);   // data.life || null
                return new OpenedSave(new TrialSave(lineage, life, o["deckSave"] as string), null, stamp);
            }
            catch (Exception e)
            {
                bool kept = KeepBrokenSave(store);
                Warn?.Invoke("save could not be read; it is " + (kept ? "kept under " + BrokenKey : "not copied (storage refused)") + " until the player starts anew", e);
                return new OpenedSave(null, new Unreadable(e, kept), stamp);
            }
        }

        // js: app/save.js:96
        /// <summary>The save, or null when nothing is saved or it does not load (OpenSave says which).</summary>
        public static TrialSave LoadSave(IKeyValueStore store) => OpenSave(store).Save;

        // js: app/save.js:99
        /// <summary>Stores the save, stamped with <paramref name="stamp"/> when given. Returns whether storage took it.</summary>
        public static bool WriteSave(IKeyValueStore store, TrialSave save, Stamp stamp = null)
        {
            if (save == null || save.Lineage == null) return false;
            string text;
            try { text = SaveText(save, stamp); }
            catch (Exception e)
            {
                Warn?.Invoke("the save could not be written as JSON", e);
                return false;
            }
            return store.Write(SaveKey, text);
        }

        /// <summary>
        /// The text <see cref="WriteSave"/> stores: {"v":1,"rev":…,"tab":"…","lineage":…,"life":…,"deckSave":…}
        /// (without rev and tab when there is no stamp). The tab loses its " and \ so it cannot break out of its string.
        /// </summary>
        public static string SaveText(TrialSave save, Stamp stamp = null)
        {
            if (save == null || save.Lineage == null) throw new ArgumentException("Save: a save without its lineage is not written.", nameof(save));
            var o = new JsonObject();
            o["v"] = 1;
            if (stamp != null)
            {
                o["rev"] = unchecked((int)stamp.Rev);   // JS stamp.rev | 0
                o["tab"] = stamp.Tab.Replace("\"", "").Replace("\\", "");
            }
            o["lineage"] = LineageCodec.Write(save.Lineage);
            o["life"] = save.Life?.ToJson();
            o["deckSave"] = string.IsNullOrEmpty(save.DeckSave) ? null : save.DeckSave;
            return Json.Stringify(o);
        }

        // js: app/save.js:107
        /// <summary>
        /// Whether the store keeps what the game writes (a probe written and removed). False when storage is blocked or
        /// missing: the game still plays, but nothing outlives the session.
        /// </summary>
        public static bool StorageWorks(IKeyValueStore store)
        {
            if (store == null || !store.Write(ProbeKey, "1")) return false;
            store.Remove(ProbeKey);
            return true;
        }

        // js: app/save.js:115
        /// <summary>
        /// Copies the save as it is stored now to a second key, so a life that no longer replays can be dropped without
        /// losing its record (a later build may read it again). Returns whether a copy was written.
        /// </summary>
        public static bool KeepBrokenSave(IKeyValueStore store)
        {
            string text = store.Read(SaveKey);
            return !string.IsNullOrEmpty(text) && store.Write(BrokenKey, text);
        }

        // js: app/save.js:121
        /// <summary>The copy KeepBrokenSave made, as stored text, or null.</summary>
        public static string ReadBrokenSave(IKeyValueStore store) => store.Read(BrokenKey);

        // js: app/save.js:123
        public static TrialSave ClearLife(IKeyValueStore store, TrialSave save)
        {
            if (save == null) return save;
            save.Life = null;
            WriteSave(store, save);
            return save;
        }

        // js: app/save.js:130
        public static void ClearAll(IKeyValueStore store) => store.Remove(SaveKey);

        // js: app/save.js:132
        /// <summary>
        /// The stored settings that can be used: each stored key whose value has the default's type (a number, or true /
        /// false). Nothing stored, or text that does not read, is an empty object.
        /// </summary>
        public static JsonObject LoadSettings(IKeyValueStore store)
        {
            var output = new JsonObject();
            string text = store.Read(SettingsKey);
            if (string.IsNullOrEmpty(text)) return output;
            try
            {
                if (!(Json.Parse(text) is JsonObject s)) return output;
                foreach (string k in TrialSettings.Keys)
                {
                    if (!s.TryGet(k, out object v)) continue;
                    bool wantsBool = k == "shortFx" || k == "muted";
                    if (wantsBool ? v is bool : Json.IsNumber(v)) output[k] = v;
                }
                return output;
            }
            catch (Exception) { return new JsonObject(); }
        }

        // js: app/save.js:143
        /// <summary>Stores exactly the settings' stored keys (never Reverb). Returns whether storage took it.</summary>
        public static bool WriteSettings(IKeyValueStore store, TrialSettings settings) =>
            settings != null && store.Write(SettingsKey, Json.Stringify(settings.ToJson()));

        // js: app/save.js:36
        /// <summary>
        /// save.js's reviver: the {"$big"}, {"$map"} and {"$set"} tags its replacer writes. A $big becomes its decimal
        /// string (the C# keeps seeds as strings), a $map an object (string keys) and a $set a list. The browser game
        /// stores none of them today (its seeds are strings already); read for safety.
        /// </summary>
        public static object Revive(object v)
        {
            switch (v)
            {
                case JsonObject o:
                {
                    var keys = new List<string>();
                    foreach (var pair in o) keys.Add(pair.Key);
                    foreach (string k in keys) o[k] = Revive(o[k]);
                    if (o.ContainsKey("$big")) return o["$big"] is string big ? big : (object)Convert.ToString(o["$big"], CultureInfo.InvariantCulture);
                    if (o.ContainsKey("$map") && o["$map"] is IList entries)
                    {
                        var map = new JsonObject();
                        foreach (object entry in entries)
                        {
                            if (entry is IList kv && kv.Count >= 2 && kv[0] is string key) map[key] = kv[1];
                            else return entries;
                        }
                        return map;
                    }
                    if (o.ContainsKey("$set") && o["$set"] is IList set) return set;
                    return o;
                }
                case List<object> list:
                    for (int i = 0; i < list.Count; i++) list[i] = Revive(list[i]);
                    return list;
                default:
                    return v;
            }
        }
    }
}
