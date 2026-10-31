// Port of unity-project-kit/Assets/View/Exploration/Bridge/EncounterBattle.cs
// The battle behind one combat node (#99): what the exploration hands in, the fights it is fought as,
// and what comes back. Pure: the one place besides Expedition that sees both cores' types, so the
// View that loads the battle scene only forwards the source and the end of each fight.
import { min, ArgumentException, ArgumentNullException, InvalidOperationException } from '../../shared/cs.js';
import { Actor, GameResult } from '../../core/battle/Types.js';
import { CardPlayed } from '../../core/battle/BattleEvents.js';
import { CardCatalog } from '../../core/battle/CardCatalog.js';
import { Chain } from '../../core/battle/Chain.js';
import { BattleLaunch } from '../depiction/BattleLaunch.js';
import { BattleReward } from './BattleRewards.js';

function nullArg(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

/**
 * Everything a combat node hands the battle (#99): the layer and the node's kind, who is fought
 * (the line of enemies of each fight), the cells each fight is fought on, HP and current stamina,
 * the max stamina the exploration's 瘴気 and temporary changes leave, the journal's 開示度 for each
 * enemy, the deck as this life has grown it, and the in-battle tools as amounts.
 */
export class BattleHandIn {
  constructor(layer, kind, encounter, fieldCells, hp, maxHp, stamina, maxStamina, miasmaPercent, disclosure, deck, tools, seed, limits = null) {
    if (encounter == null) throw nullArg('encounter');
    if (fieldCells == null) throw nullArg('fieldCells');
    if (disclosure == null) throw nullArg('disclosure');
    if (deck == null) throw nullArg('deck');
    if (tools == null) throw nullArg('tools');
    /** The enemy line: each fight's enemies in phase order, the fights in the order they are fought. */
    this.Encounter = encounter;
    /** The cells of each fight (seven_layers_v4.md §2.4), widened until its enemies stand. */
    this.FieldCells = fieldCells;
    /** The journal's 開示度 by enemy id (Map), for every enemy of the encounter (−1〜2). */
    this.Disclosure = disclosure;
    /** The deck every fight is shuffled from, each card as this life's mastery has grown it. */
    this.Deck = deck;
    /** The in-battle tools (§9.1, #452) as BattleCore reads them: the start a tool moved and three amounts. */
    this.Tools = tools;
    if (fieldCells.length !== encounter.Fights.length)
      throw new ArgumentException("one line per fight (Parameter 'fieldCells')");
    /** The floor: 1〜7. */
    this.Layer = layer;
    this.Kind = kind;
    this.Hp = hp;
    this.MaxHp = maxHp;
    this.Stamina = stamina;
    /** The exploration's max stamina (DungeonCore.ExplorationState.MaxStamina): 瘴気 and the temporary changes. */
    this.MaxStamina = maxStamina;
    /** The gauge itself, for the record. The battle reads it only through MaxStamina. */
    this.MiasmaPercent = miasmaPercent;
    /** The first fight's seed; each next fight takes one of its own from it. */
    this.Seed = seed;
    /**
     * JS extension (docs/EXTENSIONS.md, the limits job's DeckLimits): the deck limits of the life, or null
     * for the C# constants. NextLaunch hands them to the BattleLaunch (as `Limits`).
     */
    this.Limits = limits;
  }

  toJSON() {
    return {
      $type: 'BattleHandIn',
      Layer: this.Layer,
      Kind: this.Kind,
      Encounter: this.Encounter,
      FieldCells: this.FieldCells,
      Hp: this.Hp,
      MaxHp: this.MaxHp,
      Stamina: this.Stamina,
      MaxStamina: this.MaxStamina,
      MiasmaPercent: this.MiasmaPercent,
      Disclosure: this.Disclosure,
      Deck: this.Deck,
      Tools: this.Tools,
      Seed: this.Seed,
    };
  }
}

/**
 * What a finished encounter brings back to the exploration (#99): HP and current stamina, whether
 * the player fell, the mastery ticks and the steps they bought, what the journal learned (開示度,
 * raised as the battle ends), the unique cards it taught and the rest of its reward (#625).
 */
export class BattleReturn {
  constructor(won, hp, stamina, ticks, rankUps, disclosure, observed, learned, offered, reward = null) {
    /** Every fight of the encounter was won. */
    this.Won = won;
    this.Hp = hp;
    this.Stamina = stamina;
    if (ticks == null) throw nullArg('ticks');
    /** The ticks the fights gave, by card kind, before 才能 doubles any (§3.2). */
    this.Ticks = ticks;
    /** The cards that went up a step when the fights ended (§17.8). */
    this.RankUps = rankUps ?? [];
    /** The journal's 開示度 for each enemy fought (Map), after the battle (encounter 1, observation 2). */
    this.Disclosure = disclosure ?? new Map();
    /** The enemies a 観察 raised to 2 in this battle. */
    this.Observed = observed ?? [];
    /** Unique cards learned (a boss's three, a stand-in boss's two). */
    this.Learned = learned ?? [];
    /** A real elite's cards to pick one from (Expedition.Learn). */
    this.Offered = offered ?? [];
    /**
     * The rest of a won encounter's reward (encounters_and_rewards_v4.md §4.2): the pile's cards to
     * pick one from (Expedition.Learn), consumables and pages. None for a lost encounter.
     */
    this.Reward = reward ?? BattleReward.None;
  }

  /** HP reached 0 in a fight: the life ends there (RunPhase.Fallen). */
  get Died() { return this.Hp <= 0; }

  toJSON() {
    return {
      $type: 'BattleReturn',
      Won: this.Won,
      Died: this.Died,
      Hp: this.Hp,
      Stamina: this.Stamina,
      Ticks: this.Ticks,
      RankUps: this.RankUps,
      Disclosure: this.Disclosure,
      Observed: this.Observed,
      Learned: this.Learned,
      Offered: this.Offered,
      Reward: this.Reward,
    };
  }
}

/** One fight as it ended: its enemies, the final state and every event it emitted. */
export class FoughtBattle {
  constructor(fight, state, history) {
    this.Fight = fight;
    this.State = state;
    this.History = history;
  }

  /** A 観察 was played (concept-v3 §14: it raises the journal to 2 once the battle ends). */
  get Observed() {
    return this.History.some((e) => e instanceof CardPlayed
      && e.Actor === Actor.Player && e.Card.Def.Id === CardCatalog.Observe.Id);
  }
}

/** The step between two fights' seeds, the same prime DemoSession steps its retries by. */
const FightSeedStep = 7919;

/**
 * The fights of one combat node, fought in order. A 2-fight encounter carries HP and current
 * stamina from the first fight into the second (BattleCore.Chain.Carry); a lost fight ends it.
 */
export class EncounterBattle {
  constructor(handIn) {
    if (handIn == null) throw nullArg('handIn');
    this.HandIn = handIn;
    this._fought = [];
    /** int? */
    this._hp = null;
    /** int? */
    this._stamina = null;
    this._fighting = false;
  }

  /** The fights fought so far, in order. */
  get Fought() { return this._fought; }

  /** The fight to start next (0-based). */
  get Index() { return this._fought.length; }

  get Count() { return this.HandIn.Encounter.Fights.length; }

  /** A fight was lost, or every fight was won. */
  get Over() { return this.Lost || this._fought.length === this.Count; }

  get Lost() { return this._fought.length > 0 && this._fought[this._fought.length - 1].State.Result === GameResult.Lost; }

  get Won() { return this._fought.length === this.Count && !this.Lost; }

  /**
   * The next fight as the battle scene is started with it. BattleLaunch.Disclosure is the journal's
   * 開示度 handed to the omen (#500): for a fight of two enemies the lower of the two, so the omen
   * never shows a number the journal has not learned for either.
   */
  NextLaunch() {
    if (this.Over) throw new InvalidOperationException('EncounterBattle: every fight has been fought.');
    const fight = this.HandIn.Encounter.Fights[this.Index];
    const tools = this.HandIn.Tools;
    const launch = new BattleLaunch();
    launch.EnemyId = fight.EnemyIds[0];
    launch.MoreEnemyIds = fight.EnemyIds.slice(1);
    launch.Seed = (this.HandIn.Seed + (this.Index * FightSeedStep)) | 0; // unchecked int
    launch.FieldCells = this.HandIn.FieldCells[this.Index];
    launch.PlayerStartCell = tools.PlayerStartCell;
    launch.StartGap = tools.StartGap;
    launch.Disclosure = min(fight.EnemyIds, (id) => (this.HandIn.Disclosure.has(id) ? this.HandIn.Disclosure.get(id) : 0));
    launch.Deck = [...this.HandIn.Deck];
    launch.PlayerMaxHp = this.HandIn.MaxHp;
    launch.MaxStamina = this.HandIn.MaxStamina;
    launch.StartHp = this._hp ?? this.HandIn.Hp;
    launch.StartStamina = this._stamina ?? this.HandIn.Stamina;
    launch.StartStaminaBonus = tools.StartStaminaBonus;
    launch.FirstHitGuard = tools.FirstHitGuard;
    launch.BossWordWard = tools.BossWordWard;
    launch.ChainIndex = this.Index + 1;
    launch.ChainTotal = this.Count;
    // JS extension: a life's deck limits (a survivor's 45-card deck) ride to the launch, which validates
    // the deck (BattleLaunch.BuildDeck reads `Limits` once that file takes it). Unset: the C#.
    if (this.HandIn.Limits != null) launch.Limits = this.HandIn.Limits;
    return launch;
  }

  /**
   * Starts the next fight: the source the battle scene's DepictionPlayer plays.
   * `suggestCards` is for unattended runs (BattleLaunch.AutoPlay).
   */
  StartNext(suggestCards = false) {
    if (this._fighting) throw new InvalidOperationException('EncounterBattle: a fight is already being fought.');
    const launch = this.NextLaunch();
    launch.AutoPlay = suggestCards;
    const source = launch.CreateSource();
    this._fighting = true;
    return source;
  }

  /**
   * The fight started last has ended. Two C# overloads:
   * - Finish(CoreBattleSource source)
   * - Finish(BattleState finished, IReadOnlyList<BattleEvent> history)
   * told apart here by the number of arguments.
   */
  Finish(...args) {
    if (args.length < 2) {
      const source = args[0];
      if (source == null) throw nullArg('source');
      this.Finish(source.State, source.History);
      return;
    }
    const [finished, history] = args;
    if (finished == null) throw nullArg('finished');
    if (history == null) throw nullArg('history');
    if (!this._fighting) throw new InvalidOperationException('EncounterBattle: no fight is being fought.');
    if (finished.Result === GameResult.Ongoing) throw new InvalidOperationException('EncounterBattle: the fight has not ended.');

    this._fighting = false;
    this._fought.push(new FoughtBattle(this.HandIn.Encounter.Fights[this.Index], finished, [...history]));
    const carried = Chain.Carry(finished);
    this._hp = carried.Hp;
    this._stamina = carried.Stamina;
  }

  /** HP after the last fight (0 when it was lost), or the HP handed in before any fight. */
  get HpAfter() { return this._fought.length === 0 ? this.HandIn.Hp : this.Lost ? 0 : this._hp ?? this.HandIn.Hp; }

  /** Current stamina after the last fight, or the stamina handed in before any fight. */
  get StaminaAfter() { return this._stamina ?? this.HandIn.Stamina; }
}

