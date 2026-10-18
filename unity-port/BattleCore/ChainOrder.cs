using System;
using System.Collections.Generic;
using System.Globalization;

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
    /// §12: the orders a chain is fought in — the default three, roster §9's nine, or one spelt out
    /// as <c>a,b,c</c>. The layer bench (BattleCore.Sim) reads its chains through <see cref="Parse"/>.
    /// The chain the screen plays is DemoSession's (#518 removed the screenless ChainRun).
    /// </summary>
    public static class ChainOrder
    {
        /// <summary>
        /// §12's default three: 錆槍の竜兵 → 瘴牙の走竜 → 鉄壁の門竜. The layers are the first each appears on in roster §9 that keeps
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
        /// The densities come from that table; the roster no longer carries them (v4.11, #320).
        /// </summary>
        public static readonly IReadOnlyList<int> Densities = new[] { 1, 1, 2, 3, 4, 5, 7 };

        public static int DensityOf(int layer)
        {
            if (layer < 1 || layer > Densities.Count) throw new ArgumentOutOfRangeException(nameof(layer), layer, "Layers run 1〜7.");
            return Densities[layer - 1];
        }

        /// <summary>
        /// <c>a,b,c</c>: battles split by commas; a battle is enemy ids joined by '+' (phase
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
                if (part.Length == 0) throw new ArgumentException($"Chain \"{spec}\": an empty battle.", nameof(spec));
                int at = part.IndexOf('@');
                if (at >= 0)
                {
                    if (!int.TryParse(part.Substring(at + 1), NumberStyles.None, CultureInfo.InvariantCulture, out layer))
                    {
                        throw new ArgumentException($"Chain \"{spec}\": \"{part}\" has no layer number after '@'.", nameof(spec));
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
                    throw new ArgumentException($"Chain \"{spec}\": at most {Constants.EnemiesMax} enemies a battle (§7.4).", nameof(spec));
                }
                var battle = new ChainBattle(ids, layer);
                if (battle.FieldCells > Constants.FieldCellsMax)
                {
                    throw new ArgumentException(
                        $"Chain \"{spec}\": \"{part}\" needs {battle.FieldCells} cells to start at START_GAP, more than the line's {Constants.FieldCellsMax} (§7.1).",
                        nameof(spec));
                }
                battles.Add(battle);
            }
            return battles;
        }

        private static ChainBattle Single(string id, int layer) => new ChainBattle(new[] { id }, layer);
    }
}
