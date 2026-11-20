using System;

namespace DungeonContent
{
    /// <summary>
    /// What a carried item does. Every value here is something the 80% build can already
    /// honour: nothing depends on hidden rooms, shortcuts or traps, which are postponed
    /// (dungeon_exploration_v4.md §2.2), and nothing touches the frozen armour economy.
    /// </summary>
    public enum ItemEffect
    {
        /// <summary>The entry node of every layer costs no day (日読みの砂, tools_and_consumables_v4.md §2.4).</summary>
        EntryDayFree,

        /// <summary>A life that comes back alive hands over to the next heir in 1 day instead of 3 (帰り道の糸, §2.4).</summary>
        ShortHandover,

        /// <summary>The next new node costs no day (早駆けの丸薬, §3).</summary>
        NextStepFree,

        /// <summary>Lets the player pick which enemy a 情報収集 node raises to disclosure 2.</summary>
        ChooseSurveyTarget,

        /// <summary>Raises what 階層間の休憩 gives back, in points of the HP maximum.</summary>
        InterludeHeal,

        /// <summary>Restores HP, as a share of the maximum.</summary>
        HealHp,

        /// <summary>Raises max stamina for the rest of the life. Shares the ±4 clamp.</summary>
        MaxStamina,

        /// <summary>Refills current stamina.</summary>
        RefillStamina,

        /// <summary>Moves to any unresolved node of the current layer. Still costs one day.</summary>
        Teleport,

        /// <summary>
        /// Moves the player's starting cell back by Amount, widening the opening gap from the
        /// default 3 (seven_layers_v4.md §2.4). The field keeps its width.
        /// </summary>
        WiderStart,

        /// <summary>Adds stamina at the start of a battle.</summary>
        OpeningStamina,

        /// <summary>Grants Guard once per battle, the first time it would matter.</summary>
        OpeningGuard,

        /// <summary>Holds a boss's own state one stack lower than it would otherwise sit.</summary>
        BluntBossState,
    }

    /// <summary>Which core reads the effect. The slots are shared, the plumbing is not.</summary>
    public enum EffectSide
    {
        /// <summary>DungeonCore reads it: the calendar, nodes, the interlude.</summary>
        Exploration,

        /// <summary>BattleCore reads it: position, stamina, Guard, enemy states.</summary>
        Battle,
    }

    public static class ItemEffects
    {
        public static EffectSide Side(this ItemEffect effect) => effect switch
        {
            ItemEffect.EntryDayFree => EffectSide.Exploration,
            ItemEffect.ShortHandover => EffectSide.Exploration,
            ItemEffect.NextStepFree => EffectSide.Exploration,
            ItemEffect.ChooseSurveyTarget => EffectSide.Exploration,
            ItemEffect.InterludeHeal => EffectSide.Exploration,
            ItemEffect.HealHp => EffectSide.Exploration,
            ItemEffect.MaxStamina => EffectSide.Exploration,
            ItemEffect.RefillStamina => EffectSide.Exploration,
            ItemEffect.Teleport => EffectSide.Exploration,
            ItemEffect.WiderStart => EffectSide.Battle,
            ItemEffect.OpeningStamina => EffectSide.Battle,
            ItemEffect.OpeningGuard => EffectSide.Battle,
            ItemEffect.BluntBossState => EffectSide.Battle,
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, null),
        };

        /// <summary>Stable lowercase token, for saves and for the loadout screen's bindings.</summary>
        public static string ToToken(this ItemEffect effect) => effect switch
        {
            ItemEffect.EntryDayFree => "entry_day_free",
            ItemEffect.ShortHandover => "short_handover",
            ItemEffect.NextStepFree => "next_step_free",
            ItemEffect.ChooseSurveyTarget => "choose_survey_target",
            ItemEffect.InterludeHeal => "interlude_heal",
            ItemEffect.HealHp => "heal_hp",
            ItemEffect.MaxStamina => "max_stamina",
            ItemEffect.RefillStamina => "refill_stamina",
            ItemEffect.Teleport => "teleport",
            ItemEffect.WiderStart => "wider_start",
            ItemEffect.OpeningStamina => "opening_stamina",
            ItemEffect.OpeningGuard => "opening_guard",
            ItemEffect.BluntBossState => "blunt_boss_state",
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, null),
        };
    }
}
