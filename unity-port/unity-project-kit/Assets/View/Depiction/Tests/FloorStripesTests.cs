// The stripes on a floor cell and the swatches (Script/FloorStripes.cs, Script/EnemySwatch.cs, #589):
// battle-visual-v1 §4.2 tells enemies' aims apart by direction, crossing where they overlap, and each
// enemy's swatch shows its direction. One enemy reads FloorFrame as the screen always has. Runs under
// `dotnet test` (Depiction.Script.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class FloorStripesTests
    {
        private static EnemyFrame Aiming(int direction, int[] aim, int[] dense = null, int[] move = null, bool visible = true, int disclosure = OmenDisclosure.Full, OmenIcon icon = OmenIcon.Attack)
        {
            return new EnemyFrame
            {
                StripeDirection = direction,
                Swatch = true,
                AimCells = new List<int>(aim),
                DenseCells = new List<int>(dense ?? new int[0]),
                MoveCells = new List<int>(move ?? new int[0]),
                Omen = new OmenFrame { Visible = visible, Disclosure = disclosure, Icon = icon },
            };
        }

        private static DepictionFrame Several(params EnemyFrame[] enemies)
        {
            return new DepictionFrame { Enemies = new List<EnemyFrame>(enemies), Enemy = enemies[0].Unit, Omen = enemies[0].Omen };
        }

        /// <summary>A frame of one enemy as the writer leaves it: the floor and the one share hold the same lists.</summary>
        private static DepictionFrame One(int[] aim, int[] dense = null, int[] move = null, bool visible = true, int disclosure = OmenDisclosure.Full)
        {
            EnemyFrame enemy = Aiming(0, aim, dense, move, visible, disclosure);
            enemy.Swatch = false;
            return new DepictionFrame
            {
                Enemies = new List<EnemyFrame> { enemy },
                Enemy = enemy.Unit,
                Omen = enemy.Omen,
                Floor = new FloorFrame { Cells = 8, AimCells = enemy.AimCells, DenseCells = enemy.DenseCells, MoveCells = enemy.MoveCells },
            };
        }

        [Test]
        public void TwoEnemiesOnOneCell_EachDrawTheirOwnDirection()
        {
            DepictionFrame frame = Several(Aiming(0, new[] { 3, 4 }), Aiming(1, new[] { 4, 5 }));
            Assert.That(FloorStripes.Of(frame, 3), Is.EqualTo(new[] { new StripeMark(0, false) }));
            Assert.That(FloorStripes.Of(frame, 4), Is.EqualTo(new[] { new StripeMark(0, false), new StripeMark(1, false) }), "§4.2 重なったマスは 2 つの向きが交差");
            Assert.That(FloorStripes.Of(frame, 5), Is.EqualTo(new[] { new StripeMark(1, false) }));
            Assert.That(FloorStripes.Of(frame, 6), Is.Empty);
        }

        [Test]
        public void ThreeEnemiesOnOneCell_DrawAllThreeDirections()
        {
            DepictionFrame frame = Several(Aiming(0, new[] { 4 }), Aiming(1, new[] { 4 }), Aiming(2, new[] { 4 }));
            Assert.That(FloorStripes.Of(frame, 4), Is.EqualTo(new[] { new StripeMark(0, false), new StripeMark(1, false), new StripeMark(2, false) }));
        }

        [Test]
        public void OneEnemy_DrawsDirectionZeroOnly_FromTheFloor()
        {
            DepictionFrame frame = One(new[] { 2, 3 }, dense: new[] { 3 });
            Assert.That(FloorStripes.Of(frame, 2), Is.EqualTo(new[] { new StripeMark(0, false) }));
            Assert.That(FloorStripes.Of(frame, 3), Is.EqualTo(new[] { new StripeMark(0, true) }));
            Assert.That(FloorStripes.Of(frame, 4), Is.Empty);
        }

        [Test]
        public void ASourceWithNoList_ReadsTheFloorToo()
        {
            var frame = new DepictionFrame { Omen = new OmenFrame { Visible = true }, Floor = new FloorFrame { Cells = 6, AimCells = new List<int> { 2 }, MoveCells = new List<int> { 5 } } };
            Assert.That(FloorStripes.Of(frame, 2), Is.EqualTo(new[] { new StripeMark(0, false) }));
            Assert.That(FloorStripes.MoveTo(frame, 5), Is.True);
        }

        [Test]
        public void Dense_IsPerEnemy()
        {
            DepictionFrame frame = Several(Aiming(0, new[] { 4 }, dense: new[] { 4 }), Aiming(1, new[] { 4 }));
            Assert.That(FloorStripes.Of(frame, 4), Is.EqualTo(new[] { new StripeMark(0, true), new StripeMark(1, false) }));
        }

        [Test]
        public void AHiddenOmen_DrawsNone()
        {
            DepictionFrame several = Several(Aiming(0, new[] { 4 }, visible: false), Aiming(1, new[] { 4 }));
            Assert.That(FloorStripes.Of(several, 4), Is.EqualTo(new[] { new StripeMark(1, false) }), "the other's stays");
            Assert.That(FloorStripes.Of(One(new[] { 4 }, visible: false), 4), Is.Empty);
        }

        [Test]
        public void AnEnemyReadToZero_DrawsNoCells_WhenThereAreSeveral()
        {
            DepictionFrame frame = Several(Aiming(0, new[] { 4 }, move: new[] { 6 }, disclosure: OmenDisclosure.Unread), Aiming(1, new[] { 5 }, disclosure: OmenDisclosure.Partial));
            Assert.That(FloorStripes.Of(frame, 4), Is.Empty, "battle_ui_ux_v2 §7: 開示度 0 is the kind alone");
            Assert.That(FloorStripes.MoveTo(frame, 6), Is.False);
            Assert.That(FloorStripes.Of(frame, 5), Is.EqualTo(new[] { new StripeMark(1, false) }), "from 開示度 1");
        }

        [Test]
        public void OneEnemyReadToZero_DrawsNoCells_AndFromOneItDoes()
        {
            DepictionFrame unread = One(new[] { 3, 4 }, dense: new[] { 4 }, move: new[] { 6 }, disclosure: OmenDisclosure.Unread);
            for (int cell = 1; cell <= unread.Floor.Cells; cell++)
            {
                Assert.That(FloorStripes.Of(unread, cell), Is.Empty, "battle_ui_ux_v2 §7: 開示度 0 is the kind alone, cell " + cell);
                Assert.That(FloorStripes.MoveTo(unread, cell), Is.False, "cell " + cell);
            }
            DepictionFrame partial = One(new[] { 3, 4 }, dense: new[] { 4 }, move: new[] { 6 }, disclosure: OmenDisclosure.Partial);
            Assert.That(FloorStripes.Of(partial, 3), Is.EqualTo(new[] { new StripeMark(0, false) }), "§5.8 提示: from 開示度 1");
            Assert.That(FloorStripes.Of(partial, 4), Is.EqualTo(new[] { new StripeMark(0, true) }));
            Assert.That(FloorStripes.MoveTo(partial, 6), Is.True);
        }

        [Test]
        public void MoveFrames_ArePerEnemy()
        {
            DepictionFrame frame = Several(Aiming(0, new int[0], move: new[] { 3 }), Aiming(1, new int[0], move: new[] { 6 }));
            Assert.That(FloorStripes.MoveTo(frame, 3), Is.True);
            Assert.That(FloorStripes.MoveTo(frame, 6), Is.True);
            Assert.That(FloorStripes.MoveTo(frame, 4), Is.False);
            Assert.That(FloorStripes.Of(frame, 3), Is.Empty, "a move is never stripes");
        }

        [Test]
        public void AMoveOntoACellAnotherAimsAt_StandsInsideTheRedFrame()
        {
            DepictionFrame frame = Several(Aiming(0, new[] { 4 }), Aiming(1, new int[0], move: new[] { 4, 5 }));
            Assert.That(FloorStripes.MoveInset(frame, 4), Is.EqualTo(FloorStripes.MoveInsetOverAim));
            Assert.That(FloorStripes.MoveInset(frame, 5), Is.EqualTo(0f), "alone on the cell it keeps the cell's edge");
            Assert.That(FloorStripes.MoveInset(One(new[] { 4 }, move: new[] { 4 }), 4), Is.EqualTo(0f), "one enemy draws as it always has");
        }

        // ---- the swatches -----------------------------------------------------------------------

        [Test]
        public void TheSizes_AreTheDesigns()
        {
            Assert.That(EnemySwatch.BadgeSize, Is.EqualTo(22f), "battle-visual-v1 §4.4");
            Assert.That(EnemySwatch.PanelSize, Is.EqualTo(16f), "battle-visual-v1 §4.5");
            Assert.That(StanceList.SwatchSize, Is.EqualTo(16f));
        }

        [Test]
        public void SeveralEnemies_ShowTheSwatch_OnTheBadgeOfAnAimingOmen()
        {
            DepictionFrame frame = Several(Aiming(0, new[] { 4 }), Aiming(1, new int[0], move: new[] { 6 }, icon: OmenIcon.Move));
            Assert.That(EnemySwatch.OnBadge(frame, 0), Is.True);
            Assert.That(EnemySwatch.OnBadge(frame, 1), Is.False, "a move stripes nothing (§2.3)");
            Assert.That(EnemySwatch.DirectionOf(frame, 1), Is.EqualTo(1));
        }

        [Test]
        public void TheBadgesSwatch_HoldsWithTheHitMark_ButNotOnARest_NorAnUnreadEnemy_NorAHiddenOmen()
        {
            EnemyFrame marked = Aiming(0, new int[0]);
            marked.Omen.Hit = OmenHit.Misses;
            Assert.That(EnemySwatch.OnBadge(Several(marked, Aiming(1, new int[0])), 0), Is.True);
            Assert.That(EnemySwatch.OnBadge(Several(Aiming(0, new[] { 4 }, icon: OmenIcon.Rest), Aiming(1, new int[0])), 0), Is.False);
            Assert.That(EnemySwatch.OnBadge(Several(Aiming(0, new[] { 4 }, disclosure: OmenDisclosure.Unread), Aiming(1, new int[0])), 0), Is.False);
            Assert.That(EnemySwatch.OnBadge(Several(Aiming(0, new[] { 4 }, visible: false), Aiming(1, new int[0])), 0), Is.False);
        }

        [Test]
        public void AFrameWhoseEnemiesCarryNoSwatch_ShowsNone()
        {
            EnemyFrame first = Aiming(0, new[] { 4 }), second = Aiming(1, new[] { 5 });
            first.Swatch = second.Swatch = false;
            DepictionFrame frame = Several(first, second);
            Assert.That(EnemySwatch.OnBadge(frame, 0), Is.False);
            Assert.That(EnemySwatch.OnPanel(frame, 0), Is.False);
        }

        [Test]
        public void TheHpRowShowsIt_OnlyInABattleOfSeveral()
        {
            Assert.That(EnemySwatch.OnPanel(Several(Aiming(0, new int[0]), Aiming(1, new int[0])), 1), Is.True);
            Assert.That(EnemySwatch.OnPanel(One(new[] { 4 }), 0), Is.False);
            Assert.That(EnemySwatch.OnPanel(new DepictionFrame(), 0), Is.False, "a source with no list");
        }

        [Test]
        public void OneEnemysBadge_KeepsItsSwatch_WithItsHitMark_AsBefore()
        {
            DepictionFrame frame = One(new[] { 4 });
            Assert.That(EnemySwatch.OnBadge(frame, 0), Is.False, "no mark, no swatch");
            frame.Omen.Hit = OmenHit.Lands;
            Assert.That(EnemySwatch.OnBadge(frame, 0), Is.True);
            Assert.That(EnemySwatch.DirectionOf(frame, 0), Is.EqualTo(0));
            var listless = new DepictionFrame { Omen = new OmenFrame { Hit = OmenHit.Misses } };
            Assert.That(EnemySwatch.OnBadge(listless, 0), Is.True, "the filming script's too");
        }
    }
}
