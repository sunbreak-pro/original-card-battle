// The trial extension of the exploration session and screen (the browser trial's docs/EXTENSIONS.md 「探索の持ち物と
// イベント」, W-1〜W-18, X-16): the session and screen half of test/ext/world.test.mjs. The JS tests drive an Expedition;
// here the session stands alone with a stand-in keeper (TrialKeeper) and the battles reported through
// ExplorationSession.ReportBattle, which is what the Expedition's Return does. What only the Expedition does (the
// card pile's deal, the replay, the deck limits) is tested with Exploration.Bridge.
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;
using Exploration;
using Journal;
using NUnit.Framework;
using static Exploration.Tests.TrialTestKit;

namespace Exploration.Tests
{
    public class TrialSessionTests
    {
        // ---------------------------------------------------------------- off by default (W-1)

        [Test]
        public void ByDefault_NothingIsOn_AndTheRunPlaysTheCSharp()
        {
            // js: test/ext/world.test.mjs:110
            Assert.That(new RunStart().Trial, Is.False);
            Assert.That(new RunStart().CarvingKnown, Is.Null);
            Assert.That(new RunStart().MapFor, Is.Null);
            var s = new ExplorationSession(new RunStart { Seed = 1, Loadout = RunStart.TrialLoadout() });
            Assert.That(s.Trial, Is.False);
            Assert.That(s.Keeper, Is.Null);
            Assert.That(s.State.Trial, Is.False);
            Assert.That(s.State.TimeLimit, Is.EqualTo(SevenLayers.Of(1).TimeLimit));
            s.Act(ScreenAction.WinBattle);
            Assert.That(s.State.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "joka_no_ko", "joka_no_ko" }));
            s.Receive(new[] { "yakuso" });
            Assert.That(s.HasPendingFound, Is.False, "a C# session takes no find");
            Assert.That(s.State.Loadout.Consumables.Count, Is.EqualTo(2));
            // an event node only says what may happen
            var screen = s.Screen();
            var ev = screen.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Event && n.Lines.Count > 0);
            if (ev != null) Assert.That(ev.Lines, Is.EqualTo(new[] { "泉 / 重い宝箱 / 特殊な食事 のどれかが起きる" }));
            Assert.That(screen.ToolCards, Is.Empty);
            Assert.That(screen.TimeBonusLabel, Is.EqualTo(""));
            Assert.That(screen.ReliefLabel, Is.EqualTo(""));
            Assert.That(screen.Aiming, Is.False);
            Assert.That(screen.Events, Is.Empty);
            Assert.That(s.Screen().Consumables[0].Forecast, Is.Null);
            Assert.That(s.Act(ScreenAction.AcceptEvent), Is.False, "the trial buttons answer nothing");
            Assert.That(s.Act(ScreenAction.TakeFound, "0"), Is.False);
            Assert.That(s.Act(ScreenAction.LeaveFound), Is.False);
            Assert.That(s.Act(ScreenAction.ChooseSurvey, "shadow_hound"), Is.False);
            Assert.That(s.OpenTraining(), Is.False);
            Assert.That(s.CarvingKnownAt(1), Is.False);
        }

        [Test]
        public void ACSharpSession_BurnsANonGaugeConsumable_WithoutEffect_AsTheCSharpDoes()
        {
            // js: test/ext/world.test.mjs:143
            var lo = new RunLoadout(null, new[] { TrialItems.Consumable("yakuso") }, 3, trial: true);
            var s = new ExplorationSession(new RunStart { Seed = 1, Loadout = lo, Hp = 20 });
            s.Act(ScreenAction.WinBattle);
            Assert.That(s.UseConsumable(0), Is.True);
            Assert.That(s.State.Hp, Is.EqualTo(20));
            Assert.That(s.State.Loadout.Consumables, Is.Empty);
        }

        [Test]
        public void TrialItems_CarryEveryToolAndConsumable_AsTheKindTheCoreReads()
        {
            // js: test/ext/world.test.mjs:154 (TrialItems' half; the Loadout conversion is Exploration.Bridge's)
            var tools = new[] { Mask, Sand, Book, Wick }.Select(TrialItems.Tool).ToList();
            Assert.That(tools.Select(t => (t.Id, t.Kind, t.Amount)), Is.EqualTo(new[]
            {
                (Mask, ReliefKind.Density, 1), (Sand, ReliefKind.Time, 1), (Book, ReliefKind.Survey, 1), (Wick, ReliefKind.Interlude, 15),
            }));
            var consumables = new[] { "joka_no_ko", "yakuso", "shirube_no_ishi", "koi_hoshiniku", "wasuremizu" }.Select(TrialItems.Consumable).ToList();
            Assert.That(consumables.Select(t => (t.Id, t.Kind, t.Amount)), Is.EqualTo(new[]
            {
                ("joka_no_ko", ReliefKind.Gauge, 10), ("yakuso", ReliefKind.Heal, 20), ("shirube_no_ishi", ReliefKind.Teleport, 1),
                ("koi_hoshiniku", ReliefKind.MaxStamina, 1), ("wasuremizu", ReliefKind.Refill, 1),
            }));
            var battle = new[] { "maai_no_kutsu", "ikitsugi_no_fue", "ryurin_no_kakera", "ryugo_no_fu" }.Select(TrialItems.Tool).ToList();
            Assert.That(battle.All(t => t.Kind == ReliefKind.Battle), Is.True);
            Assert.That(new RunLoadout(battle, null, 4, trial: true).DensityRelief, Is.EqualTo(0));
        }

        // ---------------------------------------------------------------- tools (§2)

        [Test]
        public void 刻限の砂_AddsItsTimeToEveryLayer_AndTheBandSaysSo()
        {
            // js: test/ext/world.test.mjs:172
            var s = Trial(1, new[] { Sand });
            Assert.That(s.State.TimeLimit, Is.EqualTo(11));
            Assert.That(s.State.TimeLeft, Is.EqualTo(10), "the entry node took one");
            var screen = s.Screen();
            Assert.That(screen.TimeLabel, Is.EqualTo("刻限 10 / 11"));
            Assert.That(screen.TimeLimit, Is.EqualTo(11));
            Assert.That(screen.TimeBonusLabel, Is.EqualTo("刻限の砂 +1"));
            Assert.That(Trial(1, new[] { Sand, Sand }).State.TimeLimit, Is.EqualTo(12));
            var off = new ExplorationSession(new RunStart { Seed = 1, Loadout = new RunLoadout(new[] { TrialItems.Tool(Sand) }, null, 3, trial: true) });
            Assert.That(off.State.TimeLimit, Is.EqualTo(10));
        }

        [Test]
        public void 刻限の砂_ReachesTheNextLayer_ThroughTheDescent()
        {
            // js: test/ext/world.test.mjs:188
            var s = Trial(2, new[] { Sand });
            WalkTo(s, n => n.Kind == NodeKind.Boss);
            Win(s);
            ClearChoices(s);
            Assert.That(s.Act(ScreenAction.GoToInterlude), Is.True);
            Assert.That(s.Screen().Card.Lines.Any(l => l.EndsWith("刻限 11（刻限の砂 +1）", StringComparison.Ordinal)), Is.True);
            Assert.That(s.Act(ScreenAction.Descend), Is.True);
            Assert.That(s.State.Profile.Layer, Is.EqualTo(2));
            Assert.That(s.State.TimeLimit, Is.EqualTo(11));
            Assert.That(s.State.TimeLeft, Is.EqualTo(10));
            Assert.That(s.State.Trial, Is.True);
        }

        [Test]
        public void 灯芯の欠片_RaisesTheInterludeHealTo45Percent()
        {
            // js: test/ext/world.test.mjs:203
            var s = Trial(2, new[] { Wick });
            WalkTo(s, n => n.Kind == NodeKind.Boss);
            Win(s, 10);
            ClearChoices(s);
            Assert.That(ExplorationReducer.InterludePercent(s.State), Is.EqualTo(45));
            Assert.That(s.Screen().Card.Lines.Any(l => l.Contains("HP +23（最大の 45%・灯芯の欠片 +15%）")), Is.True);
            s.Act(ScreenAction.GoToInterlude);
            Assert.That(s.InterludeHealed, Is.EqualTo(23), "45% of 50, rounded half away from zero");
            Assert.That(s.State.Hp, Is.EqualTo(33));
            var plain = Trial(2);
            WalkTo(plain, n => n.Kind == NodeKind.Boss);
            Win(plain, 10);
            ClearChoices(plain);
            plain.Act(ScreenAction.GoToInterlude);
            Assert.That(plain.InterludeHealed, Is.EqualTo(15));
        }

        [Test]
        public void 写しの帳_LetsThePlayerPickTheSurveyTarget_AndTheJournalRaisesIt()
        {
            // js: test/ext/world.test.mjs:223
            var s = Trial(3, new[] { Book });
            var keeper = (TrialKeeper)s.Keeper;
            int survey = WalkTo(s, n => n.Kind == NodeKind.Survey);
            Assert.That(s.State.Map.Node(survey).Kind, Is.EqualTo(NodeKind.Survey));
            Assert.That(s.HasPendingSurvey, Is.True);
            Assert.That(s.IsIdle, Is.False);
            Assert.That(s.MapIsLive, Is.False);
            var card = s.Screen().Card;
            Assert.That(card.Kind, Is.EqualTo(CardKind.SurveyChoice));
            Assert.That(card.IsChoice, Is.True);
            Assert.That(s.SurveyChoices, Is.EqualTo(ExplorationSession.LayerEnemies(1).Where(id => keeper.DisclosureOf(id) < 2)));
            string target = s.SurveyChoices[s.SurveyChoices.Count - 1];
            Assert.That(card.Buttons.First(b => b.Arg == target).Label,
                Is.EqualTo($"{keeper.EnemyName(target)}（開示度 {keeper.DisclosureOf(target)} → 2）"));
            Assert.That(s.Act(ScreenAction.ChooseSurvey, "no_such_enemy"), Is.False);
            Assert.That(s.Act(ScreenAction.ChooseSurvey, target), Is.True);
            Assert.That(keeper.DisclosureOf(target), Is.EqualTo(2));
            Assert.That(s.SurveyTarget, Is.EqualTo(target));
            Assert.That(s.SurveyPicked, Is.True);
            Assert.That(s.MapIsLive, Is.True);
            Assert.That(Row(s, "dungeon-1", "情報収集"), Is.EqualTo($"済ませた。{keeper.EnemyName(target)}が開示度 2 になった（写しの帳）"));
        }

        [Test]
        public void WithoutTheBook_TheSurveyAsksNothing_TheLayerDecides()
        {
            // js: test/ext/world.test.mjs:246
            var s = Trial(3);
            WalkTo(s, n => n.Kind == NodeKind.Survey);
            Assert.That(s.HasPendingSurvey, Is.False);
            Assert.That(s.Surveyed, Is.True);
            Assert.That(s.SurveyTarget, Is.Null);
        }

        [Test]
        public void WithoutTheBook_TheIntelsLayerTarget_IsRaisedInTheJournal_InTrialOnly()
        {
            // js: test/ext/world.test.mjs:254
            var s = Trial(3, intel: new SurveyIntel("shadow_hound"));
            var keeper = (TrialKeeper)s.Keeper;
            WalkTo(s, n => n.Kind == NodeKind.Survey);
            Assert.That(s.SurveyTarget, Is.EqualTo("shadow_hound"));
            Assert.That(s.SurveyPicked, Is.False);
            Assert.That(keeper.DisclosureOf("shadow_hound"), Is.EqualTo(2));
            Assert.That(Row(s, "dungeon-1", "情報収集"), Is.EqualTo("済ませた。瘴牙の走竜が開示度 2 になった"));
            // the C# session leaves the journal to the intel's reports
            var off = new ExplorationSession(new RunStart { Seed = 3 }, new SurveyIntel("shadow_hound"));
            var offKeeper = new TrialKeeper();
            off.Keeper = offKeeper;
            if (off.HasPendingBattle) Win(off);
            foreach (int node in PathTo(off.State, n => n.Kind == NodeKind.Survey))
            {
                off.Step(node);
                if (off.HasPendingBattle) Win(off);
            }
            Assert.That(off.Surveyed, Is.True);
            Assert.That(off.SurveyTarget, Is.Null);
            Assert.That(offKeeper.DisclosureOf("shadow_hound"), Is.LessThan(2));
        }

        [Test]
        public void TheBattleTools_StayBattleOnly_ButShowInTheInventory()
        {
            // js: test/ext/world.test.mjs:299
            var s = Trial(1, new[] { Flute, Mask });
            var tools = s.Screen().ToolCards;
            Assert.That(tools.Select(t => (t.Name, t.Side, t.Effect)), Is.EqualTo(new[] { ("息継ぎの笛", "戦闘", ""), ("防瘴の面", "探索", "濃さ −1") }));
            Assert.That(s.Screen().ToolsLabel, Is.EqualTo("ツール: 息継ぎの笛・防瘴の面（2 / 3）"));
            Assert.That(s.State.Loadout.DensityRelief, Is.EqualTo(1));
            Assert.That(s.Screen().ReliefLabel, Is.EqualTo("防瘴の面 濃さは下限 1 のまま"));
            var deep = Trial(1, new[] { Mask });
            WalkToLayer(deep, 3);
            Assert.That(deep.Screen().ReliefLabel, Is.EqualTo("防瘴の面 濃さ 2 → 1"));
        }

        // ---------------------------------------------------------------- consumables (§3)

        [Test]
        public void 薬草_GivesBackTwentyPercent_AndCannotBeWastedAtFullHp()
        {
            // js: test/ext/world.test.mjs:313
            var s = Trial(1, consumables: new[] { "yakuso" });
            Win(s, 50);
            ClearChoices(s);
            Assert.That(s.CanUseSlot(0), Is.False, "full HP: nothing would change");
            Assert.That(s.Screen().Consumables[0].Reason, Is.EqualTo(ExplorationText.NoEffectNow));
            Assert.That(s.UseConsumable(0), Is.False);
            var low = Trial(1, consumables: new[] { "yakuso" });
            Win(low, 25);
            ClearChoices(low);
            var slot = low.Screen().Consumables[0];
            Assert.That(slot.CanUse, Is.True);
            Assert.That(slot.Forecast, Is.EqualTo(new[] { "HP 25 → 35" }));
            Assert.That(slot.Id, Is.EqualTo("yakuso"));
            Assert.That(low.UseConsumable(0), Is.True);
            Assert.That(low.State.Hp, Is.EqualTo(35));
            Assert.That(low.State.Loadout.Consumables, Is.Empty);
        }

        [Test]
        public void 濃い干し肉_RaisesMaxStaminaByOne_InsideTheTemporaryFour()
        {
            // js: test/ext/world.test.mjs:332
            var s = Trial(1, consumables: new[] { "koi_hoshiniku", "koi_hoshiniku" });
            Win(s);
            ClearChoices(s);
            int before = s.State.MaxStamina;
            Assert.That(s.UseConsumable(0), Is.True);
            Assert.That(s.State.MaxStamina, Is.EqualTo(before + 1));
            Assert.That(s.State.TempMaxStaminaMod, Is.EqualTo(1));
            // at +4 already, a second one would change nothing
            s.State = s.State.With(tempMaxStaminaMod: 4);
            Assert.That(s.CanUseSlot(0), Is.False);
            Assert.That(ExplorationReducer.UseConsumable(s.State, 0).TempMaxStaminaMod, Is.EqualTo(4));
        }

        [Test]
        public void 濃い干し肉_OnTheFloorOfThree_ChangesNothingNow_SoItsSlotIsNotLit()
        {
            // js: test/ext/world.test.mjs:347
            var s = Trial(3, consumables: new[] { "koi_hoshiniku" }, miasma: 80);
            Win(s, 50, 2);
            ClearChoices(s);
            s.State = s.State.With(tempMaxStaminaMod: -4);
            Assert.That(s.State.MaxStamina, Is.EqualTo(3));
            var slot = s.Screen().Consumables[0];
            Assert.That(slot.CanUse, Is.False);
            Assert.That(slot.Forecast, Is.Null);
            Assert.That(slot.Reason, Is.EqualTo(ExplorationText.NoEffectNow));
            Assert.That(s.UseConsumable(0), Is.False);
            Assert.That(s.State.TempMaxStaminaMod, Is.EqualTo(-4), "nothing spent");
            s.State = s.State.With(tempMaxStaminaMod: -3);
            var lit = s.Screen().Consumables[0];
            Assert.That(lit.CanUse, Is.True);
            Assert.That(lit.Forecast, Is.EqualTo(new[] { ExplorationText.MaxStaminaChange(3, 4) }));
        }

        [Test]
        public void ASlot_IsLit_ExactlyWhenItHasAForecast()
        {
            // js: test/ext/world.test.mjs:368 (試作で決めたこと 1)
            int checkedSlots = 0;
            foreach (var carried in new[] { new[] { "koi_hoshiniku", "yakuso", "wasuremizu" }, new[] { "joka_no_ko" } })
            {
                var s = Trial(3, consumables: carried);
                Win(s, 50, 2);
                ClearChoices(s);
                var start = s.State;
                Assert.That(start.Loadout.Consumables.Count, Is.EqualTo(carried.Length));
                foreach (int miasma in new[] { 0, 5, 20, 59, 80, 95 })
                {
                    for (int temp = -4; temp <= 4; temp++)
                    {
                        foreach (int hp in new[] { 1, 45, 50 })
                        {
                            foreach (int stamina in new[] { 0, 3, 14 })
                            {
                                int max = start.With(miasmaPercent: miasma, tempMaxStaminaMod: temp).MaxStamina;
                                s.State = start.With(miasmaPercent: miasma, tempMaxStaminaMod: temp, hp: hp, stamina: Math.Min(stamina, max));
                                foreach (var slot in s.Screen().Consumables)
                                {
                                    Assert.That(slot.CanUse, Is.EqualTo(slot.Forecast != null),
                                        $"{slot.Name} at 瘴気 {miasma}% temp {temp} HP {hp}: lit ⇔ forecast");
                                    checkedSlots++;
                                }
                            }
                        }
                    }
                }
            }
            Assert.That(checkedSlots, Is.GreaterThan(0));
        }

        [Test]
        public void 忘れ水_FillsCurrentStamina()
        {
            // js: test/ext/world.test.mjs:395
            var s = Trial(1, consumables: new[] { "wasuremizu" });
            Win(s, 50, 2);
            ClearChoices(s);
            Assert.That(s.Screen().Consumables[0].Forecast, Is.EqualTo(new[] { $"スタミナ 2 → {s.State.MaxStamina}" }));
            Assert.That(s.UseConsumable(0), Is.True);
            Assert.That(s.State.Stamina, Is.EqualTo(s.State.MaxStamina));
        }

        [Test]
        public void 浄化の香_PullsTheGaugeBackTen_AsInTheCSharp()
        {
            // js: test/ext/world.test.mjs:405
            var s = Trial(1, consumables: new[] { "joka_no_ko" }, miasma: 30);
            Win(s);
            ClearChoices(s);
            Assert.That(s.UseConsumable(0), Is.True);
            Assert.That(s.State.MiasmaPercent, Is.EqualTo(21));
        }

        [Test]
        public void TheConsumables_StayUsableOnTheLayerBreakCard_ButNotDuringABattle()
        {
            // js: test/ext/world.test.mjs:414
            var s = Trial(2, consumables: new[] { "yakuso" });
            Assert.That(s.HasPendingBattle, Is.True);
            Assert.That(s.CanUseSlot(0), Is.False, "a battle waits");
            WalkTo(s, n => n.Kind == NodeKind.Boss);
            Win(s, 20);
            ClearChoices(s);
            Assert.That(s.State.Phase, Is.EqualTo(RunPhase.LayerCleared));
            Assert.That(s.UseConsumable(0), Is.True);
            Assert.That(s.State.Hp, Is.EqualTo(30));
        }

        [Test]
        public void 標の石_AimsFirst_ThenJumpsToAnyUnresolvedNode_PayingItsTime()
        {
            // js: test/ext/world.test.mjs:427
            var s = Trial(4, consumables: new[] { "shirube_no_ishi" });
            Win(s);
            ClearChoices(s);
            var map = s.State.Map;
            var neighbours = map.Neighbours(s.State.CurrentNodeId);
            var far = map.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Rest && !neighbours.Contains(n.Id) && !s.State.HasResolved(n.Id))
                ?? map.Nodes.FirstOrDefault(n => !s.State.HasResolved(n.Id) && !neighbours.Contains(n.Id));
            Assert.That(far, Is.Not.Null, "a far node");
            int carried = s.State.Loadout.Consumables.Count;
            Assert.That(s.UseConsumable(0), Is.True);
            Assert.That(s.IsAiming, Is.True);
            Assert.That(s.State.Loadout.Consumables.Count, Is.EqualTo(carried), "aiming spends nothing yet");
            var screen = s.Screen();
            Assert.That(screen.Aiming, Is.True);
            Assert.That(screen.Hint, Is.EqualTo(ExplorationText.AimHint));
            Assert.That(screen.Consumables[0].Aiming, Is.True);
            var node = screen.Node(far.Id);
            Assert.That(node.Options.Count > 0 && node.Options.All(o => o.Jump), Is.True);
            Assert.That(node.Note, Is.EqualTo(ExplorationText.JumpNote));
            // the boss is a landing place too (§3 names no exception)
            Assert.That(screen.Node(map.BossId).Options.Count, Is.GreaterThan(0));
            // a step is refused while aiming; Esc stops aiming
            int near = neighbours.First(id => !s.State.HasResolved(id));
            Assert.That(s.Step(near), Is.False);
            Assert.That(s.Escape(), Is.True);
            Assert.That(s.IsAiming, Is.False);
            // aim again and land
            s.UseConsumable(0);
            var before = s.State;
            Assert.That(s.Jump(far.Id), Is.True);
            Assert.That(s.State.CurrentNodeId, Is.EqualTo(far.Id));
            Assert.That(s.State.TimeLeft, Is.EqualTo(before.TimeLeft - 1));
            Assert.That(s.State.MiasmaPercent, Is.EqualTo(before.MiasmaPercent + before.EffectiveDensity));
            Assert.That(s.State.Loadout.Consumables.Count, Is.EqualTo(carried - 1));
            Assert.That(s.IsAiming, Is.False);
            Assert.That(s.State.HasResolved(far.Id), Is.True);
        }

        [Test]
        public void 標の石_UsedAgain_StopsTheAim()
        {
            var s = Trial(4, consumables: new[] { "shirube_no_ishi" });
            Win(s);
            Assert.That(s.UseConsumable(0), Is.True);
            Assert.That(s.IsAiming, Is.True);
            Assert.That(s.UseConsumable(0), Is.True);
            Assert.That(s.IsAiming, Is.False);
            Assert.That(s.State.Loadout.Consumables.Count, Is.EqualTo(1));
            Assert.That(s.Act(ScreenAction.Cancel), Is.False, "nothing to cancel");
        }

        [Test]
        public void 標の石_LandingOnABattle_StartsIt_AndALethalJumpAsksFirst()
        {
            // js: test/ext/world.test.mjs:465
            var s = Trial(4, consumables: new[] { "shirube_no_ishi" }, miasma: 98);
            Win(s);
            ClearChoices(s);
            var battle = s.State.Map.Nodes.First(n => n.Kind == NodeKind.Battle && !s.State.HasResolved(n.Id) && n.Id != s.State.CurrentNodeId);
            s.UseConsumable(0);
            Assert.That(s.Jump(battle.Id), Is.True);
            Assert.That(s.HasPendingStep, Is.True, "99% + 1 reaches 100%");
            Assert.That(s.PendingJumpSlot, Is.EqualTo(0));
            Assert.That(s.Screen().Card.Kind, Is.EqualTo(CardKind.ConfirmStep));
            Assert.That(s.Act(ScreenAction.ConfirmStep), Is.True);
            Assert.That(s.State.Phase, Is.EqualTo(RunPhase.MiasmaDeath));
            Assert.That(s.HasPendingBattle, Is.False);
            Assert.That(s.State.Loadout.Consumables, Is.Empty, "the stone was spent");

            var ok = Trial(4, consumables: new[] { "shirube_no_ishi" });
            Win(ok);
            ClearChoices(ok);
            ok.UseConsumable(0);
            Assert.That(ok.Jump(battle.Id), Is.True);
            Assert.That(ok.HasPendingBattle, Is.True);
            Assert.That(ok.PendingBattleNode, Is.EqualTo(battle.Id));
        }

        [Test]
        public void 標の石_ALethalJumpCancelled_KeepsTheStone()
        {
            var s = Trial(4, consumables: new[] { "shirube_no_ishi" }, miasma: 98);
            Win(s);
            s.UseConsumable(0);
            Assert.That(s.Jump(s.State.Map.BossId), Is.True);
            Assert.That(s.HasPendingStep, Is.True);
            Assert.That(s.Act(ScreenAction.Cancel), Is.True);
            Assert.That(s.IsAiming, Is.False);
            Assert.That(s.PendingJumpSlot, Is.EqualTo(-1));
            Assert.That(s.State.Loadout.Consumables.Count, Is.EqualTo(1));
            Assert.That(s.State.Phase, Is.EqualTo(RunPhase.Exploring));
        }

        [Test]
        public void 標の石_CannotJumpOnTheLayerBreak_OrWithNoTimeLeft()
        {
            // js: test/ext/world.test.mjs:488
            var s = Trial(2, consumables: new[] { "shirube_no_ishi" });
            WalkTo(s, n => n.Kind == NodeKind.Boss);
            Win(s);
            ClearChoices(s);
            Assert.That(s.State.Phase, Is.EqualTo(RunPhase.LayerCleared));
            Assert.That(s.CanUseSlot(0), Is.False);
            Assert.That(s.Screen().Consumables[0].Reason, Is.EqualTo(ExplorationText.NoJumpNow));
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.Teleport(s.State, 0, 1));
        }

        [Test]
        public void 標の石_AimedAtAFarEvent_ForecastsABareStep_ItsContentsStayHidden()
        {
            // js: test/ext/world.test.mjs:500 (W-12)
            int farSprings = 0;
            int nearSprings = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var s = Trial(seed, consumables: new[] { "shirube_no_ishi" }, hp: 30);
                Win(s, 30);
                ClearChoices(s);
                Assert.That(s.UseConsumable(0), Is.True);
                var screen = s.Screen();
                foreach (var node in s.State.Map.Nodes)
                {
                    if (node.Kind != NodeKind.Event || s.State.HasResolved(node.Id)) continue;
                    var card = screen.Node(node.Id);
                    bool spring = s.EventAt(node.Id) == EventKind.Spring;
                    bool hpLine = card.Options.Any(o => o.Forecast.Lines.Any(l => l.StartsWith("HP ", StringComparison.Ordinal)));
                    if (card.Standing == NodeStanding.Far)
                    {
                        Assert.That(card.Lines, Is.Empty, $"seed {seed} node {node.Id}: a far event shows nothing");
                        Assert.That(hpLine, Is.False, $"seed {seed} node {node.Id}: a far {s.EventAt(node.Id)} forecasts as a bare step");
                        Assert.That(card.Detail.Any(l => l.Contains("HP ")), Is.False);
                        if (spring) farSprings++;
                    }
                    else if (spring)
                    {
                        Assert.That(hpLine, Is.True, $"seed {seed} node {node.Id}: a 泉 next door counts its heal");
                        nearSprings++;
                    }
                }
            }
            Assert.That(farSprings, Is.GreaterThan(0), "some far 泉 was aimed at");
            Assert.That(nearSprings, Is.GreaterThan(0), "some 泉 next door was aimed at");
        }

        [Test]
        public void 標の石_StaysAimed_WhenAnotherSlotIsRefused_AndAUsableOneStopsIt()
        {
            // js: test/ext/world.test.mjs:532
            var s = Trial(2, consumables: new[] { "shirube_no_ishi", "yakuso" });
            Win(s, 50);
            ClearChoices(s);
            Assert.That(s.UseConsumable(0), Is.True);
            Assert.That(s.CanUseSlot(1), Is.False);
            Assert.That(s.UseConsumable(1), Is.False);
            Assert.That(s.IsAiming, Is.True);
            var screen = s.Screen();
            Assert.That(screen.Aiming, Is.True);
            Assert.That(screen.Consumables[0].Aiming, Is.True);
            Assert.That(screen.Nodes.Any(n => n.Options.Any(o => o.Jump)), Is.True, "the map still offers the jumps");
            s.State = s.State.With(hp: 30);
            int carried = s.State.Loadout.Consumables.Count;
            Assert.That(s.UseConsumable(1), Is.True);
            Assert.That(s.IsAiming, Is.False);
            Assert.That(s.State.Hp, Is.EqualTo(40));
            Assert.That(s.State.Loadout.Consumables.Count, Is.EqualTo(carried - 1));
            Assert.That(s.State.Loadout.Consumables[0].Kind, Is.EqualTo(ReliefKind.Teleport), "the stone is still carried");
        }

        [Test]
        public void 標の石_EndingTheLife_StopsTheAim()
        {
            // js: test/ext/world.test.mjs:556
            var s = Trial(2, consumables: new[] { "shirube_no_ishi" });
            Win(s, 50);
            ClearChoices(s);
            int carried = s.State.Loadout.Consumables.Count;
            Assert.That(s.UseConsumable(0), Is.True);
            Assert.That(s.Act(ScreenAction.AskEndLife), Is.True);
            Assert.That(s.IsAiming, Is.False, "the confirmation covers the map, so the aim stops");
            Assert.That(s.Act(ScreenAction.Cancel), Is.True);
            Assert.That(s.IsAiming, Is.False);
            Assert.That(s.UseConsumable(0), Is.True);
            Assert.That(s.Act(ScreenAction.AskEndLife), Is.True);
            Assert.That(s.Act(ScreenAction.ConfirmEndLife), Is.True);
            Assert.That(s.State.Phase, Is.EqualTo(RunPhase.Survived));
            var screen = s.Screen();
            Assert.That(s.IsAiming, Is.False);
            Assert.That(screen.Aiming, Is.False);
            Assert.That(screen.Consumables[0].Aiming, Is.False);
            Assert.That(screen.Consumables[0].CanUse, Is.False);
            Assert.That(s.State.Loadout.Consumables.Count, Is.EqualTo(carried), "nothing was spent");
        }

        // ---------------------------------------------------------------- finds into the slots (§4.3)

        [Test]
        public void AFind_GoesIntoAFreeSlot()
        {
            // js: test/ext/world.test.mjs:590 (the battle's consumables reach the session through Receive)
            var s = Trial(1, consumables: new[] { "yakuso" });
            Win(s);
            s.Receive(new[] { "wasuremizu" });
            Assert.That(s.State.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "yakuso", "wasuremizu" }));
            Assert.That(s.HasPendingFound, Is.False);
        }

        [Test]
        public void WithTheSlotsFull_TheFindWaitsOnASwapOrOnGivingUp()
        {
            // js: test/ext/world.test.mjs:599
            var s = Trial(1, consumables: new[] { "yakuso", "wasuremizu", "koi_hoshiniku" });
            Win(s);
            s.Receive(new[] { "joka_no_ko" });
            Assert.That(s.PendingFound, Is.EqualTo(new[] { "joka_no_ko" }));
            Assert.That(s.IsIdle, Is.False);
            Assert.That(s.MapIsLive, Is.False);
            var card = s.Screen().Card;
            Assert.That(card.Kind, Is.EqualTo(CardKind.Swap));
            Assert.That(card.IsChoice, Is.True);
            Assert.That(card.Buttons.Select(b => (b.Label, b.Action, b.Arg)), Is.EqualTo(new[]
            {
                ("薬草と入れ替える", ScreenAction.TakeFound, "0"),
                ("忘れ水と入れ替える", ScreenAction.TakeFound, "1"),
                ("濃い干し肉と入れ替える", ScreenAction.TakeFound, "2"),
                ("諦める", ScreenAction.LeaveFound, (string)null),
            }));
            Assert.That(card.Lines, Is.EqualTo(new[]
            {
                "浄化の香を拾った。入れ替える枠を選ぶか、諦める。消耗品は拠点へ持ち帰らない。",
                $"浄化の香: {ExplorationText.PlayerNote("joka_no_ko")}",
            }));
            Assert.That(s.Act(ScreenAction.TakeFound, "7"), Is.False, "no such slot");
            Assert.That(s.Act(ScreenAction.TakeFound, "x"), Is.False, "not a slot");
            Assert.That(s.Act(ScreenAction.TakeFound, "1"), Is.True);
            Assert.That(s.State.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "yakuso", "joka_no_ko", "koi_hoshiniku" }));
            Assert.That(s.HasPendingFound, Is.False);
            Assert.That(s.MapIsLive, Is.True);

            var other = Trial(1, consumables: new[] { "yakuso", "wasuremizu", "koi_hoshiniku" });
            Win(other);
            other.Receive(new[] { "joka_no_ko" });
            Assert.That(other.Act(ScreenAction.LeaveFound), Is.True);
            Assert.That(other.State.Loadout.Consumables.Select(c => c.Id), Is.EqualTo(new[] { "yakuso", "wasuremizu", "koi_hoshiniku" }));
            Assert.That(other.Act(ScreenAction.LeaveFound), Is.False, "nothing waits any more");
        }

        [Test]
        public void TakeFound_ReadsTheSlotAsTheBrowserTrialDoes()
        {
            // Number(null) is 0 in the browser trial, so TakeFound without a slot swaps the first one (a JS quirk, ported).
            var s = Trial(1, consumables: new[] { "yakuso", "wasuremizu", "koi_hoshiniku" });
            Win(s);
            s.Receive(new[] { "joka_no_ko", "shirube_no_ishi" });
            Assert.That(s.Act(ScreenAction.TakeFound), Is.True);
            Assert.That(s.State.Loadout.Consumables[0].Id, Is.EqualTo("joka_no_ko"));
            Assert.That(s.Act(ScreenAction.TakeFound, "1.5"), Is.False);
            Assert.That(s.Act(ScreenAction.TakeFound, " 2 "), Is.True);
            Assert.That(s.State.Loadout.Consumables[2].Id, Is.EqualTo("shirube_no_ishi"));
        }

        // The browser trial's verdicts, from node: Number(arg), then Number.isInteger and the slot range (−1: refused).
        [TestCase("0", 0)]
        [TestCase("1", 1)]
        [TestCase("2", 2)]
        [TestCase("3", -1)]
        [TestCase("-1", -1)]
        [TestCase("-0", 0)]
        [TestCase("+1", 1)]
        [TestCase("\t1\n", 1)]
        [TestCase("1.0", 1)]
        [TestCase("1.5", -1)]
        [TestCase("1e0", 1)]
        [TestCase("0.2e1", 2)]
        [TestCase(".1e1", 1)]
        [TestCase("1.", 1)]
        [TestCase("+.1e1", 1)]
        [TestCase("0x1", 1)]
        [TestCase("0X2", 2)]
        [TestCase("0o1", 1)]
        [TestCase("0b10", 2)]
        [TestCase("0B1", 1)]
        [TestCase("0x0000000000000001", 1)]
        [TestCase("-0x1", -1)]
        [TestCase("0x", -1)]
        [TestCase("0b", -1)]
        [TestCase("1e", -1)]
        [TestCase("1e+", -1)]
        [TestCase("+", -1)]
        [TestCase("-", -1)]
        [TestCase(".", -1)]
        [TestCase("", 0)]
        [TestCase("   ", 0)]
        [TestCase("1x", -1)]
        [TestCase("Infinity", -1)]
        [TestCase("NaN", -1)]
        [TestCase("00002", 2)]
        [TestCase("1_0", -1)]
        [TestCase("\u00A01", 1)]
        [TestCase("\u30002", 2)]
        [TestCase("\uFEFF1", 1)]
        [TestCase("\u00851", -1)]
        [TestCase("\u20281", 1)]
        [TestCase("1\u2029", 1)]
        [TestCase("2.0000000000000001", 2)]
        [TestCase("9007199254740993", -1)]
        [TestCase("1e400", -1)]
        public void TakeFound_ReadsItsSlot_WithJsNumbersRules(string arg, int slot)
        {
            // js: ExplorationSession.js:399 (Number(arg)); the cases are the browser's own answers.
            var s = Trial(1, consumables: new[] { "yakuso", "wasuremizu", "koi_hoshiniku" });
            Win(s);
            s.Receive(new[] { "joka_no_ko" });
            Assert.That(s.Act(ScreenAction.TakeFound, arg), Is.EqualTo(slot >= 0));
            var ids = s.State.Loadout.Consumables.Select(c => c.Id).ToList();
            if (slot >= 0) Assert.That(ids[slot], Is.EqualTo("joka_no_ko"));
            else Assert.That(ids, Is.EqualTo(new[] { "yakuso", "wasuremizu", "koi_hoshiniku" }));
        }

        [Test]
        public void TheKeepersFinds_AreRecords_AsTheContractNamesThem()
        {
            // the plan's §6.5: ChestFind is a record; PendingEvent's { NodeId, Kind } compares by value too.
            var waiting = new PendingEventInfo(4, EventKind.HeavyChest);
            Assert.That(waiting, Is.EqualTo(new PendingEventInfo(4, EventKind.HeavyChest)));
            Assert.That(waiting, Is.Not.EqualTo(new PendingEventInfo(4, EventKind.StrangeMeal)));
            var (node, kind) = waiting;
            Assert.That((node, kind), Is.EqualTo((4, EventKind.HeavyChest)));

            var cards = new[] { "card-a" };
            var consumables = new[] { "yakuso" };
            Assert.That(new ChestFind(cards, consumables, 1, true), Is.EqualTo(new ChestFind(cards, consumables, 1, true)));
            var (dealt, found, pages, spent) = new ChestFind(null, null, 0, false);
            Assert.That(dealt, Is.Empty);
            Assert.That(found, Is.Empty);
            Assert.That(pages, Is.EqualTo(0));
            Assert.That(spent, Is.False);
            Assert.That(ChestFind.None.Cards, Is.Empty);
            Assert.That(ChestFind.None.Consumables, Is.Empty);
        }

        // ---------------------------------------------------------------- events (§5)

        [Test]
        public void TheTwoEventsOfALayer_NeverRepeatAKind_AndLayerSevenNeverDrawsASpring()
        {
            // js: test/ext/world.test.mjs:647
            var counts = new Dictionary<EventKind, int> { [EventKind.Spring] = 0, [EventKind.HeavyChest] = 0, [EventKind.StrangeMeal] = 0 };
            for (ulong seed = 1; seed <= 300; seed++)
            {
                foreach (int layer in new[] { 1, 2, 3 })
                {
                    var map = SevenLayers.Of(layer).Map(seed);
                    var kinds = LayerEvents.Draw(map, layer, id => ExplorationSession.EventSeed(seed, layer, id)).Values.ToList();
                    Assert.That(kinds.Count, Is.EqualTo(2));
                    Assert.That(kinds[0], Is.Not.EqualTo(kinds[1]), $"layer {layer} seed {seed}");
                    if (layer == 1) foreach (var k in kinds) counts[k]++;
                }
                var map7 = SevenLayers.Of(7).Map(seed);
                var k7 = LayerEvents.Draw(map7, 7, id => ExplorationSession.EventSeed(seed, 7, id)).Values.ToList();
                Assert.That(k7.Count, Is.EqualTo(1));
                Assert.That(k7[0], Is.Not.EqualTo(EventKind.Spring));
            }
            // with layer one's weights, the spring is the commonest
            Assert.That(counts[EventKind.Spring] > counts[EventKind.StrangeMeal] && counts[EventKind.StrangeMeal] > counts[EventKind.HeavyChest],
                Is.True, string.Join(",", counts.Select(p => $"{p.Key}={p.Value}")));
        }

        [Test]
        public void TheDraw_IsSeededFromTheNode_SoALifeReplaysTheSameEvents()
        {
            // js: test/ext/world.test.mjs:666
            var a = Trial(9);
            var b = Trial(9);
            var ids = a.State.Map.Nodes.Where(n => n.Kind == NodeKind.Event).Select(n => n.Id).ToList();
            Assert.That(ids.Select(id => a.EventAt(id)), Is.EqualTo(ids.Select(id => b.EventAt(id))));
            Assert.That(a.EventAt(a.State.Map.EntryId), Is.Null);
            Assert.That(ExplorationSession.EventSeed(9, 1, 4), Is.EqualTo(ExplorationSession.EncounterSeed(9, 1, 4) ^ 0x9E6C63D0676A9A99UL));
        }

        [Test]
        public void AnEventsContents_ShowOnceItIsNextDoor_NotWhileFar()
        {
            // js: test/ext/world.test.mjs:675
            var (seed, node) = SeedWithEvent(EventKind.HeavyChest, (s, n) => !s.State.Map.Neighbours(s.State.CurrentNodeId).Contains(n.Id));
            var ex = Trial(seed);
            Win(ex);
            ClearChoices(ex);
            var card = ex.Screen().Node(node);
            Assert.That(card.Standing, Is.EqualTo(NodeStanding.Far));
            Assert.That(card.Lines, Is.Empty);
            WalkTo(ex, n => n.Id != node && ex.State.Map.Neighbours(n.Id).Contains(node));
            ClearChoices(ex);
            if (ex.HasPendingBattle) Win(ex);
            ClearChoices(ex);
            var near = ex.Screen().Node(node);
            Assert.That(near.Lines, Is.EqualTo(new[] { "重い宝箱: 最大スタミナ −2（一時的な変化）", "中身: カード 1 枚 + 消耗品 1 個", ExplorationText.EventChoiceNote }));
        }

        [Test]
        public void 泉_HealsOnArrival_ByTheLayersShare_AndIsRecorded()
        {
            // js: test/ext/world.test.mjs:693
            var (seed, node) = SeedWithEvent(EventKind.Spring);
            var s = Trial(seed);
            Win(s);
            ClearChoices(s);
            WalkTo(s, n => n.Id == node, RestChoice.Rest, session => session.State = session.State.With(hp: 20));
            Assert.That(s.HasPendingEvent, Is.False, "a spring asks nothing");
            Assert.That(s.State.Hp, Is.EqualTo(30), "20% of 50");
            var last = s.Events[s.Events.Count - 1];
            Assert.That(last.Kind, Is.EqualTo(EventKind.Spring));
            Assert.That(last.Name, Is.EqualTo("泉"));
            Assert.That(last.Accepted, Is.True);
            Assert.That(last.Lines, Is.EqualTo(new[] { "HP 20 → 30" }));
            Assert.That(s.EventState, Is.SameAs(s.State));
            Assert.That(Row(s, "dungeon-1", "出来事"), Is.EqualTo("泉: HP 20 → 30"));
            Assert.That(s.Screen().Node(node).Lines.Last(), Is.EqualTo("結果: HP 20 → 30"));
        }

        [Test]
        public void ASpringsStep_ForecastsItsHeal()
        {
            // W-13: 「the step's forecast counts it」.
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var s = Trial(seed, hp: 30);
                Win(s, 30);
                var spring = s.Screen().Nodes.FirstOrDefault(n => n.Kind == NodeKind.Event && n.Standing == NodeStanding.Reachable &&
                    s.EventAt(n.Id) == EventKind.Spring);
                if (spring == null) continue;
                var option = spring.Options.Single();
                Assert.That(option.Forecast.Lines.Last(), Is.EqualTo($"HP 30 → {30 + ExplorationReducer.SpringHeal(s.State)}"));
                Assert.That(spring.Lines[0], Is.EqualTo(ExplorationText.SpringLine(ExplorationReducer.SpringHeal(s.State), 20, 20)));
                return;
            }
            Assert.Fail("no seed has a 泉 next to the entry");
        }

        [Test]
        public void 重い宝箱_Accepted_CostsTwoMaxStamina_AndGivesACardAConsumableAndNoPageOnLayerOne()
        {
            // js: test/ext/world.test.mjs:708
            var (seed, node) = SeedWithEvent(EventKind.HeavyChest);
            var s = Trial(seed);
            var keeper = (TrialKeeper)s.Keeper;
            Win(s, 50, 9);
            ClearChoices(s);
            WalkTo(s, n => n.Id == node);
            Assert.That(s.HasPendingEvent, Is.True);
            Assert.That(s.PendingEvent.NodeId, Is.EqualTo(node));
            Assert.That(s.PendingEvent.Kind, Is.EqualTo(EventKind.HeavyChest));
            Assert.That(s.IsIdle, Is.False);
            var card = s.Screen().Card;
            Assert.That(card.Kind, Is.EqualTo(CardKind.EventChoice));
            Assert.That(card.Title, Is.EqualTo("重い宝箱"));
            Assert.That(card.Buttons.Select(b => (b.Label, b.Action)), Is.EqualTo(new[] { ("受ける", ScreenAction.AcceptEvent), ("受けない", ScreenAction.DeclineEvent) }));
            Assert.That(card.Lines.First(), Is.EqualTo("重い宝箱がある。受けるか、受けずに通り過ぎるかを選ぶ。"));
            Assert.That(card.Lines.Last(), Is.EqualTo(ExplorationText.TempClampNote));
            Assert.That(card.Lines, Has.No.Member(ExplorationText.EventChoiceNote));
            Assert.That(card.Buttons[1].Note, Is.EqualTo(ExplorationText.DeclineNote));
            var before = s.State;
            Assert.That(card.Buttons[0].Note, Is.EqualTo(string.Join("　",
                ExplorationScreenBuilder.ChangeLines(before, ExplorationReducer.ResolveEvent(before, EventKind.HeavyChest, true)))));
            Assert.That(s.Act(ScreenAction.AcceptEvent), Is.True);
            Assert.That(s.State.TempMaxStaminaMod, Is.EqualTo(before.TempMaxStaminaMod - 2));
            Assert.That(s.State.MaxStamina, Is.EqualTo(before.MaxStamina - 2));
            Assert.That(s.State.Stamina, Is.LessThanOrEqualTo(s.State.MaxStamina), "current stamina follows the lower maximum");
            Assert.That(s.State.TimeLeft, Is.EqualTo(before.TimeLeft), "the 刻限 was paid by the step");
            Assert.That(keeper.Chests, Is.EqualTo(new[] { (1, node, new ChestContents(1, 1, 0)) }));
            var last = s.Events[s.Events.Count - 1];
            Assert.That(last.Cards.Count, Is.EqualTo(3), "the keeper lays out three");
            Assert.That(last.Pages, Is.EqualTo(0));
            Assert.That(last.Consumables.Count, Is.EqualTo(1));
            Assert.That(s.State.Loadout.Consumables.Any(c => c.Id == last.Consumables[0]), Is.True, "the consumable is in a slot");
            Assert.That(last.Lines, Has.Member(ExplorationText.FoundCards(3)));
            Assert.That(last.Record.Skip(1), Is.EqualTo(new[] { "カード 1 枚", "消耗品: 濃い干し肉" }));
        }

        [Test]
        public void 重い宝箱_WithoutAKeeper_GivesItsCostOnly()
        {
            var (seed, node) = SeedWithEvent(EventKind.HeavyChest);
            var s = Trial(seed);
            s.Keeper = null;
            Win(s);
            ClearChoices(s);
            WalkTo(s, n => n.Id == node);
            Assert.That(s.Act(ScreenAction.AcceptEvent), Is.True);
            var last = s.Events[s.Events.Count - 1];
            Assert.That(last.Cards, Is.Empty);
            Assert.That(last.Consumables, Is.Empty);
            Assert.That(last.Record.Count, Is.EqualTo(1));
        }

        [Test]
        public void 重い宝箱_OnLayerTwo_HoldsACardAndAPage()
        {
            // js: test/ext/world.test.mjs:737
            ulong seed = 1;
            for (; seed <= 200; seed++)
            {
                ulong at = seed;
                var map = SevenLayers.Of(2).Map(ExplorationSession.LayerSeed(at, 2));
                if (LayerEvents.Draw(map, 2, id => ExplorationSession.EventSeed(at, 2, id)).Values.Contains(EventKind.HeavyChest)) break;
            }
            Assert.That(seed, Is.LessThanOrEqualTo(200UL));
            var s = Trial(seed);
            WalkToLayer(s, 2);
            var node = s.State.Map.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Event && s.EventAt(n.Id) == EventKind.HeavyChest);
            Assert.That(node, Is.Not.Null, "the chest is on the map the session walks");
            WalkTo(s, n => n.Id == node.Id);
            s.Act(ScreenAction.AcceptEvent);
            var last = s.Events[s.Events.Count - 1];
            Assert.That(last.Consumables, Is.Empty);
            Assert.That(last.Cards.Count, Is.EqualTo(3));
            Assert.That(last.Pages, Is.EqualTo(1));
            Assert.That(last.Record[0], Does.Match(@"^最大スタミナ \d+ → \d+$"));
            Assert.That(last.Record.Skip(1), Is.EqualTo(new[] { "カード 1 枚", "希少な頁 1 枚" }));
        }

        [Test]
        public void 特殊な食事_Accepted_AddsOneMaxStamina_AndTakesTenPercentHp_NeverBelowOne()
        {
            // js: test/ext/world.test.mjs:761
            var (seed, node) = SeedWithEvent(EventKind.StrangeMeal);
            var s = Trial(seed);
            Win(s);
            ClearChoices(s);
            WalkTo(s, n => n.Id == node, RestChoice.Rest, session => session.State = session.State.With(hp: 3));
            Assert.That(s.HasPendingEvent, Is.True);
            var before = s.State;
            s.Act(ScreenAction.AcceptEvent);
            Assert.That(s.State.MaxStamina, Is.EqualTo(Math.Min(before.MaxStamina + 1, 14)));
            Assert.That(s.State.Hp, Is.EqualTo(1), "3 − 5 stops at 1");
            Assert.That(s.State.Phase, Is.EqualTo(RunPhase.Exploring), "a meal never kills");
        }

        [Test]
        public void 受けない_ChangesNothing_ButTheStepAlreadyPaidItsTime()
        {
            // js: test/ext/world.test.mjs:778
            var (seed, node) = SeedWithEvent(EventKind.StrangeMeal);
            var s = Trial(seed);
            Win(s);
            ClearChoices(s);
            int t0 = s.State.TimeLeft;
            WalkTo(s, n => n.Id == node);
            var before = s.State;
            Assert.That(before.TimeLeft, Is.LessThan(t0));
            Assert.That(s.Act(ScreenAction.DeclineEvent), Is.True);
            Assert.That(s.State, Is.SameAs(before));
            Assert.That(s.Events[s.Events.Count - 1].Lines, Is.EqualTo(new[] { "受けずに通り過ぎた" }));
            Assert.That(s.Act(ScreenAction.DeclineEvent), Is.False, "nothing waits any more");
            Assert.That(Row(s, "dungeon-1", "出来事"), Does.EndWith("特殊な食事: 受けなかった"));
            Assert.That(s.Screen().Node(node).Lines.Last(), Is.EqualTo("結果: 受けなかった"));
        }

        [Test]
        public void AnEventOnTheLastTime_StillWaits_BeforeTheLayerBreakCard()
        {
            // js: test/ext/world.test.mjs:796
            var (seed, node) = SeedWithEvent(EventKind.HeavyChest);
            var s = Trial(seed);
            Win(s);
            ClearChoices(s);
            WalkTo(s, n => n.Id == node);
            s.State = s.State.With(phase: RunPhase.PushedOut);
            Assert.That(s.Screen().Card.Kind, Is.EqualTo(CardKind.EventChoice));
            Assert.That(s.Act(ScreenAction.GoToInterlude), Is.False, "the event is answered first");
            s.Act(ScreenAction.DeclineEvent);
            Assert.That(s.Screen().Card.Kind, Is.EqualTo(CardKind.PushedOut));
        }

        [Test]
        public void TheNewLife_ForgetsTheEventsAndTheChoices()
        {
            // js: test/ext/world.test.mjs:810
            var (seed, node) = SeedWithEvent(EventKind.HeavyChest);
            var s = Trial(seed);
            Win(s);
            ClearChoices(s);
            WalkTo(s, n => n.Id == node);
            s.Act(ScreenAction.DeclineEvent);
            Assert.That(s.Events.Count, Is.GreaterThan(0));
            s.State = ExplorationReducer.EndLife(s.State);
            Assert.That(s.Act(ScreenAction.NewLife), Is.True);
            Assert.That(s.Events, Is.Empty);
            Assert.That(s.HasPendingEvent || s.HasPendingFound || s.HasPendingSurvey || s.IsAiming, Is.False);
            Assert.That(s.State.Trial, Is.True);
        }

        [Test]
        public void TheJournalsEventsRow_SaysWhenNothingHappenedYet()
        {
            var s = Trial(1);
            Assert.That(s.State.Map.CountOf(NodeKind.Event), Is.GreaterThan(0));
            Assert.That(Row(s, "dungeon-1", ExplorationText.EventsRow), Is.EqualTo(ExplorationText.NoEvents));
            var plain = new ExplorationSession(new RunStart { Seed = 1 });
            Assert.That(plain.Screen().Journal.Find("dungeon-1").Rows.Any(r => r.Label == ExplorationText.EventsRow), Is.False);
        }

        // ---------------------------------------------------------------- X-16 and the trace node

        [Test]
        public void X16_AFinished情報収集_NamesTheEnemyItRaised_OnTheNodeToo_AndSaysWhenNothingWasLeft()
        {
            // js: test/ext/world.test.mjs:937
            var s = Trial(3, intel: new SurveyIntel("shadow_hound"));
            int node = WalkTo(s, n => n.Kind == NodeKind.Survey);
            string line = ExplorationScreenBuilder.SurveyLine(s);
            Assert.That(line, Is.EqualTo(ExplorationText.SurveyDone("瘴牙の走竜")));
            var detail = s.Screen().Nodes.First(n => n.Id == node).Detail;
            Assert.That(detail, Has.Member(line), "the node says it");
            Assert.That(detail, Has.No.Member("この層では済ませた"), "instead of only that it was done");
            // an intel with nothing below 2 left: the survey says so
            var none = Trial(3, intel: new SurveyIntel(null));
            WalkTo(none, n => n.Kind == NodeKind.Survey);
            Assert.That(ExplorationScreenBuilder.SurveyLine(none), Is.EqualTo(ExplorationText.SurveyNothing));
            // 写しの帳: nothing to say while the pick waits
            var book = Trial(3, new[] { Book });
            WalkTo(book, n => n.Kind == NodeKind.Survey);
            Assert.That(ExplorationScreenBuilder.SurveyLine(book), Is.Null);
            string target = book.SurveyChoices[0];
            book.Act(ScreenAction.ChooseSurvey, target);
            Assert.That(ExplorationScreenBuilder.SurveyLine(book), Is.EqualTo(ExplorationText.SurveyChosen(book.Keeper.EnemyName(target))));
            // outside trial the C#'s words stay
            var off = new ExplorationSession(new RunStart { Seed = 3 }, new SurveyIntel("shadow_hound"));
            Assert.That(ExplorationScreenBuilder.SurveyLine(off), Is.Null);
        }

        [Test]
        public void TheTraceNode_SaysItWasCollected_OnceTheLayersTraceIsTaken()
        {
            // js: ExplorationScreenBuilder.js:169 (X-3)
            var map = SevenLayers.Of(1).MapSpec.With(NodeKind.Trace, 1);
            var s = Trial(1, mapFor: (profile, seed) => profile.Layer == 1 ? MapGenerator.Generate(map, seed) : profile.Map(seed));
            var keeper = (TrialKeeper)s.Keeper;
            Win(s);
            int trace = WalkTo(s, n => n.Kind == NodeKind.Trace);
            Assert.That(s.Screen().Node(trace).Lines[0], Is.EqualTo("前の生の遺産を受け取れる"));
            keeper.InheritedTraceId = "trace-1";
            keeper.InheritedLayer = 2;
            Assert.That(s.Screen().Node(trace).Lines[0], Is.EqualTo("前の生の遺産を受け取れる"), "another layer's trace");
            keeper.InheritedLayer = 1;
            Assert.That(s.Screen().Node(trace).Lines, Is.EqualTo(new[] { ExplorationText.TraceCollected, "受け取れるのは 1 つの生で 1 件" }));
        }

        [Test]
        public void AnArrivalIntel_IsToldEveryResolvedNode_AfterTheSession()
        {
            // js: app/intel.js:62 (JournalIntel.bind's hook), here the IArrivalIntel socket; off by default (NoEnemyIntel).
            var intel = new ArrivalLog();
            var s = new ExplorationSession(new RunStart { Seed = 1 }, intel);
            Assert.That(intel.Nodes, Is.EqualTo(new[] { s.State.CurrentNodeId }), "the entry");
            Win(s);
            int next = s.State.ReachableUnresolved().First();
            Assert.That(s.Step(next), Is.True);
            Assert.That(intel.Nodes.Last(), Is.EqualTo(next));
            if (s.HasPendingBattle) Win(s);
            int count = intel.Nodes.Count;
            Assert.That(s.Step(s.State.Map.EntryId), Is.True);
            Assert.That(intel.Nodes.Count, Is.EqualTo(count), "walking back resolves nothing");
        }

        private sealed class ArrivalLog : IEnemyIntel, IArrivalIntel
        {
            public List<int> Nodes { get; } = new List<int>();

            public NodeIntel ForNode(MapNode node, int layer, bool surveyed) => null;

            public IReadOnlyList<JournalPage> Pages(int layer, bool surveyed) => new JournalPage[0];

            public void Arrived(int nodeId) => Nodes.Add(nodeId);
        }
    }

    /// <summary>The trial tests' helpers: the stand-in keeper and intels, and the walks (js: test/ext/world.test.mjs:37-106).</summary>
    internal static class TrialTestKit
    {
        public const string Sand = "kokugen_no_suna";
        public const string Book = "utsushi_no_tobari";
        public const string Wick = "toshin_no_kakera";
        public const string Mask = "bosho_no_men";
        public const string Flute = "ikitsugi_no_fue";

        /// <summary>A trial session from a departure's choice, the way the game builds one, with a stand-in keeper.</summary>
        public static ExplorationSession Trial(ulong seed, string[] tools = null, string[] consumables = null, int hp = 50, int miasma = 0,
            IEnemyIntel intel = null, Func<LayerProfile, ulong, LayerMap> mapFor = null, bool? carvingKnown = null)
        {
            tools = tools ?? new string[0];
            consumables = consumables ?? new string[0];
            int slots = Math.Max(RunLoadout.ToolSlots, tools.Length);
            var loadout = new RunLoadout(tools.Select(TrialItems.Tool).ToList(), consumables.Select(TrialItems.Consumable).ToList(), slots, trial: true);
            var start = new RunStart { Seed = seed, Loadout = loadout, Hp = hp, MiasmaPercent = miasma, Trial = true, MapFor = mapFor, CarvingKnown = carvingKnown };
            var session = new ExplorationSession(start, intel);
            session.Keeper = new TrialKeeper();
            return session;
        }

        /// <summary>Wins the waiting battle with this HP left: what Expedition.Return reports.</summary>
        public static void Win(ExplorationSession session, int hp = 50, int stamina = 5)
        {
            Assert.That(session.HasPendingBattle, Is.True, "a battle waits");
            Assert.That(session.ReportBattle(hp, stamina), Is.True);
        }

        /// <summary>Clears whatever trial choice waits: swaps leave, events decline, survey picks the first.</summary>
        public static void ClearChoices(ExplorationSession session)
        {
            while (session.HasPendingFound) session.Act(ScreenAction.LeaveFound);
            if (session.HasPendingEvent) session.Act(ScreenAction.DeclineEvent);
            if (session.HasPendingSurvey) session.Act(ScreenAction.ChooseSurvey, session.SurveyChoices[0]);
        }

        /// <summary>The nodes from where the player stands to the nearest unresolved node matching, that node last.</summary>
        public static List<int> PathTo(ExplorationState state, Func<MapNode, bool> want)
        {
            var map = state.Map;
            var from = new Dictionary<int, int> { [state.CurrentNodeId] = -1 };
            var queue = new Queue<int>();
            queue.Enqueue(state.CurrentNodeId);
            int target = -1;
            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                if (want(map.Node(node)) && !state.HasResolved(node))
                {
                    target = node;
                    break;
                }
                foreach (int next in map.Neighbours(node))
                {
                    if (from.ContainsKey(next)) continue;
                    from[next] = node;
                    queue.Enqueue(next);
                }
            }
            Assert.That(target, Is.Not.EqualTo(-1), "a matching node is reachable");
            var path = new List<int>();
            for (int node = target; node != state.CurrentNodeId; node = from[node]) path.Add(node);
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Walks to the nearest node matching, winning battles and clearing choices on the way (not at the end).
        /// <paramref name="beforeLast"/> runs right before the last step.
        /// </summary>
        public static int WalkTo(ExplorationSession s, Func<MapNode, bool> want, RestChoice choice = RestChoice.Rest,
            Action<ExplorationSession> beforeLast = null)
        {
            if (s.HasPendingBattle) Win(s);
            ClearChoices(s);
            var path = PathTo(s.State, want);
            foreach (int node in path)
            {
                bool last = node == path[path.Count - 1];
                if (last) beforeLast?.Invoke(s);
                Assert.That(s.Step(node, last ? choice : RestChoice.Rest), Is.True, $"step to {node}");
                if (s.HasPendingStep) s.Act(ScreenAction.ConfirmStep);
                if (!last)
                {
                    if (s.HasPendingBattle) Win(s);
                    ClearChoices(s);
                }
            }
            return path[path.Count - 1];
        }

        /// <summary>Clears layers to the boss until the given layer's entry is reported (js: world.test.mjs:867).</summary>
        public static void WalkToLayer(ExplorationSession s, int layer)
        {
            while (s.State.Profile.Layer < layer)
            {
                WalkTo(s, n => n.Kind == NodeKind.Boss);
                Win(s);
                ClearChoices(s);
                Assert.That(s.Act(ScreenAction.GoToInterlude), Is.True);
                Assert.That(s.Act(ScreenAction.Descend), Is.True);
                if (s.HasPendingBattle) Win(s);
                ClearChoices(s);
            }
        }

        /// <summary>The first seed (1〜200) whose layer-1 map has an event of this kind.</summary>
        public static (ulong Seed, int Node) SeedWithEvent(EventKind kind, Func<ExplorationSession, MapNode, bool> extra = null)
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var s = Trial(seed);
                var node = s.State.Map.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Event && s.EventAt(n.Id) == kind);
                if (node != null && (extra == null || extra(s, node))) return (seed, node.Id);
            }
            Assert.Fail($"no seed has a {kind}");
            return (0, -1);
        }

        /// <summary>The text of a row of a journal page.</summary>
        public static string Row(ExplorationSession s, string pageId, string label) =>
            s.Screen().Journal.Find(pageId).Rows.First(r => r.Label == label).Text;
    }

    /// <summary>
    /// A stand-in for the Expedition as the session's keeper: a journal of 開示度, the names a few screens print, and a
    /// chest that lays out three cards and gives 濃い干し肉 for its consumable.
    /// </summary>
    internal sealed class TrialKeeper : IExplorationKeeper
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["shadow_hound"] = "瘴牙の走竜",
        };

        public Dictionary<string, int> Journal { get; } = new Dictionary<string, int>();

        public List<(int, int, ChestContents)> Chests { get; } = new List<(int, int, ChestContents)>();

        public string InheritedTraceId { get; set; }

        public int? InheritedLayer { get; set; }

        public ChestFind TakeChest(int layer, int nodeId, ChestContents contents)
        {
            Chests.Add((layer, nodeId, contents));
            var cards = contents.Cards > 0 ? new[] { "card-a", "card-b", "card-c" } : new string[0];
            var consumables = Enumerable.Repeat("koi_hoshiniku", contents.Consumables).ToList();
            return new ChestFind(cards, consumables, contents.Pages, false);
        }

        public void TakeSurvey(string enemyId) => Journal[enemyId] = 2;

        public int DisclosureOf(string enemyId) => Journal.TryGetValue(enemyId, out int d) ? d : 0;

        public string EnemyName(string enemyId) => Names.TryGetValue(enemyId, out string name) ? name : enemyId;
    }

    /// <summary>An intel that names the layer's 情報収集 target (the journal job's JournalIntel.SurveyTarget).</summary>
    internal sealed class SurveyIntel : IEnemyIntel, ISurveyIntel
    {
        private readonly string _target;

        public SurveyIntel(string target)
        {
            _target = target;
        }

        public NodeIntel ForNode(MapNode node, int layer, bool surveyed) => null;

        public IReadOnlyList<JournalPage> Pages(int layer, bool surveyed) => new JournalPage[0];

        public string SurveyTarget(int layer, LayerMap map) => _target;
    }
}
