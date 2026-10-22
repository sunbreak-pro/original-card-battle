// Port of unity-port/unity-project-kit/Assets/View/Depiction/Bridge/StatusChipText.cs
//
// The status chips and their hover panel (battle-visual-v1 §7.3, #242): the family a word belongs to,
// and its 効き方 / 減り方 written from the status table's values (BattleCore Constants / Statuses), so
// the View prints the panel and computes nothing. Pure: BattleCore + Depiction.Script.
import { ArgumentOutOfRangeException } from '../../shared/cs.js';
import { StatusKind, StatusDecay, EnumTokens } from '../../core/battle/Types.js';
import { Constants } from '../../core/battle/Constants.js';
import { Statuses } from '../../core/battle/Statuses.js';
import { CardCatalog } from '../../core/battle/CardCatalog.js';
import { ChipKind, StatusChip } from '../depictionScript/DepictionTypes.js';
import { CoreText } from './CoreText.js';

/** new ArgumentOutOfRangeException(paramName, actualValue, message), with .NET's Message text. */
function outOfRange(paramName, actualValue, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${paramName}')\nActual value was ${actualValue}.`);
}

export class StatusChipText {
  /** §7.3: the chip for one status word and its stacks, panel text included. */
  static Chip(kind, stacks) {
    const family = StatusChipText.KindOf(kind);
    const chip = new StatusChip();
    chip.Label = EnumTokens.ToLabel(kind);
    chip.Stacks = stacks;
    chip.Kind = family;
    chip.Title = EnumTokens.ToLabel(kind) + ' ' + stacks;
    chip.Group = StatusChipText.GroupWord(family);
    chip.Effect = StatusChipText.Effect(kind, stacks);
    chip.Decay = StatusChipText.Decay(kind);
    return chip;
  }

  /**
   * §7.3 永続の状態: one chip per stance held; the same stance held `count` times is one chip that
   * works that many times ("×2" from two on). Its 効き方 is the stance's own clause when the card
   * catalog knows the name, else the general one.
   */
  static StanceChip(name, count) {
    const clause = StatusChipText.StanceClause(name);
    const times = count > 1 ? '（' + count + ' 枚ぶん、' + count + ' 倍効く）' : '';
    const chip = new StatusChip();
    chip.Label = '構え・' + name;
    // StatusBarView reads a chip with no stacks as the stance line; a repeat keeps its count.
    chip.Stacks = count > 1 ? count : 0;
    chip.Kind = ChipKind.Lasting;
    chip.Title = name + (count > 1 ? ' ×' + count : '');
    chip.Group = StatusChipText.LastingGroup;
    chip.Effect = (clause.length > 0 ? clause : '置いたスタンスの効果が、戦闘のあいだ効き続ける') + times;
    chip.Decay = '減らない（永続）';
    return chip;
  }

  /** §7.3: the square chips (put on the opponent), the round ones (put on oneself), the bosses' words. */
  static KindOf(kind) {
    switch (kind) {
      case StatusKind.Bleed:
      case StatusKind.Fragile:
      case StatusKind.Slow:
      case StatusKind.Intimidate:
      case StatusKind.Fatigue:
        return ChipKind.OnFoe;
      case StatusKind.Empower:
      case StatusKind.Focus:
      case StatusKind.Parry:
      case StatusKind.Swift:
      case StatusKind.Regen:
        return ChipKind.OnSelf;
      case StatusKind.MiasmaShroud:
      case StatusKind.Binding:
      case StatusKind.Hook:
      case StatusKind.Depths:
      case StatusKind.Rooting:
      case StatusKind.Withering:
        return ChipKind.Boss;
      default:
        throw outOfRange('kind', kind, 'No chip family for the status ' + kind);
    }
  }

  static GroupWord(kind) {
    switch (kind) {
      case ChipKind.OnFoe: return StatusChipText.OnFoeGroup;
      case ChipKind.OnSelf: return StatusChipText.OnSelfGroup;
      case ChipKind.Lasting: return StatusChipText.LastingGroup;
      case ChipKind.Boss: return StatusChipText.BossGroup;
      default: throw outOfRange('kind', kind, 'No group word for the chip family ' + kind);
    }
  }

  /**
   * §7.3 効き方, with the numbers of battle_core_v4 §5 (Constants). The word that moves HP each
   * turn also says what it does at the stacks held now. The bosses' words are provisional until
   * #88 writes them; they follow the core's own reading (BattleCore Types / Statuses).
   */
  static Effect(kind, stacks) {
    switch (kind) {
      case StatusKind.Bleed:
        return 'ターン開始に HP −' + Constants.BleedPerStack + '×スタック（いまは −' + Constants.BleedPerStack * stacks + '）';
      case StatusKind.Fragile:
        return '次に受ける攻撃の威力が ' + StatusChipText.Mult(Constants.FragileMult) + ' 倍（Guard で引く前の値）';
      case StatusKind.Slow:
        return '次のターン、自分が動くマスと、自分が押す / 引くマスが 1 減る';
      case StatusKind.Intimidate:
        return '次の行動の威力と Guard が −' + Constants.IntimidatePenalty;
      case StatusKind.Fatigue:
        return '次のターン開始のスタミナ回復が −' + Constants.FatiguePenalty;
      case StatusKind.Empower:
        return '次の攻撃の威力が ' + StatusChipText.Mult(Constants.EmpowerMult) + ' 倍';
      case StatusKind.Focus:
        return '次に出す札の値が 1 段強くなる（コストは変わらない。いちばん強い段は超えない）';
      case StatusKind.Parry:
        return '次に攻撃を受けたとき、Guard で防いだ分の半分を相手に返す';
      case StatusKind.Swift:
        return 'ターン開始に、1 マスだけ無料で前か後ろへ動ける';
      case StatusKind.Regen:
        return 'ターン開始に HP +' + Constants.RegenPerStack + '×スタック（いまは +' + Constants.RegenPerStack * stacks + '）';
      case StatusKind.MiasmaShroud:
        return '最大スタミナが スタック 1 につき 1 下がる';
      case StatusKind.Binding:
        return '自分で動く効果が起きない。移動だけの札は出せない';
      case StatusKind.Hook:
        return '自分で後ろへ下がれない（前へは動ける）';
      case StatusKind.Depths:
        return '間合い 0 でターンを始めると、スタミナ回復が スタック 1 につき 1 減る';
      case StatusKind.Rooting:
        return '2 ターン続けて同じマスで終えると、次のターン開始に HP −' + Statuses.RootingHpPerStack + '×スタック';
      case StatusKind.Withering:
        return 'ターン開始のスタミナ回復が スタック 1 につき 1 減る';
      default:
        throw outOfRange('kind', kind, 'No panel text for the status ' + kind);
    }
  }

  /** §7.3 減り方, from the word's decay type (Statuses.DecayOf). 見切り keeps its stack when no Guard took the blow. */
  static Decay(kind) {
    if (kind === StatusKind.Parry) return '効くと 1 減る（Guard 0 で受けたときは減らない）';
    switch (Statuses.DecayOf(kind)) {
      case StatusDecay.OnTurn: return 'ターンごとに 1 減る';
      case StatusDecay.OnUse: return '効くと 1 減る';
      default: return '減らない（戦闘のあいだ残る）';
    }
  }

  /** The chips of one unit: the stances first, one chip per distinct stance, then the words in their fixed order. */
  static Chips(statuses, stanceNames) {
    const chips = StatusChipText.StanceChips(stanceNames);
    for (const kind of statuses.Kinds) chips.push(StatusChipText.Chip(kind, statuses.Stacks(kind)));
    return chips;
  }

  /** One chip per distinct stance name in the order first held, with the times it is held. */
  static StanceChips(stanceNames) {
    const chips = [];
    const order = [];
    const count = new Map();
    for (const name of stanceNames) {
      if (name == null || name === '') continue;
      if (!count.has(name)) { order.push(name); count.set(name, 0); }
      count.set(name, count.get(name) + 1);
    }
    for (const name of order) chips.push(StatusChipText.StanceChip(name, count.get(name)));
    return chips;
  }

  /**
   * private. 1.5 as "1.5" whatever the machine's culture (a comma would read as a list):
   * double.ToString(InvariantCulture). The two values it prints (1.5) read the same in JS.
   */
  static Mult(value) {
    return String(value);
  }

  /** private */
  static StanceClause(name) {
    for (const def of CardCatalog.All) {
      if (def.Name === name && def.Face.Stance != null) return CoreText.StanceText(def.Face.Stance);
    }
    return '';
  }
}

StatusChipText.OnFoeGroup = '相手に付ける系';
StatusChipText.OnSelfGroup = '自分に付ける系';
StatusChipText.LastingGroup = '永続';
StatusChipText.BossGroup = 'ボス専用';
