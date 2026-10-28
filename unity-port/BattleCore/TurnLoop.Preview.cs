using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// Step 6 before a card is released (#584): whether a card can be played and what it costs
    /// (<see cref="CanPlay"/>, <see cref="CostNow"/>), and what a held card or an enemy's shown omen
    /// would do, worked out on a copy for the screen (<see cref="Preview"/>, <see cref="PreviewOmen"/>).
    /// </summary>
    public static partial class TurnLoop
    {
        /// <summary>
        /// Whether the card can be released now. <paramref name="target"/> is the enemy a card that
        /// aims at one enemy is dropped on (§7.4); a card aimed at all, or at nobody, ignores it.
        /// The cost is the one a コスト −1 trait (#188) and a コストの割引 stance (§4) leave: <see cref="CostNow"/>.
        /// </summary>
        public static PlayRefusal CanPlay(BattleState state, string instanceId, int target = 0)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Result != GameResult.Ongoing) return PlayRefusal.BattleOver;
            if (state.Phase != BattlePhase.PlayerAction) return PlayRefusal.NotPlayerTurn;

            var card = FindInHand(state, instanceId);
            if (card == null) return PlayRefusal.NotInHand;
            if (!Combat.CanPay(CostNow(state, card.Def, target), state.Player.Stamina)) return PlayRefusal.NotEnoughStamina;

            // roster §4.1 呪縛 (#51): 「移動が中心の札は使えない」.
            if (state.Player.Statuses.Has(StatusKind.Binding) && AttributeRule.IsMovementCard(card.Def.Attributes, card.Def.Face))
            {
                return PlayRefusal.Bound;
            }

            return AimRefusal(state, card.Def, target);
        }

        /// <summary>
        /// What the card in the hand would cost if played now: its column's cost, less a コスト −1
        /// trait that holds on the current board (floor 0), less 1 more when a コストの割引 stance
        /// (§4, <see cref="StanceHook.CostDiscount"/>) has a turn's discount left for the attribute
        /// the card counts as. The screen prints this, so it never has to judge the trait or the
        /// stance itself.
        /// </summary>
        public static int CostNow(BattleState state, CardDef def, int target = 0)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (def == null) throw new ArgumentNullException(nameof(def));
            int read = ReadableUnit(state, def, target);
            // No enemy stands (the battle is over): the column's cost, as Preview's null leaves it on screen.
            if (read < 0) return def.Cost;
            var outcome = Traits.EvaluateAll(def.AllTraits, PlayerContext(state, def, read));
            return Discounted(state, def, Math.Max(0, def.Cost - outcome.CostDown));
        }

        /// <summary>
        /// §4 コストの割引: <paramref name="cost"/> (the column's, after the card's own コスト −1)
        /// less 1 when a <see cref="StanceHook.CostDiscount"/> entry would be spent on this card.
        /// </summary>
        private static int Discounted(BattleState state, CardDef def, int cost) =>
            CostDiscountEntry(state, def, cost) >= 0 ? cost - 1 : cost;

        /// <summary>
        /// §4 コストの割引 (鉄壁の構え): the index on the player's list of the entry a play of
        /// <paramref name="def"/> would spend, or −1. That is the oldest CostDiscount entry for the
        /// attribute the card counts as (folded, §2.1) that has not taken a cost down this turn
        /// (<see cref="StanceEntry.ReactedTurn"/>). One entry a card, so two entries make two cards
        /// a turn cheaper (§4: 「毎ターン 1 回、コスト −1」が 2 つなら 1 ターンに 2 回). None when
        /// <paramref name="cost"/> is already 0: an entry is not spent on a card it cannot make
        /// cheaper. 温存 reads the stamina left after this discount (<see cref="PlayerContext"/>).
        /// </summary>
        private static int CostDiscountEntry(BattleState state, CardDef def, int cost)
        {
            if (cost <= 0) return -1;
            var stances = state.Player.StanceList;
            for (int i = 0; i < stances.Count; i++)
            {
                var entry = stances[i];
                if (entry.Def.Hook != StanceHook.CostDiscount || entry.Def.Attribute != def.Attribute) continue;
                if (entry.ReactedTurn == state.Turn || entry.StoppedIn(state.Turn)) continue;
                return i;
            }
            return -1;
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
        ///
        /// The power and the damage (#248) come from resolving the card for real on a copy of the
        /// board, so a face of several blows, the 脆化 its first blow leaves, and the wall damage of a
        /// push add up exactly as <see cref="PlayCard"/> would. The copy has the stamina to pay even
        /// when the player lacks it, and keeps every enemy standing so that a blow that would fell
        /// one does not hide the rest of the card's power.
        /// </summary>
        public static PlayPreview? Preview(BattleState state, string instanceId, int target = 0)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var card = FindInHand(state, instanceId);
            if (card == null) return null;

            var def = card.Def;
            int read = ReadableUnit(state, def, target);
            if (read < 0) return null;
            var self = state.Player;
            var context = PlayerContext(state, def, read);
            var outcome = Traits.EvaluateAll(def.AllTraits, context);
            var focus = self.Statuses.Has(StatusKind.Focus) ? FocusStep.Of(def.Attributes, def.Column, def.Face) : FocusStep.None;

            bool attacks = def.Attributes.HasFlag(BattleAttribute.Attack);
            int guardBase = def.Face.Guard + focus.Guard + outcome.GuardBonus;
            int intimidate = self.Statuses.Has(StatusKind.Intimidate) && (attacks || guardBase > 0)
                ? Constants.IntimidatePenalty : 0;

            bool inReach = AimRefusal(state, def, target) == PlayRefusal.None;
            bool directed = EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets);
            int raw = 0;
            int damage = 0;
            if (directed && inReach)
            {
                (raw, damage) = ResolvedOnCopy(state, card, read, context);
            }
            else if (attacks)
            {
                (raw, damage) = OneBlow(state, def, read, outcome, focus, intimidate);
            }
            int guardGain = Math.Max(0, guardBase - (guardBase > 0 ? intimidate : 0));
            return new PlayPreview(
                outcome.Triggered, attacks, raw, damage, guardGain, inReach,
                Discounted(state, def, Math.Max(0, def.Cost - outcome.CostDown)));
        }

        /// <summary>
        /// <see cref="PlayCard"/>'s own steps on a copy: the card leaves the hand and resolves. What
        /// lands on enemy <paramref name="read"/> is summed — its blows (<see cref="DamageDealt"/>)
        /// and the wall it is pushed into (<see cref="WallHit"/>). <paramref name="context"/> is read
        /// from the real board, so a trait that reads stamina is judged on what the player holds.
        /// </summary>
        private static (int Raw, int Damage) ResolvedOnCopy(BattleState state, CardInstance card, int read, TraitContext context)
        {
            var def = card.Def;
            var copy = KeepStanding(state, Actor.Enemy, read);
            copy = copy with { Player = copy.Player with { Stamina = Math.Max(copy.Player.Stamina, def.Cost) } };
            IReadOnlyList<int> foes = def.Targets == TargetKind.All
                ? Reached(copy, def.Face.ReachOrDefault)
                : new List<int> { read };

            var hand = new List<CardInstance>(copy.Hand);
            hand.Remove(card);
            copy = copy with { Hand = hand };

            var events = new List<BattleEvent>();
            Resolve(copy, new Play(Actor.Player, read, foes, CardSource(def, def.Cost), context), new SeededRng(0), events);
            return Landed(events, Actor.Player, Actor.Enemy, read);
        }

        /// <summary>
        /// The one blow a card out of reach would deal, worked out without resolving: it cannot be
        /// resolved there (<see cref="Resolve"/> refuses a player card out of reach).
        /// </summary>
        private static (int Raw, int Damage) OneBlow(
            BattleState state, CardDef def, int read, TraitOutcome outcome, FocusStep focus, int intimidate)
        {
            var foe = state.Enemies[read].Body;
            var self = state.Player;
            int conversion = outcome.Convert ? Half(def.Face.Guard + focus.Guard) : 0;
            double mult = foe.Statuses.Has(StatusKind.Fragile) ? Constants.FragileMult
                : self.Statuses.Has(StatusKind.Empower) ? Constants.EmpowerMult
                : 1.0;
            int raw = Combat.ComputeRawPower(
                def.Face.Power + focus.Power - intimidate,
                outcome.PowerBonus + StanceAttackBonus(state, Actor.Player, 0, Actor.Enemy, read),
                self.FollowUp, conversion, mult);
            return (raw, Combat.ApplyGuard(raw, foe.Guard).Damage);
        }

        /// <summary>
        /// What enemy <paramref name="unit"/>'s shown omen would do to the player if the player ended
        /// the turn now (#248): one number for the whole action — every blow, with the 脆化 its first
        /// blow leaves, and the wall damage of its push. Like <see cref="Preview"/> it resolves the
        /// action on a copy, after the steps that come before it in the same order as
        /// <see cref="EndTurn"/>: the player's turn-end stance and 構え (step 7; its Guard can take
        /// 無防備 away), the played list cleared (step 8), and this enemy's own turn start (step 9:
        /// its Guard reset, recovery and status ticks). Enemies that act before it are not played.
        ///
        /// The action is the one <see cref="EnemyAi.ActionToExecute"/> would carry out with the
        /// stamina the enemy holds after that turn start, so only a drained enemy rests; one that
        /// falls to 出血 there counts as a rest too. An elite's or a boss's second action is not on
        /// the omen and is not counted.
        ///
        /// When the action does not reach the player where they stand, Lands is false and the copy
        /// puts the player on the free cell nearest their own from which it would, so the number
        /// still says what it would do if it landed. With no such cell it falls back to the face
        /// alone (power × hits). Null when the enemy is not standing or has no omen.
        ///
        /// Call it on a state from the player's turn (<see cref="BattlePhase.PlayerAction"/>): it plays
        /// that turn's end itself, so a state past it would have the turn end played twice.
        /// </summary>
        public static OmenPreview? PreviewOmen(BattleState state, int unit)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!IsStanding(state, unit)) return null;
            var enemy = state.Enemies[unit];
            if (enemy.Omen == null) return null;

            // Steps 7-9 on a copy, as EndTurn and EnemyPhase run them. What they emit is not wanted.
            var events = new List<BattleEvent>();
            var copy = StanceAtTurnEnd(state, Actor.Player, 0, events);
            copy = CheckReserve(copy, Actor.Player, 0, events);
            copy = copy with { Player = copy.Player with { Played = null, Moved = false, LastMove = 0, FollowUp = 0 } };
            copy = OpenTurnFor(copy, Actor.Enemy, unit, enemy.Def.Recovery, events);
            if (!copy.Enemies[unit].Alive) return new OmenPreview(0, 0, Lands: false, Rests: true);
            copy = Sway(copy, unit, events);

            var action = EnemyAi.ActionToExecute(
                enemy.Def, enemy.Omen, copy.Enemies[unit].Body.Stamina, EnemyAi.SwitchById(enemy.Def, enemy.ActiveSwitch));
            if (action == null) return new OmenPreview(0, 0, Lands: false, Rests: true);
            if (!EnemyAi.IsOpponentDirected(action.Attributes, action.Face, action.Targets))
            {
                return new OmenPreview(0, 0, Lands: false, Rests: false);
            }

            var reach = action.Face.ReachOrDefault;
            bool lands = reach.Contains(copy.GapTo(unit));
            if (!lands)
            {
                int cell = CellInReach(copy, unit, reach);
                if (cell < 0)
                {
                    return FaceAlone(action, copy.Player.Guard + copy.Tools.ScaleGuard);
                }
                copy = copy with { Player = copy.Player with { Cell = cell } };
            }
            copy = KeepStanding(copy, Actor.Player, 0);

            events.Clear();
            var source = ActionSource(action, EnemyAi.FaceAt(action, copy.GapTo(unit)));
            Resolve(copy, new Play(Actor.Enemy, unit, Array.Empty<int>(), source, EnemyContext(copy, unit, action)), new SeededRng(0), events);
            var (total, damage) = Landed(events, Actor.Enemy, Actor.Player, unit);
            return new OmenPreview(total, damage, lands, Rests: false);
        }

        /// <summary>
        /// §6 「境目を満たすマスを濃く出します」 (#455): the cells enemy <paramref name="unit"/>'s
        /// shown omen aims at whose gap meets the boundary its action reads, counted from where it
        /// stands now as <see cref="EnemyAi.TargetCells"/> counts the aimed ones. The action is the
        /// one the omen names under the switch it was decided on (#51 overrides). Empty when the enemy
        /// is not standing, has no omen, rests, or its action reads no boundary.
        /// </summary>
        public static IReadOnlyList<int> OmenBoundaryCells(BattleState state, int unit)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var action = OmenAction(state, unit);
            if (action == null) return Array.Empty<int>();
            return EnemyAi.BoundaryCells(action, state.Enemies[unit].Body);
        }

        /// <summary>
        /// battle_ui_ux_v2 §5.8 / battle-visual-v1 §4.2 敵が動く予兆の移る先 (#455): the cell enemy
        /// <paramref name="unit"/> would stand on (its lowest cell) after the movement effect of its
        /// shown omen, moved from where both sides stand now by the rule <see cref="EndTurn"/> moves it
        /// by — 鈍足 takes a cell off, 呪縛 and 鉤爪 stop it, the line and the player stop it short.
        /// Read off the board as it is, not after the turn start a 歪みの根 sways in, and before any
        /// push of the same action. −1 when the enemy is not standing, has no omen, rests, its action
        /// does not move it, or it would not get anywhere.
        /// </summary>
        public static int OmenMoveCell(BattleState state, int unit)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var action = OmenAction(state, unit);
            if (action == null) return -1;
            var face = EnemyAi.FaceAt(action, state.GapTo(unit));
            if (face.Move == 0) return -1;
            int from = state.Enemies[unit].Body.Cell;
            var moved = MoveSelf(state, Actor.Enemy, unit, face.Move, new List<BattleEvent>());
            int to = moved.Enemies[unit].Body.Cell;
            return to == from ? -1 : to;
        }

        /// <summary>
        /// battle-visual-v1 §4.7 次のターンに回復する分 (#455): the stamina the player would get back at
        /// the next turn start if the turn ended now — §9 step 7's turn-end stance (its 次の回復), then
        /// steps 1-3's recovery with the bonus 温存 and 重撃 left, 疲労, a turn-start stance's
        /// recovery and the boss words that thin it, capped at the maximum, as
        /// <see cref="BeginPlayerTurn"/> will count it. It is an estimate: what the enemy phases in
        /// between do (a 疲労 put on, a move that changes the gap a stance reads) is not played.
        /// Called on a state awaiting the turn start, the turn end is behind it and only the turn
        /// start is counted. 0 once the battle is over.
        /// </summary>
        public static int PreviewRecovery(BattleState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Result != GameResult.Ongoing) return 0;
            var events = new List<BattleEvent>();
            var copy = state;
            if (state.Phase == BattlePhase.PlayerAction)
            {
                copy = StanceAtTurnEnd(copy, Actor.Player, 0, events);
                copy = copy with { Player = copy.Player with { Played = null, Moved = false, LastMove = 0, FollowUp = 0, FreeStep = false } };
            }
            copy = copy with { Turn = copy.Turn + 1 };
            copy = ResumeStances(copy, events);
            int before = events.Count;
            OpenTurnFor(copy, Actor.Player, 0, Constants.StaminaRecovery, events);
            for (int i = before; i < events.Count; i++)
            {
                if (events[i] is StaminaRecovered recovered && recovered.Actor == Actor.Player) return recovered.Amount;
            }
            return 0;
        }

        /// <summary>The action enemy <paramref name="unit"/>'s omen names, under the switch it was decided on; null for none or a rest.</summary>
        private static EnemyActionDef? OmenAction(BattleState state, int unit)
        {
            if (!IsStanding(state, unit)) return null;
            var enemy = state.Enemies[unit];
            if (enemy.Omen == null || enemy.Omen.ActionId == EnemyAi.RestActionId) return null;
            return EnemyAi.ActionOf(enemy.Def, enemy.Omen.ActionId, EnemyAi.SwitchById(enemy.Def, enemy.ActiveSwitch));
        }

        /// <summary>
        /// The player's cell nearest their own (the nearer the enemy on a tie) from which enemy
        /// <paramref name="unit"/>'s N falls inside <paramref name="reach"/>, left of every standing
        /// enemy. −1 when the line has none.
        /// </summary>
        private static int CellInReach(BattleState state, int unit, Reach reach)
        {
            var player = state.Player;
            int front = state.Enemies[state.Nearest].Body.Cell;
            int best = -1;
            for (int cell = 1; Field.FrontCell(cell, player.Size) < front; cell++)
            {
                int gap = Field.Gap(cell, player.Size, state.Enemies[unit].Body.Cell);
                if (!reach.Contains(gap)) continue;
                if (best < 0 || Math.Abs(cell - player.Cell) <= Math.Abs(best - player.Cell)) best = cell;
            }
            return best;
        }

        /// <summary>
        /// A preview's copy with the one being struck unable to fall, so that no blow stops the rest
        /// (§17.6 F9 would end the resolution there): the player, or enemy <paramref name="unit"/> —
        /// only the one the number is read on, so that any other enemy a card for all reaches falls
        /// as it would and leaves its cells. Nothing a face or a trait reads looks at HP.
        /// </summary>
        private static BattleState KeepStanding(BattleState state, Actor side, int unit)
        {
            const int standing = 1_000_000;
            if (side == Actor.Player) return state with { Player = state.Player with { Hp = standing } };
            return state.WithEnemy(unit, state.Enemies[unit].Body with { Hp = standing });
        }

        /// <summary>
        /// An omen that can reach the player from no cell: the attack face alone, each blow meeting
        /// <paramref name="guard"/> on its own as it would (§2.4 hits), with nothing the board adds.
        /// </summary>
        private static OmenPreview FaceAlone(EnemyActionDef action, int guard)
        {
            if (!action.Attributes.HasFlag(BattleAttribute.Attack)) return new OmenPreview(0, 0, Lands: false, Rests: false);
            int raw = 0;
            int damage = 0;
            for (int blow = 0; blow < Math.Max(1, action.Face.Hits); blow++)
            {
                var hit = Combat.ApplyGuard(action.Face.Power, guard);
                guard = hit.GuardAfter;
                raw += action.Face.Power;
                damage += hit.Damage;
            }
            return new OmenPreview(raw, damage, Lands: false, Rests: false);
        }

        /// <summary>The power (before Guard) and the HP loss (after) the events put on <paramref name="victim"/> <paramref name="unit"/>: its blows from <paramref name="attacker"/> and its wall.</summary>
        private static (int Raw, int Damage) Landed(IReadOnlyList<BattleEvent> events, Actor attacker, Actor victim, int unit)
        {
            int raw = 0;
            int damage = 0;
            foreach (var e in events)
            {
                if (e is DamageDealt blow && blow.Actor == attacker && blow.Target == victim && blow.Unit == unit)
                {
                    raw += blow.Raw;
                    damage += blow.Damage;
                }
                else if (e is WallHit wall && wall.Actor == victim && wall.Unit == unit)
                {
                    raw += wall.Raw;
                    damage += wall.Damage;
                }
            }
            return (raw, damage);
        }
    }
}
