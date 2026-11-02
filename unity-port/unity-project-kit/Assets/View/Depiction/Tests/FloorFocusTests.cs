// Whose N the floor's gap number shows, where Tab moves it, and which omen badge a pointer is on
// (Script/FloorFocus.cs, #762): battle_ui_ux_v2 §2.2 の 3 / §1.4 / §3.4. Runs under `dotnet test`
// (Depiction.Script.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class FloorFocusTests
    {
        private static EnemyFrame At(int cell, int gap, bool down = false)
        {
            return new EnemyFrame { Cell = cell, Gap = gap, Unit = new UnitFrame { Down = down } };
        }

        /// <summary>The player on cell 1; the floor's own number is the nearest standing enemy's, as the writer leaves it.</summary>
        private static DepictionFrame Battle(params EnemyFrame[] enemies)
        {
            EnemyFrame nearest = null;
            foreach (EnemyFrame enemy in enemies)
            {
                if (enemy.Gap >= 0 && (nearest == null || enemy.Cell < nearest.Cell)) nearest = enemy;
            }
            nearest = nearest ?? enemies[0];
            return new DepictionFrame
            {
                Enemies = new List<EnemyFrame>(enemies),
                Enemy = nearest.Unit,
                Omen = nearest.Omen,
                Floor = new FloorFrame { Cells = 8, PlayerCell = 1, PlayerSize = 1, EnemyCell = nearest.Cell, Gap = nearest.Gap },
            };
        }

        [Test]
        public void AtRest_TheNumberIsTheFloorsOwn_TheNearestEnemys()
        {
            DepictionFrame frame = Battle(At(6, 4), At(3, 1));
            Assert.That(FloorFocus.GapUnit(frame, -1, -1), Is.EqualTo(-1));
            GapReading gap = FloorFocus.GapOf(frame, -1);
            Assert.That(gap.Gap, Is.EqualTo(1));
            Assert.That(gap.EnemyCell, Is.EqualTo(3));
        }

        [Test]
        public void AHeldCardOverAnEnemy_ShowsThatEnemysN_AndLettingGoShowsTheNearestAgain()
        {
            DepictionFrame frame = Battle(At(3, 1), At(6, 4));
            Assert.That(FloorFocus.GapUnit(frame, 1, -1), Is.EqualTo(1), "§2.2 の 3: 掴んでいる間は対象の枠で囲んだ相手");
            GapReading over = FloorFocus.GapOf(frame, 1);
            Assert.That(over.Gap, Is.EqualTo(4));
            Assert.That(over.EnemyCell, Is.EqualTo(6));
            Assert.That(over.PlayerCell, Is.EqualTo(1));
            Assert.That(FloorFocus.GapUnit(frame, -1, -1), Is.EqualTo(-1), "離すと戻る");
        }

        [Test]
        public void TheEnemyACardIsOver_WinsOverTheOneTabChose()
        {
            DepictionFrame frame = Battle(At(3, 1), At(5, 3), At(7, 5));
            Assert.That(FloorFocus.GapUnit(frame, 2, 1), Is.EqualTo(2));
            Assert.That(FloorFocus.GapUnit(frame, -1, 1), Is.EqualTo(1), "Tab の相手は札を離すと戻る先");
        }

        [Test]
        public void OneEnemy_AlwaysReadsTheFloor()
        {
            DepictionFrame frame = Battle(At(4, 2));
            Assert.That(FloorFocus.GapUnit(frame, 0, 0), Is.EqualTo(-1));
            Assert.That(FloorFocus.NextTab(frame, -1), Is.EqualTo(-1), "Tab は 2 体以上");
            var listless = new DepictionFrame { Floor = new FloorFrame { Cells = 6, PlayerCell = 2, Gap = 2, EnemyCell = 5 } };
            Assert.That(FloorFocus.GapUnit(listless, 0, 0), Is.EqualTo(-1), "the filming script keeps no list");
            Assert.That(FloorFocus.GapOf(listless, 0).Gap, Is.EqualTo(2));
        }

        [Test]
        public void AFallenEnemy_IsNeitherShownNorTabbedTo()
        {
            DepictionFrame frame = Battle(At(3, 1), At(5, -1), At(7, 5));
            Assert.That(FloorFocus.GapUnit(frame, 1, -1), Is.EqualTo(-1), "taken off the line in the core");
            Assert.That(FloorFocus.GapOf(frame, 1).Gap, Is.EqualTo(1), "the floor's own instead of −1");
            Assert.That(FloorFocus.NextTab(frame, -1), Is.EqualTo(2));
            Assert.That(FloorFocus.NextTab(frame, 2), Is.EqualTo(0));

            DepictionFrame down = Battle(At(3, 1), At(6, 4, down: true));
            Assert.That(FloorFocus.GapUnit(down, -1, 1), Is.EqualTo(-1), "its fall has played");
            Assert.That(FloorFocus.NextTab(down, -1), Is.EqualTo(-1), "one stands: nobody to move to");
        }

        [Test]
        public void Tab_GoesFromNearToFar_AndBackToTheNearest()
        {
            // Enemy 0 stands furthest: the order is the floor's, not the list's.
            DepictionFrame frame = Battle(At(7, 5), At(3, 1), At(5, 3));
            Assert.That(FloorFocus.NextTab(frame, -1), Is.EqualTo(2), "from the nearest (enemy 1) to the next");
            Assert.That(FloorFocus.NextTab(frame, 2), Is.EqualTo(0));
            Assert.That(FloorFocus.NextTab(frame, 0), Is.EqualTo(1));
        }

        [Test]
        public void Tab_FromAnEnemyThatFell_StartsFromTheNearest()
        {
            DepictionFrame frame = Battle(At(3, 1), At(5, -1), At(7, 5));
            Assert.That(FloorFocus.NextTab(frame, 1), Is.EqualTo(2));
        }

        [Test]
        public void ThePointer_FindsTheBoxItIsOn_TheLaterOverTheEarlier()
        {
            var boxes = new List<ScreenBox> { new ScreenBox(100f, 200f, 200f, 98f), new ScreenBox(250f, 200f, 200f, 98f) };
            Assert.That(FloorFocus.BoxAt(boxes, 150f, 250f), Is.EqualTo(0));
            Assert.That(FloorFocus.BoxAt(boxes, 280f, 250f), Is.EqualTo(1), "drawn over the first");
            Assert.That(FloorFocus.BoxAt(boxes, 500f, 250f), Is.EqualTo(-1));
            Assert.That(FloorFocus.BoxAt(boxes, 150f, 310f), Is.EqualTo(-1), "below the badges");
            Assert.That(FloorFocus.BoxAt(new List<ScreenBox>(), 0f, 0f), Is.EqualTo(-1));
        }
    }
}
