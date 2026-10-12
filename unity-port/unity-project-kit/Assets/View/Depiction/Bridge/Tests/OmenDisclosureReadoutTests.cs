// #455: the journal's 開示度 on the script (battle-visual-v1 §4.4). The writer is fully disclosed
// unless told otherwise, and below 2 it leaves the omen's and the plan's numbers off while the kind
// stays. Runs under `dotnet test` (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class OmenDisclosureReadoutTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static EnemyActionDef Strike(string id) =>
            new EnemyActionDef(id, id, BattleAttribute.Attack, 1, new Face(Power: 4));

        private static EnemyActionDef Shell(string id) =>
            new EnemyActionDef(id, id, BattleAttribute.Guard, 1, new Face(Guard: 2), Targets: TargetKind.Self);

        private static EnemyActionDef Stay(string id) =>
            new EnemyActionDef(id, id, BattleAttribute.None, 1, new Face(Move: 0), Targets: TargetKind.Self);

        /// <summary>An elite that, at gap 0, strikes and then plans a shell: an attack omen with a guard plan.</summary>
        private static EnemyDef Elite()
        {
            EnemyActionDef strike = Strike("strike"), shell = Shell("shell"), far = Stay("far");
            var actions = new Dictionary<string, EnemyActionDef> { [strike.Id] = strike, [shell.Id] = shell, [far.Id] = far };
            return new EnemyDef(
                "elite", "elite", 60, MaxStamina: 10, Recovery: 2, Size: 1,
                new[] { strike.Id, shell.Id }, new[] { strike.Id }, new[] { far.Id },
                actions, EnemyRank.Elite, ActionsPerPhase: 2);
        }

        /// <summary>An elite that, at gap 0, puts 鈍足 and then plans 出血: a 状態付与 omen and plan.</summary>
        private static EnemyDef Binder()
        {
            var bind = new EnemyActionDef("bind", "bind", BattleAttribute.Skill, 1,
                new Face(Reach: Reach.Only(0), Statuses: new[] { new StatusGrant(StatusKind.Slow, 1) }), Omen: OmenKind.Skill);
            var cut = new EnemyActionDef("cut", "cut", BattleAttribute.Skill, 1,
                new Face(Reach: Reach.Only(0), Statuses: new[] { new StatusGrant(StatusKind.Bleed, 1) }), Omen: OmenKind.Skill);
            EnemyActionDef far = Stay("far");
            var actions = new Dictionary<string, EnemyActionDef> { [bind.Id] = bind, [cut.Id] = cut, [far.Id] = far };
            return new EnemyDef(
                "binder", "binder", 60, MaxStamina: 10, Recovery: 2, Size: 1,
                new[] { bind.Id, cut.Id }, new[] { bind.Id }, new[] { far.Id },
                actions, EnemyRank.Elite, ActionsPerPhase: 2);
        }

        private static List<CardInstance> Deck()
        {
            CardDef filler = new CardDef("filler", "filler", BattleAttribute.Guard, 1, new Face(Guard: 1), Targets: TargetKind.Self);
            var deck = new List<CardInstance>();
            for (int i = 0; i < Constants.DeckMin; i++) deck.Add(new CardInstance("filler-" + i, filler));
            return deck;
        }

        /// <summary>The omen at the first turn start, written by a writer set to <paramref name="disclosure"/> (none: left at its default).</summary>
        private static OmenFrame TurnStartOmen(int? disclosure, EnemyDef enemyDef = null)
        {
            EnemyDef enemy = enemyDef ?? Elite();
            BattleState state = TurnLoop.Start(new BattleSetup(enemy, Deck(), 8, StartGap: 0), NoShuffle).State;
            var writer = new CoreScriptWriter(enemy);
            if (disclosure.HasValue) writer.Disclosure = disclosure.Value;
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            return writer.Write(begin.Events, begin.State).Last().After.Omen;
        }

        [Test]
        public void TheWriter_IsFullyDisclosed_UntilTheJournalHandsALevelIn()
        {
            Assert.That(new CoreScriptWriter(Elite()).Disclosure, Is.EqualTo(OmenDisclosure.Full));
            OmenFrame omen = TurnStartOmen(null);
            Assert.Multiple(() =>
            {
                Assert.That(omen.Disclosure, Is.EqualTo(2));
                Assert.That(omen.Icon, Is.EqualTo(OmenIcon.Attack));
                Assert.That(omen.ValueText, Is.Not.Empty, "the attack's number shows at 2");
                Assert.That(omen.PlanVisible, Is.True);
                Assert.That(omen.PlanValueText, Is.EqualTo("2"), "shell's face Guard");
            });
        }

        [TestCase(1)]
        [TestCase(0)]
        public void BelowTwo_TheBadgeKeepsTheKind_AndLosesTheNumbers(int level)
        {
            OmenFrame omen = TurnStartOmen(level);
            Assert.Multiple(() =>
            {
                Assert.That(omen.Disclosure, Is.EqualTo(level));
                Assert.That(omen.Visible, Is.True);
                Assert.That(omen.Icon, Is.EqualTo(OmenIcon.Attack));
                Assert.That(omen.KindLabel, Is.EqualTo("攻撃"));
                Assert.That(omen.ValueText, Is.Empty);
                Assert.That(omen.PlanValueText, Is.Empty);
            });
        }

        [Test]
        public void ReadToOne_ThePlanShowsItsKind()
        {
            OmenFrame omen = TurnStartOmen(1);
            Assert.Multiple(() =>
            {
                Assert.That(omen.PlanVisible, Is.True);
                Assert.That(omen.PlanIcon, Is.EqualTo(OmenIcon.Guard));
                Assert.That(omen.PlanKindLabel, Is.EqualTo("防御"));
            });
        }

        [Test]
        public void ReadToZero_ThePlanIsNotShown()
        {
            // battle_ui_ux_v2 §7 / §5.16 順 2 (#455): 開示度 0 は 1 手目の種別だけ.
            OmenFrame omen = TurnStartOmen(0);
            Assert.That(omen.Visible, Is.True);
            Assert.That(omen.PlanVisible, Is.False);
        }

        [Test]
        public void ReadToTwo_AStatusApplyingOmenAndPlan_CarryTheirStatuses()
        {
            // battle-visual-v1 §4.4 状態付与 (#455): the stamp, and the status where a number would stand.
            OmenFrame omen = TurnStartOmen(2, Binder());
            Assert.Multiple(() =>
            {
                Assert.That(omen.Icon, Is.EqualTo(OmenIcon.Seal));
                Assert.That(omen.Statuses.Select(s => s.Glyph), Is.EqualTo(new[] { StatusGlyph.Slow }));
                Assert.That(omen.PlanVisible, Is.True);
                Assert.That(omen.PlanIcon, Is.EqualTo(OmenIcon.Seal));
                Assert.That(omen.PlanStatuses.Select(s => s.Glyph), Is.EqualTo(new[] { StatusGlyph.Bleed }));
            });
        }

        [Test]
        public void ReadToOne_AStatusApplyingOmen_KeepsTheStamp_WithoutItsStatuses()
        {
            // §4.4: 数字と状態のアイコンは開示度 2 の敵にだけ. The stamp is the kind's icon and stays.
            OmenFrame omen = TurnStartOmen(1, Binder());
            Assert.Multiple(() =>
            {
                Assert.That(omen.Icon, Is.EqualTo(OmenIcon.Seal));
                Assert.That(omen.Statuses, Is.Empty);
                Assert.That(omen.PlanIcon, Is.EqualTo(OmenIcon.Seal));
                Assert.That(omen.PlanStatuses, Is.Empty);
            });
        }

        [Test]
        public void TheCoreSource_PassesTheLevelToItsWriter()
        {
            var deck = Deck();
            var full = new CoreBattleSource(new BattleSetup(Elite(), deck, 8, StartGap: 0), 7);
            var read = new CoreBattleSource(new BattleSetup(Elite(), deck, 8, StartGap: 0), 7, disclosure: 1);
            Assert.That(full.Frame.Omen.Disclosure, Is.EqualTo(2));
            Assert.That(read.Frame.Omen.Disclosure, Is.EqualTo(1));
        }
    }
}
