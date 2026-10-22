// dotnet-only: BattlePlayer's beats (port of scenes/battle/player.js) on scripted battles (DepictionRunner), for what the
// whole-battle tests do not pin down: the listener sets walked as a JS Set, the turn start's heartbeat and its omen blink
// after the draw (120 apart), the enemy's-turn shade held to the result, the ×1.5 脆化 label (脆化 first), an HpChange's
// typographic minus on the float and the JS's ASCII sign in the log, the log's 400 lines, the plate's one flash when
// nothing is playable, a stance card that waits at its list until its ghost leaves for the exile, the lean of a move cut
// short, a wall the player is shoved into, and a pull's 「引き寄せ」.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction;
using NUnit.Framework;
using Trial.Battle.Flow.Tests.Fakes;

namespace Trial.Battle.Flow.Tests
{
    public class BattlePlayerBeatTests
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

        private static readonly EffectSwitches Switches = EffectSwitches.AllOn();

        private static DepictionFrame Frame(int playerHp = 50, int enemyHp = 40, int turn = 1)
        {
            return new DepictionFrame
            {
                Corner = new CornerFrame { Turn = turn, Floor = 1, ChainIndex = 1, ChainTotal = 1 },
                Player = new UnitFrame { Hp = playerHp, HpMax = 50 },
                Enemy = new UnitFrame { Hp = enemyHp, HpMax = 40, ArtId = Enemies.PolearmWarpedId },
            };
        }

        private static DepictionFrame WithHand(DepictionFrame f, params CardFace[] hand)
        {
            f.Hand = hand.ToList();
            return f;
        }

        private static DepictionEvent TurnStart(int order, DepictionFrame after, params Cue[] cues) =>
            new DepictionEvent { Order = order, Kind = DepictionEventKind.TurnStart, After = after, Cues = cues.ToList() };

        private static DepictionEvent EnemyAction(int order, string title, DepictionFrame after, params Cue[] cues) =>
            new DepictionEvent { Order = order, Kind = DepictionEventKind.EnemyAction, Title = title, After = after, Cues = cues.ToList() };

        private static DepictionEvent PlayCard(int order, CardFace face, DepictionFrame after, params Cue[] cues) =>
            new DepictionEvent { Order = order, Kind = DepictionEventKind.PlayCard, Title = face.Name, CardId = face.Id, Aim = face.Aim, After = after, Cues = cues.ToList() };

        private static CardFace Card(string id, CardKind kind, CardAim aim, CardValueKind value, string number = "1") => new CardFace
        {
            Id = id, Name = id, Cost = 1, Kind = kind, Aim = aim,
            Values = new List<CardValue> { new CardValue { Kind = value, Number = number, Word = "" } },
        };

        private static Rig Scripted(DepictionScript script, BattleSettings settings = null)
        {
            var rig = new Rig(new DepictionRunner(script), null, null, settings);
            rig.Start();
            return rig;
        }

        // ---- the listener sets (player.js:302-317, 360-364: JS Sets walked with forEach) -----------------------

        [Test]
        public void TheStateListeners_AreWalkedAsAJsSet_OneAddedMeanwhileIsCalled_OneDeletedBeforeItsTurnIsNot()
        {
            var script = new DepictionScript { Opening = Frame() };
            script.Events.Add(TurnStart(1, Frame()));
            var rig = new Rig(new DepictionRunner(script));
            var calls = new List<string>();
            Action offC = null;
            rig.Player.OnState(s =>
            {
                calls.Add("A " + s);
                if (s != BattlePlayerState.Auto) return;
                offC();
                rig.Player.OnState(x => calls.Add("B " + x));
            });
            offC = rig.Player.OnState(s => calls.Add("C " + s));
            rig.Start();
            Assert.That(calls, Is.EqualTo(new[] { "A Auto", "B Auto" }));
        }

        [Test]
        public void TheEventEndListeners_AreWalkedAsAJsSet()
        {
            var script = new DepictionScript { Opening = Frame() };
            script.Events.Add(TurnStart(1, Frame()));
            script.Events.Add(TurnStart(2, Frame(turn: 2)));
            var rig = new Rig(new DepictionRunner(script));
            var calls = new List<string>();
            Action offB = null;
            bool added = false;
            rig.Player.OnEventEnd(() =>
            {
                calls.Add("A" + rig.Player.EventEnds);
                if (added) return;
                added = true;
                offB();
                rig.Player.OnEventEnd(() => calls.Add("C" + rig.Player.EventEnds));
            });
            offB = rig.Player.OnEventEnd(() => calls.Add("B" + rig.Player.EventEnds));
            rig.Start();
            Assert.That(rig.RunUntil(() => rig.Player.EventEnds == 2, 10_000f), Is.True);
            Assert.That(calls, Is.EqualTo(new[] { "A1", "C1", "A2", "C2" }));
        }

        // ---- the turn start (player.js:965-984, 1022-1036) ------------------------------------------------------

        [TestCase(14, true)]
        [TestCase(1, true)]
        [TestCase(15, false)]
        [TestCase(0, false)]
        public void TheTurnStart_BeatsTheHeartThreeTimes_900Apart_OnlyUnder15Hp(int hp, bool beats)
        {
            var script = new DepictionScript { Opening = Frame() };
            script.Events.Add(TurnStart(1, Frame(hp)));
            Rig rig = Scripted(script);
            var delays = rig.Audio.Plays.Where(p => p.Name == "heartbeat").Select(p => p.Delay).ToList();
            Assert.That(delays, Is.EqualTo(beats ? new[] { 0f, 900f, 1800f } : Array.Empty<float>()));
        }

        private static DepictionFrame TwoEnemies(int turn, params CardFace[] hand)
        {
            DepictionFrame f = WithHand(Frame(turn: turn), hand);
            f.Enemies = new List<UnitFrame>
            {
                new UnitFrame { Unit = 0, Hp = 40, HpMax = 40, ArtId = "polearm_warped" },
                new UnitFrame { Unit = 1, Hp = 30, HpMax = 30, ArtId = "crossbow_hunter" },
            };
            f.Omens = new List<OmenFrame> { new OmenFrame { Unit = 0, Visible = true }, new OmenFrame { Unit = 1, Visible = true } };
            return f;
        }

        [Test]
        public void TheStandingOmens_BlinkOnceTheDrawHasPlayed_120Apart_AndOneShownInTheEventPopsInstead()
        {
            CardFace a = Card("a", CardKind.Attack, CardAim.Single, CardValueKind.Power);
            CardFace b = Card("b", CardKind.Guard, CardAim.Self, CardValueKind.Guard);
            var draw = new Cue { Kind = CueKind.DrawHand, Target = UnitSide.Player, Amount = 2 };
            DepictionEvent first = TurnStart(1, TwoEnemies(1, a, b), draw);
            // Turn 2: enemy 1's omen is shown in the turn start itself (after the draw): it pops, only enemy 0 blinks.
            DepictionEvent second = TurnStart(2, TwoEnemies(2, a, b),
                new Cue { Kind = CueKind.DiscardHand, Target = UnitSide.Player, Amount = 2 },
                new Cue { Kind = CueKind.DrawHand, Target = UnitSide.Player, Amount = 2 },
                new Cue { Kind = CueKind.OmenShow, Target = UnitSide.Enemy, Unit = 1 });
            var script = new DepictionScript { Opening = TwoEnemies(1) };
            script.Events.Add(first);
            script.Events.Add(second);
            Rig rig = Scripted(script);
            Assert.That(rig.RunUntil(() => rig.Finished > 0 || rig.Player.State == BattlePlayerState.Over, 60_000f), Is.True);
            Assert.That(rig.Player.PlanErrors, Is.EqualTo(0), string.Join("\n", rig.Log.Errors));

            float drawn = EffectPlan.BlockingMs(first, Switches);
            Assert.That(rig.Hud.BadgeOf(0).BlinkAt[0], Is.EqualTo(drawn).Within(0.01f), "after the banner and the draw, not after the banner");
            Assert.That(rig.Hud.BadgeOf(1).BlinkAt[0], Is.EqualTo(drawn + L.OmenBlinkStaggerMs).Within(0.01f), "the next badge 120 later");

            float secondStart = rig.EventEnds[0].At;
            float drawn2 = secondStart + Switches.WaitMs(EffectId.TurnBanner) + Switches.WaitMs(EffectId.HandDiscard, 2) + Switches.WaitMs(EffectId.CardDraw, 2);
            Assert.That(rig.Hud.BadgeOf(0).BlinkAt.Count, Is.EqualTo(2));
            Assert.That(rig.Hud.BadgeOf(0).BlinkAt[1], Is.EqualTo(drawn2).Within(0.01f));
            Assert.That(rig.Hud.BadgeOf(1).BlinkAt.Count, Is.EqualTo(1), "an omen shown in this event pops instead of blinking");
        }

        // ---- the enemy's turn (player.js:296, 666-670, 955-957, 985-991) ----------------------------------------

        [Test]
        public void TheEnemysTurnShade_GoesUpWithItsBanner_AndHoldsToTheResult_OnTheDecidingTurn()
        {
            DepictionFrame enemyTurn = Frame();
            enemyTurn.EnemyTurn = true;
            // The writer marks a frame EnemyTurn only while the battle goes on: the blow that ends it reads false.
            DepictionFrame lost = Frame(0);
            lost.Outcome = BattleOutcome.Lost;
            var script = new DepictionScript { Opening = Frame() };
            script.Events.Add(TurnStart(1, Frame()));
            script.Events.Add(new DepictionEvent { Order = 2, Kind = DepictionEventKind.TurnEnd, After = enemyTurn });
            script.Events.Add(EnemyAction(3, "突き", lost,
                new Cue { Kind = CueKind.EnemyWindup, Target = UnitSide.Enemy, System = StrikeSystem.Thrust },
                new Cue { Kind = CueKind.Slash, Target = UnitSide.Player, Source = UnitSide.Enemy, Amount = 60, HpAfter = 0, System = StrikeSystem.Thrust }));
            Rig rig = Scripted(script);
            Assert.That(rig.Field.ShadeOn, Is.False);
            Assert.That(rig.RunUntil(() => rig.Player.EventEnds == 1, 10_000f), Is.True);
            rig.Run(1);
            Assert.That(rig.Hud.Banners.Last(), Is.EqualTo((DepictionText.EnemyTurn, BannerSide.Enemy)));
            Assert.That(rig.Field.ShadeOn, Is.True, "the shade comes up as the 敵の番 banner comes in");
            Assert.That(rig.RunUntil(() => rig.Hud.ResultUpAt >= 0, 60_000f), Is.True);
            Assert.That(rig.Hud.ResultOutcome, Is.EqualTo(BattleOutcome.Lost));
            Assert.That(rig.Field.ShadeOn, Is.True, "held to the result, though the deciding frame reads EnemyTurn false");
            Assert.That(rig.Hud.EnemyTurnOn, Is.True);
            Assert.That(rig.Player.PlanErrors, Is.EqualTo(0), string.Join("\n", rig.Log.Errors));
        }

        // ---- labels and signs (player.js:127-136, 563-602, 1272-1279, 500) -------------------------------------

        [Test]
        public void ABlowThatSpent脆化AndIts強化_CarriesOneLabel_脆化First()
        {
            string fragile = EnumTokens.ToLabel(StatusKind.Fragile);
            string empower = EnumTokens.ToLabel(StatusKind.Empower);
            var script = new DepictionScript { Opening = Frame() };
            script.Events.Add(TurnStart(1, Frame()));
            script.Events.Add(EnemyAction(2, "突き", Frame(32),
                new Cue { Kind = CueKind.EnemyWindup, Target = UnitSide.Enemy, System = StrikeSystem.Thrust },
                new Cue { Kind = CueKind.Slash, Target = UnitSide.Player, Source = UnitSide.Enemy, Amount = 18, HpAfter = 32, System = StrikeSystem.Thrust },
                new Cue { Kind = CueKind.StatusChange, Target = UnitSide.Player, Text = fragile, Amount = -1, StacksAfter = 0 },
                new Cue { Kind = CueKind.StatusChange, Target = UnitSide.Enemy, Text = empower, Amount = -1, StacksAfter = 0 }));
            // A 脆化 spent before the blow it belongs to (the writer opens a second blow's event where it is spent).
            script.Events.Add(EnemyAction(3, "突き（2 撃目）", Frame(20),
                new Cue { Kind = CueKind.StatusChange, Target = UnitSide.Player, Text = fragile, Amount = -1, StacksAfter = 0 },
                new Cue { Kind = CueKind.Slash, Target = UnitSide.Player, Source = UnitSide.Enemy, Amount = 12, HpAfter = 20, System = StrikeSystem.Thrust }));
            Rig rig = Scripted(script);
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Over, 60_000f), Is.True);
            Assert.That(rig.Player.PlanErrors, Is.EqualTo(0), string.Join("\n", rig.Log.Errors));
            var numbers = rig.Field.Floats.Where(f => f.Text.StartsWith(BattlePlayer.Minus, StringComparison.Ordinal)).ToList();
            Assert.That(numbers.Select(f => f.Text), Is.EqualTo(new[] { "−18", "−12" }));
            Assert.That(numbers[0].O.Side, Is.EqualTo("×1.5 " + fragile), "one label per blow, 脆化 first");
            Assert.That(numbers[1].O.Side, Is.EqualTo("×1.5 " + fragile), "the blow after it when none comes before");
        }

        [Test]
        public void AnHpChange_FloatsTheTypographicMinus_WhileTheLogKeepsTheJsAsciiSign()
        {
            var script = new DepictionScript { Opening = Frame() };
            script.Events.Add(TurnStart(1, Frame(48, 40),
                new Cue { Kind = CueKind.HpChange, Target = UnitSide.Player, Text = "出血", Amount = -2, HpAfter = 48 },
                new Cue { Kind = CueKind.HpChange, Target = UnitSide.Enemy, Unit = 0, Text = "再生", Amount = 3, HpAfter = 40 }));
            Rig rig = Scripted(script);
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Over, 60_000f), Is.True);
            var bleed = rig.Field.Floats.Single(f => f.Text.StartsWith("出血", StringComparison.Ordinal));
            Assert.That(bleed.Text, Is.EqualTo("出血 −2"));
            Assert.That(bleed.Role, Is.EqualTo(FxRole.Omen));
            Assert.That(bleed.Size, Is.EqualTo(40f));
            var regen = rig.Field.Floats.Single(f => f.Text.StartsWith("再生", StringComparison.Ordinal));
            Assert.That(regen.Text, Is.EqualTo("再生 +3"));
            Assert.That(regen.Role, Is.EqualTo(FxRole.Heal));
            // player.js:500 writes String(n): a loss keeps the ASCII '-' in the log (a JS quirk, ported as it is).
            Assert.That(rig.Player.LogLines(), Is.EqualTo(new[] { "— ターン 1 —", "　自分 出血 -2", "　敵 再生 +3" }));
        }

        [Test]
        public void TheLog_KeepsItsLast400Lines()
        {
            var script = new DepictionScript { Opening = Frame() };
            for (int i = 1; i <= 450; i++) script.Events.Add(TurnStart(i, Frame(turn: i)));
            Rig rig = Scripted(script, new BattleSettings { ShortFx = true });
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Over, 600_000f), Is.True);
            IReadOnlyList<string> lines = rig.Player.LogLines();
            Assert.That(lines.Count, Is.EqualTo(BattlePlayer.LogMax));
            Assert.That(lines[0], Is.EqualTo("— ターン 51 —"));
            Assert.That(lines[lines.Count - 1], Is.EqualTo("— ターン 450 —"));
            Assert.That(rig.Hud.Log, Is.EqualTo(lines));
        }

        // ---- the player's wait (player.js:1707-1748) ------------------------------------------------------------

        [TestCase("c", 1)]
        [TestCase("a", 0)]
        public void ThePlate_FlashesOncePerWait_WhenTheSourceWouldTakeNoCard(string next, int flashes)
        {
            CardFace a = Card("a", CardKind.Attack, CardAim.Single, CardValueKind.Power);
            CardFace b = Card("b", CardKind.Guard, CardAim.Self, CardValueKind.Guard);
            CardFace c = Card("c", CardKind.Skill, CardAim.Self, CardValueKind.Guard);
            var script = new DepictionScript { Opening = WithHand(Frame(), a, b) };
            script.Events.Add(TurnStart(1, WithHand(Frame(), a, b)));
            script.Events.Add(PlayCard(2, next == "c" ? c : a, WithHand(Frame(), b)));
            Rig rig = Scripted(script);
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle, 10_000f), Is.True);
            rig.Player.RefreshPlayable();
            rig.Player.RefreshPlayable();
            Assert.That(rig.Hud.Flashes, Is.EqualTo(flashes), "once per wait, however often the look is refreshed");
        }

        // ---- the played card (player.js:1584-1645) ---------------------------------------------------------------

        [Test]
        public void AStanceCard_WaitsAtItsList_UntilItsGhostLeavesForTheExile_WhileAnotherCardGoesAtOnce()
        {
            CardFace stance = Card("rock", CardKind.Stance, CardAim.Self, CardValueKind.Guard);
            CardFace guard = Card("block", CardKind.Guard, CardAim.Self, CardValueKind.Guard);
            var script = new DepictionScript { Opening = WithHand(Frame(), stance, guard) };
            script.Events.Add(TurnStart(1, WithHand(Frame(), stance, guard)));
            DepictionEvent setStance = PlayCard(2, stance, WithHand(Frame(), guard),
                new Cue { Kind = CueKind.StaminaChange, Target = UnitSide.Player, Amount = -1, StaminaAfter = 4, StaminaMax = 5 });
            setStance.PlayedTo = PileKind.Exile;
            script.Events.Add(setStance);
            script.Events.Add(PlayCard(3, guard, WithHand(Frame()),
                new Cue { Kind = CueKind.StaminaChange, Target = UnitSide.Player, Amount = -1, StaminaAfter = 3, StaminaMax = 5 }));
            Rig rig = Scripted(script);
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle, 10_000f), Is.True);
            float released = rig.Env.Clock.Now;
            rig.Input(() => rig.Player.HandCallbacks.OnRelease(stance.Id, DropZone.AboveThrowLine, -1));
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle, 10_000f), Is.True);
            float leaves = released + Switches.WaitMs(EffectId.CardRelease) + Math.Max(0f, EffectCatalog.CardToExileDelayMs - Switches.Ms(EffectId.CardRelease));
            rig.Run(EffectCatalog.CardToExileDelayMs + 10);
            Assert.That(rig.Hand.DropTimes.Single().Id, Is.EqualTo(stance.Id));
            Assert.That(rig.Hand.DropTimes.Single().At, Is.EqualTo(leaves).Within(0.01f), "the card stays at the stance list until its ghost leaves");
            Assert.That(rig.Field.Ghosts.Single().Kind, Is.EqualTo(GhostKind.Exile));
            Assert.That(rig.Field.Ghosts.Single().At, Is.EqualTo(leaves).Within(0.01f), "its ghost leaves from there as it goes");

            float released2 = rig.Env.Clock.Now;
            rig.Input(() => rig.Player.HandCallbacks.OnRelease(guard.Id, DropZone.AboveThrowLine, -1));
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Over, 60_000f), Is.True);
            float gone = released2 + Switches.WaitMs(EffectId.CardRelease);
            Assert.That(rig.Hand.DropTimes[1].Id, Is.EqualTo(guard.Id));
            Assert.That(rig.Hand.DropTimes[1].At, Is.EqualTo(gone).Within(0.01f), "any other card leaves the hand as its release ends");
            Assert.That(rig.Field.Ghosts[1].Kind, Is.EqualTo(GhostKind.Discard));
            Assert.That(rig.Field.Ghosts[1].At, Is.EqualTo(gone).Within(0.01f));
            Assert.That(rig.Player.PlanErrors, Is.EqualTo(0), string.Join("\n", rig.Log.Errors));
        }

        [Test]
        public void AMoveCutShort_LeansThatWay_OnItsArrival()
        {
            CardFace step = Card("step2", CardKind.Move, CardAim.Self, CardValueKind.Close, "2");
            var script = new DepictionScript { Opening = WithHand(Frame(), step) };
            script.Events.Add(TurnStart(1, WithHand(Frame(), step)));
            script.Events.Add(PlayCard(2, step, WithHand(Frame()),
                new Cue { Kind = CueKind.StaminaChange, Target = UnitSide.Player, Amount = -1, StaminaAfter = 4, StaminaMax = 5 },
                new Cue { Kind = CueKind.RangeSwitch, Target = UnitSide.Player, PlayerCellAfter = 3, EnemyCellAfter = 4, GapAfter = 0, CellsMoved = 1 }));
            Rig rig = Scripted(script);
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle, 10_000f), Is.True);
            float released = rig.Env.Clock.Now;
            rig.Input(() => rig.Player.HandCallbacks.OnRelease(step.Id, DropZone.AboveThrowLine, -1));
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Over, 60_000f), Is.True);
            Assert.That(rig.Field.Bumps, Is.EqualTo(new[] { (UnitSide.Player, 1) }), "詰める 2 walked 1 cell into the enemy: one lean forward");
            float arrived = released + Switches.WaitMs(EffectId.CardRelease) + Switches.WaitMs(EffectId.FloorMove, 1) + Switches.WaitMs(EffectId.GapRecount);
            Assert.That(rig.Field.BumpAt.Single(), Is.EqualTo(arrived).Within(0.01f), "on its arrival, after the recount");
            Assert.That(rig.Player.PlanErrors, Is.EqualTo(0), string.Join("\n", rig.Log.Errors));
        }

        // ---- walls and pulls (player.js:87-94, 539-548, 1366-1384, 1432-1434) ----------------------------------

        [Test]
        public void APlayerShovedIntoTheEndOfTheLine_TakesTheWallsBlow()
        {
            var script = new DepictionScript { Opening = Frame() };
            script.Events.Add(TurnStart(1, Frame()));
            script.Events.Add(EnemyAction(2, BattlePlayer.WallTitle, Frame(47),
                new Cue { Kind = CueKind.Hit, Target = UnitSide.Player, Amount = 3, HpAfter = 47, Unit = 0 }));
            Rig rig = Scripted(script);
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Over, 60_000f), Is.True);
            Assert.That(rig.Player.PlanErrors, Is.EqualTo(0), string.Join("\n", rig.Log.Errors));
            Assert.That(rig.Field.WallHits, Is.EqualTo(new[] { (UnitSide.Player, -1) }), "shoved left into the line's end");
            var hit = rig.Field.Floats.Single(f => f.Text == "−3");
            Assert.That(hit.O.Side, Is.EqualTo(BattlePlayer.WallSide));
            Assert.That(rig.Audio.Plays.Select(p => p.Name), Has.Member("hitBlunt").And.No.Member("hitBody"), "打's sound for the body's");
            Assert.That(rig.Player.LogLines(), Is.EqualTo(new[] { "— ターン 1 —", "敵: " + BattlePlayer.WallTitle, "　自分 壁 −3" }));
        }

        [Test]
        public void APull_Reads引き寄せ_OnTheEnemyItMoved_AndItsWallIsTheOtherWay()
        {
            CardFace hook = Card("hook", CardKind.Attack, CardAim.Single, CardValueKind.Pull, "2");
            var script = new DepictionScript { Opening = WithHand(Frame(), hook) };
            script.Events.Add(TurnStart(1, WithHand(Frame(), hook)));
            script.Events.Add(PlayCard(2, hook, WithHand(Frame(50, 37)),
                new Cue { Kind = CueKind.StaminaChange, Target = UnitSide.Player, Amount = -1, StaminaAfter = 4, StaminaMax = 5 },
                new Cue { Kind = CueKind.RangeSwitch, Target = UnitSide.Player, Pushed = true, Unit = 0, UnitCellAfter = 3, PlayerCellAfter = 2, EnemyCellAfter = 3, GapAfter = 0, CellsMoved = 2 },
                new Cue { Kind = CueKind.Hit, Target = UnitSide.Enemy, Unit = 0, Amount = 3, HpAfter = 37 }));
            Rig rig = Scripted(script);
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Idle, 10_000f), Is.True);
            rig.Input(() => rig.Player.HandCallbacks.OnRelease(hook.Id, DropZone.Receiver, -1));
            Assert.That(rig.RunUntil(() => rig.Player.State == BattlePlayerState.Over, 60_000f), Is.True);
            Assert.That(rig.Player.PlanErrors, Is.EqualTo(0), string.Join("\n", rig.Log.Errors));
            var word = rig.Field.Floats.Single(f => f.Role == FxRole.Ink2);
            Assert.That(word.Text, Is.EqualTo(BattlePlayer.PulledWord), "cell 3 is nearer the hero than the enemy's home");
            Assert.That(word.Size, Is.EqualTo(36f));
            Assert.That(rig.Field.WallHits, Is.EqualTo(new[] { (UnitSide.Enemy, -1) }), "a pull's wall is on the hero's side");
        }
    }
}
