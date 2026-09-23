using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonContent
{
    /// <summary>One carried item. Name and reading are Japanese because they are UI text.</summary>
    public sealed class ItemDef
    {
        public ItemDef(string id, string name, string reading, ItemEffect effect, int amount, string note)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("an item needs an id", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("an item needs a name", nameof(name));

            Id = id;
            Name = name;
            Reading = reading;
            Effect = effect;
            Amount = amount;
            Note = note;
        }

        public string Id { get; }

        public string Name { get; }

        public string Reading { get; }

        public ItemEffect Effect { get; }

        /// <summary>Size of the effect. Percentage points, stamina, 刻限 — whatever the effect counts.</summary>
        public int Amount { get; }

        /// <summary>One line of why it exists, for the loadout screen's tooltip.</summary>
        public string Note { get; }

        public EffectSide Side => Effect.Side();

        public override string ToString() => $"{Id}({Effect.ToToken()}:{Amount})";
    }

    /// <summary>
    /// The first pass at what an explorer can carry (Issue #100). Eight tools and five
    /// consumables, chosen so that every effect is one the 80% build can already honour.
    ///
    /// The slots are shared between exploration and battle on purpose (concept-v3.md §14):
    /// with three tool slots and four tools of each kind, reaching deeper and fighting
    /// harder pull against each other.
    ///
    /// Not in here: the 秘跡の彫り clue of layer two. It is a node, not a carried item, and
    /// picking it up has no combat effect at all (world-v1.md §5.2).
    /// </summary>
    public static class ItemCatalogue
    {
        /// <summary>
        /// ツール. Kept for the whole life. Four lean on exploration, four on battle.
        /// </summary>
        public static IReadOnlyList<ItemDef> Tools { get; } = new[]
        {
            new ItemDef("bosho_no_men", "防瘴の面", "ぼうしょうのめん",
                ItemEffect.MiasmaDensity, 1,
                "この生の間、層の濃度を 1 下げる。下限は 1（seven_layers_v4.md §3.2）"),

            new ItemDef("kokugen_no_suna", "刻限の砂", "こくげんのすな",
                ItemEffect.TimeLimit, 1,
                "どの層でも刻限が 1 増える。浅い層ほど得が大きい"),

            new ItemDef("utsushi_no_tobari", "写しの帳", "うつしのとばり",
                ItemEffect.ChooseSurveyTarget, 1,
                "情報収集で開示度を上げる相手を自分で選べる。既定は層が決める"),

            new ItemDef("toshin_no_kakera", "灯芯の欠片", "とうしんのかけら",
                ItemEffect.InterludeHeal, 15,
                "階層間の休憩の HP 回復が 30% から 45% になる"),

            new ItemDef("maai_no_kutsu", "間合いの履", "まあいのくつ",
                ItemEffect.WiderStart, 1,
                "開始のマスが 2 から 1 になり、開始の間合いが 3 から 4 に広がる。後ろに下がる余地は無くなる"),

            new ItemDef("ikitsugi_no_fue", "息継ぎの笛", "いきつぎのふえ",
                ItemEffect.OpeningStamina, 3,
                "戦闘開始時のスタミナが 3 増える"),

            new ItemDef("ryurin_no_kakera", "竜鱗の欠片", "りゅうりんのかけら",
                ItemEffect.OpeningGuard, 5,
                "1 戦につき最初の 1 回だけ Guard 5。防御は Guard だけ（アーマーは凍結）"),

            new ItemDef("ryugo_no_fu", "竜語の符", "りゅうごのふ",
                ItemEffect.BluntBossState, 1,
                "ボス専用の状態が常に 1 スタック低く乗る（battle_core_v4.md §5）"),
        };

        /// <summary>
        /// 消耗品. Picked up during a run, swapped in place, never carried back to camp
        /// (concept-v3.md §14). Fewer kinds than tools because they arrive by luck.
        /// </summary>
        public static IReadOnlyList<ItemDef> Consumables { get; } = new[]
        {
            new ItemDef("joka_no_ko", "浄化の香", "じょうかのこう",
                ItemEffect.MiasmaGauge, 10,
                "瘴気の蓄積が 10% 戻る（concept-v3.md §6）"),

            new ItemDef("yakuso", "薬草", "やくそう",
                ItemEffect.HealHp, 20,
                "HP が最大の 20% 戻る"),

            new ItemDef("koi_hoshiniku", "濃い干し肉", "こいほしにく",
                ItemEffect.MaxStamina, 1,
                "最大スタミナが 1 増える。ほかの一時的な増減と合わせて ±4 まで"),

            new ItemDef("wasuremizu", "忘れ水", "わすれみず",
                ItemEffect.RefillStamina, 1,
                "いまのスタミナが全回復する。戦闘の直前に効く"),

            new ItemDef("shirube_no_ishi", "標の石", "しるべのいし",
                ItemEffect.Teleport, 1,
                "同じ層の未踏のノードへ 1 度だけ跳ぶ。刻限は普通に 1 払う"),
        };

        public static IEnumerable<ItemDef> All => Tools.Concat(Consumables);

        public static ItemDef Tool(string id) => Find(Tools, id, "tool");

        public static ItemDef Consumable(string id) => Find(Consumables, id, "consumable");

        private static ItemDef Find(IReadOnlyList<ItemDef> from, string id, string what)
        {
            var found = from.FirstOrDefault(i => i.Id == id);
            if (found == null) throw new ArgumentOutOfRangeException(nameof(id), id, $"no such {what}");
            return found;
        }
    }
}
