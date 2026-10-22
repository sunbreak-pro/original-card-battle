// JS extension (docs/EXTENSIONS.md X-11; not in the C#, which has no inheritance yet — #106): what a 死亡地点の痕跡's
// candidates give *this* heir, for the trace card (before the choice, which is final: INHERITANCE R-10) and for the
// 受け取った遺産 card (after it). The candidate rows are Inheritance.traceNodeView's (the dead heir's 段 and 開示度);
// each row is printed with what this life ends up holding instead:
//   - a card: inheritPreview's 段, the same steps as the take (src/app/life.js Expedition.prototype.Inherit): a 3 段
//     card stops at 2 段 when the life already holds a 3 段 card (INHERITANCE 試作で決めたこと 7, R-11), a card already
//     held at that 段 adds no 段, and a copy joins the deck only while the deck has room (encounters_and_rewards_v4.md
//     §4.3 「デッキの上限の中ですぐ戦闘デッキに入ります」), else the card is owned only; the name is the one at that 段;
//   - an enemy page: the journal keeps the higher 開示度 (Inherit, Inheritance.mergeJournal), so a page adds only what
//     goes above what the journal already knows;
//   - a 走り書き the life already carries adds nothing (the router's list keeps each once);
//   - a dungeon page always adds one.
// After the take the card is written from what the take returned (`result`: Inherit's rows), not from a preview.
// Pure: reads the expedition's properties and its read-only DisclosureOf (src/app/recorder.js READ_ONLY), so a
// recorded expedition logs nothing; writes nothing.
import { CandidateKind, InheritanceRules, InheritanceText, mergeMastery } from '../../core/inheritance/Inheritance.js';
import { Constants } from '../../core/battle/Constants.js';
import { DeckRules } from '../../core/battle/DeckRules.js';
import { CardCatalog } from '../../core/battle/CardCatalog.js';
import { MasteryProfiles } from '../../core/battle/Mastery.js';

/**
 * What taking these cards from a trace would give this life, in the order given — the same steps as the game's
 * Expedition.prototype.Inherit (src/app/life.js): each card is owned and one copy joins the deck while the deck has
 * room (Expedition.Take: DeckRules.CanAdd under the life's cap), then the 段 go into the mastery book with the one-card
 * rule on 3 段 (Inheritance.mergeMastery). `cards` is [{ CardId, Rank }]; returns [{ CardId, Rank, From, To, Joins,
 * DeckFull, Owned }] — From / To the card's 段 in this life before and after, Joins whether a copy goes into the deck,
 * DeckFull whether the deck is at its cap when the card's turn comes, Owned whether the card was owned before.
 */
export function inheritPreview(expedition, cards) {
  const list = (cards ?? []).filter((c) => c && typeof c.CardId === 'string');
  const deck = [...expedition.Deck];
  const owned = new Set(expedition.Owned);
  const max = expedition.DeckMax;
  const book = expedition.Mastery;
  const rows = list.map((c) => {
    const had = owned.has(c.CardId);
    owned.add(c.CardId);
    const deckFull = deck.length >= max;
    const joins = DeckRules.CanAdd(deck, c.CardId, owned, { DeckMax: max });
    if (joins) deck.push({ Def: { Id: c.CardId } });
    return { CardId: c.CardId, Rank: c.Rank, From: book.RankOf(c.CardId), Joins: joins, DeckFull: deckFull, Owned: had };
  });
  const merged = mergeMastery(book.Records, list.map((c) => ({ CardId: c.CardId, Rank: c.Rank, Ticks: 0 })));
  return rows.map((r) => Object.freeze({ ...r, To: merged.get(r.CardId)?.Rank ?? r.From }));
}

/** An enemy page for this life: the journal's 開示度 now (From) and after the take (To: the higher of the two). */
export function pagePreview(expedition, enemyId, disclosure) {
  const from = enemyId != null && expedition && typeof expedition.DisclosureOf === 'function' ? expedition.DisclosureOf(enemyId) : 0;
  return Object.freeze({ EnemyId: enemyId, Disclosure: disclosure, From: from, To: Math.max(from, disclosure) });
}

/** The words (Japanese UI text; numbers passed in). `done` is the 受け取った遺産 card's past tense. */
export const TraceGainText = Object.freeze({
  /** The one-card rule on 3 段 stopped the card (INHERITANCE 試作で決めたこと 7). */
  Capped: (to) => `3 段目の札がこの生にあるので ${to} 段目止まり`,
  Now: (from) => `いまも ${from} 段目`,
  Already: (from) => `すでに ${from} 段目`,
  Up: (from, to) => (from > 0 ? `いま ${from} 段目 → ${to} 段目` : null),
  RankUp: (from, to) => `習熟 ${from} → ${to} 段目`,
  DeckCap: (max) => `デッキが上限 ${max} 枚`,
  Copies: (n) => `デッキに同じ札が ${n} 枚`,
  OwnedOnly: (why, done) => `所持カードにだけ${done ? '入った' : '入る'}${why ? `（${why}）` : ''}`,
  Joined: 'デッキに 1 枚入った',
  /** Before the choice, for a card that adds no 段: the copy is all it gives. */
  Joins: 'デッキに 1 枚入る',
  Nothing: (why, done) => `この生では何も増え${done ? 'なかった' : 'ない'}（${why}）`,
  /** Above the trace card's list when the deck is already at its cap and the trace holds cards. */
  DeckFullLead: (max) => `デッキが上限（${max} 枚）なので、受け取った札はデッキに入らず、所持カードにだけ入る。`,
  /** A card row printed at this heir's 段 says what the trace held, when that differs. */
  InTrace: (name, rank) => `痕跡では ${name}（${rank} 段目）`,
  // enemy pages: the journal keeps the higher 開示度
  PageUp: (from, to) => (from > 0 ? `いま 開示度 ${from} → ${to}` : null),
  PageRaised: (from, to) => `開示度 ${from} → ${to}`,
  PageNow: (from) => `いまも 開示度 ${from} / ${InheritanceRules.MaxDisclosure}`,
  PageAlready: (from) => `すでに 開示度 ${from} / ${InheritanceRules.MaxDisclosure}`,
  PageInTrace: (level) => `痕跡では 開示度 ${level} / ${InheritanceRules.MaxDisclosure}`,
  // 走り書き: each is kept once
  ScribbleHeld: 'すでに手記にある',
});

/** The card's name at a 段 (習熟 renames a card: 突き → 鋭い突き …), else its catalogue name, else null. */
export function cardNameAt(cardId, rank) {
  const profile = MasteryProfiles.Find(MasteryProfiles.All, cardId);
  const grown = profile && rank > 0 ? profile.NameAt(rank) : null;
  if (grown) return grown;
  try { return CardCatalog.ById(cardId).Name; } catch (e) { return null; }
}

/** The card a candidate row stands for ({ CardId, Rank }), or null for a page or a 走り書き. */
export function candidateCard(c) {
  if (!c || c.Kind !== CandidateKind.Card || typeof c.Id !== 'string' || !c.Id.startsWith('card:')) return null;
  const rank = Number.isInteger(c.Rank) ? c.Rank : [1, 2, 3].find((r) => InheritanceText.RankDetail(r) === c.Detail) ?? null;
  return rank === null ? null : { CardId: typeof c.CardId === 'string' ? c.CardId : c.Id.slice('card:'.length), Rank: rank };
}

/** The enemy page a candidate row stands for ({ EnemyId, Disclosure }), or null. */
export function candidatePage(c) {
  if (!c || c.Kind !== CandidateKind.EnemyPage || typeof c.Id !== 'string' || !c.Id.startsWith('enemy:')) return null;
  const level = Number.isInteger(c.Disclosure) ? c.Disclosure
    : [1, 2].find((l) => InheritanceText.DisclosureDetail(l) === c.Detail) ?? null;
  return level === null ? null : { EnemyId: typeof c.EnemyId === 'string' ? c.EnemyId : c.Id.slice('enemy:'.length), Disclosure: level };
}

const sameScribble = (a, b) => !!a && !!b && a.Generation === b.Generation && a.Text === b.Text;

/**
 * What each candidate gives this heir, as a Map candidateId → { Name, Detail, Notes: string[], Nothing: boolean,
 * Result }. Name and Detail are the row as this life will hold it (the card's name and 段 after the take, the enemy
 * page's 開示度 after it); Notes say what the row alone does not (what the trace held when it differs, no 段 gained, a
 * capped 段, a copy that cannot join the deck, a page or a 走り書き the journal already has); Nothing marks a pick that
 * gives this life nothing at all.
 * `picks` (candidate ids, in pick order) are tried together, in that order — the deck's room is shared and the
 * one-card rule on 3 段 counts every pick; each card not picked is tried as the next pick after them.
 * opts: `done` writes the 受け取った遺産 card's lines (past tense; every picked card with what it gave); `result` is
 * what the take returned (Expedition.prototype.Inherit's { Cards, Enemies } plus the router's { Scribbles }), which
 * the done lines are then written from; `scribbles` the 走り書き the life carries before the take.
 */
export function traceGains(expedition, candidates, picks = [], { done = false, result = null, scribbles = [] } = {}) {
  const out = new Map();
  if (!expedition || !expedition.Mastery) return out;
  const cards = new Map();
  for (const c of candidates ?? []) {
    const card = candidateCard(c);
    if (card) cards.set(c.Id, card);
  }
  const byId = new Map((candidates ?? []).map((c) => [c.Id, c]));
  const picked = (picks ?? []).filter((id) => cards.has(id));
  const pickedCards = picked.map((id) => cards.get(id));
  const deckNoted = deckIsFull(expedition) && !done; // the card's lead says it once (DeckFullLead)
  const taken = result && Array.isArray(result.Cards) ? result.Cards : null;
  const preview = taken && done ? picked.map((id, i) => taken.find((r) => r.CardId === cards.get(id).CardId) ?? inheritPreview(expedition, pickedCards)[i])
    : inheritPreview(expedition, pickedCards);
  picked.forEach((id, i) => out.set(id, cardGain(byId.get(id), preview[i], expedition.DeckMax, done, deckNoted)));
  if (!done) {
    for (const [id, card] of cards) {
      if (out.has(id)) continue;
      const rows = inheritPreview(expedition, [...pickedCards, card]);
      out.set(id, cardGain(byId.get(id), rows[rows.length - 1], expedition.DeckMax, done, deckNoted));
    }
  }
  const held = Array.isArray(scribbles) ? scribbles : [];
  for (const c of candidates ?? []) {
    if (out.has(c.Id)) continue;
    if (done && !(picks ?? []).includes(c.Id)) continue;
    const page = candidatePage(c);
    if (page) {
      const real = done && result && Array.isArray(result.Enemies) ? result.Enemies.find((r) => r.EnemyId === page.EnemyId) : null;
      out.set(c.Id, pageGain(c, real ?? pagePreview(expedition, page.EnemyId, page.Disclosure), done));
    } else if (c.Kind === CandidateKind.Scribble) {
      const mine = { Generation: c.Generation, Text: c.Text };
      const real = done && result && Array.isArray(result.Scribbles) ? result.Scribbles.find((s) => sameScribble(s, mine)) : null;
      const already = real ? !real.Added : held.some((s) => sameScribble(s, mine));
      out.set(c.Id, Object.freeze({
        Name: c.Name, Detail: c.Detail || '',
        Notes: Object.freeze(already ? [TraceGainText.Nothing(TraceGainText.ScribbleHeld, done)] : []),
        Nothing: already, Result: null,
      }));
    } else {
      out.set(c.Id, Object.freeze({ Name: c.Name, Detail: c.Detail || '', Notes: Object.freeze([]), Nothing: false, Result: null }));
    }
  }
  return out;
}

/** Whether the deck is at its cap now (the trace card's lead says so once, above the list). */
export function deckIsFull(expedition) {
  return !!expedition && expedition.Deck.length >= expedition.DeckMax;
}

/**
 * A card row at this heir's 段. Before the choice the line says only what the row does not (no 段 gained, a capped
 * 段, no copy for the deck — the deck's cap left out when the card's lead already says it: `deckNoted`); after it,
 * what the card gave. When the 段 printed is not the trace's, a first line says what the trace held.
 */
function cardGain(c, r, deckMax, done, deckNoted = false) {
  const gained = r.To > r.From;
  const capped = r.To < r.Rank;
  const nothing = !gained && !r.Joins && r.Owned;
  const deckWhy = r.Joins ? null : r.DeckFull ? (deckNoted ? null : TraceGainText.DeckCap(deckMax)) : TraceGainText.Copies(Constants.CopiesMax);
  let line;
  if (nothing) {
    const rankWhy = capped ? TraceGainText.Capped(r.To) : done ? TraceGainText.Already(r.From) : TraceGainText.Now(r.From);
    line = TraceGainText.Nothing([rankWhy, deckWhy].filter(Boolean).join('・'), done);
  } else {
    const parts = [];
    const why = capped ? `（${TraceGainText.Capped(r.To)}）` : '';
    if (done) {
      parts.push((gained ? TraceGainText.RankUp(r.From, r.To) : TraceGainText.Already(r.From)) + why);
      parts.push(r.Joins ? TraceGainText.Joined : TraceGainText.OwnedOnly(deckWhy, true));
    } else {
      if (!gained) parts.push(TraceGainText.Now(r.From) + why);
      else if (capped) parts.push(TraceGainText.Capped(r.To));
      else if (TraceGainText.Up(r.From, r.To)) parts.push(TraceGainText.Up(r.From, r.To));
      // a card that adds no 段 still gives its copy, when the deck has room: say so, or 「いまも 2 段目」 reads as nothing
      if (!r.Joins) parts.push(TraceGainText.OwnedOnly(deckWhy, false));
      else if (!gained) parts.push(TraceGainText.Joins);
    }
    line = parts.join('・');
  }
  const name = cardNameAt(r.CardId, r.To) ?? (c ? c.Name : r.CardId);
  const traceName = c ? c.Name : name;
  const notes = [];
  if (r.To !== r.Rank) notes.push(TraceGainText.InTrace(traceName, r.Rank));
  if (line) notes.push(line);
  return Object.freeze({ Name: name, Detail: InheritanceText.RankDetail(r.To), Notes: Object.freeze(notes), Nothing: nothing, Result: r });
}

/** An enemy page row at this life's 開示度 (the journal keeps the higher one). */
function pageGain(c, r, done) {
  const nothing = r.To <= r.From;
  const notes = [];
  if (r.To !== r.Disclosure) notes.push(TraceGainText.PageInTrace(r.Disclosure));
  if (nothing) notes.push(TraceGainText.Nothing(done ? TraceGainText.PageAlready(r.From) : TraceGainText.PageNow(r.From), done));
  else if (done) notes.push(TraceGainText.PageRaised(r.From, r.To));
  else if (TraceGainText.PageUp(r.From, r.To)) notes.push(TraceGainText.PageUp(r.From, r.To));
  return Object.freeze({ Name: c.Name, Detail: InheritanceText.DisclosureDetail(r.To), Notes: Object.freeze(notes), Nothing: nothing, Result: r });
}
