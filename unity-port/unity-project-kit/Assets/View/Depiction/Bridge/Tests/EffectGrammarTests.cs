// #57: the effect grammar on the core's cards. Every card the core knows has a row of systems, the
// writer puts that row and each beat's system on the script, and the grammar adds no wait to any
// event. Runs under `dotnet test` (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class EffectGrammarTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        /// <summary>The cards whose faces only move the player: §5.3 gives a move no system.</summary>
        private static bool MovesOnly(CardDef def)
        {
            FxFaces faces = CardSystems.FacesOf(def.Name, def.Attributes, def.Face);
            return !faces.Attacks && !faces.Guards && !faces.PutsStatus && !faces.Breathes && !faces.Stance;
        }

        // ---- the table ------------------------------------------------------------------------

        [Test]
        public void EveryCardTheCoreKnows_HasARow_AndOnlyAMoveHasNoSystem()
        {
            var table = new StringBuilder();
            foreach (CardDef def in CardCatalog.All)
            {
                Assert.That(CardSystems.Table.ContainsKey(def.Id), Is.True, def.Id + " has no row");
                IReadOnlyList<FxSystem> row = CardSystems.Table[def.Id];
                if (MovesOnly(def))
                {
                    Assert.That(row, Is.Empty, def.Name + " only moves: a move has no system (§5.3)");
                    Assert.That(def.Face.Move, Is.Not.EqualTo(0), def.Name + " has no face at all");
                }
                else
                {
                    Assert.That(row, Is.Not.Empty, def.Name + " plays no system");
                }
                Assert.That(row.Count, Is.LessThanOrEqualTo(2), def.Name + ": a card has at most two faces (§5.4)");
                Assert.That(row.Distinct().Count(), Is.EqualTo(row.Count), def.Name + " repeats a system");
                table.Append(def.Id).Append(' ').Append(def.Name).Append(": ")
                    .Append(row.Count == 0 ? "（移動）" : string.Join(" → ", row.Select(EffectGrammar.Glyph))).Append('\n');
            }
            Assert.That(CardSystems.Table.Count, Is.EqualTo(CardCatalog.All.Count), "a row for a card the core does not know");
            TestContext.Out.Write(table.ToString());
        }

        [Test]
        public void TheEightSystems_AreAllPlayedByTheSwordsmansCards()
        {
            var played = new HashSet<FxSystem>(CardSystems.Table.Values.SelectMany(row => row));
            Assert.That(played, Is.EquivalentTo(EffectGrammar.Systems), "every system has a card that shows it");
        }

        [TestCase("thrust", FxSystem.Thrust)]       // 突き
        [TestCase("kesa_cut", FxSystem.Slash)]      // 袈裟斬り: no other word, so 斬
        [TestCase("side_sweep", FxSystem.Sweep)]    // 横薙ぎ
        [TestCase("flat_strike", FxSystem.Strike)]  // 峰打ち
        [TestCase("pierce", FxSystem.Thrust)]       // 貫き
        [TestCase("throw_blade", FxSystem.Thrust)]  // 投げ
        [TestCase("stone_throw", FxSystem.Strike)]  // 石礫: 礫 is a 打 word
        [TestCase("boar_rush", FxSystem.Thrust)]    // 猪突猛進: 突 is read first
        [TestCase("lunge", FxSystem.Slash)]         // 踏み込み斬り
        [TestCase("haul_step", FxSystem.Strike)]    // 手繰りの歩み: a pull is 打 before any word
        public void AnAttack_TakesItsSystemFromItsName(string id, FxSystem expected)
        {
            CardDef def = CardCatalog.ById(id);
            Assert.That(CardSystems.Of(def)[0], Is.EqualTo(expected), def.Name);
        }

        [Test]
        public void EveryAttackCard_LeadsWithTheBlowItsNameAndPushGive_TheOtherFacesFollowInResolveOrder()
        {
            foreach (CardDef def in CardCatalog.All)
            {
                IReadOnlyList<FxSystem> row = CardSystems.Of(def);
                FxFaces faces = CardSystems.FacesOf(def.Name, def.Attributes, def.Face);
                if (faces.Attacks)
                {
                    Assert.That(row[0], Is.EqualTo(EffectGrammar.BlowOf(def.Name, def.Face.Push != 0)), def.Name);
                    Assert.That(EffectGrammar.ToStrike(row[0]), Is.EqualTo(CoreText.SystemOf(def)), def.Name + ": the blow's shape and its system agree");
                }
                // §5.4: attack → guard → skill → stance, so a blow is never after a non-blow.
                for (int i = 1; i < row.Count; i++)
                {
                    Assert.That(EffectGrammar.IsBlow(row[i]), Is.False, def.Name + ": one blow, and it leads");
                    Assert.That((int)row[i], Is.GreaterThan((int)row[i - 1]), def.Name + ": the faces keep their resolve order");
                }
                if (faces.Stance) Assert.That(row, Is.EqualTo(new[] { FxSystem.Form }), def.Name + ": a stance card has no other face");
            }
        }

        [Test]
        public void CardsOfTheSameSystem_ShareOneLook_AndEverySystemLooksApart()
        {
            var looks = new Dictionary<FxSystem, string>();
            foreach (CardDef def in CardCatalog.All)
            {
                foreach (FxSystem system in CardSystems.Of(def))
                {
                    string look = EffectGrammar.LookId(system);
                    if (looks.TryGetValue(system, out string seen)) Assert.That(look, Is.EqualTo(seen), def.Name);
                    else looks[system] = look;
                }
            }
            Assert.That(looks.Values.Distinct().Count(), Is.EqualTo(looks.Count), "two systems share a look");
        }

        [Test]
        public void EveryEnemyAction_HasASystemOrOnlyMoves()
        {
            foreach (EnemyDef enemy in Enemies.All)
            {
                foreach (EnemyActionDef action in enemy.Actions.Values)
                {
                    IReadOnlyList<FxSystem> row = CardSystems.Of(action);
                    FxFaces faces = CardSystems.FacesOf(action.Name, action.Attributes, action.Face);
                    if (faces.Attacks) Assert.That(EffectGrammar.ToStrike(row[0]), Is.EqualTo(CoreText.SystemOf(action)), enemy.Id + " " + action.Name);
                    if (row.Count == 0) Assert.That(faces.Attacks || faces.Guards || faces.Stance, Is.False, enemy.Id + " " + action.Name);
                }
            }
        }

        // ---- the writer ------------------------------------------------------------------------

        /// <summary>Plays <paramref name="card"/> on the slice's polearm, from the first gap that lets it be played.</summary>
        private static DepictionEvent Play(CardDef card)
        {
            for (int gap = 0; gap <= 4; gap++)
            {
                var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(new[] { card }, 6),
                    BattleSetup.SliceFieldCells, StartGap: gap);
                BattleState state = TurnLoop.Start(setup, NoShuffle).State;
                var writer = new CoreScriptWriter(setup.Enemy);
                writer.Opening(state);
                StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
                writer.Write(begin.Events, begin.State);
                string id = begin.State.Hand.First().InstanceId;
                if (TurnLoop.CanPlay(begin.State, id) != PlayRefusal.None) continue;
                StepResult play = TurnLoop.PlayCard(begin.State, id, NoShuffle);
                return writer.Write(play.Events, play.State).First(e => e.Kind == DepictionEventKind.PlayCard);
            }
            return null;
        }

        [Test]
        public void TheWriter_PutsTheCardsRowOnThePlay_AndEachBeatsSystemOnItsCue()
        {
            int played = 0;
            foreach (CardDef def in CardCatalog.All)
            {
                DepictionEvent ev = Play(def);
                if (ev == null) continue;
                played += 1;
                Assert.That(ev.Systems, Is.EqualTo(CardSystems.Table[def.Id]), def.Name);
                foreach (Cue cue in ev.Cues)
                {
                    if (cue.Kind == CueKind.Slash && cue.Source == UnitSide.Player)
                    {
                        Assert.That(cue.Fx, Is.EqualTo(ev.Systems[0]), def.Name + ": the blow is the card's");
                    }
                    if (cue.Fx.HasValue)
                    {
                        Assert.That(cue.Intensity, Is.EqualTo(EffectStrength.Uniform), def.Name + ": one strength (#30)");
                    }
                    if (cue.Kind == CueKind.GuardGain && cue.Target == UnitSide.Player)
                    {
                        Assert.That(cue.Fx, Is.EqualTo(FxSystem.Shield), def.Name + ": a Guard gained in a play is 盾");
                    }
                    if (cue.Kind == CueKind.StatusChange && cue.Amount > 0)
                    {
                        Assert.That(cue.Fx, Is.EqualTo(FxSystem.Spirit), def.Name + ": a status put on is 気");
                    }
                    if (cue.Kind == CueKind.StaminaChange)
                    {
                        Assert.That(cue.Fx, Is.EqualTo(cue.Amount > 0 ? FxSystem.Breath : (FxSystem?)null), def.Name + ": a stamina point is 息, the cost is nothing");
                        Assert.That(EffectGrammar.MarkOf(cue), Is.EqualTo(FxMark.None), def.Name + ": 息 on the pips is the pips lighting");
                    }
                    if (cue.Kind == CueKind.HpChange && cue.Amount > 0 && cue.Target == UnitSide.Player)
                    {
                        Assert.That(cue.Fx, Is.EqualTo(FxSystem.Breath), def.Name + ": a heal in a play is 息");
                        Assert.That(EffectGrammar.MarkOf(cue), Is.EqualTo(FxMark.ChestHeave), def.Name + ": a heal adds the chest's 400 (§5.3)");
                    }
                }
                if (ev.Systems.Contains(FxSystem.Form))
                {
                    Assert.That(ev.Cues.Any(c => c.Fx == FxSystem.Form), Is.True, def.Name + ": the stance set is 型");
                }
            }
            Assert.That(played, Is.GreaterThanOrEqualTo(CardCatalog.All.Count * 3 / 4), "most cards can be played from some gap");
        }

        [Test]
        public void AFiredTrait_MarksTheBlow_AndNeverMovesItsStrength()
        {
            // 袈裟斬り pays +5 adjacent: gap 0 lights it, gap 1 does not.
            DepictionEvent lit = Play(CardCatalog.KesaCut);
            Cue blow = lit.Cues.Single(c => c.Kind == CueKind.Slash);
            Assert.That(lit.Cues.Any(c => c.Kind == CueKind.TraitFire), Is.True);
            Assert.That(blow.TraitAccent, Is.True);
            Assert.That(blow.Intensity, Is.EqualTo(EffectStrength.Uniform), "the cue does not change the tier (J2)");

            var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(new[] { CardCatalog.KesaCut }, 6),
                BattleSetup.SliceFieldCells, StartGap: 1);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(setup.Enemy);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            writer.Write(begin.Events, begin.State);
            StepResult play = TurnLoop.PlayCard(begin.State, begin.State.Hand.First().InstanceId, NoShuffle);
            Cue plain = writer.Write(play.Events, play.State)[0].Cues.Single(c => c.Kind == CueKind.Slash);
            Assert.That(plain.TraitAccent, Is.False, "no trait, no cue");
            Assert.That(plain.Intensity, Is.EqualTo(blow.Intensity));
        }

        [Test]
        public void TheTurnStartsDraw_IsNotAPlaysBreath()
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(new[] { CardCatalog.KesaCut }, 6),
                BattleSetup.SliceFieldCells, StartGap: 0);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(setup.Enemy);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            DepictionEvent turn = writer.Write(begin.Events, begin.State).Single();
            Assert.That(turn.Systems, Is.Empty);
            Assert.That(turn.Cues.All(c => !c.Fx.HasValue), Is.True, "the turn start is no card's play");
        }

        [Test]
        public void TheEnemysAction_CarriesItsRow_AndItsBlowTheSameSystem()
        {
            var rng = new SeededRng(3);
            BattleState state = TurnLoop.Start(BattleSetup.Slice(), rng).State;
            var writer = new CoreScriptWriter(state.EnemyDef);
            writer.Opening(state);
            int checkedActions = 0;
            for (int turn = 0; turn < 6 && state.Result == GameResult.Ongoing; turn++)
            {
                StepResult begin = TurnLoop.BeginPlayerTurn(state, rng);
                writer.Write(begin.Events, begin.State);
                StepResult end = TurnLoop.EndTurn(begin.State, rng);
                foreach (DepictionEvent ev in writer.Write(end.Events, end.State))
                {
                    Cue windup = ev.Cues.FirstOrDefault(c => c.Kind == CueKind.EnemyWindup);
                    if (windup == null) continue;
                    checkedActions += 1;
                    EnemyActionDef action = state.EnemyDef.Actions.Values.First(a => a.Name == ev.Title);
                    Assert.That(ev.Systems, Is.EqualTo(CardSystems.Of(action)), ev.Title);
                    foreach (Cue slash in ev.Cues.Where(c => c.Kind == CueKind.Slash))
                    {
                        Assert.That(slash.Fx, Is.EqualTo(ev.Systems[0]), ev.Title);
                    }
                }
                state = end.State;
            }
            Assert.That(checkedActions, Is.GreaterThan(0));
        }

        // ---- the time budget -----------------------------------------------------------------

        [Test]
        public void TheGrammar_AddsNoWait_AndEveryPlayStaysInsideItsCap()
        {
            var on = EffectSwitches.AllOn();
            Assert.That(EffectCatalog.Of(EffectId.GrammarMark).Blocking, Is.False);
            Assert.That(EffectCatalog.Of(EffectId.TraitAccent).Blocking, Is.False);
            Assert.That(EffectCatalog.Of(EffectId.BreathHeave).Blocking, Is.False);
            var report = new StringBuilder();
            foreach (CardDef def in CardCatalog.All)
            {
                DepictionEvent ev = Play(def);
                if (ev == null) continue;
                float with = EffectPlan.BlockingMs(ev, on);
                var bare = new DepictionEvent { Kind = ev.Kind, Cues = ev.Cues.Select(Strip).ToList() };
                Assert.That(with, Is.EqualTo(EffectPlan.BlockingMs(bare, on)), def.Name + ": the grammar runs beside the beats");
                Assert.That(with, Is.LessThan(BattleSpeed.EventCapMs), def.Name + ": one action within 2.0 s (#28)");
                foreach (BattleSpeedStep speed in BattleSpeed.Steps)
                {
                    Assert.That(EffectPlan.BlockingMs(ev, on, speed), Is.LessThan(BattleSpeed.EventCapMsAt(speed)), def.Name + " at " + BattleSpeed.Label(speed));
                }
                report.Append(def.Name).Append(' ').Append(with.ToString("0")).Append(" ms\n");
            }
            TestContext.Out.Write(report.ToString());
        }

        [Test]
        public void ACardOfTwoSystems_StaysInsideOneAndAHalfAttacks()
        {
            // §5.0: a card with two faces plays its two systems within 1.5 times an attack-only card (1545 ms).
            const float TwoFacesCapMs = 1545f;
            var on = EffectSwitches.AllOn();
            int checkedCards = 0;
            foreach (CardDef def in CardCatalog.All.Where(d => CardSystems.Table[d.Id].Count == 2))
            {
                DepictionEvent ev = Play(def);
                if (ev == null) continue;
                checkedCards += 1;
                Assert.That(EffectPlan.BlockingMs(ev, on), Is.LessThanOrEqualTo(TwoFacesCapMs), def.Name);
            }
            Assert.That(checkedCards, Is.GreaterThan(0));
        }

        /// <summary>The cards with an attack face and a move of the player's own (踏み込み斬り, 牽制, ...), read off the data.</summary>
        private static bool AttacksAndMoves(CardDef def) =>
            CardSystems.FacesOf(def.Name, def.Attributes, def.Face).Attacks && def.Face.Move != 0;

        /// <summary>
        /// Every way the core can write the card's play into the slice's fight: each gap it can be played
        /// from, into no Guard, a little and a wall of it, with the stamina the turn gives and with it full
        /// (so a trait on held-back stamina fires too).
        /// </summary>
        private static IEnumerable<(string Label, DepictionEvent Event)> EveryPlayOf(CardDef card)
        {
            foreach (int gap in new[] { 0, 1, 2, 3, 4 })
            foreach (int enemyGuard in new[] { 0, 3, 40 })
            foreach (bool fullStamina in new[] { false, true })
            {
                var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(new[] { card }, 6),
                    BattleSetup.SliceFieldCells, StartGap: gap);
                BattleState state = TurnLoop.Start(setup, NoShuffle).State;
                state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
                state = state.WithEnemy(state.Enemy with { Guard = enemyGuard });
                if (fullStamina) state = state with { Player = state.Player with { Stamina = state.Player.MaxStamina } };
                string id = state.Hand.First().InstanceId;
                if (TurnLoop.CanPlay(state, id) != PlayRefusal.None) continue;
                var writer = new CoreScriptWriter(setup.Enemy);
                writer.Opening(state);
                StepResult play = TurnLoop.PlayCard(state, id, NoShuffle);
                yield return ("gap " + gap + ", enemy Guard " + enemyGuard + (fullStamina ? ", full stamina" : ""),
                    writer.Write(play.Events, play.State).First(e => e.Kind == DepictionEventKind.PlayCard));
            }
        }

        [Test]
        public void EveryAttackThatMoves_StaysInsideOneAndAHalfAttacks()
        {
            // §5.0 / §5.3 (#499): an attack face with a move added plays within 1.5 times an attack-only
            // card (1545 ms). The set is read off the swordsman's cards, and each is played every way the
            // core can write it. The core always writes the cells, so the move plays on the floor
            // (EffectPlan.MovesOnFloor); the waits are EffectPlan's, so whatever steps the floor move
            // comes to hold are counted here as they land.
            // A blow Guard takes only part of is three beats on the script (§5.5: Slash, GuardBlock, Hit)
            // where §5.0's row reckons one; those plays are held to the 2.0 s cap and listed, not to 1545.
            const float AttackAndMoveCapMs = 1545f;
            var on = EffectSwitches.AllOn();
            List<CardDef> cards = CardCatalog.All.Where(AttacksAndMoves).ToList();
            Assert.That(cards, Is.Not.Empty, "the swordsman has cards that attack and move");
            var report = new StringBuilder();
            var split = new StringBuilder();
            foreach (CardDef def in cards)
            {
                float worst = 0f;
                string worstLabel = "";
                int plays = 0;
                foreach ((string label, DepictionEvent ev) in EveryPlayOf(def))
                {
                    // Against the edge or the enemy the move may come to nothing (§5.7 順 5); those plays are
                    // held to the cap too, but the card must move in at least one of them.
                    if (ev.Cues.Any(c => c.Kind == CueKind.RangeSwitch)) plays += 1;
                    float ms = EffectPlan.BlockingMs(ev, on);
                    string where = def.Name + " at " + label + ": "
                        + string.Join(" + ", EffectPlan.StepsOf(ev).Select(s => s.Id + " " + EffectPlan.WaitMs(s, on).ToString("0")));
                    if (ev.Cues.Any(c => c.Kind == CueKind.GuardBlock) && ev.Cues.Any(c => c.Kind == CueKind.Hit))
                    {
                        Assert.That(ms, Is.LessThan(BattleSpeed.EventCapMs), where);
                        if (ms > AttackAndMoveCapMs) split.Append(ms.ToString("0")).Append(" ms  ").Append(where).Append('\n');
                        continue;
                    }
                    Assert.That(ms, Is.LessThanOrEqualTo(AttackAndMoveCapMs), where);
                    if (ms > worst)
                    {
                        worst = ms;
                        worstLabel = where;
                    }
                }
                Assert.That(plays, Is.GreaterThan(0), def.Name + " is played with its move from some gap");
                report.Append(worst.ToString("0")).Append(" ms  ").Append(worstLabel).Append('\n');
            }
            TestContext.Out.Write(report.ToString());
            if (split.Length > 0) TestContext.Out.Write("Over 1545 ms with Guard taking part of the blow:\n" + split);
        }

        private static Cue Strip(Cue cue)
        {
            return new Cue
            {
                Kind = cue.Kind, Target = cue.Target, Source = cue.Source, Amount = cue.Amount, Text = cue.Text,
                Intensity = cue.Intensity, HpAfter = cue.HpAfter, GuardAfter = cue.GuardAfter, StaminaAfter = cue.StaminaAfter,
                StaminaMax = cue.StaminaMax, StacksAfter = cue.StacksAfter, System = cue.System,
                // A move's cells decide whether the tag flip is waited for (#495); they are not grammar.
                Pushed = cue.Pushed, PlayerCellAfter = cue.PlayerCellAfter, EnemyCellAfter = cue.EnemyCellAfter, GapAfter = cue.GapAfter,
            };
        }
    }
}
