using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// What happened, in the order it happened. The turn loop hands these back beside the new state,
    /// and the depiction converter (#73) turns them into the script the screen plays.
    ///
    /// Every event carries settled values (the *After fields), never inputs to a formula: whoever
    /// reads the stream shows numbers and does not compute them. <see cref="BattleEvent.Actor"/> is
    /// the side the event is about.
    /// </summary>
    public abstract record BattleEvent(Actor Actor)
    {
        /// <summary>
        /// §7.4 (#47): which enemy (its index in <see cref="BattleState.Enemies"/>) is on the enemy
        /// end of this event — the enemy acting in its phase, or the enemy a player's card read or
        /// hit. 0 in a one-enemy battle and for what concerns the player alone.
        /// </summary>
        public int Unit { get; init; }
    }

    // ---- Turn frame (§9 steps 1-5, 7-8) ----

    public sealed record TurnStarted(Actor Actor, int Turn) : BattleEvent(Actor);

    /// <summary>§9 steps 1 and 9: Guard goes to 0 at the start of the holder's own turn.</summary>
    public sealed record GuardCleared(Actor Actor, int Cleared) : BattleEvent(Actor);

    /// <summary>§9 steps 2 and 9. Amount is what was actually gained once the maximum capped it.</summary>
    public sealed record StaminaRecovered(Actor Actor, int Amount, int StaminaAfter, int MaxStamina) : BattleEvent(Actor);

    /// <summary>§5: a ターンで減る型 word lost one stack at its holder's turn start.</summary>
    public sealed record StatusTicked(Actor Actor, StatusKind Kind, int StacksAfter) : BattleEvent(Actor);

    /// <summary>§9 step 4: one card, so a hand of five is five events.</summary>
    public sealed record Drawn(Actor Actor, CardInstance Card, int HandCountAfter) : BattleEvent(Actor);

    /// <summary>§8: the draw pile ran out and the discard pile was shuffled back in.</summary>
    public sealed record DeckReshuffled(Actor Actor, int DrawPileCount) : BattleEvent(Actor);

    /// <summary>
    /// §9 steps 5 and 12. Decided is true when the enemy has just chosen this omen (battle start and
    /// step 12) and false when step 5 shows the omen already standing.
    /// </summary>
    public sealed record OmenSet(Actor Actor, Omen Omen, bool Decided) : BattleEvent(Actor);

    /// <summary>§9 step 7 (and the enemy's own turn end): 構え. GuardGained is 3 or 0.</summary>
    public sealed record ReserveChecked(Actor Actor, int StaminaLeft, int GuardGained, int GuardAfter) : BattleEvent(Actor);

    public sealed record HandDiscarded(Actor Actor, int Count) : BattleEvent(Actor);

    public sealed record TurnEnded(Actor Actor, int Turn) : BattleEvent(Actor);

    // ---- One card or one action (§9 steps 6 and 10) ----

    /// <summary>The player released a card. GapBefore is the N every gap condition reads (§2.2 / §2.3).</summary>
    public sealed record CardPlayed(Actor Actor, CardInstance Card, int GapBefore) : BattleEvent(Actor);

    /// <summary>The enemy carried out its omen. GapBefore is the N at execution, the one the reach is judged on (§6).</summary>
    public sealed record ActionExecuted(Actor Actor, EnemyActionDef Action, int GapBefore) : BattleEvent(Actor);

    /// <summary>
    /// §6 空振り: the player stood outside the action's reach when it was carried out, so its
    /// opponent-directed faces did not resolve. The cost was paid and the self faces followed.
    /// </summary>
    public sealed record ActionWhiffed(Actor Actor, string SourceId, int Gap, Reach Reach) : BattleEvent(Actor);

    /// <summary>§6: the declared action could not be paid for, so the phase is recovery only.</summary>
    public sealed record Rested(Actor Actor, Omen Declared) : BattleEvent(Actor);

    public sealed record StaminaSpent(Actor Actor, int Amount, int StaminaAfter) : BattleEvent(Actor);

    /// <summary>§2.2: judged once, before any face. Only emitted for a card or action that carries a trait.</summary>
    public sealed record TraitEvaluated(Actor Actor, string SourceId, Trait Trait, TraitOutcome Outcome) : BattleEvent(Actor);

    /// <summary>One face of the card resolved. They come in §2.2 order: Attack, Move, Guard, Skill. Not emitted for a face a whiff skipped.</summary>
    public sealed record FaceResolved(Actor Actor, string SourceId, BattleAttribute Face) : BattleEvent(Actor);

    /// <summary>Actor is the attacker. Raw − Absorbed = Damage; Absorbed is what the target's Guard soaked.</summary>
    public sealed record DamageDealt(
        Actor Actor,
        Actor Target,
        int Raw,
        int Absorbed,
        int Damage,
        int TargetGuardAfter,
        int TargetHpAfter) : BattleEvent(Actor);

    public sealed record GuardGained(Actor Actor, int Amount, int GuardAfter) : BattleEvent(Actor);

    /// <summary>
    /// §7.3: Actor is whose cell changed (From → To, To is the near edge for the enemy). Pushed is true
    /// when the other side moved them (push / pull). Only emitted when the cell did change.
    /// </summary>
    public sealed record CellsMoved(Actor Actor, int From, int To, bool Pushed) : BattleEvent(Actor);

    /// <summary>
    /// §7.3 壁のダメージ: Actor is the pushed side that could not take BlockedCells of the push.
    /// Raw − Absorbed = Damage, as in <see cref="DamageDealt"/>.
    /// </summary>
    public sealed record WallHit(
        Actor Actor,
        int BlockedCells,
        int Raw,
        int Absorbed,
        int Damage,
        int GuardAfter,
        int HpAfter) : BattleEvent(Actor);

    /// <summary>
    /// §7.3: Actor is the target of a push / pull that its size (2 or more) refused. ByStance is true
    /// when a stance refused it instead (錨の構え, #188).
    /// </summary>
    public sealed record PushRefused(Actor Actor, int Size) : BattleEvent(Actor)
    {
        public bool ByStance { get; init; }
    }

    /// <summary>§5 鈍足: the holder's move, push or pull was shortened to 0 cells and so did not happen.</summary>
    public sealed record MoveBlocked(Actor Actor, StatusKind By) : BattleEvent(Actor);

    /// <summary>Actor is who applied it. Refused is true when the target was at its kind limit (§5).</summary>
    public sealed record StatusApplied(
        Actor Actor,
        Actor Target,
        StatusKind Kind,
        int Stacks,
        int StacksAfter,
        bool Refused) : BattleEvent(Actor);

    public sealed record StaminaGained(Actor Actor, int Amount, int StaminaAfter) : BattleEvent(Actor);

    // ---- The demo vocabulary (#188): statuses, heal, break, stance ----

    /// <summary>§5 使うと減る型: the word's effect landed and it lost one stack. Actor is the holder.</summary>
    public sealed record StatusConsumed(Actor Actor, StatusKind Kind, int StacksAfter) : BattleEvent(Actor);

    /// <summary>
    /// §5 出血 / 再生: HP moved at the holder's turn start. Actor is the holder; Amount is the signed
    /// change that happened (−4 for 出血 2; 再生 3 is +6 unless the maximum capped it, 0 at full HP)
    /// and HpAfter is settled.
    /// </summary>
    public sealed record StatusHpChanged(Actor Actor, StatusKind Kind, int Amount, int HpAfter) : BattleEvent(Actor);

    /// <summary>A heal face: Actor gained Amount HP (after the maximum capped it).</summary>
    public sealed record Healed(Actor Actor, int Amount, int HpAfter) : BattleEvent(Actor);

    /// <summary>崩し: Actor took Amount stamina off Target (floor 0).</summary>
    public sealed record StaminaBroken(Actor Actor, Actor Target, int Amount, int StaminaAfter) : BattleEvent(Actor);

    /// <summary>
    /// §5 見切り: Actor (the one hit) returned half of what its Guard absorbed to Target (the
    /// attacker). Raw − Absorbed = Damage against the attacker's own Guard, as in <see cref="DamageDealt"/>.
    /// </summary>
    public sealed record Reflected(
        Actor Actor,
        Actor Target,
        int Raw,
        int Absorbed,
        int Damage,
        int TargetGuardAfter,
        int TargetHpAfter) : BattleEvent(Actor);

    /// <summary>§4: Actor set a stance into its slot. Replaced is the source id of the stance it pushed out, if any.</summary>
    public sealed record StanceSet(Actor Actor, string SourceId, string Name, StanceDef Stance, string? Replaced) : BattleEvent(Actor);

    /// <summary>§4: Actor's stance took effect. What it did follows as its own events (GuardGained, …).</summary>
    public sealed record StanceFired(Actor Actor, string SourceId, StanceHook Hook) : BattleEvent(Actor);

    /// <summary>§4: a stance card left for the exile pile once it resolved. It is not drawn again this battle.</summary>
    public sealed record CardExiled(Actor Actor, CardInstance Card) : BattleEvent(Actor);

    /// <summary>
    /// §7.4 / §17.6 F9: one of several enemies fell (Unit says which) and the battle goes on. It
    /// leaves its cells at once. The last enemy to fall ends the battle instead: <see cref="BattleEnded"/>.
    /// </summary>
    public sealed record EnemyDefeated(Actor Actor) : BattleEvent(Actor);

    /// <summary>§9 steps 6 and 11. Actor is the side that just acted.</summary>
    public sealed record DefeatChecked(Actor Actor, GameResult Result) : BattleEvent(Actor);

    /// <summary>Actor is the winner's side.</summary>
    public sealed record BattleEnded(Actor Actor, GameResult Result) : BattleEvent(Actor);

    /// <summary>One move of the loop: the state it left and what happened on the way there.</summary>
    public sealed record StepResult(BattleState State, IReadOnlyList<BattleEvent> Events);
}
