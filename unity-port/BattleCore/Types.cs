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
    /// §2.4 / §7.4 targets. One is the opponent-directed default (the player picks one enemy on the
    /// drop zone); All is every enemy inside the reach; Self reads no reach. v4.3's cell left with
    /// one enemy per cell (2026-09-23, #169); ally comes with the status words that use it (#48).
    /// </summary>
    public enum TargetKind
    {
        One,
        Self,
        All,
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

    /// <summary>
    /// §5 の状態. The demo (#188) carries nine of the ten words; 俊敏 is left out because no card
    /// and no enemy gives it. The order is the order the chips are listed in.
    /// </summary>
    public enum StatusKind
    {
        /// <summary>鈍足: the holder's own cell changes (move, push, pull) are one cell shorter. OnTurn.</summary>
        Slow,

        /// <summary>出血: HP −2 × stacks at the holder's turn start. OnTurn.</summary>
        Bleed,

        /// <summary>脆化: the next attack the holder takes is ×1.5. OnUse.</summary>
        Fragile,

        /// <summary>威圧: the holder's next action has 3 less power and 3 less Guard. OnUse.</summary>
        Intimidate,

        /// <summary>疲労: the holder recovers 1 less stamina at its turn start. OnTurn.</summary>
        Fatigue,

        /// <summary>強化: the holder's next attack face is ×1.5. OnUse.</summary>
        Empower,

        /// <summary>集中: the holder's next card resolves one column to the right (demo approximation). OnUse.</summary>
        Focus,

        /// <summary>見切り: the next hit the holder's Guard absorbs returns half of it. OnUse.</summary>
        Parry,

        /// <summary>再生: HP +2 × stacks at the holder's turn start. OnTurn.</summary>
        Regen,
    }

    /// <summary>
    /// One status a face or a trait gives: the word, its stacks, and whether it lands on the one who
    /// played it (自分に) or on the opponent (相手に, which reads the reach).
    /// </summary>
    public sealed record StatusGrant(StatusKind Kind, int Stacks = Constants.StatusApplyDefault, bool OnSelf = false);

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
    /// §2.3 の条件: the twelve words, plus 無防備 for enemy actions (§17.6 F1). 間合い is two rows
    /// (at most / at least). The demo (#188) carries them at the precision a playable battle needs;
    /// #48 keeps the tests that pin every pairing.
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

        /// <summary>連動(X): something with <see cref="Trait.Attribute"/> was played earlier this turn (an enemy: this phase).</summary>
        Combo,

        /// <summary>予兆(種別): the opponent's standing omen is <see cref="Trait.Omen"/>. Player cards only.</summary>
        OmenIs,

        /// <summary>死力: the stamina before paying is at most DESPERATE_THRESHOLD (2).</summary>
        Desperate,

        /// <summary>初手: the first card played this turn.</summary>
        FirstPlay,

        /// <summary>締め: the third card played this turn, or later.</summary>
        Finisher,

        /// <summary>崩し後: the opponent's stamina is below 3.</summary>
        Broken,

        /// <summary>連打: shares an attribute with the card played just before it this turn.</summary>
        Chain,

        /// <summary>手薄: the hand left after this card is played holds at most THIN_HAND (2; decided 2026-09-23, #193).</summary>
        Thin,

        /// <summary>相手の状態(語): the opponent holds <see cref="Trait.Watch"/>.</summary>
        FoeHas,

        /// <summary>自分の状態(語): the one playing holds <see cref="Trait.Watch"/>.</summary>
        SelfHas,
    }

    /// <summary>§2.3 の効果: the ten words, plus 崩し +1 for enemy actions (roster §1.6).</summary>
    public enum TraitEffect
    {
        PowerBonus,
        GuardBonus,
        NextTurnRecovery,

        /// <summary>重撃: power +6 now, recovery −1 at the next turn start. Cost and 構え are untouched.</summary>
        HeavyBlow,

        /// <summary>スタミナ +n, at once.</summary>
        StaminaGain,

        /// <summary>ドロー +n, drawn with the face's own draws.</summary>
        Draw,

        /// <summary>状態: <see cref="Trait.Grant"/> lands with the skill face.</summary>
        Status,

        /// <summary>コスト −n (floor 0). CanPlay and Preview read it, so a card it makes payable can be played.</summary>
        CostDown,

        /// <summary>転換: half of this card's Guard face (rounded up) is added to its power.</summary>
        Convert,

        /// <summary>追撃: the next attack face this turn (not this card's own) gets +5. Gone at turn end (§17.6 F7).</summary>
        FollowUp,

        /// <summary>崩し +n (enemy actions, roster §1.6): the face's break gets n more.</summary>
        BreakBonus,
    }

    /// <summary>
    /// §2.3: one card or action carries at most one trait (背水の陣 is the one card with two, see
    /// <see cref="CardDef.ExtraTrait"/>), and a trait is one condition paired with one effect.
    /// Threshold is the gap for 間合い and the stamina bar for 温存. Attribute is 連動's, Omen
    /// 予兆's, Watch the word 相手の状態 / 自分の状態 look for, and Grant the status the Status
    /// effect gives.
    /// </summary>
    public sealed record Trait(
        TraitCondition Condition,
        TraitEffect Effect,
        int Amount = 0,
        int Threshold = 0,
        BattleAttribute Attribute = BattleAttribute.None,
        OmenKind? Omen = null,
        StatusKind? Watch = null,
        StatusGrant? Grant = null);

    /// <summary>
    /// §4: when a stance takes effect. The six shapes of §4 cut down to the ones the 14 stance cards
    /// and the enemy stances use (#188 / #189).
    /// </summary>
    public enum StanceHook
    {
        /// <summary>ターン開始付与: Guard and / or recovery at the holder's turn start (§9 steps 2-3 / 9).</summary>
        TurnStart,

        /// <summary>At the holder's turn end, before 構え: Guard and / or next-turn recovery.</summary>
        TurnEnd,

        /// <summary>条件付き加算: +Power on the holder's attack faces while the condition holds for the foe hit.</summary>
        AttackBonus,

        /// <summary>被弾時の反応: each time an attack lands on the holder.</summary>
        OnHit,

        /// <summary>The holder does not take push or pull (錨の構え).</summary>
        PushImmune,

        /// <summary>Each time an enemy moves itself, it loses Break stamina (根縛り).</summary>
        BreakOnFoeMove,
    }

    /// <summary>§4 `when`: the condition a stance effect waits for.</summary>
    public enum StanceWhen
    {
        Always,

        /// <summary>The gap is at least Threshold: the nearest enemy's for the player, the player's for an enemy.</summary>
        GapAtLeast,

        /// <summary>The gap is at most Threshold (for AttackBonus: the gap to the foe hit).</summary>
        GapAtMost,

        /// <summary>A standing enemy's omen is an attack (見切りの目). Player only.</summary>
        OmenAttack,

        /// <summary>Something with the move attribute was played earlier this turn (流れの構え).</summary>
        MovedThisTurn,

        /// <summary>The foe hit holds <see cref="StanceDef.Status"/> (狼の構え).</summary>
        TargetHasStatus,
    }

    /// <summary>
    /// §4: the stance a stance face sets into the holder's one slot. The numbers are the card's
    /// own (its column already decided them), and the slot keeps them until the battle ends or
    /// another stance replaces it. Status is the word 狼の構え watches, or the word 槍衾 gives the
    /// attacker; OncePerTurn limits an OnHit stance to one reaction per turn (司祭の祈り).
    /// </summary>
    public sealed record StanceDef(
        StanceHook Hook,
        StanceWhen When = StanceWhen.Always,
        int Threshold = 0,
        int Guard = 0,
        int Recovery = 0,
        int NextRecovery = 0,
        int Power = 0,
        int Stamina = 0,
        StatusKind? Status = null,
        int StatusStacks = 0,
        int Break = 0,
        bool OncePerTurn = false);

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
    ///
    /// #188 adds Heal (HP to the one playing), Break (崩し: the opponent's stamina −n, riding the
    /// attack face), the status list (each to self or to the opponent) and the stance a stance face
    /// sets. Statuses is null for none; read it through <see cref="StatusList"/>.
    ///
    /// Hits (§2.4 `hits`, #189) splits the attack face into that many blows, each against Guard on
    /// its own; the opponent statuses of a face with two or more land after the first blow
    /// (二段斬り's 脆化 is there for the second).
    /// </summary>
    public sealed record Face(
        int Power = 0,
        int Guard = 0,
        int Move = 0,
        int Push = 0,
        int Draw = 0,
        int StaminaGain = 0,
        Reach? Reach = null,
        IReadOnlyList<StatusGrant>? Statuses = null,
        int Heal = 0,
        int Break = 0,
        StanceDef? Stance = null,
        int Hits = 1)
    {
        public Reach ReachOrDefault => Reach ?? Reach.Default;

        public IReadOnlyList<StatusGrant> StatusList => Statuses ?? Array.Empty<StatusGrant>();

        /// <summary>Whether any status of this face lands on the opponent (and so reads the reach).</summary>
        public bool GivesFoeStatus
        {
            get
            {
                foreach (var grant in StatusList)
                {
                    if (!grant.OnSelf) return true;
                }
                return false;
            }
        }
    }

    /// <summary>
    /// §3: a card picks one column of 1-4 up front. The column number is the cost and that column's
    /// values are the effect, so there is no invest choice at play time any more.
    ///
    /// ExtraTrait is the second trait of 背水の陣 (#80), the one card the canon lets carry two
    /// (swordsman_cards_v4 §3). Both are judged at once and their bonuses add up.
    /// </summary>
    public sealed record CardDef(
        string Id,
        string Name,
        BattleAttribute Attributes,
        int Column,
        Face Face,
        Trait? Trait = null,
        TargetKind Targets = TargetKind.One,
        string Description = "",
        Trait? ExtraTrait = null)
    {
        public int Cost => Columns.CostOf(Column);

        /// <summary>The traits in the order they are judged: <see cref="Trait"/>, then <see cref="ExtraTrait"/>.</summary>
        public IReadOnlyList<Trait> AllTraits
        {
            get
            {
                var traits = new List<Trait>(2);
                if (Trait != null) traits.Add(Trait);
                if (ExtraTrait != null) traits.Add(ExtraTrait);
                return traits;
            }
        }
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
        string Description = "",
        OmenKind? Omen = null)
    {
        public int Cost => Columns.CostOf(Column);
    }

    /// <summary>roster §1.1 `rank`: what kind of fight the enemy is. Elites and bosses act twice a phase.</summary>
    public enum EnemyRank
    {
        Normal,
        Elite,
        Boss,
    }

    /// <summary>
    /// §6.1: every enemy branches three ways on the gap band. Each branch is an ordered list of
    /// action ids; the first affordable one becomes the omen. Size is the cells the enemy uses
    /// (§7.1, 1〜3); a size of 2 or more refuses push and pull (§7.3). ActionsPerPhase is 2 for
    /// elites and bosses (roster §1.3, #189); their adaptation is #50.
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
        IReadOnlyDictionary<string, EnemyActionDef> Actions,
        EnemyRank Rank = EnemyRank.Normal,
        int ActionsPerPhase = 1)
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
    ///
    /// #188: Stance is the one stance slot (§4) and StanceSource the card or action that set it.
    /// FollowUp is the 追撃 waiting for the next attack face (§17.6 F7: gone at the end of the
    /// holder's turn). Played lists the attributes of what the holder played this turn, in order
    /// (§2.3 `playedAttributes`; 連動 / 初手 / 締め / 連打 read it), cleared at turn end.
    /// StanceReactedTurn is the turn an OncePerTurn stance last reacted in.
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
        int NextTurnRecoveryBonus = 0,
        StanceDef? Stance = null,
        string? StanceSource = null,
        int FollowUp = 0,
        IReadOnlyList<BattleAttribute>? Played = null,
        int StanceReactedTurn = 0)
    {
        /// <summary>The rightmost cell used (equal to Cell for size 1).</summary>
        public int FarCell => Cell + Size - 1;

        /// <summary>The attributes played this turn, in order; empty at the start of a turn.</summary>
        public IReadOnlyList<BattleAttribute> PlayedThisTurn => Played ?? Array.Empty<BattleAttribute>();
    }

    /// <summary>
    /// §7.4: one enemy on the field — what it is, where and how it stands, and the omen it has
    /// declared. A fallen enemy stays in <see cref="BattleState.Enemies"/> (so the numbers events
    /// carry never shift) but leaves its cells and its omen: see <see cref="Alive"/>.
    /// </summary>
    public sealed record EnemyUnit(EnemyDef Def, CombatantState Body, Omen? Omen, IReadOnlyList<string>? SpentStances = null)
    {
        public bool Alive => !Combat.IsDefeated(Body.Hp);

        /// <summary>
        /// roster §1.2 (#189): the stance actions this enemy has used. Each is used once a battle and
        /// then leaves its tree.
        /// </summary>
        public IReadOnlyList<string> Spent => SpentStances ?? Array.Empty<string>();
    }

    /// <summary>
    /// The whole battle. <see cref="TurnLoop"/> moves it forward; this record only fixes what the
    /// loop may carry. The event stream is handed back beside the state, not kept inside it.
    /// FieldCells is the width of the line (§7.1), handed in per battle.
    ///
    /// Enemies (§7.4, #47) holds one to three, in definition order — which is also their phase
    /// order and the index every event's <see cref="BattleEvent.Unit"/> means. Each stands on its
    /// own cells (`CELL_CAPACITY` = 1). <see cref="Enemy"/>, <see cref="EnemyDef"/>,
    /// <see cref="Omen"/> and <see cref="Gap"/> read the first one: the whole of a one-enemy battle,
    /// such as the vertical slice.
    /// </summary>
    public sealed record BattleState(
        int Turn,
        int FieldCells,
        CombatantState Player,
        IReadOnlyList<EnemyUnit> Enemies,
        IReadOnlyList<CardInstance> Hand,
        IReadOnlyList<CardInstance> DrawPile,
        IReadOnlyList<CardInstance> DiscardPile,
        GameResult Result = GameResult.Ongoing,
        BattlePhase Phase = BattlePhase.AwaitingTurnStart,
        IReadOnlyList<CardInstance>? ExilePile = null)
    {
        /// <summary>§4 (#188): the stance cards played this battle. They never go back into the deck.</summary>
        public IReadOnlyList<CardInstance> Exiled => ExilePile ?? Array.Empty<CardInstance>();

        /// <summary>The first enemy's body.</summary>
        public CombatantState Enemy => Enemies[0].Body;

        /// <summary>The first enemy's definition.</summary>
        public EnemyDef EnemyDef => Enemies[0].Def;

        /// <summary>The first enemy's standing omen.</summary>
        public Omen? Omen => Enemies[0].Omen;

        /// <summary>§7.2: the empty cells between the player and the first enemy (0 when adjacent).</summary>
        public int Gap => GapTo(0);

        /// <summary>§7.2: N to one enemy, counted from its near edge.</summary>
        public int GapTo(int unit) => Field.GapBetween(Player, Enemies[unit].Body);

        /// <summary>The standing enemies' indices, in definition order.</summary>
        public IReadOnlyList<int> Living
        {
            get
            {
                var living = new List<int>();
                for (int i = 0; i < Enemies.Count; i++)
                {
                    if (Enemies[i].Alive) living.Add(i);
                }
                return living;
            }
        }

        /// <summary>
        /// §7.4: the standing enemy nearest the player (lowest cell), whose N a self-directed card
        /// reads. −1 once none is standing.
        /// </summary>
        public int Nearest
        {
            get
            {
                int nearest = -1;
                foreach (int i in Living)
                {
                    if (nearest < 0 || Enemies[i].Body.Cell < Enemies[nearest].Body.Cell) nearest = i;
                }
                return nearest;
            }
        }

        public BattleState WithEnemy(int unit, CombatantState body) => WithUnit(unit, Enemies[unit] with { Body = body });

        /// <summary>The first enemy's body replaced; the one-enemy shorthand tests and the slice use.</summary>
        public BattleState WithEnemy(CombatantState body) => WithEnemy(0, body);

        public BattleState WithOmen(int unit, Omen? omen) => WithUnit(unit, Enemies[unit] with { Omen = omen });

        public BattleState WithUnit(int unit, EnemyUnit value)
        {
            var enemies = new List<EnemyUnit>(Enemies);
            enemies[unit] = value;
            return this with { Enemies = enemies };
        }
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
            StatusKind.Bleed => "bleed",
            StatusKind.Fragile => "fragile",
            StatusKind.Intimidate => "intimidate",
            StatusKind.Fatigue => "fatigue",
            StatusKind.Empower => "empower",
            StatusKind.Focus => "focus",
            StatusKind.Parry => "parry",
            StatusKind.Regen => "regen",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        public static string ToLabel(this StatusKind kind) => kind switch
        {
            StatusKind.Slow => "鈍足",
            StatusKind.Bleed => "出血",
            StatusKind.Fragile => "脆化",
            StatusKind.Intimidate => "威圧",
            StatusKind.Fatigue => "疲労",
            StatusKind.Empower => "強化",
            StatusKind.Focus => "集中",
            StatusKind.Parry => "見切り",
            StatusKind.Regen => "再生",
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
