// Port of unity-port/DungeonCore/NodeKind.cs
import { defineEnum } from '../../shared/cs.js';
import { argOutOfRange } from './_errors.js';

/**
 * The node kinds kept for the 80% build, from dungeon_exploration_v4.md §2.1.
 * 階層間の休憩 is deliberately absent: it sits between layers, costs no 刻限 and
 * is not a node (§4.1). Everything postponed in §2.2 is absent as well.
 *
 * Battle   通常戦闘. One battle. The bulk of a layer.
 * Elite    精鋭. Thicker reward, heavier cost. Always avoidable (§3.1).
 * Boss     階層ボス. The exit. Exactly one per layer, on the last row.
 * Rest     休息. Rest or train a card — one of the two, for one 刻限 (§3.2).
 * Survey   情報収集. Raises one enemy's journal disclosure to level 2.
 * Event    イベント. Spring / heavy chest / strange meal (§2.1).
 * Trace    死亡地点の痕跡. The previous life's estate. Present only when the run carries
 *          one; a layer holds at most one, and only one is collected per life (concept-v3 §8.3).
 * Carving  秘跡の彫り. The layer-two carving that names 竜神の秘跡 (world-v1 §5).
 *          Picking it up has no combat effect (world-v1 §5.2) — it only reveals the layer's name.
 */
export const NodeKind = defineEnum(['Battle', 'Elite', 'Boss', 'Rest', 'Survey', 'Event', 'Trace', 'Carving']);

/** C# extension methods on NodeKind: `kind.ToToken()` → `NodeKindTokens.ToToken(kind)`. */
export const NodeKindTokens = {
  /** Stable lowercase token. Used by map fingerprints, so it must never drift. */
  ToToken(kind) {
    switch (kind) {
      case NodeKind.Battle: return 'battle';
      case NodeKind.Elite: return 'elite';
      case NodeKind.Boss: return 'boss';
      case NodeKind.Rest: return 'rest';
      case NodeKind.Survey: return 'survey';
      case NodeKind.Event: return 'event';
      case NodeKind.Trace: return 'trace';
      case NodeKind.Carving: return 'carving';
      default: throw argOutOfRange('kind', kind, null);
    }
  },

  /**
   * Kinds a player may want to skip. A row is only ever allowed to be made
   * entirely of these when the row has a single node, which never happens for
   * them by construction (MapGenerator places them in rows of width >= 2).
   */
  IsDetour(kind) {
    return kind === NodeKind.Elite || kind === NodeKind.Trace || kind === NodeKind.Carving;
  },

  /**
   * Kinds that hold a fight: arriving starts a battle drawn from the layer's pool
   * (LayerEncounters.PoolFor). The one place this is decided; whether a 死亡地点の痕跡
   * joins them is #106's.
   */
  IsCombat(kind) {
    return kind === NodeKind.Battle || kind === NodeKind.Elite || kind === NodeKind.Boss;
  },
};
