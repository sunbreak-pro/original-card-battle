// A battle with two enemies, written as the script (#589): every frame carries each enemy's figure,
// number panel, stance list and omen, with the stripe direction that tells their cells apart and the
// swatch that ties a panel to its cells; a one-enemy battle carries no swatch. The second enemy's
// events go to its own numbers, never the first's. Runs under `dotnet test`
// (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class TwoEnemyScriptTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static readonly CardDef[] Hand =
        {
            CardCatalog.KesaCut, CardCatalog.Feint, CardCatalog.Brace, CardCatalog.BodyCheck, CardCatalog.StepOutGuard,
        };

        /// <summary>roster §9's pair on an 8-cell line: 錆槍の竜兵 in front, 灰弩の竜兵 behind.</summary>
        private static (BattleState State, CoreScriptWriter Writer, DepictionFrame Opening) RustAndAsh()
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(Hand, 2), 8,
                MoreEnemies: new[] { Enemies.CrossbowHunter });
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(setup.Enemy);
            DepictionFrame opening = writer.Opening(state);
            return (state, writer, opening);
        }

        private static DepictionFrame Last(List<DepictionEvent> events) => events.Last().After;

        [Test]
        public void TwoEnemies_EveryFrame_HoldsBothFiguresPanelsStanceListsAndOmens()
        {
            var (state, writer, opening) = RustAndAsh();
            Assert.That(state.Enemies.Count, Is.EqualTo(2));
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            DepictionFrame frame = Last(writer.Write(begin.Events, begin.State));

            Assert.That(opening.Enemies.Count, Is.EqualTo(2), "the opening already stands both");
            Assert.That(frame.Enemies.Count, Is.EqualTo(2));
            EnemyFrame rust = frame.Enemies[0];
            EnemyFrame ash = frame.Enemies[1];
            EnemyUnit second = begin.State.Enemies[1];
            Assert.Multiple(() =>
            {
                // The first is the frame's own enemy, so a screen that reads one enemy still shows it.
                Assert.That(rust.Unit, Is.SameAs(frame.Enemy));
                Assert.That(rust.Omen, Is.SameAs(frame.Omen));
                Assert.That(rust.Cell, Is.EqualTo(frame.Floor.EnemyCell));
                Assert.That(rust.AimCells, Is.SameAs(frame.Floor.AimCells));

                // The figures: whose art each wears and where each stands.
                Assert.That(rust.Unit.ArtId, Is.EqualTo(Enemies.PolearmWarped.Id));
                Assert.That(ash.Unit.ArtId, Is.EqualTo(Enemies.CrossbowHunter.Id));
                Assert.That(ash.Cell, Is.EqualTo(second.Body.Cell));
                Assert.That(ash.Size, Is.EqualTo(second.Body.Size));
                Assert.That(ash.Cell, Is.GreaterThan(rust.Cell), "the second stands behind the first");

                // The number panels: each enemy's own numbers.
                Assert.That(ash.Unit.Hp, Is.EqualTo(second.Body.Hp));
                Assert.That(ash.Unit.HpMax, Is.EqualTo(second.Body.MaxHp));
                Assert.That(ash.Unit.HpMax, Is.Not.EqualTo(rust.Unit.HpMax), "60 against 81: two panels, not one copied");
                Assert.That(ash.Unit.ShowStamina, Is.True);
                Assert.That(ash.Unit.Stamina, Is.EqualTo(second.Body.Stamina));

                // The stance lists: one per enemy, empty before anyone takes a stance.
                Assert.That(rust.Unit.Stances, Is.Not.Null);
                Assert.That(ash.Unit.Stances, Is.Not.Null);
                Assert.That(ash.Unit.Stances, Is.Not.SameAs(rust.Unit.Stances));
                Assert.That(frame.StanceListsShown, Is.True);

                // The omens: step 5 shows both, each its own enemy's.
                Assert.That(rust.Omen.Visible, Is.True);
                Assert.That(ash.Omen.Visible, Is.True);
                Assert.That(ash.Omen, Is.Not.SameAs(rust.Omen));
                Assert.That(ash.Omen.KindLabel, Is.EqualTo(CoreText.OmenKindWord(second.Omen.Label.Kind)));
                Assert.That(rust.Omen.KindLabel, Is.EqualTo(CoreText.OmenKindWord(begin.State.Enemies[0].Omen.Label.Kind)));

                // battle_ui_ux_v2 §1.4: 45° for the first, −45° for the second, and the swatch on both.
                Assert.That(rust.StripeDirection, Is.EqualTo(0));
                Assert.That(ash.StripeDirection, Is.EqualTo(1));
                Assert.That(rust.Swatch, Is.True);
                Assert.That(ash.Swatch, Is.True);
            });
        }

        [Test]
        public void OneEnemy_CarriesNoSwatch_AndItsOneShareIsTheFramesEnemy()
        {
            BattleState state = TurnLoop.Start(BattleSetup.Slice(), new SeededRng(1)).State;
            var writer = new CoreScriptWriter(state.EnemyDef);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, new SeededRng(1));
            DepictionFrame frame = Last(writer.Write(begin.Events, begin.State));

            Assert.That(frame.Enemies.Count, Is.EqualTo(1));
            Assert.That(frame.Enemies[0].Swatch, Is.False, "one enemy needs nothing to tell its cells apart");
            Assert.That(frame.Enemies[0].StripeDirection, Is.EqualTo(0));
            Assert.That(frame.Enemies[0].Unit, Is.SameAs(frame.Enemy));
            Assert.That(frame.Enemies[0].Omen, Is.SameAs(frame.Omen));
        }

        [Test]
        public void TheSecondEnemysEvents_GoToItsOwnNumbers_TurnAfterTurn()
        {
            var (state, writer, _) = RustAndAsh();
            bool secondActed = false;
            for (int turn = 1; turn <= 4 && state.Result == GameResult.Ongoing; turn++)
            {
                StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
                AssertMatches(Last(writer.Write(begin.Events, begin.State)), begin.State, "turn " + turn + " begun");
                StepResult end = TurnLoop.EndTurn(begin.State, NoShuffle);
                List<DepictionEvent> written = writer.Write(end.Events, end.State);
                AssertMatches(Last(written), end.State, "turn " + turn + " ended");

                // #589: the second enemy's beats say they are its.
                if (end.Events.Any(e => e is ActionExecuted && e.Unit == 1))
                {
                    secondActed = true;
                    Assert.That(written.SelectMany(ev => ev.Cues).Any(c => c.Kind == CueKind.EnemyWindup && c.Unit == 1), Is.True, "turn " + turn);
                }
                state = end.State;
            }
            Assert.That(secondActed, Is.True, "the second enemy acted at least once");
        }

        /// <summary>Both enemies' panels and cells in <paramref name="frame"/> are the core's in <paramref name="state"/>.</summary>
        private static void AssertMatches(DepictionFrame frame, BattleState state, string when)
        {
            Assert.That(frame.Enemies.Count, Is.EqualTo(state.Enemies.Count), when);
            for (int i = 0; i < state.Enemies.Count; i++)
            {
                CombatantState body = state.Enemies[i].Body;
                EnemyFrame enemy = frame.Enemies[i];
                string who = when + ", enemy " + i;
                Assert.That(enemy.Unit.Hp, Is.EqualTo(body.Hp), who);
                Assert.That(enemy.Unit.Guard, Is.EqualTo(body.Guard), who);
                Assert.That(enemy.Unit.Stamina, Is.EqualTo(body.Stamina), who);
                Assert.That(enemy.Cell, Is.EqualTo(body.Cell), who);
            }
        }

        // ---- what the screen acts on (Script/EnemyScreen.cs), read from a fought battle (#589) ----

        /// <summary>
        /// §5.15 順 5 / §2.2 の 6: the fall of an enemy that leaves another standing is a beat naming that enemy,
        /// with no cue added for the screen: the writer has written it since the extension, and the trial
        /// plays the same beat. Enemy k keeps its panel in Enemies[k], Down, and the last fall stands alone.
        /// </summary>
        [Test]
        public void AFall_NamesItsEnemy_AndTheScreenCanTellWhetherOthersStand()
        {
            int nonLastFalls = 0;
            int lastFalls = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                CoreBattleSource source = Pair(seed);
                foreach (DepictionEvent ev in FightOut(source))
                {
                    if (ev.Kind != DepictionEventKind.Defeat) continue;
                    Cue fall = ev.Cues.Single(c => c.Kind == CueKind.Defeat);
                    int unit = EnemyScreen.IndexOf(fall, 2);
                    Assert.That(unit, Is.EqualTo(ev.Unit), "the fall goes to the enemy the event names");
                    Assert.That(ev.After.Enemies[unit].Unit.Down, Is.True, "the frame after it shows that enemy down");
                    Assert.That(EnemyScreen.Stands(ev.After, unit), Is.False);
                    if (EnemyScreen.OthersStand(ev.After, unit))
                    {
                        nonLastFalls++;
                        Assert.That(ev.After.Enemies[1 - unit].Unit.Down, Is.False);
                        Assert.That(ev.After.Outcome, Is.EqualTo(BattleOutcome.Ongoing), "the battle goes on");
                    }
                    else lastFalls++;
                }
            }
            Assert.That(nonLastFalls, Is.GreaterThan(0), "some fight has one enemy fall while the other stands");
            Assert.That(lastFalls, Is.GreaterThan(0), "some fight ends on a fall");
        }

        /// <summary>A move walks the enemy that moved and no other: the cell the cue names is that enemy's in the event's frame, its N the cue's.</summary>
        [Test]
        public void AnEnemysMove_WalksThatEnemy_AndThePlayersMove_WalksNoEnemy()
        {
            int enemyMoves = 0;
            int playerMoves = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                foreach (DepictionEvent ev in FightOut(Pair(seed)))
                {
                    List<Cue> moves = ev.Cues.Where(c => c.Kind == CueKind.RangeSwitch).ToList();
                    foreach (Cue move in moves)
                    {
                        int unit = EnemyScreen.MovedUnit(move, 2, out int cell);
                        if (unit < 0)
                        {
                            Assert.That(move.UnitCellAfter, Is.EqualTo(0));
                            playerMoves++;
                            continue;
                        }
                        enemyMoves++;
                        Assert.That(cell, Is.EqualTo(move.UnitCellAfter));
                        Assert.That(unit, Is.EqualTo(move.Unit));
                        if (moves.Count(m => m.Unit == unit && m.UnitCellAfter > 0) == 1)
                        {
                            Assert.That(ev.After.Enemies[unit].Cell, Is.EqualTo(cell), "the frame stands the enemy where the move left it");
                        }
                    }
                }
            }
            Assert.That(enemyMoves, Is.GreaterThan(0), "the enemies walk");
            Assert.That(playerMoves, Is.GreaterThan(0), "the player walks");
        }

        /// <summary>§2.2 の 5: a held card's frame on each enemy reads that enemy, from the source and the script alone.</summary>
        [Test]
        public void AHeldCard_ReadsEachEnemy_OnItsOwn()
        {
            CoreBattleSource source = Pair(5);
            while (!source.WaitingForPlayer) source.AdvanceAuto();
            DepictionFrame frame = source.Frame;
            bool someCardReachesOneOnly = false;
            foreach (CardFace face in frame.Hand)
            {
                if (face.Aim != CardAim.Single) continue;
                Assert.That(face.ReachOn.Count, Is.EqualTo(2), face.Id);
                for (int k = 0; k < 2; k++)
                {
                    Assert.That(EnemyScreen.Reaches(face, k, 2), Is.EqualTo(face.ReachOn[k]), face.Id);
                    if (!face.ReachOn[k]) Assert.That(source.PreviewFor(face.Id, k), Is.Empty, face.Id + ": nothing to preview out of reach");
                }
                if (face.ReachOn[0] != face.ReachOn[1]) someCardReachesOneOnly = true;
            }
            Assert.That(someCardReachesOneOnly, Is.True, "the enemy behind stands out of some card's reach");
        }

        /// <summary>A card released on a named enemy goes to it, and the event says so.</summary>
        [Test]
        public void ACardReleasedOnAnEnemy_IsPlayedOnThatEnemy()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                // Side by side with the player, so a card of some reach has both enemies to choose from.
                CoreBattleSource source = Pair(seed, startGap: 0);
                while (!source.WaitingForPlayer) source.AdvanceAuto();
                foreach (CardFace face in source.Frame.Hand)
                {
                    if (face.Aim != CardAim.Single) continue;
                    for (int k = 0; k < 2; k++)
                    {
                        if (source.Inspect(face.Id, k) != PlayVerdict.Accepted || k == face.TargetUnit) continue;
                        Assert.That(source.TryPlay(face.Id, DropZone.Receiver, k, out DepictionEvent played), Is.EqualTo(PlayVerdict.Accepted));
                        Assert.That(played.Unit, Is.EqualTo(k), face.Id);
                        return;
                    }
                }
            }
            Assert.Fail("no hand had a card playable on an enemy other than the one it would go to unnamed");
        }

        private static CoreBattleSource Pair(int seed, int startGap = BattleLaunch.DefaultStartGap)
        {
            return new BattleLaunch
            {
                EnemyId = Enemies.PolearmWarped.Id,
                MoreEnemyIds = new List<string> { Enemies.CrossbowHunter.Id },
                FieldCells = 8,
                StartGap = startGap,
                Seed = seed,
                AutoPlay = true,
            }.CreateSource();
        }

        /// <summary>Every event of the unattended fight, the turn starts and the enemy's beats and the cards alike.</summary>
        private static IEnumerable<DepictionEvent> FightOut(CoreBattleSource source)
        {
            for (int guard = 0; guard < 2000 && !source.Finished; guard++)
            {
                if (!source.WaitingForPlayer)
                {
                    yield return source.AdvanceAuto();
                    continue;
                }
                string id = source.SuggestedCardId;
                if (id.Length == 0)
                {
                    yield return source.EndTurn();
                    continue;
                }
                source.TryPlay(id, DepictionText.RequiredZone(DepictionText.Find(source.Frame.Hand, id).Aim), out DepictionEvent played);
                yield return played;
            }
        }
    }
}
