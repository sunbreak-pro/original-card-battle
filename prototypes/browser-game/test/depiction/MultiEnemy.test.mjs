// JS extension: battles of two and three enemies through CoreBattleSource
// (docs/port-notes/depiction-multi-enemy.md). The C# writer models the first enemy only and throws
// KeyNotFoundException in the two-enemy fight the exploration hands in on layers 2〜3; there is no
// C# test to port, so these are the port's own.
//
// 1. One-enemy battles: every member the C# writes comes out exactly as before the extension
//    (MultiEnemy.single.golden.json, written from the code before it by _singleEnemyRuns.mjs), and
//    the members added are exactly the documented ones.
// 2. The roster pair (enemy_roster_v4 §9: polearm_warped in front, crossbow_hunter behind, 7 cells)
//    × seeds 1..40, and every two- and three-enemy line the roster can make that fits a field, run
//    unattended to the end (BattleLaunch with AutoPlay, as EncounterBattle.StartNext(true) builds
//    it). After every screen event the frame keeps the per-enemy invariants; once a move has played
//    out (nothing pending) each enemy's HP, Guard, stamina, statuses, stances, cell, N, omen, plan and
//    aimed cells equal the core state for that enemy; at each wait every card's default target (the
//    cheapest receiver), preview and refusal are the core's for that target, and its cost, ▼,
//    構えの予告 and refusal texts on each enemy are the core's on that enemy (石礫's cost depends on
//    the gap; the pair is also fought with it in the deck). The broad sweep runs seeds 1..2 by default;
//    MULTI_ENEMY_SEEDS=40 runs every line × seeds 1..40 (slow).
// 3. The exploration path: an Expedition walked to a layer 2 or 3 elite node, which draws the pair
//    (LayerEncounters), fought for real through EncounterBattle and brought back.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { enumIndex } from '../../src/shared/cs.js';
import { Enemies } from '../../src/core/battle/Enemies.js';
import { Constants } from '../../src/core/battle/Constants.js';
import { BattleSetup, PlayRefusal, TurnLoop } from '../../src/core/battle/TurnLoop.js';
import { BattlePhase, CardInstance, GameResult, StatusKind } from '../../src/core/battle/Types.js';
import { Field } from '../../src/core/battle/Field.js';
import { CardCatalog, PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { Cards } from '../../src/core/battle/Cards.js';
import { BattleLaunch } from '../../src/bridge/depiction/BattleLaunch.js';
import { CoreBattleSource } from '../../src/bridge/depiction/CoreBattleSource.js';
import { CoreScriptWriter } from '../../src/bridge/depiction/CoreScriptWriter.js';
import { CoreText } from '../../src/bridge/depiction/CoreText.js';
import { FloorCells } from '../../src/bridge/depiction/FloorCells.js';
import { StatusChipText } from '../../src/bridge/depiction/StatusChipText.js';
import {
  BattleOutcome, CardAim, CardFace, ChipKind, CueKind, DepictionEventKind, DepictionText, DropZone, EnemyHatch, FloorEnemy, PlayVerdict,
  UnitSide,
} from '../../src/bridge/depictionScript/DepictionTypes.js';
import { OmenDisclosure } from '../../src/bridge/depictionScript/OmenDisclosure.js';
import { NodeKind } from '../../src/core/dungeon/NodeKind.js';
import { BattleField } from '../../src/core/dungeon/BattleField.js';
import { LayerEncounters } from '../../src/core/dungeon/LayerEncounters.js';
import { RunPhase } from '../../src/core/dungeon/ExplorationState.js';
import { RunStart } from '../../src/bridge/explorationScript/ExplorationSession.js';
import { ScreenAction } from '../../src/bridge/explorationScript/ExplorationScreen.js';
import { Expedition } from '../../src/bridge/exploration/Expedition.js';
import { EndEveryFight } from '../exploration/_battle.mjs';
import { GOLDEN, loadPort, scenarios, run, project, hash } from './_singleEnemyRuns.mjs';

const PAIR = ['polearm_warped', 'crossbow_hunter'];
const PAIR_CELLS = 7; // roster §9 / battle_core_v4 §7.4: 組の場は 7 マス
const MAX_MOVES = 6000;
const SWEEP_SEEDS = Math.max(1, Number(process.env.MULTI_ENEMY_SEEDS || 2));

/** Every member the extension adds (docs/port-notes/depiction-multi-enemy.md), as Type.Member. */
const NEW_MEMBERS = new Set([
  'CardFace.CostOn', 'CardFace.ReachOn', 'CardFace.StanceHintAfterPayOn', 'CardFace.TargetUnit',
  'Cue.Unit', 'Cue.UnitCellAfter', 'Cue.UnitGapAfter',
  'DepictionEvent.Unit',
  'DepictionFrame.Enemies', 'DepictionFrame.Omens', 'DepictionFrame.SwatchesShown',
  'FloorFrame.Enemies',
  'OmenFrame.Hatch', 'OmenFrame.Unit',
  'UnitFrame.Cell', 'UnitFrame.Gap', 'UnitFrame.GapGlyph', 'UnitFrame.Hatch', 'UnitFrame.Size', 'UnitFrame.Unit',
]);

// ============================================================================ 1. one enemy, as before

test('OneEnemyBattles_WriteEveryCSharpMember_AsBeforeTheExtension (snapshot)', async () => {
  const golden = JSON.parse(fs.readFileSync(GOLDEN, 'utf8'));
  const keys = new Map(Object.entries(golden.keys).map(([t, ks]) => [t, new Set(ks)]));
  const P = await loadPort();
  const specs = scenarios(P);
  assert.equal(specs.length, golden.scenarios.length, 'the same scenarios as the snapshot');
  const added = new Set();
  for (let s = 0; s < specs.length; s++) {
    const spec = specs[s];
    const want = golden.scenarios[s];
    assert.equal(spec.id, want.id);
    const records = run(P, spec);
    const got = records.map((c) => hash(project(c, keys, added)));
    const expected = want.hashes.split(' ');
    const first = got.findIndex((h, i) => h !== expected[i]);
    assert.ok(first < 0 && got.length === expected.length,
      `${spec.id}: record ${first < 0 ? got.length : first} differs from the one-enemy snapshot (${got.length} records, snapshot ${expected.length})`);
  }
  assert.deepEqual([...added].sort(), [...NEW_MEMBERS].sort(), 'the members added are the documented ones');
});

test('OneEnemyBattles_WriteUnitZero_TheirOneSwatchlessEnemy', () => {
  const source = new BattleLaunch({ Seed: 3, AutoPlay: true }).CreateSource();
  const ctx = fight(source, 'slice|3');
  assert.equal(ctx.n, 1);
  const frame = source.Frame;
  assert.equal(frame.SwatchesShown, false);
  assert.equal(frame.Enemies.length, 1);
  assert.equal(frame.Enemy, frame.Enemies[0], 'Frame.Enemy is the one enemy');
  assert.equal(frame.Omen, frame.Omens[0]);
  assert.equal(frame.Enemy.Unit, 0);
});

// ============================================================================ 2. two and three enemies

test('TheRosterPair_SeedsOneToForty_PlaysToTheEndUnattended_FrameFollowsTheCore', () => {
  const tally = { Won: 0, Lost: 0, defeats: 0, hitBack: 0 };
  for (let seed = 1; seed <= 40; seed++) {
    const source = launch(PAIR, seed, PAIR_CELLS);
    const ctx = fight(source, `pair|${seed}`);
    tally[source.State.Result] += 1;
    tally.defeats += ctx.defeats.length;
    tally.hitBack += ctx.hitBy.has(1) ? 1 : 0;
  }
  assert.equal(tally.Won + tally.Lost, 40);
  assert.ok(tally.Won > 0, 'some seeds are won: both enemies fall');
  assert.ok(tally.defeats > tally.Won * 2, 'some battles go on after one enemy has fallen');
  assert.ok(tally.hitBack > 0, 'the enemy behind is struck in some battles');
});

test('TheRosterPair_StartsAsTheRosterSays_TwoFramesTwoOmensTwoHatches', () => {
  const source = launch(PAIR, 1, PAIR_CELLS);
  const opening = source.Frame;
  assert.equal(opening.Enemies.length, 2);
  assert.equal(opening.SwatchesShown, true);
  assert.deepEqual(opening.Enemies.map((u) => [u.Unit, u.ArtId, u.Cell, u.Gap, u.Hatch]), [
    [0, 'polearm_warped', 6, 3, 0],
    [1, 'crossbow_hunter', 7, 4, 1],
  ], 'battle_core_v4 §7.4: 開始の間合いは 3 と 4');
  assert.equal(opening.Enemy, opening.Enemies[0], 'the one in front is the frame\'s enemy');
  assert.deepEqual(opening.Floor.Enemies.map((f) => [f.Unit, f.Cell, f.Hatch, f.Down]), [[0, 6, 0, false], [1, 7, 1, false]]);
  assert.ok(opening.Omens.every((o) => !o.Visible), 'the first omens wait for turn 1');
  const turn = source.AdvanceAuto();
  assert.equal(turn.Kind, DepictionEventKind.TurnStart);
  assert.deepEqual(turn.Cues.filter((c) => c.Kind === CueKind.OmenShow).map((c) => c.Unit), [0, 1], 'step 5 shows both omens');
  const omens = turn.After.Omens;
  assert.ok(omens[0].Visible && omens[1].Visible);
  // The bug the C# has: the crossbow's omen read against the polearm's actions. Here each is its own.
  const state = source.State;
  for (const k of [0, 1]) {
    const own = CoreText.OmenOf(state.Enemies[k].Omen, state.Enemies[k].Def);
    assert.equal(omens[k].KindLabel, own.KindLabel);
    assert.equal(omens[k].Icon, own.Icon);
  }
  assert.ok(turn.After.Floor.Enemies[1].AimCells.length > 0 || omens[1].SideGlyph === '', 'the crossbow aims at its own cells');
});

test('EveryTwoAndThreeEnemyLine_TheRosterCanMake_PlaysToTheEnd', () => {
  const ids = Enemies.All.map((d) => d.Id);
  const lines = [];
  for (const a of ids) for (const b of ids) lines.push([a, b]);
  for (const a of ids) for (const b of ids) for (const c of ids) lines.push([a, b, c]);
  let fitted = 0;
  let threes = 0;
  const results = { Won: 0, Lost: 0 };
  for (const line of lines) {
    const cells = fieldFor(line);
    if (cells < 0) continue;
    fitted += 1;
    if (line.length === 3) threes += 1;
    for (let seed = 1; seed <= SWEEP_SEEDS; seed++) {
      const source = launch(line, seed, cells);
      fight(source, `${line.join('+')}@${cells}|${seed}`);
      results[source.State.Result] += 1;
    }
  }
  // 13 × 13 ordered pairs less the 16 of two large enemies; the nine one-cell enemies in threes.
  assert.equal(fitted, 13 * 13 - 16 + 9 * 9 * 9);
  assert.equal(threes, 9 * 9 * 9);
  assert.equal(results.Won + results.Lost, fitted * SWEEP_SEEDS);
});

test('ACardForTheEnemyBehind_GoesToIt_WhenTheOneInFrontIsOutOfReach', () => {
  // A card goes to an enemy inside its reach whenever there is one, and a named target is refused
  // exactly as the core refuses it.
  const source = launch(PAIR, 7, PAIR_CELLS);
  while (!source.WaitingForPlayer) source.AdvanceAuto();
  const state = source.State;
  let checked = 0;
  for (const card of state.Hand) {
    const def = card.Def;
    if (!CoreScriptWriterAimsAtOne(def)) continue;
    const target = source.TargetFor(card.InstanceId);
    const inReach = state.Living.filter((k) => def.Face.ReachOrDefault.Contains(state.GapTo(k)));
    if (inReach.length > 0) assert.ok(inReach.includes(target), `${card.InstanceId} goes to an enemy in its reach`);
    for (const k of state.Living) {
      const expected = TurnLoop.CanPlay(state, card.InstanceId, k);
      const verdict = source.Inspect(card.InstanceId, k);
      if (expected === PlayRefusal.None) assert.equal(verdict, PlayVerdict.Accepted);
      if (expected === PlayRefusal.OutOfReach) assert.equal(verdict, PlayVerdict.OutOfRange);
      checked += 1;
    }
  }
  assert.ok(checked > 0);
});

test('ANamedTarget_IsTheOneTheCardHits_EvenWhereTheDefaultWouldGoElsewhere', () => {
  // The pair, unattended — except that while the enemy in front stands, a card that can be released
  // on the one behind is released there by name.
  let played = 0;
  let overridden = 0;
  for (let seed = 1; seed <= 40; seed++) {
    const source = launch(PAIR, seed, PAIR_CELLS);
    let guard = 0;
    while (!source.Finished && guard++ < 2000) {
      if (!source.WaitingForPlayer) {
        source.AdvanceAuto();
        continue;
      }
      const state = source.State;
      const behind = state.Enemies[0].Alive
        ? source.Frame.Hand.find((f) => f.Aim === CardAim.Single && source.Inspect(f.Id, 1) === PlayVerdict.Accepted)
        : null;
      if (behind != null) {
        const hpBefore = state.Enemies[1].Body.Hp;
        const preview = TurnLoop.Preview(state, behind.Id, 1);
        assert.equal(source.PreviewFor(behind.Id, 1), preview.Attacks ? String(preview.RawPower) : preview.GuardGain > 0 ? String(preview.GuardGain) : '');
        if (source.TargetFor(behind.Id) !== 1) overridden += 1;
        const r = source.TryPlay(behind.Id, DepictionText.RequiredZone(behind.Aim), 1);
        assert.equal(r.verdict, PlayVerdict.Accepted);
        assert.equal(r.played.Unit, 1, 'the PlayCard event names the enemy behind');
        if (preview.Attacks && preview.Damage > 0) {
          assert.ok(source.State.Enemies[1].Body.Hp < hpBefore, 'the enemy behind lost HP');
          assert.ok(r.played.Cues.some((c) => c.Kind === CueKind.Slash && c.Unit === 1), 'its slash is on unit 1');
          assert.equal(r.played.PreviewText, String(preview.RawPower), 'the played card shows the number read on unit 1');
        }
        played += 1;
        continue;
      }
      const id = source.SuggestedCardId;
      if (id.length > 0) source.TryPlay(id, DepictionText.RequiredZone(DepictionText.Find(source.Frame.Hand, id).Aim));
      else source.EndTurn();
    }
  }
  assert.ok(played > 0, 'some seed lets a card reach the enemy behind while the one in front stands');
  assert.ok(overridden > 0, 'and the default target was the one in front');
});

test('TheRosterPair_WithStoneThrows_SeedsOneToForty_ReadsEachCostOnItsEnemy', () => {
  // The prototype deck with 盾打ち swapped for 石礫 ×2 (a legal 20-card deck): 石礫's cost depends on
  // the enemy it is aimed at, so the per-enemy cost and the cheapest-first default target are met
  // in played battles too (checkWait).
  const kinds = PrototypeDeck.Kinds.map((d) => (d.Id === 'shield_bash' ? CardCatalog.StoneThrow : d));
  const deck = [];
  for (const def of kinds) for (let c = 0; c < 2; c++) deck.push(new CardInstance(`${def.Id}-${c}`, def));
  let costSplit = 0;
  let notNearest = 0;
  for (let seed = 1; seed <= 40; seed++) {
    const source = new BattleLaunch({ EnemyId: PAIR[0], MoreEnemyIds: PAIR.slice(1), Seed: seed, FieldCells: PAIR_CELLS, AutoPlay: true, Deck: deck }).CreateSource();
    const ctx = fight(source, `pair+stone|${seed}`);
    costSplit += ctx.costSplit;
    notNearest += ctx.notNearest;
  }
  assert.ok(costSplit > 0, 'some waits hold a card that costs differently on the two enemies');
  assert.ok(notNearest > 0, 'and some send it past the nearest enemy to the cheaper one');
});

test('OutOfReach_IsExplained_OnTheEnemyTheCardWasReleasedOn_AlsoWhileAnotherIsInReach (battle-visual-v1 §5.4)', () => {
  // §5.4 出せなかった理由: a card released on an enemy it cannot reach goes back to the hand with its
  // reason, the N of that enemy — also when another enemy is inside its reach. §5.1's "どの敵にも届かない
  // ときだけ" is the hover panel's rule (CardFace.ReachHint), which stays quiet then.
  let beside = 0;
  let alone = 0;
  for (let seed = 1; seed <= 40; seed++) {
    const source = launch(PAIR, seed, PAIR_CELLS);
    let guard = 0;
    while (!source.Finished && guard++ < 300) {
      if (source.WaitingForPlayer) {
        const state = source.State;
        for (const face of source.Frame.Hand) {
          if (face.Aim !== CardAim.Single) continue;
          const nobody = CoreText.ReachesNobody(state, face.Id);
          assert.equal(face.ReachHint !== '', nobody, `${face.Id}: the §5.1 panel only when no enemy is in reach`);
          for (const k of state.Living) {
            if (source.Inspect(face.Id, k) !== PlayVerdict.OutOfRange) continue;
            const r = source.TryPlay(face.Id, DepictionText.RequiredZone(face.Aim), k);
            assert.equal(r.verdict, PlayVerdict.OutOfRange, `${face.Id} on ${k}: refused for reach`);
            assert.equal(r.played, null);
            const text = source.RefusalText(face.Id, r.verdict, k);
            assert.equal(text, '「' + face.Name + '」は間合い ' + face.RequiredRangeGlyph + ' の相手にしか出せません。いまは ' + state.GapTo(k) + ' です',
              `${face.Id} on ${k}: the line names the N of the enemy the card was released on`);
            if (nobody) alone += 1;
            else beside += 1;
          }
          if (source.Inspect(face.Id) === PlayVerdict.OutOfRange) {
            assert.ok(nobody, 'the default target is refused for reach only when nobody is in reach');
            assert.ok(source.RefusalText(face.Id, PlayVerdict.OutOfRange).endsWith('いまは ' + state.GapTo(face.TargetUnit) + ' です'), 'unnamed: the N of TargetUnit');
          }
        }
        const id = source.SuggestedCardId;
        if (id) source.TryPlay(id, DepictionText.RequiredZone(DepictionText.Find(source.Frame.Hand, id).Aim));
        else source.EndTurn();
      } else {
        source.AdvanceAuto();
      }
    }
  }
  assert.ok(beside > 0 && alone > 0, `both cases met (another enemy in reach ${beside}, none ${alone})`);
});

test('ACardWhoseCostDependsOnTheEnemy_ShowsEachEnemysCost_AndIsRefusedWithIt (石礫, battle-visual-v1 §5.2 / §6.3)', () => {
  // 石礫 costs 2, 1 less at N 2 or more (CardCatalog.Initial). The player on cell 4 of the pair's line:
  // N 1 to the polearm (cell 6), N 2 to the crossbow (cell 7), so it costs 2 on unit 0 and 1 on unit 1.
  const setup = new BattleSetup(Enemies.ById(PAIR[0]), Cards.BuildDeck([CardCatalog.StoneThrow], 10), PAIR_CELLS,
    { PlayerStartCell: 4, StartGap: 1, MoreEnemies: [Enemies.ById(PAIR[1])], PlayerStartStamina: 0 });
  const source = new CoreBattleSource(setup, 1);
  while (!source.WaitingForPlayer) source.AdvanceAuto();
  const at = () => `stamina ${source.State.Player.Stamina}`;
  assert.deepEqual([source.State.GapTo(0), source.State.GapTo(1)], [1, 2]);
  const stone = () => source.Frame.Hand.find((f) => f.Id.startsWith('stone_throw'));
  let face = stone();
  assert.deepEqual(face.CostOn, [2, 1], 'the core\'s cost on each enemy');
  assert.deepEqual([0, 1].map((k) => TurnLoop.Preview(source.State, face.Id, k).Cost), [2, 1]);
  // At rest the face reads its cheapest receiver, as its lamp reads any enemy (§6.3 決定 6): a lit
  // コスト −1 lamp over a discounted cost circle.
  assert.equal(face.TargetUnit, 1);
  assert.equal(face.Cost, 1);
  assert.equal(face.CostDrop, 1);
  assert.equal(face.TraitLit, true);
  assert.deepEqual(face.LitOn, [false, true]);
  // Held over each enemy: that enemy's cost, ▼, lamp and 構えの予告 (stamina 3 at the first wait).
  assert.equal(source.State.Player.Stamina, 3);
  assert.deepEqual([face.CostFor(0), face.CostDropFor(0), face.LitFor(0), face.StanceHintAfterPayFor(0)], [2, 0, false, '']);
  assert.deepEqual([face.CostFor(1), face.CostDropFor(1), face.LitFor(1), face.StanceHintAfterPayFor(1)], [1, 1, true, '']);
  assert.deepEqual(face.StanceHintAfterPayOn, ['', '']);
  assert.equal(face.StanceHintAfterPay, face.StanceHintAfterPayFor(face.TargetUnit));
  // Spend it down on the crossbow until it costs too much on the polearm and not on the crossbow.
  while (source.State.Player.Stamina > 1) {
    const r = source.TryPlay(stone().Id, DropZone.Receiver, 1);
    assert.equal(r.verdict, PlayVerdict.Accepted, at());
    assert.equal(r.played.Unit, 1);
    while (!source.WaitingForPlayer) source.AdvanceAuto();
  }
  assert.equal(source.State.Enemies[1].Alive, true, 'the crossbow still stands');
  face = stone();
  assert.equal(source.Inspect(face.Id, 0), PlayVerdict.NotEnoughStamina, at());
  assert.equal(TurnLoop.CanPlay(source.State, face.Id, 0), PlayRefusal.NotEnoughStamina);
  assert.equal(source.TryPlay(face.Id, DropZone.Receiver, 0).verdict, PlayVerdict.NotEnoughStamina);
  assert.equal(source.RefusalText(face.Id, PlayVerdict.NotEnoughStamina, 0), '「石礫」はスタミナ 2 が要ります。残りは 1 です',
    'the cost on the enemy the card was released on');
  assert.equal(source.Inspect(face.Id, 1), PlayVerdict.Accepted);
  assert.equal(source.Inspect(face.Id), PlayVerdict.Accepted, 'unnamed, it goes where it can be paid for');
  // The last stamina on the crossbow; then unnamed it is refused with the cost of its cheapest receiver.
  assert.equal(source.TryPlay(face.Id, DropZone.Receiver, 1).verdict, PlayVerdict.Accepted);
  while (!source.WaitingForPlayer) source.AdvanceAuto();
  face = stone();
  assert.equal(source.State.Player.Stamina, 0);
  assert.equal(face.TargetUnit, 1);
  assert.equal(source.Inspect(face.Id), PlayVerdict.NotEnoughStamina);
  assert.equal(source.RefusalText(face.Id, PlayVerdict.NotEnoughStamina), '「石礫」はスタミナ 1 が要ります。残りは 0 です');
  assert.equal(source.RefusalText(face.Id, PlayVerdict.NotEnoughStamina, 1), '「石礫」はスタミナ 1 が要ります。残りは 0 です');
  assert.equal(source.RefusalText(face.Id, PlayVerdict.NotEnoughStamina, 0), '「石礫」はスタミナ 2 が要ります。残りは 0 です');
});

test('CardFace_PerEnemyCost_ReadsItsSlot_AndFallsBackToTheFace', () => {
  const face = new CardFace({ Cost: 1, PrintedCost: 2, StanceHintAfterPay: '+3', CostOn: [2, 1], StanceHintAfterPayOn: ['', '+3'] });
  assert.deepEqual([face.CostFor(0), face.CostFor(1), face.CostFor(2), face.CostFor(-1)], [2, 1, 1, 1]);
  assert.deepEqual([face.CostDropFor(0), face.CostDropFor(1), face.CostDropFor(5)], [0, 1, 1]);
  assert.deepEqual([face.StanceHintAfterPayFor(0), face.StanceHintAfterPayFor(1), face.StanceHintAfterPayFor(2)], ['', '+3', '+3']);
  // A source that writes none (the fixed script, the C#'s members only): the face's own values.
  const plain = new CardFace({ Cost: 3, PrintedCost: 3 });
  assert.deepEqual([plain.CostOn, plain.StanceHintAfterPayOn], [[], []]);
  assert.deepEqual([plain.CostFor(0), plain.CostDropFor(0), plain.StanceHintAfterPayFor(0)], [3, 0, null]);
});

test('EnemyHatch_TurnsEachEnemysStripesItsOwnWay (battle-visual-v1 §3.2)', () => {
  assert.deepEqual([0, 1, 2].map((h) => EnemyHatch.AngleOf(h)), [45, -45, 90]);
  assert.equal(EnemyHatch.Count, Constants.EnemiesMax);
  assert.deepEqual({ ...new FloorEnemy() }, { Unit: 0, Hatch: 0, Cell: 0, Size: 1, Gap: -1, Down: false, AimCells: [], DenseCells: [], MoveCells: [] });
});

// ============================================================================ 3. the exploration path

for (const layer of [2, 3]) {
  test(`TheExploration_HandsInThePair_OnALayer${layer}EliteNode_AndItIsFoughtThroughEncounterBattle`, () => {
    let fought = 0;
    for (let seed = 1n; seed <= 4n; seed++) {
      const expedition = new Expedition(new RunStart({ Seed: seed }));
      walkToLayer(expedition, layer);
      const session = expedition.Session;
      walkTo(expedition, NodeKind.Elite);
      assert.equal(session.HasPendingBattle, true, 'the elite node waits on its battle');
      const battle = expedition.StartEncounter();
      const fight0 = battle.HandIn.Encounter.Fights[0];
      assert.deepEqual([...fight0.EnemyIds], PAIR, 'LayerEncounters: the pair fills the elite slot');
      assert.equal(battle.HandIn.FieldCells[0], BattleField.CellsFor(layer, NodeKind.Elite, 2));
      assert.equal(battle.HandIn.FieldCells[0], PAIR_CELLS);
      while (!battle.Over) {
        const source = battle.StartNext(true);
        assert.equal(source.State.Enemies.length, 2);
        fight(source, `layer${layer}|${seed}`);
        battle.Finish(source);
      }
      const back = expedition.Return();
      assert.equal(back.Won, battle.Won);
      assert.equal(back.Died, !battle.Won);
      for (const id of PAIR) assert.ok(back.Disclosure.get(id) >= 1, 'the journal has met both');
      assert.equal(session.State.Phase === RunPhase.Fallen, back.Died);
      fought += 1;
    }
    assert.equal(fought, 4);
  });
}

// ============================================================================ the driver and its checks

function launch(ids, seed, cells) {
  return new BattleLaunch({ EnemyId: ids[0], MoreEnemyIds: ids.slice(1), Seed: seed, FieldCells: cells, AutoPlay: true }).CreateSource();
}

/** The narrowest line (5〜8 cells) the enemies stand on as the battle starts them, or −1 when none holds them. */
function fieldFor(ids) {
  const defs = ids.map((id) => Enemies.ById(id));
  for (let cells = Constants.FieldCellsMin; cells <= Constants.FieldCellsMax; cells++) {
    const setup = new BattleSetup(defs[0], PrototypeDeck.Build(), cells, { MoreEnemies: defs.slice(1) });
    const starts = setup.EnemyStartCells;
    try {
      Field.Validate(cells, setup.PlayerStartCell, 1, defs.map((d, i) => ({ Cell: starts[i], Size: d.Size })));
      return cells;
    } catch {
      // too narrow
    }
  }
  return -1;
}

function CoreScriptWriterAimsAtOne(def) {
  return CoreScriptWriter.AimsAtOne(def);
}

/**
 * Plays the source to the end as EncounterBattle's unattended runs do, checking every event, every
 * wait and the end of every move. Returns what it saw.
 */
function fight(source, label) {
  const n = source.State.Enemies.length;
  const ctx = { label, n, down: new Array(n).fill(false), lastOrder: 0, defeats: [], hitBy: new Set(), disclosure: source._writer.Disclosure, costSplit: 0, notNearest: 0 };
  checkFrame(source.Frame, ctx, 'opening');
  let moves = 0;
  while (!source.Finished) {
    assert.ok(++moves < MAX_MOVES, `${label}: no end after ${MAX_MOVES} moves`);
    let ev;
    if (source.WaitingForPlayer) {
      checkWait(source, ctx);
      const id = source.SuggestedCardId;
      if (id.length > 0) {
        const face = DepictionText.Find(source.Frame.Hand, id);
        const target = source.TargetFor(id);
        const r = source.TryPlay(id, DepictionText.RequiredZone(face.Aim));
        assert.equal(r.verdict, PlayVerdict.Accepted, `${label}: the suggested card plays`);
        ev = r.played;
        if (face.Aim === CardAim.Single) assert.equal(ev.Unit, target, `${label}: ${id} went to TargetFor's enemy`);
        else assert.equal(ev.Unit, -1);
      } else {
        ev = source.EndTurn();
      }
    } else {
      ev = source.AdvanceAuto();
    }
    checkEvent(ev, ctx);
    if (source._pending.length === 0) checkMoveEnd(source, ctx);
  }
  checkEnd(source, ctx);
  return ctx;
}

function checkEvent(ev, ctx) {
  const at = `${ctx.label} event ${ev.Order} (${ev.Kind} ${ev.Title})`;
  assert.equal(ev.Order, ctx.lastOrder + 1, `${at}: events are numbered in the order they play`);
  ctx.lastOrder = ev.Order;
  assert.ok(ev.Unit >= -1 && ev.Unit < ctx.n, `${at}: Unit ${ev.Unit}`);
  switch (ev.Kind) {
    case DepictionEventKind.EnemyAction:
    case DepictionEventKind.NextOmen:
    case DepictionEventKind.Defeat:
      assert.ok(ev.Unit >= 0, `${at}: an enemy's beat names its enemy`);
      break;
    case DepictionEventKind.TurnStart:
    case DepictionEventKind.TurnEnd:
      assert.equal(ev.Unit, -1, at);
      break;
    default:
      break;
  }
  for (const cue of ev.Cues) {
    const what = `${at} cue ${cue.Kind}`;
    assert.ok(cue.Unit >= -1 && cue.Unit < ctx.n, `${what}: Unit ${cue.Unit}`);
    const enemyCue = cue.Kind === CueKind.Defeat || cue.Kind === CueKind.OmenShow || cue.Kind === CueKind.EnemyWindup
      || cue.Kind === CueKind.SideBonusMiss || cue.Kind === CueKind.Slash || cue.Kind === CueKind.GuardBlock || cue.Kind === CueKind.Hit
      || (cue.Target === UnitSide.Enemy && cue.Kind !== CueKind.RangeSwitch);
    if (enemyCue) assert.ok(cue.Unit >= 0, `${what}: names its enemy`);
    else if (cue.Kind !== CueKind.RangeSwitch) assert.equal(cue.Unit, -1, `${what}: the player's alone`);
    if (cue.Kind === CueKind.RangeSwitch && cue.Unit >= 0) {
      assert.ok(cue.UnitCellAfter >= 1, `${what}: the moved enemy's cell`);
      assert.equal(cue.UnitGapAfter, Field.Gap(cue.PlayerCellAfter, ev.After.Floor.PlayerSize, cue.UnitCellAfter), `${what}: its N`);
    }
    if (cue.Kind === CueKind.Slash && cue.Source === UnitSide.Player) ctx.hitBy.add(cue.Unit);
    if (cue.Kind === CueKind.Defeat) assert.equal(cue.Unit, ev.Unit, `${what}: the beat's enemy falls`);
  }
  if (ev.Kind === DepictionEventKind.Defeat) {
    assert.equal(ctx.down[ev.Unit], false, `${at}: an enemy falls once`);
    assert.equal(ev.After.Enemies[ev.Unit].Down, true, `${at}: and is down from its beat on`);
    ctx.defeats.push(ev.Unit);
  }
  checkFrame(ev.After, ctx, at);
}

function checkFrame(frame, ctx, at) {
  const { n } = ctx;
  assert.equal(frame.Enemies.length, n, `${at}: one frame per enemy`);
  assert.equal(frame.Omens.length, n, `${at}: one omen per enemy`);
  assert.equal(frame.Floor.Enemies.length, n, `${at}: one floor entry per enemy`);
  assert.equal(frame.SwatchesShown, n >= 2, at);
  let focus = -1;
  const falling = [];
  for (let k = 0; k < n; k++) {
    const unit = frame.Enemies[k];
    const omen = frame.Omens[k];
    const floor = frame.Floor.Enemies[k];
    assert.equal(unit.Unit, k, at);
    assert.equal(unit.Hatch, k, at);
    assert.equal(omen.Unit, k, at);
    assert.equal(omen.Hatch, k, at);
    assert.equal(floor.Unit, k, at);
    assert.equal(floor.Hatch, k, at);
    assert.equal(floor.Cell, unit.Cell, at);
    assert.equal(floor.Size, unit.Size, at);
    assert.equal(floor.Down, unit.Down, at);
    assert.equal(floor.Gap, unit.Gap, at);
    if (ctx.down[k]) assert.equal(unit.Down, true, `${at}: unit ${k} stays down`);
    if (unit.Down) {
      assert.equal(omen.Visible, false, `${at}: a fallen enemy shows no omen`);
      assert.equal(omen.PlanVisible, false, `${at}: nor a plan`);
      assert.deepEqual(floor.AimCells, [], `${at}: nor aims`);
      assert.equal(unit.Gap, -1, at);
      assert.equal(unit.GapGlyph, '', at);
    } else if (unit.Gap === -1) {
      // Fallen in the core, its Defeat beat still to play: no N, and not the frame's enemy.
      assert.equal(unit.Hp, 0, `${at}: only a fallen enemy has no N`);
      assert.equal(unit.GapGlyph, '', at);
      falling.push(k);
    } else {
      assert.equal(unit.Gap, Field.Gap(frame.Floor.PlayerCell, frame.Floor.PlayerSize, unit.Cell), `${at}: unit ${k}'s own N`);
      assert.equal(unit.GapGlyph, String(unit.Gap), at);
      if (focus < 0 || unit.Cell < frame.Enemies[focus].Cell) focus = k;
    }
    if (!omen.Visible) assert.deepEqual(floor.AimCells, [], `${at}: a hidden omen aims at nothing`);
  }
  for (let k = 0; k < n; k++) ctx.down[k] = frame.Enemies[k].Down;
  // Standing enemies never share a cell (battle_core_v4 §7.1).
  const standing = frame.Enemies.filter((u) => !u.Down && u.Hp > 0);
  for (const a of standing) {
    for (const b of standing) {
      if (a !== b) assert.ok(a.Cell + a.Size - 1 < b.Cell || b.Cell + b.Size - 1 < a.Cell, `${at}: units ${a.Unit} and ${b.Unit} share a cell`);
    }
  }
  if (focus < 0 && falling.length > 0) focus = falling.reduce((a, b) => (frame.Enemies[b].Cell < frame.Enemies[a].Cell ? b : a));
  if (focus >= 0) {
    assert.equal(frame.Enemy, frame.Enemies[focus], `${at}: Frame.Enemy is the first standing in the line`);
    assert.equal(frame.Omen, frame.Omens[focus], at);
  } else {
    assert.equal(frame.Enemy.Down, true, `${at}: none stands`);
    assert.ok(frame.Enemies.includes(frame.Enemy), at);
    assert.equal(frame.Omen, frame.Omens[frame.Enemy.Unit], at);
  }
  // The single-valued members describe Frame.Enemy.
  const shown = frame.Enemy;
  assert.equal(frame.Floor.EnemyCell, shown.Cell, `${at}: Floor.EnemyCell`);
  assert.equal(frame.Floor.EnemySize, shown.Size, at);
  assert.equal(frame.Floor.Gap, Field.Gap(frame.Floor.PlayerCell, frame.Floor.PlayerSize, shown.Cell), at);
  assert.equal(frame.Player.RangeGlyph, String(frame.Floor.Gap), at);
  if (!shown.Down && shown.Hp > 0) assert.ok(frame.Floor.Gap >= 0, `${at}: the N shown is never negative`);
  assert.deepEqual(frame.Floor.AimCells, frame.Omen.Visible ? frame.Floor.Enemies[shown.Unit].AimCells : [], at);
  for (const face of frame.Hand) {
    assert.ok(face.TargetUnit >= -1 && face.TargetUnit < n, `${at}: ${face.Id}'s target`);
    assert.ok(face.ReachOn.length === 0 || face.ReachOn.length === n, `${at}: ${face.Id}'s reach per enemy`);
    if (face.TargetUnit >= 0) assert.equal(face.Aim, CardAim.Single, at);
    for (let k = 0; k < face.ReachOn.length; k++) {
      if (face.ReachOn[k]) assert.equal(frame.Enemies[k].Down, false, `${at}: ${face.Id} reaches a standing enemy`);
    }
  }
}

/** Once a move has played out, the frame is the core state, enemy by enemy. */
function checkMoveEnd(source, ctx) {
  const state = source.State;
  const frame = source.Frame;
  const at = `${ctx.label} turn ${state.Turn} after event ${ctx.lastOrder}`;
  assert.equal(frame.Player.Hp, state.Player.Hp, `${at}: player HP`);
  assert.equal(frame.Player.Guard, state.Player.Guard, `${at}: player Guard`);
  assert.equal(frame.Player.Stamina, state.Player.Stamina, `${at}: player stamina`);
  assert.equal(frame.Floor.PlayerCell, state.Player.Cell, `${at}: player cell`);
  const ongoing = state.Result === GameResult.Ongoing;
  for (let k = 0; k < ctx.n; k++) {
    const unit = state.Enemies[k];
    const body = unit.Body;
    const shown = frame.Enemies[k];
    const of = `${at}: unit ${k} (${unit.Def.Id})`;
    assert.equal(shown.ArtId, unit.Def.Id, of);
    assert.equal(shown.Hp, body.Hp, `${of} HP`);
    assert.equal(shown.Guard, body.Guard, `${of} Guard`);
    assert.equal(shown.Stamina, body.Stamina, `${of} stamina`);
    assert.equal(shown.StaminaMax, body.MaxStamina, `${of} max stamina`);
    assert.deepEqual(statusRows(shown), coreStatusRows(body), `${of} statuses`);
    assert.equal(shown.Stances.reduce((s, r) => s + r.Count, 0), body.StanceList.length, `${of} stances`);
    if (state.Result !== GameResult.Lost) assert.equal(shown.Down, !unit.Alive, `${of} down ⇔ fallen in the core`);
    else if (shown.Down) assert.equal(unit.Alive, false, `${of} down only when fallen`);
    if (!unit.Alive) continue;
    assert.equal(shown.Cell, body.Cell, `${of} cell`);
    assert.equal(shown.Gap, state.GapTo(k), `${of} N`);
    if (!ongoing || state.Turn < 1) continue;
    // The omen and the plan on screen are this enemy's own, read against its own actions.
    const omen = frame.Omens[k];
    const own = CoreText.OmenOf(unit.Omen, unit.Def);
    assert.equal(omen.Visible, true, `${of} omen shown`);
    assert.equal(omen.KindLabel, own.KindLabel, `${of} omen kind`);
    assert.equal(omen.Icon, own.Icon, `${of} omen icon`);
    assert.equal(omen.SideGlyph, own.SideGlyph, `${of} omen reach`);
    if (OmenDisclosure.ShowsPlan(ctx.disclosure)) {
      assert.equal(omen.PlanVisible, unit.Plan != null, `${of} plan shown`);
      if (unit.Plan != null) assert.equal(omen.PlanKindLabel, CoreText.OmenKindWord(unit.Plan.Label.Kind), `${of} plan kind`);
    }
    const floor = frame.Floor.Enemies[k];
    assert.deepEqual(floor.AimCells, FloorCells.AimOf(unit.Omen, body, body.Cell), `${of} aimed cells`);
    assert.deepEqual(floor.DenseCells, FloorCells.DenseOf(state, k), `${of} dense cells`);
    assert.deepEqual(floor.MoveCells, FloorCells.MoveOf(state, k), `${of} move cells`);
  }
}

/** At a wait: each card's default target, numbers and refusals are the core's for that target. */
function checkWait(source, ctx) {
  const state = source.State;
  assert.equal(state.Phase, BattlePhase.PlayerAction);
  for (const face of source.Frame.Hand) {
    const at = `${ctx.label} turn ${state.Turn} ${face.Id}`;
    const target = source.TargetFor(face.Id);
    const card = state.Hand.find((c) => c.InstanceId === face.Id);
    const one = CoreScriptWriter.AimsAtOne(card.Def);
    assert.equal(face.TargetUnit, one ? target : -1, `${at}: the face names TargetFor's enemy`);
    if (one) assert.ok(state.Living.includes(target), `${at}: the default target stands`);
    const preview = TurnLoop.Preview(state, face.Id, target);
    assert.equal(face.Cost, preview.Cost, `${at}: the cost is read on the target`);
    assert.equal(source.Inspect(face.Id), source.Inspect(face.Id, target), at);
    assert.equal(source.PreviewFor(face.Id), source.PreviewFor(face.Id, target), at);
    if (source.Inspect(face.Id) === PlayVerdict.Accepted) {
      assert.equal(TurnLoop.CanPlay(state, face.Id, target), PlayRefusal.None, at);
    }
    if (one) {
      for (let k = 0; k < ctx.n; k++) {
        const alive = state.Enemies[k].Alive;
        const reach = alive && TurnLoop.Preview(state, face.Id, k).InReach;
        assert.equal(face.ReachOn[k], reach, `${at}: reach on ${k}`);
        // §5.2 / §6.3: the cost, its ▼ and the 構えの予告 while the card frames enemy k are the core's on k.
        const cost = alive ? TurnLoop.Preview(state, face.Id, k).Cost : face.Cost;
        assert.equal(face.CostFor(k), cost, `${at}: cost on ${k}`);
        assert.equal(face.CostDropFor(k), Math.max(0, face.PrintedCost - cost), `${at}: ▼ on ${k}`);
        assert.equal(face.StanceHintAfterPayFor(k), CoreScriptWriter.ReserveHint(state.Player.Stamina - cost), `${at}: 構えの予告 on ${k}`);
        if (!alive) continue;
        // §5.4: a card released on k and refused says so with k's numbers.
        const verdict = source.Inspect(face.Id, k);
        if (verdict === PlayVerdict.NotEnoughStamina) {
          assert.equal(source.RefusalText(face.Id, verdict, k), '「' + face.Name + '」はスタミナ ' + cost + ' が要ります。残りは ' + state.Player.Stamina + ' です', `${at}: refused on ${k}`);
        } else if (verdict === PlayVerdict.OutOfRange) {
          assert.ok(source.RefusalText(face.Id, verdict, k).endsWith('いまは ' + state.GapTo(k) + ' です'), `${at}: refused on ${k}`);
        }
      }
      assert.equal(face.CostFor(face.TargetUnit), face.Cost, `${at}: Cost is TargetUnit's`);
      assert.equal(face.StanceHintAfterPayFor(face.TargetUnit), face.StanceHintAfterPay, at);
      // The default target is the cheapest of the enemies the card can be played on (else of the
      // ones in its reach), so the face at rest never shows more than the card costs on any of them.
      const playable = state.Living.filter((k) => TurnLoop.CanPlay(state, face.Id, k) === PlayRefusal.None);
      const pool = playable.length > 0 ? playable : state.Living.filter((k) => face.ReachOn[k]);
      if (pool.length > 0) assert.ok(pool.includes(target), `${at}: the default target is one of [${pool}]`);
      for (const k of pool) assert.ok(face.Cost <= face.CostFor(k), `${at}: the default target costs least (${face.Cost} vs ${face.CostFor(k)} on ${k})`);
      if (new Set(state.Living.map((k) => face.CostFor(k))).size > 1) ctx.costSplit += 1;
      if (pool.length > 1 && target !== pool[0]) ctx.notNearest += 1;
    } else {
      assert.deepEqual(face.CostOn, [], `${at}: a card aimed at no one enemy has one cost`);
      assert.deepEqual(face.StanceHintAfterPayOn, [], at);
    }
  }
}

function checkEnd(source, ctx) {
  const frame = source.Frame;
  const state = source.State;
  assert.equal(source.Finished, true);
  assert.notEqual(state.Result, GameResult.Ongoing, `${ctx.label}: the battle ended`);
  assert.equal(frame.Outcome, state.Result === GameResult.Won ? BattleOutcome.Won : BattleOutcome.Lost, ctx.label);
  if (state.Result === GameResult.Won) {
    assert.ok(frame.Enemies.every((u) => u.Down), `${ctx.label}: won only when every enemy is down`);
    assert.deepEqual([...ctx.defeats].sort(), [...Array(ctx.n).keys()], `${ctx.label}: each enemy fell once`);
  } else {
    assert.ok(state.Living.length > 0 || state.Player.Hp <= 0, ctx.label);
    assert.ok(ctx.defeats.length < ctx.n, `${ctx.label}: a lost battle leaves an enemy`);
  }
  for (const k of ctx.defeats) assert.equal(state.Enemies[k].Alive, false, `${ctx.label}: unit ${k} fell in the core`);
}

function statusRows(unitFrame) {
  return unitFrame.Statuses.filter((c) => c.Kind !== ChipKind.Lasting).map((c) => [c.Label, c.Stacks]);
}

function coreStatusRows(body) {
  return [...body.Statuses.Kinds]
    .sort((a, b) => enumIndex(StatusKind, a) - enumIndex(StatusKind, b))
    .map((kind) => {
      const chip = StatusChipText.Chip(kind, body.Statuses.Stacks(kind));
      return [chip.Label, chip.Stacks];
    });
}

// ---- the exploration walk (as ExpeditionTests' helpers do it) ----

/** Breadth-first over the map: the nodes from where the player stands to the nearest unresolved node of this kind. */
function pathTo(state, kind) {
  const map = state.Map;
  const from = new Map([[state.CurrentNodeId, -1]]);
  const queue = [state.CurrentNodeId];
  let target = -1;
  while (queue.length > 0) {
    const node = queue.shift();
    if (map.Node(node).Kind === kind && !state.HasResolved(node)) {
      target = node;
      break;
    }
    for (const next of map.Neighbours(node)) {
      if (from.has(next)) continue;
      from.set(next, node);
      queue.push(next);
    }
  }
  assert.notEqual(target, -1, `a ${kind} node is reachable`);
  const path = [];
  for (let node = target; node !== state.CurrentNodeId; node = from.get(node)) path.push(node);
  path.reverse();
  return path;
}

/** Wins whatever battle waits without fighting it, keeping full HP so the walk never falls. */
function winPending(expedition) {
  const battle = expedition.StartEncounter();
  EndEveryFight(battle, GameResult.Won, { hp: Constants.PlayerMaxHp, stamina: 5 });
  return expedition.Return();
}

/** Walks to the nearest node of `kind`, winning every battle on the way but the one on that node. */
function walkTo(expedition, kind) {
  const session = expedition.Session;
  if (session.HasPendingBattle) winPending(expedition);
  const path = pathTo(session.State, kind);
  for (const node of path) {
    assert.equal(session.Step(node), true, `step to ${node}`);
    if (session.HasPendingStep) assert.equal(session.Act(ScreenAction.ConfirmStep), true);
    if (node === path[path.length - 1]) return;
    if (session.HasPendingBattle) winPending(expedition);
  }
}

function walkToLayer(expedition, layer) {
  const session = expedition.Session;
  while (session.State.Profile.Layer < layer) {
    walkTo(expedition, NodeKind.Boss);
    winPending(expedition);
    assert.equal(session.Act(ScreenAction.GoToInterlude), true);
    assert.equal(session.Act(ScreenAction.Descend), true);
  }
  if (session.HasPendingBattle) winPending(expedition);
}

// The tables the exploration test relies on.
test('LayerEncounters_PutThePairInTheEliteSlot_OfLayersTwoAndThree', () => {
  for (const layer of [2, 3]) {
    const elite = LayerEncounters.Of(layer).PoolFor(NodeKind.Elite);
    assert.equal(elite.length, 1);
    assert.deepEqual([...elite[0].Fights[0].EnemyIds], PAIR);
  }
});
