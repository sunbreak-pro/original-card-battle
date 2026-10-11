// #455: what the writer puts on the script for battle-visual-v1 §4.2 and §4.7 — the aimed cells
// that meet the boundary the omen's action reads (dense stripes), the cells a moving omen takes the
// enemy to (the violet dashed frame), and the stamina the player gets back at the next turn start
// (the dashed dots). The values are pinned for concrete enemies on the slice's 6-cell line, with the
// player on cell 2. Runs under `dotnet test` (Depiction.Bridge.Tests.csproj) and in Unity's EditMode
// runner from the same file.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class FloorReadoutTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static readonly CardDef[] Hand =
        {
            CardCatalog.KesaCut, CardCatalog.Feint, CardCatalog.Brace, CardCatalog.ReachThrust, CardCatalog.BackLeap,
        };

        /// <summary>Turn 1 begun against <paramref name="enemy"/> at <paramref name="gap"/>, and its last frame.</summary>
        private static (BattleState State, CoreScriptWriter Writer, DepictionFrame Frame) Begun(
            EnemyDef enemy, int gap, IReadOnlyList<CardInstance> deck = null)
        {
            var setup = new BattleSetup(enemy, deck ?? Cards.BuildDeck(Hand, 1), BattleSetup.SliceFieldCells, StartGap: gap);
            BattleState start = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(enemy);
            writer.Opening(start);
            StepResult begin = TurnLoop.BeginPlayerTurn(start, NoShuffle);
            DepictionFrame frame = writer.Write(begin.Events, begin.State).Last().After;
            return (begin.State, writer, frame);
        }

        // ---- §4.2 境目を満たすマス -------------------------------------------------------------------

        [Test]
        public void ThePolearmsSweep_AtGapTwo_ThickensTheCellTwoAway()
        {
            // 錆槍の竜兵 on cell 5 shows 払い斬り (1〜2, 間合い 2 以上: 威力 +3): cells 2 and 3 are aimed
            // at, and cell 2 — gap 2, where the player stands — meets the boundary.
            var (state, _, frame) = Begun(Enemies.PolearmWarped, 2);
            Assert.That(state.Omen.ActionId, Is.EqualTo("sweep"));
            Assert.That(frame.Floor.AimCells, Is.EqualTo(new[] { 2, 3 }));
            Assert.That(frame.Floor.DenseCells, Is.EqualTo(new[] { 2 }));
            Assert.That(frame.Floor.MoveCells, Is.Empty, "払い斬り does not move it");
        }

        [Test]
        public void ThePolearmsSweep_AtGapOne_ThickensTheCellBehindThePlayer()
        {
            // On cell 4: aimed 1 and 2, gap 2 is cell 1 — a step back would take the +3.
            var (_, _, frame) = Begun(Enemies.PolearmWarped, 1);
            Assert.That(frame.Floor.AimCells, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(frame.Floor.DenseCells, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void AnOmenThatReadsNoGap_ThickensNothing()
        {
            // Gap 0: 柄尻の押し込み reads 無防備.
            var (state, _, frame) = Begun(Enemies.PolearmWarped, 0);
            Assert.That(state.Omen.ActionId, Is.EqualTo("shove"));
            Assert.That(frame.Floor.AimCells, Is.EqualTo(new[] { 2 }));
            Assert.That(frame.Floor.DenseCells, Is.Empty);
        }

        [Test]
        public void TheHiddenOmen_ThickensAndFramesNothing()
        {
            var setup = new BattleSetup(Enemies.ShadowHound, Cards.BuildDeck(Hand, 1), BattleSetup.SliceFieldCells, StartGap: 2);
            BattleState start = TurnLoop.Start(setup, NoShuffle).State;
            DepictionFrame opening = new CoreScriptWriter(setup.Enemy).Opening(start);
            Assert.That(opening.Floor.DenseCells, Is.Empty);
            Assert.That(opening.Floor.MoveCells, Is.Empty);
        }

        // ---- §4.2 敵が動く予兆の移る先 -------------------------------------------------------------------

        [Test]
        public void ThePolearmsStep_FramesTheCellItStepsInto()
        {
            // Gap 3: on cell 6 it shows 踏み込み (前へ 1). It aims at nobody, so the frame is all there is.
            var (state, _, frame) = Begun(Enemies.PolearmWarped, 3);
            Assert.That(state.Omen.ActionId, Is.EqualTo("step_forward"));
            Assert.That(frame.Floor.MoveCells, Is.EqualTo(new[] { 5 }));
            Assert.That(frame.Floor.AimCells, Is.Empty);
            Assert.That(frame.Floor.DenseCells, Is.Empty);
        }

        [Test]
        public void TheHoundsLunge_AimsThickensAndFrames()
        {
            // 瘴牙の走竜 on cell 5 shows 跳びかかり (1〜2, 間合い 2 以上: 威力 +3, then 前へ 2): it aims at
            // 2 and 3, thickens 2, and lands on 3, next to the player.
            var (state, _, frame) = Begun(Enemies.ShadowHound, 2);
            Assert.That(state.Omen.ActionId, Is.EqualTo("lunge_in"));
            Assert.That(frame.Floor.AimCells, Is.EqualTo(new[] { 2, 3 }));
            Assert.That(frame.Floor.DenseCells, Is.EqualTo(new[] { 2 }));
            Assert.That(frame.Floor.MoveCells, Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void TheTurnEndsFrame_ReadsTheOmenWhereTheEnemyStillStands()
        {
            // The turn end is written against the state the whole EndTurn left, after the hound has
            // lunged to cell 3 and chosen 噛みつき. Its own frame still shows 跳びかかり from cell 5.
            var (state, writer, _) = Begun(Enemies.ShadowHound, 2);
            StepResult end = TurnLoop.EndTurn(state, NoShuffle);
            List<DepictionEvent> written = writer.Write(end.Events, end.State);

            DepictionFrame turnEnd = written.First(e => e.Kind == DepictionEventKind.TurnEnd).After;
            Assert.That(turnEnd.Floor.EnemyCell, Is.EqualTo(5));
            Assert.That(turnEnd.Floor.DenseCells, Is.EqualTo(new[] { 2 }));
            Assert.That(turnEnd.Floor.MoveCells, Is.EqualTo(new[] { 3 }));

            DepictionFrame next = written.Last().After;
            Assert.That(end.State.Enemy.Cell, Is.EqualTo(3));
            Assert.That(next.Floor.EnemyCell, Is.EqualTo(3));
            Assert.That(next.Floor.MoveCells, Is.Empty, "噛みつき does not move it");
            Assert.That(next.Floor.DenseCells, Is.Empty, "噛みつき reads no gap");
        }
    }
}
