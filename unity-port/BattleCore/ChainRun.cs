using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace BattleCore
{
    /// <summary>
    /// One battle of a §12 chain: the enemies fought at once (one, or the two of a roster §9 pair, in
    /// phase order) and the layer it stands for. The layer is what the 瘴気 reads.
    /// </summary>
    public sealed record ChainBattle(IReadOnlyList<string> EnemyIds, int Layer)
    {
        /// <summary>The enemies, in phase order (§7.4).</summary>
        public IReadOnlyList<EnemyDef> Defs
        {
            get
            {
                var defs = new List<EnemyDef>();
                foreach (var id in EnemyIds) defs.Add(Enemies.ById(id));
                return defs;
            }
        }

        /// <summary>The layer's 瘴気 density (<see cref="ChainOrder.DensityOf"/>).</summary>
        public int Density => ChainOrder.DensityOf(Layer);

        /// <summary>
        /// The line it is fought on: the slice's 6 cells, or as many as the enemies need to start at
        /// START_GAP side by side — 7 for a size-2 enemy and for a pair (roster §0 「開始に要るマス数」).
        /// </summary>
        public int FieldCells
        {
            get
            {
                int size = 0;
                foreach (var def in Defs) size += def.Size;
                return Math.Max(BattleSetup.SliceFieldCells, Constants.PlayerStartCell + Constants.StartGap + size);
            }
        }

        /// <summary>"polearm_warped+crossbow_hunter@3", the form <see cref="ChainOrder.Parse"/> reads back.</summary>
        public override string ToString() => string.Join("+", EnemyIds) + "@" + Layer.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// §12: the orders a chain is fought in — the default three, roster §9's nine, or one handed in
    /// with <c>-chain a,b,c</c>.
    /// </summary>
    public static class ChainOrder
    {
        /// <summary>The command-line switch that names the order (§11 連戦モード).</summary>
        public const string Switch = "-chain";

        /// <summary>
        /// §12's default three: 錆槍の竜兵 → 瘴牙の走竜 → 鉄壁の門竜 (v4.1 named them 長柄の歪み兵 →
        /// 影走りの犬 → 甲冑の番人). The layers are the first each appears on in roster §9 that keeps
        /// the chain going down: 1, 2, 4.
        /// </summary>
        public static readonly IReadOnlyList<ChainBattle> Default = new[]
        {
            Single("polearm_warped", 1),
            Single("shadow_hound", 2),
            Single("armored_warden", 4),
        };

        /// <summary>roster §9 「連戦の試験運用」: the nine, with the layer each is fought on.</summary>
        public static readonly IReadOnlyList<ChainBattle> Nine = new[]
        {
            Single("shadow_hound", 1),
            Single("polearm_warped", 2),
            new ChainBattle(new[] { "polearm_warped", "crossbow_hunter" }, 3),
            Single("rusted_revenant", 4),
            Single("mist_archer", 5),
            Single("armored_warden", 5),
            Single("twin_blade_warped", 6),
            Single("pack_alpha", 6),
            Single("abyss_angler", 6),
        };

        /// <summary>
        /// The 瘴気 density of each layer, 1〜7: `seven_layers_v4.md` §2 (1 / 1 / 2 / 3 / 4 / 5 / 7).
        /// roster §9 still reads the layer number as its density until #130's table is in; this
        /// follows the table.
        /// </summary>
        public static readonly IReadOnlyList<int> Densities = new[] { 1, 1, 2, 3, 4, 5, 7 };

        public static int DensityOf(int layer)
        {
            if (layer < 1 || layer > Densities.Count) throw new ArgumentOutOfRangeException(nameof(layer), layer, "Layers run 1〜7.");
            return Densities[layer - 1];
        }

        /// <summary>
        /// <c>-chain a,b,c</c>: battles split by commas; a battle is enemy ids joined by '+' (phase
        /// order, at most ENEMIES_MAX) and an optional '@layer'. A battle that names no layer is on
        /// the one before it (the first on layer 1). "3" or "default" is <see cref="Default"/>, "9"
        /// or "nine" is <see cref="Nine"/>. An unknown id is refused.
        /// </summary>
        public static IReadOnlyList<ChainBattle> Parse(string spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            string trimmed = spec.Trim();
            if (trimmed == "3" || trimmed == "default") return Default;
            if (trimmed == "9" || trimmed == "nine") return Nine;

            var battles = new List<ChainBattle>();
            int layer = 1;
            foreach (var raw in trimmed.Split(','))
            {
                string part = raw.Trim();
                if (part.Length == 0) throw new ArgumentException($"-chain \"{spec}\": an empty battle.", nameof(spec));
                int at = part.IndexOf('@');
                if (at >= 0)
                {
                    if (!int.TryParse(part.Substring(at + 1), NumberStyles.None, CultureInfo.InvariantCulture, out layer))
                    {
                        throw new ArgumentException($"-chain \"{spec}\": \"{part}\" has no layer number after '@'.", nameof(spec));
                    }
                    DensityOf(layer);
                    part = part.Substring(0, at);
                }
                var ids = new List<string>();
                foreach (var id in part.Split('+'))
                {
                    string each = id.Trim();
                    Enemies.ById(each);
                    ids.Add(each);
                }
                if (ids.Count > Constants.EnemiesMax)
                {
                    throw new ArgumentException($"-chain \"{spec}\": at most {Constants.EnemiesMax} enemies a battle (§7.4).", nameof(spec));
                }
                battles.Add(new ChainBattle(ids, layer));
            }
            return battles;
        }

        /// <summary>The order the command line names after <see cref="Switch"/>, or <see cref="Default"/> when it names none.</summary>
        public static IReadOnlyList<ChainBattle> FromArgs(IReadOnlyList<string> args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            for (int i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], Switch, StringComparison.Ordinal)) continue;
                if (i + 1 >= args.Count) throw new ArgumentException("-chain needs an order after it.", nameof(args));
                return Parse(args[i + 1]);
            }
            return Default;
        }

        private static ChainBattle Single(string id, int layer) => new ChainBattle(new[] { id }, layer);
    }

    /// <summary>Where a chain stands.</summary>
    public enum ChainStage
    {
        /// <summary>The next battle can be set up (<see cref="ChainRun.NextSetup"/>).</summary>
        Ready,

        /// <summary>A battle was won and another follows: rest or go on (<see cref="ChainRun.GoOn"/>).</summary>
        BetweenBattles,

        /// <summary>Lost, or the last battle won. <see cref="ChainRun.Again"/> runs the same order again.</summary>
        Over,
    }

    /// <summary>One fought battle of a chain, as the log keeps it.</summary>
    public sealed record ChainEntry(
        int Pass,
        int Battle,
        ChainBattle Spec,
        int MiasmaPercent,
        int MaxStamina,
        int StartHp,
        int StartStamina,
        bool RestedBefore,
        int StaminaLeft,
        BattleTally Tally);

    /// <summary>
    /// §12 連戦モード as rules, without a screen (#52): the order, what carries from one battle to
    /// the next (HP, current stamina, the 瘴気 gauge, the layer), the rest that may come between,
    /// the same order again after a clean sweep, and the log of every battle fought. Immutable:
    /// every move hands back a new run. The battles themselves are fought by the caller on the
    /// setup it hands out.
    ///
    /// 瘴気: before each battle the gauge gains density × 3% (<see cref="Chain.AccumulateMiasma"/>),
    /// and the battle's max stamina is what the gauge leaves (<see cref="Chain.MaxStaminaAt"/>). The
    /// rest adds none (階層間の休憩 does not accumulate).
    /// </summary>
    public sealed record ChainRun(
        IReadOnlyList<ChainBattle> Order,
        int Pass,
        int Index,
        ChainStage Stage,
        int? CarriedHp,
        int? CarriedStamina,
        int MiasmaPercent,
        bool RestedBeforeNext,
        IReadOnlyList<ChainEntry> Log)
    {
        /// <summary>A chain on this order, from its first battle, at full HP and stamina and 瘴気 0.</summary>
        public static ChainRun Start(IReadOnlyList<ChainBattle>? order = null)
        {
            order ??= ChainOrder.Default;
            if (order.Count == 0) throw new ArgumentException("A chain needs at least one battle.", nameof(order));
            return new ChainRun(order, 1, 0, ChainStage.Ready, null, null, 0, false, Array.Empty<ChainEntry>());
        }

        /// <summary>The battle being set up, or the last one fought.</summary>
        public ChainBattle Current => Order[Index];

        /// <summary>The layer the run stands on: the current battle's.</summary>
        public int Layer => Current.Layer;

        /// <summary>The gauge the next battle is fought under: the carried gauge plus its layer's share.</summary>
        public int MiasmaForNext => Chain.AccumulateMiasma(MiasmaPercent, Current.Density);

        /// <summary>The battles of the pass being run, in order.</summary>
        public IReadOnlyList<ChainEntry> ThisPass
        {
            get
            {
                var entries = new List<ChainEntry>();
                foreach (var entry in Log)
                {
                    if (entry.Pass == Pass) entries.Add(entry);
                }
                return entries;
            }
        }

        /// <summary>Every battle of the pass fought and won.</summary>
        public bool AllWon
        {
            get
            {
                var pass = ThisPass;
                if (Stage != ChainStage.Over || pass.Count != Order.Count) return false;
                foreach (var entry in pass)
                {
                    if (entry.Tally.Result != GameResult.Won) return false;
                }
                return true;
            }
        }

        /// <summary>The next battle: its enemies and line, the deck (TurnLoop.Start shuffles it), the carried HP and stamina, the max stamina the gauge leaves.</summary>
        public BattleSetup NextSetup(IReadOnlyList<CardInstance> deck)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (Stage != ChainStage.Ready) throw new InvalidOperationException($"ChainRun: no battle is ready ({Stage}).");
            var defs = Current.Defs;
            var more = new List<EnemyDef>();
            for (int i = 1; i < defs.Count; i++) more.Add(defs[i]);
            return new BattleSetup(
                defs[0], deck, Current.FieldCells,
                PlayerMaxStamina: Chain.MaxStaminaAt(MiasmaForNext),
                MoreEnemies: more.Count == 0 ? null : more,
                PlayerStartHp: CarriedHp,
                PlayerStartStamina: CarriedStamina);
        }

        /// <summary>
        /// The battle set up by <see cref="NextSetup"/> has ended: log it and carry what carries. A
        /// loss, or the last battle won, ends the pass.
        /// </summary>
        public ChainRun Finish(BattleState finished, IEnumerable<BattleEvent> events)
        {
            if (finished == null) throw new ArgumentNullException(nameof(finished));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (Stage != ChainStage.Ready) throw new InvalidOperationException($"ChainRun: no battle is being fought ({Stage}).");
            if (finished.Result == GameResult.Ongoing) throw new InvalidOperationException("ChainRun: the battle has not ended.");

            int miasma = MiasmaForNext;
            int maxStamina = Chain.MaxStaminaAt(miasma);
            var tally = Chain.Tally(finished, events);
            var entry = new ChainEntry(
                Pass, Index + 1, Current, miasma, maxStamina,
                CarriedHp.HasValue ? Math.Max(1, Math.Min(finished.Player.MaxHp, CarriedHp.Value)) : finished.Player.MaxHp,
                CarriedStamina.HasValue ? Math.Max(0, Math.Min(maxStamina, CarriedStamina.Value)) : maxStamina,
                RestedBeforeNext, finished.Player.Stamina, tally);
            var log = new List<ChainEntry>(Log) { entry };
            var (hp, stamina) = Chain.Carry(finished);
            bool more = tally.Result == GameResult.Won && Index + 1 < Order.Count;
            return this with
            {
                Stage = more ? ChainStage.BetweenBattles : ChainStage.Over,
                CarriedHp = hp,
                CarriedStamina = stamina,
                MiasmaPercent = miasma,
                RestedBeforeNext = false,
                Log = log,
            };
        }

        /// <summary>
        /// §12: between battles, 階層間の休憩 (HP +30% of the maximum, stamina full at the max the
        /// gauge leaves now) or straight on with what was carried. Offered after every battle won.
        /// </summary>
        public ChainRun GoOn(bool rest)
        {
            if (Stage != ChainStage.BetweenBattles) throw new InvalidOperationException($"ChainRun: there is no next battle ({Stage}).");
            int? hp = CarriedHp;
            int? stamina = CarriedStamina;
            if (rest)
            {
                var last = Log[Log.Count - 1].Tally;
                var rested = Chain.Rest(last.HpLeft, last.MaxHp, Chain.MaxStaminaAt(MiasmaPercent));
                hp = rested.Hp;
                stamina = rested.Stamina;
            }
            return this with { Index = Index + 1, Stage = ChainStage.Ready, CarriedHp = hp, CarriedStamina = stamina, RestedBeforeNext = rest };
        }

        /// <summary>
        /// §12: the same order again from its first battle — after a clean sweep, or to start over
        /// after a loss. The new pass starts fresh (full HP and stamina, 瘴気 0); the log keeps the
        /// battles of every pass.
        /// </summary>
        public ChainRun Again()
        {
            if (Stage != ChainStage.Over) throw new InvalidOperationException($"ChainRun: the pass has not ended ({Stage}).");
            return this with
            {
                Pass = Pass + 1,
                Index = 0,
                Stage = ChainStage.Ready,
                CarriedHp = null,
                CarriedStamina = null,
                MiasmaPercent = 0,
                RestedBeforeNext = false,
            };
        }
    }

    /// <summary>
    /// §12's result log, <c>chain-&lt;日時&gt;.json</c>: which battle the run reached, the HP left, the
    /// attributes of the cards played, the traits that held and the average turns — per battle and
    /// for the whole run. Written by hand so the core needs no JSON package (Unity's profile has none).
    /// </summary>
    public static class ChainLog
    {
        /// <summary>The log's own format tag, bumped when a field is renamed or removed.</summary>
        public const string Format = "chain-log/1";

        /// <summary>"chain-20261004-153000.json".</summary>
        public static string FileName(DateTime at) => "chain-" + at.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json";

        /// <summary>Writes the log into <paramref name="directory"/> (made if missing) and returns the file's path.</summary>
        public static string Write(string directory, ChainRun run, DateTime at)
        {
            if (directory == null) throw new ArgumentNullException(nameof(directory));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, FileName(at));
            File.WriteAllText(path, ToJson(run, at), new UTF8Encoding(false));
            return path;
        }

        /// <summary>The log as JSON text.</summary>
        public static string ToJson(ChainRun run, DateTime at)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            var sb = new StringBuilder();
            sb.Append("{\n");
            Field(sb, 1, "format", Str(Format));
            Field(sb, 1, "writtenAt", Str(at.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)));

            var order = new List<string>();
            foreach (var battle in run.Order) order.Add(BattleSpec(battle));
            Field(sb, 1, "order", "[" + string.Join(", ", order) + "]");

            var pass = run.ThisPass;
            Field(sb, 1, "passes", Num(run.Pass));
            Field(sb, 1, "result", Str(run.AllWon ? "won" : run.Stage == ChainStage.Over ? "lost" : "ongoing"));
            Field(sb, 1, "reachedBattle", Num(pass.Count == 0 ? 0 : pass[pass.Count - 1].Battle));

            sb.Append("  \"battles\": [");
            for (int i = 0; i < run.Log.Count; i++)
            {
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.Append("    ").Append(Entry(run.Log[i]));
            }
            sb.Append(run.Log.Count == 0 ? "],\n" : "\n  ],\n");

            sb.Append("  \"total\": ").Append(Total(run.Log)).Append('\n');
            sb.Append("}\n");
            return sb.ToString();
        }

        private static string Entry(ChainEntry e)
        {
            var t = e.Tally;
            return "{"
                + "\"pass\": " + Num(e.Pass)
                + ", \"battle\": " + Num(e.Battle)
                + ", \"enemies\": " + Ids(e.Spec.EnemyIds)
                + ", \"layer\": " + Num(e.Spec.Layer)
                + ", \"miasmaPercent\": " + Num(e.MiasmaPercent)
                + ", \"maxStamina\": " + Num(e.MaxStamina)
                + ", \"rested\": " + (e.RestedBefore ? "true" : "false")
                + ", \"startHp\": " + Num(e.StartHp)
                + ", \"startStamina\": " + Num(e.StartStamina)
                + ", \"result\": " + Str(ResultWord(t.Result))
                + ", \"turns\": " + Num(t.Turns)
                + ", \"hpLeft\": " + Num(t.HpLeft)
                + ", \"maxHp\": " + Num(t.MaxHp)
                + ", \"staminaLeft\": " + Num(e.StaminaLeft)
                + ", \"cardsPlayed\": " + Num(t.CardsPlayed)
                + ", \"attributes\": " + Attributes(t)
                + ", \"traitsFired\": " + Num(t.TraitsFired)
                + "}";
        }

        private static string Total(IReadOnlyList<ChainEntry> log)
        {
            if (log.Count == 0)
            {
                return "{\"battles\": 0, \"turns\": 0, \"averageTurns\": 0, \"cardsPlayed\": 0, \"attributes\": "
                    + Attributes(null) + ", \"traitsFired\": 0, \"hpLeft\": 0}";
            }
            var tallies = new List<BattleTally>();
            foreach (var entry in log) tallies.Add(entry.Tally);
            var total = Chain.Total(tallies);
            double average = Math.Round((double)total.Turns / tallies.Count, 2, MidpointRounding.AwayFromZero);
            return "{"
                + "\"battles\": " + Num(tallies.Count)
                + ", \"turns\": " + Num(total.Turns)
                + ", \"averageTurns\": " + average.ToString("0.##", CultureInfo.InvariantCulture)
                + ", \"cardsPlayed\": " + Num(total.CardsPlayed)
                + ", \"attributes\": " + Attributes(total)
                + ", \"traitsFired\": " + Num(total.TraitsFired)
                + ", \"hpLeft\": " + Num(total.HpLeft)
                + "}";
        }

        private static string BattleSpec(ChainBattle battle) =>
            "{\"enemies\": " + Ids(battle.EnemyIds) + ", \"layer\": " + Num(battle.Layer) + "}";

        /// <summary>All four attributes (§2.1) every time, so the shape does not depend on what was played.</summary>
        private static string Attributes(BattleTally? tally)
        {
            var parts = new List<string>();
            foreach (var attribute in BattleTally.Order)
            {
                parts.Add(Str(attribute.ToString().ToLowerInvariant()) + ": " + Num(tally?.CountOf(attribute) ?? 0));
            }
            return "{" + string.Join(", ", parts) + "}";
        }

        private static string ResultWord(GameResult result) => result switch
        {
            GameResult.Won => "won",
            GameResult.Lost => "lost",
            _ => "ongoing",
        };

        private static string Ids(IReadOnlyList<string> ids)
        {
            var parts = new List<string>();
            foreach (var id in ids) parts.Add(Str(id));
            return "[" + string.Join(", ", parts) + "]";
        }

        private static void Field(StringBuilder sb, int indent, string name, string value)
        {
            sb.Append(' ', indent * 2).Append(Str(name)).Append(": ").Append(value).Append(",\n");
        }

        private static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);

        private static string Str(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
