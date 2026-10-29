// #575: the words the screen says about a card's attribute, an omen's kind and a trait's condition
// come from one table each. Runs under `dotnet test` (Depiction.Bridge.Tests.csproj) and in Unity's
// EditMode runner from the same file.
using System;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class TraitConditionWordsTests
    {
        /// <summary>
        /// A trait of <paramref name="condition"/> and the card that carries it: the first card in the
        /// catalogue with one, else a trait written here on 袈裟斬り with every field the forms read.
        /// </summary>
        private static (Trait Trait, CardDef Card) SampleOf(BattleCore.TraitCondition condition)
        {
            foreach (CardDef def in CardCatalog.All)
            {
                foreach (Trait trait in def.AllTraits)
                {
                    if (trait.Condition == condition) return (trait, def);
                }
            }
            var written = new Trait(condition, TraitEffect.PowerBonus, Amount: 3, Threshold: 2,
                Attribute: BattleAttribute.Attack, Omen: OmenKind.Attack, Watch: StatusKind.Bleed);
            return (written, CardCatalog.KesaCut);
        }

        [Test]
        public void EveryTraitCondition_HasAllFourForms_AndNoneIsEmpty()
        {
            foreach (BattleCore.TraitCondition condition in Enum.GetValues(typeof(BattleCore.TraitCondition)))
            {
                CardFaceText.ConditionWords words = CardFaceText.ConditionWords.Of(condition);
                var (trait, card) = SampleOf(condition);
                string at = condition + " on " + card.Id;
                Assert.That(words.Short(trait), Is.Not.Empty, at + ": the short form (the fired trait's line)");
                Assert.That(words.Box(trait, card), Is.Not.Empty, at + ": the trait box");
                Assert.That(words.Clause(trait, card), Is.Not.Empty, at + ": the clause");
                Assert.That(words.Term, Is.Not.Empty, at + ": the word it is known by");
                // The short form and the switches that used to word it now read the same row.
                Assert.That(CoreText.ConditionWord(trait), Is.EqualTo(words.Short(trait)), at);
                Assert.That(CardFaceText.Condition(trait, card), Is.EqualTo(words.Box(trait, card)), at);
                Assert.That(CardFaceText.ConditionClause(trait, card), Is.EqualTo(words.Clause(trait, card)), at);
                if (!words.Glossed) continue;
                string key = words.Term == "連動" ? "連動 X" : words.Term;
                Assert.That(CardFaceText.Glossary.ContainsKey(key), Is.True, at + ": a glossed term is a §7.5 word");
            }
            Assert.That(CardFaceText.ConditionWords.Table.Count, Is.EqualTo(Enum.GetValues(typeof(BattleCore.TraitCondition)).Length),
                "one row a condition, no row for a name that is not one");
        }

        [Test]
        public void TheFormsKeepTheirWords()
        {
            // The table moved the words, it did not change them (the short line's attribute aside, below).
            Trait unguarded = new Trait(BattleCore.TraitCondition.Unguarded, TraitEffect.PowerBonus, 3);
            Assert.That(CoreText.ConditionWord(unguarded), Is.EqualTo("無防備"));
            Assert.That(CardFaceText.Condition(unguarded, CardCatalog.KesaCut), Is.EqualTo("敵の Guard 0"));
            Trait reserve = new Trait(BattleCore.TraitCondition.Reserve, TraitEffect.NextTurnRecovery, 1, Threshold: 3);
            Assert.That(CoreText.ConditionWord(reserve), Is.EqualTo("残3"));
            Assert.That(CardFaceText.Condition(reserve, CardCatalog.Brace), Is.EqualTo("温存 残り 3 以上"));
            Assert.That(CardFaceText.ConditionClause(reserve, CardCatalog.Brace), Is.EqualTo("出した後スタミナが 3 以上残れば"));
            Trait adjacent = new Trait(BattleCore.TraitCondition.GapAtMost, TraitEffect.PowerBonus, 5, Threshold: 0);
            Assert.That(CardFaceText.ConditionClause(adjacent, CardCatalog.KesaCut), Is.EqualTo("隣り合っていれば"));
        }

        [Test]
        public void TheAttributeWords_AreTheCardsFour_Everywhere()
        {
            // battle-visual-v1 decision 15: 攻撃・防御・スキル・スタンス, in the short line too.
            var four = new[] { BattleAttribute.Attack, BattleAttribute.Guard, BattleAttribute.Skill, BattleAttribute.Stance };
            Assert.That(four.Select(CardFaceText.AttributeWord), Is.EqualTo(new[] { "攻撃", "防御", "スキル", "スタンス" }));
            Assert.That(four.Select(DeckBuilder.AttributeWords), Is.EqualTo(four.Select(CardFaceText.AttributeWord)));
            Assert.That(new[] { CardKind.Attack, CardKind.Guard, CardKind.Skill, CardKind.Stance }.Select(CoreText.KindWord),
                Is.EqualTo(four.Select(CardFaceText.AttributeWord)));
            Trait combo = new Trait(BattleCore.TraitCondition.Combo, TraitEffect.StaminaGain, 1, Attribute: BattleAttribute.Skill);
            Assert.That(CoreText.ConditionWord(combo), Is.EqualTo("連動(スキル)"));
            Assert.That(CardFaceText.Condition(combo, CardCatalog.KesaCut), Is.EqualTo("連動 スキル"));
            Assert.That(() => CoreText.KindWord(CardKind.Move), Throws.InstanceOf<ArgumentOutOfRangeException>(),
                "no core card counts as ムーブ (decision 15)");
        }

        [Test]
        public void TheOmenKindWords_AreBattleCoresOwn()
        {
            foreach (OmenKind kind in Enum.GetValues(typeof(OmenKind)))
            {
                Assert.That(CoreText.OmenKindWord(kind), Is.EqualTo(new OmenLabel(kind).ToText()), kind.ToString());
            }
            Assert.That(CoreText.OmenKindWord(OmenKind.Rest), Is.EqualTo("休み"));
            Assert.That(CoreText.OmenKindWord(OmenKind.Stance), Is.EqualTo("構え"), "the omen keeps 構え; the card's attribute is スタンス");
        }

        [Test]
        public void WitheringsPanel_SaysTheRecoveryFloor()
        {
            // roster §6.2: 疲労 included, 枯らし never takes the recovery below 1.
            string text = StatusChipText.Effect(StatusKind.Withering, 1);
            Assert.That(text, Does.Contain(Statuses.WitheringRecoveryFloor + " より下がらない"));
        }
    }
}
