// JS extension (not in the C#; docs/EXTENSIONS.md 「探索の持ち物とイベント」): the catalogue's items as the
// exploration core carries them. The C# RunLoadout holds only what relieves 瘴気 (防瘴の面 as Density, 浄化の香
// as Gauge); trial mode carries every tool and consumable of tools_and_consumables_v4.md §2 / §3, each as
// the ReliefKind that says what the core does with it. The amounts are the catalogue's.
import { ItemCatalogue } from '../../core/dungeonContent/ItemCatalogue.js';
import { ItemEffect } from '../../core/dungeonContent/ItemEffect.js';
import { ReliefItem, ReliefKind } from '../../core/dungeon/RunLoadout.js';

/** ItemEffect → the ReliefKind the exploration core reads. */
function KindOf(effect) {
  switch (effect) {
    case ItemEffect.MiasmaDensity: return ReliefKind.Density;
    case ItemEffect.MiasmaGauge: return ReliefKind.Gauge;
    case ItemEffect.TimeLimit: return ReliefKind.Time;
    case ItemEffect.ChooseSurveyTarget: return ReliefKind.Survey;
    case ItemEffect.InterludeHeal: return ReliefKind.Interlude;
    case ItemEffect.HealHp: return ReliefKind.Heal;
    case ItemEffect.MaxStamina: return ReliefKind.MaxStamina;
    case ItemEffect.RefillStamina: return ReliefKind.Refill;
    case ItemEffect.Teleport: return ReliefKind.Teleport;
    // 間合いの履 / 息継ぎの笛 / 竜鱗の欠片 / 竜語の符 act in battle (Expedition's battleToolIds); the
    // exploration carries them for the slot count and the list of tools only.
    default: return ReliefKind.Battle;
  }
}

export const TrialItems = {
  /** One catalogue item (ItemDef) as the core's ReliefItem. */
  ReliefOf(def) {
    return new ReliefItem(def.Id, KindOf(def.Effect), def.Amount);
  },

  /** A consumable by catalogue id, as the core's ReliefItem. */
  Consumable(id) {
    return TrialItems.ReliefOf(ItemCatalogue.Consumable(id));
  },

  /** A tool by catalogue id, as the core's ReliefItem. */
  Tool(id) {
    return TrialItems.ReliefOf(ItemCatalogue.Tool(id));
  },
};
