// #455: what the writer puts on the script for battle-visual-v1 §4.6's stance lists — every stance a
// side holds (battle_core_v4 §4 v4.4: a list with no cap), in the order battle_ui_ux_v2 §3.7 lists
// them, a stance put down again counted on one row and moved to the end. Runs under `dotnet test`
// (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class StanceListReadoutTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        /// <summary>Dealt from the front (NoShuffle): 岩の構え, 水の構え, 岩の構え, then three cards to fill the hand.</summary>
        private static List<CardInstance> Deck() => new List<CardInstance>
        {
            new CardInstance("rock-0", CardCatalog.RockStance),
            new CardInstance("water-0", CardCatalog.WaterStance),
            new CardInstance("rock-1", CardCatalog.RockStance),
            new CardInstance("kesa-0", CardCatalog.KesaCut),
            new CardInstance("feint-0", CardCatalog.Feint),
            new CardInstance("brace-0", CardCatalog.Brace),
            new CardInstance("kesa-1", CardCatalog.KesaCut),
        };

        private static (CoreScriptWriter Writer, BattleState State) Begun()
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, Deck(), BattleSetup.SliceFieldCells, StartGap: 2);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(Enemies.PolearmWarped);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);
            return (writer, begin.State);
        }

        private static (DepictionFrame Frame, BattleState State) Play(CoreScriptWriter writer, BattleState state, string id)
        {
            StepResult played = TurnLoop.PlayCard(state, id, NoShuffle);
            Assert.That(played.State.Hand.Any(c => c.InstanceId == id), Is.False, id + " was played");
            return (writer.Write(played.Events, played.State).Last().After, played.State);
        }

        private static string[] Names(UnitFrame unit) => unit.Stances.Select(r => r.Name + "×" + r.Count).ToArray();

        [Test]
        public void TheListsAreShown_AndEmptyBeforeAnyStance()
        {
            BattleState start = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, Deck(), BattleSetup.SliceFieldCells, StartGap: 2), NoShuffle).State;
            DepictionFrame opening = new CoreScriptWriter(Enemies.PolearmWarped).Opening(start);
            Assert.That(opening.StanceListsShown, Is.True);
            Assert.That(opening.Player.Stances, Is.Empty, "§4.6 空きの枠");
            Assert.That(opening.Enemy.Stances, Is.Empty);
        }

        [Test]
        public void EachStancePutDown_JoinsTheEndOfTheList()
        {
            var (writer, state) = Begun();
            var (afterRock, s1) = Play(writer, state, "rock-0");
            Assert.That(Names(afterRock.Player), Is.EqualTo(new[] { "岩の構え×1" }));
            var (afterWater, _) = Play(writer, s1, "water-0");
            Assert.That(Names(afterWater.Player), Is.EqualTo(new[] { "岩の構え×1", "水の構え×1" }), "the newest last");
            Assert.That(afterWater.Enemy.Stances, Is.Empty, "the player's stances stay on the player's side");
        }

        [Test]
        public void TheSameStancePutDownAgain_IsCountedOnOneRow_AndMovesToTheEnd()
        {
            var (writer, state) = Begun();
            var (_, s1) = Play(writer, state, "rock-0");
            var (_, s2) = Play(writer, s1, "water-0");
            var (frame, s3) = Play(writer, s2, "rock-1");
            Assert.That(s3.Player.StanceList.Count, Is.EqualTo(3), "the core holds the repeat as a second entry (§4)");
            Assert.That(Names(frame.Player), Is.EqualTo(new[] { "水の構え×1", "岩の構え×2" }),
                "battle_ui_ux_v2 §3.7: the repeat is not listed twice; it moves to the end with ×2");
        }

        [Test]
        public void TheListOutlivesTheTurn()
        {
            var (writer, state) = Begun();
            var (_, s1) = Play(writer, state, "rock-0");
            StepResult end = TurnLoop.EndTurn(s1, NoShuffle);
            DepictionFrame last = writer.Write(end.Events, end.State).Last().After;
            Assert.That(Names(last.Player), Is.EqualTo(new[] { "岩の構え×1" }), "a stance lasts the battle (§4)");
        }

        [Test]
        public void TheStancesHeld_AreNotStatusChips()
        {
            // battle-visual-v1 §4.5 / §4.6 (#605): the stance frames carry the stances, so the number
            // panel's status row keeps its places for the words (no ChipKind.Lasting chip).
            var (writer, state) = Begun();
            var (_, s1) = Play(writer, state, "rock-0");
            var (_, s2) = Play(writer, s1, "water-0");
            var (frame, _) = Play(writer, s2, "rock-1");
            Assert.That(frame.Player.Stances, Is.Not.Empty);
            foreach (UnitFrame unit in new[] { frame.Player, frame.Enemy })
            {
                Assert.That(unit.Statuses.Select(c => c.Kind), Has.None.EqualTo(ChipKind.Lasting));
                Assert.That(unit.Statuses.Select(c => c.Label), Has.None.StartsWith("構え・"));
            }
        }

        [Test]
        public void EachRow_CarriesItsHoverPanelText()
        {
            // §4.6 / §7.3 永続の状態 (#605): the bridge writes 効き方 and 減り方 on the row; the frame only prints them.
            var (writer, state) = Begun();
            var (_, s1) = Play(writer, state, "rock-0");
            var (_, s2) = Play(writer, s1, "water-0");
            var (frame, _) = Play(writer, s2, "rock-1");
            StanceRow water = frame.Player.Stances.Single(r => r.Name == "水の構え");
            StanceRow rock = frame.Player.Stances.Single(r => r.Name == "岩の構え");
            Assert.That(water.Effect, Is.EqualTo(StatusChipText.StanceChip("水の構え", 1).Effect));
            Assert.That(rock.Effect, Is.EqualTo(StatusChipText.StanceChip("岩の構え", 2).Effect));
            Assert.That(rock.Effect, Does.Contain("2 倍効く"), "a stance held twice says it works twice");
            Assert.That(frame.Player.Stances.Select(r => r.Decay), Has.All.EqualTo("減らない（永続）"));
        }
    }
}
