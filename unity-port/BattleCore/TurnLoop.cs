using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// §9: the turn, end to end. Pure — every move takes a state and hands back a new one with the
    /// events that led there. The only randomness is the injected <see cref="IRng"/> (shuffles).
    ///
    /// The loop waits in two places. <see cref="BeginPlayerTurn"/> runs steps 1-5 and stops for the
    /// player; <see cref="PlayCard"/> is step 6, as many times as stamina allows;
    /// <see cref="EndTurn"/> runs steps 7-12 without stopping, enemy phases included, and ends on
    /// the next omens. One turn is BeginPlayerTurn → PlayCard × n → EndTurn.
    ///
    /// Several enemies (§7.4, #47): a card that aims at one enemy names it (the drop zone's pick);
    /// the enemies take their phases one by one in definition order, each running steps 9-12; the
    /// battle is won when the last one falls. A one-enemy battle emits the same stream as before.
    ///
    /// The demo vocabulary (#188): the status words (all ten since #48), the twelve trait conditions and ten
    /// effects, heal and 崩し, and the stance slot with its exile pile. Where the canon leaves a
    /// moment open the choice is written at the spot. The enemies of the roster (#189) bring
    /// multi-blow faces, a second action for elites and bosses and a stance used once a battle;
    /// the boss adaptations go through the hook of #50 (<see cref="TreeSwitch"/>) and their conditions are #51.
    ///
    /// The class is split by job (#584). This file keeps the turn itself: the entry points that move
    /// it on, the enemy phase, the draw and the end checks. The other parts are
    /// TurnLoop.Preview.cs (whether a card can be played, and what a card or an omen would do),
    /// TurnLoop.Resolve.cs (one card or action, stage by stage), TurnLoop.Hits.cs (moves, pushes,
    /// what a hit does back, falls), TurnLoop.Stances.cs, TurnLoop.Statuses.cs (the turn start and
    /// the statuses), TurnLoop.Omens.cs (the next omen and the boss switches) and
    /// TurnLoop.Reading.cs (the board a card reads, whom it reads and the small lookups).
    /// </summary>
    public static partial class TurnLoop
    {
        // ---- Start ----

        /// <summary>
        /// Shuffles the deck and has every enemy decide its first omen, so that step 5 of turn 1 has
        /// something to show. Turn is 0 until the first <see cref="BeginPlayerTurn"/>.
        /// </summary>
        public static StepResult Start(BattleSetup setup, IRng rng)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var defs = setup.AllEnemies;
            foreach (var def in defs)
            {
                if (def == null) throw new ArgumentException("A battle needs its enemies.", nameof(setup));
            }
            var cells = setup.EnemyStartCells;
            var placed = new List<(int Cell, int Size)>();
            for (int i = 0; i < defs.Count; i++) placed.Add((cells[i], defs[i].Size));
            Field.Validate(setup.FieldCells, setup.PlayerStartCell, 1, placed);

            var player = new CombatantState(
                setup.StartHp, setup.PlayerMaxHp,
                setup.StartStamina, setup.PlayerMaxStamina,
                Guard: 0, Cell: setup.PlayerStartCell, Size: 1, StatusSet.Empty);
            var units = new List<EnemyUnit>();
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                units.Add(new EnemyUnit(def, new CombatantState(
                    def.MaxHp, def.MaxHp,
                    def.MaxStamina, def.MaxStamina,
                    Guard: 0, Cell: cells[i], Size: def.Size, StatusSet.Empty), Omen: null));
            }

            var state = new BattleState(
                Turn: 0, setup.FieldCells, player, units,
                Hand: new List<CardInstance>(),
                DrawPile: Cards.Shuffle(setup.Deck, rng),
                DiscardPile: new List<CardInstance>(),
                PlayerTools: setup.Tools);

            var events = new List<BattleEvent>();
            for (int i = 0; i < units.Count; i++) state = DecideNextOmen(state, i, events);
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

            // #334: a permanent effect stopped through the last turn works again from here, before
            // the turn-start stances fire.
            state = ResumeStances(state, events);

            // Steps 1-3: Guard to 0, recover, the stance and the statuses at turn start.
            state = OpenTurnFor(state, Actor.Player, 0, Constants.StaminaRecovery, events);

            // §17.6 F9: 出血 can take the last HP at turn start. The battle ends there; nothing is drawn.
            if (Combat.IsDefeated(state.Player.Hp))
            {
                return new StepResult(CheckDefeat(state, Actor.Player, 0, events), events);
            }

            // Step 4: one card at a time, so a reshuffle lands between the right two draws.
            state = DrawCards(state, Combat.DrawCount(), rng, events);

            // Step 5: show each omen that has been standing since step 12 (or since Start).
            foreach (int i in state.Living)
            {
                events.Add(new OmenSet(Actor.Enemy, state.Enemies[i].Omen!, Decided: false) { Unit = i });
                if (state.Enemies[i].Plan != null) events.Add(new PlanSet(Actor.Enemy, state.Enemies[i].Plan!, Decided: false) { Unit = i });
            }

            return new StepResult(state with { Phase = BattlePhase.PlayerAction }, events);
        }

        // ---- Step 6 ----

        public static StepResult PlayCard(BattleState state, string instanceId, IRng rng, int target = 0)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var refusal = CanPlay(state, instanceId, target);
            if (refusal != PlayRefusal.None)
            {
                throw new InvalidOperationException($"Card \"{instanceId}\" cannot be played: {refusal}.");
            }

            var card = FindInHand(state, instanceId)!;
            var def = card.Def;
            var events = new List<BattleEvent>();
            int read = ReadUnit(state, def, target);
            bool directed = EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets);
            IReadOnlyList<int> foes =
                !directed ? Array.Empty<int>()
                : def.Targets == TargetKind.All ? Reached(state, def.Face.ReachOrDefault)
                : new List<int> { read };

            // Every condition reads the board from before the card leaves the hand (手薄 counts the
            // hand it leaves behind).
            var context = PlayerContext(state, def, read);

            // §4 コストの割引: the entry this card spends, judged on the same board as CostNow.
            var judged = Traits.EvaluateAll(def.AllTraits, context);
            int discount = CostDiscountEntry(state, def, Math.Max(0, def.Cost - judged.CostDown));

            // §3.2 習熟 (#59): the contexts this play meets, read off the same board. They are only
            // written down here; the ticks they make land once the battle is over.
            int paid = Math.Max(0, (discount >= 0 ? def.Cost - 1 : def.Cost) - judged.CostDown);
            var mastery = new MasteryPlay(def.Id, Mastery.ContextsOf(def, context, judged.Triggered, paid));

            // §5 俊敏 (#48): the free cell is taken before the first card or not at all.
            var hand = new List<CardInstance>(state.Hand);
            hand.Remove(card);
            state = state with { Hand = hand, Player = state.Player with { FreeStep = false } };
            events.Add(new CardPlayed(Actor.Player, card, state.GapTo(read)) { Unit = read });

            if (discount >= 0)
            {
                var stances = new List<StanceEntry>(state.Player.StanceList);
                var entry = stances[discount];
                stances[discount] = entry with { ReactedTurn = state.Turn };
                state = state with { Player = state.Player with { Stances = stances } };
                events.Add(new StanceFired(Actor.Player, entry.Source, StanceHook.CostDiscount));
            }

            // The discount comes off before Resolve pays: the cost it was saving was at least 1
            // after the trait, so Resolve's own floor never swallows it.
            var source = CardSource(def, discount >= 0 ? def.Cost - 1 : def.Cost);
            state = Resolve(state, new Play(Actor.Player, read, foes, source, context), rng, events);

            // §2.3 playedAttributes: what this card was, for the cards after it this turn.
            state = RecordPlayed(state, Actor.Player, 0, def.Attribute);
            state = state with { PlayerHistory = state.History.WithCard(def.Attribute).WithMasteryPlay(mastery) };

            // §4: a stance card goes to the exile pile and is not drawn again this battle; every
            // other card goes to the discard pile once it has resolved.
            if (Cards.IsStanceCard(def))
            {
                var exile = new List<CardInstance>(state.Exiled) { card };
                state = state with { ExilePile = exile };
                events.Add(new CardExiled(Actor.Player, card));
            }
            else
            {
                var discard = new List<CardInstance>(state.DiscardPile) { card };
                state = state with { DiscardPile = discard };
            }

            state = CheckDefeat(state, Actor.Player, 0, events);
            return new StepResult(state, events);
        }

        /// <summary>
        /// §5 俊敏 (#48): whether the player may take the free cell now. It opens at the turn start
        /// (§9 step 3) when 俊敏 is held and stays open until it is taken, the first card is played
        /// or the turn ends — so the hand just drawn can inform the choice.
        /// </summary>
        public static bool CanTakeFreeStep(BattleState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return state.Result == GameResult.Ongoing && state.Phase == BattlePhase.PlayerAction && state.Player.FreeStep;
        }

        /// <summary>
        /// §5 俊敏 (#48): the player takes the free cell, <paramref name="direction"/> +1 forward or
        /// −1 back. It moves like a move face of one cell, at no cost: 鈍足 takes it to 0, the line
        /// stops it at the nearest enemy and at cell 1, and a cell that changes counts as 移動後
        /// (§2.3). The step is spent even when the line stops it. Not moving needs no call: playing
        /// a card or ending the turn lets it go.
        /// </summary>
        public static StepResult TakeFreeStep(BattleState state, int direction)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (direction != Statuses.SwiftFreeCells && direction != -Statuses.SwiftFreeCells)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "The free step is one cell: +1 (forward) or -1 (back).");
            }
            Require(state, BattlePhase.PlayerAction, nameof(TakeFreeStep));
            if (!state.Player.FreeStep)
            {
                throw new InvalidOperationException("TakeFreeStep: the player has no free step this turn (no 俊敏, or already taken or let go).");
            }

            var events = new List<BattleEvent>();
            state = state with { Player = state.Player with { FreeStep = false } };
            events.Add(new FreeStepTaken(Actor.Player, direction));
            state = MoveSelf(state, Actor.Player, 0, direction, events);
            return new StepResult(state, events);
        }

        // ---- Steps 7-12 ----

        public static StepResult EndTurn(BattleState state, IRng rng)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            Require(state, BattlePhase.PlayerAction, nameof(EndTurn));

            var events = new List<BattleEvent>();

            // Step 7: the stance's turn-end effect (根渡り, 霞み足), then 構え.
            state = StanceAtTurnEnd(state, Actor.Player, 0, events);
            state = CheckReserve(state, Actor.Player, 0, events);

            // #50: what a boss's adaptation may count — the Guard held once 構え was judged, N, and a move.
            // #51: and the cell it ended on (根張り), how many cards it played (root_st), and the way
            // its own last move went (root_m).
            int nearest = state.Nearest;
            state = state with
            {
                PlayerHistory = state.History.WithTurnEnd(new PlayerTurnEnd(
                    state.Player.Guard, nearest < 0 ? 0 : state.GapTo(nearest), state.Player.Moved,
                    state.Player.Cell, state.Player.PlayedThisTurn.Count, state.Player.LastMove)),
            };

            // Step 8: the whole hand goes. Nothing is kept (§17.6 F2). playedAttributes and a 追撃
            // that found no attack face go with it (§17.6 F7).
            int discarded = state.Hand.Count;
            var (emptyHand, discardPile) = Cards.DiscardHand(state.Hand, state.DiscardPile);
            state = state with
            {
                Hand = emptyHand,
                DiscardPile = discardPile,
                Player = state.Player with { Played = null, Moved = false, LastMove = 0, FollowUp = 0, FreeStep = false },
            };
            events.Add(new HandDiscarded(Actor.Player, discarded));
            events.Add(new TurnEnded(Actor.Player, state.Turn));

            // Steps 9-12, enemy by enemy in definition order (§9, §7.4).
            for (int i = 0; i < state.Enemies.Count; i++)
            {
                if (!state.Enemies[i].Alive) continue;
                state = EnemyPhase(state, i, rng, events);
                if (state.Result != GameResult.Ongoing) return new StepResult(state, events);
            }
            return new StepResult(state with { Phase = BattlePhase.AwaitingTurnStart }, events);
        }

        private static BattleState EnemyPhase(BattleState state, int unit, IRng rng, List<BattleEvent> events)
        {
            // Step 9: the enemy's own Guard reset, recovery, stance and status ticks.
            state = OpenTurnFor(state, Actor.Enemy, unit, state.Enemies[unit].Def.Recovery, events);
            if (!state.Enemies[unit].Alive)
            {
                // §17.6 F9: 出血 took the last HP at its turn start. It does not act.
                state = OnFall(state, Actor.Enemy, unit, events, out bool over);
                return over ? CheckDefeat(state, Actor.Enemy, unit, events) : state;
            }

            // #51 (roster §6.1 段階 3): a body that sways steps before it acts.
            state = Sway(state, unit, events);

            // Step 10: the omen, as declared — or a rest when it cannot be paid for.
            var enemy = state.Enemies[unit];
            var active = EnemyAi.SwitchById(enemy.Def, enemy.ActiveSwitch);
            var action = EnemyAi.ActionToExecute(enemy.Def, enemy.Omen!, enemy.Body.Stamina, active);
            if (action == null)
            {
                events.Add(new Rested(Actor.Enemy, enemy.Omen!) { Unit = unit });

                // #50: the phase ends on the rest, so the 予定 that was shown goes with it.
                state = RevisePlan(state, unit, EnemyAi.OmenOf(null), events);
            }
            else
            {
                state = Execute(state, unit, action, rng, events, out bool fell);
                if (fell) return state.Living.Count == 0 ? CheckDefeat(state, Actor.Enemy, unit, events) : state;

                // roster §1.3 (#189), §17.6 F11 (#50): an elite or a boss takes a second action. The
                // omen showed the first and planned the second (予定). The tree is read again from the
                // gap after the first — on the tree the omen was decided on — and the same action is
                // not taken twice in one phase. A different answer is a 予定変更, shown before it acts.
                if (enemy.Def.ActionsPerPhase >= 2 && !Combat.IsDefeated(state.Player.Hp))
                {
                    var skip = new List<string>(state.Enemies[unit].Spent) { action.Id };
                    skip.AddRange(EnemyAi.Barred(enemy.Def, state.Player.Statuses, beforeFoeTurn: false, active: active, warded: state.Tools.WardedStacks));
                    var second = EnemyAi.ChooseAction(enemy.Def, state.GapTo(unit), state.Enemies[unit].Body.Stamina, skip, active);
                    state = RevisePlan(state, unit, EnemyAi.OmenOf(second), events);
                    if (second != null)
                    {
                        state = Execute(state, unit, second, rng, events, out fell);
                        if (fell) return state.Living.Count == 0 ? CheckDefeat(state, Actor.Enemy, unit, events) : state;
                    }
                }
            }

            // The stance's turn-end effect, then 構え, which works for the enemy too (roster §1.2).
            state = StanceAtTurnEnd(state, Actor.Enemy, unit, events);
            state = CheckReserve(state, Actor.Enemy, unit, events);
            state = state.WithEnemy(unit, state.Enemies[unit].Body with { Played = null, Moved = false, LastMove = 0, FollowUp = 0 });

            // Step 11.
            state = CheckDefeat(state, Actor.Enemy, unit, events);
            if (state.Result != GameResult.Ongoing) return state;

            // Step 12: the next omen, read from this enemy's gap as it stands now — after any move or push.
            return DecideNextOmen(state, unit, events);
        }

        /// <summary>
        /// §9 step 10 for one action of an enemy. Fell is true when the enemy went down to its own
        /// blow's return (見切り): the phase ends there. A stance action is spent (roster §1.2).
        /// </summary>
        private static BattleState Execute(BattleState state, int unit, EnemyActionDef action, IRng rng, List<BattleEvent> events, out bool fell)
        {
            events.Add(new ActionExecuted(Actor.Enemy, action, state.GapTo(unit)) { Unit = unit });
            var context = EnemyContext(state, unit, action);
            var source = ActionSource(action, EnemyAi.FaceAt(action, state.GapTo(unit)));
            state = Resolve(state, new Play(Actor.Enemy, unit, Array.Empty<int>(), source, context), rng, events);
            fell = !state.Enemies[unit].Alive;
            var taken = new List<string>(state.Enemies[unit].ExecutedActions) { action.Id };
            state = state.WithUnit(unit, state.Enemies[unit] with { Executed = taken });
            if (fell) return state;
            if (action.Face.Stance != null)
            {
                var spent = new List<string>(state.Enemies[unit].Spent) { action.Id };
                state = state.WithUnit(unit, state.Enemies[unit] with { SpentStances = spent });
            }
            return RecordPlayed(state, Actor.Enemy, unit, action.Attribute);
        }

        // ---- Shared pieces ----

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

        private static BattleState RecordPlayed(BattleState state, Actor actor, int unit, BattleAttribute attributes)
        {
            var self = Get(state, actor, unit);
            var played = new List<BattleAttribute>(self.PlayedThisTurn) { attributes };
            return Set(state, actor, unit, self with { Played = played });
        }

        private static BattleState CheckReserve(BattleState state, Actor actor, int unit, List<BattleEvent> events)
        {
            var self = Get(state, actor, unit);
            int gained = Combat.ReserveGuard(self.Stamina);
            self = self with { Guard = self.Guard + gained };
            events.Add(new ReserveChecked(actor, self.Stamina, gained, self.Guard) { Unit = unit });
            return Set(state, actor, unit, self);
        }

        /// <summary>
        /// §9 steps 6 and 11: lost when the player is down, won when no enemy stands. If both happen
        /// at once the player has lost: the canon does not say, so the stricter reading is taken.
        /// </summary>
        private static BattleState CheckDefeat(BattleState state, Actor actor, int unit, List<BattleEvent> events)
        {
            GameResult result =
                Combat.IsDefeated(state.Player.Hp) ? GameResult.Lost
                : state.Living.Count == 0 ? GameResult.Won
                : GameResult.Ongoing;

            events.Add(new DefeatChecked(actor, result) { Unit = unit });
            if (result == GameResult.Ongoing) return state;

            events.Add(new BattleEnded(result == GameResult.Won ? Actor.Player : Actor.Enemy, result) { Unit = unit });
            return state with { Result = result, Phase = BattlePhase.Finished };
        }
    }
}
