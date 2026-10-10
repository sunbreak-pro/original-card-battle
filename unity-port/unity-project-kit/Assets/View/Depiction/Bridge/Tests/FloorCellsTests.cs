// #242: the floor of battle-visual-v1 §4.2 as the script carries it — the cells a card reaches, the
// cells the shown omen aims at, the hit mark of §4.4, and where both figures stand after a move.
// Runs under `dotnet test` (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner from the
// same file.
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class FloorCellsTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static readonly CardDef[] Hand =
        {
            CardCatalog.KesaCut, CardCatalog.Feint, CardCatalog.Brace, CardCatalog.ReachThrust, CardCatalog.BackLeap,
        };

        /// <summary>The polearm on the slice's line at the given gap, the writer past the opening.</summary>
        private static (BattleState State, CoreScriptWriter Writer) Begin(int gap)
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(Hand, 1), BattleSetup.SliceFieldCells, StartGap: gap);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(setup.Enemy);
            writer.Opening(state);
            return (state, writer);
        }

        // ---- §4.2 届くマスの光 -------------------------------------------------------------------------

        [Test]
        public void ACardAimedAtTheEnemy_ReachesTheCellsItsReachCountsFromThePlayersFront()
        {
            // The player on cell 2: gap 0 is cell 3, gap 1 is cell 4, gap 2 is cell 5.
            Assert.That(FloorCells.ReachOf(CardCatalog.KesaCut, 2, 1, 6), Is.EqualTo(new[] { 3, 4 }), "0〜1");
            Assert.That(FloorCells.ReachOf(CardCatalog.ReachThrust, 2, 1, 6), Is.EqualTo(new[] { 4, 5 }), "1〜2");
        }

        [Test]
        public void TheReach_StopsAtTheEndOfTheLine()
        {
            Assert.That(FloorCells.ReachOf(CardCatalog.ReachThrust, 4, 1, 6), Is.EqualTo(new[] { 6 }));
            Assert.That(FloorCells.ReachOf(CardCatalog.ReachThrust, 5, 1, 6), Is.Empty);
        }

        [Test]
        public void ACardAimedAtNobody_LightsNoCell()
        {
            Assert.That(FloorCells.ReachOf(CardCatalog.Brace, 2, 1, 6), Is.Empty);
            Assert.That(FloorCells.ReachOf(CardCatalog.BackLeap, 2, 1, 6), Is.Empty);
        }

        // ---- §4.2 狙うマス / §4.4 当たり外れの印 ---------------------------------------------------------

        [Test]
        public void TheAimedCells_AreTheCoresTargetCells_FromWhereTheEnemyStands()
        {
            BattleState state = Begin(3).State;
            var omen = new Omen("thrust", new OmenLabel(OmenKind.Attack, new Reach(0, 1)));

            // The enemy on cell 6: gap 0 is cell 5, gap 1 is cell 4, lowest first.
            Assert.That(FloorCells.AimOf(omen, state.Enemy, 6), Is.EqualTo(new[] { 4, 5 }));
            Assert.That(FloorCells.AimOf(omen, state.Enemy, 6),
                Is.EqualTo(EnemyAi.TargetCells(omen.Label, state.Enemy with { Cell = 6 })));
            Assert.That(FloorCells.AimOf(new Omen("crouch", new OmenLabel(OmenKind.Guard)), state.Enemy, 6), Is.Empty);
        }

        [Test]
        public void TheHitMark_SaysWhetherThePlayerStandsInTheAimedCells()
        {
            var omen = new Omen("thrust", new OmenLabel(OmenKind.Attack, new Reach(0, 1)));
            int[] aim = { 4, 5 };

            Assert.That(FloorCells.HitOf(omen, aim, 4, 1), Is.EqualTo(OmenHit.Lands));
            Assert.That(FloorCells.HitOf(omen, aim, 2, 1), Is.EqualTo(OmenHit.Misses));
            Assert.That(FloorCells.HitOf(omen, aim, 3, 2), Is.EqualTo(OmenHit.Lands), "a body on two cells is hit on either");
            Assert.That(FloorCells.HitOf(new Omen("crouch", new OmenLabel(OmenKind.Guard)), aim, 4, 1), Is.EqualTo(OmenHit.None),
                "an action that aims at nobody has no mark");
        }

        // ---- the writer ---------------------------------------------------------------------------

        [Test]
        public void TheWritersFrame_CarriesTheFloorAsTheCoreStands()
        {
            var (state, writer) = Begin(1);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            DepictionFrame frame = writer.Write(begin.Events, begin.State).Last().After;
            BattleState now = begin.State;

            Assert.That(frame.Floor.Cells, Is.EqualTo(now.FieldCells));
            Assert.That(frame.Floor.PlayerCell, Is.EqualTo(now.Player.Cell));
            Assert.That(frame.Floor.EnemyCell, Is.EqualTo(now.Enemy.Cell));
            Assert.That(frame.Floor.EnemySize, Is.EqualTo(now.Enemy.Size));
            Assert.That(frame.Floor.Gap, Is.EqualTo(now.GapTo(0)));
            Assert.That(frame.Omen.Visible, Is.True, "step 5 shows the first omen");
            Assert.That(frame.Floor.AimCells, Is.EqualTo(EnemyAi.TargetCells(now.Omen.Label, now.Enemy)));
            OmenHit expected = now.Omen.Label.Reach == null ? OmenHit.None
                : frame.Floor.AimCells.Contains(now.Player.Cell) ? OmenHit.Lands : OmenHit.Misses;
            Assert.That(frame.Omen.Hit, Is.EqualTo(expected));
        }

        [Test]
        public void TheOpening_AimsAtNoCell_WhileTheOmenIsHidden()
        {
            var (state, writer) = Begin(1);
            DepictionFrame frame = writer.Opening(state);

            Assert.That(frame.Floor.Cells, Is.EqualTo(BattleSetup.SliceFieldCells));
            Assert.That(frame.Floor.AimCells, Is.Empty);
            Assert.That(frame.Omen.Hit, Is.EqualTo(OmenHit.None));
        }

        [Test]
        public void EachCardOfTheHand_CarriesTheCellsItReaches()
        {
            var (state, writer) = Begin(1);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            DepictionFrame frame = writer.Write(begin.Events, begin.State).Last().After;
            int player = begin.State.Player.Cell;

            Assert.That(frame.Hand.First(c => c.Name == "袈裟斬り").ReachCells, Is.EqualTo(new[] { player + 1, player + 2 }));
            Assert.That(frame.Hand.First(c => c.Name == "伸び突き").ReachCells, Is.EqualTo(new[] { player + 2, player + 3 }));
            Assert.That(frame.Hand.First(c => c.Name == "呼吸を整える").ReachCells, Is.Empty);
        }

        [Test]
        public void AMove_CarriesWhereBothStandAfterIt()
        {
            var (state, writer) = Begin(0);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);

            string feint = begin.State.Hand.First(c => c.Def.Id == "feint").InstanceId;
            StepResult play = TurnLoop.PlayCard(begin.State, feint, NoShuffle);
            DepictionEvent ev = writer.Write(play.Events, play.State)[0];
            Cue move = ev.Cues.Last(c => c.Kind == CueKind.RangeSwitch);

            Assert.That(move.PlayerCellAfter, Is.EqualTo(play.State.Player.Cell));
            Assert.That(move.EnemyCellAfter, Is.EqualTo(play.State.Enemy.Cell));
            Assert.That(move.GapAfter, Is.EqualTo(play.State.GapTo(0)));
            Assert.That(ev.After.Floor.Gap, Is.EqualTo(move.GapAfter));
        }
    }
}
