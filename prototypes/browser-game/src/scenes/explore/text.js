// Scene text: the few Japanese lines the exploration screen prints that the ported bridge does not
// write (ExplorationText / ExplorationScreenBuilder / Expedition / Inheritance cover everything
// else). Fixed labels stay within 10 characters. Numbers are passed in, never worked out here.

export const SceneText = Object.freeze({
  /** dungeon_exploration_v4 §6.3: the five standings by the names the spec gives them. */
  Standing: Object.freeze({ Current: '今いる', Reachable: '進める', Revisit: '戻れる', Resolved: '踏んだ', Far: '遠い' }),

  // after a battle (Expedition.Return)
  BattleWon: '戦いに勝った',
  BattleLost: '戦いに敗れた',
  Learned: '覚えた札',
  RankUps: '習熟が上がった札',
  /** A 段 gained; a card the 段 renamed names both (「突き → 鋭い突き（0 → 1 段目）」, X-15). */
  RankUp: (name, from, to, was = null) => (was && was !== name ? `${SceneText.RankUpNames(name, was)}（${SceneText.RankUpSteps(from, to)}）` : `${name}　${SceneText.RankUpSteps(from, to)}`),
  /** The rank-up line's two parts, for a narrow column that breaks between them (the result card). */
  RankUpNames: (name, was = null) => (was && was !== name ? `${was} → ${name}` : name),
  RankUpSteps: (from, to) => `${from} → ${to} 段目`,
  Journal: '手記に書き足した敵',
  JournalLine: (name, level, max) => `${name}　開示度 ${level} / ${max}`,
  Found: '拾った物',
  Pages: (n) => `希少な頁 ${n} 枚`,
  /** Under a reward's or an event's 希少な頁 and on the map's page chips: the pages have no content yet (#107). */
  PagesNote: '頁の中身はまだ書かれていない。手記に数が残るだけ',
  /** The pages handed down (a survivor's journal, a trace): the life's, but not found here (INHERITANCE.md R-27). */
  Inherited: '受け継いだ物',
  InheritedNote: '前の生から受け継いだ頁。手記に入っている',
  PickOne: '1 枚を選んで覚える',
  LearnButton: 'この札を覚える',
  SkipButton: '受け取らない',
  BackToMap: '地図へ戻る',
  NothingFound: '拾った物はない',
  /** encounters_and_rewards_v4 §4.3: a card the reward wanted came as a consumable. */
  PileSpentBattle: '札の山が尽きていたので、札の代わりに消耗品を拾った',
  /** §4.3 「デッキの上限の中で」: a learned card that did not fit the deck is owned only. */
  Benched: (names, max) => `デッキが上限（${max} 枚）のため、${names} はデッキに入らず所持カードに入った`,
  OfferBenched: (max) => `デッキが上限（${max} 枚）なので、覚えた札は所持カードにだけ入る`,

  // the trace (Inheritance through ctx.inheritance)
  TraceLeaveTitle: '何も受け取らずに去りますか',
  TraceLeaveBody: (picked) => 'この痕跡は回収され、ここに残った遺産は失われます。' + (picked > 0 ? `選んだ ${picked} 件も受け取りません。` : ''),
  TraceLeaveCancel: '選び直す',
  TakenTitle: '受け取った遺産',
  TraceLost: (n) => `選ばなかった ${n} 件は失われた`,

  // the interlude's look ahead
  NextLayer: '次の層',

  // the encounter banner before a fight
  FightCount: (n) => `${n} 連戦`,

  // 習熟訓練 (Expedition.Train)
  TrainTitle: '訓練する札を選ぶ',
  TrainLead: '選んだ札に、習熟の刻みが 1 入る。「あとで」にした刻みは、この層を出るまで使える。',
  TrainButton: '訓練する',
  TrainLater: 'あとで',
  /** 次の層へ降りる with marks left: the marks are this layer's (the next layer starts with none). */
  TrainBeforeDescend: '降りる前に訓練する',
  TrainLeadDescend: '訓練の刻みは、次の層へ降りると失われる。降りる前に、刻みを入れる札を選べる。',
  TrainAndDescend: '訓練して降りる',
  TrainDiscard: '刻みを捨てて降りる',
  TrainStay: 'やめる',
  Rank: (n) => `${n} 段目`,
  /** The training card's rows and preview (X-15): the 刻み toward the next 段, from MasteryView.masteryProgress. */
  TrainTicks: (ticks, need) => `刻み ${ticks} / ${need}`,
  TrainTicksTop: '最上の段',
  TrainTicksCapped: '2 段で止まる',
  TrainTicksFlat: '段は上がらない',
  TrainBaseName: (name) => `元の名：${name}`,
  TrainNext: (ticks, need, rank) => `訓練すると 刻み ${ticks} / ${need}${ticks >= need ? `・${rank} 段目へ` : ''}`,
  TrainNextName: (name) => `次の名：${name}`,
  TrainCappedNote: (holder) => `3 段目は「${holder}」が持っているので、この札は 2 段で止まる`,
  // what the training did, said on the map once the card closes (X-15)
  TrainedKicker: '訓練した',
  Trained: (name, ticks, need) => (need ? `${name}に習熟の刻み 1　刻み ${ticks} / ${need}` : `${name}に習熟の刻み 1`),

  // the inventory
  Use: '使う',
  FoundNote: '拾った消耗品は、まだ枠に入れられない',
  FoundNoteTrial: '拾った頁は手記に入る',

  // fallback overlay pages (only when the app mounts none)
  Close: '閉じる',
  SaveQuit: '保存して閉じる',

  // trial mode (docs/EXTENSIONS.md 「探索の持ち物とイベント」): presentation labels only; the rules' words
  // are ExplorationText's
  StopAim: 'やめる',
  Swap: '入れ替える',
  FoundItem: '拾った物',
  Carried: '持っている物',
  EventKicker: 'イベント',
  SurveyKicker: '情報収集',
  ToolNote: (name, side, note) => `${name}（${side}）: ${note}`,
  SlotHint: (note, extra) => (extra ? `${note}　→ ${extra}` : note),

  // the key legend
  KeyEnter: 'Enter',
  KeyShift: 'Shift',
  KeySelect: '選ぶ',
  KeyToggle: '付ける・外す',
  KeyScroll: '一覧を見る',
  KeyItems: '消耗品',
});
