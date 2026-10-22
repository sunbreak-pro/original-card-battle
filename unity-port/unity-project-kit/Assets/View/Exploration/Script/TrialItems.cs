// Trial extension (port of the browser trial's src/bridge/explorationScript/TrialItems.js; docs/EXTENSIONS.md
// 「探索の持ち物とイベント」): the catalogue's items as the exploration core carries them. The C# RunLoadout holds
// only what relieves 瘴気 (防瘴の面 as Density, 浄化の香 as Gauge); trial mode carries every tool and consumable of
// tools_and_consumables_v4.md §2 / §3, each as the ReliefKind that says what the core does with it. The amounts
// are the catalogue's.
using DungeonContent;
using DungeonCore;

namespace Exploration
{
    public static class TrialItems
    {
        /// <summary>ItemEffect → the ReliefKind the exploration core reads.</summary>
        // js: bridge/explorationScript/TrialItems.js:10
        private static ReliefKind KindOf(ItemEffect effect)
        {
            switch (effect)
            {
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

        /// <summary>One catalogue item (ItemDef) as the core's ReliefItem.</summary>
        // js: bridge/explorationScript/TrialItems.js:29
        public static ReliefItem ReliefOf(ItemDef def) => new ReliefItem(def.Id, KindOf(def.Effect), def.Amount);

        /// <summary>A consumable by catalogue id, as the core's ReliefItem.</summary>
        // js: bridge/explorationScript/TrialItems.js:34
        public static ReliefItem Consumable(string id) => ReliefOf(ItemCatalogue.Consumable(id));

        /// <summary>A tool by catalogue id, as the core's ReliefItem.</summary>
        // js: bridge/explorationScript/TrialItems.js:39
        public static ReliefItem Tool(string id) => ReliefOf(ItemCatalogue.Tool(id));
    }
}
