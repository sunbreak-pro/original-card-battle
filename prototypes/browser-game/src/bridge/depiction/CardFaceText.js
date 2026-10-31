// Port of unity-port/unity-project-kit/Assets/View/Depiction/Bridge/CardFaceText.cs
//
// The card face of battle-visual-v1 (#242): the value columns, the rows under them, the two rows of
// the trait box, the one line on hover, the right-click detail and the lamp for each enemy. Every
// word is written here from the card's data, so the View prints and judges nothing. Pure:
// BattleCore + Depiction.Script, no DOM.
//
// Out parameter: Lamps(state, instanceId, out List<bool> perEnemy) returns { ok, value } — ok is the
// C# return value (the aggregated lamp), value the perEnemy list.
// Glossary (C# static readonly IReadOnlyDictionary) is a Map built once on first read, so this
// module reads Constants only when asked (no module-evaluation-time work across import cycles).
import { hasFlag, enumIndex, ArgumentNullException, ArgumentOutOfRangeException } from '../../shared/cs.js';
import { BattleAttribute, TargetKind, TraitCondition, TraitEffect, EnumTokens } from '../../core/battle/Types.js';
import { Constants } from '../../core/battle/Constants.js';
import { TurnLoop } from '../../core/battle/TurnLoop.js';
import { EnemyAi } from '../../core/battle/EnemyAi.js';
import {
  CardValue, CardValueKind, TermDefinition, TermRow, TermMark, CardDetail,
} from '../depictionScript/DepictionTypes.js';
import { CoreText } from './CoreText.js';

/** new ArgumentOutOfRangeException(paramName, actualValue, message), with .NET's Message text. */
function outOfRange(paramName, actualValue, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${paramName}')\nActual value was ${actualValue}.`);
}

/** new ArgumentNullException(paramName), with .NET's Message text. */
function argNull(paramName) {
  return new ArgumentNullException(`Value cannot be null. (Parameter '${paramName}')`);
}

let glossary = null;

export class CardFaceText {
  /** Fills the battle-visual-v1 fields of `face` from the card's data. */
  static Fill(face, def) {
    if (face == null) throw argNull('face');
    if (def == null) throw argNull('def');
    face.PrintedCost = def.Cost;
    face.Values = CardFaceText.Values(def.Face, def.Attributes);
    face.Lasting = def.Face.Stance != null && hasFlag(def.Attribute, BattleAttribute.Stance);
    face.TextLines = face.Lasting ? CardFaceText.LastingLines(def.Face) : CardFaceText.TextLines(def.Face, def.Attributes);
    face.Plain = def.AllTraits.length === 0;
    const rows = CardFaceText.TraitRows(def);
    face.TraitTop = rows[0];
    face.TraitBottom = rows[1];
    face.HoverLine = CardFaceText.HoverLine(def);
    face.Detail = CardFaceText.Detail(def);
    face.Terms = CardFaceText.TermMarks(def, face.TraitTop, face.TraitBottom);
  }

  // ---- §7.1 用語の下線 ----------------------------------------------------------------------

  /**
   * §7.1 / §6.1 (#455): the glossary words the card uses (TermsOf) where its trait box prints them,
   * so the View underlines those characters and shows the meaning on hover. A word is looked for in
   * the top row, then the bottom one, and never on characters another word has taken (崩し後 holds
   * 崩し). A word the box does not print is left to the right-click detail.
   */
  static TermMarks(def, top, bottom) {
    const marks = [];
    // The longer printed word first, so 崩し後 takes its characters before 崩し can.
    const words = CardFaceText.TermsOf(def);
    const order = words.slice();
    // C#'s List.Sort is unstable, but the comparison never ties (the words are distinct).
    order.sort((a, b) => {
      const byLength = compareInt(CardFaceText.LongestForm(b), CardFaceText.LongestForm(a));
      return byLength !== 0 ? byLength : compareInt(words.indexOf(a), words.indexOf(b));
    });
    const table = CardFaceText.Glossary;
    for (const word of order) {
      if (!table.has(word)) continue;
      const meaning = table.get(word);
      for (const printed of CardFaceText.PrintedForms(word)) {
        if (CardFaceText.Mark(marks, TermRow.TraitTop, top, printed, word, meaning)) break;
        if (CardFaceText.Mark(marks, TermRow.TraitBottom, bottom, printed, word, meaning)) break;
      }
    }
    // Never ties either: two marks of one row never share a start (they would overlap).
    marks.sort((a, b) => (a.Row !== b.Row
      ? compareInt(enumIndex(TermRow, a.Row), enumIndex(TermRow, b.Row))
      : compareInt(a.Start, b.Start)));
    return marks;
  }

  /** private */
  static LongestForm(word) {
    let longest = 0;
    for (const form of CardFaceText.PrintedForms(word)) longest = Math.max(longest, form.length);
    return longest;
  }

  /**
   * How a glossary word is printed on a face: "連動 X" as 連動, "崩し n" as 崩し, and a pair such as
   * "詰める / 離れる" as either of its words. (C# yields an IEnumerable; this returns an array.)
   */
  static PrintedForms(word) {
    if (word.includes(' / ')) {
      return word.split(' / ').filter((part) => part.length > 0);
    }
    if (word.endsWith(' X') || word.endsWith(' n')) {
      return [word.substring(0, word.length - 2)];
    }
    return [word];
  }

  /** private */
  static Mark(marks, row, text, printed, word, meaning) {
    if (text == null || text === '' || printed == null || printed === '') return false;
    let from = 0;
    while (from <= text.length - printed.length) {
      const at = text.indexOf(printed, from);
      if (at < 0) return false;
      if (!CardFaceText.Taken(marks, row, at, printed.length)) {
        const mark = new TermMark();
        mark.Row = row;
        mark.Start = at;
        mark.Length = printed.length;
        mark.Word = word;
        mark.Meaning = meaning;
        marks.push(mark);
        return true;
      }
      from = at + 1;
    }
    return false;
  }

  /** private */
  static Taken(marks, row, start, length) {
    for (const mark of marks) {
      if (mark.Row === row && start < mark.Start + mark.Length && mark.Start < start + length) return true;
    }
    return false;
  }

  // ---- §6.1 値 ----------------------------------------------------------------------------

  /**
   * The value columns in the order the card resolves them (§2.2): the blow, the move, the
   * push or pull, the Guard, the heal, the draw. Only the first two are printed as columns;
   * TextLines writes the rest.
   */
  static Values(face, attributes) {
    const all = CardFaceText.AllValues(face, attributes);
    return all.length > CardFaceText.MaxValues ? all.slice(0, CardFaceText.MaxValues) : all;
  }

  /** private */
  static AllValues(face, attributes) {
    const values = [];
    if (face.Stance != null && hasFlag(attributes, BattleAttribute.Stance)) return values;
    if (hasFlag(attributes, BattleAttribute.Attack)) values.push(CardFaceText.Value(CardValueKind.Power, String(CoreText.FacePower(face)), '威力'));
    if (face.Move > 0) values.push(CardFaceText.Value(CardValueKind.Close, String(face.Move), '詰める'));
    else if (face.Move < 0) values.push(CardFaceText.Value(CardValueKind.Back, String(-face.Move), '離れる'));
    if (face.Push > 0) values.push(CardFaceText.Value(CardValueKind.Push, String(face.Push), '押す'));
    else if (face.Push < 0) values.push(CardFaceText.Value(CardValueKind.Pull, String(-face.Push), '引く'));
    if (face.Guard > 0) values.push(CardFaceText.Value(CardValueKind.Guard, String(face.Guard), 'Guard'));
    if (face.Heal > 0) values.push(CardFaceText.Value(CardValueKind.Heal, '+' + face.Heal, '回復'));
    if (face.Draw > 0) values.push(CardFaceText.Value(CardValueKind.Draw, String(face.Draw), 'ドロー'));
    return values;
  }

  /** private */
  static Value(kind, number, word) {
    const value = new CardValue();
    value.Kind = kind;
    value.Number = number;
    value.Word = word;
    return value;
  }

  // ---- §6.1 説明 ----------------------------------------------------------------------------

  /**
   * The rows under the values: the statuses first (「敵に 鈍足 2・疲労 2」, 「自分に 強化 2」),
   * then what has no column (崩し, スタミナ, and any value past the second). At most three rows.
   */
  static TextLines(face, attributes) {
    const lines = [];
    const foe = CardFaceText.StatusRow(face, false);
    const self = CardFaceText.StatusRow(face, true);
    if (foe.length > 0) lines.push('敵に ' + foe);
    if (self.length > 0) lines.push('自分に ' + self);
    if (face.Break > 0) lines.push('崩し ' + face.Break);
    if (face.StaminaGain > 0) lines.push('スタミナ +' + face.StaminaGain);
    const all = CardFaceText.AllValues(face, attributes);
    for (let i = CardFaceText.MaxValues; i < all.length; i++) lines.push(all[i].Word + ' ' + all[i].Number);
    return lines.length > CardFaceText.MaxTextLines ? lines.slice(0, CardFaceText.MaxTextLines) : lines;
  }

  /** private */
  static StatusRow(face, onSelf) {
    const parts = [];
    for (const grant of face.StatusList) {
      if (grant.OnSelf === onSelf) parts.push(EnumTokens.ToLabel(grant.Kind) + ' ' + grant.Stacks);
    }
    return parts.join('・');
  }

  /** §6.1 スタンスの札: one lasting effect per row; the View puts ∞ at the head of each. */
  static LastingLines(face) {
    const lines = [];
    const text = CoreText.StanceText(face.Stance);
    if (text.length > 0) lines.push(text);
    return lines.length > CardFaceText.MaxLastingLines ? lines.slice(0, CardFaceText.MaxLastingLines) : lines;
  }

  // ---- §6.1 特性の箱 / §6.3 ---------------------------------------------------------------

  /**
   * The two rows of the trait box. One trait: the condition in §6.1's short words, then
   * "→ effect". Two traits (背水の陣, #213): one trait per row, condition and effect run together
   * ("間合い2以上→威力+5"). No trait: the plain card's two rows.
   */
  static TraitRows(def) {
    const traits = def.AllTraits;
    if (traits.length === 0) return [CardFaceText.PlainTop, CardFaceText.PlainBottom];
    if (traits.length === 1) return [CardFaceText.Condition(traits[0], def), '→ ' + CardFaceText.Effect(traits[0], def)];
    return [CardFaceText.Compact(traits[0], def), CardFaceText.Compact(traits[1], def)];
  }

  /** private */
  static Compact(trait, def) {
    return (CardFaceText.Condition(trait, def) + '→' + CardFaceText.Effect(trait, def)).split(' ').join('');
  }

  /** §6.1: the condition in the short words ("間合い 2 以上", "敵が出血中", "連動 スタンス"). */
  static Condition(trait, def) {
    switch (trait.Condition) {
      case TraitCondition.GapAtMost:
        return trait.Threshold === 0 ? '間合い 0' : '間合い ' + trait.Threshold + ' 以下';
      case TraitCondition.GapAtLeast: return '間合い ' + trait.Threshold + ' 以上';
      case TraitCondition.Unguarded: return '敵の Guard 0';
      case TraitCondition.Reserve: return '温存 残り ' + trait.Threshold + ' 以上';
      case TraitCondition.Combo: return '連動 ' + CardFaceText.AttributeWord(trait.Attribute);
      case TraitCondition.Moved: return '移動後';
      case TraitCondition.OmenIs:
        return (CardFaceText.IsStance(def) ? '置く時 ' : '') + '予兆が' + CardFaceText.OmenWord(trait.Omen);
      case TraitCondition.Desperate: return '死力';
      case TraitCondition.FirstPlay: return '初手';
      case TraitCondition.Finisher: return '締め';
      case TraitCondition.Broken: return '崩し後';
      case TraitCondition.Chain: return '連打';
      case TraitCondition.Thin: return '手薄';
      case TraitCondition.FoeHas: return '敵が' + CardFaceText.WatchWord(trait) + '中';
      case TraitCondition.SelfHas: return '自分が' + CardFaceText.WatchWord(trait) + '中';
      default: throw outOfRange('trait', trait.Condition, 'No short words for the trait condition ' + trait.Condition);
    }
  }

  /** §6.1: the effect after the arrow ("威力 +5", "敵に脆化 2", "重撃"). */
  static Effect(trait, def) {
    switch (trait.Effect) {
      case TraitEffect.PowerBonus: return '威力 +' + trait.Amount;
      case TraitEffect.GuardBonus: return 'Guard +' + trait.Amount + (CardFaceText.IsStance(def) ? '（1 回）' : '');
      case TraitEffect.NextTurnRecovery: return '次ターン回復 +' + trait.Amount;
      case TraitEffect.HeavyBlow: return '重撃';
      case TraitEffect.StaminaGain: return 'スタミナ +' + trait.Amount;
      case TraitEffect.Draw: return 'ドロー +' + trait.Amount;
      case TraitEffect.Status:
        return trait.Grant == null ? '' : (trait.Grant.OnSelf ? '自分に' : '敵に') + EnumTokens.ToLabel(trait.Grant.Kind) + ' ' + trait.Grant.Stacks;
      case TraitEffect.CostDown: return 'コスト −' + trait.Amount;
      case TraitEffect.Convert: return '転換';
      case TraitEffect.FollowUp: return '追撃';
      case TraitEffect.BreakBonus: return '崩し +' + trait.Amount;
      default: throw outOfRange('trait', trait.Effect, 'No short words for the trait effect ' + trait.Effect);
    }
  }

  // ---- §7.2 ホバーの 1 行 ---------------------------------------------------------------

  /**
   * §7.2 / §9: the one line above a hovered card, the condition as a clause and the effect with
   * the card's printed values ("間合い 2 以上なら、この札の威力 13 → 18"). Two traits are two
   * sentences. A plain card says what its +2 went into.
   */
  static HoverLine(def) {
    const traits = def.AllTraits;
    if (traits.length === 0) return CardFaceText.PlainLine(def);
    const parts = [];
    for (const trait of traits) parts.push(CardFaceText.ConditionClause(trait, def) + '、' + CardFaceText.EffectClause(trait, def));
    return parts.join('。');
  }

  /** private */
  static PlainLine(def) {
    const what = hasFlag(def.Attributes, BattleAttribute.Attack) ? '威力'
      : def.Face.Guard > 0 ? 'Guard'
        : '値';
    const value = hasFlag(def.Attributes, BattleAttribute.Attack) ? String(CoreText.FacePower(def.Face))
      : def.Face.Guard > 0 ? String(def.Face.Guard)
        : '';
    return '特性なし。そのぶん' + what + 'が ' + Constants.PlainCardBonus + ' 高い' + (value.length > 0 ? '（' + value + ' に込み）' : '');
  }

  /** §9: the condition as the clause before the comma ("隣り合っていれば", "いちばん近い敵の予兆が攻撃なら"). */
  static ConditionClause(trait, def) {
    switch (trait.Condition) {
      case TraitCondition.GapAtMost:
        return trait.Threshold === 0 ? '隣り合っていれば' : '間合い ' + trait.Threshold + ' 以下なら';
      case TraitCondition.GapAtLeast: return '間合い ' + trait.Threshold + ' 以上なら';
      case TraitCondition.Unguarded: return '敵の Guard が 0 なら';
      case TraitCondition.Reserve: return '出した後スタミナが ' + trait.Threshold + ' 以上残れば';
      case TraitCondition.Combo: return 'このターンに' + CardFaceText.AttributeWord(trait.Attribute) + 'の札を出していれば';
      case TraitCondition.Moved: return 'このターンにもう動いていれば';
      case TraitCondition.OmenIs: {
        const kind = CardFaceText.OmenWord(trait.Omen);
        if (CardFaceText.IsStance(def)) return '置くときに敵の予兆が' + kind + 'なら';
        if (!CardFaceText.Directed(def)) return 'いちばん近い敵の予兆が' + kind + 'なら';
        return '敵の予兆が' + kind + 'なら';
      }
      case TraitCondition.Desperate: return '出す前のスタミナが ' + Constants.DesperateThreshold + ' 以下なら';
      case TraitCondition.FirstPlay: return 'このターン最初に出す札なら';
      case TraitCondition.Finisher: return 'このターン ' + Constants.FinisherPlayNumber + ' 枚目以降に出すなら';
      case TraitCondition.Broken: return '敵のスタミナが ' + Constants.BrokenBelow + ' 未満なら';
      case TraitCondition.Chain: return 'このターン直前に出した札も' + CardFaceText.AttributeWord(def.Attribute) + 'なら';
      case TraitCondition.Thin: return '出した直後の手札が ' + Constants.ThinHandMax + ' 枚以下なら';
      case TraitCondition.FoeHas: return '敵が' + CardFaceText.WatchWord(trait) + '中なら';
      case TraitCondition.SelfHas: return '自分が' + CardFaceText.WatchWord(trait) + '中なら';
      default: throw outOfRange('trait', trait.Condition, 'No clause for the trait condition ' + trait.Condition);
    }
  }

  /** §9: what the trait does to the card's printed values ("この札の威力 13 → 18"). */
  static EffectClause(trait, def) {
    const power = CoreText.FacePower(def.Face);
    switch (trait.Effect) {
      case TraitEffect.PowerBonus: return 'この札の威力 ' + power + ' → ' + (power + trait.Amount);
      case TraitEffect.GuardBonus:
        return CardFaceText.IsStance(def)
          ? 'その場で Guard +' + trait.Amount + '（1 回だけ）'
          : 'この札の Guard ' + def.Face.Guard + ' → ' + (def.Face.Guard + trait.Amount);
      case TraitEffect.NextTurnRecovery:
        return '次ターンの回復 ' + Constants.StaminaRecovery + ' → ' + (Constants.StaminaRecovery + trait.Amount);
      case TraitEffect.HeavyBlow:
        return '威力 ' + power + ' → ' + (power + Constants.HeavyBlowPower) + '。ただし次ターンの回復 '
          + Constants.StaminaRecovery + ' → ' + (Constants.StaminaRecovery - Constants.HeavyBlowRecoveryPenalty);
      case TraitEffect.StaminaGain: return 'スタミナ +' + trait.Amount;
      case TraitEffect.Draw: return 'さらに ' + trait.Amount + ' 枚引く';
      case TraitEffect.Status:
        return trait.Grant == null ? '' : (trait.Grant.OnSelf ? '自分に' : '敵に') + EnumTokens.ToLabel(trait.Grant.Kind) + ' ' + trait.Grant.Stacks + ' も付ける';
      case TraitEffect.CostDown: return 'この札のコスト ' + def.Cost + ' → ' + Math.max(0, def.Cost - trait.Amount);
      case TraitEffect.Convert: return 'この札で得る Guard の半分を威力に足す（転換）';
      case TraitEffect.FollowUp: return 'このターンに次に出す攻撃の威力 +' + Constants.FollowUpPower + '（追撃）';
      case TraitEffect.BreakBonus: return '崩し +' + trait.Amount;
      default: throw outOfRange('trait', trait.Effect, 'No clause for the trait effect ' + trait.Effect);
    }
  }

  // ---- §7.4 右クリックの詳細 / §7.5 用語 ---------------------------------------------------

  /** §7.4: name, the kind line, the full text and the terms the card uses. */
  static Detail(def) {
    const detail = new CardDetail();
    detail.Name = def.Name;
    detail.KindLine = CardFaceText.KindLine(def);
    detail.Body = CardFaceText.Body(def);
    const table = CardFaceText.Glossary;
    for (const word of CardFaceText.TermsOf(def)) {
      if (table.has(word)) {
        const term = new TermDefinition();
        term.Word = word;
        term.Meaning = table.get(word);
        detail.Terms.push(term);
      }
    }
    return detail;
  }

  /** §7.4 2 行目: "攻撃 ・ 敵 1 体 ・ 間合い 0〜1", "防御 ・ 自分". One attribute, never joined by ＋. */
  static KindLine(def) {
    const kind = CardFaceText.AttributeWord(def.Attribute);
    if (!CardFaceText.Directed(def)) return kind + ' ・ 自分';
    const whom = def.Targets === TargetKind.All ? '届く敵すべて' : '敵 1 体';
    return kind + ' ・ ' + whom + ' ・ 間合い ' + def.Face.ReachOrDefault.ToText();
  }

  /** private */
  static Body(def) {
    const sentences = [];
    if (def.Face.Stance != null && hasFlag(def.Attribute, BattleAttribute.Stance)) {
      sentences.push('スタンス。置いたあと、戦闘のあいだ効き続ける（永続）：' + CoreText.StanceText(def.Face.Stance) + '。');
    }
    let described = CoreText.Describe(def.Face, def.Attributes);
    if (def.Face.Stance != null) {
      // The stance clause is the sentence above; Describe's own 構え: clause would repeat it.
      const at = described.indexOf('構え: ');
      if (at >= 0) described = described.substring(0, at);
    }
    if (described.length > 0) sentences.push(described);
    const traits = def.AllTraits;
    if (traits.length === 0) {
      sentences.push('特性を持たないかわりに、値が ' + Constants.PlainCardBonus + ' 高い札（表の値に込み）。');
    }
    for (const trait of traits) {
      const term = CardFaceText.TermOfCondition(trait.Condition);
      sentences.push((term.length > 0 ? term + '：' : '') + CardFaceText.ConditionClause(trait, def) + '、' + CardFaceText.EffectClause(trait, def) + '。');
    }
    return sentences.join('');
  }

  /** The glossary words the card's detail lists, in the order they first matter. */
  static TermsOf(def) {
    const words = [];
    if (CardFaceText.Directed(def)) CardFaceText.Add(words, '間合い');
    if (def.Face.Move !== 0) CardFaceText.Add(words, '詰める / 離れる');
    if (def.Face.Push !== 0) CardFaceText.Add(words, '押す / 引く');
    if (def.Face.Break > 0) CardFaceText.Add(words, '崩し n');
    if (def.Face.Stance != null && hasFlag(def.Attribute, BattleAttribute.Stance)) CardFaceText.Add(words, '永続');
    const traits = def.AllTraits;
    if (traits.length === 0) CardFaceText.Add(words, '素直な札');
    for (const trait of traits) {
      const condition = CardFaceText.TermOfCondition(trait.Condition);
      if (condition.length > 0) CardFaceText.Add(words, condition === '連動' ? '連動 X' : condition);
      switch (trait.Effect) {
        case TraitEffect.HeavyBlow: CardFaceText.Add(words, '重撃'); break;
        case TraitEffect.Convert: CardFaceText.Add(words, '転換'); break;
        case TraitEffect.FollowUp: CardFaceText.Add(words, '追撃'); break;
        case TraitEffect.BreakBonus: CardFaceText.Add(words, '崩し n'); break;
        default: break;
      }
    }
    return words;
  }

  /** private */
  static Add(words, word) {
    if (!words.includes(word)) words.push(word);
  }

  /** private */
  static TermOfCondition(condition) {
    switch (condition) {
      case TraitCondition.Reserve: return '温存';
      case TraitCondition.Combo: return '連動';
      case TraitCondition.Moved: return '移動後';
      case TraitCondition.Desperate: return '死力';
      case TraitCondition.FirstPlay: return '初手';
      case TraitCondition.Finisher: return '締め';
      case TraitCondition.Broken: return '崩し後';
      case TraitCondition.Chain: return '連打';
      case TraitCondition.Thin: return '手薄';
      // Empty is right here: the other conditions (間合い, 予兆, 相手の状態...) are no glossary word.
      default: return '';
    }
  }

  /**
   * battle-visual-v1 §7.5: the words with a dotted underline and what they mean. 死力, 締め and
   * 崩し後 are not in §7.5's table yet; their meaning is battle_core_v4 §2.3's, with its numbers.
   * A Map in the C# insertion order, built once.
   */
  static get Glossary() {
    if (glossary === null) {
      glossary = new Map([
        ['間合い', '自分と相手のあいだの空きマスの数。0 は隣り合っている'],
        ['構え', 'ターン終了のときスタミナが ' + Constants.ReserveThreshold + ' 以上残っていれば Guard +' + Constants.ReserveGuard],
        ['崩し n', '相手のスタミナを n 減らす'],
        ['素直な札', '特性を持たないかわりに、値が ' + Constants.PlainCardBonus + ' 高い札（表の値に込み）'],
        ['初手', 'このターンに最初に出す札'],
        ['温存', 'この札を出したあとも、スタミナが n 以上残る'],
        ['連打', 'このターンに直前に出した札と、同じ属性である'],
        ['連動 X', 'このターンに属性 X（攻撃・防御・スキル・スタンス）のカードをもう出している'],
        ['手薄', 'この札を出したあと、手札が残り ' + Constants.ThinHandMax + ' 枚以下'],
        ['重撃', '威力 +' + Constants.HeavyBlowPower + '。そのかわり次のターン開始のスタミナ回復が ' + Constants.HeavyBlowRecoveryPenalty + ' 減る'],
        ['転換', 'この札で得る Guard の半分（切り上げ）を威力に足す'],
        ['追撃', 'このターンに次に出す攻撃の威力 +' + Constants.FollowUpPower],
        ['移動後', 'このターンに自分がもう動いている'],
        ['永続', '消えずに残る。スタンスの効果。重ねると重ねた数のぶん効く'],
        ['詰める / 離れる', '自分が動いて、相手との間合いを縮める / 広げる'],
        ['押す / 引く', '相手を動かして、相手との間合いを広げる / 縮める'],
        ['死力', '札を出す前のスタミナが ' + Constants.DesperateThreshold + ' 以下'],
        ['締め', 'このターンに ' + Constants.FinisherPlayNumber + ' 枚目以降に出す札'],
        ['崩し後', '相手のスタミナが ' + Constants.BrokenBelow + ' 未満'],
      ]);
    }
    return glossary;
  }

  // ---- §6.3 ランプ ----------------------------------------------------------------------

  /**
   * §6.3: the lamp of a card in the hand, and the lamp for each enemy slot. A card aimed at one
   * enemy lights when any enemy it reaches satisfies the trait (decision 6), and each slot
   * carries that enemy's own verdict so the View can re-light it for the framed one. A card
   * aimed at every enemy in reach reads the nearest of them, and a card aimed at nobody the
   * nearest standing enemy — the core's own choice (TurnLoop.Preview). A card with no trait,
   * or not in the hand, is never lit. The verdicts are the core's (Preview's TraitHolds and
   * InReach); nothing is judged here.
   *
   * Returns { ok, value }: ok is the C# return value (the lamp), value the out perEnemy list.
   */
  static Lamps(state, instanceId) {
    const perEnemy = [];
    if (state == null) throw argNull('state');
    const card = CardFaceText.Find(state, instanceId);
    if (card == null || card.Def.AllTraits.length === 0) return { ok: false, value: perEnemy };
    const def = card.Def;

    if (CardFaceText.Directed(def) && def.Targets === TargetKind.One) {
      let any = false;
      const living = new Set(state.Living);
      for (let i = 0; i < state.Enemies.length; i++) {
        let lit = false;
        if (living.has(i)) {
          const preview = TurnLoop.Preview(state, instanceId, i);
          lit = preview != null && preview.InReach && preview.TraitHolds;
        }
        perEnemy.push(lit);
        any = any || lit;
      }
      return { ok: any, value: perEnemy };
    }

    const read = TurnLoop.Preview(state, instanceId);
    const holds = read != null && read.TraitHolds && (!CardFaceText.Directed(def) || read.InReach);
    return { ok: holds, value: perEnemy };
  }

  /**
   * battle-visual-v1 §4.5 HP の減る分 (#507): for each enemy slot, the HP card `instanceId` would
   * take off that enemy if it were played on it now. The number is the core's (Preview's Damage:
   * every blow past the Guard, and the wall damage of a push the enemy cannot take, decision A of
   * 2026-10-17), so a Guard card that pushes, such as 盾押し, has a stripe too although it does not
   * attack. The preview keeps the enemy standing, so the number is cut to the HP the enemy has. 0
   * for a slot out of reach or fallen. Empty for a card aimed at nobody, a card not in the hand,
   * and a card aimed at every enemy in reach — the core previews that one on the nearest alone,
   * and no card in the catalogue has it yet.
   */
  static HpLoss(state, instanceId) {
    const loss = [];
    if (state == null) throw argNull('state');
    const card = CardFaceText.Find(state, instanceId);
    if (card == null || !CardFaceText.Directed(card.Def) || card.Def.Targets !== TargetKind.One) return loss;

    const living = new Set(state.Living);
    for (let i = 0; i < state.Enemies.length; i++) {
      let hp = 0;
      if (living.has(i)) {
        const preview = TurnLoop.Preview(state, instanceId, i);
        if (preview != null && preview.InReach) hp = Math.min(preview.Damage, state.Enemies[i].Body.Hp);
      }
      loss.push(hp);
    }
    return loss;
  }

  /** private */
  static Find(state, instanceId) {
    for (const card of state.Hand) {
      if (card.InstanceId === instanceId) return card;
    }
    return null;
  }

  // ---- words ------------------------------------------------------------------------------

  /** private */
  static Directed(def) {
    return EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets);
  }

  /** private */
  static IsStance(def) {
    return hasFlag(def.Attribute, BattleAttribute.Stance);
  }

  /** battle-visual-v1 §2.2: the four attributes as the screen names them. */
  static AttributeWord(attribute) {
    if (hasFlag(attribute, BattleAttribute.Attack)) return '攻撃';
    if (hasFlag(attribute, BattleAttribute.Guard)) return '防御';
    if (hasFlag(attribute, BattleAttribute.Skill)) return 'スキル';
    if (hasFlag(attribute, BattleAttribute.Stance)) return 'スタンス';
    return '';
  }

  /** private */
  static OmenWord(kind) {
    return kind != null ? CoreText.OmenKindWord(kind) : '';
  }

  /** private */
  static WatchWord(trait) {
    return trait.Watch != null ? EnumTokens.ToLabel(trait.Watch) : '';
  }
}

/** §6.1: at most two value columns on a face. */
CardFaceText.MaxValues = 2;

/** §6.1: at most three rows under the values (four for a stance card, whose rows start where the values would). */
CardFaceText.MaxTextLines = 3;

CardFaceText.MaxLastingLines = 4;

/** §6.1 素直な札の特性の箱. */
CardFaceText.PlainTop = '素直な札';

CardFaceText.PlainBottom = '特性なし（+2 込み）';

/** int.CompareTo sign. */
function compareInt(a, b) {
  return a < b ? -1 : a > b ? 1 : 0;
}
