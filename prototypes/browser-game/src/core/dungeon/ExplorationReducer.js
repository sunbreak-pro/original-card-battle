// Port of unity-port/DungeonCore/ExplorationReducer.cs
import { roundAway } from '../../shared/cs.js';
import { argException, argNull } from './_errors.js';
import { NodeKind } from './NodeKind.js';
import { Miasma } from './Miasma.js';
import { RunPhase, RestChoice, ExplorationState } from './ExplorationState.js';
import { ReliefKind } from './RunLoadout.js';
import { EventKind, LayerEvents } from './LayerEvents.js';

/** Raised when a caller asks for a move the rules do not allow. */
export class ExplorationRuleException extends Error {
  constructor(message) {
    super(message);
    this.name = 'ExplorationRuleException';
  }
}

/**
 * The two exploration resources, and nothing else. 刻限 says what gets done inside a
 * layer; 瘴気 says how long the life lasts. They are kept apart on purpose — spending a
 * 刻限 is the only thing that moves the gauge, and the gauge never gives 刻限 back
 * (concept-v3.md §6).
 */
export const ExplorationReducer = {
  /** 休息: HP back by this share of the maximum (concept-v3.md §7). */
  RestHpPercent: 15,

  /** 階層間の休憩: HP back by this share (concept-v3.md §7). */
  InterludeHpPercent: 30,

  /** 休息 also lifts max stamina (concept-v3.md §7). Clamped with the other temporaries. */
  RestMaxStaminaBonus: 2,

  /** HP a 休息 gives back at this maximum, before the maximum clamps it. The screen prints this. */
  RestHeal(maxHp) { return Share(maxHp, ExplorationReducer.RestHpPercent); },

  /** HP 階層間の休憩 gives back at this maximum, before the maximum clamps it. */
  InterludeHeal(maxHp) { return Share(maxHp, ExplorationReducer.InterludeHpPercent); },

  /**
   * Walks into a layer. The entry node is resolved on arrival and costs its 刻限.
   * JS extension: `trial` (default false = the C#) turns the trial rules on for the run — 刻限の砂's
   * 刻限 is added here (tools_and_consumables_v4.md §2) and the state carries the flag to every later call.
   */
  Enter(profile, map, loadout, hp, maxHp, stamina, miasmaPercent = 0, tempMaxStaminaMod = 0, trial = false) {
    if (profile === null || profile === undefined) throw argNull('profile');
    if (map === null || map === undefined) throw argNull('map');
    if (loadout === null || loadout === undefined) throw argNull('loadout');
    if (profile.Layer !== map.Layer)
      throw argException(`layer ${profile.Layer} was handed the map of layer ${map.Layer}`, 'map');

    const fresh = new ExplorationState(
      profile,
      map,
      map.EntryId,
      new Set(),
      profile.TimeLimit + (trial === true ? loadout.TimeBonus : 0),
      miasmaPercent,
      hp,
      maxHp,
      stamina,
      tempMaxStaminaMod,
      /* trainingMarks: */ 0,
      loadout,
      RunPhase.Exploring,
      trial === true);

    return Resolve(fresh, map.EntryId, RestChoice.Rest);
  },

  /**
   * Moves to a neighbouring node. Stepping back over a node that is already resolved is
   * free; entering a new one costs one 刻限 and one layer's worth of 瘴気.
   */
  Step(state, nodeId, choice = RestChoice.Rest) {
    if (!ExplorationReducer.CanStep(state, nodeId)) throw new ExplorationRuleException(WhyNoStep(state, nodeId));
    if (state.HasResolved(nodeId)) return state.With({ currentNodeId: nodeId });
    return Resolve(state, nodeId, choice);
  },

  /**
   * Whether Step takes this node: the layer is being explored, the node touches
   * the one the player stands on, and it is either already resolved or a 刻限 is left to enter it.
   * The screen asks this before it offers a step.
   */
  CanStep(state, nodeId) {
    if (state === null || state === undefined) throw argNull('state');
    return state.Phase === RunPhase.Exploring
      && state.Map.Neighbours(state.CurrentNodeId).includes(nodeId)
      && (state.HasResolved(nodeId) || state.TimeLeft > 0);
  },

  /**
   * Burns one consumable. Costs no 刻限, so it never moves the gauge upward.
   * JS extension (trial mode, tools_and_consumables_v4.md §3): every consumable acts — 薬草 gives back
   * Amount% of the HP maximum, 濃い干し肉 raises max stamina by Amount inside the ±4 of the temporary
   * changes, 忘れ水 fills current stamina, 浄化の香 pulls the gauge back as in the C#. 標の石 is spent by
   * Teleport, which needs the node it jumps to. Outside trial only the gauge moves, as in the C#.
   */
  UseConsumable(state, index) {
    if (state === null || state === undefined) throw argNull('state');
    if (state.IsOver)
      throw new ExplorationRuleException('the run is over');

    const { value: loadout, gaugeRelief: relief, item } = state.Loadout.Spend(index);
    if (!state.Trial) {
      return state.With({
        loadout,
        miasmaPercent: Miasma.Relieve(state.MiasmaPercent, relief),
      });
    }
    switch (item.Kind) {
      case ReliefKind.Heal:
        return state.With({ loadout, hp: Math.min(state.MaxHp, state.Hp + Share(state.MaxHp, item.Amount)) });
      case ReliefKind.MaxStamina:
        // 「最大スタミナ +1（±4 の枠内）」: the same temporary change a rest makes (miasma_and_battle_v4.md §5).
        return state.With({ loadout, tempMaxStaminaMod: Clamp(state.TempMaxStaminaMod + item.Amount) });
      case ReliefKind.Refill:
        return state.With({ loadout, stamina: state.MaxStamina });
      case ReliefKind.Teleport:
        throw new ExplorationRuleException('標の石 jumps to a node: use Teleport');
      default:
        return state.With({ loadout, miasmaPercent: Miasma.Relieve(state.MiasmaPercent, relief) });
    }
  },

  /**
   * JS extension (trial mode): 標の石 — 「同じ層の未踏ノードへ 1 度だけ跳ぶ」, and 「跳んだ先のノードは新しい
   * ノードなので、普通に刻限 1 と瘴気がかかります」 (tools_and_consumables_v4.md §3). The item in `index` is
   * spent and `nodeId` is resolved exactly as a step into it would be (a rest takes `choice`).
   */
  Teleport(state, index, nodeId, choice = RestChoice.Rest) {
    if (state === null || state === undefined) throw argNull('state');
    if (!state.Trial) throw new ExplorationRuleException('標の石 acts in trial mode only');
    const item = state.Loadout.Consumables[index];
    if (item === undefined || item.Kind !== ReliefKind.Teleport)
      throw new ExplorationRuleException('no 標の石 in that slot');
    if (!ExplorationReducer.CanTeleport(state, nodeId))
      throw new ExplorationRuleException(`no jump to node ${nodeId}`);
    const { value: loadout } = state.Loadout.Spend(index);
    return Resolve(state.With({ loadout }), nodeId, choice);
  },

  /**
   * JS extension: whether 標の石 can land on this node — the layer is being explored, a 刻限 is left to
   * pay, and the node is an unresolved node of this layer (any of them, the boss included: §3 names no
   * exception — 試作で決めたこと).
   */
  CanTeleport(state, nodeId) {
    if (state === null || state === undefined) throw argNull('state');
    return state.Trial
      && state.Phase === RunPhase.Exploring
      && state.TimeLeft > 0
      && state.Map.Nodes.some((n) => n.Id === nodeId)
      && !state.HasResolved(nodeId);
  },

  /**
   * JS extension (trial mode): an イベント node's effect on the run (encounters_and_rewards_v4.md §5).
   * The node's 刻限 and 瘴気 were paid by the step; this applies what the event does once the player has
   * answered (泉 always 「受ける」):
   * - 泉: HP back by the layer's share of the maximum.
   * - 重い宝箱 受ける: max stamina −2 as a temporary change (inside ±4); current stamina follows the
   *   lower maximum. What it holds is the bridge's (cards, consumables, pages).
   * - 特殊な食事 受ける: max stamina +1 as a temporary change; HP −10% of the maximum, never below 1
   *   (試作で決めたこと: §5 does not say a meal may kill).
   * - 受けない: nothing.
   */
  ResolveEvent(state, kind, accept) {
    if (state === null || state === undefined) throw argNull('state');
    if (!state.Trial) throw new ExplorationRuleException('events resolve in trial mode only');
    if (state.IsOver) throw new ExplorationRuleException('the run is over');
    if (!accept) return state;
    switch (kind) {
      case EventKind.Spring:
        return state.With({ hp: Math.min(state.MaxHp, state.Hp + ExplorationReducer.SpringHeal(state)) });
      case EventKind.HeavyChest: {
        const temp = Clamp(state.TempMaxStaminaMod - LayerEvents.ChestStaminaCost);
        const max = Miasma.MaxStamina(temp, state.MiasmaPercent);
        return state.With({ tempMaxStaminaMod: temp, stamina: Math.min(state.Stamina, max) });
      }
      case EventKind.StrangeMeal: {
        const temp = Clamp(state.TempMaxStaminaMod + LayerEvents.MealStaminaGain);
        const hp = Math.max(1, state.Hp - ExplorationReducer.MealCost(state));
        return state.With({ tempMaxStaminaMod: temp, hp: Math.min(state.Hp, hp) });
      }
      default:
        throw argException(`no such event ${kind}`, 'kind');
    }
  },

  /**
   * JS extension: HP 階層間の休憩 gives back on this state, before the maximum clamps it — 30%, or 45% with 灯芯の欠片
   * in trial mode (InterludePercent). Interlude adds exactly this, up to the maximum.
   */
  InterludeHealOf(state) {
    if (state === null || state === undefined) throw argNull('state');
    return state.Trial ? Share(state.MaxHp, ExplorationReducer.InterludePercent(state)) : ExplorationReducer.InterludeHeal(state.MaxHp);
  },

  /** JS extension: HP a 泉 on this state's layer gives back, before the maximum clamps it. */
  SpringHeal(state) { return Share(state.MaxHp, LayerEvents.SpringPercent(state.Profile.Layer)); },

  /** JS extension: HP a 特殊な食事 takes, before the floor of 1. */
  MealCost(state) { return Share(state.MaxHp, LayerEvents.MealHpPercent); },

  /**
   * JS extension (trial mode): a consumable found on the way goes into the slots — after the others, or
   * in place of `replaceSlot` when the three are full (tools_and_consumables_v4.md §3). Costs no 刻限.
   */
  TakeConsumable(state, item, replaceSlot = -1) {
    if (state === null || state === undefined) throw argNull('state');
    if (state.IsOver) throw new ExplorationRuleException('the run is over');
    return state.With({ loadout: state.Loadout.Take(item, replaceSlot) });
  },

  /**
   * JS extension: the share of the HP maximum 階層間の休憩 gives back on this state — 30%, and 45% with
   * 灯芯の欠片 in trial mode (tools_and_consumables_v4.md §2; copies stack — 試作で決めたこと).
   */
  InterludePercent(state) {
    if (state === null || state === undefined) throw argNull('state');
    return ExplorationReducer.InterludeHpPercent + (state.Trial ? state.Loadout.InterludeBonus : 0);
  },

  /**
   * 階層間の休憩. HP back by 30%, stamina full, and the gauge does not move — the only
   * place in a run where time passes for free (concept-v3.md §7).
   */
  Interlude(state) {
    if (!ExplorationReducer.CanInterlude(state))
      throw new ExplorationRuleException(`there is no interlude from ${state.Phase}`);

    const heal = state.Trial
      ? Share(state.MaxHp, ExplorationReducer.InterludePercent(state))
      : ExplorationReducer.InterludeHeal(state.MaxHp);
    const healed = Math.min(state.MaxHp, state.Hp + heal);
    return state.With({
      hp: healed,
      stamina: state.MaxStamina,
      phase: RunPhase.Interlude,
    });
  },

  /**
   * Whether Interlude applies: the layer was left, by its boss falling or by the
   * 刻限 running out.
   */
  CanInterlude(state) {
    if (state === null || state === undefined) throw argNull('state');
    return state.Phase === RunPhase.LayerCleared || state.Phase === RunPhase.PushedOut;
  },

  /** Drops into the next layer, carrying HP, stamina and the gauge (concept-v3.md §7.1). */
  Descend(state, next, nextMap) {
    if (state === null || state === undefined) throw argNull('state');
    if (next === null || next === undefined) throw argNull('next');
    if (state.Phase !== RunPhase.Interlude)
      throw new ExplorationRuleException(`a layer is entered from the interlude, not from ${state.Phase}`);

    return ExplorationReducer.Enter(
      next,
      nextMap,
      state.Loadout,
      state.Hp,
      state.MaxHp,
      state.Stamina,
      state.MiasmaPercent,
      state.TempMaxStaminaMod,
      state.Trial);
  },

  /**
   * Hands a battle's result back to the run. HP and stamina carry across battles
   * (concept-v3.md §7.1); the battle's own max-stamina swings do not, so nothing about
   * 瘴気纏い reaches the gauge (battle_core_v4.md §6.2). Wiring this to the battle core
   * itself is #99.
   */
  ReportBattle(state, hpAfter, staminaAfter) {
    if (state === null || state === undefined) throw argNull('state');

    const hp = Math.max(0, Math.min(state.MaxHp, hpAfter));
    const stamina = Math.max(0, Math.min(state.MaxStamina, staminaAfter));
    const phase = hp === 0 ? RunPhase.Fallen : state.Phase;
    return state.With({ hp, stamina, phase });
  },

  /**
   * 「この生を終える」. The player may close the life alive at any point of the
   * exploration, the interlude included (tier1-core.md R1-11). Nothing is spent and
   * nothing is healed: the life simply stops where it stands.
   */
  EndLife(state) {
    if (state === null || state === undefined) throw argNull('state');
    if (state.IsOver)
      throw new ExplorationRuleException(`the life has already ended (${state.Phase})`);

    return state.With({ phase: RunPhase.Survived });
  },

  /**
   * Spends one mark a 習熟訓練 node earned. The card it goes to and the ticks it takes are the
   * battle core's (Mastery.Train, #625); the exploration core only counts the marks down.
   * Costs no 刻限: the node already took it.
   */
  SpendTrainingMark(state) {
    if (state === null || state === undefined) throw argNull('state');
    if (state.IsOver)
      throw new ExplorationRuleException(`the life has already ended (${state.Phase})`);
    if (state.TrainingMarks <= 0)
      throw new ExplorationRuleException('no 習熟訓練 mark left to spend');

    return state.With({ trainingMarks: state.TrainingMarks - 1 });
  },
};

function WhyNoStep(state, nodeId) {
  if (state.Phase !== RunPhase.Exploring) return `the layer is no longer being explored (${state.Phase})`;
  if (!state.Map.Neighbours(state.CurrentNodeId).includes(nodeId)) return `node ${nodeId} does not touch node ${state.CurrentNodeId}`;
  return 'no 刻限 left to spend on a new node';
}

/** Spends the 刻限 for a node and applies what standing on it does. */
function Resolve(state, nodeId, choice) {
  const node = state.Map.Node(nodeId);

  const miasma = Miasma.Accumulate(state.MiasmaPercent, state.EffectiveDensity);
  const timeLeft = state.TimeLeft - 1;
  let temp = state.TempMaxStaminaMod;
  let hp = state.Hp;
  let marks = state.TrainingMarks;

  if (node.Kind === NodeKind.Rest) {
    if (choice === RestChoice.Rest) {
      hp = Math.min(state.MaxHp, hp + ExplorationReducer.RestHeal(state.MaxHp));
      temp = Clamp(temp + ExplorationReducer.RestMaxStaminaBonus);
    } else {
      marks += 1;
    }
  }

  const resolved = new Set(state.Resolved);
  resolved.add(nodeId);

  // Stamina recovers by 3 for every 刻限 spent outside battle, up to the max that the
  // new gauge reading allows (concept-v3.md §12-16).
  const maxStamina = Miasma.MaxStamina(temp, miasma);
  let stamina = Math.min(maxStamina, state.Stamina + Miasma.FieldRecovery);
  if (choice === RestChoice.Rest && node.Kind === NodeKind.Rest) stamina = maxStamina;

  // 瘴気死 outranks everything: the life ends on the node that tipped the gauge,
  // even if that node was the layer boss.
  let phase = RunPhase.Exploring;
  if (Miasma.IsLethal(miasma)) phase = RunPhase.MiasmaDeath;
  else if (node.Kind === NodeKind.Boss)
    phase = state.Profile.IsLast ? RunPhase.Completed : RunPhase.LayerCleared;
  else if (timeLeft <= 0) phase = RunPhase.PushedOut;

  return state.With({
    currentNodeId: nodeId,
    resolved,
    timeLeft: Math.max(0, timeLeft),
    miasmaPercent: miasma,
    hp,
    stamina,
    tempMaxStaminaMod: temp,
    trainingMarks: marks,
    phase,
  });
}

/** A share of a maximum, rounded half away from zero the way the battle core rounds. */
function Share(maximum, percent) {
  return Math.trunc(roundAway(maximum * percent / 100.0));
}

function Clamp(tempMod) {
  return Math.max(-Miasma.TempModClamp, Math.min(Miasma.TempModClamp, tempMod));
}
