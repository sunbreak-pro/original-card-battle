// What the omen badge may show for how far the journal has read the enemy (battle-visual-v1 §4.4,
// battle_ui_ux_v2 §7, #455). Pure C#: the writer of the script applies it, so the View shows what it
// is handed and judges nothing. The journal does not hand a level to the battle yet, so every source
// writes Full (2) and the badge looks as it did before.
using System;

namespace Depiction
{
    public static class OmenDisclosure
    {
        /// <summary>開示度 0: the kind's icon and word only.</summary>
        public const int Unread = 0;

        /// <summary>開示度 1.</summary>
        public const int Partial = 1;

        /// <summary>開示度 2: everything the badge has. The default until the journal hands a level in.</summary>
        public const int Full = 2;

        /// <summary>
        /// battle-visual-v1 §4.4: the numbers (attack and guard, on the omen and on the plan) show for an
        /// enemy read to 2 only; 0 and 1 show the icon and the kind word. battle_ui_ux_v2 §7 shows them
        /// from 1, a battle-lane assumption that #455 leaves to the handoff on the conflict.
        /// </summary>
        public static bool ShowsNumbers(int level)
        {
            return Clamp(level) >= Full;
        }

        /// <summary>
        /// <paramref name="omen"/> as an enemy read to <paramref name="level"/> shows it: the level is
        /// kept on the frame, and the numbers it may not show are blanked. The kind, its icon, the plan
        /// and the hit mark are left as they are. Returns the same frame.
        /// </summary>
        public static OmenFrame Apply(OmenFrame omen, int level)
        {
            if (omen == null) throw new ArgumentNullException(nameof(omen));
            omen.Disclosure = Clamp(level);
            if (!ShowsNumbers(level))
            {
                omen.ValueText = "";
                omen.PlanValueText = "";
            }
            return omen;
        }

        /// <summary>
        /// 0〜2. The −1 of battle_ui_ux_v2 §7 (the enemies of the 100% run, hidden whole) has no parts
        /// yet, so it reads as 0.
        /// </summary>
        public static int Clamp(int level)
        {
            return Math.Max(Unread, Math.Min(Full, level));
        }
    }
}
