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
            Assert.That(frame.Floor.Gap, Is.EqualTo(3), "#577: the figures stand on the floor's cells, N apart");
            Assert.That(frame.Enemy.Hp, Is.EqualTo(60));
            Assert.That(frame.Enemy.HasRange, Is.False, "one tag says it all");
            Assert.That(frame.Enemy.ShowStamina, Is.True, "battle-visual-v1 §4.5 (#455): on the enemy's number panel");
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
            Assert.That(ev.After.Omen.Icon, Is.EqualTo(OmenIcon.Attack));
            Assert.That(ev.After.Omen.SideGlyph, Is.EqualTo("0"), "the shove's reach, where the side glyph was (#163)");
            // #248: the shove as it would land if the turn ended now — 構え's Guard 3 takes 無防備's +3
            // away, so 6, then the two-cell push finds one cell left and puts 3 of wall on top: 9.
            Assert.That(ev.After.Omen.ValueText, Is.EqualTo("9"));
            Assert.That(ev.After.Corner.Turn, Is.EqualTo(1));
            Assert.That(ev.After.StanceHint, Is.EqualTo("+3"));
        }

        [Test]
        public void TurnStart_OnTheSlice_DealsAFullHandOfSix_AsOneBeat()
        {
            // battle_core_v4 §8 (#351): six cards a turn. The slice's 20-card deck has enough for a full hand.
            var rng = new SeededRng(1);
            BattleState state = TurnLoop.Start(BattleSetup.Slice(), rng).State;
            var writer = new CoreScriptWriter(state.EnemyDef);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, rng);

            DepictionEvent ev = writer.Write(begin.Events, begin.State).Single();

            Assert.That(ev.Cues.Single(c => c.Kind == CueKind.DrawHand).Amount, Is.EqualTo(6));
            Assert.That(ev.After.Hand.Count, Is.EqualTo(Constants.HandDraw));
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
            Assert.That(kesa.ValueText, Is.EqualTo("15"));
            Assert.That(kesa.TraitText, Is.EqualTo("間合い0 +5"));
            Assert.That(kesa.TraitLit, Is.True, "the player stands adjacent");
            Assert.That(kesa.RequiredRangeGlyph, Is.EqualTo("0〜1"), "the reach, as printed (§2.4)");

            CardFace brace = frame.Hand.First(c => c.Name == "呼吸を整える");
            Assert.That(brace.Aim, Is.EqualTo(CardAim.Self));
            Assert.That(brace.Affects, Is.EqualTo(UnitSide.Player));
            Assert.That(brace.TypeLabel, Is.EqualTo("防御・自分"));
            Assert.That(brace.TraitText, Is.EqualTo("残6 回復+1"));
            Assert.That(brace.TraitLit, Is.True, "10 − 2 = 8 left");
            Assert.That(brace.RequiredRangeGlyph, Is.EqualTo(""), "a self card reads no reach");

            CardFace bodyCheck = frame.Hand.First(c => c.Name == "体当たり");
            Assert.That(bodyCheck.Description, Is.EqualTo("敵に 5 ダメージ。鈍足を 2 付与する。"), "§5: 「<語> を n 付与する」");
            Assert.That(bodyCheck.TraitText, Is.EqualTo("間合い0 重撃"));
            Assert.That(bodyCheck.RequiredRangeGlyph, Is.EqualTo("0〜1"), "#179: v4.3 reaches 0〜1");

            CardFace feint = frame.Hand.First(c => c.Name == "牽制");
            Assert.That(feint.Description, Is.EqualTo("敵に 10 ダメージ。後ろへ 1 動く。"));
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
            Assert.That(ev.PreviewText, Is.EqualTo("20"));
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
            Assert.That(slash.Amount, Is.EqualTo(20));
            Assert.That(slash.Intensity, Is.EqualTo(EffectStrength.Uniform), "§5.3: one strength for every beat (#30)");
            Assert.That(slash.HpAfter, Is.EqualTo(40));

            Assert.That(ev.After.Enemy.Hp, Is.EqualTo(40));
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
            Assert.That(move.GapAfter, Is.EqualTo(1), "#577: the move is drawn on the floor, N 1 after it");
            Assert.That(EffectPlan.MovesOnFloor(move), Is.True);
            Assert.That(move.RangeGlyphAfter, Is.EqualTo("1"));
            Cue guard = ev.Cues[4];
            Assert.That(guard.Amount, Is.EqualTo(3));
            Assert.That(guard.GuardAfter, Is.EqualTo(3));
        }

        [Test]
        public void ATraitsStaminaPoint_PlaysAfterTheBlow_NotBeforeIt()
        {
            // battle_ui_ux_v2 §5.9 (#588): the core gives the pip back as soon as the trait is judged; the
            // screen shows it after every face. 小手打ち at gap 0: pay 1, the cue, the cut, then +1.
            var (state, writer) = Begin(0, CardCatalog.WristCut, CardCatalog.WristCut);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);

            StepResult play = TurnLoop.PlayCard(begin.State, InHand(begin.State, "wrist_cut"), NoShuffle);
            DepictionEvent ev = writer.Write(play.Events, play.State).Single();

            Assert.That(Kinds(ev), Is.EqualTo(new[] { CueKind.StaminaChange, CueKind.TraitFire, CueKind.Slash, CueKind.StaminaChange }));
            Assert.That(ev.Cues[0].Amount, Is.EqualTo(-1));
            Assert.That(ev.Cues[0].StaminaAfter, Is.EqualTo(9));
            Cue gain = ev.Cues[3];
            Assert.That(gain.Amount, Is.EqualTo(1));
            Assert.That(gain.StaminaAfter, Is.EqualTo(10), "the core's settled value, copied as it was");
            Assert.That(gain.Fx, Is.EqualTo(FxSystem.Breath), "the trait's 息 (§5.9 順 2)");
            Assert.That(ev.After.Player.Stamina, Is.EqualTo(10));

            List<EffectId> steps = EffectPlan.StepsOf(ev).Select(s => s.Id).ToList();
            Assert.That(steps.Last(), Is.EqualTo(EffectId.StaminaRecover), "the pip lights once the cut has played out");
            Assert.That(steps.IndexOf(EffectId.StaminaRecover), Is.GreaterThan(steps.IndexOf(EffectId.HpDrain)));
        }

        /// <summary>A beat of a face (§2.2's Attack → Move → Guard → Skill → Stance), apart from the stamina a face gives.</summary>
        private static bool IsFaceBeat(Cue cue)
        {
            switch (cue.Kind)
            {
                case CueKind.Slash:
                case CueKind.GuardBlock:
                case CueKind.Hit:
                case CueKind.GuardGain:
                case CueKind.RangeSwitch:
                case CueKind.StatusChange:
                case CueKind.HpChange:
                    return true;
                case CueKind.TraitFire:
                    return cue.Text != null && cue.Text.StartsWith("構え・"); // the stance face (StanceSet)
                default:
                    return false;
            }
        }

        /// <summary>
        /// The card's play from the first board on which its stamina trait fires and a face plays, or null: gaps 0〜4 in turn,
        /// each as the turn's first card, as its third after two attacks (初手 / 決め / 連動 攻撃), and with
        /// 再生 already on the player (息継ぎ's 再生中).
        /// </summary>
        private static DepictionEvent PlayWithStaminaTraitFired(CardDef card)
        {
            var boards = new System.Func<CombatantState, CombatantState>[]
            {
                p => p,
                p => p with { Played = new[] { BattleAttribute.Attack, BattleAttribute.Attack } },
                p => p with { Statuses = StatusSet.Of((StatusKind.Regen, 2)) },
            };
            for (int gap = 0; gap <= GrammarSamples.MaxGap; gap++)
            {
                foreach (var board in boards)
                {
                    var (state, writer) = Begin(gap, Enumerable.Repeat(card, Constants.HandDraw + 2).ToArray()); // a draw face has a card to draw
                    StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
                    writer.Write(begin.Events, begin.State);
                    BattleState ready = begin.State with { Player = board(begin.State.Player) };
                    string id = InHand(ready, card.Id);
                    if (TurnLoop.CanPlay(ready, id) != PlayRefusal.None) continue;
                    StepResult play = TurnLoop.PlayCard(ready, id, NoShuffle);
                    bool fired = play.Events.OfType<TraitEvaluated>()
                        .Any(t => t.Trait.Effect == TraitEffect.StaminaGain && t.Outcome.Triggered);
                    if (!fired) continue;
                    DepictionEvent ev = writer.Write(play.Events, play.State).Single(e => e.Kind == DepictionEventKind.PlayCard);
                    if (ev.Cues.Any(IsFaceBeat)) return ev; // 足運び at gap 0 has nowhere to step
                }
            }
            return null;
        }

        [Test]
        public void EveryCardWithAStaminaTrait_ShowsTheTraitsPipAfterAllItsFaces()
        {
            // §5.9 (#588): read off the cards themselves, so a card that gains the trait later is held too.
            List<CardDef> cards = CardCatalog.All
                .Where(d => d.AllTraits.Any(t => t.Effect == TraitEffect.StaminaGain))
                .ToList();
            Assert.That(cards, Has.Count.EqualTo(8), "the swordsman's eight (5 initial, 3 learned) when #588 was filed");

            var report = new System.Text.StringBuilder();
            foreach (CardDef card in cards)
            {
                DepictionEvent ev = PlayWithStaminaTraitFired(card);
                Assert.That(ev, Is.Not.Null, card.Name + ": no board 0〜" + GrammarSamples.MaxGap + " fires its trait");
                int trait = ev.Cues.FindIndex(c => c.Kind == CueKind.TraitFire && !IsFaceBeat(c));
                int firstGain = ev.Cues.FindIndex(c => c.Kind == CueKind.StaminaChange && c.Amount > 0);
                int lastFace = ev.Cues.FindLastIndex(IsFaceBeat);
                Assert.That(ev.Cues[0].Kind, Is.EqualTo(CueKind.StaminaChange), card.Name + ": the cost first");
                Assert.That(ev.Cues[0].Amount, Is.LessThan(0), card.Name + ": the cost first");
                Assert.That(trait, Is.EqualTo(1), card.Name + ": the trait's cue right after the cost");
                Assert.That(lastFace, Is.GreaterThan(trait), card.Name + ": the card has a face beat");
                Assert.That(firstGain, Is.GreaterThan(lastFace), card.Name + ": every stamina gain after the last face (" + string.Join(", ", Kinds(ev)) + ")");
                int draw = ev.Cues.FindIndex(c => c.Kind == CueKind.DrawHand);
                if (draw >= 0) Assert.That(draw, Is.GreaterThan(firstGain), card.Name + ": §5.9's later group in its table's order, the pip and then the draw");
                // The pips step up by the core's own numbers, in the core's order.
                int pips = ev.Cues[0].StaminaAfter;
                foreach (Cue gain in ev.Cues.Where(c => c.Kind == CueKind.StaminaChange && c.Amount > 0))
                {
                    Assert.That(gain.StaminaAfter, Is.EqualTo(pips + gain.Amount), card.Name + ": the pips never step back");
                    pips = gain.StaminaAfter;
                }
                Assert.That(ev.After.Player.Stamina, Is.EqualTo(pips), card.Name);
                Assert.That(EffectPlan.BlockingMs(ev, EffectSwitches.AllOn()), Is.LessThan(2000f), card.Name + ": §5.0's 2.0 s");
                report.Append(card.Name).Append(": ").Append(string.Join(", ", Kinds(ev))).Append('\n');
            }
            TestContext.Out.Write(report.ToString());
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
        public void ACappedTopUp_BeatsOnlyWhatLanded_AndAFullWordGetsNoBeat()
        {
            // #205: a ターンで減る型 word stops at four. The beat shows what the core says landed.
            List<CardInstance> deck = Cards.BuildDeck(new[] { CardCatalog.SecondWind }, 2);
            deck.AddRange(Cards.BuildDeck(new[] { CardCatalog.Brace, CardCatalog.Feint, CardCatalog.KesaCut }, 1));
            var setup = new BattleSetup(Enemies.PolearmWarped, deck, BattleSetup.SliceFieldCells, StartGap: 3);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            state = state with { Player = state.Player with { Statuses = StatusSet.Of((StatusKind.Regen, 3)) } };
            var writer = new CoreScriptWriter(setup.Enemy);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);
            Assert.That(begin.State.Player.Statuses.Stacks(StatusKind.Regen), Is.EqualTo(2), "the turn start ticked 3 → 2");

            StepResult first = TurnLoop.PlayCard(begin.State, InHand(begin.State, "second_wind"), NoShuffle);
            Cue topUp = writer.Write(first.Events, first.State).Single().Cues.Single(c => c.Kind == CueKind.StatusChange);
            Assert.That(topUp.Amount, Is.EqualTo(2), "2 + 3 stops at 4: two of the three land");
            Assert.That(topUp.StacksAfter, Is.EqualTo(4));
            Assert.That(StatusBeat.Of(topUp), Is.EqualTo(EffectId.StatusStack), "the held chip grows; it is not a new one");

            StepResult second = TurnLoop.PlayCard(first.State, InHand(first.State, "second_wind"), NoShuffle);
            DepictionEvent full = writer.Write(second.Events, second.State).Single();
            Assert.That(full.Cues.Any(c => c.Kind == CueKind.StatusChange), Is.False, "4 stays 4: no chip beat");
            Assert.That(full.After.Player.Statuses.Single(c => c.Label == "再生").Stacks, Is.EqualTo(4));
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

            // The shove: 6 into Guard 3 → 3 blocked, 3 through; the two-cell push moves the player one
            // (cell 1), and the wall takes 3 more in an event of its own (§7.3).
            DepictionEvent shove = events[1];
            Assert.That(shove.Title, Is.EqualTo("柄尻の押し込み"));
            Assert.That(Kinds(shove), Is.EqualTo(new[]
            {
                CueKind.EnemyWindup, CueKind.Slash, CueKind.GuardBlock, CueKind.Hit, CueKind.RangeSwitch,
            }));
            Assert.That(shove.Cues[1].Source, Is.EqualTo(UnitSide.Enemy));
            Assert.That(shove.Cues[1].HpAfter, Is.EqualTo(Cue.Unchanged), "an enemy slash is the swing alone");
            Assert.That(shove.Cues[2].Amount, Is.EqualTo(3));
            Assert.That(shove.Cues[2].GuardAfter, Is.EqualTo(0));
            Assert.That(shove.Cues[3].Amount, Is.EqualTo(3));
            Assert.That(shove.Cues[3].HpAfter, Is.EqualTo(47));
            Assert.That(shove.Cues[4].Target, Is.EqualTo(UnitSide.Player));
            Assert.That(shove.Cues[4].RangeGlyphAfter, Is.EqualTo("1"));
            Assert.That(shove.After.Omen.Visible, Is.False, "the omen is spent");
            Assert.That(shove.After.Enemy.Guard, Is.EqualTo(0), "its 構え has not played yet");

            DepictionEvent wall = events[2];
            Assert.That(wall.Title, Is.EqualTo("壁に当たる"));
            Assert.That(Kinds(wall), Is.EqualTo(new[] { CueKind.Hit }));
            Assert.That(wall.Cues[0].Target, Is.EqualTo(UnitSide.Player));
            Assert.That(wall.Cues[0].Amount, Is.EqualTo(3));
            Assert.That(wall.Cues[0].HpAfter, Is.EqualTo(44));
            Assert.That(wall.After.Player.Hp, Is.EqualTo(44));

            // Next omen: the enemy's 構え goes up, then the sweep is announced from the 1〜2 branch.
            DepictionEvent next = events[3];
            Assert.That(Kinds(next), Is.EqualTo(new[] { CueKind.GuardGain, CueKind.OmenShow }));
            Assert.That(next.Cues[0].Target, Is.EqualTo(UnitSide.Enemy));
            Assert.That(next.Cues[0].GuardAfter, Is.EqualTo(3));
            Assert.That(next.After.Enemy.Guard, Is.EqualTo(3));
            Assert.That(next.After.Omen.KindLabel + "・" + next.After.Omen.SideGlyph, Is.EqualTo("攻撃・1〜2"));
            Assert.That(next.After.Omen.Icon, Is.EqualTo(OmenIcon.Attack));
            Assert.That(next.After.Omen.ValueText, Is.EqualTo("9"));
        }

        // ---- an enemy that strikes and then moves itself (#587) ----

        /// <summary>Every enemy action that strikes and then moves its own cells (§5.4 順 2: 退き撃ち and the like), read off the roster.</summary>
        private static List<(EnemyDef Enemy, EnemyActionDef Action)> StrikeThenMove()
        {
            var found = new List<(EnemyDef, EnemyActionDef)>();
            foreach (EnemyDef enemy in Enemies.All)
            {
                foreach (EnemyActionDef action in enemy.Actions.Values)
                {
                    if (action.Face.Power > 0 && action.Face.Move != 0) found.Add((enemy, action));
                }
            }
            return found;
        }

        /// <summary>
        /// The enemy carries out <paramref name="action"/> against a player holding <paramref name="guard"/>, from
        /// the first gap in its reach where it moves after the blow; the event its blow plays in, or null.
        /// The player ends the turn with no stamina, so 構え adds no Guard of its own.
        /// </summary>
        private static DepictionEvent StrikeThenMoveEvent(EnemyDef enemy, EnemyActionDef action, int guard)
        {
            Reach reach = action.Face.ReachOrDefault;
            for (int gap = reach.Min; gap <= reach.Max; gap++)
            {
                var setup = new BattleSetup(enemy, Cards.BuildDeck(new[] { CardCatalog.Brace }, Constants.HandDraw * 2),
                    Constants.FieldCellsMax, StartGap: gap);
                BattleState state = TurnLoop.Start(setup, NoShuffle).State;
                if (state.Gap != gap) continue;
                var writer = new CoreScriptWriter(setup.Enemy);
                writer.Opening(state);
                StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
                writer.Write(begin.Events, begin.State);

                EnemyUnit unit = begin.State.Enemies[0];
                BattleState ready = begin.State with { Player = begin.State.Player with { Stamina = 0, Guard = guard } };
                ready = ready.WithUnit(0, unit with { Omen = new Omen(action.Id, EnemyAi.LabelOf(action)) });
                StepResult end = TurnLoop.EndTurn(ready, NoShuffle);
                bool moved = end.Events.OfType<CellsMoved>().Any(m => m.Actor == Actor.Enemy && !m.Pushed);
                if (!moved) continue;
                return writer.Write(end.Events, end.State)
                    .FirstOrDefault(e => e.Cues.Any(c => c.Kind == CueKind.Slash && c.Source == UnitSide.Enemy) && e.Title == action.Name);
            }
            return null;
        }

        [Test]
        public void AnEnemyThatStrikesThenMoves_GoesStraightIntoTheMove_WithNoReturn()
        {
            // §5.4 順 2 (#587): no §5.5 順 8 return; the move starts on the number + 80, in the blow's own event.
            List<(EnemyDef Enemy, EnemyActionDef Action)> actions = StrikeThenMove();
            Assert.That(actions.Select(a => a.Action.Id), Is.EquivalentTo(new[]
            {
                "lunge_in", "press", "backstep", "step_slash", "crystal_rush", "recoil_thrust", "push_shield", "pounce",
            }), "the eight of #587");

            var report = new System.Text.StringBuilder();
            foreach (var (enemy, action) in actions)
            {
                string name = enemy.Name + " " + action.Name;
                // Guard 0: swing, wound, move. A little Guard: swing, shield, wound, move.
                foreach (int guard in new[] { 0, 2 })
                {
                    DepictionEvent ev = StrikeThenMoveEvent(enemy, action, guard);
                    Assert.That(ev, Is.Not.Null, name + " Guard " + guard + ": never moves after its blow");
                    int hit = ev.Cues.FindIndex(c => c.Kind == CueKind.Hit && c.Target == UnitSide.Player);
                    Assert.That(hit, Is.GreaterThan(0), name + " Guard " + guard + ": a wound");
                    if (guard > 0) Assert.That(ev.Cues[hit - 1].Kind, Is.EqualTo(CueKind.GuardBlock), name + ": the shield before the wound");
                    Assert.That(ev.Cues[hit + 1].Kind, Is.EqualTo(CueKind.RangeSwitch), name + " Guard " + guard + ": the move in the same event, right after the wound");
                    Assert.That(ev.Cues[hit + 1].Pushed, Is.False, name);
                    Assert.That(EffectPlan.EnemyMoveFollows(ev, ev.Cues[hit]), Is.True, name);

                    List<EffectStep> steps = EffectPlan.StepsOf(ev);
                    Assert.That(steps.Select(s => s.Id), Has.No.Member(EffectId.EnemyReturn), name + " Guard " + guard);
                    Assert.That(steps.Single(s => s.Id == EffectId.HpDrain).LeadMs, Is.EqualTo(EffectPlan.NextFaceLeadMs), name + " Guard " + guard);
                    float ms = EffectPlan.BlockingMs(ev, EffectSwitches.AllOn());
                    Assert.That(ms, Is.LessThan(2000f), name + " Guard " + guard + ": §5.0's 2.0 s for the 1 手");
                    report.Append(name).Append(" Guard ").Append(guard).Append(": ").Append(string.Join(", ", Kinds(ev)))
                        .Append(" = ").Append(ms).Append(" ms\n");
                }

                // Guard takes all of it: the shield, then the move from where the enemy struck — no return to wait for.
                DepictionEvent blocked = StrikeThenMoveEvent(enemy, action, 99);
                Assert.That(blocked, Is.Not.Null, name + " Guard 99");
                int block = blocked.Cues.FindIndex(c => c.Kind == CueKind.GuardBlock);
                Assert.That(block, Is.GreaterThan(0), name + " Guard 99: the shield");
                Assert.That(blocked.Cues.Any(c => c.Kind == CueKind.Hit), Is.False, name + " Guard 99: no wound");
                Assert.That(blocked.Cues[block + 1].Kind, Is.EqualTo(CueKind.RangeSwitch), name + " Guard 99: the move right after the shield");
                Assert.That(EffectPlan.StepsOf(blocked).Select(s => s.Id), Has.No.Member(EffectId.EnemyReturn), name + " Guard 99");
                Assert.That(EffectPlan.BlockingMs(blocked, EffectSwitches.AllOn()), Is.LessThan(2000f), name + " Guard 99");
                report.Append(name).Append(" Guard 99: ").Append(string.Join(", ", Kinds(blocked))).Append('\n');
            }
            TestContext.Out.Write(report.ToString());
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

            Assert.That(sweep.Title, Is.EqualTo("払い斬り"));
            Cue miss = sweep.Cues.Single(c => c.Kind == CueKind.SideBonusMiss);
            Assert.That(miss.Text, Is.EqualTo("+3"));
            Assert.That(sweep.Cues.Any(c => c.Kind == CueKind.TraitFire), Is.False);
            Assert.That(sweep.Cues.Single(c => c.Kind == CueKind.Slash).Amount, Is.EqualTo(9));
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
            Assert.That(afterRush.Floor.Gap, Is.EqualTo(0));

            StepResult end = TurnLoop.EndTurn(play.State, NoShuffle);
            List<DepictionEvent> events = writer.Write(end.Events, end.State);
            DepictionEvent sweep = events[1];

            Assert.That(sweep.Title, Is.EqualTo("払い斬り"));
            Assert.That(Kinds(sweep), Is.EqualTo(new[] { CueKind.EnemyWindup, CueKind.SideBonusMiss }), "one strike-off, not two");
            Assert.That(sweep.Cues[1].Text, Is.EqualTo("空振り"));
            Assert.That(sweep.Cues[1].Amount, Is.EqualTo(9));
            Assert.That(sweep.After.Player.Hp, Is.EqualTo(50));
            Assert.That(events[2].After.Omen.KindLabel + "・" + events[2].After.Omen.SideGlyph, Is.EqualTo("攻撃・0"), "the adjacent branch now");
        }

        [Test]
        public void TheEnemysStep_IsDrawnOnTheFloor_AndCountsTheGapAgain()
        {
            // Gap 3: the polearm steps in. The move is drawn on the floor (#577): the enemy's cell moves,
            // the player's stays, and N is counted again.
            var (state, writer) = Begin(3, FiveCards);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);

            StepResult end = TurnLoop.EndTurn(begin.State, NoShuffle);
            DepictionEvent step = writer.Write(end.Events, end.State)[1];

            Assert.That(step.Title, Is.EqualTo("踏み込み"));
            Assert.That(Kinds(step), Is.EqualTo(new[] { CueKind.EnemyWindup, CueKind.RangeSwitch, CueKind.GuardGain }));
            Assert.That(step.Cues[1].Target, Is.EqualTo(UnitSide.Player));
            Assert.That(step.Cues[1].RangeGlyphAfter, Is.EqualTo("2"));
            Assert.That(step.Cues[1].GapAfter, Is.EqualTo(2));
            Assert.That(step.Cues[1].PlayerCellAfter, Is.EqualTo(begin.State.Player.Cell), "the player stands where it stood");
            Assert.That(step.Cues[1].EnemyCellAfter, Is.EqualTo(begin.State.Enemy.Cell - 1), "the enemy one cell in");
            Assert.That(step.Cues[1].CellsMoved, Is.EqualTo(1));
            Assert.That(step.After.Player.RangeGlyph, Is.EqualTo("2"));
            Assert.That(step.After.Floor.Gap, Is.EqualTo(2));
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
        public void AGuardOmen_IsWrittenWithTheShieldAndTheFaceGuard()
        {
            BattleState state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle).State;
            var writer = new CoreScriptWriter(state.EnemyDef);
            writer.Opening(state);
            EnemyActionDef guardUp = Enemies.PolearmWarped.Actions["guard_up"];

            var stream = new List<BattleEvent>
            {
                new TurnStarted(Actor.Player, 2),
                new OmenSet(Actor.Enemy, new Omen(guardUp.Id, EnemyAi.LabelOf(guardUp)), false),
            };
            OmenFrame omen = writer.Write(stream, state).Single().After.Omen;

            // Not the omen the state stands on, so no core preview is taken: the face's Guard is shown.
            Assert.That(omen.Icon, Is.EqualTo(OmenIcon.Guard));
            Assert.That(omen.KindLabel, Is.EqualTo("防御"));
            Assert.That(omen.ValueText, Is.EqualTo("3"));
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
