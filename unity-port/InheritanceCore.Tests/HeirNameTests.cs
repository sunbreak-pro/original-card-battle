using System;
using System.Linq;
using DungeonCore;
using InheritanceCore;
using NUnit.Framework;
using static InheritanceCore.Tests.InheritanceTests;

namespace InheritanceCore.Tests
{
    /// <summary>
    /// The name the player gives the next heir in the hall (#105, concept-v3 §16.1, decided 2026-11-03): the drawn name
    /// is the default, the player may write over it, and the name written is the one every record keeps.
    /// </summary>
    public class HeirNameTests
    {
        private const ulong Seed = 42;

        [Test]
        public void NothingTyped_KeepsTheDrawnName()
        {
            Assert.That(Inheritance.ChosenHeirName(null, "ルカ"), Is.EqualTo("ルカ"));
            Assert.That(Inheritance.ChosenHeirName("", "ルカ"), Is.EqualTo("ルカ"));
            Assert.That(Inheritance.ChosenHeirName(" 　\t", "ルカ"), Is.EqualTo("ルカ"), "only blanks (the full-width space too)");
            Assert.That(Inheritance.ChosenHeirName("\n\r\u2028", "ルカ"), Is.EqualTo("ルカ"));
            Assert.Throws<ArgumentNullException>(() => Inheritance.ChosenHeirName("ミナ", null!));
        }

        [Test]
        public void ATypedName_IsTrimmed_LosesItsControlCharacters_AndIsCutAtTheLimit()
        {
            Assert.That(Inheritance.ChosenHeirName("  ミナ　", "ルカ"), Is.EqualTo("ミナ"));
            Assert.That(Inheritance.ChosenHeirName("ミ\nナ\t", "ルカ"), Is.EqualTo("ミナ"), "a pasted line break does not reach the records");
            Assert.That(Inheritance.ChosenHeirName("アイウエオカキクケコ", "ルカ"), Is.EqualTo("アイウエオカキク"));
            Assert.That(InheritanceRules.HeirNameMaxLength, Is.EqualTo(8));
            Assert.That(Inheritance.ChosenHeirName("アイウエオカキ ケ", "ルカ"), Is.EqualTo("アイウエオカキ"), "a cut that ends on a blank is trimmed");
            string flowers = string.Concat(Enumerable.Repeat("\U0001F338", 9));
            Assert.That(Inheritance.ChosenHeirName(flowers, "ルカ"), Is.EqualTo(string.Concat(Enumerable.Repeat("\U0001F338", 8))),
                "a letter outside the BMP counts once and is never split");
            Assert.That(Inheritance.ChosenHeirName("ガ", "ルカ"), Is.EqualTo("ガ"));
        }

        [Test]
        public void StartLife_WithoutAName_IsTheDrawnHeir()
        {
            var lineage = Inheritance.NewLineage(Seed);
            var drawn = Inheritance.NextHeir(lineage);
            Assert.That(Inheritance.StartLife(lineage, 1).Life.Name, Is.EqualTo(drawn.Name));
            Assert.That(Inheritance.StartLife(lineage, 1, null).Life.Name, Is.EqualTo(drawn.Name));
            Assert.That(Inheritance.StartLife(lineage, 1, "   ").Life.Name, Is.EqualTo(drawn.Name));
            Assert.That(Inheritance.Serialize(Inheritance.StartLife(lineage, 1, drawn.Name).Lineage),
                Is.EqualTo(Inheritance.Serialize(Inheritance.StartLife(lineage, 1).Lineage)), "typing the drawn name changes nothing");
        }

        [Test]
        public void ATypedName_StaysInTheLife_TheTrace_ThePastLives_TheEndingAndTheSave()
        {
            var start = Inheritance.StartLife(Inheritance.NewLineage(Seed), 1, "ミナ");
            Assert.That(start.Life.Name, Is.EqualTo("ミナ"));
            Assert.That(Inheritance.NextHeir(start.Lineage).Name, Is.EqualTo("ミナ"), "the life in progress is named so");
            Assert.That(start.Lineage.Current!.Name, Is.EqualTo("ミナ"));

            var died = Inheritance.RecordDeath(start.Lineage, start.Life, new LifePlace(3, 2, NodeKind.Battle, RunPhase.Fallen), FullDeck,
                new Journal(memo: "門竜は押すと崩れる"));
            Assert.That(died.Lives[0].Name, Is.EqualTo("ミナ"));
            Assert.That(died.Traces[0].Name, Is.EqualTo("ミナ"));
            Assert.That(died.Traces[0].Candidates.Single(c => c.Id == "memo").From, Is.EqualTo("ミナ"));
            Assert.That(Inheritance.HallView(died).PastLives[0].Name, Is.EqualTo("ミナ"));
            Assert.That(Inheritance.EndingView(died).HeirLine, Is.EqualTo(InheritanceText.HeirLine(1, "ミナ")));

            var back = Inheritance.Deserialize(Inheritance.Serialize(died));
            Assert.That(back.Lives[0].Name, Is.EqualTo("ミナ"));
            Assert.That(back.Traces[0].Name, Is.EqualTo("ミナ"));
            Assert.That(Inheritance.Serialize(back), Is.EqualTo(Inheritance.Serialize(died)));

            var next = Inheritance.StartLife(died, 1);
            Assert.That(next.Life.Name, Is.EqualTo(Inheritance.HeirName(Seed, 2)), "the next heir is drawn again");
            Assert.That(next.Life.Tier, Is.EqualTo(Inheritance.TierAfter(died.Lives[0])));
            Assert.That(Json.Stringify(Inheritance.TraceNodeView(next.Lineage, 3)!.ToJson()), Does.Contain("ミナ"), "the trace node says whose it is");
        }

        [Test]
        public void ATypedSurvivor_PassesItsNameWithTheJournal()
        {
            var start = Inheritance.StartLife(Inheritance.NewLineage(Seed), 1, "ヨナ");
            var survived = Inheritance.RecordSurvival(start.Lineage, start.Life, new LifePlace(2, 2, null, RunPhase.Survived), FullDeck,
                new Journal(memo: "生き延びた"));
            Assert.That(survived.Survivor!.Name, Is.EqualTo("ヨナ"));
            var next = Inheritance.StartLife(survived, 1, "テア");
            Assert.That(next.Life.Name, Is.EqualTo("テア"));
            Assert.That(next.JournalFrom!.Name, Is.EqualTo("ヨナ"));
            Assert.That(Inheritance.HeirJournal(next.Journal, next.JournalFrom).Scribbles.Single().Name, Is.EqualTo("ヨナ"));
        }

        [Test]
        public void AFullLengthName_IsKeptWholeByTheSave()
        {
            var start = Inheritance.StartLife(Inheritance.NewLineage(Seed), 1, "アイウエオカキク");
            var back = Inheritance.Deserialize(Inheritance.Serialize(start.Lineage));
            Assert.That(back.Current!.Name, Is.EqualTo("アイウエオカキク"));
        }
    }
}
