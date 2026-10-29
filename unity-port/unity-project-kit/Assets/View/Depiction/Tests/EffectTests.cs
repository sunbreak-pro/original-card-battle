// The effects' ids, lengths and switches (Script/Effects.cs, #75). The View animates for the
// catalog's lengths and asks the switches whether to animate at all, so what is held here is what
// the screen plays. Seeing the effects themselves is a capture in the Editor (visual-inspect).
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Depiction.Tests
{
    public class EffectTests
    {
        // ---- The catalog ----

        [Test]
        public void EveryEffect_HasALength()
        {
            foreach (EffectId id in Enum.GetValues(typeof(EffectId)))
            {
                EffectSpec spec = EffectCatalog.Of(id);
                Assert.That(spec.Id, Is.EqualTo(id));
                Assert.That(spec.Ms, Is.GreaterThan(0f), id + " plays for some time when it is on");
                Assert.That(spec.StaggerMs, Is.GreaterThanOrEqualTo(0f), id.ToString());
                Assert.That(spec.Source, Is.Not.Empty, id + " says where its length comes from");
            }
            Assert.That(EffectCatalog.All.Count, Is.EqualTo(Enum.GetValues(typeof(EffectId)).Length));
        }

        // battle_ui_ux_v2 §3.2 unless the issue chose the length (#75).
        [TestCase(EffectId.CardDraw, 260f, 60f, true)]
        [TestCase(EffectId.HandFan, 120f, 0f, false)]
        [TestCase(EffectId.CardHover, 90f, 0f, false)]
        [TestCase(EffectId.CardGrab, 90f, 0f, false)]
        [TestCase(EffectId.ReceiverShow, 150f, 0f, false)]
        [TestCase(EffectId.ThrowLineShow, 150f, 0f, false)]
        [TestCase(EffectId.ReceiverSnap, 80f, 0f, false)]
        [TestCase(EffectId.CardRelease, 160f, 0f, true)]
        [TestCase(EffectId.CardToDiscard, 200f, 0f, false)]
        [TestCase(EffectId.CardToExile, 220f, 0f, false)]
        [TestCase(EffectId.PileRebuild, 200f, 0f, true)]
        [TestCase(EffectId.CardReturn, 200f, 0f, false)]
        [TestCase(EffectId.RefusalShake, 160f, 0f, false)]
        [TestCase(EffectId.HandDiscard, 240f, 60f, true)]
        [TestCase(EffectId.UnpayableDim, 120f, 0f, false)]
        public void TheCardMotion_PlaysForTheLengthsTheCanonWrites(EffectId id, float ms, float stagger, bool blocking)
        {
            EffectSpec spec = EffectCatalog.Of(id);
            Assert.That(spec.Ms, Is.EqualTo(ms));
            Assert.That(spec.StaggerMs, Is.EqualTo(stagger));
            Assert.That(spec.Blocking, Is.EqualTo(blocking));
        }

        // #76: the waits keep the slice's lengths (the budget is spent to 1990 ms); the rest runs beside them.
        [TestCase(EffectId.AttackLunge, 100f, true)]
        [TestCase(EffectId.StrikeShape, 120f, true)]
        [TestCase(EffectId.HitStop, 30f, true)]
        [TestCase(EffectId.HitStopStrong, 90f, true)]
        [TestCase(EffectId.HitFlash, 260f, false)]
        [TestCase(EffectId.TargetRecoil, 260f, false)]
        [TestCase(EffectId.DamageNumber, 1000f, false)]
        [TestCase(EffectId.HpDrain, 300f, true)]
        [TestCase(EffectId.HpTrail, 500f, false)]
        [TestCase(EffectId.ScreenShake, 180f, false)]
        [TestCase(EffectId.AttackReturn, 120f, true)]
        [TestCase(EffectId.EnemyMotion, 220f, true)]
        [TestCase(EffectId.OmenSpend, 200f, true)]
        [TestCase(EffectId.EnemyReturn, 140f, true)]
        [TestCase(EffectId.GuardBlock, 320f, true)]
        [TestCase(EffectId.GuardNumber, 1000f, false)]
        [TestCase(EffectId.GuardBreak, 200f, false)]
        [TestCase(EffectId.GuardGain, 260f, true)]
        public void TheAttackAndDefence_PlayForTheirLengths(EffectId id, float ms, bool blocking)
        {
            EffectSpec spec = EffectCatalog.Of(id);
            Assert.That(spec.Ms, Is.EqualTo(ms));
            Assert.That(spec.Blocking, Is.EqualTo(blocking));
        }

        [Test]
        public void TheStrength_IsTwoStepsForNow()
        {
            Assert.That(EffectStrength.IsStrong(1), Is.False);
            Assert.That(EffectStrength.IsStrong(2), Is.False);
            Assert.That(EffectStrength.IsStrong(3), Is.True);
            Assert.That(EffectStrength.IsStrong(4), Is.True);
        }

        [Test]
        public void ASlash_WaitsForTheLunge_TheShape_TheStop_TheDrain_AndTheReturn()
        {
            var direct = new DepictionEvent { Kind = DepictionEventKind.PlayCard, After = new DepictionFrame() };
            direct.Cues.Add(new Cue { Kind = CueKind.Slash, Source = UnitSide.Player, Target = UnitSide.Enemy, Intensity = 3, HpAfter = 40 });
            Assert.That(Steps(direct), Is.EqualTo(new[]
            {
                (EffectId.CardRelease, 1), (EffectId.AttackLunge, 1), (EffectId.StrikeShape, 1),
                (EffectId.HitStopStrong, 1), (EffectId.HpDrain, 1), (EffectId.AttackReturn, 1),
            }));
            Assert.That(EffectPlan.BlockingMs(direct, EffectSwitches.AllOn()), Is.EqualTo(160f + 100f + 120f + 90f + 300f + 120f));

            // An enemy blow: it steps in (#288), swings, then the shield and the wound, then it draws back.
            var enemy = new DepictionEvent { Kind = DepictionEventKind.EnemyAction, After = new DepictionFrame() };
            enemy.Cues.Add(new Cue { Kind = CueKind.EnemyWindup, Target = UnitSide.Enemy, System = StrikeSystem.Strike });
            enemy.Cues.Add(new Cue { Kind = CueKind.Slash, Source = UnitSide.Enemy, Target = UnitSide.Player, Intensity = 1 });
            enemy.Cues.Add(new Cue { Kind = CueKind.GuardBlock, Target = UnitSide.Player, Amount = 3, GuardAfter = 0 });
            enemy.Cues.Add(new Cue { Kind = CueKind.Hit, Target = UnitSide.Player, Amount = 5, Intensity = 1, HpAfter = 45 });
            Assert.That(Steps(enemy), Is.EqualTo(new[]
            {
                (EffectId.EnemyMotion, 1), (EffectId.EnemyLunge, 1), (EffectId.StrikeShape, 1), (EffectId.HitStop, 1),
                (EffectId.GuardBlock, 1), (EffectId.HpDrain, 1), (EffectId.OmenSpend, 1), (EffectId.EnemyReturn, 1),
            }));
            Assert.That(EffectPlan.BlockingMs(enemy, EffectSwitches.AllOn()), Is.EqualTo(220f + 100f + 120f + 30f + 320f + 300f + 200f + 140f));
        }

        [Test]
        public void ABlowAMoveFollows_LeavesOutTheReturn_TheMoveCarriesThePlayerOn()
        {
            // §5.4 順 2 / §5.5 順 8 (#499): 牽制 hits, then steps back one cell; the step back is the move.
            var feint = new DepictionEvent { Kind = DepictionEventKind.PlayCard, After = new DepictionFrame() };
            var blow = new Cue { Kind = CueKind.Slash, Source = UnitSide.Player, Target = UnitSide.Enemy, Intensity = 2, HpAfter = 40 };
            feint.Cues.Add(blow);
            feint.Cues.Add(new Cue { Kind = CueKind.RangeSwitch, Target = UnitSide.Player, PlayerCellAfter = 1, EnemyCellAfter = 4, GapAfter = 2, CellsMoved = 1 });
            Assert.That(EffectPlan.MoveFollows(feint, blow), Is.True);
            Assert.That(Steps(feint), Is.EqualTo(new[]
            {
                (EffectId.CardRelease, 1), (EffectId.AttackLunge, 1), (EffectId.StrikeShape, 1),
                (EffectId.HitStop, 1), (EffectId.HpDrain, 1), (EffectId.FloorMove, 1), (EffectId.GapRecount, 1),
            }));
            // §5.4 順 2: the move starts on the number + 80; the bar drains on beside it.
            Assert.That(EffectPlan.StepsOf(feint).Single(s => s.Id == EffectId.HpDrain).LeadMs, Is.EqualTo(EffectPlan.NextFaceLeadMs));
            Assert.That(EffectPlan.BlockingMs(feint, EffectSwitches.AllOn()), Is.EqualTo(160f + 100f + 120f + 30f + 80f + 160f + 150f));

            // A blow Guard takes part of is three beats; the move starts on the wound's number the same way.
            var split = new DepictionEvent { Kind = DepictionEventKind.PlayCard, After = new DepictionFrame() };
            split.Cues.Add(new Cue { Kind = CueKind.Slash, Source = UnitSide.Player, Target = UnitSide.Enemy, Intensity = 2 });
            split.Cues.Add(new Cue { Kind = CueKind.GuardBlock, Target = UnitSide.Enemy, Amount = 3, GuardAfter = 0 });
            var wound = new Cue { Kind = CueKind.Hit, Target = UnitSide.Enemy, Amount = 5, Intensity = 2, HpAfter = 45 };
            split.Cues.Add(wound);
            split.Cues.Add(new Cue { Kind = CueKind.RangeSwitch, Target = UnitSide.Player, PlayerCellAfter = 1, EnemyCellAfter = 4, GapAfter = 2, CellsMoved = 1 });
            Assert.That(EffectPlan.MoveFollows(split, wound), Is.True);
            Assert.That(EffectPlan.BlockingMs(split, EffectSwitches.AllOn()), Is.EqualTo(160f + 100f + 120f + 30f + 320f + 80f + 160f + 150f));

            // Switched off, the drain holds nothing, lead or not.
            var noDrain = EffectSwitches.AllOn();
            noDrain.Set(EffectId.HpDrain, false);
            Assert.That(EffectPlan.BlockingMs(feint, noDrain), Is.EqualTo(160f + 100f + 120f + 30f + 160f + 150f));

            // Anything between the blow and the move (here a status) and the player steps back as usual.
            var apart = new DepictionEvent { Kind = DepictionEventKind.PlayCard, After = new DepictionFrame() };
            var first = new Cue { Kind = CueKind.Slash, Source = UnitSide.Player, Target = UnitSide.Enemy, Intensity = 2, HpAfter = 40 };
            apart.Cues.Add(first);
            apart.Cues.Add(new Cue { Kind = CueKind.StatusChange, Target = UnitSide.Enemy, Text = "出血", Amount = 1, StacksAfter = 1 });
            apart.Cues.Add(new Cue { Kind = CueKind.RangeSwitch, Target = UnitSide.Player, PlayerCellAfter = 1, EnemyCellAfter = 4, GapAfter = 2, CellsMoved = 1 });
            Assert.That(EffectPlan.MoveFollows(apart, first), Is.False);
            Assert.That(Steps(apart), Does.Contain((EffectId.AttackReturn, 1)));
        }

        [Test]
        public void AnEnemyBlowItsOwnMoveFollows_LeavesOutTheReturn_AndTheOmenFadesBesideTheMove()
        {
            // §5.4 順 2 (#587): 退き撃ち hits, then backs off one. No §5.5 順 8 return: the move starts on the
            // number + 80 from where the lunge left the enemy, and the omen fades beside it.
            var backstep = new DepictionEvent { Kind = DepictionEventKind.EnemyAction, After = new DepictionFrame() };
            backstep.Cues.Add(new Cue { Kind = CueKind.EnemyWindup, Target = UnitSide.Enemy, System = StrikeSystem.Thrust });
            backstep.Cues.Add(new Cue { Kind = CueKind.Slash, Source = UnitSide.Enemy, Target = UnitSide.Player, Intensity = 2 });
            var wound = new Cue { Kind = CueKind.Hit, Target = UnitSide.Player, Amount = 6, Intensity = 2, HpAfter = 44 };
            backstep.Cues.Add(wound);
            backstep.Cues.Add(new Cue { Kind = CueKind.RangeSwitch, Target = UnitSide.Player, PlayerCellAfter = 2, EnemyCellAfter = 5, GapAfter = 2, CellsMoved = 1 });
            Assert.That(EffectPlan.EnemyMoveFollows(backstep, wound), Is.True);
            Assert.That(EffectPlan.MoveFollows(backstep, wound), Is.True, "the player's rule reads any move next");
            Assert.That(Steps(backstep), Is.EqualTo(new[]
            {
                (EffectId.EnemyMotion, 1), (EffectId.EnemyLunge, 1), (EffectId.StrikeShape, 1), (EffectId.HitStop, 1),
                (EffectId.HpDrain, 1), (EffectId.OmenSpend, 1), (EffectId.FloorMove, 1), (EffectId.GapRecount, 1),
            }));
            List<EffectStep> steps = EffectPlan.StepsOf(backstep);
            Assert.That(steps.Single(s => s.Id == EffectId.HpDrain).LeadMs, Is.EqualTo(EffectPlan.NextFaceLeadMs));
            Assert.That(steps.Single(s => s.Id == EffectId.OmenSpend).LeadMs, Is.EqualTo(0f), "beside the move, not before it");
            Assert.That(EffectPlan.BlockingMs(backstep, EffectSwitches.AllOn()), Is.EqualTo(220f + 100f + 120f + 30f + 80f + 160f + 150f));

            // A push moves the player, not the enemy: the enemy still spends its omen and draws back first.
            var shove = new DepictionEvent { Kind = DepictionEventKind.EnemyAction, After = new DepictionFrame() };
            shove.Cues.Add(new Cue { Kind = CueKind.Slash, Source = UnitSide.Enemy, Target = UnitSide.Player, Intensity = 2 });
            var shoved = new Cue { Kind = CueKind.Hit, Target = UnitSide.Player, Amount = 3, Intensity = 2, HpAfter = 47 };
            shove.Cues.Add(shoved);
            shove.Cues.Add(new Cue { Kind = CueKind.RangeSwitch, Target = UnitSide.Player, Pushed = true, PlayerCellAfter = 1, EnemyCellAfter = 3, GapAfter = 1, CellsMoved = 1 });
            Assert.That(EffectPlan.EnemyMoveFollows(shove, shoved), Is.False);
            List<EffectStep> pushSteps = EffectPlan.StepsOf(shove);
            Assert.That(pushSteps.Select(s => s.Id), Does.Contain(EffectId.EnemyReturn));
            Assert.That(pushSteps.Single(s => s.Id == EffectId.HpDrain).LeadMs, Is.Null);
            Assert.That(pushSteps.Single(s => s.Id == EffectId.OmenSpend).LeadMs, Is.Null);

            // The player's blow on the enemy is not the enemy's: the rule asks only about a blow on the player.
            var feint = new DepictionEvent { Kind = DepictionEventKind.PlayCard, After = new DepictionFrame() };
            var cut = new Cue { Kind = CueKind.Hit, Target = UnitSide.Enemy, Amount = 5, Intensity = 2, HpAfter = 45 };
            feint.Cues.Add(cut);
            feint.Cues.Add(new Cue { Kind = CueKind.RangeSwitch, Target = UnitSide.Player, PlayerCellAfter = 1, EnemyCellAfter = 4, GapAfter = 2, CellsMoved = 1 });
            Assert.That(EffectPlan.EnemyMoveFollows(feint, cut), Is.False);
        }

        [Test]
        public void ATraitBeforeTheFirstFace_ShowsBesideTheRelease()
        {
            // §5.4 順 0 / §5.9 順 1 (#499): the trait's cue starts as the card is released, so the release
            // holds nothing of its own and the trait's 200 covers the card's 160 flight.
            var traited = new DepictionEvent { Kind = DepictionEventKind.PlayCard, After = new DepictionFrame() };
            traited.Cues.Add(new Cue { Kind = CueKind.StaminaChange, Target = UnitSide.Player, Amount = -1, StaminaAfter = 2, StaminaMax = 3 });
            traited.Cues.Add(new Cue { Kind = CueKind.TraitFire, Target = UnitSide.Player, Text = "間合い +3" });
            traited.Cues.Add(new Cue { Kind = CueKind.Slash, Source = UnitSide.Player, Target = UnitSide.Enemy, Intensity = 2, HpAfter = 40 });
            List<EffectStep> steps = EffectPlan.StepsOf(traited);
            Assert.That(steps[0].Id, Is.EqualTo(EffectId.CardRelease));
            Assert.That(steps[0].LeadMs, Is.EqualTo(0f));
            Assert.That(steps[1].Id, Is.EqualTo(EffectId.TraitFire));
            Assert.That(EffectPlan.BlockingMs(traited, EffectSwitches.AllOn()), Is.EqualTo(200f + 100f + 120f + 30f + 300f + 120f));

            // A trait cue after the first face (a stance set, a bonus on the Guard) is its own beat as before.
            var later = new DepictionEvent { Kind = DepictionEventKind.PlayCard, After = new DepictionFrame() };
            later.Cues.Add(new Cue { Kind = CueKind.GuardGain, Target = UnitSide.Player, Amount = 3, GuardAfter = 3 });
            later.Cues.Add(new Cue { Kind = CueKind.TraitFire, Target = UnitSide.Player, Text = "連動 +2" });
            Assert.That(EffectPlan.StepsOf(later)[0].LeadMs, Is.Null);
            Assert.That(EffectPlan.BlockingMs(later, EffectSwitches.AllOn()), Is.EqualTo(160f + 260f + 200f));
        }

        [Test]
        public void AFall_IsItsOwnBeat_OfEightHundredMs()
        {
            var fall = new DepictionEvent { Kind = DepictionEventKind.Defeat, After = new DepictionFrame() };
            fall.Cues.Add(new Cue { Kind = CueKind.Defeat, Target = UnitSide.Enemy });
            Assert.That(Steps(fall), Is.EqualTo(new[] { (EffectId.Defeat, 1) }));
            Assert.That(EffectPlan.BlockingMs(fall, EffectSwitches.AllOn()), Is.EqualTo(800f));
            Assert.That(EffectPlan.BlockingMs(fall, EffectSwitches.AllOff()), Is.EqualTo(0f), "switched off, the figure is gone at once");
        }

        // ---- Status and turn (#77) ----

        [TestCase(EffectId.StatusApply, 200f, true)]
        [TestCase(EffectId.StatusStack, 150f, true)]
        [TestCase(EffectId.StatusTick, 120f, true)]
        [TestCase(EffectId.StatusVanish, 150f, true)]
        [TestCase(EffectId.OmenBlink, 250f, false)]
        [TestCase(EffectId.OmenExecute, 200f, false)]
        [TestCase(EffectId.PushMark, 1000f, false)]
        [TestCase(EffectId.TurnBanner, 300f, true)]
        [TestCase(EffectId.EnemyTurnBanner, 400f, true)]
        [TestCase(EffectId.StaminaRecover, 80f, true)]
        [TestCase(EffectId.ResultCard, 400f, false)]
        public void TheStatusAndTurnBeats_PlayForTheirLengths(EffectId id, float ms, bool blocking)
        {
            EffectSpec spec = EffectCatalog.Of(id);
            Assert.That(spec.Ms, Is.EqualTo(ms));
            Assert.That(spec.Blocking, Is.EqualTo(blocking));
        }

        [TestCase(2, 2, EffectId.StatusApply)]   // a new word: 0 → 2
        [TestCase(1, 3, EffectId.StatusStack)]   // more of a word already there
        [TestCase(-1, 1, EffectId.StatusTick)]   // one stack less at the turn start
        [TestCase(-1, 0, EffectId.StatusVanish)] // the last one gone
        public void AStatusChange_IsOneOfTheFourChipBeats(int change, int stacksAfter, EffectId beat)
        {
            var cue = new Cue { Kind = CueKind.StatusChange, Target = UnitSide.Enemy, Text = "鈍足", Amount = change, StacksAfter = stacksAfter };
            Assert.That(StatusBeat.Of(cue), Is.EqualTo(beat));
        }

        [Test]
        public void TheTurn_OpensWithItsBanner_AndHandsOverWithTheEnemysBanner()
        {
            var start = new DepictionEvent { Kind = DepictionEventKind.TurnStart, After = new DepictionFrame() };
            start.Cues.Add(new Cue { Kind = CueKind.StaminaChange, Target = UnitSide.Player, Amount = 3, StaminaAfter = 10, StaminaMax = 10 });
            start.Cues.Add(new Cue { Kind = CueKind.DrawHand, Target = UnitSide.Player, Amount = 5 });
            Assert.That(Steps(start), Is.EqualTo(new[] { (EffectId.TurnBanner, 1), (EffectId.StaminaRecover, 3), (EffectId.CardDraw, 5) }));
            Assert.That(EffectPlan.BlockingMs(start, EffectSwitches.AllOn()), Is.EqualTo(300f + 3 * 80f + 500f), "the pips light 80 ms apart");

            var end = new DepictionEvent { Kind = DepictionEventKind.TurnEnd, After = new DepictionFrame() };
            end.Cues.Add(new Cue { Kind = CueKind.DiscardHand, Target = UnitSide.Player, Amount = 2 });
            Assert.That(Steps(end).Last(), Is.EqualTo((EffectId.EnemyTurnBanner, 1)));
        }

        // ---- The system a blow draws (battle_ui_ux_v2 §5.3) ----

        [TestCase("払い斬り", true, false, false, false, StrikeSystem.Sweep)] // 払 is read before 斬
        [TestCase("穂先の突き", true, false, false, false, StrikeSystem.Thrust)]
        [TestCase("石突きの押し込み", true, false, true, true, StrikeSystem.Strike)] // the push wins over a 突 in the name (the old name of 柄尻の押し込み)
        [TestCase("柄で受ける", false, true, false, false, StrikeSystem.Shield)]
        [TestCase("踏み込み", false, true, true, false, StrikeSystem.Step)]
        [TestCase("袈裟斬り", true, false, false, false, StrikeSystem.Slash)]
        [TestCase("盾打ち", true, true, false, false, StrikeSystem.Strike)]
        [TestCase("打ち下ろし", true, false, false, false, StrikeSystem.Strike)] // 鉄壁の門竜: 斬 under its old name 兜割り, 打 since #311
        [TestCase("牽制", true, false, true, false, StrikeSystem.Sweep)]
        [TestCase("伸び突き", true, false, false, false, StrikeSystem.Thrust)]
        public void TheSystem_ComesFromTheNameAndTheFaces(string name, bool attacks, bool guards, bool moves, bool pushes, StrikeSystem expected)
        {
            Assert.That(StrikeSystems.Of(name, attacks, guards, moves, pushes), Is.EqualTo(expected));
        }

        [Test]
        public void APerCardEffect_StartsEachNextCardOneStaggerLater()
        {
            EffectSpec draw = EffectCatalog.Of(EffectId.CardDraw);
            Assert.That(draw.TotalMs(5), Is.EqualTo(260f + 4 * 60f), "five cards: the last leaves after four staggers");
            Assert.That(draw.TotalMs(1), Is.EqualTo(260f));
            Assert.That(draw.TotalMs(0), Is.EqualTo(0f), "nothing drawn, nothing waited for");
            Assert.That(EffectCatalog.Of(EffectId.HandDiscard).TotalMs(8), Is.EqualTo(240f + 7 * 60f));
        }

        // ---- Switches ----

        [Test]
        public void EveryEffect_IsOnUntilSwitchedOff_AndOffWaitsForNothing()
        {
            var switches = EffectSwitches.AllOn();
            foreach (EffectId id in Enum.GetValues(typeof(EffectId)))
            {
                Assert.That(switches.IsOn(id), Is.True, id.ToString());
                Assert.That(switches.WaitMs(id, 3), Is.EqualTo(EffectCatalog.Of(id).TotalMs(3)), id.ToString());

                switches.Set(id, false);
                Assert.That(switches.IsOn(id), Is.False, id.ToString());
                Assert.That(switches.Ms(id), Is.EqualTo(0f), id + ": off animates for 0 ms");
                Assert.That(switches.StaggerMs(id), Is.EqualTo(0f), id.ToString());
                Assert.That(switches.WaitMs(id, 3), Is.EqualTo(0f), id + ": off holds nothing up");

                switches.Set(id, true);
                Assert.That(switches.IsOn(id), Is.True, id + " can be switched back on");
            }
        }

        [Test]
        public void TheInspectorList_SwitchesOffByName_AndHandsBackWhatItDoesNotKnow()
        {
            var switches = EffectSwitches.WithOff(new[] { "CardDraw", " HandDiscard ", "", "CardDrw", "7", null }, out List<string> unknown);

            Assert.That(switches.IsOn(EffectId.CardDraw), Is.False);
            Assert.That(switches.IsOn(EffectId.HandDiscard), Is.False, "surrounding spaces are forgiven");
            Assert.That(switches.IsOn(EffectId.CardRelease), Is.True, "\"7\" is not a name, so nothing at 7 is switched off");
            Assert.That(unknown, Is.EqualTo(new[] { "CardDrw", "7" }));

            Assert.That(EffectSwitches.WithOff(null, out List<string> none).IsOn(EffectId.CardDraw), Is.True);
            Assert.That(none, Is.Empty);
        }

        // ---- What an event waits for ----

        private static readonly EffectId[] CardMotion =
        {
            EffectId.CardDraw, EffectId.HandFan, EffectId.CardHover, EffectId.CardGrab, EffectId.ReceiverShow,
            EffectId.ThrowLineShow, EffectId.ReceiverSnap, EffectId.CardRelease, EffectId.CardToDiscard,
            EffectId.CardReturn, EffectId.RefusalShake, EffectId.HandDiscard, EffectId.UnpayableDim,
        };

        [Test]
        public void ADrawThatRanThePileOut_HoldsTheRebuildBeforeTheCards_AndKeepsOneStagger()
        {
            // battle_ui_ux_v2 §3.14 / §5.1 順 6 (#455): 260 + (n − 1) × 60 + 200 for the draw that met the end of the pile.
            var ev = new DepictionEvent { Kind = DepictionEventKind.TurnStart };
            ev.Cues.Add(new Cue { Kind = CueKind.DrawHand, Target = UnitSide.Player, Amount = 8, RebuildAfter = 2 });
            List<EffectStep> steps = EffectPlan.StepsOf(ev);
            Assert.That(steps.Select(s => (s.Id, s.Count)),
                Is.EqualTo(new[] { (EffectId.TurnBanner, 1), (EffectId.PileRebuild, 1), (EffectId.CardDraw, 8) }));
            float draw = steps.Skip(1).Sum(s => EffectPlan.WaitMs(s, EffectSwitches.AllOn()));
            Assert.That(draw, Is.EqualTo(260f + 7 * 60f + 200f), "§5.1: the worst draw is 880 ms");

            var plain = new DepictionEvent { Kind = DepictionEventKind.TurnStart };
            plain.Cues.Add(new Cue { Kind = CueKind.DrawHand, Target = UnitSide.Player, Amount = 6 });
            Assert.That(EffectPlan.StepsOf(plain).Select(s => s.Id), Has.No.Member(EffectId.PileRebuild));
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(7)]
        public void TheRebuildSlide_ComesWhenTheOldPileIsEmpty_AndTheDrawEndsOnThePlan(int rebuildAfter)
        {
            // battle_ui_ux_v2 §3.14: 「ドロー中に山札が 0 になったら … 200 ms を挟み、残りを続けて引く」.
            EffectSwitches on = EffectSwitches.AllOn();
            const int n = 8;
            float slide = EffectPlan.RebuildStartMs(rebuildAfter, on);
            Assert.That(slide, Is.EqualTo(rebuildAfter * 60f), "one stagger after the old pile's last card");
            for (int i = 0; i < rebuildAfter; i++)
                Assert.That(EffectPlan.DrawCardStartMs(i, rebuildAfter, on), Is.LessThan(slide), "card " + i + " comes off the old pile first");
            Assert.That(EffectPlan.DrawCardStartMs(rebuildAfter, rebuildAfter, on), Is.EqualTo(slide + 200f), "the new pile's first card waits for the slide");
            float lastEnds = EffectPlan.DrawCardStartMs(n - 1, rebuildAfter, on) + EffectCatalog.Of(EffectId.CardDraw).Ms;
            Assert.That(lastEnds, Is.EqualTo(260f + (n - 1) * 60f + 200f), "§5.1: the same wait as the slide at the head of the draw");
            Assert.That(lastEnds, Is.EqualTo(on.WaitMs(EffectId.PileRebuild) + on.WaitMs(EffectId.CardDraw, n)));
            Assert.That(EffectPlan.DrawCardStartMs(n - 1, Cue.Unchanged, on), Is.EqualTo((n - 1) * 60f), "a plain draw keeps one stagger");
        }

        [Test]
        public void AStanceCard_LeavesForTheExilePile_OnTheBeat600()
        {
            // battle_ui_ux_v2 §5.11: 順 1 (300) and 順 2 (300) come first; 順 4 flies 220 ms beside the beats.
            Assert.That(EffectCatalog.CardToExileDelayMs, Is.EqualTo(300f + 300f));
            Assert.That(EffectCatalog.Of(EffectId.CardToExile).Blocking, Is.False);
            Assert.That(new DepictionEvent().PlayedTo, Is.EqualTo(PileKind.Discard), "a card goes to the discard pile unless the core exiles it");
        }

        [Test]
        public void ThePileList_WritesOneRowPerKind_OrNone()
        {
            // battle_ui_ux_v2 §1.1 C4: 同種の残り「突き 2 / 3」.
            var rows = new List<PileCard>
            {
                new PileCard { Name = "突き", InPile = 2, InDeck = 3 },
                new PileCard { Name = "岩の構え", InPile = 1, InDeck = 1 },
            };
            Assert.That(PileList.BodyText(rows), Is.EqualTo("突き 2 / 3\n岩の構え 1 / 1"));
            Assert.That(PileList.BodyText(new List<PileCard>()), Is.EqualTo(PileList.Empty));
            Assert.That(new[] { PileKind.Draw, PileKind.Discard, PileKind.Exile }.Select(PileList.TabLabel), Is.EqualTo(new[] { "山札", "捨札", "除外" }));
            Assert.That(PileList.WidthShare, Is.EqualTo(0.30f), "the journal drawer's mirror: the left 30%");
        }

        [Test]
        public void TheFixedSlice_WaitsForTheDrawsTheReleasesAndTheDiscard()
        {
            List<DepictionEvent> events = TurnSliceScript.Build().Events;

            DepictionEvent turnStart = events[0];
            Assert.That(CardSteps(turnStart), Is.EqualTo(new[] { (EffectId.CardDraw, 5) }));

            foreach (DepictionEvent play in events.Where(e => e.Kind == DepictionEventKind.PlayCard))
            {
                Assert.That(CardSteps(play), Is.EqualTo(new[] { (EffectId.CardRelease, 1) }), "event " + play.Order);
                Assert.That(Steps(play).First(), Is.EqualTo((EffectId.CardRelease, 1)), "the release comes before the card's beats");
            }

            DepictionEvent turnEnd = events.Single(e => e.Kind == DepictionEventKind.TurnEnd);
            Assert.That(CardSteps(turnEnd), Is.EqualTo(new[] { (EffectId.HandDiscard, 2) }));
        }

        // ---- The waits the screen holds (#495) ----
        // DepictionPlayer.HoldBeat takes each event's steps from EffectPlan.StepsOf and holds the flow at each
        // one for EffectPlan.WaitMs. These pin that the event's length the tests sum is exactly those waits.
        // They do not prove the screen holds those beats: DepictionPlayer is Unity-only and not compiled here.
        // That the screen waits for each planned step in order is checked only at play time, by its LogError
        // on a drifted beat (PlayMode DemoFlowPlaybackTests / BattlePlaybackTests). Beats whose animation
        // length is written in code rather than read from the catalog (ShieldBlock, Reserve, the shield
        // badge's pop) can still run longer than the plan without that LogError.

        [Test]
        public void AnEventsLength_IsTheSumOfThePlansStepWaits()
        {
            var oneOff = EffectSwitches.AllOn();
            oneOff.Set(EffectId.HpDrain, false);
            foreach (EffectSwitches switches in new[] { EffectSwitches.AllOn(), oneOff, EffectSwitches.AllOff() })
            {
                foreach (DepictionEvent ev in TurnSliceScript.Build().Events)
                {
                    float held = EffectPlan.StepsOf(ev).Sum(step => EffectPlan.WaitMs(step, switches));
                    Assert.That(EffectPlan.BlockingMs(ev, switches), Is.EqualTo(held), "event " + ev.Order + " " + ev.Title);
                }
            }
        }

        [Test]
        public void AStepsWait_IsItsCatalogLengthForItsCount_AndNothingWhenSwitchedOff()
        {
            var on = EffectSwitches.AllOn();
            Assert.That(EffectPlan.WaitMs(new EffectStep(EffectId.CardDraw, 5), on), Is.EqualTo(260f + 4 * 60f));
            Assert.That(EffectPlan.WaitMs(new EffectStep(EffectId.StaminaRecover, 3), on), Is.EqualTo(3 * 80f));
            Assert.That(EffectPlan.WaitMs(new EffectStep(EffectId.GuardBlock, 1), on), Is.EqualTo(320f));
            Assert.That(EffectPlan.WaitMs(new EffectStep(EffectId.CardDraw, 5), EffectSwitches.AllOff()), Is.EqualTo(0f));
        }

        [Test]
        public void AMoveWithoutAFloor_Waits320_AndFlipsTheTagFor150()
        {
            Assert.That(EffectCatalog.Of(EffectId.RangeSwitch).Ms, Is.EqualTo(320f));
            Assert.That(EffectCatalog.Of(EffectId.RangeTagFlip).Ms, Is.EqualTo(150f));
            Assert.That(EffectCatalog.Of(EffectId.RangeTagFlip).Blocking, Is.True);

            // The fixed slice draws no floor: the figure moves to its slot, then the tag flips.
            var slot = new DepictionEvent { Kind = DepictionEventKind.PlayCard, After = new DepictionFrame() };
            slot.Cues.Add(new Cue { Kind = CueKind.RangeSwitch, Target = UnitSide.Player, RangeAfter = RangeSide.Far, RangeGlyphAfter = "遠" });
            Assert.That(EffectPlan.MovesOnFloor(slot.Cues[0]), Is.False);
            Assert.That(Steps(slot), Is.EqualTo(new[] { (EffectId.CardRelease, 1), (EffectId.RangeSwitch, 1), (EffectId.RangeTagFlip, 1) }));
            Assert.That(EffectPlan.BlockingMs(slot, EffectSwitches.AllOn()), Is.EqualTo(160f + 320f + 150f));
        }

        [Test]
        public void AMoveOnTheFloor_Walks160ACell_ThenCountsTheGapAgainFor150()
        {
            // battle_ui_ux_v2 §5.7 順 1〜2 / §5.0 (#586): 160 × cells, then 150 to count the gap number again.
            Assert.That(EffectCatalog.Of(EffectId.FloorMove).TotalMs(1), Is.EqualTo(160f));
            Assert.That(EffectCatalog.Of(EffectId.FloorMove).TotalMs(2), Is.EqualTo(320f));
            Assert.That(EffectCatalog.Of(EffectId.GapRecount).Ms, Is.EqualTo(150f));
            Assert.That(EffectCatalog.Of(EffectId.FloorMove).Blocking, Is.True);
            Assert.That(EffectCatalog.Of(EffectId.GapRecount).Blocking, Is.True);

            var on = EffectSwitches.AllOn();
            foreach ((int cells, float ms) in new[] { (1, 310f), (2, 470f) })
            {
                // The core's battles draw the floor: the figure walks to its cell and there is no tag to flip.
                var move = new DepictionEvent { Kind = DepictionEventKind.EnemyAction, After = new DepictionFrame() };
                move.Cues.Add(new Cue { Kind = CueKind.RangeSwitch, Target = UnitSide.Player, PlayerCellAfter = 2, EnemyCellAfter = 5, GapAfter = 2, CellsMoved = cells });
                Assert.That(EffectPlan.MovesOnFloor(move.Cues[0]), Is.True);
                Assert.That(Steps(move), Is.EqualTo(new[] { (EffectId.FloorMove, cells), (EffectId.GapRecount, 1) }));
                Assert.That(EffectPlan.StepsOf(move).Sum(step => EffectPlan.WaitMs(step, on)), Is.EqualTo(ms), cells + " cells");
            }
        }

        [Test]
        public void AMoveOnTheFloor_ThatDoesNotSayHowFar_Stops()
        {
            var move = new DepictionEvent { Kind = DepictionEventKind.EnemyAction, After = new DepictionFrame() };
            move.Cues.Add(new Cue { Kind = CueKind.RangeSwitch, Target = UnitSide.Player, PlayerCellAfter = 2, EnemyCellAfter = 5, GapAfter = 2 });
            Assert.Throws<ArgumentOutOfRangeException>(() => EffectPlan.StepsOf(move));
        }

        [Test]
        public void NoCardMoving_IsNoBeat()
        {
            var ev = new DepictionEvent { Kind = DepictionEventKind.TurnEnd, After = new DepictionFrame() };
            ev.Cues.Add(new Cue { Kind = CueKind.DrawHand, Target = UnitSide.Player, Amount = 0 });
            ev.Cues.Add(new Cue { Kind = CueKind.DiscardHand, Target = UnitSide.Player, Amount = 0 });
            Assert.That(Steps(ev), Is.EqualTo(new[] { (EffectId.EnemyTurnBanner, 1) }), "the screen flies no card and waits for none");
        }

        [Test]
        public void ASlashWithNoSource_IsSwungByTheSideOppositeItsTarget_OnScreenAndInThePlan()
        {
            var atEnemy = new Cue { Kind = CueKind.Slash, Target = UnitSide.Enemy, Intensity = 2, HpAfter = 40 };
            var atPlayer = new Cue { Kind = CueKind.Slash, Target = UnitSide.Player, Intensity = 2 };
            Assert.That(EffectPlan.AttackerOf(atEnemy), Is.EqualTo(UnitSide.Player));
            Assert.That(EffectPlan.AttackerOf(atPlayer), Is.EqualTo(UnitSide.Enemy));
            Assert.That(EffectPlan.AttackerOf(new Cue { Kind = CueKind.Slash, Source = UnitSide.Enemy, Target = UnitSide.Enemy }), Is.EqualTo(UnitSide.Enemy), "a named Source wins");

            var ev = new DepictionEvent { Kind = DepictionEventKind.EnemyAction, After = new DepictionFrame() };
            ev.Cues.Add(atEnemy);
            Assert.That(Steps(ev), Is.EqualTo(new[]
            {
                (EffectId.AttackLunge, 1), (EffectId.StrikeShape, 1), (EffectId.HitStop, 1), (EffectId.HpDrain, 1), (EffectId.AttackReturn, 1),
            }));
        }

        [Test]
        public void EveryCueKind_HasAPlan()
        {
            foreach (CueKind kind in Enum.GetValues(typeof(CueKind)))
            {
                var ev = new DepictionEvent { Kind = DepictionEventKind.EnemyAction, After = new DepictionFrame() };
                ev.Cues.Add(new Cue { Kind = kind, Source = UnitSide.Enemy, Target = UnitSide.Player, Amount = 1, Intensity = 1 });
                Assert.DoesNotThrow(() => EffectPlan.StepsOf(ev), kind.ToString());
            }
        }

        [Test]
        public void SwitchingAnyEffectOff_NeverLengthensAnEvent_AndAllOff_TheTurnStillPlaysToTheEnd()
        {
            List<DepictionEvent> events = TurnSliceScript.Build().Events;
            Assert.That(events.Count, Is.GreaterThan(3));

            foreach (EffectId id in Enum.GetValues(typeof(EffectId)))
            {
                var oneOff = EffectSwitches.AllOn();
                oneOff.Set(id, false);
                foreach (DepictionEvent ev in events)
                {
                    float on = EffectPlan.BlockingMs(ev, EffectSwitches.AllOn());
                    float off = EffectPlan.BlockingMs(ev, oneOff);
                    Assert.That(off, Is.InRange(0f, on), id + " off, event " + ev.Order);
                }
            }

            // With every effect off nothing is waited for, and every event still hands over its settled
            // frame: that frame is what the View shows, so the turn goes on to its end.
            var allOff = EffectSwitches.AllOff();
            foreach (DepictionEvent ev in events)
            {
                Assert.That(EffectPlan.BlockingMs(ev, allOff), Is.EqualTo(0f), "event " + ev.Order);
                Assert.That(ev.After, Is.Not.Null, "event " + ev.Order);
            }
        }

        [Test]
        public void EveryEvent_FitsInsideTheTwoSecondBudget()
        {
            var all = EffectSwitches.AllOn();
            foreach (DepictionEvent ev in TurnSliceScript.Build().Events)
            {
                Assert.That(EffectPlan.BlockingMs(ev, all), Is.LessThan(2000f), "event " + ev.Order + " " + ev.Title);
            }
        }

        // ---- The trace #78 reads ----

        [Test]
        public void TheTrace_TimesWhatPlayed_AndMarksWhatWasSwitchedOff()
        {
            double now = 10.0;
            var trace = new EffectTrace(() => now);

            int draw = trace.Begin(EffectId.CardDraw, eventOrder: 1, count: 5);
            now += 0.52;
            trace.End(draw);
            trace.End(draw); // a second End changes nothing
            trace.Skip(EffectId.CardRelease, eventOrder: 2);
            int open = trace.Begin(EffectId.HandFan, eventOrder: 2);

            Assert.That(trace.Entries, Has.Count.EqualTo(3));
            EffectTrace.Entry first = trace.Entries[0];
            Assert.That(first.Id, Is.EqualTo(EffectId.CardDraw));
            Assert.That(first.EventOrder, Is.EqualTo(1));
            Assert.That(first.Seconds, Is.EqualTo(0.52).Within(1e-9));
            Assert.That(first.NominalMs, Is.EqualTo(500f));
            Assert.That(first.Finished, Is.True);
            Assert.That(trace.Entries[1].Skipped, Is.True);
            Assert.That(trace.Entries[1].Seconds, Is.EqualTo(0.0));
            Assert.That(trace.Entries[open].Finished, Is.False, "still playing");

            string[] lines = trace.ToCsv().TrimEnd('\n').Split('\n');
            Assert.That(lines[0], Is.EqualTo("effect,event,start_s,measured_ms,nominal_ms,skipped"));
            Assert.That(lines[1], Is.EqualTo("CardDraw,1,10.000,520,500,0"));
            Assert.That(lines[2], Does.StartWith("CardRelease,2,").And.EndsWith(",1"));
            Assert.Throws<ArgumentOutOfRangeException>(() => trace.End(9));
        }

        [Test]
        public void TheTrace_RecordsTheNominalLength_AtTheSpeedInForce()
        {
            var trace = new EffectTrace(() => 0.0);
            Assert.That(trace.Speed, Is.EqualTo(BattleSpeed.Default));
            trace.Begin(EffectId.CardDraw, eventOrder: 1, count: 5);
            trace.Speed = BattleSpeedStep.Slow;
            trace.Begin(EffectId.CardDraw, eventOrder: 1, count: 5);
            trace.Speed = BattleSpeedStep.Fast;
            trace.Begin(EffectId.CardDraw, eventOrder: 1, count: 5);

            Assert.That(trace.Entries[0].NominalMs, Is.EqualTo(500f), "1.25 times is the written length");
            Assert.That(trace.Entries[1].NominalMs, Is.EqualTo(625f).Within(0.01), "1.0 times: 500 x 1.25");
            Assert.That(trace.Entries[2].NominalMs, Is.EqualTo(416.67f).Within(0.01), "1.5 times: 500 x 1.25 / 1.5");
        }

        // ---- Helpers ----

        private static (EffectId, int)[] Steps(DepictionEvent ev) =>
            EffectPlan.StepsOf(ev).Select(s => (s.Id, s.Count)).ToArray();

        private static (EffectId, int)[] CardSteps(DepictionEvent ev) =>
            Steps(ev).Where(s => CardMotion.Contains(s.Item1)).ToArray();
    }
}
