import assert from 'node:assert/strict';
import { test } from 'node:test';
import { compactTableLine, compactTables } from '../md-tables.mjs';

test('compactTableLine drops the padding around pipes and shortens the rule', () => {
  assert.equal(compactTableLine('| 主題     | 状態   |'), '| 主題 | 状態 |');
  assert.equal(compactTableLine('| -------- | :----: |'), '| --- | :---: |');
  assert.equal(compactTableLine('| a | b |'), '| a | b |');
});

test('compactTableLine leaves text that is not a table row alone', () => {
  assert.equal(compactTableLine('a  |  b'), 'a  |  b');
  assert.equal(compactTableLine('- list  item'), '- list  item');
});

test('compactTables keeps code fences and CRLF line endings as they are', () => {
  const before = '| a    | b |\r\n| ---- | - |\r\n```\r\n| keep    | me |\r\n```\r\n';
  assert.equal(compactTables(before), '| a | b |\r\n| --- | - |\r\n```\r\n| keep    | me |\r\n```\r\n');
});

test('compactTables is idempotent', () => {
  const once = compactTables('| x      | y |\n| ------ | --- |\n| longer | z |\n');
  assert.equal(compactTables(once), once);
});
