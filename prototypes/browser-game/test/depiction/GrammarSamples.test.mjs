// Tests for src/bridge/depiction/GrammarSamples.js (port of View/Depiction/Bridge/GrammarSamples.cs).
//
// Golden: demo.golden.json, written by the C# program in the scratchpad (demo-oracle/Program.cs) on
// the real C# — the nine samples (file names and labels byte for byte), the sample of each system,
// and for every card of CardCatalog.All from every gap: whether the auto drag plays it, whether a
// trait fires, the play's title and the effect WatchOf finds per system.
//
// Real layer: Assets/View/Depiction/Bridge/Tests/GrammarSamplesTests.cs and the golden, on the real
// core and script writer — skipped while a module another agent ports is missing.
//
// Isolated layer: GrammarSamples' own search (For / Build / TryCard / FirstPlaying), PlayFirstCard,
// WatchOf, TraitFired, FileName and Label with the core stubbed: the stub source plays, for a card and
// a gap, a script whose cues are made from the golden's row (a blow's Slash cue, the 盾 / 型 / 気 mark,
// 息's heal or pips, a trait's beat), so Build must find the same nine samples as the C#.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import {
  CS, DEPICTION_TYPES_STUB, EFFECTS_STUB, importIsolated, isolatedEffectGrammar, loadAll, needs,
} from './_demoIsolate.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const golden = JSON.parse(readFileSync(join(here, 'demo.golden.json'), 'utf8'));

const { m, missing } = await loadAll([
  'bridge/depiction/GrammarSamples.js',
  'bridge/depiction/CardSystems.js',
  'bridge/depictionScript/EffectGrammar.js',
  'bridge/depictionScript/Effects.js',
  'bridge/depictionScript/DepictionTypes.js',
  'core/battle/CardCatalog.js',
]);
const realOpt = needs(missing);

const sampleJson = (s) => ({
  Index: s.Index, System: s.System, TraitAccent: s.TraitAccent, CardId: s.Card.Id, StartGap: s.StartGap, Watch: s.Watch,
  FileName: s.FileName, Label: s.Label,
});

// ======================================================================== the real layer

test('ThereIsOneSamplePerSystem_InTheTablesOrder_ThenTheTraitCue', realOpt, () => {
  const { GrammarSamples, EffectGrammar } = m;
  const samples = GrammarSamples.All;
  assert.equal(samples.length, EffectGrammar.Systems.length + 1);
  for (let i = 0; i < EffectGrammar.Systems.length; i++) {
    assert.equal(samples[i].System, EffectGrammar.Systems[i]);
    assert.equal(samples[i].TraitAccent, false, samples[i].Label);
  }
  const accent = samples[samples.length - 1];
  assert.equal(accent.TraitAccent, true);
  assert.equal(EffectGrammar.IsBlow(accent.System), true, 'the trait cue rides a blow');
  assert.deepEqual(samples.map((s) => s.Index), Array.from({ length: samples.length }, (_, i) => i + 1));
  assert.equal(new Set(samples.map((s) => s.FileName)).size, samples.length, 'one file per sample');
});

test('EverySample_IsOneOfTheInitialForty_AndPlaysItsSystemAlone', realOpt, () => {
  const initial = new Set(m.CardCatalog.All.slice(0, 40).map((d) => d.Id));
  for (const sample of m.GrammarSamples.All) {
    assert.equal(initial.has(sample.Card.Id), true, sample.Label + ': a card a run starts with (§2)');
    assert.deepEqual([...m.CardSystems.Of(sample.Card)], [sample.System], sample.Label + ': nothing else on the picture');
  }
});

test('TheSlashPicture_AndTheTraitPicture_AreTheSameCard_KesaCut', realOpt, () => {
  // The issue's example: 袈裟斬り (斬) lights its +5 at gap 0, and is plain from gap 1.
  const all = m.GrammarSamples.All;
  const slash = all[0];
  const accent = all[all.length - 1];
  assert.equal(slash.Card.Id, 'kesa_cut');
  assert.equal(accent.Card.Id, 'kesa_cut');
  assert.equal(accent.StartGap, 0);
  assert.equal(slash.StartGap, 1);
});

/** GrammarSamplesTests.ExpectedWatch: read off §5.3 / §5.9 and the system alone, not off the play. */
function ExpectedWatch(sample) {
  if (sample.TraitAccent) return m.EffectId.TraitAccent;
  if (m.EffectGrammar.IsBlow(sample.System)) return m.EffectId.StrikeShape;
  if (sample.System === m.FxSystem.Breath) return m.EffectId.BreathHeave;
  return m.EffectId.GrammarMark;
}

test('EverySamplesSource_PlaysTheCard_AndTheWatchedEffectIsOnTheScreensPlan', realOpt, () => {
  const { GrammarSamples, EffectCatalog, EffectPlan } = m;
  for (const sample of GrammarSamples.All) {
    const expected = ExpectedWatch(sample);
    assert.equal(sample.Watch, expected, sample.Label + ': the effect the capture waits for');
    const play = GrammarSamples.PlayFirstCard(sample.CreateSource());
    assert.notEqual(play, null, sample.Label + ': the auto drag plays nothing');
    assert.equal(play.Title, sample.Card.Name, sample.Label);
    assert.equal(GrammarSamples.WatchOf(play, sample.System, sample.TraitAccent), expected, sample.Label + ': the play shows it');
    // The eight show their system alone; only the last one shows a trait firing.
    assert.equal(GrammarSamples.TraitFired(play), sample.TraitAccent, sample.Label + ': a trait fires');
    if (EffectCatalog.Of(sample.Watch).Blocking) {
      // A waited beat is one of the plan's steps, so the screen holds it and the trace records it.
      assert.equal(EffectPlan.StepsOf(play).some((s) => s.Id === sample.Watch), true, sample.Label);
    }
  }
});

test('TheSamplePictures_ShowEightDifferentLooks', realOpt, () => {
  const looks = m.GrammarSamples.All.filter((s) => !s.TraitAccent).map((s) => m.EffectGrammar.LookId(s.System));
  assert.equal(new Set(looks).size, m.EffectGrammar.Systems.length);
});

test('Golden: the nine samples, their files and labels', realOpt, () => {
  assert.deepEqual(m.GrammarSamples.All.map(sampleJson), golden.grammarSamples);
  assert.deepEqual(
    { DeckCopies: m.GrammarSamples.DeckCopies, MaxGap: m.GrammarSamples.MaxGap, StartHp: m.GrammarSamples.StartHp },
    golden.grammarConsts);
});

test('Golden: each system\'s sample (For)', realOpt, () => {
  for (const [system, want] of Object.entries(golden.grammarFor)) {
    const s = m.GrammarSamples.For(system);
    assert.deepEqual({ CardId: s.Card.Id, StartGap: s.StartGap, Watch: s.Watch, Index: s.Index }, want, system);
  }
});

test('Golden: every card from every gap — the play, the trait and what WatchOf finds', realOpt, () => {
  const { GrammarSamples, EffectGrammar, CardCatalog } = m;
  for (const want of golden.watches) {
    const play = GrammarSamples.PlayFirstCard(GrammarSamples.SourceFor(CardCatalog.ById(want.Card), want.Gap));
    const got = { Card: want.Card, Gap: want.Gap, Played: play != null, TraitFired: GrammarSamples.TraitFired(play), Watch: {} };
    for (const system of EffectGrammar.Systems) {
      const plain = GrammarSamples.WatchOf(play, system, false);
      const accent = GrammarSamples.WatchOf(play, system, true);
      if (plain != null) got.Watch[system] = plain;
      if (accent != null) got.Watch[system + '+accent'] = accent;
    }
    if (play != null) got.Title = play.Title;
    assert.deepEqual(got, want, `${want.Card} at gap ${want.Gap}`);
  }
});

// ======================================================================== the isolated layer

const grammar = await isolatedEffectGrammar();
const cardsJson = JSON.stringify(golden.cards.map(([Id, Name]) => ({ Id, Name })));
const rowsJson = JSON.stringify(Object.fromEntries(golden.cards.map(([id, , row]) => [id, row])));

const iso = (await importIsolated('bridge/depiction/GrammarSamples.js', {
  '../../core/battle/CardCatalog.js': `export const CardCatalog = { All: Object.freeze(${cardsJson}.map(Object.freeze)) };`,
  '../../core/battle/Cards.js': 'export const Cards = { BuildDeck(defs, copies) { return { defs, copies }; } };',
  '../../core/battle/Enemies.js': "export const Enemies = { PolearmWarped: Object.freeze({ Id: 'polearm_warped' }) };",
  '../../core/battle/TurnLoop.js': `export class BattleSetup {
    static SliceFieldCells = 6;
    constructor(Enemy, Deck, FieldCells, named) { Object.assign(this, { Enemy, Deck, FieldCells }, named); }
  }`,
  '../depictionScript/DepictionTypes.js': DEPICTION_TYPES_STUB,
  '../depictionScript/EffectGrammar.js': { url: grammar.url },
  '../depictionScript/Effects.js': EFFECTS_STUB,
  './BattleLaunch.js': 'export const BattleLaunch = { DefaultSeed: 20260921 };',
  './CardSystems.js': `const rows = ${rowsJson}; export const CardSystems = { Of(def) { return rows[def.Id]; } };`,
  // The stub source plays the script globalThis.__grammarPlay(cardId, gap) hands it: null for a card
  // the auto drag cannot play from that gap.
  './CoreBattleSource.js': `export class CoreBattleSource {
    constructor(setup, seed, suggestCards = false) {
      this.Setup = setup; this.Seed = seed; this.SuggestCards = suggestCards;
      const card = setup.Deck.defs[0];
      this.Play = globalThis.__grammarPlay(card.Id, setup.StartGap);
      this.WaitingForPlayer = false; this.Finished = false;
      this.Frame = { Hand: [{ Id: card.Id + '#1', Aim: 'Single' }] };
      globalThis.__grammarSources.push(this);
    }
    AdvanceAuto() { this.WaitingForPlayer = true; return { Kind: 'TurnStart', Cues: [] }; }
    get SuggestedCardId() { return this.WaitingForPlayer && this.Play ? this.Frame.Hand[0].Id : ''; }
    TryPlay(id, zone) {
      if (id !== this.Frame.Hand[0].Id || zone !== 'zone-for-Single') return { ok: 'WrongZone', value: null };
      this.WaitingForPlayer = false;
      return { ok: 'Accepted', value: this.Play };
    }
  }`,
})).module;
const { GrammarSamples, GrammarSample } = iso;
const { FxSystem } = grammar.module;

/** The script a golden row stands for: the cues WatchOf and TraitFired read, and the title. */
function playOf(row) {
  if (!row.Played) return null;
  const cues = [];
  if (row.TraitFired) cues.push({ Kind: 'TraitFire', Fx: null, Amount: 0, Text: '特性' });
  for (const [key, watch] of Object.entries(row.Watch)) {
    const [system, accent] = key.split('+');
    if (watch === 'StrikeShape' || watch === 'TraitAccent') {
      cues.push({ Kind: 'Slash', Source: 'Player', Fx: system, TraitAccent: accent === 'accent', Amount: 9 });
    } else if (watch === 'BreathHeave') {
      cues.push({ Kind: 'HpChange', Fx: system, Amount: 4 });
    } else if (watch === 'StaminaRecover') {
      cues.push({ Kind: 'StaminaChange', Fx: system, Amount: 1 });
    } else if (watch === 'GrammarMark') {
      cues.push({ Kind: system === 'Form' ? 'TraitFire' : system === 'Shield' ? 'GuardGain' : 'StatusChange', Fx: system, Amount: 1 });
    } else {
      throw new Error('a watch the stub does not write: ' + watch);
    }
  }
  return { Kind: 'PlayCard', Title: row.Title, Cues: cues };
}
const rowsByPlay = new Map(golden.watches.map((w) => [w.Card + '@' + w.Gap, w]));
globalThis.__grammarSources = [];
globalThis.__grammarPlay = (id, gap) => {
  const row = rowsByPlay.get(id + '@' + gap);
  if (!row) throw new Error('no golden row for ' + id + ' at ' + gap);
  return playOf(row);
};

test('[isolated] TheStubPlays_ReadBackAsTheGoldenSaysTheyShow', () => {
  // The stub's cues are read by the port's own WatchOf / TraitFired: they must give back the golden row.
  for (const row of golden.watches) {
    const play = playOf(row);
    const got = {};
    for (const system of Object.values(FxSystem)) {
      const plain = GrammarSamples.WatchOf(play, system, false);
      const accent = GrammarSamples.WatchOf(play, system, true);
      if (plain != null) got[system] = plain;
      if (accent != null) got[system + '+accent'] = accent;
    }
    assert.deepEqual(got, row.Watch, `${row.Card} at ${row.Gap}`);
    assert.equal(GrammarSamples.TraitFired(play), row.TraitFired);
  }
});

test('[isolated] Build_FindsTheNineSamplesTheCsFinds', () => {
  globalThis.__grammarSources = [];
  const all = GrammarSamples.All;
  assert.deepEqual(all.map(sampleJson), golden.grammarSamples);
  assert.ok(Object.isFrozen(all));
  assert.equal(GrammarSamples.All, all, 'built once');
  // Every source is the sample card alone, 12 copies, on the slice's line, at half HP, the default seed, suggesting.
  const source = globalThis.__grammarSources[0];
  assert.deepEqual([source.Setup.Deck.copies, source.Setup.FieldCells, source.Setup.PlayerStartHp, source.Seed, source.SuggestCards], [12, 6, 25, 20260921, true]);
  assert.equal(source.Setup.Enemy.Id, 'polearm_warped');
  assert.ok(globalThis.__grammarSources.every((s) => s.Setup.Deck.defs.length === 1 && s.Setup.StartGap >= 0 && s.Setup.StartGap <= 4));
});

test('[isolated] For_FindsEachSystemsSample', () => {
  for (const [system, want] of Object.entries(golden.grammarFor)) {
    const s = GrammarSamples.For(system);
    assert.deepEqual({ CardId: s.Card.Id, StartGap: s.StartGap, Watch: s.Watch, Index: s.Index }, want, system);
  }
});

test('[isolated] TheSearch_FallsBack_PlainThenTraitThenShared_AndThrowsWhenNothingPlays', () => {
  const saved = globalThis.__grammarPlay;
  try {
    const rowOf = (id) => golden.cards.find((c) => c[0] === id)[2];
    // Thrust, plain first across every card: with 突き's plays all firing a trait, the next card that
    // plays 突 alone and plainly is taken.
    globalThis.__grammarPlay = (id, gap) => {
      const play = playOf(rowsByPlay.get(id + '@' + gap));
      if (play && id === 'thrust') play.Cues.unshift({ Kind: 'TraitFire', Fx: null, Amount: 0 });
      return play;
    };
    const plainFirst = GrammarSamples.For(FxSystem.Thrust);
    assert.notEqual(plainFirst.Card.Id, 'thrust');
    assert.deepEqual(rowOf(plainFirst.Card.Id), ['Thrust']);
    const earlier = golden.cards.slice(0, golden.cards.findIndex((c) => c[0] === plainFirst.Card.Id)).filter((c) => c[2].length === 1 && c[2][0] === 'Thrust' && c[0] !== 'thrust');
    for (const [id] of earlier) {
      assert.ok([0, 1, 2, 3, 4].every((gap) => !rowsByPlay.get(id + '@' + gap).Watch.Thrust || rowsByPlay.get(id + '@' + gap).TraitFired), id + ' had no plain gap');
    }

    // Every 突-alone card fires a trait wherever it plays → the first gap where it shows with a trait.
    globalThis.__grammarPlay = (id, gap) => {
      const play = playOf(rowsByPlay.get(id + '@' + gap));
      const row = rowOf(id);
      if (play && row.length === 1 && row[0] === 'Thrust') play.Cues.unshift({ Kind: 'TraitFire', Fx: null, Amount: 0 });
      return play;
    };
    const thrust = GrammarSamples.For(FxSystem.Thrust);
    assert.deepEqual([thrust.Card.Id, thrust.StartGap], ['thrust', 0], 'a gap where a trait fires, on the first card');

    // 盾: no card plays it alone → the first card that shares its row with another system and shows it.
    const withoutAlone = (system) => (id, gap) => {
      const play = playOf(rowsByPlay.get(id + '@' + gap));
      const alone = rowOf(id);
      if (play && alone.length === 1 && alone[0] === system) return null;
      return play;
    };
    globalThis.__grammarPlay = withoutAlone('Shield');
    const shield = GrammarSamples.For(FxSystem.Shield);
    const row = rowOf(shield.Card.Id);
    assert.ok(row.includes('Shield') && row.length > 1, shield.Card.Id + ' shares its row');
    const firstShared = golden.cards.find(([id, , r]) => r.length > 1 && r.includes('Shield')
      && [0, 1, 2, 3, 4].some((gap) => rowsByPlay.get(id + '@' + gap).Watch.Shield));
    assert.equal(shield.Card.Id, firstShared[0]);
    assert.equal(shield.Watch, 'GrammarMark');

    // 息: with the cards that play it alone gone, no card that shares its row shows 息 from any gap: nothing plays it.
    globalThis.__grammarPlay = withoutAlone('Breath');
    assert.throws(() => GrammarSamples.For(FxSystem.Breath), (e) => e.name === 'InvalidOperationException' && e.message === 'No card plays 息.');

    // Nothing plays at all.
    globalThis.__grammarPlay = () => null;
    assert.throws(() => GrammarSamples.For(FxSystem.Spirit), (e) => e.name === 'InvalidOperationException' && e.message === 'No card plays 気.');
  } finally {
    globalThis.__grammarPlay = saved;
  }
});

test('[isolated] FileName_AndLabel', () => {
  const card = { Id: 'kesa_cut', Name: '袈裟斬り' };
  const s = new GrammarSample({ Index: 1, System: FxSystem.Slash, Card: card, StartGap: 1, Watch: 'StrikeShape' });
  assert.equal(s.FileName, '01-slash.png');
  assert.equal(s.Label, '斬 袈裟斬り (kesa_cut) 間合い 1 StrikeShape');
  const accent = new GrammarSample({ Index: 9, System: FxSystem.Slash, TraitAccent: true, Card: card, StartGap: 0, Watch: 'TraitAccent' });
  assert.equal(accent.FileName, '09-slash-trait-accent.png');
  assert.equal(accent.Label, '斬 + 特性の合図 袈裟斬り (kesa_cut) 間合い 0 TraitAccent');
  assert.equal(new GrammarSample({ Index: 123, System: FxSystem.Breath }).FileName, '123-breath.png', '"00" pads, never cuts');
  assert.equal(new GrammarSample({ Index: -3, System: FxSystem.Form }).FileName, '-03-form.png');
  const blank = new GrammarSample();
  assert.deepEqual({ ...blank }, { Index: 0, System: 'Slash', TraitAccent: false, Card: null, StartGap: 0, Watch: 'CardDraw' }, 'C# defaults');
  assert.equal(blank.FileName, '00-slash.png');
});

test('[isolated] TheConstants', () => {
  assert.equal(GrammarSamples.DeckCopies, golden.grammarConsts.DeckCopies);
  assert.equal(GrammarSamples.MaxGap, golden.grammarConsts.MaxGap);
  assert.equal(GrammarSamples.StartHp, golden.grammarConsts.StartHp);
});

test('[isolated] WatchOf_ReadsOnlyThePlayersBlow_OfThatSystem_WithThatCue', () => {
  const W = (cues, system, accent) => GrammarSamples.WatchOf({ Cues: cues }, system, accent);
  const blow = { Kind: 'Slash', Source: 'Player', Fx: 'Thrust', TraitAccent: false };
  assert.equal(W([blow], 'Thrust', false), 'StrikeShape');
  assert.equal(W([blow], 'Thrust', true), null, 'no cue on the blow');
  assert.equal(W([{ ...blow, TraitAccent: true }], 'Thrust', true), 'TraitAccent');
  assert.equal(W([{ ...blow, TraitAccent: true }], 'Thrust', false), null);
  assert.equal(W([{ ...blow, Source: 'Enemy' }], 'Thrust', false), null, "the enemy's blow");
  assert.equal(W([{ ...blow, Kind: 'Hit' }], 'Thrust', false), null);
  assert.equal(W([blow], 'Slash', false), null, 'another system');
  assert.equal(W([{ Kind: 'GuardGain', Fx: 'Shield', Amount: 3 }], 'Shield', true), null, 'the cue rides a blow only');
  assert.equal(W([{ Kind: 'GuardGain', Fx: 'Shield', Amount: 3 }], 'Shield', false), 'GrammarMark');
  assert.equal(W([{ Kind: 'GuardGain', Fx: 'Spirit', Amount: 3 }], 'Shield', false), null);
  // 息: the chest's rise on a heal first, else the pips lighting; a draw or a cost is neither.
  assert.equal(W([{ Kind: 'StaminaChange', Fx: 'Breath', Amount: 1 }, { Kind: 'HpChange', Fx: 'Breath', Amount: 4 }], 'Breath', false), 'BreathHeave');
  assert.equal(W([{ Kind: 'StaminaChange', Fx: 'Breath', Amount: 1 }], 'Breath', false), 'StaminaRecover');
  assert.equal(W([{ Kind: 'StaminaChange', Fx: 'Breath', Amount: 0 }], 'Breath', false), null);
  assert.equal(W([{ Kind: 'DrawHand', Fx: 'Breath', Amount: 1 }], 'Breath', false), null);
  assert.equal(W([{ Kind: 'HpChange', Fx: null, Amount: 4 }], 'Breath', false), null);
  assert.equal(GrammarSamples.WatchOf(null, 'Slash', false), null);
});

test('[isolated] TraitFired_IsATraitsOwnBeat_NotTheStancesFormFace', () => {
  assert.equal(GrammarSamples.TraitFired({ Cues: [{ Kind: 'TraitFire', Fx: null }] }), true);
  assert.equal(GrammarSamples.TraitFired({ Cues: [{ Kind: 'TraitFire', Fx: 'Form' }] }), false, '「構え・…」 carries 型');
  assert.equal(GrammarSamples.TraitFired({ Cues: [{ Kind: 'Slash', Fx: null }] }), false);
  assert.equal(GrammarSamples.TraitFired({ Cues: [] }), false);
  assert.equal(GrammarSamples.TraitFired(null), false);
});

/** A hand-made IDepictionSource for PlayFirstCard's branches. */
function fakeSource({ waitAfter = 1, finishedAt = Infinity, suggest = 'a', hand = [{ Id: 'a', Aim: 'Self' }], verdict = 'Accepted', played: event = null, after = [] }) {
  let advances = 0;
  let playedOnce = false;
  const queue = [...after];
  return {
    // Before the play: waiting once the turn has started (waitAfter events); after it: once the queue is read.
    get WaitingForPlayer() { return playedOnce ? queue.length === 0 : advances >= waitAfter; },
    get Finished() { return advances >= finishedAt; },
    AdvanceAuto() {
      advances++;
      return playedOnce ? queue.shift() : { Kind: 'TurnStart', Cues: [] };
    },
    get SuggestedCardId() { return suggest; },
    Frame: { Hand: hand },
    TryPlay(id, zone) {
      this.zone = zone;
      if (verdict !== 'Accepted') return { ok: verdict, value: null };
      playedOnce = true;
      return { ok: verdict, value: event };
    },
    get Advances() { return advances; },
  };
}

test('[isolated] PlayFirstCard_StartsTheTurn_ReleasesOnTheAimsZone_AndReadsOnToThePlay', () => {
  const play = { Kind: 'PlayCard', Cues: [] };
  const direct = fakeSource({ waitAfter: 1, played: play });
  assert.equal(GrammarSamples.PlayFirstCard(direct), play);
  assert.equal(direct.zone, 'zone-for-Self', 'released on the zone its aim needs');
  assert.equal(direct.Advances, 1);

  // The played event is not the play itself: read on to the next PlayCard.
  const later = { Kind: 'PlayCard', Title: 'later', Cues: [] };
  const readOn = fakeSource({ played: { Kind: 'TurnEnd', Cues: [] }, after: [{ Kind: 'TurnStart', Cues: [] }, later, { Kind: 'PlayCard', Cues: [] }] });
  assert.equal(GrammarSamples.PlayFirstCard(readOn), later);

  // Nothing suggested, a card not in the hand, a refused play, a battle over, a turn that never waits.
  assert.equal(GrammarSamples.PlayFirstCard(fakeSource({ suggest: '' })), null);
  assert.equal(GrammarSamples.PlayFirstCard(fakeSource({ suggest: null })), null);
  assert.equal(GrammarSamples.PlayFirstCard(fakeSource({ suggest: 'b' })), null);
  assert.equal(GrammarSamples.PlayFirstCard(fakeSource({ verdict: 'OutOfRange' })), null);
  assert.equal(GrammarSamples.PlayFirstCard(fakeSource({ waitAfter: 5, finishedAt: 2 })), null);
  const never = fakeSource({ waitAfter: Infinity });
  assert.equal(GrammarSamples.PlayFirstCard(never), null);
  assert.equal(never.Advances, 51, 'the guard lets 51 events by');
  const waitsAt51 = fakeSource({ waitAfter: 51, played: play });
  assert.equal(GrammarSamples.PlayFirstCard(waitsAt51), play);
  assert.throws(() => GrammarSamples.PlayFirstCard(null), (e) => e.name === 'ArgumentNullException' && e.message === "Value cannot be null. (Parameter 'source')");
  assert.throws(() => GrammarSamples.SetupFor(null, 0), (e) => e.name === 'ArgumentNullException' && e.message === "Value cannot be null. (Parameter 'card')");
});
