// Trial extension of the Expedition (port of the browser trial's src/bridge/exploration/Expedition.js and of
// Expedition.prototype.Inherit in src/app/life.js; docs/EXTENSIONS.md 「探索の持ち物とイベント」「探索の結果と再開」
// 「出立の枠（DeckLimits）」, docs/INHERITANCE.md R-11 / R-13 / R-27, X-1 / X-3 / X-4 / X-9). With no
// ExpeditionOptions (and RunStart.Trial off) nothing here acts: Trial is false, nothing is pending, the limits are
// §8's. Expedition.cs carries the hooks, each marked 「Trial extension」.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using BattleCore;
using DungeonCore;
using InheritanceCore;

// The EditMode tests set Expedition.Mastery the way the browser trial's tests do (Unity's assembly and dotnet's share the name).
[assembly: InternalsVisibleTo("Exploration.Bridge.Tests")]

namespace Exploration.Bridge
{
    public sealed partial class Expedition
    {
        private readonly DeckLimits _limits;
        private readonly int _startPages;
        private ResultCard _result;
        private int _eventsClosed;
        private LegacyReceipt _taken;
        private ExplorationState _takenState;

        /// <summary>The trial rules are on (ExpeditionOptions.Trial or RunStart.Trial). js: Expedition.js:59.</summary>
        public bool Trial { get; }

        /// <summary>
        /// The 死亡地点の痕跡 this life collected (<see cref="Inherit"/>), or null. Like the browser trial, a new life of
        /// the same expedition (ExplorationSession's 「新しい生」) does not clear it. js: Expedition.js:88.
        /// </summary>
        public string InheritedTraceId { get; private set; }

        /// <summary>The layer the collected trace lay on, or null. js: Expedition.js:89.</summary>
        public int? InheritedLayer { get; private set; }

        /// <summary>
        /// How many of the life's rare pages (<see cref="Pages"/>) were inherited — a survivor's whole journal at the
        /// start (ExpeditionOptions.StartPages) and the pages a trace gave (<see cref="Inherit"/>) — rather than found
        /// in this life. The map and the journal list them apart (docs/INHERITANCE.md R-27). js: Expedition.js:95.
        /// </summary>
        public int InheritedPages { get; private set; }

        /// <summary>The rare pages found in this life: <see cref="Pages"/> less <see cref="InheritedPages"/>. js: Expedition.js:147.</summary>
        // js: bridge/exploration/Expedition.js:147
        public int FoundPages => Math.Max(0, Pages - InheritedPages);

        /// <summary>
        /// The journal's 開示度 by enemy id, in the order the life learned them (the browser trial's
        /// <c>expedition._journal</c>, which app/names.js lifeJournal reads). Enemies at 0 may be listed.
        /// </summary>
        public IReadOnlyDictionary<string, int> JournalLevels
        {
            get
            {
                SyncLife();
                return _journal;
            }
        }

        /// <summary>Every event of this life (EventOutcome), oldest first. Empty outside trial. js: Expedition.js:335.</summary>
        // js: bridge/exploration/Expedition.js:335
        public IReadOnlyList<EventOutcome> Events => Session.Events;

        /// <summary>
        /// A 重い宝箱 was taken (encounters_and_rewards_v4.md §5): its card is the card pile's (3 laid out, pick one with
        /// Learn — §4.3), a card the piles can no longer deal becomes a consumable (§4.3), its consumables are drawn by
        /// the layer's weights (§4.3) and its pages counted. Seeded from the node (RewardSeed), so a life replays it.
        /// The session puts the consumables into the slots.
        /// </summary>
        // js: bridge/exploration/Expedition.js:344
        public ChestFind TakeChest(int layer, int nodeId, ChestContents contents)
        {
            if (contents == null) throw new ArgumentNullException(nameof(contents));
            SyncLife();
            var rng = new DungeonCore.SeededRng(RewardSeed(Session.RunSeed, layer, nodeId));
            IReadOnlyList<string> cards = Array.Empty<string>();
            var consumables = new List<string>();
            bool spent = false;
            if (contents.Cards > 0)
            {
                cards = CardPile.Deal(layer, _owned, rng);
                if (cards.Count == 0)
                {
                    spent = true;
                    consumables.Add(BattleRewards.DrawConsumable(layer, rng));
                }
            }
            for (int i = 0; i < contents.Consumables; i++) consumables.Add(BattleRewards.DrawConsumable(layer, rng));
            if (cards.Count > 0) _offer = cards.ToList();
            _pages += contents.Pages;
            return new ChestFind(cards, consumables, contents.Pages, spent);
        }

        /// <summary>
        /// The result card waiting on the player, or null: a battle's after Return (Kind Battle: Back, Raised,
        /// PileSpent, Benched, DeckMax — Raised the enemies whose 開示度 rose, Benched the learned cards a full deck
        /// kept out), or the last event's once it happened (Kind Event: Outcome; a declined event shows none). It stays
        /// until <see cref="CloseResult"/>, so a reload shows the same card — and the same pick (Offer) — again (tier1
        /// R1-16). It is dropped as soon as the exploration moved on (any change of the session's state). Null outside
        /// trial.
        /// </summary>
        // js: bridge/exploration/Expedition.js:372
        public ResultCard PendingResult
        {
            get
            {
                SyncLife();
                if (!Trial) return null;
                ExplorationSession session = Session;
                if (_result != null) return ReferenceEquals(_result.State, session.State) ? _result : null;
                if (session.HasPendingEvent || session.Events.Count <= _eventsClosed) return null;
                EventOutcome last = session.Events[session.Events.Count - 1];
                return last.Accepted && ReferenceEquals(session.EventState, session.State) ? ResultCard.ForEvent(last) : null;
            }
        }

        /// <summary>
        /// The result card was closed. A pick not taken is given up (Offer empties: 「受け取らない」) and nothing is
        /// pending any more. A chest's card pick closed without a card drops 「カード 1 枚」 from the event's record
        /// (docs/EXTENSIONS.md X-9). False outside trial.
        /// </summary>
        // js: bridge/exploration/Expedition.js:387
        public bool CloseResult()
        {
            SyncLife();
            if (!Trial) return false;
            IReadOnlyList<EventOutcome> events = Session.Events;
            EventOutcome last = events.Count > _eventsClosed ? events[events.Count - 1] : null;
            if (_result == null && last != null && last.Cards.Count > 0 && _offer.Count > 0 && last.Cards.All(id => _offer.Contains(id)))
            {
                Session.TurnDownFind(events.Count - 1, ExplorationText.RecordCards(1));
            }
            _result = null;
            _eventsClosed = Session.Events.Count;
            _offer.Clear();
            return true;
        }

        /// <summary>
        /// For each card of Offer, whether learning it puts a copy into the deck (encounters_and_rewards_v4.md §4.3
        /// 「デッキの上限の中ですぐ戦闘デッキに入ります」; DeckRules.CanAdd with the life's limits). A card that does not
        /// fit is still owned.
        /// </summary>
        // js: bridge/exploration/Expedition.js:410
        public IReadOnlyList<bool> OfferFits
        {
            get
            {
                SyncLife();
                return _offer
                    .Select(id => DeckRules.CanAdd(_deck, id, new HashSet<string>(_owned.Concat(new[] { id }), StringComparer.Ordinal), _limits))
                    .ToList();
            }
        }

        /// <summary>The deck's cap this life (the survivor's bonus raises it: docs/EXTENSIONS.md L-2).</summary>
        // js: bridge/exploration/Expedition.js:416
        public int DeckMax => (_limits ?? DeckLimits.Default).DeckMax;

        /// <summary>
        /// The 受け取った遺産 card is up (docs/EXTENSIONS.md X-3): the data the scene wrote from the trace card it just took
        /// from. It stays pending (<see cref="PendingTaken"/>) until <see cref="CloseTaken"/>, so a reload shows the same
        /// card again (tier1 R1-16). False outside trial or before this life took a trace.
        /// </summary>
        // js: bridge/exploration/Expedition.js:427
        public bool ShowTaken(LegacyReceipt card)
        {
            SyncLife();
            if (!Trial || InheritedTraceId == null || card == null) return false;
            _taken = card;
            _takenState = Session.State;
            return true;
        }

        /// <summary>The 受け取った遺産 card waiting on the player, or null; dropped once the exploration moved on (like PendingResult).</summary>
        // js: bridge/exploration/Expedition.js:435
        public LegacyReceipt PendingTaken
        {
            get
            {
                SyncLife();
                if (!Trial || _taken == null) return null;
                return ReferenceEquals(_takenState, Session.State) ? _taken : null;
            }
        }

        /// <summary>The 受け取った遺産 card was closed.</summary>
        // js: bridge/exploration/Expedition.js:442
        public bool CloseTaken()
        {
            SyncLife();
            _taken = null;
            _takenState = null;
            return true;
        }

        /// <summary>写しの帳 (or the layer's 情報収集): the enemy is raised to 開示度 2 in the journal (§2.2).</summary>
        // js: bridge/exploration/Expedition.js:449
        public void TakeSurvey(string enemyId)
        {
            SyncLife();
            if (enemyId == null) return;
            _journal[enemyId] = Math.Max(DisclosureOf(enemyId), 2);
        }

        /// <summary>An enemy's name for the exploration's cards (the battle core's catalogue), or its id.</summary>
        // js: bridge/exploration/Expedition.js:456
        public string EnemyName(string enemyId)
        {
            try
            {
                return Enemies.ById(enemyId).Name;
            }
            catch (ArgumentNullException)
            {
                return enemyId;
            }
            catch (KeyNotFoundException)
            {
                return enemyId;
            }
        }

        /// <summary>
        /// What a trace gave, taken into this life (docs/INHERITANCE.md R-11): each card is owned (and one copy joins
        /// the deck when the deck has room, as a learned card does), its 段 goes into the mastery book (keeping the
        /// one-card rule on 3 段: Inheritance.MergeMastery), the enemy pages raise 開示度 (the journal keeps the higher),
        /// the dungeon pages count as inherited (<see cref="InheritedPages"/>), not as found. <paramref name="taken"/>
        /// is Inheritance.TakeTrace(...).Taken. Returns what the take did, for the 受け取った遺産 card (TraceGain writes it
        /// from this, not from a preview). The browser trial adds this to the Expedition from app/life.js.
        /// </summary>
        // js: app/life.js:119
        public InheritResult Inherit(TakenLegacy taken)
        {
            SyncLife();
            IReadOnlyList<TakenCard> cards = taken?.Cards ?? (IReadOnlyList<TakenCard>)Array.Empty<TakenCard>();
            int max = DeckMax;
            var rows = new List<InheritedCard>();
            foreach (TakenCard c in cards)
            {
                bool owned = _owned.Contains(c.CardId);
                bool deckFull = _deck.Count >= max;
                int from = Mastery.RankOf(c.CardId);
                bool joins = Take(c.CardId);
                rows.Add(new InheritedCard(c.CardId, c.Rank, from, from, joins, deckFull, owned));
            }
            if (cards.Count > 0)
            {
                Mastery = Mastery with { Records = Inheritance.MergeMastery(Mastery.Records, cards) };
            }
            var enemies = new List<InheritedEnemy>();
            if (taken?.Journal != null)
            {
                foreach (KeyValuePair<string, int> pair in taken.Journal.Disclosure)
                {
                    int now = _journal.TryGetValue(pair.Key, out int level) ? level : 0;
                    if (pair.Value > now) _journal[pair.Key] = pair.Value;
                    enemies.Add(new InheritedEnemy(pair.Key, pair.Value, now, Math.Max(now, pair.Value)));
                }
            }
            int pages = taken?.Journal?.Pages.Count ?? 0;
            _pages += pages;
            InheritedPages += pages;
            InheritedTraceId = taken?.TraceId;
            InheritedLayer = Session.State.Profile.Layer;
            return new InheritResult(
                rows.Select(r => new InheritedCard(r.CardId, r.Rank, r.From, Mastery.RankOf(r.CardId), r.Joins, r.DeckFull, r.Owned)),
                enemies,
                pages);
        }

        // ---- the hooks Expedition.cs calls ----

        /// <summary>
        /// The start the session is built from (js: Expedition.js:59-63): in trial mode a RunStart with Trial on, and
        /// with the lineage's CarvingKnown when the options name one. The caller's RunStart is copied, never changed.
        /// </summary>
        private RunStart TrialStart(RunStart start, ExpeditionOptions options)
        {
            if (Trial && start?.Trial != true)
            {
                start = Copy(start ?? new RunStart());
                start.Trial = true;
            }
            if (Trial && options?.CarvingKnown != null && start.CarvingKnown != options.CarvingKnown)
            {
                start = Copy(start);
                start.CarvingKnown = options.CarvingKnown;
            }
            return start;
        }

        /// <summary>A RunStart with the same members (the JS's <c>new RunStart({ ...start })</c>).</summary>
        private static RunStart Copy(RunStart start) => new RunStart
        {
            Seed = start.Seed,
            MaxHp = start.MaxHp,
            Hp = start.Hp,
            Stamina = start.Stamina,
            MiasmaPercent = start.MiasmaPercent,
            Loadout = start.Loadout,
            Trial = start.Trial,
            CarvingKnown = start.CarvingKnown,
            MapFor = start.MapFor,
        };

        /// <summary>The enemies of the fights fought, each once, in the order the journal is written (js: the 開示度 Map's order).</summary>
        private static IEnumerable<string> FoughtIds(EncounterBattle battle)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (FoughtBattle fought in battle.Fought)
            {
                foreach (string id in fought.Fight.EnemyIds)
                {
                    if (seen.Add(id)) yield return id;
                }
            }
        }

        /// <summary>The journal's 開示度 of every enemy fought, before the encounter's result is written. js: Expedition.js:207.</summary>
        // js: bridge/exploration/Expedition.js:207
        private Dictionary<string, int> DisclosureBefore(EncounterBattle battle)
        {
            var was = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string id in FoughtIds(battle)) was[id] = DisclosureOf(id);
            return was;
        }

        /// <summary>
        /// Whether the reward roll asked the card pile for a card: the same roll with nothing owned deals one exactly
        /// when the real roll asked the pile (§4.3 「札の代わりに消耗品」). js: Expedition.js:240.
        /// </summary>
        // js: bridge/exploration/Expedition.js:240
        private bool PileWanted(BattleHandIn handIn, UniqueCardReward reward) =>
            BattleRewards.Roll(handIn.Kind, handIn.Layer, handIn.Encounter, reward, new HashSet<string>(StringComparer.Ordinal),
                new DungeonCore.SeededRng(RewardSeed(Session.RunSeed, handIn.Layer, Session.PendingBattleNode))).CardChoices.Count > 0;

        /// <summary>Return's result card (js: Expedition.js:258): kept until CloseResult, none when the life ended there.</summary>
        // js: bridge/exploration/Expedition.js:258
        private void KeepResult(EncounterBattle battle, BattleReturn back, IReadOnlyDictionary<string, int> disclosure,
            IReadOnlyDictionary<string, int> was, bool pileSpent, IReadOnlyList<string> benched)
        {
            _eventsClosed = Session.Events.Count;
            if (back.Died)
            {
                _result = null;
                return;
            }
            var raised = FoughtIds(battle)
                .Where(id => disclosure[id] > (was.TryGetValue(id, out int before) ? before : 0))
                .Select(id => new KeyValuePair<string, int>(id, disclosure[id]))
                .ToList();
            _result = ResultCard.ForBattle(back, raised, pileSpent, benched, DeckMax, Session.State);
        }

        /// <summary>Learn's trial part (js: Expedition.js:300): the pick is made on the result card, so the card is done with.</summary>
        private void LearnedOnResult()
        {
            _result = null;
            _eventsClosed = Session.Events.Count;
        }

        /// <summary>
        /// BeginLife's trial part: nothing pending (js: Expedition.js:478-480), and the pages the heir set out with
        /// count among the life's pages from its start (js: app/life.js:205-211, which wraps BeginLife). As in the
        /// browser trial, InheritedPages is set again only when the heir set out with pages.
        /// </summary>
        private void BeginLifeTrial()
        {
            _result = null;
            _eventsClosed = 0;
            _taken = null;
            _takenState = null;
            if (_startPages > 0)
            {
                _pages += _startPages;
                InheritedPages = _startPages;
            }
        }
    }

    /// <summary>What a result card shows: a battle's (Expedition.Return) or an event's.</summary>
    public enum ResultCardKind
    {
        Battle,
        Event,
    }

    /// <summary>
    /// The result card waiting on the player (js: Expedition.js:260 <c>{ Kind: 'battle', Back, Raised, PileSpent,
    /// Benched, DeckMax, State }</c> and :379 <c>{ Kind: 'event', Outcome }</c>). A battle's card sets the battle's
    /// members, an event's sets Outcome; the others are null, empty or 0.
    /// </summary>
    public sealed class ResultCard
    {
        private static readonly IReadOnlyList<KeyValuePair<string, int>> NoLevels =
            new ReadOnlyCollection<KeyValuePair<string, int>>(new KeyValuePair<string, int>[0]);

        private ResultCard(ResultCardKind kind, BattleReturn back, IReadOnlyList<KeyValuePair<string, int>> raised, bool pileSpent,
            IReadOnlyList<string> benched, int deckMax, ExplorationState state, EventOutcome outcome)
        {
            Kind = kind;
            Back = back;
            Raised = raised ?? NoLevels;
            PileSpent = pileSpent;
            Benched = benched ?? Array.Empty<string>();
            DeckMax = deckMax;
            State = state;
            Outcome = outcome;
        }

        public static ResultCard ForBattle(BattleReturn back, IEnumerable<KeyValuePair<string, int>> raised, bool pileSpent,
            IEnumerable<string> benched, int deckMax, ExplorationState state) =>
            new ResultCard(ResultCardKind.Battle, back,
                new ReadOnlyCollection<KeyValuePair<string, int>>((raised ?? NoLevels).ToArray()), pileSpent,
                new ReadOnlyCollection<string>((benched ?? Array.Empty<string>()).ToArray()), deckMax, state, null);

        public static ResultCard ForEvent(EventOutcome outcome) =>
            new ResultCard(ResultCardKind.Event, null, null, false, null, 0, null, outcome);

        public ResultCardKind Kind { get; }

        /// <summary>The battle's return (Battle only).</summary>
        public BattleReturn Back { get; }

        /// <summary>The enemies whose 開示度 rose in this battle, with the level after it, in the journal's order (Battle only).</summary>
        public IReadOnlyList<KeyValuePair<string, int>> Raised { get; }

        /// <summary>The reward wanted a card and every pile was spent: a consumable came instead (§4.3; Battle only).</summary>
        public bool PileSpent { get; }

        /// <summary>The learned cards a full deck kept out: owned only (§4.3; Battle only).</summary>
        public IReadOnlyList<string> Benched { get; }

        /// <summary>The deck's cap this life, for the Benched note (Battle only).</summary>
        public int DeckMax { get; }

        /// <summary>The session's state the card belongs to: it is dropped once the state moves on (Battle only).</summary>
        public ExplorationState State { get; }

        /// <summary>The event (Event only).</summary>
        public EventOutcome Outcome { get; }
    }

    /// <summary>One card a take gave (js: app/life.js:128 / bridge/exploration/TraceGain.js:42 rows).</summary>
    public sealed class InheritedCard
    {
        public InheritedCard(string cardId, int rank, int from, int to, bool joins, bool deckFull, bool owned)
        {
            CardId = cardId;
            Rank = rank;
            From = from;
            To = to;
            Joins = joins;
            DeckFull = deckFull;
            Owned = owned;
        }

        public string CardId { get; }

        /// <summary>The 段 the trace held.</summary>
        public int Rank { get; }

        /// <summary>The card's 段 in this life before the take.</summary>
        public int From { get; }

        /// <summary>The card's 段 in this life after it (the one-card rule on 3 段 may stop it at 2).</summary>
        public int To { get; }

        /// <summary>A copy joined the deck.</summary>
        public bool Joins { get; }

        /// <summary>The deck was at its cap when the card's turn came.</summary>
        public bool DeckFull { get; }

        /// <summary>The card was owned already.</summary>
        public bool Owned { get; }
    }

    /// <summary>One enemy page a take gave, or would give (js: app/life.js:140, TraceGain.js:51 pagePreview).</summary>
    public sealed class InheritedEnemy
    {
        public InheritedEnemy(string enemyId, int disclosure, int from, int to)
        {
            EnemyId = enemyId;
            Disclosure = disclosure;
            From = from;
            To = to;
        }

        public string EnemyId { get; }

        /// <summary>The 開示度 the trace held.</summary>
        public int Disclosure { get; }

        /// <summary>The journal's 開示度 before the take.</summary>
        public int From { get; }

        /// <summary>The journal's 開示度 after it (the higher of the two).</summary>
        public int To { get; }
    }

    /// <summary>
    /// What <see cref="Expedition.Inherit"/> did (js: app/life.js:147 <c>{ Cards, Enemies, Pages }</c>), and the
    /// router's 走り書き rows when it adds them (js: <c>{ ...result, Scribbles }</c>; <see cref="WithScribbles"/>).
    /// </summary>
    public sealed class InheritResult
    {
        public InheritResult(IEnumerable<InheritedCard> cards, IEnumerable<InheritedEnemy> enemies, int pages,
            IEnumerable<ScribbleRow> scribbles = null)
        {
            Cards = new ReadOnlyCollection<InheritedCard>((cards ?? Array.Empty<InheritedCard>()).ToArray());
            Enemies = new ReadOnlyCollection<InheritedEnemy>((enemies ?? Array.Empty<InheritedEnemy>()).ToArray());
            Pages = pages;
            Scribbles = new ReadOnlyCollection<ScribbleRow>((scribbles ?? Array.Empty<ScribbleRow>()).ToArray());
        }

        public IReadOnlyList<InheritedCard> Cards { get; }

        public IReadOnlyList<InheritedEnemy> Enemies { get; }

        /// <summary>The dungeon pages taken (they count as inherited).</summary>
        public int Pages { get; }

        /// <summary>The 走り書き the take offered and whether each was new to the life (the router's rows); empty from Inherit.</summary>
        public IReadOnlyList<ScribbleRow> Scribbles { get; }

        /// <summary>The same result with the router's 走り書き rows.</summary>
        public InheritResult WithScribbles(IEnumerable<ScribbleRow> scribbles) => new InheritResult(Cards, Enemies, Pages, scribbles);
    }

    /// <summary>
    /// The 受け取った遺産 card's content (js: scenes/explore/ExploreScene.js:1934 takenCard): the rows the trace card
    /// listed for the picks, each with what it gave this heir (X-11), and how many candidates were lost. Plain data
    /// the scene writes and the expedition keeps until it is closed (Expedition.ShowTaken); ToJson / FromJson give the
    /// JS's shape, for the save's log.
    /// </summary>
    public sealed class LegacyReceipt
    {
        public LegacyReceipt(string placeLine, string heirLine, IEnumerable<LegacyReceiptRow> rows, int lost)
        {
            PlaceLine = placeLine;
            HeirLine = heirLine;
            Rows = new ReadOnlyCollection<LegacyReceiptRow>((rows ?? Array.Empty<LegacyReceiptRow>()).ToArray());
            Lost = lost;
        }

        public string PlaceLine { get; }

        public string HeirLine { get; }

        public IReadOnlyList<LegacyReceiptRow> Rows { get; }

        /// <summary>How many candidates were not taken (concept-v3 §5 「選ばなかった頁と資源は失われる」).</summary>
        public int Lost { get; }

        /// <summary>The card as the JS writes it: <c>{ PlaceLine, HeirLine, Rows, Lost }</c>.</summary>
        public JsonObject ToJson() => new JsonObject
        {
            ["PlaceLine"] = PlaceLine,
            ["HeirLine"] = HeirLine,
            ["Rows"] = Rows.Select(r => (object)r.ToJson()).ToList(),
            ["Lost"] = Lost,
        };

        /// <summary>The card back from its JSON (Json.Parse's values). Throws ArgumentException on another shape.</summary>
        public static LegacyReceipt FromJson(object json)
        {
            if (!(json is JsonObject o)) throw new ArgumentException("LegacyReceipt: a JSON object", nameof(json));
            var rows = new List<LegacyReceiptRow>();
            if (o["Rows"] is IEnumerable<object> list)
            {
                foreach (object row in list) rows.Add(LegacyReceiptRow.FromJson(row));
            }
            object lost = o["Lost"];
            return new LegacyReceipt(o["PlaceLine"] as string, o["HeirLine"] as string, rows,
                Json.IsInteger(lost) ? (int)Json.ToDouble(lost) : 0);
        }
    }

    /// <summary>One row of the 受け取った遺産 card (js: ExploreScene.js:1937).</summary>
    public sealed class LegacyReceiptRow
    {
        public LegacyReceiptRow(CandidateKind kind, string kindLabel, string name, string detail, IEnumerable<string> notes, bool nothing)
        {
            Kind = kind;
            KindLabel = kindLabel;
            Name = name;
            Detail = detail;
            Notes = new ReadOnlyCollection<string>((notes ?? Array.Empty<string>()).ToArray());
            Nothing = nothing;
        }

        public CandidateKind Kind { get; }

        public string KindLabel { get; }

        public string Name { get; }

        public string Detail { get; }

        public IReadOnlyList<string> Notes { get; }

        /// <summary>The pick gave this heir nothing at all.</summary>
        public bool Nothing { get; }

        /// <summary>The row as the JS writes it: <c>{ Kind, KindLabel, Name, Detail, Notes, Nothing }</c>.</summary>
        public JsonObject ToJson() => new JsonObject
        {
            ["Kind"] = Kind.ToString(),
            ["KindLabel"] = KindLabel,
            ["Name"] = Name,
            ["Detail"] = Detail,
            ["Notes"] = Notes.Select(n => (object)n).ToList(),
            ["Nothing"] = Nothing,
        };

        /// <summary>A row back from its JSON. Throws ArgumentException on another shape or an unknown kind.</summary>
        public static LegacyReceiptRow FromJson(object json)
        {
            if (!(json is JsonObject o)) throw new ArgumentException("LegacyReceiptRow: a JSON object", nameof(json));
            if (!(o["Kind"] is string name) || !Enum.IsDefined(typeof(CandidateKind), name))
                throw new ArgumentException("LegacyReceiptRow: an unknown Kind", nameof(json));
            var notes = new List<string>();
            if (o["Notes"] is IEnumerable<object> list)
            {
                foreach (object note in list)
                {
                    if (note is string text) notes.Add(text);
                }
            }
            return new LegacyReceiptRow((CandidateKind)Enum.Parse(typeof(CandidateKind), name), o["KindLabel"] as string,
                o["Name"] as string, o["Detail"] as string, notes, Equals(o["Nothing"], true));
        }
    }
}
