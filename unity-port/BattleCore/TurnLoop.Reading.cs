using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// What a card or action reads (§2.3, #584): the board its traits read, the enemy it reads and
    /// hits (§7.4), and the small lookups every part of the class shares.
    /// </summary>
    public static partial class TurnLoop
    {
        /// <summary>
        /// The board a player's card reads, judged against enemy <paramref name="read"/>. The card is
        /// still in the hand. 温存 reads the stamina left once the cost is paid (§2.3), so a コストの割引
        /// entry (§4) the card would spend comes off what it pays. The card's own コスト −1 trait does
        /// not: it is judged on this context, and no card holds both it and 温存.
        /// </summary>
        private static TraitContext PlayerContext(BattleState state, CardDef def, int read)
        {
            var foe = state.Enemies[read];
            var self = state.Player;
            int paid = CostDiscountEntry(state, def, def.Cost) >= 0 ? def.Cost - 1 : def.Cost;
            return new TraitContext(
                Gap: state.GapTo(read),
                OpponentGuard: foe.Body.Guard,
                StaminaAfterUse: self.Stamina - paid,
                StaminaBefore: self.Stamina,
                OpponentStamina: foe.Body.Stamina,
                Played: self.PlayedThisTurn,
                HandAfterPlay: state.Hand.Count - 1,
                OpponentOmen: foe.Omen?.Label.Kind,
                SelfStatuses: self.Statuses,
                OpponentStatuses: foe.Body.Statuses,
                Attributes: def.Attribute,
                Moved: self.Moved);
        }

        /// <summary>The board an enemy action reads: the player is its opponent, and what it did earlier this phase is its playedAttributes.</summary>
        private static TraitContext EnemyContext(BattleState state, int unit, EnemyActionDef action)
        {
            var self = state.Enemies[unit].Body;
            var player = state.Player;
            return new TraitContext(
                Gap: state.GapTo(unit),
                OpponentGuard: player.Guard,
                StaminaAfterUse: self.Stamina - action.Cost,
                StaminaBefore: self.Stamina,
                OpponentStamina: player.Stamina,
                Played: self.PlayedThisTurn,
                SelfStatuses: self.Statuses,
                OpponentStatuses: player.Statuses,
                Attributes: action.Attribute,
                Moved: self.Moved);
        }

        private static IReadOnlyList<Trait> TraitsOf(Trait? trait) =>
            trait == null ? Array.Empty<Trait>() : new[] { trait };

        // ---- Whom a card reads and hits (§7.4) ----

        /// <summary>
        /// The enemy whose N and Guard the card's trait reads: the one it is dropped on for a card
        /// aimed at one, the nearest in reach for a card aimed at all, and the nearest standing enemy
        /// for a card aimed at nobody (§7.4 自分向きの札).
        /// </summary>
        private static int ReadUnit(BattleState state, CardDef def, int target)
        {
            if (EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets))
            {
                if (def.Targets != TargetKind.All) return target;
                var reached = Reached(state, def.Face.ReachOrDefault);
                if (reached.Count > 0) return reached[0];
            }
            return state.Nearest;
        }

        /// <summary><see cref="ReadUnit"/>, falling back to the nearest standing enemy when the one named is not standing. −1 when none stands.</summary>
        private static int ReadableUnit(BattleState state, CardDef def, int target)
        {
            int read = ReadUnit(state, def, target);
            return IsStanding(state, read) ? read : state.Nearest;
        }

        /// <summary>The standing enemies whose N falls inside <paramref name="reach"/>, nearest first.</summary>
        private static List<int> Reached(BattleState state, Reach reach)
        {
            var reached = new List<int>();
            foreach (int i in state.Living)
            {
                if (reach.Contains(state.GapTo(i))) reached.Add(i);
            }
            reached.Sort((a, b) => state.Enemies[a].Body.Cell.CompareTo(state.Enemies[b].Body.Cell));
            return reached;
        }

        private static bool IsStanding(BattleState state, int unit) =>
            unit >= 0 && unit < state.Enemies.Count && state.Enemies[unit].Alive;

        private static void Require(BattleState state, BattlePhase phase, string move)
        {
            if (state.Result != GameResult.Ongoing)
            {
                throw new InvalidOperationException($"{move}: the battle is over ({state.Result}).");
            }
            if (state.Phase != phase)
            {
                throw new InvalidOperationException($"{move}: expected phase {phase}, but the battle is in {state.Phase}.");
            }
        }

        private static CardInstance? FindInHand(BattleState state, string instanceId)
        {
            foreach (var card in state.Hand)
            {
                if (string.Equals(card.InstanceId, instanceId, StringComparison.Ordinal)) return card;
            }
            return null;
        }

        private static Actor Opponent(Actor actor) => actor == Actor.Player ? Actor.Enemy : Actor.Player;

        /// <summary>The player, or enemy <paramref name="unit"/>.</summary>
        private static CombatantState Get(BattleState state, Actor actor, int unit) =>
            actor == Actor.Player ? state.Player : state.Enemies[unit].Body;

        private static BattleState Set(BattleState state, Actor actor, int unit, CombatantState value) =>
            actor == Actor.Player ? state with { Player = value } : state.WithEnemy(unit, value);
    }
}
