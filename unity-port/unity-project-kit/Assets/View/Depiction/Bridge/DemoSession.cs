// The demo's run of battles (#191): one enemy on its own, or a chain (battle_core_v4 §12). Pure C#:
// BattleCore only, no UnityEngine. It decides the order, the line each battle is fought on, what
// carries between battles (BattleCore.Chain), the rest, the end of the run, and every word the mode
// screen and the end screen print — so the View (DemoFlow) only lays the words out and forwards
// the buttons.
using System;
using System.Collections.Generic;
using System.Globalization;
using BattleCore;

namespace Depiction.Bridge
{
    public enum DemoMode
    {
        /// <summary>One enemy, then the end screen.</summary>
        Single,

        /// <summary>§12: the enemies in order, HP and stamina carried, a rest offered between; a loss ends it.</summary>
        Chain,
    }

    /// <summary>Where the run stands.</summary>
    public enum DemoStage
    {
        /// <summary>The next battle can start.</summary>
        Ready,

        /// <summary>A battle is being fought.</summary>
        Fighting,

        /// <summary>A chained battle was won and another follows: rest or go on.</summary>
        BetweenBattles,

        /// <summary>Lost, or the last battle won. Again or back to the deck.</summary>
        Over,
    }

    /// <summary>What the end screen shows and offers. Every string is final text; the View prints it as it is.</summary>
    public sealed class DemoEndScreen
    {
        public string Title = "";
        public bool Won;
        public readonly List<string> Lines = new List<string>();

        /// <summary>A chained battle was won and another follows: the two ways on.</summary>
        public bool CanGoOn;
        public string RestLabel = "";
        public string GoOnLabel = "";

        public string AgainLabel = "";
        public string BackLabel = "デッキ選択へ戻る";
    }

    public sealed class DemoSession
    {
        /// <summary>
        /// §12's default three, on the v4.3 ids: 錆槍の竜兵 → 瘴牙の走竜 → 鉄壁の門竜 (the canon names
        /// them 長柄の歪み兵 → 影走りの犬 → 甲冑の番人, their v4.1 names).
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultChain = new[] { "polearm_warped", "shadow_hound", "armored_warden" };

        private readonly List<CardInstance> _deck;
        private readonly int _seed;
        private readonly List<BattleTally> _tallies = new List<BattleTally>();
        private int _started;
        private int? _hp;
        private int? _stamina;

        public DemoMode Mode { get; }

        /// <summary>The enemies of the run, in the order they are fought.</summary>
        public IReadOnlyList<EnemyDef> Order { get; }

        /// <summary>The battle being fought, or the last one fought (0-based).</summary>
        public int Index { get; private set; }

        public DemoStage Stage { get; private set; } = DemoStage.Ready;

        /// <summary>The tallies of the battles fought since the run (re)started, in order.</summary>
        public IReadOnlyList<BattleTally> Tallies => _tallies;

        private DemoSession(DemoMode mode, IReadOnlyList<EnemyDef> order, List<CardInstance> deck, int seed)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            DeckValidation validation = Cards.Validate(deck);
            if (!validation.Ok) throw new ArgumentException("DemoSession: the deck breaks §8 — " + string.Join(" / ", validation.Errors));
            Mode = mode;
            Order = order;
            _deck = new List<CardInstance>(deck);
            _seed = seed;
        }

        /// <summary>
        /// One enemy on its own. <paramref name="run"/> counts the runs started from the same seed (the
        /// flow passes how many it has made), so choosing the same mode again deals afresh.
        /// </summary>
        public static DemoSession Single(List<CardInstance> deck, string enemyId, int seed, int run = 0)
        {
            return new DemoSession(DemoMode.Single, new[] { Enemies.ById(enemyId ?? "") }, deck, RunSeed(seed, run));
        }

        /// <summary>§12: a chain of battles, by default <see cref="DefaultChain"/>.</summary>
        public static DemoSession Chain(List<CardInstance> deck, int seed, IReadOnlyList<string> order = null, int run = 0)
        {
            order = order ?? DefaultChain;
            if (order.Count == 0) throw new ArgumentException("DemoSession: a chain needs at least one enemy.");
            var enemies = new List<EnemyDef>();
            foreach (string id in order) enemies.Add(Enemies.ById(id));
            return new DemoSession(DemoMode.Chain, enemies, deck, RunSeed(seed, run));
        }

        private static int RunSeed(int seed, int run)
        {
            return unchecked(seed + (run * 104729));
        }

        /// <summary>The single-mode 「ランダム」: one of the eleven, fixed by the seed and how many picks came before.</summary>
        public static string RandomEnemyId(int seed, int pick = 0)
        {
            double roll = new SeededRng(RunSeed(seed, pick)).NextDouble();
            int index = Math.Min(Enemies.All.Count - 1, (int)Math.Floor(roll * Enemies.All.Count));
            return Enemies.All[index].Id;
        }

        /// <summary>
        /// The line an enemy is fought on: the slice's 6 cells, or as many as it needs to start at
        /// START_GAP — 7 for the size-2 enemies (roster §0 「開始に要るマス数」).
        /// </summary>
        public static int FieldCellsFor(EnemyDef enemy)
        {
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            return Math.Max(BattleSetup.SliceFieldCells, Constants.PlayerStartCell + Constants.StartGap + enemy.Size);
        }

        public EnemyDef Current => Order[Index];

        /// <summary>The next battle: this enemy, its line, the deck (shuffled by TurnLoop.Start), and what the last one carried.</summary>
        public BattleSetup NextSetup()
        {
            EnemyDef enemy = Current;
            return new BattleSetup(enemy, _deck, FieldCellsFor(enemy), PlayerStartHp: _hp, PlayerStartStamina: _stamina);
        }

        /// <summary>Starts the next battle. Every start takes a seed of its own, so a retry is dealt afresh.</summary>
        public CoreBattleSource StartBattle(bool suggestCards = false)
        {
            if (Stage != DemoStage.Ready) throw new InvalidOperationException("DemoSession: no battle is ready (" + Stage + ").");
            int seed = unchecked(_seed + (_started * 7919));
            int total = Mode == DemoMode.Chain ? Order.Count : 1;
            var source = new CoreBattleSource(NextSetup(), seed, suggestCards, 0, Index + 1, total);
            _started++;
            Stage = DemoStage.Fighting; // only once the battle is built, so a failed start leaves the run Ready
            return source;
        }

        /// <summary>The battle has ended: tally it and carry what carries. A loss or the last win ends the run.</summary>
        public BattleTally Finish(CoreBattleSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (Stage != DemoStage.Fighting) throw new InvalidOperationException("DemoSession: no battle is being fought (" + Stage + ").");
            if (source.State.Result == GameResult.Ongoing) throw new InvalidOperationException("DemoSession: the battle has not ended.");

            BattleTally tally = BattleCore.Chain.Tally(source.State, source.History);
            _tallies.Add(tally);
            var carried = BattleCore.Chain.Carry(source.State);
            _hp = carried.Hp;
            _stamina = carried.Stamina;
            bool more = Mode == DemoMode.Chain && tally.Result == GameResult.Won && Index + 1 < Order.Count;
            Stage = more ? DemoStage.BetweenBattles : DemoStage.Over;
            return tally;
        }

        /// <summary>§12: between chained battles, rest (HP +30% of the maximum, stamina full) or go straight on.</summary>
        public void GoOn(bool rest)
        {
            if (Stage != DemoStage.BetweenBattles) throw new InvalidOperationException("DemoSession: there is no next battle (" + Stage + ").");
            if (rest)
            {
                var rested = BattleCore.Chain.Rest(_hp ?? Constants.PlayerMaxHp, Constants.PlayerMaxHp, Constants.BaseMaxStamina);
                _hp = rested.Hp;
                _stamina = rested.Stamina;
            }
            Index++;
            Stage = DemoStage.Ready;
        }

        /// <summary>The same run from its first battle, at full HP (§12: after a clean sweep, the same order again).</summary>
        public void Again()
        {
            if (Stage == DemoStage.Fighting) throw new InvalidOperationException("DemoSession: a battle is being fought.");
            Index = 0;
            _hp = null;
            _stamina = null;
            _tallies.Clear();
            Stage = DemoStage.Ready;
        }

        /// <summary>A chain won to the end.</summary>
        public bool AllWon => Mode == DemoMode.Chain && Stage == DemoStage.Over && _tallies.Count == Order.Count
            && _tallies.TrueForAll(t => t.Result == GameResult.Won);

        // ---- the words ----

        /// <summary>The end screen for the battle just finished: §12's items and the ways on.</summary>
        public DemoEndScreen EndScreen()
        {
            if (_tallies.Count == 0) throw new InvalidOperationException("DemoSession: no battle has finished yet.");
            BattleTally last = _tallies[_tallies.Count - 1];
            EnemyDef enemy = Enemies.ById(last.EnemyId);
            bool won = last.Result == GameResult.Won;
            var screen = new DemoEndScreen { Won = won };

            if (Mode == DemoMode.Single) screen.Title = won ? enemy.Name + "に勝ちました" : enemy.Name + "に敗れました";
            else if (AllWon) screen.Title = "連戦に全勝しました";
            else screen.Title = won ? (Index + 1) + " 戦目に勝ちました" : (Index + 1) + " 戦目で力尽きました";

            if (Mode == DemoMode.Chain) screen.Lines.Add("何戦目: " + (Index + 1) + " / " + Order.Count + "（" + enemy.Name + "）");
            else screen.Lines.Add("相手: " + enemy.Name);
            screen.Lines.Add("残り HP: " + last.HpLeft + " / " + last.MaxHp);
            screen.Lines.Add("ターン数: " + last.Turns + (Mode == DemoMode.Chain && _tallies.Count > 1 ? "（平均 " + AverageTurns() + "）" : ""));
            screen.Lines.Add("使った札 " + last.CardsPlayed + " 枚の属性: " + Breakdown(last));
            screen.Lines.Add("特性の発動: " + last.TraitsFired + " 回");
            if (Mode == DemoMode.Chain && Stage == DemoStage.Over && _tallies.Count > 1)
            {
                // §12: the chain's result screen adds up the whole run.
                BattleTally total = BattleCore.Chain.Total(_tallies);
                screen.Lines.Add("連戦の合計（" + _tallies.Count + " 戦）: 使った札 " + total.CardsPlayed + " 枚の属性: " + Breakdown(total));
                screen.Lines.Add("連戦の合計: 特性の発動 " + total.TraitsFired + " 回、平均ターン数 " + AverageTurns());
            }

            if (Stage == DemoStage.BetweenBattles)
            {
                screen.CanGoOn = true;
                var rested = BattleCore.Chain.Rest(last.HpLeft, last.MaxHp, Constants.BaseMaxStamina);
                screen.RestLabel = "休んで次へ（HP +" + (rested.Hp - last.HpLeft) + "、スタミナ全回復）";
                screen.GoOnLabel = "そのまま次へ";
                screen.Lines.Add("次: " + Order[Index + 1].Name);
            }
            screen.AgainLabel = Mode == DemoMode.Single ? "もう一度" : AllWon ? "同じ並びをもう一度" : "最初からもう一度";
            return screen;
        }

        /// <summary>「攻撃 9・ムーブ 4・防御 3」 in §2.2 order, attributes not used left out.</summary>
        public static string Breakdown(BattleTally tally)
        {
            if (tally == null) throw new ArgumentNullException(nameof(tally));
            var parts = new List<string>();
            foreach (BattleAttribute attribute in BattleTally.Order)
            {
                int n = tally.CountOf(attribute);
                if (n > 0) parts.Add(DeckBuilder.AttributeWords(attribute) + " " + n);
            }
            return parts.Count == 0 ? "なし" : string.Join("・", parts);
        }

        /// <summary>The mean of the turns of the battles fought so far, one decimal ("8.5").</summary>
        public string AverageTurns()
        {
            return AverageOf(_tallies);
        }

        /// <summary>The mean turns of some battles, to one decimal rounded away from zero, no trailing ".0" ("8.5", "8", "6.7").</summary>
        public static string AverageOf(IReadOnlyList<BattleTally> tallies)
        {
            if (tallies == null || tallies.Count == 0) return "0";
            double sum = 0;
            foreach (BattleTally tally in tallies) sum += tally.Turns;
            return Math.Round(sum / tallies.Count, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture);
        }

        // ---- the mode screen's words ----

        public static string ModeTitle => "戦い方を選ぶ";

        public static string SingleHeading => "1 体で区切る（敵を選ぶ）";

        /// <summary>§12 in one line: what carries and what ends the chain.</summary>
        public static string ChainHeading => "連戦（HP とスタミナを持ち越し、負けたら終わり）";

        public static string RandomLabel => "ランダム";

        public static string BackToDeckLabel => "デッキ選択へ戻る";

        /// <summary>The line an enemy has on the mode screen: 「錆槍の竜兵（通常・HP 60）」.</summary>
        public static string EnemyLine(EnemyDef enemy)
        {
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            string rank = enemy.Rank == EnemyRank.Boss ? "ボス" : enemy.Rank == EnemyRank.Elite ? "精鋭" : "通常";
            return enemy.Name + "（" + rank + "・HP " + enemy.MaxHp + "）";
        }

        /// <summary>The chain as the mode screen offers it: 「連戦（3 戦）: 錆槍の竜兵 → 瘴牙の走竜 → 鉄壁の門竜」.</summary>
        public static string ChainLine(IReadOnlyList<string> order = null)
        {
            order = order ?? DefaultChain;
            var names = new List<string>();
            foreach (string id in order) names.Add(Enemies.ById(id).Name);
            return "連戦（" + order.Count + " 戦）: " + string.Join(" → ", names);
        }
    }
}
