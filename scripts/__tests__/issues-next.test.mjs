import { test } from 'node:test';
import assert from 'node:assert/strict';
import { classify, isEpic, parseDependencies, prioLevels, sevRank } from '../issues-next.mjs';

const issue = (number, labels, body = '') => ({ number, title: `issue ${number}`, labels, body });

test('prioLevels reads every prio label', () => {
  assert.deepEqual(prioLevels(['type:task', 'prio:2']), [2]);
  assert.deepEqual(prioLevels(['prio:1', 'prio:3']), [1, 3]);
  assert.deepEqual(prioLevels(['prio:5', 'sev:minor']), []);
});

test('sevRank falls back when no sev label exists', () => {
  assert.equal(sevRank(['sev:blocking']), 0);
  assert.equal(sevRank(['type:task']), 3);
});

test('parseDependencies ignores see-also numbers and other sections', () => {
  const body = '## Summary\n\n#1 is mentioned here\n\n## 依存\n\n#10、#11。関連: #12 / #13\n\n## 親\n\n#14\n';
  assert.deepEqual(parseDependencies(body), [10, 11]);
  assert.deepEqual(parseDependencies('## Summary\n\nno section'), []);
});

test('isEpic detects the child list heading', () => {
  assert.equal(isEpic('## 子 Issue\n\n- [ ] #2'), true);
  assert.equal(isEpic('## Summary'), false);
});

test('classify orders ready issues by prio, then sev, then number', () => {
  const groups = classify([
    issue(5, ['prio:2', 'sev:blocking']),
    issue(4, ['prio:1', 'sev:minor']),
    issue(3, ['prio:1', 'sev:blocking']),
    issue(2, ['prio:1', 'sev:blocking']),
  ]);
  assert.deepEqual(groups.ready.map((r) => r.number), [2, 3, 4, 5]);
});

test('classify sends blocked, human, epic and frozen issues to their own groups', () => {
  const groups = classify([
    issue(1, ['prio:1']),
    issue(2, ['prio:1'], '## 依存\n\n#1'),
    issue(3, ['prio:1'], '## 依存\n\n#999'),
    issue(4, ['prio:2', 'type:human']),
    issue(5, ['prio:1'], '## 子 Issue\n\n- [ ] #1'),
    issue(6, ['prio:4', 'status:frozen']),
  ]);
  assert.deepEqual(groups.ready.map((r) => r.number), [1, 3]);
  assert.deepEqual(groups.waiting.map((r) => [r.number, r.blockedBy]), [[2, [1]]]);
  assert.deepEqual(groups.human.map((r) => r.number), [4]);
  assert.deepEqual(groups.epics.map((r) => r.number), [5]);
  assert.deepEqual(groups.frozen.map((r) => r.number), [6]);
});

test('classify reports issues without exactly one prio label', () => {
  const groups = classify([issue(1, []), issue(2, ['prio:1', 'prio:2']), issue(3, ['prio:3'])]);
  assert.deepEqual(groups.unlabeled.map((r) => [r.number, r.found]), [[2, 2], [1, 0]]);
});
