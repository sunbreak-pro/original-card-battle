// #574: a value added to BattleCore's enums without its words, or an id nobody knows, stops loudly
// instead of printing an empty field, falling into the bosses' chip family, or leaking
// KeyNotFoundException past the demo flow.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class UnknownValueTests
    {
        private static IEnumerable<CardDef> Cards()
        {
            yield return CardCatalog.All.First(c => c.Attribute.HasFlag(BattleAttribute.Attack));
            yield return CardCatalog.All.First(c => c.Attribute.HasFlag(BattleAttribute.Stance));
        }

        /// <summary>One trait with every field a condition or an effect reads filled in.</summary>
        private static Trait Full(BattleCore.TraitCondition condition, TraitEffect effect)
        {
            return new Trait(condition, effect, Amount: 2, Threshold: 1, Attribute: BattleAttribute.Attack,
                Omen: OmenKind.Attack, Watch: StatusKind.Bleed, Grant: new StatusGrant(StatusKind.Bleed, 2));
        }

        [Test]
        public void EveryTraitCondition_HasItsWords_OnTheFaceTheHoverAndTheLamp()
        {
            foreach (BattleCore.TraitCondition condition in Enum.GetValues(typeof(BattleCore.TraitCondition)))
            {
                Trait trait = Full(condition, TraitEffect.PowerBonus);
                foreach (CardDef def in Cards())
                {
                    Assert.That(CardFaceText.Condition(trait, def), Is.Not.Empty, condition + " / Condition / " + def.Name);
                    Assert.That(CardFaceText.ConditionClause(trait, def), Is.Not.Empty, condition + " / ConditionClause / " + def.Name);
                }
                Assert.That(CoreText.ConditionWord(trait), Is.Not.Empty, condition + " / ConditionWord");
            }
        }

        [Test]
        public void EveryTraitEffect_HasItsWords_OnTheFaceTheHoverAndTheLamp()
        {
            // TraitEffect.Status without a Grant is empty on purpose; Full always gives it one.
            foreach (TraitEffect effect in Enum.GetValues(typeof(TraitEffect)))
            {
                Trait trait = Full(BattleCore.TraitCondition.GapAtMost, effect);
                foreach (CardDef def in Cards())
                {
                    Assert.That(CardFaceText.Effect(trait, def), Is.Not.Empty, effect + " / Effect / " + def.Name);
                    Assert.That(CardFaceText.EffectClause(trait, def), Is.Not.Empty, effect + " / EffectClause / " + def.Name);
                }
                Assert.That(CoreText.EffectWord(trait), Is.Not.Empty, effect + " / EffectWord");
            }
        }

        [Test]
        public void EveryStanceHook_HasAClause()
        {
            foreach (StanceHook hook in Enum.GetValues(typeof(StanceHook)))
            {
                var stance = new StanceDef(hook, StanceWhen.Always, Threshold: 1, Guard: 2, Recovery: 1, NextRecovery: 1,
                    Power: 2, Stamina: 1, Status: StatusKind.Bleed, StatusStacks: 1, Break: 1, Attribute: BattleAttribute.Guard);
                Assert.That(CoreText.StanceText(stance), Is.Not.Empty, hook.ToString());
            }
        }

        [Test]
        public void EveryOmenKind_HasAWord()
        {
            foreach (OmenKind kind in Enum.GetValues(typeof(OmenKind)))
            {
                Assert.That(CoreText.OmenKindWord(kind), Is.Not.Empty, kind.ToString());
            }
        }

        [Test]
        public void AValueOutsideTheEnums_Throws_NamingTheValue()
        {
            CardDef def = Cards().First();
            Trait condition = Full((BattleCore.TraitCondition)999, TraitEffect.PowerBonus);
            Trait effect = Full(BattleCore.TraitCondition.GapAtMost, (TraitEffect)999);
            Assert.Multiple(() =>
            {
                Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => CardFaceText.Condition(condition, def)).Message, Does.Contain("999"));
                Assert.Throws<ArgumentOutOfRangeException>(() => CardFaceText.ConditionClause(condition, def));
                Assert.Throws<ArgumentOutOfRangeException>(() => CoreText.ConditionWord(condition));
                Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => CardFaceText.Effect(effect, def)).Message, Does.Contain("999"));
                Assert.Throws<ArgumentOutOfRangeException>(() => CardFaceText.EffectClause(effect, def));
                Assert.Throws<ArgumentOutOfRangeException>(() => CoreText.EffectWord(effect));
                Assert.Throws<ArgumentOutOfRangeException>(() => CoreText.StanceText(new StanceDef((StanceHook)999)));
                Assert.Throws<ArgumentOutOfRangeException>(() => CoreText.OmenKindWord((OmenKind)999));
                Assert.Throws<ArgumentOutOfRangeException>(() => StatusChipText.KindOf((StatusKind)999));
                Assert.Throws<ArgumentOutOfRangeException>(() => StatusChipText.GroupWord((ChipKind)999));
            });
        }

        // ---- §7.3 the chip families, word by word ----

        private static readonly Dictionary<StatusKind, ChipKind> Families = new Dictionary<StatusKind, ChipKind>
        {
            { StatusKind.Slow, ChipKind.OnFoe },
            { StatusKind.Swift, ChipKind.OnSelf },
            { StatusKind.Bleed, ChipKind.OnFoe },
            { StatusKind.Fragile, ChipKind.OnFoe },
            { StatusKind.Intimidate, ChipKind.OnFoe },
            { StatusKind.Fatigue, ChipKind.OnFoe },
            { StatusKind.Empower, ChipKind.OnSelf },
            { StatusKind.Focus, ChipKind.OnSelf },
            { StatusKind.Parry, ChipKind.OnSelf },
            { StatusKind.Regen, ChipKind.OnSelf },
            { StatusKind.MiasmaShroud, ChipKind.Boss },
            { StatusKind.Binding, ChipKind.Boss },
            { StatusKind.Hook, ChipKind.Boss },
            { StatusKind.Depths, ChipKind.Boss },
            { StatusKind.Rooting, ChipKind.Boss },
            { StatusKind.Withering, ChipKind.Boss },
        };

        [Test]
        public void EveryStatusWord_IsInTheFamilyTheTableGivesIt()
        {
            foreach (StatusKind kind in Enum.GetValues(typeof(StatusKind)))
            {
                Assert.That(Families.ContainsKey(kind), Is.True, kind + " has no row in this table: give it its family here and in StatusChipText.KindOf");
                Assert.That(StatusChipText.KindOf(kind), Is.EqualTo(Families[kind]), kind.ToString());
                Assert.That(StatusChipText.Chip(kind, 1).Group, Is.EqualTo(StatusChipText.GroupWord(Families[kind])), kind.ToString());
            }
        }

        // ---- the demo's enemy ids ----

        [Test]
        public void AnUnknownEnemyId_InTheDemo_IsAnArgumentException_ListingTheKnownIds()
        {
            Departure departure = Departure.Plain(DeckBuilder.Random(9).Build());
            string known = Enemies.All.First().Id;
            Assert.Multiple(() =>
            {
                var single = Assert.Throws<ArgumentException>(() => DemoSession.Single(departure, "no_such_enemy", 1));
                Assert.That(single.Message, Does.Contain("no_such_enemy"));
                Assert.That(single.Message, Does.Contain(known));
                var chain = Assert.Throws<ArgumentException>(() => DemoSession.Chain(departure, 1, new[] { "no_such_enemy" }));
                Assert.That(chain.Message, Does.Contain("no_such_enemy"));
                Assert.That(chain.Message, Does.Contain(known));
            });
        }
    }
}
