// Tests for src/bridge/depictionScript/PileList.js (port of View/Depiction/Script/PileList.cs).
// Ported cases: ThePileList_WritesOneRowPerKind_OrNone of Assets/View/Depiction/Tests/EffectTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { PileList } from '../../src/bridge/depictionScript/PileList.js';
import { PileKind } from '../../src/bridge/depictionScript/PileLayout.js';
import { PileCard } from '../../src/bridge/depictionScript/DepictionTypes.js';

test('ThePileList_WritesOneRowPerKind_OrNone', () => {
  // battle_ui_ux_v2 §1.1 C4: 同種の残り「突き 2 / 3」.
  const rows = [
    new PileCard({ Name: '突き', InPile: 2, InDeck: 3 }),
    new PileCard({ Name: '岩の構え', InPile: 1, InDeck: 1 }),
  ];
  assert.equal(PileList.BodyText(rows), '突き 2 / 3\n岩の構え 1 / 1');
  assert.equal(PileList.BodyText([]), PileList.Empty);
  assert.deepEqual([PileKind.Draw, PileKind.Discard, PileKind.Exile].map(PileList.TabLabel), ['山札', '捨札', '除外']);
  assert.equal(PileList.WidthShare, Math.fround(0.30), "the journal drawer's mirror: the left 30%");
});

// ---- port-specific (checked against the C#) ----

test('Port_TheWords', () => {
  assert.equal(PileList.BodyText(null), 'なし');
  assert.equal(PileList.CloseLabel, '閉じる（Esc）');
  assert.equal(PileList.RowSize, 18);
  assert.equal(PileList.TabLabel('7'), '山札', 'anything else reads as the draw pile');
  assert.equal(PileList.BodyText([new PileCard({ Name: null, InPile: 0, InDeck: 1 })]), ' 0 / 1');
});
