// Port of unity-project-kit/Assets/View/Depiction/Script/Effects.cs
// The depiction's effects by id: how long each one plays, whether it is switched on, and a
// record of what actually played for the measurement of #78. Pure C#, like the rest of Script/:
// the View asks these types how long to animate and whether to animate at all, so `dotnet test`
// holds the same numbers the screen plays.
//
// Every effect can be switched off on its own (#75-#77). Off means "settle at once": the View sets
// the end state immediately and waits 0 ms, so a switched-off effect never holds the flow up.
//
// Port notes:
// - Every C# `float` is kept as a single: lengths are stored through Math.fround and each float
//   operation (TotalMs, the plan's sums, the draw's start times) is rounded with Math.fround as
//   .NET's single-precision arithmetic does. All the written lengths are small integers, so the
//   values are the plain sums.
// - `out` parameters follow the port's convention: EffectSwitches.WithOff returns
//   `{ ok: switches, value: unknown }`, also given as `switches` and `unknown`.
// - EffectTrace.Entry (a nested C# class) is exported both as `Entry` and as `EffectTrace.Entry`.
// - EffectTrace.ToCsv writes numbers as .NET's custom format strings do ("0.000", "0"): the value is
//   first cut to 15 significant digits for a double (7 for a float) with ties to even, then rounded
//   half away from zero to the places the format asks for; a negative value (or -0) keeps its "-".
// - `string.Trim()` is C#'s (char.IsWhiteSpace): it trims U+0085 and not U+FEFF, unlike JS trim().
import {
  ArgumentException,
  ArgumentNullException,
  ArgumentOutOfRangeException,
  KeyNotFoundException,
  defineEnum,
  enumValues,
} from '../../shared/cs.js';
import { Cue, CueKind, DepictionEventKind, UnitSide } from './DepictionTypes.js';
import { BattleSpeed } from './BattleSpeed.js';

const f32 = Math.fround;

/** new ArgumentNullException(nameof(x)) with .NET's message. */
function argNull(name) {
  const e = new ArgumentNullException(`Value cannot be null. (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

/** new ArgumentOutOfRangeException(nameof(x)) with .NET's message. */
function argRange(name) {
  const e = new ArgumentOutOfRangeException(`Specified argument was out of the range of valid values. (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

/** new ArgumentOutOfRangeException(paramName, actualValue, message) with .NET's message. */
function argRangeValue(name, value, message) {
  const e = new ArgumentOutOfRangeException(`${message} (Parameter '${name}')\nActual value was ${value}.`);
  e.ParamName = name;
  e.ActualValue = value;
  return e;
}

/** new ArgumentException(message, paramName) with .NET's message. */
function argBad(message, name) {
  const e = new ArgumentException(`${message} (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

/** C# string.Trim(): the characters char.IsWhiteSpace answers true for. */
const CS_WHITE = '\\t-\\r \\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000';
const CS_TRIM = new RegExp(`^[${CS_WHITE}]+|[${CS_WHITE}]+$`, 'g');
function csTrim(s) {
  return s.replace(CS_TRIM, '');
}

/** Increments a string of decimal digits by one in its last place; returns { digits, carry }. */
function incrementDigits(digits) {
  const a = digits.split('');
  let i = a.length - 1;
  while (i >= 0) {
    if (a[i] === '9') {
      a[i] = '0';
      i--;
    } else {
      a[i] = String.fromCharCode(a[i].charCodeAt(0) + 1);
      return { digits: a.join(''), carry: false };
    }
  }
  return { digits: '1' + a.join(''), carry: true };
}

/**
 * x.ToString("0" / "0.000", CultureInfo.InvariantCulture) for a double (precision 15) or a float
 * (precision 7): .NET writes the value to that many significant digits (Dragon4, ties to even),
 * then rounds the digits half away from zero to `decimals` places.
 */
function netCustomFixed(x, decimals, precision) {
  if (Number.isNaN(x)) return 'NaN';
  if (x === Infinity) return 'Infinity';
  if (x === -Infinity) return '-Infinity';
  const negative = x < 0 || Object.is(x, -0);
  const a = Math.abs(x);

  // The exact digits (D) and the place of the point (value = 0.D × 10^pointPos).
  let digits;
  let pointPos;
  if (a === 0) {
    digits = '0';
    pointPos = 1;
  } else {
    const s = a.toPrecision(100);
    const [mant, expText] = s.split('e');
    if (expText !== undefined) {
      digits = mant.replace('.', '');
      pointPos = 1 + parseInt(expText, 10);
    } else {
      const [intPart, fracPart = ''] = mant.split('.');
      if (intPart !== '0') {
        digits = intPart + fracPart;
        pointPos = intPart.length;
      } else {
        let z = 0;
        while (z < fracPart.length && fracPart[z] === '0') z++;
        digits = fracPart.slice(z);
        pointPos = -z;
      }
    }
  }

  // Cut to `precision` significant digits, ties to even.
  let head = digits.slice(0, precision).padEnd(precision, '0');
  const tail = digits.slice(precision);
  if (tail.length > 0) {
    const firstOut = tail[0];
    const restNonZero = /[1-9]/.test(tail.slice(1));
    const lastOdd = (head.charCodeAt(head.length - 1) - 48) % 2 === 1;
    if (firstOut > '5' || (firstOut === '5' && (restNonZero || lastOdd))) {
      const r = incrementDigits(head);
      head = r.carry ? r.digits.slice(0, precision) : r.digits;
      if (r.carry) pointPos += 1;
    }
  }

  // Round half away from zero to `decimals` places (RoundNumber: digit >= '5' rounds up).
  if (pointPos < 1) {
    head = '0'.repeat(1 - pointPos) + head;
    pointPos = 1;
  }
  const idx = pointPos + decimals;
  let kept = head.slice(0, idx).padEnd(idx, '0');
  const roundDigit = idx < head.length ? head[idx] : '0';
  if (roundDigit >= '5') {
    const r = incrementDigits(kept);
    kept = r.digits;
    if (r.carry) pointPos += 1;
  }
  const intPart = kept.slice(0, pointPos).replace(/^0+(?=\d)/, '');
  const frac = kept.slice(pointPos);
  return (negative ? '-' : '') + intPart + (decimals > 0 ? '.' + frac : '');
}

/**
 * One effect the screen plays (battle_ui_ux_v2 §3.2 / §5). The ids are stable names #78's
 * measurement reports under; the lengths live in EffectCatalog.
 */
export const EffectId = defineEnum([
  // ---- Card motion (#75, battle_ui_ux_v2 §3.2 / §5.2) ----

  /** One card flies from the draw pile into the fan. Cards leave one stagger apart. */
  'CardDraw',

  /** The other cards slide to their new fan places (HandFan.Place) after the hand changed. */
  'HandFan',

  /** The card under the pointer rises 28 px and grows to 1.05. */
  'CardHover',

  /** A picked-up card stands upright and grows to 1.08. */
  'CardGrab',

  /** The receiver fades in over the target while a single-target card is held. */
  'ReceiverShow',

  /** The throw line fades in above the hand while a self card is held. */
  'ThrowLineShow',

  /** A held card over the receiver is drawn toward the dish's middle. */
  'ReceiverSnap',

  /** The released card is swallowed by the dish (or tossed upward for a self card). */
  'CardRelease',

  /** The played card's ghost settles on the discard pile, beside the event's beats. */
  'CardToDiscard',

  /** A played stance card's ghost flies to the exile pile, beside the event's beats (#455). */
  'CardToExile',

  /** The draw pile ran out mid-draw: the discard pile slides onto it before the cards fly (#455). */
  'PileRebuild',

  /** A card released elsewhere slides back to its fan place. */
  'CardReturn',

  /** A card refused by a rule shakes once it is home. */
  'RefusalShake',

  /** Turn end: the whole hand flies to the discard pile, one stagger apart. */
  'HandDiscard',

  /** A card the stamina cannot pay for darkens (the reason line is the source's text). */
  'UnpayableDim',

  // ---- Attack and defence (#76, battle_ui_ux_v2 §5.5 / §5.6) ----

  /** The player steps toward the enemy before the blow. */
  'AttackLunge',

  /** The enemy steps toward the player before its blow (#288); it draws back with EnemyReturn. */
  'EnemyLunge',

  /** The blow's shape (Cue.System: arc, line, band, ripple) across the target. */
  'StrikeShape',

  /** Both sides freeze for a moment after a normal blow (intensity 1-2). */
  'HitStop',

  /** The longer freeze after a strong blow (intensity 3-4). */
  'HitStopStrong',

  /** The struck figure blinks white or red. */
  'HitFlash',

  /** The struck figure reels back and shakes. */
  'TargetRecoil',

  /** The damage number rises from the chest. */
  'DamageNumber',

  /** The HP bar shrinks to its new width. */
  'HpDrain',

  /** The grey band left behind follows the bar down, late. */
  'HpTrail',

  /** A strong blow shakes the arena. */
  'ScreenShake',

  /** The player steps back after a blow that landed. */
  'AttackReturn',

  /** The enemy's move into its action, shaped by Cue.System (sweep, thrust, shove, guard, step). */
  'EnemyMotion',

  /** The spent omen fades once its blow has landed on the player. */
  'OmenSpend',

  /** The enemy steps back to where it stood. */
  'EnemyReturn',

  /** The shield snaps up and shows how much Guard stopped. */
  'GuardBlock',

  /** "Guard −n" rises in the Guard colour. */
  'GuardNumber',

  /** The Guard badge cracks when a blow takes it to 0. */
  'GuardBreak',

  /** The Guard badge pops with the Guard just gained. */
  'GuardGain',

  // ---- The other beats (lengths as the View plays them; #77 works them out) ----

  /** The omen badge drops in over the enemy. */
  'OmenShow',

  /** A trait's condition held: the burst and its label. */
  'TraitFire',

  /** The stamina pips change. */
  'StaminaChange',

  /** 構え: the shield and "+3" at turn end. */
  'StanceCue',

  /** Without a floor, the player moves to the stand point N maps to. */
  'RangeSwitch',

  /** Without a floor, the 近 / 遠 tag at the player's feet flips over once the move has landed (#495). */
  'RangeTagFlip',

  /** On the floor, the side that moves walks one cell after another, 160 ms a cell (battle_ui_ux_v2 §5.7 順 1, #586). */
  'FloorMove',

  /** On the floor, the gap number is counted again on arrival: the old one leaves upward, the new one comes in from below (§5.7 順 2, #586). */
  'GapRecount',

  /** A bonus the enemy did not get (or a whiffed blow) is struck off the omen. */
  'SideBonusMiss',

  // ---- The effect grammar (#57, battle_ui_ux_v2 §5.3 / §5.9) ----

  /** The shape of a beat's system that is not a blow (Cue.Fx): 盾 ring, 型 crest, 気 mark. */
  'GrammarMark',

  /** A trait fired: a short cue layered on the blow, the tier unchanged (§5.9 順 1, J2). */
  'TraitAccent',

  /** 息 on a heal: the chest rises and settles once beside the HP beat (§5.3). */
  'BreathHeave',

  /** 型 (#501): the body's light fades in with the first stance and out when the list empties (§5.11 順 2). */
  'StanceGlow',

  // ---- Status and turn (#77, battle_ui_ux_v2 §5.1 / §5.10 / §5.13) ----

  /** A status word appears: its chip pops in. */
  'StatusApply',

  /** A chip's stacks grow. */
  'StatusStack',

  /** A chip loses a stack at its holder's turn start (or when used). */
  'StatusTick',

  /** A chip's last stack goes and the chip fades away. */
  'StatusVanish',

  /** At the player's turn start, the standing omen blinks once. */
  'OmenBlink',

  /** The omen flares the moment the enemy carries it out. */
  'OmenExecute',

  /** "押し出し" rises on the figure the other side moved. */
  'PushMark',

  /** "あなたの番": the banner that opens the player's turn. */
  'TurnBanner',

  /** "敵の番": the banner that hands the turn to the enemy. */
  'EnemyTurnBanner',

  /** The recovered pips light one by one. */
  'StaminaRecover',

  /** 構え's 息 (#501): pips 1-3 glow at turn end before the shield and "+3" (§5.13 順 1). */
  'ReservePips',

  /** The one card at the end: 勝ち or 負け. */
  'ResultCard',

  /** A fallen unit tilts, sinks, darkens and is gone; its cell is left empty (#288). */
  'Defeat',
]);

/**
 * How one effect plays. Ms is the length of one item; an effect that plays per card starts each
 * next item StaggerMs later. A blocking effect is waited for by the event it belongs to; the others
 * run beside the flow and never lengthen an event.
 */
export class EffectSpec {
  constructor(id, ms, staggerMs, blocking, source) {
    this._id = id;
    this._ms = f32(ms);
    this._staggerMs = f32(staggerMs);
    this._blocking = blocking;
    this._source = source;
  }

  get Id() { return this._id; }
  /** float */
  get Ms() { return this._ms; }
  /** float */
  get StaggerMs() { return this._staggerMs; }
  get Blocking() { return this._blocking; }

  /** Where the length is written: "battle_ui_ux_v2 §3.2", or the issue that chose it. */
  get Source() { return this._source; }

  /** Start to finish for `count` items: the last one starts after count − 1 staggers. */
  TotalMs(count) {
    if (count <= 0) return 0;
    return f32(this.Ms + f32(this.StaggerMs * f32(count - 1)));
  }
}

/** The written lengths, built once on first use (C#: a static readonly field). */
let _specs = null;

function specs() {
  if (_specs == null) _specs = EffectCatalog_Build();
  return _specs;
}

/** (C# private static) EffectCatalog.Build */
function EffectCatalog_Build() {
  const list = [
    new EffectSpec(EffectId.CardDraw, 260, 60, true, 'battle_ui_ux_v2 §3.2 ドロー'),
    new EffectSpec(EffectId.HandFan, 120, 0, false, 'battle_ui_ux_v2 §3.2 手札の扇'),
    new EffectSpec(EffectId.CardHover, 90, 0, false, 'battle_ui_ux_v2 §3.2 ホバー'),
    new EffectSpec(EffectId.CardGrab, 90, 0, false, 'battle_ui_ux_v2 §3.2 掴む'),
    new EffectSpec(EffectId.ReceiverShow, 150, 0, false, 'battle_ui_ux_v2 §3.2 受け皿が出る'),
    new EffectSpec(EffectId.ThrowLineShow, 150, 0, false, 'battle_ui_ux_v2 §3.2 投げ上げ線が出る'),
    new EffectSpec(EffectId.ReceiverSnap, 80, 0, false, '#75 受け皿への吸い付き'),
    new EffectSpec(EffectId.CardRelease, 160, 0, true, 'battle_ui_ux_v2 §3.2 離す（皿の上 / 線より上）'),
    new EffectSpec(EffectId.CardToDiscard, 200, 0, false, '#75 捨て札への移動'),
    new EffectSpec(EffectId.CardToExile, 220, 0, false, 'battle_ui_ux_v2 §5.11 順 4 除外の束へ飛ぶ 220 in（拍 600。面の拍と並行）'),
    new EffectSpec(EffectId.PileRebuild, 200, 0, true, 'battle_ui_ux_v2 §3.14 / §5.1 順 6 山札の作り直し 200（#453 で 300 から詰めた仮の値）'),
    new EffectSpec(EffectId.CardReturn, 200, 0, false, 'battle_ui_ux_v2 §3.2 離す（それ以外）'),
    new EffectSpec(EffectId.RefusalShake, 160, 0, false, '#75 出せない札の揺れ'),
    new EffectSpec(EffectId.HandDiscard, 240, 60, true, 'battle_ui_ux_v2 §3.2 全捨て'),
    new EffectSpec(EffectId.UnpayableDim, 120, 0, false, '#75 出せない札の暗い幕'),

    // #76: the waits are the slice's (the 2.0 s budget is already spent to 1990 ms by 牽制),
    // and what the issue adds runs beside them.
    new EffectSpec(EffectId.AttackLunge, 100, 0, true, '#76 踏み込み（§5.5 順 1 は 180。2.0 s の予算に合わせて 100）'),
    new EffectSpec(EffectId.EnemyLunge, 100, 0, true, '#288 敵の踏み込み（§5.5 は自分 → 敵の左右反転。自分の踏み込みと同じ 100）'),
    new EffectSpec(EffectId.StrikeShape, 120, 0, true, '#76 系統の形（§5.3。長さは系統で変えず 120）'),
    new EffectSpec(EffectId.HitStop, 30, 0, true, '#76 ヒットストップ・通常（強弱 2 段の仮置き）'),
    new EffectSpec(EffectId.HitStopStrong, 90, 0, true, '#76 ヒットストップ・強（強弱 2 段の仮置き）'),
    new EffectSpec(EffectId.HitFlash, 260, 0, false, '#76 被弾の点滅'),
    new EffectSpec(EffectId.TargetRecoil, 260, 0, false, '#76 のけぞり'),
    new EffectSpec(EffectId.DamageNumber, 1000, 0, false, '#76 ダメージ数字の湧き（上昇して消えるまで）'),
    new EffectSpec(EffectId.HpDrain, 300, 0, true, 'battle_ui_ux_v2 §5.5 順 7 HP バー'),
    new EffectSpec(EffectId.HpTrail, 500, 0, false, 'battle_ui_ux_v2 §5.5 順 7 灰の残像'),
    new EffectSpec(EffectId.ScreenShake, 180, 0, false, '#76 画面の揺れ（強だけ）'),
    new EffectSpec(EffectId.AttackReturn, 120, 0, true, '#76 戻り（§5.5 順 8 は 220。予算に合わせて 120）'),
    new EffectSpec(EffectId.EnemyMotion, 220, 0, true, '#76 敵の動き（系統ごとに形を変え、長さは同じ）'),
    new EffectSpec(EffectId.OmenSpend, 200, 0, true, '#76 予兆の消え方'),
    new EffectSpec(EffectId.EnemyReturn, 140, 0, true, '#76 敵の戻り'),
    new EffectSpec(EffectId.GuardBlock, 320, 0, true, 'battle_ui_ux_v2 §5.6 被弾（盾が受ける 240 + 止め 80）'),
    new EffectSpec(EffectId.GuardNumber, 1000, 0, false, '#76 Guard の数字（上昇して消えるまで）'),
    new EffectSpec(EffectId.GuardBreak, 200, 0, false, 'battle_ui_ux_v2 §5.6 順 1 盾バッジが割れる'),
    new EffectSpec(EffectId.GuardGain, 260, 0, true, '#76 盾バッジのポップ'),

    new EffectSpec(EffectId.OmenShow, 240, 0, true, '縦切りの長さ（#77 で詰める）'),
    new EffectSpec(EffectId.TraitFire, 200, 0, true, '#76 特性の合図（光は 260 で並行。待つのは 200）'),
    new EffectSpec(EffectId.StaminaChange, 220, 0, true, '縦切りの長さ（#77 で詰める）'),
    new EffectSpec(EffectId.StanceCue, 460, 0, true, '縦切りの長さ（#77 で詰める）'),
    // #495: the move and the flip are two beats, because on the floor there is no tag to flip.
    new EffectSpec(EffectId.RangeSwitch, 320, 0, true, 'battle_ui_ux_v2 §10.6 移動 320（床の無い画面だけ）'),
    new EffectSpec(EffectId.RangeTagFlip, 150, 0, true, 'battle_ui_ux_v2 §10.6 一字札の裏返し 150（床の無い画面だけ）'),
    // #586: each cell is 160 and the next starts as it ends, so TotalMs(cells) = 160 × cells.
    new EffectSpec(EffectId.FloorMove, 160, 160, true, 'battle_ui_ux_v2 §5.7 順 1 床の上の移動 160 × マス数'),
    new EffectSpec(EffectId.GapRecount, 150, 0, true, 'battle_ui_ux_v2 §5.7 順 2 到着の拍で間合いの数字を数え直す 150'),
    new EffectSpec(EffectId.SideBonusMiss, 260, 0, true, '縦切りの長さ'),

    // #57: both run beside the beat they belong to, so the grammar adds no wait to any event.
    new EffectSpec(EffectId.GrammarMark, 300, 0, false, 'battle_ui_ux_v2 §5.3 盾 / 型 / 気 の形（大きさ 1.0 で 300。型の紋と光はこの後 600 で薄れる。面の拍と並行）'),
    new EffectSpec(EffectId.TraitAccent, 150, 0, false, 'battle_ui_ux_v2 §5.9 順 1 発火の合図 150（一撃に重ね、段は変えない）'),
    new EffectSpec(EffectId.BreathHeave, 400, 0, false, 'battle_ui_ux_v2 §5.3 息 HP 回復の面だけ胸の上下 400（HP の拍と並行。息のほかの形はピップの点灯 StaminaRecover）'),
    // #501: the light itself stays for as long as the stance list holds one; only its fade is timed.
    new EffectSpec(EffectId.StanceGlow, 300, 0, false, 'battle_ui_ux_v2 §5.11 順 2 身体に薄い光 300（型の紋と並行。消えるときも同じ 300）'),

    // #77
    new EffectSpec(EffectId.StatusApply, 200, 0, true, 'battle_ui_ux_v2 §5.10 順 2 チップが膨らんで収まる'),
    new EffectSpec(EffectId.StatusStack, 150, 0, true, 'battle_ui_ux_v2 §5.10 順 3'),
    new EffectSpec(EffectId.StatusTick, 120, 0, true, '#77 スタックの減少（§5.10 順 4 は 150。敵の番の予算に合わせて 120）'),
    new EffectSpec(EffectId.StatusVanish, 150, 0, true, 'battle_ui_ux_v2 §5.10 順 6 薄くなって消える'),
    new EffectSpec(EffectId.OmenBlink, 250, 0, false, 'battle_ui_ux_v2 §5.1 順 8 予兆の札が明滅 1 回'),
    new EffectSpec(EffectId.OmenExecute, 200, 0, false, 'battle_ui_ux_v2 §5.8 実行（札が一瞬塗られる）'),
    new EffectSpec(EffectId.PushMark, 1000, 0, false, 'battle_ui_ux_v2 §5.7 順 6 押し出し（上昇して消えるまで）'),
    new EffectSpec(EffectId.TurnBanner, 300, 0, true, 'battle_ui_ux_v2 §5.1 順 1 ターン開始のバナー'),
    new EffectSpec(EffectId.EnemyTurnBanner, 400, 0, true, 'battle_ui_ux_v2 §5.13 順 5 敵の番のバナー'),
    new EffectSpec(EffectId.StaminaRecover, 80, 80, true, 'battle_ui_ux_v2 §5.1 順 3 回復ピップ 1 個ずつ 80 × n'),
    new EffectSpec(EffectId.ReservePips, 250, 0, true, 'battle_ui_ux_v2 §5.13 順 1 構え ピップ 1〜3 が光る（息）250'),
    new EffectSpec(EffectId.ResultCard, 400, 0, false, 'battle_ui_ux_v2 §5.19 順 5 せり上がり（勝ち / 負けの 1 枚）'),

    // #288: its own beat, so the blow that fells the enemy keeps its 2.0 s.
    new EffectSpec(EffectId.Defeat, 800, 0, true, 'battle_ui_ux_v2 §2.2 の 6 / §5.18 倒れ（800。薄れて消えるまでを含む）'),
  ];
  const map = new Map();
  for (const spec of list) {
    // Dictionary.Add throws on a second spec for the same id.
    if (map.has(spec.Id)) throw new ArgumentException('An item with the same key has already been added. Key: ' + spec.Id);
    map.set(spec.Id, spec);
  }
  return map;
}

/**
 * The nominal length of every effect, written at the default battle speed of 1.25 times. The View
 * hands exactly these to UiTween; another speed stretches them through UiTween.Speed (BattleSpeed).
 */
export const EffectCatalog = {
  /**
   * battle_ui_ux_v2 §5.11 順 4 (#455): a stance card leaves for the exile pile on the beat 600 ms
   * after it is released, once its 型 (順 2, 300 from 300) has shown. (float)
   */
  CardToExileDelayMs: 600,

  /** IReadOnlyCollection<EffectSpec>: every spec, in the order they are written. */
  get All() {
    return [...specs().values()];
  },

  Of(id) {
    const map = specs();
    if (!map.has(id)) {
      throw new KeyNotFoundException('No length is written for effect ' + id + '.');
    }
    return map.get(id);
  },
};

/**
 * Which effects play. Everything is on until switched off. A switched-off effect settles at once:
 * WaitMs answers 0, so nothing that waits on it can stall.
 */
export class EffectSwitches {
  constructor() {
    /** @private readonly HashSet<EffectId> */
    this._off = new Set();
  }

  static AllOn() {
    return new EffectSwitches();
  }

  static AllOff() {
    const switches = new EffectSwitches();
    for (const id of enumValues(EffectId)) switches.Set(id, false);
    return switches;
  }

  /**
   * Switches off the effects named (the Inspector's list). Names that are not effects are handed
   * back in `unknown` so the caller can say so instead of ignoring a typo.
   * C#: `EffectSwitches WithOff(IEnumerable<string> names, out List<string> unknown)`.
   * @returns {{ ok: EffectSwitches, value: string[], switches: EffectSwitches, unknown: string[] }}
   */
  static WithOff(names) {
    const switches = new EffectSwitches();
    const unknown = [];
    const result = () => ({ ok: switches, value: unknown, switches, unknown });
    if (names == null) return result();
    for (const raw of names) {
      const name = csTrim(raw ?? '');
      if (name.length === 0) continue;
      // Names only: Enum.TryParse would also take "7" and switch off whichever effect sits at 7.
      if (EffectId.$order.indexOf(name) >= 0) switches.Set(name, false);
      else unknown.push(name);
    }
    return result();
  }

  IsOn(id) {
    return !this._off.has(id);
  }

  Set(id, on) {
    if (on) this._off.delete(id);
    else this._off.add(id);
  }

  /** How long to animate one item: the catalog length when on, 0 when off. */
  Ms(id) {
    return this.IsOn(id) ? EffectCatalog.Of(id).Ms : 0;
  }

  /** How long to wait between items: the catalog stagger when on, 0 when off. */
  StaggerMs(id) {
    return this.IsOn(id) ? EffectCatalog.Of(id).StaggerMs : 0;
  }

  /** How long the flow waits for `count` items of the effect. */
  WaitMs(id, count = 1) {
    return this.IsOn(id) ? EffectCatalog.Of(id).TotalMs(count) : 0;
  }
}

/** One effect an event waits for, with how many items it plays. */
export class EffectStep {
  /** @param {number|null} [leadMs] float? */
  constructor(id, count, leadMs = null) {
    if (leadMs != null && leadMs < 0) throw argRange('leadMs');
    this._id = id;
    this._count = count;
    this._leadMs = leadMs == null ? null : f32(leadMs);
  }

  get Id() { return this._id; }
  get Count() { return this._count; }

  /**
   * §5.4 (#499): when set, the next step starts this long after this one does, and this one plays
   * on beside it. The flow is held for the lead only (or the whole effect, if that is shorter).
   * Null holds the whole effect. (float?)
   */
  get LeadMs() { return this._leadMs; }
}

/** §5.10: which of the four chip beats a StatusChange cue is, read off its change and what is left. */
export const StatusBeat = {
  Of(cue) {
    if (cue == null) throw argNull('cue');
    if (cue.StacksAfter <= 0) return EffectId.StatusVanish;
    if (cue.Amount > 0) return cue.StacksAfter === cue.Amount ? EffectId.StatusApply : EffectId.StatusStack;
    return EffectId.StatusTick;
  },
};

/**
 * §5.3 強弱, held at two steps for now (#76): intensity 3 and 4 are strong. Since #30 every
 * script writes only Uniform (2, not strong), so beats play the normal step.
 */
export const EffectStrength = {
  /**
   * The one intensity every script writes (§5.3, decided 2026-10-03, #30): the strength stays
   * the same for every beat until each card gets its own animation. Tier 2 is the size-1.0
   * step, so the shapes play at the lengths §5.3 lists. The View still reads 1..4 from the cue.
   * This is the one place the strength is set (#57): the effect grammar's systems never move it,
   * and the four steps of §5.3 come back by replacing this constant when #30 settles them.
   */
  Uniform: 2,

  IsStrong(intensity) {
    return intensity >= 3;
  },
};

/**
 * The blocking effects an event plays, in order, read off its kind and its cues. The View waits
 * for exactly these (#495): DepictionPlayer takes the event's steps from StepsOf and holds the flow
 * at each one for WaitMs, so the tests that hold the event lengths against the 2.0 s budget sum the
 * same waits the screen holds. Effects that run beside the flow (numbers, flashes, the HP trail) are
 * not listed: they never lengthen an event.
 */
export const EffectPlan = {
  /**
   * Who swings a Slash: its Source, or the side opposite its target when a script left the
   * Source out (DepictionPlayer logs that as a script bug and still plays the beat this way).
   */
  AttackerOf(cue) {
    if (cue == null) throw argNull('cue');
    return cue.Source ?? (cue.Target === UnitSide.Enemy ? UnitSide.Player : UnitSide.Enemy);
  },

  /**
   * A RangeSwitch drawn on the floor (battle-visual-v1 §4.2): the source gave the cells after the
   * move, so the figure walks to its cell and the gap number changes with no tag to flip.
   */
  MovesOnFloor(cue) {
    if (cue == null) throw argNull('cue');
    return cue.PlayerCellAfter > 0;
  },

  /**
   * How many cells a move on the floor walks (Cue.CellsMoved, §5.7 順 1). A source that draws the
   * floor always writes it, so a missing count is a script bug and stops here, not a 0 ms walk.
   */
  CellsOf(cue) {
    if (cue == null) throw argNull('cue');
    if (!EffectPlan.MovesOnFloor(cue)) throw argBad('A move without a floor walks no cells.', 'cue');
    if (cue.CellsMoved < 1) {
      throw argRangeValue('cue', cue.CellsMoved, 'A move on the floor must say how many cells it walks (Cue.CellsMoved).');
    }
    return cue.CellsMoved;
  },

  /**
   * §5.4 順 2: the second face (or the move) starts this long after the first face's result beat
   * (the number of a blow appearing) and overlaps the first face's tail. (float)
   */
  NextFaceLeadMs: 80,

  /**
   * §5.4 順 2 / §5.5 順 8 (#499): the cue right after this blow (the Slash that settles the HP, or
   * the Hit of a blow Guard took part of) is a move, so the player does not step back first; the
   * move carries the figure on from where the lunge left it, and it starts NextFaceLeadMs after the
   * number while the HP bar drains beside it. The screen (DepictionPlayer.Attack and the Hit beat)
   * and the plan both ask here, so they leave out the same return and hold the same lead.
   */
  MoveFollows(ev, blow) {
    if (ev == null) throw argNull('ev');
    const index = ev.Cues.indexOf(blow);
    return index >= 0 && index + 1 < ev.Cues.length && ev.Cues[index + 1].Kind === CueKind.RangeSwitch;
  },

  /** How long the flow is held at one step with these switches: the one wait both the screen and the tests use. */
  WaitMs(step, switches) {
    if (step == null) throw argNull('step');
    if (switches == null) throw argNull('switches');
    const whole = switches.WaitMs(step.Id, step.Count);
    return step.LeadMs != null ? Math.min(whole, step.LeadMs) : whole;
  },

  /**
   * battle_ui_ux_v2 §3.14 / §5.1 順 6 (#455): when the discard pile starts to slide onto the draw
   * pile, counted from the start of the draw: one stagger after the last card the old pile held
   * has left (at 0 when the old pile was already empty).
   */
  RebuildStartMs(rebuildAfter, switches) {
    if (switches == null) throw argNull('switches');
    return f32(f32(Math.max(0, rebuildAfter)) * switches.StaggerMs(EffectId.CardDraw));
  },

  /**
   * When card `index` of a draw leaves the draw pile, counted from the start of the draw. The cards
   * keep one stagger apart. In a draw that ran the pile out (`rebuildAfter` ≥ 0, Cue.RebuildAfter)
   * the cards from the new pile wait for the slide, so the last card lands on PileRebuild's wait +
   * CardDraw's wait (#455).
   */
  DrawCardStartMs(index, rebuildAfter, switches) {
    if (switches == null) throw argNull('switches');
    let at = f32(f32(Math.max(0, index)) * switches.StaggerMs(EffectId.CardDraw));
    if (rebuildAfter >= 0 && index >= rebuildAfter) at = f32(at + switches.WaitMs(EffectId.PileRebuild));
    return at;
  },

  /** @returns {EffectStep[]} */
  StepsOf(ev) {
    if (ev == null) throw argNull('ev');
    const steps = [];
    if (ev.Kind === DepictionEventKind.PlayCard) steps.push(new EffectStep(EffectId.CardRelease, 1));
    if (ev.Kind === DepictionEventKind.TurnStart) steps.push(new EffectStep(EffectId.TurnBanner, 1));
    for (const cue of ev.Cues) EffectPlan_AddSteps(steps, ev, cue);
    if (ev.Kind === DepictionEventKind.TurnEnd) steps.push(new EffectStep(EffectId.EnemyTurnBanner, 1));
    // §5.4 順 0 / §5.9 順 1 (#499): a trait that fires before the first face is shown as the card
    // is released, beside the card's flight, so the release holds nothing of its own.
    if (ev.Kind === DepictionEventKind.PlayCard && steps.length > 1 && steps[1].Id === EffectId.TraitFire) {
      steps[0] = new EffectStep(EffectId.CardRelease, 1, 0);
    }
    return steps;
  },

  /**
   * The time the event waits on its effects with these switches. With `speed` (C#'s second
   * overload, #348): the time the screen really waits on the event's effects at this battle speed.
   * For the measurement only: the View never rescales a length, it hands UiTween the written ms and
   * UiTween.Speed (BattleSpeed.TweenSpeed) stretches them by the same factor.
   */
  BlockingMs(ev, switches, speed) {
    if (speed !== undefined) return BattleSpeed.WallMs(EffectPlan.BlockingMs(ev, switches), speed);
    if (switches == null) throw argNull('switches');
    let ms = 0;
    for (const step of EffectPlan.StepsOf(ev)) ms = f32(ms + EffectPlan.WaitMs(step, switches));
    return ms;
  },
};

/** (C# private static) EffectPlan.AddSteps */
function EffectPlan_AddSteps(steps, ev, cue) {
  switch (cue.Kind) {
    // Both hand cues carry how many cards move: the ones drawn, the ones thrown away. No card
    // moving is no beat at all, as on screen (#495).
    case CueKind.DrawHand:
      // §3.14 / §5.1 順 6 (#455): a draw that ran the pile out holds 200 for the discard pile
      // to slide onto it. The slide plays in the middle of the draw, once the old pile is
      // empty (DrawCardStartMs), and the draw is 260 + (n − 1) × 60 + 200, as §5.1 counts it.
      if (cue.Amount > 0 && cue.RebuildAfter >= 0) steps.push(new EffectStep(EffectId.PileRebuild, 1));
      if (cue.Amount > 0) steps.push(new EffectStep(EffectId.CardDraw, cue.Amount));
      break;
    case CueKind.DiscardHand:
      if (cue.Amount > 0) steps.push(new EffectStep(EffectId.HandDiscard, cue.Amount));
      break;
    case CueKind.StaminaChange:
      // A gain lights its pips one by one; a spend pops beside the card's beats.
      if (cue.Amount > 0) steps.push(new EffectStep(EffectId.StaminaRecover, cue.Amount));
      break;
    case CueKind.OmenShow:
      steps.push(new EffectStep(EffectId.OmenShow, 1));
      break;
    case CueKind.TraitFire:
      steps.push(new EffectStep(EffectId.TraitFire, 1));
      break;
    case CueKind.Slash: {
      // A slash with no settled HP is the swing alone: the GuardBlock / Hit cues carry the rest.
      const byPlayer = EffectPlan.AttackerOf(cue) === UnitSide.Player;
      steps.push(new EffectStep(byPlayer ? EffectId.AttackLunge : EffectId.EnemyLunge, 1));
      steps.push(new EffectStep(EffectId.StrikeShape, 1));
      steps.push(new EffectStep(EffectStrength.IsStrong(cue.Intensity) ? EffectId.HitStopStrong : EffectId.HitStop, 1));
      if (cue.HpAfter !== Cue.Unchanged) {
        if (byPlayer && EffectPlan.MoveFollows(ev, cue)) steps.push(new EffectStep(EffectId.HpDrain, 1, EffectPlan.NextFaceLeadMs));
        else {
          steps.push(new EffectStep(EffectId.HpDrain, 1));
          if (byPlayer) steps.push(new EffectStep(EffectId.AttackReturn, 1));
        }
      }
      break;
    }
    case CueKind.GuardGain:
      steps.push(new EffectStep(EffectId.GuardGain, 1));
      break;
    case CueKind.RangeSwitch:
      // §5.7 順 1〜2 (#586): on the floor, 160 a cell, then the gap number is counted again
      // for 150. Without a floor, the move to the slot and the tag's flip (#495).
      if (EffectPlan.MovesOnFloor(cue)) {
        steps.push(new EffectStep(EffectId.FloorMove, EffectPlan.CellsOf(cue)));
        steps.push(new EffectStep(EffectId.GapRecount, 1));
      } else {
        steps.push(new EffectStep(EffectId.RangeSwitch, 1));
        steps.push(new EffectStep(EffectId.RangeTagFlip, 1));
      }
      break;
    case CueKind.StanceCue:
      // §5.13 順 1-2 (#501): the pips 1-3 glow (構え's 息), then the shield and "+3".
      steps.push(new EffectStep(EffectId.ReservePips, 1));
      steps.push(new EffectStep(EffectId.StanceCue, 1));
      break;
    case CueKind.EnemyWindup:
      steps.push(new EffectStep(EffectId.EnemyMotion, 1));
      break;
    case CueKind.SideBonusMiss:
      steps.push(new EffectStep(EffectId.SideBonusMiss, 1));
      break;
    case CueKind.GuardBlock:
      steps.push(new EffectStep(EffectId.GuardBlock, 1));
      break;
    case CueKind.Hit: {
      // The player's blow Guard took part of: a move right after it starts on the number too.
      const movesOn = cue.Target === UnitSide.Enemy && EffectPlan.MoveFollows(ev, cue);
      steps.push(new EffectStep(EffectId.HpDrain, 1, movesOn ? EffectPlan.NextFaceLeadMs : null));
      if (cue.Target === UnitSide.Player) {
        // The blow on the player is the enemy's own: its omen is spent and it draws back.
        steps.push(new EffectStep(EffectId.OmenSpend, 1));
        steps.push(new EffectStep(EffectId.EnemyReturn, 1));
      }
      break;
    }
    case CueKind.StatusChange:
      steps.push(new EffectStep(StatusBeat.Of(cue), 1));
      break;
    case CueKind.HpChange:
      // The number rises beside it; the bar is what is waited for (#188).
      steps.push(new EffectStep(EffectId.HpDrain, 1));
      break;
    case CueKind.Defeat:
      steps.push(new EffectStep(EffectId.Defeat, 1));
      break;
    case CueKind.GuardReset:
      break; // settles with the frame
    default:
      throw argRangeValue('cue', cue.Kind, 'No effect is planned for this cue.');
  }
}

/** One entry of EffectTrace (C#: the nested class EffectTrace.Entry). */
export class Entry {
  constructor(members) {
    this.Id = EffectId.CardDraw;
    this.EventOrder = 0;
    /** double */
    this.StartSeconds = 0;
    /** double */
    this.Seconds = 0;
    /** float */
    this.NominalMs = 0;
    this.Skipped = false;
    this.Finished = false;
    if (members != null) Object.assign(this, members);
  }
}

/**
 * What played, when and for how long (#78 reads it). The clock is injected, so the View passes
 * the unscaled game time and a test passes its own. A switched-off effect is recorded as skipped
 * with 0 s, so the measurement can tell "off" from "never happened".
 */
export class EffectTrace {
  static Entry = Entry;

  /** @param {() => number} clock Func<double> */
  constructor(clock) {
    if (clock == null) throw argNull('clock');
    /** @private readonly */
    this._clock = clock;
    /** @private readonly List<Entry> */
    this._entries = [];
    /**
     * The battle speed in force when Begin records an effect's nominal length (#348), so the
     * nominal and the measured length are read on the same clock. (C#: a get/set auto-property.)
     */
    this.Speed = BattleSpeed.Default;
  }

  /** IReadOnlyList<Entry> */
  get Entries() {
    return this._entries;
  }

  /** Starts timing one effect; hand the returned number to End. */
  Begin(id, eventOrder, count = 1) {
    const entry = new Entry();
    entry.Id = id;
    entry.EventOrder = eventOrder;
    entry.StartSeconds = this._clock();
    entry.NominalMs = f32(BattleSpeed.WallMs(EffectCatalog.Of(id).TotalMs(count), this.Speed));
    this._entries.push(entry);
    return this._entries.length - 1;
  }

  End(handle) {
    if (handle < 0 || handle >= this._entries.length) throw argRange('handle');
    const entry = this._entries[handle];
    if (entry.Finished) return;
    entry.Seconds = this._clock() - entry.StartSeconds;
    entry.Finished = true;
  }

  Skip(id, eventOrder) {
    const entry = new Entry();
    entry.Id = id;
    entry.EventOrder = eventOrder;
    entry.StartSeconds = this._clock();
    entry.Skipped = true;
    entry.Finished = true;
    this._entries.push(entry);
  }

  /** One line per effect: id, event, start, measured ms, nominal ms, skipped. */
  ToCsv() {
    let sb = 'effect,event,start_s,measured_ms,nominal_ms,skipped\n';
    for (const e of this._entries) {
      sb += String(e.Id) + ','
        + String(e.EventOrder) + ','
        + netCustomFixed(e.StartSeconds, 3, 15) + ','
        + netCustomFixed(e.Seconds * 1000.0, 0, 15) + ','
        + netCustomFixed(e.NominalMs, 0, 7) + ','
        + (e.Skipped ? '1' : '0') + '\n';
    }
    return sb;
  }
}

/** For the tests: the .NET custom-format writer ToCsv uses. Not a C# member. */
export const _netCustomFixed = netCustomFixed;
