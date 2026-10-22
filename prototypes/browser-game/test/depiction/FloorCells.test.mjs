// Port of the representative cases of unity-project-kit/Assets/View/Depiction/Bridge/Tests/FloorCellsTests.cs
// (#242: the floor of battle-visual-v1 §4.2 as the script carries it).
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { FloorCells } from '../../src/bridge/depiction/FloorCells.js';
import { CoreScriptWriter } from '../../src/bridge/depiction/CoreScriptWriter.js';
import { OmenHit, CueKind } from '../../src/bridge/depictionScript/DepictionTypes.js';
import { CardCatalog } from '../../src/core/battle/CardCatalog.js';
import { Cards } from '../../src/core/battle/Cards.js';
import { Enemies } from '../../src/core/battle/Enemies.js';
import { EnemyAi } from '../../src/core/battle/EnemyAi.js';
import { BattleSetup, TurnLoop } from '../../src/core/battle/TurnLoop.js';
import { FixedRng } from '../../src/core/battle/IRng.js';
import { CellsMoved } from '../../src/core/battle/BattleEvents.js';
import { Omen, OmenLabel, OmenKind, Reach, GameResult } from '../../src/core/battle/Types.js';

const NoShuffle = () => new FixedRng(0.9999999);

const Hand = () => [CardCatalog.KesaCut, CardCatalog.Feint, CardCatalog.Brace, CardCatalog.ReachThrust, CardCatalog.BackLeap];

/** The polearm on the slice's line at the given gap, the writer past the opening. */
function Begin(gap) {
  const setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(Hand(), 1), BattleSetup.SliceFieldCells, { StartGap: gap });
  const state = TurnLoop.Start(setup, NoShuffle()).State;
  const writer = new CoreScriptWriter(setup.Enemy);
  writer.Opening(state);
  return { state, writer };
}

const last = (arr) => arr[arr.length - 1];

// ---- §4.2 届くマスの光 ----

test('ACardAimedAtTheEnemy_ReachesTheCellsItsReachCountsFromThePlayersFront', () => {
  // The player on cell 2: gap 0 is cell 3, gap 1 is cell 4, gap 2 is cell 5.
  assert.deepEqual(FloorCells.ReachOf(CardCatalog.KesaCut, 2, 1, 6), [3, 4], '0〜1');
  assert.deepEqual(FloorCells.ReachOf(CardCatalog.ReachThrust, 2, 1, 6), [4, 5], '1〜2');
});

test('TheReach_StopsAtTheEndOfTheLine', () => {
  assert.deepEqual(FloorCells.ReachOf(CardCatalog.ReachThrust, 4, 1, 6), [6]);
  assert.deepEqual(FloorCells.ReachOf(CardCatalog.ReachThrust, 5, 1, 6), []);
});

test('ACardAimedAtNobody_LightsNoCell', () => {
  assert.deepEqual(FloorCells.ReachOf(CardCatalog.Brace, 2, 1, 6), []);
  assert.deepEqual(FloorCells.ReachOf(CardCatalog.BackLeap, 2, 1, 6), []);
});

test('ReachOf_RefusesANullCard', () => {
  assert.throws(() => FloorCells.ReachOf(null, 2, 1, 6), (e) => e.name === 'ArgumentNullException');
});

// ---- §4.2 狙うマス / §4.4 当たり外れの印 ----

test('TheAimedCells_AreTheCoresTargetCells_FromWhereTheEnemyStands', () => {
  const { state } = Begin(3);
  const omen = new Omen('thrust', new OmenLabel(OmenKind.Attack, new Reach(0, 1)));

  // The enemy on cell 6: gap 0 is cell 5, gap 1 is cell 4, lowest first.
  assert.deepEqual(FloorCells.AimOf(omen, state.Enemy, 6), [4, 5]);
  assert.deepEqual(FloorCells.AimOf(omen, state.Enemy, 6), [...EnemyAi.TargetCells(omen.Label, state.Enemy.with({ Cell: 6 }))]);
  assert.deepEqual(FloorCells.AimOf(new Omen('crouch', new OmenLabel(OmenKind.Guard)), state.Enemy, 6), []);
  assert.deepEqual(FloorCells.AimOf(null, state.Enemy, 6), []);
});

test('TheHitMark_SaysWhetherThePlayerStandsInTheAimedCells', () => {
  const omen = new Omen('thrust', new OmenLabel(OmenKind.Attack, new Reach(0, 1)));
  const aim = [4, 5];

  assert.equal(FloorCells.HitOf(omen, aim, 4, 1), OmenHit.Lands);
  assert.equal(FloorCells.HitOf(omen, aim, 2, 1), OmenHit.Misses);
  assert.equal(FloorCells.HitOf(omen, aim, 3, 2), OmenHit.Lands, 'a body on 3-4: its near edge, 4, is aimed at');
  assert.equal(FloorCells.HitOf(omen, [2], 2, 2), OmenHit.Misses,
    'a body on 2-3: its far cell, 2, does not count; the core measures N from the near edge (FarCell)');
  assert.equal(FloorCells.HitOf(new Omen('crouch', new OmenLabel(OmenKind.Guard)), aim, 4, 1), OmenHit.None,
    'an action that aims at nobody has no mark');
});

// ---- the writer ----

test('TheWritersFrame_CarriesTheFloorAsTheCoreStands', () => {
  const { state, writer } = Begin(1);
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  const frame = last(writer.Write(begin.Events, begin.State)).After;
  const now = begin.State;

  assert.equal(frame.Floor.Cells, now.FieldCells);
  assert.equal(frame.Floor.PlayerCell, now.Player.Cell);
  assert.equal(frame.Floor.EnemyCell, now.Enemy.Cell);
  assert.equal(frame.Floor.EnemySize, now.Enemy.Size);
  assert.equal(frame.Floor.Gap, now.GapTo(0));
  assert.equal(frame.Omen.Visible, true, 'step 5 shows the first omen');
  assert.deepEqual(frame.Floor.AimCells, [...EnemyAi.TargetCells(now.Omen.Label, now.Enemy)]);
  const preview = TurnLoop.PreviewOmen(now, 0);
  const expected = now.Omen.Label.Reach == null ? OmenHit.None : preview.Lands ? OmenHit.Lands : OmenHit.Misses;
  assert.equal(frame.Omen.Hit, expected);
});

test('TheHitMark_IsTheCoresLands_EvenWhenTheEnemysTurnStartMovesIt', () => {
  // 歪みの根 at stage 3 sways a cell forward or back at its own turn start (TurnLoop.Sway),
  // before it acts. The core's OmenPreview plays that sway; the cells drawn now do not.
  const root = Enemies.DistortionRoot;
  const setup = new BattleSetup(root, Cards.BuildDeck(Hand(), 1), 8, { StartGap: 3 });
  let s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, NoShuffle()).State, NoShuffle()).State;
  s = s.WithEnemy(s.Enemy.with({ Hp: 60 }));
  s = TurnLoop.EndTurn(s, NoShuffle()).State; // stages 2 and 3 are held from this step 12
  s = s.WithEnemy(s.Enemy.with({ Cell: s.FieldCells - s.Enemy.Size }));

  let differing = 0;
  for (let cell = 1; cell < s.Enemy.Cell; cell++) {
    const at = s.with({ Player: s.Player.with({ Cell: cell }) });
    const writer = new CoreScriptWriter(root);
    writer.Opening(at);
    const begin = TurnLoop.BeginPlayerTurn(at, NoShuffle());
    const frame = last(writer.Write(begin.Events, begin.State)).After;
    const now = begin.State;
    if (now.Result !== GameResult.Ongoing || now.Omen == null || now.Omen.Label.Reach == null) continue;
    const preview = TurnLoop.PreviewOmen(now, 0);
    if (preview == null || preview.Rests) continue;

    assert.equal(frame.Omen.Hit, preview.Lands ? OmenHit.Lands : OmenHit.Misses, 'player on cell ' + cell);
    const fromCellsNow = FloorCells.HitOf(now.Omen, frame.Floor.AimCells, now.Player.Cell, now.Player.Size);
    if (fromCellsNow !== frame.Omen.Hit) differing++;
  }
  assert.ok(differing > 0, 'the sway makes the core\'s Lands differ from the cells drawn on some cell');
});

test('TheOpening_AimsAtNoCell_WhileTheOmenIsHidden', () => {
  const { state, writer } = Begin(1);
  const frame = writer.Opening(state);

  assert.equal(frame.Floor.Cells, BattleSetup.SliceFieldCells);
  assert.deepEqual(frame.Floor.AimCells, []);
  assert.equal(frame.Omen.Hit, OmenHit.None);
});

test('EachCardOfTheHand_CarriesTheCellsItReaches', () => {
  const { state, writer } = Begin(1);
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  const frame = last(writer.Write(begin.Events, begin.State)).After;
  const player = begin.State.Player.Cell;

  assert.deepEqual(frame.Hand.find((c) => c.Name === '袈裟斬り').ReachCells, [player + 1, player + 2]);
  assert.deepEqual(frame.Hand.find((c) => c.Name === '伸び突き').ReachCells, [player + 2, player + 3]);
  assert.deepEqual(frame.Hand.find((c) => c.Name === '呼吸を整える').ReachCells, []);
});

test('AMove_CarriesWhereBothStandAfterIt', () => {
  const { state, writer } = Begin(0);
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  writer.Write(begin.Events, begin.State);

  const feint = begin.State.Hand.find((c) => c.Def.Id === 'feint').InstanceId;
  const play = TurnLoop.PlayCard(begin.State, feint, NoShuffle());
  const ev = writer.Write(play.Events, play.State)[0];
  const move = last(ev.Cues.filter((c) => c.Kind === CueKind.RangeSwitch));

  assert.equal(move.PlayerCellAfter, play.State.Player.Cell);
  assert.equal(move.EnemyCellAfter, play.State.Enemy.Cell);
  assert.equal(move.GapAfter, play.State.GapTo(0));
  assert.equal(ev.After.Floor.Gap, move.GapAfter);
});

for (const [cardId, gap, cells] of [['feint', 0, 1], ['boar_rush', 2, 2]]) {
  test(`AMove_CarriesHowManyCellsItWalked(${cardId}, ${gap}, ${cells})`, () => {
    // battle_ui_ux_v2 §5.7 順 1 (#586): the walk takes 160 ms a cell.
    const def = CardCatalog.All.find((c) => c.Id === cardId);
    const setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck([def, CardCatalog.Brace], 3),
      BattleSetup.SliceFieldCells, { StartGap: gap });
    const state = TurnLoop.Start(setup, NoShuffle()).State;
    const writer = new CoreScriptWriter(setup.Enemy);
    writer.Opening(state);
    const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
    writer.Write(begin.Events, begin.State);

    const card = begin.State.Hand.find((c) => c.Def.Id === cardId).InstanceId;
    const play = TurnLoop.PlayCard(begin.State, card, NoShuffle());
    const ev = writer.Write(play.Events, play.State)[0];

    const walked = play.Events.filter((e) => e instanceof CellsMoved).map((m) => Math.abs(m.To - m.From));
    assert.deepEqual(walked, [cells], cardId + ' from gap ' + gap);
    assert.deepEqual(ev.Cues.filter((c) => c.Kind === CueKind.RangeSwitch).map((c) => c.CellsMoved), walked);
  });
}
