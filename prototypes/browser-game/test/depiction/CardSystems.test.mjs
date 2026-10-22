// Port of the CardSystems cases of unity-project-kit/Assets/View/Depiction/Bridge/Tests/EffectGrammarTests.cs
// (battle_ui_ux_v2 §5.3, #57: which of the eight systems each card and each enemy action plays as).
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { CardSystems } from '../../src/bridge/depiction/CardSystems.js';
import { CoreScriptWriter } from '../../src/bridge/depiction/CoreScriptWriter.js';
import { CoreText } from '../../src/bridge/depiction/CoreText.js';
import { CueKind, DepictionEventKind, UnitSide } from '../../src/bridge/depictionScript/DepictionTypes.js';
import { EffectGrammar, FxSystem } from '../../src/bridge/depictionScript/EffectGrammar.js';
import { EffectStrength } from '../../src/bridge/depictionScript/Effects.js';
import { CardCatalog } from '../../src/core/battle/CardCatalog.js';
import { Cards } from '../../src/core/battle/Cards.js';
import { Enemies } from '../../src/core/battle/Enemies.js';
import { BattleSetup, PlayRefusal, TurnLoop } from '../../src/core/battle/TurnLoop.js';
import { FixedRng } from '../../src/core/battle/IRng.js';
import { enumIndex } from '../../src/shared/cs.js';

const NoShuffle = () => new FixedRng(0.9999999);

/** The cards whose faces only move the player: §5.3 gives a move no system. */
function MovesOnly(def) {
  const faces = CardSystems.FacesOf(def.Name, def.Attributes, def.Face);
  return !faces.Attacks && !faces.Guards && !faces.PutsStatus && !faces.Breathes && !faces.Stance;
}

// ---- the table ----

test('EveryCardTheCoreKnows_HasARow_AndOnlyAMoveHasNoSystem', () => {
  for (const def of CardCatalog.All) {
    assert.ok(CardSystems.Table.has(def.Id), def.Id + ' has no row');
    const row = CardSystems.Table.get(def.Id);
    if (MovesOnly(def)) {
      assert.deepEqual([...row], [], def.Name + ' only moves: a move has no system (§5.3)');
      assert.notEqual(def.Face.Move, 0, def.Name + ' has no face at all');
    } else {
      assert.ok(row.length > 0, def.Name + ' plays no system');
    }
    assert.ok(row.length <= 2, def.Name + ': a card has at most two faces (§5.4)');
    assert.equal(new Set(row).size, row.length, def.Name + ' repeats a system');
  }
  assert.equal(CardSystems.Table.size, CardCatalog.All.length, 'a row for a card the core does not know');
});

test('TheTable_IsReadOnly_AndBuiltOnce', () => {
  const row = CardSystems.Table.get('thrust');
  assert.ok(Object.isFrozen(row));
  assert.equal(CardSystems.Table, CardSystems.Table);
  assert.equal(CardSystems.Of(CardCatalog.Thrust), row, 'a card the table holds plays its row');
});

test('TheEightSystems_AreAllPlayedByTheSwordsmansCards', () => {
  const played = new Set([...CardSystems.Table.values()].flatMap((row) => [...row]));
  assert.deepEqual([...played].sort(), [...EffectGrammar.Systems].sort(), 'every system has a card that shows it');
});

for (const [id, expected] of [
  ['thrust', FxSystem.Thrust], // 突き
  ['kesa_cut', FxSystem.Slash], // 袈裟斬り: no other word, so 斬
  ['side_sweep', FxSystem.Sweep], // 横薙ぎ
  ['flat_strike', FxSystem.Strike], // 峰打ち
  ['pierce', FxSystem.Thrust], // 貫き
  ['throw_blade', FxSystem.Thrust], // 投げ
  ['stone_throw', FxSystem.Strike], // 石礫: 礫 is a 打 word
  ['boar_rush', FxSystem.Thrust], // 猪突猛進: 突 is read first
  ['lunge', FxSystem.Slash], // 踏み込み斬り
  ['haul_step', FxSystem.Strike], // 手繰りの歩み: a pull is 打 before any word
]) {
  test(`AnAttack_TakesItsSystemFromItsName(${id}, ${expected})`, () => {
    const def = CardCatalog.ById(id);
    assert.equal(CardSystems.Of(def)[0], expected, def.Name);
  });
}

test('ACardTheTableLacks_IsReadOffItsOwnFaces', () => {
  const renamed = CardCatalog.KesaCut.with({ Id: 'not_in_the_table', Name: '突き直し' });
  assert.deepEqual([...CardSystems.Of(renamed)], [FxSystem.Thrust]);
  assert.deepEqual(CardSystems.Of(null), []);
});

test('EveryAttackCard_LeadsWithTheBlowItsNameAndPushGive_TheOtherFacesFollowInResolveOrder', () => {
  for (const def of CardCatalog.All) {
    const row = CardSystems.Of(def);
    const faces = CardSystems.FacesOf(def.Name, def.Attributes, def.Face);
    if (faces.Attacks) {
      assert.equal(row[0], EffectGrammar.BlowOf(def.Name, def.Face.Push !== 0), def.Name);
      assert.equal(EffectGrammar.ToStrike(row[0]), CoreText.SystemOf(def), def.Name + ': the blow\'s shape and its system agree');
    }
    for (let i = 1; i < row.length; i++) {
      assert.equal(EffectGrammar.IsBlow(row[i]), false, def.Name + ': one blow, and it leads');
      assert.ok(enumIndex(FxSystem, row[i]) > enumIndex(FxSystem, row[i - 1]), def.Name + ': the faces keep their resolve order');
    }
    if (faces.Stance) assert.deepEqual([...row], [FxSystem.Form], def.Name + ': a stance card has no other face');
  }
});

test('EveryEnemyAction_HasASystemOrOnlyMoves', () => {
  for (const enemy of Enemies.All) {
    for (const action of enemy.Actions.values()) {
      const row = CardSystems.Of(action);
      const faces = CardSystems.FacesOf(action.Name, action.Attributes, action.Face);
      if (faces.Attacks) assert.equal(EffectGrammar.ToStrike(row[0]), CoreText.SystemOf(action), enemy.Id + ' ' + action.Name);
      if (row.length === 0) assert.equal(faces.Attacks || faces.Guards || faces.Stance, false, enemy.Id + ' ' + action.Name);
    }
  }
});

test('AnEnemyAction_IsReadByTheSameRuleAsACard_EvenWithACardsId', () => {
  // Overload resolution by type: an action whose id is a card's is still read off its own faces.
  const shove = Enemies.PolearmWarped.Actions.get('shove');
  const asCardId = shove.with({ Id: 'thrust' });
  assert.deepEqual([...CardSystems.Of(asCardId)], EffectGrammar.Of(CardSystems.FacesOf(shove.Name, shove.Attributes, shove.Face)));
});

// ---- the writer ----

/** Plays `card` on the slice's polearm, from the first gap that lets it be played. */
function Play(card) {
  for (let gap = 0; gap <= 4; gap++) {
    const setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck([card], 6), BattleSetup.SliceFieldCells, { StartGap: gap });
    const state = TurnLoop.Start(setup, NoShuffle()).State;
    const writer = new CoreScriptWriter(setup.Enemy);
    writer.Opening(state);
    const begin = TurnLoop.BeginPlayerTurn(state, NoShuffle());
    writer.Write(begin.Events, begin.State);
    const id = begin.State.Hand[0].InstanceId;
    if (TurnLoop.CanPlay(begin.State, id) !== PlayRefusal.None) continue;
    const play = TurnLoop.PlayCard(begin.State, id, NoShuffle());
    return writer.Write(play.Events, play.State).find((e) => e.Kind === DepictionEventKind.PlayCard);
  }
  return null;
}

test('ThePlayedCard_CarriesItsRow_AndItsBeatsTheirSystems', () => {
  let played = 0;
  for (const def of CardCatalog.All) {
    const ev = Play(def);
    if (ev == null) continue;
    played += 1;
    assert.deepEqual(ev.Systems, [...CardSystems.Table.get(def.Id)], def.Name);
    for (const cue of ev.Cues) {
      if (cue.Kind === CueKind.Slash && cue.Source === UnitSide.Player) {
        assert.equal(cue.Fx, ev.Systems[0], def.Name + ': the blow is the card\'s');
      }
      if (cue.Fx != null) assert.equal(cue.Intensity, EffectStrength.Uniform, def.Name + ': one strength (#30)');
      if (cue.Kind === CueKind.GuardGain && cue.Target === UnitSide.Player) {
        assert.equal(cue.Fx, FxSystem.Shield, def.Name + ': a Guard gained in a play is 盾');
      }
      if (cue.Kind === CueKind.StatusChange && cue.Amount > 0) {
        assert.equal(cue.Fx, FxSystem.Spirit, def.Name + ': a status put on is 気');
      }
      if (cue.Kind === CueKind.StaminaChange) {
        assert.equal(cue.Fx, cue.Amount > 0 ? FxSystem.Breath : null, def.Name + ': a stamina point is 息, the cost is nothing');
      }
    }
  }
  assert.ok(played > 0);
});
