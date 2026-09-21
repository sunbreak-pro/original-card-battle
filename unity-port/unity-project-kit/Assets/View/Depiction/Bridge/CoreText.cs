// Every string and glyph the screen prints about a core card, action or omen is written here, so
// the View keeps printing what it is given. Pure C#: BattleCore + Depiction.Script, no UnityEngine.
using System.Collections.Generic;
using BattleCore;

namespace Depiction.Bridge
{
    public static class CoreText
    {
        public static RangeSide Side(Position position)
        {
            return position == Position.Near ? RangeSide.Near : RangeSide.Far;
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
                Cost = def.Cost,
                Kind = kind,
                Aim = single ? CardAim.Single : CardAim.Self,
                Affects = single ? (UnitSide?)null : UnitSide.Player,
                TypeLabel = KindWord(kind) + (single ? "・敵単体" : "・自分"),
                Description = Describe(def.Face, def.Attributes),
                ValueText = ValueOf(def.Face, def.Attributes),
                TraitText = TraitLine(def.Trait),
                TraitLit = def.Trait != null && preview != null && preview.TraitHolds,
                // v4.2 cards can be played from either side; the side only changes what they do.
                RequiredRange = null,
                RequiredRangeGlyph = "",
            };
        }

        /// <summary>One short sentence per face, in resolution order. §5: a status reads 「<語> を n 付与する」.</summary>
        public static string Describe(Face face, BattleAttribute attributes)
        {
            var sentences = new List<string>();
            if (attributes.HasFlag(BattleAttribute.Attack)) sentences.Add("敵に " + face.Power + " ダメージ。");
            if (face.MoveTo.HasValue) sentences.Add(face.MoveTo.Value.ToLabel() + "間へ動く。");
            else if (face.FlipsSelfPosition) sentences.Add("間合いを反転する。");
            if (face.Guard > 0) sentences.Add("Guard " + face.Guard + " を得る。");
            if (face.Status.HasValue) sentences.Add(face.Status.Value.ToLabel() + "を " + face.StatusStacks + " 付与する。");
            if (face.Draw > 0) sentences.Add("カードを " + face.Draw + " 枚引く。");
            if (face.StaminaGain > 0) sentences.Add("スタミナ +" + face.StaminaGain + "。");
            return string.Join("", sentences);
        }

        public static string ValueOf(Face face, BattleAttribute attributes)
        {
            if (attributes.HasFlag(BattleAttribute.Attack)) return face.Power.ToString();
            return face.Guard > 0 ? face.Guard.ToString() : "";
        }

        /// <summary>The trait as printed on the lamp line: condition, then effect ("近 +5", "残4 Guard+3").</summary>
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
                case BattleCore.TraitCondition.SelfPosition:
                case BattleCore.TraitCondition.OpponentPosition:
                    return trait.ConditionPosition.HasValue ? trait.ConditionPosition.Value.ToLabel() : "";
                case BattleCore.TraitCondition.Unguarded: return "無防備";
                case BattleCore.TraitCondition.Reserve: return "残" + trait.Threshold;
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
                default: return "";
            }
        }

        // ---- omen ---------------------------------------------------------------------------

        /// <summary>
        /// §6: 種別 + 咎める側の一字, plus the action's own face value. The trait bonus is not folded into
        /// the number: whether it lands depends on where the player stands when the blow comes.
        /// </summary>
        public static OmenFrame OmenOf(Omen omen, EnemyDef enemy)
        {
            if (omen == null) return new OmenFrame { Visible = false };

            var frame = new OmenFrame
            {
                Visible = true,
                KindLabel = new OmenLabel(omen.Label.Kind).ToText(),
                SideGlyph = omen.Label.Side.HasValue ? omen.Label.Side.Value.ToLabel() : "",
            };
            EnemyActionDef action;
            if (enemy.Actions.TryGetValue(omen.ActionId, out action))
            {
                frame.ValueText = ValueOf(action.Face, action.Attributes);
            }
            return frame;
        }

        // ---- units --------------------------------------------------------------------------

        public static UnitFrame UnitOf(CombatantState unit, bool showStamina)
        {
            var frame = new UnitFrame
            {
                Hp = unit.Hp,
                HpMax = unit.MaxHp,
                Guard = unit.Guard,
                ShowStamina = showStamina,
                Stamina = unit.Stamina,
                StaminaMax = unit.MaxStamina,
                HasRange = unit.Position.HasValue,
            };
            if (unit.Position.HasValue)
            {
                frame.Range = Side(unit.Position.Value);
                frame.RangeGlyph = unit.Position.Value.ToLabel();
            }
            frame.Statuses = Chips(unit.Statuses);
            return frame;
        }

        public static List<StatusChip> Chips(StatusSet statuses)
        {
            var chips = new List<StatusChip>();
            foreach (StatusKind kind in statuses.Kinds)
            {
                chips.Add(new StatusChip { Label = kind.ToLabel(), Stacks = statuses.Stacks(kind) });
            }
            return chips;
        }
    }
}
