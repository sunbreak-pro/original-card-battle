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
    public abstract record BattleEvent(Actor Actor);

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

    /// <summary>The player released a card. PositionBefore is the side every position condition reads (§2.2).</summary>
    public sealed record CardPlayed(Actor Actor, CardInstance Card, Position? PositionBefore) : BattleEvent(Actor);

    /// <summary>The enemy carried out its omen. OpponentPositionBefore is the player's side before any push.</summary>
    public sealed record ActionExecuted(Actor Actor, EnemyActionDef Action, Position? OpponentPositionBefore) : BattleEvent(Actor);

    /// <summary>§6: the declared action could not be paid for, so the phase is recovery only.</summary>
    public sealed record Rested(Actor Actor, Omen Declared) : BattleEvent(Actor);

    public sealed record StaminaSpent(Actor Actor, int Amount, int StaminaAfter) : BattleEvent(Actor);

    /// <summary>§2.2: judged once, before any face. Only emitted for a card or action that carries a trait.</summary>
    public sealed record TraitEvaluated(Actor Actor, string SourceId, Trait Trait, TraitOutcome Outcome) : BattleEvent(Actor);

    /// <summary>One face of the card resolved. They come in §2.2 order: Attack, Move, Guard, Skill.</summary>
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

    /// <summary>Actor is whose position changed. Pushed is true when the other side moved them (§2.4).</summary>
    public sealed record PositionChanged(Actor Actor, Position From, Position To, bool Pushed) : BattleEvent(Actor);

    /// <summary>§5 鈍足: the move face was played but the holder could not switch sides.</summary>
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

    /// <summary>§9 steps 6 and 11. Actor is the side that just acted.</summary>
    public sealed record DefeatChecked(Actor Actor, GameResult Result) : BattleEvent(Actor);

    /// <summary>Actor is the winner's side.</summary>
    public sealed record BattleEnded(Actor Actor, GameResult Result) : BattleEvent(Actor);

    /// <summary>One move of the loop: the state it left and what happened on the way there.</summary>
    public sealed record StepResult(BattleState State, IReadOnlyList<BattleEvent> Events);
}
