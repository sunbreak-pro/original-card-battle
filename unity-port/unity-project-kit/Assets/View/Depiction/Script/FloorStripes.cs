// What one floor cell draws of the enemies' aims (battle-visual-v1 §4.2, battle_ui_ux_v2 §1.4 / §2.2 の 2,
// #589). Pure C#, so `dotnet test` holds the same answers FloorView acts on. With several enemies each
// reads its own EnemyFrame: one set of stripes per enemy aiming at the cell, in that enemy's direction,
// so the directions cross where two overlap. A frame of one enemy, or of none listed (the filming script),
// reads FloorFrame as the screen always has: its cells are the one enemy's, in direction 0.
using System;
using System.Collections.Generic;

namespace Depiction
{
    /// <summary>
    /// One set of stripes on a cell: its direction (EnemyFrame.StripeDirection), whether it is the §4.2
    /// dense one, and whether it is another enemy's while one omen badge is hovered (§2.2 の 2, #762).
    /// </summary>
    public readonly struct StripeMark
    {
        public readonly int Direction;
        public readonly bool Dense;
        public readonly bool Faint;

        public StripeMark(int direction, bool dense, bool faint = false)
        {
            Direction = direction;
            Dense = dense;
            Faint = faint;
        }

        public override string ToString() => Direction + (Dense ? " dense" : "") + (Faint ? " faint" : "");
    }

    public static class FloorStripes
    {
        /// <summary>
        /// Trial's approved look for a move's frame on a cell another enemy aims at: its dashes stand this
        /// many px inside the red frame, since the two are the same 2 px at the same place otherwise.
        /// </summary>
        public const float MoveInsetOverAim = 4f;

        /// <summary>
        /// battle_ui_ux_v2 §2.2 の 2 (#762): while one enemy's omen badge is hovered, the other enemies'
        /// stripes, frames and lights stand at this share of their opacity. battle-visual-v1 gives no value
        /// yet; this one keeps them seen while the hovered enemy's stand out at 100%.
        /// </summary>
        public const float FaintAlpha = 0.3f;

        private static readonly StripeMark[] None = new StripeMark[0];

        /// <summary>
        /// The stripes on <paramref name="cell"/> (1-based), in the enemies' order. Empty when nobody aims
        /// at it. A hidden omen aims at nothing, and neither does one read to 開示度 0, alone or among
        /// several (battle_ui_ux_v2 §7, §5.8 提示; the one-enemy case is #765). The writer still writes
        /// AimCells at 0, since the omen's hit mark reads them; the floor alone leaves them off.
        /// </summary>
        public static IReadOnlyList<StripeMark> Of(DepictionFrame frame, int cell) => Of(frame, cell, -1);

        /// <summary>
        /// <see cref="Of(DepictionFrame, int)"/> while enemy <paramref name="hovered"/>'s omen badge is
        /// hovered (§2.2 の 2): every other enemy's stripes come back <see cref="StripeMark.Faint"/>. −1, or
        /// an enemy <see cref="Hovered"/> turns down, fades nothing.
        /// </summary>
        public static IReadOnlyList<StripeMark> Of(DepictionFrame frame, int cell, int hovered)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            hovered = Hovered(frame, hovered);
            if (!Several(frame))
            {
                FloorFrame floor = frame.Floor;
                if (!frame.Omen.Visible || !ShowsCells(frame.Omen) || !floor.AimCells.Contains(cell)) return None;
                return new[] { new StripeMark(0, floor.DenseCells.Contains(cell)) };
            }
            List<StripeMark> marks = null;
            for (int k = 0; k < frame.Enemies.Count; k++)
            {
                EnemyFrame enemy = frame.Enemies[k];
                if (!Reads(enemy) || !enemy.AimCells.Contains(cell)) continue;
                if (marks == null) marks = new List<StripeMark>();
                marks.Add(new StripeMark(enemy.StripeDirection, enemy.DenseCells.Contains(cell), hovered >= 0 && k != hovered));
            }
            return (IReadOnlyList<StripeMark>)marks ?? None;
        }

        /// <summary>Whether a moving omen lands on <paramref name="cell"/>: any enemy's, the violet dashed frame of §4.2 / §5.8.</summary>
        public static bool MoveTo(DepictionFrame frame, int cell)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (!Several(frame)) return ShowsCells(frame.Omen) && frame.Floor.MoveCells.Contains(cell);
            foreach (EnemyFrame enemy in frame.Enemies)
            {
                if (Reads(enemy) && enemy.MoveCells.Contains(cell)) return true;
            }
            return false;
        }

        /// <summary>
        /// How far inside the cell the move's dashed frame stands: 0, or <see cref="MoveInsetOverAim"/> on a
        /// cell of a battle of several that one enemy aims at and another moves to.
        /// </summary>
        public static float MoveInset(DepictionFrame frame, int cell)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            return Several(frame) && MoveTo(frame, cell) && Of(frame, cell).Count > 0 ? MoveInsetOverAim : 0f;
        }

        /// <summary>
        /// The enemy whose aims a hover on its omen badge keeps (§2.2 の 2), or −1: only in a battle of
        /// several, only a standing enemy, and only one whose cells the floor draws at all (a hidden omen,
        /// or one read to 開示度 0, would fade the others for nothing to look at).
        /// </summary>
        public static int Hovered(DepictionFrame frame, int k)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (!Several(frame) || k < 0 || k >= frame.Enemies.Count) return -1;
            return EnemyScreen.Stands(frame, k) && Reads(frame.Enemies[k]) ? k : -1;
        }

        /// <summary>The cell's one red frame and light fade with its stripes: when every set on it is another enemy's.</summary>
        public static bool AimFaint(IReadOnlyList<StripeMark> marks)
        {
            if (marks == null) throw new ArgumentNullException(nameof(marks));
            if (marks.Count == 0) return false;
            foreach (StripeMark mark in marks)
            {
                if (!mark.Faint) return false;
            }
            return true;
        }

        /// <summary>
        /// The violet dashed frame on <paramref name="cell"/> fades while enemy <paramref name="hovered"/>'s
        /// badge is hovered and that enemy's move does not land there (§2.2 の 2).
        /// </summary>
        public static bool MoveFaint(DepictionFrame frame, int cell, int hovered)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            hovered = Hovered(frame, hovered);
            return hovered >= 0 && MoveTo(frame, cell) && !frame.Enemies[hovered].MoveCells.Contains(cell);
        }

        private static bool Several(DepictionFrame frame) => frame.Enemies.Count > 1;

        private static bool Reads(EnemyFrame enemy) => enemy.Omen.Visible && ShowsCells(enemy.Omen);

        private static bool ShowsCells(OmenFrame omen) => omen.Disclosure >= OmenDisclosure.Partial;
    }
}
