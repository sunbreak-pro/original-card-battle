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
        /// <summary>Lowers every layer's 瘴気 density for the whole life. Floored at 1.</summary>
        MiasmaDensity,

        /// <summary>Pulls the 瘴気 gauge back down once, by a flat percentage.</summary>
        MiasmaGauge,

        /// <summary>Adds 刻限 to every layer.</summary>
        TimeLimit,

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

        /// <summary>Moves to any unresolved node of the current layer. Still costs one 刻限.</summary>
        Teleport,

        /// <summary>Starts every battle at 遠間 instead of the default 近間.</summary>
        StartFar,

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
        /// <summary>DungeonCore reads it: 瘴気, 刻限, nodes, the interlude.</summary>
        Exploration,

        /// <summary>BattleCore reads it: position, stamina, Guard, enemy states.</summary>
        Battle,
    }

    public static class ItemEffects
    {
        public static EffectSide Side(this ItemEffect effect) => effect switch
        {
            ItemEffect.MiasmaDensity => EffectSide.Exploration,
            ItemEffect.MiasmaGauge => EffectSide.Exploration,
            ItemEffect.TimeLimit => EffectSide.Exploration,
            ItemEffect.ChooseSurveyTarget => EffectSide.Exploration,
            ItemEffect.InterludeHeal => EffectSide.Exploration,
            ItemEffect.HealHp => EffectSide.Exploration,
            ItemEffect.MaxStamina => EffectSide.Exploration,
            ItemEffect.RefillStamina => EffectSide.Exploration,
            ItemEffect.Teleport => EffectSide.Exploration,
            ItemEffect.StartFar => EffectSide.Battle,
            ItemEffect.OpeningStamina => EffectSide.Battle,
            ItemEffect.OpeningGuard => EffectSide.Battle,
            ItemEffect.BluntBossState => EffectSide.Battle,
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, null),
        };

        /// <summary>Stable lowercase token, for saves and for the loadout screen's bindings.</summary>
        public static string ToToken(this ItemEffect effect) => effect switch
        {
            ItemEffect.MiasmaDensity => "miasma_density",
            ItemEffect.MiasmaGauge => "miasma_gauge",
            ItemEffect.TimeLimit => "time_limit",
            ItemEffect.ChooseSurveyTarget => "choose_survey_target",
            ItemEffect.InterludeHeal => "interlude_heal",
            ItemEffect.HealHp => "heal_hp",
            ItemEffect.MaxStamina => "max_stamina",
            ItemEffect.RefillStamina => "refill_stamina",
            ItemEffect.Teleport => "teleport",
            ItemEffect.StartFar => "start_far",
            ItemEffect.OpeningStamina => "opening_stamina",
            ItemEffect.OpeningGuard => "opening_guard",
            ItemEffect.BluntBossState => "blunt_boss_state",
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, null),
        };
    }
}
