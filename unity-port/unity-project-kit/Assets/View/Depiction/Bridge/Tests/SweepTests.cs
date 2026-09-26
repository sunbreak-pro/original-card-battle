// #192: every enemy of the demo, fought to the end under many seeds with random decks of the
// eighty, driven the way an unattended run is (CoreBattleSource's suggested card, else end the
// turn). A battle still going at the turn limit is a stall. The table of win rates and turns is
// printed for the PR; the balance reading of it is not asserted.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class SweepTests
    {
        /// <summary>A battle not decided by this turn is a stall (the plan's 60, 2026-09-23).</summary>
        public const int StallTurns = 60;

        public const int Seeds = 20;

        /// <summary>One battle, the way an unattended run plays it. Null when it hit the turn limit.</summary>
        public static CoreBattleSource Fight(EnemyDef enemy, List<CardInstance> deck, int seed)
        {
            var source = new CoreBattleSource(new BattleSetup(enemy, deck, DemoSession.FieldCellsFor(enemy)), seed, suggestCards: true);
            int events = 0;
            while (!source.Finished)
            {
                if (source.State.Turn > StallTurns) return null;
                if (++events > StallTurns * 60) throw new InvalidOperationException("the source stopped moving");
                if (!source.WaitingForPlayer)
                {
                    source.AdvanceAuto();
                    continue;
                }
                string card = source.SuggestedCardId;
                if (card.Length == 0)
                {
                    source.EndTurn();
                    continue;
                }
                CardFace face = DepictionText.Find(source.Frame.Hand, card);
                DepictionEvent played;
                PlayVerdict verdict = source.TryPlay(card, DepictionText.RequiredZone(face.Aim), out played);
                if (verdict != PlayVerdict.Accepted) throw new InvalidOperationException("the suggested card " + card + " was refused: " + verdict);
            }
            return source;
        }

        [TestCaseSource(nameof(AllIds))]
        public void EveryEnemy_UnderTwentySeeds_EndsWithoutAStall(string id)
        {
            EnemyDef enemy = Enemies.ById(id);
            var stalls = new List<int>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                List<CardInstance> deck = DeckBuilder.Random(seed * 7919 + id.Length).Build();
                CoreBattleSource source = null;
                Assert.DoesNotThrow(() => source = Fight(enemy, deck, seed), id + " seed " + seed);
                if (source == null) stalls.Add(seed);
            }
            Assert.That(stalls, Is.Empty, id + ": stalled at seeds " + string.Join(", ", stalls));
        }

        /// <summary>
        /// #205's replay on a fixed deck of 29 with three stance cards, so a change to the random decks
        /// (#206) leaves it alone. Measured 2026-09-26 against 大黒蛇 セルク, seed 21: uncapped, 鈍足
        /// peaked at 11 (turn 12: 鈍足 8 / 疲労 5; 疲労 6 and the priest's 再生 5 at their highest);
        /// capped, 鈍足, 疲労 and the priest's 再生 all peak at 4. The battle is lost at turn 15 either way.
        /// </summary>
        public const string ReplayDeck =
            "overhead:1,flat_strike:1,reach_thrust:1,brace:1,iron_block:2,deep_breath:1,first_aid:1,observe:1," +
            "back_leap:1,slide_step:1,stone_throw:2,parry_cut:1,twist_away:1,abyss_stance:1,haul_step:1,bulwark:2," +
            "whirlwind:1,snap_guard:1,deflect:1,pommel_strike:1,keen_eye:1,resolve:1,gale_thrust:2,anchor_stance:1,vital_thrust:1";

        [Test]
        public void TheIssuesReplay_MiasmaPriestSeed21_HoldsFatigueSlowAndRegenAtTheCap()
        {
            CoreBattleSource source = Fight(Enemies.MiasmaPriest, DeckBuilder.Load(ReplayDeck).Build(), 21);
            Assert.That(source, Is.Not.Null, "the replay ends without a stall");

            List<StatusApplied> turnDecay = source.History.OfType<StatusApplied>()
                .Where(a => Statuses.DecayOf(a.Kind) == StatusDecay.OnTurn).ToList();
            int Peak(Actor target, StatusKind kind) =>
                turnDecay.Where(a => a.Target == target && a.Kind == kind).Select(a => a.StacksAfter).DefaultIfEmpty(0).Max();

            Assert.Multiple(() =>
            {
                Assert.That(turnDecay.Max(a => a.StacksAfter), Is.EqualTo(Constants.TurnDecayStackMax), "the words reach the cap and stop there");
                Assert.That(Peak(Actor.Player, StatusKind.Slow), Is.EqualTo(Constants.TurnDecayStackMax), "鈍足");
                Assert.That(Peak(Actor.Player, StatusKind.Fatigue), Is.EqualTo(Constants.TurnDecayStackMax), "疲労");
                Assert.That(Peak(Actor.Enemy, StatusKind.Regen), Is.EqualTo(Constants.TurnDecayStackMax), "the priest's 再生");
            });
        }

        [Test, Explicit("prints the table for the #192 PR; slow")]
        public void PrintTheTable()
        {
            var text = new StringBuilder();
            text.AppendLine("| 敵 | 階級 | 勝ち | 負け | 詰まり | 勝率 | 平均ターン | 最長 |");
            text.AppendLine("| --- | --- | --: | --: | --: | --: | --: | --: |");
            foreach (EnemyDef enemy in Enemies.All)
            {
                int won = 0, lost = 0, stalled = 0, longest = 0;
                var turns = new List<int>();
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    CoreBattleSource source = Fight(enemy, DeckBuilder.Random(seed * 7919 + enemy.Id.Length).Build(), seed);
                    if (source == null)
                    {
                        stalled++;
                        continue;
                    }
                    if (source.State.Result == GameResult.Won) won++;
                    else lost++;
                    turns.Add(source.State.Turn);
                    longest = Math.Max(longest, source.State.Turn);
                }
                string rank = enemy.Rank == EnemyRank.Boss ? "ボス" : enemy.Rank == EnemyRank.Elite ? "精鋭" : "通常";
                double rate = 100.0 * won / Seeds;
                string average = turns.Count == 0 ? "—" : turns.Average().ToString("0.0", CultureInfo.InvariantCulture);
                text.AppendLine("| " + enemy.Name + " | " + rank + " | " + won + " | " + lost + " | " + stalled + " | "
                    + rate.ToString("0", CultureInfo.InvariantCulture) + "% | " + average + " | " + longest + " |");
            }
            TestContext.Out.WriteLine(text.ToString());
        }

        private static IEnumerable<string> AllIds()
        {
            return Enemies.All.Select(e => e.Id);
        }
    }
}
