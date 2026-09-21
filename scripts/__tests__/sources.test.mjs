import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { check, forLane, parseLedger } from '../sources.mjs';

const LEDGER = `# SOURCES

## 2. 現行の正本

| 主題 | 正本 | 持ち主 | 状態 | 読むレーン |
| --- | --- | --- | --- | --- |
| 構想 | \`.claude/docs/vision/concept.md\` | design | APPROVED | all |
| ルール | \`.claude/docs/battle_document/core.md\` | battle | v4 | cards, design |
| 課題 | GitHub Issues | 人 | — | all |

## 3. 旧版と記録

| ファイル | 状態 | 代わりに読むもの |
| --- | --- | --- |
| \`.claude/docs/battle_document/core_old.md\` | SUPERSEDED | \`core.md\` |

## 4. 次の節
`;

function tree(files) {
  const root = mkdtempSync(join(tmpdir(), 'sources-'));
  for (const [path, body] of Object.entries(files)) {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), body);
  }
  return root;
}

const COMPLETE = {
  '.claude/docs/vision/concept.md': '# concept',
  '.claude/docs/battle_document/core.md': '# core',
  '.claude/docs/battle_document/core_old.md': '> **SUPERSEDED**: read core.md',
};

test('parseLedger reads both tables and leaves pathless rows without a path', () => {
  const ledger = parseLedger(LEDGER);
  assert.equal(ledger.current.length, 3);
  assert.equal(ledger.current[1].path, '.claude/docs/battle_document/core.md');
  assert.deepEqual(ledger.current[1].readers, ['cards', 'design']);
  assert.equal(ledger.current[2].path, null);
  assert.equal(ledger.retired[0].status, 'SUPERSEDED');
});

test('forLane splits what a lane writes from what it reads', () => {
  const { writes, reads } = forLane(parseLedger(LEDGER), 'cards');
  assert.deepEqual(writes, []);
  assert.deepEqual(
    reads.map((r) => r.topic),
    ['構想', 'ルール', '課題'],
  );
  assert.deepEqual(
    forLane(parseLedger(LEDGER), 'battle').writes.map((r) => r.topic),
    ['ルール'],
  );
});

test('check passes when the ledger matches the tree', () => {
  assert.deepEqual(check(parseLedger(LEDGER), tree(COMPLETE)), []);
});

test('check reports a missing file, an unlisted doc and a retired doc without a banner', () => {
  const root = tree({
    '.claude/docs/battle_document/core.md': '# core',
    '.claude/docs/battle_document/core_old.md': '# old core',
    '.claude/docs/battle_document/stray.md': '# stray',
    '.claude/docs/vision/plans/plan.md': '# plans stay outside the ledger',
  });
  assert.deepEqual(check(parseLedger(LEDGER), root).sort(), [
    'design doc not in the ledger: .claude/docs/battle_document/stray.md',
    'missing file: .claude/docs/vision/concept.md',
    'retired without a banner in its first 8 lines: .claude/docs/battle_document/core_old.md',
  ]);
});
