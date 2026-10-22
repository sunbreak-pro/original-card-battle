// Port of unity-project-kit/Assets/View/Depiction/Bridge/Tests/BattleLaunchTests.cs
// What the battle scene starts with (BattleLaunch), and the run a Play of that scene makes.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { BattleLaunch } from '../../src/bridge/depiction/BattleLaunch.js';
import { DepictionEventKind, DepictionText, PlayVerdict } from '../../src/bridge/depictionScript/DepictionTypes.js';
import { OmenDisclosure } from '../../src/bridge/depictionScript/OmenDisclosure.js';
import { Cards } from '../../src/core/battle/Cards.js';
import { CardCatalog } from '../../src/core/battle/CardCatalog.js';
import { Enemies } from '../../src/core/battle/Enemies.js';
import { Constants } from '../../src/core/battle/Constants.js';
import { GameResult } from '../../src/core/battle/Types.js';

const isArgEx = (e) => e.name === 'ArgumentException';
const isRangeEx = (e) => e.name === 'ArgumentOutOfRangeException';
const isInvalidOp = (e) => e.name === 'InvalidOperationException';

/**
 * Drives a source the way DepictionPlayer does with Auto Play Drags and Auto End Turn on:
 * automatic events while nothing waits, then the suggested card, then the turn end.
 */
function RunUnattended(source, safetyLimit = 400) {
  const played = [];
  while (!source.Finished && played.length < safetyLimit) {
    if (!source.WaitingForPlayer) {
      played.push(source.AdvanceAuto());
      continue;
    }
    const suggested = source.SuggestedCardId;
    if (suggested.length === 0) {
      assert.equal(source.CanEndTurn, true);
      played.push(source.EndTurn());
      continue;
    }
    const face = DepictionText.Find(source.Frame.Hand, suggested);
    const r = source.TryPlay(suggested, DepictionText.RequiredZone(face.Aim));
    assert.equal(r.ok, PlayVerdict.Accepted);
    played.push(r.value);
  }
  return played;
}

test('TheConstants_AreTheCs', () => {
  assert.equal(BattleLaunch.DefaultSeed, 20260921);
  assert.equal(BattleLaunch.SliceFieldCells, 6);
  assert.equal(BattleLaunch.DefaultStartGap, Constants.StartGap);
  assert.equal(BattleLaunch.DefaultDisclosure, OmenDisclosure.Full);
  assert.equal(BattleLaunch.LowestDisclosure, -1);
});

test('TheDefaults_AreTheSlice', () => {
  const launch = new BattleLaunch();
  const setup = launch.BuildSetup();

  assert.equal(launch.EnemyId, 'polearm_warped');
  assert.equal(launch.Seed, 20260921);
  assert.equal(launch.AutoPlay, false);
  assert.equal(launch.StopAfterTurns, 0);
  assert.equal(setup.Enemy, Enemies.PolearmWarped);
  assert.equal(setup.Enemy.Name, '錆槍の竜兵');
  assert.equal(launch.StartGap, 3);
  assert.equal(launch.FieldCells, 6);
  assert.equal(setup.PlayerStartCell, 2);
  assert.equal(setup.EnemyStartCell, 6);
  assert.equal(setup.FieldCells, 6);
  assert.equal(setup.Deck.length, 20);
  assert.equal(Cards.Validate(setup.Deck).Ok, true);
});

test('AnUnknownEnemyId_IsRefused_WithTheKnownIdsInTheMessage', () => {
  const launch = new BattleLaunch({ EnemyId: 'polearm' });
  let error = null;
  try { launch.CreateSource(); } catch (e) { error = e; }
  assert.ok(error && isArgEx(error));
  assert.ok(error.message.includes('"polearm"'));
  assert.ok(error.message.includes('polearm_warped'));
  assert.ok(error.message.startsWith('BattleLaunch: no enemy has the id "polearm". Known ids: '));

  assert.throws(() => new BattleLaunch({ EnemyId: null }).CreateSource(), isArgEx);
});

test('ANegativeTurnLimit_IsRefused', () => {
  assert.throws(() => new BattleLaunch({ StopAfterTurns: -1 }).CreateSource(), isRangeEx);
});

test('AnOutOfRangeDisclosure_IsRefused_AndMinusOneReadsAsZero', () => {
  assert.throws(() => new BattleLaunch({ Disclosure: -2 }).CreateSource(), isRangeEx);
  assert.throws(() => new BattleLaunch({ Disclosure: 3 }).CreateSource(), isRangeEx);
  const hidden = new BattleLaunch({ Disclosure: -1 }).CreateSource();
  hidden.AdvanceAuto();
  assert.equal(hidden.Frame.Omen.Disclosure, 0);
});

test('TheOtherChecks_OfBuildSetup', () => {
  assert.throws(() => new BattleLaunch({ PlayerMaxHp: 0 }).BuildSetup(), isRangeEx);
  assert.throws(() => new BattleLaunch({ MaxStamina: Constants.MaxStaminaFloor - 1 }).BuildSetup(), isRangeEx);
  assert.throws(() => new BattleLaunch({ MaxStamina: Constants.MaxStaminaCeil + 1 }).BuildSetup(), isRangeEx);
  assert.throws(() => new BattleLaunch({ PlayerStartCell: 0 }).BuildSetup(), isRangeEx);
  let error = null;
  try { new BattleLaunch({ FieldCells: 4 }).BuildSetup(); } catch (e) { error = e; }
  assert.equal(error.message, "5..8 cells. (Parameter 'FieldCells')\nActual value was 4.");
});

test('AHandedInDeck_IsCopied_AndADeckThatBreaksTheRules_IsRefused', () => {
  const deck = Cards.BuildDeck([CardCatalog.KesaCut, CardCatalog.Feint, CardCatalog.Brace, CardCatalog.Thrust, CardCatalog.BodyCheck,
    CardCatalog.ReachThrust, CardCatalog.BackLeap], 3);
  assert.equal(Cards.Validate(deck).Ok, true, '21 cards, 3 of each kind');
  const launch = new BattleLaunch({ Deck: deck });
  const built = launch.BuildDeck();
  assert.notEqual(built, deck);
  assert.deepEqual(built.map((c) => c.InstanceId), deck.map((c) => c.InstanceId));

  const tooSmall = new BattleLaunch({ Deck: deck.slice(0, 1) });
  assert.equal(Cards.Validate(tooSmall.Deck).Ok, false);
  let error = null;
  try { tooSmall.BuildDeck(); } catch (e) { error = e; }
  assert.ok(error && isArgEx(error));
  assert.ok(error.message.startsWith('BattleLaunch: the deck breaks §8 — '));
});

test('TheStartGap_PicksTheFirstOmen', () => {
  const adjacent = new BattleLaunch({ StartGap: 0 }).CreateSource();
  const apart = new BattleLaunch({ StartGap: 3 }).CreateSource();

  assert.equal(adjacent.Frame.Player.RangeGlyph, '0');
  assert.equal(adjacent.State.Omen.ActionId, 'shove');
  assert.equal(apart.Frame.Player.RangeGlyph, '3');
  assert.equal(apart.State.Omen.ActionId, 'step_forward');
  assert.throws(() => new BattleLaunch({ StartGap: -1 }).CreateSource(), isRangeEx);
});

test('TheLine_IsHandedIn_AndAShortOneBringsTheEnemyToItsEnd', () => {
  const wide = new BattleLaunch({ FieldCells: 8 }).CreateSource();
  const narrow = new BattleLaunch({ FieldCells: 5 }).CreateSource();
  const far = new BattleLaunch({ StartGap: 4 }).CreateSource();

  assert.equal(wide.State.FieldCells, 8);
  assert.equal(wide.State.Gap, 3);
  assert.equal(narrow.State.Enemy.Cell, 5, 'cell 6 is off a 5-cell line');
  assert.equal(narrow.State.Gap, 2);
  assert.equal(far.State.Enemy.Cell, 6, 'cell 7 is off a 6-cell line');
  assert.throws(() => new BattleLaunch({ FieldCells: 4 }).CreateSource(), isRangeEx);
  assert.throws(() => new BattleLaunch({ FieldCells: 9 }).CreateSource(), isRangeEx);
});

test('TheSeed_FromTheInspector_DealsTheSameHandAsTheCoreTest', () => {
  const source = new BattleLaunch().CreateSource();
  source.AdvanceAuto();

  // TurnLoopTests.PinnedHands, turn 1: the same seed, the same generator, the same deal.
  assert.deepEqual(source.State.Hand.map((c) => c.Def.Id), [
    'shield_bash', 'body_check', 'step_out_guard', 'step_in_guard', 'step_out_guard', 'body_check',
  ]);

  const other = new BattleLaunch({ Seed: 7 }).CreateSource();
  other.AdvanceAuto();
  assert.notDeepEqual(other.State.Hand.map((c) => c.InstanceId), source.State.Hand.map((c) => c.InstanceId));
});

test('APlayedBattle_SuggestsNothing_AndNeverStopsEarly', () => {
  const source = new BattleLaunch().CreateSource();
  source.AdvanceAuto();

  assert.equal(source.SuggestedCardId, '', 'the hand belongs to the player');
  source.EndTurn();
  source.AdvanceAuto();
  source.AdvanceAuto();
  assert.equal(source.Finished, false);
});

test('OneUnattendedTurn_RunsFromTheTurnStartToTheNextOmen_AndStops', () => {
  const source = new BattleLaunch({ AutoPlay: true, StopAfterTurns: 1 }).CreateSource();

  const played = RunUnattended(source);

  assert.equal(source.Finished, true);
  assert.equal(played[0].Kind, DepictionEventKind.TurnStart);
  // #192: from the opening gap of 3 it steps in (足捌き・前) and holds the rest.
  assert.equal(played.filter((e) => e.Kind === DepictionEventKind.PlayCard).length, 1);
  assert.deepEqual(played.slice(played.length - 3).map((e) => e.Kind), [
    DepictionEventKind.TurnEnd, DepictionEventKind.EnemyAction, DepictionEventKind.NextOmen,
  ]);
  assert.deepEqual(played.map((e) => e.Order), Array.from({ length: played.length }, (_, i) => i + 1));

  const last = played[played.length - 1].After;
  assert.equal(last.Corner.Turn, 1);
  assert.equal(last.Player.RangeGlyph, '1');
  assert.equal(last.Omen.Visible, true);
  assert.equal(last.Omen.KindLabel, '攻撃');
  assert.equal(last.Omen.SideGlyph, '1〜2');
  assert.equal(last.Player.Hp, 50);
  assert.equal(last.Player.Guard, 15);
  assert.equal(last.Enemy.Hp, 60);
  assert.deepEqual(last.Enemy.Statuses, []);
  assert.equal(source.GuideText, '');
  assert.throws(() => source.AdvanceAuto(), isInvalidOp);
});

test('AnUnattendedFight_ToTheEnd_Finishes_WithAWinner', () => {
  const source = new BattleLaunch({ AutoPlay: true }).CreateSource();

  const played = RunUnattended(source);

  assert.equal(source.Finished, true);
  assert.notEqual(source.State.Result, GameResult.Ongoing);
  assert.ok(played.length < 400);
  assert.equal(source.GuideText, '', '#191: the outcome is the end screen\'s, not the guide line\'s');
});

test('TheChainPosition_ReachesTheCorner', () => {
  const source = new BattleLaunch({ ChainIndex: 2, ChainTotal: 3 }).CreateSource();
  assert.equal(source.Frame.Corner.ChainIndex, 2);
  assert.equal(source.Frame.Corner.ChainTotal, 3);
});
