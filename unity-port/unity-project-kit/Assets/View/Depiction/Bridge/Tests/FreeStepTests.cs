// Port of the browser trial's test/depiction/FreeStep.test.mjs — 俊敏's free cell through the depiction
// source (battle_core_v4.md §5 #48, battle_ui_ux_v2 §3.8). CoreBattleSource.CanTakeFreeStep(dir) /
// TakeFreeStep(dir) give the screen a way to take it. These tests hold them to the core: a card that puts
// 俊敏 on the player opens the step at the next turn start, the step is one screen event written by the
// same writer as every other move, and the frame it settles on says what the core's state says.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class FreeStepTests
    {
        /// <summary>A card of the test's own: 俊敏 2 on the player (no card of the catalog puts 俊敏 on the player yet).</summary>
        private static readonly CardDef Quicken = new CardDef("quicken", "身軽", BattleAttribute.Skill, 1,
            new Face(Statuses: new[] { new StatusGrant(StatusKind.Swift, 2, true) }), null, TargetKind.Self, "");

        /// <summary>js: FreeStep.test.mjs:26 — 20 cards: six 身軽 and the prototype deck's kinds; seed 7 deals two 身軽 into the first hand.</summary>
        private static List<CardInstance> DeckWithQuicken()
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < 6; i++) deck.Add(new CardInstance("quicken-" + i, Quicken));
            IReadOnlyList<CardDef> kinds = PrototypeDeck.Kinds;
            for (int i = 0; deck.Count < 20; i++) deck.Add(new CardInstance("p-" + i, kinds[i % kinds.Count]));
            return deck;
        }

        // js: FreeStep.test.mjs:34
        private static CoreBattleSource Source(IReadOnlyList<EnemyDef> more = null, int cells = 7, int seed = 7)
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, DeckWithQuicken(), cells, MoreEnemies: more);
            return new CoreBattleSource(setup, seed);
        }

        // js: FreeStep.test.mjs:39
        private static void ToPlayer(CoreBattleSource source)
        {
            int guard = 0;
            while (!source.WaitingForPlayer && !source.Finished && guard++ < 200) source.AdvanceAuto();
            Assert.That(source.WaitingForPlayer, Is.True, "the battle waits for the player");
        }

        /// <summary>js: FreeStep.test.mjs:46 — turn 1: play 身軽 (俊敏 2), end the turn, play the enemy's turn up to the player's turn 2.</summary>
        private static void ToSwiftTurn(CoreBattleSource source)
        {
            ToPlayer(source);
            CardFace face = source.Frame.Hand.FirstOrDefault(f => f.Id.StartsWith("quicken", StringComparison.Ordinal));
            Assert.That(face, Is.Not.Null, "a 身軽 is in the first hand");
            Assert.That(source.TryPlay(face.Id, DepictionText.RequiredZone(face.Aim), out DepictionEvent _), Is.EqualTo(PlayVerdict.Accepted));
            source.EndTurn();
            ToPlayer(source);
        }

        /// <summary>js: FreeStep.test.mjs:57 — the settled frame against the core's state: the floor, every enemy's N, the hand, the 俊敏 chip.</summary>
        private static void AssertFrameMatchesCore(CoreBattleSource source)
        {
            BattleState s = source.State;
            DepictionFrame f = source.Frame;
            Assert.That(f.Floor.PlayerCell, Is.EqualTo(s.Player.Cell), "Floor.PlayerCell");
            Assert.That(f.Floor.Gap, Is.EqualTo(s.GapTo(s.Nearest)), "Floor.Gap is N to the nearest standing enemy");
            foreach (UnitFrame uf in f.Enemies)
            {
                Assert.That(uf.Cell, Is.EqualTo(s.Enemies[uf.Unit].Body.Cell), "enemy " + uf.Unit + " cell");
                if (!uf.Down) Assert.That(uf.Gap, Is.EqualTo(s.GapTo(uf.Unit)), "enemy " + uf.Unit + " N");
            }
            Assert.That(f.Hand.Select(c => c.Id).ToList(), Is.EqualTo(s.Hand.Select(c => c.InstanceId).ToList()), "the hand");
            StatusChip chip = f.Player.Statuses.FirstOrDefault(c => c.Label == "俊敏");
            int stacks = s.Player.Statuses.Stacks(StatusKind.Swift);
            if (stacks > 0)
            {
                Assert.That(chip, Is.Not.Null, "the 俊敏 chip");
                Assert.That(chip.Stacks, Is.EqualTo(stacks), "the 俊敏 chip");
            }
            else
            {
                Assert.That(chip, Is.Null);
            }
        }

        // js: FreeStep.test.mjs:72
        [Test]
        public void FreeStep_IsClosed_WithoutSwift()
        {
            CoreBattleSource source = Source();
            ToPlayer(source);
            Assert.That(TurnLoop.CanTakeFreeStep(source.State), Is.False);
            Assert.That(source.CanTakeFreeStep(), Is.False);
            Assert.That(source.CanTakeFreeStep(1), Is.False);
            Assert.That(source.CanTakeFreeStep(-1), Is.False);
            Assert.Throws<InvalidOperationException>(() => source.TakeFreeStep(1));
        }

        // js: FreeStep.test.mjs:82
        [Test]
        public void FreeStep_ASwiftCard_OpensTheStep_AtTheNextTurnStart()
        {
            CoreBattleSource source = Source();
            ToPlayer(source);
            CardFace face = source.Frame.Hand.First(f => f.Id.StartsWith("quicken", StringComparison.Ordinal));
            source.TryPlay(face.Id, DepictionText.RequiredZone(face.Aim), out DepictionEvent _);
            // 俊敏 lands, but the turn it lands in has started already (§5): no step this turn.
            Assert.That(source.State.Player.Statuses.Stacks(StatusKind.Swift), Is.EqualTo(2));
            Assert.That(source.CanTakeFreeStep(), Is.False);
            source.EndTurn();
            // Not while the enemy's turn plays.
            Assert.That(source.CanTakeFreeStep(), Is.False);
            ToPlayer(source);
            Assert.That(TurnLoop.CanTakeFreeStep(source.State), Is.True);
            Assert.That(source.CanTakeFreeStep(), Is.True);
            AssertFrameMatchesCore(source);
        }

        // js: FreeStep.test.mjs:99
        [Test]
        public void FreeStep_Back_IsOneScreenEvent_WhoseFrameMatchesTheCore()
        {
            CoreBattleSource source = Source();
            ToSwiftTurn(source);
            int before = source.State.Player.Cell;
            int historyBefore = source.History.Count;
            Assert.That(source.CanTakeFreeStep(-1), Is.True);
            DepictionEvent ev = source.TakeFreeStep(-1);

            Assert.That(ev.Kind, Is.EqualTo(CoreBattleSource.FreeStepKind));
            Assert.That(ev.Kind, Is.EqualTo(DepictionEventKind.FreeStep));
            Assert.That(ev.Title, Is.EqualTo(CoreBattleSource.FreeStepTitle));
            Assert.That(ev.Title, Is.EqualTo("俊敏"));
            Assert.That(ev.Unit, Is.EqualTo(-1), "the player's own move");
            Assert.That(source.State.Player.Cell, Is.EqualTo(before - 1));
            // The core's own events, kept in the history the end screen counts from.
            List<BattleEvent> added = source.History.Skip(historyBefore).ToList();
            Assert.That(added.Select(e => e.GetType().Name).ToList(), Is.EqualTo(new[] { "FreeStepTaken", "CellsMoved" }));
            Assert.That(((FreeStepTaken)added[0]).Direction, Is.EqualTo(-1));
            Assert.That(added[1], Is.InstanceOf<CellsMoved>());
            Assert.That(added[1].Actor, Is.EqualTo(Actor.Player));

            // One walk of one cell and the recount, as a card's move plays (§5.7 順 10 → 順 1〜2).
            List<Cue> moves = ev.Cues.Where(c => c.Kind == CueKind.RangeSwitch).ToList();
            Assert.That(moves.Count, Is.EqualTo(1));
            Assert.That(ev.Cues.Count, Is.EqualTo(1));
            Cue cue = moves[0];
            Assert.That(cue.Pushed, Is.False);
            Assert.That(cue.CellsMoved, Is.EqualTo(1));
            Assert.That(cue.Unit, Is.EqualTo(-1), "the player moved");
            Assert.That(cue.PlayerCellAfter, Is.EqualTo(source.State.Player.Cell));
            Assert.That(cue.GapAfter, Is.EqualTo(source.State.GapTo(source.State.Nearest)));
            Assert.That(EffectPlan.StepsOf(ev).Select(s => s.Id + " x" + s.Count).ToList(),
                Is.EqualTo(new[] { EffectId.FloorMove + " x1", EffectId.GapRecount + " x1" }));

            // The screen settles on the event's frame; the step is spent, the turn still the player's.
            Assert.That(source.Frame, Is.SameAs(ev.After));
            Assert.That(source.WaitingForPlayer, Is.True);
            Assert.That(source.CanTakeFreeStep(), Is.False);
            Assert.Throws<InvalidOperationException>(() => source.TakeFreeStep(1));
            AssertFrameMatchesCore(source);
            // 移動後 (§2.3) counts the step: the core says the player moved this turn.
            Assert.That(source.State.Player.Moved, Is.True);
        }

        // js: FreeStep.test.mjs:137
        [Test]
        public void FreeStep_Forward_MatchesTheCore_AndTheBattlePlaysOn()
        {
            CoreBattleSource source = Source();
            ToSwiftTurn(source);
            BattleState s = source.State;
            BattleState way = TurnLoop.TakeFreeStep(s, 1).State; // what the core alone would do (the state is a value)
            // Seed 7 leaves N 2 here, so the way forward is open.
            Assert.That(way.Player.Cell, Is.Not.EqualTo(s.Player.Cell));
            Assert.That(source.CanTakeFreeStep(1), Is.True);
            DepictionEvent ev = source.TakeFreeStep(1);
            Assert.That(source.State.Player.Cell, Is.EqualTo(way.Player.Cell));
            Assert.That(ev.After.Floor.PlayerCell, Is.EqualTo(way.Player.Cell));
            AssertFrameMatchesCore(source);
            // The turn goes on from the new cell: a card is still playable and the turn ends as usual.
            CardFace next = source.Frame.Hand.FirstOrDefault(f => source.Inspect(f.Id) == PlayVerdict.Accepted);
            Assert.That(next, Is.Not.Null, "a card is playable after the step");
            Assert.That(source.TryPlay(next.Id, DepictionText.RequiredZone(next.Aim), out DepictionEvent _), Is.EqualTo(PlayVerdict.Accepted));
            AssertFrameMatchesCore(source);
            source.EndTurn();
            ToPlayer(source);
            AssertFrameMatchesCore(source);
        }

        // js: FreeStep.test.mjs:159
        [Test]
        public void FreeStep_TheLine_ClosesAWay_AndTheSecondStackOpensTheNextTurn()
        {
            CoreBattleSource source = Source();
            ToSwiftTurn(source);
            source.TakeFreeStep(-1); // cell 2 → 1
            Assert.That(source.State.Player.Cell, Is.EqualTo(1));
            source.EndTurn();
            ToPlayer(source);
            // 俊敏 2 gives two turns of it (§5: read before the tick).
            Assert.That(source.CanTakeFreeStep(), Is.True);
            // Cell 1 is the end of the line: no way back (no arrow, battle_ui_ux_v2 §3.8).
            Assert.That(source.State.Player.Cell, Is.EqualTo(1));
            Assert.That(source.CanTakeFreeStep(-1), Is.False);
            Assert.That(source.CanTakeFreeStep(0), Is.False);
            Assert.That(source.CanTakeFreeStep(2), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => source.TakeFreeStep(0));
            AssertFrameMatchesCore(source);
        }

        // js: FreeStep.test.mjs:177
        [Test]
        public void FreeStep_PlayingACard_LetsTheStepGo()
        {
            CoreBattleSource source = Source();
            ToSwiftTurn(source);
            CardFace next = source.Frame.Hand.FirstOrDefault(f => source.Inspect(f.Id) == PlayVerdict.Accepted);
            Assert.That(next, Is.Not.Null);
            source.TryPlay(next.Id, DepictionText.RequiredZone(next.Aim), out DepictionEvent _);
            Assert.That(source.CanTakeFreeStep(), Is.False);
            Assert.That(source.CanTakeFreeStep(-1), Is.False);
        }

        // js: FreeStep.test.mjs:187
        [Test]
        public void FreeStep_UnderBinding_IsTaken_AndTheLineSaysWhy()
        {
            CoreBattleSource source = Source();
            ToSwiftTurn(source);
            // Test set-up: 呪縛 on the player, as a boss word would put it (roster §4.1). The core still takes the step.
            BattleState s = source.State;
            SetState(source, s with { Player = s.Player with { Statuses = s.Player.Statuses.Add(StatusKind.Binding, 2) } });
            Assert.That(source.CanTakeFreeStep(-1), Is.True, "the arrow stands under 呪縛 too (§3.8)");
            int cell = source.State.Player.Cell;
            int historyBefore = source.History.Count;
            DepictionEvent ev = source.TakeFreeStep(-1);
            List<BattleEvent> added = source.History.Skip(historyBefore).ToList();
            Assert.That(added[0], Is.InstanceOf<FreeStepTaken>());
            Assert.That(added[1], Is.InstanceOf<MoveBlocked>());
            Assert.That(((MoveBlocked)added[1]).By, Is.EqualTo(StatusKind.Binding));
            Assert.That(source.State.Player.Cell, Is.EqualTo(cell));
            Assert.That(ev.Cues.Select(c => c.Kind + ":" + c.Text).ToList(),
                Is.EqualTo(new[] { CueKind.TraitFire + ":" + CoreText.MoveBlockedLine(StatusKind.Binding) }));
            Assert.That(source.CanTakeFreeStep(), Is.False);
            AssertFrameMatchesCore(source);
        }

        // js: FreeStep.test.mjs:206
        [Test]
        public void FreeStep_WithTwoEnemies_EveryEnemysNMatchesTheCore()
        {
            CoreBattleSource source = Source(more: new[] { Enemies.CrossbowHunter }, cells: 8);
            ToSwiftTurn(source);
            Assert.That(source.Frame.Enemies.Count, Is.EqualTo(2));
            int way = source.CanTakeFreeStep(1) ? 1 : -1;
            Assert.That(source.CanTakeFreeStep(way), Is.True);
            DepictionEvent ev = source.TakeFreeStep(way);
            Cue cue = ev.Cues.FirstOrDefault(c => c.Kind == CueKind.RangeSwitch);
            Assert.That(cue, Is.Not.Null);
            Assert.That(cue.PlayerCellAfter, Is.EqualTo(source.State.Player.Cell));
            AssertFrameMatchesCore(source);
        }

        /// <summary>
        /// The free step's event is numbered with the battle's other events, and the next event after it
        /// goes on from its number (the writer's own NewEvent).
        /// </summary>
        [Test]
        public void FreeStep_TakesTheNextNumber_AndTheBattleCountsOn()
        {
            CoreBattleSource source = Source();
            ToSwiftTurn(source);
            int last = LastOrder(source);
            DepictionEvent step = source.TakeFreeStep(-1);
            Assert.That(step.Order, Is.EqualTo(last + 1));
            CardFace next = source.Frame.Hand.First(f => source.Inspect(f.Id) == PlayVerdict.Accepted);
            Assert.That(source.TryPlay(next.Id, DepictionText.RequiredZone(next.Aim), out DepictionEvent played), Is.EqualTo(PlayVerdict.Accepted));
            Assert.That(played.Order, Is.EqualTo(step.Order + 1));
        }

        /// <summary>The order of the last event the source handed out, read from its writer's counter.</summary>
        private static int LastOrder(CoreBattleSource source)
        {
            FieldInfo writerField = typeof(CoreBattleSource).GetField("_writer", BindingFlags.NonPublic | BindingFlags.Instance);
            object writer = writerField.GetValue(source);
            FieldInfo order = writer.GetType().GetField("_order", BindingFlags.NonPublic | BindingFlags.Instance);
            return (int)order.GetValue(writer);
        }

        /// <summary>Test set-up only (the JS assigns source._state): puts a state of the test's own behind the source.</summary>
        private static void SetState(CoreBattleSource source, BattleState state)
        {
            FieldInfo field = typeof(CoreBattleSource).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(source, state);
        }
    }
}
