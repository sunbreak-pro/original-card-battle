using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// What a battle starts from. The defaults are the slice's provisional values
    /// (vision/plans/2026-09-21-vertical-slice-polearm.md 「仮に置く値」): the player starts near with
    /// full stamina, the enemy starts with full stamina, and both start with Guard 0.
    /// </summary>
    public sealed record BattleSetup(
        EnemyDef Enemy,
        IReadOnlyList<CardInstance> Deck,
        Position PlayerStartPosition = Position.Near,
        int PlayerMaxHp = Constants.PlayerMaxHp,
        int PlayerMaxStamina = Constants.BaseMaxStamina)
    {
        /// <summary>The vertical slice: the polearm against the ten-kind prototype deck.</summary>
        public static BattleSetup Slice() => new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build());
    }

    /// <summary>The settled numbers a held card would produce. See <see cref="TurnLoop.Preview"/>.</summary>
    public sealed record PlayPreview(bool TraitHolds, bool Attacks, int RawPower, int Damage, int GuardGain);

    /// <summary>Why a card cannot be played right now. None when it can.</summary>
    public enum PlayRefusal
    {
        None,
        BattleOver,
        NotPlayerTurn,
        NotInHand,
        NotEnoughStamina,
    }

    /// <summary>
    /// §9: the turn, end to end. Pure — every move takes a state and hands back a new one with the
    /// events that led there. The only randomness is the injected <see cref="IRng"/> (shuffles).
    ///
    /// The loop waits in two places. <see cref="BeginPlayerTurn"/> runs steps 1-5 and stops for the
    /// player; <see cref="PlayCard"/> is step 6, as many times as stamina allows;
    /// <see cref="EndTurn"/> runs steps 7-12 without stopping, enemy phase included, and ends on the
    /// next omen. One turn is BeginPlayerTurn → PlayCard × n → EndTurn.
    ///
    /// Not here, on purpose: the stance slot and the exile pile (#49), two-action enemies and the
    /// four-branch tree (#50), several enemies (#52), playedAttributes and the traits that read it (#48).
    /// </summary>
    public static class TurnLoop
    {
        // ---- Start ----

        /// <summary>
        /// Shuffles the deck and has the enemy decide its first omen, so that step 5 of turn 1 has
        /// something to show. Turn is 0 until the first <see cref="BeginPlayerTurn"/>.
        /// </summary>
        public static StepResult Start(BattleSetup setup, IRng rng)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var enemyDef = setup.Enemy;
            var player = new CombatantState(
                setup.PlayerMaxHp, setup.PlayerMaxHp,
                setup.PlayerMaxStamina, setup.PlayerMaxStamina,
                Guard: 0, setup.PlayerStartPosition, StatusSet.Empty);
            var enemy = new CombatantState(
                enemyDef.MaxHp, enemyDef.MaxHp,
                enemyDef.MaxStamina, enemyDef.MaxStamina,
                Guard: 0, enemyDef.HasPosition ? enemyDef.StartPosition : null, StatusSet.Empty);

            var state = new BattleState(
                Turn: 0, player, enemy, enemyDef, Omen: null,
                Hand: new List<CardInstance>(),
                DrawPile: Cards.Shuffle(setup.Deck, rng),
                DiscardPile: new List<CardInstance>());

            var events = new List<BattleEvent>();
            state = DecideNextOmen(state, events);
            return new StepResult(state, events);
        }

        // ---- Steps 1-5 ----

        public static StepResult BeginPlayerTurn(BattleState state, IRng rng)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            Require(state, BattlePhase.AwaitingTurnStart, nameof(BeginPlayerTurn));

            var events = new List<BattleEvent>();
            state = state with { Turn = state.Turn + 1 };
            events.Add(new TurnStarted(Actor.Player, state.Turn));

            // Steps 1-3: Guard to 0, recover, tick statuses.
            state = OpenTurnFor(state, Actor.Player, Constants.StaminaRecovery, events);

            // Step 4: one card at a time, so a reshuffle lands between the right two draws.
            state = DrawCards(state, Combat.DrawCount(), rng, events);

            // Step 5: show the omen that has been standing since step 12 (or since Start).
            events.Add(new OmenSet(Actor.Enemy, state.Omen!, Decided: false));

            return new StepResult(state with { Phase = BattlePhase.PlayerAction }, events);
        }

        // ---- Step 6 ----

        public static PlayRefusal CanPlay(BattleState state, string instanceId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Result != GameResult.Ongoing) return PlayRefusal.BattleOver;
            if (state.Phase != BattlePhase.PlayerAction) return PlayRefusal.NotPlayerTurn;

            var card = FindInHand(state, instanceId);
            if (card == null) return PlayRefusal.NotInHand;
            return Combat.CanPay(card.Def.Cost, state.Player.Stamina) ? PlayRefusal.None : PlayRefusal.NotEnoughStamina;
        }

        /// <summary>
        /// What playing this card would do right now, without playing it: whether its trait holds,
        /// the raw power, what gets past the opponent's Guard, and the Guard it would give. The screen
        /// shows these while a card is held (the lamp, the predicted number), so it never has to
        /// work a rule out by itself. Null when the card is not in the hand.
        /// </summary>
        public static PlayPreview? Preview(BattleState state, string instanceId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var card = FindInHand(state, instanceId);
            if (card == null) return null;

            var def = card.Def;
            var outcome = Traits.Evaluate(def.Trait, new TraitContext(
                state.Player.Position, state.Enemy.Position, state.Enemy.Guard, state.Player.Stamina - def.Cost));

            bool attacks = def.Attributes.HasFlag(BattleAttribute.Attack);
            int raw = attacks ? Combat.ComputeRawPower(def.Face.Power, outcome.PowerBonus) : 0;
            int damage = attacks ? Combat.ApplyGuard(raw, state.Enemy.Guard).Damage : 0;
            return new PlayPreview(outcome.Triggered, attacks, raw, damage, def.Face.Guard + outcome.GuardBonus);
        }

        public static StepResult PlayCard(BattleState state, string instanceId, IRng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var refusal = CanPlay(state, instanceId);
            if (refusal != PlayRefusal.None)
            {
                throw new InvalidOperationException($"Card \"{instanceId}\" cannot be played: {refusal}.");
            }

            var card = FindInHand(state, instanceId)!;
            var events = new List<BattleEvent>();

            var hand = new List<CardInstance>(state.Hand);
            hand.Remove(card);
            state = state with { Hand = hand };
            events.Add(new CardPlayed(Actor.Player, card, state.Player.Position));

            state = Resolve(
                state, Actor.Player, card.Def.Id, card.Def.Attributes, card.Def.Face, card.Def.Trait,
                card.Def.Cost, rng, events);

            // The card goes to the discard pile once it has resolved (there is no exile pile: #49).
            var discard = new List<CardInstance>(state.DiscardPile) { card };
            state = state with { DiscardPile = discard };

            state = CheckDefeat(state, Actor.Player, events);
            return new StepResult(state, events);
        }

        // ---- Steps 7-12 ----

        public static StepResult EndTurn(BattleState state, IRng rng)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            Require(state, BattlePhase.PlayerAction, nameof(EndTurn));

            var events = new List<BattleEvent>();

            // Step 7: 構え.
            state = CheckReserve(state, Actor.Player, events);

            // Step 8: the whole hand goes. Nothing is kept (§17.6 F2).
            int discarded = state.Hand.Count;
            var (emptyHand, discardPile) = Cards.DiscardHand(state.Hand, state.DiscardPile);
            state = state with { Hand = emptyHand, DiscardPile = discardPile };
            events.Add(new HandDiscarded(Actor.Player, discarded));
            events.Add(new TurnEnded(Actor.Player, state.Turn));

            // Step 9: the enemy's own Guard reset, recovery and status ticks.
            state = OpenTurnFor(state, Actor.Enemy, state.EnemyDef.Recovery, events);

            // Step 10: the omen, as declared — or a rest when it cannot be paid for.
            var action = EnemyAi.ActionToExecute(state.EnemyDef, state.Omen!, state.Enemy.Stamina);
            if (action == null)
            {
                events.Add(new Rested(Actor.Enemy, state.Omen!));
            }
            else
            {
                events.Add(new ActionExecuted(Actor.Enemy, action, state.Player.Position));
                state = Resolve(
                    state, Actor.Enemy, action.Id, action.Attributes, action.Face, action.Trait,
                    action.Cost, rng, events);
            }

            // 構え works for the enemy too (roster §1.2), at the end of its own turn.
            state = CheckReserve(state, Actor.Enemy, events);

            // Step 11.
            state = CheckDefeat(state, Actor.Enemy, events);
            if (state.Result != GameResult.Ongoing) return new StepResult(state, events);

            // Step 12: the next omen, read from where the player stands now — after any push.
            state = DecideNextOmen(state, events);
            return new StepResult(state with { Phase = BattlePhase.AwaitingTurnStart }, events);
        }

        // ---- Shared pieces ----

        /// <summary>§9 steps 1-3 for the player, step 9 for the enemy: the same three things in the same order.</summary>
        private static BattleState OpenTurnFor(BattleState state, Actor actor, int recovery, List<BattleEvent> events)
        {
            var self = Get(state, actor);

            events.Add(new GuardCleared(actor, self.Guard));
            self = self with { Guard = 0 };

            int staminaAfter = Combat.RecoverStamina(self.Stamina, self.MaxStamina, recovery, self.NextTurnRecoveryBonus);
            events.Add(new StaminaRecovered(actor, staminaAfter - self.Stamina, staminaAfter, self.MaxStamina));
            self = self with { Stamina = staminaAfter, NextTurnRecoveryBonus = 0 };

            var ticked = self.Statuses.TickTurnStart();
            foreach (var kind in self.Statuses.Kinds)
            {
                if (Statuses.DecayOf(kind) != StatusDecay.OnTurn) continue;
                events.Add(new StatusTicked(actor, kind, ticked.Stacks(kind)));
            }
            self = self with { Statuses = ticked };

            return Set(state, actor, self);
        }

        private static BattleState DrawCards(BattleState state, int count, IRng rng, List<BattleEvent> events)
        {
            for (int i = 0; i < count; i++)
            {
                var result = Cards.Draw(state.DrawPile, state.DiscardPile, state.Hand, 1, rng);
                if (result.Drawn == 0) break;

                if (result.Reshuffled)
                {
                    // +1: the pile as it stood right after the shuffle, before this card left it.
                    events.Add(new DeckReshuffled(Actor.Player, result.DrawPile.Count + 1));
                }
                if (result.OverflowToDiscard == 0)
                {
                    events.Add(new Drawn(Actor.Player, result.Hand[result.Hand.Count - 1], result.Hand.Count));
                }
                state = state with { Hand = result.Hand, DrawPile = result.DrawPile, DiscardPile = result.DiscardPile };
            }
            return state;
        }

        /// <summary>
        /// §2.2: pay, judge the trait once, then Attack → Move → Guard → Skill. The same routine
        /// resolves a card and an enemy action, because both are written off the same face table.
        /// Stops after the attack face when the target falls (§17.6 F9).
        /// </summary>
        private static BattleState Resolve(
            BattleState state,
            Actor actor,
            string sourceId,
            BattleAttribute attributes,
            Face face,
            Trait? trait,
            int cost,
            IRng rng,
            List<BattleEvent> events)
        {
            Actor foe = Opponent(actor);
            var self = Get(state, actor);
            var other = Get(state, foe);

            self = self with { Stamina = self.Stamina - cost };
            events.Add(new StaminaSpent(actor, cost, self.Stamina));

            // Every condition reads the board from before the card: the position before the move
            // face, the opponent's Guard before the attack face, the stamina left after the cost.
            var outcome = Traits.Evaluate(trait, new TraitContext(
                self.Position, other.Position, other.Guard, self.Stamina));
            if (trait != null) events.Add(new TraitEvaluated(actor, sourceId, trait, outcome));
            if (outcome.NextTurnRecoveryBonus != 0)
            {
                self = self with { NextTurnRecoveryBonus = self.NextTurnRecoveryBonus + outcome.NextTurnRecoveryBonus };
            }

            if (attributes.HasFlag(BattleAttribute.Attack))
            {
                int raw = Combat.ComputeRawPower(face.Power, outcome.PowerBonus);
                var (damage, guardAfter, absorbed) = Combat.ApplyGuard(raw, other.Guard);
                other = other with { Guard = guardAfter, Hp = Math.Max(0, other.Hp - damage) };
                events.Add(new FaceResolved(actor, sourceId, BattleAttribute.Attack));
                events.Add(new DamageDealt(actor, foe, raw, absorbed, damage, other.Guard, other.Hp));

                if (Combat.IsDefeated(other.Hp)) return Set(Set(state, actor, self), foe, other);
            }

            if (attributes.HasFlag(BattleAttribute.Move))
            {
                events.Add(new FaceResolved(actor, sourceId, BattleAttribute.Move));
                if (face.Push)
                {
                    // §2.4: the push moves the opponent and Guard does not stop it. An opponent
                    // without a position has nowhere to be pushed to.
                    if (other.Position.HasValue)
                    {
                        var from = other.Position.Value;
                        other = other with { Position = from.Opposite() };
                        events.Add(new PositionChanged(foe, from, from.Opposite(), Pushed: true));
                    }
                }
                else if (self.Position.HasValue)
                {
                    if (!Statuses.CanSwitchPosition(self.Statuses))
                    {
                        events.Add(new MoveBlocked(actor, StatusKind.Slow));
                    }
                    else
                    {
                        var from = self.Position.Value;
                        var to = Combat.MoveResult(from, face)!.Value;
                        if (to != from)
                        {
                            self = self with { Position = to };
                            events.Add(new PositionChanged(actor, from, to, Pushed: false));
                        }
                    }
                }
            }

            // A Guard +n trait lands as Guard even on a card without a Guard face
            // (swordsman_cards_v4.md §1.2), and a move card may carry a small Guard of its own.
            if (attributes.HasFlag(BattleAttribute.Guard))
            {
                events.Add(new FaceResolved(actor, sourceId, BattleAttribute.Guard));
            }
            int guardGain = face.Guard + outcome.GuardBonus;
            if (guardGain > 0)
            {
                self = self with { Guard = self.Guard + guardGain };
                events.Add(new GuardGained(actor, guardGain, self.Guard));
            }

            if (attributes.HasFlag(BattleAttribute.Skill))
            {
                events.Add(new FaceResolved(actor, sourceId, BattleAttribute.Skill));
            }
            if (face.Status.HasValue && face.StatusStacks > 0)
            {
                // The slice's one word is applied to the opponent (§5). The player holds at most
                // six kinds; an enemy has no limit.
                var kind = face.Status.Value;
                int? limit = foe == Actor.Player ? Constants.StatusKindsPlayer : (int?)null;
                var after = other.Statuses.Add(kind, face.StatusStacks, limit);
                bool refused = after.Stacks(kind) == other.Statuses.Stacks(kind);
                other = other with { Statuses = after };
                events.Add(new StatusApplied(actor, foe, kind, face.StatusStacks, after.Stacks(kind), refused));
            }
            if (face.StaminaGain > 0)
            {
                int staminaAfter = Math.Min(self.MaxStamina, self.Stamina + face.StaminaGain);
                events.Add(new StaminaGained(actor, staminaAfter - self.Stamina, staminaAfter));
                self = self with { Stamina = staminaAfter };
            }

            state = Set(Set(state, actor, self), foe, other);

            if (face.Draw > 0 && actor == Actor.Player)
            {
                state = DrawCards(state, face.Draw, rng, events);
            }
            return state;
        }

        private static BattleState CheckReserve(BattleState state, Actor actor, List<BattleEvent> events)
        {
            var self = Get(state, actor);
            int gained = Combat.ReserveGuard(self.Stamina);
            self = self with { Guard = self.Guard + gained };
            events.Add(new ReserveChecked(actor, self.Stamina, gained, self.Guard));
            return Set(state, actor, self);
        }

        /// <summary>
        /// §9 steps 6 and 11. If both fell at once the player has lost: nothing in the slice can do
        /// that, and the canon does not say, so the stricter reading is taken.
        /// </summary>
        private static BattleState CheckDefeat(BattleState state, Actor actor, List<BattleEvent> events)
        {
            GameResult result =
                Combat.IsDefeated(state.Player.Hp) ? GameResult.Lost
                : Combat.IsDefeated(state.Enemy.Hp) ? GameResult.Won
                : GameResult.Ongoing;

            events.Add(new DefeatChecked(actor, result));
            if (result == GameResult.Ongoing) return state;

            events.Add(new BattleEnded(result == GameResult.Won ? Actor.Player : Actor.Enemy, result));
            return state with { Result = result, Phase = BattlePhase.Finished };
        }

        /// <summary>
        /// §9 step 12. The enemy judges what it can pay for with the stamina it will hold when the
        /// omen is carried out — after its next recovery — so an omen only becomes a rest when
        /// something drained it in between. The canon does not fix this moment; see #70.
        /// </summary>
        private static BattleState DecideNextOmen(BattleState state, List<BattleEvent> events)
        {
            var enemy = state.Enemy;
            int staminaThen = Combat.RecoverStamina(
                enemy.Stamina, enemy.MaxStamina, state.EnemyDef.Recovery, enemy.NextTurnRecoveryBonus);

            // The slice's player always carries a position; only the enemy may go without one.
            var omen = EnemyAi.DecideOmen(state.EnemyDef, state.Player.Position!.Value, staminaThen);
            events.Add(new OmenSet(Actor.Enemy, omen, Decided: true));
            return state with { Omen = omen };
        }

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

        private static CombatantState Get(BattleState state, Actor actor) =>
            actor == Actor.Player ? state.Player : state.Enemy;

        private static BattleState Set(BattleState state, Actor actor, CombatantState value) =>
            actor == Actor.Player ? state with { Player = value } : state with { Enemy = value };
    }
}
