// Core event stream → depiction script. Runs under `dotnet test` (Depiction.Bridge.Tests.csproj)
// and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class CoreScriptWriterTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static List<CueKind> Kinds(DepictionEvent ev)
        {
            return ev.Cues.Select(c => c.Kind).ToList();
        }

        /// <summary>A battle dealt in deck order at the given gap, with the writer already past the opening.</summary>
        private static (BattleState State, CoreScriptWriter Writer) Begin(int gap, params CardDef[] kinds)
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(kinds, 1),
                BattleSetup.SliceFieldCells, StartGap: gap);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(setup.Enemy);
            writer.Opening(state);
            return (state, writer);
        }

        private static readonly CardDef[] FiveCards =
        {
            CardCatalog.KesaCut, CardCatalog.Feint, CardCatalog.Brace, CardCatalog.BodyCheck, CardCatalog.StepOutGuard,
        };

        private static string InHand(BattleState state, string cardId)
        {
            return state.Hand.First(c => c.Def.Id == cardId).InstanceId;
        }

        // ---- opening ------------------------------------------------------------------------

        [Test]
        public void Opening_ShowsBothUnits_AndKeepsTheFirstOmenHidden()
        {
            BattleState state = TurnLoop.Start(BattleSetup.Slice(), new SeededRng(1)).State;
            DepictionFrame frame = new CoreScriptWriter(state.EnemyDef).Opening(state);

            Assert.That(frame.Player.Hp, Is.EqualTo(50));
            Assert.That(frame.Player.ShowStamina, Is.True);
            Assert.That(frame.Player.Stamina, Is.EqualTo(10));
            Assert.That(frame.Player.HasRange, Is.True);
            Assert.That(frame.Player.RangeGlyph, Is.EqualTo("3"), "the gap N on the player's tag (§7.2), START_GAP 3");
            Assert.That(frame.Player.Range, Is.EqualTo(RangeSide.Far), "gap 2 and up stand in the far slot until #163");
            Assert.That(frame.Enemy.Hp, Is.EqualTo(60));
            Assert.That(frame.Enemy.HasRange, Is.False, "one tag says it all");
            Assert.That(frame.Enemy.ShowStamina, Is.False);
            Assert.That(frame.Omen.Visible, Is.False, "step 5 of turn 1 is what shows it");
            Assert.That(frame.Hand, Is.Empty);
        }

        // ---- turn start ---------------------------------------------------------------------

        [Test]
        public void TurnStart_IsOneEvent_FiveDrawsBecomeOneBeat_AndTheOmenAppears()
        {
            var (state, writer) = Begin(0, FiveCards);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);

            List<DepictionEvent> events = writer.Write(begin.Events, begin.State);

            Assert.That(events, Has.Count.EqualTo(1));
            DepictionEvent ev = events[0];
            Assert.That(ev.Kind, Is.EqualTo(DepictionEventKind.TurnStart));
            Assert.That(ev.Order, Is.EqualTo(1));
            // Guard was already 0 and stamina already full: neither gets a beat.
            Assert.That(Kinds(ev), Is.EqualTo(new[] { CueKind.DrawHand, CueKind.OmenShow }));
            Assert.That(ev.Cues[0].Amount, Is.EqualTo(5));

            Assert.That(ev.After.Hand.Select(c => c.Id), Is.EqualTo(begin.State.Hand.Select(c => c.InstanceId)));
            Assert.That(ev.After.Omen.Visible, Is.True);
            Assert.That(ev.After.Omen.KindLabel, Is.EqualTo("攻撃"));
            Assert.That(ev.After.Omen.SideGlyph, Is.EqualTo("0"), "the shove's reach, where the side glyph was (#163)");
            Assert.That(ev.After.Omen.ValueText, Is.EqualTo("5"));
            Assert.That(ev.After.Corner.Turn, Is.EqualTo(1));
            Assert.That(ev.After.StanceHint, Is.EqualTo("+3"));
        }

        [Test]
        public void TheHandFaces_CarryTheCanonText_AndTheCoreDecidesTheLamp()
        {
            var (state, writer) = Begin(0, FiveCards);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            DepictionFrame frame = writer.Write(begin.Events, begin.State)[0].After;

            CardFace kesa = frame.Hand.First(c => c.Name == "袈裟斬り");
            Assert.That(kesa.Cost, Is.EqualTo(2));
            Assert.That(kesa.Kind, Is.EqualTo(CardKind.Attack));
            Assert.That(kesa.Aim, Is.EqualTo(CardAim.Single));
            Assert.That(kesa.TypeLabel, Is.EqualTo("攻撃・敵単体"));
            Assert.That(kesa.ValueText, Is.EqualTo("13"));
            Assert.That(kesa.TraitText, Is.EqualTo("間合い0 +5"));
            Assert.That(kesa.TraitLit, Is.True, "the player stands adjacent");
            Assert.That(kesa.RequiredRange, Is.Null);
            Assert.That(kesa.RequiredRangeGlyph, Is.EqualTo("0〜1"), "the reach, as printed (§2.4)");

            CardFace brace = frame.Hand.First(c => c.Name == "呼吸を整える");
            Assert.That(brace.Aim, Is.EqualTo(CardAim.Self));
            Assert.That(brace.Affects, Is.EqualTo(UnitSide.Player));
            Assert.That(brace.TypeLabel, Is.EqualTo("防御・自分"));
            Assert.That(brace.TraitText, Is.EqualTo("残6 回復+1"));
            Assert.That(brace.TraitLit, Is.True, "10 − 2 = 8 left");
            Assert.That(brace.RequiredRangeGlyph, Is.EqualTo(""), "a self card reads no reach");

            CardFace bodyCheck = frame.Hand.First(c => c.Name == "体当たり");
            Assert.That(bodyCheck.Description, Is.EqualTo("敵に 4 ダメージ。鈍足を 2 付与する。"), "§5: 「<語> を n 付与する」");
            Assert.That(bodyCheck.TraitText, Is.EqualTo("間合い0 重撃"));
            Assert.That(bodyCheck.RequiredRangeGlyph, Is.EqualTo("0〜1"), "#179: v4.3 reaches 0〜1");

            CardFace feint = frame.Hand.First(c => c.Name == "牽制");
            Assert.That(feint.Description, Is.EqualTo("敵に 8 ダメージ。後ろへ 1 動く。"));
        }

        [Test]
        public void TheLamp_GoesOut_WhenThePlayerStepsBack()
        {
            var (state, writer) = Begin(0, FiveCards);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);

            StepResult play = TurnLoop.PlayCard(begin.State, InHand(begin.State, "feint"), NoShuffle);
            DepictionFrame after = writer.Write(play.Events, play.State)[0].After;

            Assert.That(after.Player.RangeGlyph, Is.EqualTo("1"));
            Assert.That(after.Hand.First(c => c.Name == "袈裟斬り").TraitLit, Is.False);
        }

        // ---- playing a card -----------------------------------------------------------------

        [Test]
        public void AnUnguardedHit_IsOneSlash_WithTheSettledNumbers()
        {
            var (state, writer) = Begin(0, FiveCards);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);

            string kesa = InHand(begin.State, "kesa_cut");
            StepResult play = TurnLoop.PlayCard(begin.State, kesa, NoShuffle);
            DepictionEvent ev = writer.Write(play.Events, play.State).Single();

            Assert.That(ev.Kind, Is.EqualTo(DepictionEventKind.PlayCard));
            Assert.That(ev.WaitsForDrag, Is.True);
            Assert.That(ev.CardId, Is.EqualTo(kesa));
            Assert.That(ev.Aim, Is.EqualTo(CardAim.Single));
            Assert.That(ev.Title, Is.EqualTo("袈裟斬り"));
            Assert.That(ev.PreviewText, Is.EqualTo("18"));
            // §2.2: cost, then the trait, then the attack face.
            Assert.That(Kinds(ev), Is.EqualTo(new[] { CueKind.StaminaChange, CueKind.TraitFire, CueKind.Slash }));

            Cue stamina = ev.Cues[0];
            Assert.That(stamina.Amount, Is.EqualTo(-2));
            Assert.That(stamina.StaminaAfter, Is.EqualTo(8));
            Assert.That(stamina.StaminaMax, Is.EqualTo(10));

            Assert.That(ev.Cues[1].Text, Is.EqualTo("間合い0 +5"));

            Cue slash = ev.Cues[2];
            Assert.That(slash.Source, Is.EqualTo(UnitSide.Player), "the attacker is always named (#29)");
            Assert.That(slash.Target, Is.EqualTo(UnitSide.Enemy));
            Assert.That(slash.Amount, Is.EqualTo(18));
            Assert.That(slash.Intensity, Is.EqualTo(3));
            Assert.That(slash.HpAfter, Is.EqualTo(42));

            Assert.That(ev.After.Enemy.Hp, Is.EqualTo(42));
            Assert.That(ev.After.Player.Stamina, Is.EqualTo(8));
            Assert.That(ev.After.Hand, Has.Count.EqualTo(4));
        }

        [Test]
        public void AttackThenMoveThenGuard_PlayInThatOrder()
        {
            var (state, writer) = Begin(0, FiveCards);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);

            StepResult play = TurnLoop.PlayCard(begin.State, InHand(begin.State, "feint"), NoShuffle);
            DepictionEvent ev = writer.Write(play.Events, play.State).Single();

            Assert.That(Kinds(ev), Is.EqualTo(new[]
            {
                CueKind.StaminaChange, CueKind.TraitFire, CueKind.Slash, CueKind.RangeSwitch, CueKind.GuardGain,
            }));
            Cue move = ev.Cues[3];
            Assert.That(move.Target, Is.EqualTo(UnitSide.Player));
            Assert.That(move.RangeAfter, Is.EqualTo(RangeSide.Near), "gap 1 still stands in the near slot");
            Assert.That(move.RangeGlyphAfter, Is.EqualTo("1"));
            Cue guard = ev.Cues[4];
            Assert.That(guard.Amount, Is.EqualTo(3));
            Assert.That(guard.GuardAfter, Is.EqualTo(3));
        }

        [Test]
        public void AStatus_GetsItsOwnBeat_AndAChipOnTheFrame()
        {
            var (state, writer) = Begin(0, FiveCards);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);

            StepResult play = TurnLoop.PlayCard(begin.State, InHand(begin.State, "body_check"), NoShuffle);
            DepictionEvent ev = writer.Write(play.Events, play.State).Single();

            Cue status = ev.Cues.Single(c => c.Kind == CueKind.StatusChange);
            Assert.That(status.Target, Is.EqualTo(UnitSide.Enemy));
            Assert.That(status.Text, Is.EqualTo("鈍足"));
            Assert.That(status.Amount, Is.EqualTo(2));
            Assert.That(status.StacksAfter, Is.EqualTo(2));

            StatusChip chip = ev.After.Enemy.Statuses.Single();
            Assert.That(chip.Label, Is.EqualTo("鈍足"));
            Assert.That(chip.Stacks, Is.EqualTo(2));
        }

        [Test]
        public void ASelfCard_IsThrownAboveTheLine_AndPreviewsItsGuard()
        {
            var source = new CoreBattleSource(
                new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(FiveCards, 1), BattleSetup.SliceFieldCells), seed: 1);
            source.AdvanceAuto();
            string brace = source.Frame.Hand.First(c => c.Name == "呼吸を整える").Id;

            Assert.That(source.PreviewFor(brace), Is.EqualTo("9"));
            DepictionEvent played;
            Assert.That(source.TryPlay(brace, DropZone.Receiver, out played), Is.EqualTo(PlayVerdict.WrongZone));
            Assert.That(source.TryPlay(brace, DropZone.AboveThrowLine, out played), Is.EqualTo(PlayVerdict.Accepted));
            Assert.That(played.Aim, Is.EqualTo(CardAim.Self));
        }

        // ---- ending the turn ----------------------------------------------------------------

        [Test]
        public void EndTurn_IsThreeEvents_TurnEnd_EnemyAction_NextOmen_PlusTheWall()
        {
            var (state, writer) = Begin(0, FiveCards);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);

            StepResult end = TurnLoop.EndTurn(begin.State, NoShuffle);
            List<DepictionEvent> events = writer.Write(end.Events, end.State);

            Assert.That(events.Select(e => e.Kind), Is.EqualTo(new[]
            {
                DepictionEventKind.TurnEnd, DepictionEventKind.EnemyAction, DepictionEventKind.EnemyAction, DepictionEventKind.NextOmen,
            }));
            Assert.That(events.Select(e => e.Order), Is.EqualTo(new[] { 2, 3, 4, 5 }));

            // Turn end: 構え (10 stamina left), then the hand goes.
            DepictionEvent turnEnd = events[0];
            Assert.That(Kinds(turnEnd), Is.EqualTo(new[] { CueKind.StanceCue, CueKind.DiscardHand }));
            Assert.That(turnEnd.Cues[0].Text, Is.EqualTo("+3"));
            Assert.That(turnEnd.Cues[0].GuardAfter, Is.EqualTo(3));
            Assert.That(turnEnd.Cues[1].Amount, Is.EqualTo(5));
            Assert.That(turnEnd.After.Hand, Is.Empty);
            Assert.That(turnEnd.After.Player.Guard, Is.EqualTo(3));
            Assert.That(turnEnd.After.StanceHint, Is.EqualTo(""));

            // The shove: 5 into Guard 3 → 3 blocked, 2 through; the two-cell push moves the player one
            // (cell 1), and the wall takes 3 more in an event of its own (§7.3).
            DepictionEvent shove = events[1];
            Assert.That(shove.Title, Is.EqualTo("石突きの押し込み"));
            Assert.That(Kinds(shove), Is.EqualTo(new[]
            {
                CueKind.EnemyWindup, CueKind.Slash, CueKind.GuardBlock, CueKind.Hit, CueKind.RangeSwitch,
            }));
            Assert.That(shove.Cues[1].Source, Is.EqualTo(UnitSide.Enemy));
            Assert.That(shove.Cues[1].HpAfter, Is.EqualTo(Cue.Unchanged), "an enemy slash is the swing alone");
            Assert.That(shove.Cues[2].Amount, Is.EqualTo(3));
            Assert.That(shove.Cues[2].GuardAfter, Is.EqualTo(0));
            Assert.That(shove.Cues[3].Amount, Is.EqualTo(2));
            Assert.That(shove.Cues[3].HpAfter, Is.EqualTo(48));
            Assert.That(shove.Cues[4].Target, Is.EqualTo(UnitSide.Player));
            Assert.That(shove.Cues[4].RangeGlyphAfter, Is.EqualTo("1"));
            Assert.That(shove.After.Omen.Visible, Is.False, "the omen is spent");
            Assert.That(shove.After.Enemy.Guard, Is.EqualTo(0), "its 構え has not played yet");

            DepictionEvent wall = events[2];
            Assert.That(wall.Title, Is.EqualTo("壁に当たる"));
            Assert.That(Kinds(wall), Is.EqualTo(new[] { CueKind.Hit }));
            Assert.That(wall.Cues[0].Target, Is.EqualTo(UnitSide.Player));
            Assert.That(wall.Cues[0].Amount, Is.EqualTo(3));
            Assert.That(wall.Cues[0].HpAfter, Is.EqualTo(45));
            Assert.That(wall.After.Player.Hp, Is.EqualTo(45));

            // Next omen: the enemy's 構え goes up, then the sweep is announced from the 1〜2 branch.
            DepictionEvent next = events[3];
            Assert.That(Kinds(next), Is.EqualTo(new[] { CueKind.GuardGain, CueKind.OmenShow }));
            Assert.That(next.Cues[0].Target, Is.EqualTo(UnitSide.Enemy));
            Assert.That(next.Cues[0].GuardAfter, Is.EqualTo(3));
            Assert.That(next.After.Enemy.Guard, Is.EqualTo(3));
            Assert.That(next.After.Omen.KindLabel + "・" + next.After.Omen.SideGlyph, Is.EqualTo("攻撃・1〜2"));
            Assert.That(next.After.Omen.ValueText, Is.EqualTo("8"));
        }

        [Test]
        public void ASweepThatFindsThePlayerAtGapOne_StrikesItsBonusOff()
        {
            var kinds = new[] { CardCatalog.StepInGuard, CardCatalog.KesaCut, CardCatalog.Brace, CardCatalog.Feint, CardCatalog.Thrust };
            var (state, writer) = Begin(2, kinds);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);
            StepResult play = TurnLoop.PlayCard(begin.State, InHand(begin.State, "step_in_guard"), NoShuffle);
            writer.Write(play.Events, play.State);

            StepResult end = TurnLoop.EndTurn(play.State, NoShuffle);
            DepictionEvent sweep = writer.Write(end.Events, end.State)[1];

            Assert.That(sweep.Title, Is.EqualTo("薙ぎ払い"));
            Cue miss = sweep.Cues.Single(c => c.Kind == CueKind.SideBonusMiss);
            Assert.That(miss.Text, Is.EqualTo("+3"));
            Assert.That(sweep.Cues.Any(c => c.Kind == CueKind.TraitFire), Is.False);
            Assert.That(sweep.Cues.Single(c => c.Kind == CueKind.Slash).Amount, Is.EqualTo(8));
        }

        [Test]
        public void ASweepThatWhiffs_StrikesItsWholeNumberOff_AndHitsNobody()
        {
            // Declared at gap 2; the boar rush closes to gap 0, outside the sweep's 1〜2 (§6).
            var kinds = new[] { CardCatalog.BoarRush, CardCatalog.KesaCut, CardCatalog.Brace, CardCatalog.Feint, CardCatalog.Thrust };
            var (state, writer) = Begin(2, kinds);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);
            StepResult play = TurnLoop.PlayCard(begin.State, InHand(begin.State, "boar_rush"), NoShuffle);
            DepictionFrame afterRush = writer.Write(play.Events, play.State)[0].After;
            Assert.That(afterRush.Player.RangeGlyph, Is.EqualTo("0"));
            Assert.That(afterRush.Player.Range, Is.EqualTo(RangeSide.Near));

            StepResult end = TurnLoop.EndTurn(play.State, NoShuffle);
            List<DepictionEvent> events = writer.Write(end.Events, end.State);
            DepictionEvent sweep = events[1];

            Assert.That(sweep.Title, Is.EqualTo("薙ぎ払い"));
            Assert.That(Kinds(sweep), Is.EqualTo(new[] { CueKind.EnemyWindup, CueKind.SideBonusMiss }), "one strike-off, not two");
            Assert.That(sweep.Cues[1].Text, Is.EqualTo("空振り"));
            Assert.That(sweep.Cues[1].Amount, Is.EqualTo(8));
            Assert.That(sweep.After.Player.Hp, Is.EqualTo(50));
            Assert.That(events[2].After.Omen.KindLabel + "・" + events[2].After.Omen.SideGlyph, Is.EqualTo("攻撃・0"), "the adjacent branch now");
        }

        [Test]
        public void TheEnemysStep_MovesThePlayersTag_UntilTheFloorHasCells()
        {
            // Gap 3: the polearm steps in. The screen has no enemy slot yet (#163), so the one thing
            // that changes is N on the player's tag — and the slot it maps to.
            var (state, writer) = Begin(3, FiveCards);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);

            StepResult end = TurnLoop.EndTurn(begin.State, NoShuffle);
            DepictionEvent step = writer.Write(end.Events, end.State)[1];

            Assert.That(step.Title, Is.EqualTo("踏み込み"));
            Assert.That(Kinds(step), Is.EqualTo(new[] { CueKind.EnemyWindup, CueKind.RangeSwitch, CueKind.GuardGain }));
            Assert.That(step.Cues[1].Target, Is.EqualTo(UnitSide.Player));
            Assert.That(step.Cues[1].RangeGlyphAfter, Is.EqualTo("2"));
            Assert.That(step.Cues[1].RangeAfter, Is.EqualTo(RangeSide.Far));
            Assert.That(step.After.Player.RangeGlyph, Is.EqualTo("2"));
        }

        [Test]
        public void TheSecondTurnStart_DoesNotPopTheOmenTwice_AndClearsTheGuard()
        {
            var (state, writer) = Begin(1, PrototypeDeck.Kinds.ToArray());
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);
            // Spend 7 so that turn 2 has a real recovery to show.
            BattleState s = begin.State;
            foreach (string id in new[] { "thrust", "kesa_cut", "brace" })
            {
                StepResult play = TurnLoop.PlayCard(s, InHand(s, id), NoShuffle);
                writer.Write(play.Events, play.State);
                s = play.State;
            }
            StepResult end = TurnLoop.EndTurn(s, NoShuffle);
            writer.Write(end.Events, end.State);

            StepResult second = TurnLoop.BeginPlayerTurn(end.State, NoShuffle);
            DepictionEvent ev = writer.Write(second.Events, second.State).Single();

            Assert.That(Kinds(ev), Is.EqualTo(new[] { CueKind.GuardReset, CueKind.StaminaChange, CueKind.DrawHand }));
            Assert.That(ev.Cues[1].Amount, Is.EqualTo(3));
            Assert.That(ev.Cues[1].StaminaAfter, Is.EqualTo(6));
            Assert.That(ev.After.Omen.Visible, Is.True);
            Assert.That(ev.After.Corner.Turn, Is.EqualTo(2));
        }

        // ---- a hand-written stream ----------------------------------------------------------

        [Test]
        public void TheWriter_OnlyCopiesSettledValues_EvenFromAStreamNoBattleProduced()
        {
            // Numbers no rule would produce: if the writer recomputed anything, these would not survive.
            BattleState state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle).State;
            var writer = new CoreScriptWriter(state.EnemyDef);
            writer.Opening(state);

            var stream = new List<BattleEvent>
            {
                new TurnStarted(Actor.Player, 7),
                new GuardCleared(Actor.Player, 4),
                new StaminaRecovered(Actor.Player, 1, 2, 10),
                new OmenSet(Actor.Enemy, state.Omen, false),
            };
            DepictionEvent ev = writer.Write(stream, state).Single();

            Assert.That(Kinds(ev), Is.EqualTo(new[] { CueKind.GuardReset, CueKind.StaminaChange, CueKind.OmenShow }));
            Assert.That(ev.Cues[1].Amount, Is.EqualTo(1));
            Assert.That(ev.After.Player.Stamina, Is.EqualTo(2));
            Assert.That(ev.After.Corner.Turn, Is.EqualTo(7));

            var hit = new List<BattleEvent>
            {
                new GuardCleared(Actor.Enemy, 0),
                new ActionExecuted(Actor.Enemy, Enemies.PolearmWarped.Actions["reach_thrust"], 0),
                new DamageDealt(Actor.Enemy, Actor.Player, 99, 90, 9, 1, 41),
                new DefeatChecked(Actor.Enemy, GameResult.Ongoing),
            };
            DepictionEvent enemy = writer.Write(hit, state).Single();

            Assert.That(enemy.Title, Is.EqualTo("穂先の突き"));
            Assert.That(enemy.Cues.Single(c => c.Kind == CueKind.GuardBlock).Amount, Is.EqualTo(90));
            Assert.That(enemy.Cues.Single(c => c.Kind == CueKind.Hit).HpAfter, Is.EqualTo(41));
            Assert.That(enemy.After.Player.Hp, Is.EqualTo(41));
            Assert.That(enemy.After.Player.Guard, Is.EqualTo(1));
        }

        [Test]
        public void ARest_IsAnEnemyActionWithNoBeats()
        {
            BattleState state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle).State;
            var writer = new CoreScriptWriter(state.EnemyDef);
            writer.Opening(state);

            var stream = new List<BattleEvent>
            {
                new GuardCleared(Actor.Enemy, 0),
                new StaminaRecovered(Actor.Enemy, 1, 1, 10),
                new Rested(Actor.Enemy, state.Omen),
                new ReserveChecked(Actor.Enemy, 1, 0, 0),
                new DefeatChecked(Actor.Enemy, GameResult.Ongoing),
            };
            DepictionEvent ev = writer.Write(stream, state).Single();

            Assert.That(ev.Kind, Is.EqualTo(DepictionEventKind.EnemyAction));
            Assert.That(ev.Title, Is.EqualTo("敵は動けない"));
            Assert.That(ev.Cues, Is.Empty, "the enemy's stamina is not on screen, so its recovery has no beat");
        }
    }
}
