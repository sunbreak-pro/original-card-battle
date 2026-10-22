// Port of unity-port/BattleCore.Sim/GreedyPlayer.cs
//
// The order of every loop is the C#'s, so the choice (and its tie-breaks) is the same: the free step
// tries +1 before −1 and keeps the first strictly best; Choose walks the hand in order (the first copy
// of a kind stands for the rest) and each card's targets in order (TargetsFor), keeping the first
// strictly best value. All values are doubles summed in the C#'s order.
//
// HashSet<string>(StringComparer.Ordinal) → Set of strings. `IEnumerable<int> TargetsFor` → array.
import { defineRecord, defineEnum, clamp, ArgumentOutOfRangeException } from '../shared/cs.js';
import { GameResult, BattlePhase, TargetKind, StatusKind, BattleAttribute } from '../core/battle/Types.js';
import { TurnLoop, PlayRefusal } from '../core/battle/TurnLoop.js';
import { EnemyAi } from '../core/battle/EnemyAi.js';
import { Constants } from '../core/battle/Constants.js';
import { SeededRng } from '../core/battle/IRng.js';

/**
 * §13's positioning policies (間合いの方針). Each is a bias added to the greedy value of a play that
 * changes N to the nearest enemy; none forbids a card outright except KeepTwo, which
 * refuses to close inside 2 on its own.
 */
export const Positioning = defineEnum([
  /**
   * No bias beyond what a move does now (the omen it dodges, the cards it brings into reach), and
   * a small pull in from N 3 or more (GreedyPlayer.ApproachValue).
   */
  'Neutral',
  /** 詰め続ける: every cell closer is worth GreedyPlayer.CellBias, every cell away costs it. */
  'Close',
  /** 離れ続ける: the mirror of Close. */
  'Away',
  /** 出入りする: Close until an attack has been played this turn, Away after it (step in, strike, step out). */
  'InOut',
  /**
   * 間合い 2 以上に居続ける (§13 離れて削る型): a play that closes N to below 2 costs
   * GreedyPlayer.KeepTwoPenalty — from 2 or more, and from 1 to 0 as well; a play that
   * widens N from below 2 earns CellBias a cell.
   */
  'KeepTwo',
]);

/** One play the player chose: the card instance, the enemy it is aimed at, and its value. */
export class Choice extends defineRecord('Choice', ['InstanceId', 'Target', 'Value']) {}

/** The seed every look-ahead copy is resolved with (draws on the copy only). private const int LookSeed = 0. */
const LookSeed = 0;

/** 出血 s ticks 2s, 2(s−1), … 2 over its turns: s(s+1) in all. (private static double Ticks(int stacks)) */
function Ticks(stacks) {
  return stacks * (stacks + 1);
}

/**
 * The bench's player (§13 試験台の打ち手, the greedy method battle_core_v4 §13 describes since #403). The exact rule:
 *
 * 1. At the turn start, if 俊敏 offers a free step, the step (forward, back, or none) with the
 *    highest value is taken when that value is above 0.
 * 2. Every card in the hand that TurnLoop.CanPlay allows, against every standing enemy
 *    it can aim at, is played on a copy of the board. Its value is Value after minus
 *    before, plus DrawValue per card it draws, plus the reach and positioning terms.
 * 3. The highest value is played if it is above 0, and step 2 runs again on the new board. The turn
 *    ends when no play is worth more than 0 (or none is allowed), or once MaxPlays
 *    cards have been played this turn when a cap is set. Uncapped (the default), the player plays
 *    as many cards as stay worth their stamina — usually 1〜3.
 *
 * A card the play draws is valued by count only (DrawValue each): the copy draws the
 * real next cards of the shuffled pile, so reading them would let the player see its future draws.
 * ReachDelta and the draw count therefore look only at the cards that were in the hand
 * before the play.
 *
 * Value reads the board the way the omen says the turn will end: the enemies' HP, the
 * HP the shown omens would take off if the turn ended now (TurnLoop.PreviewOmen, which
 * counts the Guard and the 構え the player would hold), the statuses and stances at fixed worths, and
 * the stamina that would still be there next turn. Ties go to the earlier card in the hand. Nothing is
 * random: a copy is resolved with its own fixed seed, so the main RNG is never drawn from.
 */
export class GreedyPlayer {
  /**
   * @param positioning Positioning (default Neutral)
   * @param maxPlays The most cards played in one turn; 0 for no cap (the default, as §13 words the player since #403).
   *   --max-plays 2 reproduces the older wording 「最も期待値の高い 1〜2 枚」.
   */
  constructor(positioning = Positioning.Neutral, maxPlays = 0) {
    // new ArgumentOutOfRangeException(nameof(maxPlays), "0 (no cap) or more"): .NET's Message, as the other ports write it.
    if (maxPlays < 0) {
      const e = new ArgumentOutOfRangeException("0 (no cap) or more (Parameter 'maxPlays')");
      e.ParamName = 'maxPlays';
      throw e;
    }
    this.Positioning = positioning;
    this.MaxPlays = maxPlays;
  }

  /** The free step to take now (+1 forward, −1 back), or 0 for none. */
  ChooseFreeStep(state) {
    if (!TurnLoop.CanTakeFreeStep(state)) return 0;
    const before = GreedyPlayer.Value(state);
    let bestDirection = 0;
    let best = 0;
    for (const direction of [1, -1]) {
      const after = TurnLoop.TakeFreeStep(state, direction).State;
      const value = GreedyPlayer.Value(after) - before + GreedyPlayer.ReachDelta(state, after, GreedyPlayer.HeldIds(state, null)) + this.PositionBias(state, after);
      if (value > best) {
        best = value;
        bestDirection = direction;
      }
    }
    return bestDirection;
  }

  /**
   * The best play worth more than 0, or null to end the turn. `playedThisTurn` is
   * how many cards the player has already played this turn, read against MaxPlays.
   */
  Choose(state, playedThisTurn = 0) {
    if (state.Result !== GameResult.Ongoing || state.Phase !== BattlePhase.PlayerAction) return null;
    if (this.MaxPlays > 0 && playedThisTurn >= this.MaxPlays) return null;
    const before = GreedyPlayer.Value(state);
    let best = null;
    const seen = new Set();
    for (const card of state.Hand) {
      // Copies of one kind are the same play; the first in the hand stands for them.
      if (seen.has(card.Def.Id)) continue;
      seen.add(card.Def.Id);
      for (const target of GreedyPlayer.TargetsFor(state, card.Def)) {
        if (TurnLoop.CanPlay(state, card.InstanceId, target) !== PlayRefusal.None) continue;
        const value = this.Score(state, card, target, before);
        if (value > 0 && (best === null || value > best.Value)) best = new Choice(card.InstanceId, target, value);
      }
    }
    return best;
  }

  /**
   * What playing the card at the target is worth, from a copy of the board. The copy draws the real
   * next cards of the pile, so only the cards held before the play are read: each card drawn is
   * worth DrawValue whatever it is, and the value never depends on the order of the
   * draw pile.
   */
  Score(state, card, target, valueBefore) {
    const after = TurnLoop.PlayCard(state, card.InstanceId, new SeededRng(LookSeed), target).State;
    const held = GreedyPlayer.HeldIds(state, card);
    let drawn = 0;
    for (const c of after.Hand) if (!held.has(c.InstanceId)) drawn++;
    return GreedyPlayer.Value(after) - valueBefore + GreedyPlayer.DrawValue * drawn + GreedyPlayer.ReachDelta(state, after, held) + this.PositionBias(state, after);
  }

  /** The instance ids in the hand now, but `played`: the cards the player already knows. */
  static HeldIds(state, played) {
    const held = new Set();
    for (const c of state.Hand) {
      if (played == null || c.InstanceId !== played.InstanceId) held.add(c.InstanceId);
    }
    return held;
  }

  /**
   * The enemies a card can be aimed at: each standing enemy for a card aimed at one opponent, else
   * the nearest (a card aimed at all or at the player reads that one and ignores the pick).
   */
  static TargetsFor(state, def) {
    if (EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets) && def.Targets === TargetKind.One) {
      return state.Living;
    }
    const nearest = state.Nearest;
    return nearest < 0 ? [] : [nearest];
  }

  /**
   * How good the board is for the player if the turn ended now. Fixed worths (HP units): see the
   * constants. Only the shown omens are read: an elite's or boss's second action is not counted.
   */
  static Value(state) {
    if (state.Result === GameResult.Won) return GreedyPlayer.WinValue;
    if (state.Result === GameResult.Lost) return -GreedyPlayer.WinValue;

    const player = state.Player;
    let value = player.Hp - GreedyPlayer.Incoming(state);
    for (let i = 0; i < state.Enemies.length; i++) {
      const enemy = state.Enemies[i];
      if (!enemy.Alive) {
        value += GreedyPlayer.KillBonus;
        continue;
      }
      value -= enemy.Body.Hp;
      value += GreedyPlayer.FoeStatusValue(enemy.Body.Statuses);
      value -= GreedyPlayer.EnemyStaminaValue * enemy.Body.Stamina;
    }
    value += GreedyPlayer.SelfStatusValue(player.Statuses);
    value += GreedyPlayer.StanceValue * player.StanceList.length;
    const carried = player.Stamina + player.NextTurnRecoveryBonus;
    value += GreedyPlayer.StaminaValue * clamp(carried, 0, Math.max(0, player.MaxStamina - Constants.StaminaRecovery));
    value += GreedyPlayer.FollowUpValue * player.FollowUp;
    return value;
  }

  /** The HP the shown omens would take off the player if the turn ended now (0 for a rest or a whiff). */
  static Incoming(state) {
    if (state.Result !== GameResult.Ongoing || state.Phase !== BattlePhase.PlayerAction) return 0;
    let total = 0;
    for (const i of state.Living) {
      const omen = TurnLoop.PreviewOmen(state, i);
      if (omen == null || omen.Rests || !omen.Lands) continue;
      total += omen.Damage;
    }
    return total;
  }

  static FoeStatusValue(s) {
    return Ticks(s.Stacks(StatusKind.Bleed))
      + 5 * Math.min(s.Stacks(StatusKind.Fragile), 3)
      + 3 * Math.min(s.Stacks(StatusKind.Intimidate), 3)
      + 1 * Math.min(s.Stacks(StatusKind.Slow), 4)
      + 1.5 * Math.min(s.Stacks(StatusKind.Fatigue), 4);
  }

  static SelfStatusValue(s) {
    return 5 * Math.min(s.Stacks(StatusKind.Empower), 3)
      + 6 * Math.min(s.Stacks(StatusKind.Focus), 2)
      + 2 * Math.min(s.Stacks(StatusKind.Parry), 2)
      + 0.5 * Ticks(s.Stacks(StatusKind.Regen))
      + 1 * Math.min(s.Stacks(StatusKind.Swift), 2);
  }

  /**
   * ReachValue for each card of `held` (the hand before the play, the
   * played card left out) that aims at an opponent and has one in reach, after the play against
   * before it. A card the play drew is not in `held`, so it never counts. This is
   * what makes a step in worth taking when it lets the rest of the hand land.
   */
  static ReachDelta(before, after, held) {
    if (after.Result !== GameResult.Ongoing) return 0;
    return GreedyPlayer.ReachValue * (GreedyPlayer.InReach(after, held) - GreedyPlayer.InReach(before, held));
  }

  /** How many hand cards among `held` aim at an opponent that stands in their reach. */
  static InReach(state, held) {
    let count = 0;
    for (const card of state.Hand) {
      if (!held.has(card.InstanceId)) continue;
      if (GreedyPlayer.AimsAndReaches(state, card.Def)) count++;
    }
    return count;
  }

  /** Whether the card aims at an opponent and one standing enemy is inside its reach. */
  static AimsAndReaches(state, def) {
    if (!EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets)) return false;
    const reach = def.Face.ReachOrDefault;
    for (const i of state.Living) {
      if (reach.Contains(state.GapTo(i))) return true;
    }
    return false;
  }

  /** The policy's bias for the change in N to the nearest standing enemy (see Positioning). */
  PositionBias(before, after) {
    if (after.Result !== GameResult.Ongoing || before.Nearest < 0 || after.Nearest < 0) return 0;
    const gapBefore = before.GapTo(before.Nearest);
    const gapAfter = after.GapTo(after.Nearest);
    const closer = gapBefore - gapAfter;
    switch (this.Positioning) {
      case Positioning.Neutral:
        return gapBefore >= GreedyPlayer.FarGap && closer > 0 ? GreedyPlayer.ApproachValue * closer : 0;
      case Positioning.Close:
        return GreedyPlayer.CellBias * closer;
      case Positioning.Away:
        return -GreedyPlayer.CellBias * closer;
      case Positioning.InOut: {
        const struck = before.Player.PlayedThisTurn.includes(BattleAttribute.Attack);
        return struck ? -GreedyPlayer.CellBias * closer : GreedyPlayer.CellBias * closer;
      }
      case Positioning.KeepTwo:
        if (gapAfter < 2 && gapAfter < gapBefore) return -GreedyPlayer.KeepTwoPenalty;
        if (gapBefore < 2 && gapAfter > gapBefore) return GreedyPlayer.CellBias * (gapAfter - gapBefore);
        return 0;
      default:
        return 0;
    }
  }
}

// Worths, in HP. A stamina point carried into next turn is worth StaminaValue, up to the max less
// the 3 recovered (the rest would be lost to the cap). The status worths are a rough HP each.
GreedyPlayer.WinValue = 10_000;
GreedyPlayer.KillBonus = 15;
GreedyPlayer.StaminaValue = 2;
GreedyPlayer.EnemyStaminaValue = 1;
GreedyPlayer.DrawValue = 2;
GreedyPlayer.StanceValue = 10;
GreedyPlayer.FollowUpValue = 0.6;
GreedyPlayer.ReachValue = 2.5;
GreedyPlayer.CellBias = 6;
/**
 * Neutral's one positional term: a cell closed from N 3 or more (§7.2: 「届く札がほぼ無い、仕切り直しの
 * 距離」) is worth this much, so a player facing an enemy that keeps away still walks in.
 */
GreedyPlayer.ApproachValue = 2;
GreedyPlayer.FarGap = 3;
GreedyPlayer.KeepTwoPenalty = 50;
