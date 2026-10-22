// Tests for src/core/battle/Traits.js (port of unity-port/BattleCore/Traits.cs).
// Cases ported from BattleCore.Tests/TraitTests.cs and the §2.3 sections of VocabularyTests.cs, plus
// the C# dump (BattleCards.fixture.json): every condition × five boards, every effect.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { enumValues, INT_MAX } from '../../src/shared/cs.js';
import { Traits, TraitContext, TraitOutcome } from '../../src/core/battle/Traits.js';
import {
  BattleAttribute, OmenKind, StatusGrant, StatusKind, Trait, TraitCondition, TraitEffect,
} from '../../src/core/battle/Types.js';
import { StatusSet } from '../../src/core/battle/Statuses.js';
import { Constants } from '../../src/core/battle/Constants.js';
import { Combat } from '../../src/core/battle/Combat.js';
import { CardCatalog } from '../../src/core/battle/CardCatalog.js';
import { fixture, errorOf } from './_battle_cards.mjs';

const A = BattleAttribute;
const set = (kind, n) => StatusSet.Of({ Kind: kind, Stacks: n });

// ---- TraitTests ----

const SweepTrait = new Trait(TraitCondition.GapAtLeast, TraitEffect.PowerBonus, { Amount: 3, Threshold: 2 });
const ShoveTrait = new Trait(TraitCondition.Unguarded, TraitEffect.PowerBonus, { Amount: 3 });
const GuardUpTrait = new Trait(TraitCondition.Reserve, TraitEffect.NextTurnRecovery, { Amount: 1, Threshold: 4 });

test('TraitContext defaults are the C#\'s', () => {
  const c = new TraitContext();
  assert.deepEqual(
    [c.Gap, c.OpponentGuard, c.StaminaAfterUse, c.StaminaBefore, c.OpponentStamina, c.Played, c.HandAfterPlay, c.OpponentOmen, c.SelfStatuses, c.OpponentStatuses, c.Attributes, c.Moved],
    [0, 0, 0, INT_MAX, INT_MAX, null, INT_MAX, null, null, null, A.None, false]);
  const o = new TraitOutcome(false);
  assert.deepEqual(
    [o.Triggered, o.PowerBonus, o.GuardBonus, o.NextTurnRecoveryBonus, o.StaminaGain, o.Draw, o.Grants, o.CostDown, o.Convert, o.FollowUp, o.BreakBonus],
    [false, 0, 0, 0, 0, 0, null, 0, false, 0, 0]);
  assert.deepEqual(o.GrantList, []);
  assert.ok(Object.isFrozen(o.GrantList), 'Array.Empty is shared and frozen');
});

test('NoTrait_ContributesNothing', () => {
  const outcome = Traits.Evaluate(null, new TraitContext());
  assert.equal(outcome.Triggered, false);
  assert.equal(outcome.PowerBonus, 0);
  assert.equal(outcome, TraitOutcome.None);
});

test('GapAtLeast_FiresFromTheThresholdUp', () => {
  assert.equal(Traits.Evaluate(SweepTrait, new TraitContext({ Gap: 2 })).PowerBonus, 3);
  assert.equal(Traits.Evaluate(SweepTrait, new TraitContext({ Gap: 4 })).Triggered, true);
  assert.equal(Traits.Evaluate(SweepTrait, new TraitContext({ Gap: 1 })).Triggered, false);
  assert.equal(Traits.Evaluate(SweepTrait, new TraitContext({ Gap: 0 })).Triggered, false);
});

test('GapAtMost_FiresFromTheThresholdDown', () => {
  const trait = new Trait(TraitCondition.GapAtMost, TraitEffect.GuardBonus, { Amount: 3, Threshold: 0 });
  assert.equal(Traits.Evaluate(trait, new TraitContext({ Gap: 0 })).GuardBonus, 3);
  assert.equal(Traits.Evaluate(trait, new TraitContext({ Gap: 1 })).Triggered, false);
  assert.equal(Traits.Evaluate(trait.with({ Threshold: 1 }), new TraitContext({ Gap: 1 })).Triggered, true);
});

test('Unguarded_FiresOnlyWhileTheOpponentGuardIsZero', () => {
  assert.equal(Traits.Evaluate(ShoveTrait, new TraitContext({ OpponentGuard: 0 })).PowerBonus, 3);
  assert.equal(Traits.Evaluate(ShoveTrait, new TraitContext({ OpponentGuard: 1 })).Triggered, false);
});

test('Reserve_ReadsTheStaminaLeftAfterTheCostIsPaid', () => {
  assert.equal(Traits.Evaluate(GuardUpTrait, new TraitContext({ StaminaAfterUse: 4 })).NextTurnRecoveryBonus, 1);
  assert.equal(Traits.Evaluate(GuardUpTrait, new TraitContext({ StaminaAfterUse: 3 })).Triggered, false);
});

test('SweepHitsElevenAtGapTwoAndEightAtGapOne', () => {
  const far = Combat.ComputeRawPower(8, Traits.Evaluate(SweepTrait, new TraitContext({ Gap: 2 })).PowerBonus);
  const near = Combat.ComputeRawPower(8, Traits.Evaluate(SweepTrait, new TraitContext({ Gap: 1 })).PowerBonus);
  assert.equal(far, 11);
  assert.equal(near, 8);
});

test('ShoveHitsEightThroughNoGuardAndFiveThroughSome', () => {
  const bare = Combat.ComputeRawPower(5, Traits.Evaluate(ShoveTrait, new TraitContext({ OpponentGuard: 0 })).PowerBonus);
  const guarded = Combat.ComputeRawPower(5, Traits.Evaluate(ShoveTrait, new TraitContext({ OpponentGuard: 4 })).PowerBonus);
  assert.equal(bare, 8);
  assert.equal(guarded, 5);
  assert.equal(Combat.ApplyGuard(guarded, 4).Damage, 1);
});

// ---- VocabularyTests: §2.3 conditions at their edges ----

test('TraitCondition_HoldsAndFailsAtItsEdge (VocabularyTests.ConditionEdges)', () => {
  const none = [];
  const combo = new Trait(TraitCondition.Combo, TraitEffect.PowerBonus, 1, { Attribute: A.Skill });
  const moved = new Trait(TraitCondition.Moved, TraitEffect.PowerBonus, 1);
  const omen = new Trait(TraitCondition.OmenIs, TraitEffect.PowerBonus, 1, { Omen: OmenKind.Attack });
  const desperate = new Trait(TraitCondition.Desperate, TraitEffect.PowerBonus, 1);
  const first = new Trait(TraitCondition.FirstPlay, TraitEffect.PowerBonus, 1);
  const finisher = new Trait(TraitCondition.Finisher, TraitEffect.PowerBonus, 1);
  const broken = new Trait(TraitCondition.Broken, TraitEffect.PowerBonus, 1);
  const chain = new Trait(TraitCondition.Chain, TraitEffect.PowerBonus, 1);
  const thin = new Trait(TraitCondition.Thin, TraitEffect.Draw, 1);
  const foeHas = new Trait(TraitCondition.FoeHas, TraitEffect.PowerBonus, 1, { Watch: StatusKind.Bleed });
  const selfHas = new Trait(TraitCondition.SelfHas, TraitEffect.GuardBonus, 3, { Watch: StatusKind.Parry });
  const edges = [
    ['Combo_Holds_AfterACardCountedAsTheAttribute', combo, { Played: [A.Attack, A.Skill] }, true],
    ['Combo_Fails_WhenOnlyOtherAttributesWerePlayed', combo, { Played: [A.Attack, A.Guard] }, false],
    ['Combo_Fails_WithNothingPlayed', combo, { Played: none }, false],
    ['Moved_Holds_WhenTheHoldersCellChangedThisTurn', moved, { Moved: true }, true],
    ['Moved_Fails_WhenItDidNot', moved, { Moved: false }, false],
    ['Moved_Fails_EvenAfterAnAttackWasPlayed', moved, { Played: [A.Attack] }, false],
    ['OmenIs_Holds_OnTheNamedKind', omen, { OpponentOmen: OmenKind.Attack }, true],
    ['OmenIs_Fails_OnAnotherKind', omen, { OpponentOmen: OmenKind.Guard }, false],
    ['OmenIs_Fails_WithNoOmen', omen, { OpponentOmen: null }, false],
    ['Desperate_Holds_AtTwoStaminaBeforePaying', desperate, { StaminaBefore: 2 }, true],
    ['Desperate_Fails_AtThree', desperate, { StaminaBefore: 3 }, false],
    ['FirstPlay_Holds_WithNothingPlayed', first, { Played: none }, true],
    ['FirstPlay_Fails_AfterOnePlay', first, { Played: [A.Skill] }, false],
    ['Finisher_Holds_OnTheThirdPlay', finisher, { Played: [A.Attack, A.Guard] }, true],
    ['Finisher_Fails_OnTheSecondPlay', finisher, { Played: [A.Attack] }, false],
    ['Broken_Holds_BelowThreeOpponentStamina', broken, { OpponentStamina: 2 }, true],
    ['Broken_Fails_AtThree', broken, { OpponentStamina: 3 }, false],
    ['Chain_Holds_WhenThePreviousPlaySharesAnAttribute', chain, { Played: [A.Guard, A.Attack], Attributes: A.Attack }, true],
    ['Chain_ReadsOnlyThePreviousPlay', chain, { Played: [A.Attack, A.Guard], Attributes: A.Attack }, false],
    ['Chain_Fails_OnTheFirstPlay', chain, { Played: none, Attributes: A.Attack }, false],
    ['Thin_Holds_WithTwoCardsLeftAfterThePlay', thin, { HandAfterPlay: 2 }, true],
    ['Thin_Fails_WithThree', thin, { HandAfterPlay: 3 }, false],
    ['FoeHas_Holds_WhileTheOpponentHoldsTheWord', foeHas, { OpponentStatuses: set(StatusKind.Bleed, 1) }, true],
    ['FoeHas_Fails_OnAnotherWord', foeHas, { OpponentStatuses: set(StatusKind.Slow, 2) }, false],
    ['FoeHas_IgnoresTheOnePlayingsOwnWords', foeHas, { SelfStatuses: set(StatusKind.Bleed, 1) }, false],
    ['SelfHas_Holds_WhileTheOnePlayingHoldsTheWord', selfHas, { SelfStatuses: set(StatusKind.Parry, 1) }, true],
    ['SelfHas_Fails_OnAnotherWord', selfHas, { SelfStatuses: set(StatusKind.Empower, 2) }, false],
    ['SelfHas_IgnoresTheOpponentsWords', selfHas, { OpponentStatuses: set(StatusKind.Parry, 1) }, false],
  ];
  for (const [name, trait, named, holds] of edges) {
    const context = new TraitContext(named);
    assert.equal(Traits.Holds(trait, context), holds, name);
    assert.equal(Traits.Evaluate(trait, context).Triggered, holds, name);
  }
});

test('EachEffect_FillsItsOwnOutcomeField', () => {
  const holds = new TraitContext({ Played: [] });
  const grant = new StatusGrant(StatusKind.Fragile, 2);
  const On = (effect, amount = 0, g = null) => Traits.Evaluate(new Trait(TraitCondition.FirstPlay, effect, amount, { Grant: g }), holds);
  assert.ok(On(TraitEffect.StaminaGain, 1).equals(new TraitOutcome(true, { StaminaGain: 1 })));
  assert.ok(On(TraitEffect.Draw, 1).equals(new TraitOutcome(true, { Draw: 1 })));
  assert.equal(On(TraitEffect.Status, 0, grant).Triggered, true);
  assert.deepEqual(On(TraitEffect.Status, 0, grant).GrantList, [grant]);
  assert.equal(On(TraitEffect.Status, 0, grant).GrantList[0], grant, 'the grant itself, not a copy');
  assert.ok(On(TraitEffect.CostDown, 1).equals(new TraitOutcome(true, { CostDown: 1 })));
  assert.ok(On(TraitEffect.Convert).equals(new TraitOutcome(true, { Convert: true })));
  assert.ok(On(TraitEffect.FollowUp, Constants.FollowUpPower).equals(new TraitOutcome(true, { FollowUp: 5 })));
  assert.ok(On(TraitEffect.BreakBonus, 1).equals(new TraitOutcome(true, { BreakBonus: 1 })));
  const unmet = new TraitContext({ Played: [A.Attack] });
  assert.equal(Traits.Evaluate(new Trait(TraitCondition.FirstPlay, TraitEffect.Draw, 1), unmet), TraitOutcome.None);
});

test('TwoOutcomes_AddUp_FieldByField', () => {
  const bleed = new StatusGrant(StatusKind.Bleed, 1);
  const regen = new StatusGrant(StatusKind.Regen, 1, { OnSelf: true });
  const a = new TraitOutcome(true, { PowerBonus: 5, StaminaGain: 1, Grants: [bleed], FollowUp: 5 });
  const b = new TraitOutcome(false, {
    PowerBonus: 3, GuardBonus: 2, NextTurnRecoveryBonus: -1, Draw: 1, Grants: [regen], CostDown: 1, Convert: true, BreakBonus: 1,
  });
  const sum = a.Plus(b);
  assert.equal(sum.Triggered, true);
  assert.equal(sum.PowerBonus, 8);
  assert.equal(sum.GuardBonus, 2);
  assert.equal(sum.NextTurnRecoveryBonus, -1);
  assert.equal(sum.StaminaGain, 1);
  assert.equal(sum.Draw, 1);
  assert.deepEqual(sum.GrantList, [bleed, regen]);
  assert.equal(sum.CostDown, 1);
  assert.equal(sum.Convert, true);
  assert.equal(sum.FollowUp, 5);
  assert.equal(sum.BreakBonus, 1);
  assert.ok(TraitOutcome.None.Plus(TraitOutcome.None).equals(TraitOutcome.None));
  assert.equal(TraitOutcome.None.Plus(TraitOutcome.None).Grants, null, 'no grants stay null, not an empty list');
});

test('LastStand_JudgesBothTraits_AndAddsThem', () => {
  for (const [gap, staminaBefore, bonus] of [[2, 2, 8], [2, 3, 5], [1, 2, 3], [1, 3, 0]]) {
    const outcome = Traits.EvaluateAll(CardCatalog.LastStand.AllTraits, new TraitContext({ Gap: gap, StaminaBefore: staminaBefore }));
    assert.equal(CardCatalog.LastStand.AllTraits.length, 2);
    assert.equal(outcome.PowerBonus, bonus, `gap ${gap} stamina ${staminaBefore}`);
    assert.equal(outcome.Triggered, bonus > 0);
  }
});

// ---- The C# dump ----

/** The outcome as the dump writes it. */
function outcomeJson(o) {
  return {
    Triggered: o.Triggered, PowerBonus: o.PowerBonus, GuardBonus: o.GuardBonus, NextTurnRecoveryBonus: o.NextTurnRecoveryBonus,
    StaminaGain: o.StaminaGain, Draw: o.Draw,
    Grants: o.Grants == null ? null : o.Grants.map((g) => [g.Kind, g.Stacks, g.OnSelf, g.Cap]),
    CostDown: o.CostDown, Convert: o.Convert, FollowUp: o.FollowUp, BreakBonus: o.BreakBonus,
  };
}

test('C#: every condition (three thresholds, plus four odd traits) on five boards', () => {
  const traits = [];
  for (const c of enumValues(TraitCondition)) {
    for (const th of [0, 2, 4]) {
      traits.push(new Trait(c, TraitEffect.PowerBonus, 3, { Threshold: th, Attribute: A.Guard, Omen: OmenKind.Attack, Watch: StatusKind.Bleed }));
    }
  }
  traits.push(new Trait(TraitCondition.Combo, TraitEffect.PowerBonus, 1, { Attribute: A.Attack | A.Skill }));
  traits.push(new Trait(TraitCondition.OmenIs, TraitEffect.PowerBonus, 1));
  traits.push(new Trait(TraitCondition.FoeHas, TraitEffect.PowerBonus, 1));
  traits.push(new Trait(TraitCondition.SelfHas, TraitEffect.PowerBonus, 1, { Watch: StatusKind.Parry }));
  const contexts = [
    new TraitContext(),
    new TraitContext({
      Gap: 2, OpponentGuard: 3, StaminaAfterUse: 4, StaminaBefore: 2, OpponentStamina: 2, Played: [A.Guard, A.Attack], HandAfterPlay: 2,
      OpponentOmen: OmenKind.Attack, SelfStatuses: set(StatusKind.Parry, 1), OpponentStatuses: set(StatusKind.Bleed, 1), Attributes: A.Attack, Moved: true,
    }),
    new TraitContext({
      Gap: 4, StaminaAfterUse: 1, StaminaBefore: 3, OpponentStamina: 3, Played: [A.Attack, A.Guard], HandAfterPlay: 3,
      OpponentOmen: OmenKind.Guard, SelfStatuses: set(StatusKind.Bleed, 1), OpponentStatuses: set(StatusKind.Parry, 2), Attributes: A.Attack,
    }),
    new TraitContext({ Gap: 1, Played: [A.Skill], Attributes: A.Skill, SelfStatuses: StatusSet.Empty, OpponentStatuses: StatusSet.Empty }),
    new TraitContext({ Played: [], OpponentOmen: null }),
  ];
  assert.equal(traits.length, fixture.TraitHolds.length);
  traits.forEach((t, i) => {
    assert.deepEqual(contexts.map((cx) => Traits.Holds(t, cx)), fixture.TraitHolds[i], `${t.Condition} threshold ${t.Threshold}`);
  });
});

test('C#: every effect, with and without a grant, met and unmet', () => {
  const first = new TraitContext({ Played: [] });
  const got = enumValues(TraitEffect).map((e) => ({
    Effect: e,
    Plain: outcomeJson(Traits.Evaluate(new Trait(TraitCondition.FirstPlay, e, 2), first)),
    Grant: outcomeJson(Traits.Evaluate(new Trait(TraitCondition.FirstPlay, e, 2, { Grant: new StatusGrant(StatusKind.Fragile, 2) }), first)),
    Unmet: outcomeJson(Traits.Evaluate(new Trait(TraitCondition.FirstPlay, e, 2), new TraitContext({ Played: [A.Attack] }))),
  }));
  assert.deepEqual(got, fixture.TraitEffects);
});

test('C#: 背水の陣 over gaps 0〜3 × stamina 0 / 2 / 3 / 10', () => {
  const got = [];
  for (const gap of [0, 1, 2, 3]) {
    for (const st of [0, 2, 3, 10]) {
      got.push([gap, st, outcomeJson(Traits.EvaluateAll(CardCatalog.LastStand.AllTraits, new TraitContext({ Gap: gap, StaminaBefore: st })))]);
    }
  }
  assert.deepEqual(got, fixture.LastStand);
});

test('C#: exceptions', () => {
  assert.deepEqual(errorOf(() => Traits.Holds(new Trait(99, TraitEffect.Draw), new TraitContext())), fixture.TraitBad.Condition);
  assert.deepEqual(errorOf(() => Traits.Evaluate(new Trait(TraitCondition.FirstPlay, 99), new TraitContext())), fixture.TraitBad.Effect);
  assert.deepEqual(errorOf(() => Traits.Holds(null, new TraitContext())), fixture.TraitBad.HoldsNull);
  assert.deepEqual(errorOf(() => Traits.EvaluateAll(null, new TraitContext())), fixture.TraitBad.EvaluateAllNull);
  assert.deepEqual(errorOf(() => Traits.Evaluate(null, null)), fixture.Errors.EvaluateNullContext);
  assert.deepEqual(errorOf(() => TraitOutcome.None.Plus(null)), fixture.Errors.PlusNull);
});

test('Named arguments: a typo throws, positional still works', () => {
  assert.throws(() => new TraitContext({ Gap: 1, Gapp: 2 }), /unknown named argument/);
  const c = new TraitContext(3, 4);
  assert.equal(c.Gap, 3);
  assert.equal(c.OpponentGuard, 4);
  assert.equal(c.with({ Moved: true }).Gap, 3);
});
