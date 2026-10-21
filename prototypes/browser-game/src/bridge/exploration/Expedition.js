// Port of unity-project-kit/Assets/View/Exploration/Bridge/Expedition.cs
// One life in the dungeon as both cores see it (#99): the exploration session, and what the battles of
// the life share — the deck, the cards owned, the mastery book, the in-battle tools and what the
// journal has learned of each enemy. It hands a combat node's battle in (EncounterBattle) and takes
// its result back: HP and stamina into the exploration core, the ticks into the mastery book, the
// 開示度 into the journal, the unique cards into the cards owned and the deck, and the rest of the
// reward (#625: the pile's cards to pick one from, consumables, pages). It also spends the marks a
// 習熟訓練 node earns on the card the player names. Pure; the View that loads the battle scene only
// forwards the source and the end of each fight.
import { sum, ArgumentException, InvalidOperationException } from '../../shared/cs.js';
import { CardCatalog, PrototypeDeck } from '../../core/battle/CardCatalog.js';
import { Cards } from '../../core/battle/Cards.js';
import { DeckLimits, DeckRules, Departure } from '../../core/battle/DeckRules.js';
import { Enemies } from '../../core/battle/Enemies.js';
import { MasteryBook, MasteryTally } from '../../core/battle/Mastery.js';
import { CardInstance } from '../../core/battle/Types.js';
import { ItemCatalogue } from '../../core/dungeonContent/ItemCatalogue.js';
import { ItemEffect } from '../../core/dungeonContent/ItemEffect.js';
import { BattleField } from '../../core/dungeon/BattleField.js';
import { SeededRng } from '../../core/dungeon/IRng.js';
import { ExplorationSession, RunStart } from '../explorationScript/ExplorationSession.js';
import { ExplorationText } from '../explorationScript/ExplorationText.js';
import { BattleReward, BattleRewards, CardPile } from './BattleRewards.js';
import { BattleHandIn, BattleReturn, EncounterBattle } from './EncounterBattle.js';
import { UniqueCardReward, UniqueCards } from './UniqueCards.js';

let _initialKinds = null;

export class Expedition {
  /**
   * concept-v3 §14 / swordsman_cards_v4 §2: the kinds every character owns from the start (#1〜#40).
   * (C# static readonly; computed on first read so the card catalogue has loaded.)
   */
  static get InitialKinds() {
    return _initialKinds ??= CardCatalog.All.slice(0, 40).map((d) => d.Id);
  }

  /**
   * @param start How the exploration starts (seed, HP, stamina, gauge, exploration tools): a RunStart.
   * @param deck The deck the life sets out with; null for the slice's prototype deck.
   * @param battleToolIds The tools in the slots that act inside a battle (間合いの履, 息継ぎの笛,
   *   竜鱗の欠片, 竜語の符; tools_and_consumables_v4.md §2), as catalogue ids. The tools that act on
   *   the exploration go in RunStart.Loadout; turning one chosen loadout into both is 出立's (#104).
   * @param talent 才能: the card whose battle ticks count twice (§17.8), or null.
   * @param intel What the exploration screen says about the enemy behind a node.
   * @param journal The journal's 開示度 by enemy id as the life starts (a Map; a plain object is read
   *   as one). An enemy not listed reads 0.
   * @param options JS extension (not in the C#; docs/EXTENSIONS.md): `{ limits, trial, carvingKnown }`. `limits` (a
   *   DeckLimits) goes to Cards.Validate and DeckRules.CanAdd in place of the C# constants; `trial: true`
   *   turns the world extensions on (the same as RunStart.Trial): every carried item acts, found
   *   consumables enter the slots, events resolve (「探索の持ち物とイベント」). Null plays the C#.
   *
   * C# takes these as optional (named) parameters; JS takes them positionally.
   */
  constructor(start = null, deck = null, battleToolIds = null, talent = null, intel = null, journal = null, options = null) {
    /** JS extension: the deck limits the deck rules read, or null for the C# constants. */
    this._limits = options?.limits ?? null;
    /** JS extension: the trial rules are on (options.trial or RunStart.Trial). */
    this.Trial = options?.trial === true || start?.Trial === true;
    if (this.Trial && start?.Trial !== true) start = new RunStart({ ...(start ?? {}), Trial: true });
    // JS extension: `options.carvingKnown` (the lineage read the carving in an earlier life) → RunStart.CarvingKnown.
    if (this.Trial && typeof options?.carvingKnown === 'boolean' && start.CarvingKnown !== options.carvingKnown)
      start = new RunStart({ ...start, CarvingKnown: options.carvingKnown });
    this._startDeck = [...(deck ?? PrototypeDeck.Build())];
    const validation = Cards.Validate(this._startDeck, this._limits ?? undefined);
    if (!validation.Ok) throw new ArgumentException('Expedition: the deck breaks §8 — ' + validation.Errors.join(' / ') + " (Parameter 'deck')");
    this._startJournal = journal == null ? new Map() : journal instanceof Map ? journal : new Map(Object.entries(journal));
    this._talent = talent;
    /** Dictionary<string, int>, ordinal keys. */
    this._journal = new Map();
    /** HashSet<string>, ordinal. */
    this._owned = new Set();
    this._deck = null;
    this._consumables = [];
    this._offer = [];
    this._life = 0;
    this._learnedCopies = 0;
    this._pages = 0;
    // JS extension (trial mode): the result card waiting on the player (PendingResult / CloseResult).
    this._result = null;
    this._eventsClosed = 0;
    // JS extension (trial mode): the 受け取った遺産 card waiting on the player (ShowTaken / PendingTaken / CloseTaken).
    this._taken = null;
    /**
     * JS extension: the 死亡地点の痕跡 this life collected and the layer it lay on, or null. The game's
     * Expedition.prototype.Inherit (src/app/life.js) sets them; the trace node's lines read them.
     */
    this.InheritedTraceId = null;
    this.InheritedLayer = null;
    /**
     * JS extension: how many of the life's rare pages (Pages) were inherited — a survivor's whole journal at
     * the start (src/app/life.js lifeSetup) and the pages a trace gave (Inherit) — rather than found in this
     * life. The map and the journal list them apart (INHERITANCE.md R-27). 0 when nothing sets it.
     */
    this.InheritedPages = 0;
    /** This life's mastery (§3.2 / §17.8). */
    this.Mastery = null;
    /** The encounter being fought, or null. */
    this.Battle = null;
    /** The in-battle tools (§9.1, #452) as amounts, with the deck and the talent the battles read. */
    this.Tools = Expedition.BattleTools(this._startDeck, battleToolIds ?? [], talent);
    this.Session = new ExplorationSession(start, intel);
    // JS extension (trial mode): the session hands what it finds outside its core to the expedition
    // (a chest's cards and pages, the 写しの帳 pick, the journal's 開示度) — see TakeChest / TakeSurvey.
    if (this.Trial) this.Session.Keeper = this;
    this.BeginLife();
  }

  /** The deck each battle is shuffled from, as set out with plus the cards learned on the way. */
  get Deck() {
    this.SyncLife();
    return this._deck;
  }

  /** The card kinds this character owns (a Set): the initial forty, the deck's, and every card learned this life. */
  get Owned() {
    this.SyncLife();
    return this._owned;
  }

  /**
   * Cards waiting for the player to pick one (Learn): a real elite's two, or the card pile's three
   * (encounters_and_rewards_v4.md §4.2 / §4.3). Empty when none waits.
   */
  get Offer() {
    this.SyncLife();
    return this._offer;
  }

  /**
   * The consumables the battles of this life dropped, as catalogue ids in the order found. They do
   * not reach the exploration's slots yet: RunLoadout holds only what relieves 瘴気.
   * (JS extension, trial mode: they do — Return hands them to Session.Receive; this list still records them.)
   */
  get Consumables() {
    this.SyncLife();
    return this._consumables;
  }

  /** The rare journal pages the battles of this life dropped. What a page holds is #107's. */
  get Pages() {
    this.SyncLife();
    return this._pages;
  }

  /** JS extension: the rare pages found in this life (Pages less the inherited ones: InheritedPages). */
  get FoundPages() {
    return Math.max(0, this.Pages - (this.InheritedPages || 0));
  }

  /** The journal's 開示度 for an enemy: 0 until it is met, 1 after a battle with it, 2 after it is observed. */
  DisclosureOf(enemyId) {
    this.SyncLife();
    return enemyId != null && this._journal.has(enemyId) ? this._journal.get(enemyId) : 0;
  }

  /**
   * Everything the combat node waiting on a battle hands it, or null when no battle waits. The
   * battle's seed comes from the life's seed and the node, so a life replays its fights.
   */
  HandIn() {
    this.SyncLife();
    const session = this.Session;
    if (!session.HasPendingBattle || session.PendingEncounter == null) return null;

    const state = session.State;
    const layer = state.Profile.Layer;
    const kind = state.Map.Node(session.PendingBattleNode).Kind;
    const encounter = session.PendingEncounter;
    const cells = encounter.Fights
      .map((fight) => BattleField.CellsFor(layer, kind, sum(fight.EnemyIds, (id) => Enemies.ById(id).Size)));
    const disclosure = new Map();
    for (const id of encounter.EnemyIds) {
      if (!disclosure.has(id)) disclosure.set(id, this.DisclosureOf(id));
    }
    const seed = Number(BigInt.asIntN(32, ExplorationSession.EncounterSeed(session.RunSeed, layer, session.PendingBattleNode)));

    return new BattleHandIn(
      layer, kind, encounter, cells,
      state.Hp, state.MaxHp, state.Stamina, state.MaxStamina, state.MiasmaPercent,
      disclosure, this.Mastery.MasteredDeck(this._deck), this.Tools, seed, this._limits);
  }

  /** Starts the battle of the node that waits on one. Null when none waits. */
  StartEncounter() {
    if (this.Battle != null) throw new InvalidOperationException('Expedition: a battle is already being fought.');
    const handIn = this.HandIn();
    if (handIn == null) return null;
    this.Battle = new EncounterBattle(handIn);
    return this.Battle;
  }

  /**
   * The encounter is over: bring its result back. HP and stamina go to the exploration core
   * (HP 0 is a fall), every fight's ticks to the mastery book (steps go up only now, §17.8), the
   * 開示度 to the journal (every enemy fought reads at least 1, and 2 once a 観察 was played
   * against it), and a won encounter's unique cards to the cards owned. A won encounter also
   * draws the rest of its reward (§4.2) from the exploration's seed: the cards to pick one from
   * wait in Offer, the consumables and pages are kept.
   */
  Return() {
    const battle = this.Battle;
    if (battle == null) throw new InvalidOperationException('Expedition: no battle is being fought.');
    if (!battle.Over) throw new InvalidOperationException('Expedition: the encounter has fights left.');
    this.Battle = null;
    // JS extension (trial mode): the 開示度 before the encounter, so the result card names only what rose.
    const was = new Map();
    if (this.Trial) for (const f of battle.Fought) for (const id of f.Fight.EnemyIds) if (!was.has(id)) was.set(id, this.DisclosureOf(id));

    const ranks = [];
    const plays = [];
    const disclosure = new Map();
    const observed = [];
    for (const fought of battle.Fought) {
      const result = this.Mastery.AfterBattle(fought.State);
      this.Mastery = result.Book;
      ranks.push(...result.RankUps);
      plays.push(...fought.State.History.MasteryPlays);

      for (const id of fought.Fight.EnemyIds) {
        const before = this.DisclosureOf(id);
        const after = Math.max(before, fought.Observed ? 2 : 1);
        this._journal.set(id, after);
        disclosure.set(id, after);
        if (fought.Observed && before < 2 && !observed.includes(id)) observed.push(id);
      }
    }

    const handIn = battle.HandIn;
    const reward = battle.Won
      ? UniqueCards.For(handIn.Kind, handIn.Encounter, this._owned)
      : UniqueCardReward.None;
    const benched = reward.Learned.filter((id) => !this.Take(id)); // JS: Take tells whether a copy joined the deck
    const rest = battle.Won
      ? BattleRewards.Roll(handIn.Kind, handIn.Layer, handIn.Encounter, reward, this._owned,
        new SeededRng(Expedition.RewardSeed(this.Session.RunSeed, handIn.Layer, this.Session.PendingBattleNode)))
      : BattleReward.None;
    // JS extension (trial mode): the pile was spent when the roll wanted a card and none was dealt — the same
    // roll with nothing owned deals one exactly when the real roll asked the pile (§4.3 「札の代わりに消耗品」).
    const pileSpent = this.Trial && battle.Won && rest.CardChoices.length === 0 &&
      BattleRewards.Roll(handIn.Kind, handIn.Layer, handIn.Encounter, reward, new Set(),
        new SeededRng(Expedition.RewardSeed(this.Session.RunSeed, handIn.Layer, this.Session.PendingBattleNode))).CardChoices.length > 0;
    this._offer = [...(reward.Offered.length > 0 ? reward.Offered : rest.CardChoices)];
    this._consumables.push(...rest.Consumables);
    this._pages += rest.Pages;

    this.Session.ReportBattle(battle.HpAfter, battle.StaminaAfter);
    // JS extension (trial mode): the battle's consumables go into the slots, or wait on a swap
    // (encounters_and_rewards_v4.md §4.3, tools_and_consumables_v4.md §3).
    if (this.Trial) this.Session.Receive(rest.Consumables);

    const back = new BattleReturn(
      battle.Won, battle.HpAfter, battle.StaminaAfter, MasteryTally.Of(plays), ranks,
      disclosure, observed, reward.Learned, reward.Offered, rest);
    // JS extension (trial mode): the result card waits on the player until CloseResult, so a reload shows it
    // again (tier1 R1-16) — with what only the expedition knows: which enemies' 開示度 rose, whether the card
    // pile was spent (a card wanted, none dealt: §4.3), and which learned cards stayed out of a full deck (§4.3).
    if (this.Trial) {
      this._eventsClosed = this.Session.Events.length;
      this._result = back.Died ? null : Object.freeze({
        Kind: 'battle',
        Back: back,
        Raised: [...disclosure].filter(([id, level]) => level > (was.get(id) ?? 0)),
        PileSpent: pileSpent,
        Benched: benched,
        DeckMax: this.DeckMax,
        State: this.Session.State,
      });
    }
    return back;
  }

  /**
   * The seed a won node's reward is drawn from: the node's encounter seed, salted so the reward
   * does not replay the draw that picked the encounter. (ulong → BigInt)
   */
  static RewardSeed(runSeed, layer, nodeId) {
    return ExplorationSession.EncounterSeed(runSeed, layer, nodeId) ^ 0xD1B54A32D192ED03n;
  }

  /**
   * The battle could not be shown (the View found no battle scene to play it in): it is dropped
   * unfought, and the node still waits on a result.
   */
  Abandon() {
    this.Battle = null;
  }

  /**
   * Picks one card of the offer — a real elite's or the card pile's (encounters_and_rewards_v4.md
   * §4.2 / §4.3). False when it is not on offer.
   */
  Learn(cardId) {
    this.SyncLife();
    if (cardId == null || !this._offer.includes(cardId)) return false;
    this.Take(cardId);
    this._offer.length = 0; // List.Clear(): the list Offer handed out empties too
    // JS extension (trial mode): the pick is made on the result card, so the card is done with (CloseResult follows
    // it; a save from before CloseResult existed has only this call).
    if (this.Trial) { this._result = null; this._eventsClosed = this.Session.Events.length; }
    return true;
  }

  /**
   * 習熟訓練 (battle_core_v4.md §17.8): spends one mark a 習熟訓練 node earned
   * (ExplorationState.TrainingMarks) on a card this character owns, which takes the training's
   * ticks in the mastery book. False when no mark is left, the card is not owned or the
   * exploration is not waiting on the map.
   */
  Train(cardId) {
    this.SyncLife();
    if (cardId == null || !this._owned.has(cardId)) return false;
    if (!this.Session.SpendTrainingMark()) return false;
    this.Mastery = this.Mastery.Train(cardId).Book;
    return true;
  }

  /**
   * A learned card is owned at once, and one copy goes into the deck when the deck has room
   * (encounters_and_rewards_v4.md §4.3: 「デッキの上限の中ですぐ戦闘デッキに入ります」). (C# private)
   */
  Take(cardId) {
    this._owned.add(cardId);
    if (!DeckRules.CanAdd(this._deck, cardId, this._owned, this._limits ?? undefined)) return false;
    this._learnedCopies++;
    this._deck.push(new CardInstance(`${cardId}-learned-${this._learnedCopies}`, CardCatalog.ById(cardId)));
    return true; // JS: whether a copy joined the deck (the C# method is void)
  }

  // ---- JS extension (trial mode; docs/EXTENSIONS.md 「探索の持ち物とイベント」): the Keeper the session
  // hands its finds to. Called by the session on the raw expedition, so a save's replay of the session's
  // call repeats them.

  /** Every event of this life (EventOutcome), oldest first. Empty outside trial. */
  get Events() { return this.Session.Events; }

  /**
   * A 重い宝箱 was taken (encounters_and_rewards_v4.md §5): its card is the card pile's (3 laid out, pick
   * one with Learn — §4.3: 「札の山から引く回数の見込み…重い宝箱のカードを含めました」), a card the piles can
   * no longer deal becomes a consumable (§4.3), its consumables are drawn by the layer's weights (§4.3) and
   * its pages counted. Seeded from the node (RewardSeed), so a life replays it. Returns
   * { Cards, Consumables, Pages, PileSpent } — the session puts the consumables into the slots.
   */
  TakeChest(layer, nodeId, contents) {
    this.SyncLife();
    const rng = new SeededRng(Expedition.RewardSeed(this.Session.RunSeed, layer, nodeId));
    let cards = [];
    const consumables = [];
    let spent = false;
    if (contents.Cards > 0) {
      cards = CardPile.Deal(layer, this._owned, rng);
      if (cards.length === 0) {
        spent = true;
        consumables.push(BattleRewards.DrawConsumable(layer, rng));
      }
    }
    for (let i = 0; i < contents.Consumables; i++) consumables.push(BattleRewards.DrawConsumable(layer, rng));
    if (cards.length > 0) this._offer = [...cards];
    this._pages += contents.Pages;
    return { Cards: cards, Consumables: consumables, Pages: contents.Pages, PileSpent: spent };
  }

  /**
   * The result card waiting on the player, or null: a battle's after Return ({ Kind: 'battle', Back, Raised,
   * PileSpent, Benched, DeckMax } — Raised the [enemyId, 開示度] that rose, Benched the learned cards a full
   * deck kept out), or the last event's once it happened ({ Kind: 'event', Outcome }; a declined event shows
   * none). It stays until CloseResult, so a reload shows the same card — and the same pick (Offer) — again
   * (tier1 R1-16). It is dropped as soon as the exploration moved on (any change of the session's state), so a
   * save written before CloseResult existed never brings back a card the player already closed. Null outside
   * trial.
   */
  get PendingResult() {
    this.SyncLife();
    if (!this.Trial) return null;
    const session = this.Session;
    if (this._result !== null) return this._result.State === session.State ? this._result : null;
    if (session.HasPendingEvent || session.Events.length <= this._eventsClosed) return null;
    const last = session.Events[session.Events.length - 1];
    return last.Accepted && session.EventState === session.State ? { Kind: 'event', Outcome: last } : null;
  }

  /**
   * The result card was closed. A pick not taken is given up (Offer empties: 「受け取らない」) and nothing is
   * pending any more. Called through the recorder like Learn, so a replay closes it at the same moment.
   * False outside trial.
   */
  CloseResult() {
    this.SyncLife();
    if (!this.Trial) return false;
    // A chest's card pick closed without a card (「受け取らない」): the event's record drops 「カード 1 枚」, so the walked
    // node and the journal say what the heir kept (docs/EXTENSIONS.md X-9). Learn empties the offer, so a pick taken
    // never gets here with cards still on offer.
    const events = this.Session.Events;
    const last = events.length > this._eventsClosed ? events[events.length - 1] : null;
    if (this._result === null && last !== null && last.Cards.length > 0 && this._offer.length > 0 &&
      last.Cards.every((id) => this._offer.includes(id))) {
      this.Session.TurnDownFind(events.length - 1, ExplorationText.RecordCards(1));
    }
    this._result = null;
    this._eventsClosed = this.Session.Events.length;
    this._offer.length = 0;
    return true;
  }

  /**
   * For each card of Offer, whether learning it puts a copy into the deck (encounters_and_rewards_v4.md §4.3
   * 「デッキの上限の中ですぐ戦闘デッキに入ります」; DeckRules.CanAdd with the life's limits). A card that does not
   * fit is still owned.
   */
  get OfferFits() {
    this.SyncLife();
    return this._offer.map((id) => DeckRules.CanAdd(this._deck, id, new Set([...this._owned, id]), this._limits ?? undefined));
  }

  /** The deck's cap this life (the survivor's bonus raises it: docs/EXTENSIONS.md L-2). */
  get DeckMax() {
    return (this._limits ?? DeckLimits.Default).DeckMax;
  }

  /**
   * JS extension (trial mode; docs/EXTENSIONS.md X-3): the 受け取った遺産 card is up. `card` is plain data the scene
   * wrote from the trace card it just took from ({ PlaceLine, HeirLine, Rows: [{ Kind, KindLabel, Name, Detail,
   * Notes }], Lost }). It stays pending (PendingTaken) until CloseTaken, so a reload shows the same card again
   * (tier1 R1-16) — a logged call with plain data, replayed like the take itself. False outside trial or before
   * this life took a trace.
   */
  ShowTaken(card) {
    this.SyncLife();
    if (!this.Trial || this.InheritedTraceId == null || card == null) return false;
    this._taken = Object.freeze({ Card: JSON.parse(JSON.stringify(card)), State: this.Session.State });
    return true;
  }

  /** The 受け取った遺産 card waiting on the player, or null; dropped once the exploration moved on (like PendingResult). */
  get PendingTaken() {
    this.SyncLife();
    if (!this.Trial || this._taken == null) return null;
    return this._taken.State === this.Session.State ? this._taken.Card : null;
  }

  /** The 受け取った遺産 card was closed (a logged call). */
  CloseTaken() {
    this.SyncLife();
    this._taken = null;
    return true;
  }

  /** 写しの帳: the enemy the player picked is raised to 開示度 2 in the journal (§2.2). */
  TakeSurvey(enemyId) {
    this.SyncLife();
    if (enemyId == null) return;
    this._journal.set(enemyId, Math.max(this.DisclosureOf(enemyId), 2));
  }

  /** An enemy's name for the exploration's cards (the battle core's catalogue), or its id. */
  EnemyName(enemyId) {
    try { return Enemies.ById(enemyId).Name; } catch (e) { return enemyId; }
  }

  /** A new life (ExplorationSession's 「新しい生」) starts from what the expedition set out with. (C# private) */
  SyncLife() {
    if (this._life !== this.Session.Life) this.BeginLife();
  }

  /** (C# private) */
  BeginLife() {
    this._life = this.Session.Life;
    this._deck = [...this._startDeck];
    this._owned.clear();
    for (const id of Expedition.InitialKinds) this._owned.add(id);
    for (const c of this._startDeck) this._owned.add(c.Def.Id);
    this._journal.clear();
    for (const [key, value] of this._startJournal) this._journal.set(key, value);
    this._offer = [];
    this._consumables.length = 0;
    this._pages = 0;
    this._learnedCopies = 0;
    this._result = null; // JS extension (trial mode)
    this._eventsClosed = 0;
    this._taken = null;
    this.Battle = null;
    this.Mastery = MasteryBook.Begin(this._talent);
  }

  /**
   * The four in-battle tools, summed over the slots (the same tool may fill two), as a Departure the
   * core reads. (C# private static)
   */
  static BattleTools(deck, toolIds, talent) {
    let cellsBack = 0, stamina = 0, guard = 0, ward = 0;
    for (const id of toolIds) {
      const tool = ItemCatalogue.Tool(id);
      switch (tool.Effect) {
        case ItemEffect.WiderStart: cellsBack += tool.Amount; break;
        case ItemEffect.OpeningStamina: stamina += tool.Amount; break;
        case ItemEffect.OpeningGuard: guard += tool.Amount; break;
        case ItemEffect.BluntBossState: ward += tool.Amount; break;
        default:
          throw new ArgumentException(
            `Expedition: ${tool.Name} acts on the exploration; it goes in RunStart.Loadout (Parameter 'toolIds')`);
      }
    }
    return new Departure(deck, [...toolIds], [], cellsBack, talent, stamina, guard, ward);
  }
}
