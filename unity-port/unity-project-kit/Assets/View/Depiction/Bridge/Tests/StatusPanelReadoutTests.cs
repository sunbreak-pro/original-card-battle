// #455: what the writer puts on the script for battle-visual-v1 §4.5's enemy number panel — the
// stamina it holds (a dot and the number), the 構えの盾 (solid while that stamina meets the rule's
// threshold, 残り 3 以上, dashed below it) and the elite's phosphor frame. Runs under `dotnet test`
// (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class StatusPanelReadoutTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static readonly CardDef[] Hand =
        {
            CardCatalog.KesaCut, CardCatalog.Feint, CardCatalog.Brace, CardCatalog.ReachThrust, CardCatalog.BackLeap,
        };

        private static BattleState Started(EnemyDef enemy)
        {
            var setup = new BattleSetup(enemy, Cards.BuildDeck(Hand, 1), BattleSetup.SliceFieldCells, StartGap: 2);
            return TurnLoop.Start(setup, NoShuffle).State;
        }

        private static DepictionFrame OpeningWith(EnemyDef enemy, int stamina)
        {
            BattleState start = Started(enemy);
            BattleState drained = start.WithEnemy(start.Enemy with { Stamina = stamina });
            return new CoreScriptWriter(enemy).Opening(drained);
        }

        [Test]
        public void TheEnemysStamina_IsShownOnItsPanel()
        {
            BattleState start = Started(Enemies.PolearmWarped);
            DepictionFrame frame = new CoreScriptWriter(Enemies.PolearmWarped).Opening(start);
            Assert.That(frame.Enemy.ShowStamina, Is.True);
            Assert.That(frame.Enemy.Stamina, Is.EqualTo(start.Enemy.Stamina));
            Assert.That(frame.Enemy.StaminaMax, Is.EqualTo(10));
        }

        [TestCase(10, true)]
        [TestCase(3, true)]
        [TestCase(2, false)]
        [TestCase(0, false)]
        public void TheReserveShield_IsSolidFromThreeStaminaOn(int stamina, bool solid)
        {
            DepictionFrame frame = OpeningWith(Enemies.PolearmWarped, stamina);
            Assert.That(frame.Enemy.Stamina, Is.EqualTo(stamina));
            Assert.That(frame.Enemy.ReserveLikely, Is.EqualTo(solid));
            Assert.That(Constants.ReserveThreshold, Is.EqualTo(3), "the threshold §4.5 and battle_ui_ux_v2 §1.1 B5 name");
        }

        [Test]
        public void ThePlayersPanel_HasNoReserveShield()
        {
            DepictionFrame frame = OpeningWith(Enemies.PolearmWarped, 10);
            Assert.That(frame.Player.ReserveLikely, Is.False, "the player's 構え is the hint by the end-turn plate (§4.9)");
        }

        [Test]
        public void AfterTheEnemysPhase_ThePanelShowsWhatItHoldsAndTheShieldFollows()
        {
            BattleState start = Started(Enemies.PolearmWarped);
            var writer = new CoreScriptWriter(Enemies.PolearmWarped);
            writer.Opening(start);
            StepResult begin = TurnLoop.BeginPlayerTurn(start, NoShuffle);
            writer.Write(begin.Events, begin.State);
            StepResult end = TurnLoop.EndTurn(begin.State, NoShuffle);
            List<DepictionEvent> written = writer.Write(end.Events, end.State);

            DepictionFrame last = written.Last().After;
            Assert.That(last.Enemy.Stamina, Is.EqualTo(end.State.Enemy.Stamina));
            Assert.That(last.Enemy.ReserveLikely, Is.EqualTo(end.State.Enemy.Stamina >= Constants.ReserveThreshold));
        }

        [Test]
        public void AnElitesPanel_IsMarkedElite_AndANormalOnesIsNot()
        {
            Assert.That(OpeningWith(Enemies.PackAlpha, 12).Enemy.Elite, Is.True);
            Assert.That(OpeningWith(Enemies.PolearmWarped, 10).Enemy.Elite, Is.False);
            Assert.That(OpeningWith(Enemies.MiasmaPriest, 14).Enemy.Elite, Is.False, "§4.5 names the elite's frame only");
        }
    }
}
