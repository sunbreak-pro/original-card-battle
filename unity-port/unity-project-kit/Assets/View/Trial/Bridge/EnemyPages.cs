// The journal's enemy pages (port of the browser trial's src/bridge/journal/EnemyPages.js; docs/EXTENSIONS.md
// 「手記の敵の頁と地図の敵の情報」 J-1〜J-9): the content JournalBook.cs leaves to #107. Every text is transcribed byte
// for byte from enemy_document/enemy_roster_v4.md (the `journal` of §1.1 per enemy, the 条件（手記の文） column of the
// bosses' adaptation tables §4.4 / §5.4 / §6.4, the action tables' 構え and 崩し rows, the bosses' 専用の状態 tables
// §4.1 / §5.1 / §6.2), one entry per enemy of BattleCore.Enemies. Names and HP come from the core (the same EnemyDef
// the battle reads), so the page and the battle never disagree. It reads BattleCore.Enemies, so it lives in a Bridge
// folder (Journal.Script stays free of the battle core).
//
// Which row needs which 開示度 (battle_document/battle_ui_ux_v2.md §7, the 「手記の敵の頁」 column; §11.3):
//   0  名前と HP 最大だけ                                       → 名前, HP 最大
//   1  間合いの帯（0 / 1〜2 / 3 以上）ごとの傾向（決定木）          → 得意な間合い, 間合い 0, 間合い 1〜2, 間合い 3 以上
//   2  弱点・特殊行動・崩しの有無。ボスは適応条件の全文            → 弱点, 特殊行動, 適応条件
// The rows run in the order of battle_ui_ux_v2.md §2.4 「行の構成は 名前 / 間合いの帯ごとの傾向 / 弱点 / 特殊行動 /
// 適応条件（ボスだけ、開示度 2 で全文）/ 自分のメモ」; the memo row is the drawer's to add (WithMemo), since the memo is
// the player's text, not the roster's. A row the page has not reached prints 「？」 (JournalPage.TextOf).
//
// Engine-free (no UnityEngine).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BattleCore;
using Journal;

namespace Trial.Bridge
{
    /// <summary>
    /// One line of the 特殊行動 row (js: EnemyPages.js:22 act / :27 status): an action of the enemy's (ActionId, Name, its
    /// 予兆 word Omen, Effect, Trait) or a boss's own status (Status, Effect, Lasts). The other members are null.
    /// </summary>
    public sealed class EnemySpecial
    {
        private EnemySpecial(string actionId, string name, string omen, string effect, string trait, string status, string lasts)
        {
            ActionId = actionId;
            Name = name;
            Omen = omen;
            Effect = effect;
            Trait = trait;
            Status = status;
            Lasts = lasts;
        }

        // js: bridge/journal/EnemyPages.js:22
        /// <summary>The roster's 予兆 word of an action (enemy_roster_v4.md §1.2: 攻撃 / 防御 / 移動 / 構え / 技).</summary>
        public static EnemySpecial Act(string actionId, string name, string omen, string effect, string trait = null) =>
            new EnemySpecial(actionId, name, omen, effect, trait, null, null);

        // js: bridge/journal/EnemyPages.js:27
        /// <summary>A boss's own status: the 状態 / 向き / 効き方 / 持続 columns of its 専用の状態 table.</summary>
        public static EnemySpecial OfStatus(string status, string effect, string lasts) =>
            new EnemySpecial(null, null, null, effect, null, status, lasts);

        /// <summary>The action's id in the core (null for a status).</summary>
        public string ActionId { get; }

        public string Name { get; }

        /// <summary>The 予兆 word (構え / 攻撃 …).</summary>
        public string Omen { get; }

        public string Effect { get; }

        /// <summary>The action's 特性, or null.</summary>
        public string Trait { get; }

        /// <summary>The status's name (null for an action).</summary>
        public string Status { get; }

        /// <summary>How long the status lasts (null for an action).</summary>
        public string Lasts { get; }
    }

    /// <summary>The roster's entry for one enemy (js: EnemyPages.js:31 entry).</summary>
    public sealed class EnemyPageEntry
    {
        public EnemyPageEntry(string source, string favoredGap, string gap0, string gap1to2, string gap3Plus, string weakness,
            IReadOnlyList<EnemySpecial> special = null, IReadOnlyList<string> adaptations = null)
        {
            Source = source;
            FavoredGap = favoredGap;
            Gap0 = gap0;
            Gap1to2 = gap1to2;
            Gap3Plus = gap3Plus;
            Weakness = weakness;
            Special = Array.AsReadOnly((special ?? Array.Empty<EnemySpecial>()).ToArray());
            Adaptations = Array.AsReadOnly((adaptations ?? Array.Empty<string>()).ToArray());
        }

        public string Source { get; }

        /// <summary>The section heading's 「— 得意な間合い …」.</summary>
        public string FavoredGap { get; }

        public string Gap0 { get; }
        public string Gap1to2 { get; }
        public string Gap3Plus { get; }
        public string Weakness { get; }

        /// <summary>The stances, the 崩し actions, then a boss's own statuses.</summary>
        public IReadOnlyList<EnemySpecial> Special { get; }

        /// <summary>The 条件（手記の文） column, every row in table order (bosses only).</summary>
        public IReadOnlyList<string> Adaptations { get; }
    }

    public static class EnemyPages
    {
        // ---- the rows' labels (battle_ui_ux_v2.md §2.4 「行の構成」, §7) ----

        public const string NameLabel = "名前";
        public const string MaxHpLabel = "HP 最大";
        public const string FavoredGapLabel = "得意な間合い";
        public const string Gap0Label = "間合い 0";
        public const string Gap1to2Label = "間合い 1〜2";
        public const string Gap3PlusLabel = "間合い 3 以上";
        public const string WeaknessLabel = "弱点";
        public const string SpecialLabel = "特殊行動";
        public const string AdaptationsLabel = "適応条件";
        public const string MemoLabel = "自分のメモ";

        /// <summary>The 特殊行動 row of an enemy with no stance, no 崩し and no status of its own.</summary>
        public const string NoSpecial = "なし";

        /// <summary>The memo row before anything is written: the exploration journal's memo page says the same.</summary>
        public const string MemoEmpty = "メモはまだありません。";

        /// <summary>battle_ui_ux_v2.md §2.4: 自分のメモ（読むだけ、末尾に「戦闘中は書けない」）.</summary>
        public const string MemoLocked = "戦闘中は書けない";

        /// <summary>What a boss's own status is put on (the 向き column 「相手に」, worded as the terms page words it).</summary>
        public const string ToFoe = "相手に付ける";

        /// <summary>The disclosure each row needs (battle_ui_ux_v2.md §7, the 「手記の敵の頁」 column).</summary>
        private const int AtMeeting = 0;
        private const int AtEncounter = 1;
        private const int AtObservation = 2;

        private static EnemySpecial Act(string actionId, string name, string omen, string effect, string trait = null) =>
            EnemySpecial.Act(actionId, name, omen, effect, trait);

        private static EnemySpecial Status(string status, string effect, string lasts) => EnemySpecial.OfStatus(status, effect, lasts);

        // FavoredGap: each section's heading 「— 得意な間合い …」. Gap0 / Gap1to2 / Gap3Plus / Weakness: the section's
        // 「手記:」 line (§2 / §3) or 「手記の頁」 subsection (§4.6 / §5.6 / §6.7): 間合い 0「…」。1〜2「…」。3 以上「…」。
        // 弱点「…」。 Special: the action table's rows whose 予兆 is 構え (the stance, §1.2 「スタンスの行動は 1 戦闘で 1 回
        // だけ」) or whose 効果 gives 崩し (§7 「崩しの有無」), then the boss's 専用の状態. Adaptations: the 条件（手記の文）
        // column, every row in table order (§1.5 「開示度 2 では条件そのものが手記の頁に書かれます」).
        // js: bridge/journal/EnemyPages.js:52
        private static readonly IReadOnlyDictionary<string, EnemyPageEntry> Roster = new Dictionary<string, EnemyPageEntry>(StringComparer.Ordinal)
        {
            ["polearm_warped"] = new EnemyPageEntry(
                source: "enemy_roster_v4.md §2.1",
                favoredGap: "1〜2",
                gap0: "柄尻で 2 マス押し返す。Guard が無いと重い。後ろが端なら壁にも当たる",
                gap1to2: "払い斬り。2 マス空いていると +3",
                gap3Plus: "踏み込んで 1 マス寄ってくる",
                weakness: "隣へ入れば払い斬りは届かないが、1 ターンで押し返される。押し込みの予兆が出たターンに鈍足を付けると、押し返しが 1 マスに減る。Guard を残して受けてもよい"),
            ["shadow_hound"] = new EnemyPageEntry(
                source: "enemy_roster_v4.md §2.2",
                favoredGap: "0",
                gap0: "噛みつき。出血が 2 スタック残る",
                gap1to2: "跳びかかって隣まで詰める。2 マス空いていると +3",
                gap3Plus: "駆け寄って 2 マス詰める",
                weakness: "HP 80 で、通常の敵では低め。離れても 1 ターンで詰められる。噛みつきの予兆が出たら 1 マス下がれば届かない。跳びかかりの予兆が出たら、狙うマスの外（隣か 3 以上）へ動けば空振りになる"),
            ["rusted_revenant"] = new EnemyPageEntry(
                source: "enemy_roster_v4.md §2.3",
                favoredGap: "0",
                gap0: "大振り。こちらのスタミナが少ないと重くなる",
                gap1to2: "押し込みで当てながら 1 マス寄る",
                gap3Plus: "開幕に鉄の身を置き、のし歩いて寄る",
                weakness: "鉄の身を置く前の 1 ターンが最も柔らかい。大振りは隣にしか届かないので、予兆を見て 1 マス下がれば空振りになる。下がっても押し込みとのし歩きで戻ってくるので、戻った直後の 1 ターンで叩く。崩しでスタミナを削ると大振りを出せない",
                special: new[]
                {
                    Act("iron_body", "鉄の身", "構え", "毎ターン開始に Guard +3"),
                }),
            ["crossbow_hunter"] = new EnemyPageEntry(
                source: "enemy_roster_v4.md §2.4",
                favoredGap: "2〜3",
                gap0: "蹴り離して 2 マス押し返す",
                gap1to2: "撃ってから 1 マス下がる",
                gap3Plus: "弩。威圧が付いて次の一手が 3 下がる",
                weakness: "弩は 2 以上ならどこへでも届き、下がっても逃げられない。予兆を見て 1 以下へ詰めれば空振りになる。詰めても蹴り離されるので、押し返された次のターンに戻れるよう、前へ出る札を 1 枚は手札に残す。右端に追い詰めれば、退き撃ちで下がれなくなる"),
            ["mist_archer"] = new EnemyPageEntry(
                source: "enemy_roster_v4.md §2.5",
                favoredGap: "2〜3",
                gap0: "靄に溶けて 2 マス弾く",
                gap1to2: "同じく靄に溶ける。2 マス空けていれば弾かれず、Guard が付くだけ",
                gap3Plus: "靄の矢。3 以上だと +5 で、疲労が付いて回復が減る",
                weakness: "矢は 2 以上ならどこへでも届き、下がるほど重い。予兆を見て 1 以下へ入れば空振りになるが、靄に溶けるで弾き返される。間合い 2 に留まって、1〜2 に届く札で削るのが近道。HP 72"),
            ["twin_blade_warped"] = new EnemyPageEntry(
                source: "enemy_roster_v4.md §2.6",
                favoredGap: "0",
                gap0: "二段斬り。続けて 2 回斬ってくる",
                gap1to2: "踏み込み斬りで隣まで詰める",
                gap3Plus: "刃を十字に構えたまま 2 マス詰めてくる。スタミナに余裕があれば見切りが付く",
                weakness: "二段斬りは隣にしか届かない。予兆を見て 1 マス下がれば空振りになる。十字受けの見切りは、待っても消えない。竜兵の Guard が受け止めた一撃に、受け止めた分の半分を返してくる。返すたびに 1 つ減る。返る量は竜兵の Guard で決まるので、重い札で殴っても増えない"),
            ["polearm_crystal"] = new EnemyPageEntry(
                source: "enemy_roster_v4.md §2.7",
                favoredGap: "1〜2",
                gap0: "突いてから 2 マス退く。Guard が無いと +3",
                gap1to2: "大穂の突き。重いが毎ターンは出せない",
                gap3Plus: "突き進んで 2 マス詰めてくる。3 以上だと +3",
                weakness: "大穂の突きは隣に届かない。予兆を見て隣へ入れば空振りになる。隣に入ると引き突きで退かれるので、退いた先へ届く札を残しておく。右端へ押し込めば退けなくなる。押す / 引くが効く"),
            ["armored_warden"] = new EnemyPageEntry(
                source: "enemy_roster_v4.md §3.1",
                favoredGap: "0",
                gap0: "打ち下ろしで崩し、盾打ちで鈍足。隣にいると崩しが 1 増える",
                gap1to2: "盾で押し出ながら寄ってくる",
                gap3Plus: "鉄壁を置いてから、盾を掲げて寄ってくる",
                weakness: "押しても引いても動かない（大きさ 2）。鉄壁を置く前に叩く。盾打ちは隣にしか届かないので、予兆を見て 1 マス下がれば鈍足を避けられる。鈍足が付いた後は 1 マスでは下がれないので、下がるなら最初の盾打ちの前に。打ち下ろしは 1 マス下がっても届くので、Guard を残して受ける。2 手目はいつも列 1",
                special: new[]
                {
                    Act("iron_wall", "鉄壁", "構え", "毎ターン開始に Guard +3"),
                    Act("helm_splitter", "打ち下ろし", "攻撃", "威力 8、崩しを 1 付与する", "間合い 0: 崩し +1"),
                }),
            ["pack_alpha"] = new EnemyPageEntry(
                source: "enemy_roster_v4.md §3.2",
                favoredGap: "0",
                gap0: "引き裂き。出血が残る。続けて 1 マス押しやられ、鈍足が付く",
                gap1to2: "飛びかかって詰める。2 マス空いていると +5",
                gap3Plus: "狩りの構えを置いてから、飛びかかってくる",
                weakness: "引き裂きは隣にしか届かない。予兆を見て 1 マス下がれば空振りになる。ただし飛びかかりは 1〜3 に届くので、下がり過ぎると +5 で詰められる。1 マスだけ空けて受け、端へ押しやられる前に前へ出る。押す / 引くが効く",
                special: new[]
                {
                    Act("hunt_stance", "狩りの構え", "構え", "相手との間合いが 0 のときアタック威力 +3"),
                }),
            ["polearm_unyielding"] = new EnemyPageEntry(
                source: "enemy_roster_v4.md §3.3",
                favoredGap: "1〜2",
                gap0: "柄で 2 マス突き放してから突く。後ろが端だと壁に当たる",
                gap1to2: "長穂の突き。2 手目は甲で受けるだけ",
                gap3Plus: "穂先を据えてから、穂で 2 マス掛け寄せる。掛け寄せた後は突いてくる",
                weakness: "1〜2 に立っていると突きは 1 回だけで、もう 1 手は守りになる。1〜2 に届く札で削るのが近道。突き放しと掛け寄せは鈍足で 1 マスに減る。掛け寄せが 1 マスになれば、5 から引かれても突きが届かない。隣へ入るのは後ろに 2 マス空いているときだけにする。押す / 引くが効く",
                special: new[]
                {
                    Act("set_spear", "穂先を据える", "構え", "相手との間合いが 1 以上のとき攻撃威力 +3"),
                }),
            ["miasma_priest"] = new EnemyPageEntry(
                source: "enemy_roster_v4.md §4",
                favoredGap: "2 以上",
                gap0: "鎌首の打ち込み。隣では +5。2 ターン居続けると払いのけで 2 マス押し返す。Guard を切らすと払いのけが重い",
                gap1to2: "瘴気の矢。疲労が付く。2 マス空けていると縛りの言葉も届き、次のターンは移動の効果が止まる。縛りは 1 ターンおきにしか来ない",
                gap3Plus: "瘴気の儀と帳。儀式は最大スタミナを削る。帳は相手が 2 マス以上離れているときだけ効く。離れていても瘴気の矢は届き、3 マス以上空けると重くなる",
                weakness: "儀式と言葉は 2〜4 にしか届かない。1 以下へ入れば空振りし、帳も止まる。セルクは動かないので、間合いは自分の足で決められる。ただし隣に 2 ターン居続けると払いのけが先頭に来るので、1 マス空けて戦う。アタックを 3 回続けると帳を張り直すので、その隙に動く。HP 70 から儀式が増える",
                special: new[]
                {
                    Act("ward", "瘴気の帳", "構え", "相手との間合いが 2 以上のターン開始に Guard +5"),
                    Status("瘴気纏い", "この戦闘中、最大スタミナ −1。2 スタックまで重なり −2", "戦闘終了まで"),
                    Status("呪縛", "付いている間、自分の移動の効果が働かない。移動が中心の札は使えない", "スタック分のターン"),
                },
                adaptations: new[]
                {
                    "アタックを 3 回続けて受けると、とぐろを締めて帳を張り直す",
                    "Guard を 9 以上で 2 ターン受け続けると、崩しを狙う",
                    "隣に 2 ターン居続けると、払いのけで押し返す",
                    "HP が半分を切ると、儀式を毎フェーズ行う",
                }),
            ["abyss_angler"] = new EnemyPageEntry(
                source: "enemy_roster_v4.md §5",
                favoredGap: "0",
                gap0: "引きずり込みが重い。鉤爪が乗っていると更に +3。呼び声で深みが付き、隣にいる間は回復が減る",
                gap1to2: "手繰り寄せて隣まで引く。鈍足が付く",
                gap3Plus: "鉤縄と手繰り寄せ。鉤縄を受けると後ろへ下がる手が止まり、そのまま引き寄せられる",
                weakness: "鉤縄は 2 以上の相手にだけ届き、呼び声は 0〜1 の相手にだけ届く。どちらの間合いにも罠があるので、同じ間合いで 2 ターン終えない。鉤爪は下がろうとするたびに 1 減るので、剥がす 1 ターンと下がる 1 ターンを続けて用意する。糸を緩めた次のフェーズは呼び声が来るので、そこで詰める。押しても引いても動かない（大きさ 2）。HP 90 で本体が前へ出る",
                special: new[]
                {
                    Act("deep_water", "淀みを張る", "構え", "相手との間合いが 1 以下のターン開始に Guard +5"),
                    Status("鉤爪", "後ろへ動く手を止める（後ろへ n の面が 0 マスになる）。止めるたびに 1 減る（使うと減る型）。前へ動く手は通る", "戦闘終了まで"),
                    Status("深み", "ガルドとの間合いが 0 でターンを始めると、回復 −1。2 スタックで −2（0 が下限）", "戦闘終了まで"),
                },
                adaptations: new[]
                {
                    "2 マス以上離れて 2 ターン居続けると、鉤縄と手繰り寄せを続ける",
                    "鉤爪を 2 回外されると、糸を緩めて張り直す",
                    "隣で 2 ターン粘ると、引きずり込みを続ける",
                    "HP が半分を切ると、自分から淵を出て食い込む",
                }),
            ["distortion_root"] = new EnemyPageEntry(
                source: "enemy_roster_v4.md §6",
                favoredGap: "2 以上",
                gap0: "捻じるの崩しと根を張る。根張りが乗ると根の横払いが +5",
                gap1to2: "根の横払いと根を張る。伸びる根で 1〜2 へ寄せられる",
                gap3Plus: "棘と枯らしの息。棘は 3 以上だと +5、息を受けると回復が細る。状態を 2 つ付けると、樹皮を置いて振り払う",
                weakness: "根を張るは 0〜1 に、枯らしの息は 2 以上にだけ届く。どの間合いにも罠があるので、同じマスで 2 ターン終えない。攻撃だけで 4 枚続けても、移動を 2 ターン続けても咎められるので、2 ターンに 1 度動く。HP 133 で本体がせり上がって息がどこへでも届き、66 で根と息が同じフェーズに来る",
                special: new[]
                {
                    Act("twist", "捻じる", "攻撃", "威力 5、崩しを 1 付与する", "崩し後: 相手のスタミナ 3 未満で威力 +3"),
                    Act("bark", "樹皮", "構え", "相手との間合いが 2 以上のターン開始に Guard +5"),
                    Status("根張り", "同じマスで 2 ターン続けてターンを終えると、ターン開始に HP −4。2 スタックで −8", "戦闘終了まで"),
                    Status("枯らし", "ターン開始の回復 −1。2 スタックで −2。疲労と重なっても、回復は 1 より下がらない（1 が下限）", "戦闘終了まで"),
                },
                adaptations: new[]
                {
                    "アタックを 4 回続けて受けると、根を張って足を止める",
                    "Guard で 2 ターン受け切ると、崩しに回る",
                    "スタンスを置くと、その構えを枯らしにくる",
                    "状態を 2 つ付けられると、身を固めて振り払う",
                    "2 ターン続けて動くと、先回りする",
                    "HP が 3 分の 2 を切ると、せり上がって力を枯らす",
                    "HP が 3 分の 1 を切ると、根と息を同じフェーズで使う",
                }),
        };

        /// <summary>The enemies with a page, in roster order (Enemies.All).</summary>
        // js: bridge/journal/EnemyPages.js:234
        public static IReadOnlyList<string> Ids => Enemies.All.Select(d => d.Id).Where(id => Roster.ContainsKey(id)).ToList();

        // js: bridge/journal/EnemyPages.js:238
        public static bool Has(string enemyId) => enemyId != null && Roster.ContainsKey(enemyId);

        /// <summary>The roster's entry for an enemy. Throws KeyNotFoundException for an enemy with no page.</summary>
        // js: bridge/journal/EnemyPages.js:243
        public static EnemyPageEntry Entry(string enemyId)
        {
            if (!Has(enemyId)) throw new KeyNotFoundException("EnemyPages: no page for enemy \"" + enemyId + "\".");
            return Roster[enemyId];
        }

        /// <summary>The page's id: the drawer finds an enemy's page by it (enemy-&lt;id&gt;).</summary>
        // js: bridge/journal/EnemyPages.js:249
        public static string PageId(string enemyId) => "enemy-" + enemyId;

        /// <summary>The enemy's name as the battle shows it (the core's EnemyDef).</summary>
        // js: bridge/journal/EnemyPages.js:254
        public static string NameOf(string enemyId) => Enemies.ById(enemyId).Name;

        /// <summary>One line of the 特殊行動 row: 「鉄の身（構え）: 毎ターン開始に Guard +3」 / 「瘴気纏い（相手に付ける）: …（戦闘終了まで）」.</summary>
        // js: bridge/journal/EnemyPages.js:259
        public static string SpecialLine(EnemySpecial item)
        {
            if (item.Status != null) return item.Status + "（" + ToFoe + "）: " + item.Effect + "（" + item.Lasts + "）";
            return item.Name + "（" + item.Omen + "）: " + item.Effect + (string.IsNullOrEmpty(item.Trait) ? "" : "。" + item.Trait);
        }

        /// <summary>The 特殊行動 row's text: one line per stance / 崩し action / own status, or 「なし」.</summary>
        // js: bridge/journal/EnemyPages.js:265
        public static string SpecialText(string enemyId)
        {
            IReadOnlyList<EnemySpecial> special = Entry(enemyId).Special;
            return special.Count == 0 ? NoSpecial : string.Join("\n", special.Select(SpecialLine));
        }

        /// <summary>The 適応条件 row's text: every condition of the boss, one a line. Empty for an enemy without.</summary>
        // js: bridge/journal/EnemyPages.js:271
        public static string AdaptationsText(string enemyId) => string.Join("\n", Entry(enemyId).Adaptations);

        /// <summary>
        /// The enemy's journal page at a 開示度 (0..2; JournalPage refuses the rest). Rows above the disclosure print
        /// 「？」. Bosses (the roster's entries with adaptations) carry the 適応条件 row.
        /// </summary>
        // js: bridge/journal/EnemyPages.js:279
        public static JournalPage Page(string enemyId, int disclosure)
        {
            EnemyPageEntry e = Entry(enemyId);
            EnemyDef def = Enemies.ById(enemyId);
            var rows = new List<JournalRow>
            {
                new JournalRow(NameLabel, def.Name, AtMeeting),
                new JournalRow(MaxHpLabel, def.MaxHp.ToString(CultureInfo.InvariantCulture), AtMeeting),
                new JournalRow(FavoredGapLabel, e.FavoredGap, AtEncounter),
                new JournalRow(Gap0Label, e.Gap0, AtEncounter),
                new JournalRow(Gap1to2Label, e.Gap1to2, AtEncounter),
                new JournalRow(Gap3PlusLabel, e.Gap3Plus, AtEncounter),
                new JournalRow(WeaknessLabel, e.Weakness, AtObservation),
                new JournalRow(SpecialLabel, SpecialText(enemyId), AtObservation),
            };
            if (e.Adaptations.Count > 0) rows.Add(new JournalRow(AdaptationsLabel, AdaptationsText(enemyId), AtObservation));
            return new JournalPage(PageId(enemyId), JournalTab.Enemy, def.Name, rows, disclosure);
        }

        /// <summary>
        /// The 自分のメモ row (battle_ui_ux_v2.md §2.4): the player's memo, read only. In battle it ends on
        /// 「戦闘中は書けない」; while exploring it is written on the メモ tab instead.
        /// </summary>
        // js: bridge/journal/EnemyPages.js:300
        public static JournalRow MemoRow(string memo, JournalContext context)
        {
            string text = !string.IsNullOrEmpty(memo) ? memo : MemoEmpty;
            return new JournalRow(MemoLabel, context == JournalContext.Battle ? text + "\n" + MemoLocked : text, AtMeeting);
        }

        /// <summary>An enemy page with the 自分のメモ row last; any other page (or null) as it is.</summary>
        // js: bridge/journal/EnemyPages.js:306
        public static JournalPage WithMemo(JournalPage page, string memo, JournalContext context)
        {
            if (page == null || page.Tab != JournalTab.Enemy) return page;
            var rows = page.Rows.Where(r => r.Label != MemoLabel).Concat(new[] { MemoRow(memo, context) }).ToList();
            return new JournalPage(page.Id, page.Tab, page.Title, rows, page.Disclosure);
        }

        // ---- the map's words (dungeon_exploration_v4.md §6.3) ----

        /// <summary>
        /// The enemies of one encounter as the node's 「敵:」 line names them: an encounter's fights in order, joined by
        /// 「 → 」 (fought one after the other), the enemies of one fight by 「 + 」 (fought together) — the marks of
        /// enemy_roster_v4.md §9. <paramref name="fights"/> is the display name of each enemy, fight by fight.
        /// </summary>
        // js: bridge/journal/EnemyPages.js:320
        public static string EncounterName(IEnumerable<IEnumerable<string>> fights) =>
            string.Join(" → ", fights.Select(names => string.Join(" + ", names)));

        /// <summary>
        /// The 傾向 line a combat node adds at 開示度 2 (dungeon_exploration_v4.md §6.3 「開示度 2 なら傾向の行を足す」): the
        /// enemy's 得意な間合い. With more than one enemy behind the node, each line names its enemy.
        /// </summary>
        // js: bridge/journal/EnemyPages.js:328
        public static string TendencyLine(string enemyId, bool named = false)
        {
            string gap = Entry(enemyId).FavoredGap;
            return named ? "傾向（" + NameOf(enemyId) + "）: " + FavoredGapLabel + " " + gap : "傾向: " + FavoredGapLabel + " " + gap;
        }
    }
}
