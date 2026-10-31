// Helpers shared by the Bridge tests: the private helpers of ExpeditionTests.cs that end a fight without
// playing it out (Ended / EndEveryFight), drive one with the suggested cards (Drive), and build a
// hand-in (HandIn).
import assert from 'node:assert/strict';
import { PrototypeDeck, CardCatalog } from '../../src/core/battle/CardCatalog.js';
import { Constants } from '../../src/core/battle/Constants.js';
import { Departure } from '../../src/core/battle/DeckRules.js';
import { CardPlayed } from '../../src/core/battle/BattleEvents.js';
import { Actor, CardInstance } from '../../src/core/battle/Types.js';
import { BattleHandIn } from '../../src/bridge/exploration/EncounterBattle.js';

export function HandIn(encounter, kind, levels = null, cells = 7) {
  const deck = PrototypeDeck.Build();
  let disclosure = levels;
  if (disclosure == null) {
    disclosure = new Map();
    for (const id of encounter.EnemyIds) if (!disclosure.has(id)) disclosure.set(id, 0);
  }
  return new BattleHandIn(
    2, kind, encounter, encounter.Fights.map(() => cells),
    Constants.PlayerMaxHp, Constants.PlayerMaxHp, Constants.BaseMaxStamina, Constants.BaseMaxStamina, 0,
    disclosure, deck, Departure.Plain(deck), 11);
}

/** The source's state as a battle that ended this way: the end the core would report, without playing it out. */
export function Ended(source, result, hp, stamina, plays = null) {
  const state = source.State;
  let history = state.History;
  for (const play of plays ?? []) history = history.WithMasteryPlay(play);
  return state.with({ Result: result, Player: state.Player.with({ Hp: hp, Stamina: stamina }), PlayerHistory: history });
}

/** Ends every fight of the encounter this way. -1 keeps the HP and stamina the fight started with. */
export function EndEveryFight(battle, result, { hp = -1, stamina = -1, observe = false, plays = null } = {}) {
  while (!battle.Over) {
    const source = battle.StartNext();
    const player = source.State.Player;
    const events = [...source.History];
    if (observe) events.push(new CardPlayed(Actor.Player, new CardInstance('observe-0', CardCatalog.Observe), 3));
    battle.Finish(Ended(source, result, hp < 0 ? player.Hp : hp, stamina < 0 ? player.Stamina : stamina, plays), events);
  }
}

/**
 * Plays the source's suggested cards until the battle ends, as the unattended runs do. The C#
 * looks the card's aim up with DepictionText.Find / RequiredZone; here the card face's aim is read
 * through the same Depiction module when it is there.
 */
export async function Drive(source) {
  const { DepictionText, PlayVerdict } = await import('../../src/bridge/depictionScript/DepictionTypes.js');
  let guard = 0;
  while (!source.Finished && guard++ < 2000) {
    if (!source.WaitingForPlayer) {
      source.AdvanceAuto();
      continue;
    }
    const card = source.SuggestedCardId;
    if (card.length === 0) {
      source.EndTurn();
      continue;
    }
    const face = DepictionText.Find(source.Frame.Hand, card);
    // C#: TryPlay(card, zone, out _) → the port returns { ok: verdict, value: played, verdict, played }.
    const { verdict } = source.TryPlay(card, DepictionText.RequiredZone(face.Aim));
    assert.equal(verdict, PlayVerdict.Accepted);
  }
  assert.equal(source.Finished, true);
}
