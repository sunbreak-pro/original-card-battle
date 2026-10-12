// #501: what the core's script hands the screen for battle_ui_ux_v2 §5.11 順 2 (the body's light while a
// stance is held) and §5.13 順 1 (構え's pips). The light is read off each frame's stance list
// (UnitFrame.Stances) with StanceGlow, the rule DepictionPlayer applies when a frame lands: these follow
// a real fight's frames from before the first stance to the end of the turn. Runs under `dotnet test`
// (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class StanceGlowReadoutTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        /// <summary>Dealt from the front (NoShuffle): 岩の構え, 水の構え, then cards to fill the hand.</summary>
        private static List<CardInstance> Deck() => new List<CardInstance>
        {
            new CardInstance("rock-0", CardCatalog.RockStance),
            new CardInstance("water-0", CardCatalog.WaterStance),
            new CardInstance("kesa-0", CardCatalog.KesaCut),
            new CardInstance("feint-0", CardCatalog.Feint),
            new CardInstance("brace-0", CardCatalog.Brace),
            new CardInstance("kesa-1", CardCatalog.KesaCut),
            new CardInstance("brace-1", CardCatalog.Brace),
        };

        private static BattleSetup Setup() => new BattleSetup(Enemies.PolearmWarped, Deck(), BattleSetup.SliceFieldCells, StartGap: 2);

        /// <summary>The light as the screen keeps it: each landing frame moves it as StanceGlow.Change says.</summary>
        private sealed class Light
        {
            public bool Shown;

            public StanceGlowChange Land(UnitFrame unit)
            {
                StanceGlowChange change = StanceGlow.Change(Shown, unit);
                if (change != StanceGlowChange.Keep) Shown = change == StanceGlowChange.Light;
                return change;
            }
        }

        [Test]
        public void TheBodyLight_IsOnFromTheFirstStanceToTheEndOfTheTurn_AndOffBefore()
        {
            BattleState state = TurnLoop.Start(Setup(), NoShuffle).State;
            var writer = new CoreScriptWriter(Enemies.PolearmWarped);
            var player = new Light();
            var enemy = new Light();

            DepictionFrame opening = writer.Opening(state);
            Assert.That(player.Land(opening.Player), Is.EqualTo(StanceGlowChange.Keep), "no stance, no light");
            Assert.That(player.Shown, Is.False);

            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            foreach (DepictionEvent ev in writer.Write(begin.Events, begin.State))
            {
                player.Land(ev.After.Player);
                Assert.That(player.Shown, Is.False, "the turn start puts down no stance: " + ev.Title);
            }

            // The first stance: the 型 crest is drawn on the player, and the frame after it lights the body.
            StepResult rock = TurnLoop.PlayCard(begin.State, "rock-0", NoShuffle);
            DepictionEvent placed = writer.Write(rock.Events, rock.State).Last();
            Assert.That(placed.Cues.Any(c => c.Target == UnitSide.Player && c.Fx == FxSystem.Form), Is.True,
                "the 型 beat the light opens with (§5.11 順 2 足元に紋、身体に薄い光)");
            Assert.That(StanceGlow.CrestCarriesLight(player.Shown), Is.True, "the first crest brings its body light");
            Assert.That(player.Land(placed.After.Player), Is.EqualTo(StanceGlowChange.Light));
            Assert.That(enemy.Land(placed.After.Enemy), Is.EqualTo(StanceGlowChange.Keep), "the player's stance lights the player only");

            // A second stance adds no second light: its crest is drawn alone, and the kept light stays as it is.
            StepResult water = TurnLoop.PlayCard(rock.State, "water-0", NoShuffle);
            DepictionEvent second = writer.Write(water.Events, water.State).Last();
            Assert.That(second.Cues.Any(c => c.Target == UnitSide.Player && c.Fx == FxSystem.Form), Is.True, "the crest is drawn again");
            Assert.That(StanceGlow.CrestCarriesLight(player.Shown), Is.False, "2 つ目からは光を重ねず、紋だけを出し直す");
            Assert.That(player.Land(second.After.Player), Is.EqualTo(StanceGlowChange.Keep));
            Assert.That(player.Shown, Is.True);

            // The stances last the battle (battle_core_v4 §4): through the turn end and the enemy's turn the light stays.
            StepResult end = TurnLoop.EndTurn(water.State, NoShuffle);
            List<DepictionEvent> enemyTurn = writer.Write(end.Events, end.State);
            Assert.That(enemyTurn, Is.Not.Empty);
            foreach (DepictionEvent ev in enemyTurn)
            {
                Assert.That(player.Land(ev.After.Player), Is.EqualTo(StanceGlowChange.Keep), ev.Title);
                Assert.That(player.Shown, Is.True, "the light stays while the list holds a stance: " + ev.Title);
            }

            // The list empties only when a new battle opens. The screen does not fade the light then: it
            // snaps each light to the opening frame (DepictionPlayer.SnapStanceGlows, StanceGlow.IsOn) before
            // the frame lands, so it is off at once. §5.18 順 3's fade on the carry-over panel is the run
            // screen's, not built yet.
            DepictionFrame next = new CoreScriptWriter(Enemies.PolearmWarped).Opening(TurnLoop.Start(Setup(), NoShuffle).State);
            Assert.That(StanceGlow.IsOn(next.Player), Is.False, "a new battle snaps the light off");
            Assert.That(StanceGlow.IsOn(next.Enemy), Is.False);
        }

        [Test]
        public void TheReserveAtTurnEnd_GlowsThePipsBeforeTheShield_AndTheTurnEndFitsTwoSeconds()
        {
            BattleState state = TurnLoop.Start(Setup(), NoShuffle).State;
            var writer = new CoreScriptWriter(Enemies.PolearmWarped);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);
            Assert.That(begin.State.Player.Stamina, Is.GreaterThanOrEqualTo(Constants.ReserveThreshold), "nothing played: 構え holds");

            StepResult end = TurnLoop.EndTurn(begin.State, NoShuffle);
            DepictionEvent turnEnd = writer.Write(end.Events, end.State).First(e => e.Kind == DepictionEventKind.TurnEnd);
            Assert.That(turnEnd.Cues.Any(c => c.Kind == CueKind.StanceCue), Is.True);

            List<EffectId> steps = EffectPlan.StepsOf(turnEnd).Select(s => s.Id).ToList();
            int pips = steps.IndexOf(EffectId.ReservePips);
            Assert.That(pips, Is.GreaterThanOrEqualTo(0), "§5.13 順 1: the reserve beat glows pips 1-3");
            Assert.That(steps[pips + 1], Is.EqualTo(EffectId.StanceCue));
            float ms = EffectPlan.BlockingMs(turnEnd, EffectSwitches.AllOn());
            TestContext.Out.WriteLine("turn end with 構え: " + ms.ToString("0") + " ms [" + string.Join(", ", steps) + "]");
            Assert.That(ms, Is.LessThan(BattleSpeed.EventCapMs));
        }
    }
}
