// Port of the JS extension's test/depiction/MultiEnemy.test.mjs and _singleEnemyRuns.mjs
// (docs/port-notes/depiction-multi-enemy.md): battles of two and three enemies through
// CoreBattleSource, and the one-enemy battles the multi-enemy writer must leave alone.
//
// 1. One-enemy battles: every member the C# wrote before the extension comes out exactly as before
//    (MultiEnemy.single.golden.txt, written from the C# before it by SingleEnemyRuns below), and the
//    members added are exactly the documented ones. Battle.unity plays these: one enemy must not change.
// 2. Two and three enemies (the roster pair and every line the roster can make) play to the end
//    unattended, the frame follows the core enemy by enemy, and a card goes to the enemy it is
//    released on.
//
// The JS hashes do not carry over (another serialisation), so the C# golden is its own: the
// scenarios follow the JS's, the writer runs use a small chooser of their own (BattleCore.Sim is not
// in the Unity build), and a free-step deck pins what the writer does with a step it is fed.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class MultiEnemyTests
    {
        /// <summary>
        /// Every member added since the one-enemy snapshot, as Type.Member: #589's DepictionFrame.Enemies
        /// (EnemyFrame, the per-enemy share) and Cue.Unit, and the extension's (depiction-multi-enemy.md).
        /// </summary>
        private static readonly string[] NewMembers =
        {
            "CardFace.CostOn", "CardFace.ReachOn", "CardFace.StanceHintAfterPayOn", "CardFace.TargetUnit",
            "Cue.Unit", "Cue.UnitCellAfter", "Cue.UnitGapAfter",
            "DepictionEvent.Unit",
            "DepictionFrame.Enemies",
        };

        // ======================================================================= 1. one enemy, as before

        /// <summary>
        /// js: test/depiction/MultiEnemy.test.mjs:71. Regenerate the golden (from the code before a
        /// deliberate change to the one-enemy depiction or the battle rules, then apply the change and
        /// check the diff is the intended one): set DEPICTION_SINGLE_GOLDEN=write and run this test.
        /// DEPICTION_SINGLE_GOLDEN_DUMP=&lt;folder&gt; also writes every record as text, for a diff.
        /// </summary>
        [Test]
        public void OneEnemyBattles_WriteEveryCSharpMember_AsBeforeTheExtension()
        {
            string path = SingleEnemyRuns.GoldenPath();
            string dump = Environment.GetEnvironmentVariable(SingleEnemyRuns.DumpSwitch);
            List<SingleEnemyRuns.Spec> specs = SingleEnemyRuns.Scenarios();
            if (Environment.GetEnvironmentVariable(SingleEnemyRuns.WriteSwitch) == "write")
            {
                SingleEnemyRuns.WriteGolden(path, specs, dump);
                Assert.Pass("wrote " + path);
            }
            if (!File.Exists(path))
            {
#if UNITY_5_3_OR_NEWER
                Assert.Ignore("the one-enemy golden is read under dotnet test (" + path + " is not in this project)");
#else
                Assert.Fail("the one-enemy golden is missing: " + path);
#endif
            }

            SingleEnemyRuns.Golden golden = SingleEnemyRuns.Golden.Read(path);
            Assert.That(specs.Select(s => s.Id).ToList(), Is.EqualTo(golden.Scenarios.Select(s => s.Id).ToList()),
                "the same scenarios as the snapshot");
            var canon = new SingleEnemyRuns.Canon(golden.Keys);
            for (int s = 0; s < specs.Count; s++)
            {
                SingleEnemyRuns.Spec spec = specs[s];
                List<string> records = SingleEnemyRuns.Run(spec, canon);
                if (!string.IsNullOrEmpty(dump)) SingleEnemyRuns.Dump(dump, s, spec, records);
                List<string> got = records.Select(SingleEnemyRuns.Hash).ToList();
                string[] expected = golden.Scenarios[s].Hashes;
                int first = -1;
                for (int i = 0; i < Math.Min(got.Count, expected.Length); i++)
                {
                    if (got[i] != expected[i])
                    {
                        first = i;
                        break;
                    }
                }
                Assert.That(first < 0 && got.Count == expected.Length, Is.True,
                    spec.Id + ": record " + (first < 0 ? got.Count : first) + " differs from the one-enemy snapshot (" + got.Count
                    + " records, snapshot " + expected.Length + ")");
            }
            Assert.That(canon.Added.ToList(), Is.EquivalentTo(NewMembers), "the members added are the documented ones");
        }

        // js: test/depiction/MultiEnemy.test.mjs:92
        [Test]
        public void OneEnemyBattles_WriteUnitZero_TheirOneSwatchlessEnemy()
        {
            CoreBattleSource source = new BattleLaunch { Seed = 3, AutoPlay = true }.CreateSource();
            Fight ctx = FightToTheEnd(source, "slice|3");
            Assert.That(ctx.N, Is.EqualTo(1));
            DepictionFrame frame = source.Frame;
            Assert.That(frame.Enemies.Count, Is.EqualTo(1));
            Assert.That(frame.Enemies[0].Swatch, Is.False);
            Assert.That(frame.Enemies[0].StripeDirection, Is.EqualTo(0));
            Assert.That(frame.Enemy, Is.SameAs(frame.Enemies[0].Unit), "Frame.Enemy is the one enemy");
            Assert.That(frame.Omen, Is.SameAs(frame.Enemies[0].Omen));
        }

        // ======================================================================= 2. two and three enemies

        private static readonly string[] Pair = { "polearm_warped", "crossbow_hunter" };
        private const int PairCells = 7; // roster §9 / battle_core_v4 §7.4: 組の場は 7 マス
        private const int MaxMoves = 6000;

        /// <summary>MULTI_ENEMY_SEEDS=40 runs every line × seeds 1..40 (slow); 2 by default, as the JS.</summary>
        private static int SweepSeeds
        {
            get
            {
                int seeds;
                string raw = Environment.GetEnvironmentVariable("MULTI_ENEMY_SEEDS");
                return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out seeds) && seeds > 0 ? seeds : 2;
            }
        }

        // js: test/depiction/MultiEnemy.test.mjs:106
        [Test]
        public void TheRosterPair_SeedsOneToForty_PlaysToTheEndUnattended_FrameFollowsTheCore()
        {
            int won = 0;
            int lost = 0;
            int defeats = 0;
            int hitBack = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                CoreBattleSource source = Launch(Pair, seed, PairCells);
                Fight ctx = FightToTheEnd(source, "pair|" + seed);
                if (source.State.Result == GameResult.Won) won++;
                else lost++;
                defeats += ctx.Defeats.Count;
                hitBack += ctx.HitBy.Contains(1) ? 1 : 0;
            }
            Assert.That(won + lost, Is.EqualTo(40));
            Assert.That(won, Is.GreaterThan(0), "some seeds are won: both enemies fall");
            Assert.That(defeats, Is.GreaterThan(won * 2), "some battles go on after one enemy has fallen");
            Assert.That(hitBack, Is.GreaterThan(0), "the enemy behind is struck in some battles");
        }

        // js: test/depiction/MultiEnemy.test.mjs:121
        [Test]
        public void TheRosterPair_StartsAsTheRosterSays_TwoFramesTwoOmensTwoHatches()
        {
            CoreBattleSource source = Launch(Pair, 1, PairCells);
            DepictionFrame opening = source.Frame;
            Assert.That(opening.Enemies.Count, Is.EqualTo(2));
            Assert.That(opening.Enemies.All(e => e.Swatch), Is.True);
            Assert.That(opening.Enemies.Select((e, k) => k + "," + e.Unit.ArtId + "," + e.Cell + "," + e.Gap + "," + e.StripeDirection + "," + e.Unit.Down).ToList(),
                Is.EqualTo(new[] { "0,polearm_warped,6,3,0,False", "1,crossbow_hunter,7,4,1,False" }), "battle_core_v4 §7.4: 開始の間合いは 3 と 4");
            Assert.That(opening.Enemy, Is.SameAs(opening.Enemies[0].Unit), "the one in front is the frame's enemy");
            Assert.That(opening.Enemies.All(e => !e.Omen.Visible), Is.True, "the first omens wait for turn 1");
            DepictionEvent turn = source.AdvanceAuto();
            Assert.That(turn.Kind, Is.EqualTo(DepictionEventKind.TurnStart));
            Assert.That(turn.Cues.Where(c => c.Kind == CueKind.OmenShow).Select(c => c.Unit).ToList(), Is.EqualTo(new[] { 0, 1 }),
                "step 5 shows both omens");
            List<OmenFrame> omens = turn.After.Enemies.Select(e => e.Omen).ToList();
            Assert.That(omens[0].Visible && omens[1].Visible, Is.True);
            // The bug the one-enemy writer had: the crossbow's omen read against the polearm's actions. Here each is its own.
            BattleState state = source.State;
            foreach (int k in new[] { 0, 1 })
            {
                OmenFrame own = CoreText.OmenOf(state.Enemies[k].Omen, state.Enemies[k].Def);
                Assert.That(omens[k].KindLabel, Is.EqualTo(own.KindLabel));
                Assert.That(omens[k].Icon, Is.EqualTo(own.Icon));
            }
            Assert.That(turn.After.Enemies[1].AimCells.Count > 0 || omens[1].SideGlyph == "", Is.True, "the crossbow aims at its own cells");
        }

        // js: test/depiction/MultiEnemy.test.mjs:148
        [Test]
        public void EveryTwoAndThreeEnemyLine_TheRosterCanMake_PlaysToTheEnd()
        {
            List<string> ids = Enemies.All.Select(d => d.Id).ToList();
            var lines = new List<string[]>();
            foreach (string a in ids)
            {
                foreach (string b in ids) lines.Add(new[] { a, b });
            }
            foreach (string a in ids)
            {
                foreach (string b in ids)
                {
                    foreach (string c in ids) lines.Add(new[] { a, b, c });
                }
            }
            int fitted = 0;
            int threes = 0;
            int ended = 0;
            int seeds = SweepSeeds;
            foreach (string[] line in lines)
            {
                int cells = FieldFor(line);
                if (cells < 0) continue;
                fitted += 1;
                if (line.Length == 3) threes += 1;
                for (int seed = 1; seed <= seeds; seed++)
                {
                    CoreBattleSource source = Launch(line, seed, cells);
                    FightToTheEnd(source, string.Join("+", line) + "@" + cells + "|" + seed);
                    if (source.State.Result != GameResult.Ongoing) ended++;
                }
            }
            // 13 × 13 ordered pairs less the 16 of two large enemies; the nine one-cell enemies in threes.
            Assert.That(fitted, Is.EqualTo(13 * 13 - 16 + 9 * 9 * 9));
            Assert.That(threes, Is.EqualTo(9 * 9 * 9));
            Assert.That(ended, Is.EqualTo(fitted * seeds));
        }

        // js: test/depiction/MultiEnemy.test.mjs:173
        [Test]
        public void ACardForTheEnemyBehind_GoesToIt_WhenTheOneInFrontIsOutOfReach()
        {
            // A card goes to an enemy inside its reach whenever there is one, and a named target is refused
            // exactly as the core refuses it.
            CoreBattleSource source = Launch(Pair, 7, PairCells);
            while (!source.WaitingForPlayer) source.AdvanceAuto();
            BattleState state = source.State;
            int checkedCount = 0;
            foreach (CardInstance card in state.Hand)
            {
                CardDef def = card.Def;
                if (!AimsAtOne(def)) continue;
                int target = source.TargetFor(card.InstanceId);
                List<int> inReach = state.Living.Where(k => def.Face.ReachOrDefault.Contains(state.GapTo(k))).ToList();
                if (inReach.Count > 0) Assert.That(inReach, Does.Contain(target), card.InstanceId + " goes to an enemy in its reach");
                foreach (int k in state.Living)
                {
                    PlayRefusal expected = TurnLoop.CanPlay(state, card.InstanceId, k);
                    PlayVerdict verdict = source.Inspect(card.InstanceId, k);
                    if (expected == PlayRefusal.None) Assert.That(verdict, Is.EqualTo(PlayVerdict.Accepted));
                    if (expected == PlayRefusal.OutOfReach) Assert.That(verdict, Is.EqualTo(PlayVerdict.OutOfRange));
                    checkedCount += 1;
                }
            }
            Assert.That(checkedCount, Is.GreaterThan(0));
        }

        // js: test/depiction/MultiEnemy.test.mjs:197
        [Test]
        public void ANamedTarget_IsTheOneTheCardHits_EvenWhereTheDefaultWouldGoElsewhere()
        {
            // The pair, unattended — except that while the enemy in front stands, a card that can be released
            // on the one behind is released there by name.
            int played = 0;
            int overridden = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                CoreBattleSource source = Launch(Pair, seed, PairCells);
                int guard = 0;
                while (!source.Finished && guard++ < 2000)
                {
                    if (!source.WaitingForPlayer)
                    {
                        source.AdvanceAuto();
                        continue;
                    }
                    BattleState state = source.State;
                    CardFace behind = state.Enemies[0].Alive
                        ? source.Frame.Hand.FirstOrDefault(f => f.Aim == CardAim.Single && source.Inspect(f.Id, 1) == PlayVerdict.Accepted)
                        : null;
                    if (behind != null)
                    {
                        int hpBefore = state.Enemies[1].Body.Hp;
                        PlayPreview preview = TurnLoop.Preview(state, behind.Id, 1);
                        Assert.That(source.PreviewFor(behind.Id, 1),
                            Is.EqualTo(preview.Attacks ? preview.RawPower.ToString() : preview.GuardGain > 0 ? preview.GuardGain.ToString() : ""));
                        if (source.TargetFor(behind.Id) != 1) overridden += 1;
                        PlayVerdict verdict = source.TryPlay(behind.Id, DepictionText.RequiredZone(behind.Aim), 1, out DepictionEvent ev);
                        Assert.That(verdict, Is.EqualTo(PlayVerdict.Accepted));
                        Assert.That(ev.Unit, Is.EqualTo(1), "the PlayCard event names the enemy behind");
                        if (preview.Attacks && preview.Damage > 0)
                        {
                            Assert.That(source.State.Enemies[1].Body.Hp, Is.LessThan(hpBefore), "the enemy behind lost HP");
                            Assert.That(ev.Cues.Any(c => c.Kind == CueKind.Slash && c.Unit == 1), Is.True, "its slash is on unit 1");
                            Assert.That(ev.PreviewText, Is.EqualTo(preview.RawPower.ToString()), "the played card shows the number read on unit 1");
                        }
                        played += 1;
                        continue;
                    }
                    string id = source.SuggestedCardId;
                    if (id.Length > 0) source.TryPlay(id, DepictionText.RequiredZone(DepictionText.Find(source.Frame.Hand, id).Aim), out DepictionEvent _);
                    else source.EndTurn();
                }
            }
            Assert.That(played, Is.GreaterThan(0), "some seed lets a card reach the enemy behind while the one in front stands");
            Assert.That(overridden, Is.GreaterThan(0), "and the default target was the one in front");
        }

        // js: test/depiction/MultiEnemy.test.mjs:239
        [Test]
        public void TheRosterPair_WithStoneThrows_SeedsOneToForty_ReadsEachCostOnItsEnemy()
        {
            // The prototype deck with 盾打ち swapped for 石礫 ×2 (a legal 20-card deck): 石礫's cost depends on
            // the enemy it is aimed at, so the per-enemy cost and the cheapest-first default target are met
            // in played battles too (CheckWait).
            List<CardDef> kinds = PrototypeDeck.Kinds.Select(d => d.Id == "shield_bash" ? CardCatalog.StoneThrow : d).ToList();
            var deck = new List<CardInstance>();
            foreach (CardDef def in kinds)
            {
                for (int c = 0; c < 2; c++) deck.Add(new CardInstance(def.Id + "-" + c, def));
            }
            int costSplit = 0;
            int notNearest = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                CoreBattleSource source = new BattleLaunch
                {
                    EnemyId = Pair[0], MoreEnemyIds = Pair.Skip(1).ToList(), Seed = seed, FieldCells = PairCells, AutoPlay = true, Deck = deck,
                }.CreateSource();
                Fight ctx = FightToTheEnd(source, "pair+stone|" + seed);
                costSplit += ctx.CostSplit;
                notNearest += ctx.NotNearest;
            }
            Assert.That(costSplit, Is.GreaterThan(0), "some waits hold a card that costs differently on the two enemies");
            Assert.That(notNearest, Is.GreaterThan(0), "and some send it past the nearest enemy to the cheaper one");
        }

        // js: test/depiction/MultiEnemy.test.mjs:258
        [Test]
        public void OutOfReach_IsExplained_OnTheEnemyTheCardWasReleasedOn_AlsoWhileAnotherIsInReach()
        {
            // battle-visual-v1 §5.4 出せなかった理由: a card released on an enemy it cannot reach goes back to
            // the hand with its reason, the N of that enemy — also when another enemy is inside its reach.
            // §5.1's "どの敵にも届かないときだけ" is the hover panel's rule (CardFace.ReachHint), which stays quiet then.
            int beside = 0;
            int alone = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                CoreBattleSource source = Launch(Pair, seed, PairCells);
                int guard = 0;
                while (!source.Finished && guard++ < 300)
                {
                    if (source.WaitingForPlayer)
                    {
                        BattleState state = source.State;
                        foreach (CardFace face in source.Frame.Hand)
                        {
                            if (face.Aim != CardAim.Single) continue;
                            bool nobody = CoreText.ReachesNobody(state, face.Id);
                            Assert.That(face.ReachHint != "", Is.EqualTo(nobody), face.Id + ": the §5.1 panel only when no enemy is in reach");
                            foreach (int k in state.Living)
                            {
                                if (source.Inspect(face.Id, k) != PlayVerdict.OutOfRange) continue;
                                PlayVerdict verdict = source.TryPlay(face.Id, DepictionText.RequiredZone(face.Aim), k, out DepictionEvent played);
                                Assert.That(verdict, Is.EqualTo(PlayVerdict.OutOfRange), face.Id + " on " + k + ": refused for reach");
                                Assert.That(played, Is.Null);
                                string text = source.RefusalText(face.Id, verdict, k);
                                Assert.That(text,
                                    Is.EqualTo("「" + face.Name + "」は間合い " + face.RequiredRangeGlyph + " の相手にしか出せません。いまは " + state.GapTo(k) + " です"),
                                    face.Id + " on " + k + ": the line names the N of the enemy the card was released on");
                                if (nobody) alone += 1;
                                else beside += 1;
                            }
                            if (source.Inspect(face.Id) == PlayVerdict.OutOfRange)
                            {
                                Assert.That(nobody, Is.True, "the default target is refused for reach only when nobody is in reach");
                                Assert.That(source.RefusalText(face.Id, PlayVerdict.OutOfRange), Does.EndWith("いまは " + state.GapTo(face.TargetUnit) + " です"),
                                    "unnamed: the N of TargetUnit");
                            }
                        }
                        string id = source.SuggestedCardId;
                        if (id.Length > 0) source.TryPlay(id, DepictionText.RequiredZone(DepictionText.Find(source.Frame.Hand, id).Aim), out DepictionEvent _);
                        else source.EndTurn();
                    }
                    else
                    {
                        source.AdvanceAuto();
                    }
                }
            }
            Assert.That(beside > 0 && alone > 0, Is.True, "both cases met (another enemy in reach " + beside + ", none " + alone + ")");
        }

        // js: test/depiction/MultiEnemy.test.mjs:301
        [Test]
        public void ACardWhoseCostDependsOnTheEnemy_ShowsEachEnemysCost_AndIsRefusedWithIt()
        {
            // 石礫 costs 2, 1 less at N 2 or more (CardCatalog.Initial). The player on cell 4 of the pair's line:
            // N 1 to the polearm (cell 6), N 2 to the crossbow (cell 7), so it costs 2 on unit 0 and 1 on unit 1.
            var setup = new BattleSetup(Enemies.ById(Pair[0]), Cards.BuildDeck(new[] { CardCatalog.StoneThrow }, 10), PairCells,
                StartGap: 1, PlayerStartCell: 4, MoreEnemies: new[] { Enemies.ById(Pair[1]) }, PlayerStartStamina: 0);
            var source = new CoreBattleSource(setup, 1);
            while (!source.WaitingForPlayer) source.AdvanceAuto();
            Assert.That(new[] { source.State.GapTo(0), source.State.GapTo(1) }, Is.EqualTo(new[] { 1, 2 }));
            CardFace Stone() => source.Frame.Hand.First(f => f.Id.StartsWith("stone_throw", StringComparison.Ordinal));
            CardFace face = Stone();
            Assert.That(face.CostOn, Is.EqualTo(new[] { 2, 1 }), "the core's cost on each enemy");
            Assert.That(new[] { 0, 1 }.Select(k => TurnLoop.Preview(source.State, face.Id, k).Cost).ToList(), Is.EqualTo(new[] { 2, 1 }));
            // At rest the face reads its cheapest receiver, as its lamp reads any enemy (§6.3 決定 6): a lit
            // コスト −1 lamp over a discounted cost circle.
            Assert.That(face.TargetUnit, Is.EqualTo(1));
            Assert.That(face.Cost, Is.EqualTo(1));
            Assert.That(face.CostDrop, Is.EqualTo(1));
            Assert.That(face.TraitLit, Is.True);
            Assert.That(face.LitOn, Is.EqualTo(new[] { false, true }));
            // Held over each enemy: that enemy's cost, ▼, lamp and 構えの予告 (stamina 3 at the first wait).
            Assert.That(source.State.Player.Stamina, Is.EqualTo(3));
            Assert.That(new object[] { face.CostFor(0), face.CostDropFor(0), face.LitFor(0), face.StanceHintAfterPayFor(0) }, Is.EqualTo(new object[] { 2, 0, false, "" }));
            Assert.That(new object[] { face.CostFor(1), face.CostDropFor(1), face.LitFor(1), face.StanceHintAfterPayFor(1) }, Is.EqualTo(new object[] { 1, 1, true, "" }));
            Assert.That(face.StanceHintAfterPayOn, Is.EqualTo(new[] { "", "" }));
            Assert.That(face.StanceHintAfterPay, Is.EqualTo(face.StanceHintAfterPayFor(face.TargetUnit)));
            // Spend it down on the crossbow until it costs too much on the polearm and not on the crossbow.
            while (source.State.Player.Stamina > 1)
            {
                PlayVerdict verdict = source.TryPlay(Stone().Id, DropZone.Receiver, 1, out DepictionEvent played);
                Assert.That(verdict, Is.EqualTo(PlayVerdict.Accepted), "stamina " + source.State.Player.Stamina);
                Assert.That(played.Unit, Is.EqualTo(1));
                while (!source.WaitingForPlayer) source.AdvanceAuto();
            }
            Assert.That(source.State.Enemies[1].Alive, Is.True, "the crossbow still stands");
            face = Stone();
            Assert.That(source.Inspect(face.Id, 0), Is.EqualTo(PlayVerdict.NotEnoughStamina));
            Assert.That(TurnLoop.CanPlay(source.State, face.Id, 0), Is.EqualTo(PlayRefusal.NotEnoughStamina));
            Assert.That(source.TryPlay(face.Id, DropZone.Receiver, 0, out DepictionEvent _), Is.EqualTo(PlayVerdict.NotEnoughStamina));
            Assert.That(source.RefusalText(face.Id, PlayVerdict.NotEnoughStamina, 0), Is.EqualTo("「石礫」はスタミナ 2 が要ります。残りは 1 です"),
                "the cost on the enemy the card was released on");
            Assert.That(source.Inspect(face.Id, 1), Is.EqualTo(PlayVerdict.Accepted));
            Assert.That(source.Inspect(face.Id), Is.EqualTo(PlayVerdict.Accepted), "unnamed, it goes where it can be paid for");
            // The last stamina on the crossbow; then unnamed it is refused with the cost of its cheapest receiver.
            Assert.That(source.TryPlay(face.Id, DropZone.Receiver, 1, out DepictionEvent _), Is.EqualTo(PlayVerdict.Accepted));
            while (!source.WaitingForPlayer) source.AdvanceAuto();
            face = Stone();
            Assert.That(source.State.Player.Stamina, Is.EqualTo(0));
            Assert.That(face.TargetUnit, Is.EqualTo(1));
            Assert.That(source.Inspect(face.Id), Is.EqualTo(PlayVerdict.NotEnoughStamina));
            Assert.That(source.RefusalText(face.Id, PlayVerdict.NotEnoughStamina), Is.EqualTo("「石礫」はスタミナ 1 が要ります。残りは 0 です"));
            Assert.That(source.RefusalText(face.Id, PlayVerdict.NotEnoughStamina, 1), Is.EqualTo("「石礫」はスタミナ 1 が要ります。残りは 0 です"));
            Assert.That(source.RefusalText(face.Id, PlayVerdict.NotEnoughStamina, 0), Is.EqualTo("「石礫」はスタミナ 2 が要ります。残りは 0 です"));
        }

        // js: test/depiction/MultiEnemy.test.mjs:355
        [Test]
        public void CardFace_PerEnemyCost_ReadsItsSlot_AndFallsBackToTheFace()
        {
            var face = new CardFace
            {
                Cost = 1, PrintedCost = 2, StanceHintAfterPay = "+3",
                CostOn = new List<int> { 2, 1 }, StanceHintAfterPayOn = new List<string> { "", "+3" },
            };
            Assert.That(new[] { face.CostFor(0), face.CostFor(1), face.CostFor(2), face.CostFor(-1) }, Is.EqualTo(new[] { 2, 1, 1, 1 }));
            Assert.That(new[] { face.CostDropFor(0), face.CostDropFor(1), face.CostDropFor(5) }, Is.EqualTo(new[] { 0, 1, 1 }));
            Assert.That(new[] { face.StanceHintAfterPayFor(0), face.StanceHintAfterPayFor(1), face.StanceHintAfterPayFor(2) }, Is.EqualTo(new[] { "", "+3", "+3" }));
            // A source that writes none (the fixed script, the members written before): the face's own values.
            var plain = new CardFace { Cost = 3, PrintedCost = 3 };
            Assert.That(plain.CostOn, Is.Empty);
            Assert.That(plain.StanceHintAfterPayOn, Is.Empty);
            Assert.That(plain.CostFor(0), Is.EqualTo(3));
            Assert.That(plain.CostDropFor(0), Is.EqualTo(0));
            Assert.That(plain.StanceHintAfterPayFor(0), Is.Null);
            Assert.That(plain.ReachFor(0), Is.False);
            Assert.That(plain.TargetUnit, Is.EqualTo(-1));
        }

        // js: test/depiction/MultiEnemy.test.mjs:366
        [Test]
        public void EachEnemysStripes_TurnTheirOwnWay()
        {
            Assert.That(Enumerable.Range(0, Constants.EnemiesMax).Select(EnemyFrame.DirectionOf).ToList(), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(new[] { 0, 1, 2 }.Select(EnemyFrame.AngleOf).ToList(), Is.EqualTo(new[] { 45, -45, 90 }));
            var enemy = new EnemyFrame();
            Assert.That(new object[] { enemy.Cell, enemy.Size, enemy.Gap, enemy.StripeDirection, enemy.Swatch, enemy.Unit.Down },
                Is.EqualTo(new object[] { 0, 1, -1, 0, false, false }));
            Assert.That(enemy.AimCells, Is.Empty);
            Assert.That(enemy.DenseCells, Is.Empty);
            Assert.That(enemy.MoveCells, Is.Empty);
        }

        /// <summary>
        /// The defaults of the members added, which a source that writes none (the fixed script) leaves: no
        /// enemy on an event (−1), #589's 0 on a cue, no enemy moved, and no per-enemy share.
        /// </summary>
        [Test]
        public void TheAddedMembers_DefaultToNoEnemy()
        {
            Assert.That(new DepictionEvent().Unit, Is.EqualTo(-1));
            var cue = new Cue();
            Assert.That(new[] { cue.Unit, cue.UnitCellAfter, cue.UnitGapAfter }, Is.EqualTo(new[] { 0, 0, 0 }));
            var frame = new DepictionFrame();
            Assert.That(frame.Enemies, Is.Empty);
        }

        // ======================================================================= the source's own extension tests

        /// <summary>
        /// js: test/depiction/CoreBattleSource.test.mjs:380 — the one-enemy writer kept one omen and put the
        /// second enemy's (crossbow_hunter's bolt) on the first when it read the floor: KeyNotFoundException
        /// "The given key 'bolt' was not present in the dictionary." The same moves are now written, each
        /// omen read against its own enemy.
        /// </summary>
        [Test]
        public void ATwoEnemyFight_IsWritten_WhereTheOneEnemyWriterThrew()
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), 8, MoreEnemies: new[] { Enemies.ById("crossbow_hunter") });
            var source = new CoreBattleSource(setup, 1);
            for (int i = 0; i < 20 && !source.Finished; i++)
            {
                if (source.WaitingForPlayer) source.EndTurn();
                else source.AdvanceAuto();
            }
            DepictionFrame frame = source.Frame;
            Assert.That(frame.Enemies.Count, Is.EqualTo(2));
            BattleState state = source.State;
            int read = 0;
            foreach (int k in new[] { 0, 1 })
            {
                if (!state.Enemies[k].Alive || !frame.Enemies[k].Omen.Visible) continue;
                Assert.That(frame.Enemies[k].Omen.KindLabel, Is.EqualTo(CoreText.OmenOf(state.Enemies[k].Omen, state.Enemies[k].Def).KindLabel), "unit " + k + "'s own omen");
                read++;
            }
            Assert.That(read, Is.GreaterThan(0), "an omen was on screen to read");
        }

        /// <summary>
        /// js: test/depiction/CoreBattleSource.test.mjs:414 — ?autobattle fights a fight a reload replayed
        /// from a player's page (suggestCards false); the run reads AutoCardId, which picks as
        /// SuggestedCardId would on a suggesting source.
        /// </summary>
        [Test]
        public void AutoCardId_IsTheSuggestion_EvenOnASourceBuiltNotToSuggest()
        {
            const int seed = 20260921; // js: CoreBattleSource.test.mjs:26 Seed
            var quiet = new CoreBattleSource(BattleSetup.Slice(), seed, false);
            var loud = new CoreBattleSource(BattleSetup.Slice(), seed, true);
            Assert.That(quiet.AutoCardId, Is.EqualTo(""), "nothing before the turn waits for the player");
            quiet.AdvanceAuto();
            loud.AdvanceAuto();
            Assert.That(quiet.SuggestedCardId, Is.EqualTo(""), "the one-enemy getter stays silent");
            Assert.That(quiet.AutoCardId, Is.Not.EqualTo(""));
            Assert.That(quiet.AutoCardId, Is.EqualTo(loud.SuggestedCardId));
            Assert.That(TurnLoop.CanPlay(quiet.State, quiet.AutoCardId), Is.EqualTo(PlayRefusal.None));
        }

        /// <summary>
        /// rules.md §4.2 / depiction-multi-enemy.md "Targets": a named target that is not a standing enemy
        /// answers NotWaiting (the core's NoSuchTarget has no verdict of its own), and a release on it
        /// changes nothing. The pair is fought unattended until the enemy in front has fallen and the one
        /// behind still stands.
        /// </summary>
        [Test]
        public void ANamedTarget_ThatHasFallen_IsRefusedAsNotWaiting_AndNothingMoves()
        {
            int refused = 0;
            for (int seed = 1; seed <= 40 && refused == 0; seed++)
            {
                CoreBattleSource source = Launch(Pair, seed, PairCells);
                int guard = 0;
                while (!source.Finished && guard++ < 2000)
                {
                    if (!source.WaitingForPlayer)
                    {
                        source.AdvanceAuto();
                        continue;
                    }
                    BattleState state = source.State;
                    if (!state.Enemies[0].Alive && state.Enemies[1].Alive)
                    {
                        foreach (CardFace face in source.Frame.Hand)
                        {
                            if (TurnLoop.CanPlay(state, face.Id, 0) != PlayRefusal.NoSuchTarget) continue;
                            int history = source.History.Count;
                            DepictionFrame before = source.Frame;
                            Assert.That(source.Inspect(face.Id, 0), Is.EqualTo(PlayVerdict.NotWaiting), face.Id + " on the fallen enemy");
                            Assert.That(source.PreviewFor(face.Id, 0), Is.EqualTo(""), face.Id + ": no number over a fallen enemy");
                            PlayVerdict verdict = source.TryPlay(face.Id, DepictionText.RequiredZone(face.Aim), 0, out DepictionEvent played);
                            Assert.That(verdict, Is.EqualTo(PlayVerdict.NotWaiting));
                            Assert.That(played, Is.Null);
                            Assert.That(source.History.Count, Is.EqualTo(history), "the core was not asked to play it");
                            Assert.That(source.Frame, Is.SameAs(before), "the screen did not move");
                            Assert.That(source.State, Is.SameAs(state));
                            // Unnamed, the same card goes to the enemy that stands.
                            Assert.That(source.TargetFor(face.Id), Is.EqualTo(1), face.Id + ": the default target is the one standing");
                            refused++;
                        }
                    }
                    string id = source.SuggestedCardId;
                    if (id.Length > 0) source.TryPlay(id, DepictionText.RequiredZone(DepictionText.Find(source.Frame.Hand, id).Aim), out DepictionEvent _);
                    else source.EndTurn();
                }
            }
            Assert.That(refused, Is.GreaterThan(0), "some seed leaves the enemy behind standing alone with a card aimed at one enemy in hand");
        }

        /// <summary>
        /// The port's convention (battle-flow §4.5: unit −1 = the JS's omitted argument): a negative target
        /// names no enemy, so each overload taking one answers as the one without it — the card is judged,
        /// previewed, refused and played on <see cref="CoreBattleSource.TargetFor"/>'s enemy. (In the JS an
        /// explicit −1 reaches the core and is refused; the trial's battle player passes −1 for "no enemy
        /// framed", so the C# reads it as the JS reads <c>undefined</c>.)
        /// </summary>
        [Test]
        public void ANegativeTarget_NamesNoEnemy_AndAnswersAsTheOverloadWithoutOne()
        {
            ITrialSource trial = Launch(Pair, 3, PairCells);
            Assert.That(trial, Is.InstanceOf<IDepictionSource>());
            var source = (CoreBattleSource)trial;
            PlayVerdict[] verdicts = (PlayVerdict[])Enum.GetValues(typeof(PlayVerdict));
            int waits = 0;
            int plays = 0;
            int guard = 0;
            while (!source.Finished && guard++ < 2000)
            {
                if (!source.WaitingForPlayer)
                {
                    source.AdvanceAuto();
                    continue;
                }
                waits++;
                foreach (CardFace face in source.Frame.Hand)
                {
                    foreach (int none in new[] { -1, -2 })
                    {
                        Assert.That(trial.Inspect(face.Id, none), Is.EqualTo(source.Inspect(face.Id)), face.Id + " Inspect " + none);
                        Assert.That(trial.PreviewFor(face.Id, none), Is.EqualTo(source.PreviewFor(face.Id)), face.Id + " PreviewFor " + none);
                        foreach (PlayVerdict v in verdicts)
                        {
                            Assert.That(trial.RefusalText(face.Id, v, none), Is.EqualTo(source.RefusalText(face.Id, v)), face.Id + " RefusalText " + v + " " + none);
                        }
                    }
                }
                string id = source.AutoCardId;
                if (id.Length > 0)
                {
                    CardFace face = DepictionText.Find(source.Frame.Hand, id);
                    int target = source.TargetFor(id);
                    Assert.That(trial.TryPlay(id, DepictionText.RequiredZone(face.Aim), -1, out DepictionEvent played), Is.EqualTo(PlayVerdict.Accepted));
                    Assert.That(played.Unit, Is.EqualTo(face.Aim == CardAim.Single ? target : -1), id + " went to TargetFor's enemy");
                    plays++;
                }
                else
                {
                    trial.EndTurn();
                }
            }
            Assert.That(waits, Is.GreaterThan(0));
            Assert.That(plays, Is.GreaterThan(0));
        }

        // ======================================================================= the driver and its checks

        private static CoreBattleSource Launch(IReadOnlyList<string> ids, int seed, int cells)
        {
            return new BattleLaunch
            {
                EnemyId = ids[0], MoreEnemyIds = ids.Skip(1).ToList(), Seed = seed, FieldCells = cells, AutoPlay = true,
            }.CreateSource();
        }

        /// <summary>js: MultiEnemy.test.mjs:412 — the narrowest line (5〜8 cells) the enemies stand on as the battle starts them, or −1 when none holds them.</summary>
        private static int FieldFor(IReadOnlyList<string> ids)
        {
            List<EnemyDef> defs = ids.Select(Enemies.ById).ToList();
            for (int cells = Constants.FieldCellsMin; cells <= Constants.FieldCellsMax; cells++)
            {
                var setup = new BattleSetup(defs[0], PrototypeDeck.Build(), cells, MoreEnemies: defs.Skip(1).ToList());
                IReadOnlyList<int> starts = setup.EnemyStartCells;
                try
                {
                    Field.Validate(cells, setup.PlayerStartCell, 1, defs.Select((d, i) => (starts[i], d.Size)).ToList());
                    return cells;
                }
                catch (Exception)
                {
                    // too narrow
                }
            }
            return -1;
        }

        /// <summary>A card the player releases on one enemy (targets one, aimed at the opponent): CoreScriptWriter's AimsAtOne.</summary>
        private static bool AimsAtOne(CardDef def)
        {
            return def.Targets == TargetKind.One && EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets);
        }

        /// <summary>構えの予告 for <paramref name="stamina"/> left: CoreScriptWriter's ReserveHint.</summary>
        private static string ReserveHint(int stamina)
        {
            int reserve = Combat.ReserveGuard(Math.Max(0, stamina));
            return reserve > 0 ? "+" + reserve : "";
        }

        /// <summary>What the source still holds back (the JS reads source._pending directly).</summary>
        private static int PendingCount(CoreBattleSource source)
        {
            FieldInfo field = typeof(CoreBattleSource).GetField("_pending", BindingFlags.NonPublic | BindingFlags.Instance);
            return ((ICollection)field.GetValue(source)).Count;
        }

        /// <summary>The 開示度 the source's writer reads (the JS reads source._writer.Disclosure).</summary>
        private static int DisclosureOf(CoreBattleSource source)
        {
            FieldInfo field = typeof(CoreBattleSource).GetField("_writer", BindingFlags.NonPublic | BindingFlags.Instance);
            return ((CoreScriptWriter)field.GetValue(source)).Disclosure;
        }

        private sealed class Fight
        {
            public string Label;
            public int N;
            public bool[] Down;
            public int LastOrder;
            public readonly List<int> Defeats = new List<int>();
            public readonly HashSet<int> HitBy = new HashSet<int>();
            public int Disclosure;
            public int CostSplit;
            public int NotNearest;
        }

        /// <summary>
        /// js: MultiEnemy.test.mjs:435 fight — plays the source to the end as EncounterBattle's unattended
        /// runs do, checking every event, every wait and the end of every move. Returns what it saw.
        /// </summary>
        private static Fight FightToTheEnd(CoreBattleSource source, string label)
        {
            int n = source.State.Enemies.Count;
            var ctx = new Fight { Label = label, N = n, Down = new bool[n], Disclosure = DisclosureOf(source) };
            CheckFrame(source.Frame, ctx, "opening");
            int moves = 0;
            while (!source.Finished)
            {
                Assert.That(++moves, Is.LessThan(MaxMoves), label + ": no end after " + MaxMoves + " moves");
                DepictionEvent ev;
                if (source.WaitingForPlayer)
                {
                    CheckWait(source, ctx);
                    string id = source.SuggestedCardId;
                    if (id.Length > 0)
                    {
                        CardFace face = DepictionText.Find(source.Frame.Hand, id);
                        int target = source.TargetFor(id);
                        PlayVerdict verdict = source.TryPlay(id, DepictionText.RequiredZone(face.Aim), out ev);
                        Assert.That(verdict, Is.EqualTo(PlayVerdict.Accepted), label + ": the suggested card plays");
                        if (face.Aim == CardAim.Single) Assert.That(ev.Unit, Is.EqualTo(target), label + ": " + id + " went to TargetFor's enemy");
                        else Assert.That(ev.Unit, Is.EqualTo(-1), label);
                    }
                    else
                    {
                        ev = source.EndTurn();
                    }
                }
                else
                {
                    ev = source.AdvanceAuto();
                }
                CheckEvent(ev, ctx);
                if (PendingCount(source) == 0) CheckMoveEnd(source, ctx);
            }
            CheckEnd(source, ctx);
            return ctx;
        }

        // js: MultiEnemy.test.mjs:467
        private static void CheckEvent(DepictionEvent ev, Fight ctx)
        {
            string at = ctx.Label + " event " + ev.Order + " (" + ev.Kind + " " + ev.Title + ")";
            Assert.That(ev.Order, Is.EqualTo(ctx.LastOrder + 1), at + ": events are numbered in the order they play");
            ctx.LastOrder = ev.Order;
            Assert.That(ev.Unit >= -1 && ev.Unit < ctx.N, Is.True, at + ": Unit " + ev.Unit);
            switch (ev.Kind)
            {
                case DepictionEventKind.EnemyAction:
                case DepictionEventKind.NextOmen:
                case DepictionEventKind.Defeat:
                    Assert.That(ev.Unit, Is.GreaterThanOrEqualTo(0), at + ": an enemy's beat names its enemy");
                    break;
                case DepictionEventKind.TurnStart:
                case DepictionEventKind.TurnEnd:
                    Assert.That(ev.Unit, Is.EqualTo(-1), at);
                    break;
            }
            foreach (Cue cue in ev.Cues)
            {
                string what = at + " cue " + cue.Kind;
                // #589: every cue names the enemy on its enemy end (0 for the player alone in a one-enemy battle).
                Assert.That(cue.Unit >= 0 && cue.Unit < ctx.N, Is.True, what + ": Unit " + cue.Unit);
                if (cue.Kind == CueKind.RangeSwitch && cue.UnitCellAfter > 0)
                {
                    // An enemy moved: the one the cue names, where it stands now and its own N.
                    Assert.That(cue.UnitGapAfter, Is.EqualTo(Field.Gap(cue.PlayerCellAfter, ev.After.Floor.PlayerSize, cue.UnitCellAfter)), what + ": its N");
                }
                if (cue.Kind == CueKind.Slash && cue.Source == UnitSide.Player) ctx.HitBy.Add(cue.Unit);
                if (cue.Kind == CueKind.Defeat) Assert.That(cue.Unit, Is.EqualTo(ev.Unit), what + ": the beat's enemy falls");
            }
            if (ev.Kind == DepictionEventKind.Defeat)
            {
                Assert.That(ctx.Down[ev.Unit], Is.False, at + ": an enemy falls once");
                Assert.That(ev.After.Enemies[ev.Unit].Unit.Down, Is.True, at + ": and is down from its beat on");
                ctx.Defeats.Add(ev.Unit);
            }
            CheckFrame(ev.After, ctx, at);
        }

        // js: MultiEnemy.test.mjs:508
        private static void CheckFrame(DepictionFrame frame, Fight ctx, string at)
        {
            int n = ctx.N;
            Assert.That(frame.Enemies.Count, Is.EqualTo(n), at + ": one share per enemy");
            int focus = -1;
            var falling = new List<int>();
            for (int k = 0; k < n; k++)
            {
                EnemyFrame enemy = frame.Enemies[k];
                UnitFrame unit = enemy.Unit;
                OmenFrame omen = enemy.Omen;
                Assert.That(enemy.StripeDirection, Is.EqualTo(EnemyFrame.DirectionOf(k)), at);
                Assert.That(enemy.Swatch, Is.EqualTo(n >= 2), at);
                if (ctx.Down[k]) Assert.That(unit.Down, Is.True, at + ": unit " + k + " stays down");
                if (unit.Down)
                {
                    Assert.That(omen.Visible, Is.False, at + ": a fallen enemy shows no omen");
                    Assert.That(omen.PlanVisible, Is.False, at + ": nor a plan");
                    Assert.That(enemy.AimCells, Is.Empty, at + ": nor aims");
                    Assert.That(enemy.Gap, Is.EqualTo(-1), at);
                }
                else if (enemy.Gap == -1)
                {
                    // Fallen in the core, its Defeat beat still to play: no N, and not the frame's enemy.
                    Assert.That(unit.Hp, Is.EqualTo(0), at + ": only a fallen enemy has no N");
                    falling.Add(k);
                }
                else
                {
                    Assert.That(enemy.Gap, Is.EqualTo(Field.Gap(frame.Floor.PlayerCell, frame.Floor.PlayerSize, enemy.Cell)), at + ": unit " + k + "'s own N");
                    if (focus < 0 || enemy.Cell < frame.Enemies[focus].Cell) focus = k;
                }
                if (!omen.Visible) Assert.That(enemy.AimCells, Is.Empty, at + ": a hidden omen aims at nothing");
            }
            for (int k = 0; k < n; k++) ctx.Down[k] = frame.Enemies[k].Unit.Down;
            // Standing enemies never share a cell (battle_core_v4 §7.1).
            List<int> standing = Enumerable.Range(0, n).Where(k => !frame.Enemies[k].Unit.Down && frame.Enemies[k].Unit.Hp > 0).ToList();
            foreach (int ia in standing)
            {
                foreach (int ib in standing)
                {
                    if (ia == ib) continue;
                    EnemyFrame a = frame.Enemies[ia];
                    EnemyFrame b = frame.Enemies[ib];
                    Assert.That(a.Cell + a.Size - 1 < b.Cell || b.Cell + b.Size - 1 < a.Cell, Is.True, at + ": units " + ia + " and " + ib + " share a cell");
                }
            }
            if (focus < 0 && falling.Count > 0) focus = falling.Aggregate((a, b) => frame.Enemies[b].Cell < frame.Enemies[a].Cell ? b : a);
            if (focus >= 0)
            {
                Assert.That(frame.Enemy, Is.SameAs(frame.Enemies[focus].Unit), at + ": Frame.Enemy is the first standing in the line");
                Assert.That(frame.Omen, Is.SameAs(frame.Enemies[focus].Omen), at);
            }
            else
            {
                Assert.That(frame.Enemy.Down, Is.True, at + ": none stands");
                focus = frame.Enemies.FindIndex(e => e.Unit == frame.Enemy);
                Assert.That(focus, Is.GreaterThanOrEqualTo(0), at);
                Assert.That(frame.Omen, Is.SameAs(frame.Enemies[focus].Omen), at);
            }
            // The single-valued members describe Frame.Enemy's share, the same lists (#589).
            EnemyFrame shown = frame.Enemies[focus];
            Assert.That(frame.Floor.EnemyCell, Is.EqualTo(shown.Cell), at + ": Floor.EnemyCell");
            Assert.That(frame.Floor.EnemySize, Is.EqualTo(shown.Size), at);
            Assert.That(frame.Floor.Gap, Is.EqualTo(Field.Gap(frame.Floor.PlayerCell, frame.Floor.PlayerSize, shown.Cell)), at);
            Assert.That(frame.Player.RangeGlyph, Is.EqualTo(frame.Floor.Gap.ToString()), at);
            if (!shown.Unit.Down && shown.Unit.Hp > 0) Assert.That(frame.Floor.Gap, Is.GreaterThanOrEqualTo(0), at + ": the N shown is never negative");
            Assert.That(frame.Floor.AimCells, Is.SameAs(shown.AimCells), at);
            Assert.That(frame.Floor.DenseCells, Is.SameAs(shown.DenseCells), at);
            Assert.That(frame.Floor.MoveCells, Is.SameAs(shown.MoveCells), at);
            foreach (CardFace face in frame.Hand)
            {
                Assert.That(face.TargetUnit >= -1 && face.TargetUnit < n, Is.True, at + ": " + face.Id + "'s target");
                Assert.That(face.ReachOn.Count == 0 || face.ReachOn.Count == n, Is.True, at + ": " + face.Id + "'s reach per enemy");
                if (face.TargetUnit >= 0) Assert.That(face.Aim, Is.EqualTo(CardAim.Single), at);
                for (int k = 0; k < face.ReachOn.Count; k++)
                {
                    if (face.ReachOn[k]) Assert.That(frame.Enemies[k].Unit.Down, Is.False, at + ": " + face.Id + " reaches a standing enemy");
                }
            }
        }

        /// <summary>js: MultiEnemy.test.mjs:585 — once a move has played out, the frame is the core state, enemy by enemy.</summary>
        private static void CheckMoveEnd(CoreBattleSource source, Fight ctx)
        {
            BattleState state = source.State;
            DepictionFrame frame = source.Frame;
            string at = ctx.Label + " turn " + state.Turn + " after event " + ctx.LastOrder;
            Assert.That(frame.Player.Hp, Is.EqualTo(state.Player.Hp), at + ": player HP");
            Assert.That(frame.Player.Guard, Is.EqualTo(state.Player.Guard), at + ": player Guard");
            Assert.That(frame.Player.Stamina, Is.EqualTo(state.Player.Stamina), at + ": player stamina");
            Assert.That(frame.Floor.PlayerCell, Is.EqualTo(state.Player.Cell), at + ": player cell");
            bool ongoing = state.Result == GameResult.Ongoing;
            for (int k = 0; k < ctx.N; k++)
            {
                EnemyUnit unit = state.Enemies[k];
                CombatantState body = unit.Body;
                EnemyFrame share = frame.Enemies[k];
                UnitFrame shown = share.Unit;
                string of = at + ": unit " + k + " (" + unit.Def.Id + ")";
                Assert.That(shown.ArtId, Is.EqualTo(unit.Def.Id), of);
                Assert.That(shown.Hp, Is.EqualTo(body.Hp), of + " HP");
                Assert.That(shown.Guard, Is.EqualTo(body.Guard), of + " Guard");
                Assert.That(shown.Stamina, Is.EqualTo(body.Stamina), of + " stamina");
                Assert.That(shown.StaminaMax, Is.EqualTo(body.MaxStamina), of + " max stamina");
                Assert.That(StatusRows(shown), Is.EqualTo(CoreStatusRows(body)), of + " statuses");
                Assert.That(shown.Stances.Sum(r => r.Count), Is.EqualTo(body.StanceList.Count), of + " stances");
                if (state.Result != GameResult.Lost) Assert.That(shown.Down, Is.EqualTo(!unit.Alive), of + " down ⇔ fallen in the core");
                else if (shown.Down) Assert.That(unit.Alive, Is.False, of + " down only when fallen");
                if (!unit.Alive) continue;
                Assert.That(share.Cell, Is.EqualTo(body.Cell), of + " cell");
                Assert.That(share.Gap, Is.EqualTo(state.GapTo(k)), of + " N");
                if (!ongoing || state.Turn < 1) continue;
                // The omen and the plan on screen are this enemy's own, read against its own actions.
                OmenFrame omen = share.Omen;
                OmenFrame own = CoreText.OmenOf(unit.Omen, unit.Def);
                Assert.That(omen.Visible, Is.True, of + " omen shown");
                Assert.That(omen.KindLabel, Is.EqualTo(own.KindLabel), of + " omen kind");
                Assert.That(omen.Icon, Is.EqualTo(own.Icon), of + " omen icon");
                Assert.That(omen.SideGlyph, Is.EqualTo(own.SideGlyph), of + " omen reach");
                if (OmenDisclosure.ShowsPlan(ctx.Disclosure))
                {
                    Assert.That(omen.PlanVisible, Is.EqualTo(unit.Plan != null), of + " plan shown");
                    if (unit.Plan != null) Assert.That(omen.PlanKindLabel, Is.EqualTo(CoreText.OmenKindWord(unit.Plan.Label.Kind)), of + " plan kind");
                }
                Assert.That(share.AimCells, Is.EqualTo(FloorCells.AimOf(unit.Omen, body, body.Cell)), of + " aimed cells");
                Assert.That(share.DenseCells, Is.EqualTo(FloorCells.DenseOf(state, k)), of + " dense cells");
                Assert.That(share.MoveCells, Is.EqualTo(FloorCells.MoveOf(state, k)), of + " move cells");
            }
        }

        /// <summary>js: MultiEnemy.test.mjs:631 — at a wait: each card's default target, numbers and refusals are the core's for that target.</summary>
        private static void CheckWait(CoreBattleSource source, Fight ctx)
        {
            BattleState state = source.State;
            Assert.That(state.Phase, Is.EqualTo(BattlePhase.PlayerAction));
            IReadOnlyList<int> living = state.Living;
            foreach (CardFace face in source.Frame.Hand)
            {
                string at = ctx.Label + " turn " + state.Turn + " " + face.Id;
                int target = source.TargetFor(face.Id);
                CardInstance card = state.Hand.First(c => c.InstanceId == face.Id);
                bool one = AimsAtOne(card.Def);
                Assert.That(face.TargetUnit, Is.EqualTo(one ? target : -1), at + ": the face names TargetFor's enemy");
                if (one) Assert.That(living, Does.Contain(target), at + ": the default target stands");
                PlayPreview preview = TurnLoop.Preview(state, face.Id, target);
                Assert.That(face.Cost, Is.EqualTo(preview.Cost), at + ": the cost is read on the target");
                Assert.That(source.Inspect(face.Id), Is.EqualTo(source.Inspect(face.Id, target)), at);
                Assert.That(source.PreviewFor(face.Id), Is.EqualTo(source.PreviewFor(face.Id, target)), at);
                if (source.Inspect(face.Id) == PlayVerdict.Accepted)
                {
                    Assert.That(TurnLoop.CanPlay(state, face.Id, target), Is.EqualTo(PlayRefusal.None), at);
                }
                if (one)
                {
                    for (int k = 0; k < ctx.N; k++)
                    {
                        bool alive = state.Enemies[k].Alive;
                        bool reach = alive && TurnLoop.Preview(state, face.Id, k).InReach;
                        Assert.That(face.ReachOn[k], Is.EqualTo(reach), at + ": reach on " + k);
                        // §5.2 / §6.3: the cost, its ▼ and the 構えの予告 while the card frames enemy k are the core's on k.
                        int cost = alive ? TurnLoop.Preview(state, face.Id, k).Cost : face.Cost;
                        Assert.That(face.CostFor(k), Is.EqualTo(cost), at + ": cost on " + k);
                        Assert.That(face.CostDropFor(k), Is.EqualTo(Math.Max(0, face.PrintedCost - cost)), at + ": ▼ on " + k);
                        Assert.That(face.StanceHintAfterPayFor(k), Is.EqualTo(ReserveHint(state.Player.Stamina - cost)), at + ": 構えの予告 on " + k);
                        if (!alive) continue;
                        // §5.4: a card released on k and refused says so with k's numbers.
                        PlayVerdict verdict = source.Inspect(face.Id, k);
                        if (verdict == PlayVerdict.NotEnoughStamina)
                        {
                            Assert.That(source.RefusalText(face.Id, verdict, k),
                                Is.EqualTo("「" + face.Name + "」はスタミナ " + cost + " が要ります。残りは " + state.Player.Stamina + " です"), at + ": refused on " + k);
                        }
                        else if (verdict == PlayVerdict.OutOfRange)
                        {
                            Assert.That(source.RefusalText(face.Id, verdict, k), Does.EndWith("いまは " + state.GapTo(k) + " です"), at + ": refused on " + k);
                        }
                    }
                    Assert.That(face.CostFor(face.TargetUnit), Is.EqualTo(face.Cost), at + ": Cost is TargetUnit's");
                    Assert.That(face.StanceHintAfterPayFor(face.TargetUnit), Is.EqualTo(face.StanceHintAfterPay), at);
                    // The default target is the cheapest of the enemies the card can be played on (else of the
                    // ones in its reach), so the face at rest never shows more than the card costs on any of them.
                    List<int> playable = living.Where(k => TurnLoop.CanPlay(state, face.Id, k) == PlayRefusal.None).ToList();
                    List<int> pool = playable.Count > 0 ? playable : living.Where(k => face.ReachOn[k]).ToList();
                    if (pool.Count > 0) Assert.That(pool, Does.Contain(target), at + ": the default target is one of [" + string.Join(",", pool) + "]");
                    foreach (int k in pool)
                    {
                        Assert.That(face.Cost, Is.LessThanOrEqualTo(face.CostFor(k)), at + ": the default target costs least (" + face.Cost + " vs " + face.CostFor(k) + " on " + k + ")");
                    }
                    if (living.Select(k => face.CostFor(k)).Distinct().Count() > 1) ctx.CostSplit += 1;
                    if (pool.Count > 1 && target != pool[0]) ctx.NotNearest += 1;
                }
                else
                {
                    Assert.That(face.CostOn, Is.Empty, at + ": a card aimed at no one enemy has one cost");
                    Assert.That(face.StanceHintAfterPayOn, Is.Empty, at);
                }
            }
        }

        // js: MultiEnemy.test.mjs:684
        private static void CheckEnd(CoreBattleSource source, Fight ctx)
        {
            DepictionFrame frame = source.Frame;
            BattleState state = source.State;
            Assert.That(source.Finished, Is.True);
            Assert.That(state.Result, Is.Not.EqualTo(GameResult.Ongoing), ctx.Label + ": the battle ended");
            Assert.That(frame.Outcome, Is.EqualTo(state.Result == GameResult.Won ? BattleOutcome.Won : BattleOutcome.Lost), ctx.Label);
            if (state.Result == GameResult.Won)
            {
                Assert.That(frame.Enemies.All(e => e.Unit.Down), Is.True, ctx.Label + ": won only when every enemy is down");
                Assert.That(ctx.Defeats.OrderBy(k => k).ToList(), Is.EqualTo(Enumerable.Range(0, ctx.N).ToList()), ctx.Label + ": each enemy fell once");
            }
            else
            {
                Assert.That(state.Living.Count > 0 || state.Player.Hp <= 0, Is.True, ctx.Label);
                Assert.That(ctx.Defeats.Count, Is.LessThan(ctx.N), ctx.Label + ": a lost battle leaves an enemy");
            }
            foreach (int k in ctx.Defeats) Assert.That(state.Enemies[k].Alive, Is.False, ctx.Label + ": unit " + k + " fell in the core");
        }

        private static List<string> StatusRows(UnitFrame unitFrame)
        {
            return unitFrame.Statuses.Where(c => c.Kind != ChipKind.Lasting).Select(c => c.Label + " " + c.Stacks).ToList();
        }

        private static List<string> CoreStatusRows(CombatantState body)
        {
            return body.Statuses.Kinds.OrderBy(kind => (int)kind)
                .Select(kind =>
                {
                    StatusChip chip = StatusChipText.Chip(kind, body.Statuses.Stacks(kind));
                    return chip.Label + " " + chip.Stacks;
                }).ToList();
        }
    }

    /// <summary>
    /// The one-enemy battles <see cref="MultiEnemyTests"/> replays to prove the multi-enemy writer leaves
    /// them alone (port of the JS's test/depiction/_singleEnemyRuns.mjs). Every screen event, every frame
    /// and every answer the source gives at a wait (Inspect, PreviewFor, RefusalText for each verdict,
    /// GuideText, SuggestedCardId, CanEndTurn) is serialized with every public member, names sorted, and
    /// hashed. The golden keeps the hashes and the member names each type had; the test serializes the
    /// new code's output keeping only those members (the new ones are additions, checked on their own)
    /// and compares the hashes.
    /// </summary>
    internal static class SingleEnemyRuns
    {
        public const string GoldenName = "MultiEnemy.single.golden.txt";

        /// <summary>"write": the test writes the golden from the code as it stands, instead of comparing.</summary>
        public const string WriteSwitch = "DEPICTION_SINGLE_GOLDEN";

        /// <summary>A folder: the test also writes every record of every scenario there, one text file each.</summary>
        public const string DumpSwitch = "DEPICTION_SINGLE_GOLDEN_DUMP";

        private const int SourceMaxMoves = 4000;
        private const int WriterMaxTurns = 40;
        private const int WriterMaxPlays = 6;

        /// <summary>The golden next to this file (dotnet test reads the kit's copy).</summary>
        public static string GoldenPath([CallerFilePath] string here = "")
        {
            return Path.Combine(Path.GetDirectoryName(here) ?? "", GoldenName);
        }

        /// <summary>swordsman_cards_v4 §2's initial forty (BattleCore.Sim's Decks.InitialFortyIds).</summary>
        private static readonly string[] InitialFortyIds =
        {
            "thrust", "kesa_cut", "overhead", "wrist_cut", "side_sweep",
            "flat_strike", "probe_thrust", "pierce", "throw_blade", "reach_thrust",
            "brace", "iron_block", "low_guard", "riposte_guard", "deep_breath",
            "water_stance", "rock_stance", "flow_stance", "first_aid", "spirit_roar",
            "focus", "observe", "war_cry", "second_wind", "footwork",
            "back_leap", "slide_step", "break_off", "lunge", "feint",
            "boar_rush", "rend", "body_check", "stone_throw", "parry_cut",
            "guard_thrust", "step_in_guard", "step_out_guard", "iron_wall", "twist_away",
        };

        /// <summary>A card of the test's own: 俊敏 2 on the player (as the JS's FreeStep.test.mjs), so the writer is fed free steps.</summary>
        private static readonly CardDef Quicken = new CardDef("quicken", "身軽", BattleAttribute.Skill, 1,
            new Face(Statuses: new[] { new StatusGrant(StatusKind.Swift, 2, true) }), null, TargetKind.Self, "");

        public sealed class Spec
        {
            public string Id;
            /// <summary>"source" (CoreBattleSource through BattleLaunch, unattended) or "writer" (the writer fed TurnLoop by a chooser).</summary>
            public string Kind;
            public Action<BattleLaunch> Launch;
            public string Enemy;
            public int Seed;
            public string Deck;
        }

        // ---- the scenarios ----------------------------------------------------------------------

        /// <summary>js: _singleEnemyRuns.mjs:135 — every one-enemy scenario.</summary>
        public static List<Spec> Scenarios()
        {
            var list = new List<Spec>();
            List<string> ids = Enemies.All.Select(d => d.Id).ToList();
            // The unattended source, as EncounterBattle.StartNext(true) builds it, for every enemy.
            foreach (string id in ids)
            {
                foreach (int seed in new[] { 1, 2, 3, 4 })
                {
                    string enemy = id;
                    int s = seed;
                    list.Add(new Spec
                    {
                        Id = "source:" + id + "|" + seed, Kind = "source",
                        Launch = l => { l.EnemyId = enemy; l.Seed = s; l.FieldCells = 7; },
                    });
                }
            }
            // Journal levels below 2, the narrow and wide lines, carried HP and stamina, the tools, a turn limit.
            var variants = new (string Tag, Action<BattleLaunch> Set)[]
            {
                ("d0", l => l.Disclosure = 0),
                ("d1", l => l.Disclosure = 1),
                ("narrow", l => l.FieldCells = 5),
                ("wide", l => { l.FieldCells = 8; l.PlayerStartCell = 1; l.StartGap = 4; }),
                ("carried", l => { l.StartHp = 23; l.StartStamina = 2; l.MaxStamina = 8; l.ChainIndex = 2; l.ChainTotal = 2; }),
                ("tools", l => { l.StartStaminaBonus = 3; l.FirstHitGuard = 5; l.BossWordWard = 1; l.PlayerMaxHp = 70; }),
                ("limited", l => l.StopAfterTurns = 4),
            };
            foreach (string id in new[] { "polearm_warped", "crossbow_hunter", "armored_warden", "pack_alpha", "miasma_priest", "abyss_angler", "distortion_root" })
            {
                foreach (var v in variants)
                {
                    string enemy = id;
                    Action<BattleLaunch> set = v.Set;
                    list.Add(new Spec
                    {
                        Id = "source:" + id + "|" + v.Tag, Kind = "source",
                        Launch = l => { l.EnemyId = enemy; l.Seed = 5; l.FieldCells = 7; set(l); },
                    });
                }
            }
            // The writer fed TurnLoop by the chooser below, two decks per enemy.
            foreach (string id in ids)
            {
                foreach (int seed in new[] { 2, 3 })
                {
                    list.Add(new Spec { Id = "writer:" + id + "|" + seed, Kind = "writer", Enemy = id, Seed = seed, Deck = seed == 2 ? "prototype" : "initial_forty" });
                }
            }
            // The writer fed free steps (the deck of six 身軽): whatever the writer makes of a step it is handed.
            foreach (string id in new[] { "polearm_warped", "crossbow_hunter", "distortion_root" })
            {
                list.Add(new Spec { Id = "writer:" + id + "|quicken", Kind = "writer", Enemy = id, Seed = 7, Deck = "quicken" });
            }
            return list;
        }

        /// <summary>js: _singleEnemyRuns.mjs:165 — runs one scenario and hands back its records (canonical JSON), in order.</summary>
        public static List<string> Run(Spec spec, Canon canon)
        {
            return spec.Kind == "source" ? RunSource(spec, canon) : RunWriter(spec, canon);
        }

        private static List<string> RunSource(Spec spec, Canon canon)
        {
            var records = new List<string>();
            var launch = new BattleLaunch { AutoPlay = true };
            spec.Launch(launch);
            CoreBattleSource source = launch.CreateSource();
            records.Add(canon.Of(Bag("open", source.Frame)));
            var verdicts = (PlayVerdict[])Enum.GetValues(typeof(PlayVerdict));
            int guard = 0;
            while (!source.Finished && guard++ < SourceMaxMoves)
            {
                var at = Bag(
                    "Waiting", source.WaitingForPlayer, "Finished", source.Finished, "CanEndTurn", source.CanEndTurn,
                    "Guide", source.GuideText, "Suggested", source.SuggestedCardId);
                DepictionEvent ev;
                try
                {
                    if (source.WaitingForPlayer)
                    {
                        var hand = new List<object>();
                        foreach (CardFace face in source.Frame.Hand)
                        {
                            var refusals = new SortedDictionary<string, object>(StringComparer.Ordinal);
                            foreach (PlayVerdict v in verdicts) refusals[v.ToString()] = source.RefusalText(face.Id, v);
                            hand.Add(Bag("Id", face.Id, "Inspect", source.Inspect(face.Id), "PreviewFor", source.PreviewFor(face.Id), "Refusals", refusals));
                        }
                        at["Hand"] = hand;
                        string id = source.SuggestedCardId;
                        if (id.Length > 0)
                        {
                            CardFace face = DepictionText.Find(source.Frame.Hand, id);
                            DropZone wrong = face.Aim == CardAim.Single ? DropZone.AboveThrowLine : DropZone.Receiver;
                            at["Wrong"] = source.TryPlay(id, wrong, out DepictionEvent _);
                            at["Verdict"] = source.TryPlay(id, DepictionText.RequiredZone(face.Aim), out ev);
                        }
                        else
                        {
                            ev = source.EndTurn();
                        }
                    }
                    else
                    {
                        ev = source.AdvanceAuto();
                    }
                }
                catch (Exception e)
                {
                    records.Add(canon.Of(Bag("at", at, "error", e)));
                    break;
                }
                records.Add(canon.Of(Bag("at", at, "ev", ev)));
            }
            records.Add(canon.Of(Bag("end", Bag(
                "Finished", source.Finished, "Result", source.State.Result, "Turn", source.State.Turn,
                "History", source.History.Count, "Guard", guard))));
            return records;
        }

        private static List<string> RunWriter(Spec spec, Canon canon)
        {
            var records = new List<string>();
            var setup = new BattleSetup(Enemies.ById(spec.Enemy), DeckOf(spec.Deck), 7);
            var writer = new CoreScriptWriter(setup.Enemy);
            var rng = new SeededRng(spec.Seed);
            void Write(StepResult r)
            {
                try
                {
                    records.Add(canon.Of(Bag("events", writer.Write(r.Events, r.State))));
                }
                catch (Exception e)
                {
                    records.Add(canon.Of(Bag("error", e)));
                }
            }

            StepResult step = TurnLoop.Start(setup, rng);
            records.Add(canon.Of(Bag("open", writer.Opening(step.State))));
            BattleState state = step.State;
            while (state.Result == GameResult.Ongoing && state.Turn < WriterMaxTurns)
            {
                step = TurnLoop.BeginPlayerTurn(state, rng);
                Write(step);
                state = step.State;
                if (state.Result != GameResult.Ongoing) break;
                int free = ChooseFreeStep(state);
                if (free != 0)
                {
                    step = TurnLoop.TakeFreeStep(state, free);
                    Write(step);
                    state = step.State;
                }
                int played = 0;
                while (state.Result == GameResult.Ongoing)
                {
                    (string Id, int Target)? choice = Choose(state, played);
                    if (choice == null) break;
                    step = TurnLoop.PlayCard(state, choice.Value.Id, rng, choice.Value.Target);
                    Write(step);
                    state = step.State;
                    played++;
                }
                if (state.Result != GameResult.Ongoing) break;
                step = TurnLoop.EndTurn(state, rng);
                Write(step);
                state = step.State;
            }
            records.Add(canon.Of(Bag("end", Bag("Result", state.Result, "Turn", state.Turn))));
            return records;
        }

        private static List<CardInstance> DeckOf(string deck)
        {
            switch (deck)
            {
                case "prototype":
                    return PrototypeDeck.Build();
                case "initial_forty":
                    return InitialFortyIds.Select(id => new CardInstance(id + "-0", CardCatalog.ById(id))).ToList();
                case "quicken":
                {
                    var cards = new List<CardInstance>();
                    for (int i = 0; i < 6; i++) cards.Add(new CardInstance("quicken-" + i, Quicken));
                    IReadOnlyList<CardDef> kinds = PrototypeDeck.Kinds;
                    for (int i = 0; cards.Count < 20; i++) cards.Add(new CardInstance("p-" + i, kinds[i % kinds.Count]));
                    return cards;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(deck), deck, null);
            }
        }

        /// <summary>The free step, when the core offers one: forward on even turns, back on odd ones.</summary>
        private static int ChooseFreeStep(BattleState state)
        {
            if (!TurnLoop.CanTakeFreeStep(state)) return 0;
            return state.Turn % 2 == 0 ? 1 : -1;
        }

        /// <summary>
        /// The writer runs' player: of every card the core lets the player play now, on every standing
        /// enemy, the one whose preview takes most HP, then has the most power, then gives most Guard;
        /// the earlier card and enemy on a tie. At most <see cref="WriterMaxPlays"/> cards a turn.
        /// </summary>
        private static (string Id, int Target)? Choose(BattleState state, int played)
        {
            if (played >= WriterMaxPlays) return null;
            (string Id, int Target)? best = null;
            long bestScore = long.MinValue;
            foreach (CardInstance card in state.Hand)
            {
                foreach (int target in state.Living)
                {
                    if (TurnLoop.CanPlay(state, card.InstanceId, target) != PlayRefusal.None) continue;
                    PlayPreview preview = TurnLoop.Preview(state, card.InstanceId, target);
                    long score = preview == null ? 0 : preview.Damage * 10000L + preview.RawPower * 100L + preview.GuardGain;
                    if (best == null || score > bestScore)
                    {
                        best = (card.InstanceId, target);
                        bestScore = score;
                    }
                }
            }
            return best;
        }

        // ---- canonical JSON ---------------------------------------------------------------------

        /// <summary>An ad-hoc record: name, value, name, value … (an untagged object, its names sorted).</summary>
        public static SortedDictionary<string, object> Bag(params object[] pairs)
        {
            var bag = new SortedDictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < pairs.Length; i += 2) bag[(string)pairs[i]] = pairs[i + 1];
            return bag;
        }

        public static string Hash(string record)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(record));
                var sb = new StringBuilder();
                for (int i = 0; i < 6; i++) sb.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        /// <summary>
        /// js: _singleEnemyRuns.mjs:63 canon / :108 project. A value as plain JSON: every public instance
        /// field and settable property (computed getters are left out), names sorted, class instances
        /// tagged with "$t" (their type's name), enums by name. With <c>keys</c> (the golden's), a tagged
        /// object keeps only the members its type had then, and the ones it drops are counted in
        /// <see cref="Added"/>; a type the golden did not have is an error.
        /// </summary>
        public sealed class Canon
        {
            private readonly IReadOnlyDictionary<string, HashSet<string>> _keys;
            private static readonly Dictionary<Type, List<(string Name, Func<object, object> Get)>> Members =
                new Dictionary<Type, List<(string Name, Func<object, object> Get)>>();

            /// <summary>The member names of every tagged type met, for the golden's keys.</summary>
            public readonly SortedDictionary<string, SortedSet<string>> Seen = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

            /// <summary>"Type.Member" of every member left out because the golden's type did not have it.</summary>
            public readonly SortedSet<string> Added = new SortedSet<string>(StringComparer.Ordinal);

            public Canon(IReadOnlyDictionary<string, HashSet<string>> keys)
            {
                _keys = keys;
            }

            public string Of(object value)
            {
                var sb = new StringBuilder();
                Write(sb, value, 0);
                return sb.ToString();
            }

            private void Write(StringBuilder sb, object v, int depth)
            {
                if (depth > 60) throw new InvalidOperationException("Canon: nested too deep");
                switch (v)
                {
                    case null:
                        sb.Append("null");
                        return;
                    case string s:
                        Str(sb, s);
                        return;
                    case bool b:
                        sb.Append(b ? "true" : "false");
                        return;
                    case Enum e:
                        Str(sb, e.ToString());
                        return;
                    case int i:
                        sb.Append(i.ToString(CultureInfo.InvariantCulture));
                        return;
                    case long l:
                        sb.Append(l.ToString(CultureInfo.InvariantCulture));
                        return;
                    case float f:
                        sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                        return;
                    case double d:
                        sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                        return;
                    case Exception ex:
                        Write(sb, Bag("$error", ex.GetType().Name, "Message", ex.Message), depth + 1);
                        return;
                    case IDictionary<string, object> bag:
                    {
                        sb.Append('{');
                        bool first = true;
                        foreach (string key in bag.Keys.OrderBy(k => k, StringComparer.Ordinal))
                        {
                            if (!first) sb.Append(',');
                            first = false;
                            Str(sb, key);
                            sb.Append(':');
                            Write(sb, bag[key], depth + 1);
                        }
                        sb.Append('}');
                        return;
                    }
                    case IEnumerable list:
                    {
                        sb.Append('[');
                        bool first = true;
                        foreach (object item in list)
                        {
                            if (!first) sb.Append(',');
                            first = false;
                            Write(sb, item, depth + 1);
                        }
                        sb.Append(']');
                        return;
                    }
                }

                Type type = v.GetType();
                string name = type.Name;
                HashSet<string> known = null;
                if (_keys != null && !_keys.TryGetValue(name, out known))
                {
                    throw new InvalidOperationException("Canon: type " + name + " did not exist in the one-enemy snapshot");
                }
                if (!Seen.TryGetValue(name, out SortedSet<string> seen))
                {
                    seen = new SortedSet<string>(StringComparer.Ordinal);
                    Seen[name] = seen;
                }
                sb.Append("{\"$t\":");
                Str(sb, name);
                foreach ((string Name, Func<object, object> Get) member in MembersOf(type))
                {
                    seen.Add(member.Name);
                    if (known != null && !known.Contains(member.Name))
                    {
                        Added.Add(name + "." + member.Name);
                        continue;
                    }
                    sb.Append(',');
                    Str(sb, member.Name);
                    sb.Append(':');
                    Write(sb, member.Get(v), depth + 1);
                }
                sb.Append('}');
            }

            private static List<(string Name, Func<object, object> Get)> MembersOf(Type type)
            {
                lock (Members)
                {
                    if (Members.TryGetValue(type, out var cached)) return cached;
                    var list = new List<(string Name, Func<object, object> Get)>();
                    foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        FieldInfo f = field;
                        list.Add((f.Name, o => f.GetValue(o)));
                    }
                    foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (property.GetIndexParameters().Length > 0 || property.SetMethod == null || !property.SetMethod.IsPublic) continue;
                        PropertyInfo p = property;
                        list.Add((p.Name, o => p.GetValue(o)));
                    }
                    list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                    Members[type] = list;
                    return list;
                }
            }

            private static void Str(StringBuilder sb, string s)
            {
                sb.Append('"');
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
                sb.Append('"');
            }
        }

        // ---- the golden file ----------------------------------------------------------------------

        /// <summary>
        /// MultiEnemy.single.golden.txt: "key Type Member …" lines (the member names each type had) and
        /// "scenario id count hash …" lines (one 12-hex hash per record), in scenario order.
        /// </summary>
        public sealed class Golden
        {
            public readonly Dictionary<string, HashSet<string>> Keys = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            public readonly List<(string Id, string[] Hashes)> Scenarios = new List<(string Id, string[] Hashes)>();

            public static Golden Read(string path)
            {
                var golden = new Golden();
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.TrimEnd();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    string[] parts = line.Split(' ');
                    if (parts[0] == "key")
                    {
                        golden.Keys[parts[1]] = new HashSet<string>(parts.Skip(2), StringComparer.Ordinal);
                    }
                    else if (parts[0] == "scenario")
                    {
                        int count = int.Parse(parts[2], CultureInfo.InvariantCulture);
                        string[] hashes = parts.Skip(3).ToArray();
                        if (hashes.Length != count) throw new InvalidDataException(parts[1] + ": " + hashes.Length + " hashes, " + count + " expected");
                        golden.Scenarios.Add((parts[1], hashes));
                    }
                }
                return golden;
            }
        }

        /// <summary>js: _singleEnemyRuns.mjs:259 main — writes the golden from the code as it stands.</summary>
        public static void WriteGolden(string path, List<Spec> specs, string dump)
        {
            var canon = new Canon(null);
            var lines = new List<string>();
            int records = 0;
            for (int s = 0; s < specs.Count; s++)
            {
                List<string> run = Run(specs[s], canon);
                if (!string.IsNullOrEmpty(dump)) Dump(dump, s, specs[s], run);
                records += run.Count;
                lines.Add("scenario " + specs[s].Id + " " + run.Count + " " + string.Join(" ", run.Select(Hash)));
            }
            var head = new List<string>
            {
                "# One-enemy runs of the C# depiction (CoreBattleSource / CoreScriptWriter) before the multi-enemy",
                "# extension: MultiEnemyTests.cs, SingleEnemyRuns. " + specs.Count + " scenarios, " + records + " records.",
                "# Regenerate from the code before a deliberate one-enemy change: DEPICTION_SINGLE_GOLDEN=write.",
            };
            foreach (KeyValuePair<string, SortedSet<string>> pair in canon.Seen)
            {
                head.Add("key " + pair.Key + " " + string.Join(" ", pair.Value));
            }
            head.AddRange(lines);
            File.WriteAllText(path, string.Join("\n", head) + "\n", new UTF8Encoding(false));
        }

        public static void Dump(string folder, int index, Spec spec, List<string> records)
        {
            Directory.CreateDirectory(folder);
            string name = index.ToString("000", CultureInfo.InvariantCulture) + "-" + spec.Id.Replace(':', '_').Replace('|', '_') + ".txt";
            File.WriteAllText(Path.Combine(folder, name), string.Join("\n", records) + "\n", new UTF8Encoding(false));
        }
    }
}
