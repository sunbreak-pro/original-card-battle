using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DungeonCore
{
    /// <summary>
    /// 崩壊の暦: the one clock of the game (seven_layers_v4.md §2, concept-v3.md §6). Every life shares it. A new node
    /// costs one day, walking back over a node costs none, the interlude costs none, and handing over to the next
    /// heir costs days by how the life ended. At 0 the dragon god wakes and the world ends.
    /// </summary>
    public static class Calendar
    {
        /// <summary>§2.1: the days left when the world starts.</summary>
        public const int TotalDays = 360;

        /// <summary>§2.1: the next heir sets out this many days after a life that came back alive.</summary>
        public const int HandoverSurvivedDays = 3;

        /// <summary>tools_and_consumables_v4.md §2.4: the same, with 帰り道の糸 carried.</summary>
        public const int HandoverShortDays = 1;

        /// <summary>§2.1: the mask takes this long to come back from a life that died.</summary>
        public const int HandoverFallenDays = 10;

        /// <summary>§2.1: one who went down past the three kings does not come back; the mask returns as from a death.</summary>
        public const int HandoverDescendedDays = 10;

        /// <summary>§2.2: a downed king comes back after this many days, and the calendar stands still meanwhile.</summary>
        public const int KingReturnDays = 60;
    }

    /// <summary>
    /// Where the calendar stands: the days left, and how long until each downed king comes back. Immutable: every
    /// change returns a new state, so a run replays from its inputs like the rest of the exploration core.
    ///
    /// While any king is down the calendar does not count down (§2.2: the seal only wears while all three kings
    /// stand). Two kings down at once still freeze one day per day, so their returns overlap rather than add.
    /// </summary>
    public sealed class CalendarState : IEquatable<CalendarState>
    {
        private static readonly IReadOnlyDictionary<Region, int> NoKings =
            new ReadOnlyDictionary<Region, int>(new Dictionary<Region, int>());

        /// <summary>A new world: every day left and every king standing.</summary>
        public static CalendarState Fresh { get; } = new CalendarState(Calendar.TotalDays);

        public CalendarState(int daysLeft, IReadOnlyDictionary<Region, int>? kingReturns = null)
        {
            if (daysLeft < 0 || daysLeft > Calendar.TotalDays)
                throw new ArgumentOutOfRangeException(nameof(daysLeft), daysLeft, $"the calendar runs 0..{Calendar.TotalDays}");

            DaysLeft = daysLeft;
            if (kingReturns == null || kingReturns.Count == 0)
            {
                KingReturns = NoKings;
                return;
            }

            // Kept in Region order, so two equal states enumerate (and save) the same way.
            var kings = new SortedDictionary<Region, int>();
            foreach (var pair in kingReturns)
            {
                if (!pair.Key.HasKing())
                    throw new ArgumentException($"{pair.Key} has no king to come back", nameof(kingReturns));
                if (pair.Value < 1 || pair.Value > Calendar.KingReturnDays)
                    throw new ArgumentOutOfRangeException(
                        nameof(kingReturns), pair.Value, $"a downed king comes back in 1..{Calendar.KingReturnDays} days");
                kings[pair.Key] = pair.Value;
            }
            KingReturns = new ReadOnlyDictionary<Region, int>(kings);
        }

        /// <summary>Days until the world ends. Never below 0.</summary>
        public int DaysLeft { get; }

        /// <summary>Days until each downed king comes back. Only the three king regions appear; a standing king is absent.</summary>
        public IReadOnlyDictionary<Region, int> KingReturns { get; }

        /// <summary>A king is down: the next day does not count down (§2.2).</summary>
        public bool IsFrozen => KingReturns.Count > 0;

        /// <summary>The calendar ran out: the dragon god wakes and the world ends (§2.1 期限切れ).</summary>
        public bool IsExpired => DaysLeft <= 0;

        /// <summary>All three kings are down at the same time: the clear (§2.2).</summary>
        public bool ClearReached => KingReturns.Count == 3;

        /// <summary>
        /// Lets <paramref name="days"/> days pass, one at a time. A day that starts with a king down leaves the days
        /// left alone and brings every downed king a day closer; a king whose count reaches 0 is back. Any other day
        /// takes one off the days left, never below 0.
        /// </summary>
        public CalendarState Advance(int days = 1)
        {
            if (days < 0) throw new ArgumentOutOfRangeException(nameof(days), days, "the calendar does not go back");

            int left = DaysLeft;
            var kings = new Dictionary<Region, int>(KingReturns.ToDictionary(p => p.Key, p => p.Value));
            for (int i = 0; i < days; i++)
            {
                if (kings.Count > 0)
                {
                    foreach (var region in kings.Keys.ToList())
                    {
                        int next = kings[region] - 1;
                        if (next <= 0) kings.Remove(region);
                        else kings[region] = next;
                    }
                }
                else
                {
                    left = Math.Max(0, left - 1);
                }
            }
            return days == 0 ? this : new CalendarState(left, kings);
        }

        /// <summary>A king falls: it comes back in <see cref="Calendar.KingReturnDays"/> days, counted from now.</summary>
        public CalendarState KingDown(Region region)
        {
            if (!region.HasKing()) throw new ArgumentException($"{region} has no king", nameof(region));
            var kings = KingReturns.ToDictionary(p => p.Key, p => p.Value);
            kings[region] = Calendar.KingReturnDays;
            return new CalendarState(DaysLeft, kings);
        }

        /// <summary>
        /// The days the calendar would gain if <paramref name="region"/>'s king fell now (§2.2: the screen shows it):
        /// its return time minus the days another downed king already holds the calendar still.
        /// </summary>
        public int ExtensionIfKingDown(Region region)
        {
            if (!region.HasKing()) throw new ArgumentException($"{region} has no king", nameof(region));
            int overlap = KingReturns.Where(p => p.Key != region).Select(p => p.Value).DefaultIfEmpty(0).Max();
            return Math.Max(0, Calendar.KingReturnDays - overlap);
        }

        /// <summary>
        /// The days between this life's end and the next heir's departure (§2.1): 3 after a life that came back alive
        /// (1 with 帰り道の糸, <paramref name="shortReturn"/>), 10 after a death or a descent past the kings. A world
        /// that expired has no next heir, so it stays as it is.
        /// </summary>
        public CalendarState Handover(RunPhase endedAs, bool shortReturn)
        {
            switch (endedAs)
            {
                case RunPhase.Expired:
                    return this;
                case RunPhase.Survived:
                    return Advance(shortReturn ? Calendar.HandoverShortDays : Calendar.HandoverSurvivedDays);
                case RunPhase.Completed:
                    return Advance(Calendar.HandoverDescendedDays);
                default:
                    return Advance(Calendar.HandoverFallenDays);
            }
        }

        public bool Equals(CalendarState? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return DaysLeft == other.DaysLeft
                && KingReturns.Count == other.KingReturns.Count
                && KingReturns.All(p => other.KingReturns.TryGetValue(p.Key, out int v) && v == p.Value);
        }

        public override bool Equals(object? obj) => Equals(obj as CalendarState);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(DaysLeft);
            foreach (var pair in KingReturns)
            {
                hash.Add(pair.Key);
                hash.Add(pair.Value);
            }
            return hash.ToHashCode();
        }

        public override string ToString() =>
            KingReturns.Count == 0
                ? $"{DaysLeft} days"
                : $"{DaysLeft} days ({string.Join(", ", KingReturns.Select(p => $"{p.Key.ToToken()} {p.Value}"))})";
    }
}
