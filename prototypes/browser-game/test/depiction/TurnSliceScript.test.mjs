// Tests for src/bridge/depictionScript/TurnSliceScript.js (port of View/Depiction/Script/TurnSliceScript.cs).
// Ported cases: Assets/View/Depiction/Tests/DepictionScriptTests.cs (all: the fixed slice played through
// DepictionRunner, DepictionTypes' runner) and the slice's case of EffectTests.cs
// (TheFixedSlice_WaitsForTheDrawsTheReleasesAndTheDiscard). The Effects case is skipped, naming the
// module, while it is missing.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { TurnSliceScript } from '../../src/bridge/depictionScript/TurnSliceScript.js';
import {
  CardAim, Cue, CueKind, DepictionEventKind, DepictionRunner, DropZone, OmenFrame, OmenIcon, PlayVerdict, RangeSide,
} from '../../src/bridge/depictionScript/DepictionTypes.js';

async function load(rel) {
  try { return await import(new URL('../../src/' + rel, import.meta.url).href); } catch { return null; }
}
const Effects = await load('bridge/depictionScript/Effects.js');
const needEffects = Effects ? false : 'needs bridge/depictionScript/Effects.js';

function Play(runner, cardId, zone, expectedOrder) {
  const r = runner.TryPlay(cardId, zone);
  assert.equal(r.verdict, PlayVerdict.Accepted);
  assert.equal(r.played.Order, expectedOrder);
}

test('SevenEventsFlowThroughInOrder', () => {
  const runner = new DepictionRunner(TurnSliceScript.Build());

  assert.equal(runner.AdvanceAuto().Order, 1);
  assert.equal(runner.Frame.Player.Stamina, 9);
  assert.equal(runner.Frame.Hand.length, 5, 'the filmed slice keeps the five-card hand from before #351');
  assert.equal(runner.Frame.Omen.KindLabel + runner.Frame.Omen.SideGlyph + runner.Frame.Omen.ValueText, '攻撃近13');
  assert.equal(runner.Frame.Omen.Icon, OmenIcon.Attack);

  Play(runner, TurnSliceScript.Kesagiri, DropZone.Receiver, 2);
  assert.equal(runner.Frame.Enemy.Hp, 51);
  assert.equal(runner.Frame.Player.Stamina, 8);

  Play(runner, TurnSliceScript.Daijodan, DropZone.Receiver, 3);
  assert.equal(runner.Frame.Enemy.Hp, 38);
  assert.equal(runner.Frame.Player.Stamina, 6);

  Play(runner, TurnSliceScript.Ushirotobi, DropZone.AboveThrowLine, 4);
  assert.equal(runner.Frame.Player.Range, RangeSide.Far);
  assert.equal(runner.Frame.Player.Guard, 7);
  assert.equal(runner.Frame.Player.Stamina, 5);

  assert.equal(runner.AdvanceAuto().Order, 5);
  assert.equal(runner.Frame.Player.Guard, 10);
  assert.deepEqual(runner.Frame.Hand, []);

  assert.equal(runner.AdvanceAuto().Order, 6);
  assert.equal(runner.Frame.Player.Hp, 47);
  assert.equal(runner.Frame.Player.Guard, 0);

  assert.equal(runner.AdvanceAuto().Order, 7);
  assert.equal(runner.Frame.Omen.KindLabel, '防御');
  assert.equal(runner.Frame.Omen.Icon, OmenIcon.Guard);
  assert.equal(runner.Frame.Omen.ValueText, '9');
  assert.equal(runner.Finished, true);
});

test('ThePlanTag_ReadsYotei_OrYoteiHenkouOnceChanged', () => {
  assert.equal(new OmenFrame().PlanTag, '予定');
  assert.equal(new OmenFrame({ PlanChanged: true }).PlanTag, '予定変更');
});

test('DragEventsDoNotAdvanceAutomatically', () => {
  const runner = new DepictionRunner(TurnSliceScript.Build());
  runner.AdvanceAuto();
  assert.equal(runner.WaitingForPlayer, true);
  assert.throws(() => runner.AdvanceAuto(), { name: 'InvalidOperationException' });
});

test('ACardTheFixedScriptDoesNotExpectReturnsToHand', () => {
  const runner = new DepictionRunner(TurnSliceScript.Build());
  runner.AdvanceAuto();
  const r = runner.TryPlay(TurnSliceScript.Daijodan, DropZone.Receiver);
  assert.equal(r.verdict, PlayVerdict.OffScript);
  assert.equal(r.played, null);
  assert.equal(runner.Index, 1);
});

test('SingleTargetCardReleasedOffTheReceiverReturnsToHand', () => {
  const runner = new DepictionRunner(TurnSliceScript.Build());
  runner.AdvanceAuto();
  assert.equal(runner.TryPlay(TurnSliceScript.Kesagiri, DropZone.None).verdict, PlayVerdict.WrongZone);
  assert.equal(runner.TryPlay(TurnSliceScript.Kesagiri, DropZone.AboveThrowLine).verdict, PlayVerdict.WrongZone);
  assert.equal(runner.Index, 1);
});

test('SelfCardNeedsTheThrowLine', () => {
  const runner = new DepictionRunner(TurnSliceScript.Build());
  runner.AdvanceAuto();
  Play(runner, TurnSliceScript.Kesagiri, DropZone.Receiver, 2);
  Play(runner, TurnSliceScript.Daijodan, DropZone.Receiver, 3);
  assert.equal(runner.TryPlay(TurnSliceScript.Ushirotobi, DropZone.Receiver).verdict, PlayVerdict.WrongZone);
  assert.equal(runner.TryPlay(TurnSliceScript.Ushirotobi, DropZone.AboveThrowLine).verdict, PlayVerdict.Accepted);
});

test('EveryCueCarriesItsSettledValueSoTheViewNeverComputes', { skip: needEffects }, () => {
  for (const ev of TurnSliceScript.Build().Events) {
    assert.notEqual(ev.After, null, 'event ' + ev.Order);
    for (const cue of ev.Cues) {
      assert.ok(cue.Intensity >= 1 && cue.Intensity <= 4, 'event ' + ev.Order + ' ' + cue.Kind);
      if (cue.Kind === CueKind.Slash || cue.Kind === CueKind.Hit || cue.Kind === CueKind.GuardGain) {
        assert.equal(cue.Intensity, Effects.EffectStrength.Uniform, '§5.3: one strength for every beat (#30)');
      }
      if (cue.Kind === CueKind.Hit) assert.notEqual(cue.HpAfter, Cue.Unchanged);
      if (cue.Kind === CueKind.GuardGain || cue.Kind === CueKind.GuardBlock || cue.Kind === CueKind.StanceCue) assert.notEqual(cue.GuardAfter, Cue.Unchanged);
      if (cue.Kind === CueKind.StaminaChange) {
        assert.notEqual(cue.StaminaAfter, Cue.Unchanged);
        assert.notEqual(cue.StaminaMax, Cue.Unchanged);
      }
    }
  }
});

test('EverySlashNamesItsAttacker', () => {
  let slashes = 0;
  for (const ev of TurnSliceScript.Build().Events) {
    for (const cue of ev.Cues) {
      if (cue.Kind !== CueKind.Slash) continue;
      slashes += 1;
      assert.notEqual(cue.Source, null, 'event ' + ev.Order + ' slash has no Source');
      assert.notEqual(cue.Source, cue.Target, 'event ' + ev.Order);
    }
  }
  assert.equal(slashes, 3);
});

function AssertRangeGlyphs(frame, where) {
  for (const unit of [frame.Player, frame.Enemy]) {
    if (!unit.HasRange) continue;
    // The glyph and the side are written separately; they must never disagree.
    assert.equal(unit.RangeGlyph, unit.Range === RangeSide.Near ? '近' : '遠', where);
  }
}

test('EveryUnitWithARangeSideCarriesItsGlyph', () => {
  const script = TurnSliceScript.Build();
  AssertRangeGlyphs(script.Opening, 'opening');
  for (const ev of script.Events) {
    AssertRangeGlyphs(ev.After, 'event ' + ev.Order);
    for (const cue of ev.Cues) {
      if (cue.Kind === CueKind.RangeSwitch) assert.notEqual(cue.RangeGlyphAfter, '', 'event ' + ev.Order);
    }
  }
  assert.equal(script.Opening.Player.RangeGlyph, '近');
  const rangeSwitch = script.Events[3].Cues.find((c) => c.Kind === CueKind.RangeSwitch);
  assert.ok(rangeSwitch, 'event 4 has no RangeSwitch cue');
  assert.equal(rangeSwitch.RangeAfter, RangeSide.Far);
  assert.equal(rangeSwitch.RangeGlyphAfter, '遠');
  assert.equal(script.Events[3].After.Player.RangeGlyph, '遠');
});

test('EveryThrowLineCardSaysWhoseFiguresItLandsOn', () => {
  const script = TurnSliceScript.Build();
  let throwLineCards = 0;
  for (const frame of [script.Opening, ...script.Events.map((e) => e.After)]) {
    for (const face of frame.Hand) {
      if (face.Aim !== CardAim.Self) continue;
      throwLineCards += 1;
      assert.notEqual(face.Affects, null, face.Id + ' has no Affects');
    }
  }
  assert.ok(throwLineCards > 0);
});

function Width(text) {
  let width = 0;
  for (const c of text) width += c.charCodeAt(0) < 0x2000 ? 0.5 : 1.0;
  return width;
}

test('EveryHandCardPrintsItsTypeAndWhatItDoes', () => {
  const script = TurnSliceScript.Build();
  let faces = 0;
  for (const frame of [script.Opening, ...script.Events.map((e) => e.After)]) {
    for (const face of frame.Hand) {
      faces += 1;
      assert.notEqual(face.TypeLabel, '', face.Id + ' has no TypeLabel');
      assert.notEqual(face.Description, '', face.Id + ' has no Description');
      // One sentence per line in a box three lines tall and about nine full-width characters wide.
      const sentences = face.Description.replace(/。+$/, '').split('。');
      assert.ok(sentences.length <= 3, face.Id + ' has more than three sentences');
      for (const sentence of sentences) assert.ok(Width(sentence + '。') <= 9.5, face.Id + ': ' + sentence);
      if (face.ValueText) assert.ok(face.Description.includes(face.ValueText), face.Id + ' describes a number other than its value');
    }
  }
  assert.ok(faces > 0);
});

test('TheFixedSlice_WaitsForTheDrawsTheReleasesAndTheDiscard', { skip: needEffects }, () => {
  const { EffectPlan, EffectId } = Effects;
  const cardIds = [EffectId.CardDraw, EffectId.CardRelease, EffectId.HandDiscard];
  const Steps = (ev) => EffectPlan.StepsOf(ev).map((s) => [s.Id, s.Count]);
  const CardSteps = (ev) => Steps(ev).filter(([id]) => cardIds.includes(id));
  const events = TurnSliceScript.Build().Events;

  assert.deepEqual(CardSteps(events[0]), [[EffectId.CardDraw, 5]]);
  for (const play of events.filter((e) => e.Kind === DepictionEventKind.PlayCard)) {
    assert.deepEqual(CardSteps(play), [[EffectId.CardRelease, 1]], 'event ' + play.Order);
    assert.deepEqual(Steps(play)[0], [EffectId.CardRelease, 1], "the release comes before the card's beats");
  }
  const turnEnd = events.filter((e) => e.Kind === DepictionEventKind.TurnEnd);
  assert.equal(turnEnd.length, 1);
  assert.deepEqual(CardSteps(turnEnd[0]), [[EffectId.HandDiscard, 2]]);
});

// ---- port-specific (checked against the C# field by field) ----

test('Port_TheSliceIsWrittenAsTheCsWritesIt', () => {
  const script = TurnSliceScript.Build();
  assert.deepEqual([TurnSliceScript.Kesagiri, TurnSliceScript.Daijodan, TurnSliceScript.Ushirotobi, TurnSliceScript.StanceThreshold], ['kesagiri', 'daijodan', 'ushirotobi', 3]);
  assert.equal(script.Events.length, 7);
  assert.deepEqual(script.Events.map((e) => e.Title), ['ターン開始', '袈裟斬り', '大上段', '後ろ跳び', 'ターン終了', '敵の攻撃', '次の予兆']);
  // One omen frame stands in events 1 to 5, as the C# shares one object.
  assert.strictEqual(script.Events[0].After.Omen, script.Events[4].After.Omen);
  assert.notStrictEqual(script.Events[0].After.Hand, script.Events[1].After.Hand);
  assert.deepEqual(script.Events[1].After.Hand.map((c) => c.Id), ['daijodan', 'ushirotobi', 'tetsu-no-uke', 'kansatsu']);
  assert.deepEqual(script.Events[3].After.Hand.map((c) => c.Id), ['tetsu-no-uke', 'kansatsu']);
  assert.equal(script.Opening.StanceHint, '+3');
  assert.equal(script.Events[4].After.StanceHint, '');
  assert.equal(script.Opening.Corner.ChainTotal, 3);
  assert.equal(script.Events[0].After.Hand[0].TraitLit, true);
  assert.equal(script.Events[0].After.Hand[1].TraitLit, false, '大上段 has no trait');
});
