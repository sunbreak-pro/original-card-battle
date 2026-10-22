// Port of unity-port/BattleCore/Field.cs

import {
  defineRecord,
  ArgumentNullException,
  ArgumentOutOfRangeException,
  ArgumentException,
} from '../../shared/cs.js';
import { Constants } from './Constants.js';
import { Actor } from './Types.js';

/** .NET's ArgumentNullException(paramName).Message. */
function argumentNull(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

/** .NET's ArgumentOutOfRangeException(paramName, actualValue, message).Message (Linux newline). */
function argumentOutOfRange(param, value, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${param}')\nActual value was ${value}.`);
}

/**
 * A C# `(int Cell, int Size)` tuple. The convention is `{ Cell, Size }`; an unnamed tuple
 * (`{ Item1, Item2 }`) or a two-element array is read the same way.
 */
function cellAndSize(entry) {
  if (Array.isArray(entry)) return { cell: entry[0], size: entry[1] };
  if (entry.Cell !== undefined || entry.Size !== undefined) return { cell: entry.Cell, size: entry.Size };
  return { cell: entry.Item1, size: entry.Item2 };
}

/** Where a shift left its owner and how many of the asked-for cells it could not take. */
export class Shift extends defineRecord('Shift', ['From', 'To', 'Blocked']) {}

function MovePlayer(state, cells) {
  const self = state.Player;
  const from = self.Cell;
  if (cells === 0) return new Shift(from, from, 0);

  let to;
  if (cells > 0) {
    // Toward = right, capped one cell short of the nearest standing enemy.
    const nearest = state.Nearest;
    const cap = nearest < 0 ? state.FieldCells - self.Size + 1 : state.Enemies[nearest].Body.Cell - self.Size;
    to = Math.min(from + cells, cap);
  } else {
    to = Math.max(from + cells, 1);
  }
  return new Shift(from, to, Math.abs(cells) - Math.abs(to - from));
}

function MoveEnemy(state, unit, cells) {
  const self = state.Enemies[unit].Body;
  const from = self.Cell;
  if (cells === 0) return new Shift(from, from, 0);

  // Toward = left, floor one cell past the player; away = right, cap at the line's end.
  const floor = state.Player.FarCell + 1;
  const cap = state.FieldCells - self.Size + 1;
  const step = cells > 0 ? -1 : 1;
  const wanted = from + step * Math.abs(cells);
  const limit = Math.max(floor, Math.min(cap, wanted));

  // Walk back from the furthest cell asked for until the whole body fits.
  let to = from;
  for (let cell = limit; cell !== from; cell -= step) {
    if (IsFree(state, unit, cell, self.Size)) {
      to = cell;
      break;
    }
  }
  return new Shift(from, to, Math.abs(cells) - Math.abs(to - from));
}

/** Whether cells [cell, cell + size − 1] hold no standing enemy other than `unit`. */
function IsFree(state, unit, cell, size) {
  const far = cell + size - 1;
  for (let i = 0; i < state.Enemies.length; i++) {
    if (i === unit || !state.Enemies[i].Alive) continue;
    const other = state.Enemies[i].Body;
    if (cell <= other.FarCell && other.Cell <= far) return false;
  }
  return true;
}

/**
 * §7.1 / §7.4: the same for one to three enemies (`ENEMIES_MAX`), which must also each keep
 * their own cells (`CELL_CAPACITY` = 1).
 */
function ValidateMany(fieldCells, playerCell, playerSize, enemies) {
  if (enemies == null) throw argumentNull('enemies');
  if (fieldCells < Constants.FieldCellsMin || fieldCells > Constants.FieldCellsMax) {
    throw argumentOutOfRange(
      'fieldCells', fieldCells, `The line is ${Constants.FieldCellsMin}..${Constants.FieldCellsMax} cells.`);
  }
  if (enemies.length < 1 || enemies.length > Constants.EnemiesMax) {
    throw argumentOutOfRange(
      'enemies', enemies.length, `A battle has 1..${Constants.EnemiesMax} enemies (§7.4).`);
  }
  if (playerSize < 1) throw argumentOutOfRange('playerSize', playerSize, 'At least one cell.');
  if (playerCell < 1) {
    throw argumentOutOfRange('playerCell', playerCell, 'Cells start at 1.');
  }

  for (let i = 0; i < enemies.length; i++) {
    const { cell, size } = cellAndSize(enemies[i]);
    if (size < 1 || size > Constants.EnemySizeMax) {
      throw argumentOutOfRange('enemies', size, `An enemy uses 1..${Constants.EnemySizeMax} cells.`);
    }
    if (cell + size - 1 > fieldCells) {
      throw argumentOutOfRange('enemies', cell, `Enemy ${i} does not fit on the line.`);
    }
    if (playerCell + playerSize - 1 >= cell) {
      throw new ArgumentException('The player must stand to the left of every enemy (§7.1).');
    }
    for (let j = 0; j < i; j++) {
      const { cell: otherCell, size: otherSize } = cellAndSize(enemies[j]);
      if (cell <= otherCell + otherSize - 1 && otherCell <= cell + size - 1) {
        throw new ArgumentException(`Enemies ${j} and ${i} share a cell; a cell holds one enemy (§7.1).`);
      }
    }
  }
}

/**
 * §7: the line of cells. The player stands on the left and the enemies on the right, so "toward"
 * is +1 for the player and −1 for an enemy. Nothing here holds state; the turn loop does.
 *
 * Several enemies (§7.1 / §7.4, #47): one enemy per cell (`CELL_CAPACITY` = 1) and a large enemy
 * keeps its cells to itself. Nobody overtakes: the player stops short of the nearest standing
 * enemy, and an enemy stops short of the player. Enemies pass each other — only the cells an
 * enemy lands on must be free, not the ones it crosses. A fallen enemy holds no cells.
 *
 * (Actor is read only inside Move, so the Types.js ↔ Field.js import cycle is safe.)
 */
export const Field = Object.freeze({
  /** §7.2: N = the opponent's near edge − own cell − 1. Never negative: nobody overtakes (§7.1). */
  GapBetween(player, enemy) {
    if (player == null) throw argumentNull('player');
    if (enemy == null) throw argumentNull('enemy');
    return Field.Gap(player.Cell, player.Size, enemy.Cell);
  },

  /**
   * §7.2 (#573): N for a player whose cells start at `playerCell` and run
   * `playerSize` long, against an enemy whose near edge stands on
   * `enemyCell`. The one place N is counted: the turn loop, the enemy's aim and
   * the screen's tags and floor all read it from here or its inverses below.
   */
  Gap(playerCell, playerSize, enemyCell) {
    return Field.GapFrom(Field.FrontCell(playerCell, playerSize), enemyCell);
  },

  /** §7.2: the player's cell facing the enemies (CombatantState.FarCell), which N is counted from. */
  FrontCell(playerCell, playerSize) {
    return playerCell + playerSize - 1;
  },

  /** §7.2: N from the player's front cell `frontCell` to an enemy's near edge on `enemyCell`. */
  GapFrom(frontCell, enemyCell) {
    return enemyCell - frontCell - 1;
  },

  /** §7.2 read back: the player's front cell that leaves N = `gap` to an enemy's near edge on `enemyCell`. */
  FrontCellAt(gap, enemyCell) {
    return enemyCell - 1 - gap;
  },

  /** §7.2 read back: the enemy's near edge that leaves N = `gap` to the player's front cell `frontCell`. */
  NearEdgeAt(gap, frontCell) {
    return frontCell + 1 + gap;
  },

  /**
   * §7.3: move one side by `cells` toward the other (positive) or away
   * (negative), as far as the line allows. `unit` names the enemy when
   * `who` is Actor.Enemy. The caller has already taken 鈍足 off
   * the count.
   *
   * The player stops one cell short of the nearest standing enemy and at cell 1. An enemy
   * stops one cell past the player and at the line's end, and lands on the furthest cell it
   * asked for whose span no other standing enemy uses; it crosses occupied cells freely.
   */
  Move(state, who, cells, unit = 0) {
    if (state == null) throw argumentNull('state');
    return who === Actor.Player ? MovePlayer(state, cells) : MoveEnemy(state, unit, cells);
  },

  /**
   * §7.3 開始のマス: the enemy's near edge starts `startGap` empty cells to the
   * right of the player. When the line is too short to hold that — a 5-cell line, or a large
   * enemy on a narrow one — the enemy stands at the right end instead and the gap shrinks;
   * the player keeps its cell and the room behind it (2026-09-23, #169). Whether the result
   * still fits at all is Validate's to say.
   */
  EnemyStartCell(fieldCells, playerCell, playerSize, enemySize, startGap) {
    if (startGap < 0) throw argumentOutOfRange('startGap', startGap, 'A gap is never negative.');
    const wanted = playerCell + playerSize + startGap;
    return Math.min(wanted, fieldCells - enemySize + 1);
  },

  /** §7.3: the damage for the cells a pushed side could not take, before Guard. */
  WallDamage(blockedCells) {
    return Math.max(0, blockedCells) * Constants.WallDamage;
  },

  /**
   * C# overloads:
   *   Validate(int fieldCells, int playerCell, int playerSize, int enemyCell, int enemySize)
   *     Refuses a setup the line cannot hold: the width outside 5〜8, a side off the line, the
   *     player not to the left of the enemy, or an enemy wider than `ENEMY_SIZE_MAX`.
   *   Validate(int fieldCells, int playerCell, int playerSize, IReadOnlyList<(int Cell, int Size)> enemies)
   *     §7.1 / §7.4: the same for one to three enemies (`ENEMIES_MAX`), which must also each keep
   *     their own cells (`CELL_CAPACITY` = 1). Each entry is `{ Cell, Size }`.
   */
  Validate(fieldCells, playerCell, playerSize, enemyCellOrEnemies, enemySize) {
    if (typeof enemyCellOrEnemies === 'number') {
      ValidateMany(fieldCells, playerCell, playerSize, [{ Cell: enemyCellOrEnemies, Size: enemySize }]);
      return;
    }
    ValidateMany(fieldCells, playerCell, playerSize, enemyCellOrEnemies);
  },
});
