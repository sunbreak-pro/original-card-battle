// Port of unity-port/unity-project-kit/Assets/View/Depiction/Bridge/CoreText.cs
//
// Every string and glyph the screen prints about a core card, action or omen is written here, so
// the View keeps printing what it is given. Pure: BattleCore + Depiction.Script, no DOM.
//
// C# overloads folded into one JS function (same results for every C# call shape):
// - Face(card, preview) / Face(card, preview, outOfReach): outOfReach omitted (undefined) is the
//   two-argument overload (preview != null && !preview.InReach).
// - SystemOf(CardDef) / SystemOf(EnemyActionDef): both read Name, Attributes and Face the same way.
// - IconOf(kind) / IconOf(kind, action): action omitted or null reads the kind alone.
// - Chips(statuses, stanceName = "") / Chips(statuses, IReadOnlyList<string> stanceNames): an array
//   second argument is the list overload, anything else the single-name overload.
// Extension methods of BattleCore are called through their static class (StatusKind.ToLabel() →
// EnumTokens.ToLabel(kind)); Reach.ToText() stays an instance method of the Reach record.
import { hasFlag, ArgumentOutOfRangeException } from '../../shared/cs.js';
import {
  BattleAttribute, Actor, TargetKind, OmenKind, StatusKind, StanceHook, StanceWhen, TraitCondition,
  TraitEffect, CardInstance, EnumTokens,
} from '../../core/battle/Types.js';
import { TurnLoop } from '../../core/battle/TurnLoop.js';
import { EnemyAi } from '../../core/battle/EnemyAi.js';
import {
  RangeSide, UnitSide, CardKind, CardAim, CardFace, OmenFrame, OmenIcon, OmenStatus, StatusGlyph, StrikeSystems,
} from '../depictionScript/DepictionTypes.js';
import { CardFaceText } from './CardFaceText.js';
import { StatusChipText } from './StatusChipText.js';
import { DeckBuilder } from './DeckBuilder.js';

/** new ArgumentOutOfRangeException(paramName, actualValue, message), with .NET's Message text. */
function outOfRange(paramName, actualValue, message) {
  return new ArgumentOutOfRangeException(`${message} (Parameter '${paramName}')\nActual value was ${actualValue}.`);
}

export class CoreText {
  /**
   * v4.3 (#162) on the v4.2 screen: the script still knows two standing slots (near / far),
   * so the gap N is shown on the player's tag as a number and the slot is picked by N — 0〜1
   * stands in the near slot, 2 and more in the far one. The cells on the floor, the lit reach
   * and the aimed cells are #163's, and this mapping goes with them.
   */
  static SideOf(gap) {
    return gap <= 1 ? RangeSide.Near : RangeSide.Far;
  }

  /** The one glyph on the player's tag: N itself ("0", "2"). */
  static GapGlyph(gap) {
    return String(gap);
  }

  static Side(actor) {
    return actor === Actor.Player ? UnitSide.Player : UnitSide.Enemy;
  }

  /** The colour role a card borrows: the one attribute it counts as (§2.1, v4.4). Movement is an effect, not a role of its own. */
  static KindOf(attributes) {
    if (hasFlag(attributes, BattleAttribute.Attack)) return CardKind.Attack;
    if (hasFlag(attributes, BattleAttribute.Guard)) return CardKind.Guard;
    if (hasFlag(attributes, BattleAttribute.Skill)) return CardKind.Skill;
    return hasFlag(attributes, BattleAttribute.Stance) ? CardKind.Stance : CardKind.Skill;
  }

  static KindWord(kind) {
    switch (kind) {
      case CardKind.Attack: return '攻撃';
      case CardKind.Guard: return '防御';
      case CardKind.Move: return 'ムーブ';
      case CardKind.Skill: return '技';
      default: return '構え';
    }
  }

  /**
   * §5.3: the system a card's blow draws, from its name and faces (StrikeSystems.Of). The same for
   * an enemy action (C#'s SystemOf(EnemyActionDef) overload reads the same three members).
   */
  static SystemOf(def) {
    return StrikeSystems.Of(def.Name, hasFlag(def.Attributes, BattleAttribute.Attack),
      hasFlag(def.Attributes, BattleAttribute.Guard) || def.Face.Guard > 0, def.Face.Move !== 0, def.Face.Push !== 0);
  }

  // ---- cards --------------------------------------------------------------------------

  /**
   * The face of a catalog card that is not dealt: the deck screen prints the same face as the
   * hand does (no preview, so the cost is the printed one and no lamp is lit). The face's id is
   * the card's own id.
   */
  static FaceOf(def) {
    return CoreText.Face(new CardInstance(def.Id, def), null, false);
  }

  /**
   * The face of one dealt card. `preview` comes from the core (TurnLoop.Preview), so the lamp is
   * the core's verdict and not a guess made here. `outOfReach` is true when no standing enemy is
   * inside the card's reach (ReachesNobody); it fills CardFace.ReachHint. Omitted, it is the C#
   * two-argument overload: the preview's own enemy only (preview != null && !preview.InReach).
   */
  static Face(card, preview, outOfReach) {
    if (outOfReach === undefined) outOfReach = preview != null && !preview.InReach;
    const def = card.Def;
    const kind = CoreText.KindOf(def.Attribute);
    const single = def.Targets === TargetKind.One;
    const face = new CardFace();
    face.Id = card.InstanceId;
    face.Name = def.Name;
    // The cost a コスト −1 trait leaves right now is the core's (TurnLoop.Preview).
    face.Cost = preview != null ? preview.Cost : def.Cost;
    face.Kind = kind;
    face.Aim = single ? CardAim.Single : CardAim.Self;
    face.Affects = single ? null : UnitSide.Player;
    face.TypeLabel = CoreText.KindWord(kind) + (single ? '・敵単体' : '・自分');
    face.Description = CoreText.Describe(def.Face, def.Attributes);
    face.ValueText = CoreText.ValueOf(def.Face, def.Attributes);
    face.TraitText = CoreText.TraitLines(def);
    face.TraitLit = def.Trait != null && preview != null && preview.TraitHolds;
    // §2.4: the reach is printed on the card; the two-valued RequiredRange cannot hold it,
    // so the core's CanPlay (OutOfReach) is what refuses the card, not the script.
    face.RequiredRange = null;
    face.RequiredRangeGlyph = CoreText.ReachText(def.Attributes, def.Face, def.Targets);
    face.ReachHint = outOfReach ? CoreText.ReachHint(def.Attributes, def.Face, def.Targets) : '';
    // battle-visual-v1 §6 / §7 / §9 (#242): values, rows, trait box, hover line and detail.
    CardFaceText.Fill(face, def);
    return face;
  }

  /**
   * #261: true when the card aims at the opponent and every standing enemy is outside its
   * reach. Each enemy is asked through the core's own verdict (TurnLoop.Preview's InReach),
   * which looks at the gap only — so a card short of stamina alone is not out of reach, and
   * the answer holds in any phase. False for a card not in the hand or with nobody standing.
   */
  static ReachesNobody(state, instanceId) {
    let anyone = false;
    for (const unit of state.Living) {
      const preview = TurnLoop.Preview(state, instanceId, unit);
      if (preview == null) return false;
      if (preview.InReach) return false;
      anyone = true;
    }
    return anyone;
  }

  /**
   * #261: the line beside a card nobody is in reach of ("相手との間合いが 1〜2 のとき使用可能",
   * "相手との間合いが 0 のとき使用可能"), or empty for a card that aims at nobody.
   */
  static ReachHint(attributes, face, targets) {
    const reach = CoreText.ReachText(attributes, face, targets);
    return reach.length === 0 ? '' : '相手との間合いが ' + reach + ' のとき使用可能';
  }

  /** The reach as printed ("0〜1"), or empty for a card that aims at nobody. */
  static ReachText(attributes, face, targets) {
    return EnemyAi.IsOpponentDirected(attributes, face, targets) ? face.ReachOrDefault.ToText() : '';
  }

  /**
   * One short sentence per face, in resolution order. §5: a status on the opponent reads
   * 「<語> を n 付与する」, one on the one playing 「自分に <語> を n 付与する」.
   */
  static Describe(face, attributes) {
    const sentences = [];
    if (hasFlag(attributes, BattleAttribute.Attack)) sentences.push('敵に ' + face.Power + ' ダメージ。');
    if (face.Break > 0) sentences.push('崩し ' + face.Break + '。');
    if (face.Move > 0) sentences.push('前へ ' + face.Move + ' 動く。');
    else if (face.Move < 0) sentences.push('後ろへ ' + (-face.Move) + ' 動く。');
    if (face.Push > 0) sentences.push('敵を ' + face.Push + ' マス押す。');
    else if (face.Push < 0) sentences.push('敵を ' + (-face.Push) + ' マス引く。');
    if (face.Guard > 0) sentences.push('Guard ' + face.Guard + ' を得る。');
    if (face.Heal > 0) sentences.push('HP を ' + face.Heal + ' 回復する。');
    for (const grant of face.StatusList) {
      sentences.push((grant.OnSelf ? '自分に' : '') + EnumTokens.ToLabel(grant.Kind) + 'を ' + grant.Stacks + ' 付与する。');
    }
    if (face.Draw > 0) sentences.push('カードを ' + face.Draw + ' 枚引く。');
    if (face.StaminaGain > 0) sentences.push('スタミナ +' + face.StaminaGain + '。');
    if (face.Stance != null) sentences.push('構え: ' + CoreText.StanceText(face.Stance) + '。');
    return sentences.join('');
  }

  /** §4: what a stance does while it stands, as one clause ("毎ターン開始に Guard +3"). */
  static StanceText(stance) {
    if (stance == null) return '';
    switch (stance.Hook) {
      case StanceHook.TurnStart:
        return CoreText.Spaced(CoreText.WhenWord(stance, '始まるターンに'), CoreText.Gains(stance.Guard, stance.Recovery, 0));
      case StanceHook.TurnEnd:
        return CoreText.Spaced(CoreText.WhenWord(stance, '終えたターンに'), CoreText.Gains(stance.Guard, 0, stance.NextRecovery));
      case StanceHook.AttackBonus:
        switch (stance.When) {
          case StanceWhen.MovedThisTurn: return '移動したターン、アタック +' + stance.Power;
          case StanceWhen.GapAtMost: return '間合い ' + stance.Threshold + ' の相手へのアタック +' + stance.Power;
          case StanceWhen.TargetHasStatus:
            return (stance.Status != null ? EnumTokens.ToLabel(stance.Status) : '') + '中の敵へのアタック +' + stance.Power;
          default: return 'アタック +' + stance.Power;
        }
      case StanceHook.OnHit: {
        const parts = [];
        if (stance.Stamina > 0) parts.push('スタミナ +' + stance.Stamina);
        if (stance.Guard > 0) parts.push('Guard +' + stance.Guard);
        if (stance.Status != null) parts.push('敵に' + EnumTokens.ToLabel(stance.Status) + ' ' + stance.StatusStacks);
        return '被弾のたび' + (stance.OncePerTurn ? '（ターン 1 回）' : '') + parts.join('、');
      }
      case StanceHook.PushImmune: return '押す / 引くを受けない';
      case StanceHook.BreakOnFoeMove: return '敵が自分で動くたび崩し ' + stance.Break;
      case StanceHook.StatusOnAttack:
        // #253: once a blow, so a face of two blows gives it twice.
        return 'アタックのたび敵に' + (stance.Status != null ? EnumTokens.ToLabel(stance.Status) : '') + ' ' + stance.StatusStacks;
      case StanceHook.CostDiscount:
        // §4 コストの割引 (鉄壁の構え, #333), in the attribute words the deck screen uses.
        return '毎ターン 1 回、' + (stance.Attribute != null ? DeckBuilder.AttributeWords(stance.Attribute) : '') + 'のカードのコスト −1';
      default: throw outOfRange('stance', stance.Hook, 'No clause for the stance hook ' + stance.Hook);
    }
  }

  /** private */
  static WhenWord(stance, tail) {
    switch (stance.When) {
      case StanceWhen.GapAtLeast: return '間合い ' + stance.Threshold + ' 以上で' + tail;
      case StanceWhen.GapAtMost: return '間合い ' + stance.Threshold + ' 以下で' + tail;
      case StanceWhen.OmenAttack: return '予兆が攻撃のターン開始に';
      // C# as written: a TurnEnd stance with When Always also reads 毎ターン開始に (no card has one).
      default: return '毎ターン開始に';
    }
  }

  /** private. Japanese then a Latin word ("Guard") get a half-width space between them (rules: 和欧間). */
  static Spaced(head, tail) {
    const latin = tail.length > 0 && tail.charCodeAt(0) < 0x80;
    return latin ? head + ' ' + tail : head + tail;
  }

  /** private */
  static Gains(guard, recovery, nextRecovery) {
    const parts = [];
    if (guard > 0) parts.push('Guard +' + guard);
    if (recovery > 0) parts.push('回復 +' + recovery);
    if (nextRecovery > 0) parts.push('次の回復 +' + nextRecovery);
    return parts.join('、');
  }

  /**
   * The number on a face: an attack's power over all its blows as one sum (#248: "12", never
   * "6×2"), else its Guard, else its heal.
   */
  static ValueOf(face, attributes) {
    if (hasFlag(attributes, BattleAttribute.Attack)) return String(CoreText.FacePower(face));
    if (face.Guard > 0) return String(face.Guard);
    return face.Heal > 0 ? String(face.Heal) : '';
  }

  /** The attack face alone, every blow summed: power × hits. */
  static FacePower(face) {
    return face.Power * Math.max(1, face.Hits);
  }

  /** Every trait of the card on one lamp line; 背水の陣's two read "間合い2以上 +5 ／ 死力 +3". */
  static TraitLines(def) {
    const lines = [];
    for (const trait of def.AllTraits) lines.push(CoreText.TraitLine(trait));
    return lines.join(' ／ ');
  }

  /** The trait as printed on the lamp line: condition, then effect ("間合い0 +5", "残4 Guard+3"). */
  static TraitLine(trait) {
    if (trait == null) return '';
    const effect = CoreText.EffectWord(trait);
    const condition = CoreText.ConditionWord(trait);
    return condition.length === 0 ? effect : condition + ' ' + effect;
  }

  /** The condition half of TraitLine ("間合い0", "残4", "予兆攻撃"). */
  static ConditionWord(trait) {
    switch (trait.Condition) {
      case TraitCondition.GapAtMost:
        return trait.Threshold === 0 ? '間合い0' : '間合い' + trait.Threshold + '以下';
      case TraitCondition.GapAtLeast:
        return '間合い' + trait.Threshold + '以上';
      case TraitCondition.Unguarded: return '無防備';
      case TraitCondition.Reserve: return '残' + trait.Threshold;
      case TraitCondition.Combo: return '連動' + CoreText.AttributeWord(trait.Attribute);
      case TraitCondition.Moved: return '移動後';
      case TraitCondition.OmenIs: return '予兆' + (trait.Omen != null ? CoreText.OmenKindWord(trait.Omen) : '');
      case TraitCondition.Desperate: return '死力';
      case TraitCondition.FirstPlay: return '初手';
      case TraitCondition.Finisher: return '締め';
      case TraitCondition.Broken: return '崩し後';
      case TraitCondition.Chain: return '連打';
      case TraitCondition.Thin: return '手薄';
      case TraitCondition.FoeHas: return '相手' + (trait.Watch != null ? EnumTokens.ToLabel(trait.Watch) : '');
      case TraitCondition.SelfHas: return '自分' + (trait.Watch != null ? EnumTokens.ToLabel(trait.Watch) : '');
      default: throw outOfRange('trait', trait.Condition, 'No lamp word for the trait condition ' + trait.Condition);
    }
  }

  /** The effect half of TraitLine ("+5", "Guard+3", "重撃"). */
  static EffectWord(trait) {
    switch (trait.Effect) {
      case TraitEffect.PowerBonus: return '+' + trait.Amount;
      case TraitEffect.GuardBonus: return 'Guard+' + trait.Amount;
      case TraitEffect.NextTurnRecovery: return '回復+' + trait.Amount;
      case TraitEffect.HeavyBlow: return '重撃';
      case TraitEffect.StaminaGain: return 'スタミナ+' + trait.Amount;
      case TraitEffect.Draw: return 'ドロー+' + trait.Amount;
      case TraitEffect.Status:
        return trait.Grant == null ? '' : (trait.Grant.OnSelf ? '自分' : '') + EnumTokens.ToLabel(trait.Grant.Kind) + '+' + trait.Grant.Stacks;
      case TraitEffect.CostDown: return 'コスト-' + trait.Amount;
      case TraitEffect.Convert: return '転換';
      case TraitEffect.FollowUp: return '追撃';
      case TraitEffect.BreakBonus: return '崩し+' + trait.Amount;
      default: throw outOfRange('trait', trait.Effect, 'No lamp word for the trait effect ' + trait.Effect);
    }
  }

  /** private */
  static AttributeWord(attribute) {
    if (hasFlag(attribute, BattleAttribute.Attack)) return '(攻)';
    if (hasFlag(attribute, BattleAttribute.Guard)) return '(防)';
    if (hasFlag(attribute, BattleAttribute.Skill)) return '(技)';
    if (hasFlag(attribute, BattleAttribute.Stance)) return '(構)';
    return '';
  }

  // ---- the boss words that stop a move (#335) -------------------------------------------

  /**
   * The line a move that did not happen shows over the one who could not move (#335):
   * 呪縛 stops every move of the holder's own, 鉤爪 catches a move back (enemy_roster_v4 §4.1 /
   * §5.1). Empty for any other cause; 鈍足 has no line of its own yet.
   */
  static MoveBlockedLine(by) {
    switch (by) {
      case StatusKind.Binding: return EnumTokens.ToLabel(by) + 'で動けない';
      case StatusKind.Hook: return EnumTokens.ToLabel(by) + 'で下がれない';
      // Empty is right here: no other word stops a move with a line of its own (鈍足 shortens it).
      default: return '';
    }
  }

  /** Why a card of movement only goes back to the hand while the player is bound (#335, roster §4.1 呪縛). */
  static BoundRefusal(cardName) {
    return '「' + cardName + '」は移動が中心の札です。' + EnumTokens.ToLabel(StatusKind.Binding) + 'が付いている間は出せません';
  }

  // ---- omen ---------------------------------------------------------------------------

  /**
   * The omen's kind as a word: the words of battle-visual-v1 4.4, which #243 brought the
   * roster to as well (防御 / 移動). BattleCore's EnemyAi.ToText reads the same words (#357).
   */
  static OmenKindWord(kind) {
    switch (kind) {
      case OmenKind.Attack: return '攻撃';
      case OmenKind.Guard: return '防御';
      case OmenKind.Move: return '移動';
      case OmenKind.Skill: return '技';
      case OmenKind.Stance: return '構え';
      case OmenKind.Rest: return '休み';
      default: throw outOfRange('kind', kind, 'No word for the omen kind ' + kind);
    }
  }

  /**
   * battle-visual-v1 §4.4 (決定 19, #455): a 技 whose face puts a status on the opponent
   * (BattleCore's Face.GivesFoeStatus) wears the stamp (状態付与); any other 技 the star. Every
   * other kind is the kind's own icon (#349). An attack that also puts a status keeps the sword,
   * since its kind is 攻撃. Without `action` (C#'s IconOf(OmenKind) overload) a 技 wears the star.
   */
  static IconOf(kind, action = null) {
    if (kind === OmenKind.Skill && action != null && action.Face.GivesFoeStatus) return OmenIcon.Seal;
    switch (kind) {
      case OmenKind.Attack: return OmenIcon.Attack;
      case OmenKind.Guard: return OmenIcon.Guard;
      case OmenKind.Skill: return OmenIcon.Status;
      case OmenKind.Move: return OmenIcon.Move;
      case OmenKind.Stance: return OmenIcon.Stance;
      case OmenKind.Rest: return OmenIcon.Rest;
      default: return OmenIcon.None;
    }
  }

  /**
   * §4.4 状態付与 (#455): the statuses a Seal omen puts on the player, in the order the action
   * lists them, at most OmenStatusMax. Empty for any other omen.
   */
  static OmenStatusesOf(kind, action) {
    const statuses = [];
    if (CoreText.IconOf(kind, action) !== OmenIcon.Seal) return statuses;
    for (const grant of action.Face.StatusList) {
      if (grant.OnSelf) continue;
      if (statuses.length >= CoreText.OmenStatusMax) break;
      const status = new OmenStatus();
      status.Label = EnumTokens.ToLabel(grant.Kind);
      status.Glyph = CoreText.GlyphOf(grant.Kind);
      statuses.push(status);
    }
    return statuses;
  }

  /**
   * The 付録 A icon of a status an enemy puts on the player. The words 付録 A draws no icon for (the
   * boss's 呪縛 and 瘴気纏い, 鉤爪, 根張り...) are None: the badge writes their name instead.
   */
  static GlyphOf(kind) {
    switch (kind) {
      case StatusKind.Bleed: return StatusGlyph.Bleed;
      case StatusKind.Fragile: return StatusGlyph.Fragile;
      case StatusKind.Slow: return StatusGlyph.Slow;
      case StatusKind.Intimidate: return StatusGlyph.Intimidate;
      case StatusKind.Fatigue: return StatusGlyph.Fatigue;
      default: return StatusGlyph.None;
    }
  }

  /**
   * The number beside the omen's icon (#349): attack and guard only. An attack reads the
   * core's `preview` when there is one, else its face (power × hits); a guard reads the face's
   * Guard. A move with a Guard face (踏み込み) shows none, since its kind is 移動.
   */
  static OmenValueOf(kind, action, preview) {
    if (action == null) return '';
    switch (kind) {
      case OmenKind.Attack:
        return preview != null ? String(preview.RawPower) : String(CoreText.FacePower(action.Face));
      case OmenKind.Guard:
        return action.Face.Guard > 0 ? String(action.Face.Guard) : '';
      default:
        return '';
    }
  }

  /**
   * §6: 種別 (icon and word) + 狙うマス, plus one number for attack and guard (#349). Until the
   * floor shows the aimed cells (#242) the reach goes where the one-character side used to be
   * ("1〜2").
   *
   * For an attack the number is the core's (#248, TurnLoop.PreviewOmen): the whole action's
   * power before the player's Guard — every blow, the wall, and the trait as the board stands
   * now — and what it would be if it landed when it does not reach. Without a `preview` it is the
   * face alone (power × hits). A guard shows the face's Guard; any other kind shows no number.
   */
  static OmenOf(omen, enemy, preview = null) {
    if (omen == null) {
      const hidden = new OmenFrame();
      hidden.Visible = false;
      return hidden;
    }

    const kind = omen.Label.Kind;
    // A rest has no entry in the table (EnemyAi.RestActionId): action stays null, no number.
    const action = tryGetAction(enemy.Actions, omen.ActionId);
    const frame = new OmenFrame();
    frame.Visible = true;
    frame.Icon = CoreText.IconOf(kind, action);
    frame.KindLabel = CoreText.OmenKindWord(kind);
    frame.SideGlyph = omen.Label.Reach != null ? omen.Label.Reach.ToText() : '';
    frame.ValueText = CoreText.OmenValueOf(kind, action, preview);
    frame.Statuses = CoreText.OmenStatusesOf(kind, action);
    return frame;
  }

  // ---- units --------------------------------------------------------------------------

  /**
   * The chips under a gauge (v4.4): the stances held first, one chip per stance, then the status
   * words in their fixed order. The same stance twice is one chip with a count of 2, since it
   * works twice (StanceChips). A string second argument (or none) is C#'s single-name overload:
   * the stance in the slot first (§4, #188; it has no stacks to count).
   */
  static Chips(statuses, stanceNames = '') {
    if (!Array.isArray(stanceNames)) {
      const stanceName = stanceNames;
      return StatusChipText.Chips(statuses, stanceName == null || stanceName === '' ? [] : [stanceName]);
    }
    return StatusChipText.Chips(statuses, stanceNames);
  }

  /**
   * One chip per distinct stance name in the order first held; a stance held more than once
   * shows its count in Stacks, a single one shows none. The panel text is StatusChipText's (#242).
   */
  static StanceChips(stanceNames) {
    return StatusChipText.StanceChips(stanceNames);
  }

  /**
   * #334 (roster §6.4 root_st): the line shown when a permanent effect stops for a turn, and the
   * one shown when it works again. The stance keeps its chip the while; only these lines say so.
   */
  static StanceStoppedLine(stanceName) { return '構え停止・' + stanceName; }

  /** #334: see StanceStoppedLine. */
  static StanceResumedLine(stanceName) { return '構え復帰・' + stanceName; }
}

/** battle-visual-v1 §4.4: a 状態付与 omen shows at most two of the statuses it puts on. */
CoreText.OmenStatusMax = 2;

/** IReadOnlyDictionary<string, EnemyActionDef>.TryGetValue: the action, or null (C#'s out default). */
function tryGetAction(actions, id) {
  if (actions == null) return null;
  if (typeof actions.get === 'function') return actions.has(id) ? actions.get(id) : null;
  return Object.prototype.hasOwnProperty.call(actions, id) ? actions[id] : null;
}
