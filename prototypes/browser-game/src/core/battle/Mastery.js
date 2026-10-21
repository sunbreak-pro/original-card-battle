// Port of unity-port/BattleCore/Mastery.cs
//
// battle_core_v4 §3.2 / §17.8 習熟 (#59). The ledger here (MasteryTally, CardMastery, RankUp,
// Mastery.Master, RankFor, CountOf, and the mastery constants in Constants.cs) is read by the tests
// alone for now: TurnLoop writes BattleHistory.MasteryPlays and nothing on the screen reads it yet.
// It is kept on purpose, not dead code (#518, 2026-10-17): the mastery follow-ups #449 and #460 are
// to wire it to the screen.
//
// ---- JS port notes ----
// - Records take C# named arguments the way Types.js's do: a trailing plain object whose keys are
//   all parameter names (`new MasteryProfile('block', MasteryKind.Lighten, { GapContext: new Reach(2, 9) })`).
//   Omitted parameters take their C# default; `with({...})` is the C# `with` expression.
// - Dictionaries are Maps (MasteryTally.Ticks, MasteryBook.Records), in C# enumeration order.
// - C# overloads are dispatched on the argument: MasteryProfiles.Find(cardId) / Find(profiles, cardId)
//   by the argument count; MasteryTally.Of(BattleState) / Of(IEnumerable<MasteryPlay>) by whether the
//   argument is iterable; MasteryBook.AfterBattle(BattleState) / AfterBattle(MasteryTally) by
//   `instanceof MasteryTally`.
// - MasteryProfiles.Observe / All are built together on first read (as the C# type initializer
//   builds them before first use), so nothing from CardCatalog / TurnLoop is read at
//   module-evaluation time and the TurnLoop ↔ Mastery import cycle is safe.

import {
  defineRecord,
  defineFlags,
  defineEnum,
  stableSortBy,
  ArgumentNullException,
  ArgumentOutOfRangeException,
  InvalidOperationException,
} from '../../shared/cs.js';
import {
  BattleAttribute,
  TraitCondition,
  TraitEffect,
  OmenKind,
  GameResult,
  Trait,
  CardInstance,
} from './Types.js';
import { Constants } from './Constants.js';
import { Columns } from './Columns.js';
import { AttributeRule } from './Attributes.js';
import { Traits, TraitContext } from './Traits.js';
import { EnemyAi } from './EnemyAi.js';
import { CardCatalog } from './CardCatalog.js';
import { FocusStep } from './TurnLoop.js';

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
  return new ArgumentOutOfRangeException(`${message} (Parameter '${param}')\nActual value was ${value}.`);
}

/** defineRecord + C# named arguments (same convention as Types.js / Traits.js). */
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
          if (named[k] !== undefined) this[k] = named[k];
        }
      }
    }
  };
  return C;
}

/** C# Dictionary<string, int>.TryGetValue(key, out v) with v = default(int) when missing; null key throws. */
function ticksIn(map, key) {
  if (key == null) throw argumentNull('key');
  return map.has(key) ? map.get(key) : 0;
}

// ------------------------------------------------------------------ MasteryContext / MasteryKind

/**
 * battle_core_v4 §3.2 / §17.8 習熟 (#59): the four contexts a play can meet. One play gives one
 * tick for each context it meets, so 0〜4. [Flags]: numbers, combined with bit operations.
 *
 * - Gap 間合い: the N before the card fits the card's own gap (Mastery.GapHolds).
 * - Omen 予兆に合った型: the card counts as the attribute that answers the omen it reads (Mastery.Answer).
 * - Trait 特性の発火: a trait of the card held.
 * - Zone 構え圏か死力圏: 構え still stands after paying (3 or more left), or the card was paid out of 死力 (2 or less before).
 */
export const MasteryContext = defineFlags({
  None: 0,
  Gap: 1,
  Omen: 2,
  Trait: 4,
  Zone: 8,
});

/**
 * §3.2: the three ways a card grows a step. Each card that renames on mastery holds one.
 *
 * - Extend 伸び: the faces move one column right per step; the cost stays.
 * - Lighten 軽さ: the cost goes 1 down per step, floor 0.
 * - Reform 型替え: the trait is swapped per step; the numbers stay (§19.3 S6).
 */
export const MasteryKind = defineEnum(['Extend', 'Lighten', 'Reform']);

// ------------------------------------------------------------------ MasteryPlay

/** One play, as mastery reads it: which card kind, and the contexts it met. */
export class MasteryPlay extends record('MasteryPlay', ['CardId', 'Contexts']) {
  get Ticks() {
    return Mastery.CountOf(this.Contexts);
  }
}

// ------------------------------------------------------------------ MasteryProfile

/**
 * How one card kind grows (§3.2). Kind decides the thresholds (型替え 2 / 6 / 12, the others
 * 3 / 8 / 15). RankNames and RankTraits are for steps 1-3 in order: the name the card takes
 * at each step, and for 型替え the trait it takes. GapContext, when set, is the N range in which
 * a play of this card meets the 間合い context, in place of the default (Mastery.GapHolds).
 *
 * Which card gets which kind is the card table's to decide (swordsman_cards_v4, cards lane).
 * Only the cards MasteryProfiles lists grow.
 */
export class MasteryProfile extends record('MasteryProfile', [
  'CardId',
  'Kind',
  ['RankNames', null],
  ['RankTraits', null],
  ['GapContext', null],
]) {
  get Thresholds() {
    return this.Kind === MasteryKind.Reform ? Constants.MasteryThresholdsReform : Constants.MasteryThresholds;
  }

  /** The name at `rank`, or null when the card keeps the one it has. */
  NameAt(rank) {
    return rank >= 1 && this.RankNames != null && rank <= this.RankNames.length ? this.RankNames[rank - 1] : null;
  }

  /** The trait a 型替え card takes at `rank`, or null when it keeps the one it has. */
  TraitAt(rank) {
    return rank >= 1 && this.RankTraits != null && rank <= this.RankTraits.length ? this.RankTraits[rank - 1] : null;
  }
}

// ------------------------------------------------------------------ MasteryProfiles

function Extend(card, one, two, three) {
  return new MasteryProfile(card.Id, MasteryKind.Extend, { RankNames: [one, two, three] });
}

function Lighten(card, one, two, three) {
  return new MasteryProfile(card.Id, MasteryKind.Lighten, { RankNames: [one, two, three] });
}

function Reform(card, one, two, three, first, second, third) {
  return new MasteryProfile(card.Id, MasteryKind.Reform, { RankNames: [one, two, three], RankTraits: [first, second, third] });
}

/** The reading path (S7), for the five cards that start at 相手の状態: 崩し後 → 予兆(攻撃) → 初手. */
function ReadingPath(card, one, two, three) {
  return Reform(card, one, two, three,
    Loosened(card, TraitCondition.Broken),
    Loosened(card, TraitCondition.OmenIs, { omen: OmenKind.Attack }),
    Loosened(card, TraitCondition.FirstPlay));
}

/** The resource path (S7), for the cards that start at 死力, 温存(残 ≥ 6) or 自分の状態: 温存(残 ≥ 4) → 温存(残 ≥ 2) → 初手. */
function ResourcePath(card, one, two, three) {
  return Reform(card, one, two, three,
    Loosened(card, TraitCondition.Reserve, { threshold: 4 }),
    Loosened(card, TraitCondition.Reserve, { threshold: 2 }),
    Loosened(card, TraitCondition.FirstPlay));
}

/** The count path (S7), for the three cards that start at 締め: 連打 → 連動(A) → 初手. */
function CountPath(card, one, two, three) {
  return Reform(card, one, two, three,
    Loosened(card, TraitCondition.Chain),
    Loosened(card, TraitCondition.Combo, { attribute: BattleAttribute.Attack }),
    Loosened(card, TraitCondition.FirstPlay));
}

/**
 * §19.3 S6: the card's own trait with only the condition swapped. The effect, its amount and
 * its grant stay; the parameter is the new condition's alone.
 * C# optional parameters (threshold = 0, attribute = None, omen = null) are the options object.
 */
function Loosened(card, condition, { threshold = 0, attribute = BattleAttribute.None, omen = null } = {}) {
  const trait = card.Trait;
  if (trait == null) throw new InvalidOperationException(`${card.Id} has no trait to loosen.`);
  return trait.with({ Condition: condition, Threshold: threshold, Attribute: attribute, Omen: omen, Watch: null });
}

/** Observe and All, built once on first read (C# static readonly fields, in declaration order). */
let profiles = null;

function Profiles() {
  if (profiles !== null) return profiles;

  const Observe = new MasteryProfile(
    'observe',
    MasteryKind.Reform,
    {
      RankNames: ['精査', '洞察', '看破'],
      RankTraits: [
        new Trait(TraitCondition.Finisher, TraitEffect.Draw, 1),
        new Trait(TraitCondition.Combo, TraitEffect.Draw, 1, { Attribute: BattleAttribute.Skill }),
        new Trait(TraitCondition.FirstPlay, TraitEffect.Draw, 1),
      ],
    });

  const C = CardCatalog;
  /** The sixty profiles, in canon order (#1 first, #76 last). */
  const All = Object.freeze([
    // §2: the initial forty.
    Extend(C.Thrust, '鋭い突き', '穿ち突き', '一穿'),
    Extend(C.KesaCut, '深袈裟', '重ね袈裟', '断ち袈裟'),
    Extend(C.Overhead, '天上段', '雷落とし', '天墜'),
    Extend(C.WristCut, '鋭い小手', '小手砕き', '腕断ち'),
    Extend(C.SideSweep, '大横薙ぎ', '渦薙ぎ', '断界'),
    ReadingPath(C.FlatStrike, '峰返し', '峰崩し', '峰砕き'),
    Extend(C.ProbeThrust, '読み突き', '見抜き突き', '先の突き'),
    Extend(C.Pierce, '深貫き', '透し貫き', '穿通'),
    Extend(C.ThrowBlade, '飛刃', '双飛刃', '流星刃'),
    Extend(C.ReachThrust, '長伸び突き', '遠当て突き', '千里突き'),
    ResourcePath(C.Brace, '調息', '静息', '息一つ'),
    Extend(C.IronBlock, '鋼の受け', '鉄城の受け', '不壊の受け'),
    Extend(C.LowGuard, '沈みの構え', '地の構え', '根張りの構え'),
    Extend(C.RiposteGuard, '返し受け', '巻き受け', '鉄心受け'),
    Extend(C.DeepBreath, '長い呼吸', '静かな呼吸', '不動の呼吸'),
    Lighten(C.WaterStance, '流水の構え', '静水の構え', '止水の構え'),
    Lighten(C.RockStance, '磐の構え', '巌の構え', '不動岩の構え'),
    Lighten(C.FlowStance, '渦の構え', '奔流の構え', '激流の構え'),
    Lighten(C.FirstAid, '早手当て', '熟し手当て', '一息手当て'),
    ReadingPath(C.SpiritRoar, '気当て', '威気', '覇気'),
    Lighten(C.Focus, '一念', '専心', '無心'),
    Observe,
    Reform(C.WarCry, '鬨の叫び', '鬨の轟き', '天地の鬨',
      Loosened(C.WarCry, TraitCondition.Combo, { attribute: BattleAttribute.Skill }),
      Loosened(C.WarCry, TraitCondition.Combo, { attribute: BattleAttribute.Attack }),
      Loosened(C.WarCry, TraitCondition.FirstPlay)),
    ResourcePath(C.SecondWind, '深い息継ぎ', '長い息継ぎ', '尽きぬ息'),
    Extend(C.Footwork, '軽い足運び', '疾き足運び', '無音の足'),
    Extend(C.BackLeap, '高い後ろ跳び', '翻り跳び', '風跳び'),
    Extend(C.SlideStep, '忍び足', '滑り足', '影摺り'),
    CountPath(C.BreakOff, '間外し', '間断ち', '間読み'),
    Lighten(C.Lunge, '速踏み斬り', '迅踏み斬り', '瞬踏み斬り'),
    Extend(C.Feint, '鋭い牽制', '誘いの牽制', '封じの牽制'),
    Extend(C.BoarRush, '猛進斬り', '破陣突進', '突貫'),
    ReadingPath(C.Rend, '深裂き', '重ね裂き', '千裂き'),
    Extend(C.BodyCheck, '重い体当たり', '肩砕き', '山当たり'),
    Extend(C.StoneThrow, '礫打ち', '連礫', '礫の雨'),
    Lighten(C.ParryCut, '流し斬り', '柳受け斬り', '柳風'),
    Lighten(C.GuardThrust, '速い柄当て', '柄打ち', '柄砕き'),
    Lighten(C.StepInGuard, '進み受け', '速進み受け', '流れ進み'),
    Lighten(C.StepOutGuard, '退き受け', '速退き受け', '流れ退き'),
    ResourcePath(C.IronWall, '鉄城の構え', '金城の構え', '不落の構え'),
    Lighten(C.TwistAway, '速翻し', '翻しの舞', '翻天'),

    // §3: the twenty learned cards that rename.
    Extend(C.FangRush, '牙の強襲', '裂牙の突進', '鋭牙の突進'),
    CountPath(C.ShieldPush, '盾突き', '盾崩し', '城盾'),
    Extend(C.SnapGuard, '瞬の受け', '反射の受け', '無意の受け'),
    Extend(C.LeapBack, '鋭い跳び退り', '翻り退り', '風退り'),
    Lighten(C.RisingCut, '昇り斬り', '逆巻き斬り', '天昇'),
    ReadingPath(C.LegSweep, '足刈り', '足崩し', '足封じ'),
    Extend(C.DashIn, '疾駆', '迅駆け', '飛駆け'),
    Reform(C.GuardWalk, '受け進み', '盾歩み', '鉄歩',
      Loosened(C.GuardWalk, TraitCondition.Combo, { attribute: BattleAttribute.Guard }),
      Loosened(C.GuardWalk, TraitCondition.Combo, { attribute: BattleAttribute.Attack }),
      Loosened(C.GuardWalk, TraitCondition.FirstPlay)),
    Extend(C.ThornCut, '棘裂き', '茨斬り', '千棘'),
    ResourcePath(C.CalmGuard, '静心の受け', '凪の受け', '空の受け'),
    Extend(C.Deflect, '打ち弾き', '鉄弾き', '剛弾き'),
    CountPath(C.CrescentCut, '半月斬り', '満月斬り', '月輪斬り'),
    Extend(C.PommelStrike, '柄頭砕き', '重い柄頭', '鉄の柄頭'),
    Lighten(C.MistStep, '朧足', '霧足', '幻足'),
    Lighten(C.KeenEye, '読みの目', '慧眼', '千里眼'),
    ResourcePath(C.Resolve, '決死', '不屈', '泰然'),
    Extend(C.GaleThrust, '迅風突き', '烈風突き', '天風突き'),
    ReadingPath(C.AnchorStance, '重錨の構え', '大錨の構え', '不動錨の構え'),
    Extend(C.VitalThrust, '急所穿ち', '一点穿ち', '必中突き'),
    Extend(C.EvadeCut, '躱し返し', '紙一重斬り', '残影斬り'),
  ]);

  profiles = { Observe, All };
  return profiles;
}

/**
 * The sixty cards that rename on mastery (#504), from swordsman_cards_v4 §4.6 (v4.7, #449):
 * each card's kind and its names at steps 1-3, in canon order. A 型替え card keeps its own
 * trait's effect and takes the table's condition at each step (Loosened), along the three paths
 * of battle_core_v4 §19.3 S7 as #504 reads them: the reading path ends at 初手 too, a narrower
 * bracket counts as a step (温存 残 ≥ 6 → 4 → 2, 連動 widened to 連動(A)), and 自分の状態 starts
 * the resource path where 死力 does. 観察 (#22) is the card §19.3 spells out itself. The other
 * twenty cards (stances and the cards that keep their name) have no profile.
 */
export const MasteryProfiles = Object.freeze({
  get Observe() {
    return Profiles().Observe;
  },

  /** The sixty profiles, in canon order (#1 first, #76 last). */
  get All() {
    return Profiles().All;
  },

  /**
   * C# overloads: Find(string cardId) => Find(All, cardId); Find(IReadOnlyList<MasteryProfile> profiles, string cardId).
   */
  Find(...args) {
    if (args.length < 2) return MasteryProfiles.Find(MasteryProfiles.All, args[0]);
    const [profileList, cardId] = args;
    if (profileList == null) throw argumentNull('profiles');
    if (cardId == null) throw argumentNull('cardId');
    for (const profile of profileList) {
      if (profile.CardId === cardId) return profile; // string.Equals(..., StringComparison.Ordinal)
    }
    return null;
  },
});

// ------------------------------------------------------------------ Mastery

/** The four contexts in the order CountOf reads them. */
const CONTEXTS = Object.freeze([MasteryContext.Gap, MasteryContext.Omen, MasteryContext.Trait, MasteryContext.Zone]);

/**
 * §3.2 (#504): what 伸び adds, by step, to the small Guard of a movement-centred card
 * (AttributeRule.IsMovementCard): 4 → 6 → 9 → 9. Step 2 is one column of the single Guard
 * (列 1 → 2, +5) and step 1 is a value on the way to it (+2).
 */
const MovementGuardByRank = Object.freeze([0, 2, 5, 5]);

/**
 * §3.2 (#504): what 伸び adds, by step, to the ドロー or the スタミナ of a movement-centred
 * card: +1 at step 2. One draw is one column of a skill face (§3.1, 列 3 → 4), and a draw
 * does not split, so it comes whole at the step where the Guard reaches its column.
 */
const MovementExtraByRank = Object.freeze([0, 0, 1, 1]);

/**
 * 伸び on a movement-centred card (§3.2, #504). 集中 does not grow such a card's small Guard
 * and a draw or a stamina point has no column scale (§5), so the face does not move a column:
 * only the extra beside the move grows, by MovementGuardByRank and MovementExtraByRank. The move,
 * the column, the cost and the trait stay.
 */
function GrowCompanion(face, rank) {
  return face.with({
    Guard: face.Guard > 0 ? face.Guard + MovementGuardByRank[rank] : face.Guard,
    Draw: face.Draw > 0 ? face.Draw + MovementExtraByRank[rank] : face.Draw,
    StaminaGain: face.StaminaGain > 0 ? face.StaminaGain + MovementExtraByRank[rank] : face.StaminaGain,
  });
}

/** 伸び one step: the faces read one column right, as 集中 would. Column 4 does not grow. */
function ExtendOnce(def) {
  if (def.Column >= Columns.Max) return def;
  const step = FocusStep.Of(def.Attributes, def.Column, def.Face);
  let face = def.Face;
  let statuses = null;
  if (face.Statuses != null) {
    statuses = [];
    for (const grant of face.Statuses) statuses.push(grant.with({ Stacks: step.StacksOf(grant) }));
  }
  face = face.with({
    Power: face.Power + step.Power,
    Guard: face.Guard + step.Guard,
    Heal: face.Heal + step.Heal,
    Statuses: statuses ?? face.Statuses,
  });
  return def.with({ Column: def.Column + 1, Face: face });
}

/**
 * battle_core_v4 §3.2 / §17.8 / §19.3 (#59): the rules of mastery that do not need a life's
 * record — which contexts a play meets, which step a tick count reaches, and what a card looks
 * like at a step. MasteryBook keeps the record.
 */
export const Mastery = Object.freeze({
  /** §17.8: the step only Constants.MasteryTopRankCards card of a life may hold. */
  TopRank: 3,

  CountOf(contexts) {
    let count = 0;
    for (const one of CONTEXTS) {
      if ((contexts & one) !== 0) count++;
    }
    return count;
  },

  /**
   * The contexts one play meets, on the board the traits are judged on (`context`: before the
   * card leaves the hand). `traitFired` is whether any of its traits held; `paid` the stamina it
   * costs once its コスト −1 and a コストの割引 stance have come off.
   */
  ContextsOf(def, context, traitFired, paid) {
    if (def == null) throw argumentNull('def');
    if (context == null) throw argumentNull('context');

    let met = MasteryContext.None;
    if (Mastery.GapHolds(def, context.Gap, MasteryProfiles.Find(def.Id))) met |= MasteryContext.Gap;
    if (context.OpponentOmen != null && Mastery.Answer(context.OpponentOmen) === def.Attribute) met |= MasteryContext.Omen;
    if (traitFired) met |= MasteryContext.Trait;
    // C# int arithmetic: StaminaBefore defaults to int.MaxValue; paid is never negative, so no overflow.
    const stance = context.StaminaBefore - paid >= Constants.ReserveThreshold;
    const desperate = context.StaminaBefore <= Constants.DesperateThreshold;
    if (stance || desperate) met |= MasteryContext.Zone;
    return met;
  },

  /**
   * The 間合い context (§3.2, battle lane's placeholder): the profile's own range when it has
   * one; else the side a 間合い trait of the card reads (間合い n 以下 / n 以上); else, for a
   * card aimed at the opponent whose reach spans 2 N or more, the far end of that reach (a play
   * right at the edge of what it reaches). Other cards never meet it until the card table
   * gives them a range.
   */
  GapHolds(def, gap, profile = null) {
    if (def == null) throw argumentNull('def');
    if (profile != null && profile.GapContext != null) return profile.GapContext.Contains(gap);
    // Only the first 間合い trait is read. A card with two (say a main GapAtMost and an
    // ExtraTrait GapAtLeast) would need both sides; no card in the table has that today.
    for (const trait of def.AllTraits) {
      if (trait.Condition === TraitCondition.GapAtMost || trait.Condition === TraitCondition.GapAtLeast) {
        return Traits.Holds(trait, new TraitContext({ Gap: gap }));
      }
    }
    if (EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets)) {
      const reach = def.Face.ReachOrDefault;
      return reach.Max > reach.Min && gap === reach.Max;
    }
    return false;
  },

  /**
   * 予兆に合った型 (§3.2, battle lane's placeholder): the attribute that answers an omen. An
   * attack is met with 防御, a guard with スキル (a word or 崩し rather than a blow into the
   * Guard), and a move, a skill, a stance or a rest with 攻撃 (the opening it leaves).
   */
  Answer(omen) {
    switch (omen) {
      case OmenKind.Attack: return BattleAttribute.Guard;
      case OmenKind.Guard: return BattleAttribute.Skill;
      default: return BattleAttribute.Attack;
    }
  },

  /** The step `ticks` reaches on `thresholds`, before the one-card limit on step 3. */
  RankFor(ticks, thresholds) {
    if (thresholds == null) throw argumentNull('thresholds');
    let rank = 0;
    for (const threshold of thresholds) {
      if (ticks >= threshold) rank++;
    }
    return rank;
  },

  /**
   * The card at `rank` (§3.2). 伸び moves the faces one column right per step the way 集中 reads
   * the column to the right (FocusStep: the step on power, Guard, heal and 出血 / 再生 read off the
   * Columns scales; the attack rows are the v4.4 steps §5 keeps, #364, the others §3.1's), never
   * beyond column 4, and keeps the cost. On a movement-centred card it grows the extra beside the
   * move instead (GrowCompanion). 軽さ takes the cost 1 down per step, floor 0. 型替え takes the
   * profile's trait for the step. The name is the profile's for the step when it has one. The id
   * stays, so the record still finds it.
   */
  Master(def, rank, profile) {
    if (def == null) throw argumentNull('def');
    if (profile == null) throw argumentNull('profile');
    if (rank < 0 || rank > Mastery.TopRank) throw argumentOutOfRange('rank', rank, `A step is 0..${Mastery.TopRank}.`);
    if (rank === 0) return def;

    const cost = def.Cost;
    let mastered = def;
    switch (profile.Kind) {
      case MasteryKind.Extend:
        if (AttributeRule.IsMovementCard(def.Attributes, def.Face)) {
          mastered = def.with({ Face: GrowCompanion(def.Face, rank) });
        } else {
          for (let i = 0; i < rank; i++) mastered = ExtendOnce(mastered);
        }
        mastered = mastered.with({ FixedCost: cost });
        break;
      case MasteryKind.Lighten:
        mastered = def.with({ FixedCost: Math.max(0, cost - rank) });
        break;
      case MasteryKind.Reform: {
        const trait = profile.TraitAt(rank);
        if (trait != null) mastered = def.with({ Trait: trait });
        break;
      }
      default:
        throw argumentOutOfRange('profile', profile.Kind, 'Unknown mastery kind.');
    }

    const name = profile.NameAt(rank);
    return name != null ? mastered.with({ Name: name }) : mastered;
  },

  /** §3.2 (#504): 伸び's Guard growth on a movement-centred card, by step: 4 → 6 → 9 → 9. */
  MovementGuardByRank,

  /** §3.2 (#504): 伸び's ドロー / スタミナ growth on a movement-centred card, by step: +1 at step 2. */
  MovementExtraByRank,
});

// ------------------------------------------------------------------ MasteryTally

/** C# `IEnumerable<MasteryPlay>`: anything iterable (arrays, Sets, generators). */
function isIterable(x) {
  return x != null && typeof x[Symbol.iterator] === 'function' && typeof x !== 'string';
}

/**
 * The ticks one battle gave, by card kind, before 才能 doubles any of them. Read off the
 * finished battle's history (BattleHistory.MasteryPlays). Ticks is a Map<string, int>.
 */
export class MasteryTally extends record('MasteryTally', ['Ticks']) {
  /** C# overloads: Of(BattleState state) / Of(IEnumerable<MasteryPlay> plays). */
  static Of(source) {
    if (isIterable(source)) {
      const plays = source;
      const ticks = new Map();
      for (const play of plays) {
        const had = ticks.has(play.CardId) ? ticks.get(play.CardId) : 0;
        ticks.set(play.CardId, had + play.Ticks);
      }
      return new MasteryTally(ticks);
    }
    const state = source;
    if (state == null) throw argumentNull('state');
    return MasteryTally.Of(state.History.MasteryPlays);
  }

  TicksOf(cardId) {
    return ticksIn(this.Ticks, cardId);
  }
}

/** One card kind's place in a life's mastery: the ticks it has and the step they have bought. */
export class CardMastery extends record('CardMastery', ['CardId', 'Ticks', 'Rank']) {}

/** A card that went up a step, for the result screen. */
export class RankUp extends record('RankUp', ['CardId', 'From', 'To']) {}

/** What a battle's end or a training left: the book after it and the steps that changed. */
export class MasteryResult extends record('MasteryResult', ['Book', 'RankUps']) {}

// ------------------------------------------------------------------ MasteryBook

/**
 * One life's mastery (§3.2 / §17.8, #59). Ticks are kept by card kind: copies of one card share
 * them. Talent is the card picked at the start (才能), whose battle ticks count twice. Steps go
 * up only in AfterBattle(BattleState) and Train, never during a battle, so a card in play keeps
 * the face it started the battle with. Only one card of the life may hold step 3; the others
 * stop at 2. A card with no profile gathers ticks but does not step up until its profile exists.
 *
 * Leaving a card behind at death and taking it up in the next life are the dungeon side's (the
 * heritage Issue); a new life starts from Begin.
 *
 * Records is a Map<string, CardMastery> (C# IReadOnlyDictionary) or null.
 */
export class MasteryBook extends record('MasteryBook', [
  ['Talent', null],
  ['Records', null],
  ['Profiles', null],
]) {
  /** A new life: the talent card (or none) and the profiles to grow by (the canon's by default). */
  static Begin(talent, profiles = null) {
    return new MasteryBook(talent ?? null, null, profiles ?? null);
  }

  get ProfileList() {
    return this.Profiles ?? MasteryProfiles.All;
  }

  ProfileOf(cardId) {
    return MasteryProfiles.Find(this.ProfileList, cardId);
  }

  TicksOf(cardId) {
    return this.Find(cardId)?.Ticks ?? 0;
  }

  RankOf(cardId) {
    return this.Find(cardId)?.Rank ?? 0;
  }

  /** The card holding step 3, or null while the life has none. */
  get TopRankHolder() {
    if (this.Records == null) return null;
    for (const rec of this.Records.values()) {
      if (rec.Rank >= Mastery.TopRank) return rec.CardId;
    }
    return null;
  }

  /**
   * C# overloads:
   * - AfterBattle(BattleState finished): the battle's end (§17.8: the step goes up when the battle is
   *   over, never during it). The ticks of every play are added — the talent card's twice — and then
   *   the steps are judged. Refused while the battle is still going.
   * - AfterBattle(MasteryTally tally): the same, from a tally already taken.
   */
  AfterBattle(source) {
    if (source instanceof MasteryTally) {
      const tally = source;
      const ticks = this.TickTable();
      for (const [key, value] of tally.Ticks) {
        if (value <= 0) continue;
        const gained = key === this.Talent // string.Equals(pair.Key, Talent, StringComparison.Ordinal)
          ? value * Constants.TalentTickMultiplier
          : value;
        const had = ticks.has(key) ? ticks.get(key) : 0;
        ticks.set(key, had + gained);
      }
      return this.Rerank(ticks);
    }
    const finished = source;
    if (finished == null) throw argumentNull('finished');
    if (finished.Result === GameResult.Ongoing) {
      throw new InvalidOperationException('Mastery steps go up only once the battle is over (§17.8).');
    }
    return this.AfterBattle(MasteryTally.Of(finished));
  }

  /**
   * 習熟訓練 (§17.8, one 刻限): the named card gets Constants.TrainingTicks. It is out of battle,
   * so its step is judged at once. 才能 does not double it: the training gives a fixed 1.
   */
  Train(cardId) {
    if (cardId == null) throw argumentNull('cardId');
    const ticks = this.TickTable();
    const had = ticks.has(cardId) ? ticks.get(cardId) : 0;
    ticks.set(cardId, had + Constants.TrainingTicks);
    return this.Rerank(ticks);
  }

  /** The card as this life has grown it. A card with no profile or at step 0 is returned as it is. */
  Mastered(def) {
    if (def == null) throw argumentNull('def');
    const profile = this.ProfileOf(def.Id);
    const rank = this.RankOf(def.Id);
    return profile == null || rank === 0 ? def : Mastery.Master(def, rank, profile);
  }

  /** The deck the next battle is fought with: every instance keeps its id and takes its grown card. */
  MasteredDeck(deck) {
    if (deck == null) throw argumentNull('deck');
    return deck.map((card) => new CardInstance(card.InstanceId, this.Mastered(card.Def)));
  }

  /** (C# private) The record of `cardId`, or null. */
  Find(cardId) {
    if (cardId == null) throw argumentNull('cardId');
    return this.Records != null && this.Records.has(cardId) ? this.Records.get(cardId) : null;
  }

  /** (C# private) The ticks of every record, as a fresh Map (Records' enumeration order). */
  TickTable() {
    const ticks = new Map();
    if (this.Records != null) {
      for (const rec of this.Records.values()) ticks.set(rec.CardId, rec.Ticks);
    }
    return ticks;
  }

  /**
   * (C# private) The steps the new ticks buy. Steps never go down. Step 3 goes to one card only:
   * the one holding it keeps it, and when several reach it at the same moment the one with the most
   * ticks takes it (then the talent card, then the id in ordinal order). The rest stop at 2.
   * This assumes Constants.MasteryTopRankCards is 1 (a single TopRankHolder); raising that value
   * needs this method to keep a set of holders instead.
   */
  Rerank(ticks) {
    const holder = this.TopRankHolder;
    const records = new Map();
    const ups = [];
    const reachingTop = [];

    for (const [key, value] of stableSortBy([...ticks.entries()], (p) => p[0])) {
      const was = this.RankOf(key);
      const profile = this.ProfileOf(key);
      const target = profile == null ? 0 : Mastery.RankFor(value, profile.Thresholds);
      let rank = Math.max(was, target);
      if (rank >= Mastery.TopRank && key !== holder) {
        reachingTop.push(key);
        rank = Mastery.TopRank - 1;
      }
      records.set(key, new CardMastery(key, value, rank));
    }

    if (holder == null && reachingTop.length > 0) {
      const chosen = stableSortBy(reachingTop,
        { f: (id) => ticks.get(id), desc: true },
        (id) => (id === this.Talent ? 0 : 1),
        (id) => id)[0];
      records.set(chosen, records.get(chosen).with({ Rank: Mastery.TopRank }));
    }

    for (const rec of stableSortBy([...records.values()], (r) => r.CardId)) {
      const was = this.RankOf(rec.CardId);
      if (rec.Rank > was) ups.push(new RankUp(rec.CardId, was, rec.Rank));
    }

    return new MasteryResult(this.with({ Records: records }), ups);
  }
}
