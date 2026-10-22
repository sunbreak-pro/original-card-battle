// Port of unity-project-kit/Assets/View/Depiction/Bridge/Tests/CoreBattleSourceTests.cs
// The core battle as an IDepictionSource: the contract the screen relies on, a whole fight, and the
// 2.0 s-per-event budget held against every event the converter can write.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { CoreBattleSource } from '../../src/bridge/depiction/CoreBattleSource.js';
import { CoreScriptWriter } from '../../src/bridge/depiction/CoreScriptWriter.js';
import { CoreText } from '../../src/bridge/depiction/CoreText.js';
import {
  BattleOutcome, CueKind, DepictionEventKind, DepictionText, DropZone, PlayVerdict, StrikeSystem, UnitSide,
} from '../../src/bridge/depictionScript/DepictionTypes.js';
import { EffectId, EffectPlan, EffectSwitches, StatusBeat } from '../../src/bridge/depictionScript/Effects.js';
import { DemoDeck } from '../../src/bridge/depictionScript/DemoDeck.js';
import { CardCatalog, PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { Cards } from '../../src/core/battle/Cards.js';
import { Enemies } from '../../src/core/battle/Enemies.js';
import { EnemyAi } from '../../src/core/battle/EnemyAi.js';
import { BattleSetup, PlayRefusal, TurnLoop } from '../../src/core/battle/TurnLoop.js';
import { FixedRng } from '../../src/core/battle/IRng.js';
import {
  ActionExecuted, CardPlayed, CellsMoved, DamageDealt, GuardCleared, StatusApplied, StatusTicked,
} from '../../src/core/battle/BattleEvents.js';
import { Actor, BattlePhase, CardInstance, GameResult, StatusKind } from '../../src/core/battle/Types.js';

const Seed = 20260921;
const BudgetMs = 2000;
const NoShuffle = () => new FixedRng(0.9999999);
const isInvalidOp = (e) => e.name === 'InvalidOperationException';
const single = (arr, msg) => { assert.equal(arr.length, 1, msg ?? 'exactly one'); return arr[0]; };

/** What an event waits for with every effect on: the same plan the View waits by (EffectPlan). */
function NominalMs(ev) {
  return Math.trunc(EffectPlan.BlockingMs(ev, EffectSwitches.AllOn()));
}

function ZoneFor(face) {
  return DepictionText.RequiredZone(face.Aim);
}

/** The first card that is allowed, else the end-turn plate, to the end of the battle. */
function PlayGreedy(source) {
  const events = [];
  let guard = 0;
  while (!source.Finished && guard++ < 400) {
    if (!source.WaitingForPlayer) {
      events.push(source.AdvanceAuto());
      continue;
    }
    const next = source.Frame.Hand.find((f) => source.Inspect(f.Id) === PlayVerdict.Accepted) ?? null;
    if (next == null) {
      events.push(source.EndTurn());
      continue;
    }
    events.push(source.TryPlay(next.Id, DepictionText.RequiredZone(next.Aim)).value);
  }
  assert.equal(source.Finished, true);
  return events;
}

/** One greedy step: the event it hands back. */
function GreedyStep(source) {
  if (source.WaitingForPlayer) {
    const next = source.Frame.Hand.find((f) => source.Inspect(f.Id) === PlayVerdict.Accepted) ?? null;
    if (next != null) return source.TryPlay(next.Id, ZoneFor(next)).value;
    return source.EndTurn();
  }
  return source.AdvanceAuto();
}

test('Slow_SwellsIn_ShrinksAtTheEnemysTurnStart_AndFadesOutTwoTurnsLater', () => {
  // #77: 体当たり puts 鈍足 2 on the enemy; each enemy turn start takes one off (battle_core_v4 §5).
  const state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle()).State;
  const writer = new CoreScriptWriter(state.EnemyDef);
  writer.Opening(state);
  const events = [
    new CardPlayed(Actor.Player, new CardInstance('body_check-0', CardCatalog.BodyCheck), 0),
    new StatusApplied(Actor.Player, Actor.Enemy, StatusKind.Slow, 2, 2, false),
    new GuardCleared(Actor.Enemy, 0),
    new StatusTicked(Actor.Enemy, StatusKind.Slow, 1),
    new GuardCleared(Actor.Enemy, 0),
    new StatusTicked(Actor.Enemy, StatusKind.Slow, 0),
  ];
  const written = writer.Write(events, state);
  const changes = written.flatMap((ev) => ev.Cues).filter((c) => c.Kind === CueKind.StatusChange);

  assert.deepEqual(changes.map((c) => [c.Target, c.Text, c.Amount, c.StacksAfter]), [
    [UnitSide.Enemy, '鈍足', 2, 2], [UnitSide.Enemy, '鈍足', -1, 1], [UnitSide.Enemy, '鈍足', -1, 0],
  ]);
  assert.deepEqual(changes.map((c) => StatusBeat.Of(c)), [EffectId.StatusApply, EffectId.StatusTick, EffectId.StatusVanish]);
  assert.deepEqual(written[0].After.Enemy.Statuses.map((s) => s.Label + s.Stacks), ['鈍足2']);
  assert.deepEqual(written[1].After.Enemy.Statuses.map((s) => s.Label + s.Stacks), ['鈍足1']);
  assert.deepEqual(written[2].After.Enemy.Statuses, [], 'gone two turns later');
});

test('APush_IsMarkedAsOne_AndTheLastFrameSaysWhoWon', () => {
  const state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle()).State;
  const writer = new CoreScriptWriter(state.EnemyDef);
  writer.Opening(state);
  const pushed = writer.Write([
    new GuardCleared(Actor.Enemy, 0),
    new CellsMoved(Actor.Player, state.Player.Cell, state.Player.Cell - 1, true),
  ], state);
  assert.equal(single(pushed.flatMap((ev) => ev.Cues).filter((c) => c.Kind === CueKind.RangeSwitch)).Pushed, true);
  assert.equal(pushed[pushed.length - 1].After.Outcome, BattleOutcome.Ongoing);

  const won = state.with({ Result: GameResult.Won, Phase: BattlePhase.Finished });
  const last = writer.Write([new GuardCleared(Actor.Enemy, 0)], won);
  assert.equal(last[last.length - 1].After.Outcome, BattleOutcome.Won);
});

test('AWonFight_EndsOnTheEnemysFall_AsABeatOfItsOwn_AndTheFigureWearsTheEnemysArt', () => {
  // #288: the 800 ms fall follows the blow as its own event, so the blow keeps its 2.0 s.
  let won = 0;
  for (let seed = 1; seed <= 40; seed++) {
    const fight = PlayGreedy(CoreBattleSource.Slice(seed));
    assert.deepEqual([...new Set(fight.map((ev) => ev.After.Enemy.ArtId))], [Enemies.PolearmWarpedId], 'seed ' + seed);
    if (fight[fight.length - 1].After.Outcome !== BattleOutcome.Won) {
      assert.equal(fight.some((ev) => ev.Kind === DepictionEventKind.Defeat), false, 'seed ' + seed);
      continue;
    }
    won++;
    const fall = fight[fight.length - 1];
    assert.equal(fall.Kind, DepictionEventKind.Defeat, 'seed ' + seed);
    assert.deepEqual(fall.Cues.map((c) => [c.Kind, c.Target]), [[CueKind.Defeat, UnitSide.Enemy]], 'seed ' + seed);
    assert.equal(fall.After.Enemy.Down, true, 'seed ' + seed);
    assert.equal(fall.After.Omen.Visible, false, 'seed ' + seed + ': the omen goes with the enemy');
    assert.ok(fall.After.Enemy.Hp <= 0, 'seed ' + seed);
    assert.ok(fight.slice(0, fight.length - 1).every((ev) => !ev.After.Enemy.Down), 'seed ' + seed + ': up until it falls');
    assert.equal(fight.filter((ev) => ev.Kind === DepictionEventKind.Defeat).length, 1, 'seed ' + seed);
    assert.equal(NominalMs(fall), 800, 'seed ' + seed);
  }
  assert.ok(won > 0, 'no seed won; the fall was never written');
});

test('APlayedFight_EndsOnAFrameThatSaysWhoWon', () => {
  const source = CoreBattleSource.Slice(20260920);
  const fight = PlayGreedy(source);
  assert.notEqual(source.State.Result, GameResult.Ongoing);
  const expected = source.State.Result === GameResult.Won ? BattleOutcome.Won : BattleOutcome.Lost;
  const outcomes = fight.map((ev) => ev.After.Outcome);
  const settled = outcomes.slice(outcomes.findIndex((o) => o !== BattleOutcome.Ongoing));
  assert.ok(outcomes.some((o) => o !== BattleOutcome.Ongoing));
  assert.ok(settled.every((o) => o === expected));
  assert.equal(source.Frame.Outcome, expected);
  assert.equal(DepictionText.OutcomeText(source.Frame.Outcome), expected === BattleOutcome.Won ? '勝ち' : '負け');
});

test('ThePolearmsActions_EachMoveTheirOwnWay', () => {
  const systems = ['sweep', 'reach_thrust', 'shove', 'guard_up', 'step_forward']
    .map((id) => CoreText.SystemOf(Enemies.PolearmWarped.Actions.get(id)));
  assert.deepEqual(systems, [StrikeSystem.Sweep, StrikeSystem.Thrust, StrikeSystem.Strike, StrikeSystem.Shield, StrikeSystem.Step]);
});

test('TheWindupAndTheBlow_CarryTheActionsSystem', () => {
  const state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle()).State;
  const writer = new CoreScriptWriter(state.EnemyDef);
  writer.Opening(state);
  const events = [
    new GuardCleared(Actor.Enemy, 0),
    new ActionExecuted(Actor.Enemy, Enemies.PolearmWarped.Actions.get('shove'), 0),
    new DamageDealt(Actor.Enemy, Actor.Player, 8, 0, 8, 0, 42),
  ];
  const cues = writer.Write(events, state).flatMap((ev) => ev.Cues);

  assert.equal(single(cues.filter((c) => c.Kind === CueKind.EnemyWindup)).System, StrikeSystem.Strike);
  assert.equal(single(cues.filter((c) => c.Kind === CueKind.Slash)).System, StrikeSystem.Strike);
});

// ---- the contract ----

test('ItOpens_NotWaiting_AndTheFirstAutomaticEventIsTheTurnStart', () => {
  const source = CoreBattleSource.Slice(Seed);

  assert.equal(source.Finished, false);
  assert.equal(source.WaitingForPlayer, false);
  assert.equal(source.CanEndTurn, false);
  assert.deepEqual(source.Frame.Hand, []);
  assert.throws(() => source.EndTurn(), isInvalidOp);

  const start = source.AdvanceAuto();

  assert.equal(start.Kind, DepictionEventKind.TurnStart);
  assert.equal(source.Frame, start.After);
  assert.equal(source.Frame.Hand.length, 6);
  assert.equal(source.WaitingForPlayer, true);
  assert.equal(source.CanEndTurn, true);
  assert.throws(() => source.AdvanceAuto(), isInvalidOp, 'the turn waits for the player');
  assert.ok(source.GuideText.includes('ターン終了'));
  assert.equal(source.SuggestedCardId, '');
});

test('TheConstructor_RefusesANullSetup_AndANegativeTurnLimit', () => {
  assert.throws(() => new CoreBattleSource(null, 1), (e) => e.name === 'ArgumentNullException');
  assert.throws(() => new CoreBattleSource(BattleSetup.Slice(), 1, false, -1), (e) => e.name === 'ArgumentOutOfRangeException');
  assert.equal(CoreBattleSource.FleeGap, 2);
});

test('ItDoesNotReadTheDemoDeck', () => {
  const source = CoreBattleSource.Slice(Seed);
  source.AdvanceAuto();

  const demoNames = DemoDeck.All.map((c) => c.DefId);
  const catalogNames = CardCatalog.All.map((c) => c.Name);
  for (const face of source.Frame.Hand) {
    assert.ok(!demoNames.includes(DemoDeck.DefIdOf(face.Id)));
    assert.ok(catalogNames.includes(face.Name));
  }
});

test('Verdicts_ComeFromTheCore', () => {
  const source = CoreBattleSource.Slice(Seed);
  assert.equal(source.Inspect('thrust-0'), PlayVerdict.NotWaiting);

  source.AdvanceAuto();
  assert.equal(source.Inspect('no-such-card'), PlayVerdict.NotInHand);
  const refused = source.TryPlay('no-such-card', DropZone.Receiver);
  assert.equal(refused.ok, PlayVerdict.NotInHand);
  assert.equal(refused.value, null);
  assert.equal(refused.verdict, PlayVerdict.NotInHand);
  assert.equal(refused.played, null);

  // Spend until something in the hand cannot be paid for.
  let stuck = null;
  while (stuck == null) {
    const next = source.Frame.Hand.find((f) => source.Inspect(f.Id) === PlayVerdict.Accepted) ?? null;
    stuck = source.Frame.Hand.find((f) => source.Inspect(f.Id) === PlayVerdict.NotEnoughStamina) ?? null;
    if (stuck != null || next == null) break;
    source.TryPlay(next.Id, ZoneFor(next));
  }

  assert.ok(stuck != null, 'seed ' + Seed + ' deals a first hand that costs more than 10');
  assert.equal(source.PreviewFor(stuck.Id), '');
  assert.ok(source.RefusalText(stuck.Id, PlayVerdict.NotEnoughStamina).includes('スタミナ ' + stuck.Cost));
  assert.equal(source.RefusalText(stuck.Id, PlayVerdict.NotEnoughStamina),
    '「' + stuck.Name + '」はスタミナ ' + stuck.Cost + ' が要ります。残りは ' + source.Frame.Player.Stamina + ' です');
  assert.ok(source.RefusalText(stuck.Id, PlayVerdict.WrongZone).includes(DepictionText.ZoneName(stuck.Aim)));
  assert.equal(source.RefusalText(stuck.Id, PlayVerdict.Accepted), '');
  assert.equal(source.RefusalText('no-such-card', PlayVerdict.NotEnoughStamina), '');
});

test('EndTurn_HandsBackTheTurnEnd_ThenTheEnemyAction_ThenTheNextOmen_ThenTheNextTurn', () => {
  const source = CoreBattleSource.Slice(Seed);
  source.AdvanceAuto();

  const kinds = [source.EndTurn().Kind];
  assert.equal(source.WaitingForPlayer, false);
  assert.equal(source.CanEndTurn, false);
  kinds.push(source.AdvanceAuto().Kind);
  kinds.push(source.AdvanceAuto().Kind);
  kinds.push(source.AdvanceAuto().Kind);

  assert.deepEqual(kinds, [
    DepictionEventKind.TurnEnd, DepictionEventKind.EnemyAction, DepictionEventKind.NextOmen, DepictionEventKind.TurnStart,
  ]);
  assert.equal(source.WaitingForPlayer, true);
  assert.equal(source.Frame.Corner.Turn, 2);
  assert.equal(source.Frame.Player.RangeGlyph, String(source.State.Gap), 'the tag is N');
});

// ---- a whole fight ----

test('EveryEventsFrame_AgreesWithTheCoreState_WhenTheStepSettles', () => {
  const source = CoreBattleSource.Slice(Seed);
  let guard = 0;
  while (!source.Finished && guard++ < 400) {
    GreedyStep(source);

    // Whenever the source is settled (nothing queued), the screen and the core must agree.
    if (!source.WaitingForPlayer && !source.Finished) continue;
    const state = source.State;
    const frame = source.Frame;
    assert.equal(frame.Player.Hp, state.Player.Hp);
    assert.equal(frame.Player.Guard, state.Player.Guard);
    assert.equal(frame.Player.Stamina, state.Player.Stamina);
    assert.equal(frame.Player.RangeGlyph, String(state.Gap));
    assert.equal(frame.Enemy.Hp, state.Enemy.Hp);
    assert.equal(frame.Enemy.Guard, state.Enemy.Guard);
    assert.deepEqual(frame.Enemy.Statuses.map((c) => c.Label + c.Stacks), CoreText.Chips(state.Enemy.Statuses).map((c) => c.Label + c.Stacks));
    assert.deepEqual(frame.Hand.map((c) => c.Id), state.Hand.map((c) => c.InstanceId));
    assert.equal(frame.Corner.Turn, state.Turn);
  }

  assert.equal(source.Finished, true, 'greedy play ends the fight one way or the other');
  assert.equal(source.GuideText, '', '#191: the outcome is the end screen\'s, not the guide line\'s');
  assert.throws(() => source.AdvanceAuto(), isInvalidOp);
});

test('TheSameSeed_WritesTheSameScript', () => {
  function Run() {
    const source = CoreBattleSource.Slice(Seed);
    const lines = [];
    let guard = 0;
    while (!source.Finished && guard++ < 400) {
      const ev = GreedyStep(source);
      lines.push(ev.Order + ' ' + ev.Kind + ' ' + ev.Title + ' '
        + ev.Cues.map((c) => c.Kind + ':' + c.Amount).join(',')
        + ' → ' + ev.After.Player.Hp + '/' + ev.After.Enemy.Hp);
    }
    return lines.join('\n');
  }
  assert.equal(Run(), Run());
});

test('TheHistory_HoldsEveryCoreEvent_FromTheStartOn', () => {
  const source = CoreBattleSource.Slice(Seed);
  const atStart = source.History.length;
  assert.ok(atStart > 0, 'TurnLoop.Start\'s events');
  source.AdvanceAuto();
  assert.ok(source.History.length > atStart);
});

// ---- the 2.0 s budget (issue #28) ----

test('EveryEventOfAWholeFight_StaysInsideTwoSeconds_ForManySeeds', () => {
  for (let seed = 1; seed <= 40; seed++) {
    const source = CoreBattleSource.Slice(seed);
    let guard = 0;
    while (!source.Finished && guard++ < 400) {
      const ev = GreedyStep(source);
      const ms = NominalMs(ev);
      assert.ok(ms < BudgetMs, 'seed ' + seed + ' event ' + ev.Order + ' ' + ev.Title + ' ' + ms + ' ms');
    }
  }
});

test('EveryCard_AtEveryGap_IntoGuardOrNot_StaysInsideTwoSeconds', () => {
  let played = 0;
  for (const def of CardCatalog.All) {
    for (const gap of [0, 1, 2, 3]) {
      for (const enemyGuard of [0, 3, 40]) {
        // One copy of the card plus four fillers, dealt in order, so it is in the first hand.
        const deck = Cards.BuildDeck([def], 1);
        deck.push(...Cards.BuildDeck([CardCatalog.Brace], 1).map((c) => new CardInstance('filler-0', c.Def)));
        for (let i = 1; i < 4; i++) deck.push(new CardInstance('filler-' + i, CardCatalog.Brace));

        const setup = new BattleSetup(Enemies.PolearmWarped, deck, BattleSetup.SliceFieldCells, { StartGap: gap });
        let state = TurnLoop.Start(setup, NoShuffle()).State;
        state = TurnLoop.BeginPlayerTurn(state, NoShuffle()).State;
        state = state.WithEnemy(state.Enemy.with({ Guard: enemyGuard }));
        if (TurnLoop.CanPlay(state, def.Id + '-0') === PlayRefusal.OutOfReach) continue;

        const writer = new CoreScriptWriter(setup.Enemy);
        writer.Opening(state);
        const play = TurnLoop.PlayCard(state, def.Id + '-0', NoShuffle());
        const ev = single(writer.Write(play.Events, play.State), def.Id);
        played++;

        const ms = NominalMs(ev);
        assert.ok(ms < BudgetMs, def.Id + ' at gap ' + gap + ' into Guard ' + enemyGuard);
      }
    }
  }
  assert.ok(played > 0);
});

test('EveryEnemyAction_AtEveryGap_IntoGuardOrNot_StaysInsideTwoSeconds', () => {
  for (const gap of [0, 1, 2, 3]) {
    for (const playerGuard of [0, 2, 40]) {
      for (const enemyStamina of [10, 1]) {
        const setup = new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), BattleSetup.SliceFieldCells, { StartGap: gap });
        let state = TurnLoop.Start(setup, NoShuffle()).State;
        state = state.WithEnemy(state.Enemy.with({ Stamina: enemyStamina, NextTurnRecoveryBonus: enemyStamina === 1 ? -2 : 0 }));
        state = state.WithOmen(0, EnemyAi.DecideOmen(setup.Enemy, gap, enemyStamina));
        state = TurnLoop.BeginPlayerTurn(state, NoShuffle()).State;
        state = state.with({ Player: state.Player.with({ Guard: playerGuard, Stamina: 0 }) });

        const writer = new CoreScriptWriter(setup.Enemy);
        writer.Opening(state);
        const end = TurnLoop.EndTurn(state, NoShuffle());
        for (const ev of writer.Write(end.Events, end.State)) {
          const ms = NominalMs(ev);
          assert.ok(ms < BudgetMs, ev.Title + ' at gap ' + gap + ' Guard ' + playerGuard + ' (enemy stamina ' + enemyStamina + ')');
        }
      }
    }
  }
});

test('ATwoEnemyFight_IsWritten_WhereTheCSharpThrows (JS extension, documented deviation)', () => {
  // The C# writer keeps one omen and puts the second enemy's (crossbow_hunter's bolt) on the first
  // when it reads the floor: KeyNotFoundException "The given key 'bolt' was not present in the
  // dictionary." (C# oracle, scripts suite, *|polearm_warped+crossbow_hunter|1). The JS writer models
  // every enemy (docs/port-notes/depiction-multi-enemy.md; parity: test/parity/deviations.mjs), so
  // the same moves are written, each omen read against its own enemy. MultiEnemy.test.mjs has the rest.
  const setup = new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), 8, { MoreEnemies: [Enemies.ById('crossbow_hunter')] });
  const source = new CoreBattleSource(setup, 1);
  for (let i = 0; i < 20 && !source.Finished; i++) {
    if (source.WaitingForPlayer) source.EndTurn();
    else source.AdvanceAuto();
  }
  const frame = source.Frame;
  assert.equal(frame.Enemies.length, 2);
  assert.deepEqual(frame.Omens.map((o) => o.Unit), [0, 1]);
  const state = source.State;
  for (const k of [0, 1]) {
    if (!state.Enemies[k].Alive || !frame.Omens[k].Visible) continue;
    assert.equal(frame.Omens[k].KindLabel, CoreText.OmenOf(state.Enemies[k].Omen, state.Enemies[k].Def).KindLabel, `unit ${k}'s own omen`);
  }
});

test('AnUnattendedRun_NeverStepsBackFromGapTwo', () => {
  // #192: SuggestedCardId skips a card that steps back once the gap is FleeGap or more.
  const source = new CoreBattleSource(BattleSetup.Slice(), Seed, true);
  source.AdvanceAuto();
  const id = source.SuggestedCardId;
  if (id !== '') {
    const card = source.State.Hand.find((c) => c.InstanceId === id);
    assert.ok(!(card.Def.Face.Move < 0 && source.State.Gap >= CoreBattleSource.FleeGap));
    assert.equal(TurnLoop.CanPlay(source.State, id), PlayRefusal.None);
  }
});

test('AutoCardId_IsTheSuggestion_EvenOnASourceBuiltNotToSuggest', () => {
  // JS extension: ?autobattle fights a fight a reload replayed from a player's page (suggestCards false); the
  // run reads AutoCardId, which picks as SuggestedCardId would on a suggesting source.
  const quiet = new CoreBattleSource(BattleSetup.Slice(), Seed, false);
  const loud = new CoreBattleSource(BattleSetup.Slice(), Seed, true);
  assert.equal(quiet.AutoCardId, '', 'nothing before the turn waits for the player');
  quiet.AdvanceAuto();
  loud.AdvanceAuto();
  assert.equal(quiet.SuggestedCardId, '', 'the C# getter stays silent');
  assert.notEqual(quiet.AutoCardId, '');
  assert.equal(quiet.AutoCardId, loud.SuggestedCardId);
  assert.equal(TurnLoop.CanPlay(quiet.State, quiet.AutoCardId), PlayRefusal.None);
});
