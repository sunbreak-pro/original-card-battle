// Port of unity-port/BattleCore/Attributes.cs

import { ArgumentNullException, hasFlag } from '../../shared/cs.js';
import { BattleAttribute } from './Types.js';

/** .NET's ArgumentNullException(paramName).Message. */
function argumentNull(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

/**
 * §2.1 (v4.4, #258): a card or an enemy action counts as ONE attribute — 攻撃 / 防御 / スキル /
 * スタンス. It declares the faces it carries (CardDef.Attributes, Flags) and this
 * folds them: 攻撃 ＞ 防御 ＞ スキル, and スタンス on its own. The fold changes what the card is
 * counted as (played this turn, 連動, 連打, the tally, the omen); it never changes what the
 * card does, so an 攻撃 + 防御 card still puts its Guard on.
 *
 * Movement is not an attribute: HasMovement reads it off the face (前へ / 後ろへ
 * / 押す / 引く). A card that carries no attack, guard or skill face and only moves is counted
 * by what else it has: a Guard makes it 防御, anything else makes it スキル.
 *
 * (BattleAttribute is read only inside the functions, so the Types.js ↔ Attributes.js import
 * cycle is safe.)
 */
export const AttributeRule = Object.freeze({
  /** Whether the face moves anybody: the holder (前へ / 後ろへ) or the opponent (押す / 引く). */
  HasMovement(face) {
    if (face == null) throw argumentNull('face');
    return face.Move !== 0 || face.Push !== 0;
  },

  /** The one attribute the declared faces count as. BattleAttribute.None only for a card with nothing on it. */
  Fold(declared, face) {
    if (face == null) throw argumentNull('face');
    if (hasFlag(declared, BattleAttribute.Stance)) return BattleAttribute.Stance;
    if (hasFlag(declared, BattleAttribute.Attack)) return BattleAttribute.Attack;
    if (hasFlag(declared, BattleAttribute.Guard)) return BattleAttribute.Guard;
    if (hasFlag(declared, BattleAttribute.Skill)) return BattleAttribute.Skill;

    // No attack, guard or skill face is declared: a card of movement (and small extras) only.
    if (face.Guard > 0) return BattleAttribute.Guard;
    const skillLike =
      AttributeRule.HasMovement(face) ||
      face.Draw > 0 ||
      face.StaminaGain > 0 ||
      face.Heal > 0 ||
      face.StatusList.length > 0;
    return skillLike ? BattleAttribute.Skill : BattleAttribute.None;
  },

  /**
   * battle_core_v4 §3 「移動が中心の札 6 種」: a card that declares no attack, guard, skill or
   * stance face and moves (前へ / 後ろへ, with a small extra). 呪縛 keeps these out of play (#51).
   */
  IsMovementCard(declared, face) {
    if (face == null) throw argumentNull('face');
    return declared === BattleAttribute.None && AttributeRule.HasMovement(face);
  },

  /**
   * §4: a stance is an attribute of its own. A card that declares スタンス declares nothing
   * else and does not move; a card that carries a stance face declares スタンス.
   */
  StanceStandsAlone(declared, face) {
    if (face == null) throw argumentNull('face');
    const declaresStance = hasFlag(declared, BattleAttribute.Stance);
    if (!declaresStance) return face.Stance == null;
    return declared === BattleAttribute.Stance && !AttributeRule.HasMovement(face) && face.Power === 0 && face.Guard === 0;
  },
});
