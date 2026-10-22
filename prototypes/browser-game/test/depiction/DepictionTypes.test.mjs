// Tests for src/bridge/depictionScript/DepictionTypes.js (port of View/Depiction/Script/DepictionTypes.cs).
// Ported cases: Assets/View/Depiction/Tests/DepictionScriptTests.cs (the fixed slice through
// DepictionRunner), EffectTests.cs TheSystem_ComesFromTheNameAndTheFaces (StrikeSystems.Of),
// AStanceCard_LeavesForTheExilePile (PlayedTo), plus the members the C# tests read indirectly
// (defaults, LitFor / HpLossFor / CostDrop, PlanTag, RowText, the runner's texts and refusals).
//
// The module's own logic runs on the real module when its siblings (OmenDisclosure, PileLayout) are
// ported, else on the module with those two imports swapped for stubs holding the C# constants. The
// cases that read other agents' modules (TurnSliceScript, EffectGrammar's words) run on the real
// modules only and are skipped, naming the module, while it is missing.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve, relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { enumValues } from '../../src/shared/cs.js';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '../..');
const srcFile = (rel) => resolve(root, 'src', rel);
const dataUrl = (source) => 'data:text/javascript;charset=utf-8,' + encodeURIComponent(source).replace(/'/g, '%27');

async function tryImport(rel) {
  try {
    return { module: await import(pathToFileURL(srcFile(rel)).href), missing: null };
  } catch (e) {
    const why = String(e && e.message ? e.message : e).split('\n')[0];
    const notFound = /Cannot find module '([^']+)'/.exec(why);
    return { module: null, missing: notFound ? relative(root, notFound[1]) : `${rel} (${why})` };
  }
}

/** DepictionTypes.js with its imports of the other agents' modules swapped for stubs (C# constants only). */
async function isolatedTypes() {
  const cs = pathToFileURL(srcFile('shared/cs.js')).href;
  const stubs = {
    './OmenDisclosure.js': 'export const OmenDisclosure = { Unread: 0, Partial: 1, Full: 2 };',
    './PileLayout.js': `import { defineEnum } from '${cs}'; export const PileKind = defineEnum(['Draw', 'Discard', 'Exile']);`,
    './EffectGrammar.js': "export const EffectGrammar = { BlowOf() { throw new Error('stub'); }, ToStrike() { throw new Error('stub'); } };",
  };
  const path = srcFile('bridge/depictionScript/DepictionTypes.js');
  const source = readFileSync(path, 'utf8').replace(/(\bfrom\s+)'([^']+)'/g, (m, head, spec) => {
    if (stubs[spec]) return head + JSON.stringify(dataUrl(stubs[spec]));
    if (spec.startsWith('.')) return head + JSON.stringify(pathToFileURL(resolve(dirname(path), spec)).href);
    return m;
  });
  return import(dataUrl(source + `\n// isolated ${Math.random()}\n`));
}

const real = await tryImport('bridge/depictionScript/DepictionTypes.js');
const T = real.module ?? (await isolatedTypes());
const layer = real.module ? 'real' : 'isolated';
const realOnly = real.module ? {} : { skip: `needs ${real.missing}` };
const slice = await tryImport('bridge/depictionScript/TurnSliceScript.js');
const withSlice = !real.module ? realOnly : slice.module ? {} : { skip: `needs ${slice.missing}` };
const pileLayout = await tryImport('bridge/depictionScript/PileLayout.js');

const {
  RangeSide, UnitSide, CardKind, OmenIcon, StatusGlyph, OmenStatus, OmenHit, CardAim, DropZone, CardFace,
  CardValueKind, CardValue, TermDefinition, TermRow, TermMark, CardDetail, ChipKind, StatusChip, UnitFrame,
  StanceRow, OmenFrame, CornerFrame, BattleOutcome, DepictionFrame, PileFrame, PileCard, PlayedFrame,
  FloorFrame, StrikeSystem, StrikeSystems, DepictionEventKind, CueKind, Cue, DepictionEvent, DepictionScript,
  PlayVerdict, DepictionText, IDepictionSource, DepictionRunner,
} = T;
const PileKind = pileLayout.module ? pileLayout.module.PileKind : { Draw: 'Draw', Discard: 'Discard', Exile: 'Exile' };

test(`[${layer}] the enums keep the C# members in their order`, () => {
  assert.deepEqual(enumValues(RangeSide), ['Near', 'Far']);
  assert.deepEqual(enumValues(UnitSide), ['Player', 'Enemy']);
  assert.deepEqual(enumValues(CardKind), ['Attack', 'Guard', 'Stance', 'Skill', 'Move']);
  assert.deepEqual(enumValues(OmenIcon), ['None', 'Attack', 'Guard', 'Status', 'Move', 'Stance', 'Rest', 'Seal']);
  assert.deepEqual(enumValues(StatusGlyph), ['None', 'Bleed', 'Fragile', 'Slow', 'Intimidate', 'Fatigue']);
  assert.deepEqual(enumValues(OmenHit), ['None', 'Lands', 'Misses']);
  assert.deepEqual(enumValues(CardAim), ['Single', 'Self']);
  assert.deepEqual(enumValues(DropZone), ['None', 'Receiver', 'AboveThrowLine']);
  assert.deepEqual(enumValues(CardValueKind), ['Power', 'Guard', 'Heal', 'Close', 'Back', 'Push', 'Pull', 'Draw']);
  assert.deepEqual(enumValues(TermRow), ['TraitTop', 'TraitBottom']);
  assert.deepEqual(enumValues(ChipKind), ['OnFoe', 'OnSelf', 'Lasting', 'Boss']);
  assert.deepEqual(enumValues(BattleOutcome), ['Ongoing', 'Won', 'Lost']);
  assert.deepEqual(enumValues(StrikeSystem), ['Slash', 'Thrust', 'Sweep', 'Strike', 'Shield', 'Step']);
  assert.deepEqual(enumValues(DepictionEventKind), ['TurnStart', 'PlayCard', 'TurnEnd', 'EnemyAction', 'NextOmen', 'Defeat']);
  assert.deepEqual(enumValues(CueKind), [
    'GuardReset', 'StaminaChange', 'DrawHand', 'OmenShow', 'TraitFire', 'Slash', 'GuardGain', 'RangeSwitch',
    'StanceCue', 'DiscardHand', 'EnemyWindup', 'SideBonusMiss', 'GuardBlock', 'Hit', 'StatusChange', 'HpChange', 'Defeat',
  ]);
  assert.deepEqual(enumValues(PlayVerdict), [
    'Accepted', 'NotWaiting', 'NotInHand', 'NotEnoughStamina', 'OutOfRange', 'WrongZone', 'OffScript', 'Bound',
  ]);
  assert.ok(Array.isArray(IDepictionSource.$members));
});

test(`[${layer}] a new class carries the C# field initializers (enums at their first member, strings null when uninitialized)`, () => {
  const face = new CardFace();
  assert.equal(face.Id, null);
  assert.equal(face.Name, null);
  assert.equal(face.Cost, 0);
  assert.equal(face.Kind, CardKind.Attack);
  assert.equal(face.Aim, CardAim.Single);
  assert.equal(face.Affects, null);
  assert.equal(face.TypeLabel, '');
  assert.equal(face.RequiredRange, null);
  assert.deepEqual(face.Values, []);
  assert.deepEqual(face.Terms, []);
  assert.equal(face.Detail, null);
  assert.equal(face.StanceHintAfterPay, null);
  assert.equal(face.TraitLit, false);

  const cue = new Cue();
  assert.equal(Cue.Unchanged, -1);
  assert.equal(cue.Kind, CueKind.GuardReset);
  assert.equal(cue.Target, UnitSide.Player);
  assert.equal(cue.Source, null);
  assert.equal(cue.Intensity, 1);
  for (const k of ['HpAfter', 'GuardAfter', 'StaminaAfter', 'StaminaMax', 'StacksAfter', 'RebuildAfter']) assert.equal(cue[k], -1, k);
  assert.equal(cue.RangeAfter, RangeSide.Near);
  assert.equal(cue.System, StrikeSystem.Slash);
  assert.equal(cue.Fx, null);
  assert.equal(cue.CellsMoved, 0);

  const omen = new OmenFrame();
  assert.equal(omen.Disclosure, 2, 'OmenDisclosure.Full');
  assert.equal(omen.Icon, OmenIcon.None);
  assert.equal(omen.Hit, OmenHit.None);
  assert.equal(new StatusChip().Label, null);
  assert.equal(new StatusChip().Kind, ChipKind.OnFoe);
  assert.equal(new StanceRow().Count, 1);
  assert.equal(new OmenStatus().Glyph, StatusGlyph.None);
  assert.equal(new CardValue().Kind, CardValueKind.Power);
  assert.equal(new TermMark().Row, TermRow.TraitTop);
  assert.deepEqual(new CardDetail().Terms, []);
  assert.equal(new TermDefinition().Meaning, '');
  assert.equal(new UnitFrame().Range, RangeSide.Near);
  assert.equal(new CornerFrame().ChainTotal, 0);
  const floor = new FloorFrame();
  assert.equal(floor.PlayerSize, 1);
  assert.equal(floor.EnemySize, 1);

  const frame = new DepictionFrame();
  assert.equal(frame.Outcome, BattleOutcome.Ongoing);
  assert.ok(frame.Player instanceof UnitFrame && frame.Enemy instanceof UnitFrame && frame.Player !== frame.Enemy);
  assert.ok(frame.Floor instanceof FloorFrame && frame.Piles instanceof PileFrame && frame.Played instanceof PlayedFrame);
  assert.equal(frame.EnemyTurn, false);

  const ev = new DepictionEvent();
  assert.equal(ev.Kind, DepictionEventKind.TurnStart);
  assert.equal(ev.After, null);
  assert.equal(ev.WaitsForDrag, false);
  assert.equal(new DepictionScript().Opening, null);

  // The C# object initializer.
  const set = new Cue({ Kind: CueKind.Hit, Amount: 5 });
  assert.equal(set.Kind, CueKind.Hit);
  assert.equal(set.Amount, 5);
  assert.equal(set.HpAfter, -1);
});

test(`[${layer}] a card that went to the exile pile says so; any other goes to the discard pile (#455)`, () => {
  assert.equal(new DepictionEvent().PlayedTo, PileKind.Discard);
  assert.equal(new DepictionEvent({ Kind: DepictionEventKind.PlayCard }).WaitsForDrag, true);
});

test(`[${layer}] the lamp and the wick per enemy fall back to the card's own when the slot is not written (§6.3, #507)`, () => {
  const face = new CardFace({ TraitLit: true, LitOn: [false, true], HpLossOn: [4, 0] });
  assert.equal(face.LitFor(0), false);
  assert.equal(face.LitFor(1), true);
  assert.equal(face.LitFor(2), true, 'past the list: TraitLit');
  assert.equal(face.LitFor(-1), true);
  assert.equal(face.HpLossFor(0), 4);
  assert.equal(face.HpLossFor(1), 0);
  assert.equal(face.HpLossFor(5), 0);
  assert.equal(face.HpLossFor(-1), 0);
  assert.equal(new CardFace().LitFor(0), false);
});

test(`[${layer}] the cost circle drops only below the printed cost (§6.1)`, () => {
  assert.equal(new CardFace({ PrintedCost: 3, Cost: 1 }).CostDrop, 2);
  assert.equal(new CardFace({ PrintedCost: 2, Cost: 2 }).CostDrop, 0);
  assert.equal(new CardFace({ PrintedCost: 1, Cost: 3 }).CostDrop, 0);
});

test(`[${layer}] the plan tag reads 予定, or 予定変更 once changed`, () => {
  // DepictionScriptTests.ThePlanTag_ReadsYotei_OrYoteiHenkouOnceChanged
  assert.equal(new OmenFrame().PlanTag, '予定');
  assert.equal(new OmenFrame({ PlanChanged: true }).PlanTag, '予定変更');
});

test(`[${layer}] a pile row reads "突き 2 / 3"; each pile hands its own list (§1.1 C4)`, () => {
  assert.equal(new PileCard({ Name: '突き', InPile: 2, InDeck: 3 }).RowText, '突き 2 / 3');
  assert.equal(new PileCard({ Name: '岩の構え', InPile: 1, InDeck: 1 }).RowText, '岩の構え 1 / 1');
  assert.equal(new PileCard({ Name: null }).RowText, ' 0 / 0', 'C# writes a null name as ""');

  const piles = new PileFrame();
  assert.equal(piles.CardsOf(PileKind.Draw), piles.DrawCards);
  assert.equal(piles.CardsOf(PileKind.Discard), piles.DiscardCards);
  assert.equal(piles.CardsOf(PileKind.Exile), piles.ExileCards);
  assert.throws(() => piles.CardsOf('Hand'), (e) => e.name === 'ArgumentOutOfRangeException'
    && e.message === "Specified argument was out of the range of valid values. (Parameter 'pile')\nActual value was Hand.");
});

test(`[${layer}] the tally counts attack, guard, skill and stance; a move counts as none (§4.9)`, () => {
  const played = new PlayedFrame();
  assert.equal(played.Last, null);
  played.Add(CardKind.Attack);
  played.Add(CardKind.Attack);
  played.Add(CardKind.Stance);
  assert.equal(played.Last, CardKind.Stance);
  played.Add(CardKind.Move);
  assert.equal(played.Last, CardKind.Stance, 'a move is not the last card counted');
  played.Add(CardKind.Guard);
  played.Add(CardKind.Skill);
  assert.equal(played.CountOf(CardKind.Attack), 2);
  assert.equal(played.CountOf(CardKind.Guard), 1);
  assert.equal(played.CountOf(CardKind.Skill), 1);
  assert.equal(played.CountOf(CardKind.Stance), 1);
  assert.equal(played.CountOf(CardKind.Move), 0);
  assert.equal(played.Last, CardKind.Skill);
});

test(`[${layer}] the zones and strings every source shares`, () => {
  assert.equal(DepictionText.RequiredZone(CardAim.Single), DropZone.Receiver);
  assert.equal(DepictionText.RequiredZone(CardAim.Self), DropZone.AboveThrowLine);
  assert.equal(DepictionText.YourTurn, 'あなたの番');
  assert.equal(DepictionText.EnemyTurn, '敵の番');
  assert.equal(DepictionText.Won, '勝ち');
  assert.equal(DepictionText.Lost, '負け');
  assert.equal(DepictionText.Pushed, '押し出し');
  assert.equal(DepictionText.PlanTag, '予定');
  assert.equal(DepictionText.PlanChangedTag, '予定変更');
  assert.equal(DepictionText.OutcomeText(BattleOutcome.Won), '勝ち');
  assert.equal(DepictionText.OutcomeText(BattleOutcome.Lost), '負け');
  assert.equal(DepictionText.OutcomeText(BattleOutcome.Ongoing), '');
  assert.equal(DepictionText.ZoneName(CardAim.Single), '敵の受け皿の上');
  assert.equal(DepictionText.ZoneName(CardAim.Self), '投げ上げ線より上');
  const cut = new CardFace({ Id: 'c1', Name: '袈裟斬り', Aim: CardAim.Single });
  const leap = new CardFace({ Id: 'c2', Name: '後ろ跳び', Aim: CardAim.Self });
  assert.equal(DepictionText.WrongZoneText(cut), '「袈裟斬り」は敵の受け皿の上で離すと出せます');
  assert.equal(DepictionText.WrongZoneText(leap), '「後ろ跳び」は投げ上げ線より上で離すと出せます');
  assert.equal(DepictionText.Find([cut, leap], 'c2'), leap);
  assert.equal(DepictionText.Find([cut, leap], 'c9'), null);
  assert.equal(DepictionText.NameOf([cut, leap], 'c1'), '袈裟斬り');
  assert.equal(DepictionText.NameOf([cut, leap], 'c9'), '');
});

// ---- DepictionRunner on a small hand-written script (its own logic, no other module) ----

function miniScript() {
  const cut = new CardFace({ Id: 'cut', Name: '袈裟斬り', Aim: CardAim.Single });
  const leap = new CardFace({ Id: 'leap', Name: '後ろ跳び', Aim: CardAim.Self, Affects: UnitSide.Player });
  const opening = new DepictionFrame();
  const dealt = new DepictionFrame({ Hand: [cut, leap] });
  const afterCut = new DepictionFrame({ Hand: [leap] });
  const afterLeap = new DepictionFrame({ Hand: [] });
  const end = new DepictionFrame({ Outcome: BattleOutcome.Won });
  const script = new DepictionScript({ Opening: opening });
  script.Events.push(new DepictionEvent({ Order: 1, Kind: DepictionEventKind.TurnStart, After: dealt }));
  script.Events.push(new DepictionEvent({ Order: 2, Kind: DepictionEventKind.PlayCard, CardId: 'cut', Aim: CardAim.Single, PreviewText: '9', After: afterCut }));
  script.Events.push(new DepictionEvent({ Order: 3, Kind: DepictionEventKind.PlayCard, CardId: 'leap', Aim: CardAim.Self, After: afterLeap }));
  script.Events.push(new DepictionEvent({ Order: 4, Kind: DepictionEventKind.TurnEnd, After: end }));
  return { script, opening, dealt, afterCut, end };
}

test(`[${layer}] the runner walks a fixed script: auto events, drags in the script's order, then the end`, () => {
  const { script, opening, dealt, afterCut, end } = miniScript();
  const runner = new DepictionRunner(script);
  assert.equal(runner.Frame, opening);
  assert.equal(runner.Index, 0);
  assert.equal(runner.Finished, false);
  assert.equal(runner.WaitingForPlayer, false);
  assert.equal(runner.CanEndTurn, false);
  assert.equal(runner.SuggestedCardId, '');
  assert.equal(runner.GuideText, '');
  assert.equal(runner.Inspect('cut'), PlayVerdict.NotWaiting);
  assert.deepEqual(runner.TryPlay('cut', DropZone.Receiver), { ok: PlayVerdict.NotWaiting, value: null, verdict: PlayVerdict.NotWaiting, played: null });
  assert.equal(runner.PreviewFor('cut'), '', 'the next event is not the card');

  assert.equal(runner.AdvanceAuto().Order, 1);
  assert.equal(runner.Frame, dealt);
  assert.equal(runner.WaitingForPlayer, true);
  assert.equal(runner.SuggestedCardId, 'cut');
  assert.equal(runner.GuideText, '台本の次の一手：「袈裟斬り」を敵の受け皿の上へ');
  assert.equal(runner.PreviewFor('cut'), '9');
  assert.equal(runner.PreviewFor('leap'), '');
  assert.throws(() => runner.AdvanceAuto(), (e) => e.name === 'InvalidOperationException' && e.message === 'Event 2 waits for a drag.');
  assert.throws(() => runner.EndTurn(), (e) => e.name === 'InvalidOperationException' && e.message === 'A fixed script ends its own turn; the player cannot.');

  assert.equal(runner.Inspect('leap'), PlayVerdict.OffScript);
  const off = runner.TryPlay('leap', DropZone.AboveThrowLine);
  assert.equal(off.ok, PlayVerdict.OffScript);
  assert.equal(off.value, null);
  assert.equal(runner.RefusalText('leap', PlayVerdict.OffScript), '「後ろ跳び」はまだ出せません。台本の次は「袈裟斬り」です');
  assert.equal(runner.TryPlay('cut', DropZone.AboveThrowLine).ok, PlayVerdict.WrongZone);
  assert.equal(runner.TryPlay('cut', DropZone.None).verdict, PlayVerdict.WrongZone);
  assert.equal(runner.RefusalText('cut', PlayVerdict.WrongZone), '「袈裟斬り」は敵の受け皿の上で離すと出せます');
  assert.equal(runner.RefusalText('cut', PlayVerdict.NotEnoughStamina), '');
  assert.equal(runner.RefusalText('nope', PlayVerdict.OffScript), '', 'a card not in the hand has nothing to say');
  assert.equal(runner.Index, 1);

  const played = runner.TryPlay('cut', DropZone.Receiver);
  assert.equal(played.ok, PlayVerdict.Accepted);
  assert.equal(played.value.Order, 2);
  assert.equal(played.played, played.value);
  assert.equal(runner.Frame, afterCut);
  assert.equal(runner.GuideText, '台本の次の一手：「後ろ跳び」を投げ上げ線より上へ');
  assert.equal(runner.TryPlay('leap', DropZone.Receiver).ok, PlayVerdict.WrongZone);
  assert.equal(runner.TryPlay('leap', DropZone.AboveThrowLine).value.Order, 3);

  assert.equal(runner.Frame.Hand.length, 0);
  assert.equal(runner.GuideText, '');
  assert.equal(runner.AdvanceAuto().Order, 4);
  assert.equal(runner.Frame, end);
  assert.equal(runner.Finished, true);
  assert.equal(runner.Next, null);
  assert.equal(runner.WaitingForPlayer, false);
  assert.equal(runner.PreviewFor('cut'), '');
  assert.equal(runner.RefusalText('cut', PlayVerdict.OffScript), '');
  assert.throws(() => runner.AdvanceAuto(), (e) => e.name === 'InvalidOperationException' && e.message === 'The script has already finished.');
  assert.throws(() => new DepictionRunner(null), (e) => e.name === 'ArgumentNullException');
});

// ---- DepictionScriptTests (the filmed slice, TurnSliceScript) ----

function play(runner, cardId, zone, expectedOrder) {
  const r = runner.TryPlay(cardId, zone);
  assert.equal(r.ok, PlayVerdict.Accepted);
  assert.equal(r.value.Order, expectedOrder);
}

test('SevenEventsFlowThroughInOrder', withSlice, () => {
  const { TurnSliceScript } = slice.module;
  const runner = new DepictionRunner(TurnSliceScript.Build());

  assert.equal(runner.AdvanceAuto().Order, 1);
  assert.equal(runner.Frame.Player.Stamina, 9);
  assert.equal(runner.Frame.Hand.length, 5, 'the filmed slice keeps the five-card hand from before #351');
  assert.equal(runner.Frame.Omen.KindLabel + runner.Frame.Omen.SideGlyph + runner.Frame.Omen.ValueText, '攻撃近13');
  assert.equal(runner.Frame.Omen.Icon, OmenIcon.Attack);

  play(runner, TurnSliceScript.Kesagiri, DropZone.Receiver, 2);
  assert.equal(runner.Frame.Enemy.Hp, 51);
  assert.equal(runner.Frame.Player.Stamina, 8);

  play(runner, TurnSliceScript.Daijodan, DropZone.Receiver, 3);
  assert.equal(runner.Frame.Enemy.Hp, 38);
  assert.equal(runner.Frame.Player.Stamina, 6);

  play(runner, TurnSliceScript.Ushirotobi, DropZone.AboveThrowLine, 4);
  assert.equal(runner.Frame.Player.Range, RangeSide.Far);
  assert.equal(runner.Frame.Player.Guard, 7);
  assert.equal(runner.Frame.Player.Stamina, 5);

  assert.equal(runner.AdvanceAuto().Order, 5);
  assert.equal(runner.Frame.Player.Guard, 10);
  assert.equal(runner.Frame.Hand.length, 0);

  assert.equal(runner.AdvanceAuto().Order, 6);
  assert.equal(runner.Frame.Player.Hp, 47);
  assert.equal(runner.Frame.Player.Guard, 0);

  assert.equal(runner.AdvanceAuto().Order, 7);
  assert.equal(runner.Frame.Omen.KindLabel, '防御');
  assert.equal(runner.Frame.Omen.Icon, OmenIcon.Guard);
  assert.equal(runner.Frame.Omen.ValueText, '9');
  assert.equal(runner.Finished, true);
});

test('DragEventsDoNotAdvanceAutomatically', withSlice, () => {
  const runner = new DepictionRunner(slice.module.TurnSliceScript.Build());
  runner.AdvanceAuto();
  assert.equal(runner.WaitingForPlayer, true);
  assert.throws(() => runner.AdvanceAuto(), (e) => e.name === 'InvalidOperationException');
});

test('ACardTheFixedScriptDoesNotExpectReturnsToHand', withSlice, () => {
  const { TurnSliceScript } = slice.module;
  const runner = new DepictionRunner(TurnSliceScript.Build());
  runner.AdvanceAuto();
  const r = runner.TryPlay(TurnSliceScript.Daijodan, DropZone.Receiver);
  assert.equal(r.ok, PlayVerdict.OffScript);
  assert.equal(r.value, null);
  assert.equal(runner.Index, 1);
});

test('SingleTargetCardReleasedOffTheReceiverReturnsToHand', withSlice, () => {
  const { TurnSliceScript } = slice.module;
  const runner = new DepictionRunner(TurnSliceScript.Build());
  runner.AdvanceAuto();
  assert.equal(runner.TryPlay(TurnSliceScript.Kesagiri, DropZone.None).ok, PlayVerdict.WrongZone);
  assert.equal(runner.TryPlay(TurnSliceScript.Kesagiri, DropZone.AboveThrowLine).ok, PlayVerdict.WrongZone);
  assert.equal(runner.Index, 1);
});

test('SelfCardNeedsTheThrowLine', withSlice, () => {
  const { TurnSliceScript } = slice.module;
  const runner = new DepictionRunner(TurnSliceScript.Build());
  runner.AdvanceAuto();
  play(runner, TurnSliceScript.Kesagiri, DropZone.Receiver, 2);
  play(runner, TurnSliceScript.Daijodan, DropZone.Receiver, 3);
  assert.equal(runner.TryPlay(TurnSliceScript.Ushirotobi, DropZone.Receiver).ok, PlayVerdict.WrongZone);
  assert.equal(runner.TryPlay(TurnSliceScript.Ushirotobi, DropZone.AboveThrowLine).ok, PlayVerdict.Accepted);
});

test('EveryCueCarriesItsSettledValueSoTheViewNeverComputes', withSlice, async () => {
  const { EffectStrength } = await import(pathToFileURL(srcFile('bridge/depictionScript/Effects.js')).href);
  const script = slice.module.TurnSliceScript.Build();
  for (const ev of script.Events) {
    assert.notEqual(ev.After, null, 'event ' + ev.Order);
    for (const cue of ev.Cues) {
      assert.ok(cue.Intensity >= 1 && cue.Intensity <= 4, 'event ' + ev.Order + ' ' + cue.Kind);
      if (cue.Kind === CueKind.Slash || cue.Kind === CueKind.Hit || cue.Kind === CueKind.GuardGain) {
        assert.equal(cue.Intensity, EffectStrength.Uniform, '§5.3: one strength for every beat (#30)');
      }
      if (cue.Kind === CueKind.Hit) assert.notEqual(cue.HpAfter, Cue.Unchanged);
      if (cue.Kind === CueKind.GuardGain || cue.Kind === CueKind.GuardBlock || cue.Kind === CueKind.StanceCue) {
        assert.notEqual(cue.GuardAfter, Cue.Unchanged);
      }
      if (cue.Kind === CueKind.StaminaChange) {
        assert.notEqual(cue.StaminaAfter, Cue.Unchanged);
        assert.notEqual(cue.StaminaMax, Cue.Unchanged);
      }
    }
  }
});

test('EverySlashNamesItsAttacker', withSlice, () => {
  let slashes = 0;
  for (const ev of slice.module.TurnSliceScript.Build().Events) {
    for (const cue of ev.Cues) {
      if (cue.Kind !== CueKind.Slash) continue;
      slashes += 1;
      assert.notEqual(cue.Source, null, 'event ' + ev.Order + ' slash has no Source');
      assert.notEqual(cue.Source, cue.Target, 'event ' + ev.Order);
    }
  }
  assert.equal(slashes, 3);
});

function assertRangeGlyphs(frame, where) {
  for (const unit of [frame.Player, frame.Enemy]) {
    if (!unit.HasRange) continue;
    // The glyph and the side are written separately; they must never disagree.
    assert.equal(unit.RangeGlyph, unit.Range === RangeSide.Near ? '近' : '遠', where);
  }
}

test('EveryUnitWithARangeSideCarriesItsGlyph', withSlice, () => {
  const script = slice.module.TurnSliceScript.Build();
  assertRangeGlyphs(script.Opening, 'opening');
  for (const ev of script.Events) {
    assertRangeGlyphs(ev.After, 'event ' + ev.Order);
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

test('EveryThrowLineCardSaysWhoseFiguresItLandsOn', withSlice, () => {
  const script = slice.module.TurnSliceScript.Build();
  const affects = (frame, where) => {
    let n = 0;
    for (const face of frame.Hand) {
      if (face.Aim !== CardAim.Self) continue;
      n += 1;
      assert.notEqual(face.Affects, null, where + ' ' + face.Id + ' has no Affects');
    }
    return n;
  };
  let throwLineCards = affects(script.Opening, 'opening');
  for (const ev of script.Events) throwLineCards += affects(ev.After, 'event ' + ev.Order);
  assert.ok(throwLineCards > 0);
});

test('EveryHandCardPrintsItsTypeAndWhatItDoes', withSlice, () => {
  const script = slice.module.TurnSliceScript.Build();
  const frames = [script.Opening, ...script.Events.map((ev) => ev.After)];
  const width = (text) => {
    let w = 0;
    for (const c of text) w += c.charCodeAt(0) < 0x2000 ? 0.5 : 1.0;
    return w;
  };
  let faces = 0;
  for (const frame of frames) {
    for (const face of frame.Hand) {
      faces += 1;
      assert.notEqual(face.TypeLabel, '', face.Id + ' has no TypeLabel');
      assert.notEqual(face.Description, '', face.Id + ' has no Description');
      // C#: Description.TrimEnd('。').Split('。')
      const sentences = face.Description.replace(/。+$/, '').split('。');
      assert.ok(sentences.length <= 3, face.Id + ' has more than three sentences');
      for (const sentence of sentences) assert.ok(width(sentence + '。') <= 9.5, face.Id + ': ' + sentence);
      if (face.ValueText) assert.ok(face.Description.includes(face.ValueText), face.Id + ' describes a number other than its value');
    }
  }
  assert.ok(faces > 0);
});

// ---- EffectTests.TheSystem_ComesFromTheNameAndTheFaces (StrikeSystems.Of on the grammar's words) ----

const systemCases = [
  ['払い斬り', true, false, false, false, 'Sweep'], // 払 is read before 斬
  ['穂先の突き', true, false, false, false, 'Thrust'],
  ['石突きの押し込み', true, false, true, true, 'Strike'], // the push wins over a 突 in the name
  ['柄で受ける', false, true, false, false, 'Shield'],
  ['踏み込み', false, true, true, false, 'Step'],
  ['袈裟斬り', true, false, false, false, 'Slash'],
  ['盾打ち', true, true, false, false, 'Strike'],
  ['打ち下ろし', true, false, false, false, 'Strike'], // 鉄壁の門竜: 斬 under its old name 兜割り, 打 since #311
  ['牽制', true, false, true, false, 'Sweep'],
  ['伸び突き', true, false, false, false, 'Thrust'],
];

test('TheSystem_ComesFromTheNameAndTheFaces', realOnly, () => {
  for (const [name, attacks, guards, moves, pushes, expected] of systemCases) {
    assert.equal(StrikeSystems.Of(name, attacks, guards, moves, pushes), StrikeSystem[expected], name);
  }
});

test(`[${layer}] StrikeSystems.Of without a blow: a move steps, a guard alone raises the shield, nothing steps`, () => {
  assert.equal(StrikeSystems.Of('踏み込み', false, false, true, false), StrikeSystem.Step);
  assert.equal(StrikeSystems.Of('踏み込み', false, true, true, false), StrikeSystem.Step);
  assert.equal(StrikeSystems.Of('柄で受ける', false, true, false, false), StrikeSystem.Shield);
  assert.equal(StrikeSystems.Of('息を整える', false, false, false, false), StrikeSystem.Step);
});
