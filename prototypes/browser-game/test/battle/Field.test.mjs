// Tests for src/core/battle/Field.js (port of unity-port/BattleCore/Field.cs).
// Ports FieldTests (N, the inverses, Field.Move, Validate) and MultiEnemyTests' line rows. The C#
// tests reach their states through TurnLoop.Start; the pure Field rows here build the same
// BattleState by hand (player on 2, enemies where the C# fixture puts them). The same rows are
// run once more through TurnLoop at the bottom when that module is available.
import { test as nodeTest } from 'node:test';
import assert from 'node:assert/strict';
import { Constants } from '../../src/core/battle/Constants.js';
import { loadDeps } from './_foundation_deps.mjs';

// Field.js imports Types.js, which imports Statuses.js; skip with a reason while that module is missing.
const { mods, skip: SKIP } = await loadDeps(import.meta.url,
  ['../../src/core/battle/Field.js', '../../src/core/battle/Types.js', '../../src/core/battle/Statuses.js']);
const test = (name, opts, fn) => (fn === undefined ? nodeTest(name, { skip: SKIP }, opts) : nodeTest(name, { skip: SKIP || opts.skip }, fn));
const {
  Field, Shift, Actor, BattleAttribute, BattleState, CombatantState, EnemyActionDef, EnemyDef, EnemyUnit, Face, Reach,
  TargetKind, CardDef, StatusSet,
} = mods ?? {};

// ---- Fixtures (Fixtures.cs) ----

const enemyAction = (id, column = 1, face = null, attributes = BattleAttribute.Attack, trait = null, targets = TargetKind.One) =>
  new EnemyActionDef(id, id, attributes, column, face ?? new Face(), trait, targets, '');

function combatant({ hp = Constants.PlayerMaxHp, stamina = Constants.BaseMaxStamina, guard = 0, cell = Constants.PlayerStartCell, size = 1, statuses = null } = {}) {
  return new CombatantState(hp, hp, stamina, Constants.BaseMaxStamina, guard, cell, size, statuses ?? StatusSet.Empty);
}

function fixtureEnemy(id = 'dummy', size = 1) {
  const atZero = enemyAction('near_hit', 1, new Face({ Power: 5 }));
  const atOneToTwo = enemyAction('mid_hit', 1, new Face({ Power: 4, Reach: new Reach(1, 2) }));
  const atThreePlus = enemyAction('advance', 1, new Face({ Move: 1 }), BattleAttribute.None, null, TargetKind.Self);
  const actions = new Map();
  for (const a of [atZero, atOneToTwo, atThreePlus]) actions.set(a.Id, a);
  return new EnemyDef(id, id, 60, 10, 2, size, [atZero.Id], [atOneToTwo.Id], [atThreePlus.Id], actions);
}

/** A battle state with the player on `playerCell` and the enemies on [{ cell, size, hp }]. */
function line(fieldCells, enemies, playerCell = Constants.PlayerStartCell) {
  const units = enemies.map((e, i) =>
    new EnemyUnit(fixtureEnemy(String.fromCharCode(97 + i), e.size ?? 1), combatant({ hp: e.hp ?? 60, cell: e.cell, size: e.size ?? 1 }), null));
  return new BattleState(1, fieldCells, combatant({ cell: playerCell }), units, [], [], []);
}

const shift = (from, to, blocked) => new Shift(from, to, blocked);

// ---- N and the bands ----

for (const [player, enemy, gap] of [[2, 5, 2], [1, 6, 4], [2, 3, 0]]) {
  test(`TheGap_IsTheEmptyCellsBetween(${player}, ${enemy}, ${gap})`, () => {
    assert.equal(Field.GapBetween(combatant({ cell: player }), combatant({ cell: enemy })), gap);
  });
}

test('ALargeEnemy_IsCountedFromItsNearEdge', () => {
  // §7.2: a dragon on 4〜6 and the player on 2 are at gap 1.
  const dragon = combatant({ hp: 200, cell: 4, size: 3 });
  assert.equal(dragon.FarCell, 6);
  assert.equal(Field.GapBetween(combatant({ cell: 2 }), dragon), 1);
});

for (const [playerCell, playerSize, enemyCell, gap] of [[2, 1, 5, 2], [2, 1, 3, 0], [1, 2, 5, 2], [2, 2, 4, 0], [1, 2, 7, 4]]) {
  test(`Gap_CountsFromThePlayersFrontCell_ForSizeOneAndTwo(${playerCell}, ${playerSize}, ${enemyCell}, ${gap})`, () => {
    assert.equal(Field.Gap(playerCell, playerSize, enemyCell), gap);
    assert.equal(Field.FrontCell(playerCell, playerSize), playerCell + playerSize - 1);
    assert.equal(Field.GapBetween(combatant({ cell: playerCell, size: playerSize }), combatant({ cell: enemyCell })), gap, 'GapBetween reads Field.Gap');
  });
}

for (const [gap, enemyCell] of [[0, 5], [2, 6], [3, 8]]) {
  test(`TheInverses_ReadTheSameNBack(${gap}, ${enemyCell})`, () => {
    const front = Field.FrontCellAt(gap, enemyCell);
    assert.equal(Field.GapFrom(front, enemyCell), gap);
    assert.equal(Field.NearEdgeAt(gap, front), enemyCell);
    assert.equal(Field.Gap(front, 1, enemyCell), gap, 'a size-1 player on the front cell');
    assert.equal(Field.Gap(front - 1, 2, enemyCell), gap, 'a size-2 player whose front is that cell');
  });
}

test('GapBetween refuses null sides like C#', () => {
  assert.throws(() => Field.GapBetween(null, combatant()), (e) => e.name === 'ArgumentNullException' && e.message === "Value cannot be null. (Parameter 'player')");
  assert.throws(() => Field.GapBetween(combatant(), null), (e) => e.name === 'ArgumentNullException' && e.message === "Value cannot be null. (Parameter 'enemy')");
});

// ---- Field.Move ----

test('ThePlayer_StopsOneShortOfTheEnemy_AndAtCellOne', () => {
  const s = line(6, [{ cell: 5 }]); // player 2, enemy 5
  assert.deepEqual(Field.Move(s, Actor.Player, 1), shift(2, 3, 0));
  assert.deepEqual(Field.Move(s, Actor.Player, 2), shift(2, 4, 0));
  assert.deepEqual(Field.Move(s, Actor.Player, 3), shift(2, 4, 1), 'no overtaking (§7.1)');
  assert.deepEqual(Field.Move(s, Actor.Player, -1), shift(2, 1, 0));
  assert.deepEqual(Field.Move(s, Actor.Player, -2), shift(2, 1, 1));
  assert.deepEqual(Field.Move(s, Actor.Player, 0), shift(2, 2, 0));
});

test('TheEnemy_StopsOnePastThePlayer_AndAtTheLinesEnd', () => {
  const s = line(6, [{ cell: 5 }]); // player 2, enemy 5, 6 cells
  assert.deepEqual(Field.Move(s, Actor.Enemy, 1), shift(5, 4, 0));
  assert.deepEqual(Field.Move(s, Actor.Enemy, 2), shift(5, 3, 0));
  assert.deepEqual(Field.Move(s, Actor.Enemy, 3), shift(5, 3, 1));
  assert.deepEqual(Field.Move(s, Actor.Enemy, -1), shift(5, 6, 0));
  assert.deepEqual(Field.Move(s, Actor.Enemy, -2), shift(5, 6, 1));
  assert.deepEqual(Field.Move(s, Actor.Enemy, 0), shift(5, 5, 0));
});

test('ALargeEnemy_KeepsItsWholeBodyOnTheLine', () => {
  // Size 2 on 4〜5 of 6: one cell of room behind, and it is the far edge that hits the end.
  const s = line(6, [{ cell: 4, size: 2 }]);
  assert.deepEqual(Field.Move(s, Actor.Enemy, -2), shift(4, 5, 1));
  assert.deepEqual(Field.Move(s, Actor.Enemy, 1), shift(4, 3, 0));
});

test('with no enemy standing, the player is capped by the line itself', () => {
  const s = line(6, [{ cell: 5, hp: 0 }]);
  assert.deepEqual(Field.Move(s, Actor.Player, 2), shift(2, 4, 0));
  assert.deepEqual(Field.Move(s, Actor.Player, 9), shift(2, 6, 5));
});

test('Move refuses a null state like C#', () => {
  assert.throws(() => Field.Move(null, Actor.Player, 1), (e) => e.name === 'ArgumentNullException' && e.message === "Value cannot be null. (Parameter 'state')");
});

// ---- The line with several enemies (MultiEnemyTests §7.1) ----

test('ThePlayer_StopsShortOfTheNearestEnemy', () => {
  const s = line(8, [{ cell: 6 }, { cell: 4 }]);
  assert.equal(s.Nearest, 1);
  assert.deepEqual(Field.Move(s, Actor.Player, 3), shift(2, 3, 2));
});

test('Enemies_PassEachOther_WhenTheCellTheyLandOnIsFree', () => {
  const s = line(8, [{ cell: 4 }, { cell: 5 }]); // player 2, a 4, b 5
  assert.deepEqual(Field.Move(s, Actor.Enemy, 2, 1), shift(5, 3, 0), 'b steps past a onto 3');
  assert.deepEqual(Field.Move(s, Actor.Enemy, 1, 1), shift(5, 5, 1), "4 is a's: b cannot land there");
  assert.deepEqual(Field.Move(s, Actor.Enemy, -1, 0), shift(4, 4, 1), "5 is b's");
  assert.deepEqual(Field.Move(s, Actor.Enemy, -2, 0), shift(4, 6, 0), 'a steps past b onto 6');
  assert.deepEqual(Field.Move(s, Actor.Enemy, 2, 0), shift(4, 3, 1), 'and nobody passes the player');

  const aDown = s.WithEnemy(0, s.Enemies[0].Body.with({ Hp: 0 }));
  assert.deepEqual(Field.Move(aDown, Actor.Enemy, 1, 1), shift(5, 4, 0), 'a fallen enemy holds no cells');
});

test('ALargeEnemy_KeepsItsCellsToItself', () => {
  const s = line(8, [{ cell: 4 }, { cell: 5, size: 2 }]); // a 4, b 5〜6
  assert.deepEqual(Field.Move(s, Actor.Enemy, -2, 0), shift(4, 4, 2), "5 and 6 are b's");
  assert.deepEqual(Field.Move(s, Actor.Enemy, -3, 0), shift(4, 7, 0), 'but a free cell past it can be landed on');
});

test("OneEnemyFalls: a's cell is free; b still stops the player", () => {
  const s = line(8, [{ cell: 3, hp: 0 }, { cell: 4 }]);
  assert.deepEqual(s.Living, [1]);
  assert.deepEqual(Field.Move(s, Actor.Player, 2), shift(2, 3, 1));
});

// ---- Start cell, wall, validation ----

test('EnemyStartCell: START_GAP cells to the right, or the right end of a short line', () => {
  assert.equal(Field.EnemyStartCell(8, 2, 1, 1, 3), 6);
  assert.equal(Field.EnemyStartCell(6, 2, 1, 1, 3), 6);
  assert.equal(Field.EnemyStartCell(5, 2, 1, 1, 3), 5, 'a 5-cell line shrinks the gap');
  assert.equal(Field.EnemyStartCell(6, 2, 1, 2, 3), 5, 'a large enemy on a narrow line');
  assert.equal(Field.EnemyStartCell(8, 2, 1, 2, 3), 6);
  assert.equal(Field.EnemyStartCell(8, 2, 1, 1, 0), 3);
  assert.throws(() => Field.EnemyStartCell(8, 2, 1, 1, -1), (e) =>
    e.name === 'ArgumentOutOfRangeException' && e.message === "A gap is never negative. (Parameter 'startGap')\nActual value was -1.");
});

test('WallDamage: 3 per blocked cell, never negative', () => {
  assert.equal(Field.WallDamage(0), 0);
  assert.equal(Field.WallDamage(1), 3);
  assert.equal(Field.WallDamage(2), 6);
  assert.equal(Field.WallDamage(-1), 0);
});

test('Validate_RefusesWhatTheLineCannotHold', () => {
  const outOfRange = (e) => e.name === 'ArgumentOutOfRangeException';
  const argument = (e) => e.name === 'ArgumentException';
  assert.doesNotThrow(() => Field.Validate(6, 2, 1, 5, 1));
  assert.throws(() => Field.Validate(4, 2, 1, 3, 1), outOfRange);
  assert.throws(() => Field.Validate(9, 2, 1, 5, 1), outOfRange);
  assert.doesNotThrow(() => Field.Validate(6, 2, 1, 5, 2), '5〜6 fits');
  assert.throws(() => Field.Validate(6, 2, 1, 5, 3), outOfRange, '5〜7 does not');
  assert.throws(() => Field.Validate(6, 2, 1, 5, 4), outOfRange);
  assert.throws(() => Field.Validate(6, 0, 1, 5, 1), outOfRange);
  assert.throws(() => Field.Validate(6, 5, 1, 5, 1), argument);
});

test('Validate: the .NET messages are kept', () => {
  assert.throws(() => Field.Validate(9, 2, 1, 6, 1), (e) => e.message === "The line is 5..8 cells. (Parameter 'fieldCells')\nActual value was 9.");
  assert.throws(() => Field.Validate(6, 2, 1, 2, 1), (e) => e.message === 'The player must stand to the left of every enemy (§7.1).');
  assert.throws(() => Field.Validate(6, 2, 0, 5, 1), (e) => e.message === "At least one cell. (Parameter 'playerSize')\nActual value was 0.");
  assert.throws(() => Field.Validate(6, 2, 1, 6, 2), (e) => e.message === "Enemy 0 does not fit on the line. (Parameter 'enemies')\nActual value was 6.");
  assert.throws(() => Field.Validate(6, 2, 1, 5, 4), (e) => e.message === "An enemy uses 1..3 cells. (Parameter 'enemies')\nActual value was 4.");
  assert.throws(() => Field.Validate(6, 2, 1, null), (e) => e.name === 'ArgumentNullException' && e.message === "Value cannot be null. (Parameter 'enemies')");
});

test('Validate with several enemies: CELL_CAPACITY 1 and ENEMIES_MAX 3 (MultiEnemyTests)', () => {
  assert.throws(() => Field.Validate(8, 2, 1, [{ Cell: 4, Size: 2 }, { Cell: 5, Size: 1 }]), (e) =>
    e.name === 'ArgumentException' && e.message === 'Enemies 0 and 1 share a cell; a cell holds one enemy (§7.1).',
  'CELL_CAPACITY 1: no two enemies on one cell');
  assert.doesNotThrow(() => Field.Validate(8, 2, 1, [{ Cell: 4, Size: 1 }, { Cell: 5, Size: 1 }, { Cell: 6, Size: 2 }]));
  assert.throws(() => Field.Validate(8, 2, 1, [{ Cell: 4, Size: 1 }, { Cell: 5, Size: 1 }, { Cell: 6, Size: 1 }, { Cell: 7, Size: 1 }]), (e) =>
    e.name === 'ArgumentOutOfRangeException' && e.message === "A battle has 1..3 enemies (§7.4). (Parameter 'enemies')\nActual value was 4.");
  assert.throws(() => Field.Validate(8, 2, 1, []), (e) => e.name === 'ArgumentOutOfRangeException');
  // Unnamed tuples ({ Item1, Item2 }) and arrays are read the same way.
  assert.throws(() => Field.Validate(8, 2, 1, [{ Item1: 4, Item2: 2 }, { Item1: 5, Item2: 1 }]), (e) => e.name === 'ArgumentException');
  assert.throws(() => Field.Validate(8, 2, 1, [[4, 2], [5, 1]]), (e) => e.name === 'ArgumentException');
});

// ---- The same Field.Move rows through TurnLoop (FieldTests.Battle), when it is there ----

let turnLoop = null;
try {
  const tl = await import('../../src/core/battle/TurnLoop.js');
  const cards = await import('../../src/core/battle/Cards.js');
  const irng = await import('../../src/core/battle/IRng.js');
  if (tl.TurnLoop && tl.BattleSetup && cards.Cards) turnLoop = { ...tl, Cards: cards.Cards, FixedRng: irng.FixedRng };
} catch { /* TurnLoop.js / Cards.js not ported yet */ }

function battle(enemy, enemyCell, fieldCells = 6) {
  const { TurnLoop, BattleSetup, Cards, FixedRng } = turnLoop;
  const noRng = new FixedRng(0.9999999);
  const filler = new CardDef('filler', 'filler', BattleAttribute.Attack, 1, new Face(), null, TargetKind.One, '');
  const deck = Cards.BuildDeck([filler], 1);
  const setup = new BattleSetup(enemy, deck, fieldCells, { StartGap: enemyCell - Constants.PlayerStartCell - 1 });
  assert.equal(setup.EnemyStartCell, enemyCell, 'the fixture asks for a cell the line holds');
  const s = TurnLoop.Start(setup, noRng).State;
  return TurnLoop.BeginPlayerTurn(s, noRng).State;
}

test('through TurnLoop: the player and the enemy stop where the hand-built line says', { skip: turnLoop ? false : 'needs TurnLoop.js + Cards.js' }, () => {
  const s = battle(fixtureEnemy(), 5);
  assert.deepEqual(Field.Move(s, Actor.Player, 3), shift(2, 4, 1));
  assert.deepEqual(Field.Move(s, Actor.Enemy, 3), shift(5, 3, 1));
  const large = battle(fixtureEnemy('dummy', 2), 4);
  assert.deepEqual(Field.Move(large, Actor.Enemy, -2), shift(4, 5, 1));
});

// ---- Differential rows written by the C# code under dotnet 10 (random lines of 1..3 enemies) ----

import { readFileSync } from 'node:fs';
const FIXTURE = JSON.parse(readFileSync(new URL('./Field.fixture.json', import.meta.url), 'utf8'));

test('Field.Move, Nearest and Living: 1500 random lines match C#', () => {
  const def = fixtureEnemy();
  for (const [fieldCells, playerCell, playerSize, enemies, who, unit, cells, expected, nearest, living] of FIXTURE.moves) {
    const s = new BattleState(1, fieldCells, combatant({ cell: playerCell, size: playerSize }),
      enemies.map(([cell, size, hp]) => new EnemyUnit(def, combatant({ hp, cell, size }), null)), [], [], []);
    const label = JSON.stringify([fieldCells, playerCell, playerSize, enemies, who, unit, cells]);
    assert.deepEqual(Field.Move(s, who, cells, unit), shift(...expected), label);
    assert.equal(s.Nearest, nearest, label);
    assert.deepEqual(s.Living, living, label);
  }
});

test('Field.Validate: 800 random setups throw (or not) exactly as C#, same type and message', () => {
  for (const [fieldCells, playerCell, playerSize, list, expected] of FIXTURE.validate) {
    let got = 'ok';
    try {
      Field.Validate(fieldCells, playerCell, playerSize, list.map(([Cell, Size]) => ({ Cell, Size })));
    } catch (e) {
      got = `${e.name}|${e.message.replace(/\n/g, '\\n')}`;
    }
    assert.equal(got, expected, JSON.stringify([fieldCells, playerCell, playerSize, list]));
  }
});

test('Field.EnemyStartCell: 500 random rows match C#', () => {
  for (const [fieldCells, playerCell, playerSize, enemySize, startGap, expected] of FIXTURE.start) {
    let got;
    try { got = String(Field.EnemyStartCell(fieldCells, playerCell, playerSize, enemySize, startGap)); } catch (e) { got = e.name; }
    assert.equal(got, expected);
  }
});
