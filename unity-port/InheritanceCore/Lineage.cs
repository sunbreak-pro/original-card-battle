#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using BattleCore;
using DungeonCore;

// The lineage as immutable C# types: the JS keeps it as plain, deep-frozen JSON objects
// (core/inheritance/Inheritance.js). Every type here has readonly properties and returns a new copy
// from its With-methods; lists are read-only views. ToJson() writes each in the JS's key order, so a
// lineage serialises byte for byte as JSON.stringify writes the JS lineage (LineageJson.cs).
namespace InheritanceCore
{
    /// <summary>Raised when a caller asks for something the inheritance rules do not allow.</summary>
    // js: core/inheritance/Inheritance.js:17
    public sealed class InheritanceRuleException : Exception
    {
        public InheritanceRuleException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// How a life ended (dungeon_exploration_v4.md §6.9). The values are RunPhase's member names, so a
    /// finished ExplorationState's Phase maps straight onto one.
    /// </summary>
    // js: core/inheritance/Inheritance.js:28
    public enum LifeEnding
    {
        MiasmaDeath,
        Fallen,
        Completed,
        Survived,
    }

    /// <summary>What a trace holds (concept-v3.md §8.3 / §14, tier1-core.md R1-10 / R1-12).</summary>
    // js: core/inheritance/Inheritance.js:31
    public enum CandidateKind
    {
        Card,
        EnemyPage,
        DungeonPage,
        Scribble,
    }

    /// <summary>The player's own knowledge, kept across lives (R-01, R-18, R-20).</summary>
    public sealed class PlayerKnowledge
    {
        public PlayerKnowledge(bool carvingTaken = false, bool rootDefeated = false)
        {
            CarvingTaken = carvingTaken;
            RootDefeated = rootDefeated;
        }

        /// <summary>The layer-two carving was read: layer 2 shows 竜神の秘跡 from now on.</summary>
        public bool CarvingTaken { get; }

        /// <summary>歪みの根 was defeated once (the out-of-game unlock of 根の裂け目).</summary>
        public bool RootDefeated { get; }

        public PlayerKnowledge With(bool? carvingTaken = null, bool? rootDefeated = null) =>
            new PlayerKnowledge(carvingTaken ?? CarvingTaken, rootDefeated ?? RootDefeated);

        public JsonObject ToJson() => new JsonObject
        {
            ["CarvingTaken"] = CarvingTaken,
            ["RootDefeated"] = RootDefeated,
        };
    }

    /// <summary>
    /// One life of the lineage: open (Current, no Ending) or closed (in Lives). Seed is the life's run seed
    /// as a decimal string (a ulong does not fit a JS number).
    /// </summary>
    public sealed class LifeRecord
    {
        public LifeRecord(
            int generation,
            string name,
            int tier,
            string seed,
            bool survivorBonus,
            int? survivorFrom,
            bool inheritedJournal,
            string? takenTraceId,
            LifeEnding? ending,
            int? layer,
            int? nodeId,
            NodeKind? nodeKind,
            string? traceId,
            int? collectedBy,
            int? replacedSurvivor)
        {
            Generation = generation;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Tier = tier;
            Seed = seed ?? throw new ArgumentNullException(nameof(seed));
            SurvivorBonus = survivorBonus;
            SurvivorFrom = survivorFrom;
            InheritedJournal = inheritedJournal;
            TakenTraceId = takenTraceId;
            Ending = ending;
            Layer = layer;
            NodeId = nodeId;
            NodeKind = nodeKind;
            TraceId = traceId;
            CollectedBy = collectedBy;
            ReplacedSurvivor = replacedSurvivor;
        }

        public int Generation { get; }
        public string Name { get; }
        /// <summary>The 装束 tier (AttireTiers).</summary>
        public int Tier { get; }
        public string Seed { get; }
        /// <summary>This life carried the 生存者ボーナス.</summary>
        public bool SurvivorBonus { get; }
        /// <summary>The survivor whose bonus this life carried, or null.</summary>
        public int? SurvivorFrom { get; }
        /// <summary>The life set out with something in its journal (a survivor's journal).</summary>
        public bool InheritedJournal { get; }
        /// <summary>The trace this life took (one per life), or null.</summary>
        public string? TakenTraceId { get; }
        public LifeEnding? Ending { get; }
        public int? Layer { get; }
        public int? NodeId { get; }
        public NodeKind? NodeKind { get; }
        /// <summary>The trace this life left, or null.</summary>
        public string? TraceId { get; }
        /// <summary>The generation that took this life's trace, or null while it is untaken.</summary>
        public int? CollectedBy { get; }
        /// <summary>The survivor this life replaced when it closed alive, or null.</summary>
        public int? ReplacedSurvivor { get; }

        public LifeRecord WithTakenTraceId(string? takenTraceId) => new LifeRecord(Generation, Name, Tier, Seed, SurvivorBonus,
            SurvivorFrom, InheritedJournal, takenTraceId, Ending, Layer, NodeId, NodeKind, TraceId, CollectedBy, ReplacedSurvivor);

        public LifeRecord WithCollectedBy(int? collectedBy) => new LifeRecord(Generation, Name, Tier, Seed, SurvivorBonus,
            SurvivorFrom, InheritedJournal, TakenTraceId, Ending, Layer, NodeId, NodeKind, TraceId, collectedBy, ReplacedSurvivor);

        public LifeRecord WithReplacedSurvivor(int? replacedSurvivor) => new LifeRecord(Generation, Name, Tier, Seed, SurvivorBonus,
            SurvivorFrom, InheritedJournal, TakenTraceId, Ending, Layer, NodeId, NodeKind, TraceId, CollectedBy, replacedSurvivor);

        /// <summary>The life closed: its ending and place of the end, and the trace it left (or null).</summary>
        public LifeRecord Closed(LifeEnding ending, int layer, int? nodeId, NodeKind? nodeKind, string? traceId) =>
            new LifeRecord(Generation, Name, Tier, Seed, SurvivorBonus, SurvivorFrom, InheritedJournal, TakenTraceId,
                ending, layer, nodeId, nodeKind, traceId, CollectedBy, ReplacedSurvivor);

        public JsonObject ToJson() => new JsonObject
        {
            ["Generation"] = Generation,
            ["Name"] = Name,
            ["Tier"] = Tier,
            ["Seed"] = Seed,
            ["SurvivorBonus"] = SurvivorBonus,
            ["SurvivorFrom"] = SurvivorFrom,
            ["InheritedJournal"] = InheritedJournal,
            ["TakenTraceId"] = TakenTraceId,
            ["Ending"] = JsonName.Of(Ending),
            ["Layer"] = Layer,
            ["NodeId"] = NodeId,
            ["NodeKind"] = JsonName.Of(NodeKind),
            ["TraceId"] = TraceId,
            ["CollectedBy"] = CollectedBy,
            ["ReplacedSurvivor"] = ReplacedSurvivor,
        };
    }

    /// <summary>
    /// One thing a trace holds. The fields that matter depend on the kind: a card (CardId, Rank, Ticks), an
    /// enemy page (EnemyId, Disclosure), a dungeon page (PageId, Text), a 走り書き (Generation, From, Text).
    /// </summary>
    public sealed class Candidate
    {
        public Candidate(
            string id,
            CandidateKind kind,
            string name,
            string? cardId = null,
            int? rank = null,
            int? ticks = null,
            string? enemyId = null,
            int? disclosure = null,
            string? pageId = null,
            string? text = null,
            int? generation = null,
            string? from = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Kind = kind;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            CardId = cardId;
            Rank = rank;
            Ticks = ticks;
            EnemyId = enemyId;
            Disclosure = disclosure;
            PageId = pageId;
            Text = text;
            Generation = generation;
            From = from;
        }

        public static Candidate Card(string cardId, int rank, int ticks, string name) =>
            new Candidate("card:" + cardId, CandidateKind.Card, name, cardId: cardId, rank: rank, ticks: ticks);

        public static Candidate EnemyPage(string enemyId, int disclosure, string name) =>
            new Candidate("enemy:" + enemyId, CandidateKind.EnemyPage, name, enemyId: enemyId, disclosure: disclosure);

        public static Candidate DungeonPage(string pageId, string name, string? text) =>
            new Candidate("page:" + pageId, CandidateKind.DungeonPage, name, pageId: pageId, text: text);

        public static Candidate Scribble(string id, int generation, string from, string text, string name) =>
            new Candidate(id, CandidateKind.Scribble, name, text: text, generation: generation, from: from);

        public string Id { get; }
        public CandidateKind Kind { get; }
        public string Name { get; }
        public string? CardId { get; }
        public int? Rank { get; }
        public int? Ticks { get; }
        public string? EnemyId { get; }
        public int? Disclosure { get; }
        public string? PageId { get; }
        public string? Text { get; }
        public int? Generation { get; }
        public string? From { get; }

        /// <summary>The candidate as the JS writes it: Id, Kind, then the kind's fields in its order.</summary>
        public JsonObject ToJson()
        {
            var o = new JsonObject { ["Id"] = Id, ["Kind"] = JsonName.Of(Kind) };
            switch (Kind)
            {
                case CandidateKind.Card:
                    SetIf(o, "CardId", CardId);
                    SetIf(o, "Rank", Rank);
                    SetIf(o, "Ticks", Ticks);
                    o["Name"] = Name;
                    break;
                case CandidateKind.EnemyPage:
                    SetIf(o, "EnemyId", EnemyId);
                    SetIf(o, "Disclosure", Disclosure);
                    o["Name"] = Name;
                    break;
                case CandidateKind.DungeonPage:
                    SetIf(o, "PageId", PageId);
                    o["Name"] = Name;
                    SetIf(o, "Text", Text);
                    break;
                default:
                    SetIf(o, "Generation", Generation);
                    SetIf(o, "From", From);
                    SetIf(o, "Text", Text);
                    o["Name"] = Name;
                    break;
            }
            return o;
        }

        // An absent field stays absent (JSON.stringify drops undefined).
        private static void SetIf(JsonObject o, string key, object? value)
        {
            if (value != null) o[key] = value;
        }
    }

    /// <summary>A card the shallow-death rule kept out of a trace (R-05), so the hall and the ending can say why.</summary>
    public sealed class WithheldCard
    {
        public WithheldCard(string cardId, int rank, string name)
        {
            CardId = cardId ?? throw new ArgumentNullException(nameof(cardId));
            Rank = rank;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string CardId { get; }
        public int Rank { get; }
        public string Name { get; }

        public JsonObject ToJson() => new JsonObject { ["CardId"] = CardId, ["Rank"] = Rank, ["Name"] = Name };
    }

    /// <summary>死亡地点の痕跡: what a closed life left at its place of death (or, cleared, on layer 7).</summary>
    public sealed class Trace
    {
        public Trace(
            string id,
            int generation,
            string name,
            int layer,
            int? nodeId,
            NodeKind? nodeKind,
            LifeEnding ending,
            bool cleared,
            IEnumerable<Candidate> candidates,
            IEnumerable<WithheldCard> withheld)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Generation = generation;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Layer = layer;
            NodeId = nodeId;
            NodeKind = nodeKind;
            Ending = ending;
            Cleared = cleared;
            Candidates = Frozen.List(candidates);
            Withheld = Frozen.List(withheld);
        }

        public string Id { get; }
        public int Generation { get; }
        public string Name { get; }
        public int Layer { get; }
        public int? NodeId { get; }
        public NodeKind? NodeKind { get; }
        public LifeEnding Ending { get; }
        /// <summary>The trace of a life that defeated 歪みの根 (cards only, 試作で決めたこと 10).</summary>
        public bool Cleared { get; }
        public IReadOnlyList<Candidate> Candidates { get; }
        public IReadOnlyList<WithheldCard> Withheld { get; }

        public JsonObject ToJson() => new JsonObject
        {
            ["Id"] = Id,
            ["Generation"] = Generation,
            ["Name"] = Name,
            ["Layer"] = Layer,
            ["NodeId"] = NodeId,
            ["NodeKind"] = JsonName.Of(NodeKind),
            ["Ending"] = JsonName.Of(Ending),
            ["Cleared"] = Cleared,
            ["Candidates"] = Candidates.Select(c => (object?)c.ToJson()).ToList(),
            ["Withheld"] = Withheld.Select(w => (object?)w.ToJson()).ToList(),
        };
    }

    /// <summary>A rare dungeon page: id, title, and what is written on it (none has any yet, #107).</summary>
    public sealed class JournalPage
    {
        /// <param name="title">Null in a journal handed in: normalising titles it 「希少な頁 n」 by its place.</param>
        public JournalPage(string? id, string? title = null, string? text = null)
        {
            Id = id;
            Title = title;
            Text = text;
        }

        public string? Id { get; }
        public string? Title { get; }
        public string? Text { get; }

        public JsonObject ToJson()
        {
            var o = new JsonObject { ["Id"] = Id, ["Title"] = Title };
            if (Text != null) o["Text"] = Text;
            return o;
        }
    }

    /// <summary>A 走り書き: a memo of an earlier heir, carried in the journal under that heir's name.</summary>
    public sealed class Scribble
    {
        public Scribble(int generation, string? name, string? text)
        {
            Generation = generation;
            Name = name;
            Text = text;
        }

        public int Generation { get; }
        public string? Name { get; }
        public string? Text { get; }

        public JsonObject ToJson() => new JsonObject { ["Generation"] = Generation, ["Name"] = Name, ["Text"] = Text };
    }

    /// <summary>
    /// A journal as this module keeps it: 開示度 by enemy id, the rare pages, the memo and the 走り書き inherited
    /// from earlier heirs. The page of terms and statuses is not part of it (tier1 R1-10). A journal handed in
    /// may be loose (titles missing, 開示度 0, any order); <see cref="Inheritance.NormalizeJournal"/> gives the
    /// kept form. <see cref="CarvingTaken"/> is an input only (the life read the carving: recordEnd records it
    /// on the player); a normalised journal never carries it and its JSON has no such key.
    /// </summary>
    public sealed class Journal
    {
        public Journal(
            IEnumerable<KeyValuePair<string, int>>? disclosure = null,
            IEnumerable<JournalPage>? pages = null,
            string? memo = null,
            IEnumerable<Scribble>? scribbles = null,
            bool carvingTaken = false)
        {
            Disclosure = new OrderedMap<int>(disclosure ?? Array.Empty<KeyValuePair<string, int>>());
            Pages = Frozen.List(pages ?? Array.Empty<JournalPage>());
            Memo = memo ?? "";
            Scribbles = Frozen.List(scribbles ?? Array.Empty<Scribble>());
            CarvingTaken = carvingTaken;
        }

        /// <summary>
        /// A journal whose rare pages are a count (what Expedition.Pages gives): pages page-1 … page-n titled
        /// 「希少な頁 n」. A negative count is refused, as the JS refuses it.
        /// </summary>
        public static Journal WithPageCount(
            int pageCount,
            IEnumerable<KeyValuePair<string, int>>? disclosure = null,
            string? memo = null,
            IEnumerable<Scribble>? scribbles = null,
            bool carvingTaken = false)
        {
            Inheritance.RequireInt(pageCount, "page count", 0);
            var pages = Enumerable.Range(1, pageCount).Select(i => new JournalPage("page-" + i, InheritanceText.RarePageTitle(i)));
            return new Journal(disclosure, pages, memo, scribbles, carvingTaken);
        }

        /// <summary>A journal whose rare pages are given by id only (titled 「希少な頁 n」 by their place).</summary>
        public static Journal WithPageIds(
            IEnumerable<string> pageIds,
            IEnumerable<KeyValuePair<string, int>>? disclosure = null,
            string? memo = null,
            IEnumerable<Scribble>? scribbles = null,
            bool carvingTaken = false) =>
            new Journal(disclosure, pageIds.Select(id => new JournalPage(id)), memo, scribbles, carvingTaken);

        /// <summary>開示度 by enemy id, in JS object order (array-index-like ids first, then as set).</summary>
        public IReadOnlyDictionary<string, int> Disclosure { get; }
        public IReadOnlyList<JournalPage> Pages { get; }
        public string Memo { get; }
        public IReadOnlyList<Scribble> Scribbles { get; }
        public bool CarvingTaken { get; }

        /// <summary>The journal as the JS writes it: Disclosure, Pages, Memo, Scribbles.</summary>
        public JsonObject ToJson()
        {
            var disclosure = new JsonObject();
            foreach (var pair in Disclosure) disclosure[pair.Key] = pair.Value;
            return new JsonObject
            {
                ["Disclosure"] = disclosure,
                ["Pages"] = Pages.Select(p => (object?)p.ToJson()).ToList(),
                ["Memo"] = Memo,
                ["Scribbles"] = Scribbles.Select(s => (object?)s.ToJson()).ToList(),
            };
        }
    }

    /// <summary>The one survivor (R-13〜R-17): whose journal passes once and whose bonus lasts BonusLivesLeft lives.</summary>
    public sealed class Survivor
    {
        public Survivor(int generation, string name, int bonusLivesLeft, int bonusLives, Journal? journal)
        {
            Generation = generation;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            BonusLivesLeft = bonusLivesLeft;
            BonusLives = bonusLives;
            Journal = journal;
        }

        public int Generation { get; }
        public string Name { get; }
        public int BonusLivesLeft { get; }
        public int BonusLives { get; }
        /// <summary>The whole journal, until the next life has taken it (then null).</summary>
        public Journal? Journal { get; }

        public Survivor With(int bonusLivesLeft, Journal? journal) =>
            new Survivor(Generation, Name, bonusLivesLeft, BonusLives, journal);

        public JsonObject ToJson() => new JsonObject
        {
            ["Generation"] = Generation,
            ["Name"] = Name,
            ["BonusLivesLeft"] = BonusLivesLeft,
            ["BonusLives"] = BonusLives,
            ["Journal"] = Journal?.ToJson(),
        };
    }

    /// <summary>
    /// A lineage (R-01): the closed lives, the life in progress, the untaken traces, the one survivor and the
    /// player's own knowledge. No lives counter, no reset. Seed is a decimal string (it draws the heirs' names).
    /// </summary>
    public sealed class Lineage
    {
        public Lineage(
            int version,
            string seed,
            IEnumerable<LifeRecord> lives,
            LifeRecord? current,
            IEnumerable<Trace> traces,
            Survivor? survivor,
            PlayerKnowledge player)
        {
            Version = version;
            Seed = seed ?? throw new ArgumentNullException(nameof(seed));
            Lives = Frozen.List(lives);
            Current = current;
            Traces = Frozen.List(traces);
            Survivor = survivor;
            Player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public int Version { get; }
        public string Seed { get; }
        public IReadOnlyList<LifeRecord> Lives { get; }
        public LifeRecord? Current { get; }
        public IReadOnlyList<Trace> Traces { get; }
        public Survivor? Survivor { get; }
        public PlayerKnowledge Player { get; }

        public Lineage WithLives(IEnumerable<LifeRecord> lives) => new Lineage(Version, Seed, lives, Current, Traces, Survivor, Player);
        public Lineage WithCurrent(LifeRecord? current) => new Lineage(Version, Seed, Lives, current, Traces, Survivor, Player);
        public Lineage WithTraces(IEnumerable<Trace> traces) => new Lineage(Version, Seed, Lives, Current, traces, Survivor, Player);
        public Lineage WithSurvivor(Survivor? survivor) => new Lineage(Version, Seed, Lives, Current, Traces, survivor, Player);
        public Lineage WithPlayer(PlayerKnowledge player) => new Lineage(Version, Seed, Lives, Current, Traces, Survivor, player);

        /// <summary>The lineage as the JS writes it (Inheritance.toJSON): a fresh JSON value.</summary>
        public JsonObject ToJson() => new JsonObject
        {
            ["Version"] = Version,
            ["Seed"] = Seed,
            ["Lives"] = Lives.Select(l => (object?)l.ToJson()).ToList(),
            ["Current"] = Current?.ToJson(),
            ["Traces"] = Traces.Select(t => (object?)t.ToJson()).ToList(),
            ["Survivor"] = Survivor?.ToJson(),
            ["Player"] = Player.ToJson(),
        };
    }

    /// <summary>An heir by generation and name: the writer of a survivor's memo (startLife's JournalFrom).</summary>
    public sealed class HeirRef
    {
        public HeirRef(int generation, string? name)
        {
            Generation = generation;
            Name = name;
        }

        public int Generation { get; }
        public string? Name { get; }

        public JsonObject ToJson() => new JsonObject { ["Generation"] = Generation, ["Name"] = Name };
    }

    /// <summary>The heir who sets out next (or is out now): generation, name, 装束 tier.</summary>
    public sealed class Heir
    {
        public Heir(int generation, string name, int tier)
        {
            Generation = generation;
            Name = name;
            Tier = tier;
        }

        public int Generation { get; }
        public string Name { get; }
        public int Tier { get; }
    }

    /// <summary>One 装束 tier (試作で決めたこと 2): names of the three parts and their colours (MaskColor null on tier 0).</summary>
    public sealed class AttireTier
    {
        public AttireTier(int tier, string name, string mask, string lining, string clasp, string? maskColor, string liningColor, string claspColor)
        {
            Tier = tier;
            Name = name;
            Mask = mask;
            Lining = lining;
            Clasp = clasp;
            MaskColor = maskColor;
            LiningColor = liningColor;
            ClaspColor = claspColor;
        }

        public int Tier { get; }
        public string Name { get; }
        public string Mask { get; }
        public string Lining { get; }
        public string Clasp { get; }
        public string? MaskColor { get; }
        public string LiningColor { get; }
        public string ClaspColor { get; }
    }

    /// <summary>
    /// What startLife gives 出立: the new lineage, the life, the run seed, the slots and deck bounds (生存者ボーナス:
    /// ToolSlots 4, DeckMax 45), an empty RunLoadout with the right slot count, the journal the heir starts with
    /// (a survivor's whole journal or nothing) and who wrote its memo, the layers that hold a trace, whether the
    /// carving is known, and the 装束.
    /// </summary>
    public sealed class LifeStart
    {
        public LifeStart(
            Lineage lineage,
            LifeRecord life,
            ulong seed,
            int toolSlots,
            int consumableSlots,
            int deckMin,
            int deckMax,
            RunLoadout loadout,
            Journal journal,
            HeirRef? journalFrom,
            bool survivorBonus,
            IEnumerable<int> traceLayers,
            bool carvingKnown,
            AttireTier attire)
        {
            Lineage = lineage;
            Life = life;
            Seed = seed;
            ToolSlots = toolSlots;
            ConsumableSlots = consumableSlots;
            DeckMin = deckMin;
            DeckMax = deckMax;
            Loadout = loadout;
            Journal = journal;
            JournalFrom = journalFrom;
            SurvivorBonus = survivorBonus;
            TraceLayers = Frozen.List(traceLayers);
            CarvingKnown = carvingKnown;
            Attire = attire;
        }

        public Lineage Lineage { get; }
        public LifeRecord Life { get; }
        public ulong Seed { get; }
        public int ToolSlots { get; }
        public int ConsumableSlots { get; }
        public int DeckMin { get; }
        public int DeckMax { get; }
        public RunLoadout Loadout { get; }
        public Journal Journal { get; }
        /// <summary>Who wrote the journal's memo (the survivor it came from), for HeirJournal; null with no journal.</summary>
        public HeirRef? JournalFrom { get; }
        public bool SurvivorBonus { get; }
        public IReadOnlyList<int> TraceLayers { get; }
        public bool CarvingKnown { get; }
        public AttireTier Attire { get; }
    }

    /// <summary>Where and how a life ended: layer 1..7, the node, its kind, and the RunPhase that ended it.</summary>
    public sealed class LifePlace
    {
        public LifePlace(int layer, int? nodeId = null, NodeKind? nodeKind = null, RunPhase? ending = null)
        {
            Layer = layer;
            NodeId = nodeId;
            NodeKind = nodeKind;
            Ending = ending;
        }

        public int Layer { get; }
        public int? NodeId { get; }
        public NodeKind? NodeKind { get; }
        /// <summary>Null: recordDeath reads it as MiasmaDeath, recordSurvival as Survived, recordEnd refuses it.</summary>
        public RunPhase? Ending { get; }
    }

    /// <summary>A card taken from a trace: its id, 段, the ticks of that 段's threshold, and its stored name.</summary>
    public sealed class TakenCard
    {
        public TakenCard(string cardId, int rank, int ticks, string? name)
        {
            CardId = cardId ?? throw new ArgumentNullException(nameof(cardId));
            Rank = rank;
            Ticks = ticks;
            Name = name;
        }

        public string CardId { get; }
        public int Rank { get; }
        public int Ticks { get; }
        public string? Name { get; }

        public JsonObject ToJson() => new JsonObject { ["CardId"] = CardId, ["Rank"] = Rank, ["Ticks"] = Ticks, ["Name"] = Name };
    }

    /// <summary>
    /// What a take gave (takeTrace's Taken): the trace, the cards with their 段 (to seed the mastery book and the
    /// cards owned), the pages and 走り書き as journal additions (merge with MergeJournal), and how many candidates
    /// were lost.
    /// </summary>
    public sealed class TakenLegacy
    {
        public TakenLegacy(string traceId, IEnumerable<TakenCard> cards, Journal journal, int lost)
        {
            TraceId = traceId;
            Cards = Frozen.List(cards);
            Journal = journal;
            Lost = lost;
        }

        public string TraceId { get; }
        public IReadOnlyList<TakenCard> Cards { get; }
        public Journal Journal { get; }
        public int Lost { get; }

        /// <summary>The take as the JS writes it ({ TraceId, Cards, Journal, Lost }): what the recorder logs for Inherit.</summary>
        public JsonObject ToJson() => new JsonObject
        {
            ["TraceId"] = TraceId,
            ["Cards"] = Cards.Select(c => (object?)c.ToJson()).ToList(),
            ["Journal"] = Journal.ToJson(),
            ["Lost"] = Lost,
        };

        /// <summary>A take back from its JSON (a logged Inherit's argument). The journal is normalised.</summary>
        public static TakenLegacy FromJson(object? json) => LineageJson.TakenFromJson(json);
    }

    /// <summary>takeTrace's result: the lineage after the take and what was taken.</summary>
    public sealed class TakeResult
    {
        public TakeResult(Lineage lineage, TakenLegacy taken)
        {
            Lineage = lineage;
            Taken = taken;
        }

        public Lineage Lineage { get; }
        public TakenLegacy Taken { get; }
    }

    /// <summary>
    /// One card of a life's mastery handed in as a list entry (the JS's { CardId, Rank, Name?, Thresholds? }):
    /// a string converts to a card at 段 0 (歪みの根's cards in RootCards).
    /// </summary>
    public sealed class CardEntry
    {
        public CardEntry(string? cardId, int rank = 0, string? name = null, IReadOnlyList<int>? thresholds = null)
        {
            CardId = cardId;
            Rank = rank;
            Name = name;
            Thresholds = thresholds;
        }

        public string? CardId { get; }
        public int Rank { get; }
        public string? Name { get; }
        public IReadOnlyList<int>? Thresholds { get; }

        public static implicit operator CardEntry(string cardId) => new CardEntry(cardId);
    }

    /// <summary>
    /// A life's mastery as recordDeath / recordSurvival read it: a MasteryBook (its Records; ProfileOf gives the
    /// thresholds and the names at each 段) or a list of <see cref="CardEntry"/>.
    /// </summary>
    public sealed class LifeDeck
    {
        public LifeDeck(MasteryBook book)
        {
            Book = book ?? throw new ArgumentNullException(nameof(book));
        }

        public LifeDeck(IEnumerable<CardEntry> entries)
        {
            Entries = Frozen.List(entries ?? throw new ArgumentNullException(nameof(entries)));
        }

        public MasteryBook? Book { get; }
        public IReadOnlyList<CardEntry>? Entries { get; }

        // A null book or list stays null: masteredCards(null) is no cards, as in the JS.
        public static implicit operator LifeDeck?(MasteryBook? book) => book == null ? null : new LifeDeck(book);
        public static implicit operator LifeDeck?(CardEntry[]? entries) => entries == null ? null : new LifeDeck(entries);
        public static implicit operator LifeDeck?(List<CardEntry>? entries) => entries == null ? null : new LifeDeck(entries);
    }

    /// <summary>
    /// recordDeath / recordSurvival / recordEnd options: display names stored in the trace (so the hall needs no
    /// catalogue), 歪みの根's cards for a cleared life (R-18), and a won extra boss (R-17).
    /// </summary>
    public sealed class EndOptions
    {
        /// <summary>The card's name at a 段 (null falls back to the profile's name, then the id).</summary>
        public Func<string, int, string?>? CardName { get; set; }

        /// <summary>The enemy's name (null falls back to the id).</summary>
        public Func<string, string?>? EnemyName { get; set; }

        /// <summary>歪みの根's cards, left on the cleared trace at any 段 (試作で決めたこと 10).</summary>
        public IReadOnlyList<CardEntry>? RootCards { get; set; }

        /// <summary>A won extra boss stretches the survivor bonus to two lives.</summary>
        public bool ExtraBossWon { get; set; }
    }

    /// <summary>An enum as its JSON value: its name (the JS's enums are their names), null for none.</summary>
    internal static class JsonName
    {
        public static string? Of(Enum? value) => value?.ToString();
    }

    /// <summary>Read-only list views (the JS deep-freezes every lineage).</summary>
    internal static class Frozen
    {
        public static IReadOnlyList<T> List<T>(IEnumerable<T> items) => new ReadOnlyCollection<T>(items.ToArray());
    }

    /// <summary>
    /// A read-only string-keyed map that enumerates in a fixed order: JS object order (array-index keys first,
    /// ascending, then the others as given) unless <c>jsOrder</c> is false (a JS Map: as given).
    /// </summary>
    internal sealed class OrderedMap<TValue> : IReadOnlyDictionary<string, TValue>
    {
        private readonly Dictionary<string, TValue> _values = new Dictionary<string, TValue>(StringComparer.Ordinal);
        private readonly List<string> _keys = new List<string>();

        public OrderedMap(IEnumerable<KeyValuePair<string, TValue>> pairs, bool jsOrder = true)
        {
            foreach (var pair in pairs)
            {
                if (pair.Key == null) throw new ArgumentException("a key is null", nameof(pairs));
                if (!_values.ContainsKey(pair.Key)) _keys.Add(pair.Key);
                _values[pair.Key] = pair.Value;
            }
            if (jsOrder)
            {
                var index = _keys.Where(JsonObject.IsArrayIndex).OrderBy(k => uint.Parse(k, System.Globalization.CultureInfo.InvariantCulture)).ToList();
                var named = _keys.Where(k => !JsonObject.IsArrayIndex(k)).ToList();
                _keys.Clear();
                _keys.AddRange(index);
                _keys.AddRange(named);
            }
        }

        public TValue this[string key] => _values[key];
        public IEnumerable<string> Keys => _keys;
        public IEnumerable<TValue> Values => _keys.Select(k => _values[k]);
        public int Count => _keys.Count;
        public bool ContainsKey(string key) => _values.ContainsKey(key);

        public bool TryGetValue(string key, out TValue value) => _values.TryGetValue(key, out value!);

        public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator() =>
            _keys.Select(k => new KeyValuePair<string, TValue>(k, _values[k])).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
