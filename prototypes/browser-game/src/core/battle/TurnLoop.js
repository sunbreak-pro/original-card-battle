// Port of unity-port/BattleCore/TurnLoop.cs
//
// Faithful port of the battle turn loop (§9). Every public and private member keeps its C# name;
// the private ones are module-level functions (not exported on TurnLoop) so the public surface is
// the C# one. C# `out` parameters of private methods come back as objects:
//   OnFall  → { state, over }
//   Execute → { state, fell }
//   Ward    → { state, warded, wardedCap }
// C# tuples are plain objects ({ Raw, Damage }; Combat.ApplyGuard's { Damage, GuardAfter, Absorbed },
// Cards.DiscardHand's { Hand, DiscardPile }), as docs/CONVENTIONS.md says.
// Event init-only properties (`new X(...) { Unit = i }`) are set with the record's `with`.

import {
  defineRecord,
  defineEnum,
  hasFlag,
  csEquals,
  sum,
  firstOrDefault,
  idiv,
  InvalidOperationException,
  ArgumentException,
  ArgumentNullException,
  ArgumentOutOfRangeException,
} from '../../shared/cs.js';
import {
  BattleAttribute,
  FaceKind,
  Actor,
  GameResult,
  BattlePhase,
  TargetKind,
  OmenKind,
  StatusKind,
  StatusDecay,
  StanceHook,
  StanceWhen,
  CombatantState,
  EnemyUnit,
  BattleState,
  CarriedTools,
  PlayerTurnEnd,
  AdaptationView,
} from './Types.js';
import {
  TurnStarted,
  GuardCleared,
  StaminaRecovered,
  StatusTicked,
  Drawn,
  DeckReshuffled,
  OmenSet,
  PlanSet,
  PlanChanged,
  TreeSwitched,
  ReserveChecked,
  HandDiscarded,
  TurnEnded,
  CardPlayed,
  ActionExecuted,
  ActionWhiffed,
  Rested,
  StaminaSpent,
  TraitEvaluated,
  FaceResolved,
  DamageDealt,
  GuardGained,
  CellsMoved,
  WallHit,
  PushRefused,
  FreeStepTaken,
  MoveBlocked,
  StatusApplied,
  StaminaGained,
  StatusConsumed,
  StatusHpChanged,
  Healed,
  StaminaBroken,
  Reflected,
  StanceSet,
  StanceFired,
  StanceStopped,
  StanceResumed,
  CardExiled,
  EnemyDefeated,
  DefeatChecked,
  BattleEnded,
  MaxStaminaChanged,
  StatusCleared,
  StepResult,
} from './BattleEvents.js';
import { Constants } from './Constants.js';
import { Combat } from './Combat.js';
import { Field } from './Field.js';
import { Cards } from './Cards.js';
import { Traits, TraitContext } from './Traits.js';
import { Statuses, StatusSet } from './Statuses.js';
import { AttributeRule } from './Attributes.js';
import { Columns } from './Columns.js';
import { Mastery, MasteryPlay } from './Mastery.js';
import { SeededRng } from './IRng.js';
import { EnemyAi } from './EnemyAi.js';
import { Enemies } from './Enemies.js';
import { PrototypeDeck } from './CardCatalog.js';

// ---------------------------------------------------------------- .NET exception messages

/** new ArgumentNullException(nameof(name)).Message */
function nullArg(name) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${name}')`);
}
/** new ArgumentException(message, nameof(name)).Message */
function badArg(message, name) {
  return new ArgumentException(`${message} (Parameter '${name}')`);
}
/** new ArgumentOutOfRangeException(nameof(name), actualValue, message).Message (Environment.NewLine is "\n" on Linux). */
function outOfRange(name, actual, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${name}')\nActual value was ${actual}.`);
}

function isPlainObject(x) {
  return x !== null && typeof x === 'object' && Object.getPrototypeOf(x) === Object.prototype;
}

/**
 * defineRecord + C# named arguments, the same convention as Types.js: a trailing plain object
 * whose keys are all parameter names of the record is read as the named arguments
 * (C# `new BattleSetup(enemy, deck, 6, StartGap: 2)` is `new BattleSetup(enemy, deck, 6, { StartGap: 2 })`);
 * one that mixes known and unknown keys throws (a typo). Omitted parameters take their C# default.
 */
function record(name, fields) {
  const R = defineRecord(name, fields);
  const C = class extends R {
    constructor(...args) {
      let named = null;
      if (args.length > 0 && isPlainObject(args[args.length - 1])) {
        const last = args[args.length - 1];
        const keys = Object.keys(last);
        const known = keys.filter((k) => C.$fields.includes(k));
        if (keys.length > 0 && known.length === keys.length) {
          named = last;
          args = args.slice(0, -1);
        } else if (known.length > 0) {
          const unknown = keys.filter((k) => !C.$fields.includes(k));
          throw new Error(`${name}: unknown named argument(s) ${unknown.join(', ')}`);
        }
      }
      super(...args);
      if (named) {
        for (const k of Object.keys(named)) {
          // An explicit undefined means "not given" (the C# default stays).
          if (named[k] !== undefined) this[k] = named[k];
        }
      }
    }
  };
  return C;
}

/** C# `new X(...) { Unit = unit }`: the init-only Unit of a BattleEvent. */
function U(event, unit) {
  return event.with({ Unit: unit });
}

/** C# `List<T>.Remove(item)`: the first element equal (record equality) to item. */
function removeFirst(list, item) {
  for (let i = 0; i < list.length; i++) {
    if (csEquals(list[i], item)) {
      list.splice(i, 1);
      return true;
    }
  }
  return false;
}

/** C# `List<int>.Sort((a, b) => cell(a).CompareTo(cell(b)))`. Living enemies never share a cell, so List.Sort's instability cannot show. */
function sortByCell(state, list) {
  list.sort((a, b) => {
    const ca = state.Enemies[a].Body.Cell;
    const cb = state.Enemies[b].Body.Cell;
    return ca < cb ? -1 : ca > cb ? 1 : 0;
  });
}

// ---------------------------------------------------------------- BattleSetup

/**
 * What a battle starts from. FieldCells has no default (2026-09-23, #169): the layer and the
 * battle hand it in (5〜8, the dungeon side picks it in #168). The rest are §7.3's: the player
 * on cell 2, the enemy START_GAP (3) cells further (Field.EnemyStartCell for a line too short),
 * both with full stamina and Guard 0.
 *
 * MoreEnemies (§7.4, #47) are the second and third enemy, in phase order. Each starts on the
 * first free cell behind the one before it; a line too short to hold them all is refused.
 *
 * The last three are the departure tools that act inside a battle (§9.1, #452), as amounts:
 * PlayerStartStaminaBonus is 息継ぎの笛 (+3 a flute), FirstHitGuard 竜鱗の欠片 (Guard 5 a
 * scale, given once, just before the first enemy blow or wall that reaches the player) and
 * BossWordWard 竜語の符 (the boss-only words sit 1 stack lower a charm). 0 is no tool.
 *
 * The Constants defaults are read lazily (function defaults) so no Constants value is read at
 * module-evaluation time across an import cycle.
 */
export class BattleSetup extends record('BattleSetup', [
  'Enemy',
  'Deck',
  'FieldCells',
  ['StartGap', () => Constants.StartGap],
  ['PlayerStartCell', () => Constants.PlayerStartCell],
  ['PlayerMaxHp', () => Constants.PlayerMaxHp],
  ['PlayerMaxStamina', () => Constants.BaseMaxStamina],
  ['MoreEnemies', null],
  ['PlayerStartHp', null],
  ['PlayerStartStamina', null],
  ['PlayerStartStaminaBonus', 0],
  ['FirstHitGuard', 0],
  ['BossWordWard', 0],
]) {
  /**
   * §12 連戦 (#191): the HP and the current stamina the player carries in from the battle
   * before; null is full. HP above max is kept to max and stamina to 0..max. A carried HP of 0
   * or less throws (#574): a fallen character never starts a battle, so it is a caller's bug,
   * not a value to lift to 1. Nothing else carries: the deck is shuffled afresh (2026-09-23),
   * statuses, Guard and the stance slot start empty.
   */
  get StartHp() {
    if (this.PlayerStartHp == null) return this.PlayerMaxHp;
    if (this.PlayerStartHp < 1) {
      throw outOfRange('PlayerStartHp', this.PlayerStartHp, 'A carried HP is 1 or more: a fallen character does not start a battle.');
    }
    return Math.min(this.PlayerMaxHp, this.PlayerStartHp);
  }

  /**
   * The stamina the player opens with: the carried stamina kept to 0..max (full without one),
   * then 息継ぎの笛's bonus on top (§9.1, #452). The bonus may take it past the maximum, never
   * past Constants.MaxStaminaCeil. Recovery and gains add nothing to stamina at or above the
   * maximum (Combat.RecoverStamina), so the extra is only spent.
   */
  get StartStamina() {
    const carried = this.PlayerStartStamina != null
      ? Math.max(0, Math.min(this.PlayerMaxStamina, this.PlayerStartStamina))
      : this.PlayerMaxStamina;
    const bonus = Math.max(0, this.PlayerStartStaminaBonus);
    return bonus === 0 ? carried : Math.min(Math.max(carried, Constants.MaxStaminaCeil), carried + bonus);
  }

  /** §9.1 (#452): what the tools hold for the battle (竜鱗の欠片 and 竜語の符); null without them. */
  get Tools() {
    return this.FirstHitGuard > 0 || this.BossWordWard > 0
      ? new CarriedTools(Math.max(0, this.FirstHitGuard), Math.max(0, this.BossWordWard))
      : null;
  }

  /** The width the vertical slice is fought on until the dungeon hands one in (#168). */
  static SliceFieldCells = 6;

  /** Every enemy of the battle, in phase order: Enemy first. */
  get AllEnemies() {
    const all = [this.Enemy];
    if (this.MoreEnemies != null) all.push(...this.MoreEnemies);
    return all;
  }

  /** Where the first enemy's near edge stands on turn 1. */
  get EnemyStartCell() {
    return Field.EnemyStartCell(this.FieldCells, this.PlayerStartCell, 1, this.Enemy.Size, this.StartGap);
  }

  /**
   * §7.4: every enemy's near edge on turn 1. The first stands START_GAP from the player; each
   * next one on the cell right behind the far edge of the one before.
   */
  get EnemyStartCells() {
    const cells = [];
    const all = this.AllEnemies;
    for (let i = 0; i < all.length; i++) {
      cells.push(i === 0 ? this.EnemyStartCell : cells[i - 1] + all[i - 1].Size);
    }
    return cells;
  }

  /** The vertical slice: the polearm against the ten-kind prototype deck. */
  static Slice() {
    return new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), BattleSetup.SliceFieldCells);
  }
}

// ---------------------------------------------------------------- PlayPreview / OmenPreview / PlayRefusal

/**
 * The settled numbers a held card would produce. See TurnLoop.Preview. InReach is false when the
 * card aims at the opponent and N is outside its reach — the one reason a card with payable cost
 * still cannot be released (§2.4). Cost is what playing it would take now, after a コスト −1 trait
 * (#188) and a コストの割引 stance (§4), as TurnLoop.CostNow.
 *
 * RawPower and Damage are the whole card on the enemy it reads (#248): every blow of a face with
 * hits, plus the wall damage of a push it could not take (§7.3). RawPower is before that enemy's
 * Guard, Damage the HP it would lose after it. Out of reach they are one blow's, worked out
 * without resolving (the screen shows no number on an enemy it cannot reach).
 */
export class PlayPreview extends record('PlayPreview', ['TraitHolds', 'Attacks', 'RawPower', 'Damage', 'GuardGain', 'InReach', 'Cost']) {}

/**
 * What an enemy's shown omen would do to the player if the turn ended now, as one number (#248).
 * See TurnLoop.PreviewOmen. RawPower is every blow plus the wall damage before the player's
 * Guard, Damage the HP the player would lose after the Guard they would hold by then (構え
 * included). Lands is whether it reaches the player where they stand now; when it does not, the
 * two numbers are what it would do if it did. Rests is true when the enemy cannot pay for it (§6)
 * or falls to 出血 before it acts, and then both numbers are 0.
 */
export class OmenPreview extends record('OmenPreview', ['RawPower', 'Damage', 'Lands', 'Rests']) {}

/** Why a card cannot be played right now. None when it can. */
export const PlayRefusal = defineEnum([
  'None',
  'BattleOver',
  'NotPlayerTurn',
  'NotInHand',
  'NotEnoughStamina',
  /** §2.4: the opponent stands outside the card's reach. */
  'OutOfReach',
  /** §7.4: the card aims at one enemy and the one named is not a standing enemy. */
  'NoSuchTarget',
  /** roster §4.1 呪縛 (#51): the player is bound and the card is one of movement only. */
  'Bound',
]);

// ---------------------------------------------------------------- TurnLoop

// ---- Start ----

/**
 * Shuffles the deck and has every enemy decide its first omen, so that step 5 of turn 1 has
 * something to show. Turn is 0 until the first BeginPlayerTurn.
 */
function Start(setup, rng) {
  if (setup == null) throw nullArg('setup');
  if (rng == null) throw nullArg('rng');

  const defs = setup.AllEnemies;
  for (const def of defs) {
    if (def == null) throw badArg('A battle needs its enemies.', 'setup');
  }
  const cells = setup.EnemyStartCells;
  const placed = [];
  for (let i = 0; i < defs.length; i++) placed.push({ Cell: cells[i], Size: defs[i].Size });
  Field.Validate(setup.FieldCells, setup.PlayerStartCell, 1, placed);

  const player = new CombatantState(
    setup.StartHp, setup.PlayerMaxHp,
    setup.StartStamina, setup.PlayerMaxStamina,
    0, setup.PlayerStartCell, 1, StatusSet.Empty);
  const units = [];
  for (let i = 0; i < defs.length; i++) {
    const def = defs[i];
    units.push(new EnemyUnit(def, new CombatantState(
      def.MaxHp, def.MaxHp,
      def.MaxStamina, def.MaxStamina,
      0, cells[i], def.Size, StatusSet.Empty), null));
  }

  let state = new BattleState(
    0, setup.FieldCells, player, units,
    [],
    Cards.Shuffle(setup.Deck, rng),
    [],
    undefined, undefined, undefined, undefined,
    setup.Tools);

  const events = [];
  for (let i = 0; i < units.length; i++) state = DecideNextOmen(state, i, events);
  return new StepResult(state, events);
}

// ---- Steps 1-5 ----

function BeginPlayerTurn(state, rng) {
  if (state == null) throw nullArg('state');
  if (rng == null) throw nullArg('rng');
  Require(state, BattlePhase.AwaitingTurnStart, 'BeginPlayerTurn');

  const events = [];
  state = state.with({ Turn: state.Turn + 1 });
  events.push(new TurnStarted(Actor.Player, state.Turn));

  // #334: a permanent effect stopped through the last turn works again from here, before
  // the turn-start stances fire.
  state = ResumeStances(state, events);

  // Steps 1-3: Guard to 0, recover, the stance and the statuses at turn start.
  state = OpenTurnFor(state, Actor.Player, 0, Constants.StaminaRecovery, events);

  // §17.6 F9: 出血 can take the last HP at turn start. The battle ends there; nothing is drawn.
  if (Combat.IsDefeated(state.Player.Hp)) {
    return new StepResult(CheckDefeat(state, Actor.Player, 0, events), events);
  }

  // Step 4: one card at a time, so a reshuffle lands between the right two draws.
  state = DrawCards(state, Combat.DrawCount(), rng, events);

  // Step 5: show each omen that has been standing since step 12 (or since Start).
  for (const i of state.Living) {
    events.push(U(new OmenSet(Actor.Enemy, state.Enemies[i].Omen, false), i));
    if (state.Enemies[i].Plan != null) events.push(U(new PlanSet(Actor.Enemy, state.Enemies[i].Plan, false), i));
  }

  return new StepResult(state.with({ Phase: BattlePhase.PlayerAction }), events);
}

// ---- Step 6 ----

/**
 * Whether the card can be released now. target is the enemy a card that aims at one enemy is
 * dropped on (§7.4); a card aimed at all, or at nobody, ignores it. The cost is the one a
 * コスト −1 trait (#188) and a コストの割引 stance (§4) leave: CostNow.
 */
function CanPlay(state, instanceId, target = 0) {
  if (state == null) throw nullArg('state');
  if (state.Result !== GameResult.Ongoing) return PlayRefusal.BattleOver;
  if (state.Phase !== BattlePhase.PlayerAction) return PlayRefusal.NotPlayerTurn;

  const card = FindInHand(state, instanceId);
  if (card == null) return PlayRefusal.NotInHand;
  if (!Combat.CanPay(CostNow(state, card.Def, target), state.Player.Stamina)) return PlayRefusal.NotEnoughStamina;

  // roster §4.1 呪縛 (#51): 「移動が中心の札は使えない」.
  if (state.Player.Statuses.Has(StatusKind.Binding) && AttributeRule.IsMovementCard(card.Def.Attributes, card.Def.Face)) {
    return PlayRefusal.Bound;
  }

  return AimRefusal(state, card.Def, target);
}

/**
 * What the card in the hand would cost if played now: its column's cost, less a コスト −1 trait
 * that holds on the current board (floor 0), less 1 more when a コストの割引 stance (§4,
 * StanceHook.CostDiscount) has a turn's discount left for the attribute the card counts as. The
 * screen prints this, so it never has to judge the trait or the stance itself.
 */
function CostNow(state, def, target = 0) {
  if (state == null) throw nullArg('state');
  if (def == null) throw nullArg('def');
  const read = ReadableUnit(state, def, target);
  // No enemy stands (the battle is over): the column's cost, as Preview's null leaves it on screen.
  if (read < 0) return def.Cost;
  const outcome = Traits.EvaluateAll(def.AllTraits, PlayerContext(state, def, read));
  return Discounted(state, def, Math.max(0, def.Cost - outcome.CostDown));
}

/**
 * §4 コストの割引: cost (the column's, after the card's own コスト −1) less 1 when a
 * StanceHook.CostDiscount entry would be spent on this card.
 */
function Discounted(state, def, cost) {
  return CostDiscountEntry(state, def, cost) >= 0 ? cost - 1 : cost;
}

/**
 * §4 コストの割引 (鉄壁の構え): the index on the player's list of the entry a play of def would
 * spend, or −1. That is the oldest CostDiscount entry for the attribute the card counts as
 * (folded, §2.1) that has not taken a cost down this turn (StanceEntry.ReactedTurn). One entry a
 * card, so two entries make two cards a turn cheaper. None when cost is already 0: an entry is
 * not spent on a card it cannot make cheaper. 温存 reads the stamina left after this discount
 * (PlayerContext).
 */
function CostDiscountEntry(state, def, cost) {
  if (cost <= 0) return -1;
  const stances = state.Player.StanceList;
  for (let i = 0; i < stances.length; i++) {
    const entry = stances[i];
    if (entry.Def.Hook !== StanceHook.CostDiscount || entry.Def.Attribute !== def.Attribute) continue;
    if (entry.ReactedTurn === state.Turn || entry.StoppedIn(state.Turn)) continue;
    return i;
  }
  return -1;
}

/**
 * §2.4 / §7.4: the part of CanPlay that is about whom the card aims at. A card aimed at the
 * opponent needs them inside its reach — the one named for a card aimed at one, anyone for a card
 * aimed at all. A player's card never whiffs.
 */
function AimRefusal(state, def, target) {
  if (!EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets)) return PlayRefusal.None;
  if (def.Targets === TargetKind.All) {
    return Reached(state, def.Face.ReachOrDefault).length > 0 ? PlayRefusal.None : PlayRefusal.OutOfReach;
  }
  if (!IsStanding(state, target)) return PlayRefusal.NoSuchTarget;
  return def.Face.ReachOrDefault.Contains(state.GapTo(target)) ? PlayRefusal.None : PlayRefusal.OutOfReach;
}

/**
 * What playing this card would do right now, without playing it: whether its traits hold, the
 * raw power, what gets past the Guard of the enemy it reads, the Guard it would give and what it
 * would cost. Null when the card is not in the hand. The statuses that would be spent (集中,
 * 威圧, 強化, 脆化) and a waiting 追撃 are counted in; nothing is spent.
 *
 * The power and the damage (#248) come from resolving the card for real on a copy of the board,
 * so a face of several blows, the 脆化 its first blow leaves, and the wall damage of a push add up
 * exactly as PlayCard would. The copy has the stamina to pay even when the player lacks it, and
 * keeps every enemy standing so that a blow that would fell one does not hide the rest of the
 * card's power.
 */
function Preview(state, instanceId, target = 0) {
  if (state == null) throw nullArg('state');
  const card = FindInHand(state, instanceId);
  if (card == null) return null;

  const def = card.Def;
  const read = ReadableUnit(state, def, target);
  if (read < 0) return null;
  const self = state.Player;
  const context = PlayerContext(state, def, read);
  const outcome = Traits.EvaluateAll(def.AllTraits, context);
  const focus = self.Statuses.Has(StatusKind.Focus) ? FocusStep.Of(def.Attributes, def.Column, def.Face) : FocusStep.None;

  const attacks = hasFlag(def.Attributes, BattleAttribute.Attack);
  const guardBase = def.Face.Guard + focus.Guard + outcome.GuardBonus;
  const intimidate = self.Statuses.Has(StatusKind.Intimidate) && (attacks || guardBase > 0)
    ? Constants.IntimidatePenalty : 0;

  const inReach = AimRefusal(state, def, target) === PlayRefusal.None;
  const directed = EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets);
  let raw = 0;
  let damage = 0;
  if (directed && inReach) {
    ({ Raw: raw, Damage: damage } = ResolvedOnCopy(state, card, read, context));
  } else if (attacks) {
    ({ Raw: raw, Damage: damage } = OneBlow(state, def, read, outcome, focus, intimidate));
  }
  const guardGain = Math.max(0, guardBase - (guardBase > 0 ? intimidate : 0));
  return new PlayPreview(
    outcome.Triggered, attacks, raw, damage, guardGain, inReach,
    Discounted(state, def, Math.max(0, def.Cost - outcome.CostDown)));
}

/**
 * PlayCard's own steps on a copy: the card leaves the hand and resolves. What lands on enemy read
 * is summed — its blows (DamageDealt) and the wall it is pushed into (WallHit). context is read
 * from the real board, so a trait that reads stamina is judged on what the player holds.
 */
function ResolvedOnCopy(state, card, read, context) {
  const def = card.Def;
  let copy = KeepStanding(state, Actor.Enemy, read);
  copy = copy.with({ Player: copy.Player.with({ Stamina: Math.max(copy.Player.Stamina, def.Cost) }) });
  const foes = def.Targets === TargetKind.All
    ? Reached(copy, def.Face.ReachOrDefault)
    : [read];

  const hand = copy.Hand.slice();
  removeFirst(hand, card);
  copy = copy.with({ Hand: hand });

  const events = [];
  Resolve(
    copy, Actor.Player, read, foes, def.Id, def.Name, def.Attributes, def.Column, def.Face,
    def.AllTraits, def.Targets, def.Cost, context, new SeededRng(0), events);
  return Landed(events, Actor.Player, Actor.Enemy, read);
}

/**
 * The one blow a card out of reach would deal, worked out without resolving: it cannot be
 * resolved there (Resolve refuses a player card out of reach).
 */
function OneBlow(state, def, read, outcome, focus, intimidate) {
  const foe = state.Enemies[read].Body;
  const self = state.Player;
  const conversion = outcome.Convert ? Half(def.Face.Guard + focus.Guard) : 0;
  const mult = foe.Statuses.Has(StatusKind.Fragile) ? Constants.FragileMult
    : self.Statuses.Has(StatusKind.Empower) ? Constants.EmpowerMult
      : 1.0;
  const raw = Combat.ComputeRawPower(
    def.Face.Power + focus.Power - intimidate,
    outcome.PowerBonus + StanceAttackBonus(state, Actor.Player, 0, Actor.Enemy, read),
    self.FollowUp, conversion, mult);
  return { Raw: raw, Damage: Combat.ApplyGuard(raw, foe.Guard).Damage };
}

/**
 * What enemy unit's shown omen would do to the player if the player ended the turn now (#248):
 * one number for the whole action — every blow, with the 脆化 its first blow leaves, and the wall
 * damage of its push. Like Preview it resolves the action on a copy, after the steps that come
 * before it in the same order as EndTurn: the player's turn-end stance and 構え (step 7; its
 * Guard can take 無防備 away), the played list cleared (step 8), and this enemy's own turn start
 * (step 9: its Guard reset, recovery and status ticks). Enemies that act before it are not played.
 *
 * The action is the one EnemyAi.ActionToExecute would carry out with the stamina the enemy holds
 * after that turn start, so only a drained enemy rests; one that falls to 出血 there counts as a
 * rest too. An elite's or a boss's second action is not on the omen and is not counted.
 *
 * When the action does not reach the player where they stand, Lands is false and the copy puts
 * the player on the free cell nearest their own from which it would, so the number still says
 * what it would do if it landed. With no such cell it falls back to the face alone (power ×
 * hits). Null when the enemy is not standing or has no omen.
 *
 * Call it on a state from the player's turn (BattlePhase.PlayerAction): it plays that turn's end
 * itself, so a state past it would have the turn end played twice.
 */
function PreviewOmen(state, unit) {
  if (state == null) throw nullArg('state');
  if (!IsStanding(state, unit)) return null;
  const enemy = state.Enemies[unit];
  if (enemy.Omen == null) return null;

  // Steps 7-9 on a copy, as EndTurn and EnemyPhase run them. What they emit is not wanted.
  const events = [];
  let copy = StanceAtTurnEnd(state, Actor.Player, 0, events);
  copy = CheckReserve(copy, Actor.Player, 0, events);
  copy = copy.with({ Player: copy.Player.with({ Played: null, Moved: false, LastMove: 0, FollowUp: 0 }) });
  copy = OpenTurnFor(copy, Actor.Enemy, unit, enemy.Def.Recovery, events);
  if (!copy.Enemies[unit].Alive) return new OmenPreview(0, 0, false, true);
  copy = Sway(copy, unit, events);

  const action = EnemyAi.ActionToExecute(
    enemy.Def, enemy.Omen, copy.Enemies[unit].Body.Stamina, EnemyAi.SwitchById(enemy.Def, enemy.ActiveSwitch));
  if (action == null) return new OmenPreview(0, 0, false, true);
  if (!EnemyAi.IsOpponentDirected(action.Attributes, action.Face, action.Targets)) {
    return new OmenPreview(0, 0, false, false);
  }

  const reach = action.Face.ReachOrDefault;
  const lands = reach.Contains(copy.GapTo(unit));
  if (!lands) {
    const cell = CellInReach(copy, unit, reach);
    if (cell < 0) {
      return FaceAlone(action, copy.Player.Guard + copy.Tools.ScaleGuard);
    }
    copy = copy.with({ Player: copy.Player.with({ Cell: cell }) });
  }
  copy = KeepStanding(copy, Actor.Player, 0);

  events.length = 0;
  Resolve(
    copy, Actor.Enemy, unit, [], action.Id, action.Name, action.Attributes,
    action.Column, EnemyAi.FaceAt(action, copy.GapTo(unit)), TraitsOf(action.Trait), action.Targets, action.Cost,
    EnemyContext(copy, unit, action), new SeededRng(0), events);
  const { Raw: total, Damage: damage } = Landed(events, Actor.Enemy, Actor.Player, unit);
  return new OmenPreview(total, damage, lands, false);
}

/**
 * §6 「境目を満たすマスを濃く出します」 (#455): the cells enemy unit's shown omen aims at whose gap
 * meets the boundary its action reads, counted from where it stands now as EnemyAi.TargetCells
 * counts the aimed ones. The action is the one the omen names under the switch it was decided on
 * (#51 overrides). Empty when the enemy is not standing, has no omen, rests, or its action reads
 * no boundary.
 */
function OmenBoundaryCells(state, unit) {
  if (state == null) throw nullArg('state');
  const action = OmenAction(state, unit);
  if (action == null) return [];
  return EnemyAi.BoundaryCells(action, state.Enemies[unit].Body);
}

/**
 * battle_ui_ux_v2 §5.8 / battle-visual-v1 §4.2 敵が動く予兆の移る先 (#455): the cell enemy unit
 * would stand on (its lowest cell) after the movement effect of its shown omen, moved from where
 * both sides stand now by the rule EndTurn moves it by — 鈍足 takes a cell off, 呪縛 and 鉤爪 stop
 * it, the line and the player stop it short. Read off the board as it is, not after the turn
 * start a 歪みの根 sways in, and before any push of the same action. −1 when the enemy is not
 * standing, has no omen, rests, its action does not move it, or it would not get anywhere.
 */
function OmenMoveCell(state, unit) {
  if (state == null) throw nullArg('state');
  const action = OmenAction(state, unit);
  if (action == null) return -1;
  const face = EnemyAi.FaceAt(action, state.GapTo(unit));
  if (face.Move === 0) return -1;
  const from = state.Enemies[unit].Body.Cell;
  const moved = MoveSelf(state, Actor.Enemy, unit, face.Move, []);
  const to = moved.Enemies[unit].Body.Cell;
  return to === from ? -1 : to;
}

/**
 * battle-visual-v1 §4.7 次のターンに回復する分 (#455): the stamina the player would get back at the
 * next turn start if the turn ended now — §9 step 7's turn-end stance (its 次の回復), then steps
 * 1-3's recovery with the bonus 温存 and 重撃 left, 疲労, a turn-start stance's recovery and the
 * boss words that thin it, capped at the maximum, as BeginPlayerTurn will count it. It is an
 * estimate: what the enemy phases in between do is not played. Called on a state awaiting the
 * turn start, the turn end is behind it and only the turn start is counted. 0 once the battle is
 * over.
 */
function PreviewRecovery(state) {
  if (state == null) throw nullArg('state');
  if (state.Result !== GameResult.Ongoing) return 0;
  const events = [];
  let copy = state;
  if (state.Phase === BattlePhase.PlayerAction) {
    copy = StanceAtTurnEnd(copy, Actor.Player, 0, events);
    copy = copy.with({ Player: copy.Player.with({ Played: null, Moved: false, LastMove: 0, FollowUp: 0, FreeStep: false }) });
  }
  copy = copy.with({ Turn: copy.Turn + 1 });
  copy = ResumeStances(copy, events);
  const before = events.length;
  OpenTurnFor(copy, Actor.Player, 0, Constants.StaminaRecovery, events);
  for (let i = before; i < events.length; i++) {
    const e = events[i];
    if (e instanceof StaminaRecovered && e.Actor === Actor.Player) return e.Amount;
  }
  return 0;
}

/** The action enemy unit's omen names, under the switch it was decided on; null for none or a rest. */
function OmenAction(state, unit) {
  if (!IsStanding(state, unit)) return null;
  const enemy = state.Enemies[unit];
  if (enemy.Omen == null || enemy.Omen.ActionId === EnemyAi.RestActionId) return null;
  return EnemyAi.ActionOf(enemy.Def, enemy.Omen.ActionId, EnemyAi.SwitchById(enemy.Def, enemy.ActiveSwitch));
}

/**
 * The player's cell nearest their own (the nearer the enemy on a tie) from which enemy unit's N
 * falls inside reach, left of every standing enemy. −1 when the line has none.
 */
function CellInReach(state, unit, reach) {
  const player = state.Player;
  const front = state.Enemies[state.Nearest].Body.Cell;
  let best = -1;
  for (let cell = 1; Field.FrontCell(cell, player.Size) < front; cell++) {
    const gap = Field.Gap(cell, player.Size, state.Enemies[unit].Body.Cell);
    if (!reach.Contains(gap)) continue;
    if (best < 0 || Math.abs(cell - player.Cell) <= Math.abs(best - player.Cell)) best = cell;
  }
  return best;
}

/**
 * A preview's copy with the one being struck unable to fall, so that no blow stops the rest
 * (§17.6 F9 would end the resolution there): the player, or enemy unit — only the one the number
 * is read on, so that any other enemy a card for all reaches falls as it would and leaves its
 * cells. Nothing a face or a trait reads looks at HP.
 */
function KeepStanding(state, side, unit) {
  const standing = 1_000_000;
  if (side === Actor.Player) return state.with({ Player: state.Player.with({ Hp: standing }) });
  return state.WithEnemy(unit, state.Enemies[unit].Body.with({ Hp: standing }));
}

/**
 * An omen that can reach the player from no cell: the attack face alone, each blow meeting guard
 * on its own as it would (§2.4 hits), with nothing the board adds.
 */
function FaceAlone(action, guard) {
  if (!hasFlag(action.Attributes, BattleAttribute.Attack)) return new OmenPreview(0, 0, false, false);
  let raw = 0;
  let damage = 0;
  for (let blow = 0; blow < Math.max(1, action.Face.Hits); blow++) {
    const hit = Combat.ApplyGuard(action.Face.Power, guard);
    guard = hit.GuardAfter;
    raw += action.Face.Power;
    damage += hit.Damage;
  }
  return new OmenPreview(raw, damage, false, false);
}

/** The power (before Guard) and the HP loss (after) the events put on victim unit: its blows from attacker and its wall. */
function Landed(events, attacker, victim, unit) {
  let raw = 0;
  let damage = 0;
  for (const e of events) {
    if (e instanceof DamageDealt && e.Actor === attacker && e.Target === victim && e.Unit === unit) {
      raw += e.Raw;
      damage += e.Damage;
    } else if (e instanceof WallHit && e.Actor === victim && e.Unit === unit) {
      raw += e.Raw;
      damage += e.Damage;
    }
  }
  return { Raw: raw, Damage: damage };
}

function PlayCard(state, instanceId, rng, target = 0) {
  if (rng == null) throw nullArg('rng');
  const refusal = CanPlay(state, instanceId, target);
  if (refusal !== PlayRefusal.None) {
    throw new InvalidOperationException(`Card "${instanceId}" cannot be played: ${refusal}.`);
  }

  const card = FindInHand(state, instanceId);
  const def = card.Def;
  const events = [];
  const read = ReadUnit(state, def, target);
  const directed = EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets);
  const foes =
    !directed ? []
      : def.Targets === TargetKind.All ? Reached(state, def.Face.ReachOrDefault)
        : [read];

  // Every condition reads the board from before the card leaves the hand (手薄 counts the
  // hand it leaves behind).
  const context = PlayerContext(state, def, read);

  // §4 コストの割引: the entry this card spends, judged on the same board as CostNow.
  const judged = Traits.EvaluateAll(def.AllTraits, context);
  const discount = CostDiscountEntry(state, def, Math.max(0, def.Cost - judged.CostDown));

  // §3.2 習熟 (#59): the contexts this play meets, read off the same board. They are only
  // written down here; the ticks they make land once the battle is over.
  const paid = Math.max(0, (discount >= 0 ? def.Cost - 1 : def.Cost) - judged.CostDown);
  const mastery = new MasteryPlay(def.Id, Mastery.ContextsOf(def, context, judged.Triggered, paid));

  // §5 俊敏 (#48): the free cell is taken before the first card or not at all.
  const hand = state.Hand.slice();
  removeFirst(hand, card);
  state = state.with({ Hand: hand, Player: state.Player.with({ FreeStep: false }) });
  events.push(U(new CardPlayed(Actor.Player, card, state.GapTo(read)), read));

  if (discount >= 0) {
    const stances = state.Player.StanceList.slice();
    const entry = stances[discount];
    stances[discount] = entry.with({ ReactedTurn: state.Turn });
    state = state.with({ Player: state.Player.with({ Stances: stances }) });
    events.push(new StanceFired(Actor.Player, entry.Source, StanceHook.CostDiscount));
  }

  // The discount comes off before Resolve pays: the cost it was saving was at least 1
  // after the trait, so Resolve's own floor never swallows it.
  state = Resolve(
    state, Actor.Player, read, foes, def.Id, def.Name, def.Attributes, def.Column, def.Face,
    def.AllTraits, def.Targets, discount >= 0 ? def.Cost - 1 : def.Cost, context, rng, events);

  // §2.3 playedAttributes: what this card was, for the cards after it this turn.
  state = RecordPlayed(state, Actor.Player, 0, def.Attribute);
  state = state.with({ PlayerHistory: state.History.WithCard(def.Attribute).WithMasteryPlay(mastery) });

  // §4: a stance card goes to the exile pile and is not drawn again this battle; every
  // other card goes to the discard pile once it has resolved.
  if (Cards.IsStanceCard(def)) {
    const exile = [...state.Exiled, card];
    state = state.with({ ExilePile: exile });
    events.push(new CardExiled(Actor.Player, card));
  } else {
    const discard = [...state.DiscardPile, card];
    state = state.with({ DiscardPile: discard });
  }

  state = CheckDefeat(state, Actor.Player, 0, events);
  return new StepResult(state, events);
}

/**
 * §5 俊敏 (#48): whether the player may take the free cell now. It opens at the turn start (§9
 * step 3) when 俊敏 is held and stays open until it is taken, the first card is played or the turn
 * ends — so the hand just drawn can inform the choice.
 */
function CanTakeFreeStep(state) {
  if (state == null) throw nullArg('state');
  return state.Result === GameResult.Ongoing && state.Phase === BattlePhase.PlayerAction && state.Player.FreeStep;
}

/**
 * §5 俊敏 (#48): the player takes the free cell, direction +1 forward or −1 back. It moves like a
 * move face of one cell, at no cost: 鈍足 takes it to 0, the line stops it at the nearest enemy
 * and at cell 1, and a cell that changes counts as 移動後 (§2.3). The step is spent even when the
 * line stops it. Not moving needs no call: playing a card or ending the turn lets it go.
 */
function TakeFreeStep(state, direction) {
  if (state == null) throw nullArg('state');
  if (direction !== 1 && direction !== -1) {
    throw outOfRange('direction', direction, 'The free step is one cell: +1 (forward) or -1 (back).');
  }
  Require(state, BattlePhase.PlayerAction, 'TakeFreeStep');
  if (!state.Player.FreeStep) {
    throw new InvalidOperationException('TakeFreeStep: the player has no free step this turn (no 俊敏, or already taken or let go).');
  }

  const events = [];
  state = state.with({ Player: state.Player.with({ FreeStep: false }) });
  events.push(new FreeStepTaken(Actor.Player, direction));
  state = MoveSelf(state, Actor.Player, 0, direction, events);
  return new StepResult(state, events);
}

// ---- Steps 7-12 ----

function EndTurn(state, rng) {
  if (state == null) throw nullArg('state');
  if (rng == null) throw nullArg('rng');
  Require(state, BattlePhase.PlayerAction, 'EndTurn');

  const events = [];

  // Step 7: the stance's turn-end effect (根渡り, 霞み足), then 構え.
  state = StanceAtTurnEnd(state, Actor.Player, 0, events);
  state = CheckReserve(state, Actor.Player, 0, events);

  // #50: what a boss's adaptation may count — the Guard held once 構え was judged, N, and a move.
  // #51: and the cell it ended on (根張り), how many cards it played (root_st), and the way
  // its own last move went (root_m).
  const nearest = state.Nearest;
  state = state.with({
    PlayerHistory: state.History.WithTurnEnd(new PlayerTurnEnd(
      state.Player.Guard, nearest < 0 ? 0 : state.GapTo(nearest), state.Player.Moved,
      state.Player.Cell, state.Player.PlayedThisTurn.length, state.Player.LastMove)),
  });

  // Step 8: the whole hand goes. Nothing is kept (§17.6 F2). playedAttributes and a 追撃
  // that found no attack face go with it (§17.6 F7).
  const discarded = state.Hand.length;
  const { Hand: emptyHand, DiscardPile: discardPile } = Cards.DiscardHand(state.Hand, state.DiscardPile);
  state = state.with({
    Hand: emptyHand,
    DiscardPile: discardPile,
    Player: state.Player.with({ Played: null, Moved: false, LastMove: 0, FollowUp: 0, FreeStep: false }),
  });
  events.push(new HandDiscarded(Actor.Player, discarded));
  events.push(new TurnEnded(Actor.Player, state.Turn));

  // Steps 9-12, enemy by enemy in definition order (§9, §7.4).
  for (let i = 0; i < state.Enemies.length; i++) {
    if (!state.Enemies[i].Alive) continue;
    state = EnemyPhase(state, i, rng, events);
    if (state.Result !== GameResult.Ongoing) return new StepResult(state, events);
  }
  return new StepResult(state.with({ Phase: BattlePhase.AwaitingTurnStart }), events);
}

function EnemyPhase(state, unit, rng, events) {
  // Step 9: the enemy's own Guard reset, recovery, stance and status ticks.
  state = OpenTurnFor(state, Actor.Enemy, unit, state.Enemies[unit].Def.Recovery, events);
  if (!state.Enemies[unit].Alive) {
    // §17.6 F9: 出血 took the last HP at its turn start. It does not act.
    const fall = OnFall(state, Actor.Enemy, unit, events);
    state = fall.state;
    return fall.over ? CheckDefeat(state, Actor.Enemy, unit, events) : state;
  }

  // #51 (roster §6.1 段階 3): a body that sways steps before it acts.
  state = Sway(state, unit, events);

  // Step 10: the omen, as declared — or a rest when it cannot be paid for.
  const enemy = state.Enemies[unit];
  const active = EnemyAi.SwitchById(enemy.Def, enemy.ActiveSwitch);
  const action = EnemyAi.ActionToExecute(enemy.Def, enemy.Omen, enemy.Body.Stamina, active);
  if (action == null) {
    events.push(U(new Rested(Actor.Enemy, enemy.Omen), unit));

    // #50: the phase ends on the rest, so the 予定 that was shown goes with it.
    state = RevisePlan(state, unit, EnemyAi.OmenOf(null), events);
  } else {
    let executed = Execute(state, unit, action, rng, events);
    state = executed.state;
    if (executed.fell) return state.Living.length === 0 ? CheckDefeat(state, Actor.Enemy, unit, events) : state;

    // roster §1.3 (#189), §17.6 F11 (#50): an elite or a boss takes a second action. The
    // omen showed the first and planned the second (予定). The tree is read again from the
    // gap after the first — on the tree the omen was decided on — and the same action is
    // not taken twice in one phase. A different answer is a 予定変更, shown before it acts.
    if (enemy.Def.ActionsPerPhase >= 2 && !Combat.IsDefeated(state.Player.Hp)) {
      const skip = [...state.Enemies[unit].Spent, action.Id];
      skip.push(...EnemyAi.Barred(enemy.Def, state.Player.Statuses, false, active, state.Tools.WardedStacks));
      const second = EnemyAi.ChooseAction(enemy.Def, state.GapTo(unit), state.Enemies[unit].Body.Stamina, skip, active);
      state = RevisePlan(state, unit, EnemyAi.OmenOf(second), events);
      if (second != null) {
        executed = Execute(state, unit, second, rng, events);
        state = executed.state;
        if (executed.fell) return state.Living.length === 0 ? CheckDefeat(state, Actor.Enemy, unit, events) : state;
      }
    }
  }

  // The stance's turn-end effect, then 構え, which works for the enemy too (roster §1.2).
  state = StanceAtTurnEnd(state, Actor.Enemy, unit, events);
  state = CheckReserve(state, Actor.Enemy, unit, events);
  state = state.WithEnemy(unit, state.Enemies[unit].Body.with({ Played: null, Moved: false, LastMove: 0, FollowUp: 0 }));

  // Step 11.
  state = CheckDefeat(state, Actor.Enemy, unit, events);
  if (state.Result !== GameResult.Ongoing) return state;

  // Step 12: the next omen, read from this enemy's gap as it stands now — after any move or push.
  return DecideNextOmen(state, unit, events);
}

/**
 * §9 step 10 for one action of an enemy. Fell is true when the enemy went down to its own blow's
 * return (見切り): the phase ends there. A stance action is spent (roster §1.2).
 * C# `out bool fell` → returns { state, fell }.
 */
function Execute(state, unit, action, rng, events) {
  events.push(U(new ActionExecuted(Actor.Enemy, action, state.GapTo(unit)), unit));
  const context = EnemyContext(state, unit, action);
  state = Resolve(
    state, Actor.Enemy, unit, [], action.Id, action.Name, action.Attributes,
    action.Column, EnemyAi.FaceAt(action, state.GapTo(unit)), TraitsOf(action.Trait), action.Targets, action.Cost,
    context, rng, events);
  const fell = !state.Enemies[unit].Alive;
  const taken = [...state.Enemies[unit].ExecutedActions, action.Id];
  state = state.WithUnit(unit, state.Enemies[unit].with({ Executed: taken }));
  if (fell) return { state, fell };
  if (action.Face.Stance != null) {
    const spent = [...state.Enemies[unit].Spent, action.Id];
    state = state.WithUnit(unit, state.Enemies[unit].with({ SpentStances: spent }));
  }
  return { state: RecordPlayed(state, Actor.Enemy, unit, action.Attribute), fell };
}

// ---- Shared pieces ----

/**
 * §9 steps 1-3 for the player, step 9 for an enemy: the same things in the same order. Step 2 is
 * the recovery, less 1 for 疲労 and plus a turn-start stance's recovery (水の構え); step 3 is the
 * stance's Guard, then 再生, then 出血, then the ターンで減る型 ticks, then 俊敏 (#48). The holder
 * may fall to 出血 here; the caller looks.
 *
 * 俊敏 reads the stacks held before the tick, as 再生 and 出血 do, so n stacks give n turns of it.
 * The player's step is opened (CombatantState.FreeStep) and taken in TakeFreeStep; an enemy takes
 * its step here, toward the reach of the omen it is about to carry out
 * (EnemyAi.FreeStepDirection). One that fell to 出血 does not.
 */
function OpenTurnFor(state, actor, unit, recovery, events) {
  let self = Get(state, actor, unit);

  events.push(U(new GuardCleared(actor, self.Guard), unit));
  self = self.with({ Guard: 0, Played: null, Moved: false, LastMove: 0, FreeStep: false });

  // §4 (v4.4): every turn-start stance on the list fires on its own, so two of one work twice.
  const firing = [];
  for (const entry of self.StanceList) {
    if (entry.Def.Hook === StanceHook.TurnStart && !entry.StoppedIn(state.Turn) && StanceHolds(state, actor, unit, entry.Def)) firing.push(entry);
  }
  for (const entry of firing) events.push(U(new StanceFired(actor, entry.Source, StanceHook.TurnStart), unit));

  const fatigue = self.Statuses.Has(StatusKind.Fatigue) ? Constants.FatiguePenalty : 0;
  const stanceRecovery = sum(firing, (entry) => entry.Def.Recovery);
  // #51 (roster §5.1 深み, §6.2 枯らし): the boss words that thin the recovery, floor 0. The
  // 温存 bonus is deliberately inside that floor, as 疲労 is: RecoverStamina floors
  // recovery + bonus together, so a bonus first fills what the loss took. #400: 枯らし stops
  // at a recovery of 1 counted the same way (疲労 and the bonus in), 深み does not.
  const withered = Statuses.RecoveryLoss(
    self.Statuses,
    OwnGap(state, actor, unit) === 0 && state.Nearest >= 0,
    recovery + stanceRecovery - fatigue + self.NextTurnRecoveryBonus);
  const staminaAfter = Combat.RecoverStamina(
    self.Stamina, self.MaxStamina, recovery + stanceRecovery - fatigue - withered, self.NextTurnRecoveryBonus);
  events.push(U(new StaminaRecovered(actor, staminaAfter - self.Stamina, staminaAfter, self.MaxStamina), unit));
  self = self.with({ Stamina: staminaAfter, NextTurnRecoveryBonus: 0 });

  for (const entry of firing) {
    if (entry.Def.Guard <= 0) continue;
    self = self.with({ Guard: self.Guard + entry.Def.Guard });
    events.push(U(new GuardGained(actor, entry.Def.Guard, self.Guard), unit));
  }

  const regen = self.Statuses.Stacks(StatusKind.Regen);
  if (regen > 0) {
    const hpAfter = Math.min(self.MaxHp, self.Hp + (regen * Constants.RegenPerStack));
    events.push(U(new StatusHpChanged(actor, StatusKind.Regen, hpAfter - self.Hp, hpAfter), unit));
    self = self.with({ Hp: hpAfter });
  }
  const bleed = self.Statuses.Stacks(StatusKind.Bleed);
  if (bleed > 0) {
    const hpAfter = Math.max(0, self.Hp - (bleed * Constants.BleedPerStack));
    events.push(U(new StatusHpChanged(actor, StatusKind.Bleed, hpAfter - self.Hp, hpAfter), unit));
    self = self.with({ Hp: hpAfter });
  }

  // #51 (roster §6.2 根張り): the player ended the last two turns on the same cell. Only the
  // player's turns are kept in the history, and only a boss puts the word on the player.
  const rooted = self.Statuses.Stacks(StatusKind.Rooting);
  if (rooted > 0 && actor === Actor.Player && self.Hp > 0 && Statuses.StayedTwoTurns(state.History.TurnEnds)) {
    const hpAfter = Math.max(0, self.Hp - (rooted * Statuses.RootingHpPerStack));
    events.push(U(new StatusHpChanged(actor, StatusKind.Rooting, hpAfter - self.Hp, hpAfter), unit));
    self = self.with({ Hp: hpAfter });
  }

  const swift = self.Statuses.Has(StatusKind.Swift);
  const ticked = self.Statuses.TickTurnStart();
  for (const kind of self.Statuses.Kinds) {
    if (Statuses.DecayOf(kind) !== StatusDecay.OnTurn) continue;
    events.push(U(new StatusTicked(actor, kind, ticked.Stacks(kind)), unit));
  }
  self = self.with({ Statuses: ticked });
  state = Set(state, actor, unit, self);

  if (!swift || Combat.IsDefeated(self.Hp)) return state;
  if (actor === Actor.Player) return Set(state, actor, unit, self.with({ FreeStep: true }));

  // C# quirk kept: the enemy's free step reads its omen without the active switch (the 4th
  // argument of ActionToExecute is left at null), unlike EnemyPhase.
  const enemy = state.Enemies[unit];
  const omened = enemy.Omen == null ? null : EnemyAi.ActionToExecute(enemy.Def, enemy.Omen, self.Stamina);
  const direction = EnemyAi.FreeStepDirection(omened, state.GapTo(unit));
  if (direction === 0) return state;
  events.push(U(new FreeStepTaken(actor, direction), unit));
  return MoveSelf(state, actor, unit, direction, events);
}

function DrawCards(state, count, rng, events) {
  for (let i = 0; i < count; i++) {
    const result = Cards.Draw(state.DrawPile, state.DiscardPile, state.Hand, 1, rng);
    if (result.Drawn === 0) break;

    if (result.Reshuffled) {
      // +1: the pile as it stood right after the shuffle, before this card left it.
      events.push(new DeckReshuffled(Actor.Player, result.DrawPile.length + 1));
    }
    if (result.OverflowToDiscard === 0) {
      events.push(new Drawn(Actor.Player, result.Hand[result.Hand.length - 1], result.Hand.length));
    }
    state = state.with({ Hand: result.Hand, DrawPile: result.DrawPile, DiscardPile: result.DiscardPile });
  }
  return state;
}

/**
 * §2.2: pay, judge the traits once, then Attack → Move → Guard → Skill → Stance. The same routine
 * resolves a card and an enemy action, because both are written off the same face table.
 *
 * unit is the acting enemy in an enemy phase, and the enemy whose N and Guard the card reads on
 * the player's turn. foes are the enemies a player's opponent-directed faces land on — none for a
 * card aimed at nobody, one, or every one in reach for all (§7.4); an enemy's foe is always the
 * player. A pushed line resolves the far foe first, a pulled one the near foe first, so that
 * nobody is blocked by a neighbour that was about to move.
 *
 * When an enemy falls and others still stand, it leaves the line and the card goes on; the
 * routine stops after the face that ended the battle (§17.6 F9), and when the one acting falls to
 * a 見切り return.
 *
 * §6 空振り: an enemy action whose reach does not cover N at this moment skips its
 * opponent-directed faces (attack and 崩し, push / pull, the statuses on the opponent) and still
 * resolves the rest. A player card never gets here out of reach (CanPlay refuses it).
 *
 * The statuses the one acting holds work here: 集中 (the player's next card, one column to the
 * right — FocusStep: power, Guard, heal and the face's 出血 / 再生), 威圧 (−3 on power and Guard;
 * spent by an action whose attack face lands or that has a Guard, so a whiff with no Guard leaves
 * it), 強化 (×1.5 on the attack face) and a waiting 追撃. On the one hit: 脆化 (×1.5, taken before
 * 強化 — §19.5 S13, one multiplier per blow) and 見切り.
 */
function Resolve(
  state,
  actor,
  unit,
  foes,
  sourceId,
  sourceName,
  attributes,
  column,
  face,
  traits,
  targets,
  cost,
  context,
  rng,
  events) {
  const foeSide = Opponent(actor);
  const gap = state.GapTo(unit);

  // The traits are judged on the board from before the card (the context), before any face.
  const outcome = Traits.EvaluateAll(traits, context);
  const paid = Math.max(0, cost - outcome.CostDown);

  let self = Get(state, actor, unit);
  self = self.with({ Stamina: self.Stamina - paid });
  state = Set(state, actor, unit, self);
  events.push(U(new StaminaSpent(actor, paid, self.Stamina), unit));

  // The whiff is known before anything resolves, and is announced first so that whoever
  // reads the stream knows the trait below is judged on a blow that will not land.
  const directed = EnemyAi.IsOpponentDirected(attributes, face, targets);
  const whiff = directed && !face.ReachOrDefault.Contains(gap);
  if (whiff) {
    if (actor === Actor.Player) {
      throw new InvalidOperationException(`Card "${sourceId}" was resolved out of reach; CanPlay should have refused it.`);
    }
    events.push(U(new ActionWhiffed(actor, sourceId, gap, face.ReachOrDefault), unit));
  }

  // The sides the opponent-directed faces land on: (the foe's unit; ignored for the player).
  const hit = [];
  if (actor === Actor.Enemy) {
    if (directed && !whiff) hit.push(unit);
  } else {
    hit.push(...foes);
  }

  for (const trait of traits) {
    events.push(U(new TraitEvaluated(actor, sourceId, trait, Traits.Evaluate(trait, context)), unit));
  }
  if (outcome.NextTurnRecoveryBonus !== 0) {
    self = Get(state, actor, unit);
    self = self.with({ NextTurnRecoveryBonus: self.NextTurnRecoveryBonus + outcome.NextTurnRecoveryBonus });
    state = Set(state, actor, unit, self);
  }
  if (outcome.StaminaGain > 0) state = GainStamina(state, actor, unit, outcome.StaminaGain, events);

  // 集中: the player's next card, whatever it is. 威圧: the next action whose attack face lands
  // or that has a Guard (§5: a whiff, a skill or a move, each with no Guard, and a rest leave it).
  let focus = FocusStep.None;
  if (actor === Actor.Player && Get(state, actor, unit).Statuses.Has(StatusKind.Focus)) {
    focus = FocusStep.Of(attributes, column, face);
    state = Consume(state, actor, unit, StatusKind.Focus, events);
  }
  const attacks = hasFlag(attributes, BattleAttribute.Attack) && hit.length > 0;
  const guardBase = face.Guard + focus.Guard + outcome.GuardBonus;
  let intimidate = 0;
  if (Get(state, actor, unit).Statuses.Has(StatusKind.Intimidate) && (attacks || guardBase > 0)) {
    intimidate = Constants.IntimidatePenalty;
    state = Consume(state, actor, unit, StatusKind.Intimidate, events);
  }

  if (attacks) {
    events.push(U(new FaceResolved(actor, sourceId, FaceKind.Attack), unit));
    self = Get(state, actor, unit);
    const followUp = self.FollowUp;
    if (followUp > 0) state = Set(state, actor, unit, self.with({ FollowUp: 0 }));
    const conversion = outcome.Convert ? Half(face.Guard + focus.Guard) : 0;
    const empowered = self.Statuses.Has(StatusKind.Empower);
    let empowerUsed = false;

    for (const foe of hit) {
      if (foeSide === Actor.Enemy && !state.Enemies[foe].Alive) continue;

      const stanceBonus = StanceAttackBonus(state, actor, unit, foeSide, foe, events);

      // §2.4 hits (#189): each blow meets Guard on its own. §17.6 F6: 追撃 rides the
      // first blow only and the trait's +n every blow; 強化 rides only the blows 脆化
      // does not take (§5.1, §19.5 S13).
      for (let blow = 0; blow < Math.max(1, face.Hits); blow++) {
        if (foeSide === Actor.Enemy && !state.Enemies[foe].Alive) break;
        state = RaiseScale(state, foeSide, events);
        let other = Get(state, foeSide, foe);

        // §19.5 S13: one multiplier per blow, 脆化 first.
        let mult = 1.0;
        if (other.Statuses.Has(StatusKind.Fragile)) {
          mult = Constants.FragileMult;
          state = Consume(state, foeSide, foe, StatusKind.Fragile, events);
          other = Get(state, foeSide, foe);
        } else if (empowered) {
          mult = Constants.EmpowerMult;
          empowerUsed = true;
        }

        const raw = Combat.ComputeRawPower(
          face.Power + focus.Power - intimidate, outcome.PowerBonus + stanceBonus,
          blow === 0 ? followUp : 0, conversion, mult);
        const { Damage: damage, GuardAfter: guardAfter, Absorbed: absorbed } = Combat.ApplyGuard(raw, other.Guard);
        other = other.with({ Guard: guardAfter, Hp: Math.max(0, other.Hp - damage) });
        state = Set(state, foeSide, foe, other);
        events.push(U(new DamageDealt(actor, foeSide, raw, absorbed, damage, other.Guard, other.Hp), foeSide === Actor.Enemy ? foe : unit));

        if (!Combat.IsDefeated(other.Hp)) {
          state = OnHitReactions(state, actor, unit, foeSide, foe, absorbed, events);
        }
        const fall = OnFall(state, foeSide, foe, events);
        state = fall.state;
        if (fall.over) return state;

        // 見切り may have taken the attacker down.
        if (Combat.IsDefeated(Get(state, actor, unit).Hp)) {
          state = OnFall(state, actor, unit, events).state;
          return state;
        }

        // A face of several blows puts its opponent statuses on after the first
        // (二段斬り: 脆化 on the first blow, for the second).
        if (blow === 0 && face.Hits >= 2 && (foeSide === Actor.Player || state.Enemies[foe].Alive)) {
          for (const grant of face.StatusList) {
            if (grant.OnSelf || grant.Stacks <= 0) continue;
            state = ApplyStatus(state, actor, foeSide, foe, foeSide === Actor.Enemy ? foe : unit, grant.Kind, focus.StacksOf(grant), events, grant.Cap);
          }
        }

        // #253: a stance that gives a status on every attack does so blow by blow, so
        // the word is there for the next blow (脆化 makes it ×1.5).
        if (foeSide === Actor.Player || state.Enemies[foe].Alive) {
          state = StanceStatusOnAttack(state, actor, unit, foeSide, foe, events);
        }
      }

      const broken = face.Break + outcome.BreakBonus;
      if (broken > 0 && (foeSide === Actor.Player || state.Enemies[foe].Alive)) {
        state = Break(state, actor, foeSide, foe, broken, foeSide === Actor.Enemy ? foe : unit, events);
      }
    }

    if (empowerUsed) state = Consume(state, actor, unit, StatusKind.Empower, events);
  }

  if (AttributeRule.HasMovement(face)) {
    events.push(U(new FaceResolved(actor, sourceId, FaceKind.Move), unit));
  }
  if (face.Move !== 0) state = MoveSelf(state, actor, unit, face.Move, events);
  if (face.Push !== 0 && hit.length > 0) {
    // §7.3: push (away) / pull (in) the opponent; Guard does not stop it. A large opponent
    // refuses it, and so does one standing in 錨の構え. The cells a push could not take
    // become wall damage; a pull just stops.
    const cells = Combat.CellsAfterSlow(Math.abs(face.Push), Get(state, actor, unit).Statuses);
    let blockedBySlow = false;
    for (const foe of PushOrder(state, foeSide, hit, face.Push)) {
      if (foeSide === Actor.Enemy && !state.Enemies[foe].Alive) continue;
      let other = Get(state, foeSide, foe);
      const eventUnit = foeSide === Actor.Enemy ? foe : unit;
      if (other.Size >= 2) {
        events.push(U(new PushRefused(foeSide, other.Size), eventUnit));
        continue;
      }
      const immune = firstOrDefault(other.StanceList, (entry) => entry.Def.Hook === StanceHook.PushImmune && !entry.StoppedIn(state.Turn));
      if (immune != null) {
        events.push(U(new StanceFired(foeSide, immune.Source, StanceHook.PushImmune), eventUnit));
        events.push(new PushRefused(foeSide, other.Size).with({ Unit: eventUnit, ByStance: true }));
        continue;
      }
      if (cells === 0) {
        if (!blockedBySlow) events.push(U(new MoveBlocked(actor, StatusKind.Slow), unit));
        blockedBySlow = true;
        continue;
      }

      // A positive Push sends the opponent away from us, which is "away" from their foe (−).
      const shift = Field.Move(state, foeSide, -Math.sign(face.Push) * cells, foe);
      if (shift.To !== shift.From) {
        other = other.with({ Cell: shift.To });
        state = Set(state, foeSide, foe, other);
        events.push(U(new CellsMoved(foeSide, shift.From, shift.To, true), foe));
      }
      if (face.Push > 0 && shift.Blocked > 0) {
        state = RaiseScale(state, foeSide, events);
        other = Get(state, foeSide, foe);
        const raw = Field.WallDamage(shift.Blocked);
        const { Damage: damage, GuardAfter: guardAfter, Absorbed: absorbed } = Combat.ApplyGuard(raw, other.Guard);
        other = other.with({ Guard: guardAfter, Hp: Math.max(0, other.Hp - damage) });
        state = Set(state, foeSide, foe, other);
        events.push(U(new WallHit(foeSide, shift.Blocked, raw, absorbed, damage, other.Guard, other.Hp), foe));
        const fall = OnFall(state, foeSide, foe, events);
        state = fall.state;
        if (fall.over) return state;
      }
    }
  }

  // A Guard +n trait lands as Guard even on a card without a Guard face
  // (swordsman_cards_v4.md §1.2), and a move card may carry a small Guard of its own.
  if (hasFlag(attributes, BattleAttribute.Guard)) {
    events.push(U(new FaceResolved(actor, sourceId, FaceKind.Guard), unit));
  }
  const guardGain = guardBase > 0 ? Math.max(0, guardBase - intimidate) : 0;
  if (guardGain > 0) {
    self = Get(state, actor, unit);
    self = self.with({ Guard: self.Guard + guardGain });
    state = Set(state, actor, unit, self);
    events.push(U(new GuardGained(actor, guardGain, self.Guard), unit));
  }

  if (hasFlag(attributes, BattleAttribute.Skill)) {
    events.push(U(new FaceResolved(actor, sourceId, FaceKind.Skill), unit));
  }
  const heal = face.Heal > 0 ? face.Heal + focus.Heal : 0;
  if (heal > 0) {
    self = Get(state, actor, unit);
    const hpAfter = Math.min(self.MaxHp, self.Hp + heal);
    state = Set(state, actor, unit, self.with({ Hp: hpAfter }));
    events.push(U(new Healed(actor, hpAfter - self.Hp, hpAfter), unit));
  }

  // §5: the face's statuses, then the trait's. The player holds at most six kinds; an enemy
  // has no kind limit. A ターンで減る型 word stops at four on either side (#205).
  // Statuses on the opponent need the blow to have reached them.
  // A face of several blows has put its opponent statuses on already, after the first blow.
  // 集中 grows the face's 出血 / 再生 to the next column; a trait's grant is not the column's.
  const grants = [];
  for (const grant of face.StatusList) {
    if (grant.OnSelf || face.Hits < 2 || !attacks) grants.push(grant.with({ Stacks: focus.StacksOf(grant) }));
  }
  grants.push(...outcome.GrantList);
  for (const grant of grants) {
    if (grant.Stacks <= 0) continue;
    if (grant.OnSelf) {
      state = ApplyStatus(state, actor, actor, unit, unit, grant.Kind, grant.Stacks, events, grant.Cap);
      continue;
    }
    for (const foe of hit) {
      if (foeSide === Actor.Enemy && !state.Enemies[foe].Alive) continue;
      state = ApplyStatus(state, actor, foeSide, foe, foeSide === Actor.Enemy ? foe : unit, grant.Kind, grant.Stacks, events, grant.Cap);
    }
  }
  if (face.StaminaGain > 0) state = GainStamina(state, actor, unit, face.StaminaGain, events);

  // #334 (roster §6.4 root_st): the face stops the newest permanent effect of each opponent
  // it reached. Like a status on the opponent, it needs the blow to have reached them.
  if (face.StopStance) {
    for (const foe of hit) {
      if (foeSide === Actor.Enemy && !state.Enemies[foe].Alive) continue;
      if (Combat.IsDefeated(Get(state, foeSide, foe).Hp)) continue;
      state = StopNewestStance(state, foeSide, foe, foeSide === Actor.Enemy ? foe : unit, events);
    }
  }

  // §4 (v4.4): the stance face joins the list of permanent effects. Nothing ends and there
  // is no cap; the same stance twice is two entries and works twice.
  if (hasFlag(attributes, BattleAttribute.Stance)) {
    events.push(U(new FaceResolved(actor, sourceId, FaceKind.Stance), unit));
  }
  if (face.Stance != null) {
    self = Get(state, actor, unit);
    state = Set(state, actor, unit, self.WithStance(face.Stance, sourceId));
    events.push(U(new StanceSet(actor, sourceId, sourceName, face.Stance), unit));
  }

  const draw = face.Draw + outcome.Draw;
  if (draw > 0 && actor === Actor.Player) {
    state = DrawCards(state, draw, rng, events);
  }

  // 追撃 waits for the next attack face, not this card's own (§2.3).
  if (outcome.FollowUp > 0) {
    self = Get(state, actor, unit);
    state = Set(state, actor, unit, self.with({ FollowUp: self.FollowUp + outcome.FollowUp }));
  }
  return state;
}

/**
 * §7.3: 前へ / 後ろへ n of the holder's own, as far as the line allows — a move face, or the 俊敏
 * free cell (#48). 呪縛 stops it outright and 鉤爪 catches a move back (#51); the mover's own 鈍足
 * takes one cell off. A cell that changes is 移動後 (§2.3) for the rest of the turn and records
 * the way it went (LastMove, #51: root_m), and an enemy that moves itself pays each 根縛り the
 * player holds.
 */
function MoveSelf(state, actor, unit, move, events) {
  const self = Get(state, actor, unit);
  if (self.Statuses.Has(StatusKind.Binding)) {
    // #51 (roster §4.1 呪縛): the holder's own movement effect does not resolve at all.
    events.push(U(new MoveBlocked(actor, StatusKind.Binding), unit));
    return state;
  }

  const cells = Combat.CellsAfterSlow(Math.abs(move), self.Statuses);
  if (cells === 0) {
    events.push(U(new MoveBlocked(actor, StatusKind.Slow), unit));
    return state;
  }

  const shift = Field.Move(state, actor, Math.sign(move) * cells, unit);
  if (shift.To === shift.From) return state;

  if (move < 0 && self.Statuses.Has(StatusKind.Hook)) {
    // #51 (roster §5.1 鉤爪): a move back that would go is stopped at 0 cells and spends one
    // stack. One that 鈍足 already stopped, or that the edge of the line would stop anyway,
    // is not caught, and keeps the stack.
    state = Consume(state, actor, unit, StatusKind.Hook, events);
    events.push(U(new MoveBlocked(actor, StatusKind.Hook), unit));
    if (actor === Actor.Player) state = state.with({ PlayerHistory: state.History.WithHookSnag(state.Turn) });
    return state;
  }

  // 移動後 (§2.3): the holder's own cell changed, so the rest of this turn reads it.
  state = Set(state, actor, unit, self.with({ Cell: shift.To, Moved: true, LastMove: Math.sign(move) }));
  events.push(U(new CellsMoved(actor, shift.From, shift.To, false), unit));

  // 根縛り: an enemy that moves itself pays for it for each such stance the player holds.
  if (actor === Actor.Enemy) {
    for (const bind of state.Player.StanceList) {
      if (bind.Def.Hook !== StanceHook.BreakOnFoeMove || bind.Def.Break <= 0 || bind.StoppedIn(state.Turn)) continue;
      events.push(U(new StanceFired(Actor.Player, bind.Source, StanceHook.BreakOnFoeMove), unit));
      state = Break(state, Actor.Player, Actor.Enemy, unit, bind.Def.Break, unit, events);
    }
  }
  return state;
}

/**
 * What the one hit does back, once the blow has landed and it still stands: 見切り returns half of
 * what its Guard absorbed (rounded up; spent only when there was something to return), then an
 * OnHit stance reacts (司祭の祈り, 槍衾).
 */
function OnHitReactions(state, attacker, attackerUnit, victim, victimUnit, absorbed, events) {
  const enemyUnit = victim === Actor.Enemy ? victimUnit : attackerUnit;

  let hurt = Get(state, victim, victimUnit);
  if (hurt.Statuses.Has(StatusKind.Parry) && absorbed > 0) {
    state = Consume(state, victim, victimUnit, StatusKind.Parry, events);
    const raw = Half(absorbed);
    let striker = Get(state, attacker, attackerUnit);
    const { Damage: damage, GuardAfter: guardAfter, Absorbed: soaked } = Combat.ApplyGuard(raw, striker.Guard);
    striker = striker.with({ Guard: guardAfter, Hp: Math.max(0, striker.Hp - damage) });
    state = Set(state, attacker, attackerUnit, striker);
    events.push(U(new Reflected(victim, attacker, raw, soaked, damage, striker.Guard, striker.Hp), enemyUnit));
  }

  // §4 (v4.4): each OnHit stance on the list reacts on its own; OncePerTurn is counted per entry.
  const entries = Get(state, victim, victimUnit).StanceList.length;
  for (let i = 0; i < entries; i++) {
    hurt = Get(state, victim, victimUnit);
    const entry = hurt.StanceList[i];
    const stance = entry.Def;
    if (stance.Hook !== StanceHook.OnHit || entry.StoppedIn(state.Turn)) continue;
    if (stance.OncePerTurn && entry.ReactedTurn === state.Turn) continue;

    events.push(U(new StanceFired(victim, entry.Source, StanceHook.OnHit), enemyUnit));
    const reacted = hurt.StanceList.slice();
    reacted[i] = entry.with({ ReactedTurn: state.Turn });
    state = Set(state, victim, victimUnit, hurt.with({ Stances: reacted }));
    if (stance.Stamina > 0) state = GainStamina(state, victim, victimUnit, stance.Stamina, events);
    if (stance.Guard > 0) {
      hurt = Get(state, victim, victimUnit);
      hurt = hurt.with({ Guard: hurt.Guard + stance.Guard });
      state = Set(state, victim, victimUnit, hurt);
      events.push(U(new GuardGained(victim, stance.Guard, hurt.Guard), enemyUnit));
    }
    if (stance.Status != null && stance.StatusStacks > 0 && !Combat.IsDefeated(Get(state, attacker, attackerUnit).Hp)) {
      state = ApplyStatus(state, victim, attacker, attackerUnit, enemyUnit, stance.Status, stance.StatusStacks, events);
    }
  }
  return state;
}

/**
 * #334 (roster §6.4 root_st, battle_core_v4 §4): the holder's newest permanent effect — the last
 * entry on its list, which is kept in the order the entries were put on — does nothing for the
 * rest of this turn and the whole next one. One already stopped is stopped again, to the later
 * turn. A holder with no permanent effect loses nothing, and nothing is said.
 */
function StopNewestStance(state, holder, unit, eventUnit, events) {
  const self = Get(state, holder, unit);
  const list = self.StanceList;
  if (list.length === 0) return state;

  const newest = list.length - 1;
  const through = state.Turn + 1;
  const stopped = list.slice();
  stopped[newest] = list[newest].with({ StoppedThrough: through });
  events.push(U(new StanceStopped(holder, list[newest].Source, newest, through), eventUnit));
  return Set(state, holder, unit, self.with({ Stances: stopped }));
}

/**
 * #334: at the start of a turn, every permanent effect (the player's and each enemy's) whose stop
 * ran through the turn before works again, and says so.
 */
function ResumeStances(state, events) {
  state = ResumeStancesOf(state, Actor.Player, 0, 0, events);
  for (let i = 0; i < state.Enemies.length; i++) {
    if (state.Enemies[i].Alive) state = ResumeStancesOf(state, Actor.Enemy, i, i, events);
  }
  return state;
}

function ResumeStancesOf(state, holder, unit, eventUnit, events) {
  const self = Get(state, holder, unit);
  const list = self.StanceList;
  let resumed = null;
  for (let i = 0; i < list.length; i++) {
    if (list[i].StoppedThrough === 0 || list[i].StoppedIn(state.Turn)) continue;
    resumed ??= list.slice();
    resumed[i] = list[i].with({ StoppedThrough: 0 });
    events.push(U(new StanceResumed(holder, list[i].Source, i), eventUnit));
  }
  return resumed == null ? state : Set(state, holder, unit, self.with({ Stances: resumed }));
}

/** §4: the turn-end stance (根渡り, 霞み足), before 構え. */
function StanceAtTurnEnd(state, actor, unit, events) {
  let self = Get(state, actor, unit);
  for (const entry of self.StanceList) {
    const stance = entry.Def;
    if (stance.Hook !== StanceHook.TurnEnd || entry.StoppedIn(state.Turn) || !StanceHolds(state, actor, unit, stance)) continue;

    events.push(U(new StanceFired(actor, entry.Source, StanceHook.TurnEnd), unit));
    if (stance.Guard > 0) {
      self = self.with({ Guard: self.Guard + stance.Guard });
      events.push(U(new GuardGained(actor, stance.Guard, self.Guard), unit));
    }
    if (stance.NextRecovery > 0) self = self.with({ NextTurnRecoveryBonus: self.NextTurnRecoveryBonus + stance.NextRecovery });
  }
  return Set(state, actor, unit, self);
}

/**
 * §4 `when` for the stances that do not look at one foe: the gap is the nearest enemy's for the
 * player and the player's for an enemy.
 */
function StanceHolds(state, actor, unit, stance) {
  switch (stance.When) {
    case StanceWhen.Always: return true;
    case StanceWhen.GapAtLeast: return OwnGap(state, actor, unit) >= stance.Threshold;
    case StanceWhen.GapAtMost: return OwnGap(state, actor, unit) <= stance.Threshold;
    case StanceWhen.MovedThisTurn: return Get(state, actor, unit).Moved;
    case StanceWhen.OmenAttack:
      if (actor !== Actor.Player) return false;
      for (const i of state.Living) {
        const omen = state.Enemies[i].Omen;
        if (omen != null && omen.Label.Kind === OmenKind.Attack) return true;
      }
      return false;
    default:
      return false;
  }
}

/** §4 条件付き加算: the power an AttackBonus stance adds to a blow on this foe. 0 when it does not hold. */
function StanceAttackBonus(state, actor, unit, foeSide, foe, events = null) {
  let total = 0;
  for (const entry of Get(state, actor, unit).StanceList) {
    const stance = entry.Def;
    if (stance.Hook !== StanceHook.AttackBonus || entry.StoppedIn(state.Turn) || !StanceHoldsOn(state, actor, unit, stance, foeSide, foe)) continue;
    total += stance.Power;
    if (stance.Power > 0 && events != null) {
      events.push(U(new StanceFired(actor, entry.Source, StanceHook.AttackBonus), foeSide === Actor.Enemy ? foe : unit));
    }
  }
  return total;
}

/**
 * §4 攻撃ごとの状態付与 (#253): a StatusOnAttack stance puts its word on the foe a blow has just
 * landed on, when the condition holds for that foe. The caller has checked the foe stands.
 */
function StanceStatusOnAttack(state, actor, unit, foeSide, foe, events) {
  const eventUnit = foeSide === Actor.Enemy ? foe : unit;
  for (const entry of Get(state, actor, unit).StanceList) {
    const stance = entry.Def;
    if (stance.Hook !== StanceHook.StatusOnAttack || stance.Status == null || stance.StatusStacks <= 0) continue;
    if (entry.StoppedIn(state.Turn)) continue;
    if (!StanceHoldsOn(state, actor, unit, stance, foeSide, foe)) continue;
    if (foeSide === Actor.Enemy && !state.Enemies[foe].Alive) break;

    events.push(U(new StanceFired(actor, entry.Source, StanceHook.StatusOnAttack), eventUnit));
    state = ApplyStatus(state, actor, foeSide, foe, eventUnit, stance.Status, stance.StatusStacks, events);
  }
  return state;
}

/** §4 `when` for the stances that look at the one foe hit (AttackBonus, StatusOnAttack). */
function StanceHoldsOn(state, actor, unit, stance, foeSide, foe) {
  const gap = actor === Actor.Player ? state.GapTo(foe) : state.GapTo(unit);
  switch (stance.When) {
    case StanceWhen.Always: return true;
    case StanceWhen.GapAtMost: return gap <= stance.Threshold;
    case StanceWhen.GapAtLeast: return gap >= stance.Threshold;
    case StanceWhen.MovedThisTurn: return Get(state, actor, unit).Moved;
    case StanceWhen.TargetHasStatus: return stance.Status != null && Get(state, foeSide, foe).Statuses.Has(stance.Status);
    default: return false;
  }
}

function OwnGap(state, actor, unit) {
  if (actor === Actor.Enemy) return state.GapTo(unit);
  const nearest = state.Nearest;
  return nearest < 0 ? 0 : state.GapTo(nearest);
}

function GainStamina(state, actor, unit, amount, events) {
  const self = Get(state, actor, unit);
  // §9.1 (#452): stamina 息継ぎの笛 took past the maximum is kept, but a gain adds nothing to it.
  const staminaAfter = self.Stamina >= self.MaxStamina ? self.Stamina : Math.min(self.MaxStamina, self.Stamina + amount);
  events.push(U(new StaminaGained(actor, staminaAfter - self.Stamina, staminaAfter), unit));
  return Set(state, actor, unit, self.with({ Stamina: staminaAfter }));
}

/** 崩し: target loses stamina (floor 0). */
function Break(state, breaker, target, targetUnit, amount, eventUnit, events) {
  const other = Get(state, target, targetUnit);
  const staminaAfter = Math.max(0, other.Stamina - amount);
  events.push(U(new StaminaBroken(breaker, target, other.Stamina - staminaAfter, staminaAfter), eventUnit));
  return Set(state, target, targetUnit, other.with({ Stamina: staminaAfter }));
}

/** §5 使うと減る型: one stack spent. */
function Consume(state, holder, unit, kind, events) {
  const self = Get(state, holder, unit);
  const after = self.Statuses.Consume(kind);
  events.push(U(new StatusConsumed(holder, kind, after.Stacks(kind)), unit));
  return Set(state, holder, unit, self.with({ Statuses: after }));
}

/**
 * §5: stacks on one side; the player takes on at most six kinds, an enemy any number. A
 * ターンで減る型 word stops at four on either side and the rest is dropped (#205): that is not a
 * refusal, so the event says how much was dropped instead.
 */
function ApplyStatus(state, by, target, targetUnit, eventUnit, kind, stacks, events, cap = 0) {
  const limit = target === Actor.Player ? Constants.StatusKindsPlayer : null;
  const other = Get(state, target, targetUnit);
  const before = other.Statuses.Stacks(kind);

  // §9.1 竜語の符 (#452): a boss-only word sits the ward's stacks lower on the player.
  let warded = 0;
  if (target === Actor.Player && Statuses.IsBossOnly(kind) && state.Tools.BossWordWard > 0) {
    const ward = Ward(state, kind, stacks, cap, before);
    state = ward.state;
    warded = ward.warded;
    cap = ward.wardedCap;
  }

  // #51: a grant's own cap (the boss words, roster §4.1 / §5.1 / §6.2) drops what would go past
  // it, the same way the turn-decay cap does; a word already at it takes nothing.
  // giving is 0 only when the word is already held at the cap or the ward kept it all off,
  // so neither is a refusal.
  const offered = stacks - warded;
  const giving = cap > 0 ? Math.min(offered, Math.max(0, cap - before)) : offered;
  const after = giving > 0 ? other.Statuses.Add(kind, giving, limit) : other.Statuses;
  const refused = giving > 0 && !after.Has(kind);
  const dropped = refused ? 0 : offered - (after.Stacks(kind) - before);
  events.push(new StatusApplied(by, target, kind, stacks, after.Stacks(kind), refused).with({ Unit: eventUnit, Dropped: dropped, Warded: warded }));
  state = Set(state, target, targetUnit, other.with({ Statuses: after }));

  // #51 (roster §6.4 root_sk): what the player has put on an enemy, counted by turn.
  if (by === Actor.Player && target === Actor.Enemy && after.Stacks(kind) > before) {
    state = state.with({ PlayerHistory: state.History.WithInfliction(state.Turn) });
  }

  // #51 (battle_core_v4 §6.2 瘴気纏い): each stack lowers the holder's maximum stamina by 1, and
  // what it holds is cut to the new maximum.
  if (kind === StatusKind.MiasmaShroud && after.Stacks(kind) > before) {
    const holder = Get(state, target, targetUnit);
    const lowered = after.Stacks(kind) - before;
    const maxAfter = Math.max(Constants.MaxStaminaFloor, holder.MaxStamina - lowered);
    const staminaAfter = Math.min(holder.Stamina, maxAfter);
    events.push(U(new MaxStaminaChanged(target, maxAfter - holder.MaxStamina, maxAfter, staminaAfter), eventUnit));
    state = Set(state, target, targetUnit, holder.with({ MaxStamina: maxAfter, Stamina: staminaAfter }));
  }
  return state;
}

/**
 * §9.1 竜語の符 (#452): how many of stacks the ward keeps off a boss-only word headed for the
 * player (warded), and the cap the rest then meets (wardedCap, 0 for none).
 *
 * A 戦闘終了まで word (瘴気纏い / 深み / 根張り / 枯らし) never leaves, so it sits exactly the ward
 * lower than it would: the first ward-many stacks given this battle are kept off (counted in
 * CarriedTools.Warded) and its cap is the ward lower; a cap the ward takes to 0 lets nothing on.
 * What the lowered cap stops is counted as warded, not dropped. A word that wears off (呪縛 by the
 * turn, 鉤爪 by use) comes and goes, so every grant of it is the ward lower, floor 0.
 * C# `out int warded, out int wardedCap` → returns { state, warded, wardedCap }.
 */
function Ward(state, kind, stacks, cap, before) {
  const tools = state.Tools;
  const ward = tools.BossWordWard;
  let wardedCap = cap;
  let warded;
  if (Statuses.DecayOf(kind) !== StatusDecay.Lasting) {
    warded = Math.min(stacks, ward);
    return { state, warded, wardedCap };
  }

  const keptOff = Math.min(stacks, Math.max(0, ward - tools.WardedStacks.Stacks(kind)));
  warded = keptOff;
  if (cap > 0) {
    const lowered = cap - ward;
    if (lowered <= 0) {
      warded = stacks;
    } else {
      const offered = stacks - keptOff;
      const underCap = Math.min(offered, Math.max(0, cap - before));
      const underLowered = Math.min(offered, Math.max(0, lowered - before));
      warded = keptOff + (underCap - underLowered);
      wardedCap = lowered;
    }
  }
  if (keptOff > 0) {
    state = state.with({ PlayerTools: tools.with({ Warded: tools.WardedStacks.Add(kind, keptOff) }) });
  }
  return { state, warded, wardedCap };
}

/**
 * §9.1 竜鱗の欠片 (#452): the first enemy blow or wall damage that reaches the player this battle
 * meets the scale's Guard first. The Guard is gained just before it resolves (a GuardGained on the
 * player) and then behaves as any Guard: what the blow does not take stays until the player's
 * next turn start (§9 step 1).
 */
function RaiseScale(state, victim, events) {
  if (victim !== Actor.Player) return state;
  const scale = state.Tools.ScaleGuard;
  if (scale <= 0) return state;
  const player = state.Player.with({ Guard: state.Player.Guard + scale });
  events.push(new GuardGained(Actor.Player, scale, player.Guard));
  return state.with({ Player: player, PlayerTools: state.Tools.with({ ScaleGuard: 0 }) });
}

function RecordPlayed(state, actor, unit, attributes) {
  const self = Get(state, actor, unit);
  const played = [...self.PlayedThisTurn, attributes];
  return Set(state, actor, unit, self.with({ Played: played }));
}

/** Half, rounded up: 転換 and 見切り. */
function Half(value) {
  return value <= 0 ? 0 : idiv(value + 1, 2);
}

/**
 * §17.6 F9: after a hit, see who fell. Over is true when that ended the battle (the player, or
 * the last standing enemy); the caller stops resolving. An enemy that falls while others stand
 * leaves its cells and its omen, and the card goes on.
 * C# `out bool over` → returns { state, over }.
 */
function OnFall(state, side, unit, events) {
  if (!Combat.IsDefeated(Get(state, side, unit).Hp)) return { state, over: false };
  if (side === Actor.Player || state.Living.length === 0) {
    return { state, over: true };
  }
  events.push(U(new EnemyDefeated(Actor.Enemy), unit));
  return { state: state.WithOmen(unit, null), over: false };
}

/** A push moves the farthest foe first, a pull the nearest first; the player is a single foe. */
function PushOrder(state, foeSide, hit, push) {
  if (foeSide === Actor.Player || hit.length < 2) return hit;
  const order = hit.slice();
  sortByCell(state, order);
  if (push > 0) order.reverse();
  return order;
}

function CheckReserve(state, actor, unit, events) {
  let self = Get(state, actor, unit);
  const gained = Combat.ReserveGuard(self.Stamina);
  self = self.with({ Guard: self.Guard + gained });
  events.push(U(new ReserveChecked(actor, self.Stamina, gained, self.Guard), unit));
  return Set(state, actor, unit, self);
}

/**
 * §9 steps 6 and 11: lost when the player is down, won when no enemy stands. If both happen at
 * once the player has lost: the canon does not say, so the stricter reading is taken.
 */
function CheckDefeat(state, actor, unit, events) {
  const result =
    Combat.IsDefeated(state.Player.Hp) ? GameResult.Lost
      : state.Living.length === 0 ? GameResult.Won
        : GameResult.Ongoing;

  events.push(U(new DefeatChecked(actor, result), unit));
  if (result === GameResult.Ongoing) return state;

  events.push(U(new BattleEnded(result === GameResult.Won ? Actor.Player : Actor.Enemy, result), unit));
  return state.with({ Result: result, Phase: BattlePhase.Finished });
}

/**
 * §9 step 12, for one enemy. It judges what it can pay for with the stamina it will hold when the
 * omen is carried out — after its next recovery — so an omen only becomes a rest when something
 * drained it in between. The canon does not fix this moment; see #70. The branch is this enemy's
 * gap band at this moment (§6.1).
 */
function DecideNextOmen(state, unit, events) {
  let enemy = state.Enemies[unit];

  // #50: the boss's tree is asked first. A switch that starts or stops holding is announced.
  const view = AdaptationViewOf(state, unit);
  const active = EnemyAi.ActiveSwitch(enemy.Def, view);

  // #51: a switch holding for the first time this battle is remembered (a stage that never
  // goes back reads it), and moves the body when it says so (EnterMove).
  if (enemy.Def.Switches != null) {
    for (const each of enemy.Def.Switches) {
      if (view.HasHeld(each.Id) || !each.Holds(view)) continue;
      const held = [...state.Enemies[unit].Held, each.Id];
      state = state.WithUnit(unit, state.Enemies[unit].with({ HeldSwitches: held }));
      if (each.EnterMove !== 0) state = MoveBySwitch(state, unit, each.EnterMove, events);
    }
    enemy = state.Enemies[unit];
  }

  const activeId = active != null ? active.Id : null;
  if (activeId !== (enemy.ActiveSwitch ?? null)) {
    events.push(U(new TreeSwitched(Actor.Enemy, enemy.ActiveSwitch ?? null, activeId), unit));
  }

  // #51 (roster §6.4 root_sk): the switch that shakes off every word the enemy holds. It
  // clears at every decision where it holds, not only when it starts holding: root_sk holds
  // only in the decision right after the turn that triggered it, so holding twice in a row
  // means it was triggered twice, and each trigger clears.
  if (active != null && active.Cleanse && enemy.Body.Statuses.Kinds.length > 0) {
    for (const kind of enemy.Body.Statuses.Kinds) {
      events.push(U(new StatusCleared(Actor.Enemy, kind, enemy.Body.Statuses.Stacks(kind)), unit));
    }
    state = state.WithEnemy(unit, enemy.Body.with({ Statuses: StatusSet.Empty }));
    enemy = state.Enemies[unit];
  }

  const gap = state.GapTo(unit);
  const stamina = EnemyAi.StaminaAtAction(enemy.Def, enemy.Body);

  // #51 (roster §4.3): an action the player's word keeps out, as it will stand after the
  // player's next turn start, is passed over like a spent stance — and, #497 (v4.18), one
  // whose word the player already holds at the cap of the switch decided here.
  const skip = [...enemy.Spent];
  skip.push(...EnemyAi.Barred(enemy.Def, state.Player.Statuses, true, active, state.Tools.WardedStacks));
  const omen = EnemyAi.DecideOmen(enemy.Def, gap, stamina, skip, active);
  events.push(U(new OmenSet(Actor.Enemy, omen, true), unit));

  // §17.6 F11 (#50): an elite or a boss shows its second step too, as a 予定.
  const plan = EnemyAi.DecidePlan(enemy.Def, gap, stamina, omen, skip, active);
  if (plan != null) events.push(U(new PlanSet(Actor.Enemy, plan, true), unit));

  return state.WithUnit(unit, enemy.with({ Omen: omen, Plan: plan, ActiveSwitch: activeId }));
}

/** #50: what an adaptation of this enemy reads at this moment. #51 adds N and the switches that have held. */
function AdaptationViewOf(state, unit) {
  const enemy = state.Enemies[unit];
  return new AdaptationView(
    state.Turn, enemy.Body.Hp, enemy.Body.MaxHp, enemy.ExecutedActions, state.History,
    state.GapTo(unit), enemy.Held);
}

/**
 * #51: the enemy moves itself because a switch says so — a stage's EnterMove, or a step of a
 * Sway. As far as the line allows, with no 鈍足 taken off (the roster's 「出られるだけ」 /
 * 「動けるだけ」). It is the enemy moving itself, so 根縛り fires on it as on a move face.
 */
function MoveBySwitch(state, unit, cells, events) {
  const shift = Field.Move(state, Actor.Enemy, cells, unit);
  if (shift.To === shift.From) return state;
  state = state.WithEnemy(unit, state.Enemies[unit].Body.with({ Cell: shift.To }));
  events.push(U(new CellsMoved(Actor.Enemy, shift.From, shift.To, false), unit));
  for (const bind of state.Player.StanceList) {
    if (bind.Def.Hook !== StanceHook.BreakOnFoeMove || bind.Def.Break <= 0 || bind.StoppedIn(state.Turn)) continue;
    events.push(U(new StanceFired(Actor.Player, bind.Source, StanceHook.BreakOnFoeMove), unit));
    state = Break(state, Actor.Player, Actor.Enemy, unit, bind.Def.Break, unit, events);
  }
  return state;
}

/**
 * #51 (roster §6.1 段階 3): once a switch with Sway has held, the body steps at the start of each
 * of its phases — 前へ 1 on an odd turn, 後ろへ 1 on an even one — as far as it can.
 */
function Sway(state, unit, events) {
  const enemy = state.Enemies[unit];
  if (enemy.Def.Switches == null || enemy.Held.length === 0) return state;
  for (const each of enemy.Def.Switches) {
    if (!each.Sway || !enemy.Held.includes(each.Id)) continue;
    return MoveBySwitch(state, unit, state.Turn % 2 === 1 ? 1 : -1, events);
  }
  return state;
}

/**
 * #50 予定変更: the second action, read again after the first, is not the one planned. The plan is
 * replaced by what the tree now says; the event carries both.
 */
function RevisePlan(state, unit, now, events) {
  const planned = state.Enemies[unit].Plan;
  if (planned == null || csEquals(planned, now)) return state;
  events.push(U(new PlanChanged(Actor.Enemy, planned, now), unit));
  return state.WithUnit(unit, state.Enemies[unit].with({ Plan: now }));
}

// ---- What a card or action reads (§2.3) ----

/**
 * The board a player's card reads, judged against enemy read. The card is still in the hand.
 * 温存 reads the stamina left once the cost is paid (§2.3), so a コストの割引 entry (§4) the card
 * would spend comes off what it pays. The card's own コスト −1 trait does not: it is judged on
 * this context, and no card holds both it and 温存.
 */
function PlayerContext(state, def, read) {
  const foe = state.Enemies[read];
  const self = state.Player;
  const paid = CostDiscountEntry(state, def, def.Cost) >= 0 ? def.Cost - 1 : def.Cost;
  return new TraitContext(
    state.GapTo(read), // Gap
    foe.Body.Guard, // OpponentGuard
    self.Stamina - paid, // StaminaAfterUse
    self.Stamina, // StaminaBefore
    foe.Body.Stamina, // OpponentStamina
    self.PlayedThisTurn, // Played
    state.Hand.length - 1, // HandAfterPlay
    foe.Omen != null ? foe.Omen.Label.Kind : null, // OpponentOmen
    self.Statuses, // SelfStatuses
    foe.Body.Statuses, // OpponentStatuses
    def.Attribute, // Attributes
    self.Moved); // Moved
}

/** The board an enemy action reads: the player is its opponent, and what it did earlier this phase is its playedAttributes. */
function EnemyContext(state, unit, action) {
  const self = state.Enemies[unit].Body;
  const player = state.Player;
  return new TraitContext(
    state.GapTo(unit), // Gap
    player.Guard, // OpponentGuard
    self.Stamina - action.Cost, // StaminaAfterUse
    self.Stamina, // StaminaBefore
    player.Stamina, // OpponentStamina
    self.PlayedThisTurn, // Played
    undefined, // HandAfterPlay: default (int.MaxValue)
    undefined, // OpponentOmen: default (null)
    self.Statuses, // SelfStatuses
    player.Statuses, // OpponentStatuses
    action.Attribute, // Attributes
    self.Moved); // Moved
}

function TraitsOf(trait) {
  return trait == null ? [] : [trait];
}

// ---- Whom a card reads and hits (§7.4) ----

/**
 * The enemy whose N and Guard the card's trait reads: the one it is dropped on for a card aimed at
 * one, the nearest in reach for a card aimed at all, and the nearest standing enemy for a card
 * aimed at nobody (§7.4 自分向きの札).
 */
function ReadUnit(state, def, target) {
  if (EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets)) {
    if (def.Targets !== TargetKind.All) return target;
    const reached = Reached(state, def.Face.ReachOrDefault);
    if (reached.length > 0) return reached[0];
  }
  return state.Nearest;
}

/** ReadUnit, falling back to the nearest standing enemy when the one named is not standing. −1 when none stands. */
function ReadableUnit(state, def, target) {
  const read = ReadUnit(state, def, target);
  return IsStanding(state, read) ? read : state.Nearest;
}

/** The standing enemies whose N falls inside reach, nearest first. */
function Reached(state, reach) {
  const reached = [];
  for (const i of state.Living) {
    if (reach.Contains(state.GapTo(i))) reached.push(i);
  }
  sortByCell(state, reached);
  return reached;
}

function IsStanding(state, unit) {
  return unit >= 0 && unit < state.Enemies.length && state.Enemies[unit].Alive;
}

function Require(state, phase, move) {
  if (state.Result !== GameResult.Ongoing) {
    throw new InvalidOperationException(`${move}: the battle is over (${state.Result}).`);
  }
  if (state.Phase !== phase) {
    throw new InvalidOperationException(`${move}: expected phase ${phase}, but the battle is in ${state.Phase}.`);
  }
}

function FindInHand(state, instanceId) {
  for (const card of state.Hand) {
    if (card.InstanceId === instanceId) return card;
  }
  return null;
}

function Opponent(actor) {
  return actor === Actor.Player ? Actor.Enemy : Actor.Player;
}

/** The player, or enemy unit. */
function Get(state, actor, unit) {
  return actor === Actor.Player ? state.Player : state.Enemies[unit].Body;
}

function Set(state, actor, unit, value) {
  return actor === Actor.Player ? state.with({ Player: value }) : state.WithEnemy(unit, value);
}

/**
 * §9: the turn, end to end. Pure — every move takes a state and hands back a new one with the
 * events that led there. The only randomness is the injected IRng (shuffles).
 *
 * The loop waits in two places. BeginPlayerTurn runs steps 1-5 and stops for the player; PlayCard
 * is step 6, as many times as stamina allows; EndTurn runs steps 7-12 without stopping, enemy
 * phases included, and ends on the next omens. One turn is BeginPlayerTurn → PlayCard × n →
 * EndTurn.
 *
 * Several enemies (§7.4, #47): a card that aims at one enemy names it (the drop zone's pick); the
 * enemies take their phases one by one in definition order, each running steps 9-12; the battle
 * is won when the last one falls. A one-enemy battle emits the same stream as before.
 */
export const TurnLoop = Object.freeze({
  Start,
  BeginPlayerTurn,
  CanPlay,
  CostNow,
  Preview,
  PreviewOmen,
  OmenBoundaryCells,
  OmenMoveCell,
  PreviewRecovery,
  PlayCard,
  CanTakeFreeStep,
  TakeFreeStep,
  EndTurn,
});

// ---------------------------------------------------------------- FocusStep

/** §3.1: 出血 and 再生 are the two words whose stacks the column decides. */
function GrowsWithColumn(kind) {
  return kind === StatusKind.Bleed || kind === StatusKind.Regen;
}

/**
 * §5 集中 (#188, #48): the next card resolves one column to the right, never beyond column 4. A
 * card is written into one column and keeps no table of the others, so the column to the right is
 * read off the Columns scales: the step to the next column is added to its power (the v4.4 attack
 * rows §5 keeps, #364), its Guard and its heal (§3.1's rows), and to the stacks of each 出血 /
 * 再生 its face gives (the stack scale 1 / 2 / 3 / 4). A face only grows on the attribute that
 * owns it (a move card's small Guard does not), and column 4 does not grow.
 *
 * What is the same in every column stays: the other words (2 stacks whatever the column), 崩し
 * (§3.1: not on the scale), the cost, the reach, and a trait's grant (the trait belongs to the
 * card, not to its column). The skill face's "kinds" row is not read: which word a column would
 * add is the card author's choice, not a number.
 */
export class FocusStep extends record('FocusStep', ['Power', 'Guard', 'Heal', ['TickStacks', 0]]) {
  static None = new FocusStep(0, 0, 0);

  static Of(attributes, column, face) {
    if (face == null) throw nullArg('face');
    if (!Columns.IsValid(column)) return FocusStep.None;
    // A face beside a movement effect is one of two things the card does (§2.1: a 面 を 2 つ持つ札
    // reads the 65% scale); the movement is no longer a declared flag, so read it off the face.
    const single = (attributes === BattleAttribute.Attack || attributes === BattleAttribute.Guard
      || attributes === BattleAttribute.Skill) && !AttributeRule.HasMovement(face);
    const power = hasFlag(attributes, BattleAttribute.Attack) && face.Power > 0
      ? Columns.StepRight(single ? Columns.SingleAttackPower : Columns.DualAttackPower, column) : 0;
    const guard = hasFlag(attributes, BattleAttribute.Guard) && face.Guard > 0
      ? Columns.StepRight(single ? Columns.SingleGuard : Columns.DualGuard, column) : 0;
    const heal = hasFlag(attributes, BattleAttribute.Skill) && face.Heal > 0
      ? Columns.StepRight(single ? Columns.SingleHeal : Columns.DualHeal, column) : 0;
    const ticks = face.StatusList.some((grant) => GrowsWithColumn(grant.Kind))
      ? Columns.StepRight(Columns.TickStacks, column) : 0;
    return new FocusStep(power, guard, heal, ticks);
  }

  /** The stacks grant lands with: one column further on the 出血 / 再生 scale, any other word as written. */
  StacksOf(grant) {
    if (grant == null) throw nullArg('grant');
    return GrowsWithColumn(grant.Kind) ? grant.Stacks + this.TickStacks : grant.Stacks;
  }
}
