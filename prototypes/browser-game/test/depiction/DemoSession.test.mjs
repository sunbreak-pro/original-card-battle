// Tests for src/bridge/depiction/DemoSession.js (port of View/Depiction/Bridge/DemoSession.cs).
//
// Real layer: Assets/View/Depiction/Bridge/Tests/DemoSessionTests.cs (the line and the setup, the
// chain, the rest, the end screen's values and words, 「降参する」, the stages), the DemoSession cases of
// JournalDisclosureHandInTests.cs and UnknownValueTests.cs — on the real battle core, skipped while a
// module another agent ports is missing.
//
// Isolated layer: DemoSession's own logic with BattleCore and the other Bridge files stubbed
// (_demoIsolate.mjs): the seeds, the stages and their refusals, what carries, the rest, every title,
// line and label of the end screen, the breakdown, the average and the mode screen's words.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { Constants } from '../../src/core/battle/Constants.js';
import { SeededRng } from '../../src/core/battle/IRng.js';
import { demoStubs, isolatedDemoSession, loadAll, needs } from './_demoIsolate.mjs';

const { m, missing } = await loadAll([
  'bridge/depiction/DemoSession.js',
  'bridge/depiction/DeckBuilder.js',
  'bridge/depiction/BattleLaunch.js',
  'bridge/depictionScript/DepictionTypes.js',
  'core/battle/Types.js',
  'core/battle/Enemies.js',
  'core/battle/TurnLoop.js',
  'core/battle/Chain.js',
  'core/battle/DeckRules.js',
  'core/battle/BattleEvents.js',
]);
const realOpt = needs(missing);

// ======================================================================== the real layer

function Deck() {
  return m.DeckBuilder.Random(9).Build();
}

/** Plays the suggested card the way the C# tests do: released on the zone its aim needs. */
function playSuggested(source, card) {
  const face = m.DepictionText.Find(source.Frame.Hand, card);
  source.TryPlay(card, m.DepictionText.RequiredZone(face.Aim));
}

/** Fights the source to the end the way an unattended run does (leftmost payable card). */
function FightToTheEnd(source) {
  let guard = 0;
  while (!source.Finished && guard++ < 3000) {
    if (!source.WaitingForPlayer) {
      source.AdvanceAuto();
      continue;
    }
    const card = source.SuggestedCardId;
    if (card.length === 0) {
      source.EndTurn();
      continue;
    }
    playSuggested(source, card);
  }
  assert.equal(source.Finished, true);
}

/** Plays the battle on until the core has decided it, leaving its last events unshown (the screen still playing them). */
function FightUntilDecided(source) {
  let guard = 0;
  while (source.State.Result === m.GameResult.Ongoing && !source.Finished && guard++ < 3000) {
    if (!source.WaitingForPlayer) {
      source.AdvanceAuto();
      continue;
    }
    const card = source.SuggestedCardId;
    if (card.length === 0) {
      source.EndTurn();
      continue;
    }
    playSuggested(source, card);
  }
  assert.notEqual(source.State.Result, m.GameResult.Ongoing, 'the battle was not decided');
}

/** Plays the battle on until player turn `turn` waits for a card, or the battle ends first. */
function FightUntilTurn(source, turn) {
  let guard = 0;
  while (!source.Finished && guard++ < 3000) {
    if (!source.WaitingForPlayer) {
      source.AdvanceAuto();
      continue;
    }
    if (source.State.Turn >= turn) return;
    const card = source.SuggestedCardId;
    if (card.length === 0) {
      source.EndTurn();
      continue;
    }
    playSuggested(source, card);
  }
}

const isName = (name) => (e) => e && e.name === name;
const playerCardsPlayed = (source) => source.History.filter((e) => e instanceof m.CardPlayed && e.Actor === m.Actor.Player).length;
const cardsPlayed = (source) => source.History.filter((e) => e instanceof m.CardPlayed).length;

// ---- the line and the setup ----

for (const [id, cells] of [
  ['polearm_warped', 6], ['shadow_hound', 6], ['armored_warden', 7], ['miasma_priest', 7], ['abyss_angler', 7], ['distortion_root', 7],
]) {
  test(`TheLine_IsLongEnoughToStartAtGapThree(${id}, ${cells})`, realOpt, () => {
    const enemy = m.Enemies.ById(id);
    assert.equal(m.DemoSession.FieldCellsFor(enemy), cells);
    const setup = m.DemoSession.Single(Deck(), id, 1).NextSetup();
    assert.equal(setup.FieldCells, cells);
    assert.equal(m.TurnLoop.Start(setup, new SeededRng(1)).State.Gap, Constants.StartGap);
  });
}

test('ASingleBattle_IsTheChosenEnemy_AtFullHp_OneOfOne', realOpt, () => {
  const session = m.DemoSession.Single(Deck(), 'mist_archer', 5);
  const source = session.StartBattle();
  assert.equal(session.Mode, m.DemoMode.Single);
  assert.equal(source.State.EnemyDef.Id, 'mist_archer');
  assert.equal(source.State.Player.Hp, Constants.PlayerMaxHp);
  assert.equal(source.Frame.Corner.ChainIndex, 1);
  assert.equal(source.Frame.Corner.ChainTotal, 1);
  assert.equal(session.Stage, m.DemoStage.Fighting);
});

test('ADeckThatBreaksTheRules_IsRefused', realOpt, () => {
  const nineteen = m.DeckBuilder.Random(1, 20).Build().slice(0, 19);
  assert.throws(() => m.DemoSession.Single(nineteen, 'polearm_warped', 1), isName('ArgumentException'));
  assert.throws(() => m.DemoSession.Chain(nineteen, 1), isName('ArgumentException'));
});

test('TheRandomPick_IsOneOfTheEleven_FixedBySeed', realOpt, () => {
  const picked = Array.from({ length: 200 }, (_, i) => m.DemoSession.RandomEnemyId(i));
  assert.equal(picked.every((id) => m.Enemies.All.some((e) => e.Id === id)), true);
  assert.equal(new Set(picked).size, m.Enemies.All.length, '200 seeds reach every enemy');
  assert.equal(m.DemoSession.RandomEnemyId(42), m.DemoSession.RandomEnemyId(42));
});

// ---- the chain (§12) ----

test('TheDefaultChain_IsTheThreeOfSection12', realOpt, () => {
  const session = m.DemoSession.Chain(Deck(), 1);
  assert.deepEqual(session.Order.map((e) => e.Id), ['polearm_warped', 'shadow_hound', 'armored_warden']);
  assert.equal(m.DemoSession.ChainLine(), '連戦（3 戦）: 錆槍の竜兵 → 瘴牙の走竜 → 鉄壁の門竜');
  assert.equal(Constants.ChainBattlesDefault, session.Order.length);
});

test('AChain_CarriesHpAndStamina_AndReshufflesTheDeck', realOpt, () => {
  // Find a seed whose first battle is won, then look at what the second starts from.
  for (let seed = 1; seed < 40; seed++) {
    const session = m.DemoSession.Chain(Deck(), seed);
    const first = session.StartBattle(true);
    FightToTheEnd(first);
    const tally = session.Finish(first);
    if (tally.Result !== m.GameResult.Won) continue;

    assert.equal(session.Stage, m.DemoStage.BetweenBattles);
    const hp = first.State.Player.Hp;
    const stamina = first.State.Player.Stamina;
    session.GoOn(false);
    const second = session.StartBattle();
    const state = second.State;
    assert.equal(state.EnemyDef.Id, 'shadow_hound');
    assert.equal(state.Player.Hp, hp, 'HP carries');
    assert.equal(state.Player.Stamina, stamina, 'current stamina carries');
    assert.equal(state.Player.StanceList.length, 0);
    assert.deepEqual(state.DrawPile.map((c) => c.InstanceId).sort(), Deck().map((c) => c.InstanceId).sort(), 'the whole deck again');
    assert.equal(second.Frame.Corner.ChainIndex, 2);
    assert.equal(second.Frame.Corner.ChainTotal, 3);
    return;
  }
  assert.fail('no seed won the first battle');
});

test('TheRest_GivesThirtyPercentAndFullStamina_BeforeTheNextBattle', realOpt, () => {
  for (let seed = 1; seed < 40; seed++) {
    const session = m.DemoSession.Chain(Deck(), seed);
    const first = session.StartBattle(true);
    FightToTheEnd(first);
    if (session.Finish(first).Result !== m.GameResult.Won) continue;

    const hp = first.State.Player.Hp;
    const screen = session.EndScreen();
    session.GoOn(true);
    const state = session.StartBattle().State;
    const expected = Math.min(Constants.PlayerMaxHp, hp + 15);
    assert.equal(state.Player.Hp, expected);
    assert.equal(state.Player.Stamina, Constants.BaseMaxStamina);
    assert.equal(screen.CanGoOn, true);
    assert.equal(screen.RestLabel, '休んで次へ（HP +' + (expected - hp) + '、スタミナ全回復）');
    assert.equal(screen.GoOnLabel, 'そのまま次へ');
    return;
  }
  assert.fail('no seed won the first battle');
});

test('ALoss_EndsTheChain', realOpt, () => {
  // A chain against three bosses is lost early (the leftmost auto-player rarely beats them).
  for (let seed = 1; seed < 40; seed++) {
    const session = m.DemoSession.Chain(Deck(), seed, ['abyss_angler', 'distortion_root', 'miasma_priest']);
    const source = session.StartBattle(true);
    FightToTheEnd(source);
    if (session.Finish(source).Result !== m.GameResult.Lost) continue;

    const screen = session.EndScreen();
    assert.equal(session.Stage, m.DemoStage.Over);
    assert.equal(session.AllWon, false);
    assert.throws(() => session.GoOn(false), isName('InvalidOperationException'));
    assert.throws(() => session.StartBattle(), isName('InvalidOperationException'));
    assert.equal(screen.Title, '1 戦目で力尽きました');
    assert.equal(screen.Won, false);
    assert.equal(screen.CanGoOn, false);
    assert.equal(screen.AgainLabel, '最初からもう一度');
    assert.equal(screen.BackLabel, 'デッキ選択へ戻る');

    session.Again();
    const again = session.StartBattle().State;
    assert.equal(again.EnemyDef.Id, 'abyss_angler');
    assert.equal(again.Player.Hp, Constants.PlayerMaxHp, 'again starts at full HP');
    assert.equal(session.Tallies.length, 0);
    return;
  }
  assert.fail('no seed lost the first battle');
});

test('AChainWonToTheEnd_OffersTheSameOrderAgain', realOpt, () => {
  // A one-battle chain against the polearm: won once, it is a clean sweep.
  for (let seed = 1; seed < 40; seed++) {
    const session = m.DemoSession.Chain(Deck(), seed, ['polearm_warped']);
    const source = session.StartBattle(true);
    FightToTheEnd(source);
    if (session.Finish(source).Result !== m.GameResult.Won) continue;

    const screen = session.EndScreen();
    assert.equal(session.AllWon, true);
    assert.equal(screen.Title, '連戦に全勝しました');
    assert.equal(screen.AgainLabel, '同じ並びをもう一度');
    return;
  }
  assert.fail('no seed won');
});

// ---- the end screen's values ----

test('TheEndScreen_ShowsSection12sItems_FromTheBattlesOwnEvents', realOpt, () => {
  const session = m.DemoSession.Single(Deck(), 'polearm_warped', 3);
  const source = session.StartBattle(true);
  FightToTheEnd(source);
  const tally = session.Finish(source);
  const screen = session.EndScreen();
  const state = source.State;

  assert.equal(tally.Turns, state.Turn);
  assert.equal(tally.HpLeft, state.Player.Hp);
  assert.equal(tally.CardsPlayed, cardsPlayed(source));
  assert.equal(screen.Title, state.Result === m.GameResult.Won ? '錆槍の竜兵に勝ちました' : '錆槍の竜兵に敗れました');
  assert.deepEqual(screen.Lines, [
    '相手: 錆槍の竜兵',
    '残り HP: ' + state.Player.Hp + ' / 50',
    'ターン数: ' + state.Turn,
    '使った札 ' + tally.CardsPlayed + ' 枚の属性: ' + m.DemoSession.Breakdown(tally),
    '特性の発動: ' + tally.TraitsFired + ' 回',
  ]);
  assert.equal(screen.AgainLabel, 'もう一度');
  assert.equal(session.Stage, m.DemoStage.Over);
});

test('OnlyASingleBattlesEndScreen_OffersToChooseTheEnemyAgain', realOpt, () => {
  // #211: after one enemy, fought out or given up, another can be picked with the same deck. A
  // chain's end screen keeps its own ways on, however its battle went.
  const single = m.DemoSession.Single(Deck(), 'polearm_warped', 3);
  const one = single.StartBattle(true);
  FightToTheEnd(one);
  single.Finish(one);
  const afterOne = single.EndScreen();

  const givenUp = m.DemoSession.Single(Deck(), 'polearm_warped', 3);
  givenUp.Surrender(givenUp.StartBattle());
  const afterGivingUp = givenUp.EndScreen();

  const chain = m.DemoSession.Chain(Deck(), 3, ['polearm_warped']);
  const first = chain.StartBattle(true);
  FightToTheEnd(first);
  chain.Finish(first);
  const afterChain = chain.EndScreen();

  const chainGivenUp = m.DemoSession.Chain(Deck(), 3);
  chainGivenUp.Surrender(chainGivenUp.StartBattle());
  const afterGivingUpAChain = chainGivenUp.EndScreen();

  assert.equal(afterOne.CanChooseEnemy, true);
  assert.equal(afterOne.ChooseEnemyLabel, '敵を選び直す');
  assert.equal(afterOne.CanGoOn, false);
  assert.equal(afterGivingUp.Title, '錆槍の竜兵に降参しました');
  assert.equal(afterGivingUp.CanChooseEnemy, true, 'a single battle given up');
  assert.equal(afterGivingUp.ChooseEnemyLabel, '敵を選び直す');
  assert.equal(afterChain.CanChooseEnemy, false);
  assert.equal(afterChain.ChooseEnemyLabel, '');
  assert.equal(afterGivingUpAChain.CanChooseEnemy, false, 'a chain given up');
});

test('TheRestsWords_AndTheRest_ReadTheBattlesOwnMaximum', realOpt, () => {
  // #211: the rest button's number and the rest itself come from one place, the tally's maximum.
  const low = new m.BattleTally('polearm_warped', m.GameResult.Won, 5, 10, 80, 0, new Map(), 0);
  const high = new m.BattleTally('polearm_warped', m.GameResult.Won, 5, 70, 80, 0, new Map(), 0);
  assert.equal(m.DemoSession.RestAfter(low).Hp, 34, '10 + 80 × 30%, not 10 + 50 × 30%');
  assert.equal(m.DemoSession.RestAfter(low).Stamina, Constants.BaseMaxStamina);
  assert.equal(m.DemoSession.RestAfter(high).Hp, 80, 'no further than the maximum');
  assert.throws(() => m.DemoSession.RestAfter(null), isName('ArgumentNullException'));
});

test('TheBreakdown_AndTheAverage_ReadInOrder', realOpt, () => {
  const A = m.BattleAttribute;
  const tally = new m.BattleTally('polearm_warped', m.GameResult.Won, 7, 30, 50, 9, new Map([[A.Guard, 3], [A.Attack, 6], [A.Skill, 2]]), 4);
  assert.equal(m.DemoSession.Breakdown(tally), '攻撃 6・防御 3・技 2');
  assert.equal(m.DemoSession.Breakdown(new m.BattleTally('x', m.GameResult.Lost, 1, 0, 50, 0, new Map(), 0)), 'なし');
  assert.equal(m.DemoSession.EnemyLine(m.Enemies.AbyssAngler), '獄竜 ガルド（ボス・HP 180）');
});

test('AChainsEndScreen_ShowsEachLine_AndTheWholeRunAddedUp', realOpt, () => {
  // A two-battle chain won once and then ended: 何戦目, the battle's own items, the average
  // turns, and (the run over) the totals over both battles (§12).
  for (let seed = 1; seed < 60; seed++) {
    const session = m.DemoSession.Chain(Deck(), seed, ['polearm_warped', 'pack_alpha']);
    const first = session.StartBattle(true);
    FightToTheEnd(first);
    const one = session.Finish(first);
    if (one.Result !== m.GameResult.Won) continue;

    const between = session.EndScreen();
    assert.deepEqual(between.Lines, [
      '何戦目: 1 / 2（錆槍の竜兵）',
      '残り HP: ' + one.HpLeft + ' / 50',
      'ターン数: ' + one.Turns,
      '使った札 ' + one.CardsPlayed + ' 枚の属性: ' + m.DemoSession.Breakdown(one),
      '特性の発動: ' + one.TraitsFired + ' 回',
      '次: 統牙の長竜',
    ]);
    assert.equal(between.Title, '1 戦目に勝ちました');

    session.GoOn(false);
    const second = session.StartBattle(true);
    FightToTheEnd(second);
    const two = session.Finish(second);
    const total = m.Chain.Total(session.Tallies);
    const end = session.EndScreen();
    const average = m.DemoSession.AverageOf(session.Tallies);

    assert.equal(session.Stage, m.DemoStage.Over);
    assert.deepEqual(end.Lines, [
      '何戦目: 2 / 2（統牙の長竜）',
      '残り HP: ' + two.HpLeft + ' / 50',
      'ターン数: ' + two.Turns + '（平均 ' + average + '）',
      '使った札 ' + two.CardsPlayed + ' 枚の属性: ' + m.DemoSession.Breakdown(two),
      '特性の発動: ' + two.TraitsFired + ' 回',
      '連戦の合計（2 戦）: 使った札 ' + (one.CardsPlayed + two.CardsPlayed) + ' 枚の属性: ' + m.DemoSession.Breakdown(total),
      '連戦の合計: 特性の発動 ' + (one.TraitsFired + two.TraitsFired) + ' 回、平均ターン数 ' + average,
    ]);
    assert.equal(total.CardsPlayed, one.CardsPlayed + two.CardsPlayed);
    assert.equal(total.CountOf(m.BattleAttribute.Attack), one.CountOf(m.BattleAttribute.Attack) + two.CountOf(m.BattleAttribute.Attack));
    assert.equal(total.Turns, one.Turns + two.Turns);
    return;
  }
  assert.fail('no seed won the first battle');
});

for (const [turns, expected] of [[[8, 9], '8.5'], [[8, 8], '8'], [[6, 7, 7], '6.7'], [[5], '5']]) {
  test(`TheAverageTurns_RoundToOneDecimal([${turns}], ${expected})`, realOpt, () => {
    const tallies = turns.map((t) => new m.BattleTally('polearm_warped', m.GameResult.Won, t, 50, 50, 0, new Map(), 0));
    assert.equal(m.DemoSession.AverageOf(tallies), expected);
    assert.equal(m.DemoSession.AverageOf([]), '0');
  });
}

test('EachRun_DealsAfresh_AndTheRandomPickMovesOn', realOpt, () => {
  // Choosing the same mode again from the same seed: another deal, and another random enemy draw.
  const first = m.DemoSession.Single(Deck(), 'polearm_warped', 7, 0).StartBattle().State;
  const again = m.DemoSession.Single(Deck(), 'polearm_warped', 7, 1).StartBattle().State;
  assert.notDeepEqual(again.DrawPile.map((c) => c.InstanceId), first.DrawPile.map((c) => c.InstanceId));
  assert.ok(new Set(Array.from({ length: 30 }, (_, p) => m.DemoSession.RandomEnemyId(7, p))).size > 1);
});

test('TheModeScreensWords_AreTheBridges', realOpt, () => {
  assert.equal(m.DemoSession.ModeTitle, '戦い方を選ぶ');
  assert.equal(m.DemoSession.SingleHeading, '1 体で区切る（敵を選ぶ）');
  assert.equal(m.DemoSession.ChainHeading, '連戦（HP とスタミナを持ち越し、負けたら終わり）');
  assert.equal(m.DemoSession.RandomLabel, 'ランダム');
  assert.equal(m.DemoSession.BackToDeckLabel, 'デッキ選択へ戻る');
  assert.equal(m.DemoSession.SurrenderLabel, '降参する');
});

// ---- 「降参する」 (#203) ----

test('ASurrender_MidChain_EndsTheChainAsALoss', realOpt, () => {
  // Win the first battle, then give the second up on its second turn: the chain is over and lost.
  for (let seed = 1; seed < 40; seed++) {
    const session = m.DemoSession.Chain(Deck(), seed);
    const first = session.StartBattle(true);
    FightToTheEnd(first);
    if (session.Finish(first).Result !== m.GameResult.Won) continue;

    session.GoOn(false);
    const second = session.StartBattle(true);
    FightUntilTurn(second, 2);
    if (second.State.Result !== m.GameResult.Ongoing) continue;
    const state = second.State;

    const two = session.Surrender(second);
    const screen = session.EndScreen();
    assert.equal(two.Result, m.GameResult.Lost);
    assert.equal(two.Turns, state.Turn);
    assert.equal(two.HpLeft, state.Player.Hp, 'the HP it had, not 0');
    assert.equal(two.CardsPlayed, playerCardsPlayed(second));
    assert.equal(session.Stage, m.DemoStage.Over);
    assert.equal(session.Surrendered, true);
    assert.equal(session.AllWon, false);
    assert.deepEqual(session.Tallies.map((t) => t.Result), [m.GameResult.Won, m.GameResult.Lost]);
    assert.throws(() => session.GoOn(false), isName('InvalidOperationException'));
    assert.throws(() => session.StartBattle(), isName('InvalidOperationException'));
    assert.equal(screen.Title, '2 戦目で降参しました');
    assert.equal(screen.Won, false);
    assert.equal(screen.CanGoOn, false);
    assert.equal(screen.Lines[0], '何戦目: 2 / 3（瘴牙の走竜）');
    assert.equal(screen.Lines[1], '残り HP: ' + state.Player.Hp + ' / 50');
    assert.equal(screen.Lines.some((l) => l.startsWith('連戦の合計（2 戦）')), true, 'the run is added up');
    assert.equal(screen.AgainLabel, '最初からもう一度');
    assert.equal(screen.BackLabel, 'デッキ選択へ戻る');

    session.Again();
    const again = session.StartBattle().State;
    assert.equal(session.Surrendered, false);
    assert.equal(again.EnemyDef.Id, 'polearm_warped');
    assert.equal(again.Player.Hp, Constants.PlayerMaxHp, 'again starts at full HP');
    return;
  }
  assert.fail('no seed won the first battle');
});

test('ASurrender_BeforeTheFirstCard_EndsTheChainAtOnce', realOpt, () => {
  const session = m.DemoSession.Chain(Deck(), 1);
  const source = session.StartBattle();
  const tally = session.Surrender(source);
  const screen = session.EndScreen();
  assert.equal(tally.Result, m.GameResult.Lost);
  assert.equal(tally.HpLeft, Constants.PlayerMaxHp);
  assert.equal(tally.CardsPlayed, 0);
  assert.equal(session.Stage, m.DemoStage.Over);
  assert.equal(screen.Title, '1 戦目で降参しました');
  assert.equal(screen.CanGoOn, false);
  assert.equal(screen.Lines.some((l) => l.startsWith('連戦の合計')), false, 'one battle is not added up');
});

test('ASurrenderedSingleBattle_IsALoss_WithTheBattleSoFar', realOpt, () => {
  const session = m.DemoSession.Single(Deck(), 'polearm_warped', 3);
  const source = session.StartBattle(true);
  FightUntilTurn(source, 2);
  assert.equal(source.State.Result, m.GameResult.Ongoing, 'seed 3 is still fighting on turn 2');
  const state = source.State;

  const tally = session.Surrender(source);
  const screen = session.EndScreen();
  assert.equal(tally.Result, m.GameResult.Lost);
  assert.ok(tally.CardsPlayed > 0, 'turn 1 was played');
  assert.equal(session.Stage, m.DemoStage.Over);
  assert.equal(screen.Title, '錆槍の竜兵に降参しました');
  assert.equal(screen.Won, false);
  assert.deepEqual(screen.Lines, [
    '相手: 錆槍の竜兵',
    '残り HP: ' + state.Player.Hp + ' / 50',
    'ターン数: ' + state.Turn,
    '使った札 ' + tally.CardsPlayed + ' 枚の属性: ' + m.DemoSession.Breakdown(tally),
    '特性の発動: ' + tally.TraitsFired + ' 回',
  ]);
  assert.equal(screen.AgainLabel, 'もう一度');
});

test('ASurrender_AfterTheCoreHasDecided_KeepsTheCoresResult', realOpt, () => {
  // The button can be pressed while the last events are still on the screen.
  for (let seed = 1; seed < 40; seed++) {
    const session = m.DemoSession.Single(Deck(), 'polearm_warped', seed);
    const source = session.StartBattle(true);
    FightUntilDecided(source);
    if (source.Finished) continue; // decided with nothing left to show

    const tally = session.Surrender(source);
    assert.equal(tally.Result, source.State.Result);
    assert.equal(session.Surrendered, false);
    assert.equal(session.Stage, m.DemoStage.Over);
    assert.equal(session.EndScreen().Title.includes('降参'), false);
    return;
  }
  assert.fail('no seed left the deciding events unshown');
});

test('ASurrender_AsAChainedWinIsStillShown_GoesOnLikeTheWin', realOpt, () => {
  // A chain's first battle won, its last events still on the screen: the chain goes on.
  for (let seed = 1; seed < 40; seed++) {
    const session = m.DemoSession.Chain(Deck(), seed);
    const source = session.StartBattle(true);
    FightUntilDecided(source);
    if (source.State.Result !== m.GameResult.Won || source.Finished) continue;

    const tally = session.Surrender(source);
    const screen = session.EndScreen();
    assert.equal(tally.Result, m.GameResult.Won);
    assert.equal(session.Surrendered, false);
    assert.equal(session.Stage, m.DemoStage.BetweenBattles);
    assert.equal(screen.CanGoOn, true);
    assert.equal(screen.Title, '1 戦目に勝ちました');
    return;
  }
  assert.fail('no seed won the first battle with its last events unshown');
});

test('ASurrender_OutsideABattle_IsRefused', realOpt, () => {
  const session = m.DemoSession.Single(Deck(), 'polearm_warped', 1);
  assert.throws(() => session.Surrender(m.DemoSession.Single(Deck(), 'polearm_warped', 1).StartBattle()), isName('InvalidOperationException'), 'nothing started yet');
  const source = session.StartBattle();
  assert.throws(() => session.Surrender(null), isName('ArgumentNullException'));
  session.Surrender(source);
  assert.throws(() => session.Surrender(source), isName('InvalidOperationException'), 'the run is over');
  assert.throws(() => session.Finish(source), isName('InvalidOperationException'), 'a battle given up is not finished again');
});

test('AgainDuringABattle_IsRefused', realOpt, () => {
  const session = m.DemoSession.Single(Deck(), 'polearm_warped', 1);
  session.StartBattle();
  assert.throws(() => session.Again(), isName('InvalidOperationException'));
});

test('TheStages_RefuseMovesOutOfTurn', realOpt, () => {
  const session = m.DemoSession.Single(Deck(), 'polearm_warped', 1);
  assert.throws(() => session.EndScreen(), isName('InvalidOperationException'));
  const source = session.StartBattle();
  assert.throws(() => session.StartBattle(), isName('InvalidOperationException'));
  assert.throws(() => session.Finish(source), isName('InvalidOperationException'), 'the battle has not ended');
});

// ---- JournalDisclosureHandInTests (#500) ----

test('ARunWithoutAJournal_IsFullyDisclosed', realOpt, () => {
  const run = m.DemoSession.Single(Deck(), 'abyss_angler', 3);
  assert.equal(run.DisclosureOf(run.Current), 2);
  assert.equal(run.StartBattle().Frame.Omen.Disclosure, 2);
});

for (const [journal, shown] of [[1, 1], [2, 2], [0, 0], [-1, 0]]) {
  test(`ASingleBattle_StartsAtTheJournalsLevel_ForItsEnemy(${journal}, ${shown})`, realOpt, () => {
    const levels = new Map([['abyss_angler', journal]]);
    const run = m.DemoSession.Single(Deck(), 'abyss_angler', 3, 0, (id) => levels.get(id));
    assert.equal(run.DisclosureOf(run.Current), shown);
    assert.equal(run.StartBattle().Frame.Omen.Disclosure, shown);
  });
}

test('AChain_ReadsEachEnemysOwnLevel', realOpt, () => {
  const levels = new Map([['polearm_warped', 1], ['shadow_hound', 2], ['armored_warden', 0]]);
  const run = m.DemoSession.Chain(Deck(), 3, null, 0, (id) => levels.get(id));
  assert.deepEqual(run.Order.map((e) => run.DisclosureOf(e)), [1, 2, 0]);
  assert.equal(run.StartBattle().Frame.Omen.Disclosure, 1, "the first battle is polearm_warped's");
});

// ---- UnknownValueTests: an enemy id nobody defined ----

test('AnUnknownEnemyId_IsRefused_WithTheKnownIds', realOpt, () => {
  const departure = m.Departure.Plain(Deck());
  const known = m.Enemies.All.map((d) => d.Id).join(', ');
  for (const make of [() => m.DemoSession.Single(departure, 'no_such_enemy', 1), () => m.DemoSession.Chain(departure, 1, ['no_such_enemy'])]) {
    assert.throws(make, (e) => e.name === 'ArgumentException' && e.message.includes('no_such_enemy') && e.message.includes(known));
  }
});

// ======================================================================== the isolated layer

const iso = (await isolatedDemoSession()).module;
const s = await demoStubs();
const { DemoSession, DemoMode, DemoStage, DemoEndScreen } = iso;
const IsoOpt = {};

/** A bare deck of n stub cards (Cards.Validate is stubbed to 20〜40). */
const bareDeck = (n = 20) => Array.from({ length: n }, (_, i) => ({ InstanceId: 'c' + i }));
const tally = (fields) => new s.BattleTally(fields.EnemyId ?? 'polearm_warped', fields.Result ?? 'Won', fields.Turns ?? 5,
  fields.HpLeft ?? 30, fields.MaxHp ?? 50, fields.CardsPlayed ?? 0, fields.Attributes ?? new Map(), fields.TraitsFired ?? 0);
/** Ends the stub battle: the result, the player's HP / stamina and the tally Chain.Tally hands back. */
function end(source, t, hp = t.HpLeft, stamina = 4) {
  source.State = { Result: t.Result, Player: { Hp: hp, Stamina: stamina }, Tally: t };
  return source;
}
const errorIs = (name, message) => (e) => e && e.name === name && (message === undefined || e.message === message);

test('[isolated] TheModeScreensWords_AndTheEnemysLines', IsoOpt, () => {
  assert.equal(DemoSession.ModeTitle, '戦い方を選ぶ');
  assert.equal(DemoSession.SingleHeading, '1 体で区切る（敵を選ぶ）');
  assert.equal(DemoSession.ChainHeading, '連戦（HP とスタミナを持ち越し、負けたら終わり）');
  assert.equal(DemoSession.RandomLabel, 'ランダム');
  assert.equal(DemoSession.BackToDeckLabel, 'デッキ選択へ戻る');
  assert.equal(DemoSession.SurrenderLabel, '降参する');
  assert.deepEqual([...DemoSession.DefaultChain], ['polearm_warped', 'shadow_hound', 'armored_warden']);
  assert.ok(Object.isFrozen(DemoSession.DefaultChain));
  assert.equal(DemoSession.ChainLine(), '連戦（3 戦）: 錆槍の竜兵 → 瘴牙の走竜 → 鉄壁の門竜');
  assert.equal(DemoSession.ChainLine(['abyss_angler']), '連戦（1 戦）: 獄竜 ガルド');
  assert.equal(DemoSession.EnemyLine(s.Enemies.ById('polearm_warped')), '錆槍の竜兵（通常・HP 60）');
  assert.equal(DemoSession.EnemyLine(s.Enemies.ById('pack_alpha')), '統牙の長竜（精鋭・HP 90）');
  assert.equal(DemoSession.EnemyLine(s.Enemies.ById('abyss_angler')), '獄竜 ガルド（ボス・HP 180）');
  assert.throws(() => DemoSession.EnemyLine(null), errorIs('ArgumentNullException', "Value cannot be null. (Parameter 'enemy')"));
  assert.deepEqual(DemoMode.$order, ['Single', 'Chain']);
  assert.deepEqual(DemoStage.$order, ['Ready', 'Fighting', 'BetweenBattles', 'Over']);
});

test('[isolated] TheEndScreen_StartsWithTheCsDefaults', IsoOpt, () => {
  const screen = new DemoEndScreen();
  assert.deepEqual({ ...screen }, {
    Title: '', Won: false, Lines: [], CanGoOn: false, RestLabel: '', GoOnLabel: '', CanChooseEnemy: false, ChooseEnemyLabel: '',
    CanAgain: true, AgainLabel: '', BackLabel: 'デッキ選択へ戻る',
  });
  assert.equal(new DemoEndScreen({ Won: true }).Won, true);
});

test('[isolated] TheBreakdown_ReadsTheFourInOrder', IsoOpt, () => {
  const t = tally({ Attributes: new Map([[2, 3], [1, 6], [4, 2]]) });
  assert.equal(DemoSession.Breakdown(t), '攻撃 6・防御 3・技 2');
  assert.equal(DemoSession.Breakdown(tally({ Attributes: new Map([[8, 1], [4, 0]]) })), '構え 1');
  assert.equal(DemoSession.Breakdown(tally({})), 'なし');
  assert.throws(() => DemoSession.Breakdown(null), errorIs('ArgumentNullException', "Value cannot be null. (Parameter 'tally')"));
});

test('[isolated] TheAverage_RoundsAwayFromZero_ToOneDecimal', IsoOpt, () => {
  const of = (turns) => DemoSession.AverageOf(turns.map((t) => tally({ Turns: t })));
  assert.equal(of([8, 9]), '8.5');
  assert.equal(of([8, 8]), '8');
  assert.equal(of([6, 7, 7]), '6.7');
  assert.equal(of([5]), '5');
  assert.equal(of([8, 8, 8, 9]), '8.3', '8.25 rounds away from zero');
  assert.equal(of([1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2]), '1.1');
  assert.equal(of([10, 11, 11]), '10.7');
  assert.equal(of([0]), '0');
  assert.equal(DemoSession.AverageOf([]), '0');
  assert.equal(DemoSession.AverageOf(null), '0');
});

test('[isolated] TheLine_IsTheSlicesOrWhatTheEnemyNeeds', IsoOpt, () => {
  assert.equal(DemoSession.FieldCellsFor(s.Enemies.ById('polearm_warped')), 6);
  assert.equal(DemoSession.FieldCellsFor(s.Enemies.ById('armored_warden')), 7);
  assert.equal(DemoSession.FieldCellsFor(s.Enemies.ById('polearm_warped'), 1, 4), 6, 'a cell back is a cell more of gap');
  assert.equal(DemoSession.FieldCellsFor(s.Enemies.ById('abyss_angler'), 1, 4), 7);
  assert.equal(DemoSession.FieldCellsFor(s.Enemies.ById('polearm_warped'), 3, 5), 9);
  assert.throws(() => DemoSession.FieldCellsFor(null), errorIs('ArgumentNullException', "Value cannot be null. (Parameter 'enemy')"));
});

test('[isolated] TheRandomPick_RollsTheSeedPlusPicksTimes104729_Wrapped', IsoOpt, () => {
  const n = s.Enemies.All.length;
  const byHand = (seed) => s.Enemies.All[Math.min(n - 1, Math.floor(new SeededRng(seed).NextDouble() * n))].Id;
  for (const [seed, pick] of [[0, 0], [1, 0], [42, 0], [7, 3], [-5, 2]]) {
    assert.equal(DemoSession.RandomEnemyId(seed, pick), byHand(seed + pick * 104729), `${seed}, ${pick}`);
  }
  // unchecked int: 2147483647 + 104729 wraps to −2147378920.
  assert.equal(DemoSession.RandomEnemyId(2147483647, 1), byHand(-2147378920));
  assert.equal(DemoSession.RandomEnemyId(2147483647, 1), DemoSession.RandomEnemyId(-2147378920, 0));
  assert.equal(DemoSession.RandomEnemyId(5), DemoSession.RandomEnemyId(5, 0));
  const picked = new Set(Array.from({ length: 200 }, (_, i) => DemoSession.RandomEnemyId(i)));
  assert.equal(picked.size, n, '200 seeds reach every enemy');
});

test('[isolated] ARunIsRefused_ForABadDeck_ABadStart_AnEmptyChain_OrAnUnknownEnemy', IsoOpt, () => {
  assert.throws(() => DemoSession.Single(bareDeck(19), 'polearm_warped', 1),
    errorIs('ArgumentException', 'DemoSession: the deck breaks §8 — The deck has 19 cards; it needs 20..40.'));
  assert.throws(() => DemoSession.Chain(bareDeck(19), 1), errorIs('ArgumentException'));
  const forward = new s.Departure(bareDeck(), [], [], -1, null);
  assert.throws(() => DemoSession.Single(forward, 'polearm_warped', 1), errorIs('ArgumentException', 'DemoSession: the start never moves forward.'));
  assert.throws(() => DemoSession.Chain(bareDeck(), 1, []), errorIs('ArgumentException', 'DemoSession: a chain needs at least one enemy.'));
  assert.throws(() => DemoSession.Single(bareDeck(), 'no_such_enemy', 1), (e) => e.name === 'ArgumentException'
    && e.message.startsWith('DemoSession: no enemy has the id "no_such_enemy". Known ids: polearm_warped, '));
  assert.throws(() => DemoSession.Single(null, 'polearm_warped', 1), errorIs('ArgumentNullException', "Value cannot be null. (Parameter 'departure')"));
  assert.throws(() => DemoSession.Single(new s.Departure(null, [], [], 0, null), 'polearm_warped', 1), errorIs('ArgumentNullException'));
});

test('[isolated] TheRunKeepsACopyOfTheDeck_AndTheDeparture', IsoOpt, () => {
  const deck = bareDeck();
  const departure = new s.Departure(deck, ['maai_no_kutsu'], [], 1, 'kesa_cut', 3, 5, 1);
  const session = DemoSession.Single(departure, 'polearm_warped', 1);
  assert.notEqual(session.Departure, departure);
  assert.notEqual(session.Departure.Deck, deck, 'a copy of the list');
  assert.deepEqual(session.Departure.Deck, deck);
  assert.equal(session.Departure.TalentCardId, 'kesa_cut');
  assert.equal(session.Departure.StartStaminaBonus, 3);
  deck.pop();
  assert.equal(session.Departure.Deck.length, 20, 'the deck the run set out with');
  const plain = DemoSession.Chain(bareDeck(), 1).Departure;
  assert.deepEqual([plain.Tools, plain.Consumables, plain.StartCellsBack, plain.TalentCardId], [[], [], 0, null]);
});

test('[isolated] ASingleBattle_StartsOnItsSeed_ItsLine_OneOfOne', IsoOpt, () => {
  const session = DemoSession.Single(bareDeck(), 'armored_warden', 11, 2);
  assert.equal(session.Mode, DemoMode.Single);
  assert.equal(session.Stage, DemoStage.Ready);
  assert.deepEqual(session.Order.map((e) => e.Id), ['armored_warden']);
  assert.equal(session.Current.Id, 'armored_warden');
  const setup = session.NextSetup();
  assert.deepEqual([setup.FieldCells, setup.StartGap, setup.PlayerStartCell, setup.PlayerStartHp, setup.PlayerStartStamina], [7, 3, 2, null, null]);
  const source = session.StartBattle();
  assert.equal(session.Stage, DemoStage.Fighting);
  const a = source.Args;
  assert.equal(a.seed, 11 + 2 * 104729, 'the run seed');
  assert.deepEqual([a.suggestCards, a.stopAfterTurns, a.chainIndex, a.chainTotal, a.disclosure], [false, 0, 1, 1, 2]);
  assert.equal(a.setup.Enemy.Id, 'armored_warden');
  assert.throws(() => session.StartBattle(), errorIs('InvalidOperationException', 'DemoSession: no battle is ready (Fighting).'));
  assert.throws(() => session.Again(), errorIs('InvalidOperationException', 'DemoSession: a battle is being fought.'));
  assert.throws(() => session.GoOn(true), errorIs('InvalidOperationException', 'DemoSession: there is no next battle (Fighting).'));
  assert.throws(() => session.EndScreen(), errorIs('InvalidOperationException', 'DemoSession: no battle has finished yet.'));
  assert.throws(() => session.Finish(source), errorIs('InvalidOperationException', 'DemoSession: the battle has not ended.'));
  assert.throws(() => session.Finish(null), errorIs('ArgumentNullException', "Value cannot be null. (Parameter 'source')"));
  assert.throws(() => session.Surrender(null), errorIs('ArgumentNullException', "Value cannot be null. (Parameter 'source')"));
});

test('[isolated] AToolsStart_ReachesTheLineAndTheSetup', IsoOpt, () => {
  const session = DemoSession.Chain(new s.Departure(bareDeck(), ['maai_no_kutsu'], [], 1, null), 1, ['abyss_angler']);
  const setup = session.NextSetup();
  assert.deepEqual([setup.PlayerStartCell, setup.StartGap, setup.FieldCells], [1, 4, 7]);
});

test('[isolated] EveryStart_TakesASeedOfItsOwn_7919Apart_Wrapped', IsoOpt, () => {
  const session = DemoSession.Single(bareDeck(), 'polearm_warped', 2147483000);
  const seeds = [];
  for (let i = 0; i < 3; i++) {
    const source = session.StartBattle(true);
    seeds.push(source.Args.seed);
    assert.equal(source.Args.suggestCards, true);
    session.Surrender(source);
    session.Again();
  }
  assert.deepEqual(seeds, [2147483000, 2147483000 + 7919 - 4294967296, 2147483000 + 2 * 7919 - 4294967296]);
});

test('[isolated] ASingleBattleWon_EndsTheRun_WithItsWords', IsoOpt, () => {
  const session = DemoSession.Single(bareDeck(), 'polearm_warped', 1);
  const source = session.StartBattle();
  const t = tally({ Result: 'Won', Turns: 7, HpLeft: 31, MaxHp: 50, CardsPlayed: 12, Attributes: new Map([[1, 8], [2, 4]]), TraitsFired: 3 });
  assert.equal(session.Finish(end(source, t)), t);
  assert.equal(source.State.TalliedFrom, source.History, 'tallied from the battle\'s own events');
  assert.equal(session.Stage, DemoStage.Over);
  assert.equal(session.AllWon, false, 'a single battle is no chain');
  assert.deepEqual(session.Tallies, [t]);
  const screen = session.EndScreen();
  assert.deepEqual({ ...screen }, {
    Title: '錆槍の竜兵に勝ちました', Won: true,
    Lines: ['相手: 錆槍の竜兵', '残り HP: 31 / 50', 'ターン数: 7', '使った札 12 枚の属性: 攻撃 8・防御 4', '特性の発動: 3 回'],
    CanGoOn: false, RestLabel: '', GoOnLabel: '', CanChooseEnemy: true, ChooseEnemyLabel: '敵を選び直す',
    CanAgain: true, AgainLabel: 'もう一度', BackLabel: 'デッキ選択へ戻る',
  });
  assert.throws(() => session.Finish(source), errorIs('InvalidOperationException', 'DemoSession: no battle is being fought (Over).'));
});

test('[isolated] ASingleBattleLost_OrGivenUp_SaysSo', IsoOpt, () => {
  const lost = DemoSession.Single(bareDeck(), 'shadow_hound', 1);
  lost.Finish(end(lost.StartBattle(), tally({ EnemyId: 'shadow_hound', Result: 'Lost', HpLeft: 0 })));
  assert.equal(lost.EndScreen().Title, '瘴牙の走竜に敗れました');
  assert.equal(lost.EndScreen().Won, false);

  const given = DemoSession.Single(bareDeck(), 'shadow_hound', 1);
  const source = given.StartBattle();
  source.State = { Result: 'Ongoing', Player: { Hp: 22, Stamina: 3 }, Tally: tally({ EnemyId: 'shadow_hound', Result: 'Ongoing', HpLeft: 22, Turns: 2 }) };
  const t = given.Surrender(source);
  assert.equal(t.Result, 'Lost', 'tallied where it stands, as a loss');
  assert.deepEqual([t.HpLeft, t.Turns], [22, 2]);
  assert.equal(given.Surrendered, true);
  assert.equal(given.Stage, DemoStage.Over);
  const screen = given.EndScreen();
  assert.equal(screen.Title, '瘴牙の走竜に降参しました');
  assert.equal(screen.Lines[1], '残り HP: 22 / 50');
  assert.equal(screen.CanChooseEnemy, true);
  given.Again();
  assert.equal(given.Surrendered, false);
  assert.equal(given.Stage, DemoStage.Ready);
});

test('[isolated] ASurrenderOnADecidedBattle_IsItsFinish', IsoOpt, () => {
  const session = DemoSession.Chain(bareDeck(), 1);
  const source = session.StartBattle();
  const won = tally({ Result: 'Won', HpLeft: 20 });
  assert.equal(session.Surrender(end(source, won, 20, 6)), won);
  assert.equal(session.Surrendered, false);
  assert.equal(session.Stage, DemoStage.BetweenBattles);
  session.GoOn(false);
  assert.deepEqual([session.NextSetup().PlayerStartHp, session.NextSetup().PlayerStartStamina], [20, 6], 'it carried like a win');
});

test('[isolated] AChain_CarriesHpAndStamina_RestsOnTheTallysMaximum_AndAddsUpTheRun', IsoOpt, () => {
  const session = DemoSession.Chain(bareDeck(), 5, null, 0, (id) => (id === 'shadow_hound' ? -1 : 1));
  assert.deepEqual(session.Order.map((e) => e.Id), ['polearm_warped', 'shadow_hound', 'armored_warden']);
  assert.deepEqual(session.Order.map((e) => session.DisclosureOf(e)), [1, 0, 1]);

  // Battle 1: won with 18 HP and 3 stamina left.
  const first = session.StartBattle();
  assert.deepEqual([first.Args.chainIndex, first.Args.chainTotal, first.Args.disclosure, first.Args.seed], [1, 3, 1, 5]);
  const one = tally({ Result: 'Won', Turns: 8, HpLeft: 18, MaxHp: 50, CardsPlayed: 10, Attributes: new Map([[1, 7], [4, 3]]), TraitsFired: 2 });
  session.Finish(end(first, one, 18, 3));
  assert.equal(session.Stage, DemoStage.BetweenBattles);
  const between = session.EndScreen();
  assert.deepEqual({ ...between }, {
    Title: '1 戦目に勝ちました', Won: true,
    Lines: ['何戦目: 1 / 3（錆槍の竜兵）', '残り HP: 18 / 50', 'ターン数: 8', '使った札 10 枚の属性: 攻撃 7・技 3', '特性の発動: 2 回', '次: 瘴牙の走竜'],
    CanGoOn: true, RestLabel: '休んで次へ（HP +15、スタミナ全回復）', GoOnLabel: 'そのまま次へ', CanChooseEnemy: false, ChooseEnemyLabel: '',
    CanAgain: true, AgainLabel: '最初からもう一度', BackLabel: 'デッキ選択へ戻る',
  });
  assert.deepEqual(DemoSession.RestAfter(one), { Hp: 33, Stamina: Constants.BaseMaxStamina });

  // Rest: 18 + 15 HP, stamina full.
  session.GoOn(true);
  assert.equal(session.Index, 1);
  assert.equal(session.Current.Id, 'shadow_hound');
  const second = session.StartBattle();
  assert.deepEqual([second.Args.setup.PlayerStartHp, second.Args.setup.PlayerStartStamina], [33, 10]);
  assert.deepEqual([second.Args.chainIndex, second.Args.disclosure, second.Args.seed], [2, 0, 5 + 7919]);

  // Battle 2: won with 40 HP of a 60 maximum, 2 stamina; go straight on.
  const two = tally({ EnemyId: 'shadow_hound', Result: 'Won', Turns: 9, HpLeft: 40, MaxHp: 60, CardsPlayed: 11, Attributes: new Map([[1, 5], [2, 6]]), TraitsFired: 1 });
  session.Finish(end(second, two, 40, 2));
  assert.equal(session.EndScreen().RestLabel, '休んで次へ（HP +18、スタミナ全回復）', '30% of the tally\'s own maximum');
  assert.equal(session.EndScreen().Lines[2], 'ターン数: 9（平均 8.5）');
  session.GoOn(false);
  const third = session.StartBattle();
  assert.deepEqual([third.Args.setup.PlayerStartHp, third.Args.setup.PlayerStartStamina, third.Args.setup.FieldCells], [40, 2, 7]);

  // Battle 3: lost.
  const three = tally({ EnemyId: 'armored_warden', Result: 'Lost', Turns: 4, HpLeft: 0, MaxHp: 60, CardsPlayed: 6, Attributes: new Map([[2, 6]]), TraitsFired: 0 });
  session.Finish(end(third, three, 0, 0));
  assert.equal(session.Stage, DemoStage.Over);
  assert.equal(session.AllWon, false);
  const over = session.EndScreen();
  assert.equal(over.Title, '3 戦目で力尽きました');
  assert.deepEqual(over.Lines, [
    '何戦目: 3 / 3（鉄壁の門竜）',
    '残り HP: 0 / 60',
    'ターン数: 4（平均 7）',
    '使った札 6 枚の属性: 防御 6',
    '特性の発動: 0 回',
    '連戦の合計（3 戦）: 使った札 27 枚の属性: 攻撃 12・防御 12・技 3',
    '連戦の合計: 特性の発動 3 回、平均ターン数 7',
  ]);
  assert.equal(over.CanGoOn, false);
  assert.equal(over.AgainLabel, '最初からもう一度');
  assert.equal(session.AverageTurns(), '7');

  // Again: the first battle, full HP, the tallies emptied (the same list).
  const tallies = session.Tallies;
  session.Again();
  assert.equal(tallies.length, 0);
  assert.equal(session.Tallies, tallies);
  assert.deepEqual([session.Index, session.Stage, session.NextSetup().PlayerStartHp, session.NextSetup().PlayerStartStamina], [0, 'Ready', null, null]);
  assert.equal(session.StartBattle().Args.seed, 5 + 3 * 7919);
});

test('[isolated] AChainWonToTheEnd_IsAllWon_AndOffersTheSameOrder', IsoOpt, () => {
  const session = DemoSession.Chain(bareDeck(), 1, ['polearm_warped', 'pack_alpha']);
  session.Finish(end(session.StartBattle(), tally({ Result: 'Won', Turns: 6 })));
  assert.equal(session.AllWon, false, 'not over yet');
  session.GoOn(false);
  session.Finish(end(session.StartBattle(), tally({ EnemyId: 'pack_alpha', Result: 'Won', Turns: 7, HpLeft: 12 })));
  assert.equal(session.AllWon, true);
  const screen = session.EndScreen();
  assert.equal(screen.Title, '連戦に全勝しました');
  assert.equal(screen.AgainLabel, '同じ並びをもう一度');
  assert.equal(screen.Lines[0], '何戦目: 2 / 2（統牙の長竜）');
  assert.equal(screen.Lines[5], '連戦の合計（2 戦）: 使った札 0 枚の属性: なし');
  assert.equal(screen.Lines[6], '連戦の合計: 特性の発動 0 回、平均ターン数 6.5');
  assert.equal(screen.Lines.length, 7, 'no next battle');
});

test('[isolated] AChainGivenUp_MidRun_IsOverAndAddedUp', IsoOpt, () => {
  const session = DemoSession.Chain(bareDeck(), 1);
  session.Finish(end(session.StartBattle(), tally({ Result: 'Won', Turns: 5 })));
  session.GoOn(false);
  const second = session.StartBattle();
  second.State = { Result: 'Ongoing', Player: { Hp: 9, Stamina: 1 }, Tally: tally({ EnemyId: 'shadow_hound', Result: 'Ongoing', Turns: 2, HpLeft: 9 }) };
  session.Surrender(second);
  const screen = session.EndScreen();
  assert.equal(screen.Title, '2 戦目で降参しました');
  assert.equal(screen.Lines[2], 'ターン数: 2（平均 3.5）');
  assert.equal(screen.Lines.filter((l) => l.startsWith('連戦の合計')).length, 2);
  assert.equal(screen.CanChooseEnemy, false);
  assert.throws(() => session.GoOn(false), errorIs('InvalidOperationException', 'DemoSession: there is no next battle (Over).'));
  assert.throws(() => session.StartBattle(), errorIs('InvalidOperationException', 'DemoSession: no battle is ready (Over).'));
});

test('[isolated] TheJournalsLevel_IsReadPerEnemy_AndFullWithoutOne', IsoOpt, () => {
  const none = DemoSession.Single(bareDeck(), 'abyss_angler', 3);
  assert.equal(none.DisclosureOf(none.Current), 2);
  for (const [journal, shown] of [[1, 1], [2, 2], [0, 0], [-1, 0], [5, 2]]) {
    const run = DemoSession.Single(bareDeck(), 'abyss_angler', 3, 0, () => journal);
    assert.equal(run.DisclosureOf(run.Current), shown, String(journal));
    assert.equal(run.StartBattle().Args.disclosure, shown);
  }
  assert.throws(() => none.DisclosureOf(null), errorIs('ArgumentNullException', "Value cannot be null. (Parameter 'enemy')"));
  assert.throws(() => DemoSession.RestAfter(null), errorIs('ArgumentNullException', "Value cannot be null. (Parameter 'last')"));
});

// ======================================================================== the golden (demo.golden.json)
//
// Written by the C# program in the scratchpad (demo-oracle/Program.cs) on the real C#: the random
// picks, the averages, every enemy's line, and 36 scripted runs fought on the core — each battle's
// start, tally, stage and end screen, then Again and the first start once more.

const golden = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'demo.golden.json'), 'utf8'));
const ATTRIBUTE_NAMES = [[1, 'Attack'], [2, 'Guard'], [4, 'Skill'], [8, 'Stance']];

test('[isolated] Golden: RandomEnemyId, AverageOf, ChainLine and Breakdown', () => {
  for (const [seed, pick, id] of golden.randomEnemy) assert.equal(DemoSession.RandomEnemyId(seed, pick), id, `${seed}, ${pick}`);
  for (const [turns, text] of golden.averageOf) assert.equal(DemoSession.AverageOf(turns.map((t) => tally({ Turns: t }))), text, String(turns));
  assert.equal(DemoSession.ChainLine(), golden.chainLine);
  assert.deepEqual([
    DemoSession.Breakdown(tally({ Attributes: new Map([[8, 2], [4, 1], [2, 4], [1, 9]]) })),
    DemoSession.Breakdown(tally({ Attributes: new Map([[3, 3], [4, 1]]) })),
  ], golden.breakdowns, 'a key that is two attributes at once is not read');
});

test('Golden: RandomEnemyId, AverageOf, every enemy\'s line and field, ChainLine, Breakdown', realOpt, () => {
  for (const [seed, pick, id] of golden.randomEnemy) assert.equal(m.DemoSession.RandomEnemyId(seed, pick), id, `${seed}, ${pick}`);
  for (const [turns, text] of golden.averageOf) {
    const tallies = turns.map((t) => new m.BattleTally('polearm_warped', m.GameResult.Won, t, 50, 50, 0, new Map(), 0));
    assert.equal(m.DemoSession.AverageOf(tallies), text, String(turns));
  }
  assert.deepEqual(m.Enemies.All.map((e) => [e.Id, m.DemoSession.EnemyLine(e), m.DemoSession.FieldCellsFor(e), m.DemoSession.FieldCellsFor(e, 1, 4)]), golden.enemies);
  assert.equal(m.DemoSession.ChainLine(), golden.chainLine);
  const A = m.BattleAttribute;
  assert.deepEqual([
    m.DemoSession.Breakdown(new m.BattleTally('x', m.GameResult.Won, 1, 1, 50, 0, new Map([[A.Stance, 2], [A.Skill, 1], [A.Guard, 4], [A.Attack, 9]]), 0)),
    m.DemoSession.Breakdown(new m.BattleTally('x', m.GameResult.Won, 1, 1, 50, 0, new Map([[A.Guard | A.Attack, 3], [A.Skill, 1]]), 0)),
  ], golden.breakdowns);
});

function startJson(s) {
  return {
    Enemy: s.State.EnemyDef.Id, Hp: s.State.Player.Hp, Stamina: s.State.Player.Stamina, Cell: s.State.Player.Cell, Gap: s.State.Gap,
    FieldCells: s.State.FieldCells, Draw: s.State.DrawPile.map((c) => c.InstanceId), ChainIndex: s.Frame.Corner.ChainIndex,
    ChainTotal: s.Frame.Corner.ChainTotal, Disclosure: s.Frame.Omen.Disclosure,
  };
}
function tallyJson(t) {
  return {
    EnemyId: t.EnemyId, Result: t.Result, Turns: t.Turns, HpLeft: t.HpLeft, MaxHp: t.MaxHp, CardsPlayed: t.CardsPlayed,
    TraitsFired: t.TraitsFired, Attributes: Object.fromEntries(ATTRIBUTE_NAMES.map(([a, name]) => [name, t.CountOf(a)])),
  };
}
const screenJson = (e) => ({ ...e, Lines: [...e.Lines] });

/** Program.cs Run(): the battles fought (or the one given up on turn 2), resting as told, then Again. */
function Run(session, rest, surrenderAt = 0) {
  const battles = [];
  for (;;) {
    const number = session.Index + 1;
    const source = session.StartBattle(true);
    const start = startJson(source);
    let t;
    if (number === surrenderAt) {
      FightUntilTurn(source, 2);
      t = session.Surrender(source);
    } else {
      FightToTheEnd(source);
      t = session.Finish(source);
    }
    const screen = session.EndScreen();
    const entry = {
      Start: start, Tally: tallyJson(t), Stage: session.Stage, Screen: screenJson(screen),
      AllWon: session.AllWon, Surrendered: session.Surrendered, Average: session.AverageTurns(),
    };
    battles.push(entry);
    if (session.Stage !== m.DemoStage.BetweenBattles) break;
    const r = rest(number);
    entry.Rest = r;
    session.GoOn(r);
  }
  session.Again();
  return { Battles: battles, Again: startJson(session.StartBattle()) };
}

/** The runs of Program.cs, in its order, by name. */
function goldenRuns() {
  const runs = [];
  const Random = (seed) => m.DeckBuilder.Random(seed).Build();
  for (const id of ['polearm_warped', 'shadow_hound', 'mist_archer', 'armored_warden', 'abyss_angler', 'distortion_root']) {
    for (const seed of [1, 3, 7]) runs.push(['single ' + id + ' ' + seed, () => Run(m.DemoSession.Single(Random(9), id, seed), () => false)]);
  }
  for (const seed of [1, 2, 3, 4, 5, 6, 8, 11]) runs.push(['chain ' + seed, () => Run(m.DemoSession.Chain(Random(9), seed), (n) => (n + seed) % 2 === 0)]);
  for (const seed of [1, 2, 3, 4]) {
    runs.push(['chain pair ' + seed, () => Run(m.DemoSession.Chain(Random(seed), seed, ['polearm_warped', 'pack_alpha'], 1), () => true)]);
  }
  runs.push(['single surrendered', () => Run(m.DemoSession.Single(Random(9), 'polearm_warped', 3), () => false, 1)]);
  for (const seed of [1, 2, 3]) runs.push(['chain surrendered ' + seed, () => Run(m.DemoSession.Chain(Random(9), seed), () => false, 2)]);
  // A departure with tools: a cell back, stamina +3, Guard 5 on the first hit; a journal at 1. One
  // Departure shared by both runs, as in the C#.
  let departure = null;
  const dep = () => {
    if (departure == null) {
      const deck = Random(4);
      departure = new m.Departure(deck, ['maai_no_kutsu', 'ikitsugi_no_fue'], [], 1, deck[0].Def.Id, 3, 5, 0);
    }
    return departure;
  };
  runs.push(['departure single', () => Run(m.DemoSession.Single(dep(), 'polearm_warped', 5, 2, () => 1), () => false)]);
  runs.push(['departure chain', () => Run(m.DemoSession.Chain(dep(), 5, null, 1, (id) => (id === 'shadow_hound' ? -1 : 2)), (n) => n === 1)]);
  return runs;
}

if (m) {
  const runs = goldenRuns();
  assert.deepEqual(runs.map(([name]) => name), golden.runs.map((r) => r.Name), 'the runs are the C# program\'s');
  for (const [i, [name, run]] of runs.entries()) {
    test(`Golden run: ${name}`, () => {
      const want = golden.runs[i];
      const got = run();
      assert.equal(got.Battles.length, want.Battles.length, 'battles fought');
      for (let b = 0; b < want.Battles.length; b++) {
        const [g, w] = [got.Battles[b], want.Battles[b]];
        assert.deepEqual(g.Start, w.Start, `battle ${b + 1}: the start`);
        assert.deepEqual(g.Tally, w.Tally, `battle ${b + 1}: the tally`);
        assert.deepEqual(g.Screen, w.Screen, `battle ${b + 1}: the end screen`);
        assert.deepEqual(g, w, `battle ${b + 1}`);
      }
      assert.deepEqual(got.Again, want.Again, 'again: the first start once more');
    });
  }
} else {
  test('Golden runs (36 scripted demo runs on the core)', realOpt, () => {});
}
