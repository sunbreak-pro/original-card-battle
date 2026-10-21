// Tests for src/core/dungeonContent/ItemEffect.js — the effect cases of
// DungeonContent.Tests/ItemCatalogueTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { enumValues } from '../../src/shared/cs.js';
import { ItemEffect, EffectSide, ItemEffects } from '../../src/core/dungeonContent/ItemEffect.js';
import { caught, expectedError } from './_helpers.mjs';

test('every effect knows which core reads it, and tokens are unique', () => {
  const effects = enumValues(ItemEffect);
  assert.equal(effects.length, 13);
  assert.deepEqual(effects.map((e) => ItemEffects.Side(e)), [
    'Exploration', 'Exploration', 'Exploration', 'Exploration', 'Exploration', 'Exploration', 'Exploration',
    'Exploration', 'Exploration', 'Battle', 'Battle', 'Battle', 'Battle',
  ]);
  const tokens = effects.map((e) => ItemEffects.ToToken(e));
  assert.deepEqual(tokens, [
    'miasma_density', 'miasma_gauge', 'time_limit', 'choose_survey_target', 'interlude_heal', 'heal_hp',
    'max_stamina', 'refill_stamina', 'teleport', 'wider_start', 'opening_stamina', 'opening_guard', 'blunt_boss_state',
  ]);
  assert.equal(new Set(tokens).size, tokens.length);
  assert.deepEqual(enumValues(EffectSide), ['Exploration', 'Battle']);
});

test('an undefined value throws the C# message', () => {
  assert.equal(caught(() => ItemEffects.Side(99)), expectedError('effect.invalid'));
  assert.equal(caught(() => ItemEffects.ToToken(99)), expectedError('effectToken.invalid'));
});
