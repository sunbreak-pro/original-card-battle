using System;
using System.Collections.Generic;

namespace BattleCore
{
    // Battle core v3 (battle_core_v3.md, 2026-09-12). C# is the source of truth.
    // The TS battle-lab core is frozen; nothing here mirrors it any more.

    public enum RangeBand
    {
        Close,
        Mid,
        Far,
    }

    public enum CardType
    {
        Attack,
        Move,
        Guard,
        Heal,
    }

    public enum CardDefId
    {
        Thrust,
        Lunge,
        Feint,
        StepIn,
        StepOut,
        Brace,
        FirstAid,
    }

    public enum EnemyActionId
    {
        Sweep,
        ReachThrust,
        Shove,
        Reposition,
        GuardUp,
    }

    public enum GameResult
    {
        Ongoing,
        Won,
        Lost,
    }

    public enum Actor
    {
        Player,
        Enemy,
    }

    /// <summary>Reserve rules (§3.5): Calm = bonus recovery next turn when enough stamina is left after use; Desperate = ×1.5 when current stamina is ≤ 2.</summary>
    public enum ReserveKind
    {
        Calm,
        Desperate,
    }

    public static class EnumTokens
    {
        public static string ToToken(this RangeBand band) => band switch
        {
            RangeBand.Close => "close",
            RangeBand.Mid => "mid",
            RangeBand.Far => "far",
            _ => throw new ArgumentOutOfRangeException(nameof(band), band, null),
        };

        public static string ToToken(this CardType type) => type switch
        {
            CardType.Attack => "attack",
            CardType.Move => "move",
            CardType.Guard => "guard",
            CardType.Heal => "heal",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

        public static string ToToken(this CardDefId defId) => defId switch
        {
            CardDefId.Thrust => "thrust",
            CardDefId.Lunge => "lunge",
            CardDefId.Feint => "feint",
            CardDefId.StepIn => "step_in",
            CardDefId.StepOut => "step_out",
            CardDefId.Brace => "brace",
            CardDefId.FirstAid => "first_aid",
            _ => throw new ArgumentOutOfRangeException(nameof(defId), defId, null),
        };

        public static string ToToken(this EnemyActionId id) => id switch
        {
            EnemyActionId.Sweep => "sweep",
            EnemyActionId.ReachThrust => "reach_thrust",
            EnemyActionId.Shove => "shove",
            EnemyActionId.Reposition => "reposition",
            EnemyActionId.GuardUp => "guard_up",
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, null),
        };

        public static string ToToken(this GameResult result) => result switch
        {
            GameResult.Ongoing => "ongoing",
            GameResult.Won => "won",
            GameResult.Lost => "lost",
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
        };
    }

    // ---- Card / enemy definitions (§7 / §8) ----

    /// <summary>Performance at one invest level (T0..T3). Shift: negative closes in, positive backs off.</summary>
    public sealed record Tier(
        int Power = 0,
        int Guard = 0,
        int Heal = 0,
        int Shift = 0,
        int BreakStamina = 0);

    /// <summary>Card-level reserve rule. Calm: remaining stamina after use ≥ Threshold → +Bonus recovery next turn. Desperate: current stamina ≤ DesperateThreshold → power × DesperateMult.</summary>
    public sealed record ReserveRule(ReserveKind Kind, int Threshold, int Bonus = 1);

    public sealed record CardDef(
        CardDefId Id,
        string Name,
        CardType Type,
        RangeBand? EffectiveRange,
        int MinInvest,
        IReadOnlyList<Tier> Tiers,
        ReserveRule? Reserve,
        string Description);

    /// <summary>A card in a deck: definition + unique instance id ("thrust-0").</summary>
    public sealed record CardInstance(string InstanceId, CardDef Def);

    public sealed record EnemyAction(
        EnemyActionId Id,
        string Name,
        CardType Type,
        RangeBand? EffectiveRange,
        int MinInvest,
        IReadOnlyList<Tier> Tiers,
        int Shift,
        bool TowardHome,
        string Description);

    public sealed record EnemyDef(
        string Name,
        int MaxHp,
        int MaxStamina,
        RangeBand HomeRange,
        IReadOnlyDictionary<RangeBand, IReadOnlyList<EnemyActionId>> DecisionTree,
        IReadOnlyDictionary<EnemyActionId, EnemyAction> Actions);

    /// <summary>The enemy's declared next action (§6.1). TargetRange is the attack's effective range; null for guard / move.</summary>
    public sealed record Omen(EnemyActionId ActionId, RangeBand? TargetRange);

    // ---- Battle state ----

    public sealed record LogEntry(int Id, string Text);

    /// <summary>
    /// Inputs the exploration layer hands to a battle (§1.1: max stamina is external; HP and stamina carry over).
    /// Floor / miasma / time-limit / disclosure are HUD-only inside a battle and never change here.
    /// </summary>
    public sealed record BattleInit(
        int PlayerMaxStamina = Constants.BaseMaxStamina,
        int? PlayerStamina = null,
        int PlayerHp = Constants.PlayerMaxHp,
        int PlayerMaxHp = Constants.PlayerMaxHp,
        int InitialDistanceIndex = Constants.InitialDistanceIndex,
        int Floor = 1,
        int MiasmaPercent = 0,
        int MiasmaDensity = 1,
        int TimeLimitLeft = 10,
        int TimeLimitMax = 10,
        int Disclosure = 1);

    public sealed record BattleState(
        int Turn,
        int DistanceIndex,
        int PlayerHp,
        int PlayerMaxHp,
        int PlayerStamina,
        int PlayerMaxStamina,
        int PlayerGuard,
        int PendingBonusRecovery,
        int EnemyHp,
        int EnemyMaxHp,
        int EnemyStamina,
        int EnemyMaxStamina,
        int EnemyGuard,
        Omen? Omen,
        IReadOnlyList<CardInstance> Hand,
        IReadOnlyList<CardInstance> DrawPile,
        IReadOnlyList<CardInstance> DiscardPile,
        IReadOnlyList<LogEntry> Log,
        int LogSeq,
        IReadOnlyList<BattleEvent> Events,
        GameResult Result,
        BattleInit Init);

    // ---- Actions ----

    public abstract record BattleAction;

    /// <summary>Play a card from hand with an invest of MinInvest..MaxInvest (§3.4).</summary>
    public sealed record PlayCardAction(string InstanceId, int Invest) : BattleAction;

    public sealed record EndTurnAction : BattleAction;

    public sealed record RestartAction : BattleAction;

    // ---- Events (what happened during the last dispatch; the View animates from these) ----

    public abstract record BattleEvent;

    public sealed record TurnStartedEvent(int Turn, RangeBand Band, int Recovery, int BonusRecovery) : BattleEvent;

    public sealed record CardsDrawnEvent(int Count) : BattleEvent;

    public sealed record HandDiscardedEvent(int Count) : BattleEvent;

    public sealed record CardPlayedEvent(string InstanceId, CardDefId DefId, string Name, int Invest) : BattleEvent;

    /// <summary>Diff ≥ WhiffDiff means "ほぼ空振り" (no hit flash). GuardAbsorbed + Damage == Raw.</summary>
    public sealed record AttackResolvedEvent(
        Actor Attacker,
        string Name,
        int Power,
        double Mult,
        int Diff,
        bool Desperate,
        int Raw,
        int GuardAbsorbed,
        int Damage,
        int TargetHpAfter) : BattleEvent;

    public sealed record MovedEvent(Actor Mover, int From, int To, bool Clamped) : BattleEvent;

    public sealed record GuardGainedEvent(Actor Who, int Amount, int Total, string Source) : BattleEvent;

    public sealed record HealedEvent(Actor Who, int Amount, int HpAfter) : BattleEvent;

    public sealed record StaminaBrokenEvent(Actor Target, int Amount, int StaminaAfter) : BattleEvent;

    /// <summary>構え (§3.5): stamina ≥ ReserveThreshold at turn end → Guard +ReserveGuard.</summary>
    public sealed record ReserveGuardEvent(Actor Who, int Remaining, int Amount) : BattleEvent;

    public sealed record CalmTriggeredEvent(string CardName, int Bonus) : BattleEvent;

    public sealed record EnemyPhaseStartedEvent(RangeBand Band, int Recovery) : BattleEvent;

    public sealed record OmenExecutedEvent(EnemyActionId ActionId, string Name, int Invest) : BattleEvent;

    /// <summary>§6.2: the declared attack would land at diff ≥ 2, so the enemy drops it and repositions instead.</summary>
    public sealed record OmenWhiffedEvent(EnemyActionId ActionId, string Name) : BattleEvent;

    public sealed record EnemyRestedEvent : BattleEvent;

    public sealed record OmenDeclaredEvent(Omen Omen) : BattleEvent;

    public sealed record BattleEndedEvent(GameResult Result) : BattleEvent;
}
