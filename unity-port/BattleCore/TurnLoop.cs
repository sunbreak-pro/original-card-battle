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
    /// with payable cost still cannot be released (§2.4). Cost is what playing it would take now,
    /// after a コスト −1 trait (#188).
    /// </summary>
    public sealed record PlayPreview(bool TraitHolds, bool Attacks, int RawPower, int Damage, int GuardGain, bool InReach, int Cost);

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
    /// The demo vocabulary (#188): the nine status words, the twelve trait conditions and ten
    /// effects, heal and 崩し, and the stance slot with its exile pile. Where the canon leaves a
    /// moment open the choice is written at the spot. Two-action enemies and adaptation are #189 /
    /// #50.
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
            }

            return new StepResult(state with { Phase = BattlePhase.PlayerAction }, events);
        }

        // ---- Step 6 ----

        /// <summary>
        /// Whether the card can be released now. <paramref name="target"/> is the enemy a card that
        /// aims at one enemy is dropped on (§7.4); a card aimed at all, or at nobody, ignores it.
        /// The cost is the one a コスト −1 trait leaves (#188).
        /// </summary>
        public static PlayRefusal CanPlay(BattleState state, string instanceId, int target = 0)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Result != GameResult.Ongoing) return PlayRefusal.BattleOver;
            if (state.Phase != BattlePhase.PlayerAction) return PlayRefusal.NotPlayerTurn;

            var card = FindInHand(state, instanceId);
            if (card == null) return PlayRefusal.NotInHand;
            if (!Combat.CanPay(CostNow(state, card.Def, target), state.Player.Stamina)) return PlayRefusal.NotEnoughStamina;

            return AimRefusal(state, card.Def, target);
        }

        /// <summary>
        /// What the card in the hand would cost if played now: its column's cost, less a コスト −1
        /// trait that holds on the current board (floor 0). The screen prints this, so it never has
        /// to judge the trait itself.
        /// </summary>
        public static int CostNow(BattleState state, CardDef def, int target = 0)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (def == null) throw new ArgumentNullException(nameof(def));
            int read = ReadableUnit(state, def, target);
            if (read < 0) return def.Cost;
            var outcome = Traits.EvaluateAll(def.AllTraits, PlayerContext(state, def, read));
            return Math.Max(0, def.Cost - outcome.CostDown);
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
        /// What playing this card would do right now, without playing it: whether its traits hold,
        /// the raw power, what gets past the Guard of the enemy it reads, the Guard it would give and
        /// what it would cost. The screen shows these while a card is held (the lamp, the predicted
        /// number), so it never has to work a rule out by itself. Null when the card is not in the
        /// hand. The statuses that would be spent (集中, 威圧, 強化, 脆化) and a waiting 追撃 are
        /// counted in; nothing is spent.
        /// </summary>
        public static PlayPreview? Preview(BattleState state, string instanceId, int target = 0)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var card = FindInHand(state, instanceId);
            if (card == null) return null;

            var def = card.Def;
            int read = ReadableUnit(state, def, target);
            if (read < 0) return null;
            var foe = state.Enemies[read].Body;
            var self = state.Player;
            var outcome = Traits.EvaluateAll(def.AllTraits, PlayerContext(state, def, read));
            var focus = self.Statuses.Has(StatusKind.Focus) ? FocusStep.Of(def.Attributes, def.Column, def.Face) : FocusStep.None;

            bool attacks = def.Attributes.HasFlag(BattleAttribute.Attack);
            int guardBase = def.Face.Guard + focus.Guard + outcome.GuardBonus;
            int intimidate = self.Statuses.Has(StatusKind.Intimidate) && (attacks || guardBase > 0)
                ? Constants.IntimidatePenalty : 0;

            int raw = 0;
            int damage = 0;
            if (attacks)
            {
                int conversion = outcome.Convert ? Half(def.Face.Guard + focus.Guard) : 0;
                double mult = foe.Statuses.Has(StatusKind.Fragile) ? Constants.FragileMult
                    : self.Statuses.Has(StatusKind.Empower) ? Constants.EmpowerMult
                    : 1.0;
                raw = Combat.ComputeRawPower(
                    def.Face.Power + focus.Power - intimidate,
                    outcome.PowerBonus + StanceAttackBonus(state, Actor.Player, 0, Actor.Enemy, read),
                    self.FollowUp, conversion, mult);
                damage = Combat.ApplyGuard(raw, foe.Guard).Damage;
            }
            bool inReach = AimRefusal(state, def, target) == PlayRefusal.None;
            int guardGain = Math.Max(0, guardBase - (guardBase > 0 ? intimidate : 0));
            return new PlayPreview(
                outcome.Triggered, attacks, raw, damage, guardGain, inReach,
                Math.Max(0, def.Cost - outcome.CostDown));
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
            bool directed = EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets);
            IReadOnlyList<int> foes =
                !directed ? Array.Empty<int>()
                : def.Targets == TargetKind.All ? Reached(state, def.Face.ReachOrDefault)
                : new List<int> { read };

            // Every condition reads the board from before the card leaves the hand (手薄 counts the
            // hand it leaves behind).
            var context = PlayerContext(state, def, read);

            var hand = new List<CardInstance>(state.Hand);
            hand.Remove(card);
            state = state with { Hand = hand };
            events.Add(new CardPlayed(Actor.Player, card, state.GapTo(read)) { Unit = read });

            state = Resolve(
                state, Actor.Player, read, foes, def.Id, def.Name, def.Attributes, def.Column, def.Face,
                def.AllTraits, def.Targets, def.Cost, context, rng, events);

            // §2.3 playedAttributes: what this card was, for the cards after it this turn.
            state = RecordPlayed(state, Actor.Player, 0, def.Attributes);

            // §4: a stance card goes to the exile pile and is not drawn again this battle; every
            // other card goes to the discard pile once it has resolved.
            if (def.Attributes.HasFlag(BattleAttribute.Stance))
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

            // Step 8: the whole hand goes. Nothing is kept (§17.6 F2). playedAttributes and a 追撃
            // that found no attack face go with it (§17.6 F7).
            int discarded = state.Hand.Count;
            var (emptyHand, discardPile) = Cards.DiscardHand(state.Hand, state.DiscardPile);
            state = state with
            {
                Hand = emptyHand,
                DiscardPile = discardPile,
                Player = state.Player with { Played = null, FollowUp = 0 },
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
                var context = EnemyContext(state, unit, action);
                state = Resolve(
                    state, Actor.Enemy, unit, Array.Empty<int>(), action.Id, action.Name, action.Attributes,
                    action.Column, action.Face, TraitsOf(action.Trait), action.Targets, action.Cost, context, rng, events);
                if (!state.Enemies[unit].Alive)
                {
                    // It fell to its own blow's return (見切り). The battle goes on without it, or ends here.
                    return state.Living.Count == 0 ? CheckDefeat(state, Actor.Enemy, unit, events) : state;
                }
                state = RecordPlayed(state, Actor.Enemy, unit, action.Attributes);
            }

            // The stance's turn-end effect, then 構え, which works for the enemy too (roster §1.2).
            state = StanceAtTurnEnd(state, Actor.Enemy, unit, events);
            state = CheckReserve(state, Actor.Enemy, unit, events);
            state = state.WithEnemy(unit, state.Enemies[unit].Body with { Played = null, FollowUp = 0 });

            // Step 11.
            state = CheckDefeat(state, Actor.Enemy, unit, events);
            if (state.Result != GameResult.Ongoing) return state;

            // Step 12: the next omen, read from this enemy's gap as it stands now — after any move or push.
            return DecideNextOmen(state, unit, events);
        }

        // ---- Shared pieces ----

        /// <summary>
        /// §9 steps 1-3 for the player, step 9 for an enemy: the same things in the same order.
        /// Step 2 is the recovery, less 1 for 疲労 and plus a turn-start stance's recovery (水の構え);
        /// step 3 is the stance's Guard, then 再生, then 出血, then the ターンで減る型 ticks. The
        /// holder may fall to 出血 here; the caller looks.
        /// </summary>
        private static BattleState OpenTurnFor(BattleState state, Actor actor, int unit, int recovery, List<BattleEvent> events)
        {
            var self = Get(state, actor, unit);

            events.Add(new GuardCleared(actor, self.Guard) { Unit = unit });
            self = self with { Guard = 0, Played = null };

            var stance = self.Stance;
            bool stanceFires = stance != null && stance.Hook == StanceHook.TurnStart && StanceHolds(state, actor, unit, stance);
            if (stanceFires) events.Add(new StanceFired(actor, self.StanceSource ?? "", StanceHook.TurnStart) { Unit = unit });

            int fatigue = self.Statuses.Has(StatusKind.Fatigue) ? Constants.FatiguePenalty : 0;
            int stanceRecovery = stanceFires ? stance!.Recovery : 0;
            int staminaAfter = Combat.RecoverStamina(
                self.Stamina, self.MaxStamina, recovery + stanceRecovery - fatigue, self.NextTurnRecoveryBonus);
            events.Add(new StaminaRecovered(actor, staminaAfter - self.Stamina, staminaAfter, self.MaxStamina) { Unit = unit });
            self = self with { Stamina = staminaAfter, NextTurnRecoveryBonus = 0 };

            if (stanceFires && stance!.Guard > 0)
            {
                self = self with { Guard = self.Guard + stance.Guard };
                events.Add(new GuardGained(actor, stance.Guard, self.Guard) { Unit = unit });
            }

            int regen = self.Statuses.Stacks(StatusKind.Regen);
            if (regen > 0)
            {
                int hpAfter = Math.Min(self.MaxHp, self.Hp + (regen * Constants.RegenPerStack));
                events.Add(new StatusHpChanged(actor, StatusKind.Regen, hpAfter - self.Hp, hpAfter) { Unit = unit });
                self = self with { Hp = hpAfter };
            }
            int bleed = self.Statuses.Stacks(StatusKind.Bleed);
            if (bleed > 0)
            {
                int hpAfter = Math.Max(0, self.Hp - (bleed * Constants.BleedPerStack));
                events.Add(new StatusHpChanged(actor, StatusKind.Bleed, hpAfter - self.Hp, hpAfter) { Unit = unit });
                self = self with { Hp = hpAfter };
            }

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
        /// §2.2: pay, judge the traits once, then Attack → Move → Guard → Skill → Stance. The same
        /// routine resolves a card and an enemy action, because both are written off the same face
        /// table.
        ///
        /// <paramref name="unit"/> is the acting enemy in an enemy phase, and the enemy whose N and
        /// Guard the card reads on the player's turn. <paramref name="foes"/> are the enemies a
        /// player's opponent-directed faces land on — none for a card aimed at nobody, one, or every
        /// one in reach for all (§7.4); an enemy's foe is always the player. A pushed line resolves
        /// the far foe first, a pulled one the near foe first, so that nobody is blocked by a
        /// neighbour that was about to move.
        ///
        /// When an enemy falls and others still stand, it leaves the line and the card goes on; the
        /// routine stops after the face that ended the battle (§17.6 F9), and when the one acting
        /// falls to a 見切り return.
        ///
        /// §6 空振り: an enemy action whose reach does not cover N at this moment skips its
        /// opponent-directed faces (attack and 崩し, push / pull, the statuses on the opponent) and
        /// still resolves the rest. A player card never gets here out of reach (CanPlay refuses it).
        ///
        /// The statuses the one acting holds work here: 集中 (the player's next card, one column to
        /// the right — the demo reads it as the scale's step added to power, Guard and heal), 威圧
        /// (−3 on power and Guard, spent when there is either), 強化 (×1.5 on the attack face) and a
        /// waiting 追撃. On the one hit: 脆化 (×1.5, taken before 強化 — §19.5 S13, one multiplier per
        /// blow) and 見切り.
        /// </summary>
        private static BattleState Resolve(
            BattleState state,
            Actor actor,
            int unit,
            IReadOnlyList<int> foes,
            string sourceId,
            string sourceName,
            BattleAttribute attributes,
            int column,
            Face face,
            IReadOnlyList<Trait> traits,
            TargetKind targets,
            int cost,
            TraitContext context,
            IRng rng,
            List<BattleEvent> events)
        {
            Actor foeSide = Opponent(actor);
            int gap = state.GapTo(unit);

            // The traits are judged on the board from before the card (the context), before any face.
            var outcome = Traits.EvaluateAll(traits, context);
            int paid = Math.Max(0, cost - outcome.CostDown);

            var self = Get(state, actor, unit);
            self = self with { Stamina = self.Stamina - paid };
            state = Set(state, actor, unit, self);
            events.Add(new StaminaSpent(actor, paid, self.Stamina) { Unit = unit });

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
                if (directed && !whiff) hit.Add(unit);
            }
            else
            {
                hit.AddRange(foes);
            }

            foreach (var trait in traits)
            {
                events.Add(new TraitEvaluated(actor, sourceId, trait, Traits.Evaluate(trait, context)) { Unit = unit });
            }
            if (outcome.NextTurnRecoveryBonus != 0)
            {
                self = Get(state, actor, unit);
                self = self with { NextTurnRecoveryBonus = self.NextTurnRecoveryBonus + outcome.NextTurnRecoveryBonus };
                state = Set(state, actor, unit, self);
            }
            if (outcome.StaminaGain > 0) state = GainStamina(state, actor, unit, outcome.StaminaGain, events);

            // 集中: the player's next card, whatever it is. 威圧: the next action that has a power or a Guard.
            var focus = FocusStep.None;
            if (actor == Actor.Player && Get(state, actor, unit).Statuses.Has(StatusKind.Focus))
            {
                focus = FocusStep.Of(attributes, column, face);
                state = Consume(state, actor, unit, StatusKind.Focus, events);
            }
            bool attacks = attributes.HasFlag(BattleAttribute.Attack) && hit.Count > 0;
            int guardBase = face.Guard + focus.Guard + outcome.GuardBonus;
            int intimidate = 0;
            if (Get(state, actor, unit).Statuses.Has(StatusKind.Intimidate) && (attacks || guardBase > 0))
            {
                intimidate = Constants.IntimidatePenalty;
                state = Consume(state, actor, unit, StatusKind.Intimidate, events);
            }

            if (attacks)
            {
                events.Add(new FaceResolved(actor, sourceId, BattleAttribute.Attack) { Unit = unit });
                self = Get(state, actor, unit);
                int followUp = self.FollowUp;
                if (followUp > 0) state = Set(state, actor, unit, self with { FollowUp = 0 });
                int conversion = outcome.Convert ? Half(face.Guard + focus.Guard) : 0;
                bool empowered = self.Statuses.Has(StatusKind.Empower);
                bool empowerUsed = false;

                foreach (int foe in hit)
                {
                    if (foeSide == Actor.Enemy && !state.Enemies[foe].Alive) continue;
                    var other = Get(state, foeSide, foe);

                    int stanceBonus = StanceAttackBonus(state, actor, unit, foeSide, foe);
                    if (stanceBonus > 0)
                    {
                        events.Add(new StanceFired(actor, Get(state, actor, unit).StanceSource ?? "", StanceHook.AttackBonus) { Unit = foeSide == Actor.Enemy ? foe : unit });
                    }

                    // §19.5 S13: one multiplier per blow, 脆化 first.
                    double mult = 1.0;
                    if (other.Statuses.Has(StatusKind.Fragile))
                    {
                        mult = Constants.FragileMult;
                        state = Consume(state, foeSide, foe, StatusKind.Fragile, events);
                        other = Get(state, foeSide, foe);
                    }
                    else if (empowered)
                    {
                        mult = Constants.EmpowerMult;
                        empowerUsed = true;
                    }

                    int raw = Combat.ComputeRawPower(
                        face.Power + focus.Power - intimidate, outcome.PowerBonus + stanceBonus, followUp, conversion, mult);
                    var (damage, guardAfter, absorbed) = Combat.ApplyGuard(raw, other.Guard);
                    other = other with { Guard = guardAfter, Hp = Math.Max(0, other.Hp - damage) };
                    state = Set(state, foeSide, foe, other);
                    events.Add(new DamageDealt(actor, foeSide, raw, absorbed, damage, other.Guard, other.Hp) { Unit = foeSide == Actor.Enemy ? foe : unit });

                    if (!Combat.IsDefeated(other.Hp))
                    {
                        state = OnHitReactions(state, actor, unit, foeSide, foe, absorbed, events);
                    }
                    state = OnFall(state, foeSide, foe, events, out bool over);
                    if (over) return state;

                    // 見切り may have taken the attacker down.
                    if (Combat.IsDefeated(Get(state, actor, unit).Hp))
                    {
                        state = OnFall(state, actor, unit, events, out _);
                        return state;
                    }

                    int broken = face.Break + outcome.BreakBonus;
                    if (broken > 0 && (foeSide == Actor.Player || state.Enemies[foe].Alive))
                    {
                        state = Break(state, actor, foeSide, foe, broken, foeSide == Actor.Enemy ? foe : unit, events);
                    }
                }

                if (empowerUsed) state = Consume(state, actor, unit, StatusKind.Empower, events);
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

                        // 根縛り: an enemy that moves itself pays for it while the player holds the stance.
                        var bind = state.Player.Stance;
                        if (actor == Actor.Enemy && bind != null && bind.Hook == StanceHook.BreakOnFoeMove && bind.Break > 0)
                        {
                            events.Add(new StanceFired(Actor.Player, state.Player.StanceSource ?? "", StanceHook.BreakOnFoeMove) { Unit = unit });
                            state = Break(state, Actor.Player, Actor.Enemy, unit, bind.Break, unit, events);
                        }
                    }
                }
            }
            if (face.Push != 0 && hit.Count > 0)
            {
                // §7.3: push (away) / pull (in) the opponent; Guard does not stop it. A large opponent
                // refuses it, and so does one standing in 錨の構え. The cells a push could not take
                // become wall damage; a pull just stops.
                int cells = Combat.CellsAfterSlow(Math.Abs(face.Push), Get(state, actor, unit).Statuses);
                bool blockedBySlow = false;
                foreach (int foe in PushOrder(state, foeSide, hit, face.Push))
                {
                    if (foeSide == Actor.Enemy && !state.Enemies[foe].Alive) continue;
                    var other = Get(state, foeSide, foe);
                    int eventUnit = foeSide == Actor.Enemy ? foe : unit;
                    if (other.Size >= 2)
                    {
                        events.Add(new PushRefused(foeSide, other.Size) { Unit = eventUnit });
                        continue;
                    }
                    if (other.Stance != null && other.Stance.Hook == StanceHook.PushImmune)
                    {
                        events.Add(new StanceFired(foeSide, other.StanceSource ?? "", StanceHook.PushImmune) { Unit = eventUnit });
                        events.Add(new PushRefused(foeSide, other.Size) { Unit = eventUnit, ByStance = true });
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
            int guardGain = guardBase > 0 ? Math.Max(0, guardBase - intimidate) : 0;
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
            int heal = face.Heal > 0 ? face.Heal + focus.Heal : 0;
            if (heal > 0)
            {
                self = Get(state, actor, unit);
                int hpAfter = Math.Min(self.MaxHp, self.Hp + heal);
                state = Set(state, actor, unit, self with { Hp = hpAfter });
                events.Add(new Healed(actor, hpAfter - self.Hp, hpAfter) { Unit = unit });
            }

            // §5: the face's statuses, then the trait's. The player holds at most six kinds; an enemy
            // has no limit. Statuses on the opponent need the blow to have reached them.
            var grants = new List<StatusGrant>(face.StatusList);
            grants.AddRange(outcome.GrantList);
            foreach (var grant in grants)
            {
                if (grant.Stacks <= 0) continue;
                if (grant.OnSelf)
                {
                    state = ApplyStatus(state, actor, actor, unit, unit, grant.Kind, grant.Stacks, events);
                    continue;
                }
                foreach (int foe in hit)
                {
                    if (foeSide == Actor.Enemy && !state.Enemies[foe].Alive) continue;
                    state = ApplyStatus(state, actor, foeSide, foe, foeSide == Actor.Enemy ? foe : unit, grant.Kind, grant.Stacks, events);
                }
            }
            if (face.StaminaGain > 0) state = GainStamina(state, actor, unit, face.StaminaGain, events);

            // §4: the stance face takes the one slot; a stance already there ends.
            if (attributes.HasFlag(BattleAttribute.Stance))
            {
                events.Add(new FaceResolved(actor, sourceId, BattleAttribute.Stance) { Unit = unit });
            }
            if (face.Stance != null)
            {
                self = Get(state, actor, unit);
                string? replaced = self.StanceSource;
                state = Set(state, actor, unit, self with { Stance = face.Stance, StanceSource = sourceId, StanceReactedTurn = 0 });
                events.Add(new StanceSet(actor, sourceId, sourceName, face.Stance, replaced) { Unit = unit });
            }

            int draw = face.Draw + outcome.Draw;
            if (draw > 0 && actor == Actor.Player)
            {
                state = DrawCards(state, draw, rng, events);
            }

            // 追撃 waits for the next attack face, not this card's own (§2.3).
            if (outcome.FollowUp > 0)
            {
                self = Get(state, actor, unit);
                state = Set(state, actor, unit, self with { FollowUp = self.FollowUp + outcome.FollowUp });
            }
            return state;
        }

        /// <summary>
        /// What the one hit does back, once the blow has landed and it still stands: 見切り returns
        /// half of what its Guard absorbed (rounded up; spent only when there was something to
        /// return), then an OnHit stance reacts (司祭の祈り, 槍衾).
        /// </summary>
        private static BattleState OnHitReactions(
            BattleState state, Actor attacker, int attackerUnit, Actor victim, int victimUnit, int absorbed, List<BattleEvent> events)
        {
            int enemyUnit = victim == Actor.Enemy ? victimUnit : attackerUnit;

            var hurt = Get(state, victim, victimUnit);
            if (hurt.Statuses.Has(StatusKind.Parry) && absorbed > 0)
            {
                state = Consume(state, victim, victimUnit, StatusKind.Parry, events);
                int raw = Half(absorbed);
                var striker = Get(state, attacker, attackerUnit);
                var (damage, guardAfter, soaked) = Combat.ApplyGuard(raw, striker.Guard);
                striker = striker with { Guard = guardAfter, Hp = Math.Max(0, striker.Hp - damage) };
                state = Set(state, attacker, attackerUnit, striker);
                events.Add(new Reflected(victim, attacker, raw, soaked, damage, striker.Guard, striker.Hp) { Unit = enemyUnit });
            }

            hurt = Get(state, victim, victimUnit);
            var stance = hurt.Stance;
            if (stance == null || stance.Hook != StanceHook.OnHit) return state;
            if (stance.OncePerTurn && hurt.StanceReactedTurn == state.Turn) return state;

            events.Add(new StanceFired(victim, hurt.StanceSource ?? "", StanceHook.OnHit) { Unit = enemyUnit });
            state = Set(state, victim, victimUnit, hurt with { StanceReactedTurn = state.Turn });
            if (stance.Stamina > 0) state = GainStamina(state, victim, victimUnit, stance.Stamina, events);
            if (stance.Guard > 0)
            {
                hurt = Get(state, victim, victimUnit);
                hurt = hurt with { Guard = hurt.Guard + stance.Guard };
                state = Set(state, victim, victimUnit, hurt);
                events.Add(new GuardGained(victim, stance.Guard, hurt.Guard) { Unit = enemyUnit });
            }
            if (stance.Status.HasValue && stance.StatusStacks > 0 && !Combat.IsDefeated(Get(state, attacker, attackerUnit).Hp))
            {
                state = ApplyStatus(state, victim, attacker, attackerUnit, enemyUnit, stance.Status.Value, stance.StatusStacks, events);
            }
            return state;
        }

        /// <summary>§4: the turn-end stance (根渡り, 霞み足), before 構え.</summary>
        private static BattleState StanceAtTurnEnd(BattleState state, Actor actor, int unit, List<BattleEvent> events)
        {
            var self = Get(state, actor, unit);
            var stance = self.Stance;
            if (stance == null || stance.Hook != StanceHook.TurnEnd || !StanceHolds(state, actor, unit, stance)) return state;

            events.Add(new StanceFired(actor, self.StanceSource ?? "", StanceHook.TurnEnd) { Unit = unit });
            if (stance.Guard > 0)
            {
                self = self with { Guard = self.Guard + stance.Guard };
                events.Add(new GuardGained(actor, stance.Guard, self.Guard) { Unit = unit });
            }
            if (stance.NextRecovery > 0) self = self with { NextTurnRecoveryBonus = self.NextTurnRecoveryBonus + stance.NextRecovery };
            return Set(state, actor, unit, self);
        }

        /// <summary>
        /// §4 `when` for the stances that do not look at one foe: the gap is the nearest enemy's for
        /// the player and the player's for an enemy.
        /// </summary>
        private static bool StanceHolds(BattleState state, Actor actor, int unit, StanceDef stance)
        {
            switch (stance.When)
            {
                case StanceWhen.Always: return true;
                case StanceWhen.GapAtLeast: return OwnGap(state, actor, unit) >= stance.Threshold;
                case StanceWhen.GapAtMost: return OwnGap(state, actor, unit) <= stance.Threshold;
                case StanceWhen.MovedThisTurn: return PlayedMove(Get(state, actor, unit));
                case StanceWhen.OmenAttack:
                    if (actor != Actor.Player) return false;
                    foreach (int i in state.Living)
                    {
                        var omen = state.Enemies[i].Omen;
                        if (omen != null && omen.Label.Kind == OmenKind.Attack) return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>§4 条件付き加算: the power an AttackBonus stance adds to a blow on this foe. 0 when it does not hold.</summary>
        private static int StanceAttackBonus(BattleState state, Actor actor, int unit, Actor foeSide, int foe)
        {
            var self = Get(state, actor, unit);
            var stance = self.Stance;
            if (stance == null || stance.Hook != StanceHook.AttackBonus) return 0;
            int gap = actor == Actor.Player ? state.GapTo(foe) : state.GapTo(unit);
            bool holds = stance.When switch
            {
                StanceWhen.Always => true,
                StanceWhen.GapAtMost => gap <= stance.Threshold,
                StanceWhen.GapAtLeast => gap >= stance.Threshold,
                StanceWhen.MovedThisTurn => PlayedMove(self),
                StanceWhen.TargetHasStatus => stance.Status.HasValue && Get(state, foeSide, foe).Statuses.Has(stance.Status.Value),
                _ => false,
            };
            return holds ? stance.Power : 0;
        }

        private static bool PlayedMove(CombatantState self)
        {
            foreach (var attributes in self.PlayedThisTurn)
            {
                if (attributes.HasFlag(BattleAttribute.Move)) return true;
            }
            return false;
        }

        private static int OwnGap(BattleState state, Actor actor, int unit)
        {
            if (actor == Actor.Enemy) return state.GapTo(unit);
            int nearest = state.Nearest;
            return nearest < 0 ? 0 : state.GapTo(nearest);
        }

        private static BattleState GainStamina(BattleState state, Actor actor, int unit, int amount, List<BattleEvent> events)
        {
            var self = Get(state, actor, unit);
            int staminaAfter = Math.Min(self.MaxStamina, self.Stamina + amount);
            events.Add(new StaminaGained(actor, staminaAfter - self.Stamina, staminaAfter) { Unit = unit });
            return Set(state, actor, unit, self with { Stamina = staminaAfter });
        }

        /// <summary>崩し: <paramref name="target"/> loses stamina (floor 0).</summary>
        private static BattleState Break(
            BattleState state, Actor breaker, Actor target, int targetUnit, int amount, int eventUnit, List<BattleEvent> events)
        {
            var other = Get(state, target, targetUnit);
            int staminaAfter = Math.Max(0, other.Stamina - amount);
            events.Add(new StaminaBroken(breaker, target, other.Stamina - staminaAfter, staminaAfter) { Unit = eventUnit });
            return Set(state, target, targetUnit, other with { Stamina = staminaAfter });
        }

        /// <summary>§5 使うと減る型: one stack spent.</summary>
        private static BattleState Consume(BattleState state, Actor holder, int unit, StatusKind kind, List<BattleEvent> events)
        {
            var self = Get(state, holder, unit);
            var after = self.Statuses.Consume(kind);
            events.Add(new StatusConsumed(holder, kind, after.Stacks(kind)) { Unit = unit });
            return Set(state, holder, unit, self with { Statuses = after });
        }

        /// <summary>§5: stacks on one side; the player takes on at most six kinds, an enemy any number.</summary>
        private static BattleState ApplyStatus(
            BattleState state, Actor by, Actor target, int targetUnit, int eventUnit, StatusKind kind, int stacks, List<BattleEvent> events)
        {
            int? limit = target == Actor.Player ? Constants.StatusKindsPlayer : (int?)null;
            var other = Get(state, target, targetUnit);
            var after = other.Statuses.Add(kind, stacks, limit);
            bool refused = after.Stacks(kind) == other.Statuses.Stacks(kind);
            events.Add(new StatusApplied(by, target, kind, stacks, after.Stacks(kind), refused) { Unit = eventUnit });
            return Set(state, target, targetUnit, other with { Statuses = after });
        }

        private static BattleState RecordPlayed(BattleState state, Actor actor, int unit, BattleAttribute attributes)
        {
            var self = Get(state, actor, unit);
            var played = new List<BattleAttribute>(self.PlayedThisTurn) { attributes };
            return Set(state, actor, unit, self with { Played = played });
        }

        /// <summary>Half, rounded up: 転換 and 見切り.</summary>
        private static int Half(int value) => value <= 0 ? 0 : (value + 1) / 2;

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

        /// <summary>
        /// §9 step 12, for one enemy. It judges what it can pay for with the stamina it will hold when
        /// the omen is carried out — after its next recovery — so an omen only becomes a rest when
        /// something drained it in between. The canon does not fix this moment; see #70. The branch
        /// is this enemy's gap band at this moment (§6.1).
        /// </summary>
        private static BattleState DecideNextOmen(BattleState state, int unit, List<BattleEvent> events)
        {
            // 疲労 it already holds will take its 1 off that recovery too (§9 step 9); one put on it
            // later is what "drained in between" means.
            var enemy = state.Enemies[unit];
            int fatigue = enemy.Body.Statuses.Has(StatusKind.Fatigue) ? Constants.FatiguePenalty : 0;
            int staminaThen = Combat.RecoverStamina(
                enemy.Body.Stamina, enemy.Body.MaxStamina, enemy.Def.Recovery - fatigue, enemy.Body.NextTurnRecoveryBonus);

            var omen = EnemyAi.DecideOmen(enemy.Def, state.GapTo(unit), staminaThen);
            events.Add(new OmenSet(Actor.Enemy, omen, Decided: true) { Unit = unit });
            return state.WithOmen(unit, omen);
        }

        // ---- What a card or action reads (§2.3) ----

        /// <summary>The board a player's card reads, judged against enemy <paramref name="read"/>. The card is still in the hand.</summary>
        private static TraitContext PlayerContext(BattleState state, CardDef def, int read)
        {
            var foe = state.Enemies[read];
            var self = state.Player;
            return new TraitContext(
                Gap: state.GapTo(read),
                OpponentGuard: foe.Body.Guard,
                StaminaAfterUse: self.Stamina - def.Cost,
                StaminaBefore: self.Stamina,
                OpponentStamina: foe.Body.Stamina,
                Played: self.PlayedThisTurn,
                HandAfterPlay: state.Hand.Count - 1,
                OpponentOmen: foe.Omen?.Label.Kind,
                SelfStatuses: self.Statuses,
                OpponentStatuses: foe.Body.Statuses,
                Attributes: def.Attributes);
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
                Attributes: action.Attributes);
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

    /// <summary>
    /// §5 集中, the demo's reading (#188): the next card resolves one column to the right, taken as
    /// the step to the next column on each scale the card is written from — added to its power, its
    /// Guard and its heal. A face only grows on the attribute that owns it (a move card's small
    /// Guard does not), and column 4 does not grow. Status stacks are left as they are.
    /// </summary>
    public sealed record FocusStep(int Power, int Guard, int Heal)
    {
        public static readonly FocusStep None = new FocusStep(0, 0, 0);

        public static FocusStep Of(BattleAttribute attributes, int column, Face face)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            if (!Columns.IsValid(column)) return None;
            bool single = attributes == BattleAttribute.Attack || attributes == BattleAttribute.Guard
                || attributes == BattleAttribute.Skill;
            int power = attributes.HasFlag(BattleAttribute.Attack) && face.Power > 0
                ? Columns.StepRight(single ? Columns.SingleAttackPower : Columns.DualAttackPower, column) : 0;
            int guard = attributes.HasFlag(BattleAttribute.Guard) && face.Guard > 0
                ? Columns.StepRight(single ? Columns.SingleGuard : Columns.DualGuard, column) : 0;
            int heal = attributes.HasFlag(BattleAttribute.Skill) && face.Heal > 0
                ? Columns.StepRight(single ? Columns.SingleHeal : Columns.DualHeal, column) : 0;
            return new FocusStep(power, guard, heal);
        }
    }
}
