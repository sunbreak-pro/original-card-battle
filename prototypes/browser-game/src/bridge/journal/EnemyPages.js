// JS extension (not in the C#; docs/EXTENSIONS.md 「手記の敵の頁と地図の敵の情報」): the journal's enemy pages,
// the content JournalBook.cs leaves to #107. Every text is transcribed byte for byte from
// enemy_document/enemy_roster_v4.md (the `journal` of §1.1 per enemy, the 条件（手記の文） column of the
// bosses' adaptation tables §4.4 / §5.4 / §6.4, the action tables' 構え and 崩し rows, the bosses'
// 専用の状態 tables §4.1 / §5.1 / §6.2), one entry per enemy of src/core/battle/Enemies.js. Names and HP
// come from the core (the same EnemyDef the battle reads), so the page and the battle never disagree.
//
// Which row needs which 開示度 (battle_document/battle_ui_ux_v2.md §7, the 「手記の敵の頁」 column; §11.3):
//   0  名前と HP 最大だけ                                       → 名前, HP 最大
//   1  間合いの帯（0 / 1〜2 / 3 以上）ごとの傾向（決定木）          → 得意な間合い, 間合い 0, 間合い 1〜2, 間合い 3 以上
//   2  弱点・特殊行動・崩しの有無。ボスは適応条件の全文            → 弱点, 特殊行動, 適応条件
// The rows run in the order of battle_ui_ux_v2.md §2.4 「行の構成は 名前 / 間合いの帯ごとの傾向 / 弱点 /
// 特殊行動 / 適応条件（ボスだけ、開示度 2 で全文）/ 自分のメモ」; the memo row is the drawer's to add
// (WithMemo), since the memo is the player's text, not the roster's. A row the page has not reached
// prints 「？」 (JournalPage.TextOf).
import { JournalContext, JournalPage, JournalRow, JournalTab } from './JournalBook.js';
import { Enemies } from '../../core/battle/Enemies.js';

// ---------------------------------------------------------------- how a special line reads

/** The roster's 予兆 word of an action (enemy_roster_v4.md §1.2: 攻撃 / 防御 / 移動 / 構え / 技). */
function act(actionId, name, omen, effect, trait = null) {
  return Object.freeze({ ActionId: actionId, Name: name, Omen: omen, Effect: effect, Trait: trait });
}

/** A boss's own status: the 状態 / 向き / 効き方 / 持続 columns of its 専用の状態 table. */
function status(name, effect, lasts) {
  return Object.freeze({ Status: name, Effect: effect, Lasts: lasts });
}

function entry(e) {
  return Object.freeze({
    Source: e.Source,
    FavoredGap: e.FavoredGap,
    Gap0: e.Gap0,
    Gap1to2: e.Gap1to2,
    Gap3Plus: e.Gap3Plus,
    Weakness: e.Weakness,
    Special: Object.freeze(e.Special ?? []),
    Adaptations: Object.freeze(e.Adaptations ?? []),
  });
}

// ---------------------------------------------------------------- the roster's pages

// FavoredGap: each section's heading 「— 得意な間合い …」. Gap0 / Gap1to2 / Gap3Plus / Weakness: the
// section's 「手記:」 line (§2 / §3) or 「手記の頁」 subsection (§4.6 / §5.6 / §6.7): 間合い 0「…」。1〜2「…」。
// 3 以上「…」。弱点「…」。 Special: the action table's rows whose 予兆 is 構え (the stance, §1.2 「スタンスの
// 行動は 1 戦闘で 1 回だけ」) or whose 効果 gives 崩し (§7 「崩しの有無」), then the boss's 専用の状態.
// Adaptations: the 条件（手記の文） column, every row in table order (§1.5 「開示度 2 では条件そのものが
// 手記の頁に書かれます」).
const ROSTER = Object.freeze({
  polearm_warped: entry({
    Source: 'enemy_roster_v4.md §2.1',
    FavoredGap: '1〜2',
    Gap0: '柄尻で 2 マス押し返す。Guard が無いと重い。後ろが端なら壁にも当たる',
    Gap1to2: '払い斬り。2 マス空いていると +3',
    Gap3Plus: '踏み込んで 1 マス寄ってくる',
    Weakness: '隣へ入れば払い斬りは届かないが、1 ターンで押し返される。押し込みの予兆が出たターンに鈍足を付けると、押し返しが 1 マスに減る。Guard を残して受けてもよい',
  }),
  shadow_hound: entry({
    Source: 'enemy_roster_v4.md §2.2',
    FavoredGap: '0',
    Gap0: '噛みつき。出血が 2 スタック残る',
    Gap1to2: '跳びかかって隣まで詰める。2 マス空いていると +3',
    Gap3Plus: '駆け寄って 2 マス詰める',
    Weakness: 'HP 80 で、通常の敵では低め。離れても 1 ターンで詰められる。噛みつきの予兆が出たら 1 マス下がれば届かない。跳びかかりの予兆が出たら、狙うマスの外（隣か 3 以上）へ動けば空振りになる',
  }),
  rusted_revenant: entry({
    Source: 'enemy_roster_v4.md §2.3',
    FavoredGap: '0',
    Gap0: '大振り。こちらのスタミナが少ないと重くなる',
    Gap1to2: '押し込みで当てながら 1 マス寄る',
    Gap3Plus: '開幕に鉄の身を置き、のし歩いて寄る',
    Weakness: '鉄の身を置く前の 1 ターンが最も柔らかい。大振りは隣にしか届かないので、予兆を見て 1 マス下がれば空振りになる。下がっても押し込みとのし歩きで戻ってくるので、戻った直後の 1 ターンで叩く。崩しでスタミナを削ると大振りを出せない',
    Special: [act('iron_body', '鉄の身', '構え', '毎ターン開始に Guard +3')],
  }),
  crossbow_hunter: entry({
    Source: 'enemy_roster_v4.md §2.4',
    FavoredGap: '2〜3',
    Gap0: '蹴り離して 2 マス押し返す',
    Gap1to2: '撃ってから 1 マス下がる',
    Gap3Plus: '弩。威圧が付いて次の一手が 3 下がる',
    Weakness: '弩は 2 以上ならどこへでも届き、下がっても逃げられない。予兆を見て 1 以下へ詰めれば空振りになる。詰めても蹴り離されるので、押し返された次のターンに戻れるよう、前へ出る札を 1 枚は手札に残す。右端に追い詰めれば、退き撃ちで下がれなくなる',
  }),
  mist_archer: entry({
    Source: 'enemy_roster_v4.md §2.5',
    FavoredGap: '2〜3',
    Gap0: '靄に溶けて 2 マス弾く',
    Gap1to2: '同じく靄に溶ける。2 マス空けていれば弾かれず、Guard が付くだけ',
    Gap3Plus: '靄の矢。3 以上だと +5 で、疲労が付いて回復が減る',
    Weakness: '矢は 2 以上ならどこへでも届き、下がるほど重い。予兆を見て 1 以下へ入れば空振りになるが、靄に溶けるで弾き返される。間合い 2 に留まって、1〜2 に届く札で削るのが近道。HP 72',
  }),
  twin_blade_warped: entry({
    Source: 'enemy_roster_v4.md §2.6',
    FavoredGap: '0',
    Gap0: '二段斬り。続けて 2 回斬ってくる',
    Gap1to2: '踏み込み斬りで隣まで詰める',
    Gap3Plus: '刃を十字に構えたまま 2 マス詰めてくる。スタミナに余裕があれば見切りが付く',
    Weakness: '二段斬りは隣にしか届かない。予兆を見て 1 マス下がれば空振りになる。十字受けの見切りは、待っても消えない。竜兵の Guard が受け止めた一撃に、受け止めた分の半分を返してくる。返すたびに 1 つ減る。返る量は竜兵の Guard で決まるので、重い札で殴っても増えない',
  }),
  polearm_crystal: entry({
    Source: 'enemy_roster_v4.md §2.7',
    FavoredGap: '1〜2',
    Gap0: '突いてから 2 マス退く。Guard が無いと +3',
    Gap1to2: '大穂の突き。重いが毎ターンは出せない',
    Gap3Plus: '突き進んで 2 マス詰めてくる。3 以上だと +3',
    Weakness: '大穂の突きは隣に届かない。予兆を見て隣へ入れば空振りになる。隣に入ると引き突きで退かれるので、退いた先へ届く札を残しておく。右端へ押し込めば退けなくなる。押す / 引くが効く',
  }),
  armored_warden: entry({
    Source: 'enemy_roster_v4.md §3.1',
    FavoredGap: '0',
    Gap0: '打ち下ろしで崩し、盾打ちで鈍足。隣にいると崩しが 1 増える',
    Gap1to2: '盾で押し出ながら寄ってくる',
    Gap3Plus: '鉄壁を置いてから、盾を掲げて寄ってくる',
    Weakness: '押しても引いても動かない（大きさ 2）。鉄壁を置く前に叩く。盾打ちは隣にしか届かないので、予兆を見て 1 マス下がれば鈍足を避けられる。鈍足が付いた後は 1 マスでは下がれないので、下がるなら最初の盾打ちの前に。打ち下ろしは 1 マス下がっても届くので、Guard を残して受ける。2 手目はいつも列 1',
    Special: [
      act('iron_wall', '鉄壁', '構え', '毎ターン開始に Guard +3'),
      act('helm_splitter', '打ち下ろし', '攻撃', '威力 8、崩しを 1 付与する', '間合い 0: 崩し +1'),
    ],
  }),
  pack_alpha: entry({
    Source: 'enemy_roster_v4.md §3.2',
    FavoredGap: '0',
    Gap0: '引き裂き。出血が残る。続けて 1 マス押しやられ、鈍足が付く',
    Gap1to2: '飛びかかって詰める。2 マス空いていると +5',
    Gap3Plus: '狩りの構えを置いてから、飛びかかってくる',
    Weakness: '引き裂きは隣にしか届かない。予兆を見て 1 マス下がれば空振りになる。ただし飛びかかりは 1〜3 に届くので、下がり過ぎると +5 で詰められる。1 マスだけ空けて受け、端へ押しやられる前に前へ出る。押す / 引くが効く',
    Special: [act('hunt_stance', '狩りの構え', '構え', '相手との間合いが 0 のときアタック威力 +3')],
  }),
  polearm_unyielding: entry({
    Source: 'enemy_roster_v4.md §3.3',
    FavoredGap: '1〜2',
    Gap0: '柄で 2 マス突き放してから突く。後ろが端だと壁に当たる',
    Gap1to2: '長穂の突き。2 手目は甲で受けるだけ',
    Gap3Plus: '穂先を据えてから、穂で 2 マス掛け寄せる。掛け寄せた後は突いてくる',
    Weakness: '1〜2 に立っていると突きは 1 回だけで、もう 1 手は守りになる。1〜2 に届く札で削るのが近道。突き放しと掛け寄せは鈍足で 1 マスに減る。掛け寄せが 1 マスになれば、5 から引かれても突きが届かない。隣へ入るのは後ろに 2 マス空いているときだけにする。押す / 引くが効く',
    Special: [act('set_spear', '穂先を据える', '構え', '相手との間合いが 1 以上のとき攻撃威力 +3')],
  }),
  miasma_priest: entry({
    Source: 'enemy_roster_v4.md §4',
    FavoredGap: '2 以上',
    Gap0: '鎌首の打ち込み。隣では +5。2 ターン居続けると払いのけで 2 マス押し返す。Guard を切らすと払いのけが重い',
    Gap1to2: '瘴気の矢。疲労が付く。2 マス空けていると縛りの言葉も届き、次のターンは移動の効果が止まる。縛りは 1 ターンおきにしか来ない',
    Gap3Plus: '瘴気の儀と帳。儀式は最大スタミナを削る。帳は相手が 2 マス以上離れているときだけ効く。離れていても瘴気の矢は届き、3 マス以上空けると重くなる',
    Weakness: '儀式と言葉は 2〜4 にしか届かない。1 以下へ入れば空振りし、帳も止まる。セルクは動かないので、間合いは自分の足で決められる。ただし隣に 2 ターン居続けると払いのけが先頭に来るので、1 マス空けて戦う。アタックを 3 回続けると帳を張り直すので、その隙に動く。HP 70 から儀式が増える',
    Special: [
      act('ward', '瘴気の帳', '構え', '相手との間合いが 2 以上のターン開始に Guard +5'),
      status('瘴気纏い', 'この戦闘中、最大スタミナ −1。2 スタックまで重なり −2', '戦闘終了まで'),
      status('呪縛', '付いている間、自分の移動の効果が働かない。移動が中心の札は使えない', 'スタック分のターン'),
    ],
    Adaptations: [
      'アタックを 3 回続けて受けると、とぐろを締めて帳を張り直す',
      'Guard を 9 以上で 2 ターン受け続けると、崩しを狙う',
      '隣に 2 ターン居続けると、払いのけで押し返す',
      'HP が半分を切ると、儀式を毎フェーズ行う',
    ],
  }),
  abyss_angler: entry({
    Source: 'enemy_roster_v4.md §5',
    FavoredGap: '0',
    Gap0: '引きずり込みが重い。鉤爪が乗っていると更に +3。呼び声で深みが付き、隣にいる間は回復が減る',
    Gap1to2: '手繰り寄せて隣まで引く。鈍足が付く',
    Gap3Plus: '鉤縄と手繰り寄せ。鉤縄を受けると後ろへ下がる手が止まり、そのまま引き寄せられる',
    Weakness: '鉤縄は 2 以上の相手にだけ届き、呼び声は 0〜1 の相手にだけ届く。どちらの間合いにも罠があるので、同じ間合いで 2 ターン終えない。鉤爪は下がろうとするたびに 1 減るので、剥がす 1 ターンと下がる 1 ターンを続けて用意する。糸を緩めた次のフェーズは呼び声が来るので、そこで詰める。押しても引いても動かない（大きさ 2）。HP 90 で本体が前へ出る',
    Special: [
      act('deep_water', '淀みを張る', '構え', '相手との間合いが 1 以下のターン開始に Guard +5'),
      status('鉤爪', '後ろへ動く手を止める（後ろへ n の面が 0 マスになる）。止めるたびに 1 減る（使うと減る型）。前へ動く手は通る', '戦闘終了まで'),
      status('深み', 'ガルドとの間合いが 0 でターンを始めると、回復 −1。2 スタックで −2（0 が下限）', '戦闘終了まで'),
    ],
    Adaptations: [
      '2 マス以上離れて 2 ターン居続けると、鉤縄と手繰り寄せを続ける',
      '鉤爪を 2 回外されると、糸を緩めて張り直す',
      '隣で 2 ターン粘ると、引きずり込みを続ける',
      'HP が半分を切ると、自分から淵を出て食い込む',
    ],
  }),
  distortion_root: entry({
    Source: 'enemy_roster_v4.md §6',
    FavoredGap: '2 以上',
    Gap0: '捻じるの崩しと根を張る。根張りが乗ると根の横払いが +5',
    Gap1to2: '根の横払いと根を張る。伸びる根で 1〜2 へ寄せられる',
    Gap3Plus: '棘と枯らしの息。棘は 3 以上だと +5、息を受けると回復が細る。状態を 2 つ付けると、樹皮を置いて振り払う',
    Weakness: '根を張るは 0〜1 に、枯らしの息は 2 以上にだけ届く。どの間合いにも罠があるので、同じマスで 2 ターン終えない。攻撃だけで 4 枚続けても、移動を 2 ターン続けても咎められるので、2 ターンに 1 度動く。HP 133 で本体がせり上がって息がどこへでも届き、66 で根と息が同じフェーズに来る',
    Special: [
      act('twist', '捻じる', '攻撃', '威力 5、崩しを 1 付与する', '崩し後: 相手のスタミナ 3 未満で威力 +3'),
      act('bark', '樹皮', '構え', '相手との間合いが 2 以上のターン開始に Guard +5'),
      status('根張り', '同じマスで 2 ターン続けてターンを終えると、ターン開始に HP −4。2 スタックで −8', '戦闘終了まで'),
      status('枯らし', 'ターン開始の回復 −1。2 スタックで −2。疲労と重なっても、回復は 1 より下がらない（1 が下限）', '戦闘終了まで'),
    ],
    Adaptations: [
      'アタックを 4 回続けて受けると、根を張って足を止める',
      'Guard で 2 ターン受け切ると、崩しに回る',
      'スタンスを置くと、その構えを枯らしにくる',
      '状態を 2 つ付けられると、身を固めて振り払う',
      '2 ターン続けて動くと、先回りする',
      'HP が 3 分の 2 を切ると、せり上がって力を枯らす',
      'HP が 3 分の 1 を切ると、根と息を同じフェーズで使う',
    ],
  }),
});

/** The disclosure each row needs (battle_ui_ux_v2.md §7, the 「手記の敵の頁」 column). */
const AtMeeting = 0;
const AtEncounter = 1;
const AtObservation = 2;

export const EnemyPages = {
  // ---- the rows' labels (battle_ui_ux_v2.md §2.4 「行の構成」, §7) ----
  NameLabel: '名前',
  MaxHpLabel: 'HP 最大',
  FavoredGapLabel: '得意な間合い',
  Gap0Label: '間合い 0',
  Gap1to2Label: '間合い 1〜2',
  Gap3PlusLabel: '間合い 3 以上',
  WeaknessLabel: '弱点',
  SpecialLabel: '特殊行動',
  AdaptationsLabel: '適応条件',
  MemoLabel: '自分のメモ',

  /** The 特殊行動 row of an enemy with no stance, no 崩し and no status of its own. */
  NoSpecial: 'なし',

  /** The memo row before anything is written: the exploration journal's memo page says the same. */
  MemoEmpty: 'メモはまだありません。',

  /** battle_ui_ux_v2.md §2.4: 自分のメモ（読むだけ、末尾に「戦闘中は書けない」）. */
  MemoLocked: '戦闘中は書けない',

  /** What a boss's own status is put on (the 向き column 「相手に」, worded as the terms page words it). */
  ToFoe: '相手に付ける',

  /** The enemies with a page, in roster order (Enemies.All). */
  get Ids() {
    return Enemies.All.map((d) => d.Id).filter((id) => Object.hasOwn(ROSTER, id));
  },

  Has(enemyId) {
    return enemyId != null && Object.hasOwn(ROSTER, enemyId);
  },

  /** The roster's entry for an enemy (frozen). Throws for an enemy with no page. */
  Entry(enemyId) {
    if (!EnemyPages.Has(enemyId)) throw new Error(`EnemyPages: no page for enemy "${enemyId}".`);
    return ROSTER[enemyId];
  },

  /** The page's id: the drawer finds an enemy's page by it (enemy-<id>). */
  PageId(enemyId) {
    return `enemy-${enemyId}`;
  },

  /** The enemy's name as the battle shows it (the core's EnemyDef). */
  NameOf(enemyId) {
    return Enemies.ById(enemyId).Name;
  },

  /** One line of the 特殊行動 row: 「鉄の身（構え）: 毎ターン開始に Guard +3」 / 「瘴気纏い（相手に付ける）: …（戦闘終了まで）」. */
  SpecialLine(item) {
    if (item.Status != null) return `${item.Status}（${EnemyPages.ToFoe}）: ${item.Effect}（${item.Lasts}）`;
    return `${item.Name}（${item.Omen}）: ${item.Effect}` + (item.Trait ? `。${item.Trait}` : '');
  },

  /** The 特殊行動 row's text: one line per stance / 崩し action / own status, or 「なし」. */
  SpecialText(enemyId) {
    const special = EnemyPages.Entry(enemyId).Special;
    return special.length === 0 ? EnemyPages.NoSpecial : special.map(EnemyPages.SpecialLine).join('\n');
  },

  /** The 適応条件 row's text: every condition of the boss, one a line. Empty for an enemy without. */
  AdaptationsText(enemyId) {
    return EnemyPages.Entry(enemyId).Adaptations.join('\n');
  },

  /**
   * The enemy's journal page at a 開示度 (0..2; JournalPage refuses the rest). Rows above the
   * disclosure print 「？」. Bosses (the roster's entries with adaptations) carry the 適応条件 row.
   */
  Page(enemyId, disclosure) {
    const e = EnemyPages.Entry(enemyId);
    const def = Enemies.ById(enemyId);
    const rows = [
      new JournalRow(EnemyPages.NameLabel, def.Name, AtMeeting),
      new JournalRow(EnemyPages.MaxHpLabel, `${def.MaxHp}`, AtMeeting),
      new JournalRow(EnemyPages.FavoredGapLabel, e.FavoredGap, AtEncounter),
      new JournalRow(EnemyPages.Gap0Label, e.Gap0, AtEncounter),
      new JournalRow(EnemyPages.Gap1to2Label, e.Gap1to2, AtEncounter),
      new JournalRow(EnemyPages.Gap3PlusLabel, e.Gap3Plus, AtEncounter),
      new JournalRow(EnemyPages.WeaknessLabel, e.Weakness, AtObservation),
      new JournalRow(EnemyPages.SpecialLabel, EnemyPages.SpecialText(enemyId), AtObservation),
    ];
    if (e.Adaptations.length > 0) rows.push(new JournalRow(EnemyPages.AdaptationsLabel, EnemyPages.AdaptationsText(enemyId), AtObservation));
    return new JournalPage(EnemyPages.PageId(enemyId), JournalTab.Enemy, def.Name, rows, disclosure);
  },

  /**
   * The 自分のメモ row (battle_ui_ux_v2.md §2.4): the player's memo, read only. In battle it ends on
   * 「戦闘中は書けない」; while exploring it is written on the メモ tab instead.
   */
  MemoRow(memo, context) {
    const text = memo != null && String(memo).length > 0 ? String(memo) : EnemyPages.MemoEmpty;
    return new JournalRow(EnemyPages.MemoLabel, context === JournalContext.Battle ? `${text}\n${EnemyPages.MemoLocked}` : text, AtMeeting);
  },

  /** An enemy page with the 自分のメモ row last; any other page as it is. */
  WithMemo(page, memo, context) {
    if (page == null || page.Tab !== JournalTab.Enemy) return page;
    const rows = page.Rows.filter((r) => r.Label !== EnemyPages.MemoLabel).concat([EnemyPages.MemoRow(memo, context)]);
    return new JournalPage(page.Id, page.Tab, page.Title, rows, page.Disclosure);
  },

  // ---- the map's words (dungeon_exploration_v4.md §6.3) ----

  /**
   * The enemies of one encounter as the node's 「敵:」 line names them: an encounter's fights in
   * order, joined by 「 → 」 (fought one after the other), the enemies of one fight by 「 + 」 (fought
   * together) — the marks of enemy_roster_v4.md §9 「列の「→」は…続けて戦うこと…「+」は同時に戦うこと」.
   * `fights` is the display name of each enemy, fight by fight.
   */
  EncounterName(fights) {
    return fights.map((names) => names.join(' + ')).join(' → ');
  },

  /**
   * The 傾向 line a combat node adds at 開示度 2 (dungeon_exploration_v4.md §6.3 「開示度 2 なら傾向の行を
   * 足す」): the enemy's 得意な間合い. With more than one enemy behind the node, each line names its enemy.
   */
  TendencyLine(enemyId, named = false) {
    const gap = EnemyPages.Entry(enemyId).FavoredGap;
    return named ? `傾向（${EnemyPages.NameOf(enemyId)}）: ${EnemyPages.FavoredGapLabel} ${gap}` : `傾向: ${EnemyPages.FavoredGapLabel} ${gap}`;
  },
};
