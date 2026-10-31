// Port of unity-project-kit/Assets/View/Depiction/Script/DemoDeck.cs
// The five card faces the fixed filming script prints. Pure C#: no UnityEngine, no BattleCore.
// The numbers are the §18 draft scale the slice was recorded at (attack 6 / 13, guard 4 / 9); they
// are print, not rules. A played battle takes its cards and rules from BattleCore (#571).
//
// Port notes:
// - `TraitCondition` here is the Depiction namespace's enum, not BattleCore's (same name, other type).
// - DemoCard is a mutable class: `new DemoCard()` gives the C# field initializers (an enum field
//   without one is its first member), and `new DemoCard({ ...members })` is C#'s object initializer.
// - DemoDeck.All is C#'s static readonly list, built once on first read (a lazy getter keeps the
//   module free of evaluation-order trouble in import cycles).
import { CardAim, CardFace, CardKind, RangeSide, UnitSide } from './DepictionTypes.js';
import { defineEnum } from '../../shared/cs.js';

/** When a card's printed trait adds its bonus. */
export const TraitCondition = defineEnum([
  'None',
  /** Nothing has been played yet this turn ("初手 +3"). */
  'FirstCardOfTurn',
  /** The enemy's declared next action is an attack ("予兆 +3"). */
  'AgainstAttackOmen',
]);

/** One card kind: what its face prints. It holds no rule. */
export class DemoCard {
  constructor(members) {
    this.DefId = '';
    this.Name = '';
    this.Cost = 0;
    this.Kind = CardKind.Attack;
    this.Aim = CardAim.Single;
    /** (UnitSide?) */
    this.Affects = null;
    this.TypeLabel = '';
    this.Description = '';
    this.ValueText = '';
    this.TraitText = '';
    this.Trait = TraitCondition.None;
    /** The range side the card may be played from; null when either side works. (RangeSide?) */
    this.RequiredRange = null;
    if (members != null) Object.assign(this, members);
  }
}

let all = null;

function buildAll() {
  return [
    new DemoCard({
      DefId: DemoDeck.Kesagiri, Name: '袈裟斬り', Cost: 1, Kind: CardKind.Attack, Aim: CardAim.Single,
      TypeLabel: '攻撃・敵単体', Description: '敵に 6 ダメージ。ターン最初なら +3。',
      ValueText: '6', TraitText: '初手 +3', Trait: TraitCondition.FirstCardOfTurn,
      RequiredRange: RangeSide.Near,
    }),
    new DemoCard({
      DefId: DemoDeck.Daijodan, Name: '大上段', Cost: 2, Kind: CardKind.Attack, Aim: CardAim.Single,
      TypeLabel: '攻撃・敵単体', Description: '敵に 13 ダメージ。振りかぶる一撃。',
      ValueText: '13', RequiredRange: RangeSide.Near,
    }),
    new DemoCard({
      DefId: DemoDeck.Ushirotobi, Name: '後ろ跳び', Cost: 1, Kind: CardKind.Move, Aim: CardAim.Self,
      Affects: UnitSide.Player,
      TypeLabel: 'ムーブ・自分', Description: '遠間へ下がる。Guard 4 を得る。予兆が攻撃なら +3。',
      ValueText: '4', TraitText: '予兆 +3', Trait: TraitCondition.AgainstAttackOmen,
      RequiredRange: RangeSide.Near,
    }),
    new DemoCard({
      DefId: DemoDeck.TetsuNoUke, Name: '鉄の受け', Cost: 2, Kind: CardKind.Guard, Aim: CardAim.Self,
      Affects: UnitSide.Player,
      TypeLabel: '防御・自分', Description: 'Guard 9 を得る。重い一撃に備える。',
      ValueText: '9',
    }),
    new DemoCard({
      // Its text follows swordsman_cards_v4 (draw 1 at the first tier).
      DefId: DemoDeck.Kansatsu, Name: '観察', Cost: 1, Kind: CardKind.Skill, Aim: CardAim.Single,
      TypeLabel: '技・敵単体', Description: 'カードを 1 枚引く。相手をよく見る。',
    }),
  ];
}

export const DemoDeck = {
  Kesagiri: 'kesagiri',
  Daijodan: 'daijodan',
  Ushirotobi: 'ushirotobi',
  TetsuNoUke: 'tetsu-no-uke',
  Kansatsu: 'kansatsu',

  /** (IList<DemoCard>) The same list on every read, as C#'s static readonly field. */
  get All() {
    if (all === null) all = buildAll();
    return all;
  },

  /** The kind an instance id belongs to ("kesagiri#1" -> "kesagiri"). */
  DefIdOf(instanceId) {
    const hash = instanceId.indexOf('#');
    return hash < 0 ? instanceId : instanceId.substring(0, hash);
  },

  /** How a range side is printed. The View prints the string as given. */
  Glyph(range) {
    return range === RangeSide.Near ? '近' : '遠';
  },

  /**
   * The face of one dealt card. `instanceId` tells copies apart, so the hand can hold two of a kind
   * and the screen still knows which one was dragged.
   */
  Face(def, instanceId, traitLit) {
    return new CardFace({
      Id: instanceId,
      Name: def.Name,
      Cost: def.Cost,
      Kind: def.Kind,
      Aim: def.Aim,
      Affects: def.Affects,
      TypeLabel: def.TypeLabel,
      Description: def.Description,
      ValueText: def.ValueText,
      TraitText: def.TraitText,
      TraitLit: traitLit && def.Trait !== TraitCondition.None,
      RequiredRange: def.RequiredRange,
      RequiredRangeGlyph: def.RequiredRange != null ? DemoDeck.Glyph(def.RequiredRange) : '',
    });
  },
};
