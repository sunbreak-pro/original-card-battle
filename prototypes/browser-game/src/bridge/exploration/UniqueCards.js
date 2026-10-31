// Port of unity-project-kit/Assets/View/Exploration/Bridge/UniqueCards.cs
// Which learned cards a won encounter hands the player (encounters_and_rewards_v4.md §4.2, #99). The
// owners of the cards are the card table's (swordsman_cards_v4.md §3, cards lane); this file copies
// them per enemy id, and is changed when the card table moves a card to another owner.
import { ArgumentNullException } from '../../shared/cs.js';
import { CardCatalog } from '../../core/battle/CardCatalog.js';
import { NodeKind } from '../../core/dungeon/NodeKind.js';

/** IReadOnlyCollection<string>.Contains for a Set (HashSet) or an array (List). */
function contains(collection, id) {
  return collection instanceof Set ? collection.has(id) : collection.includes(id);
}

/** What a won encounter teaches: cards learned at once, and cards to choose one from. */
export class UniqueCardReward {
  static None = new UniqueCardReward([], []);

  constructor(learned, offered) {
    /** Card ids learned on the spot, in card-table order. */
    this.Learned = learned ?? [];
    /** Card ids the player picks one from. Empty when nothing is offered. */
    this.Offered = offered ?? [];
  }

  get IsEmpty() { return this.Learned.length === 0 && this.Offered.length === 0; }

  toJSON() {
    return { $type: 'UniqueCardReward', Learned: this.Learned, Offered: this.Offered, IsEmpty: this.IsEmpty };
  }
}

function Ids(...defs) {
  return defs.map((d) => d.Id);
}

function NotOwned(ids, owned) {
  return ids.filter((id) => !contains(owned, id));
}

// C# static readonly dictionaries; built on first use so the card catalogue is read after it loads.
let _bossCards = null;
let _eliteCards = null;

/** §4.2: a real boss teaches its three cards (roster §4.5 / §5.5 / §6.6 「撃破で 3 種を習得」). */
function BossCards() {
  return _bossCards ??= new Map([
    ['miasma_priest', Ids(CardCatalog.MiasmaBlade, CardCatalog.PriestPrayer, CardCatalog.PurgeFlash)], // 大黒蛇 セルク #41〜#43
    ['abyss_angler', Ids(CardCatalog.LineLash, CardCatalog.AbyssStance, CardCatalog.HaulStep)], // 獄竜 ガルド #44〜#46
    ['distortion_root', Ids(CardCatalog.RootStride, CardCatalog.ThornShot, CardCatalog.RootBind)], // 歪みの根 #47〜#49
  ]);
}

/** §4.2: a real elite offers its two cards, one to pick. */
function EliteCards() {
  return _eliteCards ??= new Map([
    ['armored_warden', Ids(CardCatalog.Bulwark, CardCatalog.ShieldBash)], // 鉄壁の門竜 #50 / #51
    ['pack_alpha', Ids(CardCatalog.PackHowl, CardCatalog.FangRush)], // 統牙の長竜 #52 / #53
    ['polearm_unyielding', Ids(CardCatalog.RiposteStance, CardCatalog.SpearWall)], // 不退の槍竜 #55 / #57
  ]);
}

export const UniqueCards = {
  /** The cards a boss teaches, or an empty list for an enemy that teaches none. */
  OfBoss(enemyId) {
    return enemyId != null && BossCards().has(enemyId) ? BossCards().get(enemyId) : [];
  },

  /** The cards an elite offers, or an empty list for an enemy that offers none. */
  OfElite(enemyId) {
    return enemyId != null && EliteCards().has(enemyId) ? EliteCards().get(enemyId) : [];
  },

  /**
   * §4.2 for a won encounter on a node of `kind`. Cards the player already owns are left out.
   *
   * - A real boss (セルク / ガルド / 歪みの根): its three cards, all learned.
   * - A stand-in boss that is an elite (門竜 on layer 4, 不退 on layer 5): that elite's two cards,
   *   all learned.
   * - A real elite: its two cards offered, one to pick.
   *
   * Everything else on the table — the layer's card pile, consumables and pages, and the pile a
   * stand-in elite or a layer 1〜2 stand-in boss draws from — is the rest of the reward table,
   * not a unique card, and is not handed back here.
   */
  For(kind, encounter, owned) {
    if (encounter == null) throw new ArgumentNullException("Value cannot be null. (Parameter 'encounter')");
    if (owned == null) throw new ArgumentNullException("Value cannot be null. (Parameter 'owned')");

    const enemies = [...encounter.EnemyIds];
    if (enemies.length !== 1) return UniqueCardReward.None; // pairs and chains are all stand-ins
    const enemy = enemies[0];

    if (kind === NodeKind.Boss && !encounter.IsStandIn)
      return new UniqueCardReward(NotOwned(UniqueCards.OfBoss(enemy), owned), null);
    if (kind === NodeKind.Boss)
      return new UniqueCardReward(NotOwned(UniqueCards.OfElite(enemy), owned), null);
    if (kind === NodeKind.Elite && !encounter.IsStandIn)
      return new UniqueCardReward(null, NotOwned(UniqueCards.OfElite(enemy), owned));
    return UniqueCardReward.None;
  },
};
