// Port of unity-port/unity-project-kit/Assets/View/Depiction/Bridge/FloorCells.cs
// The floor the screen draws (battle-visual-v1 §4.2, #242): which cells a card reaches from where the
// player stands, which cells the shown omen aims at, and whether the player stands in them; and
// (#455) which aimed cells meet the boundary the omen reads and where a moving omen takes the enemy.
// Every cell is read from the core (EnemyAi.TargetCells, TurnLoop.OmenBoundaryCells / OmenMoveCell,
// the card's printed reach, Field's gap), so the View only draws. Pure: BattleCore + Depiction.Script.
import { ArgumentNullException } from '../../shared/cs.js';
import { EnemyAi } from '../../core/battle/EnemyAi.js';
import { Field } from '../../core/battle/Field.js';
import { TurnLoop } from '../../core/battle/TurnLoop.js';
import { OmenHit } from '../depictionScript/DepictionTypes.js';

/** new ArgumentNullException(nameof(x)) with .NET's message. */
function argNull(name) {
  const e = new ArgumentNullException(`Value cannot be null. (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

export const FloorCells = {
  /**
   * §4.2 届くマスの光: the cells a card aimed at an opponent reaches, from the player's front edge
   * out: gap g of the card's reach is the cell g + 1 past it (battle_core_v4 §7.2's N). Cells past
   * the end of the line are left out. Empty for a card that aims at nobody.
   * @returns {number[]}
   */
  ReachOf(def, playerCell, playerSize, fieldCells) {
    if (def == null) throw argNull('def');
    const cells = [];
    if (!EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets)) return cells;
    const reach = def.Face.ReachOrDefault;
    const front = Field.FrontCell(playerCell, Math.max(1, playerSize));
    for (let gap = reach.Min; gap <= reach.Max; gap++) {
      const cell = Field.NearEdgeAt(gap, front);
      if (cell >= 1 && cell <= fieldCells) cells.push(cell);
    }
    return cells;
  },

  /**
   * §4.2 狙うマス: the cells the omen aims at with the enemy standing on `enemyCell`
   * (EnemyAi.TargetCells, lowest first). Empty for an omen that aims at nobody.
   * @returns {number[]}
   */
  AimOf(omen, enemy, enemyCell) {
    const cells = [];
    if (omen == null || enemy == null) return cells;
    cells.push(...EnemyAi.TargetCells(omen.Label, enemy.with({ Cell: enemyCell })));
    return cells;
  },

  /**
   * §4.2 境目を満たすマス (#455): the aimed cells whose gap meets the boundary the omen's action
   * reads (TurnLoop.OmenBoundaryCells), lowest first. Empty when it reads none.
   * @returns {number[]}
   */
  DenseOf(state, unit) {
    if (state == null) throw argNull('state');
    return [...TurnLoop.OmenBoundaryCells(state, unit)];
  },

  /**
   * §4.2 敵が動く予兆の移る先 (#455): every cell enemy `unit` would take after the move its omen
   * makes (TurnLoop.OmenMoveCell, then its size), lowest first. Empty when it does not move.
   * @returns {number[]}
   */
  MoveOf(state, unit) {
    if (state == null) throw argNull('state');
    const cells = [];
    const to = TurnLoop.OmenMoveCell(state, unit);
    if (to < 1) return cells;
    const size = Math.max(1, state.Enemies[unit].Body.Size);
    for (let i = 0; i < size; i++) cells.push(to + i);
    return cells;
  },

  /**
   * §4.4 当たり外れの印, read off the cells drawn now: Lands when the player's near edge (the cell
   * facing the enemy, CombatantState.FarCell — the one the core counts N from) is aimed at,
   * Misses when it is not, and None for an omen that aims at nobody (自分にだけ効く行動には出しません).
   * The writer prefers the core's OmenPreview.Lands and falls back to this only without one.
   */
  HitOf(omen, aim, playerCell, playerSize) {
    if (omen == null || omen.Label.Reach == null) return OmenHit.None;
    const near = Field.FrontCell(playerCell, Math.max(1, playerSize));
    return FloorCells.Contains(aim, near) ? OmenHit.Lands : OmenHit.Misses;
  },

  /** private static bool Contains(IReadOnlyList<int> cells, int cell) */
  Contains(cells, cell) {
    for (const c of cells) {
      if (c === cell) return true;
    }
    return false;
  },
};
