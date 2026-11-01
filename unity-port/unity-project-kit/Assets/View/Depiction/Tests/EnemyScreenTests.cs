// Which enemy each part of the screen belongs to (Script/EnemyScreen.cs, #589). The View acts on these
// answers and is not reached by `dotnet test`, so these hold what it will do: a cue goes to the enemy it
// names, a move walks the enemy that moved, the nearer figure is drawn in front, and a held card frames
// each enemy by the script's own answer. One enemy takes the path it always took.
using System.Collections.Generic;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class EnemyScreenTests
    {
        private static EnemyFrame Enemy(int cell, int size = 1, bool down = false, int gap = 1)
        {
            return new EnemyFrame { Cell = cell, Size = size, Gap = gap, Unit = new UnitFrame { Down = down }, Omen = new OmenFrame() };
        }

        private static DepictionFrame TwoEnemies(EnemyFrame first, EnemyFrame second)
        {
            return new DepictionFrame { Enemies = new List<EnemyFrame> { first, second }, Enemy = first.Unit, Omen = first.Omen };
        }

        /// <summary>The filming script's frame: no list, the one enemy in the single-valued members.</summary>
        private static DepictionFrame OneEnemyWithNoList()
        {
            return new DepictionFrame
            {
                Enemy = new UnitFrame { ArtId = "only" },
                Omen = new OmenFrame { KindLabel = "攻撃" },
                Floor = new FloorFrame { EnemyCell = 5, EnemySize = 0 },
            };
        }

        [Test]
        public void ASourceWithNoList_HasOneEnemy_ReadFromTheSingleMembers()
        {
            DepictionFrame frame = OneEnemyWithNoList();
            Assert.That(EnemyScreen.CountOf(frame), Is.EqualTo(1));
            Assert.That(EnemyScreen.UnitOf(frame, 0), Is.SameAs(frame.Enemy));
            Assert.That(EnemyScreen.OmenOf(frame, 0), Is.SameAs(frame.Omen));
            Assert.That(EnemyScreen.CellOf(frame, 0), Is.EqualTo(5));
            Assert.That(EnemyScreen.SizeOf(frame, 0), Is.EqualTo(1), "at least one cell");
            Assert.That(EnemyScreen.FocusOf(frame), Is.EqualTo(0));
            Assert.That(EnemyScreen.Stands(frame, 0), Is.True);
            Assert.That(EnemyScreen.OthersStand(frame, 0), Is.False);
        }

        [Test]
        public void EachEnemy_ReadsItsOwnShare()
        {
            EnemyFrame first = Enemy(4), second = Enemy(6, size: 2);
            DepictionFrame frame = TwoEnemies(first, second);
            Assert.That(EnemyScreen.CountOf(frame), Is.EqualTo(2));
            Assert.That(EnemyScreen.UnitOf(frame, 1), Is.SameAs(second.Unit));
            Assert.That(EnemyScreen.OmenOf(frame, 1), Is.SameAs(second.Omen));
            Assert.That(EnemyScreen.CellOf(frame, 1), Is.EqualTo(6));
            Assert.That(EnemyScreen.SizeOf(frame, 1), Is.EqualTo(2));
        }

        [Test]
        public void TheFocus_IsTheEnemyTheSingleMembersDescribe()
        {
            EnemyFrame first = Enemy(6), second = Enemy(4);
            DepictionFrame frame = TwoEnemies(first, second);
            frame.Enemy = second.Unit;
            Assert.That(EnemyScreen.FocusOf(frame), Is.EqualTo(1));
        }

        [TestCase(0, 2, 0)]
        [TestCase(1, 2, 1)]
        [TestCase(2, 3, 2)]
        [TestCase(1, 1, 0)] // one enemy: whatever the cue says, the only figure
        [TestCase(5, 2, 0)] // an enemy the screen does not draw: the first, not a crash
        [TestCase(-1, 2, 0)]
        public void ACue_GoesToTheEnemyItNames(int unit, int count, int expected)
        {
            Assert.That(EnemyScreen.IndexOf(new Cue { Unit = unit }, count), Is.EqualTo(expected));
        }

        [Test]
        public void WithOneEnemy_TheMoveReadsEnemyCellAfter_AsItAlwaysDid()
        {
            var moved = new Cue { Kind = CueKind.RangeSwitch, EnemyCellAfter = 4, UnitCellAfter = 0 };
            Assert.That(EnemyScreen.MovedUnit(moved, 1, out int cell), Is.EqualTo(0));
            Assert.That(cell, Is.EqualTo(4));
            Assert.That(EnemyScreen.MovedUnit(new Cue { EnemyCellAfter = 0 }, 1, out _), Is.EqualTo(-1), "a source that draws no enemy cell");
        }

        [Test]
        public void WithSeveral_OnlyUnitCellAfter_NamesAMove()
        {
            // The player walked: EnemyCellAfter is the focus enemy's cell, and nobody else moved.
            var playerWalked = new Cue { Kind = CueKind.RangeSwitch, EnemyCellAfter = 4, UnitCellAfter = 0, Unit = 0 };
            Assert.That(EnemyScreen.MovedUnit(playerWalked, 2, out _), Is.EqualTo(-1));
            // The second enemy stepped; EnemyCellAfter still names the focus enemy.
            var secondStepped = new Cue { Kind = CueKind.RangeSwitch, EnemyCellAfter = 4, UnitCellAfter = 6, Unit = 1 };
            Assert.That(EnemyScreen.MovedUnit(secondStepped, 2, out int cell), Is.EqualTo(1));
            Assert.That(cell, Is.EqualTo(6));
        }

        [Test]
        public void TheFigureNearerThePlayer_IsDrawnLast_SoInFront()
        {
            Assert.That(EnemyScreen.BackToFront(TwoEnemies(Enemy(4), Enemy(6))), Is.EqualTo(new[] { 1, 0 }));
            Assert.That(EnemyScreen.BackToFront(TwoEnemies(Enemy(6), Enemy(4))), Is.EqualTo(new[] { 0, 1 }), "the order follows the cells, not the list");
            Assert.That(EnemyScreen.BackToFront(OneEnemyWithNoList()), Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void AFallenEnemy_NoLongerStands_NorOneTheCoreHasTakenOffTheLine()
        {
            DepictionFrame frame = TwoEnemies(Enemy(4, down: true, gap: -1), Enemy(6));
            Assert.That(EnemyScreen.Stands(frame, 0), Is.False, "its fall has played");
            Assert.That(EnemyScreen.Stands(frame, 1), Is.True);
            Assert.That(EnemyScreen.OthersStand(frame, 0), Is.True, "the second stands: this fall is not the last");
            Assert.That(EnemyScreen.OthersStand(frame, 1), Is.False);
            DepictionFrame stillToFall = TwoEnemies(Enemy(4, gap: -1), Enemy(6, gap: -1));
            Assert.That(EnemyScreen.OthersStand(stillToFall, 0), Is.False, "both fell in one move: neither stands for the other");
        }

        [Test]
        public void TheFramesReach_IsTheScriptsPerEnemy_WithSeveral()
        {
            var face = new CardFace { ReachOn = new List<bool> { false, true }, ReachHint = "" };
            Assert.That(EnemyScreen.Reaches(face, 0, 2), Is.False, "out of reach of the first");
            Assert.That(EnemyScreen.Reaches(face, 1, 2), Is.True);
            Assert.That(EnemyScreen.Reaches(face, 2, 2), Is.False, "no answer written is no reach");
        }

        [Test]
        public void TheFramesReach_IsTheHoverPanels_WithOne()
        {
            Assert.That(EnemyScreen.Reaches(new CardFace { ReachHint = "" }, 0, 1), Is.True);
            Assert.That(EnemyScreen.Reaches(new CardFace { ReachHint = "相手との間合いが 1〜2 のとき使用可能" }, 0, 1), Is.False);
        }

        [TestCase(1, 0, 1)] // over the second, whatever it would go to
        [TestCase(-1, 1, 1)] // over nobody: the one it would go to unnamed
        [TestCase(-1, -1, 0)] // a card that names none: the first, as one enemy always read
        public void AHeldCard_IsReadOnTheEnemyItFrames(int picked, int targetUnit, int expected)
        {
            Assert.That(EnemyScreen.FramedUnit(new CardFace { TargetUnit = targetUnit }, picked), Is.EqualTo(expected));
        }
    }
}
