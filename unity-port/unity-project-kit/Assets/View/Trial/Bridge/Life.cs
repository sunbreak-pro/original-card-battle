// One life of the lineage as the ported cores see it (port of the browser trial's src/app/life.js, and the data half of
// main.js beginLife). The C# Expedition stops at a single run; the lineage rules are InheritanceCore's. This file is the
// seam between them:
//   - the life's start is plain data (seed, deck by instance, tools, journal) so a save can rebuild it (SaveData.cs
//     holds the shapes: LifeStartData, ChoiceData);
//   - each layer's map gets the lineage's trace node and drops the carving once it is known (Inheritance.MapSpecFor's
//     rule), frozen at the start of the life so a replay draws the same maps;
//   - what a trace gives (cards with their 段, pages, enemy 開示度) goes into the Expedition through Expedition.Inherit,
//     a call the recorder logs like any other (Recorded.cs).
//
// The JS wraps two of the Expedition's methods from here (life.js:205-213); C# cannot, so the Expedition does both
// itself: the pages the heir sets out with are ExpeditionOptions.StartPages (BeginLife counts them), and 「this life
// took its trace」, which the map hook reads, is Expedition.InheritedTraceId (set by Inherit, kept across a new life of
// the same Expedition, like the JS's life.traceTaken).
//
// Engine-free (no UnityEngine).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BattleCore;
using DungeonContent;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;

namespace Trial.Bridge
{
    /// <summary>
    /// The departure's choice as 出立 hands it (js: departure.js onSetOut's <c>{ deck, battleToolIds, consumableIds,
    /// loadout, talent }</c>, the shape life.js choiceToData reads): the deck by instance, the tools that act in battle,
    /// the exploration loadout — the DungeonContent Loadout of DepartureCarry.Carry (catalogue ItemDefs), or a RunLoadout
    /// kept as it is — the talent, and the ids 出立 picked.
    /// </summary>
    public sealed class DepartureChoice
    {
        public IReadOnlyList<CardInstance> Deck { get; set; } = Array.Empty<CardInstance>();
        public IReadOnlyList<string> BattleToolIds { get; set; } = Array.Empty<string>();

        /// <summary>The departure's Loadout (DepartureCarry.Carry's). Read when <see cref="RunLoadout"/> is null.</summary>
        public Loadout Loadout { get; set; }

        /// <summary>An exploration RunLoadout kept as it is (js: <c>choice.loadout instanceof RunLoadout</c>); null: from <see cref="Loadout"/>.</summary>
        public RunLoadout RunLoadout { get; set; }

        /// <summary>才能: a card id, or null.</summary>
        public string Talent { get; set; }

        /// <summary>Always empty today (the departure passes the consumables only).</summary>
        public IReadOnlyList<string> ToolIds { get; set; } = Array.Empty<string>();

        public IReadOnlyList<string> ConsumableIds { get; set; } = Array.Empty<string>();

        /// <summary>The deck string the life set out with (DeckBuilder.Save), for the save's deckSave; or null.</summary>
        public string DeckSave { get; set; }

        /// <summary>
        /// The choice a built Departure and its carry make (js: departure.js:712 — deck, battleToolIds, consumableIds,
        /// loadout, talent). <paramref name="deckSave"/> is the deck builder's Save().
        /// </summary>
        public static DepartureChoice Of(Departure departure, CarriedSupplies carry, string deckSave = null)
        {
            if (departure == null) throw new ArgumentNullException(nameof(departure));
            if (carry == null) throw new ArgumentNullException(nameof(carry));
            return new DepartureChoice
            {
                Deck = departure.Deck,
                BattleToolIds = carry.BattleToolIds,
                ConsumableIds = carry.ConsumableIds,
                Loadout = carry.Loadout,
                Talent = departure.TalentCardId,
                DeckSave = deckSave,
            };
        }
    }

    /// <summary>
    /// The life as lifeSetup's map hook sees it (js: life.js:180 <c>life = { traceTaken }</c>): whether this life took its
    /// trace, so the later layers of the same life draw no trace node (INHERITANCE.md R-09), in the first run and in the
    /// replay alike.
    /// </summary>
    public sealed class TrialLife
    {
        /// <summary>The life's Expedition (set once LifeSetup built it; null while the Expedition's constructor runs).</summary>
        public Expedition Expedition { get; internal set; }

        /// <summary>
        /// This life took its trace: Expedition.Inherit ran (its InheritedTraceId is set; a new life of the same
        /// Expedition keeps it, as the JS's flag stays). False while the Expedition is being built.
        /// </summary>
        public bool TraceTaken => Expedition != null && Expedition.InheritedTraceId != null;
    }

    /// <summary>What <see cref="Life.LifeSetup"/> builds: the Expedition, the life its map hook reads, and the bound intel.</summary>
    public sealed class LifeSetupResult
    {
        public LifeSetupResult(Expedition expedition, TrialLife life, JournalIntel intel)
        {
            Expedition = expedition;
            Life = life;
            Intel = intel;
        }

        public Expedition Expedition { get; }
        public TrialLife Life { get; }
        public JournalIntel Intel { get; }
    }

    /// <summary>addScribbles' result (js: life.js:159 <c>{ list, rows }</c>).</summary>
    public sealed class AddedScribbles
    {
        public AddedScribbles(List<Scribble> list, List<ScribbleRow> rows)
        {
            List = list;
            Rows = rows;
        }

        /// <summary>The 走り書き the life carries after the take: the held ones, then each new one.</summary>
        public List<Scribble> List { get; }

        /// <summary>Each 走り書き offered, and whether it was new to the life (for the 受け取った遺産 card).</summary>
        public List<ScribbleRow> Rows { get; }
    }

    /// <summary>
    /// The life in progress as the router keeps it (js: main.js:319 <c>live = { expedition, raw, log, start, life, memo,
    /// scribbles }</c>): what SaveKeeper.Persist writes (ILiveLife) and what the scenes act through.
    /// </summary>
    public sealed class LiveLife : ILiveLife
    {
        public LiveLife(RecordedExpedition expedition, TrialLife life, LifeStartData start, string memo, List<Scribble> scribbles)
        {
            Expedition = expedition ?? throw new ArgumentNullException(nameof(expedition));
            Life = life;
            Start = start;
            Memo = memo ?? "";
            Scribbles = scribbles ?? new List<Scribble>();
        }

        /// <summary>The recorded Expedition: every state-changing call the scenes make goes through it.</summary>
        public RecordedExpedition Expedition { get; }

        /// <summary>The raw Expedition, for reads.</summary>
        public Expedition Raw => Expedition.Raw;

        /// <summary>The log the recorder appends to.</summary>
        public LifeLog Log => Expedition.Log;

        public LifeStartData Start { get; }

        public TrialLife Life { get; }

        /// <summary>The heir's memo sheet.</summary>
        public string Memo { get; set; }

        /// <summary>The 走り書き the life carries (a trace adds to them: Life.AddScribbles).</summary>
        public List<Scribble> Scribbles { get; set; }

        /// <summary>The 走り書き as the save writes them ([{ Generation, Name, Text }]).</summary>
        public object ScribblesJson => global::Trial.Bridge.Life.ScribblesToJson(Scribbles);

        /// <summary>The fight the reload found in progress, recorded into this life's log (main.js resumeFight), or null.</summary>
        public RecordedSource OpenSource { get; internal set; }
    }

    public static class Life
    {
        // ------------------------------------------------------------------ the start as data

        /// <summary>
        /// The departure's choice as plain data: the deck by instance id and kind (instance ids and order are what the
        /// battles shuffle, so they are kept exactly), the tools that act in battle, the exploration loadout as relief
        /// items, and the talent. The departure hands a DungeonContent Loadout (catalogue ItemDefs, no relief Kind); the
        /// exploration reads it as the RunLoadout Loadouts.ToRunLoadout makes of it, with the life's tool slots
        /// (<paramref name="toolSlots"/>: 4 with the survivor's bonus). A RunLoadout is kept as it is.
        /// </summary>
        // js: app/life.js:30
        public static ChoiceData ChoiceToData(DepartureChoice choice, int? toolSlots = null)
        {
            if (choice == null) throw new ArgumentNullException(nameof(choice));
            RunLoadout lo = choice.RunLoadout
                ?? (choice.Loadout == null ? RunLoadout.Empty : Loadouts.ToRunLoadout(choice.Loadout, toolSlots));
            return new ChoiceData
            {
                Deck = (choice.Deck ?? Array.Empty<CardInstance>()).Select(c => new DeckRow(c.InstanceId, c.Def.Id)).ToList(),
                BattleToolIds = (choice.BattleToolIds ?? Array.Empty<string>()).ToList(),
                Loadout = Loadouts.RunLoadoutToData(lo),
                Talent = choice.Talent,
                ToolIds = (choice.ToolIds ?? Array.Empty<string>()).ToList(),
                ConsumableIds = (choice.ConsumableIds ?? Array.Empty<string>()).ToList(),
            };
        }

        // js: app/life.js:44
        private static List<CardInstance> DeckFromData(IReadOnlyList<DeckRow> rows) =>
            rows.Select(r => new CardInstance(r.InstanceId, CardCatalog.ById(r.DefId))).ToList();

        // js: app/life.js:47
        private static RunLoadout LoadoutFromData(object lo)
        {
            // a save without `trial` reads as the C#'s RunLoadout
            try
            {
                return Loadouts.RunLoadoutFromData(lo);
            }
            catch (ArgumentException e)
            {
                throw new SaveFormatException("bad save: the loadout (" + e.Message + ")", e);
            }
        }

        /// <summary>
        /// The saved start of a life, from Inheritance.StartLife's result and the departure's choice. Everything a replay
        /// needs and nothing that changes as the lineage moves on.
        /// </summary>
        // js: app/life.js:55
        public static LifeStartData MakeStart(LifeStart lifeStart, DepartureChoice choice)
        {
            if (lifeStart == null) throw new ArgumentNullException(nameof(lifeStart));
            return new LifeStartData
            {
                V = LifeStartData.Version,
                Generation = lifeStart.Life.Generation,
                Name = lifeStart.Life.Name,
                Seed = lifeStart.Seed.ToString(CultureInfo.InvariantCulture),
                ToolSlots = lifeStart.ToolSlots,
                DeckMax = lifeStart.DeckMax,
                SurvivorBonus = lifeStart.SurvivorBonus,
                TraceLayers = lifeStart.TraceLayers.ToList(),
                CarvingKnown = lifeStart.CarvingKnown,
                // The journal the heir carries (INHERITANCE.md R-13): a survivor's whole journal, its memo turned into
                // that survivor's 走り書き so the heir starts on a sheet of their own (Inheritance.HeirJournal).
                Journal = Inheritance.HeirJournal(lifeStart.Journal, lifeStart.JournalFrom).ToJson(),
                Choice = ChoiceToData(choice, lifeStart.ToolSlots),
            };
        }

        /// <summary>The rare pages the heir starts with (a survivor's whole journal, R-13), as the count Expedition keeps.</summary>
        // js: app/life.js:74
        public static int StartPages(LifeStartData start)
        {
            object p = start != null && start.Journal is JsonObject j ? j["Pages"] : null;
            if (p is IList list && !(p is string)) return list.Count;
            return JsonNumber.TryInt(p, out int n) && n > 0 ? n : 0;
        }

        /// <summary>
        /// The 走り書き of a life: the ones its start carries (a survivor's journal: start.journal.Scribbles) and the ones
        /// the life holds already (<paramref name="saved"/>: the save's list, which a taken trace adds to), each once (the
        /// same Generation and Text). A start saved before the memo became a 走り書き (an older build's MakeStart) still has
        /// the survivor's memo in start.journal.Memo: it is read as that survivor's 走り書き, named from the lineage, so a
        /// save made then does not lose it (R-13).
        /// </summary>
        // js: app/life.js:87
        public static List<Scribble> LifeScribbles(LifeStartData start, IEnumerable<Scribble> saved, Lineage lineage = null)
        {
            var output = new List<Scribble>();
            void Add(Scribble s)
            {
                if (s == null || s.Text == null) return;
                if (!output.Any(t => t.Generation == s.Generation && t.Text == s.Text)) output.Add(new Scribble(s.Generation, s.Name, s.Text));
            }

            var j = start?.Journal as JsonObject ?? new JsonObject();
            foreach (Scribble s in ScribblesFromJson(j["Scribbles"])) Add(s);
            if (j["Memo"] is string memo && !IsBlank(memo) && start.Generation.HasValue && start.Generation.Value > 1)
            {
                int gen = start.Generation.Value - 1;
                LifeRecord prev = lineage?.Lives.FirstOrDefault(l => l.Generation == gen);
                Add(new Scribble(gen, prev != null ? prev.Name : "", memo));
            }
            foreach (Scribble s in saved ?? Array.Empty<Scribble>()) Add(s);
            return output;
        }

        /// <summary><see cref="LifeScribbles(LifeStartData, IEnumerable{Scribble}, Lineage)"/> with the save's list as stored (JSON).</summary>
        // js: app/life.js:87
        public static List<Scribble> LifeScribbles(LifeStartData start, object savedJson, Lineage lineage = null) =>
            LifeScribbles(start, ScribblesFromJson(savedJson), lineage);

        // ------------------------------------------------------------------ inheritance into a running life

        /// <summary>
        /// The 走り書き of a life after a take: <paramref name="held"/> with each of <paramref name="added"/> that it does not
        /// carry yet (the same Generation and Text: LifeScribbles keeps each once). Rows say which of
        /// <paramref name="added"/> were new, for the 受け取った遺産 card (InheritResult.WithScribbles).
        /// </summary>
        // js: app/life.js:159
        public static AddedScribbles AddScribbles(IEnumerable<Scribble> held, IEnumerable<Scribble> added)
        {
            var list = (held ?? Array.Empty<Scribble>()).ToList();
            var rows = new List<ScribbleRow>();
            foreach (Scribble s in added ?? Array.Empty<Scribble>())
            {
                if (s == null || s.Text == null) continue;
                bool dup = list.Any(t => t.Generation == s.Generation && t.Text == s.Text);
                if (!dup) list.Add(new Scribble(s.Generation, s.Name, s.Text));
                rows.Add(new ScribbleRow(s.Generation, s.Name, s.Text, !dup));
            }
            return new AddedScribbles(list, rows);
        }

        // ------------------------------------------------------------------ the life

        /// <summary>
        /// Builds the Expedition of a life from its saved start: the deck by instance, the tools, the talent, the
        /// loadout, the journal's 開示度, the rare pages the heir set out with, the trial rules, the life's deck limits (a
        /// survivor's heir may hold 45 cards; a life without the bonus: none, the C#'s), a JournalIntel bound to it, and
        /// the map hook: the carving dropped once known, the trace node on the lineage's trace layers until this life has
        /// taken its trace (so a trace taken on layer 2 keeps the trace node off layers 3〜7 of the same life, R-09).
        /// Throws <see cref="SaveFormatException"/> (or the cores' own exceptions) on a start that does not build.
        /// </summary>
        // js: app/life.js:178
        public static LifeSetupResult LifeSetup(LifeStartData start)
        {
            if (start == null) throw new ArgumentNullException(nameof(start));
            ChoiceData c = start.Choice ?? throw new SaveFormatException("bad save: the start has no choice");
            var life = new TrialLife();
            var traceLayers = new HashSet<int>(start.TraceLayers ?? new List<int>());
            bool carvingKnown = start.CarvingKnown;
            LayerMap MapFor(LayerProfile profile, ulong seed)
            {
                LayerMapSpec spec = profile.MapSpec;
                if (carvingKnown && spec.Quotas.ContainsKey(NodeKind.Carving)) spec = spec.With(NodeKind.Carving, 0);
                if (!life.TraceTaken && traceLayers.Contains(profile.Layer)) spec = spec.With(NodeKind.Trace, 1);
                return MapGenerator.Generate(spec, seed);
            }
            var runStart = new RunStart { Seed = start.SeedValue, Loadout = LoadoutFromData(c.Loadout), MapFor = MapFor };
            // The journal the heir set out with (INHERITANCE.md R-13: a survivor's whole journal): its 開示度 go to the
            // Expedition's journal, its rare pages to the Expedition's page count (ExpeditionOptions.StartPages).
            Dictionary<string, int> journal = DisclosureOf(start.Journal);
            var intel = new JournalIntel();
            // The life's deck limits (docs/EXTENSIONS.md 「出立の枠」: a survivor's heir may hold 45 cards), rebuilt from
            // the saved start so a replay holds the deck to the same cap. A life without the bonus: the C#'s, no option.
            var limits = new DeckLimits(deckMax: start.DeckMax, toolSlots: start.ToolSlots);
            // The game plays with the document rules the C# has not got yet (docs/EXTENSIONS.md: items, events, the
            // survey): trial on. Tests and the parity oracle build Expeditions without it and get the C#.
            var options = new ExpeditionOptions
            {
                Trial = true,
                Limits = limits.IsDefault ? null : limits,
                // The pages the heir set out with count among the life's pages from the start, also when the Expedition
                // begins its life again; they are inherited, not found (InheritedPages, INHERITANCE.md R-27).
                StartPages = StartPages(start),
            };
            var expedition = new Expedition(runStart, DeckFromData(c.Deck), c.BattleToolIds, c.Talent, intel, journal, options);
            intel.Bind(expedition);
            life.Expedition = expedition;
            return new LifeSetupResult(expedition, life, intel);
        }

        /// <summary>
        /// The data half of the router's beginLife (js: main.js:306): builds the life from its start and replays its log.
        /// A fight the log left in progress comes back where it stood (LiveLife.OpenSource, recorded into the same log);
        /// only a fight whose calls no longer replay (a save from another build) starts over, with a warning (Save.Warn).
        /// A log that fails before its last fight throws as it is (the router's 「生を再開できません」). The live life
        /// records every later call into the replayed log; <paramref name="onCall"/> runs after each (the router's
        /// PersistSoon).
        /// </summary>
        // js: app/main.js:306
        public static LiveLife BeginLife(LifeStartData start, IReadOnlyList<LogEntry> log, string memo = "", object scribbles = null,
            Lineage lineage = null, Action<string, string> onCall = null, ReplayTable table = null)
        {
            if (start == null) throw new ArgumentNullException(nameof(start));
            log = log ?? new LifeLog();
            LifeSetupResult setup = LifeSetup(start);
            ReplayResult res;
            try
            {
                res = Recorder.ReplayLife(setup.Expedition, log, table);
            }
            catch (Exception e)
            {
                LifeLog trimmed = LifeLog.TrimUnfinished(log);
                if (trimmed.Count == log.Count) throw;
                Save.Warn?.Invoke("the fight in progress did not replay; it starts over", e);
                setup = LifeSetup(start);
                res = Recorder.ReplayLife(setup.Expedition, trimmed, table);
            }
            RecordedExpedition rec = Recorder.RecordExpedition(setup.Expedition, res.Kept, onCall);
            var live = new LiveLife(rec, setup.Life, start, memo ?? "", LifeScribbles(start, scribbles, lineage));
            if (res.Open != null) live.OpenSource = rec.WrapSource(res.Open.Source);
            return live;
        }

        // ------------------------------------------------------------------ 走り書き as the save holds them

        /// <summary>The 走り書き as JSON values ([{ Generation, Name, Text }]), as the save stores them.</summary>
        public static List<object> ScribblesToJson(IEnumerable<Scribble> scribbles) =>
            (scribbles ?? Array.Empty<Scribble>()).Where(s => s != null).Select(s => (object)s.ToJson()).ToList();

        /// <summary>
        /// 走り書き read back leniently, as life.js reads them: a list of objects, each kept when its Text is a string
        /// (Generation an integer, else 0; Name a string, else null). Anything else is no 走り書き. A list of Scribble is
        /// taken as it is.
        /// </summary>
        public static List<Scribble> ScribblesFromJson(object json)
        {
            var list = new List<Scribble>();
            if (json is IEnumerable<Scribble> typed)
            {
                list.AddRange(typed.Where(s => s != null && s.Text != null));
                return list;
            }
            if (!(json is IList items) || json is string) return list;
            foreach (object item in items)
            {
                if (!(item is JsonObject o) || !(o["Text"] is string text)) continue;
                int generation = JsonNumber.TryInt(o["Generation"], out int g) ? g : 0;
                list.Add(new Scribble(generation, o["Name"] as string, text));
            }
            return list;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>start.journal.Disclosure as the Expedition's journal (ids in the stored order); none: empty.</summary>
        private static Dictionary<string, int> DisclosureOf(object journal)
        {
            var levels = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!(journal is JsonObject j) || !(j["Disclosure"] is JsonObject disclosure)) return levels;
            foreach (KeyValuePair<string, object> pair in disclosure)
            {
                if (!JsonNumber.TryInt(pair.Value, out int level))
                    throw new SaveFormatException("bad save: the journal's 開示度 of " + pair.Key);
                levels[pair.Key] = level;
            }
            return levels;
        }

        /// <summary>JS <c>!s.trim()</c>: only JS WhiteSpace and LineTerminator characters (InheritanceCore's JsText.IsBlank).</summary>
        private static bool IsBlank(string s)
        {
            foreach (char c in s)
            {
                int code = c;
                bool space = code == 0x09 || code == 0x0A || code == 0x0B || code == 0x0C || code == 0x0D || code == 0x20 ||
                    code == 0xA0 || code == 0x1680 || code == 0x2028 || code == 0x2029 || code == 0x202F || code == 0x205F ||
                    code == 0x3000 || code == 0xFEFF || (code >= 0x2000 && code <= 0x200A);
                if (!space) return false;
            }
            return true;
        }
    }
}
