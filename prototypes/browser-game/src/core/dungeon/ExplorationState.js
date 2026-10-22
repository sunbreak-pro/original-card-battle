// Port of unity-port/DungeonCore/ExplorationState.cs
import { defineEnum } from '../../shared/cs.js';
import { argNull } from './_errors.js';
import { Miasma } from './Miasma.js';

/**
 * Where a run currently stands. Only one of these is ever true at a time.
 *
 * Exploring     Inside a layer with 刻限 left.
 * LayerCleared  The layer boss is down. The way on is the interlude.
 * PushedOut     刻限 ran out. The layer's actions are over, boss or not (§4.3).
 * Interlude     階層間の休憩. No 刻限, no 瘴気 (concept-v3.md §7).
 * MiasmaDeath   The gauge hit 100%.
 * Fallen        HP reached 0 in a battle. The battle core reports this in.
 * Completed     The last layer's boss is down.
 * Survived      The player closed the life alive: the 生存ルート (concept-v3.md §8.1). What the next
 *               life receives from it is #108's; the run only records that it ended this way.
 */
export const RunPhase = defineEnum([
  'Exploring', 'LayerCleared', 'PushedOut', 'Interlude', 'MiasmaDeath', 'Fallen', 'Completed', 'Survived',
]);

/**
 * A 休息 node offers one of two things for its single 刻限, never both
 * (dungeon_exploration_v4.md §3.2).
 *
 * Rest   HP back, stamina full, max stamina up.
 * Train  One notch of mastery on a chosen card instead.
 */
export const RestChoice = defineEnum(['Rest', 'Train']);

/** IReadOnlyCollection<int>.Contains — a Set in practice, an array accepted too. */
function contains(collection, value) {
  return collection instanceof Set ? collection.has(value) : collection.includes(value);
}

/**
 * One layer's exploration state. Immutable: every transition returns a new one, so a run
 * can be replayed from its seed and its inputs, the same way the battle core works.
 */
export class ExplorationState {
  /**
   * @param {import('./SevenLayers.js').LayerProfile} profile
   * @param {import('./LayerMap.js').LayerMap} map
   * @param {number} currentNodeId
   * @param {Set<number>} resolved
   * @param {number} timeLeft
   * @param {number} miasmaPercent
   * @param {number} hp
   * @param {number} maxHp
   * @param {number} stamina
   * @param {number} tempMaxStaminaMod
   * @param {number} trainingMarks
   * @param {import('./RunLoadout.js').RunLoadout} loadout
   * @param {string} phase RunPhase
   * @param {boolean} [trial] JS extension (not in the C#; docs/EXTENSIONS.md 「探索の持ち物とイベント」):
   *   the run plays the trial rules — every carried item acts and イベント nodes resolve. False is the C#.
   */
  constructor(profile, map, currentNodeId, resolved, timeLeft, miasmaPercent, hp, maxHp, stamina,
    tempMaxStaminaMod, trainingMarks, loadout, phase, trial = false) {
    if (profile === null || profile === undefined) throw argNull('profile');
    this.Profile = profile;
    if (map === null || map === undefined) throw argNull('map');
    this.Map = map;
    if (resolved === null || resolved === undefined) throw argNull('resolved');
    /** Nodes already spent a 刻限 on. Walking back over them is free (§4.2). */
    this.Resolved = resolved;
    if (loadout === null || loadout === undefined) throw argNull('loadout');
    this.Loadout = loadout;
    this.CurrentNodeId = currentNodeId;
    this.TimeLeft = timeLeft;
    this.MiasmaPercent = miasmaPercent;
    this.Hp = hp;
    this.MaxHp = maxHp;
    this.Stamina = stamina;
    /** Heavy packs, strange meals, a rest. Clamped to ±4 when max stamina is read. */
    this.TempMaxStaminaMod = tempMaxStaminaMod;
    /** Notches earned at 習熟訓練 nodes this layer. */
    this.TrainingMarks = trainingMarks;
    this.Phase = phase;
    /** JS extension: the trial rules are on (ExplorationReducer.Enter's `trial`). */
    this.Trial = trial === true;
  }

  /**
   * JS extension (trial mode): the layer's 刻限 with 刻限の砂 counted — 「どの層でも刻限 +1」
   * (tools_and_consumables_v4.md §2). Outside trial this is the profile's, as in the C#.
   */
  get TimeLimit() { return this.Profile.TimeLimit + this.TimeBonus; }

  /** JS extension: 刻限 刻限の砂 adds to every layer (0 outside trial). */
  get TimeBonus() { return this.Trial ? this.Loadout.TimeBonus : 0; }

  /** What one 刻限 in this layer costs after the run's tools are counted. */
  get EffectiveDensity() { return Miasma.EffectiveDensity(this.Profile.Density, this.Loadout.DensityRelief); }

  /**
   * The max stamina the next battle starts from. The battle's own 瘴気纏い is applied on
   * top of this inside the battle and never written back (battle_core_v4.md §6.2).
   */
  get MaxStamina() { return Miasma.MaxStamina(this.TempMaxStaminaMod, this.MiasmaPercent); }

  get IsOver() {
    return this.Phase === RunPhase.MiasmaDeath || this.Phase === RunPhase.Fallen || this.Phase === RunPhase.Completed ||
      this.Phase === RunPhase.Survived;
  }

  HasResolved(nodeId) { return contains(this.Resolved, nodeId); }

  /** Nodes the player could still spend a 刻限 on from where they stand. */
  ReachableUnresolved() {
    return this.Map.Neighbours(this.CurrentNodeId).filter((id) => !this.HasResolved(id));
  }

  /**
   * C# `With(profile: …, map: …, currentNodeId: …, …)` with named optional arguments →
   * `With({ profile, map, currentNodeId, resolved, timeLeft, miasmaPercent, hp, maxHp, stamina,
   * tempMaxStaminaMod, trainingMarks, loadout, phase })`. A missing or null field keeps the
   * current value (the C# `??`). JS extension: `trial` as well.
   */
  With(changes = {}) {
    const c = changes;
    return new ExplorationState(
      c.profile ?? this.Profile,
      c.map ?? this.Map,
      c.currentNodeId ?? this.CurrentNodeId,
      c.resolved ?? this.Resolved,
      c.timeLeft ?? this.TimeLeft,
      c.miasmaPercent ?? this.MiasmaPercent,
      c.hp ?? this.Hp,
      c.maxHp ?? this.MaxHp,
      c.stamina ?? this.Stamina,
      c.tempMaxStaminaMod ?? this.TempMaxStaminaMod,
      c.trainingMarks ?? this.TrainingMarks,
      c.loadout ?? this.Loadout,
      c.phase ?? this.Phase,
      c.trial ?? this.Trial);
  }

  /** Public properties, in declaration order, for the parity JSON. */
  toJSON() {
    return {
      $type: 'ExplorationState',
      Profile: this.Profile,
      Map: this.Map,
      CurrentNodeId: this.CurrentNodeId,
      Resolved: this.Resolved,
      TimeLeft: this.TimeLeft,
      MiasmaPercent: this.MiasmaPercent,
      Hp: this.Hp,
      MaxHp: this.MaxHp,
      Stamina: this.Stamina,
      TempMaxStaminaMod: this.TempMaxStaminaMod,
      TrainingMarks: this.TrainingMarks,
      Loadout: this.Loadout,
      Phase: this.Phase,
      EffectiveDensity: this.EffectiveDensity,
      MaxStamina: this.MaxStamina,
      IsOver: this.IsOver,
    };
  }
}
