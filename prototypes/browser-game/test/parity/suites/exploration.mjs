// Mirrors oracle/src/Suites/Exploration.cs: Expedition runs through layers 1〜3 with stubbed battles.
import { Try, arr } from '../lib/cs.mjs';

export const imports = [
  'src/bridge/exploration/Expedition.js',
  'src/bridge/explorationScript/ExplorationSession.js',
  'src/bridge/explorationScript/ExplorationScreen.js',
  'src/core/dungeon/RunLoadout.js',
  'src/core/dungeon/ExplorationState.js',
  'src/core/dungeon/NodeKind.js',
  'src/core/battle/Enemies.js',
  'src/core/battle/Types.js',
];

export function run(c, { use }) {
  const { Expedition } = use('src/bridge/exploration/Expedition.js', 'Expedition');
  const { RunStart } = use('src/bridge/explorationScript/ExplorationSession.js', 'RunStart');
  const { Overlay, ScreenAction } = use('src/bridge/explorationScript/ExplorationScreen.js', 'Overlay', 'ScreenAction');
  const { RunLoadout } = use('src/core/dungeon/RunLoadout.js', 'RunLoadout');
  const { RunPhase, RestChoice } = use('src/core/dungeon/ExplorationState.js', 'RunPhase', 'RestChoice');
  const { NodeKind } = use('src/core/dungeon/NodeKind.js', 'NodeKind');
  const { Enemies } = use('src/core/battle/Enemies.js', 'Enemies');
  const { GameResult, BattlePhase } = use('src/core/battle/Types.js', 'GameResult', 'BattlePhase');

  const input = c.input;
  const start = new RunStart();
  start.Seed = BigInt(input.Seed);
  start.Loadout = input.Loadout === 'trial' ? RunStart.TrialLoadout() : RunLoadout.Empty;
  // Expedition(start, deck, battleToolIds, talent, intel, journal)
  const exp = new Expedition(start, null, input.BattleToolIds, input.Talent);

  const stub = (started) =>
    started.with({
      Result: GameResult.Won,
      Phase: BattlePhase.Finished,
      Player: started.Player.with({ Hp: Math.max(1, started.Player.Hp - input.StubHpLoss) }),
    });

  const moves = [];
  const record = (op, args, result) => {
    const m = { Op: op };
    if (args !== null && args !== undefined) m.Args = args;
    m.Result = result;
    m.Screen = Try(() => exp.Session.Screen());
    m.Expedition = summary(exp, Enemies);
    moves.push(m);
  };

  record('Begin', null, { Tools: exp.Tools, InitialKinds: Expedition.InitialKinds });
  record('Toggle', 'Journal', exp.Session.Toggle(Overlay.Journal));
  record('Toggle', 'Journal', exp.Session.Toggle(Overlay.Journal));
  record('Escape', null, exp.Session.Escape());
  record('Escape', null, exp.Session.Escape());

  let trained = false;
  let usedConsumable = false;
  let guard = 0;
  while (guard++ < input.MaxMoves) {
    const session = exp.Session;
    const state = session.State;
    if (state.IsOver || state.Profile.Layer > input.LastLayer) break;

    if (session.HasPendingBattle) {
      const battle = exp.StartEncounter();
      record('StartEncounter', null, battle ? battle.HandIn : null);
      if (!battle) break;
      while (!battle.Over) {
        const launch = battle.NextLaunch();
        const source = battle.StartNext();
        const finished = stub(source.State);
        battle.Finish(finished, source.History);
        record('Fight', null, {
          Launch: launch,
          StartState: source.State,
          Opening: source.Frame,
          HpAfter: battle.HpAfter,
          StaminaAfter: battle.StaminaAfter,
          Index: battle.Index,
          Count: battle.Count,
          Won: battle.Won,
        });
      }
      record('Return', null, Try(() => exp.Return()));
      const offer = arr(exp.Offer);
      if (offer.length > 0) {
        const pick = offer[0];
        record('Learn', pick, exp.Learn(pick));
      }
      continue;
    }
    if (session.HasPendingStep) {
      record('Act', ScreenAction.ConfirmStep, session.Act(ScreenAction.ConfirmStep));
      continue;
    }
    if (session.ConfirmingDescend) {
      record('Act', ScreenAction.ConfirmDescend, session.Act(ScreenAction.ConfirmDescend));
      continue;
    }
    if (state.Phase === RunPhase.LayerCleared || state.Phase === RunPhase.PushedOut) {
      record('Act', ScreenAction.GoToInterlude, session.Act(ScreenAction.GoToInterlude));
      continue;
    }
    if (state.Phase === RunPhase.Interlude) {
      if (state.Profile.Layer >= input.LastLayer) break;
      record('Act', ScreenAction.Descend, session.Act(ScreenAction.Descend));
      continue;
    }
    if (state.TrainingMarks > 0) {
      const card = arr(exp.Deck)[0].Def.Id;
      record('Train', card, exp.Train(card));
      continue;
    }
    if (!usedConsumable && session.CanUseConsumables && arr(state.Loadout.Consumables).length > 0 && state.MiasmaPercent > 0) {
      usedConsumable = true;
      record('UseConsumable', 0, session.UseConsumable(0));
      continue;
    }
    const open = arr(state.ReachableUnresolved()).map((id) => state.Map.Node(id));
    if (open.length === 0) {
      record('Act', ScreenAction.AskEndLife, session.Act(ScreenAction.AskEndLife));
      record('Act', ScreenAction.ConfirmEndLife, session.Act(ScreenAction.ConfirmEndLife));
      continue;
    }
    // OrderBy(Column).ThenBy(Id).First()
    const pickNode = open.slice().sort((a, b) => a.Column - b.Column || a.Id - b.Id)[0];
    let choice = RestChoice.Rest;
    if (pickNode.Kind === NodeKind.Rest) {
      choice = trained ? RestChoice.Rest : RestChoice.Train;
      trained = true;
    }
    record('Step', { Node: pickNode.Id, Choice: choice }, session.Step(pickNode.Id, choice));
  }
  return { Moves: moves };
}

function summary(exp, Enemies) {
  const s = exp.Session;
  const disclosure = {};
  for (const e of arr(Enemies.All)) disclosure[e.Id] = exp.DisclosureOf(e.Id);
  return {
    Life: s.Life,
    RunSeed: s.RunSeed,
    State: s.State,
    CarvingTaken: s.CarvingTaken,
    Surveyed: s.Surveyed,
    PendingBattleNode: s.PendingBattleNode,
    PendingEncounter: s.PendingEncounter,
    PendingStepNode: s.PendingStepNode,
    ConfirmingEndLife: s.ConfirmingEndLife,
    ConfirmingDescend: s.ConfirmingDescend,
    InterludeHealed: s.InterludeHealed,
    OpenOverlay: s.OpenOverlay,
    MapIsLive: s.MapIsLive,
    CanEndLife: s.CanEndLife,
    Deck: arr(exp.Deck).map((ci) => ci.InstanceId),
    Owned: arr(exp.Owned),
    Offer: exp.Offer,
    Consumables: exp.Consumables,
    Pages: exp.Pages,
    Mastery: exp.Mastery,
    Disclosure: disclosure,
  };
}
