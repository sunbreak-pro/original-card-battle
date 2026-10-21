// Scene text: the few words the meta screens print that no bridge module holds yet. Everything else
// (every name, number, rule line and sentence) comes from the ported bridge or core: Inheritance
// (InheritanceText, hallView, endingView), DeckBuilder, DepartureBuilder, CardFaceText / CoreText,
// JournalBook / JournalTerms, BattleSpeed, ItemCatalogue. Fixed labels stay within 10 characters.

export const MetaText = Object.freeze({
  // ---- title (concept-v3.md §1: 短縮形) ----
  GameName: 'Original Card Battle',
  TaglineLines: Object.freeze(['死ぬたびに人は変わる。', 'だが、技と記憶は次の生へ。']),
  NewGame: 'はじめから',
  Continue: 'つづきから',
  Settings: '設定',
  NoSave: '保存された生は無い',
  /** つづきから when a save is stored but could not be read (the router's notice under the menu says more). */
  Unreadable: '記録を読み込めない',
  TitleKeys: '↑↓ 選ぶ　Enter 決める',

  // ---- 継承の間 ----
  NameSeal: Object.freeze(['襲', '名']),
  HallKeys: '←→ 痕跡を選ぶ　Enter 出立　Esc 設定',
  // the layer column's marks (review: three looks, no key): the hover line and the key under the column
  MarkTitle: (heirLine, ending, trace) => `${heirLine}　${ending}・${trace}`,
  MarkUntaken: '未回収の痕跡',
  MarkTaken: '回収済み',
  MarkAlive: '生きて閉じた',
  HallKeysNoTrace: 'Enter 出立　Esc 設定',
  // The key line under the hall: ←→ only with two or more traces, ↑↓ only while the trace's list runs past the panel.
  HallKeysFor: (traces, scrolls) => [traces ? '←→ 痕跡を選ぶ' : null, scrolls ? '↑↓ 候補を読む' : null, 'Enter 出立', 'Esc 設定']
    .filter(Boolean).join('　'),
  JournalKey: 'J',
  BackToTitle: 'タイトルへ',
  // A past life's trace once someone visited it: visiting collects it even with 「何も受け取らない」
  // (INHERITANCE.md R-10, 試作で決めたこと 5), and the lineage keeps who collected it, not what was picked.
  TraceCollected: (generation, name) => `痕跡：第 ${generation} 代 ${name}が回収した`,

  // ---- the ending card ----
  // A trace left behind an older one on the same layer (INHERITANCE.md 試作で決めたこと 4; the hall's
  // 「同じ層の古い痕跡を回収すると現れる」).
  TraceWaits: (layer, generation, name) => `第 ${layer} 層には第 ${generation} 代 ${name}の古い痕跡がある。それが回収されると現れる`,
  TraceWaitsMany: (layer, count) => `第 ${layer} 層には古い痕跡が ${count} 件ある。それらが回収されると現れる`,

  // ---- 出立 (Unity DepartureScreen.cs / DeckSelectScreen.cs literals) ----
  StepDeck: 'デッキを組む',
  StepSupplies: '出立の支度',
  ToSupplies: '出立の支度へ',
  PrevPage: '前のページ',
  NextPage: '次のページ',
  Prototype: '試作デッキ',
  Random: 'ランダム',
  Clear: '空にする',
  PickCardHint: 'カードを押すと、効果の文が出ます。',
  DeckCount: (n) => `デッキ: ${n} 枚`,
  CardsUnit: '枚',
  NotOwned: '未所持',
  InDeck: 'デッキの札',
  SideExplore: '探索',
  SideBattle: '戦闘',
  EmptySlot: '空き',
  DeckListHeading: 'いまのデッキ',
  // Beside the heading while the deck list runs past its three rows (the deck step's arrows move the card cursor).
  DeckListMore: 'Shift+↑↓ 続きを読む',
  StartHeading: '開始の位置',
  DeckKeys: '←↑↓→ 札を選ぶ　＋ − 枚数　PgUp PgDn ページ',
  SupplyKeys: 'Tab 項目を移る　Space 押す',
  EscBack: 'Esc 戻る',
  EscSettings: 'Esc 設定',

  // ---- 所持カード (CAMP_FACILITIES_DESIGN.md §3.4) ----
  OwnedTitle: '所持カード',
  OwnedCount: (n) => `${n} 種`,
  OwnedKeys: '←↑↓→ 選ぶ　PgUp PgDn ページ　C か Esc で閉じる',
  OwnedNone: '持っている札は無い。',
  Mastery: '習熟',
  RankLine: (rank) => `${rank} 段`,
  NextRank: '次の段まで',
  TicksLine: (ticks, need) => `刻み ${ticks} / ${need}`,
  TopRank: '最上の段',
  NoGrowth: '習熟で名は変わらない',
  NextName: (name) => `次の名：${name}`,
  // concept-v3 §14 1 つの生の段数: 「3 段目を持てるのは 1 枚だけ。ほかは 2 段目まで」.
  TopTaken: (holder) => `3 段目を持てるのは 1 つの生で 1 枚だけ。この生では「${holder}」が持っているので、この札は 2 段で止まる。`,
  TicksSoFar: (ticks) => `刻み ${ticks}`,
  BaseName: (name) => `元の名：${name}`,
  Lasting: '永続能力',
  NoLasting: 'なし',
  Close: '閉じる',

  // ---- 設定・セーブ (CAMP_FACILITIES_DESIGN.md §3.5) ----
  SettingsTitle: '設定・セーブ',
  Speed: '戦闘の速さ',
  ShortFx: '演出短縮',
  ShortFxOn: '入れる',
  ShortFxOff: '切る',
  ShortFxNote: '演出短縮を入れると、速さに関わらず演出が縮みます。',
  Volume: '音の大きさ',
  Master: '全体',
  Effects: '効果音',
  Ambience: '環境音',
  Mute: '消音',
  MuteOn: '消す',
  MuteOff: '鳴らす',
  SaveQuit: '保存して閉じる',
  SaveNote: '今の状態を保存してゲームを閉じます。生は終わりません。',
  SaveToTitle: '保存してタイトルへ',
  /** The button by where it is pressed: a life (the map, a fight) or 継承の間 / 出立 ('lineage'). */
  SaveQuitOf: (kind) => (kind === 'lineage' ? '保存してタイトルへ' : '保存して閉じる'),
  SaveNoteOf: (kind) => (kind === 'battle' ? '戦闘の途中を保存して閉じます。つづきからで、この場面に戻ります。'
    : kind === 'lineage' ? 'ここまでの記録は保存されています。つづきからで、ここへ戻ります。'
      : '今の状態を保存してゲームを閉じます。生は終わりません。'),
  SettingsKeys: '↑↓ 項目　←→ 変える　Esc 閉じる',

  // ---- 手記 (dungeon_exploration_v4.md §6.7) ----
  EnemyPagePending: '頁の中身は、まだ書かれていない。',
  /** The 用語 page's stance row (X-17): the attribute 「スタンス」 said apart from the rule 「構え」. */
  StanceTerm: 'スタンス',
  StanceTermText: '置くと戦闘のあいだ効き続ける札の属性（水の構え・岩の構え など）。上の「構え」の規則とは別',
  JournalKeys: '←→ 頁　J か Esc で閉じる',
  MemoMine: '自分のメモ',
  MemoInherited: '受け継いだ走り書き',
  // the ダンジョン tab's 希少な頁 (INHERITANCE.md R-27): handed down or found in this life
  RarePagesTitle: '希少な頁',
  LayerShort: (n) => `第 ${n} 層`,
  RarePagesInherited: '受け継いだ頁',
  RarePagesFound: 'この生で拾った頁',
  RarePagesCount: (n) => `${n} 枚`,
  RarePagesPending: '頁の中身は、まだ書かれていない。数に入るだけで、戦いにも探索にも効き目は無い。',
  /** Where the pages go when the life ends (INHERITANCE.md R-13, 試作で決めたこと 9). */
  RarePagesFate: '生きてこの生を閉じれば、手記と一緒に次の生へ渡る。中身の無い頁は痕跡の遺産の候補にならない。',

});
