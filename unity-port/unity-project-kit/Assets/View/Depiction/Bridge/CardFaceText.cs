// The card face of battle-visual-v1 (#242): the value columns, the rows under them, the two rows of
// the trait box, the one line on hover, the right-click detail and the lamp for each enemy. Every
// word is written here from the card's data, so the View prints and judges nothing. Pure C#:
// BattleCore + Depiction.Script, no UnityEngine.
using System;
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    public static class CardFaceText
    {
        /// <summary>§6.1: at most two value columns on a face.</summary>
        public const int MaxValues = 2;

        /// <summary>§6.1: at most three rows under the values (four for a stance card, whose rows start where the values would).</summary>
        public const int MaxTextLines = 3;

        public const int MaxLastingLines = 4;

        /// <summary>§6.1 素直な札の特性の箱.</summary>
        public const string PlainTop = "素直な札";

        public const string PlainBottom = "特性なし（+2 込み）";

        /// <summary>Fills the battle-visual-v1 fields of <paramref name="face"/> from the card's data.</summary>
        public static void Fill(CardFace face, CardDef def)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            if (def == null) throw new ArgumentNullException(nameof(def));
            face.PrintedCost = def.Cost;
            face.Values = Values(def.Face, def.Attributes);
            face.Lasting = def.Face.Stance != null && def.Attribute.HasFlag(BattleAttribute.Stance);
            face.TextLines = face.Lasting ? LastingLines(def.Face) : TextLines(def.Face, def.Attributes);
            face.Plain = def.AllTraits.Count == 0;
            string[] rows = TraitRows(def);
            face.TraitTop = rows[0];
            face.TraitBottom = rows[1];
            face.HoverLine = HoverLine(def);
            face.Detail = Detail(def);
        }

        // ---- §6.1 値 ----------------------------------------------------------------------------

        /// <summary>
        /// The value columns in the order the card resolves them (§2.2): the blow, the move, the
        /// push or pull, the Guard, the heal, the draw. Only the first two are printed as columns;
        /// <see cref="TextLines"/> writes the rest.
        /// </summary>
        public static List<CardValue> Values(Face face, BattleAttribute attributes)
        {
            List<CardValue> all = AllValues(face, attributes);
            return all.Count > MaxValues ? all.GetRange(0, MaxValues) : all;
        }

        private static List<CardValue> AllValues(Face face, BattleAttribute attributes)
        {
            var values = new List<CardValue>();
            if (face.Stance != null && attributes.HasFlag(BattleAttribute.Stance)) return values;
            if (attributes.HasFlag(BattleAttribute.Attack)) values.Add(Value(CardValueKind.Power, CoreText.FacePower(face).ToString(), "威力"));
            if (face.Move > 0) values.Add(Value(CardValueKind.Close, face.Move.ToString(), "詰める"));
            else if (face.Move < 0) values.Add(Value(CardValueKind.Back, (-face.Move).ToString(), "離れる"));
            if (face.Push > 0) values.Add(Value(CardValueKind.Push, face.Push.ToString(), "押す"));
            else if (face.Push < 0) values.Add(Value(CardValueKind.Pull, (-face.Push).ToString(), "引く"));
            if (face.Guard > 0) values.Add(Value(CardValueKind.Guard, face.Guard.ToString(), "Guard"));
            if (face.Heal > 0) values.Add(Value(CardValueKind.Heal, "+" + face.Heal, "回復"));
            if (face.Draw > 0) values.Add(Value(CardValueKind.Draw, face.Draw.ToString(), "ドロー"));
            return values;
        }

        private static CardValue Value(CardValueKind kind, string number, string word)
        {
            return new CardValue { Kind = kind, Number = number, Word = word };
        }

        // ---- §6.1 説明 ----------------------------------------------------------------------------

        /// <summary>
        /// The rows under the values: the statuses first (「敵に 鈍足 2・疲労 2」, 「自分に 強化 2」),
        /// then what has no column (崩し, スタミナ, and any value past the second). At most three rows.
        /// </summary>
        public static List<string> TextLines(Face face, BattleAttribute attributes)
        {
            var lines = new List<string>();
            string foe = StatusRow(face, onSelf: false);
            string self = StatusRow(face, onSelf: true);
            if (foe.Length > 0) lines.Add("敵に " + foe);
            if (self.Length > 0) lines.Add("自分に " + self);
            if (face.Break > 0) lines.Add("崩し " + face.Break);
            if (face.StaminaGain > 0) lines.Add("スタミナ +" + face.StaminaGain);
            List<CardValue> all = AllValues(face, attributes);
            for (int i = MaxValues; i < all.Count; i++) lines.Add(all[i].Word + " " + all[i].Number);
            return lines.Count > MaxTextLines ? lines.GetRange(0, MaxTextLines) : lines;
        }

        private static string StatusRow(Face face, bool onSelf)
        {
            var parts = new List<string>();
            foreach (StatusGrant grant in face.StatusList)
            {
                if (grant.OnSelf == onSelf) parts.Add(grant.Kind.ToLabel() + " " + grant.Stacks);
            }
            return string.Join("・", parts);
        }

        /// <summary>§6.1 スタンスの札: one lasting effect per row; the View puts ∞ at the head of each.</summary>
        public static List<string> LastingLines(Face face)
        {
            var lines = new List<string>();
            string text = CoreText.StanceText(face.Stance);
            if (text.Length > 0) lines.Add(text);
            return lines.Count > MaxLastingLines ? lines.GetRange(0, MaxLastingLines) : lines;
        }

        // ---- §6.1 特性の箱 / §6.3 ---------------------------------------------------------------

        /// <summary>
        /// The two rows of the trait box. One trait: the condition in §6.1's short words, then
        /// "→ effect". Two traits (背水の陣, #213): one trait per row, condition and effect run together
        /// ("間合い2以上→威力+5"). No trait: the plain card's two rows.
        /// </summary>
        public static string[] TraitRows(CardDef def)
        {
            IReadOnlyList<Trait> traits = def.AllTraits;
            if (traits.Count == 0) return new[] { PlainTop, PlainBottom };
            if (traits.Count == 1) return new[] { Condition(traits[0], def), "→ " + Effect(traits[0], def) };
            return new[] { Compact(traits[0], def), Compact(traits[1], def) };
        }

        private static string Compact(Trait trait, CardDef def)
        {
            return (Condition(trait, def) + "→" + Effect(trait, def)).Replace(" ", "");
        }

        /// <summary>§6.1: the condition in the short words ("間合い 2 以上", "敵が出血中", "連動 スタンス").</summary>
        public static string Condition(Trait trait, CardDef def)
        {
            switch (trait.Condition)
            {
                case BattleCore.TraitCondition.GapAtMost:
                    return trait.Threshold == 0 ? "間合い 0" : "間合い " + trait.Threshold + " 以下";
                case BattleCore.TraitCondition.GapAtLeast: return "間合い " + trait.Threshold + " 以上";
                case BattleCore.TraitCondition.Unguarded: return "敵の Guard 0";
                case BattleCore.TraitCondition.Reserve: return "温存 残り " + trait.Threshold + " 以上";
                case BattleCore.TraitCondition.Combo: return "連動 " + AttributeWord(trait.Attribute);
                case BattleCore.TraitCondition.Moved: return "移動後";
                case BattleCore.TraitCondition.OmenIs:
                    return (IsStance(def) ? "置く時 " : "") + "予兆が" + OmenWord(trait.Omen);
                case BattleCore.TraitCondition.Desperate: return "死力";
                case BattleCore.TraitCondition.FirstPlay: return "初手";
                case BattleCore.TraitCondition.Finisher: return "締め";
                case BattleCore.TraitCondition.Broken: return "崩し後";
                case BattleCore.TraitCondition.Chain: return "連打";
                case BattleCore.TraitCondition.Thin: return "手薄";
                case BattleCore.TraitCondition.FoeHas: return "敵が" + WatchWord(trait) + "中";
                case BattleCore.TraitCondition.SelfHas: return "自分が" + WatchWord(trait) + "中";
                default: return "";
            }
        }

        /// <summary>§6.1: the effect after the arrow ("威力 +5", "敵に脆化 2", "重撃").</summary>
        public static string Effect(Trait trait, CardDef def)
        {
            switch (trait.Effect)
            {
                case TraitEffect.PowerBonus: return "威力 +" + trait.Amount;
                case TraitEffect.GuardBonus: return "Guard +" + trait.Amount + (IsStance(def) ? "（1 回）" : "");
                case TraitEffect.NextTurnRecovery: return "次ターン回復 +" + trait.Amount;
                case TraitEffect.HeavyBlow: return "重撃";
                case TraitEffect.StaminaGain: return "スタミナ +" + trait.Amount;
                case TraitEffect.Draw: return "ドロー +" + trait.Amount;
                case TraitEffect.Status:
                    return trait.Grant == null ? "" : (trait.Grant.OnSelf ? "自分に" : "敵に") + trait.Grant.Kind.ToLabel() + " " + trait.Grant.Stacks;
                case TraitEffect.CostDown: return "コスト −" + trait.Amount;
                case TraitEffect.Convert: return "転換";
                case TraitEffect.FollowUp: return "追撃";
                case TraitEffect.BreakBonus: return "崩し +" + trait.Amount;
                default: return "";
            }
        }

        // ---- §7.2 ホバーの 1 行 ---------------------------------------------------------------

        /// <summary>
        /// §7.2 / §9: the one line above a hovered card, the condition as a clause and the effect with
        /// the card's printed values ("間合い 2 以上なら、この札の威力 13 → 18"). Two traits are two
        /// sentences. A plain card says what its +2 went into.
        /// </summary>
        public static string HoverLine(CardDef def)
        {
            IReadOnlyList<Trait> traits = def.AllTraits;
            if (traits.Count == 0) return PlainLine(def);
            var parts = new List<string>();
            foreach (Trait trait in traits) parts.Add(ConditionClause(trait, def) + "、" + EffectClause(trait, def));
            return string.Join("。", parts);
        }

        private static string PlainLine(CardDef def)
        {
            string what = def.Attributes.HasFlag(BattleAttribute.Attack) ? "威力"
                : def.Face.Guard > 0 ? "Guard"
                : "値";
            string value = def.Attributes.HasFlag(BattleAttribute.Attack) ? CoreText.FacePower(def.Face).ToString()
                : def.Face.Guard > 0 ? def.Face.Guard.ToString()
                : "";
            return "特性なし。そのぶん" + what + "が " + Constants.PlainCardBonus + " 高い" + (value.Length > 0 ? "（" + value + " に込み）" : "");
        }

        /// <summary>§9: the condition as the clause before the comma ("隣り合っていれば", "いちばん近い敵の予兆が攻撃なら").</summary>
        public static string ConditionClause(Trait trait, CardDef def)
        {
            switch (trait.Condition)
            {
                case BattleCore.TraitCondition.GapAtMost:
                    return trait.Threshold == 0 ? "隣り合っていれば" : "間合い " + trait.Threshold + " 以下なら";
                case BattleCore.TraitCondition.GapAtLeast: return "間合い " + trait.Threshold + " 以上なら";
                case BattleCore.TraitCondition.Unguarded: return "敵の Guard が 0 なら";
                case BattleCore.TraitCondition.Reserve: return "出した後スタミナが " + trait.Threshold + " 以上残れば";
                case BattleCore.TraitCondition.Combo: return "このターンに" + AttributeWord(trait.Attribute) + "の札を出していれば";
                case BattleCore.TraitCondition.Moved: return "このターンにもう動いていれば";
                case BattleCore.TraitCondition.OmenIs:
                {
                    string kind = OmenWord(trait.Omen);
                    if (IsStance(def)) return "置くときに敵の予兆が" + kind + "なら";
                    if (!Directed(def)) return "いちばん近い敵の予兆が" + kind + "なら";
                    return "敵の予兆が" + kind + "なら";
                }
                case BattleCore.TraitCondition.Desperate: return "出す前のスタミナが " + Constants.DesperateThreshold + " 以下なら";
                case BattleCore.TraitCondition.FirstPlay: return "このターン最初に出す札なら";
                case BattleCore.TraitCondition.Finisher: return "このターン " + Constants.FinisherPlayNumber + " 枚目以降に出すなら";
                case BattleCore.TraitCondition.Broken: return "敵のスタミナが " + Constants.BrokenBelow + " 未満なら";
                case BattleCore.TraitCondition.Chain: return "このターン直前に出した札も" + AttributeWord(def.Attribute) + "なら";
                case BattleCore.TraitCondition.Thin: return "出した直後の手札が " + Constants.ThinHandMax + " 枚以下なら";
                case BattleCore.TraitCondition.FoeHas: return "敵が" + WatchWord(trait) + "中なら";
                case BattleCore.TraitCondition.SelfHas: return "自分が" + WatchWord(trait) + "中なら";
                default: return "";
            }
        }

        /// <summary>§9: what the trait does to the card's printed values ("この札の威力 13 → 18").</summary>
        public static string EffectClause(Trait trait, CardDef def)
        {
            int power = CoreText.FacePower(def.Face);
            switch (trait.Effect)
            {
                case TraitEffect.PowerBonus: return "この札の威力 " + power + " → " + (power + trait.Amount);
                case TraitEffect.GuardBonus:
                    return IsStance(def)
                        ? "その場で Guard +" + trait.Amount + "（1 回だけ）"
                        : "この札の Guard " + def.Face.Guard + " → " + (def.Face.Guard + trait.Amount);
                case TraitEffect.NextTurnRecovery:
                    return "次ターンの回復 " + Constants.StaminaRecovery + " → " + (Constants.StaminaRecovery + trait.Amount);
                case TraitEffect.HeavyBlow:
                    return "威力 " + power + " → " + (power + Constants.HeavyBlowPower) + "。ただし次ターンの回復 "
                        + Constants.StaminaRecovery + " → " + (Constants.StaminaRecovery - Constants.HeavyBlowRecoveryPenalty);
                case TraitEffect.StaminaGain: return "スタミナ +" + trait.Amount;
                case TraitEffect.Draw: return "さらに " + trait.Amount + " 枚引く";
                case TraitEffect.Status:
                    return trait.Grant == null ? "" : (trait.Grant.OnSelf ? "自分に" : "敵に") + trait.Grant.Kind.ToLabel() + " " + trait.Grant.Stacks + " も付ける";
                case TraitEffect.CostDown: return "この札のコスト " + def.Cost + " → " + Math.Max(0, def.Cost - trait.Amount);
                case TraitEffect.Convert: return "この札で得る Guard の半分を威力に足す（転換）";
                case TraitEffect.FollowUp: return "このターンに次に出す攻撃の威力 +" + Constants.FollowUpPower + "（追撃）";
                case TraitEffect.BreakBonus: return "崩し +" + trait.Amount;
                default: return "";
            }
        }

        // ---- §7.4 右クリックの詳細 / §7.5 用語 ---------------------------------------------------

        /// <summary>§7.4: name, the kind line, the full text and the terms the card uses.</summary>
        public static CardDetail Detail(CardDef def)
        {
            var detail = new CardDetail
            {
                Name = def.Name,
                KindLine = KindLine(def),
                Body = Body(def),
            };
            foreach (string word in TermsOf(def))
            {
                string meaning;
                if (Glossary.TryGetValue(word, out meaning)) detail.Terms.Add(new TermDefinition { Word = word, Meaning = meaning });
            }
            return detail;
        }

        /// <summary>§7.4 2 行目: "攻撃 ・ 敵 1 体 ・ 間合い 0〜1", "防御 ・ 自分". One attribute, never joined by ＋.</summary>
        public static string KindLine(CardDef def)
        {
            string kind = AttributeWord(def.Attribute);
            if (!Directed(def)) return kind + " ・ 自分";
            string whom = def.Targets == TargetKind.All ? "届く敵すべて" : "敵 1 体";
            return kind + " ・ " + whom + " ・ 間合い " + def.Face.ReachOrDefault.ToText();
        }

        private static string Body(CardDef def)
        {
            var sentences = new List<string>();
            if (def.Face.Stance != null && def.Attribute.HasFlag(BattleAttribute.Stance))
            {
                sentences.Add("スタンス。置いたあと、戦闘のあいだ効き続ける（永続）：" + CoreText.StanceText(def.Face.Stance) + "。");
            }
            string described = CoreText.Describe(def.Face, def.Attributes);
            if (def.Face.Stance != null)
            {
                // The stance clause is the sentence above; Describe's own 構え: clause would repeat it.
                int at = described.IndexOf("構え: ", StringComparison.Ordinal);
                if (at >= 0) described = described.Substring(0, at);
            }
            if (described.Length > 0) sentences.Add(described);
            IReadOnlyList<Trait> traits = def.AllTraits;
            if (traits.Count == 0)
            {
                sentences.Add("特性を持たないかわりに、値が " + Constants.PlainCardBonus + " 高い札（表の値に込み）。");
            }
            foreach (Trait trait in traits)
            {
                string term = TermOfCondition(trait.Condition);
                sentences.Add((term.Length > 0 ? term + "：" : "") + ConditionClause(trait, def) + "、" + EffectClause(trait, def) + "。");
            }
            return string.Join("", sentences);
        }

        /// <summary>The glossary words the card's detail lists, in the order they first matter.</summary>
        public static List<string> TermsOf(CardDef def)
        {
            var words = new List<string>();
            if (Directed(def)) Add(words, "間合い");
            if (def.Face.Move != 0) Add(words, "詰める / 離れる");
            if (def.Face.Push != 0) Add(words, "押す / 引く");
            if (def.Face.Break > 0) Add(words, "崩し n");
            if (def.Face.Stance != null && def.Attribute.HasFlag(BattleAttribute.Stance)) Add(words, "永続");
            IReadOnlyList<Trait> traits = def.AllTraits;
            if (traits.Count == 0) Add(words, "素直な札");
            foreach (Trait trait in traits)
            {
                string condition = TermOfCondition(trait.Condition);
                if (condition.Length > 0) Add(words, condition == "連動" ? "連動 X" : condition);
                switch (trait.Effect)
                {
                    case TraitEffect.HeavyBlow: Add(words, "重撃"); break;
                    case TraitEffect.Convert: Add(words, "転換"); break;
                    case TraitEffect.FollowUp: Add(words, "追撃"); break;
                    case TraitEffect.BreakBonus: Add(words, "崩し n"); break;
                }
            }
            return words;
        }

        private static void Add(List<string> words, string word)
        {
            if (!words.Contains(word)) words.Add(word);
        }

        private static string TermOfCondition(BattleCore.TraitCondition condition)
        {
            switch (condition)
            {
                case BattleCore.TraitCondition.Reserve: return "温存";
                case BattleCore.TraitCondition.Combo: return "連動";
                case BattleCore.TraitCondition.Moved: return "移動後";
                case BattleCore.TraitCondition.Desperate: return "死力";
                case BattleCore.TraitCondition.FirstPlay: return "初手";
                case BattleCore.TraitCondition.Finisher: return "締め";
                case BattleCore.TraitCondition.Broken: return "崩し後";
                case BattleCore.TraitCondition.Chain: return "連打";
                case BattleCore.TraitCondition.Thin: return "手薄";
                default: return "";
            }
        }

        /// <summary>
        /// battle-visual-v1 §7.5: the words with a dotted underline and what they mean. 死力, 締め and
        /// 崩し後 are not in §7.5's table yet; their meaning is battle_core_v4 §2.3's, with its numbers.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Glossary = new Dictionary<string, string>
        {
            { "間合い", "自分と相手のあいだの空きマスの数。0 は隣り合っている" },
            { "構え", "ターン終了のときスタミナが " + Constants.ReserveThreshold + " 以上残っていれば Guard +" + Constants.ReserveGuard },
            { "崩し n", "相手のスタミナを n 減らす" },
            { "素直な札", "特性を持たないかわりに、値が " + Constants.PlainCardBonus + " 高い札（表の値に込み）" },
            { "初手", "このターンに最初に出す札" },
            { "温存", "この札を出したあとも、スタミナが n 以上残る" },
            { "連打", "このターンに直前に出した札と、同じ属性である" },
            { "連動 X", "このターンに属性 X（攻撃・防御・スキル・スタンス）のカードをもう出している" },
            { "手薄", "この札を出したあと、手札が残り " + Constants.ThinHandMax + " 枚以下" },
            { "重撃", "威力 +" + Constants.HeavyBlowPower + "。そのかわり次のターン開始のスタミナ回復が " + Constants.HeavyBlowRecoveryPenalty + " 減る" },
            { "転換", "この札で得る Guard の半分（切り上げ）を威力に足す" },
            { "追撃", "このターンに次に出す攻撃の威力 +" + Constants.FollowUpPower },
            { "移動後", "このターンに自分がもう動いている" },
            { "永続", "消えずに残る。スタンスの効果。重ねると重ねた数のぶん効く" },
            { "詰める / 離れる", "自分が動いて、相手との間合いを縮める / 広げる" },
            { "押す / 引く", "相手を動かして、相手との間合いを広げる / 縮める" },
            { "死力", "札を出す前のスタミナが " + Constants.DesperateThreshold + " 以下" },
            { "締め", "このターンに " + Constants.FinisherPlayNumber + " 枚目以降に出す札" },
            { "崩し後", "相手のスタミナが " + Constants.BrokenBelow + " 未満" },
        };

        // ---- §6.3 ランプ ----------------------------------------------------------------------

        /// <summary>
        /// §6.3: the lamp of a card in the hand, and the lamp for each enemy slot. A card aimed at one
        /// enemy lights when any enemy it reaches satisfies the trait (decision 6), and each slot
        /// carries that enemy's own verdict so the View can re-light it for the framed one. A card
        /// aimed at every enemy in reach reads the nearest of them, and a card aimed at nobody the
        /// nearest standing enemy — the core's own choice (TurnLoop.Preview). A card with no trait,
        /// or not in the hand, is never lit. The verdicts are the core's (Preview's TraitHolds and
        /// InReach); nothing is judged here.
        /// </summary>
        public static bool Lamps(BattleState state, string instanceId, out List<bool> perEnemy)
        {
            perEnemy = new List<bool>();
            if (state == null) throw new ArgumentNullException(nameof(state));
            CardInstance card = Find(state, instanceId);
            if (card == null || card.Def.AllTraits.Count == 0) return false;
            CardDef def = card.Def;

            if (Directed(def) && def.Targets == TargetKind.One)
            {
                bool any = false;
                var living = new HashSet<int>(state.Living);
                for (int i = 0; i < state.Enemies.Count; i++)
                {
                    bool lit = false;
                    if (living.Contains(i))
                    {
                        PlayPreview preview = TurnLoop.Preview(state, instanceId, i);
                        lit = preview != null && preview.InReach && preview.TraitHolds;
                    }
                    perEnemy.Add(lit);
                    any |= lit;
                }
                return any;
            }

            PlayPreview read = TurnLoop.Preview(state, instanceId);
            bool holds = read != null && read.TraitHolds && (!Directed(def) || read.InReach);
            return holds;
        }

        private static CardInstance Find(BattleState state, string instanceId)
        {
            foreach (CardInstance card in state.Hand)
            {
                if (card.InstanceId == instanceId) return card;
            }
            return null;
        }

        // ---- words ------------------------------------------------------------------------------

        private static bool Directed(CardDef def)
        {
            return EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets);
        }

        private static bool IsStance(CardDef def)
        {
            return def.Attribute.HasFlag(BattleAttribute.Stance);
        }

        /// <summary>battle-visual-v1 §2.2: the four attributes as the screen names them.</summary>
        public static string AttributeWord(BattleAttribute attribute)
        {
            if (attribute.HasFlag(BattleAttribute.Attack)) return "攻撃";
            if (attribute.HasFlag(BattleAttribute.Guard)) return "防御";
            if (attribute.HasFlag(BattleAttribute.Skill)) return "スキル";
            if (attribute.HasFlag(BattleAttribute.Stance)) return "スタンス";
            return "";
        }

        private static string OmenWord(OmenKind? kind)
        {
            return kind.HasValue ? CoreText.OmenKindWord(kind.Value) : "";
        }

        private static string WatchWord(Trait trait)
        {
            return trait.Watch.HasValue ? trait.Watch.Value.ToLabel() : "";
        }
    }
}
