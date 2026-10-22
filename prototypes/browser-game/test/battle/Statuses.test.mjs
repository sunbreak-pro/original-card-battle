// Tests for src/core/battle/Statuses.js (port of unity-port/BattleCore/Statuses.cs).
// Cases ported from BattleCore.Tests/StatusTests.cs and BossStatusTests.cs, plus the C# dump
// (BattleCards.fixture.json) for the tables, hashes and texts.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { csEquals, enumValues } from '../../src/shared/cs.js';
import { StatusSet, Statuses } from '../../src/core/battle/Statuses.js';
import { StatusKind, StatusDecay, CombatantState, PlayerTurnEnd, EnumTokens } from '../../src/core/battle/Types.js';
import { Constants } from '../../src/core/battle/Constants.js';
import { Combat } from '../../src/core/battle/Combat.js';
import { fixture, errorOf } from './_battle_cards.mjs';

/** Fixtures.Combatant(hp, stamina, guard, cell, size, statuses). */
function Combatant({ hp = Constants.PlayerMaxHp, stamina = Constants.BaseMaxStamina, guard = 0, cell = Constants.PlayerStartCell, size = 1, statuses = null } = {}) {
  return new CombatantState(hp, hp, stamina, Constants.BaseMaxStamina, guard, cell, size, statuses ?? StatusSet.Empty);
}

const turnDecayKinds = () => enumValues(StatusKind).filter((k) => Statuses.DecayOf(k) === StatusDecay.OnTurn);
const useDecayKinds = () => enumValues(StatusKind).filter((k) => Statuses.DecayOf(k) === StatusDecay.OnUse);

// ---- StatusTests ----

test('AnEmptySet_HoldsNothing', () => {
  assert.equal(StatusSet.Empty.KindCount, 0);
  assert.equal(StatusSet.Empty.Has(StatusKind.Slow), false);
  assert.equal(StatusSet.Empty.Stacks(StatusKind.Slow), 0);
});

test('ApplyingAStatus_GivesTwoStacksByDefault', () => {
  assert.equal(StatusSet.Empty.Add(StatusKind.Slow).Stacks(StatusKind.Slow), Constants.StatusApplyDefault);
});

test('ApplyingTwice_Stacks', () => {
  assert.equal(StatusSet.Empty.Add(StatusKind.Slow, 2).Add(StatusKind.Slow, 1).Stacks(StatusKind.Slow), 3);
});

test('SlowIsTheTurnDecayType', () => {
  assert.equal(Statuses.DecayOf(StatusKind.Slow), StatusDecay.OnTurn);
});

test('TurnStart_TakesOneStackOffTheTurnDecayType', () => {
  assert.equal(StatusSet.Empty.Add(StatusKind.Slow, 2).TickTurnStart().Stacks(StatusKind.Slow), 1);
});

test('TheLastStackDropsTheWordEntirely', () => {
  const set = StatusSet.Empty.Add(StatusKind.Slow, 1).TickTurnStart();
  assert.equal(set.Has(StatusKind.Slow), false);
  assert.equal(set.KindCount, 0);
});

test('SlowTakesOneCellOffEveryMove_ForAsLongAsItIsHeld', () => {
  const slowed = StatusSet.Empty.Add(StatusKind.Slow, 2);
  assert.equal(Statuses.MoveReduction(slowed), 1);
  assert.equal(Combat.CellsAfterSlow(1, slowed), 0, 'a one-cell move still stops dead');
  assert.equal(Combat.CellsAfterSlow(2, slowed), 1, 'a two-cell move becomes one');
  const afterOneTurn = slowed.TickTurnStart();
  assert.equal(Statuses.MoveReduction(afterOneTurn), 1, 'one stack is still one stack');
  const afterTwoTurns = afterOneTurn.TickTurnStart();
  assert.equal(Statuses.MoveReduction(afterTwoTurns), 0);
  assert.equal(Combat.CellsAfterSlow(1, afterTwoTurns), 1);
});

test('APlayerHoldsSixKindsAtMost', () => {
  assert.equal(Constants.StatusKindsPlayer, 6);
  const atLimit = StatusSet.Empty.Add(StatusKind.Slow, 2, 1);
  const stillAtLimit = atLimit.Add(StatusKind.Slow, 2, 1);
  assert.equal(atLimit.KindCount, 1);
  assert.equal(stillAtLimit.Stacks(StatusKind.Slow), 4, 'an existing word keeps stacking');
  // A new word at the limit is refused and the very same set comes back.
  assert.equal(atLimit.Add(StatusKind.Bleed, 2, 1), atLimit);
  // C# named arguments as a trailing object.
  assert.equal(atLimit.Add(StatusKind.Bleed, 2, { kindLimit: 1 }), atLimit);
  assert.equal(StatusSet.Empty.Add(StatusKind.Bleed, { kindLimit: 0 }), StatusSet.Empty);
  assert.equal(StatusSet.Empty.Add(StatusKind.Bleed, { kindLimit: 1 }).Stacks(StatusKind.Bleed), Constants.StatusApplyDefault);
  assert.equal(StatusSet.Empty.Add(StatusKind.Bleed, 3, null).Stacks(StatusKind.Bleed), 3, 'null is no limit');
});

test('AddingZeroOrLess_ChangesNothing', () => {
  assert.equal(StatusSet.Empty.Add(StatusKind.Slow, 0).KindCount, 0);
  assert.equal(StatusSet.Empty.Add(StatusKind.Slow, -2), StatusSet.Empty);
});

test('SetsWithTheSameStacks_AreEqual', () => {
  const a = StatusSet.Empty.Add(StatusKind.Slow, 2);
  const b = StatusSet.Of({ Kind: StatusKind.Slow, Stacks: 2 });
  assert.ok(a.Equals(b));
  assert.ok(a.equals(b));
  assert.ok(csEquals(a, b), 'record equality (cs.js) agrees');
  assert.equal(a.GetHashCode(), b.GetHashCode());
  assert.ok(!a.Equals(StatusSet.Empty));
  assert.ok(!csEquals(a, StatusSet.Empty));
  assert.ok(!a.Equals(null));
  // Inside a record (CombatantState equality is structural, as C# record equality calls StatusSet.Equals).
  assert.ok(Combatant({ statuses: a }).equals(Combatant({ statuses: b })));
  assert.ok(!Combatant({ statuses: a }).equals(Combatant()));
});

test('AddingDoesNotDisturbTheSetItCameFrom', () => {
  const before = StatusSet.Empty.Add(StatusKind.Slow, 2);
  const after = before.Add(StatusKind.Slow, 2);
  assert.equal(before.Stacks(StatusKind.Slow), 2);
  assert.equal(after.Stacks(StatusKind.Slow), 4);
});

test('SlowedCombatantsStateCarriesTheWord', () => {
  const player = Combatant({ cell: 2, statuses: StatusSet.Empty.Add(StatusKind.Slow) });
  assert.equal(player.Statuses.Has(StatusKind.Slow), true);
  assert.equal(Statuses.MoveReduction(player.Statuses), 1);
  assert.equal(player.Cell, 2, 'still where it stood');
});

test('SlowOnTheEnemy_ShortensItsPushAndItsStep', () => {
  const polearm = Combatant({ hp: 60, cell: 5, statuses: StatusSet.Empty.Add(StatusKind.Slow) });
  assert.equal(polearm.Statuses.Has(StatusKind.Slow), true, 'the chip still shows');
  assert.equal(Combat.CellsAfterSlow(2, polearm.Statuses), 1, 'the two-cell shove pushes one');
  assert.equal(Combat.CellsAfterSlow(1, polearm.Statuses), 0, 'the one-cell step is stopped');
});

test('ATurnDecayWord_StopsAtTheCap_AndTheRestIsDropped (every turn-decay kind)', () => {
  assert.deepEqual(turnDecayKinds(), ['Slow', 'Swift', 'Bleed', 'Fatigue', 'Regen', 'Binding']);
  for (const kind of turnDecayKinds()) {
    assert.equal(StatusSet.Empty.Add(kind, 3).Add(kind, 3).Stacks(kind), Constants.TurnDecayStackMax, kind);
  }
});

test('AUseDecayWord_IsNotCapped (every use-decay kind)', () => {
  assert.deepEqual(useDecayKinds(), ['Fragile', 'Intimidate', 'Empower', 'Focus', 'Parry', 'Hook']);
  for (const kind of useDecayKinds()) {
    assert.equal(StatusSet.Empty.Add(kind, 3).Add(kind, 3).Stacks(kind), 6, kind);
  }
});

test('AFreshHolder_TakesColumnFoursBleedWhole', () => {
  assert.equal(StatusSet.Empty.Add(StatusKind.Bleed, 4).Stacks(StatusKind.Bleed), 4);
});

test('AWordAtTheCap_ComesBackUnchanged', () => {
  const full = StatusSet.Of({ Kind: StatusKind.Regen, Stacks: Constants.TurnDecayStackMax });
  assert.equal(full.Add(StatusKind.Regen, 2), full);
});

test('Of_HoldsATurnDecayWordToTheCap_ButNotAUseDecayWord', () => {
  assert.equal(StatusSet.Of({ Kind: StatusKind.Regen, Stacks: 9 }).Stacks(StatusKind.Regen), Constants.TurnDecayStackMax);
  assert.equal(StatusSet.Of({ Kind: StatusKind.Empower, Stacks: 9 }).Stacks(StatusKind.Empower), 9);
});

test('ToppingUpTwiceEveryTurn_NeverClimbsPastTheCap (every turn-decay kind)', () => {
  for (const kind of turnDecayKinds()) {
    let set = StatusSet.Empty;
    for (let turn = 0; turn < 100; turn++) {
      set = set.Add(kind, 2).Add(kind, 2);
      assert.equal(set.Stacks(kind), Constants.TurnDecayStackMax, kind);
      set = set.TickTurnStart();
      assert.equal(set.Stacks(kind), Constants.TurnDecayStackMax - 1, kind);
    }
  }
});

// ---- BossStatusTests ----

test('EachBossWord_HasItsDecay_ItsSide_AndItsName', () => {
  const rows = [
    [StatusKind.MiasmaShroud, StatusDecay.Lasting, 'miasma_shroud', '瘴気纏い'],
    [StatusKind.Binding, StatusDecay.OnTurn, 'binding', '呪縛'],
    [StatusKind.Hook, StatusDecay.OnUse, 'hook', '鉤爪'],
    [StatusKind.Depths, StatusDecay.Lasting, 'depths', '深み'],
    [StatusKind.Rooting, StatusDecay.Lasting, 'rooting', '根張り'],
    [StatusKind.Withering, StatusDecay.Lasting, 'withering', '枯らし'],
  ];
  for (const [kind, decay, token, label] of rows) {
    assert.equal(Statuses.DecayOf(kind), decay, kind);
    assert.equal(Statuses.IsOwn(kind), false, '相手に');
    assert.equal(Statuses.IsBossOnly(kind), true);
    assert.equal(EnumTokens.ToToken(kind), token);
    assert.equal(EnumTokens.ToLabel(kind), label);
  }
});

test('TheLastingWords_SurviveTheTurnStart', () => {
  const set = StatusSet.Of(
    { Kind: StatusKind.MiasmaShroud, Stacks: 1 }, { Kind: StatusKind.Depths, Stacks: 2 },
    { Kind: StatusKind.Rooting, Stacks: 1 }, { Kind: StatusKind.Withering, Stacks: 2 }, { Kind: StatusKind.Binding, Stacks: 2 });
  const ticked = set.TickTurnStart();
  assert.equal(ticked.Stacks(StatusKind.MiasmaShroud), 1);
  assert.equal(ticked.Stacks(StatusKind.Depths), 2);
  assert.equal(ticked.Stacks(StatusKind.Rooting), 1);
  assert.equal(ticked.Stacks(StatusKind.Withering), 2);
  assert.equal(ticked.Stacks(StatusKind.Binding), 1, '呪縛 is a turn-decay word');
});

test('Withering_TakesNoMoreThan_LeavesOne', () => {
  for (const [stacks, recoveryBefore, loss] of [[2, 3, 2], [2, 2, 1], [2, 1, 0], [2, 0, 0], [1, -1, 0]]) {
    assert.equal(Statuses.RecoveryLoss(StatusSet.Of({ Kind: StatusKind.Withering, Stacks: stacks }), false, recoveryBefore), loss);
  }
});

// ---- The C# dump ----

test('C#: decay, side and boss-only for every word, and the constants', () => {
  assert.deepEqual(enumValues(StatusKind).map((k) => [k, Statuses.DecayOf(k), Statuses.IsOwn(k), Statuses.IsBossOnly(k)]), fixture.StatusKinds);
  assert.deepEqual([Statuses.RootingHpPerStack, Statuses.BossWordStackMax, Statuses.WitheringRecoveryFloor], fixture.StatusConsts);
});

test('C#: Of / Add / TickTurnStart / Consume give the same kinds, stacks, hash and text', () => {
  const S = StatusKind;
  const set = (s) => ({ Kinds: s.Kinds, Stacks: s.Kinds.map((k) => s.Stacks(k)), Hash: s.GetHashCode(), Text: s.ToString(), KindCount: s.KindCount });
  const s1 = StatusSet.Of([S.Regen, 9], [S.Slow, 2], [S.Empower, 9], [S.Withering, 2], [S.Bleed, 0]);
  const got = {
    of: set(s1),
    tick: set(s1.TickTurnStart()),
    tick2: set(s1.TickTurnStart().TickTurnStart()),
    consume: set(s1.Consume(S.Empower)),
    consumeAbsent: set(s1.Consume(S.Focus)),
    add: set(s1.Add(S.Focus).Add(S.Parry, 1).Add(S.Regen, 3).Add(S.Hook, 5)),
    limit: set(s1.Add(S.Focus, 2, 4)),
    limitOpen: set(s1.Add(S.Focus, 2, 5)),
    limitExisting: set(s1.Add(S.Empower, 2, 1)),
    empty: set(StatusSet.Empty),
    all: set(StatusSet.Of(enumValues(S).map((k) => ({ Kind: k, Stacks: 3 })))),
    consumeLast: set(StatusSet.Of([S.Parry, 1]).Consume(S.Parry)),
  };
  assert.deepEqual(got, fixture.Sets);
  assert.equal(s1.Consume(S.Focus), s1, 'consuming a word not held hands back the same set');
});

test('C#: RecoveryLoss over 枯らし × 深み × adjacent × recovery', () => {
  for (const [w, dp, adj, rb, loss] of fixture.RecoveryLoss) {
    const s = StatusSet.Of({ Kind: StatusKind.Withering, Stacks: w }, { Kind: StatusKind.Depths, Stacks: dp });
    assert.equal(Statuses.RecoveryLoss(s, adj, rb), loss, JSON.stringify([w, dp, adj, rb]));
  }
});

test('C#: StayedTwoTurns', () => {
  const ends = {
    none: [],
    one: [new PlayerTurnEnd(0, 2, false, 3)],
    same: [new PlayerTurnEnd(0, 2, false, 3), new PlayerTurnEnd(0, 2, false, 3)],
    moved: [new PlayerTurnEnd(0, 2, false, 3), new PlayerTurnEnd(0, 2, true, 3)],
    other: [new PlayerTurnEnd(0, 2, false, 3), new PlayerTurnEnd(0, 2, false, 4)],
    zero: [new PlayerTurnEnd(0, 2, false, 0), new PlayerTurnEnd(0, 2, false, 0)],
    three: [new PlayerTurnEnd(0, 2, false, 5), new PlayerTurnEnd(0, 2, false, 3), new PlayerTurnEnd(0, 2, false, 3)],
  };
  assert.deepEqual(Object.entries(ends).map(([name, list]) => [name, Statuses.StayedTwoTurns(list)]), fixture.Stayed);
});

test('C#: exceptions', () => {
  assert.deepEqual(errorOf(() => Statuses.DecayOf(99)), fixture.Errors.DecayOf99);
  assert.deepEqual(errorOf(() => Statuses.MoveReduction(null)), fixture.Errors.MoveReductionNull);
  assert.deepEqual(errorOf(() => Statuses.RecoveryLoss(null, false, 1)), fixture.Errors.MoveReductionNull);
});

// ---- JS shape (not in the C#) ----

test('Of accepts each tuple spelling and a whole params array', () => {
  const expected = StatusSet.Of({ Kind: StatusKind.Bleed, Stacks: 2 }, { Kind: StatusKind.Focus, Stacks: 1 });
  assert.ok(StatusSet.Of([StatusKind.Bleed, 2], [StatusKind.Focus, 1]).Equals(expected));
  assert.ok(StatusSet.Of({ Item1: StatusKind.Bleed, Item2: 2 }, { Item1: StatusKind.Focus, Item2: 1 }).Equals(expected));
  assert.ok(StatusSet.Of([[StatusKind.Bleed, 2], [StatusKind.Focus, 1]]).Equals(expected));
  assert.ok(StatusSet.Of([{ Kind: StatusKind.Bleed, Stacks: 2 }, { Kind: StatusKind.Focus, Stacks: 1 }]).Equals(expected));
  assert.equal(StatusSet.Of().KindCount, 0);
  assert.equal(StatusSet.Of([]).KindCount, 0);
  assert.equal(StatusSet.Of([StatusKind.Regen, 3]).Stacks(StatusKind.Regen), 3, 'one tuple as [kind, n]');
});

test('toJSON is the parity shape, StatusSet.Empty prints the em dash', () => {
  const s = StatusSet.Of([StatusKind.Regen, 3], [StatusKind.Slow, 1]);
  assert.deepEqual(JSON.parse(JSON.stringify(s)), { $type: 'StatusSet', Stacks: { Slow: 1, Regen: 3 } });
  assert.equal(String(StatusSet.Empty), '—');
  assert.equal(`${s}`, '鈍足 1 / 再生 3');
  assert.equal(StatusSet.$name, 'StatusSet');
});
