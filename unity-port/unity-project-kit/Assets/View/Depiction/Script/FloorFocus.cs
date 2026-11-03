// Which enemy the floor's one gap number counts to, and which enemy Tab moves it to, in a battle of
// several (battle_ui_ux_v2 §1.4 / §2.2 の 3 / §3.9 / §3.4 キー操作, #762). Pure C#, so `dotnet test`
// holds the same answers the View acts on. The numbers themselves are the script's (EnemyFrame.Gap,
// FloorFrame.Gap); this only says whose to show.
using System;
using System.Collections.Generic;

namespace Depiction
{
    /// <summary>The one gap number on the floor and the two cells it stands between (FloorView.SetGap's arguments).</summary>
    public readonly struct GapReading
    {
        public readonly int Gap;
        public readonly int PlayerCell;
        public readonly int PlayerSize;
        public readonly int EnemyCell;

        public GapReading(int gap, int playerCell, int playerSize, int enemyCell)
        {
            Gap = gap;
            PlayerCell = playerCell;
            PlayerSize = playerSize;
            EnemyCell = enemyCell;
        }

        public override string ToString() => "N " + Gap + " (" + PlayerCell + "×" + PlayerSize + " → " + EnemyCell + ")";
    }

    public static class FloorFocus
    {
        /// <summary>
        /// The enemy the gap number counts to: the one a held card is over (<paramref name="picked"/>), else
        /// the one Tab chose (<paramref name="chosen"/>), while it stands. −1 is the floor's own number, the
        /// nearest standing enemy's (§2.2 の 3). Always −1 with one enemy.
        /// </summary>
        public static int GapUnit(DepictionFrame frame, int picked, int chosen)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.Enemies.Count <= 1) return -1;
            if (Counts(frame, picked)) return picked;
            if (Counts(frame, chosen)) return chosen;
            return -1;
        }

        /// <summary>
        /// The number the floor shows for enemy <paramref name="unit"/>: its own N to the player and its
        /// lowest cell. −1, a fallen enemy, or one outside the list read the floor's own (FloorFrame.Gap).
        /// </summary>
        public static GapReading GapOf(DepictionFrame frame, int unit)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            FloorFrame floor = frame.Floor;
            if (unit >= 0 && unit < frame.Enemies.Count && frame.Enemies[unit].Gap >= 0)
            {
                EnemyFrame enemy = frame.Enemies[unit];
                return new GapReading(enemy.Gap, floor.PlayerCell, floor.PlayerSize, enemy.Cell);
            }
            return new GapReading(floor.Gap, floor.PlayerCell, floor.PlayerSize, floor.EnemyCell);
        }

        /// <summary>
        /// §3.4 Tab: the enemy the gap number moves to from <paramref name="current"/> (−1, or one that no
        /// longer stands, is the nearest one the floor shows at rest). The standing enemies take turns from
        /// near to far and back to the nearest. −1 when fewer than two stand: there is nobody to move to.
        /// </summary>
        public static int NextTab(DepictionFrame frame, int current)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            var standing = new List<int>();
            for (int k = 0; k < frame.Enemies.Count; k++)
            {
                if (Counts(frame, k)) standing.Add(k);
            }
            if (standing.Count < 2) return -1;
            standing.Sort((a, b) =>
            {
                int byCell = frame.Enemies[a].Cell.CompareTo(frame.Enemies[b].Cell);
                return byCell != 0 ? byCell : a.CompareTo(b);
            });
            int from = Counts(frame, current) ? current : EnemyScreen.FocusOf(frame);
            int at = standing.IndexOf(from);
            return standing[(at + 1) % standing.Count];
        }

        /// <summary>
        /// The box under the pointer on the 1920 screen, or −1: which omen badge a hover is on (§2.2 の 2).
        /// The later box wins where two overlap, as it is drawn over the earlier.
        /// </summary>
        public static int BoxAt(IReadOnlyList<ScreenBox> boxes, float x, float y)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            for (int i = boxes.Count - 1; i >= 0; i--)
            {
                ScreenBox box = boxes[i];
                if (x >= box.X && x <= box.X + box.Width && y >= box.Y && y <= box.Y + box.Height) return i;
            }
            return -1;
        }

        private static bool Counts(DepictionFrame frame, int k)
        {
            return k >= 0 && k < frame.Enemies.Count && EnemyScreen.Stands(frame, k);
        }
    }
}
