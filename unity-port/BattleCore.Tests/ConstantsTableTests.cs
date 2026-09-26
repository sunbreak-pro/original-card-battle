using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// battle_core_v4.md §10 is the single source of truth for numbers (#47). This reads the table
    /// out of the canon itself and holds every row against the code, so a row changed on either side
    /// alone — or a row added to the canon with no constant behind it — fails here.
    ///
    /// The copy Unity runs (Assets/Tests) has no canon beside it, so there the test is skipped.
    /// </summary>
    public class ConstantsTableTests
    {
        private const string CanonPath = ".claude/docs/battle_document/battle_core_v4.md";

        private static string Join(params int[] values) => string.Join(" / ", values);

        private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>Each §10 row, as the canon prints its value. Null: a row the code has no number for.</summary>
        private static readonly Dictionary<string, string?> Expected = new Dictionary<string, string?>
        {
            ["HAND_DRAW"] = Join(Constants.HandDraw),
            ["HAND_DRAW_MIN / MAX"] = Join(Constants.HandDrawMin, Constants.HandDrawMax),
            ["HAND_LIMIT"] = Join(Constants.HandLimit),
            ["DECK_MIN / MAX"] = Join(Constants.DeckMin, Constants.DeckMax),
            ["COPIES_MAX"] = Join(Constants.CopiesMax),
            ["STANCE_CARDS_MAX"] = Join(Constants.StanceCardsMax),
            ["OWNED_KINDS_MAX"] = Join(Constants.OwnedKindsMax),
            ["COLUMN_COUNT"] = Join(Constants.ColumnCount),
            ["COST_MIN / MAX"] = Join(Constants.CostMin, Constants.CostMax),
            ["STAMINA_RECOVERY"] = Join(Constants.StaminaRecovery),
            ["BASE_MAX_STAMINA"] = Join(Constants.BaseMaxStamina),
            ["PLAYER_MAX_HP"] = Join(Constants.PlayerMaxHp),
            ["RESERVE_THRESHOLD"] = Join(Constants.ReserveThreshold),
            ["RESERVE_GUARD"] = Join(Constants.ReserveGuard),
            ["DESPERATE_THRESHOLD"] = Join(Constants.DesperateThreshold),
            ["TRAIT_CONDITIONS / EFFECTS"] = Join(Constants.TraitConditions, Constants.TraitEffects),
            ["POSITION_TRAIT_CARDS"] = Join(Constants.PositionTraitCards),
            ["FIELD_CELLS"] = "戦闘ごと",
            ["CELL_CAPACITY"] = Join(Constants.CellCapacity),
            ["ENEMY_SIZE_MAX"] = Join(Constants.EnemySizeMax),
            ["START_GAP"] = Join(Constants.StartGap),
            ["REACH_DEFAULT"] = Reach.Default.ToText(),
            ["MOVE_STEP_MAX"] = Join(Constants.MoveStepMax),
            ["WALL_DAMAGE"] = Join(Constants.WallDamage),
            ["GAP_SCALING_CARDS_MAX"] = Join(Constants.GapScalingCardsMax),
            ["PLAIN_CARD_BONUS"] = "+" + Constants.PlainCardBonus,
            ["SAME_TRAIT_MAX"] = Join(Constants.SameTraitMax),
            ["DUAL_FACE_RATIO"] = Number(Columns.DualFaceRatio),
            ["STATUS_KINDS_PLAYER"] = Join(Constants.StatusKindsPlayer),
            ["STATUS_KINDS_ENEMY"] = "上限なし",
            ["STANCE_SLOTS"] = Join(Constants.StanceSlots),
            ["BOSS_STATUS_KINDS"] = Join(Constants.BossStatusKinds),
            ["ENEMIES_MAX"] = Join(Constants.EnemiesMax),
            ["ELITE_ACTIONS"] = Join(Constants.EliteActions),
            ["OMEN_DEPTH_ELITE"] = Join(Constants.OmenDepthElite),
            ["EMPOWER_MULT"] = Number(Constants.EmpowerMult),
            ["FRAGILE_MULT"] = Number(Constants.FragileMult),
            ["MASTERY_THRESHOLDS"] = Join(Constants.MasteryThresholds.ToArray()),
            ["CHAIN_BATTLES_DEFAULT"] = Join(Constants.ChainBattlesDefault),
        };

        private static string? FindCanon()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, CanonPath);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        /// <summary>The rows of §10's table: name → value, as printed.</summary>
        private static Dictionary<string, string> ReadTable(string path)
        {
            var rows = new Dictionary<string, string>();
            bool inside = false;
            foreach (string line in File.ReadLines(path))
            {
                if (line.StartsWith("## 10.", StringComparison.Ordinal)) { inside = true; continue; }
                if (!inside) continue;
                if (line.StartsWith("## ", StringComparison.Ordinal)) break;
                if (!line.StartsWith("|", StringComparison.Ordinal)) continue;

                string[] cells = line.Split('|').Select(c => c.Trim()).ToArray();
                if (cells.Length < 4) continue;
                string name = cells[1];
                if (name == "定数" || name.StartsWith("-", StringComparison.Ordinal)) continue;
                rows[name] = cells[2];
            }
            return rows;
        }

        [Test]
        public void EveryRowOfSection10_MatchesTheCode()
        {
            string? path = FindCanon();
            if (path == null) Assert.Ignore("battle_core_v4.md is not beside this copy of the tests (Unity).");

            var table = ReadTable(path!);
            Assert.That(table, Is.Not.Empty, "§10's table was found");
            Assert.Multiple(() =>
            {
                foreach (var row in table)
                {
                    Assert.That(Expected.ContainsKey(row.Key), Is.True, $"§10 has {row.Key}, the code has no constant for it");
                    if (Expected.TryGetValue(row.Key, out var value)) Assert.That(row.Value, Is.EqualTo(value), row.Key);
                }
                foreach (string name in Expected.Keys)
                {
                    Assert.That(table.ContainsKey(name), Is.True, $"the code checks {name}, §10 no longer has it");
                }
            });
        }
    }
}
