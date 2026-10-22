// Port of unity-port/unity-project-kit/Assets/View/Depiction/Bridge/DepartureBuilder.cs
//
// The 出立 step after the deck (#58): the three tool slots, the three consumable slots, the start a
// tool moves back, the other battle tools' amounts (#452), and the talent card. Pure: BattleCore
// only, no DOM. The rules themselves are BattleCore.DeckRules'; this class keeps what the player
// picked and prints it.
//
// The tools and consumables are the dungeon side's (tools_and_consumables_v4.md, read only here).
// This module does not read DungeonContent: the caller hands them in as SupplyOptions (Unity's
// DepartureSupplies), carrying only what the battle reads: how many cells a tool moves the start,
// and the amounts of 息継ぎの笛 / 竜鱗の欠片 / 竜語の符 (battle_core_v4 §9.1).
//
// SupplyOption's constructor is positional like C#'s: (id, name, note, startCellsBack = 0,
// startStaminaBonus = 0, firstHitGuard = 0, bossWordWard = 0); a C# call with named arguments
// (startStaminaBonus: 3) passes 0 for the ones it skips.
//
// JS extension (not in the C#), docs/EXTENSIONS.md 「出立の枠（DeckLimits）」: `new DepartureBuilder(tools,
// consumables, limits)` holds the slots to a DeckLimits (`Limits`): CanAddTool / CanAddConsumable,
// Validate / CanSetOut, ToolsLine / ConsumablesLine and the instance Title / ToolHeading /
// ConsumableHeading (static TitleFor / ToolHeadingFor / ConsumableHeadingFor) read it. Left out it is
// DeckLimits.Default (the C#'s 3 and 3) and the class is the C#'s; the static Title / ToolHeading /
// ConsumableHeading stay the C#'s. concept-v3.md §14 生存者ボーナス: a survivor's heir has 4 tool slots.
import { idiv, ArgumentException, ArgumentNullException, ArgumentOutOfRangeException } from '../../shared/cs.js';
import { CardCatalog } from '../../core/battle/CardCatalog.js';
import { DeckRules, Departure, DeckLimits } from '../../core/battle/DeckRules.js';
import { DeckPage } from './DeckBuilder.js';

/** new ArgumentOutOfRangeException(paramName, actualValue, message), with .NET's Message text. */
function outOfRange(paramName, actualValue, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${paramName}')\nActual value was ${actualValue}.`);
}

/** new ArgumentNullException(paramName), with .NET's Message text. */
function argNull(paramName) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${paramName}')`);
}

/** One tool or consumable as the departure screen offers it. */
export class SupplyOption {
  constructor(id, name, note, startCellsBack = 0, startStaminaBonus = 0, firstHitGuard = 0, bossWordWard = 0) {
    if (id == null || id === '') throw new ArgumentException(`A supply needs an id. (Parameter 'id')`);
    this.Id = id;
    this.Name = name ?? id;
    /** The catalogue's one line about it (ItemDef.Note), printed as it is. */
    this.Note = note ?? '';
    /** Cells the player's start moves back while it is carried (間合いの履: 1). 0 for the rest. */
    this.StartCellsBack = Math.max(0, startCellsBack);
    /** Stamina added at the start of each battle (息継ぎの笛: 3). 0 for the rest. */
    this.StartStaminaBonus = Math.max(0, startStaminaBonus);
    /** Guard given once a battle, before the first enemy blow that reaches the player (竜鱗の欠片: 5). 0 for the rest. */
    this.FirstHitGuard = Math.max(0, firstHitGuard);
    /** Stacks lower the boss-only words sit on the player (竜語の符: 1). 0 for the rest. */
    this.BossWordWard = Math.max(0, bossWordWard);
  }

  /** Whether the battles of this demo feel it: the four battle tools since #452, none of the exploration ones. */
  get WorksInBattle() {
    return this.StartCellsBack > 0 || this.StartStaminaBonus > 0 || this.FirstHitGuard > 0 || this.BossWordWard > 0;
  }
}

export class DepartureBuilder {
  /** `limits` (JS extension): the DeckLimits the slots and the deck are held to; left out, the C#'s. */
  constructor(tools, consumables, limits = DeckLimits.Default) {
    this._tools = [];
    this._consumables = [];
    if (tools == null) throw argNull('tools');
    this.ToolOptions = tools;
    if (consumables == null) throw argNull('consumables');
    this.ConsumableOptions = consumables;
    /** JS extension: the slot counts (DeckLimits; DeckLimits.Default is the C#'s 3 tools and 3 consumables). */
    this.Limits = DeckLimits.From(limits);
    /** The card picked as the talent, or null. Kept while the deck changes; TalentIn drops it when its kind leaves. (C#: private set.) */
    this.Talent = null;
  }

  /** The ids in the tool slots, in the order they went in. The same tool may fill two. */
  get Tools() { return this._tools; }

  get Consumables() { return this._consumables; }

  // ---- the slots ----

  ToolCount(id) { return DepartureBuilder.Count(this._tools, id); }

  ConsumableCount(id) { return DepartureBuilder.Count(this._consumables, id); }

  /** (JS: the slot count is Limits.ToolSlots — 4 with the survivor's bonus, concept-v3.md §14.) */
  CanAddTool(id) { return this._tools.length < this.Limits.ToolSlots && DepartureBuilder.Find(this.ToolOptions, id) != null; }

  CanAddConsumable(id) { return this._consumables.length < this.Limits.ConsumableSlots && DepartureBuilder.Find(this.ConsumableOptions, id) != null; }

  AddTool(id) {
    if (!this.CanAddTool(id)) return false;
    this._tools.push(id);
    return true;
  }

  RemoveTool(id) { return listRemove(this._tools, id); }

  AddConsumable(id) {
    if (!this.CanAddConsumable(id)) return false;
    this._consumables.push(id);
    return true;
  }

  RemoveConsumable(id) { return listRemove(this._consumables, id); }

  // ---- the start ----

  /** The cells the carried tools move the start back, before DeckRules keeps it on the line. */
  get StartCellsBack() {
    let back = 0;
    for (const id of this._tools) back += DepartureBuilder.Find(this.ToolOptions, id).StartCellsBack;
    return back;
  }

  get PlayerStartCell() { return DeckRules.PlayerStartCell(this.StartCellsBack); }

  get StartGap() { return DeckRules.StartGap(this.StartCellsBack); }

  // ---- the other battle tools (#452) ----

  /** The stamina the carried tools add at each battle's start (息継ぎの笛 +3 each). */
  get StartStaminaBonus() { return this.Sum((o) => o.StartStaminaBonus); }

  /** The Guard the carried tools give before the first enemy blow of each battle (竜鱗の欠片 5 each). */
  get FirstHitGuard() { return this.Sum((o) => o.FirstHitGuard); }

  /** How many stacks lower the boss-only words sit (竜語の符 1 each). */
  get BossWordWard() { return this.Sum((o) => o.BossWordWard); }

  /** private */
  Sum(amount) {
    let total = 0;
    for (const id of this._tools) total += amount(DepartureBuilder.Find(this.ToolOptions, id));
    return total;
  }

  /** 「開始のマス 2・開始の間合い 3」, and the tool that moved it: 「開始のマス 1・開始の間合い 4（間合いの履）」. */
  get StartLine() {
    const line = '開始のマス ' + this.PlayerStartCell + '・開始の間合い ' + this.StartGap;
    const movers = [];
    for (const id of this._tools) {
      const option = DepartureBuilder.Find(this.ToolOptions, id);
      if (option.StartCellsBack > 0 && !movers.includes(option.Name)) movers.push(option.Name);
    }
    return movers.length === 0 ? line : line + '（' + movers.join('、') + '）';
  }

  // ---- the talent ----

  /** Picks the talent. Only a kind the deck holds is taken; anything else is refused. */
  ChooseTalent(deck, id) {
    if (deck == null) throw argNull('deck');
    if (id == null || id === '' || deck.CountOf(id) === 0) return false;
    this.Talent = id;
    return true;
  }

  /** The talent if its kind is still in `deck`, else null. */
  TalentIn(deck) {
    if (deck == null) throw argNull('deck');
    return this.Talent != null && deck.CountOf(this.Talent) > 0 ? this.Talent : null;
  }

  /** The deck's kinds in canon order: what the talent may be. */
  static TalentChoices(deck) {
    if (deck == null) throw argNull('deck');
    const kinds = [];
    for (const def of CardCatalog.All) {
      if (deck.CountOf(def.Id) > 0) kinds.push(def);
    }
    return kinds;
  }

  /** One page of TalentChoices. The page is clamped. */
  static TalentPage(deck, page, perPage) {
    if (perPage < 1) throw outOfRange('perPage', perPage, '1 or more.');
    const kinds = DepartureBuilder.TalentChoices(deck);
    const pages = Math.max(1, idiv(kinds.length + perPage - 1, perPage));
    page = Math.max(0, Math.min(pages - 1, page));
    const first = page * perPage;
    return new DeckPage(kinds.slice(first, first + Math.min(perPage, kinds.length - first)), page, pages, kinds.length);
  }

  // ---- setting out ----

  /** What sets out with `deck`: its cards, the slots, the start and the talent. */
  Build(deck) {
    if (deck == null) throw argNull('deck');
    return new Departure(deck.Build(), this._tools.slice(), this._consumables.slice(), this.StartCellsBack, this.TalentIn(deck),
      this.StartStaminaBonus, this.FirstHitGuard, this.BossWordWard);
  }

  /**
   * (JS: DeckRules.Validate with limits made of the deck builder's bounds — the same ones its IsValid
   * reads, so StatusText and CanSetOut agree — and this builder's slots. Both default to the C#'s.)
   */
  Validate(deck) {
    if (deck == null) throw argNull('deck');
    return DeckRules.Validate(this.Build(deck), deck.Owned, this.LimitsWith(deck));
  }

  /** private (JS extension): the deck's DeckMin / DeckMax (when it carries limits) with this builder's slots. */
  LimitsWith(deck) {
    const own = deck.Limits instanceof DeckLimits ? deck.Limits : this.Limits;
    if (own === this.Limits) return this.Limits;
    return this.Limits.With({ DeckMin: own.DeckMin, DeckMax: own.DeckMax });
  }

  CanSetOut(deck) { return this.Validate(deck).Ok; }

  // ---- what the screen prints ----

  static get Title() { return DepartureBuilder.TitleFor(DeckLimits.Default); }

  static get ToolHeading() { return DepartureBuilder.ToolHeadingFor(DeckLimits.Default); }

  static get ConsumableHeading() { return DepartureBuilder.ConsumableHeadingFor(DeckLimits.Default); }

  /** JS extension: 「出立の支度（ツール 4 枠・消耗品 3 枠・才能 1 枚）」 for some limits. */
  static TitleFor(limits) {
    const L = DeckLimits.From(limits);
    return '出立の支度（ツール ' + L.ToolSlots + ' 枠・消耗品 ' + L.ConsumableSlots + ' 枠・才能 1 枚）';
  }

  /** JS extension: 「ツール（探索と戦闘で共通の 4 枠。同じものを 2 つ持てます）」 for some limits. */
  static ToolHeadingFor(limits) {
    return 'ツール（探索と戦闘で共通の ' + DeckLimits.From(limits).ToolSlots + ' 枠。同じものを 2 つ持てます）';
  }

  /** JS extension: 「消耗品（3 枠。拠点へは持ち帰りません）」 for some limits. */
  static ConsumableHeadingFor(limits) {
    return '消耗品（' + DeckLimits.From(limits).ConsumableSlots + ' 枠。拠点へは持ち帰りません）';
  }

  /** JS extension: this builder's title (TitleFor(Limits)); the static Title is the C#'s. */
  get Title() { return DepartureBuilder.TitleFor(this.Limits); }

  /** JS extension: this builder's tool heading; the static ToolHeading is the C#'s. */
  get ToolHeading() { return DepartureBuilder.ToolHeadingFor(this.Limits); }

  /** JS extension: this builder's consumable heading; the static ConsumableHeading is the C#'s. */
  get ConsumableHeading() { return DepartureBuilder.ConsumableHeadingFor(this.Limits); }

  /**
   * JS extension: the label over what the limits raise (concept-v3.md §14 生存者ボーナス; the hall's
   * InheritanceText says the same word). Showing it at 出立 is 試作で決めたこと 6 in docs/EXTENSIONS.md
   * 「出立の枠」: the documents show the bonus in 継承の間 (CAMP_FACILITIES_DESIGN.md §3.1).
   */
  static get BonusLabel() { return '生存者ボーナス'; }

  /**
   * JS extension: the tool slots the limits add over the C#'s 3, in the hall's words
   * (InheritanceText.SurvivorTools): 「ツール枠 +1（3 → 4）」. "" when the slots are the C#'s.
   */
  get RaisedLine() {
    const from = DeckLimits.Default.ToolSlots;
    const to = this.Limits.ToolSlots;
    return to > from ? 'ツール枠 +' + (to - from) + '（' + from + ' → ' + to + '）' : '';
  }

  /** JS extension: whether tool slot `index` (0-based) is one the limits add (the survivor's 4th). */
  IsBonusToolSlot(index) {
    return index >= DeckLimits.Default.ToolSlots && index < this.Limits.ToolSlots;
  }

  static get TalentHeading() { return '才能（デッキの札から 1 枚。その札だけ習熟が 2 倍で溜まります）'; }

  static get SetOutLabel() { return '出立する'; }

  static get BackLabel() { return 'デッキに戻る'; }

  /** 「ツール 2 / 3: 間合いの履、防瘴の面」, or 「ツール 0 / 3: なし」. */
  get ToolsLine() { return DepartureBuilder.SlotsLine('ツール', this._tools, this.ToolOptions, this.Limits.ToolSlots); }

  get ConsumablesLine() { return DepartureBuilder.SlotsLine('消耗品', this._consumables, this.ConsumableOptions, this.Limits.ConsumableSlots); }

  /** The line an option has on the screen: its name and note, and a word when this demo's battles do not feel it. */
  static LineOf(option) {
    if (option == null) throw argNull('option');
    return option.Name + '　' + option.Note + (option.WorksInBattle ? '' : '（この試験の戦闘では効果が出ません）');
  }

  /** 「才能: 突き」, or 「才能: まだ選んでいません」. */
  TalentLine(deck) {
    const id = this.TalentIn(deck);
    return '才能: ' + (id == null ? 'まだ選んでいません' : CardCatalog.ById(id).Name);
  }

  /** The one line beside 「出立する」: what is still missing, or that it may set out. */
  StatusText(deck) {
    if (deck == null) throw argNull('deck');
    if (!deck.IsValid) return 'デッキが決まりに合っていません。デッキに戻って直してください';
    if (this.TalentIn(deck) == null) return '才能にする札を 1 枚選ぶと出立できます';
    return '出立できます';
  }

  // ---- helpers ----

  /** private */
  static SlotsLine(label, ids, options, slots) {
    const names = [];
    for (const id of ids) names.push(DepartureBuilder.Find(options, id).Name);
    return label + ' ' + ids.length + ' / ' + slots + ': ' + (names.length === 0 ? 'なし' : names.join('、'));
  }

  /** private */
  static Find(options, id) {
    if (id == null || id === '') return null;
    for (const option of options) {
      if (option.Id === id) return option;
    }
    return null;
  }

  /** private */
  static Count(ids, id) {
    let n = 0;
    for (const each of ids) {
      if (each === id) n++;
    }
    return n;
  }
}

/** List<T>.Remove: the first occurrence, true when one was taken out. */
function listRemove(list, item) {
  const at = list.indexOf(item);
  if (at < 0) return false;
  list.splice(at, 1);
  return true;
}
