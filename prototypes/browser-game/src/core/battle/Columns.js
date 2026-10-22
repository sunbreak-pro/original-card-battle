// Port of unity-port/BattleCore/Columns.cs

import { ArgumentNullException, ArgumentOutOfRangeException } from '../../shared/cs.js';
import { Constants } from './Constants.js';

/** .NET's ArgumentNullException(paramName).Message. */
function argumentNull(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

/** .NET's ArgumentOutOfRangeException(paramName, actualValue, message).Message (Linux newline). */
function argumentOutOfRange(param, value, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${param}')\nActual value was ${value}.`);
}

const Min = 1;
const Max = Constants.ColumnCount;

/**
 * §3 and §3.1: the column a card sits in, and the scale each column is authored from.
 *
 * The old tier table used to be a range the player picked at play time. It is now a design-time
 * ruler: a card is written into one column, the column number is its cost, and that column's
 * value is its effect. Column 4 is the exception — it still costs 3, so that 集中 and 習熟 have
 * a rung above a cost-3 card to climb to.
 *
 * The heal scales came in with the eighty cards (#188), the 出血 / 再生 stack scale with #48:
 * 集中 reads them to find the column to the right. The stance step-up stays on the cards
 * themselves. The two attack scales are not §3.1's attack rows any more (#364, #451): they only
 * hold the steps 集中 and 伸び add to an attack face (§5), so no card is authored from them.
 */
export const Columns = Object.freeze({
  Min,
  Max,

  /** §2.1: a single-attribute move card is always cost 1, whatever it carries. */
  MoveCardColumn: 1,

  /** §2.1: each face of a two-face card is about 65% of the single-face column (the card still counts as one attribute). */
  DualFaceRatio: 0.65,

  /**
   * §5 集中 (#364, #451): the base of what one column to the right adds to a lone attack face,
   * +7 / +8 / +9 through StepRight. These are the v4.4 attack row, kept for 集中
   * and 伸び only. §3.1's attack row is now 7 / 15 / 24 / 33 and is not read here: a card's own
   * power comes from the reach band table in swordsman_cards_v4.md §1.4.
   */
  SingleAttackPower: Object.freeze([6, 13, 21, 30]),

  /** §3.1: a lone guard face. */
  SingleGuard: Object.freeze([4, 9, 15, 22]),

  /**
   * §5 集中 (#364, #451): the base of what one column to the right adds to the attack face of a
   * card with two faces, +4 / +6 / +6 through StepRight. These are the v4.4 row,
   * kept for 集中 and 伸び only; §3.1's row is now 5 / 10 / 16 / 22 and is not read here.
   */
  DualAttackPower: Object.freeze([4, 8, 14, 20]),

  /** §3.1: the guard face of a card with two faces. */
  DualGuard: Object.freeze([3, 6, 10, 14]),
  SingleHeal: Object.freeze([3, 8, 13, 19]),
  DualHeal: Object.freeze([2, 5, 8, 12]),

  /** §3.1 出血 / 再生のスタック: the stacks a face of each column gives. */
  TickStacks: Object.freeze([1, 2, 3, 4]),

  /**
   * §5 集中 (#188, #48): what one column to the right adds on a scale — the step from
   * `column` to the next. 0 at column 4, which 集中 never passes.
   */
  StepRight(scale, column) {
    if (scale == null) throw argumentNull('scale');
    Columns.Validate(column);
    return column >= Max ? 0 : scale[column] - scale[column - 1];
  },

  /** §3.1: how many status kinds a single-attribute skill face applies. Column 4 also draws 1. */
  SingleSkillKinds: Object.freeze([1, 2, 3, 3]),

  /** §3: the column number is the cost, except that column 4 still costs 3. */
  CostOf(column) {
    Columns.Validate(column);
    return Math.min(column, Constants.CostMax);
  },

  /** Reads one column off a scale. Columns are 1-based; the lists are not. */
  Value(scale, column) {
    if (scale == null) throw argumentNull('scale');
    Columns.Validate(column);
    return scale[column - 1];
  },

  IsValid(column) {
    return column >= Min && column <= Max;
  },

  Validate(column) {
    if (!Columns.IsValid(column)) {
      throw argumentOutOfRange('column', column, `Column must be ${Min}..${Max}.`);
    }
  },
});
