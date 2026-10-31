// The enemy pages of the journal (#107): what the journal knows of each enemy, cut by 開示度 into the
// rows a page shows. The words are enemy_document/enemy_roster_v4.md's 「手記」 of each enemy (the cards
// lane's; v4.24): §2.1〜§3.3 for the normal seven and the three elites, §4.6 / §5.6 / §6.7 / §7.13 for
// the bosses. When the roster's 手記 changes, copy it here; the roster is the source.
//
// The roster gives each enemy four sentences (間合い 0 / 1〜2 / 3 以上 / 弱点; roster §1.1:
// 「開示度 2 で全部見える」). A boss also has its adaptations, whose 「条件（手記の文）」 column the roster
// writes on the page at 開示度 2 (roster §1.5; the tables of §4.4 / §5.4 / §6.4 / §7.11). The cut
// (dungeon_exploration_v4.md §6.11): an encounter (開示度 1) opens the three range rows, the moves the
// player has seen; observation or the survey (開示度 2) adds the favoured range, the weakness and a
// boss's adaptation conditions, which are the reading of those moves. At 開示度 0 every row reads 「？」.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Journal
{
    /// <summary>One enemy's journal text, as the roster writes it.</summary>
    public sealed class EnemyJournalEntry
    {
        public EnemyJournalEntry(string id, string name, string favouredGap, string atGapZero, string atGapOneToTwo, string atGapThreePlus, string weakness, params string[] adaptations)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("an entry needs the roster id", nameof(id));
            Id = id;
            Name = name ?? "";
            FavouredGap = favouredGap ?? "";
            AtGapZero = atGapZero ?? "";
            AtGapOneToTwo = atGapOneToTwo ?? "";
            AtGapThreePlus = atGapThreePlus ?? "";
            Weakness = weakness ?? "";
            Adaptations = (adaptations ?? Array.Empty<string>()).Select(a => a ?? "").ToArray();
        }

        /// <summary>The roster id (roster §1.8), the same as BattleCore.Enemies and DungeonCore.LayerEncounters.</summary>
        public string Id { get; }

        public string Name { get; }

        /// <summary>得意な間合い, as the roster's heading writes it (「0」「1〜2」「2 以上」).</summary>
        public string FavouredGap { get; }

        public string AtGapZero { get; }

        public string AtGapOneToTwo { get; }

        public string AtGapThreePlus { get; }

        public string Weakness { get; }

        /// <summary>
        /// A boss's adaptation conditions: the roster's 「条件（手記の文）」 column in the table's order,
        /// without the quote marks. Empty for an enemy with no adaptation.
        /// </summary>
        public IReadOnlyList<string> Adaptations { get; }
    }

    public static class EnemyJournal
    {
        /// <summary>An encounter opens the rows of what the enemy does at each range.</summary>
        public const int MovesDisclosure = 1;

        /// <summary>Observation or the survey opens the favoured range, the weakness and a boss's adaptation conditions.</summary>
        public const int WeaknessDisclosure = 2;

        public const string GapZeroLabel = "間合い 0";

        public const string GapOneToTwoLabel = "間合い 1〜2";

        public const string GapThreePlusLabel = "間合い 3 以上";

        public const string FavouredGapLabel = "得意な間合い";

        public const string WeaknessLabel = "弱点";

        /// <summary>An adaptation row reads 「適応 1」「適応 2」…, in the roster's order.</summary>
        public const string AdaptationLabel = "適応";

        /// <summary>An enemy page's id: 「enemy-」 and the roster id.</summary>
        public static string PageId(string enemyId) => "enemy-" + enemyId;

        /// <summary>
        /// The fourteen the roster gives a 手記 to, in the roster's order: the normal seven (§2), the
        /// three elites (§3), the three bosses (§4〜§6) and the extra boss (§7). The eight of §8 have no
        /// id yet and no 手記.
        /// </summary>
        public static readonly IReadOnlyList<EnemyJournalEntry> All = new[]
        {
            new EnemyJournalEntry(
                "polearm_warped", "錆槍の竜兵", "1〜2",
                "柄尻で 2 マス押し返す。Guard が無いと重い。後ろが端なら壁にも当たる",
                "払い斬り。2 マス空いていると +3",
                "踏み込んで 1 マス寄ってくる",
                "隣へ入れば払い斬りは届かないが、1 ターンで押し返される。押し込みの予兆が出たターンに鈍足を付けると、押し返しが 1 マスに減る。Guard を残して受けてもよい"),
            new EnemyJournalEntry(
                "shadow_hound", "瘴牙の走竜", "0",
                "噛みつき。出血が 2 スタック残る",
                "跳びかかって隣まで詰める。2 マス空いていると +3",
                "駆け寄って 2 マス詰める",
                "HP 80 で、通常の敵では低め。離れても 1 ターンで詰められる。噛みつきの予兆が出たら 1 マス下がれば届かない。跳びかかりの予兆が出たら、狙うマスの外（隣か 3 以上）へ動けば空振りになる"),
            new EnemyJournalEntry(
                "rusted_revenant", "瘴甲の竜兵", "0",
                "大振り。こちらのスタミナが少ないと重くなる",
                "押し込みで当てながら 1 マス寄る",
                "開幕に鉄の身を置き、のし歩いて寄る",
                "鉄の身を置く前の 1 ターンが最も柔らかい。大振りは隣にしか届かないので、予兆を見て 1 マス下がれば空振りになる。下がっても押し込みとのし歩きで戻ってくるので、戻った直後の 1 ターンで叩く。崩しでスタミナを削ると大振りを出せない"),
            new EnemyJournalEntry(
                "crossbow_hunter", "灰弩の竜兵", "2〜3",
                "蹴り離して 2 マス押し返す",
                "撃ってから 1 マス下がる",
                "弩。威圧が付いて次の一手が 3 下がる",
                "弩は 2 以上ならどこへでも届き、下がっても逃げられない。予兆を見て 1 以下へ詰めれば空振りになる。詰めても蹴り離されるので、押し返された次のターンに戻れるよう、前へ出る札を 1 枚は手札に残す。右端に追い詰めれば、退き撃ちで下がれなくなる"),
            new EnemyJournalEntry(
                "mist_archer", "燐弓の竜兵", "2〜3",
                "靄に溶けて 2 マス弾く",
                "同じく靄に溶ける。2 マス空けていれば弾かれず、Guard が付くだけ",
                "靄の矢。3 以上だと +5 で、疲労が付いて回復が減る",
                "矢は 2 以上ならどこへでも届き、下がるほど重い。予兆を見て 1 以下へ入れば空振りになるが、靄に溶けるで弾き返される。間合い 2 に留まって、1〜2 に届く札で削るのが近道。HP 72"),
            new EnemyJournalEntry(
                "twin_blade_warped", "燐刃の竜兵", "0",
                "二段斬り。続けて 2 回斬ってくる",
                "踏み込み斬りで隣まで詰める",
                "刃を十字に構えたまま 2 マス詰めてくる。スタミナに余裕があれば見切りが付く",
                "二段斬りは隣にしか届かない。予兆を見て 1 マス下がれば空振りになる。十字受けの見切りは、待っても消えない。竜兵の Guard が受け止めた一撃に、受け止めた分の半分を返してくる。返すたびに 1 つ減る。返る量は竜兵の Guard で決まるので、重い札で殴っても増えない"),
            new EnemyJournalEntry(
                "polearm_crystal", "晶槍の竜兵", "1〜2",
                "突いてから 2 マス退く。Guard が無いと +3",
                "大穂の突き。重いが毎ターンは出せない",
                "突き進んで 2 マス詰めてくる。3 以上だと +3",
                "大穂の突きは隣に届かない。予兆を見て隣へ入れば空振りになる。隣に入ると引き突きで退かれるので、退いた先へ届く札を残しておく。右端へ押し込めば退けなくなる。押す / 引くが効く"),
            new EnemyJournalEntry(
                "armored_warden", "鉄壁の門竜", "0",
                "打ち下ろしで崩し、盾打ちで鈍足。隣にいると崩しが 1 増える",
                "盾で押し出ながら寄ってくる",
                "鉄壁を置いてから、盾を掲げて寄ってくる",
                "押しても引いても動かない（大きさ 2）。鉄壁を置く前に叩く。盾打ちは隣にしか届かないので、予兆を見て 1 マス下がれば鈍足を避けられる。鈍足が付いた後は 1 マスでは下がれないので、下がるなら最初の盾打ちの前に。打ち下ろしは 1 マス下がっても届くので、Guard を残して受ける。2 手目はいつも列 1"),
            new EnemyJournalEntry(
                "pack_alpha", "統牙の長竜", "0",
                "引き裂き。出血が残る。続けて 1 マス押しやられ、鈍足が付く",
                "飛びかかって詰める。2 マス空いていると +5",
                "狩りの構えを置いてから、飛びかかってくる",
                "引き裂きは隣にしか届かない。予兆を見て 1 マス下がれば空振りになる。ただし飛びかかりは 1〜3 に届くので、下がり過ぎると +5 で詰められる。1 マスだけ空けて受け、端へ押しやられる前に前へ出る。押す / 引くが効く"),
            new EnemyJournalEntry(
                "polearm_unyielding", "不退の槍竜", "1〜2",
                "柄で 2 マス突き放してから突く。後ろが端だと壁に当たる",
                "長穂の突き。2 手目は甲で受けるだけ",
                "穂先を据えてから、穂で 2 マス掛け寄せる。掛け寄せた後は突いてくる",
                "1〜2 に立っていると突きは 1 回だけで、もう 1 手は守りになる。1〜2 に届く札で削るのが近道。突き放しと掛け寄せは鈍足で 1 マスに減る。掛け寄せが 1 マスになれば、5 から引かれても突きが届かない。隣へ入るのは後ろに 2 マス空いているときだけにする。押す / 引くが効く"),
            new EnemyJournalEntry(
                "miasma_priest", "大黒蛇 セルク", "2 以上",
                "鎌首の打ち込み。隣では +5。2 ターン居続けると払いのけで 2 マス押し返す。Guard を切らすと払いのけが重い",
                "瘴気の矢。疲労が付く。2 マス空けていると縛りの言葉も届き、次のターンは移動の効果が止まる。縛りは 1 ターンおきにしか来ない",
                "瘴気の儀と帳。儀式は最大スタミナを削る。帳は相手が 2 マス以上離れているときだけ効く。離れていても瘴気の矢は届き、3 マス以上空けると重くなる",
                "儀式と言葉は 2〜4 にしか届かない。1 以下へ入れば空振りし、帳も止まる。セルクは動かないので、間合いは自分の足で決められる。ただし隣に 2 ターン居続けると払いのけが先頭に来るので、1 マス空けて戦う。アタックを 3 回続けると帳を張り直すので、その隙に動く。HP 70 から儀式が増える",
                "アタックを 3 回続けて受けると、とぐろを締めて帳を張り直す",
                "Guard を 9 以上で 2 ターン受け続けると、崩しを狙う",
                "隣に 2 ターン居続けると、払いのけで押し返す",
                "HP が半分を切ると、儀式を毎フェーズ行う"),
            new EnemyJournalEntry(
                "abyss_angler", "獄竜 ガルド", "0",
                "引きずり込みが重い。鉤爪が乗っていると更に +3。呼び声で深みが付き、隣にいる間は回復が減る",
                "手繰り寄せて隣まで引く。鈍足が付く",
                "鉤縄と手繰り寄せ。鉤縄を受けると後ろへ下がる手が止まり、そのまま引き寄せられる",
                "鉤縄は 2 以上の相手にだけ届き、呼び声は 0〜1 の相手にだけ届く。どちらの間合いにも罠があるので、同じ間合いで 2 ターン終えない。鉤爪は下がろうとするたびに 1 減るので、剥がす 1 ターンと下がる 1 ターンを続けて用意する。糸を緩めた次のフェーズは呼び声が来るので、そこで詰める。押しても引いても動かない（大きさ 2）。HP 90 で本体が前へ出る",
                "2 マス以上離れて 2 ターン居続けると、鉤縄と手繰り寄せを続ける",
                "鉤爪を 2 回外されると、糸を緩めて張り直す",
                "隣で 2 ターン粘ると、引きずり込みを続ける",
                "HP が半分を切ると、自分から淵を出て食い込む"),
            new EnemyJournalEntry(
                "distortion_root", "歪みの根", "2 以上",
                "捻じるの崩しと根を張る。根張りが乗ると根の横払いが +5",
                "根の横払いと根を張る。伸びる根で 1〜2 へ寄せられる",
                "棘と枯らしの息。棘は 3 以上だと +5、息を受けると回復が細る。状態を 2 つ付けると、樹皮を置いて振り払う",
                "根を張るは 0〜1 に、枯らしの息は 2 以上にだけ届く。どの間合いにも罠があるので、同じマスで 2 ターン終えない。攻撃だけで 4 枚続けても、移動を 2 ターン続けても咎められるので、2 ターンに 1 度動く。HP 133 で本体がせり上がって息がどこへでも届き、66 で根と息が同じフェーズに来る",
                "アタックを 4 回続けて受けると、根を張って足を止める",
                "Guard で 2 ターン受け切ると、崩しに回る",
                "スタンスを置くと、その構えを枯らしにくる",
                "状態を 2 つ付けられると、身を固めて振り払う",
                "2 ターン続けて動くと、先回りする",
                "HP が 3 分の 2 を切ると、せり上がって力を枯らす",
                "HP が 3 分の 1 を切ると、根と息を同じフェーズで使う"),
            new EnemyJournalEntry(
                "dragon_god", "竜神", "1〜2",
                "首の内側で、竜牙も首を伸ばすも届かない。代わりに踏みしだくが来て、崩しが付く。Guard が 0 だと +5。懐に 2 ターン居ると、翼で 2 マス打ち払われる",
                "竜牙と首を伸ばすが続けて来る。HP 166 から竜の息が来る。息は懐の外の全てのマスに届き、受けると次のターンは Guard が半分になる",
                "首を伸ばす。3 マス以上空けると +5。咆哮を受けると、次のターンは前へ出られない",
                "首は懐に届かない。息の予兆が見えたら懐へ入る。咆哮も息も 1 フェーズおきにしか来ないので、来ないフェーズに削る。Guard を固め続けると息が先に来る。HP 83 から本体が 1 マス前へ出て、懐に入っても翼で追い出され、咆哮で戻れなくなる。そのあとの息は灼けの切れたフェーズに来るので、Guard を積んで受ける。押しても引いても動かない（大きさ 3）",
                "懐に 2 ターン居続けると、翼で打ち払う",
                "Guard を 9 以上で 2 ターン受け続けると、息を先に吐く",
                "HP が 3 分の 2 を切ると、息を吐く",
                "HP が 3 分の 1 を切ると、降り立って懐から追い出す"),
        };

        private static readonly Dictionary<string, EnemyJournalEntry> ById = All.ToDictionary(e => e.Id, StringComparer.Ordinal);

        /// <summary>The entry for this roster id, or null when the roster gives it no 手記.</summary>
        public static EnemyJournalEntry Find(string enemyId) =>
            enemyId != null && ById.TryGetValue(enemyId, out EnemyJournalEntry entry) ? entry : null;

        /// <summary>The entry for this roster id. Throws for an id the roster gives no 手記.</summary>
        public static EnemyJournalEntry Of(string enemyId) =>
            Find(enemyId) ?? throw new ArgumentException($"the roster gives {enemyId} no 手記", nameof(enemyId));

        /// <summary>
        /// The enemy's page at this 開示度 (0〜2; a level outside is clamped): the three range rows from
        /// 1, the favoured range, the weakness and a boss's adaptation conditions from 2. Rows the level
        /// does not reach read 「？」.
        /// </summary>
        public static JournalPage Page(string enemyId, int disclosure)
        {
            EnemyJournalEntry entry = Of(enemyId);
            var rows = new List<JournalRow>
            {
                new JournalRow(GapZeroLabel, entry.AtGapZero, MovesDisclosure),
                new JournalRow(GapOneToTwoLabel, entry.AtGapOneToTwo, MovesDisclosure),
                new JournalRow(GapThreePlusLabel, entry.AtGapThreePlus, MovesDisclosure),
                new JournalRow(FavouredGapLabel, entry.FavouredGap, WeaknessDisclosure),
                new JournalRow(WeaknessLabel, entry.Weakness, WeaknessDisclosure),
            };
            for (int i = 0; i < entry.Adaptations.Count; i++)
                rows.Add(new JournalRow($"{AdaptationLabel} {i + 1}", entry.Adaptations[i], WeaknessDisclosure));
            return new JournalPage(PageId(entry.Id), JournalTab.Enemy, entry.Name, rows, Clamp(disclosure));
        }

        /// <summary>
        /// The tendency line a combat node adds at 開示度 2 (dungeon_exploration_v4.md §6.3), or "" below
        /// 2 and for an id the roster gives no 手記.
        /// </summary>
        public static string Tendency(string enemyId, int disclosure)
        {
            EnemyJournalEntry entry = Find(enemyId);
            if (entry == null || Clamp(disclosure) < WeaknessDisclosure) return "";
            return $"傾向: {entry.Name}は{FavouredGapLabel} {entry.FavouredGap}";
        }

        public static int Clamp(int disclosure) => Math.Max(0, Math.Min(JournalPage.MaxDisclosure, disclosure));
    }
}
