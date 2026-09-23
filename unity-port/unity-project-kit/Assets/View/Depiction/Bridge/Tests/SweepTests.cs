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
