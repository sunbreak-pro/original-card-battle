// The five cards the depiction demo fights with, and what each one does. Pure C#: no UnityEngine,
// no BattleCore. Numbers are the §18 draft scale (attack 6 / 13 / 21, guard 4 / 9 / 15).
//
// One table, two readers: TurnSliceScript prints these faces into its fixed script, and LiveTurn
// plays them by their rules. Keeping them apart is how the two drifted before.
using System;
using System.Collections.Generic;

namespace Depiction
{
    /// <summary>When a card's printed trait adds its bonus.</summary>
    public enum TraitCondition
    {
        None,
        /// <summary>Nothing has been played yet this turn ("初手 +3").</summary>
        FirstCardOfTurn,
        /// <summary>The enemy's declared next action is an attack ("予兆 +3").</summary>
        AgainstAttackOmen,
    }

    /// <summary>One card kind: what it prints, and the settled numbers it produces.</summary>
    public sealed class DemoCard
    {
        public string DefId = "";
        public string Name = "";
        public int Cost;
        public CardKind Kind;
        public CardAim Aim;
        public UnitSide? Affects;
        public string TypeLabel = "";
        public string Description = "";
        public string ValueText = "";
        public string TraitText = "";
        public TraitCondition Trait = TraitCondition.None;
        /// <summary>The range side the card may be played from; null when either side works.</summary>
        public RangeSide? RequiredRange;

        public int Damage;
        public int Guard;
        /// <summary>The side the player ends up on; null when the card does not move.</summary>
        public RangeSide? MovesTo;
        public int Draw;
    }

    public static class DemoDeck
    {
        public const string Kesagiri = "kesagiri";
        public const string Daijodan = "daijodan";
        public const string Ushirotobi = "ushirotobi";
        public const string TetsuNoUke = "tetsu-no-uke";
        public const string Kansatsu = "kansatsu";

        /// <summary>Both printed traits add the same amount (§18 draft).</summary>
        public const int TraitBonus = 3;

        /// <summary>How many of each kind the live deck holds.</summary>
        public const int Copies = 2;

        public static readonly IList<DemoCard> All = new List<DemoCard>
        {
            new DemoCard
            {
                DefId = Kesagiri, Name = "袈裟斬り", Cost = 1, Kind = CardKind.Attack, Aim = CardAim.Single,
                TypeLabel = "攻撃・敵単体", Description = "敵に 6 ダメージ。ターン最初なら +3。",
                ValueText = "6", TraitText = "初手 +3", Trait = TraitCondition.FirstCardOfTurn,
                RequiredRange = RangeSide.Near, Damage = 6,
            },
            new DemoCard
            {
                DefId = Daijodan, Name = "大上段", Cost = 2, Kind = CardKind.Attack, Aim = CardAim.Single,
                TypeLabel = "攻撃・敵単体", Description = "敵に 13 ダメージ。振りかぶる一撃。",
                ValueText = "13", RequiredRange = RangeSide.Near, Damage = 13,
            },
            new DemoCard
            {
                DefId = Ushirotobi, Name = "後ろ跳び", Cost = 1, Kind = CardKind.Move, Aim = CardAim.Self,
                Affects = UnitSide.Player,
                TypeLabel = "ムーブ・自分", Description = "遠間へ下がる。Guard 4 を得る。予兆が攻撃なら +3。",
                ValueText = "4", TraitText = "予兆 +3", Trait = TraitCondition.AgainstAttackOmen,
                RequiredRange = RangeSide.Near, Guard = 4, MovesTo = RangeSide.Far,
            },
            new DemoCard
            {
                DefId = TetsuNoUke, Name = "鉄の受け", Cost = 2, Kind = CardKind.Guard, Aim = CardAim.Self,
                Affects = UnitSide.Player,
                TypeLabel = "防御・自分", Description = "Guard 9 を得る。重い一撃に備える。",
                ValueText = "9", Guard = 9,
            },
            new DemoCard
            {
                // Its text follows swordsman_cards_v4 (draw 1 at the first tier).
                DefId = Kansatsu, Name = "観察", Cost = 1, Kind = CardKind.Skill, Aim = CardAim.Single,
                TypeLabel = "技・敵単体", Description = "カードを 1 枚引く。相手をよく見る。",
                Draw = 1,
            },
        };

        public static DemoCard Def(string defId)
        {
            foreach (DemoCard card in All)
            {
                if (card.DefId == defId) return card;
            }
            throw new ArgumentOutOfRangeException(nameof(defId), defId, "not a demo card");
        }

        /// <summary>The kind an instance id belongs to ("kesagiri#1" -> "kesagiri").</summary>
        public static string DefIdOf(string instanceId)
        {
            int hash = instanceId.IndexOf('#');
            return hash < 0 ? instanceId : instanceId.Substring(0, hash);
        }

        /// <summary>How a range side is printed. The View prints the string as given.</summary>
        public static string Glyph(RangeSide range)
        {
            return range == RangeSide.Near ? "近" : "遠";
        }

        /// <summary>
        /// The face of one dealt card. <paramref name="instanceId"/> tells copies apart, so the hand
        /// can hold two of a kind and the screen still knows which one was dragged.
        /// </summary>
        public static CardFace Face(DemoCard def, string instanceId, bool traitLit)
        {
            return new CardFace
            {
                Id = instanceId,
                Name = def.Name,
                Cost = def.Cost,
                Kind = def.Kind,
                Aim = def.Aim,
                Affects = def.Affects,
                TypeLabel = def.TypeLabel,
                Description = def.Description,
                ValueText = def.ValueText,
                TraitText = def.TraitText,
                TraitLit = traitLit && def.Trait != TraitCondition.None,
                RequiredRange = def.RequiredRange,
                RequiredRangeGlyph = def.RequiredRange.HasValue ? Glyph(def.RequiredRange.Value) : "",
            };
        }
    }
}
