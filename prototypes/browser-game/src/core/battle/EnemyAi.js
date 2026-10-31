// Port of unity-port/BattleCore/EnemyAi.cs
//
// §6 / §6.1 and roster §1.2: how an enemy picks its next action and what it shows of it.
//
// There is no randomness here. The tree is walked top-down and the first action that can be paid
// for wins, so the same board always gives the same omen.
//
// The C# extension method `OmenLabel.ToText(this OmenLabel label)` is EnemyAi.ToText(label) here.
// `IReadOnlyDictionary<string, EnemyActionDef> Actions` is a Map (docs/CONVENTIONS.md).

import {
  hasFlag,
  ArgumentNullException,
  ArgumentOutOfRangeException,
  KeyNotFoundException,
} from '../../shared/cs.js';
import {
  BattleAttribute,
  TargetKind,
  OmenKind,
  StatusKind,
  StatusDecay,
  TraitCondition,
  OmenLabel,
  Omen,
  GapBoundary,
  EnumTokens,
} from './Types.js';
import { Constants } from './Constants.js';
import { Combat } from './Combat.js';
import { Field } from './Field.js';
import { Statuses, StatusSet } from './Statuses.js';
import { AttributeRule } from './Attributes.js';

/** new ArgumentNullException(nameof(name)).Message */
function nullArg(name) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${name}')`);
}

/** C# `dictionary[key]` on IReadOnlyDictionary<string, EnemyActionDef> (a Map): throws KeyNotFoundException when missing. */
function dictGet(dict, key) {
  if (!dict.has(key)) throw new KeyNotFoundException(`The given key '${key}' was not present in the dictionary.`);
  return dict.get(key);
}

/** The stacks of word the player holds when the action is taken (see Barred). */
function Left(foe, word, beforeFoeTurn) {
  return foe.Stacks(word) - (beforeFoeTurn && Statuses.DecayOf(word) === StatusDecay.OnTurn ? 1 : 0);
}

/**
 * #497: every capped word the action puts on the player is held at the cap. An action with no
 * capped grant on the player is never at it.
 */
function AtFoeCap(action, foe, beforeFoeTurn, warded) {
  let capped = false;
  for (const grant of action.Face.StatusList) {
    if (grant.OnSelf || grant.Cap <= 0) continue;
    capped = true;
    if (Left(foe, grant.Kind, beforeFoeTurn) + warded.Stacks(grant.Kind) < grant.Cap) return false;
  }
  return capped;
}

function Contains(ids, id) {
  for (const each of ids) {
    if (each === id) return true;
  }
  return false;
}

let restLabel = null;

export const EnemyAi = {
  /** The action id an omen carries when nothing in the branch can be paid for. */
  RestActionId: 'rest',

  /** static readonly: one instance, built on first read (no Types.js read at module-evaluation time). */
  get RestLabel() {
    if (restLabel === null) restLabel = new OmenLabel(OmenKind.Rest);
    return restLabel;
  },

  /**
   * §6.1: every enemy branches three ways on the gap band at the moment the omen is decided.
   * active is the boss's swapped tree (#50): a branch it names replaces the base one, a band it
   * leaves null keeps the base branch.
   */
  BranchFor(def, gap, active = null) {
    if (def == null) throw nullArg('def');
    const band = EnumTokens.ToBand(gap);
    return (active != null ? active.Branch(band) : null) ?? def.Branch(band);
  },

  /**
   * #50 the hook: the switch that holds now, or null for the base tree. Of the switches that hold
   * the last in the enemy's list wins (a later stage overrides an earlier one), and one that stops
   * holding lets the base tree back, so this is asked at every omen decision.
   */
  ActiveSwitch(def, view) {
    if (def == null) throw nullArg('def');
    if (view == null) throw nullArg('view');
    let found = null;
    if (def.Switches == null) return null;
    for (const each of def.Switches) {
      if (each.Holds(view)) found = each;
    }
    return found;
  },

  /** The switch of this id, or null (also for a null id). */
  SwitchById(def, id) {
    if (def == null) throw nullArg('def');
    if (id == null || def.Switches == null) return null;
    for (const each of def.Switches) {
      if (each.Id === id) return each;
    }
    return null;
  },

  /**
   * roster §1.2: walk the branch from the top and take the first action whose cost fits in
   * stamina. Null when none does — the enemy rests.
   *
   * The caller decides which stamina to pass. The turn loop passes what the enemy will hold when
   * the omen is carried out (after its recovery), so an omen only turns into a rest when something
   * drained the enemy in between.
   *
   * skip are actions it may not take now (#189): stances it has used — a stance action is used
   * once a battle and then leaves the tree (roster §1.2) — and, for the second action of an elite
   * or a boss, the first one (roster §1.3).
   */
  ChooseAction(def, gap, stamina, skip = null, active = null) {
    for (const actionId of EnemyAi.BranchFor(def, gap, active)) {
      if (skip != null && Contains(skip, actionId)) continue;
      const action = EnemyAi.ActionOf(def, actionId, active);
      if (Combat.CanPay(action.Cost, stamina)) return action;
    }
    return null;
  },

  /**
   * §9 step 12: the stamina an enemy will hold when its omen is carried out — now, plus its next
   * recovery. 疲労 it already holds will take its 1 off that recovery too (§9 step 9); one put on
   * it later is what "drained in between" means. The turn loop passes this to DecideOmen and
   * DecidePlan; it is public so a test that walks the trees (#395) reads the omen's stamina the
   * same way.
   */
  StaminaAtAction(def, body) {
    if (def == null) throw nullArg('def');
    if (body == null) throw nullArg('body');
    const fatigue = body.Statuses.Has(StatusKind.Fatigue) ? Constants.FatiguePenalty : 0;
    return Combat.RecoverStamina(body.Stamina, body.MaxStamina, def.Recovery - fatigue, body.NextTurnRecoveryBonus);
  },

  /**
   * #51: the enemy's action of this id as it stands under the active switch — the switch's
   * override when it carries one (roster §4.4 breaker: 錫杖 with 崩し 2), else the enemy's own.
   */
  ActionOf(def, actionId, active = null) {
    if (def == null) throw nullArg('def');
    return (active != null ? active.Override(actionId) : null) ?? dictGet(def.Actions, actionId);
  },

  /**
   * #51 (roster §4.3): the actions the tree passes over because the player holds the word the
   * action waits out (EnemyActionDef.NotWhileFoeHas: 縛りの言葉 while 呪縛 remains). beforeFoeTurn
   * is true for an omen decided at step 12, which is taken after the player's next turn start: a
   * ターンで減る型 word loses a stack there, so it counts as remaining only if a stack is left after
   * that tick. False for the second action of a phase, read with nothing in between.
   *
   * #497 (roster v4.18 §4.3): also the actions that would put nothing more on the player
   * (EnemyActionDef.NotAtFoeCap: 瘴気の儀 once 瘴気纏い is at its cap), read off the action as it
   * stands under active (the second stage's cap of 2). warded is what 竜語の符 has kept off (§9.1,
   * #452): the word counts as it would sit without the ward, so the ward lowers the word and leaves
   * the choice as it is.
   */
  Barred(def, foe, beforeFoeTurn, active = null, warded = null) {
    if (def == null) throw nullArg('def');
    if (foe == null) throw nullArg('foe');
    const barred = [];
    for (const own of def.Actions.values()) {
      const action = EnemyAi.ActionOf(def, own.Id, active);
      if (action.NotWhileFoeHas != null && Left(foe, action.NotWhileFoeHas, beforeFoeTurn) > 0) {
        barred.push(action.Id);
      } else if (action.NotAtFoeCap && AtFoeCap(action, foe, beforeFoeTurn, warded ?? StatusSet.Empty)) {
        barred.push(action.Id);
      }
    }
    return barred;
  },

  /**
   * #51 (roster §6.3 伸びる根): the face an action resolves with at gap gap. An action with
   * EnemyActionDef.PullBeyond pushes |Push| cells at that gap or nearer and pulls |Push| cells
   * beyond it; every other action's face is its own.
   */
  FaceAt(action, gap) {
    if (action == null) throw nullArg('action');
    if (action.PullBeyond == null || action.Face.Push === 0) return action.Face;
    const cells = Math.abs(action.Face.Push);
    return action.Face.with({ Push: gap <= action.PullBeyond ? cells : -cells });
  },

  /**
   * §6: the omen for the next action — 種別 + 狙うマス. An elite or a boss shows a second step
   * beside it, DecidePlan.
   */
  DecideOmen(def, gap, stamina, skip = null, active = null) {
    const action = EnemyAi.ChooseAction(def, gap, stamina, skip, active);
    return EnemyAi.OmenOf(action);
  },

  /** The omen an action carries, or the rest omen for none. */
  OmenOf(action) {
    return action == null ? new Omen(EnemyAi.RestActionId, EnemyAi.RestLabel) : new Omen(action.Id, EnemyAi.LabelOf(action));
  },

  /**
   * §9 step 12 / §17.6 F11 (#50): the 予定 of an elite or a boss — the action it would take second,
   * chosen from the tree as it stands now with the first action's cost already paid and the first
   * action out of the running (roster §1.3). Null when it takes only one action a phase, or when
   * the first omen is a rest (nothing follows a rest). It is not a commitment: the tree is read
   * again after the first action (TurnLoop), and a different answer is a 予定変更.
   */
  DecidePlan(def, gap, stamina, first, skip = null, active = null) {
    if (def == null) throw nullArg('def');
    if (first == null) throw nullArg('first');
    if (def.ActionsPerPhase < 2 || first.ActionId === EnemyAi.RestActionId) return null;

    const firstAction = EnemyAi.ActionOf(def, first.ActionId, active);
    const second = [];
    if (skip != null) second.push(...skip);
    second.push(first.ActionId);
    return EnemyAi.OmenOf(EnemyAi.ChooseAction(def, gap, stamina - firstAction.Cost, second, active));
  },

  /**
   * §6: the omen is a commitment. The declared action is carried out as shown, and when it can no
   * longer be paid for the enemy rests instead of picking something cheaper.
   */
  ActionToExecute(def, omen, stamina, active = null) {
    if (def == null) throw nullArg('def');
    if (omen == null) throw nullArg('omen');
    if (omen.ActionId === EnemyAi.RestActionId) return null;

    const action = EnemyAi.ActionOf(def, omen.ActionId, active);
    return Combat.CanPay(action.Cost, stamina) ? action : null;
  },

  /**
   * §5 俊敏 (#48): the free cell an enemy holding 俊敏 takes at its turn start (§9 step 9), as +1
   * (forward, toward the player), −1 (back) or 0 (it stays). It serves the omen it is about to
   * carry out: an action aimed at the player whose reach the gap falls outside steps one cell
   * toward that reach. An action already in reach, one aimed at nobody, and a rest (action null)
   * stay. Nothing random: the player can read it off the omen.
   */
  FreeStepDirection(action, gap) {
    if (action == null) return 0;
    if (!EnemyAi.IsOpponentDirected(action.Attributes, action.Face, action.Targets)) return 0;
    const reach = action.Face.ReachOrDefault;
    if (gap > reach.Max) return 1;
    if (gap < reach.Min) return -1;
    return 0;
  },

  /**
   * §2.4 / §6: whether a face is aimed at the opponent, which is what a reach applies to — an
   * attack, a status put on the opponent, a push / pull, or 崩し. A Self-targeted action reads no
   * reach even when it carries those rows. One and All (§7.4) both aim at the opponent.
   */
  IsOpponentDirected(attributes, face, targets) {
    if (face == null) throw nullArg('face');
    if (targets === TargetKind.Self) return false;
    return hasFlag(attributes, BattleAttribute.Attack) || face.GivesFoeStatus || face.Push !== 0 || face.Break > 0;
  },

  /**
   * §6: the omen label, read off the action. The kind is the first attribute in the order Attack,
   * Move, Guard, Skill, Stance — the movement effect (not an attribute, §2.1) is put before a guard
   * here (unlike the card's colour role, CoreText.KindOf) because where the enemy will stand is
   * what the player has to read. The reach is the action's own when it has an opponent-directed
   * face, else null.
   */
  LabelOf(action) {
    if (action == null) throw nullArg('action');
    // The roster's 予兆 column wins where the attribute order would read it otherwise (#189:
    // 盾を掲げて前進 is 防御, 追い立てる is 技 — the same faces as moves that read 移動).
    const kind =
      action.Omen != null ? action.Omen
        : hasFlag(action.Attributes, BattleAttribute.Attack) ? OmenKind.Attack
          : AttributeRule.HasMovement(action.Face) ? OmenKind.Move
            : hasFlag(action.Attributes, BattleAttribute.Guard) ? OmenKind.Guard
              : hasFlag(action.Attributes, BattleAttribute.Skill) ? OmenKind.Skill
                : OmenKind.Stance;
    const reach = EnemyAi.IsOpponentDirected(action.Attributes, action.Face, action.Targets)
      ? action.Face.ReachOrDefault
      : null;
    return new OmenLabel(kind, reach);
  },

  /**
   * §6: the cells an omen aims at, counted from the enemy's near edge right now (the player side
   * of the line). Empty for an action that aims at nobody, or when nothing it reaches is on the
   * line. Lowest cell first.
   */
  TargetCells(label, enemy) {
    if (label == null) throw nullArg('label');
    if (enemy == null) throw nullArg('enemy');
    const cells = [];
    if (label.Reach == null) return cells;
    for (let gap = label.Reach.Max; gap >= label.Reach.Min; gap--) {
      const cell = Field.FrontCellAt(gap, enemy.Cell);
      if (cell >= 1) cells.push(cell);
    }
    return cells;
  },

  /**
   * §6 「行動が間合いの境目を読むとき」: the gap boundary action's trait reads (間合い n 以上 /
   * 間合い n 以下), or null for an action whose trait reads none.
   */
  BoundaryOf(action) {
    if (action == null) throw nullArg('action');
    const trait = action.Trait;
    if (trait == null) return null;
    switch (trait.Condition) {
      case TraitCondition.GapAtLeast: return new GapBoundary(true, trait.Threshold);
      case TraitCondition.GapAtMost: return new GapBoundary(false, trait.Threshold);
      default: return null;
    }
  },

  /**
   * §6: 境目を満たすマス — of the cells action aims at from where enemy stands (TargetCells), the
   * ones whose gap meets the boundary its trait reads. A player standing on one would meet the
   * trait when the action is carried out from there. Empty when the action reads no boundary or
   * aims at nobody. Lowest cell first.
   */
  BoundaryCells(action, enemy) {
    if (action == null) throw nullArg('action');
    if (enemy == null) throw nullArg('enemy');
    const cells = [];
    const boundary = EnemyAi.BoundaryOf(action);
    if (boundary == null) return cells;
    for (const cell of EnemyAi.TargetCells(EnemyAi.LabelOf(action), enemy)) {
      // TargetCells counts a cell as Field.FrontCellAt(gap, …); read the gap back off it.
      if (boundary.Holds(Field.GapFrom(cell, enemy.Cell))) cells.push(cell);
    }
    return cells;
  },

  /** §6: the omen as the screen words it — "攻撃・1〜2", "防御", "休み" (battle-visual-v1 4.4, #357). C# extension method on OmenLabel. */
  ToText(label) {
    if (label == null) throw nullArg('label');
    let kind;
    switch (label.Kind) {
      case OmenKind.Attack: kind = '攻撃'; break;
      case OmenKind.Guard: kind = '防御'; break;
      case OmenKind.Move: kind = '移動'; break;
      case OmenKind.Skill: kind = '技'; break;
      case OmenKind.Stance: kind = '構え'; break;
      case OmenKind.Rest: kind = '休み'; break;
      default:
        throw new ArgumentOutOfRangeException(
          `Specified argument was out of the range of valid values. (Parameter 'label')\nActual value was ${label.Kind}.`);
    }
    return label.Reach != null ? `${kind}・${label.Reach.ToText()}` : kind;
  },
};
