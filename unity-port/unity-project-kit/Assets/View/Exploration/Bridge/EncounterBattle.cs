// The battle behind one combat node (#99): what the exploration hands in, the fights it is fought as,
// and what comes back. Pure C#: the one place besides Expedition that sees both cores' types, so the
// View that loads the battle scene only forwards the source and the end of each fight.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction.Bridge;
using DungeonCore;

namespace Exploration.Bridge
{
    /// <summary>
    /// Everything a combat node hands the battle (#99): the layer and the node's kind, who is fought
    /// (the line of enemies of each fight), the cells each fight is fought on, HP and current stamina,
    /// the max stamina the exploration's 瘴気 and temporary changes leave, the journal's 開示度 for each
    /// enemy, the deck as this life has grown it, and the in-battle tools as amounts.
    /// </summary>
    public sealed class BattleHandIn
    {
        /// <param name="limits">
        /// Trial extension (js: bridge/exploration/EncounterBattle.js:24, docs/EXTENSIONS.md 「出立の枠（DeckLimits）」):
        /// the life's deck limits, which <see cref="EncounterBattle.NextLaunch"/> hands to the launch. Null: §8's.
        /// </param>
        // js: bridge/exploration/EncounterBattle.js:24
        public BattleHandIn(
            int layer,
            NodeKind kind,
            Encounter encounter,
            IReadOnlyList<int> fieldCells,
            int hp,
            int maxHp,
            int stamina,
            int maxStamina,
            int miasmaPercent,
            IReadOnlyDictionary<string, int> disclosure,
            IReadOnlyList<CardInstance> deck,
            Departure tools,
            int seed,
            DeckLimits limits = null)
        {
            Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            FieldCells = fieldCells ?? throw new ArgumentNullException(nameof(fieldCells));
            Disclosure = disclosure ?? throw new ArgumentNullException(nameof(disclosure));
            Deck = deck ?? throw new ArgumentNullException(nameof(deck));
            Tools = tools ?? throw new ArgumentNullException(nameof(tools));
            if (fieldCells.Count != encounter.Fights.Count)
                throw new ArgumentException("one line per fight", nameof(fieldCells));
            string unknown = encounter.EnemyIds.FirstOrDefault(id => !disclosure.ContainsKey(id));
            if (unknown != null)
                throw new ArgumentException($"no 開示度 for {unknown}: every enemy of the encounter needs one", nameof(disclosure));
            Layer = layer;
            Kind = kind;
            Hp = hp;
            MaxHp = maxHp;
            Stamina = stamina;
            MaxStamina = maxStamina;
            MiasmaPercent = miasmaPercent;
            Seed = seed;
            Limits = limits;
        }

        /// <summary>The floor: 1〜7.</summary>
        public int Layer { get; }

        public NodeKind Kind { get; }

        /// <summary>The enemy line: each fight's enemies in phase order, the fights in the order they are fought.</summary>
        public Encounter Encounter { get; }

        /// <summary>The cells of each fight (seven_layers_v4.md §2.4), widened until its enemies stand.</summary>
        public IReadOnlyList<int> FieldCells { get; }

        public int Hp { get; }

        public int MaxHp { get; }

        public int Stamina { get; }

        /// <summary>The exploration's max stamina (DungeonCore.ExplorationState.MaxStamina): 瘴気 and the temporary changes.</summary>
        public int MaxStamina { get; }

        /// <summary>The gauge itself, for the record. The battle reads it only through <see cref="MaxStamina"/>.</summary>
        public int MiasmaPercent { get; }

        /// <summary>The journal's 開示度 by enemy id, for every enemy of the encounter (−1〜2).</summary>
        public IReadOnlyDictionary<string, int> Disclosure { get; }

        /// <summary>The deck every fight is shuffled from, each card as this life's mastery has grown it.</summary>
        public IReadOnlyList<CardInstance> Deck { get; }

        /// <summary>The in-battle tools (§9.1, #452) as BattleCore reads them: the start a tool moved and three amounts.</summary>
        public Departure Tools { get; }

        /// <summary>The first fight's seed; each next fight takes one of its own from it.</summary>
        public int Seed { get; }

        /// <summary>
        /// Trial extension (js: bridge/exploration/EncounterBattle.js:58): the deck limits of the life, or null for
        /// §8's constants. <see cref="EncounterBattle.NextLaunch"/> hands them to the BattleLaunch (as Limits).
        /// </summary>
        public DeckLimits Limits { get; }
    }

    /// <summary>
    /// What a finished encounter brings back to the exploration (#99): HP and current stamina, whether
    /// the player fell, the mastery ticks and the steps they bought, what the journal learned (開示度,
    /// raised as the battle ends), the unique cards it taught and the rest of its reward (#625).
    /// </summary>
    public sealed class BattleReturn
    {
        public BattleReturn(
            bool won,
            int hp,
            int stamina,
            MasteryTally ticks,
            IReadOnlyList<RankUp> rankUps,
            IReadOnlyDictionary<string, int> disclosure,
            IReadOnlyList<string> observed,
            IReadOnlyList<string> learned,
            IReadOnlyList<string> offered,
            BattleReward reward = null)
        {
            Won = won;
            Hp = hp;
            Stamina = stamina;
            Ticks = ticks ?? throw new ArgumentNullException(nameof(ticks));
            RankUps = rankUps ?? Array.Empty<RankUp>();
            Disclosure = disclosure ?? new Dictionary<string, int>();
            Observed = observed ?? Array.Empty<string>();
            Learned = learned ?? Array.Empty<string>();
            Offered = offered ?? Array.Empty<string>();
            Reward = reward ?? BattleReward.None;
        }

        /// <summary>Every fight of the encounter was won.</summary>
        public bool Won { get; }

        /// <summary>HP reached 0 in a fight: the life ends there (RunPhase.Fallen).</summary>
        public bool Died => Hp <= 0;

        public int Hp { get; }

        public int Stamina { get; }

        /// <summary>The ticks the fights gave, by card kind, before 才能 doubles any (§3.2).</summary>
        public MasteryTally Ticks { get; }

        /// <summary>The cards that went up a step when the fights ended (§17.8).</summary>
        public IReadOnlyList<RankUp> RankUps { get; }

        /// <summary>The journal's 開示度 for each enemy fought, after the battle (encounter 1, observation 2).</summary>
        public IReadOnlyDictionary<string, int> Disclosure { get; }

        /// <summary>The enemies a 観察 raised to 2 in this battle.</summary>
        public IReadOnlyList<string> Observed { get; }

        /// <summary>Unique cards learned (a boss's three, a stand-in boss's two).</summary>
        public IReadOnlyList<string> Learned { get; }

        /// <summary>A real elite's cards to pick one from (Expedition.Learn).</summary>
        public IReadOnlyList<string> Offered { get; }

        /// <summary>
        /// The rest of a won encounter's reward (encounters_and_rewards_v4.md §4.2): the pile's cards to
        /// pick one from (Expedition.Learn), consumables and pages. None for a lost encounter.
        /// </summary>
        public BattleReward Reward { get; }
    }

    /// <summary>One fight as it ended: its enemies, the final state and every event it emitted.</summary>
    public sealed class FoughtBattle
    {
        public FoughtBattle(Fight fight, BattleState state, IReadOnlyList<BattleEvent> history)
        {
            Fight = fight;
            State = state;
            History = history;
        }

        public Fight Fight { get; }

        public BattleState State { get; }

        public IReadOnlyList<BattleEvent> History { get; }

        /// <summary>A 観察 was played (concept-v3 §14: it raises the journal to 2 once the battle ends).</summary>
        public bool Observed => History.OfType<CardPlayed>()
            .Any(played => played.Actor == Actor.Player && played.Card.Def.Id == CardCatalog.Observe.Id);
    }

    /// <summary>
    /// The fights of one combat node, fought in order. A 2-fight encounter carries HP and current
    /// stamina from the first fight into the second (BattleCore.Chain.Carry); a lost fight ends it.
    /// </summary>
    public sealed class EncounterBattle
    {
        /// <summary>The step between two fights' seeds, the same prime DemoSession steps its retries by.</summary>
        private const int FightSeedStep = 7919;

        private readonly List<FoughtBattle> _fought = new List<FoughtBattle>();
        private int? _hp;
        private int? _stamina;
        private bool _fighting;

        public EncounterBattle(BattleHandIn handIn)
        {
            HandIn = handIn ?? throw new ArgumentNullException(nameof(handIn));
        }

        public BattleHandIn HandIn { get; }

        /// <summary>The fights fought so far, in order.</summary>
        public IReadOnlyList<FoughtBattle> Fought => _fought;

        /// <summary>The fight to start next (0-based).</summary>
        public int Index => _fought.Count;

        public int Count => HandIn.Encounter.Fights.Count;

        /// <summary>A fight was lost, or every fight was won.</summary>
        public bool Over => Lost || _fought.Count == Count;

        public bool Lost => _fought.Count > 0 && _fought[_fought.Count - 1].State.Result == GameResult.Lost;

        public bool Won => _fought.Count == Count && !Lost;

        /// <summary>
        /// The next fight as the battle scene is started with it. <see cref="BattleLaunch.Disclosure"/>
        /// is the journal's 開示度 handed to the omen (#500): for a fight of two enemies the lower of
        /// the two, so the omen never shows a number the journal has not learned for either.
        /// </summary>
        public BattleLaunch NextLaunch()
        {
            if (Over) throw new InvalidOperationException("EncounterBattle: every fight has been fought.");
            Fight fight = HandIn.Encounter.Fights[Index];
            Departure tools = HandIn.Tools;
            return new BattleLaunch
            {
                EnemyId = fight.EnemyIds[0],
                MoreEnemyIds = fight.EnemyIds.Skip(1).ToList(),
                Seed = unchecked(HandIn.Seed + (Index * FightSeedStep)),
                FieldCells = HandIn.FieldCells[Index],
                PlayerStartCell = tools.PlayerStartCell,
                StartGap = tools.StartGap,
                Disclosure = fight.EnemyIds.Min(id => HandIn.Disclosure[id]),
                Deck = new List<CardInstance>(HandIn.Deck),
                PlayerMaxHp = HandIn.MaxHp,
                MaxStamina = HandIn.MaxStamina,
                StartHp = _hp ?? HandIn.Hp,
                StartStamina = _stamina ?? HandIn.Stamina,
                StartStaminaBonus = tools.StartStaminaBonus,
                FirstHitGuard = tools.FirstHitGuard,
                BossWordWard = tools.BossWordWard,
                ChainIndex = Index + 1,
                ChainTotal = Count,
                // Trial extension (js: bridge/exploration/EncounterBattle.js:211): a life's deck limits (a survivor's
                // 45-card deck) ride to the launch, which holds the deck to them (BattleLaunch.BuildDeck). Null: §8's.
                Limits = HandIn.Limits,
            };
        }

        /// <summary>
        /// Starts the next fight: the source the battle scene's DepictionPlayer plays.
        /// <paramref name="suggestCards"/> is for unattended runs (BattleLaunch.AutoPlay).
        /// </summary>
        public CoreBattleSource StartNext(bool suggestCards = false)
        {
            if (_fighting) throw new InvalidOperationException("EncounterBattle: a fight is already being fought.");
            BattleLaunch launch = NextLaunch();
            launch.AutoPlay = suggestCards;
            CoreBattleSource source = launch.CreateSource();
            _fighting = true;
            return source;
        }

        /// <summary>
        /// The fight started last, if it has ended: records it and returns true. False, recording
        /// nothing, when the battle never reached its end (the scene had no player to play it out), so
        /// the View that loads the battle scene never reads the core's result itself.
        /// </summary>
        public bool TryFinish(CoreBattleSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.State.Result == GameResult.Ongoing) return false;
            Finish(source);
            return true;
        }

        /// <summary>The fight started last has ended.</summary>
        public void Finish(CoreBattleSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Finish(source.State, source.History);
        }

        /// <summary>The fight started last has ended, as its final state and its events.</summary>
        public void Finish(BattleState finished, IReadOnlyList<BattleEvent> history)
        {
            if (finished == null) throw new ArgumentNullException(nameof(finished));
            if (history == null) throw new ArgumentNullException(nameof(history));
            if (!_fighting) throw new InvalidOperationException("EncounterBattle: no fight is being fought.");
            if (finished.Result == GameResult.Ongoing) throw new InvalidOperationException("EncounterBattle: the fight has not ended.");

            _fighting = false;
            _fought.Add(new FoughtBattle(HandIn.Encounter.Fights[Index], finished, history.ToList()));
            var carried = BattleCore.Chain.Carry(finished);
            _hp = carried.Hp;
            _stamina = carried.Stamina;
        }

        /// <summary>HP after the last fight (0 when it was lost), or the HP handed in before any fight.</summary>
        public int HpAfter => _fought.Count == 0 ? HandIn.Hp : Lost ? 0 : _hp ?? HandIn.Hp;

        /// <summary>Current stamina after the last fight, or the stamina handed in before any fight.</summary>
        public int StaminaAfter => _stamina ?? HandIn.Stamina;
    }
}
