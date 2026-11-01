// dotnet-only: BattlePlayer's input paths (port of scenes/battle/player.js) against a real CoreBattleSource and the fake
// modules — the end-turn mash window (review fixes 16 / 17 / 21: presses every 0.1 / 0.4 / 0.65–0.8 / 0.7–1.4 s), the
// queued end turn, the refusal lines, the one-press fast-forward's end rules (BattleScene.js armFF on the player's
// eventEnds / afterWaiting / onEventEnd / onState), the result's hold and skip, a loss waiting for 「手記へ」, the speed
// switch, Halt. Real time is stepped 1 ms a frame.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction;
using Depiction.Bridge;
using NUnit.Framework;
using Trial.Battle.Flow.Tests.Fakes;

namespace Trial.Battle.Flow.Tests
{
    public class BattlePlayerInputTests
    {
        private List<Exception> _unhandled;

        [SetUp]
        public void SetUp()
        {
            Microtasks.Reset();
            _unhandled = new List<Exception>();
            Microtasks.Unhandled = e => _unhandled.Add(e);
        }

        [TearDown]
        public void TearDown()
        {
            Assert.That(_unhandled, Is.Empty, "no exception may reach Microtasks.Unhandled");
            Microtasks.Reset();
        }

        /// <summary>A battle played by hand (no ctx.Auto), started and run to the player's first wait.</summary>
        private static Rig ToFirstWait(int startGap = BattleLaunch.DefaultStartGap, int? startHp = null, string enemy = Enemies.PolearmWarpedId, int seed = BattleLaunch.DefaultSeed)
        {
            var rig = new Rig(Rig.Launch(enemy, seed, 6, startHp: startHp, startGap: startGap));
            rig.Start();
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle), Is.True, "the first turn opens");
            return rig;
        }

        private static int EndTurns(Rig rig) => rig.Source.Calls.Count(c => c == "EndTurn");

        // ---- the end-turn mash window (player.js:1886) ------------------------------------------------------

        [Test]
        public void APress_InTheTurnsFirstHalfSecond_IsRefused_AndTheWindowSlides_ThenAPressAfterItEndsTheTurn()
        {
            Rig rig = ToFirstWait();
            rig.Run(300);
            Assert.That(rig.Input(() => rig.Player.RequestEndTurn(queue: true, press: true)), Is.False, "the open guard (500 ms)");
            Assert.That(rig.Hud.Refuses, Is.EqualTo(1), "a swallowed press shakes the plate");
            rig.Run(300);
            Assert.That(rig.Input(() => rig.Player.RequestEndTurn(queue: true, press: true)), Is.False, "inside the 600 ms window the first press opened");
            Assert.That(rig.Hud.Refuses, Is.EqualTo(2));
            Assert.That(EndTurns(rig), Is.EqualTo(0));
            rig.Run(700);
            Assert.That(rig.Input(() => rig.Player.RequestEndTurn(queue: true, press: true)), Is.True, "a press after the window ends the turn");
            Assert.That(EndTurns(rig), Is.EqualTo(1));
            Assert.That(rig.Player.State, Is.EqualTo(BattlePlayerState.Resolving));
        }

        [Test]
        public void AFirstPress_AfterAPause_EndsTheTurnAtOnce()
        {
            Rig rig = ToFirstWait();
            rig.Run(600);
            Assert.That(rig.Input(() => rig.Player.RequestEndTurn(queue: true, press: true)), Is.True);
            Assert.That(rig.Hud.Refuses, Is.EqualTo(0));
            Assert.That(rig.Hand.Cancels, Is.EqualTo(1), "a held or selected card is let go");
        }

        /// <summary>
        /// Presses that keep a rhythm under 1.5 s while the enemy's turn plays (review 16 / 17 / 21): every press while the flow
        /// runs is swallowed; in the next turn the presses are refused until 1.5 s after the player was asked, and the first
        /// press from then on ends the turn.
        /// </summary>
        [TestCase(100)]
        [TestCase(400)]
        [TestCase(700)]
        [TestCase(1000)]
        [TestCase(1400)]
        public void APressRhythm_CarriedIntoTheNextTurn_EndsIt_OnlyFromOneAndAHalfSecondsAfterItOpened(int every)
        {
            Rig rig = ToFirstWait();
            rig.Run(1000);
            Assert.That(rig.Input(() => rig.Player.RequestEndTurn(queue: true, press: true)), Is.True, "turn 1 ends");
            float openedAt = -1;
            int swallowedInIdle = 0;
            int endedAfter = -1;
            for (int presses = 0; presses < 400 && endedAfter < 0; presses++)
            {
                for (int k = 0; k < every; k++)
                {
                    BattlePlayerState before = rig.Player.State;
                    rig.Env.Clock.Step(1f);
                    if (openedAt < 0 && before != BattlePlayerState.Idle && rig.Player.State == BattlePlayerState.Idle) openedAt = rig.Env.Real.Now;
                }
                bool idle = rig.Player.State == BattlePlayerState.Idle;
                int calls = EndTurns(rig);
                bool ended = rig.Input(() => rig.Player.RequestEndTurn(queue: true, press: true));
                if (!idle)
                {
                    Assert.That(ended, Is.False, "a press while the flow runs is swallowed");
                    Assert.That(EndTurns(rig), Is.EqualTo(calls));
                }
                else if (ended) endedAfter = (int)(rig.Env.Real.Now - openedAt);
                else swallowedInIdle++;
            }
            Assert.That(openedAt, Is.GreaterThan(0), "turn 2 opened");
            Assert.That(endedAfter, Is.GreaterThanOrEqualTo((int)L.EndTurnMashMaxMs), "not before 1.5 s after the turn opened");
            Assert.That(endedAfter, Is.LessThan((int)L.EndTurnMashMaxMs + every), "the first press from 1.5 s on ends it");
            Assert.That(rig.Hud.Refuses, Is.EqualTo(swallowedInIdle), "each press refused in the player's wait shakes the plate");
            Assert.That(swallowedInIdle, Is.GreaterThan(0));
        }

        [Test]
        public void AnEndTurnAsked_WhileARefusedCardSlidesHome_IsTakenOnceTheCardIsBack()
        {
            Rig rig = ToFirstWait(startGap: 0);
            rig.Run(1000);
            CardFace face = rig.Source.Frame.Hand.First(f => f.Aim == CardAim.Single && rig.Player.IsPlayable(f.Id));
            // A single-target card released above the throw line: WrongZone, the scene's line names 敵の上 (review 17).
            rig.Input(() => rig.Player.HandCallbacks.OnRelease(face.Id, DropZone.AboveThrowLine, -1));
            Assert.That(rig.Hand.Refusals.Last(), Is.EqualTo("「" + face.Name + "」は敵の上で離すと出せます"));
            Assert.That(rig.Hand.Refusals.Last(), Is.EqualTo(BattlePlayer.WrongZoneOnEnemy(face.Name)));
            Assert.That(rig.Hand.Returns.Last(), Is.EqualTo((face.Id, false)), "a card on the wrong zone just slides home");
            Assert.That(rig.Player.Returning, Is.True);
            Assert.That(rig.Player.State, Is.EqualTo(BattlePlayerState.Resolving));
            Assert.That(rig.Input(() => rig.Player.RequestEndTurn(queue: true, press: true)), Is.True, "queued");
            Assert.That(EndTurns(rig), Is.EqualTo(0));
            rig.Run(199);
            Assert.That(EndTurns(rig), Is.EqualTo(0));
            rig.Run(2);
            Assert.That(rig.Player.Returning, Is.False);
            Assert.That(EndTurns(rig), Is.EqualTo(1), "taken as soon as the card is home (CardReturn 200)");
        }

        [Test]
        public void ACardTheRulesRefuse_ShakesHome_WithTheSourcesLine()
        {
            Rig rig = ToFirstWait(startGap: 0);
            rig.Run(1000);
            // Spend the stamina until a card cannot be paid for.
            CardFace dear = null;
            for (int guard = 0; guard < 10 && dear == null; guard++)
            {
                dear = rig.Source.Frame.Hand.FirstOrDefault(f => rig.Source.Inspect(f.Id) == PlayVerdict.NotEnoughStamina);
                if (dear != null) break;
                CardFace cheap = rig.Source.Frame.Hand.FirstOrDefault(f => rig.Player.IsPlayable(f.Id));
                if (cheap == null) break;
                rig.Input(() => rig.Player.HandCallbacks.OnRelease(cheap.Id, DepictionText.RequiredZone(cheap.Aim), -1));
                Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle || rig.Player.State == BattlePlayerState.Over), Is.True);
                if (rig.Player.State == BattlePlayerState.Over) Assert.Inconclusive("the battle ended before a card was too dear");
            }
            Assume.That(dear, Is.Not.Null, "a card the stamina cannot pay for");
            rig.Input(() => rig.Player.HandCallbacks.OnRelease(dear.Id, DepictionText.RequiredZone(dear.Aim), -1));
            Assert.That(rig.Hand.Refusals.Last(), Is.EqualTo(rig.Source.RefusalText(dear.Id, PlayVerdict.NotEnoughStamina)));
            Assert.That(rig.Hand.Refusals.Last(), Does.StartWith("「" + dear.Name + "」はスタミナ "));
            Assert.That(rig.Hand.Returns.Last(), Is.EqualTo((dear.Id, true)), "refused by a rule on its own zone: it shakes");
        }

        [Test]
        public void AReleaseWhileTheFlowRuns_GoesHome_WithoutAskingTheSource()
        {
            var rig = new Rig(Rig.Launch(Enemies.PolearmWarpedId, BattleLaunch.DefaultSeed));
            rig.Start();
            rig.Run(50);
            Assert.That(rig.Player.State, Is.EqualTo(BattlePlayerState.Auto));
            CardFace face = rig.Source.Frame.Hand.FirstOrDefault() ?? new CardFace { Id = "x", Name = "x" };
            rig.Input(() => rig.Player.HandCallbacks.OnRelease(face.Id, DropZone.Receiver, -1));
            Assert.That(rig.Source.Calls.Any(c => c.StartsWith("TryPlay", StringComparison.Ordinal)), Is.False);
            Assert.That(rig.Hand.Returns.Last(), Is.EqualTo((face.Id, false)));
        }

        // ---- the drawer / journal refusals (review 16 / 17 / 21) -----------------------------------------

        [Test]
        public void ThePileListAndTheJournal_SayWhyACardIsNotTaken_AndTheLineGoesWithThem()
        {
            Rig rig = ToFirstWait();
            bool drawer = true;
            bool journal = false;
            rig.Player.SetDrawerCheck(() => drawer);
            rig.Player.SetJournalCheck(() => journal);
            rig.Player.RefreshPlayable();
            Assert.That(rig.Hand.Guide, Is.EqualTo(""), "the guide is quiet while the list is open (review 21)");
            rig.Input(() => rig.Player.RefuseWhileDrawer());
            Assert.That(rig.Hand.Refusals.Last(), Is.EqualTo(BattlePlayer.DrawerGuide));
            Assert.That(rig.Audio.Plays.Last().Name, Is.EqualTo("refusal"));
            drawer = false;
            rig.Player.OverlaysChanged(drawer, journal);
            Assert.That(rig.Hand.RefusalsHidden, Is.EqualTo(1), "the line that asked to close the list goes with it");
            journal = true;
            rig.Player.RefreshPlayable();
            Assert.That(rig.Hand.Guide, Is.EqualTo(BattlePlayer.JournalGuide));
            rig.Input(() => rig.Player.RefuseWhileJournal());
            Assert.That(rig.Hand.Refusals.Last(), Is.EqualTo(BattlePlayer.JournalGuide));
            journal = false;
            rig.Player.OverlaysChanged(drawer, journal);
            Assert.That(rig.Hand.RefusalsHidden, Is.EqualTo(2));
            rig.Player.RefreshPlayable();
            Assert.That(rig.Hand.Guide, Is.EqualTo(rig.Source.GuideText));
        }

        // ---- the one-press fast-forward (BattleScene.js:277-318, 459-462) on the player's counters ----------

        /// <summary>BattleScene.js's fast-forward arming, over the player's eventEnds / afterWaiting / onEventEnd / onState.</summary>
        private sealed class SceneFF
        {
            private readonly Rig _rig;
            public bool Armed;
            public int Until;
            public float ArmedAt = float.NegativeInfinity;
            public readonly List<(int Ends, bool On, float SinceArm)> AtEnds = new List<(int, bool, float)>();

            public SceneFF(Rig rig)
            {
                _rig = rig;
                rig.Player.OnEventEnd(() =>
                {
                    float since = _rig.Env.Real.Now - ArmedAt;
                    if (Armed && _rig.Player.EventEnds >= Until)
                    {
                        if (since < L.FfMinRunMs) Until = _rig.Player.EventEnds + 1;
                        else
                        {
                            Armed = false;
                            Update();
                        }
                    }
                    AtEnds.Add((_rig.Player.EventEnds, _rig.Env.Clock.FastForward, since));
                });
                rig.Player.OnState(s =>
                {
                    if (s == BattlePlayerState.Idle || s == BattlePlayerState.Over || s == BattlePlayerState.Halted)
                    {
                        Armed = false;
                        Until = 0;
                    }
                    Update();
                });
            }

            public void Arm()
            {
                if (!_rig.Player.CanFastForward()) return;
                if (!Armed) ArmedAt = _rig.Env.Real.Now;
                Armed = true;
                Until = Math.Max(Until, _rig.Player.EventEnds + (_rig.Player.AfterWaiting ? 2 : 1));
                Update();
            }

            private void Update() => _rig.Env.Clock.SetFastForward(Armed && _rig.Player.CanFastForward());
        }

        [Test]
        public void APressMidEvent_FastForwardsToTheEndOfThatEvent_OrTheNextWhenItRanUnder300ms()
        {
            Rig rig = ToFirstWait();
            rig.Run(600);
            var ff = new SceneFF(rig);
            Assert.That(rig.Input(() => rig.Player.RequestEndTurn()), Is.True);
            Assert.That(rig.Player.CanFastForward(), Is.True, "a play resolving can be hurried");
            rig.Run(20);
            Assert.That(rig.Player.AfterWaiting, Is.False);
            int k = rig.Player.EventEnds;
            rig.Input(() => ff.Arm());
            Assert.That(ff.Until, Is.EqualTo(k + 1), "a press mid-event runs to that event's end");
            Assert.That(rig.Env.Clock.FastForward, Is.True);
            rig.Env.Clock.Step(1f);
            Assert.That(rig.Env.Clock.Scale, Is.EqualTo(L.FfScale), "the scene runs ×4");
            Assert.That(rig.RunUntil(() => !ff.Armed || rig.Player.State == BattlePlayerState.Idle), Is.True);
            // It ended at the first event end at or past k + 1 once 300 real ms had run since the press.
            var end = ff.AtEnds.First(e => !e.On);
            Assert.That(end.Ends, Is.GreaterThanOrEqualTo(k + 1));
            Assert.That(end.SinceArm, Is.GreaterThanOrEqualTo(L.FfMinRunMs));
            Assert.That(ff.AtEnds.TakeWhile(e => e.On).All(e => e.Ends < k + 1 || e.SinceArm < L.FfMinRunMs), Is.True, "no earlier end stopped it");
        }

        [Test]
        public void APressInAnAfterWait_RunsToTheEndOfTheNextEvent()
        {
            Rig rig = ToFirstWait();
            rig.Run(600);
            var ff = new SceneFF(rig);
            Assert.That(rig.Input(() => rig.Player.RequestEndTurn()), Is.True);
            // The turn end's after-wait (250 ms, the player's own event).
            Assert.That(rig.RunUntil(() => rig.Player.AfterWaiting, 10_000f), Is.True);
            int k = rig.Player.EventEnds;
            rig.Input(() => ff.Arm());
            Assert.That(ff.Until, Is.EqualTo(k + 2), "pressed in an after-wait, it runs to the end of the next event (review 19)");
            Assert.That(rig.RunUntil(() => rig.Player.EventEnds == k + 1), Is.True);
            Assert.That(rig.Player.AfterWaiting, Is.False, "OnEventEnd comes after the after-wait");
            Assert.That(rig.Env.Clock.FastForward, Is.True, "still on through the next event");
            Assert.That(rig.RunUntil(() => !ff.Armed || rig.Player.State == BattlePlayerState.Idle), Is.True);
            Assert.That(ff.AtEnds.First(e => !e.On).Ends, Is.GreaterThanOrEqualTo(k + 2));
        }

        [Test]
        public void TheTurnOpening_DisarmsTheFastForward()
        {
            Rig rig = ToFirstWait();
            rig.Run(600);
            var ff = new SceneFF(rig);
            Assert.That(rig.Input(() => rig.Player.RequestEndTurn()), Is.True);
            rig.Input(() => ff.Arm());
            // Keep pressing through the whole enemy turn: it stays on until the player is asked again.
            Assert.That(rig.RunUntil(() =>
            {
                if (rig.Player.CanFastForward()) ff.Arm();
                return rig.Player.State == BattlePlayerState.Idle || rig.Player.State == BattlePlayerState.Over;
            }), Is.True);
            Assert.That(rig.Env.Clock.FastForward, Is.False);
            Assert.That(ff.Armed, Is.False);
            Assert.That(rig.Player.CanFastForward(), Is.False, "nothing to hurry in the player's wait");
        }

        [Test]
        public void TheAfterWait_IsTheLastPartOfEveryEvent_350Or250Or80()
        {
            Rig rig = BattlePlayerFlowTests.FightToTheEnd(Rig.Launch("twin_blade_warped", 11, 6));
            Assert.That(rig.Player.EventEnds, Is.EqualTo(rig.Source.Events.Count));
            // A rig with a listener that notes when each after-wait starts.
            var rig2 = new Rig(Rig.Launch("twin_blade_warped", 11, 6), GreedyAuto.Make());
            var waits = new List<float>();
            float startedAt = -1;
            rig2.Player.OnEventEnd(() =>
            {
                waits.Add(rig2.Env.Clock.Now - startedAt);
                startedAt = -1;
            });
            rig2.Start();
            Assert.That(rig2.RunUntil(() =>
            {
                if (rig2.Player.AfterWaiting && startedAt < 0) startedAt = rig2.Env.Clock.Now;
                return rig2.Finished > 0;
            }), Is.True);
            // The flag is noticed one frame after it rose (the step runs first): each wait is its length + 1 frame at the most.
            Assert.That(waits.All(w => new[] { EffectFlow.AfterAutoEventMs, EffectFlow.AfterPlayerEventMs, BattlePlayer.ContinuePauseMs }.Any(a => w >= a - 1 && w <= a)), Is.True,
                string.Join(", ", waits));
            Assert.That(waits.Any(w => w <= BattlePlayer.ContinuePauseMs), Is.True, "a two-blow action's 80 ms");
        }

        // ---- the result (player.js:2010-2081, 2293) ---------------------------------------------------------

        private static Rig ToResult()
        {
            var rig = new Rig(Rig.Launch(Enemies.PolearmWarpedId, BattleLaunch.DefaultSeed), GreedyAuto.Make());
            rig.Start();
            Assert.That(rig.RunUntil(() => rig.Hud.ResultUpAt >= 0), Is.True, "the result card rose");
            Assert.That(rig.Hud.ResultOutcome, Is.EqualTo(BattleOutcome.Won));
            return rig;
        }

        [Test]
        public void AWin_GoesOn_1200RealMsAfterTheCardRose_ExactlyOnce()
        {
            Rig rig = ToResult();
            Assert.That(rig.Player.State, Is.EqualTo(BattlePlayerState.Over));
            Assert.That(rig.Hud.SpeedVisible, Is.False);
            Assert.That(rig.Hud.Result.Lines[0], Is.EqualTo(BattleLayout.EnemyNameOf(Enemies.PolearmWarpedId) + "を倒した。"));
            Assert.That(rig.Hud.Result.Lines[1], Does.Match("^ターン [0-9]+ ・ 残りの HP [0-9]+ / [0-9]+$"));
            Assert.That(rig.Hud.Result.Title, Is.Null);
            rig.Run(L.ResultHoldMs - 1);
            Assert.That(rig.Finished, Is.EqualTo(0));
            rig.Run(2);
            Assert.That(rig.Finished, Is.EqualTo(1));
            rig.Input(() => rig.Player.SkipResult());
            rig.Input(() => rig.Hud.ResultButton());
            rig.Run(5000);
            Assert.That(rig.Finished, Is.EqualTo(1), "ctx.OnFinished once");
        }

        [Test]
        public void ASkip_CountsOnlyAfter400ms_AndAfterA350msPause()
        {
            Rig rig = ToResult();
            rig.Run(200);
            rig.Input(() => rig.Player.SkipResult());
            Assert.That(rig.Finished, Is.EqualTo(0), "the card has not stood 400 ms");
            rig.Run(250);
            rig.Input(() => rig.Player.SkipResult());
            Assert.That(rig.Finished, Is.EqualTo(0), "250 ms after the last press: no pause");
            rig.Run(400);
            rig.Input(() => rig.Player.SkipResult());
            Assert.That(rig.Finished, Is.EqualTo(1), "after a 350 ms pause, 850 ms after the card rose");
        }

        [Test]
        public void AMashOfClicks_NeverSkipsTheResult_TheHoldRunsOut()
        {
            Rig rig = ToResult();
            for (int i = 0; i < 11; i++)
            {
                rig.Run(100);
                rig.Input(() => rig.Player.SkipResult());
                Assert.That(rig.Finished, Is.EqualTo(0), "press " + i);
            }
            rig.Run(L.ResultHoldMs - 1100 + 1);
            Assert.That(rig.Finished, Is.EqualTo(1));
        }

        [Test]
        public void TheHold_CountsOnlyTimeNoOverlayHeldTheScene()
        {
            Rig rig = ToResult();
            bool overlay = true;
            rig.Player.SetOverlayCheck(() => overlay);
            rig.Run(5000);
            Assert.That(rig.Finished, Is.EqualTo(0), "the journal holds the result");
            overlay = false;
            rig.Run(L.ResultHoldMs + 60);
            Assert.That(rig.Finished, Is.EqualTo(1));
        }

        [Test]
        public void ALoss_FallsInkInksAndWaits_ForTheButton()
        {
            var rig = new Rig(Rig.Launch(Enemies.PolearmWarpedId, BattleLaunch.DefaultSeed, 6, startHp: 1, startGap: 0));
            rig.Start();
            Assert.That(rig.RunUntil(() =>
            {
                if (rig.Player.State == BattlePlayerState.Idle) rig.Input(() => rig.Player.RequestEndTurn());
                return rig.Hud.ResultUpAt >= 0;
            }), Is.True, "the battle is lost and the result screen rose");
            Assert.That(rig.Hud.ResultOutcome, Is.EqualTo(BattleOutcome.Lost));
            Assert.That(rig.Field.Falls.Any(f => f.Side == UnitSide.Player), Is.True, "the hero falls (the script never marks the player Down)");
            Assert.That(rig.Hud.LossWords, Is.EqualTo(new[] { BattlePlayer.LossWord }));
            ResultInfo info = rig.Hud.Result;
            Assert.That(info.Title, Is.EqualTo("第 1 層で倒れた"));
            Assert.That(info.Lines, Is.EqualTo(new[] { "残 HP 0 / " + rig.Source.Frame.Player.HpMax }));
            Assert.That(info.BarsLabel, Is.EqualTo("出した札"));
            Assert.That(info.Bars.Select(b => b.Label), Is.EqualTo(new[] { "攻撃", "防御", "スキル", "スタンス" }));
            Assert.That(info.Bars.Select(b => b.Color), Is.EqualTo(new[] { "var(--violet)", "var(--guard)", "var(--amber)", "var(--crimson)" }));
            Assert.That(info.Bars.Sum(b => b.Value), Is.EqualTo(0), "no card was played");
            Assert.That(info.Stats, Is.EqualTo(new[] { "特性の発動 0 回", rig.Source.Frame.Corner.Turn + " ターン戦った" }));
            Assert.That(info.Button, Is.EqualTo("手記へ"));
            rig.Run(20_000);
            Assert.That(rig.Finished, Is.EqualTo(0), "a loss waits for 「手記へ」");
            rig.Input(() => rig.Hud.ResultButton());
            Assert.That(rig.Finished, Is.EqualTo(1));
        }

        [Test]
        public void ALoss_UnderAutoPlay_GoesOnAfterTheHold()
        {
            var rig = new Rig(Rig.Launch(Enemies.PolearmWarpedId, BattleLaunch.DefaultSeed, 6, startHp: 1, startGap: 0), GreedyAuto.Make(endTurnsOnly: true));
            rig.Start();
            Assert.That(rig.RunUntil(() => rig.Hud.ResultUpAt >= 0), Is.True);
            Assert.That(rig.Hud.ResultOutcome, Is.EqualTo(BattleOutcome.Lost));
            rig.Run(L.ResultHoldMs + 1);
            Assert.That(rig.Finished, Is.EqualTo(1), "the dev page's auto play does not wait for the button");
        }

        // ---- several enemies: the Tab at rest, the target a release names, the preview (§3.4, review 19 / fix) ----

        [Test]
        public void WithTwoEnemies_TabAtRestMovesTheFloorsN_AReleaseNamesItsEnemy_AndThePreviewIsTheWick()
        {
            var rig = new Rig(Rig.Launch(BattlePlayerFlowTests.Pair[0], BattlePlayerFlowTests.PairSeed, BattlePlayerFlowTests.PairCells,
                BattlePlayerFlowTests.Pair.Skip(1).ToList()));
            rig.Start();
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle), Is.True);
            Assert.That(rig.Player.RestingUnit, Is.EqualTo(-1), "at rest the floor's N is the nearest enemy's");
            Assert.That(rig.Player.CycleGapFocus(1), Is.True);
            int first = rig.Player.RestingUnit;
            Assert.That(first, Is.AnyOf(0, 1));
            Assert.That(rig.Field.GapsShown.Last(), Is.EqualTo(first), "the floor's N moved to the Tab's enemy");
            Assert.That(rig.Player.CycleGapFocus(1), Is.True);
            Assert.That(rig.Player.RestingUnit, Is.EqualTo(1 - first), "and on to the other one");
            Assert.That(rig.Player.CycleGapFocus(-1), Is.True);
            Assert.That(rig.Player.RestingUnit, Is.EqualTo(first), "Shift+Tab goes back");

            CardFace blow = null;
            int unit = -1;
            foreach (int k in new[] { 1, 0 })
            {
                blow = rig.Source.Frame.Hand.FirstOrDefault(f => f.Aim == CardAim.Single && rig.Source.Inspect(f.Id, k) == PlayVerdict.Accepted
                    && f.Values.Any(v => v.Kind == CardValueKind.Power) && f.HpLossOn.Count > k);
                unit = k;
                if (blow != null) break;
            }
            Assert.That(blow, Is.Not.Null, "a blow one of the enemies can take now");
            string raw = rig.Source.PreviewFor(blow.Id, unit);
            Assert.That(raw, Is.Not.Empty);
            Assert.That(rig.Player.HandCallbacks.Preview(blow.Id, unit), Is.EqualTo(blow.HpLossFor(unit).ToString()), "the tag shows the HP after the Guard (the wick's number)");
            rig.Input(() => rig.Player.HandCallbacks.OnRelease(blow.Id, DropZone.Receiver, unit));
            Assert.That(rig.Source.Calls.Last(), Is.EqualTo("TryPlay:" + blow.Id + ":" + DropZone.Receiver + ":" + unit), "the source hears the enemy the card was released on");
            Assert.That(rig.Player.AcceptedPlays, Is.EqualTo(1));
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle || rig.Player.State == BattlePlayerState.Over), Is.True);
            Assume.That(rig.Player.State, Is.EqualTo(BattlePlayerState.Idle));
            Assert.That(rig.Input(() => rig.Player.RequestEndTurn()), Is.True);
            Assert.That(rig.Player.RestingUnit, Is.EqualTo(-1), "the turn's end gives the N back to the nearest enemy");
        }

        // ---- speed, halt ----------------------------------------------------------------------------------

        [Test]
        public void TheSpeedSwitch_CyclesTheSettings_AndTellsTheApp()
        {
            var settings = new BattleSettings { Speed = 1.25 };
            var rig = new Rig(Rig.Launch(Enemies.PolearmWarpedId, BattleLaunch.DefaultSeed), null, null, settings);
            var told = new List<double>();
            rig.Ctx.OnSettingsChange = s => told.Add(s.Speed);
            rig.Start();
            Assert.That(rig.Hud.SpeedLabel, Is.EqualTo(BattleSpeed.Label(BattleSpeedStep.Normal)));
            rig.Input(() => rig.Player.CycleSpeed());
            Assert.That(settings.Speed, Is.EqualTo(1.5));
            Assert.That(rig.Hud.SpeedLabel, Is.EqualTo(BattleSpeed.Label(BattleSpeedStep.Fast)));
            Assert.That(rig.Hud.SpeedChevrons, Is.EqualTo(BattleSpeed.Chevrons(BattleSpeedStep.Fast)));
            rig.Input(() => rig.Player.CycleSpeed());
            rig.Input(() => rig.Player.CycleSpeed());
            Assert.That(told, Is.EqualTo(new[] { 1.5, 1.0, 1.25 }));
            Assert.That(rig.Audio.Plays.Count(p => p.Name == "uiClick"), Is.EqualTo(3));
            rig.Env.Clock.Step(10f);
            Assert.That(rig.Env.Clock.Scale, Is.EqualTo(1f), "the clock reads the speed live");
        }

        [Test]
        public void Halt_StopsInputAndEvents()
        {
            Rig rig = ToFirstWait();
            rig.Input(() => rig.Player.Halt());
            Assert.That(rig.Hand.Interactable, Is.False);
            Assert.That(rig.Player.CanEndTurn(), Is.False);
            rig.Run(1000);
            Assert.That(rig.Input(() => rig.Player.RequestEndTurn()), Is.False);
            Assert.That(EndTurns(rig), Is.EqualTo(0));
        }

        [Test]
        public void Destroy_StopsTheFlow_WhereItStands()
        {
            var rig = new Rig(Rig.Launch(Enemies.PolearmWarpedId, BattleLaunch.DefaultSeed), GreedyAuto.Make());
            rig.Start();
            rig.Run(100);
            int calls = rig.Source.Calls.Count;
            rig.Player.Destroy();
            rig.Env.Clock.Destroy();
            rig.Run(60_000);
            Assert.That(rig.Source.Calls.Count, Is.EqualTo(calls), "a destroyed scene never reaches the source again");
            Assert.That(rig.Finished, Is.EqualTo(0));
        }
    }
}
