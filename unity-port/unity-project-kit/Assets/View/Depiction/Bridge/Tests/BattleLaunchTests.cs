// What the battle scene starts with (BattleLaunch), and the run a Play of that scene makes: the
// same calls DepictionPlayer makes, in the same order, without a screen.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class BattleLaunchTests
    {
        /// <summary>
        /// Drives a source the way DepictionPlayer does with Auto Play Drags and Auto End Turn on:
        /// automatic events while nothing waits, then the suggested card, then the turn end.
        /// </summary>
        private static List<DepictionEvent> RunUnattended(IDepictionSource source, int safetyLimit = 400)
        {
            var played = new List<DepictionEvent>();
            while (!source.Finished && played.Count < safetyLimit)
            {
                if (!source.WaitingForPlayer)
                {
                    played.Add(source.AdvanceAuto());
                    continue;
                }

                string suggested = source.SuggestedCardId;
                if (suggested.Length == 0)
                {
                    Assert.That(source.CanEndTurn, Is.True);
                    played.Add(source.EndTurn());
                    continue;
                }

                CardFace face = DepictionText.Find(source.Frame.Hand, suggested);
                DepictionEvent ev;
                Assert.That(source.TryPlay(suggested, DepictionText.RequiredZone(face.Aim), out ev), Is.EqualTo(PlayVerdict.Accepted));
                played.Add(ev);
            }
            return played;
        }

        [Test]
        public void TheDefaults_AreTheSlice()
        {
            var launch = new BattleLaunch();
            BattleSetup setup = launch.BuildSetup();

            Assert.That(launch.EnemyId, Is.EqualTo("polearm_warped"));
            Assert.That(launch.Seed, Is.EqualTo(20260921));
            Assert.That(launch.AutoPlay, Is.False);
            Assert.That(launch.StopAfterTurns, Is.EqualTo(0));
            Assert.That(setup.Enemy, Is.SameAs(Enemies.PolearmWarped));
            Assert.That(setup.Enemy.Name, Is.EqualTo("長柄の歪み兵"));
            Assert.That(setup.PlayerStartPosition, Is.EqualTo(Position.Near));
            Assert.That(setup.Deck, Has.Count.EqualTo(20));
            Assert.That(Cards.Validate(setup.Deck).Ok, Is.True);
        }

        [Test]
        public void AnUnknownEnemyId_IsRefused_WithTheKnownIdsInTheMessage()
        {
            var launch = new BattleLaunch { EnemyId = "polearm" };
            var error = Assert.Throws<ArgumentException>(() => launch.CreateSource());
            Assert.That(error.Message, Does.Contain("\"polearm\""));
            Assert.That(error.Message, Does.Contain("polearm_warped"));

            Assert.Throws<ArgumentException>(() => new BattleLaunch { EnemyId = null }.CreateSource());
        }

        [Test]
        public void ANegativeTurnLimit_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleLaunch { StopAfterTurns = -1 }.CreateSource());
        }

        [Test]
        public void TheStartSide_PicksTheFirstOmen()
        {
            CoreBattleSource near = new BattleLaunch { StartSide = RangeSide.Near }.CreateSource();
            CoreBattleSource far = new BattleLaunch { StartSide = RangeSide.Far }.CreateSource();

            Assert.That(near.Frame.Player.RangeGlyph, Is.EqualTo("近"));
            Assert.That(near.State.Omen.ActionId, Is.EqualTo("shove"));
            Assert.That(far.Frame.Player.RangeGlyph, Is.EqualTo("遠"));
            Assert.That(far.State.Omen.ActionId, Is.EqualTo("sweep"));
        }

        [Test]
        public void TheSeed_FromTheInspector_DealsTheSameHandAsTheCoreTest()
        {
            CoreBattleSource source = new BattleLaunch().CreateSource();
            source.AdvanceAuto();

            // TurnLoopTests.PinnedHands, turn 1: the same seed, the same generator, the same deal.
            Assert.That(source.State.Hand.Select(c => c.Def.Id), Is.EqualTo(new[]
            {
                "shield_bash", "body_check", "step_out_guard", "step_in_guard", "step_out_guard",
            }));

            CoreBattleSource other = new BattleLaunch { Seed = 7 }.CreateSource();
            other.AdvanceAuto();
            Assert.That(other.State.Hand.Select(c => c.InstanceId), Is.Not.EqualTo(source.State.Hand.Select(c => c.InstanceId)));
        }

        [Test]
        public void APlayedBattle_SuggestsNothing_AndNeverStopsEarly()
        {
            CoreBattleSource source = new BattleLaunch().CreateSource();
            source.AdvanceAuto();

            Assert.That(source.SuggestedCardId, Is.EqualTo(""), "the hand belongs to the player");
            source.EndTurn();
            source.AdvanceAuto();
            source.AdvanceAuto();
            Assert.That(source.Finished, Is.False);
        }

        // ---- "Play すると 1 ターンが通る" (#74), without the screen ---------------------------

        [Test]
        public void OneUnattendedTurn_RunsFromTheTurnStartToTheNextOmen_AndStops()
        {
            CoreBattleSource source = new BattleLaunch { AutoPlay = true, StopAfterTurns = 1 }.CreateSource();

            List<DepictionEvent> played = RunUnattended(source);

            Assert.That(source.Finished, Is.True);
            Assert.That(played.First().Kind, Is.EqualTo(DepictionEventKind.TurnStart));
            Assert.That(played.Count(e => e.Kind == DepictionEventKind.PlayCard), Is.EqualTo(4), "10 stamina pays for 2 + 1 + 3 + 3");
            Assert.That(played.Skip(played.Count - 3).Select(e => e.Kind), Is.EqualTo(new[]
            {
                DepictionEventKind.TurnEnd, DepictionEventKind.EnemyAction, DepictionEventKind.NextOmen,
            }));
            Assert.That(played.Select(e => e.Order), Is.EqualTo(Enumerable.Range(1, played.Count)));

            // The end point of the slice: shoved far, so the next omen is the sweep (攻撃・遠).
            DepictionFrame last = played.Last().After;
            Assert.That(last.Corner.Turn, Is.EqualTo(1));
            Assert.That(last.Player.RangeGlyph, Is.EqualTo("遠"));
            Assert.That(last.Omen.Visible, Is.True);
            Assert.That(last.Omen.KindLabel, Is.EqualTo("攻撃"));
            Assert.That(last.Omen.SideGlyph, Is.EqualTo("遠"));
            Assert.That(last.Player.Hp, Is.EqualTo(50));
            Assert.That(last.Enemy.Hp, Is.EqualTo(42));
            Assert.That(last.Enemy.Statuses.Single().Label, Is.EqualTo("鈍足"));
            Assert.That(source.GuideText, Is.EqualTo(""));
            Assert.That(() => source.AdvanceAuto(), Throws.InvalidOperationException);
        }

        [Test]
        public void AnUnattendedFight_ToTheEnd_Finishes_WithAWinner()
        {
            CoreBattleSource source = new BattleLaunch { AutoPlay = true }.CreateSource();

            List<DepictionEvent> played = RunUnattended(source);

            Assert.That(source.Finished, Is.True);
            Assert.That(source.State.Result, Is.Not.EqualTo(GameResult.Ongoing));
            Assert.That(played.Count, Is.LessThan(400));
            Assert.That(source.GuideText, Is.Not.EqualTo(""));
        }
    }
}
