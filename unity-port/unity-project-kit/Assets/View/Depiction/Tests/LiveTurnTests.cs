using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Depiction.Tests
{
    /// <summary>
    /// The hand is free (issue #36): a card comes back only because of stamina, the range side or
    /// where it was released — never because a script wanted another card first.
    /// </summary>
    public class LiveTurnTests
    {
        [Test]
        public void TheHandCanBePlayedInAnyOrder()
        {
            LiveTurn turn = OpeningHandWith(DemoDeck.Kesagiri, DemoDeck.Daijodan);

            // The filmed slice plays 袈裟斬り first. Here the heavy cut goes first and is still accepted.
            Play(turn, DemoDeck.Daijodan, DropZone.Receiver);
            Assert.That(turn.Frame.Enemy.Hp, Is.EqualTo(47));

            // 初手 +3 has already been spent by the card before it, so this one lands for 6.
            Play(turn, DemoDeck.Kesagiri, DropZone.Receiver);
            Assert.That(turn.Frame.Enemy.Hp, Is.EqualTo(41));
        }

        [Test]
        public void NoCardIsEverRefusedForBeingOutOfScriptOrder()
        {
            LiveTurn turn = Opened();

            foreach (CardFace face in turn.Frame.Hand)
            {
                Assert.That(turn.Inspect(face.Id), Is.Not.EqualTo(PlayVerdict.OffScript), face.Id);
            }
        }

        [Test]
        public void ACardCostingMoreThanTheStaminaLeftComesBackSayingSo()
        {
            var turn = new LiveTurn(7);
            string tooDear = PlayUntilRefusedFor(turn, PlayVerdict.NotEnoughStamina);

            Assert.That(turn.TryPlay(tooDear, DropZone.AboveThrowLine, out DepictionEvent played),
                Is.EqualTo(PlayVerdict.NotEnoughStamina));
            Assert.That(played, Is.Null);
            Assert.That(turn.RefusalText(tooDear, PlayVerdict.NotEnoughStamina), Does.Contain("スタミナ"));
        }

        [Test]
        public void ACardThatNeedsTheOtherSideComesBackSayingSo()
        {
            LiveTurn turn = OpeningHandWith(DemoDeck.Ushirotobi, DemoDeck.Daijodan);
            Play(turn, DemoDeck.Ushirotobi, DropZone.AboveThrowLine);
            Assert.That(turn.Frame.Player.Range, Is.EqualTo(RangeSide.Far));

            string daijodan = IdOf(turn, DemoDeck.Daijodan);
            Assert.That(turn.Inspect(daijodan), Is.EqualTo(PlayVerdict.OutOfRange));
            Assert.That(turn.RefusalText(daijodan, PlayVerdict.OutOfRange), Does.Contain("間"));
        }

        [Test]
        public void ASelfCardStillNeedsTheThrowLine()
        {
            LiveTurn turn = OpeningHandWith(DemoDeck.TetsuNoUke);
            string tetsu = IdOf(turn, DemoDeck.TetsuNoUke);

            Assert.That(turn.TryPlay(tetsu, DropZone.Receiver, out _), Is.EqualTo(PlayVerdict.WrongZone));
            Assert.That(turn.TryPlay(tetsu, DropZone.None, out _), Is.EqualTo(PlayVerdict.WrongZone));
            Assert.That(turn.TryPlay(tetsu, DropZone.AboveThrowLine, out _), Is.EqualTo(PlayVerdict.Accepted));
        }

        [Test]
        public void EndingTheTurnTakesTheStanceShieldAndHandsOverToTheEnemy()
        {
            LiveTurn turn = Opened();
            Assert.That(turn.CanEndTurn, Is.True);

            DepictionEvent end = turn.EndTurn();
            Assert.That(end.Kind, Is.EqualTo(DepictionEventKind.TurnEnd));
            Assert.That(turn.Frame.Player.Guard, Is.EqualTo(LiveTurn.StanceGuard));
            Assert.That(turn.Frame.Hand, Is.Empty);
            Assert.That(turn.CanEndTurn, Is.False);

            // Caught on the side the omen names: 13 + 5, of which the stance shield eats 3.
            DepictionEvent enemy = turn.AdvanceAuto();
            Assert.That(enemy.Kind, Is.EqualTo(DepictionEventKind.EnemyAction));
            Assert.That(turn.Frame.Player.Hp, Is.EqualTo(LiveTurn.PlayerMaxHp - 15));
        }

        [Test]
        public void TheFilmedSliceReplaysWithTheSameNumbersWhenItsThreeCardsArePlayed()
        {
            LiveTurn turn = OpeningHandWith(DemoDeck.Kesagiri, DemoDeck.Daijodan, DemoDeck.Ushirotobi);

            Play(turn, DemoDeck.Kesagiri, DropZone.Receiver);     // 6 + 初手 3
            Assert.That(turn.Frame.Enemy.Hp, Is.EqualTo(51));
            Play(turn, DemoDeck.Daijodan, DropZone.Receiver);     // 13
            Assert.That(turn.Frame.Enemy.Hp, Is.EqualTo(38));
            Play(turn, DemoDeck.Ushirotobi, DropZone.AboveThrowLine); // Guard 4 + 予兆 3, step back
            Assert.That(turn.Frame.Player.Guard, Is.EqualTo(7));
            Assert.That(turn.Frame.Player.Range, Is.EqualTo(RangeSide.Far));

            turn.EndTurn();
            Assert.That(turn.Frame.Player.Guard, Is.EqualTo(10));

            DepictionEvent enemy = turn.AdvanceAuto();
            Assert.That(turn.Frame.Player.Hp, Is.EqualTo(47), "13 - 10 through the shield");
            Assert.That(enemy.Cues.Exists(c => c.Kind == CueKind.SideBonusMiss), Is.True, "the +5 should have missed");
        }

        [Test]
        public void AnEnemyThatMissedItsReachClosesTheDistance()
        {
            LiveTurn turn = OpeningHandWith(DemoDeck.Ushirotobi);
            Play(turn, DemoDeck.Ushirotobi, DropZone.AboveThrowLine);
            turn.EndTurn();

            DepictionEvent enemy = turn.AdvanceAuto();
            Cue closing = enemy.Cues.Find(c => c.Kind == CueKind.RangeSwitch);
            Assert.That(closing, Is.Not.Null, "the enemy should step in after missing");
            Assert.That(closing.RangeAfter, Is.EqualTo(RangeSide.Near));
            Assert.That(closing.RangeGlyphAfter, Is.EqualTo("近"));
            Assert.That(turn.Frame.Player.Range, Is.EqualTo(RangeSide.Near), "so the player can back away again");
        }

        [Test]
        public void EveryTurnDealsAFreshHandAndReturnsStamina()
        {
            LiveTurn turn = Opened();
            int handSize = turn.Frame.Hand.Count;
            PlayEverythingPlayable(turn);
            turn.EndTurn();
            turn.AdvanceAuto(); // enemy
            turn.AdvanceAuto(); // next omen

            DepictionEvent start = turn.AdvanceAuto();
            Assert.That(start.Kind, Is.EqualTo(DepictionEventKind.TurnStart));
            Assert.That(turn.Frame.Hand.Count, Is.EqualTo(handSize));
            Assert.That(turn.Frame.Corner.Turn, Is.EqualTo(2));
            Assert.That(turn.Frame.Player.Guard, Is.EqualTo(0), "Guard does not carry over");
        }

        [Test]
        public void EveryGeneratedCueCarriesItsSettledValueSoTheViewNeverComputes()
        {
            foreach (DepictionEvent ev in FightToTheEnd(new LiveTurn(7)))
            {
                Assert.That(ev.After, Is.Not.Null, "event " + ev.Order);
                foreach (Cue cue in ev.Cues)
                {
                    string where = "event " + ev.Order + " " + cue.Kind;
                    Assert.That(cue.Intensity, Is.InRange(1, 4), where);
                    if (cue.Kind == CueKind.Hit) Assert.That(cue.HpAfter, Is.Not.EqualTo(Cue.Unchanged), where);
                    if (cue.Kind == CueKind.Slash)
                    {
                        Assert.That(cue.Source.HasValue, Is.True, where);
                        Assert.That(cue.Source.Value, Is.Not.EqualTo(cue.Target), where);
                    }
                    if (cue.Kind == CueKind.GuardGain || cue.Kind == CueKind.GuardBlock
                        || cue.Kind == CueKind.StanceCue || cue.Kind == CueKind.GuardReset)
                    {
                        Assert.That(cue.GuardAfter, Is.Not.EqualTo(Cue.Unchanged), where);
                    }
                    if (cue.Kind == CueKind.StaminaChange)
                    {
                        Assert.That(cue.StaminaAfter, Is.Not.EqualTo(Cue.Unchanged), where);
                        Assert.That(cue.StaminaMax, Is.Not.EqualTo(Cue.Unchanged), where);
                    }
                    if (cue.Kind == CueKind.RangeSwitch) Assert.That(cue.RangeGlyphAfter, Is.Not.Empty, where);
                }
                foreach (CardFace face in ev.After.Hand)
                {
                    Assert.That(face.TypeLabel, Is.Not.Empty, face.Id);
                    Assert.That(face.Description, Is.Not.Empty, face.Id);
                }
                Assert.That(ev.After.Player.RangeGlyph, Is.EqualTo(DemoDeck.Glyph(ev.After.Player.Range)));
            }
        }

        [Test]
        public void TheFightReachesAnEndAndSaysWhichWay()
        {
            var turn = new LiveTurn(7);
            FightToTheEnd(turn);

            Assert.That(turn.Finished, Is.True);
            Assert.That(turn.PlayerWon ? turn.Frame.Enemy.Hp : turn.Frame.Player.Hp, Is.EqualTo(0));
            Assert.That(turn.GuideText, Is.Not.Empty);
        }

        [Test]
        public void ThePlayerCanAlwaysEndTheTurnEvenWithNothingPlayable()
        {
            LiveTurn turn = Opened();
            PlayEverythingPlayable(turn);

            foreach (CardFace face in turn.Frame.Hand)
            {
                Assert.That(turn.Inspect(face.Id), Is.Not.EqualTo(PlayVerdict.Accepted));
            }
            Assert.That(turn.CanEndTurn, Is.True);
            Assert.That(turn.GuideText, Does.Contain("ターン終了"));
        }

        // ---- helpers -----------------------------------------------------------------------

        /// <summary>A turn opened on its first player phase.</summary>
        private static LiveTurn Opened()
        {
            var turn = new LiveTurn(7);
            turn.AdvanceAuto();
            return turn;
        }

        /// <summary>
        /// The first seed whose opening hand holds all of these kinds. The deck is shuffled, so a test
        /// that needs a named card asks for a deal that has it instead of assuming one.
        /// </summary>
        private static LiveTurn OpeningHandWith(params string[] defIds)
        {
            for (int seed = 0; seed < 500; seed++)
            {
                var turn = new LiveTurn(seed);
                turn.AdvanceAuto();
                bool all = true;
                foreach (string defId in defIds)
                {
                    if (Find(turn, defId) == null) { all = false; break; }
                }
                if (all) return turn;
            }
            throw new InvalidOperationException("no seed deals " + string.Join(", ", defIds));
        }

        private static string Find(LiveTurn turn, string defId)
        {
            foreach (CardFace face in turn.Frame.Hand)
            {
                if (DemoDeck.DefIdOf(face.Id) == defId) return face.Id;
            }
            return null;
        }

        private static string IdOf(LiveTurn turn, string defId)
        {
            string id = Find(turn, defId);
            Assert.That(id, Is.Not.Null, defId + " is not in the hand");
            return id;
        }

        private static void Play(LiveTurn turn, string defId, DropZone zone)
        {
            string id = IdOf(turn, defId);
            Assert.That(turn.TryPlay(id, zone, out DepictionEvent played), Is.EqualTo(PlayVerdict.Accepted), defId);
            Assert.That(played, Is.Not.Null);
            Assert.That(played.CardId, Is.EqualTo(id));
        }

        /// <summary>
        /// Fights greedily until a card in the hand is refused for <paramref name="want"/>, and returns
        /// it. Draining the stamina takes more than one turn, so the test plays the fight rather than
        /// reaching into the turn's state.
        /// </summary>
        private static string PlayUntilRefusedFor(LiveTurn turn, PlayVerdict want)
        {
            for (int guard = 0; guard < 400 && !turn.Finished; guard++)
            {
                if (!turn.WaitingForPlayer)
                {
                    turn.AdvanceAuto();
                    continue;
                }
                foreach (CardFace face in turn.Frame.Hand)
                {
                    if (turn.Inspect(face.Id) == want) return face.Id;
                }
                bool played = false;
                foreach (CardFace face in new List<CardFace>(turn.Frame.Hand))
                {
                    if (turn.Inspect(face.Id) != PlayVerdict.Accepted) continue;
                    turn.TryPlay(face.Id, DepictionText.RequiredZone(face.Aim), out _);
                    played = true;
                    break;
                }
                if (!played) turn.EndTurn();
            }
            Assert.Fail("no card was ever refused for " + want);
            return null;
        }

        /// <summary>Plays cards until nothing in the hand can be played.</summary>
        private static void PlayEverythingPlayable(LiveTurn turn)
        {
            bool played = true;
            while (played && turn.WaitingForPlayer)
            {
                played = false;
                foreach (CardFace face in new List<CardFace>(turn.Frame.Hand))
                {
                    if (turn.Inspect(face.Id) != PlayVerdict.Accepted) continue;
                    turn.TryPlay(face.Id, DepictionText.RequiredZone(face.Aim), out _);
                    played = true;
                    break;
                }
            }
        }

        /// <summary>Plays the whole fight out, greedily, and returns every event it produced.</summary>
        private static List<DepictionEvent> FightToTheEnd(LiveTurn turn)
        {
            var events = new List<DepictionEvent>();
            for (int guard = 0; guard < 400 && !turn.Finished; guard++)
            {
                if (turn.WaitingForPlayer)
                {
                    bool played = false;
                    foreach (CardFace face in new List<CardFace>(turn.Frame.Hand))
                    {
                        if (turn.Inspect(face.Id) != PlayVerdict.Accepted) continue;
                        turn.TryPlay(face.Id, DepictionText.RequiredZone(face.Aim), out DepictionEvent ev);
                        events.Add(ev);
                        played = true;
                        break;
                    }
                    if (!played) events.Add(turn.EndTurn());
                }
                else
                {
                    events.Add(turn.AdvanceAuto());
                }
            }
            Assert.That(turn.Finished, Is.True, "the fight never ended");
            return events;
        }
    }
}
