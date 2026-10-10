using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace BattleCore.Tests
{
    /// <summary>
    /// #334: the second half of 歪みの根's root_st (enemy_roster_v4 §6.4, battle_core_v4 §4). The
    /// 枯らしの息 the root takes under root_st stops the player's newest permanent effect when it
    /// lands: for the rest of that turn and the whole next one, after which it works again.
    /// </summary>
    public class StanceStopTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private const int Cells = 8;

        private static readonly CardDef Older = Fixtures.Card(
            "older_stance", 1, new Face(Stance: new StanceDef(StanceHook.TurnStart, Guard: 1)), BattleAttribute.Stance, targets: TargetKind.Self);

        private static readonly CardDef Newer = Fixtures.Card(
            "newer_stance", 1, new Face(Stance: new StanceDef(StanceHook.TurnStart, Guard: 2)), BattleAttribute.Stance, targets: TargetKind.Self);

        private static readonly CardDef Filler = Fixtures.Card("filler", face: new Face(Guard: 1), attributes: BattleAttribute.Guard, targets: TargetKind.Self);

        [Test]
        public void RootSt_TheBreathLanding_StopsTheNewestStance_ForTheNextTurn_ThenItIsBack()
        {
            // Turn 1: the player places two stances; at step 12 root_st holds and the breath goes on top.
            var s = PlaceTwoStances();
            var turn1 = TurnLoop.EndTurn(s, NoShuffle);
            Assert.That(turn1.State.Enemies[0].ActiveSwitch, Is.EqualTo("root_st"));
            Assert.That(turn1.State.Omen!.ActionId, Is.EqualTo("wither_breath"));
            Assert.That(turn1.Events.OfType<StanceStopped>(), Is.Empty, "the breath of turn 1 is the base tree's, which stops nothing");

            // Turn 2: the breath lands at gap 3 (2〜4) and stops the newer stance through turn 3.
            var turn2Start = TurnLoop.BeginPlayerTurn(turn1.State, NoShuffle);
            Assert.That(Fired(turn2Start.Events), Is.EqualTo(new[] { "older_stance", "newer_stance" }), "both work before the breath");
            var turn2 = TurnLoop.EndTurn(turn2Start.State, NoShuffle);
            var stopped = turn2.Events.OfType<StanceStopped>().ToList();

            // Turn 3: the newer one does nothing; the older one still works.
            var turn3Start = TurnLoop.BeginPlayerTurn(turn2.State, NoShuffle);
            var turn3 = TurnLoop.EndTurn(turn3Start.State, NoShuffle);

            // Turn 4: it is back, and says so before the turn-start stances fire.
            var turn4Start = TurnLoop.BeginPlayerTurn(turn3.State, NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(stopped, Is.EqualTo(new[] { new StanceStopped(Actor.Player, "newer_stance", 1, 3) { Unit = 0 } }));
                Assert.That(turn2.State.Player.StanceList.Count, Is.EqualTo(2), "a stopped stance stays on the list");
                Assert.That(turn2.State.Player.StanceList[1].StoppedThrough, Is.EqualTo(3));

                Assert.That(turn3Start.State.Turn, Is.EqualTo(3));
                Assert.That(Fired(turn3Start.Events), Is.EqualTo(new[] { "older_stance" }), "the newest one is stopped in the next turn");
                Assert.That(turn3Start.State.Player.Guard, Is.EqualTo(1), "only the older stance's Guard +1");
                Assert.That(turn3Start.Events.OfType<StanceResumed>(), Is.Empty);
                Assert.That(turn3.Events.OfType<StanceStopped>(), Is.Empty, "root_st does not hold again: no stance was placed in turn 2");

                Assert.That(turn4Start.Events.OfType<StanceResumed>(), Is.EqualTo(new[] { new StanceResumed(Actor.Player, "newer_stance", 1) { Unit = 0 } }));
                Assert.That(Fired(turn4Start.Events), Is.EqualTo(new[] { "older_stance", "newer_stance" }), "both work again");
                Assert.That(turn4Start.State.Player.Guard, Is.EqualTo(3));
                Assert.That(turn4Start.State.Player.StanceList.All(entry => entry.StoppedThrough == 0), Is.True);
                int resumedAt = turn4Start.Events.ToList().FindIndex(e => e is StanceResumed);
                int firedAt = turn4Start.Events.ToList().FindIndex(e => e is StanceFired);
                Assert.That(resumedAt, Is.LessThan(firedAt), "back before the turn-start stances fire");
            });
        }

        [Test]
        public void RootSt_TheBreathLanding_OnAPlayerWithNoStance_StopsNothing()
        {
            var s = PlaceTwoStances();
            var turn1 = TurnLoop.EndTurn(s, NoShuffle);
            Assert.That(turn1.State.Enemies[0].ActiveSwitch, Is.EqualTo("root_st"));

            var turn2Start = TurnLoop.BeginPlayerTurn(turn1.State, NoShuffle).State;
            turn2Start = turn2Start with { Player = turn2Start.Player with { Stances = null } };
            var turn2 = TurnLoop.EndTurn(turn2Start, NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(turn2.Events.OfType<ActionExecuted>().First().Action.Face.StopStance, Is.True, "the breath that would stop one was taken");
                Assert.That(turn2.Events.OfType<ActionWhiffed>(), Is.Empty, "and it landed");
                Assert.That(turn2.Events.OfType<StanceStopped>(), Is.Empty);
                Assert.That(turn2.State.Player.StanceList, Is.Empty);
            });
        }

        [Test]
        public void RootSt_TheBreathThatWhiffs_StopsNothing()
        {
            // Gap 0 is outside the base breath's 2〜4: the face does not reach, so nothing is stopped.
            var s = PlaceTwoStances();
            var turn1 = TurnLoop.EndTurn(s, NoShuffle);
            var turn2Start = TurnLoop.BeginPlayerTurn(turn1.State, NoShuffle).State;
            turn2Start = turn2Start with { Player = turn2Start.Player with { Cell = turn2Start.Enemy.Cell - 1 } };
            Assert.That(turn2Start.Gap, Is.EqualTo(0));
            var turn2 = TurnLoop.EndTurn(turn2Start, NoShuffle);

            Assert.Multiple(() =>
            {
                Assert.That(turn2.Events.OfType<ActionWhiffed>().Select(w => w.SourceId), Does.Contain("wither_breath"));
                Assert.That(turn2.Events.OfType<StanceStopped>(), Is.Empty);
            });
        }

        [Test]
        public void RootSt_TheBreath_StopsThroughTheStages_KeepingTheStagesReach()
        {
            var root = Enemies.DistortionRoot;
            var baseBreath = root.Switches!.Single(s => s.Id == "root_st").Override("wither_breath")!;
            var stageBreath = root.Switches!.Single(s => s.Id == "stage_3+root_st").Override("wither_breath")!;
            Assert.Multiple(() =>
            {
                Assert.That(root.Actions["wither_breath"].Face.StopStance, Is.False, "the root's own breath stops nothing");
                Assert.That(root.Switches!.Single(s => s.Id == "stage_2").Override("wither_breath")!.Face.StopStance, Is.False);
                Assert.That(baseBreath.Face.StopStance, Is.True);
                Assert.That(baseBreath.Face.ReachOrDefault, Is.EqualTo(new Reach(2, 4)));
                Assert.That(stageBreath.Face.StopStance, Is.True);
                Assert.That(stageBreath.Face.ReachOrDefault, Is.EqualTo(new Reach(0, 4)), "the stage's reach carries into root_st over it");
                Assert.That(stageBreath.Face.StatusList.First().Cap, Is.EqualTo(Statuses.BossWordStackMax), "and so does its 枯らし cap");
                Assert.That(baseBreath.Description, Does.EndWith("いちばん新しく置いた永続の効果 1 つが 1 ターン働かない"));
            });
        }

        /// <summary>Turn 1 of a battle with the root at gap 3, the older stance placed before the newer one.</summary>
        private static BattleState PlaceTwoStances()
        {
            var s = TurnLoop.BeginPlayerTurn(TurnLoop.Start(new BattleSetup(Enemies.DistortionRoot, Deck(Older, Newer), Cells, StartGap: 3), NoShuffle).State, NoShuffle).State;
            s = TurnLoop.PlayCard(s, s.Hand.First(c => c.Def.Id == "older_stance").InstanceId, NoShuffle).State;
            s = TurnLoop.PlayCard(s, s.Hand.First(c => c.Def.Id == "newer_stance").InstanceId, NoShuffle).State;
            Assert.That(s.Player.StanceList.Select(entry => entry.Source), Is.EqualTo(new[] { "older_stance", "newer_stance" }));
            return s;
        }

        private static IEnumerable<string> Fired(IReadOnlyList<BattleEvent> events) =>
            events.OfType<StanceFired>().Where(f => f.Actor == Actor.Player && f.Hook == StanceHook.TurnStart).Select(f => f.SourceId).ToList();

        private static List<CardInstance> Deck(params CardDef[] first)
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < first.Length; i++) deck.Add(new CardInstance(first[i].Id + "-" + i, first[i]));
            for (int i = deck.Count; i < Constants.DeckMin; i++) deck.Add(new CardInstance("filler-" + i, Filler));
            return deck;
        }
    }
}
