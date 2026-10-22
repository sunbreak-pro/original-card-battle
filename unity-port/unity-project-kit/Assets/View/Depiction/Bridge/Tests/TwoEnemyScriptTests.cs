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
    }
}
