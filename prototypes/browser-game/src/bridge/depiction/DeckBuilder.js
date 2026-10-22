// Port of unity-port/unity-project-kit/Assets/View/Depiction/Bridge/DeckBuilder.cs
//
// The deck the demo fights with (#190), built from the eighty. Pure: BattleCore only, no DOM, so
// every rule the deck screen shows — the 20〜40 cards, the three of a kind (and no cap on stance
// cards, §4 / §8 v4.4), what a filter keeps, what a saved string restores to, every word printed
// about a card — is decided here. The screen shows what this class says and stores the string it
// hands out.
//
// JS shapes of C# signatures:
// - KeyValuePair<string, T> (AttributeOptions, CostOptions) → { Key, Value }.
// - LoadOrPrototype(saved, text, out string notice): called with a third argument (any value, e.g.
//   true) it is the out overload and returns { ok, value } — ok the C# return value (the DeckBuilder),
//   value the notice. With two arguments it returns the DeckBuilder alone, as C# does.
// - new DeckFilter { Attribute = a, Cost = c } → new DeckFilter({ Attribute: a, Cost: c }) (or set the fields).
// - Owned (IReadOnlyCollection<string>) is an array of ids.
//
// JS extension (not in the C#), docs/EXTENSIONS.md 「出立の枠（DeckLimits）」: a builder carries a
// DeckLimits (`new DeckBuilder(owned, limits)`, `Limits`), and every rule and every line that prints the
// cap read it — CanAdd / Add / Validate / StatusText / Random / Prototype / Load / Read /
// LoadOrPrototype, the instance Title and the static TitleFor(limits). Left out it is
// DeckLimits.Default (the C# constants) and the class is the C#'s. The static Title stays the C#'s.
// concept-v3.md §14 生存者ボーナス: a survivor's heir builds up to 45 cards (DeckLimits from
// Inheritance.startLife). ReadOverCap / OverCapNotice / WithOwned open a deck saved over the life's cap
// (the bonus life's 45 after the bonus ended) with every card and the reason, instead of Read's empty
// deck; StatusText then asks for the extra cards to come out.
import { hasFlag, idiv, ArgumentOutOfRangeException } from '../../shared/cs.js';
import { BattleAttribute, TargetKind, CardInstance } from '../../core/battle/Types.js';
import { Constants } from '../../core/battle/Constants.js';
import { CardCatalog, PrototypeDeck } from '../../core/battle/CardCatalog.js';
import { Cards } from '../../core/battle/Cards.js';
import { DeckRules, DeckLimits } from '../../core/battle/DeckRules.js';
import { EnemyAi } from '../../core/battle/EnemyAi.js';
import { SeededRng } from '../../core/battle/IRng.js';
import { CoreText } from './CoreText.js';

/** JS extension (X-17): the battle's attribute words (CardFaceText.AttributeWord), by flag. */
const SHOWN_WORDS = new Map([
  [BattleAttribute.Attack, '攻撃'], [BattleAttribute.Guard, '防御'], [BattleAttribute.Skill, 'スキル'], [BattleAttribute.Stance, 'スタンス'],
]);

/** new ArgumentOutOfRangeException(paramName, actualValue, message), with .NET's Message text. */
function outOfRange(paramName, actualValue, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${paramName}')\nActual value was ${actualValue}.`);
}

/** What the deck screen lists: an attribute (None = all) and a cost (0 = all). */
export class DeckFilter {
  constructor(init) {
    this.Attribute = BattleAttribute.None;
    this.Cost = 0;
    if (init) Object.assign(this, init);
  }

  Keeps(def) {
    if (def == null) return false;
    if (this.Attribute !== BattleAttribute.None && def.Attribute !== this.Attribute) return false;
    return this.Cost === 0 || def.Cost === this.Cost;
  }
}

DeckFilter.All = new DeckFilter();

/** One page of the deck screen's list. */
export class DeckPage {
  constructor(cards, index, count, kept) {
    this.Cards = cards;
    this.Index = index;
    this.Count = count;
    this.Kept = kept;
  }

  /** 「2 / 4 ページ（38 種）」. */
  get Text() { return (this.Index + 1) + ' / ' + this.Count + ' ページ（' + this.Kept + ' 種）'; }
}

export class DeckBuilder {
  /**
   * `owned`: the kinds the character owns (§8: at most 80). Null owns the whole catalog, which is
   * what the demo hands every character until the inheritance side decides what a life starts with.
   * `limits` (JS extension): the DeckLimits the deck is held to; left out, the C# constants.
   */
  constructor(owned = null, limits = DeckLimits.Default) {
    this._counts = new Map();
    /** JS extension: the deck's bounds (DeckLimits; DeckLimits.Default is the C#'s 20〜40). */
    this.Limits = DeckLimits.From(limits);
    if (owned == null) {
      const all = [];
      for (const def of CardCatalog.All) all.push(def.Id);
      owned = all;
    }
    /** The kinds that may go in (#58). A kind outside it has its ＋ greyed out and fails Validate. */
    this.Owned = owned;
  }

  /** Cards in the deck. */
  get Total() {
    let total = 0;
    for (const n of this._counts.values()) total += n;
    return total;
  }

  /** Copies of one kind in the deck. */
  CountOf(id) {
    return id != null && this._counts.has(id) ? this._counts.get(id) : 0;
  }

  /** Cards of the deck that are among `kinds`. */
  CountIn(kinds) {
    let n = 0;
    for (const def of kinds) n += this.CountOf(def.Id);
    return n;
  }

  /** Cards of the deck that are stances (v4.4: counted for the screen, not capped). */
  get StanceCount() {
    let n = 0;
    for (const def of CardCatalog.All) {
      if (Cards.IsStanceCard(def)) n += this.CountOf(def.Id);
    }
    return n;
  }

  /**
   * §8 through DeckRules.CanAdd (#58): a fourth copy, a 41st card, or a kind not owned is refused,
   * and so is an id the catalog does not know. (JS extension: the card past Limits.DeckMax — the 46th
   * with the survivor's bonus.)
   */
  CanAdd(id) {
    return DeckBuilder.Known(id) && DeckRules.CanAdd(this.Build(), id, this.Owned, this.Limits);
  }

  Add(id) {
    if (!this.CanAdd(id)) return false;
    this._counts.set(id, this.CountOf(id) + 1);
    return true;
  }

  Remove(id) {
    const n = this.CountOf(id);
    if (n === 0) return false;
    if (n === 1) this._counts.delete(id);
    else this._counts.set(id, n - 1);
    return true;
  }

  /** The deck laid out in canon order, instance ids "thrust-0", "thrust-1". */
  Build() {
    const deck = [];
    for (const def of CardCatalog.All) {
      const n = this.CountOf(def.Id);
      for (let copy = 0; copy < n; copy++) deck.push(new CardInstance(def.Id + '-' + copy, def));
    }
    return deck;
  }

  /**
   * §8 through the core's own check (DeckRules.ValidateDeck, #58): 20〜40 cards, three of a kind at
   * most, every kind owned. Stance cards have no cap of their own (§4 / §8, v4.4; §24.1 retired
   * §19.6 S15's three).
   */
  Validate() {
    return DeckRules.ValidateDeck(this.Build(), this.Owned, this.Limits);
  }

  get IsValid() { return this.Validate().Ok; }

  /** The one line the screen shows beside 「出立の支度へ」: how many cards, and what is still missing. */
  get StatusText() {
    const total = this.Total;
    const min = this.Limits.DeckMin;
    if (total < min) return total + ' 枚です。あと ' + (min - total) + ' 枚入れると戦えます';
    // JS extension: a deck over its cap (ReadOverCap: a survivor's 45 opened after the bonus ended).
    // The C# never holds one, since Add and Read refuse the card past the cap.
    const max = this.Limits.DeckMax;
    if (total > max) return total + ' 枚です。あと ' + (total - max) + ' 枚外すと戦えます';
    return total + ' 枚です。' + min + '〜' + max + ' 枚、1 種 ' + Constants.CopiesMax
      + ' 枚を満たしています';
  }

  /**
   * JS extension: what the limits raise over the C#'s cap, in the hall's words
   * (InheritanceText.SurvivorDeck): 「デッキ上限 +5（40 → 45）」. "" when the cap is the C#'s 40.
   * The 出立 screen shows it beside the deck step's title: docs/EXTENSIONS.md 「出立の枠」 試作で決めたこと 6.
   */
  get RaisedLine() {
    const from = DeckLimits.Default.DeckMax;
    const to = this.Limits.DeckMax;
    return to > from ? 'デッキ上限 +' + (to - from) + '（' + from + ' → ' + to + '）' : '';
  }

  /** The cards a filter keeps, in canon order. */
  Filter(filter) {
    const kept = [];
    for (const def of CardCatalog.All) {
      if ((filter ?? DeckFilter.All).Keeps(def)) kept.push(def);
    }
    return kept;
  }

  /** One page of the list: the cards a filter keeps, `perPage` at a time. The page is clamped. */
  Page(filter, page, perPage) {
    if (perPage < 1) throw outOfRange('perPage', perPage, '1 or more.');
    const kept = this.Filter(filter);
    const pages = Math.max(1, idiv(kept.length + perPage - 1, perPage));
    page = Math.max(0, Math.min(pages - 1, page));
    const first = page * perPage;
    const cards = kept.slice(first, first + Math.min(perPage, kept.length - first));
    return new DeckPage(cards, page, pages, kept.length);
  }

  // ---- what the screen prints ----

  /** The deck screen's title, with §8's rule in it. */
  static get Title() {
    return DeckBuilder.TitleFor(DeckLimits.Default);
  }

  /** JS extension: the title for some limits: 「デッキを組む（80 種から 20〜45 枚、1 種 3 枚まで）」. */
  static TitleFor(limits) {
    const L = DeckLimits.From(limits);
    return 'デッキを組む（' + Constants.OwnedKindsMax + ' 種から ' + L.DeckMin + '〜' + L.DeckMax
      + ' 枚、1 種 ' + Constants.CopiesMax + ' 枚まで）';
  }

  /** JS extension: this builder's title (TitleFor(Limits)); the static Title is the C#'s. */
  get Title() {
    return DeckBuilder.TitleFor(this.Limits);
  }

  /** The attribute filters the screen offers, in the §2.2 order after 「全て」. */
  static get AttributeOptions() {
    const options = [{ Key: '全て', Value: BattleAttribute.None }];
    for (const attribute of [BattleAttribute.Attack, BattleAttribute.Guard, BattleAttribute.Skill, BattleAttribute.Stance]) {
      options.push({ Key: DeckBuilder.AttributeWords(attribute), Value: attribute });
    }
    return options;
  }

  /** The cost filters the screen offers: 「全コスト」, then each cost §3 allows. */
  static get CostOptions() {
    const options = [{ Key: '全コスト', Value: 0 }];
    for (let cost = Constants.CostMin; cost <= Constants.CostMax; cost++) {
      options.push({ Key: 'コスト ' + cost, Value: cost });
    }
    return options;
  }

  /** The line a card has in the list: 「突き　コスト 3　攻撃」. */
  static LineOf(def) {
    return def.Name + '　コスト ' + def.Cost + '　' + DeckBuilder.AttributeWords(def.Attribute);
  }

  /** How many of the card the deck holds, as printed beside it: "×2", or "" for none. */
  CountText(id) {
    const n = this.CountOf(id);
    return n === 0 ? '' : '×' + n;
  }

  /**
   * What pressing a card shows: its name; its cost, attributes and reach; what it does (the
   * same sentences the hand prints); its trait; and its flavour line. One line each.
   */
  static DetailOf(def) {
    const aims = EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets);
    const lines = [
      def.Name,
      'コスト ' + def.Cost + '　' + DeckBuilder.AttributeWords(def.Attribute)
        + (aims ? '　届く間合い ' + def.Face.ReachOrDefault.ToText() : '　自分向き'),
      CoreText.Describe(def.Face, def.Attributes),
    ];
    const trait = CoreText.TraitLines(def);
    lines.push(trait.length > 0 ? '特性: ' + trait : '特性: なし（素直な札）');
    if (def.Description != null && def.Description !== '') lines.push('「' + def.Description + '」');
    return lines;
  }

  /** The deck as the screen lists it, one kind a line in canon order: 「突き ×2」. */
  DeckLines() {
    const lines = [];
    for (const def of CardCatalog.All) {
      const n = this.CountOf(def.Id);
      if (n > 0) lines.push(def.Name + ' ×' + n);
    }
    return lines;
  }

  /**
   * JS extension (docs/EXTENSIONS.md X-17): the attribute words the screens print — the battle's
   * (CardFaceText.AttributeWord: 「攻撃」「防御」「スキル」「スタンス」, battle-visual-v1 §15 / battle_ui_ux_v2 §1.1 B6),
   * which the card hover, the 連動 trait, the 用語 page and the result card all use. The C# deck builder's own words
   * (AttributeWords: 技 / 構え) stay as the C# writes them; 構え is the end-of-turn Guard rule on every other screen.
   */
  static ShownAttributeWords(attributes) {
    const words = [];
    for (const flag of [BattleAttribute.Attack, BattleAttribute.Guard, BattleAttribute.Skill, BattleAttribute.Stance]) {
      if (hasFlag(attributes, flag)) words.push(SHOWN_WORDS.get(flag));
    }
    return words.join('＋');
  }

  /** JS extension (X-17): AttributeOptions with the shown words. */
  static get ShownAttributeOptions() {
    return DeckBuilder.AttributeOptions.map((o) => (o.Value === BattleAttribute.None ? o : { Key: DeckBuilder.ShownAttributeWords(o.Value), Value: o.Value }));
  }

  /** JS extension (X-17): DetailOf with the shown words in its second line. */
  static ShownDetailOf(def) {
    const lines = DeckBuilder.DetailOf(def);
    const words = DeckBuilder.AttributeWords(def.Attribute);
    const head = 'コスト ' + def.Cost + '　';
    if (lines[1].startsWith(head + words)) lines[1] = head + DeckBuilder.ShownAttributeWords(def.Attribute) + lines[1].slice((head + words).length);
    return lines;
  }

  /** The word for the attribute a card counts as (§2.1, v4.4): 「攻撃」「防御」「技」「構え」. */
  static AttributeWords(attributes) {
    const words = [];
    if (hasFlag(attributes, BattleAttribute.Attack)) words.push('攻撃');
    if (hasFlag(attributes, BattleAttribute.Guard)) words.push('防御');
    if (hasFlag(attributes, BattleAttribute.Skill)) words.push('技');
    if (hasFlag(attributes, BattleAttribute.Stance)) words.push('構え');
    return words.join('＋');
  }

  // ---- presets ----

  /**
   * JS extension (docs/EXTENSIONS.md 「出立の枠（DeckLimits）」, 試作で決めたこと): `kinds` and then the
   * prototype deck's kinds, each once. Expedition.BeginLife (Expedition.cs) owns the kinds of the deck a
   * life sets out with on top of the initial forty, and 盾打ち (#51) is the prototype's one kind outside
   * them; a departure that offers 「試作デッキ」 owns its kinds too, so the preset opens at its full 20
   * instead of losing its two 盾打ち.
   */
  static WithPrototypeKinds(kinds) {
    const all = [...new Set(kinds ?? [])];
    const seen = new Set(all);
    for (const def of PrototypeDeck.Kinds) {
      if (seen.has(def.Id)) continue;
      seen.add(def.Id);
      all.push(def.Id);
    }
    return all;
  }

  /** The slice's ten kinds × 2 (#71), the deck the battle scene fought with until #190. (JS: `limits` is the builder's.) */
  static Prototype(limits = DeckLimits.Default) {
    const builder = new DeckBuilder(null, limits);
    for (const def of PrototypeDeck.Kinds) {
      for (let i = 0; i < PrototypeDeck.Copies; i++) builder.Add(def.Id);
    }
    return builder;
  }

  /**
   * A random deck of `size` cards (clamped to 20〜40), fixed by the seed. It always passes §8: a
   * kind that already holds three is refused by Add and another is drawn. Stance cards have no cap
   * of their own (battle_core_v4 §4 / §8, v4.4).
   *
   * It also always holds one card that closes in from gap 3 even under 鈍足 (駆け込み, 疾風突き,
   * or 猪突猛進 since it reaches 3 in v4.5) and at least RandomForwardMin cards that step forward.
   * Without them a random deck can be locked out by 大黒蛇 セルク, which never moves and binds the
   * feet every phase (#196), and the demo would hand the player a battle that cannot end.
   *
   * JS extension: `limits` (DeckLimits) is the builder's and replaces the 20〜40 of the clamp, so a
   * survivor's heir can draw up to 45. Left out, the C#'s.
   */
  static Random(seed, size = DeckBuilder.RandomDefaultSize, limits = DeckLimits.Default) {
    const L = DeckLimits.From(limits);
    size = Math.max(L.DeckMin, Math.min(L.DeckMax, size));
    const rng = new SeededRng(seed);
    const builder = new DeckBuilder(null, L);
    const closers = DeckBuilder.Closers;
    const forward = DeckBuilder.Forward;
    while (builder.Total < size) {
      const pool =
        builder.CountIn(closers) < 1 ? closers
          : builder.CountIn(forward) < DeckBuilder.RandomForwardMin ? forward
            : CardCatalog.All;
      let index = Math.floor(rng.NextDouble() * pool.length);
      if (index >= pool.length) index = pool.length - 1;
      builder.Add(pool[index].Id);
    }
    return builder;
  }

  /** The cards that step forward (前へ n). */
  static get Forward() {
    const kept = [];
    for (const def of CardCatalog.All) {
      if (def.Face.Move > 0) kept.push(def);
    }
    return kept;
  }

  /** The cards that close in from gap 3 even under 鈍足: two cells forward, playable at gap 3. */
  static get Closers() {
    const kept = [];
    for (const def of CardCatalog.All) {
      if (def.Face.Move >= 2 && (def.Targets === TargetKind.Self || def.Face.ReachOrDefault.Contains(3))) kept.push(def);
    }
    return kept;
  }

  // ---- the saved string ----

  /** "thrust:2,kesa_cut:3" in canon order. The screen stores it; this class reads it back. */
  Save() {
    let text = '';
    for (const def of CardCatalog.All) {
      const n = this.CountOf(def.Id);
      if (n === 0) continue;
      if (text.length > 0) text += ',';
      text += def.Id + ':' + n;
    }
    return text;
  }

  /**
   * The deck a saved string describes. A string that does not read cleanly — an unknown id, a
   * count outside 1〜3, the same id twice, a deck past 40, stray text — gives an empty deck
   * rather than a guess. Any number of stance cards reads (§4 / §8, v4.4).
   * (JS extension: `limits` is the deck's — a 45-card string reads under the survivor's 45.)
   */
  static Load(saved, limits = DeckLimits.Default) {
    return DeckBuilder.Read(saved, limits).ok;
  }

  /**
   * The deck the screen opens with: the prototype deck the first time (nothing saved yet), and
   * the saved deck after that — empty when the player emptied it or the string is broken.
   *
   * With a third argument (C#'s `out string notice` overload, #211) it returns { ok, value }: ok the
   * deck, value the one line the deck screen prints about it — why a saved string left the deck
   * empty. "" when there is nothing to say: nothing saved, a string that reads, or a deck the
   * player emptied.
   *
   * JS extension: a DeckLimits as the last argument is the deck's limits, not the out argument —
   * LoadOrPrototype(saved, text, limits) returns the DeckBuilder, LoadOrPrototype(saved, text, true,
   * limits) the { ok, value } pair.
   */
  static LoadOrPrototype(saved, text, ...out) {
    let limits = DeckLimits.Default;
    if (out.length > 0 && out[out.length - 1] instanceof DeckLimits) limits = out.pop();
    let notice = '';
    let deck;
    if (saved) {
      const read = DeckBuilder.Read(text, limits);
      deck = read.ok;
      notice = read.value;
    } else {
      deck = DeckBuilder.Prototype(limits);
    }
    return out.length > 0 ? { ok: deck, value: notice } : deck;
  }

  /**
   * private. Reads a saved string (Load) and says why one gave an empty deck (#211). An entry that
   * is not "id:count" — an id in the catalog's shape, a count of 1 or more — or an id given twice,
   * does not read. An entry that reads but whose id the catalog does not know (most likely a card
   * renamed since) is named; stray text is never named as a card. A string that reads but that Add
   * refuses on the way breaks the deck's rules, which are not named, so the line stays true as they
   * grow. The notice is "" when it reads, an empty string too.
   * Returns { ok: DeckBuilder, value: notice } (C#'s return value and out notice).
   * JS extension: `limits` is the deck's (every builder it returns, empty ones too, carries it).
   */
  static Read(saved, limits = DeckLimits.Default) {
    let notice = '';
    const L = DeckLimits.From(limits);
    const builder = new DeckBuilder(null, L);
    if (saved == null || saved === '') return { ok: builder, value: notice };
    for (const entry of saved.split(',')) {
      const pair = entry.split(':');
      const n = pair.length === 2 ? tryParseInt(pair[1]) : null;
      if (pair.length !== 2 || !DeckBuilder.IdShaped(pair[0]) || n === null
        || n < 1 || builder.CountOf(pair[0]) > 0) {
        notice = '保存したデッキを読めなかったため、空のデッキから始めます';
        return { ok: new DeckBuilder(null, L), value: notice };
      }
      if (!DeckBuilder.Known(pair[0])) {
        notice = '保存したデッキに知らないカード「' + pair[0] + '」があったため、空のデッキから始めます';
        return { ok: new DeckBuilder(null, L), value: notice };
      }
      for (let i = 0; i < n; i++) {
        // A fourth copy, a 41st card: Add holds the rules, the count above is only read.
        if (builder.Add(pair[0])) continue;
        notice = '保存したデッキがデッキの決まりに合わないため、空のデッキから始めます';
        return { ok: new DeckBuilder(null, L), value: notice };
      }
    }
    return { ok: builder, value: notice };
  }

  /**
   * JS extension (docs/EXTENSIONS.md 「出立の枠」 試作で決めたこと 7): Read, except for a string whose one
   * fault is holding more cards than `limits.DeckMax` — a deck saved in a survivor's bonus life (up to 45)
   * and opened in the next life, whose cap is back to 40. That deck keeps every card: the builder carries
   * the life's limits with the deck over them, so ＋ stays greyed out, Validate fails, StatusText asks for
   * the extra cards to come out (「45 枚です。あと 5 枚外すと戦えます」) and the notice says why
   * (OverCapNotice). Every other string reads exactly as Read reads it (the C#'s empty deck and notice).
   * Returns { ok, value } like Read.
   */
  static ReadOverCap(saved, limits = DeckLimits.Default) {
    const read = DeckBuilder.Read(saved, limits);
    if (read.value === '') return read;
    const L = read.ok.Limits;
    const wide = DeckBuilder.Read(saved, L.With({ DeckMax: Math.max(L.DeckMax, Constants.OwnedKindsMax * Constants.CopiesMax) }));
    if (wide.value !== '' || wide.ok.Total <= L.DeckMax) return read;
    const over = new DeckBuilder(null, L);
    for (const [id, n] of wide.ok._counts) over._counts.set(id, n);
    return { ok: over, value: DeckBuilder.OverCapNotice(over.Total, L) };
  }

  /**
   * JS extension: why a saved deck opens over the cap. The cap can only have been raised by the survivor's
   * bonus (concept-v3.md §14 「デッキ上限 +5 を次の 1 生だけ」), so a cap back at the C#'s 40 says the bonus
   * ended: 「生存者ボーナスが終わり、デッキ上限が 40 枚に戻りました。保存したデッキ 45 枚から、外す 5 枚を選んでください」.
   */
  static OverCapNotice(total, limits = DeckLimits.Default) {
    const L = DeckLimits.From(limits);
    const extra = total - L.DeckMax;
    const pick = '保存したデッキ ' + total + ' 枚から、外す ' + extra + ' 枚を選んでください';
    return L.DeckMax <= DeckLimits.Default.DeckMax
      ? '生存者ボーナスが終わり、デッキ上限が ' + L.DeckMax + ' 枚に戻りました。' + pick
      : 'この生のデッキ上限は ' + L.DeckMax + ' 枚です。' + pick;
  }

  /**
   * JS extension: this deck on a builder of `owned` kinds (null: the catalog) with the same limits. Each
   * owned kind keeps its copies — a deck over the cap (ReadOverCap) stays over it — and a kind not owned is
   * left out, as Add would leave it.
   */
  WithOwned(owned) {
    const b = new DeckBuilder(owned, this.Limits);
    for (const def of CardCatalog.All) {
      const n = this.CountOf(def.Id);
      if (n > 0 && b.Owned.includes(def.Id)) b._counts.set(def.Id, n);
    }
    return b;
  }

  /** private. The shape every catalog id has ("kesa_cut"): lowercase letters, digits and '_'. */
  static IdShaped(id) {
    if (id == null || id === '') return false;
    for (let i = 0; i < id.length; i++) {
      const c = id.charCodeAt(i);
      if ((c < 0x61 || c > 0x7a) && (c < 0x30 || c > 0x39) && c !== 0x5f) return false;
    }
    return true;
  }

  /** private */
  static Known(id) {
    if (id == null || id === '') return false;
    for (const def of CardCatalog.All) {
      if (def.Id === id) return true;
    }
    return false;
  }
}

/** The size the 「ランダム」 button builds (between §8's 20 and 40). */
DeckBuilder.RandomDefaultSize = 30;

/** Random decks hold this many cards that step forward (前へ n), so the fight can close in. */
DeckBuilder.RandomForwardMin = 3;

/**
 * int.TryParse(s, out n) with NumberStyles.Integer and the invariant signs: optional leading and
 * trailing white space (U+0009〜U+000D, U+0020), an optional '+' / '-', ASCII digits, trailing NULs
 * allowed after the white space (as .NET does), and the value inside int. Null when it does not read.
 */
function tryParseInt(s) {
  if (s == null) return null;
  const m = /^[\t\n\v\f\r ]*([+-]?)([0-9]+)[\t\n\v\f\r ]*\0*$/.exec(s);
  if (m === null) return null;
  const digits = m[2].replace(/^0+(?=[0-9])/, '');
  if (digits.length > 10) return null;
  const value = (m[1] === '-' ? -1 : 1) * Number(digits);
  if (value > 2147483647 || value < -2147483648) return null;
  return value;
}
