// Port of unity-project-kit/Assets/View/Exploration/Script/ExplorationScreenBuilder.cs
// Turns a session into one frame of the exploration screen (dungeon_exploration_v4.md §6). Every
// number comes from the exploration core. The forecast of a step is ExplorationReducer.Step tried on
// the current state — the state is immutable, so trying it changes nothing — which is why the
// forecast and the step taken can never disagree.
import { stableSortBy, first, enumValues } from '../../shared/cs.js';
import { ExplorationReducer } from '../../core/dungeon/ExplorationReducer.js';
import { RunPhase, RestChoice } from '../../core/dungeon/ExplorationState.js';
import { Miasma } from '../../core/dungeon/Miasma.js';
import { NodeKind, NodeKindTokens } from '../../core/dungeon/NodeKind.js';
import { ReliefKind, RunLoadout } from '../../core/dungeon/RunLoadout.js';
import { SevenLayers } from '../../core/dungeon/SevenLayers.js';
import { EventKind, LayerEvents } from '../../core/dungeon/LayerEvents.js';
import { ItemCatalogue } from '../../core/dungeonContent/ItemCatalogue.js';
import { JournalBook, JournalContext, JournalPage, JournalRow, JournalTab } from '../journal/JournalBook.js';
import { JournalTerms } from '../journal/JournalTerms.js';
import { ExplorationText } from './ExplorationText.js';
import {
  CardButton,
  CardKind,
  EdgeLine,
  ExplorationScreen,
  MiasmaGauge,
  NodeCard,
  NodeStanding,
  Overlay,
  OverlayPage,
  ScreenAction,
  ScreenCard,
  SlotCard,
  StepForecast,
  StepOption,
  ToolCard,
  TrialAction,
  TrialCardKind,
} from './ExplorationScreen.js';

// C# float arithmetic (32-bit): every float result is rounded to single precision.
const f32 = Math.fround;

let _cardsPage = null;
let _settingsPage = null;

/**
 * 所持カード (CAMP_FACILITIES_DESIGN.md §3.4): a stand-in until #244's deck screen lends its
 * parts and the run carries the character's cards. (C# static readonly; built on first use.)
 */
function CardsPage() {
  return _cardsPage ??= new OverlayPage('所持カード（仮）',
    [
      '今のキャラクターが持つカードの、現在の名称・習熟度・永続能力を読む画面です。',
      '読むだけで、戦闘デッキは組み替えられません。',
      '',
      'カードの一覧はまだつながっていません。',
    ],
    '閉じる（C）');
}

/**
 * 設定・セーブ (CAMP_FACILITIES_DESIGN.md §3.5): the items it will hold, none of them wired yet.
 * 「保存してゲームを閉じる」 saves without ending the life; 「この生を終える」 is a different button.
 */
function SettingsPage() {
  return _settingsPage ??= new OverlayPage('設定・セーブ（仮）',
    [
      '戦闘の速さ: ×1.0 / ×1.25 / ×1.5（既定は ×1.25）',
      '演出短縮: 入れる / 切る',
      '音の大きさ',
      '保存してゲームを閉じる: 今の状態を保存して閉じる。生は終わらない',
      '',
      '設定と保存はまだつながっていません。いまは開け閉めだけができます。',
    ],
    '閉じる（Esc）');
}

function StandingOf(state, id) {
  if (id === state.CurrentNodeId) return NodeStanding.Current;
  const resolved = state.HasResolved(id);
  if (ExplorationReducer.CanStep(state, id)) return resolved ? NodeStanding.Revisit : NodeStanding.Reachable;
  return resolved ? NodeStanding.Resolved : NodeStanding.Far;
}

function NoteOf(standing) {
  switch (standing) {
    case NodeStanding.Current: return '今いるノード';
    case NodeStanding.Reachable: return '入ると刻限を 1 使う';
    case NodeStanding.Revisit: return '踏んだノード。刻限を使わずに移れる';
    case NodeStanding.Resolved: return '踏んだノード';
    default: return ExplorationText.FarNode;
  }
}

/** The detail panel's lines: the note, the contents, then each way in with its forecast. */
function Detail(note, contents, options) {
  const detail = [note];
  detail.push(...contents);
  for (const option of options) {
    if (option.Forecast == null) continue;
    detail.push('');
    detail.push(`【${option.Label}】` + option.Forecast.Lines.join('　'));
    detail.push(...option.Forecast.Warnings.map((w) => '！ ' + w));
  }
  return detail;
}

function Lit(state, nodes, from, to) {
  let other;
  if (from === state.CurrentNodeId) other = to;
  else if (to === state.CurrentNodeId) other = from;
  else return false;

  const standing = first(nodes, (n) => n.Id === other).Standing;
  return standing === NodeStanding.Reachable || standing === NodeStanding.Revisit;
}

/** What a node holds, by kind (dungeon_exploration_v4.md §6.3). */
function ContentLines(session, node) {
  const state = session.State;
  const lines = [];
  const threat = ExplorationText.Threat(node.Kind);
  if (threat.length > 0) lines.push(`脅威度: ${threat}`);

  if (NodeKindTokens.IsCombat(node.Kind)) {
    // case NodeKind kind when kind.IsCombat():
    if (node.Kind === NodeKind.Elite) lines.push('避けて通れる寄り道');
    if (node.Kind === NodeKind.Boss) {
      lines.push(state.Profile.IsLast
        ? '倒すと、この生で潜り切ったことになる'
        : '倒すと階層間の休憩へ進む');
    }
    const intel = session.Intel.ForNode(node, state.Profile.Layer, session.Surveyed);
    if (intel == null || intel.EnemyName.length === 0) {
      lines.push(`敵: ${ExplorationText.UnknownEnemy}`);
    } else {
      lines.push(`敵: ${intel.EnemyName}（開示度 ${intel.Disclosure} / ${JournalPage.MaxDisclosure}）`);
      lines.push(...intel.Lines);
    }
  } else {
    switch (node.Kind) {
      case NodeKind.Rest: {
        // JS extension (trial mode): the HP the rest would give now, marked when the maximum stops it — the same
        // reading as the layer-break card and the interlude (「HP +4（最大の 30%・上限まで）」); the C# prints the share.
        const share = ExplorationReducer.RestHeal(state.MaxHp);
        const gain = session.Trial ? Math.min(share, Math.max(0, state.MaxHp - state.Hp)) : share;
        lines.push(`休む: HP +${gain}（最大の ${ExplorationReducer.RestHpPercent}%${gain < share ? ExplorationText.CappedMark : ''}）・` +
          `スタミナ全回復・最大スタミナ +${ExplorationReducer.RestMaxStaminaBonus}`);
        lines.push('訓練する: 選んだ 1 枚に習熟の刻み 1');
        lines.push('どちらか 1 つだけを選ぶ');
        break;
      }

      case NodeKind.Survey:
        lines.push('この層の敵 1 種の開示度が 2 段目へ上がる');
        // JS extension (trial mode): once done, the node says which enemy it raised (SurveyLine), not only that it
        // was done — the C# prints 「この層では済ませた」 and the journal's row was the one place that named it.
        if (session.Surveyed) lines.push(SurveyLine(session) ?? 'この層では済ませた');
        // JS extension (trial mode): 写しの帳 (tools_and_consumables_v4.md §2.2).
        if (session.Trial && state.Loadout.ChoosesSurvey && !session.Surveyed) lines.push(ExplorationText.SurveyChooseLine);
        break;

      case NodeKind.Event:
        if (session.Trial) lines.push(...TrialEventLines(session, node));
        else lines.push('泉 / 重い宝箱 / 特殊な食事 のどれかが起きる');
        break;

      case NodeKind.Trace:
        // JS extension (trial mode): once this life collected the layer's trace (the game's Expedition.Inherit sets
        // InheritedTraceId / InheritedLayer, src/app/life.js), the node says so instead of offering it again.
        lines.push(session.Trial && session.Keeper && session.Keeper.InheritedTraceId != null &&
          session.Keeper.InheritedLayer === state.Profile.Layer ? ExplorationText.TraceCollected : '前の生の遺産を受け取れる');
        lines.push('受け取れるのは 1 つの生で 1 件');
        break;

      case NodeKind.Carving:
        lines.push(session.CarvingTaken ? '読んだ。この層の名前が分かった' : 'この層の名前が分かる');
        lines.push('戦闘には効かない');
        break;
    }
  }
  return lines;
}

/**
 * JS extension (trial mode): what an event node holds, once it is next door or walked
 * (encounters_and_rewards_v4.md §5: 「中身は隣接したときに見えます」). A far node never gets here.
 */
function TrialEventLines(session, node) {
  const state = session.State;
  const kind = session.EventAt(node.Id);
  if (kind === null) return [ExplorationText.EventHidden];
  const layer = state.Profile.Layer;
  const done = session.Events.find((e) => e.Layer === layer && e.NodeId === node.Id) ?? null;
  const lines = [];
  switch (kind) {
    case EventKind.Spring:
      lines.push(ExplorationText.SpringLine(ExplorationReducer.SpringHeal(state), LayerEvents.SpringPercent(layer),
        done === null ? Math.max(0, state.MaxHp - state.Hp) : null));
      break;
    case EventKind.HeavyChest:
      lines.push(ExplorationText.ChestLine(LayerEvents.ChestStaminaCost));
      lines.push(ExplorationText.ChestContents(LayerEvents.ChestContents(layer)));
      break;
    case EventKind.StrangeMeal:
      lines.push(ExplorationText.MealLine(LayerEvents.MealStaminaGain, ExplorationReducer.MealCost(state), LayerEvents.MealHpPercent));
      break;
  }
  if (done !== null) lines.push(ExplorationText.EventDone(done.Accepted, done.Summary));
  else if (LayerEvents.HasChoice(kind)) lines.push(ExplorationText.EventChoiceNote);
  return lines;
}

/**
 * JS extension (trial mode): the event a step onto this node resolves on arrival without asking — a 泉 —
 * so the forecast can count it (dungeon_exploration_v4.md §6.4: 「変わるときだけ…HP 30 → 38 を足します」).
 */
function ArrivalEvent(session, node) {
  if (!session.Trial || node.Kind !== NodeKind.Event || session.State.HasResolved(node.Id)) return null;
  const kind = session.EventAt(node.Id);
  return kind === EventKind.Spring ? kind : null;
}

/**
 * JS extension (trial mode): 標の石's ways onto a node it can land on, each with its forecast.
 * A far node's contents stay hidden (encounters_and_rewards_v4.md §5 「中身は隣接したときに見えます」;
 * dungeon_exploration_v4.md §6.3), so the jump's forecast counts its event only once the node is next door
 * or walked — a far 泉 forecasts as a bare step, like a far 重い宝箱.
 */
function JumpOptions(session, node, slot, standing) {
  const state = session.State;
  if (!ExplorationReducer.CanTeleport(state, node.Id)) return [];
  const event = standing === NodeStanding.Far ? null : ArrivalEvent(session, node);
  const forecast = (choice) => ExplorationScreenBuilder.Forecast(state, node.Id, choice, slot, event);
  if (node.Kind === NodeKind.Rest) {
    return [
      new StepOption(ExplorationText.JumpRestLabel, RestChoice.Rest, forecast(RestChoice.Rest), true),
      new StepOption(ExplorationText.JumpTrainLabel, RestChoice.Train, forecast(RestChoice.Train), true),
    ];
  }
  return [new StepOption(ExplorationText.JumpLabel, RestChoice.Rest, forecast(RestChoice.Rest), true)];
}

function Options(state, node, standing, event = null) {
  if (standing === NodeStanding.Revisit)
    return [new StepOption('移る', RestChoice.Rest, null)];
  if (standing !== NodeStanding.Reachable)
    return [];

  if (node.Kind === NodeKind.Rest) {
    return [
      new StepOption('休む', RestChoice.Rest, ExplorationScreenBuilder.Forecast(state, node.Id, RestChoice.Rest)),
      new StepOption('訓練する', RestChoice.Train, ExplorationScreenBuilder.Forecast(state, node.Id, RestChoice.Train)),
    ];
  }
  return [new StepOption('進む', RestChoice.Rest, ExplorationScreenBuilder.Forecast(state, node.Id, RestChoice.Rest, -1, event))];
}

function Gauge(session) {
  const state = session.State;
  const percent = state.MiasmaPercent;
  let next = percent;
  let forecast = '';

  let below;
  if (state.Phase === RunPhase.Exploring && state.TimeLeft > 0) {
    next = Miasma.Accumulate(percent, state.EffectiveDensity);
    forecast = `次の 1 歩で +${state.EffectiveDensity}%`;
  } else if ((below = session.TryDescend()) != null) {
    // The next layer's entry node is resolved on arrival, so descending costs one step at once.
    next = below.MiasmaPercent;
    // JS extension (trial mode): the label says what a step of the next layer adds — its density, as the map's
    // 「次の 1 歩で +N%」 does — not the gap to the 100% cap, which near the top would understate the layer
    // below (the interlude card's own line says 「1 歩ごとに瘴気 +N%」 and warns of the cap).
    forecast = `降りると +${session.Trial ? below.EffectiveDensity : below.MiasmaPercent - percent}%`;
  }

  const stepLines = [];
  for (let line = Miasma.StepPercent; line < Miasma.DeathAt; line += Miasma.StepPercent) stepLines.push(line);

  return new MiasmaGauge(percent, next, `瘴気 ${percent}%`, forecast, stepLines);
}

/**
 * JS extension (trial mode): 防瘴の面's note under the gauge, for the layer the gauge's forecast speaks of — the
 * layer being walked (「次の 1 歩で +N%」), or, once the layer is left, the layer below (「降りると +N%」, X-6), so the
 * note and the figure beside it are the same layer's (dungeon_exploration_v4.md §6.5 「次の層の予告」).
 */
function ReliefLabel(session) {
  const state = session.State;
  const names = ToolNames(state.Loadout, ReliefKind.Density);
  if (!(state.Phase === RunPhase.Exploring && state.TimeLeft > 0)) {
    const below = session.TryDescend();
    if (below != null) return ExplorationText.Relief(names, below.Profile.Density, below.EffectiveDensity);
  }
  return ExplorationText.Relief(names, state.Profile.Density, state.EffectiveDensity);
}

/** 「最大 9 = 10 − 瘴気 1」, plus the temporary modifier when there is one. */
function MaxStaminaLabel(state) {
  const penalty = Miasma.MaxStaminaPenalty(state.MiasmaPercent);
  const temp = state.TempMaxStaminaMod;
  const max = state.MaxStamina;

  // The 3..14 clamp can break the sum; then only the maximum itself is honest to print.
  if (Miasma.BaseMaxStamina - penalty + temp !== max) return `最大 ${max}`;

  let label = `最大 ${max} = ${Miasma.BaseMaxStamina} − 瘴気 ${penalty}`;
  if (temp > 0) label += ` + 一時 ${temp}`;
  else if (temp < 0) label += ` − 一時 ${-temp}`;
  return label;
}

function ToolsLabel(loadout) {
  const names = loadout.Tools.length === 0
    ? 'なし'
    : loadout.Tools.map((t) => ExplorationText.ItemName(t.Id)).join('・');
  return `ツール: ${names}（${loadout.Tools.length} / ${loadout.ToolSlotCount}）`;
}

function Slots(session) {
  if (session.Trial) return TrialSlots(session);
  const carried = session.State.Loadout.Consumables;
  const slots = [];
  for (let i = 0; i < RunLoadout.ConsumableSlots; i++) {
    if (i < carried.length) {
      const id = carried[i].Id;
      slots.push(new SlotCard(i, ExplorationText.ItemName(id), ExplorationText.ItemNote(id), session.CanUseConsumables));
    } else {
      slots.push(new SlotCard(i, ExplorationText.EmptySlot, '', false));
    }
  }
  return slots;
}

/**
 * JS extension (trial mode): every slot says whether it can be used now, what using it would change
 * (tried on the state, the way a step's forecast is), or why it cannot.
 */
function TrialSlots(session) {
  const state = session.State;
  const carried = state.Loadout.Consumables;
  const slots = [];
  for (let i = 0; i < RunLoadout.ConsumableSlots; i++) {
    if (i >= carried.length) {
      slots.push(new SlotCard(i, ExplorationText.EmptySlot, '', false));
      continue;
    }
    const item = carried[i];
    const canUse = session.CanUseSlot(i);
    let forecast = null;
    let reason = '';
    if (item.Kind === ReliefKind.Teleport) {
      if (!canUse && session.CanUseConsumables) reason = ExplorationText.NoJumpNow;
    } else if (session.CanUseConsumables) {
      const after = ExplorationReducer.UseConsumable(state, i);
      forecast = ChangeLines(state, after);
      if (forecast.length === 0) {
        forecast = null;
        reason = ExplorationText.NoEffectNow;
      }
    }
    slots.push(new SlotCard(i, ExplorationText.ItemName(item.Id), ExplorationText.PlayerNote(item.Id), canUse, forecast, reason,
      session.AimingSlot === i, item.Id));
  }
  return slots;
}

/**
 * JS extension: 「HP 30 → 40」 and the like for every value that differs between two states — the gauge,
 * max stamina, stamina and HP, the values the screen shows. It is also the one definition of 「changes
 * something now」: ExplorationSession.CanUseSlot offers a consumable only when this is not empty, so a slot is
 * lit exactly when it has a forecast (a hidden temporary change the floor of 3 swallows counts as nothing).
 */
export function ChangeLines(before, after) {
  const lines = [];
  if (after.MiasmaPercent !== before.MiasmaPercent) lines.push(ExplorationText.MiasmaChange(before.MiasmaPercent, after.MiasmaPercent));
  if (after.MaxStamina !== before.MaxStamina) lines.push(ExplorationText.MaxStaminaChange(before.MaxStamina, after.MaxStamina));
  if (after.Stamina !== before.Stamina) lines.push(ExplorationText.StaminaChange(before.Stamina, after.Stamina));
  if (after.Hp !== before.Hp) lines.push(ExplorationText.HpChange(before.Hp, after.Hp));
  return lines;
}

/** JS extension (trial mode): every carried tool with what it does (tools_and_consumables_v4.md §2). */
function ToolCards(session) {
  if (!session.Trial) return [];
  const state = session.State;
  return state.Loadout.Tools.map((t) => {
    const def = ItemCatalogue.All.find((d) => d.Id === t.Id) ?? null;
    const battle = t.Kind === ReliefKind.Battle;
    const effect = ExplorationText.ToolEffect[t.Kind] ? ExplorationText.ToolEffect[t.Kind](t.Amount) : '';
    return new ToolCard(t.Id, def ? def.Name : t.Id, ExplorationText.PlayerNote(t.Id),
      battle ? ExplorationText.SideBattle : ExplorationText.SideExploration, effect);
  });
}

/** JS extension (trial mode): the names of the carried tools of one kind, joined (「刻限の砂・刻限の砂」→ one name). */
function ToolNames(loadout, kind) {
  const names = [];
  for (const t of loadout.Tools) {
    if (t.Kind !== kind) continue;
    const name = ExplorationText.ItemName(t.Id);
    if (!names.includes(name)) names.push(name);
  }
  return names.join('・');
}

/**
 * JS extension (trial mode): the interlude's share with 灯芯の欠片 named. `capped` (trial): the HP printed beside it is
 * less than the share because the maximum stopped it, so the share says 「上限まで」 (one reading on every screen:
 * the real gain, marked when capped — dungeon_exploration_v4.md §6.3 / §6.5).
 */
function InterludeShare(state, capped = false) {
  const percent = ExplorationReducer.InterludePercent(state);
  const bonus = percent - ExplorationReducer.InterludeHpPercent;
  return state.Trial
    ? ExplorationText.InterludeShare(percent, ToolNames(state.Loadout, ReliefKind.Interlude), bonus, capped)
    : `（最大の ${ExplorationReducer.InterludeHpPercent}%）`;
}

/** JS extension (trial mode): the cards that wait on a choice, before the layer's or the life's end. */
function TrialCard(session) {
  const state = session.State;
  if (session.HasPendingEvent) {
    const waiting = session.PendingEvent;
    const name = ExplorationText.EventName(waiting.Kind);
    const accepted = ExplorationReducer.ResolveEvent(state, waiting.Kind, true);
    const node = state.Map.Node(waiting.NodeId);
    const lines = [ExplorationText.EventLead(name)];
    lines.push(...TrialEventLines(session, node).filter((l) => l !== ExplorationText.EventChoiceNote));
    lines.push(ExplorationText.TempClampNote);
    return new ScreenCard(TrialCardKind.EventChoice, name, lines,
      [
        new CardButton(ExplorationText.AcceptLabel, TrialAction.AcceptEvent, null, ChangeLines(state, accepted).join('　')),
        new CardButton(ExplorationText.DeclineLabel, TrialAction.DeclineEvent, null, ExplorationText.DeclineNote),
      ]);
  }

  if (session.HasPendingSurvey) {
    const keeper = session.Keeper;
    const buttons = session.SurveyChoices.map((id) => new CardButton(
      ExplorationText.SurveyButton(keeper ? keeper.EnemyName(id) : id, keeper ? keeper.DisclosureOf(id) : 0),
      TrialAction.ChooseSurvey, id));
    return new ScreenCard(TrialCardKind.SurveyChoice, ExplorationText.SurveyTitle, [ExplorationText.SurveyLead], buttons);
  }

  if (session.HasPendingFound) {
    const id = session.PendingFound[0];
    const name = ExplorationText.ItemName(id);
    const buttons = state.Loadout.Consumables.map((item, i) =>
      new CardButton(ExplorationText.SwapButton(ExplorationText.ItemName(item.Id)), TrialAction.TakeFound, i, ExplorationText.PlayerNote(item.Id)));
    buttons.push(new CardButton(ExplorationText.LeaveFoundLabel, TrialAction.LeaveFound, null, ''));
    return new ScreenCard(TrialCardKind.Swap, ExplorationText.SwapTitle,
      [ExplorationText.SwapLead(name), `${name}: ${ExplorationText.PlayerNote(id)}`], buttons);
  }
  return null;
}

/** The card over the map. Waiting inputs come first, then the layer's or the life's end. */
function Card(session) {
  const state = session.State;
  const layer = state.Profile.Layer;

  if (session.HasPendingBattle) {
    const kind = state.Map.Node(session.PendingBattleNode).Kind;
    const title = kind === NodeKind.Boss ? '階層ボスとの戦闘（仮）'
      : kind === NodeKind.Elite ? '精鋭との戦闘（仮）'
        : '戦闘（仮）';
    return new ScreenCard(CardKind.Battle, title,
      [
        '探索と戦闘はまだつながっていません。結果を選んで探索へ戻ります。',
        '勝ったことにする: HP とスタミナはそのまま',
        '倒れたことにする: HP 0 でこの生が終わる',
      ],
      [
        new CardButton('勝ったことにする', ScreenAction.WinBattle),
        new CardButton('倒れたことにする', ScreenAction.FallInBattle),
      ]);
  }

  if (session.HasPendingStep) {
    return new ScreenCard(CardKind.ConfirmStep, '瘴気が 100% に達します',
      ['このノードへ入ると、瘴気死でこの生が終わります。'],
      [
        new CardButton('それでも進む', ScreenAction.ConfirmStep),
        new CardButton('やめる', ScreenAction.Cancel),
      ]);
  }

  if (session.ConfirmingDescend) {
    return new ScreenCard(CardKind.ConfirmDescend, '瘴気が 100% に達します',
      ['次の層へ降りると、入口で瘴気が 100% に達し、この生が終わります。'],
      [
        new CardButton('それでも降りる', ScreenAction.ConfirmDescend),
        new CardButton('やめる', ScreenAction.Cancel),
      ]);
  }

  if (session.ConfirmingEndLife) {
    return new ScreenCard(CardKind.ConfirmEndLife, 'この生を終えますか',
      [
        '生きたまま次の生へ移ります。手記は丸ごと次の生へ渡ります。',
        `いまの到達: 第 ${layer} 層、瘴気 ${state.MiasmaPercent}%`,
      ],
      [
        new CardButton('この生を終える', ScreenAction.ConfirmEndLife),
        new CardButton('やめる', ScreenAction.Cancel),
      ]);
  }

  // JS extension (trial mode): an event, a survey pick or a swap waits before the layer's end.
  if (session.Trial) {
    const trial = TrialCard(session);
    if (trial !== null) return trial;
  }

  // What the interlude will give, tried on the state the way a step's forecast is.
  const interludeGain = ExplorationReducer.CanInterlude(state) ? ExplorationReducer.Interlude(state).Hp - state.Hp : 0;
  const interludeLine = ExplorationReducer.CanInterlude(state)
    ? `階層間の休憩: HP +${interludeGain}` +
      `${InterludeShare(state, interludeGain < ExplorationReducer.InterludeHealOf(state))}・スタミナ全回復・瘴気はたまらない`
    : '';
  const toInterlude = [new CardButton('階層間の休憩へ', ScreenAction.GoToInterlude)];

  switch (state.Phase) {
    case RunPhase.LayerCleared:
      return new ScreenCard(CardKind.LayerCleared, '階層ボスを倒した',
        [`第 ${layer} 層を抜けます。`, interludeLine], toInterlude);

    case RunPhase.PushedOut:
      return new ScreenCard(CardKind.PushedOut, '刻限が尽きた',
        [
          state.Profile.IsLast
            ? 'この層の行動は終わりです。これより下の層はありません。'
            : 'この層の行動は終わりです。階層間の休憩を経て、次の層へ押し出されます。',
          interludeLine,
        ],
        toInterlude);

    case RunPhase.Interlude:
      return InterludeCard(session);

    case RunPhase.MiasmaDeath:
      return EndCard(CardKind.MiasmaDeath, '瘴気死', `瘴気が 100% に達し、第 ${layer} 層でこの生が終わった。`, state);

    case RunPhase.Fallen:
      return EndCard(CardKind.Fallen, '倒れた', `第 ${layer} 層の戦いで倒れた。`, state);

    case RunPhase.Completed:
      return EndCard(CardKind.Completed, '踏破', '歪みの根を倒した。', state);

    case RunPhase.Survived:
      return EndCard(CardKind.Survived, '生存ルート', `生きたまま、第 ${layer} 層でこの生を閉じた。`, state);

    default:
      return ScreenCard.None;
  }
}

function InterludeCard(session) {
  const state = session.State;
  // JS extension (trial mode): the rest's own lines read the state the rest left (session.InterludeState), so a
  // consumable used at the interlude (§6.5 「休憩の間も消耗品は使える」) changes the band and the slots, not
  // what the rest is said to have given (「HP +23…で HP 37 / 50」 stays true). The C# reads the state itself.
  const rest = session.Trial && session.InterludeState !== null ? session.InterludeState : state;
  const lines = [
    `HP +${session.InterludeHealed}${InterludeShare(rest, session.Trial && session.InterludeHealed < ExplorationReducer.InterludeHealOf(rest))}で HP ${rest.Hp} / ${rest.MaxHp}`,
    `スタミナが全回復した（${rest.Stamina} / ${rest.MaxStamina}）`,
    `瘴気はたまらない（${rest.MiasmaPercent}%）`,
  ];
  const buttons = [];

  if (state.Profile.IsLast) {
    lines.push('これより下の層はありません。');
  } else {
    const next = SevenLayers.Of(state.Profile.Layer + 1);
    const density = Miasma.EffectiveDensity(next.Density, state.Loadout.DensityRelief);
    // A hidden name stays hidden until its own carving is read, so the next layer shows its first name.
    // JS extension (trial mode): 刻限の砂 adds to the next layer's 刻限 too.
    const time = state.TimeBonus > 0
      ? `${next.TimeLimit + state.TimeBonus}（${ExplorationText.TimeBonus(ToolNames(state.Loadout, ReliefKind.Time), state.TimeBonus)}）`
      : `${next.TimeLimit}`;
    // JS: the name the life knows (DisplayName) — the same as Name in the C#, where the layer-two carving is
    // never read before its own layer; a lineage that read it in an earlier life knows it from the start (R-20).
    lines.push(`次は第 ${next.Layer} 層　${next.DisplayName(session.CarvingTaken)}: 1 歩ごとに瘴気 +${density}%、刻限 ${time}`);
    if (session.TryDescend().Phase === RunPhase.MiasmaDeath)
      lines.push('！ 降りると入口で瘴気が 100% に達し、この生が終わります');
    buttons.push(new CardButton('次の層へ降りる', ScreenAction.Descend));
  }
  buttons.push(new CardButton('この生を終える', ScreenAction.AskEndLife));
  return new ScreenCard(CardKind.Interlude, '階層間の休憩', lines, buttons);
}

function EndCard(kind, title, line, state) {
  return new ScreenCard(kind, title,
    [line, `瘴気 ${state.MiasmaPercent}% ／ HP ${state.Hp} / ${state.MaxHp}`],
    [new CardButton('新しい生で潜り直す', ScreenAction.NewLife)]);
}

/**
 * JS extension (trial mode): what this layer's 情報収集 did, once it is done — the enemy it raised to 開示度 2
 * (「済ませた。瘴甲の竜兵が開示度 2 になった」, 写しの帳's pick marked), or that every enemy behind the map was at 2
 * already. Null outside trial, before the survey, and while 写しの帳's choice waits. The node's detail, the journal's
 * 情報収集 row and the map's notice on arrival all print this one line.
 */
function SurveyLine(session) {
  if (!session.Trial || !session.Surveyed || !session.Keeper) return null;
  if (session.SurveyTarget !== null && session.SurveyTarget !== undefined) {
    return (session.SurveyPicked ? ExplorationText.SurveyChosen : ExplorationText.SurveyDone)(session.Keeper.EnemyName(session.SurveyTarget));
  }
  if (session.SurveyChoices && session.SurveyChoices.length > 0) return null;
  // the layer was asked (the intel names targets) and had none left below 2; an intel that names none says nothing
  return session.Intel && typeof session.Intel.SurveyTarget === 'function' ? ExplorationText.SurveyNothing : null;
}

/** The journal the drawer shows while exploring (dungeon_exploration_v4.md §6.7). */
function Book(session) {
  const state = session.State;
  const layer = state.Profile.Layer;
  const pages = [];

  const enemies = session.Intel.Pages(layer, session.Surveyed);
  if (enemies.length > 0) {
    pages.push(...enemies);
  } else {
    pages.push(new JournalPage('enemy-none', JournalTab.Enemy, '敵の頁',
      [new JournalRow('', '敵の頁はまだありません。遭遇すると開示度 1 の頁ができます。')],
      0));
  }

  const profile = state.Profile;
  const relief = state.Loadout.DensityRelief;
  let density = `1 刻限ごとに +${state.EffectiveDensity}%`;
  // JS extension (trial mode): the density stops at its floor (seven_layers_v4.md §3.2 「濃度は 1 より下がりません」),
  // so a subtraction that would go below it says so instead of printing 1 − 2 next to +1%.
  if (relief > 0 && session.Trial && profile.Density - relief < state.EffectiveDensity) {
    density += `（${ExplorationText.ReliefFloor(profile.Density, relief, state.EffectiveDensity)}）`;
  } else if (relief > 0) density += `（層の濃さ ${profile.Density} − ツール ${relief}）`;

  const counts = enumValues(NodeKind)
    .filter((k) => state.Map.CountOf(k) > 0)
    .map((k) => `${ExplorationText.KindLabel(k)} ${state.Map.CountOf(k)}`);

  const rows = [
    new JournalRow('瘴気の濃さ', density),
    new JournalRow('刻限', `${state.TimeLimit}（寄り道なしの踏破は ${state.Map.ShortestPathCost}）`),
    new JournalRow('ノード', counts.join('・')),
    new JournalRow('情報収集', session.Surveyed ? SurveyLine(session) ?? '済ませた。この層の敵 1 種が開示度 2 になった' : 'まだ'),
  ];
  // JS extension (trial mode): the events of this layer (encounters_and_rewards_v4.md §5: 「起きたイベントは、
  // 手記のダンジョンの頁に…残します」).
  if (session.Trial && state.Map.CountOf(NodeKind.Event) > 0) {
    const here = session.Events.filter((e) => e.Layer === layer);
    rows.push(new JournalRow(ExplorationText.EventsRow, here.length === 0
      ? ExplorationText.NoEvents
      : here.map((e) => ExplorationText.EventRecord(e.Name, e.Accepted, e.Summary)).join(' ／ ')));
  }
  pages.push(new JournalPage(`dungeon-${layer}`, JournalTab.Dungeon,
    ExplorationText.LayerTitle(layer, profile.DisplayName(session.CarvingTaken)), rows));
  // JS extension (trial mode): reading the layer-two carving adds a page (seven_layers_v4.md §5 「手記に頁が 1 つ増える」).
  // It is the player's knowledge, like the layer's name (§5 「生をまたぐ」, INHERITANCE R-20): a life that knows the name
  // from an earlier life has the page too.
  if (session.Trial && session.CarvingTaken) {
    pages.push(new JournalPage('carving', JournalTab.Dungeon, ExplorationText.CarvingPageTitle,
      ExplorationText.CarvingPageRows.map(([label, text]) => new JournalRow(label, text))));
  }

  pages.push(new JournalPage('memo', JournalTab.Memo, 'メモ',
    [new JournalRow('', 'メモはまだありません。')]));

  pages.push(JournalTerms.Page());

  return new JournalBook(pages, JournalContext.Exploration);
}

export const ExplorationScreenBuilder = {
  /** JS extension (trial mode): the line this layer's 情報収集 left (see SurveyLine), or null. */
  SurveyLine(session) { return SurveyLine(session); },

  Build(session) {
    const state = session.State;
    const map = state.Map;
    const mapLive = session.MapIsLive;

    const nodes = [];
    for (const node of stableSortBy(map.Nodes, (n) => n.Id)) {
      const standing = StandingOf(state, node.Id);
      const rowWidth = map.Row(node.Row).length;
      const x = f32(f32(node.Column + 0.5) / rowWidth);
      const y = map.RowCount === 1 ? 0.5 : f32(1 - f32(node.Row / (map.RowCount - 1)));

      const lines = standing === NodeStanding.Far ? [] : ContentLines(session, node);
      // JS extension (trial mode): while a 標の石 is aimed, the nodes it can land on take its jump instead.
      const jumps = mapLive && session.IsAiming ? JumpOptions(session, node, session.AimingSlot, standing) : null;
      const options = jumps !== null ? jumps : mapLive ? Options(state, node, standing, ArrivalEvent(session, node)) : [];
      const note = jumps !== null && jumps.length > 0 ? ExplorationText.JumpNote : NoteOf(standing);
      nodes.push(new NodeCard(
        node,
        x,
        y,
        ExplorationText.Glyph(node.Kind),
        ExplorationText.KindLabel(node.Kind),
        standing,
        lines,
        options,
        note,
        `${ExplorationText.Glyph(node.Kind)}　${ExplorationText.KindLabel(node.Kind)}`,
        Detail(note, lines, options)));
    }

    const edges = [];
    for (const node of stableSortBy(map.Nodes, (n) => n.Id)) {
      for (const to of stableSortBy(map.Successors(node.Id), (id) => id)) {
        edges.push(new EdgeLine(node.Id, to, Lit(state, nodes, node.Id, to)));
      }
    }

    // JS extension (trial mode): 刻限の砂 and 防瘴の面 shown where their numbers are (the band).
    const timeBonusLabel = state.TimeBonus > 0
      ? ExplorationText.TimeBonus(ToolNames(state.Loadout, ReliefKind.Time), state.TimeBonus) : '';
    const reliefLabel = session.Trial && state.Loadout.DensityRelief > 0 ? ReliefLabel(session) : '';
    const hint = !mapLive ? '' : session.IsAiming ? ExplorationText.AimHint
      : '明るい枠のノードへ進めます。踏んだノードへは刻限を使わずに移れます。';

    return new ExplorationScreen(
      ExplorationText.LayerTitle(state.Profile.Layer, state.Profile.DisplayName(session.CarvingTaken)),
      `刻限 ${state.TimeLeft} / ${state.TimeLimit}`,
      state.TimeLeft,
      state.TimeLimit,
      Gauge(session),
      `HP ${state.Hp} / ${state.MaxHp}`,
      `スタミナ ${state.Stamina} / ${state.MaxStamina}`,
      MaxStaminaLabel(state),
      `訓練の刻み ${state.TrainingMarks}`,
      nodes,
      edges,
      state.CurrentNodeId,
      hint,
      ToolsLabel(state.Loadout),
      Slots(session),
      session.CanEndLife,
      !state.IsOver,
      Card(session),
      Book(session),
      session.OpenOverlay,
      session.CanOpen(Overlay.Cards),
      session.CanOpen(Overlay.Settings),
      CardsPage(),
      SettingsPage(),
      ToolCards(session),
      timeBonusLabel,
      reliefLabel,
      session.IsAiming,
      session.Events);
  },

  /**
   * JS extension: the 階層間の休憩 card on its own, or null outside the interlude. Build's Card puts a confirmation
   * (この生を終える, a descent into 瘴気死) in its place; the scene draws the rest under that confirmation from this
   * (docs/EXTENSIONS.md X-8: 「the interlude's own card stays under its confirmation」, also after a reload).
   */
  InterludeCard(session) {
    return session.State.Phase === RunPhase.Interlude ? InterludeCard(session) : null;
  },

  /**
   * The step tried on the current state, told as before → after (dungeon_exploration_v4.md §6.4).
   * JS extension: with `jumpSlot` (≥ 0) it is 標の石's jump from that slot instead of a step; with
   * `event` (trial mode) the event the node resolves on arrival is counted too.
   */
  Forecast(before, nodeId, choice, jumpSlot = -1, event = null) {
    let after = jumpSlot >= 0
      ? ExplorationReducer.Teleport(before, jumpSlot, nodeId, choice)
      : ExplorationReducer.Step(before, nodeId, choice);
    // JS extension (trial mode): a 泉 heals on arrival, so its step's forecast counts the HP.
    if (event !== null && !after.IsOver) after = ExplorationReducer.ResolveEvent(after, event, true);

    const lines = [
      `刻限 ${ExplorationText.Change(before.TimeLeft, after.TimeLeft)}`,
      `瘴気 ${before.MiasmaPercent}% → ${after.MiasmaPercent}%`,
    ];
    if (after.MaxStamina !== before.MaxStamina)
      lines.push(`最大スタミナ ${ExplorationText.Change(before.MaxStamina, after.MaxStamina)}`);
    if (after.Stamina !== before.Stamina)
      lines.push(`スタミナ ${ExplorationText.Change(before.Stamina, after.Stamina)}`);
    if (after.Hp !== before.Hp)
      lines.push(`HP ${ExplorationText.Change(before.Hp, after.Hp)}`);

    const warnings = [];
    if (after.Phase === RunPhase.MiasmaDeath) {
      warnings.push('ここで瘴気が 100% に達し、この生が終わります');
    } else {
      // Judged on the maximum itself: a 休息 that crosses a 20% line still lifts it by 2.
      if (after.MaxStamina < before.MaxStamina) {
        const line = Miasma.MaxStaminaPenalty(after.MiasmaPercent) * Miasma.StepPercent;
        warnings.push(`瘴気が ${line}% を越え、最大スタミナが ${before.MaxStamina} から ${after.MaxStamina} に下がります`);
      }
      if (after.Phase === RunPhase.PushedOut)
        warnings.push('刻限が尽き、この層の行動が終わります');
    }
    return new StepForecast(before, after, lines, warnings);
  },
};
