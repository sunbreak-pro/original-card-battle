// The demo's run of battles (#191): one enemy or §12's chain, what carries, the rest, the end of
// the run, and the words of the mode and end screens.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class DemoSessionTests
    {
        private static List<CardInstance> Deck()
        {
            return DeckBuilder.Random(9).Build();
        }

        /// <summary>Fights the source to the end the way an unattended run does (leftmost payable card).</summary>
        private static void FightToTheEnd(CoreBattleSource source)
        {
            int guard = 0;
            while (!source.Finished && guard++ < 3000)
            {
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
                source.TryPlay(card, DepictionText.RequiredZone(face.Aim), out played);
            }
            Assert.That(source.Finished, Is.True);
        }

        // ---- the line and the setup ----

        [TestCase("polearm_warped", 6)]
        [TestCase("shadow_hound", 6)]
        [TestCase("armored_warden", 7)]
        [TestCase("miasma_priest", 7)]
        [TestCase("abyss_angler", 7)]
        [TestCase("distortion_root", 7)]
        public void TheLine_IsLongEnoughToStartAtGapThree(string id, int cells)
        {
            EnemyDef enemy = Enemies.ById(id);
            Assert.That(DemoSession.FieldCellsFor(enemy), Is.EqualTo(cells));
            BattleSetup setup = DemoSession.Single(Deck(), id, 1).NextSetup();
            Assert.That(setup.FieldCells, Is.EqualTo(cells));
            Assert.That(TurnLoop.Start(setup, new SeededRng(1)).State.Gap, Is.EqualTo(Constants.StartGap));
        }

        [Test]
        public void ASingleBattle_IsTheChosenEnemy_AtFullHp_OneOfOne()
        {
            var session = DemoSession.Single(Deck(), "mist_archer", 5);
            CoreBattleSource source = session.StartBattle();
            Assert.Multiple(() =>
            {
                Assert.That(session.Mode, Is.EqualTo(DemoMode.Single));
                Assert.That(source.State.EnemyDef.Id, Is.EqualTo("mist_archer"));
                Assert.That(source.State.Player.Hp, Is.EqualTo(Constants.PlayerMaxHp));
                Assert.That(source.Frame.Corner.ChainIndex, Is.EqualTo(1));
                Assert.That(source.Frame.Corner.ChainTotal, Is.EqualTo(1));
                Assert.That(session.Stage, Is.EqualTo(DemoStage.Fighting));
            });
        }

        [Test]
        public void ADeckThatBreaksTheRules_IsRefused()
        {
            var nineteen = DeckBuilder.Random(1, 20).Build().Take(19).ToList();
            Assert.Throws<ArgumentException>(() => DemoSession.Single(nineteen, "polearm_warped", 1));
            Assert.Throws<ArgumentException>(() => DemoSession.Chain(nineteen, 1));
        }

        [Test]
        public void TheRandomPick_IsOneOfTheEleven_FixedBySeed()
        {
            var picked = Enumerable.Range(0, 200).Select(DemoSession.RandomEnemyId).ToList();
            Assert.That(picked.All(id => Enemies.All.Any(e => e.Id == id)), Is.True);
            Assert.That(picked.Distinct().Count(), Is.EqualTo(Enemies.All.Count), "200 seeds reach every enemy");
            Assert.That(DemoSession.RandomEnemyId(42), Is.EqualTo(DemoSession.RandomEnemyId(42)));
        }

        // ---- the chain (§12) ----

        [Test]
        public void TheDefaultChain_IsTheThreeOfSection12()
        {
            var session = DemoSession.Chain(Deck(), 1);
            Assert.That(session.Order.Select(e => e.Id), Is.EqualTo(new[] { "polearm_warped", "shadow_hound", "armored_warden" }));
            Assert.That(DemoSession.ChainLine(), Is.EqualTo("連戦（3 戦）: 錆槍の竜兵 → 瘴牙の走竜 → 鉄壁の門竜"));
            Assert.That(Constants.ChainBattlesDefault, Is.EqualTo(session.Order.Count));
        }

        [Test]
        public void AChain_CarriesHpAndStamina_AndReshufflesTheDeck()
        {
            // Find a seed whose first battle is won, then look at what the second starts from.
            for (int seed = 1; seed < 40; seed++)
            {
                var session = DemoSession.Chain(Deck(), seed);
                CoreBattleSource first = session.StartBattle(suggestCards: true);
                FightToTheEnd(first);
                BattleTally tally = session.Finish(first);
                if (tally.Result != GameResult.Won) continue;

                Assert.That(session.Stage, Is.EqualTo(DemoStage.BetweenBattles));
                int hp = first.State.Player.Hp;
                int stamina = first.State.Player.Stamina;
                session.GoOn(rest: false);
                CoreBattleSource second = session.StartBattle();
                BattleState state = second.State;
                Assert.Multiple(() =>
                {
                    Assert.That(state.EnemyDef.Id, Is.EqualTo("shadow_hound"));
                    Assert.That(state.Player.Hp, Is.EqualTo(hp), "HP carries");
                    Assert.That(state.Player.Stamina, Is.EqualTo(stamina), "current stamina carries");
                    Assert.That(state.Player.Stance, Is.Null);
                    Assert.That(state.DrawPile.Select(c => c.InstanceId), Is.EquivalentTo(Deck().Select(c => c.InstanceId)), "the whole deck again");
                    Assert.That(second.Frame.Corner.ChainIndex, Is.EqualTo(2));
                    Assert.That(second.Frame.Corner.ChainTotal, Is.EqualTo(3));
                });
                return;
            }
            Assert.Fail("no seed won the first battle");
        }

        [Test]
        public void TheRest_GivesThirtyPercentAndFullStamina_BeforeTheNextBattle()
        {
            for (int seed = 1; seed < 40; seed++)
            {
                var session = DemoSession.Chain(Deck(), seed);
                CoreBattleSource first = session.StartBattle(suggestCards: true);
                FightToTheEnd(first);
                if (session.Finish(first).Result != GameResult.Won) continue;

                int hp = first.State.Player.Hp;
                DemoEndScreen screen = session.EndScreen();
                session.GoOn(rest: true);
                BattleState state = session.StartBattle().State;
                int expected = Math.Min(Constants.PlayerMaxHp, hp + 15);
                Assert.Multiple(() =>
                {
                    Assert.That(state.Player.Hp, Is.EqualTo(expected));
                    Assert.That(state.Player.Stamina, Is.EqualTo(Constants.BaseMaxStamina));
                    Assert.That(screen.CanGoOn, Is.True);
                    Assert.That(screen.RestLabel, Is.EqualTo("休んで次へ（HP +" + (expected - hp) + "、スタミナ全回復）"));
                    Assert.That(screen.GoOnLabel, Is.EqualTo("そのまま次へ"));
                });
                return;
            }
            Assert.Fail("no seed won the first battle");
        }

        [Test]
        public void ALoss_EndsTheChain()
        {
            // A chain against three bosses is lost early (the leftmost auto-player rarely beats them).
            for (int seed = 1; seed < 40; seed++)
            {
                var session = DemoSession.Chain(Deck(), seed, new[] { "abyss_angler", "distortion_root", "miasma_priest" });
                CoreBattleSource source = session.StartBattle(suggestCards: true);
                FightToTheEnd(source);
                if (session.Finish(source).Result != GameResult.Lost) continue;

                DemoEndScreen screen = session.EndScreen();
                Assert.Multiple(() =>
                {
                    Assert.That(session.Stage, Is.EqualTo(DemoStage.Over));
                    Assert.That(session.AllWon, Is.False);
                    Assert.That(() => session.GoOn(false), Throws.InvalidOperationException);
                    Assert.That(() => session.StartBattle(), Throws.InvalidOperationException);
                    Assert.That(screen.Title, Is.EqualTo("1 戦目で力尽きました"));
                    Assert.That(screen.Won, Is.False);
                    Assert.That(screen.CanGoOn, Is.False);
                    Assert.That(screen.AgainLabel, Is.EqualTo("最初からもう一度"));
                    Assert.That(screen.BackLabel, Is.EqualTo("デッキ選択へ戻る"));
                });

                session.Again();
                BattleState again = session.StartBattle().State;
                Assert.That(again.EnemyDef.Id, Is.EqualTo("abyss_angler"));
                Assert.That(again.Player.Hp, Is.EqualTo(Constants.PlayerMaxHp), "again starts at full HP");
                Assert.That(session.Tallies, Is.Empty);
                return;
            }
            Assert.Fail("no seed lost the first battle");
        }

        [Test]
        public void AChainWonToTheEnd_OffersTheSameOrderAgain()
        {
            // A one-battle chain against the polearm: won once, it is a clean sweep.
            for (int seed = 1; seed < 40; seed++)
            {
                var session = DemoSession.Chain(Deck(), seed, new[] { "polearm_warped" });
                CoreBattleSource source = session.StartBattle(suggestCards: true);
                FightToTheEnd(source);
                if (session.Finish(source).Result != GameResult.Won) continue;

                DemoEndScreen screen = session.EndScreen();
                Assert.That(session.AllWon, Is.True);
                Assert.That(screen.Title, Is.EqualTo("連戦に全勝しました"));
                Assert.That(screen.AgainLabel, Is.EqualTo("同じ並びをもう一度"));
                return;
            }
            Assert.Fail("no seed won");
        }

        // ---- the end screen's values ----

        [Test]
        public void TheEndScreen_ShowsSection12sItems_FromTheBattlesOwnEvents()
        {
            var session = DemoSession.Single(Deck(), "polearm_warped", 3);
            CoreBattleSource source = session.StartBattle(suggestCards: true);
            FightToTheEnd(source);
            BattleTally tally = session.Finish(source);
            DemoEndScreen screen = session.EndScreen();
            BattleState state = source.State;

            Assert.Multiple(() =>
            {
                Assert.That(tally.Turns, Is.EqualTo(state.Turn));
                Assert.That(tally.HpLeft, Is.EqualTo(state.Player.Hp));
                Assert.That(tally.CardsPlayed, Is.EqualTo(source.History.OfType<CardPlayed>().Count()));
                Assert.That(screen.Title, Is.EqualTo(state.Result == GameResult.Won ? "錆槍の竜兵に勝ちました" : "錆槍の竜兵に敗れました"));
                Assert.That(screen.Lines, Is.EqualTo(new[]
                {
                    "相手: 錆槍の竜兵",
                    "残り HP: " + state.Player.Hp + " / 50",
                    "ターン数: " + state.Turn,
                    "使った札 " + tally.CardsPlayed + " 枚の属性: " + DemoSession.Breakdown(tally),
                    "特性の発動: " + tally.TraitsFired + " 回",
                }));
                Assert.That(screen.AgainLabel, Is.EqualTo("もう一度"));
                Assert.That(session.Stage, Is.EqualTo(DemoStage.Over));
            });
        }

        [Test]
        public void TheBreakdown_AndTheAverage_ReadInOrder()
        {
            var tally = new BattleTally("polearm_warped", GameResult.Won, 7, 30, 50, 9,
                new Dictionary<BattleAttribute, int> { [BattleAttribute.Guard] = 3, [BattleAttribute.Attack] = 6, [BattleAttribute.Move] = 2 }, 4);
            Assert.That(DemoSession.Breakdown(tally), Is.EqualTo("攻撃 6・ムーブ 2・防御 3"));
            Assert.That(DemoSession.Breakdown(new BattleTally("x", GameResult.Lost, 1, 0, 50, 0, new Dictionary<BattleAttribute, int>(), 0)), Is.EqualTo("なし"));
            Assert.That(DemoSession.EnemyLine(Enemies.AbyssAngler), Is.EqualTo("獄竜 ガルド（ボス・HP 180）"));
        }

        [Test]
        public void AChainsEndScreen_ShowsEachLine_AndTheWholeRunAddedUp()
        {
            // A two-battle chain won once and then ended: 何戦目, the battle's own items, the average
            // turns, and (the run over) the totals over both battles (§12).
            for (int seed = 1; seed < 60; seed++)
            {
                var session = DemoSession.Chain(Deck(), seed, new[] { "polearm_warped", "pack_alpha" });
                CoreBattleSource first = session.StartBattle(suggestCards: true);
                FightToTheEnd(first);
                BattleTally one = session.Finish(first);
                if (one.Result != GameResult.Won) continue;

                DemoEndScreen between = session.EndScreen();
                Assert.That(between.Lines, Is.EqualTo(new[]
                {
                    "何戦目: 1 / 2（錆槍の竜兵）",
                    "残り HP: " + one.HpLeft + " / 50",
                    "ターン数: " + one.Turns,
                    "使った札 " + one.CardsPlayed + " 枚の属性: " + DemoSession.Breakdown(one),
                    "特性の発動: " + one.TraitsFired + " 回",
                    "次: 統牙の長竜",
                }));
                Assert.That(between.Title, Is.EqualTo("1 戦目に勝ちました"));

                session.GoOn(rest: false);
                CoreBattleSource second = session.StartBattle(suggestCards: true);
                FightToTheEnd(second);
                BattleTally two = session.Finish(second);
                BattleTally total = Chain.Total(session.Tallies);
                DemoEndScreen end = session.EndScreen();
                string average = DemoSession.AverageOf(session.Tallies);

                Assert.Multiple(() =>
                {
                    Assert.That(session.Stage, Is.EqualTo(DemoStage.Over));
                    Assert.That(end.Lines, Is.EqualTo(new[]
                    {
                        "何戦目: 2 / 2（統牙の長竜）",
                        "残り HP: " + two.HpLeft + " / 50",
                        "ターン数: " + two.Turns + "（平均 " + average + "）",
                        "使った札 " + two.CardsPlayed + " 枚の属性: " + DemoSession.Breakdown(two),
                        "特性の発動: " + two.TraitsFired + " 回",
                        "連戦の合計（2 戦）: 使った札 " + (one.CardsPlayed + two.CardsPlayed) + " 枚の属性: " + DemoSession.Breakdown(total),
                        "連戦の合計: 特性の発動 " + (one.TraitsFired + two.TraitsFired) + " 回、平均ターン数 " + average,
                    }));
                    Assert.That(total.CardsPlayed, Is.EqualTo(one.CardsPlayed + two.CardsPlayed));
                    Assert.That(total.CountOf(BattleAttribute.Attack), Is.EqualTo(one.CountOf(BattleAttribute.Attack) + two.CountOf(BattleAttribute.Attack)));
                    Assert.That(total.Turns, Is.EqualTo(one.Turns + two.Turns));
                });
                return;
            }
            Assert.Fail("no seed won the first battle");
        }

        [TestCase(new[] { 8, 9 }, "8.5")]
        [TestCase(new[] { 8, 8 }, "8")]
        [TestCase(new[] { 6, 7, 7 }, "6.7")]
        [TestCase(new[] { 5 }, "5")]
        public void TheAverageTurns_RoundToOneDecimal(int[] turns, string expected)
        {
            var tallies = turns.Select(t => new BattleTally("polearm_warped", GameResult.Won, t, 50, 50, 0, new Dictionary<BattleAttribute, int>(), 0)).ToList();
            Assert.That(DemoSession.AverageOf(tallies), Is.EqualTo(expected));
            Assert.That(DemoSession.AverageOf(new List<BattleTally>()), Is.EqualTo("0"));
        }

        [Test]
        public void EachRun_DealsAfresh_AndTheRandomPickMovesOn()
        {
            // Choosing the same mode again from the same seed: another deal, and another random enemy draw.
            var first = DemoSession.Single(Deck(), "polearm_warped", 7, 0).StartBattle().State;
            var again = DemoSession.Single(Deck(), "polearm_warped", 7, 1).StartBattle().State;
            Assert.That(again.DrawPile.Select(c => c.InstanceId), Is.Not.EqualTo(first.DrawPile.Select(c => c.InstanceId)));
            Assert.That(Enumerable.Range(0, 30).Select(p => DemoSession.RandomEnemyId(7, p)).Distinct().Count(), Is.GreaterThan(1));
        }

        [Test]
        public void TheModeScreensWords_AreTheBridges()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DemoSession.ModeTitle, Is.EqualTo("戦い方を選ぶ"));
                Assert.That(DemoSession.SingleHeading, Is.EqualTo("1 体で区切る（敵を選ぶ）"));
                Assert.That(DemoSession.ChainHeading, Is.EqualTo("連戦（HP とスタミナを持ち越し、負けたら終わり）"));
                Assert.That(DemoSession.RandomLabel, Is.EqualTo("ランダム"));
                Assert.That(DemoSession.BackToDeckLabel, Is.EqualTo("デッキ選択へ戻る"));
            });
        }

        [Test]
        public void AgainDuringABattle_IsRefused()
        {
            var session = DemoSession.Single(Deck(), "polearm_warped", 1);
            session.StartBattle();
            Assert.Throws<InvalidOperationException>(() => session.Again());
        }

        [Test]
        public void TheStages_RefuseMovesOutOfTurn()
        {
            var session = DemoSession.Single(Deck(), "polearm_warped", 1);
            Assert.Throws<InvalidOperationException>(() => session.EndScreen());
            CoreBattleSource source = session.StartBattle();
            Assert.Throws<InvalidOperationException>(() => session.StartBattle());
            Assert.Throws<InvalidOperationException>(() => session.Finish(source), "the battle has not ended");
        }
    }
}
