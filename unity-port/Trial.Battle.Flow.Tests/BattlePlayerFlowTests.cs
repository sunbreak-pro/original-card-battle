// dotnet-only: BattlePlayer (port of scenes/battle/player.js) playing whole battles of a real CoreBattleSource through
// fake hand / field / hud modules whose beats resolve after the ms they are handed (battle-flow §4.6). player.js has no
// JS unit test of its own (the JS checks it with the dev playtest, dev/battle-autoplay.mjs); these hold what
// docs/BATTLE_SCENE.md §4 describes: the plan's waits are held exactly (no EffectPlan mismatch), each event lasts its
// blocking waits plus its after-wait, a two-blow action steps in once, two enemies are played one after the other, and
// every state-changing call goes to ctx.Source as it stands.
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
    public class BattlePlayerFlowTests
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

        internal static readonly string[] Pair = { "polearm_warped", "crossbow_hunter" };
        internal const int PairCells = 7;
        /// <summary>A seed the greedy player wins the pair on.</summary>
        internal const int PairSeed = 1;
        /// <summary>A seed on which both enemies act in one enemy turn (the pause between them shows).</summary>
        internal const int PairPauseSeed = 3;

        /// <summary>Plays the battle to the end with the greedy chooser; returns the rig after ctx.OnFinished.</summary>
        internal static Rig FightToTheEnd(ITrialSource source, int maxPlays = 4)
        {
            var rig = new Rig(source, GreedyAuto.Make(maxPlays));
            rig.Start();
            Assert.That(rig.RunUntil(() => rig.Finished > 0, 3_600_000f), "the battle ends within an hour of scene time");
            return rig;
        }

        // ---- plan errors and the end --------------------------------------------------------------------

        [Test]
        public void OneEnemy_PlaysToTheEnd_WithNoPlanMismatch()
        {
            Rig rig = FightToTheEnd(Rig.Launch(Enemies.PolearmWarpedId, BattleLaunch.DefaultSeed));
            Assert.That(rig.Player.PlanErrors, Is.EqualTo(0), string.Join("\n", rig.Log.Errors));
            Assert.That(rig.Log.Errors, Is.Empty);
            Assert.That(rig.Player.State, Is.EqualTo(BattlePlayerState.Over));
            Assert.That(rig.Finished, Is.EqualTo(1), "ctx.OnFinished is called exactly once");
            Assert.That(rig.Hud.ResultOutcome, Is.EqualTo(rig.Source.Frame.Outcome));
            Assert.That(rig.Source.Calls.Count(c => c.StartsWith("TryPlay", StringComparison.Ordinal)), Is.GreaterThan(0), "cards were played");
            Assert.That(rig.Source.Calls.Count(c => c == "EndTurn"), Is.GreaterThan(0), "turns were ended");
            // Every event the source handed out was played out (one OnEventEnd each).
            Assert.That(rig.EventEnds.Count, Is.EqualTo(rig.Source.Events.Count));
        }

        [Test]
        public void TwoEnemies_FightToAWin_WithNoPlanMismatch()
        {
            CoreBattleSource source = Rig.Launch(Pair[0], PairSeed, PairCells, Pair.Skip(1).ToList());
            Rig rig = FightToTheEnd(source);
            Assert.That(rig.Source.Frame.Outcome, Is.EqualTo(BattleOutcome.Won), "the greedy player wins the pair on this seed");
            Assert.That(rig.Player.PlanErrors, Is.EqualTo(0), string.Join("\n", rig.Log.Errors));
            Assert.That(rig.Log.Errors, Is.Empty);
            Assert.That(rig.Finished, Is.EqualTo(1));
            // The cards aimed at one enemy named it (only a battle of several sends a target to the source).
            Assert.That(rig.Source.Calls.Any(c => c.StartsWith("TryPlay", StringComparison.Ordinal) && c.Count(ch => ch == ':') == 3), Is.True);
            // Both enemies fell; with the other one standing, the first fell with its foot light kept (§5.15 順 5).
            Assert.That(rig.Field.Falls.Count(f => f.Side == UnitSide.Enemy), Is.EqualTo(2));
            Assert.That(rig.Field.Falls.Count(f => f.Side == UnitSide.Enemy && f.KeepFoot), Is.EqualTo(1));
            Assert.That(rig.Hud.Result.Lines[0], Does.EndWith("を倒した。"));
            Assert.That(rig.Hud.Result.Lines[0], Does.Contain(BattleLayout.EnemyNameOf(Pair[0])));
            Assert.That(rig.Hud.Result.Lines[0], Does.Contain(BattleLayout.EnemyNameOf(Pair[1])));
            // The log names each enemy by its roster name (review 21), never 敵 alone.
            Assert.That(rig.Player.LogLines().Any(l => l.StartsWith(BattleLayout.EnemyNameOf(Pair[1]) + ": ", StringComparison.Ordinal)), Is.True);
        }

        // ---- event lengths --------------------------------------------------------------------------------

        [TestCase("polearm_warped", 20260921, 6)]
        [TestCase("twin_blade_warped", 11, 6)]
        public void OneEnemy_EveryEventLasts_ItsPlanWaits_PlusItsAfterWait(string enemy, int seed, int cells)
        {
            Rig rig = FightToTheEnd(Rig.Launch(enemy, seed, cells));
            AssertEventLengths(rig, multi: false);
        }

        [Test]
        public void TwoEnemies_EveryEventLasts_ItsPlanWaits_PlusItsAfterWait_AndThePauseBetweenEnemies()
        {
            Rig rig = FightToTheEnd(Rig.Launch(Pair[0], PairPauseSeed, PairCells, Pair.Skip(1).ToList()));
            int pauses = AssertEventLengths(rig, multi: true);
            Assert.That(pauses, Is.GreaterThan(0), "the 600 ms between two enemies' phases (§3.11) was held");
        }

        /// <summary>The whole roster one by one, and the three-enemy line (dev/battle-autoplay.mjs's sweep): no plan mismatch, every event its length.</summary>
        [Test]
        public void TheWholeRoster_AndTheTrio_PlayWithNoPlanMismatch_AndEveryEventItsLength()
        {
            var fights = Enemies.All.Select(d => (Id: d.Id, More: (List<string>)null, Cells: 6)).ToList();
            fights.Add((Pair[0], new List<string> { Pair[1], "shadow_hound" }, 8));
            foreach (var (id, more, cells) in fights)
            {
                Microtasks.Reset();
                Rig rig = FightToTheEnd(Rig.Launch(id, 1, cells, more));
                string label = id + (more != null ? "+" + string.Join("+", more) : "");
                Assert.That(rig.Player.PlanErrors, Is.EqualTo(0), label + ": " + string.Join("\n", rig.Log.Errors));
                Assert.That(rig.Log.Errors, Is.Empty, label);
                Assert.That(rig.Player.State, Is.EqualTo(BattlePlayerState.Over), label);
                AssertEventLengths(rig, multi: more != null);
            }
        }

        /// <summary>
        /// Each event's scene time — from the call that took it (or the end of the event before, for one fetched early) to
        /// its OnEventEnd — against EffectPlan.BlockingMs plus its after-wait (350 for an automatic event, 250 for one the
        /// player asked for), with the scene's own holds: the 250 ms that makes the pause between two enemies 600, a fall
        /// with others standing (+300 foot +300 grey), and a two-blow action held as one (§5.12: the spent omen and the step
        /// back wait for the action's end, 80 ms between its events, its second step in not taken). Returns the pauses seen.
        /// </summary>
        private static int AssertEventLengths(Rig rig, bool multi)
        {
            var events = rig.Source.Events;
            Assert.That(rig.EventEnds.Count, Is.EqualTo(events.Count));
            var switches = EffectSwitches.AllOn();
            int? lastEnemy = null;
            int pauses = 0;
            string heldTitle = null;
            int heldUnit = -1;
            bool prevHeld = false;
            float prevEnd = float.NegativeInfinity;
            var mismatches = new List<string>();
            for (int i = 0; i < events.Count; i++)
            {
                var (ev, call, at) = events[i];
                bool auto = call == "AdvanceAuto";
                float w = 0f;
                if (auto)
                {
                    // BattlePlayer.StartsOtherEnemy (player.js:908).
                    if (ev.Kind != DepictionEventKind.EnemyAction && ev.Kind != DepictionEventKind.NextOmen)
                    {
                        if (ev.Kind != DepictionEventKind.Defeat) lastEnemy = null;
                    }
                    else if (ev.Unit >= 0 && multi)
                    {
                        if (lastEnemy != null && ev.Unit != lastEnemy.Value)
                        {
                            w = L.BetweenEnemiesMs - EffectFlow.AfterAutoEventMs;
                            pauses++;
                        }
                        lastEnemy = ev.Unit;
                    }
                }
                float b = EffectPlan.BlockingMs(ev, switches);
                foreach (Cue c in ev.Cues)
                    if (c.Kind == CueKind.Defeat && c.Target == UnitSide.Enemy && multi && ev.After.Enemies.Where((e, k) => !e.Unit.Down && k != c.Unit).Any())
                        b += L.FallFootMs + L.FallGreyMs;
                bool continues = prevHeld && auto && Continues(heldTitle, heldUnit, ev);
                bool held = IsHeld(ev);
                if (continues)
                {
                    Cue blow = ev.Cues.FirstOrDefault(c => c.Kind == CueKind.Slash && EffectPlan.AttackerOf(c) == UnitSide.Enemy);
                    if (blow != null) b -= switches.WaitMs(EffectId.EnemyLunge);
                }
                float a;
                string title = continues ? heldTitle : ev.Title;
                int unit = ev.Unit;
                if (held)
                {
                    b -= switches.WaitMs(EffectId.OmenSpend) + switches.WaitMs(EffectId.EnemyReturn);
                    bool nextContinues = i + 1 < events.Count && events[i + 1].Call == "AdvanceAuto" && Continues(title, HeldUnitOf(ev), events[i + 1].Ev);
                    if (nextContinues) a = BattlePlayer.ContinuePauseMs;
                    else
                    {
                        bool away = continues || ev.Cues.Any(c => c.Kind == CueKind.EnemyWindup || (c.Kind == CueKind.Slash && EffectPlan.AttackerOf(c) == UnitSide.Enemy));
                        a = EffectFlow.AfterAutoEventMs + switches.WaitMs(EffectId.OmenSpend) + (away ? switches.WaitMs(EffectId.EnemyReturn) : 0f);
                    }
                }
                else
                {
                    a = auto ? EffectFlow.AfterAutoEventMs : EffectFlow.AfterPlayerEventMs;
                    // The held action ends with this event (player.js:1014): its badge is spent (200, held); its step back
                    // already started beside the flow at the event's end (EnemyReturn 140 < 200), so it adds no wait.
                    if (continues) a += switches.WaitMs(EffectId.OmenSpend);
                }
                prevHeld = held;
                heldTitle = held ? title : null;
                heldUnit = held ? HeldUnitOf(ev) : -1;
                float start = Math.Max(at, prevEnd);
                float end = rig.EventEnds[i].At;
                float expected = w + b + a;
                if (Math.Abs(end - start - expected) > 0.01f)
                    mismatches.Add("event " + ev.Order + " " + ev.Kind + " 「" + ev.Title + "」 (" + call + "): " + (end - start) + " ms, the plan says " + expected
                        + " (pause " + w + ", blocking " + b + ", after " + a + ")");
                prevEnd = end;
            }
            Assert.That(mismatches, Is.Empty, string.Join("\n", mismatches));
            return pauses;
        }

        /// <summary>An enemy action whose last cue is its blow on the player (player.js:1313): its end is held.</summary>
        private static bool IsHeld(DepictionEvent ev)
        {
            if (ev.Kind != DepictionEventKind.EnemyAction || ev.Cues.Count == 0) return false;
            Cue last = ev.Cues[ev.Cues.Count - 1];
            return last.Kind == CueKind.Hit && last.Target == UnitSide.Player;
        }

        private static int HeldUnitOf(DepictionEvent ev)
        {
            Cue last = ev.Cues[ev.Cues.Count - 1];
            return last.Unit >= 0 ? last.Unit : -1;
        }

        /// <summary>player.js:922 continuesHeld.</summary>
        private static bool Continues(string title, int unit, DepictionEvent ev)
        {
            if (string.IsNullOrEmpty(title) || ev.Kind != DepictionEventKind.EnemyAction) return false;
            int u = ev.Unit >= 0 ? ev.Unit : -1;
            if (u >= 0 && unit >= 0 && u != unit) return false;
            return (ev.Title ?? "").StartsWith(title + "（", StringComparison.Ordinal);
        }

        // ---- the two-blow action (§5.12, review 21) ---------------------------------------------------------

        [Test]
        public void TwoBlowAction_StepsInOnce_AndBackOnce_WithTheBadgeLitThroughout()
        {
            Rig rig = FightToTheEnd(Rig.Launch("twin_blade_warped", 11, 6));
            Assert.That(rig.Player.PlanErrors, Is.EqualTo(0), string.Join("\n", rig.Log.Errors));
            var events = rig.Source.Events.Select(e => e.Ev).ToList();
            int seconds = 0;
            for (int i = 1; i < events.Count; i++)
            {
                DepictionEvent second = events[i];
                if (second.Kind != DepictionEventKind.EnemyAction || !second.Title.EndsWith("（2 撃目）", StringComparison.Ordinal)) continue;
                DepictionEvent first = events[i - 1];
                Assert.That(second.Title, Does.StartWith(first.Title + "（"), "the second blow's event follows its first");
                seconds++;
                float from = i >= 2 ? rig.EventEnds[i - 2].At : 0f;
                float to = rig.EventEnds[i].At;
                int lunges = rig.Field.Lunges.Count(l => l.Side == UnitSide.Enemy && l.At >= from && l.At <= to);
                int returns = rig.Field.Returns.Count(r => r.Side == UnitSide.Enemy && r.At >= from && r.At <= to);
                Assert.That(lunges, Is.EqualTo(1), "one step in for the two blows (「踏み込みと止めは 1 回」)");
                Assert.That(returns, Is.EqualTo(1), "one step back once the action is over");
                // The returns come after the second blow's event, not between the two.
                Assert.That(rig.Field.Returns.Any(r => r.Side == UnitSide.Enemy && r.At > from && r.At < rig.EventEnds[i - 1].At), Is.False);
            }
            Assert.That(seconds, Is.GreaterThan(0), "the fight showed 二段斬り at least once");
        }

        // ---- ctx.Source, read live ----------------------------------------------------------------------

        [Test]
        public void EveryStateChangingCall_GoesToCtxSource_AsItStandsNow()
        {
            CoreBattleSource core = Rig.Launch(Enemies.PolearmWarpedId, BattleLaunch.DefaultSeed);
            var rig = new Rig(core, GreedyAuto.Make());
            rig.Start();
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle), Is.True);
            int before = rig.Source.Calls.Count;
            // The save recorder may be swapped in mid-fight (a reload): the next call goes to the new one.
            var second = new RecordingSource(core, () => rig.Env.Clock.Now);
            rig.Ctx.Source = second;
            Assert.That(rig.RunUntil(() => rig.Finished > 0), Is.True);
            Assert.That(rig.Source.Calls.Count, Is.EqualTo(before), "no call reached the old source after the swap");
            Assert.That(second.Calls.Count, Is.GreaterThan(0));
            Assert.That(rig.Player.PlanErrors, Is.EqualTo(0));
        }

        [Test]
        public void ASourceThatThrows_HaltsTheBattle_AndTellsTheApp()
        {
            var rig = new Rig(Rig.Launch(Enemies.PolearmWarpedId, BattleLaunch.DefaultSeed), GreedyAuto.Make());
            var boom = new InvalidOperationException("boom");
            rig.Source.FailNext = boom;
            var states = new List<BattlePlayerState>();
            rig.Player.OnState(s => states.Add(s));
            rig.Start();
            rig.Run(2000);
            Assert.That(rig.Player.State, Is.EqualTo(BattlePlayerState.Halted));
            Assert.That(states, Is.EqualTo(new[] { BattlePlayerState.Auto, BattlePlayerState.Halted }));
            Assert.That(rig.SourceErrors, Is.EqualTo(new[] { boom }));
            Assert.That(rig.Log.Errors.Single(), Does.StartWith("[battle] the source failed; the battle stops here:"));
            Assert.That(rig.Hand.Interactable, Is.False);
            Assert.That(rig.Player.RequestEndTurn(), Is.False, "nothing more reaches the source");
            Assert.That(rig.Finished, Is.EqualTo(0));
        }

        // ---- sounds ------------------------------------------------------------------------------------------

        private static readonly HashSet<string> Budgeted = new HashSet<string>
        {
            "hitSlash", "hitThrust", "hitBlunt", "hitBody", "hitGuard", "guardBreak", "guardUp", "seal", "stance", "breath", "reserve",
        };

        [Test]
        public void EachEvent_SoundsAtMostTwo_OfItsResultSounds_AndAPlayedCardAtMostOne()
        {
            Rig rig = FightToTheEnd(Rig.Launch("twin_blade_warped", 11, 6));
            var byEvent = rig.Audio.Plays.Where(p => Budgeted.Contains(p.Name)).GroupBy(p => p.Event).ToList();
            Assert.That(byEvent, Is.Not.Empty);
            foreach (var g in byEvent)
            {
                // The event that played while EventEnds was g.Key is events[g.Key] (0-based).
                string call = g.Key < rig.Source.Events.Count ? rig.Source.Events[g.Key].Call : "?";
                int cap = call == "TryPlay" ? 1 : 2;
                Assert.That(g.Count(), Is.LessThanOrEqualTo(cap), "event " + g.Key + " (" + call + "): " + string.Join(", ", g.Select(p => p.Name)));
            }
            // An enemy's blow on the player always sounds (its system's hit is essential).
            Assert.That(rig.Audio.Plays.Any(p => p.Name == "hitSlash" || p.Name == "hitThrust" || p.Name == "hitBlunt"), Is.True);
            Assert.That(rig.Audio.Plays.Count(p => p.Name == "foeTurn"), Is.EqualTo(rig.Source.Calls.Count(c => c == "EndTurn")), "foeTurn once per turn end");
        }

        // ---- 俊敏's free step (§3.8) ----------------------------------------------------------------------------

        private static readonly CardDef Quicken = new CardDef("quicken", "身軽", BattleAttribute.Skill, 1,
            new Face(Statuses: new[] { new StatusGrant(StatusKind.Swift, 2, true) }), null, TargetKind.Self, "");

        private static CoreBattleSource SwiftSource()
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < 6; i++) deck.Add(new CardInstance("quicken-" + i, Quicken));
            IReadOnlyList<CardDef> kinds = PrototypeDeck.Kinds;
            for (int i = 0; deck.Count < 20; i++) deck.Add(new CardInstance("p-" + i, kinds[i % kinds.Count]));
            return new CoreBattleSource(new BattleSetup(Enemies.PolearmWarped, deck, 7), 7);
        }

        [Test]
        public void FreeStep_IsOffered_AtTheTurnStart_LocksTheCards_AndPlaysThroughTheSource()
        {
            var rig = new Rig(SwiftSource());
            rig.Start();
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle), Is.True);
            // Turn 1: play 身軽 (俊敏 2) and end the turn, by hand.
            CardFace quicken = rig.Source.Frame.Hand.First(f => f.Id.StartsWith("quicken", StringComparison.Ordinal));
            rig.Input(() => rig.Player.HandCallbacks.OnRelease(quicken.Id, DepictionText.RequiredZone(quicken.Aim), -1));
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle), Is.True);
            rig.Run(2000);
            Assert.That(rig.Input(() => rig.Player.RequestEndTurn()), Is.True);
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle && rig.Player.FreeStepPending), Is.True, "turn 2 opens 俊敏's choice");
            Assert.That(rig.Field.FreeShown, Is.True);
            Assert.That(rig.Hand.Guide, Is.EqualTo(BattlePlayer.FreeStepGuide));
            Assert.That(rig.Hand.GrabLock, Is.Not.Null, "no card is taken until a mark is chosen");
            // A card tried meanwhile: the refusal line, and the sound.
            rig.Input(() => rig.Hand.GrabLock());
            Assert.That(rig.Hand.Refusals.Last(), Is.EqualTo(BattlePlayer.FreeStepRefusal));
            Assert.That(rig.Audio.Plays.Last().Name, Is.EqualTo("refusal"));
            // → picks forward (the key of a way always picks it); Enter chooses.
            FreeStepWays ways = rig.Player.FreeStepWays.Value;
            int dir = ways.Forward ? 1 : -1;
            Assert.That(rig.Input(() => rig.Player.MoveFreeStepFocus(dir)), Is.True);
            Assert.That(rig.Field.FreeStepFocus, Is.EqualTo(dir));
            Assert.That(rig.Input(() => rig.Player.ConfirmFreeStep()), Is.True);
            Assert.That(rig.Source.Calls.Last(), Is.EqualTo("TakeFreeStep:" + dir));
            Assert.That(rig.Hand.GrabLock, Is.Null);
            Assert.That(rig.Hand.RefusalsHidden, Is.GreaterThan(0), "the choice's refusal line goes with the choice");
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle), Is.True);
            Assert.That(rig.Player.FreeStepPending, Is.False, "asked once a turn");
            Assert.That(rig.Player.PlanErrors, Is.EqualTo(0), string.Join("\n", rig.Log.Errors));
            Assert.That(rig.Player.LogLines(), Has.Some.EqualTo("自分: " + CoreBattleSource.FreeStepTitle));
        }

        // ---- the chain's opening (§2.5 / §5.18) -------------------------------------------------------------

        [Test]
        public void AChainsSecondFight_OpensOnTheBoard_ThenTheTurnStarts()
        {
            var rig = new Rig(Rig.Launch(Enemies.PolearmWarpedId, BattleLaunch.DefaultSeed, chainIndex: 2, chainTotal: 3), null, new BattleChain(2, 3));
            rig.Start();
            Assert.That(rig.Hud.ChainBanners.Single().Title, Is.EqualTo("2 戦目"));
            Assert.That(rig.Hud.ChainBanners.Single().Name, Is.EqualTo(BattleLayout.EnemyNameOf(Enemies.PolearmWarpedId)));
            Assert.That(rig.Source.Calls, Is.Empty, "the turn start waits for the board");
            rig.Run(1399);
            Assert.That(rig.Source.Calls, Is.Empty);
            rig.Run(2);
            Assert.That(rig.Source.Calls, Is.EqualTo(new[] { "AdvanceAuto" }));
        }
    }
}
