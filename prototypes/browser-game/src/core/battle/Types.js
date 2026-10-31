// Port of unity-port/BattleCore/Types.cs
//
// Battle core v4.3 (battle_document/battle_core_v4.md, 2026-09-22). C# is the source of truth.
//
// This file holds the vertical slice's bones (#69): the values a battle carries (§1), the
// attribute / face / trait shape that cards and enemy actions share (§2, §17.6 F5), and the
// state record the turn loop (#72) drives. It carries no data: the ten prototype cards are
// #71 and the polearm's actions are #70.
//
// v4.3 (#162): the two-valued Position (近間 / 遠間) became a cell on a line (§7.1) and the
// distance N between the two sides (§7.2). Faces carry a reach (§2.4), move by cells (§7.3) and
// push / pull by cells; the enemy tree branches on the gap band (§6.1). v4.2 stays in git history.
//
// ---- How the records here are built (JS port) ----
// Every record is `defineRecord` (src/shared/cs.js) with one addition, C# named arguments:
//   - positional, exactly as C#:            new Trait(TraitCondition.Combo, TraitEffect.PowerBonus, 3)
//   - named only (C# `new Face(Power: 7)`): new Face({ Power: 7 })
//   - positional then named (C# `new Trait(c, e, amount, Threshold: gap)`):
//                                           new Trait(c, e, amount, { Threshold: gap })
// A trailing plain object (prototype Object.prototype) whose keys are all parameter names of the
// record is read as the named arguments; one that mixes known and unknown keys throws (a typo).
// Omitted parameters take their C# default. `with({...})` is the C# `with` expression.
//
// Import-cycle note: this module reads only Constants (a leaf) and cs.js at module-evaluation time.
// Columns / AttributeRule / Field / Combat / StatusSet are read lazily inside getters and methods.

import {
  defineRecord,
  defineEnum,
  defineFlags,
  ArgumentNullException,
  ArgumentOutOfRangeException,
} from '../../shared/cs.js';
import { Constants } from './Constants.js';
import { Columns } from './Columns.js';
import { AttributeRule } from './Attributes.js';
import { Field } from './Field.js';
import { Combat } from './Combat.js';
import { StatusSet } from './Statuses.js';

// ------------------------------------------------------------------ local helpers (not C# types)

/** Array.Empty<T>(): one shared, frozen empty list. */
const EMPTY = Object.freeze([]);

function isPlainObject(x) {
  return x !== null && typeof x === 'object' && Object.getPrototypeOf(x) === Object.prototype;
}

/** .NET's ArgumentNullException(paramName).Message. */
function argumentNull(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

/** .NET's ArgumentOutOfRangeException(paramName, actualValue, message).Message (Linux newline). */
function argumentOutOfRange(param, value, message) {
  const text = message == null ? 'Specified argument was out of the range of valid values.' : message;
  return new ArgumentOutOfRangeException(`${text} (Parameter '${param}')\nActual value was ${value}.`);
}

/**
 * defineRecord + C# named arguments (see the header). `fields` as for defineRecord.
 * Returns a class to extend: `export class Face extends record('Face', [...]) { getters }`.
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

// ------------------------------------------------------------------ enums

/**
 * §2.1 の属性 4 つ (v4.4, #258): 攻撃 / 防御 / スキル / スタンス. Movement is not one: it is an
 * effect any card can carry (Face.Move, Face.Push).
 *
 * Flags because a card DECLARES the faces it carries (攻撃 + 防御 is one card) — that is what
 * CardDef.Attributes holds. What a card COUNTS as is one value, folded by
 * AttributeRule.Fold (CardDef.Attribute): 攻撃 ＞ 防御 ＞ スキル, and
 * スタンス on its own. Everything that counts attributes (played this turn, 連動, 連打, the
 * tally) reads the folded value. Named BattleAttribute, not Attribute, so it never shadows
 * System.Attribute.
 */
export const BattleAttribute = defineFlags({
  None: 0,
  Attack: 1 << 0,
  Guard: 1 << 1,
  Skill: 1 << 2,
  Stance: 1 << 3,
});

/**
 * §2.2: the five things one card resolves, in the order it resolves them. Not an attribute:
 * Move is the movement effect, which sits between the attack and the guard.
 */
export const FaceKind = defineEnum(['Attack', 'Move', 'Guard', 'Skill', 'Stance']);

export const Actor = defineEnum(['Player', 'Enemy']);

export const GameResult = defineEnum(['Ongoing', 'Won', 'Lost']);

/**
 * §9: where the turn loop stands. It only ever waits in two places — before a player turn is
 * opened (steps 1-5) and while the player plays cards (step 6). Steps 7-12 run in one go.
 */
export const BattlePhase = defineEnum(['AwaitingTurnStart', 'PlayerAction', 'Finished']);

/**
 * §2.4 / §7.4 targets. One is the opponent-directed default (the player picks one enemy on the
 * drop zone); All is every enemy inside the reach; Self reads no reach. v4.3's cell left with
 * one enemy per cell (2026-09-23, #169). ally (a status put on another enemy) stays a rule of
 * §2.4 with no user yet (decided 2026-10-03, #269): no enemy carries it, so it has no value here
 * until one does.
 */
export const TargetKind = defineEnum(['One', 'Self', 'All']);

/** §6: 予兆の種別. Rest is the enemy outcome when the declared action cannot be paid for. */
export const OmenKind = defineEnum(['Attack', 'Guard', 'Move', 'Skill', 'Stance', 'Rest']);

/**
 * §5 の状態: all ten words (俊敏 came in with #48). The order is the order the chips are listed
 * in. The six boss-only words follow the ten (#51).
 *
 * - Slow 鈍足: the holder's own cell changes (move, push, pull) are one cell shorter. OnTurn.
 * - Swift 俊敏 (#48): at the start of each turn it is held (the stacks before the tick, as 再生 and
 *   出血 read them), the holder may move one cell forward or back for free. The player takes
 *   it with TurnLoop.TakeFreeStep before the first card, or lets it go; an enemy
 *   steps toward its omen's reach (EnemyAi.FreeStepDirection). OnTurn.
 * - Bleed 出血: HP −2 × stacks at the holder's turn start. OnTurn.
 * - Fragile 脆化: the next attack the holder takes is ×1.5. OnUse.
 * - Intimidate 威圧: the holder's next action that lands an attack face or has a Guard loses 3 power and 3 Guard. OnUse.
 * - Fatigue 疲労: the holder recovers 1 less stamina at its turn start. OnTurn.
 * - Empower 強化: the holder's next attack face is ×1.5. OnUse.
 * - Focus 集中: the holder's next card resolves one column to the right, never beyond column 4
 *   (FocusStep): power, Guard and heal, and the stacks of the 出血 / 再生 it
 *   gives, come from the next column of their scales. OnUse.
 * - Parry 見切り: the next hit the holder's Guard absorbs returns half of it. OnUse.
 * - Regen 再生: HP +2 × stacks at the holder's turn start. OnTurn.
 *
 * Boss-only words (#51): battle_core_v4 §5 「ボス専用の状態」, written in enemy_roster_v4
 * §4.1 / §5.1 / §6.2. Each boss puts two of them on the player; nothing else gives them.
 *
 * - MiasmaShroud 瘴気纏い (大黒蛇 セルク): the holder's max stamina is 1 lower per stack, and the stamina held is cut to it (§6.2). Lasting.
 * - Binding 呪縛 (セルク): the holder's own movement effect does not resolve, and a card of movement only cannot be played. OnTurn.
 * - Hook 鉤爪 (獄竜 ガルド): the holder's own move back is stopped (0 cells), one stack spent each time. A move forward goes through. OnUse.
 * - Depths 深み (ガルド): starting a turn at N 0 to the nearest enemy, the holder recovers 1 less per stack (floor 0). Lasting.
 * - Rooting 根張り (歪みの根): having ended two turns in a row on the same cell, the holder loses 4 HP per stack at the next turn start. Lasting.
 * - Withering 枯らし (歪みの根): the holder recovers 1 less per stack at every turn start, never below 1 even with 疲労 (#400). Lasting.
 */
export const StatusKind = defineEnum([
  'Slow',
  'Swift',
  'Bleed',
  'Fragile',
  'Intimidate',
  'Fatigue',
  'Empower',
  'Focus',
  'Parry',
  'Regen',
  // Boss-only words (#51).
  'MiasmaShroud',
  'Binding',
  'Hook',
  'Depths',
  'Rooting',
  'Withering',
]);

/**
 * One status a face or a trait gives: the word, its stacks, and whether it lands on the one who
 * played it (自分に) or on the opponent (相手に, which reads the reach). Cap (#51) is the most the
 * holder may hold of the word through this grant (0: no cap of its own); what would go past it
 * is dropped, as at the turn-decay cap. The bosses' words are capped this way (roster §4.1:
 * 瘴気纏いは 2 スタックまで, and 1 until the stage that lets it reach 2).
 */
export class StatusGrant extends record('StatusGrant', [
  'Kind',
  ['Stacks', Constants.StatusApplyDefault],
  ['OnSelf', false],
  ['Cap', 0],
]) {}

/**
 * §5: 減り方の 2 型, and the boss-only words that last the battle (#51).
 * Lasting: 戦闘終了まで (roster §4.1 / §5.1 / §6.2): neither the turn start nor its use takes a stack off.
 */
export const StatusDecay = defineEnum(['OnUse', 'OnTurn', 'Lasting']);

/**
 * §6.1: the three branches every enemy tree has, keyed on the gap N when the omen is decided.
 * Zero: 間合い 0 (the two sides are adjacent). OneToTwo: 間合い 1〜2. ThreePlus: 間合い 3 以上.
 */
export const GapBand = defineEnum(['Zero', 'OneToTwo', 'ThreePlus']);

/**
 * §2.3 の条件: the thirteen words, plus 無防備 for enemy actions (§17.6 F1). 間合い is two rows
 * (at most / at least). The demo (#188) carries them at the precision a playable battle needs;
 * #48 keeps the tests that pin every pairing.
 *
 * - GapAtMost 間合い n 以下: the gap to the opponent, read before the card is played (§2.3).
 * - GapAtLeast 間合い n 以上.
 * - Unguarded 無防備: the opponent Guard is 0 (§17.6 F1). Not the card-side word 手薄.
 * - Reserve 温存: the stamina left after paying the cost is at or above the threshold.
 * - Combo 連動(X): something that counts as Trait.Attribute (folded, §2.1) was played earlier this turn (an enemy: this phase).
 * - Moved 移動後 (v4.4): the holder's own cell changed earlier this turn (an enemy: this phase). Replaces 連動(ムーブ).
 * - OmenIs 予兆(種別): the opponent's standing omen is Trait.Omen. Player cards only.
 * - Desperate 死力: the stamina before paying is at most DESPERATE_THRESHOLD (2).
 * - FirstPlay 初手: the first card played this turn.
 * - Finisher 締め: the third card played this turn, or later.
 * - Broken 崩し後: the opponent's stamina is below 3.
 * - Chain 連打: shares an attribute with the card played just before it this turn.
 * - Thin 手薄: the hand left after this card is played holds at most THIN_HAND (2; decided 2026-09-23, #193).
 * - FoeHas 相手の状態(語): the opponent holds Trait.Watch.
 * - SelfHas 自分の状態(語): the one playing holds Trait.Watch.
 */
export const TraitCondition = defineEnum([
  'GapAtMost',
  'GapAtLeast',
  'Unguarded',
  'Reserve',
  'Combo',
  'Moved',
  'OmenIs',
  'Desperate',
  'FirstPlay',
  'Finisher',
  'Broken',
  'Chain',
  'Thin',
  'FoeHas',
  'SelfHas',
]);

/**
 * §2.3 の効果: the ten words, plus 崩し +1 for enemy actions (roster §1.6).
 *
 * - HeavyBlow 重撃: power +6 now, recovery −1 at the next turn start. Cost and 構え are untouched.
 * - StaminaGain: スタミナ +n, at once.
 * - Draw: ドロー +n, drawn with the face's own draws.
 * - Status 状態: Trait.Grant lands with the skill face.
 * - CostDown: コスト −n (floor 0). CanPlay and Preview read it, so a card it makes payable can be played.
 * - Convert 転換: half of this card's Guard face (rounded up) is added to its power.
 * - FollowUp 追撃: the next attack face this turn (not this card's own) gets +5. Gone at turn end (§17.6 F7).
 * - BreakBonus 崩し +n (enemy actions, roster §1.6): the face's break gets n more.
 */
export const TraitEffect = defineEnum([
  'PowerBonus',
  'GuardBonus',
  'NextTurnRecovery',
  'HeavyBlow',
  'StaminaGain',
  'Draw',
  'Status',
  'CostDown',
  'Convert',
  'FollowUp',
  'BreakBonus',
]);

/**
 * §2.3: one card or action carries at most one trait (背水の陣 is the one card with two, see
 * CardDef.ExtraTrait), and a trait is one condition paired with one effect.
 * Threshold is the gap for 間合い and the stamina bar for 温存. Attribute is 連動's, Omen
 * 予兆's, Watch the word 相手の状態 / 自分の状態 look for, and Grant the status the Status
 * effect gives.
 */
export class Trait extends record('Trait', [
  'Condition',
  'Effect',
  ['Amount', 0],
  ['Threshold', 0],
  ['Attribute', BattleAttribute.None],
  ['Omen', null],
  ['Watch', null],
  ['Grant', null],
]) {}

/**
 * §4: when a stance takes effect. The six shapes of §4 cut down to the ones the 14 stance cards
 * and the enemy stances use (#188 / #189).
 *
 * - TurnStart ターン開始付与: Guard and / or recovery at the holder's turn start (§9 steps 2-3 / 9).
 * - TurnEnd: At the holder's turn end, before 構え: Guard and / or next-turn recovery.
 * - AttackBonus 条件付き加算: +Power on the holder's attack faces while the condition holds for the foe hit.
 * - OnHit 被弾時の反応: each time an attack lands on the holder.
 * - PushImmune: The holder does not take push or pull (錨の構え).
 * - BreakOnFoeMove: Each time an enemy moves itself, it loses Break stamina (根縛り).
 * - StatusOnAttack 攻撃ごとの状態付与 (#253): each blow of the holder's attack faces that lands on a foe still
 *   standing puts Status × StatusStacks on that foe, while the condition holds for it. A face
 *   of several blows gives it once a blow, and it counts from the next blow on. Status is both
 *   the word it gives and, with When = TargetHasStatus, the word it watches: that pairing only
 *   tops up a word the foe already has. No card uses it.
 * - CostDiscount コストの割引 (鉄壁の構え, v4.4 #257): 「毎ターン 1 回、<属性> のカードのコスト −1」. Each entry
 *   takes 1 off one card a turn that counts as StanceDef.Attribute (folded,
 *   §2.1), floor 0, after the card's own コスト −1 trait; two entries discount two cards a turn
 *   (§4), never the same card twice. An entry is spent only on a card it makes cheaper, and
 *   it marks StanceEntry.ReactedTurn. See TurnLoop.CostNow.
 *   The player's cards only: an enemy action pays its column (no enemy carries one).
 */
export const StanceHook = defineEnum([
  'TurnStart',
  'TurnEnd',
  'AttackBonus',
  'OnHit',
  'PushImmune',
  'BreakOnFoeMove',
  'StatusOnAttack',
  'CostDiscount',
]);

/**
 * §4 `when`: the condition a stance effect waits for.
 *
 * - Always.
 * - GapAtLeast: The gap is at least Threshold: the nearest enemy's for the player, the player's for an enemy.
 * - GapAtMost: The gap is at most Threshold (for AttackBonus: the gap to the foe hit).
 * - OmenAttack: A standing enemy's omen is an attack (見切りの目). Player only.
 * - MovedThisTurn: The holder's own cell changed earlier this turn (流れの構え; 移動後, §2.3).
 * - TargetHasStatus: The foe hit holds StanceDef.Status (狼の構え).
 */
export const StanceWhen = defineEnum([
  'Always',
  'GapAtLeast',
  'GapAtMost',
  'OmenAttack',
  'MovedThisTurn',
  'TargetHasStatus',
]);

/**
 * One entry of the holder's permanent effects (§4): the stance, the card or action that put it
 * there, and the turn an OncePerTurn stance last reacted in (or a CostDiscount stance last took
 * a cost down). Each entry works on its own.
 *
 * StoppedThrough (#334, roster §6.4 root_st) is the last battle turn the entry does nothing in,
 * 0 for an entry that works: a Face.StopStance that lands stops the holder's newest
 * entry for the rest of the turn it lands in and the whole of the next one. The entry stays on
 * the list meanwhile, and it is back at the start of the turn after (StanceResumed).
 */
export class StanceEntry extends record('StanceEntry', [
  'Def',
  'Source',
  ['ReactedTurn', 0],
  ['StoppedThrough', 0],
]) {
  /** Whether the entry does nothing in battle turn `turn` (#334). */
  StoppedIn(turn) {
    return this.StoppedThrough >= turn;
  }
}

/**
 * §4: the stance a stance face puts on the holder's list of permanent effects. The numbers are
 * the card's own (its column already decided them), and the list keeps them until the battle
 * ends. Nothing replaces a stance and there is no cap: the same stance twice is two entries and
 * works twice (v4.4, #258). Status is the word 狼の構え watches, the word 槍衾 gives the
 * attacker, or the word a StatusOnAttack stance gives the foe it hits; OncePerTurn limits an
 * OnHit stance to one reaction per turn (司祭の祈り). Attribute is the one a CostDiscount
 * stance discounts (鉄壁の構え: 防御), null for every other hook.
 */
export class StanceDef extends record('StanceDef', [
  'Hook',
  ['When', StanceWhen.Always],
  ['Threshold', 0],
  ['Guard', 0],
  ['Recovery', 0],
  ['NextRecovery', 0],
  ['Power', 0],
  ['Stamina', 0],
  ['Status', null],
  ['StatusStacks', 0],
  ['Break', 0],
  ['OncePerTurn', false],
  ['Attribute', null],
]) {}

/**
 * §2.4 / §7.2: the gaps a face lands at, both ends inclusive. A card cannot be played at an
 * opponent outside it; an enemy action whiffs (§6). The default is 0〜1 (`REACH_DEFAULT`).
 */
export class Reach extends record('Reach', ['Min', 'Max']) {
  static Only(gap) {
    return new Reach(gap, gap);
  }

  Contains(gap) {
    return gap >= this.Min && gap <= this.Max;
  }

  /** As printed on the omen badge and the card: "0〜1", or "0" when one gap only. */
  ToText() {
    return this.Min === this.Max ? String(this.Min) : `${this.Min}〜${this.Max}`;
  }
}
/** public static readonly Reach Default = new Reach(0, 1); */
Reach.Default = new Reach(0, 1);

/**
 * §2.3 / §6 間合いの境目: the gap a trait of 間合い n 以上 (AtLeast) or 間合い n 以下 reads.
 * An enemy action reads it at the gap it is carried out at; the screen thickens the aimed cells
 * that meet it (battle-visual-v1 §4.2). See EnemyAi.BoundaryOf.
 */
export class GapBoundary extends record('GapBoundary', ['AtLeast', 'Threshold']) {
  /** Whether gap `gap` meets the boundary. */
  Holds(gap) {
    return this.AtLeast ? gap >= this.Threshold : gap <= this.Threshold;
  }
}

/**
 * §2.4: what one column of a card or action does. Cards and enemy actions share this table
 * (a CardDef and an EnemyActionDef are written from the same face rows), which is why #70 and
 * #71 add data without adding types.
 *
 * Move is the movement effect in cells (an effect any card can carry, not an attribute, v4.4):
 * positive is 前へ (toward the opponent), negative is 後ろへ (§7.3). Push moves the opponent by cells: positive pushes them away, negative pulls them in;
 * Guard does not reduce it (§2.4). Reach is null for the default 0〜1; read it through
 * ReachOrDefault. Both are at most `MOVE_STEP_MAX` in size.
 *
 * #188 adds Heal (HP to the one playing), Break (崩し: the opponent's stamina −n, riding the
 * attack face), the status list (each to self or to the opponent) and the stance a stance face
 * sets. Statuses is null for none; read it through StatusList.
 *
 * Hits (§2.4 `hits`, #189) splits the attack face into that many blows, each against Guard on
 * its own; the opponent statuses of a face with two or more land after the first blow. A face
 * of two or more carries no opponent status, on the face or in a trait, on a card (#253) and on
 * an enemy action alike (v4.4, Cards.MultiHitCarriesNoFoeStatus(BattleAttribute, Face, IReadOnlyList{Trait})).
 * The resolver would still land one after the first blow where an action carried it; none does
 * since #51 put 二段斬り on v4.5's 7 × 2. A status given on every blow comes from a stance
 * (StanceHook.StatusOnAttack), not from the face.
 *
 * StopStance (#334, roster §6.4 root_st) stops the newest permanent effect of each opponent the
 * face lands on (in reach, still standing) for the rest of this turn and the whole next one
 * (StanceEntry.StoppedThrough). Only the 枯らしの息 歪みの根 takes under root_st
 * carries it; an opponent with no permanent effect loses nothing.
 */
export class Face extends record('Face', [
  ['Power', 0],
  ['Guard', 0],
  ['Move', 0],
  ['Push', 0],
  ['Draw', 0],
  ['StaminaGain', 0],
  ['Reach', null],
  ['Statuses', null],
  ['Heal', 0],
  ['Break', 0],
  ['Stance', null],
  ['Hits', 1],
  ['StopStance', false],
]) {
  get ReachOrDefault() {
    return this.Reach ?? Reach.Default;
  }

  get StatusList() {
    return this.Statuses ?? EMPTY;
  }

  /** Whether any status of this face lands on the opponent (and so reads the reach). */
  get GivesFoeStatus() {
    for (const grant of this.StatusList) {
      if (!grant.OnSelf) return true;
    }
    return false;
  }
}

/**
 * §3: a card picks one column of 1-4 up front. The column number is the cost and that column's
 * values are the effect, so there is no invest choice at play time any more.
 *
 * ExtraTrait is the second trait of 背水の陣 (#80), the one card the canon lets carry two
 * (swordsman_cards_v4 §3). Both are judged at once and their bonuses add up.
 *
 * FixedCost is set only by mastery (§3.2, #59): 伸び moves the faces (and Column) right while
 * the cost stays, and 軽さ takes the cost down to as low as 0. Null is the column's own cost.
 */
export class CardDef extends record('CardDef', [
  'Id',
  'Name',
  'Attributes',
  'Column',
  'Face',
  ['Trait', null],
  ['Targets', TargetKind.One],
  ['Description', ''],
  ['ExtraTrait', null],
  ['FixedCost', null],
]) {
  get Cost() {
    return this.FixedCost ?? Columns.CostOf(this.Column);
  }

  /** The one attribute this card counts as (§2.1): the declared faces folded by 攻撃 ＞ 防御 ＞ スキル. */
  get Attribute() {
    return AttributeRule.Fold(this.Attributes, this.Face);
  }

  /** The traits in the order they are judged: Trait, then ExtraTrait. */
  get AllTraits() {
    const traits = [];
    if (this.Trait != null) traits.push(this.Trait);
    if (this.ExtraTrait != null) traits.push(this.ExtraTrait);
    return traits;
  }
}

/** A card in a deck: definition plus a unique instance id (thrust-0). */
export class CardInstance extends record('CardInstance', ['InstanceId', 'Def']) {}

/**
 * §6: 予兆は「種別 + 狙うマス」. Reach is the action's reach, counted from the enemy's current
 * cell; null for an action with no opponent-directed face. The screen turns it into cells.
 */
export class OmenLabel extends record('OmenLabel', ['Kind', ['Reach', null]]) {}

/** The declared next enemy action (§6). Committed: it is shown, then executed as shown. */
export class Omen extends record('Omen', ['ActionId', 'Label']) {}

/**
 * One enemy action. Same Column / Face / Trait / Targets rows as CardDef. The omen label is
 * derived (EnemyAi.LabelOf): with the one-character side gone (v4.3), kind and
 * reach are both on the action itself, so there is nothing left to author separately.
 *
 * NotWhileFoeHas (#51, roster §4.3): the tree passes over this action while the player holds
 * the word at the phase it would be taken in (縛りの言葉 and 呪縛). See EnemyAi.Barred.
 *
 * NotAtFoeCap (#497, roster v4.18 §4.3): the tree passes over this action while every capped
 * word it puts on the player is already held at the cap of the action as it stands under the
 * active switch (瘴気の儀: 瘴気纏い 1, 2 from the second stage). See EnemyAi.Barred.
 *
 * PullBeyond (#51, roster §6.3 伸びる根): when set, the face's Push is a push of |Push| cells
 * while N is at most PullBeyond and a pull of |Push| cells beyond it, read when it resolves.
 * See EnemyAi.FaceAt.
 */
export class EnemyActionDef extends record('EnemyActionDef', [
  'Id',
  'Name',
  'Attributes',
  'Column',
  'Face',
  ['Trait', null],
  ['Targets', TargetKind.One],
  ['Description', ''],
  ['Omen', null],
  ['NotWhileFoeHas', null],
  ['PullBeyond', null],
  ['NotAtFoeCap', false],
]) {
  get Cost() {
    return Columns.CostOf(this.Column);
  }

  /** The one attribute this action counts as (§2.1), folded like a card's. */
  get Attribute() {
    return AttributeRule.Fold(this.Attributes, this.Face);
  }
}

/**
 * One finished player turn, as a boss's adaptation may read it (roster §1.5, §4.4): the Guard
 * the player held once 構え was judged, N to the nearest enemy, and whether their own cell
 * changed that turn (移動後, §2.3). Cell is where the player stood at the turn's end and Played
 * how many cards they played in it (#51: 根張り counts the cell, root_st the stance cards).
 * LastMove is the way the player's own last move in the turn went (+1 前へ, -1 後ろへ, 0 none):
 * root_m reads it, not the change of Cell, since a push or pull would hide the way they went.
 */
export class PlayerTurnEnd extends record('PlayerTurnEnd', [
  'Guard',
  'Gap',
  'Moved',
  ['Cell', 0],
  ['Played', 0],
  ['LastMove', 0],
]) {}

/**
 * The player's side of the battle so far, kept so a boss's adaptation can be judged by counting
 * (roster §1.5: 条件は行動履歴を数えるだけで判定でき、乱数を使いません). Cards holds the attribute
 * each played card counted as (§2.1, folded), oldest first; TurnEnds one entry per finished turn.
 * HookSnags (#51) is the turn of each 鉤爪 stack a stopped move back spent (roster §5.4 snap);
 * Inflictions the turn of each status the player put on an enemy (§6.4 root_sk).
 * MasteryPlays (#59) is the contexts each played card met (§3.2), oldest first; the ticks
 * they make are only applied once the battle has ended (MasteryBook.AfterBattle).
 */
export class BattleHistory extends record('BattleHistory', [
  ['PlayedCards', null],
  ['PlayerTurns', null],
  ['HookSnagTurns', null],
  ['InflictionTurns', null],
  ['MasteryPlayList', null],
]) {
  get MasteryPlays() {
    return this.MasteryPlayList ?? EMPTY;
  }

  WithMasteryPlay(play) {
    if (play == null) throw argumentNull('play');
    const plays = [...this.MasteryPlays, play];
    return this.with({ MasteryPlayList: plays });
  }

  get Cards() {
    return this.PlayedCards ?? EMPTY;
  }

  get TurnEnds() {
    return this.PlayerTurns ?? EMPTY;
  }

  get HookSnags() {
    return this.HookSnagTurns ?? EMPTY;
  }

  get Inflictions() {
    return this.InflictionTurns ?? EMPTY;
  }

  WithCard(counted) {
    const cards = [...this.Cards, counted];
    return this.with({ PlayedCards: cards });
  }

  WithTurnEnd(turn) {
    const turns = [...this.TurnEnds, turn];
    return this.with({ PlayerTurns: turns });
  }

  WithHookSnag(turn) {
    const snags = [...this.HookSnags, turn];
    return this.with({ HookSnagTurns: snags });
  }

  WithInfliction(turn) {
    const inflictions = [...this.Inflictions, turn];
    return this.with({ InflictionTurns: inflictions });
  }

  /** The attributes of the cards played in the last finished turn, oldest first. */
  get LastTurnCards() {
    const turnEnds = this.TurnEnds;
    const cards = this.Cards;
    if (turnEnds.length === 0) return EMPTY;
    const played = Math.min(cards.length, turnEnds[turnEnds.length - 1].Played);
    const last = [];
    for (let i = cards.length - played; i < cards.length; i++) last.push(cards[i]);
    return last;
  }
}

/**
 * What an adaptation reads when the enemy decides its omen (#50): the enemy's own HP and the
 * actions it has taken, and the player's history. Nothing here is random and nothing can be
 * changed through it. #51 adds the enemy's N to the player at that moment (roster §4.4 pusher:
 * 「間合いが 1 以上になる」で戻る) and the ids of the switches that have held so far this battle,
 * so a stage that never goes back (戻らない) can say so after 再生 lifts the HP over its line.
 */
export class AdaptationView extends record('AdaptationView', [
  'Turn',
  'Hp',
  'MaxHp',
  'EnemyActions',
  'Player',
  ['Gap', 0],
  ['HeldSwitches', null],
]) {
  get Held() {
    return this.HeldSwitches ?? EMPTY;
  }

  /** Whether the switch of this id has held at some omen decision before this one. */
  HasHeld(id) {
    for (const each of this.Held) {
      if (each === id) return true; // string.Equals(each, id, StringComparison.Ordinal)
    }
    return false;
  }
}

/**
 * The one hook that swaps a boss's decision tree (#50, §9 step 12; roster §1.5). An adaptation
 * (a count over the player's history) and a stage (the enemy's HP) are the same thing here: a
 * named condition and the branches that replace the base ones while it holds. A branch left null
 * keeps the base branch of that band. Of the switches that hold, the LAST in the enemy's list
 * wins, so a later stage overrides an earlier one. Holds is judged at every omen decision, so a
 * switch that stops holding lets the base tree back. The conditions themselves are enemy data
 * (#51); the core fixes only how they are asked.
 *
 * What the roster's adaptations and stages do besides swapping branches (#51, roster §4.4 /
 * §5.4 / §6.1 / §6.4) rides on the same switch:
 * - Overrides: actions that stand in for the enemy's own of the same id while the switch is the
 *   active one (錫杖 with 崩し 2, 鉤縄 with 鉤爪 2, 枯らしの息 reaching 0〜4 …). The omen is
 *   decided, and the action resolved, on the override.
 * - EnterMove: the enemy moves this many cells (前へ positive) the first time the switch holds in
 *   the battle, as far as the line lets it (ガルドの淵を出る 前へ 2, 根のせり上がり 前へ 1).
 * - Sway: once the switch has held, the enemy moves 前へ 1 and 後ろへ 1 in turn, one step at the
 *   start of each of its phases (歪みの根 段階 3).
 * - Cleanse: the enemy's own statuses are all taken off when the switch becomes the active one
 *   (root_sk 「自分に付いた状態を全て消す」).
 *
 * Holds is a JS function (AdaptationView) => boolean (C# Func<AdaptationView, bool>).
 */
export class TreeSwitch extends record('TreeSwitch', [
  'Id',
  'Holds',
  ['BranchAtGapZero', null],
  ['BranchAtGapOneToTwo', null],
  ['BranchAtGapThreePlus', null],
  ['Overrides', null],
  ['EnterMove', 0],
  ['Sway', false],
  ['Cleanse', false],
]) {
  Branch(band) {
    switch (band) {
      case GapBand.Zero: return this.BranchAtGapZero;
      case GapBand.OneToTwo: return this.BranchAtGapOneToTwo;
      case GapBand.ThreePlus: return this.BranchAtGapThreePlus;
      default: throw argumentOutOfRange('band', band, null);
    }
  }

  /** The action that stands in for the enemy's own `actionId` while this switch is active, or null. */
  Override(actionId) {
    if (this.Overrides == null) return null;
    for (const each of this.Overrides) {
      if (each.Id === actionId) return each; // string.Equals(..., StringComparison.Ordinal)
    }
    return null;
  }
}

/** roster §1.1 `rank`: what kind of fight the enemy is. Elites and bosses act twice a phase. */
export const EnemyRank = defineEnum(['Normal', 'Elite', 'Boss']);

/**
 * §6.1: every enemy branches three ways on the gap band. Each branch is an ordered list of
 * action ids; the first affordable one becomes the omen. Size is the cells the enemy uses
 * (§7.1, 1〜3); a size of 2 or more refuses push and pull (§7.3). ActionsPerPhase is 2 for
 * elites and bosses (roster §1.3, #189). Switches is the hook a boss's tree is swapped through
 * (#50, TreeSwitch): null for an enemy that fights on its base tree.
 *
 * Actions is a Map<string, EnemyActionDef> (C# IReadOnlyDictionary<string, EnemyActionDef>).
 */
export class EnemyDef extends record('EnemyDef', [
  'Id',
  'Name',
  'MaxHp',
  'MaxStamina',
  'Recovery',
  'Size',
  'BranchAtGapZero',
  'BranchAtGapOneToTwo',
  'BranchAtGapThreePlus',
  'Actions',
  ['Rank', EnemyRank.Normal],
  ['ActionsPerPhase', 1],
  ['Switches', null],
]) {
  Branch(band) {
    switch (band) {
      case GapBand.Zero: return this.BranchAtGapZero;
      case GapBand.OneToTwo: return this.BranchAtGapOneToTwo;
      case GapBand.ThreePlus: return this.BranchAtGapThreePlus;
      default: throw argumentOutOfRange('band', band, null);
    }
  }
}

/**
 * §1 の「戦闘中に持つ値」for one side. Cell is the leftmost cell the combatant uses (§7.1; the
 * player is on the left, the enemy on the right, so the enemy's Cell is its near edge) and
 * Size the cells it uses. NextTurnRecoveryBonus is what 温存 leaves behind for the next turn
 * start (§9 step 2).
 *
 * #188 / v4.4: Stances is the list of permanent effects (§4), no cap, each entry with the card or
 * action that put it there. Moved says the holder's own cell changed this turn (移動後, §2.3);
 * it is cleared with Played. LastMove (#51) is the way the holder's own last move this turn went
 * (+1 前へ, -1 後ろへ, 0 none), cleared with Moved; pushes and pulls do not touch it. FollowUp is the 追撃 waiting for the next attack face (§17.6 F7: gone at the end of the
 * holder's turn). Played lists the attributes of what the holder played this turn, in order and
 * folded (§2.1, §2.3 `playedAttributes`; 連動 / 初手 / 締め / 連打 read it), cleared at turn end.
 * FreeStep is the player's 俊敏 step waiting to be taken (§5, #48): set at the turn start when
 * 俊敏 is held, gone once taken, once a card is played or once the turn ends.
 */
export class CombatantState extends record('CombatantState', [
  'Hp',
  'MaxHp',
  'Stamina',
  'MaxStamina',
  'Guard',
  'Cell',
  'Size',
  'Statuses',
  ['NextTurnRecoveryBonus', 0],
  ['Stances', null],
  ['FollowUp', 0],
  ['Played', null],
  ['Moved', false],
  ['FreeStep', false],
  ['LastMove', 0],
]) {
  /** The permanent effects held, oldest first; empty for none. */
  get StanceList() {
    return this.Stances ?? EMPTY;
  }

  /** This holder with one more permanent effect (the same stance twice is two entries). */
  WithStance(stance, source) {
    const list = [...this.StanceList, new StanceEntry(stance, source)];
    return this.with({ Stances: list });
  }

  /** The rightmost cell used (equal to Cell for size 1). */
  get FarCell() {
    return Field.FrontCell(this.Cell, this.Size);
  }

  /** The attributes played this turn, in order; empty at the start of a turn. */
  get PlayedThisTurn() {
    return this.Played ?? EMPTY;
  }
}

/**
 * §7.4: one enemy on the field — what it is, where and how it stands, and the omen it has
 * declared. A fallen enemy stays in BattleState.Enemies (so the numbers events
 * carry never shift) but leaves its cells and its omen: see Alive.
 */
export class EnemyUnit extends record('EnemyUnit', [
  'Def',
  'Body',
  'Omen',
  ['SpentStances', null],
  ['Plan', null],
  ['ActiveSwitch', null],
  ['Executed', null],
  ['HeldSwitches', null],
]) {
  get Alive() {
    return !Combat.IsDefeated(this.Body.Hp);
  }

  /** #51: the ids of the switches that have held at some omen decision this battle, in the order they first did. */
  get Held() {
    return this.HeldSwitches ?? EMPTY;
  }

  /**
   * roster §1.2 (#189): the stance actions this enemy has used. Each is used once a battle and
   * then leaves its tree.
   */
  get Spent() {
    return this.SpentStances ?? EMPTY;
  }

  /** #50: the ids of the actions this enemy has taken this battle, in order, for an adaptation to count. */
  get ExecutedActions() {
    return this.Executed ?? EMPTY;
  }
}

/**
 * §9.1 (#452): the departure tools (tools_and_consumables_v4.md §2) that act inside one battle,
 * as amounts. 息継ぎの笛 is spent at the start (BattleSetup.StartStamina) and is
 * not kept here.
 *
 * ScaleGuard is 竜鱗の欠片: the Guard the player gains just before the first enemy blow or wall
 * damage that reaches them this battle; 0 once it has been given (or without the tool).
 * BossWordWard is 竜語の符: how many stacks lower the boss-only words sit on the player.
 * Warded counts, per 戦闘終了まで word (瘴気纏い / 深み / 根張り / 枯らし), the stacks the ward
 * has already kept off, so that the word sits exactly BossWordWard lower than it would.
 */
export class CarriedTools extends record('CarriedTools', [
  ['ScaleGuard', 0],
  ['BossWordWard', 0],
  ['Warded', null],
]) {
  /** The stacks the ward has kept off so far, per lasting boss word. */
  get WardedStacks() {
    return this.Warded ?? StatusSet.Empty;
  }
}
/** public static readonly CarriedTools None = new CarriedTools(); */
CarriedTools.None = new CarriedTools();

/**
 * The whole battle. TurnLoop moves it forward; this record only fixes what the
 * loop may carry. The event stream is handed back beside the state, not kept inside it.
 * FieldCells is the width of the line (§7.1), handed in per battle.
 *
 * Enemies (§7.4, #47) holds one to three, in definition order — which is also their phase
 * order and the index every event's BattleEvent.Unit means. Each stands on its
 * own cells (`CELL_CAPACITY` = 1). Enemy, EnemyDef,
 * Omen and Gap read the first one: the whole of a one-enemy battle,
 * such as the vertical slice.
 */
export class BattleState extends record('BattleState', [
  'Turn',
  'FieldCells',
  'Player',
  'Enemies',
  'Hand',
  'DrawPile',
  'DiscardPile',
  ['Result', GameResult.Ongoing],
  ['Phase', BattlePhase.AwaitingTurnStart],
  ['ExilePile', null],
  ['PlayerHistory', null],
  ['PlayerTools', null],
]) {
  /** §9.1 (#452): what the player's departure tools still hold for this battle; CarriedTools.None without them. */
  get Tools() {
    return this.PlayerTools ?? CarriedTools.None;
  }

  /** §4 (#188): the stance cards played this battle, one instance each. Those instances never go back into the deck; a copy still in the deck is another instance. */
  get Exiled() {
    return this.ExilePile ?? EMPTY;
  }

  /** The player's side of the battle so far, for a boss's adaptation (#50). */
  get History() {
    return this.PlayerHistory ?? new BattleHistory();
  }

  /** The first enemy's body. */
  get Enemy() {
    return this.Enemies[0].Body;
  }

  /** The first enemy's definition. */
  get EnemyDef() {
    return this.Enemies[0].Def;
  }

  /** The first enemy's standing omen. */
  get Omen() {
    return this.Enemies[0].Omen;
  }

  /** §7.2: the empty cells between the player and the first enemy (0 when adjacent). */
  get Gap() {
    return this.GapTo(0);
  }

  /** §7.2: N to one enemy, counted from its near edge. */
  GapTo(unit) {
    return Field.GapBetween(this.Player, this.Enemies[unit].Body);
  }

  /** The standing enemies' indices, in definition order. */
  get Living() {
    const living = [];
    for (let i = 0; i < this.Enemies.length; i++) {
      if (this.Enemies[i].Alive) living.push(i);
    }
    return living;
  }

  /**
   * §7.4: the standing enemy nearest the player (lowest cell), whose N a self-directed card
   * reads. −1 once none is standing.
   */
  get Nearest() {
    let nearest = -1;
    for (const i of this.Living) {
      if (nearest < 0 || this.Enemies[i].Body.Cell < this.Enemies[nearest].Body.Cell) nearest = i;
    }
    return nearest;
  }

  /**
   * C# overloads:
   *   WithEnemy(int unit, CombatantState body) => WithUnit(unit, Enemies[unit] with { Body = body })
   *   WithEnemy(CombatantState body) => WithEnemy(0, body)  — the first enemy's body replaced; the
   *   one-enemy shorthand tests and the slice use.
   */
  WithEnemy(unitOrBody, body) {
    if (typeof unitOrBody !== 'number') return this.WithEnemy(0, unitOrBody);
    const unit = unitOrBody;
    return this.WithUnit(unit, this.Enemies[unit].with({ Body: body }));
  }

  WithOmen(unit, omen) {
    return this.WithUnit(unit, this.Enemies[unit].with({ Omen: omen }));
  }

  WithUnit(unit, value) {
    const enemies = [...this.Enemies];
    enemies[unit] = value;
    return this.with({ Enemies: enemies });
  }
}

/**
 * C# extension methods (`gap.ToBand()`, `kind.ToToken()`, `kind.ToLabel()`) as plain functions:
 * EnumTokens.ToBand(gap), EnumTokens.ToToken(kind), EnumTokens.ToLabel(kind).
 */
export const EnumTokens = Object.freeze({
  /** §6.1: which branch a gap falls in. */
  ToBand(gap) {
    if (gap < 0) throw argumentOutOfRange('gap', gap, 'A gap is never negative.');
    if (gap === 0) return GapBand.Zero;
    return gap <= 2 ? GapBand.OneToTwo : GapBand.ThreePlus;
  },

  ToToken(kind) {
    switch (kind) {
      case StatusKind.Slow: return 'slow';
      case StatusKind.Swift: return 'swift';
      case StatusKind.Bleed: return 'bleed';
      case StatusKind.Fragile: return 'fragile';
      case StatusKind.Intimidate: return 'intimidate';
      case StatusKind.Fatigue: return 'fatigue';
      case StatusKind.Empower: return 'empower';
      case StatusKind.Focus: return 'focus';
      case StatusKind.Parry: return 'parry';
      case StatusKind.Regen: return 'regen';
      // Boss-only words (#51).
      case StatusKind.MiasmaShroud: return 'miasma_shroud';
      case StatusKind.Binding: return 'binding';
      case StatusKind.Hook: return 'hook';
      case StatusKind.Depths: return 'depths';
      case StatusKind.Rooting: return 'rooting';
      case StatusKind.Withering: return 'withering';
      default: throw argumentOutOfRange('kind', kind, null);
    }
  },

  ToLabel(kind) {
    switch (kind) {
      case StatusKind.Slow: return '鈍足';
      case StatusKind.Swift: return '俊敏';
      case StatusKind.Bleed: return '出血';
      case StatusKind.Fragile: return '脆化';
      case StatusKind.Intimidate: return '威圧';
      case StatusKind.Fatigue: return '疲労';
      case StatusKind.Empower: return '強化';
      case StatusKind.Focus: return '集中';
      case StatusKind.Parry: return '見切り';
      case StatusKind.Regen: return '再生';
      // Boss-only words (#51).
      case StatusKind.MiasmaShroud: return '瘴気纏い';
      case StatusKind.Binding: return '呪縛';
      case StatusKind.Hook: return '鉤爪';
      case StatusKind.Depths: return '深み';
      case StatusKind.Rooting: return '根張り';
      case StatusKind.Withering: return '枯らし';
      default: throw argumentOutOfRange('kind', kind, null);
    }
  },
});
