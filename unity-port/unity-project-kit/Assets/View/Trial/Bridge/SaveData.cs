// The shapes the trial's save holds (src/app/save.js, main.js, life.js makeStart), as JSON values with
// JS key order, so a save the C# writes reads like the browser's and a browser save reads here:
//
//   {"v":1,"rev":12,"tab":"…","lineage":{…},"life":null | {"start":{…},"log":[…],"memo":"…","scribbles":[…],"before":{…}},"deckSave":null | "…"}
//
// - TrialSave: the lineage (the inheritance module's, InheritanceCore), the life in progress, the last set-out deck.
// - LifeSave: the life in progress as stored. It stays JSON (main.js keeps save.life as the plain object it read):
//   a life that does not load must not make the whole save unreadable, it fails when it is resumed (ReadStart /
//   ReadLog throw SaveFormatException there, inside the router's try, as lifeSetup / replayLife throw in the JS).
// - LifeStartData / ChoiceData: the life's start (life.js makeStart). The journal and the loadout stay JSON: they
//   are the inheritance module's and the loadout port's to read.
//
// Engine-free (no UnityEngine).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using InheritanceCore;

namespace Trial.Bridge
{
    /// <summary>A save, or a part of one, that does not have the shape the game writes (JS Error('bad save: …')).</summary>
    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message) : base(message) { }
        public SaveFormatException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>The stored save: { lineage, life, deckSave } (main.js `save`). Mutable, as the router moves it on.</summary>
    public sealed class TrialSave
    {
        public TrialSave(Lineage lineage, LifeSave life = null, string deckSave = null)
        {
            Lineage = lineage;
            Life = life;
            DeckSave = deckSave;
        }

        /// <summary>The lineage. A save without one is not written (WriteSave) and does not load (OpenSave).</summary>
        public Lineage Lineage { get; set; }

        /// <summary>The life in progress, or null.</summary>
        public LifeSave Life { get; set; }

        /// <summary>The deck the last life set out with (DeckBuilder.Save's string), handed to 出立 as savedDeck; or null.</summary>
        public string DeckSave { get; set; }
    }

    /// <summary>
    /// save.life: { start, log, memo, scribbles, before } as stored. Read back leniently (anything is kept as it was
    /// stored); the typed parts are read with <see cref="ReadStart"/> / <see cref="ReadLog"/>, which throw on a bad shape.
    /// </summary>
    public sealed class LifeSave
    {
        private readonly JsonObject _json;
        private readonly object _raw;   // a stored life that is not an object (kept, written back as it was)

        private LifeSave(JsonObject json, object raw)
        {
            _json = json;
            _raw = raw;
        }

        // js: app/main.js:131
        /// <summary>
        /// The life as persist writes it: { start, log, memo, scribbles, before }. `before` is the one the stored life
        /// held (main.js `before: save.life &amp;&amp; save.life.before`): null when there was no life, left out when that
        /// life had none.
        /// </summary>
        /// <param name="start">The life's start.</param>
        /// <param name="log">The log as it stands (written as it is now: a snapshot).</param>
        /// <param name="memo">The heir's memo sheet.</param>
        /// <param name="scribbles">The 走り書き the life carries, as JSON ([{Generation, Name, Text}]).</param>
        /// <param name="previous">The life the save held until now (save.life), or null.</param>
        public static LifeSave Compose(LifeStartData start, LifeLog log, string memo, object scribbles, LifeSave previous)
        {
            var o = new JsonObject();
            o["start"] = start?.ToJson();
            o["log"] = log == null ? new List<object>() : log.ToJson();
            o["memo"] = memo ?? "";
            o["scribbles"] = scribbles;
            if (previous == null) o["before"] = null;
            else if (previous.Has("before")) o["before"] = previous.Before;
            return new LifeSave(o, null);
        }

        // js: app/main.js:290
        /// <summary>The life as 出立 sets it before it begins: { before } (the lineage JSON as it stood before 出立).</summary>
        public static LifeSave Departing(object before)
        {
            var o = new JsonObject();
            o["before"] = before;
            return new LifeSave(o, null);
        }

        /// <summary>
        /// A stored life, kept as it was; null when there is none (save.js: data.life || null — null, false, 0 and ""
        /// are no life). Never throws (a broken life fails when it is resumed).
        /// </summary>
        public static LifeSave FromJson(object value)
        {
            if (!JsonNumber.Truthy(value)) return null;
            if (value is JsonObject o) return new LifeSave(o, null);
            return new LifeSave(new JsonObject(), value);
        }

        /// <summary>Whether the stored life has this key.</summary>
        public bool Has(string key) => _raw == null && _json.ContainsKey(key);

        /// <summary>The lineage as it stood before 出立 (for 「あきらめる」), as JSON; null when there is none.</summary>
        public object Before => _json["before"];

        /// <summary>The memo sheet (main.js beginLife: memo || ''); "" when it is not a string.</summary>
        public string Memo => _json["memo"] as string ?? "";

        /// <summary>The 走り書き the life held, as stored (life.js lifeScribbles reads it leniently); null when none.</summary>
        public object Scribbles => _json["scribbles"];

        /// <summary>The stored start, as JSON (null when none).</summary>
        public object StartJson => _json["start"];

        /// <summary>The life's start. Throws <see cref="SaveFormatException"/> when there is none or it has a bad shape.</summary>
        public LifeStartData ReadStart() => LifeStartData.FromJson(_raw != null ? null : _json["start"]);

        /// <summary>The life's log (resumeLife: l.log || []). Throws <see cref="SaveFormatException"/> on a bad shape.</summary>
        public LifeLog ReadLog() => LifeLog.FromJson(_raw != null ? null : _json["log"]);

        /// <summary>The JSON value the save writes (the object it was read as, or composed).</summary>
        public object ToJson() => _raw ?? _json;
    }

    /// <summary>One card of the deck by instance: [instanceId, defId] (instance ids and order drive the battle shuffles).</summary>
    public readonly struct DeckRow
    {
        public DeckRow(string instanceId, string defId)
        {
            InstanceId = instanceId;
            DefId = defId;
        }

        public string InstanceId { get; }
        public string DefId { get; }

        public override string ToString() => InstanceId + "/" + DefId;
    }

    /// <summary>
    /// The departure's choice as plain data (life.js choiceToData): the deck by instance, the tools that act in battle,
    /// the exploration loadout (JSON: { tools: [[id, kind, amount]], consumables: […], slots, trial }), the talent, and
    /// the ids 出立 picked.
    /// </summary>
    public sealed class ChoiceData
    {
        public List<DeckRow> Deck { get; set; } = new List<DeckRow>();
        public List<string> BattleToolIds { get; set; } = new List<string>();

        /// <summary>runLoadoutToData's object, as JSON (null when none).</summary>
        public object Loadout { get; set; }

        /// <summary>才能: a card id, or null.</summary>
        public string Talent { get; set; }

        public List<string> ToolIds { get; set; } = new List<string>();
        public List<string> ConsumableIds { get; set; } = new List<string>();

        // js: app/life.js:30 (the shape)
        /// <summary>{ deck, battleToolIds, loadout, talent, toolIds, consumableIds } in the JS order.</summary>
        public JsonObject ToJson()
        {
            var deck = new List<object>(Deck.Count);
            foreach (DeckRow row in Deck) deck.Add(new List<object> { row.InstanceId, row.DefId });
            var o = new JsonObject();
            o["deck"] = deck;
            o["battleToolIds"] = new List<object>(BattleToolIds ?? new List<string>());
            o["loadout"] = Loadout;
            o["talent"] = Talent;
            o["toolIds"] = new List<object>(ToolIds ?? new List<string>());
            o["consumableIds"] = new List<object>(ConsumableIds ?? new List<string>());
            return o;
        }

        /// <summary>A stored choice. Throws <see cref="SaveFormatException"/> when it is not an object or its deck is not a list of [instanceId, defId].</summary>
        public static ChoiceData FromJson(object json)
        {
            if (!(json is JsonObject o)) throw new SaveFormatException("bad save: the start has no choice");
            var c = new ChoiceData();
            if (!(o["deck"] is IList deck) || o["deck"] is string) throw new SaveFormatException("bad save: the choice has no deck");
            for (int i = 0; i < deck.Count; i++)
            {
                if (!(deck[i] is IList row) || row.Count < 2 || !(row[0] is string iid) || !(row[1] is string defId))
                    throw new SaveFormatException("bad save: deck row " + i.ToString(CultureInfo.InvariantCulture));
                c.Deck.Add(new DeckRow(iid, defId));
            }
            c.BattleToolIds = Strings(o["battleToolIds"], "battleToolIds");
            c.Loadout = o["loadout"];
            object talent = o["talent"];
            if (talent != null && !(talent is string)) throw new SaveFormatException("bad save: the talent");
            c.Talent = (string)talent;
            c.ToolIds = Strings(o["toolIds"], "toolIds");
            c.ConsumableIds = Strings(o["consumableIds"], "consumableIds");
            return c;
        }

        /// <summary>A list of strings (JS [...(x || [])]): null reads as empty.</summary>
        internal static List<string> Strings(object json, string what)
        {
            var list = new List<string>();
            if (json == null) return list;
            if (!(json is IList items) || json is string) throw new SaveFormatException("bad save: " + what);
            foreach (object item in items)
            {
                if (!(item is string s)) throw new SaveFormatException("bad save: " + what);
                list.Add(s);
            }
            return list;
        }
    }

    /// <summary>
    /// The saved start of a life (life.js makeStart): everything a replay needs and nothing that changes as the lineage
    /// moves on. Numbers that the JS reads with a default when they are missing are nullable here.
    /// </summary>
    public sealed class LifeStartData
    {
        public const int Version = 1;

        public int V { get; set; } = Version;
        public int? Generation { get; set; }
        public string Name { get; set; } = "";

        /// <summary>The life's seed as a decimal string (JS String(BigInt)).</summary>
        public string Seed { get; set; } = "0";

        public int? ToolSlots { get; set; }
        public int? DeckMax { get; set; }
        public bool SurvivorBonus { get; set; }
        public List<int> TraceLayers { get; set; } = new List<int>();
        public bool CarvingKnown { get; set; }

        /// <summary>The journal the heir carries (Inheritance.heirJournal's object, as JSON: { Disclosure, Pages, Memo, Scribbles }).</summary>
        public object Journal { get; set; }

        public ChoiceData Choice { get; set; } = new ChoiceData();

        /// <summary>The seed as the number the cores take (JS BigInt(start.seed)). Throws <see cref="SaveFormatException"/> when it is not a decimal u64.</summary>
        public ulong SeedValue
        {
            get
            {
                if (Seed != null && Seed.Length > 0 && ulong.TryParse(Seed, NumberStyles.None, CultureInfo.InvariantCulture, out ulong s)) return s;
                throw new SaveFormatException("bad save: the seed " + (Seed ?? "null"));
            }
        }

        // js: app/life.js:55 (the shape)
        /// <summary>{ v, generation, name, seed, toolSlots, deckMax, survivorBonus, traceLayers, carvingKnown, journal, choice } in the JS order.</summary>
        public JsonObject ToJson()
        {
            var o = new JsonObject();
            o["v"] = V;
            if (Generation.HasValue) o["generation"] = Generation.Value;
            o["name"] = Name;
            o["seed"] = Seed;
            if (ToolSlots.HasValue) o["toolSlots"] = ToolSlots.Value;
            if (DeckMax.HasValue) o["deckMax"] = DeckMax.Value;
            o["survivorBonus"] = SurvivorBonus;
            var layers = new List<object>(TraceLayers?.Count ?? 0);
            if (TraceLayers != null) foreach (int l in TraceLayers) layers.Add(l);
            o["traceLayers"] = layers;
            o["carvingKnown"] = CarvingKnown;
            o["journal"] = Journal;
            o["choice"] = (Choice ?? new ChoiceData()).ToJson();
            return o;
        }

        /// <summary>
        /// A stored start. Throws <see cref="SaveFormatException"/> when there is none, the seed is not a decimal u64 or
        /// the choice is not there; the rest reads as the JS reads it (a missing list is empty, a missing flag false).
        /// </summary>
        public static LifeStartData FromJson(object json)
        {
            if (!(json is JsonObject o)) throw new SaveFormatException("bad save: the life has no start");
            var s = new LifeStartData();
            if (JsonNumber.TryInt(o["v"], out int v)) s.V = v;
            s.Generation = OptionalInt(o, "generation");
            s.Name = o["name"] as string ?? "";
            s.Seed = SeedText(o["seed"]);
            _ = s.SeedValue;   // throws on a bad seed
            s.ToolSlots = OptionalInt(o, "toolSlots");
            s.DeckMax = OptionalInt(o, "deckMax");
            s.SurvivorBonus = JsonNumber.Truthy(o["survivorBonus"]);
            object layers = o["traceLayers"];
            if (layers != null)
            {
                if (!(layers is IList list) || layers is string) throw new SaveFormatException("bad save: traceLayers");
                foreach (object l in list)
                {
                    if (!JsonNumber.TryInt(l, out int layer)) throw new SaveFormatException("bad save: traceLayers");
                    s.TraceLayers.Add(layer);
                }
            }
            s.CarvingKnown = JsonNumber.Truthy(o["carvingKnown"]);
            s.Journal = o["journal"];
            s.Choice = ChoiceData.FromJson(o["choice"]);
            return s;
        }

        private static int? OptionalInt(JsonObject o, string key)
        {
            object v = o[key];
            if (v == null) return null;
            if (JsonNumber.TryInt(v, out int i)) return i;
            throw new SaveFormatException("bad save: " + key);
        }

        /// <summary>A seed as stored: a decimal string, an integer, or the {"$big": "…"} tag of save.js's replacer.</summary>
        private static string SeedText(object v)
        {
            switch (v)
            {
                case string text: return text;
                case JsonObject big when big["$big"] is string b: return b;
            }
            if (JsonNumber.TryLong(v, out long l) && l >= 0) return l.ToString(CultureInfo.InvariantCulture);
            throw new SaveFormatException("bad save: the life has no seed");
        }
    }

    /// <summary>The lineage's JSON, through the inheritance module (Inheritance.toJSON / fromJSON, which validates).</summary>
    internal static class LineageCodec
    {
        internal static Lineage Read(object json) => Inheritance.FromJson(json);

        internal static object Write(Lineage lineage) => Inheritance.ToJson(lineage);
    }
}
