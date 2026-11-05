import { test } from 'node:test';
import assert from 'node:assert/strict';
import { plan } from '../issue-prio.mjs';

const REMINDER_BODY = '`prio:` ラベルが付いていません。`prio:1`〜`prio:4` のどれか 1 つを付けてください。';
const issue = (number, labels = [], body = '', comments = []) => ({ number, labels, body, comments });

test('an issue from the dropdown gets its prio label', () => {
  const body = '### 優先順位\n\nprio:3\n\n### 内容\n...';
  assert.deepEqual(plan([issue(10, ['type:task'], body)]), [{ number: 10, kind: 'label', prio: 3 }]);
});

test('an issue without a dropdown choice gets the reminder once', () => {
  assert.deepEqual(plan([issue(11)]), [{ number: 11, kind: 'reminder' }]);
  assert.deepEqual(plan([issue(11, [], '', [{ body: REMINDER_BODY }])]), []);
});

test('an issue that already has a prio label is left alone', () => {
  const body = '### 優先順位\n\nprio:1';
  assert.deepEqual(plan([issue(12, ['prio:2'], body), issue(13, ['prio:1', 'prio:3'])]), []);
});

test('a dropdown value outside 1 to 4 falls back to the reminder', () => {
  assert.deepEqual(plan([issue(14, [], '### 優先順位\n\nprio:9')]), [{ number: 14, kind: 'reminder' }]);
});
