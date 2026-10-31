// JS extension (docs/EXTENSIONS.md X-15; the C# screens have no such reading): a card's 習熟 as the exploration's
// cards print it. 習熟 renames a card (突き → 鋭い突き …), so a line that says 「0 → 1 段目」 next to the new name alone
// names a card the player never held; and the training card needs the 刻み toward the next 段 to be a choice at all.
// Every number comes from the MasteryBook and the card's MasteryProfile (BattleCore/Mastery.cs); nothing is decided here.
import { CardCatalog } from '../../core/battle/CardCatalog.js';
import { Mastery } from '../../core/battle/Mastery.js';

/** The card's name at `rank` in this life's book (the catalogue's name at 0, or for a card that never grows). */
export function nameAtRank(book, cardId, rank) {
  let def;
  try { def = CardCatalog.ById(cardId); } catch (e) { return cardId; }
  const profile = book && typeof book.ProfileOf === 'function' ? book.ProfileOf(cardId) : null;
  if (profile == null || !(rank > 0)) return def.Name;
  return Mastery.Master(def, rank, profile).Name;
}

/**
 * Where a card stands in its 習熟: { Rank, Ticks, Need, Grows, Top, Capped, Name, BaseName, NextName }.
 * Need is the 刻み the next 段 asks (the profile's threshold), null when no 段 can come: a card with no profile
 * (Grows false), the 3rd 段 (Top), or the 2nd 段 while another card holds the life's one 3rd 段 (Capped,
 * Constants.MasteryTopRankCards). NextName is the name the next 段 brings, null when the name stays.
 */
export function masteryProgress(book, cardId) {
  const rank = book.RankOf(cardId);
  const ticks = book.TicksOf(cardId);
  const profile = book.ProfileOf(cardId);
  const name = nameAtRank(book, cardId, rank);
  const base = nameAtRank(book, cardId, 0);
  const top = rank >= Mastery.TopRank;
  const holder = book.TopRankHolder;
  const capped = profile != null && !top && rank === Mastery.TopRank - 1 && holder != null && holder !== cardId;
  const open = profile != null && !top && !capped;
  const next = open ? nameAtRank(book, cardId, rank + 1) : null;
  return Object.freeze({
    Rank: rank,
    Ticks: ticks,
    Need: open ? profile.Thresholds[rank] : null,
    Grows: profile != null,
    Top: top,
    Capped: capped,
    Name: name,
    BaseName: base,
    NextName: next !== null && next !== name ? next : null,
  });
}
