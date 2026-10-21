// Port of unity-port/DungeonCore/BattleField.cs
import { argException, argOutOfRange } from './_errors.js';
import { NodeKind } from './NodeKind.js';
import { SevenLayers } from './SevenLayers.js';

/**
 * How many cells the battle behind a combat node is fought on
 * (`.claude/docs/danjeon_document/seven_layers_v4.md` §2.4, Issue #168). The cell rules are the
 * battle core's (battle_core_v4 §7); this table only holds the per-layer values, and #99 hands
 * them across.
 *
 * 通常戦闘 is 6 on layers 1〜3, 7 on 4〜6 and 8 on 7. 精鋭 is one cell wider than the layer.
 * 階層ボス is 8 on every layer. A line is never narrower than every enemy of the fight needs:
 * 5 + the sum of their sizes, since the player stands on cell 2 and the first enemy on cell 6.
 */
export const BattleField = {
  /** battle_core_v4 §7.1: the narrowest line a battle may be handed. */
  MinCells: 5,

  /** battle_core_v4 §7.1: the widest line. No fight that needs more is put on a node. */
  MaxCells: 8,

  /**
   * §2.4: the cells before the first enemy's near edge — the player's cell 2 plus the opening gap
   * of 3. A tool that moves the player back to cell 1 widens the gap instead, so this stays.
   */
  CellsBeforeEnemies: 5,

  /** The layer's line for a node of this kind, before the enemies' sizes are counted. */
  Cells(layer, kind) {
    if (layer < 1 || layer > SevenLayers.Count)
      throw argOutOfRange('layer', layer, `the lair has ${SevenLayers.Count} layers`);

    const normal = layer <= 3 ? 6 : layer <= 6 ? 7 : 8;
    switch (kind) {
      case NodeKind.Battle: return normal;
      case NodeKind.Elite: return Math.min(BattleField.MaxCells, normal + 1);
      case NodeKind.Boss: return BattleField.MaxCells;
      default: throw argException(`a ${kind} node holds no fight`, 'kind');
    }
  },

  /**
   * §2.4: the layer's line, widened until every enemy of the fight can stand on it.
   * `enemySizes` is the sum of the cells the fight's enemies use.
   */
  CellsFor(layer, kind, enemySizes) {
    if (enemySizes < 1) throw argOutOfRange('enemySizes', enemySizes, 'a fight has at least one enemy');

    const needed = BattleField.CellsBeforeEnemies + enemySizes;
    if (needed > BattleField.MaxCells)
      throw argException(`enemies of ${enemySizes} cells need ${needed} cells; a line holds ${BattleField.MaxCells}`, 'enemySizes');
    return Math.max(BattleField.Cells(layer, kind), needed);
  },
};
