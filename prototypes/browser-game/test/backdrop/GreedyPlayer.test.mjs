// Port of the GreedyPlayer cases of unity-port/BattleCore.Sim.Tests/SimTests.cs (#53: the bench plays only
// legal cards, a score never reads the draw pile, the cap ends a turn), unit cases of the value terms on
// hand-made boards, and the oracle's `battles` suite replayed: every greedy decision (free step, card,
// target and its double value) must be the C#'s, in order.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { loadModules, needExports, Setup, SeedFor, MainDecks, oracleCases } from './_sim.mjs';

const { mods, missing: loadMissing } = await loadModules([
  'sim/GreedyPlayer.js',
  'sim/Decks.js',
  'core/battle/TurnLoop.js',
  'core/battle/Types.js',
  'core/battle/Constants.js',
  'core/battle/IRng.js',
  'core/battle/Enemies.js',
  'core/battle/CardCatalog.js',
]);
const missing = loadMissing
  || needExports(mods, 'core/battle/Enemies.js', ['Enemies'])
  || needExports(mods, 'core/battle/CardCatalog.js', ['CardCatalog']);
const skip = missing || false;
// The pure-logic cases need only the GreedyPlayer module itself to load.
const skipUnit = loadMissing && !mods['sim/GreedyPlayer.js'] ? loadMissing : false;

const { GreedyPlayer, Positioning, Choice } = mods['sim/GreedyPlayer.js'] ?? {};
const { Decks } = mods['sim/Decks.js'] ?? {};
const { TurnLoop, BattleSetup, PlayRefusal } = mods['core/battle/TurnLoop.js'] ?? {};
const { GameResult, BattlePhase, BattleAttribute, StatusKind, CardInstance } = mods['core/battle/Types.js'] ?? {};
const { Constants } = mods['core/battle/Constants.js'] ?? {};
const { SeededRng } = mods['core/battle/IRng.js'] ?? {};
const { Enemies } = mods['core/battle/Enemies.js'] ?? {};
const { CardCatalog } = mods['core/battle/CardCatalog.js'] ?? {};

const TurnLimit = 60;

// ---- SimTests.cs ReplayTests ----

test('TheGreedyPlayer_NeverMakesAnIllegalPlay', { skip }, () => {
  for (const deck of MainDecks(Decks)) {
    for (const policy of Positioning.$order) {
      const player = new GreedyPlayer(policy);
      let plays = 0;
      for (const enemy of Enemies.All) {
        const rng = new SeededRng(SeedFor(3, 'legal', enemy.Id, 0));
        let state = TurnLoop.Start(Setup(BattleSetup, Constants, deck, enemy), rng).State;
        while (state.Result === GameResult.Ongoing && state.Turn < TurnLimit) {
          state = TurnLoop.BeginPlayerTurn(state, rng).State;
          if (state.Result !== GameResult.Ongoing) break;
          const step = player.ChooseFreeStep(state);
          if (step !== 0) {
            assert.equal(TurnLoop.CanTakeFreeStep(state), true);
            state = TurnLoop.TakeFreeStep(state, step).State;
          }
          let choice;
          while (state.Result === GameResult.Ongoing && (choice = player.Choose(state)) !== null) {
            assert.equal(TurnLoop.CanPlay(state, choice.InstanceId, choice.Target), PlayRefusal.None,
              `${deck.Id} vs ${enemy.Id} turn ${state.Turn}: ${choice.InstanceId}`);
            assert.ok(choice.Value > 0);
            state = TurnLoop.PlayCard(state, choice.InstanceId, rng, choice.Target).State;
            plays++;
          }
          if (state.Result !== GameResult.Ongoing) break;
          state = TurnLoop.EndTurn(state, rng).State;
        }
      }
      assert.ok(plays > 0, `${deck.Id} ${policy}`);
    }
  }
});

test('ChooseReturnsNothing_OutsideThePlayersTurn', { skip }, () => {
  const state = TurnLoop.Start(Setup(BattleSetup, Constants, Decks.Archetypes[0], Enemies.PolearmWarped), new SeededRng(1)).State;
  assert.equal(new GreedyPlayer().Choose(state), null, 'before BeginPlayerTurn');
});

test('AScore_DoesNotDependOnTheOrderOfTheDrawPile', { skip }, () => {
  const deck = Decks.Archetypes[0];
  const rng = new SeededRng(5);
  let start = TurnLoop.Start(Setup(BattleSetup, Constants, deck, Enemies.PolearmWarped), rng).State;
  start = TurnLoop.BeginPlayerTurn(start, rng).State;

  const Card = (id, copy = 0) => new CardInstance(`${id}-x${copy}`, CardCatalog.ById(id));
  const footwork = Card('footwork');
  const hand = [footwork, Card('thrust'), Card('brace')];
  const pile = [Card('throw_blade'), Card('iron_block'), Card('iron_block', 1), Card('deep_breath')];
  const one = start.with({ Hand: hand, DrawPile: pile, DiscardPile: [] });
  const two = one.with({ DrawPile: [...pile].reverse() });
  assert.equal(TurnLoop.CanPlay(one, footwork.InstanceId, one.Nearest), PlayRefusal.None);

  for (const policy of Positioning.$order) {
    const player = new GreedyPlayer(policy);
    const a = player.Score(one, footwork, one.Nearest, GreedyPlayer.Value(one));
    const b = player.Score(two, footwork, two.Nearest, GreedyPlayer.Value(two));
    assert.equal(b, a, policy);
  }
});

test('MaxPlays_EndsTheTurnAtTheCap', { skip }, () => {
  const deck = Decks.CostOneOnly();
  const rng = new SeededRng(11);
  let state = TurnLoop.Start(Setup(BattleSetup, Constants, deck, Enemies.PolearmWarped), rng).State;
  state = TurnLoop.BeginPlayerTurn(state, rng).State;
  const capped = new GreedyPlayer(Positioning.Neutral, 2);
  assert.equal(capped.Choose(state, 2), null);
  const free = new GreedyPlayer().Choose(state, 2);
  assert.ok(free === null ? new GreedyPlayer().Choose(state) === null : free.equals(new GreedyPlayer().Choose(state)));
  assert.throws(() => new GreedyPlayer(Positioning.Neutral, -1), { name: 'ArgumentOutOfRangeException' });
});

// ---- the value terms on hand-made boards (only the fields each method reads) ----

const stacks = (m) => ({ Stacks: (kind) => m[kind] ?? 0 });

test('TheConstants_AreTheCSharpWorths', { skip: skipUnit }, () => {
  assert.deepEqual(
    [GreedyPlayer.WinValue, GreedyPlayer.KillBonus, GreedyPlayer.StaminaValue, GreedyPlayer.EnemyStaminaValue, GreedyPlayer.DrawValue,
      GreedyPlayer.StanceValue, GreedyPlayer.FollowUpValue, GreedyPlayer.ReachValue, GreedyPlayer.CellBias, GreedyPlayer.ApproachValue,
      GreedyPlayer.FarGap, GreedyPlayer.KeepTwoPenalty],
    [10000, 15, 2, 1, 2, 10, 0.6, 2.5, 6, 2, 3, 50],
  );
  assert.deepEqual(Positioning.$order, ['Neutral', 'Close', 'Away', 'InOut', 'KeepTwo']);
  const p = new GreedyPlayer();
  assert.equal(p.Positioning, Positioning.Neutral);
  assert.equal(p.MaxPlays, 0);
  assert.throws(() => new GreedyPlayer(Positioning.Close, -1), { name: 'ArgumentOutOfRangeException', message: "0 (no cap) or more (Parameter 'maxPlays')" });
  assert.ok(new Choice('a-0', 1, 2.5).equals(new Choice('a-0', 1, 2.5)));
});

test('TheStatusWorths', { skip: skipUnit }, () => {
  const K = StatusKind;
  // 出血 3 ticks 3·4 = 12; 脆化 caps at 3 (5 each), 威圧 at 3 (3 each), 鈍足 at 4, 疲労 at 4 (1.5 each).
  assert.equal(GreedyPlayer.FoeStatusValue(stacks({ [K.Bleed]: 3, [K.Fragile]: 5, [K.Intimidate]: 1, [K.Slow]: 9, [K.Fatigue]: 2 })), 12 + 15 + 3 + 4 + 3);
  // 強化 caps at 3 (5), 集中 at 2 (6), 見切り at 2 (2), 再生 s(s+1)/2, 俊敏 at 2.
  assert.equal(GreedyPlayer.SelfStatusValue(stacks({ [K.Empower]: 4, [K.Focus]: 1, [K.Parry]: 3, [K.Regen]: 2, [K.Swift]: 5 })), 15 + 6 + 4 + 3 + 2);
  assert.equal(GreedyPlayer.FoeStatusValue(stacks({})), 0);
});

function board({ result, phase, hp = 30, stamina = 3, bonus = 0, maxStamina = 6, followUp = 0, stances = 0, self = {}, enemies = [] }) {
  return {
    Result: result ?? GameResult.Ongoing,
    Phase: phase ?? BattlePhase.AwaitingTurnStart, // not PlayerAction: Incoming reads 0
    Player: { Hp: hp, Statuses: stacks(self), StanceList: new Array(stances).fill({}), Stamina: stamina, NextTurnRecoveryBonus: bonus, MaxStamina: maxStamina, FollowUp: followUp },
    Enemies: enemies.map((e) => ({ Alive: e.hp > 0, Body: { Hp: e.hp, Statuses: stacks(e.statuses ?? {}), Stamina: e.stamina ?? 0 } })),
  };
}

test('Value_ReadsTheBoardAsTheTurnWouldEnd', { skip: skipUnit }, () => {
  assert.equal(GreedyPlayer.Value(board({ result: GameResult.Won })), 10000);
  assert.equal(GreedyPlayer.Value(board({ result: GameResult.Lost })), -10000);
  // 30 HP, a fallen enemy (+15), a standing one at 12 HP / 2 stamina with 出血 1 (+2),
  // one stance (+10), stamina 3 + 1 carried capped at 6 − 3 = 3 (+6), 追撃 5 (+3).
  const v = GreedyPlayer.Value(board({
    hp: 30, stamina: 3, bonus: 1, maxStamina: 6, followUp: 5, stances: 1,
    enemies: [{ hp: 0 }, { hp: 12, stamina: 2, statuses: { [StatusKind.Bleed]: 1 } }],
  }));
  assert.equal(v, 30 + 15 - 12 + 2 - 2 + 10 + 6 + 0.6 * 5);
  // the carried stamina never goes below 0 nor above max − recovery
  assert.equal(GreedyPlayer.Value(board({ hp: 0, stamina: -4, maxStamina: 2 })), 0);
});

function gaps(result, nearest, gap, played = []) {
  return { Result: result ?? GameResult.Ongoing, Nearest: nearest, GapTo: () => gap, Player: { PlayedThisTurn: played } };
}

test('PositionBias_PerPolicy', { skip: skipUnit }, () => {
  const bias = (policy, before, after, played) => new GreedyPlayer(policy).PositionBias(gaps(null, 0, before, played), gaps(null, 0, after));
  // Neutral: only a pull in from 3 or more.
  assert.equal(bias(Positioning.Neutral, 3, 1), 4);
  assert.equal(bias(Positioning.Neutral, 2, 1), 0);
  assert.equal(bias(Positioning.Neutral, 3, 4), 0);
  // Close / Away: ±6 a cell.
  assert.equal(bias(Positioning.Close, 2, 0), 12);
  assert.equal(bias(Positioning.Away, 2, 0), -12);
  // InOut: Close until an attack was played this turn, Away after.
  assert.equal(bias(Positioning.InOut, 2, 1, []), 6);
  assert.equal(bias(Positioning.InOut, 2, 1, [BattleAttribute.Guard]), 6);
  assert.equal(bias(Positioning.InOut, 2, 1, [BattleAttribute.Attack]), -6);
  // KeepTwo: −50 for closing below 2, +6 a cell for widening from below 2.
  assert.equal(bias(Positioning.KeepTwo, 3, 1), -50);
  assert.equal(bias(Positioning.KeepTwo, 1, 0), -50);
  assert.equal(bias(Positioning.KeepTwo, 0, 2), 12);
  assert.equal(bias(Positioning.KeepTwo, 3, 2), 0);
  // nothing once the battle is over or nobody stands
  const p = new GreedyPlayer(Positioning.Close);
  assert.equal(p.PositionBias(gaps(null, 0, 3), gaps(GameResult.Won, 0, 0)), 0);
  assert.equal(p.PositionBias(gaps(null, -1, 3), gaps(null, 0, 0)), 0);
});

test('HeldIds_LeavesThePlayedCardOut', { skip: skipUnit }, () => {
  const state = { Hand: [{ InstanceId: 'a-0' }, { InstanceId: 'b-0' }, { InstanceId: 'a-1' }] };
  assert.deepEqual([...GreedyPlayer.HeldIds(state, null)], ['a-0', 'b-0', 'a-1']);
  assert.deepEqual([...GreedyPlayer.HeldIds(state, { InstanceId: 'b-0' })], ['a-0', 'a-1']);
});

// ---- the C# oracle: the battles suite's greedy decisions ----

// Deck-major: each deck × the 14 setups × seed 1 (98 battles); the full 490 run in the parity harness.
const cases = skip ? null : oracleCases('battles', (id) => id.endsWith('|1'));
test('Oracle_Battles_TheGreedyChoosesWhatTheCSharpChose', { skip: skip || (cases && cases.length ? false : 'oracle/out/battles.json not written') }, () => {
  const decks = new Map([...Decks.Archetypes, Decks.Initial()].map((d) => [d.Id, d]));
  let decisions = 0;
  for (const c of cases) {
    const input = c.input;
    const enemies = input.Enemies.map((id) => Enemies.ById(id));
    const setup = new BattleSetup(enemies[0], decks.get(input.Deck).Build(), input.FieldCells,
      Constants.StartGap, Constants.PlayerStartCell, Constants.PlayerMaxHp, Constants.BaseMaxStamina,
      enemies.length > 1 ? enemies.slice(1) : null, null, null, 0, 0, 0);
    const want = (c.output.Steps ?? []).filter((s) => s.Call !== 'Start' && s.Call !== 'BeginPlayerTurn' && s.Call !== 'EndTurn');
    const got = [];
    const player = new GreedyPlayer(Positioning.Neutral, 0);
    const rng = new SeededRng(input.Seed);
    let state = TurnLoop.Start(setup, rng).State;
    while (state.Result === GameResult.Ongoing && state.Turn < TurnLimit) {
      state = TurnLoop.BeginPlayerTurn(state, rng).State;
      if (state.Result !== GameResult.Ongoing) break;
      const step = player.ChooseFreeStep(state);
      if (step !== 0) {
        got.push({ Call: 'TakeFreeStep', Direction: step });
        state = TurnLoop.TakeFreeStep(state, step).State;
      }
      let played = 0;
      while (state.Result === GameResult.Ongoing) {
        const choice = player.Choose(state, played);
        if (choice === null) break;
        got.push({ Call: 'PlayCard', InstanceId: choice.InstanceId, Target: choice.Target, Value: choice.Value });
        state = TurnLoop.PlayCard(state, choice.InstanceId, rng, choice.Target).State;
        played++;
      }
      if (state.Result !== GameResult.Ongoing) break;
      state = TurnLoop.EndTurn(state, rng).State;
    }
    for (let i = 0; i < Math.max(want.length, got.length); i++) {
      const w = want[i] ? { Call: want[i].Call, InstanceId: want[i].InstanceId, Target: want[i].Target, Direction: want[i].Direction, Value: want[i].Value } : null;
      const g = got[i] ? { Call: got[i].Call, InstanceId: got[i].InstanceId, Target: got[i].Target, Direction: got[i].Direction, Value: got[i].Value } : null;
      assert.deepEqual(g, w, `${c.id}: decision ${i}`);
      decisions++;
    }
    assert.equal(state.Result, c.output.Result, `${c.id}: result`);
  }
  assert.ok(decisions > 0);
});
