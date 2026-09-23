using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// What a battle starts from. FieldCells has no default (2026-09-23, #169): the layer and the
    /// battle hand it in (5〜8, the dungeon side picks it in #168). The rest are §7.3's: the player
    /// on cell 2, the enemy START_GAP (3) cells further (<see cref="Field.EnemyStartCell"/> for a
    /// line too short), both with full stamina and Guard 0.
    ///
    /// MoreEnemies (§7.4, #47) are the second and third enemy, in phase order. Each starts on the
    /// first free cell behind the one before it; a line too short to hold them all is refused.
    /// </summary>
    public sealed record BattleSetup(
        EnemyDef Enemy,
        IReadOnlyList<CardInstance> Deck,
        int FieldCells,
        int StartGap = Constants.StartGap,
        int PlayerStartCell = Constants.PlayerStartCell,
        int PlayerMaxHp = Constants.PlayerMaxHp,
        int PlayerMaxStamina = Constants.BaseMaxStamina,
        IReadOnlyList<EnemyDef>? MoreEnemies = null)
    {
        /// <summary>The width the vertical slice is fought on until the dungeon hands one in (#168).</summary>
        public const int SliceFieldCells = 6;

        /// <summary>Every enemy of the battle, in phase order: <see cref="Enemy"/> first.</summary>
        public IReadOnlyList<EnemyDef> AllEnemies
        {
            get
            {
                var all = new List<EnemyDef> { Enemy };
                if (MoreEnemies != null) all.AddRange(MoreEnemies);
                return all;
            }
        }

        /// <summary>Where the first enemy's near edge stands on turn 1.</summary>
        public int EnemyStartCell => Field.EnemyStartCell(FieldCells, PlayerStartCell, 1, Enemy.Size, StartGap);

        /// <summary>
        /// §7.4: every enemy's near edge on turn 1. The first stands START_GAP from the player; each
        /// next one on the cell right behind the far edge of the one before.
        /// </summary>
        public IReadOnlyList<int> EnemyStartCells
        {
            get
            {
                var cells = new List<int>();
                var all = AllEnemies;
                for (int i = 0; i < all.Count; i++)
                {
                    cells.Add(i == 0 ? EnemyStartCell : cells[i - 1] + all[i - 1].Size);
                }
                return cells;
            }
        }

        /// <summary>The vertical slice: the polearm against the ten-kind prototype deck.</summary>
        public static BattleSetup Slice() => new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), SliceFieldCells);
    }

    /// <summary>
    /// The settled numbers a held card would produce. See <see cref="TurnLoop.Preview"/>. InReach is
    /// false when the card aims at the opponent and N is outside its reach — the one reason a card
    /// with payable cost still cannot be released (§2.4).
    /// </summary>
    public sealed record PlayPreview(bool TraitHolds, bool Attacks, int RawPower, int Damage, int GuardGain, bool InReach);

    /// <summary>Why a card cannot be played right now. None when it can.</summary>
    public enum PlayRefusal
    {
        None,
        BattleOver,
        NotPlayerTurn,
        NotInHand,
        NotEnoughStamina,

        /// <summary>§2.4: the opponent stands outside the card's reach.</summary>
        OutOfReach,

        /// <summary>§7.4: the card aims at one enemy and the one named is not a standing enemy.</summary>
        NoSuchTarget,
    }

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
    /// Not here, on purpose: the stance slot and the exile pile (#49), two-action enemies and
    /// adaptation (#50), playedAttributes and the traits that read it (#48).
    /// </summary>
    public static class TurnLoop
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
                setup.PlayerMaxHp, setup.PlayerMaxHp,
                setup.PlayerMaxStamina, setup.PlayerMaxStamina,
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
                DiscardPile: new List<CardInstance>());

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

            // Steps 1-3: Guard to 0, recover, tick statuses.
            state = OpenTurnFor(state, Actor.Player, 0, Constants.StaminaRecovery, events);

            // Step 4: one card at a time, so a reshuffle lands between the right two draws.
            state = DrawCards(state, Combat.DrawCount(), rng, events);

            // Step 5: show each omen that has been standing since step 12 (or since Start).
            foreach (int i in state.Living)
            {
                events.Add(new OmenSet(Actor.Enemy, state.Enemies[i].Omen!, Decided: false) { Unit = i });
            }

            return new StepResult(state with { Phase = BattlePhase.PlayerAction }, events);
        }

        // ---- Step 6 ----

        /// <summary>
        /// Whether the card can be released now. <paramref name="target"/> is the enemy a card that
        /// aims at one enemy is dropped on (§7.4); a card aimed at all, or at nobody, ignores it.
        /// </summary>
        public static PlayRefusal CanPlay(BattleState state, string instanceId, int target = 0)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Result != GameResult.Ongoing) return PlayRefusal.BattleOver;
            if (state.Phase != BattlePhase.PlayerAction) return PlayRefusal.NotPlayerTurn;

            var card = FindInHand(state, instanceId);
            if (card == null) return PlayRefusal.NotInHand;
            if (!Combat.CanPay(card.Def.Cost, state.Player.Stamina)) return PlayRefusal.NotEnoughStamina;

            return AimRefusal(state, card.Def, target);
        }

        /// <summary>
        /// §2.4 / §7.4: the part of <see cref="CanPlay"/> that is about whom the card aims at. A card
        /// aimed at the opponent needs them inside its reach — the one named for a card aimed at
        /// one, anyone for a card aimed at all. A player's card never whiffs.
        /// </summary>
        private static PlayRefusal AimRefusal(BattleState state, CardDef def, int target)
        {
            if (!EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets)) return PlayRefusal.None;
            if (def.Targets == TargetKind.All)
            {
                return Reached(state, def.Face.ReachOrDefault).Count > 0 ? PlayRefusal.None : PlayRefusal.OutOfReach;
            }
            if (!IsStanding(state, target)) return PlayRefusal.NoSuchTarget;
            return def.Face.ReachOrDefault.Contains(state.GapTo(target)) ? PlayRefusal.None : PlayRefusal.OutOfReach;
        }

        /// <summary>
        /// What playing this card would do right now, without playing it: whether its trait holds,
        /// the raw power, what gets past the Guard of the enemy it reads, and the Guard it would give.
        /// The screen shows these while a card is held (the lamp, the predicted number), so it never
        /// has to work a rule out by itself. Null when the card is not in the hand.
        /// </summary>
        public static PlayPreview? Preview(BattleState state, string instanceId, int target = 0)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var card = FindInHand(state, instanceId);
            if (card == null) return null;

            var def = card.Def;
            int read = ReadUnit(state, def, target);
            if (!IsStanding(state, read)) read = state.Nearest;
            if (read < 0) return null;
            var foe = state.Enemies[read].Body;
            var outcome = Traits.Evaluate(def.Trait, new TraitContext(
                state.GapTo(read), foe.Guard, state.Player.Stamina - def.Cost));

            bool attacks = def.Attributes.HasFlag(BattleAttribute.Attack);
            int raw = attacks ? Combat.ComputeRawPower(def.Face.Power, outcome.PowerBonus) : 0;
            int damage = attacks ? Combat.ApplyGuard(raw, foe.Guard).Damage : 0;
            bool inReach = AimRefusal(state, def, target) == PlayRefusal.None;
            return new PlayPreview(outcome.Triggered, attacks, raw, damage, def.Face.Guard + outcome.GuardBonus, inReach);
        }

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
            var foes = def.Targets == TargetKind.All && EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets)
                ? Reached(state, def.Face.ReachOrDefault)
                : new List<int> { read };

            var hand = new List<CardInstance>(state.Hand);
            hand.Remove(card);
            state = state with { Hand = hand };
            events.Add(new CardPlayed(Actor.Player, card, state.GapTo(read)) { Unit = read });

            state = Resolve(
                state, Actor.Player, read, foes, def.Id, def.Attributes, def.Face, def.Trait,
                def.Targets, def.Cost, rng, events);

            // The card goes to the discard pile once it has resolved (there is no exile pile: #49).
            var discard = new List<CardInstance>(state.DiscardPile) { card };
            state = state with { DiscardPile = discard };

            state = CheckDefeat(state, Actor.Player, 0, events);
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
            state = CheckReserve(state, Actor.Player, 0, events);

            // Step 8: the whole hand goes. Nothing is kept (§17.6 F2).
            int discarded = state.Hand.Count;
            var (emptyHand, discardPile) = Cards.DiscardHand(state.Hand, state.DiscardPile);
            state = state with { Hand = emptyHand, DiscardPile = discardPile };
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
            // Step 9: the enemy's own Guard reset, recovery and status ticks.
            state = OpenTurnFor(state, Actor.Enemy, unit, state.Enemies[unit].Def.Recovery, events);

            // Step 10: the omen, as declared — or a rest when it cannot be paid for.
            var enemy = state.Enemies[unit];
            var action = EnemyAi.ActionToExecute(enemy.Def, enemy.Omen!, enemy.Body.Stamina);
            if (action == null)
            {
                events.Add(new Rested(Actor.Enemy, enemy.Omen!) { Unit = unit });
            }
            else
            {
                events.Add(new ActionExecuted(Actor.Enemy, action, state.GapTo(unit)) { Unit = unit });
                state = Resolve(
                    state, Actor.Enemy, unit, Array.Empty<int>(), action.Id, action.Attributes, action.Face,
                    action.Trait, action.Targets, action.Cost, rng, events);
            }

            // 構え works for the enemy too (roster §1.2), at the end of its own turn.
            state = CheckReserve(state, Actor.Enemy, unit, events);

            // Step 11.
            state = CheckDefeat(state, Actor.Enemy, unit, events);
            if (state.Result != GameResult.Ongoing) return state;

            // Step 12: the next omen, read from this enemy's gap as it stands now — after any move or push.
            return DecideNextOmen(state, unit, events);
        }

        // ---- Shared pieces ----

        /// <summary>§9 steps 1-3 for the player, step 9 for an enemy: the same three things in the same order.</summary>
        private static BattleState OpenTurnFor(BattleState state, Actor actor, int unit, int recovery, List<BattleEvent> events)
        {
            var self = Get(state, actor, unit);

            events.Add(new GuardCleared(actor, self.Guard) { Unit = unit });
            self = self with { Guard = 0 };

            int staminaAfter = Combat.RecoverStamina(self.Stamina, self.MaxStamina, recovery, self.NextTurnRecoveryBonus);
            events.Add(new StaminaRecovered(actor, staminaAfter - self.Stamina, staminaAfter, self.MaxStamina) { Unit = unit });
            self = self with { Stamina = staminaAfter, NextTurnRecoveryBonus = 0 };

            var ticked = self.Statuses.TickTurnStart();
            foreach (var kind in self.Statuses.Kinds)
            {
                if (Statuses.DecayOf(kind) != StatusDecay.OnTurn) continue;
                events.Add(new StatusTicked(actor, kind, ticked.Stacks(kind)) { Unit = unit });
            }
            self = self with { Statuses = ticked };

            return Set(state, actor, unit, self);
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
        ///
        /// <paramref name="unit"/> is the acting enemy in an enemy phase, and the enemy whose N and
        /// Guard the card reads on the player's turn. <paramref name="foes"/> are the enemies a
        /// player's opponent-directed faces land on — one, or every one in reach for all (§7.4); an
        /// enemy's foe is always the player. A pushed line resolves the far foe first, a pulled one
        /// the near foe first, so that nobody is blocked by a neighbour that was about to move.
        ///
        /// When an enemy falls and others still stand, it leaves the line and the card goes on; the
        /// routine stops after the face that ended the battle (§17.6 F9).
        ///
        /// §6 空振り: an enemy action whose reach does not cover N at this moment skips its
        /// opponent-directed faces (attack, push / pull, the status on the opponent) and still
        /// resolves the rest. A player card never gets here out of reach (CanPlay refuses it).
        /// </summary>
        private static BattleState Resolve(
            BattleState state,
            Actor actor,
            int unit,
            IReadOnlyList<int> foes,
            string sourceId,
            BattleAttribute attributes,
            Face face,
            Trait? trait,
            TargetKind targets,
            int cost,
            IRng rng,
            List<BattleEvent> events)
        {
            Actor foeSide = Opponent(actor);
            int gap = state.GapTo(unit);

            var self = Get(state, actor, unit);
            self = self with { Stamina = self.Stamina - cost };
            state = Set(state, actor, unit, self);
            events.Add(new StaminaSpent(actor, cost, self.Stamina) { Unit = unit });

            // The whiff is known before anything resolves, and is announced first so that whoever
            // reads the stream knows the trait below is judged on a blow that will not land.
            bool directed = EnemyAi.IsOpponentDirected(attributes, face, targets);
            bool whiff = directed && !face.ReachOrDefault.Contains(gap);
            if (whiff)
            {
                if (actor == Actor.Player)
                {
                    throw new InvalidOperationException($"Card \"{sourceId}\" was resolved out of reach; CanPlay should have refused it.");
                }
                events.Add(new ActionWhiffed(actor, sourceId, gap, face.ReachOrDefault) { Unit = unit });
            }

            // The sides the opponent-directed faces land on: (the foe's unit; ignored for the player).
            var hit = new List<int>();
            if (actor == Actor.Enemy)
            {
                if (!whiff) hit.Add(unit);
            }
            else
            {
                hit.AddRange(foes);
            }

            // Every condition reads the board from before the card: the gap before the move face,
            // the opponent's Guard before the attack face, the stamina left after the cost.
            var read = actor == Actor.Player ? state.Enemies[unit].Body : state.Player;
            var outcome = Traits.Evaluate(trait, new TraitContext(gap, read.Guard, self.Stamina));
            if (trait != null) events.Add(new TraitEvaluated(actor, sourceId, trait, outcome) { Unit = unit });
            if (outcome.NextTurnRecoveryBonus != 0)
            {
                self = self with { NextTurnRecoveryBonus = self.NextTurnRecoveryBonus + outcome.NextTurnRecoveryBonus };
                state = Set(state, actor, unit, self);
            }

            if (attributes.HasFlag(BattleAttribute.Attack) && hit.Count > 0)
            {
                events.Add(new FaceResolved(actor, sourceId, BattleAttribute.Attack) { Unit = unit });
                int raw = Combat.ComputeRawPower(face.Power, outcome.PowerBonus);
                foreach (int foe in hit)
                {
                    var other = Get(state, foeSide, foe);
                    var (damage, guardAfter, absorbed) = Combat.ApplyGuard(raw, other.Guard);
                    other = other with { Guard = guardAfter, Hp = Math.Max(0, other.Hp - damage) };
                    state = Set(state, foeSide, foe, other);
                    events.Add(new DamageDealt(actor, foeSide, raw, absorbed, damage, other.Guard, other.Hp) { Unit = foe });
                    state = OnFall(state, foeSide, foe, events, out bool over);
                    if (over) return state;
                }
            }

            if (attributes.HasFlag(BattleAttribute.Move))
            {
                events.Add(new FaceResolved(actor, sourceId, BattleAttribute.Move) { Unit = unit });
            }
            if (face.Move != 0)
            {
                // §7.3: 前へ / 後ろへ n, as far as the line allows. The mover's own 鈍足 takes one cell off.
                self = Get(state, actor, unit);
                int cells = Combat.CellsAfterSlow(Math.Abs(face.Move), self.Statuses);
                if (cells == 0)
                {
                    events.Add(new MoveBlocked(actor, StatusKind.Slow) { Unit = unit });
                }
                else
                {
                    var shift = Field.Move(state, actor, Math.Sign(face.Move) * cells, unit);
                    if (shift.To != shift.From)
                    {
                        state = Set(state, actor, unit, self with { Cell = shift.To });
                        events.Add(new CellsMoved(actor, shift.From, shift.To, Pushed: false) { Unit = unit });
                    }
                }
            }
            if (face.Push != 0 && hit.Count > 0)
            {
                // §7.3: push (away) / pull (in) the opponent; Guard does not stop it. A large opponent
                // refuses it. The cells a push could not take become wall damage; a pull just stops.
                int cells = Combat.CellsAfterSlow(Math.Abs(face.Push), Get(state, actor, unit).Statuses);
                bool blockedBySlow = false;
                foreach (int foe in PushOrder(state, foeSide, hit, face.Push))
                {
                    if (foeSide == Actor.Enemy && !state.Enemies[foe].Alive) continue;
                    var other = Get(state, foeSide, foe);
                    if (other.Size >= 2)
                    {
                        events.Add(new PushRefused(foeSide, other.Size) { Unit = foe });
                        continue;
                    }
                    if (cells == 0)
                    {
                        if (!blockedBySlow) events.Add(new MoveBlocked(actor, StatusKind.Slow) { Unit = unit });
                        blockedBySlow = true;
                        continue;
                    }

                    // A positive Push sends the opponent away from us, which is "away" from their foe (−).
                    var shift = Field.Move(state, foeSide, -Math.Sign(face.Push) * cells, foe);
                    if (shift.To != shift.From)
                    {
                        other = other with { Cell = shift.To };
                        state = Set(state, foeSide, foe, other);
                        events.Add(new CellsMoved(foeSide, shift.From, shift.To, Pushed: true) { Unit = foe });
                    }
                    if (face.Push > 0 && shift.Blocked > 0)
                    {
                        int raw = Field.WallDamage(shift.Blocked);
                        var (damage, guardAfter, absorbed) = Combat.ApplyGuard(raw, other.Guard);
                        other = other with { Guard = guardAfter, Hp = Math.Max(0, other.Hp - damage) };
                        state = Set(state, foeSide, foe, other);
                        events.Add(new WallHit(foeSide, shift.Blocked, raw, absorbed, damage, other.Guard, other.Hp) { Unit = foe });
                        state = OnFall(state, foeSide, foe, events, out bool over);
                        if (over) return state;
                    }
                }
            }

            // A Guard +n trait lands as Guard even on a card without a Guard face
            // (swordsman_cards_v4.md §1.2), and a move card may carry a small Guard of its own.
            if (attributes.HasFlag(BattleAttribute.Guard))
            {
                events.Add(new FaceResolved(actor, sourceId, BattleAttribute.Guard) { Unit = unit });
            }
            int guardGain = face.Guard + outcome.GuardBonus;
            if (guardGain > 0)
            {
                self = Get(state, actor, unit);
                self = self with { Guard = self.Guard + guardGain };
                state = Set(state, actor, unit, self);
                events.Add(new GuardGained(actor, guardGain, self.Guard) { Unit = unit });
            }

            if (attributes.HasFlag(BattleAttribute.Skill))
            {
                events.Add(new FaceResolved(actor, sourceId, BattleAttribute.Skill) { Unit = unit });
            }
            if (face.Status.HasValue && face.StatusStacks > 0)
            {
                // The slice's one word is applied to the opponent (§5). The player holds at most
                // six kinds; an enemy has no limit.
                var kind = face.Status.Value;
                int? limit = foeSide == Actor.Player ? Constants.StatusKindsPlayer : (int?)null;
                foreach (int foe in hit)
                {
                    if (foeSide == Actor.Enemy && !state.Enemies[foe].Alive) continue;
                    var other = Get(state, foeSide, foe);
                    var after = other.Statuses.Add(kind, face.StatusStacks, limit);
                    bool refused = after.Stacks(kind) == other.Statuses.Stacks(kind);
                    state = Set(state, foeSide, foe, other with { Statuses = after });
                    events.Add(new StatusApplied(actor, foeSide, kind, face.StatusStacks, after.Stacks(kind), refused) { Unit = foe });
                }
            }
            if (face.StaminaGain > 0)
            {
                self = Get(state, actor, unit);
                int staminaAfter = Math.Min(self.MaxStamina, self.Stamina + face.StaminaGain);
                events.Add(new StaminaGained(actor, staminaAfter - self.Stamina, staminaAfter) { Unit = unit });
                state = Set(state, actor, unit, self with { Stamina = staminaAfter });
            }

            if (face.Draw > 0 && actor == Actor.Player)
            {
                state = DrawCards(state, face.Draw, rng, events);
            }
            return state;
        }

        /// <summary>
        /// §17.6 F9: after a hit, see who fell. Over is true when that ended the battle (the player,
        /// or the last standing enemy); the caller stops resolving. An enemy that falls while others
        /// stand leaves its cells and its omen, and the card goes on.
        /// </summary>
        private static BattleState OnFall(BattleState state, Actor side, int unit, List<BattleEvent> events, out bool over)
        {
            over = false;
            if (!Combat.IsDefeated(Get(state, side, unit).Hp)) return state;
            if (side == Actor.Player || state.Living.Count == 0)
            {
                over = true;
                return state;
            }
            events.Add(new EnemyDefeated(Actor.Enemy) { Unit = unit });
            return state.WithOmen(unit, null);
        }

        /// <summary>A push moves the farthest foe first, a pull the nearest first; the player is a single foe.</summary>
        private static IReadOnlyList<int> PushOrder(BattleState state, Actor foeSide, IReadOnlyList<int> hit, int push)
        {
            if (foeSide == Actor.Player || hit.Count < 2) return hit;
            var order = new List<int>(hit);
            order.Sort((a, b) => state.Enemies[a].Body.Cell.CompareTo(state.Enemies[b].Body.Cell));
            if (push > 0) order.Reverse();
            return order;
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
        /// at once the player has lost: nothing in the slice can do that, and the canon does not
        /// say, so the stricter reading is taken.
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

        /// <summary>
        /// §9 step 12, for one enemy. It judges what it can pay for with the stamina it will hold when
        /// the omen is carried out — after its next recovery — so an omen only becomes a rest when
        /// something drained it in between. The canon does not fix this moment; see #70. The branch
        /// is this enemy's gap band at this moment (§6.1).
        /// </summary>
        private static BattleState DecideNextOmen(BattleState state, int unit, List<BattleEvent> events)
        {
            var enemy = state.Enemies[unit];
            int staminaThen = Combat.RecoverStamina(
                enemy.Body.Stamina, enemy.Body.MaxStamina, enemy.Def.Recovery, enemy.Body.NextTurnRecoveryBonus);

            var omen = EnemyAi.DecideOmen(enemy.Def, state.GapTo(unit), staminaThen);
            events.Add(new OmenSet(Actor.Enemy, omen, Decided: true) { Unit = unit });
            return state.WithOmen(unit, omen);
        }

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
