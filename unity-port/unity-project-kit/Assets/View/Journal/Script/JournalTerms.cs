// The page of terms and statuses (#234): the rules explained, the same in every life. It has no
// disclosure, is never left at a death or handed down as a legacy, and opens in battle as well as
// while exploring (CAMP_FACILITIES_DESIGN.md §3.3, dungeon_exploration_v4.md §6.7). The words are
// art_document/battle-visual-v1.md §7.5 (terms) and §7.3 (statuses); when a term or a status is
// added there, add it here.
using System.Collections.Generic;
using System.Linq;

namespace Journal
{
    public static class JournalTerms
    {
        public const string PageId = "terms";

        public const string Title = "用語と状態";

        public const string TermsHeading = "【用語】";

        public const string StatusesHeading = "【状態】";

        /// <summary>The terms and their definitions (battle-visual-v1.md §7.5).</summary>
        public static readonly IReadOnlyList<JournalRow> Terms = new[]
        {
            new JournalRow("間合い", "自分と相手のあいだの空きマスの数。0 は隣り合っている"),
            new JournalRow("構え", "ターン終了のときスタミナが 3 以上残っていれば Guard +3"),
            new JournalRow("崩し n", "相手のスタミナを n 減らす"),
            new JournalRow("素直な札", "特性を持たないかわりに、値が 2 高い札（表の値に込み）"),
            new JournalRow("初手", "このターンに最初に出す札"),
            new JournalRow("温存", "この札を出したあとも、スタミナが n 以上残る"),
            new JournalRow("連打", "このターンに直前に出した札と、同じ属性である"),
            new JournalRow("連動 X", "このターンに属性 X（攻撃・防御・スキル・スタンス）のカードをもう出している"),
            new JournalRow("手薄", "この札を出したあと、手札が残り 2 枚以下"),
            new JournalRow("重撃", "威力 +6。そのかわり次のターン開始のスタミナ回復が 1 減る"),
            new JournalRow("転換", "この札で得る Guard の半分（切り上げ）を威力に足す"),
            new JournalRow("追撃", "このターンに次に出す攻撃の威力 +5"),
            new JournalRow("移動後", "このターンに自分がもう動いている"),
            new JournalRow("永続", "消えずに残る。スタンスの効果。重ねると重ねた数のぶん効く"),
            new JournalRow("詰める / 離れる", "自分が動いて、相手との間合いを縮める / 広げる"),
            new JournalRow("押す / 引く", "相手を動かして、相手との間合いを広げる / 縮める"),
        };

        /// <summary>The statuses: who carries them, how they act, how they wear off (battle-visual-v1.md §7.3).</summary>
        public static readonly IReadOnlyList<JournalRow> Statuses = new[]
        {
            Status("出血", ToFoe, "相手のターン開始に HP −2×スタック", "ターンごとに 1 減る"),
            Status("脆化", ToFoe, "次に受ける攻撃の威力が 1.5 倍（Guard で引く前の値）", "効くと 1 減る"),
            Status("鈍足", ToFoe, "次のターン、自分が動くマスと、自分が押す / 引くマスが 1 減る", "ターンごとに 1 減る"),
            Status("威圧", ToFoe, "次の行動の威力と Guard が −3", "効くと 1 減る"),
            Status("疲労", ToFoe, "次のターン開始のスタミナ回復が −1", "ターンごとに 1 減る"),
            Status("強化", ToSelf, "次の攻撃の威力が 1.5 倍", "効くと 1 減る"),
            Status("集中", ToSelf, "次に出す札の値が 1 段強くなる（コストは変わらない。いちばん強い段は超えない）", "効くと 1 減る"),
            Status("見切り", ToSelf, "次に攻撃を受けたとき、Guard で防いだ分の半分を相手に返す", "効くと 1 減る（Guard 0 で受けたときは減らない）"),
            Status("俊敏", ToSelf, "ターン開始に、1 マスだけ無料で前か後ろへ動ける", "ターンごとに 1 減る"),
            Status("再生", ToSelf, "ターン開始に HP +2×スタック", "ターンごとに 1 減る"),
        };

        /// <summary>The page itself: the terms on top, the statuses below, every row readable from the first life.</summary>
        public static JournalPage Page() =>
            new JournalPage(PageId, JournalTab.Terms, Title,
                new[] { new JournalRow(TermsHeading, "") }
                    .Concat(Terms)
                    .Concat(new[] { new JournalRow(StatusesHeading, "") })
                    .Concat(Statuses)
                    .ToList());

        private const string ToFoe = "相手に付ける";

        private const string ToSelf = "自分に付ける";

        private static JournalRow Status(string name, string side, string effect, string wearsOff) =>
            new JournalRow($"{name}（{side}）", $"効き方: {effect}\n減り方: {wearsOff}");
    }
}
