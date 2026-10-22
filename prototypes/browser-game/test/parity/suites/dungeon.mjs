// Mirrors oracle/src/Suites/Dungeon.cs. ulong arguments (seeds) are passed as BigInt.
import { Try, enumValues, arr, readStatic, range, outPair } from '../lib/cs.mjs';

export const imports = [
  'src/core/dungeon/SevenLayers.js',
  'src/core/dungeon/MapValidator.js',
  'src/core/dungeon/MapGenerator.js',
  'src/core/dungeon/LayerMapSpec.js',
  'src/core/dungeon/ExplorationReducer.js',
  'src/core/dungeon/ExplorationState.js',
  'src/core/dungeon/LayerEncounters.js',
  'src/core/dungeon/IRng.js',
  'src/core/dungeon/BattleField.js',
  'src/core/dungeon/NodeKind.js',
  'src/core/dungeon/Miasma.js',
  'src/core/dungeon/RunLoadout.js',
  'src/core/dungeonContent/ItemCatalogue.js',
  'src/core/dungeonContent/ItemEffect.js',
  'src/core/dungeonContent/Loadout.js',
  'src/bridge/explorationScript/ExplorationSession.js',
  'src/bridge/exploration/Expedition.js',
];

const ENCOUNTER_SEEDS = 20;

export function run(c, { use, expected }) {
  const kind = c.id.slice(0, c.id.indexOf(':'));
  const D = (rel, ...names) => use(rel, ...names);
  switch (kind) {
    case 'map': {
      const { SevenLayers } = D('src/core/dungeon/SevenLayers.js', 'SevenLayers');
      const { MapValidator } = D('src/core/dungeon/MapValidator.js', 'MapValidator');
      const profile = SevenLayers.Of(c.input.Layer);
      const map = profile.Map(BigInt(c.input.Seed));
      const nodes = arr(map.Nodes);
      return {
        Map: map,
        Validate: Try(() => {
          MapValidator.Validate(map, profile.MapSpec);
          return 'ok';
        }),
        Rows: range(0, map.RowCount).map((r) => arr(map.Row(r)).map((n) => n.Id)),
        Neighbours: nodes.map((n) => map.Neighbours(n.Id)),
        Predecessors: nodes.map((n) => map.Predecessors(n.Id)),
      };
    }
    case 'mapspec': {
      const { LayerMapSpec } = D('src/core/dungeon/LayerMapSpec.js', 'LayerMapSpec');
      const { MapGenerator } = D('src/core/dungeon/MapGenerator.js', 'MapGenerator');
      const spec = c.input.Spec === 'Minimal' ? LayerMapSpec.Minimal(1) : LayerMapSpec.Full(1);
      return { Spec: spec, Map: Try(() => MapGenerator.Generate(spec, BigInt(c.input.Seed))) };
    }
    case 'walk':
      return walk(c.input, D);
    case 'encounter': {
      const { LayerEncounters } = D('src/core/dungeon/LayerEncounters.js', 'LayerEncounters');
      const { SeededRng } = D('src/core/dungeon/IRng.js', 'SeededRng');
      const { BattleField } = D('src/core/dungeon/BattleField.js', 'BattleField');
      const { NodeKindTokens } = D('src/core/dungeon/NodeKind.js', 'NodeKindTokens');
      const { Layer: l, Kind: k } = c.input;
      return {
        Picks: range(1, ENCOUNTER_SEEDS).map((seed) => Try(() => LayerEncounters.Pick(k, l, new SeededRng(BigInt(seed))))),
        Pool: LayerEncounters.Of(l).PoolFor(k),
        Cells: Try(() => BattleField.Cells(l, k)),
        CellsFor1to4: range(1, 4).map((z) => Try(() => BattleField.CellsFor(l, k, z))),
        Token: NodeKindTokens.ToToken(k),
        IsDetour: NodeKindTokens.IsDetour(k),
        IsCombat: NodeKindTokens.IsCombat(k),
      };
    }
    case 'table':
      return table(c.id.slice(6), D, expected || {});
    default:
      throw new Error(`harness: unknown dungeon case ${c.id}`);
  }
}

function table(name, D, expected) {
  switch (name) {
    case 'LayerEncounters': {
      const { LayerEncounters } = D('src/core/dungeon/LayerEncounters.js', 'LayerEncounters');
      return { All: LayerEncounters.All, OfOutOfRange: Try(() => LayerEncounters.Of(8)) };
    }
    case 'Miasma': {
      const { Miasma } = D('src/core/dungeon/Miasma.js', 'Miasma');
      const eff = [];
      for (let d = 0; d <= 8; d++) for (let r = -1; r <= 3; r++) eff.push([d, r, Miasma.EffectiveDensity(d, r)]);
      const acc = [];
      for (const p of [-5, 0, 7, 50, 95, 99, 100, 120]) for (const e of [-1, 0, 1, 3, 7]) acc.push([p, e, Miasma.Accumulate(p, e), Miasma.Relieve(p, e)]);
      const pen = [];
      for (let p = -5; p <= 105; p++) pen.push([p, Miasma.MaxStaminaPenalty(p), Miasma.IsLethal(p)]);
      const max = [];
      for (let t = -6; t <= 6; t++) for (let p = 0; p <= 100; p += 5) max.push([t, p, Miasma.MaxStamina(t, p)]);
      return {
        Static: readStatic(Miasma, Object.keys(expected.Static || {})),
        EffectiveDensity: eff,
        AccumulateRelieve: acc,
        PenaltyLethal: pen,
        MaxStamina: max,
        NegativeDensity: Try(() => Miasma.EffectiveDensity(-1, 0)),
      };
    }
    case 'SevenLayers': {
      const { SevenLayers } = D('src/core/dungeon/SevenLayers.js', 'SevenLayers');
      return {
        Static: readStatic(SevenLayers, Object.keys(expected.Static || {})),
        DisplayName: arr(SevenLayers.All).map((p) => [p.DisplayName(false), p.DisplayName(true)]),
        BareMinimumMiasma0to3: range(0, 4).map((r) => SevenLayers.BareMinimumMiasma(r)),
        FullSpendMiasma0to3: range(0, 4).map((r) => SevenLayers.FullSpendMiasma(r)),
        OfOutOfRange: Try(() => SevenLayers.Of(0)),
      };
    }
    case 'BattleField': {
      const { BattleField } = D('src/core/dungeon/BattleField.js', 'BattleField');
      return readStatic(BattleField, Object.keys(expected));
    }
    case 'ExplorationReducer': {
      const { ExplorationReducer } = D('src/core/dungeon/ExplorationReducer.js', 'ExplorationReducer');
      return {
        Static: readStatic(ExplorationReducer, Object.keys(expected.Static || {})),
        RestHeal0to100: range(0, 101).map((h) => ExplorationReducer.RestHeal(h)),
        InterludeHeal0to100: range(0, 101).map((h) => ExplorationReducer.InterludeHeal(h)),
      };
    }
    case 'ItemCatalogue': {
      const { ItemCatalogue } = D('src/core/dungeonContent/ItemCatalogue.js', 'ItemCatalogue');
      const { ItemEffect, ItemEffects } = D('src/core/dungeonContent/ItemEffect.js', 'ItemEffect', 'ItemEffects');
      const effects = {};
      for (const e of enumValues(ItemEffect)) effects[e] = { Token: ItemEffects.ToToken(e), Side: ItemEffects.Side(e) };
      const all = arr(ItemCatalogue.All);
      return {
        Tools: ItemCatalogue.Tools,
        Consumables: ItemCatalogue.Consumables,
        All: all.map((i) => i.Id),
        ToString: all.map((i) => (typeof i.ToString === 'function' ? i.ToString() : i.toString())),
        Effects: effects,
        UnknownTool: Try(() => ItemCatalogue.Tool('nope')),
        ConsumableAsTool: Try(() => ItemCatalogue.Tool('yakuso')),
      };
    }
    case 'Loadout': {
      const { Loadout } = D('src/core/dungeonContent/Loadout.js', 'Loadout');
      const { ItemEffect } = D('src/core/dungeonContent/ItemEffect.js', 'ItemEffect');
      const full = Loadout.Of(['bosho_no_men', 'kokugen_no_suna', 'toshin_no_kakera'], ['yakuso', 'joka_no_ko', 'wasuremizu']);
      const has = {};
      for (const e of enumValues(ItemEffect)) has[e] = full.Has(e);
      return {
        Static: readStatic(Loadout, Object.keys(expected.Static || {})),
        Empty: Loadout.Empty,
        Full: full,
        Has: has,
        TwoMasks: Loadout.Of(['bosho_no_men', 'bosho_no_men']),
        Survivor: Loadout.Of(['bosho_no_men', 'kokugen_no_suna', 'toshin_no_kakera', 'ikitsugi_no_fue'], null, Loadout.SurvivorToolSlots),
        TooManyTools: Try(() => Loadout.Of(['bosho_no_men', 'kokugen_no_suna', 'toshin_no_kakera', 'ikitsugi_no_fue'])),
        TooManyConsumables: Try(() => Loadout.Of([], ['yakuso', 'yakuso', 'yakuso', 'yakuso'])),
        TakeIntoFree: Try(() => Loadout.Empty.Take('yakuso')),
        TakeFullNoSlot: Try(() => full.Take('shirube_no_ishi')),
        TakeFullSlot1: Try(() => full.Take('shirube_no_ishi', 1)),
        Use1: Try(() => {
          const [next, used] = outPair(full.Use(1), 'used');
          return { Loadout: next, Used: used };
        }),
        UseBad: Try(() => Loadout.Empty.Use(0)),
        Leave: full.LeaveTheDungeon(),
      };
    }
    case 'RunLoadout': {
      const { RunLoadout, ReliefItem } = D('src/core/dungeon/RunLoadout.js', 'RunLoadout', 'ReliefItem');
      const { RunStart } = D('src/bridge/explorationScript/ExplorationSession.js', 'RunStart');
      const trial = RunStart.TrialLoadout();
      const den = (id, n) => ReliefItem.Density(id, n);
      return {
        Static: readStatic(RunLoadout, Object.keys(expected.Static || {})),
        Trial: trial,
        SpendGauge: Try(() => {
          const [next, relief] = outPair(trial.Spend(0), 'gaugeRelief');
          return { Loadout: next, Relief: relief };
        }),
        SpendBad: Try(() => trial.Spend(5)),
        TooManyTools: Try(() => new RunLoadout([den('a', 1), den('b', 1), den('c', 1), den('d', 1)])),
        GaugeTool: Try(() => new RunLoadout([ReliefItem.Gauge('a', 1)])),
        SurvivorSlots: new RunLoadout([den('a', 1), den('b', 2), den('c', 1), den('d', 1)], null, RunLoadout.SurvivorBonusToolSlots),
      };
    }
    case 'Seeds': {
      const { ExplorationSession } = D('src/bridge/explorationScript/ExplorationSession.js', 'ExplorationSession');
      const { Expedition } = D('src/bridge/exploration/Expedition.js', 'Expedition');
      const { SeededRng, RngExtensions } = D('src/core/dungeon/IRng.js', 'SeededRng', 'RngExtensions');
      const MAX = 18446744073709551615n;
      const runs = [1n, 2n, 3n, 20260921n, MAX];
      const layerSeeds = [];
      const encounterSeeds = [];
      const rewardSeeds = [];
      for (const run of runs) {
        for (let layer = 1; layer <= 7; layer++) {
          layerSeeds.push(ExplorationSession.LayerSeed(run, layer));
          for (let node = 0; node < 13; node += 4) {
            encounterSeeds.push(ExplorationSession.EncounterSeed(run, layer, node));
            rewardSeeds.push(Expedition.RewardSeed(run, layer, node));
          }
        }
      }
      const draws = [];
      for (const seed of [0n, 1n, 2n, 12345n, MAX]) {
        const rng = new SeededRng(seed);
        const row = [];
        for (let i = 0; i < 4; i++) row.push(rng.NextUInt64());
        for (let i = 0; i < 4; i++) row.push(rng.NextDouble());
        for (let i = 0; i < 4; i++) row.push(RngExtensions.NextInt(rng, 0, 7));
        for (let i = 0; i < 4; i++) row.push(RngExtensions.NextChance(rng, 0.5));
        const list = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
        RngExtensions.Shuffle(rng, list);
        row.push(list);
        draws.push(row);
      }
      return { Runs: runs, LayerSeed: layerSeeds, EncounterSeed: encounterSeeds, RewardSeed: rewardSeeds, DungeonRng: draws };
    }
    default:
      throw new Error(`harness: unknown dungeon table ${name}`);
  }
}

/** Applies the recorded ops (docs/PARITY_SHAPES.md "Dungeon walk ops") to one ExplorationState. */
function walk(input, D) {
  const { SevenLayers } = D('src/core/dungeon/SevenLayers.js', 'SevenLayers');
  const { ExplorationReducer } = D('src/core/dungeon/ExplorationReducer.js', 'ExplorationReducer');
  const { RunLoadout } = D('src/core/dungeon/RunLoadout.js', 'RunLoadout');
  const { RunStart } = D('src/bridge/explorationScript/ExplorationSession.js', 'RunStart');
  const R = ExplorationReducer;
  let state = null;
  const results = [];
  for (const op of input.Ops) {
    let outcome;
    try {
      switch (op.Op) {
        case 'Enter': {
          const profile = SevenLayers.Of(op.Layer);
          const loadout = op.Loadout === 'trial' ? RunStart.TrialLoadout() : RunLoadout.Empty;
          state = R.Enter(profile, profile.Map(BigInt(op.MapSeed)), loadout, op.Hp, op.MaxHp, op.Stamina, op.MiasmaPercent, op.TempMaxStaminaMod);
          break;
        }
        case 'Step': state = R.Step(state, op.Node, op.Choice); break;
        case 'ReportBattle': state = R.ReportBattle(state, op.Hp, op.Stamina); break;
        case 'UseConsumable': state = R.UseConsumable(state, op.Index); break;
        case 'SpendTrainingMark': state = R.SpendTrainingMark(state); break;
        case 'Interlude': state = R.Interlude(state); break;
        case 'EndLife': state = R.EndLife(state); break;
        case 'Descend': {
          const next = SevenLayers.Of(op.Layer);
          state = R.Descend(state, next, next.Map(BigInt(op.MapSeed)));
          break;
        }
        default: throw new Error(`harness: unknown op ${op.Op}`);
      }
      outcome = state;
    } catch (e) {
      if (e && e.name === 'Missing') throw e;
      outcome = e;
    }
    const r = { State: outcome };
    if (state != null) r.CanStep = arr(state.Map.Nodes).map((n) => R.CanStep(state, n.Id));
    results.push(r);
  }
  return results;
}
