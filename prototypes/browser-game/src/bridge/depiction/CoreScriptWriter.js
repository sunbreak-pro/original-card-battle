// Port of unity-port/unity-project-kit/Assets/View/Depiction/Bridge/CoreScriptWriter.cs
// Turns the core's event stream (BattleCore.BattleEvent) into the script the screen plays
// (DepictionEvent / Cue / DepictionFrame). Pure: BattleCore + Depiction.Script.
//
// Nothing here works a rule out. Every number on a cue is copied from an event's settled value
// (StaminaAfter, TargetHpAfter, GuardAfter …), and what a held card or a shown omen would do comes
// from the core's own TurnLoop.Preview / PreviewOmen. What this class does decide is presentation:
// where one screen event ends and the next begins, and which beat shows which number.
//
// JS extension — several enemies (docs/port-notes/depiction-multi-enemy.md). The C# writer models
// the first enemy only (one _enemy, one _omen that takes every OmenSet whichever enemy it is about),
// and throws KeyNotFoundException in the two-enemy battle the exploration hands in on layers 2〜3.
// This port keeps what the C# keeps of that one enemy once per enemy (EnemyTrack), reads each
// event's enemy from its Unit, and writes every enemy into the frame (DepictionFrame.Enemies /
// Omens, FloorFrame.Enemies, Cue.Unit, DepictionEvent.Unit). The single-valued members the C# writes
// (Frame.Enemy, Frame.Omen, Floor.EnemyCell …) describe the first enemy standing in the line; in a
// one-enemy battle every member the C# writes comes out exactly as it does (MultiEnemy.test.mjs
// replays a snapshot of the one-enemy output taken before this extension).
import { ArgumentNullException, KeyNotFoundException, csEquals, enumIndex } from '../../shared/cs.js';
import {
  ActionExecuted, ActionWhiffed, BattleEnded, CardExiled, CardPlayed, CellsMoved, DamageDealt, DeckReshuffled,
  Drawn, EnemyDefeated, GuardCleared, GuardGained, Healed, HandDiscarded, MaxStaminaChanged, MoveBlocked, OmenSet,
  PlanChanged, PlanSet, Reflected, ReserveChecked, Rested, StaminaBroken, StaminaGained, StaminaRecovered,
  StaminaSpent, StanceFired, StanceResumed, StanceSet, StanceStopped, StatusApplied, StatusCleared, StatusConsumed,
  StatusHpChanged, StatusTicked, TraitEvaluated, TurnStarted, WallHit,
} from '../../core/battle/BattleEvents.js';
import {
  Actor, BattleAttribute, BattlePhase, EnemyRank, EnumTokens, GameResult, StanceHook, StatusKind, TargetKind,
  TraitCondition, TraitEffect,
} from '../../core/battle/Types.js';
import { PlayRefusal, TurnLoop } from '../../core/battle/TurnLoop.js';
import { EnemyAi } from '../../core/battle/EnemyAi.js';
import { Field } from '../../core/battle/Field.js';
import { Combat } from '../../core/battle/Combat.js';
import { Constants } from '../../core/battle/Constants.js';
import { CardCatalog } from '../../core/battle/CardCatalog.js';
import {
  BattleOutcome, CardAim, CornerFrame, Cue, CueKind, DepictionEvent, DepictionEventKind, DepictionFrame,
  DepictionText, FloorEnemy, FloorFrame, OmenFrame, OmenHit, PileFrame, PlayedFrame, StrikeSystem, UnitFrame, UnitSide,
} from '../depictionScript/DepictionTypes.js';
import { EffectGrammar, FxSystem } from '../depictionScript/EffectGrammar.js';
import { EffectStrength } from '../depictionScript/Effects.js';
import { OmenDisclosure } from '../depictionScript/OmenDisclosure.js';
import { StanceList } from '../depictionScript/StanceList.js';
import { PileKind } from '../depictionScript/PileLayout.js';
import { CoreText } from './CoreText.js';
import { CardFaceText } from './CardFaceText.js';
import { StatusChipText } from './StatusChipText.js';
import { CardSystems } from './CardSystems.js';
import { FloorCells } from './FloorCells.js';
import { PileCards } from './PileCards.js';

/** new ArgumentNullException(nameof(x)) with .NET's message. */
function argNull(name) {
  const e = new ArgumentNullException(`Value cannot be null. (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

/** C# `dict[key]` on an IReadOnlyDictionary (a Map here): throws KeyNotFoundException when missing. */
function getOrThrow(map, key) {
  if (!map.has(key)) throw new KeyNotFoundException(`The given key '${key}' was not present in the dictionary.`);
  return map.get(key);
}

/** C# object initializer: `new T { A = 1, B = 2 }` → construct with the defaults, then assign in order. */
function make(Type, props) {
  return Object.assign(new Type(), props);
}

/** C# `Equals(a, b)` (static object.Equals) on records: null-safe structural equality. */
function Equals(a, b) {
  return csEquals(a ?? null, b ?? null);
}

/** JS extension: the enemy a cue about `side`'s numbers concerns — `unit` for an enemy, −1 for the player. */
function holderUnit(side, unit) {
  return side === Actor.Enemy ? unit : -1;
}

/**
 * The numbers one unit shows, copied forward from events as they are read. (C# private sealed class)
 * `Statuses` is a SortedDictionary<StatusKind, int>: a Map here, enumerated in StatusKind order
 * through `SortedStatuses()`.
 */
class UnitModel {
  constructor() {
    this.Hp = 0;
    this.HpMax = 0;
    this.Guard = 0;
    this.Stamina = 0;
    this.StaminaMax = 0;
    this.Cell = 0;
    this.Size = 0;
    /** The unit's fall has been written (#288); the frames from its Defeat beat on show it gone. */
    this.Down = false;
    /** @type {Map<string, number>} SortedDictionary<StatusKind, int> */
    this.Statuses = new Map();
    /** §4 (v4.4): the name of each stance on the list of permanent effects, in the order they were put on; a repeat is listed again. */
    this.Stances = [];
  }

  /** The SortedDictionary's enumeration: by the StatusKind's underlying value. */
  SortedStatuses() {
    return [...this.Statuses.entries()].sort((a, b) => enumIndex(StatusKind, a[0]) - enumIndex(StatusKind, b[0]));
  }

  CopyFrom(unit, enemy) {
    this.Hp = unit.Hp;
    this.HpMax = unit.MaxHp;
    this.Guard = unit.Guard;
    this.Stamina = unit.Stamina;
    this.StaminaMax = unit.MaxStamina;
    this.Cell = unit.Cell;
    this.Size = unit.Size;
    this.Down = false;
    this.Statuses.clear();
    for (const kind of unit.Statuses.Kinds) this.Statuses.set(kind, unit.Statuses.Stacks(kind));
    this.Stances.length = 0;
    for (const entry of unit.StanceList) this.Stances.push(CoreScriptWriter.StanceName(entry.Source, enemy));
  }
}

/**
 * JS extension: what the writer keeps of one enemy. The C# keeps these once, as private fields of the
 * writer, for the first enemy (_enemyDef, _enemy, _omen, _omenVisible, _omenPreview, _omenPreviewFor,
 * _plan, _planVisible, _planChanged, _enemyReserveHeld, and _enemyFalls as FallPending); track 0 is
 * exactly those.
 */
class EnemyTrack {
  constructor(def) {
    this.Def = def;
    this.Model = new UnitModel();
    this.Omen = null;
    this.OmenVisible = false;
    /**
     * The core's reading of the omen on screen (#248, TurnLoop.PreviewOmen), and the omen it was
     * taken for. It is only taken on a state from the player's turn: a frame is read against the
     * state a whole move left, and after an enemy phase that state is past the turn end the
     * number already counts — even when the enemy chose the same action again. Until the omen
     * shown is spent it keeps the number the player last saw; a newly decided omen starts over.
     */
    this.OmenPreview = null;
    this.OmenPreviewFor = null;
    /** #50: the 予定 (the second action an elite or a boss plans), whether it is on screen, and whether a 予定変更 has replaced it. */
    this.Plan = null;
    this.PlanVisible = false;
    this.PlanChanged = false;
    /** The enemy's 構え, held back so it plays with its next omen and not inside the attack (see Write). */
    this.ReserveHeld = null;
    /** The core said this enemy fell in the move being written; its fall is written as a beat of its own (#288). */
    this.FallPending = false;
  }
}

export class CoreScriptWriter {
  constructor(enemyDef) {
    if (enemyDef == null) throw argNull('enemyDef');
    /** @private readonly EnemyDef — the first enemy's, as handed in (track 0's). */
    this._enemyDef = enemyDef;
    this._player = new UnitModel();
    /**
     * JS extension: one track per enemy, in the core's enemy order. Track 0 is the C#'s one enemy;
     * the others are taken from the state at Opening (or at the first Write).
     * @private
     */
    this._tracks = [new EnemyTrack(enemyDef)];
    /** @private readonly List<CardInstance> */
    this._hand = [];
    this._turn = 0;

    /** The power the card being written has put on the enemy it reads so far (#248), or −1 before its first blow. */
    this._playedPower = -1;
    this._playedUnit = 0;
    this._playerActs = false;

    /**
     * battle-visual-v1 §5.5 (#455): from the player's turn end until the next turn start the enemy
     * has the turn. Unlike _playerActs it is false before turn 1 opens.
     */
    this._enemyTurn = false;
    this._order = 0;

    /** The enemy action being written whiffed (§6): its trait miss has nothing to strike off. */
    this._whiffed = false;

    /** #455: the core has just made the draw pile again (DeckReshuffled); the next Drawn marks its beat. */
    this._rebuildPending = false;

    /** The system (§5.3) of the card or action whose blows are being written. */
    this._strike = StrikeSystem.Slash;

    /** #57 (§5.9 J2): a trait of the card or action being written fired, so its blows carry the short cue. */
    this._traitLit = false;

    /**
     * The enemy whose phase is being written has carried out an action in it already (#189: an
     * elite's second one opens a beat of its own). Enemies take their phases one by one (§7.4), so
     * one flag serves them all: each phase's GuardCleared clears it.
     */
    this._enemyActed = false;

    /** JS extension: the falls the core reported in the move being written, in the order they fell (units). */
    this._falls = [];

    /** JS extension: the enemy whose Defeat beat was written last — the one Frame.Enemy shows once none stands. */
    this._lastDown = 0;

    /** §12 連戦 (#191): which battle of the chain this is, and how many there are; 1 / 1 alone. */
    this.ChainIndex = 1;
    this.ChainTotal = 1;

    /**
     * battle-visual-v1 §4.4 (#455): how far the journal has read this enemy (開示度 0〜2). Below 2 the
     * omen's numbers and statuses are left off the script, and at 0 its plan (OmenDisclosure). CoreBattleSource sets
     * it once as the battle starts (#500); left unset it is 2, everything shown. One level for every
     * enemy of the battle (EncounterBattle hands in the lowest of them).
     */
    this.Disclosure = OmenDisclosure.Full;
  }

  /** JS extension: one track per enemy of `state`, track 0 kept; a later enemy's track reads its def off the state. (private) */
  EnsureTracks(state) {
    for (let k = this._tracks.length; k < state.Enemies.length; k++) this._tracks.push(new EnemyTrack(state.Enemies[k].Def));
  }

  /**
   * JS extension: the enemy the single-valued members of a frame describe (Frame.Enemy, Frame.Omen,
   * Floor.EnemyCell / Gap / AimCells …, Player.Range): the first one standing in the line — the
   * lowest cell among the enemies the core still has standing. An enemy that fell in the move being
   * written (its Defeat beat still to come) is passed over: the core has already taken its cells, so
   * the player may stand past it. When it is the last one (the battle is won), it is that one; once
   * every fall has been written, the one that fell last. Always 0 in a one-enemy battle. (private)
   */
  get FocusUnit() {
    let focus = -1;
    let falling = -1;
    for (let k = 0; k < this._tracks.length; k++) {
      const track = this._tracks[k];
      if (track.Model.Down) continue;
      if (track.FallPending) {
        if (falling < 0 || track.Model.Cell < this._tracks[falling].Model.Cell) falling = k;
        continue;
      }
      if (focus < 0 || track.Model.Cell < this._tracks[focus].Model.Cell) focus = k;
    }
    return focus >= 0 ? focus : falling >= 0 ? falling : this._lastDown;
  }

  /** JS extension: enemy `unit` has fallen in the core — its fall written (Down) or still to come. (private) */
  Fallen(unit) {
    const track = this._tracks[unit];
    return track.Model.Down || track.FallPending;
  }

  /** §7.2: N as the two models stand now — the player and the enemy the frame shows (FocusUnit). (C# private property) */
  get Gap() {
    return this.GapTo(this.FocusUnit);
  }

  /** JS extension: §7.2, N between the player and enemy `unit` as the models stand now. (private) */
  GapTo(unit) {
    return Field.Gap(this._player.Cell, this._player.Size, this._tracks[unit].Model.Cell);
  }

  /**
   * The screen before turn 1. The first omen is already decided (TurnLoop.Start) but stays
   * hidden: step 5 of the first turn is what shows it.
   */
  Opening(state) {
    if (state == null) throw argNull('state');
    this._turn = Math.max(1, state.Turn);
    this._player.CopyFrom(state.Player, null);
    // JS extension: every enemy. A track of a later enemy whose def changed (a writer reused for
    // another battle) starts afresh; one the state no longer has goes.
    for (let k = 1; k < this._tracks.length && k < state.Enemies.length; k++) {
      if (this._tracks[k].Def !== state.Enemies[k].Def) this._tracks[k] = new EnemyTrack(state.Enemies[k].Def);
    }
    this._tracks.length = Math.max(1, Math.min(this._tracks.length, state.Enemies.length));
    this.EnsureTracks(state);
    for (let k = 0; k < this._tracks.length; k++) {
      const track = this._tracks[k];
      const unit = k < state.Enemies.length ? state.Enemies[k] : null;
      // C#: _enemy.CopyFrom(state.Enemy, _enemyDef); _omen = state.Omen; _plan = state.Enemies[0].Plan.
      if (unit != null) track.Model.CopyFrom(unit.Body, track.Def);
      track.Omen = unit != null ? unit.Omen : null;
      track.Plan = unit != null ? unit.Plan : null;
      track.PlanVisible = false;
      track.PlanChanged = false;
      track.OmenVisible = false;
    }
    this._lastDown = 0;
    this._hand.length = 0;
    this._hand.push(...state.Hand);
    this._playerActs = state.Phase === BattlePhase.PlayerAction;
    this._enemyTurn = false;
    return this.Snapshot(state);
  }

  /**
   * One move of the loop → the screen events it plays as. BeginPlayerTurn and PlayCard are one
   * event each; EndTurn is three for the slice (turn end, the enemy's action, the next omen),
   * the same beats the screen already knows. A few moments take a beat of their own so each
   * event stays inside the 2.0 s it may take: the enemy's push driving the player into the
   * wall, and (#189) an enemy turn start that did something, an elite's second action, the
   * second blow of 二段斬り and the step an enemy takes after its blow.
   *
   * With several enemies (JS extension) EndTurn plays each enemy's phase in turn: its 敵の番 beat,
   * its action's beats and its next omen. An enemy that falls while others stand falls as a beat of
   * its own right after the beat it fell in.
   *
   * `after` is the state the move left. It is only asked what the cards still in the hand and the
   * omen on screen would do (the lamp, the omen's number), never for a number an event already carries.
   * @returns {Array} List<DepictionEvent>
   */
  Write(events, after) {
    if (events == null) throw argNull('events');
    if (after == null) throw argNull('after');
    this.EnsureTracks(after);

    const written = [];
    let current = null;

    for (const e of events) {
      const opened = this.OpenIfBoundary(e, current);
      if (opened != null) {
        this.Close(current, after);
        // JS extension (#288 for several enemies): an enemy that fell in the beat just closed, with
        // others still standing (EnemyDefeated), falls before the next beat, so its figure and omen
        // are gone before the next enemy acts. The beat that opened was numbered first; it plays after.
        if (current != null && this._falls.length > 0) {
          const first = opened.Order;
          const falls = this.WriteFalls(after);
          for (let i = 0; i < falls.length; i++) falls[i].Order = first + i;
          opened.Order = first + falls.length;
          written.push(...falls);
        }
        current = opened;
        written.push(current);
        // The enemy's held 構え plays at the head of its next omen (see ApplyReserve). C#: at the end
        // of the move, which with one enemy is the same event and the same place in it.
        if (opened.Kind === DepictionEventKind.NextOmen) this.FlushHeldReserve(current, opened.Unit);
      }
      if (current == null) continue; // TurnLoop.Start's first omen: folded into Opening, not played.
      this.Apply(e, current);
    }

    // A held enemy 構え plays at the head of the last event: the next omen, or — when the
    // battle ended and no omen follows — the enemy's own action.
    if (current != null) this.FlushHeldReserves(current);
    this.Close(current, after);

    // #288: the fall is a beat of its own after everything the move did, so the 800 ms never
    // lengthens the blow's event past its 2.0 s, and the figure stays up until it plays.
    written.push(...this.WriteFalls(after));
    return written;
  }

  /** JS extension: every fall waiting, in the order the core reported them. (private) */
  WriteFalls(after) {
    const out = [];
    while (this._falls.length > 0) out.push(this.WriteFall(this._falls.shift(), after));
    return out;
  }

  /** (C# private; the unit is the JS extension's) */
  WriteFall(unit, after) {
    const track = this._tracks[unit];
    track.FallPending = false;
    track.Model.Down = true;
    track.OmenVisible = false; // a fallen enemy takes its omen with it (TurnLoop.OnFall)
    track.PlanVisible = false; // a fallen elite takes its plan with it, now that the badge draws it (#349)
    this._lastDown = unit;
    const ev = this.NewEvent(DepictionEventKind.Defeat, '倒れ');
    ev.Unit = unit;
    ev.Cues.push(make(Cue, { Kind: CueKind.Defeat, Target: UnitSide.Enemy, Unit: unit }));
    this.Close(ev, after);
    return ev;
  }

  /** JS extension: the core said enemy `unit` fell; its fall waits to be written. (private) */
  QueueFall(unit) {
    const track = this._tracks[unit];
    if (track.FallPending || track.Model.Down) return;
    track.FallPending = true;
    this._falls.push(unit);
  }

  // ---- where a screen event begins ----------------------------------------------------

  /** (C# private) The pattern switch, case by case in the C#'s order (a failed guard falls through). */
  OpenIfBoundary(e, current) {
    if (e instanceof WallHit && current != null && current.Kind === DepictionEventKind.EnemyAction) {
      // §7.3: the player, shoved into the end of the line. Inside a player's card the wall
      // is the enemy's and fits in the card's own event.
      return this.About(this.NewEvent(DepictionEventKind.EnemyAction, '壁に当たる'), current.Unit);
    }

    if (e instanceof TurnStarted && e.Actor === Actor.Player) {
      this._turn = e.Turn;
      this._playerActs = true;
      this._enemyTurn = false;
      return this.NewEvent(DepictionEventKind.TurnStart, 'ターン開始');
    }

    if (e instanceof CardPlayed) {
      const ev = this.NewEvent(DepictionEventKind.PlayCard, e.Card.Def.Name);
      ev.CardId = e.Card.InstanceId;
      ev.Aim = e.Card.Def.Targets === TargetKind.One ? CardAim.Single : CardAim.Self;
      // JS extension: the enemy a card aimed at one was played on (CardPlayed.Unit).
      if (ev.Aim === CardAim.Single) ev.Unit = e.Unit;
      return ev;
    }

    // Step 7 opens the turn end: a turn-end stance (根渡り, 霞み足, #188) fires before 構え
    // and belongs to the same event, so 構え does not open a second one.
    if (e instanceof StanceFired && e.Actor === Actor.Player && e.Hook === StanceHook.TurnEnd) {
      this._playerActs = false;
      this._enemyTurn = true;
      return this.NewEvent(DepictionEventKind.TurnEnd, 'ターン終了');
    }

    if (e instanceof ReserveChecked && e.Actor === Actor.Player
      && (current == null || current.Kind !== DepictionEventKind.TurnEnd)) {
      this._playerActs = false;
      this._enemyTurn = true;
      return this.NewEvent(DepictionEventKind.TurnEnd, 'ターン終了');
    }

    if (e instanceof GuardCleared && e.Actor === Actor.Enemy) {
      this._enemyActed = false;
      return this.About(this.NewEvent(DepictionEventKind.EnemyAction, '敵の番'), e.Unit);
    }

    // roster §1.3 (#189): an elite's or a boss's second action is a beat of its own, so
    // neither blow is squeezed into the 2.0 s one event may take.
    if (e instanceof ActionExecuted && current != null && current.Kind === DepictionEventKind.EnemyAction
      && this._enemyActed) {
      return this.About(this.NewEvent(DepictionEventKind.EnemyAction, e.Action.Name), e.Unit);
    }

    // #189: an enemy turn start that did something of its own (a stance's Guard, 再生 or
    // 出血) is its beat; the action follows as the next one.
    if (e instanceof ActionExecuted && current != null && current.Kind === DepictionEventKind.EnemyAction
      && current.Cues.some((c) => c.Kind === CueKind.GuardGain || c.Kind === CueKind.HpChange)) {
      return this.About(this.NewEvent(DepictionEventKind.EnemyAction, e.Action.Name), e.Unit);
    }

    // §2.2 (#189): an enemy that strikes and then steps in (飛びかかり, 当ててから前へ 2) takes
    // the step as its own beat once the blow has landed.
    if (e instanceof CellsMoved && e.Actor === Actor.Enemy && !e.Pushed && current != null
      && current.Kind === DepictionEventKind.EnemyAction
      && current.Cues.some((c) => c.Kind === CueKind.Hit || c.Kind === CueKind.GuardBlock)) {
      return this.About(this.NewEvent(DepictionEventKind.EnemyAction, current.Title + (e.To < e.From ? '（詰める）' : '（下がる）')), e.Unit);
    }

    // §2.4 hits (#189): the second blow of an enemy face is a beat of its own (二段斬り),
    // opened where the 脆化 its first blow left is spent for it.
    if (e instanceof StatusConsumed && e.Actor === Actor.Player && e.Kind === StatusKind.Fragile
      && current != null && current.Kind === DepictionEventKind.EnemyAction
      && current.Cues.some((c) => c.Kind === CueKind.Slash && c.Source === UnitSide.Enemy)) {
      return this.About(this.NewEvent(DepictionEventKind.EnemyAction, current.Title + '（2 撃目）'), current.Unit);
    }

    if (e instanceof DamageDealt && e.Actor === Actor.Enemy && current != null
      && current.Kind === DepictionEventKind.EnemyAction
      && current.Cues.some((c) => c.Kind === CueKind.Slash && c.Source === UnitSide.Enemy)) {
      return this.About(this.NewEvent(DepictionEventKind.EnemyAction, current.Title + '（2 撃目）'), e.Unit);
    }

    // #50: a 予定変更 is a beat of its own, shown before the second action acts. An enemy read
    // to 0 shows no plan (#455), so its change has no beat and costs no gap between events.
    if (e instanceof PlanChanged && current != null && OmenDisclosure.ShowsPlan(this.Disclosure)) {
      return this.About(this.NewEvent(DepictionEventKind.EnemyAction, DepictionText.PlanChangedTag), e.Unit);
    }

    if (e instanceof OmenSet && e.Decided && this._order > 0) {
      return this.About(this.NewEvent(DepictionEventKind.NextOmen, '次の予兆'), e.Unit);
    }

    return null;
  }

  /** JS extension: `ev` with DepictionEvent.Unit set. (private) */
  About(ev, unit) {
    ev.Unit = unit;
    return ev;
  }

  // ---- one core event → its beats -----------------------------------------------------

  /** (C# private) The type switch: every event type appears once, so the first match is the C#'s case. */
  Apply(e, ev) {
    if (e instanceof GuardCleared) {
      const unit = this.Unit(e.Actor, e.Unit);
      unit.Guard = 0;
      if (e.Cleared > 0) {
        ev.Cues.push(make(Cue, { Kind: CueKind.GuardReset, Target: CoreText.Side(e.Actor), GuardAfter: 0, Unit: holderUnit(e.Actor, e.Unit) }));
      }
    } else if (e instanceof StaminaRecovered) {
      this.SetStamina(ev, e.Actor, e.Unit, e.Amount, e.StaminaAfter);
    } else if (e instanceof StaminaSpent) {
      this.SetStamina(ev, e.Actor, e.Unit, -e.Amount, e.StaminaAfter);
    } else if (e instanceof StaminaGained) {
      this.Grammar(ev, this.SetStamina(ev, e.Actor, e.Unit, e.Amount, e.StaminaAfter), FxSystem.Breath);
    } else if (e instanceof StatusTicked) {
      this.SetStatus(ev, e.Actor, e.Unit, e.Kind, -1, e.StacksAfter);
    } else if (e instanceof StatusApplied) {
      // #205: a ターンで減る型 word stops at its cap, so what landed can be less than what
      // was given, or nothing; the core says how much.
      if (e.Landed > 0) this.Grammar(ev, this.SetStatus(ev, e.Target, e.Unit, e.Kind, e.Landed, e.StacksAfter), FxSystem.Spirit);
    } else if (e instanceof MaxStaminaChanged) {
      // #51 瘴気纏い: the pips lose their top, and what is held is cut to the new maximum.
      const unit = this.Unit(e.Actor, e.Unit);
      const cut = e.StaminaAfter - unit.Stamina;
      unit.StaminaMax = e.MaxAfter;
      this.SetStamina(ev, e.Actor, e.Unit, cut, e.StaminaAfter);
    } else if (e instanceof StatusCleared) {
      // #51 root_sk: the enemy shook the word off.
      this.SetStatus(ev, e.Actor, e.Unit, e.Kind, -e.Stacks, 0);
    } else if (e instanceof Drawn) {
      this._hand.push(e.Card);
      // A hand of six is six core events and one beat on screen.
      let last = ev.Cues.length > 0 ? ev.Cues[ev.Cues.length - 1] : null;
      if (last == null || last.Kind !== CueKind.DrawHand) {
        last = this.Grammar(ev, make(Cue, { Kind: CueKind.DrawHand, Target: UnitSide.Player, Amount: 0 }), FxSystem.Breath);
        ev.Cues.push(last);
      }
      // §3.14 (#455): the core made the draw pile again just before this card; the cards
      // already drawn in this beat came off the old pile.
      if (this._rebuildPending && last.RebuildAfter === Cue.Unchanged) last.RebuildAfter = last.Amount;
      this._rebuildPending = false;
      last.Amount += 1;
    } else if (e instanceof DeckReshuffled) {
      // §3.14 山札切れ (#455): the discard pile is shuffled into the draw pile; the next Drawn is its first card.
      this._rebuildPending = true;
    } else if (e instanceof CardExiled && e.Actor === Actor.Player && ev.Kind === DepictionEventKind.PlayCard) {
      // battle-visual-v1 §4.7 / battle_ui_ux_v2 §5.11 順 4 (#455): the played stance card goes to the exile pile.
      ev.PlayedTo = PileKind.Exile;
    } else if (e instanceof OmenSet) {
      const track = this._tracks[e.Unit];
      const alreadyShown = track.OmenVisible && Equals(track.Omen, e.Omen);
      // Step 12's omen is a new one even when it is the same action: the old number goes.
      if (e.Decided) {
        track.OmenPreviewFor = null;
        track.Plan = null; // #50: the plan of the phase that just ended; a PlanSet follows if the new omen has one.
        track.PlanVisible = false;
        track.PlanChanged = false;
      }
      track.Omen = e.Omen;
      track.OmenVisible = true;
      // Step 5 re-announces the omen step 12 already put up; the screen shows it once.
      if (!alreadyShown) ev.Cues.push(make(Cue, { Kind: CueKind.OmenShow, Target: UnitSide.Enemy, Unit: e.Unit }));
    } else if (e instanceof CardPlayed) {
      CoreScriptWriter.RemoveFirst(this._hand, e.Card);
      this._strike = CoreText.SystemOf(e.Card.Def);
      this._traitLit = false;
      ev.Systems.length = 0;
      ev.Systems.push(...CardSystems.Of(e.Card.Def));
      this._playedPower = -1;
      this._playedUnit = e.Unit;
    } else if (e instanceof PlanSet) {
      // #50: the plan is shown from the omen until the second action acts; a phase that stops
      // at one action (a rest, a stop at the first blow) replaces it with 予定変更.
      const track = this._tracks[e.Unit];
      track.Plan = e.Plan;
      track.PlanVisible = true;
      track.PlanChanged = false;
    } else if (e instanceof PlanChanged) {
      const track = this._tracks[e.Unit];
      track.Plan = e.Now;
      track.PlanVisible = true;
      track.PlanChanged = true;
      // #455: an enemy read to 0 shows no plan (battle_ui_ux_v2 §7), so its change has nothing to pop.
      if (OmenDisclosure.ShowsPlan(this.Disclosure)) {
        ev.Cues.push(make(Cue, { Kind: CueKind.OmenShow, Target: UnitSide.Enemy, Text: DepictionText.PlanChangedTag, Unit: e.Unit }));
      }
    } else if (e instanceof ActionExecuted) {
      if (this._enemyActed) this._tracks[e.Unit].PlanVisible = false; // the second action of the phase spends the plan
      this._enemyActed = true;
      ev.Title = e.Action.Name;
      this._strike = CoreText.SystemOf(e.Action);
      this._traitLit = false;
      ev.Systems.length = 0;
      ev.Systems.push(...CardSystems.Of(e.Action));
      ev.Cues.push(make(Cue, { Kind: CueKind.EnemyWindup, Target: UnitSide.Enemy, System: this._strike, Unit: e.Unit }));
    } else if (e instanceof Rested) {
      ev.Title = '敵は動けない';
    } else if (e instanceof TraitEvaluated) {
      if (e.Outcome.Triggered) this._traitLit = true;
      this.ApplyTrait(ev, e);
    } else if (e instanceof DamageDealt) {
      this.ApplyDamage(ev, e);
    } else if (e instanceof GuardGained) {
      this.Unit(e.Actor, e.Unit).Guard = e.GuardAfter;
      const cue = CoreScriptWriter.GuardGain(e.Actor, e.Amount, e.GuardAfter);
      cue.Unit = holderUnit(e.Actor, e.Unit);
      ev.Cues.push(this.Grammar(ev, cue, FxSystem.Shield));
    } else if (e instanceof CellsMoved) {
      // Whichever side moved, the screen has one thing to show for it: the new N on the
      // player's tag, and the player's figure in the slot N maps to (CoreText.SideOf).
      // Moving the enemy figure waits for the floor cells (#163).
      this.Unit(e.Actor, e.Unit).Cell = e.To;
      const focus = this.FocusUnit;
      const cue = make(Cue, {
        Kind: CueKind.RangeSwitch, Target: UnitSide.Player, Pushed: e.Pushed,
        RangeAfter: CoreText.SideOf(this.Gap), RangeGlyphAfter: CoreText.GapGlyph(this.Gap),
        // battle-visual-v1 §4.2 (#242): where both stand after the move, for the floor.
        PlayerCellAfter: this._player.Cell, EnemyCellAfter: this._tracks[focus].Model.Cell, GapAfter: this.Gap,
        // battle_ui_ux_v2 §5.7 順 1 (#586): the walk takes 160 ms a cell.
        CellsMoved: Math.abs(e.To - e.From),
      });
      // JS extension: the enemy that moved, and where it stands now.
      if (e.Actor === Actor.Enemy) {
        cue.Unit = e.Unit;
        cue.UnitCellAfter = this._tracks[e.Unit].Model.Cell;
        cue.UnitGapAfter = this.GapTo(e.Unit);
      }
      ev.Cues.push(cue);
    } else if (e instanceof WallHit) {
      this.ApplyWallHit(ev, e);
    } else if (e instanceof MoveBlocked) {
      // #335: 呪縛 / 鉤爪 stopped the move; nothing moved, so the line is all there is to show.
      const line = CoreText.MoveBlockedLine(e.By);
      if (line.length > 0) {
        ev.Cues.push(make(Cue, { Kind: CueKind.TraitFire, Target: CoreText.Side(e.Actor), Text: line, Unit: holderUnit(e.Actor, e.Unit) }));
      }
    } else if (e instanceof ActionWhiffed) {
      // The blow finds nobody: the omen's number is struck off, the way a missed side bonus is.
      this._whiffed = true;
      ev.Cues.push(make(Cue, {
        Kind: CueKind.SideBonusMiss, Target: UnitSide.Enemy,
        Amount: this.ShownOmenPower(e.SourceId, e.Unit), Text: '空振り', Unit: e.Unit,
      }));
    } else if (e instanceof ReserveChecked) {
      this.ApplyReserve(ev, e);
    } else if (e instanceof HandDiscarded) {
      this._hand.length = 0;
      if (e.Count > 0) {
        ev.Cues.push(make(Cue, { Kind: CueKind.DiscardHand, Target: UnitSide.Player, Amount: e.Count }));
      }
    }

    // ---- the demo vocabulary (#188) ----

    else if (e instanceof StatusConsumed) {
      this.SetStatus(ev, e.Actor, e.Unit, e.Kind, -1, e.StacksAfter);
    } else if (e instanceof StatusHpChanged) {
      this.SetHp(ev, e.Actor, e.Unit, e.Amount, e.HpAfter, EnumTokens.ToLabel(e.Kind));
    } else if (e instanceof Healed) {
      this.Grammar(ev, this.SetHp(ev, e.Actor, e.Unit, e.Amount, e.HpAfter, '回復'), FxSystem.Breath);
    } else if (e instanceof StaminaBroken) {
      this.SetStamina(ev, e.Target, e.Unit, -e.Amount, e.StaminaAfter);
    } else if (e instanceof Reflected) {
      const struck = this.Unit(e.Target, e.Unit);
      struck.Guard = e.TargetGuardAfter;
      if (e.Absorbed > 0) {
        ev.Cues.push(make(Cue, {
          Kind: CueKind.GuardBlock, Target: CoreText.Side(e.Target),
          Amount: e.Absorbed, GuardAfter: e.TargetGuardAfter, Unit: e.Unit,
        }));
      }
      this.SetHp(ev, e.Target, e.Unit, -e.Damage, e.TargetHpAfter, '見切り');
    } else if (e instanceof StanceSet) {
      this.Unit(e.Actor, e.Unit).Stances.push(e.Name);
      ev.Cues.push(this.Grammar(ev, make(Cue, {
        Kind: CueKind.TraitFire, Target: CoreText.Side(e.Actor), Text: '構え・' + e.Name, Unit: holderUnit(e.Actor, e.Unit),
      }), FxSystem.Form));
    } else if (e instanceof StanceStopped) {
      // #334 (roster §6.4 root_st): one permanent effect stops for a turn, then comes back.
      ev.Cues.push(make(Cue, {
        Kind: CueKind.TraitFire, Target: CoreText.Side(e.Actor),
        Text: CoreText.StanceStoppedLine(CoreScriptWriter.StanceName(e.SourceId, this.DefOf(e.Unit))), Unit: holderUnit(e.Actor, e.Unit),
      }));
    } else if (e instanceof StanceResumed) {
      ev.Cues.push(make(Cue, {
        Kind: CueKind.TraitFire, Target: CoreText.Side(e.Actor),
        Text: CoreText.StanceResumedLine(CoreScriptWriter.StanceName(e.SourceId, this.DefOf(e.Unit))), Unit: holderUnit(e.Actor, e.Unit),
      }));
    }

    // #288: an enemy that falls with others standing is EnemyDefeated; the last one to fall
    // ends the battle instead (TurnLoop.OnFall), and BattleEnded is all that says so — for every
    // enemy whose fall has not been written yet (JS extension: the C# marks the first enemy only).
    else if (e instanceof EnemyDefeated) {
      this.QueueFall(e.Unit);
    } else if (e instanceof BattleEnded && e.Result === GameResult.Won) {
      for (let k = 0; k < this._tracks.length; k++) this.QueueFall(k);
    }

    // The omen is spent once the enemy has acted on it (or rested through it).
    if (e instanceof ActionExecuted || e instanceof Rested) this._tracks[e.Unit].OmenVisible = false;
  }

  /** (C# private) */
  ApplyTrait(ev, trait) {
    const side = CoreText.Side(trait.Actor);
    if (trait.Outcome.Triggered) {
      ev.Cues.push(make(Cue, { Kind: CueKind.TraitFire, Target: side, Text: CoreText.TraitLine(trait.Trait), Unit: holderUnit(trait.Actor, trait.Unit) }));
      return;
    }

    // An enemy blow that reads the gap and found the player outside its threshold: the screen
    // strikes the bonus off the omen badge, so stepping away (or in) visibly paid off. A blow
    // that whiffed has already had its whole number struck off.
    if (!this._whiffed
      && trait.Actor === Actor.Enemy
      && (trait.Trait.Condition === TraitCondition.GapAtLeast
        || trait.Trait.Condition === TraitCondition.GapAtMost)
      && trait.Trait.Effect === TraitEffect.PowerBonus) {
      ev.Cues.push(make(Cue, {
        Kind: CueKind.SideBonusMiss, Target: UnitSide.Enemy,
        Amount: trait.Trait.Amount, Text: '+' + trait.Trait.Amount, Unit: trait.Unit,
      }));
    }
  }

  /**
   * One attack, settled. When Guard swallows part of it — and for every enemy blow — it plays as
   * three beats (swing, shield, wound) so each number has its own moment; otherwise one slash.
   * (C# private) Every beat carries the enemy of the exchange (DamageDealt.Unit).
   */
  ApplyDamage(ev, damage) {
    const source = CoreText.Side(damage.Actor);
    const target = CoreText.Side(damage.Target);
    const unit = this.Unit(damage.Target, damage.Unit);
    unit.Guard = damage.TargetGuardAfter;
    unit.Hp = damage.TargetHpAfter;

    // #248: the held card's number was its whole power before Guard; the played card shows the same sum.
    if (ev.Kind === DepictionEventKind.PlayCard && damage.Actor === Actor.Player && damage.Unit === this._playedUnit) {
      this._playedPower = Math.max(0, this._playedPower) + damage.Raw;
      ev.PreviewText = String(this._playedPower);
    }

    const splits = damage.Absorbed > 0 || damage.Actor === Actor.Enemy;
    if (!splits) {
      ev.Cues.push(make(Cue, {
        Kind: CueKind.Slash, Source: source, Target: target, System: this._strike,
        Fx: EffectGrammar.FromStrike(this._strike), TraitAccent: this._traitLit,
        Amount: damage.Raw, Intensity: EffectStrength.Uniform, HpAfter: damage.TargetHpAfter, Unit: damage.Unit,
      }));
      return;
    }

    ev.Cues.push(make(Cue, {
      Kind: CueKind.Slash, Source: source, Target: target, System: this._strike,
      Fx: EffectGrammar.FromStrike(this._strike), TraitAccent: this._traitLit,
      Amount: damage.Raw, Intensity: EffectStrength.Uniform, Unit: damage.Unit,
    }));
    if (damage.Absorbed > 0) {
      ev.Cues.push(make(Cue, {
        Kind: CueKind.GuardBlock, Target: target,
        Amount: damage.Absorbed, GuardAfter: damage.TargetGuardAfter, Unit: damage.Unit,
      }));
    }
    if (damage.Damage > 0) {
      ev.Cues.push(make(Cue, {
        Kind: CueKind.Hit, Target: target,
        Amount: damage.Damage, Intensity: EffectStrength.Uniform, HpAfter: damage.TargetHpAfter, Unit: damage.Unit,
      }));
    }
  }

  /** §7.3 壁のダメージ: the shield and the wound, like an enemy blow without its swing. (C# private) */
  ApplyWallHit(ev, wall) {
    const target = CoreText.Side(wall.Actor);
    const unit = this.Unit(wall.Actor, wall.Unit);
    unit.Guard = wall.GuardAfter;
    unit.Hp = wall.HpAfter;
    // The wall a card's push drives the enemy into is part of the card's sum (TurnLoop.Preview).
    if (ev.Kind === DepictionEventKind.PlayCard && wall.Actor === Actor.Enemy && wall.Unit === this._playedUnit && this._playedPower >= 0) {
      this._playedPower += wall.Raw;
      ev.PreviewText = String(this._playedPower);
    }
    if (wall.Absorbed > 0) {
      ev.Cues.push(make(Cue, { Kind: CueKind.GuardBlock, Target: target, Amount: wall.Absorbed, GuardAfter: wall.GuardAfter, Unit: wall.Unit }));
    }
    if (wall.Damage > 0) {
      ev.Cues.push(make(Cue, {
        Kind: CueKind.Hit, Target: target,
        Amount: wall.Damage, Intensity: EffectStrength.Uniform, HpAfter: wall.HpAfter, Unit: wall.Unit,
      }));
    }
  }

  /** (C# private) */
  ApplyReserve(ev, reserve) {
    if (reserve.Actor === Actor.Player) {
      this._player.Guard = reserve.GuardAfter;
      if (reserve.GuardGained > 0) {
        ev.Cues.push(make(Cue, {
          Kind: CueKind.StanceCue, Target: UnitSide.Player,
          Amount: reserve.GuardGained, Text: '+' + reserve.GuardGained, GuardAfter: reserve.GuardAfter,
        }));
      }
      return;
    }

    // The enemy's 構え would push its own action past the 2.0 s an event may take (a shove is
    // already windup + swing + shield + wound + the push). It plays with the next omen instead,
    // and the Guard number on screen waits with it.
    if (reserve.GuardGained > 0) this._tracks[reserve.Unit].ReserveHeld = reserve;
  }

  /** JS extension: every enemy's held 構え at the head of `ev`, the first enemy's first. (private) */
  FlushHeldReserves(ev) {
    for (let k = this._tracks.length - 1; k >= 0; k--) this.FlushHeldReserve(ev, k);
  }

  /** (C# private; the unit is the JS extension's) */
  FlushHeldReserve(ev, unit) {
    const track = this._tracks[unit];
    if (track == null || track.ReserveHeld == null) return;
    const reserve = track.ReserveHeld;
    track.ReserveHeld = null;
    track.Model.Guard = reserve.GuardAfter;
    // First in the event: the shield goes up, then the omen it stands behind appears.
    const cue = CoreScriptWriter.GuardGain(Actor.Enemy, reserve.GuardGained, reserve.GuardAfter);
    cue.Unit = unit;
    ev.Cues.unshift(cue);
  }

  /** Writes the pips' cue and hands it back; null when nothing is shown (the enemy's stamina, no change). (C# private) */
  SetStamina(ev, actor, unitIndex, amount, staminaAfter) {
    const unit = this.Unit(actor, unitIndex);
    unit.Stamina = staminaAfter;
    // Only the player's pips play a cue. The enemy's stamina is on its number panel since #455,
    // but it settles with the event's frame: battle_ui_ux_v2 §5.5 順 0 (its pips going by the cost
    // before the blow) is not played yet.
    if (actor !== Actor.Player || amount === 0) return null;
    const cue = make(Cue, {
      Kind: CueKind.StaminaChange, Target: UnitSide.Player,
      Amount: amount, StaminaAfter: staminaAfter, StaminaMax: unit.StaminaMax,
    });
    ev.Cues.push(cue);
    return cue;
  }

  /** (C# private) */
  SetStatus(ev, holder, unitIndex, kind, change, stacksAfter) {
    const unit = this.Unit(holder, unitIndex);
    if (stacksAfter > 0) unit.Statuses.set(kind, stacksAfter);
    else unit.Statuses.delete(kind);
    const cue = make(Cue, {
      Kind: CueKind.StatusChange, Target: CoreText.Side(holder),
      Amount: change, Text: EnumTokens.ToLabel(kind), StacksAfter: stacksAfter, Unit: holderUnit(holder, unitIndex),
    });
    ev.Cues.push(cue);
    return cue;
  }

  /** HP moved without a blow: 出血 / 再生, a heal, a 見切り return. The bar follows the settled value. (C# private) */
  SetHp(ev, actor, unitIndex, amount, hpAfter, cause) {
    this.Unit(actor, unitIndex).Hp = hpAfter;
    if (amount === 0) return null;
    const cue = make(Cue, {
      Kind: CueKind.HpChange, Target: CoreText.Side(actor),
      Amount: amount, HpAfter: hpAfter, Text: cause, Unit: holderUnit(actor, unitIndex),
    });
    ev.Cues.push(cue);
    return cue;
  }

  /**
   * §5.3 (#57): marks a beat that a card or an enemy action plays with its system, at the one
   * strength every beat has (#30). A beat outside a play (a turn start's tick, the reserve's
   * Guard) and a loss (a spent pip, a lost stack) keep none. Hands the cue back, null stays null.
   * (C# private)
   */
  Grammar(ev, cue, system) {
    if (cue == null || !this.InPlay(ev)) return cue;
    const counted = cue.Kind === CueKind.StaminaChange || cue.Kind === CueKind.HpChange || cue.Kind === CueKind.StatusChange;
    if (counted && cue.Amount <= 0) return cue;
    cue.Fx = system;
    cue.Intensity = EffectStrength.Uniform;
    return cue;
  }

  /** The event is a card being played, or the enemy carrying out the action it chose. (C# private) */
  InPlay(ev) {
    return ev.Kind === DepictionEventKind.PlayCard || (ev.Kind === DepictionEventKind.EnemyAction && this._enemyActed);
  }

  /** The display name of a stance's source: a card for the player, an action of the enemy's. (C# private static) */
  static StanceName(sourceId, enemy) {
    if (sourceId == null || sourceId === '') return '';
    if (enemy != null && enemy.Actions.has(sourceId)) return enemy.Actions.get(sourceId).Name;
    for (const def of CardCatalog.All) {
      if (def.Id === sourceId) return def.Name;
    }
    return sourceId;
  }

  /** (C# private static) */
  static GuardGain(actor, amount, guardAfter) {
    return make(Cue, {
      Kind: CueKind.GuardGain, Target: CoreText.Side(actor),
      Amount: amount, Intensity: EffectStrength.Uniform, GuardAfter: guardAfter,
    });
  }

  /** C# List<CardInstance>.Remove: the first element Equals (record equality) to `card`, if any. */
  static RemoveFirst(list, card) {
    const i = list.findIndex((c) => Equals(c, card));
    if (i >= 0) list.splice(i, 1);
    return i >= 0;
  }

  /**
   * JS extension: the enemy a card aimed at one enemy goes to when the screen names none —
   * CoreBattleSource.TargetFor, and the target every number on the card's face is read against.
   * The core takes the target from whoever releases the card (TurnLoop.PlayCard's `target`, §7.4 受け皿で
   * 1 体); this picks the receiver the way a player dropping on "the enemy" means it: among the
   * standing enemies the card can be played on now (TurnLoop.CanPlay), the one it costs least on,
   * the nearest the player (lowest cell) of those; else, among the ones inside its reach
   * (TurnLoop.Preview's InReach), the one it costs least on, the nearest of those; else the nearest
   * standing one (BattleState.Nearest, the enemy the core itself reads when the one named does not
   * stand); 0 when none stands. The cost comes first so that the face, whose cost is read on this
   * enemy, shows the lowest cost the card has on any enemy in reach — the same "any enemy" reading
   * as its lamp (battle-visual-v1 §6.3 決定 6): a lit コスト −1 lamp never sits over an undiscounted
   * cost circle. The cost differs between enemies only through a trait read on the gap (石礫), so in
   * every other case this is the nearest. A card aimed at nobody or at every enemy in reach ignores
   * the target and gets the same fallback. With one enemy this is always 0, which is what the C#
   * source passes.
   */
  static TargetOf(state, instanceId) {
    if (state == null) throw argNull('state');
    const nearest = state.Nearest;
    const fallback = nearest < 0 ? 0 : nearest;
    let card = null;
    for (const c of state.Hand) {
      if (c.InstanceId === instanceId) {
        card = c;
        break;
      }
    }
    if (card == null || !CoreScriptWriter.AimsAtOne(card.Def)) return fallback;
    const line = CoreScriptWriter.LineOf(state);
    const playable = CoreScriptWriter.Cheapest(state, instanceId, line.filter((unit) => TurnLoop.CanPlay(state, instanceId, unit) === PlayRefusal.None));
    if (playable >= 0) return playable;
    const inReach = CoreScriptWriter.Cheapest(state, instanceId, line.filter((unit) => {
      const preview = TurnLoop.Preview(state, instanceId, unit);
      return preview != null && preview.InReach;
    }));
    return inReach >= 0 ? inReach : fallback;
  }

  /**
   * JS extension: of `units` (nearest first), the one card `instanceId` costs least on
   * (TurnLoop.Preview's Cost), the first of them on a tie; −1 when `units` is empty. (private static)
   */
  static Cheapest(state, instanceId, units) {
    let best = -1;
    let bestCost = 0;
    for (const unit of units) {
      const preview = TurnLoop.Preview(state, instanceId, unit);
      const cost = preview != null ? preview.Cost : Number.MAX_SAFE_INTEGER;
      if (best < 0 || cost < bestCost) {
        best = unit;
        bestCost = cost;
      }
    }
    return best;
  }

  /**
   * JS extension: battle-visual-v1 §5.2 スタミナ / §6.3 予測値もその敵で, for each enemy slot the cost
   * the core charges for card `instanceId` played on that enemy now (TurnLoop.Preview's Cost on it).
   * A fallen slot, or a card the state no longer holds, gets `shown` (the face's Cost). Empty for a
   * card that does not aim at one enemy: its cost does not depend on a target. (private static)
   */
  static CostOn(state, card, shown) {
    const on = [];
    if (!CoreScriptWriter.AimsAtOne(card.Def)) return on;
    const living = new Set(state.Living);
    for (let k = 0; k < state.Enemies.length; k++) {
      const preview = living.has(k) ? TurnLoop.Preview(state, card.InstanceId, k) : null;
      on.push(preview != null ? preview.Cost : shown);
    }
    return on;
  }

  /** JS extension: a card the player releases on one enemy (targets one, aimed at the opponent). (private static) */
  static AimsAtOne(def) {
    return def.Targets === TargetKind.One && EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets);
  }

  /** JS extension: the standing enemies, nearest the player first (living enemies never share a cell). (private static) */
  static LineOf(state) {
    return state.Living.sort((a, b) => state.Enemies[a].Body.Cell - state.Enemies[b].Body.Cell);
  }

  /**
   * JS extension: battle-visual-v1 §5.2, for each enemy slot whether `def` can be aimed at that
   * enemy now — it stands and its N is inside the card's reach, the core's own test (Reach.Contains on
   * BattleState.GapTo, as TurnLoop's aim check and Reached read it). Empty for a card aimed at nobody.
   * (private static)
   */
  static ReachOn(state, def) {
    const on = [];
    if (!EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets)) return on;
    const reach = def.Face.ReachOrDefault;
    for (let k = 0; k < state.Enemies.length; k++) on.push(state.Enemies[k].Alive && reach.Contains(state.GapTo(k)));
    return on;
  }

  // ---- frames -------------------------------------------------------------------------

  /** (C# private) */
  NewEvent(kind, title) {
    this._order += 1;
    this._whiffed = false;
    this._rebuildPending = false;
    return make(DepictionEvent, { Order: this._order, Kind: kind, Title: title });
  }

  /** (C# private) */
  Close(ev, after) {
    if (ev == null) return;
    ev.After = this.Snapshot(after);
  }

  /** (C# private) */
  Snapshot(after) {
    const frame = new DepictionFrame();
    frame.Outcome = after.Result === GameResult.Won ? BattleOutcome.Won
      : after.Result === GameResult.Lost ? BattleOutcome.Lost
        : BattleOutcome.Ongoing;
    // Floor and miasma belong to the exploration layer (#99); the chain position is the demo's (#191).
    frame.Corner = make(CornerFrame, { Turn: this._turn, Floor: 1, ChainIndex: this.ChainIndex, ChainTotal: this.ChainTotal, MiasmaPercent: 0 });
    const focus = this.FocusUnit;
    frame.Player = CoreScriptWriter.UnitOf(this._player, true, this.GapTo(focus));
    // JS extension: every enemy and its omen; Frame.Enemy / Frame.Omen are the focus enemy's.
    for (let k = 0; k < this._tracks.length; k++) {
      const track = this._tracks[k];
      // battle-visual-v1 §4.5 (#455): the enemy's stamina is on its number panel (a dot and the number).
      frame.Enemies.push(CoreScriptWriter.UnitOf(track.Model, true, null));
      const omen = this.WithPlan(k, track.OmenVisible ? this.OmenFrameOf(after, k) : make(OmenFrame, { Visible: false }));
      omen.Unit = k;
      omen.Hatch = k;
      frame.Omens.push(omen);
    }
    frame.Enemy = frame.Enemies[focus];
    frame.Omen = frame.Omens[focus];
    for (let k = 0; k < this._tracks.length; k++) {
      const track = this._tracks[k];
      const enemy = frame.Enemies[k];
      // #288: the figure wears the art filed under the enemy's id; the View only looks it up.
      enemy.ArtId = track.Def.Id;
      // §4.5 構えの盾: solid while the stamina it holds meets 構え's threshold (残り 3 以上), the
      // rule's own Combat.ReserveGuard; an elite's panel takes the phosphor frame.
      enemy.ReserveLikely = !track.Model.Down && Combat.ReserveGuard(track.Model.Stamina) > 0;
      enemy.Elite = track.Def.Rank === EnemyRank.Elite;
      // JS extension: which enemy, where it stands, its own N and its hatch.
      enemy.Unit = k;
      enemy.Cell = track.Model.Cell;
      enemy.Size = Math.max(1, track.Model.Size);
      enemy.Gap = this.Fallen(k) ? -1 : this.GapTo(k);
      enemy.GapGlyph = enemy.Gap >= 0 ? CoreText.GapGlyph(enemy.Gap) : '';
      enemy.Hatch = k;
    }
    frame.SwatchesShown = this._tracks.length >= 2;
    // battle-visual-v1 §4.7 (#455): the dashed dots, the core's estimate of the next recovery.
    frame.Player.NextRecovery = TurnLoop.PreviewRecovery(this.AsShown(after));
    frame.Floor = this.FloorOf(after, focus);
    // battle-visual-v1 §4.7 (#455): the piles' counts, as the move left them.
    frame.Piles = make(PileFrame, { Shown: true, Draw: after.DrawPile.length, Discard: after.DiscardPile.length, Exile: after.Exiled.length });
    // battle_ui_ux_v2 §1.1 C4 (#455): what each pile holds, for the list a click on it opens.
    PileCards.Fill(frame.Piles, after);
    // battle-visual-v1 §4.6 (#455): both sides' stance lists, from the stances the models hold.
    frame.StanceListsShown = true;
    for (let k = 0; k < frame.Omens.length; k++) {
      const omen = frame.Omens[k];
      if (omen.Visible) omen.Hit = this.HitMark(k, frame.Floor.Enemies[k].AimCells);
      OmenDisclosure.Apply(omen, this.Disclosure);
    }
    const waiting = this._playerActs && after.Result === GameResult.Ongoing;
    // battle-visual-v1 §4.9 (#455): the attributes the player has played this turn, as the core
    // folds and keeps them (CombatantState.PlayedThisTurn, cleared when the turn ends).
    frame.Played = CoreScriptWriter.PlayedOf(after.Player.PlayedThisTurn);
    // battle_ui_ux_v2 §1.1 B6 (#455): a 追撃 waiting for the next attack face, as the core keeps it.
    frame.Played.FollowUp = after.Player.FollowUp;
    for (const card of this._hand) {
      // JS extension: every number on the face is read against the enemy the card would go to
      // (TargetOf); with one enemy that is 0, the C#'s target.
      const target = CoreScriptWriter.TargetOf(after, card.InstanceId);
      const preview = TurnLoop.Preview(after, card.InstanceId, target);
      const face = CoreText.Face(card, preview, CoreText.ReachesNobody(after, card.InstanceId));
      face.ReachCells = FloorCells.ReachOf(card.Def, this._player.Cell, this._player.Size, after.FieldCells);
      // battle-visual-v1 §6.3 (#242): lit when any enemy in reach satisfies the trait, with each
      // enemy's own verdict for the one a held card frames. A card the state no longer holds
      // (the hand mirrored here runs ahead of it mid-event) keeps the preview's lamp.
      // C# `bool Lamps(state, id, out List<bool> perEnemy)` → `{ ok, value }` (the port's out convention).
      const lamps = CardFaceText.Lamps(after, card.InstanceId);
      const lit = lamps.ok;
      const perEnemy = lamps.value;
      if (preview != null) {
        face.TraitLit = lit;
        face.LitOn = perEnemy;
      }
      // battle-visual-v1 §4.5 (#507): the HP the card would take off each enemy, wall included.
      face.HpLossOn = CardFaceText.HpLoss(after, card.InstanceId);
      // §4.9 / §5.3 (#455): the 構えの予告 once this card's cost is paid, the rule's own
      // Combat.ReserveGuard on what would be left.
      face.StanceHintAfterPay = waiting ? CoreScriptWriter.ReserveHint(this._player.Stamina - face.Cost) : '';
      // JS extension: §5.2's frames per enemy, and the receiver the card goes to unnamed.
      face.ReachOn = CoreScriptWriter.ReachOn(after, card.Def);
      face.TargetUnit = CoreScriptWriter.AimsAtOne(card.Def) ? target : -1;
      // JS extension: §5.2 / §6.3, the cost and the 構えの予告 while the card frames each enemy.
      face.CostOn = CoreScriptWriter.CostOn(after, card, face.Cost);
      face.StanceHintAfterPayOn = face.CostOn.map((cost) => (waiting ? CoreScriptWriter.ReserveHint(this._player.Stamina - cost) : ''));
      frame.Hand.push(face);
    }
    frame.StanceHint = waiting ? CoreScriptWriter.ReserveHint(this._player.Stamina) : '';
    // §5.5 (#455): the screen sinks below the throw line while the enemy has the turn.
    frame.EnemyTurn = this._enemyTurn && after.Result === GameResult.Ongoing;
    frame.StanceDivider = Constants.ReserveThreshold; // §4.7: the stamina tag's divider
    return frame;
  }

  /** 構えの予告 for `stamina` left at the turn end: "+3" when 構え would give Guard, "" when not. (C# private static) */
  static ReserveHint(stamina) {
    const reserve = Combat.ReserveGuard(Math.max(0, stamina));
    return reserve > 0 ? '+' + reserve : '';
  }

  /** §4.9 (#455): the played attributes counted per kind, the last one marked. A card of movement only counts as none. (C# private static) */
  static PlayedOf(played) {
    const frame = make(PlayedFrame, { Shown: true });
    for (const attribute of played) {
      if (attribute === BattleAttribute.None) continue;
      frame.Add(CoreText.KindOf(attribute));
    }
    return frame;
  }

  /**
   * battle-visual-v1 §4.2 (#242): the floor as the models stand — the line's width, both
   * figures' cells, N, and the cells the omen on screen aims at (none while it is hidden).
   * (C# private) JS extension: the same for every enemy (FloorFrame.Enemies); the single-valued
   * members are the focus enemy's.
   */
  FloorOf(after, focus) {
    const shownEnemy = this._tracks[focus].Model;
    const floor = make(FloorFrame, {
      Cells: after.FieldCells,
      PlayerCell: this._player.Cell,
      PlayerSize: Math.max(1, this._player.Size),
      EnemyCell: shownEnemy.Cell,
      EnemySize: Math.max(1, shownEnemy.Size),
      Gap: this.GapTo(focus),
    });
    let shown = null;
    for (let k = 0; k < this._tracks.length; k++) {
      const track = this._tracks[k];
      const enemy = make(FloorEnemy, {
        Unit: k, Hatch: k, Cell: track.Model.Cell, Size: Math.max(1, track.Model.Size),
        Gap: this.Fallen(k) ? -1 : this.GapTo(k), Down: track.Model.Down,
      });
      if (track.OmenVisible && after.Enemies.length > k) {
        enemy.AimCells = FloorCells.AimOf(track.Omen, after.Enemies[k].Body, track.Model.Cell);
        // #455: the thick stripes and the move's frame are the core's, read on the board as shown.
        shown ??= this.AsShown(after);
        enemy.DenseCells = FloorCells.DenseOf(shown, k);
        enemy.MoveCells = FloorCells.MoveOf(shown, k);
      }
      floor.Enemies.push(enemy);
    }
    if (this._tracks[focus].OmenVisible && after.Enemies.length > 0) {
      const aimed = floor.Enemies[focus];
      floor.AimCells = [...aimed.AimCells];
      floor.DenseCells = [...aimed.DenseCells];
      floor.MoveCells = [...aimed.MoveCells];
    }
    return floor;
  }

  /**
   * `after` as the frame being written shows it (#455): the models' cells, the player's
   * stamina and the omens on screen. A move's frames are all read against the state the whole move
   * left, which can be past them — an EndTurn ends on the next omen, after the enemy has moved —
   * so what the core reads for a frame is read on this. (C# private; the C# sets the first enemy only)
   */
  AsShown(after) {
    if (after.Enemies.length === 0) return after;
    let shown = after.with({ Player: after.Player.with({ Cell: this._player.Cell, Stamina: this._player.Stamina }) });
    for (let k = 0; k < after.Enemies.length && k < this._tracks.length; k++) {
      const track = this._tracks[k];
      const unit = after.Enemies[k];
      shown = shown.WithUnit(k, unit.with({ Body: unit.Body.with({ Cell: track.Model.Cell }), Omen: track.Omen ?? unit.Omen }));
    }
    return shown;
  }

  /**
   * battle-visual-v1 §4.4 当たり外れの印 (#242): the core's OmenPreview.Lands whenever the preview
   * read belongs to the omen on screen and is not a rest — it plays the enemy's turn start first
   * (a stage's Sway moves it), so it can differ from the cells drawn now. Otherwise the aimed
   * cells against the player's near edge, as the core counts N (FloorCells.HitOf). None for an
   * omen that aims at nobody. (C# private; per enemy in the JS extension)
   */
  HitMark(unit, aim) {
    const track = this._tracks[unit];
    if (track.Omen == null || track.Omen.Label.Reach == null) return OmenHit.None;
    if (track.OmenPreview != null && Equals(track.OmenPreviewFor, track.Omen) && !track.OmenPreview.Rests) {
      return track.OmenPreview.Lands ? OmenHit.Lands : OmenHit.Misses;
    }
    return FloorCells.HitOf(track.Omen, aim, this._player.Cell, this._player.Size);
  }

  /**
   * The omen badge. Its number is the core's (TurnLoop.PreviewOmen), taken whenever the state
   * read is the player's turn with the shown omen standing; otherwise the last number it was
   * shown with stays, or the face alone before there is one. (C# private; per enemy in the JS
   * extension, each read against its own enemy's actions)
   */
  OmenFrameOf(after, unit) {
    const track = this._tracks[unit];
    if (track.Omen != null && after.Phase === BattlePhase.PlayerAction && after.Enemies.length > unit
      && Equals(after.Enemies[unit].Omen, track.Omen)) {
      track.OmenPreview = TurnLoop.PreviewOmen(after, unit);
      track.OmenPreviewFor = track.Omen;
    }
    return CoreText.OmenOf(track.Omen, track.Def, Equals(track.OmenPreviewFor, track.Omen) ? track.OmenPreview : null);
  }

  /**
   * #50: the 予定 beside the omen: its kind (icon and word), its cells, and for attack and guard
   * the face number (#349). The second action's predicted value comes after this (#242). It
   * outlives the spent first omen. (C# private; per enemy in the JS extension)
   */
  WithPlan(unit, frame) {
    const track = this._tracks[unit];
    if (!track.PlanVisible || track.Plan == null) return frame;
    const kind = track.Plan.Label.Kind;
    const planned = track.Def.Actions.has(track.Plan.ActionId) ? track.Def.Actions.get(track.Plan.ActionId) : null;
    frame.PlanVisible = true;
    frame.PlanIcon = CoreText.IconOf(kind, planned);
    frame.PlanKindLabel = CoreText.OmenKindWord(kind);
    frame.PlanValueText = CoreText.OmenValueOf(kind, planned, null);
    frame.PlanStatuses = CoreText.OmenStatusesOf(kind, planned);
    frame.PlanSideGlyph = track.Plan.Label.Reach != null ? track.Plan.Label.Reach.ToText() : '';
    frame.PlanChanged = track.PlanChanged;
    return frame;
  }

  /** What a whiff strikes off: the number the omen badge was showing, or the face alone. (C# private; per enemy in the JS extension) */
  ShownOmenPower(actionId, unit) {
    const track = this._tracks[unit];
    if (track.OmenPreview != null && track.OmenPreviewFor != null && track.OmenPreviewFor.ActionId === actionId) return track.OmenPreview.RawPower;
    return CoreText.FacePower(getOrThrow(track.Def.Actions, actionId).Face);
  }

  /** (C# private static) `gap` is C# int?: null for none. */
  static UnitOf(unit, showStamina, gap) {
    const hasGap = gap != null;
    const frame = make(UnitFrame, {
      Hp: unit.Hp, HpMax: unit.HpMax, Guard: unit.Guard,
      ShowStamina: showStamina, Stamina: unit.Stamina, StaminaMax: unit.StaminaMax,
      HasRange: hasGap, Down: unit.Down,
    });
    if (hasGap) {
      frame.Range = CoreText.SideOf(gap);
      frame.RangeGlyph = CoreText.GapGlyph(gap);
    }
    // §4: the stances held lead the chips, one chip per stance (a repeat is one chip with its count).
    frame.Statuses.push(...CoreText.StanceChips(unit.Stances));
    // battle-visual-v1 §4.6 (#455): the same stances as the list at the unit's edge.
    frame.Stances = StanceList.Rows(unit.Stances);
    for (const [kind, stacks] of unit.SortedStatuses()) {
      frame.Statuses.push(StatusChipText.Chip(kind, stacks));
    }
    return frame;
  }

  /** (C# private) The player, or enemy `unit` (the C# has one enemy model and ignores the unit). */
  Unit(actor, unit) {
    return actor === Actor.Player ? this._player : this._tracks[unit].Model;
  }

  /** JS extension: enemy `unit`'s def (a stance's name is looked up in it), the first enemy's for a unit the writer does not know. (private) */
  DefOf(unit) {
    return (this._tracks[unit] ?? this._tracks[0]).Def;
  }
}
