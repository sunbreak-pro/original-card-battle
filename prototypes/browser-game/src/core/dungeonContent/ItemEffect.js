// Port of unity-port/DungeonContent/ItemEffect.cs
import { defineEnum } from '../../shared/cs.js';
import { argOutOfRange } from '../dungeon/_errors.js';

/**
 * What a carried item does. Every value here is something the 80% build can already
 * honour: nothing depends on hidden rooms, shortcuts or traps, which are postponed
 * (dungeon_exploration_v4.md §2.2), and nothing touches the frozen armour economy.
 *
 * MiasmaDensity       Lowers every layer's 瘴気 density for the whole life. Floored at 1.
 * MiasmaGauge         Pulls the 瘴気 gauge back down once, by a flat percentage.
 * TimeLimit           Adds 刻限 to every layer.
 * ChooseSurveyTarget  Lets the player pick which enemy a 情報収集 node raises to disclosure 2.
 * InterludeHeal       Raises what 階層間の休憩 gives back, in points of the HP maximum.
 * HealHp              Restores HP, as a share of the maximum.
 * MaxStamina          Raises max stamina for the rest of the life. Shares the ±4 clamp.
 * RefillStamina       Refills current stamina.
 * Teleport            Moves to any unresolved node of the current layer. Still costs one 刻限.
 * WiderStart          Moves the player's starting cell back by Amount, widening the opening gap from
 *                     the default 3 (seven_layers_v4.md §2.4). The field keeps its width.
 * OpeningStamina      Adds stamina at the start of a battle.
 * OpeningGuard        Grants Guard once per battle, the first time it would matter.
 * BluntBossState      Holds a boss's own state one stack lower than it would otherwise sit.
 */
export const ItemEffect = defineEnum([
  'MiasmaDensity',
  'MiasmaGauge',
  'TimeLimit',
  'ChooseSurveyTarget',
  'InterludeHeal',
  'HealHp',
  'MaxStamina',
  'RefillStamina',
  'Teleport',
  'WiderStart',
  'OpeningStamina',
  'OpeningGuard',
  'BluntBossState',
]);

/**
 * Which core reads the effect. The slots are shared, the plumbing is not.
 *
 * Exploration  DungeonCore reads it: 瘴気, 刻限, nodes, the interlude.
 * Battle       BattleCore reads it: position, stamina, Guard, enemy states.
 */
export const EffectSide = defineEnum(['Exploration', 'Battle']);

/** C# extension methods on ItemEffect: `effect.Side()` → `ItemEffects.Side(effect)`. */
export const ItemEffects = {
  Side(effect) {
    switch (effect) {
      case ItemEffect.MiasmaDensity: return EffectSide.Exploration;
      case ItemEffect.MiasmaGauge: return EffectSide.Exploration;
      case ItemEffect.TimeLimit: return EffectSide.Exploration;
      case ItemEffect.ChooseSurveyTarget: return EffectSide.Exploration;
      case ItemEffect.InterludeHeal: return EffectSide.Exploration;
      case ItemEffect.HealHp: return EffectSide.Exploration;
      case ItemEffect.MaxStamina: return EffectSide.Exploration;
      case ItemEffect.RefillStamina: return EffectSide.Exploration;
      case ItemEffect.Teleport: return EffectSide.Exploration;
      case ItemEffect.WiderStart: return EffectSide.Battle;
      case ItemEffect.OpeningStamina: return EffectSide.Battle;
      case ItemEffect.OpeningGuard: return EffectSide.Battle;
      case ItemEffect.BluntBossState: return EffectSide.Battle;
      default: throw argOutOfRange('effect', effect, null);
    }
  },

  /** Stable lowercase token, for saves and for the loadout screen's bindings. */
  ToToken(effect) {
    switch (effect) {
      case ItemEffect.MiasmaDensity: return 'miasma_density';
      case ItemEffect.MiasmaGauge: return 'miasma_gauge';
      case ItemEffect.TimeLimit: return 'time_limit';
      case ItemEffect.ChooseSurveyTarget: return 'choose_survey_target';
      case ItemEffect.InterludeHeal: return 'interlude_heal';
      case ItemEffect.HealHp: return 'heal_hp';
      case ItemEffect.MaxStamina: return 'max_stamina';
      case ItemEffect.RefillStamina: return 'refill_stamina';
      case ItemEffect.Teleport: return 'teleport';
      case ItemEffect.WiderStart: return 'wider_start';
      case ItemEffect.OpeningStamina: return 'opening_stamina';
      case ItemEffect.OpeningGuard: return 'opening_guard';
      case ItemEffect.BluntBossState: return 'blunt_boss_state';
      default: throw argOutOfRange('effect', effect, null);
    }
  },
};
