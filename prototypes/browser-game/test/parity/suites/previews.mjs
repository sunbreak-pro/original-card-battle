// Mirrors oracle/src/Suites/Previews.cs. The battle is replayed from the recorded calls (not the greedy),
// so a greedy difference does not hide a preview difference.
import { BATTLE_MODULES, battleApi, replayCalls } from '../lib/battle.mjs';
import { Try, outPair, arr } from '../lib/cs.mjs';

export const imports = [...BATTLE_MODULES, 'src/bridge/depiction/CoreText.js', 'src/bridge/depiction/CardFaceText.js'];

export function run(c, { use }) {
  const T = battleApi(use);
  const { GreedyPlayer } = use('src/sim/GreedyPlayer.js', 'GreedyPlayer');
  const { CoreText } = use('src/bridge/depiction/CoreText.js', 'CoreText');
  const { CardFaceText } = use('src/bridge/depiction/CardFaceText.js', 'CardFaceText');
  const { steps, error, decisions } = replayCalls(T, c.input);
  const points = decisions.map((i) => decisionPoint(i, steps[i].result.State, { T, GreedyPlayer, CoreText, CardFaceText }));
  // A replay the port could not finish: the error goes last, so the diff shows it as the extra element.
  if (error) points.push(error);
  return points;
}

export function decisionPoint(step, state, { T, GreedyPlayer, CoreText, CardFaceText }) {
  const { TurnLoop } = T;
  const hand = arr(state.Hand);
  const enemies = arr(state.Enemies);
  const cards = [];
  for (const card of hand) {
    for (let target = 0; target < enemies.length; target++) {
      cards.push({
        InstanceId: card.InstanceId,
        Target: target,
        CanPlay: Try(() => TurnLoop.CanPlay(state, card.InstanceId, target)),
        CostNow: Try(() => TurnLoop.CostNow(state, card.Def, target)),
        Preview: Try(() => TurnLoop.Preview(state, card.InstanceId, target)),
      });
    }
  }
  const omens = [];
  for (let unit = 0; unit < enemies.length; unit++) {
    omens.push({
      Unit: unit,
      PreviewOmen: Try(() => TurnLoop.PreviewOmen(state, unit)),
      OmenBoundaryCells: Try(() => TurnLoop.OmenBoundaryCells(state, unit)),
      OmenMoveCell: Try(() => TurnLoop.OmenMoveCell(state, unit)),
    });
  }
  const texts = hand.map((card) => ({
    InstanceId: card.InstanceId,
    ReachesNobody: Try(() => CoreText.ReachesNobody(state, card.InstanceId)),
    Lamps: Try(() => {
      const [lit, perEnemy] = outPair(CardFaceText.Lamps(state, card.InstanceId), 'perEnemy');
      return { Result: lit, PerEnemy: perEnemy };
    }),
    HpLoss: Try(() => CardFaceText.HpLoss(state, card.InstanceId)),
  }));
  return {
    Step: step,
    Cards: cards,
    Omens: omens,
    PreviewRecovery: Try(() => TurnLoop.PreviewRecovery(state)),
    CanTakeFreeStep: TurnLoop.CanTakeFreeStep(state),
    GreedyValue: GreedyPlayer.Value(state),
    GreedyIncoming: GreedyPlayer.Incoming(state),
    Texts: texts,
  };
}
