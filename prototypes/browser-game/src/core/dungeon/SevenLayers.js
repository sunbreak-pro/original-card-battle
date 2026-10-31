// Port of unity-port/DungeonCore/SevenLayers.cs
import { sum } from '../../shared/cs.js';
import { argException, argNull, argOutOfRange } from './_errors.js';
import { NodeKind } from './NodeKind.js';
import { LayerMapSpec } from './LayerMapSpec.js';
import { MapGenerator } from './MapGenerator.js';
import { Miasma } from './Miasma.js';

/**
 * One layer's numbers. The values come from
 * `.claude/docs/danjeon_document/seven_layers_v4.md` §2 and §4 (Issue #130).
 */
export class LayerProfile {
  /**
   * @param {number} layer
   * @param {string} name
   * @param {string|null} trueName
   * @param {number} density
   * @param {number} timeLimit
   * @param {LayerMapSpec} mapSpec
   */
  constructor(layer, name, trueName, density, timeLimit, mapSpec) {
    if (layer < 1) throw argOutOfRange('layer');
    if (density < 0) throw argOutOfRange('density');
    if (timeLimit < 1) throw argOutOfRange('timeLimit');
    if (mapSpec === null || mapSpec === undefined) throw argNull('mapSpec');
    if (mapSpec.Layer !== layer)
      throw argException(`layer ${layer} was handed the spec of layer ${mapSpec.Layer}`, 'mapSpec');
    if (timeLimit >= mapSpec.NodeCount)
      throw argException(
        `layer ${layer} has ${timeLimit} 刻限 for ${mapSpec.NodeCount} nodes; a layer must always keep ` +
        'at least one node out of reach (seven_layers_v4.md §2.2)', 'timeLimit');

    this.Layer = layer;
    /** What the layer is called before anything is learned about it. */
    this.Name = name;
    /** The name behind the blanks, if the layer hides one (world-v1.md §5.1). */
    this.TrueName = trueName ?? null;
    /** 瘴気 added per 刻限 spent in this layer. */
    this.Density = density;
    /** 刻限 for this layer. Never carried over to the next (dungeon_exploration_v4.md §3.1). */
    this.TimeLimit = timeLimit;
    this.MapSpec = mapSpec;
  }

  /** 刻限 to reach the boss without detouring. */
  get ShortestPathCost() { return this.MapSpec.ShortestPathCost; }

  get IsLast() { return this.Layer === SevenLayers.Count; }

  /**
   * The name to print. A layer with a hidden name shows the blanks until the clue is
   * picked up, and its true name afterwards (world-v1.md §5.1).
   */
  DisplayName(clueTaken) {
    return clueTaken && this.TrueName !== null ? this.TrueName : this.Name;
  }

  /** This layer's map for the given seed (ulong: number or BigInt). */
  Map(seed) {
    return MapGenerator.Generate(this.MapSpec, seed);
  }

  /** Public properties, in declaration order, for the parity JSON. */
  toJSON() {
    return {
      $type: 'LayerProfile',
      Layer: this.Layer,
      Name: this.Name,
      TrueName: this.TrueName,
      Density: this.Density,
      TimeLimit: this.TimeLimit,
      MapSpec: this.MapSpec,
      ShortestPathCost: this.ShortestPathCost,
      IsLast: this.IsLast,
    };
  }
}

/** Eleven nodes over five rows — the full-sized layer of dungeon_exploration_v4 §3.1. */
function Wide(layer, battles, events, rests, surveys) {
  return new LayerMapSpec(layer, [1, 3, 3, 3, 1], new Map([
    [NodeKind.Battle, battles],
    [NodeKind.Elite, 1],
    [NodeKind.Rest, rests],
    [NodeKind.Survey, surveys],
    [NodeKind.Event, events],
  ]));
}

/** Eight nodes over four rows. Events drop out: there is no 刻限 left for them. */
function Narrow(layer, battles, rests, surveys) {
  return new LayerMapSpec(layer, [1, 3, 3, 1], new Map([
    [NodeKind.Battle, battles],
    [NodeKind.Elite, 1],
    [NodeKind.Rest, rests],
    [NodeKind.Survey, surveys],
  ]));
}

function BuildProfiles() {
  return [
    new LayerProfile(1, '燦光の樹海', null, /* density: */ 1, /* timeLimit: */ 10,
      Wide(1, 3, 2, 2, 1)),

    // Layer two hides its name. One battle is traded for the carving that reveals it.
    new LayerProfile(2, '■■の秘跡', '竜神の秘跡', 1, 10,
      Wide(2, 3, 2, 2, 1).With(NodeKind.Carving, 1)),

    new LayerProfile(3, '大黒蛇の住処', null, 2, 10,
      Wide(3, 3, 2, 2, 1)),

    // From here down the layers narrow: 刻限 falls as density rises, so the deep
    // layers are meant to be run through rather than combed (seven_layers_v4.md §2.2).
    new LayerProfile(4, '輝晶の間', null, 3, 7,
      Narrow(4, 2, 2, 1)),

    new LayerProfile(5, '血染めの死海', null, 4, 7,
      Narrow(5, 2, 2, 1)),

    // A second rest widens the third row to three, so every layer offers two nodes that
    // move max stamina (R2-4, Issue #530). The row count stays, and so does the 刻限 a
    // straight run pays: only the number of nodes left behind grows.
    new LayerProfile(6, '獄雷峡', null, 5, 6,
      Narrow(6, 2, 2, 1)),

    // The void. Density jumps a step because the layer is made of 瘴気 (world-v1.md §4).
    // Its middle row takes an event rather than a second rest: with two rests side by side
    // the spare 刻限 would buy both right before 歪みの根 (seven_layers_v4.md §4.1).
    new LayerProfile(7, '果てなき世界', null, 7, 4,
      new LayerMapSpec(7, [1, 4, 1], new Map([
        [NodeKind.Battle, 1],
        [NodeKind.Elite, 1],
        [NodeKind.Rest, 1],
        [NodeKind.Event, 1],
      ]))),
  ];
}

/**
 * The seven layers of the dragon's lair, with the density and 刻限 redrawn from five
 * floors to seven (Issue #130). Names and order come from `vision/world-v1.md` §4.
 *
 * Running straight down costs 89% of the gauge, which leaves max stamina 6 at 歪みの根 —
 * the condition `battle_core_v4.md` §13 already sets a win-rate target for.
 */
export const SevenLayers = {
  Count: 7,

  /** The layer that hides its name until the carving is found. */
  SacramentLayer: 2,

  /** The layer 歪みの根 and, once unlocked, 根の裂け目 sit on. */
  RootLayer: 7,

  get All() { return Profiles(); },

  Of(layer) {
    if (layer < 1 || layer > SevenLayers.Count)
      throw argOutOfRange('layer', layer, `the lair has ${SevenLayers.Count} layers`);
    return Profiles()[layer - 1];
  },

  /** 瘴気 a run picks up walking straight down every layer, with no relief. */
  BareMinimumMiasma(densityRelief = 0) {
    return sum(Profiles(), (p) => Miasma.EffectiveDensity(p.Density, densityRelief) * p.ShortestPathCost);
  },

  /** 瘴気 a run picks up spending every 刻限 of every layer. */
  FullSpendMiasma(densityRelief = 0) {
    return sum(Profiles(), (p) => Miasma.EffectiveDensity(p.Density, densityRelief) * p.TimeLimit);
  },
};

// static readonly LayerProfile[] Profiles = BuildProfiles(): built on first use, like a C#
// static initializer, so module evaluation order never matters.
let _profiles = null;
function Profiles() {
  if (_profiles === null) _profiles = BuildProfiles();
  return _profiles;
}
