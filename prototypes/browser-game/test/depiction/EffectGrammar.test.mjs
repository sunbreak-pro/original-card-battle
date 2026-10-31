// Tests for src/bridge/depictionScript/EffectGrammar.js (port of View/Depiction/Script/EffectGrammar.cs).
// Ported cases: Assets/View/Depiction/Tests/EffectGrammarRuleTests.cs (the grammar's rule on its own:
// the words, their priority, the order the faces resolve in, one look per system). The core's cards
// are held by CardSystems.test.mjs (the Bridge's EffectGrammarTests).
//
// The rule cases run on the real module when DepictionTypes has been ported, else on the module
// isolated from it (its CueKind / StrikeSystem stubbed); the cases that read Effects / DepictionTypes
// themselves (EffectCatalog, StrikeSystems, EffectPlan) run on the real modules only.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { enumValues } from '../../src/shared/cs.js';
import { isolatedEffectGrammar, loadAll, needs, tryImport } from './_demoIsolate.mjs';

const real = await tryImport('bridge/depictionScript/EffectGrammar.js');
const g = real.module ?? (await isolatedEffectGrammar()).module;
const layer = real.module ? 'real' : 'isolated';
const { EffectGrammar, FxSystem, FxForm, FxMark, FxFaces } = g;
const { m: full, missing } = await loadAll([
  'bridge/depictionScript/EffectGrammar.js',
  'bridge/depictionScript/DepictionTypes.js',
  'bridge/depictionScript/Effects.js',
]);
const fullOpt = needs(missing);

/** A cue as the tests write it: the real Cue when DepictionTypes is there, else its fields alone. */
function cue(fields) {
  if (full && full.Cue) return Object.assign(new full.Cue(), fields);
  return { Fx: null, Amount: 0, ...fields };
}
const kinds = full ? full.CueKind : { GuardGain: 'GuardGain', TraitFire: 'TraitFire', StatusChange: 'StatusChange', Slash: 'Slash', StaminaChange: 'StaminaChange', DrawHand: 'DrawHand', HpChange: 'HpChange' };

// ---- the words ----

for (const [name, expected] of [
  ['突き打ち', 'Thrust'], // 突 before 打
  ['打ち払い', 'Strike'], // 打 before 払
  ['払い斬り', 'Sweep'], // 払 before 斬
  ['袈裟斬り', 'Slash'], // 斬 is the default
  ['一閃', 'Slash'],
  ['貫き', 'Thrust'],
  ['投げ刃', 'Thrust'],
  ['体当たり', 'Strike'],
  ['石礫', 'Strike'],
  ['圧殺', 'Strike'],
  ['横薙ぎ', 'Sweep'],
  ['牽制', 'Sweep'],
  ['', 'Slash'],
  [null, 'Slash'],
]) {
  test(`TheBlow_ReadsTheNameInTheOrder_Thrust_Strike_Sweep_Slash(${JSON.stringify(name)}, ${expected}) [${layer}]`, () => {
    assert.equal(EffectGrammar.BlowOf(name, false), FxSystem[expected]);
  });
}

test(`APushOrPull_IsStrike_BeforeAnyWordIsRead [${layer}]`, () => {
  assert.equal(EffectGrammar.BlowOf('石突きの押し込み', true), FxSystem.Strike);
  assert.equal(EffectGrammar.BlowOf('払い寄せ', true), FxSystem.Strike);
});

test(`EveryWordOfTheTable_ReadsAsItsSystem [${layer}]`, () => {
  // §5.3's three word lists, each word alone, and undefined (C# has no undefined; read like null).
  for (const w of ['突', '貫', '矢', '弩', '撃', '投']) assert.equal(EffectGrammar.BlowOf('x' + w + 'y', false), FxSystem.Thrust, w);
  for (const w of ['打', '当', '礫', '圧殺', '押し込み']) assert.equal(EffectGrammar.BlowOf(w, false), FxSystem.Strike, w);
  for (const w of ['薙', '払', '牽制']) assert.equal(EffectGrammar.BlowOf(w, false), FxSystem.Sweep, w);
  assert.equal(EffectGrammar.BlowOf('圧', false), FxSystem.Slash, '圧 alone is no word: 圧殺 is');
  assert.equal(EffectGrammar.BlowOf('押し', false), FxSystem.Slash, '押し alone is no word: 押し込み is');
  assert.equal(EffectGrammar.BlowOf('牽', false), FxSystem.Slash);
});

// ---- the faces ----

test(`TheFaces_PlayInResolveOrder_AttackGuardSkillStance [${layer}]`, () => {
  const shieldBash = new FxFaces({ Name: '盾打ち', Attacks: true, Guards: true });
  assert.deepEqual(EffectGrammar.Of(shieldBash), [FxSystem.Strike, FxSystem.Shield]);

  const rend = new FxFaces({ Name: '裂き斬り', Attacks: true, PutsStatus: true });
  assert.deepEqual(EffectGrammar.Of(rend), [FxSystem.Slash, FxSystem.Spirit]);

  const observe = new FxFaces({ Name: '観察', Guards: true, Breathes: true });
  assert.deepEqual(EffectGrammar.Of(observe), [FxSystem.Shield, FxSystem.Breath]);

  const stance = new FxFaces({ Name: '水の構え', Stance: true });
  assert.deepEqual(EffectGrammar.Of(stance), [FxSystem.Form]);

  const move = new FxFaces({ Name: '足運び' });
  assert.deepEqual(EffectGrammar.Of(move), [], '§5.3: a move has no system');
});

test(`AStatus_IsSpirit_EvenWhenTheSkillAlsoHealsOrDraws [${layer}]`, () => {
  const both = new FxFaces({ Name: '息継ぎ', PutsStatus: true, Breathes: true });
  assert.deepEqual(EffectGrammar.Of(both), [FxSystem.Spirit]);
  const breath = new FxFaces({ Name: '応急処置', Breathes: true });
  assert.deepEqual(EffectGrammar.Of(breath), [FxSystem.Breath]);
});

test(`TheFaces_DefaultToNothing_AndEveryFaceAtOnceKeepsTheOrder [${layer}]`, () => {
  const faces = new FxFaces();
  assert.deepEqual({ ...faces }, { Name: '', Attacks: false, Pushes: false, Guards: false, PutsStatus: false, Breathes: false, Stance: false });
  const all = new FxFaces({ Name: '突き', Attacks: true, Pushes: true, Guards: true, PutsStatus: true, Breathes: true, Stance: true });
  assert.deepEqual(EffectGrammar.Of(all), [FxSystem.Strike, FxSystem.Shield, FxSystem.Spirit, FxSystem.Form], 'a push is 打 before the name; 気 over 息');
  assert.throws(() => EffectGrammar.Of(null), (e) => e.name === 'ArgumentNullException' && e.message === "Value cannot be null. (Parameter 'faces')");
});

// ---- one look per system ----

test(`EverySystem_HasItsOwnForm_LookAndGlyph [${layer}]`, () => {
  assert.deepEqual([...EffectGrammar.Systems].sort(), enumValues(FxSystem).sort());
  assert.deepEqual([...EffectGrammar.Systems], enumValues(FxSystem), 'the table order is the declaration order');
  assert.equal(new Set(EffectGrammar.Systems.map((s) => EffectGrammar.FormOf(s))).size, 8);
  assert.equal(new Set(EffectGrammar.Systems.map((s) => EffectGrammar.LookId(s))).size, 8);
  assert.deepEqual(EffectGrammar.Systems.map((s) => EffectGrammar.Glyph(s)), ['斬', '突', '払', '打', '盾', '型', '気', '息']);
  assert.ok(Object.isFrozen(EffectGrammar.Systems));
});

test(`TheLook_IsReadOffTheSystemAlone [${layer}]`, () => {
  for (const system of EffectGrammar.Systems) assert.equal(EffectGrammar.LookId(system), EffectGrammar.LookId(system));
  assert.equal(EffectGrammar.LookId(FxSystem.Shield), 'fx.ring');
  assert.deepEqual(EffectGrammar.Systems.map((s) => EffectGrammar.LookId(s)),
    ['fx.arc', 'fx.line', 'fx.band', 'fx.ripple', 'fx.ring', 'fx.crest', 'fx.sigil', 'fx.pips']);
  assert.deepEqual(EffectGrammar.Systems.map((s) => EffectGrammar.FormOf(s)), enumValues(FxForm));
});

test(`ASystemTheTablesDoNotKnow_Throws [${layer}]`, () => {
  assert.throws(() => EffectGrammar.FormOf('Nothing'), (e) => e.name === 'ArgumentOutOfRangeException'
    && e.message === "No form is written for this system. (Parameter 'system')\nActual value was Nothing.");
  assert.throws(() => EffectGrammar.Glyph('Nothing'), (e) => e.name === 'ArgumentOutOfRangeException'
    && e.message.startsWith('No glyph is written for this system.'));
  assert.throws(() => EffectGrammar.LookId('Nothing'), (e) => e.name === 'ArgumentOutOfRangeException');
});

test(`TheBlowSystems_AreTheFour [${layer}]`, () => {
  assert.deepEqual(EffectGrammar.Systems.filter((s) => EffectGrammar.IsBlow(s)), [FxSystem.Slash, FxSystem.Thrust, FxSystem.Sweep, FxSystem.Strike]);
  assert.equal(EffectGrammar.IsBlow(null), false);
});

// ---- the mark beside a beat ----

test(`TheViewDraws_TheShapeOfShieldFormAndSpirit_AndBreathIsThePipsWithTheChestOnlyOnAHeal [${layer}]`, () => {
  // §5.3: 盾 / 型 / 気 draw their shape beside the beat; a blow draws its own inside the attack.
  assert.equal(EffectGrammar.MarkOf(cue({ Kind: kinds.GuardGain, Amount: 3, Fx: FxSystem.Shield })), FxMark.Shape);
  assert.equal(EffectGrammar.MarkOf(cue({ Kind: kinds.TraitFire, Fx: FxSystem.Form })), FxMark.Shape);
  assert.equal(EffectGrammar.MarkOf(cue({ Kind: kinds.StatusChange, Amount: 1, Fx: FxSystem.Spirit })), FxMark.Shape);
  assert.equal(EffectGrammar.MarkOf(cue({ Kind: kinds.Slash, Amount: 9, Fx: FxSystem.Thrust })), FxMark.None);
  // 息's form is the pips lighting (the stamina beat itself) and the draw is its own beat; a heal adds the chest's 400.
  assert.equal(EffectGrammar.MarkOf(cue({ Kind: kinds.StaminaChange, Amount: 1, Fx: FxSystem.Breath })), FxMark.None);
  assert.equal(EffectGrammar.MarkOf(cue({ Kind: kinds.DrawHand, Amount: 1, Fx: FxSystem.Breath })), FxMark.None);
  assert.equal(EffectGrammar.MarkOf(cue({ Kind: kinds.HpChange, Amount: 4, Fx: FxSystem.Breath })), FxMark.ChestHeave);
  assert.equal(EffectGrammar.MarkOf(cue({ Kind: kinds.HpChange, Amount: 4 })), FxMark.None, 'no system, nothing beside the beat');
  assert.equal(EffectGrammar.FormOf(FxSystem.Breath), FxForm.Pips);
  // Not in the C# test: a heal of 0 and a null cue.
  assert.equal(EffectGrammar.MarkOf(cue({ Kind: kinds.HpChange, Amount: 0, Fx: FxSystem.Breath })), FxMark.None);
  assert.equal(EffectGrammar.MarkOf(null), FxMark.None);
});

test('TheViewDraws_... (EffectCatalog: §5.3 息: 回復は + 400)', fullOpt, () => {
  assert.equal(full.EffectCatalog.Of(full.EffectId.BreathHeave).Ms, 400, '§5.3 息: 回復は + 400');
});

// ---- the blow's shape ----

test(`TheBlowSystems_MapToTheirShapeAndBack [${layer}]`, () => {
  const blows = [];
  for (const system of EffectGrammar.Systems) {
    if (!EffectGrammar.IsBlow(system)) continue;
    blows.push(system);
    assert.equal(EffectGrammar.FromStrike(EffectGrammar.ToStrike(system)), system);
  }
  assert.deepEqual(blows, [FxSystem.Slash, FxSystem.Thrust, FxSystem.Sweep, FxSystem.Strike]);
  assert.equal(EffectGrammar.FromStrike('Step'), null, 'a step has no system');
  // Not in the C# test: 盾 maps both ways too, and 型 / 気 / 息 have no blow of their own.
  assert.equal(EffectGrammar.ToStrike(FxSystem.Shield), 'Shield');
  assert.equal(EffectGrammar.FromStrike('Shield'), FxSystem.Shield);
  for (const s of [FxSystem.Form, FxSystem.Spirit, FxSystem.Breath]) assert.equal(EffectGrammar.ToStrike(s), 'Step', s);
});

test('StrikeSystems_ReadsTheSameWordsAsTheGrammar', fullOpt, () => {
  for (const name of ['突き', '峰打ち', '横薙ぎ', '袈裟斬り', '伸び突き', '打ち下ろし']) {
    assert.equal(full.StrikeSystems.Of(name, true, false, false, false),
      full.EffectGrammar.ToStrike(full.EffectGrammar.BlowOf(name, false)), name);
  }
});

test('TheGrammarsEffects_RunBesideTheBeats_SoNoEventWaitsLonger', fullOpt, () => {
  const { EffectCatalog, EffectId, DepictionEvent, DepictionEventKind, Cue, CueKind, UnitSide, EffectStrength, EffectPlan, EffectSwitches } = full;
  assert.equal(EffectCatalog.Of(EffectId.GrammarMark).Blocking, false);
  assert.equal(EffectCatalog.Of(EffectId.TraitAccent).Blocking, false);
  assert.equal(EffectCatalog.Of(EffectId.BreathHeave).Blocking, false);
  const ev = Object.assign(new DepictionEvent(), { Kind: DepictionEventKind.PlayCard });
  ev.Cues.push(Object.assign(new Cue(), { Kind: CueKind.Slash, Source: UnitSide.Player, Target: UnitSide.Enemy, Amount: 9, HpAfter: 40, Intensity: EffectStrength.Uniform }));
  ev.Cues.push(Object.assign(new Cue(), { Kind: CueKind.GuardGain, Target: UnitSide.Player, Amount: 3, GuardAfter: 3, Intensity: EffectStrength.Uniform }));
  const bare = EffectPlan.BlockingMs(ev, EffectSwitches.AllOn());
  ev.Cues[0].Fx = full.FxSystem.Slash;
  ev.Cues[0].TraitAccent = true;
  ev.Cues[1].Fx = full.FxSystem.Shield;
  assert.equal(EffectPlan.BlockingMs(ev, EffectSwitches.AllOn()), bare);
  assert.equal(ev.Cues[0].Intensity, EffectStrength.Uniform, "the trait's cue leaves the tier alone");
});
