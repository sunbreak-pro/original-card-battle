// Mirrors oracle/src/Suites/Scripts.cs.
import { BATTLE_MODULES, battleApi, replayCalls, setupFor, callOnly } from '../lib/battle.mjs';
import { Try, outPair, arr } from '../lib/cs.mjs';

export const imports = [
  ...BATTLE_MODULES,
  'src/bridge/depiction/CoreScriptWriter.js',
  'src/bridge/depiction/CoreBattleSource.js',
  'src/bridge/depictionScript/DepictionTypes.js',
  'src/bridge/depictionScript/OmenDisclosure.js',
];

const SOURCE_MAX_MOVES = 3000;

export function run(c, { use }) {
  const T = battleApi(use);
  if (c.id.startsWith('writer:')) {
    const { CoreScriptWriter } = use('src/bridge/depiction/CoreScriptWriter.js', 'CoreScriptWriter');
    const { OmenDisclosure } = use('src/bridge/depictionScript/OmenDisclosure.js', 'OmenDisclosure');
    const { steps, error } = replayCalls(T, c.input);
    if (error && steps.length === 0) throw error;
    const setup = setupFor(T, c.input);
    const writer = new CoreScriptWriter(setup.Enemy);
    writer.ChainIndex = 1;
    writer.ChainTotal = 1;
    writer.Disclosure = OmenDisclosure.Full;
    const opening = writer.Opening(steps[0].result.State);
    const out = [];
    for (let i = 1; i < steps.length; i++) {
      const s = steps[i];
      out.push({ ...callOnly(s.call), Events: Try(() => writer.Write(s.result.Events, s.result.State)) });
    }
    if (error) out.push(error);
    return { Opening: opening, Steps: out };
  }

  // source:<battle>: CoreBattleSource run unattended.
  const { CoreBattleSource } = use('src/bridge/depiction/CoreBattleSource.js', 'CoreBattleSource');
  const { DepictionText, PlayVerdict } = use('src/bridge/depictionScript/DepictionTypes.js', 'DepictionText', 'PlayVerdict');
  const source = new CoreBattleSource(setupFor(T, c.input), c.input.Seed, c.input.SuggestCards, c.input.StopAfterTurns);
  const moves = [];
  const opening = source.Frame;
  let guard = 0;
  while (!source.Finished && guard++ < SOURCE_MAX_MOVES) {
    const move = {};
    if (source.WaitingForPlayer) {
      const id = source.SuggestedCardId;
      move.Guide = source.GuideText;
      move.CanEndTurn = source.CanEndTurn;
      move.Suggested = id;
      move.Hand = arr(source.Frame.Hand).map((face) => {
        const verdict = source.Inspect(face.Id);
        return {
          Id: face.Id,
          Inspect: verdict,
          PreviewFor: source.PreviewFor(face.Id),
          RefusalText: source.RefusalText(face.Id, verdict),
          WrongZoneText: source.RefusalText(face.Id, PlayVerdict.WrongZone),
        };
      });
      if (id.length > 0) {
        const face = DepictionText.Find(source.Frame.Hand, id);
        const [verdict, played] = outPair(source.TryPlay(id, DepictionText.RequiredZone(face.Aim)), 'played');
        move.Action = 'TryPlay';
        move.Verdict = verdict;
        move.Event = played;
      } else {
        move.Action = 'EndTurn';
        move.Event = source.EndTurn();
      }
    } else {
      move.Action = 'AdvanceAuto';
      move.Event = source.AdvanceAuto();
    }
    moves.push(move);
  }
  return {
    Opening: opening,
    Moves: moves,
    Finished: source.Finished,
    Result: source.State.Result,
    Turn: source.State.Turn,
    HistoryCount: arr(source.History).length,
  };
}
