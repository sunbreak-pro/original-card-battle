using System;

namespace BattleCore
{
    /// <summary>
    /// §2.1 (v4.4, #258): a card or an enemy action counts as ONE attribute — 攻撃 / 防御 / スキル /
    /// スタンス. It declares the faces it carries (<see cref="CardDef.Attributes"/>, Flags) and this
    /// folds them: 攻撃 ＞ 防御 ＞ スキル, and スタンス on its own. The fold changes what the card is
    /// counted as (played this turn, 連動, 連打, the tally, the omen); it never changes what the
    /// card does, so an 攻撃 + 防御 card still puts its Guard on.
    ///
    /// Movement is not an attribute: <see cref="HasMovement"/> reads it off the face (前へ / 後ろへ
    /// / 押す / 引く). A card that carries no attack, guard or skill face and only moves is counted
    /// by what else it has: a Guard makes it 防御, anything else makes it スキル.
    /// </summary>
    public static class AttributeRule
    {
        /// <summary>Whether the face moves anybody: the holder (前へ / 後ろへ) or the opponent (押す / 引く).</summary>
        public static bool HasMovement(Face face)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            return face.Move != 0 || face.Push != 0;
        }

        /// <summary>The one attribute the declared faces count as. <see cref="BattleAttribute.None"/> only for a card with nothing on it.</summary>
        public static BattleAttribute Fold(BattleAttribute declared, Face face)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            if (declared.HasFlag(BattleAttribute.Stance)) return BattleAttribute.Stance;
            if (declared.HasFlag(BattleAttribute.Attack)) return BattleAttribute.Attack;
            if (declared.HasFlag(BattleAttribute.Guard)) return BattleAttribute.Guard;
            if (declared.HasFlag(BattleAttribute.Skill)) return BattleAttribute.Skill;

            // No attack, guard or skill face is declared: a card of movement (and small extras) only.
            if (face.Guard > 0) return BattleAttribute.Guard;
            bool skillLike = HasMovement(face) || face.Draw > 0 || face.StaminaGain > 0 || face.Heal > 0 || face.StatusList.Count > 0;
            return skillLike ? BattleAttribute.Skill : BattleAttribute.None;
        }

        /// <summary>
        /// battle_core_v4 §3 「移動が中心の札 6 種」: a card that declares no attack, guard, skill or
        /// stance face and moves (前へ / 後ろへ, with a small extra). 呪縛 keeps these out of play (#51).
        /// </summary>
        public static bool IsMovementCard(BattleAttribute declared, Face face)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            return declared == BattleAttribute.None && HasMovement(face);
        }

        /// <summary>
        /// §4: a stance is an attribute of its own. A card that declares スタンス declares nothing
        /// else and does not move; a card that carries a stance face declares スタンス.
        /// </summary>
        public static bool StanceStandsAlone(BattleAttribute declared, Face face)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            bool declaresStance = declared.HasFlag(BattleAttribute.Stance);
            if (!declaresStance) return face.Stance == null;
            return declared == BattleAttribute.Stance && !HasMovement(face) && face.Power == 0 && face.Guard == 0;
        }
    }
}
