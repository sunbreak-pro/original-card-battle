// Port of unity-project-kit/Assets/View/Exploration/Script/ExplorationScreen.cs
// What the exploration screen shows, as plain values (dungeon_exploration_v4.md §6). The View puts
// these on screen and computes nothing: every number and line of text here was read from the
// exploration core by ExplorationScreenBuilder. Positions are fractions of the map area, so the
// View only scales them.
import { defineEnum } from '../../shared/cs.js';
import { RunPhase } from '../../core/dungeon/ExplorationState.js';

/**
 * Where a node stands relative to the player (dungeon_exploration_v4.md §6.3).
 * - Current: the player stands here.
 * - Reachable: next door and not yet resolved, with 刻限 left: entering costs one.
 * - Revisit: next door and already resolved: walking back is free.
 * - Resolved: resolved, not next door.
 * - Far: not resolved, not next door. Only the kind shows.
 */
export const NodeStanding = defineEnum(['Current', 'Reachable', 'Revisit', 'Resolved', 'Far']);

/**
 * What a button on the screen asks the session to do.
 * - WinBattle: the stand-in battle: report it won, HP and stamina unchanged (§6.8).
 * - FallInBattle: the stand-in battle: report it lost, HP 0 (§6.8).
 * - AskEndLife: opens the confirmation for 「この生を終える」 (§6.6).
 * - ConfirmStep: takes the step that was waiting on a confirmation (瘴気死 ahead).
 * - ConfirmDescend: descends although the next layer's entry ends the life by 瘴気.
 * - Cancel: closes a confirmation without doing anything.
 * - NewLife: starts a fresh run from layer one with a new seed (§6.9).
 * (The trial mode's buttons are TrialAction's; this enum keeps the C# members and order.)
 */
export const ScreenAction = defineEnum([
  'WinBattle',
  'FallInBattle',
  'GoToInterlude',
  'Descend',
  'AskEndLife',
  'ConfirmEndLife',
  'ConfirmStep',
  'ConfirmDescend',
  'Cancel',
  'NewLife',
]);

/**
 * JS extension (trial mode, docs/EXTENSIONS.md 「探索の持ち物とイベント」): the buttons of the trial cards,
 * answered by ExplorationSession.Act(action, arg) like ScreenAction's. A separate enum so ScreenAction keeps
 * the C#'s members and order.
 * - AcceptEvent / DeclineEvent: 「受ける / 受けない」 on a 重い宝箱 or 特殊な食事 (encounters_and_rewards_v4.md §5).
 * - TakeFound: a found consumable takes the slot `arg` (0〜2) when the three are full (§4.3).
 * - LeaveFound: 「諦める」 — the found consumable is left behind (tools_and_consumables_v4.md §3).
 * - ChooseSurvey: 写しの帳 — the enemy id `arg` is the 情報収集 target (tools_and_consumables_v4.md §2.2).
 */
export const TrialAction = defineEnum(['AcceptEvent', 'DeclineEvent', 'TakeFound', 'LeaveFound', 'ChooseSurvey']);

/** Which card sits over the map, if any. (Exploration.CardKind, not Depiction's.) */
export const CardKind = defineEnum([
  'None',
  'Battle',
  'ConfirmStep',
  'ConfirmDescend',
  'ConfirmEndLife',
  'LayerCleared',
  'PushedOut',
  'Interlude',
  'MiasmaDeath',
  'Fallen',
  'Completed',
  'Survived',
]);

/**
 * JS extension (trial mode): the cards over the map that wait on a choice — EventChoice (受ける / 受けない),
 * Swap (a found consumable and full slots), SurveyChoice (写しの帳's pick of the 情報収集 target). A
 * ScreenCard's Kind is one of CardKind's or one of these; a separate enum keeps CardKind the C#'s.
 */
export const TrialCardKind = defineEnum(['EventChoice', 'Swap', 'SurveyChoice']);

/**
 * What the top band's buttons open over the screen (dungeon_exploration_v4.md §6.2), one at a
 * time. The journal is a drawer beside the map; the other two cover it.
 * - Journal: the journal drawer: the band's button or J.
 * - Cards: the cards the character holds, read only: the band's button or C.
 * - Settings: settings and 「保存してゲームを閉じる」: the band's gear or Esc.
 */
export const Overlay = defineEnum(['None', 'Journal', 'Cards', 'Settings']);

/** The run's numbers before and after entering one node, straight from ExplorationReducer.Step. */
export class StepForecast {
  constructor(before, after, lines, warnings) {
    this.TimeBefore = before.TimeLeft;
    this.TimeAfter = after.TimeLeft;
    this.MiasmaBefore = before.MiasmaPercent;
    this.MiasmaAfter = after.MiasmaPercent;
    this.MaxStaminaBefore = before.MaxStamina;
    this.MaxStaminaAfter = after.MaxStamina;
    this.StaminaBefore = before.Stamina;
    this.StaminaAfter = after.Stamina;
    this.HpBefore = before.Hp;
    this.HpAfter = after.Hp;
    this.PhaseAfter = after.Phase;
    /** 「刻限 7 → 6」 and the like, one per value that changes. */
    this.Lines = lines;
    /** A 20% line crossed, 瘴気死, or being pushed out (§6.4). */
    this.Warnings = warnings;
  }

  get IsLethal() { return this.PhaseAfter === RunPhase.MiasmaDeath; }

  toJSON() {
    return {
      $type: 'StepForecast',
      TimeBefore: this.TimeBefore,
      TimeAfter: this.TimeAfter,
      MiasmaBefore: this.MiasmaBefore,
      MiasmaAfter: this.MiasmaAfter,
      MaxStaminaBefore: this.MaxStaminaBefore,
      MaxStaminaAfter: this.MaxStaminaAfter,
      StaminaBefore: this.StaminaBefore,
      StaminaAfter: this.StaminaAfter,
      HpBefore: this.HpBefore,
      HpAfter: this.HpAfter,
      PhaseAfter: this.PhaseAfter,
      Lines: this.Lines,
      Warnings: this.Warnings,
      IsLethal: this.IsLethal,
    };
  }
}

/** One way into a node: 「進む」, 「休む」, 「訓練する」 or 「移る」. */
export class StepOption {
  constructor(label, choice, forecast, jump = false) {
    this.Label = label;
    this.Choice = choice;
    /** Null for walking back over a resolved node: nothing changes but where the player stands. */
    this.Forecast = forecast;
    /**
     * JS extension (trial mode): the way in is 標の石's jump (ExplorationSession.Jump), not a step
     * (tools_and_consumables_v4.md §3).
     */
    this.Jump = jump === true;
  }

  /** Stepping here ends the life, so the button asks first. */
  get NeedsConfirm() { return this.Forecast != null && this.Forecast.IsLethal; }

  toJSON() {
    return { $type: 'StepOption', Label: this.Label, Choice: this.Choice, Forecast: this.Forecast, NeedsConfirm: this.NeedsConfirm };
  }
}

/** One node on the map. */
export class NodeCard {
  constructor(node, x, y, glyph, kindLabel, standing, lines, options, note, title, detail) {
    this.Id = node.Id;
    this.Row = node.Row;
    this.Column = node.Column;
    this.Kind = node.Kind;
    /** 0 = left edge of the map area, 1 = right edge. (C# float: a float32 value.) */
    this.X = x;
    /** 0 = bottom of the map area, 1 = top. The entry sits on top, the boss at the bottom. (C# float.) */
    this.Y = y;
    /** The one-character stand-in for the node's icon (戦・精・主・休・調・事・痕・彫). */
    this.Glyph = glyph;
    this.KindLabel = kindLabel;
    this.Standing = standing;
    /** What is inside. Empty for a far node, which shows its kind only (§6.3). */
    this.Lines = lines;
    /** The buttons the detail panel offers. Empty unless the node can be walked to now. */
    this.Options = options;
    /** A short line on where the node stands (「入ると刻限を 1 使う」 and the like). */
    this.Note = note;
    /** The detail panel's heading: the glyph and the kind. */
    this.Title = title;
    /**
     * The detail panel's text, line by line: the note, what is inside, then one line per way in
     * with its forecast (「【休む】刻限 9 → 8　瘴気 1% → 2%」) and its warnings.
     */
    this.Detail = detail;
  }

  toJSON() {
    return {
      $type: 'NodeCard',
      Id: this.Id,
      Row: this.Row,
      Column: this.Column,
      Kind: this.Kind,
      X: this.X,
      Y: this.Y,
      Glyph: this.Glyph,
      KindLabel: this.KindLabel,
      Standing: this.Standing,
      Lines: this.Lines,
      Options: this.Options,
      Note: this.Note,
      Title: this.Title,
      Detail: this.Detail,
    };
  }
}

/** A link between two nodes. Lit when it leads from where the player stands to somewhere they can go. */
export class EdgeLine {
  constructor(from, to, lit) {
    this.From = from;
    this.To = to;
    this.Lit = lit;
  }

  toJSON() {
    return { $type: 'EdgeLine', From: this.From, To: this.To, Lit: this.Lit };
  }
}

/** The 瘴気 gauge and what the next new node adds to it. */
export class MiasmaGauge {
  constructor(percent, nextPercent, label, forecastLabel, stepLines) {
    this.Percent = percent;
    /** The reading after one more new node. Equal to Percent when no step is possible. */
    this.NextPercent = nextPercent;
    this.Label = label;
    /** 「次の 1 歩で +3%」, or "" when no new node can be entered. */
    this.ForecastLabel = forecastLabel;
    /** The readings where max stamina drops by one (20, 40, 60, 80). */
    this.StepLines = stepLines;
  }

  /** C#: Percent / 100f — a float32. */
  get Fill() { return Math.fround(this.Percent / 100); }

  /** C#: NextPercent / 100f — a float32. */
  get NextFill() { return Math.fround(this.NextPercent / 100); }

  toJSON() {
    return {
      $type: 'MiasmaGauge',
      Percent: this.Percent,
      NextPercent: this.NextPercent,
      Fill: this.Fill,
      NextFill: this.NextFill,
      Label: this.Label,
      ForecastLabel: this.ForecastLabel,
      StepLines: this.StepLines,
    };
  }
}

/** One consumable slot. */
export class SlotCard {
  constructor(index, name, note, canUse, forecast = null, reason = '', aiming = false, id = null) {
    this.Index = index;
    /** The item's name, or 「（空き）」. */
    this.Name = name;
    this.Note = note;
    this.CanUse = canUse;
    /** JS extension (trial mode): what using it changes, tried on the state (「HP 30 → 40」), or null. */
    this.Forecast = forecast;
    /** JS extension (trial mode): why it cannot be used now, or "". */
    this.Reason = reason;
    /** JS extension (trial mode): this 標の石 is being aimed — the map picks where it lands. */
    this.Aiming = aiming === true;
    /** JS extension: the catalogue id of the item in the slot, or null when empty. */
    this.Id = id;
  }

  toJSON() {
    return { $type: 'SlotCard', Index: this.Index, Name: this.Name, Note: this.Note, CanUse: this.CanUse };
  }
}

export class CardButton {
  constructor(label, action, arg = null, note = '') {
    this.Label = label;
    this.Action = action;
    /** JS extension: the argument ExplorationSession.Act takes with the action (a slot, an enemy id), or null. */
    this.Arg = arg;
    /** JS extension: one line under the button (what the choice changes), or "". */
    this.Note = note;
  }

  toJSON() {
    return { $type: 'CardButton', Label: this.Label, Action: this.Action };
  }
}

/** The card over the map: a stand-in battle, a confirmation, a layer's end or a life's end. */
export class ScreenCard {
  static None = new ScreenCard(CardKind.None, '', [], []);

  constructor(kind, title, lines, buttons) {
    this.Kind = kind;
    this.Title = title;
    this.Lines = lines;
    this.Buttons = buttons;
  }

  get IsShown() { return this.Kind !== CardKind.None; }

  /** A card that waits for a yes or a no. Esc answers no before it does anything else (§6.2). */
  get IsConfirmation() {
    return this.Kind === CardKind.ConfirmStep || this.Kind === CardKind.ConfirmDescend || this.Kind === CardKind.ConfirmEndLife;
  }

  /** JS extension (trial mode): a card that waits for one of its buttons (an event, a swap, a survey pick). */
  get IsChoice() {
    return this.Kind === TrialCardKind.EventChoice || this.Kind === TrialCardKind.Swap || this.Kind === TrialCardKind.SurveyChoice;
  }

  toJSON() {
    return {
      $type: 'ScreenCard',
      Kind: this.Kind,
      Title: this.Title,
      Lines: this.Lines,
      Buttons: this.Buttons,
      IsShown: this.IsShown,
      IsConfirmation: this.IsConfirmation,
    };
  }
}

/**
 * JS extension (trial mode): one carried tool as the inventory shows it — its name, the catalogue's note,
 * and whether it acts on the exploration or in battle (tools_and_consumables_v4.md §2).
 */
export class ToolCard {
  constructor(id, name, note, side, effect) {
    this.Id = id;
    this.Name = name;
    this.Note = note;
    /** 「探索」 or 「戦闘」. */
    this.Side = side;
    /** What it does on this layer, in numbers (「刻限 +1」), or "" for a battle tool. */
    this.Effect = effect;
  }
}

/**
 * JS extension (trial mode): what one イベント did (encounters_and_rewards_v4.md §5), kept for the
 * result card and the journal's dungeon page (「起きたイベントは、手記のダンジョンの頁に…残します」).
 */
export class EventOutcome {
  constructor(layer, nodeId, kind, name, accepted, lines, cards, consumables, pages, record = null) {
    this.Layer = layer;
    this.NodeId = nodeId;
    /** LayerEvents' EventKind. */
    this.Kind = kind;
    /** 「泉」 and the like. */
    this.Name = name;
    /** The player took it (a 泉 always does). */
    this.Accepted = accepted;
    /** What changed, line by line (「HP 30 → 40」). */
    this.Lines = lines;
    /** A chest's cards laid out to pick one from (Expedition.Learn), as ids. */
    this.Cards = cards ?? [];
    /** A chest's consumables, as catalogue ids. */
    this.Consumables = consumables ?? [];
    /** A chest's rare pages. */
    this.Pages = pages ?? 0;
    /** The short lines the journal keeps (what changed and what was found), or the result lines. */
    this.Record = record ?? lines;
  }

  /** The one-line summary the journal's dungeon page shows. */
  get Summary() {
    return this.Record.join('・');
  }
}

/** A screen the top band opens over the map: 所持カード or 設定・セーブ (§6.2). */
export class OverlayPage {
  constructor(title, lines, closeLabel) {
    this.Title = title;
    this.Lines = lines;
    /** 「閉じる（C）」 and the like: the button and the key that close it. */
    this.CloseLabel = closeLabel;
  }

  toJSON() {
    return { $type: 'OverlayPage', Title: this.Title, Lines: this.Lines, CloseLabel: this.CloseLabel };
  }
}

/** One frame of the exploration screen. */
export class ExplorationScreen {
  constructor(
    layerTitle,
    timeLabel,
    timeLeft,
    timeLimit,
    miasma,
    hpLabel,
    staminaLabel,
    maxStaminaLabel,
    marksLabel,
    nodes,
    edges,
    currentNodeId,
    hint,
    toolsLabel,
    consumables,
    canEndLife,
    showsEndLife,
    card,
    journal,
    openOverlay,
    canOpenCards,
    canOpenSettings,
    cards,
    settings,
    toolCards = [],
    timeBonusLabel = '',
    reliefLabel = '',
    aiming = false,
    events = []) {
    /** 「第 2 層　■■の秘跡」. */
    this.LayerTitle = layerTitle;
    /** 「刻限 7 / 10」. */
    this.TimeLabel = timeLabel;
    /** How many of the TimeLimit pips are still lit. */
    this.TimeLeft = timeLeft;
    this.TimeLimit = timeLimit;
    this.Miasma = miasma;
    this.HpLabel = hpLabel;
    this.StaminaLabel = staminaLabel;
    /** 「最大 9 = 10 − 瘴気 1」, with a temporary modifier added when there is one. */
    this.MaxStaminaLabel = maxStaminaLabel;
    this.MarksLabel = marksLabel;
    this.Nodes = nodes;
    this.Edges = edges;
    this.CurrentNodeId = currentNodeId;
    /** One line under the map on how to read it. */
    this.Hint = hint;
    /** 「ツール: 防瘴の面（1 / 3）」. */
    this.ToolsLabel = toolsLabel;
    this.Consumables = consumables;
    /** The 「この生を終える」 button is live (§6.6). */
    this.CanEndLife = canEndLife;
    /** The button is on screen at all. It goes once the life has ended (§6.2). */
    this.ShowsEndLife = showsEndLife;
    this.Card = card;
    this.Journal = journal;
    /** What is open over the screen from the top band, if anything. */
    this.OpenOverlay = openOverlay;
    /**
     * The band's 所持カード button is live. Like the gear, it goes dead while the stand-in battle
     * card is up, which counts as being in battle (§6.2). The journal's button never does.
     */
    this.CanOpenCards = canOpenCards;
    this.CanOpenSettings = canOpenSettings;
    /** The 所持カード screen. Its contents wait for #244; until then it is a stand-in. */
    this.Cards = cards;
    /** The 設定・セーブ screen. Settings and saving are not wired yet; it is a stand-in. */
    this.Settings = settings;
    /** JS extension (trial mode): every carried tool with its note (ToolCard). Empty outside trial. */
    this.ToolCards = toolCards;
    /** JS extension (trial mode): 「刻限の砂 +1」 beside the 刻限 in the band, or "". */
    this.TimeBonusLabel = timeBonusLabel;
    /** JS extension (trial mode): 「防瘴の面 濃さ 2 → 1」 beside the gauge, or "". */
    this.ReliefLabel = reliefLabel;
    /** JS extension (trial mode): a 標の石 is being aimed; the nodes it can land on carry jump options. */
    this.Aiming = aiming === true;
    /** JS extension (trial mode): every event of this life so far (EventOutcome), oldest first. */
    this.Events = events;
  }

  /**
   * The map takes input only while no card covers it. The carried items, the band and
   * 「この生を終える」 stay above a card: whether they work is CanUse / CanEndLife.
   */
  get MapIsLive() { return !this.Card.IsShown; }

  Node(id) {
    for (const node of this.Nodes) {
      if (node.Id === id) return node;
    }
    return null;
  }

  toJSON() {
    return {
      $type: 'ExplorationScreen',
      LayerTitle: this.LayerTitle,
      TimeLabel: this.TimeLabel,
      TimeLeft: this.TimeLeft,
      TimeLimit: this.TimeLimit,
      Miasma: this.Miasma,
      HpLabel: this.HpLabel,
      StaminaLabel: this.StaminaLabel,
      MaxStaminaLabel: this.MaxStaminaLabel,
      MarksLabel: this.MarksLabel,
      Nodes: this.Nodes,
      Edges: this.Edges,
      CurrentNodeId: this.CurrentNodeId,
      Hint: this.Hint,
      ToolsLabel: this.ToolsLabel,
      Consumables: this.Consumables,
      CanEndLife: this.CanEndLife,
      ShowsEndLife: this.ShowsEndLife,
      Card: this.Card,
      Journal: this.Journal,
      OpenOverlay: this.OpenOverlay,
      CanOpenCards: this.CanOpenCards,
      CanOpenSettings: this.CanOpenSettings,
      Cards: this.Cards,
      Settings: this.Settings,
      MapIsLive: this.MapIsLive,
    };
  }
}
