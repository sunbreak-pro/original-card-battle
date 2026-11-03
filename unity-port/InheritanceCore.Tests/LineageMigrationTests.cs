using System;
using System.Collections.Generic;
using System.Linq;
using InheritanceCore;
using NUnit.Framework;

namespace InheritanceCore.Tests
{
    /// <summary>
    /// The lineage save's version policy (#109, dungeon_exploration_v4.md §7.9): an older save is converted step by
    /// step, a newer one is refused, a version with no step is refused. The version-2 steps here are made up for the
    /// test; the build has none while Inheritance.SaveVersion is 1.
    /// </summary>
    public class LineageMigrationTests
    {
        private static JsonObject SaveOf(int version)
        {
            var s = Inheritance.StartLife(Inheritance.NewLineage(42), 1);
            var lineage = Inheritance.RecordDeath(s.Lineage, s.Life, new LifePlace(3, 2, null, DungeonCore.RunPhase.Fallen),
                InheritanceTests.FullDeck, InheritanceTests.FullJournal);
            var json = Inheritance.ToJson(lineage);
            json["Version"] = version;
            return json;
        }

        /// <summary>A made-up shape change: version n gains a key "Step{n}" and becomes n + 1.</summary>
        private static LineageMigration Step(int from) => new LineageMigration(from, save =>
        {
            save[$"Step{from}"] = true;
            save["Version"] = from + 1;
            return save;
        });

        private static void Refused(TestDelegate fn, string pattern)
        {
            var e = Assert.Throws<InheritanceRuleException>(fn);
            Assert.That(e!.Message, Does.Match(pattern));
        }

        [Test]
        public void EveryVersionBelowTheBuildsHasItsStep()
        {
            // Raising SaveVersion without the step that reads the old version would refuse every save players have.
            Assert.That(LineageMigrations.Steps.Select(s => s.From), Is.EqualTo(Enumerable.Range(1, Inheritance.SaveVersion - 1)));
        }

        [Test]
        public void ASaveOfThisBuildsVersion_ReadsAsItIs()
        {
            var save = SaveOf(Inheritance.SaveVersion);
            Assert.That(LineageMigrations.Upgrade(save), Is.SameAs(save));
            string text = Json.Stringify(save);
            Assert.That(Inheritance.Serialize(Inheritance.FromJson(Json.Parse(text))), Is.EqualTo(text));
        }

        [Test]
        public void AnOlderSave_GoesThroughEveryStepInOrder_OnACopy()
        {
            var save = SaveOf(1);
            string before = Json.Stringify(save);
            var steps = new[] { Step(2), Step(1) };   // the order given does not matter: each step names the version it reads
            var upgraded = LineageMigrations.Upgrade(save, 3, steps);
            Assert.That(upgraded["Version"], Is.EqualTo(3));
            Assert.That(upgraded["Step1"], Is.EqualTo(true));
            Assert.That(upgraded["Step2"], Is.EqualTo(true));
            Assert.That(Json.Stringify(save), Is.EqualTo(before), "the stored save is not changed");
            upgraded.Remove("Step1");
            upgraded.Remove("Step2");
            upgraded["Version"] = 1;
            Assert.That(Json.Stringify(upgraded), Is.EqualTo(before), "the steps change only what they say");
        }

        [Test]
        public void ASaveNewerThanTheBuild_IsRefused()
        {
            Refused(() => LineageMigrations.Upgrade(SaveOf(2), 1, new[] { Step(1) }), "^bad save: version 2 is newer than this build reads \\(1\\)$");
            Refused(() => Inheritance.FromJson(SaveOf(Inheritance.SaveVersion + 1)), "is newer than this build reads");
        }

        [Test]
        public void AVersionWithNoStep_IsRefused_NotGuessed()
        {
            Refused(() => LineageMigrations.Upgrade(SaveOf(1), 3, new[] { Step(1) }), "^bad save: version 2 has no step to version 3$");
            Refused(() => LineageMigrations.Upgrade(SaveOf(1), 2, Array.Empty<LineageMigration>()), "^bad save: version 1 has no step to version 2$");
        }

        [Test]
        public void AStepThatDoesNotLandOnTheNextVersion_IsRefused()
        {
            var skips = new LineageMigration(1, save =>
            {
                save["Version"] = 3;
                return save;
            });
            Refused(() => LineageMigrations.Upgrade(SaveOf(1), 3, new[] { skips }), "^bad save: the step from version 1 gave version 3$");
            var stays = new LineageMigration(1, save => save);
            Refused(() => LineageMigrations.Upgrade(SaveOf(1), 2, new[] { stays }), "^bad save: the step from version 1 gave version 1$");
            Assert.Throws<ArgumentException>(() => LineageMigrations.Upgrade(SaveOf(1), 2, new[] { Step(1), Step(1) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LineageMigration(0, s => s));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(1.5)]
        [TestCase("1")]
        [TestCase("missing")]
        public void AVersionThatIsNotAnIntegerFromOne_IsRefused(object version)
        {
            var save = SaveOf(1);
            if (Equals(version, "missing")) save.Remove("Version");
            else save["Version"] = version;
            Refused(() => Inheritance.FromJson(save), "^bad save: version ");
        }
    }
}
