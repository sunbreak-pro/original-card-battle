// Port of unity-port/BattleCore/BattleEvents.cs
//
// JS shape of the event records:
//   - `export class TurnStarted extends event('TurnStarted', ['Turn'])` = C#
//     `sealed record TurnStarted(Actor Actor, int Turn) : BattleEvent(Actor)`. The constructor takes
//     the base field (Actor) first, then its own: `new TurnStarted(Actor.Player, 1)`.
//   - Init-only properties (`public int Unit { get; init; }`, PushRefused.ByStance,
//     StatusApplied.Dropped / Warded) are own fields with their C# default (0 / false), kept in
//     toJSON and in equality. A C# object initializer `new X(...) { Unit = unit }` is either
//     `new X(..., { Unit: unit })` (a trailing plain object of property names, like C# named
//     arguments) or `new X(...).with({ Unit: unit })`. `ev with { Unit = u }` is `ev.with({ Unit: u })`.
//   - Pattern matching on the event type (`case DamageDealt d:`) is `ev instanceof DamageDealt`.
//
// This module imports nothing but cs.js, so it can be read at any point of an import cycle. The C#
// types the events carry (Actor, StatusKind, CardInstance, Omen, Reach, Trait, TraitOutcome,
// EnemyActionDef, FaceKind, StanceDef, StanceHook, GameResult, BattleState) live in Types.js /
// Traits.js and are only referenced in these comments.

import { defineRecord } from '../../shared/cs.js';

function isPlainObject(x) {
  return x !== null && typeof x === 'object' && Object.getPrototypeOf(x) === Object.prototype;
}

/**
 * defineRecord + init-only properties (`init`: name → C# default) + a trailing named-argument
 * object (see the header). `base` is the parent record class, as for defineRecord.
 */
function record(name, fields, init = {}, base = undefined) {
  const R = defineRecord(name, fields, base);
  const ownInit = Object.entries(init);
  const C = class extends R {
    constructor(...args) {
      let named = null;
      // Only the most-derived record reads the named-argument object; a base constructor called
      // through super() receives just its own positional fields.
      if (new.target.$name === name && args.length > 0 && isPlainObject(args[args.length - 1])) {
        const last = args[args.length - 1];
        const allowed = C.$fields.concat(C.$init);
        const keys = Object.keys(last);
        const known = keys.filter((k) => allowed.includes(k));
        if (keys.length > 0 && known.length === keys.length) {
          named = last;
          args = args.slice(0, -1);
        } else if (known.length > 0) {
          const unknown = keys.filter((k) => !allowed.includes(k));
          throw new Error(`${name}: unknown named argument(s) ${unknown.join(', ')}`);
        }
      }
      super(...args);
      for (const [k, v] of ownInit) this[k] = v;
      if (named) {
        for (const k of Object.keys(named)) {
          if (named[k] !== undefined) this[k] = named[k];
        }
      }
    }

    toJSON() {
      const o = super.toJSON();
      for (const k of this.constructor.$init) o[k] = this[k];
      return o;
    }
  };
  C.$init = (base && base.$init ? base.$init : []).concat(ownInit.map(([k]) => k));
  return C;
}

/**
 * What happened, in the order it happened. The turn loop hands these back beside the new state,
 * and the depiction converter (#73) turns them into the script the screen plays.
 *
 * Every event carries settled values (the *After fields), never inputs to a formula: whoever
 * reads the stream shows numbers and does not compute them. BattleEvent.Actor is
 * the side the event is about.
 *
 * Unit (init-only): §7.4 (#47): which enemy (its index in BattleState.Enemies) is on the enemy
 * end of this event — the enemy acting in its phase, or the enemy a player's card read or
 * hit. 0 in a one-enemy battle and for what concerns the player alone.
 */
export class BattleEvent extends record('BattleEvent', ['Actor'], { Unit: 0 }) {}

/** A record derived from BattleEvent: `fields` are its own positional fields after Actor. */
function event(name, fields, init = {}) {
  return record(name, fields, init, BattleEvent);
}

// ---- Turn frame (§9 steps 1-5, 7-8) ----

export class TurnStarted extends event('TurnStarted', ['Turn']) {}

/** §9 steps 1 and 9: Guard goes to 0 at the start of the holder's own turn. */
export class GuardCleared extends event('GuardCleared', ['Cleared']) {}

/** §9 steps 2 and 9. Amount is what was actually gained once the maximum capped it. */
export class StaminaRecovered extends event('StaminaRecovered', ['Amount', 'StaminaAfter', 'MaxStamina']) {}

/** §5: a ターンで減る型 word lost one stack at its holder's turn start. */
export class StatusTicked extends event('StatusTicked', ['Kind', 'StacksAfter']) {}

/** §9 step 4: one card, so a hand of six is six events. */
export class Drawn extends event('Drawn', ['Card', 'HandCountAfter']) {}

/** §8: the draw pile ran out and the discard pile was shuffled back in. */
export class DeckReshuffled extends event('DeckReshuffled', ['DrawPileCount']) {}

/**
 * §9 steps 5 and 12. Decided is true when the enemy has just chosen this omen (battle start and
 * step 12) and false when step 5 shows the omen already standing.
 */
export class OmenSet extends event('OmenSet', ['Omen', 'Decided']) {}

/**
 * The second step of an elite's or boss's omen (#50, §9 step 12, §17.6 F11): the action it
 * plans to take second, shown as a 予定 beside the first. Decided is as for OmenSet.
 * It is not a commitment: the tree is read again after the first action.
 */
export class PlanSet extends event('PlanSet', ['Plan', 'Decided']) {}

/**
 * 予定変更 (#50): the tree, read again after the first action, gives a different second action
 * than the one planned. Now is a rest omen when nothing in the branch can be paid for, or when
 * the first action itself could not be paid and the phase ends there.
 */
export class PlanChanged extends event('PlanChanged', ['Was', 'Now']) {}

/**
 * A boss's tree was swapped (#50, TreeSwitch): From and To are switch ids, null for
 * the base tree. Emitted at the omen decision that first sees the change.
 */
export class TreeSwitched extends event('TreeSwitched', ['From', 'To']) {}

/** §9 step 7 (and the enemy's own turn end): 構え. GuardGained is 3 or 0. */
export class ReserveChecked extends event('ReserveChecked', ['StaminaLeft', 'GuardGained', 'GuardAfter']) {}

export class HandDiscarded extends event('HandDiscarded', ['Count']) {}

export class TurnEnded extends event('TurnEnded', ['Turn']) {}

// ---- One card or one action (§9 steps 6 and 10) ----

/** The player released a card. GapBefore is the N every gap condition reads (§2.2 / §2.3). */
export class CardPlayed extends event('CardPlayed', ['Card', 'GapBefore']) {}

/** The enemy carried out its omen. GapBefore is the N at execution, the one the reach is judged on (§6). */
export class ActionExecuted extends event('ActionExecuted', ['Action', 'GapBefore']) {}

/**
 * §6 空振り: the player stood outside the action's reach when it was carried out, so its
 * opponent-directed faces did not resolve. The cost was paid and the self faces followed.
 */
export class ActionWhiffed extends event('ActionWhiffed', ['SourceId', 'Gap', 'Reach']) {}

/** §6: the declared action could not be paid for, so the phase is recovery only. */
export class Rested extends event('Rested', ['Declared']) {}

export class StaminaSpent extends event('StaminaSpent', ['Amount', 'StaminaAfter']) {}

/** §2.2: judged once, before any face. Only emitted for a card or action that carries a trait. */
export class TraitEvaluated extends event('TraitEvaluated', ['SourceId', 'Trait', 'Outcome']) {}

/** One face of the card resolved. They come in §2.2 order: Attack, Move, Guard, Skill, Stance (Move is the movement effect, not an attribute). Not emitted for a face a whiff skipped. */
export class FaceResolved extends event('FaceResolved', ['SourceId', 'Face']) {}

/** Actor is the attacker. Raw − Absorbed = Damage; Absorbed is what the target's Guard soaked. */
export class DamageDealt extends event('DamageDealt', [
  'Target',
  'Raw',
  'Absorbed',
  'Damage',
  'TargetGuardAfter',
  'TargetHpAfter',
]) {}

export class GuardGained extends event('GuardGained', ['Amount', 'GuardAfter']) {}

/**
 * §7.3: Actor is whose cell changed (From → To, To is the near edge for the enemy). Pushed is true
 * when the other side moved them (push / pull). Only emitted when the cell did change.
 */
export class CellsMoved extends event('CellsMoved', ['From', 'To', 'Pushed']) {}

/**
 * §7.3 壁のダメージ: Actor is the pushed side that could not take BlockedCells of the push.
 * Raw − Absorbed = Damage, as in DamageDealt.
 */
export class WallHit extends event('WallHit', [
  'BlockedCells',
  'Raw',
  'Absorbed',
  'Damage',
  'GuardAfter',
  'HpAfter',
]) {}

/**
 * §7.3: Actor is the target of a push / pull that its size (2 or more) refused. ByStance is true
 * when a stance refused it instead (錨の構え, #188).
 */
export class PushRefused extends event('PushRefused', ['Size'], { ByStance: false }) {}

/**
 * §5 俊敏 (#48): the holder takes its free cell, +1 forward or −1 back. What the line made of it
 * follows as a CellsMoved (or a MoveBlocked under 鈍足, or nothing
 * when the line stops it), exactly as for a move face.
 */
export class FreeStepTaken extends event('FreeStepTaken', ['Direction']) {}

/**
 * §5 鈍足: the holder's move, push or pull was shortened to 0 cells and so did not happen. #51:
 * By is 呪縛 when the holder is bound (no own move resolves) and 鉤爪 when a move back was
 * caught (one stack spent, StatusConsumed beside it).
 */
export class MoveBlocked extends event('MoveBlocked', ['By']) {}

/**
 * Actor is who applied it. Stacks is what the face, trait or stance gave; StacksAfter is what
 * the target holds now. Refused is true when the target was at its kind limit (§5) and nothing
 * went on.
 *
 * Dropped (init-only): §5 (#205): what the ターンで減る型 cap threw away. A word already at the cap drops all of it; that is not a refusal.
 * Warded (init-only): §9.1 竜語の符 (#452): the stacks of a boss-only word the player's ward kept off. Not a refusal either.
 */
export class StatusApplied extends event(
  'StatusApplied',
  ['Target', 'Kind', 'Stacks', 'StacksAfter', 'Refused'],
  { Dropped: 0, Warded: 0 },
) {
  /** What actually went on — the number the screen shows. 0 when refused or when all of it was dropped or warded. */
  get Landed() {
    return this.Refused ? 0 : this.Stacks - this.Dropped - this.Warded;
  }
}

export class StaminaGained extends event('StaminaGained', ['Amount', 'StaminaAfter']) {}

// ---- The demo vocabulary (#188): statuses, heal, break, stance ----

/** §5 使うと減る型: the word's effect landed and it lost one stack. Actor is the holder. */
export class StatusConsumed extends event('StatusConsumed', ['Kind', 'StacksAfter']) {}

/**
 * §5 出血 / 再生: HP moved at the holder's turn start. Actor is the holder; Amount is the signed
 * change that happened (−4 for 出血 2; 再生 3 is +6 unless the maximum capped it, 0 at full HP)
 * and HpAfter is settled.
 */
export class StatusHpChanged extends event('StatusHpChanged', ['Kind', 'Amount', 'HpAfter']) {}

/** A heal face: Actor gained Amount HP (after the maximum capped it). */
export class Healed extends event('Healed', ['Amount', 'HpAfter']) {}

/** 崩し: Actor took Amount stamina off Target (floor 0). */
export class StaminaBroken extends event('StaminaBroken', ['Target', 'Amount', 'StaminaAfter']) {}

/**
 * §5 見切り: Actor (the one hit) returned half of what its Guard absorbed to Target (the
 * attacker). Raw − Absorbed = Damage against the attacker's own Guard, as in DamageDealt.
 */
export class Reflected extends event('Reflected', [
  'Target',
  'Raw',
  'Absorbed',
  'Damage',
  'TargetGuardAfter',
  'TargetHpAfter',
]) {}

/** §4: Actor put a stance on its list of permanent effects. Nothing is pushed out (v4.4): the list has no cap and a repeat is another entry. */
export class StanceSet extends event('StanceSet', ['SourceId', 'Name', 'Stance']) {}

/** §4: Actor's stance took effect. What it did follows as its own events (GuardGained, …). */
export class StanceFired extends event('StanceFired', ['SourceId', 'Hook']) {}

/**
 * #334 (roster §6.4 root_st): Actor's newest permanent effect — Index on its list, put there by
 * SourceId — stopped working, through battle turn Through. It stays on the list; see
 * StanceResumed.
 */
export class StanceStopped extends event('StanceStopped', ['SourceId', 'Index', 'Through']) {}

/** #334: Actor's permanent effect at Index (put there by SourceId) works again, from this turn start on. */
export class StanceResumed extends event('StanceResumed', ['SourceId', 'Index']) {}

/** §4: a stance card left for the exile pile once it resolved. It is not drawn again this battle. */
export class CardExiled extends event('CardExiled', ['Card']) {}

/**
 * §7.4 / §17.6 F9: one of several enemies fell (Unit says which) and the battle goes on. It
 * leaves its cells at once. The last enemy to fall ends the battle instead: BattleEnded.
 */
export class EnemyDefeated extends event('EnemyDefeated', []) {}

/** §9 steps 6 and 11. Actor is the side that just acted. */
export class DefeatChecked extends event('DefeatChecked', ['Result']) {}

/** Actor is the winner's side. */
export class BattleEnded extends event('BattleEnded', ['Result']) {}

// ---- The boss-only words and the bosses' switches (#51) ----

/**
 * battle_core_v4 §6.2 瘴気纏い: Actor's maximum stamina moved by Amount (negative: lowered) to
 * MaxAfter, and the stamina held was cut to it (StaminaAfter). The one exception to a maximum
 * that never changes in battle.
 */
export class MaxStaminaChanged extends event('MaxStaminaChanged', ['Amount', 'MaxAfter', 'StaminaAfter']) {}

/** roster §6.4 root_sk: Actor shook off a word it held, all Stacks of it, when the switch that cleanses became active. */
export class StatusCleared extends event('StatusCleared', ['Kind', 'Stacks']) {}

/** One move of the loop: the state it left and what happened on the way there. Not a BattleEvent. */
export class StepResult extends record('StepResult', ['State', 'Events']) {}
