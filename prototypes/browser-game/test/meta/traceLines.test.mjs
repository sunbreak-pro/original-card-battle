// The meta screens' trace lines over Inheritance's views (src/scenes/meta/ending.js withWaitingTrace,
// src/scenes/meta/hall.js pastTraceLine): a trace left behind an older one on the same layer waits
// (docs/INHERITANCE.md 試作で決めたこと 4), and a visited trace is 回収した whatever was picked (R-10, 試作 5).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { newLineage, startLife, recordDeath, recordSurvival, takeTrace, endingView, hallView, InheritanceText, InheritanceRules } from '../../src/core/inheritance/Inheritance.js';
import { RunPhase } from '../../src/core/dungeon/ExplorationState.js';
import { withWaitingTrace } from '../../src/scenes/meta/ending.js';
import { pastTraceLine } from '../../src/scenes/meta/hall.js';
import { MetaText } from '../../src/scenes/meta/text.js';

const DECK = [{ CardId: 'tsuki', Rank: 2 }, { CardId: 'harai', Rank: 1 }];
const JOURNAL = { Disclosure: { sabiyari: 1 }, Pages: 0, Memo: '' };

function dieAt(lineage, layer) {
  const s = startLife(lineage, 1n);
  return recordDeath(s.Lineage, s.Life, { Layer: layer, NodeId: 3, Ending: RunPhase.Fallen }, DECK, JOURNAL);
}
const visit = (layer) => InheritanceText.TraceVisit(layer, InheritanceRules.LegacyPicks);

test('the first trace on a layer keeps endingView\'s 「次の踏破者が…受け取れる」', () => {
  const l = dieAt(newLineage(7n), 7);
  const view = endingView(l);
  assert.ok(view.Lines.includes(visit(7)));
  assert.equal(withWaitingTrace(view, l), view);
});

test('a death behind an older untaken trace on the same layer says it waits, naming the older one', () => {
  const l1 = dieAt(newLineage(7n), 7);
  const l2 = dieAt(l1, 7);
  const first = l2.Lives[0];
  const view = withWaitingTrace(endingView(l2), l2);
  assert.ok(!view.Lines.includes(visit(7)));
  assert.ok(view.Lines.includes(MetaText.TraceWaits(7, first.Generation, first.Name)));
  assert.equal(view.Lines.length, endingView(l2).Lines.length);
  // the hall says the same of it
  assert.equal(hallView(l2).Traces.find((t) => t.Generation === 2).AppearsLine, InheritanceText.Waits);
});

test('two older traces ahead: the count, not one name', () => {
  const l = dieAt(dieAt(dieAt(newLineage(7n), 7), 7), 7);
  const view = withWaitingTrace(endingView(l), l);
  assert.ok(view.Lines.includes(MetaText.TraceWaitsMany(7, 2)));
});

test('a trace on another layer does not make it wait', () => {
  const l = dieAt(dieAt(newLineage(7n), 6), 7);
  const view = endingView(l);
  assert.equal(withWaitingTrace(view, l), view);
});

test('a cleared life\'s card trace behind an older layer-7 trace gets the wait after its line', () => {
  const l1 = dieAt(newLineage(7n), 7);
  const s = startLife(l1, 1n);
  const l2 = recordSurvival(s.Lineage, s.Life, { Layer: 7, NodeId: 9, Ending: RunPhase.Completed }, DECK, JOURNAL, {});
  const before = endingView(l2);
  const after = withWaitingTrace(before, l2);
  const wait = MetaText.TraceWaits(7, l1.Lives[0].Generation, l1.Lives[0].Name);
  const at = after.Lines.indexOf(wait);
  assert.ok(at > 0, JSON.stringify(after.Lines));
  assert.ok(after.Lines[at - 1].includes('札の痕跡'));
  assert.equal(after.Lines.length, before.Lines.length + 1);
});

test('the hall says 回収した for a trace left with 何も受け取らない', () => {
  const l1 = dieAt(newLineage(7n), 4);
  const s = startLife(l1, 1n);
  const left = takeTrace(s.Lineage, l1.Traces[0].Id, [], { Layer: 4 }).Lineage;
  const l2 = recordDeath(left, null, { Layer: 2, NodeId: 1, Ending: RunPhase.Fallen }, DECK, JOURNAL);
  const view = hallView(l2);
  const first = view.PastLives.find((p) => p.Generation === 1);
  const second = view.PastLives.find((p) => p.Generation === 2);
  assert.equal(pastTraceLine(l2, first), MetaText.TraceCollected(2, l2.Lives[1].Name));
  assert.equal(pastTraceLine(l2, second), InheritanceText.TraceStatus.Untaken);
});
