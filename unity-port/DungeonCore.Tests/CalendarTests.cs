using DungeonCore;

namespace DungeonCore.Tests
{
    /// <summary>崩壊の暦 (seven_layers_v4.md §2, #888).</summary>
    public class CalendarTests
    {
        private static IReadOnlyDictionary<Region, int> Kings(params (Region Region, int Days)[] kings) =>
            kings.ToDictionary(k => k.Region, k => k.Days);

        [Test]
        public void TheNumbersAreTheTable()
        {
            // §2.1 and §2.2.
            Assert.That(Calendar.TotalDays, Is.EqualTo(360));
            Assert.That(Calendar.HandoverSurvivedDays, Is.EqualTo(3));
            Assert.That(Calendar.HandoverShortDays, Is.EqualTo(1));
            Assert.That(Calendar.HandoverFallenDays, Is.EqualTo(10));
            Assert.That(Calendar.HandoverDescendedDays, Is.EqualTo(10));
            Assert.That(Calendar.KingReturnDays, Is.EqualTo(60));
        }

        [Test]
        public void AFreshWorldHasEveryDayAndEveryKing()
        {
            var fresh = CalendarState.Fresh;
            Assert.That(fresh.DaysLeft, Is.EqualTo(360));
            Assert.That(fresh.KingReturns, Is.Empty);
            Assert.That(fresh.IsFrozen, Is.False);
            Assert.That(fresh.IsExpired, Is.False);
            Assert.That(fresh.ClearReached, Is.False);
            Assert.That(new CalendarState(360), Is.EqualTo(fresh));
        }

        [Test]
        public void EachDayTakesOneOff()
        {
            var state = CalendarState.Fresh;
            for (int i = 1; i <= 10; i++)
            {
                state = state.Advance();
                Assert.That(state.DaysLeft, Is.EqualTo(360 - i));
            }
            Assert.That(CalendarState.Fresh.Advance(30).DaysLeft, Is.EqualTo(330));
            Assert.That(CalendarState.Fresh.Advance(0), Is.SameAs(CalendarState.Fresh));
            Assert.Throws<ArgumentOutOfRangeException>(() => CalendarState.Fresh.Advance(-1));
        }

        [Test]
        public void TheCalendarStopsAtZeroAndExpires()
        {
            var last = new CalendarState(1);
            Assert.That(last.IsExpired, Is.False);
            var gone = last.Advance();
            Assert.That(gone.DaysLeft, Is.Zero);
            Assert.That(gone.IsExpired, Is.True);
            Assert.That(gone.Advance(5).DaysLeft, Is.Zero);
            Assert.That(new CalendarState(3).Advance(10).IsExpired, Is.True);
        }

        [Test]
        public void ADownedKingFreezesTheCalendarUntilItComesBack()
        {
            var down = new CalendarState(200).KingDown(Region.Dragon);
            Assert.That(down.KingReturns, Is.EqualTo(Kings((Region.Dragon, 60))));
            Assert.That(down.IsFrozen, Is.True);

            var later = down.Advance(59);
            Assert.That(later.DaysLeft, Is.EqualTo(200), "frozen days do not count down");
            Assert.That(later.KingReturns[Region.Dragon], Is.EqualTo(1));

            var back = later.Advance();
            Assert.That(back.DaysLeft, Is.EqualTo(200), "the day the king comes back is still frozen");
            Assert.That(back.KingReturns, Is.Empty);
            Assert.That(back.IsFrozen, Is.False);
            Assert.That(back.Advance().DaysLeft, Is.EqualTo(199));

            // 60 frozen days in all: the calendar is 60 days longer than it would have been.
            Assert.That(down.Advance(100).DaysLeft, Is.EqualTo(200 - 40));
        }

        [Test]
        public void OverlappingKingsFreezeOneDayPerDay()
        {
            // §2.2: two kings down at once still count one day as one day.
            var first = new CalendarState(200).KingDown(Region.Dragon).Advance(20);
            Assert.That(first.KingReturns[Region.Dragon], Is.EqualTo(40));

            var both = first.KingDown(Region.Ghost);
            Assert.That(both.KingReturns, Is.EqualTo(Kings((Region.Dragon, 40), (Region.Ghost, 60))));

            var dragonBack = both.Advance(40);
            Assert.That(dragonBack.DaysLeft, Is.EqualTo(200));
            Assert.That(dragonBack.KingReturns, Is.EqualTo(Kings((Region.Ghost, 20))));

            var allBack = dragonBack.Advance(20);
            Assert.That(allBack.KingReturns, Is.Empty);
            Assert.That(allBack.DaysLeft, Is.EqualTo(200), "80 days passed, all of them frozen");
        }

        [Test]
        public void TheExtensionIsTheReturnLessTheDaysAlreadyFrozen()
        {
            // §2.2: the screen shows 「暦が N 日延びる見込み」 before the king falls.
            var calm = CalendarState.Fresh;
            Assert.That(calm.ExtensionIfKingDown(Region.Dragon), Is.EqualTo(60));

            var oneDown = calm.KingDown(Region.Dragon).Advance(20); // dragon back in 40
            Assert.That(oneDown.ExtensionIfKingDown(Region.Ghost), Is.EqualTo(20));
            Assert.That(oneDown.ExtensionIfKingDown(Region.Dragon), Is.EqualTo(60), "the king's own count is not an overlap");

            var twoDown = oneDown.KingDown(Region.Parasite); // parasite back in 60
            Assert.That(twoDown.ExtensionIfKingDown(Region.Ghost), Is.Zero);

            // What it says is what happens.
            int before = oneDown.Advance(100).DaysLeft;
            int after = oneDown.KingDown(Region.Ghost).Advance(100).DaysLeft;
            Assert.That(after - before, Is.EqualTo(oneDown.ExtensionIfKingDown(Region.Ghost)));

            Assert.Throws<ArgumentException>(() => calm.ExtensionIfKingDown(Region.Common));
        }

        [Test]
        public void ThreeKingsDownAtOnceIsTheClear()
        {
            var state = CalendarState.Fresh.KingDown(Region.Dragon).Advance(10).KingDown(Region.Parasite).Advance(10);
            Assert.That(state.ClearReached, Is.False);
            Assert.That(state.KingDown(Region.Ghost).ClearReached, Is.True);

            // The kings come back, so felling them one at a time is no clear.
            var slow = CalendarState.Fresh.KingDown(Region.Dragon).Advance(60).KingDown(Region.Parasite).Advance(60)
                .KingDown(Region.Ghost);
            Assert.That(slow.ClearReached, Is.False);
            Assert.That(slow.KingReturns.Keys, Is.EqualTo(new[] { Region.Ghost }));
        }

        [Test]
        public void OnlyTheThreeRegionsHaveAKing()
        {
            Assert.Throws<ArgumentException>(() => CalendarState.Fresh.KingDown(Region.Common));
            Assert.Throws<ArgumentException>(() => new CalendarState(10, Kings((Region.Common, 5))));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CalendarState(10, Kings((Region.Dragon, 61))));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CalendarState(10, Kings((Region.Dragon, 0))));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CalendarState(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CalendarState(361));
        }

        [Test]
        public void TheHandoverIsThreeOneOrTenDays()
        {
            var state = new CalendarState(100);
            Assert.That(state.Handover(RunPhase.Survived, shortReturn: false).DaysLeft, Is.EqualTo(97));
            Assert.That(state.Handover(RunPhase.Survived, shortReturn: true).DaysLeft, Is.EqualTo(99), "帰り道の糸");
            Assert.That(state.Handover(RunPhase.Fallen, shortReturn: false).DaysLeft, Is.EqualTo(90));
            Assert.That(state.Handover(RunPhase.Fallen, shortReturn: true).DaysLeft, Is.EqualTo(90), "the thread only helps a life that came back");
            Assert.That(state.Handover(RunPhase.Completed, shortReturn: true).DaysLeft, Is.EqualTo(90), "one who went down past the kings does not come back");
            Assert.That(state.Handover(RunPhase.Expired, shortReturn: false), Is.SameAs(state));
        }

        [Test]
        public void AHandoverWhileAKingIsDownIsFrozenToo()
        {
            var state = new CalendarState(100).KingDown(Region.Ghost);
            var next = state.Handover(RunPhase.Fallen, shortReturn: false);
            Assert.That(next.DaysLeft, Is.EqualTo(100));
            Assert.That(next.KingReturns[Region.Ghost], Is.EqualTo(50));
        }

        [Test]
        public void EqualStatesAreEqual()
        {
            var a = new CalendarState(50, Kings((Region.Ghost, 3), (Region.Dragon, 7)));
            var b = new CalendarState(50, Kings((Region.Dragon, 7), (Region.Ghost, 3)));
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a.KingReturns.Keys, Is.EqualTo(new[] { Region.Dragon, Region.Ghost }), "kept in Region order");
            Assert.That(a, Is.Not.EqualTo(new CalendarState(50)));
            Assert.That(a, Is.Not.EqualTo(new CalendarState(51, Kings((Region.Dragon, 7), (Region.Ghost, 3)))));
        }
    }
}
