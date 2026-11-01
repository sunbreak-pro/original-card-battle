// The enemy side's stance list with several enemies (Script/EnemyStanceRows.cs, #589): battle-visual-v1
// §4.6 gives the side one place, so every standing enemy's stances share it, each in its own frame, the
// one put down last in the slot. A fallen enemy's go with it.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class EnemyStanceRowsTests
    {
        private static EnemyFrame Holding(bool down, params (string Name, int Count)[] rows)
        {
            var unit = new UnitFrame { Down = down };
            foreach ((string name, int count) in rows) unit.Stances.Add(new StanceRow { Name = name, Count = count });
            return new EnemyFrame { Unit = unit };
        }

        private static List<string> Names(List<StanceRow> rows) => rows.Select(r => r.Name + "×" + r.Count).ToList();

        [Test]
        public void EachRow_KnowsTheSwatchOfTheEnemyHoldingIt()
        {
            EnemyFrame first = Holding(false, ("岩の構え", 1));
            EnemyFrame second = Holding(false, ("岩の構え", 1), ("水の構え", 2));
            first.Swatch = second.Swatch = true;
            first.StripeDirection = 0;
            second.StripeDirection = 1;
            var rows = new EnemyStanceRows();
            List<EnemyStanceRow> owned = rows.Owned(new[] { first, second });
            Assert.That(owned.Select(r => r.Row.Name + ":" + r.StripeDirection), Is.EqualTo(new[] { "岩の構え:0", "岩の構え:1", "水の構え:1" }));
            Assert.That(owned.All(r => r.Swatch), Is.True);
        }

        [Test]
        public void Of_AndOwned_ListTheSameRows_WithNoNewStamps()
        {
            EnemyFrame first = Holding(false, ("岩の構え", 1));
            EnemyFrame second = Holding(false, ("水の構え", 1));
            var rows = new EnemyStanceRows();
            List<StanceRow> listed = rows.Of(new[] { first, second });
            Assert.That(rows.Owned(new[] { first, second }).Select(r => r.Row), Is.EqualTo(listed));
        }

        [Test]
        public void EachEnemysStance_HasItsOwnFrame_AlsoWhenTheNameIsTheSame()
        {
            var rows = new EnemyStanceRows();
            List<StanceRow> list = rows.Of(new[] { Holding(false, ("岩の構え", 1)), Holding(false, ("岩の構え", 1)) });
            Assert.That(Names(list), Is.EqualTo(new[] { "岩の構え×1", "岩の構え×1" }));
        }

        [Test]
        public void TheOnePutDownLast_IsLast_WhicheverEnemyHoldsIt()
        {
            var rows = new EnemyStanceRows();
            rows.Of(new[] { Holding(false), Holding(false, ("風", 1)) });
            List<StanceRow> list = rows.Of(new[] { Holding(false, ("岩", 1)), Holding(false, ("風", 1)) });
            Assert.That(Names(list), Is.EqualTo(new[] { "風×1", "岩×1" }), "the first enemy's came later");
        }

        [Test]
        public void AStancePutDownAgain_GoesToTheNewestPlace_WithItsCount()
        {
            var rows = new EnemyStanceRows();
            rows.Of(new[] { Holding(false, ("岩", 1)), Holding(false, ("風", 1)) });
            List<StanceRow> list = rows.Of(new[] { Holding(false, ("岩", 2)), Holding(false, ("風", 1)) });
            Assert.That(Names(list), Is.EqualTo(new[] { "風×1", "岩×2" }));
        }

        [Test]
        public void OneEnemysOwnOrder_IsKept_WhenItComesInAtOnce()
        {
            var rows = new EnemyStanceRows();
            List<StanceRow> list = rows.Of(new[] { Holding(false, ("岩", 1), ("風", 1)), Holding(false) });
            Assert.That(Names(list), Is.EqualTo(new[] { "岩×1", "風×1" }));
        }

        [Test]
        public void AFallenEnemysStances_GoWithIt()
        {
            var rows = new EnemyStanceRows();
            rows.Of(new[] { Holding(false, ("岩", 1)), Holding(false, ("風", 1)) });
            List<StanceRow> list = rows.Of(new[] { Holding(true, ("岩", 1)), Holding(false, ("風", 1)) });
            Assert.That(Names(list), Is.EqualTo(new[] { "風×1" }));
        }

        [Test]
        public void OnceNoneStands_TheListStaysAsTheyStood()
        {
            var rows = new EnemyStanceRows();
            rows.Of(new[] { Holding(false, ("岩", 1)), Holding(false, ("風", 1)) });
            List<StanceRow> list = rows.Of(new[] { Holding(true, ("岩", 1)), Holding(true, ("風", 1)) });
            Assert.That(Names(list), Is.EqualTo(new[] { "岩×1", "風×1" }));
        }

        [Test]
        public void AFreshBattle_StartsTheOrderOver()
        {
            var rows = new EnemyStanceRows();
            rows.Of(new[] { Holding(false, ("岩", 1)), Holding(false) });
            rows.Reset();
            List<StanceRow> list = rows.Of(new[] { Holding(false, ("岩", 1)), Holding(false, ("風", 1)) });
            Assert.That(Names(list), Is.EqualTo(new[] { "岩×1", "風×1" }), "in the core's order, as nothing was put down before");
        }

        [Test]
        public void TheMergedList_FoldsAsOneSideDoes()
        {
            var rows = new EnemyStanceRows();
            List<StanceRow> list = rows.Of(new[] { Holding(false, ("甲", 1), ("乙", 1)), Holding(false, ("丙", 1), ("丁", 1)) });
            Assert.That(StanceList.Frames(list.Count), Is.EqualTo(StanceList.MaxRows));
            Assert.That(StanceList.Folded(list.Count), Is.EqualTo(2), "the two oldest go into 「+2」");
        }
    }
}
