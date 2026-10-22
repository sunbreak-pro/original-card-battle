// No C# counterpart: 生・死・継承 (concept-v3.md §8 / §14 / §16) is not implemented in unity-port yet
// (#104 出立, #106 痕跡の中身, #108 生存者). This module is designed from the documents; every rule
// and every 試作で決めたこと is listed with its source in docs/INHERITANCE.md.
//
// Pure and deterministic: no DOM, no clock, no Math.random. A lineage is a plain, deep-frozen,
// JSON-safe object; every function returns a new one and never touches its input.
import { defineEnum, compareOrdinal } from '../../shared/cs.js';
import { Constants } from '../battle/Constants.js';
import { RunLoadout } from '../dungeon/RunLoadout.js';
import { NodeKind } from '../dungeon/NodeKind.js';
import { RunPhase } from '../dungeon/ExplorationState.js';
import { SevenLayers } from '../dungeon/SevenLayers.js';
import { SeededRng, RngExtensions } from '../dungeon/IRng.js';
import { toUlong } from '../dungeon/_errors.js';

/** Raised when a caller asks for something the inheritance rules do not allow. */
export class InheritanceRuleException extends Error {
  constructor(message) {
    super(message);
    this.name = 'InheritanceRuleException';
  }
}

/**
 * How a life ended (dungeon_exploration_v4.md §6.9). The values are RunPhase's member names, so
 * `state.Phase` of a finished ExplorationState can be passed straight in.
 */
export const LifeEnding = defineEnum(['MiasmaDeath', 'Fallen', 'Completed', 'Survived']);

/** What a trace holds (concept-v3.md §8.3 / §14, tier1-core.md R1-10 / R1-12). */
export const CandidateKind = defineEnum(['Card', 'EnemyPage', 'DungeonPage', 'Scribble']);

const SAVE_VERSION = 1;
const MASK64 = (1n << 64n) - 1n;
const DEATHS = [LifeEnding.MiasmaDeath, LifeEnding.Fallen];
const SURVIVALS = [LifeEnding.Survived, LifeEnding.Completed];

// ---------------------------------------------------------------------------------------------
// Numbers
// ---------------------------------------------------------------------------------------------

/** The rules' numbers, one place. Sources in docs/INHERITANCE.md. */
export const InheritanceRules = Object.freeze({
  /** concept-v3 §8.3 「限られた枠（例: 3 つまで）」, tier1 R1-12 「仮 3」. */
  LegacyPicks: 3,
  /** concept-v3 §14: candidates are cards at 習熟 1 段 or more. */
  CandidateMinRank: 1,
  /** concept-v3 §14 浅層死亡の閾値: cards at this rank or more ... */
  DeepRank: 2,
  /** ... stay only from a death at this layer or deeper. */
  DeepLayer: 3,
  /** concept-v3 §14 生存者ボーナス: tool slots +1 (RunLoadout.SurvivorBonusToolSlots). */
  SurvivorToolSlots: RunLoadout.SurvivorBonusToolSlots,
  /** concept-v3 §14 生存者ボーナス: deck cap +5. */
  SurvivorDeckBonus: 5,
  /** concept-v3 §14 「次の 1 生だけ」. */
  SurvivorBonusLives: 1,
  /** concept-v3 §15.2 / roster §7.4: the extra boss stretches the bonus to two lives. */
  ExtraBossBonusLives: 2,
  /** 試作で決めたこと: the 装束 tier by the previous life's reached layer (index = layer 0..7). */
  TierByLayer: Object.freeze([0, 0, 1, 1, 2, 2, 3, 3]),
  /** 試作で決めたこと: the tier after a life that defeated 歪みの根. */
  ClearedTier: 4,
  /** journal disclosure runs 0..2 (JournalPage.MaxDisclosure, concept-v3 §14). */
  MaxDisclosure: 2,
});

// ---------------------------------------------------------------------------------------------
// Japanese text the hall and the ending screens show (data, not logic)
// ---------------------------------------------------------------------------------------------

/**
 * 試作で決めたこと: the personal names of the heirs (concept-v3 §1 / §14 「世代ごとに名前を持つ」,
 * §16.1 「名前は個人のもの、姿は役のもの」). Katakana like the lair's named bosses (world-v1 §7.1),
 * none of which (ガルド, セルク) is reused.
 */
export const HeirNames = Object.freeze([
  'アルマ', 'イリス', 'ウルス', 'エルカ', 'オスカ', 'カイル', 'キリエ', 'クラウ', 'ケイト', 'コルネ',
  'サリア', 'シオン', 'スヴェン', 'ソラ', 'タリス', 'ティモ', 'トーヴァ', 'ナディア', 'ニコ', 'ノエル',
  'ハンナ', 'ヒルダ', 'フェン', 'ヘルガ', 'ホルン', 'マルタ', 'ミロ', 'ユリア', 'ヨハン', 'ラウル',
  'リーネ', 'ルカ', 'レア', 'ロタ', 'ヴェラ', 'イーダ', 'エミル', 'オリヴ', 'カリナ', 'テオ',
]);

/**
 * 試作で決めたこと: the 装束 tiers (concept-v3 §16.3: 面の縁飾り・マントの裏地・留め具 change with
 * the previous life's reached layer; the outline never changes). 装束 stays achromatic or cool and
 * the warm colours sit on the clasps only (style-guide §14.1, world-v1 §3 規則 4). Colours are the
 * world's six (world-v1 §6) where one fits.
 */
export const AttireTiers = Object.freeze([
  Object.freeze({ Tier: 0, Name: '縁なし', Mask: '縁飾りなし', Lining: '鈍鋼の裏地', Clasp: '鉄の留め具', MaskColor: null, LiningColor: '#6d7787', ClaspColor: '#8a8f97' }),
  Object.freeze({ Tier: 1, Name: '鉄の縁', Mask: '鉄の細い縁', Lining: '藍鼠の裏地', Clasp: '真鍮の留め具', MaskColor: '#6d7787', LiningColor: '#4f5a6e', ClaspColor: '#b08d57' }),
  Object.freeze({ Tier: 2, Name: '銀の縁', Mask: '銀の縁', Lining: '紺の裏地', Clasp: '琥珀の留め具', MaskColor: '#aeb6c2', LiningColor: '#2b3552', ClaspColor: '#e8a84c' }),
  Object.freeze({ Tier: 3, Name: '白銀の縁', Mask: '白銀の二重の縁', Lining: '深い紺の裏地', Clasp: '琥珀を象嵌した留め具', MaskColor: '#dfe4ea', LiningColor: '#1c2238', ClaspColor: '#e8a84c' }),
  Object.freeze({ Tier: 4, Name: '灯芯の留め', Mask: '白銀の二重の縁', Lining: '銀糸を織った深い紺の裏地', Clasp: '灯芯色の玉の留め具', MaskColor: '#dfe4ea', LiningColor: '#1c2238', ClaspColor: '#ffd89a' }),
]);

/** Every sentence the hall, the 痕跡 card and the ending card print. */
export const InheritanceText = Object.freeze({
  HallTitle: '継承の間',
  HallLead: '前の生から次の生へ渡すものを見る部屋。',
  HallEmpty: '最初の踏破者。継ぐものはまだ無い。',
  RoleName: '踏破者',
  StartButton: '出立の支度へ',
  ToHallButton: '継承の間へ',
  TraceHeading: '未回収の痕跡',
  NoTraces: '未回収の痕跡は無い。',
  PastLivesHeading: 'これまでの踏破者',
  SurvivorHeading: '前回の生存者',
  NextHeirHeading: '次の踏破者',
  AttireHeading: '装束',
  TraceRules: Object.freeze([
    '遺産の受け取りは、死亡地点を訪れたときに行う。ここでは候補を見るだけ。',
    '1 つの生で受け取れる痕跡は 1 件（最初に訪れた痕跡）。',
    '痕跡は回収されるまで残る。',
    '習熟 2 段目以上の札は、第 3 層より浅い死では遺産に残らない。',
  ]),
  TraceNodeTitle: '死亡地点の痕跡',
  TakeButton: '受け取る',
  LeaveButton: '何も受け取らない',
  AlreadyTaken: 'この生ではもう遺産を受け取った。この痕跡は残り、後の生で受け取れる。',
  Appears: 'この層の通常戦闘 1 つと入れ替わって現れる',
  Waits: '同じ層の古い痕跡を回収すると現れる',
  ClearedTraceNote: '歪みの根を倒した生の痕跡。札だけを残し、手記は生存者として渡った',
  WithheldHeading: '浅い層の死では残らなかった札',
  KindLabel: Object.freeze({ Card: '技術', EnemyPage: '敵の頁', DungeonPage: 'ダンジョンの頁', Scribble: '走り書き' }),
  EndingTitle: Object.freeze({ MiasmaDeath: '瘴気死', Fallen: '倒れた', Completed: '踏破', Survived: '生存ルート' }),
  /** The node kinds by name, as ExplorationText.KindLabel prints them. */
  NodeKindLabel: Object.freeze({
    Battle: '通常戦闘', Elite: '精鋭', Boss: '階層ボス', Rest: '休息', Survey: '情報収集',
    Event: 'イベント', Trace: '死亡地点の痕跡', Carving: '秘跡の彫り',
  }),

  GenerationLabel: (generation) => `第 ${generation} 代`,
  HeirTitle: (name) => `踏破者 ${name}`,
  HeirLine: (generation, name) => `第 ${generation} 代 踏破者 ${name}`,
  /** Same format as ExplorationText.LayerTitle (full-width space). */
  LayerTitle: (layer, name) => `第 ${layer} 層　${name}`,
  RankDetail: (rank) => `${rank} 段目`,
  DisclosureDetail: (level) => `開示度 ${level} / ${InheritanceRules.MaxDisclosure}`,
  DungeonPageDetail: '手記の頁',
  ScribbleName: (generation, name) => `走り書き（第 ${generation} 代 ${name}）`,
  RarePageTitle: (index) => `希少な頁 ${index}`,
  WithheldReason: (rank) => `${rank} 段目。第 ${InheritanceRules.DeepLayer} 層より浅い死では残らない`,
  PickLead: (max) => `遺産を ${max} つまで選んで受け取る。選ばなかったものはここで失われる。`,
  PickCount: (picked, max) => `${picked} / ${max}`,
  TraceOf: (generation, name) => `第 ${generation} 代 踏破者 ${name}の痕跡`,
  CandidateCount: (cards, pages, scribbles) => `遺産の候補：技術 ${cards} ／ 頁 ${pages} ／ 走り書き ${scribbles}`,

  TierFirst: '最初の踏破者。支給品は縁なし',
  TierReason: (generation, name, layer) => `前の生（第 ${generation} 代 ${name}）が第 ${layer} 層まで届いたため`,
  TierCleared: (generation, name) => `前の生（第 ${generation} 代 ${name}）が歪みの根を倒したため`,

  /** The four end sentences, the same words as ExplorationScreenBuilder's end cards. */
  EndingSentence(ending, layer) {
    switch (ending) {
      case LifeEnding.MiasmaDeath: return `瘴気が 100% に達し、第 ${layer} 層でこの生が終わった。`;
      case LifeEnding.Fallen: return `第 ${layer} 層の戦いで倒れた。`;
      case LifeEnding.Completed: return '歪みの根を倒した。';
      case LifeEnding.Survived: return `生きたまま、第 ${layer} 層でこの生を閉じた。`;
      default: throw new InheritanceRuleException(`unknown ending ${ending}`);
    }
  },
  TraceLeft: (layerTitle) => `${layerTitle}に痕跡と手記が残った`,
  TraceVisit: (layer, max) => `次の踏破者が第 ${layer} 層を訪れると、${max} つまで選んで受け取れる`,
  WithheldLine: (names) => `浅い層の死では残らない札：${names.join('、')}`,
  NothingLeft: '遺産になるものは残らなかった',
  JournalPasses: '手記は丸ごと次の生へ渡る',
  ClearedTraceLeft: (layerTitle) => `${layerTitle}に札の痕跡が残った（歪みの根の札を含む）`,
  SurvivorBonusLine: (lives) => `生存者ボーナス：ツール枠 +1 ／ デッキ上限 +5（${lives === 1 ? '次の 1 生' : `次の ${lives} 生`}）`,
  SurvivorReplaced: (generation, name) => `生存者は一人まで。第 ${generation} 代 ${name}の分は退いた`,
  NextHeirLine: (generation, name) => `次の踏破者：第 ${generation} 代 ${name}`,
  SurvivorJournal: '手記を丸ごと受け継ぐ',
  SurvivorTools: (from, to) => `ツール枠 +1（${from} → ${to}）`,
  SurvivorDeck: (from, to) => `デッキ上限 +${to - from}（${from} → ${to}）`,
  SurvivorDuration: (lives) => (lives === 1 ? '次の 1 生だけ' : `あと ${lives} 生`),
  /** The hall after the life that used the bonus closed without a new survivor (R-14: 次の 1 生だけ). */
  SurvivorSpent: (generation, name) => `第 ${generation} 代 ${name}の生でボーナスを使い終えた`,
  SurvivorSpentLimits: (tools, deck) => `ツール枠 ${tools} ／ デッキ上限 ${deck} に戻った`,
  TraceStatus: Object.freeze({
    None: '痕跡なし（生きて閉じた）',
    Untaken: '痕跡：未回収',
    Taken: (generation, name) => `痕跡：第 ${generation} 代 ${name}が受け取った`,
  }),
});

// ---------------------------------------------------------------------------------------------
// Small helpers
// ---------------------------------------------------------------------------------------------

function deepFreeze(value) {
  if (value !== null && typeof value === 'object' && !Object.isFrozen(value)) {
    Object.freeze(value);
    for (const key of Object.keys(value)) deepFreeze(value[key]);
  }
  return value;
}

/** A field read under its PascalCase or camelCase name. */
function field(obj, name) {
  if (obj === null || obj === undefined) return undefined;
  if (obj[name] !== undefined) return obj[name];
  const camel = name.charAt(0).toLowerCase() + name.slice(1);
  return obj[camel];
}

function isMember(E, v) {
  return E.$order.includes(v);
}

function isInt(v) {
  return typeof v === 'number' && Number.isInteger(v);
}

function requireInt(v, what, min = -Infinity, max = Infinity) {
  if (!isInt(v) || v < min || v > max) throw new InheritanceRuleException(`${what} must be an integer in ${min}..${max} (got ${v})`);
  return v;
}

function seedOf(seed) {
  if (seed === null || seed === undefined) throw new InheritanceRuleException('a seed is required');
  if (typeof seed === 'string') {
    if (!/^\d+$/.test(seed)) throw new InheritanceRuleException(`bad seed ${seed}`);
    return toUlong(BigInt(seed));
  }
  return toUlong(seed);
}

function mix(seed, salt) {
  return (seed * 0x9E3779B97F4A7C15n + salt) & MASK64;
}

function layerName(lineage, layer) {
  return SevenLayers.Of(layer).DisplayName(lineage.Player.CarvingTaken);
}

function placeTitle(lineage, layer, nodeKind) {
  const title = InheritanceText.LayerTitle(layer, layerName(lineage, layer));
  return nodeKind ? `${title}（${InheritanceText.NodeKindLabel[nodeKind]}）` : title;
}

// ---------------------------------------------------------------------------------------------
// Names and attire
// ---------------------------------------------------------------------------------------------

/** The cycle-th shuffled copy of the name pool, drawn from the lineage seed. */
function nameCycle(seed, cycle) {
  const names = HeirNames.slice();
  RngExtensions.Shuffle(new SeededRng(mix(seed, 0x5A17n + BigInt(cycle))), names);
  return names;
}

/**
 * The personal name of the heir of this generation. Deterministic from the lineage seed: the hall
 * can print the next heir's name before the life starts, and startLife gives the same one. Names do
 * not repeat inside one pass over the pool; a new pass never opens with the name the last one ended on.
 */
export function heirName(seed, generation) {
  requireInt(generation, 'generation', 1);
  const s = seedOf(seed);
  const n = HeirNames.length;
  const cycle = Math.floor((generation - 1) / n);
  const index = (generation - 1) % n;
  const names = nameCycle(s, cycle);
  if (cycle > 0) {
    const previousLast = nameCycle(s, cycle - 1)[n - 1];
    if (names[0] === previousLast) [names[0], names[1]] = [names[1], names[0]];
  }
  return names[index];
}

/** The 装束 tier the heir after `previous` wears (concept-v3 §14 / §16.3). */
export function tierAfter(previous) {
  if (!previous) return 0;
  if (previous.Ending === LifeEnding.Completed) return InheritanceRules.ClearedTier;
  return InheritanceRules.TierByLayer[previous.Layer] ?? 0;
}

function attireOf(tier) {
  const t = AttireTiers[tier];
  if (!t) throw new InheritanceRuleException(`no attire tier ${tier}`);
  return t;
}

function tierReason(previous) {
  if (!previous) return InheritanceText.TierFirst;
  if (previous.Ending === LifeEnding.Completed) return InheritanceText.TierCleared(previous.Generation, previous.Name);
  return InheritanceText.TierReason(previous.Generation, previous.Name, previous.Layer);
}

// ---------------------------------------------------------------------------------------------
// Journal
// ---------------------------------------------------------------------------------------------

/** The empty journal of a life that inherits none. */
export function emptyJournal() {
  return deepFreeze({ Disclosure: {}, Pages: [], Memo: '', Scribbles: [] });
}

/**
 * The journal as this module keeps it: 開示度 by enemy id (Expedition's journal), the rare pages
 * (Expedition.Pages; contents are #107's), the memo, and the 走り書き inherited from earlier heirs.
 * The page of terms and statuses is not part of it: it is never left at a death (tier1 R1-10).
 * Accepts a Map or an object for the disclosure, and a count, ids or {Id, Title} for the pages.
 */
export function normalizeJournal(journal) {
  if (journal === null || journal === undefined) return emptyJournal();
  const disclosure = {};
  const rawDisclosure = field(journal, 'Disclosure');
  const entries = rawDisclosure instanceof Map ? [...rawDisclosure] : Object.entries(rawDisclosure ?? {});
  for (const [id, level] of entries.sort((a, b) => compareOrdinal(a[0], b[0]))) {
    requireInt(level, `disclosure of ${id}`, 0, InheritanceRules.MaxDisclosure);
    if (level > 0) disclosure[id] = level;
  }

  const rawPages = field(journal, 'Pages') ?? [];
  let pages;
  if (isInt(rawPages)) {
    requireInt(rawPages, 'page count', 0);
    pages = Array.from({ length: rawPages }, (_, i) => ({ Id: `page-${i + 1}`, Title: InheritanceText.RarePageTitle(i + 1) }));
  } else if (Array.isArray(rawPages)) {
    pages = rawPages.map((p, i) => {
      if (typeof p === 'string') return { Id: p, Title: InheritanceText.RarePageTitle(i + 1) };
      const page = { Id: String(field(p, 'Id')), Title: String(field(p, 'Title') ?? InheritanceText.RarePageTitle(i + 1)) };
      const text = field(p, 'Text');
      if (typeof text === 'string' && text.trim().length > 0) page.Text = text;
      return page;
    });
  } else {
    throw new InheritanceRuleException('journal pages must be a count or a list');
  }
  const seen = new Set();
  for (const p of pages) {
    if (seen.has(p.Id)) throw new InheritanceRuleException(`page ${p.Id} is in the journal twice`);
    seen.add(p.Id);
  }

  const memo = field(journal, 'Memo') ?? '';
  if (typeof memo !== 'string') throw new InheritanceRuleException('the memo is text');
  const scribbles = (field(journal, 'Scribbles') ?? []).map((s) => ({
    Generation: requireInt(field(s, 'Generation'), 'scribble generation', 1),
    Name: String(field(s, 'Name')),
    Text: String(field(s, 'Text')),
  }));
  return deepFreeze({ Disclosure: disclosure, Pages: pages, Memo: memo, Scribbles: scribbles });
}

/**
 * Adds what a trace gave (or any journal) to a journal: 開示度 keeps the higher level, pages are
 * united by id, 走り書き are appended. The memo stays this heir's own.
 */
export function mergeJournal(journal, additions) {
  const a = normalizeJournal(journal);
  const b = normalizeJournal(additions);
  const disclosure = { ...a.Disclosure };
  for (const [id, level] of Object.entries(b.Disclosure)) disclosure[id] = Math.max(disclosure[id] ?? 0, level);
  const sorted = {};
  for (const id of Object.keys(disclosure).sort(compareOrdinal)) sorted[id] = disclosure[id];
  const ids = new Set(a.Pages.map((p) => p.Id));
  const pages = a.Pages.concat(b.Pages.filter((p) => !ids.has(p.Id)));
  const scribbles = a.Scribbles.concat(b.Scribbles.filter(
    (s) => !a.Scribbles.some((t) => t.Generation === s.Generation && t.Text === s.Text)));
  return deepFreeze({ Disclosure: sorted, Pages: pages, Memo: a.Memo, Scribbles: scribbles });
}

/**
 * The journal a new heir carries into the life: startLife's Journal (a survivor's whole journal, R-13)
 * with every 開示度, page and 走り書き as they are, and the memo turned into one more 走り書き of the heir
 * who wrote it (`from`: { Generation, Name }, startLife's JournalFrom). The memo is one heir's own sheet
 * (試作で決めたこと 8: an inherited 走り書き never overwrites the heir's own memo), so the new heir starts
 * on an empty sheet and the survivor's words stay theirs: read on the メモ tab under 受け継いだ走り書き,
 * and a candidate of their own (「走り書き（第 N 代 名前）」) when this heir's journal is left at a death.
 * Without a memo, or without `from` to name its writer, the journal is returned as it is.
 */
export function heirJournal(journal, from = null) {
  const j = normalizeJournal(journal);
  if (j.Memo.trim().length === 0 || from === null || from === undefined) return j;
  const generation = requireInt(field(from, 'Generation'), 'memo writer generation', 1);
  const name = String(field(from, 'Name') ?? '');
  const scribbles = j.Scribbles.some((s) => s.Generation === generation && s.Text === j.Memo)
    ? j.Scribbles
    : j.Scribbles.concat([{ Generation: generation, Name: name, Text: j.Memo }]);
  return deepFreeze({ Disclosure: j.Disclosure, Pages: j.Pages, Memo: '', Scribbles: scribbles });
}

// ---------------------------------------------------------------------------------------------
// Cards and mastery
// ---------------------------------------------------------------------------------------------

/**
 * The mastered cards of a life, from a MasteryBook (its Records, a Map or an object of
 * {CardId, Ticks, Rank}; ProfileOf(id) is used for names and thresholds when present) or from a
 * list of {CardId, Rank, Ticks?, Name?, Thresholds?}. Sorted by rank (high first), then id.
 */
function masteredCards(deck, cardName) {
  if (deck === null || deck === undefined) return [];
  let entries;
  let book = null;
  if (Array.isArray(deck)) {
    entries = deck;
  } else {
    book = deck;
    const records = field(deck, 'Records');
    entries = records instanceof Map ? [...records.values()] : Object.values(records ?? {});
  }
  const seen = new Set();
  const cards = [];
  for (const e of entries) {
    const cardId = field(e, 'CardId');
    if (typeof cardId !== 'string' || cardId.length === 0) throw new InheritanceRuleException('a card needs a CardId');
    if (seen.has(cardId)) throw new InheritanceRuleException(`card ${cardId} is listed twice`);
    seen.add(cardId);
    const rank = requireInt(field(e, 'Rank') ?? 0, `rank of ${cardId}`, 0, 3);
    const profile = book && typeof book.ProfileOf === 'function' ? book.ProfileOf(cardId) : null;
    const thresholds = field(e, 'Thresholds') ?? profile?.Thresholds ?? Constants.MasteryThresholds;
    const name = field(e, 'Name')
      ?? (cardName ? cardName(cardId, rank) : null)
      ?? (profile && typeof profile.NameAt === 'function' ? profile.NameAt(rank) : null)
      ?? cardId;
    cards.push({ CardId: cardId, Rank: rank, Ticks: rank === 0 ? 0 : thresholds[rank - 1], Name: String(name) });
  }
  return cards.sort((x, y) => (y.Rank - x.Rank) || compareOrdinal(x.CardId, y.CardId));
}

/**
 * Seeds a life's mastery with inherited cards (concept-v3 §14 「段をそのまま継ぐ」). Each card keeps
 * the higher of its rank and ticks; the one-card limit on step 3 (Mastery.TopRank, Constants
 * .MasteryTopRankCards) holds: a card already at step 3 keeps it and an inherited step 3 stops at 2.
 * `records` is a MasteryBook's Records (Map or object) or null; `make(cardId, ticks, rank)` builds
 * each record (pass `(a, b, c) => new CardMastery(a, b, c)`). Returns a Map in ordinal id order.
 */
export function mergeMastery(records, cards, make = (CardId, Ticks, Rank) => ({ CardId, Ticks, Rank })) {
  const base = new Map();
  const list = records instanceof Map ? [...records.values()] : Object.values(records ?? {});
  for (const r of list) base.set(field(r, 'CardId'), { CardId: field(r, 'CardId'), Ticks: field(r, 'Ticks') ?? 0, Rank: field(r, 'Rank') ?? 0 });
  let holder = null;
  for (const r of base.values()) if (r.Rank >= 3 && holder === null) holder = r.CardId;
  for (const c of cards ?? []) {
    const had = base.get(c.CardId);
    let rank = Math.max(had?.Rank ?? 0, c.Rank);
    const ticks = Math.max(had?.Ticks ?? 0, c.Ticks ?? 0);
    if (rank >= 3) {
      if (holder === null) holder = c.CardId;
      else if (holder !== c.CardId) rank = 2;
    }
    base.set(c.CardId, { CardId: c.CardId, Ticks: ticks, Rank: rank });
  }
  const out = new Map();
  for (const id of [...base.keys()].sort(compareOrdinal)) {
    const r = base.get(id);
    out.set(id, make(r.CardId, r.Ticks, r.Rank));
  }
  return out;
}

// ---------------------------------------------------------------------------------------------
// Candidates
// ---------------------------------------------------------------------------------------------

/**
 * Whether a rare dungeon page has something written on it. Pages are counted (Expedition.Pages) but their contents
 * are #107's and none has any yet, so today no page is filled (試作で決めたこと 9).
 */
export function isFilledPage(p) {
  return !!p && typeof p.Text === 'string' && p.Text.trim().length > 0;
}

/**
 * The candidates a screen offers: a trace saved before empty pages stopped being candidates still holds them, and
 * they are left out of its list and counts (taking one still replays, so an old save's log loads).
 */
function offeredCandidates(trace) {
  return trace.Candidates.filter((c) => c.Kind !== CandidateKind.DungeonPage || isFilledPage(c));
}

/** The candidates a dead heir leaves, and the cards the shallow rule keeps out (§8.4 / §14). */
function buildCandidates(life, layer, cards, journal, enemyName, { withPages = true } = {}) {
  const candidates = [];
  const withheld = [];
  for (const c of cards) {
    if (c.Rank < InheritanceRules.CandidateMinRank) continue;
    if (c.Rank >= InheritanceRules.DeepRank && layer < InheritanceRules.DeepLayer) {
      withheld.push({ CardId: c.CardId, Rank: c.Rank, Name: c.Name });
      continue;
    }
    candidates.push({ Id: `card:${c.CardId}`, Kind: CandidateKind.Card, CardId: c.CardId, Rank: c.Rank, Ticks: c.Ticks, Name: c.Name });
  }
  if (withPages) {
    for (const [id, level] of Object.entries(journal.Disclosure)) {
      candidates.push({ Id: `enemy:${id}`, Kind: CandidateKind.EnemyPage, EnemyId: id, Disclosure: level, Name: String(enemyName ? enemyName(id) ?? id : id) });
    }
    // Only a filled page is a candidate (§14 「埋まった頁を全部並べる」). The rare pages have no content yet (#107):
    // an empty one gives the heir nothing, so it never takes one of the 3 picks (試作で決めたこと 9).
    for (const p of journal.Pages.filter(isFilledPage)) {
      candidates.push({ Id: `page:${p.Id}`, Kind: CandidateKind.DungeonPage, PageId: p.Id, Name: p.Title, Text: p.Text });
    }
    journal.Scribbles.forEach((s, i) => {
      candidates.push({ Id: `scribble:${i}`, Kind: CandidateKind.Scribble, Generation: s.Generation, From: s.Name, Text: s.Text, Name: InheritanceText.ScribbleName(s.Generation, s.Name) });
    });
    if (journal.Memo.trim().length > 0) {
      candidates.push({ Id: 'memo', Kind: CandidateKind.Scribble, Generation: life.Generation, From: life.Name, Text: journal.Memo, Name: InheritanceText.ScribbleName(life.Generation, life.Name) });
    }
  }
  return { candidates, withheld };
}

function candidateDetail(c) {
  switch (c.Kind) {
    case CandidateKind.Card: return InheritanceText.RankDetail(c.Rank);
    case CandidateKind.EnemyPage: return InheritanceText.DisclosureDetail(c.Disclosure);
    case CandidateKind.DungeonPage: return InheritanceText.DungeonPageDetail;
    case CandidateKind.Scribble: return c.Text.length > 20 ? `${c.Text.slice(0, 20)}…` : c.Text;
    default: throw new InheritanceRuleException(`unknown candidate kind ${c.Kind}`);
  }
}

/**
 * A candidate row as the hall and the 痕跡 card print it, plus what it holds as data (the card and its 段, the
 * enemy and its 開示度, the page, the 走り書き), so a screen can say what the row gives a given heir (the game's
 * TraceGain) without reading the printed words back.
 */
function candidateView(c) {
  const v = { Id: c.Id, Kind: c.Kind, KindLabel: InheritanceText.KindLabel[c.Kind], Name: c.Name, Detail: candidateDetail(c) };
  switch (c.Kind) {
    case CandidateKind.Card: v.CardId = c.CardId; v.Rank = c.Rank; break;
    case CandidateKind.EnemyPage: v.EnemyId = c.EnemyId; v.Disclosure = c.Disclosure; break;
    case CandidateKind.DungeonPage: v.PageId = c.PageId; break;
    case CandidateKind.Scribble: v.Generation = c.Generation; v.From = c.From; v.Text = c.Text; break;
    default: break;
  }
  return v;
}

function countCandidates(trace) {
  const offered = offeredCandidates(trace);
  const cards = offered.filter((c) => c.Kind === CandidateKind.Card).length;
  const pages = offered.filter((c) => c.Kind === CandidateKind.EnemyPage || c.Kind === CandidateKind.DungeonPage).length;
  const scribbles = offered.filter((c) => c.Kind === CandidateKind.Scribble).length;
  return { cards, pages, scribbles, total: offered.length };
}

// ---------------------------------------------------------------------------------------------
// Lineage
// ---------------------------------------------------------------------------------------------

/**
 * A new lineage: no life yet. The seed draws the heirs' names (and default life seeds), so a
 * lineage replays from it.
 */
export function newLineage(seed) {
  return deepFreeze({
    Version: SAVE_VERSION,
    Seed: seedOf(seed).toString(),
    Lives: [],
    Current: null,
    Traces: [],
    Survivor: null,
    Player: { CarvingTaken: false, RootDefeated: false },
  });
}

function nextGeneration(lineage) {
  return lineage.Lives.length + (lineage.Current ? 1 : 0) + 1;
}

function lastLife(lineage) {
  return lineage.Lives.length ? lineage.Lives[lineage.Lives.length - 1] : null;
}

/**
 * The heir who sets out next — generation, name, tier — before startLife, so the hall can name
 * them; while a life is in progress, that life's heir.
 */
export function nextHeir(lineage) {
  const c = lineage.Current;
  if (c) return deepFreeze({ Generation: c.Generation, Name: c.Name, Tier: c.Tier });
  const generation = lineage.Lives.length + 1;
  return deepFreeze({
    Generation: generation,
    Name: heirName(lineage.Seed, generation),
    Tier: tierAfter(lastLife(lineage)),
  });
}

/** The default seed of a life when startLife is given none: drawn from the lineage seed. */
export function lifeSeed(lineage, generation) {
  return new SeededRng(mix(seedOf(lineage.Seed), 0xC0FFEEn + BigInt(generation))).NextUInt64();
}

/**
 * The trace that stands on `layer` for the life in progress (or the next one when none is in
 * progress), or null. A layer holds at most one (NodeKind.Trace): the oldest untaken trace there.
 * None appears once the life has taken its one trace (concept-v3 §8.3).
 */
export function traceForLayer(lineage, layer) {
  if (lineage.Current && lineage.Current.TakenTraceId !== null) return null;
  return lineage.Traces.find((t) => t.Layer === layer) ?? null;
}

/** The layers that show a trace node for the life in progress, in order. */
export function traceLayers(lineage) {
  const layers = [];
  for (let layer = 1; layer <= SevenLayers.Count; layer++) if (traceForLayer(lineage, layer)) layers.push(layer);
  return layers;
}

/**
 * The map spec of `profile` for this lineage: one battle traded for a trace node on a layer that
 * holds one (seven_layers_v4 §4, encounters_and_rewards_v4 §2.1), and the carving dropped on layer
 * two once the player has read it (seven_layers_v4 §5: 「ノードは出ない」, battle back in its place).
 * Generate the map with `MapGenerator.Generate(mapSpecFor(lineage, profile), seed)`.
 */
export function mapSpecFor(lineage, profile) {
  let spec = profile.MapSpec;
  if (lineage.Player.CarvingTaken && spec.Quotas.has(NodeKind.Carving)) spec = spec.With(NodeKind.Carving, 0);
  if (traceForLayer(lineage, profile.Layer)) spec = spec.With(NodeKind.Trace, 1);
  return spec;
}

/**
 * Starts the next life (concept-v3 §8 / §9, CAMP_FACILITIES_DESIGN §3.1 「新しいキャラクターを開始」).
 * `seed` is the life's run seed (ExplorationSession's RunStart.Seed); omitted, it is drawn from the
 * lineage seed. Returns the new lineage and what 出立 adds: tool slots and deck cap (生存者ボーナス,
 * §14), the journal the heir starts with (a survivor's whole journal, §8.2, or nothing), an empty
 * RunLoadout with the right slot count, and the layers that hold a trace.
 */
export function startLife(lineage, seed = null) {
  if (lineage.Current) throw new InheritanceRuleException(`generation ${lineage.Current.Generation} is still in progress`);
  const generation = nextGeneration(lineage);
  const previous = lastLife(lineage);
  const runSeed = seed === null || seed === undefined ? lifeSeed(lineage, generation) : seedOf(seed);

  const survivor = lineage.Survivor;
  const bonus = survivor !== null && survivor.BonusLivesLeft > 0;
  const passes = survivor !== null && survivor.Journal !== null && survivor.Generation === generation - 1;
  const journal = passes ? normalizeJournal(survivor.Journal) : emptyJournal();
  let nextSurvivor = null;
  if (survivor !== null) {
    const left = bonus ? survivor.BonusLivesLeft - 1 : survivor.BonusLivesLeft;
    nextSurvivor = left > 0 ? { ...survivor, BonusLivesLeft: left, Journal: null } : null;
  }

  const tier = tierAfter(previous);
  const life = {
    Generation: generation,
    Name: heirName(lineage.Seed, generation),
    Tier: tier,
    Seed: runSeed.toString(),
    SurvivorBonus: bonus,
    SurvivorFrom: bonus ? survivor.Generation : null,
    InheritedJournal: journal.Pages.length > 0 || Object.keys(journal.Disclosure).length > 0
      || journal.Scribbles.length > 0 || journal.Memo.length > 0,
    TakenTraceId: null,
    Ending: null,
    Layer: null,
    NodeId: null,
    NodeKind: null,
    TraceId: null,
    CollectedBy: null,
    ReplacedSurvivor: null,
  };
  const next = deepFreeze({ ...lineage, Current: life, Survivor: nextSurvivor });

  const toolSlots = bonus ? InheritanceRules.SurvivorToolSlots : RunLoadout.ToolSlots;
  const deckMax = Constants.DeckMax + (bonus ? InheritanceRules.SurvivorDeckBonus : 0);
  return Object.freeze({
    Lineage: next,
    Life: life,
    Seed: runSeed,
    ToolSlots: toolSlots,
    ConsumableSlots: RunLoadout.ConsumableSlots,
    DeckMin: Constants.DeckMin,
    DeckMax: deckMax,
    Loadout: new RunLoadout(null, null, toolSlots),
    Journal: journal,
    /** Who wrote the journal's memo (the survivor it came from), for heirJournal; null with no journal. */
    JournalFrom: passes ? Object.freeze({ Generation: survivor.Generation, Name: survivor.Name }) : null,
    SurvivorBonus: bonus,
    TraceLayers: traceLayers(next),
    CarvingKnown: lineage.Player.CarvingTaken,
    Attire: attireOf(tier),
  });
}

/** Where the life stands, from a finished ExplorationState (any object with Profile, CurrentNodeId, Map, Phase). */
export function placeFromState(state) {
  return {
    Layer: state.Profile.Layer,
    NodeId: state.CurrentNodeId,
    NodeKind: state.Map.Node(state.CurrentNodeId).Kind,
    Ending: state.Phase,
  };
}

function checkLife(lineage, life) {
  const current = lineage.Current;
  if (!current) throw new InheritanceRuleException('no life is in progress');
  if (life !== null && life !== undefined) {
    const generation = isInt(life) ? life : field(life, 'Generation');
    if (generation !== current.Generation)
      throw new InheritanceRuleException(`generation ${generation} is not the life in progress (${current.Generation})`);
  }
  return current;
}

function checkPlace(place, allowed) {
  const layer = requireInt(field(place, 'Layer'), 'layer', 1, SevenLayers.Count);
  const nodeId = field(place, 'NodeId') ?? null;
  if (nodeId !== null) requireInt(nodeId, 'node id', 0);
  const nodeKind = field(place, 'NodeKind') ?? null;
  if (nodeKind !== null && !isMember(NodeKind, nodeKind)) throw new InheritanceRuleException(`unknown node kind ${nodeKind}`);
  const ending = field(place, 'Ending') ?? field(place, 'Phase') ?? allowed[0];
  if (RunPhase[ending] !== ending || !isMember(RunPhase, ending)) throw new InheritanceRuleException(`${ending} is not a RunPhase`);
  if (!allowed.includes(ending)) throw new InheritanceRuleException(`${ending} is not one of ${allowed.join(' / ')}`);
  return { Layer: layer, NodeId: nodeId, NodeKind: nodeKind, Ending: ending };
}

function playerAfter(lineage, journal, extra = {}) {
  const carving = lineage.Player.CarvingTaken || Boolean(field(journal ?? {}, 'CarvingTaken'));
  return { ...lineage.Player, CarvingTaken: carving, ...extra };
}

/**
 * The heir died (HP 0 or 瘴気死, concept-v3 §8.1 / §8.3, tier1 R1-11). The life closes, and a trace
 * stays at the place of death with the journal: every card at 習熟 1 段 or more and every filled
 * page (§14), minus the cards the shallow rule keeps out (§8.4 / §14). Earlier traces stay (§8.3).
 *
 * @param life   the life in progress (its record, its generation, or null for the current one)
 * @param place  { Layer, NodeId?, NodeKind?, Ending: RunPhase.Fallen | RunPhase.MiasmaDeath }
 * @param deck   a MasteryBook or a list of { CardId, Rank, Name? } — the life's mastery
 * @param journal the life's journal (see normalizeJournal); CarvingTaken: true records the carving
 * @param opts   { cardName(cardId, rank), enemyName(enemyId) } to store display names
 */
export function recordDeath(lineage, life, place, deck, journal, opts = {}) {
  const current = checkLife(lineage, life);
  const where = checkPlace(place, DEATHS);
  const cards = masteredCards(deck, opts.cardName);
  const book = normalizeJournal(journal);
  const { candidates, withheld } = buildCandidates(current, where.Layer, cards, book, opts.enemyName);
  const traceId = `trace-${current.Generation}`;
  const trace = {
    Id: traceId,
    Generation: current.Generation,
    Name: current.Name,
    Layer: where.Layer,
    NodeId: where.NodeId,
    NodeKind: where.NodeKind,
    Ending: where.Ending,
    Cleared: false,
    Candidates: candidates,
    Withheld: withheld,
  };
  const closed = { ...current, Ending: where.Ending, Layer: where.Layer, NodeId: where.NodeId, NodeKind: where.NodeKind, TraceId: traceId };
  return deepFreeze({
    ...lineage,
    Lives: lineage.Lives.concat([closed]),
    Current: null,
    Traces: lineage.Traces.concat([trace]),
    Player: playerAfter(lineage, journal),
  });
}

/**
 * The heir closed the life alive: 「この生を終える」 (RunPhase.Survived) or 歪みの根 defeated
 * (RunPhase.Completed). The heir becomes the one survivor (§8.2: a new survivor replaces the
 * previous one, tier1 R1-13): the whole journal passes to the next life, and the 生存者ボーナス
 * (tool slots +1, deck cap +5) holds for the next life only (§14) — two after the extra boss
 * (§15.2, opts.ExtraBossWon). A cleared life also leaves a trace on its layer with its mastered
 * cards and 歪みの根's cards (opts.RootCards; roster §6.6 / §7.4: they pass only as legacy) —
 * 試作で決めたこと, see docs/INHERITANCE.md.
 */
export function recordSurvival(lineage, life, place, deck, journal, opts = {}) {
  const current = checkLife(lineage, life);
  const where = checkPlace(place, SURVIVALS);
  const book = normalizeJournal(journal);
  const cleared = where.Ending === LifeEnding.Completed;
  const lives = opts.ExtraBossWon ? InheritanceRules.ExtraBossBonusLives : InheritanceRules.SurvivorBonusLives;

  let traces = lineage.Traces;
  let traceId = null;
  if (cleared) {
    const rootCards = (opts.RootCards ?? []).map((c) => (typeof c === 'string' ? { CardId: c, Rank: 0 } : c));
    const cards = masteredCards(deck, opts.cardName);
    const { candidates, withheld } = buildCandidates(current, where.Layer, cards, book, opts.enemyName, { withPages: false });
    const ids = new Set(candidates.map((c) => c.CardId));
    const roots = masteredCards(rootCards.filter((c) => !ids.has(field(c, 'CardId'))), opts.cardName);
    for (const c of roots) {
      candidates.push({ Id: `card:${c.CardId}`, Kind: CandidateKind.Card, CardId: c.CardId, Rank: c.Rank, Ticks: c.Ticks, Name: c.Name });
    }
    traceId = `trace-${current.Generation}`;
    traces = traces.concat([{
      Id: traceId,
      Generation: current.Generation,
      Name: current.Name,
      Layer: where.Layer,
      NodeId: where.NodeId,
      NodeKind: where.NodeKind,
      Ending: where.Ending,
      Cleared: true,
      Candidates: candidates,
      Withheld: withheld,
    }]);
  }

  const replaced = lineage.Survivor ? lineage.Survivor.Generation : null;
  const closed = {
    ...current,
    Ending: where.Ending,
    Layer: where.Layer,
    NodeId: where.NodeId,
    NodeKind: where.NodeKind,
    TraceId: traceId,
    ReplacedSurvivor: replaced,
  };
  return deepFreeze({
    ...lineage,
    Lives: lineage.Lives.concat([closed]),
    Current: null,
    Traces: traces,
    Survivor: { Generation: current.Generation, Name: current.Name, BonusLivesLeft: lives, BonusLives: lives, Journal: book },
    Player: playerAfter(lineage, journal, cleared ? { RootDefeated: true } : {}),
  });
}

/** recordDeath or recordSurvival by the place's Ending. */
export function recordEnd(lineage, life, place, deck, journal, opts = {}) {
  const ending = field(place, 'Ending') ?? field(place, 'Phase');
  if (DEATHS.includes(ending)) return recordDeath(lineage, life, place, deck, journal, opts);
  if (SURVIVALS.includes(ending)) return recordSurvival(lineage, life, place, deck, journal, opts);
  throw new InheritanceRuleException(`${ending} does not end a life`);
}

/** The layer-two carving was read: the player knows the name from now on (seven_layers_v4 §5). */
export function noteCarving(lineage) {
  if (lineage.Player.CarvingTaken) return lineage;
  return deepFreeze({ ...lineage, Player: { ...lineage.Player, CarvingTaken: true } });
}

/**
 * At a 痕跡 node: takes up to InheritanceRules.LegacyPicks of the trace's candidates (concept-v3
 * §8.3). Only the first trace a life visits gives anything (one per life); visiting takes it, so
 * the trace is collected and whatever was not chosen is lost (§5 「選ばなかった頁と資源は失われる」).
 * An empty pick list is allowed ("何も受け取らない") and still collects it.
 *
 * Returns { Lineage, Taken: { TraceId, Cards: [{CardId, Rank, Ticks, Name}], Journal } } where
 * Journal is the additions in journal shape (merge with mergeJournal) and Cards seed the mastery
 * book (mergeMastery) and the cards owned.
 *
 * @param opts { Layer } the layer the heir stands on; when given it must be the trace's.
 */
export function takeTrace(lineage, traceId, picks, opts = {}) {
  const current = lineage.Current;
  if (!current) throw new InheritanceRuleException('no life is in progress');
  if (current.TakenTraceId !== null)
    throw new InheritanceRuleException(`generation ${current.Generation} already took ${current.TakenTraceId}`);
  const trace = lineage.Traces.find((t) => t.Id === traceId);
  if (!trace) throw new InheritanceRuleException(`no untaken trace ${traceId}`);
  const layer = field(opts, 'Layer');
  if (layer !== undefined && layer !== null && layer !== trace.Layer)
    throw new InheritanceRuleException(`${traceId} is on layer ${trace.Layer}, not ${layer}`);
  if (traceForLayer(lineage, trace.Layer)?.Id !== traceId)
    throw new InheritanceRuleException(`${traceId} waits behind an older trace on layer ${trace.Layer}`);

  const chosen = picks ?? [];
  if (!Array.isArray(chosen)) throw new InheritanceRuleException('picks are a list of candidate ids');
  if (chosen.length > InheritanceRules.LegacyPicks)
    throw new InheritanceRuleException(`${chosen.length} picks, at most ${InheritanceRules.LegacyPicks}`);
  if (new Set(chosen).size !== chosen.length) throw new InheritanceRuleException('a candidate is picked twice');
  const taken = chosen.map((id) => {
    const c = trace.Candidates.find((x) => x.Id === id);
    if (!c) throw new InheritanceRuleException(`${id} is not a candidate of ${traceId}`);
    return c;
  });

  const cards = taken.filter((c) => c.Kind === CandidateKind.Card)
    .map((c) => ({ CardId: c.CardId, Rank: c.Rank, Ticks: c.Ticks, Name: c.Name }));
  const disclosure = {};
  const pages = [];
  const scribbles = [];
  for (const c of taken) {
    if (c.Kind === CandidateKind.EnemyPage) disclosure[c.EnemyId] = c.Disclosure;
    else if (c.Kind === CandidateKind.DungeonPage) pages.push(c.Text ? { Id: c.PageId, Title: c.Name, Text: c.Text } : { Id: c.PageId, Title: c.Name });
    else if (c.Kind === CandidateKind.Scribble) scribbles.push({ Generation: c.Generation, Name: c.From, Text: c.Text });
  }
  const journal = normalizeJournal({ Disclosure: disclosure, Pages: pages, Scribbles: scribbles });

  const lives = lineage.Lives.map((l) => (l.TraceId === traceId ? { ...l, CollectedBy: current.Generation } : l));
  const next = deepFreeze({
    ...lineage,
    Lives: lives,
    Current: { ...current, TakenTraceId: traceId },
    Traces: lineage.Traces.filter((t) => t.Id !== traceId),
  });
  return Object.freeze({
    Lineage: next,
    Taken: deepFreeze({ TraceId: traceId, Cards: cards, Journal: journal, Lost: trace.Candidates.length - taken.length }),
  });
}

// ---------------------------------------------------------------------------------------------
// View-models
// ---------------------------------------------------------------------------------------------

function heirView(heir, reason) {
  const attire = attireOf(heir.Tier);
  return {
    Generation: heir.Generation,
    GenerationLabel: InheritanceText.GenerationLabel(heir.Generation),
    Name: heir.Name,
    Title: InheritanceText.HeirTitle(heir.Name),
    Line: InheritanceText.HeirLine(heir.Generation, heir.Name),
    Tier: heir.Tier,
    TierName: attire.Name,
    TierReason: reason,
    Attire: [
      { Part: '面の縁飾り', Text: attire.Mask, Color: attire.MaskColor },
      { Part: 'マントの裏地', Text: attire.Lining, Color: attire.LiningColor },
      { Part: '留め具', Text: attire.Clasp, Color: attire.ClaspColor },
    ],
  };
}

function traceView(lineage, trace) {
  const visible = traceForLayer({ ...lineage, Current: null }, trace.Layer)?.Id === trace.Id;
  const counts = countCandidates(trace);
  return {
    Id: trace.Id,
    Generation: trace.Generation,
    HeirLine: InheritanceText.HeirLine(trace.Generation, trace.Name),
    Layer: trace.Layer,
    NodeId: trace.NodeId,
    PlaceLine: placeTitle(lineage, trace.Layer, trace.NodeKind),
    EndingLabel: InheritanceText.EndingTitle[trace.Ending],
    Cleared: trace.Cleared,
    Note: trace.Cleared ? InheritanceText.ClearedTraceNote : '',
    Visible: visible,
    AppearsLine: visible ? InheritanceText.Appears : InheritanceText.Waits,
    CountLine: InheritanceText.CandidateCount(counts.cards, counts.pages, counts.scribbles),
    Candidates: offeredCandidates(trace).map(candidateView),
    Withheld: trace.Withheld.map((w) => ({ Name: w.Name, Detail: InheritanceText.WithheldReason(w.Rank) })),
  };
}

function survivorView(lineage) {
  const s = lineage.Survivor;
  if (!s) return null;
  const nextGen = nextGeneration(lineage);
  const journalPending = s.Journal !== null && s.Generation === nextGen - 1;
  const lines = [];
  if (journalPending) lines.push(InheritanceText.SurvivorJournal);
  if (s.BonusLivesLeft > 0) {
    lines.push(InheritanceText.SurvivorTools(RunLoadout.ToolSlots, InheritanceRules.SurvivorToolSlots));
    lines.push(InheritanceText.SurvivorDeck(Constants.DeckMax, Constants.DeckMax + InheritanceRules.SurvivorDeckBonus));
  }
  return {
    Generation: s.Generation,
    Line: InheritanceText.HeirLine(s.Generation, s.Name),
    Lines: lines,
    DurationLine: s.BonusLivesLeft > 0 ? InheritanceText.SurvivorDuration(s.BonusLivesLeft) : '',
    BonusLivesLeft: s.BonusLivesLeft,
  };
}

/**
 * The survivor whose bonus the last closed life used up, when no survivor stands now (R-14: the bonus is for the
 * next life only; R-15): the hall says so where the survivor stood, so the block does not vanish without a word and
 * the next heir's 3 tool slots and 40 cards read as the bonus having ended. Null otherwise.
 */
function survivorSpentView(lineage) {
  if (lineage.Survivor && lineage.Survivor.BonusLivesLeft > 0) return null;
  const last = lastLife(lineage);
  if (!last || !last.SurvivorBonus || last.SurvivorFrom === null || last.SurvivorFrom === undefined) return null;
  if (lineage.Survivor && lineage.Survivor.Generation === last.Generation) return null;
  const from = lineage.Lives.find((l) => l.Generation === last.SurvivorFrom);
  return {
    Generation: last.SurvivorFrom,
    Line: InheritanceText.HeirLine(last.SurvivorFrom, from ? from.Name : ''),
    Lines: [
      InheritanceText.SurvivorSpent(last.Generation, last.Name),
      InheritanceText.SurvivorSpentLimits(RunLoadout.ToolSlots, Constants.DeckMax),
    ],
  };
}

function pastLifeView(lineage, life) {
  let traceLine;
  if (life.TraceId === null) traceLine = InheritanceText.TraceStatus.None;
  else if (life.CollectedBy === null) traceLine = InheritanceText.TraceStatus.Untaken;
  else {
    const by = lineage.Lives.find((l) => l.Generation === life.CollectedBy) ?? lineage.Current;
    traceLine = InheritanceText.TraceStatus.Taken(life.CollectedBy, by ? by.Name : '');
  }
  return {
    Generation: life.Generation,
    GenerationLabel: InheritanceText.GenerationLabel(life.Generation),
    Name: life.Name,
    Line: InheritanceText.HeirLine(life.Generation, life.Name),
    TierName: attireOf(life.Tier).Name,
    Ending: life.Ending,
    EndingLabel: InheritanceText.EndingTitle[life.Ending],
    EndingLine: InheritanceText.EndingSentence(life.Ending, life.Layer),
    PlaceLine: placeTitle(lineage, life.Layer, life.NodeKind),
    TraceLine: traceLine,
  };
}

/**
 * 継承の間 (CAMP_FACILITIES_DESIGN §3.1): the past lives (newest first), every untaken trace with
 * its candidates (look only: the choice is made at the place of death, concept-v3 §8.3), the
 * survivor's bonus, and the next heir with name and 装束. Nothing in it takes a trace.
 */
export function hallView(lineage) {
  const heir = nextHeir(lineage);
  return deepFreeze({
    Title: InheritanceText.HallTitle,
    Lead: InheritanceText.HallLead,
    Empty: lineage.Lives.length === 0 ? InheritanceText.HallEmpty : '',
    NextHeirHeading: InheritanceText.NextHeirHeading,
    NextHeir: heirView(heir, tierReason(lastLife(lineage))),
    PastLivesHeading: InheritanceText.PastLivesHeading,
    PastLives: lineage.Lives.slice().reverse().map((l) => pastLifeView(lineage, l)),
    TraceHeading: InheritanceText.TraceHeading,
    NoTraces: lineage.Traces.length === 0 ? InheritanceText.NoTraces : '',
    Traces: lineage.Traces.map((t) => traceView(lineage, t)),
    TraceRules: InheritanceText.TraceRules,
    SurvivorHeading: InheritanceText.SurvivorHeading,
    Survivor: survivorView(lineage),
    SurvivorSpent: survivorView(lineage) ? null : survivorSpentView(lineage),
    StartButton: InheritanceText.StartButton,
  });
}

/**
 * The card the 痕跡 node opens (dungeon_exploration_v4 §6.3: 「前の生の遺産を受け取れること（1 つの生で
 * 1 件）」): the trace on `layer`, its candidates to pick from and how many may be picked. Null when
 * no trace stands there for this life.
 */
export function traceNodeView(lineage, layer) {
  const trace = lineage.Traces.find((t) => t.Layer === layer) ?? null;
  if (!trace) return null;
  const current = lineage.Current;
  const canTake = current !== null && current.TakenTraceId === null;
  const view = traceView(lineage, trace);
  return deepFreeze({
    Title: InheritanceText.TraceNodeTitle,
    TraceId: trace.Id,
    HeirLine: InheritanceText.TraceOf(trace.Generation, trace.Name),
    PlaceLine: view.PlaceLine,
    Lead: InheritanceText.PickLead(InheritanceRules.LegacyPicks),
    Note: view.Note,
    Candidates: view.Candidates,
    WithheldHeading: trace.Withheld.length ? InheritanceText.WithheldHeading : '',
    Withheld: view.Withheld,
    MaxPicks: InheritanceRules.LegacyPicks,
    CanTake: canTake,
    Refusal: canTake ? '' : InheritanceText.AlreadyTaken,
    TakeButton: InheritanceText.TakeButton,
    LeaveButton: InheritanceText.LeaveButton,
  });
}

/** The 「n / 3」 the 痕跡 card prints under its list, and whether one more may be picked. */
export function pickState(selected) {
  const n = (selected ?? []).length;
  return Object.freeze({
    Line: InheritanceText.PickCount(n, InheritanceRules.LegacyPicks),
    CanPickMore: n < InheritanceRules.LegacyPicks,
  });
}

/**
 * The ending card of the life that closed last (dungeon_exploration_v4 §6.9): the end sentence,
 * then what stays and what passes on — the trace and its candidates, the cards the shallow rule
 * kept out, or the whole journal and the survivor's bonus — and the next heir.
 */
export function endingView(lineage) {
  const life = lastLife(lineage);
  if (!life || lineage.Current) throw new InheritanceRuleException('no closed life to show');
  const lines = [];
  const layerTitle = InheritanceText.LayerTitle(life.Layer, layerName(lineage, life.Layer));
  const trace = life.TraceId ? lineage.Traces.find((t) => t.Id === life.TraceId) : null;
  if (DEATHS.includes(life.Ending)) {
    lines.push(InheritanceText.TraceLeft(layerTitle));
    if (trace && countCandidates(trace).total) {
      const c = countCandidates(trace);
      lines.push(InheritanceText.CandidateCount(c.cards, c.pages, c.scribbles));
      lines.push(InheritanceText.TraceVisit(life.Layer, InheritanceRules.LegacyPicks));
    } else {
      lines.push(InheritanceText.NothingLeft);
    }
    if (trace && trace.Withheld.length) lines.push(InheritanceText.WithheldLine(trace.Withheld.map((w) => w.Name)));
  } else {
    lines.push(InheritanceText.JournalPasses);
    lines.push(InheritanceText.SurvivorBonusLine(lineage.Survivor ? lineage.Survivor.BonusLives : InheritanceRules.SurvivorBonusLives));
    if (life.Ending === LifeEnding.Completed && trace) lines.push(InheritanceText.ClearedTraceLeft(layerTitle));
    if (life.ReplacedSurvivor !== null) {
      const old = lineage.Lives.find((l) => l.Generation === life.ReplacedSurvivor);
      lines.push(InheritanceText.SurvivorReplaced(life.ReplacedSurvivor, old ? old.Name : ''));
    }
  }
  const heir = nextHeir(lineage);
  return deepFreeze({
    Ending: life.Ending,
    Title: InheritanceText.EndingTitle[life.Ending],
    Sentence: InheritanceText.EndingSentence(life.Ending, life.Layer),
    HeirLine: InheritanceText.HeirLine(life.Generation, life.Name),
    Lines: lines,
    NextLine: InheritanceText.NextHeirLine(heir.Generation, heir.Name),
    Button: InheritanceText.ToHallButton,
  });
}

// ---------------------------------------------------------------------------------------------
// Saving (tier1 R1-16: a death, an end and a taken trace are saved the moment they happen)
// ---------------------------------------------------------------------------------------------

/** The lineage as a plain JSON value. It already is one; this copies it. */
export function toJSON(lineage) {
  return JSON.parse(JSON.stringify(lineage));
}

function checkJournalShape(j, what) {
  if (j === null) return null;
  if (typeof j !== 'object') throw new InheritanceRuleException(`bad save: ${what} journal`);
  return normalizeJournal(j);
}

function checkCandidate(c, traceId) {
  if (!c || typeof c.Id !== 'string' || !isMember(CandidateKind, c.Kind) || typeof c.Name !== 'string')
    throw new InheritanceRuleException(`bad save: a candidate of ${traceId}`);
  if (c.Kind === CandidateKind.Card) {
    requireInt(c.Rank, `rank in ${traceId}`, 0, 3);
    requireInt(c.Ticks, `ticks in ${traceId}`, 0);
  }
  if (c.Kind === CandidateKind.EnemyPage) requireInt(c.Disclosure, `disclosure in ${traceId}`, 1, InheritanceRules.MaxDisclosure);
  return { ...c };
}

function checkLifeRecord(l, open) {
  if (!l || typeof l !== 'object') throw new InheritanceRuleException('bad save: a life');
  requireInt(l.Generation, 'generation', 1);
  if (typeof l.Name !== 'string') throw new InheritanceRuleException('bad save: a life name');
  requireInt(l.Tier, 'tier', 0, AttireTiers.length - 1);
  seedOf(l.Seed);
  if (open) {
    if (l.Ending !== null) throw new InheritanceRuleException('bad save: the current life has an ending');
  } else {
    if (!isMember(LifeEnding, l.Ending)) throw new InheritanceRuleException(`bad save: ending ${l.Ending}`);
    requireInt(l.Layer, 'layer', 1, SevenLayers.Count);
  }
  return {
    Generation: l.Generation,
    Name: l.Name,
    Tier: l.Tier,
    Seed: l.Seed,
    SurvivorBonus: Boolean(l.SurvivorBonus),
    SurvivorFrom: l.SurvivorFrom ?? null,
    InheritedJournal: Boolean(l.InheritedJournal),
    TakenTraceId: l.TakenTraceId ?? null,
    Ending: l.Ending ?? null,
    Layer: l.Layer ?? null,
    NodeId: l.NodeId ?? null,
    NodeKind: l.NodeKind ?? null,
    TraceId: l.TraceId ?? null,
    CollectedBy: l.CollectedBy ?? null,
    ReplacedSurvivor: l.ReplacedSurvivor ?? null,
  };
}

/** A saved lineage back, checked. Throws InheritanceRuleException on a save it cannot read. */
export function fromJSON(data) {
  if (!data || typeof data !== 'object') throw new InheritanceRuleException('bad save: not an object');
  if (data.Version !== SAVE_VERSION) throw new InheritanceRuleException(`bad save: version ${data.Version}`);
  seedOf(data.Seed);
  if (!Array.isArray(data.Lives) || !Array.isArray(data.Traces)) throw new InheritanceRuleException('bad save: lists');
  const lives = data.Lives.map((l) => checkLifeRecord(l, false));
  lives.forEach((l, i) => {
    if (l.Generation !== i + 1) throw new InheritanceRuleException('bad save: generations out of order');
  });
  const current = data.Current ? checkLifeRecord(data.Current, true) : null;
  if (current && current.Generation !== lives.length + 1) throw new InheritanceRuleException('bad save: current generation');
  const traces = data.Traces.map((t) => {
    if (!t || typeof t.Id !== 'string') throw new InheritanceRuleException('bad save: a trace');
    requireInt(t.Layer, 'trace layer', 1, SevenLayers.Count);
    requireInt(t.Generation, 'trace generation', 1);
    return {
      Id: t.Id,
      Generation: t.Generation,
      Name: String(t.Name),
      Layer: t.Layer,
      NodeId: t.NodeId ?? null,
      NodeKind: t.NodeKind ?? null,
      Ending: t.Ending,
      Cleared: Boolean(t.Cleared),
      Candidates: (t.Candidates ?? []).map((c) => checkCandidate(c, t.Id)),
      Withheld: (t.Withheld ?? []).map((w) => ({ CardId: String(w.CardId), Rank: requireInt(w.Rank, 'withheld rank', 0, 3), Name: String(w.Name) })),
    };
  });
  let survivor = null;
  if (data.Survivor) {
    const s = data.Survivor;
    requireInt(s.Generation, 'survivor generation', 1);
    survivor = {
      Generation: s.Generation,
      Name: String(s.Name),
      BonusLivesLeft: requireInt(s.BonusLivesLeft, 'survivor bonus', 0),
      BonusLives: requireInt(s.BonusLives ?? s.BonusLivesLeft, 'survivor bonus lives', 0),
      Journal: checkJournalShape(s.Journal ?? null, 'survivor'),
    };
  }
  const player = data.Player ?? {};
  return deepFreeze({
    Version: SAVE_VERSION,
    Seed: seedOf(data.Seed).toString(),
    Lives: lives,
    Current: current,
    Traces: traces,
    Survivor: survivor,
    Player: { CarvingTaken: Boolean(player.CarvingTaken), RootDefeated: Boolean(player.RootDefeated) },
  });
}

/** JSON text for localStorage. */
export function serialize(lineage) {
  return JSON.stringify(lineage);
}

/** A lineage from JSON text. */
export function deserialize(text) {
  return fromJSON(JSON.parse(text));
}

/** Everything above under one name, for callers that prefer `Inheritance.startLife(...)`. */
export const Inheritance = Object.freeze({
  Rules: InheritanceRules,
  Text: InheritanceText,
  newLineage,
  nextHeir,
  heirName,
  tierAfter,
  lifeSeed,
  startLife,
  traceForLayer,
  traceLayers,
  mapSpecFor,
  placeFromState,
  recordDeath,
  recordSurvival,
  recordEnd,
  noteCarving,
  takeTrace,
  mergeJournal,
  heirJournal,
  mergeMastery,
  normalizeJournal,
  emptyJournal,
  hallView,
  traceNodeView,
  pickState,
  endingView,
  toJSON,
  fromJSON,
  serialize,
  deserialize,
});
