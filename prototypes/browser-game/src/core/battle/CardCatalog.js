// Port of unity-port/BattleCore/CardCatalog.cs
//
// C# `CardCatalog` is one `static partial class` over three files. Here:
//   - CardCatalog.Initial.js exports the part CardCatalog.Initial.cs declares (#1-#40) as `CardCatalog`,
//   - CardCatalog.Learned.js exports the part CardCatalog.Learned.cs declares (#41-#80) as `CardCatalog`,
//   - this module exports the merged `CardCatalog` every other module imports: the eighty cards under
//     their C# names (Thrust, KesaCut, ...), All (canon order), ById, plus PrototypeDeck.
//
// The trait / status shorthands (AtMost, When, Foe, ...) are `private static` members of the C#
// partial class, used by the two part files. They are exported from here as hoisted function
// declarations so the parts can call them while this module is still loading (the parts import
// them back: a cycle). They are not part of CardCatalog's public surface.
//
// Import-cycle note: this module never reads the two parts at module-evaluation time. The card
// members, All and PrototypeDeck.Kinds are lazy getters (All and Kinds are built once, on first
// read), which is also the C# rule: All is assigned in the static constructor, after every field
// initializer of every part has run.

import {
  ArgumentNullException,
  KeyNotFoundException,
} from '../../shared/cs.js';
import { Trait, TraitCondition, StatusGrant } from './Types.js';
import { Cards } from './Cards.js';
import { CardCatalog as InitialPart } from './CardCatalog.Initial.js';
import { CardCatalog as LearnedPart } from './CardCatalog.Learned.js';

/** .NET's ArgumentNullException(paramName).Message. */
function argumentNull(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

// ---- v4.4 (#258): attributes are four, movement is an effect ----
// The six cards of movement only (足運び 後ろ跳び 摺り足 間合い切り 跳び退り 駆け込み) declare
// no face: BattleAttribute.None. AttributeRule.Fold counts them as 防御 when they carry a Guard
// (後ろ跳び 摺り足, and 間合い切り since v4.5) and as スキル otherwise. Declaring nothing keeps
// 集中's column step where it was. The other cards that used to declare the move flag now
// just carry Face.Move / Face.Push.
//
// The fourteen stance cards are stance only (v4.4 #257, written here by #333): they declare
// BattleAttribute.Stance, carry nothing but the stance face and aim at nobody.

// ---- Trait shorthands: the conditions of §1.2, one per helper ----
// grant is only for the Status effect (「<語>を n 付与」 / 「<語> +n スタック」).
// C# `(int gap, TraitEffect effect, int amount = 0, StatusGrant? grant = null)`: positional in JS.

/** 間合い n 以下 (「間合い 0」 is 0 以下). */
export function AtMost(gap, effect, amount = 0, grant = null) {
  return new Trait(TraitCondition.GapAtMost, effect, amount, { Threshold: gap, Grant: grant });
}

/** 間合い n 以上. */
export function AtLeast(gap, effect, amount = 0, grant = null) {
  return new Trait(TraitCondition.GapAtLeast, effect, amount, { Threshold: gap, Grant: grant });
}

/** 温存(残 ≥ n). */
export function Reserve(threshold, effect, amount = 0, grant = null) {
  return new Trait(TraitCondition.Reserve, effect, amount, { Threshold: threshold, Grant: grant });
}

/** The conditions that carry no parameter: 死力 / 初手 / 締め / 崩し後 / 連打 / 手薄. */
export function When(condition, effect, amount = 0, grant = null) {
  return new Trait(condition, effect, amount, { Grant: grant });
}

/** 連動(X): X is one of the four attributes (v4.4). 連動(ムーブ) is 移動後: `When(TraitCondition.Moved, ...)`. */
export function Combo(attribute, effect, amount = 0, grant = null) {
  return new Trait(TraitCondition.Combo, effect, amount, { Attribute: attribute, Grant: grant });
}

/** 予兆(攻撃 / 移動 / 防御). */
export function OmenIs(omen, effect, amount = 0, grant = null) {
  return new Trait(TraitCondition.OmenIs, effect, amount, { Omen: omen, Grant: grant });
}

/** 相手の状態(語). */
export function FoeHas(watch, effect, amount = 0, grant = null) {
  return new Trait(TraitCondition.FoeHas, effect, amount, { Watch: watch, Grant: grant });
}

/** 自分の状態(語). */
export function SelfHas(watch, effect, amount = 0, grant = null) {
  return new Trait(TraitCondition.SelfHas, effect, amount, { Watch: watch, Grant: grant });
}

// ---- Status shorthands: who a word lands on (battle_core_v4 §5) ----

/** A word that lands on the opponent: 出血 / 脆化 / 鈍足 / 威圧 / 疲労. */
export function Foe(kind, stacks) {
  return new StatusGrant(kind, stacks, { OnSelf: false });
}

/** A word that lands on the one playing: 強化 / 集中 / 見切り / 再生. */
export function Self(kind, stacks) {
  return new StatusGrant(kind, stacks, { OnSelf: true });
}

// ---- The canon order (the C# static constructor's list) ----

/** §2: the initial forty, the members of CardCatalog.Initial.cs. */
const INITIAL_ORDER = Object.freeze([
  'Thrust', 'KesaCut', 'Overhead', 'WristCut', 'SideSweep',
  'FlatStrike', 'ProbeThrust', 'Pierce', 'ThrowBlade', 'ReachThrust',
  'Brace', 'IronBlock', 'LowGuard', 'RiposteGuard', 'DeepBreath',
  'WaterStance', 'RockStance', 'FlowStance', 'FirstAid', 'SpiritRoar',
  'Focus', 'Observe', 'WarCry', 'SecondWind', 'Footwork',
  'BackLeap', 'SlideStep', 'BreakOff', 'Lunge', 'Feint',
  'BoarRush', 'Rend', 'BodyCheck', 'StoneThrow', 'ParryCut',
  'GuardThrust', 'StepInGuard', 'StepOutGuard', 'IronWall', 'TwistAway',
]);

/** §3: the learned forty, the members of CardCatalog.Learned.cs. */
const LEARNED_ORDER = Object.freeze([
  'MiasmaBlade', 'PriestPrayer', 'PurgeFlash', 'LineLash', 'AbyssStance',
  'HaulStep', 'RootStride', 'ThornShot', 'RootBind', 'Bulwark',
  'ShieldBash', 'PackHowl', 'FangRush', 'Whirlwind', 'RiposteStance',
  'HunterMark', 'SpearWall', 'ShieldPush', 'SnapGuard', 'LeapBack',
  'RisingCut', 'LegSweep', 'DashIn', 'GuardWalk', 'ThornCut',
  'CalmGuard', 'Deflect', 'CrescentCut', 'PommelStrike', 'MistStep',
  'KeenEye', 'Resolve', 'GaleThrust', 'AnchorStance', 'VitalThrust',
  'EvadeCut', 'BloodDance', 'WolfStance', 'ShadowLunge', 'LastStand',
]);

let allCards = null;

/**
 * Card data, written as records from card_document/swordsman_cards_v4.md v4.6 (2026-10-08,
 * #401: v4.5 of #352 with #5 横薙ぎ at 0〜2 and 23). The numbers are the canon's and are not derived here: the 素直 +2 and the band table
 * of §1.4 (the attack power by column and reach width) are already inside them.
 *
 * The core carries all eighty swordsman cards (#188): the initial forty (§2, #1-#40) live in
 * CardCatalog.Initial.js and the learned forty (§3, #41-#80) in CardCatalog.Learned.js. This
 * file keeps the shorthands those two are written with, the canon-order list and the lookup.
 */
export const CardCatalog = {
  /**
   * Every card the core knows, in canon order (#1 first, #80 last).
   *
   * Assigned in the static constructor, not by a field initializer: the order the field
   * initializers of partial files run in is undefined, but all of them have run before the
   * constructor body does. (JS: built on first read, from both parts; a frozen array.)
   */
  get All() {
    if (allCards === null) {
      allCards = Object.freeze([
        // §2: the initial forty.
        ...INITIAL_ORDER.map((name) => InitialPart[name]),
        // §3: the learned forty.
        ...LEARNED_ORDER.map((name) => LearnedPart[name]),
      ]);
    }
    return allCards;
  },

  ById(id) {
    if (id == null) throw argumentNull('id');
    for (const def of CardCatalog.All) {
      if (def.Id === id) return def;
    }
    throw new KeyNotFoundException(`Unknown card id "${id}".`);
  },
};
for (const name of INITIAL_ORDER) {
  Object.defineProperty(CardCatalog, name, { get: () => InitialPart[name], enumerable: true });
}
for (const name of LEARNED_ORDER) {
  Object.defineProperty(CardCatalog, name, { get: () => LearnedPart[name], enumerable: true });
}
Object.freeze(CardCatalog);

let prototypeKinds = null;

/**
 * The player deck the slice fights the polearm with: ten kinds × 2 = 20 cards, the floor of §8.
 * Why these ten is written on #71 and in the PR that added this file. They are named one by one
 * because CardCatalog.All holds all eighty since #188.
 */
export const PrototypeDeck = Object.freeze({
  Copies: 2,

  /** C# `public static IReadOnlyList<CardDef> Kinds { get; } = new[] { ... }` (JS: built on first read, frozen). */
  get Kinds() {
    if (prototypeKinds === null) {
      prototypeKinds = Object.freeze([
        CardCatalog.Thrust,
        CardCatalog.KesaCut,
        CardCatalog.ReachThrust,
        CardCatalog.Brace,
        CardCatalog.Feint,
        CardCatalog.BoarRush,
        CardCatalog.BodyCheck,
        CardCatalog.StepInGuard,
        CardCatalog.StepOutGuard,
        CardCatalog.ShieldBash,
      ]);
    }
    return prototypeKinds;
  },

  /** Laid out in the order above and not shuffled; the turn loop shuffles with its own RNG. */
  Build() {
    return Cards.BuildDeck(PrototypeDeck.Kinds, PrototypeDeck.Copies);
  },
});
