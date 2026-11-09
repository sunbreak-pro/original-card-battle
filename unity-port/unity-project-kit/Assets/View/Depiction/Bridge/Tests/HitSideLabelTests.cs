// #455 (battle_ui_ux_v2 §5.0 脇ラベル): what the writer puts beside a blow's number — the trait's add
// with its condition's word, then the multiplying status, in that order. Runs under `dotnet test`
// (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class HitSideLabelTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static readonly CardDef[] Hand =
        {
            CardCatalog.KesaCut, CardCatalog.Feint, CardCatalog.Brace, CardCatalog.ReachThrust, CardCatalog.BackLeap,
        };

        /// <summary>Plays 袈裟斬り (+5 at 間合い 0) at <paramref name="gap"/> against an enemy holding <paramref name="enemyStatuses"/>.</summary>
        private static List<Cue> PlayKesaCut(int gap, StatusSet enemyStatuses, StatusSet playerStatuses = null)
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(Hand, 1), BattleSetup.SliceFieldCells, StartGap: gap);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(Enemies.PolearmWarped);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            BattleState ready = begin.State.WithEnemy(begin.State.Enemy with { Statuses = enemyStatuses });
            ready = ready with { Player = ready.Player with { Statuses = playerStatuses ?? StatusSet.Empty } };
            writer.Write(begin.Events, ready);
            string id = ready.Hand.First(c => c.Def.Id == CardCatalog.KesaCut.Id).InstanceId;
            StepResult played = TurnLoop.PlayCard(ready, id, NoShuffle);
            return writer.Write(played.Events, played.State).SelectMany(e => e.Cues).ToList();
        }

        private static Cue Wound(List<Cue> cues) =>
            cues.Single(c => (c.Kind == CueKind.Hit || c.Kind == CueKind.Slash) && c.HpAfter != Cue.Unchanged);

        [Test]
        public void ATraitsAdd_IsNamedByItsCondition()
        {
            Assert.That(Wound(PlayKesaCut(0, StatusSet.Empty)).SideLabel, Is.EqualTo("+5 間合い"));
        }

        [Test]
        public void ABlowWithNoTraitAndNoMultiplier_HasNoLabel()
        {
            Assert.That(Wound(PlayKesaCut(1, StatusSet.Empty)).SideLabel, Is.EqualTo(""), "in reach at 間合い 1 but the trait (間合い 0) does not hold");
        }

        [Test]
        public void AFragileTarget_PutsTheMultiplierAfterTheTrait()
        {
            var fragile = StatusSet.Of((StatusKind.Fragile, 1));
            Assert.That(Wound(PlayKesaCut(0, fragile)).SideLabel, Is.EqualTo("+5 間合い ×1.5 脆化"));
            Assert.That(Wound(PlayKesaCut(1, fragile)).SideLabel, Is.EqualTo("×1.5 脆化"));
        }

        [Test]
        public void AnEmpoweredBlow_SaysSo_AndFragileWinsWhenBothApply()
        {
            var empower = StatusSet.Of((StatusKind.Empower, 1));
            Assert.That(Wound(PlayKesaCut(1, StatusSet.Empty, empower)).SideLabel, Is.EqualTo("×1.5 強化"));
            var fragile = StatusSet.Of((StatusKind.Fragile, 1));
            Assert.That(Wound(PlayKesaCut(1, fragile, empower)).SideLabel, Is.EqualTo("×1.5 脆化"), "§19.5 S13: one multiplier a blow, 脆化 first");
        }

        // ---- #951: 背水の陣's ×3 and ×2 in a slot of their own ----

        private static readonly CardDef Ten = new CardDef("ten", "十", BattleAttribute.Attack, 1, new Face(Power: 10, Reach: new Reach(0, 2)));

        /// <summary>Plays 背水の陣, then a power-10 card at 間合い 1 (no trait holds there).</summary>
        private static List<Cue> PlayTenUnderBackwater(StatusSet enemyStatuses)
        {
            var hand = new[] { CardCatalog.Backwater, Ten, CardCatalog.Feint, CardCatalog.Brace, CardCatalog.BackLeap };
            var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(hand, 1), BattleSetup.SliceFieldCells, StartGap: 1);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(Enemies.PolearmWarped);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            BattleState ready = begin.State.WithEnemy(begin.State.Enemy with { Statuses = enemyStatuses });
            ready = ready with { Player = ready.Player with { Stamina = 10 } };
            writer.Write(begin.Events, ready);
            StepResult placed = TurnLoop.PlayCard(ready, ready.Hand.First(c => c.Def.Id == CardCatalog.Backwater.Id).InstanceId, NoShuffle);
            writer.Write(placed.Events, placed.State);
            StepResult played = TurnLoop.PlayCard(placed.State, placed.State.Hand.First(c => c.Def.Id == Ten.Id).InstanceId, NoShuffle);
            return writer.Write(played.Events, played.State).SelectMany(e => e.Cues).ToList();
        }

        [Test]
        public void ABackwaterBlow_SaysTimesThree_BesideTheThirty()
        {
            Cue wound = Wound(PlayTenUnderBackwater(StatusSet.Empty));
            Assert.That(wound.Amount, Is.EqualTo(30), "10 × 3");
            Assert.That(wound.SideLabel, Is.EqualTo("×3 背水の陣"));
        }

        [Test]
        public void ABackwaterBlow_OnAFragileTarget_PutsTheStatusFirst()
        {
            Cue wound = Wound(PlayTenUnderBackwater(StatusSet.Of((StatusKind.Fragile, 1))));
            Assert.That(wound.SideLabel, Is.EqualTo("×1.5 脆化 ×3 背水の陣"));
        }

        [Test]
        public void ABlowTakenUnderBackwater_SaysTimesTwo()
        {
            // 穂先の突き (5, reaches 0〜2) into a player holding 背水の陣: 5 × 2 = 10.
            var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(Hand, 1), BattleSetup.SliceFieldCells, StartGap: 1);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(Enemies.PolearmWarped);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            EnemyActionDef thrust = Enemies.PolearmWarped.Actions["reach_thrust"];
            BattleState ready = begin.State.WithOmen(0, EnemyAi.OmenOf(thrust));
            ready = ready with { Player = ready.Player.WithStance(CardCatalog.Backwater.Face.Stance, CardCatalog.Backwater.Id) };
            writer.Write(begin.Events, ready);
            StepResult end = TurnLoop.EndTurn(ready, NoShuffle);
            List<Cue> cues = writer.Write(end.Events, end.State).SelectMany(e => e.Cues).ToList();

            // An enemy blow plays as swing then wound; the label rides on the wound.
            Cue swing = cues.First(c => c.Kind == CueKind.Slash && c.Target == UnitSide.Player);
            Cue wound = cues.Single(c => c.Kind == CueKind.Hit && c.Target == UnitSide.Player && c.HpAfter != Cue.Unchanged);
            Assert.That(swing.Amount, Is.EqualTo(10), "5 × 2");
            Assert.That(wound.SideLabel, Is.EqualTo("×2 背水の陣"));
        }

        [Test]
        public void TheLabel_LeavesAStanceOfTimesOneOut()
        {
            Assert.That(CoreText.HitLabel(0, null, null, (1, "背水の陣"), null), Is.EqualTo(""));
            Assert.That(CoreText.HitLabel(0, null, StatusKind.Empower, null, (2, "背水の陣")), Is.EqualTo("×1.5 強化 ×2 背水の陣"));
        }

        [Test]
        public void TheLabelDoesNotChangeTheNumber()
        {
            var plain = Wound(PlayKesaCut(0, StatusSet.Empty));
            var fragile = Wound(PlayKesaCut(0, StatusSet.Of((StatusKind.Fragile, 1))));
            Assert.That(fragile.Amount, Is.GreaterThan(plain.Amount), "the number is the core's settled value");
        }
    }
}
