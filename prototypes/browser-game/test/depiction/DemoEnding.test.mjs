// Tests for src/bridge/depiction/DemoEnding.js (port of View/Depiction/Bridge/DemoEnding.cs).
//
// Real layer: Assets/View/Depiction/Bridge/Tests/DemoEndingTests.cs (a tally or an end screen that
// throws is reported, and a fallback end screen still offers to choose again), on the real
// DemoSession — skipped while a module another agent ports is missing.
//
// Isolated layer: DemoEnding on the DemoSession isolated from BattleCore (_demoIsolate.mjs), so the
// report / fallback logic and the fallback's words are checked on their own.
//
// C# passes `session.EndScreen` as a method group (Func<DemoEndScreen>); JS needs the session bound:
// `() => session.EndScreen()`.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { demoStubs, importIsolated, isolatedDemoSession, loadAll, needs } from './_demoIsolate.mjs';

const { m, missing } = await loadAll([
  'bridge/depiction/DemoEnding.js',
  'bridge/depiction/DemoSession.js',
  'bridge/depiction/DeckBuilder.js',
]);
const realOpt = needs(missing);

/** DemoEndingTests.AssertOffersToChooseAgain */
function AssertOffersToChooseAgain(screen) {
  assert.equal(screen.CanChooseEnemy, true, 'the mode screen is offered');
  assert.ok(screen.ChooseEnemyLabel.includes('選び直す'));
  assert.equal(screen.BackLabel, 'デッキ選択へ戻る');
  assert.equal(screen.CanAgain, false, '「もう一度」 would step a run left half-done');
  assert.equal(screen.CanGoOn, false, "nor would the chain's rest");
  assert.notEqual(screen.Title, '');
  assert.ok(screen.Lines.length > 0);
}

// ======================================================================== the real layer

const Deck = () => m.DeckBuilder.Random(9).Build();

test('AFinishThatThrows_IsReported_AndTheFallbackOffersToChooseAgain', realOpt, () => {
  const session = m.DemoSession.Single(Deck(), 'polearm_warped', 1);
  const source = session.StartBattle();
  const reported = [];

  // The battle is still going, so DemoSession.Finish throws.
  const tallied = m.DemoEnding.Tally(() => session.Finish(source), (e) => reported.push(e));
  const screen = m.DemoEnding.Screen(tallied, () => session.EndScreen(), (e) => reported.push(e));

  assert.equal(tallied, false);
  assert.equal(reported.length, 1);
  assert.equal(reported[0].name, 'InvalidOperationException');
  AssertOffersToChooseAgain(screen);
});

test('ASurrenderThatThrows_IsReported_AndTheFallbackOffersToChooseAgain', realOpt, () => {
  const session = m.DemoSession.Single(Deck(), 'polearm_warped', 1);
  session.StartBattle();
  const reported = [];

  const tallied = m.DemoEnding.Tally(() => session.Surrender(null), (e) => reported.push(e));
  const screen = m.DemoEnding.Screen(tallied, () => session.EndScreen(), (e) => reported.push(e));

  assert.equal(tallied, false);
  assert.equal(reported.length, 1);
  assert.equal(reported[0].name, 'ArgumentNullException');
  AssertOffersToChooseAgain(screen);
});

test('AnEndScreenThatThrows_IsReported_AndTheFallbackOffersToChooseAgain', realOpt, () => {
  // Nothing tallied yet, so DemoSession.EndScreen throws.
  const session = m.DemoSession.Single(Deck(), 'polearm_warped', 1);
  const reported = [];

  const screen = m.DemoEnding.Screen(true, () => session.EndScreen(), (e) => reported.push(e));

  assert.equal(reported.length, 1);
  assert.equal(reported[0].name, 'InvalidOperationException');
  AssertOffersToChooseAgain(screen);
});

test('AnEndScreenThatComesOutEmpty_FallsBack', realOpt, () => {
  const reported = [];
  AssertOffersToChooseAgain(m.DemoEnding.Screen(true, () => null, (e) => reported.push(e)));
  AssertOffersToChooseAgain(m.DemoEnding.Screen(true, null, (e) => reported.push(e)));
  assert.equal(reported.length, 0);
});

test('AReportThatThrows_DoesNotTakeTheFallbackAway', realOpt, () => {
  const broken = () => { throw new Error('the log is broken'); };

  const tallied = m.DemoEnding.Tally(() => { throw new Error('the tally'); }, broken);
  const screen = m.DemoEnding.Screen(true, () => { throw new Error('the words'); }, broken);

  assert.equal(tallied, false);
  AssertOffersToChooseAgain(screen);
});

test('AnEndingThatGoesThrough_ShowsTheSessionsOwnEndScreen', realOpt, () => {
  const session = m.DemoSession.Single(Deck(), 'polearm_warped', 1);
  const source = session.StartBattle();
  const reported = [];

  const tallied = m.DemoEnding.Tally(() => session.Surrender(source), (e) => reported.push(e));
  const screen = m.DemoEnding.Screen(tallied, () => session.EndScreen(), (e) => reported.push(e));

  assert.equal(tallied, true);
  assert.equal(reported.length, 0);
  assert.ok(screen.Title.includes('降参しました'));
  assert.equal(screen.CanAgain, true);
  assert.equal(screen.AgainLabel, 'もう一度');
  assert.equal(screen.ChooseEnemyLabel, '敵を選び直す');
});

// ======================================================================== the isolated layer

const demo = await isolatedDemoSession();
const { DemoEnding } = (await importIsolated('bridge/depiction/DemoEnding.js', { './DemoSession.js': { url: demo.url } })).module;
const { DemoSession, DemoEndScreen } = demo.module;
const stubs = await demoStubs();
const bareDeck = () => Array.from({ length: 20 }, (_, i) => ({ InstanceId: 'c' + i }));

test('[isolated] TheFallback_IsThePlainEndScreen_WordForWord', () => {
  const screen = DemoEnding.Fallback();
  assert.ok(screen instanceof DemoEndScreen);
  assert.deepEqual({ ...screen }, {
    Title: '結果を表示できませんでした', Won: false,
    Lines: ['戦闘の記録の途中で問題が起きました。', '戦い方かデッキを選び直してください。'],
    CanGoOn: false, RestLabel: '', GoOnLabel: '', CanChooseEnemy: true, ChooseEnemyLabel: '戦い方を選び直す',
    CanAgain: false, AgainLabel: '', BackLabel: 'デッキ選択へ戻る',
  });
  AssertOffersToChooseAgain(screen);
  assert.notEqual(DemoEnding.Fallback(), screen, 'a fresh screen each time');
});

test('[isolated] TheTally_SaysWhetherItWentThrough_AndReportsAThrow', () => {
  const reported = [];
  let ran = 0;
  assert.equal(DemoEnding.Tally(() => { ran++; }, (e) => reported.push(e)), true);
  assert.equal(ran, 1);
  assert.equal(reported.length, 0);
  const thrown = new Error('the tally');
  assert.equal(DemoEnding.Tally(() => { throw thrown; }, (e) => reported.push(e)), false);
  assert.deepEqual(reported, [thrown]);
  assert.equal(DemoEnding.Tally(() => { throw new Error('unreported'); }, null), false, 'no report is fine');
  assert.throws(() => DemoEnding.Tally(null, () => {}), (e) => e.name === 'ArgumentNullException' && e.message === "Value cannot be null. (Parameter 'tally')");
});

test('[isolated] TheScreen_IsBuildsOwn_OrTheFallback', () => {
  const reported = [];
  const own = new DemoEndScreen({ Title: 'x' });
  let built = 0;
  const build = () => { built++; return own; };
  assert.equal(DemoEnding.Screen(true, build, (e) => reported.push(e)), own);
  assert.equal(built, 1);
  assert.equal(DemoEnding.Screen(false, build, (e) => reported.push(e)).Title, '結果を表示できませんでした', 'not tallied: the words are not asked for');
  assert.equal(built, 1);
  const thrown = new Error('the words');
  assert.equal(DemoEnding.Screen(true, () => { throw thrown; }, (e) => reported.push(e)).Title, '結果を表示できませんでした');
  assert.equal(DemoEnding.Screen(true, () => undefined, (e) => reported.push(e)).Title, '結果を表示できませんでした');
  assert.equal(DemoEnding.Screen(true, null, null).CanChooseEnemy, true);
  assert.deepEqual(reported, [thrown]);
  const broken = () => { throw new Error('the log is broken'); };
  assert.equal(DemoEnding.Tally(() => { throw new Error('the tally'); }, broken), false);
  AssertOffersToChooseAgain(DemoEnding.Screen(true, () => { throw new Error('the words'); }, broken));
});

test('[isolated] TheFlow_OverTheSessionsOwnRefusals', () => {
  // DemoEndingTests' cases, on the isolated session: a Finish on a battle still going, a Surrender(null)
  // and an EndScreen with nothing tallied all fall back; a Surrender that goes through shows the session's screen.
  const session = DemoSession.Single(bareDeck(), 'polearm_warped', 1);
  const source = session.StartBattle();
  const reported = [];
  const tallied = DemoEnding.Tally(() => session.Finish(source), (e) => reported.push(e));
  AssertOffersToChooseAgain(DemoEnding.Screen(tallied, () => session.EndScreen(), (e) => reported.push(e)));
  assert.deepEqual(reported.map((e) => e.name), ['InvalidOperationException']);

  assert.equal(DemoEnding.Tally(() => session.Surrender(null), (e) => reported.push(e)), false);
  assert.equal(reported[1].name, 'ArgumentNullException');

  const fresh = DemoSession.Single(bareDeck(), 'polearm_warped', 1);
  AssertOffersToChooseAgain(DemoEnding.Screen(true, () => fresh.EndScreen(), (e) => reported.push(e)));
  assert.equal(reported[2].message, 'DemoSession: no battle has finished yet.');

  assert.equal(DemoEnding.Tally(() => session.Surrender(source), (e) => reported.push(e)), true);
  const screen = DemoEnding.Screen(true, () => session.EndScreen(), (e) => reported.push(e));
  assert.equal(screen.Title, '錆槍の竜兵に降参しました');
  assert.equal(screen.CanAgain, true);
  assert.equal(screen.AgainLabel, 'もう一度');
  assert.equal(screen.ChooseEnemyLabel, '敵を選び直す');
  assert.equal(reported.length, 3);
  assert.ok(stubs.BattleTally, 'the stubs are the ones the session reads');
});
