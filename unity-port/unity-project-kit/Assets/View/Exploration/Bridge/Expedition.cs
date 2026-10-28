// One life in the dungeon as both cores see it (#99): the exploration session, and what the battles of
// the life share — the deck, the cards owned, the mastery book, the in-battle tools and what the
// journal has learned of each enemy. It hands a combat node's battle in (EncounterBattle) and takes
// its result back: HP and stamina into the exploration core, the ticks into the mastery book, the
// 開示度 into the journal, the unique cards into the cards owned and the deck, and the rest of the
// reward (#625: the pile's cards to pick one from, consumables, pages). A page is read into the journal
// at once (#107: one of the layer's dungeon entries in full), or turns into a card when the layer has
// nothing left to tell. A consumable found goes into a
// free slot of the exploration (#651), or waits for the player to swap it in or give it up. It also
// spends the marks a 習熟訓練 node earns on the card the player names. Pure C#; the View that loads the battle scene only
// forwards the source and the end of each fight.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using DungeonContent;
using DungeonCore;

namespace Exploration.Bridge
{
    public sealed class Expedition
    {
        /// <summary>concept-v3 §14 / swordsman_cards_v4 §2: the kinds every character owns from the start (#1〜#40).</summary>
        public static readonly IReadOnlyList<string> InitialKinds = CardCatalog.All.Take(40).Select(d => d.Id).ToList();

        private readonly IReadOnlyList<CardInstance> _startDeck;
        private readonly IReadOnlyDictionary<string, int> _startJournal;
        private readonly string _talent;
        private readonly Dictionary<string, int> _journal = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _owned = new HashSet<string>(StringComparer.Ordinal);
        private List<CardInstance> _deck;
        private readonly List<string> _consumables = new List<string>();
        private readonly List<string> _waiting = new List<string>();
        private List<string> _offer = new List<string>();
        private int _life;
        private int _learnedCopies;
        private int _pages;
        private int _pileDeals;
        private int _pileLayer;
        private DungeonCore.IRng _pileRng;

        /// <param name="start">How the exploration starts (seed, HP, stamina, gauge, exploration tools).</param>
        /// <param name="deck">The deck the life sets out with; null for the slice's prototype deck.</param>
        /// <param name="battleToolIds">
        /// The tools in the slots that act inside a battle (間合いの履, 息継ぎの笛, 竜鱗の欠片, 竜語の符;
        /// tools_and_consumables_v4.md §2), as catalogue ids. The tools that act on the exploration go in
        /// <see cref="RunStart.Loadout"/>; turning one chosen loadout into both is 出立's (#104).
        /// </param>
        /// <param name="talent">才能: the card whose battle ticks count twice (§17.8), or null.</param>
        /// <param name="intel">
        /// What the exploration screen says about the enemy behind a node. Null: this expedition's own
        /// <see cref="Journal"/>, which reads the 開示度 the battles leave here (#107).
        /// </param>
        /// <param name="journal">The journal's 開示度 by enemy id as the life starts. An enemy not listed reads 0.</param>
        public Expedition(
            RunStart start = null,
            IReadOnlyList<CardInstance> deck = null,
            IReadOnlyList<string> battleToolIds = null,
            string talent = null,
            IEnemyIntel intel = null,
            IReadOnlyDictionary<string, int> journal = null)
        {
            _startDeck = (deck ?? PrototypeDeck.Build()).ToList();
            DeckValidation validation = Cards.Validate(_startDeck);
            if (!validation.Ok) throw new ArgumentException("Expedition: the deck breaks §8 — " + string.Join(" / ", validation.Errors), nameof(deck));
            _startJournal = journal ?? new Dictionary<string, int>();
            _talent = talent;
            Tools = BattleTools(_startDeck, battleToolIds ?? Array.Empty<string>(), talent);
            Journal = new LifeJournal(Learned);
            Session = new ExplorationSession(start, intel ?? Journal);
            Journal.Follow(Session);
            BeginLife();
        }

        public ExplorationSession Session { get; }

        /// <summary>
        /// This life's journal (#107): the enemy pages cut by 開示度, the dungeon entries, the memo and the
        /// lines for the legacy list. It adds the survey's 2 on top of what the battles taught.
        /// </summary>
        public LifeJournal Journal { get; }

        /// <summary>The deck each battle is shuffled from, as set out with plus the cards learned on the way.</summary>
        public IReadOnlyList<CardInstance> Deck
        {
            get
            {
                SyncLife();
                return _deck;
            }
        }

        /// <summary>The card kinds this character owns: the initial forty, the deck's, and every card learned this life.</summary>
        public IReadOnlyCollection<string> Owned
        {
            get
            {
                SyncLife();
                return _owned;
            }
        }

        /// <summary>This life's mastery (§3.2 / §17.8).</summary>
        public MasteryBook Mastery { get; private set; }

        /// <summary>The in-battle tools (§9.1, #452) as amounts, with the deck and the talent the battles read.</summary>
        public Departure Tools { get; }

        /// <summary>
        /// Cards waiting for the player to pick one (<see cref="Learn"/>): a real elite's two, or the card
        /// pile's three (encounters_and_rewards_v4.md §4.2 / §4.3). Empty when none waits.
        /// </summary>
        public IReadOnlyList<string> Offer
        {
            get
            {
                SyncLife();
                return _offer;
            }
        }

        /// <summary>
        /// The consumables the battles of this life dropped, as catalogue ids in the order found: the
        /// ones that went into a slot and the ones in <see cref="WaitingConsumables"/> alike.
        /// </summary>
        public IReadOnlyList<string> Consumables
        {
            get
            {
                SyncLife();
                return _consumables;
            }
        }

        /// <summary>
        /// Consumables found while every slot was full (tools_and_consumables_v4.md §3: swap in place or
        /// give up), as catalogue ids in the order found. They wait for <see cref="SwapConsumable"/> or
        /// <see cref="DiscardConsumable"/>; the exploration does not stop for them. Empty when none waits.
        /// </summary>
        public IReadOnlyList<string> WaitingConsumables
        {
            get
            {
                SyncLife();
                return _waiting;
            }
        }

        /// <summary>
        /// The rare journal pages the battles of this life dropped and the journal read: each told one
        /// of its layer's dungeon entries in full (LifeJournal.DrawRarePage, dungeon_exploration_v4.md
        /// §6.11). A page whose layer had nothing left to tell turned into a card and is not counted.
        /// </summary>
        public int Pages
        {
            get
            {
                SyncLife();
                return _pages;
            }
        }

        /// <summary>The encounter being fought, or null.</summary>
        public EncounterBattle Battle { get; private set; }

        /// <summary>
        /// The journal's 開示度 for an enemy: 0 until it is met, 1 after a battle with it, 2 after it is
        /// observed or once a 情報収集 named it (#107, LifeJournal.SurveyTarget).
        /// </summary>
        public int DisclosureOf(string enemyId) => Journal.DisclosureOf(enemyId);

        /// <summary>What the battles of this life taught of an enemy, without the survey.</summary>
        private int Learned(string enemyId)
        {
            SyncLife();
            return enemyId != null && _journal.TryGetValue(enemyId, out int level) ? level : 0;
        }

        /// <summary>
        /// Everything the combat node waiting on a battle hands it, or null when no battle waits. The
        /// battle's seed comes from the life's seed and the node, so a life replays its fights.
        /// </summary>
        public BattleHandIn HandIn()
        {
            SyncLife();
            var session = Session;
            if (!session.HasPendingBattle || session.PendingEncounter == null) return null;

            ExplorationState state = session.State;
            int layer = state.Profile.Layer;
            NodeKind kind = state.Map.Node(session.PendingBattleNode).Kind;
            Encounter encounter = session.PendingEncounter;
            var cells = encounter.Fights
                .Select(fight => BattleField.CellsFor(layer, kind, fight.EnemyIds.Sum(id => Enemies.ById(id).Size)))
                .ToList();
            var disclosure = encounter.EnemyIds.Distinct(StringComparer.Ordinal).ToDictionary(id => id, DisclosureOf, StringComparer.Ordinal);
            int seed = unchecked((int)ExplorationSession.EncounterSeed(session.RunSeed, layer, session.PendingBattleNode));

            return new BattleHandIn(
                layer, kind, encounter, cells,
                state.Hp, state.MaxHp, state.Stamina, state.MaxStamina, state.MiasmaPercent,
                disclosure, Mastery.MasteredDeck(_deck), Tools, seed);
        }

        /// <summary>Starts the battle of the node that waits on one. Null when none waits.</summary>
        public EncounterBattle StartEncounter()
        {
            if (Battle != null) throw new InvalidOperationException("Expedition: a battle is already being fought.");
            BattleHandIn handIn = HandIn();
            if (handIn == null) return null;
            Battle = new EncounterBattle(handIn);
            return Battle;
        }

        /// <summary>
        /// The encounter is over: bring its result back. HP and stamina go to the exploration core
        /// (HP 0 is a fall), every fight's ticks to the mastery book (steps go up only now, §17.8), the
        /// 開示度 to the journal (every enemy fought reads at least 1, and 2 once a 観察 was played
        /// against it), and a won encounter's unique cards to the cards owned. A won encounter also
        /// draws the rest of its reward (§4.2) from the exploration's seed: the cards to pick one from
        /// wait in <see cref="Offer"/>, the consumables go into the exploration's free slots (the rest wait
        /// in <see cref="WaitingConsumables"/>) and each page is read into the journal: it tells one entry
        /// of the layer not yet known in full (LifeJournal.DrawRarePage). A page whose layer has nothing
        /// left to tell turns into a card (encounters_and_rewards_v4.md §4.3): the pile's three to pick
        /// one from, laid out once no other pick waits, or a consumable when the piles are spent.
        /// </summary>
        public BattleReturn Return()
        {
            EncounterBattle battle = Battle ?? throw new InvalidOperationException("Expedition: no battle is being fought.");
            if (!battle.Over) throw new InvalidOperationException("Expedition: the encounter has fights left.");
            Battle = null;

            var ranks = new List<RankUp>();
            var plays = new List<MasteryPlay>();
            var disclosure = new Dictionary<string, int>(StringComparer.Ordinal);
            var observed = new List<string>();
            foreach (FoughtBattle fought in battle.Fought)
            {
                MasteryResult result = Mastery.AfterBattle(fought.State);
                Mastery = result.Book;
                ranks.AddRange(result.RankUps);
                plays.AddRange(fought.State.History.MasteryPlays);

                foreach (string id in fought.Fight.EnemyIds)
                {
                    int before = DisclosureOf(id);
                    int after = Math.Max(before, fought.Observed ? 2 : 1);
                    _journal[id] = after;
                    disclosure[id] = after;
                    if (fought.Observed && before < 2 && !observed.Contains(id)) observed.Add(id);
                }
            }

            BattleHandIn handIn = battle.HandIn;
            UniqueCardReward reward = battle.Won
                ? UniqueCards.For(handIn.Kind, handIn.Encounter, _owned)
                : UniqueCardReward.None;
            foreach (string id in reward.Learned) Take(id);
            BattleReward rest = battle.Won
                ? BattleRewards.Roll(handIn.Kind, handIn.Layer, handIn.Encounter, reward, _owned,
                    new DungeonCore.SeededRng(RewardSeed(Session.RunSeed, handIn.Layer, Session.PendingBattleNode)))
                : BattleReward.None;
            _offer = (reward.Offered.Count > 0 ? reward.Offered : rest.CardChoices).ToList();
            _consumables.AddRange(rest.Consumables);

            var pagesRead = new List<string>();
            int pagesTurned = 0;
            var pageRng = new DungeonCore.SeededRng(PageSeed(Session.RunSeed, handIn.Layer, Session.PendingBattleNode));
            for (int i = 0; i < rest.Pages; i++)
            {
                string told = Journal.DrawRarePage(handIn.Layer, pageRng);
                if (told != null) pagesRead.Add(told);
                else pagesTurned++;
            }
            _pages += pagesRead.Count;

            Session.ReportBattle(battle.HpAfter, battle.StaminaAfter);
            foreach (string id in rest.Consumables) Stow(id);

            bool pickWaited = _offer.Count > 0;
            int foundBefore = _consumables.Count;
            _pileDeals = pagesTurned;
            _pileLayer = handIn.Layer;
            _pileRng = pageRng;
            DealTurnedPages();
            var brought = new BattleReward(
                pickWaited ? rest.CardChoices : _offer.ToList(),
                rest.Consumables.Concat(_consumables.Skip(foundBefore)).ToList(),
                pagesRead.Count);

            return new BattleReturn(
                battle.Won, battle.HpAfter, battle.StaminaAfter, MasteryTally.Of(plays), ranks,
                disclosure, observed, reward.Learned, reward.Offered, brought, pagesRead, pagesTurned);
        }

        /// <summary>
        /// The seed a won node's reward is drawn from: the node's encounter seed, salted so the reward
        /// does not replay the draw that picked the encounter.
        /// </summary>
        public static ulong RewardSeed(ulong runSeed, int layer, int nodeId) =>
            ExplorationSession.EncounterSeed(runSeed, layer, nodeId) ^ 0xD1B54A32D192ED03UL;

        /// <summary>
        /// The seed a won node's pages are read from (and the cards a page turns into are dealt from),
        /// salted apart from the reward's own draw.
        /// </summary>
        private static ulong PageSeed(ulong runSeed, int layer, int nodeId) =>
            RewardSeed(runSeed, layer, nodeId) ^ 0x632BE59BD9B4E019UL;

        /// <summary>
        /// The battle could not be shown (the View found no battle scene to play it in): it is dropped
        /// unfought, and the node still waits on a result.
        /// </summary>
        public void Abandon()
        {
            Battle = null;
        }

        /// <summary>
        /// Picks one card of the offer — a real elite's or the card pile's (encounters_and_rewards_v4.md
        /// §4.2 / §4.3). False when it is not on offer.
        /// </summary>
        public bool Learn(string cardId)
        {
            SyncLife();
            if (cardId == null || !_offer.Contains(cardId)) return false;
            Take(cardId);
            _offer.Clear();
            DealTurnedPages();
            return true;
        }

        /// <summary>
        /// Swaps a waiting consumable in for the one in <paramref name="replaceSlot"/>. A slot freed since
        /// it was found takes it without a swap. False when it is not waiting or the life is over; a
        /// swap into full slots that names no slot throws (DungeonCore.RunLoadout.Take).
        /// </summary>
        public bool SwapConsumable(string consumableId, int replaceSlot)
        {
            SyncLife();
            if (consumableId == null || !_waiting.Contains(consumableId)) return false;
            if (!Session.TakeConsumable(RunStart.Consumable(consumableId), replaceSlot)) return false;
            _waiting.Remove(consumableId);
            return true;
        }

        /// <summary>Gives a waiting consumable up. False when it is not waiting.</summary>
        public bool DiscardConsumable(string consumableId)
        {
            SyncLife();
            return consumableId != null && _waiting.Remove(consumableId);
        }

        /// <summary>
        /// 習熟訓練 (battle_core_v4.md §17.8): spends one mark a 習熟訓練 node earned
        /// (ExplorationState.TrainingMarks) on a card this character owns, which takes the training's
        /// ticks in the mastery book. False when no mark is left, the card is not owned or the
        /// exploration is not waiting on the map.
        /// </summary>
        public bool Train(string cardId)
        {
            SyncLife();
            if (cardId == null || !_owned.Contains(cardId)) return false;
            if (!Session.SpendTrainingMark()) return false;
            Mastery = Mastery.Train(cardId).Book;
            return true;
        }

        /// <summary>
        /// A learned card is owned at once, and one copy goes into the deck when the deck has room
        /// (encounters_and_rewards_v4.md §4.3: 「デッキの上限の中ですぐ戦闘デッキに入ります」).
        /// </summary>
        private void Take(string cardId)
        {
            _owned.Add(cardId);
            if (!DeckRules.CanAdd(_deck, cardId, _owned)) return;
            _learnedCopies++;
            _deck.Add(new CardInstance($"{cardId}-learned-{_learnedCopies}", CardCatalog.ById(cardId)));
        }

        /// <summary>
        /// The cards the last won encounter's pages turned into (§4.3), one pick at a time: while no other
        /// pick waits, the next one lays the pile's three out in <see cref="Offer"/>. The pile is dealt
        /// only then, so it leaves out the card just picked. When every pile up to the layer is spent, the
        /// card becomes a consumable, as a card the reward cannot deal does (BattleRewards.Roll).
        /// </summary>
        private void DealTurnedPages()
        {
            while (_pileDeals > 0 && _offer.Count == 0)
            {
                _pileDeals--;
                IReadOnlyList<string> cards = CardPile.Deal(_pileLayer, _owned, _pileRng);
                if (cards.Count > 0)
                {
                    _offer = cards.ToList();
                    continue;
                }
                string consumable = BattleRewards.DrawConsumable(_pileLayer, _pileRng);
                _consumables.Add(consumable);
                Stow(consumable);
            }
        }

        /// <summary>A consumable found: into a free slot, or onto the list waiting for a swap.</summary>
        private void Stow(string consumableId)
        {
            ExplorationState state = Session.State;
            if (!state.IsOver && !state.Loadout.ConsumableSlotsFull && Session.TakeConsumable(RunStart.Consumable(consumableId))) return;
            _waiting.Add(consumableId);
        }

        /// <summary>A new life (ExplorationSession's 「新しい生」) starts from what the expedition set out with.</summary>
        private void SyncLife()
        {
            if (_life != Session.Life) BeginLife();
        }

        private void BeginLife()
        {
            _life = Session.Life;
            _deck = _startDeck.ToList();
            _owned.Clear();
            _owned.UnionWith(InitialKinds);
            _owned.UnionWith(_startDeck.Select(c => c.Def.Id));
            _journal.Clear();
            foreach (var pair in _startJournal) _journal[pair.Key] = pair.Value;
            _offer = new List<string>();
            _consumables.Clear();
            _waiting.Clear();
            _pages = 0;
            _pileDeals = 0;
            _pileRng = null;
            _learnedCopies = 0;
            Battle = null;
            Mastery = MasteryBook.Begin(_talent);
        }

        /// <summary>The four in-battle tools, summed over the slots (the same tool may fill two), as a Departure the core reads.</summary>
        private static Departure BattleTools(IReadOnlyList<CardInstance> deck, IReadOnlyList<string> toolIds, string talent)
        {
            int cellsBack = 0, stamina = 0, guard = 0, ward = 0;
            foreach (string id in toolIds)
            {
                ItemDef tool = ItemCatalogue.Tool(id);
                switch (tool.Effect)
                {
                    case ItemEffect.WiderStart: cellsBack += tool.Amount; break;
                    case ItemEffect.OpeningStamina: stamina += tool.Amount; break;
                    case ItemEffect.OpeningGuard: guard += tool.Amount; break;
                    case ItemEffect.BluntBossState: ward += tool.Amount; break;
                    default:
                        throw new ArgumentException(
                            $"Expedition: {tool.Name} acts on the exploration; it goes in RunStart.Loadout", nameof(toolIds));
                }
            }
            return new Departure(deck, toolIds.ToList(), Array.Empty<string>(), cellsBack, talent, stamina, guard, ward);
        }
    }
}
