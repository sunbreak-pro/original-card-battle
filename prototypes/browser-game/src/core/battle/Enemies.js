// Port of unity-port/BattleCore/Enemies.cs
//
// Enemy data, written as records from enemy_document/enemy_roster_v4.md v4.19 (2026-10-11): the
// normal seven's attack faces sit at about 70% of the player's (#353, followed by #498).
// Nothing here decides anything: the decision tree is walked by EnemyAi and the numbers are
// resolved by the turn loop (#72).
//
// v4.15 (#396, followed by #446) reordered four trees so no action sits where it is never chosen
// (統牙の長竜 1〜2, 獄竜 ガルド 0, 大黒蛇 セルク 0 — 払いのけ only under pusher —, 歪みの根 3+ without
// 樹皮, which only root_sk takes) and the root's stage 3, and took off seven traits that could
// never hold (the three 温存 of a guard picked only at stamina 1, the 脆化 / 出血 / 疲労 bonuses
// on words gone before the next blow, and 盾を構える's 追撃 that dies with the phase).
//
// The thirteen of the 80% roster: the seven normal enemies (§2), the three elites (§3) and the
// three bosses (§4〜§6). Every row is an EnemyActionDef written off the same face table the cards
// use (§6.1); the omen label is read off the action (EnemyAi.LabelOf). An action declares the
// faces it carries and is counted as one attribute by AttributeRule.Fold (roster v4.5, #257): an
// action of movement only declares none and folds to スキル, as the six movement cards do.
//
// #51 brought the roster in whole: the addendum of #196 (弩の一射 2〜5, 靄の矢 2〜4, セルクの
// 3 以上の枝に瘴気の矢, 呪縛が残るフェーズに縛りの言葉を取らない), 二段斬り 7 × 2 (#257; 8 × 2 since v4.19), the
// two spear dragoons of v4.6 (#286), the six boss-only words with their own effects (§4.1 /
// §5.1 / §6.2) and the bosses' adaptations and stages on the hook of #50 (§4.4 / §5.4 / §6.4,
// TreeSwitch). How each condition is counted is written beside it below.
//
// The HP, the boss recovery 3 and the heavy blows on column 2 are the v4.4 numbers measured
// headless for #204 (roster §0, §10).
//
// ---- JS port notes ----
// - C# `public static class Enemies` → the frozen object `Enemies`. Its static readonly fields
//   (the thirteen EnemyDefs, MultiHitRedesignPending, All) are built together on first read, the way
//   a C# type initializer runs every field initializer at once before the first member is used
//   (CardCatalog.js does the same). Nothing is read from another module at module-evaluation time,
//   so the import cycles of the battle core (TurnLoop ↔ Enemies, Types ↔ Statuses, ...) are safe.
// - EnemyDef.Actions is a Map (C# IReadOnlyDictionary, insertion order = the roster's order).
// - TreeSwitch.Holds is a JS function (AdaptationView) => boolean (C# Func<AdaptationView, bool>).
// - The private records Tree / Adaptation / Stage are module-private classes; C# named arguments
//   to their constructors are a trailing options object.

import {
  defineRecord,
  idiv,
  ArgumentException,
  ArgumentNullException,
  ArgumentOutOfRangeException,
  KeyNotFoundException,
} from '../../shared/cs.js';
import {
  BattleAttribute,
  TargetKind,
  OmenKind,
  StatusKind,
  TraitCondition,
  TraitEffect,
  StanceHook,
  StanceWhen,
  StatusGrant,
  Trait,
  StanceDef,
  Reach,
  Face,
  EnemyActionDef,
  TreeSwitch,
  EnemyRank,
  EnemyDef,
} from './Types.js';
import { Constants } from './Constants.js';
import { Statuses } from './Statuses.js';

// ------------------------------------------------------------------ local helpers (not C# types)

/** Array.Empty<T>(): one shared, frozen empty list. */
const EMPTY = Object.freeze([]);

/** .NET's ArgumentNullException(paramName).Message. */
function argumentNull(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

/** .NET's ArgumentOutOfRangeException(paramName, actualValue, message).Message (Linux newline). */
function argumentOutOfRange(param, value, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${param}')\nActual value was ${value}.`);
}

// ------------------------------------------------------------------ the shorthands (private static)

/** The roster id. Ids never change (roster §1.8); names do. */
const PolearmWarpedId = 'polearm_warped';

function Foe(kind, stacks, cap = 0) {
  return new StatusGrant(kind, stacks, { Cap: cap });
}

/**
 * battle_core_v4 §5's default of 2 for 鈍足 an enemy puts on (#197): one stack put on in the
 * enemy phase is gone at the player's turn start before it slows anything. The roster writes 2
 * since v4.4's addendum. (C# `private const int SlowStacks = Constants.StatusApplyDefault;`)
 */
function SlowStacks() {
  return Constants.StatusApplyDefault;
}

function Self(kind, stacks) {
  return new StatusGrant(kind, stacks, { OnSelf: true });
}

function AtMost(gap, effect, amount = 0) {
  return new Trait(TraitCondition.GapAtMost, effect, amount, { Threshold: gap });
}

function AtLeast(gap, effect, amount = 0) {
  return new Trait(TraitCondition.GapAtLeast, effect, amount, { Threshold: gap });
}

function Reserve(threshold, effect, amount = 0, grant = null) {
  return new Trait(TraitCondition.Reserve, effect, amount, { Threshold: threshold, Grant: grant });
}

function When(condition, effect, amount = 0) {
  return new Trait(condition, effect, amount);
}

function FoeHas(watch, effect, amount = 0) {
  return new Trait(TraitCondition.FoeHas, effect, amount, { Watch: watch });
}

// ------------------------------------------------------------------ the bosses' switches (#51 on the hook of #50)

/** A boss's three branches, as data the adaptations and stages reshape. (C# private sealed record Tree.) */
class Tree extends defineRecord('Tree', ['Zero', 'One', 'Three']) {
  /** C# `public static Tree All(params string[] ids) => new Tree(ids, ids, ids);` — one array for all three. */
  static All(...ids) {
    return new Tree(ids, ids, ids);
  }

  /** The action on top of every band; where it already stood further down, it leaves that place. */
  Prefix(id) {
    return new Tree(Front(id, this.Zero), Front(id, this.One), Front(id, this.Three));
  }

  /** The action on top of the 0 and 1〜2 bands as Prefix puts it; the 3+ band stays as it is. */
  PrefixNear(id) {
    return new Tree(Front(id, this.Zero), Front(id, this.One), this.Three);
  }

  Without(ids) {
    return new Tree(Drop(ids, this.Zero), Drop(ids, this.One), Drop(ids, this.Three));
  }
}

function Front(id, branch) {
  const list = [id];
  for (const each of branch) if (each !== id) list.push(each);
  return list;
}

function Drop(ids, branch) {
  const list = [];
  for (const each of branch) {
    let dropped = false;
    for (const id of ids) dropped = dropped || id === each; // C# `dropped |= id == each` (no short-circuit; no side effects)
    if (!dropped) list.push(each);
  }
  return list;
}

/**
 * One adaptation of roster §4.4 / §5.4 / §6.4: a count over the history, and how it reshapes
 * the tree it holds over. Overrides are fixed; Retouch works overrides out of the ones the
 * layer it holds over already has (none on the base tree, the stage's over a stage), for an
 * adaptation that changes an action a stage changes too (root_st's 枯らしの息, #334).
 *
 * C# `private sealed record Adaptation(string Id, Func<AdaptationView, bool> Holds,
 * Func<Tree, Tree> Reshape, IReadOnlyList<EnemyActionDef>? Overrides = null, bool Cleanse = false,
 * Func<IReadOnlyList<EnemyActionDef>, IReadOnlyList<EnemyActionDef>>? Retouch = null)`.
 * JS: `new Adaptation(Id, Holds, Reshape, Overrides = null, { Cleanse, Retouch } = {})`.
 */
class Adaptation {
  constructor(Id, Holds, Reshape, Overrides = null, { Cleanse = false, Retouch = null } = {}) {
    this.Id = Id;
    this.Holds = Holds;
    this.Reshape = Reshape;
    this.Overrides = Overrides;
    this.Cleanse = Cleanse;
    this.Retouch = Retouch;
  }

  /** The overrides of this adaptation over a layer that has `layer`: the layer's, then its own on top. */
  Over(layer) {
    const overrides = [...layer];
    Put(overrides, this.Overrides ?? EMPTY);
    if (this.Retouch != null) Put(overrides, this.Retouch(layer));
    return overrides;
  }
}

/** C# `overrides.RemoveAll(o => o.Id == each.Id); overrides.Add(each);` for each added, in place. */
function Put(overrides, added) {
  for (const each of added) {
    for (let i = overrides.length - 1; i >= 0; i--) {
      if (overrides[i].Id === each.Id) overrides.splice(i, 1);
    }
    overrides.push(each);
  }
}

/**
 * One stage: an HP line that, once crossed, holds for the rest of the battle (戻らない — 再生
 * lifting the HP back over the line does not undo it), its tree, its overrides and its moves.
 * Without are actions the stage takes out of every tree it holds over (ガルド: 糸を緩める).
 *
 * C# `private sealed record Stage(string Id, Func<AdaptationView, bool> Crossed, Func<Tree, Tree> Reshape,
 * IReadOnlyList<EnemyActionDef> Overrides, int EnterMove = 0, bool Sway = false, IReadOnlyList<string>? Without = null)`.
 * JS: `new Stage(Id, Crossed, Reshape, Overrides, { EnterMove, Sway, Without } = {})`.
 */
class Stage {
  constructor(Id, Crossed, Reshape, Overrides, { EnterMove = 0, Sway = false, Without = null } = {}) {
    this.Id = Id;
    this.Crossed = Crossed;
    this.Reshape = Reshape;
    this.Overrides = Overrides;
    this.EnterMove = EnterMove;
    this.Sway = Sway;
    this.Without = Without;
  }

  Holds(view) {
    return this.Crossed(view) || view.HasHeld(this.Id);
  }
}

/**
 * The switches the core asks, in the order that makes the last one holding the right one
 * (battle_core_v4 §6.3): each adaptation over the base tree; then, stage by stage, the stage
 * itself and each adaptation over that stage's tree (id "stage+adaptation"). A later stage so
 * wins over an earlier one, and an adaptation over the stage that holds. Of several
 * adaptations holding at once the later one in the roster's table wins. The stage's moves
 * (EnterMove, Sway) ride the stage's own switch only; its overrides and its Without carry into
 * the adaptations over it.
 */
function Layered(baseTree, { adaptations, stages }) {
  const switches = [];
  for (const adaptation of adaptations) {
    const overrides = adaptation.Over(EMPTY);
    switches.push(Switch(adaptation.Id, adaptation.Holds, adaptation.Reshape(baseTree), overrides.length === 0 ? null : overrides,
      { cleanse: adaptation.Cleanse }));
  }
  for (const stage of stages) {
    const without = stage.Without ?? EMPTY;
    const stageTree = stage.Reshape(baseTree).Without(without);
    // C# passes the method group `stage.Holds`: a delegate bound to the stage.
    switches.push(Switch(stage.Id, (view) => stage.Holds(view), stageTree, stage.Overrides,
      { enterMove: stage.EnterMove, sway: stage.Sway }));
    for (const adaptation of adaptations) {
      const overrides = adaptation.Over(stage.Overrides);
      const adaptationHolds = adaptation.Holds;
      const stageHolds = (view) => stage.Holds(view);
      switches.push(Switch(
        stage.Id + '+' + adaptation.Id,
        (view) => stageHolds(view) && adaptationHolds(view),
        adaptation.Reshape(stageTree).Without(without),
        overrides,
        { cleanse: adaptation.Cleanse }));
    }
  }
  return switches;
}

function Switch(id, holds, tree, overrides, { enterMove = 0, sway = false, cleanse = false } = {}) {
  return new TreeSwitch(id, holds, tree.Zero, tree.One, tree.Three, overrides, enterMove, sway, cleanse);
}

// ------------------------------------------------------------------ what the adaptations count
// (roster §1.5: by counting the history, never by chance)

/**
 * 「アタックを n 回続けて受けると」: the player's cards counted as 攻撃 in a row, the count
 * starting over once it reaches n (so 6 in a row is two runs). Holds when a run was completed
 * by a card of the last finished turn — for the one phase after it (1 フェーズで戻る).
 */
function RunCompletedLastTurn(view, n) {
  const cards = view.Player.Cards;
  const lastTurnFrom = cards.length - view.Player.LastTurnCards.length;
  let run = 0;
  let completed = false;
  for (let i = 0; i < cards.length; i++) {
    run = cards[i] === BattleAttribute.Attack ? run + 1 : 0;
    if (run < n) continue;
    run = 0;
    completed = i >= lastTurnFrom && view.Player.LastTurnCards.length > 0;
  }
  return completed && view.Player.TurnEnds.length > 0;
}

/**
 * 「Guard を 9 以上で 2 ターン受け続けると」, until 「プレイヤーの Guard が 5 以下になる」:
 * two turn ends in a row on Guard 9 or more set it, and a turn end on 5 or less clears it.
 */
function GuardSiege(view) {
  let on = false;
  let run = 0;
  for (const turn of view.Player.TurnEnds) {
    if (turn.Guard <= 5) on = false;
    run = turn.Guard >= 9 ? run + 1 : 0;
    if (run >= 2) on = true;
  }
  return on;
}

/**
 * 「〜で 2 ターン続けてターンを終えた」 until 「間合いが〜になる」: two turn ends in a row whose N
 * satisfies `stay` set it; a turn end whose N satisfies `leave` clears it, and so does N at this
 * moment.
 */
function Lingered(view, stay, leave) {
  let on = false;
  let run = 0;
  for (const turn of view.Player.TurnEnds) {
    if (leave(turn.Gap)) on = false;
    run = stay(turn.Gap) ? run + 1 : 0;
    if (run >= 2) on = true;
  }
  return on && !leave(view.Gap);
}

/**
 * 「〜を 2 回…されると」 counted over the whole battle: holds after the turn in which the count
 * (turn numbers, one per time) reached an even number — the 2nd, the 4th, … — for one phase.
 */
function EverySecondInLastTurn(view, turns) {
  const total = turns.length;
  let inLast = 0;
  for (const turn of turns) if (turn === view.Turn) inLast++;
  return inLast > 0 && idiv(total, 2) > idiv(total - inLast, 2);
}

/** root_st 「プレイヤーがスタンスの札を置いた」: a stance card among the last finished turn's. */
function PlacedStanceLastTurn(view) {
  for (const played of view.Player.LastTurnCards) {
    if (played === BattleAttribute.Stance) return true;
  }
  return false;
}

/**
 * root_m 「2 ターン続けて自分のマスを動かした」: the last two turn ends both say the player moved
 * itself. Counted in runs of two like RunCompletedLastTurn, so it holds for one phase each time
 * two more turns of moving add up.
 */
function MovedTwiceLastTurn(view) {
  const turns = view.Player.TurnEnds;
  let run = 0;
  let completed = false;
  for (let i = 0; i < turns.length; i++) {
    run = turns[i].Moved ? run + 1 : 0;
    if (run < 2) continue;
    run = 0;
    completed = i === turns.length - 1;
  }
  return completed;
}

/**
 * The way the player's own last move of its last finished turn went (+1 前へ, toward the
 * enemies; -1 後ろへ; 0 none). Read from the move itself, not from the change of cell between
 * turn ends, which a push or pull in between, or a step out and back, would hide
 * (roster §6.4 「直前に動いた向き」).
 */
function LastStep(view) {
  const turns = view.Player.TurnEnds;
  return turns.length === 0 ? 0 : turns[turns.length - 1].LastMove;
}

// ------------------------------------------------------------------ Build and its checks

/**
 * Builds an enemy and refuses a tree that names an action the enemy does not have, an empty
 * branch (§6.1: every branch holds something), or a face outside §7.3's cell limits, so a
 * typo in the data fails at load and not mid-battle. A switch's branches and overrides are
 * held to the same.
 *
 * C# named parameters (maxHp:, maxStamina:, ...) are the options object.
 */
function Build(id, name, rank, {
  maxHp,
  maxStamina,
  recovery,
  size,
  branchAtGapZero,
  branchAtGapOneToTwo,
  branchAtGapThreePlus,
  actions,
  actionsPerPhase = 1,
  switches = null,
}) {
  if (size < 1 || size > Constants.EnemySizeMax) {
    throw argumentOutOfRange('size', size, `Enemy "${id}": size is 1..${Constants.EnemySizeMax}.`);
  }
  if (actionsPerPhase < 1 || actionsPerPhase > Constants.EliteActions) {
    throw argumentOutOfRange('actionsPerPhase', actionsPerPhase, `Enemy "${id}": 1..${Constants.EliteActions} actions.`);
  }
  const map = new Map();
  for (const action of actions) {
    if (map.has(action.Id)) {
      throw new ArgumentException(`Enemy "${id}" defines action "${action.Id}" twice.`);
    }
    CheckCells(id, action);
    map.set(action.Id, action);
  }

  CheckBranches(id, map, branchAtGapZero, branchAtGapOneToTwo, branchAtGapThreePlus);
  for (const each of switches ?? EMPTY) {
    CheckBranches(id + '/' + each.Id, map,
      each.BranchAtGapZero ?? branchAtGapZero, each.BranchAtGapOneToTwo ?? branchAtGapOneToTwo, each.BranchAtGapThreePlus ?? branchAtGapThreePlus);
    for (const stand of each.Overrides ?? EMPTY) {
      if (!map.has(stand.Id)) throw new ArgumentException(`Enemy "${id}": switch "${each.Id}" overrides unknown action "${stand.Id}".`);
      CheckCells(id, stand);
    }
  }

  return new EnemyDef(
    id, name, maxHp, maxStamina, recovery, size,
    branchAtGapZero, branchAtGapOneToTwo, branchAtGapThreePlus, map,
    rank, actionsPerPhase, switches);
}

function CheckCells(id, action) {
  if (Math.abs(action.Face.Move) > Constants.MoveStepMax || Math.abs(action.Face.Push) > Constants.MoveStepMax) {
    throw new ArgumentException(`Enemy "${id}": action "${action.Id}" moves more than ${Constants.MoveStepMax} cells.`);
  }
}

function CheckBranches(id, map, ...branches) {
  for (const branch of branches) {
    if (branch.length === 0) throw new ArgumentException(`Enemy "${id}" has an empty branch (§6.1).`);
    for (const actionId of branch) {
      if (!map.has(actionId)) {
        throw new ArgumentException(`Enemy "${id}" branches to unknown action "${actionId}".`);
      }
    }
  }
}

// ------------------------------------------------------------------ §4: 大黒蛇 セルク's parts

function PriestBase() {
  return new Tree(
    ['staff_strike', 'coil'],
    ['miasma_bolt', 'binding_word', 'coil'],
    ['miasma_rite', 'ward', 'miasma_bolt', 'coil']);
}

function MiasmaRite(cap) {
  return new EnemyActionDef(
    'miasma_rite', '瘴気の儀', BattleAttribute.Skill, 3,
    new Face({ Reach: new Reach(2, 4), Statuses: [Foe(StatusKind.MiasmaShroud, 1, cap), Self(StatusKind.Regen, 1)] }),
    {
      Description: cap >= 2
        ? '間合い 2〜4 に届く。瘴気纏いを 1 付与する（2 まで重なる）。自分に再生を 1 付与する。瘴気纏いが上限のあいだは使わない'
        : '間合い 2〜4 に届く。瘴気纏いを 1 付与する。自分に再生を 1 付与する。瘴気纏いが上限のあいだは使わない',
      NotAtFoeCap: true,
    });
}

function StaffStrike(breaks) {
  return new EnemyActionDef(
    'staff_strike', '鎌首の打ち込み', BattleAttribute.Attack, 3,
    new Face({ Power: 13, Break: breaks }),
    AtMost(0, TraitEffect.PowerBonus, 5),
    {
      Description: breaks > 0
        ? '間合い 0〜1 に届く。崩しを ' + breaks + ' 付与する。間合い 0: 威力 +5'
        : '間合い 0〜1 に届く。間合い 0: 威力 +5',
    });
}

// ------------------------------------------------------------------ §5: 獄竜 ガルド's parts

function AnglerBase() {
  return new Tree(
    ['drown', 'deep_water', 'fathom_call', 'line_whip'],
    ['reel_in', 'line_whip', 'slack_line'],
    ['hook_cast', 'reel_in']);
}

// roster §5.1 / §5.4 give 鉤爪 no stack limit (only 瘴気纏い / 深み / 枯らし / 根張り stop at 2),
// so the stacks add up: the stage-2 鉤縄 on a player holding 1 leaves 3.
function HookCast(stacks) {
  return new EnemyActionDef(
    'hook_cast', '鉤縄を打つ', BattleAttribute.Attack | BattleAttribute.Skill, 3,
    new Face({ Power: 9, Reach: new Reach(2, 4), Statuses: [Foe(StatusKind.Hook, stacks)] }),
    AtLeast(3, TraitEffect.PowerBonus, 3),
    { Description: '間合い 2〜4 に届く。鉤爪を ' + stacks + ' 付与する。間合い 3 以上: 威力 +3' });
}

// ------------------------------------------------------------------ §6: 歪みの根's parts

function RootBase() {
  return new Tree(
    ['twist', 'root_grip', 'sweep', 'creeping_root'],
    ['sweep', 'root_grip', 'thorn_volley', 'creeping_root'],
    ['wither_breath', 'thorn_volley', 'creeping_root']);
}

function WitherBreath(reach, cap) {
  return new EnemyActionDef(
    'wither_breath', '枯らしの息', BattleAttribute.Skill, 2,
    new Face({ Reach: reach, Statuses: [Foe(StatusKind.Withering, 1, cap), Foe(StatusKind.Fatigue, 1)] }),
    { Description: '間合い ' + reach.ToText() + ' に届く。枯らしを 1 付与する' + (cap >= 2 ? '（2 まで重なる）' : '') + '。疲労を 1 付与する' });
}

/** The root's own 枯らしの息 (roster §6.3): 2〜4, 枯らし 1 at most. (C# a property: a new instance each read.) */
function RootWitherBreath() {
  return WitherBreath(new Reach(2, 4), 1);
}

/**
 * root_st (#334): the breath, which on landing also stops the player's newest permanent
 * effect for the rest of the turn and the next one (Face.StopStance).
 */
function StoppingStance(breath) {
  return breath.with({
    Face: breath.Face.with({ StopStance: true }),
    Description: breath.Description + '。当たると、相手がいちばん新しく置いた永続の効果 1 つが 1 ターン働かない',
  });
}

function Find(actions, id) {
  for (const each of actions) if (each.Id === id) return each;
  return null;
}

function RootGrip(reach, cap) {
  return new EnemyActionDef(
    'root_grip', '根を張る', BattleAttribute.Skill, 2,
    new Face({ Reach: reach, Statuses: [Foe(StatusKind.Rooting, 1, cap), Self(StatusKind.Regen, 1)] }),
    { Description: '間合い ' + reach.ToText() + ' に届く。根張りを 1 付与する' + (cap >= 2 ? '（2 まで重なる）' : '') + '。自分に再生を 1 付与する' });
}

function Twist(breaks) {
  return new EnemyActionDef(
    'twist', '捻じる', BattleAttribute.Attack | BattleAttribute.Skill, 2,
    new Face({ Power: 5, Break: breaks }),
    When(TraitCondition.Broken, TraitEffect.PowerBonus, 3),
    { Description: '間合い 0〜1 に届く。崩し ' + breaks + '。崩し後: 相手のスタミナ 3 未満で威力 +3' });
}

/** 伸びる根: null is the base (push at gap 1 or less, pull at 2 or more); ±1 is root_m's fixed way. */
function CreepingRoot(push) {
  return new EnemyActionDef(
    'creeping_root', '伸びる根', BattleAttribute.Guard, 1,
    new Face({ Guard: 2, Push: push ?? 1, Reach: new Reach(0, 4) }),
    {
      Description: push == null ? '間合い 0〜4 に届く。Guard 2。相手が間合い 1 以下なら 1 マス押し、2 以上なら 1 マス引く'
        : push > 0 ? '間合い 0〜4 に届く。Guard 2。相手を 1 マス押す'
          : '間合い 0〜4 に届く。Guard 2。相手を 1 マス引く',
      Omen: OmenKind.Guard,
      PullBeyond: push == null ? 1 : null,
    });
}

// ------------------------------------------------------------------ the roster (static readonly fields)

/** Built once, on first read: every static readonly field of the C# class, in declaration order. */
let roster = null;

function Roster() {
  if (roster !== null) return roster;
  const r = {};

  // ---- §2 normal ----

  /**
   * roster §2.1: 錆槍の竜兵. HP 60 / max stamina 10 / recovery 2 / size 1 / favours gap 1〜2:
   * the sweep punishes 1〜2, the shove throws a close player two cells back, the step closes
   * from 3+. No statuses.
   */
  r.PolearmWarped = Build(
    PolearmWarpedId, '錆槍の竜兵', EnemyRank.Normal, {
      maxHp: 60, maxStamina: 10, recovery: 2, size: 1,
      branchAtGapZero: ['shove', 'guard_up', 'reach_thrust'],
      branchAtGapOneToTwo: ['sweep', 'reach_thrust', 'guard_up'],
      branchAtGapThreePlus: ['step_forward', 'guard_up'],
      actions: [
        new EnemyActionDef(
          'sweep', '払い斬り', BattleAttribute.Attack, 2,
          new Face({ Power: 9, Reach: new Reach(1, 2) }),
          AtLeast(2, TraitEffect.PowerBonus, 3),
          { Description: '間合い 1〜2 に届く。間合い 2 以上: 威力 +3' }),

        new EnemyActionDef(
          'shove', '柄尻の押し込み', BattleAttribute.Attack, 2,
          new Face({ Power: 6, Push: 2, Reach: Reach.Only(0) }),
          When(TraitCondition.Unguarded, TraitEffect.PowerBonus, 3),
          { Description: '間合い 0 に届く。相手を 2 マス押す。無防備: 相手の Guard が 0 なら威力 +3' }),

        new EnemyActionDef(
          'reach_thrust', '穂先の突き', BattleAttribute.Attack, 1,
          new Face({ Power: 5, Reach: new Reach(0, 2) }),
          { Description: '間合い 0〜2 に届く' }),

        new EnemyActionDef(
          'guard_up', '柄で受ける', BattleAttribute.Guard, 1,
          new Face({ Guard: 3 }),
          { Targets: TargetKind.Self, Description: 'Guard 3' }),

        new EnemyActionDef(
          'step_forward', '踏み込み', BattleAttribute.Guard, 1,
          new Face({ Move: 1, Guard: 2 }),
          { Targets: TargetKind.Self, Description: '前へ 1 動き、Guard 2 を得る' }),
      ],
    });

  /** roster §2.2: 瘴牙の走竜. HP 80, favours gap 0: bites for bleed, leaps in from 1〜2, dashes from 3+. */
  r.ShadowHound = Build(
    'shadow_hound', '瘴牙の走竜', EnemyRank.Normal, {
      maxHp: 80, maxStamina: 10, recovery: 2, size: 1,
      branchAtGapZero: ['bite', 'crouch'],
      branchAtGapOneToTwo: ['lunge_in', 'crouch'],
      branchAtGapThreePlus: ['dash', 'crouch'],
      actions: [
        new EnemyActionDef(
          'bite', '噛みつき', BattleAttribute.Attack, 2,
          new Face({ Power: 9, Reach: Reach.Only(0), Statuses: [Foe(StatusKind.Bleed, 2)] }),
          { Description: '間合い 0 に届く。出血を 2 付与する' }),
        new EnemyActionDef(
          'lunge_in', '跳びかかり', BattleAttribute.Attack, 2,
          new Face({ Power: 6, Move: 2, Reach: new Reach(1, 2) }),
          AtLeast(2, TraitEffect.PowerBonus, 3),
          { Description: '間合い 1〜2 に届く。当ててから前へ 2。間合い 2 以上: 威力 +3' }),
        new EnemyActionDef(
          'dash', '駆け寄る', BattleAttribute.None, 1,
          new Face({ Move: 2 }),
          { Targets: TargetKind.Self, Description: '前へ 2' }),
        new EnemyActionDef(
          'crouch', '身を低くする', BattleAttribute.Guard, 1,
          new Face({ Guard: 3 }),
          { Targets: TargetKind.Self, Description: 'Guard 3' }),
      ],
    });

  /** roster §2.3: 瘴甲の竜兵. HP 87, favours gap 0: sets 鉄の身 from afar, trudges in, swings heavy. */
  r.RustedRevenant = Build(
    'rusted_revenant', '瘴甲の竜兵', EnemyRank.Normal, {
      maxHp: 87, maxStamina: 10, recovery: 2, size: 1,
      branchAtGapZero: ['heavy_swing', 'iron_body', 'trudge'],
      branchAtGapOneToTwo: ['press', 'trudge'],
      branchAtGapThreePlus: ['iron_body', 'trudge'],
      actions: [
        new EnemyActionDef(
          'iron_body', '鉄の身', BattleAttribute.Stance, 2,
          new Face({ Stance: new StanceDef(StanceHook.TurnStart, { Guard: 3 }) }),
          { Targets: TargetKind.Self, Description: '構え: 毎ターン開始に Guard +3' }),
        new EnemyActionDef(
          'heavy_swing', '大振り', BattleAttribute.Attack, 3,
          new Face({ Power: 15, Reach: Reach.Only(0) }),
          When(TraitCondition.Broken, TraitEffect.PowerBonus, 3),
          { Description: '間合い 0 に届く。崩し後: 相手のスタミナ 3 未満で威力 +3' }),
        new EnemyActionDef(
          'press', '押し込み', BattleAttribute.Attack, 1,
          new Face({ Power: 4, Move: 1, Reach: new Reach(1, 2) }),
          { Description: '間合い 1〜2 に届く。当ててから前へ 1' }),
        new EnemyActionDef(
          'trudge', 'のし歩く', BattleAttribute.Guard, 1,
          new Face({ Move: 1, Guard: 2 }),
          { Targets: TargetKind.Self, Description: '前へ 1、Guard 2' }),
      ],
    });

  /**
   * roster §2.4: 灰弩の竜兵. HP 81, favours gap 2〜3: the bolt from afar, shoots and backs off at
   * 1〜2, kicks away at 0. The bolt reaches 2〜5 (#196): the far branch never steps forward, so it
   * has to reach the farthest gap its fields allow (the pair's back cell from cell 1 is 5).
   */
  r.CrossbowHunter = Build(
    'crossbow_hunter', '灰弩の竜兵', EnemyRank.Normal, {
      maxHp: 81, maxStamina: 10, recovery: 2, size: 1,
      branchAtGapZero: ['kick_off', 'brace'],
      branchAtGapOneToTwo: ['backstep', 'brace'],
      branchAtGapThreePlus: ['bolt', 'brace'],
      actions: [
        new EnemyActionDef(
          'bolt', '弩の一射', BattleAttribute.Attack, 3,
          new Face({ Power: 15, Reach: new Reach(2, 5), Statuses: [Foe(StatusKind.Intimidate, 1)] }),
          { Description: '間合い 2〜5 に届く。威圧を 1 付与する' }),
        new EnemyActionDef(
          'backstep', '退き撃ち', BattleAttribute.Attack, 2,
          new Face({ Power: 6, Move: -1, Reach: new Reach(1, 2) }),
          Reserve(4, TraitEffect.GuardBonus, 3),
          { Description: '間合い 1〜2 に届く。撃ってから後ろへ 1。温存: 残 4 以上で Guard +3' }),
        new EnemyActionDef(
          'kick_off', '蹴り離し', BattleAttribute.Attack, 2,
          new Face({ Power: 6, Push: 2, Reach: Reach.Only(0) }),
          { Description: '間合い 0 に届く。相手を 2 マス押す' }),
        new EnemyActionDef(
          'brace', '盾を構える', BattleAttribute.Guard, 1,
          new Face({ Guard: 3 }),
          { Targets: TargetKind.Self, Description: 'Guard 3' }),
      ],
    });

  /**
   * roster §2.5: 燐弓の竜兵. HP 72, favours gap 2〜3: the misted arrow tires, fading pushes a close
   * player off. The arrow reaches 2〜4 (#196): it never moves, and a player backed to cell 1
   * stands at gap 4.
   */
  r.MistArcher = Build(
    'mist_archer', '燐弓の竜兵', EnemyRank.Normal, {
      maxHp: 72, maxStamina: 10, recovery: 2, size: 1,
      branchAtGapZero: ['fade', 'scatter'],
      branchAtGapOneToTwo: ['fade', 'scatter'],
      branchAtGapThreePlus: ['mist_arrow', 'scatter'],
      actions: [
        new EnemyActionDef(
          'mist_arrow', '靄の矢', BattleAttribute.Attack, 3,
          new Face({ Power: 15, Reach: new Reach(2, 4), Statuses: [Foe(StatusKind.Fatigue, 1)] }),
          AtLeast(3, TraitEffect.PowerBonus, 5),
          { Description: '間合い 2〜4 に届く。疲労を 1 付与する。間合い 3 以上: 威力 +5' }),
        new EnemyActionDef(
          'fade', '靄に溶ける', BattleAttribute.Guard, 2,
          new Face({ Guard: 4, Push: 2 }),
          When(TraitCondition.Unguarded, TraitEffect.NextTurnRecovery, 1),
          {
            Description: '間合い 0〜1 に届く。Guard 4、相手を 2 マス押す。無防備: 次の回復 +1',
            Omen: OmenKind.Guard,
          }),
        new EnemyActionDef(
          'scatter', '散り矢', BattleAttribute.Attack, 1,
          new Face({ Power: 5, Reach: new Reach(0, 2) }),
          { Description: '間合い 0〜2 に届く' }),
      ],
    });

  /**
   * roster §2.6: 燐刃の竜兵. HP 96, favours gap 0. The twin slash is 8 × 2 and puts nothing on
   * the player (v4.5, #257: the multi-hit rule binds enemy actions too). 十字受け is the only
   * action of the 3+ branch and closes two cells with its Guard (v4.13, #328): the battle opens
   * at gap 3, so it is always the first action.
   */
  r.TwinBladeWarped = Build(
    'twin_blade_warped', '燐刃の竜兵', EnemyRank.Normal, {
      maxHp: 96, maxStamina: 10, recovery: 2, size: 1,
      branchAtGapZero: ['twin_slash', 'retreat_cut'],
      branchAtGapOneToTwo: ['step_slash', 'retreat_cut'],
      branchAtGapThreePlus: ['cross_guard'],
      actions: [
        new EnemyActionDef(
          'twin_slash', '二段斬り', BattleAttribute.Attack, 3,
          new Face({ Power: 8, Hits: 2, Reach: Reach.Only(0) }),
          { Description: '間合い 0 に届く。威力 8 × 2' }),
        new EnemyActionDef(
          'step_slash', '踏み込み斬り', BattleAttribute.Attack, 2,
          new Face({ Power: 6, Move: 2, Reach: new Reach(1, 2) }),
          { Description: '間合い 1〜2 に届く。当ててから前へ 2' }),
        new EnemyActionDef(
          'retreat_cut', '退き斬り', BattleAttribute.Attack, 1,
          new Face({ Power: 5 }),
          { Description: '間合い 0〜1 に届く' }),
        new EnemyActionDef(
          'cross_guard', '十字受け', BattleAttribute.Guard, 1,
          new Face({ Guard: 2, Move: 2 }),
          Reserve(4, TraitEffect.Status, 0, Self(StatusKind.Parry, 1)),
          TargetKind.Self,
          '前へ 2、Guard 2。温存: 残 4 以上で見切りを 1 付与する'),
      ],
    });

  /**
   * roster §2.7 (v4.6, #286): 晶槍の竜兵. HP 90, favours gap 1〜2. Unlike the 錆槍 it never moves
   * the player: it rushes in from afar and thrusts and backs off two when adjacent. Every move
   * it makes rides an attack.
   */
  r.PolearmCrystal = Build(
    'polearm_crystal', '晶槍の竜兵', EnemyRank.Normal, {
      maxHp: 90, maxStamina: 10, recovery: 2, size: 1,
      branchAtGapZero: ['recoil_thrust', 'short_jab', 'haft_guard'],
      branchAtGapOneToTwo: ['great_thrust', 'short_jab', 'haft_guard'],
      branchAtGapThreePlus: ['crystal_rush', 'haft_guard'],
      actions: [
        new EnemyActionDef(
          'great_thrust', '大穂の突き', BattleAttribute.Attack, 3,
          new Face({ Power: 15, Reach: new Reach(1, 2) }),
          { Description: '間合い 1〜2 に届く。威力 15' }),
        new EnemyActionDef(
          'crystal_rush', '晶穂の突進', BattleAttribute.Attack, 2,
          new Face({ Power: 6, Move: 2, Reach: new Reach(2, 4) }),
          AtLeast(3, TraitEffect.PowerBonus, 3),
          { Description: '間合い 2〜4 に届く。当ててから前へ 2。間合い 3 以上: 威力 +3' }),
        new EnemyActionDef(
          'recoil_thrust', '引き突き', BattleAttribute.Attack, 2,
          new Face({ Power: 6, Move: -2, Reach: Reach.Only(0) }),
          When(TraitCondition.Unguarded, TraitEffect.PowerBonus, 3),
          { Description: '間合い 0 に届く。当ててから後ろへ 2。無防備: 相手の Guard が 0 なら威力 +3' }),
        new EnemyActionDef(
          'short_jab', '短い突き', BattleAttribute.Attack, 1,
          new Face({ Power: 5, Reach: new Reach(0, 2) }),
          { Description: '間合い 0〜2 に届く' }),
        new EnemyActionDef(
          'haft_guard', '柄を立てる', BattleAttribute.Guard, 1,
          new Face({ Guard: 3 }),
          { Targets: TargetKind.Self, Description: 'Guard 3' }),
      ],
    });

  // ---- §3 elite: two actions a phase ----

  /** roster §3.1: 鉄壁の門竜. HP 110, size 2 (refuses push / pull), favours gap 0: breaks and slows up close. */
  r.ArmoredWarden = Build(
    'armored_warden', '鉄壁の門竜', EnemyRank.Elite, {
      maxHp: 110, maxStamina: 12, recovery: 3, size: 2,
      branchAtGapZero: ['helm_splitter', 'shield_bash', 'brace'],
      branchAtGapOneToTwo: ['push_shield', 'advance_guard', 'brace'],
      branchAtGapThreePlus: ['iron_wall', 'advance_guard', 'brace'],
      actions: [
        new EnemyActionDef(
          'iron_wall', '鉄壁', BattleAttribute.Stance, 2,
          new Face({ Stance: new StanceDef(StanceHook.TurnStart, { Guard: 3 }) }),
          { Targets: TargetKind.Self, Description: '構え: 毎ターン開始に Guard +3' }),
        new EnemyActionDef(
          'helm_splitter', '打ち下ろし', BattleAttribute.Attack, 2,
          new Face({ Power: 8, Break: 1 }),
          AtMost(0, TraitEffect.BreakBonus, 1),
          { Description: '間合い 0〜1 に届く。崩し 1。間合い 0: 崩し +1' }),
        new EnemyActionDef(
          'push_shield', '盾で押し出る', BattleAttribute.Attack, 2,
          new Face({ Power: 5, Move: 1, Reach: new Reach(1, 2) }),
          { Description: '間合い 1〜2 に届く。当ててから前へ 1' }),
        new EnemyActionDef(
          'shield_bash', '盾打ち', BattleAttribute.Attack, 1,
          new Face({ Power: 4, Reach: Reach.Only(0), Statuses: [Foe(StatusKind.Slow, SlowStacks())] }),
          new Trait(TraitCondition.Combo, TraitEffect.PowerBonus, 3, { Attribute: BattleAttribute.Attack }),
          { Description: '間合い 0 に届く。鈍足を 2 付与する。連動: 同じフェーズにアタックを出していれば威力 +3' }),
        new EnemyActionDef(
          'advance_guard', '盾を掲げて前進', BattleAttribute.Guard, 1,
          new Face({ Guard: 2, Move: 1 }),
          { Targets: TargetKind.Self, Description: 'Guard 2、前へ 1' }),
        new EnemyActionDef(
          'brace', '盾を構える', BattleAttribute.Guard, 1,
          new Face({ Guard: 3 }),
          { Targets: TargetKind.Self, Description: 'Guard 3' }),
      ],
      actionsPerPhase: Constants.EliteActions,
    });

  /** roster §3.2: 統牙の長竜. HP 120, favours gap 0: rends for bleed, herds a close player to the wall, pounces from afar. */
  r.PackAlpha = Build(
    'pack_alpha', '統牙の長竜', EnemyRank.Elite, {
      maxHp: 120, maxStamina: 12, recovery: 3, size: 1,
      branchAtGapZero: ['rend', 'herd', 'crouch'],
      branchAtGapOneToTwo: ['pounce', 'crouch', 'howl'],
      branchAtGapThreePlus: ['hunt_stance', 'pounce', 'howl'],
      actions: [
        new EnemyActionDef(
          'hunt_stance', '狩りの構え', BattleAttribute.Stance, 2,
          new Face({ Stance: new StanceDef(StanceHook.AttackBonus, StanceWhen.GapAtMost, { Threshold: 0, Power: 3 }) }),
          { Targets: TargetKind.Self, Description: '構え: 相手との間合いが 0 のときアタック威力 +3' }),
        new EnemyActionDef(
          'rend', '引き裂き', BattleAttribute.Attack, 2,
          new Face({ Power: 8, Reach: Reach.Only(0), Statuses: [Foe(StatusKind.Bleed, 1)] }),
          { Description: '間合い 0 に届く。出血を 1 付与する' }),
        new EnemyActionDef(
          'pounce', '飛びかかり', BattleAttribute.Attack, 2,
          new Face({ Power: 5, Move: 2, Reach: new Reach(1, 3) }),
          AtLeast(2, TraitEffect.PowerBonus, 5),
          { Description: '間合い 1〜3 に届く。当ててから前へ 2。間合い 2 以上: 威力 +5' }),
        new EnemyActionDef(
          'herd', '追い立てる', BattleAttribute.Skill, 1,
          new Face({ Push: 1, Reach: Reach.Only(0), Statuses: [Foe(StatusKind.Slow, SlowStacks())] }),
          {
            Description: '間合い 0 に届く。相手を 1 マス押し、鈍足を 2 付与する',
            Omen: OmenKind.Skill,
          }),
        new EnemyActionDef(
          'howl', '遠吠え', BattleAttribute.Skill, 1,
          new Face({ Statuses: [Self(StatusKind.Empower, 1)] }),
          { Targets: TargetKind.Self, Description: '自分に強化を 1 付与する' }),
        new EnemyActionDef(
          'crouch', '身を伏せる', BattleAttribute.Guard, 1,
          new Face({ Guard: 3 }),
          Reserve(5, TraitEffect.GuardBonus, 3),
          TargetKind.Self,
          'Guard 3。温存: 残 5 以上で Guard +3'),
      ],
      actionsPerPhase: Constants.EliteActions,
    });

  /**
   * roster §3.3 (v4.6, #286): 不退の槍竜. HP 120, favours gap 1〜2, never steps back: it moves the
   * player into its spear's reach first (柄で突き放す pushes 2, 穂で掛け寄せる pulls 2), then
   * the redrawn second action thrusts, +3 for the skill it took first (連動).
   */
  r.PolearmUnyielding = Build(
    'polearm_unyielding', '不退の槍竜', EnemyRank.Elite, {
      maxHp: 120, maxStamina: 12, recovery: 3, size: 1,
      branchAtGapZero: ['haft_shove', 'plate_guard'],
      branchAtGapOneToTwo: ['long_thrust', 'plate_guard'],
      branchAtGapThreePlus: ['set_spear', 'hook_in', 'clank_on'],
      actions: [
        new EnemyActionDef(
          'set_spear', '穂先を据える', BattleAttribute.Stance, 2,
          new Face({ Stance: new StanceDef(StanceHook.AttackBonus, StanceWhen.GapAtLeast, { Threshold: 1, Power: 3 }) }),
          { Targets: TargetKind.Self, Description: '構え: 相手との間合いが 1 以上のとき攻撃威力 +3' }),
        new EnemyActionDef(
          'long_thrust', '長穂の突き', BattleAttribute.Attack, 2,
          new Face({ Power: 8, Reach: new Reach(1, 3) }),
          new Trait(TraitCondition.Combo, TraitEffect.PowerBonus, 3, { Attribute: BattleAttribute.Skill }),
          { Description: '間合い 1〜3 に届く。連動: 同じフェーズにスキルを出していれば威力 +3' }),
        new EnemyActionDef(
          'haft_shove', '柄で突き放す', BattleAttribute.Skill, 1,
          new Face({ Push: 2, Reach: Reach.Only(0) }),
          {
            Description: '間合い 0 に届く。相手を 2 マス押す',
            Omen: OmenKind.Skill,
          }),
        new EnemyActionDef(
          'hook_in', '穂で掛け寄せる', BattleAttribute.Skill, 1,
          new Face({ Push: -2, Reach: new Reach(3, 5) }),
          {
            Description: '間合い 3〜5 に届く。相手を 2 マス引く',
            Omen: OmenKind.Skill,
          }),
        new EnemyActionDef(
          'plate_guard', '甲で受ける', BattleAttribute.Guard, 1,
          new Face({ Guard: 3 }),
          Reserve(5, TraitEffect.GuardBonus, 3),
          TargetKind.Self,
          'Guard 3。温存: 残 5 以上で Guard +3'),
        new EnemyActionDef(
          'clank_on', '甲を鳴らして進む', BattleAttribute.Guard, 1,
          new Face({ Guard: 2, Move: 1 }),
          { Targets: TargetKind.Self, Description: 'Guard 2、前へ 1' }),
      ],
      actionsPerPhase: Constants.EliteActions,
    });

  // ---- §4〜§6 bosses: two actions a phase, their own words, adaptations and stages ----

  /**
   * roster §4: 大黒蛇 セルク. HP 140, size 2, never moves itself, favours gap 2+. Its words are
   * 瘴気纏い (max stamina −1, up to 2 from the second stage) and 呪縛 (2 stacks: the player's
   * next turn has no own movement). 縛りの言葉 is not taken in a phase in which 呪縛 remains on
   * the player (§4.3). Adaptations turtle / breaker / pusher and the second stage at HP ≤ 70.
   */
  r.MiasmaPriest = Build(
    'miasma_priest', '大黒蛇 セルク', EnemyRank.Boss, {
      maxHp: 140, maxStamina: 14, recovery: 3, size: 2,
      branchAtGapZero: PriestBase().Zero,
      branchAtGapOneToTwo: PriestBase().One,
      branchAtGapThreePlus: PriestBase().Three,
      actions: [
        new EnemyActionDef(
          'ward', '瘴気の帳', BattleAttribute.Stance, 2,
          new Face({ Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtLeast, { Threshold: 2, Guard: 5 }) }),
          { Targets: TargetKind.Self, Description: '構え: 相手との間合いが 2 以上のターン開始に Guard +5' }),
        MiasmaRite(1),
        StaffStrike(0),
        new EnemyActionDef(
          'miasma_bolt', '瘴気の矢', BattleAttribute.Attack, 2,
          new Face({ Power: 8, Reach: new Reach(1, 4), Statuses: [Foe(StatusKind.Fatigue, 1)] }),
          AtLeast(3, TraitEffect.PowerBonus, 7),
          { Description: '間合い 1〜4 に届く。疲労を 1 付与する。間合い 3 以上: 威力 +7' }),
        new EnemyActionDef(
          'push_back', '払いのけ', BattleAttribute.Attack, 2,
          new Face({ Power: 5, Push: 2, Reach: Reach.Only(0) }),
          When(TraitCondition.Unguarded, TraitEffect.PowerBonus, 5),
          { Description: '間合い 0 に届く。相手を 2 マス押す。無防備: 相手の Guard が 0 なら威力 +5' }),
        new EnemyActionDef(
          'binding_word', '縛りの言葉', BattleAttribute.Skill, 1,
          new Face({ Reach: new Reach(2, 4), Statuses: [Foe(StatusKind.Binding, 2), Foe(StatusKind.Intimidate, 1)] }),
          {
            Description: '間合い 2〜4 に届く。呪縛を 2 付与する。威圧を 1 付与する。呪縛が残っているあいだは使わない',
            NotWhileFoeHas: StatusKind.Binding,
          }),
        new EnemyActionDef(
          'coil', 'とぐろを締める', BattleAttribute.Guard, 1,
          new Face({ Guard: 3 }),
          { Targets: TargetKind.Self, Description: 'Guard 3' }),
      ],
      actionsPerPhase: Constants.EliteActions,
      switches: Layered(
        PriestBase(), {
          adaptations: [
            // 「アタックを 3 回続けて受けると、とぐろを締めて帳を張り直す」: the next phase is
            // とぐろ → 帳 (or 瘴気の矢 once 帳 is spent), every band, for one phase.
            new Adaptation('turtle', (view) => RunCompletedLastTurn(view, 3),
              (tree) => Tree.All('coil', 'ward', 'miasma_bolt')),
            // 「Guard を 9 以上で 2 ターン受け続けると、崩しを狙う」: 錫杖 with 崩し 2, on top of
            // the 0 and 1〜2 bands only (v4.18, #497: it reaches 0〜1, so on top of 3+ it was
            // chosen out of reach every phase), until the player ends a turn on 5 or less.
            new Adaptation('breaker', (view) => GuardSiege(view),
              (tree) => tree.PrefixNear('staff_strike'), [StaffStrike(2)]),
            // 「隣に 2 ターン居続けると、払いのけで押し返す」: 払いのけ on top of every band
            // until N is 1 or more.
            new Adaptation('pusher', (view) => Lingered(view, (gap) => gap === 0, (gap) => gap >= 1),
              (tree) => tree.Prefix('push_back')),
          ],
          stages: [
            // 「HP が半分を切ると、儀式を毎フェーズ行う」: 瘴気の儀 on top, 瘴気纏い up to 2.
            new Stage('second_stage', (view) => view.Hp <= 70,
              (tree) => tree.Prefix('miasma_rite'), [MiasmaRite(Statuses.BossWordStackMax)]),
          ],
        }),
    });

  /**
   * roster §5: 獄竜 ガルド. HP 180, size 2, favours gap 0: hooks a far player and hauls them in.
   * Its words are 鉤爪 (a move back stopped, one stack spent each time) and 深み (the recovery
   * −1 a stack when a turn starts adjacent). Adaptations bait / snap / pin and the second stage
   * at HP ≤ 90, which steps 2 forward, drops 糸を緩める and hooks 2 at a time.
   */
  r.AbyssAngler = Build(
    'abyss_angler', '獄竜 ガルド', EnemyRank.Boss, {
      maxHp: 180, maxStamina: 14, recovery: 3, size: 2,
      branchAtGapZero: AnglerBase().Zero,
      branchAtGapOneToTwo: AnglerBase().One,
      branchAtGapThreePlus: AnglerBase().Three,
      actions: [
        HookCast(1),
        new EnemyActionDef(
          'drown', '引きずり込む', BattleAttribute.Attack, 2,
          new Face({ Power: 8 }),
          FoeHas(StatusKind.Hook, TraitEffect.PowerBonus, 3),
          { Description: '間合い 0〜1 に届く。相手の状態（鉤爪）: 威力 +3' }),
        new EnemyActionDef(
          'fathom_call', '深みへの呼び声', BattleAttribute.Skill, 2,
          new Face({ Statuses: [Foe(StatusKind.Depths, 1, Statuses.BossWordStackMax), Self(StatusKind.Regen, 1)] }),
          { Description: '間合い 0〜1 に届く。深みを 1 付与する。自分に再生を 1 付与する' }),
        new EnemyActionDef(
          'line_whip', '糸の一打', BattleAttribute.Attack, 2,
          new Face({ Power: 8, Reach: new Reach(0, 2) }),
          FoeHas(StatusKind.Slow, TraitEffect.PowerBonus, 3),
          { Description: '間合い 0〜2 に届く。相手の状態（鈍足）: 威力 +3' }),
        new EnemyActionDef(
          'deep_water', '淀みを張る', BattleAttribute.Stance, 2,
          new Face({ Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtMost, { Threshold: 1, Guard: 5 }) }),
          { Targets: TargetKind.Self, Description: '構え: 相手との間合いが 1 以下のターン開始に Guard +5' }),
        new EnemyActionDef(
          'reel_in', '手繰り寄せる', BattleAttribute.Skill, 1,
          new Face({ Push: -2, Reach: new Reach(1, 4), Statuses: [Foe(StatusKind.Slow, SlowStacks())] }),
          {
            Description: '間合い 1〜4 に届く。相手を 2 マス引き、鈍足を 2 付与する',
            Omen: OmenKind.Skill,
          }),
        new EnemyActionDef(
          'slack_line', '糸を緩める', BattleAttribute.Guard, 1,
          new Face({ Guard: 2, Move: -1 }),
          { Targets: TargetKind.Self, Description: 'Guard 2、後ろへ 1' }),
      ],
      actionsPerPhase: Constants.EliteActions,
      switches: Layered(
        AnglerBase(), {
          adaptations: [
            // 「2 マス以上離れて 2 ターン居続けると、鉤縄と手繰り寄せを続ける」: 1〜2 and 3+
            // become 鉤縄 → 手繰り寄せる, until N is 0.
            new Adaptation('bait', (view) => Lingered(view, (gap) => gap >= 2, (gap) => gap === 0),
              (tree) => tree.with({ One: ['hook_cast', 'reel_in'], Three: ['hook_cast', 'reel_in'] })),
            // 「鉤爪を 2 回外されると、糸を緩めて張り直す」: every second stack a stopped move
            // back spent; the next phase is 糸を緩める → 深みへの呼び声.
            new Adaptation('snap', (view) => EverySecondInLastTurn(view, view.Player.HookSnags),
              (tree) => Tree.All('slack_line', 'fathom_call')),
            // 「隣で 2 ターン粘ると、引きずり込みを続ける」: 0 becomes 引きずり込む → 糸の一打
            // until N is 1 or more.
            new Adaptation('pin', (view) => Lingered(view, (gap) => gap === 0, (gap) => gap >= 1),
              (tree) => tree.with({ Zero: ['drown', 'line_whip'] })),
          ],
          stages: [
            // 「HP が半分を切ると、自分から淵を出て食い込む」: 前へ 2 once, no more 糸を緩める,
            // and 鉤縄 puts 鉤爪 2 on at once.
            new Stage('second_stage', (view) => view.Hp <= 90,
              (tree) => tree, [HookCast(2)], { EnterMove: 2, Without: ['slack_line'] }),
          ],
        }),
    });

  /**
   * roster §6: 歪みの根. HP 200, size 2, never moves on its own, favours gap 2+, three stages. Its
   * words are 根張り (ending two turns on one cell costs 4 HP a stack) and 枯らし (the recovery
   * −1 a stack). 伸びる根 pushes a player at gap 1 or less and pulls one at 2 or more. The five
   * adaptations (root_a / root_g / root_st / root_sk / root_m) work through all three stages.
   */
  r.DistortionRoot = Build(
    'distortion_root', '歪みの根', EnemyRank.Boss, {
      maxHp: 200, maxStamina: 14, recovery: 3, size: 2,
      branchAtGapZero: RootBase().Zero,
      branchAtGapOneToTwo: RootBase().One,
      branchAtGapThreePlus: RootBase().Three,
      actions: [
        new EnemyActionDef(
          'sweep', '根の横払い', BattleAttribute.Attack, 2,
          new Face({ Power: 8, Reach: new Reach(0, 2) }),
          FoeHas(StatusKind.Rooting, TraitEffect.PowerBonus, 5),
          { Description: '間合い 0〜2 に届く。相手の状態（根張り）: 威力 +5' }),
        new EnemyActionDef(
          'thorn_volley', '棘を飛ばす', BattleAttribute.Attack, 2,
          new Face({ Power: 8, Reach: new Reach(1, 4), Statuses: [Foe(StatusKind.Bleed, 1)] }),
          AtLeast(3, TraitEffect.PowerBonus, 5),
          { Description: '間合い 1〜4 に届く。出血を 1 付与する。間合い 3 以上: 威力 +5' }),
        RootWitherBreath(),
        RootGrip(Reach.Default, 1),
        Twist(1),
        new EnemyActionDef(
          'bark', '樹皮', BattleAttribute.Stance, 2,
          new Face({ Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtLeast, { Threshold: 2, Guard: 5 }) }),
          { Targets: TargetKind.Self, Description: '構え: 相手との間合いが 2 以上のターン開始に Guard +5' }),
        CreepingRoot(null),
      ],
      actionsPerPhase: Constants.EliteActions,
      switches: Layered(
        RootBase(), {
          adaptations: [
            // root_a 「アタックを 4 回続けて受けると、根を張って足を止める」.
            new Adaptation('root_a', (view) => RunCompletedLastTurn(view, 4),
              (tree) => Tree.All('root_grip', 'twist', 'creeping_root')),
            // root_g 「Guard で 2 ターン受け切ると、崩しに回る」: 捻じる with 崩し 2, on top.
            new Adaptation('root_g', (view) => GuardSiege(view),
              (tree) => tree.Prefix('twist'), [Twist(2)]),
            // root_st 「スタンスを置くと、その構えを枯らしにくる」: 枯らしの息 first, and when
            // it lands 「プレイヤーがいちばん新しく置いた永続の効果 1 つが 1 ターン働かなくなる」
            // (#334). The breath is the one of the layer it holds over — a stage's 0〜4 one
            // keeps its reach — with the stop added (Face.StopStance).
            new Adaptation('root_st', (view) => PlacedStanceLastTurn(view),
              (tree) => tree.Prefix('wither_breath'), null,
              { Retouch: (layer) => [StoppingStance(Find(layer, 'wither_breath') ?? RootWitherBreath())] }),
            // root_sk 「状態を 2 つ付けられると、身を固めて振り払う」: 樹皮 → 根の横払い, and
            // every word on the root is shaken off.
            new Adaptation('root_sk', (view) => EverySecondInLastTurn(view, view.Player.Inflictions),
              (tree) => Tree.All('bark', 'sweep', 'creeping_root'), null, { Cleanse: true }),
            // root_m 「2 ターン続けて動くと、先回りする」: 伸びる根 first, moving the player
            // against the way they last went — in (toward the root) is answered by a push,
            // out by a pull.
            new Adaptation('root_m_in', (view) => MovedTwiceLastTurn(view) && LastStep(view) > 0,
              (tree) => tree.Prefix('creeping_root'), [CreepingRoot(1)]),
            new Adaptation('root_m_out', (view) => MovedTwiceLastTurn(view) && LastStep(view) < 0,
              (tree) => tree.Prefix('creeping_root'), [CreepingRoot(-1)]),
          ],
          stages: [
            // stage_2 「HP が 3 分の 2 を切ると、せり上がって力を枯らす」: 前へ 1 once; 枯らしの息
            // reaches 0〜4, goes on top, and 枯らし stacks to 2.
            new Stage('stage_2', (view) => view.Hp <= 133,
              (tree) => tree.Prefix('wither_breath'),
              [WitherBreath(new Reach(0, 4), Statuses.BossWordStackMax)],
              { EnterMove: 1 }),
            // stage_3 「HP が 3 分の 1 を切ると、根と息を同じフェーズで使う」: the body sways each
            // phase; 根を張る also reaches 0〜4 and 根張り stacks to 2; every band leads with 根を張る,
            // and the second move is 根の横払い at 0〜2 but 枯らしの息 at 3+, where the sweep
            // (0〜2) would miss (roster v4.15, #396). The roster names only those two moves; the
            // rest of each band is ours: 0〜2 keep the breath and 伸びる根 behind the sweep, and 3+
            // ends on 伸びる根 (#446, handed to cards to write into the roster).
            new Stage('stage_3', (view) => view.Hp <= 66,
              (tree) => new Tree(
                ['root_grip', 'sweep', 'wither_breath', 'creeping_root'],
                ['root_grip', 'sweep', 'wither_breath', 'creeping_root'],
                ['root_grip', 'wither_breath', 'creeping_root']),
              [
                WitherBreath(new Reach(0, 4), Statuses.BossWordStackMax),
                RootGrip(new Reach(0, 4), Statuses.BossWordStackMax),
              ],
              { Sway: true }),
          ],
        }),
    });

  /**
   * The enemy actions that break the multi-hit rule (v4.4: Cards.ValidateEnemyAction).
   * Empty since #51 put 二段斬り on v4.5's 7 × 2 with no status; kept so the check has a list to
   * hold the roster against. (C# IReadOnlyCollection<string> over a HashSet → a Set.)
   */
  r.MultiHitRedesignPending = new Set();

  /** Every enemy the core knows, in roster order: seven normal, three elite, three bosses. */
  r.All = Object.freeze([
    r.PolearmWarped, r.ShadowHound, r.RustedRevenant, r.CrossbowHunter, r.MistArcher, r.TwinBladeWarped, r.PolearmCrystal,
    r.ArmoredWarden, r.PackAlpha, r.PolearmUnyielding,
    r.MiasmaPriest, r.AbyssAngler, r.DistortionRoot,
  ]);

  roster = r;
  return roster;
}

/**
 * C# `public static class Enemies`. The static readonly members are read-only getters over the
 * roster built on first read (see the header).
 */
export const Enemies = Object.freeze({
  /** The roster id. Ids never change (roster §1.8); names do. */
  PolearmWarpedId,

  // §2 normal
  get PolearmWarped() { return Roster().PolearmWarped; },
  get ShadowHound() { return Roster().ShadowHound; },
  get RustedRevenant() { return Roster().RustedRevenant; },
  get CrossbowHunter() { return Roster().CrossbowHunter; },
  get MistArcher() { return Roster().MistArcher; },
  get TwinBladeWarped() { return Roster().TwinBladeWarped; },
  get PolearmCrystal() { return Roster().PolearmCrystal; },
  // §3 elite
  get ArmoredWarden() { return Roster().ArmoredWarden; },
  get PackAlpha() { return Roster().PackAlpha; },
  get PolearmUnyielding() { return Roster().PolearmUnyielding; },
  // §4〜§6 bosses
  get MiasmaPriest() { return Roster().MiasmaPriest; },
  get AbyssAngler() { return Roster().AbyssAngler; },
  get DistortionRoot() { return Roster().DistortionRoot; },

  /** The enemy actions that break the multi-hit rule; empty since #51. */
  get MultiHitRedesignPending() { return Roster().MultiHitRedesignPending; },

  /** Every enemy the core knows, in roster order: seven normal, three elite, three bosses. */
  get All() { return Roster().All; },

  ById(id) {
    if (id == null) throw argumentNull('id');
    for (const def of Enemies.All) {
      if (def.Id === id) return def; // string.Equals(def.Id, id, StringComparison.Ordinal)
    }
    throw new KeyNotFoundException(`Unknown enemy id "${id}".`);
  },
});
