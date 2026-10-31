// Port of unity-project-kit/Assets/View/Exploration/Bridge/BattleRewards.cs
// The rest of a won encounter's reward (encounters_and_rewards_v4.md §4.2 / §4.3, #625): cards from the
// layer's card pile to pick one from, consumables and rare pages. The unique cards of a real boss or
// elite are UniqueCards'; this file draws what the table adds around them. The pile's cards are the card
// table's (swordsman_cards_v4.md §3, cards lane) and the chances and weights are §4.2 / §4.3's; all are
// copies, changed when the canon moves. The draws come from the exploration's seed, so a life replays them.
import { sum, ArgumentNullException, ArgumentOutOfRangeException, InvalidOperationException } from '../../shared/cs.js';
import { CardCatalog } from '../../core/battle/CardCatalog.js';
import { RngExtensions } from '../../core/dungeon/IRng.js';
import { NodeKind } from '../../core/dungeon/NodeKind.js';
import { SevenLayers } from '../../core/dungeon/SevenLayers.js';
import { UniqueCards } from './UniqueCards.js';

/** IReadOnlyCollection<string>.Contains for a Set (HashSet) or an array (List). */
function contains(collection, id) {
  return collection instanceof Set ? collection.has(id) : collection.includes(id);
}

function nullArg(param) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${param}')`);
}

/** What a won encounter adds besides its unique cards. */
export class BattleReward {
  static None = new BattleReward([], [], 0);

  constructor(cardChoices, consumables, pages) {
    if (pages < 0) throw new ArgumentOutOfRangeException("Specified argument was out of the range of valid values. (Parameter 'pages')");
    /** Card ids from the card pile, one to pick (§4.3: up to three). Empty when no card came. */
    this.CardChoices = cardChoices ?? [];
    /** Consumable ids found (ItemCatalogue.Consumables), in the order they were drawn. */
    this.Consumables = consumables ?? [];
    /** Rare journal pages found. What a page holds is #107's. */
    this.Pages = pages;
  }

  get IsEmpty() { return this.CardChoices.length === 0 && this.Consumables.length === 0 && this.Pages === 0; }

  toJSON() {
    return { $type: 'BattleReward', CardChoices: this.CardChoices, Consumables: this.Consumables, Pages: this.Pages, IsEmpty: this.IsEmpty };
  }
}

// §4.3's table as canon numbers (swordsman_cards_v4.md §3), layer 1 first.
const Numbers = [
  [58, 59, 60],
  [61, 62, 63],
  [64, 65, 66],
  [67, 68, 69],
  [70, 71, 72],
  [73, 74, 75, 76],
  [77, 78, 79, 80],
];

/** §4.3 札の山: the learned cards each layer's fights hand out. */
export const CardPile = {
  /** §4.3: how many cards are laid out to pick one from. */
  Choices: 3,

  /** The card ids of a layer's pile, in canon order. */
  Of(layer) {
    BattleRewards.CheckLayer(layer);
    return Numbers[layer - 1].map((n) => CardCatalog.All[n - 1].Id);
  },

  /**
   * §4.3: up to three cards the player does not own. The layer's own pile comes first, and what it
   * lacks is filled from the shallower layers' piles, the nearest first. Each pile is shuffled, so
   * which cards of a four-card pile show is drawn. Empty when every pile up to the layer is spent.
   */
  Deal(layer, owned, rng) {
    BattleRewards.CheckLayer(layer);
    if (owned == null) throw nullArg('owned');
    if (rng == null) throw nullArg('rng');

    const dealt = [];
    for (let l = layer; l >= 1 && dealt.length < CardPile.Choices; l--) {
      const left = CardPile.Of(l).filter((id) => !contains(owned, id));
      RngExtensions.Shuffle(rng, left);
      dealt.push(...left.slice(0, CardPile.Choices - dealt.length));
    }
    return dealt;
  },
};

// §4.2 通常戦闘, layers 1〜6: the chance in percent of a card, a consumable and a page. Each row adds to 100.
const NormalPercent = [
  [30, 70, 0],
  [35, 60, 5],
  [40, 50, 10],
  [45, 40, 15],
  [50, 30, 20],
  [50, 25, 25],
];

// §4.2 精鋭: the chance in percent of a page on top of the card and the consumable, layer 1 first.
const ElitePagePercent = [0, 10, 20, 30, 40, 50, 100];

// §4.3 消耗品の重み, in ConsumableIds' order, layer 1 first. Each row adds to 100.
const ConsumableWeights = [
  [10, 35, 20, 30, 5],
  [15, 30, 20, 30, 5],
  [20, 30, 20, 25, 5],
  [30, 25, 20, 20, 5],
  [35, 25, 20, 15, 5],
  [40, 25, 20, 10, 5],
  [50, 30, 20, 0, 0],
];

/** A stand-in boss borrowed from a §9 elite (layers 4〜5), whose cards it teaches. */
function IsEliteStandIn(encounter) {
  const enemies = [...encounter.EnemyIds];
  return enemies.length === 1 && UniqueCards.OfElite(enemies[0]).length > 0;
}

function IndexOf(consumableId) {
  for (let i = 0; i < BattleRewards.ConsumableIds.length; i++) {
    if (BattleRewards.ConsumableIds[i] === consumableId) return i;
  }
  return -1;
}

/** The reward as it is drawn, one part at a time in the rule's order. (C# private nested class.) */
class Draw {
  constructor(layer, owned, rng) {
    this._layer = layer;
    this._owned = owned;
    this._rng = rng;
    this._consumables = [];
    this._cards = [];
    this._pages = 0;
  }

  Card() {
    const cards = CardPile.Deal(this._layer, this._owned, this._rng);
    if (cards.length === 0) this.Consumable();
    else this._cards = cards;
  }

  Consumable() { this._consumables.push(BattleRewards.DrawConsumable(this._layer, this._rng)); }

  Page(percent) {
    if (RngExtensions.NextChance(this._rng, percent / 100.0)) this._pages++;
  }

  Result() { return new BattleReward(this._cards, this._consumables, this._pages); }
}

/** §4.2: the reward of a won encounter on a combat node, around its unique cards. */
export const BattleRewards = {
  /** §4.2: layer 7's battles always drop a page, and draw a card apart from it at this chance. */
  LastLayerCardPercent: 50,

  /** §4.2: a stand-in boss of layers 1〜2 drops a page at this chance. */
  StandInBossPagePercent: 50,

  /** §4.3: the consumables a fight can drop, in the weight table's column order. */
  ConsumableIds: Object.freeze(['joka_no_ko', 'yakuso', 'koi_hoshiniku', 'wasuremizu', 'shirube_no_ishi']),

  /**
   * The chance in percent a 通常戦闘 of a layer 1〜6 drops a card, a consumable and a page.
   * C# returns the tuple (int Card, int Consumable, int Page); here { Card, Consumable, Page }.
   */
  NormalChances(layer) {
    BattleRewards.CheckLayer(layer);
    if (layer === SevenLayers.Count) {
      throw new ArgumentOutOfRangeException(`Layer 7 drops a page and draws a card apart from it. (Parameter 'layer')\nActual value was ${layer}.`);
    }
    const row = NormalPercent[layer - 1];
    return { Card: row[0], Consumable: row[1], Page: row[2] };
  },

  /** §4.3: the weight a consumable has on a layer; 0 for an id the table does not name. */
  WeightOf(consumableId, layer) {
    BattleRewards.CheckLayer(layer);
    const index = IndexOf(consumableId);
    return index < 0 ? 0 : ConsumableWeights[layer - 1][index];
  },

  /** One consumable, drawn by the layer's weights. */
  DrawConsumable(layer, rng) {
    BattleRewards.CheckLayer(layer);
    if (rng == null) throw nullArg('rng');
    const weights = ConsumableWeights[layer - 1];
    let roll = RngExtensions.NextInt(rng, 0, sum(weights));
    for (let i = 0; i < weights.length; i++) {
      if (roll < weights[i]) return BattleRewards.ConsumableIds[i];
      roll -= weights[i];
    }
    throw new InvalidOperationException('BattleRewards: the weights did not cover the roll.');
  },

  /**
   * §4.2 for a won encounter on a node of `kind`, given the unique cards it already teaches
   * (UniqueCards.For).
   *
   * - 通常戦闘, layers 1〜6: a card, a consumable or a page, by the layer's chances.
   * - 通常戦闘, layer 7: a page, and a card at 50% apart from it.
   * - 精鋭: a card and a consumable, and a page at the layer's chance. The card is the real elite's
   *   offer when there is one; otherwise (a stand-in, or both its cards owned) it is the pile's.
   * - A real boss: a page on top of its three cards.
   * - A stand-in boss that is an elite (layers 4〜5): a consumable and a page on top of its cards.
   * - Any other stand-in boss (layers 1〜2): a card from the pile, a consumable, and a page at 50%.
   *
   * A card the piles can no longer deal becomes a consumable (§4.3).
   */
  Roll(kind, layer, encounter, unique, owned, rng) {
    BattleRewards.CheckLayer(layer);
    if (encounter == null) throw nullArg('encounter');
    if (unique == null) throw nullArg('unique');
    if (owned == null) throw nullArg('owned');
    if (rng == null) throw nullArg('rng');

    const draw = new Draw(layer, owned, rng);
    if (kind === NodeKind.Battle && layer === SevenLayers.Count) {
      draw.Page(100);
      if (RngExtensions.NextChance(rng, BattleRewards.LastLayerCardPercent / 100.0)) draw.Card();
    } else if (kind === NodeKind.Battle) {
      const percent = NormalPercent[layer - 1];
      const roll = RngExtensions.NextInt(rng, 0, 100);
      if (roll < percent[0]) draw.Card();
      else if (roll < percent[0] + percent[1]) draw.Consumable();
      else draw.Page(100);
    } else if (kind === NodeKind.Elite) {
      if (unique.Offered.length === 0) draw.Card();
      draw.Consumable();
      draw.Page(ElitePagePercent[layer - 1]);
    } else if (kind === NodeKind.Boss && !encounter.IsStandIn) {
      draw.Page(100);
    } else if (kind === NodeKind.Boss && IsEliteStandIn(encounter)) {
      draw.Consumable();
      draw.Page(100);
    } else if (kind === NodeKind.Boss) {
      draw.Card();
      draw.Consumable();
      draw.Page(BattleRewards.StandInBossPagePercent);
    } else {
      return BattleReward.None;
    }
    return draw.Result();
  },

  /** C# internal static. */
  CheckLayer(layer) {
    if (layer < 1 || layer > SevenLayers.Count) {
      throw new ArgumentOutOfRangeException(`Layers run 1〜7. (Parameter 'layer')\nActual value was ${layer}.`);
    }
  },
};
