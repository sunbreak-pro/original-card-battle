// Every string and glyph the screen prints about a core card, action or omen is written here, so
// the View keeps printing what it is given. Pure C#: BattleCore + Depiction.Script, no UnityEngine.
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    public static class CoreText
    {
        /// <summary>
        /// v4.3 (#162) on the v4.2 screen: the script still knows two standing slots (near / far),
        /// so the gap N is shown on the player's tag as a number and the slot is picked by N — 0〜1
        /// stands in the near slot, 2 and more in the far one. The cells on the floor, the lit reach
        /// and the aimed cells are #163's, and this mapping goes with them.
        /// </summary>
        public static RangeSide SideOf(int gap)
        {
            return gap <= 1 ? RangeSide.Near : RangeSide.Far;
        }

        /// <summary>The one glyph on the player's tag: N itself ("0", "2").</summary>
        public static string GapGlyph(int gap)
        {
            return gap.ToString();
        }

        public static UnitSide Side(Actor actor)
        {
            return actor == Actor.Player ? UnitSide.Player : UnitSide.Enemy;
        }

        /// <summary>The colour role a card borrows: its first attribute in resolution order (§2.2).</summary>
        public static CardKind KindOf(BattleAttribute attributes)
        {
            if (attributes.HasFlag(BattleAttribute.Attack)) return CardKind.Attack;
            if (attributes.HasFlag(BattleAttribute.Guard)) return CardKind.Guard;
            if (attributes.HasFlag(BattleAttribute.Move)) return CardKind.Move;
            if (attributes.HasFlag(BattleAttribute.Skill)) return CardKind.Skill;
            return CardKind.Stance;
        }

        public static string KindWord(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Attack: return "攻撃";
                case CardKind.Guard: return "防御";
                case CardKind.Move: return "ムーブ";
                case CardKind.Skill: return "技";
                default: return "構え";
            }
        }

        /// <summary>How hard a beat looks, 1..4. The same steps LiveTurn uses; the thresholds are #30's to settle.</summary>
        /// <summary>§5.3: the system a card's blow draws, from its name and faces (StrikeSystems.Of).</summary>
        public static StrikeSystem SystemOf(CardDef def)
        {
            return StrikeSystems.Of(def.Name, def.Attributes.HasFlag(BattleAttribute.Attack),
                def.Attributes.HasFlag(BattleAttribute.Guard) || def.Face.Guard > 0, def.Face.Move != 0, def.Face.Push != 0);
        }

        /// <summary>§5.3: the same for an enemy action: the shape of its move and of its blow.</summary>
        public static StrikeSystem SystemOf(EnemyActionDef action)
        {
            return StrikeSystems.Of(action.Name, action.Attributes.HasFlag(BattleAttribute.Attack),
                action.Attributes.HasFlag(BattleAttribute.Guard) || action.Face.Guard > 0, action.Face.Move != 0, action.Face.Push != 0);
        }

        public static int Intensity(int amount)
        {
            if (amount <= 9) return 1;
            if (amount <= 16) return 2;
            if (amount <= 24) return 3;
            return 4;
        }

        // ---- cards --------------------------------------------------------------------------

        /// <summary>
        /// The face of one dealt card. <paramref name="preview"/> comes from the core
        /// (TurnLoop.Preview), so the lamp is the core's verdict and not a guess made here.
        /// </summary>
        public static CardFace Face(CardInstance card, PlayPreview preview)
        {
            CardDef def = card.Def;
            CardKind kind = KindOf(def.Attributes);
            bool single = def.Targets == TargetKind.One;
            return new CardFace
            {
                Id = card.InstanceId,
                Name = def.Name,
                // The cost a コスト −1 trait leaves right now is the core's (TurnLoop.Preview).
                Cost = preview != null ? preview.Cost : def.Cost,
                Kind = kind,
                Aim = single ? CardAim.Single : CardAim.Self,
                Affects = single ? (UnitSide?)null : UnitSide.Player,
                TypeLabel = KindWord(kind) + (single ? "・敵単体" : "・自分"),
                Description = Describe(def.Face, def.Attributes),
                ValueText = ValueOf(def.Face, def.Attributes),
                TraitText = TraitLines(def),
                TraitLit = def.Trait != null && preview != null && preview.TraitHolds,
                // §2.4: the reach is printed on the card; the two-valued RequiredRange cannot hold it,
                // so the core's CanPlay (OutOfReach) is what refuses the card, not the script.
                RequiredRange = null,
                RequiredRangeGlyph = ReachText(def.Attributes, def.Face, def.Targets),
            };
        }

        /// <summary>The reach as printed ("0〜1"), or empty for a card that aims at nobody.</summary>
        public static string ReachText(BattleAttribute attributes, Face face, TargetKind targets)
        {
            return EnemyAi.IsOpponentDirected(attributes, face, targets) ? face.ReachOrDefault.ToText() : "";
        }

        /// <summary>
        /// One short sentence per face, in resolution order. §5: a status on the opponent reads
        /// 「<語> を n 付与する」, one on the one playing 「自分に <語> を n 付与する」.
        /// </summary>
        public static string Describe(Face face, BattleAttribute attributes)
        {
            var sentences = new List<string>();
            if (attributes.HasFlag(BattleAttribute.Attack)) sentences.Add("敵に " + face.Power + " ダメージ。");
            if (face.Break > 0) sentences.Add("崩し " + face.Break + "。");
            if (face.Move > 0) sentences.Add("前へ " + face.Move + " 動く。");
            else if (face.Move < 0) sentences.Add("後ろへ " + (-face.Move) + " 動く。");
            if (face.Push > 0) sentences.Add("敵を " + face.Push + " マス押す。");
            else if (face.Push < 0) sentences.Add("敵を " + (-face.Push) + " マス引く。");
            if (face.Guard > 0) sentences.Add("Guard " + face.Guard + " を得る。");
            if (face.Heal > 0) sentences.Add("HP を " + face.Heal + " 回復する。");
            foreach (StatusGrant grant in face.StatusList)
            {
                sentences.Add((grant.OnSelf ? "自分に" : "") + grant.Kind.ToLabel() + "を " + grant.Stacks + " 付与する。");
            }
            if (face.Draw > 0) sentences.Add("カードを " + face.Draw + " 枚引く。");
            if (face.StaminaGain > 0) sentences.Add("スタミナ +" + face.StaminaGain + "。");
            if (face.Stance != null) sentences.Add("構え: " + StanceText(face.Stance) + "。");
            return string.Join("", sentences);
        }

        /// <summary>§4: what a stance does while it stands, as one clause ("毎ターン開始に Guard +3").</summary>
        public static string StanceText(StanceDef stance)
        {
            if (stance == null) return "";
            switch (stance.Hook)
            {
                case StanceHook.TurnStart:
                    return Spaced(WhenWord(stance, "始まるターンに"), Gains(stance.Guard, stance.Recovery, 0));
                case StanceHook.TurnEnd:
                    return Spaced(WhenWord(stance, "終えたターンに"), Gains(stance.Guard, 0, stance.NextRecovery));
                case StanceHook.AttackBonus:
                    switch (stance.When)
                    {
                        case StanceWhen.MovedThisTurn: return "ムーブを出したターン、アタック +" + stance.Power;
                        case StanceWhen.GapAtMost: return "間合い " + stance.Threshold + " の相手へのアタック +" + stance.Power;
                        case StanceWhen.TargetHasStatus:
                            return (stance.Status.HasValue ? stance.Status.Value.ToLabel() : "") + "中の敵へのアタック +" + stance.Power;
                        default: return "アタック +" + stance.Power;
                    }
                case StanceHook.OnHit:
                {
                    var parts = new List<string>();
                    if (stance.Stamina > 0) parts.Add("スタミナ +" + stance.Stamina);
                    if (stance.Guard > 0) parts.Add("Guard +" + stance.Guard);
                    if (stance.Status.HasValue) parts.Add("敵に" + stance.Status.Value.ToLabel() + " " + stance.StatusStacks);
                    return "被弾のたび" + (stance.OncePerTurn ? "（ターン 1 回）" : "") + string.Join("、", parts);
                }
                case StanceHook.PushImmune: return "押す / 引くを受けない";
                case StanceHook.BreakOnFoeMove: return "敵が自分で動くたび崩し " + stance.Break;
                default: return "";
            }
        }

        private static string WhenWord(StanceDef stance, string tail)
        {
            switch (stance.When)
            {
                case StanceWhen.GapAtLeast: return "間合い " + stance.Threshold + " 以上で" + tail;
                case StanceWhen.GapAtMost: return "間合い " + stance.Threshold + " 以下で" + tail;
                case StanceWhen.OmenAttack: return "予兆が攻撃のターン開始に";
                default: return "毎ターン開始に";
            }
        }

        /// <summary>Japanese then a Latin word ("Guard") get a half-width space between them (rules: 和欧間).</summary>
        private static string Spaced(string head, string tail)
        {
            bool latin = tail.Length > 0 && tail[0] < 0x80;
            return latin ? head + " " + tail : head + tail;
        }

        private static string Gains(int guard, int recovery, int nextRecovery)
        {
            var parts = new List<string>();
            if (guard > 0) parts.Add("Guard +" + guard);
            if (recovery > 0) parts.Add("回復 +" + recovery);
            if (nextRecovery > 0) parts.Add("次の回復 +" + nextRecovery);
            return string.Join("、", parts);
        }

        public static string ValueOf(Face face, BattleAttribute attributes)
        {
            if (attributes.HasFlag(BattleAttribute.Attack)) return face.Hits > 1 ? face.Power + "×" + face.Hits : face.Power.ToString();
            if (face.Guard > 0) return face.Guard.ToString();
            return face.Heal > 0 ? face.Heal.ToString() : "";
        }

        /// <summary>Every trait of the card on one lamp line; 背水の陣's two read "間合い2以上 +5 ／ 死力 +3".</summary>
        public static string TraitLines(CardDef def)
        {
            var lines = new List<string>();
            foreach (Trait trait in def.AllTraits) lines.Add(TraitLine(trait));
            return string.Join(" ／ ", lines);
        }

        /// <summary>The trait as printed on the lamp line: condition, then effect ("間合い0 +5", "残4 Guard+3").</summary>
        public static string TraitLine(Trait trait)
        {
            if (trait == null) return "";
            string effect = EffectWord(trait);
            string condition = ConditionWord(trait);
            return condition.Length == 0 ? effect : condition + " " + effect;
        }

        private static string ConditionWord(Trait trait)
        {
            switch (trait.Condition)
            {
                case BattleCore.TraitCondition.GapAtMost:
                    return trait.Threshold == 0 ? "間合い0" : "間合い" + trait.Threshold + "以下";
                case BattleCore.TraitCondition.GapAtLeast:
                    return "間合い" + trait.Threshold + "以上";
                case BattleCore.TraitCondition.Unguarded: return "無防備";
                case BattleCore.TraitCondition.Reserve: return "残" + trait.Threshold;
                case BattleCore.TraitCondition.Combo: return "連動" + AttributeWord(trait.Attribute);
                case BattleCore.TraitCondition.OmenIs: return "予兆" + (trait.Omen.HasValue ? OmenWord(trait.Omen.Value) : "");
                case BattleCore.TraitCondition.Desperate: return "死力";
                case BattleCore.TraitCondition.FirstPlay: return "初手";
                case BattleCore.TraitCondition.Finisher: return "締め";
                case BattleCore.TraitCondition.Broken: return "崩し後";
                case BattleCore.TraitCondition.Chain: return "連打";
                case BattleCore.TraitCondition.Thin: return "手薄";
                case BattleCore.TraitCondition.FoeHas: return "相手" + (trait.Watch.HasValue ? trait.Watch.Value.ToLabel() : "");
                case BattleCore.TraitCondition.SelfHas: return "自分" + (trait.Watch.HasValue ? trait.Watch.Value.ToLabel() : "");
                default: return "";
            }
        }

        private static string EffectWord(Trait trait)
        {
            switch (trait.Effect)
            {
                case TraitEffect.PowerBonus: return "+" + trait.Amount;
                case TraitEffect.GuardBonus: return "Guard+" + trait.Amount;
                case TraitEffect.NextTurnRecovery: return "回復+" + trait.Amount;
                case TraitEffect.HeavyBlow: return "重撃";
                case TraitEffect.StaminaGain: return "スタミナ+" + trait.Amount;
                case TraitEffect.Draw: return "ドロー+" + trait.Amount;
                case TraitEffect.Status:
                    return trait.Grant == null ? "" : (trait.Grant.OnSelf ? "自分" : "") + trait.Grant.Kind.ToLabel() + "+" + trait.Grant.Stacks;
                case TraitEffect.CostDown: return "コスト-" + trait.Amount;
                case TraitEffect.Convert: return "転換";
                case TraitEffect.FollowUp: return "追撃";
                case TraitEffect.BreakBonus: return "崩し+" + trait.Amount;
                default: return "";
            }
        }

        private static string AttributeWord(BattleAttribute attribute)
        {
            if (attribute.HasFlag(BattleAttribute.Attack)) return "(攻)";
            if (attribute.HasFlag(BattleAttribute.Guard)) return "(防)";
            if (attribute.HasFlag(BattleAttribute.Move)) return "(動)";
            if (attribute.HasFlag(BattleAttribute.Skill)) return "(技)";
            if (attribute.HasFlag(BattleAttribute.Stance)) return "(構)";
            return "";
        }

        private static string OmenWord(OmenKind kind)
        {
            switch (kind)
            {
                case OmenKind.Attack: return "攻撃";
                case OmenKind.Move: return "移動";
                case OmenKind.Guard: return "防御";
                default: return new OmenLabel(kind).ToText();
            }
        }

        // ---- omen ---------------------------------------------------------------------------

        /// <summary>
        /// §6: 種別 + 狙うマス, plus the action's own face value. Until the floor shows the aimed cells
        /// (#163) the reach goes where the one-character side used to be ("1〜2"). The trait bonus is
        /// not folded into the number: whether it lands depends on where the player stands when the
        /// blow comes.
        /// </summary>
        public static OmenFrame OmenOf(Omen omen, EnemyDef enemy)
        {
            if (omen == null) return new OmenFrame { Visible = false };

            var frame = new OmenFrame
            {
                Visible = true,
                KindLabel = new OmenLabel(omen.Label.Kind).ToText(),
                SideGlyph = omen.Label.Reach != null ? omen.Label.Reach.ToText() : "",
            };
            EnemyActionDef action;
            if (enemy.Actions.TryGetValue(omen.ActionId, out action))
            {
                frame.ValueText = ValueOf(action.Face, action.Attributes);
            }
            return frame;
        }

        // ---- units --------------------------------------------------------------------------

        /// <summary>
        /// One side's gauges. <paramref name="gap"/> is N, shown on the tag of the player only: the
        /// screen has one tag slot per side and one number says it all (#163 draws the cells).
        /// </summary>
        public static UnitFrame UnitOf(CombatantState unit, bool showStamina, int? gap, string stanceName = "")
        {
            var frame = new UnitFrame
            {
                Hp = unit.Hp,
                HpMax = unit.MaxHp,
                Guard = unit.Guard,
                ShowStamina = showStamina,
                Stamina = unit.Stamina,
                StaminaMax = unit.MaxStamina,
                HasRange = gap.HasValue,
            };
            if (gap.HasValue)
            {
                frame.Range = SideOf(gap.Value);
                frame.RangeGlyph = GapGlyph(gap.Value);
            }
            frame.Statuses = Chips(unit.Statuses, stanceName);
            return frame;
        }

        /// <summary>
        /// The chips under a gauge: the stance in the slot first (§4, #188; it has no stacks to count),
        /// then the status words in their fixed order.
        /// </summary>
        public static List<StatusChip> Chips(StatusSet statuses, string stanceName = "")
        {
            var chips = new List<StatusChip>();
            if (!string.IsNullOrEmpty(stanceName)) chips.Add(new StatusChip { Label = "構え・" + stanceName, Stacks = 0 });
            foreach (StatusKind kind in statuses.Kinds)
            {
                chips.Add(new StatusChip { Label = kind.ToLabel(), Stacks = statuses.Stacks(kind) });
            }
            return chips;
        }
    }
}
