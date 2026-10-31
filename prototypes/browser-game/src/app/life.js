// One life of the lineage as the ported cores see it. The C# stops at a single run (Expedition); the
// lineage rules are the trial module's (src/core/inheritance). This file is the seam between them:
//   - the life's start is plain data (seed, deck by instance, tools, journal) so a save can rebuild it;
//   - each layer's map gets the lineage's trace node and drops the carving once it is known
//     (Inheritance.mapSpecFor), frozen at the start of the life so a replay draws the same maps;
//   - what a trace gives (cards with their 段, pages, enemy 開示度) goes into the Expedition through
//     Expedition.prototype.Inherit, a call the recorder logs like any other.
import { CardCatalog } from '../core/battle/CardCatalog.js';
import { CardMastery } from '../core/battle/Mastery.js';
import { CardInstance } from '../core/battle/Types.js';
import { MapGenerator } from '../core/dungeon/MapGenerator.js';
import { NodeKind } from '../core/dungeon/NodeKind.js';
import { RunLoadout } from '../core/dungeon/RunLoadout.js';
import { DeckLimits } from '../core/battle/DeckRules.js';
import { Expedition } from '../bridge/exploration/Expedition.js';
import { toRunLoadout, runLoadoutToData, runLoadoutFromData } from '../bridge/exploration/Loadouts.js';
import { RunStart } from '../bridge/explorationScript/ExplorationSession.js';
import * as Inh from '../core/inheritance/Inheritance.js';
import { JournalIntel } from './intel.js';

// ------------------------------------------------------------------ the start as data

/**
 * The departure's choice as plain data: the deck by instance id and kind (instance ids and order are
 * what the battles shuffle, so they are kept exactly), the tools that act in battle, the exploration
 * loadout as relief items, and the talent. The departure hands a DungeonContent Loadout (catalogue
 * ItemDefs, no relief Kind); the exploration reads it as the RunLoadout toRunLoadout makes of it, with
 * the life's tool slots (`toolSlots`: 4 with the survivor's bonus). A RunLoadout is kept as it is.
 */
export function choiceToData(choice, toolSlots = null) {
  const deck = (choice.deck || []).map((c) => [c.InstanceId, c.Def.Id]);
  const lo = choice.loadout == null ? RunLoadout.Empty
    : choice.loadout instanceof RunLoadout ? choice.loadout : toRunLoadout(choice.loadout, toolSlots);
  return {
    deck,
    battleToolIds: [...(choice.battleToolIds || [])],
    loadout: runLoadoutToData(lo),
    talent: choice.talent ?? null,
    toolIds: [...(choice.toolIds || [])],
    consumableIds: [...(choice.consumableIds || [])],
  };
}

function deckFromData(rows) {
  return rows.map(([iid, defId]) => new CardInstance(iid, CardCatalog.ById(defId)));
}
function loadoutFromData(lo) {
  return runLoadoutFromData(lo);   // a save without `trial` reads as the C#'s RunLoadout
}

/**
 * The saved start of a life, from Inheritance.startLife's result and the departure's choice.
 * Everything a replay needs and nothing that changes as the lineage moves on.
 */
export function makeStart(lifeStart, choice) {
  return {
    v: 1,
    generation: lifeStart.Life.Generation,
    name: lifeStart.Life.Name,
    seed: String(lifeStart.Seed),
    toolSlots: lifeStart.ToolSlots,
    deckMax: lifeStart.DeckMax,
    survivorBonus: !!lifeStart.SurvivorBonus,
    traceLayers: [...lifeStart.TraceLayers],
    carvingKnown: !!lifeStart.CarvingKnown,
    // The journal the heir carries (INHERITANCE.md R-13): a survivor's whole journal, its memo turned into
    // that survivor's 走り書き so the heir starts on a sheet of their own (Inheritance.heirJournal).
    journal: JSON.parse(JSON.stringify(Inh.heirJournal(lifeStart.Journal, lifeStart.JournalFrom ?? null))),
    choice: choiceToData(choice, lifeStart.ToolSlots),
  };
}

/** The rare pages the heir starts with (a survivor's whole journal, R-13), as the count Expedition keeps. */
export function startPages(start) {
  const p = start && start.journal ? start.journal.Pages : 0;
  if (Array.isArray(p)) return p.length;
  return Number.isInteger(p) && p > 0 ? p : 0;
}

/**
 * The 走り書き of a life: the ones its start carries (a survivor's journal: start.journal.Scribbles) and
 * the ones the life holds already (`saved`: the save's list, which a taken trace adds to), each once. A
 * start saved before the memo became a 走り書き (an older build's makeStart) still has the survivor's memo
 * in start.journal.Memo: it is read as that survivor's 走り書き, named from the lineage, so a save made
 * then does not lose it (R-13).
 */
export function lifeScribbles(start, saved, lineage = null) {
  const j = (start && start.journal) || {};
  const out = [];
  const add = (s) => {
    if (!s || typeof s.Text !== 'string') return;
    if (!out.some((t) => t.Generation === s.Generation && t.Text === s.Text)) out.push({ Generation: s.Generation, Name: s.Name, Text: s.Text });
  };
  (Array.isArray(j.Scribbles) ? j.Scribbles : []).forEach(add);
  if (typeof j.Memo === 'string' && j.Memo.trim() && Number.isInteger(start.generation) && start.generation > 1) {
    const gen = start.generation - 1;
    const prev = lineage && Array.isArray(lineage.Lives) ? lineage.Lives.find((l) => l.Generation === gen) : null;
    add({ Generation: gen, Name: prev ? prev.Name : '', Text: j.Memo });
  }
  (Array.isArray(saved) ? saved : []).forEach(add);
  return out;
}

// ------------------------------------------------------------------ inheritance into a running life

/**
 * What a trace gave, taken into this life (INHERITANCE.md R-11): each card is owned (and one copy
 * joins the deck when the deck has room, as a learned card does), its 段 goes into the mastery book
 * (keeping the one-card rule on 3 段), the enemy pages raise 開示度, the dungeon pages count.
 * `taken` is Inheritance.takeTrace(...).Taken as plain data. App-side extension of the port: the C#
 * has no inheritance yet (#106).
 *
 * Returns what the take did, for the 受け取った遺産 card (TraceGain writes it from this, not from a preview):
 * { Cards: [{ CardId, Rank, From, To, Joins, DeckFull, Owned }] — the 段 before and after, whether a copy
 * joined the deck (Expedition.Take's answer), whether the deck was at its cap, whether the card was owned
 * already —, Enemies: [{ EnemyId, Disclosure, From, To }] — the journal's 開示度 before and after —,
 * Pages: n }. The pages count as inherited (InheritedPages), not as found this life.
 */
Expedition.prototype.Inherit = function Inherit(taken) {
  this.SyncLife();
  const cards = (taken && taken.Cards) || [];
  const max = this.DeckMax;
  const rows = cards.map((c) => {
    const owned = this._owned.has(c.CardId);
    const deckFull = this._deck.length >= max;
    const from = this.Mastery.RankOf(c.CardId);
    const joins = this.Take(c.CardId) === true;
    return { CardId: c.CardId, Rank: c.Rank, From: from, Joins: joins, DeckFull: deckFull, Owned: owned };
  });
  if (cards.length) {
    const records = Inh.mergeMastery(this.Mastery.Records, cards, (id, ticks, rank) => new CardMastery(id, ticks, rank));
    this.Mastery = this.Mastery.with({ Records: records });
  }
  const j = (taken && taken.Journal) || {};
  const disclosure = j.Disclosure || {};
  const enemies = [];
  for (const id of Object.keys(disclosure)) {
    const now = this._journal.get(id) || 0;
    if (disclosure[id] > now) this._journal.set(id, disclosure[id]);
    enemies.push(Object.freeze({ EnemyId: id, Disclosure: disclosure[id], From: now, To: Math.max(now, disclosure[id]) }));
  }
  const pages = Array.isArray(j.Pages) ? j.Pages.length : (j.Pages || 0);
  this._pages += pages;
  this.InheritedPages = (this.InheritedPages || 0) + pages;
  this.InheritedTraceId = taken ? taken.TraceId : null;
  this.InheritedLayer = this.Session.State.Profile.Layer;
  return Object.freeze({
    Cards: Object.freeze(rows.map((r) => Object.freeze({ ...r, To: this.Mastery.RankOf(r.CardId) }))),
    Enemies: Object.freeze(enemies),
    Pages: pages,
  });
};

/**
 * The 走り書き of a life after a take: `held` with each of `added` that it does not carry yet (the same
 * Generation and Text: lifeScribbles keeps each once). Returns { list, rows } — rows [{ Generation, Name,
 * Text, Added }] say which of `added` were new, for the 受け取った遺産 card.
 */
export function addScribbles(held, added) {
  const list = (Array.isArray(held) ? held : []).slice();
  const rows = [];
  for (const s of Array.isArray(added) ? added : []) {
    if (!s || typeof s.Text !== 'string') continue;
    const dup = list.some((t) => t.Generation === s.Generation && t.Text === s.Text);
    if (!dup) list.push({ Generation: s.Generation, Name: s.Name, Text: s.Text });
    rows.push({ Generation: s.Generation, Name: s.Name, Text: s.Text, Added: !dup });
  }
  return { list, rows };
}

// ------------------------------------------------------------------ the life

/**
 * Builds the Expedition of a life from its saved start. `life.traceTaken` is read by the map hook,
 * so a trace taken on layer 2 keeps the trace node off layers 3〜7 of the same life (R-09), in the
 * first run and in the replay alike.
 */
export function lifeSetup(start) {
  const c = start.choice;
  const life = { traceTaken: false };
  const traceLayers = new Set(start.traceLayers || []);
  const mapFor = (profile, seed) => {
    let spec = profile.MapSpec;
    if (start.carvingKnown && spec.Quotas.has(NodeKind.Carving)) spec = spec.With(NodeKind.Carving, 0);
    if (!life.traceTaken && traceLayers.has(profile.Layer)) spec = spec.With(NodeKind.Trace, 1);
    return MapGenerator.Generate(spec, seed);
  };
  const runStart = new RunStart({ Seed: BigInt(start.seed), Loadout: loadoutFromData(c.loadout), MapFor: mapFor });
  // The journal the heir set out with (INHERITANCE.md R-13: a survivor's whole journal): its 開示度 go to
  // the Expedition's journal, its rare pages to the Expedition's page count (below).
  const journal = start.journal && start.journal.Disclosure ? start.journal.Disclosure : {};
  const intel = new JournalIntel();
  // The life's deck limits (docs/EXTENSIONS.md 「出立の枠」: a survivor's heir may hold 45 cards), rebuilt from
  // the saved start so a replay holds the deck to the same cap. A life without the bonus: the C#'s, no option.
  const limits = DeckLimits.From({ DeckMax: start.deckMax, ToolSlots: start.toolSlots });
  // The game plays with the document rules the C# has not got yet (docs/EXTENSIONS.md: items, events, the
  // survey): trial on. Tests and the parity oracle build Expeditions without it and get the C#.
  const options = { trial: true, limits: limits.IsDefault ? null : limits };
  const expedition = new Expedition(runStart, deckFromData(c.deck), c.battleToolIds, c.talent, intel, journal, options);
  intel.bind(expedition);
  // The pages the heir set out with count among the life's pages from the start (the C# Expedition starts
  // every life at 0 and has no journal of pages yet, #107), so the map's chip, the life's journal at its
  // end and the trace it leaves all hold them. Kept when the Expedition begins its life again. They are
  // inherited, not found (InheritedPages): the map lists them apart from 拾った物 (INHERITANCE.md R-27).
  const pages = startPages(start);
  expedition.InheritedPages = pages;
  if (pages > 0) {
    expedition._pages += pages;
    const begin = expedition.BeginLife;
    expedition.BeginLife = function BeginLife() { begin.call(this); this._pages += pages; this.InheritedPages = pages; };
  }
  const inherit = expedition.Inherit;
  expedition.Inherit = function (taken) { life.traceTaken = true; return inherit.call(this, taken); };
  return { expedition, life };
}
