// Which enemy each part of the screen belongs to when a battle has several (battle_ui_ux_v2 §2.2,
// #589). Pure C#, so `dotnet test` holds the same answers the MonoBehaviours act on: the View asks
// here and computes nothing. Enemy k is DepictionFrame.Enemies[k] for the whole battle. A source that
// keeps one enemy (the filming script) writes no list, so its one enemy is enemy 0, read from the
// single-valued members; a one-enemy battle therefore takes the same path whichever source plays it.
using System;

namespace Depiction
{
    public static class EnemyScreen
    {
        /// <summary>How many enemies the screen draws: the list's, or the one a source without a list keeps.</summary>
        public static int CountOf(DepictionFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            return frame.Enemies.Count > 0 ? frame.Enemies.Count : 1;
        }

        /// <summary>Enemy <paramref name="k"/>'s number panel and figure.</summary>
        public static UnitFrame UnitOf(DepictionFrame frame, int k)
        {
            return InList(frame, k) ? frame.Enemies[k].Unit : frame.Enemy;
        }

        /// <summary>Enemy <paramref name="k"/>'s omen badge.</summary>
        public static OmenFrame OmenOf(DepictionFrame frame, int k)
        {
            return InList(frame, k) ? frame.Enemies[k].Omen : frame.Omen;
        }

        /// <summary>Enemy <paramref name="k"/>'s lowest cell.</summary>
        public static int CellOf(DepictionFrame frame, int k)
        {
            return InList(frame, k) ? frame.Enemies[k].Cell : frame.Floor.EnemyCell;
        }

        /// <summary>How many cells enemy <paramref name="k"/> takes, at least one.</summary>
        public static int SizeOf(DepictionFrame frame, int k)
        {
            return Math.Max(1, InList(frame, k) ? frame.Enemies[k].Size : frame.Floor.EnemySize);
        }

        /// <summary>
        /// The enemy the single-valued members describe (DepictionFrame.Enemy): §2.2 の 3's nearest
        /// standing one, or the one that fell last. A card that lands on "the enemy" without aiming, and
        /// the receiver at rest, stand with it. 0 with one enemy.
        /// </summary>
        public static int FocusOf(DepictionFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            for (int k = 0; k < frame.Enemies.Count; k++)
            {
                if (ReferenceEquals(frame.Enemies[k].Unit, frame.Enemy)) return k;
            }
            return 0;
        }

        /// <summary>
        /// §2.2 の 5: enemy <paramref name="k"/> takes a target frame and a place on the badges' row. Not one
        /// whose fall has played (Down), nor one the core has already taken off the line with its fall still
        /// to play (EnemyFrame.Gap is −1).
        /// </summary>
        public static bool Stands(DepictionFrame frame, int k)
        {
            if (UnitOf(frame, k).Down) return false;
            return !InList(frame, k) || frame.Enemies[k].Gap >= 0;
        }

        /// <summary>
        /// §5.15 順 5: another enemy's figure is still up, so enemy <paramref name="k"/>'s fall is not the last
        /// to play (順 7). This reads the screen and not the core: one whose fall is still to play counts, so
        /// when a move fells two, the first plays 順 5 beside the second figure and the second plays 順 7.
        /// </summary>
        public static bool OthersStand(DepictionFrame frame, int k)
        {
            int count = CountOf(frame);
            for (int other = 0; other < count; other++)
            {
                if (other != k && !UnitOf(frame, other).Down) return true;
            }
            return false;
        }

        /// <summary>
        /// The enemy a cue goes to: the one <see cref="Cue.Unit"/> names, or the first when it names none
        /// the screen draws (one enemy, or a script that leaves it 0).
        /// </summary>
        public static int IndexOf(Cue cue, int count)
        {
            if (cue == null) throw new ArgumentNullException(nameof(cue));
            return cue.Unit > 0 && cue.Unit < count ? cue.Unit : 0;
        }

        /// <summary>
        /// The enemy a RangeSwitch walks and the cell it walks to; −1 when the player moved alone. With one
        /// enemy the cue's EnemyCellAfter says where it stands after the move, moved or not. With several
        /// EnemyCellAfter is the focus enemy's, so only <see cref="Cue.UnitCellAfter"/> names a move
        /// (depiction-multi-enemy.md).
        /// </summary>
        public static int MovedUnit(Cue cue, int count, out int cell)
        {
            if (cue == null) throw new ArgumentNullException(nameof(cue));
            if (count <= 1)
            {
                cell = cue.EnemyCellAfter;
                return cell > 0 ? 0 : -1;
            }
            cell = cue.UnitCellAfter;
            return cell > 0 ? IndexOf(cue, count) : -1;
        }

        /// <summary>
        /// §2.2 の 1: the enemies in the order they are drawn, far from the player first, so the nearer one
        /// stands in front. The player stands left of them all, so nearer is the lower cell.
        /// </summary>
        public static int[] BackToFront(DepictionFrame frame)
        {
            int count = CountOf(frame);
            var order = new int[count];
            for (int k = 0; k < count; k++) order[k] = k;
            Array.Sort(order, (a, b) =>
            {
                int byCell = CellOf(frame, b).CompareTo(CellOf(frame, a));
                return byCell != 0 ? byCell : b.CompareTo(a);
            });
            return order;
        }

        /// <summary>
        /// §2.2 の 5 / §3.9: the frame on enemy <paramref name="k"/> is the reaching one, or steel hooks with
        /// no number. With several the script writes one answer per enemy (CardFace.ReachOn). With one
        /// the hover panel's own answer is the frame's too (CardFace.ReachHint), which is what a source
        /// that writes no list has.
        /// </summary>
        public static bool Reaches(CardFace face, int k, int count)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            return count > 1 ? face.ReachFor(k) : string.IsNullOrEmpty(face.ReachHint);
        }

        /// <summary>
        /// §6.3: the enemy a held card's numbers are read on (its lamp, its cost, the 構えの予告): the one it
        /// is over, or the one it would go to unnamed (CardFace.TargetUnit), or the first.
        /// </summary>
        public static int FramedUnit(CardFace face, int picked)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            return picked >= 0 ? picked : Math.Max(0, face.TargetUnit);
        }

        private static bool InList(DepictionFrame frame, int k)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            return k >= 0 && k < frame.Enemies.Count;
        }
    }
}
