using System;
using System.Collections.Generic;

namespace BattleCore
{
    // Battle core v4.3 (battle_document/battle_core_v4.md, 2026-09-22). C# is the source of truth.
    //
    // This file holds the vertical slice's bones (#69): the values a battle carries (§1), the
    // attribute / face / trait shape that cards and enemy actions share (§2, §17.6 F5), and the
    // state record the turn loop (#72) drives. It carries no data: the ten prototype cards are
    // #71 and the polearm's actions are #70.
    //
    // v4.3 (#162): the two-valued Position (近間 / 遠間) became a cell on a line (§7.1) and the
    // distance N between the two sides (§7.2). Faces carry a reach (§2.4), move by cells (§7.3) and
    // push / pull by cells; the enemy tree branches on the gap band (§6.1). v4.2 stays in git history.

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

    /// <summary>
    /// §2.4 targets. One is the opponent-directed default; Self reads no reach. cell / all are
    /// §7.4 and arrive with several enemies (#52); ally is the retinue's (#52 too).
    /// </summary>
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

    /// <summary>§6.1: the three branches every enemy tree has, keyed on the gap N when the omen is decided.</summary>
    public enum GapBand
    {
        /// <summary>間合い 0: the two sides are adjacent.</summary>
        Zero,

        /// <summary>間合い 1〜2.</summary>
        OneToTwo,

        /// <summary>間合い 3 以上.</summary>
        ThreePlus,
    }

    /// <summary>
    /// §2.3 の条件. The slice carries four of the twelve words: 間合い in both directions (the one
    /// threshold a card may carry), 無防備 (enemy actions) and 温存. #48 adds rows here without
    /// changing the shape of Traits.Evaluate.
    /// </summary>
    public enum TraitCondition
    {
        /// <summary>間合い n 以下: the gap to the opponent, read before the card is played (§2.3).</summary>
        GapAtMost,

        /// <summary>間合い n 以上.</summary>
        GapAtLeast,

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
    /// one effect. Threshold is the gap for 間合い and the stamina bar for 温存.
    /// </summary>
    public sealed record Trait(
        TraitCondition Condition,
        TraitEffect Effect,
        int Amount = 0,
        int Threshold = 0);

    /// <summary>
    /// §2.4 / §7.2: the gaps a face lands at, both ends inclusive. A card cannot be played at an
    /// opponent outside it; an enemy action whiffs (§6). The default is 0〜1 (`REACH_DEFAULT`).
    /// </summary>
    public sealed record Reach(int Min, int Max)
    {
        public static readonly Reach Default = new Reach(0, 1);

        public static Reach Only(int gap) => new Reach(gap, gap);

        public bool Contains(int gap) => gap >= Min && gap <= Max;

        /// <summary>As printed on the omen badge and the card: "0〜1", or "0" when one gap only.</summary>
        public string ToText() => Min == Max ? Min.ToString() : $"{Min}〜{Max}";
    }

    /// <summary>
    /// §2.4: what one column of a card or action does. Cards and enemy actions share this table
    /// (a CardDef and an EnemyActionDef are written from the same face rows), which is why #70 and
    /// #71 add data without adding types.
    ///
    /// Move is the move face in cells: positive is 前へ (toward the opponent), negative is 後ろへ
    /// (§7.3). Push moves the opponent by cells: positive pushes them away, negative pulls them in;
    /// Guard does not reduce it (§2.4). Reach is null for the default 0〜1; read it through
    /// <see cref="ReachOrDefault"/>. Both are at most `MOVE_STEP_MAX` in size.
    /// </summary>
    public sealed record Face(
        int Power = 0,
        int Guard = 0,
        StatusKind? Status = null,
        int StatusStacks = 0,
        int Move = 0,
        int Push = 0,
        int Draw = 0,
        int StaminaGain = 0,
        Reach? Reach = null)
    {
        public Reach ReachOrDefault => Reach ?? Reach.Default;
    }

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

    /// <summary>
    /// §6: 予兆は「種別 + 狙うマス」. Reach is the action's reach, counted from the enemy's current
    /// cell; null for an action with no opponent-directed face. The screen turns it into cells.
    /// </summary>
    public sealed record OmenLabel(OmenKind Kind, Reach? Reach = null);

    /// <summary>The declared next enemy action (§6). Committed: it is shown, then executed as shown.</summary>
    public sealed record Omen(string ActionId, OmenLabel Label);

    /// <summary>
    /// One enemy action. Same Column / Face / Trait / Targets rows as CardDef. The omen label is
    /// derived (<see cref="EnemyAi.LabelOf"/>): with the one-character side gone (v4.3), kind and
    /// reach are both on the action itself, so there is nothing left to author separately.
    /// </summary>
    public sealed record EnemyActionDef(
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

    /// <summary>
    /// §6.1: every enemy branches three ways on the gap band. Each branch is an ordered list of
    /// action ids; the first affordable one becomes the omen. Size is the cells the enemy uses
    /// (§7.1, 1〜3); a size of 2 or more refuses push and pull (§7.3). Elites and bosses (two
    /// actions, adaptation) are #50.
    /// </summary>
    public sealed record EnemyDef(
        string Id,
        string Name,
        int MaxHp,
        int MaxStamina,
        int Recovery,
        int Size,
        IReadOnlyList<string> BranchAtGapZero,
        IReadOnlyList<string> BranchAtGapOneToTwo,
        IReadOnlyList<string> BranchAtGapThreePlus,
        IReadOnlyDictionary<string, EnemyActionDef> Actions)
    {
        public IReadOnlyList<string> Branch(GapBand band) => band switch
        {
            GapBand.Zero => BranchAtGapZero,
            GapBand.OneToTwo => BranchAtGapOneToTwo,
            GapBand.ThreePlus => BranchAtGapThreePlus,
            _ => throw new ArgumentOutOfRangeException(nameof(band), band, null),
        };
    }

    /// <summary>
    /// §1 の「戦闘中に持つ値」for one side. Cell is the leftmost cell the combatant uses (§7.1; the
    /// player is on the left, the enemy on the right, so the enemy's Cell is its near edge) and
    /// Size the cells it uses. NextTurnRecoveryBonus is what 温存 leaves behind for the next turn
    /// start (§9 step 2).
    /// </summary>
    public sealed record CombatantState(
        int Hp,
        int MaxHp,
        int Stamina,
        int MaxStamina,
        int Guard,
        int Cell,
        int Size,
        StatusSet Statuses,
        int NextTurnRecoveryBonus = 0)
    {
        /// <summary>The rightmost cell used (equal to Cell for size 1).</summary>
        public int FarCell => Cell + Size - 1;
    }

    /// <summary>
    /// The whole battle. <see cref="TurnLoop"/> moves it forward; this record only fixes what the
    /// loop may carry. The event stream is handed back beside the state, not kept inside it.
    /// FieldCells is the width of the line (§7.1).
    /// </summary>
    public sealed record BattleState(
        int Turn,
        int FieldCells,
        CombatantState Player,
        CombatantState Enemy,
        EnemyDef EnemyDef,
        Omen? Omen,
        IReadOnlyList<CardInstance> Hand,
        IReadOnlyList<CardInstance> DrawPile,
        IReadOnlyList<CardInstance> DiscardPile,
        GameResult Result = GameResult.Ongoing,
        BattlePhase Phase = BattlePhase.AwaitingTurnStart)
    {
        /// <summary>§7.2: the empty cells between the player and the enemy (0 when adjacent).</summary>
        public int Gap => Field.GapBetween(Player, Enemy);
    }

    public static class EnumTokens
    {
        /// <summary>§6.1: which branch a gap falls in.</summary>
        public static GapBand ToBand(this int gap)
        {
            if (gap < 0) throw new ArgumentOutOfRangeException(nameof(gap), gap, "A gap is never negative.");
            if (gap == 0) return GapBand.Zero;
            return gap <= 2 ? GapBand.OneToTwo : GapBand.ThreePlus;
        }

        public static string ToToken(this GapBand band) => band switch
        {
            GapBand.Zero => "gap0",
            GapBand.OneToTwo => "gap1-2",
            GapBand.ThreePlus => "gap3+",
            _ => throw new ArgumentOutOfRangeException(nameof(band), band, null),
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
