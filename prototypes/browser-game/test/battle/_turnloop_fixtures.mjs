// Shared test helpers for test/battle/TurnLoop.test.mjs and EnemyAi.test.mjs.
// Port of unity-port/BattleCore.Tests/Fixtures.cs plus small assertion helpers.
//
// The battle modules are loaded dynamically (loadDeps): while a sibling module on an import path is
// not written yet, a static import would make the whole test file fail to load. loadDeps hands back
// a skip reason ('needs Statuses.js') instead, so the suites report as skipped. Any other load error
// (a syntax error, a TDZ error from an import cycle) is re-thrown: it must fail loudly.

import assert from 'node:assert/strict';
import { csEquals, toParityJson } from '../../src/shared/cs.js';

/**
 * Imports every module of `paths` (relative to `base`) and merges their exports.
 * @returns {Promise<{ mods: object|null, skip: string|false }>}
 */
export async function loadDeps(base, paths) {
  const mods = {};
  try {
    for (const p of paths) Object.assign(mods, await import(new URL(p, base).href));
    return { mods, skip: false };
  } catch (e) {
    if (e && e.code === 'ERR_MODULE_NOT_FOUND') {
      const m = /Cannot find module '([^']+)'/.exec(e.message);
      return { mods: null, skip: `needs ${m ? m[1].split('/').pop() : 'a module'}` };
    }
    throw e;
  }
}

const B = '../../src/core/battle/';
const deps = await loadDeps(import.meta.url, [B + 'Types.js', B + 'Constants.js', B + 'Statuses.js', B + 'IRng.js']);
/** Why the fixtures cannot be built (a sibling module is missing), or false. */
export const fixturesSkip = deps.skip;
// Cards.js only backs TwentyCardDeck; the suites that call it load Cards.js themselves.
const cards = deps.skip ? deps : await loadDeps(import.meta.url, [B + 'Cards.js']);
const M = { ...(deps.mods ?? {}), ...(cards.mods ?? {}) };

/** FixedRng(0.9999999) makes Fisher-Yates a no-op: the draw order is the deck order. */
export const NoRng = M.FixedRng ? new M.FixedRng(0.9999999) : null;

/** Fixtures.Card(id, column = 1, face = null, attributes = Attack, trait = null, targets = One). */
export function Card(id, { column = 1, face = null, attributes = M.BattleAttribute.Attack, trait = null, targets = M.TargetKind.One } = {}) {
  return new M.CardDef(id, id, attributes, column, face ?? new M.Face(), trait, targets, '');
}

/** Fixtures.EnemyAction(id, column = 1, face = null, attributes = Attack, trait = null, targets = One). */
export function EnemyAction(id, { column = 1, face = null, attributes = M.BattleAttribute.Attack, trait = null, targets = M.TargetKind.One } = {}) {
  return new M.EnemyActionDef(id, id, attributes, column, face ?? new M.Face(), trait, targets, '');
}

/** Fixtures.Combatant(hp = 50, stamina = 10, guard = 0, cell = 2, size = 1, statuses = null). */
export function Combatant({ hp = M.Constants.PlayerMaxHp, stamina = M.Constants.BaseMaxStamina, guard = 0, cell = M.Constants.PlayerStartCell, size = 1, statuses = null } = {}) {
  return new M.CombatantState(hp, hp, stamina, M.Constants.BaseMaxStamina, guard, cell, size, statuses ?? M.StatusSet.Empty);
}

/**
 * Fixtures.Enemy: a three-branch enemy with one action per branch. Size 1 unless asked otherwise;
 * every action reaches 0〜1 unless its face says so.
 */
export function Enemy(id = 'dummy', { size = 1, atZero = null, atOneToTwo = null, atThreePlus = null } = {}) {
  atZero ??= EnemyAction('near_hit', { face: new M.Face({ Power: 5 }) });
  atOneToTwo ??= EnemyAction('mid_hit', { face: new M.Face({ Power: 4, Reach: new M.Reach(1, 2) }) });
  atThreePlus ??= EnemyAction('advance', { face: new M.Face({ Move: 1 }), attributes: M.BattleAttribute.None, targets: M.TargetKind.Self });
  const actions = new Map();
  for (const a of [atZero, atOneToTwo, atThreePlus]) actions.set(a.Id, a);
  return new M.EnemyDef(
    id, id, 60, 10, 2, size,
    [atZero.Id], [atOneToTwo.Id], [atThreePlus.Id],
    actions);
}

/** A 20-card deck of ten kinds × 2. */
export function TwentyCardDeck() {
  const defs = [];
  for (let i = 0; i < 10; i++) defs.push(Card(`card_${i}`, { column: (i % 3) + 1, face: new M.Face({ Power: 6 + i }) }));
  return M.Cards.BuildDeck(defs, 2);
}

/** A deck: the given cards first (id-i), then fillers up to `size`. */
export function DeckOf(filler, size, ...cards) {
  const deck = [];
  for (let i = 0; i < cards.length; i++) deck.push(new M.CardInstance(cards[i].Id + '-' + i, cards[i]));
  for (let i = deck.length; i < size; i++) deck.push(new M.CardInstance(filler.Id + '-' + i, filler));
  return deck;
}

/**
 * C# StatusSet.Of((kind, n), ...): pairs as [kind, n]. The C# tuple is a named tuple
 * (Kind, Stacks), so the JS port takes { Kind, Stacks } objects (docs/CONVENTIONS.md); an array
 * pair is passed through when the port reads tuples positionally.
 */
export function SS(...pairs) {
  return M.StatusSet.Of(...pairs.map(([Kind, Stacks]) => ({ Kind, Stacks })));
}

export function InHand(state, cardId) {
  const card = state.Hand.find((c) => c.Def.Id === cardId);
  assert.ok(card, `${cardId} is not in the hand: ${state.Hand.map((c) => c.Def.Id).join(',')}`);
  return card.InstanceId;
}

export function ofType(events, Type) {
  return events.filter((e) => e instanceof Type);
}

export function single(list, pred = () => true, msg = '') {
  const found = [...list].filter(pred);
  assert.equal(found.length, 1, `expected exactly one${msg ? ' (' + msg + ')' : ''}, got ${found.length}`);
  return found[0];
}

export function last(list) {
  return list[list.length - 1];
}

export function typeNames(events) {
  return events.map((e) => e.constructor.$name ?? e.constructor.name);
}

/** C# record equality (Is.EqualTo on records); shows a JSON diff on failure. */
export function eq(actual, expected, msg) {
  if (csEquals(actual, expected)) return;
  assert.deepStrictEqual(toParityJson(actual), toParityJson(expected), msg);
  assert.fail(`${msg ?? 'not equal'} (same JSON, different record types)`);
}

/** Asserts that `expected` types appear in `actual` in this order (gaps allowed). */
export function assertInOrder(actual, ...expected) {
  let cursor = 0;
  for (const type of expected) {
    let found = -1;
    for (let i = cursor; i < actual.length; i++) {
      if (actual[i].constructor === type) { found = i; break; }
    }
    assert.ok(found >= 0, `${type.name} is missing after index ${cursor} in: ${typeNames(actual).join(', ')}`);
    cursor = found + 1;
  }
}

/** StatusSet.ToString(): "—" for none. */
export function statusText(set) {
  if (typeof set.ToString === 'function') return set.ToString();
  return String(set);
}
