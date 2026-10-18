// What the omen badge may show for how far the journal has read the enemy (battle-visual-v1 §4.4,
// battle_ui_ux_v2 §7, #455). Pure C#: the writer of the script applies it, so the View shows what it
// is handed and judges nothing. The level reaches the writer once, as the battle starts (#500:
// BattleLaunch, DemoSession); a battle nobody hands a level to is written at Full (2).
using System;

namespace Depiction
{
    public static class OmenDisclosure
    {
        /// <summary>開示度 0: the first omen's kind (icon and word) only; no plan.</summary>
        public const int Unread = 0;

        /// <summary>開示度 1.</summary>
        public const int Partial = 1;

        /// <summary>開示度 2: everything the badge has. The default until the journal hands a level in.</summary>
        public const int Full = 2;

        /// <summary>
        /// battle-visual-v1 §4.4: the numbers (attack and guard, on the omen and on the plan) show for an
        /// enemy read to 2 only; 0 and 1 show the icon and the kind word. battle_ui_ux_v2 §7 says the same
        /// since #496 (decision A, 2026-10-12).
        /// </summary>
        public static bool ShowsNumbers(int level)
        {
            return Clamp(level) >= Full;
        }

        /// <summary>
        /// battle_ui_ux_v2 §7 / §3.10 / §5.16 順 2: an elite's or a boss's second action (the plan) shows
        /// from 開示度 1; an enemy read to 0 shows its first omen's kind alone. battle-visual-v1 §4.4 says
        /// nothing of the plan at 0, so the two do not disagree here.
        /// </summary>
        public static bool ShowsPlan(int level)
        {
            return Clamp(level) >= Partial;
        }

        /// <summary>
        /// <paramref name="omen"/> as an enemy read to <paramref name="level"/> shows it: the level is
        /// kept on the frame, and what it may not show is taken off — below 2 the numbers and the
        /// statuses a 状態付与 omen puts on (§4.4: 数字と状態のアイコンは開示度 2 の敵にだけ), at 0 the
        /// plan as well. The kind, its icon and the hit mark are left as they are. Returns the same frame.
        /// </summary>
        public static OmenFrame Apply(OmenFrame omen, int level)
        {
            if (omen == null) throw new ArgumentNullException(nameof(omen));
            omen.Disclosure = Clamp(level);
            if (!ShowsNumbers(level))
            {
                omen.ValueText = "";
                omen.PlanValueText = "";
                omen.Statuses.Clear();
                omen.PlanStatuses.Clear();
            }
            if (!ShowsPlan(level)) omen.PlanVisible = false;
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
