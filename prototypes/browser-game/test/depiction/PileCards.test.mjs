// Port of the representative cases of unity-project-kit/Assets/View/Depiction/Bridge/Tests/PileReadoutTests.cs
// (#455: the piles' counts and lists on the script), plus direct cases of PileCards.Rows.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { PileCards } from '../../src/bridge/depiction/PileCards.js';
import { CoreScriptWriter } from '../../src/bridge/depiction/CoreScriptWriter.js';
import { Cue, CueKind, DepictionEventKind, PileFrame } from '../../src/bridge/depictionScript/DepictionTypes.js';
import { PileKind } from '../../src/bridge/depictionScript/PileLayout.js';
import { EffectId, EffectPlan } from '../../src/bridge/depictionScript/Effects.js';
import { CardCatalog } from '../../src/core/battle/CardCatalog.js';
import { Cards } from '../../src/core/battle/Cards.js';
import { Enemies } from '../../src/core/battle/Enemies.js';
import { BattleSetup, TurnLoop } from '../../src/core/battle/TurnLoop.js';
import { FixedRng } from '../../src/core/battle/IRng.js';
import { DeckReshuffled } from '../../src/core/battle/BattleEvents.js';
import { CardInstance, GameResult } from '../../src/core/battle/Types.js';

const NoShuffle = () => new FixedRng(0.9999999);

const Kinds = () => [CardCatalog.RockStance, CardCatalog.Brace, CardCatalog.KesaCut, CardCatalog.Feint, CardCatalog.ReachThrust];

/** 20 cards, dealt in this order (NoShuffle): the first hand holds 岩の構え and 呼吸を整える. */
const Deck = () => Cards.BuildDeck(Kinds(), 4);

function Opened() {
  const setup = new BattleSetup(Enemies.PolearmWarped, Deck(), BattleSetup.SliceFieldCells, { StartGap: 2 });
  const state = TurnLoop.Start(setup, NoShuffle()).State;
  const writer = new CoreScriptWriter(Enemies.PolearmWarped);
  return { writer, state, opening: writer.Opening(state) };
}

function AssertMatches(piles, state) {
  assert.equal(piles.Shown, true);
  assert.equal(piles.Draw, state.DrawPile.length, 'draw');
  assert.equal(piles.Discard, state.DiscardPile.length, 'discard');
  assert.equal(piles.Exile, state.Exiled.length, 'exile');
}

const last = (arr) => arr[arr.length - 1];

// ---- PileCards.Rows directly ----

test('Rows_FollowTheCatalogue_NotThePile_AndCountAgainstTheDeck', () => {
  const pile = [
    new CardInstance('feint-0', CardCatalog.Feint),
    new CardInstance('kesa_cut-0', CardCatalog.KesaCut),
    new CardInstance('feint-1', CardCatalog.Feint),
    new CardInstance('thrust-0', CardCatalog.Thrust),
  ];
  const deck = new Map([[CardCatalog.Feint.Name, 3], [CardCatalog.KesaCut.Name, 2]]);
  const rows = PileCards.Rows(pile, deck);
  const order = (name) => CardCatalog.All.findIndex((d) => d.Name === name);

  assert.deepEqual(rows.map((r) => r.Name), [CardCatalog.Thrust.Name, CardCatalog.KesaCut.Name, CardCatalog.Feint.Name]
    .sort((a, b) => order(a) - order(b)));
  const feint = rows.find((r) => r.Name === CardCatalog.Feint.Name);
  assert.equal(feint.InPile, 2);
  assert.equal(feint.InDeck, 3);
  assert.equal(feint.RowText, CardCatalog.Feint.Name + ' 2 / 3');
  // A name the deck does not count falls back to what the pile holds.
  const thrust = rows.find((r) => r.Name === CardCatalog.Thrust.Name);
  assert.equal(thrust.InDeck, 1);
  // No deck at all: every row counts the pile only.
  assert.deepEqual(PileCards.Rows(pile, null).map((r) => r.InDeck), rows.map((r) => r.InPile));
});

test('Rows_PutACardTheCatalogueLacks_AfterEveryOneItHas', () => {
  const stranger = CardCatalog.KesaCut.with({ Id: 'no_such_card', Name: 'ん' });
  const pile = [new CardInstance('x-0', stranger), new CardInstance('thrust-0', CardCatalog.Thrust)];
  assert.deepEqual(PileCards.Rows(pile, null).map((r) => r.Name), [CardCatalog.Thrust.Name, 'ん']);
});

test('Fill_RefusesNulls', () => {
  assert.throws(() => PileCards.Fill(null, Opened().state), (e) => e.name === 'ArgumentNullException');
  assert.throws(() => PileCards.Fill(new PileFrame(), null), (e) => e.name === 'ArgumentNullException');
});

// ---- PileReadoutTests ----

test('BeforeTheFirstDraw_TheWholeDeckIsTheDrawPile', () => {
  const { state, opening } = Opened();
  AssertMatches(opening.Piles, state);
  assert.equal(opening.Piles.Draw, 20);
  assert.equal(opening.Piles.Discard, 0);
  assert.equal(opening.Piles.Exile, 0);
});

test('AtTheTurnStart_TheDrawPileHasGivenTheHand', () => {
  const { writer, state } = Opened();
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  const frame = last(writer.Write(begin.Events, begin.State)).After;
  AssertMatches(frame.Piles, begin.State);
  assert.equal(frame.Piles.Draw + frame.Hand.length, 20);
});

test('APlayedCard_GoesToTheDiscardPile_AndAStanceToTheExilePile', () => {
  const { writer, state } = Opened();
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  writer.Write(begin.Events, begin.State);
  const now = begin.State;

  const stance = now.Hand.find((c) => c.Def === CardCatalog.RockStance);
  const other = now.Hand.find((c) => c.Def !== CardCatalog.RockStance && c.Def.Cost <= now.Player.Stamina);
  assert.ok(stance, 'the deck is dealt unshuffled, 岩の構え first');
  assert.ok(other, 'and the self card after it');

  const played = TurnLoop.PlayCard(now, other.InstanceId, NoShuffle());
  const otherEvents = writer.Write(played.Events, played.State);
  const afterOther = last(otherEvents).After;
  AssertMatches(afterOther.Piles, played.State);
  assert.equal(afterOther.Piles.Discard, 1);
  assert.equal(otherEvents.filter((e) => e.Kind === DepictionEventKind.PlayCard).length, 1);
  assert.equal(otherEvents.find((e) => e.Kind === DepictionEventKind.PlayCard).PlayedTo, PileKind.Discard);

  assert.ok(stance.Def.Cost <= played.State.Player.Stamina);
  const placed = TurnLoop.PlayCard(played.State, stance.InstanceId, NoShuffle());
  const stanceEvents = writer.Write(placed.Events, placed.State);
  const afterStance = last(stanceEvents).After;
  AssertMatches(afterStance.Piles, placed.State);
  assert.equal(afterStance.Piles.Exile, 1, 'a stance card stays out of this battle');
  assert.equal(stanceEvents.find((e) => e.Kind === DepictionEventKind.PlayCard).PlayedTo, PileKind.Exile);
  assert.deepEqual(afterStance.Piles.ExileCards.map((c) => c.RowText), [CardCatalog.RockStance.Name + ' 1 / 4']);
});

test('ThePileLists_CountEachKind_AgainstTheWholeDeck_InTheCatalogsOrder', () => {
  const { writer, state } = Opened();
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  const piles = last(writer.Write(begin.Events, begin.State)).After.Piles;

  assert.equal(piles.DrawCards.reduce((s, c) => s + c.InPile, 0), piles.Draw, 'every card of the draw pile is on a row');
  const names = piles.DrawCards.map((c) => c.Name);
  assert.equal(new Set(names).size, names.length, 'one row per kind');
  assert.ok(piles.DrawCards.every((c) => c.InDeck === 4), '4 of each kind in the deck, the hand\'s included');
  assert.deepEqual(piles.DiscardCards, []);
  assert.deepEqual(piles.ExileCards, []);
  const order = piles.DrawCards.map((c) => CardCatalog.All.findIndex((d) => d.Name === c.Name));
  assert.deepEqual(order, [...order].sort((a, b) => a - b));
  assert.equal(piles.CardsOf(PileKind.Draw), piles.DrawCards);
});

test('ADrawThatRunsThePileOut_MarksWhereThePileWasMadeAgain', () => {
  let { writer, state } = Opened();
  for (let turn = 0; turn < 8; turn++) {
    const pileBefore = state.DrawPile.length;
    const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
    const events = writer.Write(begin.Events, begin.State);
    const draw = events.flatMap((e) => e.Cues).find((c) => c.Kind === CueKind.DrawHand) ?? null;
    const rebuilt = begin.Events.some((e) => e instanceof DeckReshuffled);
    assert.ok(draw != null);
    if (rebuilt) {
      assert.equal(draw.RebuildAfter, pileBefore, 'the cards left on the old pile come first');
      assert.ok(draw.Amount > pileBefore);
      const steps = EffectPlan.StepsOf(events.find((e) => e.Cues.includes(draw)));
      assert.ok(steps.map((s) => s.Id).includes(EffectId.PileRebuild));
      return;
    }
    assert.equal(draw.RebuildAfter, Cue.Unchanged);
    const end = TurnLoop.EndTurn(begin.State, NoShuffle());
    writer.Write(end.Events, end.State);
    state = end.State;
    assert.equal(state.Result, GameResult.Ongoing, 'the battle lasts until the pile runs out');
  }
  assert.fail('the 20-card deck never ran its draw pile out');
});

test('AtTheTurnEnd_TheHandIsInTheDiscardPile', () => {
  const { writer, state } = Opened();
  const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
  writer.Write(begin.Events, begin.State);
  const hand = begin.State.Hand.length;
  const end = TurnLoop.EndTurn(begin.State, NoShuffle());
  const written = writer.Write(end.Events, end.State);

  const lastFrame = last(written).After;
  AssertMatches(lastFrame.Piles, end.State);
  assert.equal(lastFrame.Piles.Discard, hand);
});
