// Port of unity-project-kit/Assets/View/Exploration/Script/ExplorationSession.cs
// Holds one run of the exploration screen: the core's state, the few flags the screen needs on top
// of it (the carving, the survey, the battle waiting on a combat node and who is fought there, the
// confirmations, what the top band has open), and the inputs the View forwards. Every rule is the core's: this class only chooses which ExplorationReducer call a click
// becomes, and refuses clicks the screen should not have offered.
//
// C# ulong seeds (Seed, RunSeed, LayerSeed, EncounterSeed) are BigInt here, wrapped to 64 bits.
import { ArgumentOutOfRangeException } from '../../shared/cs.js';
import { ItemCatalogue } from '../../core/dungeonContent/ItemCatalogue.js';
import { ExplorationReducer } from '../../core/dungeon/ExplorationReducer.js';
import { RunPhase, RestChoice } from '../../core/dungeon/ExplorationState.js';
import { SeededRng } from '../../core/dungeon/IRng.js';
import { LayerEncounters } from '../../core/dungeon/LayerEncounters.js';
import { Miasma } from '../../core/dungeon/Miasma.js';
import { NodeKind, NodeKindTokens } from '../../core/dungeon/NodeKind.js';
import { ReliefItem, ReliefKind, RunLoadout } from '../../core/dungeon/RunLoadout.js';
import { SevenLayers } from '../../core/dungeon/SevenLayers.js';
import { EventKind, LayerEvents } from '../../core/dungeon/LayerEvents.js';
import { NoEnemyIntel } from './EnemyIntel.js';
import { EventOutcome, Overlay, ScreenAction, TrialAction } from './ExplorationScreen.js';
import { ChangeLines, ExplorationScreenBuilder } from './ExplorationScreenBuilder.js';
import { ExplorationText } from './ExplorationText.js';
import { TrialItems } from './TrialItems.js';

const U64 = (x) => BigInt.asUintN(64, x);

/** A C# ulong value: a BigInt (or a whole number, converted), wrapped to 64 bits. */
function toUlong(x) {
  return U64(typeof x === 'bigint' ? x : BigInt(x));
}

/**
 * How a run starts. The exploration screen's own stand-ins until 出立 (#104) hands a real one in.
 * C# builds it with an object initializer (`new RunStart { Seed = 3, Hp = 41 }`); here the same
 * members can be passed as one object (`new RunStart({ Seed: 3n, Hp: 41 })`) or assigned after.
 */
export class RunStart {
  /** The battle core's PLAYER_MAX_HP (BattleCore.Constants). #99 hands it to every battle as the maximum. */
  static DefaultMaxHp = 50;

  constructor(init) {
    /** ulong (BigInt). */
    this.Seed = 1n;
    this.MaxHp = RunStart.DefaultMaxHp;
    this.Hp = RunStart.DefaultMaxHp;
    this.Stamina = Miasma.BaseMaxStamina;
    /** The gauge the life starts at. 0 for a fresh life; higher to try the deep end of the gauge. */
    this.MiasmaPercent = 0;
    this.Loadout = RunLoadout.Empty;
    /**
     * JS extension (not in the C#; docs/EXTENSIONS.md 「探索の持ち物とイベント」): the trial rules — every
     * carried item acts (tools_and_consumables_v4.md §2〜§3), found consumables enter the slots
     * (encounters_and_rewards_v4.md §4.3) and イベント nodes resolve (§5). False plays exactly the C#.
     */
    this.Trial = false;
    /**
     * JS extension (trial mode): the layer-two carving was read in an earlier life (docs/INHERITANCE.md R-20), so
     * every life after shows 竜神の秘跡 from its start. Null leaves it to the map seam (CarvingKnownAt).
     */
    this.CarvingKnown = null;
    if (init != null) Object.assign(this, init);
  }

  /**
   * A loadout for trying the screen: 防瘴の面 and two 浄化の香 — the carried items whose
   * effect the exploration core already honours. Amounts come from the catalogue.
   */
  static TrialLoadout() {
    const mask = ItemCatalogue.Tool('bosho_no_men');
    const incense = ItemCatalogue.Consumable('joka_no_ko');
    return new RunLoadout(
      [ReliefItem.Density(mask.Id, mask.Amount)],
      [ReliefItem.Gauge(incense.Id, incense.Amount), ReliefItem.Gauge(incense.Id, incense.Amount)]);
  }
}

const None = -1;

export class ExplorationSession {
  constructor(start = null, intel = null) {
    this._start = start ?? new RunStart();
    this.Intel = intel ?? new NoEnemyIntel();

    /** The seed this life was started from (ulong, BigInt). Each layer's map is drawn from it. */
    this.RunSeed = 0n;
    /** How many lives this session has started, counting the first. */
    this.Life = 0;
    this.State = null;
    /** The layer-two carving has been read, so that layer shows its true name. */
    this.CarvingTaken = false;
    /** This layer's 情報収集 node has been resolved. */
    this.Surveyed = false;
    /** The combat node waiting on its battle's result, or −1. */
    this.PendingBattleNode = None;
    /**
     * Who is fought on PendingBattleNode (encounters_and_rewards_v4.md §2), drawn from the layer's
     * table as the node is reached; null while no battle waits. #99 hands it to the battle.
     */
    this.PendingEncounter = null;
    /** The node whose step waits on a confirmation because it ends the life, or −1. */
    this.PendingStepNode = None;
    this.PendingStepChoice = RestChoice.Rest; // default(RestChoice)
    this.ConfirmingEndLife = false;
    /** A descent waits on a confirmation: the next layer's entry would end the life by 瘴気. */
    this.ConfirmingDescend = false;
    /** HP the last 階層間の休憩 actually gave back, after the maximum clamped it. */
    this.InterludeHealed = 0;
    /** What the top band has open over the screen (dungeon_exploration_v4.md §6.2). */
    this.OpenOverlay = Overlay.None; // default(Overlay)

    // ---- JS extension (trial mode; docs/EXTENSIONS.md 「探索の持ち物とイベント」) ----
    /** The trial rules are on (RunStart.Trial). Every field below stays empty while it is off. */
    this.Trial = this._start.Trial === true;
    /**
     * What keeps what the exploration finds outside its core, or null: Expedition sets itself here
     * (TakeChest(layer, nodeId, contents) → { Cards, Consumables, Pages, PileSpent }, TakeSurvey(enemyId),
     * DisclosureOf(enemyId), EnemyName(enemyId)). Without one a chest gives its max-stamina change only.
     */
    this.Keeper = null;
    /** A 重い宝箱 / 特殊な食事 waiting on 受ける / 受けない: { NodeId, Kind }, or null. */
    this.PendingEvent = null;
    /** Every event of this life (EventOutcome), oldest first — the result cards and the journal read it. */
    this.Events = [];
    /** Consumables found while the three slots are full, waiting on a swap or 「諦める」 (catalogue ids). */
    this.PendingFound = [];
    /** 写しの帳: the enemies the player may pick as this layer's 情報収集 target; empty when none waits. */
    this.SurveyChoices = [];
    /** This layer's 情報収集 target once known: 写しの帳's pick, or the intel's (the layer decides); else null. */
    this.SurveyTarget = null;
    /** The target was the player's pick (写しの帳), not the layer's. */
    this.SurveyPicked = false;
    /** The 標の石 slot being aimed (the map picks where it lands), or −1. */
    this.AimingSlot = None;
    /** A 標の石 jump waiting on the 瘴気死 confirmation: its slot, or −1. */
    this.PendingJumpSlot = None;
    /** The state the last event left (its result card is pending while the state is still this one), or null. */
    this.EventState = null;
    /**
     * The state the last 階層間の休憩 left: the interlude card's own lines (HP, stamina, gauge) read it, so a
     * consumable used at the interlude does not rewrite what the rest gave (§6.5). Null outside trial.
     */
    this.InterludeState = null;
    /**
     * A 習熟訓練 mark waits on its card (the scene's training card is up): set when a rest's 「訓練する」 earns a
     * mark, or by OpenTraining; cleared by PutOffTraining (「あとで」), by spending the last mark, and by leaving the
     * layer. A reload brings the card back while it is set (tier1 R1-16). Always false outside trial.
     */
    this.TrainingWaiting = false;
    /** For each PendingFound entry, the index in Events of the chest it came from, or −1 (a battle's find). */
    this._foundFrom = [];
    this._eventMap = null;
    this._eventKinds = null;

    this.Begin(toUlong(this._start.Seed));
  }

  // ---- JS extension (trial mode): what waits on the player besides the C#'s cards.

  /** A 重い宝箱 or 特殊な食事 waits on 受ける / 受けない. */
  get HasPendingEvent() { return this.PendingEvent !== null; }

  /** A found consumable waits on a slot to swap or on 「諦める」. */
  get HasPendingFound() { return this.PendingFound.length > 0; }

  /** 写しの帳's pick of the 情報収集 target waits. */
  get HasPendingSurvey() { return this.SurveyChoices.length > 0; }

  /** A 標の石 is being aimed. */
  get IsAiming() { return this.AimingSlot !== None; }

  get HasPendingBattle() { return this.PendingBattleNode !== None; }

  get HasPendingStep() { return this.PendingStepNode !== None; }

  /** Nothing waits on the player but the map. (JS extension: nor a trial choice — always none outside trial.) */
  get IsIdle() {
    return !this.HasPendingBattle && !this.HasPendingStep && !this.ConfirmingEndLife && !this.ConfirmingDescend &&
      !this.HasPendingEvent && !this.HasPendingFound && !this.HasPendingSurvey;
  }

  /**
   * The map takes steps: the layer is being explored, and neither a card nor a screen opened
   * from the band covers it. The journal drawer sits beside the map and leaves it live.
   */
  get MapIsLive() { return this.State.Phase === RunPhase.Exploring && this.IsIdle && !this.CoversMap; }

  get CoversMap() { return this.OpenOverlay === Overlay.Cards || this.OpenOverlay === Overlay.Settings; }

  get CanEndLife() { return !this.State.IsOver && this.IsIdle; }

  get CanUseConsumables() { return !this.State.IsOver && this.IsIdle; }

  /** This frame of the screen. */
  Screen() { return ExplorationScreenBuilder.Build(this); }

  /**
   * Walks to a neighbouring node. A step that would end the life by 瘴気 waits for
   * ScreenAction.ConfirmStep instead of being taken. False when the screen should not
   * have offered the step.
   */
  Step(nodeId, choice = RestChoice.Rest) {
    if (!this.MapIsLive) return false;
    if (this.IsAiming) return false; // JS extension: a 標の石 being aimed takes Jump, not a step
    if (!ExplorationReducer.CanStep(this.State, nodeId)) return false;

    const fresh = !this.State.HasResolved(nodeId);
    if (fresh && ExplorationReducer.Step(this.State, nodeId, choice).Phase === RunPhase.MiasmaDeath) {
      this.PendingStepNode = nodeId;
      this.PendingStepChoice = choice;
      return true;
    }

    this.Apply(nodeId, choice);
    return true;
  }

  /**
   * Burns the consumable in this slot. Costs no 刻限.
   * JS extension (trial mode): every consumable acts (ExplorationReducer.UseConsumable), one that would
   * change nothing now is refused (CanUseSlot), and a 標の石 starts aiming instead — the map then offers
   * Jump to the nodes it can land on. Using the aimed slot again stops aiming; using another slot that can
   * be used stops it too. A refused call changes nothing — an aimed 標の石 stays aimed.
   */
  UseConsumable(index) {
    if (!this.CanUseConsumables) return false;
    if (index < 0 || index >= this.State.Loadout.Consumables.length) return false;

    if (this.Trial) {
      if (this.IsAiming && this.AimingSlot === index) {
        this.AimingSlot = None;
        return true;
      }
      if (!this.CanUseSlot(index)) return false;
      this.AimingSlot = None;
      if (this.State.Loadout.Consumables[index].Kind === ReliefKind.Teleport) {
        this.AimingSlot = index;
        return true;
      }
    }
    this.State = ExplorationReducer.UseConsumable(this.State, index);
    return true;
  }

  /**
   * JS extension (trial mode): whether the item in this slot can be used now. 標の石 needs the map live
   * with a 刻限 left and a node to land on; any other item must change something (試作で決めたこと: a use that
   * changes nothing is not offered, so the slots never offer a choice that does nothing). 「Changes something」
   * is the slot's forecast (ExplorationScreenBuilder's ChangeLines) not being empty — one definition for both,
   * so a lit slot always says what it changes.
   */
  CanUseSlot(index) {
    if (!this.CanUseConsumables) return false;
    const item = this.State.Loadout.Consumables[index];
    if (item === undefined) return false;
    if (!this.Trial) return true;
    if (item.Kind === ReliefKind.Teleport) {
      return this.MapIsLive && this.State.Map.Nodes.some((n) => ExplorationReducer.CanTeleport(this.State, n.Id));
    }
    const before = this.State;
    return ChangeLines(before, ExplorationReducer.UseConsumable(before, index)).length > 0;
  }

  /**
   * JS extension (trial mode): 標の石 lands on `nodeId` (tools_and_consumables_v4.md §3: 「同じ層の未踏ノードへ
   * 1 度だけ跳ぶ」, paying the node's 刻限 and 瘴気). A jump that would end the life by 瘴気 waits for
   * ScreenAction.ConfirmStep, like a step. False when no 標の石 is aimed or it cannot land there.
   */
  Jump(nodeId, choice = RestChoice.Rest) {
    if (!this.IsAiming || !this.MapIsLive) return false;
    if (!ExplorationReducer.CanTeleport(this.State, nodeId)) return false;

    const slot = this.AimingSlot;
    if (ExplorationReducer.Teleport(this.State, slot, nodeId, choice).Phase === RunPhase.MiasmaDeath) {
      this.PendingStepNode = nodeId;
      this.PendingStepChoice = choice;
      this.PendingJumpSlot = slot;
      return true;
    }
    this.ApplyJump(slot, nodeId, choice);
    return true;
  }

  /**
   * Spends one 習熟訓練 mark (#625). The card it goes to is named outside the exploration
   * (Exploration.Bridge.Expedition.Train). False when no mark is left or something else waits.
   */
  SpendTrainingMark() {
    if (this.State.IsOver || !this.IsIdle || this.State.TrainingMarks <= 0) return false;
    this.State = ExplorationReducer.SpendTrainingMark(this.State);
    if (this.State.TrainingMarks <= 0) this.TrainingWaiting = false; // JS extension: no mark left to wait on
    return true;
  }

  /**
   * JS extension (trial mode): the training card is opened from the band (「訓練の刻み」), so a reload brings it
   * back like the card a rest's 「訓練する」 opens. False outside trial, with no mark, or while something else waits.
   */
  OpenTraining() {
    if (!this.Trial || this.State.IsOver || !this.IsIdle || this.State.TrainingMarks <= 0) return false;
    this.TrainingWaiting = true;
    return true;
  }

  /**
   * JS extension (trial mode): 「あとで」 — the marks stay on the band until the layer is left (the scene asks
   * again before 次の層へ降りる). False when the card was not waiting.
   */
  PutOffTraining() {
    if (!this.TrainingWaiting) return false;
    this.TrainingWaiting = false;
    return true;
  }

  /**
   * A button on a card, or 「この生を終える」. False when the action does not apply now.
   * JS extension: `action` may also be a TrialAction (trial mode's cards), and `arg` carries what its
   * button names (CardButton.Arg) — the slot of TakeFound, the enemy id of ChooseSurvey. The C#'s
   * actions ignore it.
   */
  Act(action, arg = null) {
    switch (action) {
      case ScreenAction.WinBattle:
        return this.ReportBattle(this.State.Hp, this.State.Stamina);

      case ScreenAction.FallInBattle:
        return this.ReportBattle(0, this.State.Stamina);

      case ScreenAction.GoToInterlude: {
        if (!this.IsIdle) return false;
        if (!ExplorationReducer.CanInterlude(this.State)) return false;
        const hpBefore = this.State.Hp;
        this.State = ExplorationReducer.Interlude(this.State);
        this.InterludeHealed = this.State.Hp - hpBefore;
        if (this.Trial) this.InterludeState = this.State; // JS extension: the rest's own result, for its card
        return true;
      }

      case ScreenAction.Descend: {
        if (!this.IsIdle) return false;
        const below = this.TryDescend();
        if (below == null) return false;
        // Like a step, a descent that ends the life asks first (dungeon_exploration_v4.md §6.5).
        if (below.Phase === RunPhase.MiasmaDeath) {
          this.ConfirmingDescend = true;
          return true;
        }
        this.Descended(below);
        return true;
      }

      case ScreenAction.ConfirmDescend:
        if (!this.ConfirmingDescend) return false;
        this.ConfirmingDescend = false;
        this.Descended(this.TryDescend());
        return true;

      case ScreenAction.AskEndLife:
        if (!this.CanEndLife) return false;
        this.ConfirmingEndLife = true;
        this.AimingSlot = None; // JS extension: the card covers the map, so an aimed 標の石 stops (as Cancel does)
        return true;

      case ScreenAction.ConfirmEndLife:
        if (!this.ConfirmingEndLife) return false;
        this.ConfirmingEndLife = false;
        this.State = ExplorationReducer.EndLife(this.State);
        return true;

      case ScreenAction.ConfirmStep: {
        if (!this.HasPendingStep) return false;
        const node = this.PendingStepNode;
        this.PendingStepNode = None;
        if (this.PendingJumpSlot !== None) this.ApplyJump(this.PendingJumpSlot, node, this.PendingStepChoice);
        else this.Apply(node, this.PendingStepChoice);
        return true;
      }

      case ScreenAction.Cancel:
        if (!this.ConfirmingEndLife && !this.HasPendingStep && !this.ConfirmingDescend && !this.IsAiming) return false;
        this.ConfirmingEndLife = false;
        this.ConfirmingDescend = false;
        this.PendingStepNode = None;
        this.AimingSlot = None; // JS extension: Esc or 「やめる」 also stops aiming a 標の石
        this.PendingJumpSlot = None;
        return true;

      // ---- JS extension (trial mode) ----
      case TrialAction.AcceptEvent:
      case TrialAction.DeclineEvent: {
        if (!this.HasPendingEvent) return false;
        const waiting = this.PendingEvent;
        this.PendingEvent = null;
        this.ResolveEvent(waiting.NodeId, waiting.Kind, action === TrialAction.AcceptEvent);
        return true;
      }

      case TrialAction.TakeFound: {
        if (!this.HasPendingFound) return false;
        const slot = Number(arg);
        const full = !this.State.Loadout.HasFreeSlot;
        if (full && (!Number.isInteger(slot) || slot < 0 || slot >= this.State.Loadout.Consumables.length)) return false;
        const id = this.PendingFound[0];
        this.PendingFound = this.PendingFound.slice(1);
        this._foundFrom = this._foundFrom.slice(1);
        this.State = ExplorationReducer.TakeConsumable(this.State, TrialItems.Consumable(id), full ? slot : -1);
        return true;
      }

      case TrialAction.LeaveFound: {
        if (!this.HasPendingFound) return false;
        const id = this.PendingFound[0];
        const from = this._foundFrom.length > 0 ? this._foundFrom[0] : None;
        this.PendingFound = this.PendingFound.slice(1);
        this._foundFrom = this._foundFrom.slice(1);
        // a chest's consumable given up leaves the event's record (X-9: the record says what was kept)
        if (from !== None) this.TurnDownFind(from, ExplorationText.FoundConsumable(ExplorationText.ItemName(id)));
        return true;
      }

      case TrialAction.ChooseSurvey:
        if (!this.HasPendingSurvey || !this.SurveyChoices.includes(arg)) return false;
        this.SurveyTarget = arg;
        this.SurveyPicked = true;
        this.SurveyChoices = [];
        if (this.Keeper) this.Keeper.TakeSurvey(arg);
        return true;

      case ScreenAction.NewLife:
        if (!this.State.IsOver || !this.IsIdle) return false;
        this.Begin(U64(this.RunSeed + 1n));
        return true;

      default:
        throw new ArgumentOutOfRangeException(`Specified argument was out of the range of valid values. (Parameter 'action')\nActual value was ${action}.`);
    }
  }

  /**
   * The battle on the pending node is over (#99): HP and current stamina come back through the
   * core's ReportBattle, and HP 0 is a fall. The stand-in card's two buttons come here as well.
   * False when no battle waits.
   */
  ReportBattle(hpAfter, staminaAfter) {
    if (!this.HasPendingBattle) return false;
    this.State = ExplorationReducer.ReportBattle(this.State, hpAfter, staminaAfter);
    this.PendingBattleNode = None;
    this.PendingEncounter = null;
    return true;
  }

  /**
   * 所持カード and 設定・セーブ stay shut while the stand-in battle waits, which counts as being in
   * battle (§6.2): a battle plays its deck only, and its state is not part of a save. The journal
   * opens everywhere.
   */
  CanOpen(overlay) {
    return overlay === Overlay.Journal || (overlay !== Overlay.None && !this.HasPendingBattle);
  }

  /**
   * A band button, J or C: opens that screen in place of whichever was open, or closes it when it
   * is the one open. False when it may not open now.
   */
  Toggle(overlay) {
    if (overlay === Overlay.None) return false;
    if (this.OpenOverlay === overlay) {
      this.OpenOverlay = Overlay.None;
      return true;
    }
    if (!this.CanOpen(overlay)) return false;
    this.OpenOverlay = overlay;
    return true;
  }

  /**
   * Esc (§6.2): an open confirmation is answered 「やめる」 first; failing that, the journal or
   * 所持カード closes; only when neither is open does 設定・セーブ open or close.
   */
  Escape() {
    if (this.HasPendingStep || this.ConfirmingDescend || this.ConfirmingEndLife || this.IsAiming) return this.Act(ScreenAction.Cancel);
    if (this.OpenOverlay === Overlay.Journal || this.OpenOverlay === Overlay.Cards) {
      this.OpenOverlay = Overlay.None;
      return true;
    }
    return this.Toggle(Overlay.Settings);
  }

  /**
   * The state the next layer's entry would leave, tried without taking it: the core's own
   * Descend on the immutable state. Null outside the interlude and below the last layer.
   */
  TryDescend() {
    if (this.State.Phase !== RunPhase.Interlude || this.State.Profile.IsLast) return null;
    const next = SevenLayers.Of(this.State.Profile.Layer + 1);
    return ExplorationReducer.Descend(this.State, next, this.MapOf(next, ExplorationSession.LayerSeed(this.RunSeed, next.Layer)));
  }

  /**
   * JS-only seam (not in the C#): a layer's map. RunStart.MapFor(profile, seed), when the game sets
   * one, draws it instead of profile.Map(seed) — the lineage's trace node and the known carving
   * (src/app/life.js, Inheritance.mapSpecFor). Without it this is exactly the C#.
   */
  MapOf(profile, seed) {
    return this._start.MapFor ? this._start.MapFor(profile, seed) : profile.Map(seed);
  }

  /** A layer's map seed, drawn from the life's seed so a life replays from one number. (ulong → BigInt) */
  static LayerSeed(runSeed, layer) {
    return U64(toUlong(runSeed) * 0x9E3779B97F4A7C15n + U64(BigInt(layer)));
  }

  /**
   * The seed a combat node's encounter is drawn from: the layer's map seed and the node, so the
   * same life meets the same enemies on the same nodes. (ulong → BigInt)
   */
  static EncounterSeed(runSeed, layer, nodeId) {
    return U64(ExplorationSession.LayerSeed(runSeed, layer) * 0xBF58476D1CE4E5B9n + U64(BigInt(nodeId + 1)));
  }

  /**
   * JS extension: the seed an イベント node's kind is drawn from — the node's encounter seed, salted so it
   * does not replay the encounter draw (the same rule as Expedition.RewardSeed). (ulong → BigInt)
   */
  static EventSeed(runSeed, layer, nodeId) {
    return ExplorationSession.EncounterSeed(runSeed, layer, nodeId) ^ 0x9E6C63D0676A9A99n;
  }

  /**
   * JS extension: the enemies a layer's table can field (LayerEncounters, normal → elite → boss, each
   * once) — 「対象はこの階層の敵 1 種」 (dungeon_exploration_v4.md §2.1), the list 写しの帳 picks from.
   */
  static LayerEnemies(layer) {
    const table = LayerEncounters.Of(layer);
    const ids = [];
    for (const pool of [table.Normal, table.Elite, table.Boss]) {
      for (const encounter of pool) for (const id of encounter.EnemyIds) if (!ids.includes(id)) ids.push(id);
    }
    return ids;
  }

  /**
   * JS extension (trial mode): which event an イベント node of this layer holds (LayerEvents.Draw), or
   * null for any other node. 「中身は隣接したときに見えます」 — the screen shows it once the node is next door.
   */
  EventAt(nodeId) {
    const map = this.State.Map;
    if (this._eventMap !== map) {
      const layer = this.State.Profile.Layer;
      const runSeed = this.RunSeed;
      this._eventKinds = LayerEvents.Draw(map, layer, (id) => ExplorationSession.EventSeed(runSeed, layer, id));
      this._eventMap = map;
    }
    return this._eventKinds.get(nodeId) ?? null;
  }

  /**
   * JS extension (trial mode): consumables found on the way (a battle's reward, a chest) go into the free
   * slots; what does not fit waits in PendingFound for a swap or 「諦める」 (tools_and_consumables_v4.md §3,
   * encounters_and_rewards_v4.md §4.3). Nothing is taken once the life is over. Called by Expedition.
   */
  Receive(consumableIds, fromEvent = None) {
    if (!this.Trial) return;
    for (const id of consumableIds ?? []) {
      if (this.State.IsOver) return;
      if (this.State.Loadout.HasFreeSlot && !this.HasPendingFound) {
        this.State = ExplorationReducer.TakeConsumable(this.State, TrialItems.Consumable(id));
      } else {
        this.PendingFound = [...this.PendingFound, id];
        this._foundFrom = [...this._foundFrom, fromEvent];
      }
    }
  }

  /**
   * JS extension (trial mode): a chest's find was turned down after all — its card not learned (Expedition.CloseResult)
   * or its consumable given up (LeaveFound) — so the event's record drops that row: the walked node's 「結果」 and the
   * journal's 「出来事」 say what the heir kept (docs/EXTENSIONS.md X-9, W-17). The result card's own lines (what the
   * chest held) stay. Called by the session and the expedition on themselves, never by the scene.
   */
  TurnDownFind(eventIndex, line) {
    const e = this.Events[eventIndex];
    if (!e) return false;
    const at = e.Record.indexOf(line);
    if (at < 0) return false;
    const record = [...e.Record.slice(0, at), ...e.Record.slice(at + 1)];
    const next = new EventOutcome(e.Layer, e.NodeId, e.Kind, e.Name, e.Accepted, e.Lines, e.Cards, e.Consumables, e.Pages, record);
    this.Events = this.Events.map((x, i) => (i === eventIndex ? next : x));
    return true;
  }

  /** JS extension: the jump itself (Jump, or ConfirmStep after the 瘴気死 warning). */
  ApplyJump(slot, nodeId, choice) {
    this.AimingSlot = None;
    this.PendingJumpSlot = None;
    const marks = this.State.TrainingMarks;
    this.State = ExplorationReducer.Teleport(this.State, slot, nodeId, choice);
    if (this.Trial && this.State.TrainingMarks > marks) this.TrainingWaiting = true; // the training card opens
    this.Arrived(nodeId);
  }

  /** JS extension: an event happens — the core's effect, a chest's finds through the Keeper, the record. */
  ResolveEvent(nodeId, kind, accept) {
    const before = this.State;
    const layer = before.Profile.Layer;
    this.State = ExplorationReducer.ResolveEvent(before, kind, accept);
    const after = this.State;

    let found = { Cards: [], Consumables: [], Pages: 0, PileSpent: false };
    if (accept && kind === EventKind.HeavyChest && this.Keeper) {
      found = this.Keeper.TakeChest(layer, nodeId, LayerEvents.ChestContents(layer));
    }

    // The result card's lines, and the shorter record the journal's dungeon page keeps.
    const lines = [];
    const record = [];
    if (!accept) {
      lines.push(ExplorationText.EventDeclined);
      record.push(ExplorationText.EventDeclined);
    } else {
      if (after.Hp !== before.Hp || kind === EventKind.Spring) {
        lines.push(ExplorationText.HpChange(before.Hp, after.Hp));
        record.push(ExplorationText.HpChange(before.Hp, after.Hp));
      }
      if (kind !== EventKind.Spring) {
        lines.push(ExplorationText.MaxStaminaChange(before.MaxStamina, after.MaxStamina));
        record.push(ExplorationText.MaxStaminaChange(before.MaxStamina, after.MaxStamina));
        if (after.Stamina !== before.Stamina) lines.push(ExplorationText.StaminaChange(before.Stamina, after.Stamina));
        if (after.MaxStamina === before.MaxStamina) lines.push(ExplorationText.TempClampNote);
      }
      if (found.Cards.length > 0) {
        lines.push(ExplorationText.FoundCards(found.Cards.length));
        record.push(ExplorationText.RecordCards(1));
      }
      if (found.Pages > 0) {
        lines.push(ExplorationText.FoundPages(found.Pages));
        record.push(ExplorationText.FoundPages(found.Pages));
      }
      for (const id of found.Consumables) {
        lines.push(ExplorationText.FoundConsumable(ExplorationText.ItemName(id)));
        record.push(ExplorationText.FoundConsumable(ExplorationText.ItemName(id)));
      }
      if (found.PileSpent) lines.push(ExplorationText.PileSpent);
    }
    this.Events = [...this.Events, new EventOutcome(layer, nodeId, kind, ExplorationText.EventName(kind), accept === true,
      lines, found.Cards, found.Consumables, found.Pages, record)];
    this.Receive(found.Consumables, this.Events.length - 1);
    this.EventState = this.State;
  }

  /**
   * JS extension (trial mode): whether the life starts knowing the carving (docs/INHERITANCE.md R-20, seven_layers_v4.md
   * §5: 「生をまたぐ。一度拾ったら次の生でも 竜神の秘跡 と表示し、ノードは出ない」). RunStart.CarvingKnown says so when
   * the game passes it. Otherwise the lineage's map seam does: a layer whose spec holds a carving but whose map
   * (RunStart.MapFor) holds none had it taken out because the name is known (Inheritance.mapSpecFor, src/app/life.js)
   * — the node is not offered exactly when the name is known, so the map is the lineage's word for it. False outside
   * trial and without a seam: the C#'s carving is read again each life.
   */
  CarvingKnownAt(seed) {
    if (!this.Trial) return false;
    if (typeof this._start.CarvingKnown === 'boolean') return this._start.CarvingKnown;
    if (!this._start.MapFor) return false;
    for (let layer = 1; layer <= SevenLayers.Count; layer++) {
      const profile = SevenLayers.Of(layer);
      if (!(profile.MapSpec.Quotas.get(NodeKind.Carving) > 0)) continue;
      if (this.MapOf(profile, ExplorationSession.LayerSeed(seed, layer)).CountOf(NodeKind.Carving) === 0) return true;
    }
    return false;
  }

  Begin(seed) {
    this.RunSeed = seed;
    this.Life += 1;
    this.CarvingTaken = this.CarvingKnownAt(seed); // JS extension: false in the C# (and outside trial)
    this.Surveyed = false;
    this.PendingBattleNode = None;
    this.PendingEncounter = null;
    this.PendingStepNode = None;
    this.ConfirmingEndLife = false;
    this.ConfirmingDescend = false;
    this.InterludeHealed = 0;
    // JS extension (trial mode): a new life starts with nothing waiting and no events behind it.
    this.PendingEvent = null;
    this.Events = [];
    this.PendingFound = [];
    this.SurveyChoices = [];
    this.SurveyTarget = null;
    this.SurveyPicked = false;
    this.AimingSlot = None;
    this.PendingJumpSlot = None;
    this.EventState = null;
    this.InterludeState = null;
    this.TrainingWaiting = false;
    this._foundFrom = [];
    this._eventMap = null;
    this._eventKinds = null;

    const first = SevenLayers.Of(1);
    this.State = ExplorationReducer.Enter(
      first,
      this.MapOf(first, ExplorationSession.LayerSeed(seed, first.Layer)),
      this._start.Loadout ?? RunLoadout.Empty,
      this._start.Hp,
      this._start.MaxHp,
      this._start.Stamina,
      this._start.MiasmaPercent,
      0,
      this.Trial);

    // The entry node is resolved on arrival, and it is always a battle (dungeon_exploration_v4.md §4.1).
    this.Arrived(this.State.CurrentNodeId);
  }

  Descended(below) {
    this.State = below;
    this.Surveyed = false;
    this.SurveyTarget = null; // JS extension: the survey's target is per layer, like Surveyed
    this.SurveyPicked = false;
    this.SurveyChoices = [];
    this.AimingSlot = None;
    this.TrainingWaiting = false; // the new layer starts with no mark (ExplorationReducer.Enter)
    this.Arrived(this.State.CurrentNodeId);
  }

  Apply(nodeId, choice) {
    const fresh = !this.State.HasResolved(nodeId);
    const marks = this.State.TrainingMarks;
    this.State = ExplorationReducer.Step(this.State, nodeId, choice);
    if (this.Trial && this.State.TrainingMarks > marks) this.TrainingWaiting = true; // JS extension: the training card opens
    if (fresh) this.Arrived(nodeId);
  }

  /** What resolving a node means for the screen, on top of what the core already did. */
  Arrived(nodeId) {
    const kind = this.State.Map.Node(nodeId).Kind;
    if (kind === NodeKind.Carving) this.CarvingTaken = true;
    if (kind === NodeKind.Survey) this.Surveyed = true;
    // 瘴気死 ends the life on the node before any fight starts.
    if (NodeKindTokens.IsCombat(kind) && this.State.Phase !== RunPhase.MiasmaDeath) {
      this.PendingBattleNode = nodeId;
      const layer = this.State.Profile.Layer;
      this.PendingEncounter = LayerEncounters.Pick(kind, layer, new SeededRng(ExplorationSession.EncounterSeed(this.RunSeed, layer, nodeId)));
    }
    // JS extension (trial mode): an event happens (泉) or waits on 受ける / 受けない; 写しの帳 asks for the
    // survey's target. Like a fight, nothing happens on the node that ended the life by 瘴気.
    if (this.Trial && this.State.Phase !== RunPhase.MiasmaDeath) {
      if (kind === NodeKind.Event) {
        const event = this.EventAt(nodeId);
        if (event !== null && LayerEvents.HasChoice(event)) this.PendingEvent = { NodeId: nodeId, Kind: event };
        else if (event !== null) this.ResolveEvent(nodeId, event, true);
      }
      if (kind === NodeKind.Survey && this.State.Loadout.ChoosesSurvey) {
        const keeper = this.Keeper;
        this.SurveyChoices = ExplorationSession.LayerEnemies(this.State.Profile.Layer)
          .filter((id) => keeper === null || keeper.DisclosureOf(id) < 2);
      } else if (kind === NodeKind.Survey && typeof this.Intel.SurveyTarget === 'function') {
        // 「既定は階層が決め」 (tools_and_consumables_v4.md §2.2): the intel names the layer's target (the
        // journal job's JournalIntel.SurveyTarget, docs/EXTENSIONS.md J-10) and the journal is raised to 2
        // (dungeon_exploration_v4.md §2.1 「手記の開示度を 2 段目へ上げる」), so the battles' omens read it too.
        const target = this.Intel.SurveyTarget(this.State.Profile.Layer, this.State.Map);
        if (target != null) {
          this.SurveyTarget = target;
          if (this.Keeper) this.Keeper.TakeSurvey(target);
        }
      }
    }
  }
}
