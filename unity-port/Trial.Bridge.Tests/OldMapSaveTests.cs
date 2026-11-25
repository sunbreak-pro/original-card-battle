// #1049: layers 1〜6 now draw 7〜9 rows, so a save made before reads its seed into a different map. A life is saved as its
// start and the log of its calls, and is replayed onto the map the seed draws now (Life.BeginLife).
using System;
using System.Linq;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using InheritanceCore;
using NUnit.Framework;

namespace Trial.Bridge.Tests
{
    public sealed class OldMapSaveTests
    {
        // Written by the build before #1049 (lineage 11, life 1, nothing carried): the entry fight won, then a step to
        // node 3, the right end of row 1 of the 1,3,3,3,1 map it drew then (a survey there).
        private const string StartBefore = """
            {"v":1,"generation":1,"name":"ナディア","seed":"1690397200466873065","toolSlots":3,"deckMax":40,"survivorBonus":false,"traceLayers":[],"carvingKnown":false,"journal":{"Disclosure":{},"Pages":[],"Memo":"","Scribbles":[]},"choice":{"deck":[["side_sweep-0","side_sweep"],["side_sweep-1","side_sweep"],["kesa_cut-0","kesa_cut"],["kesa_cut-1","kesa_cut"],["reach_thrust-0","reach_thrust"],["reach_thrust-1","reach_thrust"],["brace-0","brace"],["brace-1","brace"],["feint-0","feint"],["feint-1","feint"],["boar_rush-0","boar_rush"],["boar_rush-1","boar_rush"],["body_check-0","body_check"],["body_check-1","body_check"],["step_in_guard-0","step_in_guard"],["step_in_guard-1","step_in_guard"],["step_out_guard-0","step_out_guard"],["step_out_guard-1","step_out_guard"],["shield_bash-0","shield_bash"],["shield_bash-1","shield_bash"]],"battleToolIds":[],"loadout":{"tools":[],"consumables":[],"slots":3,"trial":true},"toolIds":[],"consumableIds":[]}}
            """;

        private const string LogBefore = """
            [["E","StartEncounter",[]],["B","StartNext",[true]],["C","AdvanceAuto",[]],["C","TryPlay",["brace-1","AboveThrowLine"]],["C","TryPlay",["brace-0","AboveThrowLine"]],["C","EndTurn",[]],["C","AdvanceAuto",[],3],["C","TryPlay",["side_sweep-0","Receiver"]],["C","TryPlay",["step_in_guard-0","AboveThrowLine"]],["C","TryPlay",["side_sweep-1","Receiver"]],["C","TryPlay",["body_check-0","Receiver"]],["C","EndTurn",[]],["C","AdvanceAuto",[],3],["C","TryPlay",["body_check-1","Receiver"]],["C","EndTurn",[]],["C","AdvanceAuto",[],3],["C","TryPlay",["kesa_cut-1","Receiver"]],["C","EndTurn",[]],["C","AdvanceAuto",[],3],["C","TryPlay",["step_out_guard-0","AboveThrowLine"]],["C","TryPlay",["body_check-1","Receiver"]],["C","AdvanceAuto",[]],["B","Finish",["$source"]],["E","Return",[]],["S","Step",[3,"Rest"]]]
            """;

        [Test]
        public void ALifeSavedBeforeTheLongerMaps_Resumes_OnTheNodeOfTheSameNumberInTheNewMap()
        {
            LifeStartData start = LifeStartData.FromJson(Json.Parse(StartBefore));
            LifeLog log = LifeLog.FromJson(Json.Parse(LogBefore));

            LiveLife live = null;
            Assert.DoesNotThrow(() => live = Life.BeginLife(start, log), "the save loads");
            ExplorationState state = live.Raw.Session.State;
            Assert.Multiple(() =>
            {
                Assert.That(state.Map.RowCount, Is.InRange(7, 9), "the layer is drawn the new way");
                Assert.That(live.Log.Count, Is.EqualTo(log.Count), "every call replays");
                Assert.That(state.Map.Node(3).Row, Is.EqualTo(1), "node 3 is still next to the entry, on a row 3 wide");
                Assert.That(state.CurrentNodeId, Is.EqualTo(3), "the life goes on from the node the save names");
                Assert.That(state.DaysLeft, Is.EqualTo(Calendar.TotalDays - 2));
            });
        }

        [Test]
        public void AStepTheNewMapDoesNotLink_IsLeftOut_AndTheLifeResumesWhereItStoodBefore()
        {
            // The same save with its step aimed at node 10, the boss of the map drawn then. On a map of 7〜9 rows node 10
            // lies three rows or more below the entry: the session refuses the step and the replay goes on without it.
            LifeStartData start = LifeStartData.FromJson(Json.Parse(StartBefore));
            LifeLog log = LifeLog.FromJson(Json.Parse(LogBefore.Replace("[\"S\",\"Step\",[3,", "[\"S\",\"Step\",[10,")));

            LiveLife live = null;
            Assert.DoesNotThrow(() => live = Life.BeginLife(start, log), "the save loads");
            ExplorationState state = live.Raw.Session.State;
            Assert.Multiple(() =>
            {
                Assert.That(state.Map.Node(10).Row, Is.GreaterThanOrEqualTo(3));
                Assert.That(live.Log.Count, Is.EqualTo(log.Count), "every call replays; the refused step changes nothing");
                Assert.That(state.Phase, Is.EqualTo(RunPhase.Exploring));
                Assert.That(state.CurrentNodeId, Is.EqualTo(state.Map.EntryId), "the life stands where the fight left it");
                Assert.That(state.DaysLeft, Is.EqualTo(Calendar.TotalDays - 1), "only the entry's day is spent");
                Assert.That(live.Raw.Session.MapIsLive, Is.True, "the player walks on from the entry");
            });
        }
    }
}
