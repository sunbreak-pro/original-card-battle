// Port of unity-port/BattleCore/Combat.cs

import { ArgumentNullException, roundAway } from '../../shared/cs.js';
import { Constants } from './Constants.js';
import { Statuses } from './Statuses.js';

/** .NET's ArgumentNullException(paramName).Message. */
function argumentNull(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

/**
 * The arithmetic of one exchange: §5.1 の damage formula, Guard, 構え, and the two clamps the
 * turn loop needs. Nothing here holds state — the turn loop (#72) does.
 *
 * (Statuses is read only inside CellsAfterSlow, so an import cycle through Statuses.js is safe.)
 */
export const Combat = Object.freeze({
  /**
   * §5.1 / §17.6 F4: add first, then multiply by at most one multiplier.
   *
   *   (face + trait + followUp + conversion) × one multiplier → round AwayFromZero → − Guard
   *
   * Rounding is away from zero (2.5 → 3), not banker's, because the design tables are read
   * that way (decided 2026-09-12). This returns the raw power, before Guard; feed it to
   * ApplyGuard.
   *
   * §19.5 S13: one multiplier per blow, 脆化 before 強化. The turn loop picks it for each blow
   * (TurnLoop.Resolve and TurnLoop.Preview) and hands it in as `blowMult` —
   * 脆化's or 強化's, or 1.0 when neither applies. There is one slot, so two multipliers can
   * never be stacked on the same blow.
   *
   * C# named arguments (`ComputeRawPower(p, blowMult: 1.5)`) are positional here
   * (`ComputeRawPower(p, 0, 0, 0, 1.5)`, or `undefined` for a skipped default).
   */
  ComputeRawPower(face, traitBonus = 0, followUp = 0, conversion = 0, blowMult = 1.0) {
    const raw = (face + traitBonus + followUp + conversion) * blowMult;
    return Math.max(0, Math.trunc(roundAway(raw)));
  },

  /**
   * Guard soaks the hit before HP does: damage = max(0, raw − guard), and the Guard that
   * absorbed it is spent. Overkill past the Guard is not refunded to the Guard.
   *
   * Returns the C# tuple (int Damage, int GuardAfter, int Absorbed) as { Damage, GuardAfter, Absorbed }.
   */
  ApplyGuard(raw, guard) {
    const damage = Math.max(0, raw - guard);
    const guardAfter = Math.max(0, guard - raw);
    return { Damage: damage, GuardAfter: guardAfter, Absorbed: raw - damage };
  },

  /** 構え (§9 step 7): Guard +3 when 3 or more stamina is left at turn end, for both sides. */
  ReserveGuard(staminaLeft) {
    return staminaLeft >= Constants.ReserveThreshold ? Constants.ReserveGuard : 0;
  },

  /**
   * §5 鈍足 / §7.3: the cells a move, push or pull actually covers once the mover's own 鈍足
   * has taken one off (floor 0). The other side's 鈍足 never reduces what is done to it.
   */
  CellsAfterSlow(cells, moverStatuses) {
    if (moverStatuses == null) throw argumentNull('moverStatuses');
    return Math.max(0, cells - Statuses.MoveReduction(moverStatuses));
  },

  /** §3: a card can only be played if its cost is payable in full. */
  CanPay(cost, stamina) {
    return stamina >= cost;
  },

  /**
   * §9 steps 2 and 9: recover, then cap at the maximum. The bonus is what 温存 left behind on
   * the previous turn; 疲労 would subtract here, and is #48.
   *
   * §9.1 (#452): stamina already at or above the maximum (息継ぎの笛 opens past it) is kept as
   * it is: recovery adds nothing to it and does not cut it back either.
   */
  RecoverStamina(current, max, recovery, bonus = 0) {
    if (current >= max) return current;
    const raw = current + Math.max(0, recovery + bonus);
    return Math.min(max, Math.max(0, raw));
  },

  /** §8: draw 6 plus whatever modifiers apply, clamped to 3..8. */
  DrawCount(modifier = 0) {
    const raw = Constants.HandDraw + modifier;
    if (raw < Constants.HandDrawMin) return Constants.HandDrawMin;
    return raw > Constants.HandDrawMax ? Constants.HandDrawMax : raw;
  },

  /** §1: max stamina stays within 3..14 for the whole battle. */
  ClampMaxStamina(value) {
    if (value < Constants.MaxStaminaFloor) return Constants.MaxStaminaFloor;
    return value > Constants.MaxStaminaCeil ? Constants.MaxStaminaCeil : value;
  },

  /** §17.6 F9: a combatant falls the moment its HP reaches 0, wherever the damage came from. */
  IsDefeated(hp) {
    return hp <= 0;
  },
});
