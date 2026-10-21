// Port of unity-port/DungeonCore/LayerEncounters.cs
import { defineRecord, InvalidOperationException } from '../../shared/cs.js';
import { argException, argNull, argOutOfRange, isNullOrEmpty } from './_errors.js';
import { RngExtensions } from './IRng.js';
import { NodeKind, NodeKindTokens } from './NodeKind.js';
import { SevenLayers } from './SevenLayers.js';

/** HashCode stand-in: .NET's HashCode is seeded per process, so only "equal → same hash" carries over. */
function combineHash(hash, value) {
  return (Math.imul(hash, 31) + value) | 0;
}

function stringHash(s) {
  let h = 0;
  for (let i = 0; i < s.length; i++) h = combineHash(h, s.charCodeAt(i));
  return h;
}

/**
 * One battle: the enemies fought at once, in phase order. One id for a single enemy, two for
 * roster §9's pair (錆槍 in front, 灰弩 one cell behind).
 */
export class Fight extends defineRecord('Fight', ['EnemyIds']) {
  /** @param {string[]} enemyIds */
  constructor(enemyIds) {
    if (enemyIds === null || enemyIds === undefined) throw argNull('enemyIds');
    if (enemyIds.length === 0) throw argException('a fight needs at least one enemy', 'enemyIds');
    if (enemyIds.some(isNullOrEmpty)) throw argException('an enemy id is empty', 'enemyIds');
    // new ReadOnlyCollection<string>(enemyIds.ToArray()): a read-only copy.
    super(Object.freeze(enemyIds.slice()));
  }

  /** Enemy ids as BattleCore.Enemies knows them, in phase order. */
  // EnemyIds — the record field.

  /** "polearm_warped+crossbow_hunter" — the same joiner as BattleCore's -chain switch. */
  ToString() { return this.EnemyIds.join('+'); }

  toString() { return this.ToString(); }

  /**
   * Value equality over the ids, in order. The compiler's record equality would compare the
   * list by reference, so two fights with the same enemies would not be equal.
   */
  Equals(other) {
    if (other === null || other === undefined || !(other instanceof Fight)) return false;
    const a = this.EnemyIds;
    const b = other.EnemyIds;
    if (a.length !== b.length) return false;
    for (let i = 0; i < a.length; i++) if (a[i] !== b[i]) return false;
    return true;
  }

  equals(other) { return this.Equals(other); }

  GetHashCode() {
    let hash = 17;
    for (const id of this.EnemyIds) hash = combineHash(hash, stringHash(id));
    return hash;
  }
}

/**
 * What one combat node holds: a single fight, or two fought back to back with HP and stamina
 * carried over (the 2-fight elite stand-in). The node costs one 刻限 either way.
 */
export class Encounter extends defineRecord('Encounter', ['Fights', 'IsStandIn']) {
  /**
   * @param {Fight[]} fights
   * @param {boolean} isStandIn
   */
  constructor(fights, isStandIn) {
    if (fights === null || fights === undefined) throw argNull('fights');
    if (fights.length === 0) throw argException('an encounter needs at least one fight', 'fights');
    if (fights.some((f) => f === null || f === undefined)) throw argException('a fight is null', 'fights');
    super(Object.freeze(fights.slice()), isStandIn);
  }

  /** The fights in the order they are fought. */
  // Fights — the record field.

  /**
   * True when the enemy in this slot is not the one roster §9 puts in that column on this
   * layer, and the table borrows it from the same layer instead
   * (encounters_and_rewards_v4.md §2.2). That covers an empty §9 slot filled from another
   * column, and a §9 elite moved up to an empty boss slot (門竜 on layer 4, 不退 on layer 5).
   * Cards replaces these once §9 fills the slot.
   */
  // IsStandIn — the record field.

  /** Every enemy id of every fight, in order. (C# IEnumerable, lazily built; an array here.) */
  get EnemyIds() { return this.Fights.flatMap((f) => f.EnemyIds); }

  /** "polearm_warped,shadow_hound" — fights joined the way the -chain switch joins them. */
  ToString() { return this.Fights.map((f) => f.ToString()).join(','); }

  toString() { return this.ToString(); }

  /** Value equality over the fights, in order, and the stand-in flag. */
  Equals(other) {
    if (other === null || other === undefined || !(other instanceof Encounter)) return false;
    if (this.IsStandIn !== other.IsStandIn) return false;
    if (this.Fights.length !== other.Fights.length) return false;
    for (let i = 0; i < this.Fights.length; i++) if (!this.Fights[i].Equals(other.Fights[i])) return false;
    return true;
  }

  equals(other) { return this.Equals(other); }

  GetHashCode() {
    let hash = 17;
    for (const fight of this.Fights) hash = combineHash(hash, fight.GetHashCode());
    hash = combineHash(hash, this.IsStandIn ? 1 : 0);
    return hash;
  }

  /** Record fields plus the public EnemyIds property, for the parity JSON. */
  toJSON() {
    return { $type: 'Encounter', Fights: this.Fights, IsStandIn: this.IsStandIn, EnemyIds: this.EnemyIds };
  }
}

const EMPTY_POOL = Object.freeze([]);

function Freeze(pool, name) {
  if (pool === null || pool === undefined) throw argNull(name);
  if (pool.length === 0) throw argException('a pool needs at least one encounter', name);
  return Object.freeze(pool.slice());
}

/** One layer's pools. Each pool is drawn from uniformly. */
export class LayerEncounterTable {
  /**
   * @param {number} layer
   * @param {Encounter[]} normal
   * @param {Encounter[]} elite
   * @param {Encounter[]} boss
   */
  constructor(layer, normal, elite, boss) {
    if (layer < 1 || layer > SevenLayers.Count) throw argOutOfRange('layer');
    this.Layer = layer;
    /** 通常戦闘. Also what a 死亡地点の痕跡 would replace, once #106 fills it. */
    this.Normal = Freeze(normal, 'normal');
    /** 精鋭. */
    this.Elite = Freeze(elite, 'elite');
    /** 階層ボス. */
    this.Boss = Freeze(boss, 'boss');
  }

  /** The pool a node kind draws from, or an empty list for a kind that holds no fight. */
  PoolFor(kind) {
    if (!NodeKindTokens.IsCombat(kind)) return EMPTY_POOL;
    switch (kind) {
      case NodeKind.Battle: return this.Normal;
      case NodeKind.Elite: return this.Elite;
      case NodeKind.Boss: return this.Boss;
      default: throw new InvalidOperationException(`${kind} holds a fight but no pool is kept for it`);
    }
  }

  /** Public properties, in declaration order, for the parity JSON. */
  toJSON() {
    return { $type: 'LayerEncounterTable', Layer: this.Layer, Normal: this.Normal, Elite: this.Elite, Boss: this.Boss };
  }
}

// roster §2: normals.
const PolearmWarped = 'polearm_warped';        // 錆槍の竜兵, layers 1〜2
const ShadowHound = 'shadow_hound';            // 瘴牙の走竜, layers 1〜2
const RustedRevenant = 'rusted_revenant';      // 瘴甲の竜兵, layers 2〜4
const CrossbowHunter = 'crossbow_hunter';      // 灰弩の竜兵, layers 2〜3
const MistArcher = 'mist_archer';              // 燐弓の竜兵, layers 4〜6
const TwinBladeWarped = 'twin_blade_warped';   // 燐刃の竜兵, layers 4〜7
const PolearmCrystal = 'polearm_crystal';      // 晶槍の竜兵, layers 3〜4

// roster §3: elites.
const ArmoredWarden = 'armored_warden';        // 鉄壁の門竜, layers 4〜5
const PackAlpha = 'pack_alpha';                // 統牙の長竜, layers 5〜7
const PolearmUnyielding = 'polearm_unyielding'; // 不退の槍竜, layers 5〜6

// roster §4〜§6: bosses.
const MiasmaPriest = 'miasma_priest';          // 大黒蛇 セルク, layer 3
const AbyssAngler = 'abyss_angler';            // 獄竜 ガルド, layer 6
const DistortionRoot = 'distortion_root';      // 歪みの根, layer 7

function Single(id, standIn) {
  return new Encounter([new Fight([id])], standIn);
}

/** Normal fights: never stand-ins, since §9 lists every normal per layer. */
function Singles(...ids) {
  return ids.map((id) => Single(id, false));
}

/** Two enemies on the field at once. */
function Pair(standIn, front, back) {
  return new Encounter([new Fight([front, back])], standIn);
}

/** Single fights back to back, with no rest between them. */
function Chain(standIn, ...ids) {
  return new Encounter(ids.map((id) => new Fight([id])), standIn);
}

/**
 * Every 2-fight chain of two different enemies, in both orders: n × (n − 1) chains, so one
 * uniform draw picks the pair and the order alike.
 */
function DistinctChains(...ids) {
  const chains = [];
  for (let i = 0; i < ids.length; i++) {
    for (let j = 0; j < ids.length; j++) {
      if (i === j) continue;
      chains.push(Chain(true, ids[i], ids[j]));
    }
  }
  return chains;
}

function BuildTables() {
  return [
    // Layer 1. §9 has no elite and no boss here: the elite is the two normals back to back,
    // the boss the sturdier normal (瘴牙 80 HP over 錆槍 60).
    new LayerEncounterTable(1,
      Singles(PolearmWarped, ShadowHound),
      [Chain(true, PolearmWarped, ShadowHound)],
      [Single(ShadowHound, true)]),

    // Layer 2. The pair fills the elite slot: 141 HP over two cells, needing 7 cells — the
    // elite field of the layer. The boss is the sturdiest normal (瘴甲 87 HP).
    new LayerEncounterTable(2,
      Singles(PolearmWarped, ShadowHound, RustedRevenant, CrossbowHunter),
      [Pair(true, PolearmWarped, CrossbowHunter)],
      [Single(RustedRevenant, true)]),

    // Layer 3. The pair again in the elite slot; セルク is the real boss.
    new LayerEncounterTable(3,
      Singles(RustedRevenant, CrossbowHunter, PolearmCrystal),
      [Pair(true, PolearmWarped, CrossbowHunter)],
      [Single(MiasmaPriest, false)]),

    // Layer 4. §9's only elite (門竜) takes the boss slot, so the elite slot becomes two
    // different normals back to back. Every ordered pair is listed, so which one comes
    // second (with HP and stamina already spent) is as random as which two are drawn.
    new LayerEncounterTable(4,
      Singles(RustedRevenant, MistArcher, TwinBladeWarped, PolearmCrystal),
      DistinctChains(RustedRevenant, MistArcher, TwinBladeWarped, PolearmCrystal),
      [Single(ArmoredWarden, true)]),

    // Layer 5. §9 lists three elites. 不退 — where the three spear dragoons end up — is the
    // boss, so a node's draw never depends on another node's.
    new LayerEncounterTable(5,
      Singles(MistArcher, TwinBladeWarped),
      [Single(ArmoredWarden, false), Single(PackAlpha, false)],
      [Single(PolearmUnyielding, true)]),

    new LayerEncounterTable(6,
      Singles(MistArcher, TwinBladeWarped),
      [Single(PackAlpha, false), Single(PolearmUnyielding, false)],
      [Single(AbyssAngler, false)]),

    new LayerEncounterTable(7,
      Singles(TwinBladeWarped),
      [Single(PackAlpha, false)],
      [Single(DistortionRoot, false)]),
  ];
}

// private static readonly IReadOnlyList<LayerEncounterTable> Tables = Array.AsReadOnly(BuildTables());
// Built on first use (a C# static initializer), frozen so callers cannot swap a layer out.
let _tables = null;
function Tables() {
  if (_tables === null) _tables = Object.freeze(BuildTables());
  return _tables;
}

/**
 * Who is fought on each combat node of each layer
 * (`.claude/docs/danjeon_document/encounters_and_rewards_v4.md` §2, Issue #102).
 *
 * The enemies and the layers they may appear on come from `enemy_document/enemy_roster_v4.md`
 * §9 (cards lane). This table never moves an enemy to a layer §9 does not put it on; where §9
 * leaves an elite or boss slot empty, the slot borrows an enemy from the same layer and is
 * flagged Encounter.IsStandIn. On layers 4 and 5 that borrowing moves a §9 elite
 * into the boss slot. Enemy ids are plain strings that match BattleCore.Enemies (roster §1.8)
 * — DungeonCore does not reference BattleCore (#97).
 *
 * The exploration screen draws a combat node's encounter from here as the node is reached
 * (Exploration.ExplorationSession.PendingEncounter), and #99 hands it to the battle.
 */
export const LayerEncounters = {
  /** The seven tables, layer 1 first. Read-only: callers cannot swap a layer out. */
  get All() { return Tables(); },

  Of(layer) {
    if (layer < 1 || layer > SevenLayers.Count)
      throw argOutOfRange('layer', layer, `the lair has ${SevenLayers.Count} layers`);
    return Tables()[layer - 1];
  },

  /**
   * The encounter behind a node of this kind on this layer, drawn uniformly from its pool
   * with one rng draw. Null for kinds that hold no fight: rest, survey, event and carving.
   * A trace is null as well until #106 decides what it holds.
   */
  Pick(kind, layer, rng) {
    if (rng === null || rng === undefined) throw argNull('rng');
    const pool = LayerEncounters.Of(layer).PoolFor(kind);
    if (pool.length === 0) return null;
    return pool[RngExtensions.NextInt(rng, 0, pool.length)];
  },
};
