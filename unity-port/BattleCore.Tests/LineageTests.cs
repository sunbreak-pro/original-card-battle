using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace BattleCore.Tests
{
    /// <summary>
    /// #962: the lineages' rules (battle_core_v4 §7.5, enemy_roster_v4 §1.9), the king rank, the five
    /// words of the lineage bosses (§5) and the three bosses of roster §13〜§15 that carry them.
    /// </summary>
    public class LineageTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static readonly CardDef Filler = Fixtures.Card("filler", face: new Face(Guard: 1), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        private static readonly EnemyActionDef Wait =
            Fixtures.EnemyAction("wait", face: new Face(Guard: 1), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        /// <summary>An enemy that only raises 1 Guard, so whatever happens to the player is the rule's doing.</summary>
        private static readonly EnemyDef Idle = Fixtures.Enemy("idle", atZero: Wait, atOneToTwo: Wait, atThreePlus: Wait);

        private static List<CardInstance> Deck(params CardDef[] first)
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < first.Length; i++) deck.Add(new CardInstance(first[i].Id + "-" + i, first[i]));
            for (int i = deck.Count; i < Constants.DeckMin; i++) deck.Add(new CardInstance("filler-" + i, Filler));
            return deck;
        }

        private static StepResult Started(EnemyDef enemy, int cells, int gap = Constants.StartGap, params CardDef[] first) =>
            TurnLoop.Start(new BattleSetup(enemy, Deck(first), cells, StartGap: gap), NoShuffle);

        private static BattleState Opened(EnemyDef enemy, int cells, int gap = Constants.StartGap, params CardDef[] first) =>
            TurnLoop.BeginPlayerTurn(Started(enemy, cells, gap, first).State, NoShuffle).State;

        private static BattleState WithPlayer(BattleState s, params (StatusKind Kind, int Stacks)[] words) =>
            s with { Player = s.Player with { Statuses = StatusSet.Of(words) } };

        private static string InHand(BattleState s, string id) => s.Hand.First(c => c.Def.Id == id).InstanceId;

        private static BattleState WithTurnEnds(BattleState s, params PlayerTurnEnd[] turns) =>
            s with { PlayerHistory = new BattleHistory(PlayerTurns: turns.ToList()) };

        // ---- The rank and the roster ----

        [Test]
        public void TheKingRank_Follows_TheBoss()
        {
            Assert.That(System.Enum.GetValues(typeof(EnemyRank)).Cast<EnemyRank>(),
                Is.EqualTo(new[] { EnemyRank.Normal, EnemyRank.Elite, EnemyRank.Boss, EnemyRank.King }));
        }

        [Test]
        public void TheLineageBosses_AreTheRostersProposals_AndStayOutOfTheGamesList()
        {
            var tree = Enemies.RottedWorldTree;
            var core = Enemies.RottedWorldTreeCore;
            var king = Enemies.WraithKing;
            var hero = Enemies.UnreturnedHero;
            Assert.Multiple(() =>
            {
                Assert.That(Enemies.LineageBosses.Select(e => e.Id), Is.EqualTo(new[] { "rotted_world_tree", "wraith_king", "unreturned_hero" }));
                Assert.That(Enemies.All, Has.Count.EqualTo(13), "the game's thirteen are untouched");
                Assert.That(Enemies.All.Intersect(Enemies.LineageBosses), Is.Empty);
                foreach (var each in Enemies.LineageBosses) Assert.That(Enemies.ById(each.Id), Is.SameAs(each));

                // roster §13: trunk 170 + core 60, both kings acting twice; only the trunk is size 3.
                Assert.That((tree.Rank, tree.MaxHp, tree.MaxStamina, tree.Recovery, tree.Size, tree.Lineage, tree.RootCrawlGap),
                    Is.EqualTo((EnemyRank.King, 170, 14, 3, 3, EnemyLineage.Parasite, (int?)1)));
                Assert.That(tree.Parasite, Is.SameAs(core));
                Assert.That((core.Rank, core.MaxHp, core.Size, core.Lineage, core.RootCrawlGap, core.Parasite),
                    Is.EqualTo((EnemyRank.King, 60, 1, EnemyLineage.Parasite, (int?)0, (EnemyDef?)null)));

                // roster §14 / §15: the ghosts and their leap plans.
                Assert.That((king.Rank, king.MaxHp, king.Size, king.Lineage, king.DelaysOmen), Is.EqualTo((EnemyRank.King, 220, 2, EnemyLineage.Ghost, true)));
                Assert.That(king.LeapPlan, Is.EqualTo(new[] { 2, 0, 3, 0 }));
                Assert.That((hero.Rank, hero.MaxHp, hero.Size, hero.Lineage, hero.DelaysOmen), Is.EqualTo((EnemyRank.Boss, 170, 1, EnemyLineage.Ghost, true)));
                Assert.That(hero.LeapPlan, Is.EqualTo(new[] { 0, 2, 0, 3 }));

                foreach (var each in new[] { tree, core, king, hero }) Assert.That(each.ActionsPerPhase, Is.EqualTo(Constants.EliteActions), each.Id);
                Assert.That(Enemies.DistortionRoot.Lineage, Is.EqualTo(EnemyLineage.Parasite), "歪みの根 is the tree's root (roster §1.8)");
                Assert.That(Enemies.DistortionRoot.RootCrawlGap, Is.Null, "and its roots do not crawl (handed to main)");
                Assert.That(Enemies.All.Where(e => e.Id != "distortion_root").Select(e => e.Lineage).Distinct(), Is.EqualTo(new[] { EnemyLineage.Dragon }));
            });
        }

        private static IEnumerable<EnemyDef> LineageDefs() => Enemies.LineageBosses.Append(Enemies.RottedWorldTreeCore);

        [TestCaseSource(nameof(LineageDefs))]
        public void ALineageBoss_KeepsTheTreeRules(EnemyDef enemy)
        {
            // roster §1.2: every branch ends on column 1〜2, and 3+ holds a forward move or reaches 4.
            Assert.Multiple(() =>
            {
                foreach (var action in enemy.Actions.Values) Assert.That(Cards.ValidateEnemyAction(action), Is.Empty, action.Id);
                foreach (var band in new[] { GapBand.Zero, GapBand.OneToTwo, GapBand.ThreePlus })
                {
                    var branch = enemy.Branch(band);
                    Assert.That(enemy.Actions[branch[branch.Count - 1]].Column, Is.LessThanOrEqualTo(2), enemy.Id + " " + band);
                }
                Assert.That(enemy.Branch(GapBand.ThreePlus).Select(id => enemy.Actions[id]).Any(a => a.Face.Move > 0 || a.Face.ReachOrDefault.Max >= 4),
                    Is.True, enemy.Id + " 3+");
            });
        }

        [Test]
        public void TheWordsBarredWhileTheyRemain_AreTheRostersOneEveryOtherPhase()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Enemies.RottedWorldTree.Actions["sap_burst"].NotWhileFoeHas, Is.EqualTo(StatusKind.Festering));
                Assert.That(Enemies.RottedWorldTree.Actions["spore_rain"].NotWhileFoeHas, Is.EqualTo(StatusKind.Sprouting));
                Assert.That(Enemies.RottedWorldTreeCore.Actions["spore_puff"].NotWhileFoeHas, Is.EqualTo(StatusKind.Sprouting));
                Assert.That(Enemies.RottedWorldTreeCore.Actions["reroot"].NotWhileSelfHas, Is.EqualTo(StatusKind.Regen));
                Assert.That(Enemies.WraithKing.Actions["luring_voice"].NotWhileFoeHas, Is.EqualTo(StatusKind.Bewildered));
                Assert.That(Enemies.WraithKing.Actions["frost_touch"].NotWhileFoeHas, Is.EqualTo(StatusKind.Frost));
                Assert.That(Enemies.WraithKing.Actions["soul_torrent"].NotTwiceRunning, Is.True);
                Assert.That(Enemies.UnreturnedHero.Actions["stitch_blade"].NotWhileFoeHas, Is.EqualTo(StatusKind.Shadowstitch));
            });
        }

        // ---- The five words ----

        [TestCase(StatusKind.Sprouting, "sprouting", "芽吹き")]
        [TestCase(StatusKind.Festering, "festering", "爛れ")]
        [TestCase(StatusKind.Bewildered, "bewildered", "惑い")]
        [TestCase(StatusKind.Frost, "frost", "凍て")]
        [TestCase(StatusKind.Shadowstitch, "shadowstitch", "影縫い")]
        public void EachLineageBossWord_IsATurnWord_PutOnTheFoe_AndBossOnly(StatusKind kind, string token, string label)
        {
            Assert.Multiple(() =>
            {
                Assert.That(Statuses.DecayOf(kind), Is.EqualTo(StatusDecay.OnTurn));
                Assert.That(Statuses.IsOwn(kind), Is.False);
                Assert.That(Statuses.IsBossOnly(kind), Is.True, "竜語の符 lowers it");
                Assert.That(kind.ToToken(), Is.EqualTo(token));
                Assert.That(kind.ToLabel(), Is.EqualTo(label));
            });
        }

        [Test]
        public void Sprouting_CostsTwoHpACard_PastGuard()
        {
            var s = WithPlayer(Opened(Idle, 6), (StatusKind.Sprouting, 1));
            s = s with { Player = s.Player with { Guard = 10 } };
            var played = TurnLoop.PlayCard(s, s.Hand[0].InstanceId, NoShuffle);
            Assert.Multiple(() =>
            {
                Assert.That(played.State.Player.Hp, Is.EqualTo(s.Player.Hp - Statuses.SproutingHpPerCard));
                Assert.That(played.Events.OfType<StatusHpChanged>().Single(),
                    Is.EqualTo(new StatusHpChanged(Actor.Player, StatusKind.Sprouting, -2, s.Player.Hp - 2)));
            });
        }

        [Test]
        public void Festering_KeepsTheBleedStack_WhileItIsHeldAfterTheTick()
        {
            // roster §13.1: 2 stacks work through the next turn start only.
            var s = WithPlayer(Opened(Idle, 6), (StatusKind.Bleed, 2), (StatusKind.Festering, 2));
            var next = TurnLoop.BeginPlayerTurn(TurnLoop.EndTurn(s, NoShuffle).State, NoShuffle).State;
            var after = TurnLoop.BeginPlayerTurn(TurnLoop.EndTurn(next, NoShuffle).State, NoShuffle).State;
            Assert.Multiple(() =>
            {
                Assert.That(next.Player.Statuses.Stacks(StatusKind.Bleed), Is.EqualTo(2), "爛れ 1 is left: the bleed keeps its stack");
                Assert.That(next.Player.Statuses.Stacks(StatusKind.Festering), Is.EqualTo(1));
                Assert.That(next.Player.Hp, Is.EqualTo(s.Player.Hp - 4));
                Assert.That(after.Player.Statuses.Stacks(StatusKind.Bleed), Is.EqualTo(1), "爛れ is gone: the bleed wears off again");
                Assert.That(after.Player.Statuses.Has(StatusKind.Festering), Is.False);
            });
        }

        [Test]
        public void Frost_KeepsTheStanceCardsOutOfPlay()
        {
            var s = Opened(Idle, 6, Constants.StartGap, Fixtures.TurnGuardStance);
            string stance = InHand(s, "turn_guard_stance");
            Assert.Multiple(() =>
            {
                Assert.That(TurnLoop.CanPlay(s, stance), Is.EqualTo(PlayRefusal.None));
                Assert.That(TurnLoop.CanPlay(WithPlayer(s, (StatusKind.Frost, 1)), stance), Is.EqualTo(PlayRefusal.Frozen));
                Assert.That(TurnLoop.CanPlay(WithPlayer(s, (StatusKind.Frost, 1)), InHand(s, "filler")), Is.EqualTo(PlayRefusal.None));
            });
        }

        [Test]
        public void Shadowstitch_CostsThreeHpAMove_OnACardAndOnTheFreeStep()
        {
            var s = WithPlayer(Opened(Idle, 6, Constants.StartGap, CardCatalog.Footwork), (StatusKind.Shadowstitch, 1));
            var walked = TurnLoop.PlayCard(s, InHand(s, "footwork"), NoShuffle);

            var swift = s with { Player = s.Player with { FreeStep = true } };
            var stepped = TurnLoop.TakeFreeStep(swift, -1);

            var low = swift with { Player = swift.Player with { Hp = 2 } };
            var fell = TurnLoop.TakeFreeStep(low, -1);
            Assert.Multiple(() =>
            {
                Assert.That(walked.State.Player.Cell, Is.EqualTo(s.Player.Cell + 1));
                Assert.That(walked.State.Player.Hp, Is.EqualTo(s.Player.Hp - Statuses.ShadowstitchHpPerMove));
                Assert.That(stepped.State.Player.Hp, Is.EqualTo(s.Player.Hp - 3));
                Assert.That(fell.State.Result, Is.EqualTo(GameResult.Lost), "the step itself can take the last HP");
            });
        }

        [Test]
        public void Bewildered_HidesTheAimedCells_AndNothingElse()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Statuses.SeesAimedCells(StatusSet.Empty), Is.True);
                Assert.That(Statuses.SeesAimedCells(StatusSet.Of((StatusKind.Bewildered, 1))), Is.False);
            });
        }

        // ---- 寄生: 宿主と寄生体 ----

        [Test]
        public void TheHostFalling_LetsTheParasiteOut_AfterTheCard_AtFullHp()
        {
            // 8 cells, gap 3: the trunk uses 6〜8, the core comes out on 6.
            var twoBlows = Fixtures.Card("two_blows", face: new Face(Power: 20, Reach: new Reach(0, 4), Hits: 2));
            var s = Opened(Enemies.RottedWorldTree, 8, Constants.StartGap, twoBlows, twoBlows);
            s = s.WithEnemy(0, s.Enemy with { Hp = 5, Statuses = StatusSet.Of((StatusKind.Regen, 2)), Guard = 0 });
            var hit = TurnLoop.PlayCard(s, InHand(s, "two_blows"), NoShuffle);
            var core = hit.State.Enemies[0];
            Assert.Multiple(() =>
            {
                Assert.That(hit.State.Result, Is.EqualTo(GameResult.Ongoing));
                Assert.That(hit.Events.OfType<HostFell>().Single().HostId, Is.EqualTo("rotted_world_tree"));
                Assert.That(hit.Events.OfType<ParasiteEmerged>().Single(),
                    Is.EqualTo(new ParasiteEmerged(Actor.Enemy, "rotted_world_tree", "rotted_world_tree_core", 6, 60, 60)));
                Assert.That(hit.Events.OfType<EnemyDefeated>(), Is.Empty);
                Assert.That(hit.Events.OfType<DamageDealt>().Count(), Is.EqualTo(1), "the second blow does not land on the core");
                Assert.That(core.Def, Is.SameAs(Enemies.RottedWorldTreeCore));
                Assert.That(core.Host, Is.SameAs(Enemies.RottedWorldTree));
                Assert.That((core.Body.Hp, core.Body.Cell, core.Body.Size, core.Body.Guard), Is.EqualTo((60, 6, 1, 0)));
                Assert.That(core.Body.Statuses, Is.EqualTo(StatusSet.Empty), "the host's words stay behind");
                Assert.That(core.Omen, Is.Not.Null, "the core shows its omen at once");
                Assert.That(hit.Events.OfType<OmenSet>().Single().Decided, Is.True);
            });

            var last = hit.State.WithEnemy(0, hit.State.Enemy with { Hp = 5 });
            var won = TurnLoop.PlayCard(last, InHand(last, "two_blows"), NoShuffle);
            Assert.That(won.State.Result, Is.EqualTo(GameResult.Won), "the core falls for good");
        }

        [Test]
        public void AHostFallingToBleed_AtItsTurnStart_LetsTheParasiteOut_WhichWaitsForTheNextPhase()
        {
            var s = Opened(Enemies.RottedWorldTree, 8);
            s = s.WithEnemy(0, s.Enemy with { Hp = 2, Statuses = StatusSet.Of((StatusKind.Bleed, 1)) });
            var end = TurnLoop.EndTurn(s, NoShuffle);
            var events = end.Events.ToList();
            int emerged = events.FindIndex(e => e is ParasiteEmerged);
            Assert.Multiple(() =>
            {
                Assert.That(end.State.Result, Is.EqualTo(GameResult.Ongoing));
                Assert.That(emerged, Is.GreaterThan(events.FindIndex(e => e is HostFell)));
                Assert.That(events.Skip(emerged).OfType<ActionExecuted>(), Is.Empty, "it acts from the next phase");
                Assert.That(end.State.Enemies[0].Def.Id, Is.EqualTo("rotted_world_tree_core"));
            });
        }

        // ---- 寄生: 根の這い ----

        private static int RecoveredAt(EnemyDef enemy, int gap, params (StatusKind Kind, int Stacks)[] words)
        {
            var s = Started(enemy, 8, gap).State;
            s = s with { Player = s.Player with { Stamina = 0, Statuses = StatusSet.Of(words) } };
            return TurnLoop.BeginPlayerTurn(s, NoShuffle).Events.OfType<StaminaRecovered>().Single().Amount;
        }

        [Test]
        public void RootCrawl_TakesOneOffTheRecovery_WithinTheCrawlGap_NeverBelowOne()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RecoveredAt(Enemies.RottedWorldTree, 1), Is.EqualTo(2), "the trunk crawls at gap 1");
                Assert.That(RecoveredAt(Enemies.RottedWorldTree, 2), Is.EqualTo(3));
                Assert.That(RecoveredAt(Enemies.RottedWorldTree, 1, (StatusKind.Fatigue, 2), (StatusKind.Withering, 1)), Is.EqualTo(1),
                    "疲労 and 枯らし have left 1: the crawl takes nothing more");
                Assert.That(RecoveredAt(Enemies.DistortionRoot, 0), Is.EqualTo(3), "歪みの根 does not crawl");
            });

            var s = Started(Enemies.RottedWorldTree, 8, 1).State;
            var begin = TurnLoop.BeginPlayerTurn(s with { Player = s.Player with { Stamina = 0 } }, NoShuffle);
            Assert.That(begin.Events.OfType<RootCrawled>().Single(), Is.EqualTo(new RootCrawled(Actor.Player, 1)));
        }

        [Test]
        public void TheBarkShell_GuardsAtTheTurnStart_WhileThePlayerBleeds()
        {
            var shell = Enemies.RottedWorldTree.Actions["bark_shell"].Face.Stance!;
            var s = Opened(Idle, 6);
            s = s.WithEnemy(0, s.Enemy.WithStance(shell, "bark_shell"));
            var bleeding = TurnLoop.EndTurn(WithPlayer(s, (StatusKind.Bleed, 2)), NoShuffle);
            var dry = TurnLoop.EndTurn(s, NoShuffle);
            Assert.Multiple(() =>
            {
                Assert.That(bleeding.Events.OfType<StanceFired>().Count(f => f.SourceId == "bark_shell"), Is.EqualTo(1));
                Assert.That(dry.Events.OfType<StanceFired>().Count(f => f.SourceId == "bark_shell"), Is.Zero);
            });
        }

        // ---- 亡霊: 押し引きを受けない ----

        [Test]
        public void AGhostOfSizeOne_RefusesPushAndPull_AndTakesNoWallDamage()
        {
            var shove = Fixtures.Card("shove", face: new Face(Power: 1, Push: 2, Reach: new Reach(0, 3)));
            var s = Opened(Enemies.UnreturnedHero, 6, Constants.StartGap, shove);
            var pushed = TurnLoop.PlayCard(s, InHand(s, "shove"), NoShuffle);
            Assert.Multiple(() =>
            {
                Assert.That(pushed.Events.OfType<PushRefused>().Single().ByLineage, Is.True);
                Assert.That(pushed.Events.OfType<WallHit>(), Is.Empty);
                Assert.That(pushed.State.Enemy.Cell, Is.EqualTo(s.Enemy.Cell));
            });
        }

        // ---- 亡霊: 跳ぶ と 予兆が 1 フェーズ遅れる ----

        [Test]
        public void AGhost_ShowsTwoPhasesOfOmens_AndCarriesOutTheEarlierOne_EachPhase()
        {
            // 6 cells, gap 3: the hero starts on 6. Phase 1 is read at gap 3, phase 2 at the gap 0 its
            // first leap (plan 0 → 2 → 0 → 3) will put it at.
            var start = Started(Enemies.UnreturnedHero, 6);
            var later = start.Events.OfType<LaterOmenSet>().Single();
            var first = start.State.Enemies[0].Omen!;
            Assert.Multiple(() =>
            {
                Assert.That(first.ActionId, Is.EqualTo("stitch_blade"), "3+");
                Assert.That(later.Omen.ActionId, Is.EqualTo("hero_cleave"), "0, after the first leap");
                Assert.That(later.Decided, Is.True);
                Assert.That(Started(Enemies.PolearmWarped, 6).Events.OfType<LaterOmenSet>(), Is.Empty, "a dragon acts on time");
            });

            var opened = TurnLoop.BeginPlayerTurn(start.State, NoShuffle);
            Assert.That(opened.Events.OfType<LaterOmenSet>().Single().Decided, Is.False, "step 5 shows it again");

            var phase1 = TurnLoop.EndTurn(opened.State, NoShuffle);
            var leap = phase1.Events.OfType<CellsMoved>().Single(m => m.Actor == Actor.Enemy);
            Assert.Multiple(() =>
            {
                Assert.That(phase1.Events.OfType<ActionExecuted>().First().Action.Id, Is.EqualTo("stitch_blade"));
                Assert.That((leap.From, leap.To, leap.Leap, leap.Pushed), Is.EqualTo((6, 3, true, false)));
                Assert.That(phase1.State.Enemies[0].Omen!.ActionId, Is.EqualTo("hero_cleave"), "the omen shown a phase ago is up next");
                Assert.That(phase1.State.Enemies[0].Later, Is.Not.Null);
                Assert.That(phase1.State.Enemies[0].LeapIndex, Is.EqualTo(1));
            });

            var phase2 = TurnLoop.EndTurn(TurnLoop.BeginPlayerTurn(phase1.State, NoShuffle).State, NoShuffle);
            Assert.Multiple(() =>
            {
                Assert.That(phase2.Events.OfType<ActionExecuted>().First().Action.Id, Is.EqualTo("hero_cleave"));
                Assert.That(phase2.State.Enemy.Cell, Is.EqualTo(5), "gap 2 next");
            });
        }

        [Test]
        public void ALeap_PassesOverItsOwnCell_AndNeitherSlowNorBindingStopsIt()
        {
            // The hero stands at gap 0 already, so the plan's 0 is its own cell: it takes the 2 after it.
            var s = Opened(Enemies.UnreturnedHero, 6);
            s = s.WithEnemy(0, s.Enemy with { Cell = 3, Statuses = StatusSet.Of((StatusKind.Slow, 4), (StatusKind.Binding, 4)) });
            var end = TurnLoop.EndTurn(s, NoShuffle);
            Assert.Multiple(() =>
            {
                Assert.That(end.State.Enemy.Cell, Is.EqualTo(5));
                Assert.That(end.State.Enemies[0].LeapIndex, Is.EqualTo(2));
                Assert.That(end.Events.OfType<MoveBlocked>().Where(b => b.Actor == Actor.Enemy), Is.Empty);
            });
        }

        [Test]
        public void Pursuit_SendsTheNextLeapNextToAPlayerWhoStayedAway_AndThePlanWaits()
        {
            // roster §15.4: two turn ends at 3 or more. The plan's next is 2 (index 1); pursuit sends it to 0.
            var s = WithTurnEnds(Opened(Enemies.UnreturnedHero, 6), new PlayerTurnEnd(0, 3, false, Cell: 2));
            s = s.WithUnit(0, s.Enemies[0] with { LeapIndex = 1 });
            var end = TurnLoop.EndTurn(s, NoShuffle);
            Assert.Multiple(() =>
            {
                Assert.That(end.State.Enemy.Cell, Is.EqualTo(3));
                Assert.That(end.State.Enemies[0].LeapIndex, Is.EqualTo(1), "the plan picks up where it was");
            });
        }

        [Test]
        public void Anchor_SendsTheWraithKingNextToAPlayerWhoEndedTwoTurnsOnOneCell()
        {
            // 7 cells: the king uses 6〜7. Its plan starts on 2 (cell 5); anchor sends it to 0 (cell 3).
            // Its phase is held to 骨の垣 and 霊槍 so that 招きの声 does not pull the player first.
            var s = WithPlayer(Opened(Enemies.WraithKing, 7), (StatusKind.Bewildered, 1));
            s = s.WithUnit(0, s.Enemies[0] with { Omen = EnemyAi.OmenOf(Enemies.WraithKing.Actions["bone_ward"]), Plan = null });
            var plain = TurnLoop.EndTurn(s, NoShuffle);
            var anchored = TurnLoop.EndTurn(WithTurnEnds(s, new PlayerTurnEnd(0, 3, false, Cell: 2)), NoShuffle);
            Assert.Multiple(() =>
            {
                Assert.That(plain.State.Enemy.Cell, Is.EqualTo(5));
                Assert.That(anchored.State.Enemy.Cell, Is.EqualTo(3));
            });
        }

        [Test]
        public void TheHerosSecondStage_LeapsOnlyNextToOrOneAway()
        {
            // HP 85 or less: 0 → 1 → 0 → 1. At index 1 the base plan says 2 (cell 5), the stage 1 (cell 4).
            var s = Opened(Enemies.UnreturnedHero, 6);
            s = s.WithUnit(0, s.Enemies[0] with { LeapIndex = 1 });
            var wounded = s.WithEnemy(0, s.Enemy with { Hp = 80 });
            Assert.Multiple(() =>
            {
                Assert.That(TurnLoop.EndTurn(s, NoShuffle).State.Enemy.Cell, Is.EqualTo(5));
                Assert.That(TurnLoop.EndTurn(wounded, NoShuffle).State.Enemy.Cell, Is.EqualTo(4));
            });
        }

        // ---- Whole battles ----

        private static List<StepResult> Fight(EnemyDef enemy, int power, int turns = 40)
        {
            var card = Fixtures.Card("blow_" + power, face: new Face(Power: power, Reach: new Reach(0, 4)));
            var deck = Cards.BuildDeck(new[] { card }, copies: Constants.DeckMin);
            var steps = new List<StepResult> { TurnLoop.Start(new BattleSetup(enemy, deck, 8), NoShuffle) };
            var s = steps[0].State;
            for (int turn = 0; turn < turns && s.Result == GameResult.Ongoing; turn++)
            {
                steps.Add(TurnLoop.BeginPlayerTurn(s, NoShuffle));
                s = steps[steps.Count - 1].State;
                while (s.Result == GameResult.Ongoing)
                {
                    var playable = s.Hand.FirstOrDefault(c => TurnLoop.CanPlay(s, c.InstanceId, s.Nearest) == PlayRefusal.None);
                    if (playable == null) break;
                    steps.Add(TurnLoop.PlayCard(s, playable.InstanceId, NoShuffle, s.Nearest));
                    s = steps[steps.Count - 1].State;
                }
                if (s.Result != GameResult.Ongoing) break;
                steps.Add(TurnLoop.EndTurn(s, NoShuffle));
                s = steps[steps.Count - 1].State;
            }
            return steps;
        }

        [TestCaseSource(nameof(LineageDefs))]
        public void ALineageBoss_FightsAWholeBattle_ToItsEnd(EnemyDef enemy)
        {
            // A strong deck wins through every stage; a weak one lives through many phases of the tree.
            var strong = Fight(enemy, power: 30);
            var weak = Fight(enemy, power: 6);
            var events = strong.SelectMany(step => step.Events).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(strong[strong.Count - 1].State.Result, Is.EqualTo(GameResult.Won), enemy.Id);
                Assert.That(weak.Count, Is.GreaterThan(10), enemy.Id);
                Assert.That(events.OfType<ParasiteEmerged>().Count(), Is.EqualTo(enemy.Parasite == null ? 0 : 1), enemy.Id);
                if (enemy.DelaysOmen)
                {
                    Assert.That(weak.SelectMany(step => step.Events).OfType<CellsMoved>().Any(m => m.Leap), Is.True, enemy.Id + " leaps");
                }
            });
        }

        // ---- What the tree passes over ----

        [Test]
        public void NotTwiceRunning_PassesOverTheActionTakenInThePhaseBefore()
        {
            var king = Enemies.WraithKing;
            var torrent = king.Actions["soul_torrent"];
            Assert.Multiple(() =>
            {
                Assert.That(EnemyAi.RanBefore(king, new[] { "soul_torrent" }), Is.EqualTo(new[] { "soul_torrent" }));
                Assert.That(EnemyAi.RanBefore(king, new[] { "soul_reap" }), Is.Empty);
                Assert.That(EnemyAi.BarredLater(king, StatusSet.Empty, StatusSet.Empty, new[] { torrent }), Does.Contain("soul_torrent"));
                Assert.That(EnemyAi.BarredLater(king, StatusSet.Empty, StatusSet.Empty, new EnemyActionDef[0]), Does.Not.Contain("soul_torrent"));
            });
        }

        [Test]
        public void AGhostsLaterOmen_CountsTheWordItsNextPhasePutsOn_AsRemaining()
        {
            // 凍ての手 in the next phase puts 凍て 2 on: one stack is left two phases on, so not again.
            var king = Enemies.WraithKing;
            var frost = king.Actions["frost_touch"];
            Assert.Multiple(() =>
            {
                Assert.That(EnemyAi.BarredLater(king, StatusSet.Empty, StatusSet.Empty, new[] { frost }), Does.Contain("frost_touch"));
                Assert.That(EnemyAi.BarredLater(king, StatusSet.Of((StatusKind.Frost, 2)), StatusSet.Empty, new EnemyActionDef[0]),
                    Does.Not.Contain("frost_touch"), "2 stacks now are gone after two turn starts");
                Assert.That(EnemyAi.BarredLater(king, StatusSet.Of((StatusKind.Frost, 3)), StatusSet.Empty, new EnemyActionDef[0]),
                    Does.Contain("frost_touch"));
            });
        }

        [Test]
        public void Reroot_WaitsOutItsOwnRegen()
        {
            var core = Enemies.RottedWorldTreeCore;
            Assert.Multiple(() =>
            {
                Assert.That(EnemyAi.Barred(core, StatusSet.Empty, beforeFoeTurn: true, self: StatusSet.Of((StatusKind.Regen, 2))), Does.Contain("reroot"));
                Assert.That(EnemyAi.Barred(core, StatusSet.Empty, beforeFoeTurn: true, self: StatusSet.Of((StatusKind.Regen, 1))), Does.Not.Contain("reroot"));
            });
        }
    }
}
