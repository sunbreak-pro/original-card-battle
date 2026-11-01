// Where each enemy's swatch stands (battle-visual-v1 §4.2 / §4.4 / §4.5 / §4.6, #589): the small tile of
// that enemy's red stripes that ties its omen badge, its HP row and its stance frames to its cells on the
// floor. Pure C#, so `dotnet test` holds the same answers the Views act on.
using System;

namespace Depiction
{
    public static class EnemySwatch
    {
        /// <summary>§4.4 1 行目: 22 px at the right end of the omen badge's first row.</summary>
        public const float BadgeSize = 22f;

        /// <summary>§4.5 1 行目: 16 px at the left end of the HP row.</summary>
        public const float PanelSize = 16f;

        /// <summary>
        /// Whether enemy <paramref name="k"/>'s omen badge shows its swatch. With several enemies it stands
        /// while the omen ties the badge to something on the floor: the cells it aims at, or the hit mark
        /// read from them. Never on a move or a rest, which stripe nothing (§2.3 keeps the red to the cells
        /// an action reaches), nor on an enemy read to 開示度 0, whose cells the floor does not draw (§7).
        ///
        /// One enemy keeps the badge it always had: §4.4 puts the swatch on the first row with the hit mark
        /// on the second, with or without other enemies. <see cref="EnemyFrame.Swatch"/> stays false there.
        /// </summary>
        public static bool OnBadge(DepictionFrame frame, int k)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.Enemies.Count <= 1) return EnemyScreen.OmenOf(frame, k).Hit != OmenHit.None;
            if (k < 0 || k >= frame.Enemies.Count) return false;
            EnemyFrame enemy = frame.Enemies[k];
            OmenFrame omen = enemy.Omen;
            if (!enemy.Swatch || !omen.Visible || omen.Icon == OmenIcon.Rest || omen.Disclosure < OmenDisclosure.Partial) return false;
            return enemy.AimCells.Count > 0 || omen.Hit != OmenHit.None;
        }

        /// <summary>§4.5: whether enemy <paramref name="k"/>'s HP row shows its swatch: only in a battle of two or more.</summary>
        public static bool OnPanel(DepictionFrame frame, int k)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            return k >= 0 && k < frame.Enemies.Count && frame.Enemies[k].Swatch;
        }

        /// <summary>The direction of enemy <paramref name="k"/>'s stripes: 0 for the one enemy of a source that lists none.</summary>
        public static int DirectionOf(DepictionFrame frame, int k)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            return k >= 0 && k < frame.Enemies.Count ? frame.Enemies[k].StripeDirection : 0;
        }
    }
}
