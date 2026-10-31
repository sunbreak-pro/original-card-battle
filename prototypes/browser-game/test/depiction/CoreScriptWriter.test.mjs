// Port of unity-project-kit/Assets/View/Depiction/Bridge/Tests/CoreScriptWriterTests.cs
// Core event stream → depiction script.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { CoreScriptWriter } from '../../src/bridge/depiction/CoreScriptWriter.js';
import { CoreBattleSource } from '../../src/bridge/depiction/CoreBattleSource.js';
import {
  CardAim, CardKind, Cue, CueKind, DepictionEventKind, DropZone, OmenIcon, PlayVerdict, RangeSide, UnitSide,
} from '../../src/bridge/depictionScript/DepictionTypes.js';
import { EffectId, EffectStrength, StatusBeat } from '../../src/bridge/depictionScript/Effects.js';
import { CardCatalog, PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { Cards } from '../../src/core/battle/Cards.js';
import { Constants } from '../../src/core/battle/Constants.js';
import { Enemies } from '../../src/core/battle/Enemies.js';
import { EnemyAi } from '../../src/core/battle/EnemyAi.js';
import { BattleSetup, TurnLoop } from '../../src/core/battle/TurnLoop.js';
import { FixedRng, SeededRng } from '../../src/core/battle/IRng.js';
import { StatusSet } from '../../src/core/battle/Statuses.js';
import {
  ActionExecuted, DamageDealt, DefeatChecked, GuardCleared, OmenSet, ReserveChecked, Rested, StaminaRecovered, TurnStarted,
} from '../../src/core/battle/BattleEvents.js';
import { Actor, GameResult, Omen, StatusKind } from '../../src/core/battle/Types.js';

const NoShuffle = () => new FixedRng(0.9999999);

const Kinds = (ev) => ev.Cues.map((c) => c.Kind);
const single = (arr, msg) => { assert.equal(arr.length, 1, msg ?? 'exactly one'); return arr[0]; };

/** A battle dealt in deck order at the given gap, with the writer already past the opening. */
function Begin(gap, kinds) {
  const setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(kinds, 1), BattleSetup.SliceFieldCells, { StartGap: gap });
  const state = TurnLoop.Start(setup, NoShuffle()).State;
  const writer = new CoreScriptWriter(setup.Enemy);
  writer.Opening(state);
  return { state, writer };
}

const FiveCards = () => [CardCatalog.KesaCut, CardCatalog.Feint, CardCatalog.Brace, CardCatalog.BodyCheck, CardCatalog.StepOutGuard];

function InHand(state, cardId) {
  return state.Hand.find((c) => c.Def.Id === cardId).InstanceId;
}

// ---- opening ----

test('Opening_ShowsBothUnits_AndKeepsTheFirstOmenHidden', () => {
  const state = TurnLoop.Start(BattleSetup.Slice(), new SeededRng(1)).State;
  const frame = new CoreScriptWriter(state.EnemyDef).Opening(state);

  assert.equal(frame.Player.Hp, 50);
  assert.equal(frame.Player.ShowStamina, true);
  assert.equal(frame.Player.Stamina, 10);
  assert.equal(frame.Player.HasRange, true);
  assert.equal(frame.Player.RangeGlyph, '3', 'the gap N on the player\'s tag (§7.2), START_GAP 3');
  assert.equal(frame.Player.Range, RangeSide.Far, 'gap 2 and up stand in the far slot until #163');
  assert.equal(frame.Enemy.Hp, 60);
  assert.equal(frame.Enemy.HasRange, false, 'one tag says it all');
  assert.equal(frame.Enemy.ShowStamina, true, 'battle-visual-v1 §4.5 (#455): on the enemy\'s number panel');
  assert.equal(frame.Omen.Visible, false, 'step 5 of turn 1 is what shows it');
  assert.deepEqual(frame.Hand, []);
});

test('TheWriter_RefusesNulls', () => {
  assert.throws(() => new CoreScriptWriter(null), (e) => e.name === 'ArgumentNullException');
  const writer = new CoreScriptWriter(Enemies.PolearmWarped);
  assert.throws(() => writer.Opening(null), (e) => e.name === 'ArgumentNullException');
  assert.throws(() => writer.Write(null, {}), (e) => e.name === 'ArgumentNullException');
  assert.throws(() => writer.Write([], null), (e) => e.name === 'ArgumentNullException');
});

// ---- turn start ----

test('TurnStart_IsOneEvent_FiveDrawsBecomeOneBeat_AndTheOmenAppears', () => {
  const { state, writer } = Begin(0, FiveCards());
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());

  const events = writer.Write(begin.Events, begin.State);

  assert.equal(events.length, 1);
  const ev = events[0];
  assert.equal(ev.Kind, DepictionEventKind.TurnStart);
  assert.equal(ev.Order, 1);
  // Guard was already 0 and stamina already full: neither gets a beat.
  assert.deepEqual(Kinds(ev), [CueKind.DrawHand, CueKind.OmenShow]);
  assert.equal(ev.Cues[0].Amount, 5);

  assert.deepEqual(ev.After.Hand.map((c) => c.Id), begin.State.Hand.map((c) => c.InstanceId));
  assert.equal(ev.After.Omen.Visible, true);
  assert.equal(ev.After.Omen.KindLabel, '攻撃');
  assert.equal(ev.After.Omen.Icon, OmenIcon.Attack);
  assert.equal(ev.After.Omen.SideGlyph, '0', 'the shove\'s reach, where the side glyph was (#163)');
  // #248: 構え's Guard 3 takes 無防備's +3 away, so 6, then the two-cell push finds one cell left and puts 3 of wall on top: 9.
  assert.equal(ev.After.Omen.ValueText, '9');
  assert.equal(ev.After.Corner.Turn, 1);
  assert.equal(ev.After.StanceHint, '+3');
});

test('TurnStart_OnTheSlice_DealsAFullHandOfSix_AsOneBeat', () => {
  const rng = new SeededRng(1);
  const state = TurnLoop.Start(BattleSetup.Slice(), rng).State;
  const writer = new CoreScriptWriter(state.EnemyDef);
  writer.Opening(state);
  const begin = TurnLoop.BeginPlayerTurn(state, rng);

  const ev = single(writer.Write(begin.Events, begin.State));

  assert.equal(single(ev.Cues.filter((c) => c.Kind === CueKind.DrawHand)).Amount, 6);
  assert.equal(ev.After.Hand.length, Constants.HandDraw);
});

test('TheHandFaces_CarryTheCanonText_AndTheCoreDecidesTheLamp', () => {
  const { state, writer } = Begin(0, FiveCards());
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  const frame = writer.Write(begin.Events, begin.State)[0].After;

  const kesa = frame.Hand.find((c) => c.Name === '袈裟斬り');
  assert.equal(kesa.Cost, 2);
  assert.equal(kesa.Kind, CardKind.Attack);
  assert.equal(kesa.Aim, CardAim.Single);
  assert.equal(kesa.TypeLabel, '攻撃・敵単体');
  assert.equal(kesa.ValueText, '15');
  assert.equal(kesa.TraitText, '間合い0 +5');
  assert.equal(kesa.TraitLit, true, 'the player stands adjacent');
  assert.equal(kesa.RequiredRange, null);
  assert.equal(kesa.RequiredRangeGlyph, '0〜1', 'the reach, as printed (§2.4)');

  const brace = frame.Hand.find((c) => c.Name === '呼吸を整える');
  assert.equal(brace.Aim, CardAim.Self);
  assert.equal(brace.Affects, UnitSide.Player);
  assert.equal(brace.TypeLabel, '防御・自分');
  assert.equal(brace.TraitText, '残6 回復+1');
  assert.equal(brace.TraitLit, true, '10 − 2 = 8 left');
  assert.equal(brace.RequiredRangeGlyph, '', 'a self card reads no reach');

  const bodyCheck = frame.Hand.find((c) => c.Name === '体当たり');
  assert.equal(bodyCheck.Description, '敵に 5 ダメージ。鈍足を 2 付与する。', '§5: 「<語> を n 付与する」');
  assert.equal(bodyCheck.TraitText, '間合い0 重撃');
  assert.equal(bodyCheck.RequiredRangeGlyph, '0〜1', '#179: v4.3 reaches 0〜1');

  const feint = frame.Hand.find((c) => c.Name === '牽制');
  assert.equal(feint.Description, '敵に 10 ダメージ。後ろへ 1 動く。');
});

test('TheLamp_GoesOut_WhenThePlayerStepsBack', () => {
  const { state, writer } = Begin(0, FiveCards());
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  writer.Write(begin.Events, begin.State);

  const play = TurnLoop.PlayCard(begin.State, InHand(begin.State, 'feint'), NoShuffle());
  const after = writer.Write(play.Events, play.State)[0].After;

  assert.equal(after.Player.RangeGlyph, '1');
  assert.equal(after.Hand.find((c) => c.Name === '袈裟斬り').TraitLit, false);
});

// ---- playing a card ----

test('AnUnguardedHit_IsOneSlash_WithTheSettledNumbers', () => {
  const { state, writer } = Begin(0, FiveCards());
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  writer.Write(begin.Events, begin.State);

  const kesa = InHand(begin.State, 'kesa_cut');
  const play = TurnLoop.PlayCard(begin.State, kesa, NoShuffle());
  const ev = single(writer.Write(play.Events, play.State));

  assert.equal(ev.Kind, DepictionEventKind.PlayCard);
  assert.equal(ev.WaitsForDrag, true);
  assert.equal(ev.CardId, kesa);
  assert.equal(ev.Aim, CardAim.Single);
  assert.equal(ev.Title, '袈裟斬り');
  assert.equal(ev.PreviewText, '20');
  // §2.2: cost, then the trait, then the attack face.
  assert.deepEqual(Kinds(ev), [CueKind.StaminaChange, CueKind.TraitFire, CueKind.Slash]);

  const stamina = ev.Cues[0];
  assert.equal(stamina.Amount, -2);
  assert.equal(stamina.StaminaAfter, 8);
  assert.equal(stamina.StaminaMax, 10);

  assert.equal(ev.Cues[1].Text, '間合い0 +5');

  const slash = ev.Cues[2];
  assert.equal(slash.Source, UnitSide.Player, 'the attacker is always named (#29)');
  assert.equal(slash.Target, UnitSide.Enemy);
  assert.equal(slash.Amount, 20);
  assert.equal(slash.Intensity, EffectStrength.Uniform, '§5.3: one strength for every beat (#30)');
  assert.equal(slash.HpAfter, 40);

  assert.equal(ev.After.Enemy.Hp, 40);
  assert.equal(ev.After.Player.Stamina, 8);
  assert.equal(ev.After.Hand.length, 4);
});

test('AttackThenMoveThenGuard_PlayInThatOrder', () => {
  const { state, writer } = Begin(0, FiveCards());
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  writer.Write(begin.Events, begin.State);

  const play = TurnLoop.PlayCard(begin.State, InHand(begin.State, 'feint'), NoShuffle());
  const ev = single(writer.Write(play.Events, play.State));

  assert.deepEqual(Kinds(ev), [CueKind.StaminaChange, CueKind.TraitFire, CueKind.Slash, CueKind.RangeSwitch, CueKind.GuardGain]);
  const move = ev.Cues[3];
  assert.equal(move.Target, UnitSide.Player);
  assert.equal(move.RangeAfter, RangeSide.Near, 'gap 1 still stands in the near slot');
  assert.equal(move.RangeGlyphAfter, '1');
  const guard = ev.Cues[4];
  assert.equal(guard.Amount, 3);
  assert.equal(guard.GuardAfter, 3);
});

test('AStatus_GetsItsOwnBeat_AndAChipOnTheFrame', () => {
  const { state, writer } = Begin(0, FiveCards());
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  writer.Write(begin.Events, begin.State);

  const play = TurnLoop.PlayCard(begin.State, InHand(begin.State, 'body_check'), NoShuffle());
  const ev = single(writer.Write(play.Events, play.State));

  const status = single(ev.Cues.filter((c) => c.Kind === CueKind.StatusChange));
  assert.equal(status.Target, UnitSide.Enemy);
  assert.equal(status.Text, '鈍足');
  assert.equal(status.Amount, 2);
  assert.equal(status.StacksAfter, 2);

  const chip = single(ev.After.Enemy.Statuses);
  assert.equal(chip.Label, '鈍足');
  assert.equal(chip.Stacks, 2);
});

test('ACappedTopUp_BeatsOnlyWhatLanded_AndAFullWordGetsNoBeat', () => {
  // #205: a ターンで減る型 word stops at four. The beat shows what the core says landed.
  const deck = Cards.BuildDeck([CardCatalog.SecondWind], 2);
  deck.push(...Cards.BuildDeck([CardCatalog.Brace, CardCatalog.Feint, CardCatalog.KesaCut], 1));
  const setup = new BattleSetup(Enemies.PolearmWarped, deck, BattleSetup.SliceFieldCells, { StartGap: 3 });
  let state = TurnLoop.Start(setup, NoShuffle()).State;
  state = state.with({ Player: state.Player.with({ Statuses: StatusSet.Of([StatusKind.Regen, 3]) }) });
  const writer = new CoreScriptWriter(setup.Enemy);
  writer.Opening(state);
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  writer.Write(begin.Events, begin.State);
  assert.equal(begin.State.Player.Statuses.Stacks(StatusKind.Regen), 2, 'the turn start ticked 3 → 2');

  const first = TurnLoop.PlayCard(begin.State, InHand(begin.State, 'second_wind'), NoShuffle());
  const topUp = single(single(writer.Write(first.Events, first.State)).Cues.filter((c) => c.Kind === CueKind.StatusChange));
  assert.equal(topUp.Amount, 2, '2 + 3 stops at 4: two of the three land');
  assert.equal(topUp.StacksAfter, 4);
  assert.equal(StatusBeat.Of(topUp), EffectId.StatusStack, 'the held chip grows; it is not a new one');

  const second = TurnLoop.PlayCard(first.State, InHand(first.State, 'second_wind'), NoShuffle());
  const full = single(writer.Write(second.Events, second.State));
  assert.equal(full.Cues.some((c) => c.Kind === CueKind.StatusChange), false, '4 stays 4: no chip beat');
  assert.equal(single(full.After.Player.Statuses.filter((c) => c.Label === '再生')).Stacks, 4);
});

test('ASelfCard_IsThrownAboveTheLine_AndPreviewsItsGuard', () => {
  const source = new CoreBattleSource(
    new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(FiveCards(), 1), BattleSetup.SliceFieldCells), 1);
  source.AdvanceAuto();
  const brace = source.Frame.Hand.find((c) => c.Name === '呼吸を整える').Id;

  assert.equal(source.PreviewFor(brace), '9');
  assert.equal(source.TryPlay(brace, DropZone.Receiver).ok, PlayVerdict.WrongZone);
  const r = source.TryPlay(brace, DropZone.AboveThrowLine);
  assert.equal(r.ok, PlayVerdict.Accepted);
  assert.equal(r.value.Aim, CardAim.Self);
});

// ---- ending the turn ----

test('EndTurn_IsThreeEvents_TurnEnd_EnemyAction_NextOmen_PlusTheWall', () => {
  const { state, writer } = Begin(0, FiveCards());
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  writer.Write(begin.Events, begin.State);

  const end = TurnLoop.EndTurn(begin.State, NoShuffle());
  const events = writer.Write(end.Events, end.State);

  assert.deepEqual(events.map((e) => e.Kind), [
    DepictionEventKind.TurnEnd, DepictionEventKind.EnemyAction, DepictionEventKind.EnemyAction, DepictionEventKind.NextOmen,
  ]);
  assert.deepEqual(events.map((e) => e.Order), [2, 3, 4, 5]);

  // Turn end: 構え (10 stamina left), then the hand goes.
  const turnEnd = events[0];
  assert.deepEqual(Kinds(turnEnd), [CueKind.StanceCue, CueKind.DiscardHand]);
  assert.equal(turnEnd.Cues[0].Text, '+3');
  assert.equal(turnEnd.Cues[0].GuardAfter, 3);
  assert.equal(turnEnd.Cues[1].Amount, 5);
  assert.deepEqual(turnEnd.After.Hand, []);
  assert.equal(turnEnd.After.Player.Guard, 3);
  assert.equal(turnEnd.After.StanceHint, '');

  // The shove: 6 into Guard 3 → 3 blocked, 3 through; the two-cell push moves the player one
  // (cell 1), and the wall takes 3 more in an event of its own (§7.3).
  const shove = events[1];
  assert.equal(shove.Title, '柄尻の押し込み');
  assert.deepEqual(Kinds(shove), [CueKind.EnemyWindup, CueKind.Slash, CueKind.GuardBlock, CueKind.Hit, CueKind.RangeSwitch]);
  assert.equal(shove.Cues[1].Source, UnitSide.Enemy);
  assert.equal(shove.Cues[1].HpAfter, Cue.Unchanged, 'an enemy slash is the swing alone');
  assert.equal(shove.Cues[2].Amount, 3);
  assert.equal(shove.Cues[2].GuardAfter, 0);
  assert.equal(shove.Cues[3].Amount, 3);
  assert.equal(shove.Cues[3].HpAfter, 47);
  assert.equal(shove.Cues[4].Target, UnitSide.Player);
  assert.equal(shove.Cues[4].RangeGlyphAfter, '1');
  assert.equal(shove.After.Omen.Visible, false, 'the omen is spent');
  assert.equal(shove.After.Enemy.Guard, 0, 'its 構え has not played yet');

  const wall = events[2];
  assert.equal(wall.Title, '壁に当たる');
  assert.deepEqual(Kinds(wall), [CueKind.Hit]);
  assert.equal(wall.Cues[0].Target, UnitSide.Player);
  assert.equal(wall.Cues[0].Amount, 3);
  assert.equal(wall.Cues[0].HpAfter, 44);
  assert.equal(wall.After.Player.Hp, 44);

  // Next omen: the enemy's 構え goes up, then the sweep is announced from the 1〜2 branch.
  const next = events[3];
  assert.deepEqual(Kinds(next), [CueKind.GuardGain, CueKind.OmenShow]);
  assert.equal(next.Cues[0].Target, UnitSide.Enemy);
  assert.equal(next.Cues[0].GuardAfter, 3);
  assert.equal(next.After.Enemy.Guard, 3);
  assert.equal(next.After.Omen.KindLabel + '・' + next.After.Omen.SideGlyph, '攻撃・1〜2');
  assert.equal(next.After.Omen.Icon, OmenIcon.Attack);
  assert.equal(next.After.Omen.ValueText, '9');
});

test('ASweepThatFindsThePlayerAtGapOne_StrikesItsBonusOff', () => {
  const kinds = [CardCatalog.StepInGuard, CardCatalog.KesaCut, CardCatalog.Brace, CardCatalog.Feint, CardCatalog.Thrust];
  const { state, writer } = Begin(2, kinds);
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  writer.Write(begin.Events, begin.State);
  const play = TurnLoop.PlayCard(begin.State, InHand(begin.State, 'step_in_guard'), NoShuffle());
  writer.Write(play.Events, play.State);

  const end = TurnLoop.EndTurn(play.State, NoShuffle());
  const sweep = writer.Write(end.Events, end.State)[1];

  assert.equal(sweep.Title, '払い斬り');
  const miss = single(sweep.Cues.filter((c) => c.Kind === CueKind.SideBonusMiss));
  assert.equal(miss.Text, '+3');
  assert.equal(sweep.Cues.some((c) => c.Kind === CueKind.TraitFire), false);
  assert.equal(single(sweep.Cues.filter((c) => c.Kind === CueKind.Slash)).Amount, 9);
});

test('ASweepThatWhiffs_StrikesItsWholeNumberOff_AndHitsNobody', () => {
  // Declared at gap 2; the boar rush closes to gap 0, outside the sweep's 1〜2 (§6).
  const kinds = [CardCatalog.BoarRush, CardCatalog.KesaCut, CardCatalog.Brace, CardCatalog.Feint, CardCatalog.Thrust];
  const { state, writer } = Begin(2, kinds);
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  writer.Write(begin.Events, begin.State);
  const play = TurnLoop.PlayCard(begin.State, InHand(begin.State, 'boar_rush'), NoShuffle());
  const afterRush = writer.Write(play.Events, play.State)[0].After;
  assert.equal(afterRush.Player.RangeGlyph, '0');
  assert.equal(afterRush.Player.Range, RangeSide.Near);

  const end = TurnLoop.EndTurn(play.State, NoShuffle());
  const events = writer.Write(end.Events, end.State);
  const sweep = events[1];

  assert.equal(sweep.Title, '払い斬り');
  assert.deepEqual(Kinds(sweep), [CueKind.EnemyWindup, CueKind.SideBonusMiss], 'one strike-off, not two');
  assert.equal(sweep.Cues[1].Text, '空振り');
  assert.equal(sweep.Cues[1].Amount, 9);
  assert.equal(sweep.After.Player.Hp, 50);
  assert.equal(events[2].After.Omen.KindLabel + '・' + events[2].After.Omen.SideGlyph, '攻撃・0', 'the adjacent branch now');
});

test('TheEnemysStep_MovesThePlayersTag_UntilTheFloorHasCells', () => {
  const { state, writer } = Begin(3, FiveCards());
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  writer.Write(begin.Events, begin.State);

  const end = TurnLoop.EndTurn(begin.State, NoShuffle());
  const step = writer.Write(end.Events, end.State)[1];

  assert.equal(step.Title, '踏み込み');
  assert.deepEqual(Kinds(step), [CueKind.EnemyWindup, CueKind.RangeSwitch, CueKind.GuardGain]);
  assert.equal(step.Cues[1].Target, UnitSide.Player);
  assert.equal(step.Cues[1].RangeGlyphAfter, '2');
  assert.equal(step.Cues[1].RangeAfter, RangeSide.Far);
  assert.equal(step.After.Player.RangeGlyph, '2');
});

test('TheSecondTurnStart_DoesNotPopTheOmenTwice_AndClearsTheGuard', () => {
  const { state, writer } = Begin(1, [...PrototypeDeck.Kinds]);
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  writer.Write(begin.Events, begin.State);
  // Spend 7 so that turn 2 has a real recovery to show.
  let s = begin.State;
  for (const id of ['thrust', 'kesa_cut', 'brace']) {
    const play = TurnLoop.PlayCard(s, InHand(s, id), NoShuffle());
    writer.Write(play.Events, play.State);
    s = play.State;
  }
  const end = TurnLoop.EndTurn(s, NoShuffle());
  writer.Write(end.Events, end.State);

  const second = TurnLoop.BeginPlayerTurn(end.State, NoShuffle());
  const ev = single(writer.Write(second.Events, second.State));

  assert.deepEqual(Kinds(ev), [CueKind.GuardReset, CueKind.StaminaChange, CueKind.DrawHand]);
  assert.equal(ev.Cues[1].Amount, 3);
  assert.equal(ev.Cues[1].StaminaAfter, 6);
  assert.equal(ev.After.Omen.Visible, true);
  assert.equal(ev.After.Corner.Turn, 2);
});

// ---- a hand-written stream ----

test('TheWriter_OnlyCopiesSettledValues_EvenFromAStreamNoBattleProduced', () => {
  const state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle()).State;
  const writer = new CoreScriptWriter(state.EnemyDef);
  writer.Opening(state);

  const stream = [
    new TurnStarted(Actor.Player, 7),
    new GuardCleared(Actor.Player, 4),
    new StaminaRecovered(Actor.Player, 1, 2, 10),
    new OmenSet(Actor.Enemy, state.Omen, false),
  ];
  const ev = single(writer.Write(stream, state));

  assert.deepEqual(Kinds(ev), [CueKind.GuardReset, CueKind.StaminaChange, CueKind.OmenShow]);
  assert.equal(ev.Cues[1].Amount, 1);
  assert.equal(ev.After.Player.Stamina, 2);
  assert.equal(ev.After.Corner.Turn, 7);

  const hit = [
    new GuardCleared(Actor.Enemy, 0),
    new ActionExecuted(Actor.Enemy, Enemies.PolearmWarped.Actions.get('reach_thrust'), 0),
    new DamageDealt(Actor.Enemy, Actor.Player, 99, 90, 9, 1, 41),
    new DefeatChecked(Actor.Enemy, GameResult.Ongoing),
  ];
  const enemy = single(writer.Write(hit, state));

  assert.equal(enemy.Title, '穂先の突き');
  assert.equal(single(enemy.Cues.filter((c) => c.Kind === CueKind.GuardBlock)).Amount, 90);
  assert.equal(single(enemy.Cues.filter((c) => c.Kind === CueKind.Hit)).HpAfter, 41);
  assert.equal(enemy.After.Player.Hp, 41);
  assert.equal(enemy.After.Player.Guard, 1);
});

test('AGuardOmen_IsWrittenWithTheShieldAndTheFaceGuard', () => {
  const state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle()).State;
  const writer = new CoreScriptWriter(state.EnemyDef);
  writer.Opening(state);
  const guardUp = Enemies.PolearmWarped.Actions.get('guard_up');

  const stream = [
    new TurnStarted(Actor.Player, 2),
    new OmenSet(Actor.Enemy, new Omen(guardUp.Id, EnemyAi.LabelOf(guardUp)), false),
  ];
  const omen = single(writer.Write(stream, state)).After.Omen;

  // Not the omen the state stands on, so no core preview is taken: the face's Guard is shown.
  assert.equal(omen.Icon, OmenIcon.Guard);
  assert.equal(omen.KindLabel, '防御');
  assert.equal(omen.ValueText, '3');
});

test('ARest_IsAnEnemyActionWithNoBeats', () => {
  const state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle()).State;
  const writer = new CoreScriptWriter(state.EnemyDef);
  writer.Opening(state);

  const stream = [
    new GuardCleared(Actor.Enemy, 0),
    new StaminaRecovered(Actor.Enemy, 1, 1, 10),
    new Rested(Actor.Enemy, state.Omen),
    new ReserveChecked(Actor.Enemy, 1, 0, 0),
    new DefeatChecked(Actor.Enemy, GameResult.Ongoing),
  ];
  const ev = single(writer.Write(stream, state));

  assert.equal(ev.Kind, DepictionEventKind.EnemyAction);
  assert.equal(ev.Title, '敵は動けない');
  assert.deepEqual(ev.Cues, [], 'the enemy\'s stamina is not on screen, so its recovery has no beat');
});

test('TheStartOmen_IsFoldedIntoTheOpening_AndWritesNoEvent', () => {
  // TurnLoop.Start's first OmenSet arrives before any boundary: nothing is written for it.
  const state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle()).State;
  const writer = new CoreScriptWriter(state.EnemyDef);
  writer.Opening(state);
  assert.deepEqual(writer.Write([new OmenSet(Actor.Enemy, state.Omen, true)], state), []);
});
