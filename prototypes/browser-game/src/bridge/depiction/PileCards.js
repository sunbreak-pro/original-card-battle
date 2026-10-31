// Port of unity-port/unity-project-kit/Assets/View/Depiction/Bridge/PileCards.cs
// The rows of the pile lists (battle_ui_ux_v2 §1.1 C4, #455): what the draw, discard and exile piles
// hold, one row per kind of card with how many the pile holds and how many the battle's deck holds
// ("突き 2 / 3"). Pure: BattleCore + Depiction.Script.
//
// The counting is presentation, like the lamps' "any enemy" fold: the core keeps the piles
// (BattleState.DrawPile / DiscardPile / Exiled) and this only groups them. The rows follow the card
// catalogue's order, never the pile's, so the list does not give away what is drawn next.
import { ArgumentNullException, compareOrdinal, INT_MAX } from '../../shared/cs.js';
import { CardCatalog } from '../../core/battle/CardCatalog.js';
import { PileCard } from '../depictionScript/DepictionTypes.js';

/** new ArgumentNullException(nameof(x)) with .NET's message. */
function argNull(name) {
  const e = new ArgumentNullException(`Value cannot be null. (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

/** private static Dictionary<string, int> _catalogOrder (built on first use, like the C#). */
let _catalogOrder = null;

/** C# Dictionary.TryGetValue on either a Map or a plain object (deck may be handed in as either). */
function tryGetCount(dict, key) {
  if (dict instanceof Map) return dict.has(key) ? { ok: true, value: dict.get(key) } : { ok: false, value: 0 };
  return Object.prototype.hasOwnProperty.call(dict, key) ? { ok: true, value: dict[key] } : { ok: false, value: 0 };
}

export const PileCards = {
  /** Writes the three lists of `state` onto `piles`. */
  Fill(piles, state) {
    if (piles == null) throw argNull('piles');
    if (state == null) throw argNull('state');
    const deck = new Map();
    PileCards.Count(deck, state.Hand);
    PileCards.Count(deck, state.DrawPile);
    PileCards.Count(deck, state.DiscardPile);
    PileCards.Count(deck, state.Exiled);
    piles.DrawCards = PileCards.Rows(state.DrawPile, deck);
    piles.DiscardCards = PileCards.Rows(state.DiscardPile, deck);
    piles.ExileCards = PileCards.Rows(state.Exiled, deck);
  },

  /**
   * One row per name in `pile`, in the catalogue's order (then by name).
   * @param {Array} pile CardInstance list
   * @param {Map<string, number>|null} deck name → count over the whole battle deck
   */
  Rows(pile, deck) {
    const inPile = new Map();
    const firstId = new Map();
    for (const card of pile) {
      const name = card.Def.Name;
      inPile.set(name, inPile.has(name) ? inPile.get(name) + 1 : 1);
      if (!firstId.has(name)) firstId.set(name, card.Def.Id);
    }
    const names = [...inPile.keys()];
    names.sort((a, b) => {
      const oa = PileCards.OrderOf(firstId.get(a));
      const ob = PileCards.OrderOf(firstId.get(b));
      const byCatalog = oa < ob ? -1 : oa > ob ? 1 : 0;
      return byCatalog !== 0 ? byCatalog : compareOrdinal(a, b);
    });
    const rows = [];
    for (const name of names) {
      const row = new PileCard();
      row.Name = name;
      row.InPile = inPile.get(name);
      let inDeck = inPile.get(name);
      if (deck != null) {
        const got = tryGetCount(deck, name);
        if (got.ok) inDeck = got.value;
      }
      row.InDeck = inDeck;
      rows.push(row);
    }
    return rows;
  },

  /** private static void Count(Dictionary<string, int> deck, IReadOnlyList<CardInstance> cards) */
  Count(deck, cards) {
    for (const card of cards) {
      const name = card.Def.Name;
      deck.set(name, deck.has(name) ? deck.get(name) + 1 : 1);
    }
  },

  /** Where the card stands in CardCatalog.All; a card the catalogue lacks goes after every one it has. */
  OrderOf(id) {
    if (_catalogOrder == null) {
      const order = new Map();
      let i = 0;
      for (const def of CardCatalog.All) {
        if (!order.has(def.Id)) order.set(def.Id, i);
        i++;
      }
      _catalogOrder = order;
    }
    const key = id ?? '';
    return _catalogOrder.has(key) ? _catalogOrder.get(key) : INT_MAX;
  },
};
