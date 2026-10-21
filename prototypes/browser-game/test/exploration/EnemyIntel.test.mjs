// EnemyIntel: the socket the enemy data plugs into. Ports the intel parts of
// ExplorationSessionTests.cs (WithoutEnemyDataTheEnemyIsAQuestionMark / TheSurveyIsHandedToTheEnemySocket).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { NodeIntel, NoEnemyIntel, IEnemyIntel } from '../../src/bridge/explorationScript/EnemyIntel.js';

test('NodeIntel_TurnsNullsIntoEmpties', () => {
  const intel = new NodeIntel(null, 1, null);
  assert.equal(intel.EnemyName, '');
  assert.equal(intel.Disclosure, 1);
  assert.deepEqual(intel.Lines, []);
  const full = new NodeIntel('歪み兵', 2, ['傾向: 間合い 1〜2 で突く']);
  assert.equal(full.EnemyName, '歪み兵');
  assert.deepEqual(full.Lines, ['傾向: 間合い 1〜2 で突く']);
});

test('NoEnemyIntel_KnowsNothing', () => {
  const intel = new NoEnemyIntel();
  assert.equal(intel.ForNode({ Id: 0 }, 1, false), null);
  assert.equal(intel.ForNode({ Id: 0 }, 1, true), null);
  assert.deepEqual(intel.Pages(1, false), []);
  assert.deepEqual(intel.Pages(3, true), []);
  assert.ok(IEnemyIntel != null, 'the interface name resolves');
});
