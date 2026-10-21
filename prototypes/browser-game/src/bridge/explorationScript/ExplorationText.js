// Port of unity-project-kit/Assets/View/Exploration/Script/ExplorationText.cs
// Every word the exploration screen prints about a node kind, an item or a number. UI text is
// Japanese (CLAUDE.md); the numbers are passed in, never worked out here.
import { firstOrDefault, ArgumentOutOfRangeException } from '../../shared/cs.js';
import { ItemCatalogue } from '../../core/dungeonContent/ItemCatalogue.js';
import { NodeKind, NodeKindTokens } from '../../core/dungeon/NodeKind.js';
import { EventKind } from '../../core/dungeon/LayerEvents.js';

function badKind(kind) {
  return new ArgumentOutOfRangeException(`Specified argument was out of the range of valid values. (Parameter 'kind')\nActual value was ${kind}.`);
}

// C#: new Regex(@"（[^（）]*\.md[^（）]*）") used with Regex.Replace, which replaces every match.
const DocPointer = /（[^（）]*\.md[^（）]*）/g;
// JS extension: a sentence carrying a design-status remark (「。防御は Guard だけ（アーマーは凍結）」).
const DesignStatus = /。?[^。（）]*（[^（）]*凍結[^（）]*）/g;

export const ExplorationText = {
  /** The one-character stand-in for a node's icon until #90's art arrives. */
  Glyph(kind) {
    switch (kind) {
      case NodeKind.Battle: return '戦';
      case NodeKind.Elite: return '精';
      case NodeKind.Boss: return '主';
      case NodeKind.Rest: return '休';
      case NodeKind.Survey: return '調';
      case NodeKind.Event: return '事';
      case NodeKind.Trace: return '痕';
      case NodeKind.Carving: return '彫';
      default: throw badKind(kind);
    }
  },

  KindLabel(kind) {
    switch (kind) {
      case NodeKind.Battle: return '通常戦闘';
      case NodeKind.Elite: return '精鋭';
      case NodeKind.Boss: return '階層ボス';
      case NodeKind.Rest: return '休息';
      case NodeKind.Survey: return '情報収集';
      case NodeKind.Event: return 'イベント';
      case NodeKind.Trace: return '死亡地点の痕跡';
      case NodeKind.Carving: return '秘跡の彫り';
      default: throw badKind(kind);
    }
  },

  /** The threat word a combat node shows (dungeon_exploration_v4.md §6.3). Empty for the rest. */
  Threat(kind) {
    switch (kind) {
      case NodeKind.Battle: return '通常';
      case NodeKind.Elite: return '精鋭';
      case NodeKind.Boss: return '階層の主';
      default: return '';
    }
  },

  /** Whether the node holds a fight. The rule is DungeonCore's (NodeKindTokens.IsCombat). */
  IsCombat(kind) {
    return NodeKindTokens.IsCombat(kind);
  },

  LayerTitle(layer, name) {
    return `第 ${layer} 層　${name}`;
  },

  UnknownEnemy: '？',

  EmptySlot: '（空き）',

  JournalButton: '手記（J）',

  CardsButton: '所持カード（C）',

  /** The gear of 設定・セーブ, in words until the design lane draws the icon. */
  SettingsButton: '設定（Esc）',

  EndLifeButton: 'この生を終える',

  FarNode: '遠いノードは種別しか分かりません',

  /** An item's name from the catalogue, or its id when the catalogue does not know it. */
  ItemName(id) {
    return firstOrDefault(ItemCatalogue.All, (i) => i.Id === id)?.Name ?? id;
  },

  /** The catalogue's note without the design-document pointers it carries for the team. */
  ItemNote(id) {
    return (firstOrDefault(ItemCatalogue.All, (i) => i.Id === id)?.Note ?? '').replace(DocPointer, '').trim();
  },

  /**
   * JS extension (trial mode): the note a player reads (the slots, the tool chips, the swap card) — ItemNote without
   * the design-status remarks it carries for the team too (「防御は Guard だけ（アーマーは凍結）」: what the design has
   * frozen, CLAUDE.md 凍結), the same cut 出立 makes (departure.js DESIGN_STATUS): the sentence goes with its remark.
   */
  PlayerNote(id) {
    return ExplorationText.ItemNote(id).replace(DesignStatus, '').trim();
  },

  /** 「7 → 6」. */
  Change(before, after) {
    return `${before} → ${after}`;
  },

  // ---- JS extension (trial mode; docs/EXTENSIONS.md 「探索の持ち物とイベント」). Same style as above: the
  // numbers are passed in. Sources: encounters_and_rewards_v4.md §4〜§5, tools_and_consumables_v4.md §2〜§3,
  // dungeon_exploration_v4.md §6.

  /** encounters_and_rewards_v4.md §5: the three events by name. */
  EventName(kind) {
    switch (kind) {
      case EventKind.Spring: return '泉';
      case EventKind.HeavyChest: return '重い宝箱';
      case EventKind.StrangeMeal: return '特殊な食事';
      default: throw badKind(kind);
    }
  },

  /**
   * 「泉: HP +10（最大の 20%）」. `room` (the HP the maximum still lets in, or null): a heal the maximum would stop
   * prints what it gives, marked 「上限まで」 — the rest's and the interlude's reading.
   */
  SpringLine(heal, percent, room = null) {
    const gain = room == null ? heal : Math.min(heal, room);
    return `泉: HP +${gain}（最大の ${percent}%${gain < heal ? ExplorationText.CappedMark : ''}）`;
  },

  /** A share of the HP maximum that the maximum stopped short: 「（最大の 30%・上限まで）」. */
  CappedMark: '・上限まで',

  /** §5 重い宝箱: the cost line. */
  ChestLine(cost) {
    return `重い宝箱: 最大スタミナ −${cost}（一時的な変化）`;
  },

  /** §5 重い宝箱's contents in the table's order: 「カード 1 枚 + 頁 1 枚 + 消耗品 1 個」. */
  ChestContents(contents) {
    const parts = [];
    if (contents.Cards > 0) parts.push(`カード ${contents.Cards} 枚`);
    if (contents.Pages > 0) parts.push(`頁 ${contents.Pages} 枚`);
    if (contents.Consumables > 0) parts.push(`消耗品 ${contents.Consumables} 個`);
    return `中身: ${parts.join(' + ')}`;
  },

  /** §5 特殊な食事: the gain and the cost. */
  MealLine(gain, cost, percent) {
    return `特殊な食事: 最大スタミナ +${gain}（一時的な変化）・HP −${cost}（最大の ${percent}%）`;
  },

  /** §5: 「どちらを選んでも刻限は 1 減ります」. */
  EventChoiceNote: '受ける / 受けない を選べる。どちらでも刻限は 1 減る',

  /** miasma_and_battle_v4.md §5: the temporary changes share ±4. */
  TempClampNote: '一時的な変化は、休息の +2 と合わせて ±4 まで',

  /** §5: 「中身は隣接したときに見えます」 — a far event node keeps its kind only. */
  EventHidden: '隣に来ると、何が起きるかが分かる',

  AcceptLabel: '受ける',

  DeclineLabel: '受けない',

  /** The event card's lead, after the step paid its 刻限. */
  EventLead(name) {
    return `${name}がある。受けるか、受けずに通り過ぎるかを選ぶ。`;
  },

  /** The result line of a declined event. */
  EventDeclined: '受けずに通り過ぎた',

  /** Under the 受けない button: the 刻限 was paid on the way in. */
  DeclineNote: '何も変わらない（刻限はもう払った）',

  /** A chest's card on the journal's dungeon page. */
  RecordCards(n) { return `カード ${n} 枚`; },

  /** 「HP 30 → 40」 and the like, for an event's result and a consumable's forecast. */
  HpChange(before, after) { return `HP ${before} → ${after}`; },

  MaxStaminaChange(before, after) { return `最大スタミナ ${before} → ${after}`; },

  StaminaChange(before, after) { return `スタミナ ${before} → ${after}`; },

  MiasmaChange(before, after) { return `瘴気 ${before}% → ${after}%`; },

  /** A chest's finds on the result card. */
  FoundCards(n) { return `カード ${n} 枚から 1 枚を選べる`; },

  FoundPages(n) { return `希少な頁 ${n} 枚`; },

  FoundConsumable(name) { return `消耗品: ${name}`; },

  /** §4.3: 「すべての山が尽きたときは、札の代わりに消耗品を出します」. */
  PileSpent: '札の山が尽きていたので、札の代わりに消耗品が入っていた',

  /** The journal's dungeon page row of this layer's events. */
  EventsRow: '出来事',

  /** One event on the journal's dungeon page. */
  EventRecord(name, accepted, summary) {
    return accepted ? `${name}: ${summary}` : `${name}: 受けなかった`;
  },

  /** What an event did, on its node once walked. */
  EventDone(accepted, summary) {
    return accepted ? `結果: ${summary}` : '結果: 受けなかった';
  },

  NoEvents: 'まだ何も起きていない',

  // ---- the slots (tools_and_consumables_v4.md §3, encounters_and_rewards_v4.md §4.3)

  /** A slot whose item would change nothing now. */
  NoEffectNow: 'いま使っても何も変わらない',

  /** 標の石 with nowhere to land, or no 刻限 to pay. */
  NoJumpNow: '跳べるのは、刻限が残る探索中だけ',

  /** The hint while a 標の石 is aimed. */
  AimHint: '標の石: 跳ぶ先の、まだ踏んでいないノードを選ぶ（刻限 1 を使う。Esc でやめる）',

  /** A node 標の石 can land on. */
  JumpNote: '標の石で跳べる。入ると刻限を 1 使う',

  JumpLabel: '跳ぶ',

  JumpRestLabel: '跳んで休む',

  JumpTrainLabel: '跳んで訓練する',

  SwapTitle: '消耗品の枠が埋まっている',

  /** The swap card's lead. */
  SwapLead(name) {
    return `${name}を拾った。入れ替える枠を選ぶか、諦める。消耗品は拠点へ持ち帰らない。`;
  },

  SwapButton(name) { return `${name}と入れ替える`; },

  LeaveFoundLabel: '諦める',

  // ---- the tools (tools_and_consumables_v4.md §2)

  SideExploration: '探索',

  SideBattle: '戦闘',

  /** 刻限の砂 in the band: 「刻限の砂 +1」. */
  TimeBonus(name, amount) { return `${name} +${amount}`; },

  /** 防瘴の面 beside the gauge: 「防瘴の面 濃さ 2 → 1」, or 「防瘴の面 濃さは下限 1 のまま」 (seven_layers_v4.md §3.2). */
  Relief(names, density, effective) {
    return effective < density ? `${names} 濃さ ${density} → ${effective}` : `${names} 濃さは下限 ${effective} のまま`;
  },

  /**
   * The journal's density row when the tools would take the density below its floor (seven_layers_v4.md §3.2
   * 「濃度は 1 より下がりません」): 「層の濃さ 2 − ツール 2、下限 1」.
   */
  ReliefFloor(density, relief, floor) {
    return `層の濃さ ${density} − ツール ${relief}、下限 ${floor}`;
  },

  /**
   * The journal's page of the layer-two carving (seven_layers_v4.md §5 「拾うと起きる … 手記に頁が 1 つ増える」; what it
   * shows: world-v1.md §5, 「二層の柱の彫りが竜の形をしている」; no battle effect, §5.2).
   */
  CarvingPageTitle: '秘跡の彫り',
  CarvingPageRows: Object.freeze([
    Object.freeze(['', '第 2 層の柱に、竜の形をした彫りがあった。']),
    Object.freeze(['層の名', '■■の秘跡 は、竜神の秘跡 と読めた']),
    Object.freeze(['戦闘', '効かない']),
  ]),

  /** A 死亡地点の痕跡 node once this life collected it (docs/INHERITANCE.md R-09 / R-10). */
  TraceCollected: 'この痕跡は回収した',

  /** 灯芯の欠片 on the interlude line: 「（最大の 45%・灯芯の欠片 +15%）」. */
  InterludeShare(percent, name, bonus, capped = false) {
    const mark = capped ? ExplorationText.CappedMark : '';
    return bonus > 0 ? `（最大の ${percent}%・${name} +${bonus}%${mark}）` : `（最大の ${percent}%${mark}）`;
  },

  /** A tool's effect in numbers on the inventory. */
  ToolEffect: Object.freeze({
    Density: (n) => `濃さ −${n}`,
    Time: (n) => `刻限 +${n}`,
    Survey: () => '相手を選ぶ',
    Interlude: (n) => `休憩 +${n}%`,
  }),

  // ---- 写しの帳 (tools_and_consumables_v4.md §2.2)

  /** On a 情報収集 node when 写しの帳 is carried. */
  SurveyChooseLine: '写しの帳: 開示度を上げる相手を自分で選べる',

  SurveyTitle: '情報収集の相手を選ぶ',

  SurveyLead: '写しの帳: この層の敵から 1 種を選び、手記の開示度を 2 段目へ上げる。',

  /** One enemy on the survey card. */
  SurveyButton(name, disclosure) { return `${name}（開示度 ${disclosure} → 2）`; },

  /** The journal's survey row once the player chose. */
  SurveyChosen(name) { return `済ませた。${name}が開示度 2 になった（写しの帳）`; },

  /** The journal's survey row once the layer chose. */
  SurveyDone(name) { return `済ませた。${name}が開示度 2 になった`; },

  /** The survey found every enemy behind this map at 開示度 2 already (EnemyIntel.SurveyTarget gave none). */
  SurveyNothing: '済ませた。この層の敵は、みな開示度 2 だった',
};
