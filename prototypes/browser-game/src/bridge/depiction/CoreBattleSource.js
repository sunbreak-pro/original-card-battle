// Port of unity-port/unity-project-kit/Assets/View/Depiction/Bridge/CoreBattleSource.cs
// The battle the v4.3 core runs, offered to the screen as an IDepictionSource. Pure: BattleCore +
// Depiction.Script.
//
// This is the source of every played battle; the only other one is DepictionRunner (the fixed
// script kept for filming). The source that wrote the rules by hand is gone (#571), so the battle
// rules live in BattleCore alone. This class holds no rule of its own either: TurnLoop moves
// the battle, CoreScriptWriter turns what happened into screen events, and this class only decides
// which of the two the screen is waiting for. It does not read DemoDeck.
//
// JS extension — several enemies (docs/port-notes/depiction-multi-enemy.md). The C# source never
// names a target, so TurnLoop reads its default 0: the first enemy, also once it has fallen (and then
// every card aimed at one enemy is refused). Here Inspect, TryPlay, PreviewFor and RefusalText take the
// enemy the card is released on as an optional last argument (its index in the core's enemy list,
// as DepictionFrame.Enemies[k].Unit), and without one the card goes where TargetFor sends it — the
// target every number on its face was read against. With one enemy both are 0, as in the C#.
//
// JS extension — 俊敏's free cell (battle_core_v4.md §5 #48, battle_ui_ux_v2 §3.8 / §5.1 順 7 / §5.7 順 10).
// The core opens the step at the turn start (TurnLoop.CanTakeFreeStep / TakeFreeStep), but the C# source
// offers the screen no way to take it, so Unity lets it go. Here CanTakeFreeStep(dir) (read-only) and
// TakeFreeStep(dir) do: the step's core events go through the same writer as every other player move
// (its Apply and Close, in one screen event of their own), and TakeFreeStep returns that event as EndTurn
// returns its first. The event's Kind is FreeStepKind, a JS-extension kind outside the C# enum: none of
// the C# kinds fits (PlayCard waits for a card's release, TurnStart for its banner), and with this one
// EffectPlan lists only the cues' own steps (the walk and the gap's recount, or a MoveBlocked line).
// A source that never calls them behaves exactly as the C#.
import { ArgumentNullException, ArgumentOutOfRangeException, InvalidOperationException } from '../../shared/cs.js';
import { Actor, BattlePhase, GameResult } from '../../core/battle/Types.js';
import { Field } from '../../core/battle/Field.js';
import { BattleSetup, PlayRefusal, TurnLoop } from '../../core/battle/TurnLoop.js';
import { SeededRng } from '../../core/battle/IRng.js';
import { DepictionText, PlayVerdict } from '../depictionScript/DepictionTypes.js';
import { OmenDisclosure } from '../depictionScript/OmenDisclosure.js';
import { CoreScriptWriter } from './CoreScriptWriter.js';
import { CoreText } from './CoreText.js';

/** new ArgumentNullException(nameof(x)) with .NET's message. */
function argNull(name) {
  const e = new ArgumentNullException(`Value cannot be null. (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

/** new ArgumentOutOfRangeException(paramName, actualValue, message) with .NET's message. */
function argRange(name, value, message) {
  const e = new ArgumentOutOfRangeException(`${message} (Parameter '${name}')\nActual value was ${value}.`);
  e.ParamName = name;
  e.ActualValue = value;
  return e;
}

/** @implements {IDepictionSource} */
export class CoreBattleSource {
  /** The gap from which the unattended run stops stepping back (SuggestedCardId). */
  static FleeGap = 2;

  /** JS extension: DepictionEvent.Kind of the free step's event (not a member of the C# DepictionEventKind). */
  static FreeStepKind = 'FreeStep';

  /** JS extension: the free step event's title (the status's name, battle_core_v4.md §5). */
  static FreeStepTitle = '俊敏';

  /**
   * @param setup Who fights whom, with which deck.
   * @param seed Fixes every shuffle, so the same seed replays the same battle here and under test.
   * @param suggestCards Unattended runs only (captures, the PlayMode test): suggest the leftmost card
   *   that can be paid for, and nothing once none can. A played battle leaves the choice to the player.
   * @param stopAfterTurns Unattended runs only: report Finished once this many turns have closed on
   *   their next omen. 0 = fight to the end.
   * @param chainIndex §12 連戦 (#191): which battle of the chain this is (1-based), for the corner of the screen.
   * @param chainTotal How many battles the chain holds; 1 for a battle on its own.
   * @param disclosure battle-visual-v1 §4.4 (#455): the journal's 開示度 for this enemy (0〜2); below 2
   *   the omen shows no numbers. Read once here, as the battle starts (#500): BattleLaunch and
   *   DemoSession hand it in.
   */
  constructor(setup, seed, suggestCards = false, stopAfterTurns = 0, chainIndex = 1, chainTotal = 1, disclosure = OmenDisclosure.Full) {
    if (setup == null) throw argNull('setup');
    if (stopAfterTurns < 0) throw argRange('stopAfterTurns', stopAfterTurns, '0 or more.');
    /** @private readonly Queue<DepictionEvent> */
    this._pending = [];
    /** @private readonly List<BattleEvent> */
    this._history = [];
    this._suggestCards = suggestCards;
    this._stopAfterTurns = stopAfterTurns;
    this._rng = new SeededRng(seed);
    this._writer = new CoreScriptWriter(setup.Enemy);
    this._writer.ChainIndex = chainIndex;
    this._writer.ChainTotal = chainTotal;
    this._writer.Disclosure = disclosure;
    const start = TurnLoop.Start(setup, this._rng);
    this._state = start.State;
    this._history.push(...start.Events);
    /** public DepictionFrame Frame { get; private set; } */
    this.Frame = this._writer.Opening(this._state);
  }

  /** The slice: the polearm against the ten-kind prototype deck. */
  static Slice(seed) {
    return new CoreBattleSource(BattleSetup.Slice(), seed);
  }

  /** The core state behind the screen. For tests and tools; the View never reads it. */
  get State() { return this._state; }

  /** Every core event of the battle so far, from TurnLoop.Start on. The end screen counts from it (Chain.Tally, #191). */
  get History() { return this._history; }

  get Finished() {
    return this._pending.length === 0 && (this._state.Result !== GameResult.Ongoing || this.ReachedTurnLimit);
  }

  get WaitingForPlayer() {
    return this._pending.length === 0 && this._state.Result === GameResult.Ongoing && this._state.Phase === BattlePhase.PlayerAction;
  }

  /** The turn limit is only looked at between turns, so a limited run still ends on a next omen. (C# private) */
  get ReachedTurnLimit() {
    return this._stopAfterTurns > 0 && this._state.Phase === BattlePhase.AwaitingTurnStart && this._state.Turn >= this._stopAfterTurns;
  }

  get CanEndTurn() { return this.WaitingForPlayer; }

  /**
   * A played battle has no opinion about which card to drag; an unattended run takes the
   * leftmost payable one — except a card that steps back while the gap is already 2 or more
   * (#192). Backing off to the end of the line puts the player out of every reach of 大黒蛇
   * セルク, which never moves and binds the feet every phase, and the run would never end (#196).
   * JS extension: the gap is the nearest standing enemy's (the C# reads the first enemy's, also
   * once it has fallen), and a card is payable on the enemy TargetFor sends it to.
   */
  get SuggestedCardId() {
    if (!this._suggestCards || !this.WaitingForPlayer) return '';
    return this.AutoCardId;
  }

  /**
   * JS extension: SuggestedCardId's choice whether or not this source was built to suggest — for the game's
   * ?autobattle test switch, which also fights a fight a reload brings back: that source is replayed as the
   * player's page built it (suggestCards false), and with SuggestedCardId alone the run only ended turns.
   * Read-only (a getter: the save recorder logs no read).
   */
  get AutoCardId() {
    if (!this.WaitingForPlayer) return '';
    for (const card of this._state.Hand) {
      if (card.Def.Face.Move < 0 && this.NearestGap >= CoreBattleSource.FleeGap) continue;
      if (TurnLoop.CanPlay(this._state, card.InstanceId, this.TargetFor(card.InstanceId)) === PlayRefusal.None) return card.InstanceId;
    }
    return '';
  }

  /** JS extension: N to the nearest standing enemy (BattleState.Gap, the first enemy's, when none stands). (private) */
  get NearestGap() {
    const nearest = this._state.Nearest;
    return nearest < 0 ? this._state.Gap : this._state.GapTo(nearest);
  }

  /**
   * JS extension: the enemy (its index in the core's enemy list) a card released on the receiver
   * goes to when the screen names none, and the one its face's cost and preview are read against
   * (CoreScriptWriter.TargetOf): of the standing enemies it can be played on now, the one it costs
   * least on (the nearest on a tie); else the same among the ones inside its reach; else the nearest
   * standing one. A card aimed at nobody or at every enemy in reach ignores it. Always 0 in a
   * one-enemy battle.
   */
  TargetFor(cardId) {
    return CoreScriptWriter.TargetOf(this._state, cardId);
  }

  get GuideText() {
    if (this._pending.length > 0) return '';
    // The outcome is the end screen's to show (#191); the guide line says nothing once the battle is over.
    if (this._state.Result !== GameResult.Ongoing) return '';
    if (this.ReachedTurnLimit) return '';
    if (!this.WaitingForPlayer) return '';
    return this.HasPlayableCard()
      ? '出したい札を離してください。手が済んだら「ターン終了」'
      : '出せる札がありません。「ターン終了」を押してください';
  }

  AdvanceAuto() {
    if (this._pending.length === 0) {
      if (this.Finished) throw new InvalidOperationException('The battle is over.');
      if (this._state.Phase !== BattlePhase.AwaitingTurnStart) throw new InvalidOperationException('The turn waits for the player.');
      this.Take(TurnLoop.BeginPlayerTurn(this._state, this._rng));
    }
    return this.Next();
  }

  EndTurn() {
    if (!this.CanEndTurn) throw new InvalidOperationException('The turn cannot be ended right now.');
    this.Take(TurnLoop.EndTurn(this._state, this._rng));
    return this.Next();
  }

  /**
   * JS extension (see the head of the file): whether the player may take 俊敏's free cell now — the battle
   * waits for the player and the core holds the step open (it opens at the turn start and goes with the
   * first card played or the turn's end). With `dir` (+1 forward, −1 back), also whether the line has a
   * cell that way: the move the core would make (Field.Move) changes the player's cell. Only the line
   * closes a way (the end of the line, the nearest standing enemy): 呪縛, 鈍足 and 鉤爪 do not, the core
   * takes the step and answers with MoveBlocked (battle_ui_ux_v2 §3.8: the arrow is shown under 呪縛 too).
   * Read-only; any other `dir` is false.
   */
  CanTakeFreeStep(dir) {
    if (!this.WaitingForPlayer || !TurnLoop.CanTakeFreeStep(this._state)) return false;
    if (dir === undefined || dir === null) return true;
    if (dir !== 1 && dir !== -1) return false;
    const shift = Field.Move(this._state, Actor.Player, dir);
    return shift.To !== shift.From;
  }

  /**
   * JS extension (see the head of the file): the player takes 俊敏's free cell, `dir` +1 forward or −1 back
   * (a plain number, so the save recorder can log the call). Returns the screen event the step plays as:
   * the walk of one cell (a RangeSwitch cue), or what stopped it (a MoveBlocked line: 呪縛 / 鉤爪), or
   * nothing to show (鈍足, the line). Staying needs no call: the first card or the turn's end lets the step go.
   * Throws when the step cannot be taken now (CanTakeFreeStep() is false) or `dir` is not ±1.
   */
  TakeFreeStep(dir) {
    if (!this.CanTakeFreeStep()) throw new InvalidOperationException('The free step cannot be taken right now.');
    if (dir !== 1 && dir !== -1) throw argRange('dir', dir, 'The free step is one cell: +1 (forward) or -1 (back).');
    const step = TurnLoop.TakeFreeStep(this._state, dir);
    this._state = step.State;
    this._history.push(...step.Events);
    this._pending.push(this.WriteFreeStep(step.Events));
    return this.Next();
  }

  /**
   * JS extension: the free step's core events as one screen event, written by the writer as it writes any
   * move of the player's turn (its Apply per core event, its Close for the settled frame). The writer's own
   * Write opens a screen event only at a turn start, a card or an enemy beat, and would drop these. (private)
   */
  WriteFreeStep(events) {
    const writer = this._writer;
    writer.EnsureTracks(this._state);
    const ev = writer.NewEvent(CoreBattleSource.FreeStepKind, CoreBattleSource.FreeStepTitle);
    for (const e of events) writer.Apply(e, ev);
    writer.Close(ev, this._state);
    return ev;
  }

  /**
   * JS extension: `target` is the enemy the card would be released on (its index in the core's
   * enemy list); omitted, TargetFor's. A target that is not a standing enemy answers NotWaiting, as
   * any refusal the screen has no word for (the screen offers no receiver on a fallen enemy).
   */
  Inspect(cardId, target) {
    if (!this.WaitingForPlayer) return PlayVerdict.NotWaiting;
    switch (TurnLoop.CanPlay(this._state, cardId, target ?? this.TargetFor(cardId))) {
      case PlayRefusal.None: return PlayVerdict.Accepted;
      case PlayRefusal.NotInHand: return PlayVerdict.NotInHand;
      case PlayRefusal.NotEnoughStamina: return PlayVerdict.NotEnoughStamina;
      case PlayRefusal.OutOfReach: return PlayVerdict.OutOfRange;
      // #335: 呪縛 has a reason of its own to give, not the silent "not now".
      case PlayRefusal.Bound: return PlayVerdict.Bound;
      default: return PlayVerdict.NotWaiting;
    }
  }

  /**
   * C#: `PlayVerdict TryPlay(string cardId, DropZone zone, out DepictionEvent played)`.
   * The out parameter follows the port's convention: returns `{ ok, value }` where `ok` is the
   * PlayVerdict and `value` the played DepictionEvent (null unless Accepted). The same two are also
   * given as `verdict` and `played`.
   * JS extension: `target`, the enemy the card is released on (its index in the core's enemy list);
   * omitted, TargetFor's.
   * @returns {{ ok: string, value: object|null, verdict: string, played: object|null }}
   */
  TryPlay(cardId, zone, target) {
    let played = null;
    const result = (verdict) => ({ ok: verdict, value: played, verdict, played });
    const aimed = target ?? this.TargetFor(cardId);
    const verdict = this.Inspect(cardId, aimed);
    if (verdict !== PlayVerdict.Accepted) return result(verdict);

    const face = DepictionText.Find(this.Frame.Hand, cardId);
    if (face == null) return result(PlayVerdict.NotInHand);
    if (zone !== DepictionText.RequiredZone(face.Aim)) return result(PlayVerdict.WrongZone);

    this.Take(TurnLoop.PlayCard(this._state, cardId, this._rng, aimed));
    played = this.Next();
    return result(PlayVerdict.Accepted);
  }

  /**
   * The one number shown while a card is held: for an attack, the whole card's power before
   * the enemy's Guard (#248: every blow and the wall, as one sum); else the Guard it gives.
   * The number sits beside the power icon, and what gets past the Guard is the HP bar's
   * hatching, not this (battle-visual-v1.md §5.2, §8).
   * JS extension: `target`, the enemy a held card frames (its index in the core's enemy list), the
   * one the number is read on; omitted, TargetFor's — the target TryPlay would use.
   */
  PreviewFor(cardId, target) {
    const aimed = target ?? this.TargetFor(cardId);
    if (this.Inspect(cardId, aimed) !== PlayVerdict.Accepted) return '';
    const preview = TurnLoop.Preview(this._state, cardId, aimed);
    if (preview == null) return '';
    if (preview.Attacks) return String(preview.RawPower);
    return preview.GuardGain > 0 ? String(preview.GuardGain) : '';
  }

  /**
   * The battle-visual-v1 §5.4 出せなかった理由 line: why a released card went back to the hand.
   * JS extension for several enemies: `target` is the enemy the card was released on (its index in the
   * core's enemy list); omitted, the one TryPlay sends it to (CardFace.TargetUnit). Both numbers the line
   * quotes are that enemy's, as the frame gives them: the cost of the card on it
   * (CardFace.CostFor — 石礫 costs 1 less on an enemy at N 2 or more) and its N
   * (DepictionFrame.Enemies[k].GapGlyph; Player.RangeGlyph for a card aimed at nobody or an enemy
   * that does not stand). OutOfRange is written whenever it is asked for, also while another enemy
   * is inside the card's reach: §5.1's "どの敵にも届かないときだけ" is the hover panel's rule
   * (CardFace.ReachHint), not this line's. A one-enemy battle answers exactly as the C#.
   */
  RefusalText(cardId, verdict, target) {
    const face = DepictionText.Find(this.Frame.Hand, cardId);
    if (face == null) return '';
    switch (verdict) {
      case PlayVerdict.NotEnoughStamina:
        return '「' + face.Name + '」はスタミナ ' + (target == null ? face.Cost : face.CostFor(target)) + ' が要ります。残りは ' + this.Frame.Player.Stamina + ' です';
      case PlayVerdict.OutOfRange:
        return '「' + face.Name + '」は間合い ' + face.RequiredRangeGlyph + ' の相手にしか出せません。いまは ' + this.ShownGapGlyph(target ?? face.TargetUnit) + ' です';
      case PlayVerdict.WrongZone:
        return DepictionText.WrongZoneText(face);
      case PlayVerdict.Bound:
        return CoreText.BoundRefusal(face.Name);
      default:
        return '';
    }
  }

  /**
   * JS extension: the N of enemy `unit` as the frame shows it (its GapGlyph), or Player.RangeGlyph
   * (the focus enemy's, the C#'s) when `unit` is −1 or does not stand. (private)
   */
  ShownGapGlyph(unit) {
    const enemy = unit >= 0 ? this.Frame.Enemies.find((u) => u.Unit === unit) : null;
    return enemy != null && enemy.Gap >= 0 ? enemy.GapGlyph : this.Frame.Player.RangeGlyph;
  }

  /** (C# private) */
  Take(step) {
    this._state = step.State;
    this._history.push(...step.Events);
    for (const ev of this._writer.Write(step.Events, this._state)) this._pending.push(ev);
  }

  /** (C# private) Queue.Dequeue throws on an empty queue. */
  Next() {
    if (this._pending.length === 0) throw new InvalidOperationException('Queue empty.');
    const ev = this._pending.shift();
    this.Frame = ev.After;
    return ev;
  }

  /** (C# private) JS extension: each card on the enemy TargetFor sends it to. */
  HasPlayableCard() {
    for (const card of this._state.Hand) {
      if (TurnLoop.CanPlay(this._state, card.InstanceId, this.TargetFor(card.InstanceId)) === PlayRefusal.None) return true;
    }
    return false;
  }
}
