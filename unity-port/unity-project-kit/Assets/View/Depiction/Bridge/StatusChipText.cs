// The status chips and their hover panel (battle-visual-v1 §7.3, #242): the family a word belongs to,
// and its 効き方 / 減り方 written from the status table's values (BattleCore Constants / Statuses), so
// the View prints the panel and computes nothing. Pure C#: BattleCore + Depiction.Script.
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    public static class StatusChipText
    {
        public const string OnFoeGroup = "相手に付ける系";
        public const string OnSelfGroup = "自分に付ける系";
        public const string LastingGroup = "永続";
        public const string BossGroup = "ボス専用";

        /// <summary>§7.3: the chip for one status word and its stacks, panel text included.</summary>
        public static StatusChip Chip(StatusKind kind, int stacks)
        {
            ChipKind family = KindOf(kind);
            return new StatusChip
            {
                Label = kind.ToLabel(),
                Stacks = stacks,
                Kind = family,
                Title = kind.ToLabel() + " " + stacks,
                Group = GroupWord(family),
                Effect = Effect(kind, stacks),
                Decay = Decay(kind),
            };
        }

        /// <summary>
        /// §7.3 永続の状態: one chip per stance held; the same stance held <paramref name="count"/> times
        /// is one chip that works that many times ("×2" from two on). Its 効き方 is the stance's own
        /// clause when the card catalog knows the name, else the general one.
        /// </summary>
        public static StatusChip StanceChip(string name, int count)
        {
            string clause = StanceClause(name);
            string times = count > 1 ? "（" + count + " 枚ぶん、" + count + " 倍効く）" : "";
            return new StatusChip
            {
                Label = "構え・" + name,
                // StatusBarView reads a chip with no stacks as the stance line; a repeat keeps its count.
                Stacks = count > 1 ? count : 0,
                Kind = ChipKind.Lasting,
                Title = name + (count > 1 ? " ×" + count : ""),
                Group = LastingGroup,
                Effect = (clause.Length > 0 ? clause : "置いたスタンスの効果が、戦闘のあいだ効き続ける") + times,
                Decay = "減らない（永続）",
            };
        }

        /// <summary>§7.3: the square chips (put on the opponent), the round ones (put on oneself), the bosses' words.</summary>
        public static ChipKind KindOf(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Bleed:
                case StatusKind.Fragile:
                case StatusKind.Slow:
                case StatusKind.Intimidate:
                case StatusKind.Fatigue:
                    return ChipKind.OnFoe;
                case StatusKind.Empower:
                case StatusKind.Focus:
                case StatusKind.Parry:
                case StatusKind.Swift:
                case StatusKind.Regen:
                    return ChipKind.OnSelf;
                default:
                    return ChipKind.Boss;
            }
        }

        public static string GroupWord(ChipKind kind)
        {
            switch (kind)
            {
                case ChipKind.OnFoe: return OnFoeGroup;
                case ChipKind.OnSelf: return OnSelfGroup;
                case ChipKind.Lasting: return LastingGroup;
                default: return BossGroup;
            }
        }

        /// <summary>
        /// §7.3 効き方, with the numbers of battle_core_v4 §5 (Constants). The word that moves HP each
        /// turn also says what it does at the stacks held now. The bosses' words are provisional until
        /// #88 writes them; they follow the core's own reading (BattleCore Types / Statuses).
        /// </summary>
        public static string Effect(StatusKind kind, int stacks)
        {
            switch (kind)
            {
                case StatusKind.Bleed:
                    return "ターン開始に HP −" + Constants.BleedPerStack + "×スタック（いまは −" + Constants.BleedPerStack * stacks + "）";
                case StatusKind.Fragile:
                    return "次に受ける攻撃の威力が " + Mult(Constants.FragileMult) + " 倍（Guard で引く前の値）";
                case StatusKind.Slow:
                    return "次のターン、自分が動くマスと、自分が押す / 引くマスが 1 減る";
                case StatusKind.Intimidate:
                    return "次の行動の威力と Guard が −" + Constants.IntimidatePenalty;
                case StatusKind.Fatigue:
                    return "次のターン開始のスタミナ回復が −" + Constants.FatiguePenalty;
                case StatusKind.Empower:
                    return "次の攻撃の威力が " + Mult(Constants.EmpowerMult) + " 倍";
                case StatusKind.Focus:
                    return "次に出す札の値が 1 段強くなる（コストは変わらない。いちばん強い段は超えない）";
                case StatusKind.Parry:
                    return "次に攻撃を受けたとき、Guard で防いだ分の半分を相手に返す";
                case StatusKind.Swift:
                    return "ターン開始に、1 マスだけ無料で前か後ろへ動ける";
                case StatusKind.Regen:
                    return "ターン開始に HP +" + Constants.RegenPerStack + "×スタック（いまは +" + Constants.RegenPerStack * stacks + "）";
                case StatusKind.MiasmaShroud:
                    return "最大スタミナが スタック 1 につき 1 下がる";
                case StatusKind.Binding:
                    return "自分で動く効果が起きない。移動だけの札は出せない";
                case StatusKind.Hook:
                    return "自分で後ろへ下がれない（前へは動ける）";
                case StatusKind.Depths:
                    return "間合い 0 でターンを始めると、スタミナ回復が スタック 1 につき 1 減る";
                case StatusKind.Rooting:
                    return "2 ターン続けて同じマスで終えると、次のターン開始に HP −" + Statuses.RootingHpPerStack + "×スタック";
                case StatusKind.Withering:
                    return "ターン開始のスタミナ回復が スタック 1 につき 1 減る";
                default:
                    return "";
            }
        }

        /// <summary>§7.3 減り方, from the word's decay type (Statuses.DecayOf). 見切り keeps its stack when no Guard took the blow.</summary>
        public static string Decay(StatusKind kind)
        {
            if (kind == StatusKind.Parry) return "効くと 1 減る（Guard 0 で受けたときは減らない）";
            switch (Statuses.DecayOf(kind))
            {
                case StatusDecay.OnTurn: return "ターンごとに 1 減る";
                case StatusDecay.OnUse: return "効くと 1 減る";
                default: return "減らない（戦闘のあいだ残る）";
            }
        }

        /// <summary>The chips of one unit: the stances first, one chip per distinct stance, then the words in their fixed order.</summary>
        public static List<StatusChip> Chips(StatusSet statuses, IReadOnlyList<string> stanceNames)
        {
            List<StatusChip> chips = StanceChips(stanceNames);
            foreach (StatusKind kind in statuses.Kinds) chips.Add(Chip(kind, statuses.Stacks(kind)));
            return chips;
        }

        /// <summary>One chip per distinct stance name in the order first held, with the times it is held.</summary>
        public static List<StatusChip> StanceChips(IReadOnlyList<string> stanceNames)
        {
            var chips = new List<StatusChip>();
            var order = new List<string>();
            var count = new Dictionary<string, int>();
            foreach (string name in stanceNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                if (!count.ContainsKey(name)) { order.Add(name); count[name] = 0; }
                count[name]++;
            }
            foreach (string name in order) chips.Add(StanceChip(name, count[name]));
            return chips;
        }

        /// <summary>1.5 as "1.5" whatever the machine's culture (a comma would read as a list).</summary>
        private static string Mult(double value)
        {
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string StanceClause(string name)
        {
            foreach (CardDef def in CardCatalog.All)
            {
                if (def.Name == name && def.Face.Stance != null) return CoreText.StanceText(def.Face.Stance);
            }
            return "";
        }
    }
}
