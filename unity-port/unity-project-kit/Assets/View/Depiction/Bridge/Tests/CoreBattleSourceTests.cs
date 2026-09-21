// The core battle as an IDepictionSource: the contract the screen relies on, a whole fight, and the
// 2.0 s-per-event budget held against every event the converter can write.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class CoreBattleSourceTests
    {
        private const int Seed = 20260921;

        /// <summary>
        /// How long the screen blocks on each cue, in milliseconds — copied from DepictionPlayer.PlayCue
        /// and the helpers it calls (DepictionFx, StatusBarView). This is the nominal time: each tween
        /// may run one frame long, which is what the PlayMode test (issue #28) measures for real.
        /// Keep this table in step with DepictionPlayer when a beat's length changes.
        /// </summary>
        private static int NominalMs(Cue cue, DepictionEvent ev)
        {
            switch (cue.Kind)
            {
                case CueKind.GuardReset: return 0;
                case CueKind.StaminaChange: return cue.Amount > 0 ? 220 : 0;
                case CueKind.DrawHand: return 60 * ev.After.Hand.Count + 260;
                case CueKind.OmenShow: return 240;
                case CueKind.TraitFire: return 260;
                case CueKind.Slash:
                {
                    int[] hitStop = { 0, 40, 70, 110 };
                    int swing = (cue.Source == UnitSide.Player ? 100 : 0) + 120 + hitStop[cue.Intensity - 1];
                    return cue.HpAfter == Cue.Unchanged ? swing : swing + 300 + 120;
                }
                case CueKind.GuardGain: return 260;
                case CueKind.RangeSwitch: return 320 + 150;
                case CueKind.StanceCue: return 200 + 260;
                case CueKind.DiscardHand: return 60 * cue.Amount + 240;
                case CueKind.EnemyWindup: return 220;
                case CueKind.SideBonusMiss: return 260;
                case CueKind.GuardBlock: return 240 + 80;
                case CueKind.Hit: return 300 + (cue.Target == UnitSide.Player ? 200 + 140 : 0);
                case CueKind.StatusChange: return 0; // no beat yet (#77); the chips settle with the frame
                default: throw new ArgumentOutOfRangeException(nameof(cue), cue.Kind, "no nominal time");
            }
        }

        /// <summary>A released card flies for 160 ms before its cues start, inside the measured time.</summary>
        private static int NominalMs(DepictionEvent ev)
        {
            return (ev.WaitsForDrag ? 160 : 0) + ev.Cues.Sum(c => NominalMs(c, ev));
        }

        private const int BudgetMs = 2000;

        private static DropZone ZoneFor(CardFace face)
        {
            return DepictionText.RequiredZone(face.Aim);
        }

        // ---- the contract -------------------------------------------------------------------

        [Test]
        public void ItOpens_NotWaiting_AndTheFirstAutomaticEventIsTheTurnStart()
        {
            CoreBattleSource source = CoreBattleSource.Slice(Seed);

            Assert.That(source.Finished, Is.False);
            Assert.That(source.WaitingForPlayer, Is.False);
            Assert.That(source.CanEndTurn, Is.False);
            Assert.That(source.Frame.Hand, Is.Empty);
            Assert.That(() => source.EndTurn(), Throws.InvalidOperationException);

            DepictionEvent start = source.AdvanceAuto();

            Assert.That(start.Kind, Is.EqualTo(DepictionEventKind.TurnStart));
            Assert.That(source.Frame, Is.SameAs(start.After));
            Assert.That(source.Frame.Hand, Has.Count.EqualTo(5));
            Assert.That(source.WaitingForPlayer, Is.True);
            Assert.That(source.CanEndTurn, Is.True);
            Assert.That(() => source.AdvanceAuto(), Throws.InvalidOperationException, "the turn waits for the player");
            Assert.That(source.GuideText, Does.Contain("ターン終了"));
            Assert.That(source.SuggestedCardId, Is.EqualTo(""));
        }

        [Test]
        public void ItDoesNotReadTheDemoDeck()
        {
            CoreBattleSource source = CoreBattleSource.Slice(Seed);
            source.AdvanceAuto();

            var demoNames = DemoDeck.All.Select(c => c.DefId).ToList();
            foreach (CardFace face in source.Frame.Hand)
            {
                Assert.That(demoNames, Has.None.EqualTo(DemoDeck.DefIdOf(face.Id)));
                Assert.That(CardCatalog.All.Select(c => c.Name), Has.Member(face.Name));
            }
        }

        [Test]
        public void Verdicts_ComeFromTheCore()
        {
            CoreBattleSource source = CoreBattleSource.Slice(Seed);
            DepictionEvent ignored;
            Assert.That(source.Inspect("thrust-0"), Is.EqualTo(PlayVerdict.NotWaiting));

            source.AdvanceAuto();
            Assert.That(source.Inspect("no-such-card"), Is.EqualTo(PlayVerdict.NotInHand));
            Assert.That(source.TryPlay("no-such-card", DropZone.Receiver, out ignored), Is.EqualTo(PlayVerdict.NotInHand));

            // Spend until something in the hand cannot be paid for.
            CardFace stuck = null;
            while (stuck == null)
            {
                CardFace next = source.Frame.Hand.FirstOrDefault(f => source.Inspect(f.Id) == PlayVerdict.Accepted);
                stuck = source.Frame.Hand.FirstOrDefault(f => source.Inspect(f.Id) == PlayVerdict.NotEnoughStamina);
                if (stuck != null || next == null) break;
                source.TryPlay(next.Id, ZoneFor(next), out ignored);
            }

            Assert.That(stuck, Is.Not.Null, "seed " + Seed + " deals a first hand that costs more than 10");
            Assert.That(source.PreviewFor(stuck.Id), Is.EqualTo(""));
            Assert.That(source.RefusalText(stuck.Id, PlayVerdict.NotEnoughStamina), Does.Contain("スタミナ " + stuck.Cost));
            Assert.That(source.RefusalText(stuck.Id, PlayVerdict.WrongZone), Does.Contain(DepictionText.ZoneName(stuck.Aim)));
        }

        [Test]
        public void EndTurn_HandsBackTheTurnEnd_ThenTheEnemyAction_ThenTheNextOmen_ThenTheNextTurn()
        {
            CoreBattleSource source = CoreBattleSource.Slice(Seed);
            source.AdvanceAuto();

            var kinds = new List<DepictionEventKind> { source.EndTurn().Kind };
            Assert.That(source.WaitingForPlayer, Is.False);
            Assert.That(source.CanEndTurn, Is.False);
            kinds.Add(source.AdvanceAuto().Kind);
            kinds.Add(source.AdvanceAuto().Kind);
            kinds.Add(source.AdvanceAuto().Kind);

            Assert.That(kinds, Is.EqualTo(new[]
            {
                DepictionEventKind.TurnEnd, DepictionEventKind.EnemyAction,
                DepictionEventKind.NextOmen, DepictionEventKind.TurnStart,
            }));
            Assert.That(source.WaitingForPlayer, Is.True);
            Assert.That(source.Frame.Corner.Turn, Is.EqualTo(2));
            Assert.That(source.Frame.Player.RangeGlyph, Is.EqualTo("遠"), "shoved on turn 1");
        }

        // ---- a whole fight ------------------------------------------------------------------

        [Test]
        public void EveryEventsFrame_AgreesWithTheCoreState_WhenTheStepSettles()
        {
            CoreBattleSource source = CoreBattleSource.Slice(Seed);
            int guard = 0;
            while (!source.Finished && guard++ < 400)
            {
                if (source.WaitingForPlayer)
                {
                    CardFace next = source.Frame.Hand.FirstOrDefault(f => source.Inspect(f.Id) == PlayVerdict.Accepted);
                    DepictionEvent ignored;
                    if (next != null) source.TryPlay(next.Id, ZoneFor(next), out ignored);
                    else source.EndTurn();
                }
                else
                {
                    source.AdvanceAuto();
                }

                // Whenever the source is settled (nothing queued), the screen and the core must agree.
                if (!source.WaitingForPlayer && !source.Finished) continue;
                BattleState state = source.State;
                DepictionFrame frame = source.Frame;
                Assert.That(frame.Player.Hp, Is.EqualTo(state.Player.Hp));
                Assert.That(frame.Player.Guard, Is.EqualTo(state.Player.Guard));
                Assert.That(frame.Player.Stamina, Is.EqualTo(state.Player.Stamina));
                Assert.That(frame.Player.RangeGlyph, Is.EqualTo(state.Player.Position.Value.ToLabel()));
                Assert.That(frame.Enemy.Hp, Is.EqualTo(state.Enemy.Hp));
                Assert.That(frame.Enemy.Guard, Is.EqualTo(state.Enemy.Guard));
                Assert.That(frame.Enemy.Statuses.Select(c => c.Label + c.Stacks),
                    Is.EqualTo(CoreText.Chips(state.Enemy.Statuses).Select(c => c.Label + c.Stacks)));
                Assert.That(frame.Hand.Select(c => c.Id), Is.EqualTo(state.Hand.Select(c => c.InstanceId)));
                Assert.That(frame.Corner.Turn, Is.EqualTo(state.Turn));
            }

            Assert.That(source.Finished, Is.True, "greedy play ends the fight one way or the other");
            Assert.That(source.GuideText, Is.EqualTo(source.State.Result == GameResult.Won ? "敵を討ち取りました" : "力尽きました"));
            Assert.That(() => source.AdvanceAuto(), Throws.InvalidOperationException);
        }

        [Test]
        public void TheSameSeed_WritesTheSameScript()
        {
            string Run()
            {
                CoreBattleSource source = CoreBattleSource.Slice(Seed);
                var lines = new List<string>();
                int guard = 0;
                while (!source.Finished && guard++ < 400)
                {
                    DepictionEvent ev;
                    if (source.WaitingForPlayer)
                    {
                        CardFace next = source.Frame.Hand.FirstOrDefault(f => source.Inspect(f.Id) == PlayVerdict.Accepted);
                        if (next != null) source.TryPlay(next.Id, ZoneFor(next), out ev);
                        else ev = source.EndTurn();
                    }
                    else
                    {
                        ev = source.AdvanceAuto();
                    }
                    lines.Add(ev.Order + " " + ev.Kind + " " + ev.Title + " "
                        + string.Join(",", ev.Cues.Select(c => c.Kind + ":" + c.Amount))
                        + " → " + ev.After.Player.Hp + "/" + ev.After.Enemy.Hp);
                }
                return string.Join("\n", lines);
            }

            Assert.That(Run(), Is.EqualTo(Run()));
        }

        // ---- the 2.0 s budget (issue #28) ---------------------------------------------------

        [Test]
        public void EveryEventOfAWholeFight_StaysInsideTwoSeconds_ForManySeeds()
        {
            int worst = 0;
            string worstLine = "";
            for (int seed = 1; seed <= 40; seed++)
            {
                CoreBattleSource source = CoreBattleSource.Slice(seed);
                int guard = 0;
                while (!source.Finished && guard++ < 400)
                {
                    DepictionEvent ev;
                    if (source.WaitingForPlayer)
                    {
                        CardFace next = source.Frame.Hand.FirstOrDefault(f => source.Inspect(f.Id) == PlayVerdict.Accepted);
                        if (next != null) source.TryPlay(next.Id, ZoneFor(next), out ev);
                        else ev = source.EndTurn();
                    }
                    else
                    {
                        ev = source.AdvanceAuto();
                    }

                    int ms = NominalMs(ev);
                    if (ms > worst)
                    {
                        worst = ms;
                        worstLine = "seed " + seed + " event " + ev.Order + " " + ev.Title + " ["
                            + string.Join(", ", ev.Cues.Select(c => c.Kind)) + "]";
                    }
                    Assert.That(ms, Is.LessThan(BudgetMs), "seed " + seed + " event " + ev.Order + " " + ev.Title);
                }
            }
            TestContext.Out.WriteLine("worst nominal event: " + worst + " ms — " + worstLine);
        }

        [Test]
        public void EveryCard_FromEitherSide_IntoGuardOrNot_StaysInsideTwoSeconds()
        {
            int worst = 0;
            string worstLine = "";
            foreach (CardDef def in CardCatalog.All)
            foreach (Position side in new[] { Position.Near, Position.Far })
            foreach (int enemyGuard in new[] { 0, 3, 40 })
            {
                // One copy of the card plus four fillers, dealt in order, so it is in the first hand.
                var deck = Cards.BuildDeck(new[] { def }, 1);
                deck.AddRange(Cards.BuildDeck(new[] { CardCatalog.Brace }, 1).Select(c => new CardInstance("filler-0", c.Def)));
                for (int i = 1; i < 4; i++) deck.Add(new CardInstance("filler-" + i, CardCatalog.Brace));

                var setup = new BattleSetup(Enemies.PolearmWarped, deck, side);
                BattleState state = TurnLoop.Start(setup, new FixedRng(0.9999999)).State;
                state = TurnLoop.BeginPlayerTurn(state, new FixedRng(0.9999999)).State;
                state = state with { Enemy = state.Enemy with { Guard = enemyGuard } };

                var writer = new CoreScriptWriter(setup.Enemy);
                writer.Opening(state);
                StepResult play = TurnLoop.PlayCard(state, def.Id + "-0", new FixedRng(0.9999999));
                DepictionEvent ev = writer.Write(play.Events, play.State).Single();

                int ms = NominalMs(ev);
                string line = def.Id + " from " + side + " into Guard " + enemyGuard;
                if (ms > worst) { worst = ms; worstLine = line; }
                Assert.That(ms, Is.LessThan(BudgetMs), line);
            }
            TestContext.Out.WriteLine("worst nominal card: " + worst + " ms — " + worstLine);
        }

        [Test]
        public void EveryEnemyAction_AgainstEitherSide_IntoGuardOrNot_StaysInsideTwoSeconds()
        {
            int worst = 0;
            string worstLine = "";
            foreach (Position side in new[] { Position.Near, Position.Far })
            foreach (int playerGuard in new[] { 0, 2, 40 })
            foreach (int enemyStamina in new[] { 10, 1 })
            {
                var setup = new BattleSetup(Enemies.PolearmWarped, PrototypeDeck.Build(), side);
                BattleState state = TurnLoop.Start(setup, new FixedRng(0.9999999)).State;
                state = state with { Enemy = state.Enemy with { Stamina = enemyStamina, NextTurnRecoveryBonus = enemyStamina == 1 ? -2 : 0 } };
                // Re-decide the omen for the drained enemy, so that every action of the tree is reached.
                state = state with { Omen = EnemyAi.DecideOmen(setup.Enemy, side, enemyStamina) };
                state = TurnLoop.BeginPlayerTurn(state, new FixedRng(0.9999999)).State;
                state = state with { Player = state.Player with { Guard = playerGuard, Stamina = 0 } };

                var writer = new CoreScriptWriter(setup.Enemy);
                writer.Opening(state);
                StepResult end = TurnLoop.EndTurn(state, new FixedRng(0.9999999));
                foreach (DepictionEvent ev in writer.Write(end.Events, end.State))
                {
                    int ms = NominalMs(ev);
                    string line = ev.Title + " vs " + side + " Guard " + playerGuard + " (enemy stamina " + enemyStamina + ")";
                    if (ms > worst) { worst = ms; worstLine = line; }
                    Assert.That(ms, Is.LessThan(BudgetMs), line);
                }
            }
            TestContext.Out.WriteLine("worst nominal enemy-phase event: " + worst + " ms — " + worstLine);
        }
    }
}
