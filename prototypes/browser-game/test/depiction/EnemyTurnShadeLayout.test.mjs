// Tests for src/bridge/depictionScript/EnemyTurnShadeLayout.js (port of View/Depiction/Script/EnemyTurnShadeLayout.cs).
// Ported cases: Assets/View/Depiction/Tests/EnemyTurnShadeLayoutTests.cs (all).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { EnemyTurnShadeLayout } from '../../src/bridge/depictionScript/EnemyTurnShadeLayout.js';

test('TheShade_RunsFromY790ToTheBottom_ClearToBlack72', () => {
  assert.equal(EnemyTurnShadeLayout.Top, 790, "the throw line's height (§5.3)");
  assert.equal(EnemyTurnShadeLayout.Height, 290);
  assert.equal(EnemyTurnShadeLayout.TopAlpha, 0);
  assert.equal(EnemyTurnShadeLayout.BottomAlpha, Math.fround(0.72));
});

test('TheEndTurnPlate_StandsAt40Percent', () => {
  assert.equal(EnemyTurnShadeLayout.EndTurnAlpha, Math.fround(0.4), '§4.9 敵の番は不透明度 40%');
});
