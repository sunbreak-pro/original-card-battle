// Port of unity-port/BattleCore/ChainOrder.cs
//
// Import-cycle note: Default / Nine / Densities only build ChainBattle records from literals at
// module-evaluation time; Enemies, BattleSetup and Constants are read inside getters and Parse.

import {
  defineRecord,
  INT_MAX,
  ArgumentNullException,
  ArgumentException,
  ArgumentOutOfRangeException,
} from '../../shared/cs.js';
import { Constants } from './Constants.js';
import { Enemies } from './Enemies.js';
import { BattleSetup } from './TurnLoop.js';

// ------------------------------------------------------------------ local helpers (not C# types)

/** .NET's ArgumentNullException(paramName).Message. */
function argumentNull(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

/** .NET's ArgumentException(message, paramName).Message. */
function argument(message, param) {
  return new ArgumentException(`${message} (Parameter '${param}')`);
}

/** .NET's ArgumentOutOfRangeException(paramName, actualValue, message).Message (Linux newline). */
function argumentOutOfRange(param, value, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${param}')\nActual value was ${value}.`);
}

/**
 * .NET String.Trim(): strips the characters char.IsWhiteSpace accepts (U+0009–U+000D, U+0020,
 * U+0085, U+00A0, U+1680, U+2000–U+200A, U+2028, U+2029, U+202F, U+205F, U+3000). JS trim()
 * differs on two: it strips U+FEFF (.NET does not) and keeps U+0085 (.NET strips it).
 */
const CS_WHITESPACE = '[\\t-\\r \\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000]';
const CS_TRIM = new RegExp(`^${CS_WHITESPACE}+|${CS_WHITESPACE}+$`, 'g');
function csTrim(s) {
  return s.replace(CS_TRIM, '');
}

/**
 * int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out int value): ASCII digits
 * only (no sign, no whitespace, no separators), at least one, and the value fits an int.
 * Returns `{ ok, value }` (value 0 on failure, as the C# out parameter).
 */
function tryParseDigits(s) {
  if (!/^[0-9]+$/.test(s)) return { ok: false, value: 0 };
  const v = Number(s);
  if (v > INT_MAX) return { ok: false, value: 0 };
  return { ok: true, value: v };
}

// ------------------------------------------------------------------ ChainBattle

/**
 * One battle of a §12 chain: the enemies fought at once (one, or the two of a roster §9 pair, in
 * phase order) and the layer it stands for. The layer is what the 瘴気 reads.
 */
export class ChainBattle extends defineRecord('ChainBattle', ['EnemyIds', 'Layer']) {
  /** The enemies, in phase order (§7.4). A new list on every read, as the C# getter builds one. */
  get Defs() {
    const defs = [];
    for (const id of this.EnemyIds) defs.push(Enemies.ById(id));
    return defs;
  }

  /**
   * The line it is fought on: the slice's 6 cells, or as many as the enemies need to start at
   * START_GAP side by side — 7 for a size-2 enemy and for a pair (roster §0 「開始に要るマス数」).
   */
  get FieldCells() {
    let size = 0;
    for (const def of this.Defs) size += def.Size;
    return Math.max(BattleSetup.SliceFieldCells, Constants.PlayerStartCell + Constants.StartGap + size);
  }

  /** "polearm_warped+crossbow_hunter@3", the form ChainOrder.Parse reads back. */
  ToString() {
    return this.EnemyIds.join('+') + '@' + String(this.Layer);
  }

  toString() {
    return this.ToString();
  }
}

// ------------------------------------------------------------------ ChainOrder

/** C# `private static ChainBattle Single(string id, int layer)`. */
function single(id, layer) {
  return new ChainBattle(Object.freeze([id]), layer);
}

/**
 * §12: the orders a chain is fought in — the default three, roster §9's nine, or one spelt out
 * as `a,b,c`. The layer bench (BattleCore.Sim) reads its chains through Parse.
 * The chain the screen plays is DemoSession's (#518 removed the screenless ChainRun).
 */
export const ChainOrder = Object.freeze({
  /**
   * §12's default three: 錆槍の竜兵 → 瘴牙の走竜 → 鉄壁の門竜. The layers are the first each appears on in roster §9 that keeps
   * the chain going down: 1, 2, 4.
   */
  Default: Object.freeze([
    single('polearm_warped', 1),
    single('shadow_hound', 2),
    single('armored_warden', 4),
  ]),

  /** roster §9 「連戦の試験運用」: the nine, with the layer each is fought on. */
  Nine: Object.freeze([
    single('shadow_hound', 1),
    single('polearm_warped', 2),
    new ChainBattle(Object.freeze(['polearm_warped', 'crossbow_hunter']), 3),
    single('rusted_revenant', 4),
    single('mist_archer', 5),
    single('armored_warden', 5),
    single('twin_blade_warped', 6),
    single('pack_alpha', 6),
    single('abyss_angler', 6),
  ]),

  /**
   * The 瘴気 density of each layer, 1〜7: `seven_layers_v4.md` §2 (1 / 1 / 2 / 3 / 4 / 5 / 7).
   * The densities come from that table; the roster no longer carries them (v4.11, #320).
   */
  Densities: Object.freeze([1, 1, 2, 3, 4, 5, 7]),

  DensityOf(layer) {
    if (layer < 1 || layer > ChainOrder.Densities.length) throw argumentOutOfRange('layer', layer, 'Layers run 1〜7.');
    return ChainOrder.Densities[layer - 1];
  },

  /**
   * `a,b,c`: battles split by commas; a battle is enemy ids joined by '+' (phase
   * order, at most ENEMIES_MAX) and an optional '@layer'. A battle that names no layer is on
   * the one before it (the first on layer 1). "3" or "default" is Default, "9"
   * or "nine" is Nine. An unknown id is refused.
   */
  Parse(spec) {
    if (spec == null) throw argumentNull('spec');
    const trimmed = csTrim(spec);
    if (trimmed === '3' || trimmed === 'default') return ChainOrder.Default;
    if (trimmed === '9' || trimmed === 'nine') return ChainOrder.Nine;

    const battles = [];
    let layer = 1;
    for (const raw of trimmed.split(',')) {
      let part = csTrim(raw);
      if (part.length === 0) throw argument(`Chain "${spec}": an empty battle.`, 'spec');
      const at = part.indexOf('@');
      if (at >= 0) {
        const parsed = tryParseDigits(part.substring(at + 1));
        layer = parsed.value;
        if (!parsed.ok) {
          throw argument(`Chain "${spec}": "${part}" has no layer number after '@'.`, 'spec');
        }
        ChainOrder.DensityOf(layer);
        part = part.substring(0, at);
      }
      const ids = [];
      for (const id of part.split('+')) {
        const each = csTrim(id);
        Enemies.ById(each);
        ids.push(each);
      }
      if (ids.length > Constants.EnemiesMax) {
        throw argument(`Chain "${spec}": at most ${Constants.EnemiesMax} enemies a battle (§7.4).`, 'spec');
      }
      const battle = new ChainBattle(ids, layer);
      if (battle.FieldCells > Constants.FieldCellsMax) {
        throw argument(
          `Chain "${spec}": "${part}" needs ${battle.FieldCells} cells to start at START_GAP, more than the line's ${Constants.FieldCellsMax} (§7.1).`,
          'spec');
      }
      battles.push(battle);
    }
    return battles;
  },
});
