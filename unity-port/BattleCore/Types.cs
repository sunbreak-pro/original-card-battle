using System;
using System.Collections.Generic;

namespace BattleCore
{
    // Battle core v4.2 (battle_document/battle_core_v4.md, 2026-09-21). C# is the source of truth.
    //
    // This file holds the vertical slice's bones (#69): the values a battle carries (§1), the
    // attribute / face / trait shape that cards and enemy actions share (§2, §17.6 F5), and the
    // state record the turn loop (#72) drives. It carries no data: the ten prototype cards are
    // #71 and the polearm's actions are #70.
    //
    // v3 (invest 0-3, three range bands, whiff avoidance) was replaced here rather than kept in a
    // second namespace, so there is one set of rules to read. v3 stays in git history.

    /// <summary>
    /// §2.1 の属性 5 つ. Flags because one card carries 1-2 of them (単属性 5 + 二属性 10 = 15 patterns).
    /// Named BattleAttribute, not Attribute, so it never shadows System.Attribute.
    /// </summary>
    [Flags]
    public enum BattleAttribute
    {
        None = 0,
        Attack = 1 << 0,
        Guard = 1 << 1,
        Move = 1 << 2,
        Skill = 1 << 3,
        Stance = 1 << 4,
    }

    /// <summary>§7.1: 位置. Two values, so one switch always lands on the other side.</summary>
    public enum Position
    {
        Near,
        Far,
    }

    public enum Actor
    {
        Player,
        Enemy,
    }

    public enum GameResult
    {
        Ongoing,
        Won,
        Lost,
    }

    /// <summary>
    /// §9: where the turn loop stands. It only ever waits in two places — before a player turn is
    /// opened (steps 1-5) and while the player plays cards (step 6). Steps 7-12 run in one go.
    /// </summary>
    public enum BattlePhase
    {
        AwaitingTurnStart,
        PlayerAction,
        Finished,
    }

    /// <summary>§2.4 targets. The slice uses one and self only; all is §17.6 F10 and ally is #52.</summary>
    public enum TargetKind
    {
        One,
        Self,
    }

    /// <summary>§6: 予兆の種別. Rest is the enemy outcome when the declared action cannot be paid for.</summary>
    public enum OmenKind
    {
        Attack,
        Guard,
        Move,
        Skill,
        Stance,
        Rest,
    }

    /// <summary>§5 の状態. The slice carries 鈍足 only; the other nine words are #48.</summary>
    public enum StatusKind
    {
        Slow,
    }

    /// <summary>§5: 減り方の 2 型.</summary>
    public enum StatusDecay
    {
        OnUse,
        OnTurn,
    }

    /// <summary>
    /// §2.3 の条件. The slice carries four of the twelve words: the three the polearm needs
    /// (位置(相手) / 無防備 / 温存) plus 位置(自分) for the player cards (#71). #48 adds rows here
    /// without changing the shape of Traits.Evaluate.
    /// </summary>
    public enum TraitCondition
    {
        /// <summary>位置（自分）: a player card reads its own side only (§17.3).</summary>
        SelfPosition,

        /// <summary>位置（相手）: enemy actions only (enemy_roster_v4.md §1.6).</summary>
        OpponentPosition,

        /// <summary>無防備: the opponent Guard is 0 (§17.6 F1). Not the card-side word 手薄.</summary>
        Unguarded,

        /// <summary>温存: the stamina left after paying the cost is at or above the threshold.</summary>
        Reserve,
    }

    /// <summary>
    /// §2.3 の効果. Four of the ten words; the rest are #48. 重撃 came in with the prototype deck
    /// (#71): every card in the canon that applies 鈍足 carries a trait outside the first three
    /// words, and 体当たり needs this one.
    /// </summary>
    public enum TraitEffect
    {
        PowerBonus,
        GuardBonus,
        NextTurnRecovery,

        /// <summary>重撃: power +6 now, recovery −1 at the next turn start. Cost and 構え are untouched.</summary>
        HeavyBlow,
    }

    /// <summary>
    /// §2.3: one card or action carries at most one trait, and a trait is one condition paired with
    /// one effect. ConditionPosition is the side a position condition asks for; Threshold is the
    /// 温存 bar.
    /// </summary>
    public sealed record Trait(
        TraitCondition Condition,
        TraitEffect Effect,
        int Amount = 0,
        Position? ConditionPosition = null,
        int Threshold = 0);

    /// <summary>
    /// §2.4: what one column of a card or action does. Cards and enemy actions share this table
    /// (a CardDef and an EnemyActionDef are written from the same face rows), which is why #70 and
    /// #71 add data without adding types. Push is the enemy-only 相手の位置を反転する (§2.4) and is
    /// not reduced by Guard.
    ///
    /// A move face is one of three (swordsman_cards_v4.md §1.1): 近間へ / 遠間へ set MoveTo, 反転 sets
    /// FlipsSelfPosition. A directed move played from the side it points at leaves the position alone.
    /// </summary>
    public sealed record Face(
        int Power = 0,
        int Guard = 0,
        StatusKind? Status = null,
        int StatusStacks = 0,
        bool FlipsSelfPosition = false,
        bool Push = false,
        int Draw = 0,
        int StaminaGain = 0,
        Position? MoveTo = null);

    /// <summary>
    /// §3: a card picks one column of 1-4 up front. The column number is the cost and that column's
    /// values are the effect, so there is no invest choice at play time any more.
    /// </summary>
    public sealed record CardDef(
        string Id,
        string Name,
        BattleAttribute Attributes,
        int Column,
        Face Face,
        Trait? Trait = null,
        TargetKind Targets = TargetKind.One,
        string Description = "")
    {
        public int Cost => Columns.CostOf(Column);
    }

    /// <summary>A card in a deck: definition plus a unique instance id (thrust-0).</summary>
    public sealed record CardInstance(string InstanceId, CardDef Def);

    /// <summary>§6: 予兆は「種別 + 咎める側の一字」. Side is null for actions that read no side.</summary>
    public sealed record OmenLabel(OmenKind Kind, Position? Side = null);

    /// <summary>The declared next enemy action (§6). Committed: it is shown, then executed as shown.</summary>
    public sealed record Omen(string ActionId, OmenLabel Label);

    /// <summary>
    /// One enemy action. Same Column / Face / Trait rows as CardDef; the omen label is authored per
    /// action rather than derived, because the roster table is the source of truth for it and
    /// deriving it would drop the one-character side (→ #116).
    /// </summary>
    public sealed record EnemyActionDef(
        string Id,
        string Name,
        BattleAttribute Attributes,
        int Column,
        Face Face,
        OmenLabel Omen,
        Trait? Trait = null,
        string Description = "")
    {
        public int Cost => Columns.CostOf(Column);
    }

    /// <summary>
    /// §6.1: an enemy without a position branches on the player's side (two branches). Each branch is
    /// an ordered list of action ids; the first affordable one becomes the omen. Elites and bosses
    /// branch four ways on both sides, which is #50.
    /// </summary>
    public sealed record EnemyDef(
        string Id,
        string Name,
        int MaxHp,
        int MaxStamina,
        int Recovery,
        bool HasPosition,
        Position? StartPosition,
        IReadOnlyList<string> BranchWhenPlayerNear,
        IReadOnlyList<string> BranchWhenPlayerFar,
        IReadOnlyDictionary<string, EnemyActionDef> Actions);

    /// <summary>
    /// §1 の「戦闘中に持つ値」for one side. Position is null for a combatant that carries none — the
    /// polearm does not (§7.1) — and null is what the trait evaluator reads as "no side to compare".
    /// NextTurnRecoveryBonus is what 温存 leaves behind for the next turn start (§9 step 2).
    /// </summary>
    public sealed record CombatantState(
        int Hp,
        int MaxHp,
        int Stamina,
        int MaxStamina,
        int Guard,
        Position? Position,
        StatusSet Statuses,
        int NextTurnRecoveryBonus = 0);

    /// <summary>
    /// The whole battle. <see cref="TurnLoop"/> moves it forward; this record only fixes what the
    /// loop may carry. The event stream is handed back beside the state, not kept inside it.
    /// </summary>
    public sealed record BattleState(
        int Turn,
        CombatantState Player,
        CombatantState Enemy,
        EnemyDef EnemyDef,
        Omen? Omen,
        IReadOnlyList<CardInstance> Hand,
        IReadOnlyList<CardInstance> DrawPile,
        IReadOnlyList<CardInstance> DiscardPile,
        GameResult Result = GameResult.Ongoing,
        BattlePhase Phase = BattlePhase.AwaitingTurnStart);

    public static class EnumTokens
    {
        /// <summary>§7.1: the one-character card at a combatant's feet.</summary>
        public static string ToLabel(this Position position) => position switch
        {
            Position.Near => "近",
            Position.Far => "遠",
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
        };

        public static Position Opposite(this Position position) =>
            position == Position.Near ? Position.Far : Position.Near;

        public static string ToToken(this Position position) => position switch
        {
            Position.Near => "near",
            Position.Far => "far",
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
        };

        public static string ToToken(this OmenKind kind) => kind switch
        {
            OmenKind.Attack => "attack",
            OmenKind.Guard => "guard",
            OmenKind.Move => "move",
            OmenKind.Skill => "skill",
            OmenKind.Stance => "stance",
            OmenKind.Rest => "rest",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        public static string ToToken(this StatusKind kind) => kind switch
        {
            StatusKind.Slow => "slow",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        public static string ToLabel(this StatusKind kind) => kind switch
        {
            StatusKind.Slow => "鈍足",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        public static string ToToken(this GameResult result) => result switch
        {
            GameResult.Ongoing => "ongoing",
            GameResult.Won => "won",
            GameResult.Lost => "lost",
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
        };
    }
}
