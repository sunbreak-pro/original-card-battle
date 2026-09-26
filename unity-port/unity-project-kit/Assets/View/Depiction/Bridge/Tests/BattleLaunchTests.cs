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
            Assert.That(setup.Enemy.Name, Is.EqualTo("錆槍の竜兵"));
            Assert.That(launch.StartGap, Is.EqualTo(3));
            Assert.That(launch.FieldCells, Is.EqualTo(6));
            Assert.That(setup.PlayerStartCell, Is.EqualTo(2));
            Assert.That(setup.EnemyStartCell, Is.EqualTo(6));
            Assert.That(setup.FieldCells, Is.EqualTo(6));
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
        public void TheStartGap_PicksTheFirstOmen()
        {
            CoreBattleSource adjacent = new BattleLaunch { StartGap = 0 }.CreateSource();
            CoreBattleSource apart = new BattleLaunch { StartGap = 3 }.CreateSource();

            Assert.That(adjacent.Frame.Player.RangeGlyph, Is.EqualTo("0"));
            Assert.That(adjacent.State.Omen.ActionId, Is.EqualTo("shove"));
            Assert.That(apart.Frame.Player.RangeGlyph, Is.EqualTo("3"));
            Assert.That(apart.State.Omen.ActionId, Is.EqualTo("step_forward"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleLaunch { StartGap = -1 }.CreateSource());
        }

        [Test]
        public void TheLine_IsHandedIn_AndAShortOneBringsTheEnemyToItsEnd()
        {
            // battle_core_v4 §7.1 / §7.3 (2026-09-23, #169): the width comes with the battle.
            CoreBattleSource wide = new BattleLaunch { FieldCells = 8 }.CreateSource();
            CoreBattleSource narrow = new BattleLaunch { FieldCells = 5 }.CreateSource();
            CoreBattleSource far = new BattleLaunch { StartGap = 4 }.CreateSource();

            Assert.That(wide.State.FieldCells, Is.EqualTo(8));
            Assert.That(wide.State.Gap, Is.EqualTo(3));
            Assert.That(narrow.State.Enemy.Cell, Is.EqualTo(5), "cell 6 is off a 5-cell line");
            Assert.That(narrow.State.Gap, Is.EqualTo(2));
            Assert.That(far.State.Enemy.Cell, Is.EqualTo(6), "cell 7 is off a 6-cell line");
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleLaunch { FieldCells = 4 }.CreateSource());
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleLaunch { FieldCells = 9 }.CreateSource());
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
            Assert.That(played.Count(e => e.Kind == DepictionEventKind.PlayCard), Is.EqualTo(3), "shield_bash and body_check reach 0 only; 10 stamina pays for 3 + 3 + 3");
            Assert.That(played.Skip(played.Count - 3).Select(e => e.Kind), Is.EqualTo(new[]
            {
                DepictionEventKind.TurnEnd, DepictionEventKind.EnemyAction, DepictionEventKind.NextOmen,
            }));
            Assert.That(played.Select(e => e.Order), Is.EqualTo(Enumerable.Range(1, played.Count)));

            // The end point (TurnLoopTests.PinnedSummaries, turn 1): the player backed off to gap 3,
            // the sweep whiffed, and the polearm's next omen is its step in (動).
            DepictionFrame last = played.Last().After;
            Assert.That(last.Corner.Turn, Is.EqualTo(1));
            Assert.That(last.Player.RangeGlyph, Is.EqualTo("3"));
            Assert.That(last.Omen.Visible, Is.True);
            Assert.That(last.Omen.KindLabel, Is.EqualTo("動"));
            Assert.That(last.Omen.SideGlyph, Is.EqualTo(""));
            Assert.That(last.Player.Hp, Is.EqualTo(50));
            Assert.That(last.Player.Guard, Is.EqualTo(36));
            Assert.That(last.Enemy.Hp, Is.EqualTo(60));
            Assert.That(last.Enemy.Statuses, Is.Empty);
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
            Assert.That(source.GuideText, Is.EqualTo(""), "#191: the outcome is the end screen's, not the guide line's");
        }
    }
}
