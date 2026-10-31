// What the inheritance module needs from a finished life that the ported C# does not carry as one
// value: the journal (開示度 by enemy, the rare pages, the memo and inherited 走り書き) and the display
// names stored in a trace, so the hall can draw it without the catalogues.
import { CardCatalog } from '../core/battle/CardCatalog.js';
import { Enemies } from '../core/battle/Enemies.js';
import { MasteryProfiles } from '../core/battle/Mastery.js';
import { RunPhase } from '../core/dungeon/ExplorationState.js';
import { UniqueCards } from '../bridge/exploration/UniqueCards.js';

export function lifeJournal(expedition, live) {
  const disclosure = {};
  for (const [id, d] of expedition._journal) if (d > 0) disclosure[id] = d;
  return {
    Disclosure: disclosure,
    Pages: expedition.Pages,
    Memo: (live && live.memo) || '',
    Scribbles: (live && live.scribbles) || [],
    CarvingTaken: !!expedition.Session.CarvingTaken,
  };
}

/** The card's name at its 段 (習熟 renames a card: 突き → 伸び突き …), else its catalogue name. */
export function cardName(cardId, rank = 0) {
  const profile = MasteryProfiles.Find(MasteryProfiles.All, cardId);
  const grown = profile ? profile.NameAt(rank) : null;
  if (grown) return grown;
  try { return CardCatalog.ById(cardId).Name; } catch (e) { return cardId; }
}

export function enemyName(enemyId) {
  try { return Enemies.ById(enemyId).Name; } catch (e) { return enemyId; }
}

/** 歪みの根's three cards: they pass only as legacy (roster §6.6 / §7.4, INHERITANCE.md 試作 10). */
function rootCards() {
  return [...UniqueCards.OfBoss('distortion_root')];
}

export function endOptions(_expedition, state) {
  const opts = { cardName, enemyName };
  if (state.Phase === RunPhase.Completed) opts.RootCards = rootCards();
  return opts;
}
