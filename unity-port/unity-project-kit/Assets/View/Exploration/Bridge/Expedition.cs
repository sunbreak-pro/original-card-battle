// One life in the dungeon as both cores see it (#99): the exploration session, and what the battles of
// the life share — the deck, the cards owned, the mastery book, the in-battle tools and what the
// journal has learned of each enemy. It hands a combat node's battle in (EncounterBattle) and takes
// its result back: HP and stamina into the exploration core, the ticks into the mastery book, the
// 開示度 into the journal, the unique cards into the cards owned and the deck. Pure C#; the View that
// loads the battle scene only forwards the source and the end of each fight.
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
        private List<string> _offer = new List<string>();
        private int _life;
        private int _learnedCopies;

        /// <param name="start">How the exploration starts (seed, HP, stamina, gauge, exploration tools).</param>
        /// <param name="deck">The deck the life sets out with; null for the slice's prototype deck.</param>
        /// <param name="battleToolIds">
        /// The tools in the slots that act inside a battle (間合いの履, 息継ぎの笛, 竜鱗の欠片, 竜語の符;
        /// tools_and_consumables_v4.md §2), as catalogue ids. The tools that act on the exploration go in
        /// <see cref="RunStart.Loadout"/>; turning one chosen loadout into both is 出立's (#104).
        /// </param>
        /// <param name="talent">才能: the card whose battle ticks count twice (§17.8), or null.</param>
        /// <param name="intel">What the exploration screen says about the enemy behind a node.</param>
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
            Session = new ExplorationSession(start, intel);
            BeginLife();
        }

        public ExplorationSession Session { get; }

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

        /// <summary>A real elite's cards waiting for the player to pick one (<see cref="Learn"/>). Empty when none waits.</summary>
        public IReadOnlyList<string> Offer
        {
            get
            {
                SyncLife();
                return _offer;
            }
        }

        /// <summary>The encounter being fought, or null.</summary>
        public EncounterBattle Battle { get; private set; }

        /// <summary>The journal's 開示度 for an enemy: 0 until it is met, 1 after a battle with it, 2 after it is observed.</summary>
        public int DisclosureOf(string enemyId)
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
        /// against it), and a won encounter's unique cards to the cards owned.
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

            UniqueCardReward reward = battle.Won
                ? UniqueCards.For(battle.HandIn.Kind, battle.HandIn.Encounter, _owned)
                : UniqueCardReward.None;
            foreach (string id in reward.Learned) Take(id);
            _offer = reward.Offered.ToList();

            Session.ReportBattle(battle.HpAfter, battle.StaminaAfter);

            return new BattleReturn(
                battle.Won, battle.HpAfter, battle.StaminaAfter, MasteryTally.Of(plays), ranks,
                disclosure, observed, reward.Learned, reward.Offered);
        }

        /// <summary>
        /// The battle could not be shown (the View found no battle scene to play it in): it is dropped
        /// unfought, and the node still waits on a result.
        /// </summary>
        public void Abandon()
        {
            Battle = null;
        }

        /// <summary>Picks one card of a real elite's offer (encounters_and_rewards_v4.md §4.2). False when it is not on offer.</summary>
        public bool Learn(string cardId)
        {
            SyncLife();
            if (cardId == null || !_offer.Contains(cardId)) return false;
            Take(cardId);
            _offer.Clear();
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
