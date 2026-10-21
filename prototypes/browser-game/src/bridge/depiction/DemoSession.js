// Port of unity-port/unity-project-kit/Assets/View/Depiction/Bridge/DemoSession.cs
// The demo's run of battles (#191): one enemy on its own, or a chain (battle_core_v4 §12). Pure C#:
// BattleCore only, no UnityEngine. It decides the order, the line each battle is fought on, what
// carries between battles (BattleCore.Chain), the rest, a battle given up (#203), the end of the
// run, and every word the mode screen, the end screen and the battle's one button print — so the
// View (DemoFlow) only lays the words out and forwards the buttons.
import {
  ArgumentException, ArgumentNullException, InvalidOperationException, defineEnum, roundAway,
} from '../../shared/cs.js';
import { EnemyRank, GameResult } from '../../core/battle/Types.js';
// `BattleCore.Chain` in the C#: aliased here because DemoSession has static methods named Chain.
import { BattleTally, Chain as CoreChain } from '../../core/battle/Chain.js';
import { Departure } from '../../core/battle/DeckRules.js';
import { Cards } from '../../core/battle/Cards.js';
import { Constants } from '../../core/battle/Constants.js';
import { Enemies } from '../../core/battle/Enemies.js';
import { SeededRng } from '../../core/battle/IRng.js';
import { BattleSetup } from '../../core/battle/TurnLoop.js';
import { OmenDisclosure } from '../depictionScript/OmenDisclosure.js';
import { BattleLaunch } from './BattleLaunch.js';
import { CoreBattleSource } from './CoreBattleSource.js';
import { DeckBuilder } from './DeckBuilder.js';

/** new ArgumentNullException(nameof(x)) with .NET's message. */
function argNull(name) {
  const e = new ArgumentNullException(`Value cannot be null. (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

/**
 * - Single: one enemy, then the end screen.
 * - Chain: §12: the enemies in order, HP and stamina carried, a rest offered between; a loss ends it.
 */
export const DemoMode = defineEnum(['Single', 'Chain']);

/**
 * Where the run stands.
 * - Ready: the next battle can start.
 * - Fighting: a battle is being fought.
 * - BetweenBattles: a chained battle was won and another follows: rest or go on.
 * - Over: lost, or the last battle won. Again or back to the deck.
 */
export const DemoStage = defineEnum(['Ready', 'Fighting', 'BetweenBattles', 'Over']);

/**
 * What the end screen shows and offers. Every string is final text; the View prints it as it is.
 *
 * JS extra: `init` plays the part of a C# object initializer (`new DemoEndScreen { Won = won }`):
 * its own properties are assigned after the field defaults. `new DemoEndScreen()` is the C# default.
 */
export class DemoEndScreen {
  constructor(init) {
    this.Title = '';
    this.Won = false;
    /** C# `public readonly List<string> Lines`: the list is fixed, its items are added to. */
    this.Lines = [];

    /** A chained battle was won and another follows: the two ways on. */
    this.CanGoOn = false;
    this.RestLabel = '';
    this.GoOnLabel = '';

    /** A single battle ended, fought out or given up: the mode screen again, to pick another enemy with the same deck (#211). */
    this.CanChooseEnemy = false;
    this.ChooseEnemyLabel = '';

    /** The same run again. Off only on DemoEnding's fallback, where the run is left half-done (#297). */
    this.CanAgain = true;
    this.AgainLabel = '';
    this.BackLabel = 'デッキ選択へ戻る';

    if (init) Object.assign(this, init);
  }
}

/** C# `unchecked(seed + (run * 104729))`: int arithmetic that wraps. */
function RunSeed(seed, run) {
  return (seed + Math.imul(run, 104729)) | 0;
}

/** C# `private static Departure Plain(List<CardInstance> deck)` */
function Plain(deck) {
  if (deck == null) throw argNull('deck');
  return Departure.Plain(deck);
}

/**
 * C# overloads take either a `List<CardInstance>` (a bare deck, made Plain) or a `Departure`; an
 * array is the deck. A null goes to the Departure overload (C# would need the null's static type to
 * pick one; either way it is an ArgumentNullException).
 */
function DepartureOf(deckOrDeparture) {
  return Array.isArray(deckOrDeparture) ? Plain(deckOrDeparture) : deckOrDeparture;
}

/**
 * C# `double.ToString("0.#", CultureInfo.InvariantCulture)` of a value already rounded to one decimal:
 * one decimal at most, no trailing ".0". (A negative zero would print "-0" in .NET; turns are never
 * negative, so it cannot occur.)
 */
function FormatOneDecimal(value) {
  let text = value.toFixed(1);
  if (text.endsWith('.0')) text = text.slice(0, -2);
  return text;
}

export class DemoSession {
  /** §12's default three, on the v4.3 ids: 錆槍の竜兵 → 瘴牙の走竜 → 鉄壁の門竜. */
  static DefaultChain = Object.freeze(['polearm_warped', 'shadow_hound', 'armored_warden']);

  #seed;
  /** @type {Array} List<BattleTally> */
  #tallies = [];
  #started = 0;
  /** int? */
  #hp = null;
  /** int? */
  #stamina = null;
  /** Func<string, int> or null */
  #disclosureOf;

  #Mode;
  #Departure;
  #Order;
  #Index = 0;
  #Stage = DemoStage.Ready;
  #Surrendered = false;

  /**
   * The deck is checked against §8 here as well as on the departure screen, because a run can be
   * made without the screen (the tests, a bare deck). The slots and the talent are the screen's
   * to check (DepartureBuilder.CanSetOut); a run made from a bare deck has neither.
   *
   * C# private: build a run with Single / Chain.
   */
  constructor(mode, order, departure, seed, disclosureOf) {
    if (departure == null || departure.Deck == null) throw argNull('departure');
    const validation = Cards.Validate(departure.Deck);
    if (!validation.Ok) throw new ArgumentException('DemoSession: the deck breaks §8 — ' + validation.Errors.join(' / '));
    if (departure.StartCellsBack < 0) throw new ArgumentException('DemoSession: the start never moves forward.');
    this.#Mode = mode;
    this.#Order = order;
    this.#Departure = departure.with({ Deck: [...departure.Deck] });
    this.#seed = seed;
    this.#disclosureOf = disclosureOf ?? null;
  }

  get Mode() { return this.#Mode; }

  /**
   * What the run set out with (#58): the deck every battle is shuffled from, the slots, the
   * start a tool moved back, and the talent card. The talent is only recorded; #59 reads it.
   */
  get Departure() { return this.#Departure; }

  /** The enemies of the run, in the order they are fought. */
  get Order() { return this.#Order; }

  /** The battle being fought, or the last one fought (0-based). */
  get Index() { return this.#Index; }

  get Stage() { return this.#Stage; }

  /** The tallies of the battles fought since the run (re)started, in order. */
  get Tallies() { return this.#tallies; }

  /** The run ended on 「降参する」 (#203): its last battle was given up, not lost on HP. */
  get Surrendered() { return this.#Surrendered; }

  /**
   * One enemy on its own. `run` counts the runs started from the same seed (the flow passes how
   * many it has made), so choosing the same mode again deals afresh. `disclosureOf` is the
   * journal's 開示度 by enemy id (DisclosureOf).
   *
   * C# overloads: Single(List<CardInstance> deck, ...) and Single(Departure departure, ...) — with
   * what the departure screen set out with (#58).
   */
  static Single(deckOrDeparture, enemyId, seed, run = 0, disclosureOf = null) {
    const departure = DepartureOf(deckOrDeparture);
    return new DemoSession(DemoMode.Single, Object.freeze([BattleLaunch.EnemyById(enemyId, 'DemoSession')]), departure, RunSeed(seed, run), disclosureOf);
  }

  /**
   * §12: a chain of battles, by default DefaultChain.
   *
   * C# overloads: Chain(List<CardInstance> deck, ...) and Chain(Departure departure, ...) — §12's
   * chain with what the departure screen set out with (#58): the same screen leads to both modes.
   */
  static Chain(deckOrDeparture, seed, order = null, run = 0, disclosureOf = null) {
    const departure = DepartureOf(deckOrDeparture);
    order = order ?? DemoSession.DefaultChain;
    if (order.length === 0) throw new ArgumentException('DemoSession: a chain needs at least one enemy.');
    const enemies = [];
    for (const id of order) enemies.push(BattleLaunch.EnemyById(id, 'DemoSession'));
    return new DemoSession(DemoMode.Chain, Object.freeze(enemies), departure, RunSeed(seed, run), disclosureOf);
  }

  /** The single-mode 「ランダム」: one of the eleven, fixed by the seed and how many picks came before. */
  static RandomEnemyId(seed, pick = 0) {
    const roll = new SeededRng(RunSeed(seed, pick)).NextDouble();
    const index = Math.min(Enemies.All.length - 1, Math.trunc(Math.floor(roll * Enemies.All.length)));
    return Enemies.All[index].Id;
  }

  /**
   * The line an enemy is fought on: the slice's 6 cells, or as many as it needs to start at
   * START_GAP — 7 for the size-2 enemies (roster §0 「開始に要るマス数」).
   *
   * C# overload FieldCellsFor(enemy, playerStartCell, startGap): the same for a start a tool moved
   * (#58). A cell back is a cell more of gap, so the enemy's cell and the width stay what they were.
   */
  static FieldCellsFor(enemy, playerStartCell, startGap) {
    if (playerStartCell === undefined && startGap === undefined) {
      return DemoSession.FieldCellsFor(enemy, Constants.PlayerStartCell, Constants.StartGap);
    }
    if (enemy == null) throw argNull('enemy');
    return Math.max(BattleSetup.SliceFieldCells, playerStartCell + startGap + enemy.Size);
  }

  /** `Order[Index]`. Index only moves on in GoOn, which needs a next battle, so it is never past the end. */
  get Current() { return this.#Order[this.#Index]; }

  /** The next battle: this enemy, its line, the deck (shuffled by TurnLoop.Start), the start, and what the last one carried. */
  NextSetup() {
    const enemy = this.Current;
    const cells = DemoSession.FieldCellsFor(enemy, this.#Departure.PlayerStartCell, this.#Departure.StartGap);
    return this.#Departure.SetupFor(enemy, cells, this.#hp, this.#stamina);
  }

  /**
   * battle_ui_ux_v2 §7 (#500): the journal's 開示度 for `enemy`, read once as its battle starts —
   * it does not move during a battle, and a chain reads each enemy as its turn comes. A run made
   * without a journal is fully disclosed (2); −1 has no parts yet and reads as 0.
   */
  DisclosureOf(enemy) {
    if (enemy == null) throw argNull('enemy');
    return this.#disclosureOf == null ? OmenDisclosure.Full : OmenDisclosure.Clamp(this.#disclosureOf(enemy.Id));
  }

  /** Starts the next battle. Every start takes a seed of its own, so a retry is dealt afresh. */
  StartBattle(suggestCards = false) {
    if (this.#Stage !== DemoStage.Ready) throw new InvalidOperationException('DemoSession: no battle is ready (' + this.#Stage + ').');
    const seed = (this.#seed + Math.imul(this.#started, 7919)) | 0;
    const total = this.#Mode === DemoMode.Chain ? this.#Order.length : 1;
    const source = new CoreBattleSource(this.NextSetup(), seed, suggestCards, 0, this.#Index + 1, total, this.DisclosureOf(this.Current));
    this.#started++;
    this.#Stage = DemoStage.Fighting; // only once the battle is built, so a failed start leaves the run Ready
    return source;
  }

  /** The battle has ended: tally it and carry what carries. A loss or the last win ends the run. */
  Finish(source) {
    if (source == null) throw argNull('source');
    if (this.#Stage !== DemoStage.Fighting) throw new InvalidOperationException('DemoSession: no battle is being fought (' + this.#Stage + ').');
    if (source.State.Result === GameResult.Ongoing) throw new InvalidOperationException('DemoSession: the battle has not ended.');

    const tally = CoreChain.Tally(source.State, source.History);
    this.#tallies.push(tally);
    const carried = CoreChain.Carry(source.State);
    this.#hp = carried.Hp;
    this.#stamina = carried.Stamina;
    const more = this.#Mode === DemoMode.Chain && tally.Result === GameResult.Won && this.#Index + 1 < this.#Order.length;
    this.#Stage = more ? DemoStage.BetweenBattles : DemoStage.Over;
    return tally;
  }

  /**
   * 「降参する」 (#203): the player gives up the battle being fought. It is tallied where it
   * stands — the turns, the HP left, the cards played so far — as a loss, and a loss ends the
   * run, a chain too (§12). Nothing carries. A battle the core has already decided (its last
   * events still on the screen) keeps its own result, as Finish tallies it.
   */
  Surrender(source) {
    if (source == null) throw argNull('source');
    if (this.#Stage !== DemoStage.Fighting) throw new InvalidOperationException('DemoSession: no battle is being fought (' + this.#Stage + ').');
    if (source.State.Result !== GameResult.Ongoing) return this.Finish(source);

    const tally = CoreChain.Tally(source.State, source.History).with({ Result: GameResult.Lost });
    this.#tallies.push(tally);
    this.#Surrendered = true;
    this.#Stage = DemoStage.Over;
    return tally;
  }

  /** §12: between chained battles, rest (HP +30% of the maximum, stamina full) or go straight on. */
  GoOn(rest) {
    if (this.#Stage !== DemoStage.BetweenBattles) throw new InvalidOperationException('DemoSession: there is no next battle (' + this.#Stage + ').');
    if (rest) {
      const rested = DemoSession.RestAfter(this.#tallies[this.#tallies.length - 1]);
      this.#hp = rested.Hp;
      this.#stamina = rested.Stamina;
    }
    this.#Index++;
    this.#Stage = DemoStage.Ready;
  }

  /** The same run from its first battle, at full HP (§12: after a clean sweep, the same order again). */
  Again() {
    if (this.#Stage === DemoStage.Fighting) throw new InvalidOperationException('DemoSession: a battle is being fought.');
    this.#Index = 0;
    this.#hp = null;
    this.#stamina = null;
    this.#tallies.length = 0; // List.Clear(): the same list, emptied (Tallies hands out this list).
    this.#Surrendered = false;
    this.#Stage = DemoStage.Ready;
  }

  /**
   * §12's rest after the battle `last` tallies: its HP left, back by 30% of its maximum, and
   * stamina full. The rest button's words and the rest itself both read this, so the number
   * printed is the number given (#211).
   * @returns {{ Hp: number, Stamina: number }} C# (int Hp, int Stamina)
   */
  static RestAfter(last) {
    if (last == null) throw argNull('last');
    return CoreChain.Rest(last.HpLeft, last.MaxHp, Constants.BaseMaxStamina);
  }

  /** A chain won to the end. */
  get AllWon() {
    return this.#Mode === DemoMode.Chain && this.#Stage === DemoStage.Over && this.#tallies.length === this.#Order.length
      && this.#tallies.every((t) => t.Result === GameResult.Won);
  }

  // ---- the words ----

  /** The end screen for the battle just finished: §12's items and the ways on. */
  EndScreen() {
    if (this.#tallies.length === 0) throw new InvalidOperationException('DemoSession: no battle has finished yet.');
    const last = this.#tallies[this.#tallies.length - 1];
    const enemy = Enemies.ById(last.EnemyId);
    const won = last.Result === GameResult.Won;
    const screen = new DemoEndScreen({ Won: won });
    const chain = this.#Mode === DemoMode.Chain;

    if (this.#Mode === DemoMode.Single) screen.Title = won ? enemy.Name + 'に勝ちました' : enemy.Name + (this.#Surrendered ? 'に降参しました' : 'に敗れました');
    else if (this.AllWon) screen.Title = '連戦に全勝しました';
    else screen.Title = won ? (this.#Index + 1) + ' 戦目に勝ちました' : (this.#Index + 1) + (this.#Surrendered ? ' 戦目で降参しました' : ' 戦目で力尽きました');

    if (chain) screen.Lines.push('何戦目: ' + (this.#Index + 1) + ' / ' + this.#Order.length + '（' + enemy.Name + '）');
    else screen.Lines.push('相手: ' + enemy.Name);
    screen.Lines.push('残り HP: ' + last.HpLeft + ' / ' + last.MaxHp);
    screen.Lines.push('ターン数: ' + last.Turns + (chain && this.#tallies.length > 1 ? '（平均 ' + this.AverageTurns() + '）' : ''));
    screen.Lines.push('使った札 ' + last.CardsPlayed + ' 枚の属性: ' + DemoSession.Breakdown(last));
    screen.Lines.push('特性の発動: ' + last.TraitsFired + ' 回');
    if (chain && this.#Stage === DemoStage.Over && this.#tallies.length > 1) {
      // §12: the chain's result screen adds up the whole run.
      const total = CoreChain.Total(this.#tallies);
      screen.Lines.push('連戦の合計（' + this.#tallies.length + ' 戦）: 使った札 ' + total.CardsPlayed + ' 枚の属性: ' + DemoSession.Breakdown(total));
      screen.Lines.push('連戦の合計: 特性の発動 ' + total.TraitsFired + ' 回、平均ターン数 ' + this.AverageTurns());
    }

    if (this.#Stage === DemoStage.BetweenBattles) {
      screen.CanGoOn = true;
      const rested = DemoSession.RestAfter(last);
      screen.RestLabel = '休んで次へ（HP +' + (rested.Hp - last.HpLeft) + '、スタミナ全回復）';
      screen.GoOnLabel = 'そのまま次へ';
      screen.Lines.push('次: ' + this.#Order[this.#Index + 1].Name);
    }
    if (this.#Mode === DemoMode.Single) {
      screen.CanChooseEnemy = true;
      screen.ChooseEnemyLabel = '敵を選び直す';
    }
    screen.AgainLabel = this.#Mode === DemoMode.Single ? 'もう一度' : this.AllWon ? '同じ並びをもう一度' : '最初からもう一度';
    return screen;
  }

  /** 「攻撃 9・ムーブ 4・防御 3」 in §2.2 order, attributes not used left out. */
  static Breakdown(tally) {
    if (tally == null) throw argNull('tally');
    const parts = [];
    for (const attribute of BattleTally.Order) {
      const n = tally.CountOf(attribute);
      if (n > 0) parts.push(DeckBuilder.AttributeWords(attribute) + ' ' + n);
    }
    return parts.length === 0 ? 'なし' : parts.join('・');
  }

  /** The mean of the turns of the battles fought so far, one decimal ("8.5"). */
  AverageTurns() {
    return DemoSession.AverageOf(this.#tallies);
  }

  /** The mean turns of some battles, to one decimal rounded away from zero, no trailing ".0" ("8.5", "8", "6.7"). */
  static AverageOf(tallies) {
    if (tallies == null || tallies.length === 0) return '0';
    let sum = 0;
    for (const tally of tallies) sum += tally.Turns;
    // Math.Round(sum / n, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture)
    return FormatOneDecimal(roundAway(sum / tallies.length, 1));
  }

  // ---- the mode screen's words ----

  static get ModeTitle() { return '戦い方を選ぶ'; }

  static get SingleHeading() { return '1 体で区切る（敵を選ぶ）'; }

  /** §12 in one line: what carries and what ends the chain. */
  static get ChainHeading() { return '連戦（HP とスタミナを持ち越し、負けたら終わり）'; }

  static get RandomLabel() { return 'ランダム'; }

  static get BackToDeckLabel() { return 'デッキ選択へ戻る'; }

  // ---- the battle's word ----

  /** The one button over a demo battle (#203): give it up and go to the end screen. */
  static get SurrenderLabel() { return '降参する'; }

  /** The line an enemy has on the mode screen: 「錆槍の竜兵（通常・HP 60）」. */
  static EnemyLine(enemy) {
    if (enemy == null) throw argNull('enemy');
    const rank = enemy.Rank === EnemyRank.Boss ? 'ボス' : enemy.Rank === EnemyRank.Elite ? '精鋭' : '通常';
    return enemy.Name + '（' + rank + '・HP ' + enemy.MaxHp + '）';
  }

  /** The chain as the mode screen offers it: 「連戦（3 戦）: 錆槍の竜兵 → 瘴牙の走竜 → 鉄壁の門竜」. */
  static ChainLine(order = null) {
    order = order ?? DemoSession.DefaultChain;
    const names = [];
    for (const id of order) names.push(Enemies.ById(id).Name);
    return '連戦（' + order.length + ' 戦）: ' + names.join(' → ');
  }
}
